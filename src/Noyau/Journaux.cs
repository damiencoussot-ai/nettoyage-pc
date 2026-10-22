// Nettoyage PC - lecture simplifiée des journaux de Windows (événements avec leurs données nommées)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml.Linq;

namespace NettoyagePC
{
    internal class EvenementSimple
    {
        public DateTime Date;
        public int Id;
        public string Source = "";
        public Dictionary<string, string> Donnees = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public string D(string cle) { string v; return Donnees.TryGetValue(cle, out v) ? (v ?? "").Trim() : ""; }
    }

    internal static class Journaux
    {
        /// <summary>Événements (les plus récents d'abord) avec leurs données nommées (EventData et UserData). Liste vide si illisible.</summary>
        public static List<EvenementSimple> Lister(string journal, string xpath, int max = 500, bool donnees = true)
        {
            // (la lecture est dans une méthode à part : si les journaux de Windows sont absents, l'erreur reste attrapée ici)
            if (!Natif.EstWindows) return new List<EvenementSimple>();
            try { return ListerInterne(journal, xpath, max, donnees); } catch { return new List<EvenementSimple>(); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static List<EvenementSimple> ListerInterne(string journal, string xpath, int max, bool donnees)
        {
            var l = new List<EvenementSimple>();
            try
            {
                foreach (var e in Evenements.Lire(journal, xpath, max))
                {
                    using (e)
                    {
                        var s = new EvenementSimple { Date = e.TimeCreated ?? DateTime.MinValue, Id = e.Id, Source = e.ProviderName ?? "" };
                        if (donnees)
                        {
                            try
                            {
                                var x = XDocument.Parse(e.ToXml());
                                foreach (var el in x.Descendants())
                                {
                                    if (el.Name.LocalName == "Data") { var n = el.Attribute("Name"); if (n != null && !s.Donnees.ContainsKey(n.Value)) s.Donnees[n.Value] = el.Value; }
                                    else if (!el.HasElements && el.Ancestors().Any(a => a.Name.LocalName == "UserData") && !s.Donnees.ContainsKey(el.Name.LocalName)) s.Donnees[el.Name.LocalName] = el.Value;
                                }
                            }
                            catch { }
                        }
                        l.Add(s);
                    }
                }
            }
            catch { }
            return l;
        }

        /// <summary>Filtre XPath « depuis N jours » (à combiner avec d'autres conditions System).</summary>
        public static string Depuis(int jours) { return "TimeCreated[timediff(@SystemTime) <= " + ((long)jours * 86400000L) + "]"; }
    }
}
