// Nettoyage PC - réglages de Windows proposés (confidentialité, jeux) : état actuel et application suivie dans
// l'Historique (réversible). Rien côté sécurité : Windows Update, l'antivirus et le pare-feu ne sont jamais touchés.
// Partagé par la version 4 et la version 5.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal class ReglageJeu
    {
        public string Cle, Libelle, Gain;
        public Func<bool> Etat;
        public Func<bool> Appliquer;
    }

    internal static class ReglagesWindows
    {
        private const string Cdm = @"HKCU\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager";
        private static readonly string[] Suggestions = { "SubscribedContent-338388Enabled", "SubscribedContent-338389Enabled", "SubscribedContent-353694Enabled", "SubscribedContent-353696Enabled", "SystemPaneSuggestionsEnabled", "SoftLandingEnabled" };

        private static int? Dw(string cle, string nom) { var v = Registre.Lire(cle, nom); try { return v == null ? (int?)null : Convert.ToInt32(v); } catch { return null; } }
        private const string CleGpuPref = @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences";

        /// <summary>Réglages de ton compte Windows qui limitent la publicité et le pistage.</summary>
        public static List<ReglageJeu> Confidentialite()
        {
            var l = new List<ReglageJeu>
            {
                new ReglageJeu { Cle = "Pub", Libelle = "Pas d'identifiant de publicité", Gain = "Les applications ne peuvent plus te reconnaître pour te montrer des publicités ciblées.",
                    Etat = () => Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled") == 0,
                    Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo", "Enabled", 0, RegistryValueKind.DWord, "Confidentialité : identifiant de publicité désactivé") },
                new ReglageJeu { Cle = "Suggestions", Libelle = "Pas de suggestions ni de publicités dans Windows", Gain = "Plus d'applications « suggérées » dans le menu Démarrer, de conseils et d'offres dans Paramètres et sur l'écran de verrouillage.",
                    Etat = () => Suggestions.All(v => Dw(Cdm, v) == 0),
                    Appliquer = () => { bool r = false; foreach (var v in Suggestions) r |= Registre.EcrireSuivi(Cdm, v, 0, RegistryValueKind.DWord, "Confidentialité : suggestions de Windows (" + v + ")"); return r; } },
                new ReglageJeu { Cle = "Experiences", Libelle = "Pas d'expériences personnalisées", Gain = "Microsoft n'utilise plus tes données de diagnostic pour personnaliser les conseils et les publicités.",
                    Etat = () => Dw(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled") == 0,
                    Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Privacy", "TailoredExperiencesWithDiagnosticDataEnabled", 0, RegistryValueKind.DWord, "Confidentialité : expériences personnalisées désactivées") },
                new ReglageJeu { Cle = "Langues", Libelle = "Les sites web ne voient pas ta liste de langues", Gain = "Les sites ne peuvent plus lire la liste des langues installées sur ton PC (utilisée pour te pister).",
                    Etat = () => Dw(@"HKCU\Control Panel\International\User Profile", "HttpAcceptLanguageOptOut") == 1,
                    Appliquer = () => Registre.EcrireSuivi(@"HKCU\Control Panel\International\User Profile", "HttpAcceptLanguageOptOut", 1, RegistryValueKind.DWord, "Confidentialité : liste des langues masquée aux sites web") },
                new ReglageJeu { Cle = "Bing", Libelle = "Recherche du menu Démarrer sans résultats Bing", Gain = "La recherche du menu Démarrer ne cherche plus sur Internet : plus rapide, et tes recherches restent sur ton PC. Effet à la prochaine ouverture de session.",
                    Etat = () => Dw(@"HKCU\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions") == 1,
                    Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, RegistryValueKind.DWord, "Confidentialité : recherche Bing du menu Démarrer désactivée") }
            };
            return l;
        }

        /// <summary>Réglages de Windows pour les jeux (Booster FPS) : état actuel et application suivie dans l'Historique.</summary>
        public static List<ReglageJeu> Jeux()
        {
            var reglages = new List<ReglageJeu>();
            reglages.Add(new ReglageJeu
            {
                Cle = "ModeJeuWin", Libelle = "Mode Jeu de Windows", Gain = "Windows donne la priorité au jeu (processeur, carte graphique) et repousse les mises à jour pendant la partie.",
                Etat = () => { var v = Registre.Lire(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled"); return v == null || Convert.ToInt32(v) != 0; },
                Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Microsoft\GameBar", "AutoGameModeEnabled", 1, RegistryValueKind.DWord, "Jeux : Mode Jeu de Windows activé")
            });
            reglages.Add(new ReglageJeu
            {
                Cle = "CaptureFond", Libelle = "Pas d'enregistrement vidéo en arrière-plan", Gain = "La Xbox Game Bar n'enregistre plus en continu les 30 dernières secondes : quelques FPS gagnés. Les captures manuelles (Win+Alt+R) marchent toujours.",
                Etat = () => { var v = Registre.Lire(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled"); return v == null || Convert.ToInt32(v) == 0; },
                Appliquer = () => Registre.EcrireSuivi(@"HKCU\Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", 0, RegistryValueKind.DWord, "Jeux : enregistrement vidéo en arrière-plan désactivé")
            });
            if (Natif.Build >= 19041 || !Natif.EstWindows)
                reglages.Add(new ReglageJeu
                {
                    Cle = "Hags", Libelle = "Planification GPU accélérée par le matériel", Gain = "La carte graphique gère elle-même sa mémoire : moins de latence et souvent plus de FPS. Prend effet après un redémarrage.",
                    Etat = () => { var v = Registre.Lire(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode"); return v != null && Convert.ToInt32(v) == 2; },
                    Appliquer = () => Registre.EcrireSuivi(@"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, RegistryValueKind.DWord, "Jeux : planification GPU accélérée par le matériel (après redémarrage)")
                });
            if (Natif.Build >= 22000 || !Natif.EstWindows)
                reglages.Add(new ReglageJeu
                {
                    Cle = "Fenetre", Libelle = "Optimisations pour les jeux en mode fenêtré", Gain = "Réglage de Windows 11 : moins de latence et meilleures performances pour les jeux en fenêtre ou en plein écran fenêtré.",
                    Etat = () => Registre.Texte(CleGpuPref, "DirectXUserGlobalSettings").Contains("SwapEffectUpgradeEnable=1"),
                    Appliquer = () =>
                    {
                        string actuel = Registre.Texte(CleGpuPref, "DirectXUserGlobalSettings");
                        string nouveau = Regex.Replace(actuel, @"SwapEffectUpgradeEnable=\d;?", "").Trim() + "SwapEffectUpgradeEnable=1;";
                        return Registre.EcrireSuivi(CleGpuPref, "DirectXUserGlobalSettings", nouveau, RegistryValueKind.String, "Jeux : optimisations pour les jeux en mode fenêtré");
                    }
                });
            return reglages;
        }
    }
}
