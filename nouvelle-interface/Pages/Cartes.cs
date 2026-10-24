// Nettoyage PC 5 - briques des rubriques : cartes d'action (titre, explication, bouton), grille qui s'adapte
// à la largeur, « Plus d'options » repliable, et onglets en haut d'une rubrique qui a plusieurs pages.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    /// <summary>Une carte : son bouton lance une action du moteur (« action:cle »), ouvre une rubrique, ou exécute du code.</summary>
    internal sealed class CarteAction
    {
        public string Titre, Texte, Bouton, Cible;
        public Action Faire;
        public bool Principal, EnPlus;

        public static CarteAction A(string titre, string texte, string bouton, string action, bool principal = false)
        { return new CarteAction { Titre = titre, Texte = texte, Bouton = bouton, Cible = "action:" + action, Principal = principal }; }

        public static CarteAction Code(string titre, string texte, string bouton, Action faire, bool principal = false)
        { return new CarteAction { Titre = titre, Texte = texte, Bouton = bouton, Faire = faire, Principal = principal }; }

        public CarteAction Plus() { EnPlus = true; return this; }
    }

    internal static class Cartes
    {
        /// <summary>Grille de cartes : 3 colonnes en grand, 2 en moyen, 1 en étroit ; les cartes d'une ligne ont la même hauteur.</summary>
        public static Control Grille(FenetrePrincipale f, IEnumerable<CarteAction> cartes)
        {
            var liste = cartes.ToList();
            var g = new UniformGrid { Columns = 3 };
            foreach (var c in liste) g.Children.Add(Carte(f, c));
            g.SizeChanged += (o, a) =>
            {
                int col = a.NewSize.Width >= 900 ? 3 : a.NewSize.Width >= 560 ? 2 : 1;
                col = Math.Max(1, Math.Min(col, liste.Count));
                if (g.Columns != col) g.Columns = col;
            };
            return g;
        }

        public static Control Carte(FenetrePrincipale f, CarteAction c)
        {
            var P = Habillage.P;
            var b = new Border
            {
                Margin = new Thickness(0, 0, 12, 12), Padding = new Thickness(20, 18, 20, 18), CornerRadius = new CornerRadius(3),
                Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(c.Principal ? Habillage.Alpha(P.Accent, 110) : P.Bordure), BorderThickness = new Thickness(1),
            };
            var d = new DockPanel { LastChildFill = true };
            var bouton = new BoutonNoyau(c.Bouton, c.Principal, Math.Max(150, Math.Min(260, c.Bouton.Length * 8.2 + 44)), 36) { TaillePolice = 13.5, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 14, 0, 0) };
            bouton.Clic += () =>
            {
                if (c.Faire != null) { try { c.Faire(); } catch (Exception ex) { JournalDemarrage.Erreur("carte " + c.Titre, ex); f.Statut(Langue.T("Une erreur s'est produite :") + " " + ex.Message, true); } }
                else f.Executer(c.Cible, c.Bouton);
            };
            DockPanel.SetDock(bouton, Dock.Bottom);
            d.Children.Add(bouton);
            var s = new StackPanel { Spacing = 6 };
            if (c.Principal) s.Children.Add(new Border { Height = 2, Width = 36, HorizontalAlignment = HorizontalAlignment.Left, Background = Habillage.DegradeAccent(), Margin = new Thickness(0, 0, 0, 4) });
            s.Children.Add(new TextBlock { Text = Langue.T(c.Titre), FontFamily = Habillage.Affichage, FontSize = 16.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
            s.Children.Add(new TextBlock { Text = Langue.T(c.Texte), FontFamily = Habillage.Corps, FontSize = 13, LineHeight = 19, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            d.Children.Add(s);
            b.Child = d;
            return b;
        }

        /// <summary>Cartes principales, puis « Plus d'options » (replié) pour les autres.</summary>
        public static Control Page(FenetrePrincipale f, string intro, IEnumerable<CarteAction> cartes, string clePlus)
        {
            var P = Habillage.P;
            var liste = cartes.ToList();
            var pile = new StackPanel { Spacing = 12 };
            if (!string.IsNullOrEmpty(intro))
                pile.Children.Add(new TextBlock { Text = Langue.T(intro), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 12, 4) });
            pile.Children.Add(Grille(f, liste.Where(c => !c.EnPlus)));
            var plus = liste.Where(c => c.EnPlus).ToList();
            if (plus.Count > 0)
            {
                bool ouvert = f.OptionOuverte(clePlus);
                var grille = Grille(f, plus);
                grille.IsVisible = ouvert;
                var bascule = new BoutonNoyau(ouvert ? "Moins d'options  ▴" : "Plus d'options  ▾", false, 170, 32) { TaillePolice = 13, HorizontalAlignment = HorizontalAlignment.Left };
                bascule.Clic += () =>
                {
                    bool o = !grille.IsVisible;
                    grille.IsVisible = o;
                    bascule.Changer(o ? "Moins d'options  ▴" : "Plus d'options  ▾");
                    f.OptionOuverte(clePlus, o);
                };
                pile.Children.Add(bascule);
                pile.Children.Add(grille);
            }
            return pile;
        }
    }

    /// <summary>Rubrique à plusieurs pages : onglets en haut (trait lumineux sous l'onglet actif), page dessous.</summary>
    internal sealed class Onglets : DockPanel
    {
        private readonly List<Tuple<string, string, Func<Control>>> pages;
        private readonly StackPanel barre = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        private readonly TransitioningContentControl contenu = new TransitioningContentControl();
        private readonly FenetrePrincipale F;
        private readonly string rubrique;
        private string active;

        /// <summary>pages : (clé, nom affiché, fabrique de la page).</summary>
        public Onglets(FenetrePrincipale f, string rubrique, IEnumerable<Tuple<string, string, Func<Control>>> pages, string premiere = null)
        {
            F = f; this.rubrique = rubrique;
            this.pages = pages.ToList();
            Margin = new Thickness(28, 6, 16, 24);
            LastChildFill = true;
            if (Habillage.Animations) contenu.PageTransition = new Avalonia.Animation.CrossFade(TimeSpan.FromMilliseconds(150));
            var haut = new Border { BorderBrush = Habillage.Pinceau(Habillage.P.Bordure), BorderThickness = new Thickness(0, 0, 0, 1), Margin = new Thickness(0, 0, 12, 18), Child = barre };
            SetDock(haut, Dock.Top);
            Children.Add(haut);
            Children.Add(contenu);
            Choisir(premiere ?? f.SousPage(rubrique) ?? this.pages[0].Item1);
        }

        public void Choisir(string cle)
        {
            var p = pages.FirstOrDefault(x => x.Item1 == cle) ?? pages[0];
            active = p.Item1;
            F.SousPage(rubrique, active);
            barre.Children.Clear();
            var P = Habillage.P;
            foreach (var o in pages)
            {
                bool actif = o.Item1 == active;
                var texte = new TextBlock { Text = Langue.T(o.Item2), FontFamily = Habillage.Affichage, FontSize = 14, FontWeight = actif ? FontWeight.SemiBold : FontWeight.Normal, Foreground = Habillage.Pinceau(actif ? P.Texte : P.Discret) };
                var trait = new Border { Height = 2, Background = actif ? Habillage.DegradeAccent() : Brushes.Transparent, Margin = new Thickness(0, 9, 0, -1) };
                var bloc = new StackPanel { Children = { texte, trait }, Margin = new Thickness(0, 0, 18, 0), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand), Background = Brushes.Transparent };
                string cle2 = o.Item1;
                bloc.PointerEntered += (s, e) => { if (cle2 != active) texte.Foreground = Habillage.Pinceau(P.Texte); };
                bloc.PointerExited += (s, e) => { if (cle2 != active) texte.Foreground = Habillage.Pinceau(P.Discret); };
                bloc.PointerReleased += (s, e) => { if (cle2 != active) Choisir(cle2); };
                barre.Children.Add(bloc);
            }
            contenu.Content = p.Item3();
        }
    }
}
