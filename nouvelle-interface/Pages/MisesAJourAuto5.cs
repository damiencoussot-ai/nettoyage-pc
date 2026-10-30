// Nettoyage PC 5 - à l'ouverture : nouveautés de la version qui vient d'être installée, puis recherche d'une nouvelle
// version (source officielle GitHub) avec « Installer maintenant » / « Plus tard ». Refaite toutes les 6 heures si la
// fenêtre reste ouverte. Désactivable (réglage « vérifier les mises à jour automatiquement » partagé avec la version 4).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal static class MisesAJourAuto5
    {
        private static bool proposee, installation;
        private static DispatcherTimer minuteur;

        /// <summary>Appelé quand la fenêtre principale s'ouvre (pas dans l'aperçu, ni pendant les essais).</summary>
        public static void AuDemarrage(FenetrePrincipale f, bool profilCree)
        {
            bool nouveautes = false;
            try { nouveautes = profilCree && MontrerNouveautes(f); } catch (Exception ex) { JournalDemarrage.Erreur("nouveautés (version 5)", ex); }
            Parametres p;
            try { p = Parametres.Lire(); } catch { p = new Parametres(); }
            if (!p.VerifAuto) return;
            // juste après une mise à jour, inutile de chercher tout de suite : la prochaine ouverture le fera
            if (!nouveautes) Chercher(f);
            minuteur = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
            minuteur.Tick += (s, e) => Chercher(f);
            minuteur.Start();
        }

        /// <summary>Nouveautés des versions installées depuis la dernière ouverture (une seule fois).</summary>
        private static bool MontrerNouveautes(FenetrePrincipale f)
        {
            var p = Parametres.Lire();
            string avant = p.DerniereVersionLancee ?? "";
            if (avant == Appli.Version) return false;
            p.DerniereVersionLancee = Appli.Version;
            p.Ecrire();
            if (avant == "" || MisesAJour.Comparer(avant, Appli.Version) >= 0) return false;
            var versions = JournalVersions.Liste.Where(x => MisesAJour.Comparer(x.Key, avant) > 0 && MisesAJour.Comparer(x.Key, Appli.Version) <= 0).ToList();
            if (versions.Count == 0) return false;
            var lignes = new List<string>();
            foreach (var v in versions.Take(3))
            {
                if (versions.Count > 1) lignes.Add(Langue.T("Version") + " " + v.Key);
                lignes.AddRange(v.Value.Select(l => "•  " + Langue.T(l)));
                lignes.Add("");
            }
            f.Demander(Langue.F("Nettoyage PC {0} est installé : voici les nouveautés", Appli.Version), string.Join("\n", lignes).Trim(), "Super", null, r => { });
            return true;
        }

        private static void Chercher(FenetrePrincipale f)
        {
            if (proposee || installation) return;
            Task.Run(() => { try { return MisesAJour.Verifier(); } catch { return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                var v = t.Result == null ? null : t.Result.FirstOrDefault();
                if (v == null || v.UrlSetup == "" || v.UrlHash == "" || MisesAJour.Comparer(v.Version, Appli.Version) <= 0 || proposee || installation) return;
                proposee = true;   // une seule question par ouverture (« Plus tard »)
                var integre = JournalVersions.Liste.FirstOrDefault(x => x.Key == v.Version);
                var points = integre.Value != null ? integre.Value.Select(Langue.T).ToList()
                    : Regex.Split(v.Notes ?? "", "\r?\n").Select(n => n.Trim()).Where(n => n.StartsWith("-")).Select(n => n.TrimStart('-', ' ')).ToList();
                string texte = (points.Count > 0 ? string.Join("\n", points.Take(4).Select(x => "•  " + x)) + "\n\n" : "")
                    + Langue.T("L'application se ferme puis se rouvre toute seule (moins d'une minute). Ton historique, tes réglages et Premium sont conservés.");
                f.Demander(Langue.F("Nettoyage PC {0} est disponible", v.Version), texte, "Installer maintenant", "Plus tard", oui => { if (oui) Installer(f, v, null); });
            }));
        }

        /// <summary>
        /// Téléchargement depuis la page officielle, contrôle de l'empreinte SHA256 (obligatoire), puis installation silencieuse
        /// avec l'autorisation de Windows ; l'application se ferme et l'installateur la rouvre. echec : appelé si rien n'est installé.
        /// </summary>
        public static void Installer(FenetrePrincipale F, VersionPubliee v, Action echec)
        {
            if (v == null || installation) return;
            if (F.Moteur.EnCours) { F.Demander("Une action est en cours", "Attends qu'elle se termine avant de mettre à jour.", "Compris", null, r => { }); if (echec != null) echec(); return; }
            installation = true;
            F.Statut(Langue.F("Téléchargement de la version {0}…", v.Version));
            string err = null, empreinte = null;
            Task.Run(() =>
            {
                string fichier;
                try { string h; fichier = MisesAJour.Telecharger(v, out h); empreinte = h; } catch (Exception ex) { err = ex.Message; return false; }
                // le gardien garde le programme ouvert : il se relancera avec la nouvelle version
                try { Gardien.Arreter(); } catch { }
                // seule une copie placée dans Program Files et revérifiée là-bas est lancée (autorisation de Windows)
                bool ok = Elevation.Commande(90, "installer-maj", fichier, empreinte);
                if (!ok) { err = Langue.T("l'autorisation de Windows a été refusée, ou le fichier n'a pas passé la vérification de sécurité."); try { Gardien.DemarrerSiBesoin(); } catch { } }
                try { File.Delete(fichier); Directory.Delete(Path.GetDirectoryName(fichier)); } catch { }
                return ok;
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (t.Result) { F.Statut("Installation de la mise à jour…"); F.Close(); return; }
                installation = false;
                if (echec != null) echec();
                F.Statut("Mise à jour non installée.", true);
                F.Demander("Mise à jour non installée", err ?? "", "Compris", null, r => { }, true);
            }));
        }
    }
}
