// Nettoyage PC 5 - Windows et pilotes : pilote de la carte graphique (outil officiel du fabricant) et mises à jour de
// Windows Update triées (ce qui est recommandé est coché, le reste est expliqué). Installation par le moteur, question avant.
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
    internal sealed class Pilotes5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tGpu, tPilote, tInfo;
        private readonly BoutonNoyau bOutil, bSite, bChercher, bInstaller;
        private readonly Tableau tableau;
        private readonly HashSet<MajWindows> coches = new HashSet<MajWindows>();
        private readonly Dictionary<MajWindows, ClassementMaj> classes = new Dictionary<MajWindows, ClassementMaj>();
        private List<CarteGraphique> gpus = new List<CarteGraphique>();
        private List<MajWindows> maj = new List<MajWindows>();
        private OutilPilote outil = new OutilPilote();
        private bool recherche;

        /// <summary>(captures) carte graphique et mises à jour d'exemple : rien n'est demandé à Windows Update.</summary>
        public static Tuple<List<CarteGraphique>, List<MajWindows>> Exemple;

        public Pilotes5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12; Margin = new Thickness(0, 0, 12, 0);
            var carte = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var s = new StackPanel { Spacing = 5 };
            s.Children.Add(new TextBlock { Text = Langue.T("Carte graphique").ToUpperInvariant(), FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(P.Discret) });
            tGpu = new TextBlock { Text = Langue.T("Recherche de la carte graphique…"), FontFamily = Habillage.Affichage, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            tPilote = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13, LineHeight = 19, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            s.Children.Add(tGpu); s.Children.Add(tPilote);
            carte.Children.Add(s);
            var boutons = new StackPanel { Spacing = 8, Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            bOutil = new BoutonNoyau("Mettre à jour le pilote", true, 250, 38) { TaillePolice = 14, IsVisible = false };
            bOutil.Clic += OuvrirOutil;
            bSite = new BoutonNoyau("Site officiel du fabricant", false, 250, 32) { TaillePolice = 13, IsVisible = false };
            bSite.Clic += () => { if (outil.Site != "") Programmes.Ouvrir(outil.Site); };
            boutons.Children.Add(bOutil); boutons.Children.Add(bSite);
            Grid.SetColumn(boutons, 1); carte.Children.Add(boutons);
            Children.Add(new Border { Child = carte, Padding = new Thickness(20, 16), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1) });

            Children.Add(new TextBlock { Text = Langue.T("Mises à jour Windows et pilotes"), FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), Margin = new Thickness(0, 8, 0, 0) });
            var rangee = new WrapPanel { Orientation = Orientation.Horizontal };
            bChercher = new BoutonNoyau("Rechercher", false, 0, 34) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 10, 6) };
            bChercher.Clic += Rechercher;
            bInstaller = new BoutonNoyau("Installer la sélection", true, 0, 34) { TaillePolice = 13.5, IsEnabled = false, Margin = new Thickness(0, 0, 10, 6) };
            bInstaller.Clic += Installer;
            var bWu = new BoutonNoyau("Ouvrir Windows Update", false, 0, 34) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bWu.Clic += () => Programmes.Ouvrir("ms-settings:windowsupdate");
            var bApps = new BoutonNoyau("Mettre à jour les applications", false, 0, 34) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            ToolTip.SetTip(bApps, Langue.T("Met à jour tes logiciels avec winget, l'outil officiel de Microsoft. Corrige aussi des failles de sécurité."));
            bApps.Clic += () => F.LancerAction("majapps", () => { AfficherGpu(); Rechercher(); });
            rangee.Children.Add(bChercher); rangee.Children.Add(bInstaller); rangee.Children.Add(bWu); rangee.Children.Add(bApps);
            Children.Add(rangee);
            tableau = new Tableau(true, false, new Colonne("Mise à jour", "3*"), new Colonne("Type", "130"), new Colonne("Taille", "90", true, true, o => ((MajWindows)o).Taille), new Colonne("Conseil", "2.4*"));
            tableau.HauteurListe = 300;
            tableau.CaseChangee = (o, v) => { var m = (MajWindows)o; if (v) coches.Add(m); else coches.Remove(m); Resume(); return true; };
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            tInfo = new TextBlock { Text = Langue.T("Clique sur « Rechercher » : Nettoyage PC demande à Windows Update la liste des mises à jour et pilotes disponibles, et coche ce qui est recommandé."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tInfo);
            AttachedToVisualTree += (o, e) => { AfficherGpu(); Rechercher(); };
        }

        private void Info(string t, Color c) { tInfo.Text = t; tInfo.Foreground = Habillage.Pinceau(c); }

        private void AfficherGpu()
        {
            if (Exemple != null) { MontrerGpu(Exemple.Item1); return; }
            Task.Run(() => { try { return InfosPc.CartesGraphiques(); } catch { return new List<CarteGraphique>(); } }).ContinueWith(t => Dispatcher.UIThread.Post(() => MontrerGpu(t.Result)));
        }

        private void MontrerGpu(List<CarteGraphique> l)
        {
            var P = Habillage.P;
            gpus = l ?? new List<CarteGraphique>();
            bool vieux;
            tPilote.Text = Langue.T(Pilotes.ResumeGpu(gpus, out vieux));
            tPilote.Foreground = Habillage.Pinceau(vieux ? P.Orange : P.Discret);
            if (gpus.Count == 0) { tGpu.Text = Langue.T("Aucune carte graphique détectée"); bOutil.IsVisible = bSite.IsVisible = false; return; }
            tGpu.Text = gpus[0].Nom;
            outil = OutilPilote.Pour(gpus[0].Marque);
            bOutil.Changer(outil.Bouton); bOutil.IsVisible = true;
            bSite.IsVisible = outil.Site != "" && outil.Site.StartsWith("http");
            if (maj.Count > 0) AfficherMaj();   // le conseil des pilotes graphiques dépend de la carte
        }

        private void OuvrirOutil()
        {
            if (outil.Exe != "") { Programmes.Ouvrir(outil.Exe); F.Demander(Langue.T("Pilote graphique"), outil.Explication, "Compris", null, r => { }); }
            else if (outil.Site != "") Programmes.Ouvrir(outil.Site);
        }

        private void Rechercher()
        {
            var P = Habillage.P;
            if (recherche) return;
            if (Exemple != null) { maj = Exemple.Item2; AfficherMaj(); return; }
            recherche = true; bChercher.IsEnabled = false; bInstaller.IsEnabled = false;
            Info(Langue.T("Recherche auprès de Windows Update… (cela peut prendre une ou deux minutes)"), P.Accent);
            string erreur = null;
            Task.Run(() => { try { return Pilotes.ChercherMaj(out erreur); } catch (Exception ex) { erreur = ex.Message; return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                recherche = false; bChercher.IsEnabled = true;
                if (t.Result == null) { Info(Langue.T("Windows Update ne répond pas (service arrêté ou pas de connexion Internet).") + " " + erreur, Habillage.P.Rouge); return; }
                maj = t.Result;
                AfficherMaj();
            }));
        }

        private void AfficherMaj()
        {
            classes.Clear(); coches.Clear();
            foreach (var m in maj) { var c = Pilotes.Classer(m, gpus, outil.Bouton); classes[m] = c; if (c.Coche) coches.Add(m); }
            tableau.Remplir(maj.OrderBy(m => classes[m].Ordre).ThenBy(m => m.Titre).Cast<object>(), o =>
            {
                var m = (MajWindows)o; var c = classes[m];
                return new[] { m.Titre, Langue.T(c.Type), m.Taille > 0 ? Format.Taille(m.Taille) : "", Langue.T(c.Conseil) };
            }, o => coches.Contains((MajWindows)o), o => !classes[(MajWindows)o].Coche);
            Resume();
        }

        private void Resume()
        {
            var P = Habillage.P;
            if (maj.Count == 0) { Info(Langue.T("Windows est à jour : aucune mise à jour ni pilote en attente. ✔"), P.Vert); bInstaller.IsEnabled = false; bInstaller.Changer(Langue.T("Installer la sélection")); return; }
            double total = coches.Sum(m => m.Taille);
            Info(Langue.F("{0} mise(s) à jour disponible(s) ; {1} cochée(s) ({2}). Coche ou décoche, puis « Installer la sélection ».", maj.Count, coches.Count, Format.Taille(total)), P.Discret);
            bInstaller.IsEnabled = coches.Count > 0;
            bInstaller.Changer(Langue.T("Installer la sélection") + (coches.Count > 0 ? " (" + coches.Count + ")" : ""));
        }

        private void Installer()
        {
            var choix = maj.Where(m => coches.Contains(m)).ToList();
            if (choix.Count == 0) { F.Statut("Coche au moins une mise à jour dans la liste."); return; }
            string question;
            var args = Pilotes.ArgumentsInstallation(choix, out question);
            F.LancerAction("majwin", args, question, () => { AfficherGpu(); Rechercher(); });
        }
    }
}
