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
                catch (DllNotFoundException) { indisponible = true; valeur = -1; }
                catch (EntryPointNotFoundException) { indisponible = true; valeur = -1; }
                catch { valeur = -1; }
                return valeur;
            }
        }

        private static double Mesurer()
        {
            var e = new ENUMADAPTERS2();
            if (D3DKMTEnumAdapters2(ref e) != 0 || e.NumAdapters == 0 || e.NumAdapters > 32) return -1;
            int taille = Marshal.SizeOf<ADAPTERINFO>();
            e.pAdapters = Marshal.AllocHGlobal(taille * (int)e.NumAdapters);
            IntPtr perf = Marshal.AllocHGlobal(Marshal.SizeOf<ADAPTERPERFDATA>());
            double max = -1;
            try
            {
                if (D3DKMTEnumAdapters2(ref e) != 0) return -1;
                for (int i = 0; i < e.NumAdapters; i++)
                {
                    var a = Marshal.PtrToStructure<ADAPTERINFO>(e.pAdapters + i * taille);
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
                        if (!journalise) JournalDemarrage.Noter(string.Format("Température GPU : carte {0}/{1}, état 0x{2:X8} puis 0x{3:X8}", i + 1, e.NumAdapters, st1, st2));
                    }
                    finally { var f = new CLOSEADAPTER { hAdapter = a.hAdapter }; try { D3DKMTCloseAdapter(ref f); } catch { } }
                }
            }
            finally { Marshal.FreeHGlobal(e.pAdapters); Marshal.FreeHGlobal(perf); }
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
