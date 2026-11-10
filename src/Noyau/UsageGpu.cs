// Velox - utilisation de la carte graphique en %, comme l'onglet Performances du Gestionnaire des tâches :
// compteurs de Windows « GPU Engine » (moteurs 3D), sans droits administrateur. La carte la plus occupée est retenue.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal static class UsageGpu
    {
        private static readonly object verrou = new object();
        private static Dictionary<string, CounterSample> avant;
        private static DateTime derniere = DateTime.MinValue;
        private static double valeur = -1;
        private static bool indisponible;
        private static readonly Regex Carte = new Regex(@"luid_0x[0-9a-f]+_0x[0-9a-f]+_phys_\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Utilisation 3D de la carte graphique la plus occupée (0 à 100), ou -1 si Windows ne la donne pas.
        /// Lue au plus une fois toutes les 1,5 s (la première lecture renvoie -1 : il faut deux relevés).</summary>
        public static double Lire()
        {
            if (indisponible || !OperatingSystem.IsWindows()) return -1;
            lock (verrou)
            {
                if ((DateTime.Now - derniere).TotalMilliseconds < 1500) return valeur;
                derniere = DateTime.Now;
                try
                {
                    var maintenant = new Dictionary<string, CounterSample>(StringComparer.OrdinalIgnoreCase);
                    var donnees = new PerformanceCounterCategory("GPU Engine").ReadCategory();
                    var c = donnees["Utilization Percentage"];
                    if (c == null) { indisponible = true; return valeur = -1; }
                    foreach (InstanceData i in c.Values)
                        if (i.InstanceName.EndsWith("engtype_3D", StringComparison.OrdinalIgnoreCase)) maintenant[i.InstanceName] = i.Sample;
                    if (avant != null)
                    {
                        var parCarte = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                        foreach (var kv in maintenant)
                        {
                            CounterSample a;
                            if (!avant.TryGetValue(kv.Key, out a)) continue;
                            double v = CounterSample.Calculate(a, kv.Value);
                            if (double.IsNaN(v) || v <= 0) continue;
                            var m = Carte.Match(kv.Key);
                            string cle = m.Success ? m.Value : "";
                            double s; parCarte.TryGetValue(cle, out s); parCarte[cle] = s + v;
                        }
                        double max = 0;
                        foreach (var v in parCarte.Values) if (v > max) max = v;
                        valeur = Math.Min(100, Math.Round(max));
                    }
                    avant = maintenant;
                }
                catch (InvalidOperationException) { indisponible = true; valeur = -1; }   // catégorie absente (pilote ancien, machine virtuelle)
                catch (Exception ex)
                {
                    indisponible = true; valeur = -1;
                    JournalDemarrage.Noter("Utilisation GPU non lue : " + ex.GetType().Name + " " + ex.Message);
                }
                return valeur;
            }
        }
    }
}
