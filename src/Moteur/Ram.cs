// Nettoyage PC - moteur : mémoire vive (barrettes, utilisation, libération)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace NettoyagePC
{
    internal class Barrette
    {
        public long Capacite;
        public int TypeSmbios, Format, Vitesse, VitesseConfiguree;
        public string Fabricant = "", Reference = "", Emplacement = "", Banque = "";
        public string Type
        {
            get
            {
                var t = new Dictionary<int, string> { { 20, "DDR" }, { 21, "DDR2" }, { 24, "DDR3" }, { 26, "DDR4" }, { 27, "LPDDR" }, { 28, "LPDDR2" }, { 29, "LPDDR3" }, { 30, "LPDDR4" }, { 34, "DDR5" }, { 35, "LPDDR5" } };
                string s; return t.TryGetValue(TypeSmbios, out s) ? s : "type inconnu";
            }
        }

        public static List<Barrette> Lister()
        {
            return Wmi.Requete("SELECT Capacity, SMBIOSMemoryType, FormFactor, Speed, ConfiguredClockSpeed, Manufacturer, PartNumber, DeviceLocator, BankLabel FROM Win32_PhysicalMemory")
                .Select(o => new Barrette
                {
                    Capacite = Wmi.Long(o, "Capacity"), TypeSmbios = (int)Wmi.Long(o, "SMBIOSMemoryType"), Format = (int)Wmi.Long(o, "FormFactor"),
                    Vitesse = (int)Wmi.Long(o, "Speed"), VitesseConfiguree = (int)Wmi.Long(o, "ConfiguredClockSpeed"), Fabricant = Wmi.Texte(o, "Manufacturer"),
                    Reference = Wmi.Texte(o, "PartNumber"), Emplacement = Wmi.Texte(o, "DeviceLocator"), Banque = Wmi.Texte(o, "BankLabel")
                }).ToList();
        }

        /// <summary>Vitesse prévue par le fabricant, souvent écrite dans la référence (ex. CMK32GX5M2E6000C36 = 6000).</summary>
        public static int VitessePrevue(List<Barrette> l)
        {
            int prevue = 0;
            foreach (var b in l)
                foreach (Match m in Regex.Matches(b.Reference ?? "", @"(?<!\d)(\d{4})(?!\d)"))
                {
                    int v = int.Parse(m.Groups[1].Value);
                    bool plage = (b.TypeSmbios == 34 && v >= 4800 && v <= 9600) || (b.TypeSmbios == 26 && v >= 2133 && v <= 5333);
                    if (plage && v > prevue) prevue = v;
                }
            return prevue;
        }

        public static int VitesseReelle(List<Barrette> l)
        {
            var v = l.Where(b => b.VitesseConfiguree > 0).Select(b => b.VitesseConfiguree).ToList();
            return v.Count == 0 ? 0 : v.Min();
        }
    }

    internal class EtatMemoire
    {
        public long Total, Utilisee, Disponible, Libre, Cache, Modifiee, Engagee, LimiteEngagee;

        public static EtatMemoire Lire()
        {
            var e = new EtatMemoire();
            ulong t, d; Natif.Memoire(out t, out d);
            e.Total = (long)t; e.Disponible = (long)d; e.Libre = e.Disponible;
            var p = Wmi.Requete("SELECT AvailableBytes, FreeAndZeroPageListBytes, StandbyCacheCoreBytes, StandbyCacheNormalPriorityBytes, StandbyCacheReserveBytes, ModifiedPageListBytes, CommittedBytes, CommitLimit FROM Win32_PerfFormattedData_PerfOS_Memory").FirstOrDefault();
            if (p != null)
            {
                e.Disponible = Wmi.Long(p, "AvailableBytes");
                e.Libre = Wmi.Long(p, "FreeAndZeroPageListBytes");
                e.Cache = Wmi.Long(p, "StandbyCacheCoreBytes") + Wmi.Long(p, "StandbyCacheNormalPriorityBytes") + Wmi.Long(p, "StandbyCacheReserveBytes");
                e.Modifiee = Wmi.Long(p, "ModifiedPageListBytes");
                e.Engagee = Wmi.Long(p, "CommittedBytes");
                e.LimiteEngagee = Wmi.Long(p, "CommitLimit");
            }
            e.Utilisee = e.Total - e.Disponible;
            return e;
        }
    }

    internal partial class Moteur
    {
        private void AlerteRam(string texte) { alertesRam.Add(texte); Ecrire("    ATTENTION : " + texte); }

        private void AfficherMemoire(string titre, EtatMemoire e)
        {
            double pct = e.Total > 0 ? 100.0 * e.Utilisee / e.Total : 0;
            Ecrire("  " + titre + " : " + T(e.Utilisee) + " utilisés sur " + T(e.Total) + " (" + pct.ToString("0", Appli.Fr) + " %)");
            Ecrire("    Disponible " + T(e.Disponible) + " (dont libre " + T(e.Libre) + " et cache " + T(e.Cache) + ") ; pages modifiées " + T(e.Modifiee));
            if (e.LimiteEngagee > 0) Ecrire("    Mémoire engagée " + T(e.Engagee) + " sur " + T(e.LimiteEngagee) + " possibles (RAM + fichier d'échange)");
        }

        private void MemoireVive()
        {
            Ecrire("=== Mémoire vive (RAM) ===");
            var barrettes = Barrette.Lister();
            var baie = Wmi.Requete("SELECT MemoryDevices, MaxCapacityEx FROM Win32_PhysicalMemoryArray").FirstOrDefault();
            if (barrettes.Count == 0) Ecrire("  Barrettes : information indisponible (machine virtuelle, ou BIOS qui ne la fournit pas).");
            else
            {
                long total = barrettes.Sum(b => b.Capacite);
                int emplacements = baie == null ? 0 : (int)Wmi.Long(baie, "MemoryDevices");
                Ecrire("  Barrettes installées : " + barrettes.Count + (emplacements > 0 ? " sur " + emplacements + " emplacement(s)" : "") + ", total " + T(total));
                if (baie != null && Wmi.Long(baie, "MaxCapacityEx") > 0) Ecrire("  Capacité maximale prise en charge par la carte mère : " + T(Wmi.Long(baie, "MaxCapacityEx") * 1024.0));
                foreach (var b in barrettes)
                {
                    string vitesse = b.VitesseConfiguree > 0 ? b.VitesseConfiguree + " MHz" : "";
                    if (b.Vitesse > 0 && b.VitesseConfiguree > 0 && b.VitesseConfiguree < b.Vitesse) vitesse += " (nominale " + b.Vitesse + " MHz)";
                    string emp = b.Emplacement; if (b.Banque != "" && b.Banque != emp) emp = b.Banque + " / " + emp;
                    Ecrire("    " + Col(emp, 26) + " " + Dr(T(b.Capacite), 9) + "  " + b.Type + " " + (b.Format == 12 ? "SODIMM" : "DIMM") + "  " + vitesse + "  " + b.Fabricant + " " + b.Reference);
                }
                if (barrettes.Count == 1 && emplacements > 1) Ecrire("  Conseil : une seule barrette est installée. Avec deux barrettes identiques, la carte mère peut utiliser le double canal (meilleures performances).");
                if (barrettes.Count > 1 && barrettes.Select(b => b.Capacite).Distinct().Count() > 1) Ecrire("  Remarque : les barrettes n'ont pas toutes la même capacité (le double canal peut être partiel).");
                int prevue = Barrette.VitessePrevue(barrettes), reelle = Barrette.VitesseReelle(barrettes);
                if (prevue > 0 && reelle > 0 && reelle < prevue - 100)
                    AlerteRam("Tes barrettes sont prévues pour " + prevue + " MHz mais tournent à " + reelle + " MHz : le profil XMP/EXPO n'est pas activé dans le BIOS. L'activer (une option à cocher dans le BIOS) donne de meilleures performances, surtout en jeu. Détails : Réglages > Matériel. Si le PC devient instable ensuite, désactive-le simplement.");
                else if (barrettes.Any(b => b.Vitesse > 0 && b.VitesseConfiguree > 0 && b.VitesseConfiguree < b.Vitesse))
                    Ecrire("  Remarque : une barrette tourne sous sa vitesse nominale. Si ce sont des barrettes gaming, le profil XMP/EXPO n'est peut-être pas activé dans le BIOS.");
            }
            Ecrire();

            var avant = EtatMemoire.Lire();
            AfficherMemoire("Utilisation actuelle", avant);
            double pctU = avant.Total > 0 ? 100.0 * avant.Utilisee / avant.Total : 0;
            resumeRam = T(avant.Utilisee) + " utilisés sur " + T(avant.Total) + " (" + pctU.ToString("0", Appli.Fr) + " %)";
            Ecrire();
            Ecrire("  Applications qui utilisent le plus de mémoire :");
            try
            {
                var groupes = Process.GetProcesses().GroupBy(p => p.ProcessName).Select(g => new { Nom = g.Key, N = g.Count(), Octets = g.Sum(p => { try { return p.WorkingSet64; } catch { return 0L; } }) })
                    .OrderByDescending(x => x.Octets).Take(12).ToList();
                foreach (var g in groupes) Ecrire("    " + Dr(T(g.Octets), 10) + "  " + g.Nom + (g.N > 1 ? " (" + g.N + " processus)" : ""));
            }
            catch { }
            if (pctU >= 85) AlerteRam("Plus de 85 % de la RAM est utilisée. Ferme les applications du haut de la liste (Nettoyage PC ne ferme jamais d'application à ta place), redémarre le PC de temps en temps, et si c'est permanent, ajoute de la RAM.");
            Ecrire();

            var whea = Evenements.Lire("System", "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and TimeCreated[timediff(@SystemTime) <= 2592000000]]]", 200);
            if (whea.Count > 0) AlerteRam(whea.Count + " erreur(s) matérielle(s) signalée(s) par Windows (WHEA) ces 30 derniers jours. Ce n'est pas forcément la RAM (processeur, bus PCIe, disque), mais teste tes barrettes (bouton « Test des barrettes »).");
            else if (Natif.EstWindows) Ecrire("  Erreurs matérielles Windows (WHEA) sur 30 jours : aucune");
            var test = Evenements.Lire("System", "*[System[Provider[@Name='Microsoft-Windows-MemoryDiagnostics-Results']]]", 1).FirstOrDefault();
            if (test != null)
            {
                string msg = Regex.Replace(Evenements.Message(test), @"\s+", " ").Trim();
                if (msg.Length > 170) msg = msg.Substring(0, 170) + "...";
                Ecrire("  Dernier test mémoire de Windows (" + (test.TimeCreated ?? DateTime.Now).ToString("dd/MM/yyyy") + ") : " + msg);
            }
            else Ecrire("  Aucun test mémoire de Windows enregistré : le bouton « Test des barrettes » permet de tester les barrettes.");

            if (O.LibererRam)
            {
                Ecrire();
                if (O.Simulation)
                {
                    Ecrire("  Libération prévue (simulation, rien n'est exécuté) :");
                    Ecrire("    1. Réduction des working sets : les pages peu utilisées des processus quittent les barrettes.");
                    Ecrire("    2. Écriture sur disque de la liste des pages modifiées.");
                    Ecrire("    3. Purge du cache mémoire (liste standby) : environ " + T(avant.Cache) + " seraient rendus à la mémoire libre.");
                    resumeRam += " - libération non exécutée (simulation)";
                }
                else if (!Admin) Ecrire("  Libération ignorée : il faut l'autorisation administrateur.");
                else
                {
                    var etapes = new[] { Tuple.Create("Réduction des working sets des processus", 2), Tuple.Create("Écriture des pages modifiées sur disque", 3), Tuple.Create("Purge du cache mémoire (liste standby)", 4) };
                    foreach (var e in etapes)
                    {
                        int c = Natif.CommandeMemoire(e.Item2);
                        if (c == 0) Ecrire("  OK : " + e.Item1);
                        else if (c == -1) Ecrire("  Droits insuffisants : " + e.Item1);
                        else Ecrire("  Non exécuté (code 0x" + c.ToString("X8") + ") : " + e.Item1);
                    }
                    Thread.Sleep(3000);
                    var apres = EtatMemoire.Lire();
                    Ecrire();
                    AfficherMemoire("Après libération", apres);
                    long gagne = avant.Utilisee - apres.Utilisee;
                    if (gagne > 0)
                    {
                        Ecrire("  -> " + T(gagne) + " de RAM libérés (utilisés : " + T(avant.Utilisee) + " -> " + T(apres.Utilisee) + ")");
                        resumeRam = T(gagne) + " libérés (utilisés : " + T(avant.Utilisee) + " -> " + T(apres.Utilisee) + ")";
                    }
                    else { Ecrire("  -> Aucun gain mesurable : Windows gérait déjà bien la mémoire."); resumeRam += " - aucun gain mesurable"; }
                    Ecrire();
                    Ecrire("  Note : cette libération est temporaire. Windows utilise la mémoire « disponible » comme cache pour accélérer les applications ;");
                    Ecrire("  la vider peut ralentir un peu les premières secondes, le temps que les applications se rechargent.");
                    Ecrire("  Elle sert surtout juste avant de lancer une application ou un jeu gourmand.");
                }
            }
            Ecrire();
        }

        private void TesterRam()
        {
            Ecrire("Ouverture du Diagnostic de mémoire Windows...");
            Ecrire("Choisis « Redémarrer maintenant » (enregistre d'abord ton travail) ou « Rechercher les problèmes au prochain démarrage ».");
            Ecrire("Le test dure plusieurs minutes. Son résultat s'affichera après le redémarrage, et Nettoyage PC le rappelle dans la section Mémoire vive.");
            string md = System.IO.Path.Combine(Chemins.Systeme32, "mdsched.exe");
            if (!System.IO.File.Exists(md)) Ecrire("Le Diagnostic de mémoire Windows n'est pas disponible sur ce PC.");
            else try { Process.Start(new ProcessStartInfo(md) { UseShellExecute = true }); } catch { }
        }
    }
}
