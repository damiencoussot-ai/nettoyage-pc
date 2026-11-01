// Nettoyage PC - moteur : le même programme, lancé avec --moteur (par l'interface ou l'entretien automatique)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class OptionsMoteur
    {
        public bool Simulation, Profond, Disques, DisquesSeuls, Verifier, LibererRam, RamSeule, TesterRam, SansRapport, RapportSeul;
        public bool Planifier, Deplanifier, PlanifierApps, DeplanifierApps, TacheApps, PointRestauration, Optimiser, MajApps, Securite, Antivirus, Reparer, Seul, Compat;
        public bool Notifier, Surveillance, Sauvegarde, ModeJeu, FinModeJeu, MajWindows, Progression, AnciensPilotes, SanteDisques, AnalyseDemarrage, Kit, Wifi;
        public bool Plantages, Leger, Incrementale;
        public int JoursTelechargements = 90, JoursTemp = 2, SeuilMo = 10;
        public string ModeAlim = "", Destination = "", Dossiers = "Documents,Images,Bureau", IdsMaj = "", Tube = "";
        public List<string> PilotesChoisis = new List<string>();

        public static OptionsMoteur Lire(string[] args)
        {
            var o = new OptionsMoteur();
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                Func<string> suivant = () => i + 1 < args.Length ? args[++i] : "";
                switch (a)
                {
                    case "--simulation": o.Simulation = true; break;
                    case "--profond": o.Profond = true; break;
                    case "--disques": o.Disques = true; break;
                    case "--disques-seuls": o.DisquesSeuls = true; break;
                    case "--verifier": o.Verifier = true; break;
                    case "--liberer-ram": o.LibererRam = true; break;
                    case "--ram-seule": o.RamSeule = true; break;
                    case "--tester-ram": o.TesterRam = true; break;
                    case "--sans-rapport": o.SansRapport = true; break;
                    case "--rapport-seul": o.RapportSeul = true; break;
                    case "--planifier": o.Planifier = true; break;
                    case "--deplanifier": o.Deplanifier = true; break;
                    case "--planifier-apps": o.PlanifierApps = true; break;
                    case "--deplanifier-apps": o.DeplanifierApps = true; break;
                    case "--point-restauration": o.PointRestauration = true; break;
                    case "--optimiser": o.Optimiser = true; break;
                    case "--maj-apps": o.MajApps = true; break;
                    case "--securite": o.Securite = true; break;
                    case "--antivirus": o.Antivirus = true; break;
                    case "--reparer": o.Reparer = true; break;
                    case "--seul": o.Seul = true; break;
                    case "--compat": o.Compat = true; break;
                    case "--notifier": o.Notifier = true; break;
                    case "--surveillance": o.Surveillance = true; break;
                    case "--sauvegarde": o.Sauvegarde = true; break;
                    case "--mode-jeu": o.ModeJeu = true; break;
                    case "--fin-mode-jeu": o.FinModeJeu = true; break;
                    case "--maj-windows": o.MajWindows = true; break;
                    case "--progression": o.Progression = true; break;
                    case "--anciens-pilotes": o.AnciensPilotes = true; break;
                    case "--sante-disques": o.SanteDisques = true; break;
                    case "--analyse-demarrage": o.AnalyseDemarrage = true; break;
                    case "--kit-reinstallation": o.Kit = true; break;
                    case "--wifi": o.Wifi = true; break;
                    case "--analyse-plantages": o.Plantages = true; break;
                    case "--leger": o.Leger = true; break;
                    case "--incrementale": o.Incrementale = true; break;
                    case "--pilote": o.PilotesChoisis.Add(suivant()); break;
                    case "--mode-alim": o.ModeAlim = suivant(); break;
                    case "--destination": o.Destination = suivant(); break;
                    case "--dossiers": o.Dossiers = suivant(); break;
                    case "--ids-maj": o.IdsMaj = suivant(); break;
                    case "--tube": o.Tube = suivant(); break;
                    case "--jours-telechargements": int.TryParse(suivant(), out o.JoursTelechargements); break;
                    case "--jours-temp": int.TryParse(suivant(), out o.JoursTemp); break;
                    case "--seuil-mo": int.TryParse(suivant(), out o.SeuilMo); break;
                    case "--tache":
                        switch (suivant().ToLowerInvariant())
                        {
                            case "hebdo": o.SansRapport = true; o.Notifier = true; break;
                            case "complet": o.Profond = true; o.Disques = true; o.Securite = true; o.Notifier = true; break;
                            case "surveillance": o.Surveillance = true; break;
                            case "applications": o.MajApps = true; o.Seul = true; o.SansRapport = true; o.TacheApps = true; break;
                        }
                        break;
                }
            }
            if (o.ModeAlim != "Performances" && o.ModeAlim != "Equilibre") o.ModeAlim = "";
            if (!Regex.IsMatch(o.IdsMaj, @"^[0-9a-fA-F,\-]*$")) o.IdsMaj = "";
            o.PilotesChoisis = o.PilotesChoisis.Where(p => Regex.IsMatch(p, @"^oem\d{1,4}\.inf$", RegexOptions.IgnoreCase)).ToList();
            return o;
        }
    }

    internal partial class Moteur
    {
        private readonly OptionsMoteur O;
        private readonly bool Admin;
        private bool faireDisques, faireRam, faireNettoyage, faireRapports, faireSecurite, fairePoint;
        private string verbe;
        private StreamWriter journalFlux;
        private string journal, rapport, dossierSortie;
        private readonly List<string> alertesSecurite = new List<string>(), alertesDisques = new List<string>(), alertesRam = new List<string>();
        private readonly List<string> conseilsOptim = new List<string>(), resumeAutres = new List<string>();
        private string resumeRam = "";
        private long totalWindows, totalNavigateurs, totalApplications, tailleCorbeille, totalProfond, tailleDeplacee, grand;
        private int nbDeplaces;

        private Moteur(OptionsMoteur o) { O = o; Admin = Appli.EstAdmin; }

        public static int Executer(string[] args)
        {
            var o = OptionsMoteur.Lire(args);
            Sortie.Connecter(o.Tube);
            try
            {
                var m = new Moteur(o);
                m.Lancer();
                return 0;
            }
            catch (Exception ex)
            {
                Sortie.Ligne("ATTENTION : erreur inattendue du moteur : " + ex.Message);
                JournalDemarrage.Erreur("moteur", ex);
                return 1;
            }
            finally { Sortie.Fermer(); }
        }

        // ------------------------------------------------------------ Sortie
        private void Ecrire(string texte = "")
        {
            if (O.Progression && texte.StartsWith("=== ")) SignalerProgression(texte);
            Sortie.Ligne(texte);
            if (journalFlux != null) { try { journalFlux.WriteLine(texte); } catch { } }
        }

        /// <summary>Données pour l'interface (« @@DONNEES clé json ») : ni affichées ni écrites dans le journal.</summary>
        private static void Donnees(string cle, object valeur) { Sortie.Ligne("@@DONNEES " + cle + " " + Json.Ecrire(valeur).Replace("\r", "").Replace("\n", " ")); }

        private static string T(double octets) { return Format.Taille(octets); }
        private static string Col(string texte, int largeur) { return texte.Length >= largeur ? texte : texte + new string(' ', largeur - texte.Length); }
        private static string Dr(string texte, int largeur) { return texte.Length >= largeur ? texte : new string(' ', largeur - texte.Length) + texte; }

        // ------------------------------------------------------------ Avancement (« @@PROGRESSION début fin »)
        private class Etape { public string Motif; public double Poids; }
        private readonly List<Etape> plan = new List<Etape>();
        private int indexEtape;
        private double poidsTotal;

        private void AjouterEtape(string motif, double poids) { plan.Add(new Etape { Motif = motif, Poids = poids }); }

        private void PreparerPlan()
        {
            if (fairePoint) AjouterEtape("^=== Point de restauration", 4);
            if (faireNettoyage)
            {
                AjouterEtape("^=== Windows :", 5); AjouterEtape("^=== Navigateurs", 4); AjouterEtape("^=== Applications et outils", 3);
                AjouterEtape("^=== Corbeille", 1); AjouterEtape("^=== Téléchargements", 2);
                AjouterEtape("^=== Nettoyage profond", O.Profond ? 35 : 0.5);
            }
            if (faireDisques) AjouterEtape("^=== Disques", O.Verifier ? 35 : 15);
            if (O.AnciensPilotes) AjouterEtape("^=== Anciens pilotes", 10);
            if (O.ModeJeu) AjouterEtape("^=== Mode jeu", 2);
            if (O.FinModeJeu) AjouterEtape("^=== Fin du mode jeu", 1);
            if (O.Sauvegarde) AjouterEtape("^=== Sauvegarde", 40);
            if (faireRam) AjouterEtape("^=== Mémoire vive", 4);
            if (O.Optimiser) AjouterEtape("^=== Bilan d.optimisation", 6);
            if (O.ModeAlim != "") AjouterEtape("^=== Mode d.alimentation", 1);
            if (O.MajApps) AjouterEtape("^=== Mise à jour des applications", 30);
            if (O.MajWindows) AjouterEtape("^=== Mises à jour Windows", 40);
            if (faireSecurite) AjouterEtape("^=== Bilan de sécurité", O.Antivirus ? 25 : 4);
            if (O.Reparer) AjouterEtape("^=== Réparation de Windows", 60);
            if (faireRapports) AjouterEtape("^=== Rapports : ", 15);
            poidsTotal = plan.Sum(e => e.Poids);
        }

        private void SignalerProgression(string titre)
        {
            if (titre.StartsWith("=== Résumé")) { Sortie.Ligne("@@PROGRESSION 100 100"); return; }
            if (poidsTotal <= 0) return;
            for (int i = indexEtape; i < plan.Count; i++)
            {
                if (!Regex.IsMatch(titre, plan[i].Motif)) continue;
                double avant = 0; for (int j = 0; j < i; j++) avant += plan[j].Poids;
                indexEtape = i + 1;
                int debut = (int)Math.Floor(100 * avant / poidsTotal);
                int fin = (int)Math.Min(99, Math.Floor(100 * (avant + plan[i].Poids) / poidsTotal));
                Sortie.Ligne("@@PROGRESSION " + debut + " " + fin);
                return;
            }
        }

        // ------------------------------------------------------------ Déroulement
        private void Lancer()
        {
            faireDisques = O.Disques || O.DisquesSeuls || O.Verifier;
            faireRam = O.LibererRam || O.RamSeule;
            faireNettoyage = !(O.RapportSeul || O.DisquesSeuls || O.RamSeule || O.Seul);
            faireRapports = !(O.SansRapport || O.DisquesSeuls || O.RamSeule || O.Seul);
            faireSecurite = O.Securite || O.Antivirus;
            fairePoint = O.PointRestauration || O.Reparer || (O.Profond && faireNettoyage);

            if (O.Compat) { Compatibilite(); return; }
            if (O.Surveillance) { SurveillanceDisques(); return; }
            if (O.SanteDisques) { SanteDisques(); return; }
            if (O.AnalyseDemarrage) { AnalyseDemarrage(); return; }
            if (O.Kit) { KitReinstallation(); return; }
            if (O.Plantages) { AnalysePlantages(); return; }
            if (Natif.Build < 10240 && Natif.EstWindows) { Ecrire("Nettoyage PC est conçu pour Windows 10 et Windows 11. Ce système est trop ancien : opération annulée par sécurité."); return; }
            if (O.TesterRam) { TesterRam(); return; }
            if (O.Planifier || O.Deplanifier) { Planification(); return; }
            if (O.PlanifierApps || O.DeplanifierApps) { PlanificationApps(); return; }
            if (O.TacheApps)
            {
                // tâche Premium : ne fait rien si Premium n'est plus actif sur ce PC
                try { Licence.Charger(); } catch { }
                if (!Licence.Premium) { Ecrire("Mises à jour automatiques des applications : Premium n'est plus actif sur ce PC, rien n'est fait."); return; }
            }

            bool besoinAdmin = O.Profond || faireDisques || O.LibererRam || fairePoint || O.ModeAlim != "" || O.MajApps || faireSecurite || O.Reparer || O.Optimiser || O.ModeJeu || O.MajWindows || O.AnciensPilotes;
            if (besoinAdmin && !Admin)
                Ecrire("  (Lancé sans l'autorisation administrateur : les éléments qui touchent tout le PC seront ignorés)");

            PreparerPlan();
            dossierSortie = Chemins.Donnees;
            try { Directory.CreateDirectory(dossierSortie); } catch { }
            string horodatage = DateTime.Now.ToString("yyyy-MM-dd_HH-mm");
            journal = NomNeuf(Path.Combine(dossierSortie, "journal_" + horodatage + ".txt"));
            rapport = NomNeuf(Path.Combine(dossierSortie, "rapport_" + horodatage + ".txt"));
            OuvrirJournal();

            verbe = O.Simulation ? "libérables" : "libérés";
            Ecrire("Nettoyage PC complet - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + " - " + Mode());
            Ecrire("  " + InfosPc.Resume() + " - Nettoyage PC " + Appli.Version);
            if (!Admin) Ecrire("  (Lancé sans droits administrateur : les éléments système seront ignorés)");
            Ecrire();

            // Ménage des journaux : on garde 60 jours
            if (!O.Simulation)
            {
                try
                {
                    foreach (var f in new DirectoryInfo(dossierSortie).GetFiles())
                        if (Regex.IsMatch(f.Name, "^(journal|rapport)_") && f.LastWriteTime < DateTime.Now.AddDays(-60) && (f.Attributes & FileAttributes.ReparsePoint) == 0)
                            try { f.Delete(); } catch { }
                }
                catch { }
            }

            string lecteur = Path.GetPathRoot(Chemins.Windows ?? "C:\\");
            long libreAvant = EspaceLibre(lecteur);

            if (fairePoint) PointDeRestauration();
            if (faireNettoyage) Nettoyer();
            if (faireDisques) ModuleDisques();
            if (O.AnciensPilotes) AnciensPilotes();
            if (O.ModeJeu) ModeJeuDebut();
            if (O.FinModeJeu) ModeJeuFin();
            if (O.Sauvegarde) SauvegardeRapide();
            if (faireRam) MemoireVive();
            if (O.Optimiser) BilanOptimisation();
            if (O.ModeAlim != "") ModeAlimentation();
            if (O.MajApps) MajApplications();
            if (O.MajWindows) MajWindows();
            if (faireSecurite) BilanSecurite();
            if (O.Reparer) Reparation();
            if (faireRapports) Rapports();
            else if (!(O.DisquesSeuls || O.RamSeule || O.Seul)) { Ecrire("=== Rapports ==="); Ecrire("  Ignorés (option -SansRapport)."); Ecrire(); }

            Resume(lecteur, libreAvant);
            try { if (journalFlux != null) journalFlux.Dispose(); } catch { }
        }

        private string Mode()
        {
            string mode;
            if (O.RapportSeul) mode = "RAPPORT SEUL (aucun nettoyage)";
            else if (O.DisquesSeuls) mode = O.Simulation ? "DISQUES SEULS - SIMULATION (analyse sans modification)" : "DISQUES SEULS";
            else if (O.RamSeule) mode = O.Simulation ? "RAM SEULE - SIMULATION (analyse sans modification)" : "RAM SEULE";
            else if (O.Seul)
            {
                var n = new List<string>();
                if (O.PointRestauration) n.Add("POINT DE RESTAURATION");
                if (O.Optimiser) n.Add("BILAN D'OPTIMISATION");
                if (O.ModeAlim != "") n.Add("MODE " + O.ModeAlim.ToUpperInvariant());
                if (O.MajApps) n.Add("MISE À JOUR DES APPLICATIONS");
                if (O.Securite) n.Add("BILAN DE SÉCURITÉ");
                if (O.Antivirus) n.Add("ANALYSE ANTIVIRUS");
                if (O.Reparer) n.Add("RÉPARATION DE WINDOWS");
                if (O.ModeJeu) n.Add("MODE JEU");
                if (O.FinModeJeu) n.Add("FIN DU MODE JEU");
                if (O.Sauvegarde) n.Add("SAUVEGARDE");
                if (O.MajWindows) n.Add("MISES À JOUR WINDOWS ET PILOTES");
                if (O.AnciensPilotes) n.Add("ANCIENS PILOTES");
                mode = string.Join(" + ", n);
                if (O.Simulation) mode += " - SIMULATION";
            }
            else if (O.Simulation) mode = "SIMULATION (rien n'est supprimé ni déplacé)";
            else mode = "NETTOYAGE RÉEL";
            if (!O.Seul) { if (O.Optimiser) mode += " + OPTIMISATION"; if (faireSecurite) mode += " + SÉCURITÉ"; }
            if (O.Profond && faireNettoyage) mode += " + PROFOND";
            if (faireDisques && !O.DisquesSeuls) mode += " + DISQUES";
            if (faireRam && !O.RamSeule) mode += " + RAM";
            if (O.Verifier) mode += " + VÉRIFICATION";
            return mode;
        }

        private static string NomNeuf(string chemin)
        {
            if (!File.Exists(chemin) && !Directory.Exists(chemin)) return chemin;
            return Path.Combine(Path.GetDirectoryName(chemin), Path.GetFileNameWithoutExtension(chemin) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6) + Path.GetExtension(chemin));
        }

        /// <summary>Journal créé neuf (jamais à travers un lien), gardé ouvert jusqu'à la fin.</summary>
        private void OuvrirJournal()
        {
            try
            {
                if (!Protection.CheminSansLien(journal)) return;
                var fs = new FileStream(journal, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                if (Natif.EstWindows)
                {
                    string reel = Natif.CheminReel(fs.SafeFileHandle);
                    if (reel != null && !Path.GetDirectoryName(reel).Equals(Path.GetDirectoryName(journal), StringComparison.OrdinalIgnoreCase)) { fs.Dispose(); return; }
                }
                journalFlux = new StreamWriter(fs, new UTF8Encoding(true)) { AutoFlush = true };
            }
            catch { journalFlux = null; }
            if (journalFlux == null) Sortie.Ligne("  (Remarque : le journal de cette action n'a pas pu être enregistré dans Documents\\NettoyagePC.)");
        }

        public static long EspaceLibre(string racine)
        {
            try { return new DriveInfo(racine).AvailableFreeSpace; } catch { return 0; }
        }

        // ------------------------------------------------------------ Résumé
        private void Resume(string lecteur, long libreAvant)
        {
            long libreApres = EspaceLibre(lecteur);
            Ecrire("=== Résumé ===");
            if (faireNettoyage)
            {
                grand = totalWindows + totalNavigateurs + totalApplications + tailleCorbeille + totalProfond;
                Ecrire("  Windows               : " + T(totalWindows));
                Ecrire("  Navigateurs           : " + T(totalNavigateurs));
                Ecrire("  Applications / outils : " + T(totalApplications));
                Ecrire("  Corbeille             : " + T(tailleCorbeille));
                if (O.Profond) Ecrire("  Nettoyage profond     : " + T(totalProfond) + " (hors DISM)");
                Ecrire("  Téléchargements       : " + nbDeplaces + " élément(s) à vérifier (" + T(tailleDeplacee) + ")");
                Ecrire("  TOTAL " + verbe + " : " + T(grand));
                if (!O.Simulation)
                {
                    string type = O.Profond ? "Optimisation complète" : "Nettoyage rapide";
                    if (O.Notifier) type += " (automatique)";
                    FichierSur.AjouterLigne(Path.Combine(dossierSortie, "statistiques.csv"), DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ";" + grand + ";" + type);
                    Ecrire("  Espace libre sur " + lecteur.TrimEnd('\\') + " : " + T(libreAvant) + " -> " + T(libreApres));
                }
            }
            foreach (var r in resumeAutres) Ecrire("  " + r);
            if (O.Optimiser) Ecrire("  Optimisation          : " + conseilsOptim.Count + " conseil(s)");
            if (faireSecurite)
            {
                if (alertesSecurite.Count == 0) Ecrire("  Sécurité              : aucun problème détecté");
                else { Ecrire("  Sécurité              : " + alertesSecurite.Count + " point(s) à corriger"); foreach (var a in alertesSecurite) Ecrire("    - " + a); }
            }
            if (faireRam)
            {
                if (resumeRam != "") Ecrire("  Mémoire vive (RAM)    : " + resumeRam);
                foreach (var a in alertesRam) Ecrire("    - " + a);
            }
            if (faireDisques)
            {
                if (alertesDisques.Count == 0) Ecrire("  Disques               : aucune alerte");
                else { Ecrire("  Disques               : " + alertesDisques.Count + " alerte(s)"); foreach (var a in alertesDisques) Ecrire("    - " + a); }
                if (O.DisquesSeuls && !O.Simulation) Ecrire("  Espace libre sur " + lecteur.TrimEnd('\\') + " : " + T(libreAvant) + " -> " + T(libreApres));
            }
            Ecrire();
            if (journalFlux != null) Ecrire("Journal : " + journal);
            if (faireRapports) Ecrire("Rapport : " + rapport);

            if (O.Notifier && !O.Simulation)
            {
                int nb = alertesSecurite.Count + alertesDisques.Count;
                string texte = nb > 0 ? nb + " point(s) à vérifier : ouvre Nettoyage PC." : "Tout va bien.";
                string libere = faireNettoyage ? T(grand) + " libérés. " : "";
                try
                {
                    var gpu = InfosPc.CarteGraphiquePrincipale();
                    if (gpu != null && gpu.DatePilote.HasValue)
                    {
                        int mois = (int)Math.Floor((DateTime.Now - gpu.DatePilote.Value).TotalDays / 30.4);
                        if (mois >= 4) texte += " Pilote graphique de " + mois + " mois : Accélérer > Pilotes.";
                    }
                }
                catch { }
                Notification.Afficher("Nettoyage PC : entretien terminé", libere + texte);
            }
        }
    }
}
