// Nettoyage PC - nouveautés de chaque version (affichées même sans Internet). Partagé par la version 4 et la version 5.
using System.Collections.Generic;

namespace NettoyagePC
{
    internal static class JournalVersions
    {
        public static readonly List<KeyValuePair<string, string[]>> Liste = new List<KeyValuePair<string, string[]>>
        {
            N("4.6", "Interface encore plus futuriste et fluide : un indicateur lumineux glisse dans le menu, une lumière parcourt l'en-tête quand tu changes de rubrique, l'anneau de l'accueil respire, et les boutons principaux s'illuminent au survol.",
                "Plus simple : l'accueil montre d'abord ce qu'il y a à faire, et ce qui va bien tient sur une seule ligne. Au premier lancement, tu choisis entre le mode simple (trois gros boutons) et le mode complet.",
                "Le briefing vocal est retiré : Nettoyage PC t'accueille toujours par ton pseudo, sans parler.",
                "Premium : l'achat par Stripe est prêt, avec Premium activé tout seul juste après le paiement (la boutique ouvrira bientôt)."),
            N("4.5.1", "Corrections de l'accueil : boutons, mesures et liste se placent bien sous le texte d'état, même sur plusieurs lignes ou quand la page est descendue.",
                "Briefing vocal : le premier briefing après la création du profil n'est plus coupé, et les boutons « Écouter » suivent mieux la voix de Windows.",
                "Petits écrans (portables 1366 × 768) : les fenêtres restent entières à l'écran, avec une barre de défilement si besoin.",
                "Assistant vocal (ORION) : « NettoyagePC.exe --etat » indique maintenant si Premium est actif. Espace libre des disques traduit en anglais et en espagnol."),
            N("4.5", "Nouvelle interface « Noyau » : un anneau holographique à l'accueil (stockage, mémoire, sécurité, matériel), un menu numéroté, des lignes fines et des coins lumineux, la police Bahnschrift de Windows. En clair comme en sombre.",
                "Faisons connaissance : au premier lancement, choisis ton pseudo, ta couleur et le briefing vocal. Ton profil reste sur ce PC, sans mot de passe ni compte en ligne.",
                "Briefing vocal : à l'ouverture (une fois par jour, par défaut), Nettoyage PC te salue par ton pseudo et te dit l'état du PC et les améliorations proposées, avec la voix de Windows. Bouton « Écouter le briefing » sur l'accueil et en mode simple."),
            N("4.4", "Pourquoi mon PC ralentit ? (Premium) : l'enquêteur retrace tout ce qui a changé sur le PC (applications, pilotes, mises à jour, programmes au démarrage, réglages) et le compare aux performances pour désigner le coupable.",
                "Santé prédictive (Premium) : erreurs du matériel et du disque, arrêts brutaux, usure des SSD, batterie et chauffe, avec la tendance et une prévision « à ce rythme ».",
                "Assistant local (Premium) : pose tes questions avec tes mots ; l'IA tourne sur ton PC (Ollama), rien ne part sur Internet. Elle propose des boutons, tu confirmes. Commandes pour assistant vocal (ORION).",
                "Preuve en FPS (Premium) : les images par seconde réelles d'un jeu (moyenne, 1 % et 0,1 % les plus lents) avant / après un réglage, mesurées par PresentMon d'Intel.",
                "Coût en électricité (Premium) : combien le PC coûte en euros par jour, mois et an, et quelles applications consomment le plus (estimation relevée par le gardien).",
                "Bouclier anti-arnaque (gratuit) : repère les logiciels de prise en main à distance (AnyDesk, TeamViewer...) et explique quoi faire face à un faux technicien. Il ne ferme rien sans ton accord.",
                "Plein potentiel (gratuit, Outils) : écran réglé en dessous de sa fréquence, mauvais câble ou mauvaise prise, Resizable BAR, liaison PCIe, mémoire en simple canal ou sans EXPO/XMP."),
            N("4.3.4", "Le nouveau logo s'affiche tout de suite sur le Bureau et dans le menu Démarrer après une mise à jour : Nettoyage PC demande à Windows de rafraîchir ses icônes (avant, Windows gardait parfois l'ancienne image)."),
            N("4.3.3", "Nouveau logo : un hexagone néon (cyan, violet, rose) autour d'une étincelle, dans la fenêtre, la barre des tâches, le menu Démarrer et l'installateur."),
            N("4.3.2", "Corrections après l'essai sur un vrai PC : la recherche automatique des mises à jour à l'ouverture se termine bien (le bandeau « Installer maintenant » apparaît), la ligne choisie dans les listes est lisible en thème sombre, la carte graphique dédiée est reconnue avant la puce intégrée au processeur.",
                "Finitions : listes déroulantes aux couleurs du thème, graphique de l'historique plus lisible, recherche (Ctrl+K) qui classe les meilleurs résultats en premier."),
            N("4.3.1", "Mises à jour plus visibles : à chaque ouverture, Nettoyage PC regarde s'il existe une nouvelle version. Si oui, un bandeau s'affiche en haut de la fenêtre, sur toutes les pages (et en mode simple), avec « Installer maintenant ». Plus besoin d'aller dans Mises à jour.",
                "« Plus tard » cache le bandeau jusqu'à la prochaine ouverture. Si l'application reste ouverte longtemps, la vérification est refaite toutes les 6 heures."),
            N("4.3", "Plus fluide : la fenêtre s'ouvre plus vite, les pages s'affichent d'un coup sans clignoter, défilement doux à la molette, survols et sélection du menu animés. Les listes longues à lire se chargent en arrière-plan.",
                "Plus simple : 6 rubriques principales (Mises à jour, Historique et Réglages en bas du menu). Chaque page montre l'essentiel, le reste est rangé derrière « Plus d'options ».",
                "Les cartes remplissent toute la largeur de la fenêtre ; un seul bouton pour le mode d'alimentation et pour l'entretien automatique."),
            N("4.2", "Premium s'enrichit : gardien en temps réel (alertes et nettoyage léger automatique quand le disque se remplit), sauvegarde automatique dès que la clé USB est branchée, analyse des plantages expliquée simplement, santé de la batterie et thèmes de couleur exclusifs.",
                "Essai gratuit de Premium pendant 7 jours, sans paiement ni adresse e-mail. Après l'achat : satisfait ou remboursé 14 jours.",
                "Nouvelle page Premium, badge dans le menu, icônes sur les cartes et couleur de l'interface au choix (Réglages).",
                "Informations légales consultables à tout moment (Réglages) : contrat de licence, conditions de vente, confidentialité, mentions légales. Ni publicité, ni pistage, ni télémétrie.",
                "Tout ce qui était gratuit le reste."),
            N("4.1", "Nouvel onglet Premium (9,99 €, une seule fois, 3 PC) : analyse du démarrage (qui ralentit ton PC, avec le nom précis), mesure des performances avant / après un réglage, kit de réinstallation (applications, pilotes et Wi-Fi remis en place en quelques minutes).",
                "Mode simple (Réglages) : un seul écran avec trois gros boutons « Nettoyer mon PC », « Vérifier mon PC » et « Appeler à l'aide », idéal pour un débutant.",
                "Mises à jour plus sûres : chaque nouvelle version se contrôle elle-même à l'installation ; en cas de problème, la version précédente revient toute seule.",
                "Tout ce qui était gratuit le reste."),
            N("4.0.1", "La fenêtre des nouveautés ne revient plus à chaque démarrage : après une mise à jour, une petite notification s'affiche une seule fois dans le coin (un clic montre les nouveautés).",
                "Enregistrement des réglages plus fiable, et les nouveautés restent marquées comme vues même si une ancienne version de Nettoyage PC est relancée."),
            N("4.0", "Entièrement réécrit : Nettoyage PC s'ouvre en une seconde (au lieu d'une dizaine), l'interface est plus fluide et tient en un seul programme.",
                "Matériel : détecte la mémoire vive bridée (profil EXPO / XMP désactivé) avec le guide pour ta carte mère, et affiche la santé détaillée des disques (usure, température, heures d'utilisation, erreurs).",
                "Réseau : ping, débit Internet, comparaison des serveurs DNS et changement en un clic (réversible).",
                "Aide à distance : l'Assistance rapide de Windows pour qu'un proche t'aide, avec un résumé du PC à lui copier.",
                "Anciens pilotes : retire les anciennes versions que Windows garde (souvent plusieurs Go pour la carte graphique).",
                "Applications préinstallées inutiles : retire les applications publicitaires de Windows, réinstallables depuis le Microsoft Store.",
                "English / Español : l'interface existe aussi en anglais et en espagnol (Réglages > Langue)."),
            N("3.1", "Démarrage corrigé : l'application ne se referme plus toute seule juste après l'écran de démarrage. Un journal de démarrage et un mode sans échec automatique ont été ajoutés.", "Plus sûr : Nettoyage PC ne tourne plus en administrateur. Windows demande l'autorisation seulement au moment d'une action qui touche tout le PC (nettoyage profond, disques, réparation...), et uniquement pour elle.", "Mode sombre retravaillé : messages, infobulles, listes déroulantes et cases à cocher aux couleurs du thème, contrastes revus ; le thème « automatique » suit Windows en direct."),
            N("3.0.1", "Audit de sécurité : le nettoyage ne traverse plus jamais un dossier « lien » (jonction), même placé exprès dans un dossier temporaire.", "Mises à jour : empreinte SHA256 obligatoire, téléchargement depuis GitHub uniquement, dans un dossier protégé.", "Jeux, navigateurs, liens, documents et désinstallateurs « par utilisateur » s'ouvrent sans les droits administrateur ; l'Historique vérifie chaque annulation avant de l'exécuter."),
            N("3.0", "Nouvelle interface « futur » : thème sombre bleu nuit avec lueurs cyan et violettes, boutons et cartes lumineux, barre latérale en dégradé.", "Jauge façon instrument de bord (radar pendant l'analyse) et courbes en direct du processeur, de la mémoire, du disque et de la température.", "Barre de commandes Ctrl+K : tape quelques lettres pour lancer n'importe quelle action ou ouvrir une page ; raccourcis Ctrl+1 à Ctrl+9.", "Notifications élégantes à la fin des actions et écran de démarrage animé."),
            N("2.13", "Menu simplifié en rubriques avec des onglets en haut.", "Pourcentage d'avancement dans la barre du bas pendant le nettoyage, et sur l'icône de la barre des tâches."),
            N("2.12", "Température de la carte graphique en direct sur l'Accueil, avec alerte en cas de surchauffe.", "Carte de l'espace disque : chaque dossier en rectangle proportionnel à sa taille.", "Audit des extensions des navigateurs et réglages de confidentialité (page Sécurité)."),
            N("2.11", "Onglet Jeux : détecte tes jeux Steam, Epic, Xbox, GOG, Ubisoft, Battle.net, EA, Riot et Rockstar.", "Booster FPS : réglages de Windows pour les jeux, tous réversibles.", "« Booster et lancer » : mode performances et RAM libérée avant le jeu, puis retour automatique au mode normal."),
            N("2.10", "Onglet Windows et pilotes : pilote de la carte graphique et mises à jour Windows en attente, avec un tri intelligent."),
            N("2.9", "Onglet Doublons : trouve les fichiers identiques (contenu vérifié par empreinte SHA-256)."),
            N("2.7", "Alerte quand un disque est presque plein ou en mauvaise santé.", "Onglet Sauvegarde : copie Documents, Images, Bureau... sur une clé USB en un clic.", "Mode jeu."),
            N("2.6", "Onglet Mises à jour, message de bienvenue après chaque mise à jour, page « À propos »."),
            N("2.4", "Mises à jour automatiques depuis GitHub, avec vérification d'intégrité (SHA256)."),
            N("2.2", "Vrai programme NettoyagePC.exe avec icône, installateur et désinstallation propre.")
        };
        private static KeyValuePair<string, string[]> N(string v, params string[] l) { return new KeyValuePair<string, string[]>(v, l); }
    }
}
