// Velox - calibration du casque pour le Studio son.
// Base libre AutoEq (Jaakko Pasanen, licence MIT) : plus de 6 000 casques et écouteurs mesurés en laboratoire
// (oratory1990, crinacle, Rtings…), avec pour chacun une correction en filtres paramétriques, déjà au format
// d'Equalizer APO. Velox télécharge la liste (gardée deux semaines), puis la correction du modèle choisi ; chaque ligne
// est relue strictement (types, bornes) : rien d'autre que des filtres n'entre dans le fichier d'Equalizer APO.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal sealed class CasqueAutoEq
    {
        public string Nom = "", Chemin = "", Source = "";
        public bool IntraAuriculaire;
    }

    internal static class Casques
    {
        public const string Base = "https://raw.githubusercontent.com/jaakkopasanen/AutoEq/master/results/";
        public const string Projet = "https://github.com/jaakkopasanen/AutoEq";
        public const int FiltresMax = 20;

        private static readonly Regex Ligne = new Regex(@"^- \[(.{1,150}?)\]\(\./([^)\r\n]{1,400})\) by ([^\r\n]{1,120})$", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        // chemin relatif dans « results » : segments encodés (%20…), sans « .. » ni barre inverse
        private static readonly Regex CheminSur = new Regex(@"^[A-Za-z0-9%()\-_.,&+'!]+(/[A-Za-z0-9%()\-_.,&+'!]+){1,3}$", RegexOptions.CultureInvariant);
        private static readonly Regex LigneFiltre = new Regex(@"^Filter\s+\d{1,2}:\s+ON\s+(PK|LSC|HSC)\s+Fc\s+(\d{1,5}(?:\.\d+)?)\s+Hz\s+Gain\s+(-?\d{1,2}(?:\.\d+)?)\s+dB\s+Q\s+(\d{1,2}(?:\.\d+)?)\s*$", RegexOptions.CultureInvariant);
        private static readonly Regex LignePreamp = new Regex(@"^Preamp:\s+(-?\d{1,2}(?:\.\d+)?)\s+dB\s*$", RegexOptions.CultureInvariant);

        // sources préférées quand un même modèle a été mesuré plusieurs fois
        private static readonly string[] Preferees = { "oratory1990", "crinacle", "Rtings", "Innerfidelity", "Super Review", "Headphone.com Legacy" };
        private static int Rang(string source) { int i = Array.FindIndex(Preferees, p => source.StartsWith(p, StringComparison.OrdinalIgnoreCase)); return i < 0 ? Preferees.Length : i; }

        public static bool CheminValide(string c) { return c != null && CheminSur.IsMatch(c) && !c.Contains("..") && !c.Contains("%2F") && !c.Contains("%2f") && !c.Contains("%5C") && !c.Contains("%5c"); }

        /// <summary>Liste des casques (fichier INDEX.md d'AutoEq) : un modèle par nom, mesure de la source la plus fiable.</summary>
        public static List<CasqueAutoEq> LireIndex(string md)
        {
            var parNom = new Dictionary<string, CasqueAutoEq>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Ligne.Matches(md ?? ""))
            {
                string nom = m.Groups[1].Value.Trim(), chemin = m.Groups[2].Value.Trim(), source = m.Groups[3].Value.Trim();
                if (nom == "" || !CheminValide(chemin)) continue;
                var c = new CasqueAutoEq { Nom = nom, Chemin = chemin, Source = source, IntraAuriculaire = chemin.Contains("in-ear") || chemin.Contains("earbud") };
                CasqueAutoEq deja;
                if (!parNom.TryGetValue(nom, out deja) || Rang(source) < Rang(deja.Source)) parNom[nom] = c;
            }
            return parNom.Values.OrderBy(c => c.Nom, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static string Simple(string s)
        {
            var d = (s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var b = new StringBuilder();
            foreach (char ch in d) if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) b.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
            return Regex.Replace(b.ToString(), @"\s+", " ").Trim();
        }

        /// <summary>Recherche : tous les mots tapés doivent apparaître ; les noms qui commencent par la recherche d'abord,
        /// les casques avant les écouteurs.</summary>
        public static List<CasqueAutoEq> Chercher(List<CasqueAutoEq> liste, string texte, int max = 8)
        {
            string q = Simple(texte);
            if (q.Length < 2 || liste == null) return new List<CasqueAutoEq>();
            var mots = q.Split(' ');
            return liste.Select(c => new { c, n = Simple(c.Nom) })
                .Where(x => mots.All(w => x.n.Contains(w)))
                .OrderBy(x => x.n.StartsWith(q) ? 0 : 1).ThenBy(x => x.c.IntraAuriculaire ? 1 : 0).ThenBy(x => x.n.Length)
                .Take(max).Select(x => x.c).ToList();
        }

        /// <summary>Filtres d'un fichier ParametricEQ.txt d'AutoEq : seules les lignes « Filter n: ON PK|LSC|HSC … » dans
        /// leurs bornes sont gardées (le reste est ignoré). Null si le fichier ne contient aucun filtre valable.</summary>
        public static List<FiltreAudio> LireEq(string texte)
        {
            var l = new List<FiltreAudio>();
            var c = CultureInfo.InvariantCulture;
            foreach (var brute in (texte ?? "").Split('\n'))
            {
                var m = LigneFiltre.Match(brute.TrimEnd('\r'));
                if (!m.Success) continue;
                double fc = double.Parse(m.Groups[2].Value, c), g = double.Parse(m.Groups[3].Value, c), q = double.Parse(m.Groups[4].Value, c);
                if (fc < 20 || fc > 20000 || Math.Abs(g) > 20 || q < 0.1 || q > 20) continue;
                l.Add(new FiltreAudio { Type = m.Groups[1].Value, Fc = (int)Math.Round(fc), Gain = Math.Round(g, 1), Q = Math.Round(q, 2) });
                if (l.Count >= FiltresMax) break;
            }
            return l.Count > 0 ? l : null;
        }

        // mots qui ne disent rien du modèle (noms génériques de Windows, de pilotes, de puces audio)
        private static readonly string[] Generiques = { "casque", "headphones", "headphone", "headset", "ecouteurs", "speakers", "speaker", "haut", "parleurs",
            "haut-parleurs", "audio", "usb", "device", "peripherique", "high", "definition", "realtek", "r", "nvidia", "amd", "intel", "smart", "sound", "technology",
            "digital", "output", "sortie", "hands", "free", "hands-free", "stereo", "game", "chat", "for", "pour", "de", "the", "le", "la", "with", "microphone", "mic",
            "2nd", "gen", "virtual", "display", "hdmi", "displayport", "default", "par", "defaut", "voicemeeter", "cable", "vb", "input" };

        /// <summary>
        /// Démarrage rapide : ce que le nom de la sortie de Windows dit du modèle du casque (« Casque (Arctis Nova Pro
        /// Wireless) » → « arctis nova pro wireless »), ou "" s'il ne dit rien (« Haut-parleurs (Realtek(R) Audio) »).
        /// </summary>
        public static string RequeteDepuisSortie(string libelle)
        {
            var mots = Simple(libelle).Split(' ').Where(m => m.Length > 0 && !Generiques.Contains(m)).ToList();
            // il faut des lettres, et un mot d'au moins 3 lettres ou un numéro de modèle (« HD 650 ») : sinon ce n'est rien
            if (!mots.Any(m => m.Any(char.IsLetter)) || !mots.Any(m => (m.Length >= 3 && m.Any(char.IsLetter)) || m.Any(char.IsDigit))) return "";
            return string.Join(" ", mots.Take(6));
        }

        /// <summary>Modèles proposés pour cette sortie de Windows (au plus « max »), en retirant des mots de la fin si rien
        /// ne correspond (« Arctis Nova Pro Wireless Base » → « Arctis Nova Pro Wireless »).</summary>
        public static List<CasqueAutoEq> Deviner(List<CasqueAutoEq> liste, string libelle, int max = 3)
        {
            var mots = RequeteDepuisSortie(libelle).Split(' ').Where(m => m != "").ToList();
            while (mots.Count > 0)
            {
                string q = string.Join(" ", mots);
                if (q.Length >= 3)
                {
                    var l = Chercher(liste, q, max);
                    if (l.Count > 0) return l;
                }
                mots.RemoveAt(mots.Count - 1);
                if (mots.Count == 1 && mots[0].Length < 4) break;   // un seul mot court : trop vague
            }
            return new List<CasqueAutoEq>();
        }

        public static string AdresseEq(CasqueAutoEq c)
        {
            if (c == null || !CheminValide(c.Chemin)) return null;
            string dernier = c.Chemin.Substring(c.Chemin.LastIndexOf('/') + 1);
            return Base + c.Chemin + "/" + dernier + "%20ParametricEQ.txt";
        }

        private static string FichierIndex { get { return Path.Combine(Chemins.Local, "casques-autoeq.md"); } }

        private static string Telecharger(string adresse, int maxOctets)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("NettoyagePC/" + Appli.Version);
                using (var rep = client.GetAsync(adresse, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                {
                    if (!rep.IsSuccessStatusCode) throw new InvalidOperationException("La base des casques ne répond pas (code " + (int)rep.StatusCode + ") : réessaie plus tard.");
                    using (var s = rep.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (var m = new MemoryStream())
                    {
                        var t = new byte[16384]; int n;
                        while ((n = s.Read(t, 0, t.Length)) > 0) { m.Write(t, 0, n); if (m.Length > maxOctets) throw new InvalidOperationException("Réponse inattendue de la base des casques."); }
                        return Encoding.UTF8.GetString(m.ToArray());
                    }
                }
            }
        }

        private static List<CasqueAutoEq> enMemoire;

        /// <summary>Liste des casques : gardée en mémoire et sur le disque (deux semaines), sinon téléchargée. Si le
        /// téléchargement échoue, l'ancienne liste sert encore.</summary>
        public static List<CasqueAutoEq> Index()
        {
            if (enMemoire != null) return enMemoire;
            string md = null;
            try { if (File.Exists(FichierIndex) && (DateTime.Now - File.GetLastWriteTime(FichierIndex)).TotalDays < 14) md = File.ReadAllText(FichierIndex); } catch { }
            if (md == null)
            {
                try
                {
                    md = Telecharger(Base + "INDEX.md", 8 * 1024 * 1024);
                    try { Directory.CreateDirectory(Chemins.Local); File.WriteAllText(FichierIndex, md); } catch { }
                }
                catch
                {
                    try { if (File.Exists(FichierIndex)) md = File.ReadAllText(FichierIndex); } catch { }
                    if (md == null) throw;
                }
            }
            var l = LireIndex(md);
            if (l.Count == 0) throw new InvalidOperationException("Réponse inattendue de la base des casques.");
            return enMemoire = l;
        }

        /// <summary>Correction du casque choisi (téléchargée, relue strictement).</summary>
        public static List<FiltreAudio> Correction(CasqueAutoEq c)
        {
            string a = AdresseEq(c);
            if (a == null) throw new InvalidOperationException("Casque inconnu.");
            var f = LireEq(Telecharger(a, 64 * 1024));
            if (f == null) throw new InvalidOperationException("La correction de ce casque est illisible : choisis une autre mesure.");
            return f;
        }
    }
}
