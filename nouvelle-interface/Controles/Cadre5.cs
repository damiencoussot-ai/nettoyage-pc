// Nettoyage PC 5 - cadre des cartes : coins coupés (style HUD) ou légèrement arrondis (style classique).
// Mêmes propriétés que Border (fond, bordure, rayon, marge intérieure, enfant) pour remplacer un Border sans autre changement.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    public sealed class Cadre5 : Decorator
    {
        private IBrush fond, bordure;
        private Thickness epaisseur;
        private CornerRadius rayon = new CornerRadius(3);
        private Color? traitHaut;

        public IBrush Background { get { return fond; } set { fond = value; InvalidateVisual(); } }
        public IBrush BorderBrush { get { return bordure; } set { bordure = value; InvalidateVisual(); } }
        public Thickness BorderThickness { get { return epaisseur; } set { epaisseur = value; InvalidateVisual(); } }
        /// <summary>Style classique seulement (le HUD coupe les coins).</summary>
        public CornerRadius CornerRadius { get { return rayon; } set { rayon = value; InvalidateVisual(); } }
        /// <summary>Taille de la coupe des coins en style HUD (0 : angles droits).</summary>
        public double Coupe = 14;
        /// <summary>HUD : seulement le coin haut droit coupé (tuiles de l'accès rapide).</summary>
        public bool UnCoin;
        /// <summary>Trait de couleur de 2 px en haut (tuiles de mesure du HUD).</summary>
        public Color? TraitHaut { get { return traitHaut; } set { traitHaut = value; InvalidateVisual(); } }

        public override void Render(DrawingContext ctx)
        {
            var r = new Rect(Bounds.Size);
            if (r.Width <= 0 || r.Height <= 0) return;
            double e = Math.Max(Math.Max(epaisseur.Left, epaisseur.Right), Math.Max(epaisseur.Top, epaisseur.Bottom));
            var crayon = bordure != null && e > 0 ? new Pen(bordure, e) : null;
            if (Habillage.Hud)
            {
                var forme = Habillage.Chanfrein(r.Deflate(e / 2), Coupe, !UnCoin);
                ctx.DrawGeometry(fond, crayon, forme);
                if (traitHaut.HasValue) ctx.DrawRectangle(Habillage.Pinceau(traitHaut.Value), null, new Rect(0, 0, Math.Max(0, r.Width - Coupe), 2));
            }
            else
            {
                var forme = new RoundedRect(r.Deflate(e / 2), rayon.TopLeft, rayon.TopRight, rayon.BottomRight, rayon.BottomLeft);
                ctx.DrawRectangle(fond, crayon, forme);
            }
        }
    }

    /// <summary>Grille holographique du fond (style HUD) : lignes fines tous les 32 px.</summary>
    public sealed class GrilleFond : Control
    {
        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            var crayon = new Pen(Habillage.Pinceau(P.Accent, P.Sombre ? 12 : 18), 1);
            double w = Bounds.Width, h = Bounds.Height;
            for (double x = 0.5; x < w; x += 32) ctx.DrawLine(crayon, new Point(x, 0), new Point(x, h));
            for (double y = 0.5; y < h; y += 32) ctx.DrawLine(crayon, new Point(0, y), new Point(w, y));
        }
    }

    /// <summary>HUD : trait lumineux qui balaie la page de haut en bas quand elle s'ouvre.</summary>
    public sealed class BalayageHud : Control
    {
        private double position = -1;

        public void Lancer()
        {
            if (!Habillage.Animations || !Habillage.Hud) return;
            long debut = Horloge.Maintenant;
            IsVisible = true;
            Horloge.Lancer(t =>
            {
                position = Math.Min(1, (t - debut) / 650.0);
                InvalidateVisual();
                if (position >= 1) { position = -1; IsVisible = false; return false; }
                return true;
            });
        }

        public override void Render(DrawingContext ctx)
        {
            if (position < 0) return;
            var P = Habillage.P;
            double h = Bounds.Height, w = Bounds.Width, y = Habillage.Douce(position) * h, alpha = 1 - position * 0.6;
            var traine = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Habillage.Alpha(P.Accent, 0), 0), new GradientStop(Habillage.Alpha(P.Accent, (int)(38 * alpha)), 1) },
            };
            ctx.DrawRectangle(traine, null, new Rect(0, Math.Max(0, y - 90), w, Math.Min(90, y)));
            ctx.DrawRectangle(Habillage.Pinceau(P.Accent, (int)(200 * alpha)), null, new Rect(0, y, w, 1.5));
        }
    }
}
