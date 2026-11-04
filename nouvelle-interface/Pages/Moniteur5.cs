// Velox 5 - moniteur en direct (Premium) : processeur, mémoire vive, place libre sur le disque et température
// sur les 60 dernières secondes, en courbes style cockpit. Mesures en lecture seule, toutes les secondes, hors du fil
// de l'interface ; elles s'arrêtent quand la fenêtre se ferme.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class Moniteur5 : StackPanel
    {
        /// <summary>(captures) courbes d'exemple : rien n'est mesuré.</summary>
        public static bool Exemple;
        private readonly FenetrePrincipale F;
        private readonly CourbeDirect5[] courbes;
        private readonly DispatcherTimer minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private bool enCours;
        private int n;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var m = new Moniteur5(f);
            var fen = f.Ouvrir("Moniteur en direct", m, 1000);
            fen.Fermee += () => m.minuteur.Stop();
        }

        private Moniteur5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 14;
            Children.Add(new TextBlock { Text = Langue.T("Les 60 dernières secondes, mises à jour chaque seconde. Rien n'est modifié : Velox ne fait que lire les compteurs de Windows."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            courbes = new[]
            {
                new CourbeDirect5 { Titre = "PROCESSEUR", Unite = "%", Couleur = P.Accent, Max = 100 },
                new CourbeDirect5 { Titre = "MÉMOIRE VIVE", Unite = "%", Couleur = Color.FromRgb(183, 156, 255), Max = 100 },
                new CourbeDirect5 { Titre = "LIBRE SUR LE DISQUE DE WINDOWS", Unite = "%", Couleur = P.Rouge, Max = 100 },
                new CourbeDirect5 { Titre = "TEMPÉRATURE", Unite = "°C", Couleur = P.Vert, Max = 100 },
            };
            var grille = new UniformGrid { Columns = 2 };
            foreach (var c in courbes) { c.Height = 170; c.Margin = new Thickness(0, 0, 12, 12); grille.Children.Add(c); }
            Children.Add(grille);
            if (Exemple)
            {
                var r = new Random(7);
                for (int i = 0; i < 60; i++)
                {
                    courbes[0].Ajouter(14 + 10 * Math.Sin(i / 6.0) + r.Next(0, 9) + (i > 40 && i < 48 ? 35 : 0));
                    courbes[1].Ajouter(41 + 3 * Math.Sin(i / 15.0) + r.NextDouble());
                    courbes[2].Ajouter(11.2 - i * 0.004);
                    courbes[3].Ajouter(46 + 4 * Math.Sin(i / 9.0) + (i > 40 && i < 50 ? 9 : 0));
                }
                return;
            }
            minuteur.Tick += (s, e) => Mesurer();
            AttachedToVisualTree += (s, e) => { minuteur.Start(); Mesurer(); };
            DetachedFromVisualTree += (s, e) => minuteur.Stop();
        }

        private void Mesurer()
        {
            if (enCours || F.Source.Immediat) return;
            enCours = true;
            bool avecTemp = n++ % 2 == 0;   // la température est plus lente à lire
            Task.Run(() => { try { return F.Source.Mesurer(avecTemp); } catch { return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false;
                var m = t.Result;
                if (m == null) return;
                if (m.Processeur >= 0) courbes[0].Ajouter(m.Processeur);
                if (m.Memoire >= 0) courbes[1].Ajouter(m.Memoire);
                if (m.DisquePourcentLibre >= 0) courbes[2].Ajouter(m.DisquePourcentLibre);
                if (m.Temperature > 0) courbes[3].Ajouter(m.Temperature);
                else if (!avecTemp && courbes[3].Derniere.HasValue) courbes[3].Ajouter(courbes[3].Derniere.Value);
            }));
        }
    }

    /// <summary>Courbe des 60 dernières valeurs : grille, aire sous la courbe, valeur actuelle, moyenne et maximum.</summary>
    internal sealed class CourbeDirect5 : Control
    {
        public string Titre = "", Unite = "";
        public Color Couleur;
        public double Max = 100;
        private readonly List<double> valeurs = new List<double>();
        public double? Derniere { get { return valeurs.Count > 0 ? valeurs[valeurs.Count - 1] : (double?)null; } }

        public void Ajouter(double v)
        {
            valeurs.Add(v);
            while (valeurs.Count > 60) valeurs.RemoveAt(0);
            InvalidateVisual();
        }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            var r = new Rect(Bounds.Size);
            var forme = Habillage.Hud ? Habillage.Chanfrein(r.Deflate(0.5), 14) : (Geometry)new RectangleGeometry(r.Deflate(0.5));
            ctx.DrawGeometry(Habillage.Pinceau(P.Carte), new Pen(Habillage.Pinceau(Couleur, 70), 1), forme);
            ctx.DrawRectangle(Habillage.Pinceau(Couleur), null, new Rect(0, 0, Math.Max(0, r.Width - 14), 2));
            var titre = new FormattedText(Langue.T(Titre), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Console), 10.5, Habillage.Pinceau(P.Discret));
            ctx.DrawText(titre, new Point(14, 12));
            string actuel = Derniere.HasValue ? Derniere.Value.ToString("0", CultureInfo.CurrentCulture) + " " + Unite : "…";
            var grand = new FormattedText(actuel, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Affichage, FontStyle.Normal, FontWeight.SemiBold), 26, Habillage.Pinceau(P.Texte));
            ctx.DrawText(grand, new Point(14, 28));
            if (valeurs.Count > 0)
            {
                string stats = Langue.F("moy. {0} · max {1}", valeurs.Average().ToString("0", CultureInfo.CurrentCulture), valeurs.Max().ToString("0", CultureInfo.CurrentCulture)) + " " + Unite;
                var st = new FormattedText(stats, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Console), 10.5, Habillage.Pinceau(Couleur));
                ctx.DrawText(st, new Point(r.Width - st.Width - 14, 14));
            }
            // zone de la courbe
            var z = new Rect(14, 68, r.Width - 28, r.Height - 82);
            if (z.Width <= 10 || z.Height <= 10) return;
            var trait = new Pen(Habillage.Pinceau(P.Accent, 22), 1);
            for (int i = 0; i <= 4; i++) { double y = z.Top + z.Height * i / 4; ctx.DrawLine(trait, new Point(z.Left, y), new Point(z.Right, y)); }
            for (int i = 0; i <= 6; i++) { double x = z.Left + z.Width * i / 6; ctx.DrawLine(trait, new Point(x, z.Top), new Point(x, z.Bottom)); }
            if (valeurs.Count < 2) return;
            Func<int, Point> pt = i => new Point(z.Right - (valeurs.Count - 1 - i) * z.Width / 59.0, z.Bottom - Math.Max(0, Math.Min(1, valeurs[i] / Max)) * z.Height);
            var ligne = new StreamGeometry();
            var aire = new StreamGeometry();
            using (var g = ligne.Open())
            {
                g.BeginFigure(pt(0), false);
                for (int i = 1; i < valeurs.Count; i++) g.LineTo(pt(i));
                g.EndFigure(false);
            }
            using (var g = aire.Open())
            {
                g.BeginFigure(new Point(pt(0).X, z.Bottom), true);
                for (int i = 0; i < valeurs.Count; i++) g.LineTo(pt(i));
                g.LineTo(new Point(pt(valeurs.Count - 1).X, z.Bottom));
                g.EndFigure(true);
            }
            var degrade = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Habillage.Alpha(Couleur, 70), 0), new GradientStop(Habillage.Alpha(Couleur, 0), 1) },
            };
            ctx.DrawGeometry(degrade, null, aire);
            ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(Couleur), 2, lineJoin: PenLineJoin.Round), ligne);
            var fin = pt(valeurs.Count - 1);
            ctx.DrawEllipse(Habillage.Pinceau(Couleur), null, fin, 3.5, 3.5);
            ctx.DrawEllipse(Habillage.Pinceau(Couleur, 60), null, fin, 8, 8);
        }
    }
}
