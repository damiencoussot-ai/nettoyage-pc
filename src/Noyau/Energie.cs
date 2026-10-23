// Nettoyage PC - coût en électricité (Premium) : ESTIMATION de la consommation du PC et de chaque application,
// à partir de l'activité du processeur et de la carte graphique (relevée chaque minute par le gardien) et de la
// puissance typique de ce matériel. Les écrans ne sont pas comptés. Tout reste sur le PC.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class JourEnergie
    {
        public DateTime Jour;
        public double Wh, WhInactif;
        public int Minutes, MinutesInactif;
        public Dictionary<string, double> Apps = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
    }

    internal class ModeleEnergie
    {
        public string Processeur = "", Carte = "";
        public int Tdp, Tbp, Base;
        public bool Portable, CarteIntegree;
        public double Rendement = 0.88;
    }

    internal static class Energie
    {
        public static string Kwh(double wh) { return (wh / 1000).ToString(wh < 10000 ? "0.00" : "0.0", Appli.Fr) + " kWh"; }
        public static string Euros(double wh) { double e = wh / 1000 * Prix; return e.ToString(e < 10 ? "0.00" : "0", Appli.Fr) + " €"; }

        /// <summary>État de la mesure (faite par le gardien). actif : la mesure tourne.</summary>
        public static string EtatMesure(out bool actif)
        {
            var reg = ReglagesGardien.Lire();
            actif = Gardien.EnCours && reg.Energie;
            return actif ? "● Mesure en cours (le gardien relève l'activité chaque minute)" : !reg.Actif || !Gardien.EnCours ? "Le gardien est arrêté : active-le pour mesurer la consommation." : "La mesure de consommation est désactivée dans les réglages du gardien.";
        }

        /// <summary>Consommation par application (Wh), de la plus gourmande à la moins gourmande.</summary>
        public static List<KeyValuePair<string, double>> ParApplication(IEnumerable<JourEnergie> jours)
        {
            var parApp = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var j in jours) foreach (var a in j.Apps) { double v; parApp.TryGetValue(a.Key, out v); parApp[a.Key] = v + a.Value; }
            return parApp.OrderByDescending(a => a.Value).ToList();
        }

        /// <summary>Conseils pour payer moins, d'après les 30 derniers jours (déjà traduits, précédés de « • »).</summary>
        public static List<string> Conseils(List<JourEnergie> mois)
        {
            var conseils = new List<string>();
            double whMois = mois.Sum(j => j.Wh);
            int nbJours = mois.Count(j => j.Minutes >= 30);
            int minInactif = mois.Sum(j => j.MinutesInactif); double whInactif = mois.Sum(j => j.WhInactif);
            if (nbJours >= 3 && minInactif / (double)Math.Max(1, nbJours) >= 60)
                conseils.Add(Langue.F("•  Ton PC reste allumé {0} h par jour en moyenne sans que personne ne s'en serve. Une mise en veille automatique après 15 à 30 minutes économiserait environ {1} par an.",
                    (minInactif / 60.0 / nbJours).ToString("0.#", Appli.Fr), Euros(whInactif / nbJours * 365 * 0.9)));
            var jeu = ParApplication(mois).Where(a => a.Key != NomSysteme).FirstOrDefault();
            if (jeu.Key != null && whMois > 0 && jeu.Value / whMois >= 0.25)
                conseils.Add(Langue.F("•  « {0} » représente {1} % de la consommation. Si c'est un jeu : limiter les images par seconde à la fréquence de ton écran (et activer FreeSync / G-Sync) réduit nettement la consommation de la carte graphique, sans différence visible.",
                    jeu.Key, (jeu.Value / whMois * 100).ToString("0")));
            conseils.Add(Langue.T("•  Éteins (ou mets en veille) le PC la nuit : un PC de bureau allumé pour rien consomme autant qu'une ampoule halogène."));
            conseils.Add(Langue.T("•  Le mode d'alimentation « Équilibré » consomme moins que « Performances élevées » sans ralentir l'usage courant (Accélérer > Optimisation)."));
            return conseils;
        }

        /// <summary>Base de calcul affichée sous les chiffres.</summary>
        public static string TexteModele(ModeleEnergie m)
        {
            return Langue.F("Base de calcul : processeur {0} ({1} W), carte graphique {2}, reste du PC {3} W, pertes de l'alimentation comprises. Écrans non comptés.",
                m.Processeur == "" ? "?" : m.Processeur, m.Tdp, m.Tbp > 0 ? m.Carte + " (" + m.Tbp + " W)" : Langue.T("intégrée"), m.Base);
        }

        public static string Fichier { get { return Path.Combine(Chemins.Local, "energie.json"); } }
        private static string FichierPrix { get { return Path.Combine(Chemins.Local, "energie-prix.txt"); } }
        public const string NomSysteme = "(Windows et PC allumé)";

        public static double Prix
        {
            get { try { double p; if (File.Exists(FichierPrix) && double.TryParse(File.ReadAllText(FichierPrix).Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out p) && p > 0 && p < 5) return p; } catch { } return 0.20; }
            set { try { Directory.CreateDirectory(Chemins.Local); File.WriteAllText(FichierPrix, value.ToString("0.####", CultureInfo.InvariantCulture)); } catch { } }
        }

        // ------------------------------------------------------------ Puissance typique du matériel
        private static readonly Tuple<string, int>[] Processeurs =
        {
            Tuple.Create(@"Ryzen 9 (9950X3D|9950X|7950X3D|7950X|9900X3D)", 170), Tuple.Create(@"Ryzen 9 (9900X|7900X3D|7900X)", 120), Tuple.Create(@"Ryzen 9 (7900|5950X|5900X)", 105),
            Tuple.Create(@"Ryzen 7 (9800X3D|7800X3D|5800X3D)", 120), Tuple.Create(@"Ryzen 7 (9700X|7700X|5800X|5700X3D)", 105), Tuple.Create(@"Ryzen 7 (7700|5700X|5700G)", 65),
            Tuple.Create(@"Ryzen 5 (9600X|7600X|5600X3D)", 105), Tuple.Create(@"Ryzen 5 (7600|5600X|5600G|5600)", 65),
            Tuple.Create(@"i9-1[234]900K", 253), Tuple.Create(@"i7-1[234]700K", 253), Tuple.Create(@"i5-1[234]600K", 181), Tuple.Create(@"Core\(TM\) Ultra 9 2\d\dK", 250), Tuple.Create(@"Core\(TM\) Ultra 7 2\d\dK", 250), Tuple.Create(@"Core\(TM\) Ultra 5 2\d\dK", 159),
            Tuple.Create(@"i[3579]-1[234]\d00F?\b", 65), Tuple.Create(@"i[3579]-1[01]\d00K", 125), Tuple.Create(@"i[3579]-\d{4,5}(U|P)\b|Ultra [579] \d{3}[UV]\b|Ryzen [3579] \d{4}U", 20),
            Tuple.Create(@"i[3579]-\d{4,5}H[SKX]?\b|Ryzen [3579] \d{4}H[SX]?\b|Ultra [579] \d{3}H\b|Ryzen AI", 45)
        };
        private static readonly Tuple<string, int>[] Cartes =
        {
            Tuple.Create(@"RTX 5090", 575), Tuple.Create(@"RTX 5080", 360), Tuple.Create(@"RTX 5070 Ti", 300), Tuple.Create(@"RTX 5070", 250), Tuple.Create(@"RTX 5060 Ti", 180), Tuple.Create(@"RTX 5060", 145),
            Tuple.Create(@"RTX 4090", 450), Tuple.Create(@"RTX 4080", 320), Tuple.Create(@"RTX 4070 Ti", 285), Tuple.Create(@"RTX 4070", 200), Tuple.Create(@"RTX 4060 Ti", 160), Tuple.Create(@"RTX 4060", 115),
            Tuple.Create(@"RTX 3090", 350), Tuple.Create(@"RTX 3080", 320), Tuple.Create(@"RTX 3070", 220), Tuple.Create(@"RTX 3060 Ti", 200), Tuple.Create(@"RTX 3060", 170), Tuple.Create(@"RTX 3050|GTX 16\d0", 120),
            Tuple.Create(@"RTX 20\d0", 215), Tuple.Create(@"GTX 10[78]0", 180), Tuple.Create(@"GTX 10[56]0", 110),
            Tuple.Create(@"RX 9070 XT", 304), Tuple.Create(@"RX 9070", 220), Tuple.Create(@"RX 9060 XT", 160),
            Tuple.Create(@"RX 7900 XTX", 355), Tuple.Create(@"RX 7900 XT\b", 315), Tuple.Create(@"RX 7900 GRE", 260), Tuple.Create(@"RX 7800 XT", 263), Tuple.Create(@"RX 7700 XT", 245), Tuple.Create(@"RX 7600", 165),
            Tuple.Create(@"RX 69[05]0", 300), Tuple.Create(@"RX 6800", 250), Tuple.Create(@"RX 67[05]0", 230), Tuple.Create(@"RX 66[05]0", 160), Tuple.Create(@"RX 6[45]00", 75), Tuple.Create(@"RX 5[67]00", 180),
            Tuple.Create(@"Arc B580", 190), Tuple.Create(@"Arc A7[57]0", 225), Tuple.Create(@"Arc A[35]\d0", 120),
            Tuple.Create(@"Laptop GPU|Mobile|Max-Q", 100)
        };

        public static ModeleEnergie Modele()
        {
            var m = new ModeleEnergie();
            try { m.Portable = InfosPc.EstPortable; } catch { }
            try { m.Processeur = InfosPc.Processeur(); } catch { }
            m.Tdp = Processeurs.Where(t => Regex.IsMatch(m.Processeur, t.Item1, RegexOptions.IgnoreCase)).Select(t => t.Item2).FirstOrDefault();
            if (m.Tdp == 0) m.Tdp = m.Portable ? 28 : 95;
            try
            {
                var c = InfosPc.CartesGraphiques().FirstOrDefault();
                if (c != null)
                {
                    m.Carte = c.Nom; m.CarteIntegree = c.Integree;
                    m.Tbp = c.Integree ? 0 : Cartes.Where(t => Regex.IsMatch(c.Nom, t.Item1, RegexOptions.IgnoreCase)).Select(t => t.Item2).FirstOrDefault();
                    if (!c.Integree && m.Tbp == 0) m.Tbp = m.Portable ? 90 : 200;
                }
            }
            catch { }
            m.Base = m.Portable ? 10 : 40;           // carte mère, mémoire, disques, ventilateurs (portable : écran intégré compris)
            m.Rendement = m.Portable ? 0.90 : 0.88;  // pertes de l'alimentation
            return m;
        }

        // ------------------------------------------------------------ Lecture / écriture
        public static List<JourEnergie> Lire()
        {
            var l = new List<JourEnergie>();
            var d = Json.Dict(Json.LireFichier(Fichier) as Dictionary<string, object>, "Jours");
            if (d == null) return l;
            foreach (var kv in d)
            {
                DateTime j; var o = kv.Value as Dictionary<string, object>;
                if (o == null || !DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out j)) continue;
                var je = new JourEnergie { Jour = j, Wh = Json.Double(o, "Wh"), WhInactif = Json.Double(o, "WhInactif"), Minutes = (int)Json.Long(o, "Minutes"), MinutesInactif = (int)Json.Long(o, "Inactif") };
                var a = Json.Dict(o, "Apps");
                if (a != null) foreach (var x in a) je.Apps[x.Key] = Convert.ToDouble(x.Value, CultureInfo.InvariantCulture);
                l.Add(je);
            }
            return l.OrderBy(x => x.Jour).ToList();
        }

        public static void Ecrire(IEnumerable<JourEnergie> jours)
        {
            var d = new Dictionary<string, object>();
            foreach (var j in jours.Where(x => x.Jour >= DateTime.Today.AddDays(-400)))
            {
                var apps = j.Apps.OrderByDescending(x => x.Value).ToList();
                var garder = apps.Take(30).ToDictionary(x => x.Key, x => (object)Math.Round(x.Value, 2));
                double reste = apps.Skip(30).Sum(x => x.Value);
                if (reste > 0) garder["(autres)"] = Math.Round(reste + (garder.ContainsKey("(autres)") ? Convert.ToDouble(garder["(autres)"]) : 0), 2);
                d[j.Jour.ToString("yyyy-MM-dd")] = new Dictionary<string, object> { { "Wh", Math.Round(j.Wh, 2) }, { "WhInactif", Math.Round(j.WhInactif, 2) }, { "Minutes", j.Minutes }, { "Inactif", j.MinutesInactif }, { "Apps", garder } };
            }
            Directory.CreateDirectory(Chemins.Local);
            File.WriteAllText(Fichier, Json.Ecrire(new Dictionary<string, object> { { "Jours", d } }));
        }

        // ------------------------------------------------------------ Relevé (gardien, une fois par minute)
        internal class Releveur
        {
            private readonly ModeleEnergie m = Modele();
            private long idle0, kernel0, user0;
            private DateTime t0 = DateTime.MinValue;
            private readonly Dictionary<int, Tuple<string, long>> cpu0 = new Dictionary<int, Tuple<string, long>>();
            private Dictionary<string, CounterSample> gpu0 = new Dictionary<string, CounterSample>();
            private bool gpuIndisponible;
            private readonly Dictionary<DateTime, JourEnergie> jours = new Dictionary<DateTime, JourEnergie>();
            private int nonEcrits;

            public Releveur() { foreach (var j in Lire().Where(x => x.Jour >= DateTime.Today.AddDays(-2))) jours[j.Jour] = j; }

            public void Mesurer()
            {
                if (!Natif.EstWindows) return;
                DateTime t = DateTime.Now;
                long idle, kernel, user;
                if (!GetSystemTimes(out idle, out kernel, out user)) return;
                var procs = new Dictionary<int, Tuple<string, long>>();
                foreach (var p in Process.GetProcesses())
                {
                    try { if (p.Id > 4) procs[p.Id] = Tuple.Create(p.ProcessName, p.TotalProcessorTime.Ticks); } catch { }
                    finally { p.Dispose(); }
                }
                var gpu = LireGpu();
                if (t0 != DateTime.MinValue && (t - t0).TotalSeconds >= 20 && (t - t0).TotalMinutes <= 10)
                {
                    double total = (kernel - kernel0) + (user - user0), libre = idle - idle0;
                    double utilCpu = total > 0 ? Math.Max(0, Math.Min(1, (total - libre) / total)) : 0;
                    // part de chaque application dans le processeur (temps processeur consommé pendant l'intervalle)
                    var partsCpu = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    double sommeCpu = 0;
                    foreach (var kv in procs)
                    {
                        Tuple<string, long> avant;
                        if (!cpu0.TryGetValue(kv.Key, out avant) || avant.Item1 != kv.Value.Item1) continue;
                        double dt = kv.Value.Item2 - avant.Item2;
                        if (dt <= 0) continue;
                        double v; partsCpu.TryGetValue(kv.Value.Item1, out v); partsCpu[kv.Value.Item1] = v + dt; sommeCpu += dt;
                    }
                    // part de chaque application dans la carte graphique (moteur 3D)
                    var partsGpu = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                    double utilGpu = 0;
                    foreach (var kv in gpu)
                    {
                        CounterSample avant;
                        if (!gpu0.TryGetValue(kv.Key, out avant)) continue;
                        double u; try { u = CounterSample.Calculate(avant, kv.Value); } catch { continue; }
                        if (u <= 0 || double.IsNaN(u)) continue;
                        var mp = Regex.Match(kv.Key, @"^pid_(\d+)_");
                        if (!mp.Success) continue;
                        int pid = int.Parse(mp.Groups[1].Value);
                        Tuple<string, long> pr; string nom = procs.TryGetValue(pid, out pr) ? pr.Item1 : "?";
                        double v; partsGpu.TryGetValue(nom, out v); partsGpu[nom] = v + u; utilGpu += u;
                    }
                    utilGpu = Math.Min(1, utilGpu / 100.0);
                    double h = (t - t0).TotalHours;
                    double pCpuRepos = m.Tdp * (m.Portable ? 0.08 : 0.12), pCpuDyn = (m.Tdp - pCpuRepos) * utilCpu;
                    double pGpuRepos = m.Tbp * 0.08, pGpuDyn = (m.Tbp - pGpuRepos) * utilGpu;
                    double eff = 1 / m.Rendement;
                    double whTotal = (m.Base + pCpuRepos + pCpuDyn + pGpuRepos + pGpuDyn) * eff * h;
                    var jour = Jour(t.Date);
                    jour.Wh += whTotal; jour.Minutes += (int)Math.Round((t - t0).TotalMinutes);
                    double attribue = 0;
                    if (sommeCpu > 0) foreach (var kv in partsCpu) { double wh = pCpuDyn * eff * h * kv.Value / sommeCpu; Ajouter(jour, kv.Key, wh); attribue += wh; }
                    double sommeGpu = partsGpu.Values.Sum();
                    if (sommeGpu > 0) foreach (var kv in partsGpu) { double wh = pGpuDyn * eff * h * kv.Value / sommeGpu; Ajouter(jour, kv.Key, wh); attribue += wh; }
                    Ajouter(jour, NomSysteme, Math.Max(0, whTotal - attribue));
                    if (SecondesInactif() >= 600) { jour.MinutesInactif += (int)Math.Round((t - t0).TotalMinutes); jour.WhInactif += whTotal; }
                    if (++nonEcrits >= 10) Enregistrer();
                }
                t0 = t; idle0 = idle; kernel0 = kernel; user0 = user;
                cpu0.Clear(); foreach (var kv in procs) cpu0[kv.Key] = kv.Value;
                gpu0 = gpu;
            }

            private static void Ajouter(JourEnergie j, string app, double wh)
            {
                if (wh <= 0) return;
                string n = app == "?" || app == "Idle" || app == "System" || app == "dwm" || app == "csrss" ? NomSysteme : app;
                double v; j.Apps.TryGetValue(n, out v); j.Apps[n] = v + wh;
            }

            private JourEnergie Jour(DateTime d)
            {
                JourEnergie j;
                if (!jours.TryGetValue(d, out j)) { j = new JourEnergie { Jour = d }; jours[d] = j; foreach (var vieux in jours.Keys.Where(x => x < d.AddDays(-2)).ToList()) jours.Remove(vieux); }
                return j;
            }

            public void Enregistrer()
            {
                if (nonEcrits == 0) return;
                try
                {
                    var tous = Lire().ToDictionary(x => x.Jour);
                    foreach (var kv in jours) tous[kv.Key] = kv.Value;
                    Ecrire(tous.Values);
                    nonEcrits = 0;
                }
                catch (Exception ex) { JournalDemarrage.Erreur("consommation électrique", ex); }
            }

            private Dictionary<string, CounterSample> LireGpu()
            {
                var d = new Dictionary<string, CounterSample>();
                if (gpuIndisponible || m.Tbp <= 0) return d;
                try
                {
                    var cat = new PerformanceCounterCategory("GPU Engine");
                    var donnees = cat.ReadCategory();
                    var c = donnees["Utilization Percentage"];
                    if (c == null) return d;
                    foreach (InstanceData i in c.Values)
                        if (i.InstanceName.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase)) d[i.InstanceName] = i.Sample;
                }
                catch { gpuIndisponible = true; }
                return d;
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

        [StructLayout(LayoutKind.Sequential)] private struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LASTINPUTINFO l);

        /// <summary>Secondes depuis la dernière action au clavier ou à la souris.</summary>
        public static double SecondesInactif()
        {
            try { var l = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf(typeof(LASTINPUTINFO)) }; if (GetLastInputInfo(ref l)) return ((uint)Environment.TickCount - l.dwTime) / 1000.0; } catch { }
            return 0;
        }
    }
}
