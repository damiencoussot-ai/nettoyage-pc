// Nettoyage PC - historique des modifications réversibles (Documents\NettoyagePC\modifications.json)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal class Modif
    {
        public string Id = Guid.NewGuid().ToString();
        public string Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        public string Type = "";
        public string Titre = "";
        public Dictionary<string, object> Donnees = new Dictionary<string, object>();
        public bool Annule;

        public string D(string cle) { return Json.Texte(Donnees, cle); }

        public Dictionary<string, object> VersJson()
        {
            return new Dictionary<string, object> { { "Id", Id }, { "Date", Date }, { "Type", Type }, { "Titre", Titre }, { "Donnees", Donnees }, { "Annule", Annule } };
        }

        public static Modif DepuisJson(Dictionary<string, object> d)
        {
            return new Modif
            {
                Id = Json.Texte(d, "Id"), Date = Json.Texte(d, "Date"), Type = Json.Texte(d, "Type"), Titre = Json.Texte(d, "Titre"),
                Donnees = Json.Dict(d, "Donnees") ?? new Dictionary<string, object>(), Annule = Json.Bool(d, "Annule")
            };
        }
    }

    internal static class Historique
    {
        private static readonly object verrou = new object();
        public static string Fichier { get { return Chemins.Fichier("modifications.json"); } }

        public static List<Modif> Lire()
        {
            lock (verrou) return Json.Objets(Json.LireFichier(Fichier)).Select(Modif.DepuisJson).ToList();
        }

        public static bool Ecrire(List<Modif> liste)
        {
            lock (verrou)
            {
                var garder = liste.Skip(Math.Max(0, liste.Count - 500)).Select(m => (object)m.VersJson()).ToArray();
                return FichierSur.Ecrire(Fichier, Json.Ecrire(garder));
            }
        }

        public static void Ajouter(string type, string titre, Dictionary<string, object> donnees)
        {
            try
            {
                lock (verrou)
                {
                    var l = Lire();
                    l.Add(new Modif { Type = type, Titre = titre, Donnees = donnees ?? new Dictionary<string, object>() });
                    Ecrire(l);
                }
            }
            catch { }
        }

        /// <summary>Marque comme annulées les entrées actives qui correspondent (l'utilisateur a défait lui-même).</summary>
        public static void MarquerAnnulees(Func<Modif, bool> filtre)
        {
            try
            {
                lock (verrou)
                {
                    var l = Lire(); bool change = false;
                    foreach (var m in l) if (!m.Annule && filtre(m)) { m.Annule = true; change = true; }
                    if (change) Ecrire(l);
                }
            }
            catch { }
        }

        // ------------------------------------------------------------ Annulation
        private const string Invalide = "entrée de l'historique invalide (refusée par sécurité)";
        private static readonly Regex Guid = new Regex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$");

        // Clés que Nettoyage PC modifie lui-même (seules celles-ci peuvent être rétablies)
        public static readonly string[] ClesReglages =
        {
            @"HKCU\Software\Microsoft\GameBar", @"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences",
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager",
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy", @"HKCU\Control Panel\International\User Profile", @"HKCU\Software\Policies\Microsoft\Windows\Explorer",
            @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences", @"HKCU\Software\Microsoft\DirectX\GraphicsSettings",
            @"HKCU\Software\Microsoft\Windows\CurrentVersion\Search", @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced",
            @"HKCU\Software\Microsoft\InputPersonalization", @"HKCU\Software\Microsoft\InputPersonalization\TrainedDataStore",
            @"HKCU\Software\Microsoft\Personalization\Settings", @"HKCU\Software\Microsoft\Siuf\Rules"
        };

        /// <summary>Annule une modification ; renvoie "" si c'est réussi, sinon la raison.</summary>
        public static string Annuler(Modif e)
        {
            // L'historique est un fichier de tes Documents, qu'un autre programme pourrait modifier :
            // chaque annulation est vérifiée avant d'être exécutée.
            try
            {
                switch (e.Type)
                {
                    case "deplacement":
                        {
                            string src = e.D("Source"), dst = e.D("Destination");
                            int i = dst.LastIndexOf(@"\_A_verifier\", StringComparison.OrdinalIgnoreCase);
                            if (i < 3) return Invalide;
                            string dl = dst.Substring(0, i);
                            if (!string.Equals(Path.GetDirectoryName(src), dl, StringComparison.OrdinalIgnoreCase) ||
                                !string.Equals(Path.GetDirectoryName(dst), dl + @"\_A_verifier", StringComparison.OrdinalIgnoreCase) ||
                                Protection.CheminProtege(src) || Protection.CheminProtege(dst)) return Invalide;
                            bool estDossier = Directory.Exists(dst);
                            if (!estDossier && !File.Exists(dst)) return "introuvable dans _A_verifier (déjà supprimé ou déplacé)";
                            if (File.Exists(src) || Directory.Exists(src)) return "un élément du même nom existe déjà à l'emplacement d'origine";
                            Directory.CreateDirectory(Path.GetDirectoryName(src));
                            if (estDossier) Directory.Move(dst, src); else File.Move(dst, src);
                            return "";
                        }
                    case "alimentation":
                        {
                            string avant = e.D("Avant");
                            if (!Guid.IsMatch(avant)) return Invalide;
                            int code;
                            Programmes.Executer(Path.Combine(Chemins.Systeme32, "powercfg.exe"), "/setactive " + avant, 20000, null, out code);
                            return code == 0 ? "" : "mode d'alimentation précédent introuvable";
                        }
                    case "registre":
                        {
                            string cle = Registre.Normaliser(e.D("Cle")), nom = e.D("Nom");
                            bool autorise = ClesReglages.Any(c => c.Equals(cle, StringComparison.OrdinalIgnoreCase));
                            object avant = Json.Valeur(e.Donnees, "Avant");
                            if (cle.Equals(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", StringComparison.OrdinalIgnoreCase) && nom == "HwSchMode")
                            {
                                long a = avant == null ? 0 : Json.Long(e.Donnees, "Avant");
                                if (avant == null || a == 1 || a == 2) autorise = true;
                            }
                            string typeAvant = e.D("TypeAvant");
                            if (!autorise || !(typeAvant == "" || typeAvant == "DWord" || typeAvant == "String" || typeAvant == "QWord")) return Invalide;
                            if (Json.Bool(e.Donnees, "Existait"))
                            {
                                RegistryValueKind k = typeAvant == "String" ? RegistryValueKind.String : typeAvant == "QWord" ? RegistryValueKind.QWord : RegistryValueKind.DWord;
                                object v = k == RegistryValueKind.String ? (object)Convert.ToString(avant) : k == RegistryValueKind.QWord ? (object)Convert.ToInt64(avant) : (object)Convert.ToInt32(avant);
                                if (Registre.EstMachine(cle) && k != RegistryValueKind.DWord) return Invalide;
                                Registre.Ecrire(cle, nom, v, k);
                            }
                            else Registre.Supprimer(cle, nom);
                            return "";
                        }
                    case "corbeille":
                        {
                            int ok, introuvables;
                            RestaurerCorbeille(Json.Textes(e.Donnees, "Fichiers"), out ok, out introuvables);
                            if (introuvables == 0) return "";
                            return ok + " fichier(s) restauré(s), " + introuvables + " introuvable(s) dans la corbeille (vidée entre-temps ?)";
                        }
                    case "demarrage":
                        {
                            string appro = e.D("Appro"), nom = e.D("NomAppro");
                            if (!GestionDemarrage.ApprosConnus.Any(s => s.Equals(Registre.Normaliser(appro), StringComparison.OrdinalIgnoreCase)) || string.IsNullOrWhiteSpace(nom)) return Invalide;
                            GestionDemarrage.Approuver(Registre.Normaliser(appro), nom, true);
                            return "";
                        }
                    case "appx":
                        {
                            string nom = e.D("Nom");
                            if (!Regex.IsMatch(nom, @"^[\w.\-]{3,120}$")) return Invalide;
                            Programmes.Ouvrir("ms-windows-store://search/?query=" + Uri.EscapeDataString(e.D("Libelle").Length > 0 ? e.D("Libelle") : nom));
                            return "";
                        }
                    case "dns":
                        return Reseau.AnnulerDns(e);
                }
                return "type de modification inconnu";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Remet des fichiers de la corbeille à leur place (fiches $I de la corbeille de l'utilisateur).</summary>
        public static void RestaurerCorbeille(IEnumerable<string> chemins, out int ok, out int introuvables)
        {
            ok = 0;
            var restants = new HashSet<string>(chemins.Where(c => !string.IsNullOrEmpty(c)), StringComparer.OrdinalIgnoreCase);
            string sid = Elevation.SidActuel();
            foreach (var racine in restants.Select(c => Path.GetPathRoot(c)).Distinct(StringComparer.OrdinalIgnoreCase).ToList())
            {
                string dossier = Path.Combine(racine, "$Recycle.Bin", sid);
                if (!Directory.Exists(dossier)) continue;
                foreach (var fiche in Directory.GetFiles(dossier, "$I*"))
                {
                    string orig;
                    try
                    {
                        byte[] o = File.ReadAllBytes(fiche);
                        if (BitConverter.ToInt64(o, 0) == 2) { int n = BitConverter.ToInt32(o, 24); orig = Encoding.Unicode.GetString(o, 28, Math.Max(0, n - 1) * 2); }
                        else orig = Encoding.Unicode.GetString(o, 24, Math.Min(520, o.Length - 24));
                        orig = orig.Split('\0')[0];
                    }
                    catch { continue; }
                    if (string.IsNullOrEmpty(orig) || !restants.Contains(orig)) continue;
                    if (Protection.CheminProtege(orig)) continue;   // jamais de restauration dans un dossier du système
                    string contenu = Path.Combine(dossier, "$R" + Path.GetFileName(fiche).Substring(2));
                    if (!File.Exists(contenu)) continue;
                    if (File.Exists(orig)) { restants.Remove(orig); continue; }
                    try
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(orig));
                        File.Move(contenu, orig);
                        try { File.Delete(fiche); } catch { }
                        restants.Remove(orig);
                        ok++;
                    }
                    catch { }
                }
            }
            introuvables = restants.Count;
        }
    }

    /// <summary>Programmes au démarrage : même mécanisme que le Gestionnaire des tâches (rien n'est supprimé).</summary>
    internal static class GestionDemarrage
    {
        internal class Source { public string Cle, Appro, Lieu; }

        public static readonly Source[] Sources =
        {
            new Source { Cle = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Run", Appro = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", Lieu = "Ton compte" },
            new Source { Cle = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run", Appro = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run", Lieu = "Tous les comptes" },
            new Source { Cle = @"HKLM\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run", Appro = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32", Lieu = "Tous les comptes (32 bits)" }
        };

        public const string ApproDossierUtilisateur = @"HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";
        public const string ApproDossierTous = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        /// <summary>Clés d'approbation que l'application modifie (programmes et dossiers Démarrage).</summary>
        public static IEnumerable<string> ApprosConnus { get { return Sources.Select(s => s.Appro).Concat(new[] { ApproDossierUtilisateur, ApproDossierTous }); } }

        public static bool EstApprouve(string appro, string nom)
        {
            var v = Registre.Lire(appro, nom) as byte[];
            if (v != null && v.Length > 0) return (v[0] & 1) == 0;
            return true;
        }

        public static void Approuver(string appro, string nom, bool actif)
        {
            var octets = new byte[12];
            if (actif) octets[0] = 2;
            else { octets[0] = 3; Array.Copy(BitConverter.GetBytes(DateTime.Now.ToFileTime()), 0, octets, 4, 8); }
            Registre.Ecrire(appro, nom, octets, RegistryValueKind.Binary);
        }
    }
}
