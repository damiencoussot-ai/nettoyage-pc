// Essai réel de l'installation de VB-Cable par Velox, sur un vrai Windows (GitHub, voir .github/workflows/essai-vbcable.yml) :
// téléchargement du paquet officiel, choix de l'installateur, signature de VB-Audio vérifiée par Windows, installation
// silencieuse en administrateur, pilote détecté, copie gardée pour le retirer. Ne tourne que si VELOX_VBCABLE_REEL=1
// (ailleurs, l'essai ne fait rien : il installerait un pilote sur la machine).
using System;
using System.IO;
using System.Threading;
using NettoyagePC;
using Xunit;
using Xunit.Abstractions;

namespace Velox.Tests
{
    public class VbCableReel
    {
        private readonly ITestOutputHelper sortie;
        public VbCableReel(ITestOutputHelper sortie) { this.sortie = sortie; }

        private static bool Actif { get { return Environment.GetEnvironmentVariable("VELOX_VBCABLE_REEL") == "1" && OperatingSystem.IsWindows(); } }

        [Fact]
        public void Installation_complete_sur_Windows()
        {
            if (!Actif) return;
            sortie.WriteLine("Windows : " + InstallationApo.Architecture + ", pilote déjà là : " + InstallationCable.PiloteInstalle());

            int dernier = -1;
            string dossier = InstallationCable.Telecharger(p => { if (p / 25 != dernier / 25) sortie.WriteLine("téléchargé " + p + " %"); dernier = p; }, CancellationToken.None);
            sortie.WriteLine("paquet décompressé : " + dossier);
            foreach (string f in Directory.GetFiles(dossier)) sortie.WriteLine("  " + Path.GetFileName(f) + " (" + new FileInfo(f).Length + " o)");
            Assert.Contains(Path.DirectorySeparatorChar + "NettoyagePC-cable-", dossier);

            // ce que fait le processus administrateur lancé par Velox (le runner GitHub est déjà administrateur)
            int code = InstallationCable.InstallerEnAdmin(new[] { "installer-cable", dossier });
            sortie.WriteLine("installer-cable : code " + code + " ; pilote installé : " + InstallationCable.PiloteInstalle());
            string journal = JournalDemarrage.Fichier;
            if (File.Exists(journal)) sortie.WriteLine("journal : " + File.ReadAllText(journal));
            Assert.Equal(0, code);
            Assert.True(InstallationCable.PiloteInstalle(), "pilote « VB-Audio Virtual Cable » introuvable après l'installation");

            // la copie gardée sert à le retirer (désinstalleur de Velox, bouton « Remove Driver »)
            string garde = Path.Combine(InstallationCable.Dossier, InstallationCable.Installateur(InstallationApo.Architecture, new[] { "VBCABLE_Setup.exe", "VBCABLE_Setup_x64.exe", "VBCABLE_Setup_ARM64.exe" }));
            sortie.WriteLine("copie gardée : " + garde);
            Assert.True(File.Exists(garde), "copie de l'installateur absente de " + InstallationCable.Dossier);
            try { Directory.Delete(dossier, true); } catch { }
        }
    }
}
