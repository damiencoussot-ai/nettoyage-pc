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
                    : CarteAction.A("Mode jeu", "Avant de jouer : performances maximales et mémoire vive libérée. Un clic pour revenir comme avant après ta partie.", "Activer le mode jeu", "modejeu", true),
                CarteAction.A("Bilan d'optimisation", "Durée du démarrage, programmes au démarrage, mode d'alimentation, mises à jour disponibles : des conseils concrets, sans rien modifier.", "Lancer le bilan", "optim", true),
                CarteAction.A("Mettre à jour les applications", "Met à jour tes logiciels avec winget, l'outil officiel de Microsoft. Corrige aussi des failles de sécurité.", "Mettre à jour", "majapps"),
                CarteAction.A("Mode d'alimentation", (alim != "" ? "Mode actuel : " + alim + ". " : "") + (perf ? "Équilibré consomme moins (conseillé sur portable)." : "Performances élevées rend le PC plus réactif."),
                    perf ? "Revenir en Équilibré" : "Passer en Performances", perf ? "equilibre" : "perf"),
                CarteAction.A("Libérer la RAM", "Libère la mémoire vive inutilisée, idéal juste avant un jeu ou une application gourmande. Effet temporaire.", "Libérer", "ramlib").Plus(),
                CarteAction.A("Infos mémoire vive", "Tes barrettes (capacité, type, vitesse), applications les plus gourmandes et erreurs matérielles.", "Afficher", "raminfo").Plus(),
                CarteAction.A("Tester les barrettes", "Lance le Diagnostic de mémoire Windows pour détecter une barrette défectueuse. Demande un redémarrage.", "Tester", "ramtest").Plus(),
            }, "Optimisation");
        }

        /// <summary>Mode d'alimentation actif (lu dans le registre, sans rien modifier).</summary>
        private static string ModeAlimentation()
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
                CarteAction.Code("Preuve en FPS", "Mesure les images par seconde réelles d'un jeu, avant puis après un réglage : tu vois le vrai gain, en chiffres (PresentMon, l'outil gratuit d'Intel).", "Mesurer un jeu", () => f.Bientot("Preuve en FPS"), true),
                CarteAction.Code("Réglages des jeux", "Mode jeu de Windows, planification matérielle du GPU, optimisations pour le jeu en fenêtré : l'état de chacun et un conseil.", "Voir les réglages", () => f.Bientot("Réglages des jeux")),
            }, "Jeux");
        }
    }

    /// <summary>Programmes lancés avec Windows : décocher = ne plus le lancer (rien n'est supprimé, recocher pour annuler).</summary>
    internal sealed class PageDemarrage5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Tableau tableau;
        private readonly TextBlock info;
        private bool enLecture;

        public PageDemarrage5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12; Margin = new Thickness(0, 0, 12, 0);
            Children.Add(new TextBlock { Text = "Décoche un programme pour l'empêcher de se lancer avec Windows. Rien n'est supprimé : recoche-le pour le réactiver. Effet au prochain démarrage.", FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            tableau = new Tableau(true, false,
                new Colonne("Programme", "2*"), new Colonne("État", "100"), new Colonne("Emplacement", "1.4*"), new Colonne("Commande", "3*"));
            tableau.HauteurListe = 460;
            tableau.CaseChangee = Changer;
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            info = new TextBlock { Text = "Lecture de la liste…", FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret) };
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

        /// <summary>Liste lue en arrière-plan (registre, dossiers de démarrage) : la page reste fluide.</summary>
        private void Charger()
        {
            if (enLecture) return;
            enLecture = true;
            info.Text = "Lecture de la liste…";
            Task.Run(() => { try { return ElementsDemarrage.Lister(); } catch { return new List<ElementDemarrage>(); } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enLecture = false;
                var l = t.Result.OrderBy(x => x.Nom, StringComparer.CurrentCultureIgnoreCase).ToList();
                tableau.Remplir(l.Cast<object>(), o => { var el = (ElementDemarrage)o; return new[] { el.Nom, el.Actif ? "Activé" : "Désactivé", el.Lieu, el.Commande }; },
                    o => ((ElementDemarrage)o).Actif, o => !((ElementDemarrage)o).Actif);
                int actifs = l.Count(x => x.Actif);
                info.Text = l.Count == 0 ? "Aucun programme au démarrage." : actifs + " programme(s) se lancent avec Windows, " + (l.Count - actifs) + " désactivé(s).";
            }));
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
                    F.Statut("« " + el.Nom + " » se lancera de nouveau au démarrage.");
                    Historique.MarquerAnnulees(m => m.Type == "demarrage" && Registre.Normaliser(m.D("Appro")).Equals(el.Appro, StringComparison.OrdinalIgnoreCase) && m.D("NomAppro") == el.NomAppro);
                }
                else
                {
                    F.Statut("« " + el.Nom + " » ne se lancera plus au démarrage (recoche-le pour annuler).");
                    Historique.Ajouter("demarrage", "Programme au démarrage désactivé : " + el.Nom, new Dictionary<string, object> { { "Appro", el.Appro }, { "NomAppro", el.NomAppro }, { "Nom", el.Nom } });
                }
                return true;
            }
            catch (Exception ex)
            {
                F.Statut("Impossible de modifier « " + el.Nom + " » : " + ex.Message, true);
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
            bInutiles.Clic += () => F.Bientot("Applications préinstallées inutiles");
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
            F.Demander("Désinstaller « " + a.Nom + " » ?",
                "Le programme de désinstallation de l'application va s'ouvrir : suis ses étapes. Si tu as un doute sur ce programme, réponds « Annuler » et renseigne-toi d'abord.",
                "Désinstaller", "Annuler", oui =>
                {
                    if (!oui) return;
                    string cmd = a.Desinstaller ?? "";
                    if (Regex.IsMatch(cmd, "msiexec", RegexOptions.IgnoreCase)) cmd = Regex.Replace(cmd, @"/I\s*\{", "/X{", RegexOptions.IgnoreCase);
                    // les désinstallateurs demandent eux-mêmes l'autorisation de Windows s'ils en ont besoin
                    Programmes.LancerCommande(cmd);
                    F.Statut("Désinstallation de « " + a.Nom + " » lancée. Clique sur « Actualiser » quand elle est terminée.");
                }, true);
        }
    }
}
