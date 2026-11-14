// Velox 5 - « Studio » : l'habillage propre à la rubrique Audio. Une console de mixage sombre (violet nuit, ambre et
// magenta), différente du reste de Velox, dans les deux thèmes : molettes, faders, courbe de l'égaliseur avec le spectre
// en direct, VU-mètres et gros bouton marche. Tout se règle aussi au clavier (flèches, Page préc. / suiv., Début, Fin).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    /// <summary>Couleurs, polices et briques du Studio.</summary>
    internal static class Studio
    {
        public static readonly Color Fond1 = Color.FromRgb(11, 7, 22), Fond2 = Color.FromRgb(27, 13, 44), Panneau = Color.FromRgb(21, 13, 37), PanneauHaut = Color.FromRgb(30, 19, 52),
            Bord = Color.FromRgb(52, 36, 86), Texte = Color.FromRgb(245, 239, 255), Discret = Color.FromRgb(170, 155, 205), Faible = Color.FromRgb(112, 98, 148),
            Ambre = Color.FromRgb(255, 181, 71), Magenta = Color.FromRgb(255, 79, 163), Violet = Color.FromRgb(157, 107, 255), Cyan = Color.FromRgb(60, 230, 224),
            Vert = Color.FromRgb(71, 240, 160), Rouge = Color.FromRgb(255, 92, 122);

        private const string Polices = "avares://NettoyagePC/Assets/Polices#";
        public static readonly FontFamily Titre = new FontFamily(Polices + "Chakra Petch, Bahnschrift, Segoe UI, DejaVu Sans, sans-serif");
        public static readonly FontFamily Corps = new FontFamily(Polices + "IBM Plex Sans, Segoe UI, DejaVu Sans, sans-serif");
        public static readonly FontFamily Chiffres = new FontFamily(Polices + "JetBrains Mono, Cascadia Mono, Consolas, DejaVu Sans Mono, monospace");

        public static IBrush P(Color c, int a = 255) { return Habillage.Pinceau(c, a); }

        public static LinearGradientBrush Degrade(double angle, params Color[] c)
        {
            var g = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = angle == 0 ? new RelativePoint(1, 0, RelativeUnit.Relative) : new RelativePoint(0, 1, RelativeUnit.Relative) };
            for (int i = 0; i < c.Length; i++) g.GradientStops.Add(new GradientStop(c[i], c.Length == 1 ? 0 : (double)i / (c.Length - 1)));
            return g;
        }

        /// <summary>Le fond de la console (dégradé violet nuit, bord lumineux en haut).</summary>
        public static Border Console(Control contenu)
        {
            return new Border
            {
                Child = contenu, CornerRadius = new CornerRadius(24), Padding = new Thickness(26, 22, 26, 26), ClipToBounds = false,
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Fond2, 0), new GradientStop(Fond1, 0.55), new GradientStop(Color.FromRgb(16, 8, 26), 1) },
                },
                BorderBrush = Degrade(0, Habillage.Alpha(Ambre, 150), Habillage.Alpha(Magenta, 120), Habillage.Alpha(Violet, 90)), BorderThickness = new Thickness(1.2),
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 40, OffsetY = 14, Color = Habillage.Alpha(Colors.Black, 150) }),
            };
        }

        /// <summary>Un module de la console : titre en petites capitales, puis le contenu.</summary>
        public static Border Module(string titre, Control contenu, Control aDroite = null)
        {
            var s = new StackPanel { Spacing = 12 };
            if (titre != null)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                g.Children.Add(new TextBlock { Text = Langue.T(titre).ToUpper(Langue.Culture), FontFamily = Chiffres, FontSize = 11, LetterSpacing = 2, Foreground = P(Ambre, 220), VerticalAlignment = VerticalAlignment.Center });
                var trait = new Border { Height = 1, Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Background = Degrade(0, Habillage.Alpha(Ambre, 90), Habillage.Alpha(Magenta, 0)) };
                Grid.SetColumn(trait, 1); g.Children.Add(trait);
                if (aDroite != null) { Grid.SetColumn(aDroite, 2); g.Children.Add(aDroite); }
                s.Children.Add(g);
            }
            s.Children.Add(contenu);
            return new Border
            {
                Child = s, CornerRadius = new CornerRadius(18), Padding = new Thickness(18, 16), Margin = new Thickness(0, 0, 0, 16),
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Habillage.Alpha(PanneauHaut, 235), 0), new GradientStop(Habillage.Alpha(Panneau, 235), 1) },
                },
                BorderBrush = P(Bord, 200), BorderThickness = new Thickness(1),
            };
        }

        public static TextBlock T(string texte, double taille = 13, Color? couleur = null, bool gras = false, FontFamily police = null)
        {
            return new TextBlock { Text = texte, FontFamily = police ?? Corps, FontSize = taille, Foreground = P(couleur ?? Texte), FontWeight = gras ? FontWeight.SemiBold : FontWeight.Normal, TextWrapping = TextWrapping.Wrap };
        }

        /// <summary>Témoin lumineux : point de couleur et texte court.</summary>
        public static Border Temoin(string texte, Color c, bool allume)
        {
            var point = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = P(allume ? c : Faible), VerticalAlignment = VerticalAlignment.Center };
            if (allume) point.BoxShadow = BoxShadows.Parse("0 0 8 0 " + c.ToString());
            return new Border
            {
                Padding = new Thickness(10, 5), CornerRadius = new CornerRadius(14), Margin = new Thickness(0, 0, 8, 6), Background = P(allume ? c : Faible, allume ? 26 : 18), BorderBrush = P(allume ? c : Faible, allume ? 120 : 80), BorderThickness = new Thickness(1),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { point, new TextBlock { Text = texte, FontFamily = Chiffres, FontSize = 11, Foreground = P(allume ? Texte : Discret), VerticalAlignment = VerticalAlignment.Center } } },
            };
        }

        /// <summary>Bouton du Studio (pilule) : plein (ambre → magenta) ou contour.</summary>
        public static Border Bouton(string texte, bool plein, Action clic, string bulle = null)
        {
            var t = new TextBlock { Text = texte, FontFamily = Titre, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = P(plein ? Color.FromRgb(24, 8, 22) : Texte), HorizontalAlignment = HorizontalAlignment.Center };
            var b = new Border
            {
                Child = t, Padding = new Thickness(16, 8), CornerRadius = new CornerRadius(18), Margin = new Thickness(0, 0, 8, 8), Cursor = new Cursor(StandardCursorType.Hand), Focusable = true,
                Background = plein ? (IBrush)Degrade(0, Ambre, Magenta) : P(Violet, 22), BorderBrush = P(plein ? Ambre : Violet, plein ? 0 : 140), BorderThickness = new Thickness(1),
            };
            if (bulle != null) ToolTip.SetTip(b, bulle);
            b.PointerEntered += (s, e) => b.Opacity = 0.86;
            b.PointerExited += (s, e) => b.Opacity = 1;
            b.PointerReleased += (s, e) => { if (b.IsEffectivelyEnabled) clic(); };
            b.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; clic(); } };
            return b;
        }

        /// <summary>Contour de focus clavier, commun aux contrôles du Studio.</summary>
        public static void Focus(DrawingContext ctx, Rect r, double rayon) { ctx.DrawRectangle(null, new Pen(P(Ambre, 200), 1, new DashStyle(new double[] { 2, 2 }, 0)), new RoundedRect(r, rayon)); }

        public static FormattedText Mesure(string t, double taille, Color c, FontFamily police = null, bool gras = false)
        {
            return new FormattedText(t, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(police ?? Chiffres, FontStyle.Normal, gras ? FontWeight.SemiBold : FontWeight.Normal), taille, P(c));
        }

        public static string Db(double v, int dec = 1) { return (v > 0.0001 ? "+" : v < -0.0001 ? "−" : "") + Math.Abs(v).ToString(dec == 0 ? "0" : "0.0", Langue.Culture) + " dB"; }
    }

    // ================================================================ molette rotative

    /// <summary>Molette : glisser vers le haut ou le bas, molette de la souris, flèches du clavier ; double clic : valeur par défaut.</summary>
    internal sealed class Molette5 : Control
    {
        public readonly double Min, Max, Pas, Defaut;
        public bool Bipolaire;   // l'arc part de zéro (balance, niveau)
        public event Action<double> Change;
        private double valeur, departY, departV;
        private bool glisse;

        public Molette5(double min, double max, double pas, double valeur, double defaut)
        {
            Min = min; Max = max; Pas = pas; Defaut = defaut; this.valeur = Ajuster(valeur);
            Width = 78; Height = 78; Focusable = true; Cursor = new Cursor(StandardCursorType.SizeNorthSouth);
        }

        public double Valeur { get { return valeur; } set { double v = Ajuster(value); if (v != valeur) { valeur = v; InvalidateVisual(); } } }

        private double Ajuster(double v) { if (double.IsNaN(v)) v = Min; v = Min + Math.Round((v - Min) / Pas, MidpointRounding.AwayFromZero) * Pas; return Math.Max(Min, Math.Min(Max, v)); }
        private void Choisir(double v) { v = Ajuster(v); if (v == valeur) return; valeur = v; InvalidateVisual(); var c = Change; if (c != null) c(v); }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.ClickCount == 2) { Choisir(Defaut); e.Handled = true; return; }
            glisse = true; departY = e.GetPosition(this).Y; departV = valeur; e.Pointer.Capture(this); Focus(); e.Handled = true; InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (glisse) Choisir(departV + (departY - e.GetPosition(this).Y) / 160.0 * (Max - Min));
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); if (glisse) { glisse = false; e.Pointer.Capture(null); InvalidateVisual(); } }
        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); glisse = false; InvalidateVisual(); }
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { base.OnPointerWheelChanged(e); Choisir(valeur + Math.Sign(e.Delta.Y) * Pas); e.Handled = true; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            double v = valeur;
            switch (e.Key)
            {
                case Key.Left: case Key.Down: v -= Pas; break;
                case Key.Right: case Key.Up: v += Pas; break;
                case Key.PageDown: v -= 5 * Pas; break;
                case Key.PageUp: v += 5 * Pas; break;
                case Key.Home: v = Min; break;
                case Key.End: v = Max; break;
                case Key.Delete: case Key.Back: v = Defaut; break;
                default: return;
            }
            e.Handled = true; Choisir(v);
        }

        protected override void OnGotFocus(GotFocusEventArgs e) { base.OnGotFocus(e); InvalidateVisual(); }
        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }

        private double Angle(double v) { return 135 + 270 * (v - Min) / (Max - Min); }

        public override void Render(DrawingContext ctx)
        {
            double t = Math.Min(Bounds.Width, Bounds.Height), r = t / 2 - 6;
            var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
            ctx.DrawEllipse(Brushes.Transparent, null, c, t / 2, t / 2);
            // piste, puis l'arc de la valeur (ambre → magenta)
            ctx.DrawGeometry(null, new Pen(Studio.P(Studio.Bord), 5, lineCap: PenLineCap.Round), Habillage.Arc(c, r, 135, 270));
            double depart = Bipolaire && Min < 0 && Max > 0 ? Angle(0) : 135, fin = Angle(valeur);
            double a0 = Math.Min(depart, fin), balayage = Math.Abs(fin - depart);
            if (balayage > 0.5)
            {
                var degrade = new LinearGradientBrush { StartPoint = new RelativePoint(0, 1, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = { new GradientStop(Studio.Ambre, 0), new GradientStop(Studio.Magenta, 1) } };
                ctx.DrawGeometry(null, new Pen(Studio.P(Studio.Magenta, glisse ? 90 : 50), 11, lineCap: PenLineCap.Round), Habillage.Arc(c, r, a0, balayage));
                ctx.DrawGeometry(null, new Pen(degrade, 5, lineCap: PenLineCap.Round), Habillage.Arc(c, r, a0, balayage));
            }
            // bouton central et son repère
            double ri = r - 10;
            ctx.DrawEllipse(new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromRgb(58, 42, 92), 0), new GradientStop(Color.FromRgb(24, 15, 40), 1) }, Center = new RelativePoint(0.4, 0.3, RelativeUnit.Relative), GradientOrigin = new RelativePoint(0.4, 0.3, RelativeUnit.Relative) },
                new Pen(Studio.P(Studio.Bord), 1), c, ri, ri);
            double a = fin * Math.PI / 180;
            ctx.DrawLine(new Pen(Studio.P(Studio.Texte), 2.4, lineCap: PenLineCap.Round), new Point(c.X + Math.Cos(a) * ri * 0.35, c.Y + Math.Sin(a) * ri * 0.35), new Point(c.X + Math.Cos(a) * (ri - 4), c.Y + Math.Sin(a) * (ri - 4)));
            if (IsFocused) Studio.Focus(ctx, new Rect(Bounds.Size).Deflate(0.5), t / 2);
        }
    }

    // ================================================================ fader vertical

    internal sealed class Fader5 : Control
    {
        public readonly double Min, Max, Pas;
        public event Action<double> Change;
        private double valeur;
        private bool glisse;

        public Fader5(double min, double max, double pas, double valeur)
        {
            Min = min; Max = max; Pas = pas; this.valeur = Ajuster(valeur);
            Width = 46; Height = 170; Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
        }

        public double Valeur { get { return valeur; } set { double v = Ajuster(value); if (v != valeur) { valeur = v; InvalidateVisual(); } } }
        private double Ajuster(double v) { if (double.IsNaN(v)) v = 0; v = Min + Math.Round((v - Min) / Pas, MidpointRounding.AwayFromZero) * Pas; return Math.Max(Min, Math.Min(Max, v)); }
        private void Choisir(double v) { v = Ajuster(v); if (v == valeur) return; valeur = v; InvalidateVisual(); var c = Change; if (c != null) c(v); }
        private const double Marge = 10;
        private double Y(double v) { return Marge + (Bounds.Height - 2 * Marge) * (Max - v) / (Max - Min); }
        private void Viser(PointerEventArgs e) { Choisir(Max - (e.GetPosition(this).Y - Marge) / Math.Max(1, Bounds.Height - 2 * Marge) * (Max - Min)); }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (e.ClickCount == 2) { Choisir(0); e.Handled = true; return; }
            glisse = true; e.Pointer.Capture(this); Focus(); Viser(e); e.Handled = true;
        }
        protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (glisse) Viser(e); }
        protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); if (glisse) { glisse = false; e.Pointer.Capture(null); InvalidateVisual(); } }
        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); glisse = false; }
        protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { base.OnPointerWheelChanged(e); Choisir(valeur + Math.Sign(e.Delta.Y) * Pas); e.Handled = true; }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            double v = valeur;
            switch (e.Key)
            {
                case Key.Down: case Key.Left: v -= Pas; break;
                case Key.Up: case Key.Right: v += Pas; break;
                case Key.PageDown: v -= 5 * Pas; break;
                case Key.PageUp: v += 5 * Pas; break;
                case Key.Home: v = Min; break;
                case Key.End: v = Max; break;
                case Key.Delete: case Key.Back: v = 0; break;
                default: return;
            }
            e.Handled = true; Choisir(v);
        }

        protected override void OnGotFocus(GotFocusEventArgs e) { base.OnGotFocus(e); InvalidateVisual(); }
        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }

        public override void Render(DrawingContext ctx)
        {
            double x = Bounds.Width / 2;
            ctx.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
            // graduations tous les 6 dB
            for (double d = Min; d <= Max + 0.01; d += 6)
            {
                double y = Y(d); bool zero = Math.Abs(d) < 0.01;
                ctx.DrawLine(new Pen(Studio.P(zero ? Studio.Discret : Studio.Bord), zero ? 1.4 : 1), new Point(x - (zero ? 16 : 11), y), new Point(x - 7, y));
                ctx.DrawLine(new Pen(Studio.P(zero ? Studio.Discret : Studio.Bord), zero ? 1.4 : 1), new Point(x + 7, y), new Point(x + (zero ? 16 : 11), y));
            }
            ctx.DrawRectangle(Studio.P(Color.FromRgb(8, 5, 16)), new Pen(Studio.P(Studio.Bord), 1), new RoundedRect(new Rect(x - 3, Marge, 6, Bounds.Height - 2 * Marge), 3));
            double y0 = Y(0), yv = Y(valeur);
            if (Math.Abs(yv - y0) > 0.5)
                ctx.DrawRectangle(Studio.Degrade(1, valeur > 0 ? Studio.Ambre : Studio.Violet, valeur > 0 ? Studio.Magenta : Studio.Cyan), null, new RoundedRect(new Rect(x - 3, Math.Min(y0, yv), 6, Math.Abs(yv - y0)), 3));
            // poignée
            var p = new Rect(x - 15, yv - 7, 30, 14);
            ctx.DrawRectangle(null, new Pen(Studio.P(Studio.Magenta, glisse ? 110 : 45), 6), new RoundedRect(p, 5));
            ctx.DrawRectangle(Studio.Degrade(1, Color.FromRgb(250, 240, 255), Color.FromRgb(180, 160, 220)), new Pen(Studio.P(Studio.Fond1), 1), new RoundedRect(p, 5));
            ctx.DrawLine(new Pen(Studio.P(Studio.Magenta), 2), new Point(x - 9, yv), new Point(x + 9, yv));
            if (IsFocused) Studio.Focus(ctx, new Rect(Bounds.Size).Deflate(0.5), 6);
        }
    }

    // ================================================================ bouton marche

    internal sealed class Marche5 : Control
    {
        private bool actif;
        public event Action<bool> Demande;
        public Marche5(bool actif) { this.actif = actif; Width = 72; Height = 72; Focusable = true; Cursor = new Cursor(StandardCursorType.Hand); }
        public bool Actif { get { return actif; } set { if (value != actif) { actif = value; InvalidateVisual(); } } }
        private void Demander() { if (!IsEffectivelyEnabled) return; var d = Demande; if (d != null) d(!actif); }
        protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); e.Handled = true; }
        protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) { e.Handled = true; Focus(); Demander(); } }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; Demander(); } }
        protected override void OnGotFocus(GotFocusEventArgs e) { base.OnGotFocus(e); InvalidateVisual(); }
        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }

        public override void Render(DrawingContext ctx)
        {
            var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
            double r = Math.Min(Bounds.Width, Bounds.Height) / 2 - 4;
            if (actif)
            {
                ctx.DrawEllipse(null, new Pen(Studio.P(Studio.Magenta, 50), 9), c, r, r);
                ctx.DrawEllipse(new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromRgb(70, 24, 66), 0), new GradientStop(Color.FromRgb(28, 10, 34), 1) } }, new Pen(Studio.Degrade(1, Studio.Ambre, Studio.Magenta), 3), c, r, r);
            }
            else ctx.DrawEllipse(Studio.P(Color.FromRgb(20, 13, 34)), new Pen(Studio.P(Studio.Bord), 2), c, r, r);
            // pictogramme « marche » : arc ouvert en haut et trait vertical
            var couleur = actif ? Studio.Ambre : Studio.Faible;
            var crayon = new Pen(Studio.P(couleur), 3.2, lineCap: PenLineCap.Round);
            ctx.DrawGeometry(null, crayon, Habillage.Arc(c, r * 0.42, -60, 300));
            ctx.DrawLine(crayon, new Point(c.X, c.Y - r * 0.58), new Point(c.X, c.Y - r * 0.08));
            if (IsFocused) Studio.Focus(ctx, new Rect(Bounds.Size).Deflate(0.5), Bounds.Width / 2);
        }
    }

    // ================================================================ VU-mètre stéréo (diodes)

    internal sealed class VuMetre5 : Control
    {
        private double g = -90, d = -90, crG = -90, crD = -90;
        private long tG, tD;
        public VuMetre5() { Height = 30; MinWidth = 120; }

        private static double Db(float lin) { return lin > 0.00001f ? 20 * Math.Log10(lin) : -90; }

        /// <summary>Nouvelles crêtes (0 à 1) ; la crête maximale reste affichée une seconde.</summary>
        public void Definir(float gauche, float droite)
        {
            long t = Horloge.Maintenant;
            double ng = Db(gauche), nd = Db(droite);
            g = Math.Max(ng, g - 2.5); d = Math.Max(nd, d - 2.5);   // retombée douce
            if (ng >= crG || t - tG > 1000) { crG = ng; tG = t; }
            if (nd >= crD || t - tD > 1000) { crD = nd; tD = t; }
            InvalidateVisual();
        }

        public override void Render(DrawingContext ctx)
        {
            const int N = 30; const double bas = -48, haut = 3;
            double w = Bounds.Width, h = (Bounds.Height - 4) / 2, pas = w / N;
            for (int ligne = 0; ligne < 2; ligne++)
            {
                double v = ligne == 0 ? g : d, cr = ligne == 0 ? crG : crD, y = ligne * (h + 4);
                for (int i = 0; i < N; i++)
                {
                    double seuil = bas + (haut - bas) * (i + 0.5) / N;
                    Color c = seuil >= -1 ? Studio.Rouge : seuil >= -9 ? Studio.Ambre : Studio.Cyan;
                    bool allume = v >= seuil, pic = Math.Abs(cr - seuil) <= (haut - bas) / N / 2 && cr > bas;
                    ctx.DrawRectangle(Studio.P(c, allume || pic ? 255 : 34), null, new RoundedRect(new Rect(i * pas + 1, y, pas - 2, h), 1.5));
                }
            }
        }
    }

    // ================================================================ courbe de l'égaliseur et spectre en direct

    /// <summary>
    /// La réponse de l'égaliseur (somme des filtres, de 20 Hz à 20 kHz), le spectre du son joué derrière, et une poignée
    /// par bande : la glisser vers le haut ou le bas change son gain. Au clavier : flèches gauche / droite pour choisir la
    /// bande, haut / bas pour la régler. En version compacte (cartes des profils) : la courbe seule.
    /// </summary>
    internal sealed class CourbeEq5 : Control
    {
        public ReglagesAudioJeu Reglages;
        public bool Compact, Allume = true;
        /// <summary>Spectre en dB par bande d'AnalyseurSpectre (null : pas de spectre).</summary>
        public float[] Spectre;
        /// <summary>Une poignée a été déplacée : bande et nouveau gain effectif (la page le ramène au gain du réglage).</summary>
        public event Action<int, double> GainChange;
        private int choisie = -1, survol = -1;
        private bool glisse;
        private string cle;
        private Point[] points;
        private const double PlageDb = 15;

        public CourbeEq5(bool compact = false)
        {
            Compact = compact;
            Height = compact ? 46 : 260; MinWidth = 100;
            if (!compact) { Focusable = true; Cursor = new Cursor(StandardCursorType.Hand); }
            IsHitTestVisible = !compact;
        }

        private Rect Zone { get { return Compact ? new Rect(Bounds.Size) : new Rect(40, 12, Math.Max(10, Bounds.Width - 52), Math.Max(10, Bounds.Height - 36)); } }
        private double X(double f, Rect z) { return z.X + z.Width * Math.Log10(f / 20) / 3; }
        private double Y(double db, Rect z) { double p = Compact ? 12 : PlageDb; return z.Y + z.Height * (p - Math.Max(-p, Math.Min(p, db))) / (2 * p); }

        public void Actualiser() { cle = null; InvalidateVisual(); }

        private void Calculer(Rect z)
        {
            var r = Reglages ?? new ReglagesAudioJeu();
            string k = string.Join(",", r.Gains) + "|" + r.Intensite + "|" + r.CoupeBas + "|" + z.Width + "|" + z.Height;
            if (k == cle && points != null) return;
            cle = k;
            var filtres = AudioJeu.Filtres(r);
            int n = Compact ? 60 : 180;
            points = new Point[n];
            for (int i = 0; i < n; i++)
            {
                double f = 20 * Math.Pow(1000, i / (double)(n - 1)), s = 0;
                foreach (var x in filtres) s += AudioJeu.Reponse(x.Type, x.Fc, x.Gain, x.Q, f);
                points[i] = new Point(X(f, z), Y(s, z));
            }
        }

        private Point Poignee(int k, Rect z) { var r = Reglages ?? new ReglagesAudioJeu(); return new Point(X(AudioJeu.Bandes[k].Fc, z), Y(AudioJeu.GainEffectif(r, k), z)); }

        private int Toucher(Point p)
        {
            var z = Zone;
            for (int k = 0; k < AudioJeu.Bandes.Length; k++) { var q = Poignee(k, z); if (Math.Abs(q.X - p.X) <= 16 && Math.Abs(q.Y - p.Y) <= 16) return k; }
            return -1;
        }

        private void Deplacer(int k, double db)
        {
            db = Math.Max(-12, Math.Min(12, Math.Round(db * 2, MidpointRounding.AwayFromZero) / 2));
            var h = GainChange; if (h != null) h(k, db);
            Actualiser();
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            int k = Toucher(e.GetPosition(this));
            if (k < 0) return;
            choisie = k; glisse = true; e.Pointer.Capture(this); Focus(); e.Handled = true; InvalidateVisual();
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var p = e.GetPosition(this);
            if (glisse && choisie >= 0)
            {
                var z = Zone; double plage = PlageDb;
                Deplacer(choisie, plage - (p.Y - z.Y) / z.Height * 2 * plage);
                return;
            }
            int s = Toucher(p);
            if (s != survol) { survol = s; InvalidateVisual(); }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); if (glisse) { glisse = false; e.Pointer.Capture(null); InvalidateVisual(); } }
        protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); if (survol >= 0) { survol = -1; InvalidateVisual(); } }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (Compact) return;
            var r = Reglages ?? new ReglagesAudioJeu();
            if (choisie < 0) choisie = 3;
            switch (e.Key)
            {
                case Key.Left: choisie = Math.Max(0, choisie - 1); InvalidateVisual(); break;
                case Key.Right: choisie = Math.Min(AudioJeu.Bandes.Length - 1, choisie + 1); InvalidateVisual(); break;
                case Key.Up: Deplacer(choisie, AudioJeu.GainEffectif(r, choisie) + 0.5); break;
                case Key.Down: Deplacer(choisie, AudioJeu.GainEffectif(r, choisie) - 0.5); break;
                default: return;
            }
            e.Handled = true;
        }

        protected override void OnGotFocus(GotFocusEventArgs e) { base.OnGotFocus(e); if (choisie < 0) choisie = 3; InvalidateVisual(); }
        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e) { base.OnLostFocus(e); if (!glisse) choisie = -1; InvalidateVisual(); }

        public override void Render(DrawingContext ctx)
        {
            var z = Zone;
            if (z.Width < 20) return;
            Calculer(z);
            if (!Compact)
            {
                // fond et grille : décades de fréquence, lignes de gain tous les 6 dB
                ctx.DrawRectangle(Studio.P(Color.FromRgb(8, 4, 16), 230), new Pen(Studio.P(Studio.Bord), 1), new RoundedRect(new Rect(Bounds.Size), 14));
                foreach (double f in new double[] { 20, 50, 100, 200, 500, 1000, 2000, 5000, 10000, 20000 })
                {
                    double x = X(f, z);
                    ctx.DrawLine(new Pen(Studio.P(Studio.Bord, f == 100 || f == 1000 || f == 10000 ? 200 : 110), 1), new Point(x, z.Top), new Point(x, z.Bottom));
                    var t = Studio.Mesure(f >= 1000 ? (f / 1000) + "k" : f.ToString(CultureInfo.InvariantCulture), 10, Studio.Faible);
                    ctx.DrawText(t, new Point(Math.Max(z.Left, Math.Min(z.Right - t.Width, x - t.Width / 2)), z.Bottom + 6));
                }
                for (double db = -12; db <= 12; db += 6)
                {
                    double y = Y(db, z);
                    ctx.DrawLine(new Pen(Studio.P(db == 0 ? Studio.Discret : Studio.Bord, db == 0 ? 150 : 110), 1, db == 0 ? null : new DashStyle(new double[] { 3, 4 }, 0)), new Point(z.Left, y), new Point(z.Right, y));
                    var t = Studio.Mesure((db > 0 ? "+" : "") + db, 10, Studio.Faible);
                    ctx.DrawText(t, new Point(z.Left - t.Width - 8, y - t.Height / 2));
                }
                // spectre en direct, derrière (−84 dB en bas, 0 dB en haut)
                var sp = Spectre;
                if (sp != null && sp.Length == AnalyseurSpectre.NbBandes)
                {
                    double larg = z.Width / sp.Length;
                    for (int i = 0; i < sp.Length; i++)
                    {
                        double niv = Math.Max(0, Math.Min(1, (sp[i] + 84) / 84.0));
                        if (niv <= 0.005) continue;
                        double hb = z.Height * niv, x = z.X + i * larg;
                        ctx.DrawRectangle(Studio.Degrade(1, Habillage.Alpha(Studio.Cyan, 150), Habillage.Alpha(Studio.Violet, 30)), null, new RoundedRect(new Rect(x + 1, z.Bottom - hb, Math.Max(1, larg - 2), hb), 2, 2, 0, 0));
                    }
                }
            }
            // la courbe : remplissage depuis 0 dB, halo, trait
            double y0 = Y(0, z);
            var aire = new StreamGeometry();
            using (var g = aire.Open())
            {
                g.BeginFigure(new Point(points[0].X, y0), true);
                foreach (var p in points) g.LineTo(p);
                g.LineTo(new Point(points[points.Length - 1].X, y0));
                g.EndFigure(true);
            }
            var ligne = new StreamGeometry();
            using (var g = ligne.Open())
            {
                g.BeginFigure(points[0], false);
                for (int i = 1; i < points.Length; i++) g.LineTo(points[i]);
                g.EndFigure(false);
            }
            int alpha = Allume ? 255 : 110;
            ctx.DrawGeometry(Studio.Degrade(1, Habillage.Alpha(Studio.Magenta, Allume ? 70 : 25), Habillage.Alpha(Studio.Violet, 8)), null, aire);
            var trait = Studio.Degrade(0, Habillage.Alpha(Studio.Ambre, alpha), Habillage.Alpha(Studio.Magenta, alpha), Habillage.Alpha(Studio.Violet, alpha));
            if (Allume && !Compact) ctx.DrawGeometry(null, new Pen(Studio.Degrade(0, Habillage.Alpha(Studio.Ambre, 60), Habillage.Alpha(Studio.Magenta, 60)), 8, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), ligne);
            ctx.DrawGeometry(null, new Pen(trait, Compact ? 2 : 2.6, Allume ? null : new DashStyle(new double[] { 4, 3 }, 0), PenLineCap.Round, PenLineJoin.Round), ligne);
            if (Compact) return;
            // poignées des bandes
            var r = Reglages ?? new ReglagesAudioJeu();
            for (int k = 0; k < AudioJeu.Bandes.Length; k++)
            {
                var q = Poignee(k, z);
                bool vise = k == choisie || k == survol;
                ctx.DrawLine(new Pen(Studio.P(Studio.Ambre, vise ? 120 : 40), 1, new DashStyle(new double[] { 2, 3 }, 0)), new Point(q.X, y0), q);
                ctx.DrawEllipse(Studio.P(Studio.Fond1), new Pen(Studio.P(vise ? Studio.Ambre : Studio.Magenta), vise ? 3 : 2), q, vise ? 8 : 6.5, vise ? 8 : 6.5);
                if (vise)
                {
                    var b = AudioJeu.Bandes[k];
                    var t = Studio.Mesure(Langue.T(b.Nom) + "  " + (b.Fc >= 1000 ? (b.Fc / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + " kHz" : b.Fc + " Hz") + "  " + Studio.Db(AudioJeu.GainEffectif(r, k)), 11.5, Studio.Texte, null, true);
                    double tx = Math.Max(z.Left + 4, Math.Min(z.Right - t.Width - 12, q.X - t.Width / 2 - 6)), ty = q.Y > z.Y + 40 ? q.Y - 36 : q.Y + 14;
                    ctx.DrawRectangle(Studio.P(Studio.Fond1, 230), new Pen(Studio.P(Studio.Ambre, 160), 1), new RoundedRect(new Rect(tx, ty, t.Width + 12, t.Height + 8), 8));
                    ctx.DrawText(t, new Point(tx + 6, ty + 4));
                }
            }
            if (IsFocused) Studio.Focus(ctx, new Rect(Bounds.Size).Deflate(1), 14);
        }
    }
}
