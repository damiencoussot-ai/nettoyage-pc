// Nettoyage PC 5 - « Preuve en FPS » (Premium) : images par seconde réelles d'un jeu (moyenne, 1 % et 0,1 % les plus
// lents, saccades), avant puis après un réglage. Mesure par PresentMon (Intel), téléchargé et vérifié avec ta permission.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class Fps5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly ComboBox cbJeu = new ComboBox { MinWidth = 420 }, cbDuree = new ComboBox { Width = 110 }, cbDelai = new ComboBox { Width = 150 };
        private readonly TextBox libelle = new TextBox { Width = 240 };
        private readonly BoutonNoyau bMesurer, bActualiser, bSupprimer;
        private readonly TextBlock tEtat, tComparaison;
        private readonly Tuile tMoy, tBas1, tBas01, tSacc;
        private readonly Tableau tableau;
        private readonly DispatcherTimer compte = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private List<CibleJeu> jeux = new List<CibleJeu>();
        private List<MesureFps> historique = new List<MesureFps>();
        private DateTime finPrevue;
        private bool enCours, ouverte = true;

        /// <summary>(captures) mesures d'exemple : rien n'est lu ni mesuré.</summary>
        public static List<MesureFps> Exemple;

        private sealed class Tuile : Border
        {
            private readonly TextBlock valeur;
            public Tuile(string titre)
            {
                var P = Habillage.P;
                var s = new StackPanel { Spacing = 3 };
                s.Children.Add(new TextBlock { Text = Langue.T(titre).ToUpperInvariant(), FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(P.Discret), TextTrimming = TextTrimming.CharacterEllipsis });
                valeur = new TextBlock { Text = "—", FontFamily = Habillage.Affichage, FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) };
                s.Children.Add(valeur);
                Child = s; Padding = new Thickness(14, 10); Margin = new Thickness(0, 0, 10, 10); Width = 218; CornerRadius = new CornerRadius(3);
                Background = Habillage.Pinceau(P.Carte); BorderBrush = Habillage.Pinceau(P.Bordure); BorderThickness = new Thickness(1);
            }
            public void Definir(string v, Color? c = null) { valeur.Text = v; valeur.Foreground = Habillage.Pinceau(c ?? Habillage.P.Texte); }
        }

        /// <summary>Outil Premium : sans Premium, la fenêtre d'achat s'ouvre (essai gratuit possible).</summary>
        public static void OuvrirPremium(FenetrePrincipale f) { if (Licence.Premium || Exemple != null) Ouvrir(f); else Achat5.Ouvrir(f); }

        public static void Ouvrir(FenetrePrincipale f)
        {
            f.Ouvrir("Preuve en FPS", new Fps5(f), 980);
        }

        private Fps5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(new TextBlock { Text = Langue.T("Mesure les images par seconde réelles d'un jeu (moyenne, et les 1 % et 0,1 % d'images les plus lentes, qui font les saccades), avant puis après un réglage : tu vois le vrai gain, en chiffres. Mesure faite par PresentMon, l'outil gratuit et open source d'Intel."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });

            var l1 = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            l1.Children.Add(Etiquette("1.  Lance ton jeu, puis choisis-le :", 250));
            cbJeu.Margin = new Thickness(0, 0, 10, 6); l1.Children.Add(cbJeu);
            bActualiser = new BoutonNoyau("Actualiser", false, 0, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 0, 6) };
            bActualiser.Clic += RemplirJeux;
            l1.Children.Add(bActualiser);
            Children.Add(l1);

            var l2 = new WrapPanel { Orientation = Orientation.Horizontal };
            l2.Children.Add(Etiquette("2.  Durée de la mesure :", 250));
            foreach (var t in new[] { "30 s", "60 s", "2 min" }) cbDuree.Items.Add(t);
            cbDuree.SelectedIndex = 1; cbDuree.Margin = new Thickness(0, 0, 18, 6); l2.Children.Add(cbDuree);
            l2.Children.Add(Etiquette("Elle commence :", 0));
            foreach (var t in new[] { "dans 10 s", "dans 20 s", "tout de suite" }) cbDelai.Items.Add(Langue.T(t));
            cbDelai.SelectedIndex = 0; cbDelai.Margin = new Thickness(0, 0, 0, 6); l2.Children.Add(cbDelai);
            Children.Add(l2);

            var l3 = new WrapPanel { Orientation = Orientation.Horizontal };
            l3.Children.Add(Etiquette("3.  Nom de la mesure :", 250));
            libelle.Text = Langue.T("avant"); libelle.Margin = new Thickness(0, 0, 12, 6); l3.Children.Add(libelle);
            l3.Children.Add(new TextBlock { Text = Langue.T("(par exemple « avant », puis « après nettoyage »)"), FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 0, 6) });
            Children.Add(l3);

            var l4 = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(0, 4, 0, 4) };
            bMesurer = new BoutonNoyau("Mesurer", true, 180, 40) { TaillePolice = 15, VerticalAlignment = VerticalAlignment.Top };
            bMesurer.Clic += Mesurer;
            l4.Children.Add(bMesurer);
            tEtat = new TextBlock { Text = Langue.T("Pendant la mesure, retourne dans le jeu et joue normalement (toujours au même endroit, pour comparer)."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            Grid.SetColumn(tEtat, 1); l4.Children.Add(tEtat);
            Children.Add(l4);

            var tuiles = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            tMoy = new Tuile("FPS moyen"); tBas1 = new Tuile("1 % les plus lents"); tBas01 = new Tuile("0,1 % les plus lents"); tSacc = new Tuile("saccades (images très lentes)");
            foreach (var t in new[] { tMoy, tBas1, tBas01, tSacc }) tuiles.Children.Add(t);
            Children.Add(tuiles);
            tComparaison = new TextBlock { FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tComparaison);

            tableau = new Tableau(false, false, new Colonne("Date", "150", false, true, o => ((MesureFps)o).Date), new Colonne("Jeu", "1.3*"), new Colonne("Mesure", "1.3*"),
                new Colonne("FPS moyen", "100", true, true, o => ((MesureFps)o).Moyenne), new Colonne("1 % lents", "90", true, true, o => ((MesureFps)o).Bas1),
                new Colonne("0,1 % lents", "95", true, true, o => ((MesureFps)o).Bas01), new Colonne("Durée", "70", true, true, o => ((MesureFps)o).Secondes));
            tableau.HauteurListe = 200;
            tableau.SelectionChangee += () => { var m = tableau.Selection as MesureFps; bSupprimer.IsEnabled = m != null && !enCours; if (m != null) AfficherMesure(m); };
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            bSupprimer = new BoutonNoyau("Supprimer la mesure", false, 0, 32) { TaillePolice = 13, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left };
            bSupprimer.Clic += Supprimer;
            Children.Add(bSupprimer);
            compte.Tick += (s, e) => Compter();
            // fenêtre fermée pendant une mesure : la mesure continue et sera quand même enregistrée
            DetachedFromVisualTree += (o, e) => { ouverte = false; compte.Stop(); };
            AttachedToVisualTree += (o, e) =>
            {
                RemplirJeux();
                try { historique = Exemple ?? Fps.Historique(); } catch { historique = new List<MesureFps>(); }
                AfficherHistorique();
            };
        }

        private static TextBlock Etiquette(string t, double largeur)
        {
            var e = new TextBlock { Text = Langue.T(t), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(Habillage.P.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 6) };
            if (largeur > 0) e.Width = largeur;
            return e;
        }

        private void Etat(string t, Color c) { tEtat.Text = Langue.T(t); tEtat.Foreground = Habillage.Pinceau(c); }

        private void RemplirJeux()
        {
            var avant = cbJeu.SelectedIndex >= 0 && cbJeu.SelectedIndex < jeux.Count ? jeux[cbJeu.SelectedIndex] : null;
            if (Exemple != null) jeux = new List<CibleJeu> { new CibleJeu { Processus = "Cyberpunk2077.exe", Jeu = true, Libelle = "★ Cyberpunk 2077  (Cyberpunk2077.exe)" } };
            else { try { jeux = Fps.FenetresOuvertes(); } catch { jeux = new List<CibleJeu>(); } }
            cbJeu.Items.Clear();
            foreach (var c in jeux) cbJeu.Items.Add(c.Libelle);
            if (jeux.Count == 0) cbJeu.Items.Add(Langue.T("Aucun jeu ouvert : lance-le, puis clique sur « Actualiser »"));
            int i = avant == null ? 0 : jeux.FindIndex(c => c.Processus == avant.Processus);
            cbJeu.SelectedIndex = Math.Max(0, i);
        }

        private void Mesurer()
        {
            if (enCours) return;
            var c = cbJeu.SelectedIndex >= 0 && cbJeu.SelectedIndex < jeux.Count ? jeux[cbJeu.SelectedIndex] : null;
            if (c == null || c.Processus == "") { F.Demander("Choisis un jeu", "Lance d'abord ton jeu, puis clique sur « Actualiser » et choisis-le dans la liste.", "Compris", null, r => { }); return; }
            if (Exemple != null) return;
            int secondes = cbDuree.SelectedIndex == 0 ? 30 : cbDuree.SelectedIndex == 2 ? 120 : 60;
            int delai = cbDelai.SelectedIndex == 0 ? 10 : cbDelai.SelectedIndex == 1 ? 20 : 0;
            string nom = (libelle.Text ?? "").Trim(); if (nom == "") nom = DateTime.Now.ToString("dd/MM HH:mm");
            Action partir = () => Lancer(c, secondes, delai, nom);
            if (!Fps.OutilPresent)
                F.Demander("Télécharger PresentMon ?", "Pour mesurer, Nettoyage PC utilise PresentMon, l'outil gratuit et open source d'Intel.\n\nIl va être téléchargé depuis sa page officielle (GitHub, environ 2 Mo), vérifié, puis installé dans le dossier de Nettoyage PC. Windows demandera ensuite ton autorisation pour la mesure.", "Continuer", "Annuler", oui => { if (oui) partir(); });
            else partir();
        }

        private void Lancer(CibleJeu c, int secondes, int delai, string nom)
        {
            enCours = true; bMesurer.IsEnabled = false; cbJeu.IsEnabled = false; bActualiser.IsEnabled = false; bSupprimer.IsEnabled = false;
            Etat("Windows demande ton autorisation pour la mesure : clique sur « Oui ».", Habillage.P.Accent);
            string erreur = null;
            // hors du fil de l'interface : téléchargement, autorisation de Windows, puis la mesure elle-même (jusqu'à 2 min)
            Task.Run(() => Fps.Mesurer(c, secondes, delai, nom,
                t => Dispatcher.UIThread.Post(() => { if (ouverte) Etat(t, Habillage.P.Accent); }),
                () => Dispatcher.UIThread.Post(() => { finPrevue = DateTime.Now.AddSeconds(secondes + delai + 3); if (ouverte) compte.Start(); }),
                out erreur)).ContinueWith(t => Dispatcher.UIThread.Post(() => Fin(t.IsFaulted ? null : t.Result, t.IsFaulted ? Langue.T("Mesure impossible : ") + t.Exception.GetBaseException().Message : erreur)));
        }

        private void Compter()
        {
            int reste = (int)Math.Ceiling((finPrevue - DateTime.Now).TotalSeconds);
            if (reste > 0) Etat(Langue.F("Mesure en cours : retourne dans le jeu et joue normalement. Encore {0} s environ.", reste), Habillage.P.Accent);
            else Etat("Calcul des résultats…", Habillage.P.Accent);
        }

        private void Fin(MesureFps m, string erreur)
        {
            var P = Habillage.P;
            compte.Stop(); enCours = false; bMesurer.IsEnabled = true; cbJeu.IsEnabled = true; bActualiser.IsEnabled = true;
            if (erreur != null || m == null) { if (ouverte) Etat(erreur ?? Langue.T("Mesure impossible."), P.Orange); else F.Statut(erreur ?? "Mesure impossible.", true); return; }
            // la mesure est gardée même si la fenêtre a été fermée entre-temps
            historique.Insert(0, m);
            Fps.Enregistrer(historique);
            string fini = Langue.F("Mesure « {0} » terminée : {1} images en {2} s.", m.Libelle, m.Images, m.Secondes);
            if (!ouverte) { F.Statut(fini); return; }
            Etat(fini, P.Vert);
            AfficherHistorique();
            AfficherMesure(m);
            if ((libelle.Text ?? "").Trim().Equals(Langue.T("avant"), StringComparison.OrdinalIgnoreCase)) libelle.Text = Langue.T("après");
        }

        private void AfficherMesure(MesureFps m)
        {
            var P = Habillage.P;
            Func<double, string> f = v => v.ToString(v < 100 ? "0.0" : "0", Appli.Fr);
            tMoy.Definir(f(m.Moyenne)); tBas1.Definir(f(m.Bas1)); tBas01.Definir(f(m.Bas01));
            tSacc.Definir(m.Saccades.ToString("0"), m.Saccades > m.Secondes ? P.Orange : (Color?)null);
            int niveau;
            tComparaison.Text = Fps.Comparaison(historique, m, out niveau);
            tComparaison.Foreground = Habillage.Pinceau(niveau == 0 ? P.Discret : niveau == 2 ? P.Vert : niveau == 1 ? P.Orange : P.Texte);
        }

        private void AfficherHistorique()
        {
            tableau.Remplir(historique.Cast<object>(), o =>
            {
                var m = (MesureFps)o;
                DateTime d;
                string date = DateTime.TryParseExact(m.Date, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) ? d.ToString("dd/MM/yyyy HH:mm") : m.Date;
                return new[] { date, m.Jeu, m.Libelle, m.Moyenne.ToString("0.0", Appli.Fr), m.Bas1.ToString("0.0", Appli.Fr), m.Bas01.ToString("0.0", Appli.Fr), m.Secondes + " s" };
            });
            if (historique.Count > 0) AfficherMesure(historique[0]);
            else { foreach (var t in new[] { tMoy, tBas1, tBas01, tSacc }) t.Definir("—"); tComparaison.Text = ""; }
        }

        private void Supprimer()
        {
            var m = tableau.Selection as MesureFps;
            if (m == null || enCours) return;
            F.Demander(Langue.F("Supprimer la mesure « {0} » ?", m.Libelle), null, "Supprimer", "Annuler", oui =>
            {
                if (!oui) return;
                historique.Remove(m);
                if (Exemple == null) Fps.Enregistrer(historique);
                AfficherHistorique();
                bSupprimer.IsEnabled = false;
            });
        }
    }
}
