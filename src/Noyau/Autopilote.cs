// Velox 5.13 - Autopilote du mode jeu (Premium, avec les FPS en direct, désactivé par défaut).
// Au lieu d'appliquer des réglages « qui donnent des FPS » sans preuve, Velox les essaie sur TES parties, un à la fois :
// des blocs de 3 minutes avec le réglage, d'autres sans (dans l'ordre sans, avec, avec, sans… pour que l'échauffement du
// jeu ou du PC ne favorise aucun côté). La Boîte noire mesure le FPS minimum et les saccades de chaque bloc. Quand l'écart
// est net et régulier, le réglage est gardé pour ce jeu ; s'il ne change rien ou s'il est pire, il est annulé.
// Réglages essayés : tous réversibles, rien n'est jamais fermé ; l'antivirus, le pare-feu et Windows Update ne sont pas touchés.
//   - retrait     : le programme qui fait le plus saccader ce jeu (d'après la Boîte noire) passe en priorité « inférieure » ;
//   - priorite    : le jeu passe en priorité « haute » (jamais si un anti-triche tourne) ;
//   - alimentation: mode « Performances élevées » pendant le jeu (seulement si le PC ne l'utilise pas déjà, hors mode jeu).
// Tout est rétabli à la fin de chaque bloc, de la partie, ou au démarrage suivant de Velox s'il a été fermé brutalement.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    /// <summary>Résultat d'un bloc de mesure (3 minutes de jeu avec, ou sans, le réglage essayé).</summary>
    internal class BlocAutopilote
    {
        public bool Avec;
        public double Fps1, Fps, SaccadesMin, Secondes;
    }

    /// <summary>Un réglage essayé sur un jeu.</summary>
    internal class EssaiAutopilote
    {
        public string Reglage = "";
        /// <summary>« attente » (pas encore essayé), « test », « garde », « annule » ou « impossible ».</summary>
        public string Etat = "attente";
        /// <summary>Pour « annule » : « aucun » (aucun effet mesurable) ou « pire » ; pour « impossible » : la raison.</summary>
        public string Raison = "";
        public List<BlocAutopilote> Blocs = new List<BlocAutopilote>();
        /// <summary>Écarts mesurés, avec par rapport à sans (en fraction : 0,07 = +7 %). Gain1 : FPS minimum ; GainSaccades : saccades en moins.</summary>
        public double Gain1, GainSaccades;
        public DateTime Date;
        public int NbAvec { get { return Blocs.Count(b => b.Avec); } }
        public int NbSans { get { return Blocs.Count(b => !b.Avec); } }
    }

    internal class JeuAutopilote
    {
        public string Jeu = "";
        /// <summary>Programme d'arrière-plan visé par le réglage « retrait » (nom de processus), ou vide.</summary>
        public string Cible = "";
        public List<EssaiAutopilote> Essais = new List<EssaiAutopilote>();
    }

    internal static class Autopilote
    {
        public static readonly string[] Reglages = { "retrait", "priorite", "alimentation" };
        /// <summary>Durée d'un bloc, en secondes de jeu (modifiable pour les essais automatiques).</summary>
        public static int DureeBloc = 180;
        /// <summary>Secondes ignorées au début de chaque partie (chargement, compilation des effets).</summary>
        public static int Echauffement = 60;
        public const int BlocsMin = 6, BlocsMax = 12;

        private static readonly object verrou = new object();
        private static string Fichier { get { return Path.Combine(Chemins.Local, "autopilote.json"); } }
        private static string FichierRetablir { get { return Path.Combine(Chemins.Local, "autopilote-retablir.json"); } }

        /// <summary>Appelé (hors du fil de l'interface) quand un réglage reçoit son verdict : jeu, essai.</summary>
        public static Action<string, EssaiAutopilote> Verdict;

        /// <summary>Applique (avec = vrai) ou retire un réglage. Remplaçable pour les essais automatiques.
        /// Renvoie faux si Windows (ou l'anti-triche du jeu) refuse.</summary>
        public static Func<string, bool, int, string, bool> Appliquer = AppliquerVraiment;

        // ---------------------------------------------------------------- partie en cours
        private static string jeu = ""; private static int pidJeu;
        private static int secondesJeu, sansImage, numeroBloc;
        private static JeuAutopilote donnees;
        private static EssaiAutopilote essai;              // réglage en cours d'essai (null : rien à essayer)
        private static bool blocAvec, actif;
        private static readonly int[] histo = new int[1201];
        private static long images; private static double sommeMs, secondesBloc; private static int saccadesBloc;
        private static readonly List<string> gardesAppliques = new List<string>();

        public static bool EnCours { get { lock (verrou) return actif; } }
        /// <summary>Réglage en cours d'essai et côté du bloc (pour l'affichage), ou vide.</summary>
        public static string Etat(out bool avec) { lock (verrou) { avec = blocAvec; return actif && essai != null ? essai.Reglage : ""; } }

        /// <summary>À appeler chaque seconde pendant les FPS en direct, une fois la Boîte noire servie.
        /// images : durées des images du jeu au premier plan (null : aucune) ; saccade : la Boîte noire en a vu une.</summary>
        public static void Seconde(string application, int pid, List<double> imagesSeconde, bool saccade)
        {
            lock (verrou)
            {
                bool aDesImages = imagesSeconde != null && imagesSeconde.Count >= 2 && !string.IsNullOrEmpty(application) && pid > 0;
                if (actif && aDesImages && (!application.Equals(jeu, StringComparison.OrdinalIgnoreCase) || pid != pidJeu)) Fin();
                if (!aDesImages)
                {
                    if (actif && ++sansImage >= 120) Fin();
                    return;
                }
                sansImage = 0;
                if (!actif) Debut(application, pid);
                if (++secondesJeu <= Echauffement) return;
                if (essai == null) return;
                foreach (double ms in imagesSeconde)
                {
                    if (ms <= 0 || ms > 3000) continue;
                    images++; sommeMs += ms; histo[Math.Min(histo.Length - 1, (int)(ms * 4))]++;
                }
                secondesBloc++;
                if (saccade) saccadesBloc++;
                if (secondesBloc >= DureeBloc) FinBloc(true, true);
            }
        }

        /// <summary>La partie est finie (jeu quitté, FPS en direct arrêtés, Velox fermé) : tout est rétabli.</summary>
        public static void Terminer() { lock (verrou) Fin(); }

        // sous verrou
        private static void Debut(string application, int pid)
        {
            actif = true; jeu = application; pidJeu = pid; secondesJeu = 0; sansImage = 0; numeroBloc = 0;
            donnees = Charger().FirstOrDefault(j => j.Jeu.Equals(application, StringComparison.OrdinalIgnoreCase)) ?? new JeuAutopilote { Jeu = application };
            Preparer(donnees);
            // les réglages déjà gagnants restent en place toute la partie
            gardesAppliques.Clear();
            foreach (var e in donnees.Essais.Where(x => x.Etat == "garde"))
                if (Appliquer(e.Reglage, true, pid, donnees.Cible)) gardesAppliques.Add(e.Reglage);
            essai = donnees.Essais.FirstOrDefault(x => x.Etat == "test") ?? donnees.Essais.FirstOrDefault(x => x.Etat == "attente");
            if (essai != null) { essai.Etat = "test"; if (essai.Date == DateTime.MinValue) essai.Date = DateTime.Now; }
            NouveauBloc();
            Enregistrer(donnees);
        }

        /// <summary>Complète la liste des réglages à essayer pour ce jeu (et choisit le programme visé par « retrait »).</summary>
        public static void Preparer(JeuAutopilote j)
        {
            if (string.IsNullOrEmpty(j.Cible)) j.Cible = CibleRetrait(j.Jeu);
            foreach (string r in Reglages)
            {
                if (j.Essais.Any(e => e.Reglage == r)) continue;
                if (r == "retrait" && string.IsNullOrEmpty(j.Cible)) continue;   // pas de programme gênant repéré : rien à essayer
                j.Essais.Add(new EssaiAutopilote { Reglage = r });
            }
        }

        private static void NouveauBloc()
        {
            Array.Clear(histo, 0, histo.Length); images = 0; sommeMs = 0; secondesBloc = 0; saccadesBloc = 0;
            if (essai == null) { blocAvec = false; return; }
            // ordre sans, avec, avec, sans, sans, avec… : l'échauffement ne favorise aucun côté
            blocAvec = numeroBloc % 4 == 1 || numeroBloc % 4 == 2;
            if (blocAvec && !Appliquer(essai.Reglage, true, pidJeu, donnees.Cible))
            {
                essai.Etat = "impossible"; essai.Raison = "refus";
                Enregistrer(donnees); Annoncer(essai);
                essai = null; blocAvec = false;
            }
        }

        // sous verrou : termine le bloc en cours. complet : il a duré un bloc entier (sinon il compte s'il a duré les 2/3) ;
        // continuer : un nouveau bloc commence (faux à la fin de la partie : rien n'est appliqué au jeu qui se ferme)
        private static void FinBloc(bool complet, bool continuer)
        {
            if (essai == null) return;
            if (blocAvec) Appliquer(essai.Reglage, false, pidJeu, donnees.Cible);
            if (complet || secondesBloc >= DureeBloc * 2 / 3)
            {
                if (images >= 600 && sommeMs > 0)
                {
                    essai.Blocs.Add(new BlocAutopilote { Avec = blocAvec, Fps = 1000.0 * images / sommeMs, Fps1 = BoiteNoire.Fps1(histo, images), SaccadesMin = saccadesBloc * 60.0 / Math.Max(1, secondesBloc), Secondes = secondesBloc });
                    Juger(essai);
                    Enregistrer(donnees);
                    if (essai.Etat != "test")
                    {
                        Annoncer(essai);
                        if (continuer && essai.Etat == "garde" && Appliquer(essai.Reglage, true, pidJeu, donnees.Cible)) gardesAppliques.Add(essai.Reglage);
                        essai = donnees.Essais.FirstOrDefault(x => x.Etat == "attente");
                        if (essai != null) { essai.Etat = "test"; essai.Date = DateTime.Now; Enregistrer(donnees); }
                        numeroBloc = 0;
                        if (continuer) NouveauBloc(); else blocAvec = false;
                        return;
                    }
                }
            }
            numeroBloc++;
            if (continuer) NouveauBloc(); else blocAvec = false;
        }

        private static void Fin()
        {
            if (!actif) return;
            try { FinBloc(false, false); } catch { }
            foreach (string r in gardesAppliques) { try { Appliquer(r, false, pidJeu, donnees != null ? donnees.Cible : ""); } catch { } }
            gardesAppliques.Clear();
            if (donnees != null) Enregistrer(donnees);
            actif = false; essai = null; blocAvec = false; jeu = ""; pidJeu = 0; donnees = null;
        }

        private static void Annoncer(EssaiAutopilote e)
        {
            var a = Verdict; string j = donnees != null ? donnees.Jeu : jeu;
            if (a != null) { try { a(j, e); } catch { } }
        }

        // ---------------------------------------------------------------- verdict
        /// <summary>Compare les blocs avec et sans. Un réglage n'est gardé que si l'écart est net (3 % de FPS minimum en plus,
        /// ou 30 % de saccades en moins) ET régulier d'un bloc à l'autre (test de Mann-Whitney, moins de 10 % de risque de hasard).</summary>
        public static void Juger(EssaiAutopilote e)
        {
            var avec = e.Blocs.Where(b => b.Avec).ToList(); var sans = e.Blocs.Where(b => !b.Avec).ToList();
            if (avec.Count < BlocsMin || sans.Count < BlocsMin) return;
            double m1a = BoiteNoire.Mediane(avec.Select(b => b.Fps1).ToList()), m1s = BoiteNoire.Mediane(sans.Select(b => b.Fps1).ToList());
            double sa = avec.Average(b => b.SaccadesMin), ss = sans.Average(b => b.SaccadesMin);
            e.Gain1 = m1s > 0 ? (m1a - m1s) / m1s : 0;
            e.GainSaccades = ss >= 0.05 ? (ss - sa) / ss : 0;
            double pMieux1 = MannWhitney(avec.Select(b => b.Fps1).ToList(), sans.Select(b => b.Fps1).ToList());
            double pPire1 = MannWhitney(sans.Select(b => b.Fps1).ToList(), avec.Select(b => b.Fps1).ToList());
            double pMoinsSac = MannWhitney(sans.Select(b => b.SaccadesMin).ToList(), avec.Select(b => b.SaccadesMin).ToList());
            double pPlusSac = MannWhitney(avec.Select(b => b.SaccadesMin).ToList(), sans.Select(b => b.SaccadesMin).ToList());
            bool mieux = (e.Gain1 >= 0.03 && pMieux1 < 0.1 && e.GainSaccades > -0.25) || (e.GainSaccades >= 0.3 && pMoinsSac < 0.1 && e.Gain1 > -0.02);
            bool pire = (e.Gain1 <= -0.03 && pPire1 < 0.1) || (e.GainSaccades <= -0.3 && pPlusSac < 0.1);
            if (mieux && !pire) { e.Etat = "garde"; e.Raison = ""; }
            else if (pire) { e.Etat = "annule"; e.Raison = "pire"; }
            else if (avec.Count >= BlocsMax && sans.Count >= BlocsMax) { e.Etat = "annule"; e.Raison = "aucun"; }
        }

        /// <summary>Test de Mann-Whitney (approximation normale, ex aequo comptés à moitié) : probabilité que « a » paraisse
        /// plus grand que « b » par simple hasard. Petite valeur = « a » est vraiment plus grand.</summary>
        public static double MannWhitney(List<double> a, List<double> b)
        {
            if (a.Count == 0 || b.Count == 0) return 1;
            double u = 0;
            foreach (double x in a) foreach (double y in b) u += x > y ? 1 : x == y ? 0.5 : 0;
            double n1 = a.Count, n2 = b.Count, moyenne = n1 * n2 / 2, ecart = Math.Sqrt(n1 * n2 * (n1 + n2 + 1) / 12);
            if (ecart <= 0) return 1;
            double z = (u - moyenne - 0.5) / ecart;
            return 1 - Normale(z);
        }

        private static double Normale(double z)
        {
            // fonction de répartition de la loi normale (Abramowitz et Stegun 7.1.26, erreur < 1,5e-7)
            double x = Math.Abs(z) / Math.Sqrt(2), t = 1 / (1 + 0.3275911 * x);
            double erf = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
            return z >= 0 ? 0.5 * (1 + erf) : 0.5 * (1 - erf);
        }

        // ---------------------------------------------------------------- réglages réels
        /// <summary>Programmes d'anti-triche : tant que l'un tourne, Velox ne touche pas à la priorité du jeu.</summary>
        private static readonly string[] AntiTriche = { "vgc", "vgtray", "EasyAntiCheat", "EasyAntiCheat_EOS", "BEService", "BEService_x64", "FACEIT", "FACEITService", "faceit_ac", "EAAntiCheat.GameService", "Randgrid", "ricochet", "PnkBstrA", "PnkBstrB", "nProtect", "GameMon", "xigncode", "mhyprot2" };

        public static bool AntiTricheActif()
        {
            foreach (string n in AntiTriche)
            {
                try { var l = Process.GetProcessesByName(n); bool oui = l.Length > 0; foreach (var p in l) p.Dispose(); if (oui) return true; } catch { }
            }
            return false;
        }

        /// <summary>Programme qui a le plus fait saccader ce jeu d'après la Boîte noire (au moins 3 saccades), en écartant
        /// l'antivirus, Windows Update et les services de Windows, auxquels Velox ne touche jamais.</summary>
        public static string CibleRetrait(string application)
        {
            try
            {
                var compte = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in BoiteNoire.Parties().Where(x => x.Jeu.Equals(application, StringComparison.OrdinalIgnoreCase)))
                    foreach (var s in p.Saccades.Where(k => k.Cause == "programme" && k.Programme != ""))
                    {
                        string f = ActiviteProgrammes.Famille(s.Programme);
                        if (f == "antivirus" || f == "windowsupdate" || f == "windows" || f == "indexation" || ActiviteProgrammes.Ignore(s.Programme)) continue;
                        int n; compte.TryGetValue(s.Programme, out n); compte[s.Programme] = n + 1;
                    }
                var meilleur = compte.OrderByDescending(kv => kv.Value).FirstOrDefault();
                return meilleur.Value >= 3 ? meilleur.Key : "";
            }
            catch { return ""; }
        }

        private static readonly Dictionary<int, ProcessPriorityClass> priorites = new Dictionary<int, ProcessPriorityClass>();
        private static string planAvant = "";

        private static bool AppliquerVraiment(string reglage, bool avec, int pid, string cible)
        {
            if (!OperatingSystem.IsWindows()) return false;
            switch (reglage)
            {
                case "priorite":
                    if (avec && AntiTricheActif()) return false;
                    return avec ? Priorite(pid, ProcessPriorityClass.High) : Retablir(pid, ProcessPriorityClass.High);
                case "retrait":
                {
                    if (string.IsNullOrEmpty(cible) || ActiviteProgrammes.Ignore(cible)) return false;
                    bool un = false;
                    Process[] l;
                    try { l = Process.GetProcessesByName(cible); } catch { return false; }
                    foreach (var p in l)
                    {
                        try { if (p.Id == pid) continue; if (avec ? Priorite(p.Id, ProcessPriorityClass.BelowNormal) : Retablir(p.Id, ProcessPriorityClass.BelowNormal)) un = true; }
                        finally { p.Dispose(); }
                    }
                    // le programme n'est pas ouvert pendant cette partie : rien à faire, le bloc compte quand même
                    return un || l.Length == 0;
                }
                case "alimentation":
                    return avec ? PlanPerformances() : PlanRetablir();
            }
            return false;
        }

        /// <summary>Change la priorité d'un processus en notant l'ancienne (pour la rétablir, même après un arrêt brutal de Velox).</summary>
        public static bool Priorite(int pid, ProcessPriorityClass nouvelle)
        {
            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    var avant = p.PriorityClass;
                    if (avant == nouvelle) return true;
                    if (avant != ProcessPriorityClass.Normal) return false;   // priorité réglée par quelqu'un d'autre : on n'y touche pas
                    lock (priorites) priorites[pid] = avant;
                    NoterRetablir();
                    p.PriorityClass = nouvelle;
                    return p.PriorityClass == nouvelle;
                }
            }
            catch { lock (priorites) priorites.Remove(pid); return false; }
        }

        /// <summary>Remet la priorité d'avant, seulement si elle est encore celle que Velox avait mise.</summary>
        public static bool Retablir(int pid, ProcessPriorityClass mise)
        {
            ProcessPriorityClass avant;
            lock (priorites) { if (!priorites.TryGetValue(pid, out avant)) return true; priorites.Remove(pid); }
            NoterRetablir();
            try { using (var p = Process.GetProcessById(pid)) { if (p.PriorityClass == mise) p.PriorityClass = avant; } } catch { }
            return true;
        }

        private static bool ModeJeuActif() { try { return File.Exists(Chemins.Fichier("modejeu.json")); } catch { return false; } }

        private static bool PlanPerformances()
        {
            if (ModeJeuActif()) return false;   // le mode jeu gère déjà l'alimentation : rien à comparer
            string actuel = Alimentation.PlanActif();
            if (actuel == "" || actuel == Alimentation.Performances) return false;   // déjà en « Performances élevées » : rien à essayer
            if (!Alimentation.Liste().Contains(Alimentation.Performances)) return false;
            planAvant = actuel; NoterRetablir();
            if (Alimentation.Activer(Alimentation.Performances)) return true;
            planAvant = ""; NoterRetablir();
            return false;
        }

        private static bool PlanRetablir()
        {
            if (planAvant == "") return true;
            string avant = planAvant; planAvant = ""; NoterRetablir();
            // seulement si personne n'a changé de mode entre-temps (le mode jeu, par exemple)
            if (!ModeJeuActif() && Alimentation.PlanActif() == Alimentation.Performances) Alimentation.Activer(avant);
            return true;
        }

        // ce que Velox a changé, pour le rétablir au prochain démarrage s'il est fermé brutalement
        private static void NoterRetablir()
        {
            try
            {
                List<object> l;
                lock (priorites) l = priorites.Select(kv => (object)new Dictionary<string, object> { { "Pid", kv.Key.ToString(CultureInfo.InvariantCulture) }, { "Avant", kv.Value.ToString() }, { "Debut", Debut(kv.Key) } }).ToList();
                if (l.Count == 0 && planAvant == "") { if (File.Exists(FichierRetablir)) File.Delete(FichierRetablir); return; }
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(FichierRetablir, Json.Ecrire(new Dictionary<string, object> { { "Plan", planAvant }, { "Priorites", l } }));
            }
            catch { }
        }

        private static string Debut(int pid)
        {
            try { using (var p = Process.GetProcessById(pid)) return p.StartTime.ToUniversalTime().ToString("o"); } catch { return ""; }
        }

        /// <summary>Au démarrage de Velox : rétablit ce qu'une session précédente fermée brutalement aurait laissé.</summary>
        public static void RetablirApresArret()
        {
            try
            {
                if (!File.Exists(FichierRetablir)) return;
                var d = Json.LireFichier(FichierRetablir) as Dictionary<string, object>;
                File.Delete(FichierRetablir);
                if (d == null) return;
                string plan = Json.Texte(d, "Plan");
                if (System.Text.RegularExpressions.Regex.IsMatch(plan, "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$") && !ModeJeuActif() && Alimentation.PlanActif() == Alimentation.Performances)
                    Alimentation.Activer(plan);
                object pr;
                if (d.TryGetValue("Priorites", out pr) && pr != null)
                    foreach (var o in Json.Objets(pr))
                    {
                        int pid; ProcessPriorityClass avant;
                        if (!int.TryParse(Json.Texte(o, "Pid"), out pid) || !Enum.TryParse(Json.Texte(o, "Avant"), out avant)) continue;
                        try
                        {
                            // même processus (même heure de démarrage) : sinon le numéro a été réutilisé par un autre programme
                            using (var p = Process.GetProcessById(pid))
                                if (p.StartTime.ToUniversalTime().ToString("o") == Json.Texte(o, "Debut") && (p.PriorityClass == ProcessPriorityClass.High || p.PriorityClass == ProcessPriorityClass.BelowNormal))
                                    p.PriorityClass = avant;
                        }
                        catch { }
                    }
            }
            catch (Exception ex) { JournalDemarrage.Noter("Autopilote : rétablissement impossible : " + ex.Message); }
        }

        // ---------------------------------------------------------------- données
        public static List<JeuAutopilote> Charger()
        {
            var l = new List<JeuAutopilote>();
            lock (verrou)
            {
                try
                {
                    foreach (var d in Json.Objets(Json.LireFichier(Fichier)))
                    {
                        var j = new JeuAutopilote { Jeu = Json.Texte(d, "Jeu"), Cible = Json.Texte(d, "Cible") };
                        if (j.Jeu == "" || j.Jeu.Length > 120) continue;
                        object es;
                        if (d.TryGetValue("Essais", out es) && es != null)
                            foreach (var e in Json.Objets(es))
                            {
                                string r = Json.Texte(e, "Reglage"), etat = Json.Texte(e, "Etat");
                                if (!Reglages.Contains(r) || !new[] { "attente", "test", "garde", "annule", "impossible" }.Contains(etat)) continue;
                                var x = new EssaiAutopilote { Reglage = r, Etat = etat, Raison = Json.Texte(e, "Raison"), Gain1 = Nombre(e, "Gain1"), GainSaccades = Nombre(e, "GainSaccades") };
                                DateTime t; if (DateTime.TryParse(Json.Texte(e, "Date"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t)) x.Date = t;
                                object bs;
                                if (e.TryGetValue("Blocs", out bs) && bs != null)
                                    foreach (var b in Json.Objets(bs))
                                        x.Blocs.Add(new BlocAutopilote { Avec = Json.Bool(b, "Avec"), Fps = Nombre(b, "Fps"), Fps1 = Nombre(b, "Fps1"), SaccadesMin = Nombre(b, "SaccadesMin"), Secondes = Nombre(b, "Secondes") });
                                if (j.Essais.All(y => y.Reglage != r)) j.Essais.Add(x);
                            }
                        l.Add(j);
                    }
                }
                catch { }
            }
            return l;
        }

        private static double Nombre(Dictionary<string, object> d, string cle)
        {
            double v; return double.TryParse(Json.Texte(d, cle), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : 0;
        }

        private static string Nb(double v) { return Math.Round(v, 3).ToString(CultureInfo.InvariantCulture); }

        public static void Enregistrer(JeuAutopilote j)
        {
            lock (verrou)
            {
                try
                {
                    var tous = Charger().Where(x => !x.Jeu.Equals(j.Jeu, StringComparison.OrdinalIgnoreCase)).ToList();
                    tous.Insert(0, j);
                    Ecrire(tous);
                }
                catch { }
            }
        }

        private static void Ecrire(List<JeuAutopilote> tous)
        {
            Directory.CreateDirectory(Chemins.Local);
            File.WriteAllText(Fichier, Json.Ecrire(tous.Take(30).Select(x => (object)new Dictionary<string, object>
            {
                { "Jeu", x.Jeu }, { "Cible", x.Cible ?? "" },
                { "Essais", x.Essais.Select(e => (object)new Dictionary<string, object>
                    {
                        { "Reglage", e.Reglage }, { "Etat", e.Etat }, { "Raison", e.Raison ?? "" }, { "Gain1", Nb(e.Gain1) }, { "GainSaccades", Nb(e.GainSaccades) },
                        { "Date", e.Date == DateTime.MinValue ? "" : e.Date.ToString("o") },
                        { "Blocs", e.Blocs.Take(40).Select(b => (object)new Dictionary<string, object> { { "Avec", b.Avec }, { "Fps", Nb(b.Fps) }, { "Fps1", Nb(b.Fps1) }, { "SaccadesMin", Nb(b.SaccadesMin) }, { "Secondes", Nb(b.Secondes) } }).ToList() },
                    }).ToList() },
            }).ToList()));
        }

        /// <summary>Recommence tous les essais d'un jeu (après une mise à jour du jeu ou des pilotes, par exemple).</summary>
        public static void Recommencer(string application)
        {
            lock (verrou)
            {
                if (actif && jeu.Equals(application, StringComparison.OrdinalIgnoreCase)) Fin();
                var tous = Charger().Where(x => !x.Jeu.Equals(application, StringComparison.OrdinalIgnoreCase)).ToList();
                Ecrire(tous);
            }
        }

        /// <summary>Données d'exemple (captures, aperçu).</summary>
        public static List<JeuAutopilote> Exemple()
        {
            Func<bool, double, double, BlocAutopilote> b = (avec, f1, sac) => new BlocAutopilote { Avec = avec, Fps1 = f1, Fps = f1 * 1.6, SaccadesMin = sac, Secondes = 180 };
            var cp = new JeuAutopilote { Jeu = "Cyberpunk2077.exe", Cible = "Discord" };
            var e1 = new EssaiAutopilote { Reglage = "retrait", Etat = "garde", Gain1 = 0.06, GainSaccades = 0.44, Date = DateTime.Today.AddDays(-3) };
            for (int i = 0; i < 7; i++) { e1.Blocs.Add(b(false, 70 + i % 3, 1.8)); e1.Blocs.Add(b(true, 74 + i % 3, 1.0)); }
            var e2 = new EssaiAutopilote { Reglage = "priorite", Etat = "annule", Raison = "aucun", Gain1 = 0.004, GainSaccades = 0.02, Date = DateTime.Today.AddDays(-1) };
            for (int i = 0; i < 12; i++) { e2.Blocs.Add(b(false, 75, 1)); e2.Blocs.Add(b(true, 75, 1)); }
            var e3 = new EssaiAutopilote { Reglage = "alimentation", Etat = "test", Date = DateTime.Today };
            for (int i = 0; i < 3; i++) { e3.Blocs.Add(b(false, 75, 1)); if (i < 2) e3.Blocs.Add(b(true, 76, 1)); }
            cp.Essais.AddRange(new[] { e1, e2, e3 });
            var val = new JeuAutopilote { Jeu = "VALORANT-Win64-Shipping.exe" };
            val.Essais.Add(new EssaiAutopilote { Reglage = "priorite", Etat = "impossible", Raison = "refus" });
            var e4 = new EssaiAutopilote { Reglage = "alimentation", Etat = "garde", Gain1 = 0.09, GainSaccades = 0.1 };
            for (int i = 0; i < 6; i++) { e4.Blocs.Add(b(false, 210, 0.5)); e4.Blocs.Add(b(true, 229, 0.45)); }
            val.Essais.Add(e4);
            return new List<JeuAutopilote> { cp, val };
        }
    }
}
