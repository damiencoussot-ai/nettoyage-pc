// Nettoyage PC - fenêtre principale : barre latérale, en-tête, onglets, pages, journal, exécution des actions
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class ActionMoteur
    {
        public string Cle, Titre, Confirmer, Requiert;
        public string[] Args = new string[0];
        public bool Simu, SansAdmin;
    }

    /// <summary>Ce qu'un bouton déclenche : une action du moteur, une page, un lien ou du code.</summary>
    internal class Cible
    {
        public string Type, Cle;
        public Action Code;
        public static Cible A(string cle) { return new Cible { Type = "action", Cle = cle }; }
        public static Cible Page(string cle) { return new Cible { Type = "page", Cle = cle }; }
        public static Cible Lien(string uri) { return new Cible { Type = "uri", Cle = uri }; }
        public static Cible Faire(Action a) { return new Cible { Type = "code", Code = a }; }
    }

    internal class Section { public string Cle, Nom; public int Icone; public string[] Pages; public bool Cachee, EnBas; }

    internal partial class FenetrePrincipale : Form
    {
        public Parametres Reglages = Parametres.Lire();
        public readonly Dictionary<string, ActionMoteur> Actions = new Dictionary<string, ActionMoteur>();
        public readonly Dictionary<string, bool> Disponible = new Dictionary<string, bool>();
        public readonly Dictionary<string, string> Raisons = new Dictionary<string, string>
        {
            { "winget", "winget (App Installer) n'est pas installé sur ce PC." }, { "defender", "Microsoft Defender n'est pas l'antivirus actif sur ce PC." },
            { "stockage", "Les outils de stockage de Windows sont absents de ce PC." }, { "mdsched", "Le Diagnostic de mémoire Windows est absent de ce PC." },
            { "restaurer", "Les points de restauration ne sont pas disponibles sur ce PC." }
        };
        public readonly List<Bouton> BoutonsActions = new List<Bouton>();
        public readonly Dictionary<string, Page> Pages = new Dictionary<string, Page>();
        public readonly List<Section> Sections = new List<Section>();
        private readonly Dictionary<string, string> dernierePage = new Dictionary<string, string>();
        public string PageActive = "", SectionActive = "";
        public event Action<string> ActionFinie;
        /// <summary>Données envoyées par le moteur (« @@DONNEES clé json ») : clé, objet JSON.</summary>
        public event Action<string, object> DonneesRecues;
        public void RecevoirDonnees(string cle, object valeur) { var h = DonneesRecues; if (h != null) h(cle, valeur); }
        public string EtatMaj = "";   // "", verification, disponible, ajour, erreur
        public bool FermetureSansQuestion;

        // Contrôles de structure
        private BarreLaterale barre;
        private Surface droite, entete, hotePages, panneauJournal, barreJournal;
        private Etiquette titrePage, sousTitrePage, statut, infoSysteme;
        public CaseACocher ChkSimu;
        private BoutonIcone bTheme;
        private BoutonRecherche bRecherche;
        private Bouton bRetourSimple;
        public BandeauMiseAJour BandeauMaj;
        private BarreOnglets onglets;
        private BarreProgression progression;
        private Bouton boutonArret, boutonDetails;
        private ConsoleJournal console;
        private readonly Dictionary<string, BoutonNav> boutonsNav = new Dictionary<string, BoutonNav>();
        private readonly Timer minuteur = new Timer { Interval = 200 };
        private readonly Timer minuteurThemeAuto = new Timer { Interval = 2500 };
        private readonly Timer minuteurLicence = new Timer { Interval = 600000 };

        public FenetrePrincipale()
        {
            Text = "Nettoyage PC " + Appli.Version;
            AutoScaleMode = AutoScaleMode.None;
            var zone = Screen.PrimaryScreen.WorkingArea;
            Size = new Size(Math.Min(1200, zone.Width - 20), Math.Min(800, zone.Height - 20));
            MinimumSize = new Size(Math.Min(880, zone.Width), Math.Min(580, zone.Height));
            StartPosition = FormStartPosition.CenterScreen;
            if (zone.Width < 1220 || zone.Height < 780) WindowState = FormWindowState.Maximized;
            Font = Theme.Normal; KeyPreview = true; DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Chemins.Exe); } catch { }
            Theme.ChoisirAccent(Licence.Premium ? Reglages.Accent : "aurore");
            Grille.LireOptions = cle => ("," + Reglages.OptionsOuvertes + ",").Contains("," + cle + ",");
            Grille.EcrireOptions = (cle, ouvert) =>
            {
                var l = Reglages.OptionsOuvertes.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(x => x != cle).ToList();
                if (ouvert) l.Add(cle);
                Reglages.OptionsOuvertes = string.Join(",", l); Reglages.Ecrire();
            };
            Theme.P = ThemeVoulu() ? Theme.Sombre : Theme.Clair;
            BackColor = Theme.P.Fond;

            DefinirActions();
            DefinirSections();
            foreach (var sc in Sections) sc.Nom = Langue.T(sc.Nom);
            ConstruireStructure();
            ConstruirePages();
            DetecterDisponibilites();

            minuteur.Tick += (s, e) => LireSortieMoteur();
            Licence.Change += () => { try { if (IsHandleCreated) BeginInvoke(new Action(LicenceChangee)); } catch { } };
            minuteurLicence.Tick += (s, e) => Licence.Reevaluer();
            minuteurThemeAuto.Tick += (s, e) => { if (Reglages.Theme == "Auto" && Theme.WindowsEnSombre() != Theme.EstSombre) AppliquerTheme(Theme.WindowsEnSombre()); };
            KeyDown += Raccourcis;
            Shown += (s, e) => AuDemarrage();
            FormClosing += Fermeture;
        }

        // ------------------------------------------------------------ Actions du moteur
        private void Act(string cle, string titre, string args, bool simu, bool sansAdmin = false, string confirmer = null, string requiert = null)
        {
            Actions[cle] = new ActionMoteur { Cle = cle, Titre = Langue.T(titre), Args = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), Simu = simu, SansAdmin = sansAdmin, Confirmer = confirmer, Requiert = requiert };
        }

        private void DefinirActions()
        {
            Act("standard", "Nettoyage rapide", "", true, true, "Lancer le nettoyage rapide ?\n\nTemporaires et caches supprimés, corbeille vidée, téléchargements de plus de 90 jours rangés dans « _A_verifier ». Ferme tes navigateurs pour un meilleur résultat.");
            Act("complet", "Optimisation complète", "--profond --disques --liberer-ram --optimiser --securite", true, false, "Lancer l'optimisation complète ?\n\nUn point de restauration est créé d'abord. Puis : nettoyage, nettoyage profond de Windows, optimisation des disques, libération de la RAM, bilans d'optimisation et de sécurité.\n\nCela peut durer longtemps : évite d'éteindre le PC et enregistre ton travail.");
            Act("analyse", "Analyse complète (sans modification)", "--simulation --profond --disques --liberer-ram --optimiser --securite", false);
            Act("disques", "Optimisation des disques", "--disques-seuls", true, false, "Optimiser les disques maintenant ?\n\nSSD : ReTrim (rapide, jamais de défragmentation). Disques durs : défragmentation (peut être long).", "stockage");
            Act("verifier", "Vérification des disques", "--disques-seuls --verifier", true, false, "Optimiser et vérifier les disques ?\n\nL'analyse du système de fichiers est en lecture seule mais peut durer plusieurs minutes par disque.", "stockage");
            Act("ramlib", "Libération de la RAM", "--ram-seule --liberer-ram", true);
            Act("raminfo", "Infos mémoire vive", "--ram-seule", false, true);
            Act("ramtest", "Test des barrettes", "--tester-ram", false, true, "Ouvrir le Diagnostic de mémoire Windows ?\n\nIl te proposera de redémarrer le PC pour tester les barrettes. Enregistre ton travail avant.", "mdsched");
            Act("rapport", "Rapport du PC", "--rapport-seul", false, true);
            Act("analyselite", "Analyse (sans modification)", "--simulation", false, true);
            Act("compat", "Compatibilité de ce PC", "--compat", false, true);
            Act("planifier", "Planification", "--planifier", false, false, "Activer l'entretien automatique ?\n\n- Nettoyage rapide : chaque dimanche à 10h\n- Optimisation complète : toutes les 4 semaines, le dimanche à 11h\n- Surveillance des disques : chaque jour\n\nNe déplace plus le dossier de l'application ensuite.");
            Act("deplanifier", "Suppression de la planification", "--deplanifier", false);
            Act("optim", "Bilan d'optimisation", "--optimiser --seul", false);
            Act("perf", "Mode performances", "--mode-alim Performances --seul", true, false, "Activer le mode « Performances élevées » ?\n\nPlus réactif, consommation un peu plus élevée (autonomie réduite sur portable). Réversible avec « Mode équilibré ».");
            Act("equilibre", "Mode équilibré", "--mode-alim Equilibre --seul", true);
            Act("majapps", "Mise à jour des applications", "--maj-apps --seul", true, false, "Mettre à jour toutes les applications ?\n\nwinget, l'outil officiel de Microsoft, télécharge les nouvelles versions depuis les éditeurs. Ferme si possible les applications concernées. Cela peut durer longtemps.", "winget");
            Act("secu", "Bilan de sécurité", "--securite --seul", false);
            Act("antivirus", "Analyse antivirus", "--securite --antivirus --seul", true, false, null, "defender");
            Act("reparer", "Réparation de Windows", "--reparer --seul", true, false, "Réparer Windows ?\n\nUn point de restauration est créé, puis DISM et SFC vérifient et réparent les fichiers système (20 à 50 minutes). Évite d'éteindre le PC pendant l'opération.");
            Act("point", "Point de restauration", "--point-restauration --seul", false, false, null, "restaurer");
            Act("modejeu", "Mode jeu", "--mode-jeu --ram-seule --liberer-ram --seul", true);
            Act("finmodejeu", "Fin du mode jeu", "--fin-mode-jeu --seul", true, true);
            Act("sauvegarde", "Sauvegarde", "", true, true);
            Act("majwin", "Mises à jour Windows et pilotes", "", true);
            Act("pilotes", "Nettoyage des anciens pilotes", "--anciens-pilotes --seul", true);
            Act("santedisques", "Santé détaillée des disques", "--sante-disques", false);
            Act("demarrage", "Analyse du démarrage", "--analyse-demarrage", false);
            Act("kit", "Kit de réinstallation", "--kit-reinstallation", false);
            Act("plantages", "Analyse des plantages", "--analyse-plantages", false, true);
        }

        // ------------------------------------------------------------ Rubriques et pages
        private void DefinirSections()
        {
            // 6 rubriques principales ; en bas, plus discrètes : mises à jour, historique, réglages
            Sections.Add(new Section { Cle = "Accueil", Nom = "Accueil", Icone = 0xE80F, Pages = new[] { "Accueil" } });
            Sections.Add(new Section { Cle = "Nettoyer", Nom = "Nettoyer", Icone = 0xE74D, Pages = new[] { "Nettoyage", "Disques", "Doublons" } });
            Sections.Add(new Section { Cle = "Accelerer", Nom = "Accélérer", Icone = 0xE945, Pages = new[] { "Optimisation", "Demarrage", "Applications", "Jeux" } });
            Sections.Add(new Section { Cle = "Proteger", Nom = "Protéger", Icone = 0xEA18, Pages = new[] { "Securite", "Sauvegarde" } });
            Sections.Add(new Section { Cle = "Outils", Nom = "Outils", Icone = 0xE90F, Pages = new[] { "Materiel", "Potentiel", "Reseau", "Aide" } });
            Sections.Add(new Section { Cle = "Premium", Nom = "Premium", Icone = 0xE734, Pages = new[] { "Premium" } });
            Sections.Add(new Section { Cle = "MisesAJour", Nom = "Mises à jour", Icone = 0xE896, Pages = new[] { "Pilotes", "MisesAJour" }, EnBas = true });
            Sections.Add(new Section { Cle = "Historique", Nom = "Historique", Icone = 0xE81C, Pages = new[] { "Historique" }, EnBas = true });
            Sections.Add(new Section { Cle = "Reglages", Nom = "Réglages", Icone = 0xE713, Pages = new[] { "Reglages" }, EnBas = true });
            Sections.Add(new Section { Cle = "Simple", Nom = "Accueil", Icone = 0xE80F, Pages = new[] { "Simple" }, Cachee = true });
        }

        private void ConstruirePages()
        {
            var liste = new Page[]
            {
                new PageAccueil(this), new PageNettoyage(this), new PageDoublons(this), new PageDisques(this),
                new PageOptimisation(this), new PageDemarrage(this), new PageApplications(this), new PagePilotes(this),
                new PageJeux(this), new PageSecurite(this), new PageSauvegarde(this),
                new PageMateriel(this), new PagePotentiel(this), new PageReseau(this), new PageAide(this), new PagePremium(this), new PageSimple(this),
                new PageHistorique(this), new PageMisesAJour(this), new PageReglages(this)
            };
            foreach (var p in liste)
            {
                p.Dock = DockStyle.Fill; p.Visible = false;
                Pages[p.Cle] = p;
                hotePages.Controls.Add(p);
            }
        }

        public Section SectionDe(string page) { return Sections.First(s => s.Pages.Contains(page)); }

        public void AfficherPage(string cle)
        {
            Page p;
            if (!Pages.TryGetValue(cle, out p)) return;
            using (Dessin.Geler(droite))
            {
                AfficherPageSansDessin(cle, p);
            }
        }

        private void AfficherPageSansDessin(string cle, Page p)
        {
            hotePages.SuspendLayout();
            p.Visible = true;
            foreach (var kv in Pages) if (kv.Key != cle) kv.Value.Visible = false;
            hotePages.ResumeLayout();
            var sc = SectionDe(cle);
            SectionActive = sc.Cle; dernierePage[sc.Cle] = cle; PageActive = cle;
            titrePage.Text = sc.Nom;
            sousTitrePage.Text = p.Sous;
            bool avecOnglets = sc.Pages.Length > 1;
            onglets.Visible = avecOnglets && !Reglages.ModeSimple;
            bRetourSimple.Visible = Reglages.ModeSimple && cle != "Simple";
            entete.Height = avecOnglets && !Reglages.ModeSimple ? 118 : 76;
            onglets.Invalidate();
            foreach (var b in boutonsNav.Values) b.MettreAJour();
            if (!p.Chargee) { p.Chargee = true; try { p.PremiereFois(); } catch (Exception ex) { JournalDemarrage.Erreur("page " + cle, ex); } }
            try { p.Montrer(); } catch (Exception ex) { JournalDemarrage.Erreur("page " + cle, ex); }
        }

        public void AfficherSection(string cle)
        {
            var sc = Sections.FirstOrDefault(s => s.Cle == cle);
            if (sc == null) return;
            string pg; if (!dernierePage.TryGetValue(cle, out pg)) pg = sc.Pages[0];
            if (cle == "MisesAJour" && EtatMaj == "disponible") pg = "MisesAJour";   // la pastille orange mène à la nouvelle version
            AfficherPage(pg);
        }

        /// <summary>Commande reçue de l'extérieur (raccourci, gardien, assistant vocal). Une action est toujours confirmée.</summary>
        public void TraiterCommande(CommandeExterne c, bool auDemarrage)
        {
            if (c == null || c.Vide || IsDisposed) return;
            try
            {
                if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
                Activate();
                if (c.Page != null && Pages.ContainsKey(c.Page) && (!Reglages.ModeSimple || !auDemarrage)) AfficherPage(c.Page);
                if (c.Ouvrir != null) OuvrirOutil(c.Ouvrir);
                if (c.Action != null)
                {
                    ActionMoteur a;
                    if (!Assistant.ActionsPermises.ContainsKey(c.Action) || !Actions.TryGetValue(c.Action, out a)) { Statut(Langue.F("Action refusée ou inconnue : « {0} ».", c.Action), true); }
                    else LancerAction(c.Action, null, Langue.F("Une demande est arrivée de l'extérieur (raccourci ou assistant vocal) : « {0} ».", Langue.T(a.Titre)) + "\n\n" + Langue.T(a.Confirmer ?? "Lancer cette action maintenant ?"));
                }
                if (c.Question != null) FenetreAssistant.Ouvrir(this, c.Question);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("commande externe", ex); }
        }

        /// <summary>Ouvre un outil par son nom court (gardien, bouclier, assistant...). Les outils Premium proposent Premium s'il n'est pas actif.</summary>
        public void OuvrirOutil(string nom)
        {
            switch (nom)
            {
                case "bouclier": FenetreBouclier.Ouvrir(this); break;
                case "gardien": if (Licence.Premium) FenetreGardien.Ouvrir(this); else AfficherPage("Premium"); break;
                case "assistant": if (Licence.Premium) FenetreAssistant.Ouvrir(this, null); else AfficherPage("Premium"); break;
                case "enqueteur": if (Licence.Premium) FenetreEnqueteur.Ouvrir(this); else AfficherPage("Premium"); break;
                case "sante": if (Licence.Premium) FenetreSante.Ouvrir(this); else AfficherPage("Premium"); break;
                case "energie": if (Licence.Premium) FenetreEnergie.Ouvrir(this); else AfficherPage("Premium"); break;
                case "fps": if (Licence.Premium) FenetreFps.Ouvrir(this); else AfficherPage("Premium"); break;
                case "plantages": if (Licence.Premium) new FenetrePlantages(this).Show(this); else AfficherPage("Premium"); break;
                case "batterie": if (Licence.Premium) using (var w = new FenetreBatterie()) w.ShowDialog(this); else AfficherPage("Premium"); break;
            }
        }

        /// <summary>Le profil (pseudo) a changé : les accueils suivent.</summary>
        public void ProfilChange()
        {
            var a = Pages["Accueil"] as PageAccueil; if (a != null) a.RafraichirProfil();
            var s = Pages["Simple"] as PageSimple; if (s != null) s.RafraichirProfil();
        }

        /// <summary>Premier lancement de la 4.5 : « Faisons connaissance », dès qu'aucune autre fenêtre n'est ouverte.</summary>
        private void ProposerProfil()
        {
            var t = new Timer { Interval = 1500 };
            int essais = 0;
            t.Tick += (s, e) =>
            {
                if (IsDisposed || ++essais > 120) { t.Stop(); t.Dispose(); return; }
                if (Reglages.ProfilCree) { t.Stop(); t.Dispose(); return; }
                if (Application.OpenForms.Cast<Form>().Any(x => x != this && x.Visible && x.Modal) || !ContainsFocus && Form.ActiveForm != this) return;
                t.Stop(); t.Dispose();
                FenetreProfil.Ouvrir(this, true);
            };
            t.Start();
        }

        /// <summary>Exécute ce qu'indique un bouton.</summary>
        public void Executer(Cible c)
        {
            if (c == null) return;
            switch (c.Type)
            {
                case "action": LancerAction(c.Cle); break;
                case "page": AfficherPage(c.Cle); break;
                case "uri": Programmes.Ouvrir(c.Cle); break;
                case "code": if (c.Code != null) c.Code(); break;
            }
        }

        // ------------------------------------------------------------ Structure de la fenêtre
        private void ConstruireStructure()
        {
            barre = new BarreLaterale(this) { Dock = DockStyle.Left, Width = 220 };
            Image logo = null;
            try { if (Icon != null) logo = new Icon(Icon, 32, 32).ToBitmap(); } catch { }
            barre.Logo = logo;
            int xTitre = logo != null ? 60 : 20;
            barre.Controls.Add(new Etiquette("Nettoyage PC", Theme.TitreApp, Role.Blanc, xTitre - 3, 20));
            barre.Controls.Add(new Etiquette("Version " + Appli.Version, Theme.Petit, Role.BarreTexte, xTitre, 48));
            int yNav = 88;
            var bas = new List<BoutonNav>();
            foreach (var sc in Sections.Where(x => !x.Cachee))
            {
                var bn = new BoutonNav(this, sc) { Location = new Point(0, yNav), Size = new Size(220, sc.EnBas ? 36 : 42) };
                bn.Click += (s, e) => AfficherSection(((BoutonNav)s).Section.Cle);
                barre.Controls.Add(bn);
                boutonsNav[sc.Cle] = bn;
                if (sc.EnBas) bas.Add(bn); else yNav += 44;
            }
            int finNav = yNav;
            infoSysteme = new Etiquette("", Theme.Petit, Role.BarreTexte, 20, 0) { AutoSize = false, Size = new Size(190, 54), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            barre.Controls.Add(infoSysteme);
            barre.Resize += (s, e) =>
            {
                // groupe du bas collé en bas de la barre (au-dessus des infos du PC), sinon juste sous le menu
                int hBas = bas.Count * 37;
                int yInfo = barre.ClientSize.Height - 66;
                bool avecInfo = yInfo - hBas - 18 > finNav + 8;
                int y = avecInfo ? yInfo - hBas - 6 : Math.Max(finNav + 18, barre.ClientSize.Height - hBas - 10);
                barre.Separateur = y - 9;
                foreach (var b in bas) { b.Location = new Point(0, y); y += 37; }
                infoSysteme.Location = new Point(20, yInfo); infoSysteme.Visible = avecInfo;
                barre.Invalidate();
            };

            droite = new Surface(Role.Fond) { Dock = DockStyle.Fill };
            entete = new Surface(Role.Fond) { Dock = DockStyle.Top, Height = 76 };
            titrePage = new Etiquette("Accueil", Theme.TitrePage, Role.Texte, 28, 12);
            sousTitrePage = new Etiquette("", Theme.Normal, Role.Discret, 30, 46) { AutoSize = false, AutoEllipsis = true, Size = new Size(400, 22) };
            ChkSimu = new CaseACocher("Mode simulation (rien n'est modifié)") { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            ChkSimu.CheckedChanged += (s, e) => SimulationChangee();
            bTheme = new BoutonIcone { Size = new Size(38, 38), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            bTheme.Click += (s, e) => BasculerTheme();
            bRecherche = new BoutonRecherche { Size = new Size(250, 38), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            bRecherche.Click += (s, e) => OuvrirPalette();
            onglets = new BarreOnglets(this) { Location = new Point(0, 76), Height = 40, Visible = false };
            bRetourSimple = new Bouton("← Revenir à l'accueil", true, 200, 36) { Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            bRetourSimple.Click += (s, e) => AfficherPage("Simple");
            entete.Controls.AddRange(new Control[] { titrePage, sousTitrePage, ChkSimu, bTheme, bRecherche, onglets, bRetourSimple });
            entete.Resize += (s, e) =>
            {
                ChkSimu.Location = new Point(entete.ClientSize.Width - ChkSimu.Width - 28, 26);
                bTheme.Location = new Point(ChkSimu.Left - 54, 18);
                bRecherche.Location = new Point(bTheme.Left - 262, 18);
                bRecherche.Visible = bRecherche.Left > 330 && !Reglages.ModeSimple;
                sousTitrePage.Width = Math.Max(200, (bRecherche.Visible ? bRecherche.Left : bTheme.Left) - 44);
                onglets.Width = entete.ClientSize.Width;
                bRetourSimple.Location = new Point(entete.ClientSize.Width - 228, 20);
                if (Reglages.ModeSimple) bTheme.Location = new Point(bRetourSimple.Left - 54, 18);
            };
            Infobulle.Definir(bRecherche, "Rechercher une action ou une page (Ctrl+K)");

            hotePages = new Surface(Role.Fond) { Dock = DockStyle.Fill };

            panneauJournal = new Surface(Role.Carte) { Dock = DockStyle.Bottom, Height = 44 };
            barreJournal = new Surface(Role.Carte) { Dock = DockStyle.Top, Height = 44 };
            barreJournal.Paint += (s, e) => { using (var p = new Pen(Theme.P.Bordure)) e.Graphics.DrawLine(p, 0, 0, barreJournal.Width, 0); };
            statut = new Etiquette("Prêt.", Theme.Normal, Role.Discret, 18, 13) { AutoSize = false, AutoEllipsis = true, Height = 22 };
            progression = new BarreProgression { Size = new Size(250, 22), Visible = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            progression.PourcentageChange += pct => BarreTaches.Progression(Handle, pct);
            boutonArret = new Bouton("Arrêter", false, 84, 28) { RoleTexte = Role.Rouge, Enabled = false, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            boutonArret.Click += (s, e) => { if (BoiteMessage.Afficher("Arrêter l'action en cours ?\n\nUn arrêt pendant le nettoyage profond peut laisser Windows Update arrêté jusqu'au prochain redémarrage.", "Nettoyage PC", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) ArreterAction(); };
            boutonDetails = new Bouton("Afficher les détails", false, 150, 28) { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            boutonDetails.Click += (s, e) => JournalOuvert(panneauJournal.Height <= 44);
            barreJournal.Controls.AddRange(new Control[] { statut, progression, boutonArret, boutonDetails });
            barreJournal.Resize += (s, e) =>
            {
                int w = barreJournal.ClientSize.Width;
                boutonDetails.Location = new Point(w - 166, 8);
                boutonArret.Location = new Point(w - 258, 8);
                statut.Width = Math.Max(200, w - 18 - 540);
                progression.Location = new Point(w - 524, 11);
            };
            console = new ConsoleJournal { Dock = DockStyle.Fill };
            panneauJournal.Controls.Add(console);
            panneauJournal.Controls.Add(barreJournal);
            console.BringToFront();

            BandeauMaj = new BandeauMiseAJour();
            droite.Controls.Add(hotePages);
            droite.Controls.Add(BandeauMaj);
            droite.Controls.Add(panneauJournal);
            droite.Controls.Add(entete);
            // ordre d'empilement : en-tête en haut, journal en bas, bandeau de mise à jour sous l'en-tête, pages au milieu
            droite.Controls.SetChildIndex(entete, 3); droite.Controls.SetChildIndex(panneauJournal, 2);
            droite.Controls.SetChildIndex(BandeauMaj, 1); droite.Controls.SetChildIndex(hotePages, 0);
            Controls.Add(droite);
            Controls.Add(barre);
            droite.BringToFront();
        }

        public void JournalOuvert(bool ouvert)
        {
            panneauJournal.Height = ouvert ? Math.Max(220, (int)(droite.ClientSize.Height * 0.36)) : 44;
            boutonDetails.Text = ouvert ? "Masquer les détails" : "Afficher les détails";
        }

        public void Statut(string texte, bool alerte = false, Role? role = null)
        {
            statut.Text = texte;
            statut.RoleTexte = role ?? (alerte ? Role.Orange : Role.Discret);
            statut.AppliquerTheme();
        }

        public void RafraichirNav() { foreach (var b in boutonsNav.Values) b.Invalidate(); }

        /// <summary>Mode simple : un seul écran avec trois gros boutons, sans menu ni mode simulation (pour les débutants).</summary>
        public void AppliquerModeSimple(bool simple, bool enregistrer = true)
        {
            if (enregistrer) { Reglages.ModeSimple = simple; Reglages.Ecrire(); }
            SuspendLayout();
            barre.Visible = !simple;
            ChkSimu.Visible = !simple;
            bRecherche.Visible = !simple && bRecherche.Left > 330;
            if (simple && ChkSimu.Checked) ChkSimu.Checked = false;
            bTheme.Location = simple ? new Point(entete.ClientSize.Width - 282, 18) : new Point(ChkSimu.Left - 54, 18);
            ResumeLayout();
            AfficherPage(simple ? "Simple" : "Accueil");
            if (simple) Statut("Mode simple : choisis ce que tu veux faire.");
        }

        public void Journal(string ligne) { console.AjouterLigne(ligne); }
        public void ViderJournal() { console.Clear(); }

        public void Notifier(string titre, string texte, string type = "ok", Action auClic = null) { Toast.Afficher(this, titre, texte, type, auClic); }

        // ------------------------------------------------------------ Disponibilités
        private void DetecterDisponibilites()
        {
            Disponible["winget"] = InfosPc.Winget() != null;
            Disponible["defender"] = true;   // vérifié en arrière-plan après l'ouverture (requête WMI lente)
            Disponible["stockage"] = Programmes.ExisteSysteme("defrag.exe");
            Disponible["mdsched"] = Programmes.ExisteSysteme("mdsched.exe");
            Disponible["restaurer"] = Programmes.ExisteSysteme("rstrui.exe");
            if (!Natif.EstWindows) foreach (var k in Disponible.Keys.ToList()) Disponible[k] = true;   // (essais hors Windows)
        }

        public void EnregistrerBoutonAction(Bouton b) { if (!BoutonsActions.Contains(b)) BoutonsActions.Add(b); }

        /// <summary>Boutons d'actions indisponibles sur ce PC : désactivés avec la raison ; infobulle « autorisation » pour les autres.</summary>
        public void AppliquerDisponibilites()
        {
            foreach (var b in BoutonsActions)
            {
                var c = b.Cible as Cible;
                if (c == null || c.Type != "action") continue;
                ActionMoteur a; if (!Actions.TryGetValue(c.Cle, out a)) continue;
                if (a.Requiert != null && !Disponible[a.Requiert])
                {
                    string raison = Raisons[a.Requiert];
                    if (a.Requiert == "winget") { b.Text = "Installer App Installer"; b.Cible = Cible.Lien("ms-windows-store://pdp/?ProductId=9NBLGGH4NNS1"); }
                    else { b.Text = "Indisponible"; b.Enabled = false; }
                    Infobulle.Definir(b, raison);
                    continue;
                }
                if (!a.SansAdmin)
                {
                    string avant = Infobulle.T.GetToolTip(b);
                    string note = "Windows te demandera l'autorisation (bouclier) : clique sur « Oui ».";
                    if (!avant.Contains("autorisation")) Infobulle.Definir(b, string.IsNullOrEmpty(avant) ? note : avant + "\n" + note);
                }
            }
        }

        // ------------------------------------------------------------ Thème
        private bool ThemeVoulu() { return Reglages.Theme == "Auto" ? Theme.WindowsEnSombre() : Reglages.Theme != "Clair"; }

        public void AppliquerTheme(bool sombre)
        {
            Theme.Choisir(sombre);
            Theme.Appliquer(this);
            foreach (Form f in Application.OpenForms) if (f != this && !(f is Toast)) Theme.Appliquer(f);
            foreach (var p in Pages.Values) p.ThemeChange();
        }

        public void BasculerTheme()
        {
            Reglages.Theme = Theme.EstSombre ? "Clair" : "Sombre";
            Reglages.Ecrire();
            AppliquerTheme(ThemeVoulu());
        }

        public void ChangerModeTheme()
        {
            Reglages.Theme = Reglages.Theme == "Auto" ? "Sombre" : Reglages.Theme == "Sombre" ? "Clair" : "Auto";
            Reglages.Ecrire();
            AppliquerTheme(ThemeVoulu());
        }

        /// <summary>Change la couleur d'accent (Premium, sauf « Aurore ») et redessine toute l'application.</summary>
        public void ChangerAccent(string cle)
        {
            var a = Theme.Accents.FirstOrDefault(x => x.Cle == cle);
            if (a == null || a.Premium && !Licence.Premium) return;
            Reglages.Accent = cle; Reglages.Ecrire();
            Theme.ChoisirAccent(cle);
            AppliquerTheme(Theme.EstSombre);
        }

        /// <summary>La licence a changé (achat, fin de l'essai...) : couleur, menu et pages Premium suivent.</summary>
        private void LicenceChangee()
        {
            string voulu = Licence.Premium ? Reglages.Accent : "aurore";
            if (Theme.AccentActuel != voulu) { Theme.ChoisirAccent(voulu); AppliquerTheme(Theme.EstSombre); }
            RafraichirNav();
            if (Licence.Premium) Gardien.DemarrerSiBesoin(); else if (Gardien.EnCours) Gardien.Arreter();
        }

        public string LibelleTheme()
        {
            string t = Reglages.Theme == "Auto" ? "automatique" : Reglages.Theme == "Clair" ? "clair" : "sombre";
            if (Reglages.Theme == "Auto") t += " (" + (Theme.EstSombre ? "sombre" : "clair") + ")";
            return "Thème : " + t;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Natif.BarreTitreSombre(Handle, Theme.EstSombre);
        }

        // ------------------------------------------------------------ Démarrage
        private void AuDemarrage()
        {
            JournalDemarrage.Noter("Fenêtre affichée en " + Programme.Chrono.ElapsedMilliseconds + " ms.");
            Natif.ModeApplication(Theme.EstSombre);
            Theme.Appliquer(this);
            System.Threading.Tasks.Task.Run(() =>
            {
                string txt = Environment.MachineName + "\n" + InfosPc.Edition + "\n" + Langue.F("{0} Go de RAM", InfosPc.RamGo.ToString("0", Appli.Fr));
                string cpu = InfosPc.Processeur();
                try { BeginInvoke(new Action(() => { infoSysteme.Text = txt; Infobulle.Definir(infoSysteme, cpu); })); } catch { }
            });
            AppliquerDisponibilites();
            if (Natif.EstWindows)
                System.Threading.Tasks.Task.Run(() =>
                {
                    bool defender = true;
                    try { defender = InfosPc.DefenderTempsReel(); } catch { }
                    if (!defender) try { BeginInvoke(new Action(() => { Disponible["defender"] = false; AppliquerDisponibilites(); })); } catch { }
                });
            bool dejaUtilise = false;
            try { dejaUtilise = Directory.Exists(Chemins.Donnees) && Directory.GetFiles(Chemins.Donnees, "journal_*.txt").Length > 0; } catch { }
            if (!dejaUtilise)
            {
                ChkSimu.Checked = true;
                Statut("Premier lancement : le mode simulation est activé. Décoche-le en haut à droite pour agir réellement.", true);
            }
            AppliquerModeSimple(Reglages.ModeSimple, false);
            minuteurThemeAuto.Start();
            minuteurLicence.Start();
            if (Programme.DossierAutotest == null)
            {
                System.Threading.Tasks.Task.Run(() => { try { Gardien.DemarrerSiBesoin(); } catch { } });
                System.Threading.Tasks.Task.Run(() => { System.Threading.Thread.Sleep(20000); Instantanes.PrendreSiBesoin(); });
                var c0 = Programme.CommandeInitiale;
                if (c0 != null && !c0.Vide) BeginInvoke(new Action(() => TraiterCommande(c0, true)));
                else if (!Reglages.ProfilCree) ProposerProfil();
                CommandeExterne.Ecouter(c => { try { BeginInvoke(new Action(() => TraiterCommande(c, false))); } catch { } });
            }
            System.Threading.Tasks.Task.Run(() => { try { Licence.VerifierSiBesoin(); } catch { } });
            // achat lancé depuis ce PC et payé pendant que l'application était fermée : la clé s'active toute seule
            if (Programme.DossierAutotest == null && Licence.AchatEnAttente)
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        string cle, r = Licence.RecupererAchat(out cle);
                        if (r == "paid") BeginInvoke(new Action(() => Notifier("Premium activé ✔", Langue.T("Ton achat est arrivé : tous les outils Premium sont débloqués. Ta clé, pour tes autres PC :") + " " + cle, "ok", () => AfficherPage("Premium"))));
                    }
                    catch { }
                });
            // mesures gardées pour l'enquêteur (durées de démarrage) et la santé prédictive (usure des SSD)
            DonneesRecues += (cle, v) => { if (cle == "demarrage") Enqueteur.EnregistrerDemarrages(v); else if (cle == "disques") Sante.EnregistrerDisques(v); };
            Journal("Nettoyage PC " + Appli.Version + " - prêt.");
            Journal("  Le journal détaillé de chaque action s'affiche ici.");
            if (Programme.ModeSansEchec) Statut("Mode sans échec : le démarrage précédent s'est interrompu, certaines fonctions avancées sont en pause pour cette fois.", true);
            ((PageMisesAJour)Pages["MisesAJour"]).AuDemarrage();
            if (Programme.DossierAutotest != null) Autotest.Lancer(this);
        }

        private void Raccourcis(object s, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.K) { e.SuppressKeyPress = true; if (!Reglages.ModeSimple) OuvrirPalette(); return; }
            if (e.Control && e.KeyValue >= 49 && e.KeyValue <= 57)
            {
                int i = e.KeyValue - 49;
                var visibles = Sections.Where(x => !x.Cachee).ToList();
                if (i < visibles.Count && !Reglages.ModeSimple) { e.SuppressKeyPress = true; AfficherSection(visibles[i].Cle); }
                return;
            }
            if (e.KeyCode == Keys.F5 && PageActive == "Accueil") { e.SuppressKeyPress = true; ((PageAccueil)Pages["Accueil"]).MettreAJourBilan(); }
        }

        private void SimulationChangee()
        {
            var acc = Pages["Accueil"] as PageAccueil;
            if (acc != null) acc.SimulationChangee(ChkSimu.Checked);
            if (ChkSimu.Checked) Statut("Mode simulation activé : les actions montrent ce qu'elles feraient, sans rien modifier.", true);
            else Statut("Mode simulation désactivé : les actions modifient réellement le PC (avec confirmation).");
        }

        public void OuvrirPalette()
        {
            var l = new List<Commande>();
            foreach (var p in Pages.Values.Where(x => x.Cle != "Simple"))
            {
                var sc = SectionDe(p.Cle);
                string lieu = sc.Nom == p.NomOnglet ? "Page" : "Page · " + sc.Nom;
                string cle = p.Cle;
                l.Add(new Commande { Titre = p.NomOnglet, Detail = p.Sous, Lieu = lieu, Icone = p.Icone, Executer = () => AfficherPage(cle) });
            }
            var exclues = new HashSet<string> { "sauvegarde", "majwin", "finmodejeu", "analyselite", "deplanifier", "pilotes", "santedisques", "demarrage", "kit", "plantages" };
            foreach (var a in Actions.Values.Where(x => !exclues.Contains(x.Cle)))
            {
                string cle = a.Cle;
                l.Add(new Commande { Titre = a.Titre, Detail = "Lancer l'action (la simulation et les confirmations s'appliquent)", Lieu = "Action", Icone = 0xE768, Executer = () => LancerAction(cle) });
            }
            l.Add(new Commande { Titre = "Basculer le thème clair / sombre", Detail = "Change l'apparence de Nettoyage PC", Lieu = "Outil", Icone = 0xE706, Executer = BasculerTheme });
            l.Add(new Commande { Titre = "Carte de l'espace disque", Detail = "Voir ce qui remplit tes disques", Lieu = "Outil", Icone = 0xEDA2, Executer = () => new FenetreCarteEspace(this).Show(this) });
            l.Add(new Commande { Titre = "Extensions des navigateurs", Detail = "Vérifier les extensions de Chrome, Edge, Firefox...", Lieu = "Outil", Icone = 0xE774, Executer = () => { using (var w = new FenetreExtensions(this)) w.ShowDialog(this); } });
            l.Add(new Commande { Titre = "Confidentialité", Detail = "Publicité, suggestions et pistage de Windows", Lieu = "Outil", Icone = 0xE72E, Executer = () => FenetreReglagesWindows.Confidentialite(this) });
            l.Add(new Commande { Titre = "Rechercher les mises à jour de Nettoyage PC", Detail = "Vérifier s'il existe une nouvelle version", Lieu = "Outil", Icone = 0xE895, Executer = () => { AfficherPage("MisesAJour"); ((PageMisesAJour)Pages["MisesAJour"]).Verifier(true); } });
            l.Add(new Commande { Titre = "Aide à distance", Detail = "Ouvrir l'Assistance rapide de Windows", Lieu = "Outil", Icone = 0xE8D4, Executer = () => AfficherPage("Aide") });
            l.Add(new Commande { Titre = "Applications préinstallées inutiles", Detail = "Retirer les applications publicitaires de Windows", Lieu = "Outil", Icone = 0xE74D, Executer = () => { using (var w = new FenetreAppsInutiles(this)) w.ShowDialog(this); } });
            l.Add(new Commande { Titre = "Afficher le journal détaillé", Detail = "Ouvre ou ferme le journal en bas de la fenêtre", Lieu = "Outil", Icone = 0xE8A5, Executer = () => JournalOuvert(panneauJournal.Height <= 44) });
            l.Add(new Commande { Titre = "À propos de Nettoyage PC", Detail = "Version, éditeur et engagements", Lieu = "Outil", Icone = 0xE946, Executer = () => PageReglages.APropos(this) });
            l.Add(new Commande { Titre = "Informations légales", Detail = "Licence, conditions de vente, confidentialité, mentions légales", Lieu = "Outil", Icone = 0xE8A5, Executer = () => FenetreLegal.Ouvrir(this) });
            PaletteCommandes.Ouvrir(this, l);
        }

        private void Fermeture(object s, FormClosingEventArgs e)
        {
            if (ActionEnCours && !FermetureSansQuestion)
            {
                if (BoiteMessage.Afficher("Une action est en cours. Quitter quand même et l'arrêter ?", "Nettoyage PC", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) { e.Cancel = true; return; }
                if (!ArreterAction()) { e.Cancel = true; return; }
            }
            foreach (var p in Pages.Values) { try { p.Fermeture(); } catch { } }
        }
    }

    // ================================================================ Éléments dessinés de la fenêtre

    internal class BarreLaterale : Panel, IThemable
    {
        public Image Logo;
        /// <summary>Position du trait qui sépare le menu principal du groupe du bas.</summary>
        public int Separateur;
        public BarreLaterale(FenetrePrincipale f)
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            AppliquerTheme();
        }
        public void AppliquerTheme() { BackColor = Theme.P.Barre; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            int h = Math.Max(2, ClientSize.Height), w = ClientSize.Width;
            using (var b = new SolidBrush(P.Barre)) g.FillRectangle(b, 0, 0, w, h);
            using (var b = new SolidBrush(Color.FromArgb(46, P.Lueur1))) g.FillRectangle(b, w - 1, 0, 1, h);
            if (Separateur > 0)
                using (var pen = new Pen(Color.FromArgb(46, P.Lueur1))) g.DrawLine(pen, 18, Separateur, w - 18, Separateur);
            if (Logo != null)
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(Logo, 20, 24, 32, 32);
            }
        }
    }

    internal class BoutonNav : Control, IThemable
    {
        private readonly FenetrePrincipale f;
        public readonly Section Section;
        private readonly Fondu fSurvol, fActif;
        public BoutonNav(FenetrePrincipale f, Section s)
        {
            this.f = f; Section = s; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            fSurvol = new Fondu(this) { Duree = 120 };
            fActif = new Fondu(this) { Duree = 200 };
        }
        public void AppliquerTheme() { Invalidate(); }
        /// <summary>La rubrique active a changé : la sélection glisse en fondu d'un bouton à l'autre.</summary>
        public void MettreAJour() { fActif.Vers(Section.Cle == f.SectionActive ? 1 : 0); Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { fSurvol.Vers(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { fSurvol.Vers(0); base.OnMouseLeave(e); }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // fond de la barre latérale (dégradé) recopié sous le bouton
            using (var b = new SolidBrush(Theme.P.Barre)) e.Graphics.FillRectangle(b, ClientRectangle);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool actif = Section.Cle == f.SectionActive;
            float va = fActif.Valeur, vs = Math.Min(fSurvol.Valeur, 1 - va);
            if (vs > 0.01f) Theme.RemplirArrondi(g, Color.FromArgb((int)(170 * vs), P.BarreSurvol), 12, 3, Width - 24, Height - 6, 2);
            if (va > 0.01f)
            {
                int a = (int)(255 * va);
                // style Noyau : cadre fin cyan et fond teinté, qui apparaît en glissant légèrement depuis la gauche
                float dx = (1 - va) * 10;
                Theme.RemplirArrondi(g, Color.FromArgb((int)(36 * va), P.Lueur1), 12 - dx, 3, Width - 24, Height - 6, 2);
                Theme.Contour(g, Color.FromArgb((int)(120 * va), P.Lueur1), 12.5f - dx, 3.5f, Width - 25, Height - 7, 2);
            }
            Color texte = Theme.Melanger(P.BarreTexte, Color.White, Math.Max(va, fSurvol.Valeur));
            int x = 26;
            if (!Section.EnBas)
            {
                int n = f.Sections.Where(sc => !sc.Cachee && !sc.EnBas).ToList().IndexOf(Section) + 1;
                TextRenderer.DrawText(g, n.ToString("00"), Theme.Mono, new Rectangle(24, 0, 26, Height), Theme.Melanger(P.Discret, P.Lueur1, va), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
                x = 54;
            }
            string nom = Langue.T(Section.Nom).ToUpper(Langue.Culture);
            TextRenderer.DrawText(g, nom, Section.EnBas ? Theme.Petit : actif ? Theme.NavActif : Theme.Nav, new Rectangle(x, 0, Width - x - 30, Height), Section.EnBas && va < 0.5f ? Theme.Melanger(P.Discret, Color.White, fSurvol.Valeur * 0.6) : texte, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
            if (Section.Cle == "MisesAJour" && f.EtatMaj == "disponible")
                using (var b = new SolidBrush(P.Orange)) g.FillEllipse(b, Width - 36, (Height - 9) / 2f, 9, 9);
            if (Section.Cle == "Premium")
            {
                // pastille : PRO (à découvrir), jours d'essai restants, ou coche (Premium actif)
                string p = Licence.Premium ? (Licence.EnEssai ? Licence.JoursEssaiRestants + Langue.T(" j") : "✓") : "PRO";
                int lp = Math.Max(26, TextRenderer.MeasureText(p, Theme.PetitGras).Width + 12);
                var r = new Rectangle(Width - 22 - lp, (Height - 20) / 2, lp, 20);
                Theme.RemplirArrondi(g, P.Lueur1, r.X, r.Y, r.Width, r.Height, 2);
                Theme.Texte(g, p, Theme.PetitGras, r, Theme.TexteSur(P.Lueur1), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    internal class BarreOnglets : Control, IThemable, IAnimation
    {
        private readonly FenetrePrincipale f;
        private readonly List<Tuple<string, Rectangle>> rects = new List<Tuple<string, Rectangle>>();
        private string survol = "";
        // soulignement de l'onglet actif : glisse d'un onglet à l'autre
        private float sx, sl, dx, dl, cx = -1, cl;
        private long debut;
        private string sectionSoulignee = "";
        public bool Avancer(long t)
        {
            if (IsDisposed) return false;
            float p = Math.Min(1f, (t - debut) / 200f), e = Animations.Douce(p);
            sx = dx + (cx - dx) * e; sl = dl + (cl - dl) * e;
            Invalidate();
            return p < 1f;
        }
        private void Souligner(Rectangle cible, string section)
        {
            if (cible.X == cx && cible.Width == cl) return;
            bool glisser = Animations.Actives && section == sectionSoulignee && cx >= 0 && Visible;
            cx = cible.X; cl = cible.Width; sectionSoulignee = section;
            if (!glisser) { sx = cx; sl = cl; return; }
            dx = sx; dl = sl; debut = Animations.Maintenant;
            Animations.Lancer(this);
        }
        public BarreOnglets(FenetrePrincipale f)
        {
            this.f = f; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        public void AppliquerTheme() { BackColor = Theme.P.Fond; Invalidate(); }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var t = rects.FirstOrDefault(r => r.Item2.Contains(e.Location));
            string s = t == null ? "" : t.Item1;
            if (s != survol) { survol = s; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { survol = ""; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            var t = rects.FirstOrDefault(r => r.Item2.Contains(e.Location));
            if (t != null) f.AfficherPage(t.Item1);
            base.OnMouseClick(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(P.Fond);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = new Pen(P.Bordure)) g.DrawLine(p, 28, Height - 1, Width - 28, Height - 1);
            rects.Clear();
            var sc = f.Sections.FirstOrDefault(s => s.Cle == f.SectionActive);
            if (sc == null) return;
            int x = 28;
            foreach (var pg in sc.Pages)
            {
                bool actif = pg == f.PageActive;
                var police = actif ? Theme.NavActif : Theme.Nav;
                string nom = f.Pages[pg].NomOnglet;
                int l = TextRenderer.MeasureText(nom, police).Width + 28;
                var r = new Rectangle(x, 2, l, Height - 6);
                if (!actif && pg == survol) Theme.RemplirArrondi(g, P.Survol, r.X, r.Y, r.Width, r.Height, 8);
                Theme.Texte(g, nom, police, new Rectangle(x, 0, l, Height - 4), actif ? P.Texte : P.Discret, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                if (actif) Souligner(new Rectangle(x + 10, 0, l - 20, 0), sc.Cle);
                rects.Add(Tuple.Create(pg, r));
                x += l + 4;
            }
            if (sl > 1) Theme.RemplirDegrade(g, P.Lueur1, P.Lueur2, sx, Height - 4, sl, 3, 1.5f);
        }
    }

    internal class BoutonIcone : Control, IThemable
    {
        private bool survol;
        public BoutonIcone()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
        public void AppliquerTheme() { BackColor = Theme.P.Fond; Invalidate(); Infobulle.Definir(this, Theme.EstSombre ? "Passer en thème clair" : "Passer en thème sombre"); }
        protected override void OnMouseEnter(EventArgs e) { survol = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { survol = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(P.Fond); g.SmoothingMode = SmoothingMode.AntiAlias;
            if (survol) Theme.RemplirArrondi(g, P.Survol, 1, 1, Width - 2, Height - 2, 10);
            Theme.Contour(g, P.Bordure, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            if (Theme.IconesGrand != null) Theme.Icone(g, Theme.EstSombre ? 0xE706 : 0xE708, Theme.IconesGrand, ClientRectangle, P.Texte);
            else Theme.Texte(g, Theme.EstSombre ? "☼" : "☾", Theme.CarteTitre, ClientRectangle, P.Texte, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }

    internal class BoutonRecherche : Control, IThemable
    {
        private bool survol;
        public BoutonRecherche()
        {
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
        public void AppliquerTheme() { BackColor = Theme.P.Fond; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { survol = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { survol = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(P.Fond); g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Width, h = Height;
            Theme.RemplirArrondi(g, survol ? P.Survol : P.Carte, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 10);
            if (survol) Theme.ContourLumineux(g, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 10); else Theme.Contour(g, P.Bordure, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 10);
            int x = 12;
            if (Theme.Icones != null) { Theme.Icone(g, 0xE721, Theme.Icones, new Rectangle(8, 0, 22, h), P.Discret); x = 34; }
            Theme.Texte(g, "Que veux-tu faire ?", Theme.Normal, new Rectangle(x, 0, w - x - 70, h), P.Discret, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
            var tc = TextRenderer.MeasureText("Ctrl+K", Theme.PetitGras);
            Theme.RemplirArrondi(g, P.Piste, w - tc.Width - 18, 9, tc.Width + 10, 20, 6);
            Theme.Texte(g, "Ctrl+K", Theme.PetitGras, new Rectangle(w - tc.Width - 18, 9, tc.Width + 10, 20), P.Discret, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
    }
}
