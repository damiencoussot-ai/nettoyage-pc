// Velox - progression du profil Gamer (pour le plaisir) : XP gagnée en entretenant le PC, niveau, rang (Bronze à
// Légende), série de jours, défis du jour et succès avec rareté. Tout est calculé à partir de ce que l'application a
// déjà noté sur ce PC (nettoyages, indices, parties, journal du profil Gamer) : rien n'est envoyé, rien n'est modifié.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal enum Rarete { Commun, Rare, Epique, Legendaire }

    internal class SuccesJeu
    {
        public string Icone = "", Nom = "", Condition = "";
        public bool Obtenu;
        public Rarete Rarete;
        public int Xp { get { return Rarete == Rarete.Legendaire ? 150 : Rarete == Rarete.Epique ? 75 : Rarete == Rarete.Rare ? 40 : 20; } }
    }

    internal class DefiJeu
    {
        public string Cle = "", Nom = "";
        public bool Fait;
        public int Xp = 30;
    }

    internal class RangJeu
    {
        public string Nom = "";
        public int Palier;               // 0 = Bronze ... 5 = Légende
        public int NiveauMin, NiveauSuivant;
    }

    internal class ProgressionJeu
    {
        public int Xp, Niveau, XpNiveau, XpSuivant, Serie, MeilleureSerie, DefisFaits;
        public List<SuccesJeu> Succes = new List<SuccesJeu>();
        public List<DefiJeu> DefisDuJour = new List<DefiJeu>();
        public double Avancement { get { return XpSuivant > XpNiveau ? (double)(Xp - XpNiveau) / (XpSuivant - XpNiveau) : 1; } }
        public RangJeu Rang { get { return RangsJeu.Rang(Niveau); } }
        public string Titre { get { return Rang.Nom; } }
    }

    /// <summary>Journal du profil Gamer (Documents\NettoyagePC\journal-jeu.txt) : « date;événement ».</summary>
    internal static class JournalJeu
    {
        private static string Fichier { get { return Chemins.Fichier("journal-jeu.txt"); } }
        private static readonly object verrou = new object();

        /// <summary>Note un événement (ouverture, boost, overlay, monpc, session) ; « ouverture » une seule fois par jour.</summary>
        public static void Noter(string type)
        {
            try
            {
                lock (verrou)
                {
                    string jour = DateTime.Now.ToString("yyyy-MM-dd");
                    if (type == "ouverture" && Lire().Any(e => e.Item2 == "ouverture" && e.Item1.ToString("yyyy-MM-dd") == jour)) return;
                    Chemins.CreerDonnees();
                    FichierSur.AjouterLigne(Fichier, DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + ";" + type);
                }
            }
            catch { }
        }

        public static List<Tuple<DateTime, string>> Lire()
        {
            var l = new List<Tuple<DateTime, string>>();
            try
            {
                if (!File.Exists(Fichier)) return l;
                foreach (var x in File.ReadAllLines(Fichier).Reverse().Take(5000))
                {
                    var c = x.Split(';'); DateTime d;
                    if (c.Length >= 2 && DateTime.TryParseExact(c[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) l.Add(Tuple.Create(d, c[1].Trim()));
                }
            }
            catch { }
            return l;
        }
    }

    /// <summary>Ce qui sert au calcul (lu sur le PC, ou d'exemple pour les captures).</summary>
    internal class DonneesJeu
    {
        public List<Tuple<DateTime, long>> Nettoyages = new List<Tuple<DateTime, long>>();
        public List<Tuple<DateTime, int>> Indices = new List<Tuple<DateTime, int>>();
        public List<Tuple<DateTime, string>> Journal = new List<Tuple<DateTime, string>>();
        public int PartiesAuto, MeilleurGain;
    }

    internal static class RangsJeu
    {
        public static readonly string[] Rangs = { "Bronze", "Argent", "Or", "Platine", "Diamant", "Légende" };
        private static readonly int[] Paliers = { 1, 5, 10, 15, 20, 30 };

        public static RangJeu Rang(int niveau)
        {
            int p = 0;
            for (int i = 0; i < Paliers.Length; i++) if (niveau >= Paliers[i]) p = i;
            return new RangJeu { Nom = Rangs[p], Palier = p, NiveauMin = Paliers[p], NiveauSuivant = p + 1 < Paliers.Length ? Paliers[p + 1] : 0 };
        }
    }

    internal static class Succes
    {
        /// <summary>XP nécessaire pour atteindre un niveau (courbe douce : 0, 100, 300, 600, 1000...).</summary>
        public static int XpPourNiveau(int n) { return 50 * (n - 1) * n; }

        // défis possibles (trois par jour, toujours les mêmes pour une date donnée)
        private static readonly string[][] Defis =
        {
            new[] { "nettoyage", "Fais un nettoyage" },
            new[] { "500mo", "Libère 500 Mo" },
            new[] { "boost", "Lance un BOOST" },
            new[] { "analyse", "Analyse ton PC" },
            new[] { "monpc", "Jette un œil à Mon PC" },
            new[] { "overlay", "Affiche l'overlay en jeu" },
            new[] { "indice80", "Atteins un indice de 80" },
        };

        public static List<string[]> DefisPour(DateTime jour)
        {
            // tirage stable : la date sert de graine
            var r = new Random(jour.Year * 1000 + jour.DayOfYear);
            return Defis.OrderBy(x => r.Next()).Take(3).ToList();
        }

        private static bool DefiFait(string cle, DateTime jour, DonneesJeu d)
        {
            Func<DateTime, bool> ceJour = x => x.Date == jour.Date;
            switch (cle)
            {
                case "nettoyage": return d.Nettoyages.Any(x => ceJour(x.Item1));
                case "500mo": return d.Nettoyages.Where(x => ceJour(x.Item1)).Sum(x => x.Item2) >= 500L * 1048576;
                case "analyse": return d.Indices.Any(x => ceJour(x.Item1));
                case "indice80": return d.Indices.Any(x => ceJour(x.Item1) && x.Item2 >= 80);
                default: return d.Journal.Any(x => ceJour(x.Item1) && x.Item2 == cle);
            }
        }

        public static DonneesJeu Lire()
        {
            var d = new DonneesJeu();
            try
            {
                string f = Chemins.Fichier("statistiques.csv");
                if (File.Exists(f))
                    foreach (var l in File.ReadAllLines(f))
                    {
                        var c = l.Split(';'); DateTime t; long o;
                        if (c.Length >= 2 && DateTime.TryParseExact(c[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) && long.TryParse(c[1].Trim(), out o) && o >= 0) d.Nettoyages.Add(Tuple.Create(t, o));
                    }
            }
            catch { }
            try
            {
                string f = Chemins.Fichier("scores.csv");
                if (File.Exists(f))
                    foreach (var l in File.ReadAllLines(f))
                    {
                        var c = l.Split(';'); DateTime t; int s;
                        if (c.Length >= 2 && DateTime.TryParseExact(c[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) && int.TryParse(c[1].Trim(), out s)) d.Indices.Add(Tuple.Create(t, s));
                    }
            }
            catch { }
            d.Journal = JournalJeu.Lire();
            try { d.PartiesAuto = ModeJeuAuto.Sessions().Count; } catch { }
            try { d.MeilleurGain = AvantApres.Liste().Select(c => c.Gain).DefaultIfEmpty(0).Max(); } catch { }
            return d;
        }

        public static ProgressionJeu Calculer() { return Construire(Lire(), DateTime.Now); }

        /// <summary>(captures) progression d'exemple : deux semaines d'utilisation réaliste.</summary>
        public static ProgressionJeu Exemple()
        {
            var d = new DonneesJeu { PartiesAuto = 12, MeilleurGain = 11 };
            var maintenant = DateTime.Now; var r = new Random(7);
            for (int j = 13; j >= 0; j--)
            {
                var jour = maintenant.Date.AddDays(-j).AddHours(20);
                d.Journal.Add(Tuple.Create(jour, "ouverture"));
                if (j % 2 == 0) d.Nettoyages.Add(Tuple.Create(jour, (long)(r.Next(300, 2500)) * 1048576));
                d.Indices.Add(Tuple.Create(jour, 70 + r.Next(0, 16)));
                if (j % 3 == 0) d.Journal.Add(Tuple.Create(jour, "boost"));
            }
            d.Journal.Add(Tuple.Create(maintenant, "boost"));
            return Construire(d, maintenant);
        }

        public static ProgressionJeu Construire(DonneesJeu d, DateTime maintenant)
        {
            var p = new ProgressionJeu();
            long octets = d.Nettoyages.Sum(x => x.Item2);
            int nettoyages = d.Nettoyages.Count;
            int semaines = d.Nettoyages.Select(x => x.Item1.Date.AddDays(-(((int)x.Item1.DayOfWeek + 6) % 7))).Distinct().Count();
            int meilleurIndice = d.Indices.Select(x => x.Item2).DefaultIfEmpty(0).Max();
            int boosts = d.Journal.Count(x => x.Item2 == "boost");
            bool nuitBlanche = d.Journal.Any(x => x.Item2 == "boost" && x.Item1.Hour < 5);

            // jours d'activité (Velox ouvert ou nettoyage) : série en cours et meilleure série
            var jours = new HashSet<DateTime>(d.Journal.Where(x => x.Item2 == "ouverture").Select(x => x.Item1.Date).Concat(d.Nettoyages.Select(x => x.Item1.Date)));
            DateTime k = jours.Contains(maintenant.Date) ? maintenant.Date : maintenant.Date.AddDays(-1);
            while (jours.Contains(k)) { p.Serie++; k = k.AddDays(-1); }
            int courante = 0; DateTime? avant = null;
            foreach (var j in jours.OrderBy(x => x)) { courante = avant.HasValue && (j - avant.Value).TotalDays == 1 ? courante + 1 : 1; avant = j; p.MeilleureSerie = Math.Max(p.MeilleureSerie, courante); }

            // défis : ceux d'aujourd'hui, et ceux des jours passés (sur un an) pour l'XP et les succès
            int journeesParfaites = 0;
            foreach (var j in jours.Concat(new[] { maintenant.Date }).Distinct().Where(x => x > maintenant.Date.AddDays(-366)))
            {
                var liste = DefisPour(j);
                int faits = 0;
                foreach (var df in liste)
                {
                    bool ok = DefiFait(df[0], j, d);
                    if (ok) faits++;
                    if (j == maintenant.Date) p.DefisDuJour.Add(new DefiJeu { Cle = df[0], Nom = df[1], Fait = ok });
                }
                p.DefisFaits += faits;
                if (faits == liste.Count) journeesParfaites++;
            }

            // succès, avec leur rareté
            Action<string, string, Rarete, bool> s = (nom, cond, rar, ok) => p.Succes.Add(new SuccesJeu { Icone = "◆", Nom = nom, Condition = cond, Rarete = rar, Obtenu = ok });
            const long Go = 1073741824L;
            s("Premier coup de balai", "Faire un premier nettoyage", Rarete.Commun, nettoyages >= 1);
            s("Un giga de gagné", "Libérer 1 Go au total", Rarete.Commun, octets >= Go);
            s("Dix gigas", "Libérer 10 Go au total", Rarete.Rare, octets >= 10 * Go);
            s("Centurion", "Libérer 100 Go au total", Rarete.Epique, octets >= 100 * Go);
            s("Habitué", "Faire 10 nettoyages", Rarete.Commun, nettoyages >= 10);
            s("Régulier", "Entretenir le PC 4 semaines différentes", Rarete.Rare, semaines >= 4);
            s("En forme", "Atteindre un indice de 80", Rarete.Commun, meilleurIndice >= 80);
            s("Machine de guerre", "Atteindre un indice de 90", Rarete.Epique, meilleurIndice >= 90);
            s("Coup de boost", "Gagner 10 points d'indice en un nettoyage", Rarete.Rare, d.MeilleurGain >= 10);
            s("Joueur", "Jouer une partie en mode jeu automatique", Rarete.Commun, d.PartiesAuto >= 1);
            s("Marathonien", "Jouer 25 parties en mode jeu automatique", Rarete.Epique, d.PartiesAuto >= 25);
            s("Allumage", "Lancer un premier BOOST", Rarete.Commun, boosts >= 1);
            s("Turbo", "Lancer 10 BOOST", Rarete.Rare, boosts >= 10);
            s("Nuit blanche", "Lancer un BOOST entre minuit et 5 h", Rarete.Rare, nuitBlanche);
            s("En série", "Une série de 3 jours", Rarete.Commun, p.MeilleureSerie >= 3);
            s("Semaine parfaite", "Une série de 7 jours", Rarete.Rare, p.MeilleureSerie >= 7);
            s("Inarrêtable", "Une série de 30 jours", Rarete.Legendaire, p.MeilleureSerie >= 30);
            s("Chasseur de défis", "Réussir 10 défis du jour", Rarete.Rare, p.DefisFaits >= 10);
            s("Journée parfaite", "Réussir les 3 défis d'une même journée", Rarete.Rare, journeesParfaites >= 1);
            s("Maître des défis", "Réussir 100 défis du jour", Rarete.Legendaire, p.DefisFaits >= 100);

            // XP : entretien + défis + séries + succès
            long xp = nettoyages * 10L + octets / (100L * 1048576) + d.PartiesAuto * 5L + semaines * 20L + boosts * 5L
                + p.DefisFaits * 30L + journeesParfaites * 50L + jours.Count * 5L
                + (meilleurIndice >= 90 ? 100 : meilleurIndice >= 80 ? 50 : 0)
                + p.Succes.Where(x => x.Obtenu).Sum(x => (long)x.Xp);
            p.Xp = (int)Math.Min(int.MaxValue, xp);
            p.Niveau = 1;
            while (XpPourNiveau(p.Niveau + 1) <= p.Xp && p.Niveau < 99) p.Niveau++;
            p.XpNiveau = XpPourNiveau(p.Niveau); p.XpSuivant = XpPourNiveau(p.Niveau + 1);
            // succès de niveau (après le calcul du niveau : leur XP ne compte pas, pour éviter une boucle)
            s("Or massif", "Atteindre le rang Or (niveau 10)", Rarete.Epique, p.Niveau >= 10);
            s("Légende", "Atteindre le rang Légende (niveau 30)", Rarete.Legendaire, p.Niveau >= 30);
            return p;
        }
    }
}
