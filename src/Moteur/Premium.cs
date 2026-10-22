// Nettoyage PC - moteur : fonctions Premium (analyse du démarrage, kit de réinstallation)
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NettoyagePC
{
    internal partial class Moteur
    {
        // ------------------------------------------------------------ Analyse du démarrage
        /// <summary>
        /// Windows chronomètre chaque démarrage (journal Diagnostics-Performance, lisible avec l'autorisation administrateur) :
        /// durée totale, partie avant / après l'ouverture de session, et ce qui l'a ralenti (applications, pilotes, services, appareils).
        /// </summary>
        private void AnalyseDemarrage()
        {
            Ecrire("=== Analyse du démarrage ===");
            if (!Admin) { Ecrire("  Il faut l'autorisation administrateur pour lire le chronométrage des démarrages."); Donnees("demarrage", new Dictionary<string, object> { { "Admin", false } }); return; }
            var demarrages = new List<object>();
            var ralent = new List<Tuple<string, string, long, DateTime>>();   // type, nom, ms, date
            try
            {
                var q = new EventLogQuery("Microsoft-Windows-Diagnostics-Performance/Operational", PathType.LogName, "*[System[(EventID>=100 and EventID<=110) and TimeCreated[timediff(@SystemTime) <= 7776000000]]]") { ReverseDirection = true };
                using (var r = new EventLogReader(q))
                {
                    EventRecord e; int n = 0;
                    while ((e = r.ReadEvent()) != null && n++ < 2000)
                    {
                        using (e)
                        {
                            var d = DonneesEvenement(e);
                            DateTime date = e.TimeCreated ?? DateTime.Now;
                            if (e.Id == 100)
                            {
                                long total = Nombre(d, "BootTime"), principal = Nombre(d, "MainPathBootTime"), apres = Nombre(d, "BootPostBootTime");
                                if (total > 0 && total < 3600000)
                                    demarrages.Add(new Dictionary<string, object> { { "Date", date.ToString("yyyy-MM-dd HH:mm") }, { "Total", total }, { "Principal", principal }, { "Apres", apres } });
                            }
                            else
                            {
                                string type = e.Id == 101 ? "Application" : e.Id == 102 ? "Pilote" : e.Id == 103 ? "Service" : e.Id == 109 ? "Appareil" : e.Id == 106 ? "Optimisation de Windows" : "Autre";
                                string nom = Texte(d, "FriendlyName"); if (nom == "") nom = Texte(d, "Name"); if (nom == "") nom = Texte(d, "FileName");
                                long retard = Nombre(d, "DegradationTime"); if (retard <= 0) retard = Nombre(d, "TotalTime");
                                if (nom != "" && retard > 0) ralent.Add(Tuple.Create(type, nom, retard, date));
                            }
                        }
                    }
                }
            }
            catch (Exception ex) { Ecrire("  Journal des démarrages illisible : " + ex.Message); }
            var groupes = ralent.GroupBy(x => x.Item1 + "|" + x.Item2.ToLowerInvariant()).Select(g => new Dictionary<string, object>
            {
                { "Type", g.First().Item1 }, { "Nom", g.First().Item2 }, { "Fois", g.Count() }, { "Moyenne", (long)g.Average(x => x.Item3) }, { "Max", g.Max(x => x.Item3) }, { "Dernier", g.Max(x => x.Item4).ToString("yyyy-MM-dd") }
            }).OrderByDescending(x => (long)x["Moyenne"] * Math.Min(5, (int)x["Fois"])).Take(30).Cast<object>().ToList();
            object rapide = Registre.Lire(@"HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Power", "HiberbootEnabled");
            Ecrire("  " + demarrages.Count + " démarrage(s) chronométré(s) sur 90 jours, " + groupes.Count + " cause(s) de ralentissement repérée(s).");
            Donnees("demarrage", new Dictionary<string, object> { { "Admin", true }, { "Demarrages", demarrages.Take(60).ToArray() }, { "Ralentissements", groupes.ToArray() }, { "DemarrageRapide", rapide is int && (int)rapide == 1 } });
        }

        private static Dictionary<string, string> DonneesEvenement(EventRecord e)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var x = XDocument.Parse(e.ToXml());
                foreach (var el in x.Descendants().Where(el => el.Name.LocalName == "Data"))
                {
                    var n = el.Attribute("Name");
                    if (n != null && !d.ContainsKey(n.Value)) d[n.Value] = el.Value;
                }
            }
            catch { }
            return d;
        }
        private static long Nombre(Dictionary<string, string> d, string k) { string s; long v; return d.TryGetValue(k, out s) && long.TryParse(s, out v) ? v : 0; }
        private static string Texte(Dictionary<string, string> d, string k) { string s; return d.TryGetValue(k, out s) ? (s ?? "").Trim() : ""; }

        // ------------------------------------------------------------ Kit de réinstallation
        /// <summary>
        /// Prépare tout pour réinstaller Windows ou passer à un nouveau PC : liste des applications (réinstallables d'un coup
        /// avec winget), pilotes, réseaux Wi-Fi, et un script qui remet tout en place.
        /// </summary>
        private void KitReinstallation()
        {
            Ecrire("=== Kit de réinstallation ===");
            string dest = O.Destination;
            if (string.IsNullOrEmpty(dest) || !Directory.Exists(dest)) { Ecrire("  ATTENTION : destination introuvable (clé USB débranchée ?)."); return; }
            if (Protection.CheminProtege(dest) || !Protection.CheminSansLien(Path.Combine(dest, "x"))) { Ecrire("  ATTENTION : cette destination n'est pas autorisée (dossier du système ou lien)."); return; }
            string dossier = Path.Combine(dest, "Kit-NettoyagePC-" + Regex.Replace(Environment.MachineName, @"[^\w\-]", "_") + "-" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm"));
            if (Directory.Exists(dossier)) dossier += "-" + Guid.NewGuid().ToString("N").Substring(0, 4);
            Directory.CreateDirectory(dossier);
            if (!Protection.CheminSansLien(Path.Combine(dossier, "x"))) { Ecrire("  ATTENTION : dossier du kit invalide (lien) : arrêt."); return; }
            Ecrire("  Dossier du kit : " + dossier);
            var resume = new List<string>();

            // 1. Applications
            Ecrire("  Applications...");
            var progs = ProgrammesInstalles.Lister();
            var sb = new StringBuilder();
            sb.AppendLine("Applications installées sur " + Environment.MachineName + " le " + DateTime.Now.ToString("dd/MM/yyyy") + " (" + progs.Count + ")");
            sb.AppendLine("Celles qui existent dans winget sont réinstallées par Reinstaller.cmd ; les autres sont à réinstaller depuis le site de leur éditeur.");
            sb.AppendLine();
            foreach (var p in progs) sb.AppendLine(p.Nom + (p.Version != "" ? "  " + p.Version : "") + (p.Editeur != "" ? "  (" + p.Editeur + ")" : ""));
            File.WriteAllText(Path.Combine(dossier, "applications.txt"), sb.ToString(), new UTF8Encoding(true));
            string winget = InfosPc.Winget();
            bool okWinget = false;
            if (winget != null)
            {
                int code;
                Programmes.Executer(winget, "export -o \"" + Path.Combine(dossier, "applications-winget.json") + "\" --accept-source-agreements --disable-interactivity", 300000, Encoding.UTF8, out code);
                okWinget = File.Exists(Path.Combine(dossier, "applications-winget.json"));
                int nb = 0;
                try { nb = Regex.Matches(File.ReadAllText(Path.Combine(dossier, "applications-winget.json")), "\"PackageIdentifier\"").Count; } catch { }
                Ecrire(okWinget ? "    -> " + nb + " application(s) réinstallables automatiquement (winget), " + progs.Count + " listée(s) au total." : "    winget n'a pas pu exporter la liste (code " + code + ").");
                if (okWinget) resume.Add(nb + " applications réinstallables automatiquement");
            }
            else Ecrire("    winget absent : seule la liste des applications est enregistrée.");

            // 2. Pilotes
            Ecrire("  Pilotes (cela peut prendre quelques minutes)...");
            if (Admin)
            {
                string dp = Path.Combine(dossier, "pilotes");
                Directory.CreateDirectory(dp);
                int code;
                Programmes.Executer(Path.Combine(Chemins.Systeme32, "pnputil.exe"), "/export-driver * \"" + dp + "\"", 1800000, null, out code);
                int nb = 0; long taille = 0;
                try { nb = Directory.GetFiles(dp, "*.inf", SearchOption.AllDirectories).Length; taille = TailleFichiers.Dossier(dp); } catch { }
                Ecrire(nb > 0 ? "    -> " + nb + " pilote(s) sauvegardé(s) (" + T(taille) + ")." : "    Aucun pilote exporté (code " + code + ").");
                if (nb > 0) resume.Add(nb + " pilotes");
            }
            else Ecrire("    Ignoré : il faut l'autorisation administrateur.");

            // 3. Réseaux Wi-Fi
            string dw = Path.Combine(dossier, "wifi");
            Directory.CreateDirectory(dw);
            {
                int code;
                Programmes.Executer(Path.Combine(Chemins.Systeme32, "netsh.exe"), "wlan export profile folder=\"" + dw + "\"" + (O.Wifi && Admin ? " key=clear" : ""), 60000, null, out code);
                int nb = 0; try { nb = Directory.GetFiles(dw, "*.xml").Length; } catch { }
                if (nb == 0) { try { Directory.Delete(dw); } catch { } Ecrire("    Réseaux Wi-Fi : aucun (PC sans Wi-Fi ?)."); }
                else
                {
                    Ecrire("    -> " + nb + " réseau(x) Wi-Fi" + (O.Wifi && Admin ? ", avec leur mot de passe : garde le kit en lieu sûr." : " (sans les mots de passe)."));
                    resume.Add(nb + " réseaux Wi-Fi");
                }
            }

            // 4. Script de remise en place et mode d'emploi
            var cmd = new StringBuilder();
            cmd.AppendLine("@echo off");
            cmd.AppendLine("chcp 65001 >nul");
            cmd.AppendLine("title Nettoyage PC - remise en place");
            cmd.AppendLine("net session >nul 2>&1");
            cmd.AppendLine("if errorlevel 1 ( echo Clic droit sur Reinstaller.cmd puis \"Executer en tant qu'administrateur\". & pause & exit /b 1 )");
            cmd.AppendLine("cd /d \"%~dp0\"");
            cmd.AppendLine("if exist pilotes ( echo === Pilotes === & pnputil /add-driver \"%~dp0pilotes\\*.inf\" /subdirs /install )");
            cmd.AppendLine("if exist wifi ( echo === Reseaux Wi-Fi === & for %%f in (\"%~dp0wifi\\*.xml\") do netsh wlan add profile filename=\"%%f\" user=all )");
            cmd.AppendLine("if exist applications-winget.json ( echo === Applications === & winget import -i \"%~dp0applications-winget.json\" --accept-package-agreements --accept-source-agreements --ignore-unavailable )");
            cmd.AppendLine("echo.");
            cmd.AppendLine("echo Termine. Les applications absentes de winget sont listees dans applications.txt.");
            cmd.AppendLine("pause");
            File.WriteAllText(Path.Combine(dossier, "Reinstaller.cmd"), cmd.ToString().Replace("\n", "\r\n").Replace("\r\r\n", "\r\n"), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(dossier, "LISEZMOI-kit.txt"),
                "KIT DE RÉINSTALLATION - " + Environment.MachineName + " - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + "\r\n\r\n" +
                "Après une réinstallation de Windows ou sur un nouveau PC :\r\n" +
                "1. Branche cette clé (ou copie ce dossier sur le PC).\r\n" +
                "2. Clic droit sur « Reinstaller.cmd » > « Exécuter en tant qu'administrateur ».\r\n" +
                "   Les pilotes, les réseaux Wi-Fi puis les applications (winget) sont remis en place.\r\n" +
                "3. applications.txt liste toutes les applications, même celles à réinstaller à la main.\r\n\r\n" +
                "Tes documents ne sont pas dans ce kit : utilise aussi Protéger > Sauvegarde.\r\n" +
                (O.Wifi && Admin ? "ATTENTION : le dossier wifi contient les mots de passe de tes réseaux Wi-Fi. Garde ce kit en lieu sûr.\r\n" : ""),
                new UTF8Encoding(true));
            Ecrire("  -> Kit prêt : " + dossier);
            resumeAutres.Add("Kit de réinstallation : " + (resume.Count > 0 ? string.Join(", ", resume) : "liste des applications") + " dans " + dossier);
            Donnees("kit", new Dictionary<string, object> { { "Dossier", dossier } });
            Ecrire();
            Ecrire("=== Résumé ===");
            foreach (var r in resumeAutres) Ecrire("  " + r);
        }
    }
}
