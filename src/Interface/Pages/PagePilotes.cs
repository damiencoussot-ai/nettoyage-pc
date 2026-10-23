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
    internal class PagePilotes : Page
    {
        private readonly Carte carteGpu = new Carte(600, 132, true) { Location = new Point(28, 4) };
        private readonly Etiquette lblGpuNom, lblGpuPilote, lblMaj;
        private readonly Bouton bOutil, bSite, bChercher, bInstaller, bWu, bApps;
        private readonly ListeSombre lv = new ListeSombre { CheckBoxes = true, MultiSelect = false, ShowItemToolTips = true };
        private List<CarteGraphique> gpus = new List<CarteGraphique>();
        private CarteGraphique principale;
        private OutilPilote outil = new OutilPilote();
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
            bSite.Click += (s, e) => { if (outil.Site != "") Programmes.Ouvrir(outil.Site); };
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
                bool vieux;
                lblGpuPilote.Text = Pilotes.ResumeGpu(gpus, out vieux);
                outil = OutilPilote.Pour(principale.Marque);
                lblGpuPilote.RoleTexte = vieux ? Role.Orange : Role.Discret; lblGpuPilote.AppliquerTheme();
                bOutil.Visible = true; bSite.Visible = outil.Site != "";
                bOutil.Text = outil.Bouton;
            });
        }

        private void OuvrirOutil()
        {
            if (outil.Exe != "")
            {
                Programmes.Ouvrir(outil.Exe);
                BoiteMessage.Info(outil.Explication, "Nettoyage PC - pilote graphique");
            }
            else if (outil.Site != "") Programmes.Ouvrir(outil.Site);
        }

        // ------------------------------------------------------------ Windows Update
        private void Rechercher()
        {
            if (recherche) return;
            recherche = true; bChercher.Enabled = false; bInstaller.Enabled = false; lv.Items.Clear();
            InfoMaj("Recherche auprès de Windows Update... (cela peut prendre une ou deux minutes)", Role.AccentVif);
            string erreur = null;
            EnFond(() => Pilotes.ChercherMaj(out erreur), l =>
            {
                recherche = false; bChercher.Enabled = true;
                if (l == null) { InfoMaj(Langue.T("Windows Update ne répond pas (service arrêté ou pas de connexion Internet).") + " " + erreur, Role.Rouge); return; }
                maj = l;
                AfficherMaj();
            });
        }

        private ClassementMaj Classer(MajWindows m) { return Pilotes.Classer(m, gpus, bOutil.Text); }

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
            string msg;
            var args = Pilotes.ArgumentsInstallation(coches, out msg);
            F.LancerAction("majwin", args, msg);
        }
    }
}
