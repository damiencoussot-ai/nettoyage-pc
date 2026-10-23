// Nettoyage PC - sécurité des chemins, écritures sûres, lancement de programmes
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace NettoyagePC
{
    /// <summary>Dossiers du système et liens : jamais de restauration, de déplacement ou de suppression dedans.</summary>
    internal static class Protection
    {
        public static IEnumerable<string> RacinesProtegees()
        {
            var l = new List<string>
            {
                Environment.GetEnvironmentVariable("windir"), Environment.GetEnvironmentVariable("ProgramFiles"),
                Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramData")
            };
            string sd = Environment.GetEnvironmentVariable("SystemDrive");
            if (!string.IsNullOrEmpty(sd)) { l.Add(sd + "\\$Recycle.Bin"); l.Add(sd + "\\System Volume Information"); }
            return l.Where(x => !string.IsNullOrEmpty(x)).Select(x => x.TrimEnd('\\'));
        }

        public static bool SousRacine(string chemin, string racine)
        {
            string c = chemin.TrimEnd('\\'), r = racine.TrimEnd('\\');
            return c.Equals(r, StringComparison.OrdinalIgnoreCase) || c.StartsWith(r + "\\", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Vrai si le chemin est dans un dossier du système ou passe par une jonction / un lien symbolique.</summary>
        public static bool CheminProtege(string chemin)
        {
            string plein;
            try { plein = Path.GetFullPath(chemin); } catch { return true; }
            foreach (var r in RacinesProtegees()) if (SousRacine(plein, r)) return true;
            string d = Path.GetDirectoryName(plein);
            while (!string.IsNullOrEmpty(d) && d.Length > 3)
            {
                if (Natif.EstLien(d)) return true;
                d = Path.GetDirectoryName(d);
            }
            return false;
        }

        /// <summary>Aucun lien (jonction, lien symbolique, lien physique) sur le chemin ni sur le fichier.</summary>
        public static bool CheminSansLien(string chemin)
        {
            string plein;
            try { plein = Path.GetFullPath(chemin); } catch { return false; }
            string d = Path.GetDirectoryName(plein);
            while (!string.IsNullOrEmpty(d))
            {
                if (Natif.EstLien(d)) return false;
                string p = Path.GetDirectoryName(d);
                if (string.IsNullOrEmpty(p) || p == d) break;
                d = p;
            }
            if (File.Exists(plein) && (Natif.EstLien(plein) || Natif.NombreLiens(plein) > 1)) return false;
            return true;
        }

        /// <summary>Fichier du dossier racine indiqué, sans lien entre les deux (pour une suppression sûre).</summary>
        public static bool DansRacineSansLien(string chemin, string racine)
        {
            string r = racine.TrimEnd('\\');
            string d = Path.GetDirectoryName(chemin);
            while (!string.IsNullOrEmpty(d))
            {
                if (Natif.EstLien(d)) return false;
                if (d.TrimEnd('\\').Equals(r, StringComparison.OrdinalIgnoreCase)) return true;
                if (d.Length <= r.Length) return false;
                d = Path.GetDirectoryName(d);
            }
            return false;
        }
    }

    /// <summary>
    /// Écritures dans Documents\NettoyagePC. Ce dossier est modifiable par tout programme de l'utilisateur :
    /// quand le moteur a l'autorisation administrateur, un programme malveillant pourrait y placer un lien vers
    /// un fichier de Windows pour le faire écraser. Chaque écriture vérifie donc le chemin, écrit dans un fichier
    /// NEUF, contrôle son emplacement réel, puis remplace l'ancien.
    /// </summary>
    internal static class FichierSur
    {
        public static bool Ecrire(string chemin, string texte, bool ajouter = false)
        {
            string plein, tmp = null;
            try
            {
                plein = Path.GetFullPath(chemin);
                string dossier = Path.GetDirectoryName(plein);
                Directory.CreateDirectory(dossier);
                if (!Protection.CheminSansLien(plein)) return false;
                if (ajouter && File.Exists(plein))
                {
                    string ancien = File.ReadAllText(plein, Encoding.UTF8);
                    if (ancien.Length > 0 && !ancien.EndsWith("\n")) ancien += "\r\n";
                    texte = ancien + texte;
                }
                tmp = Path.Combine(dossier, ".np-" + Guid.NewGuid().ToString("N") + ".tmp");
                using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (Natif.EstWindows)
                    {
                        string reel = Natif.CheminReel(fs.SafeFileHandle);
                        if (reel != null && !Path.GetDirectoryName(reel).Equals(dossier.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)
                            && !Path.GetDirectoryName(reel).Equals(dossier, StringComparison.OrdinalIgnoreCase))
                        {
                            // Le dossier a été remplacé par un lien entre-temps : on n'écrit rien
                            fs.Dispose();
                            try { File.Delete(tmp); } catch { }
                            return false;
                        }
                    }
                    var enc = new UTF8Encoding(true);
                    byte[] pre = enc.GetPreamble(); fs.Write(pre, 0, pre.Length);
                    byte[] o = enc.GetBytes(texte); fs.Write(o, 0, o.Length);
                }
                if (File.Exists(plein)) File.Replace(tmp, plein, null); else File.Move(tmp, plein);
                return true;
            }
            catch
            {
                try { if (tmp != null && File.Exists(tmp)) File.Delete(tmp); } catch { }
                return false;
            }
        }

        public static bool AjouterLigne(string chemin, string ligne) { return Ecrire(chemin, ligne + "\r\n", true); }

        /// <summary>
        /// Écriture faite par l'interface (toujours sans droits administrateur) : écriture sûre, sinon écriture simple.
        /// Sans autorisation administrateur, un lien ne permet d'écrire que là où l'utilisateur peut déjà écrire.
        /// </summary>
        public static bool EcrireInterface(string chemin, string texte)
        {
            if (Ecrire(chemin, texte)) return true;
            if (Appli.EstAdmin) return false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(chemin)));
                File.WriteAllText(chemin, texte, new UTF8Encoding(true));
                return true;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("écriture de " + Path.GetFileName(chemin), ex); return false; }
        }
    }

    /// <summary>Lancement de programmes et de commandes.</summary>
    internal static class Programmes
    {
        [System.Runtime.InteropServices.DllImport("shell32.dll")] private static extern void SHChangeNotify(int evenement, uint options, IntPtr a, IntPtr b);

        /// <summary>Demande à Windows de relire les icônes (Bureau, menu Démarrer, barre des tâches) : après un changement
        /// de logo, sinon Windows garde l'ancienne image en cache. Sans droits administrateur, sans fenêtre.</summary>
        public static void RafraichirIcones()
        {
            if (!Natif.EstWindows) return;
            try
            {
                string ie4 = Path.Combine(Chemins.Systeme32, "ie4uinit.exe");
                if (File.Exists(ie4)) Executer(ie4, "-show", 20000);
                SHChangeNotify(0x08000000, 0x1000, IntPtr.Zero, IntPtr.Zero);   // SHCNE_ASSOCCHANGED, SHCNF_FLUSH
            }
            catch { }
        }

        public static string Ps { get { return Path.Combine(Chemins.Systeme32, "WindowsPowerShell", "v1.0", "powershell.exe"); } }

        /// <summary>Exécute un programme et renvoie sa sortie (texte), avec un délai maximum.</summary>
        public static string Executer(string exe, string arguments, int delaiMs = 60000, Encoding encodage = null)
        {
            int code;
            return Executer(exe, arguments, delaiMs, encodage, out code);
        }

        public static string Executer(string exe, string arguments, int delaiMs, Encoding encodage, out int code)
        {
            code = -1;
            try
            {
                var psi = new ProcessStartInfo(exe, arguments)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = encodage ?? Encodages.Ansi, StandardErrorEncoding = encodage ?? Encodages.Ansi
                };
                using (var p = Process.Start(psi))
                {
                    var sortie = new StringBuilder();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) lock (sortie) sortie.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (sortie) sortie.AppendLine(e.Data); };
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    if (!p.WaitForExit(delaiMs)) { try { p.Kill(); } catch { } return sortie.ToString(); }
                    p.WaitForExit();
                    code = p.ExitCode;
                    return sortie.ToString();
                }
            }
            catch { return ""; }
        }

        /// <summary>Exécute un programme et transmet chaque ligne de sortie (pour les outils longs : DISM, SFC...).</summary>
        public static int ExecuterLignes(string exe, string arguments, Action<string> ligne, Encoding encodage = null, int delaiMs = -1)
        {
            try
            {
                var psi = new ProcessStartInfo(exe, arguments)
                {
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = encodage ?? Encodages.Ansi, StandardErrorEncoding = encodage ?? Encodages.Ansi
                };
                using (var p = Process.Start(psi))
                {
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) ligne(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null && e.Data.Trim().Length > 0) ligne(e.Data); };
                    p.BeginOutputReadLine(); p.BeginErrorReadLine();
                    if (delaiMs > 0) { if (!p.WaitForExit(delaiMs)) { try { p.Kill(); } catch { } return -1; } }
                    p.WaitForExit();
                    return p.ExitCode;
                }
            }
            catch (Exception ex) { ligne("  (outil indisponible : " + ex.Message + ")"); return -1; }
        }

        /// <summary>Ouvre un lien, un document ou un programme avec les droits normaux de l'utilisateur.</summary>
        public static void Ouvrir(string cible, string arguments = null)
        {
            if (string.IsNullOrEmpty(cible)) return;
            try
            {
                if (Appli.EstAdmin && Natif.EstWindows)
                {
                    // Lancé par l'Explorateur : le programme n'hérite pas de l'autorisation administrateur
                    if (string.IsNullOrEmpty(arguments)) { Process.Start(new ProcessStartInfo(Path.Combine(Chemins.Windows, "explorer.exe"), "\"" + cible + "\"") { UseShellExecute = false }); return; }
                }
                var psi = new ProcessStartInfo(cible) { UseShellExecute = true };
                if (!string.IsNullOrEmpty(arguments)) psi.Arguments = arguments;
                Process.Start(psi);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("ouverture de " + cible, ex); }
        }

        /// <summary>Montre un fichier dans l'Explorateur (sélectionné), sans l'ouvrir.</summary>
        public static void MontrerDansExplorateur(string fichier)
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(Chemins.Windows, "explorer.exe"), "/select,\"" + fichier + "\"") { UseShellExecute = false }); }
            catch { }
        }

        private static readonly HashSet<string> extensionsExecutables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { ".exe", ".com", ".bat", ".cmd", ".ps1", ".psm1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".msi", ".msp", ".scr", ".pif", ".lnk", ".hta", ".cpl", ".reg", ".jar", ".appx", ".msix", ".msc", ".url" };

        /// <summary>Ouvre un fichier ; un fichier qui s'exécute est seulement montré dans l'Explorateur.</summary>
        public static void OuvrirFichierSur(string chemin)
        {
            if (string.IsNullOrEmpty(chemin) || !File.Exists(chemin)) return;
            if (extensionsExecutables.Contains(Path.GetExtension(chemin))) { MontrerDansExplorateur(chemin); return; }
            Ouvrir(chemin);
        }

        /// <summary>Lance une ligne de commande avec les droits normaux (cmd /c, fenêtre cachée).</summary>
        public static void LancerCommande(string commande)
        {
            try { Process.Start(new ProcessStartInfo(Path.Combine(Chemins.Systeme32, "cmd.exe"), "/s /c \"" + commande + "\"") { UseShellExecute = false, CreateNoWindow = true }); }
            catch (Exception ex) { JournalDemarrage.Erreur("commande", ex); }
        }

        public static bool ExisteSysteme(string nomExe) { return File.Exists(Path.Combine(Chemins.Systeme32, nomExe)); }
    }
}
