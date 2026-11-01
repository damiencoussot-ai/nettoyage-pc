// Nettoyage PC - surveillance du réseau (Premium) : les appareils connectés à ta box (nouvel appareil = alerte du
// gardien) et les applications connectées à Internet en ce moment. Lecture seule : rien n'est bloqué ni modifié.
// Sans interface : partagé v4 / v5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace NettoyagePC
{
    internal class AppareilReseau
    {
        public string Mac = "", Ip = "", Nom = "", Surnom = "";
        public DateTime Premier, Dernier;
        public bool Connu, Box, CePc;
        public string Libelle { get { return Surnom != "" ? Surnom : Nom != "" ? Nom : Ip; } }
    }

    internal class AppConnectee
    {
        public string Processus = "", Chemin = "";
        public int Connexions;
        public List<string> Adresses = new List<string>();
    }

    internal static class SurveillanceReseau
    {
        private static string Fichier { get { return Path.Combine(Chemins.Local, "reseau-appareils.json"); } }

        // ------------------------------------------------------------ Appareils
        /// <summary>Réseau local de la connexion active : (adresse de ce PC, masque, box). null sans connexion.</summary>
        private static Tuple<IPAddress, IPAddress, IPAddress> Local()
        {
            foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
            {
                try
                {
                    if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    var p = n.GetIPProperties();
                    var gw = p.GatewayAddresses.Select(g => g.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
                    if (gw == null) continue;
                    var u = p.UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                    if (u == null || u.IPv4Mask == null) continue;
                    return Tuple.Create(u.Address, u.IPv4Mask, gw);
                }
                catch { }
            }
            return null;
        }

        [DllImport("iphlpapi.dll")] private static extern int SendARP(uint ipDest, uint ipSrc, byte[] mac, ref int len);

        private static string Mac(IPAddress ip)
        {
            try
            {
                var mac = new byte[6]; int len = 6;
                if (SendARP(BitConverter.ToUInt32(ip.GetAddressBytes(), 0), 0, mac, ref len) != 0 || len < 6) return "";
                if (mac.All(b => b == 0)) return "";
                return string.Join(":", mac.Take(6).Select(b => b.ToString("X2")));
            }
            catch { return ""; }
        }

        /// <summary>Appareils présents sur le réseau local (balayage des adresses voisines, 254 au plus). Plusieurs secondes.</summary>
        public static List<AppareilReseau> Balayer()
        {
            var res = new List<AppareilReseau>();
            var loc = Local();
            if (loc == null) return res;
            var ip = loc.Item1.GetAddressBytes(); var masque = loc.Item2.GetAddressBytes();
            // réseaux plus grands qu'un /24 : seules les 254 adresses voisines sont balayées
            if (masque[0] != 255 || masque[1] != 255) masque = new byte[] { 255, 255, 255, 0 };
            uint m = (uint)(masque[0] << 24 | masque[1] << 16 | masque[2] << 8 | masque[3]);
            uint a = (uint)(ip[0] << 24 | ip[1] << 16 | ip[2] << 8 | ip[3]);
            uint reseau = a & m, diffusion = reseau | ~m;
            if (diffusion - reseau > 256) { reseau = a & 0xFFFFFF00; diffusion = reseau | 0xFF; }
            var adresses = new List<IPAddress>();
            for (uint x = reseau + 1; x < diffusion; x++) adresses.Add(new IPAddress(new[] { (byte)(x >> 24), (byte)(x >> 16), (byte)(x >> 8), (byte)x }));
            var trouves = new System.Collections.Concurrent.ConcurrentBag<AppareilReseau>();
            Parallel.ForEach(adresses, new ParallelOptions { MaxDegreeOfParallelism = 32 }, adr =>
            {
                string mac = "";
                if (adr.Equals(loc.Item1))
                {
                    try { mac = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.GetIPProperties().UnicastAddresses.Any(u => u.Address.Equals(adr))).Select(n => string.Join(":", n.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")))).FirstOrDefault() ?? ""; } catch { }
                }
                else mac = Mac(adr);   // SendARP attend la réponse de l'appareil (moins d'une seconde)
                if (mac == "") return;
                trouves.Add(new AppareilReseau { Mac = mac, Ip = adr.ToString(), Box = adr.Equals(loc.Item3), CePc = adr.Equals(loc.Item1) });
            });
            res = trouves.OrderBy(x => x.Box ? 0 : x.CePc ? 1 : 2).ThenBy(x => Version.Parse(x.Ip)).ToList();
            Parallel.ForEach(res, new ParallelOptions { MaxDegreeOfParallelism = 16 }, x =>
            {
                try
                {
                    var t = Dns.GetHostEntryAsync(x.Ip);
                    if (t.Wait(1500) && t.Result != null && t.Result.HostName != x.Ip) x.Nom = Regex.Replace(t.Result.HostName, @"\.(home|lan|local|box|localdomain|fritz\.box)$", "", RegexOptions.IgnoreCase);
                }
                catch { }
            });
            return res;
        }

        /// <summary>Appareils déjà vus (mémorisés sur ce PC).</summary>
        public static List<AppareilReseau> Connus()
        {
            var l = new List<AppareilReseau>();
            try
            {
                foreach (var o in Json.Objets(Json.LireFichier(Fichier)))
                {
                    DateTime p, d;
                    DateTime.TryParse(Json.Texte(o, "Premier"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out p);
                    DateTime.TryParse(Json.Texte(o, "Dernier"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d);
                    l.Add(new AppareilReseau { Mac = Json.Texte(o, "Mac"), Ip = Json.Texte(o, "Ip"), Nom = Json.Texte(o, "Nom"), Surnom = Json.Texte(o, "Surnom"), Premier = p, Dernier = d, Connu = Json.Bool(o, "Connu"), Box = Json.Bool(o, "Box"), CePc = Json.Bool(o, "CePc") });
                }
            }
            catch { }
            return l;
        }

        private static void Enregistrer(List<AppareilReseau> l)
        {
            try
            {
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(Fichier, Json.Ecrire(l.OrderByDescending(x => x.Dernier).Take(300).Select(x => (object)new Dictionary<string, object>
                {
                    { "Mac", x.Mac }, { "Ip", x.Ip }, { "Nom", x.Nom }, { "Surnom", x.Surnom }, { "Premier", x.Premier.ToString("o") }, { "Dernier", x.Dernier.ToString("o") },
                    { "Connu", x.Connu }, { "Box", x.Box }, { "CePc", x.CePc }
                }).ToList()));
            }
            catch { }
        }

        /// <summary>
        /// Balaye le réseau, fusionne avec les appareils déjà vus et retourne les nouveaux (jamais vus). Au tout premier
        /// passage, tous les appareils présents sont considérés comme connus (pas d'alerte pour ceux qui étaient déjà là).
        /// </summary>
        public static List<AppareilReseau> MettreAJour(out List<AppareilReseau> tous)
        {
            var presents = Balayer();
            var connus = Connus();
            bool premier = connus.Count == 0;
            var nouveaux = new List<AppareilReseau>();
            foreach (var p in presents)
            {
                var c = connus.FirstOrDefault(x => x.Mac.Equals(p.Mac, StringComparison.OrdinalIgnoreCase));
                if (c == null)
                {
                    p.Premier = p.Dernier = DateTime.Now; p.Connu = premier || p.Box || p.CePc;
                    connus.Add(p);
                    if (!p.Connu) nouveaux.Add(p);
                }
                else { c.Ip = p.Ip; c.Dernier = DateTime.Now; if (p.Nom != "") c.Nom = p.Nom; c.Box = p.Box; c.CePc = p.CePc; }
            }
            Enregistrer(connus);
            var macs = new HashSet<string>(presents.Select(x => x.Mac), StringComparer.OrdinalIgnoreCase);
            tous = connus.Where(x => macs.Contains(x.Mac)).OrderBy(x => x.Box ? 0 : x.CePc ? 1 : 2).ThenBy(x => x.Connu).ToList();
            return nouveaux;
        }

        /// <summary>« Je le connais » : l'appareil ne déclenche plus d'alerte (avec un surnom facultatif).</summary>
        public static void Reconnaitre(string mac, string surnom)
        {
            var l = Connus();
            var a = l.FirstOrDefault(x => x.Mac.Equals(mac, StringComparison.OrdinalIgnoreCase));
            if (a == null) return;
            a.Connu = true;
            if (surnom != null) a.Surnom = surnom.Trim().Length > 40 ? surnom.Trim().Substring(0, 40) : surnom.Trim();
            Enregistrer(l);
        }

        // ------------------------------------------------------------ Applications connectées
        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(IntPtr table, ref int taille, bool trier, int famille, int classe, uint reserve);

        private static bool Privee(IPAddress a)
        {
            var b = a.GetAddressBytes();
            return IPAddress.IsLoopback(a) || b[0] == 10 || (b[0] == 172 && b[1] >= 16 && b[1] <= 31) || (b[0] == 192 && b[1] == 168) || (b[0] == 169 && b[1] == 254) || b[0] == 0;
        }

        /// <summary>Applications qui ont une connexion ouverte vers Internet en ce moment (connexions TCP établies, IPv4).</summary>
        public static List<AppConnectee> Applications()
        {
            var parPid = new Dictionary<int, AppConnectee>();
            int taille = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref taille, true, 2, 5, 0);   // AF_INET, TCP_TABLE_OWNER_PID_ALL
            if (taille <= 0) return new List<AppConnectee>();
            IntPtr buf = Marshal.AllocHGlobal(taille);
            try
            {
                if (GetExtendedTcpTable(buf, ref taille, true, 2, 5, 0) != 0) return new List<AppConnectee>();
                int n = Marshal.ReadInt32(buf);
                for (int i = 0; i < n; i++)
                {
                    IntPtr ligne = buf + 4 + i * 24;   // état, adresse locale, port local, adresse distante, port distant, pid
                    int etat = Marshal.ReadInt32(ligne);
                    if (etat != 5) continue;           // ESTABLISHED
                    var distante = new IPAddress(BitConverter.GetBytes(Marshal.ReadInt32(ligne + 12)));
                    if (Privee(distante)) continue;
                    int pid = Marshal.ReadInt32(ligne + 20);
                    AppConnectee a;
                    if (!parPid.TryGetValue(pid, out a)) { a = new AppConnectee(); parPid[pid] = a; }
                    a.Connexions++;
                    string adr = distante.ToString();
                    if (!a.Adresses.Contains(adr) && a.Adresses.Count < 6) a.Adresses.Add(adr);
                }
            }
            finally { Marshal.FreeHGlobal(buf); }
            foreach (var kv in parPid)
            {
                try { using (var p = Process.GetProcessById(kv.Key)) { kv.Value.Processus = p.ProcessName; try { kv.Value.Chemin = p.MainModule.FileName; } catch { } } }
                catch { kv.Value.Processus = kv.Key == 4 ? "System" : "?"; }
            }
            // un même programme lancé plusieurs fois (navigateur...) : regroupé
            return parPid.Values.GroupBy(x => x.Processus, StringComparer.OrdinalIgnoreCase)
                .Select(g => new AppConnectee { Processus = g.Key, Chemin = g.Select(x => x.Chemin).FirstOrDefault(c => c != "") ?? "", Connexions = g.Sum(x => x.Connexions), Adresses = g.SelectMany(x => x.Adresses).Distinct().Take(6).ToList() })
                .OrderByDescending(x => x.Connexions).ToList();
        }
    }
}
