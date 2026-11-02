// Nettoyage PC 5 - annonce du nouveau look (version 5.5) : montrée une fois, à la première ouverture après la mise à
// jour, pour que personne ne soit perdu. Elle rappelle que rien n'a bougé ni disparu, et permet de revenir à
// l'apparence d'avant en un clic (aussi dans Réglages, « Style de l'interface »).
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NettoyagePC.Nouveau
{
    internal static class Annonce5
    {
        /// <summary>À l'ouverture : vrai si l'annonce a été montrée (les nouveautés habituelles ne s'affichent pas en plus).</summary>
        public static bool SiBesoin(FenetrePrincipale f, bool profilCree)
        {
            Parametres p;
            try { p = Parametres.Lire(); } catch { return false; }
            if (p.AnnonceHudVue) return false;
            p.AnnonceHudVue = true;
            // nouvel utilisateur : il découvre directement le nouveau look, rien à annoncer
            if (!profilCree) { try { p.Ecrire(); } catch { } return false; }
            p.DerniereVersionLancee = Appli.Version;   // l'annonce remplace la fenêtre des nouveautés de cette version
            try { p.Ecrire(); } catch { }
            Ouvrir(f);
            return true;
        }

        public static void Ouvrir(FenetrePrincipale f)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 16 };
            var tete = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            try { tete.Children.Add(new Image { Source = new Bitmap(AssetLoader.Open(new Uri("avares://NettoyagePC/Assets/logo.png"))), Width = 52, Height = 52, VerticalAlignment = VerticalAlignment.Center }); } catch { }
            var titres = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            titres.Children.Add(new TextBlock { Text = Langue.F("NOUVEAU · VERSION {0}", Appli.Version), FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(P.Accent) });
            titres.Children.Add(new TextBlock { Text = Langue.T("Nettoyage PC change de look et de logo"), FontFamily = Habillage.Affichage, FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
            tete.Children.Add(titres);
            s.Children.Add(tete);
            s.Children.Add(new TextBlock
            {
                Text = Langue.T("Tout ce que tu utilisais est toujours là, au même endroit : rien n'a été supprimé et tes réglages sont gardés."),
                FontFamily = Habillage.Corps, FontSize = 14.5, LineHeight = 22, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap,
            });
            string[][] points =
            {
                new[] { "Les rubriques n'ont pas bougé", "Nettoyer, Accélérer, Protéger, Outils et Premium sont toujours à gauche, dans le même ordre." },
                new[] { "Tes favoris et tes réglages sont gardés", "Tes favoris sont sur l'accueil, avec leur bulle verte quand ils sont activés." },
                new[] { "Tu ne trouves pas un outil ?", "Appuie sur Ctrl+K et tape son nom : il s'ouvre directement." },
            };
            for (int i = 0; i < points.Length; i++)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("38,*") };
                g.Children.Add(new TextBlock { Text = (i + 1).ToString("00"), FontFamily = Habillage.Console, FontSize = 13, Foreground = Habillage.Pinceau(P.Accent), Margin = new Thickness(0, 2, 0, 0) });
                var t = new StackPanel { Spacing = 2 };
                t.Children.Add(new TextBlock { Text = Langue.T(points[i][0]), FontFamily = Habillage.Corps, FontSize = 14.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                t.Children.Add(new TextBlock { Text = Langue.T(points[i][1]), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(t, 1); g.Children.Add(t);
                s.Children.Add(new Cadre5 { Child = g, Coupe = 10, Padding = new Thickness(14, 12), Background = Habillage.Pinceau(P.Accent, P.Sombre ? 12 : 10), BorderBrush = Habillage.Pinceau(P.Accent, 55), BorderThickness = new Thickness(1) });
            }
            var boutons = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            FenetreInterieure fen = null;
            var ok = new BoutonNoyau("C'est parti", true, 170, 44) { TaillePolice = 15, Margin = new Thickness(0, 0, 12, 8) };
            ok.Clic += () => { if (fen != null) fen.Fermer(); };
            var ancien = new BoutonNoyau("Revenir à l'ancien look", false, 220, 44) { TaillePolice = 14, Margin = new Thickness(0, 0, 12, 8) };
            ancien.Clic += () =>
            {
                if (fen != null) fen.Fermer();
                f.ChangerStyle(false);
                f.Statut("Apparence classique remise. Le nouveau look reste disponible dans Réglages, « Style de l'interface ».");
            };
            boutons.Children.Add(ok); boutons.Children.Add(ancien);
            s.Children.Add(boutons);
            s.Children.Add(new TextBlock { Text = Langue.T("Tu pourras changer d'avis à tout moment : Réglages, « Style de l'interface »."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            fen = f.Ouvrir("Nouveau look, mêmes outils", s, 720);
        }
    }
}
