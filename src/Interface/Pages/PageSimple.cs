// Nettoyage PC - mode simple : trois gros boutons et l'état du PC en une phrase (pour les débutants)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    /// <summary>Gros bouton carré : icône, titre, explication.</summary>
    internal class GrosBouton : Control, IThemable
    {
        public int Glyphe; public string Titre, Detail; public bool Principal;
        private bool survol, appui;
        public GrosBouton(int glyphe, string titre, string detail, bool principal)
        {
            Glyphe = glyphe; Titre = titre; Detail = detail; Principal = principal; Cursor = Cursors.Hand; Size = new Size(280, 210);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
        }
        public void AppliquerTheme() { Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { survol = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { survol = false; appui = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { appui = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { appui = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) OnClick(EventArgs.Empty); base.OnKeyDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Width, h = Height;
            if (Principal)
            {
                Color c1 = P.Lueur1, c2 = P.Lueur2;
                if (appui) { c1 = Theme.Melanger(c1, Color.Black, 0.15); c2 = Theme.Melanger(c2, Color.Black, 0.15); }
                else if (survol) { c1 = Theme.Melanger(c1, Color.White, 0.15); c2 = Theme.Melanger(c2, Color.White, 0.15); }
                Theme.RemplirDegrade(g, c1, c2, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 16);
            }
            else
            {
                Theme.RemplirArrondi(g, appui ? Theme.Melanger(P.Survol, P.Fond, 0.4) : survol ? P.Survol : P.Carte, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 16);
                if (survol || Focused) Theme.ContourLumineux(g, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 16, 1.6f); else Theme.Contour(g, P.Bordure, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 16);
            }
            Color surAccent = Theme.TexteSur(Theme.Melanger(P.Lueur1, P.Lueur2, 0.5));
            Color texte = Principal ? surAccent : P.Texte, discret = Principal ? Color.FromArgb(225, surAccent) : P.Discret;
            using (var fi = Theme.IconesTresGrand != null ? new Font(Theme.IconesTresGrand.FontFamily, 34) : null)
                if (fi != null) Theme.Icone(g, Glyphe, fi, new Rectangle(0, 22, w, 64), Principal ? surAccent : P.AccentVif);
            using (var ft = new Font("Segoe UI Semibold", 15))
                Theme.Texte(g, Titre, ft, new Rectangle(10, 96, w - 20, 36), texte, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            using (var fd = new Font("Segoe UI", 10.5f))
                Theme.Texte(g, Detail, fd, new Rectangle(18, 134, w - 36, h - 142), discret, TextFormatFlags.HorizontalCenter | TextFormatFlags.Top | TextFormatFlags.WordBreak);
            if (Focused && ShowFocusCues) using (var s = new Pen(Color.FromArgb(150, P.Lueur1)) { DashStyle = DashStyle.Dot }) g.DrawPath(s, Theme.CheminArrondi(4, 4, w - 9, h - 9, 13));
        }
    }

    internal class PageSimple : Page
    {
        private readonly Etiquette bonjour, etat, detail;
        private readonly GrosBouton bNettoyer, bVerifier, bAide;
        private readonly FlowLayoutPanel problemes = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
        private readonly Lien lienComplet, lienArnaque;
        private readonly Bouton bBriefing;
        private readonly Timer minuteurVoix = new Timer { Interval = 500 };
        private PageAccueil accueil;

        public PageSimple(FenetrePrincipale f) : base(f, "Simple", "Accueil", "Tout ce qu'il faut, en trois boutons.", 0xE80F)
        {
            bonjour = Texte(Profil.Accueil(f.Reglages.Pseudo), 40, 18, Role.Texte, new Font(Theme.Grand.FontFamily, 24));
            etat = Texte("Je regarde l'état de ton PC...", 42, 70, Role.AccentVif, new Font("Segoe UI Semibold", 14));
            detail = Texte("", 44, 104, Role.Discret, new Font("Segoe UI", 11));
            bNettoyer = new GrosBouton(0xE74D, "Nettoyer mon PC", "Supprime les fichiers inutiles. Tes documents et tes photos ne sont jamais touchés.", true);
            bVerifier = new GrosBouton(0xE9D9, "Vérifier mon PC", "Regarde si tout va bien : disques, antivirus, mises à jour, espace libre.", false);
            bAide = new GrosBouton(0xE8D4, "Appeler à l'aide", "Un proche voit ton écran et t'aide depuis chez lui, en toute sécurité.", false);
            bNettoyer.Click += (s, e) => Nettoyer();
            bVerifier.Click += (s, e) => Verifier();
            bAide.Click += (s, e) => Aide();
            Controls.AddRange(new Control[] { bNettoyer, bVerifier, bAide, problemes });
            problemes.BackColor = Theme.P.Fond;
            lienComplet = new Lien("Afficher toutes les options (mode complet)", 0, 0);
            lienComplet.LinkClicked += (s, e) =>
            {
                if (BoiteMessage.Question("Passer au mode complet ?\n\nToutes les rubriques et les options avancées seront affichées. Tu pourras revenir au mode simple dans Réglages.")) F.AppliquerModeSimple(false);
            };
            Controls.Add(lienComplet);
            lienArnaque = new Lien("On t'appelle pour « réparer » ton PC ? Clique ici", 0, 0);
            lienArnaque.LinkClicked += (s, e) => FenetreBouclier.Ouvrir(F, true);
            Controls.Add(lienArnaque);
            bBriefing = new Bouton("▶  Écouter", false, 150, 40) { Font = new Font(Theme.Normal.FontFamily, 10.5f) };
            Infobulle.Definir(bBriefing, "Nettoyage PC te dit à voix haute l'état de ton PC et ce qu'il te propose.");
            bBriefing.Click += (s, e) =>
            {
                if (Voix.EnCours) { Voix.Arreter(); bBriefing.Text = "▶  Écouter"; minuteurVoix.Stop(); return; }
                if (accueil == null) return;
                accueil.LireBriefing(true);
                if (Voix.EnCours) { bBriefing.Text = "■  Arrêter"; minuteurVoix.Start(); }
            };
            minuteurVoix.Tick += (s, e) => { if (!Voix.EnCours) { minuteurVoix.Stop(); bBriefing.Text = "▶  Écouter"; } };
            Controls.Add(bBriefing);
            Resize += (s, e) => Disposer();
            f.ActionFinie += cle => { if (F.Reglages.ModeSimple && cle == "standard") Verifier(false); };
        }

        public void RafraichirProfil() { bonjour.Text = Profil.Accueil(F.Reglages.Pseudo); }

        public override void ThemeChange() { problemes.BackColor = Theme.P.Fond; AfficherProblemes(); }

        private void Disposer()
        {
            int w = ClientSize.Width, l = 280, ecart = 24;
            int total = 3 * l + 2 * ecart, x = Math.Max(40, (w - total) / 2);
            if (w < total + 80) { l = Math.Max(200, (w - 80 - 2 * ecart) / 3); x = 40; }
            int y = 150;
            foreach (var b in new[] { bNettoyer, bVerifier, bAide }) { b.Size = new Size(l, 210); b.Location = new Point(x, y); x += l + ecart; }
            bonjour.Left = bNettoyer.Left; etat.Left = bNettoyer.Left + 2; detail.Left = bNettoyer.Left + 4;
            bBriefing.Location = new Point(bAide.Right - bBriefing.Width, 26);
            detail.MaximumSize = new Size(Math.Max(300, 3 * l + 2 * ecart), 0);
            problemes.Location = new Point(bNettoyer.Left, y + 234);
            lienComplet.Location = new Point(Math.Max(20, w - lienComplet.Width - 30), Math.Max(problemes.Bottom + 16, ClientSize.Height - 34));
            lienArnaque.Location = new Point(bNettoyer.Left, lienComplet.Top);
            if (lienArnaque.Right > lienComplet.Left - 20) lienArnaque.Top = lienComplet.Top - 28;
        }

        public override void PremiereFois()
        {
            accueil = F.Pages["Accueil"] as PageAccueil;
            if (accueil != null)
            {
                accueil.BilanPret += () => { if (!IsDisposed) AfficherProblemes(); };
                var h = accueil.Handle;   // la page Accueil fait le bilan même cachée
                if (!accueil.Chargee) { accueil.Chargee = true; accueil.PremiereFois(); }
                else if (accueil.Points.Count > 0) AfficherProblemes();
            }
        }

        private void Nettoyer()
        {
            F.LancerAction("standard", null, "Nettoyer le PC maintenant ?\n\nLes fichiers inutiles (temporaires, caches) sont supprimés et la corbeille est vidée. Tes documents, photos et vidéos ne sont jamais touchés.\n\nFerme ton navigateur Internet pour un meilleur résultat.");
        }

        private void Verifier(bool message = true)
        {
            if (accueil == null) return;
            etat.Text = "Je regarde l'état de ton PC..."; etat.RoleTexte = Role.AccentVif; etat.AppliquerTheme();
            accueil.MettreAJourBilan();
        }

        private void Aide()
        {
            var aide = F.Pages["Aide"] as PageAide;
            if (!BoiteMessage.Question("Quelqu'un que tu connais va pouvoir voir ton écran pour t'aider.\n\n1. Appelle cette personne. Elle ouvre l'Assistance rapide sur son PC et te donne un code.\n2. Clique sur « Oui » ici, tape le code, puis accepte.\n\nAttention : n'accepte jamais si c'est quelqu'un qui t'a appelé sans que tu le demandes (faux technicien, fausse banque).\n\nOuvrir l'Assistance rapide ?", "Appeler à l'aide")) return;
            if (aide != null) aide.OuvrirAssistance();
        }

        private void AfficherProblemes()
        {
            if (accueil == null) return;
            var l = accueil.Points.Where(p => p.Etat != "ok").ToList();
            int nbP = l.Count(p => p.Etat == "probleme");
            if (accueil.Points.Count == 0) return;
            bonjour.Text = Profil.Accueil(F.Reglages.Pseudo);
            if (l.Count == 0) { etat.Text = "✔  Ton PC va bien."; etat.RoleTexte = Role.Vert; detail.Text = "Rien à faire pour l'instant. Tu peux le nettoyer de temps en temps avec le premier bouton."; }
            else if (nbP > 0) { etat.Text = "Ton PC a besoin de toi : " + l.Count + " chose(s) à régler."; etat.RoleTexte = Role.Rouge; detail.Text = "Clique sur le bouton à droite de chaque ligne ci-dessous."; }
            else { etat.Text = "Ton PC va bien, mais " + l.Count + " chose(s) peuvent être améliorées."; etat.RoleTexte = Role.Orange; detail.Text = "Clique sur le bouton à droite de chaque ligne ci-dessous, ou sur « Nettoyer mon PC »."; }
            etat.AppliquerTheme();
            var anciens = problemes.Controls.Cast<Control>().ToList();
            problemes.Controls.Clear();
            foreach (var c in anciens) { foreach (var b in c.Controls.OfType<Bouton>()) F.BoutonsActions.Remove(b); c.Dispose(); }
            int w = Math.Max(500, bAide.Right - bNettoyer.Left);
            foreach (var p in l.Take(5))
            {
                var ligne = new Carte(w, 52) { Margin = new Padding(0, 0, 0, 8) };
                Role r = p.Etat == "probleme" ? Role.Rouge : Role.Orange;
                ligne.Controls.Add(new Etiquette(p.Etat == "probleme" ? "✖" : "!", new Font("Segoe UI Semibold", 13), r, 16, 12));
                ligne.Controls.Add(new Etiquette(p.Texte, new Font("Segoe UI", 11), Role.Texte, 46, 14) { AutoSize = false, AutoEllipsis = true, Size = new Size(w - 240, 26) });
                if (p.Bouton != null)
                {
                    var b = new Bouton(p.Bouton, p.Etat == "probleme", 170, 34) { Location = new Point(w - 184, 9), Cible = p.Cible, Font = new Font("Segoe UI", 10.5f) };
                    b.Click += (s, e) => F.Executer(((Bouton)s).Cible as Cible);
                    ligne.Controls.Add(b);
                    if (p.Cible != null && p.Cible.Type == "action") F.EnregistrerBoutonAction(b);
                }
                problemes.Controls.Add(ligne);
            }
            F.AppliquerDisponibilites();
            Disposer();
        }
    }
}
