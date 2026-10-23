// Nettoyage PC - mesure des performances (processeur, mémoire, disque), pour comparer avant / après un réglage
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace NettoyagePC
{
    internal class ResultatBenchmark
    {
        public string Date = "", Note = "", Disque = "";
        public double Cpu1, CpuN, Memoire, DisqueLecture, DisqueEcriture, DisqueAleatoire;   // Mo/s (CPU : Mo hachés/s), Go/s, Mo/s, IOPS
        public int RamMts;
        public Dictionary<string, object> VersJson()
        {
            return new Dictionary<string, object>
            {
                { "Date", Date }, { "Note", Note }, { "Disque", Disque }, { "Cpu1", Math.Round(Cpu1, 1) }, { "CpuN", Math.Round(CpuN, 1) }, { "Memoire", Math.Round(Memoire, 2) },
                { "DisqueLecture", Math.Round(DisqueLecture) }, { "DisqueEcriture", Math.Round(DisqueEcriture) }, { "DisqueAleatoire", Math.Round(DisqueAleatoire) }, { "RamMts", RamMts }
            };
        }
        public static ResultatBenchmark DepuisJson(Dictionary<string, object> d)
        {
            Func<string, double> v = k => { var o = Json.Valeur(d, k); try { return o == null ? 0 : Convert.ToDouble(o); } catch { return 0; } };
            return new ResultatBenchmark
            {
                Date = Json.Texte(d, "Date"), Note = Json.Texte(d, "Note"), Disque = Json.Texte(d, "Disque"), Cpu1 = v("Cpu1"), CpuN = v("CpuN"), Memoire = v("Memoire"),
                DisqueLecture = v("DisqueLecture"), DisqueEcriture = v("DisqueEcriture"), DisqueAleatoire = v("DisqueAleatoire"), RamMts = (int)v("RamMts")
            };
        }
    }

    internal static class Benchmark
    {
        public static string Points(double mo) { return Format.Nombre(mo * 10) + " pts"; }

        /// <summary>Dossier de test sur le disque choisi (le dossier temporaire sur le disque de Windows).</summary>
        public static string DossierTest(string racine)
        {
            string dossier = racine == Path.GetPathRoot(Path.GetTempPath()) ? Path.GetTempPath() : Path.Combine(racine, "NettoyagePC-test");
            try { Directory.CreateDirectory(dossier); } catch { dossier = Path.GetTempPath(); }
            return dossier;
        }

        /// <summary>Comparaison de la dernière mesure avec la précédente (texte non traduit d'un bloc).</summary>
        public static string Comparaison(List<ResultatBenchmark> h)
        {
            if (h.Count == 0) return "Aucune mesure pour l'instant. Astuce : fais une mesure AVANT un réglage (par exemple l'EXPO), puis une APRÈS : la différence s'affiche ici.";
            var der = h[h.Count - 1];
            if (h.Count == 1) return "Première mesure enregistrée : refais-en une après un réglage pour voir la différence.";
            var ref0 = h[h.Count - 2];
            Func<double, double, string> pct = (a, b) => b <= 0 ? "—" : ((a - b) / b * 100).ToString("+0;-0;0", Appli.Fr) + " %";
            return "Par rapport à la mesure précédente (" + ref0.Date + (ref0.Note != "" ? ", " + ref0.Note : "") + ") :   processeur " + pct(der.CpuN, ref0.CpuN) +
                "   ·   mémoire " + pct(der.Memoire, ref0.Memoire) + "   ·   disque " + pct(der.DisqueLecture, ref0.DisqueLecture) +
                (Math.Abs(der.Memoire - ref0.Memoire) / Math.Max(0.1, ref0.Memoire) > 0.1 && der.RamMts != ref0.RamMts && der.RamMts > 0 ? "\nLa mémoire est passée de " + ref0.RamMts + " à " + der.RamMts + " MT/s." : "") +
                "\n(Écarts de moins de 3 % : variation normale d'une mesure à l'autre.)";
        }

        public static string Fichier { get { return Chemins.Fichier("benchmarks.json"); } }
        public static volatile bool Arret;

        public static List<ResultatBenchmark> Historique()
        {
            return Json.Objets(Json.LireFichier(Fichier)).Select(ResultatBenchmark.DepuisJson).ToList();
        }

        public static void Enregistrer(ResultatBenchmark r)
        {
            var l = Historique(); l.Add(r);
            FichierSur.EcrireInterface(Fichier, Json.Ecrire(l.Skip(Math.Max(0, l.Count - 50)).Select(x => (object)x.VersJson()).ToArray()));
        }

        /// <summary>Lance toutes les mesures (environ 40 secondes). etape(texte, pourcentage).</summary>
        public static ResultatBenchmark Lancer(string dossierDisque, Action<string, int> etape)
        {
            Arret = false;
            var r = new ResultatBenchmark { Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Disque = Path.GetPathRoot(dossierDisque) ?? "" };
            try { r.RamMts = Barrette.VitesseReelle(Barrette.Lister()); } catch { }
            etape("Processeur (1 cœur)...", 5); r.Cpu1 = Processeur(1, 4000);
            if (Arret) return null;
            etape("Processeur (tous les cœurs)...", 20); r.CpuN = Processeur(Environment.ProcessorCount, 4000);
            if (Arret) return null;
            etape("Mémoire vive...", 40); r.Memoire = Memoire(3000);
            if (Arret) return null;
            etape("Disque...", 55);
            double lec, ecr, alea;
            Disque(dossierDisque, out lec, out ecr, out alea, p => etape("Disque...", 55 + p * 45 / 100));
            r.DisqueLecture = lec; r.DisqueEcriture = ecr; r.DisqueAleatoire = alea;
            etape("Terminé", 100);
            return Arret ? null : r;
        }

        /// <summary>Débit de hachage SHA-256 (calcul pur, en Mo/s) sur n fils.</summary>
        private static double Processeur(int fils, int ms)
        {
            long total = 0;
            var chrono = Stopwatch.StartNew();
            var t = Enumerable.Range(0, fils).Select(i => new Thread(() =>
            {
                var donnees = new byte[64 * 1024]; new Random(i).NextBytes(donnees);
                long n = 0;
                using (var h = new SHA256Managed())
                    while (chrono.ElapsedMilliseconds < ms && !Arret) { h.ComputeHash(donnees); n += donnees.Length; }
                Interlocked.Add(ref total, n);
            }) { IsBackground = true, Priority = ThreadPriority.AboveNormal }).ToList();
            t.ForEach(x => x.Start()); t.ForEach(x => x.Join());
            return total / 1048576.0 / (chrono.ElapsedMilliseconds / 1000.0);
        }

        /// <summary>Bande passante mémoire (copie de blocs de 64 Mo sur plusieurs fils), en Go/s.</summary>
        private static double Memoire(int ms)
        {
            ulong tot, dispo; Natif.Memoire(out tot, out dispo);
            int fils = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
            int taille = 64 * 1048576;
            if (dispo > 0 && (ulong)fils * 2UL * (ulong)taille > dispo / 2) fils = Math.Max(1, (int)(dispo / 4 / (ulong)taille / 2));
            long total = 0;
            var chrono = Stopwatch.StartNew();
            var t = Enumerable.Range(0, fils).Select(i => new Thread(() =>
            {
                var a = new byte[taille]; var b = new byte[taille];
                for (int k = 0; k < taille; k += 4096) a[k] = (byte)k;
                long n = 0;
                while (chrono.ElapsedMilliseconds < ms && !Arret) { Buffer.BlockCopy(a, 0, b, 0, taille); n += taille; }
                Interlocked.Add(ref total, n);
            }) { IsBackground = true }).ToList();
            t.ForEach(x => x.Start()); t.ForEach(x => x.Join());
            GC.Collect();
            return total / 1073741824.0 / (chrono.ElapsedMilliseconds / 1000.0);
        }

        // --- Disque : lecture et écriture SANS le cache de Windows (comme les logiciels de test de disque)
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] private static extern SafeFileHandle CreateFile(string nom, uint acces, uint partage, IntPtr secu, uint creation, uint options, IntPtr modele);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool WriteFile(SafeFileHandle h, IntPtr tampon, uint taille, out uint ecrits, IntPtr chevauche);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool ReadFile(SafeFileHandle h, IntPtr tampon, uint taille, out uint lus, IntPtr chevauche);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool SetFilePointerEx(SafeFileHandle h, long distance, out long nouvelle, uint methode);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr VirtualAlloc(IntPtr adresse, UIntPtr taille, uint type, uint protection);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern bool VirtualFree(IntPtr adresse, UIntPtr taille, uint type);

        private const uint GENERIC_READ = 0x80000000, GENERIC_WRITE = 0x40000000, CREATE_ALWAYS = 2, OPEN_EXISTING = 3;
        private const uint FILE_FLAG_NO_BUFFERING = 0x20000000, FILE_FLAG_WRITE_THROUGH = 0x80000000, FILE_FLAG_DELETE_ON_CLOSE = 0x04000000, FILE_ATTRIBUTE_TEMPORARY = 0x100;

        private static void Disque(string dossier, out double lecture, out double ecriture, out double aleatoire, Action<int> progression)
        {
            lecture = ecriture = aleatoire = 0;
            const int bloc = 8 * 1048576;
            long tailleFichier = 1024L * 1048576;
            try { var di = new DriveInfo(Path.GetPathRoot(dossier)); if (di.AvailableFreeSpace < tailleFichier * 3) tailleFichier = Math.Max(128L * 1048576, di.AvailableFreeSpace / 6 / bloc * bloc); } catch { }
            string f = Path.Combine(dossier, "np-test-disque-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".tmp");
            if (!Natif.EstWindows) { DisqueSimple(f, tailleFichier, bloc, out lecture, out ecriture); aleatoire = 0; return; }
            IntPtr tampon = VirtualAlloc(IntPtr.Zero, (UIntPtr)bloc, 0x3000, 0x04);
            if (tampon == IntPtr.Zero) return;
            try
            {
                var alea = new byte[bloc]; new Random(42).NextBytes(alea); Marshal.Copy(alea, 0, tampon, bloc);
                // écriture séquentielle
                using (var h = CreateFile(f, GENERIC_WRITE, 0, IntPtr.Zero, CREATE_ALWAYS, FILE_FLAG_NO_BUFFERING | FILE_FLAG_WRITE_THROUGH | FILE_ATTRIBUTE_TEMPORARY, IntPtr.Zero))
                {
                    if (h.IsInvalid) return;
                    var c = Stopwatch.StartNew(); long fait = 0; uint n;
                    while (fait < tailleFichier && !Arret) { if (!WriteFile(h, tampon, bloc, out n, IntPtr.Zero)) break; fait += n; progression((int)(30 * fait / tailleFichier)); }
                    ecriture = fait / 1048576.0 / Math.Max(0.001, c.Elapsed.TotalSeconds);
                }
                // lecture séquentielle
                using (var h = CreateFile(f, GENERIC_READ, 1, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_NO_BUFFERING, IntPtr.Zero))
                {
                    if (h.IsInvalid) return;
                    var c = Stopwatch.StartNew(); long fait = 0; uint n;
                    while (fait < tailleFichier && !Arret) { if (!ReadFile(h, tampon, bloc, out n, IntPtr.Zero) || n == 0) break; fait += n; progression(30 + (int)(30 * fait / tailleFichier)); }
                    lecture = fait / 1048576.0 / Math.Max(0.001, c.Elapsed.TotalSeconds);
                }
                // lectures aléatoires de 4 Ko (une à la fois), 4 secondes ; le fichier est supprimé à la fermeture
                using (var h = CreateFile(f, GENERIC_READ, 1 | 4, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_NO_BUFFERING | FILE_FLAG_DELETE_ON_CLOSE, IntPtr.Zero))
                {
                    if (h.IsInvalid) return;
                    var rnd = new Random(7); long blocs4k = tailleFichier / 4096, ops = 0; long pos; uint n;
                    var c = Stopwatch.StartNew();
                    while (c.ElapsedMilliseconds < 4000 && !Arret)
                    {
                        long b = (long)(rnd.NextDouble() * blocs4k) * 4096;
                        if (!SetFilePointerEx(h, b, out pos, 0) || !ReadFile(h, tampon, 4096, out n, IntPtr.Zero)) break;
                        ops++;
                        if ((ops & 255) == 0) progression(60 + (int)(40 * c.ElapsedMilliseconds / 4000));
                    }
                    aleatoire = ops / Math.Max(0.001, c.Elapsed.TotalSeconds);
                }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("test du disque", ex); }
            finally
            {
                VirtualFree(tampon, UIntPtr.Zero, 0x8000);
                try { if (File.Exists(f)) File.Delete(f); } catch { }
            }
        }

        /// <summary>(Hors Windows, pour les essais) mesure simple avec le cache.</summary>
        private static void DisqueSimple(string f, long taille, int bloc, out double lecture, out double ecriture)
        {
            var o = new byte[bloc];
            var c = Stopwatch.StartNew();
            using (var s = new FileStream(f, FileMode.Create, FileAccess.Write, FileShare.None, 1, FileOptions.WriteThrough)) { for (long k = 0; k < taille; k += bloc) s.Write(o, 0, bloc); s.Flush(true); }
            ecriture = taille / 1048576.0 / c.Elapsed.TotalSeconds;
            c.Restart();
            using (var s = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.DeleteOnClose)) while (s.Read(o, 0, bloc) > 0) { }
            lecture = taille / 1048576.0 / c.Elapsed.TotalSeconds;
        }
    }
}
