// Velox - rapport de problème : version, Windows, réglages utiles et dernières erreurs du journal de démarrage,
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

        public static string Creer(string description) { return Creer(description, null); }

        /// <summary>« son » : la partie Son déjà relevée (SectionSon), pour ne pas tout relire à chaque lettre tapée.</summary>
        public static string Creer(string description, string son)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Velox " + Appli.Version + " — rapport de problème");
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
            sb.Append(son ?? SectionSon());
            sb.AppendLine();
            var erreurs = Erreurs(40);
            sb.AppendLine(erreurs.Count == 0 ? "Aucune erreur dans le journal ces 30 derniers jours." : "Dernières erreurs (" + erreurs.Count + ") :");
            foreach (var e in erreurs) sb.AppendLine(e);
            return Anonymiser(sb.ToString());
        }

        /// <summary>
        /// État du son (Studio son), pour diagnostiquer à distance : sortie par défaut, Equalizer APO, égaliseur, autres
        /// traitements, 7.1 virtuel, câble, Pas boostés et jeu détecté. Seulement des noms d'appareils et des réglages.
        /// </summary>
        public static string SectionSon()
        {
            var sb = new StringBuilder();
            try { Son(sb); } catch (Exception ex) { sb.AppendLine("Son : illisible (" + ex.Message + ")"); }
            return sb.ToString();
        }

        private static void Son(StringBuilder sb)
        {
            if (!Natif.EstWindows) { sb.AppendLine("Son : Windows uniquement."); return; }
            sb.AppendLine("Son (Studio son) :");
            var fen = AudioWindows.Lister();
            string defaut = AudioWindows.ParDefaut();
            var sd = fen.FirstOrDefault(w => w.Id == defaut);
            sb.AppendLine("- Sortie par défaut : " + (sd != null ? sd.Nom : "aucune"));
            sb.AppendLine("- Sorties actives : " + (fen.Count == 0 ? "aucune" : string.Join(" ; ", fen.Select(w => w.Nom))));
            var e = AudioJeu.Etat();
            var apo = e.Present ? AudioJeu.Sorties() : new List<SortieAudio>();
            sb.AppendLine("- Equalizer APO : " + (e.Present ? "installé" + (e.Include ? ", relié" : ", PAS relié (ligne Include absente)") + " · sur : " + (apo.Count == 0 ? "aucune sortie" : string.Join(" ; ", apo.Select(x => x.Libelle))) : "non installé"));
            if (e.Autres.Count > 0) sb.AppendLine("- Autres traitements dans Equalizer APO : " + string.Join(", ", e.Autres));
            var r = AudioJeu.Lire();
            sb.AppendLine("- Égaliseur : " + (r.Actif ? "actif" : "coupé") + " · profil " + AudioJeu.NomProfil(r) + " · force " + r.Intensite + " %"
                + (r.AutoJeu ? " · Auto jeux" + (r.ProfilParJeu ? " + profil par jeu" : "") : "") + " · moteur " + r.Moteur + " · sortie visée " + r.Cible
                + (r.CalibActive ? " · casque calibré : " + r.CalibNom : " · sans calibration") + (r.Oreilles ? " · compensation des oreilles" : "")
                + (r.CoupeBas > 0 ? " · coupe-explosions " + r.CoupeBas + " Hz" : ""));
            try
            {
                var h = HeSuVi.Etat();
                if (h.Present) sb.AppendLine("- Son 7.1 virtuel (HeSuVi) : " + (h.Actif && h.Inclus ? "actif, " + h.Modele : "coupé"));
            }
            catch { }
            var cables = MoteurSon.Cables(fen);
            sb.AppendLine("- VB-Cable : " + (cables.Count == 0 ? "non installé" : string.Join(" ; ", cables.Select(c => c.Nom + " = « " + (fen.FirstOrDefault(w => w.Id == c.Sortie)?.Nom ?? "?") + " »"))));
            var studio = fen.Where(w => w.Nom == NomStudio.Nom || w.Nom.StartsWith(NomStudio.Nom + " (", StringComparison.Ordinal)).ToList();
            if (studio.Count > 0) sb.AppendLine("- Nom « Velox Studio » sur : " + string.Join(" ; ", studio.Select(w => w.Nom)) + (studio.Any(w => MoteurSon.EstCable(w.Nom)) ? " (le câble : bien)" : " (PAS le câble : le jeu contourne Pas boostés s'il sort dessus)"));
            var m = MoteurSon.Lire();
            var et = MoteurSon.LireEtat();
            sb.AppendLine("- Pas boostés : " + (m.Actif ? "allumé" : "coupé") + " · force " + m.Force + " · ton arme " + m.Arme + (m.JeuxSeulement ? " · seulement en jeu" : "")
                + (m.ParJeu.Count > 0 ? " · par jeu : " + string.Join(", ", m.ParJeu.Select(kv => kv.Key + "=" + kv.Value[0] + "/" + kv.Value[1])) : "")
                + (m.Actif ? " · moteur " + (MoteurSon.EnMarche() ? "lancé" : "PAS lancé") + (et.Marche ? ", son en cours, " + et.Canaux + " canaux, retard " + et.LatenceMs + " ms" + (et.Jeu ? ", traite le son" : ", en attente d'un jeu (son non traité)") : "") + (et.Erreur != "" ? ", erreur : " + et.Erreur : "") : ""));
            string jeu = null; try { jeu = AudioJeu.JeuDetecte(); } catch { }
            sb.AppendLine("- Jeu reconnu en cours : " + (string.IsNullOrEmpty(jeu) ? "aucun" : jeu));
            // programmes qui ressemblent à un jeu de tir mais que Velox ne reconnaît pas (pour ajouter leur nom)
            var possibles = new List<string>();
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcesses())
                    using (p)
                    {
                        string n = p.ProcessName + ".exe";
                        if (Regex.IsMatch(n, @"cod|warzone|blackops|modernwarfare|valorant|cs2|apex|fortnite|overwatch|battlefield|bf\d|tarkov|rainbow|pubg|deltaforce|finals", RegexOptions.IgnoreCase) && !AudioJeu.EstJeu(n) && !possibles.Contains(n)) possibles.Add(n);
                    }
            }
            catch { }
            if (possibles.Count > 0) sb.AppendLine("- Jeux possibles non reconnus : " + string.Join(", ", possibles.Take(10)));
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
