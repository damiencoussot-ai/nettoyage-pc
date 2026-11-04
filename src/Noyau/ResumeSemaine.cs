// Velox - résumé de la semaine : espace libéré et nombre de nettoyages des 7 derniers jours, indice du PC
// aujourd'hui et son évolution. Montré une fois par semaine (notification du gardien, et message à l'ouverture).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal class ResumeHebdo
    {
        public long Octets;
        public int Nettoyages, Indice = -1, IndiceAvant = -1;
        public string Titre = "", Texte = "";
    }

    internal static class ResumeSemaine
    {
        public static ResumeHebdo Calculer()
        {
            var r = new ResumeHebdo();
            var fin = DateTime.Now; var debut = fin.AddDays(-7);
            try { var g = Bulletin.GainsPeriode(debut, fin.AddMinutes(1)); r.Octets = g.Item1; r.Nettoyages = g.Item2; } catch { }
            // indices notés à chaque bilan (« date;score ») : le plus récent, et le dernier d'il y a une semaine ou plus
            try
            {
                string f = Chemins.Fichier("scores.csv");
                if (File.Exists(f))
                {
                    var points = new List<Tuple<DateTime, int>>();
                    foreach (var l in File.ReadAllLines(f))
                    {
                        var c = l.Split(';'); DateTime d; int s;
                        if (c.Length >= 2 && DateTime.TryParseExact(c[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && int.TryParse(c[1].Trim(), out s)) points.Add(Tuple.Create(d, s));
                    }
                    if (points.Count > 0)
                    {
                        r.Indice = points.OrderBy(p => p.Item1).Last().Item2;
                        var avant = points.Where(p => p.Item1 <= debut).OrderBy(p => p.Item1).LastOrDefault();
                        if (avant != null) r.IndiceAvant = avant.Item2;
                    }
                }
            }
            catch { }
            r.Titre = Langue.T("Ta semaine avec Velox");
            var morceaux = new List<string>();
            morceaux.Add(r.Nettoyages > 0 ? Langue.F("{0} libérés en {1} nettoyage(s)", Format.Taille(r.Octets), r.Nettoyages) : Langue.T("aucun nettoyage cette semaine"));
            if (r.Indice >= 0)
                morceaux.Add(r.IndiceAvant >= 0 && r.IndiceAvant != r.Indice ? Langue.F("indice {0} ({1}{2})", r.Indice, r.Indice > r.IndiceAvant ? "+" : "", r.Indice - r.IndiceAvant) : Langue.F("indice {0}", r.Indice));
            r.Texte = Langue.T("Cette semaine") + " : " + string.Join(", ", morceaux) + ". "
                + (r.Indice >= 85 ? Langue.T("Rien à signaler.") : r.Indice >= 0 ? Langue.T("Ouvre Velox pour voir ce qui peut être amélioré.") : "");
            return r;
        }
    }
}
