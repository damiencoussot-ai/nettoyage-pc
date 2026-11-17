// Velox - moteur audio « Pas boostés » : traitement du son en direct, fait par Velox lui-même.
// Equalizer APO ne sait que filtrer (égaliseur) : il ne peut pas remonter un pas lointain sans remonter aussi l'explosion
// qui joue sur les mêmes fréquences. Ce moteur fait ce que font les logiciels de jeu (compression dynamique) : les sons
// faibles remontent par rapport aux sons forts, puis un limiteur empêche toute saturation.
//
// Chemin du son : le jeu joue dans un câble virtuel (VB-Cable, gratuit, sortie par défaut de Windows) ; le moteur, dans
// un processus à part (« NettoyagePC.exe --moteur-son »), capte ce câble, traite le son et le joue sur ton casque.
// L'égaliseur et la calibration du casque restent dans Equalizer APO, sur le casque, après le moteur (pas de double
// traitement). Ce fichier contient le traitement (sans Windows, essayé hors ligne sur la scène de jeu) et ses réglages.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    /// <summary>Réglages du moteur « Pas boostés » (fichier moteur-son.json, relu par le moteur à chaque changement).</summary>
    internal sealed class ReglagesMoteurSon
    {
        public bool Actif;
        /// <summary>Force de 0 à 100 (Léger 35, Fort 65, Extrême 100).</summary>
        public int Force = 65;
        /// <summary>Compression seulement quand un jeu reconnu tourne (sinon le son passe tel quel).</summary>
        public bool JeuxSeulement = true;
        /// <summary>Câble capté (entrée Windows) et casque où jouer (sortie Windows) : identifiants complets.</summary>
        public string Source = "", Destination = "";
        /// <summary>Sortie par défaut de Windows avant que Velox la mette sur le câble (remise à l'arrêt).</summary>
        public string DefautAvant = "";

        public ReglagesMoteurSon Borner()
        {
            Force = Math.Max(0, Math.Min(100, Force));
            if (!AudioWindows.IdValide(Destination)) Destination = "";
            if (!AudioWindows.IdEntreeValide(Source)) Source = "";
            if (!AudioWindows.IdValide(DefautAvant)) DefautAvant = "";
            return this;
        }

        public string Json()
        {
            return NettoyagePC.Json.Ecrire(new Dictionary<string, object>
            {
                { "Actif", Actif }, { "Force", Force }, { "JeuxSeulement", JeuxSeulement },
                { "Source", Source }, { "Destination", Destination }, { "DefautAvant", DefautAvant },
            });
        }

        public static ReglagesMoteurSon Lire(string json)
        {
            var r = new ReglagesMoteurSon();
            try
            {
                var d = NettoyagePC.Json.Lire(json) as Dictionary<string, object>;
                if (d == null) return r;
                object o;
                if (d.TryGetValue("Actif", out o) && o is bool a) r.Actif = a;
                if (d.TryGetValue("Force", out o)) r.Force = (int)Math.Round(Convert.ToDouble(o, CultureInfo.InvariantCulture));
                if (d.TryGetValue("JeuxSeulement", out o) && o is bool j) r.JeuxSeulement = j;
                if (d.TryGetValue("Source", out o)) r.Source = Convert.ToString(o, CultureInfo.InvariantCulture) ?? "";
                if (d.TryGetValue("Destination", out o)) r.Destination = Convert.ToString(o, CultureInfo.InvariantCulture) ?? "";
                if (d.TryGetValue("DefautAvant", out o)) r.DefautAvant = Convert.ToString(o, CultureInfo.InvariantCulture) ?? "";
            }
            catch { return new ReglagesMoteurSon(); }
            return r.Borner();
        }

        public ReglagesMoteurSon Copie() { return Lire(Json()); }
    }

    /// <summary>
    /// Compresseur « pas boostés » (stéréo lié, à genou souple) suivi d'un limiteur à anticipation. Les sons faibles (pas
    /// lointains, recharges) remontent ; les sons forts (explosions, tirs) sont tenus ; rien ne dépasse 0,95.
    /// - Niveau mesuré en moyenne sur 5 ms (pas en crête) : un pas est bref, sa crête est 15 dB au-dessus de son niveau ;
    ///   mesuré en crête, il serait pris pour un son fort et baissé au lieu d'être remonté.
    /// - Garde : dans le silence et le souffle (sous −60 dB), la remontée s'efface ; elle revient en 2 ms dès qu'un son
    ///   arrive (le premier pas après un silence est donc remonté tout de suite).
    /// - Limiteur : il voit 1,5 ms à l'avance (retard du son : 1,5 ms) et baisse le gain avant le pic, sans claquement.
    /// </summary>
    internal sealed class CompresseurPas
    {
        public readonly double Seuil, Rapport, Remontee, Genou = 8;
        private readonly double aNiveau, attaque, relache, gardeMonte, gardeDescend, relacheLim;
        private readonly int avance;
        private double puissance = 1e-12, gainLisse, garde, gainLim = 1;
        private float[] retard, sec; private float[] besoin; private int pos, canauxRetard;
        private readonly double aMelange;
        private double melange = 1;
        /// <summary>1 : son traité ; 0 : son d'origine (hors jeu, avec « jeux seulement »). Bascule en douceur (100 ms).</summary>
        public double MelangeCible = 1;
        public double ReductionMax;     // (relevé) plus forte réduction depuis la dernière lecture, dB
        public const float Plafond = 0.95f;

        public CompresseurPas(int force, int frequence)
        {
            double f = Math.Max(0, Math.Min(100, force)) / 100.0;
            Seuil = -14 - 26 * f;                  // −14 dB (léger) à −40 dB (extrême)
            Rapport = 1.5 + 4.5 * f;               // 1,5:1 à 6:1
            Remontee = -Seuil * (1 - 1 / Rapport) * 0.7;
            double fe = Math.Max(8000, frequence);
            aNiveau = Math.Exp(-1 / (0.005 * fe));
            attaque = Math.Exp(-1 / (0.0015 * fe));
            relache = Math.Exp(-1 / ((0.20 - 0.09 * f) * fe));          // 200 ms à 110 ms : les pas reviennent vite après un tir
            gardeMonte = Math.Exp(-1 / (0.002 * fe)); gardeDescend = Math.Exp(-1 / (0.08 * fe));
            relacheLim = Math.Exp(-1 / (0.06 * fe));
            avance = Math.Max(1, (int)(0.0015 * fe));
            aMelange = Math.Exp(-1 / (0.1 * fe));
            gainLisse = Remontee;
        }

        /// <summary>Retard ajouté par le traitement (images).</summary>
        public int Retard { get { return avance; } }

        /// <summary>Courbe du compresseur : gain (dB) pour un niveau d'entrée (dB), remontée comprise, sans la garde.</summary>
        public double Gain(double niveauDb)
        {
            double x = niveauDb, y, d = 2 * (x - Seuil);
            if (d < -Genou) y = x;
            else if (d > Genou) y = Seuil + (x - Seuil) / Rapport;
            else { double z = x - Seuil + Genou / 2; y = x + (1 / Rapport - 1) * z * z / (2 * Genou); }
            return y - x + Remontee;
        }

        /// <summary>Traite « images » images entrelacées de « canaux » canaux, en place (sortie retardée de Retard images).</summary>
        public void Traiter(float[] t, int images, int canaux)
        {
            if (retard == null || canauxRetard != canaux)
            {
                canauxRetard = canaux; retard = new float[(avance + 1) * canaux]; sec = new float[(avance + 1) * canaux]; besoin = new float[avance + 1]; pos = 0;
                for (int k = 0; k < besoin.Length; k++) besoin[k] = 1;
            }
            int lg = avance + 1;
            for (int i = 0; i < images; i++)
            {
                int b = i * canaux;
                // 1. niveau (moyenne sur 5 ms) et gain du compresseur
                float p = 0;
                for (int c = 0; c < canaux; c++) { float v = t[b + c]; if (v * v > p) p = v * v; }
                puissance = aNiveau * puissance + (1 - aNiveau) * p;
                double niv = 10 * Math.Log10(puissance + 1e-18);
                double cible = Gain(niv);
                gainLisse = cible < gainLisse ? attaque * gainLisse + (1 - attaque) * cible : relache * gainLisse + (1 - relache) * cible;
                double voulue = Math.Max(0, Math.Min(1, (niv + 75) / 15));
                garde = voulue > garde ? gardeMonte * garde + (1 - gardeMonte) * voulue : gardeDescend * garde + (1 - gardeDescend) * voulue;
                double gDb = gainLisse - Remontee * (1 - garde);
                double red = Remontee - gainLisse; if (red > ReductionMax) ReductionMax = red;
                float g = (float)Math.Pow(10, gDb / 20);
                // 2. échantillons compressés, mis dans la ligne à retard ; gain que demandera le limiteur pour eux
                float crete = 0;
                for (int c = 0; c < canaux; c++) { float v = t[b + c] * g; retard[pos * canaux + c] = v; sec[pos * canaux + c] = t[b + c]; if (Math.Abs(v) > crete) crete = Math.Abs(v); }
                besoin[pos] = crete > Plafond ? Plafond / crete : 1;
                // 3. limiteur : le plus petit gain demandé sur les 1,5 ms à venir, relâché en douceur
                float mini = 1;
                for (int k = 0; k < lg; k++) if (besoin[k] < mini) mini = besoin[k];
                gainLim = mini < gainLim ? mini : Math.Min(mini, relacheLim * gainLim + (1 - relacheLim));
                // 4. sortie : l'échantillon le plus ancien de la ligne
                int ancien = (pos + 1) % lg;
                melange = aMelange * melange + (1 - aMelange) * MelangeCible;
                float m = (float)melange;
                for (int c = 0; c < canaux; c++)
                {
                    float v = (float)(retard[ancien * canaux + c] * gainLim) * m + sec[ancien * canaux + c] * (1 - m);
                    t[b + c] = v > Plafond ? Plafond : v < -Plafond ? -Plafond : v;
                }
                pos = ancien;
            }
        }
    }

    /// <summary>Un câble virtuel trouvé sur ce PC : sa sortie (où joue le jeu) et son entrée (que capte le moteur).</summary>
    internal sealed class CableVirtuel
    {
        public string Nom = "", Sortie = "", Entree = "";
    }

    internal static partial class MoteurSon
    {
        public const string SiteCable = "https://vb-audio.com/Cable/";
        public static string FichierReglages { get { return Path.Combine(Chemins.Local, "moteur-son.json"); } }
        public static string FichierEtat { get { return Path.Combine(Chemins.Local, "moteur-son-etat.txt"); } }

        /// <summary>Préréglages proposés dans la page.</summary>
        public static readonly Tuple<string, int>[] Niveaux = { Tuple.Create("Léger", 35), Tuple.Create("Fort", 65), Tuple.Create("Extrême", 100) };

        public static ReglagesMoteurSon Lire()
        {
            try { if (File.Exists(FichierReglages)) return ReglagesMoteurSon.Lire(File.ReadAllText(FichierReglages)); } catch { }
            return new ReglagesMoteurSon();
        }

        public static void Ecrire(ReglagesMoteurSon r)
        {
            Directory.CreateDirectory(Chemins.Local);
            string temp = FichierReglages + ".tmp";
            File.WriteAllText(temp, r.Borner().Json());
            File.Move(temp, FichierReglages, true);
        }

        /// <summary>
        /// (essais) Scène de mesure, comme dans un vrai jeu : un pas lointain (−40 dB) à 0,3 s, une explosion forte (crête
        /// vers −1 dB) de 1 à 2 s, puis un pas lointain à 2,4 s. Écarts (dB) entre l'explosion et le premier pas sans puis
        /// avec le compresseur, écart du pas d'après l'explosion (le compresseur doit s'être relâché), et crête finale.
        /// </summary>
        public static void Mesurer(int force, out double ecartSans, out double ecartAvec, out double retourPas, out float creteAvec)
        {
            int fe = DemoAudio.Frequence, n = 3 * fe;
            var t = new float[n * 2];
            var alea = new Random(7);
            Func<double> bruit = () => alea.NextDouble() * 2 - 1;
            foreach (double debut in new[] { 0.3, 2.4 })
            {
                var bp = DemoAudio.Biquad.PasseBande(2500, 1.4);
                for (int i = 0; i < (int)(0.08 * fe); i++)
                {
                    double tt = i / (double)fe, v = 0.03 * Math.Min(1, tt / 0.003) * Math.Exp(-tt / 0.03) * bp.Passer(bruit()) * 3;
                    int k = (int)(debut * fe) + i; t[2 * k] += (float)v; t[2 * k + 1] += (float)(0.7 * v);
                }
            }
            {
                var lp = DemoAudio.Biquad.PasseBas(200, 0.8); double ph = 0;
                for (int i = 0; i < fe; i++)
                {
                    double tt = i / (double)fe, env = Math.Min(1, tt / 0.01) * Math.Exp(-tt / 0.5);
                    ph += 2 * Math.PI * 45 / fe;
                    double v = env * (0.45 * Math.Sin(ph) + 0.5 * lp.Passer(bruit()) * 6);
                    v = Math.Max(-0.9, Math.Min(0.9, v));
                    int k = fe + i; t[2 * k] += (float)v; t[2 * k + 1] += (float)v;
                }
            }
            Func<double, double, double> niveau = (t0, t1) =>
            {
                double e = 0; int a = (int)(t0 * fe), b = (int)(t1 * fe);
                for (int i = a; i < b; i++) e += t[2 * i] * (double)t[2 * i] + t[2 * i + 1] * (double)t[2 * i + 1];
                return 10 * Math.Log10(e / (2.0 * (b - a)) + 1e-20);
            };
            ecartSans = niveau(1.0, 1.3) - niveau(0.3, 0.36);
            double retourSans = niveau(1.0, 1.3) - niveau(2.4, 2.46);
            var comp = new CompresseurPas(force, fe);
            comp.Traiter(t, n, 2);
            // la sortie est retardée de quelques images : on la recale pour mesurer aux mêmes instants
            int dec = comp.Retard;
            Array.Copy(t, 2 * dec, t, 0, t.Length - 2 * dec);
            ecartAvec = niveau(1.0, 1.3) - niveau(0.3, 0.36);
            retourPas = (niveau(1.0, 1.3) - niveau(2.4, 2.46)) - ecartAvec;   // ~0 : le pas d'après remonte autant que le premier
            creteAvec = 0; foreach (var v in t) creteAvec = Math.Max(creteAvec, Math.Abs(v));
        }
    }
}
