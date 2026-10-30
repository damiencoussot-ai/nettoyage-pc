// Nettoyage PC - ce que les nettoyages ont rapporté (Documents\NettoyagePC\statistiques.csv, écrit par le moteur :
// « date;octets libérés;type »). Lecture seule, partagé par la version 4 et la version 5.
using System;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal class GainsNettoyage
    {
        /// <summary>Dernier nettoyage réel (null si aucun) et ce qu'il a libéré.</summary>
        public DateTime? Dernier;
        public long DernierOctets;
        public string DernierType = "";
        /// <summary>Depuis le début : total libéré et nombre de nettoyages.</summary>
        public long Total;
        public int Nombre;
    }

    internal static class Gains
    {
        public static GainsNettoyage Lire()
        {
            var g = new GainsNettoyage();
            try
            {
                string f = Chemins.Fichier("statistiques.csv");
                if (!File.Exists(f)) return g;
                foreach (var l in File.ReadAllLines(f))
                {
                    var c = l.Split(';');
                    DateTime d; long o;
                    if (c.Length < 2 || !DateTime.TryParseExact(c[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) || !long.TryParse(c[1].Trim(), out o) || o < 0) continue;
                    g.Total += o; g.Nombre++;
                    if (!g.Dernier.HasValue || d >= g.Dernier.Value) { g.Dernier = d; g.DernierOctets = o; g.DernierType = c.Length > 2 ? c[2].Trim() : ""; }
                }
            }
            catch { }
            return g;
        }

        /// <summary>Nombre de jours depuis le dernier nettoyage (-1 si jamais).</summary>
        public static int JoursDepuisDernier(GainsNettoyage g)
        {
            return g.Dernier.HasValue ? (int)Math.Floor((DateTime.Now - g.Dernier.Value).TotalDays) : -1;
        }
    }
}
