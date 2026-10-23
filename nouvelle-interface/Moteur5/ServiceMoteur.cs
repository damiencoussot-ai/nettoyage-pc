// Nettoyage PC 5 - exécution d'une action du moteur (même programme, option --moteur), comme la version 4 :
// le moteur écrit ses lignes dans un tube nommé local ; ici on les lit, on suit la progression et les données,
// et on récupère le résumé à la fin. L'autorisation de Windows n'est demandée que pour les actions qui touchent tout le PC.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class ServiceMoteur
    {
        private Process proc;
        private CanalSortie canal;
        private bool eleve;
        private DateTime? finProc;
        private readonly DispatcherTimer minuteur = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };

        public string Cle { get; private set; } = "";
        public string Titre { get; private set; } = "";
        public bool Simulation { get; private set; }
        public bool EnPreparation { get; private set; }   // autorisation demandée, programme pas encore lancé
        public readonly List<string> Lignes = new List<string>();
        /// <summary>Progression (étape faite / total) ; négative tant que le moteur n'en a pas donné.</summary>
        public double Fait = -1, Total = -1;
        public string DerniereEtape = "";
        public List<string> Resume = new List<string>();
        public bool Termine { get; private set; }
        public bool Arrete { get; private set; }

        /// <summary>Nouvelles lignes ou progression (fil de l'interface).</summary>
        public event Action Change;
        /// <summary>Données envoyées par le moteur (« @@DONNEES clé json »).</summary>
        public event Action<string, object> Donnees;
        /// <summary>Action terminée (fil de l'interface).</summary>
        public event Action Fini;

        public ServiceMoteur() { minuteur.Tick += (s, e) => Lire(); }

        public bool EnCours
        {
            get
            {
                if (EnPreparation || essaiEnCours) return true;
                try { return proc != null && !proc.HasExited; } catch { return false; }
            }
        }

        /// <summary>Lance l'action ; « surErreur » reçoit "refus" (autorisation refusée) ou le message d'erreur.</summary>
        public void Lancer(ActionMoteur a, bool simulation, IEnumerable<string> supplementaires, Action<string> surErreur)
        {
            if (EnCours) { surErreur("Une action est déjà en cours : attends qu'elle se termine."); return; }
            var args = new List<string>(a.Args);
            if (supplementaires != null) args.AddRange(supplementaires);
            Simulation = simulation && a.Simu;
            if (Simulation) args.Insert(0, "--simulation");
            Cle = a.Cle; Titre = a.Titre + (Simulation ? " (simulation)" : "");
            Lignes.Clear(); Resume = new List<string>(); Fait = Total = -1; DerniereEtape = ""; Termine = false; Arrete = false; finProc = null;
            canal = new CanalSortie();
            if (!canal.Ecouter()) { canal = null; surErreur("impossible de préparer l'action (canal de communication)"); return; }
            bool besoinAdmin = !a.SansAdmin && !Appli.EstAdmin;
            eleve = besoinAdmin;
            EnPreparation = true;
            Signaler();
            var c = canal;
            // hors du fil de l'interface : la fenêtre d'autorisation de Windows bloque l'appel tant qu'on n'a pas répondu
            Task.Run(() =>
            {
                string erreur;
                var p = Elevation.LancerMoteur(args, !a.SansAdmin, c, out erreur);
                Dispatcher.UIThread.Post(() =>
                {
                    EnPreparation = false;
                    if (p == null)
                    {
                        c.Fermer(); canal = null;
                        Signaler();
                        surErreur(erreur ?? "erreur inconnue");
                        return;
                    }
                    proc = p;
                    minuteur.Start();
                    Signaler();
                });
            });
        }

        private void Signaler() { var h = Change; if (h != null) h(); }

        private void Lire()
        {
            if (canal == null || proc == null) { minuteur.Stop(); return; }
            bool fini;
            try { fini = proc.HasExited; } catch { fini = true; }
            if (fini)
            {
                // programme terminé : on attend la fin du canal (dernières lignes), 3 s au plus
                if (!finProc.HasValue) finProc = DateTime.Now;
                if (!canal.Fini) { if ((DateTime.Now - finProc.Value).TotalSeconds < 3) fini = false; else canal.Fermer(); }
            }
            string ligne; int n = 0; bool change = false;
            while (canal.Lignes.TryDequeue(out ligne))
            {
                if (ligne.StartsWith("@@PROGRESSION"))
                {
                    var pp = ligne.Split(' ');
                    double d, t;
                    if (pp.Length >= 3 && double.TryParse(pp[1], out d) && double.TryParse(pp[2], out t)) { Fait = d; Total = t; change = true; }
                    continue;
                }
                if (ligne.StartsWith("@@DONNEES "))
                {
                    var morceaux = ligne.Split(new[] { ' ' }, 3);
                    if (morceaux.Length == 3)
                    {
                        try { var h = Donnees; if (h != null) h(morceaux[1], Json.Lire(morceaux[2])); }
                        catch (Exception ex) { JournalDemarrage.Erreur("données du moteur (version 5)", ex); }
                    }
                    continue;
                }
                Lignes.Add(ligne); change = true;
                if (ligne.TrimStart().StartsWith("===")) DerniereEtape = ligne.Replace("=", "").Trim();
                if (++n >= 400) break;
            }
            if (change) Signaler();
            if (fini && canal.Lignes.IsEmpty) Terminer();
        }

        private void Terminer()
        {
            minuteur.Stop();
            canal = null;
            Termine = true;
            Resume = ExtraireResume(Lignes);
            Natif5.Son();
            Signaler();
            var h = Fini; if (h != null) h();
        }

        /// <summary>Lignes de la section « Résumé » du moteur.</summary>
        public static List<string> ExtraireResume(IEnumerable<string> lignes)
        {
            var r = new List<string>(); bool dedans = false;
            foreach (var l in lignes)
            {
                if (l.TrimStart().StartsWith("=== Résumé")) { dedans = true; continue; }
                if (!dedans) continue;
                if (l.StartsWith("Journal :") || l.StartsWith("Rapport :")) break;
                if (l.Trim() != "") r.Add(l.Trim());
            }
            return r;
        }

        /// <summary>Arrête l'action ; avec l'autorisation administrateur, l'arrêt demande la même autorisation.</summary>
        public void Arreter(Action<bool> resultat)
        {
            if (!EnCours || proc == null) { resultat(true); return; }
            var p = proc; bool adm = eleve;
            Task.Run(() =>
            {
                bool ok = true;
                if (adm)
                {
                    ok = Elevation.Commande(30, "arreter", p.Id.ToString());
                    bool termine; try { termine = p.HasExited; } catch { termine = true; }
                    ok = ok || termine;
                }
                else
                {
                    try { Programmes.Executer(Path.Combine(Chemins.Systeme32, "taskkill.exe"), "/PID " + p.Id + " /T /F", 10000); } catch { }
                    try { if (!p.WaitForExit(5000)) p.Kill(); } catch { }
                }
                Dispatcher.UIThread.Post(() =>
                {
                    if (ok) { Arrete = true; Lignes.Add(""); Lignes.Add("ATTENTION : action arrêtée par l'utilisateur."); Signaler(); }
                    resultat(ok);
                });
            });
        }

        // ------------------------------------------------------------ essais (captures) : une action fictive affichée telle quelle
        private bool essaiEnCours;

        public void Montrer(string titre, IEnumerable<string> lignes, double fait, double total, bool termine, bool simulation)
        {
            essaiEnCours = !termine; Simulation = simulation; Arrete = false;
            Titre = titre; Lignes.Clear(); Lignes.AddRange(lignes); Fait = fait; Total = total; Termine = termine;
            DerniereEtape = Lignes.LastOrDefault(l => l.TrimStart().StartsWith("===")) ?? "";
            DerniereEtape = DerniereEtape.Replace("=", "").Trim();
            Resume = termine ? ExtraireResume(Lignes) : new List<string>();
            Signaler();
        }
    }

    internal static class Natif5
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool MessageBeep(uint type);
        public static void Son() { if (OperatingSystem.IsWindows()) { try { MessageBeep(0x40); } catch { } } }
    }
}
