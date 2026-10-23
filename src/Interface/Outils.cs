// Nettoyage PC - fenêtres d'outils : confidentialité, extensions des navigateurs, carte de l'espace disque,
// applications préinstallées inutiles, anciens pilotes
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NettoyagePC
{
    // ================================================================ Réglages de Windows (confidentialité)
    internal static class FenetreReglagesWindows
    {
        public static void Confidentialite(FenetrePrincipale f)
        {
            var l = ReglagesWindows.Confidentialite();
            Afficher(f, "Confidentialité", "Réglages de ton compte Windows qui limitent la publicité et le pistage. Rien n'est désactivé côté sécurité : Windows Update, l'antivirus et le pare-feu ne sont pas touchés.", l);
        }

        /// <summary>Fenêtre générique : état actuel, cases à cocher, confirmation, Historique (réversible).</summary>
        public static void Afficher(FenetrePrincipale principale, string titre, string intro, List<ReglageJeu> definitions)
        {
            var cases = new List<Tuple<CaseACocher, ReglageJeu>>();
            DialogResult r;
            using (var f = new FenetreTheme(titre, 740, 210 + 34 * definitions.Count))
            {
                f.Controls.Add(new Etiquette(intro, Theme.Normal, Role.Discret, 20, 16) { MaximumSize = new Size(680, 0) });
                int y = 76;
                foreach (var d in definitions)
                {
                    bool ok = false; try { ok = d.Etat(); } catch { }
                    var cb = new CaseACocher(d.Libelle, 22, y) { Checked = !ok, Enabled = !ok };
                    Infobulle.Definir(cb, d.Gain);
                    var et = new Etiquette(ok ? "✔ déjà fait" : "à régler", Theme.PetitGras, ok ? Role.Vert : Role.Orange, 480, y + 3);
                    f.Controls.Add(cb); f.Controls.Add(et);
                    cases.Add(Tuple.Create(cb, d));
                    y += 34;
                }
                f.Controls.Add(new Etiquette("Passe la souris sur une ligne pour le détail. Chaque réglage est enregistré dans l'Historique et peut être annulé.", Theme.Petit, Role.Discret, 22, y + 6));
                var bOk = new Bouton("Appliquer les réglages cochés", true, 250, 34) { Location = new Point(350, y + 38), DialogResult = DialogResult.OK, Enabled = cases.Any(c => c.Item1.Enabled) };
                var bAn = new Bouton("Fermer", false, 100, 34) { Location = new Point(610, y + 38), DialogResult = DialogResult.Cancel };
                f.Controls.Add(bOk); f.Controls.Add(bAn); f.CancelButton = bAn;
                f.ClientSize = new Size(f.ClientSize.Width, y + 92);
                Theme.Appliquer(f);
                r = f.ShowDialog(principale);
            }
            var choisis = cases.Where(c => c.Item1.Enabled && c.Item1.Checked).Select(c => c.Item2).ToList();
            if (r != DialogResult.OK || choisis.Count == 0) return;
            string liste = string.Join("\n", choisis.Select(d => "- " + d.Libelle));
            if (principale.ChkSimu.Checked) { BoiteMessage.Info("Mode simulation : ces réglages seraient appliqués :\n\n" + liste); return; }
            int faits = 0; var erreurs = new List<string>();
            foreach (var d in choisis) { try { d.Appliquer(); faits++; } catch (Exception ex) { erreurs.Add(d.Libelle + " : " + ex.Message); } }
            string msg = faits + " réglage(s) appliqué(s). Certains prennent effet à la prochaine ouverture de session.";
            if (erreurs.Count > 0) msg += "\n\nNon appliqué :\n" + string.Join("\n", erreurs);
            BoiteMessage.Info(msg);
        }
    }

    // ================================================================ Extensions des navigateurs (lecture seule)
    internal class FenetreExtensions : FenetreTheme
    {
        private readonly ListeSombre lv = new ListeSombre { MultiSelect = false, ShowItemToolTips = true, Location = new Point(20, 66), Size = new Size(944, 410), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Etiquette info;

        public FenetreExtensions(FenetrePrincipale f) : base("Extensions des navigateurs", 1000, 610, true)
        {
            Controls.Add(new Etiquette("Les extensions installées dans tes navigateurs. Une extension inconnue, installée hors de la boutique officielle ou qui peut lire tous les sites mérite d'être vérifiée. Nettoyage PC ne touche à rien : la suppression se fait dans le navigateur.", Theme.Normal, Role.Discret, 20, 14) { MaximumSize = new Size(940, 0) });
            lv.Colonne("Extension", 270); lv.Colonne("Navigateur", 140); lv.Colonne("Version", 90); lv.Colonne("Avis", 430);
            Controls.Add(lv);
            info = new Etiquette("Recherche des extensions...", Theme.Normal, Role.AccentVif, 20, 486) { Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            var bGerer = new Bouton("Gérer dans le navigateur", true, 220, 34) { Location = new Point(20, 514), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            var bPage = new Bouton("Page de l'extension", false, 170, 34) { Location = new Point(250, 514), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(864, 514), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            bGerer.Click += (s, e) => Gerer();
            bPage.Click += (s, e) => { if (lv.SelectedItems.Count > 0) Programmes.Ouvrir(((Extension)lv.SelectedItems[0].Tag).Page); };
            lv.DoubleClick += (s, e) => Gerer();
            Controls.AddRange(new Control[] { info, bGerer, bPage, bFermer });
            CancelButton = bFermer;
            Theme.Appliquer(this);
            Shown += (s, e) => Charger();
        }

        private void Gerer()
        {
            if (lv.SelectedItems.Count == 0) return;
            var x = (Extension)lv.SelectedItems[0].Tag;
            var pages = new Dictionary<string, string[]> { { "Chrome", new[] { "chrome.exe", "chrome://extensions" } }, { "Edge", new[] { "msedge.exe", "edge://extensions" } }, { "Brave", new[] { "brave.exe", "brave://extensions" } }, { "Vivaldi", new[] { "vivaldi.exe", "vivaldi://extensions" } }, { "Firefox", new[] { "firefox.exe", "about:addons" } } };
            string[] p; if (!pages.TryGetValue(x.Navigateur, out p)) return;
            try { Programmes.Ouvrir(p[0], p[1]); } catch { BoiteMessage.Info("Ouvre " + x.Navigateur + " puis va dans " + p[1]); }
        }

        private void Charger()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                List<Extension> l; try { l = ExtensionsNavigateurs.Lister(); } catch (Exception ex) { JournalDemarrage.Erreur("extensions", ex); l = new List<Extension>(); }
                try { BeginInvoke(new Action(() => Afficher(l))); } catch { }
            });
        }

        private void Afficher(List<Extension> liste)
        {
            if (IsDisposed) return;
            int aVerifier = 0;
            lv.BeginUpdate();
            foreach (var e in liste.Select(x =>
            {
                int niveau; string avis = ExtensionsNavigateurs.Avis(x, out niveau);
                return new { X = x, Niveau = niveau, Avis = avis };
            }).OrderBy(e => e.Niveau).ThenBy(e => e.X.Nom))
            {
                var it = lv.Ajouter(e.X, e.X.Nom, e.X.Navigateur + " (" + e.X.Profil + ")", e.X.Version, e.Avis);
                if (e.Niveau == 0) { it.ForeColor = Theme.P.Orange; aVerifier++; }
                it.ToolTipText = "Identifiant : " + e.X.Id + (e.X.Date.HasValue ? " · installée le " + e.X.Date.Value.ToString("dd/MM/yyyy") : "");
            }
            lv.EndUpdate();
            if (liste.Count == 0) { info.Text = "Aucune extension trouvée (Chrome, Edge, Brave, Vivaldi, Firefox)."; info.RoleTexte = Role.Vert; }
            else if (aVerifier > 0) { info.Text = liste.Count + " extension(s), dont " + aVerifier + " à vérifier (en orange). Si tu ne la reconnais pas, supprime-la depuis le navigateur."; info.RoleTexte = Role.Orange; }
            else { info.Text = liste.Count + " extension(s), aucune n'est suspecte. ✔"; info.RoleTexte = Role.Vert; }
            info.AppliquerTheme();
        }

    }

    // ================================================================ Carte de l'espace disque (rectangles proportionnels)
    internal class Pave { public string Libelle = "", Chemin = ""; public double Taille; public bool Dossier; public RectangleF R; }

    internal class ZoneCarte : Control, IThemable
    {
        public List<Pave> Paves = new List<Pave>();
        public Pave Survol;
        public bool Calcul;
        public Func<long> Fichiers;
        public ZoneCarte() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Cursor = Cursors.Hand; }
        public void AppliquerTheme() { BackColor = Theme.P.Carte; Invalidate(); }

        private static readonly Color[] Couleurs = { Color.FromArgb(0, 120, 212), Color.FromArgb(0, 153, 153), Color.FromArgb(122, 82, 199), Color.FromArgb(214, 112, 32), Color.FromArgb(46, 140, 70), Color.FromArgb(194, 57, 101), Color.FromArgb(60, 110, 180), Color.FromArgb(150, 120, 40) };

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(P.Carte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Calcul)
            {
                Theme.Texte(g, "Analyse en cours...  " + Format.Nombre(Fichiers == null ? 0 : Fichiers()) + " fichiers comptés\n(clique sur « Arrêter » pour interrompre)", Theme.CarteTitre, new Rectangle(30, 30, Width - 60, 80), P.Discret, TextFormatFlags.Left | TextFormatFlags.WordBreak);
                return;
            }
            int i = 0;
            foreach (var p in Paves)
            {
                var r = new RectangleF(p.R.X + 1.5f, p.R.Y + 1.5f, Math.Max(0, p.R.Width - 3), Math.Max(0, p.R.Height - 3));
                Color c = p.Dossier ? Couleurs[i % Couleurs.Length] : Color.FromArgb(120, 126, 138);
                if (p == Survol) c = ControlPaint.Light(c, 0.35f);
                if (r.Width > 2 && r.Height > 2) Theme.RemplirDegrade(g, c, Theme.Melanger(c, Color.Black, 0.18), r.X, r.Y, r.Width, r.Height, 5, true);
                if (r.Width > 70 && r.Height > 34)
                    Theme.Texte(g, p.Libelle + "\n" + Format.Taille(p.Taille), Theme.PetitGras, Rectangle.Round(new RectangleF(r.X + 6, r.Y + 5, r.Width - 12, r.Height - 10)), Color.White, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
                i++;
            }
        }

        /// <summary>Pavage « squarifié » : des rectangles aussi carrés que possible.</summary>
        public static List<Pave> Pavage(List<Pave> elements, float x, float y, float l, float h)
        {
            double total = elements.Sum(e => e.Taille);
            var sortie = new List<Pave>();
            if (total <= 0 || l < 2 || h < 2) return sortie;
            double echelle = l * h / total;
            var reste = new List<Tuple<Pave, double>>(elements.Select(e => Tuple.Create(e, e.Taille * echelle)));
            Func<List<Tuple<Pave, double>>, double, double> pire = (rangee, cote) =>
            {
                double s = rangee.Sum(r => r.Item2), mx = rangee.Max(r => r.Item2), mn = rangee.Min(r => r.Item2);
                if (s <= 0 || mn <= 0) return double.MaxValue;
                return Math.Max(cote * cote * mx / (s * s), s * s / (cote * cote * mn));
            };
            while (reste.Count > 0)
            {
                double cote = Math.Min(l, h);
                var rangee = new List<Tuple<Pave, double>>();
                double ratio = double.MaxValue;
                while (reste.Count > 0)
                {
                    rangee.Add(reste[0]);
                    double nv = pire(rangee, cote);
                    if (rangee.Count > 1 && nv > ratio) { rangee.RemoveAt(rangee.Count - 1); break; }
                    ratio = nv; reste.RemoveAt(0);
                }
                double somme = rangee.Sum(r => r.Item2);
                if (l >= h)
                {
                    float lc = (float)(somme / h), yy = y;
                    foreach (var r in rangee) { float hh = (float)(r.Item2 / lc); r.Item1.R = new RectangleF(x, yy, lc, hh); sortie.Add(r.Item1); yy += hh; }
                    x += lc; l -= lc;
                }
                else
                {
                    float hc = (float)(somme / l), xx = x;
                    foreach (var r in rangee) { float ww = (float)(r.Item2 / hc); r.Item1.R = new RectangleF(xx, y, ww, hc); sortie.Add(r.Item1); xx += ww; }
                    y += hc; h -= hc;
                }
            }
            return sortie;
        }
    }

    internal class FenetreCarteEspace : FenetreTheme
    {
        private readonly ListeDeroulante cb = new ListeDeroulante(320) { Location = new Point(20, 16) };
        private readonly Bouton bAna, bHaut, bExp;
        private readonly Etiquette lblChemin, lblInfo;
        private readonly ZoneCarte zone = new ZoneCarte { Location = new Point(20, 88), Size = new Size(1044, 588), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly List<DriveInfo> lecteurs;
        private readonly ConcurrentDictionary<string, long> tailles = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Forms.Timer minuteur = new System.Windows.Forms.Timer { Interval = 400 };
        private string racine = "", courant = "", perso = "";
        private double totalCourant;
        private volatile bool arret;
        private long fichiers;

        public FenetreCarteEspace(FenetrePrincipale f) : base("Carte de l'espace disque", 1100, 770, true)
        {
            MinimumSize = new Size(760, 500); ShowInTaskbar = true; MinimizeBox = true;
            lecteurs = DriveInfo.GetDrives().Where(d => { try { return d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable); } catch { return false; } }).ToList();
            foreach (var d in lecteurs) cb.Items.Add(d.Name.TrimEnd('\\') + "  " + d.VolumeLabel + " — " + Langue.F("{0} utilisés sur {1}", Format.Taille(d.TotalSize - d.AvailableFreeSpace), Format.Taille(d.TotalSize)));
            cb.Items.Add("Mon dossier utilisateur"); cb.Items.Add("Choisir un dossier...");
            cb.SelectedIndex = 0;
            bAna = new Bouton("Analyser", true, 110, 30) { Location = new Point(350, 15) };
            bHaut = new Bouton("↑  Remonter", false, 120, 30) { Location = new Point(470, 15) };
            bExp = new Bouton("Ouvrir dans l'Explorateur", false, 200, 30) { Location = new Point(600, 15) };
            lblChemin = new Etiquette("Choisis un disque puis clique sur « Analyser ». Clique sur un rectangle pour entrer dans le dossier.", Theme.CarteTitre, Role.Texte, 20, 58) { AutoSize = false, AutoEllipsis = true, Size = new Size(1040, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            lblInfo = new Etiquette("", Theme.Normal, Role.Discret, 20, 684) { AutoSize = false, AutoEllipsis = true, Size = new Size(1040, 22), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            zone.Fichiers = () => Interlocked.Read(ref fichiers);
            Controls.AddRange(new Control[] { cb, bAna, bHaut, bExp, lblChemin, zone, lblInfo });
            Theme.Appliquer(this);

            cb.SelectionChangeCommitted += (s, e) =>
            {
                if (cb.SelectedIndex != cb.Items.Count - 1) return;
                using (var dlg = new FolderBrowserDialog())
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK) { perso = dlg.SelectedPath; cb.Items[cb.Items.Count - 1] = "Dossier : " + perso; }
                    else if (perso == "") cb.SelectedIndex = 0;
                }
            };
            bAna.Click += (s, e) =>
            {
                if (zone.Calcul) { arret = true; return; }
                int i = cb.SelectedIndex;
                string r = i < lecteurs.Count ? lecteurs[i].RootDirectory.FullName : i == lecteurs.Count ? Chemins.Profil : perso;
                if (string.IsNullOrEmpty(r) || !Directory.Exists(r)) return;
                tailles.Clear(); racine = r;
                Mesurer(r);
            };
            bHaut.Click += (s, e) => Remonter();
            bExp.Click += (s, e) =>
            {
                string c = zone.Survol != null && zone.Survol.Chemin != "" ? zone.Survol.Chemin : courant;
                if (c != "" && (Directory.Exists(c) || File.Exists(c))) Programmes.MontrerDansExplorateur(c);
            };
            zone.Resize += (s, e) => { if (courant != "" && !zone.Calcul) Afficher(courant); };
            zone.MouseMove += (s, e) =>
            {
                var t = zone.Paves.FirstOrDefault(p => p.R.Contains(e.X, e.Y));
                if (t == zone.Survol) return;
                zone.Survol = t;
                lblInfo.Text = t == null ? "" : t.Libelle + "   —   " + Format.Taille(t.Taille) + "  (" + (totalCourant > 0 ? 100 * t.Taille / totalCourant : 0).ToString("N1", Appli.Fr) + " %)" + (t.Dossier ? "   ·   clic pour entrer" : "");
                zone.Invalidate();
            };
            zone.MouseClick += (s, e) =>
            {
                if (zone.Calcul) return;
                if (e.Button == MouseButtons.Right) { Remonter(); return; }
                var p = zone.Survol;
                if (p != null && p.Dossier && p.Chemin != "") { if (tailles.ContainsKey(p.Chemin) && EnfantsConnus(p.Chemin)) Afficher(p.Chemin); else Mesurer(p.Chemin); }
            };
            minuteur.Tick += (s, e) => { if (zone.Calcul) zone.Invalidate(); else minuteur.Stop(); };
            FormClosed += (s, e) => { arret = true; minuteur.Stop(); };
        }

        private void Remonter()
        {
            if (courant == "" || zone.Calcul) return;
            string parent = Path.GetDirectoryName(courant.TrimEnd('\\'));
            if (string.IsNullOrEmpty(parent) || courant.TrimEnd('\\').Equals(racine.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) || !courant.StartsWith(racine.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
            if (parent.Length == 2) parent += "\\";
            Afficher(parent);
        }

        private bool EnfantsConnus(string dossier)
        {
            try { foreach (var d in Directory.GetDirectories(dossier)) if (!tailles.ContainsKey(d)) return false; } catch { }
            return true;
        }

        private long MesurerRec(string dossier, int profondeur)
        {
            if (arret) return 0;
            long total = 0;
            try
            {
                var di = new DirectoryInfo(dossier);
                foreach (var f in di.EnumerateFiles())
                {
                    try { if (TailleFichiers.EstCloud(f.Attributes)) continue; total += f.Length; Interlocked.Increment(ref fichiers); } catch { }
                }
                foreach (var d in di.EnumerateDirectories())
                {
                    try { if ((d.Attributes & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                    total += MesurerRec(d.FullName, profondeur + 1);
                }
            }
            catch { }
            if (profondeur <= 3) tailles[dossier] = total;
            return total;
        }

        private void Mesurer(string dossier)
        {
            zone.Calcul = true; bAna.Text = "Arrêter";
            lblChemin.Text = "Analyse de " + dossier + "...";
            arret = false; Interlocked.Exchange(ref fichiers, 0);
            zone.Invalidate(); minuteur.Start();
            System.Threading.Tasks.Task.Run(() =>
            {
                MesurerRec(dossier, 0);
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        zone.Calcul = false; bAna.Text = "Analyser";
                        if (arret) { lblChemin.Text = "Analyse arrêtée."; zone.Invalidate(); return; }
                        Afficher(dossier);
                    }));
                }
                catch { }
            });
        }

        private void Afficher(string dossier)
        {
            courant = dossier;
            var el = new List<Pave>();
            try
            {
                foreach (var d in Directory.GetDirectories(dossier)) { long t; if (tailles.TryGetValue(d, out t) && t > 0) el.Add(new Pave { Libelle = Path.GetFileName(d), Chemin = d, Taille = t, Dossier = true }); }
            }
            catch { }
            var fichiersDossier = new List<FileInfo>();
            try { fichiersDossier = new DirectoryInfo(dossier).GetFiles().Where(f => !TailleFichiers.EstCloud(f.Attributes)).ToList(); } catch { }
            double total = el.Sum(e => e.Taille) + fichiersDossier.Sum(f => (double)f.Length);
            int petits = 0; double taillePetits = 0;
            foreach (var f in fichiersDossier)
            {
                if (total > 0 && f.Length >= total * 0.01) el.Add(new Pave { Libelle = f.Name, Chemin = f.FullName, Taille = f.Length });
                else { petits++; taillePetits += f.Length; }
            }
            if (petits > 0 && taillePetits > 0) el.Add(new Pave { Libelle = petits + " autre(s) fichier(s)", Taille = taillePetits });
            var tri = el.OrderByDescending(e => e.Taille).ToList();
            if (tri.Count > 60)
            {
                double autres = tri.Skip(59).Sum(e => e.Taille);
                tri = tri.Take(59).Concat(new[] { new Pave { Libelle = (tri.Count - 59) + " autres éléments", Taille = autres } }).ToList();
            }
            totalCourant = total;
            zone.Survol = null;
            zone.Paves = ZoneCarte.Pavage(tri, 0, 0, zone.ClientSize.Width, zone.ClientSize.Height);
            lblChemin.Text = dossier + "   —   " + Format.Taille(total);
            lblInfo.Text = "Clic : entrer dans le dossier   ·   clic droit ou « Remonter » : revenir   ·   « Ouvrir dans l'Explorateur » : le dossier survolé";
            zone.Invalidate();
        }
    }

    // ================================================================ Applications préinstallées inutiles
    internal class FenetreAppsInutiles : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly ListeSombre lv = new ListeSombre { CheckBoxes = true, MultiSelect = false, ShowItemToolTips = true, Location = new Point(20, 70), Size = new Size(744, 330), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Etiquette info;
        private readonly Bouton bRetirer;
        private bool travail;

        public FenetreAppsInutiles(FenetrePrincipale f) : base("Applications préinstallées inutiles", 800, 540, true)
        {
            F = f;
            Controls.Add(new Etiquette("Applications livrées avec Windows ou par le fabricant, souvent publicitaires. Celles conseillées sont cochées ; garde celles dont tu te sers. Chacune se réinstalle en un clic depuis le Microsoft Store (et depuis l'Historique).", Theme.Normal, Role.Discret, 20, 14) { MaximumSize = new Size(740, 0) });
            lv.Colonne("Application", 240); lv.Colonne("Pourquoi", 400); lv.Colonne("Conseil", 90);
            Controls.Add(lv);
            info = new Etiquette("Recherche des applications installées...", Theme.Normal, Role.AccentVif, 20, 410) { AutoSize = false, Size = new Size(744, 40), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            bRetirer = new Bouton("Retirer les applications cochées", true, 270, 34) { Location = new Point(20, 456), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Enabled = false };
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(664, 456), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            bRetirer.Click += (s, e) => Retirer();
            Controls.AddRange(new Control[] { info, bRetirer, bFermer });
            CancelButton = bFermer;
            lv.ItemChecked += (s, e) => Compter();
            Theme.Appliquer(this);
            Shown += (s, e) => Charger();
            FormClosing += (s, e) => { if (travail) { e.Cancel = true; BoiteMessage.Info("Attends la fin de la désinstallation en cours."); } };
        }

        private void Charger()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                List<Tuple<Paquet, AppInutile>> l = AppsInutiles.Trouver();
                try { BeginInvoke(new Action(() => Afficher(l))); } catch { }
            });
        }

        private void Afficher(List<Tuple<Paquet, AppInutile>> l)
        {
            if (IsDisposed) return;
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var x in l.OrderByDescending(x => x.Item2.Conseille).ThenBy(x => x.Item2.Libelle))
            {
                string nom = x.Item2.Motif.Contains("*") && x.Item1.NomAffiche != x.Item1.Nom ? x.Item1.NomAffiche : x.Item2.Libelle;
                var it = lv.Ajouter(x, nom, x.Item2.Description, x.Item2.Conseille ? "Retirer" : "À toi de voir");
                it.Checked = x.Item2.Conseille;
                it.ToolTipText = x.Item1.NomComplet;
            }
            lv.EndUpdate();
            if (l.Count == 0) { info.Text = "✔ Aucune application préinstallée inutile sur ce PC."; info.RoleTexte = Role.Vert; info.AppliquerTheme(); }
            Compter();
        }

        private void Compter()
        {
            if (lv.Items.Count == 0) { bRetirer.Enabled = false; return; }
            int n = lv.CheckedItems.Count;
            info.Text = lv.Items.Count + " application(s) trouvée(s), " + n + " cochée(s).";
            info.RoleTexte = Role.Discret; info.AppliquerTheme();
            bRetirer.Enabled = n > 0 && !travail;
        }

        private void Retirer()
        {
            var choix = lv.CheckedItems.Cast<ListViewItem>().Select(i => (Tuple<Paquet, AppInutile>)i.Tag).ToList();
            if (choix.Count == 0) return;
            string liste = string.Join("\n", choix.Select(c => "- " + (c.Item2.Motif.Contains("*") ? c.Item1.NomAffiche : c.Item2.Libelle)));
            if (F.ChkSimu.Checked) { BoiteMessage.Info("Mode simulation : ces applications seraient retirées :\n\n" + liste); return; }
            if (!BoiteMessage.Question("Retirer ces applications de ton compte ?\n\n" + liste + "\n\nTu pourras les réinstaller depuis le Microsoft Store (l'Historique t'y emmène).")) return;
            travail = true; bRetirer.Enabled = false; Cursor = Cursors.WaitCursor;
            System.Threading.Tasks.Task.Run(() =>
            {
                var echecs = new List<string>(); int ok = 0;
                foreach (var c in choix)
                {
                    string nom = c.Item2.Motif.Contains("*") ? c.Item1.NomAffiche : c.Item2.Libelle;
                    try { BeginInvoke(new Action(() => { info.Text = "Désinstallation de « " + nom + " »..."; info.RoleTexte = Role.AccentVif; info.AppliquerTheme(); })); } catch { }
                    string r = AppsInutiles.Retirer(c.Item1, nom);
                    if (r == "") ok++; else echecs.Add(nom + " : " + r);
                }
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        travail = false; Cursor = Cursors.Default;
                        string msg = ok + " application(s) retirée(s).";
                        if (echecs.Count > 0) msg += "\n\nNon retirée(s) :\n- " + string.Join("\n- ", echecs);
                        BoiteMessage.Info(msg);
                        Charger();
                    }));
                }
                catch { }
            });
        }
    }

    // ================================================================ Anciens pilotes
    internal class FenetreAnciensPilotes : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly ListeSombre lv = new ListeSombre { CheckBoxes = true, MultiSelect = false, ShowItemToolTips = true, Location = new Point(20, 70), Size = new Size(904, 360), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Etiquette info;
        private readonly Bouton bRetirer;

        public FenetreAnciensPilotes(FenetrePrincipale f) : base("Anciens pilotes", 960, 560, true)
        {
            F = f;
            Controls.Add(new Etiquette("À chaque mise à jour d'un pilote, Windows garde l'ancienne version « au cas où ». Ces versions inutilisées peuvent être retirées : la plus récente de chaque pilote et celles que Windows utilise sont toujours gardées.", Theme.Normal, Role.Discret, 20, 14) { MaximumSize = new Size(900, 0) });
            lv.Colonne("Fournisseur", 220); lv.Colonne("Type", 130); lv.Colonne("Version", 150); lv.Colonne("Date", 100); lv.Colonne("Taille", 90, HorizontalAlignment.Right); lv.Colonne("État", 190); lv.ColonneElastique = 0;
            lv.ActiverTri();
            Controls.Add(lv);
            info = new Etiquette("Analyse du magasin de pilotes...", Theme.Normal, Role.AccentVif, 20, 440) { AutoSize = false, Size = new Size(904, 22), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            bRetirer = new Bouton("Retirer les versions cochées", true, 260, 34) { Location = new Point(20, 474), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Enabled = false };
            Infobulle.Definir(bRetirer, "Windows te demandera l'autorisation (bouclier) : clique sur « Oui ». Un pilote encore utilisé est toujours gardé (Windows refuse de le retirer).");
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(824, 474), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            bRetirer.Click += (s, e) => Retirer();
            Controls.AddRange(new Control[] { info, bRetirer, bFermer });
            CancelButton = bFermer;
            lv.ItemChecked += (s, e) => { if (e.Item.Tag is PilotesAnciens.Pilote && !((PilotesAnciens.Pilote)e.Item.Tag).Supprimable && e.Item.Checked) e.Item.Checked = false; Compter(); };
            Theme.Appliquer(this);
            Shown += (s, e) => Charger();
        }

        private void Charger()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                List<PilotesAnciens.Pilote> l; try { l = PilotesAnciens.Analyser(); } catch (Exception ex) { JournalDemarrage.Erreur("anciens pilotes", ex); l = new List<PilotesAnciens.Pilote>(); }
                try { BeginInvoke(new Action(() => Afficher(l))); } catch { }
            });
        }

        private void Afficher(List<PilotesAnciens.Pilote> l)
        {
            if (IsDisposed) return;
            lv.BeginUpdate(); lv.Items.Clear();
            // les versions retirables d'abord, puis les versions gardées des mêmes pilotes (pour comparer)
            var familles = new HashSet<string>(l.Where(p => p.Supprimable).Select(p => p.Original), StringComparer.OrdinalIgnoreCase);
            foreach (var p in l.Where(p => p.Supprimable || (familles.Contains(p.Original) && p.PlusRecent)).OrderByDescending(p => p.Supprimable).ThenByDescending(p => p.Taille))
            {
                string etat = p.Supprimable ? "Ancienne version, inutilisée" : p.Utilise ? "Utilisée : gardée" : "Plus récente : gardée";
                var it = lv.Ajouter(p, p.Fournisseur, p.Classe, p.Version, p.DateTexte, Format.Taille(p.Taille), etat);
                it.SubItems[3].Tag = p.Date; it.SubItems[4].Tag = p.Taille;
                it.Checked = p.Supprimable;
                if (!p.Supprimable) it.ForeColor = Theme.P.Discret;
                it.ToolTipText = p.Inf + " · " + p.Original;
            }
            lv.EndUpdate();
            if (!l.Any(p => p.Supprimable)) { info.Text = "✔ Aucune ancienne version de pilote à retirer : le magasin de pilotes est propre."; info.RoleTexte = Role.Vert; info.AppliquerTheme(); bRetirer.Enabled = false; return; }
            Compter();
        }

        private void Compter()
        {
            var c = lv.CheckedItems.Cast<ListViewItem>().Select(i => (PilotesAnciens.Pilote)i.Tag).ToList();
            if (lv.Items.Count == 0) return;
            info.Text = c.Count + " ancienne(s) version(s) cochée(s) : " + Format.Taille(c.Sum(p => p.Taille)) + " à récupérer.";
            info.RoleTexte = Role.Discret; info.AppliquerTheme();
            bRetirer.Enabled = c.Count > 0 && !F.ActionEnCours;
        }

        private void Retirer()
        {
            var c = lv.CheckedItems.Cast<ListViewItem>().Select(i => (PilotesAnciens.Pilote)i.Tag).Where(p => p.Supprimable).ToList();
            if (c.Count == 0) return;
            var args = new List<string>();
            foreach (var p in c) { args.Add("--pilote"); args.Add(p.Inf); }
            string conf = "Retirer " + c.Count + " ancienne(s) version(s) de pilotes (" + Format.Taille(c.Sum(p => p.Taille)) + ") ?\n\nLa version la plus récente de chaque pilote est gardée, et Windows refuse de retirer un pilote encore utilisé. Un point de restauration n'est pas nécessaire : ces versions ne servent plus.";
            ArgsChoisis = args; Confirmation = conf;
            DialogResult = DialogResult.OK;
        }

        public List<string> ArgsChoisis; public string Confirmation;

        public static void Ouvrir(FenetrePrincipale f)
        {
            List<string> args = null; string conf = null;
            using (var w = new FenetreAnciensPilotes(f)) { if (w.ShowDialog(f) == DialogResult.OK) { args = w.ArgsChoisis; conf = w.Confirmation; } }
            if (args != null) f.LancerAction("pilotes", args, conf);
        }
    }
}
