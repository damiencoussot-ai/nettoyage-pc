// Nettoyage PC - fenêtre « Preuve en FPS » (Premium) : mesure avant / après des images par seconde d'un jeu
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreFps : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly ListeDeroulante cbJeu = new ListeDeroulante(430), cbDuree = new ListeDeroulante(130), cbDelai = new ListeDeroulante(150);
        private readonly ZoneTexte txtLibelle = new ZoneTexte(260);
        private readonly Bouton bMesurer, bActualiser, bSupprimer;
        private readonly Etiquette lblEtat, lblComparaison;
        private readonly TuileChiffre tMoy = new TuileChiffre(), tBas1 = new TuileChiffre(), tBas01 = new TuileChiffre(), tSacc = new TuileChiffre();
        private readonly ListeSombre lv = new ListeSombre();
        private readonly System.Windows.Forms.Timer compte = new System.Windows.Forms.Timer { Interval = 1000 };
        private List<MesureFps> historique = new List<MesureFps>();
        private DateTime finPrevue;
        private bool enCours;

        public static void Ouvrir(FenetrePrincipale f) { using (var w = new FenetreFps(f)) w.ShowDialog(f); }

        internal FenetreFps(FenetrePrincipale f) : base("Preuve en FPS", 980, 740, true)
        {
            F = f; MinimumSize = new Size(880, 640); MinimizeBox = true;
            Controls.Add(new Etiquette("Preuve en FPS", Theme.Grand, Role.Texte, 20, 14));
            Controls.Add(new Etiquette("Mesure les images par seconde réelles d'un jeu (moyenne, et les 1 % et 0,1 % d'images les plus lentes, qui font les saccades), avant puis après un réglage : tu vois le vrai gain, en chiffres. Mesure faite par PresentMon, l'outil gratuit et open source d'Intel.",
                Theme.Normal, Role.Discret, 22, 52) { MaximumSize = new Size(920, 0) });
            int y = 112;
            Controls.Add(new Etiquette("1.  Lance ton jeu, puis choisis-le :", Theme.Normal, Role.Texte, 22, y + 4));
            cbJeu.Location = new Point(260, y); Controls.Add(cbJeu);
            bActualiser = new Bouton("Actualiser", false, 110, 32) { Location = new Point(700, y - 2) };
            bActualiser.Click += (s, e) => RemplirJeux();
            Controls.Add(bActualiser);
            y += 44;
            Controls.Add(new Etiquette("2.  Durée de la mesure :", Theme.Normal, Role.Texte, 22, y + 4));
            cbDuree.Location = new Point(260, y); cbDuree.Items.AddRange(new object[] { "30 s", "60 s", "2 min" }); cbDuree.SelectedIndex = 1; Controls.Add(cbDuree);
            Controls.Add(new Etiquette("Elle commence :", Theme.Normal, Role.Texte, 410, y + 4));
            cbDelai.Location = new Point(530, y); cbDelai.Items.AddRange(new object[] { Langue.T("dans 10 s"), Langue.T("dans 20 s"), Langue.T("tout de suite") }); cbDelai.SelectedIndex = 0; Controls.Add(cbDelai);
            y += 44;
            Controls.Add(new Etiquette("3.  Nom de la mesure :", Theme.Normal, Role.Texte, 22, y + 4));
            txtLibelle.Location = new Point(260, y + 1); txtLibelle.Text = Langue.T("avant"); Controls.Add(txtLibelle);
            Controls.Add(new Etiquette("(par exemple « avant », puis « après nettoyage »)", Theme.Petit, Role.Discret, 530, y + 6));
            y += 46;
            bMesurer = new Bouton("Mesurer", true, 200, 40) { Location = new Point(20, y), Font = Theme.CarteTitre };
            bMesurer.Click += (s, e) => Mesurer();
            Controls.Add(bMesurer);
            lblEtat = new Etiquette("Pendant la mesure, retourne dans le jeu et joue normalement (toujours au même endroit, pour comparer).", Theme.Normal, Role.Discret, 232, y + 10) { MaximumSize = new Size(720, 0) };
            Controls.Add(lblEtat);
            y += 56;
            int x = 20;
            foreach (var t in new[] { tMoy, tBas1, tBas01, tSacc }) { t.Location = new Point(x, y); t.Size = new Size(228, 78); Controls.Add(t); x += 238; }
            tMoy.Libelle = Langue.T("FPS moyen"); tBas1.Libelle = Langue.T("1 % les plus lents"); tBas01.Libelle = Langue.T("0,1 % les plus lents"); tSacc.Libelle = Langue.T("saccades (images très lentes)");
            y += 90;
            lblComparaison = new Etiquette("", Theme.CarteTitre, Role.Texte, 22, y) { MaximumSize = new Size(920, 0) };
            Controls.Add(lblComparaison);
            y += 34;
            lv.Location = new Point(20, y); lv.Size = new Size(944, 640 - y); lv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lv.Colonne("Date", 130); lv.Colonne("Jeu", 200); lv.Colonne("Mesure", 200); lv.Colonne("FPS moyen", 100, HorizontalAlignment.Right); lv.Colonne("1 % lents", 90, HorizontalAlignment.Right); lv.Colonne("0,1 % lents", 90, HorizontalAlignment.Right); lv.Colonne("Durée", 70, HorizontalAlignment.Right);
            lv.ColonneElastique = 2; lv.ActiverTri();
            lv.SelectedIndexChanged += (s, e) => { bSupprimer.Enabled = lv.SelectedItems.Count > 0; var m = lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as MesureFps : null; if (m != null) AfficherMesure(m); };
            Controls.Add(lv);
            bSupprimer = new Bouton("Supprimer la mesure", false, 180, 36) { Location = new Point(20, 654), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Enabled = false };
            bSupprimer.Click += (s, e) => Supprimer();
            var bFermer = new Bouton("Fermer", false, 110, 36) { Location = new Point(854, 654), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            Controls.Add(bSupprimer); Controls.Add(bFermer); CancelButton = bFermer;
            ClientSize = new Size(984, 700);
            Theme.Appliquer(this);
            compte.Tick += (s, e) => Compter();
            FormClosing += (s, e) => { if (enCours && !BoiteMessage.Question("Une mesure est en cours. Fermer quand même ? (elle s'arrêtera toute seule à la fin de la durée choisie)")) e.Cancel = true; };
            Shown += (s, e) => { RemplirJeux(); historique = Fps.Historique(); AfficherHistorique(); };
        }

        private void RemplirJeux()
        {
            var avant = cbJeu.SelectedItem as CibleJeu;
            var l = Fps.FenetresOuvertes();
            cbJeu.Items.Clear();
            foreach (var c in l) cbJeu.Items.Add(c);
            if (cbJeu.Items.Count == 0) { cbJeu.Items.Add(new CibleJeu { Libelle = Langue.T("Aucun jeu ouvert : lance-le, puis clique sur « Actualiser »") }); }
            int i = avant == null ? 0 : cbJeu.Items.Cast<CibleJeu>().ToList().FindIndex(c => c.Processus == avant.Processus);
            cbJeu.SelectedIndex = Math.Max(0, i);
        }

        private void Mesurer()
        {
            var c = cbJeu.SelectedItem as CibleJeu;
            if (c == null || c.Processus == "") { BoiteMessage.Info("Lance d'abord ton jeu, puis clique sur « Actualiser » et choisis-le dans la liste."); return; }
            int secondes = cbDuree.SelectedIndex == 0 ? 30 : cbDuree.SelectedIndex == 2 ? 120 : 60;
            int delai = cbDelai.SelectedIndex == 0 ? 10 : cbDelai.SelectedIndex == 1 ? 20 : 0;
            string libelle = txtLibelle.Text.Trim(); if (libelle == "") libelle = DateTime.Now.ToString("dd/MM HH:mm");
            bool telecharger = !Fps.OutilPresent;
            if (telecharger && !BoiteMessage.Question("Pour mesurer, Nettoyage PC utilise PresentMon, l'outil gratuit et open source d'Intel.\n\nIl va être téléchargé depuis sa page officielle (GitHub, environ 2 Mo), vérifié, puis installé dans le dossier de Nettoyage PC. Windows demandera ensuite ton autorisation pour la mesure.\n\nContinuer ?")) return;
            enCours = true; bMesurer.Enabled = false; cbJeu.Enabled = false;
            var fil = new Thread(() =>
            {
                string erreur;
                var m = Fps.Mesurer(c, secondes, delai, libelle, t => Invoquer(() => Etat(t, Role.AccentVif)), () => Invoquer(() => { finPrevue = DateTime.Now.AddSeconds(secondes + delai + 3); compte.Start(); }), out erreur);
                Invoquer(() => Fin(m, erreur));
            }) { IsBackground = true };
            fil.SetApartmentState(ApartmentState.STA);
            fil.Start();
        }

        private void Invoquer(Action a) { try { if (!IsDisposed) BeginInvoke(a); } catch { } }

        private void Compter()
        {
            int reste = (int)Math.Ceiling((finPrevue - DateTime.Now).TotalSeconds);
            if (reste > 0) Etat(Langue.F("Mesure en cours : retourne dans le jeu et joue normalement. Encore {0} s environ.", reste), Role.AccentVif);
            else Etat("Calcul des résultats...", Role.AccentVif);
        }

        private void Fin(MesureFps m, string erreur)
        {
            compte.Stop(); enCours = false; bMesurer.Enabled = true; cbJeu.Enabled = true;
            if (IsDisposed) return;
            try { Activate(); } catch { }
            if (erreur != null) { Etat(erreur, Role.Orange); return; }
            historique.Insert(0, m);
            Fps.Enregistrer(historique);
            Etat(Langue.F("Mesure « {0} » terminée : {1} images en {2} s.", m.Libelle, m.Images, m.Secondes), Role.Vert);
            AfficherHistorique();
            AfficherMesure(m);
            if (txtLibelle.Text.Trim().Equals(Langue.T("avant"), StringComparison.OrdinalIgnoreCase)) txtLibelle.Text = Langue.T("après");
        }

        private void Etat(string texte, Role r) { lblEtat.Text = texte; lblEtat.RoleTexte = r; lblEtat.AppliquerTheme(); }

        private void AfficherMesure(MesureFps m)
        {
            Func<double, string> f = v => v.ToString(v < 100 ? "0.0" : "0", Appli.Fr);
            tMoy.Valeur = f(m.Moyenne); tBas1.Valeur = f(m.Bas1); tBas01.Valeur = f(m.Bas01); tSacc.Valeur = m.Saccades.ToString("0");
            tSacc.Couleur = m.Saccades > m.Secondes ? Role.Orange : Role.Texte;
            foreach (var t in new[] { tMoy, tBas1, tBas01, tSacc }) t.Invalidate();
            int niveau;
            lblComparaison.Text = Fps.Comparaison(historique, m, out niveau);
            lblComparaison.RoleTexte = niveau == 0 ? Role.Discret : niveau == 2 ? Role.Vert : niveau == 1 ? Role.Orange : Role.Texte;
            lblComparaison.AppliquerTheme();
        }

        private void AfficherHistorique()
        {
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var m in historique)
            {
                DateTime d; string date = DateTime.TryParseExact(m.Date, "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out d) ? d.ToString("dd/MM/yyyy HH:mm") : m.Date;
                lv.Ajouter(m, date, m.Jeu, m.Libelle, m.Moyenne.ToString("0.0", Appli.Fr), m.Bas1.ToString("0.0", Appli.Fr), m.Bas01.ToString("0.0", Appli.Fr), m.Secondes + " s");
            }
            lv.EndUpdate(); lv.AjusterColonnes();
            if (historique.Count > 0 && tMoy.Valeur == "—") AfficherMesure(historique[0]);
        }

        private void Supprimer()
        {
            var m = lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as MesureFps : null;
            if (m == null || !BoiteMessage.Question(Langue.F("Supprimer la mesure « {0} » ?", m.Libelle))) return;
            historique.Remove(m); Fps.Enregistrer(historique); AfficherHistorique();
        }
    }
}
