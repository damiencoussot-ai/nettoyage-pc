// Nettoyage PC - cœur du gardien en temps réel (Premium) : vérifications légères toutes les minutes (mémoire, chaleur,
// démarrage, disque, antivirus, anti-arnaque, santé, électricité, sauvegarde automatique). Il ne ferme jamais rien et ne
// modifie rien d'autre que le nettoyage léger automatique (si activé) ; il prévient par une notification.
// L'icône près de l'horloge (et ses notifications) est fournie par l'interface : WinForms en version 4, native en version 5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace NettoyagePC
{
    /// <summary>Ce que l'icône du gardien sait faire. type de notification : 0 information, 1 avertissement, 2 erreur.</summary>
    internal interface ISurfaceGardien
    {
        void Notifier(string titre, string texte, int type, Action clic);
        void SurUi(Action a);
        void Infobulle(string texte);
        void Quitter();
    }

    internal class GardienCoeur
    {
        private readonly ISurfaceGardien S;
        public ReglagesGardien R = ReglagesGardien.Lire();
        private readonly Dictionary<string, object> etat;
        private int tick;
        private int occupe;                     // vérification ou tâche de fond en cours (0 / 1)
        private int memoireHaute, chaleur;
        private Energie.Releveur energie;
        private double chaleurMaxJour; private DateTime jourChaleur = DateTime.Today; private bool chaleurANoter;
        public DateTime PauseJusqua = DateTime.MinValue;
        private bool tacheEnCours;

        private static string FichierEtat { get { return Path.Combine(Chemins.Local, "gardien-etat.json"); } }

        public GardienCoeur(ISurfaceGardien surface)
        {
            S = surface;
            etat = Json.LireFichier(FichierEtat) as Dictionary<string, object> ?? new Dictionary<string, object>();
        }

        /// <summary>Le gardien démarre seulement avec Premium et s'il est activé (réglages communs aux deux versions).</summary>
        public static bool DoitTourner()
        {
            Langue.Choisir(Parametres.Lire().Langue);
            Licence.Charger();
            return Licence.Premium && ReglagesGardien.Lire().Actif;
        }

        public void Notifier(string titre, string texte, int type, Action clic)
        {
            if (DateTime.Now < PauseJusqua) return;
            S.Notifier(Langue.T(titre), Langue.T(texte), type, clic);
        }

        public void Pause() { PauseJusqua = DateTime.Now.AddHours(1); S.Infobulle(Langue.T("Nettoyage PC : gardien en pause")); }

        public void Recharger() { R = ReglagesGardien.Lire(); if (!R.Actif) S.Quitter(); }

        public void Arreter() { try { if (energie != null) energie.Enregistrer(); } catch { } }

        public static void OuvrirAppli(string args)
        {
            try { Process.Start(new ProcessStartInfo(Chemins.Exe, args ?? "") { UseShellExecute = false, WorkingDirectory = Chemins.Ici }); } catch { }
        }

        /// <summary>Entrée « Sauvegarder maintenant » du menu de l'icône.</summary>
        public void SauvegarderMaintenant()
        {
            if (!LancerSauvegarde(true)) Notifier("Sauvegarde automatique", "Branche la clé USB ou le disque de sauvegarde choisi dans les réglages du gardien.", 0, () => OuvrirAppli("--ouvrir gardien"));
        }

        // ------------------------------------------------------------ État (dates des dernières alertes, programmes connus au démarrage)
        private DateTime Date(string cle)
        {
            DateTime d;
            return DateTime.TryParse(Json.Texte(etat, cle), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d) ? d : DateTime.MinValue;
        }
        private void Noter(string cle) { lock (etat) etat[cle] = DateTime.Now.ToString("o"); EnregistrerEtat(); }
        private bool Depuis(string cle, double heures) { return (DateTime.Now - Date(cle)).TotalHours >= heures; }
        private void EnregistrerEtat() { try { Directory.CreateDirectory(Chemins.Local); string t; lock (etat) t = Json.Ecrire(etat); File.WriteAllText(FichierEtat, t); } catch { } }

        // ------------------------------------------------------------ Vérifications (une fois par minute)
        public void Verifier()
        {
            tick++;
            if (DateTime.Now >= PauseJusqua && PauseJusqua != DateTime.MinValue) { PauseJusqua = DateTime.MinValue; S.Infobulle(Langue.T("Nettoyage PC : gardien actif")); }
            if (Interlocked.CompareExchange(ref occupe, 1, 0) != 0) return;
            int n = tick;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (n % 360 == 0) { Licence.VerifierSiBesoin(); Licence.Reevaluer(); if (!Licence.Premium) { S.SurUi(() => Notifier("Gardien arrêté", "Premium n'est plus actif sur ce PC : le gardien s'arrête. Tu peux le réactiver depuis l'onglet Premium.", 0, () => OuvrirAppli("--page Premium"))); Thread.Sleep(16000); S.SurUi(S.Quitter); return; } }
                    if (R.Memoire) VerifierMemoire();
                    if (R.Temperature) VerifierTemperature();
                    if (R.Demarrage && n % 2 == 1) VerifierDemarrage();
                    if (R.Disque && n % 5 == 1) VerifierDisque();
                    if (R.SauvegardeAuto && n % 5 == 3) S.SurUi(() => LancerSauvegarde(false));
                    if (R.Antivirus && n % 30 == 2) VerifierAntivirus();
                    if (R.Arnaque) VerifierAccesDistant();
                    if (R.Energie) { if (energie == null) energie = new Energie.Releveur(); energie.Mesurer(); }
                    if (R.Sante && n % 720 == 61) VerifierSante();
                    if (R.RappelMensuel && n % 120 == 17) VerifierEntretienMensuel();
                    if (R.ModeJeuAuto || ModeJeuAuto.Actif) VerifierModeJeu();
                    if (R.Bulletin && n % 120 == 47) VerifierBulletin();
                    if (R.NouveauxAppareils && n % 30 == 23) VerifierAppareils();
                    if (n % 360 == 5) Instantanes.PrendreSiBesoin();
                    if (chaleurANoter && n % 30 == 7) { chaleurANoter = false; Sante.EnregistrerTemperature(jourChaleur, chaleurMaxJour); }
                }
                catch (Exception ex) { JournalDemarrage.Erreur("vérification du gardien", ex); }
                finally { Interlocked.Exchange(ref occupe, 0); }
            });
        }

        private void VerifierMemoire()
        {
            ulong total, dispo; Natif.Memoire(out total, out dispo);
            if (total == 0) return;
            double pct = 100.0 * (total - dispo) / total;
            memoireHaute = pct >= 92 ? memoireHaute + 1 : 0;
            if (memoireHaute < 3 || !Depuis("alerteMemoire", 2)) return;
            Process pire = null; long max = 0;
            int moi = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcesses()) { try { if (p.WorkingSet64 > max && p.Id != moi) { max = p.WorkingSet64; pire = p; } } catch { } }
            string qui = pire == null ? "" : " " + Langue.F("« {0} » utilise à lui seul {1}.", pire.ProcessName, Format.Taille(max));
            Noter("alerteMemoire");
            string titre = Langue.F("Mémoire vive presque pleine ({0} %)", pct.ToString("0")), texte = Langue.T("Le PC risque de ralentir.") + qui + " " + Langue.T("Ferme les programmes dont tu n'as pas besoin.");
            S.SurUi(() => Notifier(titre, texte, 1, () => OuvrirAppli("--page Optimisation")));
        }

        // Bouclier anti-arnaque : prévient (une fois) quand un logiciel de prise en main à distance s'ouvre. Ne ferme jamais rien.
        private readonly HashSet<string> accesSignales = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private void VerifierAccesDistant()
        {
            var l = Bouclier.ASignaler();
            accesSignales.RemoveWhere(nom => !l.Any(a => a.Nom == nom));   // refermé : on pourra prévenir à nouveau
            var nouveaux = l.Where(a => !accesSignales.Contains(a.Nom)).ToList();
            if (nouveaux.Count == 0) return;
            foreach (var a in nouveaux) accesSignales.Add(a.Nom);
            var a0 = nouveaux[0];
            string titre = Langue.F("{0} vient de s'ouvrir", a0.Nom);
            string texte = Langue.T("Ce logiciel permet de voir ou de contrôler ton écran à distance. Si c'est toi qui l'as ouvert pour un proche ou ton informaticien, tout va bien. Si un inconnu te l'a demandé au téléphone, c'est une arnaque : clique ici.");
            S.SurUi(() => Notifier(titre, texte, 1, () => OuvrirAppli("--ouvrir bouclier")));
        }

        // Santé prédictive : alerte (au plus tous les 3 jours) si le matériel montre un signe sérieux aujourd'hui
        private void VerifierSante()
        {
            if (!Depuis("alerteSante", 72)) return;
            var a = Sante.AlerteDuJour();
            if (a == null) return;
            Noter("alerteSante");
            S.SurUi(() => Notifier(a.Item1, a.Item2, 1, () => OuvrirAppli("--ouvrir sante")));
        }

        private void VerifierTemperature()
        {
            double t = Natif.TemperatureCarteGraphique();
            if (DateTime.Today != jourChaleur) { if (chaleurMaxJour > 0) Sante.EnregistrerTemperature(jourChaleur, chaleurMaxJour); jourChaleur = DateTime.Today; chaleurMaxJour = 0; }
            if (t > chaleurMaxJour && t < 130) { chaleurMaxJour = t; chaleurANoter = true; }
            chaleur = t >= 90 ? chaleur + 1 : 0;
            if (chaleur < 2 || !Depuis("alerteChaleur", 1)) return;
            Noter("alerteChaleur");
            S.SurUi(() => Notifier(Langue.F("Carte graphique très chaude ({0} °C)", t.ToString("0")), "Vérifie que les ventilateurs tournent et que l'air circule autour du PC (poussière, PC collé au mur).", 1, () => OuvrirAppli(null)));
        }

        private static IEnumerable<string> ProgrammesAuDemarrage()
        {
            var l = new List<string>();
            Action<RegistryKey, string, string> lire = (racine, chemin, prefixe) =>
            {
                try { using (var k = racine.OpenSubKey(chemin)) if (k != null) foreach (var n in k.GetValueNames()) if (n != "" && n != "NettoyagePC-Gardien") l.Add(prefixe + "|" + n); } catch { }
            };
            lire(Registry.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKCU");
            lire(Registry.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", "HKLM");
            lire(Registry.LocalMachine, @"Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", "HKLM32");
            foreach (var d in new[] { Environment.GetFolderPath(Environment.SpecialFolder.Startup), Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup) })
                try { if (Directory.Exists(d)) foreach (var f in Directory.GetFiles(d)) if (!Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) l.Add("DOSSIER|" + Path.GetFileName(f)); } catch { }
            return l.Distinct();
        }

        private void VerifierDemarrage()
        {
            var actuels = ProgrammesAuDemarrage().ToList();
            List<string> liste;
            lock (etat)
            {
                var connus = Json.Valeur(etat, "demarrage") as System.Collections.IEnumerable;
                liste = connus == null || connus is string ? null : connus.Cast<object>().Select(Convert.ToString).ToList();
                etat["demarrage"] = actuels.ToArray();
            }
            EnregistrerEtat();
            if (liste == null) return;   // premier passage : on mémorise
            var nouveaux = actuels.Where(a => !liste.Contains(a)).Select(a => a.Substring(a.IndexOf('|') + 1)).Select(n => n.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - 4) : n).ToList();
            if (nouveaux.Count == 0) return;
            string noms = string.Join(", ", nouveaux.Take(3).Select(n => "« " + n + " »")) + (nouveaux.Count > 3 ? "..." : "");
            string titre = nouveaux.Count == 1 ? Langue.T("Nouveau programme au démarrage") : Langue.F("{0} nouveaux programmes au démarrage", nouveaux.Count);
            string texte = Langue.F(nouveaux.Count == 1 ? "{0} se lancera à chaque démarrage de Windows. Si tu ne l'as pas voulu, clique ici pour le désactiver." : "{0} se lanceront à chaque démarrage de Windows. Si tu ne les as pas voulus, clique ici pour les désactiver.", noms);
            S.SurUi(() => Notifier(titre, texte, 0, () => OuvrirAppli("--page Demarrage")));
        }

        private void VerifierDisque()
        {
            string lecteur = Path.GetPathRoot(Chemins.Windows ?? "C:\\");
            DriveInfo d; try { d = new DriveInfo(lecteur); if (!d.IsReady || d.TotalSize <= 0) return; } catch { return; }
            double pct = 100.0 * d.AvailableFreeSpace / d.TotalSize;
            if (pct >= R.SeuilDisque && d.AvailableFreeSpace >= 5L * 1073741824) return;
            string nom = lecteur.TrimEnd('\\');
            if (R.NettoyageAuto && Depuis("nettoyageAuto", 20) && !tacheEnCours)
            {
                Noter("nettoyageAuto");
                long avant = d.AvailableFreeSpace;
                tacheEnCours = true;
                try { LancerMoteur(new[] { "--leger", "--sans-rapport" }, 30); } finally { tacheEnCours = false; }
                long apres = avant; try { apres = new DriveInfo(lecteur).AvailableFreeSpace; } catch { }
                long libere = Math.Max(0, apres - avant);
                if (libere >= 50L * 1048576)
                {
                    string titre = Langue.F("Nettoyage automatique : {0} libérés", Format.Taille(libere));
                    string texte = Langue.F("Le disque {0} se remplissait : les fichiers temporaires et les caches ont été supprimés (ni la Corbeille, ni tes documents). Il reste {1}.", nom, Format.Taille(apres));
                    S.SurUi(() => Notifier(titre, texte, 0, () => OuvrirAppli("--page Nettoyage")));
                    return;
                }
            }
            if (!Depuis("alerteDisque", 24)) return;
            Noter("alerteDisque");
            long libre = d.AvailableFreeSpace;
            string titreD = Langue.F("Disque {0} presque plein", nom), texteD = Langue.F("Il ne reste que {0} ({1} %). Clique pour voir ce qui prend de la place et faire le ménage.", Format.Taille(libre), pct.ToString("0"));
            S.SurUi(() => Notifier(titreD, texteD, 1, () => OuvrirAppli("--page Disques")));
        }

        /// <summary>
        /// Une fois par mois : si le PC a besoin d'un petit entretien (pas de nettoyage depuis un mois et de l'espace à
        /// récupérer, disque qui se remplit, programmes au démarrage qui peuvent attendre), une seule notification le dit.
        /// Rien n'est lancé : le clic ouvre l'accueil, où chaque action pose sa question.
        /// </summary>
        private void VerifierEntretienMensuel()
        {
            if (Date("rappelMensuel") == DateTime.MinValue) { Noter("rappelMensuel"); return; }   // premier passage : le mois commence
            if (!Depuis("rappelMensuel", 24 * 30)) return;
            Noter("rappelMensuel");
            var raisons = new List<string>();
            var g = Gains.Lire();
            int jours = Gains.JoursDepuisDernier(g);
            long recuperable = 0;
            try { long n; recuperable = Natif.TailleCorbeille(out n) + TailleFichiers.Dossier(Path.GetTempPath()); } catch { }
            if ((jours < 0 || jours >= 30) && recuperable >= 300L * 1048576)
                raisons.Add(jours < 0 ? Langue.F("{0} récupérables", Format.Taille(recuperable)) : Langue.F("{0} récupérables, dernier nettoyage il y a {1} jours", Format.Taille(recuperable), jours));
            try
            {
                var dq = new DriveInfo(Path.GetPathRoot(Chemins.Windows ?? "C:\\"));
                double pct = dq.TotalSize > 0 ? 100.0 * dq.AvailableFreeSpace / dq.TotalSize : 100;
                if (pct < 15) raisons.Add(Langue.F("disque {0} rempli à {1} %", dq.Name.TrimEnd('\\'), (100 - pct).ToString("0")));
            }
            catch { }
            try
            {
                int attendre = ConseilsDemarrage.PeuventAttendre(ElementsDemarrage.Lister()).Count;
                if (attendre >= 3) raisons.Add(Langue.F("{0} programmes au démarrage peuvent attendre", attendre));
            }
            catch { }
            if (raisons.Count == 0) return;
            string texte = Langue.T("Ce mois-ci :") + " " + string.Join(" ; ", raisons) + ". " + Langue.T("Clique pour voir le bilan : chaque action te demande ton accord avant de se lancer.");
            S.SurUi(() => Notifier("Petit entretien du mois", texte, 0, () => OuvrirAppli("--page Accueil")));
        }

        // ------------------------------------------------------------ Mode jeu automatique
        private int sansJeu;

        private void VerifierModeJeu()
        {
            string jeu = R.ModeJeuAuto ? ModeJeuAuto.JeuEnCours() : null;
            if (jeu != null)
            {
                sansJeu = 0;
                if (ModeJeuAuto.Actif) return;
                string fait = ModeJeuAuto.Demarrer(jeu, R.PauseOneDrive);
                string texte = fait != "" ? Langue.F("Pour « {0} » : {1}. Tout revient comme avant quand tu fermes le jeu.", jeu, fait)
                    : Langue.F("Pour « {0} ». Ton PC est déjà en mode performances : rien à changer. Tout revient comme avant quand tu fermes le jeu.", jeu);
                S.SurUi(() => Notifier("Mode jeu activé", texte, 0, () => OuvrirAppli("--ouvrir modejeu")));
                return;
            }
            if (!ModeJeuAuto.Actif) return;
            // le jeu doit être fermé depuis 2 minutes (chargements, changement de fenêtre)
            if (R.ModeJeuAuto && ++sansJeu < 2) return;
            sansJeu = 0;
            var s = ModeJeuAuto.Terminer();
            if (s != null && s.Duree.TotalMinutes >= 1)
                S.SurUi(() => Notifier("Mode jeu terminé", Langue.F("{0} de jeu sur « {1} ». Ton PC est revenu comme avant.", ModeJeuAuto.TexteDuree(s.Duree), s.Jeu), 0, null));
        }

        // ------------------------------------------------------------ Bulletin mensuel
        private void VerifierBulletin()
        {
            // les premiers jours du mois : le bulletin du mois précédent, une seule fois
            var precedent = DateTime.Now.AddMonths(-1);
            precedent = new DateTime(precedent.Year, precedent.Month, 1);
            if (File.Exists(Bulletin.Fichier(precedent))) return;
            if (Date("bulletin") != DateTime.MinValue && Date("bulletin") >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1)) return;
            Noter("bulletin");
            string f;
            try { f = Bulletin.Creer(precedent); } catch (Exception ex) { JournalDemarrage.Erreur("bulletin mensuel", ex); return; }
            string mois = precedent.ToString("MMMM", Langue.Culture);
            S.SurUi(() => Notifier(Langue.F("Ton bulletin de {0} est prêt", mois), "Note du PC, espace gagné, démarrage, températures et santé du mois. Clique pour l'ouvrir (il est aussi dans Documents\\NettoyagePC\\Bulletins).", 0, () => Programmes.Ouvrir(f)));
        }

        // ------------------------------------------------------------ Nouveaux appareils sur le réseau
        private void VerifierAppareils()
        {
            List<AppareilReseau> tous, nouveaux;
            try { nouveaux = SurveillanceReseau.MettreAJour(out tous); } catch { return; }
            if (nouveaux.Count == 0) return;
            string noms = string.Join(", ", nouveaux.Take(3).Select(a => a.Libelle + " (" + a.Ip + ")")) + (nouveaux.Count > 3 ? "..." : "");
            string titre = nouveaux.Count == 1 ? Langue.T("Nouvel appareil sur ton réseau") : Langue.F("{0} nouveaux appareils sur ton réseau", nouveaux.Count);
            string texte = Langue.F("{0}. Si tu ne le reconnais pas (téléphone d'un invité, objet connecté...), change le mot de passe du Wi-Fi de ta box. Clique pour voir la liste.", noms);
            S.SurUi(() => Notifier(titre, texte, 1, () => OuvrirAppli("--ouvrir reseau")));
        }

        private void VerifierAntivirus()
        {
            var produits = Wmi.Requete("SELECT displayName, productState FROM AntiVirusProduct", @"root\SecurityCenter2", 20);
            if (produits.Count == 0) return;
            bool actif = produits.Any(o => { try { long s = Convert.ToInt64(o["productState"]); return ((s >> 12) & 0xF) == 1; } catch { return true; } });
            if (actif) { lock (etat) etat.Remove("alerteAntivirus"); return; }
            if (!Depuis("alerteAntivirus", 12)) return;
            Noter("alerteAntivirus");
            S.SurUi(() => Notifier("Ton PC n'est plus protégé", "Aucun antivirus n'est actif en ce moment (protection en temps réel désactivée). Clique pour ouvrir la Sécurité Windows et la réactiver.", 2, () => Programmes.Ouvrir("windowsdefender://threat/")));
        }

        /// <summary>Lance le moteur sans fenêtre et attend sa fin (minutes).</summary>
        private static int LancerMoteur(IEnumerable<string> args, int minutes)
        {
            var tous = new List<string> { "--moteur" }; tous.AddRange(args);
            using (var p = Process.Start(new ProcessStartInfo(Chemins.Exe, Elevation.Joindre(tous.ToArray())) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Chemins.Ici, WindowStyle = ProcessWindowStyle.Hidden }))
            {
                if (p == null) return -1;
                if (!p.WaitForExit(minutes * 60000)) { try { p.Kill(); } catch { } return -1; }
                return p.ExitCode;
            }
        }

        /// <summary>Sauvegarde automatique : si le lecteur choisi est branché et que la dernière sauvegarde date d'assez longtemps (ou demandée).</summary>
        public bool LancerSauvegarde(bool forcer)
        {
            if (tacheEnCours || !R.SauvegardeAuto && !forcer) return tacheEnCours;
            string dest = Gardien.TrouverDestination(R.SauvegardeId);
            if (dest == null) return false;
            var der = Gardien.DerniereSauvegarde();
            DateTime d = DateTime.MinValue;
            if (der != null) DateTime.TryParse(Json.Texte(der, "Date"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d);
            if (!forcer && ((DateTime.Now - d).TotalHours < R.SauvegardeJours * 24 - 4 || !Depuis("essaiSauvegarde", 6))) return true;
            if (R.SauvegardeDossiers.Count == 0) return false;
            Noter("essaiSauvegarde");
            tacheEnCours = true;
            string libelle = R.SauvegardeLibelle != "" ? R.SauvegardeLibelle : dest.TrimEnd('\\');
            string noms = string.Join(", ", R.SauvegardeDossiers.Select(x => Langue.T(x == "Videos" ? "Vidéos" : x)));
            Notifier("Sauvegarde automatique en cours", Langue.F("Copie des nouveautés ({0}) sur « {1} ». Ne débranche pas la clé pendant la copie.", noms, libelle), 0, null);
            var dossiers = string.Join(",", R.SauvegardeDossiers);
            System.Threading.Tasks.Task.Run(() =>
            {
                int code = -1;
                try { code = LancerMoteur(new[] { "--sauvegarde", "--seul", "--incrementale", "--destination", dest, "--dossiers", dossiers }, 360); }
                catch (Exception ex) { JournalDemarrage.Erreur("sauvegarde automatique", ex); }
                var r = Gardien.DerniereSauvegarde();
                DateTime fin = DateTime.MinValue;
                if (r != null) DateTime.TryParse(Json.Texte(r, "Date"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out fin);
                bool ok = code == 0 && (DateTime.Now - fin).TotalMinutes < 30;
                long octets = r == null ? 0 : Json.Long(r, "Octets");
                S.SurUi(() =>
                {
                    tacheEnCours = false;
                    if (ok) Notifier("Sauvegarde automatique terminée", octets > 0 ? Langue.F("{0} de nouveautés copiées sur « {1} ». Tu peux débrancher la clé.", Format.Taille(octets), libelle) : Langue.F("Rien de nouveau à copier : tout était déjà sauvegardé sur « {0} ». Tu peux débrancher la clé.", libelle), 0, () => OuvrirAppli("--page Sauvegarde"));
                    else Notifier("Sauvegarde automatique incomplète", Langue.F("La copie sur « {0} » ne s'est pas terminée (clé débranchée ou pleine ?). Elle sera retentée plus tard.", libelle), 1, () => OuvrirAppli("--ouvrir gardien"));
                });
            });
            return true;
        }
    }
}
