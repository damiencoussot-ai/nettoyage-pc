// Nettoyage PC - ce qui est activé, et est-ce que ça fonctionne : entretien automatique, gardien et ses fonctions,
// mises à jour automatiques, mode jeu, Premium ; plus quelques vérifications de l'application elle-même (dossier des
// données, réglages, erreurs récentes). Lecture seule : rien n'est modifié ici. Sans interface : partagé v4 / v5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal class EtatFonction
    {
        public string Id = "", Nom = "", Groupe = "", Detail = "", Probleme = "";
        public bool Actif, Premium;
        /// <summary>Correction proposée quand il y a un problème (clé lue par l'interface), sinon "".</summary>
        public string Reparation = "";
        public bool Ok { get { return Probleme == ""; } }
    }

    internal static class EtatFonctions
    {
        public const string Automatique = "Fonctions automatiques", Application = "Fonctionnement de l'application";

        /// <summary>Relevé complet (lent : tâches planifiées, winget). À appeler hors du fil de l'interface.</summary>
        public static List<EtatFonction> Collecter()
        {
            var l = new List<EtatFonction>();
            ReglagesGardien r;
            try { r = ReglagesGardien.Lire(); } catch { r = new ReglagesGardien(); }
            bool premium = Licence.Premium;
            bool gardien = r.Actif && premium;
            Func<string, string, bool, bool, EtatFonction> ajouter = (id, nom, actif, prem) =>
            {
                var e = new EtatFonction { Id = id, Nom = nom, Actif = actif, Premium = prem, Groupe = Automatique };
                l.Add(e); return e;
            };

            // ---- Premium
            var p = ajouter("premium", "Premium", premium, false);
            p.Groupe = Application;
            p.Detail = Licence.EnEssai ? Langue.F("Essai gratuit : {0} jour(s) restant(s).", Licence.JoursEssaiRestants)
                : premium ? Langue.T("Actif sur ce PC, pour les versions 4 et 5.") : Langue.T("Version gratuite : les outils Premium sont verrouillés.");

            // ---- entretien automatique (tâches planifiées)
            bool hebdo = false, complet = false, surveillance = false;
            try { hebdo = Moteur.TacheExiste(Moteur.TacheHebdo); complet = Moteur.TacheExiste(Moteur.TacheComplet); surveillance = Moteur.TacheExiste(Moteur.TacheSurveillance); } catch { }
            var en = ajouter("entretien", "Entretien automatique", hebdo, false);
            en.Detail = Langue.T(hebdo ? "Nettoyage rapide chaque dimanche, optimisation complète toutes les 4 semaines, surveillance des disques." : "Désactivé : ton PC ne se nettoie pas tout seul.");
            if (hebdo && (!complet || !surveillance)) { en.Probleme = Langue.T("Une partie des tâches manque : réactive l'entretien automatique pour les remettre."); en.Reparation = "entretien"; }

            // ---- recherche des nouvelles versions
            bool verif = true;
            try { verif = Parametres.Lire().VerifAuto; } catch { }
            var v = ajouter("verifmaj", "Recherche des nouvelles versions", verif, false);
            v.Detail = Langue.T(verif ? "À l'ouverture, puis toutes les 6 heures si la fenêtre reste ouverte." : "Désactivée : cherche toi-même dans Mises à jour.");

            // ---- gardien et ses fonctions (Premium)
            var g = ajouter("gardien", "Gardien en temps réel", gardien, true);
            g.Detail = Langue.T(r.Actif ? "Veille en continu : démarrage, disque, antivirus, mémoire, chauffe." : "Désactivé.");
            if (r.Actif && !premium) g.Probleme = Langue.T("En pause : Premium n'est plus actif sur ce PC.");
            else if (gardien && Natif.EstWindows)
            {
                bool tourne = false, auto = false;
                try { tourne = Gardien.EnCours; auto = Gardien.LancementAutomatique; } catch { }
                if (!auto) { g.Probleme = Langue.T("Il ne se relancera pas à la prochaine ouverture de session."); g.Reparation = "gardien-activer"; }
                else if (!tourne) { g.Probleme = Langue.T("Activé, mais il ne tourne pas en ce moment."); g.Reparation = "gardien-demarrer"; }
            }
            string sansGardien = Langue.T("Le gardien est désactivé : cette fonction ne se fait pas.");

            var s = ajouter("sauvegardeauto", "Sauvegarde automatique", r.SauvegardeAuto && gardien, true);
            s.Detail = DerniereSauvegarde(r.SauvegardeAuto);
            if (r.SauvegardeAuto && premium && !r.Actif) { s.Probleme = sansGardien; s.Reparation = "gardien"; }

            var mj = ajouter("modejeuauto", "Mode jeu automatique", r.ModeJeuAuto && gardien, true);
            string jeu = null;
            try { jeu = ModeJeuAuto.JeuActif(); } catch { }
            mj.Detail = !string.IsNullOrEmpty(jeu) && ModeJeuAuto.Actif ? Langue.F("En cours : {0}.", jeu) : Langue.T(r.ModeJeuAuto ? (r.PauseOneDrive ? "Se lance avec tes jeux, OneDrive en pause pendant la partie." : "Se lance tout seul avec tes jeux.") : "Désactivé.");
            if (r.ModeJeuAuto && premium && !r.Actif) { mj.Probleme = sansGardien; mj.Reparation = "gardien"; }

            bool apps = false;
            try { apps = MajAppsAuto.Active; } catch { }
            var ma = ajouter("majappsauto", "Mises à jour automatiques des applications", apps, true);
            DateTime? der = null;
            try { der = MajAppsAuto.Derniere(); } catch { }
            ma.Detail = !apps ? Langue.T("Désactivées.") : der.HasValue ? Langue.F("Chaque mercredi à 19 h. Dernière fois : {0}.", der.Value.ToLocalTime().ToString("dd/MM/yyyy HH:mm")) : Langue.T("Chaque mercredi à 19 h. Pas encore lancées.");
            if (apps && !premium) ma.Probleme = Langue.T("En pause : Premium n'est plus actif sur ce PC.");
            else if (apps && Natif.EstWindows) { string w = null; try { w = InfosPc.Winget(); } catch { } if (w == null) ma.Probleme = Langue.T("winget est introuvable : installe « App Installer » depuis le Microsoft Store."); }

            var b = ajouter("bulletin", "Bulletin mensuel du PC", r.Bulletin && gardien, true);
            FileInfo dernier = null;
            try { dernier = Bulletin.Liste().FirstOrDefault(); } catch { }
            b.Detail = !(r.Bulletin && gardien) ? Langue.T("Avec le gardien activé, un bulletin chaque début de mois.") : dernier != null ? Langue.F("Dernier bulletin : {0}.", dernier.LastWriteTime.ToString("dd/MM/yyyy")) : Langue.T("Le premier arrive au début du mois prochain.");

            var ap = ajouter("appareils", "Surveillance du réseau", r.NouveauxAppareils && gardien, true);
            ap.Detail = Langue.T(r.NouveauxAppareils && gardien ? "Alerte quand un appareil inconnu se connecte à ta box." : "Avec le gardien activé, alerte pour les appareils inconnus.");

            // ---- mode jeu manuel (oublié ?)
            string fJeu = Chemins.Fichier("modejeu.json");
            if (File.Exists(fJeu))
            {
                var m = ajouter("modejeu", "Mode jeu", true, false);
                m.Groupe = Application;
                DateTime debut = DateTime.Now;
                try { debut = File.GetLastWriteTime(fJeu); } catch { }
                m.Detail = Langue.F("Activé depuis le {0}.", debut.ToString("dd/MM/yyyy HH:mm"));
                if ((DateTime.Now - debut).TotalHours > 12) { m.Probleme = Langue.T("Toujours actif après ta partie ? Termine-le pour revenir aux réglages d'avant."); m.Reparation = "finmodejeu"; }
            }

            // ---- l'application elle-même
            l.Add(Dossier());
            l.Add(Reglages());
            l.Add(Erreurs());
            return l;
        }

        private static string DerniereSauvegarde(bool active)
        {
            if (!active) return Langue.T("Désactivée.");
            try
            {
                var d = Gardien.DerniereSauvegarde(); DateTime t;
                if (d != null && DateTime.TryParse(Json.Texte(d, "Date"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t))
                    return Langue.F("Dernière sauvegarde : {0}.", t.ToLocalTime().ToString("dd/MM/yyyy HH:mm"));
            }
            catch { }
            return Langue.T("Pas encore de sauvegarde : branche ta clé USB ou ton disque externe.");
        }

        private static EtatFonction Dossier()
        {
            var e = new EtatFonction { Id = "donnees", Nom = "Dossier de Nettoyage PC", Groupe = Application, Actif = true, Detail = Chemins.Donnees };
            foreach (var d in new[] { Chemins.Donnees, Chemins.Local })
            {
                try
                {
                    Directory.CreateDirectory(d);
                    string f = Path.Combine(d, ".essai-ecriture");
                    File.WriteAllText(f, "ok"); File.Delete(f);
                }
                catch (Exception ex) { e.Actif = false; e.Probleme = Langue.F("Impossible d'écrire dans « {0} » :", d) + " " + ex.Message; break; }
            }
            return e;
        }

        private static EtatFonction Reglages()
        {
            var e = new EtatFonction { Id = "reglages", Nom = "Fichier des réglages", Groupe = Application, Actif = true, Detail = Langue.T("Lu sans problème.") };
            try
            {
                if (!File.Exists(Parametres.Fichier)) e.Detail = Langue.T("Pas encore créé : les réglages par défaut sont utilisés.");
                else if (!(Json.LireFichier(Parametres.Fichier) is Dictionary<string, object>)) { e.Actif = false; e.Detail = Parametres.Fichier; e.Probleme = Langue.T("Illisible : les réglages par défaut sont utilisés. Change un réglage pour le réécrire."); }
            }
            catch (Exception ex) { e.Actif = false; e.Probleme = ex.Message; }
            return e;
        }

        /// <summary>Erreurs notées dans le journal de démarrage ces 7 derniers jours.</summary>
        public static int ErreursRecentes()
        {
            int n = 0;
            try
            {
                if (!File.Exists(JournalDemarrage.Fichier)) return 0;
                var limite = DateTime.Now.AddDays(-7);
                foreach (var ligne in File.ReadAllLines(JournalDemarrage.Fichier))
                {
                    if (ligne.Length < 26 || ligne.IndexOf("ERREUR (", 19, StringComparison.Ordinal) < 0) continue;
                    DateTime d;
                    if (DateTime.TryParseExact(ligne.Substring(0, 19), "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && d >= limite) n++;
                }
            }
            catch { }
            return n;
        }

        private static EtatFonction Erreurs()
        {
            int n = ErreursRecentes();
            var e = new EtatFonction { Id = "erreurs", Nom = "Erreurs récentes", Groupe = Application, Actif = n == 0, Detail = Langue.T("Aucune erreur ces 7 derniers jours.") };
            if (n > 0) { e.Detail = Langue.T("Notées dans le journal de démarrage (à envoyer à la personne qui t'aide si un outil ne marche pas)."); e.Probleme = Langue.F("{0} erreur(s) ces 7 derniers jours.", n); e.Reparation = "journal"; }
            return e;
        }
    }
}
