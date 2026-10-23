// Nettoyage PC 5 - élément du menu de gauche : numéro, nom en capitales, pastille (PRO, mise à jour).
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    public sealed class ElementMenu : Control
    {
        public readonly string Cle, Nom;
        public readonly int Numero;     // 0 : sans numéro (groupe du bas)
        public string Pastille = "";
        public event Action<ElementMenu> Clic;
        private bool actif;
        private double survol, vActif;

        public ElementMenu(string cle, string nom, int numero, double hauteur)
        {
            Cle = cle; Nom = nom; Numero = numero; Height = hauteur;
            Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
        }

        public bool Actif
        {
            get { return actif; }
            set
            {
                if (actif == value) return;
                actif = value;
                Glisser(v => vActif = v, vActif, value ? 1 : 0, 200);
            }
        }

        private void Glisser(Action<double> fixer, double depart, double cible, int duree)
        {
            if (!Habillage.Animations) { fixer(cible); InvalidateVisual(); return; }
            long t0 = Horloge.Maintenant;
            Horloge.Lancer(t => { double p = Math.Min(1, (t - t0) / (double)duree); fixer(depart + (cible - depart) * Habillage.Douce(p)); InvalidateVisual(); return p < 1; });
        }

        protected override void OnPointerEntered(PointerEventArgs e) { base.OnPointerEntered(e); Glisser(v => survol = v, survol, 1, 120); }
        protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); Glisser(v => survol = v, survol, 0, 160); }
        protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); if (new Rect(Bounds.Size).Contains(e.GetPosition(this))) { var c = Clic; if (c != null) c(this); } }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; var c = Clic; if (c != null) c(this); } }

        public override void Render(DrawingContext ctx)
        {
            var A = Color.FromRgb(61, 245, 255);       // la barre reste sombre : accent lumineux dans les deux thèmes
            Color discret = Color.FromRgb(120, 142, 162), texte = Habillage.Melanger(Color.FromRgb(200, 216, 230), Colors.White, Math.Max(vActif, survol));
            var r = new Rect(12, 3, Bounds.Width - 24, Bounds.Height - 6);
            if (survol > 0.01 && vActif < 0.99) ctx.DrawRectangle(Habillage.Pinceau(Color.FromRgb(16, 28, 44), (int)(200 * survol * (1 - vActif))), null, new RoundedRect(r, 2));
            if (vActif > 0.01)
            {
                double dx = (1 - vActif) * 10;
                var ra = new Rect(r.X - dx, r.Y, r.Width, r.Height);
                ctx.DrawRectangle(Habillage.Pinceau(A, (int)(36 * vActif)), new Pen(Habillage.Pinceau(A, (int)(120 * vActif)), 1), new RoundedRect(ra.Deflate(0.5), 2));
            }
            double x = 26;
            if (Numero > 0)
            {
                var num = new FormattedText(Numero.ToString("00"), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Console), 11, Habillage.Pinceau(Habillage.Melanger(discret, A, vActif)));
                ctx.DrawText(num, new Point(24, (Bounds.Height - num.Height) / 2));
                x = 54;
            }
            var nom = new FormattedText(Nom.ToUpper(CultureInfo.GetCultureInfo("fr-FR")), CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(Habillage.Affichage, FontStyle.Normal, actif ? FontWeight.SemiBold : FontWeight.Normal), Numero > 0 ? 14 : 12, Habillage.Pinceau(Numero > 0 ? texte : Habillage.Melanger(discret, Colors.White, Math.Max(vActif, survol * 0.6))));
            ctx.DrawText(nom, new Point(x, (Bounds.Height - nom.Height) / 2));
            if (Pastille != "")
            {
                var p = new FormattedText(Pastille, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Corps, FontStyle.Normal, FontWeight.Bold), 11, Habillage.Pinceau(Color.FromRgb(4, 18, 26)));
                double lp = Math.Max(26, p.Width + 12);
                var rp = new Rect(Bounds.Width - 22 - lp, (Bounds.Height - 20) / 2, lp, 20);
                ctx.DrawRectangle(Habillage.Pinceau(A), null, new RoundedRect(rp, 2));
                ctx.DrawText(p, new Point(rp.X + (lp - p.Width) / 2, rp.Y + (20 - p.Height) / 2));
            }
            if (IsFocused) ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(A, 150), 1, new DashStyle(new double[] { 2, 2 }, 0)), new RoundedRect(r.Deflate(2), 2));
        }
    }
}
