// Nettoyage PC - catalogue des actions du moteur : titre, options, simulation possible, autorisation, confirmation.
// Partagé par la version 4 et la version 5 (même programme, option --moteur).
using System;
using System.Collections.Generic;
using System.Linq;

namespace NettoyagePC
{
    internal class ActionMoteur
    {
        public string Cle, Titre, Confirmer, Requiert;
        public string[] Args = new string[0];
        /// <summary>Simu : l'action peut être simulée (case « Mode simulation »). SansAdmin : pas d'autorisation de Windows.</summary>
        public bool Simu, SansAdmin;
    }

    internal static class CatalogueActions
    {
        /// <summary>Pourquoi une action est indisponible (clé de « Requiert »).</summary>
        public static readonly Dictionary<string, string> Raisons = new Dictionary<string, string>
        {
            { "winget", "winget (App Installer) n'est pas installé sur ce PC." }, { "defender", "Microsoft Defender n'est pas l'antivirus actif sur ce PC." },
            { "stockage", "Les outils de stockage de Windows sont absents de ce PC." }, { "mdsched", "Le Diagnostic de mémoire Windows est absent de ce PC." },
            { "restaurer", "Les points de restauration ne sont pas disponibles sur ce PC." }
        };

        /// <summary>Outils de Windows présents sur ce PC (clés de « Requiert »).</summary>
        public static Dictionary<string, bool> Disponibilites()
        {
            var d = new Dictionary<string, bool>();
            d["winget"] = InfosPc.Winget() != null;
            d["defender"] = true;   // vérifié en arrière-plan après l'ouverture (requête WMI lente)
            d["stockage"] = Programmes.ExisteSysteme("defrag.exe");
            d["mdsched"] = Programmes.ExisteSysteme("mdsched.exe");
            d["restaurer"] = Programmes.ExisteSysteme("rstrui.exe");
            if (!Natif.EstWindows) foreach (var k in d.Keys.ToList()) d[k] = true;   // (essais hors Windows)
            return d;
        }

