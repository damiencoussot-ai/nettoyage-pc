// Nettoyage PC 5 - petits graphiques des outils Premium : courbe dans le temps (repères, seuil, prévision en pointillés,
// ou barres par jour), classement en barres horizontales, jauge de santé en arc.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal static class Dessin
    {
        public static FormattedText Texte(string t, double taille, Color c, bool gras = false, bool chiffres = false)
        {
            return new FormattedText(t ?? "", CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
                new Typeface(chiffres ? Habillage.Console : gras ? Habillage.Affichage : Habillage.Corps, FontStyle.Normal, gras ? FontWeight.SemiBold : FontWeight.Normal), taille, Habillage.Pinceau(c));
        }

        /// <summary>Texte coupé avec « … » s'il dépasse la largeur donnée.</summary>
        public static FormattedText TexteCoupe(string t, double taille, Color c, double largeur, bool gras = false, bool chiffres = false)
        {
            var f = Texte(t, taille, c, gras, chiffres);
            f.MaxTextWidth = Math.Max(10, largeur); f.MaxLineCount = 1; f.Trimming = TextTrimming.CharacterEllipsis;
            return f;
        }

        public static void Cadre(DrawingContext ctx, Rect r)
        {
            var P = Habillage.P;
            ctx.DrawRectangle(Habillage.Pinceau(P.Carte), new Pen(Habillage.Pinceau(P.Bordure), 1), new RoundedRect(r.Deflate(0.5), 3));
        }

        public static IBrush Degrade(bool vertical = false)
        {
            var P = Habillage.P;
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, vertical ? 0 : 0.5, RelativeUnit.Relative), EndPoint = new RelativePoint(vertical ? 0 : 1, vertical ? 1 : 0.5, RelativeUnit.Relative),
                GradientStops = { new GradientStop(P.Accent, 0), new GradientStop(P.Accent2, 1) },
            };
        }
    }

    /// <summary>Valeur dans le temps, avec des repères verticaux (changements), un seuil et une prévision en pointillés.</summary>
    internal sealed class CourbeTemps5 : Control
    {
        public string Titre = "", VideTexte = "Pas encore assez de mesures.";
        public List<Tuple<DateTime, double>> Points = new List<Tuple<DateTime, double>>();
        /// <summary>Repères : date, suspect (orange) ou non (discret).</summary>
        public List<Tuple<DateTime, bool>> Reperes = new List<Tuple<DateTime, bool>>();
        public Func<double, string> Format = v => v.ToString("0.#", Appli.Fr);
        public double? Pente, Seuil;
        public DateTime? PrevisionJusqua, Debut;
        public bool Barres, DepuisZero = true;

        public void Actualiser() { InvalidateVisual(); }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height;
            Dessin.Cadre(ctx, new Rect(0, 0, w, h));
            ctx.DrawText(Dessin.TexteCoupe(Langue.T(Titre), 12, P.Discret, w - 28), new Point(14, 8));
            var pts = Points.Where(p => !Debut.HasValue || p.Item1 >= Debut.Value).OrderBy(p => p.Item1).ToList();
            if (pts.Count < (Barres ? 1 : 2))
            {
                var v = Dessin.Texte(Langue.T(VideTexte), 13, P.Discret); v.MaxTextWidth = w - 28;
                ctx.DrawText(v, new Point(14, Math.Max(30, h / 2 - v.Height / 2)));
                return;
            }
            double haut = 34, bas = h - 26, gauche = 58, droite = w - 16;
            DateTime t0 = Debut ?? pts[0].Item1, t1 = pts[pts.Count - 1].Item1;
            if (PrevisionJusqua.HasValue && Pente.HasValue && PrevisionJusqua.Value > t1) t1 = PrevisionJusqua.Value;
            if (Barres) t1 = t1.AddDays(1);
            if (t1 <= t0) t1 = t0.AddDays(1);
            double vmin = DepuisZero ? Math.Min(0, pts.Min(p => p.Item2)) : pts.Min(p => p.Item2), vmax = pts.Max(p => p.Item2);
            if (!DepuisZero) vmin = Math.Max(0, vmin - Math.Max(2, (vmax - vmin) * 0.25));
            if (Seuil.HasValue) vmax = Math.Max(vmax, Seuil.Value);
            if (Pente.HasValue && PrevisionJusqua.HasValue) { double vf = pts[pts.Count - 1].Item2 + Pente.Value * (PrevisionJusqua.Value - pts[pts.Count - 1].Item1).TotalDays; vmax = Math.Max(vmax, vf); vmin = Math.Min(vmin, vf); }
            if (vmax <= vmin) vmax = vmin + 1;
            vmax += (vmax - vmin) * 0.08;
            Func<DateTime, double> X = d => gauche + (d - t0).TotalSeconds / (t1 - t0).TotalSeconds * (droite - gauche);
            Func<double, double> Y = v => bas - (bas - haut) * (v - vmin) / (vmax - vmin);
            var grille = new Pen(Habillage.Pinceau(Habillage.Alpha(P.Discret, 50)), 1, new DashStyle(new double[] { 1, 3 }, 0));
            for (int i = 0; i <= 2; i++)
            {
                double v = vmin + (vmax - vmin) * i / 2.0;
                ctx.DrawLine(grille, new Point(gauche, Y(v)), new Point(droite, Y(v)));
                var t = Dessin.Texte(Format(v), 10.5, P.Discret, false, true);
                ctx.DrawText(t, new Point(gauche - 8 - t.Width, Y(v) - t.Height / 2));
            }
            foreach (var r in Reperes.Where(x => x.Item1 >= t0 && x.Item1 <= t1))
                ctx.DrawLine(r.Item2 ? new Pen(Habillage.Pinceau(Habillage.Alpha(P.Orange, 200)), 1.6) : new Pen(Habillage.Pinceau(Habillage.Alpha(P.Discret, 70)), 1, new DashStyle(new double[] { 3, 3 }, 0)),
                    new Point(X(r.Item1), haut), new Point(X(r.Item1), bas));
            if (Seuil.HasValue)
                ctx.DrawLine(new Pen(Habillage.Pinceau(Habillage.Alpha(P.Rouge, 170)), 1.2, new DashStyle(new double[] { 4, 3 }, 0)), new Point(gauche, Y(Seuil.Value)), new Point(droite, Y(Seuil.Value)));
            if (Barres)
            {
                double lb = Math.Max(2, (droite - gauche) / Math.Max(1, (t1 - t0).TotalDays) * 0.7);
                var br = Dessin.Degrade(true);
                foreach (var pt in pts)
                {
                    double hb = bas - Y(pt.Item2);
                    if (hb >= 0.5) ctx.DrawRectangle(br, null, new RoundedRect(new Rect(X(pt.Item1), bas - hb, lb, hb), 2, 2, 0, 0));
                }
            }
            else
            {
                var ligne = pts.Select(p => new Point(X(p.Item1), Y(p.Item2))).ToList();
                var g = new StreamGeometry();
                using (var c = g.Open()) { c.BeginFigure(ligne[0], false); for (int i = 1; i < ligne.Count; i++) c.LineTo(ligne[i]); c.EndFigure(false); }
                var br = Dessin.Degrade();
                ctx.DrawGeometry(null, new Pen(br, 2.2, lineJoin: PenLineJoin.Round), g);
                foreach (var pt in ligne) ctx.DrawEllipse(Habillage.Pinceau(P.Accent), null, pt, 2.6, 2.6);
                if (Pente.HasValue && PrevisionJusqua.HasValue)
                {
                    var der = pts[pts.Count - 1];
                    double vf = der.Item2 + Pente.Value * (PrevisionJusqua.Value - der.Item1).TotalDays;
                    ctx.DrawLine(new Pen(Habillage.Pinceau(Habillage.Alpha(P.Orange, 180)), 1.6, new DashStyle(new double[] { 4, 3 }, 0)), new Point(X(der.Item1), Y(der.Item2)), new Point(X(PrevisionJusqua.Value), Y(vf)));
                }
            }
            var d0 = Dessin.Texte(t0.ToString("dd/MM/yy"), 10.5, P.Discret, false, true);
            ctx.DrawText(d0, new Point(gauche - 4, bas + 6));
            var d1 = Dessin.Texte((Barres ? t1.AddDays(-1) : t1).ToString("dd/MM/yy"), 10.5, P.Discret, false, true);
            ctx.DrawText(d1, new Point(droite - d1.Width, bas + 6));
        }
    }

    /// <summary>Barres horizontales libellées (classement), valeur affichée à droite.</summary>
    internal sealed class BarresClassement5 : Control
    {
        public string Titre = "";
        public List<Tuple<string, double, string>> Lignes = new List<Tuple<string, double, string>>();   // libellé, valeur, texte affiché

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height;
            Dessin.Cadre(ctx, new Rect(0, 0, w, h));
            ctx.DrawText(Dessin.TexteCoupe(Langue.T(Titre), 12, P.Discret, w - 28), new Point(14, 8));
            if (Lignes.Count == 0) { ctx.DrawText(Dessin.Texte(Langue.T("Pas encore de mesures."), 13, P.Discret), new Point(14, h / 2 - 9)); return; }
            double max = Math.Max(1e-9, Lignes.Max(l => l.Item2));
            double y = 34, hl = Math.Max(20, Math.Min(28, (h - 42) / Math.Max(1, Lignes.Count)));
            double lLib = Math.Min(240, w * 0.42), lVal = 74, x0 = 14 + lLib + 10, lMax = w - x0 - lVal - 20;
            var piste = Habillage.Pinceau(Habillage.Alpha(P.Discret, 40));
            var br = Dessin.Degrade();
            foreach (var l in Lignes)
            {
                if (y + hl > h - 6) break;
                var lib = Dessin.TexteCoupe(l.Item1, 12.5, P.Texte, lLib);
                ctx.DrawText(lib, new Point(14, y + hl / 2 - lib.Height / 2));
                ctx.DrawRectangle(piste, null, new RoundedRect(new Rect(x0, y + hl / 2 - 4, lMax, 8), 4));
                double lb = lMax * l.Item2 / max;
                if (lb >= 1) ctx.DrawRectangle(br, null, new RoundedRect(new Rect(x0, y + hl / 2 - 4, Math.Max(8, lb), 8), 4));
                var v = Dessin.Texte(l.Item3, 11.5, P.Texte, false, true);
                ctx.DrawText(v, new Point(w - 14 - v.Width, y + hl / 2 - v.Height / 2));
                y += hl;
            }
        }
    }

    /// <summary>Jauge de santé en arc (100 % : neuf). Vert à partir de 80 %, orange à partir de 60 %, rouge en dessous.</summary>
    internal sealed class JaugeSante5 : Control
    {
        public double Valeur = -1;

        public JaugeSante5() { Width = 190; Height = 190; }

        private static Point SurCercle(Point c, double r, double degres) { double a = degres * Math.PI / 180; return new Point(c.X + r * Math.Cos(a), c.Y + r * Math.Sin(a)); }

        private static Geometry Arc(Point c, double r, double debut, double balayage)
        {
            var g = new StreamGeometry();
            using (var x = g.Open())
            {
                x.BeginFigure(SurCercle(c, r, debut), false);
                x.ArcTo(SurCercle(c, r, debut + balayage), new Size(r, r), 0, balayage > 180, SweepDirection.Clockwise);
                x.EndFigure(false);
            }
            return g;
        }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height, r = Math.Min(w, h) / 2 - 14;
            var c = new Point(w / 2, h / 2);
            ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(Habillage.Alpha(P.Discret, 45)), 14, lineCap: PenLineCap.Round), Arc(c, r, 135, 270));
            if (Valeur >= 0)
            {
                Color couleur = Valeur >= 80 ? P.Vert : Valeur >= 60 ? P.Orange : P.Rouge;
                double b = 270 * Math.Max(1, Math.Min(100, Valeur)) / 100;
                ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(couleur), 14, lineCap: PenLineCap.Round), Arc(c, r, 135, b));
            }
            var t = Dessin.Texte(Valeur >= 0 ? Valeur.ToString("0") + " %" : "—", 34, P.Texte, true);
            ctx.DrawText(t, new Point(c.X - t.Width / 2, c.Y - t.Height / 2 - 8));
            var s = Dessin.Texte(Langue.T("SANTÉ"), 11, P.Discret, false, true);
            ctx.DrawText(s, new Point(c.X - s.Width / 2, c.Y + 22));
        }
    }
}
