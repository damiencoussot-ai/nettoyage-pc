// Nettoyage PC - jeux installés (Steam, Epic, GOG, Xbox, Ubisoft, EA, Battle.net, Riot...) : dossier, programme
// principal et lancement. Partagé par les deux interfaces.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal class Jeu
    {
        public string Nom = "", Plateforme = "", Dossier = "", Exe = "", Lancer = "", Arguments = "";
        public double Taille;
    }

    internal static class DetectionJeux
    {
        private static readonly Regex Exclus = new Regex("unins|crash|setup|redist|dxsetup|dotnet|easyanticheat|battleye|beservice|cefprocess|helper|report|updat|install|prereq|vc_?redist|7z|python|ffmpeg|launcherpatcher|uploader|benchmark|server", RegexOptions.IgnoreCase);

        /// <summary>Programme principal probable d'un jeu : le plus gros .exe (3 niveaux de dossiers au plus).</summary>
        public static string TrouverExe(string dossier)
        {
            if (string.IsNullOrEmpty(dossier) || !Directory.Exists(dossier)) return "";
            string meilleur = ""; long taille = -1;
            Action<string, int> parcourir = null;
            parcourir = (d, niveau) =>
            {
                try
                {
                    foreach (var f in Directory.GetFiles(d, "*.exe"))
                    {
                        if (Exclus.IsMatch(Path.GetFileName(f))) continue;
                        try { long t = new FileInfo(f).Length; if (t > taille) { taille = t; meilleur = f; } } catch { }
                    }
                    if (niveau < 3) foreach (var s in Directory.GetDirectories(d)) { if (!Protection.CheminSansLien(s)) continue; parcourir(s, niveau + 1); }
                }
                catch { }
            };
            parcourir(dossier, 0);
            return meilleur;
        }

        public static List<Jeu> Lister()
        {
            var jeux = new List<Jeu>();
            var vus = new HashSet<string>();
            Action<string, string, string, string, string, string, double> ajouter = (nom, plateforme, dossier, exe, lancer, args, taille) =>
            {
                if (string.IsNullOrWhiteSpace(nom)) return;
                string cle = Regex.Replace(nom, @"[^\p{L}\p{N}]", "").ToLowerInvariant();
                if (cle == "" || !vus.Add(cle)) return;
                jeux.Add(new Jeu { Nom = nom.Trim(), Plateforme = plateforme, Dossier = dossier ?? "", Exe = exe ?? "", Lancer = lancer ?? "", Arguments = args ?? "", Taille = taille });
            };

            // Steam
            try
            {
                string steam = Registre.Texte(@"HKCU\Software\Valve\Steam", "SteamPath");
                if (steam == "") steam = Registre.Texte(@"HKLM\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath");
                if (steam != "")
                {
                    steam = steam.Replace('/', '\\');
                    var biblios = new List<string> { steam };
                    string vdf = Path.Combine(steam, @"steamapps\libraryfolders.vdf");
                    if (File.Exists(vdf)) foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\"")) biblios.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                    foreach (var b in biblios.Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        string sa = Path.Combine(b, "steamapps");
                        if (!Directory.Exists(sa)) continue;
                        foreach (var acf in Directory.GetFiles(sa, "appmanifest_*.acf"))
                        {
                            try
                            {
                                string t = File.ReadAllText(acf);
                                Func<string, string> champ = n => Regex.Match(t, "\"" + n + "\"\\s+\"([^\"]*)\"", RegexOptions.IgnoreCase).Groups[1].Value;
                                string id = champ("appid"), nom = champ("name"), dir = champ("installdir");
                                double taille; double.TryParse(champ("SizeOnDisk"), out taille);
                                if (id == "228980" || Regex.IsMatch(nom, "redistributable|steamworks|proton|steam linux|sdk|dedicated server|soundtrack|steamvr|wallpaper engine", RegexOptions.IgnoreCase)) continue;
                                if (!Regex.IsMatch(id, @"^\d+$") || dir == "") continue;
                                string dossier = Path.Combine(sa, "common", dir);
                                ajouter(nom, "Steam", dossier, TrouverExe(dossier), "steam://rungameid/" + id, "", taille);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }

            // Epic Games
            try
            {
                string man = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
                if (Directory.Exists(man))
                    foreach (var f in Directory.GetFiles(man, "*.item"))
                    {
                        try
                        {
                            var j = Json.LireFichier(f) as Dictionary<string, object>;
                            if (j == null || Json.Bool(j, "bIsIncompleteInstall")) continue;
                            var cat = Json.Textes(j, "AppCategories");
                            if (cat.Count > 0 && !cat.Contains("games")) continue;
                            string lieu = Json.Texte(j, "InstallLocation"), exe = "";
                            if (Json.Texte(j, "LaunchExecutable") != "" && lieu != "") exe = Path.Combine(lieu, Json.Texte(j, "LaunchExecutable"));
                            string uri = string.Format("com.epicgames.launcher://apps/{0}%3A{1}%3A{2}?action=launch&silent=true",
                                Uri.EscapeDataString(Json.Texte(j, "CatalogNamespace")), Uri.EscapeDataString(Json.Texte(j, "CatalogItemId")), Uri.EscapeDataString(Json.Texte(j, "AppName")));
                            ajouter(Json.Texte(j, "DisplayName"), "Epic Games", lieu, exe, uri, "", Json.Long(j, "InstallSize"));
                        }
                        catch { }
                    }
            }
            catch { }

            // Xbox / Microsoft Store (jeux avec MicrosoftGame.config)
            try
            {
                foreach (var p in Paquets.Lister())
                {
                    try
                    {
                        if (p.Dossier == "" || p.AppId == "") continue;
                        string cfg = Path.Combine(p.Dossier, "MicrosoftGame.config");
                        if (!File.Exists(cfg)) continue;
                        var x = System.Xml.Linq.XDocument.Load(cfg);
                        var sv = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "ShellVisuals");
                        string nom = sv == null ? "" : (string)sv.Attribute("DefaultDisplayName") ?? "";
                        var ex = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "Executable");
                        string exeNom = ex == null ? "" : (string)ex.Attribute("Name") ?? "";
                        if (nom == "" || nom.StartsWith("ms-resource")) nom = Path.GetFileName(Path.GetDirectoryName(p.Dossier.TrimEnd('\\')));
                        ajouter(nom, "Xbox", p.Dossier, exeNom == "" ? "" : Path.Combine(p.Dossier, exeNom), "shell:AppsFolder\\" + p.Famille + "!" + p.AppId, "", 0);
                    }
                    catch { }
                }
            }
            catch { }

            // GOG
            foreach (var k in Registre.SousCles(@"HKLM\SOFTWARE\WOW6432Node\GOG.com\Games"))
            {
                string c = @"HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\" + k;
                string exe = Registre.Texte(c, "exe");
                if (exe != "") ajouter(Registre.Texte(c, "gameName"), "GOG", Registre.Texte(c, "path"), exe, exe, "", 0);
            }

            // Ubisoft Connect
            foreach (var k in Registre.SousCles(@"HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs"))
            {
                string dir = Registre.Texte(@"HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\" + k, "InstallDir").TrimEnd('/', '\\').Replace('/', '\\');
                if (dir == "" || !Directory.Exists(dir) || !Regex.IsMatch(k, @"^\d+$")) continue;
                ajouter(Path.GetFileName(dir), "Ubisoft Connect", dir, TrouverExe(dir), "uplay://launch/" + k + "/0", "", 0);
            }

            // Battle.net, EA, Riot, Rockstar : programmes installés de ces éditeurs
            try
            {
                foreach (var e in ProgrammesInstalles.Lister())
                {
                    if (!Regex.IsMatch(e.Editeur, "blizzard|electronic arts|riot games|rockstar games", RegexOptions.IgnoreCase)) continue;
                    if (Regex.IsMatch(e.Nom, @"battle\.net|^ea app|^ea$|origin|riot client|vanguard|launcher|social club|redistributable|anticheat|anti-cheat", RegexOptions.IgnoreCase)) continue;
                    string plat = Regex.IsMatch(e.Editeur, "electronic arts", RegexOptions.IgnoreCase) ? "EA" : Regex.IsMatch(e.Editeur, "riot", RegexOptions.IgnoreCase) ? "Riot" : Regex.IsMatch(e.Editeur, "rockstar", RegexOptions.IgnoreCase) ? "Rockstar" : "Battle.net";
                    string lancer = "", args = "", exe = "";
                    var m = Regex.Match(e.Desinstaller, "\"?([^\"]*RiotClientServices\\.exe)\"?\\s+--uninstall-product=(\\S+)\\s+--uninstall-patchline=(\\S+)", RegexOptions.IgnoreCase);
                    if (m.Success) { lancer = m.Groups[1].Value; args = "--launch-product=" + m.Groups[2].Value + " --launch-patchline=" + m.Groups[3].Value; }
                    if (e.Dossier != "") exe = TrouverExe(e.Dossier);
                    if (exe == "") { var mi = Regex.Match(e.Icone, "^\"?([^\",]+\\.exe)", RegexOptions.IgnoreCase); if (mi.Success) exe = mi.Groups[1].Value; }
                    if (lancer == "") lancer = exe;
                    if (lancer == "") continue;
                    ajouter(e.Nom, plat, e.Dossier, exe, lancer, args, e.Octets);
                }
            }
            catch { }
            return jeux.OrderBy(j => j.Nom, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
}
