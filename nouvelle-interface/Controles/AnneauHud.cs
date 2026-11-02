// Nettoyage PC 5 - anneau du style HUD (maquette A) : graduations pointillées qui tournent, anneau épais en segments
// rempli selon l'indice, anneau violet en tirets, et au centre « INDICE », le score lumineux et le niveau.
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    public sealed class AnneauHud : Panel
    {
        private readonly AnneauHudFixe fixe = new AnneauHudFixe();
        private readonly AnneauHudPointille pointille = new AnneauHudPointille();
        private readonly AnneauHudTirets tirets = new AnneauHudTirets();
        private readonly RotateTransform rotation = new RotateTransform(), contre = new RotateTransform();
        private readonly TextBlock tScore, tNiveau;
        private readonly Action<long> ambiance;
        private int score = -1;

        public AnneauHud(double taille)
        {
            var P = Habillage.P;
            Width = taille; Height = taille;
            pointille.RenderTransform = rotation; tirets.RenderTransform = contre;
            pointille.IsHitTestVisible = false; tirets.IsHitTestVisible = false;
            Children.Add(pointille); Children.Add(fixe); Children.Add(tirets);
            var centre = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            centre.Children.Add(new TextBlock { Text = Langue.T("INDICE"), FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 2, Foreground = Habillage.Pinceau(P.Discret), HorizontalAlignment = HorizontalAlignment.Center });
            tScore = new TextBlock
            {
                Text = "…", FontFamily = Habillage.Affichage, FontSize = 72, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Texte), HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, -6, 0, -6),
                Effect = P.Sombre ? new DropShadowEffect { BlurRadius = 22, OffsetX = 0, OffsetY = 0, Color = Habillage.Alpha(P.Accent, 170) } : null,
            };
            tNiveau = new TextBlock { Text = Langue.T("analyse…").ToUpper(Langue.Culture), FontFamily = Habillage.Console, FontSize = 11, LetterSpacing = 1, Foreground = Habillage.Pinceau(P.Orange), HorizontalAlignment = HorizontalAlignment.Center };
            centre.Children.Add(tScore); centre.Children.Add(tNiveau);
            Children.Add(centre);
            long debut = Horloge.Maintenant;
            ambiance = t => { double s = (t - debut) / 1000.0; rotation.Angle = (s * 6) % 360; contre.Angle = -(s * 10) % 360; };
            AttachedToVisualTree += (s, e) => Horloge.Ambiance(ambiance);
            DetachedFromVisualTree += (s, e) => Horloge.RetirerAmbiance(ambiance);
        }

        public void Montrer(Bilan b)
        {
            int cible = b.Score, depart = Math.Max(0, score);
            score = cible;
            var c = Habillage.Niveau(cible);
            tNiveau.Text = (cible >= 85 ? "✔ " : "▲ ") + Langue.T(Textes.Niveau(cible)).ToUpper(Langue.Culture);
            tNiveau.Foreground = Habillage.Pinceau(cible >= 85 ? Habillage.P.Vert : c);
            long t0 = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double p = Math.Min(1, (t - t0) / 1100.0), e = Habillage.Douce(p);
                fixe.Valeur = (depart + (cible - depart) * e) / 100.0;
                tScore.Text = ((int)Math.Round(depart + (cible - depart) * e)).ToString(CultureInfo.InvariantCulture);
                fixe.InvalidateVisual();
                return p < 1;
            });
        }
    }

    /// <summary>Anneau épais en segments : piste sombre, partie remplie lumineuse.</summary>
    public sealed class AnneauHudFixe : Control
    {
        public double Valeur;   // 0 à 1

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height, R = Math.Min(w, h) / 2;
            var c = new Point(w / 2, h / 2);
            double r = R * 0.79, ep = R * 0.12;
            ctx.DrawEllipse(null, new Pen(Habillage.Pinceau(P.Accent, 26), ep), c, r, r);
            if (Valeur > 0.002)
            {
                double balayage = Math.Min(359.9, 360 * Valeur);
                if (P.Sombre) ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(P.Accent, 45), ep + 8), Habillage.Arc(c, r, -90, balayage));
                ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(P.Accent), ep), Habillage.Arc(c, r, -90, balayage));
            }
            // fentes entre les segments (couleur du panneau)
            var fente = new Pen(Habillage.Pinceau(P.Carte), 3);
            for (int i = 0; i < 360; i += 6)
            {
                double a = i * Math.PI / 180, r1 = r - ep / 2 - 5, r2 = r + ep / 2 + 5;
                ctx.DrawLine(fente, new Point(c.X + r1 * Math.Cos(a), c.Y + r1 * Math.Sin(a)), new Point(c.X + r2 * Math.Cos(a), c.Y + r2 * Math.Sin(a)));
            }
            ctx.DrawEllipse(null, new Pen(Habillage.Pinceau(P.Accent, 38), 1), c, R * 0.56, R * 0.56);
        }
    }

    /// <summary>Graduations pointillées extérieures (tournent lentement).</summary>
    public sealed class AnneauHudPointille : Control
    {
        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height, R = Math.Min(w, h) / 2;
            var c = new Point(w / 2, h / 2);
            double r = R * 0.95;
            var crayon = new Pen(Habillage.Pinceau(P.Accent, 110), 1.2);
            for (int i = 0; i < 120; i++)
            {
                double a = i * 3 * Math.PI / 180, l = i % 10 == 0 ? 6 : 2.5;
                ctx.DrawLine(crayon, new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a)), new Point(c.X + (r - l) * Math.Cos(a), c.Y + (r - l) * Math.Sin(a)));
            }
        }
    }

    /// <summary>Anneau violet en tirets (tourne à l'envers).</summary>
    public sealed class AnneauHudTirets : Control
    {
        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width, h = Bounds.Height, R = Math.Min(w, h) / 2;
            var c = new Point(w / 2, h / 2);
            var crayon = new Pen(new SolidColorBrush(Color.FromArgb(150, 124, 92, 255)), 2);
            for (int k = 0; k < 9; k++) ctx.DrawGeometry(null, crayon, Habillage.Arc(c, R * 0.63, k * 40, 30));
        }
    }
}
