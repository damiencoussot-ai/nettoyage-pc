// Nettoyage PC - conseil pour chaque programme lancé au démarrage : impact estimé et ce qu'il vaut mieux en faire.
// Sans interface (partagé par la version 4 et la version 5). Rien n'est modifié ici : le conseil aide seulement à choisir.
// L'impact est une estimation d'après le type de programme (lanceur de jeux, messagerie, mise à jour...) : Windows ne
// donne pas la mesure exacte sans analyse du démarrage (outil Premium « Analyse du démarrage »).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    /// <summary>Que faire d'un programme au démarrage.</summary>
    internal enum ChoixDemarrage { Garder = 0, AuChoix = 1, PeutAttendre = 2 }

    internal class ConseilDemarrage
    {
        /// <summary>0 faible, 1 moyen, 2 élevé.</summary>
        public int Impact;
        public ChoixDemarrage Choix;
        public string Conseil = "";

        public string TexteImpact { get { return Impact == 2 ? "Élevé" : Impact == 1 ? "Moyen" : "Faible"; } }
        public string TexteChoix { get { return Choix == ChoixDemarrage.PeutAttendre ? "Peut attendre" : Choix == ChoixDemarrage.Garder ? "À garder" : "Selon ton usage"; } }
    }

    internal static class ConseilsDemarrage
    {
        private class Regle
        {
            public Regex Motif; public int Impact; public ChoixDemarrage Choix; public string Conseil;
        }

        private static Regle R(string motif, int impact, ChoixDemarrage choix, string conseil)
        {
            return new Regle { Motif = new Regex(motif, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant), Impact = impact, Choix = choix, Conseil = conseil };
        }

        // Premier motif qui correspond (au nom affiché ou à la commande) : l'ordre compte.
        private static readonly List<Regle> Regles = new List<Regle>
        {
            // --- à garder : protection, pilotes, matériel
            R(@"SecurityHealth|Windows ?Defender|MsMpEng|avast|\bavg\b|kaspersky|\bavp\b|bitdefender|\beset\b|egui|norton|mcafee|malwarebytes|\bmbam|sophos|trend ?micro|f-secure|panda|webroot|g ?data",
              0, ChoixDemarrage.Garder, "Protection du PC : à garder."),
            R(@"Realtek|RtkAud|RTHDVCPL|Nahimic|Waves ?MaxxAudio|Dolby|Sonic ?Studio|SoundBlaster|Creative(?! ?Cloud)",
              0, ChoixDemarrage.Garder, "Pilote du son : à garder (sinon certains réglages audio peuvent disparaître)."),
            R(@"Synaptics|ETDCtrl|\bELAN\b|SynTP|Precision ?Touch|Touchpad|Pen ?Service|Wacom",
              0, ChoixDemarrage.Garder, "Pavé tactile ou stylet : à garder."),
            R(@"Bluetooth|Intel.*(Graphics|Command|Rapid|Optane)|IGCC|igfx|hkcmd|Thunderbolt|Lenovo ?Vantage|\bDell\b|\bHP\b.*(Hotkey|Support)|ASUS|MSI ?Center|Armoury|Dragon ?Center|Alienware|Fn ?Key|HotKey",
              0, ChoixDemarrage.Garder, "Outil du fabricant (touches spéciales, batterie, matériel) : mieux vaut le garder."),
            R(@"Bitwarden|1Password|KeePass|Dashlane|LastPass|NordPass|Proton ?Pass",
              0, ChoixDemarrage.Garder, "Gestionnaire de mots de passe : à garder si tu t'en sers tous les jours."),
            R(@"ctfmon|\bIME\b|TextInput|Clavier|Keyboard ?Layout",
              0, ChoixDemarrage.Garder, "Saisie au clavier de Windows : à garder."),

            // --- lanceurs de jeux : impact élevé, ils se lancent tout seuls quand on ouvre un jeu
            R(@"\bSteam\b|steamwebhelper", 2, ChoixDemarrage.PeutAttendre, "Lanceur de jeux : il s'ouvre tout seul quand tu lances un jeu. Le désactiver accélère le démarrage."),
            R(@"Epic ?Games|EpicGamesLauncher", 2, ChoixDemarrage.PeutAttendre, "Lanceur de jeux : il s'ouvre tout seul quand tu lances un jeu. Le désactiver accélère le démarrage."),
            R(@"EA ?Desktop|\bEA ?app\b|\bOrigin\b|Ubisoft|uplay|Battle\.?net|Riot ?Client|GOG ?Galaxy|Rockstar|Xbox ?App|Amazon ?Games|\bitch\b",
              2, ChoixDemarrage.PeutAttendre, "Lanceur de jeux : il s'ouvre tout seul quand tu lances un jeu. Le désactiver accélère le démarrage."),
            R(@"Wallpaper ?Engine|Lively|Rainmeter",
              1, ChoixDemarrage.AuChoix, "Fond d'écran animé ou gadgets : agréable, mais il occupe le processeur et la carte graphique en permanence."),

            // --- messageries, musique, visio : se lancent quand on en a besoin
            R(@"Discord", 2, ChoixDemarrage.PeutAttendre, "Messagerie : tu peux l'ouvrir toi-même quand tu en as besoin (tu recevras tes messages à ce moment-là)."),
            R(@"Spotify|Deezer|iTunes|Apple ?Music|Tidal|Amazon ?Music", 1, ChoixDemarrage.PeutAttendre, "Musique : tu peux l'ouvrir toi-même quand tu veux écouter."),
            R(@"Teams|Skype|Zoom|Slack|WhatsApp|Telegram|Signal|Messenger|Viber|Webex",
              1, ChoixDemarrage.AuChoix, "Messagerie ou visio : à garder si tu dois recevoir des appels dès l'allumage, sinon il peut attendre."),

            // --- navigateurs et logiciels lourds qui se préchargent
            R(@"MicrosoftEdgeAutoLaunch|msedge.*--no-startup-window|Edge.*AutoLaunch", 1, ChoixDemarrage.PeutAttendre, "Préchargement de Microsoft Edge : inutile, Edge s'ouvre aussi vite sans."),
            R(@"GoogleChromeAutoLaunch|chrome.*--no-startup-window|Opera.*(Assistant|AutoLaunch)|Brave.*AutoLaunch|BrowserAssistant",
              1, ChoixDemarrage.PeutAttendre, "Préchargement du navigateur : inutile, il s'ouvre aussi vite sans."),
            R(@"Adobe ?Creative ?Cloud|CCXProcess|Creative ?Cloud|Adobe ?Desktop", 2, ChoixDemarrage.PeutAttendre, "Adobe Creative Cloud : lourd au démarrage, il s'ouvre tout seul quand tu lances un logiciel Adobe."),
            R(@"Acrobat|AdobeARM|Adobe ?Update", 0, ChoixDemarrage.AuChoix, "Mises à jour d'Adobe : léger. Le garder évite d'oublier les correctifs de sécurité."),
            R(@"Cortana|Copilot", 1, ChoixDemarrage.PeutAttendre, "Assistant de Windows : il peut attendre que tu l'ouvres."),

            // --- synchronisation : à garder si on s'en sert
            R(@"OneDrive", 1, ChoixDemarrage.AuChoix, "Synchronisation de tes fichiers : à garder si tu utilises OneDrive (sinon tes fichiers ne se sauvegardent plus)."),
            R(@"Dropbox|Google ?Drive|GoogleDriveFS|iCloud|pCloud|\bMEGA(sync)?\b|Nextcloud|\bSync",
              1, ChoixDemarrage.AuChoix, "Synchronisation de fichiers : à garder si tu t'en sers, sinon tes fichiers ne se sauvegardent plus."),
            R(@"VPN|NordVPN|Surfshark|ExpressVPN|ProtonVPN|CyberGhost|YouVPN|WireGuard|OpenVPN",
              1, ChoixDemarrage.AuChoix, "VPN : à garder seulement si tu veux être protégé dès l'allumage."),

            // --- mises à jour et petits utilitaires
            R(@"Stream ?Deck|\bLogi|Logitech|G ?HUB|Razer|Synapse|Corsair|iCUE|SteelSeries|HyperX|NGenuity|Elgato",
              1, ChoixDemarrage.AuChoix, "Logiciel de souris, clavier ou accessoire : à garder si tu utilises ses réglages (touches, lumières), sinon il peut attendre."),
            R(@"AMD ?Software|RadeonSoftware|Radeon|AMD.*(Noise|Link)|NVIDIA|GeForce|NvBackend|ShadowPlay",
              1, ChoixDemarrage.AuChoix, "Logiciel de la carte graphique : le pilote marche sans lui. À garder pour l'enregistrement de jeu, l'overlay ou les réglages rapides."),
            R(@"CCleaner|Glary|Wise ?Care|Advanced ?SystemCare|Driver ?Booster|Driver ?Easy|PC ?Optimizer|Pilote ?Gratuit|Updater de pilote",
              1, ChoixDemarrage.PeutAttendre, "Logiciel de « nettoyage » ou de mise à jour de pilotes : inutile au démarrage, souvent source de publicités."),
            R(@"uTorrent|BitTorrent|qBittorrent|Vuze|Deluge", 1, ChoixDemarrage.PeutAttendre, "Logiciel de téléchargement : il utilise ta connexion dès l'allumage. Ouvre-le seulement quand tu en as besoin."),
            R(@"\bOBS\b|Streamlabs|Medal|Outplayed|Overwolf", 1, ChoixDemarrage.PeutAttendre, "Enregistrement ou overlay de jeu : il peut attendre que tu l'ouvres."),
            R(@"Ollama", 1, ChoixDemarrage.AuChoix, "Moteur d'IA locale : à garder si tu utilises l'assistant local tous les jours, sinon il peut attendre."),
            R(@"Update|Updater|Helper|Notifier|Tray|Agent|Launcher|Assistant",
              0, ChoixDemarrage.AuChoix, "Petit utilitaire (mise à jour, icône près de l'horloge) : faible impact."),
        };

        /// <summary>Conseil pour un programme (nom affiché et commande). Jamais null.</summary>
        public static ConseilDemarrage Pour(string nom, string commande)
        {
            string texte = (nom ?? "") + " " + (commande ?? "");
            var r = Regles.FirstOrDefault(x => x.Motif.IsMatch(texte));
            if (r != null) return new ConseilDemarrage { Impact = r.Impact, Choix = r.Choix, Conseil = r.Conseil };
            return new ConseilDemarrage { Impact = 1, Choix = ChoixDemarrage.AuChoix, Conseil = "Programme inconnu de Nettoyage PC : si tu ne sais pas à quoi il sert, cherche son nom avant de le désactiver." };
        }

        /// <summary>Programmes actifs qui peuvent attendre (lanceurs, messageries, préchargements).</summary>
        public static List<ElementDemarrage> PeuventAttendre(IEnumerable<ElementDemarrage> elements)
        {
            return elements.Where(e => e.Actif && Pour(e.Nom, e.Commande).Choix == ChoixDemarrage.PeutAttendre).ToList();
        }

        /// <summary>Résumé pour la page : « 4 programmes peuvent attendre... ».</summary>
        public static string Resume(IList<ElementDemarrage> elements)
        {
            int actifs = elements.Count(x => x.Actif);
            var attendre = PeuventAttendre(elements);
            if (actifs == 0) return "Aucun programme ne se lance avec Windows.";
            if (attendre.Count == 0) return Langue.F("{0} programme(s) se lancent avec Windows. Aucun ne semble inutile au démarrage.", actifs);
            int eleves = attendre.Count(e => Pour(e.Nom, e.Commande).Impact == 2);
            return eleves > 0
                ? Langue.F("{0} programme(s) se lancent avec Windows, dont {1} qui peuvent attendre que tu les ouvres ({2} à fort impact). Les désactiver accélère le démarrage, sans rien désinstaller.", actifs, attendre.Count, eleves)
                : Langue.F("{0} programme(s) se lancent avec Windows, dont {1} qui peuvent attendre que tu les ouvres. Les désactiver accélère le démarrage, sans rien désinstaller.", actifs, attendre.Count);
        }
    }
}
