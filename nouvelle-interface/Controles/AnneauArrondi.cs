// Velox 5 - anneau du score, style arrondi : une piste sombre et un arc épais au dégradé violet-rose (la part du score),
// le score au centre et son état dessous. Pendant l'analyse, un arc court tourne.
using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    public sealed class AnneauArrondi : Control
    {
        private double affiche, cible = -1, rotation;
        private long departAnim; private double departValeur;
        private bool tourne = true;
        private string etat = "";
        private Color couleurEtat;

        public AnneauArrondi(double taille)
        {
            Width = taille; Height = taille;
            couleurEtat = Habillage.P.Discret;
            AttachedToVisualTree += (s, e) => { if (tourne) Tourner(); };
        }

        /// <summary>Score (0 à 100) et son état, en douceur depuis la valeur affichée.</summary>
        public void Montrer(Bilan b)
        {
            if (b == null) return;
            tourne = false;
            cible = Math.Max(0, Math.Min(100, b.Score));
            Color c; Textes.EtatCourt(b, out c);
            couleurEtat = c;
            // même règle que la phrase d'état à côté (Textes.EtatCourt) : un problème, un point à revoir, ou rien
            bool probleme = b.Points.Any(p => p.Etat == EtatPoint.Probleme), attention = b.Points.Any(p => p.Etat == EtatPoint.Attention);
            etat = Langue.T(probleme ? "À SOIGNER" : attention ? "À AMÉLIORER" : "EN FORME");
            if (!Habillage.Animations) { affiche = cible; InvalidateVisual(); return; }
            departValeur = affiche; departAnim = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double p = Math.Min(1, (t - departAnim) / 900.0);
                affiche = departValeur + (cible - departValeur) * Habillage.Douce(p);
                InvalidateVisual();
                return p < 1;
            });
        }

        private bool rotationLancee;

        private void Tourner()
        {
            if (!Habillage.Animations || rotationLancee) return;   // une seule boucle à la fois (détaché puis rattaché vite)
            rotationLancee = true;
            long t0 = Horloge.Maintenant;
            Horloge.Lancer(t => { rotation = ((t - t0) / 3.0) % 360; InvalidateVisual(); bool suite = tourne && Avalonia.VisualTree.VisualExtensions.GetVisualRoot(this) != null; if (!suite) rotationLancee = false; return suite; });
        }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double taille = Math.Min(Bounds.Width, Bounds.Height), ep = Math.Max(10, taille * 0.075), r = taille / 2 - ep / 2 - 2;
            var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
            ctx.DrawEllipse(null, new Pen(Habillage.Pinceau(P.Sombre ? Color.FromRgb(28, 31, 61) : Color.FromRgb(232, 229, 246)), ep), c, r, r);
            var degrade = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0.5, 1, RelativeUnit.Relative), EndPoint = new RelativePoint(0.5, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(P.Accent, 0), new GradientStop(P.Accent2, 1) },
            };
            var crayon = new Pen(degrade, ep, lineCap: PenLineCap.Round);
            if (cible < 0)
            {
                ctx.DrawGeometry(null, crayon, Habillage.Arc(c, r, rotation - 90, 70));
            }
            else if (affiche > 0.3)
            {
                double balayage = Math.Min(359.5, 360 * affiche / 100);
                ctx.DrawGeometry(null, crayon, Habillage.Arc(c, r, -90, balayage));
            }
            string score = cible < 0 ? "…" : Math.Round(affiche).ToString(CultureInfo.InvariantCulture);
            var ft = new FormattedText(score, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Affichage, FontStyle.Normal, FontWeight.Bold), taille * 0.27, Habillage.Pinceau(P.Texte));
            double h2 = etat != "" ? taille * 0.07 : 0;
            ctx.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - (ft.Height + h2) / 2 - 2));
            if (etat != "")
            {
                var e = new FormattedText(etat, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Corps, FontStyle.Normal, FontWeight.Bold), Math.Max(10, taille * 0.058), Habillage.Pinceau(couleurEtat));
                ctx.DrawText(e, new Point(c.X - e.Width / 2, c.Y + (ft.Height - h2) / 2 - 4));
            }
        }
    }
}
