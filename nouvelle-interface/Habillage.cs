// Nettoyage PC 5 - thème « Noyau » : palette, polices et petits outils de dessin.
using System;
using Avalonia;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    /// <summary>Couleurs d'un thème (sombre ou clair). La barre de menu reste sombre dans les deux.</summary>
    public sealed class Palette
    {
        public bool Sombre;
        public Color Fond, Carte, CarteSurvol, Barre, Bordure, Texte, Discret, Accent, Accent2, Orange, Rouge, Vert, SurAccent, TexteBarre;

        public static readonly Palette SombreNoyau = new Palette
        {
            Sombre = true,
            Fond = Color.FromRgb(6, 10, 18), Carte = Color.FromRgb(10, 18, 32), CarteSurvol = Color.FromRgb(14, 26, 44), Barre = Color.FromRgb(7, 12, 22),
            Bordure = Color.FromRgb(24, 50, 63), Texte = Color.FromRgb(221, 235, 245), Discret = Color.FromRgb(138, 160, 180),
            Accent = Color.FromRgb(61, 245, 255), Accent2 = Color.FromRgb(48, 186, 255), Orange = Color.FromRgb(255, 181, 71),
            Rouge = Color.FromRgb(255, 107, 122), Vert = Color.FromRgb(108, 240, 176), SurAccent = Color.FromRgb(4, 18, 26), TexteBarre = Color.FromRgb(200, 216, 230),
        };

        public static readonly Palette ClairNoyau = new Palette
        {
            Sombre = false,
            Fond = Color.FromRgb(238, 243, 247), Carte = Color.FromRgb(255, 255, 255), CarteSurvol = Color.FromRgb(246, 249, 251), Barre = Color.FromRgb(7, 12, 22),
            Bordure = Color.FromRgb(203, 217, 227), Texte = Color.FromRgb(14, 28, 38), Discret = Color.FromRgb(76, 97, 112),
            Accent = Color.FromRgb(0, 122, 158), Accent2 = Color.FromRgb(0, 98, 168), Orange = Color.FromRgb(168, 95, 0),
            Rouge = Color.FromRgb(194, 40, 59), Vert = Color.FromRgb(10, 122, 82), SurAccent = Color.FromRgb(255, 255, 255), TexteBarre = Color.FromRgb(200, 216, 230),
        };

        /// <summary>Style HUD (version 5.5) : noir profond, cyan franc, violet du logo.</summary>
        public static readonly Palette SombreHud = new Palette
        {
            Sombre = true,
            Fond = Color.FromRgb(4, 7, 12), Carte = Color.FromRgb(6, 15, 23), CarteSurvol = Color.FromRgb(10, 24, 36), Barre = Color.FromRgb(4, 10, 16),
            Bordure = Color.FromRgb(18, 54, 66), Texte = Color.FromRgb(227, 246, 255), Discret = Color.FromRgb(127, 163, 181),
            Accent = Color.FromRgb(0, 229, 255), Accent2 = Color.FromRgb(0, 196, 255), Orange = Color.FromRgb(255, 181, 71),
            Rouge = Color.FromRgb(255, 122, 138), Vert = Color.FromRgb(61, 255, 162), SurAccent = Color.FromRgb(4, 7, 12), TexteBarre = Color.FromRgb(169, 199, 211),
        };

        public static readonly Palette ClairHud = new Palette
        {
            Sombre = false,
            Fond = Color.FromRgb(236, 242, 246), Carte = Color.FromRgb(255, 255, 255), CarteSurvol = Color.FromRgb(244, 249, 252), Barre = Color.FromRgb(4, 10, 16),
            Bordure = Color.FromRgb(190, 212, 224), Texte = Color.FromRgb(8, 22, 32), Discret = Color.FromRgb(70, 96, 112),
            Accent = Color.FromRgb(0, 122, 150), Accent2 = Color.FromRgb(0, 104, 140), Orange = Color.FromRgb(168, 95, 0),
            Rouge = Color.FromRgb(194, 40, 59), Vert = Color.FromRgb(10, 122, 82), SurAccent = Color.FromRgb(255, 255, 255), TexteBarre = Color.FromRgb(169, 199, 211),
        };

        /// <summary>Palette du style actuel (HUD ou classique).</summary>
        public static Palette Pour(bool sombre)
        {
            if (Habillage.Hud) return sombre ? SombreHud : ClairHud;
            return sombre ? SombreNoyau : ClairNoyau;
        }

        /// <summary>Accent lumineux de la barre de menu (toujours sombre) : suit la couleur de l'interface.</summary>
        public Color AccentBarre = Color.FromRgb(61, 245, 255);
    }

    /// <summary>Couleur de l'interface : « Noyau » (cyan) d'origine, les autres font partie de Premium.
    /// Mêmes clés que la version 4 (réglage « Accent » de parametres.json), donc la couleur choisie suit le passage 4 -> 5.</summary>
    public sealed class CouleurInterface
    {
        public string Cle, Nom;
        public bool Premium;
        public Color Sombre1, Sombre2, Clair1, Clair2;   // dégradé en thème sombre (lumineux) et en thème clair (assez foncé pour du texte blanc)
    }

    public static class Habillage
    {
        public static Palette P = Palette.SombreHud;

        private static Color C(int r, int g, int b) { return Color.FromRgb((byte)r, (byte)g, (byte)b); }

        public static readonly CouleurInterface[] Couleurs =
        {
            new CouleurInterface { Cle = "aurore", Nom = "Noyau", Sombre1 = C(61, 245, 255), Sombre2 = C(48, 186, 255), Clair1 = C(0, 122, 158), Clair2 = C(0, 98, 168) },
            new CouleurInterface { Cle = "ocean", Nom = "Océan", Premium = true, Sombre1 = C(56, 189, 248), Sombre2 = C(47, 148, 242), Clair1 = C(29, 78, 216), Clair2 = C(37, 99, 235) },
            new CouleurInterface { Cle = "emeraude", Nom = "Émeraude", Premium = true, Sombre1 = C(52, 211, 153), Sombre2 = C(34, 183, 145), Clair1 = C(4, 120, 87), Clair2 = C(15, 118, 110) },
            new CouleurInterface { Cle = "amethyste", Nom = "Améthyste", Premium = true, Sombre1 = C(192, 132, 252), Sombre2 = C(161, 99, 245), Clair1 = C(126, 34, 206), Clair2 = C(109, 40, 217) },
            new CouleurInterface { Cle = "soleil", Nom = "Coucher de soleil", Premium = true, Sombre1 = C(251, 146, 60), Sombre2 = C(244, 113, 102), Clair1 = C(194, 65, 12), Clair2 = C(190, 24, 93) },
            new CouleurInterface { Cle = "rubis", Nom = "Rubis", Premium = true, Sombre1 = C(251, 113, 133), Sombre2 = C(224, 70, 100), Clair1 = C(190, 18, 60), Clair2 = C(159, 18, 57) },
        };

        public static string CouleurActuelle { get; private set; } = "aurore";

        /// <summary>Applique une couleur aux deux palettes (sombre et clair) ; l'interface doit ensuite être reconstruite.</summary>
        public static void ChoisirCouleur(string cle)
        {
            CouleurInterface c = Couleurs[0];
            foreach (var x in Couleurs) if (x.Cle == cle) c = x;
            CouleurActuelle = c.Cle;
            foreach (var s in new[] { Palette.SombreNoyau, Palette.SombreHud })
            {
                // le style HUD garde son cyan franc pour la couleur « Noyau »
                bool hudNoyau = s == Palette.SombreHud && c.Cle == "aurore";
                s.Accent = hudNoyau ? C(0, 229, 255) : c.Sombre1; s.Accent2 = hudNoyau ? C(0, 196, 255) : c.Sombre2;
                s.SurAccent = TexteSur(Melanger(s.Accent, s.Accent2, 0.5));
                s.AccentBarre = s.Accent;
            }
            foreach (var l in new[] { Palette.ClairNoyau, Palette.ClairHud })
            {
                l.Accent = c.Clair1; l.Accent2 = c.Clair2;
                l.SurAccent = Colors.White;
                l.AccentBarre = l == Palette.ClairHud ? Palette.SombreHud.Accent : c.Sombre1;
            }
        }

        /// <summary>Animations autorisées (coupées pour les captures automatiques, ou si l'utilisateur le demande).</summary>
        public static bool Animations = true;

        /// <summary>Style HUD (par défaut depuis la 5.5) ; faux = l'apparence classique d'avant.</summary>
        public static bool Hud = true;

        private const string Polices = "avares://NettoyagePC/Assets/Polices#";
        private static readonly FontFamily affichageClassique = new FontFamily("Bahnschrift, Segoe UI, DejaVu Sans, sans-serif");
        private static readonly FontFamily corpsClassique = new FontFamily("Segoe UI, DejaVu Sans, sans-serif");
        private static readonly FontFamily consoleClassique = new FontFamily("Cascadia Mono, Consolas, DejaVu Sans Mono, monospace");
        // polices du HUD intégrées au programme (licence libre SIL OFL, fichiers dans Assets/Polices) ; les symboles
        // qu'elles n'ont pas (★, ✔, ⚠...) viennent des polices de Windows
        private static readonly FontFamily affichageHud = new FontFamily(Polices + "Chakra Petch, Bahnschrift, Segoe UI, DejaVu Sans, sans-serif");
        private static readonly FontFamily corpsHud = new FontFamily(Polices + "IBM Plex Sans, Segoe UI, DejaVu Sans, sans-serif");
        private static readonly FontFamily consoleHud = new FontFamily(Polices + "JetBrains Mono, Cascadia Mono, Consolas, DejaVu Sans Mono, monospace");

        public static FontFamily Affichage { get { return Hud ? affichageHud : affichageClassique; } }
        public static FontFamily Corps { get { return Hud ? corpsHud : corpsClassique; } }
        public static FontFamily Console { get { return Hud ? consoleHud : consoleClassique; } }

        /// <summary>Contour à coins coupés du HUD (haut droit et bas gauche) ; c = taille de la coupe.</summary>
        public static Geometry Chanfrein(Rect r, double c)
        {
            c = Math.Max(0, Math.Min(c, Math.Min(r.Width, r.Height) / 2));
            var g = new StreamGeometry();
            using (var x = g.Open())
            {
                x.BeginFigure(new Point(r.Left, r.Top), true);
                x.LineTo(new Point(r.Right - c, r.Top));
                x.LineTo(new Point(r.Right, r.Top + c));
                x.LineTo(new Point(r.Right, r.Bottom));
                x.LineTo(new Point(r.Left + c, r.Bottom));
                x.LineTo(new Point(r.Left, r.Bottom - c));
                x.EndFigure(true);
            }
            return g;
        }

        public static Color Alpha(Color c, int a) { return Color.FromArgb((byte)Math.Max(0, Math.Min(255, a)), c.R, c.G, c.B); }
        public static IBrush Pinceau(Color c) { return new SolidColorBrush(c); }
        public static IBrush Pinceau(Color c, int a) { return new SolidColorBrush(Alpha(c, a)); }

        public static Color Melanger(Color a, Color b, double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((byte)(a.A + (b.A - a.A) * t), (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
        }

        /// <summary>Texte lisible posé sur une couleur (sombre sur fond clair, blanc sur fond foncé).</summary>
        public static Color TexteSur(Color fond)
        {
            double l = (0.2126 * fond.R + 0.7152 * fond.G + 0.0722 * fond.B) / 255.0;
            return l > 0.45 ? Color.FromRgb(4, 18, 26) : Colors.White;
        }

        public static IBrush DegradeAccent()
        {
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(P.Accent, 0), new GradientStop(P.Accent2, 1) },
            };
        }

        /// <summary>Couleur d'un niveau de santé (0 à 100).</summary>
        public static Color Niveau(double v) { return v >= 80 ? P.Accent : v >= 55 ? P.Orange : P.Rouge; }

        /// <summary>Arc de cercle (angles en degrés, 0 = à droite, sens des aiguilles d'une montre).</summary>
        public static Geometry Arc(Point centre, double r, double debut, double balayage)
        {
            var g = new StreamGeometry();
            using (var x = g.Open())
            {
                double a0 = debut * Math.PI / 180, a1 = (debut + balayage) * Math.PI / 180;
                x.BeginFigure(new Point(centre.X + r * Math.Cos(a0), centre.Y + r * Math.Sin(a0)), false);
                x.ArcTo(new Point(centre.X + r * Math.Cos(a1), centre.Y + r * Math.Sin(a1)), new Size(r, r), 0, balayage > 180, SweepDirection.Clockwise);
                x.EndFigure(false);
            }
            return g;
        }

        /// <summary>Courbe « ralentie à l'arrivée » (0 à 1).</summary>
        public static double Douce(double p) { p = Math.Max(0, Math.Min(1, p)); double q = 1 - p; return 1 - q * q * q; }
    }
}
