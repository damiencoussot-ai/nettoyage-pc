// Outils des essais : fabrication de scènes de jeu synthétiques (pas, tirs, fond) et mesures sur le son traité.
// Tout est déterministe (générateurs aléatoires à graine fixe) : un essai donne toujours le même résultat.
using System;
using NettoyagePC;

namespace Velox.Tests
{
    /// <summary>Son stéréo (ou multicanal) entrelacé, à 48 kHz, avec ses composantes gardées à part pour les mesures.</summary>
    internal sealed class Scene
    {
        public const int Fe = DemoAudio.Frequence;
        public readonly int Canaux, Images;
        public readonly float[] Son;

        public Scene(double secondes, int canaux = 2)
        {
            Canaux = canaux; Images = (int)(secondes * Fe); Son = new float[Images * canaux];
        }

        public static Func<double> Bruit(int graine)
        {
            var alea = new Random(graine);
            return () => alea.NextDouble() * 2 - 1;
        }

        /// <summary>Un pas : bruit filtré autour de 2,2 kHz, attaque 2 ms, décroissance 20 ms, 60 ms en tout. Amplitude
        /// de crête approximative « niveau » ; gauche et droite pondérées (1, 1 : centré ; 1, 0,2 : à gauche).</summary>
        public static float[] Pas(double niveau, int graine)
        {
            var b = Bruit(graine); var bp = DemoAudio.Biquad.PasseBande(2200, 1.0);
            var r = new float[(int)(0.06 * Fe)];
            for (int i = 0; i < r.Length; i++)
            {
                double t = i / (double)Fe;
                r[i] = (float)(niveau * Math.Min(1, t / 0.002) * Math.Exp(-t / 0.02) * bp.Passer(b()) * 3);
            }
            return r;
        }

        /// <summary>Un tir : bruit large bande (au-dessus de 150 Hz), décroissance 20 ms, 60 ms en tout.</summary>
        public static float[] Tir(double niveau, int graine)
        {
            var b = Bruit(graine); var hp = DemoAudio.Biquad.PasseHaut(150, 0.7);
            var r = new float[(int)(0.06 * Fe)];
            for (int i = 0; i < r.Length; i++) r[i] = (float)(niveau * Math.Exp(-i / (0.02 * Fe)) * hp.Passer(b()));
            return r;
        }

        /// <summary>Ajoute un son mono à l'instant donné, avec un gain par canal.</summary>
        public void Ajouter(float[] mono, double debut, params double[] gains)
        {
            int d = (int)(debut * Fe);
            for (int i = 0; i < mono.Length && d + i < Images; i++)
                for (int c = 0; c < Canaux && c < gains.Length; c++)
                    Son[(d + i) * Canaux + c] += (float)(mono[i] * gains[c]);
        }

        /// <summary>Ajoute une sinusoïde continue (fond repérable par sa fréquence), sur tous les canaux.</summary>
        public void AjouterSinus(double frequence, double amplitude)
        {
            for (int i = 0; i < Images; i++)
            {
                float v = (float)(amplitude * Math.Sin(2 * Math.PI * frequence * i / Fe));
                for (int c = 0; c < Canaux; c++) Son[i * Canaux + c] += v;
            }
        }

        /// <summary>Ajoute un souffle continu (bruit large bande) sur tous les canaux.</summary>
        public void AjouterSouffle(double amplitude, int graine)
        {
            var b = Bruit(graine);
            for (int i = 0; i < Images; i++)
                for (int c = 0; c < Canaux; c++) Son[i * Canaux + c] += (float)(amplitude * b());
        }

        public float[] Copie() { return (float[])Son.Clone(); }
    }

    internal static class Mesure
    {
        public const int Fe = Scene.Fe;

