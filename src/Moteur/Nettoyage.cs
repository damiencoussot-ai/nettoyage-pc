// Nettoyage PC - moteur : point de restauration, nettoyage des fichiers, téléchargements, nettoyage profond
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.ServiceProcess;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal partial class Moteur
    {
        // ------------------------------------------------------------ Point de restauration
        private void PointDeRestauration()
        {
            Ecrire("=== Point de restauration ===");
            if (O.Simulation) { Ecrire("  Un point de restauration serait créé avant les modifications (simulation : non créé)."); Ecrire(); return; }
            if (!Admin) { Ecrire("  Ignoré : il faut l'autorisation administrateur."); Ecrire(); return; }
            string sd = (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:") + "\\";
            object rp = Registre.Lire(@"HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore", "RPSessionInterval");
            if (rp is int && (int)rp == 0)
            {
                Ecrire("  La protection du système était désactivée sur " + sd.TrimEnd('\\') + " : activation (Windows réserve un peu d'espace disque pour les points de restauration).");
                try
                {
                    using (var cls = new ManagementClass(new ManagementScope(@"\\.\root\default"), new ManagementPath("SystemRestore"), null))
                    {
                        var p = cls.GetMethodParameters("Enable"); p["Drive"] = sd;
                        cls.InvokeMethod("Enable", p, null);
                    }
                }
                catch { }
            }
            var avant = DernierPoint();
            try
            {
                using (var cls = new ManagementClass(new ManagementScope(@"\\.\root\default"), new ManagementPath("SystemRestore"), null))
                {
                    var p = cls.GetMethodParameters("CreateRestorePoint");
                    p["Description"] = "Nettoyage PC - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm");
                    p["RestorePointType"] = 12;   // modification de réglages
                    p["EventType"] = 100;
                    var r = cls.InvokeMethod("CreateRestorePoint", p, null);
                    uint code = r == null ? 1 : Convert.ToUInt32(r["ReturnValue"]);
                    var apres = DernierPoint();
                    if (code != 0) Ecrire("  Point de restauration impossible (code " + code + ").");
                    else if (apres != null && (avant == null || apres.Item1 != avant.Item1)) { Ecrire("  OK : point de restauration créé."); resumeAutres.Add("Point de restauration créé"); }
                    else Ecrire("  Windows n'a pas créé de nouveau point : il en existe déjà un de moins de 24 h, qui reste utilisable.");
                }
            }
            catch (Exception ex)
            {
                Ecrire("  Point de restauration impossible : " + ex.Message);
                Ecrire("  (Les opérations continuent : elles restent sûres, le point de restauration est une précaution supplémentaire.)");
            }
            var d = DernierPoint();
            if (d != null) Ecrire("  Dernier point disponible : " + d.Item2 + " (" + d.Item3.ToString("dd/MM/yyyy HH:mm") + ")");
            Ecrire("  Pour revenir en arrière : touche Windows, tape « Créer un point de restauration », puis « Restauration du système ».");
            Ecrire();
        }

        /// <summary>(numéro, description, date) du point de restauration le plus récent, ou null.</summary>
        public static Tuple<long, string, DateTime> DernierPoint()
        {
            var l = Wmi.Requete("SELECT SequenceNumber, Description, CreationTime FROM SystemRestore", @"root\default");
            var o = l.OrderByDescending(x => Wmi.Long(x, "SequenceNumber")).FirstOrDefault();
            if (o == null) return null;
            return Tuple.Create(Wmi.Long(o, "SequenceNumber"), Wmi.Texte(o, "Description"), Wmi.Date(o, "CreationTime") ?? DateTime.MinValue);
        }

        public static int NombrePoints() { return Wmi.Requete("SELECT SequenceNumber FROM SystemRestore", @"root\default").Count; }

        // ------------------------------------------------------------ Suppression sûre
        private class Cible { public string Categorie, Nom, Chemin, Filtre; public int Jours; public bool Vides; }
        private readonly List<Cible> cibles = new List<Cible>();

        private void AjouterCible(string categorie, string nom, string chemin, int jours, bool vides = false, string filtre = "")
        {
            if (string.IsNullOrEmpty(chemin)) return;
            cibles.Add(new Cible { Categorie = categorie, Nom = nom, Chemin = chemin, Jours = jours, Vides = vides, Filtre = filtre });
        }

        private static bool EstReparse(FileSystemInfo f) { try { return (f.Attributes & FileAttributes.ReparsePoint) != 0; } catch { return true; } }

        /// <summary>Fichiers d'un dossier (récursif), sans jamais traverser un dossier « lien ».</summary>
        private static IEnumerable<FileInfo> FichiersSurs(string racine)
        {
            DirectoryInfo d0;
            try { d0 = new DirectoryInfo(racine); if (!d0.Exists || EstReparse(d0)) yield break; } catch { yield break; }
            var pile = new Stack<DirectoryInfo>(); pile.Push(d0);
            while (pile.Count > 0)
            {
                var d = pile.Pop();
                DirectoryInfo[] sous = new DirectoryInfo[0]; FileInfo[] fs = new FileInfo[0];
                try { sous = d.GetDirectories(); } catch { }
                foreach (var s in sous) if (!EstReparse(s)) pile.Push(s);
                try { fs = d.GetFiles(); } catch { }
                foreach (var f in fs) if (!EstReparse(f)) yield return f;
            }
        }

        private static IEnumerable<DirectoryInfo> DossiersSurs(string racine)
        {
            DirectoryInfo d0;
            try { d0 = new DirectoryInfo(racine); if (!d0.Exists || EstReparse(d0)) yield break; } catch { yield break; }
            var pile = new Stack<DirectoryInfo>(); pile.Push(d0);
            while (pile.Count > 0)
            {
                var d = pile.Pop();
                DirectoryInfo[] sous = new DirectoryInfo[0];
                try { sous = d.GetDirectories(); } catch { }
                foreach (var s in sous) if (!EstReparse(s)) { pile.Push(s); yield return s; }
            }
        }

        /// <summary>Supprime un fichier et renvoie l'espace libéré (en simulation : l'espace qui serait libéré).</summary>
        private long SupprimerFichier(FileInfo f, string racine)
        {
            long taille; try { taille = f.Length; } catch { return 0; }
            if (O.Simulation) return taille;
            if (!string.IsNullOrEmpty(racine) && !Protection.DansRacineSansLien(f.FullName, racine)) return 0;
            try { if ((f.Attributes & FileAttributes.ReadOnly) != 0) f.Attributes &= ~FileAttributes.ReadOnly; f.Delete(); } catch { }
            try { if (File.Exists(f.FullName)) return 0; } catch { return 0; }   // verrouillé : ignoré
            return taille;
        }

        private long ViderDossier(string chemin, int jours, bool dossiersVides)
        {
            long total = 0;
            if (!Directory.Exists(chemin)) return 0;
            var limite = DateTime.Now.AddDays(-jours);
            foreach (var f in FichiersSurs(chemin).ToList())
            {
                DateTime dt; try { dt = f.LastWriteTime; } catch { continue; }
                if (dt < limite) total += SupprimerFichier(f, chemin);
            }
            if (dossiersVides && !O.Simulation)
            {
                foreach (var d in DossiersSurs(chemin).OrderByDescending(x => x.FullName.Length).ToList())
                {
                    try
                    {
                        if (d.LastWriteTime < limite && Protection.DansRacineSansLien(Path.Combine(d.FullName, "x"), chemin) && !d.EnumerateFileSystemInfos().Any()) d.Delete();
                    }
                    catch { }
                }
            }
            return total;
        }

        private long ViderFichiers(string dossier, string filtre, int jours)
        {
            long total = 0;
            if (!Directory.Exists(dossier) || Natif.EstLien(dossier)) return 0;
            var limite = DateTime.Now.AddDays(-jours);
            FileInfo[] fs;
            try { fs = new DirectoryInfo(dossier).GetFiles(filtre); } catch { return 0; }
            foreach (var f in fs) { if (EstReparse(f)) continue; try { if (f.LastWriteTime < limite) total += SupprimerFichier(f, dossier); } catch { } }
            return total;
        }

        private long TraiterCibles(string categorie)
        {
            long sous = 0;
            foreach (var c in cibles.Where(x => x.Categorie == categorie))
            {
                long l = c.Filtre != "" ? ViderFichiers(c.Chemin, c.Filtre, c.Jours) : ViderDossier(c.Chemin, c.Jours, c.Vides);
                if (l > 0) Ecrire("  " + Col(c.Nom, 52) + " " + T(l));
                sous += l;
            }
            Ecrire("  -> Total " + categorie.ToLowerInvariant() + " : " + T(sous) + " " + verbe);
            Ecrire();
            return sous;
        }

        // ------------------------------------------------------------ Nettoyage standard
        private string Env(string nom) { return Environment.GetEnvironmentVariable(nom) ?? ""; }

        private void Nettoyer()
        {
            string la = Chemins.LocalAppData, ad = Chemins.AppData, pd = Env("ProgramData"), win = Chemins.Windows;
            var ouvertes = new[] { "chrome", "msedge", "brave", "firefox", "opera", "vivaldi", "discord", "slack", "Teams", "Code", "steam", "EpicGamesLauncher" }
                .Where(n => { try { return Process.GetProcessesByName(n).Length > 0; } catch { return false; } }).ToList();
            if (ouvertes.Count > 0)
            {
                Ecrire("  Applications ouvertes : " + string.Join(", ", ouvertes));
                Ecrire("  Leur cache ne sera nettoyé qu'en partie : ferme-les pour un meilleur résultat.");
                Ecrire();
            }

            // --- Windows
            Ecrire("=== Windows : temporaires, rapports d'erreurs, caches graphiques ===");
            string tempUtilisateur = Environment.GetEnvironmentVariable("NP_TEMP_UTILISATEUR");
            if (string.IsNullOrEmpty(tempUtilisateur)) tempUtilisateur = Path.GetTempPath().TrimEnd('\\');
            AjouterCible("Windows", "Temporaires utilisateur", tempUtilisateur, O.JoursTemp, true);
            AjouterCible("Windows", "Cache Internet Windows", Path.Combine(la, @"Microsoft\Windows\INetCache"), O.JoursTemp);
            AjouterCible("Windows", "Rapports de plantage utilisateur", Path.Combine(la, "CrashDumps"), 0);
            AjouterCible("Windows", "Rapports d'erreurs utilisateur", Path.Combine(la, @"Microsoft\Windows\WER"), 0);
            AjouterCible("Windows", "Cache des miniatures", Path.Combine(la, @"Microsoft\Windows\Explorer"), 0, false, "thumbcache_*.db");
            AjouterCible("Windows", "Cache shaders DirectX", Path.Combine(la, "D3DSCache"), 0);
            AjouterCible("Windows", "Cache shaders NVIDIA (DX)", Path.Combine(la, @"NVIDIA\DXCache"), 0);
            AjouterCible("Windows", "Cache shaders NVIDIA (GL)", Path.Combine(la, @"NVIDIA\GLCache"), 0);
            AjouterCible("Windows", "Cache shaders AMD", Path.Combine(la, @"AMD\DxCache"), 0);
            AjouterCible("Windows", "Cache shaders AMD (DX12)", Path.Combine(la, @"AMD\DxcCache"), 0);
            AjouterCible("Windows", "Cache shaders AMD (Vulkan)", Path.Combine(la, @"AMD\VkCache"), 0);
            AjouterCible("Windows", "Cache shaders AMD (OpenGL)", Path.Combine(la, @"AMD\GLCache"), 0);
            AjouterCible("Windows", "Cache shaders Intel", Path.Combine(la, @"Intel\ShaderCache"), 0);
            AjouterCible("Windows", "Cache shaders NVIDIA (compute)", Path.Combine(ad, @"NVIDIA\ComputeCache"), 0);
            if (Admin)
            {
                AjouterCible("Windows", "Cache shaders NVIDIA (système)", Path.Combine(pd, @"NVIDIA Corporation\NV_Cache"), 0);
                AjouterCible("Windows", "Temporaires Windows", Path.Combine(win, "Temp"), O.JoursTemp, true);
                AjouterCible("Windows", "Rapports d'erreurs système (archives)", Path.Combine(pd, @"Microsoft\Windows\WER\ReportArchive"), 0);
                AjouterCible("Windows", "Rapports d'erreurs système (file)", Path.Combine(pd, @"Microsoft\Windows\WER\ReportQueue"), 0);
                AjouterCible("Windows", "Rapports d'erreurs système (temp)", Path.Combine(pd, @"Microsoft\Windows\WER\Temp"), 0);
                AjouterCible("Windows", "Mini-dumps de plantage", Path.Combine(win, "Minidump"), 0);
                AjouterCible("Windows", "Dump mémoire complet", win, 0, false, "MEMORY.DMP");
                AjouterCible("Windows", "Journaux CBS de plus de 30 jours", Path.Combine(win, @"Logs\CBS"), 30, false, "*.log");
                // programmes d'installation des pilotes déjà décompressés par AMD et NVIDIA (souvent plusieurs Go) : ils ne servent plus
                // une fois le pilote installé (le prochain pilote se télécharge en entier de toute façon)
                string racine = Path.GetPathRoot(win);
                AjouterCible("Windows", "Restes d'installation des pilotes AMD (C:\\AMD)", Path.Combine(racine, "AMD"), 7, true);
                AjouterCible("Windows", "Restes d'installation des pilotes NVIDIA", Path.Combine(racine, @"NVIDIA\DisplayDriver"), 7, true);
            }
            else Ecrire("  (Temporaires système, mini-dumps et anciens journaux ignorés : choisis le nettoyage COMPLET pour les inclure)");
            totalWindows = TraiterCibles("Windows");

            // --- Navigateurs
            Ecrire("=== Navigateurs (cache uniquement) ===");
            var chromium = new[]
            {
                new { Nom = "Chrome", Racine = Path.Combine(la, @"Google\Chrome\User Data"), Profil = false },
                new { Nom = "Edge", Racine = Path.Combine(la, @"Microsoft\Edge\User Data"), Profil = false },
                new { Nom = "Brave", Racine = Path.Combine(la, @"BraveSoftware\Brave-Browser\User Data"), Profil = false },
                new { Nom = "Vivaldi", Racine = Path.Combine(la, @"Vivaldi\User Data"), Profil = false },
                new { Nom = "Opera", Racine = Path.Combine(la, @"Opera Software\Opera Stable"), Profil = true },
                new { Nom = "Opera GX", Racine = Path.Combine(la, @"Opera Software\Opera GX Stable"), Profil = true },
                new { Nom = "Opera (profil Roaming)", Racine = Path.Combine(ad, @"Opera Software\Opera Stable"), Profil = true },
                new { Nom = "Opera GX (profil Roaming)", Racine = Path.Combine(ad, @"Opera Software\Opera GX Stable"), Profil = true },
                new { Nom = "Chrome Beta", Racine = Path.Combine(la, @"Google\Chrome Beta\User Data"), Profil = false },
                new { Nom = "Chromium", Racine = Path.Combine(la, @"Chromium\User Data"), Profil = false },
                new { Nom = "Yandex", Racine = Path.Combine(la, @"Yandex\YandexBrowser\User Data"), Profil = false },
                new { Nom = "Edge Beta", Racine = Path.Combine(la, @"Microsoft\Edge Beta\User Data"), Profil = false }
            };
            var sousCaches = new[] { "Cache", "Code Cache", "GPUCache", "DawnCache", @"Service Worker\CacheStorage" };
            foreach (var nav in chromium)
            {
                if (!Directory.Exists(nav.Racine)) continue;
                var profils = new List<DirectoryInfo>();
                if (nav.Profil) profils.Add(new DirectoryInfo(nav.Racine));
                else
                {
                    try { profils.AddRange(new DirectoryInfo(nav.Racine).GetDirectories().Where(d => Regex.IsMatch(d.Name, @"^(Default|Profile \d+)$"))); } catch { }
                    foreach (var g in new[] { "ShaderCache", "GrShaderCache", "GraphiteDawnCache" }) AjouterCible("Navigateurs", nav.Nom + " - " + g, Path.Combine(nav.Racine, g), 0);
                }
                foreach (var p in profils) foreach (var s in sousCaches) AjouterCible("Navigateurs", nav.Nom + " (" + p.Name + ") - " + s, Path.Combine(p.FullName, s), 0);
            }
            foreach (var fam in new[] { new { Nom = "Firefox", Racine = Path.Combine(la, @"Mozilla\Firefox\Profiles") }, new { Nom = "Waterfox", Racine = Path.Combine(la, @"Waterfox\Profiles") },
                                         new { Nom = "LibreWolf", Racine = Path.Combine(la, @"librewolf\Profiles") }, new { Nom = "Floorp", Racine = Path.Combine(la, @"Floorp\Profiles") } })
            {
                if (!Directory.Exists(fam.Racine)) continue;
                DirectoryInfo[] ps = new DirectoryInfo[0];
                try { ps = new DirectoryInfo(fam.Racine).GetDirectories(); } catch { }
                foreach (var p in ps) foreach (var s in new[] { "cache2", "startupCache" }) AjouterCible("Navigateurs", fam.Nom + " (" + p.Name + ") - " + s, Path.Combine(p.FullName, s), 0);
            }
            totalNavigateurs = TraiterCibles("Navigateurs");
            Ecrire("  (Cookies, mots de passe, historiques et favoris ne sont pas touchés.)");
            Ecrire();

            // --- Applications
            Ecrire("=== Applications et outils de développement ===");
            foreach (var app in new[]
            {
                new { Nom = "Teams (classique)", Racine = Path.Combine(ad, @"Microsoft\Teams"), Sous = new[] { "Cache", "Code Cache", "GPUCache", "tmp" } },
                new { Nom = "Discord", Racine = Path.Combine(ad, "discord"), Sous = new[] { "Cache", "Code Cache", "GPUCache" } },
                new { Nom = "Discord PTB", Racine = Path.Combine(ad, "discordptb"), Sous = new[] { "Cache", "Code Cache", "GPUCache" } },
                new { Nom = "Discord Canary", Racine = Path.Combine(ad, "discordcanary"), Sous = new[] { "Cache", "Code Cache", "GPUCache" } },
                new { Nom = "Slack", Racine = Path.Combine(ad, "Slack"), Sous = new[] { "Cache", "Code Cache", "GPUCache", @"Service Worker\CacheStorage" } },
                new { Nom = "Visual Studio Code", Racine = Path.Combine(ad, "Code"), Sous = new[] { "Cache", "CachedData", "Code Cache", "GPUCache", "logs" } }
            })
            {
                if (!Directory.Exists(app.Racine)) continue;
                foreach (var s in app.Sous) AjouterCible("Applications", app.Nom + " - " + s, Path.Combine(app.Racine, s), 0);
            }
            AjouterCible("Applications", "Cache npm", Path.Combine(la, "npm-cache"), 0);
            AjouterCible("Applications", "Cache npm (ancien emplacement)", Path.Combine(ad, "npm-cache"), 0);
            AjouterCible("Applications", "Cache pip", Path.Combine(la, @"pip\Cache"), 0);
            AjouterCible("Applications", "Cache NuGet", Path.Combine(la, @"NuGet\v3-cache"), 0);
            AjouterCible("Applications", "Cache Yarn", Path.Combine(la, @"Yarn\Cache"), 0);
            AjouterCible("Applications", "Spotify - cache de lecture", Path.Combine(la, @"Spotify\Data"), 0);
            AjouterCible("Applications", "Spotify - cache navigateur", Path.Combine(la, @"Spotify\Browser\Cache"), 0);
            AjouterCible("Applications", "Steam - cache web", Path.Combine(la, @"Steam\htmlcache\Cache"), 0);
            AjouterCible("Applications", "Steam - cache web (code)", Path.Combine(la, @"Steam\htmlcache\Code Cache"), 0);
            AjouterCible("Applications", "Epic Games - cache web", Path.Combine(la, @"EpicGamesLauncher\Saved\webcache"), 0);
            AjouterCible("Applications", "Epic Games - cache web 4430", Path.Combine(la, @"EpicGamesLauncher\Saved\webcache_4430"), 0);
            AjouterCible("Applications", "Epic Games - cache web 4147", Path.Combine(la, @"EpicGamesLauncher\Saved\webcache_4147"), 0);
            AjouterCible("Applications", "Epic Games - journaux de plus de 7 jours", Path.Combine(la, @"EpicGamesLauncher\Saved\Logs"), 7);
            // Steam (dossier d'installation) : cache web, journaux anciens, rapports de plantage. Jamais les jeux ni leurs sauvegardes.
            string steam = "";
            try { steam = (Registre.Texte(@"HKCU\Software\Valve\Steam", "SteamPath") ?? "").Replace('/', '\\'); } catch { }
            if (steam != "" && Directory.Exists(steam))
            {
                AjouterCible("Applications", "Steam - cache des pages", Path.Combine(steam, @"appcache\httpcache"), 0);
                AjouterCible("Applications", "Steam - journaux de plus de 7 jours", Path.Combine(steam, "logs"), 7);
                AjouterCible("Applications", "Steam - rapports de plantage", Path.Combine(steam, "dumps"), 0);
            }
            if (Admin) AjouterCible("Applications", "Battle.net - cache", Path.Combine(pd, @"Battle.net\Agent\data\cache"), 0);
            AjouterCible("Applications", "Zoom - journaux", Path.Combine(ad, @"Zoom\logs"), 7);
            AjouterCible("Applications", "Adobe - cache média", Path.Combine(ad, @"Adobe\Common\Media Cache Files"), 30);
            AjouterCible("Applications", "Adobe - base du cache média", Path.Combine(ad, @"Adobe\Common\Media Cache"), 30);
            AjouterCible("Applications", "Microsoft Store - cache", Path.Combine(la, @"Packages\Microsoft.WindowsStore_8wekyb3d8bbwe\LocalCache"), 0);
            AjouterCible("Applications", "Teams (nouveau) - cache", Path.Combine(la, @"Packages\MSTeams_8wekyb3d8bbwe\LocalCache\Microsoft\MSTeams\EBWebView\Default\Cache"), 0);
            totalApplications = TraiterCibles("Applications");
            Ecrire("  (Ces caches se reconstituent tout seuls au besoin.)");
            Ecrire();

            // Nettoyage léger (gardien Premium, disque presque plein) : seulement les temporaires et les caches
            if (O.Leger)
            {
                Ecrire("=== Corbeille ===");
                Ecrire("  (Nettoyage léger : la Corbeille et les Téléchargements ne sont pas touchés.)");
                Ecrire();
                return;
            }

            // --- Corbeille
            Ecrire("=== Corbeille ===");
            long elements;
            tailleCorbeille = Natif.TailleCorbeille(out elements);
            if (!O.Simulation && elements > 0) Natif.ViderCorbeille();
            Ecrire("  -> Environ " + T(tailleCorbeille) + " " + verbe);
            Ecrire();

            RangerTelechargements();
            NettoyageProfond();
        }

        // ------------------------------------------------------------ Téléchargements : déplacés, jamais supprimés
        private static DateTime DateReference(FileSystemInfo e)
        {
            DateTime d = e.CreationTime;
            try { if (e.LastWriteTime > d) d = e.LastWriteTime; } catch { }
            var di = e as DirectoryInfo;
            if (di != null) foreach (var f in FichiersSurs(di.FullName)) { try { if (f.LastWriteTime > d) d = f.LastWriteTime; } catch { } }
            return d;
        }

        private void RangerTelechargements()
        {
            string dl = Chemins.Telechargements;
            Ecrire("=== Téléchargements de plus de " + O.JoursTelechargements + " jours ===");
            string aVerifier = Path.Combine(dl, "_A_verifier");
            var limite = DateTime.Now.AddDays(-O.JoursTelechargements);
            if (Directory.Exists(dl) && !Natif.EstLien(dl))
            {
                FileSystemInfo[] elements = new FileSystemInfo[0];
                try { elements = new DirectoryInfo(dl).GetFileSystemInfos(); } catch { }
                foreach (var el in elements)
                {
                    if (el.Name.Equals("_A_verifier", StringComparison.OrdinalIgnoreCase) || EstReparse(el)) continue;
                    if (el.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                    if (DateReference(el) >= limite) continue;
                    long t = 0;
                    if (el is DirectoryInfo) foreach (var f in FichiersSurs(el.FullName)) { try { t += f.Length; } catch { } }
                    else try { t = ((FileInfo)el).Length; } catch { }
                    if (O.Simulation) { nbDeplaces++; tailleDeplacee += t; Ecrire("  " + Dr(T(t), 10) + "  " + el.Name); continue; }
                    if (Directory.Exists(aVerifier) && !Protection.CheminSansLien(Path.Combine(aVerifier, "x")))
                    {
                        Ecrire("  Le dossier « _A_verifier » est un lien vers un autre emplacement : rangement ignoré par sécurité.");
                        break;
                    }
                    try { Directory.CreateDirectory(aVerifier); } catch { break; }
                    string dest = Path.Combine(aVerifier, el.Name);
                    if (File.Exists(dest) || Directory.Exists(dest)) dest = Path.Combine(aVerifier, DateTime.Now.ToString("HHmmss") + "_" + el.Name);
                    try
                    {
                        if (el is DirectoryInfo) Directory.Move(el.FullName, dest); else File.Move(el.FullName, dest);
                        nbDeplaces++; tailleDeplacee += t;
                        Historique.Ajouter("deplacement", "Téléchargement rangé dans _A_verifier : " + el.Name, new Dictionary<string, object> { { "Source", el.FullName }, { "Destination", dest } });
                    }
                    catch { }
                }
            }
            if (O.Simulation) Ecrire("  -> " + nbDeplaces + " élément(s) seraient déplacés vers '_A_verifier' (" + T(tailleDeplacee) + ")");
            else
            {
                Ecrire("  -> " + nbDeplaces + " élément(s) déplacés vers '" + aVerifier + "' (" + T(tailleDeplacee) + ")");
                if (nbDeplaces > 0) Ecrire("  Vérifie ce dossier et supprime toi-même ce dont tu n'as plus besoin.");
            }
            Ecrire();
        }

        // ------------------------------------------------------------ Nettoyage profond (administrateur)
        private void NettoyageProfond()
        {
            Ecrire("=== Nettoyage profond de Windows ===");
            if (!O.Profond) { Ecrire("  Ignoré (choisis l'optimisation complète pour l'inclure)."); Ecrire(); return; }
            if (!Admin) { Ecrire("  Ignoré : il faut l'autorisation administrateur."); Ecrire(); return; }
            string win = Chemins.Windows, lecteur = Path.GetPathRoot(win);
            if (InfosPc.RedemarrageEnAttente())
            {
                Ecrire("  Un redémarrage de Windows est en attente : cache Windows Update et DISM ignorés pour éviter tout risque.");
                Ecrire("  Redémarre le PC puis relance le nettoyage complet.");
            }
            else
            {
                // Cache de téléchargement de Windows Update : services arrêtés puis remis dans leur état d'origine
                var services = new[] { "wuauserv", "bits" };
                var etaient = new Dictionary<string, bool>();
                foreach (var s in services) { try { using (var sc = new ServiceController(s)) etaient[s] = sc.Status == ServiceControllerStatus.Running; } catch { etaient[s] = false; } }
                if (!O.Simulation)
                {
                    foreach (var s in services) if (etaient[s]) { try { using (var sc = new ServiceController(s)) { sc.Stop(); sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30)); } } catch { } }
                    Thread.Sleep(2000);
                }
                long o = 0;
                try { o = ViderDossier(Path.Combine(win, @"SoftwareDistribution\Download"), 0, true); }
                finally
                {
                    if (!O.Simulation) foreach (var s in services) if (etaient[s]) { try { using (var sc = new ServiceController(s)) sc.Start(); } catch { } }
                }
                if (o > 0) Ecrire("  " + Col("Cache de téléchargement Windows Update", 52) + " " + T(o));
                totalProfond += o;

                if (O.Simulation) Ecrire("  Nettoyage du magasin de composants (DISM) : non exécuté en simulation.");
                else
                {
                    Ecrire("  Nettoyage du magasin de composants Windows (DISM) : cela peut durer plusieurs minutes...");
                    int code = Programmes.ExecuterLignes(Path.Combine(Chemins.Systeme32, "dism.exe"), "/Online /Cleanup-Image /StartComponentCleanup /NoRestart", l => { });
                    if (code == 0) Ecrire("  DISM terminé."); else Ecrire("  DISM a renvoyé le code " + code + " (voir C:\\Windows\\Logs\\DISM\\dism.log).");
                }
            }

            long d = ViderDossier(Path.Combine(win, @"ServiceProfiles\NetworkService\AppData\Local\Microsoft\Windows\DeliveryOptimization\Cache"), 0, false);
            if (d > 0) Ecrire("  " + Col("Cache Delivery Optimization", 52) + " " + T(d));
            totalProfond += d;
            if (!O.Simulation) { Natif.ViderCacheDns(); Ecrire("  Cache DNS vidé."); }

            // Outil officiel « Nettoyage de disque », catégories choisies. Exclues : Téléchargements, Windows.old,
            // fichiers de réinstallation (pour garder la possibilité de revenir en arrière ou de réinitialiser le PC).
            var categories = new[]
            {
                "Active Setup Temp Folders", "Delivery Optimization Files", "Device Driver Packages", "Downloaded Program Files", "Internet Cache Files",
                "Old ChkDsk Files", "Setup Log Files", "System error memory dump files", "System error minidump files", "Temporary Files",
                "Temporary Setup Files", "Thumbnail Cache", "Update Cleanup", "Upgrade Discarded Files", "Windows Error Reporting Files",
                "Windows Upgrade Log Files", "D3D Shader Cache", "Diagnostic Data Viewer database files", "Feedback Hub Archive log files",
                "Language Pack", "RetailDemo Offline Content"
            };
            const string racineVc = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches\";
            var presentes = categories.Where(c => InfosPc.CleExiste(racineVc + c)).ToList();
            string cleanmgr = Path.Combine(Chemins.Systeme32, "cleanmgr.exe");
            if (!File.Exists(cleanmgr)) { presentes.Clear(); Ecrire("  Nettoyage de disque Windows : non disponible sur ce PC (ignoré)."); }
            if (O.Simulation) Ecrire("  Nettoyage de disque Windows : " + presentes.Count + " catégories seraient nettoyées (anciennes mises à jour, anciens pilotes, journaux d'installation...).");
            else if (presentes.Count > 0)
            {
                Ecrire("  Nettoyage de disque Windows (outil officiel) : anciennes mises à jour, anciens pilotes, journaux... (peut durer plusieurs minutes)");
                foreach (var c in presentes) { try { Registre.EcrireDirect(racineVc + c, "StateFlags4242", 2, RegistryValueKind.DWord); } catch { } }
                long avant = EspaceLibre(lecteur);
                try
                {
                    using (var p = Process.Start(new ProcessStartInfo(cleanmgr, "/sagerun:4242") { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                        if (p != null && !p.WaitForExit(5400000)) Ecrire("  Le nettoyage de disque est toujours en cours : il continuera seul en arrière-plan.");
                }
                catch { }
                foreach (var c in presentes) { try { Registre.SupprimerDirect(racineVc + c, "StateFlags4242"); } catch { } }
                long gain = EspaceLibre(lecteur) - avant;
                if (gain > 0) { Ecrire("  " + Col("Nettoyage de disque Windows", 52) + " " + T(gain)); totalProfond += gain; }
                else Ecrire("  Nettoyage de disque Windows terminé.");
            }
            Ecrire("  -> Total profond mesurable : " + T(totalProfond) + " " + verbe + " (DISM : voir l'espace libre dans le résumé)");
            Ecrire();
        }
    }
}
