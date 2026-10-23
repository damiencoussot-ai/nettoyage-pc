// Nettoyage PC 4.0 - point d'entrée
// Un seul programme : l'interface (sans option), le moteur (--moteur ...), les commandes administrateur (--commande ...).
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal static class Programme
    {
        public static bool ModeSansEchec;
        /// <summary>Temps écoulé depuis le lancement (mesure de la rapidité d'ouverture).</summary>
        public static readonly System.Diagnostics.Stopwatch Chrono = System.Diagnostics.Stopwatch.StartNew();
        public static string DossierAutotest { get { return Appli.DossierAutotest; } set { Appli.DossierAutotest = value; } }
        /// <summary>Page à afficher au lancement (--page Cle) et fenêtre à ouvrir (--ouvrir gardien), demandées par le gardien.</summary>
        public static CommandeExterne CommandeInitiale = new CommandeExterne();
        private static Mutex verrou;

        [STAThread]
        private static int Main(string[] args)
        {
            JournalDemarrage.Preparer();
            Demarrage.AppliquerDossiersUtilisateur(args);

            // moteur, commandes administrateur, état en JSON... (sans interface, partagés avec la version 5)
            int? code = Demarrage.SansInterface(args);
            if (code.HasValue) return code.Value;
            if (args.Length > 0 && args[0] == "--gardien") return GardienProcessus.Executer();
            if (args.Length > 0 && args[0] == "--autocontrole") return AutoControle();
            if (args.Length >= 3 && args[0] == "--generer-licence") return GenererLicence(args);
            // (éditeur) fenêtre « Créer une clé offerte » : option seule, ou fichier de la clé privée glissé sur l'application
            if (args.Length >= 1 && args.Length <= 2 && args[0] == "--generer-licence") return FenetreGenerateurCles.Lancer(args.Length > 1 ? args[1] : null);
            if (args.Length == 1 && FenetreGenerateurCles.EstClePrivee(args[0])) return FenetreGenerateurCles.Lancer(args[0]);
            CommandeInitiale = CommandeExterne.Lire(args);
            int i = Array.IndexOf(args, "--autotest");
            if (i >= 0) DossierAutotest = i + 1 < args.Length ? args[i + 1] : Path.Combine(Path.GetTempPath(), "np-autotest");
            return Interface();
        }

        private static int Interface()
        {
            JournalDemarrage.Noter("--- Démarrage de Nettoyage PC " + Appli.Version + " : Windows " + Environment.OSVersion.Version + ", .NET " + Environment.Version + ", " + (Environment.Is64BitProcess ? 64 : 32) + " bits, dossier " + Chemins.Ici);

            // Toujours avec les droits normaux : lancée en administrateur, l'application se relance par l'Explorateur (une fois)
            if (Appli.EstAdmin && Natif.EstWindows && DossierAutotest == null && Demarrage.RelancerSansAdmin()) return 0;

            // Une seule fenêtre : un deuxième lancement ramène la fenêtre déjà ouverte
            bool nouveau;
            verrou = new Mutex(true, @"Local\NettoyagePC-Interface", out nouveau);
            if (!nouveau && DossierAutotest == null)
            {
                // (relance demandée par l'application elle-même, après un changement de langue : on attend que l'ancienne fenêtre se ferme)
                int attente = Environment.GetCommandLineArgs().Contains("--relance") ? 15000 : 1500;
                try { if (!verrou.WaitOne(attente)) { CommandeInitiale.Transmettre(); Demarrage.RamenerAutreFenetre(); return 0; } }
                catch (AbandonedMutexException) { }
            }

            // Mode sans échec : si le démarrage précédent s'est interrompu, les fonctions avancées sont mises en pause une fois
            string etat = Path.Combine(Chemins.Local, "etat-demarrage.txt");
            try
            {
                if (File.Exists(etat) && File.ReadAllText(etat).StartsWith("demarrage") && (DateTime.Now - File.GetLastWriteTime(etat)).TotalDays < 2) ModeSansEchec = true;
                File.WriteAllText(etat, "demarrage");
            }
            catch { }
            Natif.SansEchec = ModeSansEchec;
            if (ModeSansEchec) JournalDemarrage.Noter("Le démarrage précédent ne s'est pas terminé : mode sans échec (fonctions avancées en pause).");

            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => ErreurInterface(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) => { JournalDemarrage.Erreur("fatale", e.ExceptionObject as Exception); EcrireEtat(etat, "erreur"); };
            Langue.Choisir(Parametres.Lire().Langue);
            Licence.Charger();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            FenetrePrincipale f;
            try { f = new FenetrePrincipale(); }
            catch (Exception ex)
            {
                JournalDemarrage.Erreur("création de la fenêtre", ex);
                EcrireEtat(etat, "erreur");
                MessageBox.Show("Nettoyage PC n'a pas pu démarrer :\n\n" + ex.Message + "\n\nDétails : " + JournalDemarrage.Fichier, "Nettoyage PC", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
            f.Shown += (s, e) =>
            {
                JournalDemarrage.Noter("Fenêtre principale affichée.");
                var t = new System.Windows.Forms.Timer { Interval = 20000 };
                t.Tick += (s2, e2) => { t.Stop(); t.Dispose(); EcrireEtat(etat, "ok"); JournalDemarrage.Noter("Démarrage réussi."); };
                t.Start();
            };
            Application.Run(f);
            Langue.EcrireManquantes();
            EcrireEtat(etat, "ferme");
            JournalDemarrage.Noter("Fin normale.");
            return 0;
        }

        /// <summary>
        /// Contrôle rapide après une installation (lancé par l'installateur) : crée la fenêtre et toutes les pages,
        /// les dessine dans les deux thèmes et en anglais, sans rien afficher ni modifier. Code 0 si tout va bien.
        /// En cas d'échec, l'installateur remet la version précédente.
        /// </summary>
        private static int AutoControle()
        {
            DossierAutotest = Path.Combine(Path.GetTempPath(), "np-autocontrole");
            var garde = new Thread(() => { Thread.Sleep(90000); JournalDemarrage.Noter("Auto-contrôle : délai dépassé."); Environment.Exit(3); }) { IsBackground = true };
            garde.Start();
            try
            {
                JournalDemarrage.Noter("--- Auto-contrôle de Nettoyage PC " + Appli.Version);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
                Langue.Choisir("fr");
                Licence.Charger();
                using (var f = new FenetrePrincipale())
                {
                    var h = f.Handle;
                    f.Size = new System.Drawing.Size(1200, 800);
                    foreach (var code in new[] { "fr", "en" })
                    {
                        Langue.Choisir(code);
                        foreach (var sombre in new[] { true, false })
                        {
                            f.AppliquerTheme(sombre);
                            foreach (var p in f.Pages.Values)
                            {
                                p.Chargee = true;   // pas de chargement des listes : seulement la construction et le dessin
                                f.AfficherPage(p.Cle);
                                try { using (var b = new System.Drawing.Bitmap(1000, 700)) f.DrawToBitmap(b, new System.Drawing.Rectangle(0, 0, 1000, 700)); }
                                catch (Exception ex) when (!(ex.StackTrace ?? "").Contains("NettoyagePC.")) { /* dessin hors écran impossible ici : ce n'est pas une erreur de l'application */ }
                            }
                        }
                    }
                    Langue.Choisir("fr");
                    var o = OptionsMoteur.Lire(new[] { "--simulation", "--profond" });
                    if (!o.Simulation) throw new InvalidOperationException("options du moteur");
                }
                JournalDemarrage.Noter("Auto-contrôle réussi.");
                return 0;
            }
            catch (Exception ex)
            {
                JournalDemarrage.Erreur("auto-contrôle", ex);
                return 1;
            }
        }

        /// <summary>(Éditeur) NettoyagePC.exe --generer-licence cle-privee.xml "Nom" [AAAA-MM-JJ] : écrit la clé offerte dans un fichier texte.</summary>
        private static int GenererLicence(string[] args)
        {
            try
            {
                string cle = Licence.GenererCleOfferte(args[1], args[2], args.Length > 3 ? args[3] : null);
                string nom = System.Text.RegularExpressions.Regex.Replace(args[2], @"[^\w\-]+", "_");
                string fichier = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(args[1])), "cle-premium-" + nom + ".txt");
                File.WriteAllText(fichier, "Clé Nettoyage PC Premium pour " + args[2] + " :\r\n\r\n" + cle + "\r\n\r\nÀ coller dans Nettoyage PC > Premium > « J'ai une clé ».\r\n");
                Console.WriteLine(cle);
                MessageBox.Show("Clé créée pour « " + args[2] + " » :\n" + fichier, "Nettoyage PC - clé offerte", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Impossible de créer la clé : " + ex.Message, "Nettoyage PC", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        private static void EcrireEtat(string fichier, string etat) { try { File.WriteAllText(fichier, etat); } catch { } }

        private static DateTime derniereErreur = DateTime.MinValue;
        private static void ErreurInterface(Exception ex)
        {
            JournalDemarrage.Erreur("interface", ex);
            if ((DateTime.Now - derniereErreur).TotalSeconds < 10) return;
            derniereErreur = DateTime.Now;
            try { var f = Application.OpenForms.OfType<FenetrePrincipale>().FirstOrDefault(); if (f != null) f.Statut("Une petite erreur s'est produite (l'application continue). Détails : Réglages > Journal de démarrage.", true); }
            catch { }
        }
    }
}
