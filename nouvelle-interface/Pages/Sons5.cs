// Velox 5 - petits sons du profil Gamer (coupés par défaut, réglage « Sons » du QG GAMER) : synthétisés dans le
// programme (aucun fichier, aucun téléchargement) et joués par Windows sans bloquer l'interface.
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace NettoyagePC.Nouveau
{
    internal static class Sons5
    {
        [DllImport("winmm.dll", SetLastError = true)] private static extern bool PlaySound(byte[] son, IntPtr module, uint options);
        private const uint SND_ASYNC = 0x0001, SND_NODEFAULT = 0x0002, SND_MEMORY = 0x0004;

        public static bool Actifs;
        private static byte[] boost, succes, niveau, enCours;   // gardés en mémoire pendant la lecture

        /// <summary>« Woosh » montant puis accord (lancement du mode jeu).</summary>
        public static void Boost() { Jouer(boost ?? (boost = Synthe(1.1, (t, d) => Balayage(t, 110, 880, 0.55) * Env(t, 0.01, 0.5) * 0.5 + Accord(t - 0.5, new[] { 440.0, 554.4, 659.3, 880.0 }) * Env(t - 0.5, 0.01, 0.6) * 0.45))); }

        /// <summary>Petit carillon (succès débloqué).</summary>
        public static void Succes() { Jouer(succes ?? (succes = Synthe(0.7, (t, d) => Note(t, 987.8, 0.0, 0.35) + Note(t, 1318.5, 0.11, 0.5)))); }

        /// <summary>Arpège montant (niveau supérieur).</summary>
        public static void Niveau() { Jouer(niveau ?? (niveau = Synthe(0.9, (t, d) => Note(t, 523.3, 0, 0.3) + Note(t, 659.3, 0.1, 0.3) + Note(t, 784.0, 0.2, 0.3) + Note(t, 1046.5, 0.3, 0.5)))); }

        private static byte[] alerte;
        /// <summary>Deux bips graves (surchauffe en jeu). Jouée même si les sons d'ambiance sont coupés : c'est une alerte.</summary>
        public static void Alerte()
        {
            if (!OperatingSystem.IsWindows() || !Habillage.Animations) return;
            try { if (alerte == null) alerte = Synthe(0.8, (t, d) => Note(t, 392.0, 0.0, 0.28) + Note(t, 392.0, 0.38, 0.28)); enCours = alerte; PlaySound(alerte, IntPtr.Zero, SND_ASYNC | SND_MEMORY | SND_NODEFAULT); } catch { }
        }

        private static void Jouer(byte[] son)
        {
            if (!Actifs || !Habillage.Animations || !OperatingSystem.IsWindows()) return;
            try { enCours = son; PlaySound(son, IntPtr.Zero, SND_ASYNC | SND_MEMORY | SND_NODEFAULT); } catch { }
        }

        private static double Env(double t, double attaque, double duree) { if (t < 0 || t > duree) return 0; return t < attaque ? t / attaque : Math.Exp(-4 * (t - attaque) / duree); }
        private static double Note(double t, double f, double debut, double duree) { double x = t - debut; return x < 0 ? 0 : 0.35 * Math.Sin(2 * Math.PI * f * x) * Env(x, 0.005, duree); }
        private static double Accord(double t, double[] f) { if (t < 0) return 0; double s = 0; foreach (var x in f) s += Math.Sin(2 * Math.PI * x * t) + 0.3 * Math.Sin(4 * Math.PI * x * t); return s / f.Length; }
        private static double Balayage(double t, double f0, double f1, double duree)
        {
            if (t < 0 || t > duree) return 0;
            // fréquence qui monte de façon exponentielle (phase intégrée), un peu « dents de scie » pour le côté synthé
            double k = Math.Log(f1 / f0) / duree, phase = 2 * Math.PI * f0 * (Math.Exp(k * t) - 1) / k;
            return 0.7 * Math.Sin(phase) + 0.3 * Math.Sin(2 * phase);
        }

        /// <summary>Fichier WAV (PCM 16 bits mono 44,1 kHz) construit en mémoire.</summary>
        private static byte[] Synthe(double duree, Func<double, double, double> f)
        {
            const int frequence = 44100;
            int n = (int)(duree * frequence);
            using (var m = new MemoryStream())
            using (var w = new BinaryWriter(m))
            {
                w.Write(new[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F' }); w.Write(36 + n * 2);
                w.Write(new[] { (byte)'W', (byte)'A', (byte)'V', (byte)'E', (byte)'f', (byte)'m', (byte)'t', (byte)' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(frequence); w.Write(frequence * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { (byte)'d', (byte)'a', (byte)'t', (byte)'a' }); w.Write(n * 2);
                for (int i = 0; i < n; i++)
                {
                    double t = (double)i / frequence, v = f(t, duree);
                    // fondu final pour éviter un clic
                    if (t > duree - 0.03) v *= Math.Max(0, (duree - t) / 0.03);
                    w.Write((short)(Math.Max(-1, Math.Min(1, v)) * 0.6 * short.MaxValue));
                }
                return m.ToArray();
            }
        }
    }
}