        /// <summary>Traite le son comme le moteur (paquets de 10 ms) et recale la sortie (retard du limiteur retiré).</summary>
        public static float[] Traiter(CompresseurPas comp, float[] son, int canaux)
        {
            var t = (float[])son.Clone();
            int images = t.Length / canaux, bloc = Fe / 100;
            var b = new float[bloc * canaux];
            for (int p = 0; p < images; p += bloc)
            {
                int n = Math.Min(bloc, images - p);
                Array.Copy(t, p * canaux, b, 0, n * canaux);
                comp.Traiter(b, n, canaux);
                Array.Copy(b, 0, t, p * canaux, n * canaux);
            }
            int dec = comp.Retard;
            Array.Copy(t, dec * canaux, t, 0, t.Length - dec * canaux);
            Array.Clear(t, t.Length - dec * canaux, dec * canaux);
            return t;
        }

        public static double Db(double puissance) { return 10 * Math.Log10(puissance + 1e-20); }

        /// <summary>Niveau moyen (dB) entre deux instants, tous canaux.</summary>
        public static double Niveau(float[] t, int canaux, double t0, double t1)
        {
            int a = (int)(t0 * Fe) * canaux, b = Math.Min(t.Length, (int)(t1 * Fe) * canaux);
            double e = 0;
            for (int i = a; i < b; i++) e += t[i] * (double)t[i];
            return Db(e / Math.Max(1, b - a));
        }

        /// <summary>Niveau (dB) d'un canal entre deux instants.</summary>
        public static double NiveauCanal(float[] t, int canaux, int canal, double t0, double t1)
        {
            int a = (int)(t0 * Fe), b = Math.Min(t.Length / canaux, (int)(t1 * Fe));
            double e = 0;
            for (int i = a; i < b; i++) { double v = t[i * canaux + canal]; e += v * v; }
            return Db(e / Math.Max(1, b - a));
        }

        /// <summary>Amplitude (dB) de la sinusoïde de fréquence donnée entre deux instants (détection synchrone, canal 0).
        /// Sert à suivre un fond continu indépendamment des pas et des tirs qui jouent en même temps.</summary>
        public static double Sinus(float[] t, int canaux, double frequence, double t0, double t1)
        {
            int a = (int)(t0 * Fe), b = (int)(t1 * Fe);
            double re = 0, im = 0;
            for (int i = a; i < b; i++)
            {
                double ph = 2 * Math.PI * frequence * i / Fe, v = t[i * canaux];
                re += v * Math.Cos(ph); im += v * Math.Sin(ph);
            }
            double amp = 2 * Math.Sqrt(re * re + im * im) / Math.Max(1, b - a);
            return 20 * Math.Log10(amp + 1e-20);
        }

        /// <summary>Part (dB) d'un son connu dans le mélange traité, entre deux instants : énergie de la projection du son
        /// traité sur la référence, rapportée à l'énergie totale. Plus elle monte, plus ce son ressort.</summary>
        public static double Part(float[] traite, float[] reference, int canaux, double t0, double t1)
        {
            int a = (int)(t0 * Fe) * canaux, b = Math.Min(traite.Length, (int)(t1 * Fe) * canaux);
            double pr = 0, pp = 0, tot = 0;
            for (int i = a; i < b; i++) { pr += traite[i] * (double)reference[i]; pp += reference[i] * (double)reference[i]; tot += traite[i] * (double)traite[i]; }
            double k = pr / Math.Max(1e-20, pp);
            return Db(k * k * pp / Math.Max(1e-20, tot));
        }

        /// <summary>Gain (dB) appliqué à un son connu : amplitude de sa projection dans le son traité.</summary>
        public static double GainSur(float[] traite, float[] reference, int canaux, double t0, double t1)
        {
            int a = (int)(t0 * Fe) * canaux, b = Math.Min(traite.Length, (int)(t1 * Fe) * canaux);
            double pr = 0, pp = 0;
            for (int i = a; i < b; i++) { pr += traite[i] * (double)reference[i]; pp += reference[i] * (double)reference[i]; }
            return 20 * Math.Log10(Math.Abs(pr / Math.Max(1e-20, pp)) + 1e-20);
        }

        public static float Crete(float[] t)
        {
            float m = 0;
            foreach (var v in t) m = Math.Max(m, Math.Abs(v));
            return m;
        }
    }
}
