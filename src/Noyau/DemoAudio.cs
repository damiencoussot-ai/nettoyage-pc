// Velox - test « avant / après » du Studio son : une petite scène de jeu fabriquée par Velox (rien à télécharger) :
// des pas qui passent de gauche à droite, une explosion qui les couvre, des tirs, une recharge. Jouée une fois sans le
// Studio son, puis une fois avec, sur la sortie par défaut de Windows : on entend ce que l'égaliseur change.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NettoyagePC
{
    internal static class DemoAudio
    {
        public const int Frequence = 48000;
        public const double Duree = 6.0;

        /// <summary>Instants des pas (secondes), de gauche à droite.</summary>
        public static readonly double[] Pas = { 0.45, 0.95, 1.45, 1.95, 2.45, 2.95, 3.45, 3.95 };

        internal struct Biquad
        {
            private double b0, b1, b2, a1, a2, x1, x2, y1, y2;
            public static Biquad PasseBande(double f, double q)
            {
                double w = 2 * Math.PI * f / Frequence, al = Math.Sin(w) / (2 * q), a0 = 1 + al;
                return new Biquad { b0 = al / a0, b1 = 0, b2 = -al / a0, a1 = -2 * Math.Cos(w) / a0, a2 = (1 - al) / a0 };
            }
            public static Biquad PasseBas(double f, double q)
            {
                double w = 2 * Math.PI * f / Frequence, al = Math.Sin(w) / (2 * q), c = Math.Cos(w), a0 = 1 + al;
                return new Biquad { b0 = (1 - c) / 2 / a0, b1 = (1 - c) / a0, b2 = (1 - c) / 2 / a0, a1 = -2 * c / a0, a2 = (1 - al) / a0 };
            }
            public static Biquad PasseHaut(double f, double q)
            {
                double w = 2 * Math.PI * f / Frequence, al = Math.Sin(w) / (2 * q), c = Math.Cos(w), a0 = 1 + al;
                return new Biquad { b0 = (1 + c) / 2 / a0, b1 = -(1 + c) / a0, b2 = (1 + c) / 2 / a0, a1 = -2 * c / a0, a2 = (1 - al) / a0 };
            }
            public double Passer(double x)
            {
                double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        /// <summary>La scène en échantillons (gauche, droite), toujours la même (hasard à graine fixe), crête à 0,8.</summary>
        public static void Scene(out float[] g, out float[] d)
        {
            int n = (int)(Duree * Frequence);
            g = new float[n]; d = new float[n];
            var alea = new Random(20261116);
            Func<double> bruit = () => alea.NextDouble() * 2 - 1;

            // fond : grondement léger
            var fond = Biquad.PasseBas(110, 0.7);
            for (int i = 0; i < n; i++) { double v = 0.05 * fond.Passer(bruit()); g[i] += (float)v; d[i] += (float)v; }

            // pas : talon (bruit sourd) + frottement (bruit autour de 2,5 kHz), faibles, panoramique de gauche à droite
            for (int k = 0; k < Pas.Length; k++)
            {
                int debut = (int)(Pas[k] * Frequence), lg = (int)(0.16 * Frequence);
                var talon = Biquad.PasseBas(160, 0.9); var frot = Biquad.PasseBande(2500, 1.4); var frot2 = Biquad.PasseBande(4200, 1.8);
                double pan = -0.85 + 1.7 * k / (Pas.Length - 1), ga = Math.Sqrt((1 - pan) / 2), da = Math.Sqrt((1 + pan) / 2);
                for (int i = 0; i < lg && debut + i < n; i++)
                {
                    double t = i / (double)Frequence;
                    double env = Math.Min(1, t / 0.004) * Math.Exp(-t / 0.035);
                    double env2 = Math.Min(1, Math.Max(0, (t - 0.03) / 0.004)) * Math.Exp(-Math.Max(0, t - 0.03) / 0.03);
                    double v = 0.10 * env * talon.Passer(bruit()) * 3 + 0.16 * (env + env2) * (frot.Passer(bruit()) + 0.6 * frot2.Passer(bruit()));
                    g[debut + i] += (float)(v * ga); d[debut + i] += (float)(v * da);
                }
            }

            // explosion à 1,2 s : grondement grave qui couvre les pas suivants
            {
                int debut = (int)(1.2 * Frequence);
                var lp = Biquad.PasseBas(140, 0.8); var lp2 = Biquad.PasseBas(400, 0.7);
                double phase = 0;
                for (int i = 0; debut + i < n; i++)
                {
                    double t = i / (double)Frequence;
                    double env = Math.Min(1, t / 0.012) * Math.Exp(-t / 1.1);
                    if (env < 0.0005) break;
                    phase += 2 * Math.PI * (34 + 22 * Math.Exp(-t / 0.4)) / Frequence;
                    double v = env * (0.55 * lp.Passer(bruit()) * 4 + 0.35 * Math.Sin(phase) + 0.12 * lp2.Passer(bruit()) * 2 * Math.Exp(-t / 0.25));
                    g[debut + i] += (float)v; d[debut + i] += (float)v;
                }
            }

            // tirs (4,45 s et 4,85 s) : claquement large, un peu à droite
            foreach (double ti in new[] { 4.45, 4.85 })
            {
                int debut = (int)(ti * Frequence);
                var hp = Biquad.PasseHaut(500, 0.7); var corps = Biquad.PasseBas(900, 0.8);
                for (int i = 0; i < (int)(0.35 * Frequence) && debut + i < n; i++)
                {
                    double t = i / (double)Frequence;
                    double v = Math.Min(1, t / 0.0015) * (0.5 * Math.Exp(-t / 0.05) * hp.Passer(bruit()) + 0.35 * Math.Exp(-t / 0.09) * corps.Passer(bruit()) * 2);
                    g[debut + i] += (float)(0.75 * v); d[debut + i] += (float)v;
                }
            }

            // recharge (5,35 s) : deux clics métalliques, à gauche
            foreach (double ti in new[] { 5.35, 5.6 })
            {
                int debut = (int)(ti * Frequence);
                var bp = Biquad.PasseBande(3800, 3);
                for (int i = 0; i < (int)(0.05 * Frequence) && debut + i < n; i++)
                {
                    double t = i / (double)Frequence;
                    double v = 0.25 * Math.Exp(-t / 0.008) * bp.Passer(bruit()) * 3;
                    g[debut + i] += (float)v; d[debut + i] += (float)(0.4 * v);
                }
            }

            // crête ramenée à 0,8 (jamais de saturation), fondu de 30 ms à la fin
            float crete = 0;
            for (int i = 0; i < n; i++) crete = Math.Max(crete, Math.Max(Math.Abs(g[i]), Math.Abs(d[i])));
            float k2 = crete > 0 ? 0.8f / crete : 1;
            int fondu = (int)(0.03 * Frequence);
            for (int i = 0; i < n; i++)
            {
                float f = i >= n - fondu ? (n - i) / (float)fondu : 1;
                g[i] *= k2 * f; d[i] *= k2 * f;
            }
        }

        // ---------------------------------------------------------------- comparaison au même volume
        // L'égaliseur baisse le volume général (préampli, pour ne jamais saturer). Comparé tel quel, le son « sans »
        // serait plus fort, et un son plus fort paraît toujours meilleur : le « sans » est donc joué au même volume
        // (écart mesuré sur la scène, filtres de Velox appliqués), pour n'entendre que le travail de l'égaliseur.

        private static void Coefficients(FiltreAudio f, out double b0, out double b1, out double b2, out double a1, out double a2)
        {
            double w0 = 2 * Math.PI * f.Fc / Frequence, cos = Math.Cos(w0), q = f.Type == "HP" ? 0.7071 : f.Q, alpha = Math.Sin(w0) / (2 * q), A = Math.Pow(10, f.Gain / 40), a0;
            if (f.Type == "HP") { b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = b0; a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha; }
            else if (f.Type == "LSC" || f.Type == "HSC")
            {
                double s = 2 * Math.Sqrt(A) * alpha, sg = f.Type == "LSC" ? 1 : -1;
                b0 = A * ((A + 1) - sg * (A - 1) * cos + s); b1 = sg * 2 * A * ((A - 1) - sg * (A + 1) * cos); b2 = A * ((A + 1) - sg * (A - 1) * cos - s);
                a0 = (A + 1) + sg * (A - 1) * cos + s; a1 = -sg * 2 * ((A - 1) + sg * (A + 1) * cos); a2 = (A + 1) + sg * (A - 1) * cos - s;
            }
            else { b0 = 1 + alpha * A; b1 = -2 * cos; b2 = 1 - alpha * A; a0 = 1 + alpha / A; a1 = -2 * cos; a2 = 1 - alpha / A; }
            b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0;
        }

        /// <summary>La scène passée dans ces filtres (un canal), pour mesurer le volume obtenu.</summary>
        public static float[] Filtrer(float[] x, System.Collections.Generic.IEnumerable<FiltreAudio> filtres, double gainDb)
        {
            var y = new float[x.Length];
            double k = Math.Pow(10, gainDb / 20);
            for (int i = 0; i < x.Length; i++) y[i] = (float)(x[i] * k);
            foreach (var f in filtres)
            {
                double b0, b1, b2, a1, a2, x1 = 0, x2 = 0, y1 = 0, y2 = 0;
                Coefficients(f, out b0, out b1, out b2, out a1, out a2);
                for (int i = 0; i < y.Length; i++)
                {
                    double v = y[i], o = b0 * v + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                    x2 = x1; x1 = v; y2 = y1; y1 = o; y[i] = (float)o;
                }
            }
            return y;
        }

        public static double Niveau(float[] x) { double e = 0; foreach (var v in x) e += v * (double)v; return 10 * Math.Log10(e / Math.Max(1, x.Length) + 1e-20); }

        private static string cleEcart; private static double ecart;

        /// <summary>
        /// Volume à donner au son « sans » pour qu'il sonne aussi fort que le son « avec » (dB, de −12 à +6, pas de
        /// 0,5 dB) : écart de niveau de la scène, avec et sans les filtres et le préampli de ces réglages.
        /// </summary>
        public static double EcartNiveau(ReglagesAudioJeu r)
        {
            var filtres = AudioJeu.Filtres(r);
            double pre = AudioJeu.Preampli(r);
            string cle = string.Join(";", filtres.Select(f => f.Type + f.Fc + "/" + f.Gain + "/" + f.Q)) + "|" + pre;
            lock (verrou) if (cle == cleEcart) return ecart;
            float[] g, d; Scene(out g, out d);
            var mono = new float[g.Length];
            for (int i = 0; i < g.Length; i++) mono[i] = (g[i] + d[i]) / 2;
            double e = Niveau(Filtrer(mono, filtres, pre)) - Niveau(mono);
            e = Math.Max(-12, Math.Min(6, Math.Round(e * 2, MidpointRounding.AwayFromZero) / 2));
            lock (verrou) { cleEcart = cle; ecart = e; }
            return e;
        }

        private static byte[] wav;

        /// <summary>La scène au format WAV (PCM 16 bits, stéréo, 48 kHz), fabriquée une fois.</summary>
        public static byte[] Wav()
        {
            if (wav != null) return wav;
            float[] g, d;
            Scene(out g, out d);
            return wav = WavDe(g, d);
        }

        /// <summary>
        /// Duel à l'oreille : la scène passée dans les filtres (calibration comprise) et le préampli de chaque candidat,
        /// puis tous remis au MÊME volume (niveau moyen de la scène) : le plus fort ne gagne pas parce qu'il est plus fort.
        /// Ce volume commun est le plus haut possible sans qu'aucun ne sature (crête 0,95). Un WAV par candidat.
        /// </summary>
        public static List<byte[]> Duel(IList<ReglagesAudioJeu> candidats)
        {
            float[] g, d;
            Scene(out g, out d);
            var rendus = new List<Tuple<float[], float[], double, double>>();
            foreach (var r in candidats)
            {
                var f = AudioJeu.Filtres(r);
                double pre = AudioJeu.Preampli(r);
                float[] fg = Filtrer(g, f, pre), fd = Filtrer(d, f, pre);
                double crete = 1e-9;
                for (int i = 0; i < fg.Length; i++) crete = Math.Max(crete, Math.Max(Math.Abs(fg[i]), Math.Abs(fd[i])));
                rendus.Add(Tuple.Create(fg, fd, (Niveau(fg) + Niveau(fd)) / 2, crete));
            }
            double cible = double.MaxValue;
            foreach (var x in rendus) cible = Math.Min(cible, x.Item3 + 20 * Math.Log10(0.95 / x.Item4));
            var l = new List<byte[]>();
            foreach (var x in rendus)
            {
                float k = (float)Math.Pow(10, (cible - x.Item3) / 20);
                var a = new float[x.Item1.Length]; var b = new float[x.Item2.Length];
                for (int i = 0; i < a.Length; i++) { a[i] = x.Item1[i] * k; b[i] = x.Item2[i] * k; }
                l.Add(WavDe(a, b));
            }
            return l;
        }

        /// <summary>WAV PCM 16 bits stéréo 48 kHz de ces échantillons (écrêtés à ±1).</summary>
        public static byte[] WavDe(float[] g, float[] d)
        {
            int n = g.Length, octets = n * 4;
            using (var m = new MemoryStream(44 + octets))
            using (var w = new BinaryWriter(m, Encoding.ASCII))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + octets); w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)2); w.Write(Frequence); w.Write(Frequence * 4); w.Write((short)4); w.Write((short)16);
                w.Write(Encoding.ASCII.GetBytes("data")); w.Write(octets);
                for (int i = 0; i < n; i++)
                {
                    w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, g[i])) * 32767));
                    w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, d[i])) * 32767));
                }
                w.Flush();
                return m.ToArray();
            }
        }

        // ---------------------------------------------------------------- lecture (Windows)
        [DllImport("winmm.dll", SetLastError = true)]
        private static extern bool PlaySound(IntPtr son, IntPtr module, uint options);
        private const uint SND_ASYNC = 0x1, SND_NODEFAULT = 0x2, SND_MEMORY = 0x4;
        private static GCHandle tenu;
        private static readonly object verrou = new object();

        /// <summary>Joue la scène sur la sortie par défaut de Windows (sans attendre). Faux si Windows refuse.</summary>
        public static bool Jouer() { return Jouer(Wav()); }

        /// <summary>Joue ce WAV sur la sortie par défaut de Windows (sans attendre). Faux si Windows refuse.</summary>
        public static bool Jouer(byte[] o)
        {
            if (!OperatingSystem.IsWindows() || o == null) return false;
            lock (verrou)
            {
                Arreter();
                tenu = GCHandle.Alloc(o, GCHandleType.Pinned);   // (le son est lu depuis ce tableau : il ne doit pas bouger)
                try { return PlaySound(tenu.AddrOfPinnedObject(), IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT); }
                catch { return false; }
            }
        }

        /// <summary>Arrête la lecture en cours.</summary>
        public static void Arreter()
        {
            if (!OperatingSystem.IsWindows()) return;
            lock (verrou)
            {
                try { PlaySound(IntPtr.Zero, IntPtr.Zero, 0); } catch { }
                if (tenu.IsAllocated) tenu.Free();
            }
        }
    }
}
