// Velox - température de la carte graphique, comme le Gestionnaire des tâches de Windows (onglet Performances) :
// interface officielle de Windows (D3DKMT, pilotes WDDM 2.4 et plus, Windows 10 1803+), sans droits administrateur
// ni logiciel en plus. Marche pour AMD, NVIDIA et Intel si le pilote la donne ; sinon -1 (non lue).
using System;
using System.Runtime.InteropServices;

namespace NettoyagePC
{
    internal static class TemperatureGpu
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct ADAPTERINFO { public uint hAdapter; public uint LuidBas; public int LuidHaut; public uint NumOfSources; public int Precis; }

        [StructLayout(LayoutKind.Sequential)]
        private struct ENUMADAPTERS2 { public uint NumAdapters; public IntPtr pAdapters; }

        [StructLayout(LayoutKind.Sequential)]
        private struct QUERYADAPTERINFO { public uint hAdapter; public int Type; public IntPtr pPrivateDriverData; public uint PrivateDriverDataSize; }

        [StructLayout(LayoutKind.Sequential)]
        private struct ADAPTERPERFDATA
        {
            public uint PhysicalAdapterIndex;
            public ulong MemoryFrequency, MaxMemoryFrequency, MaxMemoryFrequencyOC, MemoryBandwidth, PCIEBandwidth;
            public uint FanRPM, Power, Temperature;   // Temperature : dixièmes de degré
            public byte PowerStateOverride;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct CLOSEADAPTER { public uint hAdapter; }

        [DllImport("gdi32.dll")] private static extern int D3DKMTEnumAdapters2(ref ENUMADAPTERS2 e);
        [DllImport("gdi32.dll")] private static extern int D3DKMTQueryAdapterInfo(ref QUERYADAPTERINFO q);
        [DllImport("gdi32.dll")] private static extern int D3DKMTCloseAdapter(ref CLOSEADAPTER c);
        [StructLayout(LayoutKind.Sequential)]
        private struct OPENADAPTERFROMLUID { public uint LuidBas; public int LuidHaut; public uint hAdapter; }
        [DllImport("gdi32.dll")] private static extern int D3DKMTOpenAdapterFromLuid(ref OPENADAPTERFROMLUID o);
        // ancienne énumération (Windows 7+) : 16 cartes au plus, tableau fixe dans la structure
        [StructLayout(LayoutKind.Sequential)]
        private struct ENUMADAPTERS1 { public uint NumAdapters; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public ADAPTERINFO[] Adapters; }
        [DllImport("gdi32.dll")] private static extern int D3DKMTEnumAdapters(ref ENUMADAPTERS1 e);
        private static bool journalise;

        private const int KMTQAITYPE_ADAPTERPERFDATA = 62;
        private static bool indisponible;
        private static readonly object verrou = new object();
        private static DateTime derniere = DateTime.MinValue;
        private static double valeur = -1;

        /// <summary>Température la plus haute parmi les cartes graphiques (°C), ou -1. Lue au plus une fois par seconde.</summary>
        public static double Lire()
        {
            if (indisponible || !OperatingSystem.IsWindows()) return -1;
            lock (verrou)
            {
                if ((DateTime.Now - derniere).TotalMilliseconds < 1000) return valeur;
                derniere = DateTime.Now;
                try { valeur = Mesurer(); }
                catch (DllNotFoundException ex) { indisponible = true; valeur = -1; Diagnostic("gdi32 introuvable : " + ex.Message); }
                catch (EntryPointNotFoundException ex) { indisponible = true; valeur = -1; Diagnostic("fonction absente : " + ex.Message); }
                catch (Exception ex) { valeur = -1; Diagnostic(ex.GetType().Name + " : " + ex.Message); }
                return valeur;
            }
        }

        /// <summary>Note une seule fois, dans le journal de démarrage, pourquoi la température n'a pas pu être lue.</summary>
        private static void Diagnostic(string texte)
        {
            if (journalise) return;
            journalise = true;
            JournalDemarrage.Noter("Température GPU non lue : " + texte);
        }

