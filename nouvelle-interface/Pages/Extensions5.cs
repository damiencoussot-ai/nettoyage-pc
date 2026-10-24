// Nettoyage PC 5 - extensions des navigateurs (lecture seule) : celles installées hors de la boutique officielle ou qui
// peuvent lire tous les sites sont signalées. La suppression se fait dans le navigateur : Nettoyage PC ne touche à rien.
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
    internal sealed class Extensions5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Tableau tableau;
        private readonly TextBlock info;
        private readonly Dictionary<Extension, int> niveaux = new Dictionary<Extension, int>();

        public static void Ouvrir(FenetrePrincipale f) { f.Ouvrir("Extensions des navigateurs", new Extensions5(f), 1040); }

        private Extensions5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(new TextBlock { Text = Langue.T("Les extensions installées dans tes navigateurs. Une extension inconnue, installée hors de la boutique officielle ou qui peut lire tous les sites mérite d'être vérifiée. Nettoyage PC ne touche à rien : la suppression se fait dans le navigateur."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            tableau = new Tableau(false, false, new Colonne("Extension", "2*"), new Colonne("Navigateur", "1.2*"), new Colonne("Version", "90", false, true), new Colonne("Avis", "3*"));
            tableau.HauteurListe = 400;
            tableau.DoubleClic += o => Gerer(o as Extension);
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            info = new TextBlock { Text = Langue.T("Recherche des extensions…"), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap };
            Children.Add(info);
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var bGerer = new BoutonNoyau("Gérer dans le navigateur", true, 0, 36) { TaillePolice = 13.5 };
            bGerer.Clic += () => Gerer(tableau.Selection as Extension);
            var bPage = new BoutonNoyau("Page de l'extension", false, 0, 36) { TaillePolice = 13 };
            bPage.Clic += () => { var x = tableau.Selection as Extension; if (x != null && x.Page != "") Programmes.Ouvrir(x.Page); };
            boutons.Children.Add(bGerer); boutons.Children.Add(bPage);
            Children.Add(boutons);
            AttachedToVisualTree += (s, e) => Charger();
        }

        private void Charger()
        {
            Task.Run(() => { try { return ExtensionsNavigateurs.Lister(); } catch (Exception ex) { JournalDemarrage.Erreur("extensions (version 5)", ex); return new List<Extension>(); } })
                .ContinueWith(t => Dispatcher.UIThread.Post(() => Afficher(t.Result)));
        }

        private void Afficher(List<Extension> liste)
        {
            var P = Habillage.P;
            niveaux.Clear();
            var avis = new Dictionary<Extension, string>();
            foreach (var x in liste) { int n; avis[x] = ExtensionsNavigateurs.Avis(x, out n); niveaux[x] = n; }
            tableau.Remplir(liste.OrderBy(x => niveaux[x]).ThenBy(x => x.Nom).Cast<object>(), o =>
            {
                var x = (Extension)o;
                return new[] { (niveaux[x] == 0 ? "⚠ " : "") + x.Nom, x.Navigateur + " (" + x.Profil + ")", x.Version, Langue.T(avis[x]) };
            }, null, o => niveaux[(Extension)o] == 2);
            int aVerifier = niveaux.Values.Count(n => n == 0);
            if (liste.Count == 0) { info.Text = Langue.T("Aucune extension trouvée (Chrome, Edge, Brave, Vivaldi, Firefox)."); info.Foreground = Habillage.Pinceau(P.Vert); }
            else if (aVerifier > 0) { info.Text = Langue.F("{0} extension(s), dont {1} à vérifier (marquées ⚠). Si tu ne la reconnais pas, supprime-la depuis le navigateur.", liste.Count, aVerifier); info.Foreground = Habillage.Pinceau(P.Orange); }
            else { info.Text = Langue.F("{0} extension(s), aucune n'est suspecte. ✔", liste.Count); info.Foreground = Habillage.Pinceau(P.Vert); }
        }

        private void Gerer(Extension x)
        {
            if (x == null) { F.Statut("Sélectionne d'abord une extension dans la liste."); return; }
            var pages = new Dictionary<string, string[]> { { "Chrome", new[] { "chrome.exe", "chrome://extensions" } }, { "Edge", new[] { "msedge.exe", "edge://extensions" } }, { "Brave", new[] { "brave.exe", "brave://extensions" } }, { "Vivaldi", new[] { "vivaldi.exe", "vivaldi://extensions" } }, { "Firefox", new[] { "firefox.exe", "about:addons" } } };
            string[] p; if (!pages.TryGetValue(x.Navigateur, out p)) return;
            try { Programmes.Ouvrir(p[0], p[1]); } catch { F.Demander(Langue.F("Ouvre {0}", x.Navigateur), Langue.F("puis va dans {0}", p[1]), "Compris", null, r => { }); }
        }
    }
}
