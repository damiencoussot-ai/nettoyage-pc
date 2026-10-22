// Nettoyage PC - contrôles dessinés aux couleurs du thème
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NettoyagePC
{
    /// <summary>Texte (libellé) dont la couleur suit le thème.</summary>
    internal class Etiquette : Label, IThemable
    {
        public Role RoleTexte = Role.Texte;
        public Etiquette(string texte, Font police, Role role, int x = 0, int y = 0)
        {
            Text = texte; Font = police; RoleTexte = role; AutoSize = true; UseMnemonic = false;
            BackColor = Color.Transparent; Location = new Point(x, y);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            AppliquerTheme();
        }
        public void AppliquerTheme() { if (RoleTexte != Role.Transparent) ForeColor = Theme.P.Couleur(RoleTexte); }
        public override string Text { get { return base.Text; } set { base.Text = Langue.T(value); } }

        // (essais hors Windows) Mono ne passe pas à la ligne avec MaximumSize : on calcule la taille comme Windows
        public override Size GetPreferredSize(Size proposedSize)
        {
            if (Natif.EstWindows || !AutoSize || MaximumSize.Width <= 0 || string.IsNullOrEmpty(Text)) return base.GetPreferredSize(proposedSize);
            var t = TextRenderer.MeasureText(Text, Font, new Size(MaximumSize.Width - Padding.Horizontal, int.MaxValue), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            return new Size(Math.Min(MaximumSize.Width, t.Width + Padding.Horizontal), t.Height + Padding.Vertical);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (BackColor.A < 255 && Theme.PeindreFondRapide(this, e.Graphics)) return;
            base.OnPaintBackground(e);
        }
    }

    /// <summary>Panneau dont le fond suit le thème.</summary>
    internal class Surface : Panel, IThemable
    {
        public Role RoleFond = Role.Fond;
        public Surface(Role fond = Role.Fond)
        {
            RoleFond = fond;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            AppliquerTheme();
        }
        public virtual void AppliquerTheme()
        {
            BackColor = RoleFond == Role.Transparent ? Color.Transparent : Theme.P.Couleur(RoleFond);
            if (IsHandleCreated && AutoScroll) Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer");
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (AutoScroll) Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer");
        }
    }

    /// <summary>Bouton lumineux (principal) ou en verre (secondaire), dessiné entièrement.</summary>
    internal class Bouton : Button, IThemable
    {
        public bool Principal;
        public Role? RoleTexte;
        private bool appui;
        public object Cible;
        private readonly Fondu fSurvol;

        public Bouton(string texte, bool principal, int largeur, int hauteur)
        {
            fSurvol = new Fondu(this) { Duree = 130 };
            Text = texte; Principal = principal; Size = new Size(largeur, hauteur);
            FlatStyle = FlatStyle.Flat; Font = Theme.Normal; Cursor = Cursors.Hand; UseMnemonic = false;
            FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        public void AppliquerTheme() { Invalidate(); }

        protected override void OnMouseEnter(EventArgs e) { fSurvol.Vers(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { appui = false; fSurvol.Vers(0); Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { }   // tout est dessiné dans OnPaint
        protected override void OnMouseDown(MouseEventArgs e) { appui = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { appui = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            var P = Theme.P;
            if (!Theme.PeindreFondRapide(this, g)) g.Clear(Theme.FondDerriere(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Width, h = Height;
            float r = Math.Min(10, h / 2f - 1);
            float v = fSurvol.Valeur;
            Color texte;
            if (!Enabled)
            {
                Theme.RemplirArrondi(g, P.Piste, 0.5f, 0.5f, w - 1.5f, h - 1.5f, r);
                texte = Theme.Melanger(P.Discret, P.Piste, 0.35);
            }
            else if (Principal)
            {
                Color c1 = P.Lueur1, c2 = P.Lueur2;
                if (appui) { c1 = Theme.Melanger(c1, Color.Black, 0.15); c2 = Theme.Melanger(c2, Color.Black, 0.15); }
                else if (v > 0) { c1 = Theme.Melanger(c1, Color.White, 0.18 * v); c2 = Theme.Melanger(c2, Color.White, 0.18 * v); }
                Theme.RemplirDegrade(g, c1, c2, 0.5f, 0.5f, w - 1.5f, h - 1.5f, r);
                texte = Theme.TexteSur(Theme.Melanger(c1, c2, 0.5));
            }
            else
            {
                Color fond = appui ? Theme.Melanger(P.Survol, P.Fond, 0.4) : Theme.Melanger(P.Carte, P.Survol, v);
                Theme.RemplirArrondi(g, fond, 0.5f, 0.5f, w - 1.5f, h - 1.5f, r);
                if (v > 0.5f || appui) Theme.ContourLumineux(g, 0.5f, 0.5f, w - 1.5f, h - 1.5f, r);
                else Theme.Contour(g, P.Bordure, 0.5f, 0.5f, w - 1.5f, h - 1.5f, r);
                texte = RoleTexte.HasValue ? P.Couleur(RoleTexte.Value) : P.Texte;
            }
            Theme.Texte(g, Text, Font, new Rectangle(4, 0, w - 8, h), texte, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            if (Focused && ShowFocusCues)
                using (var ch = Theme.CheminArrondi(2.5f, 2.5f, w - 5.5f, h - 5.5f, Math.Max(1, r - 2)))
                using (var s = new Pen(Color.FromArgb(140, P.Lueur1)) { DashStyle = DashStyle.Dot }) g.DrawPath(s, ch);
        }
    }

    /// <summary>Carte aux coins arrondis : voile de lumière, liseré d'accent, bordure lumineuse au survol.</summary>
    internal class Carte : Panel, IThemable
    {
        public bool Accent;
        /// <summary>Icône discrète dessinée en haut à droite (0 : aucune).</summary>
        public int Icone;
        private bool survol;
        private readonly Fondu fSurvol;

        public Carte(int largeur = 300, int hauteur = 176, bool accent = false)
        {
            fSurvol = new Fondu(this) { Duree = 180 };
            Size = new Size(largeur, hauteur); Accent = accent;
            Margin = new Padding(0, 0, 16, 16);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw | ControlStyles.UserPaint, true);
            AppliquerTheme();
        }

        public void AppliquerTheme() { BackColor = Theme.P.Carte; Invalidate(); }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            e.Control.MouseEnter += (s, a) => DefinirSurvol(true);
            e.Control.MouseLeave += (s, a) => VerifierSortie();
        }
        protected override void OnMouseEnter(EventArgs e) { DefinirSurvol(true); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { VerifierSortie(); base.OnMouseLeave(e); }
        private void VerifierSortie() { if (!ClientRectangle.Contains(PointToClient(Cursor.Position))) DefinirSurvol(false); }
        private void DefinirSurvol(bool s) { if (survol == s) return; survol = s; fSurvol.Vers(s ? 1 : 0); }

        /// <summary>Fond de la carte sous un élément posé dessus (style Noyau : couleur unie).</summary>
        public void PeindreFondSous(Graphics g, Rectangle zone) { g.Clear(Theme.P.Carte); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            int w = ClientSize.Width, h = ClientSize.Height;
            if (w < 4 || h < 4) return;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var chemin = Theme.CheminArrondi(0.5f, 0.5f, w - 1.5f, h - 1.5f, 2))
            {
                using (var coins = new Region(new Rectangle(0, 0, w, h)))
                using (var b = new SolidBrush(Theme.FondDerriere(this)))
                { coins.Exclude(chemin); g.FillRegion(b, coins); }
                float v = fSurvol.Valeur;
                // trait fin ; au survol il s'allume en couleur d'accent
                using (var s = new Pen(Theme.Melanger(P.Bordure, P.Lueur1, 0.55 * v))) g.DrawPath(s, chemin);
                if (Accent) { g.SmoothingMode = SmoothingMode.None; Theme.Crochets(g, P.Lueur1, 0, 0, w, h, 14, 2); g.SmoothingMode = SmoothingMode.AntiAlias; }
                else if (v > 0.01f) { g.SmoothingMode = SmoothingMode.None; Theme.Crochets(g, Color.FromArgb((int)(220 * v), P.Lueur1), 0, 0, w, h, 10, 1.5f); g.SmoothingMode = SmoothingMode.AntiAlias; }
            }
            if (Icone != 0 && Theme.IconesGrand != null)
            {
                // seulement si le titre de la carte laisse la place
                var titre = Controls.OfType<Etiquette>().FirstOrDefault();
                if (titre == null || titre.Right < w - 50)
                {
                    var r = new Rectangle(w - 46, 12, 32, 32);
                    float vi = fSurvol.Valeur;
                    Theme.RemplirArrondi(g, Theme.Melanger(P.Carte, P.Lueur1, 0.12 + 0.10 * vi), r.X, r.Y, r.Width, r.Height, 9);
                    Theme.Icone(g, Icone, Theme.IconesGrand, r, Theme.Melanger(Theme.Melanger(P.AccentVif, P.Carte, 0.15), P.AccentVif, vi));
                }
            }
        }
    }

    /// <summary>Grille de cartes qui remplit la largeur de la page : colonnes égales, cartes élargies pour occuper
    /// toute la ligne, une carte deux fois plus large occupe deux colonnes. Les cartes « en plus » sont rangées
    /// derrière un bouton « Plus d'options » (ouvert ou fermé selon le dernier choix sur cette page).</summary>
    internal class Grille : Panel, IThemable
    {
        public const int Ecart = 16;
        private readonly Control page;
        private readonly bool adaptable;
        private readonly Dictionary<Control, int> largeurBase = new Dictionary<Control, int>();
        private readonly List<Control> enPlus = new List<Control>();
        private readonly Dictionary<Control, bool> visibleConnu = new Dictionary<Control, bool>();
        private BoutonOptions bOptions;
        private bool ouvert, agencement, aRefaire;
        /// <summary>Largeur disponible quand la grille ne suit pas la page (0 : 1000 pixels).</summary>
        public int Disponible;
        /// <summary>Clé mémorisant l'état ouvert / fermé des options (en général la clé de la page).</summary>
        public string CleOptions;
        /// <summary>Lecture et enregistrement de l'état des options (branchés par la fenêtre principale).</summary>
        public static Func<string, bool> LireOptions = c => false;
        public static Action<string, bool> EcrireOptions = (c, o) => { };

        public Grille(Control page, int y = 8, bool adaptable = true)
        {
            this.page = page; this.adaptable = adaptable;
            Location = new Point(28, y); Size = new Size(1000, 10);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            AppliquerTheme();
            page.Controls.Add(this);
            if (adaptable) page.Resize += (s, e) => Agencer();
            var pg = page as Page; if (pg != null) CleOptions = pg.Cle;
        }

        public void AppliquerTheme() { BackColor = Theme.P.Fond; }

        /// <summary>Ajoute une carte rangée derrière « Plus d'options ».</summary>
        public void AjouterEnPlus(Control c)
        {
            if (bOptions == null)
            {
                bOptions = new BoutonOptions();
                bOptions.Click += (s, e) => { ouvert = !ouvert; EcrireOptions(CleOptions ?? "", ouvert); Basculer(); };
                ouvert = LireOptions(CleOptions ?? "");
                Controls.Add(bOptions);
            }
            enPlus.Add(c);
            c.Visible = ouvert;
            Controls.Add(c);
            bOptions.Nombre = enPlus.Count;
            bOptions.Ouvert = ouvert;
        }

        /// <summary>Ouvre ou ferme « Plus d'options » (sans mémoriser : essais automatiques).</summary>
        public void DefinirOuvert(bool o) { if (bOptions == null || ouvert == o) return; ouvert = o; Basculer(); }

        private void Basculer()
        {
            using (Dessin.Geler(page))
            {
                SuspendLayout();
                foreach (var c in enPlus) c.Visible = ouvert;
                bOptions.Ouvert = ouvert;
                ResumeLayout(true);
                Agencer();
            }
            if (ouvert && enPlus.Count > 0)
            {
                // fait apparaître les nouvelles cartes si elles sont sous le bas de la fenêtre
                var sp = page as ScrollableControl;
                if (sp != null && sp.AutoScroll) { try { sp.ScrollControlIntoView(enPlus[enPlus.Count - 1]); } catch { } }
            }
        }

        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e);
            var c = e.Control;
            if (c is BoutonOptions) return;
            Preparer(c);
            // une carte montrée ou cachée par la page : on réagence ; mais pas quand c'est toute la page qui s'affiche
            c.VisibleChanged += (s, a) =>
            {
                if (agencement) return;
                if (!Visible) { aRefaire = true; return; }
                bool v, connu = visibleConnu.TryGetValue(c, out v);
                if (!connu || v != c.Visible) Agencer();
            };
            Agencer();
        }

        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (Visible && aRefaire) Agencer(); }

        /// <summary>À la première rencontre (avant tout élargissement) : largeur d'origine et éléments à élargir avec la carte.</summary>
        private void Preparer(Control c)
        {
            if (c is BoutonOptions || largeurBase.ContainsKey(c)) return;
            largeurBase[c] = c.Width;
            // les éléments qui touchent le bord droit de la carte s'élargissent avec elle
            if (c is Carte)
                foreach (Control x in c.Controls)
                {
                    bool etirable = x is Bouton || x is ListeDeroulante || x is ZoneTexte || (x is Etiquette && !x.AutoSize);
                    if (etirable && x.Right >= c.Width - 40) x.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
                }
        }

        protected override void OnControlRemoved(ControlEventArgs e) { base.OnControlRemoved(e); largeurBase.Remove(e.Control); Agencer(); }

        protected override void OnLayout(LayoutEventArgs levent) { if (!agencement) Placer(); }

        /// <summary>Recalcule la largeur, place les cartes et ajuste la hauteur.</summary>
        public void Agencer()
        {
            if (!Visible && IsHandleCreated && page.IsHandleCreated) { aRefaire = true; return; }
            aRefaire = false;
            int dispo = adaptable ? Math.Max(320, page.ClientSize.Width - Left - 24) : (Disponible > 0 ? Disponible : 1000);
            if (Width != dispo) { agencement = true; Width = dispo; agencement = false; }
            Placer();
        }

        private void Placer()
        {
            if (agencement) return;
            if (!Visible && IsHandleCreated) { aRefaire = true; return; }   // page cachée : on agencera à son affichage
            agencement = true;
            try
            {
                int dispo = Width;
                foreach (Control c in Controls) { Preparer(c); visibleConnu[c] = c.Visible; }
                var principales = Controls.Cast<Control>().Where(c => !(c is BoutonOptions) && !enPlus.Contains(c) && c.Visible).ToList();
                var plus = enPlus.Where(c => c.Visible).ToList();
                var toutes = principales.Concat(plus).ToList();
                int min = toutes.Count == 0 ? 300 : toutes.Min(c => Base(c));
                min = (int)(min * 0.93);   // une carte peut se resserrer un peu pour gagner une colonne
                int cols = Math.Max(1, (dispo + Ecart) / (min + Ecart));
                int l = (dispo - (cols - 1) * Ecart) / cols;
                // pas de cartes démesurées sur un très grand écran : une colonne de plus si la place le permet
                while (l > min * 1.4 && (dispo - cols * Ecart) / (cols + 1) >= min) { cols++; l = (dispo - (cols - 1) * Ecart) / cols; }
                int y = Ranger(principales, cols, l, 0);
                if (bOptions != null)
                {
                    bOptions.Visible = enPlus.Count > 0;
                    bOptions.SetBounds(0, y, dispo, 36);
                    y = bOptions.Bottom + 10;
                    if (ouvert) y = Ranger(plus, cols, l, y);
                }
                int h = Math.Max(1, y);
                if (Height != h) Height = h;
            }
            finally { agencement = false; }
        }

        private int Base(Control c) { int b; return largeurBase.TryGetValue(c, out b) ? b : c.Width; }

        /// <summary>Range des cartes en lignes à partir de y ; renvoie le y sous la dernière ligne.</summary>
        private int Ranger(List<Control> liste, int cols, int l, int y)
        {
            int min = liste.Count == 0 ? 300 : liste.Min(c => Base(c));
            int col = 0, hLigne = 0;
            foreach (var c in liste)
            {
                int span = Math.Max(1, Math.Min(cols, (int)Math.Round((Base(c) + Ecart) / (double)(Math.Max(min, 120) + Ecart))));
                if (col > 0 && col + span > cols) { y += hLigne + Ecart; col = 0; hLigne = 0; }
                int x = col * (l + Ecart);
                int w = span * l + (span - 1) * Ecart;
                c.SetBounds(x, y, w, c.Height);
                hLigne = Math.Max(hLigne, c.Height);
                col += span;
                if (col >= cols) { y += hLigne + Ecart; col = 0; hLigne = 0; }
            }
            if (col > 0) y += hLigne + Ecart;
            return y;
        }
    }

    /// <summary>Séparateur cliquable « Plus d'options (N) » / « Moins d'options ».</summary>
    internal class BoutonOptions : Control, IThemable
    {
        public int Nombre;
        private bool ouvert;
        private readonly Fondu survol;
        public bool Ouvert { get { return ouvert; } set { ouvert = value; Invalidate(); } }

        public BoutonOptions()
        {
            Cursor = Cursors.Hand; TabStop = true;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            survol = new Fondu(this);
            AppliquerTheme();
        }
        public void AppliquerTheme() { BackColor = Theme.P.Fond; Invalidate(); }
        protected override void OnMouseEnter(EventArgs e) { survol.Vers(1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { survol.Vers(0); base.OnMouseLeave(e); }
        protected override void OnKeyDown(KeyEventArgs e) { if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; } base.OnKeyDown(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(P.Fond);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float v = survol.Valeur;
            string texte = ouvert ? Langue.T("Moins d'options") : Langue.F("Plus d'options ({0})", Nombre);
            var police = Theme.NavActif;
            var t = TextRenderer.MeasureText(texte, police);
            int hb = 30, yb = (Height - hb) / 2, lb = t.Width + 52;
            Theme.RemplirArrondi(g, Theme.Melanger(P.Carte, P.Survol, v), 0.5f, yb + 0.5f, lb, hb - 1, hb / 2f - 1);
            if (v > 0.5f || Focused) Theme.ContourLumineux(g, 0.5f, yb + 0.5f, lb, hb - 1, hb / 2f - 1);
            else Theme.Contour(g, P.Bordure, 0.5f, yb + 0.5f, lb, hb - 1, hb / 2f - 1);
            Theme.Texte(g, texte, police, new Rectangle(16, yb, t.Width + 4, hb), Theme.Melanger(P.Discret, P.Texte, v), TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
            // chevron
            float cx = 16 + t.Width + 18, cy = yb + hb / 2f;
            using (var pen = new Pen(Theme.Melanger(P.Discret, P.AccentVif, Math.Max(v, 0.4f)), 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                if (ouvert) g.DrawLines(pen, new[] { new PointF(cx - 5, cy + 2.5f), new PointF(cx, cy - 2.5f), new PointF(cx + 5, cy + 2.5f) });
                else g.DrawLines(pen, new[] { new PointF(cx - 5, cy - 2.5f), new PointF(cx, cy + 2.5f), new PointF(cx + 5, cy - 2.5f) });
            }
            using (var pen = new Pen(P.Bordure)) g.DrawLine(pen, lb + 14, Height / 2f, Width - 2, Height / 2f);
        }
    }

    /// <summary>Case à cocher moderne : case arrondie, cochée en dégradé lumineux.</summary>
    internal class CaseACocher : CheckBox, IThemable
    {
        private bool survol;
        public Role RoleTexte = Role.Texte;

        public CaseACocher(string texte, int x = 0, int y = 0)
        {
            Text = texte; Location = new Point(x, y); AutoSize = true; Font = Theme.Normal; UseMnemonic = false; Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            AppliquerTheme();
        }

        public void AppliquerTheme() { ForeColor = Theme.P.Couleur(RoleTexte); Invalidate(); }
        public override string Text { get { return base.Text; } set { base.Text = Langue.T(value); } }
        protected override void OnMouseEnter(EventArgs e) { survol = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { survol = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnPaintBackground(PaintEventArgs e) { }   // tout est dessiné dans OnPaint

        public override Size GetPreferredSize(Size proposee)
        {
            var t = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
            return new Size(t.Width + 26, Math.Max(22, t.Height + 4));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            if (BackColor.A == 255) g.Clear(BackColor);
            else if (!Theme.PeindreFondRapide(this, g)) g.Clear(Theme.FondDerriere(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int t = 16, y = (ClientSize.Height - t) / 2;
            int alpha = Enabled ? 255 : 110;
            if (Checked)
            {
                Theme.RemplirDegrade(g, Color.FromArgb(alpha, P.Lueur1), Color.FromArgb(alpha, P.Accent), 0.5f, y + 0.5f, t - 1, t - 1, 4);
                using (var s = new Pen(Color.FromArgb(alpha, 255, 255, 255), 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(s, new[] { new PointF(t * 0.25f, y + t * 0.52f), new PointF(t * 0.43f, y + t * 0.70f), new PointF(t * 0.76f, y + t * 0.30f) });
            }
            else
            {
                Theme.RemplirArrondi(g, Color.FromArgb(alpha, Theme.Melanger(Theme.FondDerriere(this), P.Texte, 0.06)), 0.5f, y + 0.5f, t - 1, t - 1, 4);
                Theme.Contour(g, Color.FromArgb(alpha, survol && Enabled ? P.AccentVif : P.Discret), 0.5f, y + 0.5f, t - 1, t - 1, 4, 1.2f);
            }
            var c = Enabled ? ForeColor : P.Discret;
            Theme.Texte(g, Text, Font, new Rectangle(t + 7, 0, ClientSize.Width - t - 7, ClientSize.Height), c, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordBreak);
            if (Focused && ShowFocusCues) using (var s = new Pen(Color.FromArgb(120, P.Lueur1)) { DashStyle = DashStyle.Dot }) g.DrawRectangle(s, t + 4, 1, ClientSize.Width - t - 6, ClientSize.Height - 3);
        }
    }

    /// <summary>Liste déroulante : éléments dessinés aux couleurs du thème.</summary>
    internal class ListeDeroulante : ComboBox, IThemable
    {
        public ListeDeroulante(int largeur)
        {
            DropDownStyle = ComboBoxStyle.DropDownList; DrawMode = DrawMode.OwnerDrawFixed; FlatStyle = FlatStyle.Flat;
            Width = largeur; Font = Theme.Normal; ItemHeight = 20;
            AppliquerTheme();
        }
        public void AppliquerTheme()
        {
            BackColor = Theme.P.Carte; ForeColor = Theme.P.Texte;
            if (IsHandleCreated) Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_CFD", "CFD");
            Invalidate();
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_CFD", "CFD"); }

        /// <summary>Après le dessin de Windows : bouton de la flèche et bordure aux couleurs du thème (sinon un bouton blanc en thème sombre).</summary>
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg != 0x000F || !Natif.EstWindows) return;
            try
            {
                using (var g = CreateGraphics())
                {
                    var P = Theme.P;
                    int w = SystemInformation.VerticalScrollBarWidth, h = ClientSize.Height;
                    var r = new Rectangle(ClientSize.Width - w - 2, 1, w + 1, h - 2);
                    using (var b = new SolidBrush(BackColor)) g.FillRectangle(b, r);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    float cx = r.X + r.Width / 2f, cy = h / 2f;
                    using (var p = new Pen(Enabled ? P.Discret : P.Piste, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                        g.DrawLines(p, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
                    g.SmoothingMode = SmoothingMode.None;
                    using (var p = new Pen(Focused || DroppedDown ? P.Accent : P.Bordure)) g.DrawRectangle(p, 0, 0, ClientSize.Width - 1, h - 1);
                }
            }
            catch { }
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool edition = (e.State & DrawItemState.ComboBoxEdit) != 0;
            bool choisi = (e.State & DrawItemState.Selected) != 0 && !edition;
            Color fond = choisi ? Theme.P.Accent : BackColor, texte = choisi ? Theme.TexteSur(Theme.P.Accent) : (Enabled ? ForeColor : Theme.P.Discret);
            using (var b = new SolidBrush(fond)) e.Graphics.FillRectangle(b, e.Bounds);
            Theme.Texte(e.Graphics, GetItemText(Items[e.Index]), Font, new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height), texte,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
        }
    }

    internal class ZoneTexte : TextBox, IThemable
    {
        public ZoneTexte(int largeur) { Width = largeur; Font = Theme.Normal; BorderStyle = BorderStyle.FixedSingle; AppliquerTheme(); }
        public void AppliquerTheme()
        {
            BackColor = Theme.P.Carte; ForeColor = Theme.P.Texte;
            if (IsHandleCreated && Multiline) Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer");
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); if (Multiline) Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer"); }
    }

    internal class Lien : LinkLabel, IThemable
    {
        public Lien(string texte, int x, int y)
        {
            Text = texte; Location = new Point(x, y); AutoSize = true; Font = Theme.Normal; BackColor = Color.Transparent; UseMnemonic = false;
            LinkBehavior = LinkBehavior.HoverUnderline;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            AppliquerTheme();
        }
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (BackColor.A < 255 && Theme.PeindreFondRapide(this, e.Graphics)) return;
            base.OnPaintBackground(e);
        }
        public void AppliquerTheme() { LinkColor = Theme.P.AccentVif; ActiveLinkColor = Theme.P.Accent; VisitedLinkColor = Theme.P.AccentVif; }
        public override string Text { get { return base.Text; } set { base.Text = Langue.T(value); } }
    }

    /// <summary>Liste (tableau) : en-têtes dessinés, ascenseurs sombres, couleurs du thème.</summary>
    internal class ListeSombre : ListView, IThemable
    {
        public ListeSombre()
        {
            View = View.Details; FullRowSelect = true; Font = Theme.Normal; BorderStyle = BorderStyle.None; HideSelection = false;
            OwnerDraw = true; DoubleBuffered = true;
            AppliquerTheme();
        }

        /// <summary>Colonne qui prend la place restante (-1 : la dernière ; -2 : aucune).</summary>
        public int ColonneElastique = -1;
        private bool ajustement;

        protected override void OnResize(EventArgs e) { base.OnResize(e); AjusterColonnes(); }

        public void AjusterColonnes()
        {
            if (ajustement || ColonneElastique < -1 || Columns.Count == 0 || ClientSize.Width < 50) return;
            int c = ColonneElastique == -1 ? Columns.Count - 1 : Math.Min(ColonneElastique, Columns.Count - 1);
            int autres = 0;
            for (int i = 0; i < Columns.Count; i++) if (i != c) autres += Columns[i].Width;
            int l = ClientSize.Width - autres - 4;
            if (l < 80) return;
            ajustement = true;
            try { Columns[c].Width = l; } finally { ajustement = false; }
        }

        public ColumnHeader Colonne(string titre, int largeur, HorizontalAlignment alignement = HorizontalAlignment.Left)
        {
            var c = Columns.Add(titre, largeur); c.TextAlign = alignement; return c;
        }

        public void AppliquerTheme()
        {
            var ancienne = Theme.EstSombre ? Theme.Clair : Theme.Sombre;
            BackColor = Theme.P.Carte; ForeColor = Theme.P.Texte;
            foreach (ListViewItem it in Items)
            {
                if (it.ForeColor == ancienne.Discret) it.ForeColor = Theme.P.Discret;
                else if (it.ForeColor == ancienne.Rouge) it.ForeColor = Theme.P.Rouge;
                else if (it.ForeColor == ancienne.Orange) it.ForeColor = Theme.P.Orange;
                else if (it.ForeColor == ancienne.Vert) it.ForeColor = Theme.P.Vert;
                else if (it.ForeColor == ancienne.Texte) it.ForeColor = Theme.P.Texte;
                it.BackColor = Theme.P.Carte;
            }
            if (IsHandleCreated) { Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer"); Natif.EnTeteListe(Handle, Theme.EstSombre); }
            Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer");
            Natif.EnTeteListe(Handle, Theme.EstSombre);
        }

        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            using (var b = new SolidBrush(P.EnTete)) g.FillRectangle(b, e.Bounds);
            using (var s = new Pen(P.Bordure))
            {
                g.DrawLine(s, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                g.DrawLine(s, e.Bounds.Right - 1, e.Bounds.Top + 6, e.Bounds.Right - 1, e.Bounds.Bottom - 7);
            }
            var f = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine;
            f |= e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left;
            Theme.Texte(g, e.Header.Text, Theme.PetitGras, new Rectangle(e.Bounds.Left + 8, e.Bounds.Top, e.Bounds.Width - 14, e.Bounds.Height), P.Discret, f);
        }
        // La ligne choisie est dessinée par l'application : le dessin de Windows rend son texte illisible en thème sombre.
        private Color FondSelection { get { return Theme.Melanger(Theme.P.Carte, Theme.P.Lueur1, Focused ? (Theme.EstSombre ? 0.34 : 0.24) : (Theme.EstSombre ? 0.22 : 0.15)); } }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            if (!e.Item.Selected) { e.DrawDefault = true; return; }
            using (var b = new SolidBrush(FondSelection)) e.Graphics.FillRectangle(b, e.Bounds);
            e.DrawDefault = false;
        }
        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            if (!e.Item.Selected || e.ColumnIndex >= Columns.Count) { e.DrawDefault = true; return; }
            e.DrawDefault = false;
            var g = e.Graphics; var r = e.Bounds;
            if (e.ColumnIndex == 0) r = new Rectangle(r.X, r.Y, Math.Min(r.Width, Columns[0].Width), r.Height);   // (la 1re colonne reçoit parfois toute la ligne)
            using (var b = new SolidBrush(FondSelection)) g.FillRectangle(b, r);
            int x = r.X + 6;
            if (e.ColumnIndex == 0 && CheckBoxes)
            {
                var etat = e.Item.Checked ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal;
                var t = CheckBoxRenderer.GetGlyphSize(g, etat);
                CheckBoxRenderer.DrawCheckBox(g, new Point(r.X + 4, r.Y + (r.Height - t.Height) / 2), etat);
                x = r.X + 4 + t.Width + 6;
            }
            var f = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix;
            var al = Columns[e.ColumnIndex].TextAlign;
            if (al == HorizontalAlignment.Right) f |= TextFormatFlags.Right; else if (al == HorizontalAlignment.Center) f |= TextFormatFlags.HorizontalCenter;
            Color texte = e.Item.UseItemStyleForSubItems || e.ColumnIndex == 0 ? e.Item.ForeColor : e.SubItem.ForeColor;
            if (texte.A == 0 || texte == e.Item.BackColor) texte = Theme.P.Texte;
            TextRenderer.DrawText(g, e.SubItem.Text, e.Item.Font ?? Font, new Rectangle(x, r.Y, Math.Max(1, r.Right - x - 6), r.Height), texte, f);
        }

        public ListViewItem Ajouter(object tag, params string[] colonnes)
        {
            var it = new ListViewItem(Langue.Exact(colonnes.Length > 0 ? colonnes[0] : "")) { Tag = tag, ForeColor = Theme.P.Texte, BackColor = Theme.P.Carte };
            for (int i = 1; i < colonnes.Length; i++) it.SubItems.Add(Langue.Exact(colonnes[i] ?? ""));
            Items.Add(it);
            return it;
        }

        /// <summary>Tri au clic sur un en-tête (texte, ou valeur numérique stockée dans Tag des sous-éléments).</summary>
        public void ActiverTri()
        {
            int col = -1; bool croissant = true;
            ColumnClick += (s, e) =>
            {
                croissant = e.Column == col ? !croissant : true; col = e.Column;
                ListViewItemSorter = new Comparateur(col, croissant);
                Sort();
            };
        }

        private class Comparateur : System.Collections.IComparer
        {
            private readonly int col; private readonly bool croissant;
            public Comparateur(int c, bool cr) { col = c; croissant = cr; }
            public int Compare(object x, object y)
            {
                var a = (ListViewItem)x; var b = (ListViewItem)y;
                var sa = col < a.SubItems.Count ? a.SubItems[col] : null; var sb = col < b.SubItems.Count ? b.SubItems[col] : null;
                int r;
                if (sa != null && sb != null && sa.Tag is IComparable && sb.Tag is IComparable && sa.Tag.GetType() == sb.Tag.GetType()) r = ((IComparable)sa.Tag).CompareTo(sb.Tag);
                else r = string.Compare(sa == null ? "" : sa.Text, sb == null ? "" : sb.Text, StringComparison.CurrentCultureIgnoreCase);
                return croissant ? r : -r;
            }
        }
    }

    /// <summary>Liste de cases à cocher (CheckedListBox) aux couleurs du thème.</summary>
    internal class ListeCases : CheckedListBox, IThemable
    {
        public ListeCases() { BorderStyle = BorderStyle.None; CheckOnClick = true; Font = Theme.Normal; AppliquerTheme(); }
        public void AppliquerTheme()
        {
            BackColor = Theme.P.Carte; ForeColor = Theme.P.Texte;
            if (IsHandleCreated) Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer");
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Natif.ThemeControle(Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer"); }
    }

    /// <summary>Barre de progression : pourcentage réel envoyé par le moteur, qui avance doucement entre deux étapes.</summary>
    internal class BarreProgression : Control, IThemable
    {
        public bool Indetermine = true;
        public double Debut, Fin, Valeur;
        private double phase;
        private int affiche = -1;
        private readonly Timer minuteur = new Timer { Interval = 40 };
        public event Action<int> PourcentageChange;

        public BarreProgression()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            minuteur.Tick += (s, e) => Avancer();
        }

        public void AppliquerTheme() { Invalidate(); }

        public void Demarrer() { Indetermine = true; Debut = Fin = Valeur = 0; affiche = -1; Visible = true; minuteur.Start(); }
        public void Arreter() { minuteur.Stop(); Visible = false; }
        public void Etape(double debut, double fin) { Debut = debut; Fin = fin; Indetermine = false; }

        private void Avancer()
        {
            phase += 0.012;
            if (!Indetermine)
            {
                // rattrape vite le début de l'étape, puis avance doucement vers sa fin (sans la dépasser)
                if (Valeur < Debut) Valeur += Math.Max(0.4, (Debut - Valeur) * 0.2);
                else Valeur += (Fin - Valeur) * 0.0012;
                if (Valeur > Fin && Fin >= Debut) Valeur = Math.Max(Debut, Math.Min(Valeur, Fin));
                int pct = (int)Math.Floor(Valeur);
                if (pct != affiche) { affiche = pct; var p = PourcentageChange; if (p != null) p(pct); }
            }
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int lb = Width - 58, yb = (Height - 10) / 2;
            Theme.RemplirArrondi(g, P.Piste, 0, yb, lb, 10, 5);
            string texte;
            if (Indetermine)
            {
                float lm = Math.Min(70, lb / 3f);
                float x = (float)((phase % 1.0) * (lb + lm)) - lm;
                g.SetClip(new RectangleF(0, yb, lb, 10));
                Theme.RemplirDegrade(g, P.Lueur1, P.Lueur2, x, yb, lm, 10, 5);
                g.ResetClip();
                texte = "...";
            }
            else
            {
                double v = Math.Max(0, Math.Min(100, Valeur));
                if (v > 0.5)
                {
                    float lv = (float)Math.Max(10, lb * v / 100);
                    Theme.RemplirDegrade(g, P.Lueur1, P.Lueur2, 0, yb, lv, 10, 5);
                    float xr = (float)((phase % 1.0) * (lv + 40)) - 40;
                    g.SetClip(new RectangleF(0, yb, lv, 10));
                    Theme.RemplirArrondi(g, Color.FromArgb(60, 255, 255, 255), xr, yb, 40, 10, 5);
                    g.ResetClip();
                }
                texte = Math.Floor(v).ToString("0") + " %";
            }
            Theme.Texte(g, texte, Theme.PetitGras, new Rectangle(lb + 4, 0, Width - lb - 4, Height), P.Texte, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        protected override void Dispose(bool d) { if (d) minuteur.Dispose(); base.Dispose(d); }
    }

    /// <summary>Infobulles aux couleurs du thème (une seule pour toute l'application).</summary>
    internal static class Infobulle
    {
        public static readonly ToolTip T = Creer();

        private static ToolTip Creer()
        {
            var t = new ToolTip { AutoPopDelay = 15000, InitialDelay = 450, ReshowDelay = 100, OwnerDraw = true };
            t.Popup += (s, e) =>
            {
                string texte = e.AssociatedControl == null ? "" : t.GetToolTip(e.AssociatedControl);
                if (string.IsNullOrEmpty(texte)) return;
                var m = TextRenderer.MeasureText(texte, Theme.Petit, new Size(360, 0), TextFormatFlags.WordBreak);
                e.ToolTipSize = new Size(m.Width + 24, m.Height + 16);
            };
            t.Draw += (s, e) =>
            {
                var g = e.Graphics; var P = Theme.P;
                int w = e.Bounds.Width, h = e.Bounds.Height;
                using (var b = new SolidBrush(Theme.EstSombre ? Color.FromArgb(22, 29, 52) : Color.White)) g.FillRectangle(b, 0, 0, w, h);
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, 3, Math.Max(2, h)), P.Lueur1, P.Lueur2, LinearGradientMode.Vertical)) g.FillRectangle(b, 0, 0, 3, h);
                using (var p = new Pen(P.Bordure)) g.DrawRectangle(p, 0, 0, w - 1, h - 1);
                Theme.Texte(g, e.ToolTipText, Theme.Petit, new Rectangle(13, 8, w - 20, h - 12), P.Texte, TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top);
            };
            return t;
        }

        public static void Definir(Control c, string texte) { T.SetToolTip(c, Langue.T(texte)); }
    }

    /// <summary>Journal d'activité : texte coloré selon le contenu des lignes.</summary>
    internal class ConsoleJournal : RichTextBox, IThemable
    {
        public ConsoleJournal()
        {
            ReadOnly = true; Font = Theme.Console; BorderStyle = BorderStyle.None; DetectUrls = false; ForeColor = Color.Gainsboro;
            AppliquerTheme();
        }
        public void AppliquerTheme()
        {
            BackColor = Theme.P.Console;
            if (IsHandleCreated) Natif.ThemeControle(Handle, true, "DarkMode_Explorer", "Explorer");
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Natif.ThemeControle(Handle, true, "DarkMode_Explorer", "Explorer"); }

        public void AjouterLigne(string ligne)
        {
            Color c = Color.Gainsboro;
            if (Regex.IsMatch(ligne, @"^\s*===")) c = Color.FromArgb(97, 214, 255);
            else if (Regex.IsMatch(ligne, "ATTENTION|impossible|refusé|Droits insuffisants")) c = Color.FromArgb(255, 120, 110);
            else if (Regex.IsMatch(ligne, @"^\s*->|OK :|terminé|aucune alerte|: aucune|aucun problème|Tâche")) c = Color.FromArgb(130, 220, 140);
            else if (Regex.IsMatch(ligne, @"^\s*[(]|^\s*Conseil|^\s*Note|^\s*Remarque")) c = Color.FromArgb(150, 150, 160);
            else if (ligne.StartsWith("Nettoyage PC")) c = Color.FromArgb(255, 210, 100);
            SelectionStart = TextLength; SelectionLength = 0; SelectionColor = c;
            AppendText(ligne + "\n");
        }
    }
}
