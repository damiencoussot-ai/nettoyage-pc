// Nettoyage PC - gardien en temps réel (Premium) : réglages partagés entre l'interface et le gardien (icône près de l'horloge),
// lancement automatique à l'ouverture de session (clé Run de l'utilisateur, sans droits administrateur), arrêt et rechargement.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal class ReglagesGardien
    {
        public bool Actif;
        public bool Demarrage = true, Disque = true, NettoyageAuto = true, Antivirus = true, Memoire = true, Temperature = true, Arnaque = true, Sante = true, Energie = true;
        public int SeuilDisque = 10;                      // % d'espace libre sur le disque de Windows
        public bool SauvegardeAuto;
        public string SauvegardeId = "", SauvegardeLibelle = "";
        public List<string> SauvegardeDossiers = new List<string> { "Documents", "Images", "Bureau" };
        public int SauvegardeJours = 1;                   // au plus une fois tous les N jours

        private static string Fichier { get { return Path.Combine(Chemins.Local, "gardien.json"); } }

        public static ReglagesGardien Lire()
        {
            var r = new ReglagesGardien();
            try
            {
                var d = Json.LireFichier(Fichier) as Dictionary<string, object>;
                if (d == null) return r;
                r.Actif = Json.Bool(d, "Actif");
                r.Demarrage = Json.Bool(d, "Demarrage", true); r.Disque = Json.Bool(d, "Disque", true); r.NettoyageAuto = Json.Bool(d, "NettoyageAuto", true);
                r.Antivirus = Json.Bool(d, "Antivirus", true); r.Memoire = Json.Bool(d, "Memoire", true); r.Temperature = Json.Bool(d, "Temperature", true);
                r.Arnaque = Json.Bool(d, "Arnaque", true); r.Sante = Json.Bool(d, "Sante", true); r.Energie = Json.Bool(d, "Energie", true);
                long s = Json.Long(d, "SeuilDisque"); if (s >= 3 && s <= 30) r.SeuilDisque = (int)s;
                r.SauvegardeAuto = Json.Bool(d, "SauvegardeAuto");
                r.SauvegardeId = Json.Texte(d, "SauvegardeId"); r.SauvegardeLibelle = Json.Texte(d, "SauvegardeLibelle");
                var dos = Json.Valeur(d, "SauvegardeDossiers") as System.Collections.IEnumerable;
                if (dos != null && !(dos is string)) r.SauvegardeDossiers = dos.Cast<object>().Select(x => Convert.ToString(x)).Where(x => Moteur.DossiersPersonnels().ContainsKey(x)).Distinct().ToList();
                long j = Json.Long(d, "SauvegardeJours"); if (j >= 1 && j <= 30) r.SauvegardeJours = (int)j;
            }
            catch { }
            return r;
        }

        public void Ecrire()
        {
            try
            {
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(Fichier, Json.Ecrire(new Dictionary<string, object>
                {
                    { "Actif", Actif }, { "Demarrage", Demarrage }, { "Disque", Disque }, { "NettoyageAuto", NettoyageAuto }, { "Antivirus", Antivirus },
                    { "Memoire", Memoire }, { "Temperature", Temperature }, { "Arnaque", Arnaque }, { "Sante", Sante }, { "Energie", Energie }, { "SeuilDisque", SeuilDisque }, { "SauvegardeAuto", SauvegardeAuto },
                    { "SauvegardeId", SauvegardeId }, { "SauvegardeLibelle", SauvegardeLibelle }, { "SauvegardeDossiers", SauvegardeDossiers.ToArray() }, { "SauvegardeJours", SauvegardeJours }
                }));
            }
            catch (Exception ex) { JournalDemarrage.Erreur("réglages du gardien", ex); }
        }
    }

    internal static class Gardien
    {
        public const string NomMutex = @"Local\NettoyagePC-Gardien", NomArret = @"Local\NettoyagePC-Gardien-Arret", NomRecharger = @"Local\NettoyagePC-Gardien-Recharger";
        private const string CleRun = @"Software\Microsoft\Windows\CurrentVersion\Run", ValeurRun = "NettoyagePC-Gardien";
        public const string FichierMarqueur = ".nettoyagepc-sauvegarde";

        public static bool EnCours
        {
            get
            {
                if (!Natif.EstWindows) return false;
                try { using (Mutex.OpenExisting(NomMutex)) return true; }
                catch (WaitHandleCannotBeOpenedException) { return false; }
                catch (UnauthorizedAccessException) { return true; }
                catch { return false; }
            }
        }

        public static bool LancementAutomatique
        {
            get { try { using (var k = Registry.CurrentUser.OpenSubKey(CleRun)) return k != null && k.GetValue(ValeurRun) != null; } catch { return false; } }
        }

        private static void Signaler(string nom)
        {
            try { EventWaitHandle e; if (EventWaitHandle.TryOpenExisting(nom, out e)) using (e) e.Set(); } catch { }
        }

        /// <summary>Active le gardien : lancement à chaque ouverture de session, et tout de suite.</summary>
        public static string Activer()
        {
            if (!Licence.Premium) return "le gardien fait partie de Premium";
            try { using (var k = Registry.CurrentUser.CreateSubKey(CleRun)) k.SetValue(ValeurRun, "\"" + Chemins.Exe + "\" --gardien"); }
            catch (Exception ex) { return "impossible de programmer le lancement automatique : " + ex.Message; }
            var r = ReglagesGardien.Lire(); r.Actif = true; r.Ecrire();
            Demarrer();
            return "";
        }

        public static void Desactiver()
        {
            try { using (var k = Registry.CurrentUser.OpenSubKey(CleRun, true)) if (k != null && k.GetValue(ValeurRun) != null) k.DeleteValue(ValeurRun, false); } catch { }
            var r = ReglagesGardien.Lire(); r.Actif = false; r.Ecrire();
            Arreter();
        }

        public static void Demarrer()
        {
            if (EnCours || !Natif.EstWindows) return;
            try { Process.Start(new ProcessStartInfo(Chemins.Exe, "--gardien") { UseShellExecute = false, WorkingDirectory = Chemins.Ici }); }
            catch (Exception ex) { JournalDemarrage.Erreur("lancement du gardien", ex); }
        }

        /// <summary>Au démarrage de l'interface : relance le gardien s'il est activé (après une mise à jour par exemple).</summary>
        public static void DemarrerSiBesoin()
        {
            try
            {
                if (!Licence.Premium) return;
                var r = ReglagesGardien.Lire();
                if (!r.Actif) return;
                // le chemin du programme a pu changer (version portable déplacée) : la clé Run est remise à jour
                using (var k = Registry.CurrentUser.CreateSubKey(CleRun)) { string v = "\"" + Chemins.Exe + "\" --gardien"; if ((k.GetValue(ValeurRun) as string) != v) k.SetValue(ValeurRun, v); }
                Demarrer();
            }
            catch { }
        }

        public static void Arreter() { Signaler(NomArret); }
        public static void Recharger() { Signaler(NomRecharger); }

        /// <summary>Destination de la sauvegarde automatique : le lecteur qui porte le marqueur de ce PC (clé USB rebranchée sous une autre lettre comprise).</summary>
        public static string TrouverDestination(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            string systeme = Path.GetPathRoot(Chemins.Windows ?? "C:\\");
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || string.Equals(d.Name, systeme, StringComparison.OrdinalIgnoreCase)) continue;
                    if (d.DriveType != DriveType.Removable && d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Network) continue;
                    string m = Path.Combine(d.RootDirectory.FullName, "NettoyagePC-Sauvegarde", FichierMarqueur);
                    if (File.Exists(m) && File.ReadAllText(m).Trim() == id) return d.RootDirectory.FullName;
                }
                catch { }
            }
            return null;
        }

        /// <summary>Marque un lecteur comme destination de la sauvegarde automatique ; renvoie l'identifiant.</summary>
        public static string MarquerDestination(string racine, string idExistant)
        {
            string id = string.IsNullOrEmpty(idExistant) ? Guid.NewGuid().ToString("N") : idExistant;
            string dossier = Path.Combine(racine, "NettoyagePC-Sauvegarde");
            Directory.CreateDirectory(dossier);
            string m = Path.Combine(dossier, FichierMarqueur);
            File.WriteAllText(m, id);
            try { File.SetAttributes(m, FileAttributes.Hidden); } catch { }
            return id;
        }

        /// <summary>Dernière sauvegarde automatique (écrite par le moteur).</summary>
        public static Dictionary<string, object> DerniereSauvegarde()
        {
            try { return Json.LireFichier(Path.Combine(Chemins.Local, "sauvegarde-auto.json")) as Dictionary<string, object>; } catch { return null; }
        }
    }
}
