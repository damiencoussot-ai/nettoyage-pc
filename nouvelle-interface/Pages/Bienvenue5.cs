// Nettoyage PC 5 - premier lancement : « Faisons connaissance » (pseudo facultatif) et le choix entre le mode simple
// (trois gros boutons) et le mode complet. Tout reste sur ce PC. On peut passer cette étape.
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal sealed class Bienvenue5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBox pseudo = new TextBox { MaxLength = 24, Width = 320, HorizontalAlignment = HorizontalAlignment.Left };
        private readonly Border carteSimple, carteComplet;
        private bool simple;
        private FenetreInterieure fenetre;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var b = new Bienvenue5(f);
            b.fenetre = f.Ouvrir("Faisons connaissance", b, 720);
            b.fenetre.Fermee += () => { if (!b.fini) f.EnregistrerReglage(r => r.ProfilCree = true); };
        }

        private bool fini;

        private Bienvenue5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(new TextBlock { Text = Langue.T("Bienvenue dans Nettoyage PC. Deux petites questions, et c'est parti. Tout reste sur ce PC : pas de compte, pas de mot de passe."), FontFamily = Habillage.Corps, FontSize = 14, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            Children.Add(Titre("Comment veux-tu qu'on t'appelle ? (facultatif)"));
            pseudo.Watermark = Langue.T("Ton pseudo");
            pseudo.Text = f.Pseudo ?? "";
            Children.Add(pseudo);
            Children.Add(Titre("Comment veux-tu utiliser Nettoyage PC ?"));
            var choix = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 };
            carteSimple = Carte("Simple", "Trois gros boutons : Nettoyer, Vérifier, Appeler à l'aide. Idéal pour débuter ou pour un proche.", () => Choisir(true));
            carteComplet = Carte("Complet", "Toutes les rubriques et les options : nettoyage, accélération, sécurité, outils. Chaque action te demande ton accord avant de se lancer.", () => Choisir(false));
            choix.Children.Add(carteSimple); choix.Children.Add(carteComplet);
            Children.Add(choix);
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
            var go = new BoutonNoyau("C'est parti", true, 160, 42) { TaillePolice = 15 };
            go.Clic += Valider;
            var passer = new BoutonNoyau("Passer cette étape", false, 170, 42) { TaillePolice = 13.5 };
            passer.Clic += () => { fini = true; F.EnregistrerReglage(r => r.ProfilCree = true); fenetre.Fermer(); };
            boutons.Children.Add(go); boutons.Children.Add(passer);
            Children.Add(boutons);
            Choisir(false);
        }

        private static TextBlock Titre(string t) { return new TextBlock { Text = Langue.T(t), FontFamily = Habillage.Affichage, FontSize = 15.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(Habillage.P.Texte), Margin = new Thickness(0, 6, 0, 0) }; }

        private static Border Carte(string titre, string texte, Action clic)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 6 };
            s.Children.Add(new TextBlock { Text = Langue.T(titre), FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            s.Children.Add(new TextBlock { Text = Langue.T(texte), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            var b = new Border { Child = s, Padding = new Thickness(18, 16), Margin = new Thickness(0, 0, 12, 0), CornerRadius = new CornerRadius(4), Background = Habillage.Pinceau(P.Carte), BorderThickness = new Thickness(1.5), Cursor = new Cursor(StandardCursorType.Hand) };
            b.PointerReleased += (o, e) => clic();
            return b;
        }

        private void Choisir(bool s)
        {
            var P = Habillage.P;
            simple = s;
            carteSimple.BorderBrush = Habillage.Pinceau(s ? P.Accent : P.Bordure);
            carteComplet.BorderBrush = Habillage.Pinceau(!s ? P.Accent : P.Bordure);
            carteSimple.Background = Habillage.Pinceau(s ? Habillage.Melanger(P.Carte, P.Accent, 0.08) : P.Carte);
            carteComplet.Background = Habillage.Pinceau(!s ? Habillage.Melanger(P.Carte, P.Accent, 0.08) : P.Carte);
        }

        private void Valider()
        {
            fini = true;
            string p = Profil.Nettoyer(pseudo.Text);
            F.Pseudo = p;
            F.EnregistrerReglage(r => { r.Pseudo = p; r.ProfilCree = true; });
            fenetre.Fermer();
            if (simple) F.AppliquerModeSimple(true);
            else F.Afficher(F.PageActive, false);
        }
    }
}
