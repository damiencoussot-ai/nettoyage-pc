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

    internal class PointBilan { public string Etat, Texte, Bouton, Parole = ""; public Cible Cible; }

    internal class PageAccueil : Page
    {
        private readonly Jauge jauge = new Jauge { Location = new Point(16, 0) };
        private readonly Etiquette etatTitre, etatCourt, etatTexte, lblPoints;
        private readonly Bouton bUnClic, bAnalyse, bActualiser, bBriefing;
        private readonly Timer minuteurVoix = new Timer { Interval = 500 };
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
            bBriefing = NouveauBouton("▶  Écouter le briefing", false, 210, 30, 338, 106, () => BasculerBriefing());
            bBriefing.Font = Theme.Petit;
            Infobulle.Definir(bBriefing, "Nettoyage PC te dit à voix haute l'état de ton PC et les améliorations proposées (voix de Windows, sans Internet).");
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
            minuteurVoix.Tick += (s, e) => { if (!Voix.EnCours) { minuteurVoix.Stop(); bBriefing.Text = "▶  Écouter le briefing"; } };
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
        public static double LireTemperature(out string source)
        {
            source = "";
            double t = Natif.TemperatureCarteGraphique();
            if (t > 0) { source = "gpu"; return t; }
            try
            {
                var z = Wmi.Requete("SELECT HighPrecisionTemperature, Temperature FROM Win32_PerfFormattedData_Counters_ThermalZoneInformation", @"root\cimv2", 5)
                    .Select(o => { long h = Wmi.Long(o, "HighPrecisionTemperature"); return h > 0 ? h / 10.0 - 273.15 : Wmi.Long(o, "Temperature") - 273.15; })
                    .Where(x => x > 15 && x < 120).ToList();
                if (z.Count > 0) { source = "systeme"; return z.Max(); }
            }
            catch { }
            return -1;
        }

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
        private class DonneesBilan
        {
            public double DisqueLibre, DisqueTotal, RamTotal, RamLibre;
            public long Corbeille, Temp;
            public DateTime? DernierJournal, Demarre, PointDate, DatePiloteGpu;
            public bool Tache, MpPresent, MpTempsReel, PointConnu, RedemarrageAttente;
            public int MpAge, NbDemarrage, RamPrevue, RamReelle;
            public List<string> Malades = new List<string>(), AutresAv = new List<string>(), PareFeuOff = new List<string>();
            public string NomGpu = "";
            public List<PointPotentiel> Potentiel = new List<PointPotentiel>();
            public List<AccesDistant> AccesDistants = new List<AccesDistant>();
        }

        public void MettreAJourBilan()
        {
            if (bilanEnCours) return;
            bilanEnCours = true; bActualiser.Enabled = false;
            if (jauge.Score < 0) minuteurAnim.Start();
            EnFond(CollecterBilan, d => { bilanEnCours = false; bActualiser.Enabled = true; if (d != null) ConstruireBilan(d); });
        }

        private static DonneesBilan CollecterBilan()
        {
            var d = new DonneesBilan();
            try { var dq = new DriveInfo(Environment.GetEnvironmentVariable("SystemDrive") ?? Path.GetPathRoot(Chemins.Windows)); if (dq.IsReady) { d.DisqueLibre = dq.AvailableFreeSpace; d.DisqueTotal = dq.TotalSize; } } catch { }
            long n; d.Corbeille = Natif.TailleCorbeille(out n);
            d.Temp = TailleFichiers.Dossier(Path.GetTempPath());
            try { var j = new DirectoryInfo(Chemins.Donnees).GetFiles("journal_*.txt").OrderByDescending(x => x.LastWriteTime).FirstOrDefault(); if (j != null) d.DernierJournal = j.LastWriteTime; } catch { }
            d.Tache = Moteur.TacheExiste(Moteur.TacheHebdo);
            ulong t, l; Natif.Memoire(out t, out l); d.RamTotal = t; d.RamLibre = l;
            var os = Wmi.Requete("SELECT LastBootUpTime FROM Win32_OperatingSystem").FirstOrDefault();
            if (os != null) d.Demarre = Wmi.Date(os, "LastBootUpTime");
            d.NbDemarrage = ElementsDemarrage.Lister().Count(x => x.Actif);
            d.Malades = DisquePhysique.Lister(false).Where(x => !x.EnBonneSante).Select(x => x.Nom).ToList();
            var mp = Wmi.Requete("SELECT RealTimeProtectionEnabled, AntivirusSignatureAge FROM MSFT_MpComputerStatus", @"root\Microsoft\Windows\Defender").FirstOrDefault();
            if (mp != null) { d.MpPresent = true; d.MpTempsReel = Wmi.Bool(mp, "RealTimeProtectionEnabled"); d.MpAge = (int)Wmi.Long(mp, "AntivirusSignatureAge"); }
            d.AutresAv = InfosPc.Antivirus().Where(x => x.IndexOf("Defender", StringComparison.OrdinalIgnoreCase) < 0).ToList();
            d.PareFeuOff = Moteur.PareFeuDesactive();
            if (Appli.EstAdmin) { var p = Moteur.DernierPoint(); if (p != null) d.PointDate = p.Item3; d.PointConnu = true; }
            else
            {
                // sans droits administrateur : le journal de Windows garde la trace des points créés
                try
                {
                    var date = Evenements.DatePremier("Application", "*[System[EventID=8194]]", 30, "SystemRestore");
                    if (date.HasValue) { d.PointDate = date; d.PointConnu = true; }
                }
                catch { }
            }
            var b = Barrette.Lister(); d.RamPrevue = Barrette.VitessePrevue(b); d.RamReelle = Barrette.VitesseReelle(b);
            var gpu = InfosPc.CarteGraphiquePrincipale();
            if (gpu != null && gpu.DatePilote.HasValue) { d.NomGpu = gpu.Nom; d.DatePiloteGpu = gpu.DatePilote; }
            d.RedemarrageAttente = InfosPc.RedemarrageEnAttente();
            try { d.Potentiel = Potentiel.Analyser(); } catch { }
            try { d.AccesDistants = Bouclier.ASignaler(); } catch { }
            return d;
        }

        private void ConstruireBilan(DonneesBilan d)
        {
            var pts = new List<PointBilan>();
            Action<string, string, string, Cible> Point = (etat, texte, bouton, cible) => pts.Add(new PointBilan { Etat = etat, Texte = texte, Bouton = bouton, Cible = cible });
            Action<string> Dire = parole => pts[pts.Count - 1].Parole = Langue.T(parole);   // phrase courte lue par le briefing vocal
            string sd = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            if (d.DisqueTotal > 0)
            {
                double pct = 100 * d.DisqueLibre / d.DisqueTotal;
                string txt = "Disque " + sd + " " + Format.Taille(d.DisqueLibre) + " libres (" + pct.ToString("0", Appli.Fr) + " %)";
                if (pct < 10) { Point("probleme", txt + " : presque plein, Windows ralentit.", "Libérer de l'espace", Cible.A("complet")); Dire("libérer de la place sur le disque, il est presque plein"); }
                else if (pct < 20) { Point("attention", txt + " : il commence à se remplir.", "Nettoyer", Cible.A("standard")); Dire("faire un peu de place sur le disque"); }
                else Point("ok", txt, null, null);
            }
            long recup = d.Corbeille + d.Temp;
            if (recup >= 1073741824L) { Point("attention", Format.Taille(recup) + " récupérables tout de suite (corbeille et fichiers temporaires).", "Nettoyer", Cible.A("standard")); Dire("vider la corbeille et les fichiers temporaires"); }
            else Point("ok", "Corbeille et fichiers temporaires : " + Format.Taille(recup), null, null);
            if (!d.DernierJournal.HasValue) { Point("attention", "Aucun nettoyage effectué avec Nettoyage PC pour l'instant.", "Nettoyer", Cible.A("standard")); Dire("faire un premier nettoyage"); }
            else
            {
                int jours = (int)(DateTime.Now - d.DernierJournal.Value).TotalDays;
                if (jours > 14) { Point("attention", "Dernière action il y a " + jours + " jours.", "Nettoyer", Cible.A("standard")); Dire("refaire un nettoyage"); }
                else Point("ok", "Dernière action : " + d.DernierJournal.Value.ToString("dd/MM/yyyy HH:mm"), null, null);
            }
            // matériel bridé (écran à 60 Hz, écran sur la carte mère, Resizable BAR...) : la mémoire bridée a déjà son propre point
            foreach (var p in d.Potentiel.Where(x => !x.Ok && x.Cle != "expo")) { Point("attention", p.Titre + (p.Gain != "" ? " (" + p.Gain + ")" : ""), "Corriger", Cible.Page("Potentiel")); Dire("corriger un réglage qui bride ton matériel"); }
            if (d.Tache) Point("ok", "Entretien automatique activé (chaque dimanche).", null, null);
            else { Point("attention", "Entretien automatique désactivé : ton PC ne se nettoie pas tout seul.", "Activer", Cible.A("planifier")); Dire("activer l'entretien automatique"); }
            if (d.RamTotal > 0)
            {
                double pr = 100 * (d.RamTotal - d.RamLibre) / d.RamTotal;
                if (pr >= 85) { Point("attention", "Mémoire vive utilisée à " + pr.ToString("0") + " %.", "Libérer la RAM", Cible.A("ramlib")); Dire("libérer la mémoire vive"); }
                else Point("ok", "Mémoire vive utilisée à " + pr.ToString("0") + " %.", null, null);
            }
            if (d.Demarre.HasValue && (DateTime.Now - d.Demarre.Value).TotalDays >= 7)
            {
                Point("attention", "PC non redémarré depuis " + (DateTime.Now - d.Demarre.Value).TotalDays.ToString("0") + " jours : un redémarrage le rendra plus rapide.", "Redémarrer", Cible.Faire(() =>
                {
                    if (BoiteMessage.Question("Redémarrer le PC maintenant ?\n\nEnregistre ton travail et ferme tes applications avant."))
                        Programmes.Executer(Path.Combine(Chemins.Systeme32, "shutdown.exe"), "/r /t 5", 5000);
                }));
                Dire("redémarrer le PC");
            }
            if (d.NbDemarrage >= 10) { Point("attention", d.NbDemarrage + " programmes se lancent au démarrage : Windows démarre plus lentement.", "Gérer", Cible.Page("Demarrage")); Dire("alléger le démarrage de Windows"); }
            else Point("ok", d.NbDemarrage + " programme(s) au démarrage.", "Gérer", Cible.Page("Demarrage"));
            if (d.Malades.Count > 0) { Point("probleme", "Disque en mauvaise santé : " + string.Join(", ", d.Malades) + ". Sauvegarde tes données.", "Voir les disques", Cible.Page("Materiel")); Dire("sauvegarder tes documents, car un disque montre des signes de faiblesse"); }
            else Point("ok", "Disques en bonne santé.", "Détails", Cible.Page("Materiel"));
            if (d.MpPresent && !d.MpTempsReel && d.AutresAv.Count == 0) { Point("probleme", "Protection antivirus en temps réel désactivée.", "Ouvrir Sécurité Windows", Cible.Lien("windowsdefender://threat")); Dire("réactiver la protection antivirus"); }
            else if (d.MpPresent && d.MpTempsReel && d.MpAge >= 7) { Point("attention", "Signatures antivirus vieilles de " + d.MpAge + " jours.", "Mettre à jour", Cible.A("antivirus")); Dire("mettre à jour l'antivirus"); }
            else if (d.AutresAv.Count > 0) Point("ok", "Antivirus actif : " + string.Join(", ", d.AutresAv), null, null);
            else Point("ok", "Antivirus Microsoft Defender actif et à jour.", null, null);
            foreach (var a in d.AccesDistants)
            {
                Point(a.Connexions > 0 ? "probleme" : "attention", Langue.F(a.Connexions > 0 ? "{0} est ouvert et connecté : quelqu'un peut voir ton écran." : "{0} est ouvert : il permet de prendre la main sur ton PC.", a.Nom), "Vérifier", Cible.Faire(() => { FenetreBouclier.Ouvrir(F); MettreAJourBilan(); }));
                pts[pts.Count - 1].Parole = Langue.F("vérifier {0}, un logiciel qui permet de prendre la main sur ton PC", a.Nom);
            }
            if (d.PareFeuOff.Count > 0) { Point("probleme", "Pare-feu désactivé (" + string.Join(", ", d.PareFeuOff) + ").", "Ouvrir le pare-feu", Cible.Lien("windowsdefender://network")); Dire("réactiver le pare-feu"); }
            else Point("ok", "Pare-feu Windows actif.", null, null);
            if (d.PointConnu || d.PointDate.HasValue)
            {
                if (!d.PointDate.HasValue) { Point("attention", "Aucun point de restauration : impossible de revenir en arrière en cas de problème.", "Créer", Cible.A("point")); Dire("créer un point de restauration"); }
                else if ((DateTime.Now - d.PointDate.Value).TotalDays > 30) { Point("attention", "Dernier point de restauration le " + d.PointDate.Value.ToString("dd/MM/yyyy") + ".", "Créer", Cible.A("point")); Dire("créer un nouveau point de restauration"); }
                else Point("ok", "Point de restauration du " + d.PointDate.Value.ToString("dd/MM/yyyy") + ".", null, null);
            }
            if (d.RedemarrageAttente) { Point("attention", "Des mises à jour Windows attendent un redémarrage.", null, null); Dire("redémarrer pour terminer les mises à jour de Windows"); }
            if (d.DatePiloteGpu.HasValue)
            {
                int mois = (int)Math.Floor((DateTime.Now - d.DatePiloteGpu.Value).TotalDays / 30.4);
                if (mois >= 4) { Point("attention", "Pilote graphique (" + d.NomGpu + ") vieux de " + mois + " mois : une version plus récente existe sûrement.", "Mettre à jour", Cible.Page("Pilotes")); Dire("mettre à jour le pilote de ta carte graphique"); }
                else Point("ok", "Pilote graphique récent (" + d.NomGpu + ").", "Voir", Cible.Page("Pilotes"));
            }
            if (d.RamPrevue > 0 && d.RamReelle > 0 && d.RamReelle < d.RamPrevue - 100)
                { Point("attention", "Mémoire vive à " + d.RamReelle + " MT/s au lieu de " + d.RamPrevue + " MT/s : active le profil EXPO / XMP dans le BIOS pour plus de performances.", "Comment faire", Cible.Page("Materiel")); Dire("activer le profil EXPO ou XMP de ta mémoire dans le BIOS"); }

            int score = 100;
            foreach (var p in pts) score -= p.Etat == "probleme" ? 20 : p.Etat == "attention" ? 7 : 0;
            jauge.Score = Math.Max(0, Math.Min(100, score));
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
                : nbP + " problème(s), " + nbA + " point(s) à améliorer. Clique sur « Optimiser en 1 clic » ou corrige les points un par un ci-dessous.";
            PlacerEntete();
            FichierSur.AjouterLigne(Chemins.Fichier("scores.csv"), DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ";" + (int)jauge.Score);
            AfficherBilan();
            minuteurAnim.Start();
            try { var bp = BilanPret; if (bp != null) bp(); } catch (Exception ex) { JournalDemarrage.Erreur("bilan", ex); }
            // briefing vocal (une fois par session au plus, selon le réglage du profil)
            if (!briefingFait && Programme.DossierAutotest == null && F.WindowState != FormWindowState.Minimized && Profil.BriefingAttendu(F.Reglages, nbP + nbA > 0))
            {
                briefingFait = true;
                F.Reglages.DernierBriefing = DateTime.Now.ToString("yyyy-MM-dd"); F.Reglages.Ecrire();
                LireBriefing(false);
            }
            var h = F.Pages["Historique"] as PageHistorique;
            if (F.PageActive == "Historique" && h != null) h.Charger();
        }

        private bool briefingFait;

        /// <summary>Le texte d'état peut tenir sur plusieurs lignes : boutons, mesures et liste se placent dessous, sans chevauchement.</summary>
        private void PlacerEntete()
        {
            etatTexte.MaximumSize = new Size(Math.Max(320, Math.Min(620, ClientSize.Width - 338 - 30)), 0);
            // les positions sont relatives au défilement : la page peut être descendue quand le bilan se rafraîchit
            int y = Math.Max(106 + AutoScrollPosition.Y, etatTexte.Bottom + 8);
            bBriefing.Top = y;
            bUnClic.Top = bAnalyse.Top = bBriefing.Bottom + 10;
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
            Func<double, double> borne = v => Math.Max(0, Math.Min(100, v));
            jauge.Secteurs[0] = d.DisqueTotal > 0 ? borne(100.0 * d.DisqueLibre / d.DisqueTotal / 25 * 100) : -1;
            double pr = d.RamTotal > 0 ? 100 * (d.RamTotal - d.RamLibre) / d.RamTotal : -1;
            jauge.Secteurs[1] = pr < 0 ? -1 : borne(pr <= 70 ? 100 : 100 - (pr - 70) * 2.4);
            double secu = 100;
            if (d.MpPresent && !d.MpTempsReel && d.AutresAv.Count == 0) secu -= 60;
            else if (d.MpPresent && d.MpTempsReel && d.MpAge >= 7) secu -= 15;
            if (d.PareFeuOff.Count > 0) secu -= 40;
            if (d.AccesDistants.Count > 0) secu -= 30;
            if (d.PointConnu && !d.PointDate.HasValue) secu -= 10;
            else if (d.PointDate.HasValue && (DateTime.Now - d.PointDate.Value).TotalDays > 30) secu -= 5;
            jauge.Secteurs[2] = borne(secu);
            double mat = 100;
            if (d.Malades.Count > 0) mat -= 60;
            if (d.DatePiloteGpu.HasValue && (DateTime.Now - d.DatePiloteGpu.Value).TotalDays >= 120) mat -= 15;
            if (d.RamPrevue > 0 && d.RamReelle > 0 && d.RamReelle < d.RamPrevue - 100) mat -= 15;
            mat -= 10 * d.Potentiel.Count(x => !x.Ok && x.Cle != "expo");
            if (d.RedemarrageAttente) mat -= 5;
            jauge.Secteurs[3] = borne(mat);
        }

        /// <summary>Le profil a changé (pseudo) : l'accueil suit.</summary>
        public void RafraichirProfil() { etatTitre.Text = Profil.Accueil(F.Reglages.Pseudo); }

        private void BasculerBriefing()
        {
            if (Voix.EnCours) { Voix.Arreter(); minuteurVoix.Stop(); bBriefing.Text = "▶  Écouter le briefing"; return; }
            LireBriefing(true);
        }

        /// <summary>Lit à voix haute l'état du PC et les améliorations proposées (rien n'est lancé).</summary>
        public void LireBriefing(bool demande)
        {
            if (points.Count == 0 && bilanEnCours) return;
            string texte = Profil.TexteBriefing(F.Reglages.Pseudo, ScoreActuel, points);
            if (!Voix.Disponible || !Voix.Lire(texte, F.Reglages.Voix))
            {
                if (demande) BoiteMessage.Info(Langue.T("La voix de Windows n'est pas disponible sur ce PC. Voici le briefing :") + "\n\n" + texte);
                return;
            }
            bBriefing.Text = "■  Arrêter le briefing";
            minuteurVoix.Start();
        }

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
            foreach (var p in points)
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
            listeBilan.ResumeLayout();
            F.AppliquerDisponibilites();
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
