// Nettoyage PC - mises à jour automatiques des applications (Premium) : une fois par semaine, une tâche de Windows
// lance winget (l'outil officiel de Microsoft) pour mettre à jour les applications, sauf celles que tu as exclues
// (« épinglées » dans winget : elles restent dans leur version). Sans interface : partagé v4 / v5.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class AppAMettreAJour
    {
        public string Nom = "", Id = "", Version = "", Disponible = "";
        public bool Exclue;
    }

    internal static class MajAppsAuto
    {
        public const string Tache = "Nettoyage PC - applications (Premium)";
        public static string FichierDerniere { get { return Path.Combine(Chemins.Local, "majapps-derniere.txt"); } }

        public static bool Active { get { return Moteur.TacheExiste(Tache); } }

        /// <summary>Date de la dernière mise à jour automatique (null si jamais).</summary>
        public static DateTime? Derniere()
        {
            try { DateTime d; if (File.Exists(FichierDerniere) && DateTime.TryParse(File.ReadAllText(FichierDerniere).Trim(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d)) return d; } catch { }
            return null;
        }

        public static bool IdValide(string id) { return !string.IsNullOrEmpty(id) && id.Length <= 128 && Regex.IsMatch(id, @"^[\w][\w.\-+]*$"); }

        /// <summary>Applications qui ont une mise à jour (winget upgrade), avec celles déjà exclues. null si winget est absent.</summary>
        public static List<AppAMettreAJour> Lister(out string erreur)
        {
            erreur = null;
            string winget = InfosPc.Winget();
            if (winget == null) { erreur = Langue.T("winget (Gestionnaire de package Windows) est introuvable. Installe « App Installer » depuis le Microsoft Store."); return null; }
            var l = Tableau(Programmes.Executer(winget, "upgrade --include-pinned --accept-source-agreements --disable-interactivity", 180000, Encoding.UTF8))
                .Where(c => c.Length >= 4 && IdValide(c[1])).Select(c => new AppAMettreAJour { Nom = c[0], Id = c[1], Version = c[2], Disponible = c[3] }).ToList();
            var epinglees = new HashSet<string>(Tableau(Programmes.Executer(winget, "pin list --accept-source-agreements --disable-interactivity", 60000, Encoding.UTF8))
                .Where(c => c.Length >= 2).Select(c => c[1]), StringComparer.OrdinalIgnoreCase);
            foreach (var a in l) a.Exclue = epinglees.Contains(a.Id);
            return l;
        }

        /// <summary>Exclure (épingler) ou réinclure une application. Retourne vrai si winget a accepté.</summary>
        public static bool Exclure(string id, bool exclure)
        {
            string winget = InfosPc.Winget();
            if (winget == null || !IdValide(id)) return false;
            int c;
            Programmes.Executer(winget, (exclure ? "pin add" : "pin remove") + " --id \"" + id + "\" --exact --accept-source-agreements --disable-interactivity", 60000, Encoding.UTF8, out c);
            return c == 0;
        }

        /// <summary>
        /// Lit un tableau de winget (en-tête, ligne de tirets, lignes) d'après la position des colonnes de l'en-tête.
        /// Fonctionne quelle que soit la langue de Windows (seules les positions comptent).
        /// </summary>
        public static List<string[]> Tableau(string sortie)
        {
            var res = new List<string[]>();
            // (winget efface sa barre de progression avec \r et \b : on garde ce qui suit le dernier)
            var lignes = (sortie ?? "").Split('\n').Select(x => { x = x.TrimEnd('\r'); int r = x.LastIndexOf('\r'); if (r >= 0) x = x.Substring(r + 1); return Regex.Replace(x, @"^.*\x08", ""); }).ToList();
            int tirets = lignes.FindIndex(x => Regex.IsMatch(x.Trim(), @"^-{10,}$"));
            if (tirets < 1) return res;
            string entete = lignes[tirets - 1];
            var debuts = new List<int>();
            for (int i = 0; i < entete.Length; i++)
                if (entete[i] != ' ' && (i == 0 || entete[i - 1] == ' ')) debuts.Add(i);
            if (debuts.Count < 2) return res;
            for (int k = tirets + 1; k < lignes.Count; k++)
            {
                string l = lignes[k];
                if (l.Trim() == "" || Regex.IsMatch(l.Trim(), @"^\d+ (mise|upgrade|package|paquet|application)", RegexOptions.IgnoreCase)) break;
                var cols = new List<string>();
                for (int c = 0; c < debuts.Count; c++)
                {
                    int a = debuts[c], b = c + 1 < debuts.Count ? debuts[c + 1] : l.Length;
                    cols.Add(a < l.Length ? l.Substring(a, Math.Min(b, l.Length) - a).Trim() : "");
                }
                res.Add(cols.ToArray());
            }
            return res;
        }
    }
}
