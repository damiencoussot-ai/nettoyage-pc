// Nettoyage PC - moteur : analyse des plantages (Premium). Lecture seule des journaux de Windows (Système, Application) sur 90 jours :
// écrans bleus, arrêts brutaux, erreurs matérielles (WHEA), erreurs de disque, pilote graphique qui redémarre, applications qui plantent ou se figent.
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NettoyagePC
{
    internal partial class Moteur
    {
        private const int JoursPlantages = 90;

        /// <summary>Code d'écran bleu : nom technique, explication simple, catégorie (pilote, memoire, disque, graphique, materiel, systeme).</summary>
        private static readonly Dictionary<uint, string[]> CodesEcranBleu = new Dictionary<uint, string[]>
        {
            { 0x0A, new[] { "IRQL_NOT_LESS_OR_EQUAL", "Un pilote a utilisé la mémoire de façon incorrecte.", "pilote" } },
            { 0x19, new[] { "BAD_POOL_HEADER", "La mémoire réservée à Windows a été abîmée (pilote ou barrette).", "pilote" } },
            { 0x1A, new[] { "MEMORY_MANAGEMENT", "Erreur de gestion de la mémoire vive (barrette, réglage de la mémoire ou pilote).", "memoire" } },
            { 0x1E, new[] { "KMODE_EXCEPTION_NOT_HANDLED", "Un pilote a provoqué une erreur qu'il n'a pas su gérer.", "pilote" } },
            { 0x24, new[] { "NTFS_FILE_SYSTEM", "Problème du système de fichiers du disque.", "disque" } },
            { 0x3B, new[] { "SYSTEM_SERVICE_EXCEPTION", "Un pilote (souvent celui de la carte graphique) a provoqué une erreur.", "pilote" } },
            { 0x4E, new[] { "PFN_LIST_CORRUPT", "Erreur de la mémoire vive (barrette ou réglage de la mémoire).", "memoire" } },
            { 0x50, new[] { "PAGE_FAULT_IN_NONPAGED_AREA", "Accès à une zone de mémoire invalide (barrette, pilote ou antivirus tiers).", "memoire" } },
            { 0x77, new[] { "KERNEL_STACK_INPAGE_ERROR", "Windows n'a pas pu relire des données sur le disque.", "disque" } },
            { 0x7A, new[] { "KERNEL_DATA_INPAGE_ERROR", "Windows n'a pas pu relire des données sur le disque (disque, câble ou mémoire).", "disque" } },
            { 0x7E, new[] { "SYSTEM_THREAD_EXCEPTION_NOT_HANDLED", "Un pilote a provoqué une erreur qu'il n'a pas su gérer.", "pilote" } },
            { 0x7F, new[] { "UNEXPECTED_KERNEL_MODE_TRAP", "Erreur matérielle du processeur ou de la mémoire (souvent un réglage d'overclocking).", "materiel" } },
            { 0x9F, new[] { "DRIVER_POWER_STATE_FAILURE", "Un pilote n'a pas répondu lors d'une mise en veille ou d'un réveil.", "pilote" } },
            { 0xBE, new[] { "ATTEMPTED_WRITE_TO_READONLY_MEMORY", "Un pilote a tenté d'écrire dans une mémoire protégée.", "pilote" } },
            { 0xC2, new[] { "BAD_POOL_CALLER", "Un pilote a mal utilisé la mémoire de Windows.", "pilote" } },
            { 0xC5, new[] { "DRIVER_CORRUPTED_EXPOOL", "Un pilote a abîmé la mémoire de Windows.", "pilote" } },
            { 0xD1, new[] { "DRIVER_IRQL_NOT_LESS_OR_EQUAL", "Un pilote (souvent réseau ou Wi-Fi) a utilisé la mémoire de façon incorrecte.", "pilote" } },
            { 0xE2, new[] { "MANUALLY_INITIATED_CRASH", "Écran bleu déclenché volontairement (clavier ou outil de diagnostic).", "systeme" } },
            { 0xED, new[] { "UNMOUNTABLE_BOOT_VOLUME", "Windows n'a pas pu lire son propre disque.", "disque" } },
            { 0xEF, new[] { "CRITICAL_PROCESS_DIED", "Un élément indispensable de Windows s'est arrêté (fichiers système abîmés ou disque).", "systeme" } },
            { 0xF4, new[] { "CRITICAL_OBJECT_TERMINATION", "Un élément indispensable de Windows s'est arrêté (souvent le disque).", "disque" } },
            { 0x101, new[] { "CLOCK_WATCHDOG_TIMEOUT", "Un cœur du processeur a cessé de répondre (overclocking, BIOS ou processeur).", "materiel" } },
            { 0x109, new[] { "CRITICAL_STRUCTURE_CORRUPTION", "Une partie de Windows a été modifiée en mémoire (pilote ou mémoire vive).", "memoire" } },
            { 0x10E, new[] { "VIDEO_MEMORY_MANAGEMENT_INTERNAL", "Erreur de la mémoire de la carte graphique ou de son pilote.", "graphique" } },
            { 0x116, new[] { "VIDEO_TDR_FAILURE", "La carte graphique a cessé de répondre et n'a pas pu être relancée.", "graphique" } },
            { 0x117, new[] { "VIDEO_TDR_TIMEOUT_DETECTED", "La carte graphique a cessé de répondre.", "graphique" } },
            { 0x119, new[] { "VIDEO_SCHEDULER_INTERNAL_ERROR", "Erreur du pilote de la carte graphique.", "graphique" } },
            { 0x124, new[] { "WHEA_UNCORRECTABLE_ERROR", "Erreur matérielle grave : processeur, mémoire, overclocking ou profil EXPO / XMP instable, alimentation ou surchauffe.", "materiel" } },
            { 0x12B, new[] { "FAULTY_HARDWARE_CORRUPTED_PAGE", "Erreur de la mémoire vive.", "memoire" } },
            { 0x133, new[] { "DPC_WATCHDOG_VIOLATION", "Un pilote (souvent celui du SSD ou du chipset) a bloqué le système trop longtemps.", "pilote" } },
            { 0x139, new[] { "KERNEL_SECURITY_CHECK_FAILURE", "Windows a détecté une donnée abîmée (pilote ou mémoire vive).", "pilote" } },
            { 0x144, new[] { "BUGCODE_USB3_DRIVER", "Erreur d'un appareil ou d'un pilote USB.", "pilote" } },
            { 0x154, new[] { "UNEXPECTED_STORE_EXCEPTION", "Erreur de lecture du disque (souvent un SSD ou son micrologiciel).", "disque" } },
            { 0xC000021A, new[] { "STATUS_SYSTEM_PROCESS_TERMINATED", "Un élément indispensable de Windows s'est arrêté (fichiers système abîmés).", "systeme" } }
        };

        private static readonly string[][] ModulesConnus =
        {
            new[] { @"^(nvwgf2um|nvd3dum|nvoglv|nvcuda|nvldumd|nvapi|nvgpucomp|nvlddmkm)", "le pilote de la carte graphique NVIDIA", "graphique" },
            new[] { @"^(amdxx|atiumd|amdxc|aticfx|atidxx|amdvlk|amdihk|atiuxp|amdenc|amfrt)", "le pilote de la carte graphique AMD Radeon", "graphique" },
            new[] { @"^(igd10|igd12|igdumd|ig9icd|igc64|igxelpicd|igdgmm|igdail)", "le pilote graphique Intel", "graphique" },
            new[] { @"^(d3d11|d3d12|d3d12core|dxgi|d3d9|dxcore)\.dll$", "DirectX (souvent à cause du pilote graphique)", "graphique" },
            new[] { @"^(msvcp|vcruntime|ucrtbase|msvcr)", "les bibliothèques Visual C++", "vcpp" },
            new[] { @"^(clr|coreclr|mscorwks|clrjit)\.dll$", ".NET (l'application elle-même)", "app" },
            new[] { @"^unityplayer\.dll$", "le moteur Unity du jeu", "app" },
            new[] { @"^(ntdll|kernelbase|kernel32)\.dll$", "Windows (erreur interne de l'application)", "app" }
        };

        private void AnalysePlantages()
        {
            Ecrire("=== Analyse des plantages (" + JoursPlantages + " derniers jours) ===");
            DateTime depuis = DateTime.Now.AddDays(-JoursPlantages);
            string periode = "TimeCreated[timediff(@SystemTime) <= " + (JoursPlantages * 86400000L) + "]";
            var ecrans = new List<Dictionary<string, object>>();
            var arrets = new List<Dictionary<string, object>>();
            var materiel = new List<Tuple<string, string, bool, DateTime>>();       // type, détail, fatal, date
            var disques = new List<Tuple<string, string, DateTime>>();             // type, détail, date
            var affichage = new List<DateTime>();
            var applis = new List<Tuple<string, string, string, bool, DateTime>>(); // appli, module, code, figée, date
            var semaines = new int[13, 3];                                        // [semaine][0 système, 1 applis, 2 matériel/disque]
            Action<DateTime, int> compter = (d, k) => { int s = (int)((DateTime.Now - d).TotalDays / 7); if (s >= 0 && s < 13) semaines[12 - s, k]++; };

            // --- Journal Système
            foreach (var e in LireJournal("System", "*[System[(" +
                "(Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting'] and EventID=1001) or " +
                "(Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=41) or " +
                "Provider[@Name='Microsoft-Windows-WHEA-Logger'] or " +
                "(Provider[@Name='disk'] and (EventID=7 or EventID=11 or EventID=51 or EventID=153)) or " +
                "(Provider[@Name='Ntfs' or @Name='Microsoft-Windows-Ntfs'] and EventID=55) or " +
                "(Provider[@Name='stornvme' or @Name='storahci' or @Name='iaStorA' or @Name='iaStorAC'] and EventID=129) or " +
                "(Provider[@Name='Display'] and EventID=4101)" +
                ") and " + periode + "]]", 3000))
            {
                string fournisseur = e.Item1; int id = e.Item2; DateTime date = e.Item3; var d = e.Item4; var brut = e.Item5;
                if (fournisseur.EndsWith("WER-SystemErrorReporting"))
                {
                    string p1 = Texte(d, "param1"); if (p1 == "" && brut.Count > 0) p1 = brut[0];
                    string dump = Texte(d, "param2"); if (dump == "" && brut.Count > 1) dump = brut[1];
                    var m = Regex.Match(p1, @"0x([0-9a-fA-F]{1,8})");
                    uint code = 0; if (m.Success) uint.TryParse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code);
                    string[] info; if (!CodesEcranBleu.TryGetValue(code, out info)) info = new[] { "", "Écran bleu (code peu courant) : le plus souvent un pilote ou un problème matériel.", "pilote" };
                    ecrans.Add(new Dictionary<string, object> { { "Date", date.ToString("yyyy-MM-dd HH:mm") }, { "Code", "0x" + code.ToString("X8") }, { "Nom", info[0] }, { "Explication", info[1] }, { "Categorie", info[2] }, { "Dump", Path.GetFileName(dump) } });
                    compter(date, 0);
                }
                else if (fournisseur.EndsWith("Kernel-Power"))
                {
                    long bug = Nombre(d, "BugcheckCode"), bouton = Nombre(d, "PowerButtonTimestamp");
                    string type = bug != 0 ? "ecran-bleu" : bouton != 0 ? "bouton" : "coupure";
                    arrets.Add(new Dictionary<string, object> { { "Date", date.ToString("yyyy-MM-dd HH:mm") }, { "Type", type } });
                    if (bug == 0) compter(date, 0);
                }
                else if (fournisseur.EndsWith("WHEA-Logger"))
                {
                    bool fatal = id == 18 || id == 20 || id == 46;
                    string quoi = id == 17 ? "Bus PCI Express (carte graphique, SSD NVMe ou carte réseau)" : id == 19 || id == 18 ? "Processeur" : id == 47 ? "Mémoire vive" :
                        id == 46 || id == 20 ? "Processeur ou mémoire" : "Composant matériel";
                    materiel.Add(Tuple.Create(quoi, "WHEA " + id, fatal, date));
                    compter(date, 2);
                }
                else if (fournisseur == "Display") { affichage.Add(date); compter(date, 2); }
                else
                {
                    string quoi = fournisseur.IndexOf("ntfs", StringComparison.OrdinalIgnoreCase) >= 0 ? "Système de fichiers abîmé (NTFS)" :
                        id == 129 ? "Le disque ne répondait plus (réinitialisé par Windows)" : id == 7 ? "Secteur illisible sur un disque" :
                        id == 51 ? "Erreur de lecture / écriture" : id == 153 ? "Lecture ou écriture qui a dû être recommencée" : "Erreur du contrôleur de disque";
                    disques.Add(Tuple.Create(quoi, fournisseur + " " + id, date));
                    compter(date, 2);
                }
            }

            // --- Journal Application
            foreach (var e in LireJournal("Application", "*[System[(" +
                "(Provider[@Name='Application Error'] and EventID=1000) or (Provider[@Name='Application Hang'] and EventID=1002) or (Provider[@Name='.NET Runtime'] and EventID=1026)" +
                ") and " + periode + "]]", 3000))
            {
                string fournisseur = e.Item1; DateTime date = e.Item3; var d = e.Item4; var brut = e.Item5;
                string appli, module = "", code = "";
                bool figee = fournisseur == "Application Hang";
                if (fournisseur == ".NET Runtime")
                {
                    var m = Regex.Match(brut.Count > 0 ? brut[0] : "", @"Application\s*:\s*(\S+)");
                    appli = m.Success ? m.Groups[1].Value : "";
                    module = ".NET";
                }
                else
                {
                    appli = Texte(d, "AppName"); if (appli == "" && brut.Count > 0) appli = brut[0];
                    if (!figee) { module = Texte(d, "ModuleName"); if (module == "" && brut.Count > 3) module = brut[3]; code = Texte(d, "ExceptionCode"); if (code == "" && brut.Count > 6) code = brut[6]; }
                }
                appli = (appli ?? "").Trim();
                if (appli == "" || Regex.IsMatch(appli, @"^(werfault|wermgr|dwm|explorer\.exe\.mun)$", RegexOptions.IgnoreCase)) continue;
                applis.Add(Tuple.Create(appli, module.Trim(), code.Trim(), figee, date));
                compter(date, 1);
            }

            // --- Regroupements
            var groupesApplis = applis.GroupBy(a => a.Item1.ToLowerInvariant()).Select(g =>
            {
                var plantages = g.Where(x => !x.Item4).ToList();
                string module = plantages.GroupBy(x => x.Item2.ToLowerInvariant()).OrderByDescending(x => x.Count()).Select(x => x.First().Item2).FirstOrDefault() ?? "";
                string explication = "", cat = "app";
                foreach (var mc in ModulesConnus) if (module != "" && Regex.IsMatch(module, mc[0], RegexOptions.IgnoreCase)) { explication = mc[1]; cat = mc[2]; break; }
                return new Dictionary<string, object>
                {
                    { "Nom", g.First().Item1 }, { "Plantages", plantages.Count }, { "Figee", g.Count(x => x.Item4) }, { "Dernier", g.Max(x => x.Item5).ToString("yyyy-MM-dd HH:mm") },
                    { "Module", module }, { "Explication", explication }, { "Categorie", cat }
                };
            }).OrderByDescending(x => (int)x["Plantages"] * 2 + (int)x["Figee"]).Take(40).ToList();
            var groupesMateriel = materiel.GroupBy(x => x.Item1 + (x.Item3 ? "|grave" : "")).Select(g => new Dictionary<string, object>
            {
                { "Nom", g.First().Item1 }, { "Detail", g.First().Item2 }, { "Grave", g.First().Item3 }, { "Fois", g.Count() }, { "Dernier", g.Max(x => x.Item4).ToString("yyyy-MM-dd HH:mm") }
            }).ToList();
            var groupesDisques = disques.GroupBy(x => x.Item1).Select(g => new Dictionary<string, object>
            {
                { "Nom", g.Key }, { "Detail", g.First().Item2 }, { "Fois", g.Count() }, { "Dernier", g.Max(x => x.Item3).ToString("yyyy-MM-dd HH:mm") }
            }).ToList();

            // --- Conseils (modèle de phrase + valeurs, traduits par l'interface)
            var conseils = new List<object[]>();
            Action<string, object[]> conseil = (modele, valeurs) => { var l = new List<object> { modele }; l.AddRange(valeurs); conseils.Add(l.ToArray()); };
            var cats = ecrans.Select(x => (string)x["Categorie"]).ToList();
            int coupures = arrets.Count(a => (string)a["Type"] == "coupure"), boutons = arrets.Count(a => (string)a["Type"] == "bouton");
            bool memoireBoostee = false;
            try { var bar = Barrette.Lister(); int reelle = Barrette.VitesseReelle(bar); memoireBoostee = reelle > 0 && (reelle > 4800 && bar.Any(b => b.TypeSmbios == 34) || reelle > 3200 && bar.All(b => b.TypeSmbios != 34)); } catch { }
            if (cats.Contains("materiel") || groupesMateriel.Any(g => (bool)g["Grave"]))
                conseil(memoireBoostee ? "Erreurs matérielles graves : vérifie la température (poussière, ventilateurs), mets à jour le BIOS de la carte mère, et essaie de désactiver le profil EXPO / XMP de la mémoire : s'il est instable, il provoque ce genre d'erreurs."
                    : "Erreurs matérielles graves : vérifie la température (poussière, ventilateurs), mets à jour le BIOS de la carte mère, et désactive tout overclocking.", new object[0]);
            else if (groupesMateriel.Count > 0)
                conseil(memoireBoostee ? "Le matériel signale des erreurs corrigées (sans plantage) : rien d'urgent, mais si elles se multiplient, mets à jour le BIOS et les pilotes (chipset, carte graphique), et vérifie la stabilité du profil EXPO / XMP."
                    : "Le matériel signale des erreurs corrigées (sans plantage) : rien d'urgent, mais si elles se multiplient, mets à jour le BIOS et les pilotes (chipset, carte graphique).", new object[0]);
            if (cats.Contains("memoire")) conseil(memoireBoostee ? "Écrans bleus liés à la mémoire vive : lance le test des barrettes (Outils > Matériel > Tester les barrettes) et essaie sans le profil EXPO / XMP."
                : "Écrans bleus liés à la mémoire vive : lance le test des barrettes (Outils > Matériel > Tester les barrettes).", new object[0]);
            if (cats.Contains("graphique") || affichage.Count > 0 || groupesApplis.Any(a => (string)a["Categorie"] == "graphique"))
            {
                if (affichage.Count > 0) conseil("La carte graphique ou son pilote est en cause (le pilote s'est arrêté puis relancé {0} fois) : installe le dernier pilote (Accélérer > Pilotes) et vérifie la température en jeu.", new object[] { affichage.Count });
                else conseil("La carte graphique ou son pilote est en cause : installe le dernier pilote (Accélérer > Pilotes) et vérifie la température en jeu.", new object[0]);
            }
            if (cats.Contains("pilote")) conseil("Écrans bleus causés par un pilote : installe les mises à jour Windows et pilotes (Mises à jour), puis celles du fabricant de la carte mère (chipset, réseau, Wi-Fi).", new object[0]);
            if (cats.Contains("disque") || groupesDisques.Count > 0) conseil("Un disque a signalé des erreurs : sauvegarde tes fichiers dès maintenant (Protéger > Sauvegarde), puis lance Nettoyer > Disques > Vérifier les disques.", new object[0]);
            if (cats.Contains("systeme")) conseil("Des fichiers de Windows semblent abîmés : lance « Réparer Windows » (Protéger > Sécurité).", new object[0]);
            if (coupures > 0) conseil("{0} arrêt(s) brutal(aux) sans écran bleu : coupure de courant, alimentation fatiguée, surchauffe ou blocage complet du PC. Vérifie la prise et la multiprise, et la propreté des ventilateurs.", new object[] { coupures });
            if (boutons > 0) conseil("{0} fois, le PC a été éteint avec le bouton d'alimentation (il était peut-être bloqué).", new object[] { boutons });
            if (groupesApplis.Any(a => (string)a["Categorie"] == "vcpp")) conseil("Certaines applications plantent dans les bibliothèques Visual C++ : réinstaller la dernière version de « Microsoft Visual C++ Redistributable » (site de Microsoft) règle souvent le problème.", new object[0]);
            var pireAppli = groupesApplis.FirstOrDefault(a => (int)a["Plantages"] >= 3 && (string)a["Categorie"] == "app");
            if (pireAppli != null) conseil("« {0} » plante souvent : mets-la à jour (Accélérer > Applications > Mettre à jour), ou réinstalle-la.", new object[] { pireAppli["Nom"] });
            if (ecrans.Count == 0 && arrets.Count == 0 && groupesMateriel.Count == 0 && groupesDisques.Count == 0 && affichage.Count == 0)
                conseils.Insert(0, new object[] { groupesApplis.Count == 0 ? "✔ Aucun plantage en {0} jours : ton PC est stable." : "✔ Windows lui-même est stable : aucun écran bleu ni arrêt brutal en {0} jours.", JoursPlantages });

            double indice = -1;
            try
            {
                var s = Wmi.Requete("SELECT SystemStabilityIndex, TimeGenerated FROM Win32_ReliabilityStabilityMetrics WHERE TimeGenerated > '" + System.Management.ManagementDateTimeConverter.ToDmtfDateTime(DateTime.Now.AddDays(-4)) + "'", @"root\cimv2", 20).OrderByDescending(o => Wmi.Texte(o, "TimeGenerated")).FirstOrDefault();
                if (s != null) { object v = s["SystemStabilityIndex"]; if (v != null) indice = Convert.ToDouble(v, CultureInfo.InvariantCulture); }
            }
            catch { }

            var graph = new List<object>();
            for (int i = 0; i < 13; i++) graph.Add(new[] { semaines[i, 0], semaines[i, 1], semaines[i, 2] });
            Ecrire("  Écrans bleus : " + ecrans.Count + " ; arrêts brutaux : " + arrets.Count + " ; erreurs matérielles : " + materiel.Count + " ; erreurs de disque : " + disques.Count +
                " ; pilote graphique relancé : " + affichage.Count + " ; applications qui ont planté ou se sont figées : " + groupesApplis.Count);
            foreach (var c in conseils) Ecrire("  Conseil : " + string.Format((string)c[0], c.Skip(1).ToArray()));
            Ecrire();
            Donnees("plantages", new Dictionary<string, object>
            {
                { "Jours", JoursPlantages }, { "Ecrans", ecrans.Take(50).Cast<object>().ToArray() }, { "Arrets", arrets.Take(100).Cast<object>().ToArray() },
                { "Materiel", groupesMateriel.Cast<object>().ToArray() }, { "Disques", groupesDisques.Cast<object>().ToArray() }, { "Affichage", affichage.Count },
                { "DernierAffichage", affichage.Count > 0 ? affichage.Max().ToString("yyyy-MM-dd HH:mm") : "" },
                { "Applications", groupesApplis.Cast<object>().ToArray() }, { "Conseils", conseils.ToArray() }, { "Semaines", graph.ToArray() }, { "Indice", indice }
            });
        }

        /// <summary>Événements d'un journal : fournisseur, numéro, date, données nommées, données dans l'ordre.</summary>
        private static List<Tuple<string, int, DateTime, Dictionary<string, string>, List<string>>> LireJournal(string journal, string xpath, int max)
        {
            var l = new List<Tuple<string, int, DateTime, Dictionary<string, string>, List<string>>>();
            if (!Natif.EstWindows) return l;
            try
            {
                var q = new EventLogQuery(journal, PathType.LogName, xpath) { ReverseDirection = true };
                using (var r = new EventLogReader(q))
                {
                    EventRecord e;
                    while (l.Count < max && (e = r.ReadEvent()) != null)
                        using (e)
                        {
                            var nommees = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            var brut = new List<string>();
                            try
                            {
                                var x = XDocument.Parse(e.ToXml());
                                foreach (var el in x.Descendants().Where(el => el.Name.LocalName == "Data"))
                                {
                                    brut.Add(el.Value);
                                    var n = el.Attribute("Name");
                                    if (n != null && !nommees.ContainsKey(n.Value)) nommees[n.Value] = el.Value;
                                }
                            }
                            catch { }
                            l.Add(Tuple.Create(e.ProviderName ?? "", e.Id, e.TimeCreated ?? DateTime.Now, nommees, brut));
                        }
                }
            }
            catch (Exception ex) { Sortie.Ligne("  (journal " + journal + " illisible : " + ex.Message + ")"); }
            return l;
        }
    }
}
