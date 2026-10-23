// Nettoyage PC 5 - démarrage. « --captures DOSSIER » : rendu sans écran de la fenêtre, enregistré en PNG
// (aperçus et essais automatiques ; rien n'est lu ni modifié sur le PC).
using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace NettoyagePC.Nouveau
{
    public sealed class App : Application
    {
        public override void Initialize()
        {
            Styles.Add(new FluentTheme());
            RequestedThemeVariant = Habillage.P.Sombre ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        /// <summary>Source des données de l'accueil : le vrai PC sous Windows, l'exemple ailleurs (essais sous Linux).</summary>
        public static ISourceBilan Source = OperatingSystem.IsWindows() ? new SourceBilanPc() : (ISourceBilan)new BilanExemple();
        public static string Pseudo = "";

        /// <summary>(essais) --essai-demarrage FICHIER : mesure l'ouverture de la fenêtre et l'arrivée du bilan, puis ferme.</summary>
        public static string FichierEssaiDemarrage;

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime bureau)
            {
                var f = new FenetrePrincipale(Source) { Pseudo = Pseudo };
                bureau.MainWindow = f;
                if (FichierEssaiDemarrage != null)
                {
                    var debut = System.Diagnostics.Process.GetCurrentProcess().StartTime;
                    string ouverture = "?";
                    f.Opened += (s, e) => ouverture = ((int)(DateTime.Now - debut).TotalMilliseconds).ToString();
                    f.BilanPret += b =>
                    {
                        string texte = "fenêtre ouverte : " + ouverture + " ms\nbilan affiché : " + (int)(DateTime.Now - debut).TotalMilliseconds + " ms (note " + b.Score + ", " + b.Points.Count + " points)\n";
                        try { File.WriteAllText(FichierEssaiDemarrage, texte); } catch { }
                        DispatcherTimer.RunOnce(() => bureau.Shutdown(0), TimeSpan.FromSeconds(2));
                    };
                    DispatcherTimer.RunOnce(() => { try { File.AppendAllText(FichierEssaiDemarrage, "délai dépassé (fenêtre : " + ouverture + " ms)\n"); } catch { } bureau.Shutdown(2); }, TimeSpan.FromSeconds(60));
                }
            }
            base.OnFrameworkInitializationCompleted();
        }
    }

    public static class Programme
    {
        [STAThread]
        public static int Main(string[] args)
        {
            int i = Array.IndexOf(args, "--captures");
            // --captures DOSSIER [--reel] : avec --reel (sous Windows), l'accueil montre le vrai bilan du PC (lecture seule)
            if (i >= 0 && i + 1 < args.Length) return Captures.Faire(args[i + 1], Array.IndexOf(args, "--reel") >= 0);

            // même programme que la version 4 pour tout ce qui n'a pas de fenêtre : moteur (--moteur ...),
            // commandes administrateur (--commande ...), état du PC en JSON (--etat)...
            JournalDemarrage.Preparer();
            Demarrage.AppliquerDossiersUtilisateur(args);
            int? code = Demarrage.SansInterface(args);
            if (code.HasValue) return code.Value;
            // le gardien (zone de notification) reste celui de la version 4 installée : l'aperçu n'ouvre rien
            if (args.Length > 0 && args[0] == "--gardien") { JournalDemarrage.Noter("Aperçu de la version 5 : le gardien reste celui de la version 4."); return 0; }

            int ie = Array.IndexOf(args, "--essai-demarrage");
            if (ie >= 0 && ie + 1 < args.Length) App.FichierEssaiDemarrage = Path.GetFullPath(args[ie + 1]);
            JournalDemarrage.Noter("--- Démarrage de Nettoyage PC 5 (aperçu) : Windows " + Environment.OSVersion.Version + ", .NET " + Environment.Version + ", dossier " + Chemins.Ici);
            // toujours avec les droits normaux : lancée en administrateur, l'application se relance par l'Explorateur
            if (Appli.EstAdmin && Natif.EstWindows && App.FichierEssaiDemarrage == null && Demarrage.RelancerSansAdmin()) return 0;
            AppDomain.CurrentDomain.UnhandledException += (s, e) => JournalDemarrage.Erreur("fatale (version 5)", e.ExceptionObject as Exception);
            try
            {
                var reglages = Parametres.Lire();
                Langue.Choisir(reglages.Langue);
                App.Pseudo = reglages.Pseudo ?? "";
                if (reglages.Theme == "Clair" || (reglages.Theme == "Auto" && WindowsEnClair())) Habillage.P = Palette.ClairNoyau;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("réglages (version 5)", ex); }
            try { Licence.Charger(); } catch (Exception ex) { JournalDemarrage.Erreur("licence (version 5)", ex); }
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            JournalDemarrage.Noter("Fin normale (version 5).");
            return 0;
        }

        /// <summary>Thème des applications choisi dans Windows (Paramètres > Personnalisation > Couleurs).</summary>
        internal static bool WindowsEnClair()
        {
            if (!OperatingSystem.IsWindows()) return false;
            try { return Convert.ToInt32(Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 0) ?? 0) == 1; }
            catch { return false; }
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
        }
    }

    /// <summary>Captures de la fenêtre dans les deux thèmes, sur l'accueil et une autre rubrique.</summary>
    public static class Captures
    {
        private static void Capturer(FenetrePrincipale f, string dossier, string nom, System.Text.StringBuilder rapport, ref int erreurs)
        {
            for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
            var image = f.CaptureRenderedFrame();
            if (image == null) { erreurs++; rapport.AppendLine("ERREUR : capture vide " + nom); return; }
            image.Save(Path.Combine(dossier, nom));
            rapport.AppendLine("OK : " + nom);
        }

        public static int Faire(string dossier, bool reel)
        {
            Directory.CreateDirectory(dossier);
            Habillage.Animations = false;   // états finaux, sans attendre les animations
            int erreurs = 0;
            var rapport = new System.Text.StringBuilder();
            ISourceBilan source = new BilanExemple();
            if (reel)
            {
                // vrai bilan, calculé une fois avant les captures (quelques secondes ; rien n'est modifié)
                try
                {
                    JournalDemarrage.Preparer();
                    var chrono = System.Diagnostics.Stopwatch.StartNew();
                    var figee = new SourceFigee(new SourceBilanPc());
                    rapport.AppendLine("Bilan réel : note " + figee.Bilan.Score + ", " + figee.Bilan.Points.Count + " points, en " + chrono.ElapsedMilliseconds + " ms");
                    foreach (var p in figee.Bilan.Points) rapport.AppendLine("  [" + p.Etat + "] " + p.Texte + (p.Cible != null ? "  -> " + p.Cible : ""));
                    rapport.AppendLine("  secteurs : " + string.Join(" / ", figee.Bilan.Secteurs));
                    rapport.AppendLine("  mesures : processeur " + figee.Bilan.Processeur + " %, mémoire " + figee.Bilan.Memoire + " %, disque " + figee.Bilan.DisqueLibre + ", température " + figee.Bilan.Temperature);
                    source = figee;
                }
                catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : bilan réel : " + ex); }
            }
            AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            var f = new FenetrePrincipale(source) { Pseudo = "Damien" };
            f.Show();
            foreach (var sombre in new[] { true, false })
            {
                f.ChangerTheme(sombre);
                foreach (var cle in new[] { "Accueil", "Nettoyer", "Premium" })
                {
                    try
                    {
                        f.Afficher(cle, false);
                        for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
                        var image = f.CaptureRenderedFrame();
                        string nom = (sombre ? "sombre-" : "clair-") + cle + ".png";
                        if (image == null) { erreurs++; rapport.AppendLine("ERREUR : capture vide " + nom); continue; }
                        image.Save(Path.Combine(dossier, nom));
                        rapport.AppendLine("OK : " + nom);
                    }
                    catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : " + cle + " : " + ex); }
                }
            }
            // rubrique Nettoyer : ses trois onglets, et « Plus d'options » ouvert
            try
            {
                f.ChangerTheme(true);
                foreach (var onglet in new[] { "Disques", "Doublons" })
                {
                    f.SousPage("Nettoyer", onglet);
                    f.Afficher("Nettoyer", false);
                    Capturer(f, dossier, "sombre-Nettoyer-" + onglet + ".png", rapport, ref erreurs);
                }
                f.SousPage("Nettoyer", "Nettoyage");
                f.OptionOuverte("Nettoyage", true);
                f.Afficher("Nettoyer", false);
                Capturer(f, dossier, "sombre-Nettoyer-options.png", rapport, ref erreurs);
                f.OptionOuverte("Nettoyage", false);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : onglets de Nettoyer : " + ex); }

            // rubrique Accélérer : optimisation et jeux (les listes de démarrage et d'applications ne sont lues que sous Windows)
            try
            {
                foreach (var onglet in new[] { "Optimisation", "Jeux" })
                {
                    f.SousPage("Accelerer", onglet);
                    f.Afficher("Accelerer", false);
                    Capturer(f, dossier, "sombre-Accelerer-" + onglet + ".png", rapport, ref erreurs);
                }
                if (OperatingSystem.IsWindows())
                    foreach (var onglet in new[] { "Demarrage", "Applications" })
                    {
                        f.SousPage("Accelerer", onglet);
                        f.Afficher("Accelerer", false);
                        for (int k = 0; k < 40; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(100); }   // liste lue en arrière-plan
                        Capturer(f, dossier, "sombre-Accelerer-" + onglet + ".png", rapport, ref erreurs);
                    }
                f.SousPage("Accelerer", "Optimisation");
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : onglets d'Accélérer : " + ex); }

            // Outils : matériel, aide à distance (plein potentiel : sous Windows seulement, l'analyse lit le matériel)
            try
            {
                foreach (var onglet in OperatingSystem.IsWindows() ? new[] { "Materiel", "Potentiel", "Aide" } : new[] { "Materiel", "Aide" })
                {
                    f.SousPage("Outils", onglet);
                    f.Afficher("Outils", false);
                    if (onglet == "Potentiel") for (int k = 0; k < 60; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(100); }
                    Capturer(f, dossier, "sombre-Outils-" + onglet + ".png", rapport, ref erreurs);
                }
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : onglets d'Outils : " + ex); }

            // Historique avec des données d'exemple (rien n'est lu ni annulé)
            try
            {
                var j = DateTime.Today;
                PageHistorique5.Exemple = Tuple.Create(
                    new System.Collections.Generic.List<Tuple<DateTime, long>> { Tuple.Create(j.AddDays(-27), 1900000000L), Tuple.Create(j.AddDays(-20), 640000000L), Tuple.Create(j.AddDays(-13), 2800000000L), Tuple.Create(j.AddDays(-6), 410000000L), Tuple.Create(j, 1250000000L) },
                    new System.Collections.Generic.List<Tuple<DateTime, int>> { Tuple.Create(j.AddDays(-12), 58), Tuple.Create(j.AddDays(-9), 65), Tuple.Create(j.AddDays(-6), 72), Tuple.Create(j.AddDays(-3), 79), Tuple.Create(j, 86) },
                    new System.Collections.Generic.List<Modif>
                    {
                        new Modif { Date = j.AddDays(-13).ToString("yyyy-MM-dd") + " 10:02", Titre = "Téléchargements de plus de 90 jours rangés dans « _A_verifier » (14 fichiers)" },
                        new Modif { Date = j.AddDays(-6).ToString("yyyy-MM-dd") + " 21:40", Titre = "Programme au démarrage désactivé : Discord", Annule = true },
                        new Modif { Date = j.ToString("yyyy-MM-dd") + " 09:15", Titre = "Mode d'alimentation : Performances élevées (avant : Équilibré)" },
                    });
                f.ChangerTheme(true);
                f.Afficher("Historique", false);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-Historique.png", rapport, ref erreurs);
                PageHistorique5.Exemple = null;
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : historique : " + ex); }

            // Mises à jour : nouveautés intégrées (sans Internet), et l'onglet des pilotes
            try
            {
                f.SousPage("MisesAJour", "MisesAJour");
                f.Afficher("MisesAJour", false);
                Capturer(f, dossier, "sombre-MisesAJour.png", rapport, ref erreurs);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : mises à jour : " + ex); }

            // Premium (licence lue sur ce PC : sous Linux, aucune) et la fenêtre d'achat
            try
            {
                f.ChangerTheme(true);
                f.Afficher("Premium", false);
                Capturer(f, dossier, "sombre-Premium-page.png", rapport, ref erreurs);
                Achat5.Ouvrir(f);
                Capturer(f, dossier, "sombre-achat.png", rapport, ref erreurs);
                f.ViderCalques();
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : premium : " + ex); }

            // Réglages
            try
            {
                f.ChangerTheme(true);
                f.Afficher("Reglages", false);
                Capturer(f, dossier, "sombre-Reglages.png", rapport, ref erreurs);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : réglages : " + ex); }

            // Protéger, et le bouclier anti-arnaque avec deux logiciels d'exemple (rien n'est détecté ni fermé)
            try
            {
                f.ChangerTheme(true);
                f.Afficher("Proteger", false);
                Capturer(f, dossier, "sombre-Proteger.png", rapport, ref erreurs);
                f.SousPage("Proteger", "Sauvegarde");
                f.Afficher("Proteger", false);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(30); }
                Capturer(f, dossier, "sombre-Sauvegarde.png", rapport, ref erreurs);
                f.SousPage("Proteger", "Securite");
                Bouclier5.Exemple = new System.Collections.Generic.List<AccesDistant>
                {
                    new AccesDistant { Nom = "AnyDesk", Connexions = 1 },
                    new AccesDistant { Nom = "Assistance rapide", Microsoft = true },
                };
                Bouclier5.Ouvrir(f);
                Capturer(f, dossier, "sombre-bouclier.png", rapport, ref erreurs);
                f.ViderCalques();
                f.ChangerTheme(false);
                f.Afficher("Proteger", false);
                Bouclier5.Ouvrir(f, true);
                Capturer(f, dossier, "clair-bouclier-urgence.png", rapport, ref erreurs);
                f.ViderCalques();
                Bouclier5.Exemple = null;
                f.ChangerTheme(true);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : bouclier : " + ex); }

            // « Que veux-tu faire ? » avec une recherche tapée
            try
            {
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                f.OuvrirPalette();
                for (int k = 0; k < 3; k++) Dispatcher.UIThread.RunJobs();
                foreach (var tb in f.GetVisualDescendants().OfType<TextBox>()) tb.Text = "demar";
                Capturer(f, dossier, "sombre-palette.png", rapport, ref erreurs);
                f.ViderCalques();
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : palette : " + ex); }

            // action en cours, puis terminée (lignes d'exemple, rien n'est lancé), et question de confirmation
            try
            {
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                var lignes = new[]
                {
                    "=== Nettoyage PC - SIMULATION (rien n'est supprimé ni déplacé) ===", "",
                    "=== Fichiers temporaires ===", "  Temporaires de Windows : 412 Mo libérables", "  Temporaires de ton compte : 1,2 Go libérables",
                    "=== Caches des navigateurs ===", "  Edge : 318 Mo libérables", "  Chrome : 655 Mo libérables",
                };
                f.Moteur.Montrer("Nettoyage rapide (simulation)", lignes, 3, 7, false, true);
                f.MontrerPanneauAction();
                Capturer(f, dossier, "sombre-action-en-cours.png", rapport, ref erreurs);
                var fin = new System.Collections.Generic.List<string>(lignes)
                {
                    "=== Corbeille ===", "  Corbeille : 96 Mo libérables", "", "=== Résumé ===",
                    "TOTAL : 2,7 Go libérables", "Téléchargements de plus de 90 jours : 14 fichiers seraient rangés dans « _A_verifier »", "Aucun document personnel n'est touché.",
                };
                f.Moteur.Montrer("Nettoyage rapide (simulation)", fin, 7, 7, true, true);
                Capturer(f, dossier, "sombre-action-finie.png", rapport, ref erreurs);
                f.FermerPanneauAction();
                f.Demander("Lancer l'optimisation complète ?", "Un point de restauration est créé d'abord. Puis : nettoyage, nettoyage profond de Windows, optimisation des disques, libération de la RAM, bilans d'optimisation et de sécurité.\n\nCela peut durer longtemps : évite d'éteindre le PC et enregistre ton travail.", "Oui, lancer", "Annuler", r => { });
                Capturer(f, dossier, "sombre-question.png", rapport, ref erreurs);
                f.ChangerTheme(false);
                f.Demander("Lancer l'optimisation complète ?", "Un point de restauration est créé d'abord.\n\nCela peut durer longtemps : évite d'éteindre le PC et enregistre ton travail.", "Oui, lancer", "Annuler", r => { });
                Capturer(f, dossier, "clair-question.png", rapport, ref erreurs);
                f.ChangerTheme(true);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : action / question : " + ex); }

            // petite fenêtre (portable 1366 x 768 avec la barre des tâches)
            try
            {
                f.ChangerTheme(true); f.Width = 1100; f.Height = 680; f.Afficher("Accueil", false);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
                var image = f.CaptureRenderedFrame();
                if (image != null) { image.Save(Path.Combine(dossier, "petit-Accueil.png")); rapport.AppendLine("OK : petit-Accueil.png"); }
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : petite fenêtre : " + ex); }
            rapport.AppendLine("Fin : " + erreurs + " erreur(s).");
            File.WriteAllText(Path.Combine(dossier, "rapport.txt"), rapport.ToString());
            Console.WriteLine(rapport.ToString());
            return erreurs == 0 ? 0 : 1;
        }
    }

    /// <summary>Bilan calculé une seule fois puis servi tel quel (captures avec les vraies données).</summary>
    public sealed class SourceFigee : ISourceBilan
    {
        public readonly Bilan Bilan;
        public SourceFigee(ISourceBilan source) { Bilan = source.Calculer(); }
        public bool Immediat { get { return true; } }
        public Bilan Calculer() { return Bilan; }
        public Mesures Mesurer(bool avecTemperature) { return null; }
    }
}
