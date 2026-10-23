// Nettoyage PC - moteur : disques (santé, TRIM, optimisation, vérification) et anciens pilotes
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    /// <summary>Disque physique vu par Windows (stockage moderne).</summary>
    internal class DisquePhysique
    {
        public string Id = "", Nom = "", Bus = "", Sante = "", Etat = "";
        public int TypeMedia, TypeBus;
        public long Taille;
        public double? Temperature, TemperatureMax, Usure;
        public long? Heures, ErreursLecture, ErreursEcriture;
        public ManagementObject Objet;

        public string Type
        {
            get
            {
                if (TypeBus == 7) return "Externe USB";
                if (TypeMedia == 4) return "SSD";
                if (TypeMedia == 3) return "HDD";
                if (TypeBus == 17) return "SSD";
                return "Inconnu";
            }
        }
        public bool EnBonneSante { get { return Sante == "Healthy" || Sante == ""; } }

        public static string NomBus(int b)
        {
            switch (b) { case 1: return "SCSI"; case 3: return "ATA"; case 7: return "USB"; case 8: return "RAID"; case 10: return "SAS"; case 11: return "SATA"; case 12: return "SD"; case 17: return "NVMe"; default: return "autre"; }
        }
        public static string NomSante(long s) { switch (s) { case 0: return "Healthy"; case 1: return "Warning"; case 2: return "Unhealthy"; default: return "Unknown"; } }

        public static List<DisquePhysique> Lister(bool fiabilite)
        {
            var l = new List<DisquePhysique>();
            foreach (var o in Wmi.Requete("SELECT * FROM MSFT_PhysicalDisk", @"root\Microsoft\Windows\Storage"))
            {
                var d = new DisquePhysique
                {
                    Id = Wmi.Texte(o, "DeviceId"), Nom = Wmi.Texte(o, "FriendlyName"), TypeMedia = (int)Wmi.Long(o, "MediaType"),
                    TypeBus = (int)Wmi.Long(o, "BusType"), Taille = Wmi.Long(o, "Size"), Sante = NomSante(Wmi.Long(o, "HealthStatus")), Objet = o
                };
                d.Bus = NomBus(d.TypeBus);
                try { var ops = o["OperationalStatus"] as ushort[]; if (ops != null) d.Etat = string.Join(", ", ops.Select(x => x == 2 ? "OK" : x.ToString())); } catch { }
                if (fiabilite)
                {
                    try
                    {
                        foreach (ManagementObject r in o.GetRelated("MSFT_StorageReliabilityCounter"))
                        {
                            object t = Wmi.Valeur(r, "Temperature"), w = Wmi.Valeur(r, "Wear"), h = Wmi.Valeur(r, "PowerOnHours");
                            if (t != null && Convert.ToDouble(t) > 0) d.Temperature = Convert.ToDouble(t);
                            object tm = Wmi.Valeur(r, "TemperatureMax"); if (tm != null && Convert.ToDouble(tm) > 0) d.TemperatureMax = Convert.ToDouble(tm);
                            if (w != null) d.Usure = Convert.ToDouble(w);
                            if (h != null && Convert.ToInt64(h) > 0) d.Heures = Convert.ToInt64(h);
                            object el = Wmi.Valeur(r, "ReadErrorsUncorrected"), ee = Wmi.Valeur(r, "WriteErrorsUncorrected");
                            if (el != null) d.ErreursLecture = Convert.ToInt64(el);
                            if (ee != null) d.ErreursEcriture = Convert.ToInt64(ee);
                        }
                    }
                    catch { }
                }
                l.Add(d);
            }
            return l;
        }

        /// <summary>Type du disque physique qui porte une lettre de lecteur.</summary>
        public static string TypeVolume(char lettre, List<DisquePhysique> disques)
        {
            foreach (var p in Wmi.Requete("SELECT DiskNumber, DriveLetter FROM MSFT_Partition", @"root\Microsoft\Windows\Storage"))
            {
                object dl = Wmi.Valeur(p, "DriveLetter");
                if (dl == null) continue;
                char c = dl is char ? (char)dl : Convert.ToChar(Convert.ToUInt16(dl));
                if (char.ToUpperInvariant(c) != char.ToUpperInvariant(lettre)) continue;
                string num = Wmi.Texte(p, "DiskNumber");
                var d = disques.FirstOrDefault(x => x.Id == num);
                return d == null ? "Inconnu" : d.Type;
            }
            return "Inconnu";
        }
    }

    internal static class TailleFichiers
    {
        /// <summary>Fichier OneDrive « à la demande » (pas sur le disque) : jamais touché ni haché.</summary>
        public static bool EstCloud(FileAttributes a) { int v = (int)a; return (v & 0x1000) != 0 || (v & 0x40000) != 0 || (v & 0x400000) != 0; }

        /// <summary>Taille réelle d'un dossier sur le disque (sans fichiers OneDrive en ligne, sans traverser de liens).</summary>
        public static long Dossier(string chemin, Func<bool> arret = null)
        {
            long total = 0;
            DirectoryInfo d0;
            try { d0 = new DirectoryInfo(chemin); if (!d0.Exists) return 0; } catch { return 0; }
            var pile = new Stack<DirectoryInfo>(); pile.Push(d0);
            while (pile.Count > 0)
            {
                if (arret != null && arret()) break;
                var d = pile.Pop();
                try
                {
                    foreach (var e in d.EnumerateFileSystemInfos())
                    {
                        FileAttributes a; try { a = e.Attributes; } catch { continue; }
                        if ((a & FileAttributes.ReparsePoint) != 0) continue;
                        if ((a & FileAttributes.Directory) != 0) pile.Push((DirectoryInfo)e);
                        else if (!EstCloud(a)) { try { total += ((FileInfo)e).Length; } catch { } }
                    }
                }
                catch { }
            }
            return total;
        }
    }

    internal partial class Moteur
    {
        /// <summary>Santé détaillée de chaque disque (compteurs de fiabilité : demande l'autorisation administrateur), envoyée à l'interface.</summary>
        private void SanteDisques()
        {
            Ecrire("=== Santé détaillée des disques ===");
            if (!Admin) Ecrire("  (Sans l'autorisation administrateur : compteurs de fiabilité indisponibles.)");
            var disques = DisquePhysique.Lister(Admin);
            bool panne = false;
            try { panne = Wmi.Requete("SELECT PredictFailure FROM MSStorageDriver_FailurePredictStatus", @"root\wmi").Any(s => Wmi.Bool(s, "PredictFailure")); } catch { }
            var l = new List<object>();
            foreach (var d in disques)
            {
                Ecrire("  " + d.Nom + " : " + d.Type + ", " + T(d.Taille) + ", " + d.Sante);
                l.Add(new Dictionary<string, object>
                {
                    { "Id", d.Id }, { "Nom", d.Nom }, { "Type", d.Type }, { "Bus", d.Bus }, { "Taille", d.Taille }, { "Sante", d.Sante }, { "Etat", d.Etat },
                    { "Temperature", d.Temperature }, { "TemperatureMax", d.TemperatureMax }, { "Usure", d.Usure }, { "Heures", d.Heures },
                    { "ErreursLecture", d.ErreursLecture }, { "ErreursEcriture", d.ErreursEcriture }
                });
            }
            Donnees("disques", new Dictionary<string, object> { { "Disques", l.ToArray() }, { "Panne", panne }, { "Admin", Admin } });
            Ecrire("  -> " + disques.Count + " disque(s) analysé(s)" + (panne ? " ; ATTENTION : Windows prévoit une panne prochaine sur un disque (SMART)." : "."));
        }

        private void AlerteDisque(string texte) { alertesDisques.Add(texte); Ecrire("    ATTENTION : " + texte); }

        private void ModuleDisques()
        {
            Ecrire("=== Disques : santé, SSD, optimisation ===");
            if (!Admin) Ecrire("  (Sans l'autorisation administrateur : santé détaillée et optimisation indisponibles.)");
            var disques = DisquePhysique.Lister(Admin);
            if (disques.Count == 0) Ecrire("  Aucune information de santé disponible sur ce PC.");
            foreach (var d in disques)
            {
                Ecrire("  Disque " + d.Id + " : " + d.Nom + " - " + d.Type + ", " + T(d.Taille) + ", bus " + d.Bus);
                Ecrire("    État : " + d.Sante + (d.Etat != "" ? " (" + d.Etat + ")" : ""));
                if (!d.EnBonneSante) AlerteDisque("Disque " + d.Nom + " : état de santé '" + d.Sante + "'. Sauvegarde tes données.");
                var details = new List<string>();
                if (d.Temperature.HasValue) details.Add("température " + d.Temperature.Value.ToString("0", Appli.Fr) + " °C");
                if (d.Type == "SSD" && d.Usure.HasValue) details.Add("usure " + d.Usure.Value.ToString("0", Appli.Fr) + " %");
                if (d.Heures.HasValue) details.Add(Format.Nombre(d.Heures.Value) + " h de fonctionnement");
                if (details.Count > 0) Ecrire("    " + string.Join(", ", details));
                if (d.Type == "SSD" && d.Usure >= 80) AlerteDisque("SSD " + d.Nom + " : usure à " + d.Usure.Value.ToString("0") + " %. Prévois son remplacement et sauvegarde tes données.");
                if (d.Temperature >= 70) AlerteDisque("Disque " + d.Nom + " : température élevée (" + d.Temperature.Value.ToString("0") + " °C). Vérifie la ventilation du boîtier.");
                long err = (d.ErreursLecture ?? 0) + (d.ErreursEcriture ?? 0);
                if (err > 0) AlerteDisque("Disque " + d.Nom + " : " + err + " erreur(s) de lecture/écriture non corrigée(s). Sauvegarde tes données.");
            }
            var smart = Wmi.Requete("SELECT PredictFailure FROM MSStorageDriver_FailurePredictStatus", @"root\wmi");
            if (smart.Count > 0)
            {
                if (smart.Any(s => Wmi.Bool(s, "PredictFailure"))) AlerteDisque("Windows prévoit une panne prochaine sur un disque (SMART) : sauvegarde tes données dès maintenant.");
                else Ecrire("  Prévision de panne SMART : aucune alerte");
            }

            // TRIM (utile s'il y a au moins un SSD)
            if (disques.Any(d => d.Type == "SSD"))
            {
                string t = Programmes.Executer(Path.Combine(Chemins.Systeme32, "fsutil.exe"), "behavior query DisableDeleteNotify", 15000);
                var m = Regex.Match(t, @"NTFS\s+DisableDeleteNotify\s*=\s*(\d)");
                if (m.Success)
                {
                    if (m.Groups[1].Value == "0") Ecrire("  TRIM Windows (NTFS) : activé");
                    else AlerteDisque("TRIM désactivé : active-le avec la commande suivante, lancée en administrateur : fsutil behavior set DisableDeleteNotify 0");
                }
            }

            // Volumes : espace libre, optimisation, vérification
            var volumes = DriveInfo.GetDrives().Where(v => { try { return v.DriveType == DriveType.Fixed && v.IsReady && v.TotalSize > 0 && v.Name.Length <= 3; } catch { return false; } }).ToList();
            string defrag = Path.Combine(Chemins.Systeme32, "defrag.exe");
            foreach (var v in volumes)
            {
                char l = v.Name[0];
                string type = DisquePhysique.TypeVolume(l, disques);
                double pct = 100.0 * v.AvailableFreeSpace / v.TotalSize;
                Ecrire();
                Ecrire("  Volume " + l + ": " + T(v.AvailableFreeSpace) + " libres sur " + T(v.TotalSize) + " (" + pct.ToString("0", Appli.Fr) + " % libre) - disque " + type);
                if (pct < 10) AlerteDisque("Volume " + l + ": moins de 10 % d'espace libre. Libère de la place : un disque presque plein ralentit Windows.");
                else if (type == "SSD" && pct < 15) AlerteDisque("SSD " + l + ": moins de 15 % d'espace libre. Un SSD presque plein est plus lent et sollicite davantage ses cellules : garde 10 à 20 % de libre.");
                if (!Admin) continue;
                if (type == "SSD")
                {
                    if (O.Simulation) Ecrire("    SSD : un ReTrim serait lancé (il signale au SSD les blocs inutilisés ; aucune défragmentation).");
                    else
                    {
                        Ecrire("    SSD : ReTrim en cours (jamais de défragmentation)...");
                        int c; Programmes.Executer(defrag, l + ": /L /U", 3600000, null, out c);
                        Ecrire(c == 0 ? "    ReTrim terminé." : "    ReTrim impossible (code " + c + ").");
                    }
                }
                else if (type == "HDD")
                {
                    if (O.Simulation) Ecrire("    Disque dur : une défragmentation serait lancée (elle peut durer longtemps).");
                    else
                    {
                        Ecrire("    Disque dur : défragmentation en cours (cela peut durer longtemps)...");
                        int c; Programmes.Executer(defrag, l + ": /D /U", 6 * 3600000, null, out c);
                        Ecrire(c == 0 ? "    Défragmentation terminée." : "    Défragmentation impossible (code " + c + ").");
                    }
                }
                else if (type == "Externe USB") Ecrire("    Disque externe USB : optimisation ignorée (branche-le et lance l'outil de Windows si besoin).");
                else
                {
                    if (O.Simulation) Ecrire("    Type de disque non signalé : Windows choisirait lui-même l'optimisation adaptée (defrag /O).");
                    else
                    {
                        Ecrire("    Type de disque non signalé : optimisation adaptée choisie par Windows (defrag /O)...");
                        int c; Programmes.Executer(defrag, l + ": /O /U", 6 * 3600000, null, out c);
                        Ecrire(c == 0 ? "    Optimisation terminée." : "    L'optimisation a renvoyé le code " + c + ".");
                    }
                }
                if (O.Verifier)
                {
                    Ecrire("    Analyse du système de fichiers (lecture seule, peut durer plusieurs minutes)...");
                    int c; Programmes.Executer(Path.Combine(Chemins.Systeme32, "chkdsk.exe"), l + ": /scan", 3600000, null, out c);
                    if (c == 0) Ecrire("    Aucune erreur détectée.");
                    else if (c > 0) AlerteDisque("Volume " + l + ": l'analyse signale un problème (code " + c + "). Sauvegarde tes données puis lance 'chkdsk " + l + ": /f' (un redémarrage peut être demandé).");
                    else Ecrire("    Analyse impossible.");
                }
            }

            // Optimisation automatique de Windows
            try
            {
                dynamic svc = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));
                svc.Connect();
                dynamic t = svc.GetFolder(@"\Microsoft\Windows\Defrag").GetTask("ScheduledDefrag");
                bool actif = t.Enabled;
                DateTime der = t.LastRunTime;
                Ecrire();
                Ecrire("  Optimisation automatique de Windows : " + (actif ? "activée" : "désactivée") + (der.Year > 2000 ? " (dernière exécution : " + der.ToString("dd/MM/yyyy HH:mm") + ")" : ""));
                if (!actif) Ecrire("    Elle est désactivée : réactive-la dans 'Défragmenter et optimiser les lecteurs'.");
            }
            catch { }

            // Dossiers racine les plus volumineux
            Ecrire();
            Ecrire("  Dossiers racine les plus volumineux par volume (lecture seule, peut être long) :");
            foreach (var v in volumes)
            {
                var tailles = new List<Tuple<string, long>>();
                try
                {
                    foreach (var d in new DirectoryInfo(v.Name).GetDirectories())
                    {
                        if (EstReparse(d) || d.Name.Equals("Windows", StringComparison.OrdinalIgnoreCase) || d.Name == "System Volume Information" || d.Name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase)) continue;
                        tailles.Add(Tuple.Create(d.Name, TailleFichiers.Dossier(d.FullName)));
                    }
                }
                catch { }
                Ecrire("  Volume " + v.Name.Substring(0, 2));
                foreach (var t in tailles.OrderByDescending(x => x.Item2).Take(8)) Ecrire("    " + Dr(T(t.Item2), 10) + "  " + t.Item1);
            }
            Ecrire("    (Le dossier Windows n'est pas analysé : il est géré par Windows.)");
            Ecrire();
            Ecrire("  Conseils SSD : ne défragmente jamais un SSD (Nettoyage PC ne fait que le ReTrim) ; garde 10 à 20 % d'espace libre ;");
            Ecrire("  mets à jour son firmware avec l'outil de son fabricant (Samsung Magician, Crucial Storage Executive, WD Dashboard...).");
            Ecrire();
        }

        // ------------------------------------------------------------ Anciens pilotes (nouveauté)
        private void AnciensPilotes()
        {
            Ecrire("=== Anciens pilotes ===");
            if (!Admin) { Ecrire("  Ignoré : il faut l'autorisation administrateur."); Ecrire(); return; }
            var analyse = PilotesAnciens.Analyser();
            var candidats = analyse.Where(p => p.Supprimable).ToList();
            if (O.PilotesChoisis.Count > 0) candidats = candidats.Where(p => O.PilotesChoisis.Contains(p.Inf, StringComparer.OrdinalIgnoreCase)).ToList();
            if (candidats.Count == 0) { Ecrire("  -> Aucune ancienne version de pilote à retirer : le magasin de pilotes est propre."); Ecrire(); return; }
            long total = 0; int ok = 0, gardes = 0;
            foreach (var p in candidats)
            {
                string ligne = "  " + Dr(T(p.Taille), 10) + "  " + p.Fournisseur + " - " + p.Classe + " - version " + p.Version + " (" + p.DateTexte + ")";
                if (O.Simulation) { Ecrire(ligne); total += p.Taille; ok++; continue; }
                int code;
                Programmes.Executer(Path.Combine(Chemins.Systeme32, "pnputil.exe"), "/delete-driver " + p.Inf, 120000, null, out code);
                if (code == 0) { Ecrire(ligne + " : retiré"); total += p.Taille; ok++; }
                else { Ecrire(ligne + " : gardé (Windows l'utilise encore)"); gardes++; }
            }
            if (O.Simulation) Ecrire("  -> " + ok + " ancienne(s) version(s) pourraient être retirées (" + T(total) + ").");
            else
            {
                Ecrire("  -> " + ok + " ancienne(s) version(s) retirée(s), " + T(total) + " libérés." + (gardes > 0 ? " " + gardes + " gardée(s) car encore utilisée(s)." : ""));
                resumeAutres.Add("Anciens pilotes : " + ok + " version(s) retirée(s) (" + T(total) + ")");
                totalProfond += total;
            }
            Ecrire("  (La version la plus récente de chaque pilote est toujours gardée, ainsi que celles que Windows utilise.)");
            Ecrire();
        }
    }

    /// <summary>Versions de pilotes conservées par Windows (magasin de pilotes) : lesquelles sont anciennes et inutilisées.</summary>
    internal static class PilotesAnciens
    {
        internal class Pilote
        {
            public string Inf = "", Original = "", Fournisseur = "", Classe = "", Version = "", Paquet = "";
            public DateTime Date = DateTime.MinValue;
            public long Taille;
            public bool Utilise, PlusRecent, Supprimable;
            public string DateTexte { get { return Date == DateTime.MinValue ? "date inconnue" : Date.ToString("dd/MM/yyyy"); } }
        }

        private static readonly HashSet<string> classesExclues = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Printer", "PrintQueue", "Image", "Extension", "SoftwareComponent" };

        public static List<Pilote> Analyser()
        {
            var l = new List<Pilote>();
            if (!Natif.EstWindows) return l;
            string dossierInf = Path.Combine(Chemins.Windows, "INF");
            var utilises = new HashSet<string>(Wmi.Requete("SELECT InfName FROM Win32_PnPSignedDriver").Select(o => Wmi.Texte(o, "InfName")).Where(x => x != ""), StringComparer.OrdinalIgnoreCase);
            string[] infs;
            try { infs = Directory.GetFiles(dossierInf, "oem*.inf"); } catch { return l; }
            foreach (var f in infs)
            {
                var p = new Pilote { Inf = Path.GetFileName(f) };
                try { LireInf(f, p); } catch { continue; }
                // paquet du magasin de pilotes associé (et nom d'origine du fichier)
                try
                {
                    var v = Registre.Lire(@"HKLM\SYSTEM\DriverDatabase\DriverInfFiles\" + p.Inf, "") as string[];
                    if (v != null && v.Length > 0) p.Paquet = v[0];
                }
                catch { }
                if (p.Paquet != "")
                {
                    int i = p.Paquet.IndexOf(".inf_", StringComparison.OrdinalIgnoreCase);
                    p.Original = i > 0 ? p.Paquet.Substring(0, i + 4) : p.Paquet;
                    string rep = Path.Combine(Chemins.Systeme32, @"DriverStore\FileRepository", p.Paquet);
                    if (Directory.Exists(rep)) p.Taille = TailleFichiers.Dossier(rep);
                }
                p.Utilise = utilises.Contains(p.Inf);
                l.Add(p);
            }
            // par pilote d'origine (même fichier, même fournisseur, même classe) : la version la plus récente est gardée
            foreach (var g in l.Where(x => x.Original != "").GroupBy(x => (x.Original + "|" + x.Fournisseur + "|" + x.Classe).ToLowerInvariant()))
            {
                var tries = g.OrderByDescending(x => x.Date).ThenByDescending(x => VersionNum(x.Version)).ToList();
                tries[0].PlusRecent = true;
                foreach (var x in tries.Skip(1)) x.Supprimable = !x.Utilise && !classesExclues.Contains(x.Classe) && x.Date < tries[0].Date.AddDays(1) && x.Taille > 0;
            }
            return l.OrderByDescending(x => x.Taille).ToList();
        }

        private static Version VersionNum(string v) { Version r; return Version.TryParse((v ?? "").Trim(), out r) ? r : new Version(0, 0); }

        private static void LireInf(string fichier, Pilote p)
        {
            string section = "";
            var chaines = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string fournisseur = "", classe = "", ver = "";
            foreach (var brute in File.ReadLines(fichier, DetecterEncodage(fichier)))
            {
                string ligne = brute; int c = ligne.IndexOf(';'); if (c >= 0) ligne = ligne.Substring(0, c);
                ligne = ligne.Trim(); if (ligne.Length == 0) continue;
                if (ligne.StartsWith("[")) { section = ligne.Trim('[', ']').Trim(); continue; }
                int eg = ligne.IndexOf('='); if (eg < 0) continue;
                string cle = ligne.Substring(0, eg).Trim(), val = ligne.Substring(eg + 1).Trim().Trim('"');
                if (section.Equals("Version", StringComparison.OrdinalIgnoreCase))
                {
                    if (cle.Equals("Provider", StringComparison.OrdinalIgnoreCase)) fournisseur = val;
                    else if (cle.Equals("Class", StringComparison.OrdinalIgnoreCase)) classe = val;
                    else if (cle.Equals("DriverVer", StringComparison.OrdinalIgnoreCase)) ver = val;
                }
                else if (section.Equals("Strings", StringComparison.OrdinalIgnoreCase) && !chaines.ContainsKey(cle)) chaines[cle] = val;
            }
            Func<string, string> resoudre = s => { var m = Regex.Match(s, @"^%(.+)%$"); string r; return m.Success && chaines.TryGetValue(m.Groups[1].Value, out r) ? r : s; };
            p.Fournisseur = resoudre(fournisseur); p.Classe = resoudre(classe);
            var parties = ver.Split(',');
            DateTime d;
            if (parties.Length > 0 && DateTime.TryParseExact(parties[0].Trim(), new[] { "MM/dd/yyyy", "M/d/yyyy", "MM-dd-yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) p.Date = d;
            if (parties.Length > 1) p.Version = parties[1].Trim();
        }

        private static Encoding DetecterEncodage(string f)
        {
            try
            {
                using (var s = File.OpenRead(f))
                {
                    int a = s.ReadByte(), b = s.ReadByte();
                    if (a == 0xFF && b == 0xFE) return Encoding.Unicode;
                    if (a == 0xFE && b == 0xFF) return Encoding.BigEndianUnicode;
                }
            }
            catch { }
            return Encodages.Ansi;
        }
    }
}
