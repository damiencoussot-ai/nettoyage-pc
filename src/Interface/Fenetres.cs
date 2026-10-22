// Nettoyage PC - boîte de message du thème, notifications intégrées, palette de commandes (Ctrl+K)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace NettoyagePC
{
    /// <summary>Fenêtre secondaire aux couleurs du thème (barre de titre sombre).</summary>
    internal class FenetreTheme : Form
    {
        public FenetreTheme(string titre, int largeur, int hauteur, bool redimensionnable = false)
        {
            Text = Langue.T(titre); Size = new Size(largeur, hauteur); StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false; ShowInTaskbar = false; Font = Theme.Normal; BackColor = Theme.P.Fond;
            if (!redimensionnable) { FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; }
            var principale = Application.OpenForms.OfType<FenetrePrincipale>().FirstOrDefault();
            if (principale != null) Icon = principale.Icon;
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Natif.BarreTitreSombre(Handle, Theme.EstSombre); }

        /// <summary>Petits écrans (portable 1366 × 768) : la fenêtre reste entière à l'écran, avec une barre de défilement si besoin.</summary>
        protected override void OnShown(EventArgs e)
        {
            try
            {
                var zone = Screen.FromControl(this).WorkingArea;
                if (Height > zone.Height || Width > zone.Width)
                {
                    // fenêtre fixe : barre de défilement ; fenêtre redimensionnable : ses éléments ancrés suivent la taille
                    if (FormBorderStyle == FormBorderStyle.FixedDialog) AutoScroll = true;
                    Size = new Size(Math.Min(Width, zone.Width), Math.Min(Height, zone.Height));
                }
                Location = new Point(Math.Max(zone.Left, Math.Min(Left, zone.Right - Width)), Math.Max(zone.Top, Math.Min(Top, zone.Bottom - Height)));
            }
            catch { }
            base.OnShown(e);
        }
    }

    /// <summary>Remplace la boîte blanche de Windows : mêmes paramètres, même résultat, aux couleurs du thème.</summary>
    internal static class BoiteMessage
    {
        public static DialogResult Afficher(string texte, string titre = "Nettoyage PC", MessageBoxButtons boutons = MessageBoxButtons.OK, MessageBoxIcon icone = MessageBoxIcon.None)
        {
            texte = Langue.T(texte); titre = Langue.T(titre);
            if (Programme.DossierAutotest != null) { JournalDemarrage.Noter("(autotest) message : " + titre + " : " + texte.Replace("\n", " ")); return boutons == MessageBoxButtons.YesNo ? DialogResult.No : DialogResult.OK; }
            var P = Theme.P;
            int glyphe = 0; Color couleur = P.Accent; SystemSound son = null;
            switch (icone)
            {
                case MessageBoxIcon.Information: glyphe = 0xE946; couleur = P.Accent; son = SystemSounds.Asterisk; break;
                case MessageBoxIcon.Warning: glyphe = 0xE7BA; couleur = P.Orange; son = SystemSounds.Exclamation; break;
                case MessageBoxIcon.Error: glyphe = 0xEA39; couleur = P.Rouge; son = SystemSounds.Hand; break;
                case MessageBoxIcon.Question: glyphe = 0xE9CE; couleur = P.Lueur1; break;
            }
            var liste = new List<Tuple<string, DialogResult>>();
            switch (boutons)
            {
                case MessageBoxButtons.OKCancel: liste.Add(Tuple.Create("OK", DialogResult.OK)); liste.Add(Tuple.Create("Annuler", DialogResult.Cancel)); break;
                case MessageBoxButtons.YesNo: liste.Add(Tuple.Create("Oui", DialogResult.Yes)); liste.Add(Tuple.Create("Non", DialogResult.No)); break;
                case MessageBoxButtons.YesNoCancel: liste.Add(Tuple.Create("Oui", DialogResult.Yes)); liste.Add(Tuple.Create("Non", DialogResult.No)); liste.Add(Tuple.Create("Annuler", DialogResult.Cancel)); break;
                case MessageBoxButtons.RetryCancel: liste.Add(Tuple.Create("Réessayer", DialogResult.Retry)); liste.Add(Tuple.Create("Annuler", DialogResult.Cancel)); break;
                default: liste.Add(Tuple.Create("OK", DialogResult.OK)); break;
            }
            using (var f = new FenetreTheme(titre, 400, 200))
            {
                f.BackColor = P.Carte; f.KeyPreview = true;
                int xTexte = glyphe != 0 ? 84 : 28, largeurMax = 440;
                var mesure = TextRenderer.MeasureText(texte, Theme.Normal, new Size(largeurMax, 0), TextFormatFlags.WordBreak);
                int lTexte = Math.Max(220, Math.Min(largeurMax, mesure.Width + 6)), hTexte = mesure.Height + 4;
                Control zone;
                if (hTexte > 380)
                {
                    var tb = new ZoneTexte(largeurMax + 20) { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, WordWrap = true, TabStop = false };
                    tb.Text = texte.Replace("\r\n", "\n").Replace("\n", "\r\n");
                    lTexte = largeurMax + 20; hTexte = 380; tb.Size = new Size(lTexte, hTexte);
                    zone = tb;
                }
                else zone = new Etiquette(texte, Theme.Normal, Role.Texte) { AutoSize = false, Size = new Size(lTexte, hTexte) };
                int hContenu = Math.Max(hTexte, 46);
                zone.Location = new Point(xTexte, 26 + Math.Max(0, (46 - hTexte) / 2));
                f.Controls.Add(zone);
                var bande = new Surface(Role.Fond) { Dock = DockStyle.Bottom, Height = 64 };
                f.Controls.Add(bande);
                var bs = new List<Bouton>();
                for (int i = 0; i < liste.Count; i++)
                {
                    var b = new Bouton(liste[i].Item1, i == 0, 112, 36) { DialogResult = liste[i].Item2 };
                    bande.Controls.Add(b); bs.Add(b);
                }
                int lForm = Math.Max(xTexte + lTexte + 30, bs.Count * 120 + 40);
                f.ClientSize = new Size(lForm, 26 + hContenu + 26 + bande.Height);
                int x = lForm - 24 - bs.Count * 120 + 8;
                foreach (var b in bs) { b.Location = new Point(x, 14); x += 120; }
                f.AcceptButton = bs[0]; f.CancelButton = bs[bs.Count - 1];
                if (glyphe != 0)
                    f.Paint += (s, e) =>
                    {
                        var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
                        using (var h = new SolidBrush(Color.FromArgb(40, couleur))) g.FillEllipse(h, 22, 22, 46, 46);
                        using (var p = new Pen(Color.FromArgb(150, couleur), 1.5f)) g.DrawEllipse(p, 22, 22, 46, 46);
                        Theme.Icone(g, glyphe, Theme.IconesTresGrand ?? Theme.IconesGrand, new Rectangle(22, 22, 46, 46), couleur);
                    };
                f.KeyDown += (s, e) => { if (e.Control && e.KeyCode == Keys.C) { try { Clipboard.SetText(titre + "\r\n\r\n" + texte); } catch { } e.Handled = true; } };
                Form parent = Form.ActiveForm;
                if (parent == null || parent.FormBorderStyle == FormBorderStyle.None || !parent.Visible) parent = Application.OpenForms.OfType<FenetrePrincipale>().FirstOrDefault(p => p.Visible);
                if (parent == null) { f.StartPosition = FormStartPosition.CenterScreen; f.TopMost = true; }
                if (son != null) try { son.Play(); } catch { }
                var r = parent != null ? f.ShowDialog(parent) : f.ShowDialog();
                if (r == DialogResult.Cancel && boutons == MessageBoxButtons.YesNo) r = DialogResult.No;
                return r;
            }
        }

        public static void Info(string texte, string titre = "Nettoyage PC") { Afficher(texte, titre, MessageBoxButtons.OK, MessageBoxIcon.Information); }
        public static void Attention(string texte, string titre = "Nettoyage PC") { Afficher(texte, titre, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        public static bool Question(string texte, string titre = "Nettoyage PC") { return Afficher(texte, titre, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes; }
    }

    /// <summary>Notification qui apparaît en douceur dans l'angle de la fenêtre, puis disparaît.</summary>
    internal class Toast : Form
    {
        private static Toast actuel;
        private readonly string titre, texte;
        private readonly Color couleur;
        private readonly Action auClic;
        private readonly Timer minuteur = new Timer { Interval = 30 };
        private int age, yFinal;

        public static void Afficher(Form proprietaire, string titre, string texte, string type = "ok", Action auClic = null)
        {
            try { if (actuel != null && !actuel.IsDisposed) actuel.Close(); } catch { }
            if (proprietaire == null || !proprietaire.Visible || proprietaire.WindowState == FormWindowState.Minimized) return;
            var t = new Toast(titre, texte, type == "attention" ? Theme.P.Orange : type == "erreur" ? Theme.P.Rouge : Theme.P.Lueur1, auClic);
            t.yFinal = proprietaire.Top + proprietaire.Height - t.Height - 74;
            t.Location = new Point(proprietaire.Left + proprietaire.Width - t.Width - 24, t.yFinal + 16);
            actuel = t;
            t.Show(proprietaire);
            t.minuteur.Start();
        }

        private Toast(string titre, string texte, Color couleur, Action auClic)
        {
            this.titre = titre; this.texte = texte; this.couleur = couleur; this.auClic = auClic;
            FormBorderStyle = FormBorderStyle.None; ShowInTaskbar = false; StartPosition = FormStartPosition.Manual;
            Size = new Size(390, 104); BackColor = Theme.P.Carte; Opacity = 0; Cursor = Cursors.Hand; DoubleBuffered = true;
            minuteur.Tick += (s, e) => Animer();
            Click += (s, e) => { Close(); if (this.auClic != null) this.auClic(); };
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        private void Animer()
        {
            if (IsDisposed) { minuteur.Stop(); return; }
            age += 30;
            if (age <= 300) { Opacity = Math.Min(0.97, age / 300.0); Top = (int)(yFinal + 16 * (1 - age / 300.0)); }
            else if (age >= 7000) { Opacity = Math.Max(0, Opacity - 0.08); if (Opacity <= 0.02) { minuteur.Stop(); Close(); } }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, Width, Height), couleur, P.Lueur2, LinearGradientMode.Vertical)) g.FillRectangle(b, 0, 0, 4, Height);
            using (var p = new Pen(P.Bordure)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
            Theme.Icone(g, 0xE930, Theme.IconesGrand, new Rectangle(14, 14, 24, 24), couleur);
            Theme.Texte(g, titre, Theme.CarteTitre, new Rectangle(46, 12, Width - 60, 22), P.Texte, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            Theme.Texte(g, texte, Theme.Petit, new Rectangle(46, 36, Width - 60, Height - 42), P.Discret, TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }

        protected override void OnFormClosed(FormClosedEventArgs e) { minuteur.Stop(); if (actuel == this) actuel = null; base.OnFormClosed(e); }
        protected override void Dispose(bool d) { if (d) minuteur.Dispose(); base.Dispose(d); }
    }

    internal class Commande
    {
        public string Titre = "", Detail = "", Lieu = "";
        public int Icone;
        public Action Executer;
        public string Recherche, RechercheTitre;
    }

    /// <summary>Palette de commandes : quelques lettres, Entrée lance l'action ou ouvre la page.</summary>
    internal class PaletteCommandes : Form
    {
        private static PaletteCommandes actuelle;
        private readonly TextBox saisie;
        private readonly ListBox liste;
        private readonly List<Commande> commandes;

        public static void Ouvrir(Form proprietaire, List<Commande> commandes)
        {
            if (actuelle != null && !actuelle.IsDisposed) { actuelle.Activate(); return; }
            var p = new PaletteCommandes(commandes);
            p.Location = new Point(proprietaire.Left + (proprietaire.Width - p.Width) / 2, proprietaire.Top + 90);
            actuelle = p;
            p.Show(proprietaire);
            p.saisie.Focus();
        }

        private PaletteCommandes(List<Commande> commandes)
        {
            this.commandes = commandes;
            foreach (var c in commandes)
            {
                c.Recherche = Format.SansAccents(c.Titre + " " + c.Detail + " " + c.Lieu + " " + Langue.T(c.Titre) + " " + Langue.T(c.Detail));
                c.RechercheTitre = " " + Format.SansAccents(c.Titre + " " + Langue.T(c.Titre));
            }
            var P = Theme.P;
            FormBorderStyle = FormBorderStyle.None; StartPosition = FormStartPosition.Manual; ShowInTaskbar = false;
            Size = new Size(640, 452); BackColor = P.Carte; KeyPreview = true; DoubleBuffered = true;
            saisie = new TextBox { BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 13), BackColor = P.Carte, ForeColor = P.Texte, Location = new Point(52, 18), Size = new Size(570, 30) };
            liste = new ListBox { BorderStyle = BorderStyle.None, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 46, BackColor = P.Carte, Location = new Point(8, 64), Size = new Size(624, 350), IntegralHeight = false };
            liste.HandleCreated += (s, e) => Natif.ThemeControle(liste.Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer");
            liste.DrawItem += DessinerElement;
            Controls.Add(saisie); Controls.Add(liste);
            saisie.TextChanged += (s, e) => Filtrer();
            liste.DoubleClick += (s, e) => Executer();
            KeyDown += (s, e) =>
            {
                switch (e.KeyCode)
                {
                    case Keys.Escape: e.SuppressKeyPress = true; Close(); break;
                    case Keys.Enter: e.SuppressKeyPress = true; Executer(); break;
                    case Keys.Down: e.SuppressKeyPress = true; if (liste.SelectedIndex < liste.Items.Count - 1) liste.SelectedIndex++; break;
                    case Keys.Up: e.SuppressKeyPress = true; if (liste.SelectedIndex > 0) liste.SelectedIndex--; break;
                }
            };
            Deactivate += (s, e) => { if (!IsDisposed) Close(); };
            FormClosed += (s, e) => actuelle = null;
            Filtrer();
        }

        private void Filtrer()
        {
            var mots = Format.SansAccents(saisie.Text).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            liste.BeginUpdate();
            liste.Items.Clear();
            // les commandes dont le TITRE contient les mots passent en premier (« ram » : Libération de la RAM avant Démarrage)
            Func<Commande, int> score = c => mots.Sum(m => c.RechercheTitre.Contains(" " + m) ? 3 : c.RechercheTitre.Contains(m) ? 2 : 0);
            foreach (var c in commandes.Where(c => mots.All(m => c.Recherche.Contains(m))).Select((c, i) => new { c, i }).OrderByDescending(x => score(x.c)).ThenBy(x => x.i).Select(x => x.c)) liste.Items.Add(c);
            liste.EndUpdate();
            if (liste.Items.Count > 0) liste.SelectedIndex = 0;
        }

        private void Executer()
        {
            var c = liste.SelectedItem as Commande;
            if (c == null) return;
            Close();
            if (c.Executer != null) c.Executer();
        }

        private void DessinerElement(object s, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var c = (Commande)liste.Items[e.Index];
            var g = e.Graphics; var P = Theme.P; var r = e.Bounds;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(P.Carte)) g.FillRectangle(b, r);
            bool sel = (e.State & DrawItemState.Selected) != 0;
            if (sel)
            {
                Theme.RemplirDegrade(g, Theme.Melanger(P.Survol, P.Lueur1, 0.25), P.Survol, r.X + 4, r.Y + 2, r.Width - 8, r.Height - 4, 8);
                Theme.RemplirDegrade(g, P.Lueur1, P.Lueur2, r.X + 4, r.Y + 12, 3, r.Height - 24, 1.5f, true);
            }
            Theme.Icone(g, c.Icone, Theme.Icones, new Rectangle(r.X + 12, r.Y + 11, 24, 24), sel ? P.Lueur1 : P.Discret);
            Theme.Texte(g, c.Titre, Theme.CarteTitre, new Rectangle(r.X + 46, r.Y + 4, r.Width - 200, 22), P.Texte, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            Theme.Texte(g, c.Detail, Theme.Petit, new Rectangle(r.X + 47, r.Y + 25, r.Width - 200, 18), P.Discret, TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
            Theme.Texte(g, c.Lieu, Theme.Petit, new Rectangle(r.Right - 160, r.Y, 144, r.Height), P.Discret, TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            using (var b = new LinearGradientBrush(new Rectangle(0, 0, Width, 3), P.Lueur1, P.Lueur2, LinearGradientMode.Horizontal)) g.FillRectangle(b, 0, 0, Width, 3);
            using (var p = new Pen(P.Bordure)) { g.DrawRectangle(p, 0, 0, Width - 1, Height - 1); g.DrawLine(p, 0, 58, Width, 58); }
            Theme.Icone(g, 0xE721, Theme.IconesGrand, new Rectangle(14, 16, 28, 28), P.Discret);
            Theme.Texte(g, "Entrée : lancer   ·   ↑ ↓ : choisir   ·   Échap : fermer", Theme.Petit, new Rectangle(18, Height - 26, Width - 36, 20), P.Discret, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }
}
