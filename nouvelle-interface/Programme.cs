// Velox 5 - démarrage. « --captures DOSSIER » : rendu sans écran de la fenêtre, enregistré en PNG
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
                    // nouveautés après une mise à jour, puis recherche d'une nouvelle version (« Installer maintenant »)
                    if (!Installation5.Apercu && FichierEssaiDemarrage == null) Dispatcher.UIThread.Post(() => MisesAJourAuto5.AuDemarrage(f, ProfilCree), DispatcherPriority.Background);
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
            JournalDemarrage.Noter("--- Démarrage de Velox " + Appli.Version + (Installation5.Apercu ? " (aperçu)" : "") + " : Windows " + Environment.OSVersion.Version + ", .NET " + Environment.Version + ", dossier " + Chemins.Ici);
            if (!Installation5.Apercu) System.Threading.Tasks.Task.Run(() => TempsDemarrage.Noter());   // temps « allumage → bureau » si Velox se lance au démarrage
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
            string couleur = "aurore";
            try
            {
                var reglages = Parametres.Lire();
                couleur = reglages.Accent ?? "aurore";
                Langue.Choisir(reglages.Langue);
                App.Pseudo = reglages.Pseudo ?? "";
                App.ModeSimple = reglages.ModeSimple;
                App.ProfilCree = reglages.ProfilCree || !string.IsNullOrEmpty(reglages.Pseudo);
                Habillage.Hud = reglages.Style != "classique";
                Habillage.Gamer = reglages.ProfilUsage == "gamer";
                Sons5.Actifs = reglages.SonsGamer;   // profil Gamer : néon, ligne RGB, QG GAMER
                Habillage.P = Palette.Pour(!(reglages.Theme == "Clair" || (reglages.Theme == "Auto" && WindowsEnClair())));
            }
            catch (Exception ex) { JournalDemarrage.Erreur("réglages (version 5)", ex); }
            try { Licence.Charger(); } catch (Exception ex) { JournalDemarrage.Erreur("licence (version 5)", ex); }
            // couleur de l'interface : celles autres que « Noyau » font partie de Premium (comme dans la version 4)
            try { Habillage.ChoisirCouleur(Licence.Premium ? couleur : "aurore"); } catch (Exception ex) { JournalDemarrage.Erreur("couleur (version 5)", ex); }
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
        /// <summary>(captures) question de l'optimisation complète, comme au vrai lancement.</summary>
        private static string QuestionComplete() { return CatalogueActions.Creer()["complet"].Confirmer; }

        private static void Capturer(FenetrePrincipale f, string dossier, string nom, System.Text.StringBuilder rapport, ref int erreurs)
        {
            Console.Error.WriteLine("capture : " + nom); Console.Error.Flush();   // pour situer un arrêt brutal du rendu
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
                JournalDemarrage.Noter("--- Auto-contrôle de Velox " + Appli.Version);
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
                // essais réels des outils de la 5.10 (lecture seule : rien n'est programmé ni modifié)
                try
                {
                    if (MisesAJour.Comparer("5.10", "5.9.10") <= 0 || MisesAJour.Comparer("5.10", "5.10.0.0") != 0 || MisesAJour.Comparer("5.9.10", "5.9.9") <= 0)
                    { erreurs++; rapport.AppendLine("ERREUR : comparaison des versions"); }
                    else rapport.AppendLine("Versions : 5.10 > 5.9.10 > 5.9.9, 5.10 = 5.10.0.0");
                    int joignables = 0;
                    foreach (var reg in new[] { "us-east-1", "eu-west-3", "eu-south-2", "ap-southeast-1" })
                    {
                        var r = Reseau.PingTcp(Reseau.HoteRegion(reg), reg, 3);
                        if (r.Moyenne >= 0) joignables++;
                        rapport.AppendLine("Ping " + reg + " : " + (r.Moyenne >= 0 ? Math.Round(r.Min) + " ms (gigue " + Math.Round(r.Gigue, 1) + ", perte " + r.Perte + " %) : " + Reseau.VerdictJeu(r.Min, r.Gigue, r.Perte) : "injoignable"));
                    }
                    if (joignables == 0) { erreurs++; rapport.AppendLine("ERREUR : aucune région de jeu joignable"); }
                    string etc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc");
                    double taille = DetectionJeux.TailleDossier(etc);
                    if (taille <= 0) { erreurs++; rapport.AppendLine("ERREUR : taille de dossier illisible (" + etc + ")"); }
                    else rapport.AppendLine("Taille de " + etc + " : " + Format.Taille(taille));
                    var jeux = DetectionJeux.Lister();
                    rapport.AppendLine("Jeux détectés : " + jeux.Count);
                    rapport.AppendLine("Extinction prévue : " + (Extinction.Prevue() == null ? "aucune" : "oui"));
                    rapport.AppendLine("Démarrages notés : " + TempsDemarrage.Lire().Count + " ; code de joueur : " + Succes.CodePartage(Succes.Exemple()));
                    // ---- 5.11
                    double u1 = UsageGpu.Lire(); System.Threading.Thread.Sleep(1700); double u2 = UsageGpu.Lire();
                    rapport.AppendLine("Utilisation GPU : " + u1 + " puis " + u2 + " (−1 = pas de compteur sur cette machine)");
                    var wifi = Wifi.Lire(); string vw; int nw; var cw = Wifi.Conseils(wifi, out vw, out nw);
                    rapport.AppendLine("Wi-Fi : disponible " + wifi.Disponible + ", connecté " + wifi.Connecte + ", erreur « " + wifi.Erreur + " » ; verdict : " + vw + " (" + cw.Count + " conseil(s))");
                    double bande; int canal = Wifi.CanalDepuisFrequence(5180000, out bande);
                    if (canal != 36 || bande != 5 || Wifi.CanalDepuisFrequence(2437000, out bande) != 6 || bande != 2.4 || Wifi.CanalDepuisFrequence(5955000, out bande) != 1 || bande != 6)
                    { erreurs++; rapport.AppendLine("ERREUR : canaux Wi-Fi mal calculés"); }
                    var progs = ProgrammesInstalles.Lister();
                    rapport.AppendLine("Programmes installés : " + progs.Count);
                    if (Desinstalleur.NomSimple("Mon Appli 2.4.1 (x64)") != "Mon Appli" || Desinstalleur.NomSimple("Outil (64-bit)") != "Outil") { erreurs++; rapport.AppendLine("ERREUR : nom simplifié : " + Desinstalleur.NomSimple("Mon Appli 2.4.1 (x64)") + " / " + Desinstalleur.NomSimple("Outil (64-bit)")); }
                    // restes : un faux programme « VeloxEssaiRestes » avec un dossier de réglages et un raccourci, créés puis retirés ici
                    string nomEssai = "VeloxEssaiRestes";
                    string dRoaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), nomEssai);
                    string lnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), nomEssai + ".lnk");
                    Directory.CreateDirectory(dRoaming); File.WriteAllText(Path.Combine(dRoaming, "reglages.txt"), new string('x', 5000));
                    Directory.CreateDirectory(Path.GetDirectoryName(lnk)); File.WriteAllText(lnk, "essai");
                    var restes = Desinstalleur.ChercherRestes(new ProgrammeInstalle { Nom = nomEssai + " 1.2.3", Dossier = "" }, progs);
                    rapport.AppendLine("Restes trouvés : " + string.Join(" | ", restes.Select(x => x.Genre + " " + x.Chemin + " (" + x.Taille + " o, protégé " + x.Protege + ")")));
                    if (restes.Count != 2 || restes.Any(x => x.Protege)) { erreurs++; rapport.AppendLine("ERREUR : restes attendus : 2, non protégés"); }
                    int retires = restes.Count(Desinstalleur.Retirer);
                    rapport.AppendLine("Restes mis à la corbeille : " + retires + " ; dossier encore là : " + Directory.Exists(dRoaming) + " ; raccourci encore là : " + File.Exists(lnk));
                    if (Directory.Exists(dRoaming) || File.Exists(lnk))
                    {
                        // sur une machine sans corbeille (serveur d'essai), Windows refuse : ce n'est pas une erreur de Velox, on nettoie
                        rapport.AppendLine("  (corbeille refusée sur cette machine : nettoyage direct de l'essai)");
                        try { Directory.Delete(dRoaming, true); } catch { } try { File.Delete(lnk); } catch { }
                    }
                    else
                    {
                        int okR, perdus; Historique.RestaurerCorbeille(new[] { dRoaming, lnk }, out okR, out perdus);
                        rapport.AppendLine("Restauration depuis la corbeille : " + okR + " remis, " + perdus + " introuvable(s) ; dossier revenu : " + Directory.Exists(dRoaming));
                        if (okR != 2 || !File.Exists(Path.Combine(dRoaming, "reglages.txt"))) { erreurs++; rapport.AppendLine("ERREUR : restauration des restes"); }
                        try { Directory.Delete(dRoaming, true); } catch { } try { File.Delete(lnk); } catch { }
                    }
                    // FPS en direct : lecture d'un fichier de mesures fabriqué (2 programmes, 100 puis 50 images)
                    string csv = Path.Combine(Path.GetTempPath(), "velox-essai-fps.csv");
                    var sb = new System.Text.StringBuilder("Application,ProcessID,SwapChainAddress,PresentRuntime,MsBetweenPresents\r\n");
                    for (int i = 0; i < 100; i++) sb.Append("jeu.exe,4242,0x1,DXGI,10.0\r\n");
                    for (int i = 0; i < 30; i++) sb.Append("dwm.exe,900,0x2,DXGI,16.6\r\n");
                    File.WriteAllText(csv, sb.ToString());
                    string app; double fps1 = Fps.LireDirect(csv, 0, out app);
                    sb.Clear(); for (int i = 0; i < 50; i++) sb.Append("jeu.exe,4242,0x1,DXGI,20.0\r\n");
                    File.AppendAllText(csv, sb.ToString());
                    string app2; double fps2 = Fps.LireDirect(csv, 4242, out app2);
                    rapport.AppendLine("FPS en direct (fichier d'essai) : " + Math.Round(fps1) + " (" + app + ") puis " + Math.Round(fps2) + " (" + app2 + ")");
                    if (Math.Abs(fps1 - 100) > 0.5 || Math.Abs(fps2 - 50) > 0.5 || app != "jeu.exe") { erreurs++; rapport.AppendLine("ERREUR : calcul des FPS en direct"); }
                    try { File.Delete(csv); } catch { }
                    string appVide; Fps.LireDirect(csv, 0, out appVide);   // fichier absent : la lecture repart de zéro
                    // ---- 5.12 : Boîte noire. D'abord la logique (partie simulée de 90 secondes à 100 imagesSim par seconde, deux saccades)
                    {
                        string fichierBoite = Path.Combine(Chemins.Local, "boite-noire.json");
                        string sauvegardeBoite = File.Exists(fichierBoite) ? File.ReadAllText(fichierBoite) : null;
                        var ancienneSim = BoiteNoire.PartieFinie;
                        SessionBoite recueSim = null;
                        BoiteNoire.PartieFinie = xb => recueSim = xb;
                        BoiteNoire.SeuilCpu = 92;
                        var debutSim = DateTime.Now.AddMinutes(-5);
                        for (int iSim = 0; iSim < 90; iSim++)
                        {
                            var imagesSim = Enumerable.Repeat(10.0, 100).ToList();
                            var etatSim = new SecondeBoite { Heure = debutSim.AddSeconds(iSim), Cpu = 40, Ram = 50, TempCpu = 60, TempGpu = 65, Disque = 5 };
                            if (iSim == 50) { imagesSim[40] = 200; etatSim.Programme = "Discord"; etatSim.ProgrammeCoeur = 95; }
                            if (iSim == 70) { imagesSim[10] = 150; etatSim.TempCpu = 95; }
                            if (iSim == 80) { imagesSim[10] = 25; }   // 25 ms : trop court pour une saccade
                            BoiteNoire.Seconde(etatSim, imagesSim, "jeu-essai.exe", 4242);
                        }
                        bool enCoursBoite = BoiteNoire.EnCours;
                        BoiteNoire.Terminer();
                        BoiteNoire.PartieFinie = ancienneSim;
                        var relueSim = BoiteNoire.Parties().FirstOrDefault();
                        var causesSim = recueSim != null ? BoiteNoire.Causes(recueSim) : new System.Collections.Generic.List<CauseBoite>();
                        rapport.AppendLine("Boîte noire (partie simulée) : en cours " + enCoursBoite + ", reçue " + (recueSim != null) + (recueSim != null ? ", saccades " + recueSim.Saccades.Count + " (" + string.Join(", ", recueSim.Saccades.Select(kb => kb.Cause + ":" + kb.Programme + ":" + kb.Ms)) + "), FPS moyen " + Math.Round(recueSim.FpsMoyen, 1) + ", FPS 1 % " + Math.Round(recueSim.Fps1, 1) + ", note " + recueSim.Note + ", causes " + causesSim.Count : "") + ", relue " + (relueSim != null ? relueSim.Jeu + " " + relueSim.Saccades.Count : "non"));
                        if (!enCoursBoite || recueSim == null || recueSim.Saccades.Count != 2 || recueSim.Saccades[0].Cause != "programme" || recueSim.Saccades[0].Programme != "Discord" || recueSim.Saccades[1].Cause != "chaleur"
                            || recueSim.FpsMoyen < 95 || recueSim.FpsMoyen > 100.5 || recueSim.Fps1 < 90 || recueSim.Fps1 > 100.5 || recueSim.Note < 35 || recueSim.Note > 45 || causesSim.Count != 2
                            || relueSim == null || relueSim.Jeu != "jeu-essai.exe" || relueSim.Saccades.Count != 2 || BoiteNoire.EnCours)
                        { erreurs++; rapport.AppendLine("ERREUR : Boîte noire (partie simulée)"); }
                        try { if (sauvegardeBoite != null) File.WriteAllText(fichierBoite, sauvegardeBoite); else File.Delete(fichierBoite); } catch { }
                        bool reglesBoite = BoiteNoire.EstSaccade(40, 8) && !BoiteNoire.EstSaccade(30, 5) && !BoiteNoire.EstSaccade(60, 20) && !BoiteNoire.EstSaccade(4000, 10)
                            && ActiviteProgrammes.NomSansNumero("chrome#3") == "chrome" && ActiviteProgrammes.Famille("MsMpEng") == "antivirus" && BoiteNoire5.EstUnJeu("Cyberpunk2077.exe") && !BoiteNoire5.EstUnJeu("chrome.exe") && !BoiteNoire5.EstUnJeu("dwm.exe");
                        rapport.AppendLine("Boîte noire (règles) : " + reglesBoite);
                        if (!reglesBoite) { erreurs++; rapport.AppendLine("ERREUR : Boîte noire (règles)"); }
                        // puis les vrais compteurs de Windows : activité des programmes et du disque
                        ActiviteProgrammes.Lire(); ActiviteProgrammes.Disque();
                        var chronoSim = System.Diagnostics.Stopwatch.StartNew();
                        while (chronoSim.ElapsedMilliseconds < 1200) { }   // ce programme travaille : il doit apparaître dans le relevé
                        var actifsSim = ActiviteProgrammes.Lire();
                        double occupationSim = ActiviteProgrammes.Disque();
                        string moiNom = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
                        var moiActif = actifsSim.FirstOrDefault(xb => xb.Pid == Environment.ProcessId);
                        rapport.AppendLine("Activité des programmes : " + actifsSim.Count + " actif(s), ce programme (" + moiNom + ") : " + (moiActif != null ? Math.Round(moiActif.Coeur) + " % d'un cœur" : "absent") + ", disque occupé à " + Math.Round(occupationSim) + " %, les plus actifs : " + string.Join(", ", actifsSim.OrderByDescending(xb => xb.Coeur).Take(4).Select(xb => xb.Nom + " " + Math.Round(xb.Coeur) + "%")));
                        if (moiActif == null || moiActif.Coeur < 30 || moiActif.Coeur > 250 || occupationSim < 0) { erreurs++; rapport.AppendLine("ERREUR : activité des programmes"); }
                        var coupableSim = Coupable.Observer(2, null, null, null);
                        int nivC; string progC;
                        string verdictC = Coupable.Verdict(coupableSim, out nivC, out progC);
                        rapport.AppendLine("Qui ralentit mon PC (2 s) : " + coupableSim.Programmes.Count + " programme(s), verdict « " + verdictC + " » " + progC);
                        if (verdictC == "") { erreurs++; rapport.AppendLine("ERREUR : Qui ralentit mon PC"); }
                    }
                    // ---- 5.13 : Autopilote. D'abord la logique, avec des réglages simulés : la priorité haute réduit les images lentes
                    // (elle doit être gardée), le mode d'alimentation ne change rien (il doit être annulé « aucun effet »)
                    {
                        string apFichier = Path.Combine(Chemins.Local, "autopilote.json");
                        string apSauve = File.Exists(apFichier) ? File.ReadAllText(apFichier) : null;
                        var apAppliquer = Autopilote.Appliquer; var apVerdict = Autopilote.Verdict;
                        int apDuree = Autopilote.DureeBloc, apEchauffement = Autopilote.Echauffement;
                        var apAppels = new System.Collections.Generic.List<string>(); var apVerdicts = new System.Collections.Generic.List<string>();
                        bool apPriorite = false;
                        try
                        {
                            Autopilote.Appliquer = (r, avec, pid, cible) => { apAppels.Add(r + (avec ? "+" : "-")); if (r == "priorite") apPriorite = avec; return true; };
                            Autopilote.Verdict = (j, e) => apVerdicts.Add(e.Reglage + ":" + e.Etat + ":" + e.Raison);
                            Autopilote.DureeBloc = 20; Autopilote.Echauffement = 5;
                            Autopilote.Recommencer("jeu-auto.exe");
                            var apHasard = new Random(7);
                            for (int apI = 0; apI < 3000 && apVerdicts.Count < 2; apI++)
                            {
                                var apImages = new System.Collections.Generic.List<double>();
                                for (int apK = 0; apK < 100; apK++) apImages.Add(10 + (apHasard.NextDouble() < (apPriorite ? 0.004 : 0.03) ? 6 : 0) + apHasard.NextDouble() * 0.5);
                                Autopilote.Seconde("jeu-auto.exe", 4343, apImages, false);
                            }
                            Autopilote.Terminer();
                            var apRelu = Autopilote.Charger().FirstOrDefault(x => x.Jeu == "jeu-auto.exe");
                            string apEtats = apRelu == null ? "non relu" : string.Join(", ", apRelu.Essais.Select(x => x.Reglage + "=" + x.Etat + "(" + x.Raison + ", " + x.Blocs.Count + " blocs, FPS min " + Math.Round(x.Gain1 * 100) + " %)"));
                            int apPlus = apAppels.Count(x => x.EndsWith("+")), apMoins = apAppels.Count(x => x.EndsWith("-"));
                            rapport.AppendLine("Autopilote (simulé) : verdicts " + string.Join(" / ", apVerdicts) + " ; relu : " + apEtats + " ; réglages appliqués " + apPlus + ", retirés " + apMoins + ", priorité encore appliquée : " + apPriorite);
                            if (apVerdicts.Count != 2 || apVerdicts[0] != "priorite:garde:" || apVerdicts[1] != "alimentation:annule:aucun" || apPriorite || apRelu == null
                                || apRelu.Essais.First(x => x.Reglage == "priorite").Etat != "garde" || apRelu.Essais.First(x => x.Reglage == "priorite").Gain1 < 0.2)
                            { erreurs++; rapport.AppendLine("ERREUR : Autopilote (simulé)"); }
                        }
                        finally
                        {
                            Autopilote.Appliquer = apAppliquer; Autopilote.Verdict = apVerdict; Autopilote.DureeBloc = apDuree; Autopilote.Echauffement = apEchauffement;
                            try { if (apSauve != null) File.WriteAllText(apFichier, apSauve); else File.Delete(apFichier); } catch { }
                        }
                        bool apStats = Autopilote.MannWhitney(new System.Collections.Generic.List<double> { 5, 6, 7, 8, 9, 10 }, new System.Collections.Generic.List<double> { 1, 2, 3, 4, 5, 6 }) < 0.05
                            && Autopilote.MannWhitney(new System.Collections.Generic.List<double> { 1, 2, 3, 4, 5, 6 }, new System.Collections.Generic.List<double> { 5, 6, 7, 8, 9, 10 }) > 0.9;
                        rapport.AppendLine("Autopilote (test statistique) : " + apStats);
                        if (!apStats) { erreurs++; rapport.AppendLine("ERREUR : Autopilote (test statistique)"); }
                        // puis pour de vrai : priorité d'un vrai programme changée, notée pour un arrêt brutal, puis rétablie
                        string apRetablir = Path.Combine(Chemins.Local, "autopilote-retablir.json");
                        var apPing = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Chemins.Systeme32, "ping.exe"), "-n 30 127.0.0.1") { UseShellExecute = false, CreateNoWindow = true });
                        try
                        {
                            bool apHaut = Autopilote.Priorite(apPing.Id, System.Diagnostics.ProcessPriorityClass.High);
                            apPing.Refresh(); var apLu = apPing.PriorityClass; bool apNote = File.Exists(apRetablir);
                            Autopilote.Retablir(apPing.Id, System.Diagnostics.ProcessPriorityClass.High);
                            apPing.Refresh(); var apLu2 = apPing.PriorityClass; bool apNote2 = File.Exists(apRetablir);
                            rapport.AppendLine("Autopilote (priorité réelle) : haute " + apHaut + " (" + apLu + "), notée " + apNote + ", rétablie " + apLu2 + ", note effacée " + !apNote2 + ", anti-triche vu : " + Autopilote.AntiTricheActif());
                            if (!apHaut || apLu != System.Diagnostics.ProcessPriorityClass.High || !apNote || apLu2 != System.Diagnostics.ProcessPriorityClass.Normal || apNote2) { erreurs++; rapport.AppendLine("ERREUR : Autopilote (priorité réelle)"); }
                        }
                        finally { try { apPing.Kill(); } catch { } apPing.Dispose(); }
                        // et le mode d'alimentation, si le PC n'est pas déjà en « Performances élevées »
                        string apPlan = Alimentation.PlanActif();
                        if (apPlan != "" && apPlan != Alimentation.Performances && Alimentation.Liste().Contains(Alimentation.Performances) && !File.Exists(Chemins.Fichier("modejeu.json")))
                        {
                            bool apOk = Autopilote.Appliquer("alimentation", true, 0, "");
                            string apPendant = Alimentation.PlanActif();
                            Autopilote.Appliquer("alimentation", false, 0, "");
                            string apApres = Alimentation.PlanActif();
                            rapport.AppendLine("Autopilote (alimentation réelle) : avant " + apPlan + ", pendant " + apPendant + " (" + apOk + "), après " + apApres);
                            if (!apOk || apPendant != Alimentation.Performances || apApres != apPlan) { erreurs++; rapport.AppendLine("ERREUR : Autopilote (alimentation réelle)"); }
                        }
                        else rapport.AppendLine("Autopilote (alimentation réelle) : non essayé (plan actif « " + apPlan + " », Performances élevées absent ou déjà actif)");
                    }
                    // FPS en direct pour de vrai : PresentMon officiel téléchargé, lancé comme le fait le processus administrateur,
                    // et son fichier de mesures lu PENDANT qu'il tourne ; puis arrêt dès que le témoin disparaît.
                    try
                    {
                        string emp = null, ver, src = "-";
                        if (Fps.OutilPresent) emp = Moteur.Empreinte(Fps.Exe); else src = Fps.Telecharger(out emp, out ver);
                        var tem = new System.Threading.Mutex(false, @"Local\VeloxFpsDirect");
                        var tache = System.Threading.Tasks.Task.Run(() => Fps.DirectEnAdmin(new[] { "fps-direct", src, emp }));
                        bool lisible = false; long tailleCsv = -1; double vu = -1; string appVue = ""; int lectures = 0;
                        for (int i = 0; i < 12 && !tache.IsCompleted; i++)
                        {
                            System.Threading.Thread.Sleep(1000);
                            try { using (var fs = new FileStream(Fps.CsvDirect, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)) { lisible = true; tailleCsv = fs.Length; } } catch { }
                            string a2; double v = Fps.LireDirect(Fps.CsvDirect, 0, out a2); lectures++;
                            if (v > 0) { vu = v; appVue = a2; }
                        }
                        bool finiTropTot = tache.IsCompleted;
                        string fin = finiTropTot ? (tache.IsFaulted ? "exception " + tache.Exception.InnerException : "code " + tache.Result) : "en marche";
                        tem.Dispose();
                        bool arrete = false; try { arrete = tache.Wait(25000); } catch { arrete = true; }
                        bool reste = File.Exists(Fps.CsvDirect);
                        rapport.AppendLine("FPS en direct (PresentMon réel) : fichier lisible pendant la mesure " + lisible + " (" + tailleCsv + " o), " + lectures + " lecture(s), FPS vus " + Math.Round(vu) + " (" + appVue + "), état avant l'arrêt : " + fin + ", arrêté après le témoin : " + arrete + ", fichier laissé : " + reste);
                        if (finiTropTot && tache.IsFaulted && tache.Exception.InnerException is System.Net.WebException) rapport.AppendLine("(réseau indisponible : essai non concluant)");
                        else if (!lisible || finiTropTot || !arrete || reste) { erreurs++; rapport.AppendLine("ERREUR : FPS en direct avec le vrai PresentMon"); }
                    }
                    catch (System.Net.WebException ex) { rapport.AppendLine("FPS en direct (PresentMon réel) : non essayé, téléchargement impossible : " + ex.Message); }
                }
                catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : essais 5.10 : " + ex); }
            }
            AppBuilder.Configure<App>().UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false }).SetupWithoutStarting();
            var f = new FenetrePrincipale(source) { Pseudo = "Damien" };
            f.Show();
            // style classique (l'apparence d'avant la 5.5), pour comparer
            try
            {
                Habillage.Hud = false; f.ChangerTheme(true); f.Afficher("Accueil", false);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); AvaloniaHeadlessPlatform.ForceRenderTimerTick(); }
                Capturer(f, dossier, "classique-Accueil.png", rapport, ref erreurs);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : style classique : " + ex); }
            finally { Habillage.Hud = true; }
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

            // programmes au démarrage d'exemple (conseils, impact, bouton « ceux qui peuvent attendre ») et résumé après nettoyage
            try
            {
                Func<string, string, bool, ElementDemarrage> el = (nom, cmd, actif) => new ElementDemarrage { Nom = nom, Commande = cmd, Lieu = "Registre (ton compte)", Actif = actif };
                PageDemarrage5.Exemple = new System.Collections.Generic.List<ElementDemarrage>
                {
                    el("Steam", @"""C:\Program Files (x86)\Steam\steam.exe"" -silent", true),
                    el("Discord", @"C:\Users\Camille\AppData\Local\Discord\Update.exe --processStart Discord.exe", true),
                    el("EpicGamesLauncher", @"""C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe"" -silent", true),
                    el("Spotify", @"C:\Users\Camille\AppData\Roaming\Spotify\Spotify.exe /minimized", true),
                    el("MicrosoftEdgeAutoLaunch_0A1B2C", @"""C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"" --no-startup-window", true),
                    el("OneDrive", @"""C:\Program Files\Microsoft OneDrive\OneDrive.exe"" /background", true),
                    el("SecurityHealth", @"%windir%\system32\SecurityHealthSystray.exe", true),
                    el("RtkAudUService", @"""C:\Windows\System32\DriverStore\FileRepository\realtekservice.inf_amd64\RtkAudUService64.exe"" -background", true),
                    el("Logi Options+", @"""C:\Program Files\LogiOptionsPlus\logioptionsplus_agent.exe"" /minimized", true),
                    el("Updater de Pilote Gratuit", @"""C:\Program Files\PiloteGratuit\updater.exe"" /tray", false),
                };
                f.SousPage("Accelerer", "Demarrage");
                f.Afficher("Accelerer", false);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-demarrage-conseils.png", rapport, ref erreurs);
                PageDemarrage5.Exemple = null;
                f.SousPage("Accelerer", "Optimisation");
                Gains5.Exemple = new GainsNettoyage { Dernier = DateTime.Now, DernierOctets = (long)(2.7 * 1073741824.0), Total = (long)(18.4 * 1073741824.0), Nombre = 9 };
                Gains5.Montrer(f, Gains5.Exemple, 72, 86, 4);
                Capturer(f, dossier, "sombre-gains.png", rapport, ref erreurs);
                f.ViderCalques(); Gains5.Exemple = null;
            }
            catch (Exception ex) { PageDemarrage5.Exemple = null; Gains5.Exemple = null; erreurs++; rapport.AppendLine("ERREUR : démarrage et gains : " + ex); }
            try
            {
                // 5.7 : mises à jour des applications, profils, signaler un problème
                MajApplis5.Exemple = new System.Collections.Generic.List<AppAMettreAJour>
                {
                    new AppAMettreAJour { Nom = "Discord", Id = "Discord.Discord", Version = "1.0.9210", Disponible = "1.0.9215" },
                    new AppAMettreAJour { Nom = "Mozilla Firefox (x64 fr)", Id = "Mozilla.Firefox.fr", Version = "144.0.2", Disponible = "145.0" },
                    new AppAMettreAJour { Nom = "7-Zip 25.00 (x64)", Id = "7zip.7zip", Version = "25.00", Disponible = "25.01" },
                    new AppAMettreAJour { Nom = "VLC media player", Id = "VideoLAN.VLC", Version = "3.0.21", Disponible = "3.0.22" },
                    new AppAMettreAJour { Nom = "Steam", Id = "Valve.Steam", Version = "2.10.91.91", Disponible = "—", Exclue = true },
                };
                MajApplis5.Ouvrir(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-majapplis.png", rapport, ref erreurs);
                f.ViderCalques(); MajApplis5.Exemple = null;
                Profils5.Ouvrir(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-profils.png", rapport, ref erreurs);
                f.ViderCalques();
                Signaler5.Ouvrir(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-signaler.png", rapport, ref erreurs);
                f.ViderCalques();
                // 5.6 : moniteur en direct, widget de bureau, visite guidée
                Moniteur5.Exemple = true; Moniteur5.Ouvrir(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-moniteur.png", rapport, ref erreurs);
                f.ViderCalques(); Moniteur5.Exemple = false;
                Widget5.Apercu(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-widget.png", rapport, ref erreurs);
                f.ViderCalques();
                f.Afficher("Accueil", false);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Visite5.Lancer(f);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-visite.png", rapport, ref erreurs);
                f.ViderCalques();
                f.SousPage("Nettoyer", "Nettoyage"); f.Afficher("Nettoyer", false);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-Nettoyer-hud.png", rapport, ref erreurs);
                Annonce5.Ouvrir(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-annonce.png", rapport, ref erreurs);
                f.ViderCalques();
                AnnonceVelox5.Ouvrir(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-annonce-velox.png", rapport, ref erreurs);
                f.ViderCalques();
                // profil Gamer : néon, ligne RGB, QG GAMER, succès
                Habillage.Gamer = true; QgGamer5.Exemple = Succes.Exemple();
                Habillage.ChoisirCouleur("aurore"); f.ChangerTheme(true); f.Afficher("Accueil", false);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-gamer-accueil.png", rapport, ref erreurs);
                Gamer5.Trophee(f, Langue.T("SUCCÈS DÉBLOQUÉ"), Langue.T("Dix gigas"), Langue.T("Libérer 10 Go au total"), true);
                for (int k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-gamer-trophee.png", rapport, ref erreurs);
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                Gamer5.Cinematique(f);
                for (int k = 0; k < 8; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-gamer-boost.png", rapport, ref erreurs);
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                SessionJeu5.CpuMoyen = 38; SessionJeu5.CpuMax = 71; SessionJeu5.TempMax = 74; SessionJeu5.GpuMax = 68; SessionJeu5.RamMax = 57; SessionJeu5.NbMesures = 800;
                SessionJeu5.Rapport(f, DateTime.Now.AddMinutes(-134));
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-gamer-session.png", rapport, ref erreurs);
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                f.Ouvrir("QG GAMER", new QgGamer5(f), 1120);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-gamer-qg.png", rapport, ref erreurs);
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                Progression5.OuvrirCarte(f);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-gamer-carte.png", rapport, ref erreurs);
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                Progression5.NiveauSuperieur(f, QgGamer5.Exemple, true);
                for (int k = 0; k < 8; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-gamer-niveau.png", rapport, ref erreurs);
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                for (int k = 0; k < 4; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Succes5.Ouvrir(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-gamer-succes.png", rapport, ref erreurs);
                f.ViderCalques();
                Habillage.ChoisirCouleur("aurore"); f.ChangerTheme(false); f.Afficher("Accueil", false);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "clair-gamer-accueil.png", rapport, ref erreurs);
                Habillage.Gamer = false; QgGamer5.Exemple = null;
                Habillage.ChoisirCouleur("aurore"); f.ChangerTheme(true);
                Etat5.Exemple = new System.Collections.Generic.List<EtatFonction>
                {
                    new EtatFonction { Id = "premium", Nom = "Premium", Groupe = EtatFonctions.Application, Actif = true, Detail = "Actif sur ce PC, pour les versions 4 et 5." },
                    new EtatFonction { Id = "entretien", Nom = "Entretien automatique", Groupe = EtatFonctions.Automatique, Actif = true, Detail = "Nettoyage rapide chaque dimanche, optimisation complète toutes les 4 semaines, surveillance des disques." },
                    new EtatFonction { Id = "verifmaj", Nom = "Recherche des nouvelles versions", Groupe = EtatFonctions.Automatique, Actif = true, Detail = "À l'ouverture, puis toutes les 6 heures si la fenêtre reste ouverte." },
                    new EtatFonction { Id = "gardien", Nom = "Gardien en temps réel", Groupe = EtatFonctions.Automatique, Actif = true, Premium = true, Detail = "Veille en continu : démarrage, disque, antivirus, mémoire, chauffe.", Probleme = Langue.T("Activé, mais il ne tourne pas en ce moment."), Reparation = "gardien-demarrer" },
                    new EtatFonction { Id = "sauvegardeauto", Nom = "Sauvegarde automatique", Groupe = EtatFonctions.Automatique, Actif = true, Premium = true, Detail = Langue.F("Dernière sauvegarde : {0}.", DateTime.Now.AddDays(-1).ToString("dd/MM/yyyy") + " 18:42") },
                    new EtatFonction { Id = "modejeuauto", Nom = "Mode jeu automatique", Groupe = EtatFonctions.Automatique, Actif = true, Premium = true, Detail = "Se lance tout seul avec tes jeux." },
                    new EtatFonction { Id = "majappsauto", Nom = "Mises à jour automatiques des applications", Groupe = EtatFonctions.Automatique, Actif = false, Premium = true, Detail = "Désactivées." },
                    new EtatFonction { Id = "bulletin", Nom = "Bulletin mensuel du PC", Groupe = EtatFonctions.Automatique, Actif = true, Premium = true, Detail = Langue.F("Dernier bulletin : {0}.", "01/11/2026") },
                    new EtatFonction { Id = "appareils", Nom = "Surveillance du réseau", Groupe = EtatFonctions.Automatique, Actif = true, Premium = true, Detail = "Alerte quand un appareil inconnu se connecte à ta box." },
                    new EtatFonction { Id = "donnees", Nom = "Dossier de Velox", Groupe = EtatFonctions.Application, Actif = true, Detail = @"C:\Users\Camille\Documents\NettoyagePC" },
                    new EtatFonction { Id = "reglages", Nom = "Fichier des réglages", Groupe = EtatFonctions.Application, Actif = true, Detail = "Lu sans problème." },
                    new EtatFonction { Id = "erreurs", Nom = "Erreurs récentes", Groupe = EtatFonctions.Application, Actif = true, Detail = "Aucune erreur ces 7 derniers jours." },
                };
                Etat5.Ouvrir(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-etat.png", rapport, ref erreurs);
                f.ViderCalques();
                Favoris5.Exemple = new System.Collections.Generic.List<string> { "rapide", "modejeuauto", "gardien", "sauvegardeauto", "secu", "majappsauto" };
                f.Afficher("Accueil", false);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-Accueil-etat.png", rapport, ref erreurs);
                Etat5.Exemple = null; Favoris5.Exemple = null;
                Favoris5.Choisir(f);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-favoris.png", rapport, ref erreurs);
                f.ViderCalques();
            }
            catch (Exception ex) { Etat5.Exemple = null; Favoris5.Exemple = null; erreurs++; rapport.AppendLine("ERREUR : favoris et état : " + ex); }

            // Outils : matériel, aide à distance (plein potentiel : sous Windows seulement, l'analyse lit le matériel)
            try
            {
                // « Mon PC » avec une fiche d'exemple (rien n'est lu)
                PageMonPc5.Exemple = new FicheMonPc
                {
                    Resume = "PC fixe · environ 2 an(s) · 32 Go de mémoire vive", AgeAnnees = 2,
                    Pieces =
                    {
                        new PieceMonPc { Categorie = "Windows", Nom = "Windows 11 Famille", Detail = "build 26200 · installé le 14/03/2024" },
                        new PieceMonPc { Categorie = "Processeur", Nom = "AMD Ryzen 7 7800X3D 8-Core Processor", Detail = "16 cœurs logiques" },
                        new PieceMonPc { Categorie = "Mémoire vive", Nom = "32 Go DDR5 · bridée à 4800 MT/s", Detail = "2 barrette(s)", Etat = 1 },
                        new PieceMonPc { Categorie = "Carte graphique", Nom = "AMD Radeon RX 7900 XTX", Detail = "carte dédiée · pilote 32.0.21025.1024" },
                        new PieceMonPc { Categorie = "Carte mère", Nom = "ASUS TUF GAMING B650-PLUS", Detail = "BIOS 3024 du 12/06/2025" },
                        new PieceMonPc { Categorie = "Disque", Nom = "Samsung SSD 990 PRO 2TB", Detail = "SSD · 1,82 To · usure 3 %" },
                        new PieceMonPc { Categorie = "Disque de Windows", Nom = "C:", Detail = "27,60 Go libres sur 1,82 To", Etat = 2 },
                    },
                    Conseils =
                    {
                        new ConseilMonPc { Priorite = 1, Titre = "Active le profil EXPO", Texte = "Tes barrettes tournent moins vite que prévu. Le guide pas à pas est dans Outils > Matériel > Mémoire vive et BIOS." },
                        new ConseilMonPc { Priorite = 1, Titre = "Libère de la place sur le disque de Windows", Texte = "Moins de 15 % libres ralentit Windows et ses mises à jour. Lance un nettoyage, ou ajoute un second disque pour tes jeux et vidéos." },
                    },
                };
                f.SousPage("Outils", "MonPc"); f.Afficher("Outils", false);
                for (int k = 0; k < 6; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(50); }
                Capturer(f, dossier, "sombre-Outils-MonPc.png", rapport, ref erreurs);
                PageMonPc5.Exemple = null;
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

            // outils Premium de la 5.2, avec des données d'exemple (rien n'est lu ni modifié)
            try
            {
                Gardien5.Exemple = new ReglagesGardien { Actif = true };
                var maintenant = new DateTime(2026, 10, 30, 21, 40, 0);
                ModeJeu5.Exemple = new System.Collections.Generic.List<SessionJeu>
                {
                    new SessionJeu { Jeu = "Cyberpunk 2077", Debut = maintenant.AddHours(-2.3), Fin = maintenant, Performances = true, OneDrive = true },
                    new SessionJeu { Jeu = "Counter-Strike 2", Debut = maintenant.AddDays(-1).AddHours(-1.1), Fin = maintenant.AddDays(-1), Performances = true, OneDrive = true },
                    new SessionJeu { Jeu = "Fortnite", Debut = maintenant.AddDays(-2).AddMinutes(-48), Fin = maintenant.AddDays(-2), Performances = true },
                };
                ModeJeu5.ExempleJeux = new System.Collections.Generic.List<Jeu>
                {
                    new Jeu { Nom = "Call of Duty", Plateforme = "Steam", Exe = @"C:\Jeux\cod.exe" },
                    new Jeu { Nom = "Forza Horizon 6", Plateforme = "Xbox", Exe = @"C:\Jeux\ForzaHorizon6.exe" },
                    new Jeu { Nom = "Overwatch", Plateforme = "Battle.net", Exe = @"C:\Jeux\Overwatch.exe" },
                };
                ModeJeu5.Ouvrir(f);
                Capturer(f, dossier, "sombre-modejeu.png", rapport, ref erreurs);
                f.ViderCalques(); ModeJeu5.Exemple = null; ModeJeu5.ExempleJeux = null;
                TempsDemarrage5.Exemple = Enumerable.Range(0, 30).Select(i => Tuple.Create(new DateTime(2026, 10, 8).AddDays(i), 36.0 - i * 0.45 + (i % 4 == 1 ? 4 : i % 5 == 3 ? -2 : 0))).ToList();
                TempsDemarrage5.Ouvrir(f);
                Capturer(f, dossier, "sombre-temps-demarrage.png", rapport, ref erreurs);
                f.ViderCalques(); TempsDemarrage5.Exemple = null;
                // ---- 5.10 : ping des serveurs de jeu, mes jeux, minuteur d'extinction
                double[] ms = { 9, 14, 17, 22, 27, 21, 34, 92, 158, 214, 248, 196 };
                PingJeu5.Exemple = Reseau.RegionsJeu.Select((r, i) => new ResultatPing { Cible = Reseau.HoteRegion(r[2]), Nom = r[0], Min = ms[i], Moyenne = ms[i] + 2, Max = ms[i] + 6, Gigue = 1 + i % 4, Perte = 0 }).ToList();
                PingJeu5.Ouvrir(f);
                Capturer(f, dossier, "sombre-ping-jeu.png", rapport, ref erreurs);
                f.ViderCalques(); PingJeu5.Exemple = null;
                MesJeux5.Exemple = new System.Collections.Generic.List<Jeu>
                {
                    new Jeu { Nom = "Call of Duty", Plateforme = "Steam", Dossier = @"D:\Jeux\Call of Duty", Lancer = "steam://rungameid/1938090", Taille = 231.4 * 1073741824.0 },
                    new Jeu { Nom = "Forza Horizon 6", Plateforme = "Xbox", Dossier = @"C:\XboxGames\Forza Horizon 6", Lancer = "x", Taille = 148.2 * 1073741824.0 },
                    new Jeu { Nom = "Euro Truck Simulator 2", Plateforme = "Steam", Dossier = @"D:\Jeux\Euro Truck Simulator 2", Lancer = "x", Taille = 24.7 * 1073741824.0 },
                    new Jeu { Nom = "Overwatch", Plateforme = "Battle.net", Dossier = @"D:\Jeux\Overwatch", Lancer = "x", Taille = 61.3 * 1073741824.0 },
                    new Jeu { Nom = "Call of Duty: Black Ops II", Plateforme = "Steam", Dossier = @"D:\Jeux\Black Ops II", Lancer = "x", Taille = 16.9 * 1073741824.0 },
                };
                MesJeux5.Ouvrir(f);
                Capturer(f, dossier, "sombre-mes-jeux.png", rapport, ref erreurs);
                f.ViderCalques(); MesJeux5.Exemple = null;
                Extinction5.Exemple = true;
                Extinction5.Ouvrir(f);
                Capturer(f, dossier, "sombre-extinction.png", rapport, ref erreurs);
                f.ViderCalques(); Extinction5.Exemple = false;
                // ---- 5.11 : test du Wi-Fi, restes d'un programme désinstallé
                Wifi5.Exemple = new EtatWifi
                {
                    Disponible = true, Connecte = true, Nom = "Livebox-A4F2", Carte = "Intel Wi-Fi 6E AX211", Norme = "Wi-Fi 6 (802.11ax)", Signal = 62, Canal = 6, Bande = 2.4, DebitMbps = 144, VoisinsLus = true, Genants = 5, BandeRapideDisponible = true,
                    Voisins = Enumerable.Range(0, 9).Select(i => new ReseauVoisin { Nom = "Box-" + i, Signal = 40 + i * 3, Canal = i < 5 ? 6 : 11, Bande = 2.4 }).ToList(),
                };
                Wifi5.Ouvrir(f);
                Capturer(f, dossier, "sombre-wifi.png", rapport, ref erreurs);
                f.ViderCalques(); Wifi5.Exemple = null;
                Restes5.OuvrirExemple(f);
                Capturer(f, dossier, "sombre-restes.png", rapport, ref erreurs);
                f.ViderCalques(); Restes5.Exemple = false;
                // ---- 5.12 : Boîte noire du jeu, « Qui ralentit mon PC ? »
                BoiteNoire5.Exemple = BoiteNoire.Exemple();
                BoiteNoire5.Ouvrir(f);
                Capturer(f, dossier, "sombre-boitenoire.png", rapport, ref erreurs);
                f.ViderCalques(); BoiteNoire5.Exemple = null;
                Coupable5.Exemple = new ResultatCoupable
                {
                    Cpu = 71, Ram = 64, Disque = 22, TempCpu = 68, Coeurs = 16,
                    Programmes = new System.Collections.Generic.List<ProgrammeActif>
                    {
                        new ProgrammeActif { Nom = "MsMpEng", Coeur = 520, Disque = 41.0 * 1048576 }, new ProgrammeActif { Nom = "chrome", Coeur = 96, Disque = 0 },
                        new ProgrammeActif { Nom = "Discord", Coeur = 38, Disque = 0 }, new ProgrammeActif { Nom = "steamwebhelper", Coeur = 21, Disque = 3.0 * 1048576 },
                    },
                };
                Coupable5.Ouvrir(f);
                Capturer(f, dossier, "sombre-coupable.png", rapport, ref erreurs);
                f.ViderCalques(); Coupable5.Exemple = null;
                Autopilote5.Exemple = Autopilote.Exemple();
                Autopilote5.Ouvrir(f);
                Capturer(f, dossier, "sombre-autopilote.png", rapport, ref erreurs);
                Autopilote5.Exemple = null;
                f.ViderCalques(); Coupable5.Exemple = null;

                MajApps5.Exemple = new System.Collections.Generic.List<AppAMettreAJour>
                {
                    new AppAMettreAJour { Nom = "Google Chrome", Id = "Google.Chrome", Version = "141.0.7390.66", Disponible = "142.0.7444.60" },
                    new AppAMettreAJour { Nom = "Discord", Id = "Discord.Discord", Version = "1.0.9208", Disponible = "1.0.9265" },
                    new AppAMettreAJour { Nom = "VLC media player", Id = "VideoLAN.VLC", Version = "3.0.20", Disponible = "3.0.21" },
                    new AppAMettreAJour { Nom = "7-Zip 24.08 (x64)", Id = "7zip.7zip", Version = "24.08", Disponible = "25.01" },
                    new AppAMettreAJour { Nom = "OBS Studio", Id = "OBSProject.OBSStudio", Version = "31.1.2", Disponible = "32.0.1", Exclue = true },
                };
                MajApps5.Ouvrir(f);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(30); }
                Capturer(f, dossier, "sombre-majapps.png", rapport, ref erreurs);
                f.ViderCalques(); MajApps5.Exemple = null;

                Func<string, int, int, long, int, PhotoSimilaire> ph = (nom, w, h, o, jours) => new PhotoSimilaire { Chemin = @"C:\Users\Camille\Pictures\Vacances 2026\" + nom, Largeur = w, Hauteur = h, Taille = o, Modif = maintenant.AddDays(-jours) };
                var g1 = new GroupePhotos { Photos = { ph("IMG_2041.jpg", 4032, 3024, 3900000, 90), ph("IMG_2041 (1).jpg", 4032, 3024, 3900000, 60), ph("IMG_2041-whatsapp.jpg", 1600, 1200, 310000, 59) }, Identiques = false };
                var g2 = new GroupePhotos { Photos = { ph("IMG_2052.jpg", 4032, 3024, 4100000, 88), ph("IMG_2053.jpg", 4032, 3024, 4050000, 88), ph("IMG_2054.jpg", 4032, 3024, 4080000, 88) } };
                var g3 = new GroupePhotos { Photos = { ph("plage.png", 1920, 1440, 5200000, 80), ph("plage - copie.png", 1920, 1440, 5200000, 30) }, Identiques = true };
                foreach (var g in new[] { g1, g2, g3 }) PhotosSimilaires.Proposer(g);
                Photos5.Exemple = new System.Collections.Generic.List<GroupePhotos> { g1, g2, g3 };
                Photos5.Ouvrir(f);
                Capturer(f, dossier, "sombre-photos.png", rapport, ref erreurs);
                f.ViderCalques(); Photos5.Exemple = null;

                Bulletin5.Exemple = new System.Collections.Generic.List<string> { "octobre 2026", "septembre 2026", "août 2026" };
                Bulletin5.Ouvrir(f);
                Capturer(f, dossier, "sombre-bulletin.png", rapport, ref erreurs);
                f.ViderCalques(); Bulletin5.Exemple = null;

                ReseauPremium5.ExempleAppareils = new System.Collections.Generic.List<AppareilReseau>
                {
                    new AppareilReseau { Ip = "192.168.1.254", Mac = "A4:3E:51:0C:22:10", Nom = "livebox", Box = true, Connu = true, Premier = maintenant.AddDays(-40) },
                    new AppareilReseau { Ip = "192.168.1.20", Mac = "D8:BB:C1:5A:90:E2", Nom = "PC-CAMILLE", CePc = true, Connu = true, Premier = maintenant.AddDays(-40) },
                    new AppareilReseau { Ip = "192.168.1.31", Mac = "F2:6C:3A:91:7B:04", Surnom = "Téléphone de Camille", Connu = true, Premier = maintenant.AddDays(-38) },
                    new AppareilReseau { Ip = "192.168.1.42", Mac = "7C:2E:BD:11:4F:9A", Nom = "Chromecast", Connu = true, Premier = maintenant.AddDays(-20) },
                    new AppareilReseau { Ip = "192.168.1.57", Mac = "3A:91:0F:C4:68:B2", Connu = false, Premier = maintenant.AddMinutes(-12) },
                };
                ReseauPremium5.ExempleApps = new System.Collections.Generic.List<AppConnectee>
                {
                    new AppConnectee { Processus = "chrome", Connexions = 23, Adresses = { "142.250.75.238", "151.101.1.140", "104.18.32.7" } },
                    new AppConnectee { Processus = "Discord", Connexions = 6, Adresses = { "162.159.135.234", "162.159.130.233" } },
                    new AppConnectee { Processus = "steam", Connexions = 4, Adresses = { "23.33.67.12", "155.133.252.38" } },
                    new AppConnectee { Processus = "OneDrive", Connexions = 2, Adresses = { "13.107.42.12" } },
                };
                ReseauPremium5.Ouvrir(f);
                for (int k = 0; k < 10; k++) { Dispatcher.UIThread.RunJobs(); System.Threading.Thread.Sleep(30); }
                Capturer(f, dossier, "sombre-reseau-premium.png", rapport, ref erreurs);
                f.ViderCalques(); ReseauPremium5.ExempleAppareils = null; ReseauPremium5.ExempleApps = null;
                Gardien5.Exemple = null;
            }
            catch (Exception ex) { ModeJeu5.Exemple = null; MajApps5.Exemple = null; Photos5.Exemple = null; Bulletin5.Exemple = null; ReseauPremium5.ExempleAppareils = null; ReseauPremium5.ExempleApps = null; Gardien5.Exemple = null; erreurs++; rapport.AppendLine("ERREUR : outils Premium 5.2 : " + ex); }

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
                f.ModeSimple = false; f.Simulation = false; f.ChangerTheme(true);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : mode simple : " + ex); f.ModeSimple = false; f.Simulation = false; }

            // Réglages
            try
            {
                f.ChangerTheme(true);
                f.Afficher("Reglages", false);
                Capturer(f, dossier, "sombre-Reglages.png", rapport, ref erreurs);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : réglages : " + ex); }

            // Couleurs Premium de l'interface (appliquées sans être enregistrées)
            try
            {
                Habillage.ChoisirCouleur("rubis");
                f.ChangerTheme(true); f.Afficher("Accueil", false);
                Capturer(f, dossier, "sombre-couleur-rubis.png", rapport, ref erreurs);
                Habillage.ChoisirCouleur("emeraude");
                f.ChangerTheme(false); f.Afficher("Reglages", false);
                Capturer(f, dossier, "clair-couleur-emeraude.png", rapport, ref erreurs);
                Habillage.ChoisirCouleur("amethyste");
                f.ChangerTheme(true); f.Afficher("Premium", false);
                Capturer(f, dossier, "sombre-couleur-amethyste.png", rapport, ref erreurs);
            }
            catch (Exception ex) { erreurs++; rapport.AppendLine("ERREUR : couleurs : " + ex); }
            finally { Habillage.ChoisirCouleur("aurore"); f.ChangerTheme(true); }

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
                    "=== Velox - SIMULATION (rien n'est supprimé ni déplacé) ===", "",
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
                f.DemanderConfirmation(QuestionComplete(), r => { });
                Capturer(f, dossier, "sombre-question.png", rapport, ref erreurs);
                f.ChangerTheme(false);
                f.DemanderConfirmation(QuestionComplete(), r => { });
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
                    g.DemanderConfirmation(QuestionComplete(), r => { });
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
