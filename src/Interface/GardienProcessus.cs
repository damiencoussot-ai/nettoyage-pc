// Nettoyage PC - gardien en temps réel (Premium) : icône près de l'horloge, vérifications légères toutes les minutes,
// notifications Windows. Lancé avec --gardien (à l'ouverture de session), sans droits administrateur, sans fenêtre.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal class GardienProcessus : ApplicationContext
    {
        private static Mutex verrou;
        private readonly NotifyIcon icone = new NotifyIcon();
        private readonly System.Windows.Forms.Timer minuteur = new System.Windows.Forms.Timer { Interval = 60000 };
        private readonly Control invoqueur = new Control();
        private ReglagesGardien R = ReglagesGardien.Lire();
        private readonly Dictionary<string, object> etat;
        private Action actionClic;
        private int tick;
        private int occupe;                     // vérification ou tâche de fond en cours (0 / 1)
        private int memoireHaute, chaleur;
        private Energie.Releveur energie;
        private double chaleurMaxJour; private DateTime jourChaleur = DateTime.Today; private bool chaleurANoter;
        private DateTime pauseJusqua = DateTime.MinValue;
        private bool tacheEnCours;
        private RegisteredWaitHandle attenteArret, attenteRecharger;
        private EventWaitHandle evArret, evRecharger;

        private static string FichierEtat { get { return Path.Combine(Chemins.Local, "gardien-etat.json"); } }

        public static int Executer()
        {
            bool nouveau;
            verrou = new Mutex(true, Gardien.NomMutex, out nouveau);
            if (!nouveau) return 0;
            try
            {
                Langue.Choisir(Parametres.Lire().Langue);
                Licence.Charger();
                if (!Licence.Premium || !ReglagesGardien.Lire().Actif) return 0;
                JournalDemarrage.Noter("Gardien démarré.");
                Application.EnableVisualStyles();
                Application.Run(new GardienProcessus());
                JournalDemarrage.Noter("Gardien arrêté.");
                return 0;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("gardien", ex); return 1; }
            finally { try { verrou.ReleaseMutex(); } catch { } }
        }

        private GardienProcessus()
        {
            invoqueur.CreateControl();
            etat = Json.LireFichier(FichierEtat) as Dictionary<string, object> ?? new Dictionary<string, object>();
            try { icone.Icon = Icon.ExtractAssociatedIcon(Chemins.Exe); } catch { icone.Icon = SystemIcons.Shield; }
            icone.Text = Langue.T("Nettoyage PC : gardien actif");
            icone.ContextMenuStrip = Menu();
            icone.DoubleClick += (s, e) => OuvrirAppli(null);
            icone.BalloonTipClicked += (s, e) => { var a = actionClic; actionClic = null; if (a != null) try { a(); } catch { } };
            icone.Visible = true;

            evArret = new EventWaitHandle(false, EventResetMode.AutoReset, Gardien.NomArret);
            evRecharger = new EventWaitHandle(false, EventResetMode.AutoReset, Gardien.NomRecharger);
            attenteArret = ThreadPool.RegisterWaitForSingleObject(evArret, (o, t) => SurUi(Quitter), null, -1, true);
            attenteRecharger = ThreadPool.RegisterWaitForSingleObject(evRecharger, (o, t) => SurUi(() => { R = ReglagesGardien.Lire(); if (!R.Actif) Quitter(); }), null, -1, false);

            minuteur.Tick += (s, e) => Verifier();
            minuteur.Start();
            // première vérification une demi-minute après l'ouverture de session
            var premier = new System.Windows.Forms.Timer { Interval = 30000 };
            premier.Tick += (s, e) => { premier.Stop(); premier.Dispose(); Verifier(); };
            premier.Start();
        }

        private void SurUi(Action a) { try { if (invoqueur.IsHandleCreated) invoqueur.BeginInvoke(a); } catch { } }

        private ContextMenuStrip Menu()
        {
            var m = new ContextMenuStrip();
            m.Items.Add(new ToolStripMenuItem(Langue.T("Nettoyage PC : gardien actif")) { Enabled = false });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Langue.T("Ouvrir Nettoyage PC"), null, (s, e) => OuvrirAppli(null));
            m.Items.Add(Langue.T("Sauvegarder maintenant"), null, (s, e) => { if (!LancerSauvegarde(true)) Notifier("Sauvegarde automatique", "Branche la clé USB ou le disque de sauvegarde choisi dans les réglages du gardien.", ToolTipIcon.Info, () => OuvrirAppli("--ouvrir gardien")); });
            m.Items.Add(Langue.T("Réglages du gardien..."), null, (s, e) => OuvrirAppli("--ouvrir gardien"));
            m.Items.Add(Langue.T("Pause d'une heure"), null, (s, e) => { pauseJusqua = DateTime.Now.AddHours(1); icone.Text = Langue.T("Nettoyage PC : gardien en pause"); });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Langue.T("Arrêter le gardien"), null, (s, e) => Quitter());
            Theme.P = Theme.WindowsEnSombre() ? Theme.Sombre : Theme.Clair;
            m.Renderer = new ToolStripProfessionalRenderer(new CouleursMenu());
            m.BackColor = Theme.P.Carte; m.ForeColor = Theme.P.Texte;
            return m;
        }

        private class CouleursMenu : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Theme.P.Survol; } }
            public override Color MenuItemBorder { get { return Theme.P.Bordure; } }
            public override Color ToolStripDropDownBackground { get { return Theme.P.Carte; } }
            public override Color ImageMarginGradientBegin { get { return Theme.P.Carte; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.P.Carte; } }
            public override Color ImageMarginGradientEnd { get { return Theme.P.Carte; } }
            public override Color MenuBorder { get { return Theme.P.Bordure; } }
            public override Color SeparatorDark { get { return Theme.P.Bordure; } }
        }

        private void Quitter()
        {
            minuteur.Stop();
            try { if (energie != null) energie.Enregistrer(); } catch { }
            try { attenteArret.Unregister(null); attenteRecharger.Unregister(null); } catch { }
            icone.Visible = false; icone.Dispose();
            ExitThread();
        }

        private void OuvrirAppli(string args)
        {
            try { Process.Start(new ProcessStartInfo(Chemins.Exe, args ?? "") { UseShellExecute = false, WorkingDirectory = Chemins.Ici }); } catch { }
        }

        private void Notifier(string titre, string texte, ToolTipIcon type, Action clic)
        {
            if (DateTime.Now < pauseJusqua) return;
            actionClic = clic;
            try { icone.ShowBalloonTip(15000, Langue.T(titre), Langue.T(texte), type); } catch { }
        }

        // ------------------------------------------------------------ État (dates des dernières alertes, programmes connus au démarrage)
        private DateTime Date(string cle)
        {
            DateTime d;
            return DateTime.TryParse(Json.Texte(etat, cle), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d) ? d : DateTime.MinValue;
        }
        private void Noter(string cle) { etat[cle] = DateTime.Now.ToString("o"); EnregistrerEtat(); }
        private bool Depuis(string cle, double heures) { return (DateTime.Now - Date(cle)).TotalHours >= heures; }
        private void EnregistrerEtat() { try { Directory.CreateDirectory(Chemins.Local); File.WriteAllText(FichierEtat, Json.Ecrire(etat)); } catch { } }

        // ------------------------------------------------------------ Vérifications
        private void Verifier()
        {
            tick++;
            if (DateTime.Now >= pauseJusqua && pauseJusqua != DateTime.MinValue) { pauseJusqua = DateTime.MinValue; icone.Text = Langue.T("Nettoyage PC : gardien actif"); }
            if (Interlocked.CompareExchange(ref occupe, 1, 0) != 0) return;
            int n = tick;
            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    if (n % 360 == 0) { Licence.VerifierSiBesoin(); Licence.Reevaluer(); if (!Licence.Premium) { SurUi(() => { Notifier("Gardien arrêté", "Premium n'est plus actif sur ce PC : le gardien s'arrête. Tu peux le réactiver depuis l'onglet Premium.", ToolTipIcon.Info, () => OuvrirAppli("--page Premium")); }); Thread.Sleep(16000); SurUi(Quitter); return; } }
                    if (R.Memoire) VerifierMemoire();
                    if (R.Temperature) VerifierTemperature();
                    if (R.Demarrage && n % 2 == 1) VerifierDemarrage();
                    if (R.Disque && n % 5 == 1) VerifierDisque();
                    if (R.SauvegardeAuto && n % 5 == 3) SurUi(() => LancerSauvegarde(false));
                    if (R.Antivirus && n % 30 == 2) VerifierAntivirus();
                    if (R.Arnaque) VerifierAccesDistant();
                    if (R.Energie) { if (energie == null) energie = new Energie.Releveur(); energie.Mesurer(); }
                    if (R.Sante && n % 720 == 61) VerifierSante();
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
            foreach (var p in Process.GetProcesses()) { try { if (p.WorkingSet64 > max && p.Id != Process.GetCurrentProcess().Id) { max = p.WorkingSet64; pire = p; } } catch { } }
            string qui = pire == null ? "" : " " + Langue.F("« {0} » utilise à lui seul {1}.", pire.ProcessName, Format.Taille(max));
            Noter("alerteMemoire");
            string titre = Langue.F("Mémoire vive presque pleine ({0} %)", pct.ToString("0")), texte = Langue.T("Le PC risque de ralentir.") + qui + " " + Langue.T("Ferme les programmes dont tu n'as pas besoin.");
            SurUi(() => Notifier(titre, texte, ToolTipIcon.Warning, () => OuvrirAppli("--page Optimisation")));
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
            SurUi(() => Notifier(titre, texte, ToolTipIcon.Warning, () => OuvrirAppli("--ouvrir bouclier")));
        }

        // Santé prédictive : alerte (au plus tous les 3 jours) si le matériel montre un signe sérieux aujourd'hui
        private void VerifierSante()
        {
            if (!Depuis("alerteSante", 72)) return;
            var a = Sante.AlerteDuJour();
            if (a == null) return;
            Noter("alerteSante");
            SurUi(() => Notifier(a.Item1, a.Item2, ToolTipIcon.Warning, () => OuvrirAppli("--ouvrir sante")));
        }

        private void VerifierTemperature()
        {
            double t = Natif.TemperatureCarteGraphique();
            if (DateTime.Today != jourChaleur) { if (chaleurMaxJour > 0) Sante.EnregistrerTemperature(jourChaleur, chaleurMaxJour); jourChaleur = DateTime.Today; chaleurMaxJour = 0; }
            if (t > chaleurMaxJour && t < 130) { chaleurMaxJour = t; chaleurANoter = true; }
            chaleur = t >= 90 ? chaleur + 1 : 0;
            if (chaleur < 2 || !Depuis("alerteChaleur", 1)) return;
            Noter("alerteChaleur");
            SurUi(() => Notifier(Langue.F("Carte graphique très chaude ({0} °C)", t.ToString("0")), "Vérifie que les ventilateurs tournent et que l'air circule autour du PC (poussière, PC collé au mur).", ToolTipIcon.Warning, () => OuvrirAppli(null)));
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
            var connus = Json.Valeur(etat, "demarrage") as System.Collections.IEnumerable;
            var liste = connus == null || connus is string ? null : connus.Cast<object>().Select(Convert.ToString).ToList();
            etat["demarrage"] = actuels.ToArray();
            EnregistrerEtat();
            if (liste == null) return;   // premier passage : on mémorise
            var nouveaux = actuels.Where(a => !liste.Contains(a)).Select(a => a.Substring(a.IndexOf('|') + 1)).Select(n => n.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ? n.Substring(0, n.Length - 4) : n).ToList();
            if (nouveaux.Count == 0) return;
            string noms = string.Join(", ", nouveaux.Take(3).Select(n => "« " + n + " »")) + (nouveaux.Count > 3 ? "..." : "");
            string titre = nouveaux.Count == 1 ? Langue.T("Nouveau programme au démarrage") : Langue.F("{0} nouveaux programmes au démarrage", nouveaux.Count);
            string texte = Langue.F(nouveaux.Count == 1 ? "{0} se lancera à chaque démarrage de Windows. Si tu ne l'as pas voulu, clique ici pour le désactiver." : "{0} se lanceront à chaque démarrage de Windows. Si tu ne les as pas voulus, clique ici pour les désactiver.", noms);
            SurUi(() => Notifier(titre, texte, ToolTipIcon.Info, () => OuvrirAppli("--page Demarrage")));
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
                    SurUi(() => Notifier(titre, texte, ToolTipIcon.Info, () => OuvrirAppli("--page Nettoyage")));
                    return;
                }
            }
            if (!Depuis("alerteDisque", 24)) return;
            Noter("alerteDisque");
            long libre = d.AvailableFreeSpace;
            string titreD = Langue.F("Disque {0} presque plein", nom), texteD = Langue.F("Il ne reste que {0} ({1} %). Clique pour voir ce qui prend de la place et faire le ménage.", Format.Taille(libre), pct.ToString("0"));
            SurUi(() => Notifier(titreD, texteD, ToolTipIcon.Warning, () => OuvrirAppli("--page Disques")));
        }

        private void VerifierAntivirus()
        {
            var produits = Wmi.Requete("SELECT displayName, productState FROM AntiVirusProduct", @"root\SecurityCenter2", 20);
            if (produits.Count == 0) return;
            bool actif = produits.Any(o => { try { long s = Convert.ToInt64(o["productState"]); return ((s >> 12) & 0xF) == 1; } catch { return true; } });
            if (actif) { etat.Remove("alerteAntivirus"); return; }
            if (!Depuis("alerteAntivirus", 12)) return;
            Noter("alerteAntivirus");
            SurUi(() => Notifier("Ton PC n'est plus protégé", "Aucun antivirus n'est actif en ce moment (protection en temps réel désactivée). Clique pour ouvrir la Sécurité Windows et la réactiver.", ToolTipIcon.Error, () => Programmes.Ouvrir("windowsdefender://threat/")));
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
        private bool LancerSauvegarde(bool forcer)
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
            Notifier("Sauvegarde automatique en cours", Langue.F("Copie des nouveautés ({0}) sur « {1} ». Ne débranche pas la clé pendant la copie.", noms, libelle), ToolTipIcon.Info, null);
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
                SurUi(() =>
                {
                    tacheEnCours = false;
                    if (ok) Notifier("Sauvegarde automatique terminée", octets > 0 ? Langue.F("{0} de nouveautés copiées sur « {1} ». Tu peux débrancher la clé.", Format.Taille(octets), libelle) : Langue.F("Rien de nouveau à copier : tout était déjà sauvegardé sur « {0} ». Tu peux débrancher la clé.", libelle), ToolTipIcon.Info, () => OuvrirAppli("--page Sauvegarde"));
                    else Notifier("Sauvegarde automatique incomplète", Langue.F("La copie sur « {0} » ne s'est pas terminée (clé débranchée ou pleine ?). Elle sera retentée plus tard.", libelle), ToolTipIcon.Warning, () => OuvrirAppli("--ouvrir gardien"));
                });
            });
            return true;
        }
    }
}
