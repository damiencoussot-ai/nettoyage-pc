// Nettoyage PC - moteur : rapports, compatibilité, surveillance, entretien automatique
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal partial class Moteur
    {
        public static string Empreinte(string fichier)
        {
            try { using (var sha = SHA256.Create()) using (var f = File.OpenRead(fichier)) return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", ""); }
            catch { return null; }
        }

        // ------------------------------------------------------------ Rapports (lecture seule)
        private void Rapports()
        {
            Ecrire("=== Rapports : disques, gros fichiers, doublons, programmes ===");
            Ecrire("  Analyse en cours (cela peut prendre plusieurs minutes)...");
            long seuil = (long)O.SeuilMo * 1048576;
            var l = new List<string>
            {
                "RAPPORT DU PC - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"),
                "Ce rapport est en lecture seule : rien n'a été supprimé pour le produire.", "",
                "=== ESPACE DISQUE ==="
            };
            foreach (var d in DriveInfo.GetDrives())
            {
                try { if (d.DriveType == DriveType.Fixed && d.IsReady && d.TotalSize > 0) l.Add(d.Name.TrimEnd('\\') + "  " + T(d.AvailableFreeSpace) + " libres sur " + T(d.TotalSize) + " (" + (100.0 * d.AvailableFreeSpace / d.TotalSize).ToString("0", Appli.Fr) + " % libre)"); } catch { }
            }
            l.Add(""); l.Add("=== SANTÉ DES DISQUES ===");
            var disques = DisquePhysique.Lister(false);
            if (disques.Count == 0) l.Add("Information indisponible.");
            foreach (var d in disques) l.Add(d.Nom + " (" + d.Type + ", " + T(d.Taille) + ") - état : " + d.Sante);
            l.Add("");

            var racines = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Chemins.Documents, Chemins.Telechargements,
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Environment.GetFolderPath(Environment.SpecialFolder.MyMusic)
            }.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var gros = new List<FileInfo>();
            foreach (var r in racines)
                foreach (var f in FichiersSurs(r))
                {
                    try { if (f.Length >= seuil && !TailleFichiers.EstCloud(f.Attributes) && !f.FullName.StartsWith(dossierSortie, StringComparison.OrdinalIgnoreCase)) gros.Add(f); } catch { }
                }
            gros = gros.GroupBy(f => f.FullName, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            l.Add("=== LES 20 PLUS GROS FICHIERS (seuil : " + O.SeuilMo + " Mo) ===");
            l.Add("Dossiers analysés : " + string.Join(" | ", racines));
            foreach (var f in gros.OrderByDescending(x => x.Length).Take(20)) l.Add(Dr(T(f.Length), 10) + "  " + f.LastWriteTime.ToString("yyyy-MM-dd") + "  " + f.FullName);
            l.Add("");

            var doublons = gros.GroupBy(f => f.Length).Where(g => g.Count() > 1)
                .SelectMany(g => g.Select(f => new { F = f, H = Empreinte(f.FullName) })).Where(x => x.H != null)
                .GroupBy(x => x.H).Where(g => g.Count() > 1).ToList();
            long gaspille = 0;
            l.Add("=== DOUBLONS (contenu identique) ===");
            if (doublons.Count == 0) l.Add("Aucun doublon trouvé parmi les gros fichiers.");
            else
            {
                foreach (var d in doublons)
                {
                    long u = d.First().F.Length; gaspille += u * (d.Count() - 1);
                    l.Add(d.Count() + " x " + T(u) + " :");
                    foreach (var x in d) l.Add("    " + x.F.FullName);
                }
                l.Add("Espace récupérable en supprimant les copies : " + T(gaspille));
            }
            l.Add("");

            l.Add("=== DOSSIERS LES PLUS VOLUMINEUX DE TON PROFIL (espace réellement présent sur le disque) ===");
            var tailles = new List<Tuple<string, long>>();
            try { foreach (var d in new DirectoryInfo(Chemins.Profil).GetDirectories()) if (!EstReparse(d)) tailles.Add(Tuple.Create(d.Name, TailleFichiers.Dossier(d.FullName))); } catch { }
            foreach (var t in tailles.OrderByDescending(x => x.Item2).Take(10)) l.Add(Dr(T(t.Item2), 10) + "  " + t.Item1);
            l.Add("(AppData contient les données des applications : ne le vide pas à la main.)"); l.Add("");

            l.Add("=== INSTALLATEURS ET ARCHIVES DANS TÉLÉCHARGEMENTS (les 20 plus gros) ===");
            var ext = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".exe", ".msi", ".iso", ".zip", ".rar", ".7z" };
            var inst = FichiersSurs(Chemins.Telechargements).Where(f => { try { return ext.Contains(f.Extension) && !TailleFichiers.EstCloud(f.Attributes); } catch { return false; } })
                .OrderByDescending(f => f.Length).Take(20).ToList();
            if (inst.Count == 0) l.Add("Aucun.");
            foreach (var f in inst) l.Add(Dr(T(f.Length), 10) + "  " + f.LastWriteTime.ToString("yyyy-MM-dd") + "  " + f.FullName);
            l.Add("");

            l.Add("=== PROGRAMMES INSTALLÉS LES PLUS VOLUMINEUX (les 25 premiers) ===");
            foreach (var p in ProgrammesInstalles.Lister().Where(x => x.Octets > 0).OrderByDescending(x => x.Octets).Take(25)) l.Add(Dr(T(p.Octets), 10) + "  " + p.Nom);
            l.Add("(Désinstalle ceux dont tu ne te sers plus : page Applications.)"); l.Add("");

            l.Add("=== PROGRAMMES LANCÉS AU DÉMARRAGE ===");
            var dem = Wmi.Requete("SELECT Name, Location FROM Win32_StartupCommand").OrderBy(o => Wmi.Texte(o, "Name")).ToList();
            if (dem.Count == 0) l.Add("Information indisponible.");
            foreach (var s in dem) l.Add(Wmi.Texte(s, "Name") + "  [" + Wmi.Texte(s, "Location") + "]");
            l.Add("");

            l.Add("=== GROS FICHIERS SYSTÈME ===");
            string sd = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            try { var h = new FileInfo(sd + "\\hiberfil.sys"); if (h.Exists) l.Add("hiberfil.sys (veille prolongée) : " + T(h.Length) + ". Désactivable avec la commande 'powercfg /hibernate off' si tu n'utilises pas la veille prolongée."); } catch { }
            try { var pf = new FileInfo(sd + "\\pagefile.sys"); if (pf.Exists) l.Add("pagefile.sys (mémoire virtuelle) : " + T(pf.Length) + ". À laisser tel quel."); } catch { }
            if (Directory.Exists(sd + "\\Windows.old")) l.Add("Dossier Windows.old présent (ancienne installation de Windows) : à supprimer via Paramètres > Système > Stockage > Fichiers temporaires.");
            if (Admin)
            {
                var vss = Programmes.Executer(Path.Combine(Chemins.Systeme32, "vssadmin.exe"), "list shadowstorage", 30000).Split('\n').Select(x => x.Trim()).Where(x => x != "").ToList();
                if (vss.Count > 0) { l.Add("Espace utilisé par les points de restauration :"); foreach (var v in vss) l.Add("    " + v); }
            }
            FichierSur.Ecrire(rapport, string.Join("\r\n", l) + "\r\n");
            Ecrire("  -> " + gros.Count + " fichier(s) de " + O.SeuilMo + " Mo et plus, " + doublons.Count + " groupe(s) de doublons (" + T(gaspille) + " récupérables)");
            Ecrire();
        }

        // ------------------------------------------------------------ Compatibilité
        private void Compatibilite()
        {
            Func<bool, string> oui = v => v ? "disponible" : "indisponible";
            var avs = InfosPc.Antivirus();
            Ecrire("=== Configuration détectée ===");
            Ecrire("  Système      : " + InfosPc.VersionWindows + " - " + InfosPc.Edition + " (build " + Natif.Build + ", " + System.Globalization.CultureInfo.CurrentUICulture.Name + ")");
            Ecrire("  Architecture : " + (Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits") + " (" + Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE") + ")");
            var cs = Wmi.Requete("SELECT Manufacturer, Model FROM Win32_ComputerSystem").FirstOrDefault();
            string fab = cs == null ? "" : (Wmi.Texte(cs, "Manufacturer") + " " + Wmi.Texte(cs, "Model")).Trim();
            bool vm = System.Text.RegularExpressions.Regex.IsMatch(fab, "Virtual|VMware|VirtualBox|KVM|HVM|Parallels");
            Ecrire("  Ordinateur   : " + fab + " - " + (vm ? "machine virtuelle" : InfosPc.EstPortable ? "PC portable" : "PC fixe") + ", " + InfosPc.RamGo.ToString("0.#", Appli.Fr) + " Go de RAM");
            Ecrire("  Processeur   : " + InfosPc.Processeur());
            foreach (var g in InfosPc.CartesGraphiques()) Ecrire("  Graphique    : " + g.Nom + " (pilote " + g.VersionPilote + (g.DatePilote.HasValue ? " du " + g.DatePilote.Value.ToString("dd/MM/yyyy") : "") + ")");
            foreach (var d in DisquePhysique.Lister(false)) Ecrire("  Disque       : " + d.Nom + " (" + d.Type + ", " + d.Bus + ")");
            Ecrire("  Programme    : Nettoyage PC " + Appli.Version + " (.NET " + Environment.Version + "), " + (Chemins.EstInstalle ? "installé" : "version portable"));
            Ecrire("  Antivirus    : " + (avs.Count > 0 ? string.Join(", ", avs) : "aucun déclaré"));
            Ecrire();
            Ecrire("=== Fonctions disponibles sur ce PC ===");
            Ecrire("  Nettoyage (fichiers, caches, corbeille) : disponible");
            Ecrire("  Nettoyage de disque Windows             : " + oui(Programmes.ExisteSysteme("cleanmgr.exe")));
            Ecrire("  Optimisation des disques et SSD         : " + oui(Programmes.ExisteSysteme("defrag.exe")));
            Ecrire("  Points de restauration                  : " + oui(Wmi.Requete("SELECT * FROM meta_class WHERE __CLASS = 'SystemRestore'", @"root\default").Count > 0 || Natif.EstWindows));
            Ecrire("  Analyse antivirus Microsoft Defender    : " + oui(InfosPc.DefenderTempsReel()));
            Ecrire("  Mise à jour des applications (winget)   : " + oui(InfosPc.Winget() != null));
            Ecrire("  Test des barrettes de RAM               : " + oui(Programmes.ExisteSysteme("mdsched.exe")));
            Ecrire("  Assistance rapide (aide à distance)     : " + oui(Programmes.ExisteSysteme("quickassist.exe")));
            Ecrire("  Autorisation administrateur             : demandée par Windows au moment des actions qui touchent tout le PC");
            Ecrire();
            if (Natif.EstWindows && Natif.Build < 10240) Ecrire("  ATTENTION : Nettoyage PC est conçu pour Windows 10 et 11. Sur ce système, une partie des fonctions ne marchera pas.");
            else Ecrire("  -> Ce PC est compatible. Les fonctions indisponibles sont simplement ignorées.");
            if (InfosPc.Winget() == null) Ecrire("  (Pour la mise à jour des applications : installe « App Installer » depuis le Microsoft Store.)");
            if (!InfosPc.DefenderTempsReel() && avs.Count > 0) Ecrire("  (Un autre antivirus protège ce PC : utilise-le pour les analyses.)");
            if (vm) Ecrire("  (Machine virtuelle : les infos sur les barrettes et la santé des disques sont souvent indisponibles, c'est normal.)");
        }

        // ------------------------------------------------------------ Surveillance quotidienne (tâche planifiée)
        private void SurveillanceDisques()
        {
            string dossier = Chemins.Donnees;
            try { Directory.CreateDirectory(dossier); } catch { }
            string fichier = Path.Combine(dossier, "surveillance.json");
            var etat = new Dictionary<string, object>();
            var j = Json.LireFichier(fichier) as Dictionary<string, object>;
            if (j != null) foreach (var kv in j) etat[kv.Key] = Convert.ToString(kv.Value);
            string auj = DateTime.Now.ToString("yyyy-MM-dd");
            var alertes = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (d.DriveType != DriveType.Fixed || !d.IsReady || d.TotalSize < 20L * 1073741824) continue;
                    double pct = 100.0 * d.AvailableFreeSpace / d.TotalSize;
                    string cle = "disque_" + d.Name.Substring(0, 1);
                    if ((pct < 10 || d.AvailableFreeSpace < 10L * 1073741824) && Convert.ToString(Json.Valeur(etat, cle)) != auj)
                    {
                        alertes.Add("Disque " + d.Name.TrimEnd('\\') + " presque plein : " + (d.AvailableFreeSpace / 1073741824.0).ToString("0.0", Appli.Fr) + " Go libres (" + pct.ToString("0", Appli.Fr) + " %).");
                        etat[cle] = auj;
                    }
                }
                catch { }
            }
            foreach (var pd in DisquePhysique.Lister(false).Where(x => !x.EnBonneSante && x.Type != "Externe USB"))
            {
                string cle = "sante_" + pd.Id;
                if (Convert.ToString(Json.Valeur(etat, cle)) != auj) { alertes.Add("Disque " + pd.Nom + " : état de santé « " + pd.Sante + " ». Sauvegarde tes données."); etat[cle] = auj; }
            }
            if (alertes.Count > 0)
            {
                Notification.Afficher("Nettoyage PC : attention", string.Join(" ", alertes) + " Ouvre Nettoyage PC pour libérer de la place.");
                FichierSur.Ecrire(fichier, Json.Ecrire(etat));
            }
        }

        // ------------------------------------------------------------ Entretien automatique (tâches planifiées)
        public const string TacheHebdo = "Nettoyage PC - hebdomadaire";
        public const string TacheComplet = "Nettoyage PC - complet (toutes les 4 semaines)";
        public const string TacheSurveillance = "Nettoyage PC - surveillance des disques";
        public const string TacheAncienne = "Nettoyage PC hebdomadaire";

        public static bool TacheExiste(string nom)
        {
            try
            {
                dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                svc.Connect();
                dynamic t = svc.GetFolder("\\").GetTask(nom);
                return t != null;
            }
            catch { return false; }
        }

        private void Planification()
        {
            if (!Admin) { Ecrire("Opération impossible : il faut l'autorisation administrateur."); return; }
            string schtasks = Path.Combine(Chemins.Systeme32, "schtasks.exe");
            if (O.Deplanifier)
            {
                int supprimees = 0;
                foreach (var nom in new[] { TacheHebdo, TacheComplet, TacheAncienne, TacheSurveillance, MajAppsAuto.Tache })
                {
                    if (!TacheExiste(nom)) continue;
                    int c; Programmes.Executer(schtasks, "/Delete /TN \"" + nom + "\" /F", 30000, null, out c);
                    if (c == 0) { Ecrire("Tâche supprimée : " + nom); supprimees++; }
                }
                if (supprimees == 0) Ecrire("Aucune tâche de nettoyage planifiée trouvée.");
                return;
            }
            // Une tâche lancée avec les droits administrateur ne doit exécuter qu'un fichier que seul un administrateur
            // peut modifier (Program Files), sinon n'importe quel programme pourrait le remplacer.
            if (!Chemins.EstInstalle)
            {
                Ecrire("Opération impossible : l'entretien automatique ne peut être activé que si Nettoyage PC est installé (dossier Program Files). Installe-le avec NettoyagePC-Setup puis réactive l'entretien automatique.");
                return;
            }
            if (TacheExiste(TacheAncienne)) Programmes.Executer(schtasks, "/Delete /TN \"" + TacheAncienne + "\" /F", 30000);
            string utilisateur = WindowsIdentity.GetCurrent().Name;
            bool ok = CreerTache(TacheHebdo, "--tache hebdo", "Nettoyage standard : temporaires, caches, corbeille, vieux téléchargements.",
                    "<CalendarTrigger><StartBoundary>2026-01-04T10:00:00</StartBoundary><ScheduleByWeek><WeeksInterval>1</WeeksInterval><DaysOfWeek><Sunday /></DaysOfWeek></ScheduleByWeek></CalendarTrigger>", "PT3H", utilisateur)
                & CreerTache(TacheComplet, "--tache complet", "Nettoyage complet : standard + profond (Windows Update, DISM) + disques (santé, TRIM, optimisation) + sécurité.",
                    "<CalendarTrigger><StartBoundary>2026-01-04T11:00:00</StartBoundary><ScheduleByWeek><WeeksInterval>4</WeeksInterval><DaysOfWeek><Sunday /></DaysOfWeek></ScheduleByWeek></CalendarTrigger>", "PT6H", utilisateur)
                & CreerTache(TacheSurveillance, "--tache surveillance", "Prévient si un disque est presque plein ou en mauvaise santé (une fois par jour au maximum).",
                    "<CalendarTrigger><StartBoundary>2026-01-01T13:00:00</StartBoundary><ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay></CalendarTrigger><LogonTrigger><UserId>" + System.Security.SecurityElement.Escape(utilisateur) + "</UserId><Delay>PT15M</Delay></LogonTrigger>", "PT10M", utilisateur);
            if (ok)
            {
                Ecrire("Tâches planifiées créées :");
                Ecrire("  - Nettoyage standard : chaque dimanche à 10h00");
                Ecrire("  - Nettoyage complet  : toutes les 4 semaines, le dimanche à 11h00 (profond + disques + sécurité)");
                Ecrire("  - Surveillance       : chaque jour, alerte si un disque est presque plein ou en mauvaise santé");
                Ecrire("  Si le PC est éteint à l'heure prévue, elles s'exécutent dès qu'il est allumé.");
            }
            else Ecrire("Opération impossible : le Planificateur de tâches a refusé une tâche.");
        }

        /// <summary>Mises à jour automatiques des applications (Premium) : une tâche par semaine, ou sa suppression.</summary>
        private void PlanificationApps()
        {
            if (!Admin) { Ecrire("Opération impossible : il faut l'autorisation administrateur."); return; }
            string schtasks = Path.Combine(Chemins.Systeme32, "schtasks.exe");
            if (O.DeplanifierApps)
            {
                if (!TacheExiste(MajAppsAuto.Tache)) { Ecrire("Les mises à jour automatiques des applications n'étaient pas activées."); return; }
                int c; Programmes.Executer(schtasks, "/Delete /TN \"" + MajAppsAuto.Tache + "\" /F", 30000, null, out c);
                Ecrire(c == 0 ? "Mises à jour automatiques des applications désactivées." : "Opération impossible : le Planificateur de tâches a refusé la suppression.");
                return;
            }
            if (!Chemins.EstInstalle) { Ecrire("Opération impossible : les mises à jour automatiques ne peuvent être activées que si Nettoyage PC est installé (dossier Program Files)."); return; }
            string utilisateur = WindowsIdentity.GetCurrent().Name;
            bool ok = CreerTache(MajAppsAuto.Tache, "--tache applications", "Mises à jour des applications par winget, sauf celles exclues dans Nettoyage PC (Premium).",
                "<CalendarTrigger><StartBoundary>2026-01-07T19:00:00</StartBoundary><ScheduleByWeek><WeeksInterval>1</WeeksInterval><DaysOfWeek><Wednesday /></DaysOfWeek></ScheduleByWeek></CalendarTrigger>", "PT3H", utilisateur);
            if (ok)
            {
                Ecrire("Mises à jour automatiques des applications activées : chaque mercredi à 19h00.");
                Ecrire("  Si le PC est éteint à cette heure-là, elles se font dès qu'il est allumé.");
            }
            else Ecrire("Opération impossible : le Planificateur de tâches a refusé la tâche.");
        }

        private bool CreerTache(string nom, string arguments, string description, string declencheurs, string duree, string utilisateur)
        {
            string xml = "<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
                "<Task version=\"1.2\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">" +
                "<RegistrationInfo><Description>" + System.Security.SecurityElement.Escape(description) + "</Description><Author>Nettoyage PC</Author></RegistrationInfo>" +
                "<Triggers>" + declencheurs + "</Triggers>" +
                "<Principals><Principal id=\"Author\"><UserId>" + System.Security.SecurityElement.Escape(utilisateur) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>" +
                "<Settings><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
                "<StartWhenAvailable>true</StartWhenAvailable><ExecutionTimeLimit>" + duree + "</ExecutionTimeLimit><Enabled>true</Enabled><Priority>7</Priority></Settings>" +
                "<Actions Context=\"Author\"><Exec><Command>" + System.Security.SecurityElement.Escape(Chemins.Exe) + "</Command><Arguments>--moteur " + arguments + "</Arguments><WorkingDirectory>" + System.Security.SecurityElement.Escape(Chemins.Ici) + "</WorkingDirectory></Exec></Actions></Task>";
            // fichier temporaire dans le dossier temporaire de Windows (réservé aux administrateurs)
            string tmp = Path.Combine(Chemins.Windows, "Temp", "NettoyagePC-tache-" + Guid.NewGuid().ToString("N") + ".xml");
            try
            {
                File.WriteAllText(tmp, xml, Encoding.Unicode);
                int c; Programmes.Executer(Path.Combine(Chemins.Systeme32, "schtasks.exe"), "/Create /TN \"" + nom + "\" /XML \"" + tmp + "\" /F", 30000, null, out c);
                return c == 0;
            }
            catch { return false; }
            finally { try { File.Delete(tmp); } catch { } }
        }
    }

    /// <summary>Programmes installés (liste de « Applications installées »).</summary>
    internal class ProgrammeInstalle
    {
        public string Nom = "", Editeur = "", Version = "", Desinstaller = "", DesinstallerSilencieux = "", Cle = "", Dossier = "", Icone = "";
        public long Octets;
        public DateTime? Date;
        public bool Utilisateur;
    }

    internal static class ProgrammesInstalles
    {
        public static List<ProgrammeInstalle> Lister()
        {
            var l = new List<ProgrammeInstalle>();
            var sources = new[]
            {
                Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry64, false), Tuple.Create(RegistryHive.LocalMachine, RegistryView.Registry32, false),
                Tuple.Create(RegistryHive.CurrentUser, RegistryView.Registry64, true)
            };
            foreach (var s in sources)
            {
                try
                {
                    using (var b = RegistryKey.OpenBaseKey(s.Item1, s.Item2))
                    using (var u = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (u == null) continue;
                        foreach (var n in u.GetSubKeyNames())
                        {
                            try
                            {
                                using (var k = u.OpenSubKey(n))
                                {
                                    string nom = k.GetValue("DisplayName") as string, des = k.GetValue("UninstallString") as string;
                                    if (string.IsNullOrWhiteSpace(nom) || string.IsNullOrWhiteSpace(des)) continue;
                                    object sc = k.GetValue("SystemComponent"); if (sc is int && (int)sc == 1) continue;
                                    if (k.GetValue("ParentKeyName") != null) continue;
                                    string rt = k.GetValue("ReleaseType") as string ?? ""; if (rt.Contains("Update") || rt.Contains("Hotfix")) continue;
                                    var p = new ProgrammeInstalle
                                    {
                                        Nom = nom.Trim(), Editeur = (k.GetValue("Publisher") as string ?? "").Trim(), Version = (k.GetValue("DisplayVersion") as string ?? "").Trim(),
                                        Desinstaller = des, DesinstallerSilencieux = k.GetValue("QuietUninstallString") as string ?? "", Utilisateur = s.Item3, Cle = n,
                                        Dossier = (k.GetValue("InstallLocation") as string ?? "").Trim().Trim('"'), Icone = k.GetValue("DisplayIcon") as string ?? ""
                                    };
                                    object es = k.GetValue("EstimatedSize"); if (es is int) p.Octets = (long)(int)es * 1024;
                                    string d = k.GetValue("InstallDate") as string; DateTime dt;
                                    if (d != null && DateTime.TryParseExact(d, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out dt)) p.Date = dt;
                                    l.Add(p);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch { }
            }
            return l.GroupBy(p => p.Nom + "|" + p.Version, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(p => p.Nom, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
}
