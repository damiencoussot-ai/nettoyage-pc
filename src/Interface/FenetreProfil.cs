// Nettoyage PC - « Faisons connaissance » : profil sur ce PC (pseudo, couleur, mode simple ou complet).
// Affiché au premier lancement, puis depuis Réglages > Mon profil. Rien n'est envoyé sur Internet.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreProfil : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly bool premier;
        private readonly ZoneTexte zPseudo = new ZoneTexte(340);
        private readonly CarteChoix cSimple, cComplet;

        public static void Ouvrir(FenetrePrincipale f, bool premierLancement)
        {
            using (var w = new FenetreProfil(f, premierLancement)) w.ShowDialog(f);
        }

        internal FenetreProfil(FenetrePrincipale f, bool premierLancement) : base(premierLancement ? "Faisons connaissance" : "Mon profil", 780, 560)
        {
            F = f; premier = premierLancement;
            var r = f.Reglages;
            Controls.Add(new Etiquette(premierLancement ? "// PREMIER LANCEMENT" : "// MON PROFIL", Theme.Mono, Role.AccentVif, 26, 18));
            Controls.Add(new Etiquette("Faisons connaissance", Theme.Grand, Role.Texte, 24, 36));
            Controls.Add(new Etiquette("Ton profil reste sur ce PC : pas de mot de passe, rien n'est envoyé sur Internet.", Theme.Normal, Role.Discret, 26, 76) { MaximumSize = new Size(720, 0) });

            int y = 116;
            Controls.Add(new Etiquette("TON PSEUDO", Theme.Mono, Role.Discret, 26, y));
            zPseudo.Location = new Point(26, y + 20); zPseudo.Font = new Font(Theme.Grand.FontFamily, 14); zPseudo.MaxLength = 24;
            zPseudo.Text = r.Pseudo;
            Controls.Add(zPseudo);
            Controls.Add(new Etiquette("C'est le nom que l'application affiche pour t'accueillir.", Theme.Petit, Role.Discret, 26, y + 60));

            y = 206;
            Controls.Add(new Etiquette("TA COULEUR", Theme.Mono, Role.Discret, 26, y));
            Controls.Add(new SelecteurAccent(f) { Location = new Point(22, y + 18) });
            if (!Licence.Premium) Controls.Add(new Etiquette("Les autres couleurs font partie de Premium.", Theme.Petit, Role.Discret, 300, y + 30));

            y = 290;
            Controls.Add(new Etiquette("COMMENT VEUX-TU UTILISER NETTOYAGE PC ?", Theme.Mono, Role.Discret, 26, y));
            cSimple = new CarteChoix("Simple", "Trois gros boutons : nettoyer, vérifier, appeler à l'aide. Idéal pour débuter.", "POUR DÉBUTER") { Location = new Point(24, y + 24), Size = new Size(356, 112) };
            cComplet = new CarteChoix("Complet", "Toutes les rubriques et les outils : nettoyage, vitesse, sécurité, matériel, Premium.", "") { Location = new Point(396, y + 24), Size = new Size(356, 112) };
            cSimple.Choisi = r.ModeSimple; cComplet.Choisi = !r.ModeSimple;
            cSimple.Click += (s, e) => { cSimple.Choisi = true; cComplet.Choisi = false; };
            cComplet.Click += (s, e) => { cSimple.Choisi = false; cComplet.Choisi = true; };
            Controls.Add(cSimple); Controls.Add(cComplet);
            Controls.Add(new Etiquette("Tu peux changer à tout moment dans Réglages.", Theme.Petit, Role.Discret, 26, y + 146));

            var bPasser = new Bouton(premierLancement ? "Passer cette étape" : "Annuler", false, 180, 40) { Location = new Point(24, 496) };
            bPasser.Click += (s, e) => { if (premierLancement) { F.Reglages.ProfilCree = true; F.Reglages.Ecrire(); } Close(); };
            var bOk = new Bouton(premierLancement ? "C'est parti" : "Enregistrer", true, 220, 40) { Location = new Point(532, 496), Font = Theme.CarteTitre };
            bOk.Click += (s, e) => Enregistrer();
            Controls.Add(bPasser); Controls.Add(bOk);
            AcceptButton = bOk;
            ClientSize = new Size(776, 556);
            Theme.Appliquer(this);
            Shown += (s, e) => zPseudo.Focus();
        }

        private void Enregistrer()
        {
            var r = F.Reglages;
            r.Pseudo = Profil.Nettoyer(zPseudo.Text);
            r.ProfilCree = true;
            r.Ecrire();
            F.ProfilChange();
            if (cSimple.Choisi != r.ModeSimple) F.AppliquerModeSimple(cSimple.Choisi);
            DialogResult = DialogResult.OK;
        }
    }

    /// <summary>Carte à choisir (comme un bouton radio), avec coins lumineux quand elle est choisie.</summary>
    internal class CarteChoix : Control, IThemable
    {
        private readonly string titre, texte, badge;
        private bool choisi, survol;
        public bool Choisi { get { return choisi; } set { choisi = value; Invalidate(); } }

        public CarteChoix(string titre, string texte, string badge)
        {
            this.titre = Langue.T(titre); this.texte = Langue.T(texte); this.badge = badge == "" ? "" : Langue.T(badge);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint | ControlStyles.Selectable, true);
            Cursor = Cursors.Hand; TabStop = true;
            AccessibleRole = AccessibleRole.RadioButton; AccessibleName = this.titre;
        }

        public void AppliquerTheme() { Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { survol = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { survol = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Width, h = Height;
            using (var fond = new SolidBrush(choisi ? Color.FromArgb(Theme.EstSombre ? 40 : 26, P.Accent) : P.Carte)) g.FillRectangle(fond, 0, 0, w, h);
            using (var bord = new Pen(choisi ? P.Accent : survol ? Color.FromArgb(160, P.Accent) : P.Bordure, choisi ? 2 : 1)) g.DrawRectangle(bord, choisi ? 1 : 0, choisi ? 1 : 0, w - (choisi ? 3 : 1), h - (choisi ? 3 : 1));
            if (choisi) Theme.Crochets(g, P.Lueur1, 0, 0, w, h, 14, 2);
            // pastille du choix
            int r = 9, cx = 22, cy = 28;
            using (var p = new Pen(choisi ? P.Accent : P.Discret, 2)) g.DrawEllipse(p, cx - r, cy - r, 2 * r, 2 * r);
            if (choisi) using (var b = new SolidBrush(P.Accent)) g.FillEllipse(b, cx - 5, cy - 5, 10, 10);
            Theme.Texte(g, titre, Theme.CarteTitre, new Rectangle(42, 14, w - 60, 28), P.Texte, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            if (badge != "")
            {
                var tb = TextRenderer.MeasureText(badge, Theme.Mono);
                var rb = new Rectangle(w - tb.Width - 30, 18, tb.Width + 14, 20);
                using (var b = new SolidBrush(Color.FromArgb(Theme.EstSombre ? 50 : 30, P.Vert))) g.FillRectangle(b, rb);
                Theme.Texte(g, badge, Theme.Mono, rb, P.Vert, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            Theme.Texte(g, texte, Theme.Normal, new Rectangle(42, 46, w - 60, h - 54), P.Discret, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            if (Focused && ShowFocusCues) using (var p = new Pen(Color.FromArgb(150, P.Lueur1)) { DashStyle = DashStyle.Dot }) g.DrawRectangle(p, 4, 4, w - 9, h - 9);
        }
    }
}
