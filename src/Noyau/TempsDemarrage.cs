// Velox - temps de démarrage du PC, mesuré chaque jour sans droits administrateur : au lancement du gardien (ou de
// Velox) juste après l'ouverture de session, Windows donne le temps écoulé depuis l'allumage. C'est le temps « du bouton
// marche jusqu'au bureau » (connexion comprise). Rien n'est envoyé nulle part. Sans interface : partagé v4 / v5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal static class TempsDemarrage
    {
        private static string Fichier { get { return Path.Combine(Chemins.Local, "demarrages-velox.json"); } }
        private static readonly object verrou = new object();

        /// <summary>Note le démarrage en cours si Velox ou le gardien se lance moins de 10 minutes après l'allumage du PC.</summary>
        public static void Noter()
        {
            try
            {
                double secondes = EnvironmentCompat.Millisecondes() / 1000.0;
                if (secondes < 5 || secondes > 600) return;   // lancé plus tard dans la journée : ce n'est pas un démarrage
                var allumage = DateTime.Now.AddSeconds(-secondes);
                lock (verrou)
                {
                    var l = Lire();
                    // un seul relevé par démarrage (le gardien et Velox peuvent se lancer tous les deux) : le premier, le plus juste
                    if (l.Any(x => Math.Abs((x.Item1 - allumage).TotalMinutes) < 2)) return;
                    l.Add(Tuple.Create(allumage, Math.Round(secondes, 1)));
                    Directory.CreateDirectory(Chemins.Local);
                    File.WriteAllText(Fichier, Json.Ecrire(l.OrderByDescending(x => x.Item1).Take(200)
                        .Select(x => (object)new Dictionary<string, object> { { "Date", x.Item1.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) }, { "Secondes", x.Item2 } }).ToList()));
                }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("temps de démarrage", ex); }
        }

        /// <summary>Démarrages notés (date d'allumage, secondes jusqu'au bureau), du plus ancien au plus récent.</summary>
        public static List<Tuple<DateTime, double>> Lire()
        {
            var l = new List<Tuple<DateTime, double>>();
            try
            {
                foreach (var x in Json.Objets(Json.LireFichier(Fichier)))
                {
                    DateTime d; double s;
                    if (DateTime.TryParse(Json.Texte(x, "Date"), CultureInfo.InvariantCulture, DateTimeStyles.None, out d)
                        && double.TryParse(Convert.ToString(Json.Valeur(x, "Secondes"), CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out s) && s > 0)
                        l.Add(Tuple.Create(d, s));
                }
            }
            catch { }
            return l.OrderBy(x => x.Item1).ToList();
        }

        /// <summary>Un point par jour (le premier démarrage du jour) sur les « jours » derniers jours.</summary>
        public static List<Tuple<DateTime, double>> ParJour(int jours)
        {
            var debut = DateTime.Today.AddDays(-jours + 1);
            return Lire().Where(x => x.Item1 >= debut).GroupBy(x => x.Item1.Date).Select(g => Tuple.Create(g.Key, g.First().Item2)).OrderBy(x => x.Item1).ToList();
        }

        /// <summary>Phrase de bilan : moyenne des 7 derniers jours comparée aux tout premiers relevés.</summary>
        public static string Bilan()
        {
            var l = Lire();
            if (l.Count == 0) return Langue.T("Pas encore de mesure : redémarre ton PC, Velox notera le temps qu'il met à arriver sur le bureau.");
            var recents = l.Where(x => x.Item1 >= DateTime.Now.AddDays(-7)).Select(x => x.Item2).ToList();
            double moy = (recents.Count > 0 ? recents : l.Select(x => x.Item2).ToList()).Average();
            string t = Langue.F("Ton PC met en moyenne {0} s pour arriver sur le bureau.", Math.Round(moy));
            var premiers = l.Take(5).Select(x => x.Item2).ToList();
            if (l.Count >= 8 && premiers.Count >= 3)
            {
                double gain = premiers.Average() - moy;
                if (gain >= 2) t += " " + Langue.F("C'est {0} s plus vite qu'au début des mesures, le {1}.", Math.Round(gain), l[0].Item1.ToString("dd/MM/yyyy"));
                else if (gain <= -3) t += " " + Langue.F("C'est {0} s plus lent qu'au début des mesures : regarde les programmes lancés au démarrage.", Math.Round(-gain));
            }
            return t;
        }
    }
}
