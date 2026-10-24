// Nettoyage PC 5 - outils Premium en lecture seule : santé de la batterie, santé prédictive, coût en électricité.
// Tout est lu sur le PC (rapports de Windows, journaux, mesures du gardien) : rien n'est modifié.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal static class Premium5
    {
        public static TextBlock Intro(string t) { return new TextBlock { Text = Langue.T(t), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(Habillage.P.Discret), TextWrapping = TextWrapping.Wrap }; }
        public static TextBlock Titre(string t) { return new TextBlock { Text = Langue.T(t), FontFamily = Habillage.Affichage, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(Habillage.P.Texte), TextWrapping = TextWrapping.Wrap }; }
        public static Border Carte(Control c, Color? bord = null) { var P = Habillage.P; return new Border { Child = c, Padding = new Thickness(18, 14), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(bord ?? P.Bordure), BorderThickness = new Thickness(1) }; }

        /// <summary>Cible d'un bouton d'outil : « action:cle », « page:Cle », « uri:... » (la fenêtre se ferme pour une rubrique).</summary>
        public static void Agir(FenetrePrincipale f, FenetreInterieure fenetre, string cible, string libelle)
        {
            if (string.IsNullOrEmpty(cible)) return;
            if (cible.StartsWith("page:") && fenetre != null) fenetre.Fermer();
            f.Executer(cible, libelle);
        }
    }

    // ================================================================ Santé de la batterie
    internal sealed class Batterie5 : StackPanel
    {
        private readonly JaugeSante5 jauge = new JaugeSante5();
        private readonly TextBlock tEtat, tDetails, tConseils;
        private readonly CourbeTemps5 courbe = new CourbeTemps5 { Titre = "Capacité à pleine charge au fil du temps (100 % = batterie neuve)", VideTexte = "Pas encore assez d'historique.", Height = 150 };
        private readonly FenetrePrincipale F;

        /// <summary>(captures) batterie d'exemple : rien n'est lu.</summary>
        public static InfosBatterie Exemple;

        public static void Ouvrir(FenetrePrincipale f) { f.Ouvrir("Santé de la batterie", new Batterie5(f), 820); }

        private Batterie5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            haut.Children.Add(jauge);
            var s = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
            tEtat = new TextBlock { Text = Langue.T("Lecture du rapport de batterie de Windows…"), FontFamily = Habillage.Affichage, FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap };
            tDetails = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13.5, LineHeight = 21, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            s.Children.Add(tEtat); s.Children.Add(tDetails);
            Grid.SetColumn(s, 1); haut.Children.Add(s);
            Children.Add(haut);
            Children.Add(courbe);
            tConseils = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13, LineHeight = 20, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tConseils);
            var bRapport = new BoutonNoyau("Rapport complet de Windows", false, 0, 34) { TaillePolice = 13, HorizontalAlignment = HorizontalAlignment.Left };
            bRapport.Clic += () =>
            {
                F.Statut("Windows prépare le rapport de batterie…");
                Task.Run(() => { try { return Batterie.RapportHtml(); } catch { return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    if (t.Result != null) { Programmes.Ouvrir(t.Result); F.Statut("Rapport de batterie ouvert."); }
                    else F.Demander("Rapport de batterie", "Windows n'a pas pu créer le rapport de batterie (pas de batterie détectée ?).", "Compris", null, r => { });
                }));
            };
            Children.Add(bRapport);
            AttachedToVisualTree += (o, e) =>
            {
                if (Exemple != null) { Afficher(Exemple); return; }
                Task.Run(() => { try { return Batterie.Lire(); } catch (Exception ex) { return new InfosBatterie { Erreur = ex.Message }; } }).ContinueWith(t => Dispatcher.UIThread.Post(() => Afficher(t.Result)));
            };
        }

        private void Afficher(InfosBatterie r)
        {
            var P = Habillage.P;
            int niveau;
            tEtat.Text = Langue.T(Batterie.Resume(r, out niveau));
            if (!r.Presente)
            {
                tEtat.Foreground = Habillage.Pinceau(P.Discret);
                tDetails.Text = Langue.T("Cet outil sert aux ordinateurs portables : il mesure l'usure de la batterie et son autonomie réelle.");
                jauge.IsVisible = false; courbe.IsVisible = false; tConseils.IsVisible = false;
                return;
            }
            tEtat.Foreground = Habillage.Pinceau(niveau == 0 ? P.Discret : niveau == 3 ? P.Vert : niveau == 2 ? P.Orange : P.Rouge);
            jauge.Valeur = r.Sante; jauge.InvalidateVisual();
            tDetails.Text = string.Join("\n", Batterie.Details(r));
            courbe.Points = r.Historique.Where(p => p.Item3 > 0).Select(p => Tuple.Create(p.Item1, 100.0 * p.Item2 / p.Item3)).ToList();
            courbe.Seuil = 100; courbe.Format = v => v.ToString("0", Appli.Fr) + " %"; courbe.Actualiser();
            tConseils.Text = string.Join("\n", Batterie.Conseils(r).Select(x => "•  " + Langue.T(x)));
        }
    }

    // ================================================================ Santé prédictive
    internal sealed class Sante5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tResume;
        private readonly StackPanel liste = new StackPanel { Spacing = 10 };
        private readonly BoutonNoyau bActualiser;
        private FenetreInterieure fenetre;
        private bool enCours;

        /// <summary>(captures) indicateurs d'exemple : rien n'est lu.</summary>
        public static List<IndicateurSante> Exemple;

        public static void Ouvrir(FenetrePrincipale f) { var s = new Sante5(f); s.fenetre = f.Ouvrir("Santé prédictive", s, 1000); }

        private Sante5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(Premium5.Intro("Les petits signes qui annoncent une panne, repérés avant qu'elle arrive : erreurs du matériel, du disque, arrêts brutaux, usure des SSD, batterie et chauffe."));
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            tResume = new TextBlock { Text = Langue.T("Analyse en cours…"), FontFamily = Habillage.Affichage, FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(tResume);
            bActualiser = new BoutonNoyau("Actualiser", false, 0, 32) { TaillePolice = 13 };
            bActualiser.Clic += Charger;
            Grid.SetColumn(bActualiser, 1); g.Children.Add(bActualiser);
            Children.Add(g);
            Children.Add(new TextBlock { Text = Langue.T("Les prévisions sont des tendances (« à ce rythme ») : elles aident à anticiper, sans certitude."), FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            Children.Add(liste);
            AttachedToVisualTree += (o, e) => Charger();
        }

        private void Charger()
        {
            if (enCours) return;
            if (Exemple != null) { Afficher(Exemple); return; }
            enCours = true; bActualiser.IsEnabled = false;
            tResume.Text = Langue.T("Analyse en cours…"); tResume.Foreground = Habillage.Pinceau(Habillage.P.Accent);
            Task.Run(() => { try { return Sante.Analyser(); } catch (Exception ex) { JournalDemarrage.Erreur("santé prédictive (version 5)", ex); return new List<IndicateurSante>(); } })
                .ContinueWith(t => Dispatcher.UIThread.Post(() => { enCours = false; bActualiser.IsEnabled = true; Afficher(t.Result ?? new List<IndicateurSante>()); }));
        }

        private void Afficher(List<IndicateurSante> indicateurs)
        {
            var P = Habillage.P;
            int niveau;
            tResume.Text = Sante.Resume(indicateurs, out niveau);
            tResume.Foreground = Habillage.Pinceau(niveau == 2 ? P.Rouge : niveau == 1 ? P.Orange : P.Vert);
            liste.Children.Clear();
            foreach (var i in Sante.Ordonner(indicateurs))
            {
                var ind = i;
                Color c = i.Etat == "probleme" ? P.Rouge : i.Etat == "attention" ? P.Orange : i.Etat == "ok" ? P.Vert : P.Discret;
                bool avecCourbe = i.Points.Count >= (i.Barres ? 1 : 2) && (!i.Barres || i.Points.Any(p => p.Item2 > 0));
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions(avecCourbe ? "28,*,330" : "28,*") };
                g.Children.Add(new TextBlock { Text = i.Etat == "probleme" ? "✖" : i.Etat == "attention" ? "!" : i.Etat == "ok" ? "✔" : "?", FontSize = 16, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(c) });
                var s = new StackPanel { Spacing = 4 };
                s.Children.Add(new TextBlock { Text = Langue.T(i.Nom), FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                if (i.Valeur != "") s.Children.Add(new TextBlock { Text = Langue.T(i.Valeur), FontFamily = Habillage.Corps, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(c), TextWrapping = TextWrapping.Wrap });
                foreach (var t in new[] { Tuple.Create(i.Explication, P.Discret), Tuple.Create(i.Prevision, P.Accent), Tuple.Create(i.Conseil, P.Texte) })
                    if (!string.IsNullOrEmpty(t.Item1)) s.Children.Add(new TextBlock { Text = Langue.T(t.Item1), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(t.Item2), TextWrapping = TextWrapping.Wrap });
                if (i.Bouton != "" && i.Cible != "")
                {
                    var b = new BoutonNoyau(Langue.T(i.Bouton), i.Etat == "probleme" || i.Etat == "inconnu", 0, 32) { TaillePolice = 13, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
                    b.Clic += () => Premium5.Agir(F, fenetre, ind.Cible, ind.Bouton);
                    s.Children.Add(b);
                }
                Grid.SetColumn(s, 1); g.Children.Add(s);
                if (avecCourbe)
                {
                    var courbe = new CourbeTemps5
                    {
                        Titre = i.Unite == "par semaine" ? "par semaine, sur 3 mois" : i.Unite == "°C" ? "maximum du jour" : "évolution",
                        Points = i.Points, Barres = i.Barres, DepuisZero = i.Unite != "°C", Pente = i.Pente, PrevisionJusqua = i.PrevisionJusqua, Seuil = i.Seuil,
                        Format = v => i.Unite == "%" ? v.ToString("0", Appli.Fr) + " %" : i.Unite == "°C" ? v.ToString("0", Appli.Fr) + " °C" : v.ToString(v < 10 ? "0.#" : "0", Appli.Fr),
                        Height = 140, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top,
                    };
                    Grid.SetColumn(courbe, 2); g.Children.Add(courbe);
                }
                liste.Children.Add(Premium5.Carte(g, i.Etat == "probleme" || i.Etat == "attention" ? Habillage.Alpha(c, 110) : (Color?)null));
            }
        }
    }

    // ================================================================ Coût en électricité
    internal sealed class Energie5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBox prix = new TextBox { Width = 90 };
        private readonly TextBlock tEtat, tConseils, tModele;
        private readonly Tuile tJour, tSemaine, tMois, tAn;
        private readonly CourbeTemps5 courbe = new CourbeTemps5 { Titre = "Consommation par jour (kWh), 30 derniers jours", Barres = true, Height = 220, VideTexte = "Pas encore de mesure : le gardien relève l'activité du PC chaque minute." };
        private readonly BarresClassement5 apps = new BarresClassement5 { Titre = "Ce qui consomme le plus (30 derniers jours)", Height = 220 };
        private readonly BoutonNoyau bGardien;
        private List<JourEnergie> jours = new List<JourEnergie>();
        private ModeleEnergie modele;

        /// <summary>(captures) 30 jours de mesures d'exemple : rien n'est lu.</summary>
        public static List<JourEnergie> Exemple;

        private sealed class Tuile : Border
        {
            private readonly TextBlock valeur, libelle;
            public Tuile()
            {
                var P = Habillage.P;
                var s = new StackPanel { Spacing = 2 };
                valeur = new TextBlock { Text = "—", FontFamily = Habillage.Affichage, FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) };
                libelle = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
                s.Children.Add(valeur); s.Children.Add(libelle);
                Child = s; Padding = new Thickness(14, 10); CornerRadius = new CornerRadius(3); Margin = new Thickness(0, 0, 10, 0);
                Background = Habillage.Pinceau(P.Carte); BorderBrush = Habillage.Pinceau(P.Bordure); BorderThickness = new Thickness(1);
            }
            public void Definir(double wh, string titre, string suite = "")
            {
                valeur.Text = wh < 0 ? "—" : Energie.Euros(wh);
                libelle.Text = Langue.T(titre) + suite + (wh < 0 ? "" : " · " + Energie.Kwh(wh));
            }
        }

        public static void Ouvrir(FenetrePrincipale f) { f.Ouvrir("Coût en électricité", new Energie5(f), 1000); }

        private Energie5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("Combien ton PC te coûte en électricité, et quelles applications consomment le plus. C'est une estimation (activité du processeur et de la carte graphique, sans les écrans) : seule une prise wattmètre donne la valeur exacte."));
            var l = new WrapPanel { Orientation = Orientation.Horizontal };
            l.Children.Add(new TextBlock { Text = Langue.T("Prix du kWh (voir ta facture) :"), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) });
            try { prix.Text = Energie.Prix.ToString("0.####", Appli.Fr); } catch { prix.Text = "0,2"; }
            prix.LostFocus += (s, e) => EnregistrerPrix();
            prix.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; EnregistrerPrix(); } };
            l.Children.Add(prix);
            l.Children.Add(new TextBlock { Text = "€", FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 18, 0) });
            tEtat = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            l.Children.Add(tEtat);
            Children.Add(l);
            var tuiles = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
            tJour = new Tuile(); tSemaine = new Tuile(); tMois = new Tuile(); tAn = new Tuile();
            int k = 0; foreach (var t in new[] { tJour, tSemaine, tMois, tAn }) { Grid.SetColumn(t, k++); tuiles.Children.Add(t); }
            tAn.Margin = new Thickness(0);
            Children.Add(tuiles);
            var graph = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
            graph.Children.Add(courbe);
            Grid.SetColumn(apps, 2); graph.Children.Add(apps);
            Children.Add(graph);
            Children.Add(Premium5.Titre("Pour payer moins"));
            tConseils = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13, LineHeight = 20, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            Children.Add(tConseils);
            tModele = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tModele);
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            bGardien = new BoutonNoyau("Régler le gardien", false, 0, 34) { TaillePolice = 13 };
            bGardien.Clic += () => Gardien5.Ouvrir(F);
            var bVeille = new BoutonNoyau("Réglages de mise en veille", false, 0, 34) { TaillePolice = 13 };
            bVeille.Clic += () => Programmes.Ouvrir("ms-settings:powersleep");
            boutons.Children.Add(bGardien); boutons.Children.Add(bVeille);
            Children.Add(boutons);
            AttachedToVisualTree += (o, e) => Charger();
        }

        private void EnregistrerPrix()
        {
            double p;
            if (double.TryParse((prix.Text ?? "").Trim().Replace(',', '.').Replace("€", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out p) && p > 0 && p < 5) { if (Exemple == null) Energie.Prix = p; Afficher(); }
            else prix.Text = Energie.Prix.ToString("0.####", Appli.Fr);
        }

        private void Charger()
        {
            if (Exemple != null) { jours = Exemple; modele = new ModeleEnergie { Processeur = "AMD Ryzen 7 7800X3D", Tdp = 120, Carte = "AMD Radeon RX 7900 XTX", Tbp = 355, Base = 45 }; Afficher(); return; }
            Task.Run(() => { List<JourEnergie> l = null; ModeleEnergie m = null; try { l = Energie.Lire(); m = Energie.Modele(); } catch { } return Tuple.Create(l, m); })
                .ContinueWith(t => Dispatcher.UIThread.Post(() => { jours = t.Result.Item1 ?? new List<JourEnergie>(); modele = t.Result.Item2; Afficher(); }));
        }

        private void Afficher()
        {
            var P = Habillage.P;
            bool actif;
            string etat = Energie.EtatMesure(out actif);
            if (Exemple != null) { etat = "● Mesure en cours (le gardien relève l'activité chaque minute)"; actif = true; }
            tEtat.Text = Langue.T(etat); tEtat.Foreground = Habillage.Pinceau(actif ? P.Vert : P.Orange);
            bGardien.Principal = !actif; bGardien.InvalidateVisual();
            var auj = jours.FirstOrDefault(j => j.Jour == DateTime.Today);
            var sem = jours.Where(j => j.Jour > DateTime.Today.AddDays(-7)).ToList();
            var mois = jours.Where(j => j.Jour > DateTime.Today.AddDays(-30)).ToList();
            int nbJours = mois.Count(j => j.Minutes >= 30);
            double parJour = nbJours > 0 ? mois.Where(j => j.Minutes >= 30).Average(j => j.Wh) : 0;
            tJour.Definir(auj == null ? -1 : auj.Wh, "Aujourd'hui", auj != null ? " · " + Langue.F("{0} h allumé", (auj.Minutes / 60.0).ToString("0.#", Appli.Fr)) : "");
            tSemaine.Definir(sem.Count == 0 ? -1 : sem.Sum(j => j.Wh), "7 derniers jours");
            tMois.Definir(mois.Count == 0 ? -1 : mois.Sum(j => j.Wh), "30 derniers jours");
            tAn.Definir(nbJours >= 3 ? parJour * 365 : -1, "Sur un an, à ce rythme");
            courbe.Debut = DateTime.Today.AddDays(-29);
            courbe.Points = mois.Select(j => Tuple.Create(j.Jour, j.Wh / 1000)).ToList();
            courbe.Format = v => v.ToString("0.#", Appli.Fr);
            courbe.Actualiser();
            apps.Lignes = Energie.ParApplication(mois).Take(8).Select(a => Tuple.Create(a.Key == Energie.NomSysteme ? Langue.T("Windows et PC allumé (au repos)") : a.Key, a.Value, Energie.Euros(a.Value))).ToList();
            apps.InvalidateVisual();
            tConseils.Text = string.Join("\n", Energie.Conseils(mois));
            tModele.Text = modele != null ? Energie.TexteModele(modele) : "";
        }
    }
}
