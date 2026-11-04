// Nettoyage PC - rapport de problème : version, Windows, réglages utiles et dernières erreurs du journal de démarrage,
// SANS données personnelles (nom d'utilisateur, nom du PC, adresses e-mail et chemins personnels sont masqués ;
// ni clé de licence, ni fichiers). Le rapport est toujours montré avant l'envoi, qui se fait par l'e-mail de l'utilisateur.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal static class RapportBug
    {
        public const string Adresse = "nettoyagepcai@gmail.com";

        public static string Creer(string description)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Nettoyage PC " + Appli.Version + " — rapport de problème");
            sb.AppendLine("Date : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            if (!string.IsNullOrWhiteSpace(description)) { sb.AppendLine(); sb.AppendLine("Ce qui s'est passé :"); sb.AppendLine(description.Trim()); }
            sb.AppendLine();
            string edition = ""; try { edition = InfosPc.Edition; } catch { }
            sb.AppendLine("Windows : " + edition + " (build " + Natif.Build + ", " + (Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits") + ")");
            try { sb.AppendLine("Mémoire vive : " + Math.Round(InfosPc.RamGo) + " Go · processeurs logiques : " + Environment.ProcessorCount); } catch { }
            try
            {
                var p = Parametres.Lire();
                sb.AppendLine("Langue : " + p.Langue + " · thème : " + p.Theme + " · style : " + p.Style + (p.ProfilUsage != "" ? " · profil : " + p.ProfilUsage : "") + (p.ModeSimple ? " · mode simple" : ""));
            }
            catch { }
            sb.AppendLine("Premium : " + (Licence.Premium ? (Licence.EnEssai ? "essai" : "oui") : "non"));
            sb.AppendLine();
            var erreurs = Erreurs(40);
            sb.AppendLine(erreurs.Count == 0 ? "Aucune erreur dans le journal ces 30 derniers jours." : "Dernières erreurs (" + erreurs.Count + ") :");
            foreach (var e in erreurs) sb.AppendLine(e);
            return Anonymiser(sb.ToString());
        }

        /// <summary>Lignes « ERREUR » du journal de démarrage (30 derniers jours), les plus récentes à la fin.</summary>
        public static List<string> Erreurs(int max)
        {
            var l = new List<string>();
            var limite = DateTime.Now.AddDays(-30);
            foreach (var f in new[] { JournalDemarrage.Fichier + ".ancien", JournalDemarrage.Fichier })
            {
                try
                {
                    if (!File.Exists(f)) continue;
                    foreach (var ligne in File.ReadAllLines(f))
                    {
                        if (ligne.Length < 26 || ligne.IndexOf("ERREUR (", 19, StringComparison.Ordinal) < 0) continue;
                        DateTime d;
                        if (!DateTime.TryParseExact(ligne.Substring(0, 19), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) || d < limite) continue;
                        // la pile d'appels est longue : on garde le début, qui suffit pour retrouver l'endroit
                        l.Add(ligne.Length > 700 ? ligne.Substring(0, 700) + "…" : ligne);
                    }
                }
                catch { }
            }
            return l.Skip(Math.Max(0, l.Count - max)).ToList();
        }

        /// <summary>Masque ce qui pourrait identifier la personne : nom d'utilisateur, nom du PC, adresses e-mail, dossiers personnels.</summary>
        public static string Anonymiser(string t)
        {
            if (string.IsNullOrEmpty(t)) return t ?? "";
            t = Regex.Replace(t, @"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", m => m.Value.Equals(Adresse, StringComparison.OrdinalIgnoreCase) ? m.Value : "<e-mail>");
            t = Regex.Replace(t, @"([A-Za-z]:\\(?:Users|Utilisateurs|Documents and Settings)\\)[^\\/:*?""<>|\r\n]+", "$1<utilisateur>", RegexOptions.IgnoreCase);
            t = Regex.Replace(t, @"(/home/)[^/\s]+", "$1<utilisateur>");
            foreach (var nom in new[] { Environment.UserName, Environment.MachineName })
                if (!string.IsNullOrEmpty(nom) && nom.Length >= 3) t = Regex.Replace(t, @"\b" + Regex.Escape(nom) + @"\b", nom == Environment.MachineName ? "<pc>" : "<utilisateur>", RegexOptions.IgnoreCase);
            return t;
        }
    }
}
