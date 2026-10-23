// Nettoyage PC 5 - fenêtre principale : menu de gauche (indicateur lumineux qui glisse), en-tête (titre
// qui se décode, lumière qui parcourt le trait du bas), pages avec fondu, barre d'état.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;

namespace NettoyagePC.Nouveau
{
    public sealed class Rubrique
    {
        public string Cle, Nom, Sous;
        public bool EnBas;
        public Func<Control> Page;
    }

    public sealed class FenetrePrincipale : Window
    {
        public readonly List<Rubrique> Rubriques = new List<Rubrique>();
        private readonly Dictionary<string, ElementMenu> menu = new Dictionary<string, ElementMenu>();
        private TransitioningContentControl contenu;
        private TextBlock titre, sousTitre, statut;
        private Border indicateur, lumiere;
        private readonly TranslateTransform positionIndicateur = new TranslateTransform(), positionLumiere = new TranslateTransform(-400, 0);
        private Canvas calqueIndicateur;
        private Panel ligneEntete;
        public string PageActive { get; private set; } = "Accueil";
        public readonly ISourceBilan Source;
        public bool Simulation = true;
        public string Pseudo = "";

        // ---- bilan de l'accueil : calculé en arrière-plan, gardé entre deux visites de l'accueil
        public Bilan Bilan { get; private set; }
        public bool BilanEnCours { get; private set; }
        /// <summary>Le bilan vient d'être (re)calculé (appelé sur le fil de l'interface).</summary>
        public event Action<Bilan> BilanPret;

