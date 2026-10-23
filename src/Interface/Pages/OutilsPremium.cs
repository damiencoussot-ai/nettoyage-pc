// Nettoyage PC - outils Premium : analyse des plantages, santé de la batterie, gardien en temps réel et sauvegarde automatique,
// choix de la couleur de l'interface
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    // ================================================================ Petits éléments dessinés
    /// <summary>Chiffre clé : grand nombre et libellé, coloré selon la gravité.</summary>
    internal class TuileChiffre : Control, IThemable
    {
        public string Valeur = "—", Libelle = "";
        public Role Couleur = Role.Texte;
        public TuileChiffre() { Size = new Size(200, 78); SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }
        public void AppliquerTheme() { Invalidate(); }
        private static readonly Font PoliceValeur = new Font("Segoe UI Semibold", 22);
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.RemplirArrondi(g, P.Carte, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            Theme.Contour(g, P.Bordure, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            using (var b = new SolidBrush(P.Couleur(Couleur))) g.FillRectangle(b, 0, 12, 3, Height - 24);
            Theme.Texte(g, Valeur, PoliceValeur, new Rectangle(14, 4, Width - 20, 44), P.Couleur(Couleur), TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Theme.Texte(g, Libelle, Theme.Petit, new Rectangle(16, 46, Width - 22, 26), P.Discret, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
        }
    }

    /// <summary>Histogramme empilé par semaine (13 semaines) : système, applications, matériel.</summary>
    internal class GraphiqueSemaines : Control, IThemable
    {
        public List<int[]> Semaines = new List<int[]>();
        public GraphiqueSemaines() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }
        public void AppliquerTheme() { Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.RemplirArrondi(g, P.Carte, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            Theme.Contour(g, P.Bordure, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            Color[] couleurs = { P.Rouge, P.Orange, P.Lueur2 };
            string[] noms = { "Système (écrans bleus, arrêts)", "Applications", "Matériel, disques, affichage" };
            int xl = 16;
            for (int i = 0; i < 3; i++)
            {
                using (var b = new SolidBrush(couleurs[i])) g.FillEllipse(b, xl, 12, 9, 9);
                string n = Langue.T(noms[i]);
                Theme.Texte(g, n, Theme.Petit, new Rectangle(xl + 13, 6, 240, 20), P.Discret, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                xl += 13 + TextRenderer.MeasureText(n, Theme.Petit).Width + 18;
            }
            int haut = 34, bas = Height - 24;
            if (Semaines.Count == 0) return;
            int max = Math.Max(1, Semaines.Max(s => s.Sum()));
            float l = (Width - 32f) / Semaines.Count;
            for (int i = 0; i < Semaines.Count; i++)
            {
                float x = 16 + i * l + l * 0.18f, lb = Math.Max(3, l * 0.64f), y = bas;
                for (int k = 0; k < 3; k++)
                {
                    float hk = (float)(bas - haut) * Semaines[i][k] / max;
                    if (hk < 0.5f) continue;
                    Theme.RemplirArrondi(g, couleurs[k], x, y - hk, lb, hk, 2);
                    y -= hk;
                }
                if (Semaines[i].Sum() == 0) Theme.RemplirArrondi(g, P.Piste, x, bas - 2, lb, 2, 1);
            }
            Theme.Texte(g, "il y a 13 semaines", Theme.Petit, new Rectangle(16, bas + 4, 200, 18), P.Discret, TextFormatFlags.Left | TextFormatFlags.SingleLine);
            Theme.Texte(g, "cette semaine", Theme.Petit, new Rectangle(Width - 216, bas + 4, 200, 18), P.Discret, TextFormatFlags.Right | TextFormatFlags.SingleLine);
        }
    }

    // ================================================================ Analyse des plantages
    internal class FenetrePlantages : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly Etiquette lblResume, lblIndice, lblDetail, lblConseils, titreConseils;
        private readonly TuileChiffre tEcrans = new TuileChiffre { Libelle = "écrans bleus" }, tArrets = new TuileChiffre { Libelle = "arrêts brutaux" },
            tMateriel = new TuileChiffre { Libelle = "erreurs matérielles ou de disque" }, tApplis = new TuileChiffre { Libelle = "applications qui ont planté" };
        private readonly GraphiqueSemaines graph = new GraphiqueSemaines();
        private readonly ListeSombre lv = new ListeSombre { MultiSelect = false, ShowItemToolTips = true };
        private readonly Action<string, object> recevoir;
        private readonly Bouton bRelancer, bFiabilite, bFermer;

        public FenetrePlantages(FenetrePrincipale f) : base("Analyse des plantages", 1000, 760, true)
        {
            F = f; MinimizeBox = true; ShowInTaskbar = true; MinimumSize = new Size(820, 600);
            Controls.Add(new Etiquette("Analyse des plantages", Theme.Grand, Role.Texte, 20, 14));
            lblResume = new Etiquette("Lecture des journaux de Windows (90 derniers jours)...", Theme.CarteTitre, Role.AccentVif, 22, 54) { MaximumSize = new Size(700, 0) };
            lblIndice = new Etiquette("", Theme.PetitGras, Role.Discret, 700, 58);
            Controls.Add(lblResume); Controls.Add(lblIndice);
            foreach (var t in new[] { tEcrans, tArrets, tMateriel, tApplis }) Controls.Add(t);
            Controls.Add(graph);
            lv.Colonne("Quoi", 420); lv.Colonne("Type", 170); lv.Colonne("Fois", 60, HorizontalAlignment.Right); lv.Colonne("Dernière fois", 130);
            lv.ColonneElastique = 0; lv.ActiverTri();
            lv.SelectedIndexChanged += (s, e) => { if (lv.SelectedItems.Count > 0) { lblDetail.Text = (lv.SelectedItems[0].Tag as string) ?? ""; Disposer(); } };
            Controls.Add(lv);
            lblDetail = new Etiquette("Clique sur une ligne pour voir l'explication.", Theme.Normal, Role.Discret, 22, 0) { MaximumSize = new Size(940, 0) };
            titreConseils = new Etiquette("Que faire ?", Theme.CarteTitre, Role.Texte, 20, 0);
            lblConseils = new Etiquette("", Theme.Normal, Role.Texte, 22, 0) { MaximumSize = new Size(940, 0) };
            Controls.Add(lblDetail); Controls.Add(titreConseils); Controls.Add(lblConseils);
            bRelancer = new Bouton("Actualiser", false, 120, 34);
            bRelancer.Click += (s, e) => Lancer();
            bFiabilite = new Bouton("Moniteur de fiabilité de Windows", false, 260, 34);
            Infobulle.Definir(bFiabilite, "L'historique de fiabilité de Windows, jour par jour.");
            bFiabilite.Click += (s, e) => Programmes.Ouvrir(Path.Combine(Chemins.Systeme32, "perfmon.exe"), "/rel");
            bFermer = new Bouton("Fermer", false, 100, 34);
            bFermer.Click += (s, e) => Close();
            Controls.AddRange(new Control[] { bRelancer, bFiabilite, bFermer });
            CancelButton = bFermer;
            Resize += (s, e) => Disposer();
            Theme.Appliquer(this);
            recevoir = (cle, v) => { if (cle == "plantages" && !IsDisposed) Afficher(v as Dictionary<string, object>); };
            F.DonneesRecues += recevoir;
            FormClosed += (s, e) => F.DonneesRecues -= recevoir;
            Shown += (s, e) => { Disposer(); if (Programme.DossierAutotest == null) Lancer(); };
        }

        private void Disposer()
        {
            int w = ClientSize.Width, h = ClientSize.Height;
            lblIndice.Location = new Point(w - lblIndice.Width - 24, 60);
            lblResume.MaximumSize = new Size(Math.Max(300, w - lblIndice.Width - 70), 0);
            int y = Math.Max(lblResume.Bottom, 76) + 14;
            int lt = (w - 40 - 3 * 12) / 4;
            int x = 20;
            foreach (var t in new[] { tEcrans, tArrets, tMateriel, tApplis }) { t.Bounds = new Rectangle(x, y, lt, 78); x += lt + 12; }
            y += 90;
            graph.Bounds = new Rectangle(20, y, w - 40, 120);
            y += 132;
            int hBas = 58;
            lblConseils.MaximumSize = new Size(w - 44, 0); lblDetail.MaximumSize = new Size(w - 44, 0);
            int hConseils = lblConseils.Text == "" ? 0 : lblConseils.Height + 32;
            int hListe = Math.Max(120, h - y - hBas - hConseils - lblDetail.Height - 18);
            lv.Bounds = new Rectangle(20, y, w - 40, hListe);
            lblDetail.Location = new Point(22, lv.Bottom + 8);
            titreConseils.Location = new Point(20, lblDetail.Bottom + 12); titreConseils.Visible = lblConseils.Text != "";
            lblConseils.Location = new Point(22, titreConseils.Bottom + 6);
            bRelancer.Location = new Point(20, h - 50); bFiabilite.Location = new Point(150, h - 50); bFermer.Location = new Point(w - 120, h - 50);
        }

        private void Lancer()
        {
            lblResume.Text = "Lecture des journaux de Windows (90 derniers jours)..."; lblResume.RoleTexte = Role.AccentVif; lblResume.AppliquerTheme();
            if (!F.LancerAction("plantages")) { lblResume.Text = "Analyse non lancée (une autre action est peut-être en cours)."; lblResume.RoleTexte = Role.Orange; lblResume.AppliquerTheme(); }
        }

        private static Role RoleNiveau(int n) { return n == 2 ? Role.Rouge : n == 1 ? Role.Orange : n == 3 ? Role.Vert : Role.Texte; }

        private void Afficher(Dictionary<string, object> d)
        {
            if (d == null) return;
            int[] niveaux;
            var chiffres = AnalysePlantages.Chiffres(d, out niveaux);
            var tuiles = new[] { tEcrans, tArrets, tMateriel, tApplis };
            for (int i = 0; i < 4; i++) { tuiles[i].Valeur = chiffres[i].ToString(); tuiles[i].Couleur = RoleNiveau(niveaux[i]); tuiles[i].Invalidate(); }
            graph.Semaines = AnalysePlantages.Semaines(d);
            graph.Invalidate();
            double indice = AnalysePlantages.Indice(d);
            lblIndice.Text = indice >= 0 ? Langue.T("Indice de stabilité de Windows") + " : " + indice.ToString("0.0", Appli.Fr) + " / 10" : "";
            lblIndice.RoleTexte = indice >= 8 ? Role.Vert : indice >= 5 ? Role.Orange : Role.Rouge; lblIndice.AppliquerTheme();
            int niveau;
            lblResume.Text = AnalysePlantages.Resume(d, out niveau);
            lblResume.RoleTexte = RoleNiveau(niveau);
            lblResume.AppliquerTheme();

            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var l in AnalysePlantages.Lignes(d))
            {
                var it = lv.Ajouter(l.Detail, l.Quoi, l.Type, l.Fois, l.Derniere);
                if (l.Niveau == 1 || l.Niveau == 2) it.ForeColor = l.Niveau == 2 ? Theme.P.Rouge : Theme.P.Orange;
                it.SubItems[2].Tag = l.Nombre;
            }
            if (lv.Items.Count == 0) lv.Ajouter("", Langue.T("Rien à signaler sur les 90 derniers jours."), "", "", "").ForeColor = Theme.P.Vert;
            lv.EndUpdate();
            var conseils = AnalysePlantages.Conseils(d);
            lblConseils.Text = string.Join("\n", conseils);
            lblDetail.Text = "Clique sur une ligne pour voir l'explication.";
            Disposer();
        }
    }

    // ================================================================ Santé de la batterie
    internal class JaugeSante : Control, IThemable
    {
        public double Valeur = -1;
        public JaugeSante() { Size = new Size(190, 190); SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }
        public void AppliquerTheme() { Invalidate(); }
        private static readonly Font PoliceValeur = new Font("Segoe UI Light", 34);
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(14, 14, Width - 28, Height - 28);
            using (var p = new Pen(P.Piste, 14) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, r, 135, 270);
            if (Valeur >= 0)
            {
                Color c = Valeur >= 80 ? P.Vert : Valeur >= 60 ? P.Orange : P.Rouge;
                using (var p = new Pen(c, 14) { StartCap = LineCap.Round, EndCap = LineCap.Round }) g.DrawArc(p, r, 135, (float)(270 * Math.Max(1, Valeur) / 100));
            }
            Theme.Texte(g, Valeur >= 0 ? Valeur.ToString("0") + " %" : "—", PoliceValeur, new Rectangle(0, 40, Width, 70), P.Texte, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Theme.Texte(g, "SANTÉ", Theme.PetitGras, new Rectangle(0, 112, Width, 20), P.Discret, TextFormatFlags.HorizontalCenter | TextFormatFlags.SingleLine);
        }
    }

    internal class GraphiqueCapacite : Control, IThemable
    {
        public List<Tuple<DateTime, long, long>> Points = new List<Tuple<DateTime, long, long>>();
        public GraphiqueCapacite() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }
        public void AppliquerTheme() { Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.RemplirArrondi(g, P.Carte, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            Theme.Contour(g, P.Bordure, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 10);
            Theme.Texte(g, "Capacité à pleine charge au fil du temps (100 % = batterie neuve)", Theme.Petit, new Rectangle(14, 6, Width - 28, 20), P.Discret, TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            var pts = Points.Where(p => p.Item3 > 0).ToList();
            if (pts.Count < 2) { Theme.Texte(g, "Pas encore assez d'historique.", Theme.Normal, new Rectangle(14, 30, Width - 28, Height - 40), P.Discret, TextFormatFlags.Left | TextFormatFlags.VerticalCenter); return; }
            int haut = 30, bas = Height - 22;
            float x0 = 16, l = Width - 32;
            Func<double, float> Y = v => (float)(bas - (bas - haut) * Math.Max(0, Math.Min(110, v)) / 110.0);
            using (var p = new Pen(Color.FromArgb(70, P.Discret)) { DashStyle = DashStyle.Dot }) g.DrawLine(p, x0, Y(100), x0 + l, Y(100));
            double t0 = pts[0].Item1.Ticks, t1 = Math.Max(t0 + 1, pts[pts.Count - 1].Item1.Ticks);
            var ligne = pts.Select(p => new PointF(x0 + (float)((p.Item1.Ticks - t0) / (t1 - t0) * l), Y(100.0 * p.Item2 / p.Item3))).ToArray();
            using (var br = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), P.Lueur1, P.Lueur2, LinearGradientMode.Horizontal))
            using (var p = new Pen(br, 2.4f) { LineJoin = LineJoin.Round }) g.DrawLines(p, ligne);
            Theme.Texte(g, pts[0].Item1.ToString("MM/yyyy"), Theme.Petit, new Rectangle(14, bas + 2, 100, 18), P.Discret, TextFormatFlags.Left);
            Theme.Texte(g, pts[pts.Count - 1].Item1.ToString("MM/yyyy"), Theme.Petit, new Rectangle(Width - 114, bas + 2, 100, 18), P.Discret, TextFormatFlags.Right);
        }
    }

    internal class FenetreBatterie : FenetreTheme
    {
        private readonly JaugeSante jauge = new JaugeSante { Location = new Point(24, 70) };
        private readonly Etiquette lblEtat, lblDetails, lblConseils;
        private readonly GraphiqueCapacite graph = new GraphiqueCapacite { Location = new Point(24, 290), Size = new Size(712, 130) };

        public FenetreBatterie() : base("Santé de la batterie", 780, 640)
        {
            Controls.Add(new Etiquette("Santé de la batterie", Theme.Grand, Role.Texte, 20, 14));
            lblEtat = new Etiquette("Lecture du rapport de batterie de Windows...", Theme.CarteTitre, Role.AccentVif, 236, 82) { MaximumSize = new Size(500, 0) };
            lblDetails = new Etiquette("", Theme.Normal, Role.Texte, 238, 116) { MaximumSize = new Size(500, 0) };
            lblConseils = new Etiquette("", Theme.Normal, Role.Discret, 24, 434) { MaximumSize = new Size(712, 0) };
            Controls.AddRange(new Control[] { jauge, lblEtat, lblDetails, graph, lblConseils });
            var bRapport = new Bouton("Rapport complet de Windows", false, 240, 34) { Location = new Point(24, 556) };
            bRapport.Click += (s, e) =>
            {
                Cursor = Cursors.WaitCursor;
                string f = null; try { f = Batterie.RapportHtml(); } catch { }
                Cursor = Cursors.Default;
                if (f != null) Programmes.Ouvrir(f); else BoiteMessage.Info("Windows n'a pas pu créer le rapport de batterie (pas de batterie détectée ?).");
            };
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(636, 556), DialogResult = DialogResult.Cancel };
            Controls.Add(bRapport); Controls.Add(bFermer); CancelButton = bFermer;
            Theme.Appliquer(this);
            Shown += (s, e) =>
            {
                System.Threading.Tasks.Task.Run(() =>
                {
                    InfosBatterie r; try { r = Batterie.Lire(); } catch (Exception ex) { r = new InfosBatterie { Erreur = ex.Message }; }
                    try { BeginInvoke(new Action(() => Afficher(r))); } catch { }
                });
            };
        }

        private void Afficher(InfosBatterie r)
        {
            if (IsDisposed) return;
            if (!r.Presente)
            {
                lblEtat.Text = "Aucune batterie détectée : c'est sans doute un PC fixe.";
                lblEtat.RoleTexte = Role.Discret;
                lblDetails.Text = "Cet outil sert aux ordinateurs portables : il mesure l'usure de la batterie et son autonomie réelle.";
                graph.Visible = false; jauge.Visible = false;
                lblEtat.Location = new Point(24, 82); lblDetails.Location = new Point(26, 116);
                lblEtat.AppliquerTheme();
                return;
            }
            double s = r.Sante;
            jauge.Valeur = s; jauge.Invalidate();
            int niveau;
            lblEtat.Text = Batterie.Resume(r, out niveau);
            lblEtat.RoleTexte = niveau == 0 ? Role.Discret : niveau == 3 ? Role.Vert : niveau == 2 ? Role.Orange : Role.Rouge;
            lblEtat.AppliquerTheme();
            lblDetails.Text = string.Join("\n", Batterie.Details(r));
            graph.Points = r.Historique; graph.Invalidate();
            var c = Batterie.Conseils(r);
            lblConseils.Text = string.Join("\n", c.Select(x => "•  " + Langue.T(x)));
        }
    }

    // ================================================================ Gardien en temps réel et sauvegarde automatique
    internal class FenetreGardien : FenetreTheme
    {
        private readonly ReglagesGardien R = ReglagesGardien.Lire();
        private readonly Etiquette lblEtat, lblSauvegarde;
        private readonly Bouton bActiver;
        private readonly CaseACocher cDemarrage, cDisque, cNettoyage, cAntivirus, cMemoire, cChaleur, cArnaque, cSante, cEnergie, cSauvegarde;
        private readonly ListeDeroulante cbDest = new ListeDeroulante(330), cbFrequence = new ListeDeroulante(220);
        private readonly List<CaseACocher> casesDossiers = new List<CaseACocher>();
        private readonly List<string> racines = new List<string>();

        public static void Ouvrir(FenetrePrincipale f, bool sauvegarde = false)
        {
            using (var w = new FenetreGardien()) { if (sauvegarde) w.Shown += (s, e) => w.cSauvegarde.Focus(); w.ShowDialog(f); }
            f.Pages["Premium"].Montrer();
        }

        public FenetreGardien() : base("Gardien en temps réel", 760, 760)
        {
            Controls.Add(new Etiquette("Gardien en temps réel", Theme.Grand, Role.Texte, 20, 14));
            Controls.Add(new Etiquette("Une petite icône près de l'horloge veille sur ton PC et te prévient d'un problème, sans ralentir le PC (une vérification par minute).", Theme.Normal, Role.Discret, 22, 54) { MaximumSize = new Size(700, 0) });
            lblEtat = new Etiquette("", Theme.CarteTitre, Role.Vert, 22, 100) { MaximumSize = new Size(470, 0) };
            bActiver = new Bouton("Activer le gardien", true, 210, 38) { Location = new Point(516, 94) };
            bActiver.Click += (s, e) => Basculer();
            Controls.Add(lblEtat); Controls.Add(bActiver);

            int y = 152;
            Controls.Add(new Etiquette("Me prévenir quand :", Theme.CarteTitre, Role.Texte, 20, y)); y += 30;
            cDemarrage = Case("un programme s'ajoute au démarrage de Windows", R.Demarrage, ref y);
            cDisque = Case(Langue.F("le disque de Windows est presque plein (moins de {0} % libres)", R.SeuilDisque), R.Disque, ref y);
            cNettoyage = Case("et faire alors un nettoyage léger automatique (temporaires et caches : jamais la Corbeille ni les documents)", R.NettoyageAuto, ref y, 46);
            cAntivirus = Case("l'antivirus est désactivé", R.Antivirus, ref y);
            cMemoire = Case("la mémoire vive est saturée", R.Memoire, ref y);
            cChaleur = Case("la carte graphique surchauffe (plus de 90 °C)", R.Temperature, ref y);
            cArnaque = Case("un logiciel de prise en main à distance s'ouvre (anti-arnaque)", R.Arnaque, ref y);
            cSante = Case("le matériel montre des signes de faiblesse (erreurs du disque, du processeur, arrêts brutaux)", R.Sante, ref y);
            y += 6;
            cEnergie = Case("Estimer la consommation électrique du PC et de chaque application (Coût en électricité)", R.Energie, ref y);
            cDisque.CheckedChanged += (s, e) => cNettoyage.Enabled = cDisque.Checked;
            cNettoyage.Enabled = cDisque.Checked;

            y += 14;
            Controls.Add(new Etiquette("Sauvegarde automatique", Theme.CarteTitre, Role.Texte, 20, y)); y += 30;
            cSauvegarde = Case("Sauvegarder automatiquement dès que ma clé USB ou mon disque de sauvegarde est branché", R.SauvegardeAuto, ref y);
            Controls.Add(new Etiquette("Sur :", Theme.Normal, Role.Texte, 46, y + 4));
            cbDest.Location = new Point(110, y);
            Controls.Add(cbDest);
            cbFrequence.Location = new Point(460, y);
            cbFrequence.Items.AddRange(new object[] { Langue.T("au plus une fois par jour"), Langue.T("une fois par semaine") });
            cbFrequence.SelectedIndex = R.SauvegardeJours >= 7 ? 1 : 0;
            Controls.Add(cbFrequence);
            y += 40;
            Controls.Add(new Etiquette("Dossiers :", Theme.Normal, Role.Texte, 46, y + 2));
            int x = 130;
            foreach (var kv in new[] { new[] { "Documents", "Documents" }, new[] { "Images", "Images" }, new[] { "Bureau", "Bureau" }, new[] { "Videos", "Vidéos" }, new[] { "Musique", "Musique" } })
            {
                var c = new CaseACocher(kv[1], x, y) { Checked = R.SauvegardeDossiers.Contains(kv[0]), Tag = kv[0] };
                Controls.Add(c); casesDossiers.Add(c);
                x += c.PreferredSize.Width + 18;
            }
            y += 34;
            lblSauvegarde = new Etiquette("", Theme.Petit, Role.Discret, 46, y) { MaximumSize = new Size(680, 0) };
            Controls.Add(lblSauvegarde);
            cSauvegarde.CheckedChanged += (s, e) => ActualiserSauvegarde();
            RemplirDestinations();

            var bEnregistrer = new Bouton("Enregistrer", true, 150, 36) { Location = new Point(456, 668) };
            bEnregistrer.Click += (s, e) => { if (Enregistrer()) { DialogResult = DialogResult.OK; } };
            var bFermer = new Bouton("Annuler", false, 120, 36) { Location = new Point(616, 668), DialogResult = DialogResult.Cancel };
            Controls.Add(bEnregistrer); Controls.Add(bFermer); CancelButton = bFermer;
            ClientSize = new Size(ClientSize.Width, Math.Max(lblSauvegarde.Bottom + 90, 640));
            bEnregistrer.Top = bFermer.Top = ClientSize.Height - 56;
            Theme.Appliquer(this);
            ActualiserEtat(); ActualiserSauvegarde();
        }

        private CaseACocher Case(string texte, bool coche, ref int y, int x = 22)
        {
            var c = new CaseACocher(texte, x, y) { Checked = coche, AutoSize = false, Size = new Size(720 - x, 24) };
            Controls.Add(c); y += 30;
            return c;
        }

        private void RemplirDestinations()
        {
            string systeme = Path.GetPathRoot(Chemins.Windows ?? "C:\\");
            string actuelle = Gardien.TrouverDestination(R.SauvegardeId);
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || string.Equals(d.Name, systeme, StringComparison.OrdinalIgnoreCase)) continue;
                    if (d.DriveType != DriveType.Removable && d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Network) continue;
                    cbDest.Items.Add(d.Name.TrimEnd('\\') + "  " + (d.DriveType == DriveType.Removable ? Langue.T("Clé USB / carte") : Langue.T("Disque")) + (d.VolumeLabel != "" ? " « " + d.VolumeLabel + " »" : "") + " — " + Format.Taille(d.AvailableFreeSpace) + " " + Langue.T("libres"));
                    racines.Add(d.RootDirectory.FullName);
                }
                catch { }
            }
            if (racines.Count == 0) { cbDest.Items.Add(Langue.T("Branche une clé USB ou un disque externe")); racines.Add(""); }
            int i = actuelle == null ? -1 : racines.FindIndex(r => string.Equals(r, actuelle, StringComparison.OrdinalIgnoreCase));
            if (i < 0) i = racines.FindIndex(r => { try { return r != "" && new DriveInfo(r).DriveType == DriveType.Removable; } catch { return false; } });
            cbDest.SelectedIndex = Math.Max(0, i);
        }

        private void ActualiserEtat()
        {
            bool marche = Gardien.EnCours, actif = R.Actif;
            lblEtat.Text = marche ? "● Le gardien veille sur ce PC." : actif ? "● Le gardien est activé : il démarre à l'ouverture de session." : "Le gardien est arrêté.";
            lblEtat.RoleTexte = marche || actif ? Role.Vert : Role.Discret; lblEtat.AppliquerTheme();
            bActiver.Text = actif ? "Désactiver le gardien" : "Activer le gardien";
            bActiver.Principal = !actif; bActiver.Invalidate();
        }

        private void ActualiserSauvegarde()
        {
            bool on = cSauvegarde.Checked;
            cbDest.Enabled = on; cbFrequence.Enabled = on; foreach (var c in casesDossiers) c.Enabled = on;
            var der = Gardien.DerniereSauvegarde();
            DateTime d;
            string derniere = der != null && DateTime.TryParse(Json.Texte(der, "Date"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d)
                ? Langue.T("Dernière sauvegarde automatique") + " : " + d.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + " (" + Format.Taille(Json.Long(der, "Octets")) + " " + Langue.T("de nouveautés") + ")."
                : Langue.T("Aucune sauvegarde automatique pour l'instant.");
            lblSauvegarde.Text = Langue.F("Seuls les fichiers nouveaux ou modifiés sont copiés, dans le dossier « {0} ». Un fichier effacé du PC reste dans la sauvegarde. La copie se fait en arrière-plan : la clé peut être débranchée dès que la notification « terminée » apparaît.", "NettoyagePC-Sauvegarde\\" + Environment.MachineName + "\\Automatique") + "\n" + derniere;
        }

        private void Basculer()
        {
            if (R.Actif)
            {
                Gardien.Desactiver(); R.Actif = false;
                BoiteMessage.Info("Gardien désactivé : il ne démarrera plus avec Windows. Tu peux le réactiver ici à tout moment.");
            }
            else
            {
                if (!Enregistrer(false)) return;
                string r = Gardien.Activer();
                if (r != "") { BoiteMessage.Attention(Langue.T("Le gardien n'a pas pu être activé") + " : " + Langue.T(r)); return; }
                R.Actif = true;
                BoiteMessage.Info("Gardien activé ✔\n\nSon icône apparaît près de l'horloge (parfois cachée derrière la petite flèche « ^ »). Il démarrera tout seul à chaque ouverture de session.");
            }
            System.Threading.Thread.Sleep(300);
            ActualiserEtat();
        }

        private bool Enregistrer(bool prevenir = true)
        {
            var r = ReglagesGardien.Lire();
            r.Demarrage = cDemarrage.Checked; r.Disque = cDisque.Checked; r.NettoyageAuto = cNettoyage.Checked; r.Antivirus = cAntivirus.Checked; r.Memoire = cMemoire.Checked; r.Temperature = cChaleur.Checked; r.Arnaque = cArnaque.Checked; r.Sante = cSante.Checked; r.Energie = cEnergie.Checked;
            r.SauvegardeJours = cbFrequence.SelectedIndex == 1 ? 7 : 1;
            r.SauvegardeDossiers = casesDossiers.Where(c => c.Checked).Select(c => (string)c.Tag).ToList();
            r.SauvegardeAuto = cSauvegarde.Checked;
            if (r.SauvegardeAuto)
            {
                string racine = cbDest.SelectedIndex >= 0 && cbDest.SelectedIndex < racines.Count ? racines[cbDest.SelectedIndex] : "";
                if (racine == "" || !Directory.Exists(racine)) { BoiteMessage.Info("Branche la clé USB ou le disque externe de sauvegarde, puis choisis-le dans la liste."); return false; }
                if (r.SauvegardeDossiers.Count == 0) { BoiteMessage.Info("Choisis au moins un dossier à sauvegarder."); return false; }
                try
                {
                    r.SauvegardeId = Gardien.MarquerDestination(racine, string.Equals(Gardien.TrouverDestination(r.SauvegardeId), racine, StringComparison.OrdinalIgnoreCase) ? r.SauvegardeId : "");
                    var di = new DriveInfo(racine); r.SauvegardeLibelle = di.VolumeLabel != "" ? di.VolumeLabel : racine.TrimEnd('\\');
                }
                catch (Exception ex) { BoiteMessage.Attention("Impossible de préparer cette destination : " + ex.Message); return false; }
            }
            r.Ecrire();
            Gardien.Recharger();
            if (prevenir && r.SauvegardeAuto && !r.Actif && BoiteMessage.Question("La sauvegarde automatique est faite par le gardien, qui est arrêté. L'activer maintenant ?"))
            {
                string e = Gardien.Activer();
                if (e != "") BoiteMessage.Attention(Langue.T("Le gardien n'a pas pu être activé") + " : " + Langue.T(e));
            }
            return true;
        }
    }

    // ================================================================ Couleur de l'interface (Réglages)
    internal class SelecteurAccent : Control, IThemable
    {
        private readonly FenetrePrincipale F;
        private int survol = -1;
        private const int D = 34, Pas = 44;
        public SelecteurAccent(FenetrePrincipale f)
        {
            F = f; Size = new Size(Pas * Theme.Accents.Count, D + 8); Cursor = Cursors.Hand;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
        }
        public void AppliquerTheme() { Invalidate(); }
        private int Index(Point p) { int i = p.X / Pas; return p.Y >= 0 && p.Y < Height && i >= 0 && i < Theme.Accents.Count ? i : -1; }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            int i = Index(e.Location);
            if (i != survol)
            {
                survol = i; Invalidate();
                if (i >= 0) { var a = Theme.Accents[i]; Infobulle.T.SetToolTip(this, Langue.T(a.Nom) + (a.Premium && !Licence.Premium ? " (Premium)" : "")); }
            }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { survol = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            int i = Index(e.Location);
            if (i < 0) return;
            var a = Theme.Accents[i];
            if (a.Premium && !Licence.Premium) { FenetreAchat.Ouvrir(F); return; }
            F.ChangerAccent(a.Cle);
            base.OnMouseClick(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < Theme.Accents.Count; i++)
            {
                var a = Theme.Accents[i];
                float x = i * Pas + 4, y = 4;
                Color c1 = Theme.EstSombre ? a.L1Sombre : a.L1Clair, c2 = Theme.EstSombre ? a.L2Sombre : a.L2Clair;
                using (var b = new LinearGradientBrush(new RectangleF(x, y, D, D), c1, c2, LinearGradientMode.ForwardDiagonal)) g.FillEllipse(b, x, y, D, D);
                if (a.Cle == Theme.AccentActuel) using (var p = new Pen(P.Texte, 2.2f)) g.DrawEllipse(p, x - 3, y - 3, D + 6, D + 6);
                else if (i == survol) using (var p = new Pen(P.Discret, 1.4f)) g.DrawEllipse(p, x - 3, y - 3, D + 6, D + 6);
                if (a.Premium && !Licence.Premium)
                {
                    using (var b = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) g.FillEllipse(b, x + D / 2f - 9, y + D / 2f - 9, 18, 18);
                    using (var p = new Pen(Color.White, 1.6f)) { g.DrawArc(p, x + D / 2f - 3.5f, y + D / 2f - 6, 7, 7, 180, 180); }
                    using (var b = new SolidBrush(Color.White)) g.FillRectangle(b, x + D / 2f - 4.5f, y + D / 2f - 2, 9, 7);
                }
            }
        }
    }
}
