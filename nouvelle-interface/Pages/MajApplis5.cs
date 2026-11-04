// Velox 5 - « Mettre à jour les applications » : la liste des mises à jour trouvées par winget (l'outil officiel
// de Microsoft), avec la version installée et la nouvelle ; tu coches celles à faire, avec si tu veux un point de
// restauration avant. Chaque application est mise à jour à part, avec son résultat (à jour, application ouverte...).
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
    internal sealed class MajApplis5 : StackPanel
    {
        /// <summary>(captures) liste d'exemple : winget n'est pas lancé.</summary>
        public static List<AppAMettreAJour> Exemple;
        private readonly FenetrePrincipale F;
        private readonly TextBlock tEtat;
        private readonly StackPanel liste = new StackPanel { Spacing = 4 };
        private readonly BoutonNoyau bLancer, bActualiser;
        private readonly CheckBox cPoint;
        private readonly Dictionary<string, CheckBox> cases = new Dictionary<string, CheckBox>();
        private List<AppAMettreAJour> apps = new List<AppAMettreAJour>();
        private FenetreInterieure fenetre;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var m = new MajApplis5(f);
            m.fenetre = f.Ouvrir("Mettre à jour les applications", m, 980);
        }

        private MajApplis5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(new TextBlock { Text = Langue.T("winget, l'outil officiel de Microsoft, télécharge chaque nouvelle version depuis le site de son éditeur. Coche celles à installer : chaque application est mise à jour à part, avec son résultat. Les mises à jour corrigent souvent des failles de sécurité."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto,Auto") };
            tEtat = new TextBlock { Text = Langue.T("Recherche des mises à jour… (jusqu'à une minute)"), FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Accent), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            haut.Children.Add(tEtat);
            var tout = new BoutonNoyau("Tout cocher", false, 0, 32) { TaillePolice = 12.5, Margin = new Thickness(10, 0, 0, 0) };
            tout.Clic += () => { foreach (var c in cases.Values) if (c.IsEnabled) c.IsChecked = true; Compter(); };
            var rien = new BoutonNoyau("Tout décocher", false, 0, 32) { TaillePolice = 12.5, Margin = new Thickness(8, 0, 0, 0) };
            rien.Clic += () => { foreach (var c in cases.Values) c.IsChecked = false; Compter(); };
            bActualiser = new BoutonNoyau("Actualiser", false, 0, 32) { TaillePolice = 12.5, Margin = new Thickness(8, 0, 0, 0) };
            bActualiser.Clic += Charger;
            Grid.SetColumn(tout, 1); Grid.SetColumn(rien, 2); Grid.SetColumn(bActualiser, 3);
            haut.Children.Add(tout); haut.Children.Add(rien); haut.Children.Add(bActualiser);
            Children.Add(haut);
            // en-tête des colonnes
            var entete = Ligne(null, "APPLICATION", "INSTALLÉE", "NOUVELLE", "", P.Discret, true);
            Children.Add(entete);
            Children.Add(new ScrollViewer { Content = liste, MaxHeight = 360, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
            cPoint = new CheckBox { Content = Langue.T("Créer un point de restauration avant (conseillé si tu mets à jour beaucoup d'applications)"), IsChecked = false, Foreground = Habillage.Pinceau(P.Texte) };
            Children.Add(cPoint);
            var bas = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            bLancer = new BoutonNoyau("Mettre à jour la sélection", true, 0, 42) { TaillePolice = 14, IsEnabled = false };
            bLancer.Clic += Lancer;
            bas.Children.Add(bLancer);
            if (Licence.Premium)
            {
                var auto = new BoutonNoyau("Mises à jour automatiques et exclusions", false, 0, 42) { TaillePolice = 13 };
                auto.Clic += () => { if (fenetre != null) fenetre.Fermer(); F.OuvrirOutil("majapps"); };
                bas.Children.Add(auto);
            }
            Children.Add(bas);
            Charger();
        }

        private Control Ligne(CheckBox c, string nom, string avant, string apres, string note, Color couleur, bool entete = false)
        {
            var P = Habillage.P;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("34,*,150,150,170"), MinHeight = entete ? 22 : 36 };
            if (c != null) { c.VerticalAlignment = VerticalAlignment.Center; g.Children.Add(c); }
            Action<string, int, Color, bool> col = (t, i, coul, mono) =>
            {
                var x = new TextBlock { Text = t, FontFamily = mono || entete ? Habillage.Console : Habillage.Corps, FontSize = entete ? 10.5 : 13, Foreground = Habillage.Pinceau(coul), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 8, 0) };
                if (!entete) ToolTip.SetTip(x, t);
                Grid.SetColumn(x, i); g.Children.Add(x);
            };
            col(entete ? Langue.T(nom) : nom, 1, entete ? P.Discret : P.Texte, false);
            col(entete ? Langue.T(avant) : avant, 2, P.Discret, true);
            col(entete ? Langue.T(apres) : apres, 3, entete ? P.Discret : P.Vert, true);
            col(note, 4, couleur, false);
            if (entete) return new Border { Child = g, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(10, 0) };
            return new Border { Child = g, Background = Habillage.Pinceau(P.Carte, 230), BorderBrush = Habillage.Pinceau(P.Accent, 30), BorderThickness = new Thickness(1), Padding = new Thickness(10, 0) };
        }

        private void Charger()
        {
            bActualiser.IsEnabled = false; bLancer.IsEnabled = false;
            tEtat.Text = Langue.T("Recherche des mises à jour… (jusqu'à une minute)");
            liste.Children.Clear(); cases.Clear();
            if (Exemple != null) { Afficher(Exemple, null); return; }
            Task.Run(() => { string err; var l = MajAppsAuto.Lister(out err); return Tuple.Create(l, err); })
                .ContinueWith(t => Dispatcher.UIThread.Post(() => Afficher(t.IsFaulted ? null : t.Result.Item1, t.IsFaulted ? Langue.T("La recherche a échoué.") : t.Result.Item2)));
        }

        private void Afficher(List<AppAMettreAJour> l, string erreur)
        {
            var P = Habillage.P;
            bActualiser.IsEnabled = true;
            if (l == null) { tEtat.Text = erreur ?? Langue.T("La recherche a échoué."); tEtat.Foreground = Habillage.Pinceau(P.Orange); return; }
            apps = l;
            int n = l.Count(a => !a.Exclue);
            tEtat.Text = n == 0 ? "✔  " + Langue.T("Toutes tes applications connues de winget sont à jour.") : Langue.F("{0} mise(s) à jour disponible(s).", n);
            tEtat.Foreground = Habillage.Pinceau(n == 0 ? P.Vert : P.Accent);
            foreach (var a in l.OrderBy(x => x.Exclue).ThenBy(x => x.Nom, StringComparer.CurrentCultureIgnoreCase))
            {
                var c = new CheckBox { IsChecked = !a.Exclue, IsEnabled = !a.Exclue };
                c.IsCheckedChanged += (o, e) => Compter();
                cases[a.Id] = c;
                liste.Children.Add(Ligne(c, a.Nom, a.Version, a.Disponible, a.Exclue ? Langue.T("exclue (Premium)") : "", a.Exclue ? P.Discret : P.Accent));
            }
            Compter();
        }

        private void Compter()
        {
            int n = cases.Count(kv => kv.Value.IsChecked == true);
            bLancer.Changer(n > 0 ? Langue.F("Mettre à jour la sélection ({0})", n) : Langue.T("Mettre à jour la sélection"));
            bLancer.IsEnabled = n > 0;
        }

        private void Lancer()
        {
            var ids = cases.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).Where(MajAppsAuto.IdValide).ToList();
            if (ids.Count == 0) return;
            var options = new List<string> { "--apps", string.Join(",", ids) };
            if (cPoint.IsChecked == true) options.Add("--point-restauration");
            var noms = apps.Where(a => ids.Contains(a.Id)).Select(a => "•  " + a.Nom + "  " + a.Version + " → " + a.Disponible).Take(12).ToList();
            if (ids.Count > 12) noms.Add(Langue.F("… et {0} autre(s)", ids.Count - 12));
            string question = Langue.F("Mettre à jour {0} application(s) ?", ids.Count) + "\n\n" + string.Join("\n", noms) + "\n\n"
                + Langue.T("Ferme-les si possible avant. Windows demande une seule autorisation pour toutes.");
            if (fenetre != null) fenetre.Fermer();
            F.LancerAction("majapps", options, question, () => { });
        }
    }
}
