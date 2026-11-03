// Nettoyage PC 5 - « Ce que tu as gagné » : après un nettoyage réel (rapide ou complet), l'espace libéré, la note du PC
// avant / après, le total depuis le début, et la suite possible (programmes au démarrage qui peuvent attendre).
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal static class Gains5
    {
        /// <summary>(captures) note avant / après et gains d'exemple : rien n'est lu sur le PC.</summary>
        public static GainsNettoyage Exemple;

        /// <summary>(captures) comparaisons d'exemple.</summary>
        private static System.Collections.Generic.List<ComparaisonNettoyage> ExempleComparaisons()
        {
            return new System.Collections.Generic.List<ComparaisonNettoyage>
            {
                new ComparaisonNettoyage { Date = DateTime.Now, Type = "profond", Avant = 72, Apres = 86, Octets = (long)(2.7 * 1073741824.0) },
                new ComparaisonNettoyage { Date = DateTime.Now.AddDays(-7), Type = "rapide", Avant = 78, Apres = 84, Octets = (long)(0.9 * 1073741824.0) },
                new ComparaisonNettoyage { Date = DateTime.Now.AddDays(-15), Type = "rapide", Avant = 70, Apres = 79, Octets = (long)(1.4 * 1073741824.0) },
            };
        }

        /// <summary>« Avant / après » : les derniers nettoyages, indice avant → après, gain et espace libéré.</summary>
        public static Control Comparaisons(System.Collections.Generic.List<ComparaisonNettoyage> l)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 6 };
            s.Children.Add(new TextBlock { Text = (Habillage.Hud ? "▸ " : "") + Langue.T("Avant / après : tes derniers nettoyages"), FontFamily = Habillage.Hud ? Habillage.Console : Habillage.Affichage, FontSize = Habillage.Hud ? 11.5 : 15, FontWeight = Habillage.Hud ? FontWeight.Normal : FontWeight.SemiBold, Foreground = Habillage.Pinceau(Habillage.Hud ? P.Accent : P.Texte) });
            if (l.Count == 0) { s.Children.Add(new TextBlock { Text = Langue.T("Le premier apparaît ici après ce nettoyage."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret) }); return s; }
            foreach (var c in l.Take(6))
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("110,90,*,Auto"), Height = 30 };
                Action<string, int, Color> col = (t, i, couleur) => { var x = new TextBlock { Text = t, FontFamily = i == 2 ? Habillage.Corps : Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(couleur), VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(x, i); g.Children.Add(x); };
                col(c.Date.ToString("dd/MM/yyyy"), 0, P.Discret);
                col(Langue.T(c.Type == "profond" ? "Profond" : "Rapide"), 1, P.Texte);
                col(c.Avant >= 0 && c.Apres >= 0 ? Langue.F("Indice {0} → {1}", c.Avant, c.Apres) + (c.Gain != 0 ? "  (" + (c.Gain > 0 ? "+" : "") + c.Gain + ")" : "") : Langue.T("Indice non mesuré"), 2, c.Gain > 0 ? P.Vert : P.Texte);
                col(Format.Taille(c.Octets), 3, P.Accent);
                s.Children.Add(new Border { Child = g, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(0, 0, 0, 1) });
            }
            return s;
        }

        /// <summary>Montre le résumé. noteAvant / noteApres : -1 si inconnue ; attendre : programmes au démarrage qui peuvent attendre.</summary>
        public static void Montrer(FenetrePrincipale f, GainsNettoyage g, int noteAvant, int noteApres, int attendre)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 14 };
            bool rien = g.DernierOctets < 1048576;
            s.Children.Add(new TextBlock
            {
                Text = Langue.T(rien ? "Ton PC était déjà propre : presque rien à supprimer cette fois." : "Nettoyage terminé. Voici ce que ton PC y a gagné :"),
                FontFamily = Habillage.Corps, FontSize = 14.5, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap
            });
            var tuiles = new Avalonia.Controls.Primitives.UniformGrid { Columns = 3 };
            var t1 = new TuileChiffre5("espace libéré");
            t1.Definir(Format.Taille(g.DernierOctets), rien ? 0 : 3);
            tuiles.Children.Add(t1);
            var t2 = new TuileChiffre5("note du PC, avant → après");
            if (noteAvant >= 0 && noteApres >= 0) t2.Definir(noteAvant + " → " + noteApres, noteApres > noteAvant ? 3 : 0);
            else t2.Definir(noteApres >= 0 ? noteApres.ToString() : "—", 0);
            tuiles.Children.Add(t2);
            var t3 = new TuileChiffre5(Langue.F("libérés depuis le début ({0} nettoyage(s))", g.Nombre));
            t3.Definir(Format.Taille(g.Total), 0);
            tuiles.Children.Add(t3);
            s.Children.Add(tuiles);
            s.Children.Add(new TextBlock
            {
                Text = Langue.T("Tes documents, photos et mots de passe n'ont pas été touchés. Le détail du nettoyage est dans l'Historique."),
                FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap
            });
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            FenetreInterieure fen = null;
            if (attendre > 0)
            {
                var suite = new Border
                {
                    Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1),
                    Child = new TextBlock
                    {
                        Text = Langue.F("Pour aller plus loin : {0} programme(s) au démarrage peuvent attendre que tu les ouvres. Les désactiver accélère le démarrage de Windows.", attendre),
                        FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap
                    }
                };
                s.Children.Add(suite);
                var bDem = new BoutonNoyau("Voir le démarrage", true, 200, 36) { TaillePolice = 14 };
                bDem.Clic += () => { if (fen != null) fen.Fermer(); f.SousPage("Accelerer", "Demarrage"); f.Afficher("Accelerer", true); };
                boutons.Children.Add(bDem);
            }
            s.Children.Add(Comparaisons(Exemple != null ? ExempleComparaisons() : AvantApres.Liste()));
            var bHisto = new BoutonNoyau("Voir l'historique", attendre == 0, 180, 36) { TaillePolice = 14 };
            bHisto.Clic += () => { if (fen != null) fen.Fermer(); f.Afficher("Historique", true); };
            boutons.Children.Add(bHisto);
            s.Children.Add(boutons);
            fen = f.Ouvrir("Ce que tu as gagné", s, 760);
        }
    }
}
