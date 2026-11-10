// Velox 5 - temps de démarrage du PC sur 30 jours : une barre par jour (du bouton marche jusqu'au bureau),
// la moyenne et l'évolution depuis les premières mesures. Mesuré sans droits administrateur (voir Noyau/TempsDemarrage).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class TempsDemarrage5 : StackPanel
    {
        /// <summary>(captures) relevés d'exemple : rien n'est lu.</summary>
        public static List<Tuple<DateTime, double>> Exemple;

        public static void Ouvrir(FenetrePrincipale f) { f.Ouvrir("Temps de démarrage", new TempsDemarrage5(f), 860); }

        private readonly FenetrePrincipale F;
        private readonly TextBlock bilan;
        private readonly Grid barres = new Grid { Height = 190 };

        private TempsDemarrage5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("Chaque jour, Velox note le temps que met ton PC entre le bouton marche et le bureau (connexion comprise). Il faut que Velox ou le gardien se lance avec Windows. Rien n'est envoyé nulle part."));
            bilan = new TextBlock { Text = "…", FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            Children.Add(bilan);
            Children.Add(Premium5.Carte(barres, Habillage.Alpha(P.Accent, 70)));
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var bProg = new BoutonNoyau("Programmes au démarrage", false, 0, 38) { TaillePolice = 13 };
            bProg.Clic += () => F.LancerAction("optim");
            boutons.Children.Add(bProg);
            Children.Add(boutons);
            Children.Add(Premium5.Intro("Pour démarrer plus vite : retire du démarrage les programmes dont tu n'as pas besoin tout de suite (le bilan d'optimisation les liste), et garde de la place libre sur le disque de Windows."));
            Task.Run(() => Exemple != null ? Tuple.Create(Exemple, "") : Tuple.Create(TempsDemarrage.ParJour(30), TempsDemarrage.Bilan()))
                .ContinueWith(t => Dispatcher.UIThread.Post(() => Montrer(t.Result.Item1, Exemple != null ? Langue.F("Ton PC met en moyenne {0} s pour arriver sur le bureau.", 24) + " " + Langue.F("C'est {0} s plus vite qu'au début des mesures, le {1}.", 11, "08/10/2026") : t.Result.Item2)));
        }

        private void Montrer(List<Tuple<DateTime, double>> jours, string texte)
        {
            var P = Habillage.P;
            bilan.Text = texte;
            barres.Children.Clear(); barres.ColumnDefinitions.Clear();
            if (jours.Count == 0)
            {
                barres.Children.Add(new TextBlock { Text = Langue.T("Les barres apparaîtront après le prochain démarrage du PC."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                return;
            }
            double max = Math.Max(30, jours.Max(x => x.Item2)) * 1.1, moy = jours.Average(x => x.Item2);
            for (int i = 0; i < jours.Count; i++)
            {
                barres.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                var j = jours[i];
                Color c = j.Item2 <= moy * 0.9 ? P.Vert : j.Item2 >= moy * 1.25 ? P.Orange : P.Accent;
                var colonne = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto"), Margin = new Thickness(2, 0) };
                var barre = new Border { Height = Math.Max(4, 140 * j.Item2 / max), VerticalAlignment = VerticalAlignment.Bottom, Background = Habillage.Pinceau(c, 210), CornerRadius = new CornerRadius(2, 2, 0, 0) };
                ToolTip.SetTip(barre, j.Item1.ToString("dddd dd/MM", Langue.Culture) + " : " + Math.Round(j.Item2) + " s");
                colonne.Children.Add(barre);
                if (jours.Count <= 12 || i % 3 == 0 || i == jours.Count - 1)
                {
                    var val = new TextBlock { Text = Math.Round(j.Item2) + "", FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(P.Texte), HorizontalAlignment = HorizontalAlignment.Center };
                    var date = new TextBlock { Text = j.Item1.ToString("dd/MM"), FontFamily = Habillage.Console, FontSize = 10, Foreground = Habillage.Pinceau(P.Discret), HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetRow(val, 1); Grid.SetRow(date, 2); colonne.Children.Add(val); colonne.Children.Add(date);
                }
                Grid.SetColumn(colonne, i); barres.Children.Add(colonne);
            }
        }
    }
}
