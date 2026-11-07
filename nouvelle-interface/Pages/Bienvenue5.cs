// Velox 5 - premier lancement, en 3 étapes : 1. « Faisons connaissance » (pseudo facultatif, mode simple ou complet),
// 2. le profil (Gamer, Travail, Famille), 3. la première analyse (rien n'est modifié) dont le résultat s'affiche
// sur l'accueil. Tout reste sur ce PC. On peut passer chaque étape.
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
        private Border carteSimple, carteComplet;
        private readonly Border[] cartesProfil = new Border[3];
        private static readonly string[] ClesProfil = { "gamer", "bureautique", "famille" };
        private bool simple;
        private string profil = "";
        private int etape = 1;
        private FenetreInterieure fenetre;
        private readonly StackPanel contenu = new StackPanel { Spacing = 12 };
        private readonly TextBlock tEtape;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var b = new Bienvenue5(f);
            b.fenetre = f.Ouvrir("Faisons connaissance", b, 760);
            b.fenetre.Fermee += () => { if (!b.fini) f.EnregistrerReglage(r => r.ProfilCree = true); };
        }

        private bool fini;

        private Bienvenue5(FenetrePrincipale f)
        {
            F = f;
            Spacing = 10;
            tEtape = new TextBlock { FontFamily = Habillage.Console, FontSize = 12, LetterSpacing = 1, Foreground = Habillage.Pinceau(Habillage.P.Accent) };
            Children.Add(tEtape);
            Children.Add(contenu);
            pseudo.Text = f.Pseudo ?? "";
            Etape1();
        }

        private void Entete(string texte)
        {
            tEtape.Text = Langue.F("ÉTAPE {0} / 3", etape);
            contenu.Children.Clear();
            contenu.Children.Add(new TextBlock { Text = Langue.T(texte), FontFamily = Habillage.Corps, FontSize = 14, Foreground = Habillage.Pinceau(Habillage.P.Discret), TextWrapping = TextWrapping.Wrap });
        }

        private void Boutons(string suivant, Action faire, string passer, Action sauter)
        {
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 10, 0, 0) };
            var go = new BoutonNoyau(suivant, true, 0, 42) { TaillePolice = 15 };
            go.Clic += faire;
            var pas = new BoutonNoyau(passer, false, 0, 42) { TaillePolice = 13.5 };
            pas.Clic += sauter;
            boutons.Children.Add(go); boutons.Children.Add(pas);
            contenu.Children.Add(boutons);
        }

        // ---- 1. pseudo et mode
        private void Etape1()
        {
            etape = 1;
            Entete("Bienvenue dans Velox. Trois petites étapes, et c'est parti. Tout reste sur ce PC : pas de compte, pas de mot de passe.");
            contenu.Children.Add(Titre("Comment veux-tu qu'on t'appelle ? (facultatif)"));
            pseudo.Watermark = Langue.T("Ton pseudo");
            contenu.Children.Add(pseudo);
            contenu.Children.Add(Titre("Comment veux-tu utiliser Velox ?"));
            var choix = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 };
            carteSimple = Carte("Simple", "Trois gros boutons : Nettoyer, Vérifier, Appeler à l'aide. Idéal pour débuter ou pour un proche.", () => Choisir(true));
            carteComplet = Carte("Complet", "Toutes les rubriques et les options : nettoyage, accélération, sécurité, outils. Chaque action te demande ton accord avant de se lancer.", () => Choisir(false));
            choix.Children.Add(carteSimple); choix.Children.Add(carteComplet);
            contenu.Children.Add(choix);
            Choisir(simple);
            Boutons("Suivant", () =>
            {
                string p = Profil.Nettoyer(pseudo.Text);
                F.Pseudo = p;
                F.EnregistrerReglage(r => r.Pseudo = p);
                if (simple) Etape3(); else Etape2();
            }, "Passer cette étape", () => { fini = true; F.EnregistrerReglage(r => r.ProfilCree = true); fenetre.Fermer(); });
        }

        // ---- 2. profil
        private void Etape2()
        {
            etape = 2;
            Entete("Pour quoi utilises-tu surtout ce PC ? Velox met en avant les bons outils (tu pourras changer à tout moment, en bas à gauche).");
            var choix = new Avalonia.Controls.Primitives.UniformGrid { Columns = 3 };
            string[][] p =
            {
                new[] { "Gamer", "Jouer : mode jeu, preuve en FPS, températures, niveaux et succès, look néon." },
                new[] { "Travail", "Un PC propre, à jour et sauvegardé : nettoyage rapide, mises à jour, sauvegarde." },
                new[] { "Famille", "Sécurité d'abord : bouclier anti-arnaque, antivirus, aide à distance pour un proche." },
            };
            for (int i = 0; i < 3; i++) { int k = i; cartesProfil[i] = Carte(p[i][0], p[i][1], () => ChoisirProfil(ClesProfil[k])); choix.Children.Add(cartesProfil[i]); }
            contenu.Children.Add(choix);
            ChoisirProfil(profil);
            Boutons("Suivant", Etape3, "Je choisirai plus tard", () => { profil = ""; Etape3(); });
        }

        // ---- 3. première analyse
        private void Etape3()
        {
            etape = 3;
            Entete("Dernière étape : on regarde l'état de ton PC. L'analyse ne modifie rien : elle montre ce qui peut être nettoyé ou amélioré, et tu décides ensuite.");
            contenu.Children.Add(new TextBlock { Text = Langue.T("Environ 30 secondes. Le résultat s'affiche sur l'accueil : l'indice de ton PC sur 100 et l'espace que tu peux récupérer."), FontFamily = Habillage.Corps, FontSize = 14, Foreground = Habillage.Pinceau(Habillage.P.Texte), TextWrapping = TextWrapping.Wrap });
            Boutons("Lancer l'analyse", () => Terminer(true), "Plus tard", () => Terminer(false));
        }

        private void Terminer(bool analyser)
        {
            fini = true;
            F.EnregistrerReglage(r => r.ProfilCree = true);
            fenetre.Fermer();
            // le profil est appliqué une fois la fenêtre fermée (le profil Gamer reconstruit l'interface)
            if (!simple && profil != "") Profils5.Appliquer(F, profil);
            if (simple) F.AppliquerModeSimple(true);
            else F.Afficher("Accueil", false);
            if (analyser && !simple) F.LancerAction("analyse");
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

        private static void Marquer(Border b, bool choisi)
        {
            var P = Habillage.P;
            b.BorderBrush = Habillage.Pinceau(choisi ? P.Accent : P.Bordure);
            b.Background = Habillage.Pinceau(choisi ? Habillage.Melanger(P.Carte, P.Accent, 0.08) : P.Carte);
        }

        private void Choisir(bool s) { simple = s; Marquer(carteSimple, s); Marquer(carteComplet, !s); }

        private void ChoisirProfil(string cle)
        {
            profil = cle;
            for (int i = 0; i < 3; i++) Marquer(cartesProfil[i], ClesProfil[i] == cle);
        }
    }
}