        public static Dictionary<string, ActionMoteur> Creer()
        {
            var actions = new Dictionary<string, ActionMoteur>();
            Action<string, string, string, bool, bool, string, string> act = (cle, titre, args, simu, sansAdmin, confirmer, requiert) =>
                actions[cle] = new ActionMoteur { Cle = cle, Titre = Langue.T(titre), Args = args.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), Simu = simu, SansAdmin = sansAdmin, Confirmer = confirmer, Requiert = requiert };
            act("standard", "Nettoyage rapide", "", true, true, "Lancer le nettoyage rapide ?\n\nTemporaires et caches supprimés, corbeille vidée, téléchargements de plus de 90 jours rangés dans « _A_verifier ». Ferme tes navigateurs pour un meilleur résultat.", null);
            act("complet", "Optimisation complète", "--profond --disques --liberer-ram --optimiser --securite", true, false, "Lancer l'optimisation complète ?\n\nUn point de restauration est créé d'abord. Puis : nettoyage, nettoyage profond de Windows, optimisation des disques, libération de la RAM, bilans d'optimisation et de sécurité.\n\nCela peut durer longtemps : évite d'éteindre le PC et enregistre ton travail.", null);
            act("analyse", "Analyse complète (sans modification)", "--simulation --profond --disques --liberer-ram --optimiser --securite", false, false, null, null);
            act("disques", "Optimisation des disques", "--disques-seuls", true, false, "Optimiser les disques maintenant ?\n\nSSD : ReTrim (rapide, jamais de défragmentation). Disques durs : défragmentation (peut être long).", "stockage");
            act("verifier", "Vérification des disques", "--disques-seuls --verifier", true, false, "Optimiser et vérifier les disques ?\n\nL'analyse du système de fichiers est en lecture seule mais peut durer plusieurs minutes par disque.", "stockage");
            act("ramlib", "Libération de la RAM", "--ram-seule --liberer-ram", true, false, null, null);
            act("raminfo", "Infos mémoire vive", "--ram-seule", false, true, null, null);
            act("ramtest", "Test des barrettes", "--tester-ram", false, true, "Ouvrir le Diagnostic de mémoire Windows ?\n\nIl te proposera de redémarrer le PC pour tester les barrettes. Enregistre ton travail avant.", "mdsched");
            act("rapport", "Rapport du PC", "--rapport-seul", false, true, null, null);
            act("analyselite", "Analyse (sans modification)", "--simulation", false, true, null, null);
            act("compat", "Compatibilité de ce PC", "--compat", false, true, null, null);
            act("planifier", "Planification", "--planifier", false, false, "Activer l'entretien automatique ?\n\n- Nettoyage rapide : chaque dimanche à 10h\n- Optimisation complète : toutes les 4 semaines, le dimanche à 11h\n- Surveillance des disques : chaque jour\n\nNe déplace plus le dossier de l'application ensuite.", null);
            act("deplanifier", "Suppression de la planification", "--deplanifier", false, false, null, null);
            act("planifierapps", "Mises à jour automatiques des applications", "--planifier-apps", false, false, "Activer les mises à jour automatiques des applications ?\n\nChaque mercredi à 19h, winget (l'outil officiel de Microsoft) met à jour tes applications, sauf celles que tu as exclues. Si le PC est éteint, elles se font dès qu'il est allumé.", "winget");
            act("deplanifierapps", "Arrêt des mises à jour automatiques des applications", "--deplanifier-apps", false, false, null, null);
            act("optim", "Bilan d'optimisation", "--optimiser --seul", false, false, null, null);
            act("perf", "Mode performances", "--mode-alim Performances --seul", true, false, "Activer le mode « Performances élevées » ?\n\nPlus réactif, consommation un peu plus élevée (autonomie réduite sur portable). Réversible avec « Mode équilibré ».", null);
            act("equilibre", "Mode équilibré", "--mode-alim Equilibre --seul", true, false, null, null);
            act("majapps", "Mise à jour des applications", "--maj-apps --seul", true, false, "Mettre à jour toutes les applications ?\n\nwinget, l'outil officiel de Microsoft, télécharge les nouvelles versions depuis les éditeurs. Ferme si possible les applications concernées. Cela peut durer longtemps.", "winget");
            act("secu", "Bilan de sécurité", "--securite --seul", false, false, null, null);
            act("antivirus", "Analyse antivirus", "--securite --antivirus --seul", true, false, null, "defender");
            act("reparer", "Réparation de Windows", "--reparer --seul", true, false, "Réparer Windows ?\n\nUn point de restauration est créé, puis DISM et SFC vérifient et réparent les fichiers système (20 à 50 minutes). Évite d'éteindre le PC pendant l'opération.", null);
            act("point", "Point de restauration", "--point-restauration --seul", false, false, null, "restaurer");
            act("modejeu", "Mode jeu", "--mode-jeu --ram-seule --liberer-ram --seul", true, false, null, null);
            act("finmodejeu", "Fin du mode jeu", "--fin-mode-jeu --seul", true, true, null, null);
            act("sauvegarde", "Sauvegarde", "", true, true, null, null);
            act("majwin", "Mises à jour Windows et pilotes", "", true, false, null, null);
            act("pilotes", "Nettoyage des anciens pilotes", "--anciens-pilotes --seul", true, false, null, null);
            act("santedisques", "Santé détaillée des disques", "--sante-disques", false, false, null, null);
            act("demarrage", "Analyse du démarrage", "--analyse-demarrage", false, false, null, null);
            act("kit", "Kit de réinstallation", "--kit-reinstallation", false, false, null, null);
            act("plantages", "Analyse des plantages", "--analyse-plantages", false, true, null, null);
            return actions;
        }
    }
}
