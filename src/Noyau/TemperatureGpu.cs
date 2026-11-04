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
                        Marshal.StructureToPtr(new ADAPTERPERFDATA(), perf, false);
                        var q = new QUERYADAPTERINFO { hAdapter = a.hAdapter, Type = KMTQAITYPE_ADAPTERPERFDATA, pPrivateDriverData = perf, PrivateDriverDataSize = (uint)Marshal.SizeOf<ADAPTERPERFDATA>() };
                        if (D3DKMTQueryAdapterInfo(ref q) == 0)
                        {
                            var d = Marshal.PtrToStructure<ADAPTERPERFDATA>(perf);
                            double c = d.Temperature / 10.0;
                            if (c > 5 && c < 130) max = Math.Max(max, c);
                        }
                    }
                    finally { var f = new CLOSEADAPTER { hAdapter = a.hAdapter }; try { D3DKMTCloseAdapter(ref f); } catch { } }
                }
            }
            finally { Marshal.FreeHGlobal(e.pAdapters); Marshal.FreeHGlobal(perf); }
            return max;
        }
    }
}
