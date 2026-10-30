// Nettoyage PC - dossiers, journal de démarrage, JSON, paramètres
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Principal;
using System.Text;

namespace NettoyagePC
{
    internal static class Appli
    {
        public const string Nom = "Nettoyage PC";
#if !NET5_0_OR_GREATER
        public static readonly string Version = "4.6";   // version 4 (lu par la fabrication : .github/workflows/fabrication.yml)
#else
        public static readonly string Version = "5.1.1";   // version 5
#endif
        public const string Depot = "damiencoussot-ai/nettoyage-pc";

        public static bool EstAdmin
        {
            get
            {
                try { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); }
                catch { return false; }
            }
        }

        public static bool SousMono { get { return Type.GetType("Mono.Runtime") != null; } }

        /// <summary>Dossier de l'essai automatique de l'interface (null en temps normal) : fixé au démarrage par l'interface.</summary>
        public static string DossierAutotest;
        public static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    }

    /// <summary>
    /// Encodage « ANSI » de Windows (page de code du système, 1252 en France), utilisé par les vieux outils et fichiers INF.
    /// .NET Framework : Encoding.Default. .NET 10 : Encoding.Default est l'UTF-8, on prend donc la vraie page de code.
    /// </summary>
    internal static class Encodages
    {
#if NET5_0_OR_GREATER
        private static Encoding ansi;
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern int GetACP();
        public static Encoding Ansi
        {
            get
            {
                if (ansi != null) return ansi;
                try
                {
                    if (OperatingSystem.IsWindows())
                    {
                        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                        ansi = Encoding.GetEncoding(GetACP());
                    }
                }
                catch { }
                return ansi ?? (ansi = Encoding.UTF8);
            }
        }
#else
        public static Encoding Ansi { get { return Encoding.Default; } }
#endif
    }

    internal static class Chemins
    {
#if NET5_0_OR_GREATER
        // version 5 (un seul fichier) : l'emplacement de l'assembly est vide, on prend celui du programme
        public static string Exe { get { return Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "NettoyagePC.exe"); } }
#else
        public static string Exe { get { return Assembly.GetExecutingAssembly().Location; } }
