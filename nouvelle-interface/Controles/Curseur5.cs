// Velox 5 - curseur et interrupteur dessinés dans le style de l'interface (HUD : angles droits et halo ; classique :
// formes arrondies). Souris : clic ou glisser. Clavier : flèches (un pas), Page précédente / suivante (cinq pas),
// Début et Fin ; Entrée ou Espace pour l'interrupteur.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    /// <summary>Curseur horizontal : une valeur entre Min et Max, par pas réguliers.</summary>
    public sealed class Curseur5 : Control
    {
        public readonly double Min, Max, Pas;
        /// <summary>Le remplissage part de zéro (réglage qui va dans les deux sens : gain, balance) au lieu de partir de la gauche.</summary>
        public bool DepuisZero;
        /// <summary>La valeur vient d'être changée à la souris ou au clavier.</summary>
        public event Action<double> Change;

        private double valeur;
        private bool glisse;
        private const double Marge = 9;   // demi-largeur de la poignée

        public Curseur5(double min, double max, double pas, double valeur)
        {
            Min = min; Max = max; Pas = pas;
            this.valeur = Ajuster(valeur);
            Height = 28; MinWidth = 120;
            Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
        }

        private double Ajuster(double v)
        {
            if (double.IsNaN(v)) v = Min;
            v = Min + Math.Round((v - Min) / Pas, MidpointRounding.AwayFromZero) * Pas;
            return Math.Max(Min, Math.Min(Max, v));
        }

        /// <summary>Valeur affichée (la changer ici ne déclenche pas « Change »).</summary>
        public double Valeur
        {
            get { return valeur; }
            set { double v = Ajuster(value); if (v == valeur) return; valeur = v; InvalidateVisual(); }
        }

        private void Choisir(double v)
        {
            v = Ajuster(v);
            if (v == valeur) return;
            valeur = v; InvalidateVisual();
            var c = Change; if (c != null) c(v);
        }

        private double Position(double v) { double l = Math.Max(1, Bounds.Width - 2 * Marge); return Marge + l * (v - Min) / (Max - Min); }

        private void Viser(PointerEventArgs e)
        {
            double l = Math.Max(1, Bounds.Width - 2 * Marge);
            Choisir(Min + (e.GetPosition(this).X - Marge) / l * (Max - Min));
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (!IsEffectivelyEnabled) return;
            glisse = true; e.Pointer.Capture(this); Focus(); Viser(e); e.Handled = true;
        }

        protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); if (glisse) Viser(e); }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (!glisse) return;
            glisse = false; e.Pointer.Capture(null); InvalidateVisual();
        }

        protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { base.OnPointerCaptureLost(e); glisse = false; InvalidateVisual(); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!IsEffectivelyEnabled) return;
            double v = valeur;
            switch (e.Key)
            {
                case Key.Left: case Key.Down: v -= Pas; break;
                case Key.Right: case Key.Up: v += Pas; break;
                case Key.PageDown: v -= 5 * Pas; break;
                case Key.PageUp: v += 5 * Pas; break;
                case Key.Home: v = Min; break;
                case Key.End: v = Max; break;
                default: return;
            }
            e.Handled = true;
            Choisir(v);
        }

        protected override void OnGotFocus(GotFocusEventArgs e) { base.OnGotFocus(e); InvalidateVisual(); }
        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            bool hud = Habillage.Hud, actif = IsEffectivelyEnabled;
            double y = Math.Round(Bounds.Height / 2), x = Position(valeur);
            Color couleur = actif ? P.Accent : P.Discret;
            // toute la surface réagit au clic, pas seulement le trait
            ctx.DrawRectangle(Brushes.Transparent, null, new Rect(Bounds.Size));
            ctx.DrawRectangle(Habillage.Pinceau(P.Bordure), null, new RoundedRect(new Rect(Marge, y - 2, Math.Max(0, Bounds.Width - 2 * Marge), 4), hud ? 0 : 2));
            double origine = DepuisZero && Min < 0 && Max > 0 ? Position(0) : Marge;
            double gauche = Math.Min(origine, x), largeur = Math.Abs(x - origine);
            if (largeur > 0) ctx.DrawRectangle(Habillage.Pinceau(couleur, actif ? 255 : 120), null, new RoundedRect(new Rect(gauche, y - 2, largeur, 4), hud ? 0 : 2));
            if (DepuisZero && Min < 0 && Max > 0) ctx.DrawRectangle(Habillage.Pinceau(P.Discret, 160), null, new Rect(origine - 0.5, y - 6, 1, 12));
            if (hud)
            {
                var poignee = new Rect(x - 5, y - 10, 10, 20);
                if (actif && P.Sombre) ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(couleur, glisse ? 110 : 60), 5), poignee);
                ctx.DrawRectangle(Habillage.Pinceau(actif ? couleur : P.Bordure), new Pen(Habillage.Pinceau(P.Fond), 1), poignee);
            }
            else
            {
                ctx.DrawEllipse(Habillage.Pinceau(actif ? couleur : P.Bordure), new Pen(Habillage.Pinceau(P.Carte), 2), new Point(x, y), glisse ? 9 : 8, glisse ? 9 : 8);
            }
            if (IsFocused) ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(P.Accent, 160), 1, new DashStyle(new double[] { 2, 2 }, 0)), new Rect(0.5, 0.5, Math.Max(0, Bounds.Width - 1), Math.Max(0, Bounds.Height - 1)));
        }
    }

    /// <summary>Interrupteur marche / arrêt. Un clic ne change pas son état : il demande (« Demande »), et la page
    /// décide (question avant d'écrire, mode simulation…) puis règle « Actif ».</summary>
    public sealed class Interrupteur5 : Control
    {
        private bool actif;
        public event Action<bool> Demande;

        public Interrupteur5(bool actif, double largeur = 52, double hauteur = 26)
        {
            this.actif = actif; Width = largeur; Height = hauteur;
            Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
        }

        public bool Actif { get { return actif; } set { if (value == actif) return; actif = value; InvalidateVisual(); } }

        private void Demander() { if (!IsEffectivelyEnabled) return; var d = Demande; if (d != null) d(!actif); }

        protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); e.Handled = true; }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) { e.Handled = true; Focus(); Demander(); }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; Demander(); }
        }

        protected override void OnGotFocus(GotFocusEventArgs e) { base.OnGotFocus(e); InvalidateVisual(); }
        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            bool hud = Habillage.Hud, dispo = IsEffectivelyEnabled;
            var r = new Rect(Bounds.Size).Deflate(1);
            double h = r.Height, rayon = hud ? 0 : h / 2;
            Color couleur = !dispo ? P.Discret : actif ? P.Vert : P.Discret;
            if (hud && actif && dispo && P.Sombre) ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(couleur, 50), 5), new RoundedRect(r, rayon));
            ctx.DrawRectangle(Habillage.Pinceau(actif && dispo ? Habillage.Alpha(couleur, P.Sombre ? 46 : 36) : P.Carte), new Pen(Habillage.Pinceau(couleur, actif ? 230 : 150), 1.2), new RoundedRect(r, rayon));
            double marge = 4, cote = h - 2 * marge, x = actif ? r.Right - marge - cote : r.Left + marge;
            ctx.DrawRectangle(Habillage.Pinceau(couleur, actif ? 255 : 200), null, new RoundedRect(new Rect(x, r.Top + marge, cote, cote), hud ? 0 : cote / 2));
            if (IsFocused) ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(P.Accent, 160), 1, new DashStyle(new double[] { 2, 2 }, 0)), new RoundedRect(r.Inflate(1).Deflate(0.5), rayon));
        }
    }
}
