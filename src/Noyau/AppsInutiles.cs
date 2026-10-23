// Nettoyage PC - applications préinstallées inutiles (publicitaires ou rarement utilisées), toutes réinstallables
// depuis le Microsoft Store. Jamais : Store, Paramètres, Photos, Calculatrice, Bloc-notes, Xbox, Sécurité, Assistance rapide.
// Partagé par la version 4 et la version 5.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class AppInutile { public string Motif, Libelle, Description; public bool Conseille; }

    internal static class AppsInutiles
    {
        // Applications publicitaires ou rarement utilisées, réinstallables depuis le Microsoft Store.
        // (Jamais : Store, Paramètres, Photos, Calculatrice, Bloc-notes, Xbox, Sécurité, Assistance rapide, antivirus...)
        public static readonly AppInutile[] Connues =
        {
            A("Microsoft.BingNews", "Actualités (Microsoft Start)", "Fil d'actualités avec publicités.", true),
            A("Microsoft.BingWeather", "Météo", "Prévisions météo de Microsoft (garde-la si tu t'en sers).", false),
            A("Microsoft.BingSearch", "Recherche Bing", "Application de recherche Bing.", true),
            A("Microsoft.GetHelp", "Obtenir de l'aide", "Assistant d'aide de Microsoft.", true),
            A("Microsoft.Getstarted", "Astuces", "Conseils de prise en main de Windows.", true),
            A("Microsoft.MicrosoftSolitaireCollection", "Microsoft Solitaire Collection", "Jeux de cartes avec publicités (garde-la si tu y joues).", false),
            A("Microsoft.People", "Contacts", "Ancienne application Contacts.", true),
            A("Microsoft.WindowsFeedbackHub", "Hub de commentaires", "Pour envoyer des avis à Microsoft.", true),
            A("Microsoft.MixedReality.Portal", "Portail de réalité mixte", "Pour les casques de réalité virtuelle Windows (abandonnés).", true),
            A("Microsoft.SkypeApp", "Skype", "Service arrêté par Microsoft en 2025.", true),
            A("Microsoft.Microsoft3DViewer", "Visionneuse 3D", "Affiche des modèles 3D.", true),
            A("Microsoft.Print3D", "Print 3D", "Impression 3D.", true),
            A("Microsoft.MSPaint", "Paint 3D", "Ancienne version 3D de Paint (Paint classique n'est pas touché).", true),
            A("Microsoft.MicrosoftOfficeHub", "Microsoft 365 (Office)", "Lanceur et publicité pour l'abonnement Office (garde-le si tu l'utilises).", false),
            A("Clipchamp.Clipchamp", "Clipchamp", "Montage vidéo en ligne.", true),
            A("Microsoft.Todos", "Microsoft To Do", "Listes de tâches (garde-la si tu t'en sers).", false),
            A("Microsoft.WindowsMaps", "Cartes", "Cartes de Microsoft.", true),
            A("Microsoft.549981C3F5F10", "Cortana", "Ancien assistant vocal, arrêté par Microsoft.", true),
            A("Microsoft.PowerAutomateDesktop", "Power Automate", "Automatisation pour les entreprises.", true),
            A("Microsoft.ZuneVideo", "Films et TV", "Ancien lecteur vidéo (le Lecteur multimédia le remplace).", true),
            A("Microsoft.Windows.DevHome", "Dev Home", "Outil pour les développeurs.", true),
            A("Microsoft.Wallet", "Microsoft Pay", "Portefeuille de paiement abandonné.", true),
            A("Microsoft.Messaging", "Messages", "Ancienne application de SMS.", true),
            A("Microsoft.OneConnect", "Forfaits mobiles", "Achat de forfaits de données mobiles.", true),
            A("Microsoft.WindowsCommunicationsApps", "Courrier et Calendrier", "Applications arrêtées par Microsoft (remplacées par le nouvel Outlook).", false),
            A("MicrosoftTeams", "Microsoft Teams (personnel)", "Version gratuite de Teams (garde-la si tu t'en sers).", false),
            A("MSTeams", "Microsoft Teams", "Garde-le si tu t'en sers pour le travail.", false),
            A("Microsoft.OutlookForWindows", "Outlook (nouveau)", "Messagerie (garde-la si tu t'en sers).", false),
            A("king.com.*", "Jeux King (Candy Crush...)", "Jeux préinstallés avec achats intégrés.", true),
            A("Disney.*", "Disney+", "Application de vidéo par abonnement.", false),
            A("Facebook.*", "Facebook / Instagram", "Réseaux sociaux préinstallés.", false),
            A("BytedancePte.Ltd.TikTok", "TikTok", "Réseau social préinstallé.", false),
            A("SpotifyAB.SpotifyMusic", "Spotify", "Musique (garde-la si tu t'en sers).", false),
            A("AmazonVideo.PrimeVideo", "Prime Video", "Vidéo par abonnement.", false),
            A("4DF9E0F8.Netflix", "Netflix", "Vidéo par abonnement.", false),
            A("*.LinkedInforWindows", "LinkedIn", "Réseau professionnel préinstallé.", false),
            A("*.Twitter", "X (Twitter)", "Réseau social préinstallé.", false)
        };
        private static AppInutile A(string m, string l, string d, bool c) { return new AppInutile { Motif = m, Libelle = l, Description = d, Conseille = c }; }

        public static bool Correspond(string motif, string nom)
        {
            string re = "^" + Regex.Escape(motif).Replace("\\*", ".*") + "$";
            return Regex.IsMatch(nom, re, RegexOptions.IgnoreCase);
        }

        /// <summary>Applications connues installées sur ce compte (hors cadres et composants du système).</summary>
        public static List<Tuple<Paquet, AppInutile>> Trouver()
        {
            var l = new List<Tuple<Paquet, AppInutile>>();
            try
            {
                foreach (var p in Paquets.Lister(true))
                {
                    if (p.Cadre || p.Systeme) continue;
                    var a = Connues.FirstOrDefault(c => Correspond(c.Motif, p.Nom));
                    if (a != null) l.Add(Tuple.Create(p, a));
                }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("applications préinstallées", ex); }
            return l;
        }

        /// <summary>Retire l'application de ce compte et le note dans l'Historique (réinstallation depuis le Store). "" si réussi.</summary>
        public static string Retirer(Paquet p, string nom)
        {
            string r = Paquets.Retirer(p.NomComplet);
            if (r == "") Historique.Ajouter("appx", "Application retirée : " + nom, new Dictionary<string, object> { { "Nom", p.Nom }, { "Libelle", nom }, { "Paquet", p.NomComplet } });
            return r;
        }
    }
}
