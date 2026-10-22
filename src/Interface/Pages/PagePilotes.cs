// Nettoyage PC - page Windows et pilotes : carte graphique, Windows Update (tri intelligent)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class MajWindows
    {
        public string Id = "", Titre = "", Classe = "", Fournisseur = "", Kb = "";
        public double Taille;
        public bool Facultative, Pilote, Redemarrage;
        public DateTime? DatePilote;
        public List<string> Categories = new List<string>();
    }

    internal class PagePilotes : Page
    {
        private readonly Carte carteGpu = new Carte(600, 132, true) { Location = new Point(28, 4) };
        private readonly Etiquette lblGpuNom, lblGpuPilote, lblMaj;
        private readonly Bouton bOutil, bSite, bChercher, bInstaller, bWu, bApps;
        private readonly ListeSombre lv = new ListeSombre { CheckBoxes = true, MultiSelect = false, ShowItemToolTips = true };
        private List<CarteGraphique> gpus = new List<CarteGraphique>();
        private CarteGraphique principale;
        private string outilExe = "", outilNom = "", outilSite = "";
        private bool recherche, majListe;
        private List<MajWindows> maj = new List<MajWindows>();

        public PagePilotes(FenetrePrincipale f) : base(f, "Pilotes", "Windows et pilotes", "Pilote de la carte graphique, mises à jour de Windows et des pilotes : tu choisis ce qui est installé.", 0xE7F4, "Windows et pilotes")
        {
            AutoScroll = false;
            carteGpu.Controls.Add(new Etiquette("Carte graphique", Theme.CarteTitre, Role.Texte, 20, 18));
            lblGpuNom = new Etiquette("Recherche de la carte graphique...", Theme.Grand, Role.Texte, 20, 42);
            lblGpuPilote = new Etiquette("", Theme.Normal, Role.Discret, 22, 80) { MaximumSize = new Size(560, 0) };
            bOutil = new Bouton("Mettre à jour le pilote", true, 250, 36) { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            bSite = new Bouton("Site officiel du fabricant", false, 250, 32) { Anchor = AnchorStyles.Top | AnchorStyles.Right };
            bOutil.Click += (s, e) => OuvrirOutil();
            bSite.Click += (s, e) => { if (outilSite != "") Programmes.Ouvrir(outilSite); };
            carteGpu.Controls.AddRange(new Control[] { lblGpuNom, lblGpuPilote, bOutil, bSite });
            Controls.Add(carteGpu);
            Titre("Mises à jour Windows et pilotes", 28, 150);
            bChercher = NouveauBouton("Rechercher", false, 120, 32, 28, 180, Rechercher);
            bInstaller = NouveauBouton("Installer la sélection", true, 230, 32, 156, 180, Installer);
            bInstaller.Enabled = false;
            bWu = NouveauBouton("Ouvrir Windows Update", false, 180, 32, 394, 180, () => Programmes.Ouvrir("ms-settings:windowsupdate"));
            bApps = BoutonAction("Mettre à jour les applications", false, 220, 32, 582, 180, "majapps");
            lv.Colonne("Mise à jour", 430); lv.Colonne("Type", 140); lv.Colonne("Taille", 80, HorizontalAlignment.Right); lv.Colonne("Conseil", 330);
            Controls.Add(lv);
            lblMaj = new Etiquette("Clique sur « Rechercher » : Nettoyage PC demande à Windows Update la liste des mises à jour et pilotes disponibles, et coche ce qui est recommandé.", Theme.Normal, Role.Discret) { AutoSize = false, AutoEllipsis = true };
            Controls.Add(lblMaj);
            Resize += (s, e) =>
            {
                int w = ClientSize.Width - 56;
                carteGpu.Width = w;
                bOutil.Location = new Point(w - 270, 22); bSite.Location = new Point(w - 270, 70);
                int hl = Math.Max(160, ClientSize.Height - 222 - 44);
                lv.Location = new Point(28, 222); lv.Size = new Size(w, hl);
                lblMaj.Location = new Point(28, 222 + hl + 10); lblMaj.Size = new Size(w, 22);
                lblGpuPilote.MaximumSize = new Size(Math.Max(300, w - 320), 0);
            };
            lv.ItemChecked += (s, e) => { if (majListe) return; e.Item.ForeColor = e.Item.Checked ? Theme.P.Texte : Theme.P.Discret; ResumeMaj(); };
            f.ActionFinie += cle => { if (cle == "majwin" || cle == "majapps") { AfficherGpu(); if (Chargee) Rechercher(); } };
        }

        public override void PremiereFois() { AfficherGpu(); Rechercher(); }

        private void InfoMaj(string t, Role r = Role.Discret) { lblMaj.Text = t; lblMaj.RoleTexte = r; lblMaj.AppliquerTheme(); }

        public void AfficherGpu()
        {
            EnFond(InfosPc.CartesGraphiques, l =>
            {
                gpus = l ?? new List<CarteGraphique>();
                if (gpus.Count == 0)
                {
                    lblGpuNom.Text = "Aucune carte graphique détectée";
                    lblGpuPilote.Text = "Écran virtuel ou machine virtuelle : les pilotes sont gérés par Windows Update.";
                    bOutil.Visible = bSite.Visible = false;
                    return;
                }
                principale = gpus[0];
                lblGpuNom.Text = principale.Nom;
                string txt = "Pilote " + principale.VersionPilote;
                bool vieux = false;
                if (principale.DatePilote.HasValue)
                {
                    int mois = (int)Math.Floor((DateTime.Now - principale.DatePilote.Value).TotalDays / 30.4);
                    txt += " du " + principale.DatePilote.Value.ToString("dd/MM/yyyy");
                    if (mois >= 1) txt += " (il y a " + mois + " mois)";
                    vieux = mois >= 4;
                }
                OutilFabricant(principale.Marque);
                txt += vieux ? ". Les fabricants sortent un nouveau pilote presque chaque mois : une version plus récente existe sûrement (meilleures performances dans les jeux, corrections)." : ". Pilote récent.";
                if (gpus.Count > 1) txt += "  ·  Autre(s) : " + string.Join(", ", gpus.Skip(1).Select(x => x.Nom));
                lblGpuPilote.Text = txt;
                lblGpuPilote.RoleTexte = vieux ? Role.Orange : Role.Discret; lblGpuPilote.AppliquerTheme();
                bOutil.Visible = true; bSite.Visible = outilSite != "";
                bOutil.Text = outilExe != "" ? "Ouvrir " + outilNom : outilNom != "" ? "Télécharger " + outilNom : "Pilotes facultatifs de Windows";
            });
        }

        /// <summary>Outil officiel du fabricant (le plus sûr pour les pilotes graphiques).</summary>
        private void OutilFabricant(string marque)
        {
            outilExe = ""; outilNom = ""; outilSite = "";
            string pf = Chemins.ProgramFiles;
            switch (marque)
            {
                case "AMD":
                    outilNom = "AMD Software"; outilSite = "https://www.amd.com/fr/support/download/drivers.html";
                    if (File.Exists(Path.Combine(pf, @"AMD\CNext\CNext\RadeonSoftware.exe"))) outilExe = Path.Combine(pf, @"AMD\CNext\CNext\RadeonSoftware.exe");
                    break;
                case "NVIDIA":
                    outilNom = "NVIDIA App"; outilSite = "https://www.nvidia.com/fr-fr/software/nvidia-app/";
                    foreach (var c in new[] { Path.Combine(pf, @"NVIDIA Corporation\NVIDIA app\CEF\NVIDIA app.exe"), Path.Combine(pf, @"NVIDIA Corporation\NVIDIA GeForce Experience\NVIDIA GeForce Experience.exe") })
                        if (File.Exists(c)) { outilExe = c; if (c.Contains("GeForce")) outilNom = "GeForce Experience"; break; }
                    break;
                case "Intel":
                    outilNom = "Intel Driver & Support Assistant"; outilSite = "https://www.intel.fr/content/www/fr/fr/support/detect.html";
                    break;
                default: outilSite = "ms-settings:windowsupdate-optionalupdates"; break;
            }
        }

        private void OuvrirOutil()
        {
            if (outilExe != "")
            {
                Programmes.Ouvrir(outilExe);
                BoiteMessage.Info(outilNom + " s'ouvre.\n\nClique sur « Rechercher des mises à jour » (ou la section Pilotes), puis installe la version proposée.\nC'est l'outil officiel du fabricant : il installe le bon pilote pour ta carte, sans risque. L'écran peut clignoter pendant l'installation.", "Nettoyage PC - pilote graphique");
            }
            else if (outilSite != "") Programmes.Ouvrir(outilSite);
        }

        // ------------------------------------------------------------ Windows Update
        private void Rechercher()
        {
            if (recherche) return;
            recherche = true; bChercher.Enabled = false; bInstaller.Enabled = false; lv.Items.Clear();
            InfoMaj("Recherche auprès de Windows Update... (cela peut prendre une ou deux minutes)", Role.AccentVif);
            string erreur = null;
            EnFond(() =>
            {
                var l = new List<MajWindows>();
                try
                {
                    dynamic session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session"));
                    session.ClientApplicationID = "Nettoyage PC";
                    dynamic res = session.CreateUpdateSearcher().Search("IsInstalled=0 and IsHidden=0");
                    int n = res.Updates.Count;
                    for (int i = 0; i < n; i++)
                    {
                        dynamic u = res.Updates.Item(i);
                        var m = new MajWindows { Id = u.Identity.UpdateID, Titre = u.Title, Taille = (double)u.MaxDownloadSize, Facultative = u.BrowseOnly, Pilote = (int)u.Type == 2 };
                        try { int nc = u.Categories.Count; for (int k = 0; k < nc; k++) m.Categories.Add((string)u.Categories.Item(k).CategoryID); } catch { }
                        try { int nk = u.KBArticleIDs.Count; var kbs = new List<string>(); for (int k = 0; k < nk; k++) kbs.Add("KB" + (string)u.KBArticleIDs.Item(k)); m.Kb = string.Join(", ", kbs); } catch { }
                        if (m.Pilote) { try { m.Classe = u.DriverClass; m.DatePilote = (DateTime)u.DriverVerDate; m.Fournisseur = u.DriverProvider; } catch { } }
                        try { m.Redemarrage = (int)u.InstallationBehavior.RebootBehavior > 0; } catch { }
                        l.Add(m);
                    }
                }
                catch (Exception ex) { erreur = ex.Message; return null; }
                return l;
            }, l =>
            {
                recherche = false; bChercher.Enabled = true;
                if (l == null) { InfoMaj(Langue.T("Windows Update ne répond pas (service arrêté ou pas de connexion Internet).") + " " + erreur, Role.Rouge); return; }
                maj = l;
                AfficherMaj();
            });
        }

        private class Classement { public string Type = "Windows", Conseil = "Recommandé"; public bool Coche; public int Ordre = 3; }

        /// <summary>Tri intelligent : ce qui est recommandé est coché, le reste est expliqué.</summary>
        private Classement Classer(MajWindows m)
        {
            var c = new Classement { Coche = !m.Facultative };
            string t = m.Titre;
            Func<string, bool> re = motif => Regex.IsMatch(t, motif, RegexOptions.IgnoreCase);
            if (m.Categories.Contains("3689bdc8-b205-4af4-8d4a-a63924c5e9d5") || re("feature update|mise à jour des fonctionnalités|Windows 1\\d, version"))
            { c.Type = "Nouvelle version"; c.Coche = false; c.Conseil = "Gros changement : à faire depuis Windows Update, quand tu as le temps"; c.Ordre = 6; }
            else if (m.Categories.Contains("e0789628-ce08-4437-be74-2495b842f43b") || re("defender|security intelligence|veille de sécurité|KB2267602"))
            { c.Type = "Antivirus"; c.Coche = true; c.Conseil = "Recommandé : protection à jour"; c.Ordre = 0; }
            else if (m.Pilote && (m.Classe == "Display" || re("display|radeon|geforce|nvidia|intel.*graphics|graphique")))
            {
                c.Type = "Pilote graphique"; c.Ordre = 4;
                var noms = new Dictionary<string, string> { { "AMD", "AMD|Advanced Micro Devices|ATI " }, { "NVIDIA", "NVIDIA" }, { "Intel", "Intel" } };
                var installe = gpus.FirstOrDefault(g => noms.ContainsKey(g.Marque) && Regex.IsMatch(t + " " + m.Fournisseur, noms[g.Marque], RegexOptions.IgnoreCase));
                if (installe != null && installe.DatePilote.HasValue && m.DatePilote.HasValue && m.DatePilote <= installe.DatePilote) { c.Coche = false; c.Conseil = "Plus ancien que ton pilote actuel : à ignorer"; }
                else if (installe != null && (installe.Marque == "AMD" || installe.Marque == "NVIDIA")) { c.Coche = false; c.Conseil = "Préfère le bouton « " + bOutil.Text + " » en haut (pilote complet)"; }
                else { c.Coche = !m.Facultative; c.Conseil = "Plus récent que ton pilote actuel"; }
            }
            else if (m.Pilote) { c.Type = "Pilote"; c.Ordre = 5; if (m.Facultative) { c.Coche = false; c.Conseil = "Facultatif : utile seulement si un appareil pose problème"; } else c.Conseil = "Recommandé par Windows"; }
            else if (m.Categories.Contains("0fa1201d-4330-4fa8-8ae9-b877473b6441") || re("sécurité|security|cumulative"))
            {
                c.Type = "Sécurité"; c.Coche = true; c.Conseil = "Important : corrige des failles"; c.Ordre = 1;
                if (re("preview|aperçu|préliminaire")) { c.Type = "Windows (aperçu)"; c.Coche = false; c.Conseil = "Version d'essai : pas nécessaire"; c.Ordre = 5; }
            }
            else if (m.Categories.Contains("e6cf1350-c01b-414d-a61f-263d14d133b4")) { c.Type = "Critique"; c.Coche = true; c.Conseil = "Important"; c.Ordre = 1; }
            else if (re("preview|aperçu|préliminaire")) { c.Type = "Windows (aperçu)"; c.Coche = false; c.Conseil = "Version d'essai : pas nécessaire"; c.Ordre = 5; }
            else if (m.Facultative) { c.Coche = false; c.Conseil = "Facultative"; c.Ordre = 5; }
            else if (re("\\.NET")) { c.Type = ".NET"; c.Ordre = 2; }
            if (m.Redemarrage && c.Coche) c.Conseil += " · redémarrage";
            return c;
        }

        private void AfficherMaj()
        {
            majListe = true;
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var x in maj.Select(m => new { M = m, C = Classer(m) }).OrderBy(x => x.C.Ordre).ThenBy(x => x.M.Titre))
            {
                var it = lv.Ajouter(x.M, x.M.Titre, x.C.Type, x.M.Taille > 0 ? Format.Taille(x.M.Taille) : "", x.C.Conseil);
                it.SubItems[2].Tag = x.M.Taille;
                it.Checked = x.C.Coche; it.ToolTipText = x.M.Kb;
                if (!x.C.Coche) it.ForeColor = Theme.P.Discret;
            }
            lv.EndUpdate();
            majListe = false;
            ResumeMaj();
        }

        private void ResumeMaj()
        {
            var coches = lv.Items.Cast<ListViewItem>().Where(i => i.Checked).ToList();
            double total = coches.Sum(i => ((MajWindows)i.Tag).Taille);
            if (lv.Items.Count == 0) InfoMaj("Windows est à jour : aucune mise à jour ni pilote en attente. ✔", Role.Vert);
            else InfoMaj(lv.Items.Count + " mise(s) à jour disponible(s) ; " + coches.Count + " cochée(s) (" + Format.Taille(total) + "). Coche ou décoche, puis « Installer la sélection ».");
            bInstaller.Enabled = coches.Count > 0 && !F.ActionEnCours;
            bInstaller.Text = coches.Count > 0 ? "Installer la sélection (" + coches.Count + ")" : "Installer la sélection";
        }

        private void Installer()
        {
            var coches = lv.Items.Cast<ListViewItem>().Where(i => i.Checked).Select(i => (MajWindows)i.Tag).ToList();
            if (coches.Count == 0) { BoiteMessage.Info("Coche au moins une mise à jour dans la liste."); return; }
            double total = coches.Sum(m => m.Taille);
            var ids = coches.Select(m => m.Id).Where(id => Regex.IsMatch(id, "^[0-9a-fA-F-]{36}$")).ToList();
            string msg = "Installer " + coches.Count + " mise(s) à jour (" + Format.Taille(total) + ") ?\n\n" + string.Join("\n", coches.Take(8).Select(m => "- " + m.Titre));
            if (coches.Count > 8) msg += "\n- ... et " + (coches.Count - 8) + " autre(s)";
            msg += "\n\nUn point de restauration est créé d'abord. Le téléchargement et l'installation passent par Windows Update et peuvent prendre du temps : laisse le PC allumé.";
            if (coches.Any(m => m.Redemarrage)) msg += "\n\nUn redémarrage sera nécessaire à la fin (tu choisis quand).";
            F.LancerAction("majwin", new[] { "--maj-windows", "--seul", "--point-restauration", "--ids-maj", string.Join(",", ids) }, msg);
        }
    }
}
