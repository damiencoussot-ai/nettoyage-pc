// Velox 5 - annonce du nouveau nom (version 5.7) : « Nettoyage PC » devient « Velox ». Montrée une fois, à la
// première ouverture après la mise à jour, pour que personne ne croie avoir installé un autre logiciel :
// même éditeur, même programme, rien n'est perdu (réglages, historique, Premium).
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NettoyagePC.Nouveau
{
    internal static class AnnonceVelox5
    {
        /// <summary>À l'ouverture : vrai si l'annonce a été montrée (les nouveautés habituelles ne s'affichent pas en plus).</summary>
        public static bool SiBesoin(FenetrePrincipale f, bool profilCree)
        {
            Parametres p;
            try { p = Parametres.Lire(); } catch { return false; }
            if (p.AnnonceVeloxVue) return false;
            p.AnnonceVeloxVue = true;
            // nouvel utilisateur : il a installé Velox directement, rien à annoncer
            if (!profilCree) { try { p.Ecrire(); } catch { } return false; }
            try { p.Ecrire(); } catch { }
            Ouvrir(f);
            return true;
        }

        public static void Ouvrir(FenetrePrincipale f)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 16 };
            var tete = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            try { tete.Children.Add(new Image { Source = new Bitmap(AssetLoader.Open(new Uri("avares://NettoyagePC/Assets/logo.png"))), Width = 56, Height = 56, VerticalAlignment = VerticalAlignment.Center }); } catch { }
            var titres = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Spacing = 2 };
            titres.Children.Add(new TextBlock { Text = Langue.T("NOUVEAU NOM"), FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(P.Accent) });
            titres.Children.Add(new TextBlock { Text = Langue.T("Nettoyage PC s'appelle maintenant Velox"), FontFamily = Habillage.Affichage, FontSize = 21, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
            tete.Children.Add(titres);
            s.Children.Add(tete);
            s.Children.Add(new TextBlock
            {
                Text = Langue.T("Velox veut dire « rapide » en latin. C'est le même logiciel, du même éditeur : seul le nom change."),
                FontFamily = Habillage.Corps, FontSize = 14.5, LineHeight = 22, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap,
            });
            string[][] points =
            {
                new[] { "Rien n'est perdu", "Tes réglages, ton historique, tes favoris et l'entretien automatique sont gardés." },
                new[] { "Premium reste actif", "Ta clé Premium marche toujours, sans rien faire." },
                new[] { "Des raccourcis renommés", "Le raccourci du menu Démarrer et celui du Bureau s'appellent maintenant « Velox »." },
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
            FenetreInterieure fen = null;
            var ok = new BoutonNoyau("Compris, en avant !", true, 0, 44) { TaillePolice = 15, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
            ok.Clic += () => { if (fen != null) fen.Fermer(); };
            s.Children.Add(ok);
            fen = f.Ouvrir("Nettoyage PC devient Velox", s, 700);
        }
    }
}
