// Nettoyage PC 5 - « Que veux-tu faire ? » (Ctrl+K) : tape quelques lettres, les rubriques, onglets et actions
// correspondants s'affichent, les meilleurs d'abord. Entrée lance le premier, flèches pour choisir, Échap pour fermer.
// Les actions passent par le même chemin que leurs boutons (question avant toute action réelle).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal sealed class EntreeCommande
    {
        public string Titre, Detail, Type;   // Type : « Rubrique », « Action »
        public Action Faire;
        public string Cle;   // texte de recherche (sans accents, en minuscules)
    }

    internal sealed class PaletteCommandes : Panel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBox saisie = new TextBox();
        private readonly StackPanel liste = new StackPanel { Spacing = 2 };
        private readonly List<EntreeCommande> entrees;
        private List<EntreeCommande> resultats = new List<EntreeCommande>();
        private int choix;

        public PaletteCommandes(FenetrePrincipale f, List<EntreeCommande> entrees)
        {
            F = f; this.entrees = entrees;
            var P = Habillage.P;
            Focusable = true;
            Background = Habillage.Pinceau(Colors.Black, P.Sombre ? 120 : 60);
            var carte = new Border
            {
                Width = 620, Margin = new Thickness(0, 90, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top,
                CornerRadius = new CornerRadius(4), Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(9, 17, 31) : Colors.White),
                BorderBrush = Habillage.Pinceau(P.Accent, 140), BorderThickness = new Thickness(1), Padding = new Thickness(10),
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 40, OffsetY = 12, Color = Habillage.Alpha(Colors.Black, P.Sombre ? 170 : 60) }),
            };
            var pile = new StackPanel { Spacing = 8 };
            saisie.Watermark = Langue.T("Que veux-tu faire ? (nettoyer, démarrage, sauvegarde…)");
            saisie.FontSize = 16; saisie.FontFamily = Habillage.Corps;
            saisie.TextChanged += (s, e) => { choix = 0; Filtrer(); };
            saisie.KeyDown += Touche;
            pile.Children.Add(saisie);
            pile.Children.Add(new ScrollViewer { Content = liste, MaxHeight = 420, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto });
            carte.Child = pile;
            Children.Add(carte);
            PointerReleased += (s, e) => { if (e.Source == this) F.Retirer(this); };   // clic sur le fond : on ferme (rien n'est lancé)
            AttachedToVisualTree += (s, e) => saisie.Focus();
            Filtrer();
        }

        public static string Normaliser(string s)
        {
            var d = (s ?? "").ToLowerInvariant().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (char c in d) if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
            return sb.ToString();
        }

        private void Filtrer()
        {
            string q = Normaliser(saisie.Text).Trim();
            var mots = q.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (mots.Length == 0) resultats = entrees.Where(e => e.Type == "Rubrique").Take(12).ToList();
            else
                resultats = entrees
                    .Select(e => new { e, n = Note(e, q, mots) })
                    .Where(x => x.n > 0).OrderByDescending(x => x.n).ThenBy(x => x.e.Titre.Length).Take(12).Select(x => x.e).ToList();
            choix = Math.Max(0, Math.Min(choix, resultats.Count - 1));
            Afficher();
        }

        /// <summary>Note d'une entrée : titre qui commence par la recherche > mot du titre > texte qui contient tous les mots.</summary>
        private static int Note(EntreeCommande e, string q, string[] mots)
        {
            string titre = Normaliser(e.Titre);
            if (!mots.All(m => e.Cle.Contains(m))) return 0;
            int n = 10;
            if (titre.StartsWith(q)) n += 100;
            else if (titre.Split(' ', '\'', '-').Any(w => w.StartsWith(mots[0]))) n += 50;
            else if (titre.Contains(q)) n += 30;
            if (e.Type == "Rubrique") n += 5;
            return n;
        }

        private void Afficher()
        {
            var P = Habillage.P;
            liste.Children.Clear();
            if (resultats.Count == 0)
            {
                liste.Children.Add(new TextBlock { Text = Langue.T("Rien trouvé. Essaie un autre mot (par exemple : nettoyer, disque, démarrage, antivirus)."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), Margin = new Thickness(10, 8), TextWrapping = TextWrapping.Wrap });
                return;
            }
            for (int i = 0; i < resultats.Count; i++)
            {
                var r = resultats[i]; int k = i;
                bool actif = i == choix;
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                var s = new StackPanel { Spacing = 1 };
                s.Children.Add(new TextBlock { Text = r.Titre, FontFamily = Habillage.Corps, FontSize = 14, FontWeight = actif ? FontWeight.SemiBold : FontWeight.Normal, Foreground = Habillage.Pinceau(P.Texte) });
                if (!string.IsNullOrEmpty(r.Detail)) s.Children.Add(new TextBlock { Text = r.Detail, FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextTrimming = TextTrimming.CharacterEllipsis });
                g.Children.Add(s);
                var type = new TextBlock { Text = Langue.T(r.Type).ToUpperInvariant(), FontFamily = Habillage.Console, FontSize = 10, Foreground = Habillage.Pinceau(r.Type == "Action" ? P.Accent : P.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 4, 0) };
                Grid.SetColumn(type, 1); g.Children.Add(type);
                var ligne = new Border
                {
                    Child = g, Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(3), Cursor = new Cursor(StandardCursorType.Hand),
                    Background = actif ? Habillage.Pinceau(P.Accent, P.Sombre ? 34 : 26) : Brushes.Transparent,
                    BorderBrush = actif ? Habillage.Pinceau(P.Accent, 110) : Brushes.Transparent, BorderThickness = new Thickness(1),
                };
                ligne.PointerReleased += (o, e) => { e.Handled = true; Lancer(k); };
                liste.Children.Add(ligne);
            }
        }

        private void Touche(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) { e.Handled = true; F.Retirer(this); }
            else if (e.Key == Key.Down) { e.Handled = true; if (resultats.Count > 0) { choix = (choix + 1) % resultats.Count; Afficher(); } }
            else if (e.Key == Key.Up) { e.Handled = true; if (resultats.Count > 0) { choix = (choix - 1 + resultats.Count) % resultats.Count; Afficher(); } }
            else if (e.Key == Key.Enter) { e.Handled = true; Lancer(choix); }
        }

        private void Lancer(int i)
        {
            if (i < 0 || i >= resultats.Count) return;
            var r = resultats[i];
            F.Retirer(this);
            try { r.Faire(); } catch (Exception ex) { JournalDemarrage.Erreur("palette (version 5)", ex); }
        }
    }
}
