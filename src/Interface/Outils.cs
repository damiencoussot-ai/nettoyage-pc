// Nettoyage PC - fenêtres d'outils : confidentialité, extensions des navigateurs, carte de l'espace disque,
// applications préinstallées inutiles, anciens pilotes
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NettoyagePC
{
    // ================================================================ Réglages de Windows (confidentialité)
    internal static class FenetreReglagesWindows
    {
        private const string Cdm = @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private static readonly string[] Suggestions = { "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled", "SubscribedContent-353694Enabled", "SubscribedContent-353696Enabled", "SystemPaneSuggestionsEnabled", "SoftLandingEnabled" };

        private static int? Dw(string cle, string nom) { var v = Registre.Lire(cle, nom); try { return v == null ? (int?)null : Convert.ToInt32(v); } catch { return null; } }

        public static void Confidentialite(FenetrePrincipale f)
        {
            var l = new List<ReglageJeu>
            {
                new ReglageJeu { Cle = "Pub", Libelle = "Pas d'identifiant de publicité", Gain = "Les applications ne peuvent plus te reconnaître pour te montrer des publicités ciblées.",
                    Etat = () => Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled") == 0,
                    Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, RegistryValueKind.DWord, "Confidentialité : identifiant de publicité désactivé") },
                new ReglageJeu { Cle = "Suggestions", Libelle = "Pas de suggestions ni de publicités dans Windows", Gain = "Plus d'applications « suggérées » dans le menu Démarrer, de conseils et d'offres dans Paramètres et sur l'écran de verrouillage.",
                    Etat = () => Suggestions.All(v => Dw(Cdm, v) == 0),
                    Appliquer = () => { bool r = false; foreach (var v in Suggestions) r |= Registre.EcrireSuivi(Cdm, v, 0, RegistryValueKind.DWord, "Confidentialité : suggestions de Windows (" + v + ")"); return r; } },
                new ReglageJeu { Cle = "Experiences", Libelle = "Pas d'expériences personnalisées", Gain = "Microsoft n'utilise plus tes données de diagnostic pour personnaliser les conseils et les publicités.",
                    Etat = () => Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled") == 0,
                    Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, RegistryValueKind.DWord, "Confidentialité : expériences personnalisées désactivées") },
                new ReglageJeu { Cle = "Langues", Libelle = "Les sites web ne voient pas ta liste de langues", Gain = "Les sites ne peuvent plus lire la liste des langues installées sur ton PC (utilisée pour te pister).",
                    Etat = () => Dw(@"HKCU\Control Panel\International\User Profile", "HttpAcceptLanguageOptOut") == 1,
                    Appliquer = () => Registre.EcrireSuivi(@"HKCU\Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1, RegistryValueKind.DWord, "Confidentialité : liste des langues masquée aux sites web") },
                new ReglageJeu { Cle = "Bing", Libelle = "Recherche du menu Démarrer sans résultats Bing", Gain = "La recherche du menu Démarrer ne cherche plus sur Internet : plus rapide, et tes recherches restent sur ton PC. Effet à la prochaine ouverture de session.",
                    Etat = () => Dw(@"HKCU\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions") == 1,
                    Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord, "Confidentialité : recherche Bing du menu Démarrer désactivée") }
            };
            Afficher(f, "Confidentialité", "Réglages de ton compte Windows qui limitent la publicité et le pistage. Rien n'est désactivé côté sécurité : Windows Update, l'antivirus et le pare-feu ne sont pas touchés.", l);
        }

        /// <summary>Fenêtre générique : état actuel, cases à cocher, confirmation, Historique (réversible).</summary>
        public static void Afficher(FenetrePrincipale principale, string titre, string intro, List<ReglageJeu> definitions)
        {
            var cases = new List<Tuple<CaseACocher, ReglageJeu>>();
            DialogResult r;
            using (var f = new FenetreTheme(titre, 740, 210 + 34 * definitions.Count))
            {
                f.Controls.Add(new Etiquette(intro, Theme.Normal, Role.Discret, 20, 16) { MaximumSize = new Size(680, 0) });
                int y = 76;
                foreach (var d in definitions)
                {
                    bool ok = false; try { ok = d.Etat(); } catch { }
                    var cb = new CaseACocher(d.Libelle, 22, y) { Checked = !ok, Enabled = !ok };
                    Infobulle.Definir(cb, d.Gain);
                    var et = new Etiquette(ok ? "✔ déjà fait" : "à régler", Theme.PetitGras, ok ? Role.Vert : Role.Orange, 480, y + 3);
                    f.Controls.Add(cb); f.Controls.Add(et);
                    cases.Add(Tuple.Create(cb, d));
                    y += 34;
                }
                f.Controls.Add(new Etiquette("Passe la souris sur une ligne pour le détail. Chaque réglage est enregistré dans l'Historique et peut être annulé.", Theme.Petit, Role.Discret, 22, y + 6));
                var bOk = new Bouton("Appliquer les réglages cochés", true, 250, 34) { Location = new Point(350, y + 38), DialogResult = DialogResult.OK, Enabled = cases.Any(c => c.Item1.Enabled) };
                var bAn = new Bouton("Fermer", false, 100, 34) { Location = new Point(610, y + 38), DialogResult = DialogResult.Cancel };
                f.Controls.Add(bOk); f.Controls.Add(bAn); f.CancelButton = bAn;
                f.ClientSize = new Size(f.ClientSize.Width, y + 92);
                Theme.Appliquer(f);
                r = f.ShowDialog(principale);
            }
            var choisis = cases.Where(c => c.Item1.Enabled && c.Item1.Checked).Select(c => c.Item2).ToList();
            if (r != DialogResult.OK || choisis.Count == 0) return;
            string liste = string.Join("\n", choisis.Select(d => "- " + d.Libelle));
            if (principale.ChkSimu.Checked) { BoiteMessage.Info("Mode simulation : ces réglages seraient appliqués :\n\n" + liste); return; }
            int faits = 0; var erreurs = new List<string>();
            foreach (var d in choisis) { try { d.Appliquer(); faits++; } catch (Exception ex) { erreurs.Add(d.Libelle + " : " + ex.Message); } }
            string msg = faits + " réglage(s) appliqué(s). Certains prennent effet à la prochaine ouverture de session.";
            if (erreurs.Count > 0) msg += "\n\nNon appliqué :\n" + string.Join("\n", erreurs);
            BoiteMessage.Info(msg);
        }
    }

    // ================================================================ Extensions des navigateurs (lecture seule)
    internal class Extension
    {
        public string Navigateur = "", Profil = "", Nom = "", Version = "", Id = "", Page = "";
        public bool TousSites, Boutique;
        public List<string> Risques = new List<string>();
        public DateTime? Date;
    }

    internal class FenetreExtensions : FenetreTheme
    {
        private static readonly Dictionary<string, string> Sensibles = new Dictionary<string, string>
        {
            { "webRequest", "surveiller ta navigation" }, { "webRequestBlocking", "modifier ta navigation" }, { "cookies", "lire les cookies" }, { "history", "lire l'historique" },
            { "debugger", "contrôler les pages" }, { "nativeMessaging", "parler à des programmes du PC" }, { "proxy", "changer la connexion" }, { "management", "gérer les autres extensions" },
            { "clipboardRead", "lire le presse-papiers" }, { "downloads", "gérer les téléchargements" }, { "tabs", "voir tes onglets" }, { "scripting", "injecter du code dans les pages" }
        };
        private static readonly Regex TousLesSites = new Regex(@"^(<all_urls>|\*://\*/\*|https?://\*/\*)$");
        private readonly ListeSombre lv = new ListeSombre { MultiSelect = false, ShowItemToolTips = true, Location = new Point(20, 66), Size = new Size(944, 410), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Etiquette info;

        public FenetreExtensions(FenetrePrincipale f) : base("Extensions des navigateurs", 1000, 610, true)
        {
            Controls.Add(new Etiquette("Les extensions installées dans tes navigateurs. Une extension inconnue, installée hors de la boutique officielle ou qui peut lire tous les sites mérite d'être vérifiée. Nettoyage PC ne touche à rien : la suppression se fait dans le navigateur.", Theme.Normal, Role.Discret, 20, 14) { MaximumSize = new Size(940, 0) });
            lv.Colonne("Extension", 270); lv.Colonne("Navigateur", 140); lv.Colonne("Version", 90); lv.Colonne("Avis", 430);
            Controls.Add(lv);
            info = new Etiquette("Recherche des extensions...", Theme.Normal, Role.AccentVif, 20, 486) { Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            var bGerer = new Bouton("Gérer dans le navigateur", true, 220, 34) { Location = new Point(20, 514), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            var bPage = new Bouton("Page de l'extension", false, 170, 34) { Location = new Point(250, 514), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(864, 514), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            bGerer.Click += (s, e) => Gerer();
            bPage.Click += (s, e) => { if (lv.SelectedItems.Count > 0) Programmes.Ouvrir(((Extension)lv.SelectedItems[0].Tag).Page); };
            lv.DoubleClick += (s, e) => Gerer();
            Controls.AddRange(new Control[] { info, bGerer, bPage, bFermer });
            CancelButton = bFermer;
            Theme.Appliquer(this);
            Shown += (s, e) => Charger();
        }

        private void Gerer()
        {
            if (lv.SelectedItems.Count == 0) return;
            var x = (Extension)lv.SelectedItems[0].Tag;
            var pages = new Dictionary<string, string[]> { { "Chrome", new[] { "chrome.exe", "chrome://extensions" } }, { "Edge", new[] { "msedge.exe", "edge://extensions" } }, { "Brave", new[] { "brave.exe", "brave://extensions" } }, { "Vivaldi", new[] { "vivaldi.exe", "vivaldi://extensions" } }, { "Firefox", new[] { "firefox.exe", "about:addons" } } };
            string[] p; if (!pages.TryGetValue(x.Navigateur, out p)) return;
            try { Programmes.Ouvrir(p[0], p[1]); } catch { BoiteMessage.Info("Ouvre " + x.Navigateur + " puis va dans " + p[1]); }
        }

        private void Charger()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                List<Extension> l; try { l = Lister(); } catch (Exception ex) { JournalDemarrage.Erreur("extensions", ex); l = new List<Extension>(); }
                try { BeginInvoke(new Action(() => Afficher(l))); } catch { }
            });
        }

        private void Afficher(List<Extension> liste)
        {
            if (IsDisposed) return;
            int aVerifier = 0;
            lv.BeginUpdate();
            foreach (var e in liste.Select(x =>
            {
                var details = new List<string>();
                if (!x.Boutique) details.Add("installée hors de la boutique officielle");
                if (x.TousSites) details.Add("peut lire et modifier tous les sites");
                if (x.Risques.Count > 0) details.Add("peut " + string.Join(", ", x.Risques));
                int niveau = 2; string avis = "OK";
                if (!x.Boutique || (x.TousSites && x.Risques.Count > 0)) { niveau = 0; avis = "À vérifier : " + string.Join(" ; ", details); }
                else if (details.Count > 0) { niveau = 1; avis = "Accès étendu : " + string.Join(" ; ", details); }
                return new { X = x, Niveau = niveau, Avis = avis };
            }).OrderBy(e => e.Niveau).ThenBy(e => e.X.Nom))
            {
                var it = lv.Ajouter(e.X, e.X.Nom, e.X.Navigateur + " (" + e.X.Profil + ")", e.X.Version, e.Avis);
                if (e.Niveau == 0) { it.ForeColor = Theme.P.Orange; aVerifier++; }
                it.ToolTipText = "Identifiant : " + e.X.Id + (e.X.Date.HasValue ? " · installée le " + e.X.Date.Value.ToString("dd/MM/yyyy") : "");
            }
            lv.EndUpdate();
            if (liste.Count == 0) { info.Text = "Aucune extension trouvée (Chrome, Edge, Brave, Vivaldi, Firefox)."; info.RoleTexte = Role.Vert; }
            else if (aVerifier > 0) { info.Text = liste.Count + " extension(s), dont " + aVerifier + " à vérifier (en orange). Si tu ne la reconnais pas, supprime-la depuis le navigateur."; info.RoleTexte = Role.Orange; }
            else { info.Text = liste.Count + " extension(s), aucune n'est suspecte. ✔"; info.RoleTexte = Role.Vert; }
            info.AppliquerTheme();
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

    // ================================================================ Carte de l'espace disque (rectangles proportionnels)
    internal class Pave { public string Libelle = "", Chemin = ""; public double Taille; public bool Dossier; public RectangleF R; }

    internal class ZoneCarte : Control, IThemable
    {
        public List<Pave> Paves = new List<Pave>();
        public Pave Survol;
        public bool Calcul;
        public Func<long> Fichiers;
        public ZoneCarte() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); Cursor = Cursors.Hand; }
        public void AppliquerTheme() { BackColor = Theme.P.Carte; Invalidate(); }

        private static readonly Color[] Couleurs = { Color.FromArgb(0, 120, 212), Color.FromArgb(0, 153, 153), Color.FromArgb(122, 82, 199), Color.FromArgb(214, 112, 32), Color.FromArgb(46, 140, 70), Color.FromArgb(194, 57, 101), Color.FromArgb(60, 110, 180), Color.FromArgb(150, 120, 40) };

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(P.Carte);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Calcul)
            {
                Theme.Texte(g, "Analyse en cours...  " + Format.Nombre(Fichiers == null ? 0 : Fichiers()) + " fichiers comptés\n(clique sur « Arrêter » pour interrompre)", Theme.CarteTitre, new Rectangle(30, 30, Width - 60, 80), P.Discret, TextFormatFlags.Left | TextFormatFlags.WordBreak);
                return;
            }
            int i = 0;
            foreach (var p in Paves)
            {
                var r = new RectangleF(p.R.X + 1.5f, p.R.Y + 1.5f, Math.Max(0, p.R.Width - 3), Math.Max(0, p.R.Height - 3));
                Color c = p.Dossier ? Couleurs[i % Couleurs.Length] : Color.FromArgb(120, 126, 138);
                if (p == Survol) c = ControlPaint.Light(c, 0.35f);
                if (r.Width > 2 && r.Height > 2) Theme.RemplirDegrade(g, c, Theme.Melanger(c, Color.Black, 0.18), r.X, r.Y, r.Width, r.Height, 5, true);
                if (r.Width > 70 && r.Height > 34)
                    Theme.Texte(g, p.Libelle + "\n" + Format.Taille(p.Taille), Theme.PetitGras, Rectangle.Round(new RectangleF(r.X + 6, r.Y + 5, r.Width - 12, r.Height - 10)), Color.White, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
                i++;
            }
        }

        /// <summary>Pavage « squarifié » : des rectangles aussi carrés que possible.</summary>
        public static List<Pave> Pavage(List<Pave> elements, float x, float y, float l, float h)
        {
            double total = elements.Sum(e => e.Taille);
            var sortie = new List<Pave>();
            if (total <= 0 || l < 2 || h < 2) return sortie;
            double echelle = l * h / total;
            var reste = new List<Tuple<Pave, double>>(elements.Select(e => Tuple.Create(e, e.Taille * echelle)));
            Func<List<Tuple<Pave, double>>, double, double> pire = (rangee, cote) =>
            {
                double s = rangee.Sum(r => r.Item2), mx = rangee.Max(r => r.Item2), mn = rangee.Min(r => r.Item2);
                if (s <= 0 || mn <= 0) return double.MaxValue;
                return Math.Max(cote * cote * mx / (s * s), s * s / (cote * cote * mn));
            };
            while (reste.Count > 0)
            {
                double cote = Math.Min(l, h);
                var rangee = new List<Tuple<Pave, double>>();
                double ratio = double.MaxValue;
                while (reste.Count > 0)
                {
                    rangee.Add(reste[0]);
                    double nv = pire(rangee, cote);
                    if (rangee.Count > 1 && nv > ratio) { rangee.RemoveAt(rangee.Count - 1); break; }
                    ratio = nv; reste.RemoveAt(0);
                }
                double somme = rangee.Sum(r => r.Item2);
                if (l >= h)
                {
                    float lc = (float)(somme / h), yy = y;
                    foreach (var r in rangee) { float hh = (float)(r.Item2 / lc); r.Item1.R = new RectangleF(x, yy, lc, hh); sortie.Add(r.Item1); yy += hh; }
                    x += lc; l -= lc;
                }
                else
                {
                    float hc = (float)(somme / l), xx = x;
                    foreach (var r in rangee) { float ww = (float)(r.Item2 / hc); r.Item1.R = new RectangleF(xx, y, ww, hc); sortie.Add(r.Item1); xx += ww; }
                    y += hc; h -= hc;
                }
            }
            return sortie;
        }
    }

    internal class FenetreCarteEspace : FenetreTheme
    {
        private readonly ListeDeroulante cb = new ListeDeroulante(320) { Location = new Point(20, 16) };
        private readonly Bouton bAna, bHaut, bExp;
        private readonly Etiquette lblChemin, lblInfo;
        private readonly ZoneCarte zone = new ZoneCarte { Location = new Point(20, 88), Size = new Size(1044, 588), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly List<DriveInfo> lecteurs;
        private readonly ConcurrentDictionary<string, long> tailles = new ConcurrentDictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        private readonly System.Windows.Forms.Timer minuteur = new System.Windows.Forms.Timer { Interval = 400 };
        private string racine = "", courant = "", perso = "";
        private double totalCourant;
        private volatile bool arret;
        private long fichiers;

        public FenetreCarteEspace(FenetrePrincipale f) : base("Carte de l'espace disque", 1100, 770, true)
        {
            MinimumSize = new Size(760, 500); ShowInTaskbar = true; MinimizeBox = true;
            lecteurs = DriveInfo.GetDrives().Where(d => { try { return d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable); } catch { return false; } }).ToList();
            foreach (var d in lecteurs) cb.Items.Add(d.Name.TrimEnd('\\') + "  " + d.VolumeLabel + " — " + Langue.F("{0} utilisés sur {1}", Format.Taille(d.TotalSize - d.AvailableFreeSpace), Format.Taille(d.TotalSize)));
            cb.Items.Add("Mon dossier utilisateur"); cb.Items.Add("Choisir un dossier...");
            cb.SelectedIndex = 0;
            bAna = new Bouton("Analyser", true, 110, 30) { Location = new Point(350, 15) };
            bHaut = new Bouton("↑  Remonter", false, 120, 30) { Location = new Point(470, 15) };
            bExp = new Bouton("Ouvrir dans l'Explorateur", false, 200, 30) { Location = new Point(600, 15) };
            lblChemin = new Etiquette("Choisis un disque puis clique sur « Analyser ». Clique sur un rectangle pour entrer dans le dossier.", Theme.CarteTitre, Role.Texte, 20, 58) { AutoSize = false, AutoEllipsis = true, Size = new Size(1040, 24), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            lblInfo = new Etiquette("", Theme.Normal, Role.Discret, 20, 684) { AutoSize = false, AutoEllipsis = true, Size = new Size(1040, 22), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            zone.Fichiers = () => Interlocked.Read(ref fichiers);
            Controls.AddRange(new Control[] { cb, bAna, bHaut, bExp, lblChemin, zone, lblInfo });
            Theme.Appliquer(this);

            cb.SelectionChangeCommitted += (s, e) =>
            {
                if (cb.SelectedIndex != cb.Items.Count - 1) return;
                using (var dlg = new FolderBrowserDialog())
                {
                    if (dlg.ShowDialog(this) == DialogResult.OK) { perso = dlg.SelectedPath; cb.Items[cb.Items.Count - 1] = "Dossier : " + perso; }
                    else if (perso == "") cb.SelectedIndex = 0;
                }
            };
            bAna.Click += (s, e) =>
            {
                if (zone.Calcul) { arret = true; return; }
                int i = cb.SelectedIndex;
                string r = i < lecteurs.Count ? lecteurs[i].RootDirectory.FullName : i == lecteurs.Count ? Chemins.Profil : perso;
                if (string.IsNullOrEmpty(r) || !Directory.Exists(r)) return;
                tailles.Clear(); racine = r;
                Mesurer(r);
            };
            bHaut.Click += (s, e) => Remonter();
            bExp.Click += (s, e) =>
            {
                string c = zone.Survol != null && zone.Survol.Chemin != "" ? zone.Survol.Chemin : courant;
                if (c != "" && (Directory.Exists(c) || File.Exists(c))) Programmes.MontrerDansExplorateur(c);
            };
            zone.Resize += (s, e) => { if (courant != "" && !zone.Calcul) Afficher(courant); };
            zone.MouseMove += (s, e) =>
            {
                var t = zone.Paves.FirstOrDefault(p => p.R.Contains(e.X, e.Y));
                if (t == zone.Survol) return;
                zone.Survol = t;
                lblInfo.Text = t == null ? "" : t.Libelle + "   —   " + Format.Taille(t.Taille) + "  (" + (totalCourant > 0 ? 100 * t.Taille / totalCourant : 0).ToString("N1", Appli.Fr) + " %)" + (t.Dossier ? "   ·   clic pour entrer" : "");
                zone.Invalidate();
            };
            zone.MouseClick += (s, e) =>
            {
                if (zone.Calcul) return;
                if (e.Button == MouseButtons.Right) { Remonter(); return; }
                var p = zone.Survol;
                if (p != null && p.Dossier && p.Chemin != "") { if (tailles.ContainsKey(p.Chemin) && EnfantsConnus(p.Chemin)) Afficher(p.Chemin); else Mesurer(p.Chemin); }
            };
            minuteur.Tick += (s, e) => { if (zone.Calcul) zone.Invalidate(); else minuteur.Stop(); };
            FormClosed += (s, e) => { arret = true; minuteur.Stop(); };
        }

        private void Remonter()
        {
            if (courant == "" || zone.Calcul) return;
            string parent = Path.GetDirectoryName(courant.TrimEnd('\\'));
            if (string.IsNullOrEmpty(parent) || courant.TrimEnd('\\').Equals(racine.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase) || !courant.StartsWith(racine.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
            if (parent.Length == 2) parent += "\\";
            Afficher(parent);
        }

        private bool EnfantsConnus(string dossier)
        {
            try { foreach (var d in Directory.GetDirectories(dossier)) if (!tailles.ContainsKey(d)) return false; } catch { }
            return true;
        }

        private long MesurerRec(string dossier, int profondeur)
        {
            if (arret) return 0;
            long total = 0;
            try
            {
                var di = new DirectoryInfo(dossier);
                foreach (var f in di.EnumerateFiles())
                {
                    try { if (TailleFichiers.EstCloud(f.Attributes)) continue; total += f.Length; Interlocked.Increment(ref fichiers); } catch { }
                }
                foreach (var d in di.EnumerateDirectories())
                {
                    try { if ((d.Attributes & FileAttributes.ReparsePoint) != 0) continue; } catch { continue; }
                    total += MesurerRec(d.FullName, profondeur + 1);
                }
            }
            catch { }
            if (profondeur <= 3) tailles[dossier] = total;
            return total;
        }

        private void Mesurer(string dossier)
        {
            zone.Calcul = true; bAna.Text = "Arrêter";
            lblChemin.Text = "Analyse de " + dossier + "...";
            arret = false; Interlocked.Exchange(ref fichiers, 0);
            zone.Invalidate(); minuteur.Start();
            System.Threading.Tasks.Task.Run(() =>
            {
                MesurerRec(dossier, 0);
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        zone.Calcul = false; bAna.Text = "Analyser";
                        if (arret) { lblChemin.Text = "Analyse arrêtée."; zone.Invalidate(); return; }
                        Afficher(dossier);
                    }));
                }
                catch { }
            });
        }

        private void Afficher(string dossier)
        {
            courant = dossier;
            var el = new List<Pave>();
            try
            {
                foreach (var d in Directory.GetDirectories(dossier)) { long t; if (tailles.TryGetValue(d, out t) && t > 0) el.Add(new Pave { Libelle = Path.GetFileName(d), Chemin = d, Taille = t, Dossier = true }); }
            }
            catch { }
            var fichiersDossier = new List<FileInfo>();
            try { fichiersDossier = new DirectoryInfo(dossier).GetFiles().Where(f => !TailleFichiers.EstCloud(f.Attributes)).ToList(); } catch { }
            double total = el.Sum(e => e.Taille) + fichiersDossier.Sum(f => (double)f.Length);
            int petits = 0; double taillePetits = 0;
            foreach (var f in fichiersDossier)
            {
                if (total > 0 && f.Length >= total * 0.01) el.Add(new Pave { Libelle = f.Name, Chemin = f.FullName, Taille = f.Length });
                else { petits++; taillePetits += f.Length; }
            }
            if (petits > 0 && taillePetits > 0) el.Add(new Pave { Libelle = petits + " autre(s) fichier(s)", Taille = taillePetits });
            var tri = el.OrderByDescending(e => e.Taille).ToList();
            if (tri.Count > 60)
            {
                double autres = tri.Skip(59).Sum(e => e.Taille);
                tri = tri.Take(59).Concat(new[] { new Pave { Libelle = (tri.Count - 59) + " autres éléments", Taille = autres } }).ToList();
            }
            totalCourant = total;
            zone.Survol = null;
            zone.Paves = ZoneCarte.Pavage(tri, 0, 0, zone.ClientSize.Width, zone.ClientSize.Height);
            lblChemin.Text = dossier + "   —   " + Format.Taille(total);
            lblInfo.Text = "Clic : entrer dans le dossier   ·   clic droit ou « Remonter » : revenir   ·   « Ouvrir dans l'Explorateur » : le dossier survolé";
            zone.Invalidate();
        }
    }

    // ================================================================ Applications préinstallées inutiles
    internal class AppInutile { public string Motif, Libelle, Description; public bool Conseille; }

    internal class FenetreAppsInutiles : FenetreTheme
    {
        // Applications publicitaires ou rarement utilisées, réinstallables depuis le Microsoft Store.
        // (Jamais : Store, Paramètres, Photos, Calculatrice, Bloc-notes, Xbox, Sécurité, Assistance rapide, antivirus...)
        private static readonly AppInutile[] Connues =
        {
            A("Microsoft.BingNews", "Actualités (Microsoft Start)", "Fil d'actualités avec publicités.", true),
            A("Microsoft.BingWeather", "Météo", "Prévisions météo de Microsoft (garde-la si tu t'en sers).", false),
            A("Microsoft.BingSearch", "Recherche Bing", "Application de recherche Bing.", true),
            A("Microsoft.GetHelp", "Obtenir de l'aide", "Assistant d'aide de Microsoft.", true),
            A("Microsoft.Getstarted", "Astuces", "Conseils de prise en main de Windows.", true),
            A("Microsoft.MicrosoftSolitaireCollection", "Microsoft Solitaire Collection", "Jeux de cartes avec publicités (garde-la si tu y joues).", false),
            A("Microsoft.People", "Contacts", "Ancienne application Contacts.", true),
            A("Microsoft.WindowsFeedbackHub", "Hub de commentaires", "Pour envoyer des avis à Microsoft.", true),
            A("Microsoft.MixedReality.Portal", "Portail de réalité mixte", "Pour les casques de réalité virtuelle Windows (abandonnés).", true),
            A("Microsoft.SkypeApp", "Skype", "Service arrêté par Microsoft en 2025.", true),
            A("Microsoft.Microsoft3DViewer", "Visionneuse 3D", "Affiche des modèles 3D.", true),
            A("Microsoft.Print3D", "Print 3D", "Impression 3D.", true),
            A("Microsoft.MSPaint", "Paint 3D", "Ancienne version 3D de Paint (Paint classique n'est pas touché).", true),
            A("Microsoft.MicrosoftOfficeHub", "Microsoft 365 (Office)", "Lanceur et publicité pour l'abonnement Office (garde-le si tu l'utilises).", false),
            A("Clipchamp.Clipchamp", "Clipchamp", "Montage vidéo en ligne.", true),
            A("Microsoft.Todos", "Microsoft To Do", "Listes de tâches (garde-la si tu t'en sers).", false),
            A("Microsoft.WindowsMaps", "Cartes", "Cartes de Microsoft.", true),
            A("Microsoft.549981C3F5F10", "Cortana", "Ancien assistant vocal, arrêté par Microsoft.", true),
            A("Microsoft.PowerAutomateDesktop", "Power Automate", "Automatisation pour les entreprises.", true),
            A("Microsoft.ZuneVideo", "Films et TV", "Ancien lecteur vidéo (le Lecteur multimédia le remplace).", true),
            A("Microsoft.Windows.DevHome", "Dev Home", "Outil pour les développeurs.", true),
            A("Microsoft.Wallet", "Microsoft Pay", "Portefeuille de paiement abandonné.", true),
            A("Microsoft.Messaging", "Messages", "Ancienne application de SMS.", true),
            A("Microsoft.OneConnect", "Forfaits mobiles", "Achat de forfaits de données mobiles.", true),
            A("Microsoft.WindowsCommunicationsApps", "Courrier et Calendrier", "Applications arrêtées par Microsoft (remplacées par le nouvel Outlook).", false),
            A("MicrosoftTeams", "Microsoft Teams (personnel)", "Version gratuite de Teams (garde-la si tu t'en sers).", false),
            A("MSTeams", "Microsoft Teams", "Garde-le si tu t'en sers pour le travail.", false),
            A("Microsoft.OutlookForWindows", "Outlook (nouveau)", "Messagerie (garde-la si tu t'en sers).", false),
            A("king.com.*", "Jeux King (Candy Crush...)", "Jeux préinstallés avec achats intégrés.", true),
            A("Disney.*", "Disney+", "Application de vidéo par abonnement.", false),
            A("Facebook.*", "Facebook / Instagram", "Réseaux sociaux préinstallés.", false),
            A("BytedancePte.Ltd.TikTok", "TikTok", "Réseau social préinstallé.", false),
            A("SpotifyAB.SpotifyMusic", "Spotify", "Musique (garde-la si tu t'en sers).", false),
            A("AmazonVideo.PrimeVideo", "Prime Video", "Vidéo par abonnement.", false),
            A("4DF9E0F8.Netflix", "Netflix", "Vidéo par abonnement.", false),
            A("*.LinkedInforWindows", "LinkedIn", "Réseau professionnel préinstallé.", false),
            A("*.Twitter", "X (Twitter)", "Réseau social préinstallé.", false)
        };
        private static AppInutile A(string m, string l, string d, bool c) { return new AppInutile { Motif = m, Libelle = l, Description = d, Conseille = c }; }

        private readonly FenetrePrincipale F;
        private readonly ListeSombre lv = new ListeSombre { CheckBoxes = true, MultiSelect = false, ShowItemToolTips = true, Location = new Point(20, 70), Size = new Size(744, 330), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Etiquette info;
        private readonly Bouton bRetirer;
        private bool travail;

        public FenetreAppsInutiles(FenetrePrincipale f) : base("Applications préinstallées inutiles", 800, 540, true)
        {
            F = f;
            Controls.Add(new Etiquette("Applications livrées avec Windows ou par le fabricant, souvent publicitaires. Celles conseillées sont cochées ; garde celles dont tu te sers. Chacune se réinstalle en un clic depuis le Microsoft Store (et depuis l'Historique).", Theme.Normal, Role.Discret, 20, 14) { MaximumSize = new Size(740, 0) });
            lv.Colonne("Application", 240); lv.Colonne("Pourquoi", 400); lv.Colonne("Conseil", 90);
            Controls.Add(lv);
            info = new Etiquette("Recherche des applications installées...", Theme.Normal, Role.AccentVif, 20, 410) { AutoSize = false, Size = new Size(744, 40), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            bRetirer = new Bouton("Retirer les applications cochées", true, 270, 34) { Location = new Point(20, 456), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Enabled = false };
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(664, 456), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            bRetirer.Click += (s, e) => Retirer();
            Controls.AddRange(new Control[] { info, bRetirer, bFermer });
            CancelButton = bFermer;
            lv.ItemChecked += (s, e) => Compter();
            Theme.Appliquer(this);
            Shown += (s, e) => Charger();
            FormClosing += (s, e) => { if (travail) { e.Cancel = true; BoiteMessage.Info("Attends la fin de la désinstallation en cours."); } };
        }

        private static bool Correspond(string motif, string nom)
        {
            string re = "^" + Regex.Escape(motif).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(nom, re, RegexOptions.IgnoreCase);
        }

        private void Charger()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                List<Tuple<Paquet, AppInutile>> l = new List<Tuple<Paquet, AppInutile>>();
                try
                {
                    foreach (var p in Paquets.Lister(true))
                    {
                        if (p.Cadre || p.Systeme) continue;
                        var a = Connues.FirstOrDefault(c => Correspond(c.Motif, p.Nom));
                        if (a != null) l.Add(Tuple.Create(p, a));
                    }
                }
                catch (Exception ex) { JournalDemarrage.Erreur("applications préinstallées", ex); }
                try { BeginInvoke(new Action(() => Afficher(l))); } catch { }
            });
        }

        private void Afficher(List<Tuple<Paquet, AppInutile>> l)
        {
            if (IsDisposed) return;
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var x in l.OrderByDescending(x => x.Item2.Conseille).ThenBy(x => x.Item2.Libelle))
            {
                string nom = x.Item2.Motif.Contains("*") && x.Item1.NomAffiche != x.Item1.Nom ? x.Item1.NomAffiche : x.Item2.Libelle;
                var it = lv.Ajouter(x, nom, x.Item2.Description, x.Item2.Conseille ? "Retirer" : "À toi de voir");
                it.Checked = x.Item2.Conseille;
                it.ToolTipText = x.Item1.NomComplet;
            }
            lv.EndUpdate();
            if (l.Count == 0) { info.Text = "✔ Aucune application préinstallée inutile sur ce PC."; info.RoleTexte = Role.Vert; info.AppliquerTheme(); }
            Compter();
        }

        private void Compter()
        {
            if (lv.Items.Count == 0) { bRetirer.Enabled = false; return; }
            int n = lv.CheckedItems.Count;
            info.Text = lv.Items.Count + " application(s) trouvée(s), " + n + " cochée(s).";
            info.RoleTexte = Role.Discret; info.AppliquerTheme();
            bRetirer.Enabled = n > 0 && !travail;
        }

        private void Retirer()
        {
            var choix = lv.CheckedItems.Cast<ListViewItem>().Select(i => (Tuple<Paquet, AppInutile>)i.Tag).ToList();
            if (choix.Count == 0) return;
            string liste = string.Join("\n", choix.Select(c => "- " + (c.Item2.Motif.Contains("*") ? c.Item1.NomAffiche : c.Item2.Libelle)));
            if (F.ChkSimu.Checked) { BoiteMessage.Info("Mode simulation : ces applications seraient retirées :\n\n" + liste); return; }
            if (!BoiteMessage.Question("Retirer ces applications de ton compte ?\n\n" + liste + "\n\nTu pourras les réinstaller depuis le Microsoft Store (l'Historique t'y emmène).")) return;
            travail = true; bRetirer.Enabled = false; Cursor = Cursors.WaitCursor;
            System.Threading.Tasks.Task.Run(() =>
            {
                var echecs = new List<string>(); int ok = 0;
                foreach (var c in choix)
                {
                    string nom = c.Item2.Motif.Contains("*") ? c.Item1.NomAffiche : c.Item2.Libelle;
                    try { BeginInvoke(new Action(() => { info.Text = "Désinstallation de « " + nom + " »..."; info.RoleTexte = Role.AccentVif; info.AppliquerTheme(); })); } catch { }
                    string r = Paquets.Retirer(c.Item1.NomComplet);
                    if (r == "") { ok++; Historique.Ajouter("appx", "Application retirée : " + nom, new Dictionary<string, object> { { "Nom", c.Item1.Nom }, { "Libelle", nom }, { "Paquet", c.Item1.NomComplet } }); }
                    else echecs.Add(nom + " : " + r);
                }
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        travail = false; Cursor = Cursors.Default;
                        string msg = ok + " application(s) retirée(s).";
                        if (echecs.Count > 0) msg += "\n\nNon retirée(s) :\n- " + string.Join("\n- ", echecs);
                        BoiteMessage.Info(msg);
                        Charger();
                    }));
                }
                catch { }
            });
        }
    }

    // ================================================================ Anciens pilotes
    internal class FenetreAnciensPilotes : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly ListeSombre lv = new ListeSombre { CheckBoxes = true, MultiSelect = false, ShowItemToolTips = true, Location = new Point(20, 70), Size = new Size(904, 360), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Etiquette info;
        private readonly Bouton bRetirer;

        public FenetreAnciensPilotes(FenetrePrincipale f) : base("Anciens pilotes", 960, 560, true)
        {
            F = f;
            Controls.Add(new Etiquette("À chaque mise à jour d'un pilote, Windows garde l'ancienne version « au cas où ». Ces versions inutilisées peuvent être retirées : la plus récente de chaque pilote et celles que Windows utilise sont toujours gardées.", Theme.Normal, Role.Discret, 20, 14) { MaximumSize = new Size(900, 0) });
            lv.Colonne("Fournisseur", 220); lv.Colonne("Type", 130); lv.Colonne("Version", 150); lv.Colonne("Date", 100); lv.Colonne("Taille", 90, HorizontalAlignment.Right); lv.Colonne("État", 190); lv.ColonneElastique = 0;
            lv.ActiverTri();
            Controls.Add(lv);
            info = new Etiquette("Analyse du magasin de pilotes...", Theme.Normal, Role.AccentVif, 20, 440) { AutoSize = false, Size = new Size(904, 22), Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
            bRetirer = new Bouton("Retirer les versions cochées", true, 260, 34) { Location = new Point(20, 474), Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Enabled = false };
            Infobulle.Definir(bRetirer, "Windows te demandera l'autorisation (bouclier) : clique sur « Oui ». Un pilote encore utilisé est toujours gardé (Windows refuse de le retirer).");
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(824, 474), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            bRetirer.Click += (s, e) => Retirer();
            Controls.AddRange(new Control[] { info, bRetirer, bFermer });
            CancelButton = bFermer;
            lv.ItemChecked += (s, e) => { if (e.Item.Tag is PilotesAnciens.Pilote && !((PilotesAnciens.Pilote)e.Item.Tag).Supprimable && e.Item.Checked) e.Item.Checked = false; Compter(); };
            Theme.Appliquer(this);
            Shown += (s, e) => Charger();
        }

        private void Charger()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                List<PilotesAnciens.Pilote> l; try { l = PilotesAnciens.Analyser(); } catch (Exception ex) { JournalDemarrage.Erreur("anciens pilotes", ex); l = new List<PilotesAnciens.Pilote>(); }
                try { BeginInvoke(new Action(() => Afficher(l))); } catch { }
            });
        }

        private void Afficher(List<PilotesAnciens.Pilote> l)
        {
            if (IsDisposed) return;
            lv.BeginUpdate(); lv.Items.Clear();
            // les versions retirables d'abord, puis les versions gardées des mêmes pilotes (pour comparer)
            var familles = new HashSet<string>(l.Where(p => p.Supprimable).Select(p => p.Original), StringComparer.OrdinalIgnoreCase);
            foreach (var p in l.Where(p => p.Supprimable || (familles.Contains(p.Original) && p.PlusRecent)).OrderByDescending(p => p.Supprimable).ThenByDescending(p => p.Taille))
            {
                string etat = p.Supprimable ? "Ancienne version, inutilisée" : p.Utilise ? "Utilisée : gardée" : "Plus récente : gardée";
                var it = lv.Ajouter(p, p.Fournisseur, p.Classe, p.Version, p.DateTexte, Format.Taille(p.Taille), etat);
                it.SubItems[3].Tag = p.Date; it.SubItems[4].Tag = p.Taille;
                it.Checked = p.Supprimable;
                if (!p.Supprimable) it.ForeColor = Theme.P.Discret;
                it.ToolTipText = p.Inf + " · " + p.Original;
            }
            lv.EndUpdate();
            if (!l.Any(p => p.Supprimable)) { info.Text = "✔ Aucune ancienne version de pilote à retirer : le magasin de pilotes est propre."; info.RoleTexte = Role.Vert; info.AppliquerTheme(); bRetirer.Enabled = false; return; }
            Compter();
        }

        private void Compter()
        {
            var c = lv.CheckedItems.Cast<ListViewItem>().Select(i => (PilotesAnciens.Pilote)i.Tag).ToList();
            if (lv.Items.Count == 0) return;
            info.Text = c.Count + " ancienne(s) version(s) cochée(s) : " + Format.Taille(c.Sum(p => p.Taille)) + " à récupérer.";
            info.RoleTexte = Role.Discret; info.AppliquerTheme();
            bRetirer.Enabled = c.Count > 0 && !F.ActionEnCours;
        }

        private void Retirer()
        {
            var c = lv.CheckedItems.Cast<ListViewItem>().Select(i => (PilotesAnciens.Pilote)i.Tag).Where(p => p.Supprimable).ToList();
            if (c.Count == 0) return;
            var args = new List<string>();
            foreach (var p in c) { args.Add("--pilote"); args.Add(p.Inf); }
            string conf = "Retirer " + c.Count + " ancienne(s) version(s) de pilotes (" + Format.Taille(c.Sum(p => p.Taille)) + ") ?\n\nLa version la plus récente de chaque pilote est gardée, et Windows refuse de retirer un pilote encore utilisé. Un point de restauration n'est pas nécessaire : ces versions ne servent plus.";
            ArgsChoisis = args; Confirmation = conf;
            DialogResult = DialogResult.OK;
        }

        public List<string> ArgsChoisis; public string Confirmation;

        public static void Ouvrir(FenetrePrincipale f)
        {
            List<string> args = null; string conf = null;
            using (var w = new FenetreAnciensPilotes(f)) { if (w.ShowDialog(f) == DialogResult.OK) { args = w.ArgsChoisis; conf = w.Confirmation; } }
            if (args != null) f.LancerAction("pilotes", args, conf);
        }
    }
}
