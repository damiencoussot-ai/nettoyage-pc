// Nettoyage PC - « Preuve en FPS » (Premium) : mesure des images par seconde réelles d'un jeu avec PresentMon
// (outil libre d'Intel, github.com/GameTechDev/PresentMon), avant / après un réglage.
// PresentMon est téléchargé depuis sa page GitHub officielle, vérifié (empreinte SHA256 publiée par GitHub, ou signature
// numérique d'Intel), puis copié dans le dossier protégé de Nettoyage PC (Program Files) où il est revérifié avant chaque
// mesure. Il ne lit que le rythme des images affichées : rien d'autre n'est enregistré.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class MesureFps
    {
        public string Date = "", Jeu = "", Libelle = "";
        public int Secondes, Images;
        public double Moyenne, Bas1, Bas01, Saccades;

        public Dictionary<string, object> VersJson()
        {
            return new Dictionary<string, object> { { "Date", Date }, { "Jeu", Jeu }, { "Libelle", Libelle }, { "Secondes", Secondes }, { "Images", Images }, { "Moyenne", Math.Round(Moyenne, 1) }, { "Bas1", Math.Round(Bas1, 1) }, { "Bas01", Math.Round(Bas01, 1) }, { "Saccades", Saccades } };
        }
        public static MesureFps DepuisJson(Dictionary<string, object> d)
        {
            return new MesureFps { Date = Json.Texte(d, "Date"), Jeu = Json.Texte(d, "Jeu"), Libelle = Json.Texte(d, "Libelle"), Secondes = (int)Json.Long(d, "Secondes"), Images = (int)Json.Long(d, "Images"), Moyenne = Json.Double(d, "Moyenne"), Bas1 = Json.Double(d, "Bas1"), Bas01 = Json.Double(d, "Bas01"), Saccades = Json.Double(d, "Saccades") };
        }
    }

    internal static class Fps
    {
        private const string Depot = "GameTechDev/PresentMon";
        private static readonly Regex NomOfficiel = new Regex(@"^PresentMon-\d+(\.\d+){1,3}-x64\.exe$", RegexOptions.IgnoreCase);
        public static string DossierOutils { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nettoyage PC", "outils"); } }
        public static string Exe { get { return Path.Combine(DossierOutils, "PresentMon.exe"); } }
        public static string Csv { get { return Path.Combine(DossierOutils, "mesures", "derniere.csv"); } }
        private static string FichierHistorique { get { return Path.Combine(Chemins.Local, "fps.json"); } }

        public static bool OutilPresent { get { try { return File.Exists(Exe); } catch { return false; } } }

        // ------------------------------------------------------------ Téléchargement (droits normaux)
        private static WebClient Client()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            var w = new WebClient { Encoding = Encoding.UTF8 };
            w.Headers[HttpRequestHeader.UserAgent] = "NettoyagePC/" + Appli.Version;
            w.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return w;
        }

        private static bool UrlOfficielle(string u)
        {
            Uri uri;
            return Uri.TryCreate(u, UriKind.Absolute, out uri) && uri.Scheme == "https" && Regex.IsMatch(uri.Host, @"(^|\.)(github\.com|githubusercontent\.com)$", RegexOptions.IgnoreCase);
        }

        /// <summary>Fichiers officiels de PresentMon (nom, adresse, empreinte SHA256 publiée par GitHub ou vide), le plus récent d'abord.</summary>
        public static List<Tuple<string, string, string>> Officiels()
        {
            string json;
            using (var w = Client()) json = w.DownloadString("https://api.github.com/repos/" + Depot + "/releases?per_page=10");
            var l = new List<Tuple<string, string, string>>();
            foreach (var r in Json.Objets(Json.Lire(json)))
            {
                if (Json.Bool(r, "draft") || Json.Bool(r, "prerelease")) continue;
                foreach (var a in Json.Objets(Json.Valeur(r, "assets")))
                {
                    string nom = Json.Texte(a, "name"), url = Json.Texte(a, "browser_download_url"), dg = Json.Texte(a, "digest");
                    if (!NomOfficiel.IsMatch(nom) || !UrlOfficielle(url)) continue;
                    var m = Regex.Match(dg, "^sha256:([0-9a-fA-F]{64})$");
                    l.Add(Tuple.Create(nom, url, m.Success ? m.Groups[1].Value.ToUpperInvariant() : ""));
                }
            }
            return l;
        }

        /// <summary>Télécharge la dernière version de PresentMon et la vérifie. Renvoie le fichier (dossier temporaire) et son empreinte.</summary>
        public static string Telecharger(out string empreinte, out string version)
        {
            empreinte = null; version = "";
            var o = Officiels().FirstOrDefault();
            if (o == null) throw new InvalidOperationException(Langue.T("la page officielle de PresentMon ne propose pas de version pour ce PC."));
            version = Regex.Match(o.Item1, @"\d+(\.\d+){1,3}").Value;
            string dossier = Path.Combine(Path.GetTempPath(), "NettoyagePC-presentmon-" + Guid.NewGuid().ToString("N").Substring(0, 10));
            Directory.CreateDirectory(dossier);
            string f = Path.Combine(dossier, o.Item1);
            using (var w = Client()) w.DownloadFile(o.Item2, f);
            string h = Moteur.Empreinte(f);
            bool ok = o.Item3 != "" ? h == o.Item3 : SigneParIntel(f);
            if (!ok)
            {
                try { File.Delete(f); } catch { }
                throw new InvalidOperationException(Langue.T("le fichier téléchargé ne correspond pas à la version officielle (téléchargement corrompu ou modifié). Rien n'a été installé."));
            }
            empreinte = h;
            return f;
        }

        // ------------------------------------------------------------ Signature numérique (Authenticode)
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WINTRUST_FILE_INFO { public uint cbStruct; [MarshalAs(UnmanagedType.LPWStr)] public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WINTRUST_DATA
        {
            public uint cbStruct; public IntPtr pPolicyCallbackData, pSIPClientData; public uint dwUIChoice, fdwRevocationChecks, dwUnionChoice; public IntPtr pFile;
            public uint dwStateAction; public IntPtr hWVTStateData, pwszURLReference; public uint dwProvFlags, dwUIContext; public IntPtr pSignatureSettings;
        }
        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        private static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, IntPtr donnees);

        /// <summary>Signature numérique valide d'Intel Corporation.</summary>
        public static bool SigneParIntel(string fichier)
        {
            if (!Natif.EstWindows) return false;
            IntPtr pFichier = IntPtr.Zero, pDonnees = IntPtr.Zero;
            try
            {
                var fi = new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)), pcwszFilePath = fichier };
                pFichier = Marshal.AllocHGlobal(Marshal.SizeOf(fi)); Marshal.StructureToPtr(fi, pFichier, false);
                var d = new WINTRUST_DATA { cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)), dwUIChoice = 2, fdwRevocationChecks = 0, dwUnionChoice = 1, pFile = pFichier, dwProvFlags = 0x1000 };
                pDonnees = Marshal.AllocHGlobal(Marshal.SizeOf(d)); Marshal.StructureToPtr(d, pDonnees, false);
                if (WinVerifyTrust(IntPtr.Zero, new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE"), pDonnees) != 0) return false;
                var c = X509Certificate.CreateFromSignedFile(fichier);
                return Regex.IsMatch(c.Subject ?? "", @"(^|,\s*)O=Intel Corporation(,|$)", RegexOptions.IgnoreCase);
            }
            catch { return false; }
            finally
            {
                if (pDonnees != IntPtr.Zero) { Marshal.DestroyStructure(pDonnees, typeof(WINTRUST_DATA)); Marshal.FreeHGlobal(pDonnees); }
                if (pFichier != IntPtr.Zero) { Marshal.DestroyStructure(pFichier, typeof(WINTRUST_FILE_INFO)); Marshal.FreeHGlobal(pFichier); }
            }
        }

        // ------------------------------------------------------------ Mesure (autorisation administrateur)
        /// <summary>
        /// mesurer-fps source|- empreinte processus.exe secondes délai : installe PresentMon dans Program Files si besoin
        /// (copie revérifiée : empreinte identique à un fichier officiel publié sur GitHub, ou signature d'Intel), puis mesure.
        /// </summary>
        public static int MesurerEnAdmin(string[] args)
        {
            if (args.Length < 6) return 3;
            string source = args[1], empreinte = (args[2] ?? "").ToUpperInvariant(), processus = args[3];
            int secondes, delai;
            if (!Regex.IsMatch(empreinte, "^[A-F0-9]{64}$") || !Regex.IsMatch(processus, @"^[\w .()+\-]{1,100}\.exe$") || processus.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return 3;
            if (!int.TryParse(args[4], out secondes) || secondes < 5 || secondes > 600 || !int.TryParse(args[5], out delai) || delai < 0 || delai > 60) return 3;
            Directory.CreateDirectory(DossierOutils);
            if (!File.Exists(Exe) || Moteur.Empreinte(Exe) != empreinte)
            {
                if (source == "-" || !File.Exists(source) || !NomOfficiel.IsMatch(Path.GetFileName(source))) return 3;
                string tmp = Path.Combine(DossierOutils, "PresentMon.nouveau");
                if (File.Exists(tmp)) File.Delete(tmp);
                File.Copy(source, tmp);
                bool officiel = false;
                if (Moteur.Empreinte(tmp) == empreinte)
                {
                    try { officiel = Officiels().Any(o => o.Item3 == empreinte); } catch { }
                    if (!officiel) officiel = SigneParIntel(tmp);
                }
                if (!officiel) { try { File.Delete(tmp); } catch { } return 4; }
                if (File.Exists(Exe)) File.Delete(Exe);
                File.Move(tmp, Exe);
                if (Moteur.Empreinte(Exe) != empreinte) { try { File.Delete(Exe); } catch { } return 4; }
            }
            string dossierCsv = Path.GetDirectoryName(Csv);
            Directory.CreateDirectory(dossierCsv);
            if (File.Exists(Csv)) File.Delete(Csv);
            var psi = new ProcessStartInfo(Exe, Elevation.Joindre(new[]
            {
                "--process_name", processus, "--output_file", Csv, "--timed", secondes.ToString(CultureInfo.InvariantCulture), "--terminate_after_timed",
                "--delay", delai.ToString(CultureInfo.InvariantCulture), "--session_name", "NettoyagePC", "--stop_existing_session", "--no_console_stats"
            })) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = DossierOutils };
            using (var p = Process.Start(psi))
            {
                if (!p.WaitForExit((secondes + delai + 45) * 1000)) { try { p.Kill(); } catch { } }
            }
            return File.Exists(Csv) && new FileInfo(Csv).Length > 0 ? 0 : 5;
        }

        // ------------------------------------------------------------ Résultats
        /// <summary>Calcule les images par seconde à partir du fichier de PresentMon (la chaîne d'affichage la plus active du jeu).</summary>
        public static MesureFps Analyser(string fichier)
        {
            var lignes = File.ReadAllLines(fichier);
            if (lignes.Length < 3) return null;
            var entete = lignes[0].Split(',').Select(x => x.Trim()).ToList();
            int iTemps = -1;
            foreach (var n in new[] { "MsBetweenPresents", "FrameTime", "MsBetweenDisplayChange" })
            {
                iTemps = entete.FindIndex(x => x.Equals(n, StringComparison.OrdinalIgnoreCase));
                if (iTemps >= 0) break;
            }
            int iApp = entete.IndexOf("Application"), iChaine = entete.IndexOf("SwapChainAddress"), iPid = entete.IndexOf("ProcessID");
            if (iTemps < 0) return null;
            var parChaine = new Dictionary<string, List<double>>();
            string nomJeu = "";
            foreach (var l in lignes.Skip(1))
            {
                var c = l.Split(',');
                if (c.Length <= iTemps) continue;
                double ms;
                if (!double.TryParse(c[iTemps], NumberStyles.Float, CultureInfo.InvariantCulture, out ms) || ms <= 0 || ms > 5000) continue;
                string cle = (iPid >= 0 && iPid < c.Length ? c[iPid] : "") + "|" + (iChaine >= 0 && iChaine < c.Length ? c[iChaine] : "");
                List<double> v; if (!parChaine.TryGetValue(cle, out v)) { v = new List<double>(); parChaine[cle] = v; }
                v.Add(ms);
                if (nomJeu == "" && iApp >= 0 && iApp < c.Length) nomJeu = c[iApp];
            }
            if (parChaine.Count == 0) return null;
            var t = parChaine.OrderByDescending(kv => kv.Value.Count).First().Value;
            if (t.Count < 20) return null;
            double total = t.Sum();
            var tri = t.OrderByDescending(x => x).ToList();
            Func<double, double> bas = part => { int n = Math.Max(1, (int)Math.Ceiling(tri.Count * part)); return 1000.0 / tri.Take(n).Average(); };
            var med = t.OrderBy(x => x).ElementAt(t.Count / 2);
            return new MesureFps
            {
                Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm"), Jeu = Path.GetFileNameWithoutExtension(nomJeu), Images = t.Count, Secondes = (int)Math.Round(total / 1000),
                Moyenne = 1000.0 * t.Count / total, Bas1 = bas(0.01), Bas01 = bas(0.001), Saccades = t.Count(x => x > Math.Max(2.5 * med, med + 12))
            };
        }

        public static List<MesureFps> Historique()
        {
            return Json.Objets(Json.LireFichier(FichierHistorique)).Select(MesureFps.DepuisJson).OrderByDescending(m => m.Date).ToList();
        }

        public static void Enregistrer(List<MesureFps> l)
        {
            try { Directory.CreateDirectory(Chemins.Local); File.WriteAllText(FichierHistorique, Json.Ecrire(l.OrderByDescending(m => m.Date).Take(300).Select(m => (object)m.VersJson()).ToArray())); }
            catch (Exception ex) { JournalDemarrage.Erreur("historique des FPS", ex); }
        }
    }
}
