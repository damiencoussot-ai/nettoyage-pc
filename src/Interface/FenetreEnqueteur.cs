// Nettoyage PC - fenêtre « Pourquoi mon PC ralentit ? » (Premium) : chronologie des changements et suspects
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreEnqueteur : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly Etiquette lblVerdict, lblDetail;
        private readonly ListeDeroulante cbDepuis = new ListeDeroulante(200), cbCourbe = new ListeDeroulante(260);
        private readonly CourbeTemps courbe = new CourbeTemps();
        private readonly ListeSombre lv = new ListeSombre();
        private readonly Bouton bCorriger, bChrono, bFermer;
        private readonly CaseACocher cSuspects;
        private ResultatEnquete res;
        private readonly int[] jours = { 3, 7, 14, 30, 90 };
        private Action<string, object> recevoir;

        public static void Ouvrir(FenetrePrincipale f)
        {
            using (var w = new FenetreEnqueteur(f)) w.ShowDialog(f);
        }

        internal FenetreEnqueteur(FenetrePrincipale f) : base("Pourquoi mon PC ralentit ?", 1000, 700, true)
        {
            F = f; MinimumSize = new Size(860, 600);
            Controls.Add(new Etiquette("Pourquoi mon PC ralentit ?", Theme.Grand, Role.Texte, 20, 14));
            Controls.Add(new Etiquette("Tout ce qui a changé sur ton PC (applications, pilotes, mises à jour, démarrage, réglages), comparé à ses performances. Tout est lu sur ton PC et y reste.",
                Theme.Normal, Role.Discret, 22, 52) { MaximumSize = new Size(940, 0) });
            Controls.Add(new Etiquette("Depuis quand est-il plus lent ?", Theme.Normal, Role.Texte, 22, 90));
            cbDepuis.Location = new Point(230, 86);
            cbDepuis.Items.AddRange(new object[] { Langue.T("depuis 3 jours"), Langue.T("depuis une semaine"), Langue.T("depuis 2 semaines"), Langue.T("depuis un mois"), Langue.T("depuis 3 mois") });
            cbDepuis.SelectedIndex = 2;
            cbDepuis.SelectedIndexChanged += (s, e) => Afficher();
            Controls.Add(cbDepuis);
            cSuspects = new CaseACocher("Seulement les suspects", 450, 89) { Checked = false };
            cSuspects.CheckedChanged += (s, e) => Afficher();
            Controls.Add(cSuspects);
            bChrono = new Bouton("Ajouter la durée des démarrages", false, 270, 34) { Location = new Point(706, 82), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            Infobulle.Definir(bChrono, "Windows chronomètre chaque démarrage. Pour lire ce chronométrage, il demande ton autorisation (une fois).");
            bChrono.Click += (s, e) => { if (!F.LancerAction("demarrage")) BoiteMessage.Info("Lecture non lancée (autorisation refusée ou action déjà en cours)."); };
            Controls.Add(bChrono);

            lblVerdict = new Etiquette("Enquête en cours...", Theme.CarteTitre, Role.AccentVif, 22, 132) { MaximumSize = new Size(940, 0) };
            lblDetail = new Etiquette("", Theme.Normal, Role.Discret, 22, 160) { MaximumSize = new Size(940, 0) };
            Controls.Add(lblVerdict); Controls.Add(lblDetail);

            courbe.Location = new Point(20, 214); courbe.Size = new Size(944, 150); courbe.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(courbe);
            cbCourbe.Location = new Point(704, 220); cbCourbe.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cbCourbe.SelectedIndexChanged += (s, e) => AfficherCourbe();
            Controls.Add(cbCourbe); cbCourbe.BringToFront();

            lv.Location = new Point(20, 376); lv.Size = new Size(944, 222); lv.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            lv.Colonne("Date", 92); lv.Colonne("Ce qui a changé", 360); lv.Colonne("Type", 130); lv.Colonne("Pourquoi c'est suspect", 340);
            lv.ColonneElastique = 3; lv.ActiverTri(); lv.ShowItemToolTips = true;
            lv.SelectedIndexChanged += (s, e) => bCorriger.Enabled = Selection() != null && Selection().Cible != "";
            lv.DoubleClick += (s, e) => Corriger();
            Controls.Add(lv);

            bCorriger = new Bouton("Voir et corriger", true, 170, 36) { Location = new Point(20, 612), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Enabled = false };
            bCorriger.Click += (s, e) => Corriger();
            var bActualiser = new Bouton("Actualiser", false, 120, 36) { Location = new Point(200, 612), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            bActualiser.Click += (s, e) => Charger();
            bFermer = new Bouton("Fermer", false, 110, 36) { Location = new Point(854, 612), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            Controls.AddRange(new Control[] { bCorriger, bActualiser, bFermer }); CancelButton = bFermer;
            ClientSize = new Size(984, 662);
            Theme.Appliquer(this);

            recevoir = (cle, v) => { if (cle == "demarrage" && !IsDisposed) BeginInvoke(new Action(Charger)); };
            F.DonneesRecues += recevoir;
            FormClosed += (s, e) => F.DonneesRecues -= recevoir;
            Shown += (s, e) => Charger();
        }

        private Changement Selection() { return lv.SelectedItems.Count > 0 ? lv.SelectedItems[0].Tag as Changement : null; }

        private void Charger()
        {
            lblVerdict.Text = "Enquête en cours..."; lblVerdict.RoleTexte = Role.AccentVif; lblVerdict.AppliquerTheme();
            System.Threading.Tasks.Task.Run(() =>
            {
                ResultatEnquete r = null;
                try { Instantanes.PrendreSiBesoin(); r = Enqueteur.Analyser(); } catch (Exception ex) { JournalDemarrage.Erreur("enquêteur", ex); }
                try { BeginInvoke(new Action(() => { res = r ?? new ResultatEnquete(); RemplirCourbes(); Afficher(); })); } catch { }
            });
        }

        private DateTime Depuis { get { return DateTime.Today.AddDays(-jours[Math.Max(0, cbDepuis.SelectedIndex)]); } }

        private void RemplirCourbes()
        {
            string avant = cbCourbe.SelectedItem as string;
            cbCourbe.Items.Clear();
            foreach (var m in res.Metriques) cbCourbe.Items.Add(Langue.T(m.Nom));
            cbCourbe.Visible = res.Metriques.Count > 1;
            if (res.Metriques.Count > 0) cbCourbe.SelectedIndex = Math.Max(0, avant != null ? cbCourbe.Items.IndexOf(avant) : 0);
            else AfficherCourbe();
            bChrono.Visible = !res.ChronoDemarrages;
        }

        private void AfficherCourbe()
        {
            var m = res == null || cbCourbe.SelectedIndex < 0 || cbCourbe.SelectedIndex >= res.Metriques.Count ? null : res.Metriques[cbCourbe.SelectedIndex];
            DateTime debut = Depuis.AddDays(-Math.Max(14, jours[Math.Max(0, cbDepuis.SelectedIndex)]));
            courbe.Debut = debut;
            if (m == null)
            {
                courbe.Titre = "Performances";
                courbe.Points = new List<Tuple<DateTime, double>>();
                courbe.VideTexte = "Pas encore de mesures : clique sur « Ajouter la durée des démarrages » pour voir l'évolution du temps de démarrage.";
            }
            else
            {
                courbe.Titre = Langue.T(m.Nom) + "   ·   " + Langue.T("traits orange : changements suspects");
                courbe.Points = m.Points; courbe.Barres = m.Comptage;
                courbe.Format = v => m.Format(v);
                courbe.VideTexte = "Pas encore assez de mesures sur cette période.";
            }
            courbe.Reperes = res == null ? new List<Tuple<DateTime, bool>>() : res.Changements.Where(c => c.Date >= debut).Select(c => Tuple.Create(c.Date, c.Suspicion >= 2)).ToList();
            courbe.Invalidate();
        }

        private void Afficher()
        {
            if (res == null) return;
            var depuis = Depuis;
            var l = res.Changements.Where(c => c.Date >= depuis && (!cSuspects.Checked || c.Suspicion >= 1)).OrderByDescending(c => c.Suspicion).ThenByDescending(c => c.Date).ToList();
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var c in l)
            {
                var it = lv.Ajouter(c, c.Date.ToString("dd/MM/yyyy"), c.Titre, Langue.T(c.Type), c.Pourquoi);
                it.ToolTipText = c.Titre + (c.Pourquoi != "" ? "\n" + c.Pourquoi : "");
                if (c.Suspicion >= 2) it.ForeColor = Theme.P.Orange;
            }
            lv.EndUpdate(); lv.AjusterColonnes();
            int niveau; string detail;
            lblVerdict.Text = Enqueteur.Verdict(res, depuis, cbDepuis.SelectedItem as string ?? "", out niveau, out detail);
            lblVerdict.RoleTexte = niveau == 0 ? Role.Vert : niveau == 2 ? Role.Orange : Role.Texte;
            lblDetail.Text = detail;
            lblVerdict.AppliquerTheme();
            int y = Math.Max(lblDetail.Bottom + 10, 196);
            courbe.Top = y; cbCourbe.Top = y + 6;
            lv.Top = courbe.Bottom + 12; lv.Height = Math.Max(120, bFermer.Top - 14 - lv.Top);
            bCorriger.Enabled = false;
            AfficherCourbe();
        }

        private void Corriger()
        {
            var c = Selection();
            if (c == null || c.Cible == "") return;
            if (c.Cible.StartsWith("page:")) { string p = c.Cible.Substring(5); Close(); F.AfficherPage(p); F.Activate(); }
            else if (c.Cible.StartsWith("uri:")) Programmes.Ouvrir(c.Cible.Substring(4));
        }
    }
}
