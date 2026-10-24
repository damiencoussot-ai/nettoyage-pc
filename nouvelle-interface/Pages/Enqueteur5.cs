// Nettoyage PC 5 - « Pourquoi mon PC ralentit ? » (Premium) : chronologie de ce qui a changé (applications, pilotes,
// mises à jour, démarrage, réglages), comparée aux performances. Tout est lu sur le PC et y reste.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class Enqueteur5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly ComboBox cbDepuis = new ComboBox { Width = 210 }, cbCourbe = new ComboBox { MinWidth = 260 };
        private readonly CheckBox cSuspects;
        private readonly TextBlock tVerdict, tDetail;
        private readonly CourbeTemps5 courbe = new CourbeTemps5 { Height = 160 };
        private readonly Tableau tableau;
        private readonly BoutonNoyau bCorriger, bChrono, bActualiser;
        private readonly int[] jours = { 3, 7, 14, 30, 90 };
        private ResultatEnquete res;
        private FenetreInterieure fenetre;
        private bool enCours;

        /// <summary>(captures) enquête d'exemple : rien n'est lu.</summary>
        public static ResultatEnquete Exemple;

        public static void Ouvrir(FenetrePrincipale f) { var e = new Enqueteur5(f); e.fenetre = f.Ouvrir("Pourquoi mon PC ralentit ?", e, 1020); }

        private Enqueteur5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(Premium5.Intro("Tout ce qui a changé sur ton PC (applications, pilotes, mises à jour, démarrage, réglages), comparé à ses performances. Tout est lu sur ton PC et y reste."));
            var l = new WrapPanel { Orientation = Orientation.Horizontal };
            l.Children.Add(new TextBlock { Text = Langue.T("Depuis quand est-il plus lent ?"), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 6) });
            foreach (var t in new[] { "depuis 3 jours", "depuis une semaine", "depuis 2 semaines", "depuis un mois", "depuis 3 mois" }) cbDepuis.Items.Add(Langue.T(t));
            cbDepuis.SelectedIndex = 2; cbDepuis.Margin = new Thickness(0, 0, 16, 6);
            cbDepuis.SelectionChanged += (s, e) => Afficher();
            l.Children.Add(cbDepuis);
            cSuspects = new CheckBox { Content = Langue.T("Seulement les suspects"), Margin = new Thickness(0, 0, 16, 6) };
            cSuspects.IsCheckedChanged += (s, e) => Afficher();
            l.Children.Add(cSuspects);
            bChrono = new BoutonNoyau("Ajouter la durée des démarrages", false, 0, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 0, 6) };
            ToolTip.SetTip(bChrono, Langue.T("Windows chronomètre chaque démarrage. Pour lire ce chronométrage, il demande ton autorisation (une fois)."));
            bChrono.Clic += () => F.LancerAction("demarrage", Charger);
            l.Children.Add(bChrono);
            Children.Add(l);
            tVerdict = new TextBlock { Text = Langue.T("Enquête en cours…"), FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap };
            tDetail = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tVerdict); Children.Add(tDetail);
            var zone = new Grid();
            zone.Children.Add(courbe);
            cbCourbe.HorizontalAlignment = HorizontalAlignment.Right; cbCourbe.VerticalAlignment = VerticalAlignment.Top; cbCourbe.Margin = new Thickness(0, 6, 8, 0);
            cbCourbe.SelectionChanged += (s, e) => AfficherCourbe();
            zone.Children.Add(cbCourbe);
            Children.Add(zone);
            tableau = new Tableau(false, false, new Colonne("Date", "105", false, true, o => ((Changement)o).Date), new Colonne("Ce qui a changé", "2.6*"), new Colonne("Type", "130"), new Colonne("Pourquoi c'est suspect", "2.4*"));
            tableau.HauteurListe = 240;
            tableau.SelectionChangee += () => { var c = tableau.Selection as Changement; bCorriger.IsEnabled = c != null && c.Cible != ""; };
            tableau.DoubleClic += o => Corriger();
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            var bas = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            bCorriger = new BoutonNoyau("Voir et corriger", true, 0, 34) { TaillePolice = 13.5, IsEnabled = false };
            bCorriger.Clic += Corriger;
            bActualiser = new BoutonNoyau("Actualiser", false, 0, 34) { TaillePolice = 13 };
            bActualiser.Clic += Charger;
            bas.Children.Add(bCorriger); bas.Children.Add(bActualiser);
            Children.Add(bas);
            AttachedToVisualTree += (o, e) => Charger();
        }

        private DateTime Depuis { get { return DateTime.Today.AddDays(-jours[Math.Max(0, cbDepuis.SelectedIndex)]); } }
        private string Periode { get { return cbDepuis.SelectedItem as string ?? ""; } }

        private void Charger()
        {
            if (enCours) return;
            if (Exemple != null) { res = Exemple; RemplirCourbes(); Afficher(); return; }
            enCours = true; bActualiser.IsEnabled = false;
            tVerdict.Text = Langue.T("Enquête en cours…"); tVerdict.Foreground = Habillage.Pinceau(Habillage.P.Accent);
            Task.Run(() => { try { Instantanes.PrendreSiBesoin(); return Enqueteur.Analyser(); } catch (Exception ex) { JournalDemarrage.Erreur("enquêteur (version 5)", ex); return new ResultatEnquete(); } })
                .ContinueWith(t => Dispatcher.UIThread.Post(() => { enCours = false; bActualiser.IsEnabled = true; res = t.Result ?? new ResultatEnquete(); RemplirCourbes(); Afficher(); }));
        }

        private void RemplirCourbes()
        {
            int avant = cbCourbe.SelectedIndex;
            cbCourbe.Items.Clear();
            foreach (var m in res.Metriques) cbCourbe.Items.Add(Langue.T(m.Nom));
            cbCourbe.IsVisible = res.Metriques.Count > 1;
            if (res.Metriques.Count > 0) cbCourbe.SelectedIndex = avant >= 0 && avant < res.Metriques.Count ? avant : 0;
            bChrono.IsVisible = !res.ChronoDemarrages;
        }

        private void AfficherCourbe()
        {
            var m = res == null || cbCourbe.SelectedIndex < 0 || cbCourbe.SelectedIndex >= res.Metriques.Count ? null : res.Metriques[cbCourbe.SelectedIndex];
            DateTime debut = Depuis.AddDays(-Math.Max(14, jours[Math.Max(0, cbDepuis.SelectedIndex)]));
            courbe.Debut = debut;
            if (m == null)
            {
                courbe.Titre = "Performances";
                courbe.Points = new List<Tuple<DateTime, double>>();
                courbe.VideTexte = "Pas encore de mesures : clique sur « Ajouter la durée des démarrages » pour voir l'évolution du temps de démarrage.";
            }
            else
            {
                courbe.Titre = Langue.T(m.Nom) + "   ·   " + Langue.T("traits orange : changements suspects");
                courbe.Points = m.Points; courbe.Barres = m.Comptage;
                courbe.Format = v => m.Format(v);
                courbe.VideTexte = "Pas encore assez de mesures sur cette période.";
            }
            courbe.Reperes = res == null ? new List<Tuple<DateTime, bool>>() : res.Changements.Where(c => c.Date >= debut).Select(c => Tuple.Create(c.Date, c.Suspicion >= 2)).ToList();
            courbe.Actualiser();
        }

        private void Afficher()
        {
            if (res == null) return;
            var P = Habillage.P;
            var depuis = Depuis;
            var l = res.Changements.Where(c => c.Date >= depuis && (cSuspects.IsChecked != true || c.Suspicion >= 1)).OrderByDescending(c => c.Suspicion).ThenByDescending(c => c.Date).ToList();
            tableau.Remplir(l.Cast<object>(), o =>
            {
                var c = (Changement)o;
                return new[] { c.Date.ToString("dd/MM/yyyy"), (c.Suspicion >= 2 ? "⚠  " : "") + c.Titre, Langue.T(c.Type), c.Pourquoi };
            }, null, o => ((Changement)o).Suspicion == 0);
            int niveau; string detail;
            tVerdict.Text = Enqueteur.Verdict(res, depuis, Periode, out niveau, out detail);
            tVerdict.Foreground = Habillage.Pinceau(niveau == 0 ? P.Vert : niveau == 2 ? P.Orange : P.Texte);
            tDetail.Text = detail; tDetail.IsVisible = detail != "";
            bCorriger.IsEnabled = false;
            AfficherCourbe();
        }

        private void Corriger()
        {
            var c = tableau.Selection as Changement;
            if (c == null || c.Cible == "") return;
            Premium5.Agir(F, fenetre, c.Cible, c.Titre);
        }
    }
}
