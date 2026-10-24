// Nettoyage PC 5 - carte de l'espace disque : chaque dossier est un rectangle proportionnel à sa taille.
// Clic : entrer dans le dossier ; clic droit ou « Remonter » : revenir. Lecture seule : rien n'est modifié.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class Pave5 { public string Libelle = "", Chemin = ""; public double Taille; public bool Dossier; public Rect R; }

    internal sealed class CarteEspace5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly ComboBox cb = new ComboBox { MinWidth = 360 };
        private readonly BoutonNoyau bAna;
        private readonly TextBlock tChemin, tInfo;
        private readonly ZoneCarte5 zone = new ZoneCarte5 { Height = 480 };
        private readonly List<DriveInfo> lecteurs;
        private readonly ConcurrentDictionary<string, long> tailles = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly DispatcherTimer minuteur = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        private string racine = "", courant = "", perso = "";
        private double totalCourant;
        private volatile bool arret;
        private long fichiers;

        public static CarteEspace5 Ouvrir(FenetrePrincipale f) { var c = new CarteEspace5(f); var w = f.Ouvrir("Carte de l'espace disque", c, 1160); w.Fermee += () => c.arret = true; return c; }

        /// <summary>(captures) analyse un dossier tout de suite, sans fil d'arrière-plan.</summary>
        public void AnalyserPourEssai(string dossier)
        {
            tailles.Clear(); racine = dossier; arret = false;
            MesurerRec(dossier, 0);
            zone.Calcul = false;
            Dispatcher.UIThread.Post(() => Afficher(dossier));
        }

        private CarteEspace5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            lecteurs = DriveInfo.GetDrives().Where(d => { try { return d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable); } catch { return false; } }).ToList();
            foreach (var d in lecteurs) cb.Items.Add(d.Name.TrimEnd('\\') + "  " + d.VolumeLabel + " — " + Langue.F("{0} utilisés sur {1}", Format.Taille(d.TotalSize - d.AvailableFreeSpace), Format.Taille(d.TotalSize)));
            cb.Items.Add(Langue.T("Mon dossier utilisateur")); cb.Items.Add(Langue.T("Choisir un dossier…"));
            cb.SelectedIndex = 0;
            cb.SelectionChanged += async (s, e) =>
            {
                if (cb.SelectedIndex != cb.Items.Count - 1) return;
                try
                {
                    var top = TopLevel.GetTopLevel(this);
                    var choix = top == null ? null : await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
                    var d = choix != null ? choix.FirstOrDefault() : null;
                    string c = d != null ? d.TryGetLocalPath() : null;
                    if (!string.IsNullOrEmpty(c)) { perso = c; cb.Items[cb.Items.Count - 1] = Langue.T("Dossier :") + " " + c; cb.SelectedIndex = cb.Items.Count - 1; }
                    else if (perso == "") cb.SelectedIndex = 0;
                }
                catch { cb.SelectedIndex = 0; }
            };
            var ligne = new WrapPanel { Orientation = Orientation.Horizontal };
            cb.Margin = new Thickness(0, 0, 10, 6);
            ligne.Children.Add(cb);
            bAna = new BoutonNoyau("Analyser", true, 110, 32) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 10, 6) };
            bAna.Clic += Analyser;
            var bHaut = new BoutonNoyau("↑  Remonter", false, 120, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bHaut.Clic += Remonter;
            var bExp = new BoutonNoyau("Ouvrir dans l'Explorateur", false, 210, 32) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bExp.Clic += () =>
            {
                string c = zone.Survol != null && zone.Survol.Chemin != "" ? zone.Survol.Chemin : courant;
                if (c != "" && (Directory.Exists(c) || File.Exists(c))) Programmes.MontrerDansExplorateur(c);
            };
            ligne.Children.Add(bAna); ligne.Children.Add(bHaut); ligne.Children.Add(bExp);
            Children.Add(ligne);
            tChemin = new TextBlock { Text = Langue.T("Choisis un disque puis clique sur « Analyser ». Clique sur un rectangle pour entrer dans le dossier."), FontFamily = Habillage.Affichage, FontSize = 14.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextTrimming = TextTrimming.CharacterEllipsis };
            Children.Add(tChemin);
            zone.Fichiers = () => Interlocked.Read(ref fichiers);
            Children.Add(new Border { Child = zone, CornerRadius = new CornerRadius(3), ClipToBounds = true, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1) });
            tInfo = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextTrimming = TextTrimming.CharacterEllipsis };
            Children.Add(tInfo);
            zone.SurvolChange += p => tInfo.Text = p == null ? "" : p.Libelle + "   —   " + Format.Taille(p.Taille) + "  (" + (totalCourant > 0 ? 100 * p.Taille / totalCourant : 0).ToString("N1", Appli.Fr) + " %)" + (p.Dossier ? "   ·   " + Langue.T("clic pour entrer") : "");
            zone.Clic += p =>
            {
                if (zone.Calcul) return;
                if (p == null) { Remonter(); return; }
                if (p.Dossier && p.Chemin != "") { if (tailles.ContainsKey(p.Chemin) && EnfantsConnus(p.Chemin)) Afficher(p.Chemin); else Mesurer(p.Chemin); }
            };
            zone.SizeChanged += (s, e) => { if (courant != "" && !zone.Calcul) Afficher(courant); };
            minuteur.Tick += (s, e) => { if (zone.Calcul) zone.InvalidateVisual(); else minuteur.Stop(); };
        }

        private void Analyser()
        {
            if (zone.Calcul) { arret = true; return; }
            int i = cb.SelectedIndex;
            string r = i < lecteurs.Count ? lecteurs[i].RootDirectory.FullName : i == lecteurs.Count ? Chemins.Profil : perso;
            if (string.IsNullOrEmpty(r) || !Directory.Exists(r)) return;
            tailles.Clear(); racine = r;
            Mesurer(r);
        }

        private void Remonter()
        {
            if (courant == "" || zone.Calcul) return;
            string sep = Path.DirectorySeparatorChar.ToString();
            string parent = Path.GetDirectoryName(courant.TrimEnd(Path.DirectorySeparatorChar));
            if (string.IsNullOrEmpty(parent) || courant.TrimEnd(Path.DirectorySeparatorChar).Equals(racine.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase) || !courant.StartsWith(racine.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) return;
            if (parent.Length == 2 && parent[1] == ':') parent += sep;
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
            zone.Calcul = true; bAna.Changer("Arrêter");
            tChemin.Text = Langue.F("Analyse de {0}…", dossier);
            arret = false; Interlocked.Exchange(ref fichiers, 0);
            zone.InvalidateVisual(); minuteur.Start();
            Task.Run(() => MesurerRec(dossier, 0)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                zone.Calcul = false; bAna.Changer("Analyser");
                if (arret) { tChemin.Text = Langue.T("Analyse arrêtée."); zone.InvalidateVisual(); return; }
                Afficher(dossier);
            }));
        }

        private void Afficher(string dossier)
        {
            courant = dossier;
            var el = new List<Pave5>();
            try { foreach (var d in Directory.GetDirectories(dossier)) { long t; if (tailles.TryGetValue(d, out t) && t > 0) el.Add(new Pave5 { Libelle = Path.GetFileName(d), Chemin = d, Taille = t, Dossier = true }); } } catch { }
            var fichiersDossier = new List<FileInfo>();
            try { fichiersDossier = new DirectoryInfo(dossier).GetFiles().Where(f => !TailleFichiers.EstCloud(f.Attributes)).ToList(); } catch { }
            double total = el.Sum(e => e.Taille) + fichiersDossier.Sum(f => (double)f.Length);
            int petits = 0; double taillePetits = 0;
            foreach (var f in fichiersDossier)
            {
                if (total > 0 && f.Length >= total * 0.01) el.Add(new Pave5 { Libelle = f.Name, Chemin = f.FullName, Taille = f.Length });
                else { petits++; taillePetits += f.Length; }
            }
            if (petits > 0 && taillePetits > 0) el.Add(new Pave5 { Libelle = Langue.F("{0} autre(s) fichier(s)", petits), Taille = taillePetits });
            var tri = el.OrderByDescending(e => e.Taille).ToList();
            if (tri.Count > 60)
            {
                double autres = tri.Skip(59).Sum(e => e.Taille);
                tri = tri.Take(59).Concat(new[] { new Pave5 { Libelle = Langue.F("{0} autres éléments", tri.Count - 59), Taille = autres } }).ToList();
            }
            totalCourant = total;
            zone.Survol = null;
            zone.Paves = ZoneCarte5.Pavage(tri, 0, 0, zone.Bounds.Width, zone.Bounds.Height);
            tChemin.Text = dossier + "   —   " + Format.Taille(total);
            tInfo.Text = Langue.T("Clic : entrer dans le dossier   ·   clic droit ou « Remonter » : revenir   ·   « Ouvrir dans l'Explorateur » : le dossier survolé");
            zone.InvalidateVisual();
        }
    }

    /// <summary>Zone dessinée de la carte : rectangles en dégradé, nom et taille dans ceux qui sont assez grands.</summary>
    internal sealed class ZoneCarte5 : Control
    {
        public List<Pave5> Paves = new List<Pave5>();
        public Pave5 Survol;
        public bool Calcul;
        public Func<long> Fichiers;
        public event Action<Pave5> SurvolChange;
        /// <summary>Clic gauche sur un rectangle ; clic droit : null (remonter).</summary>
        public event Action<Pave5> Clic;

        private static readonly Color[] Couleurs =
        {
            Color.FromRgb(0, 140, 190), Color.FromRgb(0, 160, 150), Color.FromRgb(122, 92, 210), Color.FromRgb(214, 120, 40),
            Color.FromRgb(46, 150, 90), Color.FromRgb(200, 64, 110), Color.FromRgb(60, 110, 200), Color.FromRgb(160, 128, 40),
        };

        public ZoneCarte5() { Cursor = new Cursor(StandardCursorType.Hand); ClipToBounds = true; }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            var pt = e.GetPosition(this);
            var t = Paves.FirstOrDefault(p => p.R.Contains(pt));
            if (t == Survol) return;
            Survol = t; InvalidateVisual();
            var h = SurvolChange; if (h != null) h(t);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            var h = Clic; if (h == null) return;
            if (e.InitialPressMouseButton == MouseButton.Right) h(null); else h(Survol);
        }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            ctx.DrawRectangle(Habillage.Pinceau(P.Carte), null, new Rect(Bounds.Size));
            if (Calcul)
            {
                var t = new FormattedText(Langue.T("Analyse en cours…") + "  " + Format.Nombre(Fichiers == null ? 0 : Fichiers()) + " " + Langue.T("fichiers comptés") + "\n" + Langue.T("(clique sur « Arrêter » pour interrompre)"),
                    CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Affichage), 16, Habillage.Pinceau(P.Discret)) { MaxTextWidth = Math.Max(10, Bounds.Width - 60) };
                ctx.DrawText(t, new Point(30, 30));
                return;
            }
            int i = 0;
            foreach (var p in Paves)
            {
                var r = new Rect(p.R.X + 1.5, p.R.Y + 1.5, Math.Max(0, p.R.Width - 3), Math.Max(0, p.R.Height - 3));
                Color c = p.Dossier ? Couleurs[i % Couleurs.Length] : Color.FromRgb(110, 118, 132);
                if (p == Survol) c = Habillage.Melanger(c, Colors.White, 0.3);
                if (r.Width > 2 && r.Height > 2)
                    ctx.DrawRectangle(new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(c, 0), new GradientStop(Habillage.Melanger(c, Colors.Black, 0.2), 1) },
                    }, p == Survol ? new Pen(Habillage.Pinceau(Colors.White, 200), 1.5) : null, new RoundedRect(r, 3));
                if (r.Width > 70 && r.Height > 34)
                {
                    var t = new FormattedText(p.Libelle + "\n" + Format.Taille(p.Taille), CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Corps, FontStyle.Normal, FontWeight.SemiBold), 12, Brushes.White)
                    { MaxTextWidth = r.Width - 12, MaxTextHeight = r.Height - 10, Trimming = TextTrimming.CharacterEllipsis };
                    ctx.DrawText(t, new Point(r.X + 6, r.Y + 5));
                }
                i++;
            }
        }

        /// <summary>Pavage « squarifié » : des rectangles aussi carrés que possible.</summary>
        public static List<Pave5> Pavage(List<Pave5> elements, double x, double y, double l, double h)
        {
            double total = elements.Sum(e => e.Taille);
            var sortie = new List<Pave5>();
            if (total <= 0 || l < 2 || h < 2) return sortie;
            double echelle = l * h / total;
            var reste = new List<Tuple<Pave5, double>>(elements.Select(e => Tuple.Create(e, e.Taille * echelle)));
            Func<List<Tuple<Pave5, double>>, double, double> pire = (rangee, cote) =>
            {
                double s = rangee.Sum(r => r.Item2), mx = rangee.Max(r => r.Item2), mn = rangee.Min(r => r.Item2);
                if (s <= 0 || mn <= 0) return double.MaxValue;
                return Math.Max(cote * cote * mx / (s * s), s * s / (cote * cote * mn));
            };
            while (reste.Count > 0)
            {
                double cote = Math.Min(l, h);
                var rangee = new List<Tuple<Pave5, double>>();
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
                    double lc = somme / h, yy = y;
                    foreach (var r in rangee) { double hh = r.Item2 / lc; r.Item1.R = new Rect(x, yy, lc, hh); sortie.Add(r.Item1); yy += hh; }
                    x += lc; l -= lc;
                }
                else
                {
                    double hc = somme / l, xx = x;
                    foreach (var r in rangee) { double ww = r.Item2 / hc; r.Item1.R = new Rect(xx, y, ww, hc); sortie.Add(r.Item1); xx += ww; }
                    y += hc; h -= hc;
                }
            }
            return sortie;
        }
    }
}
