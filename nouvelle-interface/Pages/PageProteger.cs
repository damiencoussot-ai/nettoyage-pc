// Nettoyage PC 5 - rubrique Protéger : sécurité (bilan, antivirus, point de restauration, bouclier anti-arnaque...)
// et sauvegarde. Rien n'est désactivé : ni l'antivirus, ni le pare-feu, ni Windows Update.
using System;
using System.IO;
using Avalonia.Controls;

namespace NettoyagePC.Nouveau
{
    internal static class PageProteger
    {
        public static Control Creer(FenetrePrincipale f)
        {
            return new Onglets(f, "Proteger", new[]
            {
                Tuple.Create<string, string, Func<Control>>("Securite", "Sécurité", () => Securite(f)),
                Tuple.Create<string, string, Func<Control>>("Sauvegarde", "Sauvegarde", () => Sauvegarde(f)),
            });
        }

        private static Control Securite(FenetrePrincipale f)
        {
            return Cartes.Page(f, null, new[]
            {
                CarteAction.A("Bilan de sécurité", "Antivirus, pare-feu, contrôle de compte, Secure Boot, BitLocker, SMB1, compte Invité, Windows Update. Sans rien modifier.", "Lancer le bilan", "secu", true),
                CarteAction.A("Analyse antivirus rapide", "Met à jour Microsoft Defender puis analyse les emplacements où se cachent le plus souvent les menaces.", "Analyser", "antivirus"),
                CarteAction.Code("Bouclier anti-arnaque", "Faux technicien au téléphone ? Repère les logiciels qui permettent de prendre la main sur ton PC (AnyDesk, TeamViewer...) et dit quoi faire.", "Vérifier", () => Bouclier5.Ouvrir(f)),
                CarteAction.A("Créer un point de restauration", "Une photo de Windows pour revenir en arrière en cas de problème après une installation ou une modification.", "Créer", "point"),
                CarteAction.A("Réparer Windows", "Répare l'image de Windows (DISM) et les fichiers système (SFC), avec un point de restauration avant. 20 à 50 min.", "Réparer", "reparer").Plus(),
                CarteAction.Code("Extensions des navigateurs", "Liste les extensions de Chrome, Edge, Brave, Vivaldi et Firefox, et signale celles à vérifier (hors boutique, accès à tous les sites).", "Vérifier", () => Extensions5.Ouvrir(f)).Plus(),
                CarteAction.Code("Confidentialité", "Moins de publicité et de pistage : identifiant publicitaire, suggestions de Windows, recherche Bing du menu Démarrer. Réversible.", "Régler", () => ReglagesWindows5.Confidentialite(f)).Plus(),
                CarteAction.Code("Restaurer le système", "Revenir à un point de restauration précédent si quelque chose ne fonctionne plus (outil de Windows).", "Ouvrir", () => Programmes.Ouvrir(Path.Combine(Chemins.Systeme32, "rstrui.exe"))).Plus(),
                CarteAction.Code("Sécurité Windows", "Ouvre le centre de sécurité de Windows : antivirus, pare-feu, protection des applications et du navigateur.", "Ouvrir", () => Programmes.Ouvrir("windowsdefender:")).Plus(),
            }, "Securite");
        }

        private static Control Sauvegarde(FenetrePrincipale f) { return new Sauvegarde5(f); }
    }
}
