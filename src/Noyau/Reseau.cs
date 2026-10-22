// Nettoyage PC - réseau : ping, débit, serveurs DNS (comparaison et changement réversible)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading;

namespace NettoyagePC
{
    internal class ResultatPing { public string Cible = "", Nom = ""; public double Moyenne = -1, Min, Max, Gigue; public int Perte; }

    internal class CarteReseau
    {
        public string Id = "", Nom = "", Description = "";
        public int Index;
        public bool Wifi;
        public List<string> Dns = new List<string>();
        public bool DnsAutomatique = true;
        public string Passerelle = "";
    }

    internal class ServeurDns
    {
        public string Nom = "", Ip1 = "", Ip2 = "", Description = "";
        public double Temps = -1;   // ms (médiane)
        public bool Actuel;
    }

    internal static class Reseau
    {
        private static readonly Regex Ipv4 = new Regex(@"^(25[0-5]|2[0-4]\d|1?\d?\d)(\.(25[0-5]|2[0-4]\d|1?\d?\d)){3}$");

        public static List<CarteReseau> CartesActives()
        {
            var l = new List<CarteReseau>();
            try
            {
                foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (n.OperationalStatus != OperationalStatus.Up || n.NetworkInterfaceType == NetworkInterfaceType.Loopback || n.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;
                    var p = n.GetIPProperties();
                    var gw = p.GatewayAddresses.Select(g => g.Address).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));
                    if (gw == null) continue;
                    var c = new CarteReseau { Id = n.Id, Nom = n.Name, Description = n.Description, Wifi = n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211, Passerelle = gw.ToString() };
                    try { c.Index = p.GetIPv4Properties().Index; } catch { }
                    c.Dns = p.DnsAddresses.Where(a => a.AddressFamily == AddressFamily.InterNetwork).Select(a => a.ToString()).ToList();
                    string statique = Registre.Lire(@"HKLM\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\" + n.Id, "NameServer") as string;
                    c.DnsAutomatique = string.IsNullOrWhiteSpace(statique);
                    l.Add(c);
                }
            }
            catch { }
            return l;
        }

        public static ResultatPing Ping(string cible, string nom, int nombre = 10)
        {
            var r = new ResultatPing { Cible = cible, Nom = nom };
            var temps = new List<long>();
            int perdus = 0;
            try
            {
                using (var p = new Ping())
                    for (int i = 0; i < nombre; i++)
                    {
                        try { var rep = p.Send(cible, 1500); if (rep.Status == IPStatus.Success) temps.Add(rep.RoundtripTime); else perdus++; }
                        catch { perdus++; }
                        Thread.Sleep(80);
                    }
            }
            catch { perdus = nombre; }
            r.Perte = (int)Math.Round(100.0 * perdus / nombre);
            if (temps.Count > 0)
            {
                r.Moyenne = temps.Average(); r.Min = temps.Min(); r.Max = temps.Max();
                double g = 0; for (int i = 1; i < temps.Count; i++) g += Math.Abs(temps[i] - temps[i - 1]);
                r.Gigue = temps.Count > 1 ? g / (temps.Count - 1) : 0;
            }
            return r;
        }

