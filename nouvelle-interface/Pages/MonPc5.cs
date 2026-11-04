// Nettoyage PC 5 - Outils > « Mon PC » : fiche du matériel, âge du PC, état de chaque pièce et conseils d'amélioration.
// Lu en arrière-plan (quelques secondes), en lecture seule.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class PageMonPc5 : StackPanel
    {
        /// <summary>(captures) fiche d'exemple : rien n'est lu sur le PC.</summary>
        public static FicheMonPc Exemple;
        private static FicheMonPc cache;
        private readonly FenetrePrincipale F;
        private readonly TextBlock tResume;
        private readonly UniformGrid grille = new UniformGrid { Columns = 2 };
        private readonly StackPanel conseils = new StackPanel { Spacing = 8 };

        public PageMonPc5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 14; Margin = new Thickness(0, 0, 12, 0);
            var tete = new StackPanel { Spacing = 4 };
            tete.Children.Add(new TextBlock { Text = (Habillage.Hud ? "▸ " : "") + Langue.T("Fiche de ton PC").ToUpper(Langue.Culture), FontFamily = Habillage.Console, FontSize = 11, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Accent) });
            tResume = new TextBlock { Text = Langue.T("Lecture du matériel…"), FontFamily = Habillage.Affichage, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            tete.Children.Add(tResume);
            Children.Add(new Cadre5 { Child = tete, Coupe = 16, Padding = new Thickness(20, 16), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 80), BorderThickness = new Thickness(1) });
            grille.SizeChanged += (o, a) => { int c = a.NewSize.Width >= 760 ? 2 : 1; if (grille.Columns != c) grille.Columns = c; };
            Children.Add(grille);
            Children.Add(new TextBlock { Text = (Habillage.Hud ? "▸ " : "") + Langue.T("Conseils pour l'améliorer").ToUpper(Langue.Culture), FontFamily = Habillage.Console, FontSize = 11, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Accent), Margin = new Thickness(0, 6, 0, 0) });
            Children.Add(conseils);
            var fiche = Exemple ?? cache;
            if (fiche != null) Afficher(fiche);
            else
                Task.Run(() => { try { return MonPc.Lire(); } catch (Exception ex) { JournalDemarrage.Erreur("Mon PC", ex); return null; } })
                    .ContinueWith(t => Dispatcher.UIThread.Post(() => { if (t.Result != null) { cache = t.Result; Afficher(t.Result); } else tResume.Text = Langue.T("Le matériel n'a pas pu être lu sur ce PC."); }));
        }

        private void Afficher(FicheMonPc fiche)
        {
            var P = Habillage.P;
            tResume.Text = fiche.Resume;
            grille.Children.Clear();
            foreach (var p in fiche.Pieces)
            {
                var c = p.Etat == 2 ? P.Rouge : p.Etat == 1 ? P.Orange : P.Vert;
                var s = new StackPanel { Spacing = 3 };
                var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                haut.Children.Add(new TextBlock { Text = Langue.T(p.Categorie).ToUpper(Langue.Culture), FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1, Foreground = Habillage.Pinceau(P.Discret) });
                var etat = new TextBlock { Text = Langue.T(p.Etat == 2 ? "à vérifier" : p.Etat == 1 ? "à surveiller" : "bien"), FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(c) };
                Grid.SetColumn(etat, 1); haut.Children.Add(etat);
                s.Children.Add(haut);
                s.Children.Add(new TextBlock { Text = p.Nom, FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                if (p.Detail != "") s.Children.Add(new TextBlock { Text = p.Detail, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                grille.Children.Add(new Cadre5 { Child = s, Coupe = 12, Padding = new Thickness(16, 12), Margin = new Thickness(0, 0, 10, 10), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(p.Etat == 0 ? Habillage.Alpha(P.Accent, 45) : Habillage.Alpha(c, 110)), BorderThickness = new Thickness(1), TraitHaut = c });
            }
            conseils.Children.Clear();
            if (fiche.Conseils.Count == 0)
                conseils.Children.Add(new TextBlock { Text = "✔  " + Langue.T("Ton PC est bien équipé : rien à changer pour l'instant."), FontFamily = Habillage.Corps, FontSize = 14, Foreground = Habillage.Pinceau(P.Vert), TextWrapping = TextWrapping.Wrap });
            int n = 0;
            foreach (var c in fiche.Conseils)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("40,*") };
                g.Children.Add(new TextBlock { Text = "U-" + (++n).ToString("00"), FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(c.Priorite == 1 ? P.Orange : P.Accent), Margin = new Thickness(0, 2, 0, 0) });
                var t = new StackPanel { Spacing = 2 };
                t.Children.Add(new TextBlock { Text = Langue.T(c.Titre), FontFamily = Habillage.Corps, FontSize = 14.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                t.Children.Add(new TextBlock { Text = Langue.T(c.Texte), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                Grid.SetColumn(t, 1); g.Children.Add(t);
                conseils.Children.Add(new Border { Child = g, Padding = new Thickness(14, 12), Background = Habillage.Pinceau(P.Carte, 230), BorderBrush = Habillage.Pinceau(c.Priorite == 1 ? Habillage.Alpha(P.Orange, 90) : P.Bordure), BorderThickness = new Thickness(1) });
            }
            Habillage.Cascade(grille.Children.OfType<Control>().Concat(conseils.Children.OfType<Control>()), 40, 260);
        }
    }
}
