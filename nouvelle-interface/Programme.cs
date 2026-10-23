// Nettoyage PC 5 - démarrage. « --captures DOSSIER » : rendu sans écran de la fenêtre, enregistré en PNG
// (aperçus et essais automatiques ; rien n'est lu ni modifié sur le PC).
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;

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

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime bureau)
                bureau.MainWindow = new FenetrePrincipale(Source) { Pseudo = Pseudo };
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

            JournalDemarrage.Noter("--- Démarrage de Nettoyage PC 5 (aperçu) : Windows " + Environment.OSVersion.Version + ", .NET " + Environment.Version + ", dossier " + Chemins.Ici);
            // toujours avec les droits normaux : lancée en administrateur, l'application se relance par l'Explorateur
            if (Appli.EstAdmin && Natif.EstWindows && Demarrage.RelancerSansAdmin()) return 0;
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
        private static bool WindowsEnClair()
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
