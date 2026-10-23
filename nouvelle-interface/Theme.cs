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

        /// <summary>Accent lumineux de la barre de menu (toujours sombre).</summary>
        public Color AccentBarre { get { return Color.FromRgb(61, 245, 255); } }
    }

    public static class Theme
    {
        public static Palette P = Palette.SombreNoyau;

        /// <summary>Animations autorisées (coupées pour les captures automatiques, ou si l'utilisateur le demande).</summary>
        public static bool Animations = true;

        public static readonly FontFamily Affichage = new FontFamily("Bahnschrift, Segoe UI, DejaVu Sans, sans-serif");
        public static readonly FontFamily Corps = new FontFamily("Segoe UI, DejaVu Sans, sans-serif");
        public static readonly FontFamily Console = new FontFamily("Cascadia Mono, Consolas, DejaVu Sans Mono, monospace");

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
