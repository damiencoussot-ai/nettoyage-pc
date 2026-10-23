// Nettoyage PC 5 - accueil : l'anneau, l'état en une phrase, les deux boutons, les mesures et la liste.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    public sealed class PageAccueil : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Anneau anneau = new Anneau(300);
        private readonly StackPanel liste = new StackPanel { Spacing = 8 };
        private Bilan bilan;
        private bool okVisibles;

        public PageAccueil(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Margin = new Thickness(16, 4, 28, 24);
            Spacing = 18;
            bilan = f.Source.Calculer();

            // ---- haut : anneau + texte
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*") };
            var zoneAnneau = new Panel { Width = 320, Height = 320 };
            anneau.HorizontalAlignment = HorizontalAlignment.Center; anneau.VerticalAlignment = VerticalAlignment.Center;
            zoneAnneau.Children.Add(anneau);
            string[] noms = { "STOCKAGE", "MÉMOIRE", "SÉCURITÉ", "MATÉRIEL" };
            var coins = new[] { (HorizontalAlignment.Right, VerticalAlignment.Top), (HorizontalAlignment.Right, VerticalAlignment.Bottom), (HorizontalAlignment.Left, VerticalAlignment.Bottom), (HorizontalAlignment.Left, VerticalAlignment.Top) };
            for (int k = 0; k < 4; k++)
            {
                double v = bilan.Secteurs[k];
                var bloc = new StackPanel { HorizontalAlignment = coins[k].Item1, VerticalAlignment = coins[k].Item2, Margin = new Thickness(6) };
                var align = coins[k].Item1 == HorizontalAlignment.Right ? TextAlignment.Right : TextAlignment.Left;
                bloc.Children.Add(new TextBlock { Text = noms[k], FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(v >= 0 && v < 80 ? Habillage.Niveau(v) : P.Discret), TextAlignment = align });
                bloc.Children.Add(new TextBlock { Text = v >= 0 ? Math.Round(v) + " %" : "—", FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(v >= 0 && v < 80 ? Habillage.Niveau(v) : P.Texte), TextAlignment = align });
                zoneAnneau.Children.Add(bloc);
            }
            haut.Children.Add(zoneAnneau);

            var texte = new StackPanel { Spacing = 6, Margin = new Thickness(18, 22, 0, 0), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Left };
            Color couleurEtat;
            string etat = Textes.EtatCourt(bilan, out couleurEtat);
            texte.Children.Add(new TextBlock { Text = Textes.Salut(f.Pseudo, DateTime.Now), FontFamily = Habillage.Affichage, FontSize = 28, Foreground = Habillage.Pinceau(P.Texte) });
            texte.Children.Add(new TextBlock { Text = etat, FontFamily = Habillage.Corps, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(couleurEtat) });
            texte.Children.Add(new TextBlock { Text = Textes.EtatDetail(bilan), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };
            boutons.Children.Add(new BoutonNoyau(f.Simulation ? "Simuler en 1 clic" : "Optimiser en 1 clic", true, 240, 48) { TaillePolice = 16 });
            boutons.Children.Add(new BoutonNoyau("Analyser sans rien modifier", false, 240, 48));
            texte.Children.Add(boutons);
            var mesures = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            mesures.Children.Add(Mesure("PROCESSEUR", bilan.Processeur >= 0 ? Math.Round(bilan.Processeur) + " %" : "…"));
            mesures.Children.Add(Mesure("MÉMOIRE VIVE", bilan.Memoire >= 0 ? Math.Round(bilan.Memoire) + " %" : "…"));
            mesures.Children.Add(Mesure("DISQUE " + bilan.NomDisque, bilan.DisqueLibre != "" ? bilan.DisqueLibre : "…"));
            if (bilan.Temperature > 0) mesures.Children.Add(Mesure("TEMPÉRATURE", Math.Round(bilan.Temperature) + " °C"));
            texte.Children.Add(mesures);
            Grid.SetColumn(texte, 1);
            haut.Children.Add(texte);
            Children.Add(haut);

            // ---- liste des améliorations
            var titreListe = new TextBlock { Text = "Améliorations proposées", FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), Margin = new Thickness(12, 0, 0, -6) };
            Children.Add(titreListe);
            Children.Add(liste);
            RemplirListe();
            AttachedToVisualTree += (s, e) => anneau.Montrer(bilan);
        }

        private static Control Mesure(string titre, string valeur)
        {
            var P = Habillage.P;
            var b = new Border
            {
                Width = 156, Height = 64, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(3),
                Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1),
            };
            var s = new StackPanel { Spacing = 4 };
            s.Children.Add(new TextBlock { Text = titre, FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(P.Discret) });
            s.Children.Add(new TextBlock { Text = valeur, FontFamily = Habillage.Affichage, FontSize = 18, Foreground = Habillage.Pinceau(P.Texte), TextTrimming = TextTrimming.CharacterEllipsis });
            b.Child = s;
            return b;
        }

        private void RemplirListe()
        {
            liste.Children.Clear();
            var aFaire = bilan.Points.Where(p => p.Etat != EtatPoint.Ok).OrderBy(p => p.Etat == EtatPoint.Probleme ? 0 : 1).ToList();
            var bons = bilan.Points.Where(p => p.Etat == EtatPoint.Ok).ToList();
            foreach (var p in aFaire) liste.Children.Add(Ligne(p));
            if (bons.Count > 0)
            {
                string t = aFaire.Count == 0 ? "Tout va bien : " + bons.Count + " points vérifiés." : bons.Count + " autres points vérifiés : tout va bien.";
                var ligne = Ligne(new PointBilan(EtatPoint.Ok, t, okVisibles ? "Masquer la liste" : "Voir la liste"), () => { okVisibles = !okVisibles; RemplirListe(); });
                liste.Children.Add(ligne);
                if (okVisibles) foreach (var p in bons) liste.Children.Add(Ligne(p));
            }
        }

        private static Control Ligne(PointBilan p, Action clic = null)
        {
            var P = Habillage.P;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("34,*,Auto"), Height = 46 };
            Color c = p.Etat == EtatPoint.Probleme ? P.Rouge : p.Etat == EtatPoint.Attention ? P.Orange : P.Vert;
            string sym = p.Etat == EtatPoint.Probleme ? "✖" : p.Etat == EtatPoint.Attention ? "!" : "✔";
            g.Children.Add(new TextBlock { Text = sym, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(c), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            var t = new TextBlock { Text = p.Texte, FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Avalonia.Controls.ToolTip.SetTip(t, p.Texte);
            Grid.SetColumn(t, 1); g.Children.Add(t);
            if (p.Bouton != null)
            {
                var b = new BoutonNoyau(p.Bouton, p.Etat == EtatPoint.Probleme, 160, 30) { TaillePolice = 13, Margin = new Thickness(12, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
                if (clic != null) b.Clic += clic;
                Grid.SetColumn(b, 2); g.Children.Add(b);
            }
            return new Border { Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Child = g };
        }
    }
}
