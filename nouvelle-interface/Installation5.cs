// Nettoyage PC 5 - aperçu ou programme installé ?
// Tant que la version 4 est le programme installé (Program Files) et que ce fichier n'est pas lui, la version 5 est un
// aperçu : l'entretien automatique, le gardien et les mises à jour restent ceux de la version 4 (les tâches de Windows et
// le lancement automatique doivent pointer vers le programme installé, pas vers un fichier d'aperçu qu'on peut déplacer).
// Une fois la version 5 installée à sa place (ou utilisée seule, en version portable), tout fonctionne normalement.
using System;
using System.IO;

namespace NettoyagePC.Nouveau
{
    internal static class Installation5
    {
        private static bool? apercu;

        /// <summary>Chemin du programme installé (Paramètres > Applications), ou null.</summary>
        public static string ProgrammeInstalle()
        {
            if (!OperatingSystem.IsWindows()) return null;
            try
            {
                string dossier = null;
                foreach (var vue in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
                    using (var b = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, vue))
                    using (var k = b.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\NettoyagePC"))
                        if (k != null && (dossier = k.GetValue("InstallLocation") as string) != null) break;
                if (string.IsNullOrWhiteSpace(dossier)) return null;
                string exe = Path.Combine(dossier.Trim().Trim('"'), "NettoyagePC.exe");
                return File.Exists(exe) ? exe : null;
            }
            catch { return null; }
        }

        /// <summary>Vrai si un autre programme Nettoyage PC est installé et que celui-ci n'est qu'un aperçu.</summary>
        public static bool Apercu
        {
            get
            {
                if (apercu.HasValue) return apercu.Value;
                bool r = false;
                try
                {
                    string installe = ProgrammeInstalle();
                    if (installe != null) r = !string.Equals(Path.GetFullPath(installe), Path.GetFullPath(Chemins.Exe), StringComparison.OrdinalIgnoreCase);
                }
                catch { }
                apercu = r;
                return r;
            }
        }
    }
}
