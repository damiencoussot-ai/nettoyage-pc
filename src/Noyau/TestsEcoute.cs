// Velox - tests d'écoute du Studio son.
// 1. Test de direction : un pas joue d'une des 8 directions autour de toi (son 7.1, une voie par direction) ; tu cliques
//    d'où il vient. Score, confusions devant/derrière et gauche/droite, conseil (7.1 virtuel, tête virtuelle, casque).
//    Sans 7.1 virtuel, Windows mélange le 7.1 en stéréo : devant et derrière deviennent identiques (le test le montre).
// 2. Test d'audition : pour chaque oreille et chaque fréquence, un bip monte doucement (5 dB par bip) ; tu cliques dès que
//    tu l'entends. Velox compare tes deux oreilles : si l'une entend nettement moins bien à une fréquence (10 dB d'écart
//    ou plus), l'autre est baissée là, au plus de 6 dB. Velox ne fait que baisser (jamais de saturation). Ce n'est pas
//    un audiogramme médical : le casque et le volume comptent, seule la comparaison entre tes deux oreilles est fiable.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace NettoyagePC
{
    internal sealed class ResultatDirection
    {
        public double Score;          // sur 8 (1 point exact, 0,5 point pour la direction voisine)
        public int Exacts, DevantDerriere, GaucheDroite;
        public string Conseil = "";
    }

    internal static class TestDirection
    {
        /// <summary>Les 8 directions, dans le sens des aiguilles d'une montre depuis « devant ».</summary>
        public static readonly string[] Noms = { "Devant", "Devant à droite", "À droite", "Derrière à droite", "Derrière", "Derrière à gauche", "À gauche", "Devant à gauche" };

        /// <summary>Voies du 7.1 (ordre Windows : AvG, AvD, Centre, Caisson, ArG, ArD, CôtéG, CôtéD) pour chaque direction.</summary>
        private static readonly int[][] Voies = { new[] { 2 }, new[] { 1 }, new[] { 7 }, new[] { 5 }, new[] { 4, 5 }, new[] { 4 }, new[] { 6 }, new[] { 0 } };

        /// <summary>Deux pas (talon puis pointe, deux fois) venant de cette direction : 8 canaux entrelacés, 1,3 s.</summary>
        public static float[] Son(int direction, int fe, int graine)
        {
            int n = (int)(1.3 * fe);
            var t = new float[n * 8];
            var alea = new Random(graine);
            var voies = Voies[((direction % 8) + 8) % 8];
            float k = (float)(1 / Math.Sqrt(voies.Length));
            foreach (double debut in new[] { 0.05, 0.62 })
                foreach (var coup in new[] { Tuple.Create(0.0, 900.0, 1.0), Tuple.Create(0.09, 2600.0, 0.7) })
                {
                    var bp = DemoAudio.Biquad.PasseBande(coup.Item2, 1.1);
                    int d0 = (int)((debut + coup.Item1) * fe);
                    for (int i = 0; i < (int)(0.12 * fe) && d0 + i < n; i++)
                    {
                        double tt = i / (double)fe, v = 0.25 * coup.Item3 * Math.Min(1, tt / 0.002) * Math.Exp(-tt / 0.025) * bp.Passer(alea.NextDouble() * 2 - 1) * 3;
                        foreach (int c in voies) t[(d0 + i) * 8 + c] += (float)(v * k);
                    }
                }
            return t;
        }

        /// <summary>Direction symétrique devant/derrière (même côté).</summary>
        public static int Miroir(int d) { return (4 - d + 8) % 8; }

        /// <summary>Côté : 1 droite, −1 gauche, 0 axe (devant, derrière).</summary>
        public static int Cote(int d) { d = ((d % 8) + 8) % 8; return d >= 1 && d <= 3 ? 1 : d >= 5 ? -1 : 0; }

        /// <summary>Ordre de passage : les 8 directions, chacune une fois, mélangées.</summary>
        public static int[] Ordre(Random alea) { return Enumerable.Range(0, 8).OrderBy(x => alea.Next()).ToArray(); }

        public static ResultatDirection Noter(IList<int> joues, IList<int> reponses, bool septUnVirtuel)
        {
            var r = new ResultatDirection();
            for (int i = 0; i < Math.Min(joues.Count, reponses.Count); i++)
            {
                int j = joues[i], p = reponses[i], ecart = Math.Min((j - p + 8) % 8, (p - j + 8) % 8);
                if (ecart == 0) { r.Exacts++; r.Score += 1; }
                else if (ecart == 1) r.Score += 0.5;
                if (ecart != 0 && p == Miroir(j)) r.DevantDerriere++;
                if (Cote(j) * Cote(p) < 0) r.GaucheDroite++;
            }
            if (r.GaucheDroite >= 2) r.Conseil = "Gauche et droite semblent inversées : vérifie que ton casque est dans le bon sens (L sur l'oreille gauche).";
            else if (r.DevantDerriere >= 2)
                r.Conseil = septUnVirtuel ? "Devant et derrière se confondent : essaie une autre tête virtuelle dans « Son 7.1 virtuel », puis refais le test. Garde celle qui donne le meilleur score."
                    : "Devant et derrière se confondent : c'est normal en stéréo simple. Allume « Son 7.1 virtuel » (mode Expert), puis refais le test.";
            else if (r.Score >= 6.5) r.Conseil = "Très bonne localisation : ton réglage place bien les pas autour de toi.";
            else r.Conseil = "Localisation moyenne : refais le test au calme. Si le score reste bas, essaie une autre tête virtuelle dans « Son 7.1 virtuel ».";
            return r;
        }
    }

    internal static class TestAudition
    {
        /// <summary>Fréquences testées (Hz) : là où se trouvent les pas, les recharges et les voix.</summary>
        public static readonly int[] Frequences = { 500, 1000, 2000, 3000, 4000, 6000, 8000 };
        public const double Depart = -95, Pas = 5, DureeBip = 0.5, Reaction = 0.3;
        public const int Bips = 17;   // de −95 à −15 dB

        /// <summary>Bip montant, oreille gauche (0) ou droite (1) : stéréo entrelacée, Bips × 0,5 s.</summary>
        public static float[] Rampe(int frequence, int oreille, int fe)
        {
            int parBip = (int)(DureeBip * fe), son = (int)(0.3 * fe), bord = (int)(0.02 * fe);
            var t = new float[Bips * parBip * 2];
            for (int b = 0; b < Bips; b++)
            {
                double a = Math.Pow(10, (Depart + Pas * b) / 20) * Math.Sqrt(2);   // niveau efficace du bip = Depart + Pas × b
                for (int i = 0; i < son; i++)
                {
                    double env = Math.Min(1, Math.Min(i, son - 1 - i) / (double)bord);
                    double v = a * env * Math.Sin(2 * Math.PI * frequence * i / fe);
                    t[(b * parBip + i) * 2 + (oreille == 0 ? 0 : 1)] = (float)v;
                }
            }
            return t;
        }

        /// <summary>Niveau (dB) du bip entendu, d'après le temps écoulé au clic (temps de réaction retiré).</summary>
        public static double NiveauAuClic(double secondes)
        {
            int b = (int)Math.Floor((secondes - Reaction) / DureeBip);
            return Depart + Pas * Math.Max(0, Math.Min(Bips - 1, b));
        }

        /// <summary>
        /// Compensation : pour chaque fréquence, si une oreille entend nettement moins bien (seuil plus haut de 10 dB ou
        /// plus), l'autre oreille est baissée de (écart − 5) / 2 dB, au plus 6 dB. Seuil inconnu (null : bip jamais
        /// entendu) compté comme −10 dB. Renvoie les baisses (≤ 0) pour la gauche et la droite.
        /// </summary>
        public static void Compensation(double?[] gauche, double?[] droite, out double[] corrG, out double[] corrD)
        {
            int n = Frequences.Length;
            corrG = new double[n]; corrD = new double[n];
            for (int i = 0; i < n; i++)
            {
                double g = gauche != null && i < gauche.Length ? (gauche[i] ?? -10) : double.NaN;
                double d = droite != null && i < droite.Length ? (droite[i] ?? -10) : double.NaN;
                if (double.IsNaN(g) || double.IsNaN(d)) continue;
                double ecart = Math.Abs(g - d);
                if (ecart < 10) continue;
                double baisse = -Math.Min(6, Math.Round((ecart - 5) / 2 * 2) / 2);
                if (g > d) corrD[i] = baisse; else corrG[i] = baisse;   // la gauche entend moins bien : la droite est baissée
            }
        }

        /// <summary>Lignes d'Equalizer APO de la compensation ("" si rien à compenser).</summary>
        public static string Lignes(double[] corrG, double[] corrD)
        {
            var c = CultureInfo.InvariantCulture;
            var sb = new System.Text.StringBuilder();
            foreach (var cote in new[] { Tuple.Create("L", corrG), Tuple.Create("R", corrD) })
            {
                if (cote.Item2 == null || cote.Item2.All(x => x == 0)) continue;
                sb.Append("Channel: ").Append(cote.Item1).Append("\r\n");
                for (int i = 0; i < Math.Min(Frequences.Length, cote.Item2.Length); i++)
                    if (cote.Item2[i] < 0) sb.Append("Filter: ON PK Fc ").Append(Frequences[i].ToString(c)).Append(" Hz Gain ").Append(cote.Item2[i].ToString("F1", c)).Append(" dB Q 1.41\r\n");
            }
            if (sb.Length > 0) sb.Append("Channel: all\r\n");
            return sb.ToString();
        }
    }
}
