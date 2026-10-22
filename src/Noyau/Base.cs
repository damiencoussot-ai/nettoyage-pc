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
using System.Web.Script.Serialization;

namespace NettoyagePC
{
    internal static class Appli
    {
        public const string Nom = "Nettoyage PC";
        public static readonly string Version = "4.6";
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
        public static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    }

    internal static class Chemins
    {
        public static string Exe { get { return Assembly.GetExecutingAssembly().Location; } }
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

    /// <summary>Lecture et écriture JSON (compatible avec les fichiers écrits par la version PowerShell).</summary>
    internal static class Json
    {
        private static JavaScriptSerializer Serialiseur()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
        }

        public static object Lire(string texte)
        {
            if (string.IsNullOrWhiteSpace(texte)) return null;
            try { return Serialiseur().DeserializeObject(texte.TrimStart('﻿')); } catch { return null; }
        }

        public static object LireFichier(string chemin)
        {
            try { return File.Exists(chemin) ? Lire(File.ReadAllText(chemin, Encoding.UTF8)) : null; } catch { return null; }
        }

        public static string Ecrire(object valeur)
        {
            return Indenter(Serialiseur().Serialize(valeur));
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

        // Mise en forme lisible (le format compact de JavaScriptSerializer tient sur une ligne)
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
            if (octets >= 1073741824.0) return (octets / 1073741824.0).ToString("N2", Appli.Fr) + (fr ? " Go" : " GB");
            if (octets >= 1048576.0) return (octets / 1048576.0).ToString("N1", Appli.Fr) + (fr ? " Mo" : " MB");
            return (octets / 1024.0).ToString("N0", Appli.Fr) + (fr ? " Ko" : " KB");
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
