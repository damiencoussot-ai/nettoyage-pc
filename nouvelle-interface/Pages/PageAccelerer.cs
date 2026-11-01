// Nettoyage PC 5 - rubrique Accélérer : optimisation (mode jeu, alimentation, RAM), programmes au démarrage
// (décocher = ne plus lancer, réversible), applications installées (désinstallation par leur propre programme).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal static class PageAccelerer
    {
        public static Control Creer(FenetrePrincipale f, string premiere = null)
        {
            return new Onglets(f, "Accelerer", new[]
            {
                Tuple.Create<string, string, Func<Control>>("Optimisation", "Optimisation", () => Optimisation(f)),
                Tuple.Create<string, string, Func<Control>>("Demarrage", "Démarrage", () => new PageDemarrage5(f)),
                Tuple.Create<string, string, Func<Control>>("Applications", "Applications", () => new PageApplications5(f)),
                Tuple.Create<string, string, Func<Control>>("Jeux", "Jeux", () => Jeux(f)),
            }, premiere);
        }

        private static Control Optimisation(FenetrePrincipale f)
        {
            bool modeJeu = File.Exists(Chemins.Fichier("modejeu.json"));
            string alim = ModeAlimentation();
            bool perf = alim == "Performances élevées";
            return Cartes.Page(f, null, new[]
            {
                modeJeu
                    ? CarteAction.A("Mode jeu (actif)", "Le mode jeu est en cours : performances maximales. Termine-le après ta partie pour revenir comme avant.", "Terminer le mode jeu", "finmodejeu", true)
                    : CarteAction.A("Mode jeu", "Avant de jouer : performances maximales et mémoire vive libérée. Un clic pour revenir comme avant après ta partie. Avec Premium, le mode jeu automatique le fait tout seul à chaque jeu.", "Activer le mode jeu", "modejeu", true),
                CarteAction.A("Bilan d'optimisation", "Durée du démarrage, programmes au démarrage, mode d'alimentation, mises à jour disponibles : des conseils concrets, sans rien modifier.", "Lancer le bilan", "optim", true),
                CarteAction.A("Mettre à jour les applications", "Met à jour tes logiciels avec winget, l'outil officiel de Microsoft. Corrige aussi des failles de sécurité. Avec Premium, c'est automatique chaque semaine.", "Mettre à jour", "majapps"),
                CarteAction.A("Mode d'alimentation", (alim != "" ? Langue.F("Mode actuel : {0}.", Langue.T(alim)) + " " : "") + (perf ? "Équilibré consomme moins (conseillé sur portable)." : "Performances élevées rend le PC plus réactif."),
                    perf ? "Revenir en Équilibré" : "Passer en Performances", perf ? "equilibre" : "perf"),
                CarteAction.A("Libérer la RAM", "Libère la mémoire vive inutilisée, idéal juste avant un jeu ou une application gourmande. Effet temporaire.", "Libérer", "ramlib").Plus(),
                CarteAction.A("Infos mémoire vive", "Tes barrettes (capacité, type, vitesse), applications les plus gourmandes et erreurs matérielles.", "Afficher", "raminfo").Plus(),
                CarteAction.A("Tester les barrettes", "Lance le Diagnostic de mémoire Windows pour détecter une barrette défectueuse. Demande un redémarrage.", "Tester", "ramtest").Plus(),
            }, "Optimisation");
        }

        /// <summary>Mode d'alimentation actif (lu dans le registre, sans rien modifier).</summary>
        internal static string ModeAlimentation()
        {
            try
            {
                string id = Registre.Texte(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActivePowerScheme").ToLowerInvariant();
                if (id.StartsWith("8c5e7fda") || id.StartsWith("e9a42b02")) return "Performances élevées";
                if (id.StartsWith("381b4222")) return "Équilibré";
                if (id.StartsWith("a1841308")) return "Économie d'énergie";
                return id != "" ? "personnalisé" : "";
            }
            catch { return ""; }
        }

        private static Control Jeux(FenetrePrincipale f)
        {
            return Cartes.Page(f, null, new[]
            {
                CarteAction.Code("Preuve en FPS", "Mesure les images par seconde réelles d'un jeu, avant puis après un réglage : tu vois le vrai gain, en chiffres (PresentMon, l'outil gratuit d'Intel).", "Mesurer un jeu", () => Fps5.OuvrirPremium(f), true),
                CarteAction.Code("Réglages des jeux", "Mode jeu de Windows, planification matérielle du GPU, optimisations pour le jeu en fenêtré : l'état de chacun et un conseil.", "Voir les réglages", () => ReglagesWindows5.Jeux(f)),
            }, "Jeux");
        }
    }

    /// <summary>
    /// Programmes lancés avec Windows : décocher = ne plus le lancer (rien n'est supprimé, recocher pour annuler).
    /// Chaque programme a un impact estimé et un conseil (à garder, selon ton usage, peut attendre) ; un bouton désactive
    /// d'un coup ceux qui peuvent attendre, après une question qui les liste.
    /// </summary>
    internal sealed class PageDemarrage5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Tableau tableau;
        private readonly TextBlock info, resume, detail;
        private readonly BoutonNoyau bAttendre;
        private List<ElementDemarrage> liste = new List<ElementDemarrage>();
        private bool enLecture;

        /// <summary>(captures) liste d'exemple : rien n'est lu sur le PC.</summary>
        public static List<ElementDemarrage> Exemple;

        public PageDemarrage5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12; Margin = new Thickness(0, 0, 12, 0);
            // résumé et bouton « désactiver ceux qui peuvent attendre »
            var carte = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            resume = new TextBlock { Text = Langue.T("Lecture de la liste…"), FontFamily = Habillage.Corps, FontSize = 14.5, LineHeight = 21, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            carte.Children.Add(resume);
            bAttendre = new BoutonNoyau("Désactiver ceux qui peuvent attendre", true, 300, 38) { TaillePolice = 14, IsVisible = false, Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            bAttendre.Clic += DesactiverCeuxQuiPeuventAttendre;
            Grid.SetColumn(bAttendre, 1); carte.Children.Add(bAttendre);
            Children.Add(new Border { Child = carte, Padding = new Thickness(18, 14), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1) });
            Children.Add(new TextBlock { Text = "Décoche un programme pour l'empêcher de se lancer avec Windows. Rien n'est supprimé : recoche-le pour le réactiver. Effet au prochain démarrage.", FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            tableau = new Tableau(true, false,
                new Colonne("Programme", "1.5*"), new Colonne("État", "95"),
                new Colonne("Impact", "80", false, false, o => -Conseil(o).Impact),
                new Colonne("À faire", "135", false, false, o => -(int)Conseil(o).Choix),
                new Colonne("Conseil", "3.4*"));
            tableau.HauteurListe = 400;
            tableau.CaseChangee = Changer;
            // conseil complet du programme choisi (la colonne est souvent coupée)
            detail = new TextBlock { Text = Langue.T("Clique sur un programme pour lire le conseil en entier."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            tableau.SelectionChangee += () =>
            {
                var el = tableau.Selection as ElementDemarrage;
                if (el == null) return;
                var c = ConseilsDemarrage.Pour(el.Nom, el.Commande);
                detail.Text = el.Nom + " · " + Langue.T(c.TexteChoix) + " · " + Langue.T("impact") + " " + Langue.T(c.TexteImpact).ToLower(Langue.Culture) + " — " + Langue.T(c.Conseil);
                detail.Foreground = Habillage.Pinceau(c.Choix == ChoixDemarrage.PeutAttendre ? Habillage.P.Accent : Habillage.P.Texte);
            };
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            Children.Add(detail);
            info = new TextBlock { Text = "", FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var bActu = new BoutonNoyau("Actualiser", false, 110, 32) { TaillePolice = 13 };
            bActu.Clic += Charger;
            var bOuvrir = new BoutonNoyau("Ouvrir l'emplacement", false, 180, 32) { TaillePolice = 13 };
            bOuvrir.Clic += OuvrirEmplacement;
            boutons.Children.Add(bActu); boutons.Children.Add(bOuvrir);
            Children.Add(boutons);
            Children.Add(info);
            AttachedToVisualTree += (s, e) => Charger();
        }

        private static ConseilDemarrage Conseil(object o) { var el = (ElementDemarrage)o; return ConseilsDemarrage.Pour(el.Nom, el.Commande); }

        /// <summary>Liste lue en arrière-plan (registre, dossiers de démarrage) : la page reste fluide.</summary>
        private void Charger()
        {
            if (enLecture) return;
            enLecture = true;
            resume.Text = Langue.T("Lecture de la liste…");
            Task.Run(() => { if (Exemple != null) return Exemple; try { return ElementsDemarrage.Lister(); } catch { return new List<ElementDemarrage>(); } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enLecture = false;
                // actifs d'abord, puis ceux qui peuvent attendre, puis par impact
                liste = t.Result.OrderByDescending(x => x.Actif).ThenByDescending(x => (int)Conseil(x).Choix).ThenByDescending(x => Conseil(x).Impact)
                    .ThenBy(x => x.Nom, StringComparer.CurrentCultureIgnoreCase).ToList();
                Afficher();
            }));
        }

        private void Afficher()
        {
            tableau.Remplir(liste.Cast<object>(), o =>
            {
                var el = (ElementDemarrage)o; var c = Conseil(o);
                return new[] { el.Nom, el.Actif ? "Activé" : "Désactivé", c.TexteImpact, c.TexteChoix, c.Conseil };
            }, o => ((ElementDemarrage)o).Actif, o => !((ElementDemarrage)o).Actif);
            resume.Text = ConseilsDemarrage.Resume(liste);
            int n = ConseilsDemarrage.PeuventAttendre(liste).Count;
            bAttendre.IsVisible = n > 0;
            bAttendre.Changer(Langue.F("Désactiver ceux qui peuvent attendre ({0})", n));
            int actifs = liste.Count(x => x.Actif);
            info.Text = liste.Count == 0 ? "" : Langue.F("{0} programme(s) se lancent avec Windows, {1} désactivé(s).", actifs, liste.Count - actifs)
                + " " + Langue.T("L'impact est une estimation d'après le type de programme ; l'Analyse du démarrage (Premium) mesure le temps exact.");
        }

        private void DesactiverCeuxQuiPeuventAttendre()
        {
            var cibles = ConseilsDemarrage.PeuventAttendre(liste);
            if (cibles.Count == 0) return;
            string noms = string.Join("\n", cibles.Select(e => "•  " + e.Nom));
            F.Demander(Langue.F("Désactiver {0} programme(s) au démarrage ?", cibles.Count),
                Langue.T("Ils ne se lanceront plus avec Windows. Rien n'est désinstallé : tu les ouvres toi-même quand tu en as besoin, et tu peux les recocher ici à tout moment.") + "\n\n" + noms,
                "Désactiver", "Annuler", oui =>
                {
                    if (!oui) return;
                    if (F.Simulation) { F.Statut(Langue.F("Simulation : {0} programme(s) seraient désactivés au démarrage, rien n'est modifié.", cibles.Count)); return; }
                    int ok = 0;
                    foreach (var el in cibles) if (Changer(el, false)) ok++;
                    Afficher();
                    if (ok == cibles.Count) F.Statut(Langue.F("{0} programme(s) ne se lanceront plus au démarrage (recoche-les pour annuler).", ok));
                    else F.Statut(Langue.F("{0} programme(s) désactivé(s), {1} n'ont pas pu l'être : ils concernent tous les comptes et demandent les droits d'administrateur (Gestionnaire des tâches, onglet Démarrage).", ok, cibles.Count - ok), true);
                });
        }

        private bool Changer(object o, bool actif)
        {
            var el = (ElementDemarrage)o;
            try
            {
                GestionDemarrage.Approuver(el.Appro, el.NomAppro, actif);
                el.Actif = actif;
                if (actif)
                {
                    F.Statut(Langue.F("« {0} » se lancera de nouveau au démarrage.", el.Nom));
                    Historique.MarquerAnnulees(m => m.Type == "demarrage" && Registre.Normaliser(m.D("Appro")).Equals(el.Appro, StringComparison.OrdinalIgnoreCase) && m.D("NomAppro") == el.NomAppro);
                }
                else
                {
                    F.Statut(Langue.F("« {0} » ne se lancera plus au démarrage (recoche-le pour annuler).", el.Nom));
                    Historique.Ajouter("demarrage", "Programme au démarrage désactivé : " + el.Nom, new Dictionary<string, object> { { "Appro", el.Appro }, { "NomAppro", el.NomAppro }, { "Nom", el.Nom } });
                }
                return true;
            }
            catch (Exception ex)
            {
                F.Statut(Langue.F("Impossible de modifier « {0} » :", el.Nom) + " " + ex.Message, true);
                return false;
            }
        }

        private void OuvrirEmplacement()
        {
            var el = tableau.Selection as ElementDemarrage;
            if (el == null) { F.Statut("Sélectionne d'abord un programme dans la liste."); return; }
            string cmd = el.Commande ?? "", chemin = cmd;
            var m = Regex.Match(cmd, "^\"([^\"]+)\""); if (m.Success) chemin = m.Groups[1].Value;
            else { m = Regex.Match(cmd, @"^(\S+\.exe)", RegexOptions.IgnoreCase); if (m.Success) chemin = m.Groups[1].Value; }
            chemin = Environment.ExpandEnvironmentVariables(chemin);
            if (File.Exists(chemin)) Programmes.MontrerDansExplorateur(chemin);
            else F.Demander("Fichier introuvable", chemin, "Compris", null, r => { });
        }
    }

    /// <summary>Applications installées : recherche, tri, désinstallation par le programme de l'application (avec question avant).</summary>
    internal sealed class PageApplications5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBox recherche = new TextBox { Width = 280, Watermark = "Rechercher une application" };
        private readonly Tableau tableau;
        private readonly TextBlock info;
        private List<ProgrammeInstalle> applications = new List<ProgrammeInstalle>();
        private bool enLecture;

        public PageApplications5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12; Margin = new Thickness(0, 0, 12, 0);
            var haut = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            haut.Children.Add(recherche);
            info = new TextBlock { Text = "Chargement de la liste des applications…", FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center };
            haut.Children.Add(info);
            Children.Add(haut);
            tableau = new Tableau(false, false,
                new Colonne("Application", "3*"), new Colonne("Éditeur", "2*"),
                new Colonne("Taille", "100", true, true, o => ((ProgrammeInstalle)o).Octets),
                new Colonne("Installée le", "110", false, true, o => ((ProgrammeInstalle)o).Date ?? DateTime.MinValue),
                new Colonne("Version", "1.2*"));
            tableau.HauteurListe = 440;
            tableau.DoubleClic += o => Desinstaller();
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            var boutons = new WrapPanel { Orientation = Orientation.Horizontal };
            var bDes = new BoutonNoyau("Désinstaller", true, 140, 32) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 10, 6) };
            bDes.Clic += Desinstaller;
            var bActu = new BoutonNoyau("Actualiser", false, 110, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bActu.Clic += Charger;
            var bParam = new BoutonNoyau("Ouvrir Paramètres > Applications", false, 250, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bParam.Clic += () => Programmes.Ouvrir("ms-settings:appsfeatures");
            var bInutiles = new BoutonNoyau("Applications préinstallées inutiles", false, 260, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bInutiles.Clic += () => AppsInutiles5.Ouvrir(F);
            boutons.Children.Add(bDes); boutons.Children.Add(bActu); boutons.Children.Add(bParam); boutons.Children.Add(bInutiles);
            Children.Add(boutons);
            recherche.TextChanged += (s, e) => Afficher();
            AttachedToVisualTree += (s, e) => { if (applications.Count == 0) Charger(); };
        }

        private void Charger()
        {
            if (enLecture) return;
            enLecture = true;
            info.Text = "Chargement de la liste des applications…";
            Task.Run(() => { try { return ProgrammesInstalles.Lister(); } catch { return new List<ProgrammeInstalle>(); } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enLecture = false;
                applications = t.Result ?? new List<ProgrammeInstalle>();
                Afficher();
            }));
        }

        private void Afficher()
        {
            string filtre = (recherche.Text ?? "").Trim();
            IEnumerable<ProgrammeInstalle> l = applications;
            if (filtre != "") l = l.Where(a => (a.Nom ?? "").IndexOf(filtre, StringComparison.CurrentCultureIgnoreCase) >= 0 || (a.Editeur ?? "").IndexOf(filtre, StringComparison.CurrentCultureIgnoreCase) >= 0);
            var liste = l.OrderByDescending(a => a.Octets).ToList();
            tableau.Remplir(liste.Cast<object>(), o =>
            {
                var a = (ProgrammeInstalle)o;
                return new[] { a.Nom, a.Editeur, a.Octets > 0 ? Format.Taille(a.Octets) : "", a.Date.HasValue ? a.Date.Value.ToString("dd/MM/yyyy") : "", a.Version };
            });
            info.Text = Langue.F("{0} application(s), {1} au total (taille estimée)", liste.Count, Format.Taille(liste.Sum(a => a.Octets)));
        }

        private void Desinstaller()
        {
            var a = tableau.Selection as ProgrammeInstalle;
            if (a == null) { F.Statut("Sélectionne d'abord une application dans la liste."); return; }
            F.Demander(Langue.F("Désinstaller « {0} » ?", a.Nom),
                "Le programme de désinstallation de l'application va s'ouvrir : suis ses étapes. Si tu as un doute sur ce programme, réponds « Annuler » et renseigne-toi d'abord.",
                "Désinstaller", "Annuler", oui =>
                {
                    if (!oui) return;
                    string cmd = a.Desinstaller ?? "";
                    if (Regex.IsMatch(cmd, "msiexec", RegexOptions.IgnoreCase)) cmd = Regex.Replace(cmd, @"/I\s*\{", "/X{", RegexOptions.IgnoreCase);
                    // les désinstallateurs demandent eux-mêmes l'autorisation de Windows s'ils en ont besoin
                    Programmes.LancerCommande(cmd);
                    F.Statut(Langue.F("Désinstallation de « {0} » lancée. Clique sur « Actualiser » quand elle est terminée.", a.Nom));
                }, true);
        }
    }
}
