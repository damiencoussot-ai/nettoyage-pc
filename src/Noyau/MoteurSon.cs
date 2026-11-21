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
        /// <summary>« Ton arme » : 0 normale, 1 réduite, 2 très réduite (tes propres tirs baissés, ceux des ennemis gardés).</summary>
        public int Arme = 1;
        /// <summary>« Un réglage par jeu » : un réglage fait pendant une partie est retenu pour ce jeu (clé « cod »,
        /// « valorant »…) et repris à chaque fois qu'il tourne. Valeurs : force, arme.</summary>
        public bool ParJeuActif = true;
        /// <summary>Le câble s'appelait « Velox Studio » quand « Pas boostés » a été coupé (il a repris son nom d'origine) :
        /// il le retrouve au prochain allumage.</summary>
        public bool RenommerAuRallumage;
        public Dictionary<string, int[]> ParJeu = new Dictionary<string, int[]>();
        private static readonly Regex CleJeu = new Regex("^[a-z0-9]{1,24}$");

        /// <summary>Force et arme à utiliser quand ce jeu tourne ("" : aucun jeu).</summary>
        public void Effectifs(string jeu, out int force, out int arme)
        {
            int[] v;
            if (ParJeuActif && !string.IsNullOrEmpty(jeu) && ParJeu != null && ParJeu.TryGetValue(jeu, out v) && v != null && v.Length == 2) { force = v[0]; arme = v[1]; }
            else { force = Force; arme = Arme; }
        }

        public ReglagesMoteurSon Borner()
        {
            Force = Math.Max(0, Math.Min(130, Force));
            Arme = Math.Max(0, Math.Min(2, Arme));
            var pj = new Dictionary<string, int[]>();
            if (ParJeu != null)
                foreach (var kv in ParJeu)
                    if (kv.Key != null && CleJeu.IsMatch(kv.Key) && kv.Value != null && kv.Value.Length == 2 && pj.Count < 64)
                        pj[kv.Key] = new[] { Math.Max(0, Math.Min(130, kv.Value[0])), Math.Max(0, Math.Min(2, kv.Value[1])) };
            ParJeu = pj;
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
                { "Source", Source }, { "Destination", Destination }, { "DefautAvant", DefautAvant }, { "Arme", Arme },
                { "ParJeuActif", ParJeuActif }, { "RenommerAuRallumage", RenommerAuRallumage }, { "ParJeu", (ParJeu ?? new Dictionary<string, int[]>()).ToDictionary(kv => kv.Key, kv => (object)new object[] { kv.Value[0], kv.Value[1] }) },
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
                if (d.TryGetValue("Arme", out o)) r.Arme = (int)Math.Round(Convert.ToDouble(o, CultureInfo.InvariantCulture));
                if (d.TryGetValue("ParJeuActif", out o) && o is bool pa) r.ParJeuActif = pa;
                if (d.TryGetValue("RenommerAuRallumage", out o) && o is bool ra) r.RenommerAuRallumage = ra;
                if (d.TryGetValue("ParJeu", out o) && o is Dictionary<string, object> pj)
                    foreach (var kv in pj)
                    {
                        var l = new List<int>();
                        if (kv.Value is System.Collections.IEnumerable en && !(kv.Value is string))
                            foreach (var x in en) { try { l.Add((int)Math.Round(Convert.ToDouble(x, CultureInfo.InvariantCulture))); } catch { } }
                        if (l.Count == 2) r.ParJeu[kv.Key] = l.ToArray();
                    }
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

        // ---- dompteur de graves : les explosions et grondements (sous ~200 Hz, 350 Hz en Ultra) sont baissés à part, sans toucher aux
        // médiums et aigus (pas, recharges) qui jouent en même temps : un pas sous une explosion reste audible.
        public readonly double SeuilGraves, RapportGraves;
        private readonly double gb0, gb1, gb2, ga1, ga2;            // passe-bas 200 Hz (Butterworth, 2e ordre)
        private readonly double aGraves, attaqueGraves, relacheGraves;
        private double puissanceGraves = 1e-12, gainGraves;          // dB (≤ 0)
        private double[] gx1, gx2, gy1, gy2;
        public double ReductionGravesMax;                            // (relevé) plus forte baisse des graves, dB

        // ---- « ton arme » : tes propres tirs (et rechargements, pas, gadgets) sont joués au centre de l'image ; ceux des
        // ennemis viennent d'une direction. En 5.1/7.1, la voie centrale forte est baissée (un son faible au centre, comme
        // un pas ennemi devant toi, n'est pas touché) ; en stéréo, seul le « milieu » (ce qui est identique à gauche et à
        // droite) est baissé, et seulement quand il est fort et nettement centré. Un ennemi sur le côté garde tout son niveau.
        public readonly int Arme;
        public readonly double ProfondeurCentre, ProfondeurStereo;  // dB de baisse au maximum
        private readonly double aArme, attaqueArme, relacheArme;
        private double pMilieu = 1e-12, pCotes = 1e-12, pCentre = 1e-12, gainArme;   // gainArme en dB (≤ 0)
        public double ReductionArmeMax;                               // (relevé) plus forte baisse de ton arme, dB

        // ---- présence adaptative : les aigus des pas (vers 2,8 kHz) sont mis en avant seulement quand la scène est calme
        // (pas de tir, pas d'explosion) ; dès qu'un son fort arrive, la présence s'efface en 2 ms : les tirs ne deviennent
        // jamais criards (c'est ce qu'un égaliseur fixe ne sait pas faire).
        public readonly double GainPresence;                         // dB
        private readonly double pb0, pb1, pb2, pa1, pa2;
        private double[] px1, px2, py1, py2;
        // « corps » des pas (le choc du talon, vers 300 Hz), mis en avant avec la présence, à moitié de son gain
        public readonly double GainCorps;

        // ---- renforceur de pas : les sons BREFS et FAIBLES dans la bande des pas (700 à 5 000 Hz) et du choc du talon
        // (150 à 350 Hz) sont remontés pendant quelques dizaines de ms. Un son continu (vent, moteur, ambiance) n'est pas
        // bref ; un tir ou une explosion n'est pas faible : ni l'un ni l'autre n'est touché. C'est ce qui détache un pas
        // du fond, là où le compresseur remonte tout le fond pareil.
        public readonly double RenfortMax;                            // dB (bande des pas) ; moitié pour le choc
        private readonly Biquad2[] bandePasHaut, bandePasBas, bandeChocHaut, bandeChocBas;
        private readonly double rfMonte, rfDescendRapide, rfLent, rgMonte, rgDescend;
        private double pRapidePas = 1e-12, pLentPas = 1e-12, pRapideChoc = 1e-12, pLentChoc = 1e-12, gainRenfortPas, gainRenfortChoc;
        private double[] bufPas = new double[16], bufChoc = new double[16];
        public double RenfortRelevéMax;                               // (relevé) plus forte remontée d'un pas, dB

        /// <summary>Filtre du 2e ordre (un par canal), pour le renforceur.</summary>
        private sealed class Biquad2
        {
            private readonly double b0, b1, b2, a1, a2; private double x1, x2, y1, y2;
            public Biquad2(bool passeHaut, double f, double fe)
            {
                double w = 2 * Math.PI * Math.Min(f, 0.45 * fe) / fe, al = Math.Sin(w) / (2 * 0.7071), c = Math.Cos(w), a0 = 1 + al;
                if (passeHaut) { b0 = (1 + c) / 2 / a0; b1 = -(1 + c) / a0; b2 = b0; }
                else { b0 = (1 - c) / 2 / a0; b1 = (1 - c) / a0; b2 = b0; }
                a1 = -2 * c / a0; a2 = (1 - al) / a0;
            }
            public double Passer(double x) { double y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2; x2 = x1; x1 = x; y2 = y1; y1 = y; return y; }
        }

        /// <summary>(essais) Remontée du renforceur (dB) pour ces niveaux de bande, rapide et lent, au gain maximal donné.</summary>
        internal static double CibleRenfort(double rapideDb, double lentDb, double maxDb)
        {
            double bref = Math.Max(0, Math.Min(1, (rapideDb - lentDb - 2) / 6));   // un pas : le rapide dépasse le lent de 3 à 8 dB
            double faible = Math.Max(0, Math.Min(1, (-14 - rapideDb) / 10));      // plein sous −24 dB, rien au-dessus de −14 dB
            return maxDb * bref * faible;
        }
        private readonly double cb0, cb1, cb2, ca1, ca2;
        private double[] cx1, cx2, cy1, cy2;
        private double presence, pScene = 1e-12, pLfe = 1e-12;
        private readonly double presMonte, presDescend;

        public CompresseurPas(int force, int frequence) : this(force, frequence, 0) { }

        public CompresseurPas(int force, int frequence, int arme) : this(force, frequence, arme, true) { }

        /// <summary>« renfort » faux : sans renforceur de pas (essais de comparaison).</summary>
        public CompresseurPas(int force, int frequence, int arme, bool renfort)
        {
            double f = Math.Max(0, Math.Min(130, force)) / 100.0;
            double ultra = Math.Max(0, f - 1) / 0.3;   // 0 jusqu'à Extrême, 1 en Ultra
            SeuilGraves = -24 - 8 * f;                 // −24 dB (léger) à −32 dB (extrême), −34 dB (ultra)
            RapportGraves = 3 + 5 * Math.Min(1, f) + 12 * ultra;   // 3:1 à 8:1, 20:1 en Ultra
            double coupure = 200 + 150 * ultra;        // Ultra : les bas-médiums des explosions (jusqu'à 350 Hz) sont tenus aussi
            {
                double feG = Math.Max(8000, frequence), w = 2 * Math.PI * coupure / feG, al = Math.Sin(w) / (2 * 0.7071), c = Math.Cos(w), a0 = 1 + al;
                gb0 = (1 - c) / 2 / a0; gb1 = (1 - c) / a0; gb2 = gb0; ga1 = -2 * c / a0; ga2 = (1 - al) / a0;
                aGraves = Math.Exp(-1 / (0.010 * feG)); attaqueGraves = Math.Exp(-1 / (0.005 * feG)); relacheGraves = Math.Exp(-1 / (0.25 * feG));
            }
            Seuil = -14 - 26 * f;                  // −14 dB (léger) à −40 dB (extrême)
            Rapport = 1.5 + 4.5 * f;               // 1,5:1 à 6:1
            Remontee = -Seuil * (1 - 1 / Rapport) * 0.8;
            double fe = Math.Max(8000, frequence);
            aNiveau = Math.Exp(-1 / (0.005 * fe));
            attaque = Math.Exp(-1 / (0.0015 * fe));
            relache = Math.Exp(-1 / ((0.20 - 0.09 * f) * fe));          // 200 ms à 110 ms : les pas reviennent vite après un tir
            gardeMonte = Math.Exp(-1 / (0.002 * fe)); gardeDescend = Math.Exp(-1 / (0.08 * fe));
            relacheLim = Math.Exp(-1 / (0.06 * fe));
            avance = Math.Max(1, (int)(0.0015 * fe));
            aMelange = Math.Exp(-1 / (0.1 * fe));
            gainLisse = Remontee;
            // ton arme
            Arme = Math.Max(0, Math.Min(2, arme));
            ProfondeurCentre = Arme == 0 ? 0 : Arme == 1 ? 8 : 16;
            ProfondeurStereo = Arme == 0 ? 0 : Arme == 1 ? 6 : 12;
            aArme = Math.Exp(-1 / (0.003 * fe)); attaqueArme = Math.Exp(-1 / (0.001 * fe)); relacheArme = Math.Exp(-1 / (0.08 * fe));
            // présence : cloche (RBJ) à 2,8 kHz, Q 0,9 ; +2,6 dB (léger) à +4,5 dB (extrême et ultra)
            GainPresence = f <= 0 ? 0 : 2 + 4 * Math.Min(1, f);   // +3,4 dB (léger) à +6 dB (extrême et ultra)
            GainCorps = GainPresence / 2;
            {
                double A = Math.Pow(10, GainCorps / 40), w = 2 * Math.PI * 300 / Math.Max(8000, frequence), al = Math.Sin(w) / (2 * 1.0), c = Math.Cos(w), a0 = 1 + al / A;
                cb0 = (1 + al * A) / a0; cb1 = -2 * c / a0; cb2 = (1 - al * A) / a0; ca1 = -2 * c / a0; ca2 = (1 - al / A) / a0;
            }
            {
                double A = Math.Pow(10, GainPresence / 40), w = 2 * Math.PI * Math.Min(2800, 0.4 * fe) / fe, al = Math.Sin(w) / (2 * 0.9), c = Math.Cos(w), a0 = 1 + al / A;
                pb0 = (1 + al * A) / a0; pb1 = -2 * c / a0; pb2 = (1 - al * A) / a0; pa1 = -2 * c / a0; pa2 = (1 - al / A) / a0;
            }
            presMonte = Math.Exp(-1 / (0.06 * fe)); presDescend = Math.Exp(-1 / (0.002 * fe));
            // renforceur : +3 dB (léger) à +8 dB (fort), +10 dB (extrême), +12 dB (ultra)
            RenfortMax = !renfort || f <= 0 ? 0 : f <= 0.35 ? 3 * f / 0.35 : f <= 0.65 ? 3 + 5 * (f - 0.35) / 0.3 : f <= 1 ? 8 + 2 * (f - 0.65) / 0.35 : 10 + 2 * Math.Min(1, (f - 1) / 0.3);
            rfMonte = Math.Exp(-1 / (0.0007 * fe)); rfDescendRapide = Math.Exp(-1 / (0.015 * fe)); rfLent = Math.Exp(-1 / (0.06 * fe));
            rgMonte = Math.Exp(-1 / (0.002 * fe)); rgDescend = Math.Exp(-1 / (0.07 * fe));
            bandePasHaut = new Biquad2[16]; bandePasBas = new Biquad2[16]; bandeChocHaut = new Biquad2[16]; bandeChocBas = new Biquad2[16];
            for (int c = 0; c < 16; c++)
            {
                bandePasHaut[c] = new Biquad2(true, 700, fe); bandePasBas[c] = new Biquad2(false, 5000, fe);
                bandeChocHaut[c] = new Biquad2(true, 150, fe); bandeChocBas[c] = new Biquad2(false, 350, fe);
            }
        }

        private static double Borne01(double x) { return x < 0 ? 0 : x > 1 ? 1 : x; }

        private void LisserArme(double cible)
        {
            gainArme = cible < gainArme ? attaqueArme * gainArme + (1 - attaqueArme) * cible : relacheArme * gainArme + (1 - relacheArme) * cible;
            if (-gainArme > ReductionArmeMax) ReductionArmeMax = -gainArme;
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
                gx1 = new double[canaux]; gx2 = new double[canaux]; gy1 = new double[canaux]; gy2 = new double[canaux];
                px1 = new double[canaux]; px2 = new double[canaux]; py1 = new double[canaux]; py2 = new double[canaux];
                cx1 = new double[canaux]; cx2 = new double[canaux]; cy1 = new double[canaux]; cy2 = new double[canaux];
                for (int k = 0; k < besoin.Length; k++) besoin[k] = 1;
            }
            int lg = avance + 1;
            for (int i = 0; i < images; i++)
            {
                int b = i * canaux;
                // 0. son d'origine gardé (pour « seulement en jeu »), puis dompteur de graves : graves séparés (passe-bas
                //    200 Hz), baissés selon leur propre niveau, recombinés avec le reste intact (somme exacte si gain 0 dB)
                // un échantillon invalide (pilote en défaut) empoisonnerait tous les filtres pour toujours : remplacé par du silence
                for (int c = 0; c < canaux; c++) { float v0 = t[b + c]; if (float.IsNaN(v0) || float.IsInfinity(v0)) t[b + c] = 0; sec[pos * canaux + c] = t[b + c]; }
                // 0 bis. ton arme (avant tout le reste : le compresseur ne voit plus tes tirs, il remonte mieux le reste)
                if (Arme > 0 && canaux >= 2)
                {
                    double l = t[b], r = t[b + 1], milieu = (l + r) * 0.5, cotes = (l - r) * 0.5;
                    if (canaux >= 6)
                    {
                        // ordre Windows : avant gauche, avant droite, centre, caisson de basses, puis arrières et côtés
                        double fc = t[b + 2];
                        pCentre = aArme * pCentre + (1 - aArme) * fc * fc;
                        double nc = 10 * Math.Log10(pCentre + 1e-18);
                        LisserArme(-ProfondeurCentre * Borne01((nc + 36) / 8));   // rien sous −36 dB, tout dès −28 dB
                        t[b + 2] = (float)(fc * Math.Pow(10, gainArme / 20));
                        milieu *= Math.Pow(10, gainArme * 0.5 / 20);                // la copie « fantôme » du centre dans l'avant
                    }
                    else
                    {
                        pMilieu = aArme * pMilieu + (1 - aArme) * milieu * milieu;
                        pCotes = aArme * pCotes + (1 - aArme) * cotes * cotes;
                        double nm = 10 * Math.Log10(pMilieu + 1e-18), ns = 10 * Math.Log10(pCotes + 1e-18);
                        LisserArme(-ProfondeurStereo * Borne01((nm - ns - 9) / 6) * Borne01((nm + 24) / 6));   // fort ET centré
                        milieu *= Math.Pow(10, gainArme / 20);
                    }
                    t[b] = (float)(milieu + cotes); t[b + 1] = (float)(milieu - cotes);
                }
                float pg = 0;
                for (int c = 0; c < canaux; c++)
                {
                    float v = t[b + c];
                    double lo = gb0 * v + gb1 * gx1[c] + gb2 * gx2[c] - ga1 * gy1[c] - ga2 * gy2[c];
                    gx2[c] = gx1[c]; gx1[c] = v; gy2[c] = gy1[c]; gy1[c] = lo;
                    if (lo * lo > pg) pg = (float)(lo * lo);
                }
                puissanceGraves = aGraves * puissanceGraves + (1 - aGraves) * pg;
                double nivG = 10 * Math.Log10(puissanceGraves + 1e-18), cibleG = 0;
                if (nivG > SeuilGraves) cibleG = (SeuilGraves + (nivG - SeuilGraves) / RapportGraves) - nivG;
                gainGraves = cibleG < gainGraves ? attaqueGraves * gainGraves + (1 - attaqueGraves) * cibleG : relacheGraves * gainGraves + (1 - relacheGraves) * cibleG;
                if (-gainGraves > ReductionGravesMax) ReductionGravesMax = -gainGraves;
                double kg = Math.Pow(10, gainGraves / 20) - 1;   // (graves × k) = graves + graves × (k − 1)
                for (int c = 0; c < canaux; c++) t[b + c] = (float)(t[b + c] + gy1[c] * kg);
                // 0 ter. présence adaptative : seulement quand la scène est calme (ni son fort, ni graves d'explosion, ni caisson)
                if (GainPresence > 0)
                {
                    float ps = 0;
                    for (int c = 0; c < canaux; c++) { float v = t[b + c]; if (v * v > ps) ps = v * v; }
                    pScene = aNiveau * pScene + (1 - aNiveau) * ps;
                    bool calme = 10 * Math.Log10(pScene + 1e-18) < -22 && gainGraves > -3;
                    if (canaux >= 6) { double lf = t[b + 3]; pLfe = aGraves * pLfe + (1 - aGraves) * lf * lf; calme &= 10 * Math.Log10(pLfe + 1e-18) < -35; }
                    presence = calme ? presMonte * presence + (1 - presMonte) : presDescend * presence;
                    for (int c = 0; c < canaux; c++)
                    {
                        double x = t[b + c];
                        double y = pb0 * x + pb1 * px1[c] + pb2 * px2[c] - pa1 * py1[c] - pa2 * py2[c];
                        px2[c] = px1[c]; px1[c] = x; py2[c] = py1[c]; py1[c] = y;
                        double z = cb0 * y + cb1 * cx1[c] + cb2 * cx2[c] - ca1 * cy1[c] - ca2 * cy2[c];
                        cx2[c] = cx1[c]; cx1[c] = y; cy2[c] = cy1[c]; cy1[c] = z;
                        y = z;
                        if (canaux >= 6 && c == 3) continue;   // pas de présence sur le caisson de basses
                        t[b + c] = (float)(x + (y - x) * presence);
                    }
                }
                // 0 quater. renforceur de pas (bandes extraites, niveau rapide contre lent : un son bref et faible est remonté)
                if (RenfortMax > 0)
                {
                    double pp = 0, pc = 0;
                    for (int c = 0; c < canaux && c < 16; c++)
                    {
                        if (canaux >= 6 && c == 3) { bufPas[c] = 0; bufChoc[c] = 0; continue; }
                        double x = t[b + c];
                        double bp = bandePasBas[c].Passer(bandePasHaut[c].Passer(x)), bc = bandeChocBas[c].Passer(bandeChocHaut[c].Passer(x));
                        bufPas[c] = bp; bufChoc[c] = bc;
                        if (bp * bp > pp) pp = bp * bp; if (bc * bc > pc) pc = bc * bc;
                    }
                    pRapidePas = pp > pRapidePas ? rfMonte * pRapidePas + (1 - rfMonte) * pp : rfDescendRapide * pRapidePas + (1 - rfDescendRapide) * pp;
                    pLentPas = rfLent * pLentPas + (1 - rfLent) * pp;
                    pRapideChoc = pc > pRapideChoc ? rfMonte * pRapideChoc + (1 - rfMonte) * pc : rfDescendRapide * pRapideChoc + (1 - rfDescendRapide) * pc;
                    pLentChoc = rfLent * pLentChoc + (1 - rfLent) * pc;
                    double cp = CibleRenfort(10 * Math.Log10(pRapidePas + 1e-18), 10 * Math.Log10(pLentPas + 1e-18), RenfortMax);
                    double cc = CibleRenfort(10 * Math.Log10(pRapideChoc + 1e-18), 10 * Math.Log10(pLentChoc + 1e-18), RenfortMax / 2);
                    if (10 * Math.Log10(pRapidePas + 1e-18) < -70) cp = 0;   // souffle : rien
                    if (10 * Math.Log10(pRapideChoc + 1e-18) < -70) cc = 0;
                    gainRenfortPas = cp > gainRenfortPas ? rgMonte * gainRenfortPas + (1 - rgMonte) * cp : rgDescend * gainRenfortPas + (1 - rgDescend) * cp;
                    gainRenfortChoc = cc > gainRenfortChoc ? rgMonte * gainRenfortChoc + (1 - rgMonte) * cc : rgDescend * gainRenfortChoc + (1 - rgDescend) * cc;
                    if (gainRenfortPas > RenfortRelevéMax) RenfortRelevéMax = gainRenfortPas;
                    double kp = Math.Pow(10, gainRenfortPas / 20) - 1, kc = Math.Pow(10, gainRenfortChoc / 20) - 1;
                    for (int c = 0; c < canaux && c < 16; c++) t[b + c] = (float)(t[b + c] + bufPas[c] * kp + bufChoc[c] * kc);
                }
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
                for (int c = 0; c < canaux; c++) { float v = t[b + c] * g; retard[pos * canaux + c] = v; if (Math.Abs(v) > crete) crete = Math.Abs(v); }
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

    /// <summary>
    /// Réécoute « avant / après » : les dernières secondes (10 au plus) sont gardées deux fois, telles que le jeu les a
    /// jouées et telles que Velox les a traitées. Sur demande, elles sont rejouées : d'abord sans Velox, une seconde de
    /// silence, puis avec Velox, au même volume moyen (le plus fort des deux est baissé : à l'oreille, « plus fort »
    /// paraît toujours « mieux », la comparaison serait faussée). Pendant la réécoute, rien n'est gardé.
    /// </summary>
    internal sealed class MemoireReecoute
    {
        public readonly int Canaux, Frequence, Capacite;
        private readonly float[] sec, traite;
        private int pos, plein;
        /// <summary>0 : rien ; 1 : « sans Velox » ; 2 : silence ; 3 : « avec Velox ».</summary>
        public int Phase { get; private set; }
        private long lu, longueur, depart, pause; private float kSec = 1, kTraite = 1;
        /// <summary>(relevé) gains appliqués pour le même volume, sans puis avec.</summary>
        public float GainSec { get { return kSec; } }
        public float GainTraite { get { return kTraite; } }

        public MemoireReecoute(int canaux, int frequence, int secondes)
        {
            Canaux = Math.Max(1, canaux); Frequence = Math.Max(8000, frequence);
            // au plus 4 millions d'échantillons par anneau (10 s en 7.1 à 48 kHz ; moins à 192 kHz ou en 16 canaux)
            Capacite = Math.Max(2 * Frequence, Math.Min(Frequence * Math.Max(1, secondes), 4000000 / Canaux));
            sec = new float[Capacite * Canaux]; traite = new float[Capacite * Canaux];
        }

        /// <summary>Secondes gardées (pleines).</summary>
        public int Secondes { get { return plein / Frequence; } }

        /// <summary>Secondes restantes de la réécoute en cours (les deux écoutes comprises).</summary>
        public int SecondesRestantes
        {
            get
            {
                if (Phase == 1) return (int)Math.Ceiling((longueur - lu) / (double)Frequence) + 1 + (int)Math.Ceiling(longueur / (double)Frequence);
                if (Phase == 2) return (int)Math.Ceiling((pause + longueur) / (double)Frequence);
                if (Phase == 3) return (int)Math.Ceiling((longueur - lu) / (double)Frequence);
                return 0;
            }
        }

        private void Garder(float[] dest, float[] t, int images, int debut)
        {
            for (int i = 0; i < images; i++) Array.Copy(t, i * Canaux, dest, ((debut + i) % Capacite) * Canaux, Canaux);
        }

        /// <summary>Garde le son d'origine de ce paquet (avant traitement).</summary>
        public void GarderSec(float[] t, int images) { if (Phase == 0) Garder(sec, t, images, pos); }

        /// <summary>Garde le son traité du même paquet, et avance.</summary>
        public void GarderTraite(float[] t, int images)
        {
            if (Phase != 0) return;
            Garder(traite, t, images, pos);
            pos = (pos + images) % Capacite; plein = Math.Min(Capacite, plein + images);
        }

        /// <summary>Lance la réécoute (au moins 2 s gardées). Vrai si elle démarre.</summary>
        public bool Lancer()
        {
            if (Phase != 0 || plein < 2 * Frequence) return false;
            longueur = plein; depart = (pos - plein + Capacite) % Capacite;
            double es = 0, et = 0;
            for (long i = 0; i < longueur; i++)
            {
                long j = ((depart + i) % Capacite) * Canaux;
                for (int c = 0; c < Canaux; c++) { es += sec[j + c] * (double)sec[j + c]; et += traite[j + c] * (double)traite[j + c]; }
            }
            kSec = 1; kTraite = 1;
            if (es > 1e-12 && et > 1e-12)
            {
                float k = (float)Math.Sqrt(Math.Min(es, et) / Math.Max(es, et));
                if (et > es) kTraite = k; else kSec = k;
            }
            Phase = 1; lu = 0;
            return true;
        }

        public void Arreter() { if (Phase != 0) { Phase = 0; plein = 0; } }

        /// <summary>
        /// Enregistre les secondes gardées en deux fichiers WAV (16 bits), sans puis avec Velox, tels quels (pas au même
        /// volume) : pour analyser le vrai son d'un jeu. Noms : « prefixe-sans.wav » et « prefixe-avec.wav ». Faux s'il y a
        /// moins de 2 s gardées.
        /// </summary>
        public bool Exporter(string prefixe)
        {
            if (Phase != 0 || plein < 2 * Frequence) return false;
            long debut = (pos - plein + Capacite) % Capacite;
            foreach (var x in new[] { Tuple.Create("-sans.wav", sec), Tuple.Create("-avec.wav", traite) })
            {
                int octets = plein * Canaux * 2;
                using (var f = new FileStream(prefixe + x.Item1, FileMode.Create, FileAccess.Write))
                using (var w = new BinaryWriter(f))
                {
                    w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + octets); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                    w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)Canaux); w.Write(Frequence);
                    w.Write(Frequence * Canaux * 2); w.Write((short)(Canaux * 2)); w.Write((short)16);
                    w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(octets);
                    for (long i = 0; i < plein; i++)
                    {
                        long j = ((debut + i) % Capacite) * Canaux;
                        for (int c = 0; c < Canaux; c++) w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, x.Item2[j + c])) * 32767));
                    }
                }
            }
            return true;
        }

        /// <summary>Remplace ce paquet par la suite de la réécoute (silence une fois finie).</summary>
        public void Remplir(float[] t, int images)
        {
            for (int i = 0; i < images; i++)
            {
                int b = i * Canaux;
                if (Phase == 2)
                {
                    for (int c = 0; c < Canaux; c++) t[b + c] = 0;
                    if (--pause <= 0) { Phase = 3; lu = 0; }
                    continue;
                }
                if (Phase == 0) { for (int c = 0; c < Canaux; c++) t[b + c] = 0; continue; }
                long j = ((depart + lu) % Capacite) * Canaux;
                float[] src = Phase == 1 ? sec : traite; float k = Phase == 1 ? kSec : kTraite;
                for (int c = 0; c < Canaux; c++) t[b + c] = src[j + c] * k;
                if (++lu >= longueur)
                {
                    if (Phase == 1) { Phase = 2; pause = Frequence; }   // une seconde de silence entre les deux
                    else { Phase = 0; plein = 0; }                      // fin : on repart sur du son neuf
                }
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
        /// <summary>Demande de réécoute (« go » ou « stop »), lue puis effacée par le moteur.</summary>
        public static string FichierReecoute { get { return Path.Combine(Chemins.Local, "moteur-son-reecoute.txt"); } }

        /// <summary>Demande au moteur de rejouer les dernières secondes (sans puis avec Velox), ou d'arrêter (« stop »).</summary>
        public static string DemanderReecoute(bool lancer)
        {
            try { Directory.CreateDirectory(Chemins.Local); File.WriteAllText(FichierReecoute, lancer ? "go" : "stop"); return ""; }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Demande au moteur d'enregistrer les dernières secondes (sans et avec Velox) sur le Bureau.</summary>
        public static string DemanderExport()
        {
            try { Directory.CreateDirectory(Chemins.Local); File.WriteAllText(FichierReecoute, "export"); return ""; }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Préréglages proposés dans la page.</summary>
        public static readonly Tuple<string, int>[] Niveaux = { Tuple.Create("Léger", 35), Tuple.Create("Fort", 65), Tuple.Create("Extrême", 100), Tuple.Create("Ultra", 130) };

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
        /// <summary>
        /// (essais) Un pas (bande 1,5 à 5 kHz, −40 dB) qui tombe pendant une explosion forte : part du pas dans le son, en
        /// dB, sans puis avec le traitement (mesurée par corrélation avec le pas seul). Plus elle monte, plus le pas ressort.
        /// </summary>
        public static void MesurerSousExplosion(int force, out double partSans, out double partAvec)
        {
            int fe = DemoAudio.Frequence, n = 2 * fe;
            var pas = new float[n * 2]; var expl = new float[n * 2];
            var alea = new Random(11);
            Func<double> bruit = () => alea.NextDouble() * 2 - 1;
            var bp = DemoAudio.Biquad.PasseBande(2800, 1.2);
            int d0 = (int)(0.6 * fe);
            for (int i = 0; i < (int)(0.08 * fe); i++)
            {
                double tt = i / (double)fe, v = 0.03 * Math.Min(1, tt / 0.003) * Math.Exp(-tt / 0.03) * bp.Passer(bruit()) * 3;
                pas[2 * (d0 + i)] = (float)v; pas[2 * (d0 + i) + 1] = (float)v;
            }
            var lp = DemoAudio.Biquad.PasseBas(150, 0.8); double ph = 0;
            for (int i = 0; i < (int)(1.4 * fe); i++)
            {
                double tt = i / (double)fe, env = Math.Min(1, tt / 0.01) * Math.Exp(-tt / 0.6);
                ph += 2 * Math.PI * 45 / fe;
                double v = Math.Max(-0.9, Math.Min(0.9, env * (0.45 * Math.Sin(ph) + 0.5 * lp.Passer(bruit()) * 6)));
                int k = (int)(0.2 * fe) + i; expl[2 * k] = (float)v; expl[2 * k + 1] = (float)v;
            }
            var mix = new float[n * 2];
            for (int i = 0; i < mix.Length; i++) mix[i] = pas[i] + expl[i];
            Func<float[], double> part = y =>
            {
                // énergie de la composante « pas » (projection sur le pas seul) / énergie totale, sur la fenêtre du pas
                int a = 2 * d0, b = 2 * (d0 + (int)(0.08 * fe));
                double pr = 0, pp = 0, tot = 0;
                for (int i = a; i < b; i++) { pr += y[i] * (double)pas[i]; pp += pas[i] * (double)pas[i]; tot += y[i] * (double)y[i]; }
                double k = pr / Math.Max(1e-20, pp), ep = k * k * pp;
                return 10 * Math.Log10(ep / Math.Max(1e-20, tot) + 1e-20);
            };
            partSans = part(mix);
            var y2 = (float[])mix.Clone();
            var comp = new CompresseurPas(force, fe);
            comp.Traiter(y2, n, 2);
            int dec = comp.Retard;
            Array.Copy(y2, 2 * dec, y2, 0, y2.Length - 2 * dec);
            partAvec = part(y2);
        }

        /// <summary>
        /// (essais) « Ton arme » : tu tires en rafale (son fort, au centre) pendant qu'un ennemi marche sur ta gauche (pas
        /// faible). Part du pas ennemi dans le son (dB), traitement force 65 sans puis avec « ton arme » au niveau donné.
        /// En stéréo (8 canaux faux : false) le pas est à gauche seulement ; en 7.1, tes tirs sont sur la voie centrale
        /// (avec une copie plus faible à l'avant) et le pas sur le côté gauche.
        /// </summary>
        public static void MesurerArme(int arme, bool septUn, out double partSans, out double partAvec, out float crete)
        {
            int fe = DemoAudio.Frequence, n = (int)(1.4 * fe), k = septUn ? 8 : 2;
            var pas = new float[n * k]; var tirs = new float[n * k];
            var alea = new Random(5);
            Func<double> bruit = () => alea.NextDouble() * 2 - 1;
            var bp = DemoAudio.Biquad.PasseBande(2800, 1.2);
            int d0 = (int)(0.6 * fe), lgPas = (int)(0.08 * fe);
            for (int i = 0; i < lgPas; i++)
            {
                double tt = i / (double)fe, v = 0.03 * Math.Min(1, tt / 0.003) * Math.Exp(-tt / 0.03) * bp.Passer(bruit()) * 3;
                pas[(d0 + i) * k + (septUn ? 6 : 0)] = (float)v;
            }
            var hp = DemoAudio.Biquad.PasseHaut(150, 0.7);
            for (double debut = 0.2; debut < 1.2; debut += 0.09)
                for (int i = 0; i < (int)(0.06 * fe); i++)
                {
                    double tt = i / (double)fe, v = 0.7 * Math.Exp(-tt / 0.02) * hp.Passer(bruit());
                    int j = ((int)(debut * fe) + i) * k;
                    if (septUn) { tirs[j + 2] += (float)v; tirs[j] += (float)(0.3 * v); tirs[j + 1] += (float)(0.3 * v); }
                    else { tirs[j] += (float)v; tirs[j + 1] += (float)v; }
                }
            Func<float[], double> part = y =>
            {
                int a = d0 * k, b = (d0 + lgPas) * k;
                double pr = 0, pp = 0, tot = 0;
                for (int i = a; i < b; i++) { pr += y[i] * (double)pas[i]; pp += pas[i] * (double)pas[i]; tot += y[i] * (double)y[i]; }
                double kk = pr / Math.Max(1e-20, pp), ep = kk * kk * pp;
                return 10 * Math.Log10(ep / Math.Max(1e-20, tot) + 1e-20);
            };
            crete = 0;
            double[] res = new double[2];
            for (int essai = 0; essai < 2; essai++)
            {
                var y = new float[n * k];
                for (int i = 0; i < y.Length; i++) y[i] = pas[i] + tirs[i];
                var comp = new CompresseurPas(65, fe, essai == 0 ? 0 : arme);
                comp.Traiter(y, n, k);
                int dec = comp.Retard;
                Array.Copy(y, k * dec, y, 0, y.Length - k * dec);
                res[essai] = part(y);
                foreach (var v in y) crete = Math.Max(crete, Math.Abs(v));
            }
            partSans = res[0]; partAvec = res[1];
        }

        /// <summary>
        /// (essais) Renforceur de pas : des pas faibles (−40 dB) sur une ambiance continue (vent, −32 dB). Part des pas dans
        /// le son (dB), force donnée, traitement complet sans puis avec le renforceur ; et crête d'une rafale forte, sans puis
        /// avec (le renforceur ne doit pas la toucher).
        /// </summary>
        public static void MesurerRenfort(int force, out double partSans, out double partAvec, out double creteSans, out double creteAvec)
        {
            int fe = DemoAudio.Frequence, n = 3 * fe;
            var pas = new float[n * 2]; var fond = new float[n * 2];
            var alea = new Random(17);
            Func<double> bruit = () => alea.NextDouble() * 2 - 1;
            // ambiance : grondement et vent, surtout dans les graves (comme dans un jeu), un peu de souffle dans les aigus
            var lp = DemoAudio.Biquad.PasseBas(500, 0.7); var hp = DemoAudio.Biquad.PasseHaut(3000, 0.7);
            for (int i = 0; i < n; i++) { float v = (float)(0.03 * lp.Passer(bruit()) * 3 + 0.002 * hp.Passer(bruit())); fond[2 * i] = v; fond[2 * i + 1] = v * 0.8f; }
            foreach (double debut in new[] { 0.5, 1.0, 1.5, 2.0 })
            {
                var bp = DemoAudio.Biquad.PasseBande(2200, 1.0);
                for (int i = 0; i < (int)(0.06 * fe); i++)
                {
                    double tt = i / (double)fe, v = 0.012 * Math.Min(1, tt / 0.002) * Math.Exp(-tt / 0.02) * bp.Passer(bruit()) * 3;
                    int k = (int)(debut * fe) + i; pas[2 * k] += (float)v; pas[2 * k + 1] += (float)(0.6 * v);
                }
            }
            Func<float[], double> part = y =>
            {
                double pr = 0, pp = 0, tot = 0;
                for (int i = (int)(0.4 * fe) * 2; i < y.Length; i++) { pr += y[i] * (double)pas[i]; pp += pas[i] * (double)pas[i]; tot += y[i] * (double)y[i]; }
                double k = pr / Math.Max(1e-20, pp), ep = k * k * pp;
                return 10 * Math.Log10(ep / Math.Max(1e-20, tot) + 1e-20);
            };
            var res = new double[2]; var cretes = new double[2];
            for (int essai = 0; essai < 2; essai++)
            {
                var y = new float[n * 2];
                for (int i = 0; i < y.Length; i++) y[i] = pas[i] + fond[i];
                var comp = new CompresseurPas(force, fe, 0, essai == 1);
                comp.Traiter(y, n, 2);
                int dec = comp.Retard; Array.Copy(y, 2 * dec, y, 0, y.Length - 2 * dec);
                res[essai] = part(y);
                // rafale forte seule (tir) : crête de sortie
                var r = new float[fe * 2]; var hpTir = DemoAudio.Biquad.PasseHaut(800, 0.7);
                for (int i = 0; i < (int)(0.05 * fe); i++) { float v = (float)(0.5 * Math.Exp(-i / (0.01 * fe)) * hpTir.Passer(bruit())); r[2 * (fe / 4 + i)] = v; r[2 * (fe / 4 + i) + 1] = v; }
                var c2 = new CompresseurPas(force, fe, 0, essai == 1); c2.Traiter(r, fe, 2);
                cretes[essai] = r.Max(x => Math.Abs((double)x));
            }
            partSans = res[0]; partAvec = res[1]; creteSans = cretes[0]; creteAvec = cretes[1];
        }

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
