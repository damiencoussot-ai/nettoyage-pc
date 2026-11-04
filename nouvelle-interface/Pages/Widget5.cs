// Nettoyage PC 5 - widget de bureau (Premium) : petite fenêtre HUD toujours visible, en bas à droite de l'écran,
// avec l'indice du PC, le processeur, la mémoire vive, la température et un bouton « Nettoyer » (qui pose la question
// habituelle dans la fenêtre principale : rien n'est lancé sans ton accord). Se déplace à la souris, se ferme d'un clic.
using System;
using System.Globalization;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class Widget5 : StackPanel
    {
        private static Window fenetre;
        private static bool abonne;
        public static bool Ouvert { get { return fenetre != null; } }

        private readonly FenetrePrincipale F;
        private readonly TextBlock tIndice, tCpu, tRam, tTemp;
        private readonly DispatcherTimer minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        private bool enCours;
        private int n;
        public event Action Fermer;

        /// <summary>Ouvre le widget, ou le ferme s'il est déjà ouvert.</summary>
        public static void Basculer(FenetrePrincipale f)
        {
            if (fenetre != null) { fenetre.Close(); return; }
            var contenu = new Widget5(f, true);
            var w = new Window
            {
                Width = 300, SizeToContent = SizeToContent.Height, SystemDecorations = SystemDecorations.None, Topmost = true, CanResize = false, ShowInTaskbar = false,
                Background = Brushes.Transparent, TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }, Title = "Nettoyage PC",
                Content = contenu,
            };
            contenu.Fermer += () => w.Close();
            contenu.PointerPressed += (s, e) => { if (e.GetCurrentPoint(w).Properties.IsLeftButtonPressed) { try { w.BeginMoveDrag(e); } catch { } } };
            w.Opened += (s, e) =>
            {
                try
                {
                    var ecran = w.Screens.Primary;
                    if (ecran != null)
                    {
                        var z = ecran.WorkingArea; double k = ecran.Scaling;
                        w.Position = new PixelPoint(z.Right - (int)((300 + 24) * k), z.Bottom - (int)((w.Bounds.Height + 24) * k));
                    }
                }
                catch { }
            };
            w.Closed += (s, e) => { fenetre = null; contenu.minuteur.Stop(); f.Statut("Widget de bureau fermé."); };
            if (!abonne) { abonne = true; f.Closed += (s, e) => { if (fenetre != null) fenetre.Close(); }; }
            fenetre = w;
            w.Show();
            f.Statut("Widget de bureau ouvert en bas à droite de l'écran : fais-le glisser pour le déplacer.");
        }

        /// <summary>(captures) le widget dans une fenêtre intérieure, avec des valeurs d'exemple.</summary>
        public static void Apercu(FenetrePrincipale f) { f.Ouvrir("Widget de bureau", new Widget5(f, false) { HorizontalAlignment = HorizontalAlignment.Left, Width = 300 }, 420); }

        private Widget5(FenetrePrincipale f, bool vivant)
        {
            F = f;
            var P = Habillage.P;
            var cadre = new Cadre5 { Coupe = 16, Padding = new Thickness(16, 12, 14, 14), Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(5, 12, 19) : Colors.White, 245), BorderBrush = Habillage.Pinceau(P.Accent, 120), BorderThickness = new Thickness(1) };
            var s = new StackPanel { Spacing = 8 };
            var tete = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            tete.Children.Add(new TextBlock { Text = "NETTOYAGE PC // " + Langue.T("EN DIRECT"), FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1, Foreground = Habillage.Pinceau(P.Accent), VerticalAlignment = VerticalAlignment.Center });
            var fermer = new Button { Content = "✕", Padding = new Thickness(6, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), Foreground = Habillage.Pinceau(P.Discret), FontSize = 12, Cursor = new Cursor(StandardCursorType.Hand) };
            ToolTip.SetTip(fermer, Langue.T("Fermer le widget"));
            fermer.Click += (o, e) => { var x = Fermer; if (x != null) x(); };
            Grid.SetColumn(fermer, 1); tete.Children.Add(fermer);
            s.Children.Add(tete);
            var milieu = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var gauche = new StackPanel();
            gauche.Children.Add(new TextBlock { Text = Langue.T("INDICE"), FontFamily = Habillage.Console, FontSize = 10, Foreground = Habillage.Pinceau(P.Discret) });
            tIndice = new TextBlock { Text = F.Bilan != null ? F.Bilan.Score.ToString(CultureInfo.InvariantCulture) : vivant ? "…" : "72", FontFamily = Habillage.Affichage, FontSize = 44, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Texte), Margin = new Thickness(0, -4, 0, -4) };
            gauche.Children.Add(tIndice);
            milieu.Children.Add(gauche);
            var droite = new StackPanel { Spacing = 3, Margin = new Thickness(18, 4, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            tCpu = Ligne(droite, "CPU", vivant ? "…" : "9 %");
            tRam = Ligne(droite, "RAM", vivant ? "…" : "41 %");
            tTemp = Ligne(droite, "TEMP", vivant ? "…" : "46 °C");
            Grid.SetColumn(droite, 1); milieu.Children.Add(droite);
            s.Children.Add(milieu);
            var nettoyer = new BoutonNoyau("Nettoyer", true, 0, 34) { TaillePolice = 13, HorizontalAlignment = HorizontalAlignment.Stretch };
            nettoyer.Clic += () =>
            {
                // la question habituelle s'affiche dans la fenêtre principale
                try { F.WindowState = WindowState.Normal; F.Activate(); } catch { }
                F.LancerAction("standard");
            };
            s.Children.Add(nettoyer);
            cadre.Child = s;
            Children.Add(cadre);
            if (!vivant) return;
            minuteur.Tick += (o, e) => Mesurer();
            minuteur.Start();
            Mesurer();
        }

        private static TextBlock Ligne(StackPanel p, string nom, string valeur)
        {
            var P = Habillage.P;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("46,*") };
            g.Children.Add(new TextBlock { Text = nom, FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(P.Discret) });
            var v = new TextBlock { Text = valeur, FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Texte) };
            Grid.SetColumn(v, 1); g.Children.Add(v);
            p.Children.Add(g);
            return v;
        }

        private void Mesurer()
        {
            if (enCours || F.Source.Immediat) return;
            enCours = true;
            bool avecTemp = n++ % 3 == 0;
            Task.Run(() => { try { return F.Source.Mesurer(avecTemp); } catch { return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false;
                var m = t.Result;
                if (F.Bilan != null) tIndice.Text = F.Bilan.Score.ToString(CultureInfo.InvariantCulture);
                if (m == null) return;
                if (m.Processeur >= 0) tCpu.Text = Math.Round(m.Processeur) + " %";
                if (m.Memoire >= 0) tRam.Text = Math.Round(m.Memoire) + " %";
                if (m.Temperature > 0) { tTemp.Text = Math.Round(m.Temperature) + " °C"; tTemp.Foreground = Habillage.Pinceau(m.Temperature >= 90 ? Habillage.P.Rouge : m.Temperature >= 80 ? Habillage.P.Orange : Habillage.P.Texte); }
            }));
        }
    }
}
