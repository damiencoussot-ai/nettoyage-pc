// Nettoyage PC - page Mises à jour : vérification sur GitHub, installation vérifiée (SHA256), nouveautés
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NettoyagePC
{
    /// <summary>Texte enrichi en lecture seule aux couleurs du thème.</summary>
    internal class TexteRiche : RichTextBox, IThemable
    {
        public Role RoleFond = Role.Carte;
        public TexteRiche() { ReadOnly = true; BorderStyle = BorderStyle.None; DetectUrls = false; Font = Theme.Normal; AppliquerTheme(); }
        public void AppliquerTheme()
        {
            BackColor = Theme.P.Couleur(RoleFond); ForeColor = Theme.P.Texte;
            if (IsHandleCreated) Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer");
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer"); }
        public void Ajouter(string texte, Font police, Color couleur)
        {
            SelectionStart = TextLength; SelectionLength = 0; SelectionFont = police; SelectionColor = couleur;
            AppendText(texte + "\n");
        }
    }

    internal class PageMisesAJour : Page
    {
        // Nouveautés intégrées (affichées même sans Internet) : liste partagée avec la version 5 (Noyau/Nouveautes.cs)
        public static readonly List<KeyValuePair<string, string[]>> Nouveautes = JournalVersions.Liste;

        private readonly Carte carte = new Carte(600, 168, true) { Location = new Point(28, 4) };
        private readonly Etiquette lblEtat, lblDetail;
        private readonly CaseACocher chkAuto;
        private readonly Bouton bVerifier, bInstaller;
        private readonly Lien lienGitHub;
        private readonly Carte cadre = new Carte(600, 200) { Padding = new Padding(18, 14, 10, 10) };
        private readonly TexteRiche rt = new TexteRiche { Dock = DockStyle.Fill };
        private List<VersionPubliee> versions = new List<VersionPubliee>();
        private VersionPubliee info;
        private string etat = "inconnu", erreur = "";
        private DateTime? dateVerif;
        private bool enCours, installation;

        public PageMisesAJour(FenetrePrincipale f) : base(f, "MisesAJour", "Mises à jour", "Version installée, nouveautés et mises à jour de Nettoyage PC.", 0xE896, "Nettoyage PC")
        {
            AutoScroll = false;
            carte.Controls.Add(new Etiquette("Nettoyage PC " + Appli.Version, Theme.Grand, Role.Texte, 22, 16));
            lblEtat = new Etiquette("", Theme.CarteTitre, Role.Discret, 24, 60);
            lblDetail = new Etiquette("", Theme.Petit, Role.Discret, 24, 90) { MaximumSize = new Size(560, 0) };
            chkAuto = new CaseACocher("Rechercher les mises à jour à chaque ouverture de Nettoyage PC", 24, 128) { Checked = f.Reglages.VerifAuto };
            chkAuto.CheckedChanged += (s, e) => { F.Reglages.VerifAuto = chkAuto.Checked; F.Reglages.Ecrire(); };
            bVerifier = new Bouton("Rechercher les mises à jour", false, 220, 36);
            bVerifier.Click += (s, e) => Verifier(true);
            bInstaller = new Bouton("Installer la mise à jour", true, 220, 36) { Font = Theme.CarteTitre, Visible = false };
            bInstaller.Click += (s, e) => Installer();
            carte.Controls.AddRange(new Control[] { lblEtat, lblDetail, chkAuto, bVerifier, bInstaller });
            Controls.Add(carte);
            Titre("Nouveautés et historique des versions", 28, 188);
            lienGitHub = new Lien("Voir toutes les versions sur GitHub", 0, 190);
            lienGitHub.LinkClicked += (s, e) => Programmes.Ouvrir("https://github.com/" + MisesAJour.Depot + "/releases");
            Controls.Add(lienGitHub);
            cadre.Controls.Add(rt);
            Controls.Add(cadre);
            Resize += (s, e) =>
            {
                int w = ClientSize.Width - 56;
                carte.Width = w;
                bVerifier.Location = new Point(w - 244, 20); bInstaller.Location = new Point(w - 244, 64);
                lienGitHub.Location = new Point(ClientSize.Width - 28 - lienGitHub.Width, 190);
                cadre.Location = new Point(28, 218); cadre.Size = new Size(w, Math.Max(120, ClientSize.Height - 218 - 20));
            };
            Afficher();
        }

        public override void ThemeChange() { Afficher(); }

        /// <summary>Au démarrage : message de bienvenue après une mise à jour, puis vérification automatique (une fois par jour).</summary>
        public void AuDemarrage()
        {
            // Les nouveautés ne sont présentées qu'UNE fois par version, même si une ancienne version (3.x) relancée
            // remet « DerniereVersionLancee » en arrière : la version vue est aussi notée dans %LOCALAPPDATA%\NettoyagePC.
            string avant = F.Reglages.DerniereVersionLancee;
            string marque = System.IO.Path.Combine(Chemins.Local, "nouveautes-vues.txt");
            string vueLocale = "";
            try { if (System.IO.File.Exists(marque)) vueLocale = System.IO.File.ReadAllText(marque).Trim(); } catch { }
            bool dejaVue = MisesAJour.Comparer(F.Reglages.NouveautesVues, Appli.Version) >= 0 || MisesAJour.Comparer(vueLocale, Appli.Version) >= 0;
            bool montrer = !dejaVue && avant != "" && MisesAJour.Comparer(avant, Appli.Version) < 0 && Programme.DossierAutotest == null;
            if (avant != Appli.Version && Programme.DossierAutotest == null)
                System.Threading.Tasks.Task.Run(() => Programmes.RafraichirIcones());   // nouvelle version : nouveau logo possible
            if (avant != Appli.Version || F.Reglages.NouveautesVues != Appli.Version)
            {
                F.Reglages.DerniereVersionLancee = Appli.Version;
                if (MisesAJour.Comparer(F.Reglages.NouveautesVues, Appli.Version) < 0) F.Reglages.NouveautesVues = Appli.Version;
                F.Reglages.Ecrire();
            }
            if (MisesAJour.Comparer(vueLocale, Appli.Version) < 0)
                try { System.IO.Directory.CreateDirectory(Chemins.Local); System.IO.File.WriteAllText(marque, Appli.Version); } catch { }
            if (montrer)
            {
                // petite notification dans le coin (rien à fermer) ; un clic ouvre les nouveautés
                // toutes les versions depuis celle d'avant (4.4 -> 4.5.1 : nouveautés de 4.5 et de 4.5.1), la plus récente d'abord
                var liste = Nouveautes.Where(x => MisesAJour.Comparer(x.Key, avant) > 0 && MisesAJour.Comparer(x.Key, Appli.Version) <= 0)
                    .Take(4).SelectMany(x => x.Value).ToArray();
                var t = new Timer { Interval = 1500 };
                t.Tick += (s, e) =>
                {
                    t.Stop(); t.Dispose();
                    F.Notifier("Nettoyage PC " + Appli.Version + " est installé", "Mise à jour réussie (" + avant + " → " + Appli.Version + "). Clique ici pour voir les nouveautés.", "ok", () => AfficherBienvenue(avant, liste));
                };
                t.Start();
            }
            // à chaque ouverture (et toutes les 6 heures si l'application reste ouverte) : pas besoin d'aller dans cette page
            F.BandeauMaj.BInstaller.Click += (s, e) => Installer();
            F.BandeauMaj.BNouveautes.Click += (s, e) => F.AfficherPage("MisesAJour");
            if (F.Reglages.VerifAuto && Programme.DossierAutotest == null && !Programme.ModeSansEchec)
            {
                Verifier(false);
                var periodique = new Timer { Interval = 6 * 3600 * 1000 };
                periodique.Tick += (s, e) => { if (F.Reglages.VerifAuto && etat != "disponible") Verifier(false); };
                periodique.Start();
            }
        }

        /// <summary>Le bandeau du haut suit l'état de la mise à jour.</summary>
        private void MettreAJourBandeau()
        {
            if (etat == "disponible" && info != null)
            {
                var notes = Regex.Split(info.Notes ?? "", "\r?\n").Select(n => n.Trim().TrimStart('-', '•', '*', ' ')).Where(n => n != "" && !n.StartsWith("Nettoyage PC") && !Regex.IsMatch(n, @"^\s*SHA256")).ToList();
                var integre = Nouveautes.FirstOrDefault(x => x.Key == info.Version);
                if (notes.Count == 0 && integre.Value != null) notes = integre.Value.ToList();
                string resume = notes.Count > 0 ? string.Join(" · ", notes.Take(3)) : Langue.T("Une nouvelle version de Nettoyage PC est prête à être installée (moins d'une minute, tes réglages sont conservés).");
                F.BandeauMaj.Montrer(info.Version, resume);
            }
            else if (etat == "telechargement") F.BandeauMaj.Installation(Langue.F("Téléchargement de la version {0}...", info == null ? "" : info.Version));
            else if (etat == "installation") F.BandeauMaj.Installation(Langue.T("Installation en cours : Nettoyage PC va se rouvrir tout seul..."));
            else F.BandeauMaj.Cacher(false);
        }

        /// <summary>(Essais automatiques) Simule une nouvelle version disponible, sans Internet.</summary>
        public void SimulerPourAutotest(bool disponible)
        {
            if (disponible) { info = new VersionPubliee { Version = "9.9", Notes = "Nettoyage PC 9.9\n- Une nouveauté de démonstration\n- Une autre amélioration\nSHA256 : ...", UrlSetup = "https://exemple" }; etat = "disponible"; }
            else { info = null; etat = "inconnu"; }
            F.EtatMaj = etat; F.RafraichirNav(); Afficher();
        }

        public void Verifier(bool manuel)
        {
            if (enCours || installation) return;
            enCours = true; etat = "verification"; Afficher();
            string err = null;
            EnFond(() => { try { return MisesAJour.Verifier(); } catch (Exception ex) { err = ex.Message; return null; } }, l =>
            {
                enCours = false; dateVerif = DateTime.Now;
                if (l == null) { etat = "erreur"; erreur = err ?? ""; }
                else
                {
                    versions = l;
                    var dernier = l.FirstOrDefault();
                    if (dernier != null && MisesAJour.Comparer(dernier.Version, Appli.Version) > 0 && dernier.UrlSetup != "")
                    {
                        etat = "disponible"; info = dernier;
                        F.Statut(Langue.F("Nouvelle version {0} disponible : clique sur « Installer maintenant » en haut de la fenêtre.", dernier.Version), true);
                    }
                    else { etat = "ajour"; info = null; }
                }
                F.EtatMaj = etat == "disponible" ? "disponible" : etat;
                F.RafraichirNav();
                Afficher();
                if (manuel && etat == "erreur") BoiteMessage.Attention("Impossible de vérifier les mises à jour (connexion Internet ?).\n\n" + erreur);
                if (!manuel && etat == "disponible") F.Notifier(Langue.F("Nouvelle version {0}", info.Version), Langue.T("Une mise à jour de Nettoyage PC est disponible : « Installer maintenant » en haut de la fenêtre."), "ok", () => F.AfficherPage("MisesAJour"));
            });
        }

        private void Afficher()
        {
            var P = Theme.P;
            string detail = dateVerif.HasValue ? "Dernière vérification : " + dateVerif.Value.ToString("dd/MM/yyyy HH:mm") : "";
            detail += (detail != "" ? "   ·   " : "") + Langue.F("Source officielle : {0}", "github.com/" + MisesAJour.Depot);
            bInstaller.Visible = false; bVerifier.Enabled = !enCours && !installation;
            Role r = Role.Discret;
            switch (etat)
            {
                case "verification": lblEtat.Text = "Recherche des mises à jour..."; r = Role.AccentVif; break;
                case "disponible":
                    lblEtat.Text = "Nouvelle version disponible : " + info.Version; r = Role.Orange;
                    bInstaller.Text = "Installer la version " + info.Version; bInstaller.Visible = true; bInstaller.Enabled = !installation;
                    break;
                case "ajour": lblEtat.Text = "✔  Tu as la dernière version."; r = Role.Vert; break;
                case "erreur": lblEtat.Text = "Vérification impossible (pas de connexion Internet ?)"; r = Role.Rouge; break;
                case "installation": lblEtat.Text = "Installation en cours : Nettoyage PC va se rouvrir tout seul..."; r = Role.AccentVif; break;
                case "telechargement": lblEtat.Text = "Téléchargement de la version " + (info == null ? "" : info.Version) + "..."; r = Role.AccentVif; break;
                default: lblEtat.Text = "Clique sur « Rechercher les mises à jour »."; break;
            }
            lblEtat.RoleTexte = r; lblEtat.AppliquerTheme();
            lblDetail.Text = detail;
            MettreAJourBandeau();

            rt.SuspendLayout(); rt.Clear();
            if (versions.Count > 0)
            {
                foreach (var v in versions.Take(10))
                {
                    string titre = Langue.T("Version") + " " + v.Version + (v.Date.HasValue ? "   —   " + v.Date.Value.ToString("dd/MM/yyyy") : "");
                    if (MisesAJour.Comparer(v.Version, Appli.Version) == 0) titre += "   " + Langue.T("(installée)");
                    else if (MisesAJour.Comparer(v.Version, Appli.Version) > 0) titre += "   " + Langue.T("(nouvelle)");
                    rt.Ajouter(titre, Theme.CarteTitre, P.Texte);
                    var notes = Regex.Split(v.Notes ?? "", "\r?\n").Where(n => n.Trim() != "" && !Regex.IsMatch(n, @"^\s*SHA256")).ToList();
                    var integre = Nouveautes.FirstOrDefault(x => x.Key == v.Version);
                    // les notes publiées sur GitHub sont en français : en anglais / espagnol, les nouveautés traduites de l'application passent avant
                    if (integre.Value != null && (notes.Count == 0 || Langue.Code != "fr")) notes = integre.Value.Select(n => "- " + Langue.T(n)).ToList();
                    if (notes.Count == 0) notes.Add(Langue.T("(pas de description)"));
                    foreach (var n in notes) rt.Ajouter("   " + n.Trim(), Theme.Normal, P.Discret);
                    rt.Ajouter("", Theme.Petit, P.Discret);
                }
            }
            else
                foreach (var kv in Nouveautes)
                {
                    rt.Ajouter(Langue.T("Version") + " " + kv.Key + (kv.Key == Appli.Version ? "   " + Langue.T("(installée)") : ""), Theme.CarteTitre, P.Texte);
                    foreach (var n in kv.Value) rt.Ajouter("   - " + Langue.T(n), Theme.Normal, P.Discret);
                    rt.Ajouter("", Theme.Petit, P.Discret);
                }
            rt.SelectionStart = 0; rt.ScrollToCaret();
            rt.ResumeLayout();
        }

        /// <summary>Téléchargement, contrôle d'intégrité, installation silencieuse ; l'application se relance seule.</summary>
        private void Installer()
        {
            var v = info;
            if (v == null) return;
            if (F.ActionEnCours) { BoiteMessage.Info("Une action est en cours : attends qu'elle se termine avant de mettre à jour."); return; }
            if (!BoiteMessage.Question("Installer Nettoyage PC " + v.Version + " maintenant ?\n\nL'application va se fermer puis se rouvrir toute seule (moins d'une minute). Ton historique et tes réglages sont conservés.", "Nettoyage PC - mise à jour")) return;
            installation = true; etat = "telechargement"; Afficher();
            F.Statut("Téléchargement de la version " + v.Version + "...", false, Role.AccentVif);
            string err = null, empreinte = null;
            EnFond(() => { try { string h; var fi = MisesAJour.Telecharger(v, out h); empreinte = h; return fi; } catch (Exception ex) { err = ex.Message; return null; } }, fichier =>
            {
                if (fichier == null)
                {
                    installation = false; etat = "disponible"; Afficher(); F.Statut("Mise à jour impossible.", true);
                    BoiteMessage.Attention("La mise à jour n'a pas pu être téléchargée :\n\n" + err);
                    return;
                }
                etat = "installation"; Afficher(); F.Statut("Installation de la mise à jour...", false, Role.AccentVif);
                Gardien.Arreter();   // le gardien garde le programme ouvert : il se relancera avec la nouvelle version
                // (seule une copie placée dans Program Files et revérifiée là-bas est lancée, avec l'autorisation de Windows)
                if (!Elevation.Commande(90, "installer-maj", fichier, empreinte))
                {
                    installation = false; etat = "disponible"; Afficher(); F.Statut("Mise à jour non installée.", true);
                    Gardien.DemarrerSiBesoin();
                    BoiteMessage.Attention("La mise à jour n'a pas été installée : l'autorisation de Windows a été refusée, ou le fichier n'a pas passé la vérification de sécurité.");
                    return;
                }
                try { System.IO.File.Delete(fichier); System.IO.Directory.Delete(System.IO.Path.GetDirectoryName(fichier)); } catch { }
                F.FermetureSansQuestion = true;
                F.Close();
            });
        }

        /// <summary>Fenêtre « Bienvenue » après une mise à jour : bandeau lumineux, nouveautés, un bouton.</summary>
        private void AfficherBienvenue(string avant, string[] liste)
        {
            using (var f = new FenetreTheme("Nettoyage PC " + Appli.Version, 640, 500))
            {
                f.Paint += (s, e) =>
                {
                    var g = e.Graphics; int w = f.ClientSize.Width;
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var br = new LinearGradientBrush(new Rectangle(0, 0, w, 120), Theme.P.Lueur1, Theme.P.Lueur2, LinearGradientMode.ForwardDiagonal)) g.FillRectangle(br, 0, 0, w, 120);
                    using (var voile = new SolidBrush(Color.FromArgb(130, 0, 0, 0))) g.FillRectangle(voile, 0, 0, w, 120);
                    using (var ft = new Font("Segoe UI Semibold", 22)) Theme.Texte(g, Langue.T("Bienvenue dans la version") + " " + Appli.Version, ft, new Rectangle(26, 22, w - 40, 44), Color.White, TextFormatFlags.Left);
                    Theme.Texte(g, Langue.T("Mise à jour installée :") + " " + avant + " → " + Appli.Version, Theme.Normal, new Rectangle(30, 74, w - 40, 22), Color.White, TextFormatFlags.Left);
                };
                var txt = new TexteRiche { RoleFond = Role.Fond, Location = new Point(28, 140), Size = new Size(570, 250), Font = new Font("Segoe UI", 10.5f) };
                txt.AppliquerTheme();
                foreach (var n in liste) { txt.Ajouter("✦  " + Langue.T(n), txt.Font, Theme.P.Texte); txt.Ajouter("", Theme.Petit, Theme.P.Texte); }
                var ok = new Bouton("C'est parti", true, 180, 40) { Font = Theme.CarteTitre, Location = new Point(420, 404), DialogResult = DialogResult.OK };
                var astuce = new Etiquette("Astuce : Ctrl+K pour tout trouver.", Theme.Petit, Role.Discret, 30, 416);
                f.Controls.AddRange(new Control[] { txt, ok, astuce });
                f.AcceptButton = ok;
                f.ShowDialog(F);
            }
        }
    }
}
