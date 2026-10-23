// Nettoyage PC 5 - rubriques pas encore refaites : ce qu'elles contiendront (elles existent dans la version 4).
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    public static class PageAVenir
    {
        public static Control Nettoyer() { return Cartes(
            ("Nettoyage rapide", "Fichiers temporaires, caches de Windows, des navigateurs et des applications, corbeille."),
            ("Nettoyage profond", "En plus : restes de Windows Update, nettoyage de disque et DISM, avec un point de restauration avant."),
            ("Disques et gros fichiers", "Ce qui prend de la place, avec une carte de l'espace occupé."),
            ("Doublons", "Fichiers identiques repérés par empreinte SHA-256 : tu vérifies avant de supprimer.")); }

        public static Control Accelerer() { return Cartes(
            ("Programmes au démarrage", "Désactive ce qui ralentit le démarrage, réactivable à tout moment."),
            ("Jeux", "Steam, Epic, Xbox, GOG, Ubisoft, Battle.net, EA, Riot, Rockstar, avec des réglages réversibles."),
            ("Pilotes", "Carte graphique et mises à jour de Windows : tu choisis ce qui s'installe."),
            ("Applications inutiles", "Applications préinstallées et anciens pilotes oubliés par Windows.")); }

        public static Control Proteger() { return Cartes(
            ("Sécurité", "Antivirus, pare-feu, réparation de Windows et points de restauration."),
            ("Bouclier anti-arnaque", "Repère les logiciels de prise en main à distance et explique quoi faire."),
            ("Sauvegarde", "Documents et photos copiés sur une clé USB, en un clic."),
            ("Confidentialité", "Extensions des navigateurs et réglages de Windows.")); }

        public static Control Outils() { return Cartes(
            ("Matériel", "Mémoire vive bridée (profil EXPO / XMP), santé détaillée des disques."),
            ("Plein potentiel", "Écran sous sa fréquence, mauvais câble, liaison de la carte graphique."),
            ("Réseau", "Ping, débit et comparaison des serveurs DNS."),
            ("Aide à distance", "L'Assistance rapide de Windows, pour qu'un proche t'aide.")); }

        public static Control Premium() { return Cartes(
            ("Pourquoi mon PC ralentit ?", "L'enquêteur retrace ce qui a changé et désigne le coupable."),
            ("Santé prédictive", "Les signes d'une panne, repérés avant qu'elle arrive."),
            ("Gardien en temps réel", "Alertes discrètes et nettoyage léger automatique."),
            ("Coût en électricité", "Combien le PC coûte par jour, par mois et par an.")); }

        public static Control Simple(string titre, string texte) { return Cartes((titre, texte)); }

        private static Control Cartes(params (string Titre, string Texte)[] cartes)
        {
            var P = Habillage.P;
            var s = new StackPanel { Margin = new Thickness(28, 18, 28, 28), Spacing = 16 };
            var note = new Border
            {
                BorderBrush = Habillage.Pinceau(P.Accent, 120), BorderThickness = new Thickness(0, 0, 0, 0), Background = Habillage.Pinceau(P.Accent, P.Sombre ? 18 : 14),
                CornerRadius = new CornerRadius(3), Padding = new Thickness(16, 12),
                Child = new TextBlock
                {
                    Text = "Cette rubrique arrive bientôt dans la nouvelle interface. Tout ce qu'elle contient existe déjà dans la version 4.",
                    FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap,
                },
            };
            s.Children.Add(note);
            var grille = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var c in cartes)
            {
                var carte = new Border
                {
                    Width = 300, Margin = new Thickness(0, 0, 16, 16), Padding = new Thickness(20, 18), CornerRadius = new CornerRadius(3),
                    Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1),
                };
                var t = new StackPanel { Spacing = 8 };
                t.Children.Add(new TextBlock { Text = c.Titre, FontFamily = Habillage.Affichage, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
                t.Children.Add(new TextBlock { Text = c.Texte, FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                carte.Child = t;
                grille.Children.Add(carte);
            }
            s.Children.Add(grille);
            return s;
        }
    }
}
