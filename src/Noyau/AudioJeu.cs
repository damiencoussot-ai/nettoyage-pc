// Velox - Audio jeu : égaliseur de pas pour Call of Duty / Warzone, appliqué par Equalizer APO (logiciel libre).
// Velox écrit un petit fichier de configuration (MW_optimizer.txt) dans le dossier d'Equalizer APO et vérifie que
// config.txt l'inclut. Le jeu n'est jamais touché ; le reste de config.txt non plus (d'autres traitements y vivent).
// Toujours avec les droits normaux : si Windows refuse l'écriture, rien n'est forcé.
// Sans interface : partagé par la fenêtre, l'Historique (Annuler) et la désinstallation.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace NettoyagePC
{
    internal sealed class BandeAudio { public string Nom, Type; public int Fc; public double Q; }

    internal sealed class ProfilAudio { public string Cle, Nom, Carte, Texte; public double[] Gains; }

    internal sealed class FiltreAudio { public string Type; public int Fc; public double Gain, Q; }

    /// <summary>Un jeu reconnu : son programme, et le profil prévu pour lui (gains des six bandes, coupe-explosions).</summary>
    internal sealed class JeuAudio
    {
        public string Cle = "", Nom = "", Texte = "";
        /// <summary>Option de son spatial intégrée au jeu ("" si aucune connue) : à ne pas cumuler avec le 7.1 virtuel.</summary>
        public string Spatial = "";
        /// <summary>Réglages conseillés dans le menu audio du jeu ("" si aucun).</summary>
        public string DansLeJeu = "";
        public Regex Exe;
        public double[] Gains;
        public int CoupeBas;
    }

    /// <summary>Une sortie audio de Windows où Equalizer APO est installé.</summary>
    internal sealed class SortieAudio
    {
        public string Guid = "", Nom = "", Carte = "";
        public string Libelle { get { return Nom == "" ? (Carte == "" ? Guid : Carte) : Carte == "" ? Nom : Nom + " (" + Carte + ")"; } }
    }

    /// <summary>Réglages de l'outil (%LOCALAPPDATA%\NettoyagePC\audio-jeu.json). Toute valeur lue est ramenée dans ses bornes.</summary>
    internal sealed class ReglagesAudioJeu
    {
        /// <summary>Égaliseur actif (ce que dit le fichier d'Equalizer APO).</summary>
        public bool Actif;
        /// <summary>Gain de chaque bande, en dB (−12 à +12, pas de 0,5).</summary>
        public double[] Gains = (double[])AudioJeu.Profils[0].Gains.Clone();
        /// <summary>Force de l'effet, en % (0 à 150) : chaque gain est multiplié par elle.</summary>
        public int Intensite = 100;
        /// <summary>Niveau de sortie (−12 à +6 dB) et balance (−6 à +6 dB, positive = vers la droite).</summary>
        public double Niveau, Balance;
        /// <summary>Largeur stéréo (0 à 100 %) et coupe-explosions (passe-haut : 0 = coupé, 60, 90 ou 120 Hz).</summary>
        public int Largeur, CoupeBas;
        public bool Inversion;
        /// <summary>« Auto Warzone » : l'égaliseur s'active quand le jeu tourne et se coupe quand il se ferme.</summary>
        public bool AutoJeu;
        /// <summary>Sortie visée : « auto » (la sortie par défaut de Windows), « toutes », ou l'identifiant d'une sortie.</summary>
        public string Cible = "auto";
        /// <summary>Sortie écrite dans le dernier fichier (« » = toutes) : sert à le réécrire quand la sortie par défaut change.</summary>
        public string CibleEcrite = "";
        /// <summary>Raccourcis clavier globaux (Ctrl+Alt+F9 : égaliseur, Ctrl+Alt+F10 : profil suivant). Désactivés par défaut.</summary>
        public bool Raccourcis;
        /// <summary>Le démarrage rapide (première utilisation) a été fait ou masqué.</summary>
        public bool AssistantFait;
        /// <summary>Studio son en mode Expert (tous les réglages) ; par défaut, mode Simple (l'essentiel seulement).</summary>
        public bool ModeExpert;
        /// <summary>Moteur de l'égaliseur : « apo » (Equalizer APO) ou « vm » (égaliseur des bus de Voicemeeter Banana / Potato).</summary>
        public string Moteur = "apo";
        /// <summary>Bus de Voicemeeter égalisé (0 = A1), et bus dont Velox a allumé l'égaliseur (−1 : aucun).</summary>
        public int BusVm, VmEcrit = -1;
        /// <summary>Dernier appareil vu sur le bus A de Voicemeeter (pour viser la bonne sortie quand Voicemeeter est la sortie par défaut).</summary>
        public string AppareilVm = "";
        /// <summary>Ne pas mettre le nom « Velox Studio » dans Voicemeeter sur ce que l'égaliseur traite (mis par défaut).</summary>
        public bool SansEtiquetteVm;
        /// <summary>Calibration du casque (base AutoEq) : active ou non, modèle, source de la mesure, filtres de correction.</summary>
        public bool CalibActive;
        public string CalibNom = "", CalibSource = "";
        public List<FiltreAudio> CalibFiltres = new List<FiltreAudio>();
        /// <summary>Auto jeux : au lancement d'un jeu reconnu, charger le profil prévu pour lui. JeuEnCours : dernier jeu
        /// dont le profil a été chargé (pour ne pas écraser un réglage fait pendant la partie).</summary>
        public bool ProfilParJeu;
        public string JeuEnCours = "";
        /// <summary>Profil choisi par l'utilisateur pour un jeu (clé du jeu → gains) : chargé à la place du profil prévu
        /// par Velox quand ce jeu se lance (« Profil adapté à chaque jeu »).</summary>
        public Dictionary<string, double[]> GainsParJeu = new Dictionary<string, double[]>();

        /// <summary>Gains à charger au lancement de ce jeu : ceux choisis pour lui, sinon ceux prévus par Velox.</summary>
        public double[] GainsDuJeu(JeuAudio j)
        {
            double[] g;
            return GainsParJeu != null && GainsParJeu.TryGetValue(j.Cle, out g) && g != null && g.Length == AudioJeu.Bandes.Length ? (double[])g.Clone() : (double[])j.Gains.Clone();
        }
        /// <summary>Compensation du test d'audition : baisse (dB, ≤ 0) de chaque oreille aux fréquences de TestAudition.</summary>
        public double[] OreilleG = new double[TestAudition.Frequences.Length], OreilleD = new double[TestAudition.Frequences.Length];
        public bool Oreilles { get { return OreilleG.Any(x => x < 0) || OreilleD.Any(x => x < 0); } }

        public ReglagesAudioJeu Copie() { var c = (ReglagesAudioJeu)MemberwiseClone(); c.Gains = (double[])Gains.Clone(); c.CalibFiltres = new List<FiltreAudio>(CalibFiltres ?? new List<FiltreAudio>()); c.OreilleG = (double[])OreilleG.Clone(); c.OreilleD = (double[])OreilleD.Clone();
            c.GainsParJeu = (GainsParJeu ?? new Dictionary<string, double[]>()).ToDictionary(kv => kv.Key, kv => (double[])kv.Value.Clone()); return c; }

        private static double Borne(double v, double min, double max, double pas)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) v = 0;
            return Math.Max(min, Math.Min(max, Math.Round(v / pas, MidpointRounding.AwayFromZero) * pas));
        }

        /// <summary>Ramène chaque valeur dans ses bornes (fichier de réglages modifié à la main, entrée de l'Historique).</summary>
        public ReglagesAudioJeu Borner()
        {
            var g = new double[AudioJeu.Bandes.Length];
            for (int i = 0; i < g.Length; i++) g[i] = Borne(Gains != null && i < Gains.Length ? Gains[i] : 0, -12, 12, 0.5);
            Gains = g;
            Intensite = Math.Max(0, Math.Min(150, Intensite));
            Niveau = Borne(Niveau, -12, 6, 0.5); Balance = Borne(Balance, -6, 6, 0.5);
            Largeur = Math.Max(0, Math.Min(100, Largeur));
            if (CoupeBas != 60 && CoupeBas != 90 && CoupeBas != 120) CoupeBas = 0;
            if (Cible != "auto" && Cible != "toutes") { Cible = AudioJeu.NormaliserGuid(Cible); if (Cible == "") Cible = "auto"; }
            CibleEcrite = AudioJeu.NormaliserGuid(CibleEcrite);
            if (Moteur != "vm") Moteur = "apo";
            BusVm = Math.Max(0, Math.Min(7, BusVm)); VmEcrit = Math.Max(-1, Math.Min(7, VmEcrit));
            int nf = TestAudition.Frequences.Length;
            var og = new double[nf]; var od = new double[nf];
            for (int i = 0; i < nf; i++)
            {
                og[i] = Borne(OreilleG != null && i < OreilleG.Length ? OreilleG[i] : 0, -6, 0, 0.5);
                od[i] = Borne(OreilleD != null && i < OreilleD.Length ? OreilleD[i] : 0, -6, 0, 0.5);
            }
            OreilleG = og; OreilleD = od;
            var gj = new Dictionary<string, double[]>();
            if (GainsParJeu != null)
                foreach (var kv in GainsParJeu)
                    if (kv.Key != null && AudioJeu.Jeux.Any(j => j.Cle == kv.Key) && kv.Value != null && kv.Value.Length == AudioJeu.Bandes.Length)
                        gj[kv.Key] = kv.Value.Select(x => Borne(x, -12, 12, 0.5)).ToArray();
            GainsParJeu = gj;
            return this;
        }

        public Dictionary<string, object> VersJson()
        {
            return new Dictionary<string, object>
            {
                { "Actif", Actif }, { "Gains", Gains.Select(x => (object)x).ToArray() }, { "Intensite", Intensite }, { "Niveau", Niveau }, { "Balance", Balance },
                { "Largeur", Largeur }, { "CoupeBas", CoupeBas }, { "Inversion", Inversion }, { "AutoJeu", AutoJeu }, { "Cible", Cible }, { "CibleEcrite", CibleEcrite },
                { "Raccourcis", Raccourcis }, { "Moteur", Moteur }, { "BusVm", BusVm }, { "VmEcrit", VmEcrit }, { "AppareilVm", AppareilVm ?? "" },
                { "SansEtiquetteVm", SansEtiquetteVm }, { "AssistantFait", AssistantFait }, { "ModeExpert", ModeExpert },
                { "ProfilParJeu", ProfilParJeu }, { "JeuEnCours", JeuEnCours ?? "" },
                { "GainsParJeu", (GainsParJeu ?? new Dictionary<string, double[]>()).ToDictionary(kv => kv.Key, kv => (object)kv.Value.Select(x => (object)x).ToArray()) },
                { "OreilleG", OreilleG.Select(x => (object)x).ToArray() }, { "OreilleD", OreilleD.Select(x => (object)x).ToArray() },
                { "CalibActive", CalibActive }, { "CalibNom", CalibNom ?? "" }, { "CalibSource", CalibSource ?? "" },
                { "CalibFiltres", (CalibFiltres ?? new List<FiltreAudio>()).Select(f => (object)new object[] { f.Type, f.Fc, f.Gain, f.Q }).ToArray() },
            };
        }

        public static ReglagesAudioJeu DepuisJson(Dictionary<string, object> d)
        {
            var r = new ReglagesAudioJeu();
            if (d == null) return r;
            r.Actif = Json.Bool(d, "Actif");
            var g = new List<double>();
            var brut = Json.Valeur(d, "Gains") as System.Collections.IEnumerable;
            if (brut != null && !(brut is string))
                foreach (var x in brut) { try { g.Add(Convert.ToDouble(x, CultureInfo.InvariantCulture)); } catch { g.Add(0); } }
            if (g.Count == AudioJeu.Bandes.Length) r.Gains = g.ToArray();
            r.Intensite = (int)Json.Long(d, "Intensite", 100);
            r.Niveau = Json.Double(d, "Niveau"); r.Balance = Json.Double(d, "Balance");
            r.Largeur = (int)Json.Long(d, "Largeur"); r.CoupeBas = (int)Json.Long(d, "CoupeBas");
            r.Inversion = Json.Bool(d, "Inversion"); r.AutoJeu = Json.Bool(d, "AutoJeu");
            r.Cible = Json.Texte(d, "Cible", "auto"); r.CibleEcrite = Json.Texte(d, "CibleEcrite");
            r.Raccourcis = Json.Bool(d, "Raccourcis"); r.Moteur = Json.Texte(d, "Moteur", "apo");
            r.BusVm = (int)Json.Long(d, "BusVm"); r.VmEcrit = (int)Json.Long(d, "VmEcrit", -1);
            string av = Json.Texte(d, "AppareilVm"); r.AppareilVm = av.Length <= 200 ? av : "";
            r.SansEtiquetteVm = Json.Bool(d, "SansEtiquetteVm");
            r.AssistantFait = Json.Bool(d, "AssistantFait");
            r.ModeExpert = Json.Bool(d, "ModeExpert");
            r.ProfilParJeu = Json.Bool(d, "ProfilParJeu");
            string je = Json.Texte(d, "JeuEnCours"); r.JeuEnCours = AudioJeu.Jeux.Any(j => j.Cle == je) ? je : "";
            if (Json.Valeur(d, "GainsParJeu") is Dictionary<string, object> gpj)
                foreach (var kv in gpj)
                {
                    var lg = kv.Value as System.Collections.IEnumerable;
                    if (lg == null || lg is string) continue;
                    var v = new List<double>();
                    foreach (var x in lg) { try { v.Add(Convert.ToDouble(x, CultureInfo.InvariantCulture)); } catch { v.Add(0); } }
                    r.GainsParJeu[kv.Key] = v.ToArray();
                }
            foreach (var cote in new[] { "OreilleG", "OreilleD" })
            {
                var lo = Json.Valeur(d, cote) as System.Collections.IEnumerable;
                if (lo == null || lo is string) continue;
                var v = new List<double>();
                foreach (var x in lo) { try { v.Add(Convert.ToDouble(x, CultureInfo.InvariantCulture)); } catch { v.Add(0); } }
                if (v.Count == TestAudition.Frequences.Length) { if (cote == "OreilleG") r.OreilleG = v.ToArray(); else r.OreilleD = v.ToArray(); }
            }
            r.CalibActive = Json.Bool(d, "CalibActive");
            string cn = Json.Texte(d, "CalibNom"), cs = Json.Texte(d, "CalibSource");
            r.CalibNom = cn.Length <= 150 ? cn : ""; r.CalibSource = cs.Length <= 120 ? cs : "";
            r.CalibFiltres = new List<FiltreAudio>();
            var lf = Json.Valeur(d, "CalibFiltres") as System.Collections.IEnumerable;
            if (lf != null && !(lf is string))
                foreach (var o in lf)
                {
                    var t = (o as System.Collections.IEnumerable)?.Cast<object>().ToArray();
                    if (t == null || t.Length != 4 || r.CalibFiltres.Count >= Casques.FiltresMax) continue;
                    try
                    {
                        string ty = Convert.ToString(t[0], CultureInfo.InvariantCulture);
                        double fc = Convert.ToDouble(t[1], CultureInfo.InvariantCulture), gc = Convert.ToDouble(t[2], CultureInfo.InvariantCulture), q = Convert.ToDouble(t[3], CultureInfo.InvariantCulture);
                        if ((ty == "PK" || ty == "LSC" || ty == "HSC") && fc >= 20 && fc <= 20000 && Math.Abs(gc) <= 20 && q >= 0.1 && q <= 20)
                            r.CalibFiltres.Add(new FiltreAudio { Type = ty, Fc = (int)Math.Round(fc), Gain = Math.Round(gc, 1), Q = Math.Round(q, 2) });
                    }
                    catch { }
                }
            if (r.CalibFiltres.Count == 0) r.CalibActive = false;
            return r.Borner();
        }
    }

    /// <summary>Ce que dit le dossier d'Equalizer APO en ce moment (lecture seule).</summary>
    internal sealed class EtatAudioJeu
    {
        /// <summary>Equalizer APO est installé (son dossier de configuration existe).</summary>
        public bool Present;
        /// <summary>Le fichier de l'égaliseur existe, et il contient un égaliseur actif.</summary>
        public bool Fichier, Actif;
        /// <summary>config.txt inclut le fichier de l'égaliseur.</summary>
        public bool Include;
        /// <summary>Contenu du fichier de l'égaliseur ("" s'il n'existe pas).</summary>
        public string Contenu = "";
        /// <summary>Autres traitements trouvés dans config.txt (YouSound, HeSuVi, Peace) : les effets se cumulent.</summary>
        public List<string> Autres = new List<string>();
    }

    internal static class AudioJeu
    {
        public const string NomFichier = "MW_optimizer.txt";
        public const string LigneInclude = "Include: " + NomFichier;
        public const string LigneCoupe = "# MW Sound Optimizer : desactive";
        private const string Entete = "# MW Sound Optimizer 3.0 - fichier genere automatiquement";
        public const string Site = "https://sourceforge.net/projects/equalizerapo";

        // messages d'échec (traduits à l'affichage)
        public const string Absent = "Equalizer APO n'est pas installé sur ce PC : rien n'a été modifié.";
        public const string Refus = "Windows refuse l'écriture dans le dossier d'Equalizer APO : rien n'a été modifié.";
        public const string Occupe = "Le fichier d'Equalizer APO est occupé ou illisible : rien n'a été modifié.";
        public const string ConfigInattendu = "Le fichier config.txt d'Equalizer APO a un format inattendu : Velox n'y touche pas.";

        public static readonly BandeAudio[] Bandes =
        {
            new BandeAudio { Nom = "Graves", Fc = 100, Type = "LSC", Q = 0.70 }, new BandeAudio { Nom = "Bas-médium", Fc = 250, Type = "PK", Q = 1.00 },
            new BandeAudio { Nom = "Voix", Fc = 1200, Type = "PK", Q = 1.20 }, new BandeAudio { Nom = "Pas", Fc = 2500, Type = "PK", Q = 1.40 },
            new BandeAudio { Nom = "Recharge", Fc = 4000, Type = "PK", Q = 1.50 }, new BandeAudio { Nom = "Aigus", Fc = 7500, Type = "PK", Q = 2.00 },
        };

        /// <summary>Profils (gains dans l'ordre des bandes). Les quatre premiers sont les grandes cartes de la vue simple.</summary>
        public static readonly ProfilAudio[] Profils =
        {
            new ProfilAudio { Cle = "competitif", Nom = "Pas / compétitif", Carte = "Compétitif", Texte = "Pas et recharges en avant", Gains = new double[] { -5, -3, 2, 4, 3, -3 } },
            new ProfilAudio { Cle = "equilibre", Nom = "Équilibré", Carte = "Équilibré", Texte = "Plus de détail, sans excès", Gains = new double[] { -2, -1, 1, 2, 1, -1 } },
            new ProfilAudio { Cle = "extreme", Nom = "Pas extrême", Carte = "Pas extrême", Texte = "Maximum sur les pas, moins de graves", Gains = new double[] { -8, -5, 3, 6, 4, -4 } },
            new ProfilAudio { Cle = "ultra", Nom = "Pas ultra", Carte = "Ultra", Texte = "Les pas très en avant, son clair et plein", Gains = new double[] { -5, -4, 1, 7, 5, 2 } },
            new ProfilAudio { Cle = "immersion", Nom = "Immersion", Carte = "Immersion", Texte = "Graves et ambiance", Gains = new double[] { 3, 1, -1, -1, 0, 1 } },
            new ProfilAudio { Cle = "voix", Nom = "Voix / chat", Carte = "Voix / chat", Texte = "Les voix en avant", Gains = new double[] { -4, -1, 3, 2, 0, -2 } },
            new ProfilAudio { Cle = "neutre", Nom = "Neutre", Carte = "Neutre", Texte = "Aucune correction", Gains = new double[] { 0, 0, 0, 0, 0, 0 } },
        };

        public static ProfilAudio Profil(string cle) { return Profils.FirstOrDefault(p => p.Cle == cle); }

        /// <summary>Profil suivant parmi les quatre grands (raccourci « profil suivant ») ; après un réglage personnalisé : le premier.</summary>
        public static ProfilAudio ProfilSuivant(double[] gains)
        {
            var p = ProfilDe(gains);
            int i = p == null ? -1 : Array.IndexOf(Profils, p);
            return Profils[i < 0 || i >= 3 ? 0 : i + 1];
        }

        /// <summary>Profil dont les gains sont exactement ceux-là, ou null (réglage personnalisé).</summary>
        public static ProfilAudio ProfilDe(double[] gains)
        {
            return Profils.FirstOrDefault(p => gains != null && gains.Length == p.Gains.Length && !p.Gains.Where((g, i) => Math.Abs(g - gains[i]) > 0.001).Any());
        }

        /// <summary>Nom du profil en cours (« Personnalisé » si les gains ont été changés à la main).</summary>
        public static string NomProfil(ReglagesAudioJeu r)
        {
            var p = ProfilDe(r.Gains);
            if (p != null) return p.Nom;
            var j = Jeux.FirstOrDefault(x => x.Gains.SequenceEqual(r.Gains) && x.CoupeBas == r.CoupeBas);
            return j != null ? j.Nom : "Personnalisé";
        }

        private static readonly Regex RegexGuid = new Regex(@"^\{?([0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})\}?$");

        /// <summary>Identifiant d'une sortie en minuscules et sans accolades ; "" si ce n'en est pas un.</summary>
        public static string NormaliserGuid(string g)
        {
            var m = RegexGuid.Match((g ?? "").Trim());
            return m.Success ? m.Groups[1].Value.ToLowerInvariant() : "";
        }

        // ------------------------------------------------------------ calcul du fichier

        private const double Echantillonnage = 48000;

        /// <summary>Gain en dB, à la fréquence f, d'un filtre d'Equalizer APO (biquads du « RBJ cookbook », à 48 kHz) :
        /// PK (cloche), LSC (plateau des graves, alpha = sin(w0)/(2Q)) ou HP (passe-haut).</summary>
        internal static double Reponse(string type, double fc, double gain, double q, double f)
        {
            double w0 = 2 * Math.PI * fc / Echantillonnage, cos = Math.Cos(w0), alpha = Math.Sin(w0) / (2 * q), A = Math.Pow(10, gain / 40);
            double b0, b1, b2, a0, a1, a2;
            if (type == "HP") { b0 = (1 + cos) / 2; b1 = -(1 + cos); b2 = b0; a0 = 1 + alpha; a1 = -2 * cos; a2 = 1 - alpha; }
            else if (type == "HSC")
            {
                double s = 2 * Math.Sqrt(A) * alpha;
                b0 = A * ((A + 1) + (A - 1) * cos + s); b1 = -2 * A * ((A - 1) + (A + 1) * cos); b2 = A * ((A + 1) + (A - 1) * cos - s);
                a0 = (A + 1) - (A - 1) * cos + s; a1 = 2 * ((A - 1) - (A + 1) * cos); a2 = (A + 1) - (A - 1) * cos - s;
            }
            else if (type == "LSC")
            {
                double s = 2 * Math.Sqrt(A) * alpha;
                b0 = A * ((A + 1) - (A - 1) * cos + s); b1 = 2 * A * ((A - 1) - (A + 1) * cos); b2 = A * ((A + 1) - (A - 1) * cos - s);
                a0 = (A + 1) + (A - 1) * cos + s; a1 = -2 * ((A - 1) + (A + 1) * cos); a2 = (A + 1) + (A - 1) * cos - s;
            }
            else { b0 = 1 + alpha * A; b1 = -2 * cos; b2 = 1 - alpha * A; a0 = 1 + alpha / A; a1 = -2 * cos; a2 = 1 - alpha / A; }
            // module de (b0 + b1 z^-1 + b2 z^-2) / (a0 + a1 z^-1 + a2 z^-2) pour z = e^(jw)
            double w = 2 * Math.PI * f / Echantillonnage, c1 = Math.Cos(w), s1 = Math.Sin(w), c2 = Math.Cos(2 * w), s2 = Math.Sin(2 * w);
            double nr = b0 + b1 * c1 + b2 * c2, ni = b1 * s1 + b2 * s2, dr = a0 + a1 * c1 + a2 * c2, di = a1 * s1 + a2 * s2;
            return 10 * Math.Log10((nr * nr + ni * ni) / (dr * dr + di * di));
        }

        /// <summary>Gain réellement appliqué à une bande : gain × force de l'effet, borné à ±12 dB.</summary>
        public static double GainEffectif(ReglagesAudioJeu r, int bande) { return Math.Max(-12, Math.Min(12, r.Gains[bande] * r.Intensite / 100.0)); }

        /// <summary>Filtres écrits dans le fichier : le passe-haut s'il est actif, puis les bandes dont le gain n'est pas nul.</summary>
        internal static List<FiltreAudio> Filtres(ReglagesAudioJeu r)
        {
            var l = new List<FiltreAudio>();
            if (r.CoupeBas > 0) l.Add(new FiltreAudio { Type = "HP", Fc = r.CoupeBas, Q = 0.7071 });
            // calibration du casque (son neutre), puis le profil par-dessus
            if (r.CalibActive && r.CalibFiltres != null) l.AddRange(r.CalibFiltres.Take(Casques.FiltresMax));
            for (int i = 0; i < Bandes.Length; i++)
            {
                double g = GainEffectif(r, i);
                if (Math.Abs(g) <= 0.01) continue;
                l.Add(new FiltreAudio { Type = Bandes[i].Type, Fc = Bandes[i].Fc, Gain = g, Q = Bandes[i].Q });
            }
            return l;
        }

        /// <summary>
        /// Plus forte amplification de l'ensemble des filtres (jamais négative), relevée sur 71 fréquences de 20 Hz à 20 kHz.
        /// Les filtres étroits (Q &gt; 2, ceux de la calibration du casque) ont un pic plus fin que cette grille : il peut tomber
        /// entre deux points (jusqu'à 7 dB manqués, et le son sature). Autour de chacun, 41 points de plus sur ±1/(2Q) octave,
        /// et 0,2 dB de marge (écart restant mesuré : 0,15 dB au pire).
        /// </summary>
        internal static double Crete(List<FiltreAudio> filtres)
        {
            double max = 0;
            Action<double> point = f =>
            {
                if (f < 20 || f > 20000) return;
                double somme = 0;
                foreach (var x in filtres) somme += Reponse(x.Type, x.Fc, x.Gain, x.Q, f);
                if (somme > max) max = somme;
            };
            for (int k = 0; k <= 70; k++) point(20 * Math.Pow(1000, k / 70.0));
            bool etroits = false;
            foreach (var x in filtres)
            {
                if (x.Type == "HP" || x.Q <= 2 || x.Gain <= 0) continue;
                etroits = true;
                for (int j = -20; j <= 20; j++) point(x.Fc * Math.Pow(2, j / (40.0 * x.Q)));
            }
            return max > 0 && etroits ? max + 0.2 : max;
        }

        /// <summary>Élargissement stéréo (0 à 0,5) : part du canal opposé retranchée à chaque canal.</summary>
        internal static double Elargissement(ReglagesAudioJeu r) { return r.Largeur / 100.0 * 0.5; }

        /// <summary>Préampli écrit dans le fichier : il compense la crête des filtres et l'élargissement, pour ne pas saturer.</summary>
        public static double Preampli(ReglagesAudioJeu r) { return r.Niveau - Crete(Filtres(r)) - 20 * Math.Log10(1 + Elargissement(r)); }

        /// <summary>
        /// Contenu du fichier d'Equalizer APO. « guid » : la sortie visée ("" = toutes les sorties). Les micros ne sont
        /// jamais égalisés (condition « capture », toujours présente). Égaliseur coupé : une seule ligne de commentaire.
        /// Les nombres sont écrits avec un point, arrondis comme le fait le prototype (au plus proche, pair si égalité).
        /// </summary>
        public static string Generer(ReglagesAudioJeu r, string guid)
        {
            if (!r.Actif) return LigneCoupe + "\r\n";
            var c = CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            Action<string> ligne = t => sb.Append(t).Append("\r\n");
            const string sansMicro = "(sizeof(regexSearch(\"capture\", stage)) == 0)";
            guid = NormaliserGuid(guid);
            ligne(Entete);
            ligne(guid != "" ? "If: (sizeof(regexSearch(\"" + guid + "\", tolower(deviceGuid))) > 0) and " + sansMicro : "If: " + sansMicro);
            var filtres = Filtres(r);
            double w = Elargissement(r);
            ligne("Preamp: " + (r.Niveau - Crete(filtres) - 20 * Math.Log10(1 + w)).ToString("F1", c) + " dB");
            int n = 0;
            foreach (var f in filtres)
                ligne("Filter " + (++n).ToString(c) + ": ON " + f.Type + " Fc " + f.Fc.ToString(c) + " Hz" + (f.Type == "HP" ? "" : " Gain " + f.Gain.ToString("F1", c) + " dB Q " + f.Q.ToString("F2", c)));
            string a = r.Inversion ? "R" : "L", b = r.Inversion ? "L" : "R";
            if (w > 0)
            {
                string plus = (1 + w).ToString("F3", c), moins = (-w).ToString("F3", c);
                ligne("Copy: L=" + plus + "*" + a + "+" + moins + "*" + b + " R=" + plus + "*" + b + "+" + moins + "*" + a);
            }
            else if (r.Inversion) ligne("Copy: L=R R=L");
            if (r.Balance != 0)
            {
                // balance vers la droite : la gauche est baissée, et inversement
                ligne("Channel: " + (r.Balance > 0 ? "L" : "R"));
                ligne("Preamp: -" + Math.Abs(r.Balance).ToString("F1", c) + " dB");
                ligne("Channel: all");
            }
            // compensation du test d'audition (baisses seulement : rien ne peut saturer)
            sb.Append(TestAudition.Lignes(r.OreilleG, r.OreilleD));
            ligne("EndIf:");
            return sb.ToString();
        }

        // ------------------------------------------------------------ Equalizer APO sur ce PC

        /// <summary>(essais) dossier de configuration et fichier de réglages de remplacement : rien n'est lu ni écrit ailleurs.</summary>
        public static string DossierEssai, ReglagesEssai;

        private const string CleApo = @"HKLM\SOFTWARE\EqualizerAPO";
        private const string CleSorties = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";

        /// <summary>Dossier de configuration d'Equalizer APO : celui noté à son installation, sinon l'emplacement habituel
        /// (%ProgramFiles%\EqualizerAPO\config).</summary>
        public static string DossierConfig
        {
            get
            {
                if (DossierEssai != null) return DossierEssai;
                try { string c = Registre.Texte(CleApo, "ConfigPath"); if (c != "" && Directory.Exists(c)) return c.TrimEnd('\\'); } catch { }
                return Path.Combine(Chemins.ProgramFiles, "EqualizerAPO", "config");
            }
        }

        public static bool Present { get { try { return Directory.Exists(DossierConfig); } catch { return false; } } }

        /// <summary>Le choix des sorties d'Equalizer APO (« Device Selector » depuis la version 1.4, « Configurator » avant), ou null.</summary>
        public static string Configurator
        {
            get
            {
                var dossiers = new List<string>();
                try { dossiers.Add(Registre.Texte(CleApo, "InstallPath")); } catch { }
                try { dossiers.Add(Path.GetDirectoryName(DossierConfig)); } catch { }
                dossiers.Add(Path.Combine(Chemins.ProgramFiles, "EqualizerAPO"));
                foreach (var d in dossiers)
                {
                    foreach (var exe in new[] { "DeviceSelector.exe", "Configurator.exe" })
                        try { if (!string.IsNullOrEmpty(d) && File.Exists(Path.Combine(d, exe))) return Path.Combine(d, exe); } catch { }
                }
                return null;
            }
        }

        /// <summary>Sorties où Equalizer APO est installé (registre, lecture seule). Cette liste change dans le temps
        /// (Configurator, appareil branché) : elle est relue régulièrement, jamais gardée.</summary>
        public static List<SortieAudio> Sorties()
        {
            var l = new List<SortieAudio>();
            if (!Natif.EstWindows) return l;
            try
            {
                // (Equalizer APO s'installe aussi sur les micros : seules les sorties connues de Windows sont gardées)
                var sorties = new HashSet<string>(Registre.SousCles(CleSorties).Select(NormaliserGuid).Where(g => g != ""));
                foreach (var k in Registre.SousCles(CleApo + @"\Child APOs"))
                {
                    string g = NormaliserGuid(k);
                    if (g == "" || (sorties.Count > 0 && !sorties.Contains(g)) || l.Any(x => x.Guid == g)) continue;
                    string proprietes = CleSorties + @"\{" + g + @"}\Properties";
                    l.Add(new SortieAudio { Guid = g, Nom = Registre.Texte(proprietes, "{a45c254e-df1c-4efd-8020-67d146a850e0},2"), Carte = Registre.Texte(proprietes, "{b3f8fa53-0004-438e-9003-51a46e139bfc},6") });
                }
            }
            catch { }
            return l;
        }

        /// <summary>Nom lisible d'une sortie de Windows d'après son identifiant ("" si inconnue). Lecture seule.</summary>
        public static string NomSortie(string guid)
        {
            guid = NormaliserGuid(guid);
            if (guid == "" || !Natif.EstWindows) return "";
            try
            {
                string proprietes = CleSorties + @"\{" + guid + @"}\Properties";
                var s = new SortieAudio { Guid = guid, Nom = Registre.Texte(proprietes, "{a45c254e-df1c-4efd-8020-67d146a850e0},2"), Carte = Registre.Texte(proprietes, "{b3f8fa53-0004-438e-9003-51a46e139bfc},6") };
                return s.Nom == "" && s.Carte == "" ? "" : s.Libelle;
            }
            catch { return ""; }
        }

        /// <summary>Identifiant de la sortie par défaut de Windows ("" si elle n'a pas pu être lue). Lecture seule.</summary>
        public static string SortieParDefaut() { return AudioWindows.GuidDe(AudioWindows.ParDefaut()); }

        /// <summary>
        /// Sortie à écrire dans le fichier ("" = toutes les sorties où Equalizer APO est installé).
        /// « etat » : 0 = la sortie visée a Equalizer APO ; 1 = Auto, mais la sortie par défaut de Windows ne l'a pas
        /// (toutes les sorties sont alors visées) ; 2 = la sortie choisie ne l'a plus ; 3 = il n'est installé sur aucune sortie.
        /// </summary>
        /// <summary>« etat » 4 (Auto, avec Voicemeeter) : la sortie par défaut est Voicemeeter, l'égaliseur vise l'appareil de
        /// son bus A (« nomDefaut » : nom de la sortie par défaut ; « appareilVm » : appareil du bus A de Voicemeeter).</summary>
        public static string Resoudre(string cible, List<SortieAudio> sorties, string defaut, out int etat, string nomDefaut, string appareilVm)
        {
            // « Pas boostés » allumé : la sortie par défaut est le câble, le son finit sur le casque (c'est lui qu'on vise)
            if (cible == "auto")
            {
                string effectif = MoteurSon.DefautEffectif(NormaliserGuid(defaut));
                if (effectif != NormaliserGuid(defaut)) return Resoudre(cible, sorties, effectif, out etat);
            }
            string g = Resoudre(cible, sorties, defaut, out etat);
            if (etat != 1 || cible != "auto" || !EstVoicemeeter(nomDefaut)) return g;
            string vm = SortieVoicemeeter(appareilVm, sorties);
            if (vm == "") return g;
            etat = 4;
            return vm;
        }

        /// <summary>Vrai si ce nom de sortie est un appareil virtuel de Voicemeeter (« Voicemeeter Input (VB-Audio Voicemeeter VAIO) »).</summary>
        public static bool EstVoicemeeter(string nom)
        {
            return nom != null && (nom.IndexOf("voicemeeter", StringComparison.OrdinalIgnoreCase) >= 0 || nom.IndexOf("vb-audio", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string NomComparable(string s)
        {
            s = Regex.Replace((s ?? "").Trim(), @"^(wdm|ks|mme|asio)\s*:\s*", "", RegexOptions.IgnoreCase);
            return Regex.Replace(s, @"\s+", " ").ToLowerInvariant();
        }

        /// <summary>Sortie (avec Equalizer APO) qui correspond à l'appareil d'un bus de Voicemeeter, "" si aucune ou plusieurs.
        /// Voicemeeter peut couper les noms longs (pilote MME : 31 caractères) : un début commun suffit.</summary>
        public static string SortieVoicemeeter(string appareil, List<SortieAudio> sorties)
        {
            string a = NomComparable(appareil);
            if (a.Length < 4 || EstVoicemeeter(a)) return "";
            var trouvees = sorties.Where(s =>
            {
                string n = NomComparable(s.Libelle);
                if (n == a) return true;
                return Math.Min(n.Length, a.Length) >= 10 && (n.StartsWith(a, StringComparison.Ordinal) || a.StartsWith(n, StringComparison.Ordinal));
            }).Select(s => s.Guid).Distinct().ToList();
            return trouvees.Count == 1 ? trouvees[0] : "";
        }

        public static string Resoudre(string cible, List<SortieAudio> sorties, string defaut, out int etat)
        {
            etat = sorties.Count == 0 ? 3 : 0;
            if (cible == "toutes") return "";
            if (cible != "auto")
            {
                string g = NormaliserGuid(cible);
                if (etat == 0 && !sorties.Any(s => s.Guid == g)) etat = 2;
                return g;
            }
            defaut = NormaliserGuid(defaut);
            if (defaut != "" && sorties.Any(s => s.Guid == defaut)) return defaut;
            if (etat == 0) etat = 1;
            return "";
        }

        // ------------------------------------------------------------ détection du jeu (lecture seule)

        private static readonly Regex RegexJeu = new Regex(@"^(cod|cod\d\d-cod|sp\d\d-cod|cod-?hq|modernwarfare|warzone|blackops\w*|vanguard)\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static Regex Exe(string motif) { return new Regex("^(" + motif + @")\.exe$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant); }

        /// <summary>
        /// Jeux reconnus et leur profil (bandes : Graves 100 Hz, Bas-médium 250, Voix 1,2 k, Pas 2,5 k, Recharge 4 k, Aigus
        /// 7,5 k). Points de départ réglés d'après le son de chaque jeu (où tombent ses pas) : chacun peut les ajuster.
        /// </summary>
        public static readonly JeuAudio[] Jeux =
        {
            new JeuAudio { Cle = "cod", Nom = "Call of Duty / Warzone", Exe = RegexJeu, Gains = new double[] { -5, -3, 2, 4, 3, -3 }, Texte = "Pas et recharges en avant, explosions calmées",
                Spatial = "Mode casque amélioré (Enhanced Headphone Mode)",
                DansLeJeu = "effets à 100, musique à 0, dialogues vers 40, audio mono désactivé et réduction des acouphènes activée (elle calme les explosions). Essaie le mix Treyarch puis le mix Casque : garde celui où tu situes le mieux les pas." },
            new JeuAudio { Cle = "valorant", Nom = "Valorant", Exe = Exe("valorant-win64-shipping"), Gains = new double[] { -4, -2, 1, 4, 4, -2 }, Texte = "Pas secs et capacités bien détachés",
                Spatial = "HRTF", DansLeJeu = "musique au minimum ; l'option HRTF (Paramètres > Audio) place les sons autour de toi au casque." },
            new JeuAudio { Cle = "cs2", Nom = "Counter-Strike 2", Exe = Exe("cs2"), Gains = new double[] { -4, -1, 2, 4, 3, -1 }, Texte = "Pas et direction des sons",
                DansLeJeu = "le jeu spatialise déjà le son au casque : n'y ajoute pas le 7.1 virtuel. Musique au minimum." },
            new JeuAudio { Cle = "fortnite", Nom = "Fortnite", Exe = Exe("fortniteclient-win64-shipping(_eac|_be|_eac_eos)?"), Gains = new double[] { -3, -2, 2, 4, 2, -2 }, Texte = "Pas, coffres et constructions",
                Spatial = "Casque 3D (3D Headphones)", DansLeJeu = "musique au minimum ; l'option Casque 3D (menu Audio) spatialise les pas au casque." },
            new JeuAudio { Cle = "apex", Nom = "Apex Legends", Exe = Exe("r5apex(_dx12)?"), Gains = new double[] { -5, -3, 2, 5, 3, -3 }, Texte = "Pas très en avant, graves calmés" },
            new JeuAudio { Cle = "pubg", Nom = "PUBG", Exe = Exe("tslgame"), Gains = new double[] { -4, -2, 2, 4, 3, -2 }, Texte = "Pas et véhicules lointains" },
            new JeuAudio { Cle = "r6", Nom = "Rainbow Six Siege", Exe = Exe("rainbowsix(_vulkan|_be)?"), Gains = new double[] { -3, -1, 2, 4, 3, -1 }, Texte = "Pas au-dessus et en dessous de toi" },
            new JeuAudio { Cle = "tarkov", Nom = "Escape from Tarkov", Exe = Exe("escapefromtarkov"), Gains = new double[] { -2, 2, 2, 3, 2, -2 }, Texte = "Pas plus graves dans ce jeu : bas-médium remonté" },
            new JeuAudio { Cle = "overwatch", Nom = "Overwatch 2", Exe = Exe("overwatch"), Gains = new double[] { -3, -2, 2, 3, 2, -1 }, Texte = "Pas et répliques des héros",
                Spatial = "Dolby Atmos pour casque", DansLeJeu = "musique au minimum ; l'option Dolby Atmos pour casque (menu Son) spatialise le son au casque." },
            new JeuAudio { Cle = "bf2042", Nom = "Battlefield 2042", Exe = Exe("bf2042"), Gains = new double[] { -6, -3, 2, 4, 3, -3 }, CoupeBas = 60, Texte = "Explosions coupées, pas et voix en avant" },
        };

        /// <summary>Le jeu reconnu pour ce programme, ou null.</summary>
        public static JeuAudio JeuDe(string exe) { return exe == null ? null : Jeux.FirstOrDefault(j => j.Exe.IsMatch(exe)); }

        /// <summary>Vrai si ce programme est un jeu reconnu (Call of Duty / Warzone, Valorant, CS2…).</summary>
        public static bool EstJeu(string exe) { return JeuDe(exe) != null; }

        /// <summary>Le jeu dont on parle : celui qui tourne, sinon celui dont le profil est chargé, sinon Call of Duty / Warzone.</summary>
        public static JeuAudio JeuVise(string exeEnCours, ReglagesAudioJeu r)
        {
            return JeuDe(exeEnCours)
                ?? (r == null ? null : Jeux.FirstOrDefault(j => j.Gains.SequenceEqual(r.Gains) && j.CoupeBas == r.CoupeBas))
                ?? Jeux[0];
        }

        /// <summary>
        /// Auto jeux : l'égaliseur suit le jeu (allumé pendant la partie, coupé après) ; avec « profil adapté à chaque jeu »,
        /// le profil du jeu est chargé à son lancement, une fois (un réglage fait pendant la partie n'est pas écrasé).
        /// Modifie « r » ; vrai s'il faut réécrire. Partagé par la page Audio et le gardien.
        /// </summary>
        public static bool SuivreJeu(ReglagesAudioJeu r, string exe, bool actifActuel)
        {
            var j = JeuDe(exe);
            bool voulu = j != null, changer = actifActuel != voulu;
            r.Actif = voulu;
            string cle = j != null ? j.Cle : "";
            if (j != null && r.ProfilParJeu && r.JeuEnCours != cle)
            {
                r.Gains = r.GainsDuJeu(j); r.CoupeBas = j.CoupeBas;
                changer = true;
            }
            if (r.JeuEnCours != cle) { r.JeuEnCours = cle; changer = true; }
            return changer;
        }

        /// <summary>Programme du jeu qui tourne en ce moment (« cod.exe »), ou null. Seule la liste des programmes est lue.</summary>
        public static string JeuDetecte()
        {
            string trouve = null;
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try { if (trouve == null && EstJeu(p.ProcessName + ".exe")) trouve = p.ProcessName + ".exe"; }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            return trouve;
        }

        // ------------------------------------------------------------ réglages

        private static string FichierReglages { get { return ReglagesEssai ?? Path.Combine(Chemins.Local, "audio-jeu.json"); } }

        public static ReglagesAudioJeu Lire() { return ReglagesAudioJeu.DepuisJson(Json.LireFichier(FichierReglages) as Dictionary<string, object>); }

        public static void Enregistrer(ReglagesAudioJeu r)
        {
            try { Directory.CreateDirectory(Path.GetDirectoryName(FichierReglages)); FichierSur.EcrireInterface(FichierReglages, Json.Ecrire(r.VersJson())); }
            catch (Exception ex) { JournalDemarrage.Erreur("réglages de l'audio jeu", ex); }
        }

        /// <summary>Un changement vient d'être annulé depuis l'Historique : réglages et fichier sont revenus à l'état d'avant
        /// (appelé hors du fil de l'interface).</summary>
        public static event Action Annule;

        // ------------------------------------------------------------ fichiers d'Equalizer APO

        private static readonly object verrou = new object();
        private static readonly Encoding Octets = Encoding.Latin1;   // un octet = un caractère, dans les deux sens
        private const int TailleMax = 4 * 1024 * 1024;

        private static string Fichier { get { return Path.Combine(DossierConfig, NomFichier); } }
        private static string Config { get { return Path.Combine(DossierConfig, "config.txt"); } }

        private static byte[] ToutLire(FileStream fs)
        {
            if (fs.Length > TailleMax) throw new InvalidDataException(ConfigInattendu);
            var o = new byte[fs.Length];
            int lu = 0;
            fs.Position = 0;
            while (lu < o.Length) { int n = fs.Read(o, lu, o.Length - lu); if (n <= 0) break; lu += n; }
            if (lu != o.Length) Array.Resize(ref o, lu);
            return o;
        }

        /// <summary>Le service audio de Windows relit ces fichiers à chaque changement : s'il en a un en main au même
        /// instant, Windows refuse l'ouverture un court moment. On réessaie quelques fois.</summary>
        private static T Reessayer<T>(Func<T> faire)
        {
            for (int essai = 0; ; essai++)
            {
                try { return faire(); }
                catch (FileNotFoundException) { throw; }
                catch (DirectoryNotFoundException) { throw; }
                catch (IOException) { if (essai >= 12) throw; Thread.Sleep(40); }
            }
        }

        /// <summary>Contenu d'un fichier, ou null s'il n'existe pas.</summary>
        private static byte[] LireOctets(string chemin)
        {
            if (!File.Exists(chemin)) return null;
            try { return Reessayer(() => { using (var fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) return ToutLire(fs); }); }
            catch (FileNotFoundException) { return null; }
        }

        /// <summary>
        /// Écriture directe dans le fichier, comme le prototype et les autres outils qui pilotent Equalizer APO : il
        /// surveille les écritures de son dossier, et un fichier remplacé par renommage (fichier temporaire) risquerait
        /// de ne pas être relu. Texte en ASCII, sans marque d'encodage.
        /// </summary>
        /// <summary>
        /// Écrit le fichier d'un seul coup : contenu écrit à côté puis mis à la place (jamais de fichier à moitié écrit si
        /// Velox ou le PC s'arrête pendant l'écriture). Si le dossier n'accepte pas de nouveau fichier (droits limités au
        /// fichier lui-même), écriture directe comme avant.
        /// </summary>
        private static void EcrireOctets(string chemin, byte[] octets)
        {
            string temp = chemin + ".velox-tmp";
            try
            {
                Reessayer(() =>
                {
                    using (var fs = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None)) { fs.Write(octets, 0, octets.Length); fs.Flush(true); }
                    File.Move(temp, chemin, true);
                    return true;
                });
                return;
            }
            catch (UnauthorizedAccessException) { }
            catch (IOException) { }
            finally { try { if (File.Exists(temp)) File.Delete(temp); } catch { } }
            Reessayer(() => { using (var fs = new FileStream(chemin, FileMode.Create, FileAccess.Write, FileShare.Read)) fs.Write(octets, 0, octets.Length); return true; });
        }

        /// <summary>
        /// Texte de config.txt décodé sans perte : chaque octet (ou chaque unité UTF-16, si le fichier commence par cette
        /// marque) devient un caractère. Réécrire le texte redonne donc exactement les octets d'origine pour tout ce qui
        /// n'a pas été touché, quels que soient l'encodage et les fins de ligne du fichier.
        /// </summary>
        private sealed class TexteBrut
        {
            public string Texte = "";
            public int Unite = 1;   // 1 : un octet par caractère ; 2 : UTF-16 (octet faible d'abord) ; 3 : UTF-16 (octet fort d'abord)

            public static TexteBrut Lire(byte[] o)
            {
                var t = new TexteBrut();
                if (o.Length >= 2 && ((o[0] == 0xFF && o[1] == 0xFE) || (o[0] == 0xFE && o[1] == 0xFF)))
                {
                    if (o.Length % 2 != 0) throw new InvalidDataException(ConfigInattendu);
                    t.Unite = o[0] == 0xFF ? 2 : 3;
                    var c = new char[o.Length / 2];
                    for (int i = 0; i < c.Length; i++) c[i] = (char)(t.Unite == 2 ? o[2 * i] | (o[2 * i + 1] << 8) : (o[2 * i] << 8) | o[2 * i + 1]);
                    t.Texte = new string(c);
                }
                else t.Texte = Octets.GetString(o);
                return t;
            }

            public byte[] Encoder(string s)
            {
                if (Unite == 1) return Octets.GetBytes(s);
                var o = new byte[s.Length * 2];
                for (int i = 0; i < s.Length; i++)
                {
                    byte faible = (byte)(s[i] & 0xFF), fort = (byte)(s[i] >> 8);
                    o[2 * i] = Unite == 2 ? faible : fort; o[2 * i + 1] = Unite == 2 ? fort : faible;
                }
                return o;
            }
        }

        // « Include » tel qu'Equalizer APO le lit (casse du mot respectée), nom du fichier sans tenir compte de la casse
        private static readonly Regex RegexInclude = new Regex(@"^[ \t]*Include[ \t]*:[ \t]*(?i:MW_optimizer\.txt)[ \t]*$", RegexOptions.CultureInvariant);
        private static readonly char[] Marques = { '﻿', 'ï', '»', '¿' };   // marque d'encodage en tête de fichier (UTF-16, ou UTF-8 lu octet par octet)

        /// <summary>Lignes du texte, chacune avec sa fin de ligne (\r\n, \n ou \r) : les recoller redonne le texte.</summary>
        private static List<string> Lignes(string texte)
        {
            var l = new List<string>();
            int debut = 0;
            for (int i = 0; i < texte.Length; i++)
            {
                if (texte[i] == '\r' && i + 1 < texte.Length && texte[i + 1] == '\n') continue;   // coupé au \n qui suit
                if (texte[i] == '\n' || texte[i] == '\r') { l.Add(texte.Substring(debut, i + 1 - debut)); debut = i + 1; }
            }
            if (debut < texte.Length) l.Add(texte.Substring(debut));
            return l;
        }

        private static string SansFin(string ligne, bool premiere)
        {
            string s = ligne.TrimEnd('\r', '\n');
            return premiere ? s.TrimStart(Marques) : s;
        }

        private static bool EstInclude(string ligne, bool premiere) { return RegexInclude.IsMatch(SansFin(ligne, premiere)); }

        internal static bool IncludePresent(string texte)
        {
            var l = Lignes(texte);
            for (int i = 0; i < l.Count; i++) if (EstInclude(l[i], i == 0)) return true;
            return false;
        }

        /// <summary>Remplace le contenu du fichier ouvert en n'écrivant que ce qui change (à partir du premier octet différent).</summary>
        private static void EcrireDifference(FileStream fs, byte[] ancien, byte[] nouveau)
        {
            int commun = 0;
            while (commun < ancien.Length && commun < nouveau.Length && ancien[commun] == nouveau[commun]) commun++;
            fs.Position = commun;
            if (nouveau.Length > commun) fs.Write(nouveau, commun, nouveau.Length - commun);
            fs.SetLength(nouveau.Length);
            fs.Flush();
        }

        /// <summary>
        /// Vérifie que config.txt inclut le fichier de l'égaliseur ; sinon ajoute la ligne à la fin, dans l'encodage et
        /// avec les fins de ligne du fichier. Rien d'autre n'est modifié. « sansSaut » : le fichier ne finissait pas par
        /// une fin de ligne (il a fallu en ajouter une avant : retirée aussi si l'ajout est annulé).
        /// </summary>
        private static void AssurerInclude(out bool ajoute, out bool sansSaut)
        {
            bool a = false, s = false;
            Reessayer(() =>
            {
                using (var fs = new FileStream(Config, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read))
                {
                    var ancien = ToutLire(fs);
                    var t = TexteBrut.Lire(ancien);
                    if (IncludePresent(t.Texte)) return true;
                    string saut = t.Texte.Contains("\r\n") ? "\r\n" : t.Texte.Contains("\n") ? "\n" : "\r\n";
                    string contenu = t.Texte.TrimStart(Marques);
                    s = contenu.Length > 0 && !contenu.EndsWith("\n") && !contenu.EndsWith("\r");
                    var ajout = t.Encoder((s ? saut : "") + LigneInclude + saut);
                    fs.Position = ancien.Length;
                    fs.Write(ajout, 0, ajout.Length);
                    fs.Flush();
                    a = true;
                    return true;
                }
            });
            ajoute = a; sansSaut = s;
        }

        /// <summary>Retire de config.txt la ligne Include du fichier de l'égaliseur (et rien d'autre). « sansSaut » : voir AssurerInclude.</summary>
        private static void RetirerInclude(bool sansSaut)
        {
            if (!File.Exists(Config)) return;
            Reessayer(() =>
            {
                using (var fs = new FileStream(Config, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
                {
                    var ancien = ToutLire(fs);
                    var t = TexteBrut.Lire(ancien);
                    var l = Lignes(t.Texte);
                    bool change = false;
                    for (int i = l.Count - 1; i >= 0; i--)
                    {
                        if (!EstInclude(l[i], i == 0)) continue;
                        bool derniere = i == l.Count - 1;
                        // (la marque d'encodage en tête de fichier reste en place)
                        string garde = i == 0 ? l[i].Substring(0, l[i].Length - l[i].TrimStart(Marques).Length) : "";
                        if (garde != "") l[i] = garde; else l.RemoveAt(i);
                        if (derniere && sansSaut && i > 0) l[i - 1] = l[i - 1].TrimEnd('\r', '\n');
                        change = true;
                    }
                    if (change) EcrireDifference(fs, ancien, t.Encoder(string.Concat(l)));
                    return true;
                }
            });
        }

        /// <summary>Vrai si ce contenu applique quelque chose : au moins une ligne qui n'est ni vide ni un commentaire.</summary>
        internal static bool ContenuActif(string contenu)
        {
            if (EstVolumeSeul(contenu)) return false;   // comparaison en cours : égaliseur coupé (seul le volume reste)
            return Lignes(contenu).Select((x, i) => SansFin(x, i == 0).Trim()).Any(x => x != "" && !x.StartsWith("#"));
        }

        /// <summary>Ce que dit le dossier d'Equalizer APO en ce moment. Lecture seule.</summary>
        public static EtatAudioJeu Etat()
        {
            var e = new EtatAudioJeu();
            try
            {
                e.Present = Present;
                if (!e.Present) return e;
                var o = LireOctets(Fichier);
                if (o != null)
                {
                    e.Fichier = true;
                    e.Contenu = Octets.GetString(o);
                    e.Actif = ContenuActif(e.Contenu);
                }
                var c = LireOctets(Config);
                if (c != null)
                {
                    string texte = TexteBrut.Lire(c).Texte;
                    e.Include = IncludePresent(texte);
                    string actif = string.Join("\n", Lignes(texte).Select((x, i) => SansFin(x, i == 0).Trim()).Where(x => !x.StartsWith("#"))).ToLowerInvariant();
                    if (actif.Contains("yousound")) e.Autres.Add("YouSound");
                    if (actif.Contains("hesuvi")) e.Autres.Add("HeSuVi");
                    if (actif.Contains("peace.txt")) e.Autres.Add("Peace");
                }
            }
            catch { }
            return e;
        }

        private static string Raison(Exception ex)
        {
            if (ex is UnauthorizedAccessException || ex is System.Security.SecurityException) return Refus;
            if (ex is InvalidDataException) return ConfigInattendu;
            if (ex is DirectoryNotFoundException) return Absent;
            if (ex is IOException) return Occupe;
            return ex.Message;
        }

        /// <summary>Ligne de l'Historique qui décrit ces réglages.</summary>
        public static string Titre(ReglagesAudioJeu r)
        {
            if (r.AutoJeu) return "Audio jeu : Auto jeux (" + NomProfil(r) + ")";
            return r.Actif ? "Audio jeu : égaliseur actif (" + NomProfil(r) + ")" : "Audio jeu : égaliseur coupé";
        }

        /// <summary>
        /// Écrit le fichier d'Equalizer APO d'après les réglages et vérifie que config.txt l'inclut, puis enregistre les
        /// réglages. « guid » : la sortie visée ("" = toutes). Égaliseur coupé : le fichier passe à une ligne de
        /// commentaire ; s'il n'existe pas encore, rien n'est créé. Renvoie "" si c'est fait, sinon la raison (rien n'est
        /// alors changé).
        /// Historique : un changement demandé par l'utilisateur y est noté dès qu'un fichier change ; « toujours » le note
        /// même sans changement immédiat (Auto Warzone activé : il autorise les changements qui suivront). Une bascule
        /// automatique (« utilisateur » faux : le jeu se lance ou se ferme, la sortie par défaut change) suit un choix déjà
        /// noté et n'ajoute pas de ligne, sauf si elle a dû compléter config.txt.
        /// </summary>
        public static string Appliquer(ReglagesAudioJeu r, string guid, bool utilisateur, bool toujours = false)
        {
            lock (verrou)
            {
                try
                {
                    if (!Present) return Absent;
                    guid = NormaliserGuid(guid);
                    byte[] nouveau = Encoding.ASCII.GetBytes(Generer(r, guid));
                    byte[] avant = LireOctets(Fichier);
                    var reglagesAvant = Json.LireFichier(FichierReglages) as Dictionary<string, object>;
                    // (égaliseur coupé alors que le fichier l'est déjà, même écrit autrement par un autre outil : rien à réécrire)
                    bool ecrire = avant == null ? r.Actif : r.Actif ? !avant.SequenceEqual(nouveau) : ContenuActif(Octets.GetString(avant));
                    bool ajoute = false, sansSaut = false;
                    if (ecrire) EcrireOctets(Fichier, nouveau);
                    if (r.Actif)
                    {
                        try { AssurerInclude(out ajoute, out sansSaut); }
                        catch
                        {
                            // config.txt n'a pas pu être complété : le fichier de l'égaliseur revient comme il était
                            if (ecrire) { try { if (avant == null) File.Delete(Fichier); else EcrireOctets(Fichier, avant); } catch { } }
                            throw;
                        }
                    }
                    r.CibleEcrite = r.Actif ? guid : "";
                    Enregistrer(r);
                    if (utilisateur ? (ecrire || ajoute || toujours) : ajoute) Noter(Titre(r), utilisateur, avant, ajoute, sansSaut, reglagesAvant);
                }
                catch (Exception ex) { return Raison(ex); }
            }
            return "";
        }

        /// <summary>
        /// Bouton « Comparer » : pendant l'appui, le fichier passe à l'égaliseur coupé ; au relâchement, il revient tel qu'il
        /// était. Rien n'est enregistré ni noté dans l'Historique. Seulement si le fichier en place est celui de Velox
        /// (« attendu ») : un fichier écrit par un autre outil n'est jamais touché. Renvoie "" si c'est fait.
        /// </summary>
        public static string Comparer(bool couper, string attendu, string sansEgaliseur = null)
        {
            lock (verrou)
            {
                try
                {
                    if (!Present || string.IsNullOrEmpty(attendu)) return Absent;
                    var avant = LireOctets(Fichier);
                    if (avant == null) return Absent;
                    // « sans » : égaliseur coupé, ou seulement un volume (même niveau que « avec » : comparaison honnête)
                    string coupe = sansEgaliseur ?? LigneCoupe + "\r\n";
                    string actuel = Octets.GetString(avant), voulu = couper ? coupe : attendu;
                    if (actuel != attendu && actuel != coupe && actuel != LigneCoupe + "\r\n" && !EstVolumeSeul(actuel)) return "Le fichier en place ne vient pas de Velox : rien n'a été modifié.";
                    if (actuel != voulu) EcrireOctets(Fichier, Encoding.ASCII.GetBytes(voulu));
                    return "";
                }
                catch (Exception ex) { return Raison(ex); }
            }
        }

        /// <summary>
        /// Fichier « sans égaliseur, même volume » pour comparer : le même en-tête et la même sortie visée que le fichier de
        /// l'égaliseur, mais un seul réglage de volume (aucun filtre).
        /// </summary>
        public static string VolumeSeul(ReglagesAudioJeu r, string guid, double db)
        {
            var c = CultureInfo.InvariantCulture;
            const string sansMicro = "(sizeof(regexSearch(\"capture\", stage)) == 0)";
            guid = NormaliserGuid(guid);
            db = Math.Max(-12, Math.Min(6, db));
            return Entete + "\r\n" + (guid != "" ? "If: (sizeof(regexSearch(\"" + guid + "\", tolower(deviceGuid))) > 0) and " + sansMicro : "If: " + sansMicro) + "\r\n"
                + "Preamp: " + db.ToString("F1", c) + " dB\r\n" + MarqueVolumeSeul + "\r\nEndIf:\r\n";
        }

        private const string MarqueVolumeSeul = "# Velox : comparaison, egaliseur coupe au meme volume";
        private static bool EstVolumeSeul(string contenu) { return contenu != null && contenu.StartsWith(Entete, StringComparison.Ordinal) && contenu.Contains(MarqueVolumeSeul) && !contenu.Contains("Filter") && !contenu.Contains("Include") && !contenu.Contains("Copy"); }

        // ------------------------------------------------------------ Historique : noter, annuler

        /// <summary>Deux changements à moins de deux minutes d'écart (un curseur qu'on ajuste, un profil puis un autre)
        /// forment une seule ligne de l'Historique : « Annuler » revient à l'état d'avant le premier.</summary>
        internal static int FusionSecondes = 120;

        private static void Noter(string titre, bool utilisateur, byte[] avant, bool includeAjoute, bool sansSaut, Dictionary<string, object> reglagesAvant)
        {
            string maintenant = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            Historique.Modifier(l =>
            {
                var d = l.LastOrDefault(m => m.Type == "audiojeu");
                if (d != null && !d.Annule)
                {
                    DateTime maj;
                    double ecart = DateTime.TryParse(d.D("Maj"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out maj) ? (DateTime.Now - maj).TotalSeconds : -1;
                    // bascule automatique qui a complété config.txt : c'est la suite du dernier choix noté, quel que soit son âge
                    if (!utilisateur || (ecart >= 0 && ecart < FusionSecondes))
                    {
                        if (utilisateur) { d.Titre = titre; d.Donnees["Maj"] = maintenant; }
                        if (includeAjoute) { d.Donnees["IncludeAjoute"] = true; d.Donnees["FinSansSaut"] = sansSaut; }
                        return true;
                    }
                }
                l.Add(new Modif
                {
                    Type = "audiojeu", Titre = titre,
                    Donnees = new Dictionary<string, object>
                    {
                        { "Existait", avant != null }, { "Avant", avant == null ? "" : Octets.GetString(avant) }, { "IncludeAjoute", includeAjoute }, { "FinSansSaut", sansSaut },
                        { "Reglages", reglagesAvant }, { "Maj", maintenant },
                    },
                });
                return true;
            });
        }

        /// <summary>
        /// Annule un changement noté dans l'Historique : le fichier de l'égaliseur retrouve son contenu d'avant (ou
        /// disparaît s'il n'existait pas), la ligne Include est retirée de config.txt si c'est Velox qui l'avait ajoutée,
        /// et les réglages de l'outil reviennent à ceux d'avant. Le chemin écrit ne vient jamais de l'Historique.
        /// </summary>
        public static string Annuler(Modif e)
        {
            lock (verrou)
            {
                try
                {
                    if (!Present) return "Equalizer APO est introuvable sur ce PC";
                    string avant = e.D("Avant");
                    if (avant.Length > 256 * 1024 || avant.Any(c => c > 255)) return "entrée de l'historique invalide (refusée par sécurité)";
                    if (Json.Bool(e.Donnees, "IncludeAjoute")) RetirerInclude(Json.Bool(e.Donnees, "FinSansSaut"));
                    if (Json.Bool(e.Donnees, "Existait")) EcrireOctets(Fichier, Octets.GetBytes(avant));
                    else if (File.Exists(Fichier)) Reessayer(() => { File.Delete(Fichier); return true; });
                    var reglages = Json.Dict(e.Donnees, "Reglages");
                    if (reglages != null) Enregistrer(ReglagesAudioJeu.DepuisJson(reglages));
                    else { try { File.Delete(FichierReglages); } catch { } }   // premier usage annulé : comme si l'outil n'avait jamais servi
                }
                catch (Exception ex) { return Raison(ex); }
            }
            var h = Annule;
            if (h != null) { try { h(); } catch { } }
            return "";
        }

        /// <summary>
        /// Désinstallation de Velox : retire la ligne Include de config.txt, puis le fichier de l'égaliseur. Le reste de
        /// config.txt n'est pas touché. Lancé avec les droits de l'installateur dans un dossier que tout le monde peut
        /// modifier : un fichier remplacé par un lien n'est pas suivi.
        /// </summary>
        public static int Retirer()
        {
            lock (verrou)
            {
                try
                {
                    if (!Present) return 0;
                    if (File.Exists(Config) && Protection.CheminSansLien(Config)) RetirerInclude(false);
                    if (File.Exists(Fichier) && Protection.CheminSansLien(Fichier)) Reessayer(() => { File.Delete(Fichier); return true; });
                    return 0;
                }
                catch (Exception ex) { JournalDemarrage.Erreur("retrait de l'audio jeu", ex); return 1; }
            }
        }
    }
}
