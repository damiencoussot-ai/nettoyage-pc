// Nettoyage PC 5 - accueil : l'anneau, l'état en une phrase, les deux boutons, les mesures et la liste.
// Le bilan arrive en arrière-plan (l'anneau tourne en attendant) ; les mesures se rafraîchissent toutes les deux secondes.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    public sealed class PageAccueil : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Anneau anneau = new Anneau(300);
        private readonly StackPanel liste = new StackPanel { Spacing = 8 };
        private readonly TextBlock tEtat, tDetail, tTitreListe;
        private readonly StackPanel[] coins = new StackPanel[4];
        private readonly UniformGrid mesures;
        private readonly TextBlock[] valeurs = new TextBlock[4];
        private readonly TextBlock tTitreTemperature;
        private readonly Border tuileTemperature;
        private readonly BoutonNoyau bActualiser;
        private readonly DispatcherTimer minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private Bilan bilan;
        private bool okVisibles, mesureEnCours;
        private int compteur;

        public PageAccueil(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Margin = new Thickness(16, 4, 28, 24);
            Spacing = 18;

            // ---- haut : anneau + texte
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("320,*") };
            var zoneAnneau = new Panel { Width = 320, Height = 320 };
            anneau.HorizontalAlignment = HorizontalAlignment.Center; anneau.VerticalAlignment = VerticalAlignment.Center;
            zoneAnneau.Children.Add(anneau);
            var places = new[] { (HorizontalAlignment.Right, VerticalAlignment.Top), (HorizontalAlignment.Right, VerticalAlignment.Bottom), (HorizontalAlignment.Left, VerticalAlignment.Bottom), (HorizontalAlignment.Left, VerticalAlignment.Top) };
            for (int k = 0; k < 4; k++)
            {
                coins[k] = new StackPanel { HorizontalAlignment = places[k].Item1, VerticalAlignment = places[k].Item2, Margin = new Thickness(6) };
                zoneAnneau.Children.Add(coins[k]);
            }
            haut.Children.Add(zoneAnneau);

            var texte = new StackPanel { Spacing = 6, Margin = new Thickness(18, 22, 0, 0), MaxWidth = 720, HorizontalAlignment = HorizontalAlignment.Stretch };
            texte.Children.Add(new TextBlock { Text = Textes.Salut(f.Pseudo, DateTime.Now), FontFamily = Habillage.Affichage, FontSize = 28, Foreground = Habillage.Pinceau(P.Texte) });
            tEtat = new TextBlock { Text = "Analyse de ton PC…", FontFamily = Habillage.Corps, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent) };
            tDetail = new TextBlock { Text = "Quelques secondes : rien n'est modifié pendant l'analyse.", FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            texte.Children.Add(tEtat); texte.Children.Add(tDetail);
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(0, 12, 0, 0) };
            var unClic = new BoutonNoyau(f.Simulation ? "Simuler en 1 clic" : "Optimiser en 1 clic", true, 240, 48) { TaillePolice = 16 };
            unClic.Clic += () => F.LancerAction("complet");
            var analyser = new BoutonNoyau("Analyser sans rien modifier", false, 240, 48);
            analyser.Clic += () => F.LancerAction("analyse");
            boutons.Children.Add(unClic); boutons.Children.Add(analyser);
            texte.Children.Add(boutons);

            // mesures : quatre tuiles sur une ligne, deux par ligne quand la fenêtre est étroite
            mesures = new UniformGrid { Columns = 4, Margin = new Thickness(0, 14, 0, 0) };
            mesures.Children.Add(Tuile("PROCESSEUR", 0, out _));
            mesures.Children.Add(Tuile("MÉMOIRE VIVE", 1, out _));
            mesures.Children.Add(Tuile(Langue.F("DISQUE {0}", Environment.GetEnvironmentVariable("SystemDrive") ?? "C:"), 2, out _));
            tuileTemperature = Tuile("TEMPÉRATURE", 3, out tTitreTemperature);
            mesures.Children.Add(tuileTemperature);
            mesures.SizeChanged += (o, a) => Colonnes(a.NewSize.Width);
            texte.Children.Add(mesures);
            Grid.SetColumn(texte, 1);
            haut.Children.Add(texte);
            Children.Add(haut);

            // ---- liste des améliorations
            var titre = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(12, 0, 0, -6) };
            tTitreListe = new TextBlock { Text = "Améliorations proposées", FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center };
            titre.Children.Add(tTitreListe);
            bActualiser = new BoutonNoyau("Actualiser", false, 120, 28) { TaillePolice = 12, IsEnabled = false };
            bActualiser.Clic += () => { bActualiser.IsEnabled = false; F.ActualiserBilan(); };
            Grid.SetColumn(bActualiser, 1); titre.Children.Add(bActualiser);
            Children.Add(titre);
            Children.Add(liste);

            AttachedToVisualTree += (s, e) =>
            {
                F.BilanPret += Recevoir;
                if (F.Bilan != null) Appliquer(F.Bilan, false);
                // bilan de plus de 5 minutes, ou aucun : on le (re)calcule
                if (F.Bilan == null || (DateTime.Now - F.Bilan.Date).TotalMinutes > 5) F.ActualiserBilan();
                if (!F.Source.Immediat) { minuteur.Start(); Mesurer(); }
            };
            DetachedFromVisualTree += (s, e) => { F.BilanPret -= Recevoir; minuteur.Stop(); };
            minuteur.Tick += (s, e) => Mesurer();
        }

        private void Recevoir(Bilan b) { Appliquer(b, true); }

        private void Colonnes(double largeur)
        {
            int visibles = mesures.Children.Count(c => c.IsVisible);
            int c = largeur < 560 && visibles > 2 ? 2 : Math.Max(1, visibles);
            if (mesures.Columns != c) mesures.Columns = c;
        }

        /// <summary>Affiche un bilan : texte, coins de l'anneau, mesures, liste.</summary>
        private void Appliquer(Bilan b, bool nouveau)
        {
            var P = Habillage.P;
            bilan = b;
            bActualiser.IsEnabled = true;
            Color couleur;
            tEtat.Text = Textes.EtatCourt(b, out couleur);
            tEtat.Foreground = Habillage.Pinceau(couleur);
            tDetail.Text = Textes.EtatDetail(b);
            string[] noms = { "STOCKAGE", "MÉMOIRE", "SÉCURITÉ", "MATÉRIEL" };
            for (int k = 0; k < 4; k++)
            {
                double v = b.Secteurs != null && k < b.Secteurs.Length ? b.Secteurs[k] : -1;
                var align = coins[k].HorizontalAlignment == HorizontalAlignment.Right ? TextAlignment.Right : TextAlignment.Left;
                var c = v >= 0 && v < 80 ? Habillage.Niveau(v) : P.Discret;
                coins[k].Children.Clear();
                coins[k].Children.Add(new TextBlock { Text = noms[k], FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(c), TextAlignment = align });
                coins[k].Children.Add(new TextBlock { Text = v >= 0 ? Math.Round(v) + " %" : "—", FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(v >= 0 && v < 80 ? c : P.Texte), TextAlignment = align });
            }
            AfficherMesures(new Mesures { Processeur = b.Processeur, Memoire = b.Memoire, Temperature = b.Temperature, DisqueLibre = b.DisqueLibre });
            int aFaire = b.Points.Count(p => p.Etat != EtatPoint.Ok);
            tTitreListe.Text = aFaire == 0 ? "Points vérifiés" : "Améliorations proposées";
            RemplirListe();
            anneau.Montrer(b);
        }

        private Border Tuile(string titre, int index, out TextBlock tTitre)
        {
            var P = Habillage.P;
            var b = new Border
            {
                MinWidth = 120, Height = 64, Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(3),
                Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1),
            };
            var s = new StackPanel { Spacing = 4 };
            tTitre = new TextBlock { Text = Langue.T(titre), FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(P.Discret) };
            s.Children.Add(tTitre);
            valeurs[index] = new TextBlock { Text = "…", FontFamily = Habillage.Affichage, FontSize = 18, Foreground = Habillage.Pinceau(P.Texte), TextTrimming = TextTrimming.CharacterEllipsis };
            s.Children.Add(valeurs[index]);
            b.Child = s;
            return b;
        }

        private void AfficherMesures(Mesures m)
        {
            if (m == null) return;
            if (m.Processeur >= 0) valeurs[0].Text = Math.Round(m.Processeur) + " %";
            if (m.Memoire >= 0) valeurs[1].Text = Math.Round(m.Memoire) + " %";
            if (m.DisqueLibre != "") valeurs[2].Text = m.DisqueLibre;
            if (m.Temperature > 0)
            {
                valeurs[3].Text = Math.Round(m.Temperature) + " °C";
                valeurs[3].Foreground = Habillage.Pinceau(m.Temperature >= 90 ? Habillage.P.Rouge : m.Temperature >= 80 ? Habillage.P.Orange : Habillage.P.Texte);
                if (!string.IsNullOrEmpty(m.TitreTemperature)) tTitreTemperature.Text = Langue.T(m.TitreTemperature);
            }
            bool temp = m.Temperature > 0 || valeurs[3].Text != "…";
            if (tuileTemperature.IsVisible != temp) { tuileTemperature.IsVisible = temp; Colonnes(mesures.Bounds.Width); }
        }

        /// <summary>Mesures en direct, lues hors du fil de l'interface (la température une fois sur deux : elle est plus lente).</summary>
        private void Mesurer()
        {
            if (mesureEnCours || F.Source.Immediat) return;
            mesureEnCours = true;
            bool avecTemp = compteur++ % 2 == 0;
            System.Threading.Tasks.Task.Run(() =>
            {
                Mesures m = null;
                try { m = F.Source.Mesurer(avecTemp); } catch { }
                Dispatcher.UIThread.Post(() => { mesureEnCours = false; AfficherMesures(m); });
            });
        }

        private void RemplirListe()
        {
            liste.Children.Clear();
            if (bilan == null) return;
            var aFaire = bilan.Points.Where(p => p.Etat != EtatPoint.Ok).OrderBy(p => p.Etat == EtatPoint.Probleme ? 0 : 1).ToList();
            var bons = bilan.Points.Where(p => p.Etat == EtatPoint.Ok).ToList();
            foreach (var p in aFaire) liste.Children.Add(Ligne(p));
            if (bons.Count > 0)
            {
                string t = aFaire.Count == 0 ? Langue.F("Tout va bien : {0} points vérifiés.", bons.Count) : Langue.F("{0} autres points vérifiés : tout va bien.", bons.Count);
                liste.Children.Add(Ligne(new PointBilan(EtatPoint.Ok, t, okVisibles ? "Masquer la liste" : "Voir la liste"), () => { okVisibles = !okVisibles; RemplirListe(); }));
                if (okVisibles) foreach (var p in bons) liste.Children.Add(Ligne(p));
            }
        }

        private Control Ligne(PointBilan p, Action clic = null)
        {
            var P = Habillage.P;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("34,*,Auto"), Height = 46 };
            Color c = p.Etat == EtatPoint.Probleme ? P.Rouge : p.Etat == EtatPoint.Attention ? P.Orange : P.Vert;
            string sym = p.Etat == EtatPoint.Probleme ? "✖" : p.Etat == EtatPoint.Attention ? "!" : "✔";
            g.Children.Add(new TextBlock { Text = sym, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(c), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
            var t = new TextBlock { Text = p.Texte, FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            ToolTip.SetTip(t, p.Texte);
            Grid.SetColumn(t, 1); g.Children.Add(t);
            if (p.Bouton != null)
            {
                var b = new BoutonNoyau(p.Bouton, p.Etat == EtatPoint.Probleme, Math.Max(160, Math.Min(260, p.Bouton.Length * 7.6 + 40)), 30) { TaillePolice = 13, Margin = new Thickness(12, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
                if (clic != null) b.Clic += clic;
                else if (p.Cible != null) b.Clic += () => F.Executer(p.Cible, p.Bouton);
                Grid.SetColumn(b, 2); g.Children.Add(b);
            }
            return new Border { Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Child = g };
        }
    }
}
