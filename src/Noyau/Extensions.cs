// Nettoyage PC - extensions des navigateurs (lecture seule) : liste, permissions sensibles, origine (boutique ou non).
// Partagé par la version 4 et la version 5. Rien n'est modifié : la suppression se fait dans le navigateur.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class Extension
    {
        public string Navigateur = "", Profil = "", Nom = "", Version = "", Id = "", Page = "";
        public bool TousSites, Boutique;
        public List<string> Risques = new List<string>();
        public DateTime? Date;
    }

    internal static class ExtensionsNavigateurs
    {
        private static readonly Dictionary<string, string> Sensibles = new Dictionary<string, string>
        {
            { "webRequest", "surveiller ta navigation" }, { "webRequestBlocking", "modifier ta navigation" }, { "cookies", "lire les cookies" }, { "history", "lire l'historique" },
            { "debugger", "contrôler les pages" }, { "nativeMessaging", "parler à des programmes du PC" }, { "proxy", "changer la connexion" }, { "management", "gérer les autres extensions" },
            { "clipboardRead", "lire le presse-papiers" }, { "downloads", "gérer les téléchargements" }, { "tabs", "voir tes onglets" }, { "scripting", "injecter du code dans les pages" }
        };
        private static readonly Regex TousLesSites = new Regex(@"^(<all_urls>|\*://\*/\*|https?://\*/\*)$");

        /// <summary>Avis sur une extension. Niveau : 0 à vérifier, 1 accès étendu, 2 OK.</summary>
        public static string Avis(Extension x, out int niveau)
        {
            var details = new List<string>();
            if (!x.Boutique) details.Add("installée hors de la boutique officielle");
            if (x.TousSites) details.Add("peut lire et modifier tous les sites");
            if (x.Risques.Count > 0) details.Add("peut " + string.Join(", ", x.Risques));
            niveau = 2; string avis = "OK";
            if (!x.Boutique || (x.TousSites && x.Risques.Count > 0)) { niveau = 0; avis = "À vérifier : " + string.Join(" ; ", details); }
            else if (details.Count > 0) { niveau = 1; avis = "Accès étendu : " + string.Join(" ; ", details); }
            return avis;
        }

        private static string NomLocal(string dossier, string brut, string defaut)
        {
            var m = Regex.Match(brut ?? "", "^__MSG_(.+)__$");
            if (!m.Success) return brut ?? "";
            foreach (var loc in new[] { "fr", defaut, "en", "en_US" })
            {
                if (string.IsNullOrEmpty(loc)) continue;
                var j = Json.LireFichier(Path.Combine(dossier, "_locales", loc, "messages.json")) as Dictionary<string, object>;
                if (j == null) continue;
                foreach (var kv in j) if (kv.Key.Equals(m.Groups[1].Value, StringComparison.OrdinalIgnoreCase)) { string t = Json.Texte(kv.Value as Dictionary<string, object>, "message"); if (t != "") return t; }
            }
            return brut;
        }

        private static List<string> Chaines(object o)
        {
            var r = new List<string>();
            var a = o as System.Collections.IEnumerable;
            if (a == null || o is string) return r;
            foreach (var x in a) if (x is string) r.Add((string)x);
            return r;
        }

        /// <summary>Extensions de Chrome, Edge, Brave, Vivaldi (tous les profils) et Firefox. Lecture seule.</summary>
        public static List<Extension> Lister()
        {
            var res = new List<Extension>();
            string la = Chemins.LocalAppData;
            var navs = new[]
            {
                new[] { "Chrome", Path.Combine(la, @"Google\Chrome\User Data"), "https://chromewebstore.google.com/detail/{0}" },
                new[] { "Edge", Path.Combine(la, @"Microsoft\Edge\User Data"), "https://microsoftedge.microsoft.com/addons/detail/{0}" },
                new[] { "Brave", Path.Combine(la, @"BraveSoftware\Brave-Browser\User Data"), "https://chromewebstore.google.com/detail/{0}" },
                new[] { "Vivaldi", Path.Combine(la, @"Vivaldi\User Data"), "https://chromewebstore.google.com/detail/{0}" }
            };
            foreach (var n in navs)
            {
                if (!Directory.Exists(n[1])) continue;
                var noms = new Dictionary<string, string>();
                try
                {
                    var ls = Json.LireFichier(Path.Combine(n[1], "Local State")) as Dictionary<string, object>;
                    var cache = Json.Dict(Json.Dict(ls, "profile"), "info_cache");
                    if (cache != null) foreach (var kv in cache) noms[kv.Key] = Json.Texte(kv.Value as Dictionary<string, object>, "name");
                }
                catch { }
                foreach (var prof in Directory.GetDirectories(n[1]).Where(d => { string x = Path.GetFileName(d); return x == "Default" || x.StartsWith("Profile "); }))
                {
                    string nomProfil = Path.GetFileName(prof); string np; if (noms.TryGetValue(nomProfil, out np) && np != "") nomProfil = np;
                    string dext = Path.Combine(prof, "Extensions");
                    if (!Directory.Exists(dext)) continue;
                    foreach (var ext in Directory.GetDirectories(dext).Where(d => Path.GetFileName(d) != "Temp"))
                    {
                        try
                        {
                            var ver = new DirectoryInfo(ext).GetDirectories().OrderByDescending(d => d.LastWriteTime).FirstOrDefault();
                            if (ver == null) continue;
                            var m = Json.LireFichier(Path.Combine(ver.FullName, "manifest.json")) as Dictionary<string, object>;
                            if (m == null || m.ContainsKey("theme")) continue;
                            var perms = Chaines(Json.Valeur(m, "permissions")).Concat(Chaines(Json.Valeur(m, "optional_permissions"))).Concat(Chaines(Json.Valeur(m, "host_permissions"))).ToList();
                            foreach (var cs in Json.Objets(Json.Valeur(m, "content_scripts"))) perms.AddRange(Chaines(Json.Valeur(cs, "matches")));
                            string id = Path.GetFileName(ext);
                            res.Add(new Extension
                            {
                                Navigateur = n[0], Profil = nomProfil, Nom = NomLocal(ver.FullName, Json.Texte(m, "name"), Json.Texte(m, "default_locale")), Version = Json.Texte(m, "version"), Id = id,
                                TousSites = perms.Any(p => TousLesSites.IsMatch(p)), Risques = perms.Where(p => Sensibles.ContainsKey(p)).Select(p => Sensibles[p]).Distinct().ToList(),
                                Boutique = Regex.IsMatch(Json.Texte(m, "update_url"), @"clients2\.google\.com|edge\.microsoft\.com|microsoftedge"), Page = string.Format(n[2], id), Date = Directory.GetCreationTime(ext)
                            });
                        }
                        catch { }
                    }
                }
            }
            // Firefox
            string ff = Path.Combine(Chemins.AppData, @"Mozilla\Firefox\Profiles");
            if (Directory.Exists(ff))
                foreach (var prof in Directory.GetDirectories(ff))
                {
                    var j = Json.LireFichier(Path.Combine(prof, "extensions.json")) as Dictionary<string, object>;
                    if (j == null) continue;
                    foreach (var a in Json.Objets(Json.Valeur(j, "addons")))
                    {
                        if (Json.Texte(a, "type") != "extension" || Regex.IsMatch(Json.Texte(a, "location"), "^app-(builtin|system)")) continue;
                        var up = Json.Dict(a, "userPermissions");
                        var perms = Chaines(Json.Valeur(up, "permissions")).Concat(Chaines(Json.Valeur(up, "origins"))).ToList();
                        DateTime? date = null; try { long ms = Json.Long(a, "installDate"); if (ms > 0) date = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime; } catch { }
                        string id = Json.Texte(a, "id");
                        res.Add(new Extension
                        {
                            Navigateur = "Firefox", Profil = Regex.Replace(Path.GetFileName(prof), @"^[^.]+\.", ""), Nom = Json.Texte(Json.Dict(a, "defaultLocale"), "name"), Version = Json.Texte(a, "version"), Id = id,
                            TousSites = perms.Any(p => TousLesSites.IsMatch(p)), Risques = perms.Where(p => Sensibles.ContainsKey(p)).Select(p => Sensibles[p]).Distinct().ToList(),
                            Boutique = Json.Long(a, "signedState") > 0, Page = "https://addons.mozilla.org/fr/firefox/addon/" + Uri.EscapeDataString(id) + "/", Date = date
                        });
                    }
                }
            return res;
        }
    }
}