        /// <summary>Débit descendant en Mbit/s (téléchargement d'un fichier de test Cloudflare, 8 s au plus).</summary>
        public static double DebitDescendant(Action<double> progression = null, Func<bool> arret = null)
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            var req = (HttpWebRequest)WebRequest.Create("https://speed.cloudflare.com/__down?bytes=200000000");
            req.UserAgent = "NettoyagePC/" + Appli.Version; req.Timeout = 15000; req.ReadWriteTimeout = 15000;
            var chrono = Stopwatch.StartNew();
            long total = 0;
            using (var rep = req.GetResponse())
            using (var s = rep.GetResponseStream())
            {
                var tampon = new byte[256 * 1024];
                var debut = Stopwatch.StartNew();
                int n;
                while ((n = s.Read(tampon, 0, tampon.Length)) > 0)
                {
                    total += n;
                    if (progression != null && debut.ElapsedMilliseconds > 0) progression(total * 8.0 / 1e6 / (debut.ElapsedMilliseconds / 1000.0));
                    if (debut.ElapsedMilliseconds > 8000 || (arret != null && arret())) break;
                }
                chrono.Stop();
                return chrono.ElapsedMilliseconds > 0 ? total * 8.0 / 1e6 / (debut.ElapsedMilliseconds / 1000.0) : 0;
            }
        }

        /// <summary>Débit montant en Mbit/s (envoi de données de test à Cloudflare).</summary>
        public static double DebitMontant()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            var donnees = new byte[8 * 1024 * 1024];
            new Random().NextBytes(donnees);
            var req = (HttpWebRequest)WebRequest.Create("https://speed.cloudflare.com/__up");
            req.Method = "POST"; req.UserAgent = "NettoyagePC/" + Appli.Version; req.ContentType = "application/octet-stream";
            req.Timeout = 30000; req.ReadWriteTimeout = 30000; req.ContentLength = donnees.Length; req.AllowWriteStreamBuffering = false;
            var chrono = Stopwatch.StartNew();
            using (var s = req.GetRequestStream()) { for (int i = 0; i < donnees.Length; i += 65536) s.Write(donnees, i, Math.Min(65536, donnees.Length - i)); }
            using (var rep = req.GetResponse()) { }
            chrono.Stop();
            return chrono.ElapsedMilliseconds > 0 ? donnees.Length * 8.0 / 1e6 / (chrono.ElapsedMilliseconds / 1000.0) : 0;
        }

        public static List<ServeurDns> ServeursConnus()
        {
            return new List<ServeurDns>
            {
                new ServeurDns { Nom = "Cloudflare", Ip1 = "1.1.1.1", Ip2 = "1.0.0.1", Description = "Très rapide, respecte la vie privée" },
                new ServeurDns { Nom = "Google", Ip1 = "8.8.8.8", Ip2 = "8.8.4.4", Description = "Rapide et très fiable" },
                new ServeurDns { Nom = "Quad9", Ip1 = "9.9.9.9", Ip2 = "149.112.112.112", Description = "Bloque les sites malveillants connus" }
            };
        }

        /// <summary>Temps de réponse médian (ms) d'un serveur DNS, mesuré par des requêtes directes.</summary>
        public static double TempsDns(string serveur)
        {
            var domaines = new[] { "google.com", "youtube.com", "wikipedia.org", "microsoft.com", "amazon.fr", "facebook.com", "netflix.com", "lemonde.fr" };
            var t = new List<double>();
            foreach (var d in domaines) { double x = RequeteDns(serveur, d); if (x >= 0) t.Add(x); }
            if (t.Count == 0) return -1;
            t.Sort();
            return t[t.Count / 2];
        }

        private static double RequeteDns(string serveur, string domaine)
        {
            try
            {
                var paquet = new List<byte>();
                ushort id = (ushort)new Random().Next(1, 65535);
                paquet.AddRange(new byte[] { (byte)(id >> 8), (byte)id, 0x01, 0x00, 0, 1, 0, 0, 0, 0, 0, 0 });
                foreach (var p in domaine.Split('.')) { paquet.Add((byte)p.Length); paquet.AddRange(System.Text.Encoding.ASCII.GetBytes(p)); }
                paquet.AddRange(new byte[] { 0, 0, 1, 0, 1 });
                using (var u = new UdpClient())
                {
                    u.Client.ReceiveTimeout = 2000;
                    var ep = new IPEndPoint(IPAddress.Parse(serveur), 53);
                    var chrono = Stopwatch.StartNew();
                    u.Send(paquet.ToArray(), paquet.Count, ep);
                    var de = new IPEndPoint(IPAddress.Any, 0);
                    var rep = u.Receive(ref de);
                    chrono.Stop();
                    if (rep.Length < 12 || rep[0] != (byte)(id >> 8) || rep[1] != (byte)id) return -1;
                    return chrono.Elapsed.TotalMilliseconds;
                }
            }
            catch { return -1; }
        }

        // ------------------------------------------------------------ Changement des serveurs DNS (réversible)
        /// <summary>(Avec l'autorisation administrateur) arguments : dns index ip1,ip2 | dns index auto</summary>
        public static int ChangerDnsAdmin(string[] args)
        {
            int index;
            if (args.Length < 3 || !int.TryParse(args[1], out index) || index <= 0) return 3;
            string[] ips;
            if (args[2] == "auto") ips = new string[0];
            else
            {
                ips = args[2].Split(',').Select(x => x.Trim()).Where(x => x != "").ToArray();
                if (ips.Length == 0 || ips.Length > 4 || ips.Any(x => !Ipv4.IsMatch(x))) return 3;
            }
            using (var s = new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE InterfaceIndex = " + index))
                foreach (ManagementObject o in s.Get())
                {
                    var r = o.InvokeMethod("SetDNSServerSearchOrder", new object[] { ips });
                    uint code = r == null ? 1 : Convert.ToUInt32(r);
                    Natif.ViderCacheDns();
                    return code == 0 || code == 1 ? 0 : 5;
                }
            return 4;
        }

        /// <summary>Applique un serveur DNS à toutes les cartes actives (avec historique pour l'annulation).</summary>
        public static string Appliquer(ServeurDns s)
        {
            var cartes = CartesActives().Where(c => c.Index > 0).ToList();
            if (cartes.Count == 0) return "aucune connexion réseau active trouvée";
            var erreurs = new List<string>();
            foreach (var c in cartes)
            {
                if (!Elevation.Commande(60, "dns", c.Index.ToString(), s.Ip1 + "," + s.Ip2)) { erreurs.Add(c.Nom); continue; }
                Historique.Ajouter("dns", "Serveurs DNS de « " + c.Nom + " » : " + s.Nom + " (" + s.Ip1 + ")", new Dictionary<string, object>
                {
                    { "Index", c.Index }, { "Carte", c.Id }, { "Nom", c.Nom }, { "Automatique", c.DnsAutomatique }, { "Avant", string.Join(",", c.Dns) }, { "Apres", s.Ip1 + "," + s.Ip2 }
                });
            }
            return erreurs.Count == 0 ? "" : "non modifié pour : " + string.Join(", ", erreurs) + " (autorisation refusée ?)";
        }

        public static string AnnulerDns(Modif e)
        {
            int index = (int)Json.Long(e.Donnees, "Index");
            string avant = e.D("Avant");
            bool auto = Json.Bool(e.Donnees, "Automatique", true);
            if (index <= 0) return "entrée de l'historique invalide (refusée par sécurité)";
            // la carte doit toujours être la même (son identifiant ne change pas)
            var carte = CartesActives().FirstOrDefault(c => c.Index == index);
            if (carte != null && carte.Id != e.D("Carte")) return "la connexion réseau a changé depuis : rien n'a été modifié";
            string valeur = auto || string.IsNullOrWhiteSpace(avant) ? "auto" : avant;
            if (valeur != "auto" && valeur.Split(',').Any(x => !Ipv4.IsMatch(x.Trim()))) return "entrée de l'historique invalide (refusée par sécurité)";
            return Elevation.Commande(60, "dns", index.ToString(), valeur) ? "" : "autorisation de Windows refusée";
        }
    }
}