#endif
        public static string Ici { get { return Path.GetDirectoryName(Exe); } }

        /// <summary>Documents de la personne qui utilise l'application (transmis par l'interface au moteur élevé).</summary>
        public static string Documents
        {
            get
            {
                string e = Environment.GetEnvironmentVariable("NP_DOCUMENTS");
                if (!string.IsNullOrEmpty(e) && Directory.Exists(e)) return e;
                return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }
        }

        public static string Telechargements
        {
            get
            {
                string e = Environment.GetEnvironmentVariable("NP_TELECHARGEMENTS");
                if (!string.IsNullOrEmpty(e) && Directory.Exists(e)) return e;
                string k = Natif.DossierConnu(Natif.DossierTelechargements);
                if (!string.IsNullOrEmpty(k)) return k;
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            }
        }

        /// <summary>Documents\NettoyagePC : journaux, rapports, historique, réglages (mêmes fichiers que la version 3).</summary>
        public static string Donnees { get { return Path.Combine(Documents, "NettoyagePC"); } }
        public static string Fichier(string nom) { return Path.Combine(Donnees, nom); }

        /// <summary>Dossier local de l'application (journal de démarrage, état).</summary>
        public static string Local { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NettoyagePC"); } }

        public static string Windows
        {
            get
            {
                string w = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                if (!string.IsNullOrEmpty(w)) return w;
                w = Environment.GetEnvironmentVariable("windir");
                if (!string.IsNullOrEmpty(w)) return w;
                return Natif.EstWindows ? @"C:\Windows" : Path.Combine(Path.GetTempPath(), "np-windows");   // (essais hors Windows)
            }
        }
        public static string Systeme32 { get { return Path.Combine(Windows, "System32"); } }
        // (surchargés quand l'autorisation est donnée avec le compte d'un autre administrateur)
        private static string Surcharge(string variable, Environment.SpecialFolder dossier)
        {
            string e = Environment.GetEnvironmentVariable(variable);
            return !string.IsNullOrEmpty(e) && Directory.Exists(e) ? e : Environment.GetFolderPath(dossier);
        }
        public static string LocalAppData { get { return Surcharge("NP_LOCALAPPDATA", Environment.SpecialFolder.LocalApplicationData); } }
        public static string AppData { get { return Surcharge("NP_APPDATA", Environment.SpecialFolder.ApplicationData); } }
        public static string Profil { get { return Surcharge("NP_PROFIL", Environment.SpecialFolder.UserProfile); } }
        public static string ProgramFiles { get { return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles); } }

        /// <summary>Vrai si l'application est installée dans Program Files (dossier modifiable par les seuls administrateurs).</summary>
        public static bool EstInstalle
        {
            get
            {
                foreach (var r in new[] { Environment.GetEnvironmentVariable("ProgramFiles"), Environment.GetEnvironmentVariable("ProgramFiles(x86)"), Environment.GetEnvironmentVariable("ProgramW6432") })
                {
                    if (string.IsNullOrEmpty(r)) continue;
                    if (Ici.StartsWith(r.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) return true;
                }
                return false;
            }
        }

        public static void CreerDonnees() { try { Directory.CreateDirectory(Donnees); } catch { } }
    }

    /// <summary>Journal de démarrage (%LOCALAPPDATA%\NettoyagePC\demarrage.log) : chaque étape et toute erreur.</summary>
    internal static class JournalDemarrage
    {
        private static readonly object verrou = new object();
        public static string Fichier { get { return Path.Combine(Chemins.Local, "demarrage.log"); } }

        public static void Preparer()
        {
            try
            {
                Directory.CreateDirectory(Chemins.Local);
                var fi = new FileInfo(Fichier);
                if (fi.Exists && fi.Length > 200 * 1024) File.Copy(Fichier, Fichier + ".ancien", true);
                if (fi.Exists && fi.Length > 200 * 1024) File.Delete(Fichier);
            }
            catch { }
        }

        public static void Noter(string texte)
        {
            try
            {
                lock (verrou)
                    File.AppendAllText(Fichier, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + texte + "\r\n", new UTF8Encoding(false));
            }
            catch { }
        }

        public static void Erreur(string contexte, Exception ex)
        {
            Noter("ERREUR (" + contexte + ") : " + (ex == null ? "?" : ex.GetType().Name + " : " + ex.Message + " | " + (ex.StackTrace ?? "").Replace("\r", "").Replace("\n", " | ")));
        }
    }

    /// <summary>
    /// Lecture et écriture JSON, sans dépendance : même résultat sous .NET Framework (version 4) et sous .NET 10 (version 5).
    /// Compatible avec les fichiers déjà écrits (par la version PowerShell et par JavaScriptSerializer) :
    /// objets → Dictionary&lt;string, object&gt;, tableaux → object[], nombres → int, long ou decimal, dates « \/Date(ms)\/ ».
    /// </summary>
    internal static class Json
    {
        private const int ProfondeurMax = 64;

        public static object Lire(string texte)
        {
            if (string.IsNullOrWhiteSpace(texte)) return null;
            try { return new LecteurJson(texte.TrimStart('\uFEFF')).Tout(); } catch { return null; }
        }

        public static object LireFichier(string chemin)
        {
            try { return File.Exists(chemin) ? Lire(File.ReadAllText(chemin, Encoding.UTF8)) : null; } catch { return null; }
        }

        /// <summary>JSON lisible (indenté), pour les fichiers.</summary>
        public static string Ecrire(object valeur)
        {
            return Indenter(Compact(valeur));
        }

        /// <summary>JSON sur une ligne, pour les échanges (requêtes, clés).</summary>
        public static string Compact(object valeur)
        {
            var sb = new StringBuilder();
            EcrireValeur(sb, valeur, 0);
            return sb.ToString();
        }

        private static readonly DateTime Epoque = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static void EcrireValeur(StringBuilder sb, object v, int profondeur)
        {
            if (profondeur > ProfondeurMax) throw new InvalidOperationException("JSON : profondeur maximale dépassée");
            if (v == null || v is DBNull) { sb.Append("null"); return; }
            if (v is string) { EcrireTexte(sb, (string)v); return; }
            if (v is bool) { sb.Append((bool)v ? "true" : "false"); return; }
            if (v is char) { EcrireTexte(sb, v.ToString()); return; }
            if (v is DateTime)
            {
                var d = ((DateTime)v).ToUniversalTime();
                sb.Append("\"\\/Date(").Append(((long)(d - Epoque).TotalMilliseconds).ToString(CultureInfo.InvariantCulture)).Append(")\\/\"");
                return;
            }
            if (v is double || v is float)
            {
                double x = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (double.IsNaN(x) || double.IsInfinity(x)) { sb.Append("null"); return; }
                sb.Append(v is float ? ((float)v).ToString("r", CultureInfo.InvariantCulture) : x.ToString("r", CultureInfo.InvariantCulture));
                return;
            }
            if (v is Enum) { sb.Append(Convert.ToInt64(v, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)); return; }
            if (v is decimal || v is int || v is long || v is short || v is byte || v is sbyte || v is uint || v is ulong || v is ushort)
            {
                sb.Append(((IFormattable)v).ToString(null, CultureInfo.InvariantCulture));
                return;
            }
            if (v is Guid || v is Uri) { EcrireTexte(sb, v.ToString()); return; }
            var dict = v as IDictionary;
            if (dict != null)
            {
                sb.Append('{');
                bool premier = true;
                foreach (DictionaryEntry e in dict)
                {
                    if (!premier) sb.Append(',');
                    premier = false;
                    EcrireTexte(sb, Convert.ToString(e.Key, CultureInfo.InvariantCulture));
                    sb.Append(':');
                    EcrireValeur(sb, e.Value, profondeur + 1);
                }
                sb.Append('}');
                return;
            }
            var liste = v as IEnumerable;
            if (liste != null)
            {
                sb.Append('[');
                bool premier = true;
                foreach (var x in liste)
                {
                    if (!premier) sb.Append(',');
                    premier = false;
                    EcrireValeur(sb, x, profondeur + 1);
                }
                sb.Append(']');
                return;
            }
            // objet : champs puis propriétés publics (comme JavaScriptSerializer)
            sb.Append('{');
            bool prem = true;
            var t = v.GetType();
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!prem) sb.Append(',');
                prem = false;
                EcrireTexte(sb, f.Name); sb.Append(':'); EcrireValeur(sb, f.GetValue(v), profondeur + 1);
            }
            foreach (var pr in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!pr.CanRead || pr.GetIndexParameters().Length > 0) continue;
                object x;
                try { x = pr.GetValue(v, null); } catch { continue; }
                if (!prem) sb.Append(',');
                prem = false;
                EcrireTexte(sb, pr.Name); sb.Append(':'); EcrireValeur(sb, x, profondeur + 1);
            }
            sb.Append('}');
        }

        private static void EcrireTexte(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    default:
                        if (c < ' ' || c == '\u2028' || c == '\u2029') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        private sealed class LecteurJson
        {
            private readonly string s;
            private int i;
            public LecteurJson(string texte) { s = texte; }

            public object Tout()
            {
                var v = Valeur(0);
                Blancs();
                if (i < s.Length) throw Erreur("caractères en trop");
                return v;
            }

            private Exception Erreur(string quoi) { return new FormatException("JSON invalide (" + quoi + ", position " + i + ")"); }

            private void Blancs() { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

            private object Valeur(int profondeur)
            {
                if (profondeur > ProfondeurMax) throw Erreur("profondeur");
                Blancs();
                if (i >= s.Length) throw Erreur("fin inattendue");
                char c = s[i];
                if (c == '{') return Objet(profondeur);
                if (c == '[') return Tableau(profondeur);
                if (c == '"' || c == '\'') return TexteOuDate();
                if (c == '-' || (c >= '0' && c <= '9')) return Nombre();
                if (Mot("true")) return true;
                if (Mot("false")) return false;
                if (Mot("null")) return null;
                throw Erreur("valeur inconnue");
            }

            private bool Mot(string m)
            {
                if (string.CompareOrdinal(s, i, m, 0, m.Length) != 0) return false;
                i += m.Length;
                return true;
            }

            private Dictionary<string, object> Objet(int profondeur)
            {
                var d = new Dictionary<string, object>();
                i++;
                Blancs();
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Blancs();
                    if (i >= s.Length || (s[i] != '"' && s[i] != '\'')) throw Erreur("nom attendu");
                    string cle = Texte();
                    Blancs();
                    if (i >= s.Length || s[i] != ':') throw Erreur("« : » attendu");
                    i++;
                    d[cle] = Valeur(profondeur + 1);
                    Blancs();
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == '}') { i++; return d; }
                    throw Erreur("« , » ou « } » attendu");
                }
            }

            private object[] Tableau(int profondeur)
            {
                var l = new List<object>();
                i++;
                Blancs();
                if (i < s.Length && s[i] == ']') { i++; return l.ToArray(); }
                while (true)
                {
                    l.Add(Valeur(profondeur + 1));
                    Blancs();
                    if (i < s.Length && s[i] == ',') { i++; continue; }
                    if (i < s.Length && s[i] == ']') { i++; return l.ToArray(); }
                    throw Erreur("« , » ou « ] » attendu");
                }
            }

            private static readonly System.Text.RegularExpressions.Regex DateJson =
                new System.Text.RegularExpressions.Regex(@"^\\/Date\((-?[0-9]+)(?:[a-zA-Z]|(?:\+|-)[0-9]{4})?\)\\/$");

            private object TexteOuDate()
            {
                int debut = i;
                string t = Texte();
                // « \/Date(ms)\/ » (barres échappées dans le fichier) : date, comme JavaScriptSerializer
                if (t.StartsWith("/Date(", StringComparison.Ordinal))
                {
                    var m = DateJson.Match(s.Substring(debut + 1, i - debut - 2));
                    long ms;
                    if (m.Success && long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out ms))
                        return Epoque.AddMilliseconds(ms);
                }
                return t;
            }

            private string Texte()
            {
                char fin = s[i++];
                var sb = new StringBuilder();
                while (true)
                {
                    if (i >= s.Length) throw Erreur("texte non terminé");
                    char c = s[i++];
                    if (c == fin) return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (i >= s.Length) throw Erreur("échappement");
                    char e = s[i++];
                    switch (e)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Erreur("\\u incomplet");
                            int code;
                            if (!int.TryParse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out code)) throw Erreur("\\u invalide");
                            sb.Append((char)code); i += 4; break;
                        default: sb.Append(e); break;   // \" \\ \/ \'
                    }
                }
            }

            private object Nombre()
            {
                int debut = i;
                if (s[i] == '-') i++;
                while (i < s.Length && ((s[i] >= '0' && s[i] <= '9') || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
                string n = s.Substring(debut, i - debut);
                int a; long b; decimal c; double d;
                if (int.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out a)) return a;
                if (long.TryParse(n, NumberStyles.Integer, CultureInfo.InvariantCulture, out b)) return b;
                if (decimal.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out c)) return c;
                if (double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
                throw Erreur("nombre invalide");
            }
        }

        /// <summary>Liste d'objets JSON (un tableau, ou un objet seul).</summary>
        public static List<Dictionary<string, object>> Objets(object valeur)
        {
            var l = new List<Dictionary<string, object>>();
            if (valeur is Dictionary<string, object>) { l.Add((Dictionary<string, object>)valeur); return l; }
            var e = valeur as IEnumerable;
            if (e != null && !(valeur is string)) foreach (var x in e) { var d = x as Dictionary<string, object>; if (d != null) l.Add(d); }
            return l;
        }

        public static Dictionary<string, object> Dict(Dictionary<string, object> d, string cle)
        {
            object v; return d != null && d.TryGetValue(cle, out v) ? v as Dictionary<string, object> : null;
        }

        public static string Texte(Dictionary<string, object> d, string cle, string defaut = "")
        {
            object v;
            if (d == null || !d.TryGetValue(cle, out v) || v == null) return defaut;
            if (v is IFormattable) return ((IFormattable)v).ToString(null, CultureInfo.InvariantCulture);
            return v.ToString();
        }

        public static bool Bool(Dictionary<string, object> d, string cle, bool defaut = false)
        {
            object v;
            if (d == null || !d.TryGetValue(cle, out v) || v == null) return defaut;
            if (v is bool) return (bool)v;
            bool b; if (bool.TryParse(v.ToString(), out b)) return b;
            return defaut;
        }

        public static long Long(Dictionary<string, object> d, string cle, long defaut = 0)
        {
            object v;
            if (d == null || !d.TryGetValue(cle, out v) || v == null) return defaut;
            try { return Convert.ToInt64(v, CultureInfo.InvariantCulture); } catch { return defaut; }
        }

        public static double Double(Dictionary<string, object> d, string cle, double defaut = 0)
        {
            object v;
            if (d == null || !d.TryGetValue(cle, out v) || v == null) return defaut;
            try { return Convert.ToDouble(v, CultureInfo.InvariantCulture); } catch { return defaut; }
        }

        public static object Valeur(Dictionary<string, object> d, string cle)
        {
            object v; return d != null && d.TryGetValue(cle, out v) ? v : null;
        }

        public static List<string> Textes(Dictionary<string, object> d, string cle)
        {
            var l = new List<string>();
            object v;
            if (d == null || !d.TryGetValue(cle, out v) || v == null) return l;
            if (v is string) { l.Add((string)v); return l; }
            var e = v as IEnumerable;
            if (e != null) foreach (var x in e) if (x != null) l.Add(x.ToString());
            return l;
        }

        // Mise en forme lisible (le format compact tient sur une ligne)
        private static string Indenter(string json)
        {
            var sb = new StringBuilder(json.Length + json.Length / 4);
            int niveau = 0; bool chaine = false;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (chaine)
                {
                    sb.Append(c);
                    if (c == '\\' && i + 1 < json.Length) { sb.Append(json[++i]); continue; }
                    if (c == '"') chaine = false;
                    continue;
                }
                switch (c)
                {
                    case '"': chaine = true; sb.Append(c); break;
                    case '{': case '[':
                        sb.Append(c);
                        if (i + 1 < json.Length && (json[i + 1] == '}' || json[i + 1] == ']')) { sb.Append(json[++i]); break; }
                        niveau++; sb.Append("\r\n").Append(' ', niveau * 4); break;
                    case '}': case ']': niveau--; sb.Append("\r\n").Append(' ', Math.Max(0, niveau) * 4).Append(c); break;
                    case ',': sb.Append(c).Append("\r\n").Append(' ', niveau * 4); break;
                    case ':': sb.Append(": "); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>Réglages de l'application (Documents\NettoyagePC\parametres.json).</summary>
    internal class Parametres
    {
        public bool VerifAuto = true;
        public string DerniereVersionLancee = "";
        public string Theme = "Sombre";   // Auto, Clair, Sombre
        public string Langue = "fr";
        public string NouveautesVues = "";
        public bool ModeSimple;
        public string Accent = "aurore";   // couleur de l'interface (les autres qu'« aurore » font partie de Premium)
        public string OptionsOuvertes = "";   // pages dont « Plus d'options » est ouvert (clés séparées par des virgules)
        // Profil : pseudo affiché à l'accueil. Tout reste sur ce PC.
        public string Pseudo = "";
        public bool ProfilCree;

        public static string Fichier { get { return Chemins.Fichier("parametres.json"); } }

        public static Parametres Lire()
        {
            var p = new Parametres();
            var d = Json.LireFichier(Fichier) as Dictionary<string, object>;
            if (d != null)
            {
                p.VerifAuto = Json.Bool(d, "VerifAuto", true);
                p.DerniereVersionLancee = Json.Texte(d, "DerniereVersionLancee");
                string t = Json.Texte(d, "Theme");
                if (t == "Auto" || t == "Clair" || t == "Sombre") p.Theme = t;
                string l = Json.Texte(d, "Langue");
                if (l == "fr" || l == "en" || l == "es") p.Langue = l;
                p.NouveautesVues = Json.Texte(d, "NouveautesVues");
                p.ModeSimple = Json.Bool(d, "ModeSimple");
                string a = Json.Texte(d, "Accent"); if (System.Text.RegularExpressions.Regex.IsMatch(a, "^[a-z]{2,20}$")) p.Accent = a;
                string o = Json.Texte(d, "OptionsOuvertes"); if (System.Text.RegularExpressions.Regex.IsMatch(o, "^[A-Za-z,]{0,300}$")) p.OptionsOuvertes = o;
                p.Pseudo = Profil.Nettoyer(Json.Texte(d, "Pseudo"));
                p.ProfilCree = Json.Bool(d, "ProfilCree");
            }
            return p;
        }

        public void Ecrire()
        {
            try
            {
                Chemins.CreerDonnees();
                var d = new Dictionary<string, object> { { "VerifAuto", VerifAuto }, { "DerniereVersionLancee", DerniereVersionLancee }, { "Theme", Theme }, { "Langue", Langue }, { "NouveautesVues", NouveautesVues }, { "ModeSimple", ModeSimple }, { "Accent", Accent }, { "OptionsOuvertes", OptionsOuvertes },
                    { "Pseudo", Pseudo }, { "ProfilCree", ProfilCree } };
                FichierSur.EcrireInterface(Fichier, Json.Ecrire(d));
            }
            catch { }
        }
    }

    internal static class Format
    {
        /// <summary>Unités de taille dans la langue de l'application (« Mo » en français, « MB » sinon), précédées d'une espace.</summary>
        public static string Ko { get { return Langue.Code == "fr" ? " Ko" : " KB"; } }
        public static string Mo { get { return Langue.Code == "fr" ? " Mo" : " MB"; } }
        public static string Go { get { return Langue.Code == "fr" ? " Go" : " GB"; } }

        public static string Taille(double octets)
        {
            bool fr = Langue.Code == "fr";
            var c = fr ? Appli.Fr : Langue.Culture;   // 27,60 Go en français, 27.60 GB en anglais
            // au-delà de 1 To (disques de 2 To et plus) : « 1,14 To » plutôt que « 1 171,97 Go »
            if (octets >= 1099511627776.0) return (octets / 1099511627776.0).ToString("N2", c) + (fr ? " To" : " TB");
            if (octets >= 1073741824.0) return (octets / 1073741824.0).ToString("N2", c) + (fr ? " Go" : " GB");
            if (octets >= 1048576.0) return (octets / 1048576.0).ToString("N1", c) + (fr ? " Mo" : " MB");
            return (octets / 1024.0).ToString("N0", c) + (fr ? " Ko" : " KB");
        }

        public static string Nombre(double n, int decimales = 0) { return n.ToString("N" + decimales, Appli.Fr); }

        public static string SansAccents(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var n = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(n.Length);
            foreach (char c in n) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }
    }
}
