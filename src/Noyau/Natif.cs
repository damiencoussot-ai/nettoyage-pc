// Nettoyage PC - appels directs à Windows (P/Invoke)
// Chaque appel est protégé : sur un Windows qui ne le connaît pas, la fonction concernée renvoie simplement « indisponible ».
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace NettoyagePC
{
    internal static class Natif
    {
        public static bool EstWindows { get { return Environment.OSVersion.Platform == PlatformID.Win32NT; } }
        public static int Build { get { return Environment.OSVersion.Version.Build; } }

        // ---------------- Processeur et mémoire
        [DllImport("kernel32.dll")] private static extern bool GetSystemTimes(out long inactif, out long noyau, out long utilisateur);

        [StructLayout(LayoutKind.Sequential)]
        private class EtatMemoire
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(EtatMemoire));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GlobalMemoryStatusEx([In, Out] EtatMemoire etat);

        private static long precInactif, precNoyau, precUtilisateur;
        private static bool cpuInitialise;

        /// <summary>Charge du processeur en % depuis l'appel précédent (-1 si indisponible).</summary>
        public static double Processeur()
        {
            try
            {
                long i, k, u;
                if (!GetSystemTimes(out i, out k, out u)) return -1;
                if (!cpuInitialise) { precInactif = i; precNoyau = k; precUtilisateur = u; cpuInitialise = true; return 0; }
                long di = i - precInactif, dk = k - precNoyau, du = u - precUtilisateur;
                precInactif = i; precNoyau = k; precUtilisateur = u;
                long total = dk + du;
                if (total <= 0) return 0;
                return Math.Max(0, Math.Min(100, (total - di) * 100.0 / total));
            }
            catch { return -1; }
        }

        /// <summary>Mémoire totale et disponible, en octets (0, 0 si indisponible).</summary>
        public static void Memoire(out ulong total, out ulong disponible)
        {
            total = 0; disponible = 0;
            try
            {
                var m = new EtatMemoire();
                if (GlobalMemoryStatusEx(m)) { total = m.ullTotalPhys; disponible = m.ullAvailPhys; }
            }
            catch { }
        }

        // ---------------- Libération de la mémoire (administrateur)
        [DllImport("psapi.dll")] public static extern bool EmptyWorkingSet(IntPtr processus);
        [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int classe, ref int info, int longueur);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr processus, uint acces, out IntPtr jeton);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern bool LookupPrivilegeValue(string systeme, string nom, out long luid);
        [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct PrivilegesJeton { public int Nombre; public long Luid; public int Attributs; }
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(IntPtr jeton, bool toutDesactiver, ref PrivilegesJeton etat, int longueur, IntPtr precedent, IntPtr longueurRetour);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] public static extern bool CloseHandle(IntPtr h);

        public static bool ActiverPrivilege(string nom)
        {
            try
            {
                IntPtr jeton;
                if (!OpenProcessToken(GetCurrentProcess(), 0x28, out jeton)) return false;
                var tp = new PrivilegesJeton { Nombre = 1, Attributs = 2 };
                bool ok = LookupPrivilegeValue(null, nom, out tp.Luid);
                if (ok) { AdjustTokenPrivileges(jeton, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero); ok = Marshal.GetLastWin32Error() == 0; }
                CloseHandle(jeton);
                return ok;
            }
            catch { return false; }
        }

        /// <summary>2 = réduire les working sets, 3 = écrire les pages modifiées, 4 = purger la liste standby. 0 = réussi.</summary>
        public static int CommandeMemoire(int commande)
        {
            try
            {
                if (!ActiverPrivilege("SeProfileSingleProcessPrivilege")) return -1;
                return NtSetSystemInformation(80, ref commande, 4);
            }
            catch { return -1; }
        }

        // ---------------- Corbeille
        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct InfoCorbeille { public int cbSize; public long i64Size; public long i64NumItems; }
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHQueryRecycleBin(string racine, ref InfoCorbeille info);
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHEmptyRecycleBin(IntPtr fenetre, string racine, uint options);

        public static long TailleCorbeille(out long elements)
        {
            elements = 0;
            try
            {
                var i = new InfoCorbeille { cbSize = Marshal.SizeOf(typeof(InfoCorbeille)) };
                if (SHQueryRecycleBin(null, ref i) == 0) { elements = i.i64NumItems; return i.i64Size; }
            }
            catch { }
            return 0;
        }

        /// <summary>Vide la corbeille de tous les lecteurs, sans confirmation, sans son, sans fenêtre.</summary>
        public static bool ViderCorbeille()
        {
            try { int r = SHEmptyRecycleBin(IntPtr.Zero, null, 0x1 | 0x2 | 0x4); return r == 0 || r == unchecked((int)0x8000FFFF); }
            catch { return false; }
        }

        // ---------------- Dossiers connus
        [DllImport("shell32.dll")] private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint options, IntPtr jeton, out IntPtr chemin);
        public static readonly Guid DossierTelechargements = new Guid("374DE290-123F-4565-9164-39C4925E467B");

        public static string DossierConnu(Guid id)
        {
            try
            {
                IntPtr p;
                if (SHGetKnownFolderPath(id, 0, IntPtr.Zero, out p) == 0)
                {
                    string s = Marshal.PtrToStringUni(p);
                    Marshal.FreeCoTaskMem(p);
                    return s;
                }
            }
            catch { }
            return null;
        }

        // ---------------- Fichiers : liens, chemin réel
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateFile(string nom, uint acces, uint partage, IntPtr securite, uint creation, uint attributs, IntPtr modele);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle h, StringBuilder tampon, uint taille, uint options);
        [StructLayout(LayoutKind.Sequential)]
        private struct InfoFichierHandle
        {
            public uint Attributs; public long Creation, Acces, Ecriture; public uint NumeroVolume, TailleHaut, TailleBas, NombreLiens, IndexHaut, IndexBas;
        }
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetFileInformationByHandle(SafeFileHandle h, out InfoFichierHandle info);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DonneesRecherche
        {
            public uint Attributs; public long Creation, Acces, Ecriture; public uint TailleHaut, TailleBas, Reserve0, Reserve1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string Nom;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string NomCourt;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr FindFirstFile(string nom, out DonneesRecherche d);
        [DllImport("kernel32.dll")] private static extern bool FindClose(IntPtr h);

        public const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;
        public const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;

        /// <summary>Vrai si le chemin est une jonction ou un lien symbolique (les dossiers OneDrive « à la demande » ne le sont pas).</summary>
        public static bool EstLien(string chemin)
        {
            try
            {
                var attr = File.GetAttributes(chemin);
                if ((attr & FileAttributes.ReparsePoint) == 0) return false;
                if (!EstWindows) return true;
                DonneesRecherche d;
                IntPtr h = FindFirstFile(chemin.TrimEnd('\\'), out d);
                if (h == new IntPtr(-1)) return true;
                FindClose(h);
                return d.Reserve0 == IO_REPARSE_TAG_MOUNT_POINT || d.Reserve0 == IO_REPARSE_TAG_SYMLINK;
            }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch { return true; }
        }

        /// <summary>Nombre de liens physiques d'un fichier (1 = normal), 0 si inconnu.</summary>
        public static int NombreLiens(string fichier)
        {
            if (!EstWindows) return 1;
            try
            {
                using (var h = CreateFile(fichier, 0x80, 7, IntPtr.Zero, 3, 0x02000000 | 0x00200000, IntPtr.Zero))
                {
                    if (h.IsInvalid) return 0;
                    InfoFichierHandle i;
                    if (!GetFileInformationByHandle(h, out i)) return 0;
                    return (int)i.NombreLiens;
                }
            }
            catch { return 0; }
        }

        /// <summary>Chemin réel d'un fichier ouvert (après liens et jonctions), ou null.</summary>
        public static string CheminReel(SafeFileHandle h)
        {
            try
            {
                var sb = new StringBuilder(1024);
                uint n = GetFinalPathNameByHandle(h, sb, 1024, 0);
                if (n == 0 || n >= 1024) return null;
                string s = sb.ToString();
                if (s.StartsWith(@"\\?\UNC\")) return @"\\" + s.Substring(8);
                if (s.StartsWith(@"\\?\")) return s.Substring(4);
                return s;
            }
            catch { return null; }
        }

        // ---------------- Tubes nommés (sortie du moteur lancé avec l'autorisation administrateur)
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern SafeFileHandle CreateNamedPipe(string nom, uint ouverture, uint mode, uint instances, uint tailleSortie, uint tailleEntree, uint delai, IntPtr securite);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool ConnectNamedPipe(SafeFileHandle tube, IntPtr chevauchement);

        // ---------------- Fenêtres : thème sombre, coins, premier plan
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr h, int attribut, ref int valeur, int taille);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] private static extern int SetWindowTheme(IntPtr h, string appli, string ids);
        [DllImport("uxtheme.dll", EntryPoint = "#135")] private static extern int SetPreferredAppMode(int mode);
        [DllImport("uxtheme.dll", EntryPoint = "#133")] [return: MarshalAs(UnmanagedType.U1)] private static extern bool AllowDarkModeForWindow(IntPtr h, [MarshalAs(UnmanagedType.U1)] bool autoriser);
        [DllImport("uxtheme.dll", EntryPoint = "#136")] private static extern void FlushMenuThemes();
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
        [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
        [DllImport("user32.dll")] public static extern bool AllowSetForegroundWindow(int pid);

        /// <summary>Désactive les fonctions de thème avancées (mode sans échec).</summary>
        public static bool SansEchec;
        private static bool SombrePossible { get { return EstWindows && !SansEchec && Build >= 18362; } }

        public static void BarreTitreSombre(IntPtr h, bool sombre)
        {
            if (!EstWindows || h == IntPtr.Zero) return;
            try
            {
                int v = sombre ? 1 : 0;
                if (DwmSetWindowAttribute(h, 20, ref v, 4) != 0) DwmSetWindowAttribute(h, 19, ref v, 4);
                if (Build >= 22000 && !SansEchec) { int coins = 2; DwmSetWindowAttribute(h, 33, ref coins, 4); }
            }
            catch { }
        }

        public static void ModeApplication(bool sombre)
        {
            if (!SombrePossible) return;
            try { SetPreferredAppMode(sombre ? 2 : 3); FlushMenuThemes(); } catch { }
        }

        public static void ThemeControle(IntPtr h, bool sombre, string sombreNom, string clairNom)
        {
            if (!EstWindows || h == IntPtr.Zero) return;
            try { if (SombrePossible) AllowDarkModeForWindow(h, sombre); } catch { }
            try { SetWindowTheme(h, sombre ? sombreNom : clairNom, null); } catch { }
        }

        public static void EnTeteListe(IntPtr liste, bool sombre)
        {
            if (!EstWindows) return;
            try
            {
                IntPtr entete = SendMessage(liste, 0x101F, IntPtr.Zero, IntPtr.Zero);   // LVM_GETHEADER
                if (entete != IntPtr.Zero) ThemeControle(entete, sombre, "DarkMode_ItemsView", "ItemsView");
            }
            catch { }
        }

        // ---------------- Température des cartes graphiques (comme le Gestionnaire des tâches)
        [StructLayout(LayoutKind.Sequential)] private struct LUID { public uint Low; public int High; }
        [StructLayout(LayoutKind.Sequential)] private struct ADAPTERINFO { public uint hAdapter; public LUID Luid; public uint NumOfSources; public int bPrecise; }
        [StructLayout(LayoutKind.Sequential)] private struct ENUMADAPTERS2 { public uint NumAdapters; public IntPtr pAdapters; }
        [StructLayout(LayoutKind.Sequential)] private struct QUERYADAPTERINFO { public uint hAdapter; public int Type; public IntPtr pData; public uint Size; }
        [StructLayout(LayoutKind.Sequential)]
        private struct ADAPTERPERFDATA
        {
            public uint PhysicalAdapterIndex; public ulong MemoryFrequency; public ulong MaxMemoryFrequency; public ulong MaxMemoryFrequencyOC;
            public ulong MemoryBandwidth; public ulong PCIEBandwidth; public uint FanRPM; public uint Power; public uint Temperature; public byte PowerStateOverride;
        }
        [StructLayout(LayoutKind.Sequential)] private struct CLOSEADAPTER { public uint hAdapter; }
        [DllImport("gdi32.dll")] private static extern int D3DKMTEnumAdapters2(ref ENUMADAPTERS2 e);
        [DllImport("gdi32.dll")] private static extern int D3DKMTQueryAdapterInfo(ref QUERYADAPTERINFO q);
        [DllImport("gdi32.dll")] private static extern int D3DKMTCloseAdapter(ref CLOSEADAPTER c);

        /// <summary>Température la plus haute des cartes graphiques, en °C (-1 si indisponible).</summary>
        public static double TemperatureCarteGraphique()
        {
            if (!EstWindows || SansEchec || Build < 19041) return -1;
            double max = -1;
            try
            {
                var e = new ENUMADAPTERS2();
                if (D3DKMTEnumAdapters2(ref e) != 0 || e.NumAdapters == 0) return -1;
                int taille = Marshal.SizeOf(typeof(ADAPTERINFO));
                e.pAdapters = Marshal.AllocHGlobal(taille * (int)e.NumAdapters);
                try
                {
                    if (D3DKMTEnumAdapters2(ref e) != 0) return -1;
                    int td = Marshal.SizeOf(typeof(ADAPTERPERFDATA));
                    for (int i = 0; i < e.NumAdapters; i++)
                    {
                        var a = (ADAPTERINFO)Marshal.PtrToStructure(new IntPtr(e.pAdapters.ToInt64() + (long)i * taille), typeof(ADAPTERINFO));
                        IntPtr p = Marshal.AllocHGlobal(td);
                        try
                        {
                            Marshal.StructureToPtr(new ADAPTERPERFDATA(), p, false);
                            var q = new QUERYADAPTERINFO { hAdapter = a.hAdapter, Type = 62, pData = p, Size = (uint)td };
                            if (D3DKMTQueryAdapterInfo(ref q) == 0)
                            {
                                var d = (ADAPTERPERFDATA)Marshal.PtrToStructure(p, typeof(ADAPTERPERFDATA));
                                double t = d.Temperature / 10.0;
                                if (t > 5 && t < 130 && t > max) max = t;
                            }
                        }
                        finally { Marshal.FreeHGlobal(p); }
                        var c = new CLOSEADAPTER { hAdapter = a.hAdapter };
                        D3DKMTCloseAdapter(ref c);
                    }
                }
                finally { Marshal.FreeHGlobal(e.pAdapters); }
            }
            catch { return -1; }
            return max;
        }

        // ---------------- DNS
        [DllImport("dnsapi.dll", EntryPoint = "DnsFlushResolverCache")] private static extern uint DnsFlushResolverCache();
        public static bool ViderCacheDns() { try { return DnsFlushResolverCache() != 0; } catch { return false; } }
    }

    // ---------------- Progression dans l'icône de la barre des tâches
    [ComImport, Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITaskbarList3
    {
        void HrInit(); void AddTab(IntPtr h); void DeleteTab(IntPtr h); void ActivateTab(IntPtr h); void SetActiveAlt(IntPtr h);
        void MarkFullscreenWindow(IntPtr h, [MarshalAs(UnmanagedType.Bool)] bool plein);
        void SetProgressValue(IntPtr h, ulong fait, ulong total);
        void SetProgressState(IntPtr h, int etat);
    }
    [ComImport, Guid("56fdf344-fd6d-11d0-958a-006097c9a090"), ClassInterface(ClassInterfaceType.None)]
    internal class TaskbarListCom { }

    internal static class BarreTaches
    {
        private static ITaskbarList3 liste;
        private static bool echec;

        private static ITaskbarList3 Liste()
        {
            if (liste == null && !echec)
            {
                try { liste = (ITaskbarList3)new TaskbarListCom(); liste.HrInit(); }
                catch { echec = true; liste = null; }
            }
            return liste;
        }

        /// <summary>pct de 0 à 100 ; -1 retire la progression.</summary>
        public static void Progression(IntPtr h, double pct)
        {
            if (!Natif.EstWindows || Natif.SansEchec || h == IntPtr.Zero) return;
            try
            {
                var l = Liste(); if (l == null) return;
                if (pct < 0) { l.SetProgressState(h, 0); return; }
                l.SetProgressState(h, 2);
                l.SetProgressValue(h, (ulong)Math.Max(0, Math.Min(100, pct)), 100);
            }
            catch { }
        }

        public static void Indetermine(IntPtr h)
        {
            if (!Natif.EstWindows || Natif.SansEchec || h == IntPtr.Zero) return;
            try { var l = Liste(); if (l != null) l.SetProgressState(h, 1); } catch { }
        }
    }
}
