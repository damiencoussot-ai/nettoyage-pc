// Nettoyage PC 5 - gardien en temps réel (Premium) : icône près de l'horloge, menu, notifications Windows. Lancé avec
// --gardien à l'ouverture de session, sans droits administrateur et sans fenêtre visible. Les vérifications sont celles
// du moteur partagé (GardienCoeur), comme en version 4 ; ici, seulement l'icône, écrite directement avec Windows
// (pas d'interface graphique chargée : le gardien reste léger).
using System;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class GardienNatif : ISurfaceGardien
    {
        private const int WM_TIMER = 0x0113, WM_COMMAND = 0x0111, WM_DESTROY = 0x0002, WM_NULL = 0, WM_APP_UI = 0x8000 + 0x4E51;
        private const int WM_LBUTTONDBLCLK = 0x0203, WM_RBUTTONUP = 0x0205, WM_CONTEXTMENU = 0x007B, NIN_BALLOONUSERCLICK = 0x0405;
        private const int NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2, NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_INFO = 0x10;
        private const int MF_STRING = 0, MF_GRAYED = 1, MF_SEPARATOR = 0x800, TPM_RETURNCMD = 0x100, TPM_RIGHTBUTTON = 2;
        private const int MINUTEUR = 1, PREMIER = 2;
        private const int C_OUVRIR = 10, C_SAUVEGARDER = 11, C_REGLAGES = 12, C_PAUSE = 13, C_ARRETER = 14;

        private static Mutex verrou;
        private readonly ConcurrentQueue<Action> aFaire = new ConcurrentQueue<Action>();
        private readonly ProcFenetre proc;      // gardé : Windows l'appelle tant que la fenêtre existe
        private IntPtr fenetre, icone;
        private bool iconeExtraite;
        private int msgBarreRecreee;
        private string infobulle;
        private Action actionClic;
        private GardienCoeur coeur;
        private RegisteredWaitHandle attenteArret, attenteRecharger;
        private EventWaitHandle evArret, evRecharger;

        public static int Executer()
        {
            if (!OperatingSystem.IsWindows()) return 0;
            bool nouveau;
            verrou = new Mutex(true, Gardien.NomMutex, out nouveau);
            if (!nouveau) return 0;
            try
            {
                if (!GardienCoeur.DoitTourner()) return 0;
                JournalDemarrage.Noter("Gardien démarré (version 5).");
                new GardienNatif().Boucle();
                JournalDemarrage.Noter("Gardien arrêté (version 5).");
                return 0;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("gardien (version 5)", ex); return 1; }
            finally { try { verrou.ReleaseMutex(); } catch { } }
        }

        private GardienNatif() { proc = Recevoir; }

        private void Boucle()
        {
            var classe = new WNDCLASSEX { cbSize = Marshal.SizeOf(typeof(WNDCLASSEX)), lpfnWndProc = proc, hInstance = GetModuleHandle(null), lpszClassName = "NettoyagePC-Gardien-5" };
            RegisterClassEx(ref classe);
            // fenêtre jamais affichée : elle reçoit les clics sur l'icône et porte le menu
            fenetre = CreateWindowEx(0, classe.lpszClassName, "Nettoyage PC - gardien", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, classe.hInstance, IntPtr.Zero);
            if (fenetre == IntPtr.Zero) throw new InvalidOperationException("fenêtre du gardien impossible à créer");
            msgBarreRecreee = RegisterWindowMessage("TaskbarCreated");
            icone = NotifNative.ExtractIcon(IntPtr.Zero, Chemins.Exe, 0);
            iconeExtraite = icone != IntPtr.Zero && icone != (IntPtr)1;
            if (!iconeExtraite) icone = NotifNative.LoadIcon(IntPtr.Zero, (IntPtr)32518);   // IDI_SHIELD
            infobulle = Langue.T("Nettoyage PC : gardien actif");
            AjouterIcone();
            coeur = new GardienCoeur(this);

            evArret = new EventWaitHandle(false, EventResetMode.AutoReset, Gardien.NomArret);
            evRecharger = new EventWaitHandle(false, EventResetMode.AutoReset, Gardien.NomRecharger);
            attenteArret = ThreadPool.RegisterWaitForSingleObject(evArret, (o, t) => SurUi(Quitter), null, -1, true);
            attenteRecharger = ThreadPool.RegisterWaitForSingleObject(evRecharger, (o, t) => SurUi(coeur.Recharger), null, -1, false);
            SetTimer(fenetre, MINUTEUR, 60000, IntPtr.Zero);
            SetTimer(fenetre, PREMIER, 30000, IntPtr.Zero);   // première vérification une demi-minute après l'ouverture de session

            MSG m;
            while (GetMessage(out m, IntPtr.Zero, 0, 0) > 0) { TranslateMessage(ref m); DispatchMessage(ref m); }
            try { if (iconeExtraite) NotifNative.DestroyIcon(icone); } catch { }
        }

        private NotifNative.NOTIFYICONDATA Donnees(int drapeaux)
        {
            return new NotifNative.NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf(typeof(NotifNative.NOTIFYICONDATA)), hWnd = fenetre, uID = 1, uFlags = drapeaux,
                uCallbackMessage = NotifNative.WM_RAPPEL, hIcon = icone, szTip = Couper(infobulle, 127), szInfo = "", szInfoTitle = "",
            };
        }

        private void AjouterIcone() { var d = Donnees(NIF_MESSAGE | NIF_ICON | NIF_TIP); NotifNative.Shell_NotifyIcon(NIM_ADD, ref d); }

        private static string Couper(string s, int max) { s = s ?? ""; return s.Length <= max ? s : s.Substring(0, max - 1) + "…"; }

        // ------------------------------------------------------------ ISurfaceGardien
        public void SurUi(Action a) { aFaire.Enqueue(a); PostMessage(fenetre, WM_APP_UI, IntPtr.Zero, IntPtr.Zero); }

        public void Infobulle(string texte)
        {
            infobulle = texte;
            var d = Donnees(NIF_TIP);
            NotifNative.Shell_NotifyIcon(NIM_MODIFY, ref d);
        }

        public void Notifier(string titre, string texte, int type, Action clic)
        {
            actionClic = clic;
            var d = Donnees(NIF_INFO);
            d.szInfoTitle = Couper(titre, 63); d.szInfo = Couper(texte, 255);
            d.dwInfoFlags = type == 2 ? 3 : type == 1 ? 2 : 1;   // erreur, avertissement, information
            d.uTimeoutOrVersion = 15000;
            NotifNative.Shell_NotifyIcon(NIM_MODIFY, ref d);
        }

        public void Quitter()
        {
            try { KillTimer(fenetre, MINUTEUR); KillTimer(fenetre, PREMIER); } catch { }
            try { coeur.Arreter(); } catch { }
            try { attenteArret.Unregister(null); attenteRecharger.Unregister(null); } catch { }
            var d = Donnees(0);
            NotifNative.Shell_NotifyIcon(NIM_DELETE, ref d);
            DestroyWindow(fenetre);
        }

        // ------------------------------------------------------------ messages de Windows
        private IntPtr Recevoir(IntPtr h, int msg, IntPtr w, IntPtr l)
        {
            try
            {
                if (msg == NotifNative.WM_RAPPEL)
                {
                    int quoi = (int)((long)l & 0xFFFF);
                    if (quoi == WM_LBUTTONDBLCLK) GardienCoeur.OuvrirAppli(null);
                    else if (quoi == WM_RBUTTONUP || quoi == WM_CONTEXTMENU) Menu();
                    else if (quoi == NIN_BALLOONUSERCLICK) { var a = actionClic; actionClic = null; if (a != null) a(); }
                    return IntPtr.Zero;
                }
                if (msg == WM_APP_UI) { Action a; while (aFaire.TryDequeue(out a)) { try { a(); } catch (Exception ex) { JournalDemarrage.Erreur("gardien (version 5)", ex); } } return IntPtr.Zero; }
                if (msg == WM_TIMER)
                {
                    if ((int)w == PREMIER) KillTimer(h, PREMIER);
                    if (coeur != null) coeur.Verifier();
                    return IntPtr.Zero;
                }
                if (msg == WM_DESTROY) { PostQuitMessage(0); return IntPtr.Zero; }
                if (msgBarreRecreee != 0 && msg == msgBarreRecreee) { AjouterIcone(); return IntPtr.Zero; }   // l'Explorateur a redémarré
            }
            catch (Exception ex) { JournalDemarrage.Erreur("gardien (version 5)", ex); }
            return DefWindowProc(h, msg, w, l);
        }

        private void Menu()
        {
            IntPtr m = CreatePopupMenu();
            AppendMenu(m, MF_STRING | MF_GRAYED, IntPtr.Zero, Langue.T("Nettoyage PC : gardien actif"));
            AppendMenu(m, MF_SEPARATOR, IntPtr.Zero, null);
            AppendMenu(m, MF_STRING, (IntPtr)C_OUVRIR, Langue.T("Ouvrir Nettoyage PC"));
            AppendMenu(m, MF_STRING, (IntPtr)C_SAUVEGARDER, Langue.T("Sauvegarder maintenant"));
            AppendMenu(m, MF_STRING, (IntPtr)C_REGLAGES, Langue.T("Réglages du gardien..."));
            AppendMenu(m, MF_STRING, (IntPtr)C_PAUSE, Langue.T("Pause d'une heure"));
            AppendMenu(m, MF_SEPARATOR, IntPtr.Zero, null);
            AppendMenu(m, MF_STRING, (IntPtr)C_ARRETER, Langue.T("Arrêter le gardien"));
            POINT p; GetCursorPos(out p);
            SetForegroundWindow(fenetre);   // sans cela, le menu ne se ferme pas quand on clique ailleurs
            int choix = TrackPopupMenuEx(m, TPM_RETURNCMD | TPM_RIGHTBUTTON, p.X, p.Y, fenetre, IntPtr.Zero);
            PostMessage(fenetre, WM_NULL, IntPtr.Zero, IntPtr.Zero);
            DestroyMenu(m);
            switch (choix)
            {
                case C_OUVRIR: GardienCoeur.OuvrirAppli(null); break;
                case C_SAUVEGARDER: coeur.SauvegarderMaintenant(); break;
                case C_REGLAGES: GardienCoeur.OuvrirAppli("--ouvrir gardien"); break;
                case C_PAUSE: coeur.Pause(); break;
                case C_ARRETER: Quitter(); break;
            }
        }

        // ------------------------------------------------------------ Windows
        private delegate IntPtr ProcFenetre(IntPtr h, int msg, IntPtr w, IntPtr l);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASSEX
        {
            public int cbSize, style; public ProcFenetre lpfnWndProc; public int cbClsExtra, cbWndExtra; public IntPtr hInstance, hIcon, hCursor, hbrBackground;
            public string lpszMenuName, lpszClassName; public IntPtr hIconSm;
        }
        [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public int message; public IntPtr wParam, lParam; public int time, x, y; }
        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int X, Y; }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern ushort RegisterClassEx(ref WNDCLASSEX c);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateWindowEx(int exStyle, string classe, string titre, int style, int x, int y, int l, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [DllImport("user32.dll")] private static extern bool DestroyWindow(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DefWindowProc(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetMessage(out MSG m, IntPtr h, int min, int max);
        [DllImport("user32.dll")] private static extern bool TranslateMessage(ref MSG m);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr DispatchMessage(ref MSG m);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PostMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] private static extern void PostQuitMessage(int code);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int RegisterWindowMessage(string nom);
        [DllImport("user32.dll")] private static extern IntPtr SetTimer(IntPtr h, int id, int ms, IntPtr proc);
        [DllImport("user32.dll")] private static extern bool KillTimer(IntPtr h, int id);
        [DllImport("user32.dll")] private static extern IntPtr CreatePopupMenu();
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool AppendMenu(IntPtr m, int drapeaux, IntPtr id, string texte);
        [DllImport("user32.dll")] private static extern int TrackPopupMenuEx(IntPtr m, int drapeaux, int x, int y, IntPtr h, IntPtr param);
        [DllImport("user32.dll")] private static extern bool DestroyMenu(IntPtr m);
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string nom);
    }
}
