// Nettoyage PC 5 - doublons : fichiers au contenu identique (empreinte SHA-256). Nettoyage PC propose la copie
// à garder ; tu vérifies, tu changes les cases si besoin, puis tu confirmes. Les copies vont à la corbeille
// (restaurables depuis l'Historique) ; chaque fichier est revérifié juste avant.
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
    internal sealed class Doublons5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly ComboBox ou = new ComboBox { MinWidth = 420 };
        private readonly ComboBox seuil = new ComboBox { Width = 100 };
        private readonly Tableau tGroupes, tCopies;
        private readonly TextBlock info, tCopiesTitre;
        private readonly BoutonNoyau bChercher, bSuppr;
        private readonly EtatDoublons etat = new EtatDoublons();
        private readonly DispatcherTimer minuteur = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        private List<GroupeDoublons> groupes = new List<GroupeDoublons>();
        private GroupeDoublons groupeAffiche;
        private string dossierPerso = "";
        private bool enCours;

        public Doublons5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10; Margin = new Thickness(0, 0, 12, 0);
            Children.Add(new TextBlock { Text = Langue.T("Trouve les fichiers au contenu identique (comparés octet par octet grâce à une empreinte SHA-256, pas seulement le nom). Nettoyage PC propose la copie à garder : vérifie, change les cases si besoin, puis confirme. Les copies vont à la corbeille et peuvent être restaurées depuis l'Historique."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            var ligne = new WrapPanel { Orientation = Orientation.Horizontal };
            ligne.Children.Add(Etiquette("Où chercher :"));
            foreach (var t in new[] { "Mes dossiers (Documents, Images, Vidéos, Musique, Bureau, Téléchargements)", "Tout mon dossier utilisateur", "Choisir un dossier…" }) ou.Items.Add(Langue.T(t));
            ou.SelectedIndex = 0; ou.Margin = new Thickness(0, 0, 16, 6);
            ou.SelectionChanged += async (s, e) =>
            {
                if (ou.SelectedIndex != 2) return;
                try
                {
                    var top = TopLevel.GetTopLevel(this);
                    var choix = top == null ? null : await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Langue.T("Dossier où chercher les fichiers en double"), AllowMultiple = false });
                    var d = choix != null ? choix.FirstOrDefault() : null;
                    string chemin = d != null ? d.TryGetLocalPath() : null;
                    if (!string.IsNullOrEmpty(chemin)) { dossierPerso = chemin; ou.Items[2] = Langue.T("Dossier :") + " " + chemin; ou.SelectedIndex = 2; }
                    else if (dossierPerso == "") ou.SelectedIndex = 0;
                }
                catch { ou.SelectedIndex = 0; }
            };
            ligne.Children.Add(ou);
            ligne.Children.Add(Etiquette("Taille min. :"));
            foreach (var t in new[] { "100 Ko", "1 Mo", "10 Mo", "100 Mo" }) seuil.Items.Add(Langue.T(t));
            seuil.SelectedIndex = 1; seuil.Margin = new Thickness(0, 0, 16, 6);
            ligne.Children.Add(seuil);
            bChercher = new BoutonNoyau("Rechercher les doublons", true, 210, 34) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 0, 6) };
            bChercher.Clic += Rechercher;
            ligne.Children.Add(bChercher);
            Children.Add(ligne);

            tGroupes = new Tableau(true, false,
                new Colonne("Fichier en double", "2.2*"), new Colonne("Copies", "70", true, true),
                new Colonne("Taille", "90", true, true, o => ((GroupeDoublons)o).Taille),
                new Colonne("Récupérable", "110", true, true, o => { var g = (GroupeDoublons)o; return g.Taille * g.Fichiers.Count(x => x.Supprimer); }),
                new Colonne("Proposition", "2*"));
            tGroupes.HauteurListe = 260;
            tGroupes.CaseChangee = GroupeCoche;
            tGroupes.SelectionChangee += () => AfficherCopies(tGroupes.Selection as GroupeDoublons);
            Children.Add(Cadre(tGroupes));
            tCopiesTitre = new TextBlock { Text = Langue.T("Copies du fichier sélectionné : coche celles à mettre à la corbeille"), FontFamily = Habillage.Affichage, FontSize = 14.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), Margin = new Thickness(0, 6, 0, 0) };
            Children.Add(tCopiesTitre);
            tCopies = new Tableau(true, false, new Colonne("Corbeille ?", "2.4*"), new Colonne("Dossier", "2.4*"), new Colonne("Modifié le", "130", false, true), new Colonne("Créé le", "130", false, true));
            tCopies.HauteurListe = 200;
            tCopies.CaseChangee = CopieCochee;
            tCopies.DoubleClic += o => Programmes.OuvrirFichierSur(((CopieDoublon)o).Chemin);
            Children.Add(Cadre(tCopies));
            var boutons = new WrapPanel { Orientation = Orientation.Horizontal };
            var bOuvrir = new BoutonNoyau("Ouvrir le fichier", false, 140, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 8, 6) };
            bOuvrir.Clic += () => { var c = CopieSelectionnee(); if (c != null) Programmes.OuvrirFichierSur(c.Chemin); };
            var bEmpl = new BoutonNoyau("Ouvrir l'emplacement", false, 170, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 8, 6) };
            bEmpl.Clic += () => { var c = CopieSelectionnee(); if (c != null) Programmes.MontrerDansExplorateur(c.Chemin); };
            var bIntel = new BoutonNoyau("Sélection intelligente", false, 170, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 8, 6) };
            bIntel.Clic += () => { foreach (var g in groupes) RechercheDoublons.Proposer(g); AfficherGroupes(); };
            var bAucun = new BoutonNoyau("Tout décocher", false, 130, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 18, 6) };
            bAucun.Clic += () => { foreach (var g in groupes) RechercheDoublons.ToutGarder(g); AfficherGroupes(); };
            bSuppr = new BoutonNoyau("Vérifier et mettre à la corbeille", true, 0, 34) { TaillePolice = 13.5, IsEnabled = false, Margin = new Thickness(0, 0, 0, 6) };
            bSuppr.Clic += Supprimer;
            boutons.Children.Add(bOuvrir); boutons.Children.Add(bEmpl); boutons.Children.Add(bIntel); boutons.Children.Add(bAucun); boutons.Children.Add(bSuppr);
            Children.Add(boutons);
            info = new TextBlock { Text = Langue.T("Choisis où chercher puis clique sur « Rechercher les doublons ». Rien n'est supprimé sans ta confirmation."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(info);
            minuteur.Tick += (s, e) =>
            {
                if (!enCours) { minuteur.Stop(); return; }
                if (etat.Phase == "liste") Info(Langue.T("Recherche des fichiers…") + " " + Langue.F("{0} fichier(s) examiné(s)", Format.Nombre(etat.Fichiers)));
                else if (etat.Phase == "comparaison") Info(Langue.T("Comparaison du contenu…") + " " + Format.Nombre(etat.Fait) + " / " + Format.Nombre(etat.Total) + " " + Langue.T("fichier(s) de même taille"));
                else if (etat.Phase == "verification") Info(Langue.T("Vérification et mise à la corbeille…") + " " + Format.Nombre(etat.Fait) + " / " + Format.Nombre(etat.Total));
            };
            DetachedFromVisualTree += (s, e) => { etat.Arret = true; minuteur.Stop(); };
        }

        private static TextBlock Etiquette(string t) { return new TextBlock { Text = Langue.T(t), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(Habillage.P.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) }; }
        private static Control Cadre(Control c) { return new Border { Child = c, BorderBrush = Habillage.Pinceau(Habillage.P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true }; }
        private void Info(string t, Color? c = null) { info.Text = t; info.Foreground = Habillage.Pinceau(c ?? Habillage.P.Discret); }

        private void Rechercher()
        {
            if (enCours) { etat.Arret = true; bChercher.IsEnabled = false; Info(Langue.T("Arrêt en cours…")); return; }
            var racines = new List<string>();
            switch (ou.SelectedIndex)
            {
                case 0: racines.AddRange(new[] { Chemins.Documents, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Chemins.Telechargements }); break;
                case 1: racines.Add(Chemins.Profil); break;
                case 2: racines.Add(dossierPerso); break;
            }
            racines = racines.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (racines.Count == 0) { F.Demander("Choisis un dossier qui existe", null, "Compris", null, r => { }); return; }
            long s = new[] { 100L * 1024, 1048576L, 10L * 1048576, 100L * 1048576 }[Math.Max(0, seuil.SelectedIndex)];
            enCours = true; etat.Arret = false; etat.Fichiers = etat.Fait = etat.Total = 0; etat.Phase = "liste";
            groupes = new List<GroupeDoublons>(); AfficherGroupes();
            bChercher.Changer("Arrêter la recherche"); bSuppr.IsEnabled = false;
            minuteur.Start();
            Task.Run(() => { try { return RechercheDoublons.Chercher(racines, s, etat); } catch { return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false; minuteur.Stop();
                bChercher.Changer("Rechercher les doublons"); bChercher.IsEnabled = true;
                var res = t.Result;
                if (res == null || etat.Arret) { Info(Langue.T("Recherche arrêtée.")); return; }
                groupes = res;
                foreach (var g in groupes) RechercheDoublons.Proposer(g);
                AfficherGroupes();
                if (groupes.Count == 0) Info(Langue.F("Aucun doublon trouvé parmi {0} fichier(s). ✔", Format.Nombre(etat.Fichiers)), Habillage.P.Vert);
            }));
        }

        private void AfficherGroupes()
        {
            var sel = tGroupes.Selection as GroupeDoublons;
            tGroupes.Remplir(groupes.Cast<object>(), o =>
            {
                var g = (GroupeDoublons)o; int n = g.Fichiers.Count(x => x.Supprimer);
                return new[] { g.Fichiers[0].Nom, g.Fichiers.Count.ToString(), Format.Taille(g.Taille), Format.Taille(g.Taille * n), Langue.T(g.Proposition) };
            }, o => ((GroupeDoublons)o).Actif, o => !((GroupeDoublons)o).Actif);
            var garde = sel != null && groupes.Contains(sel) ? sel : groupes.FirstOrDefault();
            if (garde != null) tGroupes.Selectionner(garde);
            AfficherCopies(garde);
            Resume();
        }

        private void AfficherCopies(GroupeDoublons g)
        {
            groupeAffiche = g;
            var liste = g == null ? new List<CopieDoublon>() : g.Fichiers;
            tCopies.Remplir(liste.Cast<object>(), o =>
            {
                var f = (CopieDoublon)o;
                return new[] { f.Nom + "   —   " + Langue.T(f.Raison), f.Dossier, f.Modif.ToString("dd/MM/yyyy HH:mm"), f.Creation.ToString("dd/MM/yyyy HH:mm") };
            }, o => ((CopieDoublon)o).Supprimer, null);
        }

        private void Selection(out int fichiers, out long octets)
        {
            fichiers = 0; octets = 0;
            foreach (var g in groupes.Where(x => x.Actif)) { int n = g.Fichiers.Count(x => x.Supprimer); fichiers += n; octets += g.Taille * n; }
        }

        private void Resume()
        {
            if (groupes.Count == 0) { bSuppr.IsEnabled = false; return; }
            int nb; long oct; Selection(out nb, out oct);
            int copies = groupes.Sum(g => g.Fichiers.Count - 1); long total = groupes.Sum(g => g.Taille * (g.Fichiers.Count - 1));
            Info(Langue.F("{0} fichier(s) en double, {1} copie(s) en trop ({2}). Sélection : {3} fichier(s) à mettre à la corbeille, {4} libérés.", groupes.Count, copies, Format.Taille(total), nb, Format.Taille(oct)));
            bSuppr.IsEnabled = nb > 0 && !enCours;
            bSuppr.Texte = nb > 0 ? Langue.F("Vérifier et mettre {0} fichier(s) à la corbeille", nb) : Langue.T("Vérifier et mettre à la corbeille");
            bSuppr.InvalidateMeasure(); bSuppr.InvalidateVisual();
        }

        private bool GroupeCoche(object o, bool coche)
        {
            var g = (GroupeDoublons)o;
            if (coche)
            {
                RechercheDoublons.Proposer(g);
                if (!g.Actif)
                {
                    Info(Langue.T("Ce fichier n'a que des copies de sauvegarde : rien n'est proposé. Coche toi-même la copie à mettre à la corbeille en dessous."), Habillage.P.Orange);
                    tGroupes.Rafraichir();
                    return false;
                }
            }
            else RechercheDoublons.ToutGarder(g);
            if (groupeAffiche == g) AfficherCopies(g);
            Dispatcher.UIThread.Post(() => { tGroupes.Rafraichir(); Resume(); });
            return true;
        }

        private bool CopieCochee(object o, bool coche)
        {
            var g = groupeAffiche; var f = (CopieDoublon)o;
            if (g == null) return false;
            if (coche)
            {
                // il reste toujours au moins une copie de chaque fichier
                if (g.Fichiers.Count(x => !x.Supprimer && x != f) == 0) { Info(Langue.T("Impossible : il faut garder au moins une copie de chaque fichier."), Habillage.P.Orange); return false; }
                f.Supprimer = true; f.Raison = "Choisie par toi : corbeille"; g.Actif = true;
            }
            else { f.Supprimer = false; f.Raison = "Gardée (choisie par toi)"; }
            int n = g.Fichiers.Count(x => x.Supprimer);
            g.Proposition = Langue.F("Garder {0}, mettre {1} copie(s) à la corbeille", g.Fichiers.Count - n, n);
            if (n == 0) { g.Actif = false; g.Proposition = "Tout garder"; }
            Dispatcher.UIThread.Post(() => { tGroupes.Rafraichir(); Resume(); });
            return true;
        }

        private CopieDoublon CopieSelectionnee()
        {
            var c = tCopies.Selection as CopieDoublon;
            if (c != null) return c;
            var g = tGroupes.Selection as GroupeDoublons;
            return g != null ? g.Fichiers[0] : null;
        }

        private void Supprimer()
        {
            if (enCours) return;
            var lots = new List<LotDoublons>();
            foreach (var g in groupes.Where(x => x.Actif))
            {
                var suppr = g.Fichiers.Where(x => x.Supprimer).Select(x => x.Chemin).ToList();
                var garde = g.Fichiers.FirstOrDefault(x => !x.Supprimer);
                if (suppr.Count == 0 || garde == null) continue;
                lots.Add(new LotDoublons { Garde = garde.Chemin, Hash = g.Hash, Taille = g.Taille, Supprimer = suppr });
            }
            if (lots.Count == 0) return;
            Confirmer(lots, () => Executer(lots));
        }

        /// <summary>Fenêtre de vérification : la liste complète (garder / corbeille), et une case « J'ai vérifié la liste ».</summary>
        private void Confirmer(List<LotDoublons> lots, Action ok)
        {
            var P = Habillage.P;
            int nb; long oct; Selection(out nb, out oct);
            var s = new StackPanel { Spacing = 10 };
            s.Children.Add(new TextBlock { Text = Langue.F("{0} fichier(s) vont aller à la corbeille ({1} libérés)", nb, Format.Taille(oct)), FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            s.Children.Add(new TextBlock { Text = Langue.T("Vérifie la liste : une copie de chaque fichier est toujours gardée. Juste avant, chaque fichier est revérifié (contenu toujours identique à la copie gardée) ; s'il a changé, il n'est pas touché."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            var liste = new StackPanel { Spacing = 2 };
            foreach (var l in lots.Take(400))
            {
                liste.Children.Add(new TextBlock { Text = "✔  " + Langue.T("Garder :") + " " + l.Garde, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Vert), TextWrapping = TextWrapping.Wrap });
                foreach (var c in l.Supprimer) liste.Children.Add(new TextBlock { Text = "      →  " + Langue.T("corbeille :") + " " + c, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Rouge), TextWrapping = TextWrapping.Wrap });
                liste.Children.Add(new TextBlock { Text = "      (" + Langue.F("{0} chacun", Format.Taille(l.Taille)) + ")", FontFamily = Habillage.Corps, FontSize = 11.5, Foreground = Habillage.Pinceau(P.Discret), Margin = new Thickness(0, 0, 0, 6) });
            }
            s.Children.Add(new Border { Child = new ScrollViewer { Content = new Border { Child = liste, Padding = new Thickness(12, 10) }, MaxHeight = 340 }, Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3) });
            var verifie = new CheckBox { Content = new TextBlock { Text = Langue.T("J'ai vérifié la liste"), FontFamily = Habillage.Corps, FontSize = 13.5 } };
            s.Children.Add(verifie);
            var bOk = new BoutonNoyau(Langue.F("Mettre {0} fichier(s) à la corbeille", nb), true, 0, 38) { TaillePolice = 14, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left };
            verifie.IsCheckedChanged += (o, e) => bOk.IsEnabled = verifie.IsChecked == true;
            s.Children.Add(bOk);
            var w = F.Ouvrir("Vérifier avant de mettre à la corbeille", s, 880);
            bOk.Clic += () => { w.Fermer(); ok(); };
        }

        private void Executer(List<LotDoublons> lots)
        {
            enCours = true; etat.Arret = false; etat.Fait = 0; etat.Total = lots.Sum(l => l.Supprimer.Count); etat.Phase = "verification";
            bSuppr.IsEnabled = false; bChercher.IsEnabled = false;
            minuteur.Start();
            Task.Run(() => { try { return RechercheDoublons.MettreACorbeille(lots, etat); } catch { return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false; minuteur.Stop(); bChercher.IsEnabled = true;
                var r = t.Result;
                if (r == null) { Info(Langue.T("Erreur pendant la mise à la corbeille."), Habillage.P.Rouge); return; }
                var faits = r.Item1; var ignores = r.Item2; long octets = r.Item3;
                if (faits.Count > 0)
                {
                    Historique.Ajouter("corbeille", "Doublons : " + faits.Count + " fichier(s) mis à la corbeille (" + Format.Taille(octets) + ")", new Dictionary<string, object> { { "Fichiers", faits.ToArray() } });
                    var l = new List<string> { "Nettoyage PC - doublons mis à la corbeille le " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "", "Mis à la corbeille :" };
                    l.AddRange(faits.Select(x => "  " + x));
                    if (ignores.Count > 0) { l.Add(""); l.Add("Non touchés :"); l.AddRange(ignores.Select(x => "  " + x)); }
                    FichierSur.Ecrire(Chemins.Fichier("doublons_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".txt"), string.Join("\r\n", l) + "\r\n");
                }
                var partis = new HashSet<string>(faits, StringComparer.OrdinalIgnoreCase);
                var restants = new List<GroupeDoublons>();
                foreach (var g in groupes)
                {
                    g.Fichiers = g.Fichiers.Where(x => !partis.Contains(x.Chemin)).ToList();
                    if (g.Fichiers.Count >= 2) { RechercheDoublons.Proposer(g); restants.Add(g); }
                }
                groupes = restants;
                AfficherGroupes();
                Info(Langue.F("{0} fichier(s) mis à la corbeille, {1} libérés. Restaurables depuis l'Historique.", faits.Count, Format.Taille(octets)), Habillage.P.Vert);
                string msg = Langue.T("Tu peux tout restaurer depuis l'Historique (« Annuler la sélection »), tant que la corbeille n'est pas vidée.");
                if (ignores.Count > 0) msg = Langue.F("{0} fichier(s) non touché(s) par sécurité :", ignores.Count) + "\n" + string.Join("\n", ignores.Take(10)) + "\n\n" + msg;
                F.Demander(Langue.F("{0} fichier(s) mis à la corbeille, {1} libérés.", faits.Count, Format.Taille(octets)), msg, "Compris", null, x => { });
            }));
        }
    }
}
