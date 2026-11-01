// Nettoyage PC - commandes reçues de l'extérieur : raccourcis, gardien, assistant vocal (ORION...).
//   NettoyagePC.exe --page Nettoyage           ouvre une page
//   NettoyagePC.exe --ouvrir bouclier          ouvre un outil (gardien, bouclier, assistant, enqueteur, sante, energie, fps)
//   NettoyagePC.exe --action nettoyage         prépare une action : elle est TOUJOURS confirmée par la personne devant le PC
//   NettoyagePC.exe --assistant "question"     pose une question à l'assistant local
// Si l'application est déjà ouverte, la commande lui est transmise (fichier + signal), sans deuxième fenêtre.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace NettoyagePC
{
    internal class CommandeExterne
    {
        public string Page, Ouvrir, Action, Question;
        public bool Vide { get { return Page == null && Ouvrir == null && Action == null && Question == null; } }

        public static readonly string[] Outils = { "gardien", "bouclier", "assistant", "enqueteur", "sante", "energie", "fps", "modejeu", "majapps", "photos", "bulletin", "reseau" };
        private const string NomSignal = @"Local\NettoyagePC-Commande";
        private static string Fichier { get { return Path.Combine(Chemins.Local, "commande.json"); } }

        public static CommandeExterne Lire(string[] args)
        {
            var c = new CommandeExterne();
            Func<string, string> valeur = opt => { int i = Array.IndexOf(args, opt); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; };
            string v = valeur("--page");
            if (v != null && Regex.IsMatch(v, "^[A-Za-z]{2,20}$")) c.Page = v;
            v = valeur("--ouvrir");
            if (v != null && Outils.Contains(v.ToLowerInvariant())) c.Ouvrir = v.ToLowerInvariant();
            v = valeur("--action");
            if (v != null && Regex.IsMatch(v, "^[A-Za-z0-9_-]{2,40}$")) c.Action = v;
            v = valeur("--assistant");
            if (v != null && v.Trim() != "") c.Question = v.Trim().Length > 600 ? v.Trim().Substring(0, 600) : v.Trim();
            return c;
        }

        private Dictionary<string, object> VersJson()
        {
            var d = new Dictionary<string, object> { { "date", DateTime.Now.ToString("o") } };
            if (Page != null) d["page"] = Page;
            if (Ouvrir != null) d["ouvrir"] = Ouvrir;
            if (Action != null) d["action"] = Action;
            if (Question != null) d["question"] = Question;
            return d;
        }

        /// <summary>Deuxième lancement : transmet la commande à la fenêtre déjà ouverte.</summary>
        public bool Transmettre()
        {
            if (Vide) return false;
            try
            {
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(Fichier, Json.Ecrire(VersJson()));
                using (var e = EventWaitHandle.OpenExisting(NomSignal)) e.Set();
                return true;
            }
            catch { return false; }
        }

        private static EventWaitHandle signal;
        private static RegisteredWaitHandle attente;

        /// <summary>Fenêtre principale : écoute les commandes transmises par un deuxième lancement.</summary>
        public static void Ecouter(Action<CommandeExterne> recue)
        {
            try
            {
                signal = new EventWaitHandle(false, EventResetMode.AutoReset, NomSignal);
                attente = ThreadPool.RegisterWaitForSingleObject(signal, (o, t) =>
                {
                    var c = LireFichier();
                    if (c != null && !c.Vide) recue(c);
                }, null, -1, false);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("écoute des commandes", ex); }
        }

        private static CommandeExterne LireFichier()
        {
            try
            {
                if (!File.Exists(Fichier)) return null;
                var d = Json.LireFichier(Fichier) as Dictionary<string, object>;
                File.Delete(Fichier);
                if (d == null) return null;
                DateTime date;
                if (DateTime.TryParse(Json.Texte(d, "date"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out date) && (DateTime.Now - date).TotalMinutes > 2) return null;
                var args = new List<string>();
                foreach (var k in new[] { "page", "ouvrir", "action", "assistant" })
                {
                    string v = Json.Texte(d, k == "assistant" ? "question" : k);
                    if (!string.IsNullOrEmpty(v)) { args.Add("--" + k); args.Add(v); }
                }
                return Lire(args.ToArray());
            }
            catch { return null; }
        }
    }
}
