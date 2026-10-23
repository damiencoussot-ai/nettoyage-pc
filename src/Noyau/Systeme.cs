// Nettoyage PC - informations sur le PC (WMI), notifications, journaux d'événements
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
#if !NET5_0_OR_GREATER
using System.Drawing;
#endif
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Threading;
#if !NET5_0_OR_GREATER
using System.Windows.Forms;
#endif

namespace NettoyagePC
{
    /// <summary>Requêtes WMI protégées : une classe absente renvoie simplement une liste vide.</summary>
    internal static class Wmi
    {
        public static List<ManagementObject> Requete(string requete, string espace = @"root\cimv2", int delaiSecondes = 30)
        {
            var l = new List<ManagementObject>();
            if (!Natif.EstWindows) return l;
            try
            {
                var scope = new ManagementScope(espace);
                var opts = new System.Management.EnumerationOptions { Timeout = TimeSpan.FromSeconds(delaiSecondes), ReturnImmediately = true, Rewindable = false };
                using (var s = new ManagementObjectSearcher(scope, new ObjectQuery(requete), opts))
                    foreach (ManagementObject o in s.Get()) l.Add(o);
            }
            catch { }
            return l;
        }

        public static string Texte(ManagementBaseObject o, string p) { try { var v = o[p]; return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture).Trim(); } catch { return ""; } }
        public static long Long(ManagementBaseObject o, string p) { try { var v = o[p]; return v == null ? 0 : Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return 0; } }
        public static bool Bool(ManagementBaseObject o, string p) { try { var v = o[p]; return v != null && Convert.ToBoolean(v); } catch { return false; } }
        public static object Valeur(ManagementBaseObject o, string p) { try { return o[p]; } catch { return null; } }

