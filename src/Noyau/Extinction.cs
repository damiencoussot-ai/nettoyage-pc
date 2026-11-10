// Velox - minuteur d'extinction : éteindre ou redémarrer le PC dans X minutes, avec l'outil de Windows (shutdown.exe),
// sans droits administrateur. Annulable à tout moment (shutdown /a). L'heure prévue est gardée pour l'afficher.
using System;
using System.Globalization;
using System.IO;

namespace NettoyagePC
{
    internal static class Extinction
    {
        private static string Fichier { get { return Path.Combine(Chemins.Local, "extinction.txt"); } }

        /// <summary>Extinction prévue (heure, redémarrage ?) si elle est encore à venir, sinon null.</summary>
        public static Tuple<DateTime, bool> Prevue()
        {
            try
            {
                if (!File.Exists(Fichier)) return null;
                var l = File.ReadAllText(Fichier).Trim().Split('|');
                DateTime d;
                if (l.Length == 2 && DateTime.TryParseExact(l[0], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && d > DateTime.Now)
                    return Tuple.Create(d, l[1] == "r");
                File.Delete(Fichier);
            }
            catch { }
            return null;
        }

        /// <summary>Programme l'extinction (ou le redémarrage) dans « minutes ». Renvoie "" si Windows a accepté, sinon l'erreur.</summary>
        public static string Programmer(int minutes, bool redemarrer)
        {
            if (minutes < 1 || minutes > 600) return "durée invalide";
            if (!Natif.EstWindows) return "disponible seulement sous Windows";
            try
            {
                string exe = Path.Combine(Chemins.Systeme32, "shutdown.exe");
                int code;
                Programmes.Executer(exe, "/a", 8000, null, out code);   // une extinction déjà prévue est remplacée
                string sortie = Programmes.Executer(exe, (redemarrer ? "/r" : "/s") + " /t " + (minutes * 60).ToString(CultureInfo.InvariantCulture), 8000, null, out code);
                if (code != 0) return string.IsNullOrWhiteSpace(sortie) ? "Windows a refusé (code " + code + ")" : sortie.Trim();
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(Fichier, DateTime.Now.AddMinutes(minutes).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "|" + (redemarrer ? "r" : "s"));
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        /// <summary>Annule l'extinction prévue. Renvoie vrai si Windows en avait une.</summary>
        public static bool Annuler()
        {
            bool ok = false;
            try
            {
                int code;
                if (Natif.EstWindows) { Programmes.Executer(Path.Combine(Chemins.Systeme32, "shutdown.exe"), "/a", 8000, null, out code); ok = code == 0; }
            }
            catch { }
            try { File.Delete(Fichier); } catch { }
            return ok;
        }
    }
}
