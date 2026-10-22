// Nettoyage PC - santé de la batterie (Premium) : rapport de Windows (powercfg /batteryreport, sans droits administrateur),
// avec repli sur les compteurs WMI de la batterie.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace NettoyagePC
{
    internal class InfosBatterie
    {
        public bool Presente;
        public string Nom = "", Fabricant = "", Chimie = "", Erreur = "";
        public long Origine, Actuelle;          // mWh
        public int Cycles = -1, Charge = -1;
        public bool SurSecteur;
        public TimeSpan? AutonomieActuelle, AutonomieOrigine;
        public List<Tuple<DateTime, long, long>> Historique = new List<Tuple<DateTime, long, long>>();   // date, pleine charge, origine

        public double Sante { get { return Origine > 0 && Actuelle > 0 ? Math.Min(100, 100.0 * Actuelle / Origine) : -1; } }
        public string NomChimie
        {
            get
            {
                switch ((Chimie ?? "").ToUpperInvariant())
                {
                    case "LION": case "LI-I": case "LI-ION": return "lithium-ion";
                    case "LIP": case "LIPO": return "lithium-polymère";
                    case "NIMH": return "nickel-métal hydrure";
                    default: return Chimie ?? "";
                }
            }
        }
    }

    internal static class Batterie
    {
        public static InfosBatterie Lire()
        {
            var r = new InfosBatterie();
            if (!Natif.EstWindows) return r;
            var b = Wmi.Requete("SELECT Name, EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery").FirstOrDefault();
            if (b == null) return r;
            r.Presente = true;
            r.Nom = Wmi.Texte(b, "Name");
            r.Charge = (int)Wmi.Long(b, "EstimatedChargeRemaining");
            long statut = Wmi.Long(b, "BatteryStatus");
            r.SurSecteur = statut == 2 || statut >= 6 && statut <= 9;
            try { LireRapport(r); } catch (Exception ex) { r.Erreur = ex.Message; }
            if (r.Origine <= 0 || r.Actuelle <= 0) LireWmi(r);
            return r;
        }

        private static void LireWmi(InfosBatterie r)
        {
            try
            {
                var s = Wmi.Requete("SELECT DesignedCapacity FROM BatteryStaticData", @"root\wmi").FirstOrDefault();
                var p = Wmi.Requete("SELECT FullChargedCapacity FROM BatteryFullChargedCapacity", @"root\wmi").FirstOrDefault();
                var c = Wmi.Requete("SELECT CycleCount FROM BatteryCycleCount", @"root\wmi").FirstOrDefault();
                if (s != null && r.Origine <= 0) r.Origine = Wmi.Long(s, "DesignedCapacity");
                if (p != null && r.Actuelle <= 0) r.Actuelle = Wmi.Long(p, "FullChargedCapacity");
                if (c != null && r.Cycles < 0) { long n = Wmi.Long(c, "CycleCount"); if (n > 0) r.Cycles = (int)n; }
            }
            catch { }
        }

        private static string Valeur(XElement e, string nom)
        {
            var a = e.Attributes().FirstOrDefault(x => x.Name.LocalName == nom);
            if (a != null) return a.Value;
            var c = e.Elements().FirstOrDefault(x => x.Name.LocalName == nom);
            return c == null ? "" : c.Value.Trim();
        }
        private static long Long(XElement e, string nom) { long v; return long.TryParse(Valeur(e, nom), NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : 0; }

        private static TimeSpan? Duree(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return null;
            try { var t = XmlConvert.ToTimeSpan(iso); return t.TotalMinutes > 1 ? (TimeSpan?)t : null; } catch { return null; }
        }

        /// <summary>Rapport de batterie de Windows au format XML : capacités, cycles, autonomies estimées, historique de la capacité.</summary>
        private static void LireRapport(InfosBatterie r)
        {
            string fichier = Path.Combine(Path.GetTempPath(), "nettoyagepc-batterie-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".xml");
            try
            {
                int code;
                Programmes.Executer(Path.Combine(Chemins.Systeme32, "powercfg.exe"), "/batteryreport /xml /output \"" + fichier + "\"", 60000, null, out code);
                if (!File.Exists(fichier)) return;
                var x = XDocument.Load(fichier);
                var bat = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "Battery" && e.Elements().Any(c => c.Name.LocalName == "DesignCapacity"));
                if (bat != null)
                {
                    r.Origine = Long(bat, "DesignCapacity"); r.Actuelle = Long(bat, "FullChargeCapacity");
                    long cy = Long(bat, "CycleCount"); if (cy > 0) r.Cycles = (int)cy;
                    string f = Valeur(bat, "Manufacturer"); if (f != "") r.Fabricant = f;
                    string id = Valeur(bat, "Id"); if (id != "") r.Nom = id;
                    r.Chimie = Valeur(bat, "Chemistry");
                }
                var est = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "RuntimeEstimates");
                if (est != null)
                {
                    var pleine = est.Elements().FirstOrDefault(e => e.Name.LocalName == "FullChargeCapacity");
                    var origine = est.Elements().FirstOrDefault(e => e.Name.LocalName == "DesignCapacity");
                    if (pleine != null) r.AutonomieActuelle = Duree(Valeur(pleine, "ActiveRuntime"));
                    if (origine != null) r.AutonomieOrigine = Duree(Valeur(origine, "ActiveRuntime"));
                }
                foreach (var h in x.Descendants().Where(e => e.Name.LocalName.EndsWith("HistoryEntry")))
                {
                    long pc = Long(h, "FullChargeCapacity"), dc = Long(h, "DesignCapacity");
                    string ds = Valeur(h, "LocalStartDate"); if (ds == "") ds = Valeur(h, "StartDate");
                    DateTime d;
                    if (pc > 0 && DateTime.TryParse(ds, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out d)) r.Historique.Add(Tuple.Create(d, pc, dc));
                }
                r.Historique = r.Historique.OrderBy(t => t.Item1).ToList();
            }
            finally { try { File.Delete(fichier); } catch { } }
        }

        /// <summary>Rapport complet de Windows (HTML) dans Documents\NettoyagePC, ouvert ensuite dans le navigateur.</summary>
        public static string RapportHtml()
        {
            Chemins.CreerDonnees();
            string f = Chemins.Fichier("rapport-batterie.html");
            int code;
            Programmes.Executer(Path.Combine(Chemins.Systeme32, "powercfg.exe"), "/batteryreport /output \"" + f + "\"", 60000, null, out code);
            return File.Exists(f) ? f : null;
        }
    }
}
