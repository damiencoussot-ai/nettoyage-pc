// Nettoyage PC - rapport avant / après : chaque nettoyage réel (rapide ou profond) garde l'indice du PC avant et après,
// et l'espace libéré. Les 50 derniers sont gardés dans le dossier local de l'application. Sans interface : partagé v4 / v5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal class ComparaisonNettoyage
    {
        public DateTime Date;
        public string Type = "";        // « rapide » ou « profond »
        public int Avant = -1, Apres = -1;
        public long Octets;
        public int Gain { get { return Avant >= 0 && Apres >= 0 ? Apres - Avant : 0; } }
    }

    internal static class AvantApres
    {
        public const int Maximum = 50;
        public static string Fichier { get { return Path.Combine(Chemins.Local, "avant-apres.json"); } }

        public static List<ComparaisonNettoyage> Liste()
        {
            var l = new List<ComparaisonNettoyage>();
            try
            {
                foreach (var o in Json.Objets(Json.LireFichier(Fichier)))
                {
                    DateTime d;
                    if (!DateTime.TryParse(Json.Texte(o, "Date"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out d)) continue;
                    l.Add(new ComparaisonNettoyage { Date = d, Type = Json.Texte(o, "Type"), Avant = (int)Json.Long(o, "Avant"), Apres = (int)Json.Long(o, "Apres"), Octets = Json.Long(o, "Octets") });
                }
            }
            catch { }
            return l.OrderByDescending(c => c.Date).ToList();
        }

        public static void Enregistrer(string type, int avant, int apres, long octets)
        {
            try
            {
                var l = Liste();
                l.Insert(0, new ComparaisonNettoyage { Date = DateTime.Now, Type = type, Avant = avant, Apres = apres, Octets = octets });
                var sortie = l.Take(Maximum).Select(c => (object)new Dictionary<string, object>
                {
                    { "Date", c.Date.ToString("o", CultureInfo.InvariantCulture) }, { "Type", c.Type }, { "Avant", c.Avant }, { "Apres", c.Apres }, { "Octets", c.Octets },
                }).ToList();
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(Fichier, Json.Ecrire(sortie));
            }
            catch (Exception ex) { JournalDemarrage.Erreur("rapport avant / après", ex); }
        }
    }
}