        public static DateTime? Date(ManagementBaseObject o, string p)
        {
            try { var v = o[p] as string; if (string.IsNullOrEmpty(v)) return null; return ManagementDateTimeConverter.ToDateTime(v); } catch { return null; }
        }
    }

    internal class CarteGraphique
    {
        public string Nom = "", Fabricant = "", PnpId = "", VersionPilote = "";
        public DateTime? DatePilote;
        public long Memoire;
        /// <summary>Puce graphique intégrée au processeur (moins puissante qu'une carte dédiée).</summary>
        public bool Integree
        {
            get
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(Nom, @"\b(RX|R9|R7|GeForce|RTX|GTX|Quadro|Arc A\d|Radeon Pro|FirePro|Tesla)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return false;
                return System.Text.RegularExpressions.Regex.IsMatch(Nom, @"Radeon\(TM\) Graphics|Radeon Graphics|Vega \d+ Graphics|Radeon \d{3}M|UHD Graphics|HD Graphics|Iris|Intel\(R\) Graphics|Microsoft Basic", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
        }
        public string Marque
        {
            get
            {
                if (PnpId.Contains("VEN_10DE") || Nom.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) return "NVIDIA";
                if (PnpId.Contains("VEN_1002") || Regex(Nom, "AMD|Radeon|Advanced Micro Devices")) return "AMD";
                if (PnpId.Contains("VEN_8086") || Nom.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0) return "Intel";
                return "";
            }
        }
        private static bool Regex(string s, string m) { return System.Text.RegularExpressions.Regex.IsMatch(s, m, System.Text.RegularExpressions.RegexOptions.IgnoreCase); }
    }

    internal static class InfosPc
    {
        /// <summary>Température : carte graphique si possible (source "gpu"), sinon capteur de la carte mère vu par Windows ("systeme") ; -1 si aucune.</summary>
        public static double Temperature(out string source)
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

        private static string resume;

        public static string VersionWindows
        {
            get
            {
                int b = Natif.Build;
                if (!Natif.EstWindows) return "Système de test";
                return b >= 22000 ? "Windows 11" : b >= 10240 ? "Windows 10" : "Windows ancien";
            }
        }

        public static string Edition
        {
            get
            {
                var os = Wmi.Requete("SELECT Caption FROM Win32_OperatingSystem").FirstOrDefault();
                string c = os == null ? VersionWindows : Wmi.Texte(os, "Caption");
                return c.Replace("Microsoft ", "");
            }
        }

        public static bool EstPortable { get { return Wmi.Requete("SELECT Name FROM Win32_Battery").Count > 0; } }

        public static double RamGo
        {
            get { ulong t, d; Natif.Memoire(out t, out d); return t / 1073741824.0; }
        }

        public static string Resume()
        {
            if (resume != null) return resume;
            string arch = Environment.Is64BitOperatingSystem ? "64 bits" : "32 bits";
            resume = string.Format(Appli.Fr, "{0} (build {1}, {2}) - {3} - {4:N0} Go de RAM", Edition, Natif.Build, arch, EstPortable ? "PC portable" : "PC fixe", RamGo);
            return resume;
        }

        public static List<CarteGraphique> CartesGraphiques()
        {
            var l = new List<CarteGraphique>();
            foreach (var o in Wmi.Requete("SELECT Name, AdapterCompatibility, PNPDeviceID, DriverVersion, DriverDate, AdapterRAM FROM Win32_VideoController"))
            {
                var c = new CarteGraphique
                {
                    Nom = Wmi.Texte(o, "Name"), Fabricant = Wmi.Texte(o, "AdapterCompatibility"), PnpId = Wmi.Texte(o, "PNPDeviceID"),
                    VersionPilote = Wmi.Texte(o, "DriverVersion"), DatePilote = Wmi.Date(o, "DriverDate"), Memoire = Wmi.Long(o, "AdapterRAM")
                };
                if (c.PnpId.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase)) l.Add(c);
            }
            // carte dédiée d'abord (une RX 7900 XTX passe avant la puce « Radeon(TM) Graphics » d'un Ryzen)
            return l.OrderBy(c => c.Integree ? 1 : 0).ThenBy(c => c.Marque == "NVIDIA" || c.Marque == "AMD" ? 0 : 1).ThenByDescending(c => c.Memoire).ToList();
        }

        public static CarteGraphique CarteGraphiquePrincipale() { return CartesGraphiques().FirstOrDefault(); }

        public static string Processeur()
        {
            var p = Wmi.Requete("SELECT Name FROM Win32_Processor").FirstOrDefault();
            return p == null ? "" : Wmi.Texte(p, "Name");
        }

        public static bool RedemarrageEnAttente()
        {
            return CleExiste(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired")
                || CleExiste(@"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
        }

        public static bool CleExiste(string cle)
        {
            try
            {
                string s = cle.Substring(5);
                using (var b = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64))
                using (var k = b.OpenSubKey(s)) return k != null;
            }
            catch { return false; }
        }

        public static string Winget()
        {
            try
            {
                string local = Path.Combine(Chemins.LocalAppData, @"Microsoft\WindowsApps\winget.exe");
                if (File.Exists(local)) return local;
                string pf = Path.Combine(Chemins.ProgramFiles, "WindowsApps");
                if (Directory.Exists(pf))
                {
                    var d = Directory.GetDirectories(pf, "Microsoft.DesktopAppInstaller_*_x64__8wekyb3d8bbwe").OrderByDescending(x => x).FirstOrDefault();
                    if (d != null && File.Exists(Path.Combine(d, "winget.exe"))) return Path.Combine(d, "winget.exe");
                }
            }
            catch { }
            // dernier recours : le PATH
            foreach (var p in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';'))
            {
                try { string f = Path.Combine(p.Trim(), "winget.exe"); if (p.Trim().Length > 0 && File.Exists(f)) return f; } catch { }
            }
            return null;
        }

        public static bool DefenderTempsReel()
        {
            var mp = Wmi.Requete("SELECT RealTimeProtectionEnabled FROM MSFT_MpComputerStatus", @"root\Microsoft\Windows\Defender").FirstOrDefault();
            return mp != null && Wmi.Bool(mp, "RealTimeProtectionEnabled");
        }

        public static List<string> Antivirus()
        {
            return Wmi.Requete("SELECT displayName FROM AntiVirusProduct", @"root\SecurityCenter2").Select(o => Wmi.Texte(o, "displayName")).Where(x => x != "").Distinct().ToList();
        }
    }

    internal static class Evenements
    {
        /// <summary>Événements d'un journal Windows (requête XPath), les plus récents d'abord.</summary>
        public static List<EventRecord> Lire(string journal, string xpath, int max = 50)
        {
            var l = new List<EventRecord>();
            if (!Natif.EstWindows) return l;
            try
            {
                var q = new EventLogQuery(journal, PathType.LogName, xpath) { ReverseDirection = true };
                using (var r = new EventLogReader(q))
                {
                    EventRecord e;
                    while (l.Count < max && (e = r.ReadEvent()) != null) l.Add(e);
                }
            }
            catch { }
            return l;
        }

        /// <summary>Date du premier événement trouvé dont la source contient le texte donné (sans exposer EventRecord à l'appelant).</summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public static DateTime? DatePremier(string journal, string xpath, int max, string source)
        {
            try { var e = Lire(journal, xpath, max).FirstOrDefault(x => (x.ProviderName ?? "").Contains(source)); return e == null ? null : e.TimeCreated; }
            catch { return null; }
        }

        public static string Message(EventRecord e)
        {
            try { return e.FormatDescription() ?? ""; } catch { return ""; }
        }
    }

#if NET5_0_OR_GREATER
    /// <summary>
    /// Notification Windows (bulle de la zone de notification, affichée comme une notification), sans Windows Forms :
    /// une fenêtre cachée, une icône dans la zone de notification le temps de la bulle, puis tout est retiré.
    /// Un clic sur la bulle ouvre Nettoyage PC.
    /// </summary>
    internal static class Notification
    {
        public static void Afficher(string titre, string texte)
        {
            if (!OperatingSystem.IsWindows()) return;
            IntPtr fenetre = IntPtr.Zero, icone = IntPtr.Zero;
            bool iconeExtraite = false, ajoutee = false;
            var d = new NotifNative.NOTIFYICONDATA();
            try
            {
                fenetre = NotifNative.CreateWindowEx(0, "STATIC", "Nettoyage PC - notification", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (fenetre == IntPtr.Zero) return;
                icone = NotifNative.ExtractIcon(IntPtr.Zero, Chemins.Exe, 0);
                iconeExtraite = icone != IntPtr.Zero && icone != (IntPtr)1;
                if (!iconeExtraite) icone = NotifNative.LoadIcon(IntPtr.Zero, (IntPtr)32516);   // IDI_INFORMATION
                d.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(NotifNative.NOTIFYICONDATA));
                d.hWnd = fenetre; d.uID = 1; d.uCallbackMessage = NotifNative.WM_RAPPEL; d.hIcon = icone;
                d.uFlags = NotifNative.NIF_MESSAGE | NotifNative.NIF_ICON | NotifNative.NIF_TIP | NotifNative.NIF_INFO;
                d.szTip = "Nettoyage PC";
                d.szInfo = Couper(texte, 255); d.szInfoTitle = Couper(titre, 63);
                d.dwInfoFlags = NotifNative.NIIF_INFO; d.uTimeoutOrVersion = 10000;
                ajoutee = NotifNative.Shell_NotifyIcon(NotifNative.NIM_ADD, ref d);
                if (!ajoutee) return;
                var fin = DateTime.Now.AddSeconds(9);
                NotifNative.MSG m;
                while (DateTime.Now < fin)
                {
                    while (NotifNative.PeekMessage(out m, IntPtr.Zero, 0, 0, 1))
                    {
                        if (m.message == NotifNative.WM_RAPPEL && ((long)m.lParam & 0xFFFF) == NotifNative.NIN_BALLOONUSERCLICK)
                        {
                            try { Process.Start(new ProcessStartInfo(Chemins.Exe) { UseShellExecute = true }); } catch { }
                            fin = DateTime.Now;
                        }
                        NotifNative.TranslateMessage(ref m);
                        NotifNative.DispatchMessage(ref m);
                    }
                    Thread.Sleep(100);
                }
            }
            catch { }
            finally
            {
                try { if (ajoutee) NotifNative.Shell_NotifyIcon(NotifNative.NIM_DELETE, ref d); } catch { }
                try { if (iconeExtraite) NotifNative.DestroyIcon(icone); } catch { }
                try { if (fenetre != IntPtr.Zero) NotifNative.DestroyWindow(fenetre); } catch { }
            }
        }

        private static string Couper(string s, int max) { s = s ?? ""; return s.Length <= max ? s : s.Substring(0, max - 1) + "…"; }
    }

    internal static class NotifNative
    {
        public const int NIM_ADD = 0, NIM_DELETE = 2, NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_INFO = 0x10, NIIF_INFO = 1;
        public const int WM_RAPPEL = 0x8000 + 0x4E50, NIN_BALLOONUSERCLICK = 0x405;

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        public struct NOTIFYICONDATA
        {
            public int cbSize; public IntPtr hWnd; public int uID; public int uFlags; public int uCallbackMessage; public IntPtr hIcon;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public int dwState, dwStateMask;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public int uTimeoutOrVersion;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public int dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
        }

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct MSG { public IntPtr hwnd; public int message; public IntPtr wParam, lParam; public int time, x, y; }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] public static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);
        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] public static extern IntPtr ExtractIcon(IntPtr instance, string fichier, int index);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] public static extern IntPtr CreateWindowEx(int exStyle, string classe, string titre, int style, int x, int y, int l, int h, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool DestroyWindow(IntPtr fenetre);
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern IntPtr LoadIcon(IntPtr instance, IntPtr nom);
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icone);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] public static extern bool PeekMessage(out MSG m, IntPtr fenetre, int min, int max, int retirer);
        [System.Runtime.InteropServices.DllImport("user32.dll")] public static extern bool TranslateMessage(ref MSG m);
        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] public static extern IntPtr DispatchMessage(ref MSG m);
    }
#else
    /// <summary>Notification Windows (bulle de la zone de notification, affichée comme une notification).</summary>
    internal static class Notification
    {
        public static void Afficher(string titre, string texte)
        {
            try
            {
                using (var icone = new NotifyIcon())
                {
                    try { icone.Icon = Icon.ExtractAssociatedIcon(Chemins.Exe); } catch { icone.Icon = SystemIcons.Information; }
                    icone.Text = "Nettoyage PC";
                    icone.Visible = true;
                    icone.BalloonTipClicked += (s, e) => { try { Process.Start(new ProcessStartInfo(Chemins.Exe) { UseShellExecute = true }); } catch { } };
                    icone.ShowBalloonTip(10000, titre, texte, ToolTipIcon.Info);
                    var fin = DateTime.Now.AddSeconds(9);
                    while (DateTime.Now < fin) { Application.DoEvents(); Thread.Sleep(100); }
                    icone.Visible = false;
                }
            }
            catch { }
        }
    }
#endif
}
