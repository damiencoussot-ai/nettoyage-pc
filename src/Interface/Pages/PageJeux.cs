// Nettoyage PC - page Jeux : booster FPS (réglages Windows réversibles), jeux détectés et lancement boosté
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal class ReglageJeu
    {
        public string Cle, Libelle, Gain;
        public Func<bool> Etat;
        public Func<bool> Appliquer;
    }

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

    internal class PageJeux : Page
    {
        private const string CleGpuPref = @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences";
        private readonly List<ReglageJeu> reglages = new List<ReglageJeu>();
        private readonly Dictionary<string, Tuple<CaseACocher, Etiquette>> cases = new Dictionary<string, Tuple<CaseACocher, Etiquette>>();
        private readonly Carte carte;
        private readonly Etiquette lblBoost, lblMesJeux, lblJeux;
        private readonly Bouton bAppliquer, bGourmandes, bLancer, bGpu, bDossier, bActu;
        private readonly ListeSombre lv = new ListeSombre { MultiSelect = false };
        private List<Jeu> jeux = new List<Jeu>();
        private bool recherche;
        private readonly int hautJeux;
        private Jeu surveille; private string processusSurveille; private bool vu; private DateTime depart; private int absent;
        private readonly Timer minuteurJeu = new Timer { Interval = 5000 };

        public PageJeux(FenetrePrincipale f) : base(f, "Jeux", "Jeux", "Plus de FPS : réglages de Windows pour les jeux et lancement boosté de tes jeux.", 0xE7FC)
        {
            AutoScroll = false;
            DefinirReglages();
            carte = new Carte(600, 100, true) { Location = new Point(28, 4) };
            carte.Controls.Add(new Etiquette("Booster FPS : réglages de Windows pour les jeux", Theme.CarteTitre, Role.Texte, 20, 18));
            lblBoost = new Etiquette("", Theme.Petit, Role.Discret, 22, 44);
            carte.Controls.Add(lblBoost);
            int y = 78;
            foreach (var r in reglages)
            {
                var cb = new CaseACocher(r.Libelle, 22, y);
                Infobulle.Definir(cb, r.Gain);
                var et = new Etiquette("", Theme.PetitGras, Role.Vert, 0, y + 3);
                carte.Controls.Add(cb); carte.Controls.Add(et);
                cases[r.Cle] = Tuple.Create(cb, et);
                y += 28;
            }
            carte.Height = Math.Max(120, y + 16);
            bAppliquer = new Bouton("Appliquer les réglages cochés", true, 250, 36);
            bGourmandes = new Bouton("Applications gourmandes...", false, 250, 32);
            bAppliquer.Click += (s, e) => Appliquer();
            bGourmandes.Click += (s, e) => AppsGourmandes();
            carte.Controls.Add(bAppliquer); carte.Controls.Add(bGourmandes);
            Controls.Add(carte);

            hautJeux = carte.Bottom + 16;
            lblMesJeux = Titre("Mes jeux", 28, hautJeux);
            lv.Colonne("Jeu", 300); lv.Colonne("Plateforme", 120); lv.Colonne("Taille", 90, HorizontalAlignment.Right); lv.Colonne("Carte graphique", 150); lv.Colonne("Dossier", 380); lv.ColonneElastique = 4;
            lv.ActiverTri();
            Controls.Add(lv);
            bLancer = NouveauBouton("Booster et lancer", true, 190, 34, 28, 0, LancerJeu);
            bLancer.Font = Theme.CarteTitre;
            bGpu = NouveauBouton("Carte graphique performante", false, 220, 34, 228, 0, CarteGraphiquePerformante);
            bDossier = NouveauBouton("Ouvrir le dossier", false, 140, 34, 458, 0, () => { var j = Selection(); if (j != null && Directory.Exists(j.Dossier)) Programmes.Ouvrir(j.Dossier); });
            bActu = NouveauBouton("Actualiser", false, 110, 34, 608, 0, () => { Rechercher(); ActualiserBoost(); });
            lblJeux = new Etiquette("Recherche des jeux installés...", Theme.Normal, Role.Discret) { AutoSize = false, AutoEllipsis = true };
            Controls.Add(lblJeux);
            Resize += (s, e) => Disposer();
            lv.DoubleClick += (s, e) => LancerJeu();
            minuteurJeu.Tick += (s, e) => SurveillerJeu();
            f.ActionFinie += cle =>
            {
                if (cle == "finmodejeu" && finAuto != null) { F.Statut("Partie de " + finAuto + " terminée : mode d'alimentation normal rétabli.", false, Role.Vert); finAuto = null; }
            };
        }

        private string finAuto;

        private void Disposer()
        {
            int w = ClientSize.Width - 56;
            carte.Width = w;
            bAppliquer.Location = new Point(w - 270, 20); bGourmandes.Location = new Point(w - 270, 64);
            lblBoost.MaximumSize = new Size(Math.Max(300, w - 310), 0);
            foreach (var c in cases.Values) c.Item2.Location = new Point(Math.Max(380, c.Item1.Right + 12), c.Item2.Top);
            int yl = hautJeux + 28;
            int hl = Math.Max(140, ClientSize.Height - yl - 92);
            lv.Location = new Point(28, yl); lv.Size = new Size(w, hl);
            int yb = yl + hl + 10;
            bLancer.Location = new Point(28, yb); bGpu.Location = new Point(228, yb); bDossier.Location = new Point(458, yb); bActu.Location = new Point(608, yb);
            lblJeux.Location = new Point(28, yb + 42); lblJeux.Size = new Size(w, 22);
        }

        public override void PremiereFois() { ActualiserBoost(); Rechercher(); }
        public override void Fermeture() { minuteurJeu.Stop(); }

        // ------------------------------------------------------------ Booster FPS
        private void DefinirReglages()
        {
            reglages.Add(new ReglageJeu
            {
                Cle = "ModeJeuWin", Libelle = "Mode Jeu de Windows", Gain = "Windows donne la priorité au jeu (processeur, carte graphique) et repousse les mises à jour pendant la partie.",
                Etat = () => { var v = Registre.Lire(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled"); return v == null || Convert.ToInt32(v) != 0; },
                Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord, "Jeux : Mode Jeu de Windows activé")
            });
            reglages.Add(new ReglageJeu
            {
                Cle = "CaptureFond", Libelle = "Pas d'enregistrement vidéo en arrière-plan", Gain = "La Xbox Game Bar n'enregistre plus en continu les 30 dernières secondes : quelques FPS gagnés. Les captures manuelles (Win+Alt+R) marchent toujours.",
                Etat = () => { var v = Registre.Lire(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled"); return v == null || Convert.ToInt32(v) == 0; },
                Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", 0, RegistryValueKind.DWord, "Jeux : enregistrement vidéo en arrière-plan désactivé")
            });
            if (Natif.Build >= 19041 || !Natif.EstWindows)
                reglages.Add(new ReglageJeu
                {
                    Cle = "Hags", Libelle = "Planification GPU accélérée par le matériel", Gain = "La carte graphique gère elle-même sa mémoire : moins de latence et souvent plus de FPS. Prend effet après un redémarrage.",
                    Etat = () => { var v = Registre.Lire(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode"); return v != null && Convert.ToInt32(v) == 2; },
                    Appliquer = () => Registre.EcrireSuivi(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, RegistryValueKind.DWord, "Jeux : planification GPU accélérée par le matériel (après redémarrage)")
                });
            if (Natif.Build >= 22000 || !Natif.EstWindows)
                reglages.Add(new ReglageJeu
                {
                    Cle = "Fenetre", Libelle = "Optimisations pour les jeux en mode fenêtré", Gain = "Réglage de Windows 11 : moins de latence et meilleures performances pour les jeux en fenêtre ou en plein écran fenêtré.",
                    Etat = () => Registre.Texte(CleGpuPref, "DirectXUserGlobalSettings").Contains("SwapEffectUpgradeEnable=1"),
                    Appliquer = () =>
                    {
                        string actuel = Registre.Texte(CleGpuPref, "DirectXUserGlobalSettings");
                        string nouveau = Regex.Replace(actuel, @"SwapEffectUpgradeEnable=\d;?", "").Trim() + "SwapEffectUpgradeEnable=1;";
                        return Registre.EcrireSuivi(CleGpuPref, "DirectXUserGlobalSettings", nouveau, RegistryValueKind.String, "Jeux : optimisations pour les jeux en mode fenêtré");
                    }
                });
        }

        private void ActualiserBoost()
        {
            int aFaire = 0;
            foreach (var r in reglages)
            {
                bool ok = false; try { ok = r.Etat(); } catch { }
                var c = cases[r.Cle];
                c.Item1.Enabled = !ok; c.Item1.Checked = !ok;
                c.Item2.Text = ok ? "✔ déjà optimisé" : "à optimiser";
                c.Item2.RoleTexte = ok ? Role.Vert : Role.Orange; c.Item2.AppliquerTheme();
                if (!ok) aFaire++;
            }
            lblBoost.Text = aFaire == 0 ? "Tout est déjà optimisé. Lance tes jeux avec « Booster et lancer » pour le mode performances et la RAM libérée."
                : aFaire + " réglage(s) peuvent être améliorés. Passe la souris sur chaque ligne pour le détail. Tout est réversible depuis l'Historique.";
            bAppliquer.Enabled = aFaire > 0;
            Disposer();
        }

        private void Appliquer()
        {
            var choisis = reglages.Where(r => cases[r.Cle].Item1.Enabled && cases[r.Cle].Item1.Checked).ToList();
            if (choisis.Count == 0) return;
            string liste = string.Join("\n", choisis.Select(r => "- " + r.Libelle));
            if (F.ChkSimu.Checked) { BoiteMessage.Info("Mode simulation : ces réglages seraient appliqués :\n\n" + liste + "\n\nDécoche « Mode simulation » pour les appliquer vraiment.", "Nettoyage PC - jeux"); return; }
            if (!BoiteMessage.Question("Appliquer ces réglages ?\n\n" + liste + "\n\nChaque réglage est enregistré dans l'Historique et peut être annulé.", "Nettoyage PC - jeux")) return;
            int faits = 0; bool redem = false; var erreurs = new List<string>();
            foreach (var r in choisis)
            {
                try { if (r.Appliquer()) { faits++; if (r.Cle == "Hags") redem = true; } }
                catch (Exception ex) { erreurs.Add(r.Libelle + " : " + ex.Message); }
            }
            ActualiserBoost();
            string msg = faits + " réglage(s) appliqué(s).";
            if (redem) msg += "\n\nLa planification GPU prendra effet au prochain redémarrage.";
            if (erreurs.Count > 0) msg += "\n\nNon appliqué :\n" + string.Join("\n", erreurs);
            BoiteMessage.Info(msg, "Nettoyage PC - jeux");
        }

        // ------------------------------------------------------------ Applications gourmandes (fermeture normale, comme la croix)
        private static readonly Regex Proteges = new Regex(@"^(System|Idle|Registry|MemCompression|smss|csrss|wininit|winlogon|services|lsass|svchost|dwm|explorer|fontdrvhost|sihost|ctfmon|audiodg|SearchHost|StartMenuExperienceHost|ShellExperienceHost|TextInputHost|RuntimeBroker|MsMpEng|NisSrv|SecurityHealthService|SecurityHealthSystray|powershell|pwsh|conhost|NettoyagePC|steam|steamwebhelper|EpicGamesLauncher|Battle\.net|RiotClientServices|EADesktop|upc|GalaxyClient|XboxPcApp|gamingservices|amdow|AMDRSServ|RadeonSoftware|NVIDIA.*|nvcontainer|Discord|LGHUB|lghub_agent|GoXLR.*|StreamDeck|RazerAppEngine|obs64)$", RegexOptions.IgnoreCase);

        private void AppsGourmandes()
        {
            var groupes = new List<Tuple<string, string, long>>();
            try
            {
                var tous = Process.GetProcesses();
                foreach (var g in tous.Where(p => { try { return p.MainWindowHandle != IntPtr.Zero && !Proteges.IsMatch(p.ProcessName); } catch { return false; } }).GroupBy(p => p.ProcessName))
                {
                    long octets = 0;
                    foreach (var p in tous) { try { if (p.ProcessName == g.Key) octets += p.WorkingSet64; } catch { } }
                    string titre = ""; try { titre = g.First().MainWindowTitle; } catch { }
                    if (octets >= 150L * 1048576) groupes.Add(Tuple.Create(g.Key, titre, octets));
                }
            }
            catch { }
            groupes = groupes.OrderByDescending(g => g.Item3).Take(12).ToList();
            if (groupes.Count == 0) { BoiteMessage.Info("Aucune application gourmande ouverte : ton PC est prêt pour jouer.", "Nettoyage PC - jeux"); return; }
            using (var f = new FenetreTheme("Applications gourmandes ouvertes", 560, 440))
            {
                var t = new Etiquette("Coche celles à fermer avant de jouer. Elles se ferment normalement : si un document n'est pas enregistré, l'application te le demandera. Discord, les lanceurs de jeux et les logiciels de tes périphériques ne sont pas proposés.", Theme.Normal, Role.Discret, 18, 14) { MaximumSize = new Size(510, 0) };
                var lc = new ListeCases { Location = new Point(20, 84), Size = new Size(505, 246) };
                foreach (var g in groupes) lc.Items.Add(Format.Taille(g.Item3).PadLeft(10) + "   " + g.Item1 + (g.Item2 != "" ? "  —  " + g.Item2 : ""), false);
                var ok = new Bouton("Fermer les applications cochées", true, 260, 34) { Location = new Point(160, 345), DialogResult = DialogResult.OK };
                var an = new Bouton("Annuler", false, 100, 34) { Location = new Point(428, 345), DialogResult = DialogResult.Cancel };
                f.Controls.AddRange(new Control[] { t, lc, ok, an });
                f.CancelButton = an;
                Theme.Appliquer(f);
                if (f.ShowDialog(F) != DialogResult.OK || lc.CheckedIndices.Count == 0) return;
                int fermes = 0;
                foreach (int i in lc.CheckedIndices)
                    foreach (var p in Process.GetProcessesByName(groupes[i].Item1))
                        try { if (p.MainWindowHandle != IntPtr.Zero && p.CloseMainWindow()) fermes++; } catch { }
                F.Statut(fermes + " fenêtre(s) en cours de fermeture.");
            }
        }

        // ------------------------------------------------------------ Mes jeux
        private Jeu Selection() { return lv.SelectedItems.Count == 0 ? null : (Jeu)lv.SelectedItems[0].Tag; }

        private void Rechercher()
        {
            if (recherche) return;
            recherche = true; bActu.Enabled = false;
            lblJeux.Text = "Recherche des jeux installés (Steam, Epic, Xbox, GOG, Ubisoft, Battle.net, EA, Riot, Rockstar)...";
            lblJeux.RoleTexte = Role.AccentVif; lblJeux.AppliquerTheme();
            EnFond(DetectionJeux.Lister, l => { recherche = false; bActu.Enabled = true; jeux = l ?? new List<Jeu>(); Afficher(); });
        }

        private void Afficher()
        {
            var sel = Selection();
            string nomSel = sel == null ? "" : sel.Nom;
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var j in jeux)
            {
                string gp = j.Exe != "" && Registre.Texte(CleGpuPref, j.Exe).Contains("GpuPreference=2") ? "Haute performance" : "Automatique";
                var it = lv.Ajouter(j, j.Nom, j.Plateforme, j.Taille > 0 ? Format.Taille(j.Taille) : "", gp, j.Dossier);
                it.SubItems[2].Tag = j.Taille;
                if (j.Nom == nomSel) it.Selected = true;
            }
            lv.EndUpdate();
            if (lv.Items.Count > 0 && lv.SelectedItems.Count == 0) lv.Items[0].Selected = true;
            lblJeux.Text = jeux.Count == 0 ? "Aucun jeu détecté. Les jeux installés sans lanceur (Steam, Epic, Xbox...) ne sont pas reconnus."
                : jeux.Count + " jeu(x) détecté(s). « Booster et lancer » : mode performances + RAM libérée, puis retour automatique au mode normal quand tu quittes le jeu.";
            lblJeux.RoleTexte = Role.Discret; lblJeux.AppliquerTheme();
        }

        private void CarteGraphiquePerformante()
        {
            var j = Selection(); if (j == null) return;
            if (j.Exe == "" || !File.Exists(j.Exe)) { BoiteMessage.Info("Le programme du jeu n'a pas été trouvé : ce réglage n'est pas possible pour ce jeu.", "Nettoyage PC - jeux"); return; }
            string msg = "Faire utiliser la carte graphique la plus puissante à « " + j.Nom + " » ?\n\n" + j.Exe + "\n\nUtile surtout sur un PC portable ou un PC avec deux cartes graphiques (sinon, Windows utilise déjà la bonne). Réglage de Windows (Paramètres > Écran > Graphiques), réversible depuis l'Historique.";
            if (F.ChkSimu.Checked) { BoiteMessage.Info("Mode simulation : rien n'est modifié.\n\n" + msg, "Nettoyage PC - jeux"); return; }
            if (!BoiteMessage.Question(msg, "Nettoyage PC - jeux")) return;
            try
            {
                Registre.EcrireSuivi(CleGpuPref, j.Exe, "GpuPreference=2;", RegistryValueKind.String, "Jeux : carte graphique haute performance pour " + j.Nom);
                Afficher();
                F.Statut(j.Nom + " utilisera la carte graphique haute performance (au prochain lancement du jeu).");
            }
            catch (Exception ex) { BoiteMessage.Attention("Impossible : " + ex.Message); }
        }

        private bool ModeJeuActif { get { return File.Exists(Chemins.Fichier("modejeu.json")); } }

        private void LancerJeu()
        {
            var j = Selection(); if (j == null) return;
            if (F.ActionEnCours) { BoiteMessage.Info("Une action est déjà en cours : attends qu'elle se termine."); return; }
            if (ModeJeuActif) { DemarrerJeu(j); return; }
            // Boost (performances + RAM), puis lancement ; si le boost n'est pas lancé (autorisation refusée...), le jeu démarre quand même
            if (!F.LancerAction("modejeu", null, null, () => DemarrerJeu(j))) DemarrerJeu(j);
        }

        /// <summary>Les jeux sont toujours lancés avec les droits normaux (jamais administrateur) : c'est ce qu'attendent les lanceurs et les anti-triches.</summary>
        private void DemarrerJeu(Jeu j)
        {
            try
            {
                if (j.Lancer.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
                    Process.Start(new ProcessStartInfo(Path.Combine(Chemins.Windows, "explorer.exe"), j.Lancer) { UseShellExecute = false });
                else Programmes.Ouvrir(j.Lancer, j.Arguments == "" ? null : j.Arguments);
            }
            catch (Exception ex) { BoiteMessage.Attention("Impossible de lancer " + j.Nom + " : " + ex.Message); return; }
            F.Statut(j.Nom + " se lance... Bon jeu !", false, Role.Vert);
            // Surveillance : quand le jeu se ferme, on revient au mode normal
            if (j.Exe != "" && ModeJeuActif)
            {
                surveille = j; processusSurveille = Path.GetFileNameWithoutExtension(j.Exe); vu = false; depart = DateTime.Now; absent = 0;
                minuteurJeu.Start();
            }
        }

        private void SurveillerJeu()
        {
            if (surveille == null) { minuteurJeu.Stop(); return; }
            bool enCours = false;
            try { var ps = Process.GetProcessesByName(processusSurveille); enCours = ps.Length > 0; foreach (var p in ps) p.Dispose(); } catch { }
            if (enCours) { vu = true; absent = 0; return; }
            if (!vu)
            {
                // Le jeu n'a jamais été vu (lanceur long, autre programme) : on abandonne au bout de 10 minutes
                if ((DateTime.Now - depart).TotalMinutes > 10) { surveille = null; minuteurJeu.Stop(); }
                return;
            }
            if (++absent < 3) return;   // 15 s sans le jeu : il est bien fermé
            if (F.ActionEnCours) return;
            var j = surveille; surveille = null; minuteurJeu.Stop();
            if (ModeJeuActif) { finAuto = j.Nom; F.LancerAction("finmodejeu"); }
        }
    }
}
