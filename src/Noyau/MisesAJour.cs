// Nettoyage PC - mises à jour de l'application (GitHub Releases), téléchargement vérifié, installation
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class VersionPubliee
    {
        public string Version = "", Notes = "", UrlSetup = "", UrlHash = "", Page = "", NomSetup = "";
        public DateTime? Date;
    }

    internal static class MisesAJour
    {
        public static string Depot
        {
            get
            {
                // mise-a-jour.json à côté du programme (même format que la version 3)
                var d = Json.LireFichier(Path.Combine(Chemins.Ici, "mise-a-jour.json")) as Dictionary<string, object>;
                string depot = Json.Texte(d, "depot");
                return Regex.IsMatch(depot, @"^[\w.-]+/[\w.-]+$") ? depot : Appli.Depot;
            }
        }

        private static void PreparerTls()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072 | (SecurityProtocolType)12288; }
            catch { try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { } }
        }

        private static WebClient Client()
        {
            PreparerTls();
            var w = new WebClient { Encoding = Encoding.UTF8 };
            w.Headers[HttpRequestHeader.UserAgent] = "NettoyagePC/" + Appli.Version;
            w.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
            return w;
        }

        public static int Comparer(string a, string b)
        {
            Version va, vb;
            if (!Version.TryParse(Normaliser(a), out va)) va = new Version(0, 0);
            if (!Version.TryParse(Normaliser(b), out vb)) vb = new Version(0, 0);
            return va.CompareTo(vb);
        }
        private static string Normaliser(string v) { v = (v ?? "").Trim(); return v.Contains('.') ? v : v + ".0"; }

        /// <summary>Versions publiées, la plus récente d'abord. Exception si GitHub ne répond pas.</summary>
        public static List<VersionPubliee> Verifier()
        {
            string json;
            using (var w = Client()) json = w.DownloadString("https://api.github.com/repos/" + Depot + "/releases?per_page=15");
            var l = new List<VersionPubliee>();
            foreach (var r in Json.Objets(Json.Lire(json)))
            {
                if (Json.Bool(r, "draft") || Json.Bool(r, "prerelease")) continue;
                var assets = Json.Objets(Json.Valeur(r, "assets"));
                var setup = assets.FirstOrDefault(a => Regex.IsMatch(Json.Texte(a, "name"), @"^NettoyagePC-Setup-[\d.]+\.exe$", RegexOptions.IgnoreCase));
                Dictionary<string, object> hash = null;
                if (setup != null) { string nh = Regex.Replace(Json.Texte(setup, "name"), @"\.exe$", ".sha256", RegexOptions.IgnoreCase); hash = assets.FirstOrDefault(a => Json.Texte(a, "name") == nh); }
                string num = null;
                foreach (var source in new[] { Json.Texte(r, "tag_name"), Json.Texte(r, "name"), setup == null ? "" : Json.Texte(setup, "name") })
                {
                    var m = Regex.Match(source, @"(\d+\.\d+(\.\d+){0,2})");
                    if (m.Success) { num = m.Groups[1].Value; break; }
                }
                if (num == null) continue;
                var v = new VersionPubliee
                {
                    Version = num, Notes = Json.Texte(r, "body"), Page = Json.Texte(r, "html_url"),
                    UrlSetup = setup == null ? "" : Json.Texte(setup, "browser_download_url"), NomSetup = setup == null ? "" : Json.Texte(setup, "name"),
                    UrlHash = hash == null ? "" : Json.Texte(hash, "browser_download_url")
                };
                DateTime d; if (DateTime.TryParse(Json.Texte(r, "published_at"), null, System.Globalization.DateTimeStyles.RoundtripKind, out d)) v.Date = d.ToLocalTime();
                l.Add(v);
            }
            try { FichierSur.EcrireInterface(Chemins.Fichier("derniere-verification-maj.txt"), DateTime.Now.ToString("yyyy-MM-dd")); } catch { }
            return l.OrderByDescending(x => x, Comparer<VersionPubliee>.Create((a, b) => Comparer(a.Version, b.Version))).ToList();
        }

        public static bool DejaVerifieAujourdhui()
        {
            try { string f = Chemins.Fichier("derniere-verification-maj.txt"); return File.Exists(f) && File.ReadAllText(f).Trim() == DateTime.Now.ToString("yyyy-MM-dd"); }
            catch { return false; }
        }

        private static bool UrlOfficielle(string u)
        {
            Uri uri;
            return Uri.TryCreate(u, UriKind.Absolute, out uri) && uri.Scheme == "https" && Regex.IsMatch(uri.Host, @"(^|\.)(github\.com|githubusercontent\.com)$", RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// Télécharge l'installateur et vérifie son empreinte SHA256 (obligatoire). Renvoie le fichier et l'empreinte
        /// attendue ; exception avec un message clair sinon.
        /// </summary>
        public static string Telecharger(VersionPubliee v, out string empreinte)
        {
            empreinte = null;
            if (string.IsNullOrEmpty(v.UrlHash) || !UrlOfficielle(v.UrlSetup) || !UrlOfficielle(v.UrlHash) || !Regex.IsMatch(v.Version, @"^\d+(\.\d+){1,3}$"))
                throw new InvalidOperationException("cette version ne peut pas être installée automatiquement : son empreinte de sécurité (fichier .sha256) est absente ou elle ne vient pas de la page officielle.");
            string dossier = Path.Combine(Path.GetTempPath(), "NettoyagePC-maj-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(dossier);
            string cible = Path.Combine(dossier, "NettoyagePC-Setup-" + v.Version + ".exe");
            string attendu;
            using (var w = Client())
            {
                w.DownloadFile(v.UrlSetup, cible);
                attendu = (w.DownloadString(v.UrlHash).Trim().Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "").ToUpperInvariant();
            }
            string obtenu = Moteur.Empreinte(cible);
            if (!Regex.IsMatch(attendu, "^[A-F0-9]{64}$") || attendu != obtenu)
            {
                try { File.Delete(cible); } catch { }
                throw new InvalidOperationException("le fichier téléchargé ne correspond pas à l'empreinte publiée (téléchargement corrompu ou modifié). Mise à jour annulée par sécurité.");
            }
            empreinte = attendu;
            return cible;
        }
    }

    internal static class Installation
    {
        /// <summary>
        /// (Avec l'autorisation administrateur) copie l'installateur dans Program Files, le revérifie là-bas, puis le lance
        /// en silencieux. Seule la copie revérifiée est exécutée, jamais celle du dossier temporaire.
        /// </summary>
        public static int InstallerEnAdmin(string fichier, string empreinte)
        {
            if (!Regex.IsMatch(empreinte ?? "", "^[A-F0-9]{64}$") || !File.Exists(fichier)) return 3;
            string nom = Path.GetFileName(fichier);
            if (!Regex.IsMatch(nom, @"^NettoyagePC-Setup-\d+(\.\d+){1,3}\.exe$")) return 3;
            string dst = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Nettoyage PC", "maj");
            Directory.CreateDirectory(dst);
            string f = Path.Combine(dst, nom);
            if (File.Exists(f)) File.Delete(f);
            File.Copy(fichier, f);
            if (Moteur.Empreinte(f) != empreinte) { try { File.Delete(f); } catch { } return 4; }
            Process.Start(new ProcessStartInfo(f, "/S") { UseShellExecute = false });
            return 0;
        }
    }
}
