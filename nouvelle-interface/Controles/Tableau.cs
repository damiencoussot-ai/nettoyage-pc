// Nettoyage PC 5 - tableau : en-tête cliquable (tri), lignes sélectionnables, cases à cocher facultatives,
// lignes estompées (éléments désactivés). Sert aux listes : démarrage, applications, pilotes, gros fichiers...
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal sealed class Colonne
    {
        public string Titre;
        public GridLength Largeur;
        public bool Droite, Chiffres;
        /// <summary>Clé de tri (par défaut : le texte affiché).</summary>
        public Func<object, IComparable> Cle;

        public Colonne(string titre, string largeur, bool droite = false, bool chiffres = false, Func<object, IComparable> cle = null)
        {
            Titre = titre; Largeur = GridLength.Parse(largeur); Droite = droite; Chiffres = chiffres; Cle = cle;
        }
    }

    internal sealed class Tableau : DockPanel
    {
        private readonly Colonne[] colonnes;
        private readonly bool cases;
        private readonly Grid entete = new Grid();
        private readonly ListBox liste = new ListBox();
        private List<object> elements = new List<object>();
        private Func<object, string[]> textes;
        private Func<object, bool> coche, estompe;
        private int triColonne = -1;
        private bool triDescendant, remplissage;

        /// <summary>Case cochée ou décochée par l'utilisateur : élément, nouvel état. Renvoyer faux annule le changement.</summary>
        public Func<object, bool, bool> CaseChangee;
        public event Action<object> DoubleClic;
        public event Action SelectionChangee;

        public Tableau(bool cases, bool multiple, params Colonne[] colonnes)
        {
            this.colonnes = colonnes; this.cases = cases;
            var P = Habillage.P;
            LastChildFill = true;
            liste.SelectionMode = multiple ? SelectionMode.Multiple : SelectionMode.Single;
            liste.Background = Habillage.Pinceau(P.Carte);
            liste.BorderThickness = new Thickness(0);
            liste.SelectionChanged += (s, e) => { var h = SelectionChangee; if (h != null) h(); };
            liste.DoubleTapped += (s, e) => { var o = Selection; var h = DoubleClic; if (o != null && h != null) h(o); };
            Definir(entete);
            for (int i = 0; i < colonnes.Length; i++)
            {
                int k = i;
                var t = new TextBlock
                {
                    Text = Langue.T(colonnes[i].Titre).ToUpper(System.Globalization.CultureInfo.GetCultureInfo("fr-FR")), FontFamily = Habillage.Console, FontSize = 10.5,
                    Foreground = Habillage.Pinceau(P.Discret), HorizontalAlignment = colonnes[i].Droite ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    Margin = new Thickness(0, 0, 12, 0), Cursor = new Cursor(StandardCursorType.Hand), Background = Brushes.Transparent,
                };
                t.PointerReleased += (s, e) => Trier(k);
                Grid.SetColumn(t, i + (cases ? 1 : 0));
                entete.Children.Add(t);
            }
            var bandeau = new Border { Child = entete, Padding = new Thickness(14, 9, 14, 8), Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(8, 14, 26) : Color.FromRgb(244, 247, 250)), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(0, 0, 0, 1) };
            SetDock(bandeau, Dock.Top);
            Children.Add(bandeau);
            Children.Add(liste);
        }

        public double HauteurListe { set { liste.MaxHeight = value; liste.MinHeight = Math.Min(value, 120); } }

        private void Definir(Grid g)
        {
            g.ColumnDefinitions.Clear();
            if (cases) g.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(34)));
            foreach (var c in colonnes) g.ColumnDefinitions.Add(new ColumnDefinition(c.Largeur));
        }

        public void Remplir(IEnumerable<object> elements, Func<object, string[]> textes, Func<object, bool> coche = null, Func<object, bool> estompe = null)
        {
            this.elements = elements.ToList(); this.textes = textes; this.coche = coche; this.estompe = estompe;
            Afficher();
        }

        private void Afficher()
        {
            var P = Habillage.P;
            IEnumerable<object> l = elements;
            if (triColonne >= 0)
            {
                var c = colonnes[triColonne]; int k = triColonne;
                Func<object, IComparable> cle = c.Cle ?? (o => (textes(o)[k] ?? ""));
                l = triDescendant ? l.OrderByDescending(cle) : l.OrderBy(cle);
            }
            remplissage = true;
            liste.Items.Clear();
            foreach (var o in l)
            {
                var g = new Grid { Tag = o, Margin = new Thickness(0, 4) };
                Definir(g);
                if (cases)
                {
                    var cb = new CheckBox { IsChecked = coche != null && coche(o), VerticalAlignment = VerticalAlignment.Center, MinWidth = 0, Padding = new Thickness(0) };
                    var el = o; var ligne = g;
                    cb.IsCheckedChanged += (s, e) =>
                    {
                        if (remplissage) return;
                        bool v = cb.IsChecked == true;
                        bool ok = CaseChangee == null || CaseChangee(el, v);
                        if (!ok) { remplissage = true; cb.IsChecked = !v; remplissage = false; }
                        else RemplirLigne(ligne, el);   // état et couleurs de la ligne à jour
                    };
                    g.Children.Add(cb);
                }
                RemplirLigne(g, o);
                liste.Items.Add(g);
            }
            remplissage = false;
        }

        private void RemplirLigne(Grid g, object o)
        {
            var P = Habillage.P;
            foreach (var ancien in g.Children.OfType<TextBlock>().ToList()) g.Children.Remove(ancien);
            bool terne = estompe != null && estompe(o);
            var t = textes(o);
            for (int i = 0; i < colonnes.Length && i < t.Length; i++)
            {
                var c = colonnes[i];
                var tb = new TextBlock
                {
                    Text = t[i] ?? "", FontFamily = c.Chiffres ? Habillage.Console : Habillage.Corps, FontSize = c.Chiffres ? 12.5 : 13,
                    Foreground = Habillage.Pinceau(terne ? P.Discret : i == 0 ? P.Texte : P.Discret), TextTrimming = TextTrimming.CharacterEllipsis,
                    HorizontalAlignment = c.Droite ? HorizontalAlignment.Right : HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0),
                };
                if (!string.IsNullOrEmpty(t[i]) && t[i].Length > 30) ToolTip.SetTip(tb, t[i]);
                Grid.SetColumn(tb, i + (cases ? 1 : 0));
                g.Children.Add(tb);
            }
        }

        private void Trier(int k)
        {
            if (triColonne == k) triDescendant = !triDescendant;
            else { triColonne = k; triDescendant = colonnes[k].Droite; }   // tailles et nombres : du plus grand au plus petit
            Afficher();
        }

        public object Selection { get { var g = liste.SelectedItem as Control; return g != null ? g.Tag : null; } }

        public List<object> Selections
        {
            get
            {
                var l = new List<object>();
                if (liste.SelectedItems != null) foreach (var x in liste.SelectedItems) { var g = x as Control; if (g != null && g.Tag != null) l.Add(g.Tag); }
                return l;
            }
        }

        public int Nombre { get { return elements.Count; } }

        public void Retirer(object o) { elements.Remove(o); Afficher(); }
    }
}
