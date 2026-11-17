// Velox - « Velox Studio » dans Windows et dans les jeux.
// Windows permet de renommer une sortie audio (Paramètres > Son > la sortie > Renommer) : c'est son « nom d'appareil »
// (propriété PKEY_Device_DeviceDesc du point de terminaison). Velox nomme ainsi « Velox Studio » la sortie par laquelle
// il traite le son : le câble de « Pas boostés », sinon la sortie où l'égaliseur agit (Voicemeeter Input ou le casque).
// Les jeux, Discord et Windows l'affichent alors « Velox Studio (nom du pilote) ». Le nom d'origine est gardé (Historique
// et fichier noms-sorties.json) : « Annuler », couper l'option ou désinstaller Velox le remet.
// Seul ce nom est modifié (aucun autre réglage audio) ; il faut l'autorisation administrateur, comme dans Windows.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal static class NomStudio
    {
        public const string Nom = "Velox Studio";
        private const string CleSorties = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render";
        private const string ProprieteNom = "{a45c254e-df1c-4efd-8020-67d146a850e0},2";
        private static readonly Guid FmtNom = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0");

        // nom d'origine acceptable : lettres, chiffres, espaces et ponctuation courante, 1 à 120 caractères
        private static readonly Regex NomSur = new Regex(@"^[\p{L}\p{N} ()\[\]\-_.,'&+/#:]{1,120}$", RegexOptions.CultureInvariant);
        public static bool NomValide(string n) { return n != null && NomSur.IsMatch(n) && n.Trim() == n; }

        private static string Fichier { get { return Path.Combine(Chemins.Local, "noms-sorties.json"); } }

        /// <summary>Nom d'appareil actuel de cette sortie (« Casque », « CABLE Input »…), "" si illisible.</summary>
        public static string NomActuel(string id)
        {
            string g = AudioWindows.GuidDe(id);
            if (g == "" || !Natif.EstWindows) return "";
            try { return Registre.Texte(CleSorties + @"\{" + g + @"}\Properties", ProprieteNom); } catch { return ""; }
        }

        /// <summary>Noms d'origine gardés : identifiant de la sortie → nom d'avant Velox.</summary>
        public static Dictionary<string, string> Origines()
        {
            var d = new Dictionary<string, string>();
            try
            {
                if (!File.Exists(Fichier)) return d;
                var o = Json.Lire(File.ReadAllText(Fichier)) as Dictionary<string, object>;
                if (o == null) return d;
                foreach (var kv in o)
                {
                    string v = Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? "";
                    if (AudioWindows.IdValide(kv.Key) && NomValide(v)) d[kv.Key] = v;
                }
            }
            catch { }
            return d;
        }

        private static void EcrireOrigines(Dictionary<string, string> d)
        {
            Directory.CreateDirectory(Chemins.Local);
            File.WriteAllText(Fichier, Json.Ecrire(d.ToDictionary(kv => kv.Key, kv => (object)kv.Value)));
        }

        public static bool EstNomme(string id) { return NomActuel(id) == Nom; }

        /// <summary>
        /// Nomme « Velox Studio » cette sortie (autorisation administrateur). Le nom d'avant est gardé, et la sortie que
        /// Velox avait nommée avant (si c'en est une autre) reprend son nom. "" si c'est fait.
        /// </summary>
        public static string Appliquer(string id)
        {
            if (!Natif.EstWindows) return "Windows uniquement.";
            if (!AudioWindows.IdValide(id) || !AudioWindows.Lister().Any(s => s.Id == id)) return "Cette sortie n'est plus branchée ou n'est plus active.";
            string avant = NomActuel(id);
            if (avant == Nom) return "";
            if (!NomValide(avant)) return "Le nom actuel de cette sortie est illisible : rien n'a été modifié.";
            var o = Origines();
            // une seule sortie s'appelle « Velox Studio » à la fois
            foreach (var autre in o.Keys.Where(k => k != id).ToList())
                if (EstNomme(autre)) { string e0 = Renommer(autre, o[autre]); if (e0 == "") o.Remove(autre); }
            string err = Renommer(id, Nom);
            if (err != "") return err;
            o[id] = avant;
            try { EcrireOrigines(o); } catch { }
            Historique.Ajouter("nomsortie", "Nom « " + Nom + " » donné à la sortie « " + avant + " »",
                new Dictionary<string, object> { { "Id", id }, { "Avant", avant } });
            return "";
        }

        /// <summary>Remet le nom d'origine de toutes les sorties que Velox a nommées. "" si c'est fait (ou rien à faire).</summary>
        public static string Retablir()
        {
            if (!Natif.EstWindows) return "";
            var o = Origines();
            string err = "";
            foreach (var kv in o.ToList())
            {
                if (!EstNomme(kv.Key)) { o.Remove(kv.Key); continue; }   // renommée depuis par l'utilisateur : on n'y touche pas
                string e = Renommer(kv.Key, kv.Value);
                if (e == "") o.Remove(kv.Key); else err = e;
            }
            try { EcrireOrigines(o); } catch { }
            return err;
        }

        /// <summary>Annule depuis l'Historique (identifiant et nom vérifiés avant usage).</summary>
        public static string Annuler(Modif e)
        {
            string id = e.D("Id"), avant = e.D("Avant");
            if (!AudioWindows.IdValide(id) || !NomValide(avant)) return "entrée de l'historique invalide (refusée par sécurité)";
            if (!Natif.EstWindows) return "Windows uniquement.";
            if (!EstNomme(id)) return "";   // déjà renommée autrement : rien à remettre
            string err = Renommer(id, avant);
            if (err == "") { var o = Origines(); o.Remove(id); try { EcrireOrigines(o); } catch { } }
            return err;
        }

        private static string Renommer(string id, string nom)
        {
            int code = Elevation.CommandeAttendre(60, "renommer-sortie", id, Convert.ToBase64String(Encoding.UTF8.GetBytes(nom)));
            if (code == -1) return "Windows n'a pas eu l'autorisation administrateur : le nom n'a pas changé.";
            if (code != 0) return "Windows a refusé de renommer la sortie (code " + code + ").";
            return "";
        }

        /// <summary>(administrateur) « renommer-sortie ID NOM_BASE64 » : écrit le nom d'appareil de la sortie. 0 si c'est fait.</summary>
        public static int RenommerEnAdmin(string[] args)
        {
            if (args.Length < 3 || !AudioWindows.IdValide(args[1])) return 3;
            string nom;
            try { nom = Encoding.UTF8.GetString(Convert.FromBase64String(args[2])); } catch { return 3; }
            if (!NomValide(nom)) return 3;
            return RenommerDirect(args[1], nom) ? 0 : 5;
        }

        /// <summary>(administrateur) écrit PKEY_Device_DeviceDesc dans le magasin de propriétés de la sortie.</summary>
        public static bool RenommerDirect(string id, string nom)
        {
            if (!Natif.EstWindows || !AudioWindows.IdValide(id) || !NomValide(nom)) return false;
            IMMDeviceEnumerator e = null; IMMDevice d = null; IntPtr pStore = IntPtr.Zero; IPropertyStore store = null;
            var pv = new ValeurPropriete();
            try
            {
                e = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                if (e.GetDevice(id, out d) != 0 || d == null) return false;
                if (d.OpenPropertyStore(2 /* STGM_READWRITE */, out pStore) != 0 || pStore == IntPtr.Zero) return false;
                store = (IPropertyStore)Marshal.GetObjectForIUnknown(pStore);
                var cle = new ClePropriete { Format = FmtNom, Id = 2 };
                pv.Type = 31;   // VT_LPWSTR
                pv.Valeur = Marshal.StringToCoTaskMemUni(nom);
                if (store.SetValue(ref cle, ref pv) != 0) return false;
                return store.Commit() == 0;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("nom de la sortie audio", ex); return false; }
            finally
            {
                if (pv.Valeur != IntPtr.Zero) Marshal.FreeCoTaskMem(pv.Valeur);
                AudioWindows.Liberer(store);
                if (pStore != IntPtr.Zero) Marshal.Release(pStore);
                AudioWindows.Liberer(d); AudioWindows.Liberer(e);
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ClePropriete { public Guid Format; public int Id; }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    internal struct ValeurPropriete { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Valeur; }

    [ComImport, Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int nombre);
        [PreserveSig] int GetAt(int index, out ClePropriete cle);
        [PreserveSig] int GetValue(ref ClePropriete cle, out ValeurPropriete valeur);
        [PreserveSig] int SetValue(ref ClePropriete cle, ref ValeurPropriete valeur);
        [PreserveSig] int Commit();
    }
}