        public void ActualiserBilan()
        {
            if (BilanEnCours) return;
            if (Source.Immediat) { Bilan = Source.Calculer(); var h = BilanPret; if (h != null) h(Bilan); return; }
            BilanEnCours = true;
            System.Threading.Tasks.Task.Run(() =>
            {
                Bilan b = null;
                try { b = Source.Calculer(); }
                catch (Exception ex) { try { JournalDemarrage.Erreur("bilan (version 5)", ex); } catch { } }
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    BilanEnCours = false;
                    if (b == null) { Statut("Le bilan n'a pas pu être calculé. Détails : journal de démarrage.", true); return; }
                    Bilan = b;
                    var h = BilanPret; if (h != null) h(b);
                });
            });
        }

        /// <summary>Texte de la barre d'état (alerte : en orange).</summary>
        public void Statut(string texte, bool alerte = false)
        {
            if (statut == null) return;
            statut.Text = texte;
            statut.Foreground = Habillage.Pinceau(alerte ? Habillage.P.Orange : Habillage.P.Discret);
        }

        /// <summary>Bouton d'un point du bilan. Les rubriques arrivent une à une : en attendant, on dit ce qui va se passer.</summary>
        public void Executer(string cible, string libelle)
        {
            if (string.IsNullOrEmpty(cible)) return;
            int i = cible.IndexOf(':');
            string type = i < 0 ? cible : cible.Substring(0, i), cle = i < 0 ? "" : cible.Substring(i + 1);
            if (type == "page")
            {
                string rubrique = cle == "Demarrage" || cle == "Pilotes" || cle == "Potentiel" ? "Accelerer" : cle == "Materiel" ? "Outils" : cle;
                if (Rubriques.Any(r => r.Cle == rubrique)) { Afficher(rubrique, true); return; }
            }
            if (type == "uri" && cle.StartsWith("windowsdefender:", StringComparison.OrdinalIgnoreCase))
            {
                try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(cle) { UseShellExecute = true }); return; } catch { }
            }
            Statut("« " + libelle + " » arrive dans la prochaine étape de la nouvelle interface ; la version 4 le fait déjà.", true);
        }

        public FenetrePrincipale(ISourceBilan source)
        {
            Source = source;
            Title = "Nettoyage PC";
            Width = 1240; Height = 800; MinWidth = 980; MinHeight = 640;
            try { Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://NettoyagePC/Assets/NettoyagePC.ico"))); } catch { }
            DefinirRubriques();
            Construire();
            Activated += (s, e) => Horloge.Pause(false);
            Deactivated += (s, e) => Horloge.Pause(true);
            PropertyChanged += (s, e) => { if (e.Property == WindowStateProperty) Horloge.Pause(WindowState == WindowState.Minimized); };
        }

        private void DefinirRubriques()
        {
            Rubriques.Add(new Rubrique { Cle = "Accueil", Nom = "Accueil", Sous = "L'état de ton PC en un coup d'œil.", Page = () => new PageAccueil(this) });
            Rubriques.Add(new Rubrique { Cle = "Nettoyer", Nom = "Nettoyer", Sous = "Libère de l'espace sans toucher à tes documents.", Page = () => PageAVenir.Nettoyer() });
            Rubriques.Add(new Rubrique { Cle = "Accelerer", Nom = "Accélérer", Sous = "Démarrage, jeux, pilotes et applications.", Page = () => PageAVenir.Accelerer() });
            Rubriques.Add(new Rubrique { Cle = "Proteger", Nom = "Protéger", Sous = "Antivirus, bouclier anti-arnaque, sauvegarde et confidentialité.", Page = () => PageAVenir.Proteger() });
            Rubriques.Add(new Rubrique { Cle = "Outils", Nom = "Outils", Sous = "Matériel, plein potentiel, réseau et aide à distance.", Page = () => PageAVenir.Outils() });
            Rubriques.Add(new Rubrique { Cle = "Premium", Nom = "Premium", Sous = "Les outils des pros, débloqués d'un coup.", Page = () => PageAVenir.Premium() });
            Rubriques.Add(new Rubrique { Cle = "MisesAJour", Nom = "Mises à jour", Sous = "Version installée et nouveautés.", EnBas = true, Page = () => PageAVenir.Simple("Mises à jour", "Recherche des mises à jour, nouveautés de chaque version, installation en un clic.") });
            Rubriques.Add(new Rubrique { Cle = "Historique", Nom = "Historique", Sous = "Tout ce que Nettoyage PC a fait, et l'annulation.", EnBas = true, Page = () => PageAVenir.Simple("Historique", "Chaque modification est notée et peut être annulée.") });
            Rubriques.Add(new Rubrique { Cle = "Reglages", Nom = "Réglages", Sous = "Apparence, langue, profil et informations.", EnBas = true, Page = () => PageAVenir.Simple("Réglages", "Thème, langue, mode simple ou complet, entretien automatique.") });
        }

        // ------------------------------------------------------------ construction (refaite à chaque changement de thème)
        private void Construire()
        {
            var P = Habillage.P;
            Background = Habillage.Pinceau(P.Fond);
            var racine = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*") };
            racine.Children.Add(BarreDeMenu());
            var droite = new DockPanel { LastChildFill = true };
            Grid.SetColumn(droite, 1);
            var entete = EnTete(); DockPanel.SetDock(entete, Dock.Top); droite.Children.Add(entete);
            var barreEtat = BarreEtat(); DockPanel.SetDock(barreEtat, Dock.Bottom); droite.Children.Add(barreEtat);
            contenu = new TransitioningContentControl { PageTransition = Habillage.Animations ? new CrossFade(TimeSpan.FromMilliseconds(180)) : null };
            droite.Children.Add(contenu);
            racine.Children.Add(droite);
            Content = racine;
            Afficher(PageActive, false);
        }

        private Control BarreDeMenu()
        {
            var A = Color.FromRgb(61, 245, 255);
            var barre = new Border { Background = Habillage.Pinceau(Habillage.P.Barre), BorderBrush = Habillage.Pinceau(A, 46), BorderThickness = new Thickness(0, 0, 1, 0) };
            var grille = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto") };
            // logo et nom
            var tete = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20, 22, 0, 18), Spacing = 10 };
            try { tete.Children.Add(new Image { Source = new Bitmap(AssetLoader.Open(new Uri("avares://NettoyagePC/Assets/logo.png"))), Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center }); } catch { }
            var noms = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            noms.Children.Add(new TextBlock { Text = "Nettoyage PC", FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
            noms.Children.Add(new TextBlock { Text = "Version 5.0 · aperçu", FontFamily = Habillage.Corps, FontSize = 11, Foreground = Habillage.Pinceau(Color.FromRgb(138, 160, 180)) });
            tete.Children.Add(noms);
            grille.Children.Add(tete);
            // menu principal et groupe du bas
            var haut = new StackPanel { Spacing = 2 };
            var bas = new StackPanel { Spacing = 1, Margin = new Thickness(0, 0, 0, 16) };
            menu.Clear();
            int n = 0;
            foreach (var r in Rubriques)
            {
                var e = new ElementMenu(r.Cle, r.Nom, r.EnBas ? 0 : ++n, r.EnBas ? 36 : 42) { Margin = new Thickness(5, 0, 0, 0) };
                if (r.Cle == "Premium") e.Pastille = "PRO";
                e.Clic += x => Afficher(x.Cle, true);
                menu[r.Cle] = e;
                (r.EnBas ? bas : haut).Children.Add(e);
            }
            Grid.SetRow(haut, 1); Grid.SetRow(bas, 2);
            grille.Children.Add(haut); grille.Children.Add(bas);
            // indicateur lumineux de la rubrique active (bande de gauche), qui glisse
            calqueIndicateur = new Canvas { IsHitTestVisible = false };
            Grid.SetRowSpan(calqueIndicateur, 3);
            indicateur = new Border { Width = 6, Height = 34, RenderTransform = positionIndicateur, Opacity = 0, Background = Habillage.Pinceau(A, 45) };
            indicateur.Child = new Border { Width = 3, Height = 28, CornerRadius = new CornerRadius(1.5), Background = Habillage.Pinceau(A), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            calqueIndicateur.Children.Add(indicateur);
            grille.Children.Add(calqueIndicateur);
            barre.Child = grille;
            barre.LayoutUpdated += (s, e) => { if (indicateur.Opacity == 0) PlacerIndicateur(false); };
            return barre;
        }

        private Control EnTete()
        {
            var P = Habillage.P;
            var cadre = new Grid { Height = 78, RowDefinitions = new RowDefinitions("*,Auto") };
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(28, 10, 24, 0) };
            var textes = new StackPanel();
            titre = new TextBlock { Text = "", FontFamily = Habillage.Affichage, FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) };
            sousTitre = new TextBlock { Text = "", FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextTrimming = TextTrimming.CharacterEllipsis };
            textes.Children.Add(titre); textes.Children.Add(sousTitre);
            g.Children.Add(textes);
            var droite = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
            var recherche = new Border
            {
                Width = 250, Height = 38, CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1),
                Child = new Grid
                {
                    ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(12, 0, 8, 0),
                    Children =
                    {
                        new TextBlock { Text = "Que veux-tu faire ?", FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center },
                        Pastille("Ctrl+K", 1),
                    },
                },
            };
            droite.Children.Add(recherche);
            var bTheme = new BoutonNoyau(P.Sombre ? "☀" : "☾", false, 38, 38) { TaillePolice = 15 };
            bTheme.Clic += () => ChangerTheme(!Habillage.P.Sombre);
            droite.Children.Add(bTheme);
            droite.Children.Add(PastilleSimulation());
            Grid.SetColumn(droite, 1);
            g.Children.Add(droite);
            cadre.Children.Add(g);
            // trait du bas et lumière qui le parcourt au changement de rubrique
            ligneEntete = new Panel { Height = 2, ClipToBounds = true };
            ligneEntete.Children.Add(new Border { Height = 1, VerticalAlignment = VerticalAlignment.Bottom, Background = Habillage.Pinceau(P.Accent, P.Sombre ? 40 : 60) });
            lumiere = new Border
            {
                Width = 320, Height = 2, HorizontalAlignment = HorizontalAlignment.Left, RenderTransform = positionLumiere,
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Habillage.Alpha(P.Accent, 0), 0), new GradientStop(Habillage.Alpha(P.Accent, 230), 0.7), new GradientStop(Habillage.Alpha(P.Accent, 0), 1) },
                },
            };
            ligneEntete.Children.Add(lumiere);
            Grid.SetRow(ligneEntete, 1);
            cadre.Children.Add(ligneEntete);
            return cadre;
        }

        private static Control Pastille(string texte, int colonne)
        {
            var P = Habillage.P;
            var b = new Border
            {
                Padding = new Thickness(8, 2), CornerRadius = new CornerRadius(2), Background = Habillage.Pinceau(P.Bordure, 160), VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = texte, FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(P.Discret) },
            };
            Grid.SetColumn(b, colonne);
            return b;
        }

        private Control PastilleSimulation()
        {
            var P = Habillage.P;
            var point = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Habillage.Pinceau(Simulation ? P.Orange : P.Vert), VerticalAlignment = VerticalAlignment.Center };
            var texte = new TextBlock { Text = Simulation ? "Simulation : rien n'est modifié" : "Mode réel", FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center };
            var b = new Border
            {
                Height = 38, Padding = new Thickness(12, 0), CornerRadius = new CornerRadius(19), BorderThickness = new Thickness(1),
                BorderBrush = Habillage.Pinceau(Simulation ? P.Orange : P.Bordure, Simulation ? 150 : 255), Background = Habillage.Pinceau(Simulation ? P.Orange : P.Carte, Simulation ? 22 : 255),
                Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { point, texte } },
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };
            Avalonia.Controls.ToolTip.SetTip(b, "Au premier lancement, rien n'est modifié : tu vois ce qui serait fait. Clique pour passer en mode réel.");
            b.PointerReleased += (s, e) => { Simulation = !Simulation; Construire(); };
            return b;
        }

        private Control BarreEtat()
        {
            var P = Habillage.P;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Height = 44, Margin = new Thickness(18, 0, 14, 0) };
            statut = new TextBlock { Text = Simulation ? "Mode simulation : rien n'est modifié tant que tu ne passes pas en mode réel." : "Prêt.", FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(Simulation ? P.Orange : P.Discret), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            g.Children.Add(statut);
            var b = new BoutonNoyau("Afficher les détails", false, 160, 30) { TaillePolice = 13, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(b, 1); g.Children.Add(b);
            return new Border { Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(8, 13, 24) : Color.FromRgb(246, 249, 251)), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(0, 1, 0, 0), Child = g };
        }

        // ------------------------------------------------------------ navigation
        public void Afficher(string cle, bool anime)
        {
            var r = Rubriques.FirstOrDefault(x => x.Cle == cle) ?? Rubriques[0];
            bool change = r.Cle != PageActive || titre.Text == "";
            PageActive = r.Cle;
            foreach (var kv in menu) kv.Value.Actif = kv.Key == r.Cle;
            sousTitre.Text = r.Sous;
            if (anime && change && Habillage.Animations) { Decoder(r.Nom); Balayer(); } else titre.Text = r.Nom;
            contenu.Content = new ScrollViewer { Content = r.Page(), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            PlacerIndicateur(anime);
        }

        private void PlacerIndicateur(bool anime)
        {
            ElementMenu e;
            if (calqueIndicateur == null || !menu.TryGetValue(PageActive, out e)) return;
            var pos = e.TranslatePoint(new Point(0, 0), calqueIndicateur);
            if (!pos.HasValue || e.Bounds.Height <= 0) return;
            double cible = pos.Value.Y + (e.Bounds.Height - indicateur.Height) / 2;
            bool premiere = indicateur.Opacity == 0;
            indicateur.Opacity = 1;
            if (!anime || premiere || !Habillage.Animations) { positionIndicateur.Y = cible; return; }
            double depart = positionIndicateur.Y; long t0 = Horloge.Maintenant;
            Horloge.Lancer(t => { double p = Math.Min(1, (t - t0) / 260.0); positionIndicateur.Y = depart + (cible - depart) * Habillage.Douce(p); return p < 1; });
        }

        private static readonly Random hasard = new Random();
        private void Decoder(string texte)
        {
            const string signes = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            long t0 = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double p = Math.Min(1, (t - t0) / 320.0);
                int reveles = (int)Math.Round(texte.Length * Habillage.Douce(p));
                if (p >= 1) { titre.Text = texte; return false; }
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < texte.Length; i++) sb.Append(i < reveles || char.IsWhiteSpace(texte[i]) ? texte[i] : signes[hasard.Next(signes.Length)]);
                titre.Text = sb.ToString();
                return true;
            });
        }

        private void Balayer()
        {
            long t0 = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double p = Math.Min(1, (t - t0) / 700.0), w = Math.Max(400, ligneEntete.Bounds.Width);
                positionLumiere.X = -lumiere.Width + Habillage.Douce(p) * (w + lumiere.Width);
                if (p >= 1) positionLumiere.X = -lumiere.Width;
                return p < 1;
            });
        }

        public void ChangerTheme(bool sombre)
        {
            Habillage.P = sombre ? Palette.SombreNoyau : Palette.ClairNoyau;
            if (Application.Current != null) Application.Current.RequestedThemeVariant = sombre ? ThemeVariant.Dark : ThemeVariant.Light;
            Construire();
        }
    }
}
