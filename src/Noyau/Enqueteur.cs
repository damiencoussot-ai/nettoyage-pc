// Nettoyage PC - « Pourquoi mon PC ralentit ? » (Premium) : chronologie de tout ce qui a changé sur le PC
// (applications, pilotes, mises à jour, programmes au démarrage, services, réglages), croisée avec ses performances
// (durée des démarrages, plantages, mesures de performances). Tout est lu sur le PC et reste sur le PC.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal class Changement
    {
        public DateTime Date;
        public string Type = "", Titre = "", Pourquoi = "";
        /// <summary>Où corriger : « page:Cle » ou « uri:... » (vide : rien à ouvrir).</summary>
        public string Cible = "";
        /// <summary>0 : sans effet connu, 1 : à surveiller, 2 : suspect.</summary>
        public int Suspicion;
    }

    internal class Metrique
    {
        public string Cle = "", Nom = "", Unite = "";
        public bool PlusBasMieux = true;
        public List<Tuple<DateTime, double>> Points = new List<Tuple<DateTime, double>>();
        /// <summary>Valeurs journalières (0 les jours sans événement), pour les comptages comme les plantages.</summary>
        public bool Comptage;
        public string Format(double v)
        {
            if (Unite == "s") return v.ToString(v < 10 ? "0.0" : "0", Appli.Fr) + " s";
            if (Unite == "%") return v.ToString("0", Appli.Fr) + " %";
            return v.ToString(v < 10 ? "0.#" : "0", Appli.Fr) + (Unite != "" ? " " + Langue.T(Unite) : "");
        }
    }

    internal class ResultatEnquete
    {
        public List<Changement> Changements = new List<Changement>();
        public List<Metrique> Metriques = new List<Metrique>();
        public int NbInstantanes;
        public DateTime? PremierInstantane;
        public bool ChronoDemarrages;
    }

    internal static class Instantanes
    {
        private static string Dossier { get { return Path.Combine(Chemins.Local, "instantanes"); } }

        /// <summary>Une « photo » du PC par jour (appelée à l'ouverture de l'application et par le gardien). Garde 150 jours.</summary>
        public static void PrendreSiBesoin()
        {
            try
            {
                if (!Natif.EstWindows && Programme.DossierAutotest == null) return;
                string f = Path.Combine(Dossier, DateTime.Now.ToString("yyyy-MM-dd") + ".json");
                if (File.Exists(f)) return;
                Directory.CreateDirectory(Dossier);
                File.WriteAllText(f, Json.Ecrire(Prendre()));
                foreach (var ancien in Directory.GetFiles(Dossier, "*.json"))
                {
                    DateTime d;
                    if (DateTime.TryParseExact(Path.GetFileNameWithoutExtension(ancien), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && (DateTime.Now - d).TotalDays > 150)
                        try { File.Delete(ancien); } catch { }
                }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("instantané du PC", ex); }
        }

        public static Dictionary<string, object> Prendre()
        {
            var d = new Dictionary<string, object> { { "Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm") } };
            try { d["Demarrage"] = ElementsDemarrage.Lister().Where(x => x.Actif).Select(x => x.Nom).Distinct().OrderBy(x => x).ToArray(); } catch { }
            try { d["Services"] = ServicesTiers().ToArray(); } catch { }
            try
            {
                string id = Registre.Texte(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActivePowerScheme").ToLowerInvariant();
                d["Alimentation"] = id.StartsWith("8c5e7fda") || id.StartsWith("e9a42b02") ? "Performances élevées" : id.StartsWith("381b4222") ? "Équilibré" : id.StartsWith("a1841308") ? "Économie d'énergie" : id != "" ? "personnalisé" : "";
            }
            catch { }
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                    if (k != null) d["Windows"] = (k.GetValue("DisplayVersion") as string ?? "") + " (" + (k.GetValue("CurrentBuild") as string ?? "") + "." + Convert.ToString(k.GetValue("UBR") ?? "0") + ")";
            }
            catch { }
            try { d["Pilotes"] = InfosPc.CartesGraphiques().Select(c => c.Nom + " : " + c.VersionPilote).ToArray(); } catch { }
            try { d["Ram"] = Barrette.VitesseReelle(Barrette.Lister()); } catch { }
            try { var e = Potentiel.Ecrans().FirstOrDefault(x => x.Principal); if (e != null) d["Hz"] = e.Hz; } catch { }
            try
            {
                var dq = new DriveInfo(Path.GetPathRoot(Chemins.Windows ?? "C:\\"));
                if (dq.IsReady && dq.TotalSize > 0) d["DisqueLibre"] = Math.Round(100.0 * dq.AvailableFreeSpace / dq.TotalSize, 1);
            }
            catch { }
            try { ulong t, l; Natif.Memoire(out t, out l); if (t > 0) d["RamGo"] = Math.Round(t / 1073741824.0, 1); } catch { }
            return d;
        }

        /// <summary>Services qui démarrent avec Windows et ne viennent pas de Windows (programmes installés).</summary>
        private static List<string> ServicesTiers()
        {
            var l = new List<string>();
            string win = (Chemins.Windows ?? @"C:\Windows").TrimEnd('\\');
            using (var s = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services"))
            {
                if (s == null) return l;
                foreach (var n in s.GetSubKeyNames())
                {
                    try
                    {
                        using (var k = s.OpenSubKey(n))
                        {
                            object st = k.GetValue("Start"), ty = k.GetValue("Type");
                            if (!(st is int) || (int)st != 2 || !(ty is int) || ((int)ty & 0x30) == 0) continue;
                            string img = Environment.ExpandEnvironmentVariables(k.GetValue("ImagePath") as string ?? "").Trim().Trim('"');
                            if (img == "" || img.StartsWith(win, StringComparison.OrdinalIgnoreCase) || img.StartsWith(@"\SystemRoot", StringComparison.OrdinalIgnoreCase) || img.StartsWith("system32", StringComparison.OrdinalIgnoreCase)) continue;
                            string nom = k.GetValue("DisplayName") as string ?? "";
                            if (nom == "" || nom.StartsWith("@")) nom = n;
                            l.Add(nom.Trim());
                        }
                    }
                    catch { }
                }
            }
            return l.Distinct().OrderBy(x => x).ToList();
        }

        public static List<Tuple<DateTime, Dictionary<string, object>>> Lire()
        {
            var l = new List<Tuple<DateTime, Dictionary<string, object>>>();
            try
            {
                if (!Directory.Exists(Dossier)) return l;
                foreach (var f in Directory.GetFiles(Dossier, "*.json"))
                {
                    DateTime d;
                    if (!DateTime.TryParseExact(Path.GetFileNameWithoutExtension(f), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) continue;
                    var j = Json.LireFichier(f) as Dictionary<string, object>;
                    if (j != null) l.Add(Tuple.Create(d, j));
                }
            }
            catch { }
            return l.OrderBy(x => x.Item1).ToList();
        }
    }

    internal static class Enqueteur
    {
        private static string FichierDemarrages { get { return Path.Combine(Chemins.Local, "demarrages.json"); } }

        /// <summary>Garde les durées de démarrage lues par « Analyse du démarrage » (autorisation administrateur), fusionnées avec les précédentes.</summary>
        public static void EnregistrerDemarrages(object donnees)
        {
            try
            {
                var d = donnees as Dictionary<string, object>;
                if (d == null || !Json.Bool(d, "Admin")) return;
                var tous = new Dictionary<string, long>();
                foreach (var x in Json.Objets(Json.LireFichier(FichierDemarrages))) tous[Json.Texte(x, "Date")] = Json.Long(x, "Total");
                foreach (var x in Json.Objets(Json.Valeur(d, "Demarrages"))) tous[Json.Texte(x, "Date")] = Json.Long(x, "Total");
                var l = tous.Where(kv => kv.Key != "" && kv.Value > 0).OrderByDescending(kv => kv.Key).Take(400)
                    .Select(kv => (object)new Dictionary<string, object> { { "Date", kv.Key }, { "Total", kv.Value } }).ToArray();
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(FichierDemarrages, Json.Ecrire(l));
            }
            catch (Exception ex) { JournalDemarrage.Erreur("enregistrement des démarrages", ex); }
        }

        private static readonly Regex Lourds = new Regex(@"booster|optimi[sz]er|speed ?up|driver ?(updater|booster|easy|genius)|reimage|restoro|segurazo|total ?av|pc ?app ?store|wave ?browser|onelaunch|shift browser|mcafee|norton|avast|\bavg\b|kaspersky|bitdefender|webroot|bytefence|iobit|advanced systemcare|ccleaner", RegexOptions.IgnoreCase);

        public static ResultatEnquete Analyser()
        {
            var r = new ResultatEnquete();
            var limite = DateTime.Now.AddDays(-120);
            var demarrageActuel = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { foreach (var e in ElementsDemarrage.Lister().Where(x => x.Actif)) demarrageActuel.Add(e.Nom ?? ""); } catch { }

            // 1. Applications installées (date d'installation gardée par Windows)
            try
            {
                foreach (var p in ProgrammesInstalles.Lister())
                {
                    if (!p.Date.HasValue || p.Date.Value < limite || p.Nom.StartsWith("Nettoyage PC")) continue;
                    var c = new Changement { Date = p.Date.Value.Date.AddHours(12), Type = "Application", Titre = Langue.F("Installée : {0}", p.Nom), Cible = "page:Applications" };
                    if (Lourds.IsMatch(p.Nom) || Lourds.IsMatch(p.Editeur)) { c.Suspicion = 2; c.Pourquoi = Langue.T("Les « optimiseurs », les antivirus en double et les barres d'outils ralentissent souvent le PC."); }
                    else if (demarrageActuel.Any(n => n.Length >= 4 && (p.Nom.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0 || n.IndexOf(p.Nom.Split(' ')[0], StringComparison.OrdinalIgnoreCase) >= 0 && p.Nom.Split(' ')[0].Length >= 4)))
                    { c.Suspicion = 1; c.Pourquoi = Langue.T("Elle se lance à chaque démarrage de Windows."); }
                    r.Changements.Add(c);
                }
            }
            catch { }

            // 2. Mises à jour Windows réussies (dont pilotes livrés par Windows Update)
            try
            {
                foreach (var e in Journaux.Lister("System", "*[System[Provider[@Name='Microsoft-Windows-WindowsUpdateClient'] and EventID=19 and " + Journaux.Depuis(120) + "]]", 300))
                {
                    string t = e.D("updateTitle"); if (t == "") continue;
                    bool pilote = Regex.IsMatch(t, @" - \w+ - |driver|pilote|display|graphics|firmware|BIOS", RegexOptions.IgnoreCase);
                    var c = new Changement { Date = e.Date, Type = pilote ? "Pilote" : "Mise à jour Windows", Titre = t, Cible = pilote ? "page:Pilotes" : "uri:ms-settings:windowsupdate-history" };
                    if (pilote && Regex.IsMatch(t, "display|graphics|vid[ée]o", RegexOptions.IgnoreCase)) { c.Suspicion = 1; c.Pourquoi = Langue.T("Un pilote graphique changé par Windows peut faire perdre des performances en jeu."); }
                    r.Changements.Add(c);
                }
            }
            catch { }

            // 3. Pilotes installés (hors pilotes de Windows)
            try
            {
                var vus = new HashSet<string>();
                foreach (var e in Journaux.Lister("System", "*[System[Provider[@Name='Microsoft-Windows-UserPnp'] and EventID=20001 and " + Journaux.Depuis(120) + "]]", 400))
                {
                    string desc = e.D("DriverDescription"), four = e.D("DriverProvider"), ver = e.D("DriverVersion");
                    if (desc == "") desc = e.D("DeviceDescription");
                    if (desc == "") desc = e.D("DriverName");
                    if (desc == "" || four.Equals("Microsoft", StringComparison.OrdinalIgnoreCase) || e.D("InstallStatus") != "" && e.D("InstallStatus") != "0" && e.D("InstallStatus") != "0x0") continue;
                    string cle = e.Date.ToString("yyyy-MM-dd") + "|" + desc + "|" + ver;
                    if (!vus.Add(cle)) continue;
                    var c = new Changement { Date = e.Date, Type = "Pilote", Titre = desc + (ver != "" ? " " + ver : "") + (four != "" ? " (" + four + ")" : ""), Cible = "page:Pilotes" };
                    if (Regex.IsMatch(e.D("SetupClass") + desc, "display|radeon|geforce|nvidia|graphics", RegexOptions.IgnoreCase) || e.D("SetupClass").Contains("4d36e968"))
                    { c.Suspicion = 1; c.Pourquoi = Langue.T("Nouveau pilote graphique : s'il pose problème, reviens à la version précédente (Gestionnaire de périphériques)."); }
                    r.Changements.Add(c);
                }
            }
            catch { }

            // 4. Différences entre les photos quotidiennes du PC
            var inst = Instantanes.Lire();
            r.NbInstantanes = inst.Count;
            if (inst.Count > 0) r.PremierInstantane = inst[0].Item1;
            for (int i = 1; i < inst.Count; i++)
            {
                var a = inst[i - 1].Item2; var b = inst[i].Item2; DateTime d = inst[i].Item1.AddHours(9);
                foreach (var n in Json.Textes(b, "Demarrage").Except(Json.Textes(a, "Demarrage"), StringComparer.OrdinalIgnoreCase))
                    r.Changements.Add(new Changement { Date = d, Type = "Démarrage", Titre = Langue.F("Ajouté au démarrage : {0}", n), Suspicion = 2, Pourquoi = Langue.T("Il se lance à chaque démarrage et occupe le PC en arrière-plan."), Cible = "page:Demarrage" });
                foreach (var n in Json.Textes(b, "Services").Except(Json.Textes(a, "Services"), StringComparer.OrdinalIgnoreCase))
                    r.Changements.Add(new Changement { Date = d, Type = "Service", Titre = Langue.F("Nouveau service automatique : {0}", n), Suspicion = 1, Pourquoi = Langue.T("Il tourne en permanence, même quand le programme est fermé."), Cible = "uri:services.msc" });
                string alA = Json.Texte(a, "Alimentation"), alB = Json.Texte(b, "Alimentation");
                if (alA != "" && alB != "" && alA != alB)
                    r.Changements.Add(new Changement { Date = d, Type = "Réglage", Titre = Langue.F("Mode d'alimentation : {0} → {1}", Langue.T(alA), Langue.T(alB)), Suspicion = alB == "Économie d'énergie" ? 2 : 0, Pourquoi = alB == "Économie d'énergie" ? Langue.T("Le mode Économie d'énergie bride le processeur.") : "", Cible = "page:Optimisation" });
                string wA = Json.Texte(a, "Windows"), wB = Json.Texte(b, "Windows");
                if (wA != "" && wB != "" && wA != wB)
                    r.Changements.Add(new Changement { Date = d, Type = "Mise à jour Windows", Titre = Langue.F("Windows : {0} → {1}", wA, wB), Cible = "uri:ms-settings:windowsupdate-history" });
                var pA = Json.Textes(a, "Pilotes"); var pB = Json.Textes(b, "Pilotes");
                foreach (var p in pB.Except(pA)) if (pA.Count > 0)
                    r.Changements.Add(new Changement { Date = d, Type = "Pilote", Titre = Langue.F("Pilote graphique : {0}", p), Suspicion = 1, Pourquoi = Langue.T("Nouveau pilote graphique : à vérifier si les jeux sont moins fluides."), Cible = "page:Pilotes" });
                long rA = Json.Long(a, "Ram"), rB = Json.Long(b, "Ram");
                if (rA > 0 && rB > 0 && rB < rA - 50)
                    r.Changements.Add(new Changement { Date = d, Type = "Matériel", Titre = Langue.F("Mémoire vive : {0} → {1} MT/s", rA, rB), Suspicion = 2, Pourquoi = Langue.T("La mémoire tourne moins vite : le profil EXPO/XMP a été désactivé (souvent après une mise à jour du BIOS)."), Cible = "page:Potentiel" });
                long hA = Json.Long(a, "Hz"), hB = Json.Long(b, "Hz");
                if (hA > 0 && hB > 0 && hB < hA - 2)
                    r.Changements.Add(new Changement { Date = d, Type = "Écran", Titre = Langue.F("Écran principal : {0} → {1} Hz", hA, hB), Suspicion = 2, Pourquoi = Langue.T("L'image paraît moins fluide : la fréquence de l'écran a baissé."), Cible = "page:Potentiel" });
                double lA = Json.Double(a, "DisqueLibre", -1), lB = Json.Double(b, "DisqueLibre", -1);
                if (lA >= 10 && lB >= 0 && lB < 10)
                    r.Changements.Add(new Changement { Date = d, Type = "Disque", Titre = Langue.F("Disque de Windows presque plein ({0} % libres)", lB.ToString("0")), Suspicion = 2, Pourquoi = Langue.T("Un disque presque plein ralentit Windows et les mises à jour."), Cible = "page:Nettoyage" });
            }

            // Métriques
            var dem = new Metrique { Cle = "demarrage", Nom = "Durée du démarrage", Unite = "s" };
            foreach (var x in Json.Objets(Json.LireFichier(FichierDemarrages)))
            {
                DateTime d;
                if (DateTime.TryParseExact(Json.Texte(x, "Date"), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && Json.Long(x, "Total") > 0)
                    dem.Points.Add(Tuple.Create(d, Json.Long(x, "Total") / 1000.0));
            }
            dem.Points = dem.Points.OrderBy(p => p.Item1).ToList();
            r.ChronoDemarrages = dem.Points.Count > 0;
            if (dem.Points.Count > 0) r.Metriques.Add(dem);

            var pl = new Metrique { Cle = "plantages", Nom = "Plantages d'applications par jour", Unite = "", Comptage = true };
            try
            {
                var parJour = Journaux.Lister("Application", "*[System[(Provider[@Name='Application Error'] and EventID=1000 or Provider[@Name='Application Hang'] and EventID=1002) and " + Journaux.Depuis(90) + "]]", 3000, false)
                    .GroupBy(e => e.Date.Date).ToDictionary(g => g.Key, g => g.Count());
                for (var j = DateTime.Today.AddDays(-89); j <= DateTime.Today; j = j.AddDays(1)) { int n; parJour.TryGetValue(j, out n); pl.Points.Add(Tuple.Create(j, (double)n)); }
            }
            catch { }
            if (pl.Points.Any(p => p.Item2 > 0)) r.Metriques.Add(pl);

            try
            {
                var bench = Benchmark.Historique();
                var cpu = new Metrique { Cle = "cpu", Nom = "Processeur (mesure de performances)", Unite = "Mo/s", PlusBasMieux = false };
                foreach (var b in bench)
                {
                    DateTime d;
                    if (b.CpuN > 0 && (DateTime.TryParseExact(b.Date, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) || DateTime.TryParse(b.Date, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)))
                        cpu.Points.Add(Tuple.Create(d, b.CpuN));
                }
                cpu.Points = cpu.Points.OrderBy(p => p.Item1).ToList();
                if (cpu.Points.Count >= 2) r.Metriques.Add(cpu);
            }
            catch { }

            var nbDem = new Metrique { Cle = "nbdemarrage", Nom = "Programmes au démarrage", Unite = "" };
            foreach (var i in inst) { var l = Json.Textes(i.Item2, "Demarrage"); if (Json.Valeur(i.Item2, "Demarrage") != null) nbDem.Points.Add(Tuple.Create(i.Item1, (double)l.Count)); }
            if (nbDem.Points.Count >= 2) r.Metriques.Add(nbDem);

            // Effet observé après chaque changement : les performances se sont-elles dégradées ?
            foreach (var c in r.Changements)
            {
                foreach (var m in r.Metriques.Where(x => x.Cle != "nbdemarrage"))
                {
                    double avant, apres;
                    if (!Comparer(m, c.Date, out avant, out apres)) continue;
                    string effet = Effet(m, avant, apres);
                    if (effet == "") continue;
                    c.Suspicion = 2;
                    c.Pourquoi = (c.Pourquoi != "" ? c.Pourquoi + " " : "") + effet;
                }
            }
            r.Changements = r.Changements.OrderByDescending(x => x.Date).ToList();
            return r;
        }

        /// <summary>Moyennes sur les 3 semaines avant et après une date (assez de mesures de chaque côté).</summary>
        public static bool Comparer(Metrique m, DateTime date, out double avant, out double apres, int jours = 21)
        {
            avant = apres = 0;
            var av = m.Points.Where(p => p.Item1 < date && p.Item1 >= date.AddDays(-jours)).Select(p => p.Item2).ToList();
            var ap = m.Points.Where(p => p.Item1 > date && p.Item1 <= date.AddDays(jours)).Select(p => p.Item2).ToList();
            int min = m.Comptage ? 5 : 2;
            if (av.Count < min || ap.Count < (m.Comptage ? 3 : 1)) return false;
            avant = av.Average(); apres = ap.Average();
            return true;
        }

        public static string Effet(Metrique m, double avant, double apres)
        {
            switch (m.Cle)
            {
                case "demarrage":
                    if (apres - avant >= Math.Max(5, avant * 0.15)) return Langue.F("Depuis, le démarrage prend {0} de plus ({1} → {2}).", m.Format(apres - avant), m.Format(avant), m.Format(apres));
                    break;
                case "plantages":
                    if (apres >= Math.Max(0.4, avant * 2.5)) return Langue.F("Depuis, les applications plantent plus souvent ({0} → {1} par jour).", avant.ToString("0.#", Appli.Fr), apres.ToString("0.#", Appli.Fr));
                    break;
                case "cpu":
                    if (avant > 0 && (avant - apres) / avant >= 0.08) return Langue.F("Depuis, le processeur est {0} % moins rapide aux mesures.", ((avant - apres) / avant * 100).ToString("0"));
                    break;
            }
            return "";
        }

        /// <summary>Résumé chiffré d'une métrique sur la période choisie par rapport aux semaines d'avant.</summary>
        public static string Tendance(Metrique m, DateTime depuis)
        {
            var av = m.Points.Where(p => p.Item1 < depuis && p.Item1 >= depuis.AddDays(-30)).Select(p => p.Item2).ToList();
            var ap = m.Points.Where(p => p.Item1 >= depuis).Select(p => p.Item2).ToList();
            if (ap.Count == 0 || av.Count < (m.Comptage ? 5 : 1)) return "";
            double a = av.Average(), b = ap.Average();
            string sens = Math.Abs(b - a) < Math.Max(m.Comptage ? 0.2 : 0.5, Math.Abs(a) * 0.05) ? Langue.T("stable") : (b > a) == m.PlusBasMieux ? Langue.T("dégradé") : Langue.T("amélioré");
            return Langue.F("{0} : {1} avant, {2} depuis ({3})", Langue.T(m.Nom), m.Format(a), m.Format(b), sens);
        }
    }
}
