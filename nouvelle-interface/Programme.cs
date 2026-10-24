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
        public static bool ModeSimple, ProfilCree = true;

        /// <summary>(essais) --essai-demarrage FICHIER : mesure l'ouverture de la fenêtre et l'arrivée du bilan, puis ferme.</summary>
        public static string FichierEssaiDemarrage;
        /// <summary>Demande reçue au lancement (--page, --ouvrir, --action, --assistant), du gardien ou d'un raccourci.</summary>
        internal static CommandeExterne CommandeInitiale = new CommandeExterne();

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime bureau)
            {
                var f = new FenetrePrincipale(Source) { Pseudo = Pseudo };
                if (ModeSimple) f.AppliquerModeSimple(true);
                // premier lancement : « Faisons connaissance » (pseudo, mode simple ou complet)
                if (!ProfilCree && FichierEssaiDemarrage == null) f.Opened += (s, e) => Bienvenue5.Ouvrir(f);
                // demandes du gardien ou d'un raccourci : au lancement, puis transmises par un deuxième lancement
                f.Opened += (s, e) =>
                {
                    if (!CommandeInitiale.Vide) f.TraiterCommande(CommandeInitiale, true);
                    if (!Installation5.Apercu && FichierEssaiDemarrage == null) CommandeExterne.Ecouter(c => Dispatcher.UIThread.Post(() => f.TraiterCommande(c, false)));
                };
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
        private static System.Threading.Mutex verrou;

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
            // gardien (icône près de l'horloge) : dans l'aperçu, c'est celui de la version 4 installée qui tourne
            if (args.Length > 0 && args[0] == "--gardien")
            {
                if (Installation5.Apercu) { JournalDemarrage.Noter("Aperçu de la version 5 : le gardien reste celui de la version 4."); return 0; }
                return GardienNatif.Executer();
            }
            // contrôle après installation (lancé par l'installateur) : fenêtre et pages construites sans rien afficher
            if (args.Length > 0 && args[0] == "--autocontrole") return Captures.AutoControle();
            App.CommandeInitiale = CommandeExterne.Lire(args);

            int ie = Array.IndexOf(args, "--essai-demarrage");
            if (ie >= 0 && ie + 1 < args.Length) App.FichierEssaiDemarrage = Path.GetFullPath(args[ie + 1]);
            JournalDemarrage.Noter("--- Démarrage de Nettoyage PC " + Appli.Version + (Installation5.Apercu ? " (aperçu)" : "") + " : Windows " + Environment.OSVersion.Version + ", .NET " + Environment.Version + ", dossier " + Chemins.Ici);
            // toujours avec les droits normaux : lancée en administrateur, l'application se relance par l'Explorateur
            if (Appli.EstAdmin && Natif.EstWindows && App.FichierEssaiDemarrage == null && Demarrage.RelancerSansAdmin()) return 0;
            // une seule fenêtre (programme installé ou portable) : un deuxième lancement ramène la fenêtre ouverte et lui
            // transmet sa demande (page, outil...). L'aperçu, lui, ne partage rien avec la version 4 installée.
            if (!Installation5.Apercu && App.FichierEssaiDemarrage == null)
            {
                bool nouveau;
                verrou = new System.Threading.Mutex(true, @"Local\NettoyagePC-Interface", out nouveau);
                if (!nouveau)
                {
                    // (relance demandée par l'application elle-même, après un changement de langue : on attend la fermeture)
                    int attente = Array.IndexOf(args, "--relance") >= 0 ? 15000 : 1500;
                    try { if (!verrou.WaitOne(attente)) { App.CommandeInitiale.Transmettre(); Demarrage.RamenerAutreFenetre(); return 0; } }
                    catch (System.Threading.AbandonedMutexException) { }
                }
            }
            AppDomain.CurrentDomain.UnhandledException += (s, e) => JournalDemarrage.Erreur("fatale (version 5)", e.ExceptionObject as Exception);
            try
            {
                var reglages = Parametres.Lire();
                Langue.Choisir(reglages.Langue);
                App.Pseudo = reglages.Pseudo ?? "";
                App.ModeSimple = reglages.ModeSimple;
                App.ProfilCree = reglages.ProfilCree || !string.IsNullOrEmpty(reglages.Pseudo);
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

        /// <summary>
        /// Contrôle rapide après une installation (lancé par l'installateur) : crée la fenêtre et toutes les rubriques et
        /// onglets, dans les deux thèmes, en français et en anglais, sans rien afficher ni modifier (bilan d'exemple).
        /// Code 0 si tout va bien ; sinon l'installateur remet la version précédente.
        /// </summary>
        public static int AutoControle()
        {
            var garde = new System.Threading.Thread(() => { System.Threading.Thread.Sleep(120000); JournalDemarrage.Noter("Auto-contrôle (version 5) : délai dépassé."); Environment.Exit(3); }) { IsBackground = true };
            garde.Start();
            try
            {
                JournalDemarrage.Noter("--- Auto-contrôle de Nettoyage PC " + Appli.Version);
                Habillage.Animations = false;
                AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
                try { Licence.Charger(); } catch { }
                foreach (var code in new[] { "fr", "en" })
                {
                    Langue.Choisir(code);
                    var f = new FenetrePrincipale(new BilanExemple()) { Pseudo = "" };
                    f.Show();
                    foreach (var sombre in new[] { true, false })
                    {
                        f.ChangerTheme(sombre);
                        foreach (var r in f.Rubriques.ToList()) { f.Afficher(r.Cle, false); Pomper(); }
                        // (sans les onglets qui lancent une recherche longue : Windows Update, analyse du matériel)
                        foreach (var o in FenetrePrincipale.Onglets5.Where(x => x[1] != "Pilotes" && x[1] != "Potentiel"))
                        {
                            f.SousPage(o[0], o[1]); f.Afficher(o[0], false); Pomper();
                        }
                        if (f.CaptureRenderedFrame() == null) throw new InvalidOperationException("rendu de la fenêtre impossible");
                    }
                    f.Close();
                }
                Langue.Choisir("fr");
                JournalDemarrage.Noter("Auto-contrôle (version 5) réussi.");
                return 0;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("auto-contrôle (version 5)", ex); return 1; }
        }

        private static void Pomper() { for (int k = 0; k < 3; k++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); } }

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

            // mémoire vive bridée (exemple : rien n'est lu sur le PC) et son guide du BIOS
            try
            {
                var m = new InfosMateriel { CarteMere = "ASUSTeK COMPUTER INC. TUF GAMING B650-PLUS", MarqueCarteMere = "ASUS", Bios = "3024 du 12/03/2026", Processeur = "AMD Ryzen 7 7800X3D 8-Core Processor", Prevue = 6000, Reelle = 4800 };
                foreach (var emplacement in new[] { "DIMM_A2", "DIMM_B2" })
                    m.Barrettes.Add(new Barrette { Capacite = 16L * 1073741824, TypeSmbios = 34, Vitesse = 6000, VitesseConfiguree = 4800, Fabricant = "Kingston", Reference = "KF560C36-16", Emplacement = emplacement });
                Memoire5.Exemple = m;
                f.ChangerTheme(true); f.SousPage("Outils", "Materiel"); f.Afficher("Outils", false);
                Memoire5.Ouvrir(f);
                Capturer(f, dossier, "sombre-memoire.png", rapport, ref erreurs);
                f.ViderCalques();
                Memoire5.Exemple = null;
            }
            catch (Exception ex) { Memoire5.Exemple = null; erreurs++; rapport.AppendLine("ERREUR : mémoire vive : " + ex); }

            // Preuve en FPS avec deux mesures d'exemple (rien n'est mesuré)
            try
            {
                var j = DateTime.Now;
                Fps5.Exemple = new System.Collections.Generic.List<MesureFps>
                {
                    new MesureFps { Date = j.ToString("yyyy-MM-dd HH:mm"), Jeu = "Cyberpunk2077", Libelle = "après nettoyage", Secondes = 60, Images = 8142, Moyenne = 135.7, Bas1 = 98.4, Bas01 = 81.2, Saccades = 6 },
                    new MesureFps { Date = j.AddMinutes(-25).ToString("yyyy-MM-dd HH:mm"), Jeu = "Cyberpunk2077", Libelle = "avant", Secondes = 60, Images = 7410, Moyenne = 123.5, Bas1 = 71.9, Bas01 = 52.6, Saccades = 41 },
                };
                f.ChangerTheme(true); f.SousPage("Accelerer", "Jeux"); f.Afficher("Accelerer", false);
                Fps5.Ouvrir(f);
                Capturer(f, dossier, "sombre-fps.png", rapport, ref erreurs);
                f.ViderCalques(); f.SousPage("Accelerer", "Optimisation");
                Fps5.Exemple = null;
            }
            catch (Exception ex) { Fps5.Exemple = null; erreurs++; rapport.AppendLine("ERREUR : FPS : " + ex); }

            // outils Premium avec des données d'exemple (rien n'est lu, mesuré ni modifié)
            try
            {
                var j = DateTime.Today;
                var hasard = new Random(7);
                f.ChangerTheme(true); f.Afficher("Premium", false);

                var bat = new InfosBatterie { Presente = true, Nom = "5B10W51867", Fabricant = "Celxpert", Chimie = "LiP", Origine = 57000, Actuelle = 46100, Cycles = 312, Charge = 78, SurSecteur = true, AutonomieActuelle = new TimeSpan(5, 12, 0), AutonomieOrigine = new TimeSpan(7, 40, 0) };
                for (int m = 18; m >= 0; m--) bat.Historique.Add(Tuple.Create(j.AddMonths(-m), 57000L - (18 - m) * 600L - hasard.Next(0, 300), 57000L));
                Batterie5.Exemple = bat;
                Batterie5.Ouvrir(f);
                Capturer(f, dossier, "sombre-batterie.png", rapport, ref erreurs);
                f.ViderCalques(); Batterie5.Exemple = null;

                var ssd = new IndicateurSante { Cle = "ssd", Nom = "Usure du SSD (Samsung 990 PRO 2 To)", Valeur = "18 % de la durée de vie prévue consommée", Etat = "ok", Explication = "Un SSD s'use un peu à chaque écriture ; 100 % correspond à la durée prévue par le fabricant (il fonctionne souvent bien au-delà).", Prevision = "À ce rythme : 100 % dans environ 9 ans.", Unite = "%", Pente = 0.025, PrevisionJusqua = j.AddYears(2), Seuil = 100 };
                for (int m = 12; m >= 0; m--) ssd.Points.Add(Tuple.Create(j.AddMonths(-m), 9.0 + (12 - m) * 0.75));
                var arrets = new IndicateurSante { Cle = "arrets", Nom = "Arrêts brutaux", Valeur = "3 ces 30 derniers jours", Etat = "attention", Explication = "Le PC s'est éteint sans passer par « Arrêter » : coupure de courant, bouton maintenu, ou plantage.", Conseil = "Si ce n'était pas toi, l'analyse des plantages en donne la cause probable.", Bouton = "Analyser les plantages", Cible = "outil:plantages", Barres = true, Unite = "par semaine" };
                for (int s2 = 12; s2 >= 0; s2--) arrets.Points.Add(Tuple.Create(j.AddDays(-7 * s2), s2 < 4 ? (double)(s2 % 2) + 0.0 : 0.0));
                var gpu = new IndicateurSante { Cle = "gpu", Nom = "Température de la carte graphique", Valeur = "74 °C au maximum (moyenne des 7 derniers jours)", Etat = "ok", Explication = "Une carte graphique supporte jusqu'à 90 °C environ en jeu.", Unite = "°C" };
                for (int d = 20; d >= 0; d--) gpu.Points.Add(Tuple.Create(j.AddDays(-d), 66.0 + hasard.Next(0, 9)));
                var disque = new IndicateurSante { Cle = "erreurs", Nom = "Erreurs du disque", Valeur = "Aucune erreur ces 90 derniers jours", Etat = "ok" };
                Sante5.Exemple = new System.Collections.Generic.List<IndicateurSante> { ssd, arrets, gpu, disque };
                Sante5.Ouvrir(f);
                Capturer(f, dossier, "sombre-sante.png", rapport, ref erreurs);
                f.ViderCalques(); Sante5.Exemple = null;

                var jours = new System.Collections.Generic.List<JourEnergie>();
                for (int d = 29; d >= 0; d--)
                {
                    var je = new JourEnergie { Jour = j.AddDays(-d), Minutes = 300 + hasard.Next(0, 360), MinutesInactif = 60 + hasard.Next(0, 120) };
                    je.WhInactif = je.MinutesInactif * 1.4;
                    je.Apps[Energie.NomSysteme] = je.Minutes * 1.1;
                    je.Apps["Cyberpunk2077"] = d % 3 == 0 ? 0 : 380 + hasard.Next(0, 420);
                    je.Apps["chrome"] = 90 + hasard.Next(0, 80);
                    je.Apps["Discord"] = 25 + hasard.Next(0, 20);
                    je.Apps["Code"] = 30 + hasard.Next(0, 40);
                    je.Wh = je.Apps.Values.Sum();
                    jours.Add(je);
                }
                Energie5.Exemple = jours;
                Energie5.Ouvrir(f);
                Capturer(f, dossier, "sombre-energie.png", rapport, ref erreurs);
                f.ViderCalques(); Energie5.Exemple = null;

                Gardien5.Exemple = new ReglagesGardien { Actif = true, SauvegardeAuto = true };
                Gardien5.Ouvrir(f);
                Capturer(f, dossier, "sombre-gardien.png", rapport, ref erreurs);
                f.ViderCalques(); Gardien5.Exemple = null;

                var enquete = new ResultatEnquete { NbInstantanes = 24, ChronoDemarrages = true };
                enquete.Changements.Add(new Changement { Date = j.AddDays(-9), Type = "Démarrage", Titre = "« Updater de Pilote Gratuit » ajouté au démarrage", Pourquoi = "Ajouté juste avant que le démarrage ralentisse (+14 s)", Cible = "page:Demarrage", Suspicion = 2 });
                enquete.Changements.Add(new Changement { Date = j.AddDays(-9), Type = "Application", Titre = "Installation : Updater de Pilote Gratuit 3.2", Pourquoi = "Logiciel de « mise à jour des pilotes » : souvent inutile et publicitaire", Cible = "page:Applications", Suspicion = 2 });
                enquete.Changements.Add(new Changement { Date = j.AddDays(-6), Type = "Mise à jour Windows", Titre = "Mise à jour cumulative (KB5066835)", Pourquoi = "", Cible = "uri:ms-settings:windowsupdate-history", Suspicion = 0 });
                enquete.Changements.Add(new Changement { Date = j.AddDays(-3), Type = "Pilote", Titre = "Pilote graphique AMD 25.10.1", Pourquoi = "Change les performances en jeu (dans un sens ou dans l'autre)", Cible = "page:Pilotes", Suspicion = 1 });
                var dem = new Metrique { Cle = "demarrage", Nom = "Durée du démarrage", Unite = "s" };
                for (int d = 27; d >= 0; d--) dem.Points.Add(Tuple.Create(j.AddDays(-d), (d > 9 ? 21.0 : 35.0) + hasard.Next(0, 4)));
                enquete.Metriques.Add(dem);
                Enqueteur5.Exemple = enquete;
                Enqueteur5.Ouvrir(f);
                Capturer(f, dossier, "sombre-enqueteur.png", rapport, ref erreurs);
                f.ViderCalques(); Enqueteur5.Exemple = null;

                Benchmark5.Exemple = new System.Collections.Generic.List<ResultatBenchmark>
                {
                    new ResultatBenchmark { Date = j.AddDays(-1).ToString("dd/MM/yyyy") + " 21:04", Note = "avant EXPO", RamMts = 4800, Cpu1 = 268, CpuN = 3120, Memoire = 61.8, DisqueLecture = 6950, DisqueEcriture = 6120, DisqueAleatoire = 92000 },
                    new ResultatBenchmark { Date = j.ToString("dd/MM/yyyy") + " 09:31", Note = "après EXPO", RamMts = 6000, Cpu1 = 274, CpuN = 3262, Memoire = 75.4, DisqueLecture = 6990, DisqueEcriture = 6080, DisqueAleatoire = 93500 },
                };
                Benchmark5.Ouvrir(f);
                Capturer(f, dossier, "sombre-benchmark.png", rapport, ref erreurs);
                f.ViderCalques(); Benchmark5.Exemple = null;

                Assistant5.Essai = true;
                Assistant5.Ouvrir(f);
                for (int k = 0; k < 20; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-assistant.png", rapport, ref erreurs);
                f.ViderCalques(); Assistant5.Essai = false;
            }
            catch (Exception ex) { Batterie5.Exemple = null; Sante5.Exemple = null; Energie5.Exemple = null; Gardien5.Exemple = null; Enqueteur5.Exemple = null; Benchmark5.Exemple = null; Assistant5.Essai = false; erreurs++; rapport.AppendLine("ERREUR : outils Premium : " + ex); }

            // analyses des plantages et du démarrage avec des résultats d'exemple (rien n'est lancé)
            try
            {
                f.ChangerTheme(true); f.Afficher("Premium", false);
                Plantages5.Exemple = Json.Lire(@"{""Ecrans"": [{""Categorie"": ""pilote"", ""Code"": ""0x00000116"", ""Nom"": ""VIDEO_TDR_FAILURE"", ""Explication"": ""Le pilote de la carte graphique a cessé de répondre et Windows n'a pas pu le relancer."", ""Date"": ""2026-10-02T21:14:00"", ""Dump"": ""amdkmdag.sys""}], ""Arrets"": [{""Type"": ""bouton"", ""Date"": ""2026-10-05T22:40:00""}, {""Type"": ""coupure"", ""Date"": ""2026-09-20T18:02:00""}], ""Materiel"": [{""Nom"": ""Erreur matérielle corrigée (processeur)"", ""Grave"": false, ""Fois"": 3, ""Dernier"": ""2026-10-12T09:00:00"", ""Detail"": ""WHEA-Logger 19, bus PCI Express""}], ""Disques"": [], ""Affichage"": 2, ""DernierAffichage"": ""2026-10-02T21:13:00"", ""Applications"": [{""Nom"": ""Cyberpunk2077.exe"", ""Plantages"": 3, ""Figee"": 1, ""Module"": ""amdxx64.dll"", ""Explication"": ""pilote de la carte graphique"", ""Categorie"": ""graphique"", ""Dernier"": ""2026-10-14T20:11:00""}, {""Nom"": ""Discord.exe"", ""Plantages"": 0, ""Figee"": 2, ""Module"": """", ""Explication"": """", ""Categorie"": """", ""Dernier"": ""2026-10-09T12:00:00""}], ""Semaines"": [[0, 0, 0], [0, 1, 0], [0, 0, 0], [1, 0, 0], [0, 0, 0], [0, 2, 1], [0, 0, 0], [0, 0, 0], [1, 1, 2], [2, 0, 0], [0, 1, 0], [0, 0, 1], [0, 1, 0]], ""Indice"": 6.4, ""Conseils"": [""Mets à jour le pilote de la carte graphique : la plupart des plantages viennent de lui (Mises à jour > Windows et pilotes).""]}") as System.Collections.Generic.Dictionary<string, object>;
                Plantages5.Ouvrir(f);
                Capturer(f, dossier, "sombre-plantages.png", rapport, ref erreurs);
                f.ViderCalques(); Plantages5.Exemple = null;
                Demarrage5.Exemple = Json.Lire(@"{""Admin"": true, ""DemarrageRapide"": false, ""Demarrages"": [{""Date"": ""23/10"", ""Total"": 48200, ""Principal"": 31000, ""Apres"": 17200}, {""Date"": ""22/10"", ""Total"": 51800, ""Principal"": 32500, ""Apres"": 19300}, {""Date"": ""21/10"", ""Total"": 47100, ""Principal"": 30800, ""Apres"": 16300}, {""Date"": ""20/10"", ""Total"": 29900, ""Principal"": 21000, ""Apres"": 8900}, {""Date"": ""19/10"", ""Total"": 28800, ""Principal"": 20500, ""Apres"": 8300}, {""Date"": ""18/10"", ""Total"": 31200, ""Principal"": 21900, ""Apres"": 9300}, {""Date"": ""17/10"", ""Total"": 30100, ""Principal"": 21200, ""Apres"": 8900}, {""Date"": ""16/10"", ""Total"": 28500, ""Principal"": 20100, ""Apres"": 8400}], ""Ralentissements"": [{""Nom"": ""Updater de Pilote Gratuit"", ""Type"": ""Application"", ""Moyenne"": 9400, ""Fois"": 6, ""Dernier"": ""23/10/2026""}, {""Nom"": ""Pilote Realtek Audio"", ""Type"": ""Pilote"", ""Moyenne"": 3100, ""Fois"": 11, ""Dernier"": ""22/10/2026""}, {""Nom"": ""Service Steam Client"", ""Type"": ""Service"", ""Moyenne"": 1200, ""Fois"": 4, ""Dernier"": ""20/10/2026""}]}") as System.Collections.Generic.Dictionary<string, object>;
                Demarrage5.Ouvrir(f);
                Capturer(f, dossier, "sombre-demarrage.png", rapport, ref erreurs);
                f.ViderCalques(); Demarrage5.Exemple = null;
            }
            catch (Exception ex) { Plantages5.Exemple = null; Demarrage5.Exemple = null; erreurs++; rapport.AppendLine("ERREUR : analyses : " + ex); }

            // Réseau avec une connexion et des mesures d'exemple (rien n'est mesuré ni modifié)
            try
            {
                Reseau5.Exemple = true;
                f.ChangerTheme(true); f.SousPage("Outils", "Reseau"); f.Afficher("Outils", false);
                Capturer(f, dossier, "sombre-Outils-Reseau.png", rapport, ref erreurs);
                f.SousPage("Outils", "Materiel");
                Reseau5.Exemple = false;
            }
            catch (Exception ex) { Reseau5.Exemple = false; erreurs++; rapport.AppendLine("ERREUR : réseau : " + ex); }

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

            // Windows et pilotes, avec une carte graphique et des mises à jour d'exemple (rien n'est demandé à Windows Update)
            try
            {
                var j = DateTime.Today;
                Pilotes5.Exemple = Tuple.Create(
                    new System.Collections.Generic.List<CarteGraphique> { new CarteGraphique { Nom = "AMD Radeon RX 7900 XTX", PnpId = "PCI\\VEN_1002&DEV_744C", VersionPilote = "32.0.21001.9024", DatePilote = j.AddMonths(-7), Memoire = 24L * 1073741824 } },
                    new System.Collections.Generic.List<MajWindows>
                    {
                        new MajWindows { Id = "8a7c3f12-4b1e-4d6a-9f0e-2c5b7d9e1a31", Titre = "Mise à jour de la veille de sécurité pour Microsoft Defender Antivirus - KB2267602", Taille = 182000000 },
                        new MajWindows { Id = "1f2e3d4c-5b6a-4978-8a1b-2c3d4e5f6a7b", Titre = "Mise à jour cumulative pour Windows 11 Version 25H2 pour les systèmes x64 (KB5066835)", Taille = 812000000, Redemarrage = true },
                        new MajWindows { Id = "9e8d7c6b-5a4f-4321-9abc-def012345678", Titre = "Advanced Micro Devices, Inc. - Display - 31.0.24033.1003", Pilote = true, Classe = "Display", Fournisseur = "Advanced Micro Devices, Inc.", DatePilote = j.AddMonths(-11), Facultative = true, Taille = 640000000 },
                        new MajWindows { Id = "0a1b2c3d-4e5f-4a6b-8c7d-9e0f1a2b3c4d", Titre = "Realtek Semiconductor Corp. - Extension - 10.0.26100.21", Pilote = true, Facultative = true, Taille = 1200000 },
                        new MajWindows { Id = "5f4e3d2c-1b0a-4f9e-8d7c-6b5a4f3e2d1c", Titre = "Mise à jour cumulative préliminaire pour Windows 11 Version 25H2 (KB5067036)", Facultative = true, Taille = 790000000 },
                    });
                f.ChangerTheme(true); f.SousPage("MisesAJour", "Pilotes"); f.Afficher("MisesAJour", false);
                Capturer(f, dossier, "sombre-Pilotes.png", rapport, ref erreurs);
                f.SousPage("MisesAJour", "MisesAJour");
                Pilotes5.Exemple = null;
            }
            catch (Exception ex) { Pilotes5.Exemple = null; erreurs++; rapport.AppendLine("ERREUR : pilotes : " + ex); }

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

            // carte de l'espace disque, sur le dossier du programme (rien n'est modifié)
            try
            {
                f.ChangerTheme(true); f.SousPage("Nettoyer", "Disques"); f.Afficher("Nettoyer", false);
                var carte = CarteEspace5.Ouvrir(f);
                for (int k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
                string d = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".."));
                carte.AnalyserPourEssai(Directory.Exists(d) ? d : AppContext.BaseDirectory);
                Capturer(f, dossier, "sombre-carte-espace.png", rapport, ref erreurs);
                f.ViderCalques(); f.SousPage("Nettoyer", "Nettoyage");
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : carte de l'espace : " + ex); }

            // réglages de confidentialité (état lu sur ce PC ; rien n'est appliqué)
            try
            {
                f.ChangerTheme(true); f.Afficher("Proteger", false);
                ReglagesWindows5.Confidentialite(f);
                Capturer(f, dossier, "sombre-confidentialite.png", rapport, ref erreurs);
                f.ViderCalques();
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : confidentialité : " + ex); }

            // premier lancement : « Faisons connaissance »
            try
            {
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                Bienvenue5.Ouvrir(f);
                Capturer(f, dossier, "sombre-bienvenue.png", rapport, ref erreurs);
                f.ViderCalques();
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : bienvenue : " + ex); }

            // mode simple (trois gros boutons), sombre et clair
            try
            {
                foreach (var sombre in new[] { true, false })
                {
                    f.ChangerTheme(sombre);
                    f.ModeSimple = true; f.Simulation = false; f.ChangerTheme(sombre);
                    Capturer(f, dossier, (sombre ? "sombre" : "clair") + "-simple.png", rapport, ref erreurs);
                }
                f.ModeSimple = false; f.Simulation = true; f.ChangerTheme(true);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : mode simple : " + ex); f.ModeSimple = false; f.Simulation = true; }

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

            // anglais et espagnol : une nouvelle fenêtre dans chaque langue (les textes sont posés à la construction)
            foreach (var code in new[] { "en", "es" })
            {
                try
                {
                    Langue.Choisir(code);
                    var g = new FenetrePrincipale(source) { Pseudo = "Damien" };
                    g.Show();
                    g.ChangerTheme(true);
                    foreach (var cle in new[] { "Accueil", "Nettoyer", "Proteger", "Premium" })
                    {
                        g.Afficher(cle, false);
                        Capturer(g, dossier, code + "-" + cle + ".png", rapport, ref erreurs);
                    }
                    // tous les onglets et rubriques sont affichés une fois (sans capture) pour la liste des phrases non traduites
                    foreach (var o in FenetrePrincipale.Onglets5)
                    {
                        if (o[1] == "Reseau" || o[1] == "Potentiel") continue;
                        g.SousPage(o[0], o[1]); g.Afficher(o[0], false);
                        for (int k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
                    }
                    foreach (var cle in new[] { "Historique", "Reglages" })
                    {
                        g.Afficher(cle, false);
                        for (int k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
                    }
                    Capturer(g, dossier, code + "-Reglages.png", rapport, ref erreurs);
                    Reseau5.Exemple = true;
                    g.SousPage("Outils", "Reseau"); g.Afficher("Outils", false);
                    Capturer(g, dossier, code + "-Outils-Reseau.png", rapport, ref erreurs);
                    Reseau5.Exemple = false;
                    g.Demander("Lancer l'optimisation complète ?", "Un point de restauration est créé d'abord.\n\nCela peut durer longtemps : évite d'éteindre le PC et enregistre ton travail.", "Oui, lancer", "Annuler", r => { });
                    Capturer(g, dossier, code + "-question.png", rapport, ref erreurs);
                    g.ViderCalques();
                    g.Close();
                }
                catch (Exception ex) { Reseau5.Exemple = false; erreurs++; rapport.AppendLine("ERREUR : captures en " + code + " : " + ex); }
            }
            Langue.EcrireManquantes();
            Langue.Choisir("fr");
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
