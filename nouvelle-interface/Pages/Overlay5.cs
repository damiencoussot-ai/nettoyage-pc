// Velox 5 - overlay en jeu (profil Gamer) : petit bandeau toujours au premier plan, en haut à gauche de l'écran,
// avec le processeur, la mémoire vive, les températures et l'heure. Il laisse passer les clics (il ne gêne jamais le
// jeu) et s'affiche ou se cache avec Ctrl+Maj+O, même quand Velox est en arrière-plan. Visible dans les jeux en
// mode « fenêtré plein écran » (sans bordure) ; un jeu en plein écran exclusif passe par-dessus.
// Pendant le mode jeu, il note aussi les mesures de la session (rapport de session à la fin).
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal static class Overlay5
    {
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
        [DllImport("user32.dll")] private static extern int GetMessageW(out MSG msg, IntPtr hWnd, uint min, uint max);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
        private const uint MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000, WM_HOTKEY = 0x312;

        private static Window fenetre;
        private static FenetrePrincipale principale;
        private static bool raccourci;
        private static TextBlock tCpu, tGpu, tRam, tHeure;
        private static DispatcherTimer minuteur;
        private static bool mesureEnCours;

        public static bool Ouvert { get { return fenetre != null; } }

        /// <summary>À l'ouverture de Velox : raccourci Ctrl+Maj+O, overlay remis s'il était affiché, suivi de la session de jeu.</summary>
        public static void Initialiser(FenetrePrincipale f)
        {
            principale = f;
            if (f.Source.Immediat || !OperatingSystem.IsWindows()) return;
            if (!raccourci)
            {
                raccourci = true;
                var fil = new Thread(() =>
                {
                    try
                    {
                        if (!RegisterHotKey(IntPtr.Zero, 0x5601, MOD_CONTROL | MOD_SHIFT | MOD_NOREPEAT, 'O')) { JournalDemarrage.Noter("Overlay : raccourci Ctrl+Maj+O déjà pris par un autre logiciel."); return; }
                        MSG m;
                        while (GetMessageW(out m, IntPtr.Zero, 0, 0) > 0)
                            if (m.message == WM_HOTKEY) Dispatcher.UIThread.Post(() => Basculer(principale, true));
                    }
                    catch (Exception ex) { JournalDemarrage.Erreur("raccourci de l'overlay", ex); }
                }) { IsBackground = true, Name = "Raccourci overlay" };
                fil.Start();
            }
            bool voulu = false;
            try { voulu = Parametres.Lire().OverlayJeu; } catch { }
            if (voulu && Habillage.Gamer && fenetre == null) Basculer(f, false);
            SessionJeu5.Demarrer(f);
            Task.Run(() => JournalJeu.Noter("ouverture"));   // série de jours du profil Gamer
        }

        /// <summary>Affiche ou cache l'overlay (et retient le choix).</summary>
        public static void Basculer(FenetrePrincipale f, bool parRaccourci)
        {
            if (f == null) return;
            if (fenetre != null) { fenetre.Close(); f.EnregistrerReglage(r => r.OverlayJeu = false); return; }
            Ouvrir(f);
            f.EnregistrerReglage(r => r.OverlayJeu = true);
            if (!parRaccourci) f.Statut(Langue.T("Overlay en jeu affiché en haut à gauche de l'écran : Ctrl+Maj+O pour l'afficher ou le cacher."));
        }

        private static TextBlock Valeur() { return new TextBlock { FontFamily = Habillage.Console, FontSize = 13, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }; }
        private static TextBlock Nom(string t) { return new TextBlock { Text = t, FontFamily = Habillage.Console, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(255, 120, 230)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }; }

        private static void Ouvrir(FenetrePrincipale f)
        {
            tCpu = Valeur(); tGpu = Valeur(); tRam = Valeur(); tHeure = Valeur();
            tCpu.Text = tGpu.Text = tRam.Text = "…"; tHeure.Text = DateTime.Now.ToString("HH:mm");
            var ligne = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            ligne.Children.Add(new TextBlock { Text = "V//", FontFamily = Habillage.Affichage, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.FromRgb(34, 230, 255)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            ligne.Children.Add(Nom("CPU")); ligne.Children.Add(tCpu);
            ligne.Children.Add(new Border { Width = 14 });
            ligne.Children.Add(Nom("GPU")); ligne.Children.Add(tGpu);
            ligne.Children.Add(new Border { Width = 14 });
            ligne.Children.Add(Nom("RAM")); ligne.Children.Add(tRam);
            ligne.Children.Add(new Border { Width = 14 });
            ligne.Children.Add(tHeure);
            var cadre = new Border { Child = ligne, Padding = new Thickness(12, 6), Background = new SolidColorBrush(Color.FromArgb(185, 8, 3, 20)), BorderBrush = new SolidColorBrush(Color.FromArgb(170, 255, 43, 214)), BorderThickness = new Thickness(1) };
            var w = new Window
            {
                SizeToContent = SizeToContent.WidthAndHeight, SystemDecorations = SystemDecorations.None, Topmost = true, CanResize = false, ShowInTaskbar = false, ShowActivated = false,
                Background = Brushes.Transparent, TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }, Title = "Velox overlay", Content = cadre, Focusable = false,
            };
            w.Opened += (s, e) =>
            {
                try
                {
                    var ecran = w.Screens.Primary;
                    if (ecran != null) w.Position = new PixelPoint(ecran.Bounds.X + (int)(12 * ecran.Scaling), ecran.Bounds.Y + (int)(12 * ecran.Scaling));
                    // les clics passent à travers, la fenêtre ne prend jamais le focus et n'apparaît pas dans Alt+Tab
                    var h = w.TryGetPlatformHandle();
                    if (h != null)
                    {
                        long ex = GetWindowLongPtr(h.Handle, GWL_EXSTYLE).ToInt64();
                        SetWindowLongPtr(h.Handle, GWL_EXSTYLE, new IntPtr(ex | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
                    }
                }
                catch (Exception ex) { JournalDemarrage.Erreur("overlay en jeu", ex); }
            };
            minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            minuteur.Tick += (s, e) => Mesurer(f);
            w.Closed += (s, e) => { fenetre = null; if (minuteur != null) minuteur.Stop(); };
            f.Closed += (s, e) => { if (fenetre != null) fenetre.Close(); };
            fenetre = w;
            w.Show();
            Task.Run(() => JournalJeu.Noter("overlay"));
            minuteur.Start();
            Mesurer(f);
        }

        private static void Mesurer(FenetrePrincipale f)
        {
            if (mesureEnCours || fenetre == null) return;
            mesureEnCours = true;
            Task.Run(() =>
            {
                Mesures m = null; double g = -1;
                try { m = f.Source.Mesurer(true); } catch { }
                try { g = TemperatureGpu.Lire(); } catch { }
                return Tuple.Create(m, g);
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                mesureEnCours = false;
                if (fenetre == null || t.IsFaulted) return;
                var m = t.Result.Item1; double g = t.Result.Item2;
                if (m != null)
                {
                    tCpu.Text = (m.Processeur >= 0 ? Math.Round(m.Processeur) + "%" : "—") + (m.Temperature > 0 ? " " + Math.Round(m.Temperature) + "°" : "");
                    tRam.Text = m.Memoire >= 0 ? Math.Round(m.Memoire) + "%" : "—";
                    tCpu.Foreground = new SolidColorBrush(m.Temperature >= 85 ? Color.FromRgb(255, 122, 138) : Colors.White);
                }
                tGpu.Text = g > 0 ? Math.Round(g) + "°" : "—";
                tGpu.Foreground = new SolidColorBrush(g >= 85 ? Color.FromRgb(255, 122, 138) : Colors.White);
                tHeure.Text = DateTime.Now.ToString("HH:mm");
            }));
        }
    }

    /// <summary>Suivi d'une session de mode jeu (tant que Velox est ouvert) : durée, processeur, températures.
    /// Rien n'est envoyé : les mesures restent dans Documents\NettoyagePC\session-jeu.json.</summary>
    internal static class SessionJeu5
    {
        private static DispatcherTimer minuteur;
        private static bool enCours;
        public static double CpuMoyen, CpuMax, TempMax = -1, GpuMax = -1, RamMax; public static int NbMesures;
        private static string Fichier { get { return Chemins.Fichier("session-jeu.json"); } }

        public static void Demarrer(FenetrePrincipale f)
        {
            if (minuteur != null) return;
            Charger();
            minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            minuteur.Tick += (s, e) => Noter(f);
            minuteur.Start();
        }

        /// <summary>Remet les compteurs à zéro (au lancement du mode jeu).</summary>
        public static void Nouvelle() { CpuMoyen = CpuMax = RamMax = 0; TempMax = GpuMax = -1; NbMesures = 0; Enregistrer(); }

        private static void Noter(FenetrePrincipale f)
        {
            if (enCours || !Gamer5.ModeJeuActif()) return;
            enCours = true;
            Task.Run(() => { Mesures m = null; double g = -1; try { m = f.Source.Mesurer(true); } catch { } try { g = TemperatureGpu.Lire(); } catch { } return Tuple.Create(m, g); })
                .ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    enCours = false;
                    if (t.IsFaulted || t.Result.Item1 == null) return;
                    var m = t.Result.Item1;
                    if (m.Processeur >= 0) { CpuMoyen = (CpuMoyen * NbMesures + m.Processeur) / (NbMesures + 1); CpuMax = Math.Max(CpuMax, m.Processeur); NbMesures++; }
                    if (m.Temperature > 0) TempMax = Math.Max(TempMax, m.Temperature);
                    if (t.Result.Item2 > 0) GpuMax = Math.Max(GpuMax, t.Result.Item2);
                    if (m.Memoire >= 0) RamMax = Math.Max(RamMax, m.Memoire);
                    if (NbMesures % 3 == 0) Enregistrer();
                }));
        }

        private static void Charger()
        {
            try
            {
                var d = Json.LireFichier(Fichier) as System.Collections.Generic.Dictionary<string, object>;
                if (d == null) return;
                double x;
                Func<string, double> n = k => double.TryParse(Json.Texte(d, k), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) ? x : 0;
                CpuMoyen = n("CpuMoyen"); CpuMax = n("CpuMax"); TempMax = n("TempMax"); GpuMax = n("GpuMax"); RamMax = n("RamMax"); NbMesures = (int)n("Mesures");
            }
            catch { }
        }

        private static void Enregistrer()
        {
            try
            {
                var c = System.Globalization.CultureInfo.InvariantCulture;
                var d = new System.Collections.Generic.Dictionary<string, object> { { "CpuMoyen", CpuMoyen.ToString("0.#", c) }, { "CpuMax", CpuMax.ToString("0.#", c) }, { "TempMax", TempMax.ToString("0.#", c) }, { "GpuMax", GpuMax.ToString("0.#", c) }, { "RamMax", RamMax.ToString("0.#", c) }, { "Mesures", NbMesures.ToString(c) } };
                Chemins.CreerDonnees();
                FichierSur.EcrireInterface(Fichier, Json.Ecrire(d));
            }
            catch { }
        }

        /// <summary>Début de la session : la date notée dans modejeu.json par le moteur, sinon null.</summary>
        public static DateTime? Debut()
        {
            try
            {
                var d = Json.LireFichier(Chemins.Fichier("modejeu.json")) as System.Collections.Generic.Dictionary<string, object>;
                DateTime t;
                if (d != null && DateTime.TryParseExact(Json.Texte(d, "Date"), "yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out t)) return t;
            }
            catch { }
            return null;
        }

        /// <summary>Rapport de fin de session : durée, processeur, températures ; à copier pour Discord.</summary>
        public static void Rapport(FenetrePrincipale f, DateTime? debut)
        {
            var P = Habillage.P;
            TimeSpan duree = debut.HasValue ? DateTime.Now - debut.Value : TimeSpan.Zero;
            string dureeTexte = debut.HasValue ? (duree.TotalHours >= 1 ? string.Format("{0} h {1:00}", (int)duree.TotalHours, duree.Minutes) : Math.Max(1, (int)duree.TotalMinutes) + " min") : "—";
            var lignes = new System.Collections.Generic.List<Tuple<string, string>>
            {
                Tuple.Create(Langue.T("DURÉE"), dureeTexte),
                Tuple.Create(Langue.T("PROCESSEUR MOYEN"), NbMesures > 0 ? Math.Round(CpuMoyen) + " %" : "—"),
                Tuple.Create(Langue.T("PROCESSEUR MAX"), NbMesures > 0 ? Math.Round(CpuMax) + " %" : "—"),
                Tuple.Create(Langue.T("TEMP. PROCESSEUR MAX"), TempMax > 0 ? Math.Round(TempMax) + " °C" : "—"),
                Tuple.Create(Langue.T("TEMP. CARTE GRAPHIQUE MAX"), GpuMax > 0 ? Math.Round(GpuMax) + " °C" : "—"),
                Tuple.Create(Langue.T("MÉMOIRE VIVE MAX"), NbMesures > 0 ? Math.Round(RamMax) + " %" : "—"),
            };
            var s = new StackPanel { Spacing = 12 };
            s.Children.Add(new TextBlock { Text = "▸ " + Langue.T("SESSION TERMINÉE · GG"), FontFamily = Habillage.Console, FontSize = 12, LetterSpacing = 2, Foreground = Habillage.Pinceau(P.Accent) });
            var grille = new Avalonia.Controls.Primitives.UniformGrid { Columns = 3 };
            foreach (var l in lignes)
            {
                var c = new StackPanel { Spacing = 2 };
                c.Children.Add(new TextBlock { Text = l.Item1, FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1, Foreground = Habillage.Pinceau(P.Discret) });
                c.Children.Add(new TextBlock { Text = l.Item2, FontFamily = Habillage.Affichage, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Texte) });
                grille.Children.Add(new Cadre5 { Child = c, Coupe = 10, Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 10, 10), Background = Habillage.Pinceau(P.Accent, P.Sombre ? 14 : 10), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1) });
            }
            s.Children.Add(grille);
            if (NbMesures == 0) s.Children.Add(new TextBlock { Text = Langue.T("Les mesures sont notées toutes les 10 secondes pendant le mode jeu, tant que Velox est ouvert (même réduit)."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            string texte = "🎮 " + Langue.T("Session de jeu avec Velox") + "\n" + string.Join("\n", lignes.ConvertAll(l => "• " + l.Item1 + " : " + l.Item2)) + "\n" + Langue.T("Boosté avec Velox · damiencoussot-ai.github.io/nettoyage-pc");
            var copier = new BoutonNoyau("Copier pour Discord", true, 0, 42) { TaillePolice = 14, Capitales = true, HorizontalAlignment = HorizontalAlignment.Left };
            copier.Clic += async () => { try { if (f.Clipboard != null) await f.Clipboard.SetTextAsync(texte); f.Statut(Langue.T("Rapport copié : colle-le sur Discord avec Ctrl+V.")); } catch { } };
            s.Children.Add(copier);
            f.Ouvrir("Rapport de session", s, 760);
        }
    }
}
