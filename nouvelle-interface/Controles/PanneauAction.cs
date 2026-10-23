// Nettoyage PC 5 - panneau de l'action en cours, en bas de la fenêtre : titre, barre de progression lumineuse,
// étape en cours, détail repliable (les lignes du moteur), puis le résumé à la fin. « Arrêter » à tout moment.
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal sealed class PanneauAction : Border
    {
        private readonly FenetrePrincipale F;
        private readonly ServiceMoteur M;
        private readonly TextBlock tTitre, tEtape, tPourcent, tDetail;
        private readonly StackPanel resume;
        private readonly BarreProgression barre = new BarreProgression();
        private readonly BoutonNoyau bArreter, bDetail, bFermer;
        private readonly ScrollViewer defilement;
        private readonly Border cadreDetail;
        private readonly Border pointEtat;
        private static bool detailOuvert;

        public PanneauAction(FenetrePrincipale f, ServiceMoteur m)
        {
            F = f; M = m;
            var P = Habillage.P;
            Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(9, 16, 29) : Colors.White);
            BorderBrush = Habillage.Pinceau(P.Accent, 90); BorderThickness = new Thickness(0, 1, 0, 0);
            Padding = new Thickness(20, 14, 16, 14);
            var pile = new StackPanel { Spacing = 8 };

            var ligne1 = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            pointEtat = new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
            ligne1.Children.Add(pointEtat);
            tTitre = new TextBlock { FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(tTitre, 1); ligne1.Children.Add(tTitre);
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            bDetail = new BoutonNoyau(detailOuvert ? "Masquer le détail" : "Voir le détail", false, 150, 30) { TaillePolice = 12.5 };
            bDetail.Clic += () => { detailOuvert = !detailOuvert; bDetail.Texte = detailOuvert ? "Masquer le détail" : "Voir le détail"; bDetail.InvalidateVisual(); Rafraichir(); };
            bArreter = new BoutonNoyau("Arrêter", false, 100, 30) { TaillePolice = 12.5 };
            bArreter.Clic += Arreter;
            bFermer = new BoutonNoyau("Fermer", true, 100, 30) { TaillePolice = 12.5 };
            bFermer.Clic += () => F.FermerPanneauAction();
            boutons.Children.Add(bDetail); boutons.Children.Add(bArreter); boutons.Children.Add(bFermer);
            Grid.SetColumn(boutons, 2); ligne1.Children.Add(boutons);
            pile.Children.Add(ligne1);

            var ligne2 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,54") };
            barre.Height = 6; barre.VerticalAlignment = VerticalAlignment.Center;
            ligne2.Children.Add(barre);
            tPourcent = new TextBlock { FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tPourcent, 1); ligne2.Children.Add(tPourcent);
            pile.Children.Add(ligne2);

            tEtape = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextTrimming = TextTrimming.CharacterEllipsis };
            pile.Children.Add(tEtape);
            resume = new StackPanel { Spacing = 3 };
            pile.Children.Add(resume);

            tDetail = new SelectableTextBlock { FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            defilement = new ScrollViewer
            {
                Content = tDetail, MaxHeight = 220, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(10, 8), Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(5, 9, 17) : Color.FromRgb(244, 247, 250)),
            };
            cadreDetail = new Border { Child = defilement, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3) };
            pile.Children.Add(cadreDetail);
            Child = pile;

            AttachedToVisualTree += (s, e) => { M.Change += Rafraichir; Rafraichir(); };
            DetachedFromVisualTree += (s, e) => M.Change -= Rafraichir;
        }

        private void Arreter()
        {
            bArreter.IsEnabled = false;
            F.Statut("Arrêt en cours…");
            M.Arreter(ok =>
            {
                bArreter.IsEnabled = true;
                F.Statut(ok ? "Action arrêtée." : "Arrêt non autorisé : l'action continue.", !ok);
            });
        }

        private void Rafraichir()
        {
            var P = Habillage.P;
            bool enCours = M.EnCours && !M.Termine;
            tTitre.Text = M.EnPreparation ? "Autorisation demandée à Windows : " + M.Titre
                : enCours ? "En cours : " + M.Titre
                : M.Arrete ? "Arrêtée : " + M.Titre : "Terminé : " + M.Titre;
            Color c = enCours ? P.Accent : M.Arrete ? P.Orange : P.Vert;
            pointEtat.Background = Habillage.Pinceau(c);
            bArreter.IsVisible = enCours;
            bFermer.IsVisible = !enCours;
            double v = M.Total > 0 && M.Fait >= 0 ? Math.Min(1, M.Fait / M.Total) : -1;
            barre.Valeur = enCours ? v : 1;
            barre.EnCours = enCours;
            barre.Couleur = M.Arrete ? P.Orange : enCours ? P.Accent : P.Vert;
            tPourcent.Text = enCours ? (v >= 0 ? Math.Round(v * 100) + " %" : "…") : M.Arrete ? "" : "100 %";
            if (M.EnPreparation) tEtape.Text = "Clique sur « Oui » dans la fenêtre de Windows pour continuer.";
            else if (enCours) tEtape.Text = M.DerniereEtape != "" ? M.DerniereEtape : (M.Lignes.LastOrDefault(l => l.Trim() != "") ?? "Démarrage…");
            else tEtape.Text = M.Simulation && !M.Arrete ? "Simulation : rien n'a été modifié. Voici ce qui serait fait :" : M.Arrete ? "L'action a été arrêtée avant la fin." : "";
            tEtape.IsVisible = tEtape.Text != "";
            resume.Children.Clear();
            if (!enCours)
                foreach (var l in M.Resume.Take(5))
                    resume.Children.Add(new TextBlock { Text = "•  " + l, FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
            cadreDetail.IsVisible = detailOuvert;
            if (detailOuvert)
            {
                // les 600 dernières lignes suffisent (le journal complet est enregistré par le moteur)
                var lignes = M.Lignes.Count > 600 ? M.Lignes.Skip(M.Lignes.Count - 600) : M.Lignes;
                tDetail.Text = string.Join("\n", lignes);
                defilement.ScrollToEnd();
            }
        }
    }

    /// <summary>Barre de progression fine et lumineuse ; sans valeur connue, un éclat la parcourt en boucle.</summary>
    public sealed class BarreProgression : Control
    {
        private double valeur = -1, affichee;
        private bool enCours;
        private long debut = Horloge.Maintenant;
        public Color Couleur = Habillage.P.Accent;

        public double Valeur
        {
            get { return valeur; }
            set
            {
                valeur = value;
                if (value < 0) { InvalidateVisual(); return; }
                double depart = affichee; long t0 = Horloge.Maintenant;
                if (!Habillage.Animations) { affichee = value; InvalidateVisual(); return; }
                Horloge.Lancer(t => { double p = Math.Min(1, (t - t0) / 300.0); affichee = depart + (valeur - depart) * Habillage.Douce(p); InvalidateVisual(); return p < 1; });
            }
        }

        public bool EnCours
        {
            get { return enCours; }
            set
            {
                if (enCours == value) return;
                enCours = value;
                if (value && Habillage.Animations) { debut = Horloge.Maintenant; Horloge.Lancer(t => { InvalidateVisual(); return enCours; }); }
                InvalidateVisual();
            }
        }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height;
            var r = new RoundedRect(new Rect(0, 0, w, h), h / 2);
            ctx.DrawRectangle(Habillage.Pinceau(Couleur, P.Sombre ? 34 : 40), null, r);
            if (valeur >= 0 || !enCours)
            {
                double l = Math.Max(h, w * Math.Max(0, Math.Min(1, enCours ? affichee : 1)));
                var plein = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Habillage.Melanger(Couleur, P.Accent2, enCours ? 0.5 : 0), 0), new GradientStop(Couleur, 1) },
                };
                ctx.DrawRectangle(plein, null, new RoundedRect(new Rect(0, 0, l, h), h / 2));
                // halo au bout de la barre
                if (enCours) ctx.DrawEllipse(Habillage.Pinceau(Couleur, 90), null, new Point(l - h / 2, h / 2), h * 1.4, h * 1.4);
            }
            else
            {
                // valeur inconnue : un segment lumineux va et vient
                double t = ((Horloge.Maintenant - debut) % 1600) / 1600.0, seg = Math.Max(60, w * 0.22);
                double x = -seg + (w + seg) * Habillage.Douce(t);
                using (ctx.PushClip(r))
                    ctx.DrawRectangle(new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(Habillage.Alpha(Couleur, 0), 0), new GradientStop(Couleur, 0.5), new GradientStop(Habillage.Alpha(Couleur, 0), 1) },
                    }, null, new Rect(x, 0, seg, h));
            }
        }
    }
}
