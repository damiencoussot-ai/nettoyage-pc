// Nettoyage PC 5 - l'anneau holographique de l'accueil.
// Trois calques : le halo qui respire (opacité), l'anneau fixe (graduations, secteurs, disque central),
// et l'anneau qui tourne (repère et traînée) ; ce dernier et le halo ne sont jamais redessinés :
// seules leur rotation et leur opacité changent, ce que la carte graphique fait seule.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    /// <summary>Anneau complet (calques + textes du centre), prêt à poser à l'accueil.</summary>
    public sealed class Anneau : Panel
    {
        private readonly AnneauFixe fixe = new AnneauFixe();
        private readonly AnneauTournant tournant = new AnneauTournant();
        private readonly Halo halo = new Halo();
        private readonly RotateTransform rotation = new RotateTransform();
        private readonly RotateTransform contreRotation = new RotateTransform();
        private readonly AnneauSegments segments = new AnneauSegments();
        private readonly TextBlock tScore, tEtat, tTitre;
        private readonly Action<long> ambiance;
        private int score = -1;

        public Anneau(double taille)
        {
            Width = taille; Height = taille;
            tournant.RenderTransform = rotation;
            segments.RenderTransform = contreRotation;
            halo.IsHitTestVisible = false; tournant.IsHitTestVisible = false; segments.IsHitTestVisible = false;
            Children.Add(halo); Children.Add(fixe); Children.Add(segments); Children.Add(tournant);
            var centre = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 0 };
            tTitre = new TextBlock { Text = "INTÉGRITÉ", FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(Habillage.P.Discret), HorizontalAlignment = HorizontalAlignment.Center };
            tScore = new TextBlock { Text = "…", FontFamily = Habillage.Affichage, FontSize = 62, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(Habillage.P.Texte), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, -4, 0, -6) };
            tEtat = new TextBlock { Text = "analyse…", FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(Habillage.P.Orange), HorizontalAlignment = HorizontalAlignment.Center };
            centre.Children.Add(tTitre); centre.Children.Add(tScore); centre.Children.Add(tEtat);
            Children.Add(centre);
            long debut = Horloge.Maintenant;
            ambiance = t =>
            {
                double s = (t - debut) / 1000.0;
                rotation.Angle = (s * 24) % 360;            // un tour en 15 secondes
                contreRotation.Angle = -(s * 9) % 360;      // l'anneau intérieur tourne doucement à l'envers
                halo.Opacity = 0.75 + 0.25 * Math.Sin(s * 0.8);
            };
            AttachedToVisualTree += (s, e) => Horloge.Ambiance(ambiance);
            DetachedFromVisualTree += (s, e) => Horloge.RetirerAmbiance(ambiance);
        }

        /// <summary>Affiche un bilan : le score monte en une seconde, les secteurs se remplissent avec lui.</summary>
        public void Montrer(Bilan b)
        {
            fixe.Secteurs = b.Secteurs;
            int cible = b.Score, depart = Math.Max(0, score);
            score = cible;
            tEtat.Text = Textes.Niveau(cible);
            tEtat.Foreground = Habillage.Pinceau(Habillage.Niveau(cible));
            long t0 = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double p = Math.Min(1, (t - t0) / 1100.0), e = Habillage.Douce(p);
                fixe.Progression = e;
                tScore.Text = ((int)Math.Round(depart + (cible - depart) * e)).ToString();
                fixe.InvalidateVisual();
                return p < 1;
            });
        }
    }

    /// <summary>Graduations, quatre secteurs (stockage, mémoire, sécurité, matériel) et disque central.</summary>
    public sealed class AnneauFixe : Control
    {
        public double[] Secteurs = { -1, -1, -1, -1 };
        public double Progression = 1;

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height, R = Math.Min(w, h) / 2 - 4;
            var c = new Point(w / 2, h / 2);
            // graduations
            var fine = new Pen(Habillage.Pinceau(P.Accent, 70), 1);
            var grande = new Pen(Habillage.Pinceau(P.Accent, 150), 1.4);
            for (int i = 0; i < 72; i++)
            {
                double a = i * 5 * Math.PI / 180;
                bool g = i % 6 == 0;
                double r1 = R - (g ? 9 : 6), r2 = R - 2;
                ctx.DrawLine(g ? grande : fine, new Point(c.X + r1 * Math.Cos(a), c.Y + r1 * Math.Sin(a)), new Point(c.X + r2 * Math.Cos(a), c.Y + r2 * Math.Sin(a)));
            }
            // fin cercle du repère (le repère lui-même est sur le calque qui tourne)
            ctx.DrawEllipse(null, new Pen(Habillage.Pinceau(P.Accent, 40), 1), c, R - 16, R - 16);
            // secteurs
            double rs = R - 32;
            var piste = new Pen(Habillage.Pinceau(P.Accent, 34), 11);
            for (int k = 0; k < 4; k++)
            {
                double debut = -90 + k * 90 + 5;
                ctx.DrawGeometry(null, piste, Habillage.Arc(c, rs, debut, 80));
                double v = Secteurs != null && k < Secteurs.Length ? Secteurs[k] : -1;
                if (v >= 0 && Progression > 0)
                {
                    double balayage = Math.Max(1, 80 * Math.Max(0.04, v / 100) * Progression);
                    var plein = new Pen(Habillage.Pinceau(Habillage.Niveau(v)), 11, lineCap: PenLineCap.Flat);
                    ctx.DrawGeometry(null, plein, Habillage.Arc(c, rs, debut, balayage));
                }
            }
            // disque central
            double rc = R - 50;
            ctx.DrawEllipse(Habillage.Pinceau(P.Sombre ? Color.FromRgb(8, 14, 26) : Colors.White), new Pen(Habillage.Pinceau(P.Accent, 60), 1), c, rc, rc);
        }
    }

    /// <summary>Repère lumineux et sa traînée (calque qui tourne).</summary>
    public sealed class AnneauTournant : Control
    {
        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height, R = Math.Min(w, h) / 2 - 4, rr = R - 16;
            var c = new Point(w / 2, h / 2);
            for (int i = 1; i <= 12; i++)
                ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(P.Accent, (int)(170 * (1 - i / 13.0))), 2), Habillage.Arc(c, rr, -90 - i * 3.2, 3.4));
            ctx.DrawEllipse(Habillage.Pinceau(P.Accent), null, new Point(c.X, c.Y - rr), 3.2, 3.2);
            ctx.DrawEllipse(Habillage.Pinceau(P.Accent, 60), null, new Point(c.X, c.Y - rr), 7, 7);
        }
    }

    /// <summary>Anneau intérieur en 12 segments, qui tourne lentement à l'envers.</summary>
    public sealed class AnneauSegments : Control
    {
        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height, R = Math.Min(w, h) / 2 - 4, r = R - 58;
            var c = new Point(w / 2, h / 2);
            var pen = new Pen(Habillage.Pinceau(P.Accent, 90), 2);
            for (int k = 0; k < 12; k++) ctx.DrawGeometry(null, pen, Habillage.Arc(c, r, k * 30 + 4, 18));
        }
    }

    /// <summary>Halo doux derrière l'anneau (cercles concentriques de plus en plus transparents).</summary>
    public sealed class Halo : Control
    {
        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height, R = Math.Min(w, h) / 2 - 2;
            var c = new Point(w / 2, h / 2);
            int pas = 14;
            for (int i = 0; i < pas; i++)
            {
                double r = R * (1 - i / (double)pas);
                ctx.DrawEllipse(Habillage.Pinceau(P.Accent, P.Sombre ? 5 : 3), null, c, r, r);
            }
        }
    }
}
