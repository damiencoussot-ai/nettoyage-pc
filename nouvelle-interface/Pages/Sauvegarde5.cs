// Nettoyage PC 5 - sauvegarde : copie de Documents, Images, Bureau... sur une clé USB ou un disque externe,
// dans un dossier daté. Les fichiers sont copiés : rien n'est jamais supprimé du PC.
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
    internal sealed class Sauvegarde5 : StackPanel
    {
        private sealed class Destination { public string Texte, Chemin; public long Libre; public override string ToString() { return Texte; } }

        private static readonly string[][] Dossiers =
        {
            new[] { "Documents", "Documents", "1" }, new[] { "Images", "Images (photos)", "1" }, new[] { "Bureau", "Bureau", "1" },
            new[] { "Videos", "Vidéos", "0" }, new[] { "Musique", "Musique", "0" }, new[] { "Telechargements", "Téléchargements", "0" },
        };
        private readonly FenetrePrincipale F;
        private readonly ComboBox cbDest = new ComboBox { MinWidth = 440 };
        private readonly TextBlock tDest, tTotal, tDerniere;
        private readonly Dictionary<string, CheckBox> cases = new Dictionary<string, CheckBox>();
        private static readonly List<Destination> autres = new List<Destination>();
        private static Dictionary<string, long> tailles;
        private readonly BoutonNoyau bSauvegarder;
        private string dernierDossier;
        private bool lecture;

        public Sauvegarde5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10; Margin = new Thickness(0, 0, 12, 0);
            var carte = new StackPanel { Spacing = 10 };
            carte.Children.Add(Etape("1.  Où sauvegarder ?"));
            var ligne = new WrapPanel { Orientation = Orientation.Horizontal };
            cbDest.Margin = new Thickness(0, 0, 10, 6);
            cbDest.SelectionChanged += (s, e) => MettreAJourTotal();
            ligne.Children.Add(cbDest);
            var bActu = new BoutonNoyau("Actualiser", false, 110, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bActu.Clic += ActualiserDestinations;
            var bAutre = new BoutonNoyau("Autre dossier…", false, 140, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bAutre.Clic += AutreDossier;
            ligne.Children.Add(bActu); ligne.Children.Add(bAutre);
            carte.Children.Add(ligne);
            tDest = Petit(Langue.T("Branche ta clé USB ou ton disque externe, puis clique sur « Actualiser »."));
            carte.Children.Add(tDest);
            carte.Children.Add(Etape("2.  Quoi sauvegarder ?"));
            var grille = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2, MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
            foreach (var d in Dossiers)
            {
                var cb = new CheckBox { IsChecked = d[2] == "1", Content = new TextBlock { Text = Langue.T(d[1]), FontFamily = Habillage.Corps, FontSize = 13.5 }, Margin = new Thickness(0, 0, 20, 2) };
                cb.IsCheckedChanged += (s, e) => MettreAJourTotal();
                cases[d[0]] = cb; grille.Children.Add(cb);
            }
            carte.Children.Add(grille);
            tTotal = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            carte.Children.Add(tTotal);
            carte.Children.Add(Etape("3.  Lancer la sauvegarde"));
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            bSauvegarder = new BoutonNoyau("Sauvegarder maintenant", true, 240, 44) { TaillePolice = 15, IsEnabled = false };
            bSauvegarder.Clic += Sauvegarder;
            var bOuvrir = new BoutonNoyau("Ouvrir mes sauvegardes", false, 210, 44) { TaillePolice = 13.5 };
            bOuvrir.Clic += OuvrirSauvegardes;
            boutons.Children.Add(bSauvegarder); boutons.Children.Add(bOuvrir);
            carte.Children.Add(boutons);
            tDerniere = Petit("");
            carte.Children.Add(tDerniere);
            carte.Children.Add(Petit(Langue.T("Les fichiers sont copiés : rien n'est supprimé de ton PC. Chaque sauvegarde va dans un dossier daté. Les fichiers OneDrive restés « en ligne » ne sont pas copiés (ils sont déjà sauvegardés dans le cloud).")));
            Children.Add(new Border { Child = carte, Padding = new Thickness(22, 20), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1) });
            AttachedToVisualTree += (s, e) => { ActualiserDestinations(); CalculerTailles(); AfficherDerniere(); };
        }

        private static TextBlock Etape(string t) { return new TextBlock { Text = Langue.T(t), FontFamily = Habillage.Affichage, FontSize = 15.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(Habillage.P.Texte), Margin = new Thickness(0, 6, 0, 0) }; }
        private static TextBlock Petit(string t) { return new TextBlock { Text = t, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(Habillage.P.Discret), TextWrapping = TextWrapping.Wrap }; }

        private Destination Choisie { get { return cbDest.SelectedItem as Destination; } }

        /// <summary>Disques interrogés en arrière-plan : un disque externe en veille met parfois plusieurs secondes à répondre.</summary>
        private void ActualiserDestinations()
        {
            if (lecture) return;
            lecture = true;
            Task.Run(() => LireDestinations()).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                lecture = false;
                string avant = Choisie == null ? null : Choisie.Chemin;
                cbDest.Items.Clear();
                foreach (var d in (t.Result ?? new List<Destination>()).Concat(autres)) cbDest.Items.Add(d);
                if (cbDest.Items.Count > 0)
                {
                    cbDest.SelectedIndex = 0;
                    if (avant != null) for (int i = 0; i < cbDest.Items.Count; i++) if (((Destination)cbDest.Items[i]).Chemin == avant) cbDest.SelectedIndex = i;
                    tDest.Text = Langue.T("La sauvegarde sera rangée dans un dossier « NettoyagePC-Sauvegarde », daté, sur cette destination.");
                }
                else tDest.Text = Langue.T("Aucune clé USB ni disque externe détecté : branche-en un puis clique sur « Actualiser », ou choisis « Autre dossier... ».");
                MettreAJourTotal();
            }));
        }

        private static List<Destination> LireDestinations()
        {
            var l = new List<Destination>();
            if (!OperatingSystem.IsWindows()) return l;
            string systeme = (Path.GetPathRoot(Chemins.Windows) ?? "C:\\").Substring(0, 1).ToUpperInvariant();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    string lettre = d.Name.Substring(0, 1).ToUpperInvariant();
                    if (lettre == systeme || !(d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Network)) continue;
                    string type = Langue.T(d.DriveType == DriveType.Removable ? "Clé USB / carte" : d.DriveType == DriveType.Network ? "Réseau" : "Disque");
                    string vol = d.VolumeLabel != "" ? " « " + d.VolumeLabel + " »" : "";
                    l.Add(new Destination { Texte = lettre + ":  " + type + vol + "  —  " + Langue.F("{0} libres sur {1}", Format.Taille(d.AvailableFreeSpace), Format.Taille(d.TotalSize)), Chemin = d.RootDirectory.FullName, Libre = d.AvailableFreeSpace });
                }
                catch { }
            }
            return l;
        }

        private async void AutreDossier()
        {
            try
            {
                var top = TopLevel.GetTopLevel(this);
                if (top == null) return;
                var choix = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = Langue.T("Choisis le dossier où ranger la sauvegarde (de préférence sur un autre disque que celui du PC)."), AllowMultiple = false });
                var d = choix.FirstOrDefault();
                string chemin = d != null ? d.TryGetLocalPath() : null;
                if (string.IsNullOrEmpty(chemin)) return;
                long libre = 0; try { libre = new DriveInfo(Path.GetPathRoot(chemin)).AvailableFreeSpace; } catch { }
                autres.Add(new Destination { Texte = chemin + "  —  " + Langue.F("{0} libres", Format.Taille(libre)), Chemin = chemin, Libre = libre });
                cbDest.Items.Add(autres[autres.Count - 1]);
                cbDest.SelectedIndex = cbDest.Items.Count - 1;
                if (string.Equals(Path.GetPathRoot(chemin), Path.GetPathRoot(Chemins.Windows), StringComparison.OrdinalIgnoreCase))
                    F.Demander("Même disque que Windows", "Si ce disque tombe en panne, la sauvegarde sera perdue avec lui.\n\nPour une vraie sauvegarde, préfère une clé USB ou un disque externe.", "Compris", null, r => { }, true);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("dossier de sauvegarde (version 5)", ex); }
        }

        /// <summary>Taille de chaque dossier, calculée une fois en arrière-plan (sans les fichiers OneDrive restés en ligne).</summary>
        private void CalculerTailles()
        {
            if (tailles != null || F.Source.Immediat) { MettreAJourTotal(); return; }
            tTotal.Text = Langue.T("Calcul de la taille des dossiers…");
            var connus = Moteur.DossiersPersonnels();
            Task.Run(() =>
            {
                var r = new Dictionary<string, long>();
                foreach (var kv in connus) { try { if (Directory.Exists(kv.Value)) r[kv.Key] = TailleFichiers.Dossier(kv.Value); } catch { } }
                return r;
            }).ContinueWith(t => Dispatcher.UIThread.Post(() => { tailles = t.Result; MettreAJourTotal(); }));
        }

        private void MettreAJourTotal()
        {
            var P = Habillage.P;
            long total = 0;
            foreach (var d in Dossiers)
            {
                var cb = cases[d[0]]; long t = 0;
                bool connu = tailles != null && tailles.TryGetValue(d[0], out t);
                ((TextBlock)cb.Content).Text = Langue.T(d[1]) + (connu ? "  (" + Format.Taille(t) + ")" : "");
                if (connu && cb.IsChecked == true) total += t;
            }
            var dest = Choisie;
            bool ok = dest != null && cases.Values.Any(c => c.IsChecked == true);
            string txt = tailles == null ? Langue.T("Calcul de la taille des dossiers…") : Langue.T("À copier :") + " " + Format.Taille(total);
            if (dest != null && tailles != null)
            {
                txt += "   ·   " + Langue.T("Place libre sur la destination :") + " " + Format.Taille(dest.Libre);
                if (total > dest.Libre - 200L * 1048576) { txt += "   ·   " + Langue.T("PAS ASSEZ DE PLACE : décoche des dossiers ou prends une clé plus grande."); ok = false; }
            }
            tTotal.Text = txt;
            tTotal.Foreground = Habillage.Pinceau(ok || dest == null ? P.Texte : P.Rouge);
            bSauvegarder.IsEnabled = ok && !F.Moteur.EnCours;
        }

        private void AfficherDerniere()
        {
            tDerniere.Text = Langue.T("Aucune sauvegarde faite avec Nettoyage PC pour l'instant.");
            try
            {
                string f = Chemins.Fichier("sauvegardes.csv");
                if (!File.Exists(f)) return;
                var l = File.ReadAllLines(f).Where(x => x.Trim() != "").LastOrDefault();
                if (l == null) return;
                var c = l.TrimStart('\uFEFF').Split(';');
                long o; if (c.Length < 3 || !long.TryParse(c[1], out o)) return;
                tDerniere.Text = Langue.T("Dernière sauvegarde :") + " " + c[0] + "  —  " + Format.Taille(o) + "  —  " + c[2];
                dernierDossier = c[2];
            }
            catch { }
        }

        private void OuvrirSauvegardes()
        {
            string dossier = null;
            if (dernierDossier != null && Directory.Exists(dernierDossier)) dossier = Path.GetDirectoryName(dernierDossier);
            else if (Choisie != null) dossier = Path.Combine(Choisie.Chemin, "NettoyagePC-Sauvegarde");
            if (dossier != null && Directory.Exists(dossier)) Programmes.Ouvrir(dossier);
            else F.Demander("Aucune sauvegarde trouvée", "Aucune sauvegarde sur cette destination (clé débranchée ?).", "Compris", null, r => { });
        }

        private void Sauvegarder()
        {
            var dest = Choisie;
            var liste = Dossiers.Where(d => cases[d[0]].IsChecked == true).ToList();
            if (dest == null || liste.Count == 0) { F.Statut("Choisis une destination et au moins un dossier."); return; }
            string conf = "Sauvegarder " + string.Join(", ", liste.Select(d => Langue.T(d[1]))) + " vers " + dest.Chemin + " ?\n\nLes fichiers sont copiés, rien n'est supprimé de ton PC. Laisse la clé branchée jusqu'à la fin.";
            F.LancerAction("sauvegarde", new[] { "--sauvegarde", "--seul", "--destination", dest.Chemin, "--dossiers", string.Join(",", liste.Select(d => d[0])) }, conf, () => { AfficherDerniere(); ActualiserDestinations(); });
        }
    }
}