        /// <summary>Liste des cartes : d'abord D3DKMTEnumAdapters2, sinon l'ancienne D3DKMTEnumAdapters.</summary>
        private static ADAPTERINFO[] Cartes(out string etat)
        {
            etat = "";
            try
            {
                var e = new ENUMADAPTERS2();
                int st = D3DKMTEnumAdapters2(ref e);
                if (st == 0 && e.NumAdapters > 0 && e.NumAdapters <= 32)
                {
                    int taille = Marshal.SizeOf<ADAPTERINFO>();
                    e.pAdapters = Marshal.AllocHGlobal(taille * (int)e.NumAdapters);
                    try
                    {
                        st = D3DKMTEnumAdapters2(ref e);
                        if (st == 0)
                        {
                            var l = new ADAPTERINFO[e.NumAdapters];
                            for (int i = 0; i < l.Length; i++) l[i] = Marshal.PtrToStructure<ADAPTERINFO>(e.pAdapters + i * taille);
                            return l;
                        }
                    }
                    finally { Marshal.FreeHGlobal(e.pAdapters); }
                }
                etat = string.Format("EnumAdapters2 0x{0:X8} ({1} carte(s))", st, e.NumAdapters);
            }
            catch (EntryPointNotFoundException) { etat = "EnumAdapters2 absente"; }
            var v1 = new ENUMADAPTERS1 { Adapters = new ADAPTERINFO[16] };
            int s1 = D3DKMTEnumAdapters(ref v1);
            if (s1 == 0 && v1.NumAdapters > 0 && v1.NumAdapters <= 16) { var l = new ADAPTERINFO[v1.NumAdapters]; Array.Copy(v1.Adapters, l, l.Length); return l; }
            etat += string.Format(" ; EnumAdapters 0x{0:X8}", s1);
            return null;
        }

        private static double Mesurer()
        {
            string etatListe;
            var cartes = Cartes(out etatListe);
            if (cartes == null) { Diagnostic(etatListe); return -1; }
            IntPtr perf = Marshal.AllocHGlobal(Marshal.SizeOf<ADAPTERPERFDATA>());
            double max = -1;
            try
            {
                for (int i = 0; i < cartes.Length; i++)
                {
                    var a = cartes[i];
                    try
                    {
                        // d'abord avec la poignée de l'énumération, sinon en rouvrant la carte par son identifiant (LUID),
                        // comme le Gestionnaire des tâches
                        int st1 = Interroger(a.hAdapter, perf, ref max), st2 = -1;
                        if (st1 != 0)
                        {
                            var o = new OPENADAPTERFROMLUID { LuidBas = a.LuidBas, LuidHaut = a.LuidHaut };
                            if (D3DKMTOpenAdapterFromLuid(ref o) == 0)
                            {
                                st2 = Interroger(o.hAdapter, perf, ref max);
                                var fo = new CLOSEADAPTER { hAdapter = o.hAdapter }; try { D3DKMTCloseAdapter(ref fo); } catch { }
                            }
                        }
                        if (!journalise) JournalDemarrage.Noter(string.Format("Température GPU : carte {0}/{1}, état 0x{2:X8} puis 0x{3:X8}", i + 1, cartes.Length, st1, st2));
                    }
                    finally { var f = new CLOSEADAPTER { hAdapter = a.hAdapter }; try { D3DKMTCloseAdapter(ref f); } catch { } }
                }
            }
            finally { Marshal.FreeHGlobal(perf); }
            if (!journalise) { journalise = true; JournalDemarrage.Noter("Température GPU lue : " + (max > 0 ? max + " °C" : "non disponible")); }
            return max;
        }

        private static int Interroger(uint carte, IntPtr perf, ref double max)
        {
            Marshal.StructureToPtr(new ADAPTERPERFDATA(), perf, false);
            var q = new QUERYADAPTERINFO { hAdapter = carte, Type = KMTQAITYPE_ADAPTERPERFDATA, pPrivateDriverData = perf, PrivateDriverDataSize = (uint)Marshal.SizeOf<ADAPTERPERFDATA>() };
            int st = D3DKMTQueryAdapterInfo(ref q);
            if (st == 0)
            {
                var d = Marshal.PtrToStructure<ADAPTERPERFDATA>(perf);
                double c = d.Temperature / 10.0;
                if (c > 5 && c < 130) max = Math.Max(max, c);
                else if (!journalise) JournalDemarrage.Noter("Température GPU : la carte répond " + d.Temperature + " (dixièmes de degré)");
            }
            return st;
        }
    }
}
