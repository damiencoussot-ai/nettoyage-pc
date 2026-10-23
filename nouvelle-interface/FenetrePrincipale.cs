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

        // ---- actions du moteur (catalogue partagé avec la version 4)
        internal readonly ServiceMoteur Moteur = new ServiceMoteur();
        internal readonly Dictionary<string, ActionMoteur> Actions = CatalogueActions.Creer();
        private Dictionary<string, bool> disponible;
        private PanneauAction panneauAction;
        private bool panneauVisible;
        private Panel calqueDialogue;

        /// <summary>Pose une question dans la fenêtre ; « reponse » reçoit vrai pour le bouton principal.</summary>
        public void Demander(string titre, string texte, string oui, string non, Action<bool> reponse, bool alerte = false)
        {
            Dialogue d = null;
            d = new Dialogue(titre, texte, oui, non, r => { Retirer(d); reponse(r); }, alerte);
            Superposer(d);
        }

        /// <summary>Calque par-dessus la fenêtre (question, fenêtre intérieure) ; les calques s'empilent.</summary>
        public void Superposer(Control c)
        {
            calqueDialogue.Children.Add(c);
            calqueDialogue.IsVisible = true;
        }

        /// <summary>Ferme tous les calques (captures, changement de thème).</summary>
        public void ViderCalques() { calqueDialogue.Children.Clear(); calqueDialogue.IsVisible = false; }

        public void Retirer(Control c)
        {
            calqueDialogue.Children.Remove(c);
            calqueDialogue.IsVisible = calqueDialogue.Children.Count > 0;
        }

        /// <summary>Fenêtre intérieure (fond assombri, carte avec titre et « Fermer ») : bouclier, réglages de Windows...</summary>
        public FenetreInterieure Ouvrir(string titre, Control contenu, double largeur = 720)
        {
            var f = new FenetreInterieure(this, titre, contenu, largeur);
            Superposer(f);
            return f;
        }

        /// <summary>Onglets des rubriques : rubrique, clé de l'onglet (même clé que la page de la version 4), nom affiché.</summary>
        public static readonly string[][] Onglets5 =
        {
            new[] { "Nettoyer", "Nettoyage", "Nettoyage" }, new[] { "Nettoyer", "Disques", "Disques et gros fichiers" }, new[] { "Nettoyer", "Doublons", "Doublons" },
            new[] { "Accelerer", "Optimisation", "Optimisation" }, new[] { "Accelerer", "Demarrage", "Démarrage" }, new[] { "Accelerer", "Applications", "Applications" }, new[] { "Accelerer", "Jeux", "Jeux" },
            new[] { "Proteger", "Securite", "Sécurité" }, new[] { "Proteger", "Sauvegarde", "Sauvegarde" },
            new[] { "Outils", "Materiel", "Matériel" }, new[] { "Outils", "Potentiel", "Plein potentiel" }, new[] { "Outils", "Reseau", "Réseau" }, new[] { "Outils", "Aide", "Aide à distance" },
            new[] { "MisesAJour", "MisesAJour", "Mises à jour de Nettoyage PC" }, new[] { "MisesAJour", "Pilotes", "Windows et pilotes" },
        };

        /// <summary>« Que veux-tu faire ? » (Ctrl+K) : rubriques, onglets et actions du moteur.</summary>
        public void OuvrirPalette()
        {
            if (calqueDialogue.Children.OfType<PaletteCommandes>().Any()) return;
            var l = new List<EntreeCommande>();
            foreach (var r in Rubriques)
            {
                var rr = r;
                l.Add(new EntreeCommande { Titre = r.Nom, Detail = r.Sous, Type = "Rubrique", Faire = () => Afficher(rr.Cle, true) });
            }
            foreach (var o in Onglets5)
            {
                var oo = o;
                var r = Rubriques.FirstOrDefault(x => x.Cle == o[0]);
                l.Add(new EntreeCommande { Titre = o[2], Detail = r != null ? r.Nom : "", Type = "Rubrique", Faire = () => { SousPage(oo[0], oo[1]); Afficher(oo[0], true); } });
            }
            foreach (var a in Actions.Values)
            {
                var aa = a;
                l.Add(new EntreeCommande { Titre = a.Titre, Detail = Simulation && a.Simu ? Langue.T("en simulation : rien n'est modifié") : a.SansAdmin ? "" : Langue.T("demande l'autorisation de Windows"), Type = "Action", Faire = () => LancerAction(aa.Cle) });
            }
            l.Add(new EntreeCommande { Titre = Langue.T("Bouclier anti-arnaque"), Detail = Langue.T("Faux technicien ? Logiciels de prise en main à distance"), Type = "Action", Faire = () => Bouclier5.Ouvrir(this) });
            l.Add(new EntreeCommande { Titre = Langue.T(Habillage.P.Sombre ? "Thème clair" : "Thème sombre"), Detail = Langue.T("Apparence"), Type = "Action", Faire = () => { ChangerTheme(!Habillage.P.Sombre); EnregistrerReglage(r => r.Theme = Habillage.P.Sombre ? "Sombre" : "Clair"); } });
            foreach (var e in l) e.Cle = PaletteCommandes.Normaliser(e.Titre + " " + e.Detail);
            Superposer(new PaletteCommandes(this, l));
        }

        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled && e.Key == Avalonia.Input.Key.K && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control)) { e.Handled = true; OuvrirPalette(); }
        }

        /// <summary>Une fonction pas encore refaite dans la nouvelle interface : on le dit simplement.</summary>
        public void Bientot(string nom)
        {
            Demander(nom, "Cette fonction arrive bientôt dans la nouvelle interface. Elle existe déjà dans la version 4 de Nettoyage PC.", "Compris", null, r => { });
        }

        // ---- « Plus d'options » ouverts (gardés dans les réglages, comme la version 4) et page choisie dans chaque rubrique
        private HashSet<string> optionsOuvertes;
        private readonly Dictionary<string, string> sousPages = new Dictionary<string, string>();

        public bool OptionOuverte(string cle)
        {
            if (optionsOuvertes == null)
            {
                optionsOuvertes = new HashSet<string>();
                try { foreach (var o in (Parametres.Lire().OptionsOuvertes ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)) optionsOuvertes.Add(o); } catch { }
            }
            return optionsOuvertes.Contains(cle);
        }

        public void OptionOuverte(string cle, bool ouvert)
        {
            OptionOuverte(cle);
            if (ouvert) optionsOuvertes.Add(cle); else optionsOuvertes.Remove(cle);
            if (Source.Immediat) return;   // aperçus et essais : rien n'est enregistré
            try { var r = Parametres.Lire(); r.OptionsOuvertes = string.Join(",", optionsOuvertes); r.Ecrire(); } catch { }
        }

        /// <summary>Modifie et enregistre les réglages (même fichier que la version 4) ; rien n'est écrit pendant les aperçus.</summary>
        internal void EnregistrerReglage(Action<Parametres> modifier)
        {
            if (Source.Immediat) return;
            try { var r = Parametres.Lire(); modifier(r); r.Ecrire(); }
            catch (Exception ex) { JournalDemarrage.Erreur("réglages (version 5)", ex); }
        }

        public string SousPage(string rubrique) { string s; return sousPages.TryGetValue(rubrique, out s) ? s : null; }
        public void SousPage(string rubrique, string cle) { sousPages[rubrique] = cle; }

        public void FermerPanneauAction() { panneauVisible = false; if (panneauAction != null) panneauAction.IsVisible = false; }

        public void MontrerPanneauAction() { panneauVisible = true; if (panneauAction != null) panneauAction.IsVisible = true; }

        /// <summary>
        /// Lance une action du moteur. Simulation (pastille en haut) : l'action montre ce qu'elle ferait, sans rien modifier.
        /// Sinon, une question est posée avant toute action qui modifie le PC.
        /// </summary>
        public void LancerAction(string cle, Action apres = null) { LancerAction(cle, null, null, apres); }

        /// <summary>Avec des options en plus (sauvegarde : destination, dossiers) et une question propre à ce lancement.</summary>
        public void LancerAction(string cle, IEnumerable<string> supplementaires, string confirmation, Action apres = null)
        {
            ActionMoteur a;
            if (!Actions.TryGetValue(cle, out a)) { Statut("Action inconnue : " + cle, true); return; }
            if (disponible == null) { try { disponible = CatalogueActions.Disponibilites(); } catch { disponible = new Dictionary<string, bool>(); } }
            bool dispo;
            if (a.Requiert != null && disponible.TryGetValue(a.Requiert, out dispo) && !dispo)
            {
                string raison; CatalogueActions.Raisons.TryGetValue(a.Requiert, out raison);
                Demander(a.Titre, Langue.T(raison ?? "Cette action n'est pas disponible sur ce PC."), "Compris", null, r => { });
                return;
            }
            // aperçu : les tâches planifiées pointeraient vers ce fichier d'aperçu (qui peut être déplacé ou supprimé)
            if (cle == "planifier" || cle == "deplanifier")
            {
                Demander(a.Titre, "Dans cet aperçu de la version 5, l'entretien automatique se règle depuis la version 4 installée (Réglages > Entretien automatique) : les tâches de Windows doivent pointer vers le programme installé, pas vers ce fichier d'aperçu.", "Compris", null, r => { });
                return;
            }
            if (Moteur.EnCours) { Demander("Une action est déjà en cours", "Attends qu'elle se termine, ou arrête-la avec le bouton « Arrêter » en bas de la fenêtre.", "Compris", null, r => { }); return; }
            bool simulation = Simulation && a.Simu;
            var options = supplementaires == null ? null : supplementaires.ToList();
            Action partir = () =>
            {
                MontrerPanneauAction();
                Statut((simulation ? "Simulation : " : "En cours : ") + a.Titre + "…");
                Action fini = null;
                fini = () =>
                {
                    Moteur.Fini -= fini;
                    Statut((Moteur.Arrete ? "Arrêtée : " : "Terminé : ") + Moteur.Titre + " (" + DateTime.Now.ToString("HH:mm") + ")");
                    if (apres != null) { try { apres(); } catch (Exception ex) { JournalDemarrage.Erreur("après l'action (version 5)", ex); } }
                    if (!simulation) ActualiserBilan();
                    if (cle == "planifier" || cle == "deplanifier") { PageReglages5.EntretienActif = null; if (PageActive == "Reglages") Afficher("Reglages", false); }
                };
                Moteur.Fini += fini;
                Moteur.Lancer(a, simulation, options, erreur =>
                {
                    Moteur.Fini -= fini;
                    if (erreur == "refus") Statut("Autorisation refusée : l'action n'a pas été lancée, rien n'a été modifié.", true);
                    else Statut("Action impossible : " + erreur, true);
                    FermerPanneauAction();
                });
            };
            string conf = confirmation ?? a.Confirmer;
            if (simulation || conf == null) { partir(); return; }
            // texte de confirmation de la version 4 : première ligne = question, le reste = explication
            conf = Langue.T(conf);
            int coupe = conf.IndexOf("\n\n", StringComparison.Ordinal);
            string question = coupe > 0 ? conf.Substring(0, coupe) : conf, detail = coupe > 0 ? conf.Substring(coupe + 2) : "";
            Demander(question, detail, "Oui, lancer", "Annuler", oui => { if (oui) partir(); else Statut("Annulé : rien n'a été modifié."); });
        }

        /// <summary>Bouton d'un point du bilan (ou de l'accueil) : action du moteur, rubrique, lien.</summary>
        public void Executer(string cible, string libelle)
        {
            if (string.IsNullOrEmpty(cible)) return;
            int i = cible.IndexOf(':');
            string type = i < 0 ? cible : cible.Substring(0, i), cle = i < 0 ? "" : cible.Substring(i + 1);
            if (type == "action" && Actions.ContainsKey(cle)) { LancerAction(cle); return; }
            if (type == "page")
            {
                // pages de la version 4 → rubrique (et onglet) de la nouvelle interface
                var ou = Onglets5.Where(o => o[1] == cle).Select(o => new[] { o[0], o[1] }).ToDictionary(o => o[1], o => o);
                string[] cible2;
                string rubrique = ou.TryGetValue(cle, out cible2) ? cible2[0] : cle;
                if (cible2 != null) SousPage(cible2[0], cible2[1]);
                if (Rubriques.Any(r => r.Cle == rubrique)) { Afficher(rubrique, true); return; }
            }
            if (type == "code" && cle == "bouclier") { Bouclier5.Ouvrir(this); return; }
            if (type == "code" && cle == "redemarrer")
            {
                Demander("Redémarrer le PC maintenant ?", "Enregistre ton travail et ferme tes applications avant.", "Redémarrer", "Annuler", oui =>
                {
                    if (oui) Programmes.Executer(System.IO.Path.Combine(Chemins.Systeme32, "shutdown.exe"), "/r /t 5", 5000);
                }, true);
                return;
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
            Rubriques.Add(new Rubrique { Cle = "Nettoyer", Nom = "Nettoyer", Sous = "Libère de l'espace sans toucher à tes documents.", Page = () => PageNettoyer.Creer(this) });
            Rubriques.Add(new Rubrique { Cle = "Accelerer", Nom = "Accélérer", Sous = "Démarrage, jeux, pilotes et applications.", Page = () => PageAccelerer.Creer(this) });
            Rubriques.Add(new Rubrique { Cle = "Proteger", Nom = "Protéger", Sous = "Antivirus, bouclier anti-arnaque, sauvegarde et confidentialité.", Page = () => PageProteger.Creer(this) });
            Rubriques.Add(new Rubrique { Cle = "Outils", Nom = "Outils", Sous = "Matériel, plein potentiel, réseau et aide à distance.", Page = () => PageOutils.Creer(this) });
            Rubriques.Add(new Rubrique { Cle = "Premium", Nom = "Premium", Sous = "Les outils des pros, débloqués d'un coup.", Page = () => new PagePremium5(this) });
            Rubriques.Add(new Rubrique { Cle = "MisesAJour", Nom = "Mises à jour", Sous = "Version installée et nouveautés.", EnBas = true, Page = () => RubriqueMisesAJour.Creer(this) });
            Rubriques.Add(new Rubrique { Cle = "Historique", Nom = "Historique", Sous = "Tout ce que Nettoyage PC a fait, et l'annulation.", EnBas = true, Page = () => new PageHistorique5(this) });
            Rubriques.Add(new Rubrique { Cle = "Reglages", Nom = "Réglages", Sous = "Apparence, langue, profil et informations.", EnBas = true, Page = () => PageReglages5.Creer(this) });
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
            panneauAction = new PanneauAction(this, Moteur) { IsVisible = panneauVisible };
            DockPanel.SetDock(panneauAction, Dock.Bottom); droite.Children.Add(panneauAction);
            contenu = new TransitioningContentControl { PageTransition = Habillage.Animations ? new CrossFade(TimeSpan.FromMilliseconds(180)) : null };
            droite.Children.Add(contenu);
            racine.Children.Add(droite);
            // calque des questions (par-dessus tout, caché tant qu'aucune question n'est posée)
            calqueDialogue = new Panel { IsVisible = false };
            Content = new Panel { Children = { racine, calqueDialogue } };
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
            recherche.Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);
            recherche.PointerReleased += (s, e) => OuvrirPalette();
            droite.Children.Add(recherche);
            var bTheme = new BoutonNoyau("", false, 38, 38) { Icone = P.Sombre ? "soleil" : "lune" };
            ToolTip.SetTip(bTheme, P.Sombre ? "Passer en thème clair" : "Passer en thème sombre");
            bTheme.Clic += () => { ChangerTheme(!Habillage.P.Sombre); EnregistrerReglage(r => r.Theme = Habillage.P.Sombre ? "Sombre" : "Clair"); };
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
