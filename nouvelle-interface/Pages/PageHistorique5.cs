// Nettoyage PC 5 - historique : espace libéré par passage, courbe de la note de santé, et les modifications
// réversibles (chacune peut être annulée : téléchargements remis en place, démarrage réactivé...).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class PageHistorique5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly GraphiquesHistorique5 graph = new GraphiquesHistorique5 { Height = 240 };
        private readonly TextBlock tModifs;
        private readonly Tableau tableau;
        private readonly BoutonNoyau bAnnuler, bTout;
        private bool enLecture;
        /// <summary>(captures) données d'exemple à la place des fichiers du PC.</summary>
        public static Tuple<List<Tuple<DateTime, long>>, List<Tuple<DateTime, int>>, List<Modif>> Exemple;

        public PageHistorique5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 14; Margin = new Thickness(28, 6, 28, 24);
            Children.Add(graph);
            tModifs = new TextBlock { Text = "Modifications réversibles", FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), Margin = new Thickness(0, 6, 0, 0) };
            Children.Add(tModifs);
            tableau = new Tableau(false, true, new Colonne("Date", "140", false, true, o => ((Modif)o).Date), new Colonne("Modification", "*"), new Colonne("État", "110"));
            tableau.HauteurListe = 360;
            tableau.SelectionChangee += () => bAnnuler.IsEnabled = tableau.Selections.Cast<Modif>().Any(m => !m.Annule);
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            bAnnuler = new BoutonNoyau("Annuler la sélection", true, 180, 32) { TaillePolice = 13.5, IsEnabled = false };
            bAnnuler.Clic += () => Annuler(tableau.Selections.Cast<Modif>().Where(m => !m.Annule).Select(m => m.Id).ToList(), false);
            bTout = new BoutonNoyau("Tout annuler", false, 130, 32) { TaillePolice = 13 };
            bTout.Clic += () => Annuler(null, true);
            var bActu = new BoutonNoyau("Actualiser", false, 110, 32) { TaillePolice = 13 };
            bActu.Clic += Charger;
            boutons.Children.Add(bAnnuler); boutons.Children.Add(bTout); boutons.Children.Add(bActu);
            Children.Add(boutons);
            Children.Add(new TextBlock { Text = Langue.T("Les nettoyages (fichiers temporaires, caches) ne s'annulent pas : ces fichiers se recréent tout seuls au besoin."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            AttachedToVisualTree += (s, e) => Charger();
        }

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

        private void Charger()
        {
            if (enLecture) return;
            enLecture = true;
            Task.Run(() =>
            {
                if (Exemple != null) return Exemple;
                // mode jeu terminé (plus de modejeu.json) mais encore « active » dans l'historique : on corrige
                try { if (!File.Exists(Chemins.Fichier("modejeu.json"))) Historique.MarquerAnnulees(m => m.Titre.StartsWith("Mode jeu")); } catch { }
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
                List<Modif> modifs;
                try { modifs = Historique.Lire(); } catch { modifs = new List<Modif>(); }
                return Tuple.Create(stats, parJour.Values.ToList(), modifs);
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enLecture = false;
                if (t.IsFaulted || t.Result == null) return;
                graph.Stats = t.Result.Item1; graph.Scores = t.Result.Item2; graph.InvalidateVisual();
                var liste = t.Result.Item3;
                int actives = liste.Count(m => !m.Annule);
                tableau.Remplir(Enumerable.Reverse(liste).Cast<object>(), o => { var m = (Modif)o; DateTime dm; string date = DateTime.TryParseExact(m.Date, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dm) ? dm.ToString("dd/MM/yyyy HH:mm") : m.Date; return new[] { date, Langue.T(m.Titre), m.Annule ? "Annulée" : "Active" }; }, null, o => ((Modif)o).Annule);
                bTout.IsEnabled = actives > 0;
                tModifs.Text = liste.Count == 0 ? Langue.T("Modifications réversibles : aucune pour l'instant") : Langue.F("Modifications réversibles : {0} active(s)", actives);
            }));
        }

        private void Annuler(List<string> ids, bool tout)
        {
            Action faire = () =>
            {
                var liste = Historique.Lire();
                if (tout) ids = liste.Where(m => !m.Annule).Select(m => m.Id).ToList();
                int ok = 0; var echecs = new List<string>();
                foreach (var m in liste)
                {
                    if (!ids.Contains(m.Id) || m.Annule) continue;
                    string raison = Historique.Annuler(m);
                    if (raison != "") echecs.Add(m.Titre + " : " + raison); else { m.Annule = true; ok++; }
                }
                Historique.Ecrire(liste);
                Charger();
                F.Demander(Langue.F("{0} modification(s) annulée(s).", ok), echecs.Count > 0 ? Langue.T("Impossible d'annuler :") + "\n- " + string.Join("\n- ", echecs) : null, "Compris", null, r => { }, echecs.Count > 0);
            };
            if (!tout) { faire(); return; }
            F.Demander("Annuler toutes les modifications actives ?", "Les téléchargements reviennent à leur place, les programmes au démarrage sont réactivés et le mode d'alimentation précédent est rétabli.", "Tout annuler", "Garder", oui => { if (oui) faire(); }, true);
        }
    }

    /// <summary>Deux graphiques côte à côte : espace libéré par passage (barres), note de santé par jour (courbe).</summary>
    internal sealed class GraphiquesHistorique5 : Control
    {
        public List<Tuple<DateTime, long>> Stats = new List<Tuple<DateTime, long>>();
        public List<Tuple<DateTime, int>> Scores = new List<Tuple<DateTime, int>>();

        private static FormattedText Texte(string t, double taille, Color c, bool gras = false, bool chiffres = false)
        {
            return new FormattedText(t, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(chiffres ? Habillage.Console : gras ? Habillage.Affichage : Habillage.Corps, FontStyle.Normal, gras ? FontWeight.SemiBold : FontWeight.Normal), taille, Habillage.Pinceau(c));
        }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height;
            ctx.DrawRectangle(Habillage.Pinceau(P.Carte), new Pen(Habillage.Pinceau(P.Bordure), 1), new RoundedRect(new Rect(0.5, 0.5, w - 1, h - 1), 3));
            double demi = w / 2, haut = 66, bas = h - 34;
            ctx.DrawLine(new Pen(Habillage.Pinceau(P.Bordure), 1), new Point(demi, 18), new Point(demi, h - 18));

            // --- gauche : espace libéré par passage
            ctx.DrawText(Texte(Langue.T("Espace libéré par passage"), 15, P.Texte, true), new Point(20, 16));
            long total = Stats.Sum(s => s.Item2);
            ctx.DrawText(Texte(Langue.F("Total : {0} en {1} passage(s)", Format.Taille(total), Stats.Count), 12, P.Discret), new Point(20, 40));
            var axe = new Pen(Habillage.Pinceau(P.Bordure), 1);
            ctx.DrawLine(axe, new Point(20, bas), new Point(demi - 20, bas));
            var stats = Stats.Skip(Math.Max(0, Stats.Count - 10)).ToList();
            if (stats.Count == 0) ctx.DrawText(Texte(Langue.T("Aucun nettoyage enregistré pour l'instant."), 13, P.Discret), new Point(20, haut + 50));
            else
            {
                double max = Math.Max(1, stats.Max(s => s.Item2));
                double larg = (demi - 48) / stats.Count;
                for (int i = 0; i < stats.Count; i++)
                {
                    double hb = Math.Max(2, (bas - haut - 22) * stats[i].Item2 / max);
                    double x0 = 26 + i * larg, lb = larg * 0.6;
                    var degrade = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(P.Accent, 0), new GradientStop(Habillage.Alpha(P.Accent2, 120), 1) },
                    };
                    ctx.DrawRectangle(degrade, null, new RoundedRect(new Rect(x0, bas - hb, lb, hb), 2, 2, 0, 0));
                    var v = Texte(Courte(stats[i].Item2), 11, P.Texte, false, true);
                    if (v.Width <= larg - 2 || stats[i].Item2 >= max) ctx.DrawText(v, new Point(x0 + lb / 2 - v.Width / 2, bas - hb - 16));
                    var dt = Texte(stats[i].Item1.ToString("dd/MM"), 10.5, P.Discret, false, true);
                    ctx.DrawText(dt, new Point(x0 + lb / 2 - dt.Width / 2, bas + 7));
                }
            }

            // --- droite : note de santé (dernière note de chaque jour)
            ctx.DrawText(Texte(Langue.T("Score de santé"), 15, P.Texte, true), new Point(demi + 20, 16));
            ctx.DrawLine(axe, new Point(demi + 20, bas), new Point(w - 20, bas));
            var scores = Scores.Skip(Math.Max(0, Scores.Count - 14)).ToList();
            if (scores.Count == 0) { ctx.DrawText(Texte(Langue.T("Pas encore de score enregistré."), 13, P.Discret), new Point(demi + 20, haut + 50)); return; }
            ctx.DrawText(Texte(Langue.F("Aujourd'hui : {0} / 100", scores[scores.Count - 1].Item2), 12, P.Discret), new Point(demi + 20, 40));
            double zone = w - demi - 72, pas = scores.Count > 1 ? zone / (scores.Count - 1) : 0;
            var pts = scores.Select((s, i) => new Point(demi + 36 + i * pas, bas - (bas - haut - 14) * s.Item2 / 100.0)).ToList();
            if (pts.Count > 1)
            {
                var aire = new StreamGeometry();
                using (var g = aire.Open())
                {
                    g.BeginFigure(new Point(pts[0].X, bas), true);
                    foreach (var p in pts) g.LineTo(p);
                    g.LineTo(new Point(pts[pts.Count - 1].X, bas));
                    g.EndFigure(true);
                }
                ctx.DrawGeometry(new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Habillage.Alpha(P.Vert, 80), 0), new GradientStop(Habillage.Alpha(P.Vert, 0), 1) },
                }, null, aire);
                var courbe = new StreamGeometry();
                using (var g = courbe.Open())
                {
                    g.BeginFigure(pts[0], false);
                    for (int i = 1; i < pts.Count; i++) g.LineTo(pts[i]);
                    g.EndFigure(false);
                }
                ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(P.Vert), 2.4, lineJoin: PenLineJoin.Round), courbe);
            }
            for (int i = 0; i < pts.Count; i++)
            {
                ctx.DrawEllipse(Habillage.Pinceau(P.Vert), null, pts[i], 3.8, 3.8);
                if (i == 0 || i == pts.Count - 1 || pts.Count <= 7)
                {
                    var v = Texte(scores[i].Item2.ToString(), 11, P.Texte, false, true);
                    ctx.DrawText(v, new Point(pts[i].X - v.Width / 2, pts[i].Y - 19));
                    var dt = Texte(scores[i].Item1.ToString("dd/MM"), 10.5, P.Discret, false, true);
                    ctx.DrawText(dt, new Point(pts[i].X - dt.Width / 2, bas + 7));
                }
            }
        }

        private static string Courte(long o)
        {
            bool fr = Langue.Code == "fr" || Langue.Code == "es";
            if (o >= 1073741824L) return (o / 1073741824.0).ToString(o >= 10737418240L ? "0" : "0.#", Appli.Fr) + (fr ? " Go" : " GB");
            if (o >= 1048576L) return (o / 1048576.0).ToString("0", Appli.Fr) + (fr ? " Mo" : " MB");
            return (o / 1024.0).ToString("0", Appli.Fr) + (fr ? " Ko" : " KB");
        }
    }
}
