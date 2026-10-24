// Nettoyage PC 5 - question posée dans la fenêtre (pas de boîte Windows grise) : fond assombri, carte au centre,
// deux boutons. Échap = non, Entrée = le bouton principal. Rien n'est lancé sans réponse.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    public sealed class Dialogue : Panel
    {
        private readonly Action<bool> repondre;
        private readonly BoutonNoyau bOui;
        private bool repondu;

        public Dialogue(string titre, string texte, string oui, string non, Action<bool> repondre, bool alerte = false)
        {
            this.repondre = repondre;
            // textes traduits ici (les appelants passent le français, parfois déjà traduit : sans effet)
            titre = Langue.T(titre); texte = string.IsNullOrEmpty(texte) ? texte : Langue.T(texte);
            oui = Langue.T(oui); non = string.IsNullOrEmpty(non) ? non : Langue.T(non);
            var P = Habillage.P;
            Focusable = true;
            Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(0, 0, 0) : Color.FromRgb(10, 20, 30), P.Sombre ? 150 : 90);
            var carte = new Border
            {
                MaxWidth = 560, Margin = new Thickness(24), Padding = new Thickness(28, 24), CornerRadius = new CornerRadius(4),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(11, 20, 36) : Colors.White),
                BorderBrush = Habillage.Pinceau(alerte ? P.Orange : P.Accent, 140), BorderThickness = new Thickness(1),
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 40, OffsetY = 10, Color = Habillage.Alpha(Colors.Black, P.Sombre ? 160 : 60) }),
            };
            var s = new StackPanel { Spacing = 14 };
            // trait lumineux en haut de la carte
            s.Children.Add(new Border { Height = 2, Width = 56, HorizontalAlignment = HorizontalAlignment.Left, Background = alerte ? Habillage.Pinceau(P.Orange) : Habillage.DegradeAccent(), Margin = new Thickness(0, 0, 0, 2) });
            s.Children.Add(new TextBlock { Text = titre, FontFamily = Habillage.Affichage, FontSize = 21, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(texte))
                s.Children.Add(new TextBlock { Text = texte, FontFamily = Habillage.Corps, FontSize = 14, LineHeight = 21, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
            if (!string.IsNullOrEmpty(non))
            {
                var bNon = new BoutonNoyau(non, false, Math.Max(120, non.Length * 8 + 40), 40);
                bNon.Clic += () => Repondre(false);
                boutons.Children.Add(bNon);
            }
            bOui = new BoutonNoyau(oui, true, Math.Max(150, oui.Length * 8.5 + 48), 40) { TaillePolice = 14.5 };
            bOui.Clic += () => Repondre(true);
            boutons.Children.Add(bOui);
            s.Children.Add(boutons);
            carte.Child = s;
            Children.Add(carte);
            // un clic sur le fond ne ferme pas : on ne lance ni n'annule rien par mégarde
            AttachedToVisualTree += (o, e) =>
            {
                bOui.Focus();
                if (!Habillage.Animations) return;
                Opacity = 0;
                long t0 = Horloge.Maintenant;
                Horloge.Lancer(t => { double p = Math.Min(1, (t - t0) / 140.0); Opacity = Habillage.Douce(p); return p < 1; });
            };
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Escape) { e.Handled = true; Repondre(false); }
        }

        private void Repondre(bool oui)
        {
            if (repondu) return;
            repondu = true;
            repondre(oui);
        }
    }
}
