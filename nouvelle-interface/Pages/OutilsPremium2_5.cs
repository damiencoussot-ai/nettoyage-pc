// Nettoyage PC 5 - outils Premium de la 5.2 : mode jeu automatique, mises à jour automatiques des applications,
// photos en double et presque identiques, bulletin mensuel du PC, surveillance du réseau.
// Le calcul est dans le moteur partagé (Noyau) ; ici, seulement l'affichage et les choix de l'utilisateur.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal static class OutilsPremium2
    {
        /// <summary>Change un réglage du gardien et prévient le gardien qui tourne.</summary>
        public static void Regler(Action<ReglagesGardien> m)
        {
            try { var r = ReglagesGardien.Lire(); m(r); r.Ecrire(); Gardien.Recharger(); } catch (Exception ex) { JournalDemarrage.Erreur("réglage du gardien (version 5)", ex); }
        }

        public static CheckBox Case(string texte, bool coche, Action<bool> change)
        {
            var c = new CheckBox { Content = new TextBlock { Text = Langue.T(texte), TextWrapping = TextWrapping.Wrap, FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(Habillage.P.Texte) }, IsChecked = coche };
            c.IsCheckedChanged += (s, e) => change(c.IsChecked == true);
            return c;
        }

        /// <summary>Le gardien fait tourner ces outils : s'il est arrêté, une ligne le dit avec un bouton pour le régler.</summary>
        public static Control NoteGardien(FenetrePrincipale f, FenetreInterieure fen)
        {
            ReglagesGardien r; try { r = ReglagesGardien.Lire(); } catch { r = new ReglagesGardien(); }
            if (r.Actif || Gardien5.Exemple != null) return new Border();
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            g.Children.Add(new TextBlock { Text = Langue.T("Le gardien est arrêté : c'est lui qui fait ce travail en arrière-plan. Active-le pour que ça marche tout seul."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(Habillage.P.Orange), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center });
            var b = new BoutonNoyau("Régler le gardien", false, 170, 32) { TaillePolice = 13, Margin = new Thickness(12, 0, 0, 0) };
            b.Clic += () => { if (fen != null) fen.Fermer(); Gardien5.Ouvrir(f); };
            Grid.SetColumn(b, 1); g.Children.Add(b);
            return Premium5.Carte(g, Habillage.Alpha(Habillage.P.Orange, 120));
        }

        public static Border Cadre(Control c) { return new Border { Child = c, BorderBrush = Habillage.Pinceau(Habillage.P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true }; }
    }

    // ================================================================== Mode jeu automatique
    internal sealed class ModeJeu5 : StackPanel
    {
        /// <summary>(captures) parties d'exemple.</summary>
        public static List<SessionJeu> Exemple;

        public static void Ouvrir(FenetrePrincipale f) { FenetreInterieure fen = null; var c = new ModeJeu5(f, () => fen); fen = f.Ouvrir("Mode jeu automatique", c, 860); }

        private ModeJeu5(FenetrePrincipale f, Func<FenetreInterieure> fen)
        {
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("Quand tu lances un jeu (Steam, Epic, Xbox, Riot...), le gardien passe le PC en mode « Performances élevées » et peut mettre OneDrive en pause pour qu'il ne télécharge rien pendant la partie. Quand le jeu se ferme, tout revient comme avant. Rien d'autre n'est fermé ni modifié."));
            ReglagesGardien r; try { r = ReglagesGardien.Lire(); } catch { r = new ReglagesGardien(); }
            bool actif = Exemple != null || r.ModeJeuAuto;
            var s = new StackPanel { Spacing = 8 };
            s.Children.Add(OutilsPremium2.Case("Activer le mode jeu automatique", actif, v => OutilsPremium2.Regler(x => x.ModeJeuAuto = v)));
            s.Children.Add(OutilsPremium2.Case("Mettre OneDrive en pause pendant les jeux (il reprend tout seul à la fin)", Exemple != null || r.PauseOneDrive, v => OutilsPremium2.Regler(x => x.PauseOneDrive = v)));
            string etat = ModeJeuAuto.Actif ? Langue.F("Mode jeu actif en ce moment pour « {0} ».", ModeJeuAuto.JeuActif()) : Langue.T("Windows met déjà les notifications en sourdine pendant les jeux en plein écran : rien à régler de ce côté.");
            s.Children.Add(new TextBlock { Text = etat, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(ModeJeuAuto.Actif ? P.Vert : P.Discret), TextWrapping = TextWrapping.Wrap });
            Children.Add(Premium5.Carte(s, Habillage.Alpha(P.Accent, 90)));
            Children.Add(OutilsPremium2.NoteGardien(f, fen()));
            Children.Add(Premium5.Titre("Dernières parties"));
            var t = new Tableau(false, false, new Colonne("Jeu", "2.2*"), new Colonne("Date", "140", false, true, o => ((SessionJeu)o).Debut), new Colonne("Durée", "90", true, true, o => ((SessionJeu)o).Duree), new Colonne("Ce qui a été fait", "2*"));
            t.HauteurListe = 260;
            var sessions = Exemple ?? ModeJeuAuto.Sessions();
            t.Remplir(sessions.Cast<object>(), o =>
            {
                var x = (SessionJeu)o;
                var faits = new List<string>(); if (x.Performances) faits.Add(Langue.T("performances élevées")); if (x.OneDrive) faits.Add(Langue.T("OneDrive en pause"));
                return new[] { x.Jeu, x.Debut.ToString("dd/MM/yyyy HH:mm"), ModeJeuAuto.TexteDuree(x.Duree), faits.Count > 0 ? string.Join(", ", faits) : Langue.T("déjà en mode performances") };
            });
            Children.Add(OutilsPremium2.Cadre(t));
            if (sessions.Count == 0) Children.Add(Premium5.Intro("Aucune partie pour l'instant : lance un jeu, le gardien s'occupe du reste."));
            Children.Add(Premium5.Intro("Pour voir le gain en images par seconde, mesure un jeu avec la Preuve en FPS avec et sans le mode jeu."));
        }
    }

    // ================================================================== Mises à jour automatiques des applications
    internal sealed class MajApps5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Tableau tableau;
        private readonly TextBlock tEtat, info;
        private readonly BoutonNoyau bActiver;
        private List<AppAMettreAJour> apps = new List<AppAMettreAJour>();

        /// <summary>(captures) applications d'exemple.</summary>
        public static List<AppAMettreAJour> Exemple;

        public static void Ouvrir(FenetrePrincipale f) { f.Ouvrir("Mises à jour automatiques des applications", new MajApps5(f), 900); }

        private MajApps5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("Chaque mercredi à 19h, winget (l'outil officiel de Microsoft) met à jour tes applications : navigateurs, Discord, VLC, 7-Zip... Les mises à jour corrigent souvent des failles de sécurité. Décoche une application pour la garder dans sa version actuelle."));
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            tEtat = new TextBlock { FontFamily = Habillage.Affichage, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            g.Children.Add(tEtat);
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(14, 0, 0, 0) };
            bActiver = new BoutonNoyau("Activer", true, 150, 36) { TaillePolice = 14 };
            bActiver.Clic += () => F.LancerAction(Active ? "deplanifierapps" : "planifierapps", () => Dispatcher.UIThread.Post(AfficherEtat));
            var bMaintenant = new BoutonNoyau("Mettre à jour maintenant", false, 210, 36) { TaillePolice = 14 };
            bMaintenant.Clic += () => F.LancerAction("majapps");
            boutons.Children.Add(bActiver); boutons.Children.Add(bMaintenant);
            Grid.SetColumn(boutons, 1); g.Children.Add(boutons);
            Children.Add(Premium5.Carte(g, Habillage.Alpha(P.Accent, 90)));
            Children.Add(Premium5.Titre("Mises à jour disponibles"));
            tableau = new Tableau(true, false, new Colonne("Application", "2.2*"), new Colonne("Version installée", "140", false, true), new Colonne("Nouvelle version", "140", false, true), new Colonne("Identifiant winget", "1.8*"));
            tableau.HauteurListe = 300;
            tableau.CaseChangee = Changer;
            Children.Add(OutilsPremium2.Cadre(tableau));
            info = Premium5.Intro("Recherche des mises à jour (winget)…");
            Children.Add(info);
            AfficherEtat();
            Charger();
        }

        private static bool active;
        private bool Active { get { return Exemple != null || active; } }

        private void AfficherEtat()
        {
            try { active = Exemple == null && MajAppsAuto.Active; } catch { active = false; }
            var d = MajAppsAuto.Derniere();
            tEtat.Text = Active ? Langue.T("Activé : chaque mercredi à 19h.") + (d.HasValue ? " " + Langue.F("Dernière fois : {0}.", d.Value.ToString("dd/MM/yyyy HH:mm")) : "")
                : Langue.T("Désactivé : les applications ne se mettent à jour que si tu le demandes.");
            tEtat.Foreground = Habillage.Pinceau(Active ? Habillage.P.Vert : Habillage.P.Texte);
            bActiver.Changer(Active ? "Désactiver" : "Activer");
        }

        private void Charger()
        {
            Task.Run(() => { string e; return Exemple ?? MajAppsAuto.Lister(out e); }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                var l = t.Result;
                if (l == null) { info.Text = Langue.T("winget (Gestionnaire de package Windows) est introuvable. Installe « App Installer » depuis le Microsoft Store."); return; }
                apps = l;
                tableau.Remplir(apps.Cast<object>(), o => { var a = (AppAMettreAJour)o; return new[] { a.Nom, a.Version, a.Disponible, a.Id }; }, o => !((AppAMettreAJour)o).Exclue, o => ((AppAMettreAJour)o).Exclue);
                int exclues = apps.Count(a => a.Exclue);
                info.Text = apps.Count == 0 ? Langue.T("Toutes tes applications connues de winget sont à jour.")
                    : Langue.F("{0} mise(s) à jour disponible(s), {1} application(s) exclue(s). Les jeux et les logiciels qui se mettent à jour eux-mêmes ne sont pas concernés.", apps.Count, exclues);
            }));
        }

        private bool Changer(object o, bool coche)
        {
            var a = (AppAMettreAJour)o;
            if (Exemple != null) { a.Exclue = !coche; return true; }
            F.Statut(Langue.F(coche ? "« {0} » sera de nouveau mise à jour…" : "« {0} » restera dans sa version actuelle…", a.Nom));
            Task.Run(() => MajAppsAuto.Exclure(a.Id, !coche)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (t.Result) { a.Exclue = !coche; F.Statut(Langue.F(coche ? "« {0} » sera de nouveau mise à jour." : "« {0} » est exclue des mises à jour automatiques.", a.Nom)); }
                else { F.Statut(Langue.F("winget a refusé le changement pour « {0} ».", a.Nom), true); Charger(); }
            }));
            return true;
        }
    }

    // ================================================================== Photos en double et presque identiques
    internal sealed class Photos5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly ComboBox ou = new ComboBox { MinWidth = 380 };
        private readonly Tableau tGroupes, tPhotos;
        private readonly WrapPanel vignettes = new WrapPanel { Orientation = Orientation.Horizontal };
        private readonly TextBlock info;
        private readonly BoutonNoyau bChercher, bSuppr;
        private readonly EtatPhotos etat = new EtatPhotos();
        private readonly DispatcherTimer minuteur = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        private List<GroupePhotos> groupes = new List<GroupePhotos>();
        private string dossierPerso = "";
        private bool enCours;

        /// <summary>(captures) groupes d'exemple (sans vignettes).</summary>
        public static List<GroupePhotos> Exemple;

        public static void Ouvrir(FenetrePrincipale f) { var p = new Photos5(f); var fen = f.Ouvrir("Photos en double et presque identiques", p, 980); fen.Fermee += () => p.etat.Annuler = true; }

        private Photos5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(Premium5.Intro("Retrouve les rafales, les copies redimensionnées et les photos un peu retouchées. Nettoyage PC propose de garder la meilleure (la plus grande définition) : vérifie, change les cases si besoin, puis confirme. Les autres vont à la Corbeille, d'où tu peux les récupérer."));
            var ligne = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var t in new[] { "Mes images (dossier Images)", "Choisir un dossier…" }) ou.Items.Add(Langue.T(t));
            ou.SelectedIndex = 0; ou.Margin = new Thickness(0, 0, 12, 6);
            ou.SelectionChanged += async (s, e) =>
            {
                if (ou.SelectedIndex != 1) return;
                try
                {
                    var top = TopLevel.GetTopLevel(this);
                    var choix = top == null ? null : await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Langue.T("Dossier où chercher les photos"), AllowMultiple = false });
                    var d = choix != null ? choix.FirstOrDefault() : null;
                    string chemin = d != null ? d.TryGetLocalPath() : null;
                    if (!string.IsNullOrEmpty(chemin)) { dossierPerso = chemin; ou.Items[1] = Langue.T("Dossier :") + " " + chemin; ou.SelectedIndex = 1; }
                    else if (dossierPerso == "") ou.SelectedIndex = 0;
                }
                catch { ou.SelectedIndex = 0; }
            };
            ligne.Children.Add(ou);
            bChercher = new BoutonNoyau("Rechercher les photos", true, 200, 34) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 0, 6) };
            bChercher.Clic += Rechercher;
            ligne.Children.Add(bChercher);
            Children.Add(ligne);
            tGroupes = new Tableau(false, false, new Colonne("Groupe de photos", "2.4*"), new Colonne("Photos", "70", true, true, o => ((GroupePhotos)o).Photos.Count),
                new Colonne("Récupérable", "110", true, true, o => ((GroupePhotos)o).Recuperable), new Colonne("Ressemblance", "150"));
            tGroupes.HauteurListe = 200;
            tGroupes.SelectionChangee += () => AfficherGroupe(tGroupes.Selection as GroupePhotos);
            Children.Add(OutilsPremium2.Cadre(tGroupes));
            Children.Add(vignettes);
            tPhotos = new Tableau(true, false, new Colonne("Corbeille ?", "2*"), new Colonne("Définition", "120", true, true, o => ((PhotoSimilaire)o).Pixels),
                new Colonne("Taille", "90", true, true, o => ((PhotoSimilaire)o).Taille), new Colonne("Modifiée le", "130", false, true, o => ((PhotoSimilaire)o).Modif), new Colonne("Dossier", "2*"));
            tPhotos.HauteurListe = 170;
            tPhotos.CaseChangee = (o, coche) =>
            {
                var p = (PhotoSimilaire)o;
                var g = groupes.FirstOrDefault(x => x.Photos.Contains(p));
                if (coche && g != null && g.Photos.Count(x => x.Supprimer || x == p) >= g.Photos.Count) { F.Statut("Garde au moins une photo de chaque groupe.", true); return false; }
                p.Supprimer = coche; Totaux(); tGroupes.Rafraichir(); return true;
            };
            Children.Add(OutilsPremium2.Cadre(tPhotos));
            var bas = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            info = new TextBlock { Text = Langue.T("Choisis où chercher, puis clique sur « Rechercher les photos »."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            bas.Children.Add(info);
            bSuppr = new BoutonNoyau("Mettre à la Corbeille", true, 260, 36) { TaillePolice = 14, IsEnabled = false, Margin = new Thickness(12, 0, 0, 0) };
            bSuppr.Clic += Supprimer;
            Grid.SetColumn(bSuppr, 1); bas.Children.Add(bSuppr);
            Children.Add(bas);
            minuteur.Tick += (s, e) => { if (enCours) info.Text = Langue.F("Analyse des photos : {0} sur {1}…", etat.Faites, etat.Total); };
            if (Exemple != null) { groupes = Exemple; Afficher(); }
        }

        private void Rechercher()
        {
            if (enCours) { etat.Annuler = true; return; }
            string racine = ou.SelectedIndex == 1 && dossierPerso != "" ? dossierPerso : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (string.IsNullOrEmpty(racine) || !Directory.Exists(racine)) { F.Statut("Dossier introuvable.", true); return; }
            enCours = true; etat.Annuler = false; etat.Total = 0; etat.Faites = 0;
            bChercher.Changer("Arrêter"); bSuppr.IsEnabled = false; minuteur.Start();
            info.Text = Langue.T("Liste des photos…");
            Task.Run(() => { try { return PhotosSimilaires.Chercher(racine, etat); } catch (Exception ex) { JournalDemarrage.Erreur("photos similaires (version 5)", ex); return new List<GroupePhotos>(); } })
                .ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    enCours = false; minuteur.Stop(); bChercher.Changer("Rechercher les photos");
                    groupes = t.Result; Afficher();
                    if (etat.Annuler) info.Text = Langue.T("Recherche arrêtée.") + " " + info.Text;
                }));
        }

        private void Afficher()
        {
            tGroupes.Remplir(groupes.Cast<object>(), o =>
            {
                var g = (GroupePhotos)o;
                return new[] { Langue.F("{0} (+ {1})", g.Photos[0].Nom, g.Photos.Count - 1), g.Photos.Count.ToString(), Format.Taille(g.Recuperable), Langue.T(g.Identiques ? "identiques" : "presque identiques") };
            });
            if (groupes.Count > 0) tGroupes.Selectionner(groupes[0]); else { tPhotos.Remplir(new object[0], o => new string[0]); vignettes.Children.Clear(); }
            Totaux();
        }

        private void Totaux()
        {
            int n = groupes.Sum(g => g.Photos.Count(p => p.Supprimer)); long o = groupes.Sum(g => g.Recuperable);
            info.Text = groupes.Count == 0 ? (etat.Total > 0 ? Langue.F("{0} photos analysées : aucune en double.", etat.Total) : info.Text)
                : Langue.F("{0} groupe(s) de photos qui se ressemblent. Proposition : {1} photo(s) à la Corbeille, {2} récupérables.", groupes.Count, n, Format.Taille(o));
            bSuppr.IsEnabled = n > 0 && !enCours && Exemple == null;
            bSuppr.Changer(n > 0 ? Langue.F("Mettre {0} photo(s) à la Corbeille", n) : "Mettre à la Corbeille");
        }

        private void AfficherGroupe(GroupePhotos g)
        {
            vignettes.Children.Clear();
            if (g == null) return;
            tPhotos.Remplir(g.Photos.Cast<object>(), o =>
            {
                var p = (PhotoSimilaire)o;
                return new[] { p.Supprimer ? p.Nom : Langue.F("{0}   ✔ gardée", p.Nom), p.Largeur + " × " + p.Hauteur, Format.Taille(p.Taille), p.Modif.ToString("dd/MM/yyyy HH:mm"), p.Dossier };
            }, o => ((PhotoSimilaire)o).Supprimer);
            foreach (var p in g.Photos.Take(6))
            {
                var cadre = new Border { Width = 150, Height = 112, Margin = new Thickness(0, 0, 8, 4), CornerRadius = new CornerRadius(3), ClipToBounds = true, BorderThickness = new Thickness(2), BorderBrush = Habillage.Pinceau(p.Supprimer ? Habillage.P.Bordure : Habillage.P.Vert), Background = Habillage.Pinceau(Habillage.P.Carte) };
                vignettes.Children.Add(cadre);
                string chemin = p.Chemin;
                Task.Run(() => { try { using (var fs = File.OpenRead(chemin)) return Avalonia.Media.Imaging.Bitmap.DecodeToWidth(fs, 300); } catch { return null; } })
                    .ContinueWith(t => Dispatcher.UIThread.Post(() => { if (t.Result != null) cadre.Child = new Image { Source = t.Result, Stretch = Stretch.UniformToFill }; }));
            }
        }

        private void Supprimer()
        {
            int n = groupes.Sum(g => g.Photos.Count(p => p.Supprimer)); long o = groupes.Sum(g => g.Recuperable);
            if (n == 0) return;
            F.Demander(Langue.F("Mettre {0} photo(s) à la Corbeille ?", n), Langue.F("{0} seront libérés quand tu videras la Corbeille. Tu peux récupérer une photo à tout moment depuis la Corbeille de Windows. Dans chaque groupe, au moins une photo est toujours gardée.", Format.Taille(o)), "Mettre à la Corbeille", "Annuler", oui =>
            {
                if (!oui) return;
                if (F.Simulation) { F.Statut(Langue.F("Simulation : {0} photo(s) iraient à la Corbeille, rien n'est modifié.", n)); return; }
                bSuppr.IsEnabled = false;
                var copie = groupes.ToList();
                Task.Run(() => PhotosSimilaires.MettreACorbeille(copie)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    F.Statut(Langue.F("{0} photo(s) mises à la Corbeille ({1}).", t.Result.Item1, Format.Taille(t.Result.Item2)));
                    foreach (var g in groupes) g.Photos.RemoveAll(p => p.Supprimer && !File.Exists(p.Chemin));
                    groupes = groupes.Where(g => g.Photos.Count > 1).ToList();
                    Afficher();
                }));
            });
        }
    }

    // ================================================================== Bulletin mensuel du PC
    internal sealed class Bulletin5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Tableau tableau;
        private readonly BoutonNoyau bCreer;

        /// <summary>(captures) mois d'exemple déjà créés.</summary>
        public static List<string> Exemple;

        public static void Ouvrir(FenetrePrincipale f) { FenetreInterieure fen = null; var b = new Bulletin5(f, () => fen); fen = f.Ouvrir("Bulletin mensuel du PC", b, 820); }

        private Bulletin5(FenetrePrincipale f, Func<FenetreInterieure> fen)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("Chaque mois, une page claire sur la santé de ton PC : note, espace gagné, temps de démarrage, températures, santé du matériel et parties jouées. Elle reste sur ce PC (Documents\\NettoyagePC\\Bulletins) : garde-la, imprime-la ou envoie-la à la personne qui t'aide."));
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            ReglagesGardien r; try { r = ReglagesGardien.Lire(); } catch { r = new ReglagesGardien(); }
            g.Children.Add(OutilsPremium2.Case("Créer le bulletin automatiquement au début de chaque mois (gardien)", Exemple != null || r.Bulletin, v => OutilsPremium2.Regler(x => x.Bulletin = v)));
            bCreer = new BoutonNoyau("Créer le bulletin de ce mois", true, 250, 36) { TaillePolice = 14, Margin = new Thickness(12, 0, 0, 0) };
            bCreer.Clic += Creer;
            Grid.SetColumn(bCreer, 1); g.Children.Add(bCreer);
            Children.Add(Premium5.Carte(g, Habillage.Alpha(P.Accent, 90)));
            Children.Add(OutilsPremium2.NoteGardien(f, fen()));
            Children.Add(Premium5.Titre("Bulletins enregistrés"));
            tableau = new Tableau(false, false, new Colonne("Mois", "2*"), new Colonne("Créé le", "160", false, true), new Colonne("Fichier", "2*"));
            tableau.HauteurListe = 220;
            tableau.DoubleClic += o => { var fi = o as FileInfo; if (fi != null) Programmes.Ouvrir(fi.FullName); };
            Children.Add(OutilsPremium2.Cadre(tableau));
            Children.Add(Premium5.Intro("Double-clique sur un bulletin pour l'ouvrir dans ton navigateur (Ctrl+P pour l'imprimer ou l'enregistrer en PDF)."));
            Charger();
        }

        private void Charger()
        {
            if (Exemple != null)
            {
                int k = 0;
                tableau.Remplir(Exemple.Cast<object>(), o => { k++; return new[] { (string)o, (k == 1 ? "01/10/2026" : k == 2 ? "01/09/2026" : "01/08/2026") + " 09:12", "Bulletin-2026-" + (11 - k).ToString("00") + ".html" }; });
                return;
            }
            var l = Bulletin.Liste();
            tableau.Remplir(l.Cast<object>(), o =>
            {
                var fi = (FileInfo)o; DateTime m;
                string mois = DateTime.TryParseExact(fi.Name.Replace("Bulletin-", "").Replace(".html", ""), "yyyy-MM", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out m) ? m.ToString("MMMM yyyy", Langue.Culture) : fi.Name;
                return new[] { mois, fi.LastWriteTime.ToString("dd/MM/yyyy HH:mm"), fi.Name };
            });
        }

        private void Creer()
        {
            if (Exemple != null) return;
            bCreer.IsEnabled = false;
            F.Statut("Création du bulletin (bilan, santé du matériel)…");
            Task.Run(() => { try { return Bulletin.Creer(DateTime.Now); } catch (Exception ex) { JournalDemarrage.Erreur("bulletin (version 5)", ex); return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                bCreer.IsEnabled = true;
                if (t.Result == null) { F.Statut("Le bulletin n'a pas pu être créé (détails : journal de démarrage).", true); return; }
                F.Statut("Bulletin créé : il s'ouvre dans ton navigateur.");
                Programmes.Ouvrir(t.Result);
                Charger();
            }));
        }
    }

    // ================================================================== Surveillance du réseau
    internal sealed class ReseauPremium5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Tableau tAppareils, tApps;
        private readonly TextBlock infoAppareils, infoApps;
        private readonly BoutonNoyau bBalayer;
        private bool enCours;

        /// <summary>(captures) appareils et applications d'exemple.</summary>
        public static List<AppareilReseau> ExempleAppareils;
        public static List<AppConnectee> ExempleApps;

        public static void Ouvrir(FenetrePrincipale f) { FenetreInterieure fen = null; var r = new ReseauPremium5(f, () => fen); fen = f.Ouvrir("Surveillance du réseau", r, 960); }

        private ReseauPremium5(FenetrePrincipale f, Func<FenetreInterieure> fen)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(Premium5.Intro("Qui est connecté à ta box, et quelles applications de ce PC parlent à Internet en ce moment. Rien n'est bloqué ni modifié : c'est pour repérer un appareil inconnu sur ton Wi-Fi ou une application qui se connecte en cachette."));
            ReglagesGardien r; try { r = ReglagesGardien.Lire(); } catch { r = new ReglagesGardien(); }
            Children.Add(Premium5.Carte(OutilsPremium2.Case("Me prévenir quand un nouvel appareil se connecte à mon réseau (le gardien vérifie toutes les 30 minutes)", ExempleAppareils != null || r.NouveauxAppareils, v => OutilsPremium2.Regler(x => x.NouveauxAppareils = v)), Habillage.Alpha(P.Accent, 90)));
            Children.Add(OutilsPremium2.NoteGardien(f, fen()));

            var t1 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
            t1.Children.Add(Premium5.Titre("Appareils sur ton réseau"));
            bBalayer = new BoutonNoyau("Rechercher les appareils", true, 220, 32) { TaillePolice = 13 };
            bBalayer.Clic += Balayer;
            var bConnu = new BoutonNoyau("Je le connais", false, 140, 32) { TaillePolice = 13, Margin = new Thickness(8, 0, 0, 0) };
            bConnu.Clic += Reconnaitre;
            Grid.SetColumn(bBalayer, 1); Grid.SetColumn(bConnu, 2); t1.Children.Add(bBalayer); t1.Children.Add(bConnu);
            Children.Add(t1);
            tAppareils = new Tableau(false, false, new Colonne("Appareil", "2*"), new Colonne("Adresse IP", "130", false, true), new Colonne("Adresse MAC", "160"),
                new Colonne("Vu la première fois", "150", false, true, o => ((AppareilReseau)o).Premier), new Colonne("État", "120"));
            tAppareils.HauteurListe = 230;
            Children.Add(OutilsPremium2.Cadre(tAppareils));
            infoAppareils = Premium5.Intro("Clique sur « Rechercher les appareils » (environ 30 secondes).");
            Children.Add(infoAppareils);

            var t2 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            t2.Children.Add(Premium5.Titre("Applications connectées à Internet en ce moment"));
            var bActu = new BoutonNoyau("Actualiser", false, 110, 32) { TaillePolice = 13 };
            bActu.Clic += ChargerApps;
            Grid.SetColumn(bActu, 1); t2.Children.Add(bActu);
            Children.Add(t2);
            tApps = new Tableau(false, false, new Colonne("Application", "1.4*"), new Colonne("Connexions", "100", true, true, o => ((AppConnectee)o).Connexions), new Colonne("Vers les adresses", "2.6*"));
            tApps.HauteurListe = 200;
            Children.Add(OutilsPremium2.Cadre(tApps));
            infoApps = Premium5.Intro("");
            Children.Add(infoApps);

            var connus = ExempleAppareils ?? SurveillanceReseau.Connus().Where(x => (DateTime.Now - x.Dernier).TotalDays < 2).ToList();
            if (connus.Count > 0) AfficherAppareils(connus);
            ChargerApps();
        }

        private static string Etat(AppareilReseau a) { return Langue.T(a.Box ? "Ta box" : a.CePc ? "Ce PC" : a.Connu ? "Connu" : "Nouveau"); }

        private void AfficherAppareils(List<AppareilReseau> l)
        {
            tAppareils.Remplir(l.Cast<object>(), o => { var a = (AppareilReseau)o; return new[] { a.Libelle, a.Ip, a.Mac, a.Premier == DateTime.MinValue ? "" : a.Premier.ToString("dd/MM/yyyy HH:mm"), Etat(a) }; }, null, o => ((AppareilReseau)o).Connu);
            int nouveaux = l.Count(x => !x.Connu);
            infoAppareils.Text = nouveaux > 0 ? Langue.F("{0} appareil(s) connecté(s) à ton réseau, dont {1} nouveau(x). Si tu ne le reconnais pas, change le mot de passe du Wi-Fi de ta box.", l.Count, nouveaux)
                : Langue.F("{0} appareil(s) connecté(s) à ton réseau. Tous sont connus.", l.Count);
            infoAppareils.Foreground = Habillage.Pinceau(nouveaux > 0 ? Habillage.P.Orange : Habillage.P.Discret);
        }

        private void Balayer()
        {
            if (enCours || ExempleAppareils != null) return;
            enCours = true; bBalayer.IsEnabled = false;
            infoAppareils.Text = Langue.T("Recherche des appareils sur ton réseau… (environ 30 secondes)");
            Task.Run(() => { try { List<AppareilReseau> tous; SurveillanceReseau.MettreAJour(out tous); return tous; } catch (Exception ex) { JournalDemarrage.Erreur("réseau (version 5)", ex); return new List<AppareilReseau>(); } })
                .ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    enCours = false; bBalayer.IsEnabled = true;
                    if (t.Result.Count == 0) { infoAppareils.Text = Langue.T("Aucun appareil trouvé : ce PC est-il connecté à une box (câble ou Wi-Fi) ?"); return; }
                    AfficherAppareils(t.Result);
                }));
        }

        private void Reconnaitre()
        {
            var a = tAppareils.Selection as AppareilReseau;
            if (a == null) { F.Statut("Sélectionne d'abord un appareil dans la liste."); return; }
            if (ExempleAppareils == null) SurveillanceReseau.Reconnaitre(a.Mac, null);
            a.Connu = true;
            tAppareils.Rafraichir();
            F.Statut(Langue.F("« {0} » est noté comme connu : plus d'alerte pour lui.", a.Libelle));
        }

        private void ChargerApps()
        {
            Task.Run(() => { try { return ExempleApps ?? SurveillanceReseau.Applications(); } catch { return new List<AppConnectee>(); } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                tApps.Remplir(t.Result.Cast<object>(), o => { var a = (AppConnectee)o; return new[] { a.Processus, a.Connexions.ToString(), string.Join(", ", a.Adresses) + (a.Connexions > a.Adresses.Count ? " …" : "") }; });
                infoApps.Text = Langue.F("{0} application(s) connectée(s) à Internet. Les navigateurs, Windows et les messageries en ont toujours plusieurs : c'est normal. Méfie-toi seulement d'un nom que tu ne connais pas.", t.Result.Count);
            }));
        }
    }
}
