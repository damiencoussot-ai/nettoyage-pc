// Velox - restes d'un programme désinstallé (onglet Applications) : une fois que son désinstalleur officiel a fini,
// recherche de ce qu'il a laissé (dossier du programme, réglages, raccourcis). Les restes ne sont jamais supprimés :
// ils partent à la corbeille, après accord. Les dossiers protégés (Program Files, ProgramData) sont seulement signalés.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal class Reste
    {
        public string Chemin = "", Genre = "";   // « Dossier de réglages », « Raccourci »…
        public double Taille;
        public bool Protege;             // dossier du système : seulement signalé, à retirer soi-même
    }

    internal static class Desinstalleur
    {
        /// <summary>Le programme est-il toujours inscrit dans Windows ? (liste des programmes relue)</summary>
        public static bool EncoreInstalle(ProgrammeInstalle p)
        {
            try { return ProgrammesInstalles.Lister().Any(x => x.Cle == p.Cle && x.Nom == p.Nom && x.Utilisateur == p.Utilisateur); }
            catch { return true; }
        }

        /// <summary>Nom simplifié pour reconnaître les dossiers : sans version, sans « (x64) ».</summary>
        public static string NomSimple(string nom)
        {
            string n = Regex.Replace(nom ?? "", @"\((x64|x86|64[- ]bit|32[- ]bit|64 bits|32 bits)[^)]*\)", "", RegexOptions.IgnoreCase);
            n = Regex.Replace(n, @"\s+v?\d+(\.\d+)+.*$", "");
            n = Regex.Replace(n, @"\s+(x64|x86)$", "", RegexOptions.IgnoreCase);
            return n.Trim(' ', '-', '_', '.');
        }

        private static readonly HashSet<string> NomsInterdits = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Microsoft", "Windows", "Packages", "Programs", "Temp", "Common Files", "Google", "Mozilla", "Adobe", "Intel", "AMD", "NVIDIA", "NVIDIA Corporation",
            "Apple", "Steam", "Epic Games", "Desktop", "Documents", "Downloads", "Pictures", "Music", "Videos", "Roaming", "Local", "LocalLow", "Application Data",
        };

        /// <summary>
        /// Restes d'un programme qui vient d'être désinstallé. Prudent exprès : seulement le dossier d'installation s'il existe
        /// encore, et les dossiers ou raccourcis qui portent EXACTEMENT le nom du programme. Jamais un dossier partagé
        /// (éditeur, Microsoft…), jamais un dossier personnel, jamais un dossier où un autre programme est installé.
        /// </summary>
        public static List<Reste> ChercherRestes(ProgrammeInstalle p, List<ProgrammeInstalle> autres)
        {
            var l = new List<Reste>();
            string nom = NomSimple(p.Nom);
            if (nom.Length < 4 || NomsInterdits.Contains(nom)) nom = "";
            Func<string, string> dossier = v => { try { return Environment.GetFolderPath((Environment.SpecialFolder)Enum.Parse(typeof(Environment.SpecialFolder), v)); } catch { return ""; } };
            string roaming = dossier("ApplicationData"), local = dossier("LocalApplicationData"), commun = dossier("CommonApplicationData"),
                   pf = dossier("ProgramFiles"), pf86 = dossier("ProgramFilesX86"), menu = dossier("Programs"), menuCommun = dossier("CommonPrograms"),
                   bureau = dossier("DesktopDirectory"), profil = dossier("UserProfile");
            var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string, string> ajouter = (chemin, genre) =>
            {
                try
                {
                    if (string.IsNullOrEmpty(chemin)) return;
                    string plein = Path.GetFullPath(chemin).TrimEnd('\\');
                    bool estDossier = Directory.Exists(plein);
                    if (!estDossier && !File.Exists(plein)) return;
                    if (plein.Length < 12 || !vus.Add(plein)) return;
                    // jamais la racine d'un dossier connu, ni un lien, ni un dossier où un autre programme est installé
                    foreach (string r in new[] { roaming, local, commun, pf, pf86, menu, menuCommun, bureau, profil })
                        if (r != "" && plein.Equals(r.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
                    if (Natif.EstLien(plein)) return;
                    if (estDossier && autres.Any(a => !string.IsNullOrEmpty(a.Dossier) && (a.Dossier.TrimEnd('\\') + "\\").StartsWith(plein + "\\", StringComparison.OrdinalIgnoreCase))) return;
                    bool protege = Protection.RacinesProtegees().Any(r => Protection.SousRacine(plein, r));
                    l.Add(new Reste { Chemin = plein, Genre = genre, Protege = protege, Taille = estDossier ? Math.Max(0, DetectionJeux.TailleDossier(plein)) : new FileInfo(plein).Length });
                }
                catch { }
            };
            string installe = (p.Dossier ?? "").Trim().Trim('"').TrimEnd('\\');
            if (installe != "" && Path.IsPathRooted(installe)) ajouter(installe, "Dossier du programme");
            if (nom != "")
            {
                foreach (string b in new[] { roaming, local, Path.Combine(local, "Programs") }) if (b != "") ajouter(Path.Combine(b, nom), "Réglages et données du programme");
                foreach (string b in new[] { commun, pf, pf86 }) if (b != "") ajouter(Path.Combine(b, nom), "Dossier du programme");
                foreach (string b in new[] { menu, menuCommun }) if (b != "") { ajouter(Path.Combine(b, nom), "Raccourcis du menu Démarrer"); ajouter(Path.Combine(b, nom + ".lnk"), "Raccourci du menu Démarrer"); }
                if (bureau != "") ajouter(Path.Combine(bureau, nom + ".lnk"), "Raccourci du Bureau");
            }
            return l;
        }

        /// <summary>Met un reste à la corbeille (récupérable). Faux si refusé ou protégé.</summary>
        public static bool Retirer(Reste r)
        {
            if (r.Protege) return false;
            try
            {
                if (!Corbeille.Envoyer(r.Chemin)) return false;
                return !Directory.Exists(r.Chemin) && !File.Exists(r.Chemin);
            }
            catch { return false; }
        }
    }
}
