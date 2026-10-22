// Nettoyage PC - « Faisons connaissance » (4.5) : profil sur ce PC (pseudo, couleur, briefing vocal).
// Affiché au premier lancement, puis depuis Réglages > Mon profil. Rien n'est envoyé sur Internet.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreProfil : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly ZoneTexte zPseudo = new ZoneTexte(340);
        private readonly ListeDeroulante cbQuand = new ListeDeroulante(330), cbVoix = new ListeDeroulante(330);
        private readonly Etiquette lblExemple;
        private readonly Bouton bEcouter;
        private readonly List<string> voix = new List<string>();
        private static readonly string[] Quand = { "jour", "ouverture", "besoin", "jamais" };

        private bool lireApres;

        public static void Ouvrir(FenetrePrincipale f, bool premierLancement)
        {
            bool lire;
            using (var w = new FenetreProfil(f, premierLancement)) { w.ShowDialog(f); lire = w.lireApres; }
            Voix.Arreter();
            // premier profil : le briefing du jour est lu tout de suite
            var accueil = f.Pages["Accueil"] as PageAccueil;
            if (lire && accueil != null) accueil.LireBriefing(false);
        }

        internal FenetreProfil(FenetrePrincipale f, bool premierLancement) : base(premierLancement ? "Faisons connaissance" : "Mon profil", 780, 640)
        {
            F = f;
            var r = f.Reglages;
            Controls.Add(new Etiquette(premierLancement ? "// PREMIER LANCEMENT" : "// MON PROFIL", Theme.Mono, Role.AccentVif, 26, 18));
            Controls.Add(new Etiquette("Faisons connaissance", Theme.Grand, Role.Texte, 24, 36));
            Controls.Add(new Etiquette("Ton profil reste sur ce PC : pas de mot de passe, rien n'est envoyé sur Internet.", Theme.Normal, Role.Discret, 26, 76) { MaximumSize = new Size(720, 0) });

            int y = 116;
            Controls.Add(new Etiquette("TON PSEUDO", Theme.Mono, Role.Discret, 26, y));
            zPseudo.Location = new Point(26, y + 20); zPseudo.Font = new Font(Theme.Grand.FontFamily, 14); zPseudo.MaxLength = 24;
            zPseudo.Text = r.Pseudo;
            zPseudo.TextChanged += (s, e) => ActualiserExemple();
            Controls.Add(zPseudo);
            Controls.Add(new Etiquette("C'est le nom que l'application affiche et prononce.", Theme.Petit, Role.Discret, 26, y + 60));

            y = 206;
            Controls.Add(new Etiquette("TA COULEUR", Theme.Mono, Role.Discret, 26, y));
            Controls.Add(new SelecteurAccent(f) { Location = new Point(22, y + 18) });
            if (!Licence.Premium) Controls.Add(new Etiquette("Les autres couleurs font partie de Premium.", Theme.Petit, Role.Discret, 300, y + 30));

            y = 290;
            Controls.Add(new Etiquette("BRIEFING VOCAL À L'OUVERTURE", Theme.Mono, Role.Discret, 26, y));
            Controls.Add(new Etiquette("Quand ?", Theme.Normal, Role.Texte, 26, y + 24));
            cbQuand.Location = new Point(26, y + 46);
            cbQuand.Items.AddRange(new object[] { Langue.T("Une fois par jour"), Langue.T("À chaque ouverture"), Langue.T("Seulement s'il y a quelque chose à faire"), Langue.T("Jamais (texte seulement)") });
            cbQuand.SelectedIndex = Math.Max(0, Array.IndexOf(Quand, r.Briefing));
            Controls.Add(cbQuand);
            Controls.Add(new Etiquette("Voix", Theme.Normal, Role.Texte, 400, y + 24));
            cbVoix.Location = new Point(400, y + 46);
            var liste = Voix.Disponible ? Voix.Lister() : new List<Tuple<string, string>>();
            cbVoix.Items.Add(Langue.T("Automatique (langue de l'application)")); voix.Add("");
            foreach (var v in liste) { cbVoix.Items.Add(v.Item1); voix.Add(v.Item1); }
            cbVoix.SelectedIndex = Math.Max(0, voix.IndexOf(r.Voix));
            cbVoix.Enabled = liste.Count > 0;
            Controls.Add(cbVoix);

            y = 384;
            var carte = new Carte(728, 120, true) { Location = new Point(24, y) };
            bEcouter = new Bouton("▶  Écouter", true, 120, 36) { Location = new Point(18, 42) };
            bEcouter.Click += (s, e) => Ecouter();
            carte.Controls.Add(bEcouter);
            lblExemple = new Etiquette("", Theme.Normal, Role.Texte, 154, 18) { AutoSize = false, Size = new Size(556, 86) };
            carte.Controls.Add(lblExemple);
            Controls.Add(carte);
            Controls.Add(new Etiquette(liste.Count > 0 ? "La voix est celle de Windows : elle fonctionne sans Internet. Le briefing ne fait rien tout seul, il propose."
                : "La voix de Windows n'est pas disponible sur ce PC : le briefing s'affichera seulement en texte.", Theme.Petit, Role.Discret, 26, y + 128) { MaximumSize = new Size(720, 0) });

            var bPasser = new Bouton(premierLancement ? "Passer cette étape" : "Annuler", false, 180, 40) { Location = new Point(24, 578) };
            bPasser.Click += (s, e) => { if (premierLancement) { F.Reglages.ProfilCree = true; F.Reglages.Briefing = "jamais"; F.Reglages.Ecrire(); } Close(); };
            var bOk = new Bouton(premierLancement ? "Créer mon profil" : "Enregistrer", true, 220, 40) { Location = new Point(532, 578), Font = Theme.CarteTitre };
            bOk.Click += (s, e) => Enregistrer();
            Controls.Add(bPasser); Controls.Add(bOk);
            AcceptButton = bOk;
            ClientSize = new Size(776, 636);
            Theme.Appliquer(this);
            ActualiserExemple();
            Shown += (s, e) => zPseudo.Focus();
        }

        private string Exemple()
        {
            var accueil = F.Pages.ContainsKey("Accueil") ? F.Pages["Accueil"] as PageAccueil : null;
            var pts = accueil != null && accueil.Points.Count > 0 ? accueil.Points : new List<PointBilan>
            {
                new PointBilan { Etat = "attention", Parole = Langue.T("créer un point de restauration") }
            };
            int score = accueil != null && accueil.ScoreActuel >= 0 ? accueil.ScoreActuel : 93;
            return Profil.TexteBriefing(Profil.Nettoyer(zPseudo.Text), score, pts);
        }

        private void ActualiserExemple() { lblExemple.Text = "« " + Exemple() + " »"; }

        private void Ecouter()
        {
            if (Voix.EnCours) { Voix.Arreter(); bEcouter.Text = "▶  Écouter"; return; }
            string nom = cbVoix.SelectedIndex > 0 ? voix[cbVoix.SelectedIndex] : "";
            if (!Voix.Lire(Exemple(), nom)) { BoiteMessage.Info("La voix de Windows n'est pas disponible sur ce PC."); return; }
            bEcouter.Text = "■  Arrêter";
            var t = new Timer { Interval = 400 };
            t.Tick += (s, e) => { if (IsDisposed || !Voix.EnCours) { t.Stop(); t.Dispose(); if (!IsDisposed) bEcouter.Text = "▶  Écouter"; } };
            t.Start();
        }

        private void Enregistrer()
        {
            var r = F.Reglages;
            r.Pseudo = Profil.Nettoyer(zPseudo.Text);
            r.Briefing = Quand[Math.Max(0, cbQuand.SelectedIndex)];
            r.Voix = cbVoix.SelectedIndex > 0 ? voix[cbVoix.SelectedIndex] : "";
            bool premier = !r.ProfilCree;
            r.ProfilCree = true;
            if (premier && r.Briefing != "jamais") r.DernierBriefing = DateTime.Now.ToString("yyyy-MM-dd");
            r.Ecrire();
            F.ProfilChange();
            var accueil = F.Pages["Accueil"] as PageAccueil;
            lireApres = premier && r.Briefing != "jamais" && accueil != null && accueil.Points.Count > 0 && Programme.DossierAutotest == null;
            DialogResult = DialogResult.OK;
        }
    }
}
