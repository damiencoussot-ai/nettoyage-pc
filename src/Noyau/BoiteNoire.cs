// Velox 5.12 - Boîte noire du jeu : pendant que les FPS en direct tournent, chaque seconde est notée (images, processeur,
// carte graphique, mémoire, disque, températures, programme le plus actif). À chaque saccade, l'instant est figé et la
// cause la plus probable est cherchée. À la fin de la partie : FPS moyen, FPS minimum (1 % low), saccades et leurs causes.
// Tout reste sur le PC (boite-noire.json) ; rien n'est fermé ni modifié : la Boîte noire observe et explique.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    /// <summary>Un programme et ce qu'il consomme sur la dernière seconde.</summary>
    internal class ProgrammeActif
    {
        public string Nom = "";
        public int Pid;
        /// <summary>Pourcentage d'un cœur (100 = un cœur entier).</summary>
        public double Coeur;
        /// <summary>Lectures et écritures, en octets par seconde.</summary>
        public double Disque;
    }

    /// <summary>Activité des programmes, comme le Gestionnaire des tâches : compteurs « Process » de Windows, sans droits
    /// administrateur (ils voient aussi l'antivirus et Windows Update, qu'un programme normal ne peut pas ouvrir).</summary>
    internal static class ActiviteProgrammes
    {
        private static readonly object verrou = new object();
        private static Dictionary<string, CounterSample> cpuAvant, disqueAvant;
        private static bool indisponible;

        /// <summary>Programmes actifs depuis le relevé précédent (vide au premier appel : il faut deux relevés).</summary>
        public static List<ProgrammeActif> Lire()
        {
            var l = new List<ProgrammeActif>();
            if (indisponible || !OperatingSystem.IsWindows()) return l;
            lock (verrou)
            {
                try
                {
                    var donnees = new PerformanceCounterCategory("Process").ReadCategory();
                    var cpu = donnees["% Processor Time"];
                    var ids = donnees["ID Process"];
                    var disque = donnees["IO Data Bytes/sec"];
                    if (cpu == null) { indisponible = true; return l; }
                    var cpuMaintenant = new Dictionary<string, CounterSample>(StringComparer.OrdinalIgnoreCase);
                    var disqueMaintenant = new Dictionary<string, CounterSample>(StringComparer.OrdinalIgnoreCase);
                    var pids = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (InstanceData i in cpu.Values) cpuMaintenant[i.InstanceName] = i.Sample;
                    if (disque != null) foreach (InstanceData i in disque.Values) disqueMaintenant[i.InstanceName] = i.Sample;
                    if (ids != null) foreach (InstanceData i in ids.Values) pids[i.InstanceName] = (int)i.RawValue;
                    if (cpuAvant != null)
                    {
                        foreach (var kv in cpuMaintenant)
                        {
                            string nom = NomSansNumero(kv.Key);
                            if (nom == "_Total" || nom.Equals("Idle", StringComparison.OrdinalIgnoreCase)) continue;
                            CounterSample a;
                            if (!cpuAvant.TryGetValue(kv.Key, out a)) continue;
                            double c = 0, d = 0;
                            try { c = CounterSample.Calculate(a, kv.Value); } catch { }
                            CounterSample da, dm;
                            if (disqueAvant != null && disqueAvant.TryGetValue(kv.Key, out da) && disqueMaintenant.TryGetValue(kv.Key, out dm)) { try { d = CounterSample.Calculate(da, dm); } catch { } }
                            if (double.IsNaN(c) || c < 0) c = 0;
                            if (double.IsNaN(d) || d < 0) d = 0;
                            if (c < 1 && d < 1048576) continue;   // ni processeur ni disque : pas la peine de le garder
                            int pid; pids.TryGetValue(kv.Key, out pid);
                            l.Add(new ProgrammeActif { Nom = nom, Pid = pid, Coeur = c, Disque = d });
                        }
                    }
                    cpuAvant = cpuMaintenant; disqueAvant = disqueMaintenant;
                }
                catch (InvalidOperationException) { indisponible = true; }
                catch (Exception ex) { indisponible = true; JournalDemarrage.Noter("Activité des programmes non lue : " + ex.GetType().Name + " " + ex.Message); }
            }
            return l;
        }

        /// <summary>« chrome#3 » → « chrome ».</summary>
        public static string NomSansNumero(string instance)
        {
            int i = (instance ?? "").LastIndexOf('#');
            return i > 0 ? instance.Substring(0, i) : (instance ?? "");
        }

        private static PerformanceCounter compteurDisque;
        private static bool disqueIndisponible;
        /// <summary>Occupation des disques (0 à 100), ou -1. Le premier appel renvoie souvent 0.</summary>
        public static double Disque()
        {
            if (disqueIndisponible || !OperatingSystem.IsWindows()) return -1;
            try
            {
                if (compteurDisque == null) compteurDisque = new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total", true);
                double libre = compteurDisque.NextValue();
                if (double.IsNaN(libre)) return -1;
                return Math.Max(0, Math.Min(100, 100 - libre));
            }
            catch { disqueIndisponible = true; return -1; }
        }

        /// <summary>Programmes du système ou de la mesure elle-même : jamais désignés comme coupables.</summary>
        private static readonly HashSet<string> Ignores = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "System", "Registry", "Memory Compression", "smss", "csrss", "wininit", "winlogon", "lsass", "services", "dwm", "fontdrvhost", "NettoyagePC", "PresentMon", "audiodg", "Secure System", "Idle", "_Total", "conhost" };

        public static bool Ignore(string nom) { return Ignores.Contains(nom ?? ""); }

        /// <summary>Nom lisible d'un programme connu (« MsMpEng » → « Microsoft Defender (antivirus) »), sinon son nom tel quel.</summary>
        public static string NomLisible(string nom)
        {
            string n = (nom ?? "").ToLowerInvariant();
            switch (n)
            {
                case "msmpeng": case "mpdefendercoreservice": case "nissrv": return "Microsoft Defender (antivirus)";
                case "tiworker": case "trustedinstaller": case "wuauclt": case "mousocoreworker": case "usoclient": case "wuaucltcore": return "Windows Update";
                case "searchindexer": case "searchprotocolhost": case "searchfilterhost": case "searchhost": return "Indexation de Windows (recherche)";
                case "compattelrunner": case "devicecensus": return "Diagnostic de Windows (télémétrie)";
                case "onedrive": return "OneDrive";
                case "discord": return "Discord";
                case "chrome": return "Google Chrome";
                case "msedge": case "msedgewebview2": return "Microsoft Edge";
                case "firefox": return "Firefox";
                case "opera": return "Opera";
                case "brave": return "Brave";
                case "steam": case "steamwebhelper": case "steamservice": return "Steam";
                case "epicgameslauncher": case "epicwebhelper": return "Epic Games Launcher";
                case "battle.net": return "Battle.net";
                case "riotclientservices": case "riotclientux": return "Riot Client";
                case "eadesktop": case "eabackgroundservice": return "EA app";
                case "ubisoftconnect": case "upc": return "Ubisoft Connect";
                case "obs64": case "obs": return "OBS (enregistrement)";
                case "spotify": return "Spotify";
                case "teams": case "ms-teams": return "Microsoft Teams";
                case "dropbox": return "Dropbox";
                case "googledrivefs": return "Google Drive";
                case "wallpaper32": case "wallpaper64": return "Wallpaper Engine";
                case "explorer": return "Explorateur de fichiers";
                case "svchost": return "Service de Windows";
                case "wmiprvse": return "Service de Windows (WMI)";
                case "dllhost": return "Service de Windows (COM)";
                case "taskmgr": return "Gestionnaire des tâches";
            }
            return nom ?? "";
        }

        /// <summary>Famille d'un programme, pour choisir le bon conseil : « antivirus », « windowsupdate », « indexation »,
        /// « onedrive », « navigateur », « lanceur », « windows » ou « autre ».</summary>
        public static string Famille(string nom)
        {
            string l = NomLisible(nom);
            if (l.StartsWith("Microsoft Defender")) return "antivirus";
            if (l == "Windows Update") return "windowsupdate";
            if (l.StartsWith("Indexation")) return "indexation";
            if (l == "OneDrive" || l == "Dropbox" || l == "Google Drive") return "onedrive";
            if (l == "Google Chrome" || l == "Microsoft Edge" || l == "Firefox" || l == "Opera" || l == "Brave") return "navigateur";
            if (l == "Steam" || l == "Epic Games Launcher" || l == "Battle.net" || l == "Riot Client" || l == "EA app" || l == "Ubisoft Connect") return "lanceur";
            if (l.StartsWith("Service de Windows") || l.StartsWith("Diagnostic de Windows")) return "windows";
            return "autre";
        }
    }

    internal class Saccade
    {
        public DateTime Heure;
        /// <summary>Durée de l'image la plus longue de cette seconde, en millisecondes.</summary>
        public double Ms;
        /// <summary>« chaleur », « programme », « memoire », « disque », « processeur » ou « jeu » (aucune cause extérieure trouvée).</summary>
        public string Cause = "jeu";
        public string Programme = "";
        /// <summary>Valeur qui a désigné la cause (°C, % d'un cœur, % de mémoire, % du disque, % du processeur).</summary>
        public double Valeur;
    }

    internal class SessionBoite
    {
        public string Jeu = "";
        public DateTime Debut, Fin;
        public double FpsMoyen, Fps1;
        public double TempCpuMax = -1, TempGpuMax = -1;
        public long Images;
        public int Note = -1;
        public List<Saccade> Saccades = new List<Saccade>();
        public TimeSpan Duree { get { return Fin > Debut ? Fin - Debut : TimeSpan.Zero; } }
    }

    /// <summary>Saccades regroupées par cause (et par programme), de la plus fréquente à la plus rare.</summary>
    internal class CauseBoite
    {
        public string Cause = "", Programme = "";
        public int Nombre;
        public double PireMs, Valeur;
    }

    /// <summary>État du PC sur une seconde.</summary>
    internal class SecondeBoite
    {
        public DateTime Heure;
        public double Cpu = -1, Ram = -1, TempCpu = -1, TempGpu = -1, Gpu = -1, Disque = -1;
        public string Programme = ""; public double ProgrammeCoeur; public double ProgrammeDisque;
    }

    internal static class BoiteNoire
    {
        private static readonly object verrou = new object();
        private static string Fichier { get { return Path.Combine(Chemins.Local, "boite-noire.json"); } }

        // ---------------------------------------------------------------- session en cours
        private static SessionBoite session;
        private static readonly int[] histogramme = new int[1201];   // images par tranche de 0,25 ms (la dernière : 300 ms et plus)
        private static double sommeMs;
        private static readonly Queue<double> medianes = new Queue<double>();   // médiane de chacune des dernières secondes
        private static int secondesSansImage;

        /// <summary>Une partie est en cours d'enregistrement.</summary>
        public static bool EnCours { get { lock (verrou) return session != null; } }
        /// <summary>Jeu, durée et nombre de saccades de la partie en cours (null si aucune).</summary>
        public static SessionBoite Courante
        {
            get
            {
                lock (verrou)
                {
                    if (session == null) return null;
                    var c = Copie(session);
                    if (sommeMs > 0 && c.Images > 0) { c.FpsMoyen = 1000.0 * c.Images / sommeMs; c.Fps1 = Fps1(histogramme, c.Images); c.Note = Noter(c); }
                    return c;
                }
            }
        }
        /// <summary>Appelé (hors du fil de l'interface) quand une partie vient d'être enregistrée.</summary>
        public static Action<SessionBoite> PartieFinie;

        /// <summary>Seuil de température du processeur à partir duquel une saccade est attribuée à la chaleur (donné par l'interface).</summary>
        public static double SeuilCpu = 92;
        public const double SeuilGpu = 88;

        /// <summary>Une image est une saccade si elle dure au moins 4 fois plus que la normale, et au moins 33 ms.</summary>
        public static bool EstSaccade(double ms, double normale) { return normale > 0 && ms >= 33 && ms >= 4 * normale && ms <= 3000; }

        public static double Mediane(List<double> v)
        {
            if (v == null || v.Count == 0) return 0;
            var t = new List<double>(v); t.Sort();
            return t.Count % 2 == 1 ? t[t.Count / 2] : (t[t.Count / 2 - 1] + t[t.Count / 2]) / 2;
        }

        /// <summary>Cause la plus probable d'une saccade, d'après l'état du PC à cet instant.</summary>
        public static void Diagnostiquer(Saccade s, SecondeBoite e)
        {
            if (e == null) return;
            if (e.TempCpu > 0 && e.TempCpu >= SeuilCpu - 1) { s.Cause = "chaleur"; s.Programme = "processeur"; s.Valeur = e.TempCpu; return; }
            if (e.TempGpu > 0 && e.TempGpu >= SeuilGpu) { s.Cause = "chaleur"; s.Programme = "carte graphique"; s.Valeur = e.TempGpu; return; }
            if (e.Programme != "" && (e.ProgrammeCoeur >= 70 || e.ProgrammeDisque >= 30.0 * 1048576)) { s.Cause = "programme"; s.Programme = e.Programme; s.Valeur = e.ProgrammeCoeur; return; }
            if (e.Ram >= 93) { s.Cause = "memoire"; s.Valeur = e.Ram; return; }
            if (e.Disque >= 85) { s.Cause = "disque"; s.Valeur = e.Disque; return; }
            if (e.Cpu >= 95) { s.Cause = "processeur"; s.Valeur = e.Cpu; return; }
            s.Cause = "jeu";
        }

        /// <summary>Programme le plus gênant de la seconde, en dehors du jeu : celui qui prend le plus de processeur,
        /// ou celui qui écrit le plus sur le disque s'il dépasse 30 Mo/s.</summary>
        public static ProgrammeActif PlusGenant(List<ProgrammeActif> l, int pidJeu, string nomJeu)
        {
            if (l == null) return null;
            string jeu = Path.GetFileNameWithoutExtension(nomJeu ?? "");
            ProgrammeActif cpu = null, disque = null;
            foreach (var p in l)
            {
                if (ActiviteProgrammes.Ignore(p.Nom)) continue;
                if (pidJeu > 0 && p.Pid == pidJeu) continue;
                if (jeu != "" && p.Nom.Equals(jeu, StringComparison.OrdinalIgnoreCase)) continue;
                if (cpu == null || p.Coeur > cpu.Coeur) cpu = p;
                if (disque == null || p.Disque > disque.Disque) disque = p;
            }
            if (disque != null && disque.Disque >= 30.0 * 1048576 && (cpu == null || cpu.Coeur < 70)) return disque;
            return cpu;
        }

        /// <summary>À appeler chaque seconde pendant les FPS en direct (hors du fil de l'interface).
        /// images : durées (ms) des images du jeu affichées depuis l'appel précédent (vide ou null : aucune).
        /// Renvoie vrai si une saccade a été repérée pendant cette seconde.</summary>
        public static bool Seconde(SecondeBoite etat, List<double> images, string jeu, int pidJeu)
        {
            if (etat == null) return false;
            SessionBoite finie = null; bool saccade = false;
            lock (verrou)
            {
                bool aDesImages = images != null && images.Count >= 2 && !string.IsNullOrEmpty(jeu);
                // changement de jeu : la partie précédente se termine
                if (session != null && aDesImages && !session.Jeu.Equals(jeu, StringComparison.OrdinalIgnoreCase)) finie = Fermer();
                if (!aDesImages)
                {
                    // 2 minutes sans image du jeu (jeu fermé, ou autre fenêtre devant) : la partie est finie
                    if (session != null && ++secondesSansImage >= 120) finie = Fermer();
                }
                else
                {
                    secondesSansImage = 0;
                    if (session == null)
                    {
                        session = new SessionBoite { Jeu = jeu, Debut = etat.Heure, Fin = etat.Heure };
                        Array.Clear(histogramme, 0, histogramme.Length); sommeMs = 0; medianes.Clear();
                    }
                    session.Fin = etat.Heure;
                    if (etat.TempCpu > 0) session.TempCpuMax = Math.Max(session.TempCpuMax, etat.TempCpu);
                    if (etat.TempGpu > 0) session.TempGpuMax = Math.Max(session.TempGpuMax, etat.TempGpu);
                    double normale = medianes.Count >= 3 ? Mediane(medianes.ToList()) : 0;
                    double pire = 0;
                    foreach (double ms in images)
                    {
                        if (ms <= 0 || ms > 3000) continue;   // au-delà de 3 secondes : écran de chargement ou jeu en pause, pas une saccade
                        session.Images++; sommeMs += ms;
                        histogramme[Math.Min(histogramme.Length - 1, (int)(ms * 4))]++;
                        if (ms > pire) pire = ms;
                    }
                    if (EstSaccade(pire, normale) && session.Saccades.Count < 2000)
                    {
                        var s = new Saccade { Heure = etat.Heure, Ms = Math.Round(pire) };
                        Diagnostiquer(s, etat);
                        session.Saccades.Add(s);
                        saccade = true;
                    }
                    medianes.Enqueue(Mediane(images));
                    while (medianes.Count > 8) medianes.Dequeue();
                }
            }
            Annoncer(finie);
            return saccade;
        }

        /// <summary>Termine la partie en cours (FPS en direct arrêtés, Velox fermé).</summary>
        public static void Terminer()
        {
            SessionBoite finie;
            lock (verrou) finie = Fermer();
            Annoncer(finie);
        }

        private static void Annoncer(SessionBoite s)
        {
            if (s == null) return;
            try { Enregistrer(s); } catch { }
            var a = PartieFinie;
            if (a != null) { try { a(s); } catch { } }
        }

        // sous verrou : calcule le bilan de la partie ; null si elle est trop courte pour être gardée (moins d'une minute)
        private static SessionBoite Fermer()
        {
            var s = session;
            session = null; secondesSansImage = 0; medianes.Clear();
            if (s == null) return null;
            if (s.Images < 600 || s.Duree.TotalSeconds < 60 || sommeMs <= 0) return null;
            s.FpsMoyen = 1000.0 * s.Images / sommeMs;
            s.Fps1 = Fps1(histogramme, s.Images);
            s.Note = Noter(s);
            return s;
        }

        /// <summary>FPS minimum « 1 % low » : la cadence des 1 % d'images les plus lentes (durée du 99e centile).</summary>
        public static double Fps1(int[] histo, long images)
        {
            if (images <= 0) return 0;
            long seuil = (long)Math.Ceiling(images * 0.99), cumul = 0;
            for (int i = 0; i < histo.Length; i++)
            {
                cumul += histo[i];
                if (cumul >= seuil) return 1000.0 / ((i + 1) / 4.0);
            }
            return 0;
        }

        /// <summary>Note de fluidité sur 100 : saccades par tranche de 10 minutes, écart entre FPS minimum et moyen, chaleur.</summary>
        public static int Noter(SessionBoite s)
        {
            double minutes = Math.Max(1, s.Duree.TotalMinutes);
            double par10 = s.Saccades.Count * 10.0 / minutes;
            double note = 100 - Math.Min(50, par10 * 5);
            double rapport = s.FpsMoyen > 0 ? s.Fps1 / s.FpsMoyen : 1;
            if (rapport < 0.5) note -= 20; else if (rapport < 0.65) note -= 10;
            if (s.Saccades.Any(x => x.Cause == "chaleur")) note -= 10;
            return (int)Math.Max(0, Math.Min(100, Math.Round(note)));
        }

        /// <summary>Saccades regroupées par cause, de la plus fréquente à la plus rare.</summary>
        public static List<CauseBoite> Causes(SessionBoite s)
        {
            var l = new List<CauseBoite>();
            if (s == null) return l;
            foreach (var g in s.Saccades.GroupBy(x => x.Cause + "|" + (x.Cause == "programme" || x.Cause == "chaleur" ? x.Programme : "")))
            {
                var premier = g.First();
                l.Add(new CauseBoite { Cause = premier.Cause, Programme = premier.Cause == "programme" || premier.Cause == "chaleur" ? premier.Programme : "", Nombre = g.Count(), PireMs = g.Max(x => x.Ms), Valeur = g.Max(x => x.Valeur) });
            }
            return l.OrderByDescending(x => x.Nombre).ThenByDescending(x => x.PireMs).ToList();
        }

        /// <summary>Titre d'une cause, en clair (non traduit ; {0} = nombre de saccades, {1} = programme ou composant).</summary>
        public static string Titre(CauseBoite c)
        {
            switch (c.Cause)
            {
                case "chaleur": return c.Programme == "carte graphique" ? "{0} saccade(s) quand la carte graphique était trop chaude" : "{0} saccade(s) quand le processeur était trop chaud";
                case "programme": return "{0} saccade(s) pendant que {1} travaillait";
                case "memoire": return "{0} saccade(s) avec la mémoire vive presque pleine";
                case "disque": return "{0} saccade(s) pendant que le disque était très occupé";
                case "processeur": return "{0} saccade(s) avec le processeur à fond";
            }
            return "{0} saccade(s) sans cause extérieure trouvée";
        }

        /// <summary>Explication et conseil pour une cause (non traduits).</summary>
        public static string Conseil(CauseBoite c)
        {
            switch (c.Cause)
            {
                case "chaleur":
                    return c.Programme == "carte graphique"
                        ? "Au-delà de sa limite, la carte graphique ralentit pour se protéger. Dépoussière ses ventilateurs, vérifie que l'air circule dans le boîtier, ou limite les images par seconde dans le jeu."
                        : "Au-delà de sa limite, le processeur ralentit pour se protéger. Dépoussière le ventirad, vérifie que son ventilateur tourne, et pense à changer la pâte thermique si le PC a plusieurs années.";
                case "programme":
                    switch (ActiviteProgrammes.Famille(c.Programme))
                    {
                        case "antivirus": return "L'antivirus analysait des fichiers pendant la partie. Ne le désactive pas : programme plutôt son analyse à une heure où tu ne joues pas (Sécurité Windows > Protection contre les virus > Options d'analyse).";
                        case "windowsupdate": return "Windows installait une mise à jour pendant la partie. Règle tes « heures d'activité » pour qu'il attende que tu aies fini de jouer.";
                        case "indexation": return "Windows mettait à jour son index de recherche. C'est passager, surtout après une grosse installation ; ça se calme tout seul.";
                        case "onedrive": return "La synchronisation des fichiers travaillait pendant la partie. Le mode jeu de Velox peut mettre OneDrive en pause pour chaque jeu.";
                        case "navigateur": return "Le navigateur est resté ouvert pendant la partie. Ferme-le, ou au moins les onglets avec des vidéos, avant de jouer.";
                        case "lanceur": return "Le lanceur de jeux travaillait en arrière-plan, sans doute un téléchargement ou une mise à jour d'un autre jeu. Mets ses téléchargements en pause pendant que tu joues.";
                        case "windows": return "Un service de Windows travaillait en arrière-plan. Si ça revient à chaque partie, redémarre le PC avant de jouer et vérifie que Windows a fini ses mises à jour.";
                    }
                    return "Ce programme a pris du processeur ou du disque au moment des saccades. Ferme-le avant de jouer si tu n'en as pas besoin.";
                case "memoire": return "Quand la mémoire vive est pleine, Windows écrit sur le disque et le jeu se fige un instant. Ferme les programmes inutiles avant de jouer, ou libère la mémoire juste avant la partie.";
                case "disque": return "Le jeu attendait le disque, souvent en chargeant une nouvelle zone. Sur un disque dur classique c'est fréquent : installe le jeu sur un SSD si tu en as un.";
                case "processeur": return "Le processeur n'arrivait plus à suivre. Baisse dans le jeu les réglages qui pèsent sur lui (distance d'affichage, foule, physique), ou ferme ce qui tourne à côté.";
            }
            return "Velox n'a rien vu d'anormal sur le PC à ces instants : c'est probablement le jeu lui-même (chargement d'une zone, préparation des effets graphiques). C'est fréquent les premières minutes ou après une mise à jour du jeu.";
        }

        /// <summary>Où corriger : « action:cle », « outil:cle », « uri:... » ou vide, et le texte du bouton.</summary>
        public static string Correction(CauseBoite c, out string bouton)
        {
            bouton = "";
            switch (c.Cause)
            {
                case "programme":
                    switch (ActiviteProgrammes.Famille(c.Programme))
                    {
                        case "antivirus": bouton = "Sécurité Windows"; return "uri:windowsdefender://threat";
                        case "windowsupdate": bouton = "Heures d'activité"; return "uri:ms-settings:windowsupdate-activehours";
                        case "onedrive": bouton = "Réglages jeu par jeu"; return "outil:reglagesjeux";
                    }
                    return "";
                case "memoire": bouton = "Libérer la mémoire"; return "action:ramlib";
                case "disque": bouton = "Voir mes jeux"; return "outil:mesjeux";
            }
            return "";
        }

        /// <summary>Phrase de bilan d'une partie. niveau : 0 fluide, 1 quelques saccades, 2 beaucoup.</summary>
        public static string Verdict(SessionBoite s, out int niveau)
        {
            niveau = 0;
            if (s == null) return "";
            double par10 = s.Saccades.Count * 10.0 / Math.Max(1, s.Duree.TotalMinutes);
            if (s.Saccades.Count == 0) return "Partie parfaitement fluide : aucune saccade.";
            if (par10 < 2) { niveau = 0; return "Partie fluide : très peu de saccades."; }
            if (par10 < 8) { niveau = 1; return "Quelques saccades pendant la partie."; }
            niveau = 2; return "Beaucoup de saccades pendant la partie.";
        }

        // ---------------------------------------------------------------- parties gardées
        private static void Enregistrer(SessionBoite s)
        {
            lock (verrou)
            {
                var l = Parties();
                l.Insert(0, s);
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(Fichier, Json.Ecrire(l.Take(20).Select(x => (object)VersDico(x)).ToList()));
            }
        }

        private static Dictionary<string, object> VersDico(SessionBoite x)
        {
            return new Dictionary<string, object>
            {
                { "Jeu", x.Jeu }, { "Debut", x.Debut.ToString("o") }, { "Fin", x.Fin.ToString("o") },
                { "FpsMoyen", Nb(x.FpsMoyen) }, { "Fps1", Nb(x.Fps1) }, { "TempCpuMax", Nb(x.TempCpuMax) }, { "TempGpuMax", Nb(x.TempGpuMax) },
                { "Images", x.Images.ToString(CultureInfo.InvariantCulture) }, { "Note", x.Note.ToString(CultureInfo.InvariantCulture) },
                { "Saccades", x.Saccades.Take(300).Select(k => (object)new Dictionary<string, object>
                    { { "Heure", k.Heure.ToString("o") }, { "Ms", Nb(k.Ms) }, { "Cause", k.Cause }, { "Programme", k.Programme }, { "Valeur", Nb(k.Valeur) } }).ToList() },
            };
        }

        private static string Nb(double v) { return Math.Round(v, 1).ToString(CultureInfo.InvariantCulture); }
        private static double Lu(Dictionary<string, object> d, string cle)
        {
            double v;
            return double.TryParse(Json.Texte(d, cle), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0;
        }
        private static DateTime Date(Dictionary<string, object> d, string cle)
        {
            DateTime t;
            return DateTime.TryParse(Json.Texte(d, cle), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t) ? t : DateTime.MinValue;
        }

        private static readonly HashSet<string> CausesConnues = new HashSet<string> { "chaleur", "programme", "memoire", "disque", "processeur", "jeu" };

        /// <summary>Parties enregistrées, la plus récente d'abord (20 au plus).</summary>
        public static List<SessionBoite> Parties()
        {
            var l = new List<SessionBoite>();
            try
            {
                foreach (var d in Json.Objets(Json.LireFichier(Fichier)))
                {
                    var s = new SessionBoite { Jeu = Propre(Json.Texte(d, "Jeu")), Debut = Date(d, "Debut"), Fin = Date(d, "Fin"), FpsMoyen = Lu(d, "FpsMoyen"), Fps1 = Lu(d, "Fps1"), TempCpuMax = Lu(d, "TempCpuMax"), TempGpuMax = Lu(d, "TempGpuMax"), Images = (long)Lu(d, "Images"), Note = (int)Lu(d, "Note") };
                    if (s.Debut == DateTime.MinValue || s.Jeu == "") continue;
                    object sac;
                    if (d.TryGetValue("Saccades", out sac) && sac != null && !(sac is string))
                        foreach (var kd in Json.Objets(sac))
                        {
                            string cause = Json.Texte(kd, "Cause");
                            s.Saccades.Add(new Saccade { Heure = Date(kd, "Heure"), Ms = Lu(kd, "Ms"), Cause = CausesConnues.Contains(cause) ? cause : "jeu", Programme = Propre(Json.Texte(kd, "Programme")), Valeur = Lu(kd, "Valeur") });
                        }
                    l.Add(s);
                }
            }
            catch { }
            return l;
        }

        // un nom de programme lu dans un fichier : court, sans caractère de contrôle
        private static string Propre(string t)
        {
            t = new string((t ?? "").Where(c => !char.IsControl(c)).ToArray()).Trim();
            return t.Length > 80 ? t.Substring(0, 80) : t;
        }

        private static SessionBoite Copie(SessionBoite s)
        {
            var c = new SessionBoite { Jeu = s.Jeu, Debut = s.Debut, Fin = s.Fin, FpsMoyen = s.FpsMoyen, Fps1 = s.Fps1, TempCpuMax = s.TempCpuMax, TempGpuMax = s.TempGpuMax, Images = s.Images, Note = s.Note };
            c.Saccades.AddRange(s.Saccades);
            return c;
        }

        /// <summary>Nom du jeu sans « .exe ».</summary>
        public static string NomJeu(string jeu)
        {
            string n = jeu ?? "";
            return n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - 4) : n;
        }

        /// <summary>Partie d'exemple, réaliste (captures et aperçu sans Premium).</summary>
        public static SessionBoite Exemple()
        {
            var debut = DateTime.Today.AddHours(21).AddMinutes(12);
            var s = new SessionBoite { Jeu = "Cyberpunk2077.exe", Debut = debut, Fin = debut.AddMinutes(74), FpsMoyen = 118, Fps1 = 71, TempCpuMax = 81, TempGpuMax = 74, Images = 523000 };
            Action<int, double, string, string, double> a = (minute, ms, cause, prog, v) => s.Saccades.Add(new Saccade { Heure = debut.AddMinutes(minute).AddSeconds(minute * 7 % 60), Ms = ms, Cause = cause, Programme = prog, Valeur = v });
            a(3, 84, "jeu", "", 0); a(9, 212, "programme", "Discord", 96); a(9, 148, "programme", "Discord", 91); a(10, 97, "programme", "Discord", 88);
            a(27, 131, "programme", "MsMpEng", 140); a(28, 118, "programme", "MsMpEng", 133); a(41, 66, "jeu", "", 0); a(52, 92, "disque", "", 97); a(63, 71, "jeu", "", 0);
            s.Note = Noter(s);
            return s;
        }
    }

    // ==================================================================== « Qui ralentit mon PC maintenant ? »
    internal class ResultatCoupable
    {
        public double Cpu = -1, Ram = -1, Disque = -1, TempCpu = -1;
        /// <summary>Programmes les plus actifs, moyennés sur l'observation (Coeur en % d'un cœur, Disque en octets/s).</summary>
        public List<ProgrammeActif> Programmes = new List<ProgrammeActif>();
        public int Coeurs = 1;
    }

    /// <summary>Observe le PC quelques secondes et désigne ce qui le ralentit en ce moment. Lecture seule, sans droits administrateur.</summary>
    internal static class Coupable
    {
        /// <summary>Observe pendant « secondes » (une mesure par seconde). progres : 0 à 1. etat : mesures instantanées du PC.</summary>
        public static ResultatCoupable Observer(int secondes, Func<SecondeBoite> etat, Action<double> progres, Func<bool> arreter)
        {
            var r = new ResultatCoupable { Coeurs = Math.Max(1, Environment.ProcessorCount) };
            var cumul = new Dictionary<string, double[]>(StringComparer.OrdinalIgnoreCase);
            double cpu = 0, ram = 0, disque = 0; int nCpu = 0, nRam = 0, nDisque = 0, releves = 0;
            ActiviteProgrammes.Lire(); ActiviteProgrammes.Disque();   // premier relevé : sert de point de départ
            for (int i = 0; i < secondes; i++)
            {
                System.Threading.Thread.Sleep(1000);
                if (arreter != null && arreter()) break;
                var l = ActiviteProgrammes.Lire();
                releves++;
                foreach (var g in l.Where(p => !ActiviteProgrammes.Ignore(p.Nom)).GroupBy(p => p.Nom, StringComparer.OrdinalIgnoreCase))
                {
                    double[] v;
                    if (!cumul.TryGetValue(g.Key, out v)) cumul[g.Key] = v = new double[2];
                    v[0] += g.Sum(p => p.Coeur); v[1] += g.Sum(p => p.Disque);
                }
                double d = ActiviteProgrammes.Disque();
                if (d >= 0) { disque += d; nDisque++; }
                SecondeBoite e = null;
                try { if (etat != null) e = etat(); } catch { }
                if (e != null)
                {
                    if (e.Cpu >= 0) { cpu += e.Cpu; nCpu++; }
                    if (e.Ram >= 0) { ram += e.Ram; nRam++; }
                    if (e.TempCpu > 0) r.TempCpu = Math.Max(r.TempCpu, e.TempCpu);
                }
                if (progres != null) progres((i + 1.0) / secondes);
            }
            if (nCpu > 0) r.Cpu = cpu / nCpu;
            if (nRam > 0) r.Ram = ram / nRam;
            if (nDisque > 0) r.Disque = disque / nDisque;
            if (releves > 0)
                r.Programmes = cumul.Select(kv => new ProgrammeActif { Nom = kv.Key, Coeur = kv.Value[0] / releves, Disque = kv.Value[1] / releves })
                    .Where(p => p.Coeur >= 2 || p.Disque >= 2.0 * 1048576).OrderByDescending(p => p.Coeur + p.Disque / 1048576.0).Take(6).ToList();
            return r;
        }

        /// <summary>Verdict en clair (non traduit ; {0} = programme). niveau : 0 rien d'anormal, 1 à surveiller, 2 coupable trouvé.</summary>
        public static string Verdict(ResultatCoupable r, out int niveau, out string programme)
        {
            niveau = 0; programme = "";
            if (r == null) return "";
            var premier = r.Programmes.FirstOrDefault();
            // un programme qui prend plus du quart du processeur entier, ou plus d'un cœur et demi
            if (premier != null && (premier.Coeur >= 150 || premier.Coeur / r.Coeurs >= 25)) { niveau = 2; programme = premier.Nom; return "{0} occupe beaucoup le processeur en ce moment."; }
            var ecrivain = r.Programmes.OrderByDescending(p => p.Disque).FirstOrDefault();
            if (r.Disque >= 80 && ecrivain != null && ecrivain.Disque >= 20.0 * 1048576) { niveau = 2; programme = ecrivain.Nom; return "{0} fait travailler le disque sans arrêt."; }
            if (r.Ram >= 90) { niveau = 2; return "La mémoire vive est presque pleine : Windows ralentit tout."; }
            if (r.Disque >= 80) { niveau = 1; return "Le disque est très occupé en ce moment."; }
            if (r.Cpu >= 80) { niveau = 1; return "Le processeur est très sollicité, par plusieurs programmes à la fois."; }
            if (r.TempCpu >= 90) { niveau = 1; return "Le processeur est très chaud : il ralentit pour se protéger."; }
            if (premier != null && premier.Coeur >= 60) { niveau = 1; programme = premier.Nom; return "{0} est le programme le plus actif, sans que ce soit anormal."; }
            return "Rien ne ralentit ton PC en ce moment.";
        }
    }
}
