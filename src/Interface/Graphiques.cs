// Nettoyage PC - petits graphiques partagés par les outils Premium (courbe dans le temps avec repères, barres par jour)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    /// <summary>Courbe d'une valeur dans le temps, avec des repères verticaux (changements) et une prévision en pointillés.</summary>
    internal class CourbeTemps : Control, IThemable
    {
        public string Titre = "", VideTexte = "Pas encore assez de mesures.";
        public List<Tuple<DateTime, double>> Points = new List<Tuple<DateTime, double>>();
        /// <summary>Repères : date, suspect (orange) ou non (discret).</summary>
        public List<Tuple<DateTime, bool>> Reperes = new List<Tuple<DateTime, bool>>();
        public Func<double, string> Format = v => v.ToString("0.#", Appli.Fr);
        /// <summary>Prévision linéaire (pente par jour) prolongée jusqu'à cette date, à partir du dernier point.</summary>
        public double? Pente; public DateTime? PrevisionJusqua;
        /// <summary>Ligne horizontale de seuil (ex. 100 % d'usure).</summary>
        public double? Seuil;
        public bool Barres;
        /// <summary>Axe vertical à partir de 0 (sinon, il suit les valeurs : températures...).</summary>
        public bool DepuisZero = true;
        public DateTime? Debut;

        public CourbeTemps() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }
        public void AppliquerTheme() { Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.RemplirArrondi(g, P.Carte, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            Theme.Contour(g, P.Bordure, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            Theme.Texte(g, Titre, Theme.Petit, new Rectangle(14, 6, Width - 28, 20), P.Discret, TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            var pts = Points.Where(p => !Debut.HasValue || p.Item1 >= Debut.Value).OrderBy(p => p.Item1).ToList();
            if (pts.Count < (Barres ? 1 : 2)) { Theme.Texte(g, VideTexte, Theme.Normal, new Rectangle(14, 26, Width - 28, Height - 36), P.Discret, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak); return; }
            int haut = 32, bas = Height - 24, gauche = 54, droite = Width - 16;
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
            Func<DateTime, float> X = d => gauche + (float)((d - t0).TotalSeconds / (t1 - t0).TotalSeconds) * (droite - gauche);
            Func<double, float> Y = v => (float)(bas - (bas - haut) * (v - vmin) / (vmax - vmin));
            using (var p = new Pen(Color.FromArgb(50, P.Discret)) { DashStyle = DashStyle.Dot })
                for (int i = 0; i <= 2; i++)
                {
                    double v = vmin + (vmax - vmin) * i / 2.0;
                    g.DrawLine(p, gauche, Y(v), droite, Y(v));
                    Theme.Texte(g, Format(v), Theme.Petit, new Rectangle(2, (int)Y(v) - 9, gauche - 6, 18), P.Discret, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
            foreach (var r in Reperes.Where(x => x.Item1 >= t0 && x.Item1 <= t1))
                using (var p = new Pen(r.Item2 ? Color.FromArgb(200, P.Orange) : Color.FromArgb(60, P.Discret), r.Item2 ? 1.6f : 1f) { DashStyle = r.Item2 ? DashStyle.Solid : DashStyle.Dash })
                    g.DrawLine(p, X(r.Item1), haut, X(r.Item1), bas);
            if (Seuil.HasValue)
                using (var p = new Pen(Color.FromArgb(160, P.Rouge), 1.2f) { DashStyle = DashStyle.Dash }) g.DrawLine(p, gauche, Y(Seuil.Value), droite, Y(Seuil.Value));
            if (Barres)
            {
                float lb = Math.Max(2, (droite - gauche) / (float)Math.Max(1, (t1 - t0).TotalDays) * 0.7f);
                foreach (var pt in pts)
                {
                    float h = bas - Y(pt.Item2);
                    if (h >= 0.5f) Theme.RemplirArrondi(g, P.Lueur1, X(pt.Item1), bas - h, lb, h, 2);
                }
            }
            else
            {
                var ligne = pts.Select(p => new PointF(X(p.Item1), Y(p.Item2))).ToArray();
                using (var br = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), P.Lueur1, P.Lueur2, LinearGradientMode.Horizontal))
                using (var p = new Pen(br, 2.2f) { LineJoin = LineJoin.Round })
                {
                    if (ligne.Length >= 2) g.DrawLines(p, ligne);
                    foreach (var pt in ligne) g.FillEllipse(br, pt.X - 2.5f, pt.Y - 2.5f, 5, 5);
                }
                if (Pente.HasValue && PrevisionJusqua.HasValue)
                {
                    var der = pts[pts.Count - 1];
                    double vf = der.Item2 + Pente.Value * (PrevisionJusqua.Value - der.Item1).TotalDays;
                    using (var p = new Pen(Color.FromArgb(170, P.Orange), 1.6f) { DashStyle = DashStyle.Dash }) g.DrawLine(p, X(der.Item1), Y(der.Item2), X(PrevisionJusqua.Value), Y(vf));
                }
            }
            Theme.Texte(g, t0.ToString("dd/MM/yy"), Theme.Petit, new Rectangle(gauche - 4, bas + 3, 100, 18), P.Discret, TextFormatFlags.Left | TextFormatFlags.SingleLine);
            Theme.Texte(g, (Barres ? t1.AddDays(-1) : t1).ToString("dd/MM/yy"), Theme.Petit, new Rectangle(droite - 100, bas + 3, 100, 18), P.Discret, TextFormatFlags.Right | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>Barres horizontales libellées (classement), valeur à droite.</summary>
    internal class BarresClassement : Control, IThemable
    {
        public string Titre = "";
        public List<Tuple<string, double, string>> Lignes = new List<Tuple<string, double, string>>();   // libellé, valeur, texte affiché
        public BarresClassement() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }
        public void AppliquerTheme() { Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.RemplirArrondi(g, P.Carte, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            Theme.Contour(g, P.Bordure, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            Theme.Texte(g, Titre, Theme.Petit, new Rectangle(14, 6, Width - 28, 20), P.Discret, TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            if (Lignes.Count == 0) { Theme.Texte(g, "Pas encore de mesures.", Theme.Normal, new Rectangle(14, 28, Width - 28, Height - 36), P.Discret, TextFormatFlags.Left | TextFormatFlags.VerticalCenter); return; }
            double max = Math.Max(1e-9, Lignes.Max(l => l.Item2));
            int y = 30, hl = Math.Max(18, Math.Min(26, (Height - 38) / Math.Max(1, Lignes.Count)));
            int lLib = Math.Min(250, (int)(Width * 0.44)), lVal = 80, x0 = 14 + lLib + 8, lMax = Width - x0 - lVal - 18;
            foreach (var l in Lignes)
            {
                if (y + hl > Height - 6) break;
                Theme.Texte(g, l.Item1, Theme.Normal, new Rectangle(14, y, lLib, hl), P.Texte, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                float lb = (float)(lMax * l.Item2 / max);
                Theme.RemplirArrondi(g, P.Piste, x0, y + hl / 2 - 4, lMax, 8, 4);
                if (lb >= 1) Theme.RemplirDegrade(g, P.Lueur1, P.Lueur2, x0, y + hl / 2 - 4, Math.Max(8, lb), 8, 4);
                Theme.Texte(g, l.Item3, Theme.PetitGras, new Rectangle(Width - lVal - 14, y, lVal, hl), P.Texte, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                y += hl;
            }
        }
    }
}
