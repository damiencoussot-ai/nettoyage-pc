// Nettoyage PC 5 - rubrique Nettoyer : nettoyage (rapide, profond, analyse), disques et gros fichiers, doublons.
// Mêmes actions du moteur que la version 4 ; les gros fichiers vont à la corbeille (récupérables), jamais supprimés.
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
    internal static class PageNettoyer
    {
        public static Control Creer(FenetrePrincipale f)
        {
            return new Onglets(f, "Nettoyer", new[]
            {
                Tuple.Create<string, string, Func<Control>>("Nettoyage", "Nettoyage", () => Nettoyage(f)),
                Tuple.Create<string, string, Func<Control>>("Disques", "Disques et gros fichiers", () => Disques(f)),
                Tuple.Create<string, string, Func<Control>>("Doublons", "Doublons", () => Doublons(f)),
            });
        }

        private static Control Nettoyage(FenetrePrincipale f)
        {
            return Cartes.Page(f, null, new[]
            {
                CarteAction.A("Nettoyage rapide", "Fichiers temporaires, caches de Windows, des navigateurs et des applications, corbeille, vieux téléchargements rangés à part.", "Nettoyer", "standard", true),
                CarteAction.A("Nettoyage profond", "Tout le nettoyage rapide, plus Windows Update, Nettoyage de disque Windows et DISM, avec un point de restauration avant.", "Nettoyer en profondeur", "complet"),
                CarteAction.A("Voir ce qui serait nettoyé", "Analyse tout ce que l'optimisation complète ferait et affiche l'espace récupérable, sans rien supprimer.", "Analyser", "analyse"),
                CarteAction.Code("Anciens pilotes", "Windows garde les anciennes versions de chaque pilote (souvent plusieurs Go pour la carte graphique). Tu choisis lesquelles retirer.", "Voir les anciens pilotes", () => f.Bientot("Anciens pilotes")).Plus(),
                CarteAction.A("Rapport complet du PC", "Espace disque, gros fichiers, doublons, dossiers lourds, programmes volumineux. Enregistré dans tes Documents.", "Générer le rapport", "rapport").Plus(),
                CarteAction.Code("Téléchargements à vérifier", "Les téléchargements de plus de 90 jours sont rangés ici au lieu d'être supprimés. Garde ou supprime-les toi-même.", "Ouvrir le dossier", () => OuvrirAVerifier(f)).Plus(),
            }, "Nettoyage");
        }

        private static void OuvrirAVerifier(FenetrePrincipale f)
        {
            string d = Path.Combine(Chemins.Telechargements, "_A_verifier");
            if (Directory.Exists(d)) Programmes.Ouvrir(d);
            else f.Demander("Rien à vérifier pour l'instant", "Le dossier « _A_verifier » n'existe pas encore : il est créé au premier nettoyage, quand des téléchargements de plus de 90 jours sont rangés.", "Compris", null, r => { });
        }

        private static Control Disques(FenetrePrincipale f)
        {
            var pile = new StackPanel { Spacing = 18 };
            pile.Children.Add(Cartes.Grille(f, new[]
            {
                CarteAction.A("Optimiser les disques", "Santé SMART, usure et température, TRIM, ReTrim des SSD (jamais de défragmentation), défragmentation des disques durs.", "Optimiser", "disques", true),
                CarteAction.A("Vérifier les disques", "Comme « Optimiser », plus l'analyse du système de fichiers de chaque disque, en lecture seule.", "Vérifier", "verifier"),
                CarteAction.Code("Carte de l'espace disque", "Chaque dossier est un rectangle proportionnel à sa taille : tu vois d'un coup d'œil ce qui remplit ton disque.", "Afficher la carte", () => f.Bientot("Carte de l'espace disque")),
            }));
            pile.Children.Add(new GrosFichiers(f));
            return pile;
        }

        private static Control Doublons(FenetrePrincipale f)
        {
            return Cartes.Page(f, null, new[]
            {
                CarteAction.Code("Fichiers en double", "Fichiers identiques repérés par empreinte SHA-256 (jamais par le nom seul). Tu vois chaque groupe et tu choisis : les copies vont à la corbeille, l'original reste.", "Chercher les doublons", () => f.Bientot("Doublons"), true),
            }, "Doublons");
        }
    }

    /// <summary>Gros fichiers d'un dossier : liste triée par taille, ouvrir l'emplacement, mettre à la corbeille (récupérable).</summary>
    internal sealed class GrosFichiers : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBox dossier = new TextBox { Width = 380 };
        private readonly ComboBox seuil = new ComboBox { Width = 110 };
        private readonly ListBox liste = new ListBox { SelectionMode = SelectionMode.Multiple, MaxHeight = 420, MinHeight = 120 };
        private readonly TextBlock info;
        private readonly BoutonNoyau bAnalyser, bOuvrir, bCorbeille;
        private readonly List<FileInfo> trouves = new List<FileInfo>();
        private bool enCours;
        private static readonly long[] Seuils = { 50L, 100L, 500L, 1024L };

        public GrosFichiers(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Margin = new Thickness(0, 0, 12, 0);
            Children.Add(new TextBlock { Text = "Gros fichiers", FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            var ligne = new WrapPanel { Orientation = Orientation.Horizontal };
            dossier.Text = Chemins.Profil;
            ligne.Children.Add(Etiquette("Dossier :"));
            ligne.Children.Add(dossier);
            var parcourir = new BoutonNoyau("Parcourir…", false, 110, 32) { TaillePolice = 13, Margin = new Thickness(8, 0, 18, 6) };
            parcourir.Clic += Parcourir;
            ligne.Children.Add(parcourir);
            ligne.Children.Add(Etiquette("Taille min. :"));
            foreach (var s in new[] { "50 Mo", "100 Mo", "500 Mo", "1 Go" }) seuil.Items.Add(s);
            seuil.SelectedIndex = 1;
            seuil.Margin = new Thickness(0, 0, 18, 6);
            ligne.Children.Add(seuil);
            bAnalyser = new BoutonNoyau("Analyser", true, 120, 32) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 0, 6) };
            bAnalyser.Clic += Analyser;
            ligne.Children.Add(bAnalyser);
            Children.Add(ligne);
            info = new TextBlock { Text = "Choisis un dossier puis clique sur « Analyser ». Les fichiers OneDrive non téléchargés sont ignorés.", FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(info);
            liste.Background = Habillage.Pinceau(P.Carte);
            liste.BorderBrush = Habillage.Pinceau(P.Bordure); liste.BorderThickness = new Thickness(1);
            liste.IsVisible = false;
            liste.SelectionChanged += (s, e) => { bool sel = liste.SelectedItems != null && liste.SelectedItems.Count > 0; bOuvrir.IsEnabled = sel; bCorbeille.IsEnabled = sel; };
            Children.Add(liste);
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            bOuvrir = new BoutonNoyau("Ouvrir l'emplacement", false, 180, 32) { TaillePolice = 13, IsEnabled = false };
            bOuvrir.Clic += () => { var fi = Selection().FirstOrDefault(); if (fi != null) Programmes.MontrerDansExplorateur(fi.FullName); };
            bCorbeille = new BoutonNoyau("Mettre à la corbeille", false, 180, 32) { TaillePolice = 13, IsEnabled = false };
            bCorbeille.Clic += MettreACorbeille;
            boutons.Children.Add(bOuvrir); boutons.Children.Add(bCorbeille);
            Children.Add(boutons);
        }

        private static TextBlock Etiquette(string t)
        {
            return new TextBlock { Text = t, FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(Habillage.P.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 6) };
        }

        private async void Parcourir()
        {
            try
            {
                var top = TopLevel.GetTopLevel(this);
                if (top == null) return;
                var depart = await top.StorageProvider.TryGetFolderFromPathAsync(dossier.Text ?? "");
                var choix = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Dossier à analyser", AllowMultiple = false, SuggestedStartLocation = depart });
                var d = choix.FirstOrDefault();
                if (d != null) { string p = d.TryGetLocalPath(); if (!string.IsNullOrEmpty(p)) dossier.Text = p; }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("choix du dossier (version 5)", ex); }
        }

        private List<FileInfo> Selection()
        {
            var l = new List<FileInfo>();
            if (liste.SelectedItems == null) return l;
            foreach (var o in liste.SelectedItems) { var c = o as Control; var fi = c != null ? c.Tag as FileInfo : null; if (fi != null) l.Add(fi); }
            return l;
        }

        private void Analyser()
        {
            if (enCours) return;
            string racine = (dossier.Text ?? "").Trim();
            if (!Directory.Exists(racine)) { F.Demander("Ce dossier n'existe pas", "Vérifie le chemin, ou choisis-le avec « Parcourir… ».", "Compris", null, r => { }); return; }
            long s = Seuils[Math.Max(0, seuil.SelectedIndex)] * 1048576;
            enCours = true; bAnalyser.IsEnabled = false; liste.Items.Clear(); liste.IsVisible = false;
            info.Text = "Analyse en cours… (cela peut prendre quelques minutes sur un gros dossier)";
            Task.Run(() =>
            {
                var l = new List<FileInfo>();
                var pile = new Stack<DirectoryInfo>(); pile.Push(new DirectoryInfo(racine));
                while (pile.Count > 0)
                {
                    var d = pile.Pop();
                    try
                    {
                        foreach (var e in d.EnumerateFileSystemInfos())
                        {
                            FileAttributes a; try { a = e.Attributes; } catch { continue; }
                            if ((a & FileAttributes.ReparsePoint) != 0) continue;
                            if ((a & FileAttributes.Directory) != 0) pile.Push((DirectoryInfo)e);
                            else if (!TailleFichiers.EstCloud(a)) { var fi = (FileInfo)e; try { if (fi.Length >= s) l.Add(fi); } catch { } }
                        }
                    }
                    catch { }
                }
                return l.OrderByDescending(x => x.Length).Take(300).ToList();
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false; bAnalyser.IsEnabled = true;
                var res = t.IsFaulted || t.Result == null ? new List<FileInfo>() : t.Result;
                trouves.Clear(); trouves.AddRange(res);
                Remplir();
                long total = res.Sum(x => { try { return x.Length; } catch { return 0L; } });
                info.Text = res.Count == 0 ? "Aucun fichier de cette taille dans ce dossier."
                    : res.Count + " gros fichier(s) trouvé(s), " + Format.Taille(total) + " au total. Sélectionne-en pour les ouvrir ou les mettre à la corbeille (récupérables).";
            }));
        }

        private void Remplir()
        {
            var P = Habillage.P;
            liste.Items.Clear();
            foreach (var fi in trouves)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("90,2*,3*,96"), Tag = fi, Margin = new Thickness(2, 3) };
                long taille = 0; try { taille = fi.Length; } catch { }
                g.Children.Add(new TextBlock { Text = Format.Taille(taille), FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Accent), HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 14, 0) });
                var nom = new TextBlock { Text = fi.Name, FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Texte), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 12, 0) };
                Grid.SetColumn(nom, 1); g.Children.Add(nom);
                var dos = new TextBlock { Text = fi.DirectoryName, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 12, 0) };
                ToolTip.SetTip(dos, fi.FullName);
                Grid.SetColumn(dos, 2); g.Children.Add(dos);
                string date = ""; try { date = fi.LastWriteTime.ToString("dd/MM/yyyy"); } catch { }
                var dt = new TextBlock { Text = date, FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret) };
                Grid.SetColumn(dt, 3); g.Children.Add(dt);
                liste.Items.Add(g);
            }
            liste.IsVisible = trouves.Count > 0;
        }

        private void MettreACorbeille()
        {
            var choisis = Selection();
            if (choisis.Count == 0) return;
            long total = 0; foreach (var fi in choisis) { try { total += fi.Length; } catch { } }
            F.Demander("Mettre " + choisis.Count + " fichier(s) à la corbeille (" + Format.Taille(total) + ") ?",
                "Tu pourras les récupérer depuis la corbeille tant qu'elle n'est pas vidée, ou depuis l'Historique de Nettoyage PC.", "Mettre à la corbeille", "Annuler", oui =>
            {
                if (!oui) return;
                int ok = 0, refus = 0;
                var faits = new List<string>();
                foreach (var fi in choisis)
                {
                    string c = fi.FullName;
                    if (Protection.CheminProtege(c)) { refus++; continue; }
                    if (Corbeille.Envoyer(c)) { ok++; faits.Add(c); trouves.Remove(fi); } else refus++;
                }
                if (faits.Count > 0) Historique.Ajouter("corbeille", "Gros fichiers : " + faits.Count + " fichier(s) mis à la corbeille", new Dictionary<string, object> { { "Fichiers", faits.ToArray() } });
                Remplir();
                info.Text = ok + " fichier(s) mis à la corbeille (restaurables depuis l'Historique)." + (refus > 0 ? " " + refus + " ignoré(s) (fichiers système protégés, en cours d'utilisation, ou hors d'un disque local)." : "");
            }, true);
        }
    }
}
