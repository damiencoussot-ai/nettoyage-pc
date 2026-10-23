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

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime bureau)
                bureau.MainWindow = new FenetrePrincipale(new BilanExemple());
            base.OnFrameworkInitializationCompleted();
        }
    }

    public static class Programme
    {
        [STAThread]
        public static int Main(string[] args)
        {
            int i = Array.IndexOf(args, "--captures");
            if (i >= 0 && i + 1 < args.Length) return Captures.Faire(args[i + 1]);
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return 0;
        }

        public static AppBuilder BuildAvaloniaApp()
        {
            return AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
        }
    }

    /// <summary>Captures de la fenêtre dans les deux thèmes, sur l'accueil et une autre rubrique.</summary>
    public static class Captures
    {
        public static int Faire(string dossier)
        {
            Directory.CreateDirectory(dossier);
            Habillage.Animations = false;   // états finaux, sans attendre les animations
            AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            int erreurs = 0;
            var rapport = new System.Text.StringBuilder();
            var f = new FenetrePrincipale(new BilanExemple()) { Pseudo = "Damien" };
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
}
