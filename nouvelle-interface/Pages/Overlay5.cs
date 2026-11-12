// Velox 5 - overlay en jeu (profil Gamer) : petit bandeau toujours au premier plan, en haut à gauche de l'écran,
// avec le processeur, la mémoire vive, les températures et l'heure. Il laisse passer les clics (il ne gêne jamais le
// jeu) et s'affiche ou se cache avec Ctrl+Alt+O, même quand Velox est en arrière-plan. Visible dans les jeux en
// mode « fenêtré plein écran » (sans bordure) ; un jeu en plein écran exclusif passe par-dessus.
// Pendant le mode jeu, il note aussi les mesures de la session (rapport de session à la fin).
using System;
using System.Linq;
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
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint thread, uint msg, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
        private const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000, WM_HOTKEY = 0x312, WM_QUIT = 0x12;
        private static volatile uint filRaccourci;
        private static Thread filActif;
        private static readonly ManualResetEventSlim pret = new ManualResetEventSlim(false);
        [DllImport("user32.dll")] private static extern bool PeekMessageW(out MSG m, IntPtr h, uint a, uint b, uint remove);

        private static Window fenetre;
        private static FenetrePrincipale principale;
        private static bool raccourci;
        private static TextBlock tCpu, tGpu, tRam, tHeure, tFps;
        private static StackPanel zoneFps;
        private static Border cadreOverlay;
        private static int chaud;                                   // relevés chauds d'affilée
        private static DateTime derniereAlerte = DateTime.MinValue;
        private static double seuilCpu;                             // 0 = pas encore déterminé
        public const double SeuilGpu = 90;
        /// <summary>Température à partir de laquelle le processeur est jugé trop chaud : 92 °C, mais 87 °C pour les Ryzen X3D
        /// (leur limite est 89 °C : ils ralentissent avant d'atteindre 92). Lu une fois, hors du fil de l'interface.</summary>
        public static double SeuilCpu()
        {
            if (seuilCpu > 0) return seuilCpu;
            string nom = "";
            try { nom = InfosPc.Processeur() ?? ""; } catch { }
            return seuilCpu = nom.IndexOf("X3D", StringComparison.OrdinalIgnoreCase) >= 0 ? 87 : 92;
        }
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        private static readonly Color Rose = Color.FromArgb(170, 255, 43, 214), Rouge = Color.FromRgb(255, 70, 90);

        /// <summary>FPS en direct (Premium) : démarre ou arrête le compteur d'images de l'overlay. Une autorisation de Windows au démarrage.</summary>
        public static void BasculerFps(FenetrePrincipale f, Action<bool> fini)
        {
            if (Fps.DirectActif)
            {
                Fps.ArreterDirect();
                if (zoneFps != null) zoneFps.IsVisible = false;
                f.Statut(Langue.T("FPS en direct arrêtés."));
                if (fini != null) fini(false);
                return;
            }
            if (!Licence.Premium) { Achat5.Ouvrir(f); if (fini != null) fini(false); return; }
            f.Demander(Langue.T("Afficher les FPS en direct ?"),
                Langue.T("Velox compte les images affichées avec PresentMon (outil libre d'Intel, téléchargé depuis sa page officielle et vérifié). Windows demande une autorisation une seule fois. Le compteur s'arrête tout seul quand tu fermes Velox."),
                "Afficher les FPS", "Annuler", oui =>
            {
                if (!oui) { if (fini != null) fini(false); return; }
                f.Statut(Langue.T("Préparation des FPS en direct…"));
                Task.Run(() => Fps.DemarrerDirect(t => Dispatcher.UIThread.Post(() => f.Statut(t)))).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    string err = t.IsFaulted ? Langue.T("FPS en direct impossibles : ") + (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                    if (err != "") { f.Statut(err, true); if (fini != null) fini(false); return; }
                    if (fenetre == null) Basculer(f, true);
                    if (zoneFps != null) zoneFps.IsVisible = true;
                    f.Statut(Langue.T("FPS en direct : le compteur apparaît dans l'overlay dès qu'un jeu affiche des images."));
                    Task.Run(() => JournalJeu.Noter("fpsdirect"));
                    if (fini != null) fini(true);
                }));
            });
        }
        private static DispatcherTimer minuteur;
        private static bool mesureEnCours;

        public static bool Ouvert { get { return fenetre != null; } }

        /// <summary>À l'ouverture de Velox : raccourci Ctrl+Alt+O, overlay remis s'il était affiché, suivi de la session de jeu.</summary>
        public static void Initialiser(FenetrePrincipale f)
        {
            principale = f;
            if (f.Source.Immediat || !OperatingSystem.IsWindows()) return;
            Raccourci(Habillage.Gamer);
            bool voulu = false;
            try { voulu = Parametres.Lire().OverlayJeu; } catch { }
            if (voulu && Habillage.Gamer && fenetre == null) Basculer(f, false);
            SessionJeu5.Demarrer(f);
            BoiteNoire5.Demarrer(f);
            Task.Run(() => JournalJeu.Noter("ouverture"));   // série de jours du profil Gamer
        }

        /// <summary>Raccourci Ctrl+Alt+O, actif seulement avec le profil Gamer (sinon la combinaison reste libre pour
        /// les autres logiciels). Il est enregistré par un petit fil qui attend les messages de Windows.</summary>
        public static void Raccourci(bool actif)
        {
            if (!OperatingSystem.IsWindows()) return;
            if (!actif)
            {
                // arrête le petit fil proprement (on attend qu'il soit prêt, sinon le message d'arrêt serait perdu
                // et Ctrl+Alt+O resterait pris)
                if (raccourci && filActif != null)
                {
                    pret.Wait(1000);
                    if (filRaccourci != 0) PostThreadMessageW(filRaccourci, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                    filActif.Join(1000);
                }
                raccourci = false;
                if (fenetre != null) fenetre.Close();
                return;
            }
            if (raccourci) return;
            if (filActif != null && filActif.IsAlive) filActif.Join(1000);   // l'ancien fil doit avoir rendu la combinaison
            raccourci = true;
            pret.Reset();
            var fil = new Thread(() =>
            {
                try
                {
                    MSG p; PeekMessageW(out p, IntPtr.Zero, 0, 0, 0);   // crée la file de messages du fil avant d'annoncer qu'il est prêt
                    filRaccourci = GetCurrentThreadId();
                    pret.Set();
                    if (!RegisterHotKey(IntPtr.Zero, 0x5601, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, 'O')) { JournalDemarrage.Noter("Overlay : raccourci Ctrl+Alt+O déjà pris par un autre logiciel."); raccourci = false; return; }
                    MSG m;
                    while (GetMessageW(out m, IntPtr.Zero, 0, 0) > 0)
                        if (m.message == WM_HOTKEY) Dispatcher.UIThread.Post(() => Basculer(principale, true));
                    UnregisterHotKey(IntPtr.Zero, 0x5601);
                }
                catch (Exception ex) { JournalDemarrage.Erreur("raccourci de l'overlay", ex); }
                finally { filRaccourci = 0; pret.Set(); }
            }) { IsBackground = true, Name = "Raccourci overlay" };
            filActif = fil;
            fil.Start();
        }

        /// <summary>Affiche ou cache l'overlay (et retient le choix).</summary>
        public static void Basculer(FenetrePrincipale f, bool parRaccourci)
        {
            if (f == null) return;
            if (fenetre != null) { fenetre.Close(); f.EnregistrerReglage(r => r.OverlayJeu = false); return; }
            Ouvrir(f);
            f.EnregistrerReglage(r => r.OverlayJeu = true);
            if (!parRaccourci) f.Statut(Langue.T("Overlay en jeu affiché : Ctrl+Alt+O pour l'afficher ou le cacher."));
        }

        private static TextBlock Valeur() { return new TextBlock { FontFamily = Habillage.Console, FontSize = 13, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center }; }
        private static TextBlock Nom(string t) { return new TextBlock { Text = t, FontFamily = Habillage.Console, FontSize = 10.5, Foreground = new SolidColorBrush(Color.FromRgb(255, 120, 230)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 6, 0) }; }

        private static void Ouvrir(FenetrePrincipale f)
        {
            tCpu = Valeur(); tGpu = Valeur(); tRam = Valeur(); tHeure = Valeur(); tFps = Valeur();
            tCpu.Text = tGpu.Text = tRam.Text = "…"; tHeure.Text = DateTime.Now.ToString("HH:mm"); tFps.Text = "…";
            tFps.FontSize = 15; tFps.FontWeight = FontWeight.Bold; tFps.Foreground = new SolidColorBrush(Color.FromRgb(120, 255, 170));
            zoneFps = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, IsVisible = Fps.DirectActif, Children = { Nom("FPS"), tFps, new Border { Width = 14 } } };
            chaud = 0;
            var ligne = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            ligne.Children.Add(new TextBlock { Text = "V//", FontFamily = Habillage.Affichage, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(Color.FromRgb(34, 230, 255)), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
            ligne.Children.Add(zoneFps);
            ligne.Children.Add(Nom("CPU")); ligne.Children.Add(tCpu);
            ligne.Children.Add(new Border { Width = 14 });
            ligne.Children.Add(Nom("GPU")); ligne.Children.Add(tGpu);
            ligne.Children.Add(new Border { Width = 14 });
            ligne.Children.Add(Nom("RAM")); ligne.Children.Add(tRam);
            ligne.Children.Add(new Border { Width = 14 });
            ligne.Children.Add(tHeure);
            var cadre = new Border { Child = ligne, Padding = new Thickness(12, 6), Background = new SolidColorBrush(Color.FromArgb(185, 8, 3, 20)), BorderBrush = new SolidColorBrush(Color.FromArgb(170, 255, 43, 214)), BorderThickness = new Thickness(1) };
            cadreOverlay = cadre;   // gardé pour le passer en rouge en cas de surchauffe
            var w = new Window
            {
                SizeToContent = SizeToContent.WidthAndHeight, SystemDecorations = SystemDecorations.None, Topmost = true, CanResize = false, ShowInTaskbar = false, ShowActivated = false,
                Background = Brushes.Transparent, TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }, Title = "Velox overlay", Content = cadre, Focusable = false,
            };
            w.Opened += (s, e) =>
            {
                try
                {
                    Placer(w);
                    Dispatcher.UIThread.Post(() => { try { if (fenetre == w) Placer(w); } catch { } }, DispatcherPriority.Background);   // une fois la taille connue
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
            if (!fermetureSuivie) { fermetureSuivie = true; f.Closed += (s, e) => { if (fenetre != null) fenetre.Close(); }; }
            fenetre = w;
            w.Show();
            Task.Run(() => JournalJeu.Noter("overlay"));
            minuteur.Start();
            Mesurer(f);
        }

        private static bool fermetureSuivie;
        private static int coin = -1;
        private static readonly string[] NomsCoins = { "en haut à gauche", "en haut à droite", "en bas à droite", "en bas à gauche" };

        /// <summary>Place l'overlay dans le coin choisi de l'écran principal (zone de travail : jamais sous la barre des tâches).</summary>
        private static void Placer(Window w)
        {
            if (coin < 0) { try { coin = Math.Max(0, Math.Min(3, Parametres.Lire().OverlayCoin)); } catch { coin = 0; } }
            var ecran = w.Screens.Primary;
            if (ecran == null) return;
            var z = ecran.WorkingArea;
            int marge = (int)(12 * ecran.Scaling);
            int l = (int)Math.Ceiling(w.Bounds.Width * ecran.Scaling), h = (int)Math.Ceiling(w.Bounds.Height * ecran.Scaling);
            int x = coin == 1 || coin == 2 ? z.Right - l - marge : z.X + marge;
            int y = coin >= 2 ? z.Bottom - h - marge : z.Y + marge;
            w.Position = new PixelPoint(x, y);
        }

        /// <summary>Passe l'overlay au coin suivant de l'écran (et retient le choix).</summary>
        public static void ChangerCoin(FenetrePrincipale f)
        {
            if (coin < 0) { try { coin = Math.Max(0, Math.Min(3, Parametres.Lire().OverlayCoin)); } catch { coin = 0; } }
            coin = (coin + 1) % 4;
            int c = coin;
            f.EnregistrerReglage(r => r.OverlayCoin = c);
            if (fenetre != null) { try { Placer(fenetre); } catch { } }
            f.Statut(Langue.F("Overlay placé {0}.", Langue.T(NomsCoins[c])) + (fenetre == null ? " " + Langue.T("Affiche-le avec Ctrl+Alt+O (profil Gamer).") : ""));
        }

        private static void Mesurer(FenetrePrincipale f)
        {
            if (mesureEnCours || fenetre == null) return;
            mesureEnCours = true;
            Task.Run(() =>
            {
                Mesures m = null; double g = -1, usage = -1, fps = -1;
                try { m = f.Source.Mesurer(true); } catch { }
                try { g = TemperatureGpu.Lire(); } catch { }
                try { usage = UsageGpu.Lire(); } catch { }
                // les FPS sont lus une seule fois par seconde, par la Boîte noire (qui enregistre aussi la partie)
                if (Fps.DirectActif) fps = BoiteNoire5.FpsActuel;
                try { SeuilCpu(); } catch { }
                return Tuple.Create(m, g, usage, fps);
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                mesureEnCours = false;
                if (fenetre == null || t.IsFaulted) return;
                var m = t.Result.Item1; double g = t.Result.Item2, usage = t.Result.Item3, fps = t.Result.Item4;
                if (zoneFps != null)
                {
                    bool actif = Fps.DirectActif;
                    if (zoneFps.IsVisible != actif) { zoneFps.IsVisible = actif; Dispatcher.UIThread.Post(() => { try { if (fenetre != null) Placer(fenetre); } catch { } }, DispatcherPriority.Background); }
                    if (actif && fps > -2) tFps.Text = fps > 0 ? Math.Round(fps).ToString() : "—";   // -2 : pas de nouvelle image depuis une seconde, on garde l'affichage
                }
                // surchauffe : cadre rouge tout de suite, alerte (message + son) après 5 secondes d'affilée, au plus toutes les 10 minutes
                bool tropChaud = (m != null && m.Temperature >= (seuilCpu > 0 ? seuilCpu : 92)) || g >= SeuilGpu;
                chaud = tropChaud ? chaud + 1 : 0;
                if (cadreOverlay != null) cadreOverlay.BorderBrush = new SolidColorBrush(tropChaud ? Rouge : Rose);
                if (chaud == 5 && (DateTime.Now - derniereAlerte).TotalMinutes >= 10)
                {
                    derniereAlerte = DateTime.Now;
                    f.Statut(Langue.F("Surchauffe : {0}. Baisse les réglages graphiques du jeu ou vérifie les ventilateurs.", g >= SeuilGpu ? Langue.F("carte graphique à {0} °C", Math.Round(g)) : Langue.F("processeur à {0} °C", Math.Round(m.Temperature))), true);
                    try { Sons5.Alerte(); } catch { }
                    Task.Run(() => JournalJeu.Noter("surchauffe"));
                }
                if (m != null)
                {
                    tCpu.Text = (m.Processeur >= 0 ? Math.Round(m.Processeur) + "%" : "—") + (m.Temperature > 0 ? " " + Math.Round(m.Temperature) + "°" : "");
                    tRam.Text = m.Memoire >= 0 ? Math.Round(m.Memoire) + "%" : "—";
                    tCpu.Foreground = new SolidColorBrush(m.Temperature >= 85 ? Color.FromRgb(255, 122, 138) : Colors.White);
                }
                tGpu.Text = (usage >= 0 ? Math.Round(usage) + "%" : "") + (g > 0 ? (usage >= 0 ? " " : "") + Math.Round(g) + "°" : usage >= 0 ? "" : "—");
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
        private static DateTime overlayForce = DateTime.MinValue;   // dernière fois que la surchauffe a affiché l'overlay
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
            Task.Run(() => { Mesures m = null; double g = -1; try { m = f.Source.Mesurer(true); } catch { } try { g = TemperatureGpu.Lire(); } catch { } try { Overlay5.SeuilCpu(); } catch { } return Tuple.Create(m, g); })
                .ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    enCours = false;
                    if (t.IsFaulted || t.Result.Item1 == null) return;
                    var m = t.Result.Item1;
                    if (m.Processeur >= 0) { CpuMoyen = (CpuMoyen * NbMesures + m.Processeur) / (NbMesures + 1); CpuMax = Math.Max(CpuMax, m.Processeur); NbMesures++; }
                    if (m.Temperature > 0) TempMax = Math.Max(TempMax, m.Temperature);
                    if (t.Result.Item2 > 0) GpuMax = Math.Max(GpuMax, t.Result.Item2);
                    if (m.Memoire >= 0) RamMax = Math.Max(RamMax, m.Memoire);
                    // surchauffe pendant une partie alors que l'overlay est caché : il s'affiche (cadre rouge), pour que ça se voie en jeu
                    // (au plus une fois toutes les 10 minutes : si tu le caches, il ne revient pas aussitôt)
                    if ((m.Temperature >= Overlay5.SeuilCpu() || t.Result.Item2 >= Overlay5.SeuilGpu) && !Overlay5.Ouvert && OperatingSystem.IsWindows() && !f.Source.Immediat && (DateTime.Now - overlayForce).TotalMinutes >= 10)
                    { overlayForce = DateTime.Now; try { Overlay5.Basculer(f, true); } catch { } }
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
            // la Boîte noire a suivi cette partie : FPS et saccades
            SessionBoite boite = null;
            try { boite = BoiteNoire.Courante ?? (debut.HasValue ? BoiteNoire.Parties().FirstOrDefault(x => x.Fin >= debut.Value) : null); } catch { }
            if (boite != null && boite.FpsMoyen > 0)
            {
                lignes.Add(Tuple.Create(Langue.T("FPS MOYEN"), Math.Round(boite.FpsMoyen).ToString()));
                lignes.Add(Tuple.Create(Langue.T("FPS MINIMUM (1 %)"), Math.Round(boite.Fps1).ToString()));
                lignes.Add(Tuple.Create(Langue.T("SACCADES"), boite.Saccades.Count.ToString()));
            }
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
            var boutonsFin = new WrapPanel();
            copier.Margin = new Thickness(0, 0, 8, 0);
            boutonsFin.Children.Add(copier);
            var voirBoite = new BoutonNoyau("Boîte noire", false, 0, 42) { TaillePolice = 14 };
            voirBoite.Clic += () => { f.ViderCalques(); BoiteNoire5.Ouvrir(f); };
            boutonsFin.Children.Add(voirBoite);
            s.Children.Add(boutonsFin);
            f.Ouvrir("Rapport de session", s, 760);
        }
    }
}
