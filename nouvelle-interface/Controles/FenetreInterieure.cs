// Nettoyage PC 5 - fenêtre intérieure : fond assombri, carte au centre avec un titre, le contenu et « Fermer ».
// Remplace les petites fenêtres Windows de la version 4 (bouclier, réglages...) : tout reste dans la fenêtre.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    public sealed class FenetreInterieure : Panel
    {
        private readonly FenetrePrincipale F;
        public event Action Fermee;

        public FenetreInterieure(FenetrePrincipale f, string titre, Control contenu, double largeur)
        {
            F = f;
            var P = Habillage.P;
            Focusable = true;
            Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(0, 0, 0) : Color.FromRgb(10, 20, 30), P.Sombre ? 150 : 90);
            var carte = new Border
            {
                MaxWidth = largeur, Margin = new Thickness(28), CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(9, 17, 31) : Color.FromRgb(250, 252, 253)),
                BorderBrush = Habillage.Pinceau(P.Accent, 120), BorderThickness = new Thickness(1),
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 44, OffsetY = 12, Color = Habillage.Alpha(Colors.Black, P.Sombre ? 170 : 60) }),
            };
            var d = new DockPanel { LastChildFill = true };
            var tete = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(26, 20, 18, 4) };
            var bloc = new StackPanel { Spacing = 8 };
            bloc.Children.Add(new Border { Height = 2, Width = 48, HorizontalAlignment = HorizontalAlignment.Left, Background = Habillage.DegradeAccent() });
            bloc.Children.Add(new TextBlock { Text = Langue.T(titre), FontFamily = Habillage.Affichage, FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            tete.Children.Add(bloc);
            var fermer = new BoutonNoyau("Fermer", false, 96, 32) { TaillePolice = 13, VerticalAlignment = VerticalAlignment.Top };
            fermer.Clic += Fermer;
            Grid.SetColumn(fermer, 1); tete.Children.Add(fermer);
            DockPanel.SetDock(tete, Dock.Top);
            d.Children.Add(tete);
            d.Children.Add(new ScrollViewer { Content = contenu, Padding = new Thickness(26, 10, 26, 24), VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
            carte.Child = d;
            Children.Add(carte);
            AttachedToVisualTree += (o, e) =>
            {
                Focus();
                if (!Habillage.Animations) return;
                Opacity = 0;
                long t0 = Horloge.Maintenant;
                Horloge.Lancer(t => { double p = Math.Min(1, (t - t0) / 150.0); Opacity = Habillage.Douce(p); return p < 1; });
            };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) { e.Handled = true; Fermer(); }
        }

        public void Fermer()
        {
            F.Retirer(this);
            var h = Fermee; if (h != null) h();
        }
    }
}
