// Nettoyage PC - langue de l'interface (français, English, Español)
// Le texte d'origine (français) sert de clé : une phrase non traduite reste simplement en français.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal static partial class Langue
    {
        public static readonly List<KeyValuePair<string, string>> Disponibles = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("fr", "Français"), new KeyValuePair<string, string>("en", "English"), new KeyValuePair<string, string>("es", "Español")
        };

        public static string Code { get; private set; } = "fr";
        private static Dictionary<string, string> phrases;
        private static List<KeyValuePair<string, string>> debuts;
        private static List<KeyValuePair<Regex, string>> motifs;
        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly object verrou = new object();

        public static void Choisir(string code)
        {
            int col = code == "en" ? 1 : code == "es" ? 2 : 0;
            Code = col == 0 ? "fr" : code;
            lock (verrou) cache.Clear();
            if (col == 0) { phrases = null; debuts = null; motifs = null; return; }
            phrases = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i + 2 < Table.Length; i += 3) phrases[Table[i]] = Table[i + col];
            debuts = new List<KeyValuePair<string, string>>();
            for (int i = 0; i + 2 < Prefixes.Length; i += 3) debuts.Add(new KeyValuePair<string, string>(Prefixes[i], Prefixes[i + col]));
            debuts = debuts.OrderByDescending(d => d.Key.Length).ToList();
            motifs = new List<KeyValuePair<Regex, string>>();
            for (int i = 0; i + 2 < Motifs.Length; i += 3)
                try { motifs.Add(new KeyValuePair<Regex, string>(new Regex(Motifs[i], RegexOptions.CultureInvariant), Motifs[i + col])); } catch { }
        }

        /// <summary>Traduit un texte de l'interface (phrase entière, sinon ligne par ligne, sinon début de phrase connu).</summary>
        /// <summary>Culture de la langue choisie (majuscules, dates).</summary>
        public static System.Globalization.CultureInfo Culture
        {
            get { try { return System.Globalization.CultureInfo.GetCultureInfo(Code == "en" ? "en-US" : Code == "es" ? "es-ES" : "fr-FR"); } catch { return System.Globalization.CultureInfo.InvariantCulture; } }
        }

        public static string T(string texte)
        {
            if (phrases == null || string.IsNullOrEmpty(texte)) return texte;
            string r;
            if (phrases.TryGetValue(texte, out r)) return r;
            lock (verrou) { if (cache.TryGetValue(texte, out r)) return r; }
            r = Calculer(texte);
            lock (verrou) { if (cache.Count > 4000) cache.Clear(); cache[texte] = r; }
            return r;
        }

        private static string Calculer(string texte)
        {
            if (texte.IndexOf('\n') >= 0)
            {
                var l = texte.Split('\n');
                bool change = false;
                for (int i = 0; i < l.Length; i++) { string t = Une(l[i].TrimEnd('\r')); if (!ReferenceEquals(t, l[i]) && t != l[i]) { l[i] = t; change = true; } }
                return change ? string.Join("\n", l) : texte;
            }
            return Une(texte);
        }

        /// <summary>Phrase à trous : le modèle (« {0} écran(s) bleu(s) ») est traduit, puis complété.</summary>
        public static string F(string modele, params object[] valeurs)
        {
            try { return string.Format(T(modele), valeurs); } catch { return string.Format(modele, valeurs); }
        }

        /// <summary>Traduction d'une phrase entière seulement (pour les données affichées dans les listes).</summary>
        public static string Exact(string texte)
        {
            string r;
            return phrases != null && texte != null && phrases.TryGetValue(texte, out r) ? r : texte;
        }

        private static string Une(string texte)
        {
            if (texte.Length == 0) return texte;
            string r;
            if (phrases.TryGetValue(texte, out r)) return r;
            // la version 5 écrit « … » là où la table (version 4) a « ... » : même phrase
            if (texte.IndexOf('…') >= 0 && phrases.TryGetValue(texte.Replace("…", "..."), out r)) return r.Replace("...", "…");
            foreach (var m in motifs) { var x = m.Key.Match(texte); if (x.Success) return x.Result(m.Value); }
            foreach (var d in debuts) if (texte.StartsWith(d.Key, StringComparison.Ordinal) && texte.Length > d.Key.Length) return d.Value + texte.Substring(d.Key.Length);
            if (Manquantes != null) lock (Manquantes) Manquantes.Add(texte);
            return texte;
        }

        /// <summary>(Mise au point) Phrases restées sans traduction, notées quand la variable NP_TRADUCTIONS_MANQUANTES est définie.</summary>
        public static readonly HashSet<string> Manquantes = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NP_TRADUCTIONS_MANQUANTES")) ? null : new HashSet<string>();
        public static void EcrireManquantes()
        {
            string f = Environment.GetEnvironmentVariable("NP_TRADUCTIONS_MANQUANTES");
            if (Manquantes == null || string.IsNullOrEmpty(f)) return;
            try
            {
                // on écarte ce qui est déjà traduit (une traduction réaffichée repasse par T), les nombres et les noms propres sans mot
                // (toutes les langues : les captures passent de l'anglais à l'espagnol)
                var traduits = new HashSet<string>(StringComparer.Ordinal);
                var debutsTraduits = new List<string>();
                for (int i = 0; i + 2 < Table.Length; i += 3)
                    for (int c = 1; c <= 2; c++) { traduits.Add(Table[i + c]); traduits.Add(Table[i + c].ToUpperInvariant()); }
                for (int i = 0; i + 2 < Prefixes.Length; i += 3) for (int c = 1; c <= 2; c++) if (Prefixes[i + c].Length > 3) debutsTraduits.Add(Prefixes[i + c]);
                for (int i = 0; i + 2 < Motifs.Length; i += 3)
                    for (int c = 1; c <= 2; c++)
                    {
                        string m = Motifs[i + c]; int d = m.IndexOf('$');
                        string debut = d < 0 ? m : m.Substring(0, d);
                        if (debut.Length > 3) debutsTraduits.Add(debut);
                    }
                Func<string, bool> garder = x => Regex.IsMatch(x, "[A-Za-zÀ-ÿ]{3}") && !traduits.Contains(x) && !traduits.Contains(x.Replace("…", "..."))
                    && !debutsTraduits.Any(d => x.StartsWith(d, StringComparison.Ordinal));
                lock (Manquantes) System.IO.File.WriteAllLines(f, Manquantes.Where(garder).OrderBy(x => x));
            }
            catch { }
        }
    }
}
