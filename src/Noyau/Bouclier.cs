// Nettoyage PC - bouclier anti-arnaque : repère les logiciels de prise en main à distance ouverts sur le PC.
// Les arnaques au « faux support technique » demandent presque toujours d'en installer un (AnyDesk, TeamViewer...).
// Rien n'est fermé automatiquement : l'application prévient, explique, et ne ferme un logiciel que sur demande.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;

namespace NettoyagePC
{
    internal class AccesDistant
    {
        public string Nom = "", Processus = "", Chemin = "";
        public List<int> Pids = new List<int>();
        /// <summary>Connexions Internet établies par ce logiciel (une session peut être en cours).</summary>
        public int Connexions;
        public bool Microsoft;
        public bool Service;
    }

    internal static class Bouclier
    {
        // nom du processus (sans .exe, en minuscules) -> nom affiché
        private static readonly Dictionary<string, string> Connus = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "anydesk", "AnyDesk" }, { "teamviewer", "TeamViewer" }, { "tv_w32", "TeamViewer" }, { "tv_x64", "TeamViewer" }, { "teamviewer_desktop", "TeamViewer" },
            { "ultraviewer_desktop", "UltraViewer" }, { "ultraviewer", "UltraViewer" }, { "rustdesk", "RustDesk" }, { "supremo", "Supremo" }, { "supremoservice", "Supremo" },
            { "aeroadmin", "AeroAdmin" }, { "aa_v3", "Ammyy Admin" }, { "ammyy_admin", "Ammyy Admin" }, { "logmein", "LogMeIn" }, { "lmiguardiansvc", "LogMeIn" },
            { "gotoassist", "GoTo Resolve" }, { "g2ax_customer", "GoTo Resolve" }, { "screenconnect.windowsclient", "ScreenConnect" }, { "screenconnect.clientservice", "ScreenConnect" },
            { "srservice", "Splashtop" }, { "strwinclt", "Splashtop" }, { "rutserv", "Remote Utilities" }, { "rfusclient", "Remote Utilities" }, { "zaservice", "Zoho Assist" },
            { "remoting_host", "Bureau à distance Chrome" }, { "winvnc", "VNC" }, { "tvnserver", "TightVNC" }, { "vncserver", "VNC" }, { "dwagent", "DWService" },
            { "hoptodesk", "HopToDesk" }, { "iperius", "Iperius Remote" }, { "remotepc", "RemotePC" }, { "quickassist", "Assistance rapide (Microsoft)" }
        };

        private static string FichierConfiance { get { return Path.Combine(Chemins.Local, "bouclier-confiance.txt"); } }

        /// <summary>Logiciels que l'utilisateur a déclarés de confiance (il s'en sert lui-même).</summary>
        public static HashSet<string> Confiance()
        {
            try { if (File.Exists(FichierConfiance)) return new HashSet<string>(File.ReadAllLines(FichierConfiance).Select(x => x.Trim()).Where(x => x != ""), StringComparer.OrdinalIgnoreCase); } catch { }
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        public static void DefinirConfiance(string nom, bool confiance)
        {
            var c = Confiance();
            if (confiance) c.Add(nom); else c.Remove(nom);
            try { Directory.CreateDirectory(Chemins.Local); File.WriteAllLines(FichierConfiance, c.ToArray()); } catch { }
        }

        /// <summary>Logiciels de prise en main à distance ouverts en ce moment (de confiance compris).</summary>
        public static List<AccesDistant> Detecter()
        {
            var r = new Dictionary<string, AccesDistant>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    string n = p.ProcessName;
                    string nom;
                    if (!Connus.TryGetValue(n, out nom)) continue;
                    AccesDistant a;
                    if (!r.TryGetValue(nom, out a)) { a = new AccesDistant { Nom = nom, Processus = n, Microsoft = n.Equals("quickassist", StringComparison.OrdinalIgnoreCase) }; r[nom] = a; }
                    a.Pids.Add(p.Id);
                    if (p.SessionId == 0) a.Service = true;
                    if (a.Chemin == "") try { a.Chemin = p.MainModule.FileName; } catch { }
                }
                catch { }
                finally { p.Dispose(); }
            }
            if (r.Count > 0)
            {
                var connexions = ConnexionsParProcessus();
                foreach (var a in r.Values) a.Connexions = a.Pids.Sum(pid => { int c; return connexions.TryGetValue(pid, out c) ? c : 0; });
            }
            return r.Values.OrderBy(a => a.Microsoft).ThenBy(a => a.Nom).ToList();
        }

        /// <summary>Logiciels à signaler (ouverts et pas déclarés de confiance).</summary>
        public static List<AccesDistant> ASignaler()
        {
            var c = Confiance();
            return Detecter().Where(a => !c.Contains(a.Nom)).ToList();
        }

        /// <summary>Ferme le logiciel (demande polie, puis arrêt). false : impossible (service Windows, droits).</summary>
        public static bool Fermer(AccesDistant a)
        {
            bool ok = true;
            foreach (var pid in a.Pids)
            {
                try
                {
                    using (var p = Process.GetProcessById(pid))
                    {
                        if (p.HasExited) continue;
                        try { p.CloseMainWindow(); } catch { }
                        if (!p.WaitForExit(2500)) { p.Kill(); p.WaitForExit(3000); }
                    }
                }
                catch (ArgumentException) { }   // déjà fermé
                catch { ok = false; }
            }
            return ok;
        }

        // ------------------------------------------------------------ Connexions Internet établies, par processus
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr table, ref int taille, bool trier, int famille, int classe, uint reserve);

        private static Dictionary<int, int> ConnexionsParProcessus()
        {
            var d = new Dictionary<int, int>();
            if (!Natif.EstWindows) return d;
            foreach (int famille in new[] { 2, 23 })   // IPv4, IPv6
            {
                int taille = 0;
                GetExtendedTcpTable(IntPtr.Zero, ref taille, false, famille, 5, 0);   // TCP_TABLE_OWNER_PID_ALL
                if (taille <= 0) continue;
                IntPtr t = Marshal.AllocHGlobal(taille);
                try
                {
                    if (GetExtendedTcpTable(t, ref taille, false, famille, 5, 0) != 0) continue;
                    int n = Marshal.ReadInt32(t);
                    int ligne = famille == 2 ? 24 : 56, debut = 4;
                    for (int i = 0; i < n; i++)
                    {
                        IntPtr l = IntPtr.Add(t, debut + i * ligne);
                        int etat, pid;
                        bool locale;
                        if (famille == 2)
                        {
                            etat = Marshal.ReadInt32(l, 0);
                            uint distante = (uint)Marshal.ReadInt32(l, 12);
                            pid = Marshal.ReadInt32(l, 20);
                            locale = (distante & 0xFF) == 127 || distante == 0;
                        }
                        else
                        {
                            var adr = new byte[16]; Marshal.Copy(IntPtr.Add(l, 24), adr, 0, 16);
                            etat = Marshal.ReadInt32(l, 48); pid = Marshal.ReadInt32(l, 52);
                            locale = IPAddress.IsLoopback(new IPAddress(adr)) || adr.All(b => b == 0);
                        }
                        if (etat != 5 || locale) continue;   // MIB_TCP_STATE_ESTAB
                        int c; d.TryGetValue(pid, out c); d[pid] = c + 1;
                    }
                }
                catch { }
                finally { Marshal.FreeHGlobal(t); }
            }
            return d;
        }
    }
}
