// Nettoyage PC - licence Premium : clés achetées (paiement Stripe, clés créées par le service de licences de l'éditeur,
// même format que l'API de Lemon Squeezy, vérifiées en ligne)
// et clés offertes (signées par l'éditeur, vérifiées sans Internet).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    /// <summary>Réglages de la boutique. Ces valeurs sont publiques : elles ne sont pas des secrets.</summary>
    internal static class Boutique
    {
        // Paiement : lien Stripe (buy.stripe.com) ; clés : service de licences de l'éditeur (Cloudflare Worker, dossier licences).
        // Peuvent aussi être donnés dans mise-a-jour.json (installation dans Program Files) :
        //   "boutique": { "achat": "https://buy.stripe.com/...", "licences": "https://....workers.dev" }
        // (store_id / product_id : 1 et 1 avec le service de l'éditeur ; les numéros Lemon Squeezy sinon)
        private const long IdBoutiqueIntegre = 0;
        private const long IdProduitIntegre = 0;
        private const string UrlAchatIntegree = "";
        private const string ServeurLicencesIntegre = "";
        public const string Prix = "9,99 €";
        public const int NombrePc = 3;

        // Clé publique de l'éditeur pour les clés offertes (la clé privée reste chez l'éditeur)
        public const string ClePublique = "<RSAKeyValue><Modulus>1zhtjjVKgXjNeNiI7hkuZ51S4ULrLslMZ6JvrUnjv3ch8gL+8txdDFwY+XnN4LDnCDlMJ5d6RZ3yFJV0CKH9bVnBzBHe8jTnGnltQSEqwURj7suhNXNUTNVY+os0+vcgmr3MSSMoX9ruqsKVjk1dEYiMsCQJdA/Bd/n9MKD+d2fNSYvH9+NPnfYdSNqGHj9aG0pa/shjHU9RR8stR5UeWFxSpTjqg0O3bXYRDzTSAS3HCRwwe3ZzkvCoe8eCXUekDb3EwTJfSD+ShhhO3QisKBtVLbOttaR/7A+/5XYayGGFUIVneaXo6acPVGd6JuZ38ctqpIiaTnl6SB6NOEcsgQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

        private static Dictionary<string, object> config;
        private static Dictionary<string, object> Config
        {
            get
            {
                if (config != null) return config;
                config = new Dictionary<string, object>();
                try
                {
                    // (essais hors Windows uniquement) boutique de test donnée par NP_BOUTIQUE, au format de "boutique"
                    string essai = Natif.EstWindows ? null : Environment.GetEnvironmentVariable("NP_BOUTIQUE");
                    if (!string.IsNullOrEmpty(essai)) { config = Json.Lire(essai) as Dictionary<string, object> ?? config; return config; }
                    // seulement pour une installation dans Program Files (fichier modifiable par les administrateurs uniquement)
                    if (Chemins.EstInstalle)
                    {
                        var d = Json.LireFichier(Path.Combine(Chemins.Ici, "mise-a-jour.json")) as Dictionary<string, object>;
                        var b = Json.Dict(d, "boutique");
                        if (b != null) config = b;
                    }
                }
                catch { }
                return config;
            }
        }

        public static long IdBoutique { get { long v = Json.Long(Config, "store_id"); return v > 0 ? v : IdBoutiqueIntegre > 0 ? IdBoutiqueIntegre : ServeurLicences != "" ? 1 : 0; } }
        public static long IdProduit { get { long v = Json.Long(Config, "product_id"); return v > 0 ? v : IdProduitIntegre > 0 ? IdProduitIntegre : ServeurLicences != "" ? 1 : 0; } }

        /// <summary>Service de licences de l'éditeur (adresse https sans paramètres), ou "" pour celui de Lemon Squeezy.</summary>
        public static string ServeurLicences
        {
            get
            {
                string u = Json.Texte(Config, "licences");
                if (u == "") u = ServeurLicencesIntegre;
                Uri uri;
                if (!Uri.TryCreate(u, UriKind.Absolute, out uri)) return "";
                bool local = !Natif.EstWindows && uri.IsLoopback && uri.Scheme == "http";   // (essais hors Windows) service local
                return (uri.Scheme == "https" || local) && uri.Query == "" && uri.Fragment == "" ? u.TrimEnd('/') : "";
            }
        }
        /// <summary>Paiement Stripe + service de l'éditeur : la clé arrive toute seule dans l'application après le paiement.</summary>
        public static bool AchatAutomatique { get { return ServeurLicences != "" && UrlAchat.StartsWith("https://buy.stripe.com/", StringComparison.OrdinalIgnoreCase); } }
        public static string UrlAchat
        {
            get
            {
                string u = Json.Texte(Config, "achat");
                if (u == "") u = UrlAchatIntegree;
                Uri uri;
                return Uri.TryCreate(u, UriKind.Absolute, out uri) && uri.Scheme == "https"
                    && (Regex.IsMatch(uri.Host, @"(^|\.)lemonsqueezy\.com$", RegexOptions.IgnoreCase) || uri.Host.Equals("buy.stripe.com", StringComparison.OrdinalIgnoreCase)) ? u : "";
            }
        }
        public static bool Ouverte { get { return IdBoutique > 0 && IdProduit > 0; } }
    }

    internal static class Licence
    {
        public static bool Premium { get; private set; }
        public static string Type { get; private set; } = "";        // boutique, offerte
        public static string Titulaire { get; private set; } = "";
        public static string Courriel { get; private set; } = "";
        public static string CleMasquee { get; private set; } = "";
        public static string Remarque { get; private set; } = "";    // message à afficher (vérification en ligne impossible...)
        /// <summary>Essai gratuit en cours (Premium actif sans clé).</summary>
        public static bool EnEssai { get; private set; }
        public static int JoursEssaiRestants { get; private set; }
        public const int JoursEssai = 7;
        public static event Action Change;

        private static Dictionary<string, object> etat = new Dictionary<string, object>();
        private static string Fichier { get { return Path.Combine(Chemins.Local, "licence.dat"); } }
        private const int JoursEntreVerifications = 7, JoursSansInternet = 45;

        private static void Signaler() { var c = Change; if (c != null) try { c(); } catch { } }

        // ------------------------------------------------------------ Stockage (protégé par Windows pour ce compte)
        private static readonly byte[] Entropie = Encoding.UTF8.GetBytes("NettoyagePC-licence-v1");

        private static void Enregistrer()
        {
            try
            {
                Directory.CreateDirectory(Chemins.Local);
                byte[] brut = Encoding.UTF8.GetBytes(Json.Ecrire(etat));
                byte[] protege;
                try { protege = ProtectedData.Protect(brut, Entropie, DataProtectionScope.CurrentUser); }
                catch { protege = brut; }
                File.WriteAllBytes(Fichier, protege);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("enregistrement de la licence", ex); }
        }

        public static void Charger()
        {
            etat = new Dictionary<string, object>();
            try
            {
                if (File.Exists(Fichier))
                {
                    byte[] o = File.ReadAllBytes(Fichier);
                    string texte;
                    try { texte = Encoding.UTF8.GetString(ProtectedData.Unprotect(o, Entropie, DataProtectionScope.CurrentUser)); }
                    catch { texte = Encoding.UTF8.GetString(o); }
                    etat = Json.Lire(texte) as Dictionary<string, object> ?? new Dictionary<string, object>();
                }
            }
            catch { etat = new Dictionary<string, object>(); }
            Evaluer();
        }

        private static void Evaluer()
        {
            Premium = false; Remarque = ""; EnEssai = false; JoursEssaiRestants = 0;
            EvaluerCle();
            if (!Premium)
            {
                DateTime debut = DebutEssai();
                if (debut != DateTime.MinValue)
                {
                    double restant = JoursEssai - (DateTime.UtcNow - debut).TotalDays;
                    if (restant > 0 && debut <= DateTime.UtcNow.AddHours(1))
                    {
                        Premium = true; EnEssai = true; JoursEssaiRestants = (int)Math.Ceiling(restant);
                        if (Type == "") Type = "essai";
                    }
                }
            }
        }

        /// <summary>(Essai automatique de l'interface uniquement) Montre un état de licence sans rien enregistrer : "", "essai", "offerte".</summary>
        public static void SimulerPourAutotest(string type)
        {
            if (Programme.DossierAutotest == null) return;
            if (type == "") { Evaluer(); Signaler(); return; }
            Premium = true; EnEssai = type == "essai"; JoursEssaiRestants = EnEssai ? 5 : 0; Type = type; Titulaire = EnEssai ? "" : "Maman"; Remarque = "";
            Signaler();
        }

        /// <summary>Réévalue la licence (fin de l'essai pendant que l'application est ouverte) et prévient si l'état change.</summary>
        public static void Reevaluer()
        {
            bool avant = Premium; int j = JoursEssaiRestants;
            Evaluer();
            if (avant != Premium || j != JoursEssaiRestants) Signaler();
        }

        private static void EvaluerCle()
        {
            Type = Json.Texte(etat, "type"); Titulaire = Json.Texte(etat, "titulaire"); Courriel = Json.Texte(etat, "courriel");
            string cle = Json.Texte(etat, "cle");
            CleMasquee = cle.Length > 12 ? cle.Substring(0, 4) + "…" + cle.Substring(cle.Length - 4) : cle;
            if (Type == "offerte")
            {
                string titulaire;
                Premium = VerifierCleOfferte(cle, out titulaire) == "";
                if (Premium) Titulaire = titulaire;
            }
            else if (Type == "boutique")
            {
                DateTime ok;
                if (!DateTime.TryParse(Json.Texte(etat, "verifiee"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out ok)) return;
                if (Json.Bool(etat, "revoquee")) { Remarque = Json.Texte(etat, "raison"); return; }
                if ((DateTime.UtcNow - ok).TotalDays <= JoursSansInternet) Premium = true;
                else Remarque = "Connecte-toi à Internet : la licence doit être vérifiée de temps en temps.";
            }
        }

        // ------------------------------------------------------------ Essai gratuit (une fois par PC et par compte)
        private const string CleRegistreEssai = @"Software\NettoyagePC";
        private static string FichierEssai { get { return Path.Combine(Chemins.Local, "essai.dat"); } }
        private static readonly byte[] EntropieEssai = Encoding.UTF8.GetBytes("NettoyagePC-essai-v1");

        private static string Proteger(string s)
        {
            byte[] o = Encoding.UTF8.GetBytes(s);
            try { o = ProtectedData.Protect(o, EntropieEssai, DataProtectionScope.CurrentUser); } catch { }
            return Convert.ToBase64String(o);
        }

        private static string Deproteger(string s)
        {
            try
            {
                byte[] o = Convert.FromBase64String(s);
                try { o = ProtectedData.Unprotect(o, EntropieEssai, DataProtectionScope.CurrentUser); } catch { }
                return Encoding.UTF8.GetString(o);
            }
            catch { return ""; }
        }

        /// <summary>Date de début de l'essai (la plus ancienne trouvée), ou MinValue s'il n'a jamais été lancé.</summary>
        private static DateTime DebutEssai()
        {
            var dates = new List<DateTime>();
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(CleRegistreEssai))
                {
                    string v = k == null ? null : k.GetValue("EssaiPremium") as string;
                    DateTime d;
                    if (v != null && DateTime.TryParse(Deproteger(v), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d)) dates.Add(d.ToUniversalTime());
                    else if (v != null) dates.Add(DateTime.UtcNow.AddDays(-365));   // valeur illisible : essai considéré comme utilisé
                }
            }
            catch { }
            try
            {
                if (File.Exists(FichierEssai))
                {
                    DateTime d;
                    if (DateTime.TryParse(Deproteger(File.ReadAllText(FichierEssai)), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d)) dates.Add(d.ToUniversalTime());
                    else dates.Add(DateTime.UtcNow.AddDays(-365));
                }
            }
            catch { }
            return dates.Count == 0 ? DateTime.MinValue : dates.Min();
        }

        /// <summary>L'essai gratuit n'a encore jamais été lancé sur ce PC (pour ce compte).</summary>
        public static bool EssaiDisponible { get { return DebutEssai() == DateTime.MinValue; } }

        /// <summary>Lance l'essai gratuit de 7 jours. Renvoie "" si c'est réussi.</summary>
        public static string DemarrerEssai()
        {
            if (!EssaiDisponible) return "l'essai gratuit a déjà été utilisé sur ce PC";
            string maintenant = DateTime.UtcNow.ToString("o");
            bool ok = false;
            try { using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(CleRegistreEssai)) { k.SetValue("EssaiPremium", Proteger(maintenant)); ok = true; } } catch { }
            try { Directory.CreateDirectory(Chemins.Local); File.WriteAllText(FichierEssai, Proteger(maintenant)); ok = true; } catch { }
            if (!ok) return "impossible d'enregistrer le début de l'essai";
            Evaluer(); Signaler();
            return "";
        }

        // ------------------------------------------------------------ Clés offertes (NPC-<données>.<signature>)
        private static string B64(byte[] o) { return Convert.ToBase64String(o).TrimEnd('=').Replace('+', '-').Replace('/', '_'); }
        private static byte[] DeB64(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            while (s.Length % 4 != 0) s += "=";
            return Convert.FromBase64String(s);
        }

        private static string Nettoyer(string cle) { return Regex.Replace(cle ?? "", @"\s+", ""); }

        public static string VerifierCleOfferte(string cle, out string titulaire)
        {
            titulaire = "";
            cle = Nettoyer(cle);
            var m = Regex.Match(cle, @"^NPC-([A-Za-z0-9_\-]+)\.([A-Za-z0-9_\-]+)$");
            if (!m.Success) return "format de clé inconnu";
            try
            {
                byte[] donnees = DeB64(m.Groups[1].Value), signature = DeB64(m.Groups[2].Value);
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false;
                    rsa.FromXmlString(Boutique.ClePublique);
                    if (!rsa.VerifyData(donnees, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) return "clé invalide (signature)";
                }
                var d = Json.Lire(Encoding.UTF8.GetString(donnees)) as Dictionary<string, object>;
                if (d == null || Json.Texte(d, "produit") != "NettoyagePC-Premium") return "clé invalide";
                string fin = Json.Texte(d, "fin");
                DateTime df;
                if (fin != "" && DateTime.TryParse(fin, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out df) && df < DateTime.Today) return "cette clé a expiré le " + df.ToString("dd/MM/yyyy");
                titulaire = Json.Texte(d, "nom");
                return "";
            }
            catch { return "clé invalide"; }
        }

        /// <summary>(Éditeur) Crée une clé offerte avec la clé privée : NettoyagePC.exe --generer-licence cle-privee.xml "Nom" [AAAA-MM-JJ]</summary>
        public static string GenererCleOfferte(string fichierClePrivee, string nom, string fin)
        {
            var d = new Dictionary<string, object> { { "produit", "NettoyagePC-Premium" }, { "nom", nom }, { "creee", DateTime.Today.ToString("yyyy-MM-dd") } };
            if (!string.IsNullOrEmpty(fin)) d["fin"] = fin;
            byte[] donnees = Encoding.UTF8.GetBytes(new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(d));
            using (var rsa = new RSACryptoServiceProvider())
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(File.ReadAllText(fichierClePrivee));
                if (rsa.PublicOnly) throw new InvalidOperationException("ce fichier ne contient pas de clé privée");
                return "NPC-" + B64(donnees) + "." + B64(rsa.SignData(donnees, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            }
        }

        // ------------------------------------------------------------ Vérifications en ligne (API des licences de Lemon Squeezy, ou service de l'éditeur au même format)
        private static Dictionary<string, object> Appeler(string action, Dictionary<string, string> champs, out int code)
        {
            code = 0;
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            string serveur = Boutique.ServeurLicences != "" ? Boutique.ServeurLicences + "/v1/licenses/" : "https://api.lemonsqueezy.com/v1/licenses/";
            var req = (HttpWebRequest)WebRequest.Create(serveur + action);
            req.Method = "POST"; req.Accept = "application/json"; req.ContentType = "application/x-www-form-urlencoded";
            req.UserAgent = "NettoyagePC/" + Appli.Version; req.Timeout = 20000; req.ReadWriteTimeout = 20000;
            byte[] corps = Encoding.UTF8.GetBytes(string.Join("&", champs.Select(c => c.Key + "=" + Uri.EscapeDataString(c.Value))));
            using (var s = req.GetRequestStream()) s.Write(corps, 0, corps.Length);
            HttpWebResponse rep;
            try { rep = (HttpWebResponse)req.GetResponse(); }
            catch (WebException ex) when (ex.Response is HttpWebResponse) { rep = (HttpWebResponse)ex.Response; }
            using (rep)
            using (var lecteur = new StreamReader(rep.GetResponseStream(), Encoding.UTF8))
            {
                code = (int)rep.StatusCode;
                return Json.Lire(lecteur.ReadToEnd()) as Dictionary<string, object> ?? new Dictionary<string, object>();
            }
        }

        /// <summary>La réponse concerne bien Nettoyage PC Premium (et pas une clé d'une autre boutique).</summary>
        private static string ControlerProduit(Dictionary<string, object> r)
        {
            var meta = Json.Dict(r, "meta");
            if (meta == null) return "réponse inattendue de la boutique";
            if (Json.Long(meta, "store_id") != Boutique.IdBoutique || Json.Long(meta, "product_id") != Boutique.IdProduit) return "cette clé n'est pas une clé de Nettoyage PC Premium";
            var lk = Json.Dict(r, "license_key");
            string statut = Json.Texte(lk, "status");
            if (statut == "disabled") return "cette clé a été désactivée";
            if (statut == "expired") return "cette clé a expiré";
            return "";
        }

        /// <summary>Identifiant pseudonyme de l'ordinateur envoyé à la boutique (« PC-1A2B3C4D ») : ni nom de personne, ni nom du PC.</summary>
        public static string NomInstance()
        {
            string id = "";
            try { id = Registre.Texte(@"HKLM\SOFTWARE\Microsoft\Cryptography", "MachineGuid"); } catch { }
            if (id == "") id = Environment.MachineName + "|" + Environment.UserName;
            using (var h = SHA256.Create())
            {
                byte[] o = h.ComputeHash(Encoding.UTF8.GetBytes("NettoyagePC|" + id));
                return "PC-" + BitConverter.ToString(o, 0, 4).Replace("-", "");
            }
        }

        /// <summary>Active une clé (offerte ou achetée). Renvoie "" si c'est réussi, sinon la raison.</summary>
        public static string Activer(string cle)
        {
            cle = Nettoyer(cle);
            if (cle == "") return "colle d'abord ta clé";
            if (cle.StartsWith("NPC-"))
            {
                string titulaire, r = VerifierCleOfferte(cle, out titulaire);
                if (r != "") return r;
                etat = new Dictionary<string, object> { { "type", "offerte" }, { "cle", cle }, { "titulaire", titulaire }, { "activee", DateTime.UtcNow.ToString("o") } };
                Enregistrer(); Evaluer(); Signaler();
                return "";
            }
            if (!Regex.IsMatch(cle, @"^[A-Za-z0-9\-]{16,64}$")) return "format de clé inconnu (vérifie que tu as copié toute la clé)";
            if (!Boutique.Ouverte) return "la boutique n'est pas encore configurée dans cette version";
            try
            {
                int code;
                var rep = Appeler("activate", new Dictionary<string, string> { { "license_key", cle }, { "instance_name", NomInstance() } }, out code);
                if (!Json.Bool(rep, "activated"))
                {
                    string err = Json.Texte(rep, "error");
                    if (Regex.IsMatch(err, "activation limit", RegexOptions.IgnoreCase)) return Langue.F("cette clé est déjà utilisée sur {0} PC. Retire-la d'un ancien PC (Premium > Gérer la licence), puis réessaie.", Boutique.NombrePc);
                    if (code == 404 || Regex.IsMatch(err, "not found", RegexOptions.IgnoreCase)) return "clé inconnue (vérifie qu'elle est complète)";
                    return err != "" ? err : "activation refusée (code " + code + ")";
                }
                string pb = ControlerProduit(rep);
                if (pb != "")
                {
                    // clé d'un autre produit : on libère tout de suite l'activation créée
                    var inst0 = Json.Dict(rep, "instance");
                    if (inst0 != null) try { int c2; Appeler("deactivate", new Dictionary<string, string> { { "license_key", cle }, { "instance_id", Json.Texte(inst0, "id") } }, out c2); } catch { }
                    return pb;
                }
                var meta = Json.Dict(rep, "meta"); var inst = Json.Dict(rep, "instance");
                etat = new Dictionary<string, object>
                {
                    { "type", "boutique" }, { "cle", cle }, { "instance", Json.Texte(inst, "id") }, { "titulaire", Json.Texte(meta, "customer_name") },
                    { "courriel", Json.Texte(meta, "customer_email") }, { "verifiee", DateTime.UtcNow.ToString("o") }, { "activee", DateTime.UtcNow.ToString("o") }
                };
                Enregistrer(); Evaluer(); Signaler();
                return "";
            }
            catch (Exception ex) { return "impossible de joindre la boutique (connexion Internet ?) : " + ex.Message; }
        }

        /// <summary>Revérifie de temps en temps une clé achetée (en arrière-plan). Sans Internet, le Premium reste actif 45 jours.</summary>
        public static void VerifierSiBesoin(bool forcer = false)
        {
            if (Json.Texte(etat, "type") != "boutique" || !Boutique.Ouverte) return;
            DateTime ok;
            DateTime.TryParse(Json.Texte(etat, "verifiee"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out ok);
            if (!forcer && (DateTime.UtcNow - ok).TotalDays < JoursEntreVerifications) return;
            try
            {
                int code;
                var rep = Appeler("validate", new Dictionary<string, string> { { "license_key", Json.Texte(etat, "cle") }, { "instance_id", Json.Texte(etat, "instance") } }, out code);
                bool valide = Json.Bool(rep, "valid") && ControlerProduit(rep) == "";
                if (valide) { etat["verifiee"] = DateTime.UtcNow.ToString("o"); etat.Remove("revoquee"); etat.Remove("raison"); }
                else if (code != 429 && code < 500 && rep.ContainsKey("valid"))
                {
                    // réponse claire de la boutique : clé désactivée, remboursée, ou PC retiré de la licence
                    etat["revoquee"] = true;
                    string pb = ControlerProduit(rep);
                    etat["raison"] = pb == "cette clé a été désactivée" ? "Licence Premium : cette clé a été désactivée (remboursement ou fraude)." : pb == "cette clé a expiré" ? "Licence Premium : cette clé a expiré." : pb != "" ? "Licence Premium : " + pb + "." : "Licence Premium retirée de ce PC (ou désactivée). Réactive-la avec ta clé.";
                }
                Enregistrer();
            }
            catch { /* pas de connexion : on réessaiera, le Premium reste actif pendant 45 jours */ }
            bool avant = Premium;
            Evaluer();
            if (avant != Premium) Signaler();
        }

        // ------------------------------------------------------------ Achat avec activation automatique (Stripe + service de l'éditeur)
        // L'application ouvre le lien de paiement avec une référence aléatoire (client_reference_id). Le service de licences
        // relie le paiement à cette référence : l'application récupère sa clé et l'active toute seule, sans copier-coller.
        private static string FichierAchat { get { return Path.Combine(Chemins.Local, "achat-en-cours.dat"); } }
        private const int JoursAchatEnAttente = 7;

        private static string ReferenceAchat(bool creer)
        {
            try
            {
                if (File.Exists(FichierAchat))
                {
                    var d = Json.Lire(Deproteger(File.ReadAllText(FichierAchat))) as Dictionary<string, object>;
                    DateTime t;
                    string r0 = Json.Texte(d, "ref");
                    if (d != null && Regex.IsMatch(r0, "^[A-Za-z0-9_-]{24,200}$")
                        && DateTime.TryParse(Json.Texte(d, "date"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out t)
                        && (DateTime.UtcNow - t.ToUniversalTime()).TotalDays < JoursAchatEnAttente) return r0;
                }
            }
            catch { }
            if (!creer) return "";
            byte[] o = new byte[24];
            using (var alea = RandomNumberGenerator.Create()) alea.GetBytes(o);
            string r = "NPC_" + B64(o);
            try
            {
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(FichierAchat, Proteger(Json.Ecrire(new Dictionary<string, object> { { "ref", r }, { "date", DateTime.UtcNow.ToString("o") } })));
            }
            catch { }
            return r;
        }

        /// <summary>Un achat lancé depuis ce PC attend encore sa clé (7 jours au plus).</summary>
        public static bool AchatEnAttente { get { return Boutique.AchatAutomatique && ReferenceAchat(false) != ""; } }

        /// <summary>Adresse de la page de paiement à ouvrir (avec la référence de ce PC si la clé arrive toute seule).</summary>
        public static string AdresseAchat()
        {
            string url = Boutique.UrlAchat;
            if (url == "" || !Boutique.AchatAutomatique) return url;
            return url + (url.Contains("?") ? "&" : "?") + "client_reference_id=" + ReferenceAchat(true);
        }

        /// <summary>Demande au service de licences si l'achat lancé depuis ce PC est payé ; si oui, active la clé.
        /// Renvoie "paid" (Premium activé), "pending" (pas encore payé), "disabled" (remboursé), "none" (aucun achat en attente) ou un message d'erreur.</summary>
        public static string RecupererAchat(out string cle)
        {
            cle = "";
            string reference = ReferenceAchat(false);
            if (reference == "" || Boutique.ServeurLicences == "") return "none";
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
            var req = (HttpWebRequest)WebRequest.Create(Boutique.ServeurLicences + "/v1/achats/" + reference);
            req.Method = "GET"; req.Accept = "application/json"; req.UserAgent = "NettoyagePC/" + Appli.Version; req.Timeout = 15000; req.ReadWriteTimeout = 15000;
            Dictionary<string, object> rep;
            try
            {
                using (var r = (HttpWebResponse)req.GetResponse())
                using (var lecteur = new StreamReader(r.GetResponseStream(), Encoding.UTF8))
                    rep = Json.Lire(lecteur.ReadToEnd()) as Dictionary<string, object> ?? new Dictionary<string, object>();
            }
            catch (Exception ex) { return "service de licences injoignable (" + ex.Message + ")"; }
            string statut = Json.Texte(rep, "status");
            if (statut != "paid") return statut == "" ? "réponse inattendue du service de licences" : statut;
            cle = Json.Texte(rep, "license_key");
            string erreur = Activer(cle);
            if (erreur != "") return erreur;
            try { File.Delete(FichierAchat); } catch { }
            return "paid";
        }

        /// <summary>Retire la licence de ce PC (libère une activation pour un autre PC).</summary>
        public static string Retirer()
        {
            string erreur = "";
            if (Json.Texte(etat, "type") == "boutique" && Boutique.Ouverte)
            {
                try
                {
                    int code;
                    var rep = Appeler("deactivate", new Dictionary<string, string> { { "license_key", Json.Texte(etat, "cle") }, { "instance_id", Json.Texte(etat, "instance") } }, out code);
                    if (!Json.Bool(rep, "deactivated") && code != 404) erreur = Json.Texte(rep, "error");
                }
                catch (Exception ex) { erreur = "boutique injoignable (" + ex.Message + ")"; }
            }
            if (erreur != "") return erreur;
            etat = new Dictionary<string, object>();
            try { if (File.Exists(Fichier)) File.Delete(Fichier); } catch { }
            Evaluer(); Signaler();
            return "";
        }
    }
}
