// Nettoyage PC - assistant local (Premium) : questions en langage naturel sur le PC, répondues par un modèle d'IA
// qui tourne SUR le PC avec Ollama (http://127.0.0.1:11434) : rien n'est envoyé sur Internet.
// L'assistant ne fait rien lui-même : il propose des boutons, et chaque action est confirmée par la personne.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class MessageAssistant { public string Role = "user", Texte = ""; }

    internal static class Assistant
    {
        public const string Adresse = "http://127.0.0.1:11434";
        private static string FichierModele { get { return Path.Combine(Chemins.Local, "assistant-modele.txt"); } }

        /// <summary>Actions que l'assistant (ou un assistant vocal comme ORION) peut proposer. Elles passent toutes par la confirmation habituelle.</summary>
        public static readonly Dictionary<string, string> ActionsPermises = new Dictionary<string, string>
        {
            { "standard", "Nettoyage rapide : fichiers temporaires, caches et corbeille (jamais les documents)" },
            { "complet", "Optimisation complète : nettoyage approfondi, disques, réglages (point de restauration avant)" },
            { "analyse", "Analyse complète du PC, sans rien modifier" },
            { "ramlib", "Libérer la mémoire vive" },
            { "secu", "Bilan de sécurité (antivirus, pare-feu, Windows Update...)" },
            { "antivirus", "Analyse antivirus rapide avec Microsoft Defender" },
            { "point", "Créer un point de restauration" },
            { "perf", "Passer en mode d'alimentation Performances" },
            { "equilibre", "Revenir au mode d'alimentation Équilibré" },
            { "majapps", "Mettre à jour les applications" },
            { "disques", "Optimiser les disques" },
            { "verifier", "Vérifier les disques (erreurs)" },
            { "modejeu", "Activer le mode jeu" },
            { "finmodejeu", "Terminer le mode jeu" },
            { "rapport", "Créer un rapport du PC" }
        };

        public static readonly Dictionary<string, string> PagesPermises = new Dictionary<string, string>
        {
            { "Accueil", "bilan du PC" }, { "Nettoyage", "nettoyage" }, { "Disques", "espace disque" }, { "Doublons", "fichiers en double" }, { "Optimisation", "optimisation, mode d'alimentation, mémoire" },
            { "Demarrage", "programmes au démarrage" }, { "Applications", "applications installées, désinstaller" }, { "Jeux", "jeux, mode jeu" }, { "Securite", "sécurité" }, { "Sauvegarde", "sauvegarde des documents" },
            { "Materiel", "matériel, températures, mémoire vive" }, { "Potentiel", "réglages qui brident le matériel (écran, EXPO, ReBAR)" }, { "Reseau", "Internet, DNS, Wi-Fi" }, { "Aide", "aide à distance par un proche" },
            { "Pilotes", "pilotes et mises à jour Windows" }, { "Historique", "historique et annulation" }, { "Reglages", "réglages de Nettoyage PC" }
        };

        public static readonly Dictionary<string, string> OutilsPermis = new Dictionary<string, string>
        {
            { "bouclier", "bouclier anti-arnaque (prise en main à distance, faux technicien)" }, { "enqueteur", "pourquoi mon PC ralentit (chronologie des changements)" },
            { "sante", "santé prédictive du matériel" }, { "energie", "coût en électricité" }, { "fps", "mesure des FPS dans un jeu" }, { "gardien", "gardien en temps réel" }
        };

        public static string ModeleChoisi
        {
            get { try { return File.Exists(FichierModele) ? File.ReadAllText(FichierModele).Trim() : ""; } catch { return ""; } }
            set { try { Directory.CreateDirectory(Chemins.Local); File.WriteAllText(FichierModele, value ?? ""); } catch { } }
        }

        private static HttpWebRequest Requete(string chemin, string methode, int delaiMs)
        {
            var r = (HttpWebRequest)WebRequest.Create(Adresse + chemin);
            r.Method = methode; r.Proxy = null; r.Timeout = delaiMs; r.ReadWriteTimeout = Math.Max(delaiMs, 300000);
            r.UserAgent = "NettoyagePC/" + Appli.Version;
            return r;
        }

        /// <summary>Modèles installés dans Ollama (null : Ollama ne répond pas).</summary>
        public static List<string> Modeles()
        {
            try
            {
                var r = Requete("/api/tags", "GET", 2500);
                using (var rep = r.GetResponse()) using (var s = new StreamReader(rep.GetResponseStream(), Encoding.UTF8))
                {
                    var d = Json.Lire(s.ReadToEnd()) as Dictionary<string, object>;
                    return Json.Objets(Json.Valeur(d, "models")).Select(m => Json.Texte(m, "name")).Where(n => n != "" && n.IndexOf("embed", StringComparison.OrdinalIgnoreCase) < 0).OrderBy(n => n).ToList();
                }
            }
            catch { return null; }
        }

        /// <summary>Modèle conseillé parmi ceux installés (le dernier choisi, sinon un modèle généraliste connu).</summary>
        public static string Conseille(List<string> modeles)
        {
            if (modeles == null || modeles.Count == 0) return "";
            string m = ModeleChoisi;
            if (modeles.Contains(m)) return m;
            foreach (var pref in new[] { "qwen3", "qwen2.5", "mistral", "llama3", "gemma3", "gemma", "phi" })
            {
                var t = modeles.FirstOrDefault(x => x.StartsWith(pref, StringComparison.OrdinalIgnoreCase));
                if (t != null) return t;
            }
            return modeles[0];
        }

        /// <summary>Instructions et état du PC donnés au modèle avant la conversation.</summary>
        public static string Contexte(IEnumerable<string> pointsBilan)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Tu es l'assistant intégré à Nettoyage PC, une application qui nettoie, accélère et protège ce PC Windows. Tu parles à une personne qui n'est pas forcément à l'aise avec l'informatique.");
            sb.AppendLine("Réponds en " + (Langue.Code == "en" ? "anglais" : Langue.Code == "es" ? "espagnol" : "français") + ", simplement, en 8 phrases au plus, en tutoyant.");
            sb.AppendLine("Règles :");
            sb.AppendLine("- Appuie-toi uniquement sur l'état du PC ci-dessous ; si une information manque, dis-le au lieu d'inventer.");
            sb.AppendLine("- Tu ne fais rien toi-même. Pour proposer une action de l'application, écris sur une ligne à part [ACTION:clé] ; pour ouvrir une page, [PAGE:clé] ; pour un outil, [OUTIL:clé]. Trois au plus, seulement des clés des listes ci-dessous. La personne clique puis confirme.");
            sb.AppendLine("- Ne dis jamais qu'une action a été faite. Ne conseille jamais de désactiver l'antivirus, le pare-feu, Windows Update ou le contrôle de compte, ni d'installer un « optimiseur » ou un logiciel piraté. Ne demande jamais de mot de passe ni de coordonnées bancaires.");
            sb.AppendLine("- Si quelqu'un a appelé la personne pour « réparer » son PC ou lui demande d'installer un logiciel de prise en main à distance, c'est une arnaque : dis-le clairement et propose [OUTIL:bouclier].");
            sb.AppendLine("Actions : " + string.Join(" ; ", ActionsPermises.Select(a => a.Key + " = " + a.Value)));
            sb.AppendLine("Pages : " + string.Join(" ; ", PagesPermises.Select(a => a.Key + " = " + a.Value)));
            sb.AppendLine("Outils : " + string.Join(" ; ", OutilsPermis.Select(a => a.Key + " = " + a.Value)));
            sb.AppendLine();
            sb.AppendLine("État du PC (" + DateTime.Now.ToString("dd/MM/yyyy HH:mm") + ") :");
            try { sb.AppendLine("- " + InfosPc.Resume()); } catch { }
            try { sb.AppendLine("- Processeur : " + InfosPc.Processeur()); } catch { }
            try { var g = InfosPc.CarteGraphiquePrincipale(); if (g != null) sb.AppendLine("- Carte graphique : " + g.Nom + " (pilote " + g.VersionPilote + (g.DatePilote.HasValue ? " du " + g.DatePilote.Value.ToString("dd/MM/yyyy") : "") + ")"); } catch { }
            foreach (var l in EtatPc.Lignes()) sb.AppendLine("- " + l);
            var pts = (pointsBilan ?? Enumerable.Empty<string>()).ToList();
            sb.AppendLine(pts.Count == 0 ? "- Bilan de l'application : rien à signaler." : "- Points relevés par le bilan de l'application : " + string.Join(" | ", pts));
            return sb.ToString();
        }

        private static readonly Regex Balise = new Regex(@"\[(ACTION|PAGE|OUTIL)\s*:\s*([A-Za-z0-9_-]+)\]", RegexOptions.IgnoreCase);

        /// <summary>Balises valides proposées dans une réponse (type en minuscules, clé), sans doublon.</summary>
        public static List<Tuple<string, string>> Propositions(string texte)
        {
            var l = new List<Tuple<string, string>>();
            foreach (Match m in Balise.Matches(texte ?? ""))
            {
                string type = m.Groups[1].Value.ToLowerInvariant(), cle = m.Groups[2].Value;
                if (type == "action") { if (!ActionsPermises.ContainsKey(cle.ToLowerInvariant())) continue; cle = cle.ToLowerInvariant(); }
                else if (type == "page") { var k = PagesPermises.Keys.FirstOrDefault(x => x.Equals(cle, StringComparison.OrdinalIgnoreCase)); if (k == null) continue; cle = k; }
                else { if (!OutilsPermis.ContainsKey(cle.ToLowerInvariant())) continue; cle = cle.ToLowerInvariant(); }
                if (!l.Any(x => x.Item1 == type && x.Item2 == cle) && l.Count < 4) l.Add(Tuple.Create(type, cle));
            }
            return l;
        }

        /// <summary>Texte affiché : sans les balises (ni une balise en cours d'écriture à la fin).</summary>
        public static string SansBalises(string texte)
        {
            string t = Balise.Replace(texte ?? "", "");
            t = Regex.Replace(t, @"\[[A-Za-z]{0,6}(:[A-Za-z0-9_-]*)?$", "");
            return Regex.Replace(t, @"\n{3,}", "\n\n").Trim();
        }

        /// <summary>Conversation en continu : chaque morceau de réponse est passé à « morceau ». Renvoie la réponse complète.</summary>
        public static string Discuter(string modele, string contexte, List<MessageAssistant> messages, Action<string> morceau, Func<bool> arreter)
        {
            var corps = new Dictionary<string, object>
            {
                { "model", modele }, { "stream", true }, { "options", new Dictionary<string, object> { { "temperature", 0.3 } } },
                { "messages", new[] { new Dictionary<string, object> { { "role", "system" }, { "content", contexte } } }.Concat(messages.Select(m => new Dictionary<string, object> { { "role", m.Role }, { "content", m.Texte } })).ToArray() }
            };
            var r = Requete("/api/chat", "POST", 120000);
            r.ContentType = "application/json";
            var octets = Encoding.UTF8.GetBytes(Json.Compact(corps));
            using (var s = r.GetRequestStream()) s.Write(octets, 0, octets.Length);
            var tout = new StringBuilder();
            using (var rep = r.GetResponse()) using (var lec = new StreamReader(rep.GetResponseStream(), Encoding.UTF8))
            {
                string ligne;
                while ((ligne = lec.ReadLine()) != null)
                {
                    if (arreter != null && arreter()) break;
                    var d = Json.Lire(ligne) as Dictionary<string, object>;
                    if (d == null) continue;
                    string err = Json.Texte(d, "error");
                    if (err != "") throw new InvalidOperationException(err);
                    string c = Json.Texte(Json.Dict(d, "message"), "content");
                    if (c != "") { tout.Append(c); if (morceau != null) morceau(c); }
                    if (Json.Bool(d, "done")) break;
                }
            }
            return tout.ToString();
        }
    }

    /// <summary>État du PC en quelques lignes (assistant, et « --etat » pour un assistant vocal comme ORION).</summary>
    internal static class EtatPc
    {
        public static List<string> Lignes()
        {
            var l = new List<string>();
            var d = Donnees();
            if (d.ContainsKey("disqueLibreGo")) l.Add(string.Format(Appli.Fr, "Disque de Windows : {0:0.#} Go libres sur {1:0} Go ({2:0} %)", d["disqueLibreGo"], d["disqueTotalGo"], d["disqueLibrePct"]));
            if (d.ContainsKey("ramPct")) l.Add(string.Format(Appli.Fr, "Mémoire vive utilisée : {0:0} %", d["ramPct"]));
            if (d.ContainsKey("allumeDepuisJours")) l.Add(string.Format(Appli.Fr, "PC allumé depuis {0:0.#} jour(s) sans redémarrage", d["allumeDepuisJours"]));
            if (d.ContainsKey("antivirusTempsReel")) l.Add((bool)d["antivirusTempsReel"] ? "Protection antivirus en temps réel : active" : "Protection antivirus en temps réel de Microsoft Defender : inactive (peut-être un autre antivirus)");
            var acces = d.ContainsKey("priseEnMainADistance") ? (string[])d["priseEnMainADistance"] : new string[0];
            l.Add(acces.Length == 0 ? "Aucun logiciel de prise en main à distance ouvert" : "Logiciel(s) de prise en main à distance ouvert(s) : " + string.Join(", ", acces));
            if (d.ContainsKey("dernierNettoyage")) l.Add("Dernière action de Nettoyage PC : " + d["dernierNettoyage"]);
            return l;
        }

        public static Dictionary<string, object> Donnees()
        {
            var d = new Dictionary<string, object> { { "application", "Nettoyage PC" }, { "version", Appli.Version }, { "date", DateTime.Now.ToString("yyyy-MM-dd HH:mm") } };
            try { d["premium"] = Licence.Premium; } catch { }
            try
            {
                var dq = new DriveInfo(Path.GetPathRoot(Chemins.Windows ?? "C:\\"));
                if (dq.IsReady && dq.TotalSize > 0) { d["disqueLibreGo"] = Math.Round(dq.AvailableFreeSpace / 1073741824.0, 1); d["disqueTotalGo"] = Math.Round(dq.TotalSize / 1073741824.0); d["disqueLibrePct"] = Math.Round(100.0 * dq.AvailableFreeSpace / dq.TotalSize); }
            }
            catch { }
            try { ulong t, f; Natif.Memoire(out t, out f); if (t > 0) d["ramPct"] = Math.Round(100.0 * (t - f) / t); } catch { }
            try { d["allumeDepuisJours"] = Math.Round(EnvironmentCompat.Millisecondes() / 86400000.0, 1); } catch { }
            try { if (Natif.EstWindows) d["antivirusTempsReel"] = InfosPc.DefenderTempsReel(); } catch { }
            try { d["priseEnMainADistance"] = Bouclier.ASignaler().Select(a => a.Nom).ToArray(); } catch { }
            try { var j = new DirectoryInfo(Chemins.Donnees).GetFiles("journal_*.txt").OrderByDescending(x => x.LastWriteTime).FirstOrDefault(); if (j != null) d["dernierNettoyage"] = j.LastWriteTime.ToString("dd/MM/yyyy HH:mm"); } catch { }
            return d;
        }
    }

    internal static class EnvironmentCompat
    {
        [System.Runtime.InteropServices.DllImport("kernel32.dll")] private static extern ulong GetTickCount64();
        /// <summary>Millisecondes depuis le démarrage de Windows (sans le débordement de TickCount après 24 jours).</summary>
        public static double Millisecondes() { try { return Natif.EstWindows ? GetTickCount64() : Environment.TickCount & int.MaxValue; } catch { return Environment.TickCount & int.MaxValue; } }
    }
}
