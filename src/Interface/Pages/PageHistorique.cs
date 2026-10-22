// Nettoyage PC - page Historique : graphiques (espace libéré, score de santé) et modifications réversibles
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class GraphiquesHistorique : Control, IThemable
    {
        public List<Tuple<DateTime, long>> Stats = new List<Tuple<DateTime, long>>();
        public List<Tuple<DateTime, int>> Scores = new List<Tuple<DateTime, int>>();

        public GraphiquesHistorique()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        public void AppliquerTheme() { Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            int w = ClientSize.Width, h = ClientSize.Height;
            g.Clear(Theme.FondDerriere(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.RemplirArrondi(g, P.Carte, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 9);
            Theme.Contour(g, P.Bordure, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 9);
            int demi = w / 2, haut = 56, bas = h - 34;
            var fl = TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            var fc = TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;

            // --- Gauche : espace libéré par passage
            Theme.Texte(g, Langue.T("Espace libéré par passage"), Theme.CarteTitre, new Rectangle(16, 12, demi - 30, 22), P.Texte, fl);
            long total = Stats.Sum(s => s.Item2);
            Theme.Texte(g, "Total : " + Format.Taille(total) + " en " + Stats.Count + " passage(s)", Theme.Petit, new Rectangle(18, 34, demi - 30, 18), P.Discret, fl);
            using (var p = new Pen(P.Piste)) { g.DrawLine(p, 16, bas, demi - 16, bas); g.DrawLine(p, w - 16, bas, demi + 16, bas); }
            var stats = Stats.Skip(Math.Max(0, Stats.Count - 10)).ToList();
            if (stats.Count == 0) Theme.Texte(g, Langue.T("Aucun nettoyage enregistré pour l'instant."), Theme.Normal, new Rectangle(18, haut + 50, demi - 30, 22), P.Discret, fl);
            else
            {
                double max = Math.Max(1, stats.Max(s => s.Item2));
                float larg = (demi - 40f) / stats.Count;
                for (int i = 0; i < stats.Count; i++)
                {
                    float hb = (float)Math.Max(2, (bas - haut - 20) * stats[i].Item2 / max);
                    float x0 = 24 + i * larg, lb = larg * 0.62f;
                    Theme.RemplirDegrade(g, P.Lueur1, P.Lueur2, x0, bas - hb, lb, hb, 3, true);
                    // valeur courte (« 78 Mo »), affichée seulement si elle tient au-dessus de sa barre (toujours pour la plus haute)
                    string v = TailleCourte(stats[i].Item2);
                    int lt = TextRenderer.MeasureText(v, Theme.Petit).Width;
                    if (lt <= larg - 2 || stats[i].Item2 >= max)
                        Theme.Texte(g, v, Theme.Petit, new Rectangle((int)(x0 + lb / 2 - 40), (int)(bas - hb - 17), 80, 16), P.Texte, fc);
                    Theme.Texte(g, stats[i].Item1.ToString("dd/MM"), Theme.Petit, new Rectangle((int)(x0 + lb / 2 - 30), bas + 6, 60, 16), P.Discret, fc);
                }
            }

            // --- Droite : score de santé (dernier score de chaque jour)
            Theme.Texte(g, Langue.T("Score de santé"), Theme.CarteTitre, new Rectangle(demi + 16, 12, demi - 30, 22), P.Texte, fl);
            var scores = Scores.Skip(Math.Max(0, Scores.Count - 14)).ToList();
            if (scores.Count == 0) { Theme.Texte(g, Langue.T("Pas encore de score enregistré."), Theme.Normal, new Rectangle(demi + 18, haut + 50, demi - 30, 22), P.Discret, fl); return; }
            Theme.Texte(g, "Aujourd'hui : " + scores[scores.Count - 1].Item2 + " / 100", Theme.Petit, new Rectangle(demi + 18, 34, demi - 30, 18), P.Discret, fl);
            float zone = w - demi - 64;
            float pas = scores.Count > 1 ? zone / (scores.Count - 1) : 0;
            var pts = scores.Select((s, i) => new PointF(demi + 32 + i * pas, bas - (bas - haut - 12) * s.Item2 / 100f)).ToArray();
            if (pts.Length > 1)
            {
                // aire sous la courbe, puis la courbe
                using (var aire = new GraphicsPath())
                {
                    aire.AddLines(pts); aire.AddLine(pts[pts.Length - 1], new PointF(pts[pts.Length - 1].X, bas)); aire.AddLine(new PointF(pts[pts.Length - 1].X, bas), new PointF(pts[0].X, bas));
                    aire.CloseFigure();
                    using (var b = new LinearGradientBrush(new RectangleF(0, haut, 10, Math.Max(2, bas - haut)), Color.FromArgb(70, P.Vert), Color.FromArgb(0, P.Vert), LinearGradientMode.Vertical)) g.FillPath(b, aire);
                }
                using (var l = new Pen(P.Vert, 2.6f) { LineJoin = LineJoin.Round }) g.DrawLines(l, pts);
            }
            using (var b = new SolidBrush(P.Vert))
                for (int i = 0; i < pts.Length; i++)
                {
                    g.FillEllipse(b, pts[i].X - 4, pts[i].Y - 4, 8, 8);
                    if (i == 0 || i == pts.Length - 1 || pts.Length <= 7)
                    {
                        Theme.Texte(g, scores[i].Item2.ToString(), Theme.Petit, new Rectangle((int)pts[i].X - 20, (int)pts[i].Y - 21, 40, 16), P.Texte, fc);
                        Theme.Texte(g, scores[i].Item1.ToString("dd/MM"), Theme.Petit, new Rectangle((int)pts[i].X - 30, bas + 6, 60, 16), P.Discret, fc);
                    }
                }
        }

        private static string TailleCourte(long o)
        {
            bool fr = Langue.Code == "fr" || Langue.Code == "es";
            if (o >= 1073741824L) return (o / 1073741824.0).ToString(o >= 10737418240L ? "0" : "0.#", Appli.Fr) + (fr ? " Go" : " GB");
            if (o >= 1048576L) return (o / 1048576.0).ToString("0", Appli.Fr) + (fr ? " Mo" : " MB");
            return (o / 1024.0).ToString("0", Appli.Fr) + (fr ? " Ko" : " KB");
        }
    }

    internal class PageHistorique : Page
    {
        private readonly GraphiquesHistorique graph = new GraphiquesHistorique { Location = new Point(28, 4) };
        private readonly Etiquette lblModifs, lblInfo;
        private readonly ListeSombre lv = new ListeSombre { MultiSelect = true };
        private readonly Bouton bAnnuler, bTout, bActu;

        public PageHistorique(FenetrePrincipale f) : base(f, "Historique", "Historique", "Ce que Nettoyage PC a fait, et l'annulation de chaque modification.", 0xE81C)
        {
            AutoScroll = false;
            Controls.Add(graph);
            lblModifs = Titre("Modifications réversibles", 28, 248);
            lv.Colonne("Date", 130); lv.Colonne("Modification", 560); lv.Colonne("État", 160); lv.ColonneElastique = 1;
            Controls.Add(lv);
            bAnnuler = NouveauBouton("Annuler la sélection", true, 170, 32, 28, 0, () =>
            {
                var ids = lv.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag).ToList();
                if (ids.Count == 0) { BoiteMessage.Info("Sélectionne une ou plusieurs modifications dans la liste."); return; }
                AnnulerIds(ids);
            });
            bTout = NouveauBouton("Tout annuler", false, 130, 32, 206, 0, () =>
            {
                if (!BoiteMessage.Question("Annuler toutes les modifications actives ?\n\nLes téléchargements reviennent à leur place, les programmes au démarrage sont réactivés et le mode d'alimentation précédent est rétabli.")) return;
                AnnulerIds(Historique.Lire().Where(m => !m.Annule).Select(m => m.Id).ToList());
            });
            bActu = NouveauBouton("Actualiser", false, 110, 32, 344, 0, Charger);
            lblInfo = Texte("Les nettoyages (fichiers temporaires, caches) ne s'annulent pas : ces fichiers se recréent tout seuls au besoin.", 28, 0, Role.Discret, Theme.Petit);
            Resize += (s, e) =>
            {
                int w = ClientSize.Width - 56;
                graph.Size = new Size(w, 230);
                int hl = Math.Max(120, ClientSize.Height - 280 - 76);
                lv.Location = new Point(28, 278); lv.Size = new Size(w, hl);
                int yb = 278 + hl + 10;
                bAnnuler.Location = new Point(28, yb); bTout.Location = new Point(206, yb); bActu.Location = new Point(344, yb);
                lblInfo.Location = new Point(28, yb + 40);
            };
            f.ActionFinie += cle => { if (Chargee) Charger(); };
        }

        public override void PremiereFois() { Charger(); }
        public override void Montrer() { if (Chargee) Charger(); }

        private static List<string[]> LireCsv(string nom, int max = 5000)
        {
            var r = new List<string[]>();
            try
            {
                string f = Chemins.Fichier(nom);
                if (!File.Exists(f)) return r;
                var lignes = File.ReadAllLines(f);
                foreach (var l in lignes.Skip(Math.Max(0, lignes.Length - max))) { var c = l.TrimStart('﻿').Split(';'); if (c.Length >= 2) r.Add(c); }
            }
            catch { }
            return r;
        }

        private bool enLecture, relire;

        /// <summary>Relit les statistiques et l'historique en arrière-plan (la page s'affiche tout de suite).</summary>
        public void Charger()
        {
            if (enLecture) { relire = true; return; }
            enLecture = true;
            EnFond(() =>
            {
                // Mode jeu terminé (plus de modejeu.json) mais encore « Active » dans l'historique (versions 2.7 et antérieures) : on corrige
                if (!File.Exists(Chemins.Fichier("modejeu.json"))) Historique.MarquerAnnulees(m => m.Titre.StartsWith("Mode jeu"));
                DateTime d;
                var stats = new List<Tuple<DateTime, long>>();
                foreach (var c in LireCsv("statistiques.csv"))
                {
                    long o;
                    if (DateTime.TryParseExact(c[0], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && long.TryParse(c[1], out o)) stats.Add(Tuple.Create(d, o));
                }
                var parJour = new SortedDictionary<string, Tuple<DateTime, int>>();
                foreach (var c in LireCsv("scores.csv", 2000))
                {
                    int sc;
                    if (DateTime.TryParseExact(c[0], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && int.TryParse(c[1], out sc)) parJour[d.ToString("yyyy-MM-dd")] = Tuple.Create(d, sc);
                }
                return Tuple.Create(stats, parJour.Values.ToList(), Historique.Lire());
            }, r =>
            {
                enLecture = false;
                if (r != null) Afficher(r.Item1, r.Item2, r.Item3);
                if (relire) { relire = false; Charger(); }
            });
        }

        private void Afficher(List<Tuple<DateTime, long>> stats, List<Tuple<DateTime, int>> scores, List<Modif> liste)
        {
            graph.Stats = stats; graph.Scores = scores; graph.Invalidate();

            int actives = 0;
            lv.BeginUpdate(); lv.Items.Clear();
            for (int i = liste.Count - 1; i >= 0; i--)
            {
                var m = liste[i];
                var it = lv.Ajouter(m.Id, m.Date, m.Titre, m.Annule ? "Annulée" : "Active");
                if (m.Annule) it.ForeColor = Theme.P.Discret; else actives++;
            }
            lv.EndUpdate();
            bTout.Enabled = actives > 0;
            lblModifs.Text = liste.Count == 0 ? "Modifications réversibles : aucune pour l'instant" : "Modifications réversibles : " + actives + " active(s)";
        }

        private void AnnulerIds(List<string> ids)
        {
            var liste = Historique.Lire();
            int ok = 0; var echecs = new List<string>();
            Cursor = Cursors.WaitCursor;
            try
            {
                foreach (var m in liste)
                {
                    if (!ids.Contains(m.Id) || m.Annule) continue;
                    string raison = Historique.Annuler(m);
                    if (raison != "") echecs.Add(m.Titre + " : " + raison); else { m.Annule = true; ok++; }
                }
                Historique.Ecrire(liste);
            }
            finally { Cursor = Cursors.Default; }
            foreach (var cle in new[] { "Demarrage", "Jeux" }) { var p = F.Pages[cle]; if (p.Chargee) { try { p.Chargee = false; if (F.PageActive == cle) F.AfficherPage(cle); } catch { } } }
            Charger();
            string msg = ok + " modification(s) annulée(s).";
            if (echecs.Count > 0) msg += "\n\nImpossible d'annuler :\n- " + string.Join("\n- ", echecs);
            BoiteMessage.Info(msg);
        }
    }
}
