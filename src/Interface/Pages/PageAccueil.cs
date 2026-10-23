// Nettoyage PC - page Accueil : jauge de santé, mesures en direct, points à vérifier
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    /// <summary>Noyau (style 4.5) : anneau holographique. Quatre secteurs (stockage, mémoire, sécurité, matériel),
    /// graduations, anneaux qui tournent lentement, score au centre ; balayage pendant l'analyse.</summary>
    internal class Jauge : Control, IThemable
    {
        public double Score = -1, ScoreAffiche = -1, Angle, Pulsation;
        /// <summary>Santé de chaque secteur (0-100, -1 : inconnu) : stockage, mémoire, sécurité, matériel.</summary>
        public double[] Secteurs = { -1, -1, -1, -1 };
        public static readonly string[] NomsSecteurs = { "STOCKAGE", "MÉMOIRE", "SÉCURITÉ", "MATÉRIEL" };
        public string Etat = "";
        public Jauge()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Size = new Size(300, 300);
        }
        public void AppliquerTheme() { Invalidate(); }

        private static Color CouleurNiveau(double v, Palette P) { return v >= 80 ? P.Lueur1 : v >= 55 ? P.Orange : P.Rouge; }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(P.Fond); g.SmoothingMode = SmoothingMode.AntiAlias;
            float cx = Width / 2f, cy = Height / 2f, R = Math.Min(Width, Height) / 2f - 4;
            float rotation = (float)(Pulsation * 4) % 360;
            Func<float, RectangleF> Cercle = r => new RectangleF(cx - r, cy - r, 2 * r, 2 * r);
            // halo qui « respire » lentement derrière l'anneau
            using (var halo = new GraphicsPath())
            {
                halo.AddEllipse(Cercle(R * 0.98f));
                int alpha = (int)((Theme.EstSombre ? 62 : 34) + 16 * Math.Sin(Pulsation * 0.35));
                using (var pb = new PathGradientBrush(halo) { CenterColor = Color.FromArgb(Math.Max(0, alpha), P.Lueur1), SurroundColors = new[] { Color.FromArgb(0, P.Lueur1) } })
                    g.FillPath(pb, halo);
            }
            // graduations
            for (int i = 0; i < 72; i++)
            {
                double a = i * 5 * Math.PI / 180;
                bool grand = i % 6 == 0;
                float r1 = R - (grand ? 9 : 6), r2 = R - 2;
                using (var s = new Pen(Color.FromArgb(grand ? 150 : 70, P.Lueur1), grand ? 1.4f : 1f))
                    g.DrawLine(s, (float)(cx + r1 * Math.Cos(a)), (float)(cy + r1 * Math.Sin(a)), (float)(cx + r2 * Math.Cos(a)), (float)(cy + r2 * Math.Sin(a)));
            }
            // anneau fin qui tourne lentement, avec son repère
            float rr = R - 16;
            using (var s = new Pen(Color.FromArgb(40, P.Lueur1), 1)) g.DrawEllipse(s, Cercle(rr));
            double ar = (rotation - 90) * Math.PI / 180;
            // traînée lumineuse derrière le repère qui tourne
            for (int i = 1; i <= 10; i++)
                using (var s = new Pen(Color.FromArgb((int)(150 * (1 - i / 11f)), P.Lueur1), 2f)) g.DrawArc(s, Cercle(rr), rotation - 90 - i * 3.2f, 3.4f);
            using (var b = new SolidBrush(P.Lueur1)) g.FillEllipse(b, (float)(cx + rr * Math.Cos(ar)) - 3, (float)(cy + rr * Math.Sin(ar)) - 3, 6, 6);
            // quatre secteurs
            float rs = R - 32; var rect = Cercle(rs);
            double progres = Score > 0 && ScoreAffiche >= 0 ? Math.Min(1, ScoreAffiche / Score) : 0;
            for (int k = 0; k < 4; k++)
            {
                float debut = -90 + k * 90 + 5;
                using (var piste = new Pen(Color.FromArgb(34, P.Lueur1), 11)) g.DrawArc(piste, rect, debut, 80);
                double v = Secteurs[k];
                if (v >= 0 && progres > 0)
                    using (var s = new Pen(CouleurNiveau(v, P), 11)) g.DrawArc(s, rect, debut, (float)(80 * Math.Max(0.02, v / 100) * progres));
            }
            if (ScoreAffiche < 0)
                for (int k = 0; k < 16; k++)
                    using (var s = new Pen(Color.FromArgb(Math.Max(0, 230 - k * 14), P.Lueur1), 11)) g.DrawArc(s, rect, (float)(Angle - k * 4), 4.5f);
            // anneau segmenté intérieur (tourne en sens inverse)
            float ri = R - 58;
            for (int k = 0; k < 12; k++)
                using (var s = new Pen(Color.FromArgb(k % 2 == 0 ? 120 : 45, P.Lueur1), 2)) g.DrawArc(s, Cercle(ri), -rotation * 1.6f + k * 30 + 3, 24);
            // disque central
            float rd = R - 68;
            using (var b = new SolidBrush(P.Carte)) g.FillEllipse(b, Cercle(rd));
            using (var s = new Pen(Color.FromArgb(80, P.Lueur1), 1)) g.DrawEllipse(s, Cercle(rd));
            string haut = Langue.T(ScoreAffiche < 0 ? "ANALYSE" : "INTÉGRITÉ"), texte = ScoreAffiche < 0 ? "··" : ((int)Math.Round(ScoreAffiche)).ToString();
            var th = TextRenderer.MeasureText(haut, Theme.Mono);
            TextRenderer.DrawText(g, haut, Theme.Mono, new Point((int)(cx - th.Width / 2f), (int)(cy - 52)), P.Discret);
            var ts = TextRenderer.MeasureText(texte, Theme.Score);
            TextRenderer.DrawText(g, texte, Theme.Score, new Point((int)(cx - ts.Width / 2f), (int)(cy - ts.Height / 2f - 2)), ScoreAffiche < 0 ? P.Discret : P.Texte);
            if (Etat != "" && ScoreAffiche >= 0)
            {
                string et = Langue.T(Etat);
                var te = TextRenderer.MeasureText(et, Theme.Petit);
                TextRenderer.DrawText(g, et, Theme.Petit, new Point((int)(cx - te.Width / 2f), (int)(cy + 34)), Score >= 80 ? P.Lueur1 : Score >= 50 ? P.Orange : P.Rouge);
            }
            // noms et valeurs des secteurs, dans les coins
            for (int k = 0; k < 4; k++)
            {
                string nom = Langue.T(NomsSecteurs[k]).ToUpper(Langue.Culture), val = Secteurs[k] >= 0 ? ((int)Math.Round(Secteurs[k])) + " %" : "—";
                var tn = TextRenderer.MeasureText(nom, Theme.Mono); var tv = TextRenderer.MeasureText(val, Theme.PetitGras);
                bool droite = k == 0 || k == 1, bas = k == 1 || k == 2;
                int x = droite ? Width - Math.Max(tn.Width, tv.Width) - 2 : 2, y = bas ? Height - tn.Height - tv.Height - 2 : 2;
                Color c = Secteurs[k] >= 0 && Secteurs[k] < 80 ? CouleurNiveau(Secteurs[k], P) : P.Discret;
                TextRenderer.DrawText(g, nom, Theme.Mono, new Point(droite ? Width - tn.Width - 2 : x, y), c);
                TextRenderer.DrawText(g, val, Theme.PetitGras, new Point(droite ? Width - tv.Width - 2 : x, y + tn.Height), Secteurs[k] >= 0 && Secteurs[k] < 80 ? c : P.Texte);
            }
        }
    }

    /// <summary>Mesure en direct (style Noyau) : cadre fin, nom technique, valeur et tracé des dernières valeurs.</summary>
    internal class Mesure : Control, IThemable
    {
        public string Titre, TexteValeur = "...";
        public double Valeur, Cible;
        public readonly List<double> Histo = new List<double>();
        private static readonly Font PoliceValeur = new Font(Theme.CarteTitre.FontFamily, 12.5f, FontStyle.Regular);
        public Mesure(string titre)
        {
            Titre = titre; Size = new Size(150, 62);
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
        public void AppliquerTheme() { Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(P.Fond); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(P.Carte)) g.FillRectangle(b, 0, 0, Width - 1, Height - 1);
            using (var s = new Pen(P.Bordure)) g.DrawRectangle(s, 0, 0, Width - 1, Height - 1);
            string t = Langue.T(Titre).ToUpper(Langue.Culture);
            TextRenderer.DrawText(g, t, Theme.Mono, new Rectangle(9, 6, Width - 14, 16), P.Discret, TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            string tv = Langue.T(TexteValeur);
            TextRenderer.DrawText(g, tv, PoliceValeur, new Rectangle(8, 21, Width - 12, 22), P.Texte, TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            double v = Math.Min(100, Math.Max(0, Valeur));
            Color col = v >= 90 ? P.Rouge : v >= 70 ? P.Orange : P.Lueur1;
            int haut = 44, bas = Height - 6, hz = bas - haut;
            var vals = new List<double>(Histo) { v };
            if (vals.Count < 2) return;
            int n = vals.Count;
            float pas = (Width - 18) / 29f, x0 = (Width - 9) - pas * (n - 1);
            var pts = new PointF[n];
            for (int i = 0; i < n; i++) pts[i] = new PointF(x0 + i * pas, (float)(bas - hz * vals[i] / 100));
            using (var s = new Pen(col, 1.4f)) g.DrawLines(s, pts);
            using (var b = new SolidBrush(col)) g.FillEllipse(b, pts[n - 1].X - 2.5f, pts[n - 1].Y - 2.5f, 5, 5);
        }
    }

    internal class PointBilan { public string Etat, Texte, Bouton; public Cible Cible; }

    internal class PageAccueil : Page
    {
        private readonly Jauge jauge = new Jauge { Location = new Point(16, 0) };
        private readonly Etiquette etatTitre, etatCourt, etatTexte, lblPoints;
        private readonly Bouton bUnClic, bAnalyse, bActualiser;
        private readonly Mesure mCpu, mRam, mDisque, mTemp;
        private readonly FlowLayoutPanel listeBilan;
        private readonly Timer minuteurMesures = new Timer { Interval = 2000 }, minuteurAnim = new Timer { Interval = 16 }, minuteurPulse = new Timer { Interval = 60 };
        private bool bilanEnCours;
        private List<PointBilan> points = new List<PointBilan>();
        public List<PointBilan> Points { get { return points; } }
        public int ScoreActuel { get { return (int)Math.Round(jauge.Score); } }
        public bool BilanEnCours { get { return bilanEnCours; } }
        public event Action BilanPret;
        private int compteurTemp, alerteTemp;
        private string sourceTemp = "";

        public PageAccueil(FenetrePrincipale f) : base(f, "Accueil", "Accueil", "L'état de ton PC en un coup d'œil.", 0xE80F)
        {
            Controls.Add(jauge);
            etatTitre = Texte(Profil.Accueil(f.Reglages.Pseudo), 336, 10, Role.Texte, Theme.Grand);
            etatCourt = Texte("Analyse de ton PC...", 338, 48, Role.AccentVif, Theme.CarteTitre);
            etatTexte = Texte("", 338, 74); etatTexte.MaximumSize = new Size(600, 0);
            bUnClic = BoutonAction("Optimiser en 1 clic", true, 230, 46, 338, 146, "complet");
            bUnClic.Font = Theme.CarteTitre;
            Infobulle.Definir(bUnClic, "Point de restauration, nettoyage complet, disques, RAM, bilans d'optimisation et de sécurité.");
            bAnalyse = BoutonAction("Analyser sans rien modifier", false, 230, 46, 578, 146, "analyse");
            mCpu = NouvelleMesure("Processeur", 338); mRam = NouvelleMesure("Mémoire vive", 494);
            mDisque = NouvelleMesure("Disque " + (Environment.GetEnvironmentVariable("SystemDrive") ?? "C:"), 650); mTemp = NouvelleMesure("Température", 806);
            lblPoints = Titre("Améliorations proposées", 28, 314);
            lblPoints.Font = Theme.MonoGras; lblPoints.RoleTexte = Role.Discret; lblPoints.AppliquerTheme();
            bActualiser = NouveauBouton("Actualiser", false, 100, 28, 260, 308, MettreAJourBilan);
            listeBilan = new FlowLayoutPanel { Location = new Point(28, 344), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.P.Fond };
            Controls.Add(listeBilan);
            Resize += (s, e) => { AjusterBilan(); PlacerEntete(); };
            minuteurMesures.Tick += (s, e) => { if (F.PageActive == "Accueil" && F.WindowState != FormWindowState.Minimized) MettreAJourMesures(); };
            minuteurAnim.Tick += (s, e) => Animer();
            minuteurPulse.Tick += (s, e) =>
            {
                if (F.PageActive != "Accueil" || F.WindowState == FormWindowState.Minimized || jauge.ScoreAffiche < 0 || !Animations.Actives) return;
                jauge.Pulsation += 0.16; jauge.Invalidate();
            };
            F.ActionFinie += cle => { MettreAJourBilan(); MettreAJourMesures(); };
        }

        private Mesure NouvelleMesure(string titre, int x) { var m = new Mesure(titre) { Location = new Point(x, 206) }; Controls.Add(m); return m; }

        public override void ThemeChange() { listeBilan.BackColor = Theme.P.Fond; AfficherBilan(); }

        public override void PremiereFois()
        {
            double t = LireTemperature(out sourceTemp);
            if (sourceTemp == "gpu") { mTemp.Titre = "Carte graphique"; Infobulle.Definir(mTemp, "Température de la carte graphique (au repos : 30-50 °C, en jeu : 60-85 °C)."); }
            else if (sourceTemp == "systeme") Infobulle.Definir(mTemp, "Capteur de la carte mère vu par Windows : indicatif. La température exacte du processeur demande l'outil du fabricant.");
            else mTemp.Visible = false;
            MettreAJourMesures();
            MettreAJourBilan();
            minuteurMesures.Start(); minuteurPulse.Start();
        }

        public void SimulationChangee(bool simu) { bUnClic.Text = simu ? "Simuler en 1 clic" : "Optimiser en 1 clic"; }

        // ------------------------------------------------------------ Mesures
        public static double LireTemperature(out string source) { return InfosPc.Temperature(out source); }

        private void MettreAJourMesures()
        {
            double cpu = Natif.Processeur();
            if (cpu >= 0) { mCpu.Cible = cpu; mCpu.TexteValeur = cpu.ToString("0") + " %"; }
            ulong tot, dispo; Natif.Memoire(out tot, out dispo);
            if (tot > 0)
            {
                double util = tot - dispo;
                mRam.Cible = 100 * util / tot;
                mRam.TexteValeur = (util / 1073741824.0).ToString("0.0", Appli.Fr) + " / " + (tot / 1073741824.0).ToString("0", Appli.Fr) + " Go";
            }
            try
            {
                var d = new DriveInfo(Environment.GetEnvironmentVariable("SystemDrive") ?? Path.GetPathRoot(Chemins.Windows));
                if (d.IsReady && d.TotalSize > 0)
                {
                    mDisque.Cible = 100.0 * (d.TotalSize - d.AvailableFreeSpace) / d.TotalSize;
                    mDisque.TexteValeur = Format.Taille(d.AvailableFreeSpace) + " libres";
                }
            }
            catch { }
            compteurTemp++;
            if (mTemp.Visible && compteurTemp % 2 == 1)
            {
                string src; double t = LireTemperature(out src);
                if (t > 0)
                {
                    mTemp.Cible = t; mTemp.TexteValeur = t.ToString("0") + " °C";
                    alerteTemp = t >= 90 ? alerteTemp + 1 : 0;
                    if (alerteTemp == 3 && !F.ActionEnCours) F.Statut("Température élevée : " + t.ToString("0") + " °C. Vérifie que les ventilateurs tournent et que l'air circule autour du PC (poussière).", false, Role.Rouge);
                }
            }
            foreach (var m in new[] { mCpu, mRam, mDisque, mTemp })
            {
                if (m.TexteValeur == "...") continue;
                m.Histo.Add(m.Valeur);
                while (m.Histo.Count > 29) m.Histo.RemoveAt(0);
                m.Invalidate();
            }
            minuteurAnim.Start();
        }

        private void Animer()
        {
            bool actif = false;
            if (bilanEnCours && jauge.ScoreAffiche < 0) { jauge.Angle = (jauge.Angle + 7) % 360; jauge.Invalidate(); actif = true; }
            else if (jauge.Score >= 0)
            {
                if (jauge.ScoreAffiche < 0) jauge.ScoreAffiche = 0;
                double ecart = jauge.Score - jauge.ScoreAffiche;
                if (Math.Abs(ecart) > 0.3) { jauge.ScoreAffiche += ecart * 0.12; jauge.Invalidate(); actif = true; }
                else if (jauge.ScoreAffiche != jauge.Score) { jauge.ScoreAffiche = jauge.Score; jauge.Invalidate(); }
            }
            foreach (var m in new[] { mCpu, mRam, mDisque, mTemp })
            {
                double e = m.Cible - m.Valeur;
                if (Math.Abs(e) > 0.2) { m.Valeur += e * 0.18; m.Invalidate(); actif = true; }
            }
            if (!actif) minuteurAnim.Stop();
        }

        // ------------------------------------------------------------ Bilan
        public void MettreAJourBilan()
        {
            if (bilanEnCours) return;
            bilanEnCours = true; bActualiser.Enabled = false;
            if (jauge.Score < 0) minuteurAnim.Start();
            EnFond(BilanPc.Collecter, d => { bilanEnCours = false; bActualiser.Enabled = true; if (d != null) ConstruireBilan(d); });
        }

        private void ConstruireBilan(DonneesBilan d)
        {
            var pts = BilanPc.Points(d).Select(p => new PointBilan { Etat = p.Etat, Texte = p.Texte, Bouton = p.Bouton, Cible = VersCible(p.Cible) }).ToList();
            jauge.Score = BilanPc.Score(pts.Select(p => p.Etat));
            var ordre = new Dictionary<string, int> { { "probleme", 0 }, { "attention", 1 }, { "ok", 2 } };
            points = pts.OrderBy(p => ordre[p.Etat]).ToList();
            int nbP = pts.Count(p => p.Etat == "probleme"), nbA = pts.Count(p => p.Etat == "attention");
            etatTitre.Text = Profil.Accueil(F.Reglages.Pseudo);
            if (nbP > 0) { etatCourt.Text = "Ton PC a besoin d'attention"; etatCourt.RoleTexte = Role.Rouge; jauge.Etat = "a besoin d'attention"; }
            else if (nbA > 0) { etatCourt.Text = "Ton PC peut être amélioré"; etatCourt.RoleTexte = Role.Orange; jauge.Etat = "à améliorer"; }
            else { etatCourt.Text = "Ton PC est en pleine forme"; etatCourt.RoleTexte = Role.Vert; jauge.Etat = "en pleine forme"; }
            etatCourt.AppliquerTheme();
            CalculerSecteurs(d, pts);
            etatTexte.Text = nbP == 0 && nbA == 0 ? "Rien à corriger pour l'instant. L'entretien automatique s'occupe du reste."
                : nbP > 0 ? Langue.F(nbP == 1 ? "1 point à régler en priorité. Clique sur « Optimiser en 1 clic » pour tout faire d'un coup, ou choisis ci-dessous." : "{0} points à régler en priorité. Clique sur « Optimiser en 1 clic » pour tout faire d'un coup, ou choisis ci-dessous.", nbP)
                : Langue.F(nbA == 1 ? "1 amélioration possible. Clique sur « Optimiser en 1 clic » pour tout faire d'un coup, ou choisis ci-dessous." : "{0} améliorations possibles. Clique sur « Optimiser en 1 clic » pour tout faire d'un coup, ou choisis ci-dessous.", nbA);
            PlacerEntete();
            FichierSur.AjouterLigne(Chemins.Fichier("scores.csv"), DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ";" + (int)jauge.Score);
            AfficherBilan();
            minuteurAnim.Start();
            try { var bp = BilanPret; if (bp != null) bp(); } catch (Exception ex) { JournalDemarrage.Erreur("bilan", ex); }
            var h = F.Pages["Historique"] as PageHistorique;
            if (F.PageActive == "Historique" && h != null) h.Charger();
        }

        /// <summary>Le texte d'état peut tenir sur plusieurs lignes : boutons, mesures et liste se placent dessous, sans chevauchement.</summary>
        private void PlacerEntete()
        {
            etatTexte.MaximumSize = new Size(Math.Max(320, Math.Min(620, ClientSize.Width - 338 - 30)), 0);
            // les positions sont relatives au défilement : la page peut être descendue quand le bilan se rafraîchit
            int y = Math.Max(110 + AutoScrollPosition.Y, etatTexte.Bottom + 14);
            bUnClic.Top = bAnalyse.Top = y;
            foreach (var m in new[] { mCpu, mRam, mDisque, mTemp }) m.Top = bUnClic.Bottom + 14;
            int haut = Math.Max(jauge.Bottom, mCpu.Bottom) + 14;
            lblPoints.Top = haut + 4; bActualiser.Top = haut - 2;
            listeBilan.Top = bActualiser.Bottom + 8;
        }

        /// <summary>(autotest) l'en-tête ne doit pas bouger quand on le replace page descendue.</summary>
        internal void VerifierEntetePourAutotest()
        {
            AutoScrollPosition = new Point(0, 0); PlacerEntete(); int avant = bUnClic.Top, liste = listeBilan.Top;
            AutoScrollPosition = new Point(0, 200); PlacerEntete();
            AutoScrollPosition = new Point(0, 0);
            if (bUnClic.Top != avant || listeBilan.Top != liste) throw new InvalidOperationException("en-tête de l'accueil décalé après défilement : " + avant + " -> " + bUnClic.Top);
        }

        /// <summary>Santé des quatre secteurs de l'anneau, à partir du bilan.</summary>
        private void CalculerSecteurs(DonneesBilan d, List<PointBilan> pts)
        {
            var v = BilanPc.Secteurs(d);
            for (int k = 0; k < 4; k++) jauge.Secteurs[k] = v[k];
        }

        /// <summary>Cible d'un point du bilan (décrite par le moteur) → action de l'interface.</summary>
        private Cible VersCible(string c)
        {
            if (string.IsNullOrEmpty(c)) return null;
            int i = c.IndexOf(':');
            string type = i < 0 ? c : c.Substring(0, i), cle = i < 0 ? "" : c.Substring(i + 1);
            switch (type)
            {
                case "action": return Cible.A(cle);
                case "page": return Cible.Page(cle);
                case "uri": return Cible.Lien(cle);
                case "code":
                    if (cle == "redemarrer") return Cible.Faire(() =>
                    {
                        if (BoiteMessage.Question("Redémarrer le PC maintenant ?\n\nEnregistre ton travail et ferme tes applications avant."))
                            Programmes.Executer(Path.Combine(Chemins.Systeme32, "shutdown.exe"), "/r /t 5", 5000);
                    });
                    if (cle == "bouclier") return Cible.Faire(() => { FenetreBouclier.Ouvrir(F); MettreAJourBilan(); });
                    return null;
                default: return null;
            }
        }

        /// <summary>Le profil a changé (pseudo) : l'accueil suit.</summary>
        public void RafraichirProfil() { etatTitre.Text = Profil.Accueil(F.Reglages.Pseudo); }

        private int LargeurBilan { get { return Math.Max(640, ClientSize.Width - 60); } }

        private void AfficherBilan()
        {
            listeBilan.SuspendLayout();
            var anciens = listeBilan.Controls.Cast<Control>().ToList();
            listeBilan.Controls.Clear();
            foreach (var c in anciens)
            {
                foreach (var b in c.Controls.OfType<Bouton>()) F.BoutonsActions.Remove(b);
                c.Dispose();
            }
            int w = LargeurBilan;
            // d'abord ce qu'il y a à faire ; ce qui va bien tient sur une seule ligne (détail sur demande)
            var aFaire = points.Where(p => p.Etat != "ok").OrderBy(p => p.Etat == "probleme" ? 0 : 1).ToList();
            var bons = points.Where(p => p.Etat == "ok").ToList();
            foreach (var p in aFaire) AjouterLigne(p, w);
            if (bons.Count > 0)
            {
                var ligne = new Carte(w, 46) { Margin = new Padding(0, 0, 0, 8) };
                ligne.Controls.Add(new Etiquette("✔", Theme.CarteTitre, Role.Vert, 14, 11));
                var t = new Etiquette(Langue.F(aFaire.Count == 0 ? "Tout va bien : {0} points vérifiés." : "{0} autres points vérifiés : tout va bien.", bons.Count), Theme.Normal, Role.Texte, 44, 13) { AutoSize = false, Size = new Size(w - 230, 22), AutoEllipsis = true, Tag = "texte" };
                ligne.Controls.Add(t);
                var b = new Bouton(okVisibles ? "Masquer la liste" : "Voir la liste", false, 160, 30) { Location = new Point(w - 172, 8) };
                b.Click += (s, e) => { okVisibles = !okVisibles; BeginInvoke(new Action(AfficherBilan)); };
                ligne.Controls.Add(b);
                listeBilan.Controls.Add(ligne);
                if (okVisibles) foreach (var p in bons) AjouterLigne(p, w);
            }
            listeBilan.ResumeLayout();
            F.AppliquerDisponibilites();
        }

        private bool okVisibles;

        private void AjouterLigne(PointBilan p, int w)
        {
            {
                var ligne = new Carte(w, 46) { Margin = new Padding(0, 0, 0, 8) };
                Role r = p.Etat == "probleme" ? Role.Rouge : p.Etat == "attention" ? Role.Orange : Role.Vert;
                string sym = p.Etat == "probleme" ? "✖" : p.Etat == "attention" ? "!" : "✔";
                ligne.Controls.Add(new Etiquette(sym, Theme.CarteTitre, r, 14, 11));
                var t = new Etiquette(p.Texte, Theme.Normal, Role.Texte, 44, 13) { AutoSize = false, Size = new Size(w - 230, 22), AutoEllipsis = true, Tag = "texte" };
                ligne.Controls.Add(t);
                Infobulle.Definir(t, p.Texte);
                if (p.Bouton != null)
                {
                    var b = new Bouton(p.Bouton, p.Etat == "probleme", 160, 30) { Location = new Point(w - 172, 8), Cible = p.Cible };
                    b.Click += (s, e) => F.Executer(((Bouton)s).Cible as Cible);
                    ligne.Controls.Add(b);
                    if (p.Cible != null && p.Cible.Type == "action") F.EnregistrerBoutonAction(b);
                }
                listeBilan.Controls.Add(ligne);
            }
        }

        private void AjusterBilan()
        {
            int w = LargeurBilan;
            listeBilan.SuspendLayout();
            foreach (Control ligne in listeBilan.Controls)
            {
                if (ligne.Width == w) continue;
                ligne.Width = w;
                foreach (Control c in ligne.Controls)
                {
                    if (c is Button) c.Left = w - 172;
                    else if ((c.Tag as string) == "texte") c.Width = w - 230;
                }
            }
            listeBilan.ResumeLayout();
        }
    }
}
