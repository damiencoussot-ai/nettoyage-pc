// Nettoyage PC 5 - visite guidée (30 secondes) : l'écran s'assombrit, sauf la zone expliquée (menu, indice,
// bouton « Optimiser », accès rapide, alertes, Ctrl+K), avec une bulle « Suivant / Passer ». Lancée depuis l'annonce
// du nouveau look, « Que veux-tu faire ? » (Ctrl+K) ou Réglages. Rien n'est modifié pendant la visite.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class Visite5 : Panel
    {
        private sealed class Etape { public Func<Control> Cible; public string Titre, Texte; }

        private readonly FenetrePrincipale F;
        private readonly List<Etape> etapes;
        private readonly Canvas calque = new Canvas();
        private readonly Cadre5 bulle;
        private readonly TextBlock tCompteur, tTitre, tTexte;
        private readonly BoutonNoyau bSuivant;
        private Rect trou;
        private int index;

        public static void Lancer(FenetrePrincipale f)
        {
            var v = new Visite5(f);
            f.Superposer(v);
            Dispatcher.UIThread.Post(() => v.Montrer(0), DispatcherPriority.Background);
        }

        private Visite5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Focusable = true;
            Background = Brushes.Transparent;   // capte les clics : l'interface ne réagit pas pendant la visite
            etapes = new List<Etape>
            {
                new Etape { Cible = () => F.ZoneMenu, Titre = "Les rubriques", Texte = "Nettoyer, Accélérer, Protéger, Outils et Premium, toujours ici, dans le même ordre. Ctrl+1 à Ctrl+6 les ouvrent au clavier." },
                new Etape { Cible = () => PageAccueil.Courante == null ? null : PageAccueil.Courante.ZoneAnneau, Titre = "L'indice de ton PC", Texte = "Une note sur 100, calculée à partir du stockage, de la mémoire, de la sécurité et du matériel. Plus elle est haute, mieux ton PC se porte." },
                new Etape { Cible = () => PageAccueil.Courante == null ? null : PageAccueil.Courante.BoutonOptimiser, Titre = "Optimiser en 1 clic", Texte = "Tout l'entretien d'un coup. Une question s'affiche toujours avant : rien n'est lancé sans ton accord." },
                new Etape { Cible = () => PageAccueil.Courante == null ? null : PageAccueil.Courante.ZoneFavoris, Titre = "Accès rapide", Texte = "Tes outils préférés, lancés en un clic. Une bulle verte montre ceux qui sont activés. L'étoile ☆ sur chaque outil l'ajoute ici." },
                new Etape { Cible = () => PageAccueil.Courante == null ? null : PageAccueil.Courante.ZoneAlertes, Titre = "Les alertes", Texte = "Ce qui peut être amélioré sur ton PC, chacune avec son bouton pour la régler." },
                new Etape { Cible = () => F.ZoneRecherche, Titre = "Tu cherches un outil ?", Texte = "Clique ici ou appuie sur Ctrl+K, puis tape son nom : il s'ouvre directement." },
            };
            Children.Add(calque);
            var s = new StackPanel { Spacing = 8 };
            tCompteur = new TextBlock { FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Accent) };
            tTitre = new TextBlock { FontFamily = Habillage.Affichage, FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            tTexte = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13.5, LineHeight = 20, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            s.Children.Add(tCompteur); s.Children.Add(tTitre); s.Children.Add(tTexte);
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
            bSuivant = new BoutonNoyau("Suivant", true, 120, 36) { TaillePolice = 13.5 };
            bSuivant.Clic += () => Montrer(index + 1);
            var bPasser = new BoutonNoyau("Passer la visite", false, 0, 36) { TaillePolice = 13 };
            bPasser.Clic += Terminer;
            boutons.Children.Add(bSuivant); boutons.Children.Add(bPasser);
            s.Children.Add(boutons);
            bulle = new Cadre5 { Child = s, Width = 340, Coupe = 16, Padding = new Thickness(18, 16), Background = Habillage.Pinceau(P.Sombre ? Color.FromRgb(6, 16, 25) : Colors.White), BorderBrush = Habillage.Pinceau(P.Accent, 170), BorderThickness = new Thickness(1) };
            calque.Children.Add(bulle);
            KeyDown += (o, e) => { if (e.Key == Key.Escape) { e.Handled = true; Terminer(); } else if (e.Key == Key.Enter || e.Key == Key.Right) { e.Handled = true; Montrer(index + 1); } };
            SizeChanged += (o, e) => Placer();
        }

        private void Montrer(int i)
        {
            // étapes dont la zone n'est pas à l'écran (style, mode simple) : sautées
            while (i < etapes.Count && etapes[i].Cible() == null) i++;
            if (i >= etapes.Count) { Terminer(); return; }
            index = i;
            var e = etapes[i];
            int rang = etapes.Take(i + 1).Count(x => x.Cible() != null), total = etapes.Count(x => x.Cible() != null);
            tCompteur.Text = Langue.T("VISITE GUIDÉE") + " · " + rang + " / " + total;
            tTitre.Text = Langue.T(e.Titre);
            tTexte.Text = Langue.T(e.Texte);
            bSuivant.Changer(rang == total ? "Terminer" : "Suivant");
            try { e.Cible().BringIntoView(); } catch { }
            Focus();
            Dispatcher.UIThread.Post(Placer, DispatcherPriority.Background);
        }

        private void Placer()
        {
            if (index >= etapes.Count) return;
            var c = etapes[index].Cible();
            if (c == null || Bounds.Width <= 0) return;
            var p = c.TranslatePoint(new Point(0, 0), this);
            if (!p.HasValue) return;
            trou = new Rect(p.Value, c.Bounds.Size).Inflate(6).Intersect(new Rect(Bounds.Size));
            // la bulle : à droite de la zone si la place le permet, sinon dessous, sinon dessus
            double w = bulle.Width, h = Math.Max(150, bulle.DesiredSize.Height), x, y;
            if (trou.Right + 18 + w <= Bounds.Width - 12) { x = trou.Right + 18; y = Math.Max(12, Math.Min(Bounds.Height - h - 12, trou.Top)); }
            else if (trou.Bottom + 18 + h <= Bounds.Height - 12) { x = Math.Max(12, Math.Min(Bounds.Width - w - 12, trou.Left)); y = trou.Bottom + 18; }
            else { x = Math.Max(12, Math.Min(Bounds.Width - w - 12, trou.Left)); y = Math.Max(12, trou.Top - h - 18); }
            Canvas.SetLeft(bulle, x); Canvas.SetTop(bulle, y);
            InvalidateVisual();
        }

        public override void Render(DrawingContext ctx)
        {
            base.Render(ctx);
            var P = Habillage.P;
            var tout = new Rect(Bounds.Size);
            var voile = trou.Width > 0 ? (Geometry)new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(tout), new RectangleGeometry(trou)) : new RectangleGeometry(tout);
            ctx.DrawGeometry(Habillage.Pinceau(Colors.Black, P.Sombre ? 170 : 120), null, voile);
            if (trou.Width > 0)
            {
                ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(P.Accent, 60), 8), trou);
                ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(P.Accent), 1.5), trou);
            }
        }

        private void Terminer()
        {
            F.Retirer(this);
            F.Statut("Visite terminée. Tu peux la revoir à tout moment avec Ctrl+K, « Visite guidée ».");
        }
    }
}
