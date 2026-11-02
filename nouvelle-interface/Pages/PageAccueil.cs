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
        private TextBlock tEtat, tDetail, tTitreListe, tAnalyse;
        private AnneauHud anneauHud;
        private readonly Border[] barres = new Border[4];
        private readonly StackPanel[] coins = new StackPanel[4];
        private UniformGrid mesures;
        private readonly TextBlock[] valeurs = new TextBlock[4];
        private TextBlock tTitreTemperature;
        private Cadre5 tuileTemperature;
        private BoutonNoyau bActualiser;
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

            if (Habillage.Hud) { ConstruireHud(f); }
            else
            {
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
            // HUD : l'anneau dans un panneau à coins coupés
            if (Habillage.Hud) haut.Children.Add(new Cadre5 { Child = zoneAnneau, Coupe = 22, Background = Habillage.Pinceau(P.Carte, P.Sombre ? 225 : 255), BorderBrush = Habillage.Pinceau(P.Accent, 60), BorderThickness = new Thickness(1) });
            else haut.Children.Add(zoneAnneau);

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
            mesures.Children.Add(Tuile(Langue.F("LIBRE SUR {0}", Environment.GetEnvironmentVariable("SystemDrive") ?? "C:"), 2, out _));
            tuileTemperature = Tuile("TEMPÉRATURE", 3, out tTitreTemperature);
            mesures.Children.Add(tuileTemperature);
            mesures.SizeChanged += (o, a) => Colonnes(a.NewSize.Width);
            texte.Children.Add(mesures);
            Grid.SetColumn(texte, 1);
            haut.Children.Add(texte);
            Children.Add(haut);

            // ---- favoris choisis (outils gratuits et Premium), lancés en un clic
            Children.Add(Favoris5.Bande(f));

            // ---- liste des améliorations
            var titre = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(12, 0, 0, -6) };
            tTitreListe = new TextBlock { Text = "Améliorations proposées", FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center };
            titre.Children.Add(tTitreListe);
            bActualiser = new BoutonNoyau("Actualiser", false, 120, 28) { TaillePolice = 12, IsEnabled = false };
            bActualiser.Clic += () => { bActualiser.IsEnabled = false; F.ActualiserBilan(); };
            Grid.SetColumn(bActualiser, 1); titre.Children.Add(bActualiser);
            Children.Add(titre);
            Children.Add(liste);

            }

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

        /// <summary>Accueil du style HUD (maquette A) : panneau de l'anneau, analyse, boutons, tuiles avec jauge, accès rapide, alertes.</summary>
        private void ConstruireHud(FenetrePrincipale f)
        {
            var P = Habillage.P;
            Margin = new Thickness(12, 6, 28, 20);
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("330,22,*") };
            // panneau de l'anneau, étiquettes aux quatre coins
            var zone = new Panel { Height = 318 };
            anneauHud = new AnneauHud(272) { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            zone.Children.Add(anneauHud);
            var places = new[] { (HorizontalAlignment.Right, VerticalAlignment.Top), (HorizontalAlignment.Right, VerticalAlignment.Bottom), (HorizontalAlignment.Left, VerticalAlignment.Bottom), (HorizontalAlignment.Left, VerticalAlignment.Top) };
            for (int k = 0; k < 4; k++)
            {
                coins[k] = new StackPanel { HorizontalAlignment = places[k].Item1, VerticalAlignment = places[k].Item2, Margin = new Thickness(k == 0 ? 0 : 14, 12, k == 0 ? 30 : 14, 12) };
                zone.Children.Add(coins[k]);
            }
            coins[3].Children.Add(new TextBlock { Text = "INTÉGRITÉ.SYS", FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Accent, 200) });
            haut.Children.Add(new Cadre5 { Child = zone, Coupe = 22, Background = Habillage.Pinceau(P.Carte, P.Sombre ? 230 : 255), BorderBrush = Habillage.Pinceau(P.Accent, 56), BorderThickness = new Thickness(1) });

            var texte = new StackPanel { Spacing = 10, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Stretch };
            tAnalyse = new TextBlock { Text = "> " + Langue.T("ANALYSE EN COURS…"), FontFamily = Habillage.Console, FontSize = 11, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Accent, 200) };
            texte.Children.Add(tAnalyse);
            texte.Children.Add(new TextBlock { Text = Textes.Salut(f.Pseudo, DateTime.Now), FontFamily = Habillage.Affichage, FontSize = 34, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            tEtat = new TextBlock { Text = Langue.T("Analyse de ton PC…"), FontFamily = Habillage.Corps, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap };
            tDetail = new TextBlock { IsVisible = false };
            texte.Children.Add(tEtat);
            var boutons = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            var unClic = new BoutonNoyau(f.Simulation ? "Simuler en 1 clic" : "Optimiser en 1 clic", true, 0, 48) { TaillePolice = 16, Margin = new Thickness(0, 0, 12, 0) };
            unClic.Clic += () => F.LancerAction("complet");
            var analyser = new BoutonNoyau("Analyser sans rien modifier", false, 0, 48) { TaillePolice = 14, Capitales = true };
            analyser.Clic += () => F.LancerAction("analyse");
            boutons.Children.Add(unClic); boutons.Children.Add(analyser);
            texte.Children.Add(boutons);
            mesures = new UniformGrid { Columns = 4, Margin = new Thickness(0, 8, 0, 0) };
            mesures.Children.Add(TuileHud("CPU", 0, P.Accent, out _));
            mesures.Children.Add(TuileHud("MÉMOIRE VIVE", 1, P.Accent, out _));
            mesures.Children.Add(TuileHud(Langue.F("LIBRE SUR {0}", Environment.GetEnvironmentVariable("SystemDrive") ?? "C:"), 2, P.Rouge, out _));
            tuileTemperature = TuileHud("TEMPÉRATURE", 3, P.Vert, out tTitreTemperature);
            mesures.Children.Add(tuileTemperature);
            mesures.SizeChanged += (o, a) => Colonnes(a.NewSize.Width);
            texte.Children.Add(mesures);
            Grid.SetColumn(texte, 2);
            haut.Children.Add(texte);
            Children.Add(haut);

            Children.Add(Favoris5.Bande(f));

            // alertes : titre avec trait qui s'efface, puis la liste
            var titre = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Margin = new Thickness(12, 2, 0, -6) };
            tTitreListe = new TextBlock { Text = "⚠  " + Langue.T("ALERTES"), FontFamily = Habillage.Console, FontSize = 11, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Accent, 200), VerticalAlignment = VerticalAlignment.Center };
            titre.Children.Add(tTitreListe);
            var trait = new Border { Height = 1, Margin = new Thickness(12, 0, 12, 0), VerticalAlignment = VerticalAlignment.Center, Background = Trait(P.Orange) };
            Grid.SetColumn(trait, 1); titre.Children.Add(trait);
            bActualiser = new BoutonNoyau("Actualiser", false, 120, 28) { TaillePolice = 12, IsEnabled = false };
            bActualiser.Clic += () => { bActualiser.IsEnabled = false; F.ActualiserBilan(); };
            Grid.SetColumn(bActualiser, 2); titre.Children.Add(bActualiser);
            Children.Add(titre);
            Children.Add(liste);
        }

        /// <summary>Trait fin qui s'efface vers la droite (titres de section du HUD).</summary>
        public static IBrush Trait(Color c)
        {
            return new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                GradientStops = { new GradientStop(Habillage.Alpha(c, 110), 0), new GradientStop(Habillage.Alpha(c, 0), 1) },
            };
        }

        private Cadre5 TuileHud(string titre, int index, Color couleur, out TextBlock tTitre)
        {
            var P = Habillage.P;
            var b = new Cadre5
            {
                Coupe = 0, TraitHaut = couleur, Height = 72, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(12, 10, 12, 10),
                Background = Habillage.Pinceau(P.Carte, P.Sombre ? 230 : 255), BorderBrush = Habillage.Pinceau(P.Accent, 52), BorderThickness = new Thickness(1),
            };
            var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
            tTitre = new TextBlock { Text = Langue.T(titre), FontFamily = Habillage.Console, FontSize = 10, LetterSpacing = 1, Foreground = Habillage.Pinceau(P.Discret) };
            g.Children.Add(tTitre);
            valeurs[index] = new TextBlock { Text = "…", FontFamily = Habillage.Affichage, FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetRow(valeurs[index], 1); g.Children.Add(valeurs[index]);
            var piste = new Panel { Height = 4, Background = Habillage.Pinceau(couleur, 34) };
            barres[index] = new Border { Height = 4, Width = 0, HorizontalAlignment = HorizontalAlignment.Left, Background = Habillage.Pinceau(couleur) };
            piste.Children.Add(barres[index]);
            piste.SizeChanged += (o, a) => { var t = barres[index].Tag as double?; if (t.HasValue) barres[index].Width = a.NewSize.Width * t.Value; };
            Grid.SetRow(piste, 2); g.Children.Add(piste);
            b.Child = g;
            return b;
        }

        /// <summary>HUD : valeur avec unité plus petite et grise, et jauge (0 à 1).</summary>
        private void ValeurHud(int i, string nombre, string unite, double jauge)
        {
            var t = valeurs[i];
            t.Inlines = new Avalonia.Controls.Documents.InlineCollection
            {
                new Avalonia.Controls.Documents.Run(nombre),
                new Avalonia.Controls.Documents.Run(" " + unite) { FontSize = 13, Foreground = Habillage.Pinceau(Habillage.P.Discret) },
            };
            var bar = barres[i];
            if (bar == null || jauge < 0) return;
            jauge = Math.Max(0, Math.Min(1, jauge));
            bar.Tag = (double?)jauge;
            var piste = bar.Parent as Panel;
            if (piste != null && piste.Bounds.Width > 0) bar.Width = piste.Bounds.Width * jauge;
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
            int aFaire = b.Points.Count(p => p.Etat != EtatPoint.Ok);
            if (Habillage.Hud)
            {
                // HUD : « STOCKAGE 44 % » sur une ligne dans trois coins (le quatrième garde « INTÉGRITÉ.SYS »)
                if (aFaire > 0) tEtat.Text = Langue.T(tEtat.Text) + " : " + Langue.F(aFaire == 1 ? "{0} point détecté." : "{0} points détectés.", aFaire);
                tAnalyse.Text = "> " + Langue.T("ANALYSE TERMINÉE") + " · " + b.Date.ToString("HH:mm:ss");
                for (int k = 0; k < 3; k++)
                {
                    double v = b.Secteurs != null && k < b.Secteurs.Length ? b.Secteurs[k] : -1;
                    var c = v >= 0 && v < 80 ? Habillage.Niveau(v) : P.Discret;
                    coins[k].Children.Clear();
                    coins[k].Children.Add(new TextBlock { Text = Langue.T(noms[k]) + " " + (v >= 0 ? Math.Round(v) + "%" : "—"), FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1, Foreground = Habillage.Pinceau(c) });
                }
                AfficherMesures(new Mesures { Processeur = b.Processeur, Memoire = b.Memoire, Temperature = b.Temperature, DisqueLibre = b.DisqueLibre, DisquePourcentLibre = b.DisquePourcentLibre });
                tTitreListe.Text = aFaire == 0 ? "✔  " + Langue.T("POINTS VÉRIFIÉS") : "⚠  " + Langue.T("ALERTES") + " · " + aFaire;
                RemplirListe();
                anneauHud.Montrer(b);
                return;
            }
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
            tTitreListe.Text = aFaire == 0 ? "Points vérifiés" : "Améliorations proposées";
            RemplirListe();
            anneau.Montrer(b);
        }

        private Cadre5 Tuile(string titre, int index, out TextBlock tTitre)
        {
            var P = Habillage.P;
            var b = new Cadre5
            {
                Coupe = 10, TraitHaut = Habillage.Hud ? (index == 2 ? P.Rouge : index == 3 ? P.Vert : P.Accent) : (Color?)null,
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
            if (Habillage.Hud)
            {
                if (m.Processeur >= 0) ValeurHud(0, Math.Round(m.Processeur).ToString(), "%", m.Processeur / 100);
                if (m.Memoire >= 0) ValeurHud(1, Math.Round(m.Memoire).ToString(), "%", m.Memoire / 100);
                if (m.DisqueLibre != "")
                {
                    // « 27,60 Go libres » → « 27,6 » et « Go » ; la jauge montre la place libre
                    string d = m.DisqueLibre.EndsWith(" libres", StringComparison.Ordinal) ? m.DisqueLibre.Substring(0, m.DisqueLibre.Length - 7) : m.DisqueLibre;
                    int esp = d.LastIndexOf(' ');
                    ValeurHud(2, esp > 0 ? d.Substring(0, esp) : d, esp > 0 ? d.Substring(esp + 1) : "", m.DisquePourcentLibre >= 0 ? m.DisquePourcentLibre / 100 : -1);
                }
                if (m.Temperature > 0)
                {
                    ValeurHud(3, Math.Round(m.Temperature).ToString(), "°C", m.Temperature / 100);
                    valeurs[3].Foreground = Habillage.Pinceau(m.Temperature >= 90 ? Habillage.P.Rouge : m.Temperature >= 80 ? Habillage.P.Orange : Habillage.P.Texte);
                    if (!string.IsNullOrEmpty(m.TitreTemperature)) tTitreTemperature.Text = Langue.T(m.TitreTemperature);
                }
                bool t2 = m.Temperature > 0 || valeurs[3].Inlines != null && valeurs[3].Inlines.Count > 0;
                if (tuileTemperature.IsVisible != t2) { tuileTemperature.IsVisible = t2; Colonnes(mesures.Bounds.Width); }
                return;
            }
            if (m.Processeur >= 0) valeurs[0].Text = Math.Round(m.Processeur) + " %";
            if (m.Memoire >= 0) valeurs[1].Text = Math.Round(m.Memoire) + " %";
            // « 27,60 Go libres » : la tuile (« LIBRE SUR C: ») ne garde que la taille, qui tient même pour un disque de plusieurs To
            if (m.DisqueLibre != "") valeurs[2].Text = m.DisqueLibre.EndsWith(" libres", StringComparison.Ordinal) ? m.DisqueLibre.Substring(0, m.DisqueLibre.Length - 7) : m.DisqueLibre;
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
            int n = 0;
            foreach (var p in aFaire) liste.Children.Add(Ligne(p, null, ++n));
            if (bons.Count > 0)
            {
                string t = aFaire.Count == 0 ? Langue.F("Tout va bien : {0} points vérifiés.", bons.Count) : Langue.F("{0} autres points vérifiés : tout va bien.", bons.Count);
                liste.Children.Add(Ligne(new PointBilan(EtatPoint.Ok, t, okVisibles ? "Masquer la liste" : "Voir la liste"), () => { okVisibles = !okVisibles; RemplirListe(); }));
                if (okVisibles) foreach (var p in bons) liste.Children.Add(Ligne(p));
            }
        }

        private Control Ligne(PointBilan p, Action clic = null, int numero = 0)
        {
            var P = Habillage.P;
            if (Habillage.Hud) return LigneHud(p, clic, numero);
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
            return new Cadre5 { Coupe = 10, Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(Habillage.Hud && p.Etat != EtatPoint.Ok ? Habillage.Alpha(c, 70) : P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), Child = g };
        }

        /// <summary>HUD : barre de couleur à gauche, code « A-01 », texte, bouton en capitales.</summary>
        private Control LigneHud(PointBilan p, Action clic, int numero)
        {
            var P = Habillage.P;
            Color c = p.Etat == EtatPoint.Probleme ? P.Rouge : p.Etat == EtatPoint.Attention ? P.Orange : P.Vert;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("4,Auto,*,Auto"), Height = 46 };
            g.Children.Add(new Border { Background = Habillage.Pinceau(c) });
            var code = new TextBlock { Text = numero > 0 ? "A-" + numero.ToString("00") : "✔", FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(c), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 14, 0) };
            Grid.SetColumn(code, 1); g.Children.Add(code);
            var t = new TextBlock { Text = p.Texte, FontFamily = Habillage.Corps, FontSize = 14, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            ToolTip.SetTip(t, p.Texte);
            Grid.SetColumn(t, 2); g.Children.Add(t);
            if (p.Bouton != null)
            {
                var b = new BoutonNoyau(p.Bouton, false, 0, 32) { TaillePolice = 13, Capitales = true, Margin = new Thickness(12, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                if (clic != null) b.Clic += clic;
                else if (p.Cible != null) b.Clic += () => F.Executer(p.Cible, p.Bouton);
                Grid.SetColumn(b, 3); g.Children.Add(b);
            }
            return new Border { Background = Habillage.Pinceau(P.Carte, P.Sombre ? 230 : 255), BorderBrush = Habillage.Pinceau(p.Etat == EtatPoint.Ok ? P.Bordure : Habillage.Alpha(c, 64)), BorderThickness = new Thickness(1), Child = g };
        }
    }
}
