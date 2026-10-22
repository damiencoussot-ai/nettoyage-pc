// Nettoyage PC - exécution des actions : lancement du moteur (avec l'autorisation si besoin), suivi, arrêt
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal partial class FenetrePrincipale
    {
        private Process proc;
        private CanalSortie canal;
        private bool procEleve;
        private DateTime? finProc;
        private string actionEnCours = "", cleEnCours = "";
        private readonly List<string> lignesAction = new List<string>();
        private Action apresAction;

        public bool ActionEnCours { get { try { return proc != null && !proc.HasExited; } catch { return false; } } }
        public string CleEnCours { get { return cleEnCours; } }

        /// <summary>Lance une action du moteur. Les actions qui touchent tout le PC demandent l'autorisation de Windows à ce moment-là.</summary>
        public bool LancerAction(string cle, IEnumerable<string> argsSupplementaires = null, string confirmation = null, Action apres = null)
        {
            ActionMoteur a;
            if (!Actions.TryGetValue(cle, out a)) return false;
            if (a.Requiert != null && !Disponible[a.Requiert]) { BoiteMessage.Info(Raisons[a.Requiert]); return false; }
            if (ActionEnCours) { BoiteMessage.Info("Une action est déjà en cours : attends qu'elle se termine."); return false; }
            var args = new List<string>(a.Args);
            if (argsSupplementaires != null) args.AddRange(argsSupplementaires);
            string titre = a.Titre;
            bool simulation = ChkSimu.Checked && a.Simu;
            if (simulation) { args.Insert(0, "--simulation"); titre += " (simulation)"; }
            else
            {
                string conf = confirmation ?? a.Confirmer;
                if (conf != null && !BoiteMessage.Question(conf)) return false;
            }
            bool besoinAdmin = !a.SansAdmin && !Appli.EstAdmin;
            ViderJournal(); lignesAction.Clear();
            canal = new CanalSortie();
            if (!canal.Ecouter()) { Journal("ATTENTION : impossible de préparer l'action (canal de communication)."); return false; }
            if (besoinAdmin)
            {
                Journal("Windows demande ton autorisation pour cette action : clique sur « Oui » dans la fenêtre de Windows.");
                Statut("Autorisation demandée à Windows...");
                Application.DoEvents();
            }
            string erreur;
            var p = Elevation.LancerMoteur(args, !a.SansAdmin, canal, out erreur);
            if (p == null)
            {
                canal.Fermer();
                if (erreur == "refus") { Journal("Autorisation refusée : l'action n'a pas été lancée, rien n'a été modifié."); Statut("Action annulée (autorisation refusée)."); }
                else { Journal("ATTENTION : impossible de lancer l'action : " + erreur); Statut("Action impossible."); }
                return false;
            }
            proc = p; procEleve = besoinAdmin; finProc = null;
            actionEnCours = titre; cleEnCours = cle; apresAction = apres;
            Occupe(true, "En cours : " + titre + "...");
            JournalOuvert(true);
            minuteur.Start();
            return true;
        }

        private void Occupe(bool occupe, string texte)
        {
            foreach (var b in BoutonsActions) { var c = b.Cible as Cible; if (c != null && c.Type == "action") b.Enabled = !occupe && !(b.Text == "Indisponible"); }
            ChkSimu.Enabled = !occupe;
            boutonArret.Enabled = occupe;
            if (occupe) { progression.Demarrer(); BarreTaches.Indetermine(Handle); }
            else { progression.Arreter(); BarreTaches.Progression(Handle, -1); }
            Statut(texte, false, occupe ? Role.AccentVif : Role.Discret);
        }

        private void LireSortieMoteur()
        {
            if (canal == null || proc == null) return;
            bool fini;
            try { fini = proc.HasExited; } catch { fini = true; }
            if (fini)
            {
                // Programme terminé : on attend la fin du canal (dernières lignes), 3 s au plus
                if (!finProc.HasValue) finProc = DateTime.Now;
                if (!canal.Fini) { if ((DateTime.Now - finProc.Value).TotalSeconds < 3) fini = false; else canal.Fermer(); }
            }
            string ligne; int n = 0;
            while (canal.Lignes.TryDequeue(out ligne))
            {
                if (ligne.StartsWith("@@PROGRESSION"))
                {
                    var pp = ligne.Split(' ');
                    double d, f;
                    if (pp.Length >= 3 && double.TryParse(pp[1], out d) && double.TryParse(pp[2], out f)) progression.Etape(d, f);
                    continue;
                }
                if (ligne.StartsWith("@@DONNEES "))
                {
                    var morceaux = ligne.Split(new[] { ' ' }, 3);
                    if (morceaux.Length == 3) { try { RecevoirDonnees(morceaux[1], Json.Lire(morceaux[2])); } catch (Exception ex) { JournalDemarrage.Erreur("données du moteur", ex); } }
                    continue;
                }
                Journal(ligne); lignesAction.Add(ligne);
                if (++n >= 400) break;
            }
            if (n > 0)
            {
                var derniere = lignesAction.LastOrDefault(l => l.TrimStart().StartsWith("==="));
                if (derniere != null) Statut(actionEnCours + " : " + derniere.Replace("=", "").Trim(), false, Role.AccentVif);
            }
            if (fini && canal.Lignes.IsEmpty) Terminer();
        }

        private void Terminer()
        {
            minuteur.Stop();
            canal = null;
            string cle = cleEnCours;
            Occupe(false, "Terminé : " + actionEnCours + " (" + DateTime.Now.ToString("HH:mm") + ")");
            try { SystemSounds.Asterisk.Play(); } catch { }
            var resume = Resume();
            var apres = apresAction; apresAction = null;
            try { var h = ActionFinie; if (h != null) h(cle); } catch (Exception ex) { JournalDemarrage.Erreur("fin d'action", ex); }
            if (apres != null) { try { apres(); } catch (Exception ex) { JournalDemarrage.Erreur("après l'action", ex); } return; }
            if (resume.Count > 0)
            {
                string complet = actionEnCours + "\n\n" + string.Join("\n", resume);
                var court = resume.Where(r => System.Text.RegularExpressions.Regex.IsMatch(r, "TOTAL|libérés|installée|Terminé|terminé|activé|rétabli|Aucun|copiés|Sauvegarde|retirée")).Take(2).ToList();
                if (court.Count == 0) court = resume.Take(2).ToList();
                Notifier("Terminé : " + actionEnCours, string.Join("\n", court) + "\nClique pour voir le résumé complet.", "ok", () => BoiteMessage.Info(complet, "Nettoyage PC - terminé"));
            }
        }

        /// <summary>Lignes de la section « Résumé » du moteur.</summary>
        private List<string> Resume()
        {
            var r = new List<string>(); bool dedans = false;
            foreach (var l in lignesAction)
            {
                if (l.TrimStart().StartsWith("=== Résumé")) { dedans = true; continue; }
                if (!dedans) continue;
                if (l.StartsWith("Journal :") || l.StartsWith("Rapport :")) break;
                if (l.Trim() != "") r.Add(l.Trim());
            }
            return r;
        }

        /// <summary>Arrête l'action en cours ; renvoie faux si l'arrêt n'a pas été autorisé.</summary>
        public bool ArreterAction()
        {
            if (!ActionEnCours) return true;
            Statut("Arrêt en cours...");
            if (procEleve)
            {
                // L'action a l'autorisation administrateur : l'arrêter demande la même autorisation
                bool ok = Elevation.Commande(30, "arreter", proc.Id.ToString());
                bool termine; try { termine = proc.HasExited; } catch { termine = true; }
                if (!ok && !termine) { Journal("Arrêt non autorisé : l'action continue."); Statut("En cours : " + actionEnCours + "...", false, Role.AccentVif); return false; }
            }
            else
            {
                try { Programmes.Executer(Path.Combine(Chemins.Systeme32, "taskkill.exe"), "/PID " + proc.Id + " /T /F", 10000); } catch { }
                try { if (!proc.WaitForExit(5000)) proc.Kill(); } catch { }
            }
            Journal("");
            Journal("ATTENTION : action arrêtée par l'utilisateur.");
            return true;
        }
    }
}
