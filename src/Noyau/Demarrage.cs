// Nettoyage PC - démarrage sans interface : moteur, commandes administrateur, état du PC en JSON, réponse de l'assistant,
// et ce que fait le programme avant d'ouvrir une fenêtre. Partagé par la version 4 et la version 5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal static class Demarrage
    {
        private static readonly HashSet<string> optionsUtilisateur = new HashSet<string> { "--documents", "--telechargements", "--profil", "--localappdata", "--appdata", "--temp", "--sid" };

        /// <summary>Modes sans fenêtre. Renvoie le code de sortie, ou null si le programme doit ouvrir son interface.</summary>
        public static int? SansInterface(string[] args)
        {
            if (args.Length == 0) return null;
            if (args[0] == "--moteur")
            {
                ProtegerTemporaire();
                return Moteur.Executer(args.Skip(1).ToArray());
            }
            if (args[0] == "--commande")
            {
                ProtegerTemporaire();
                var cmd = args.Skip(1).TakeWhile(a => !optionsUtilisateur.Contains(a)).ToArray();
                return CommandesAdmin.Executer(cmd);
            }
            if (args[0] == "--gardien-arreter") { Gardien.Arreter(); return 0; }
            if (args[0] == "--mesurer")
            {
                var r = Benchmark.Lancer(Path.GetTempPath(), (t, p) => Console.WriteLine(p + " % " + t));
                if (r != null) Console.WriteLine(Json.Ecrire(r.VersJson()));
                return r == null ? 1 : 0;
            }
            // (assistant vocal, ORION...) état du PC en JSON, et réponse de l'assistant en texte : rien n'est exécuté
            if (args[0] == "--etat") { try { Licence.Charger(); } catch { } Console.Out.WriteLine(Json.Ecrire(EtatPc.Donnees())); Console.Out.Flush(); return 0; }
            if (args.Length >= 2 && args[0] == "--assistant-texte") return AssistantTexte(args[1]);
            if (args.Length >= 2 && args[0] == "--verifier-cle") { string t; string r = Licence.VerifierCleOfferte(File.Exists(args[1]) ? File.ReadAllText(args[1]) : args[1], out t); Console.WriteLine(r == "" ? "valide : " + t : "invalide : " + r); return r == "" ? 0 : 1; }
            return null;
        }

        private static int AssistantTexte(string question)
        {
            try
            {
                Langue.Choisir(Parametres.Lire().Langue);
                Licence.Charger();
                if (!Licence.Premium) { Console.Out.WriteLine(Langue.T("L'assistant fait partie de Nettoyage PC Premium.")); return 3; }
                var m = Assistant.Modeles();
                string modele = Assistant.Conseille(m);
                if (modele == "") { Console.Out.WriteLine(Langue.T("Ollama n'est pas démarré ou aucun modèle n'est installé.")); return 2; }
                string rep = Assistant.Discuter(modele, Assistant.Contexte(null), new List<MessageAssistant> { new MessageAssistant { Role = "user", Texte = question.Length > 600 ? question.Substring(0, 600) : question } }, null, null);
                var props = Assistant.Propositions(rep);
                Console.Out.WriteLine(Assistant.SansBalises(rep));
                foreach (var p in props) Console.Out.WriteLine("[" + p.Item1 + ":" + p.Item2 + "]");
                Console.Out.Flush();
                return 0;
            }
            catch (Exception ex) { Console.Out.WriteLine(Langue.T("Erreur de l'assistant : ") + ex.Message); return 1; }
        }

        /// <summary>Dossiers de la personne qui utilise l'application, transmis au moteur lancé avec l'autorisation administrateur.</summary>
        public static void AppliquerDossiersUtilisateur(string[] args)
        {
            var valeurs = new Dictionary<string, string>();
            for (int i = 0; i < args.Length - 1; i++) if (optionsUtilisateur.Contains(args[i])) valeurs[args[i]] = args[i + 1];
            Func<string, string> v = k => { string s; return valeurs.TryGetValue(k, out s) && !string.IsNullOrEmpty(s) && Directory.Exists(s) ? s : null; };
            if (v("--documents") != null) Environment.SetEnvironmentVariable("NP_DOCUMENTS", v("--documents"));
            if (v("--telechargements") != null) Environment.SetEnvironmentVariable("NP_TELECHARGEMENTS", v("--telechargements"));
            if (v("--temp") != null) Environment.SetEnvironmentVariable("NP_TEMP_UTILISATEUR", v("--temp"));
            string sid; valeurs.TryGetValue("--sid", out sid);
            if (!string.IsNullOrEmpty(sid) && sid != Elevation.SidActuel())
            {
                // Autorisation donnée avec le compte d'un autre administrateur (compte standard) : on nettoie le bon profil
                if (v("--profil") != null) Environment.SetEnvironmentVariable("NP_PROFIL", v("--profil"));
                if (v("--localappdata") != null) Environment.SetEnvironmentVariable("NP_LOCALAPPDATA", v("--localappdata"));
                if (v("--appdata") != null) Environment.SetEnvironmentVariable("NP_APPDATA", v("--appdata"));
            }
        }

        /// <summary>Avec l'autorisation administrateur : dossier temporaire de Windows (jamais celui de l'utilisateur).</summary>
        private static void ProtegerTemporaire()
        {
            if (!Appli.EstAdmin || !Natif.EstWindows) return;
            string t = Path.Combine(Chemins.Windows, "Temp");
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NP_TEMP_UTILISATEUR"))) Environment.SetEnvironmentVariable("NP_TEMP_UTILISATEUR", Path.GetTempPath().TrimEnd('\\'));
            if (Directory.Exists(t)) { Environment.SetEnvironmentVariable("TEMP", t); Environment.SetEnvironmentVariable("TMP", t); }
        }

        public static bool RelancerSansAdmin()
        {
            try
            {
                string marque = Path.Combine(Path.GetTempPath(), "NettoyagePC-relance.txt");
                if (File.Exists(marque) && (DateTime.Now - File.GetLastWriteTime(marque)).TotalSeconds < 20) return false;
                if (Process.GetProcessesByName("explorer").Length == 0) return false;
                File.WriteAllText(marque, DateTime.Now.ToString("o"));
                JournalDemarrage.Noter("Lancé en administrateur : relance avec les droits normaux (Explorateur).");
                Process.Start(new ProcessStartInfo(Path.Combine(Chemins.Windows, "explorer.exe"), "\"" + Chemins.Exe + "\"") { UseShellExecute = false });
                return true;
            }
            catch { return false; }
        }

        public static void RamenerAutreFenetre()
        {
            try
            {
                var moi = Process.GetCurrentProcess();
                foreach (var p in Process.GetProcessesByName(moi.ProcessName))
                {
                    if (p.Id == moi.Id || p.MainWindowHandle == IntPtr.Zero) continue;
                    if (Natif.IsIconic(p.MainWindowHandle)) Natif.ShowWindow(p.MainWindowHandle, 9);
                    Natif.SetForegroundWindow(p.MainWindowHandle);
                    JournalDemarrage.Noter("Nettoyage PC est déjà ouvert : la fenêtre existante est ramenée au premier plan.");
                    return;
                }
            }
            catch { }
        }
    }
}
