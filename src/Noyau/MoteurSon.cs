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
        /// <summary>Style du traitement : 1 « Puissant » (tout plus fort, pas très en avant : forte compression), 0 « Net »
        /// (les pas gardent leur relief, le fond monte peu).</summary>
        public int Style = 1;
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
            Style = Math.Max(0, Math.Min(1, Style));
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
                { "Source", Source }, { "Destination", Destination }, { "DefautAvant", DefautAvant }, { "Arme", Arme }, { "Style", Style },
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
                if (d.TryGetValue("Style", out o)) r.Style = (int)Math.Round(Convert.ToDouble(o, CultureInfo.InvariantCulture));
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
        private readonly Biquad2[] bandePasHaut, ajoutPasHaut, bandePasBas, bandeChocHaut, bandeChocBas;
        private readonly double rfMonte, rfDescendRapide, rfLent, rgMonte, rgDescend, rfCrete;
        private double crPas, crChoc;
        private readonly double forceRel, facteurChoc;
        /// <summary>Largeur de la rampe « son bref » (dB) : 6 en Net, 4 en Puissant.</summary>
        private readonly double largeurBref;
        /// <summary>Puissant : le renfort est réduit seul avant d'atteindre cette valeur (le limiteur, à 0,95, ne bouge pas).</summary>
        internal const double MargeRenfort = 0.85;
        private double kRenfort = 1;
        private readonly double relacheRenfort;
        private readonly bool renfortApres;
        private readonly double[] ajoutRenfort = new double[16];

        // côté du son (bande des pas) : niveau à gauche et à droite, pour remonter surtout ce qui vient d'un côté (un
        // ennemi) plutôt que ce qui est au centre (tes propres pas, tes mouvements, ton rechargement)
        private double pGauche = 1e-12, pDroite = 1e-12;
        // réglés sur de vrais enregistrements de Warzone : un son bref au centre n'est pas remonté (souvent tes propres pas
        // ou mouvements), un son nettement d'un côté l'est pleinement, et la différence gauche/droite des sons de la bande des
        // pas est accentuée de 4 dB (en Extrême) : +2 à +3 dB d'écart en faveur de ce qui vient d'un côté.
        internal const double EcartPlein = 8;
        /// <summary>Remontée d'un son bref au centre : 0 en Net ; 1 en Puissant (un ennemi juste derrière ou devant arrive
        /// aussi au centre, en stéréo : il est remonté pleinement ; la traîne des tirs est écartée autrement).</summary>
        private readonly double poidsCentre;
        /// <summary>Accent des côtés (dB, en Extrême) : 4 en Net, 6 en Puissant (direction plus nette).</summary>
        private readonly double accentCotes;

        /// <summary>Poids de la remontée selon le côté : PoidsCentre pour un son centré (gauche = droite), 1 dès que l'un
        /// des côtés dépasse l'autre de EcartPlein dB.</summary>
        internal static double PoidsCote(double gaucheDb, double droiteDb, double poidsCentre)
        {
            return poidsCentre + (1 - poidsCentre) * Math.Max(0, Math.Min(1, Math.Abs(gaucheDb - droiteDb) / EcartPlein));
        }
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

        /// <summary>Part « son bref » (0 à 1) : le niveau rapide dépasse le lent de 6 à 12 dB (un pas ; le souffle, non).</summary>
        internal static double Bref(double rapideDb, double lentDb) { return Bref(rapideDb, lentDb, 6); }

        /// <summary>Même chose, pleine dès « 6 + largeur » dB : 6 en Net ; 4 en Puissant (un pas net, qui dépasse le fond de
        /// 10 dB, est remonté en entier ; un frémissement du fond de 6 à 7 dB l'est à peine).</summary>
        internal static double Bref(double rapideDb, double lentDb, double largeur) { return Math.Max(0, Math.Min(1, (rapideDb - lentDb - 6) / largeur)); }

        /// <summary>Part « son faible » (0 à 1) : plein sous −22 dB, rien au-dessus de −13 dB (un tir). Appliquée sans retard :
        /// dès qu'un son devient fort, il n'est plus remonté (même si la remontée venait de commencer sur son attaque).</summary>
        internal static double Faible(double rapideDb) { return Math.Max(0, Math.Min(1, (-13 - rapideDb) / 9)); }
        private readonly double cb0, cb1, cb2, ca1, ca2;
        private double[] cx1, cx2, cy1, cy2;
        private double presence, pScene = 1e-12, pLfe = 1e-12;
        private readonly double presMonte, presDescend;

        public CompresseurPas(int force, int frequence) : this(force, frequence, 0) { }

        public CompresseurPas(int force, int frequence, int arme) : this(force, frequence, arme, true) { }

        /// <summary>« renfort » faux : sans renforceur de pas (essais de comparaison).</summary>
        public const int StyleNet = 0, StylePuissant = 1;
        public readonly int Style;
        // Réglage du style Puissant (5.24), mesuré sur 20 enregistrements réels de Warzone (22, 23 et 24/11) passés dans ce
        // moteur. Avec le renforceur tenu 40 ms, un pas ne ressortait du fond que de +4 dB : le renfort restait allumé sur le
        // fond après chaque pas (le « pompage »), il fallait donc le garder faible. Relâché en 6 ms et plus franc (rampe
        // « bref » de 4 dB, +22 dB en Fort), il ne remonte que le pas : +4,5 dB de relief en Fort (centre, 24 enregistrements,
        // contre +2,1 en 5.23), un fond qui bouge moins (1,05 dB contre 1,11), sans changer trop de petits sons en « tic ».
        /// <summary>Puissant : seuil du compresseur principal relevé de 12 dB (et remontée baissée d'autant) : à plein niveau,
        /// le seuil d'origine écrasait les pas (relief 16 → 8 dB, fond remonté de 18 dB).</summary>
        public const double DecalagePuissant = 12;
        /// <summary>Puissant : au-delà de Fort, la compression du fond n'augmente plus ; Extrême et Ultra renforcent les pas
        /// eux-mêmes. Mesuré sur 20 enregistrements (25/11) : comprimer plus fort en Extrême écrasait les pas proches (−5 dB de
        /// relief) et pompait davantage (1,7 dB de modulation du fond contre 1,5).</summary>
        public const double ForceCompressionMax = 0.65;
        private readonly double seuilArmeStereo, seuilArmeCentre;

        public CompresseurPas(int force, int frequence, int arme, bool renfort) : this(force, frequence, arme, renfort, StylePuissant) { }

        public CompresseurPas(int force, int frequence, int arme, bool renfort, int style)
        {
            Style = style == StyleNet ? StyleNet : StylePuissant;
            double f = Math.Max(0, Math.Min(130, force)) / 100.0;
            forceRel = f;
            double ultra = Math.Max(0, f - 1) / 0.3;   // 0 jusqu'à Extrême, 1 en Ultra
            SeuilGraves = -24 - 8 * f;                 // −24 dB (léger) à −32 dB (extrême), −34 dB (ultra)
            RapportGraves = 3 + 5 * Math.Min(1, f) + 12 * ultra;   // 3:1 à 8:1, 20:1 en Ultra
            double coupure = 200 + 150 * ultra;        // Ultra : les bas-médiums des explosions (jusqu'à 350 Hz) sont tenus aussi
            {
                double feG = Math.Max(8000, frequence), w = 2 * Math.PI * coupure / feG, al = Math.Sin(w) / (2 * 0.7071), c = Math.Cos(w), a0 = 1 + al;
                gb0 = (1 - c) / 2 / a0; gb1 = (1 - c) / a0; gb2 = gb0; ga1 = -2 * c / a0; ga2 = (1 - al) / a0;
                aGraves = Math.Exp(-1 / (0.010 * feG)); attaqueGraves = Math.Exp(-1 / (0.005 * feG)); relacheGraves = Math.Exp(-1 / (0.25 * feG));
            }
            // réglé sur de vrais enregistrements de Warzone : un pas proche arrive vers −20 dB ; un seuil plus bas (ancien
            // réglage, −31 dB en Fort) l'écrasait comme une explosion et remontait le fond de 20 dB : les pas s'y noyaient.
            // Seuil au-dessus des pas, rapport modéré, remontée limitée : le fond ne monte que de 3 à 8 dB.
            // « Net » : seuil au-dessus des pas, remontée limitée. « Puissant » : forte compression, tout est remonté (le
            // style préféré à l'oreille par les testeurs : les pas sont plus forts dans l'absolu, le fond aussi).
            if (Style == StyleNet) { Seuil = -6 - 8 * f; Rapport = 1.5 + 3 * f; Remontee = -Seuil * (1 - 1 / Rapport) * 0.4; }
            else
            {
                // Puissant : la compression du fond s'arrête à Fort ; au-delà (Extrême, Ultra), la force agit sur les pas
                // eux-mêmes (renforceur, présence), pas sur le fond. Mesuré sur de vrais enregistrements : en comprimant
                // plus fort, Extrême remontait le fond de 18 dB et les pas y perdaient la moitié de leur relief.
                double fc = Math.Min(f, ForceCompressionMax);
                Seuil = -14 - 26 * fc; Rapport = 1.5 + 4.5 * fc; Remontee = -Seuil * (1 - 1 / Rapport) * 0.8;
                if (f > 0) { Seuil += DecalagePuissant; Remontee -= DecalagePuissant; }
            }
            seuilArmeStereo = Style == StyleNet ? 14 : 24; seuilArmeCentre = Style == StyleNet ? 26 : 36;
            double fe = Math.Max(8000, frequence);
            aNiveau = Math.Exp(-1 / (0.005 * fe));
            // Puissant : l'attaque du pas passe avant que le compresseur réagisse (10 ms : un pas qui claque, plus précis ;
            // le limiteur tient les crêtes), et le renforceur s'ajoute après le compresseur, qui ne l'annule plus
            attaque = Math.Exp(-1 / ((Style == StyleNet ? 0.0015 : 0.010) * fe));
            renfortApres = Style == StylePuissant;
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
            // présence « seulement au calme » retirée : sur de vrais enregistrements, elle remontait le souffle du fond (qui est
            // « calme ») et pas le pas (qui est « fort ») ; le renforceur de pas fait mieux ce travail.
            GainPresence = Style == StyleNet || f <= 0 ? 0 : 2 + 4 * Math.Min(1, f);   // Puissant : +3,4 dB (léger) à +6 dB (extrême)
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
            // renforceur : +4,5 dB (léger) à +12 dB (fort), +15 dB (extrême), +18 dB (ultra)
            // Puissant : plus fort (×1,8, réglé sur de vrais enregistrements de Warzone), plus net (relâché en 40 ms : le « clac » du pas plutôt que sa traîne), bande
            // jusqu'à 8 kHz (là où l'oreille situe gauche/droite) et « boum » du talon moins remonté (les graves brouillent la direction)
            accentCotes = Style == StyleNet ? 4 : 6;
            poidsCentre = Style == StyleNet ? 0 : 1;
            facteurChoc = Style == StyleNet ? 0.5 : 0.25;
            // Puissant ×2,7 : +22 dB en Fort. Supportable parce que le renforceur se relâche en 6 ms (ci-dessous) : il ne
            // remonte que le pas lui-même, plus le fond qui le suit. ×3,25 (+26 dB) donnait 1 dB de relief en plus mais
            // changeait trop de petits sons secs en « tic » (mesuré sur les enregistrements du 26/11 : −36 % de tics à ×2,7).
            RenfortMax = (Style == StyleNet ? 1.5 : 2.7) * (!renfort || f <= 0 ? 0 : f <= 0.35 ? 3 * f / 0.35 : f <= 0.65 ? 3 + 5 * (f - 0.35) / 0.3 : f <= 1 ? 8 + 2 * (f - 0.65) / 0.35 : 10 + 2 * Math.Min(1, (f - 1) / 0.3));
            rfMonte = Math.Exp(-1 / (0.002 * fe)); rfDescendRapide = Math.Exp(-1 / (0.015 * fe)); rfLent = Math.Exp(-1 / (0.06 * fe));
            rgMonte = Math.Exp(-1 / (0.002 * fe)); rgDescend = Math.Exp(-1 / ((Style == StyleNet ? 0.07 : 0.006) * fe));
            // Puissant : relâché en 6 ms (40 ms avant). Mesuré sur 20 enregistrements de Warzone : tenu 40 ms, le renfort
            // restait sur le fond 100 ms après chaque pas, c'est ce qui « pompait » ; relâché vite, il suit le pas.
            largeurBref = Style == StyleNet ? 6 : 4;
            relacheRenfort = Math.Exp(-1 / (0.005 * fe));
            rfCrete = Math.Exp(-1 / ((Style == StyleNet ? 0.01 : 0.06) * fe));   // Puissant : la traîne d'un son fort (tir) n'est pas un pas
            // Puissant : le renfort ajouté repasse dans un 2e passe-haut 700 Hz (pente 24 dB/octave) : sans lui, le renfort d'un
            // pas remontait aussi les bas-médiums du fond (vers 400 Hz) pendant le pas, ce qui s'entendait comme un pompage
            bandePasHaut = new Biquad2[16]; ajoutPasHaut = Style == StyleNet ? null : new Biquad2[16]; bandePasBas = new Biquad2[16]; bandeChocHaut = new Biquad2[16]; bandeChocBas = new Biquad2[16];
            for (int c = 0; c < 16; c++)
            {
                bandePasHaut[c] = new Biquad2(true, 700, fe); if (ajoutPasHaut != null) ajoutPasHaut[c] = new Biquad2(true, 700, fe); bandePasBas[c] = new Biquad2(false, Style == StyleNet ? 5000 : 8000, fe);
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
                        LisserArme(-ProfondeurCentre * Borne01((nc + seuilArmeCentre) / 8));   // rien sous −26 dB, tout dès −18 dB   // rien sous −36 dB, tout dès −28 dB
                        t[b + 2] = (float)(fc * Math.Pow(10, gainArme / 20));
                        milieu *= Math.Pow(10, gainArme * 0.5 / 20);                // la copie « fantôme » du centre dans l'avant
                    }
                    else
                    {
                        pMilieu = aArme * pMilieu + (1 - aArme) * milieu * milieu;
                        pCotes = aArme * pCotes + (1 - aArme) * cotes * cotes;
                        double nm = 10 * Math.Log10(pMilieu + 1e-18), ns = 10 * Math.Log10(pCotes + 1e-18);
                        LisserArme(-ProfondeurStereo * Borne01((nm - ns - 9) / 6) * Borne01((nm + seuilArmeStereo) / 6));   // fort (−14 dB et plus) ET centré   // fort ET centré
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
                    double nScene = 10 * Math.Log10(pScene + 1e-18);
                    bool calme = nScene < -22 && nScene > -60 && gainGraves > -3;   // calme, mais pas le quasi-silence (souffle)
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
                    double rp = 10 * Math.Log10(pRapidePas + 1e-18), rc = 10 * Math.Log10(pRapideChoc + 1e-18);
                    double cp = rp < -70 ? 0 : RenfortMax * Bref(rp, 10 * Math.Log10(pLentPas + 1e-18), largeurBref);          // souffle : rien
                    double cc = rc < -70 ? 0 : RenfortMax * facteurChoc * Bref(rc, 10 * Math.Log10(pLentChoc + 1e-18), largeurBref);
                    // le « bref » est tenu ~70 ms (tout le pas) ; le « faible » agit tout de suite
                    gainRenfortPas = cp > gainRenfortPas ? rgMonte * gainRenfortPas + (1 - rgMonte) * cp : rgDescend * gainRenfortPas + (1 - rgDescend) * cp;
                    gainRenfortChoc = cc > gainRenfortChoc ? rgMonte * gainRenfortChoc + (1 - rgMonte) * cc : rgDescend * gainRenfortChoc + (1 - rgDescend) * cc;
                    // « faible » jugé aussi sur l'échantillon lui-même : dès la 1re milliseconde d'un tir, rien n'est remonté
                    // crête tenue ~10 ms (un échantillon isolé d'un bruit fort peut être petit : il ne doit pas rouvrir la remontée)
                    crPas = Math.Max(pp, crPas * rfCrete); crChoc = Math.Max(pc, crChoc * rfCrete);
                    double ip = 10 * Math.Log10(crPas + 1e-18) - 3, ic = 10 * Math.Log10(crChoc + 1e-18) - 3;
                    double gp = gainRenfortPas * Faible(Math.Max(rp, ip)), gc = gainRenfortChoc * Faible(Math.Max(rc, ic));
                    if (gp > RenfortRelevéMax) RenfortRelevéMax = gp;
                    if (canaux == 2 && accentCotes > 0)
                    {
                        // accent des côtés : dans la bande des pas, la différence gauche/droite (ce qui vient d'un côté) est
                        // remontée ; ce qui est au centre (tes propres sons) ne bouge pas
                        double ks = Math.Pow(10, accentCotes * Math.Min(1, forceRel) / 20) - 1, sPas = (bufPas[0] - bufPas[1]) * 0.5 * ks;
                        t[b] = (float)(t[b] + sPas); t[b + 1] = (float)(t[b + 1] - sPas);
                    }
                    if (canaux == 2)
                    {
                        // stéréo : un ennemi sur le côté est plus fort d'un côté ; tes propres sons sont au centre
                        double g2 = bufPas[0] * bufPas[0], d2 = bufPas[1] * bufPas[1];
                        pGauche = g2 > pGauche ? rfMonte * pGauche + (1 - rfMonte) * g2 : rfDescendRapide * pGauche + (1 - rfDescendRapide) * g2;
                        pDroite = d2 > pDroite ? rfMonte * pDroite + (1 - rfMonte) * d2 : rfDescendRapide * pDroite + (1 - rfDescendRapide) * d2;
                        double w = PoidsCote(10 * Math.Log10(pGauche + 1e-18), 10 * Math.Log10(pDroite + 1e-18), poidsCentre);
                        gp *= w; gc *= w;
                    }
                    double kp = Math.Pow(10, gp / 20) - 1, kc = Math.Pow(10, gc / 20) - 1;
                    for (int c = 0; c < canaux && c < 16; c++)
                    {
                        // 5.1 / 7.1 : la voie centrale porte tes propres sons : remontée réduite
                        double f2 = canaux >= 6 && c == 2 ? 0 : 1;   // en 7.1, l'arrière a ses propres voies : seul le centre est retenu
                        double ajout = ((ajoutPasHaut != null ? ajoutPasHaut[c].Passer(bufPas[c]) : bufPas[c]) * kp + bufChoc[c] * kc) * f2;
                        if (renfortApres) ajoutRenfort[c] = ajout;      // ajouté après le compresseur (il ne l'annule plus)
                        else t[b + c] = (float)(t[b + c] + ajout);
                    }
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
                if (renfortApres)
                {
                    // Puissant : le renfort d'un pas ne doit jamais faire agir le limiteur (qui baisserait tout le son :
                    // pompage) ; il est réduit seul, juste ce qu'il faut pour rester sous la marge, puis revient en 5 ms
                    double k = 1;
                    for (int c = 0; c < canaux && c < 16; c++)
                    {
                        double s0 = t[b + c] * g, a0 = ajoutRenfort[c] * g;
                        if (Math.Abs(s0 + a0) > MargeRenfort && Math.Abs(a0) > 1e-9) k = Math.Min(k, Math.Max(0, (MargeRenfort - Math.Abs(s0)) / Math.Abs(a0)));
                    }
                    kRenfort = k < kRenfort ? k : relacheRenfort * kRenfort + (1 - relacheRenfort) * k;
                }
                for (int c = 0; c < canaux; c++)
                {
                    float v = t[b + c] * g;
                    if (renfortApres && c < 16) { v += (float)(ajoutRenfort[c] * g * kRenfort); ajoutRenfort[c] = 0; }
                    retard[pos * canaux + c] = v; if (Math.Abs(v) > crete) crete = Math.Abs(v);
                }
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
        /// moins de 2 s gardées ou pendant une réécoute.
        /// </summary>
        public bool Exporter(string prefixe)
        {
            float[] sans, avec;
            if (!Copier(out sans, out avec)) return false;
            EcrireWav(prefixe + "-sans.wav", sans, Canaux, Frequence);
            EcrireWav(prefixe + "-avec.wav", avec, Canaux, Frequence);
            return true;
        }

        /// <summary>Copie (rapide, sans disque) des secondes gardées, sans et avec Velox, dans l'ordre. Faux s'il y a moins
        /// de 2 s gardées ou pendant une réécoute. Le fil du son copie ; l'écriture des fichiers se fait ailleurs.</summary>
        public bool Copier(out float[] sans, out float[] avec)
        {
            sans = null; avec = null;
            if (Phase != 0 || plein < 2 * Frequence) return false;
            int debut = (int)(((long)pos - plein + Capacite) % Capacite);
            sans = CopierAnneau(sec, debut); avec = CopierAnneau(traite, debut);
            return true;
        }

        private float[] CopierAnneau(float[] source, int debut)
        {
            var d = new float[(long)plein * Canaux];
            int premier = Math.Min(plein, Capacite - debut);
            Array.Copy(source, (long)debut * Canaux, d, 0, (long)premier * Canaux);
            if (premier < plein) Array.Copy(source, 0, d, (long)premier * Canaux, (long)(plein - premier) * Canaux);
            return d;
        }

        /// <summary>Écrit un WAV 16 bits (échantillons entrelacés, bornés à ±1).</summary>
        public static void EcrireWav(string chemin, float[] d, int canaux, int frequence)
        {
            int octets = d.Length * 2;
            using (var f = new FileStream(chemin, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(f))
            {
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + octets); w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                w.Write(System.Text.Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)canaux); w.Write(frequence);
                w.Write(frequence * canaux * 2); w.Write((short)(canaux * 2)); w.Write((short)16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data")); w.Write(octets);
                foreach (var v in d) w.Write((short)Math.Round(Math.Max(-1, Math.Min(1, v)) * 32767));
            }
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

        /// <summary>Résultat du dernier enregistrement pour analyse, écrit par le moteur : « ok|chemin », « vide » (moins de
        /// 2 s gardées, ou réécoute en cours) ou « erreur|message ».</summary>
        public static string FichierExport { get { return Path.Combine(Chemins.Local, "moteur-son-export.txt"); } }

        /// <summary>Demande au moteur d'enregistrer les dernières secondes (sans et avec Velox) sur le Bureau.</summary>
        public static string DemanderExport()
        {
            try { Directory.CreateDirectory(Chemins.Local); try { File.Delete(FichierExport); } catch { } File.WriteAllText(FichierReecoute, "export"); return ""; }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Attend (au plus « ms ») le résultat de l'enregistrement : null si le moteur n'a pas répondu.</summary>
        public static string AttendreExport(int ms)
        {
            var montre = System.Diagnostics.Stopwatch.StartNew();
            while (montre.ElapsedMilliseconds < ms)
            {
                try { if (File.Exists(FichierExport)) { string t = File.ReadAllText(FichierExport).Trim(); if (t != "") return t; } } catch { }
                System.Threading.Thread.Sleep(100);
            }
            return null;
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

        /// <summary>(réglage) Passe un WAV 16 bits (enregistré par « Enregistrer pour analyse ») par le traitement, en blocs de
        /// 10 ms comme le moteur, et écrit le résultat. 0 si c'est fait.</summary>
        public static int TraiterWav(string entree, string sortie, int force, int arme, int style = CompresseurPas.StylePuissant)
        {
            var o = File.ReadAllBytes(entree);
            int canaux = BitConverter.ToInt16(o, 22), fe = BitConverter.ToInt32(o, 24);
            int n = (o.Length - 44) / 2;
            var t = new float[n];
            for (int i = 0; i < n; i++) t[i] = BitConverter.ToInt16(o, 44 + 2 * i) / 32768f;
            var comp = new CompresseurPas(force, fe, arme, true, style);
            int bloc = fe / 100 * canaux;
            var b = new float[bloc];
            for (int p = 0; p + bloc <= n; p += bloc) { Array.Copy(t, p, b, 0, bloc); comp.Traiter(b, bloc / canaux, canaux); Array.Copy(b, 0, t, p, bloc); }
            var r = (byte[])o.Clone();
            for (int i = 0; i < n; i++) { short v = (short)Math.Round(Math.Max(-1, Math.Min(1, t[i])) * 32767); r[44 + 2 * i] = (byte)(v & 0xFF); r[45 + 2 * i] = (byte)((v >> 8) & 0xFF); }
            File.WriteAllBytes(sortie, r);
            return 0;
        }

        /// <summary>
        /// (essais) Le défaut vu sur de vrais enregistrements de Warzone : des pas proches (crête vers −20 dB) sur un fond
        /// faible (souffle vers −45 dB dans la bande des pas) et un grondement grave. Contraste des pas dans leur bande
        /// (niveau pendant les pas moins médiane hors des pas, par tranches de 20 ms), sans puis avec le traitement. L'ancien
        /// réglage le faisait passer de ~20 dB à ~5 dB (les pas se noyaient dans le fond remonté).
        /// </summary>
        public static void MesurerContraste(int force, out double contrasteSans, out double contrasteAvec, int style = CompresseurPas.StyleNet)
        {
            int fe = DemoAudio.Frequence, n = 6 * fe;
            var t = new float[n * 2]; var evenement = new bool[n / (fe / 50) + 1];
            var alea = new Random(23);
            Func<double> bruit = () => alea.NextDouble() * 2 - 1;
            var lp = DemoAudio.Biquad.PasseBas(80, 0.7); var souffle = DemoAudio.Biquad.PasseBande(2000, 0.5);
            for (int i = 0; i < n; i++) { float v = (float)(0.25 * lp.Passer(bruit()) * 3 + 0.004 * souffle.Passer(bruit())); t[2 * i] = v; t[2 * i + 1] = v * 0.9f; }
            for (double debut = 0.5; debut < 5.6; debut += 0.5)
            {
                var bp = DemoAudio.Biquad.PasseBande(1800, 1.0);
                for (int i = 0; i < (int)(0.06 * fe); i++)
                {
                    double tt = i / (double)fe, v = 0.12 * Math.Min(1, tt / 0.002) * Math.Exp(-tt / 0.02) * bp.Passer(bruit()) * 3;
                    int k = (int)(debut * fe) + i; t[2 * k] += (float)v; t[2 * k + 1] += (float)(0.7 * v);
                    if (i < (int)(0.04 * fe)) evenement[k / (fe / 50)] = true;
                }
            }
            Func<float[], double> contraste = y =>
            {
                var b1 = DemoAudio.Biquad.PasseHaut(700, 0.7); var b2 = DemoAudio.Biquad.PasseBas(5000, 0.7);
                int tr = fe / 50, m = n / tr; var niv = new double[m];
                for (int j = 0; j < m; j++)
                {
                    double e = 0;
                    for (int i = j * tr; i < (j + 1) * tr; i++) { double v = b2.Passer(b1.Passer(y[2 * i])); e += v * v; }
                    niv[j] = 10 * Math.Log10(e / tr + 1e-20);
                }
                var ev = Enumerable.Range(0, m).Where(j => evenement[j]).Select(j => niv[j]).ToList();
                var autres = Enumerable.Range(0, m).Where(j => !evenement[j] && j > 10).Select(j => niv[j]).OrderBy(x => x).ToList();
                return ev.Average() - autres[autres.Count / 2];
            };
            contrasteSans = contraste(t);
            var c2 = (float[])t.Clone();
            var comp = new CompresseurPas(force, fe, 1, true, style);
            for (int p = 0; p + fe / 100 <= n; p += fe / 100) { var b = new float[fe / 100 * 2]; Array.Copy(c2, 2 * p, b, 0, b.Length); comp.Traiter(b, fe / 100, 2); Array.Copy(b, 0, c2, 2 * p, b.Length); }
            int dec = comp.Retard; Array.Copy(c2, 2 * dec, c2, 0, c2.Length - 2 * dec);
            contrasteAvec = contraste(c2);
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

namespace NettoyagePC
{
    /// <summary>Une partie jouée avec « Pas boostés » : jeu, début, durée, style, force, plus forte réduction des
    /// explosions et retard moyen. Affichée dans « Dernières parties » du Studio son.</summary>
    internal sealed class PartieJouee
    {
        public DateTime Debut;
        public string Jeu = "";
        public int Minutes, Style, Force, Retard;
        public double Explosions;

        private static readonly System.Globalization.CultureInfo C = System.Globalization.CultureInfo.InvariantCulture;

        public string Ligne()
        {
            return Debut.ToString("yyyy-MM-ddTHH:mm", C) + "|" + Jeu + "|" + Minutes.ToString(C) + "|" + Style.ToString(C) + "|" + Force.ToString(C) + "|"
                + Explosions.ToString("0.0", C) + "|" + Retard.ToString(C);
        }

        public static PartieJouee Lire(string ligne)
        {
            var p = (ligne ?? "").Trim().Split('|');
            if (p.Length < 7) return null;
            DateTime d; int mi, st, fo, re; double ex;
            if (!DateTime.TryParseExact(p[0], "yyyy-MM-ddTHH:mm", C, System.Globalization.DateTimeStyles.None, out d)) return null;
            if (!System.Text.RegularExpressions.Regex.IsMatch(p[1], "^[a-z0-9]{1,24}$")) return null;
            if (!int.TryParse(p[2], System.Globalization.NumberStyles.Integer, C, out mi) || !int.TryParse(p[3], System.Globalization.NumberStyles.Integer, C, out st)
                || !int.TryParse(p[4], System.Globalization.NumberStyles.Integer, C, out fo) || !double.TryParse(p[5], System.Globalization.NumberStyles.Float, C, out ex)
                || !int.TryParse(p[6], System.Globalization.NumberStyles.Integer, C, out re)) return null;
            return new PartieJouee { Debut = d, Jeu = p[1], Minutes = Math.Max(0, Math.Min(10000, mi)), Style = st == 0 ? 0 : 1, Force = Math.Max(0, Math.Min(200, fo)),
                Explosions = Math.Max(0, Math.Min(60, ex)), Retard = Math.Max(0, Math.Min(500, re)) };
        }
    }

    /// <summary>Suit la partie en cours dans le moteur (appelé par le fil du son, sans lecture ni écriture de fichier) et
    /// rend la partie finie quand le jeu se ferme ou change. Une partie de moins d'une minute n'est pas retenue.</summary>
    internal sealed class SuiviPartie
    {
        private string jeu = "";
        private DateTime debut;
        private int style, force, mesures; private long sommeRetard; private double explosions;

        /// <summary>Nouveau relevé (toutes les 500 ms). Renvoie la partie qui vient de finir, ou null.</summary>
        public PartieJouee Relever(DateTime maintenant, string jeuCle, int style, int force, double reduction, int retard)
        {
            jeuCle = jeuCle ?? "";
            PartieJouee finie = null;
            if (jeuCle != jeu) { finie = Finir(maintenant); if (jeuCle != "") { jeu = jeuCle; debut = maintenant; mesures = 0; sommeRetard = 0; explosions = 0; } }
            if (jeu == "") return finie;
            if (force >= 0) { this.style = style; this.force = force; }   // le dernier réglage de la partie (−1 : traitement pas encore ouvert)
            if (reduction > explosions) explosions = reduction;
            if (retard > 0) { sommeRetard += retard; mesures++; }
            return finie;
        }

        /// <summary>Fin de la partie en cours (jeu fermé, moteur arrêté) : la partie, ou null si trop courte ou aucune.</summary>
        public PartieJouee Finir(DateTime maintenant)
        {
            if (jeu == "") return null;
            var p = new PartieJouee { Debut = debut, Jeu = jeu, Minutes = (int)Math.Round((maintenant - debut).TotalMinutes), Style = style, Force = force,
                Explosions = Math.Round(explosions, 1), Retard = mesures > 0 ? (int)Math.Round((double)sommeRetard / mesures) : 0 };
            jeu = "";
            return (maintenant - debut).TotalSeconds >= 60 ? p : null;
        }
    }

    internal static partial class MoteurSon
    {
        public static string FichierParties { get { return System.IO.Path.Combine(Chemins.Local, "moteur-son-parties.txt"); } }
        public const int PartiesGardees = 20;

        /// <summary>Ajoute une partie au journal (les 20 dernières sont gardées).</summary>
        private static readonly object verrouParties = new object();

        public static void NoterPartie(PartieJouee p)
        {
            if (p == null) return;
            lock (verrouParties)   // fin de partie (fil du pool) et arrêt du moteur peuvent écrire en même temps
            try
            {
                System.IO.Directory.CreateDirectory(Chemins.Local);
                var lignes = Parties().Select(x => x.Ligne()).ToList();
                lignes.Add(p.Ligne());
                if (lignes.Count > PartiesGardees) lignes.RemoveRange(0, lignes.Count - PartiesGardees);
                string temp = FichierParties + ".tmp";
                System.IO.File.WriteAllLines(temp, lignes);
                System.IO.File.Move(temp, FichierParties, true);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("journal des parties", ex); }
        }

        /// <summary>Les parties retenues, de la plus ancienne à la plus récente.</summary>
        public static List<PartieJouee> Parties()
        {
            var l = new List<PartieJouee>();
            try
            {
                if (!System.IO.File.Exists(FichierParties)) return l;
                foreach (var s in System.IO.File.ReadAllLines(FichierParties).Take(200)) { var p = PartieJouee.Lire(s); if (p != null) l.Add(p); }
            }
            catch { }
            return l;
        }
    }
}
