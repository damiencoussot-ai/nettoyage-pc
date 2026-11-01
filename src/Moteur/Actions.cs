// Nettoyage PC - moteur : mode jeu, sauvegarde, optimisation, alimentation, mises à jour, sécurité, réparation
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace NettoyagePC
{
    internal static class Alimentation
    {
        public const string Performances = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
        public const string Equilibre = "381b4222-f694-41f0-9685-ff5bb260df2e";
        public const string Economie = "a1841308-3541-4fab-bc81-f71556f20b4a";
        public static string Powercfg { get { return Path.Combine(Chemins.Systeme32, "powercfg.exe"); } }

        public static string PlanActif()
        {
            var m = Regex.Match(Programmes.Executer(Powercfg, "/getactivescheme", 15000), @"([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})");
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : "";
        }

        public static string NomPlanActif()
        {
            string t = Programmes.Executer(Powercfg, "/getactivescheme", 15000).Trim();
            var m = Regex.Match(t, @"\(([^)]+)\)\s*\*?\s*$");
            return m.Success ? m.Groups[1].Value : "";
        }

        public static string Liste() { return Programmes.Executer(Powercfg, "/list", 15000).ToLowerInvariant(); }

        public static bool Activer(string guid)
        {
            int c; Programmes.Executer(Powercfg, "/setactive " + guid, 15000, null, out c);
            return c == 0;
        }

        public static void Dupliquer(string guid) { Programmes.Executer(Powercfg, "/duplicatescheme " + guid + " " + guid, 15000); }
    }

    internal partial class Moteur
    {
        private string FichierModeJeu { get { return Path.Combine(dossierSortie, "modejeu.json"); } }

        // ------------------------------------------------------------ Mode jeu
        private void ModeJeuDebut()
        {
            Ecrire("=== Mode jeu ===");
            string avant = Alimentation.PlanActif();
            if (O.Simulation) Ecrire("  Le mode « Performances élevées » serait activé, puis la mémoire vive libérée.");
            else
            {
                if (!Alimentation.Liste().Contains(Alimentation.Performances)) Alimentation.Dupliquer(Alimentation.Performances);
                if (avant != "" && avant != Alimentation.Performances && !File.Exists(FichierModeJeu))
                    FichierSur.Ecrire(FichierModeJeu, Json.Ecrire(new Dictionary<string, object> { { "Avant", avant }, { "Date", DateTime.Now.ToString("yyyy-MM-dd HH:mm") } }));
                if (Alimentation.Activer(Alimentation.Performances))
                {
                    Ecrire("  OK : mode « Performances élevées » activé.");
                    if (avant != "" && avant != Alimentation.Performances)
                        Historique.Ajouter("alimentation", "Mode jeu : mode d'alimentation « Performances élevées »", new Dictionary<string, object> { { "Avant", avant }, { "Apres", Alimentation.Performances } });
                }
                else Ecrire("  Le mode « Performances élevées » n'est pas disponible sur ce PC (on garde le mode actuel).");
                resumeAutres.Add("Mode jeu activé : pense à cliquer sur « Terminer le mode jeu » après ta partie");
            }
            // Conseils : applications lourdes ouvertes (rien n'est fermé)
            try
            {
                var lourds = Process.GetProcesses().GroupBy(p => p.ProcessName)
                    .Select(g => new { Nom = g.Key, Octets = g.Sum(p => { try { return p.WorkingSet64; } catch { return 0L; } }) })
                    .Where(x => x.Octets >= 300L * 1048576 && !Regex.IsMatch(x.Nom, "^(System|Idle|MemCompression|explorer|dwm|csrss|svchost|powershell|Registry|audiodg|NettoyagePC)$", RegexOptions.IgnoreCase))
                    .OrderByDescending(x => x.Octets).Take(6).ToList();
                if (lourds.Count > 0)
                {
                    Ecrire("  Applications ouvertes qui utilisent beaucoup de mémoire (ferme celles dont tu n'as pas besoin pendant ta partie) :");
                    foreach (var l in lourds) Ecrire("    " + Dr(T(l.Octets), 10) + "  " + l.Nom);
                }
            }
            catch { }
            Ecrire();
        }

        private void ModeJeuFin()
        {
            Ecrire("=== Fin du mode jeu ===");
            if (!File.Exists(FichierModeJeu)) Ecrire("  Le mode jeu n'est pas actif : rien à rétablir.");
            else if (O.Simulation) Ecrire("  Le mode d'alimentation d'avant le mode jeu serait rétabli.");
            else
            {
                string avant = Json.Texte(Json.LireFichier(FichierModeJeu) as Dictionary<string, object>, "Avant");
                if (!Regex.IsMatch(avant, @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")) avant = "";
                if (avant != "" && Alimentation.Activer(avant)) Ecrire("  OK : mode d'alimentation précédent rétabli.");
                else Ecrire("  Mode précédent introuvable : passe en « Mode équilibré » depuis la page Optimisation.");
                if (!Admin || Protection.CheminSansLien(FichierModeJeu)) { try { File.Delete(FichierModeJeu); } catch { } }
                Historique.MarquerAnnulees(m => m.Type == "alimentation" && m.Titre.StartsWith("Mode jeu"));
                resumeAutres.Add("Mode jeu terminé");
            }
            Ecrire();
        }

        // ------------------------------------------------------------ Sauvegarde (copie, rien n'est supprimé)
        public static Dictionary<string, string> DossiersPersonnels()
        {
            return new Dictionary<string, string>
            {
                { "Documents", Chemins.Documents }, { "Images", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) },
                { "Bureau", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) }, { "Videos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) },
                { "Musique", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic) }, { "Telechargements", Chemins.Telechargements }
            };
        }

        private void SauvegardeRapide()
        {
            Ecrire("=== Sauvegarde ===");
            var connus = DossiersPersonnels();
            var choisis = O.Dossiers.Split(',').Select(x => x.Trim()).Where(x => x != "" && connus.ContainsKey(x) && Directory.Exists(connus[x])).Distinct().ToList();
            if (string.IsNullOrEmpty(O.Destination) || !Directory.Exists(O.Destination)) { Ecrire("  ATTENTION : destination introuvable (clé USB débranchée ?). Sauvegarde annulée."); Ecrire(); return; }
            if (choisis.Count == 0) { Ecrire("  ATTENTION : aucun dossier à sauvegarder."); Ecrire(); return; }
            long aCopier = 0;
            foreach (var c in choisis)
                foreach (var f in FichiersSurs(connus[c])) { try { if (!TailleFichiers.EstCloud(f.Attributes) && !f.FullName.StartsWith(dossierSortie, StringComparison.OrdinalIgnoreCase)) aCopier += f.Length; } catch { } }
            long libre = EspaceLibre(Path.GetPathRoot(O.Destination));
            Ecrire("  À copier : " + T(aCopier) + " (" + string.Join(", ", choisis) + ") ; place libre sur la destination : " + T(libre));
            if (aCopier > libre - 200L * 1048576) { Ecrire("  ATTENTION : pas assez de place sur la destination. Choisis moins de dossiers ou une clé plus grande."); Ecrire(); return; }
            if (O.Simulation) { Ecrire("  Simulation : rien n'est copié."); Ecrire(); return; }
            if (O.Incrementale) { SauvegardeIncrementale(choisis, connus); return; }
            string cible = Path.Combine(O.Destination, "NettoyagePC-Sauvegarde", Environment.MachineName, DateTime.Now.ToString("yyyy-MM-dd_HH-mm"));
            Directory.CreateDirectory(cible);
            string robocopy = Path.Combine(Chemins.Systeme32, "robocopy.exe");
            foreach (var c in choisis)
            {
                Ecrire("  Copie de " + c + "...");
                // /E sous-dossiers, /XJ sans jonctions, /XA:O sans fichiers OneDrive en ligne, /MT copie rapide
                string args = Elevation.Joindre(new[] { connus[c], Path.Combine(cible, c), "/E", "/XJ", "/XA:O", "/R:1", "/W:1", "/MT:8", "/NP", "/NFL", "/NDL", "/NJH", "/NJS", "/XD", dossierSortie });
                int code; Programmes.Executer(robocopy, args, 24 * 3600000, null, out code);
                if (code >= 8 || code < 0) Ecrire("    " + c + " : certains fichiers n'ont pas pu être copiés (code " + code + ", fichiers ouverts ou protégés).");
                else Ecrire("    " + c + " : OK");
            }
            long copie = FichiersSurs(cible).Sum(f => { try { return f.Length; } catch { return 0L; } });
            Ecrire("  -> Sauvegarde terminée : " + T(copie) + " copiés dans " + cible);
            resumeAutres.Add("Sauvegarde : " + T(copie) + " copiés vers " + cible);
            FichierSur.AjouterLigne(Path.Combine(dossierSortie, "sauvegardes.csv"), DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ";" + copie + ";" + cible);
            Ecrire();
        }

        /// <summary>
        /// Sauvegarde automatique (Premium) : un seul dossier « Automatique » mis à jour à chaque fois. Seuls les fichiers nouveaux ou modifiés
        /// sont copiés ; rien n'est jamais effacé de la sauvegarde (un fichier supprimé du PC y reste).
        /// </summary>
        private void SauvegardeIncrementale(List<string> choisis, Dictionary<string, string> connus)
        {
            string cible = Path.Combine(O.Destination, "NettoyagePC-Sauvegarde", Environment.MachineName, "Automatique");
            Directory.CreateDirectory(cible);
            Func<long> taille = () => FichiersSurs(cible).Sum(f => { try { return f.Length; } catch { return 0L; } });
            long avant = taille();
            string robocopy = Path.Combine(Chemins.Systeme32, "robocopy.exe");
            var erreurs = new List<string>();
            foreach (var c in choisis)
            {
                Ecrire("  Mise à jour de " + c + "...");
                // sans /MIR ni /PURGE : la sauvegarde ne perd jamais un fichier
                string args = Elevation.Joindre(new[] { connus[c], Path.Combine(cible, c), "/E", "/XJ", "/XA:O", "/R:1", "/W:1", "/MT:4", "/NP", "/NFL", "/NDL", "/NJH", "/NJS", "/XD", dossierSortie });
                int code; Programmes.Executer(robocopy, args, 24 * 3600000, null, out code);
                if (code >= 8 || code < 0) { erreurs.Add(c); Ecrire("    " + c + " : certains fichiers n'ont pas pu être copiés (code " + code + ")."); }
                else Ecrire("    " + c + " : OK");
            }
            long copie = Math.Max(0, taille() - avant);
            Ecrire("  -> Sauvegarde automatique à jour : " + T(copie) + " de nouveautés copiées dans " + cible);
            resumeAutres.Add("Sauvegarde automatique : " + T(copie) + " de nouveautés copiées vers " + cible);
            FichierSur.AjouterLigne(Path.Combine(dossierSortie, "sauvegardes.csv"), DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ";" + copie + ";" + cible);
            try
            {
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(Path.Combine(Chemins.Local, "sauvegarde-auto.json"), Json.Ecrire(new Dictionary<string, object>
                {
                    { "Date", DateTime.Now.ToString("o") }, { "Octets", copie }, { "Cible", cible }, { "Erreurs", erreurs.ToArray() }
                }));
            }
            catch { }
            Ecrire();
        }

        // ------------------------------------------------------------ Bilan d'optimisation (lecture seule)
        private void BilanOptimisation()
        {
            Ecrire("=== Bilan d'optimisation ===");
            var os = Wmi.Requete("SELECT LastBootUpTime, TotalVisibleMemorySize FROM Win32_OperatingSystem").FirstOrDefault();
            var boot = os == null ? null : Wmi.Date(os, "LastBootUpTime");
            if (boot.HasValue)
            {
                var depuis = DateTime.Now - boot.Value;
                Ecrire("  Allumé depuis : " + depuis.Days + " jour(s) " + depuis.Hours + " h (dernier démarrage le " + boot.Value.ToString("dd/MM/yyyy HH:mm") + ")");
                if (depuis.TotalDays >= 7) conseilsOptim.Add("Redémarre le PC : il n'a pas été redémarré depuis plus de 7 jours (« Arrêter » ne suffit pas toujours à cause du démarrage rapide).");
            }
            var evt = Evenements.Lire("Microsoft-Windows-Diagnostics-Performance/Operational", "*[System[EventID=100]]", 1).FirstOrDefault();
            if (evt != null)
            {
                try
                {
                    var m = Regex.Match(evt.ToXml(), "<Data Name='BootTime'>(\\d+)</Data>");
                    if (m.Success)
                    {
                        long sec = (long)Math.Round(long.Parse(m.Groups[1].Value) / 1000.0);
                        Ecrire("  Durée du dernier démarrage mesurée par Windows : " + sec + " s");
                        if (sec >= 90) conseilsOptim.Add("Démarrage lent (" + sec + " s) : désactive les programmes inutiles au démarrage (page Démarrage).");
                    }
                }
                catch { }
            }
            var demarrage = Wmi.Requete("SELECT Name FROM Win32_StartupCommand").Select(o => Wmi.Texte(o, "Name")).OrderBy(x => x).ToList();
            Ecrire("  Programmes lancés au démarrage : " + demarrage.Count);
            foreach (var s in demarrage.Take(15)) Ecrire("    - " + s);
            if (demarrage.Count >= 10) conseilsOptim.Add(demarrage.Count + " programmes se lancent au démarrage : désactive ceux dont tu n'as pas besoin tout de suite (page Démarrage).");

            string actif = Alimentation.PlanActif();
            if (actif != "")
            {
                Ecrire("  Mode d'alimentation : " + Alimentation.NomPlanActif());
                if (actif == Alimentation.Economie) conseilsOptim.Add("Le mode « Économie d'énergie » est actif : il bride les performances. Bouton « Mode performances » ou « Mode équilibré » pour changer.");
                else if (!InfosPc.EstPortable && actif == Alimentation.Equilibre) conseilsOptim.Add("PC fixe en mode « Équilibré » : le « Mode performances » peut le rendre un peu plus réactif (consommation légèrement supérieure).");
            }
            object ss = Registre.Lire(@"HKCU\Software\Microsoft\Windows\CurrentVersion\StorageSense\Parameters\StoragePolicy", "01");
            if (ss is int && (int)ss == 1) Ecrire("  Assistant de stockage de Windows : activé");
            else
            {
                Ecrire("  Assistant de stockage de Windows : désactivé");
                conseilsOptim.Add("Active l'Assistant de stockage (Paramètres > Système > Stockage) : Windows supprimera lui-même ses fichiers temporaires entre deux nettoyages.");
            }
            double ramGo = Math.Round(InfosPc.RamGo);
            if (ramGo > 0 && ramGo <= 8) conseilsOptim.Add("Avec " + ramGo + " Go de RAM, tu peux gagner en fluidité en réduisant les effets visuels : touche Windows, tape « Ajuster l'apparence et les performances de Windows », choisis « Ajuster afin d'obtenir les meilleures performances ».");
            var bar = Barrette.Lister();
            int prevue = Barrette.VitessePrevue(bar), reelle = Barrette.VitesseReelle(bar);
            if (prevue > 0 && reelle > 0 && reelle < prevue - 100) conseilsOptim.Add("Mémoire vive à " + reelle + " MHz au lieu de " + prevue + " MHz : active le profil XMP/EXPO dans le BIOS (Réglages > Matériel explique comment).");

            string winget = InfosPc.Winget();
            if (winget != null)
            {
                int n = WingetMisesAJour(winget).Count;
                if (n > 0) { Ecrire("  Mises à jour d'applications disponibles : " + n); conseilsOptim.Add(n + " application(s) peuvent être mises à jour : bouton « Mettre à jour les applications » (corrige aussi des failles de sécurité)."); }
                else Ecrire("  Applications : à jour (selon winget)");
            }
            Ecrire();
            if (conseilsOptim.Count == 0) Ecrire("  -> Aucune optimisation importante à faire : le PC est bien réglé.");
            else { Ecrire("  -> " + conseilsOptim.Count + " conseil(s) d'optimisation :"); foreach (var c in conseilsOptim) Ecrire("     - " + c); }
            Ecrire();
        }

        // ------------------------------------------------------------ Mode d'alimentation
        private void ModeAlimentation()
        {
            Ecrire("=== Mode d'alimentation ===");
            string g = O.ModeAlim == "Performances" ? Alimentation.Performances : Alimentation.Equilibre;
            string nom = O.ModeAlim == "Performances" ? "Performances élevées" : "Équilibré";
            if (O.Simulation) { Ecrire("  Le mode « " + nom + " » serait activé."); Ecrire(); return; }
            string liste = Alimentation.Liste();
            if (!liste.Contains(g) && O.ModeAlim == "Performances") { Alimentation.Dupliquer(g); liste = Alimentation.Liste(); }
            if (liste.Contains(g))
            {
                string avant = Alimentation.PlanActif();
                if (Alimentation.Activer(g))
                {
                    Ecrire("  OK : mode « " + nom + " » activé.");
                    if (avant != "" && avant != g) Historique.Ajouter("alimentation", "Mode d'alimentation passé à « " + nom + " »", new Dictionary<string, object> { { "Avant", avant }, { "Apres", g } });
                    resumeAutres.Add("Mode d'alimentation : " + nom);
                    if (O.ModeAlim == "Performances" && InfosPc.EstPortable) Ecrire("  Sur un portable, ce mode réduit l'autonomie sur batterie : repasse en « Mode équilibré » quand tu es débranché.");
                }
                else Ecrire("  Impossible d'activer ce mode sur ce PC.");
            }
            else Ecrire("  Le mode « " + nom + " » n'est pas disponible sur ce PC (fréquent sur les PC à veille moderne). Utilise le curseur Paramètres > Système > Alimentation.");
            Ecrire();
        }

        // ------------------------------------------------------------ Applications (winget)
        private static string NettoyerLigne(string l) { return Regex.Replace(l ?? "", @"[^\x20-\x7E\u00C0-\u017F\u2026]", "").TrimEnd(); }

        public static List<string> WingetMisesAJour(string winget)
        {
            var sortie = Programmes.Executer(winget, "upgrade --accept-source-agreements --disable-interactivity", 180000, Encoding.UTF8);
            return sortie.Split('\n').Select(x => x.TrimEnd('\r')).Where(x => Regex.IsMatch(x, @"\S+\s+\S+\s+\d[\w\.\-]*\s+\d[\w\.\-]*\s+\w+\s*$") && !Regex.IsMatch(x, "^-+$")).ToList();
        }

        private void MajApplications()
        {
            Ecrire("=== Mise à jour des applications ===");
            string winget = InfosPc.Winget();
            if (winget == null) { Ecrire("  winget (Gestionnaire de package Windows) est introuvable. Installe « App Installer » depuis le Microsoft Store, puis réessaie."); Ecrire(); return; }
            if (O.Simulation)
            {
                Ecrire("  Applications qui seraient mises à jour :");
                foreach (var l in Programmes.Executer(winget, "upgrade --accept-source-agreements --disable-interactivity", 180000, Encoding.UTF8).Split('\n'))
                {
                    string p = NettoyerLigne(l);
                    if (p != "" && !Regex.IsMatch(p, @"^[\s\-\\/|]+$")) Ecrire("    " + p);
                }
            }
            else
            {
                Ecrire("  Mise à jour de toutes les applications connues de winget (peut durer longtemps ; ferme les applications concernées si possible)...");
                Programmes.ExecuterLignes(winget, "upgrade --all --silent --accept-source-agreements --accept-package-agreements --disable-interactivity", l =>
                {
                    string p = NettoyerLigne(l);
                    if (p != "" && !Regex.IsMatch(p, @"^[\s\-\\/|█▒]+$") && !Regex.IsMatch(p, @"^\s*[\d\.]+\s*(KB|MB|GB|Ko|Mo|Go)\s*/")) Ecrire("    " + p);
                }, Encoding.UTF8, 3 * 3600000);
                Ecrire("  -> Mise à jour des applications terminée.");
                Ecrire("  (Certaines applications, comme les jeux ou les logiciels sous licence, se mettent à jour elles-mêmes et ne sont pas gérées par winget.)");
                resumeAutres.Add("Applications mises à jour (winget)");
                if (O.TacheApps)
                {
                    try { Directory.CreateDirectory(Chemins.Local); File.WriteAllText(MajAppsAuto.FichierDerniere, DateTime.Now.ToString("o")); } catch { }
                    Notification.Afficher(Langue.T("Nettoyage PC : applications mises à jour"), Langue.T("Tes applications ont été mises à jour par winget, l'outil officiel de Microsoft (sauf celles que tu as exclues)."));
                }
            }
            Ecrire();
        }

        // ------------------------------------------------------------ Windows Update
        private void MajWindows()
        {
            Ecrire("=== Mises à jour Windows et pilotes ===");
            try
            {
                dynamic session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session"));
                session.ClientApplicationID = "Nettoyage PC";
                Ecrire("  Recherche auprès de Windows Update...");
                dynamic trouvees = session.CreateUpdateSearcher().Search("IsInstalled=0 and IsHidden=0").Updates;
                var ids = O.IdsMaj.Split(',').Select(x => x.Trim()).Where(x => x != "").ToList();
                dynamic choix = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl"));
                int n = trouvees.Count;
                for (int i = 0; i < n; i++)
                {
                    dynamic u = trouvees.Item(i);
                    string id = u.Identity.UpdateID;
                    if (ids.Count > 0) { if (!ids.Contains(id, StringComparer.OrdinalIgnoreCase)) continue; }
                    else if ((bool)u.BrowseOnly) continue;
                    choix.Add(u);
                }
                if ((int)choix.Count == 0) { Ecrire("  -> Aucune des mises à jour choisies n'est encore en attente (déjà installées ?)."); Ecrire(); return; }
                for (int i = 0; i < (int)choix.Count; i++) { dynamic u = choix.Item(i); Ecrire("  - " + (string)u.Title + "  (" + T((double)u.MaxDownloadSize) + ")"); }
                if (O.Simulation) { Ecrire("  (Simulation : " + (int)choix.Count + " mise(s) à jour seraient téléchargées et installées.)"); Ecrire(); return; }
                if (!Admin) { Ecrire("  Installation impossible sans l'autorisation administrateur."); Ecrire(); return; }
                for (int i = 0; i < (int)choix.Count; i++) { dynamic u = choix.Item(i); if (!(bool)u.EulaAccepted) { try { u.AcceptEula(); } catch { } } }
                Ecrire("  Téléchargement (peut être long selon la connexion)...");
                dynamic tele = session.CreateUpdateDownloader(); tele.Updates = choix; tele.Download();
                dynamic aInstaller = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl"));
                for (int i = 0; i < (int)choix.Count; i++) { dynamic u = choix.Item(i); if ((bool)u.IsDownloaded) aInstaller.Add(u); else Ecrire("    ATTENTION : téléchargement impossible : " + (string)u.Title); }
                if ((int)aInstaller.Count > 0)
                {
                    Ecrire("  Installation (n'éteins pas le PC)...");
                    dynamic inst = session.CreateUpdateInstaller(); inst.Updates = aInstaller;
                    dynamic r = inst.Install();
                    int ok = 0, ko = 0;
                    for (int i = 0; i < (int)aInstaller.Count; i++)
                    {
                        int code = (int)r.GetUpdateResult(i).ResultCode;
                        string t = aInstaller.Item(i).Title;
                        if (code == 2 || code == 3) { Ecrire("    OK : " + t); ok++; } else { Ecrire("    ATTENTION : échec (" + code + ") : " + t); ko++; }
                    }
                    Ecrire("  -> " + ok + " mise(s) à jour installée(s), " + ko + " en échec.");
                    resumeAutres.Add("Mises à jour Windows et pilotes : " + ok + " installée(s)" + (ko > 0 ? ", " + ko + " en échec" : ""));
                    if ((bool)r.RebootRequired) { Ecrire("  Redémarrage nécessaire pour terminer l'installation."); resumeAutres.Add("Redémarre le PC pour terminer les mises à jour"); }
                }
            }
            catch (Exception ex) { Ecrire("  ATTENTION : Windows Update ne répond pas (" + ex.Message + "). Vérifie la connexion Internet, ou ouvre Paramètres > Windows Update."); }
            Ecrire();
        }

        // ------------------------------------------------------------ Bilan de sécurité et antivirus
        private void AlerteSecu(string texte) { alertesSecurite.Add(texte); Ecrire("    ATTENTION : " + texte); }

        private void BilanSecurite()
        {
            Ecrire("=== Bilan de sécurité ===");
            var avs = InfosPc.Antivirus();
            if (avs.Count > 0) Ecrire("  Antivirus déclaré(s) à Windows : " + string.Join(", ", avs));
            const string nsDef = @"root\Microsoft\Windows\Defender";
            var mp = Wmi.Requete("SELECT * FROM MSFT_MpComputerStatus", nsDef).FirstOrDefault();
            bool defenderActif = false;
            if (mp == null) Ecrire("  Microsoft Defender : non disponible sur ce PC (un autre antivirus le remplace ou il a été retiré).");
            else
            {
                defenderActif = Wmi.Bool(mp, "AntivirusEnabled") && Wmi.Bool(mp, "RealTimeProtectionEnabled");
                if (defenderActif)
                {
                    long ageSig = Wmi.Long(mp, "AntivirusSignatureAge"), ageScan = Wmi.Long(mp, "QuickScanAge");
                    Ecrire("  Microsoft Defender : protection en temps réel active");
                    Ecrire("    Signatures antivirus : il y a " + ageSig + " jour(s) ; dernière analyse rapide : il y a " + ageScan + " jour(s)");
                    if (ageSig >= 7) AlerteSecu("Signatures de Defender vieilles de " + ageSig + " jours : lance « Analyse antivirus » (elle les met à jour) et vérifie Windows Update.");
                }
                else if (avs.Count <= 1) AlerteSecu("La protection en temps réel de Microsoft Defender est désactivée et aucun autre antivirus n'est déclaré : réactive-la dans Sécurité Windows > Protection contre les virus et menaces.");
                else Ecrire("  Microsoft Defender : en retrait (un autre antivirus le remplace, c'est normal).");
                int menaces = Wmi.Requete("SELECT InitialDetectionTime FROM MSFT_MpThreatDetection", nsDef).Count(o => (Wmi.Date(o, "InitialDetectionTime") ?? DateTime.MinValue) > DateTime.Now.AddDays(-30));
                if (menaces > 0) AlerteSecu(menaces + " menace(s) détectée(s) par Defender ces 30 derniers jours : consulte Sécurité Windows > Historique de protection.");
            }
            var off = PareFeuDesactive();
            if (off.Count == 0) Ecrire("  Pare-feu Windows : actif sur tous les profils");
            else AlerteSecu("Pare-feu Windows désactivé pour : " + string.Join(", ", off) + ". Réactive-le dans Sécurité Windows > Pare-feu et protection du réseau (sauf si un autre pare-feu le remplace).");
            object lua = Registre.Lire(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableLUA");
            if (lua is int && (int)lua == 0) AlerteSecu("Le contrôle de compte d'utilisateur (UAC) est désactivé : n'importe quel programme peut modifier Windows sans te demander. Réactive-le (touche Windows, tape « UAC »).");
            else Ecrire("  Contrôle de compte d'utilisateur (UAC) : actif");
            object sb = Registre.Lire(@"HKLM\SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled");
            if (sb is int && (int)sb == 1) Ecrire("  Démarrage sécurisé (Secure Boot) : actif");
            else if (sb is int) Ecrire("  Démarrage sécurisé (Secure Boot) : désactivé (il protège contre les logiciels malveillants au démarrage ; il s'active dans le BIOS/UEFI).");
            string sd = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            var bl = Wmi.Requete("SELECT DriveLetter, ProtectionStatus FROM Win32_EncryptableVolume", @"root\CIMV2\Security\MicrosoftVolumeEncryption").FirstOrDefault(o => Wmi.Texte(o, "DriveLetter").Equals(sd, StringComparison.OrdinalIgnoreCase));
            if (bl != null)
            {
                if (Wmi.Long(bl, "ProtectionStatus") == 1) Ecrire("  Chiffrement BitLocker de " + sd + " : actif (garde ta clé de récupération en lieu sûr)");
                else Ecrire("  Chiffrement BitLocker de " + sd + " : inactif (utile surtout sur un portable, en cas de vol).");
            }
            var smb = Wmi.Requete("SELECT EnableSMB1Protocol FROM MSFT_SmbServerConfiguration", @"root\Microsoft\Windows\SMB").FirstOrDefault();
            if (smb != null && Wmi.Bool(smb, "EnableSMB1Protocol"))
                AlerteSecu("L'ancien protocole de partage SMB1 est actif : il est vulnérable (rançongiciel WannaCry). Désactive-le sauf si un vieil appareil réseau en a besoin : « Activer ou désactiver des fonctionnalités Windows » > « Support de partage de fichiers SMB 1.0 ».");
            var invite = Wmi.Requete("SELECT SID, Disabled FROM Win32_UserAccount WHERE LocalAccount=True").FirstOrDefault(o => Wmi.Texte(o, "SID").EndsWith("-501"));
            if (invite != null && !Wmi.Bool(invite, "Disabled")) AlerteSecu("Le compte Invité est activé : désactive-le s'il ne sert pas (Gestion de l'ordinateur > Utilisateurs et groupes locaux).");
            DateTime? derniere = null; string kb = "";
            foreach (var h in Wmi.Requete("SELECT HotFixID, InstalledOn FROM Win32_QuickFixEngineering"))
            {
                DateTime d;
                if (DateTime.TryParse(Wmi.Texte(h, "InstalledOn"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d) && (!derniere.HasValue || d > derniere)) { derniere = d; kb = Wmi.Texte(h, "HotFixID"); }
            }
            if (derniere.HasValue)
            {
                int age = (int)(DateTime.Now - derniere.Value).TotalDays;
                Ecrire("  Dernière mise à jour Windows installée : il y a " + age + " jour(s) (" + kb + ")");
                if (age >= 45) AlerteSecu("Aucune mise à jour Windows depuis " + age + " jours : ouvre Paramètres > Windows Update et installe les mises à jour.");
            }
            if (InfosPc.RedemarrageEnAttente()) Ecrire("  Un redémarrage est en attente pour terminer des mises à jour : redémarre le PC dès que possible.");
            if (Admin)
            {
                int nb = NombrePoints(); var p = DernierPoint();
                if (nb == 0 || p == null) Ecrire("  Points de restauration : aucun (le bouton « Créer un point de restauration » en crée un).");
                else Ecrire("  Points de restauration : " + nb + " (le plus récent : " + p.Item3.ToString("dd/MM/yyyy") + ")");
            }
            if (O.Antivirus)
            {
                Ecrire();
                string mpcmd = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Windows Defender\MpCmdRun.exe");
                if (mp == null || !File.Exists(mpcmd)) Ecrire("  Microsoft Defender est indisponible : lance une analyse avec ton antivirus.");
                else if (!defenderActif) Ecrire("  Microsoft Defender n'est pas l'antivirus actif : lance l'analyse depuis ton antivirus.");
                else if (O.Simulation) Ecrire("  Les signatures de Defender seraient mises à jour puis une analyse rapide serait lancée.");
                else
                {
                    Ecrire("  Mise à jour des signatures de Microsoft Defender...");
                    Programmes.Executer(mpcmd, "-SignatureUpdate", 600000);
                    Ecrire("  Analyse rapide en cours (quelques minutes)...");
                    DateTime debut = DateTime.Now;
                    Programmes.Executer(mpcmd, "-Scan -ScanType 1", 3 * 3600000);
                    int nouvelles = Wmi.Requete("SELECT InitialDetectionTime FROM MSFT_MpThreatDetection", nsDef).Count(o => (Wmi.Date(o, "InitialDetectionTime") ?? DateTime.MinValue) >= debut.AddSeconds(-5));
                    if (nouvelles == 0) { Ecrire("  -> Analyse rapide terminée : aucune menace détectée."); resumeAutres.Add("Analyse antivirus : aucune menace"); }
                    else AlerteSecu(nouvelles + " menace(s) trouvée(s) et traitée(s) par Defender : consulte Sécurité Windows > Historique de protection, puis lance une analyse complète.");
                }
            }
            Ecrire();
            if (alertesSecurite.Count == 0) Ecrire("  -> Aucun problème de sécurité détecté.");
            else Ecrire("  -> " + alertesSecurite.Count + " point(s) de sécurité à corriger (détails ci-dessus). Nettoyage PC ne modifie pas ces réglages à ta place.");
            Ecrire();
        }

        public static List<string> PareFeuDesactive()
        {
            var l = new List<string>();
            var profils = new[] { Tuple.Create("DomainProfile", "Domaine"), Tuple.Create("StandardProfile", "Privé"), Tuple.Create("PublicProfile", "Public") };
            foreach (var p in profils)
            {
                object v = Registre.Lire(@"HKLM\SYSTEM\CurrentControlSet\Services\SharedAccess\Parameters\FirewallPolicy\" + p.Item1, "EnableFirewall");
                if (v is int && (int)v == 0) l.Add(p.Item2);
            }
            return l;
        }

        // ------------------------------------------------------------ Réparation (DISM + SFC)
        private void Reparation()
        {
            Ecrire("=== Réparation de Windows ===");
            if (O.Simulation) { Ecrire("  DISM vérifierait et réparerait l'image de Windows, puis SFC vérifierait et réparerait les fichiers système."); Ecrire(); return; }
            if (!Admin) { Ecrire("  Impossible sans l'autorisation administrateur."); Ecrire(); return; }
            Ecrire("  1/2  DISM : vérification et réparation de l'image de Windows (10 à 30 min, connexion Internet conseillée)...");
            int dism = Programmes.ExecuterLignes(Path.Combine(Chemins.Systeme32, "dism.exe"), "/Online /Cleanup-Image /RestoreHealth /NoRestart", l => { });
            if (dism == 0) Ecrire("  DISM : image de Windows saine ou réparée.");
            else Ecrire("  DISM a renvoyé le code " + dism + " (détails : C:\\Windows\\Logs\\DISM\\dism.log).");
            Ecrire("  2/2  SFC : vérification des fichiers système (10 à 20 min)...");
            int sfcCode;
            string sortie = Programmes.Executer(Path.Combine(Chemins.Systeme32, "sfc.exe"), "/scannow", 3 * 3600000, Encoding.Unicode, out sfcCode).Replace("\0", "");
            foreach (var l in sortie.Split('\n').Select(x => x.Trim()).Where(x => x != "" && !x.Contains("%")).Reverse().Take(3).Reverse()) Ecrire("  " + l);
            if (sfcCode == 0) Ecrire("  -> Réparation terminée. Redémarre le PC pour finaliser.");
            else Ecrire("  -> SFC signale des fichiers qu'il n'a pas pu réparer : redémarre, puis relance « Réparer Windows ». Détails : C:\\Windows\\Logs\\CBS\\CBS.log");
            resumeAutres.Add("Réparation de Windows effectuée (DISM " + dism + ", SFC " + sfcCode + ")");
            Ecrire();
        }
    }
}
