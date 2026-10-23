// Nettoyage PC 5 - mode simple : l'état du PC en une phrase, trois gros boutons (Nettoyer, Vérifier, Appeler à l'aide)
// et les quelques points à régler. Pour un débutant : chaque action réelle demande d'abord « Oui ».
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal sealed class PageSimple5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tEtat, tDetail;
        private readonly StackPanel problemes = new StackPanel { Spacing = 8 };

        public PageSimple5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 18; Margin = new Thickness(40, 18, 40, 28); MaxWidth = 960;
            Children.Add(new TextBlock { Text = Textes.Salut(f.Pseudo, DateTime.Now), FontFamily = Habillage.Affichage, FontSize = 30, Foreground = Habillage.Pinceau(P.Texte) });
            var entete = new StackPanel { Spacing = 4, Margin = new Thickness(0, -10, 0, 0) };
            tEtat = new TextBlock { Text = Langue.T("Je regarde l'état de ton PC…"), FontFamily = Habillage.Affichage, FontSize = 19, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap };
            tDetail = new TextBlock { FontFamily = Habillage.Corps, FontSize = 14, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            entete.Children.Add(tEtat); entete.Children.Add(tDetail);
            Children.Add(entete);
            var grille = new Avalonia.Controls.Primitives.UniformGrid { Columns = 3 };
            grille.SizeChanged += (o, a) => grille.Columns = a.NewSize.Width < 640 ? 1 : 3;
            var bNettoyer = new GrosBouton5("nettoyer", "Nettoyer mon PC", "Supprime les fichiers inutiles. Tes documents et tes photos ne sont jamais touchés.", true);
            bNettoyer.Clic += () => F.LancerAction("standard", null, "Nettoyer le PC maintenant ?\n\nLes fichiers inutiles (temporaires, caches) sont supprimés et la corbeille est vidée. Tes documents, photos et vidéos ne sont jamais touchés.\n\nFerme ton navigateur Internet pour un meilleur résultat.");
            var bVerifier = new GrosBouton5("verifier", "Vérifier mon PC", "Regarde si tout va bien : disques, antivirus, mises à jour, espace libre.", false);
            bVerifier.Clic += () => { tEtat.Text = Langue.T("Je regarde l'état de ton PC…"); tEtat.Foreground = Habillage.Pinceau(Habillage.P.Accent); F.ActualiserBilan(); };
            var bAide = new GrosBouton5("aide", "Appeler à l'aide", "Un proche voit ton écran et t'aide depuis chez lui, en toute sécurité.", false);
            bAide.Clic += Aide;
            grille.Children.Add(bNettoyer); grille.Children.Add(bVerifier); grille.Children.Add(bAide);
            Children.Add(grille);
            Children.Add(problemes);
            var liens = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 8, 0, 0) };
            var arnaque = new BoutonNoyau("On t'appelle pour « réparer » ton PC ? Clique ici", false, 0, 34) { TaillePolice = 13, HorizontalAlignment = HorizontalAlignment.Left };
            arnaque.Clic += () => Bouclier5.Ouvrir(F, true);
            liens.Children.Add(arnaque);
            var complet = new BoutonNoyau("Afficher toutes les options (mode complet)", false, 0, 34) { TaillePolice = 13 };
            complet.Clic += () => F.Demander("Passer au mode complet ?", "Toutes les rubriques et les options avancées seront affichées. Tu pourras revenir au mode simple dans Réglages.", "Mode complet", "Rester en mode simple", oui => { if (oui) F.AppliquerModeSimple(false); });
            Grid.SetColumn(complet, 1); liens.Children.Add(complet);
            Children.Add(liens);
            AttachedToVisualTree += (s, e) =>
            {
                F.BilanPret += Afficher;
                if (F.Bilan != null) Afficher(F.Bilan);
                if (F.Bilan == null || (DateTime.Now - F.Bilan.Date).TotalMinutes > 5) F.ActualiserBilan();
            };
            DetachedFromVisualTree += (s, e) => F.BilanPret -= Afficher;
        }

        private void Aide()
        {
            F.Demander("Appeler à l'aide", "Quelqu'un que tu connais va pouvoir voir ton écran pour t'aider.\n\n1. Appelle cette personne. Elle ouvre l'Assistance rapide sur son PC et te donne un code.\n2. Clique sur « Ouvrir l'Assistance rapide » ici, tape le code, puis accepte.\n\nAttention : n'accepte jamais si c'est quelqu'un qui t'a appelé sans que tu le demandes (faux technicien, fausse banque).",
                "Ouvrir l'Assistance rapide", "Annuler", oui => { if (oui) PageAide5.Ouvrir(F); });
        }

        private void Afficher(Bilan b)
        {
            var P = Habillage.P;
            var l = b.Points.Where(p => p.Etat != EtatPoint.Ok).OrderBy(p => p.Etat == EtatPoint.Probleme ? 0 : 1).ToList();
            int nbP = l.Count(p => p.Etat == EtatPoint.Probleme);
            if (l.Count == 0) { tEtat.Text = Langue.T("✔  Ton PC va bien."); tEtat.Foreground = Habillage.Pinceau(P.Vert); tDetail.Text = Langue.T("Rien à faire pour l'instant. Tu peux le nettoyer de temps en temps avec le premier bouton."); }
            else if (nbP > 0) { tEtat.Text = Langue.F("Ton PC a besoin de toi : {0} chose(s) à régler.", l.Count); tEtat.Foreground = Habillage.Pinceau(P.Rouge); tDetail.Text = Langue.T("Clique sur le bouton à droite de chaque ligne ci-dessous."); }
            else { tEtat.Text = Langue.F("Ton PC va bien, mais {0} chose(s) peuvent être améliorées.", l.Count); tEtat.Foreground = Habillage.Pinceau(P.Orange); tDetail.Text = Langue.T("Clique sur le bouton à droite de chaque ligne ci-dessous, ou sur « Nettoyer mon PC »."); }
            problemes.Children.Clear();
            foreach (var p in l.Take(5))
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("36,*,Auto"), MinHeight = 52 };
                Color c = p.Etat == EtatPoint.Probleme ? P.Rouge : P.Orange;
                g.Children.Add(new TextBlock { Text = p.Etat == EtatPoint.Probleme ? "✖" : "!", FontSize = 16, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(c), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center });
                var t = new TextBlock { Text = p.Texte, FontFamily = Habillage.Corps, FontSize = 14.5, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8) };
                Grid.SetColumn(t, 1); g.Children.Add(t);
                if (p.Bouton != null && p.Cible != null)
                {
                    var bt = new BoutonNoyau(p.Bouton, p.Etat == EtatPoint.Probleme, 0, 36) { TaillePolice = 14, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 10, 0) };
                    var cible = p.Cible; var nom = p.Bouton;
                    bt.Clic += () => F.Executer(cible, nom);
                    Grid.SetColumn(bt, 2); g.Children.Add(bt);
                }
                problemes.Children.Add(new Border { Child = g, CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(Habillage.Alpha(c, 110)), BorderThickness = new Thickness(1) });
            }
        }
    }

    /// <summary>Gros bouton carré : icône dessinée, titre, explication. Le principal est en dégradé lumineux.</summary>
    internal sealed class GrosBouton5 : Control
    {
        private readonly string icone, titre, detail;
        private readonly bool principal;
        private bool survol, appui;
        public event Action Clic;

        public GrosBouton5(string icone, string titre, string detail, bool principal)
        {
            this.icone = icone; this.titre = Langue.T(titre); this.detail = Langue.T(detail); this.principal = principal;
            Height = 220; Margin = new Thickness(0, 0, 18, 18); Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
        }

        protected override void OnPointerEntered(PointerEventArgs e) { base.OnPointerEntered(e); survol = true; InvalidateVisual(); }
        protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); survol = false; appui = false; InvalidateVisual(); }
        protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); appui = true; InvalidateVisual(); }
        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            bool dedans = new Rect(Bounds.Size).Contains(e.GetPosition(this));
            bool ok = appui && dedans; appui = false; InvalidateVisual();
            if (ok) { var c = Clic; if (c != null) c(); }
        }
        protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; var c = Clic; if (c != null) c(); } }
        protected override void OnGotFocus(GotFocusEventArgs e) { base.OnGotFocus(e); InvalidateVisual(); }
        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            var r = new Rect(Bounds.Size).Deflate(0.5);
            var forme = new RoundedRect(r, 6);
            Color texte, discret, trait;
            if (principal)
            {
                Color c1 = P.Accent, c2 = P.Accent2;
                if (appui) { c1 = Habillage.Melanger(c1, Colors.Black, 0.15); c2 = Habillage.Melanger(c2, Colors.Black, 0.15); }
                else if (survol) { c1 = Habillage.Melanger(c1, Colors.White, 0.15); c2 = Habillage.Melanger(c2, Colors.White, 0.15); }
                ctx.DrawRectangle(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(c1, 0), new GradientStop(c2, 1) } }, null, forme);
                texte = Habillage.TexteSur(Habillage.Melanger(c1, c2, 0.5)); discret = Habillage.Alpha(texte, 220); trait = texte;
            }
            else
            {
                var fond = appui ? Habillage.Melanger(P.CarteSurvol, P.Fond, 0.4) : survol ? P.CarteSurvol : P.Carte;
                ctx.DrawRectangle(Habillage.Pinceau(fond), new Pen(Habillage.Pinceau(survol || IsFocused ? P.Accent : P.Bordure, survol || IsFocused ? 200 : 255), survol ? 1.6 : 1), forme);
                texte = P.Texte; discret = P.Discret; trait = P.Accent;
            }
            Icone(ctx, new Point(Bounds.Width / 2, 56), trait);
            var ft = new FormattedText(titre, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Affichage, FontStyle.Normal, FontWeight.SemiBold), 19, Habillage.Pinceau(texte))
            { MaxTextWidth = Math.Max(10, Bounds.Width - 24), TextAlignment = TextAlignment.Center, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
            ctx.DrawText(ft, new Point(12, 104));
            var fd = new FormattedText(detail, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Corps), 13.5, Habillage.Pinceau(discret))
            { MaxTextWidth = Math.Max(10, Bounds.Width - 40), TextAlignment = TextAlignment.Center, MaxTextHeight = Bounds.Height - 150 };
            ctx.DrawText(fd, new Point(20, 140));
            if (IsFocused) ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(principal ? texte : P.Accent, 160), 1, new DashStyle(new double[] { 2, 2 }, 0)), new RoundedRect(r.Deflate(4), 4));
        }

        /// <summary>Icônes dessinées (pas d'emoji ni de police d'icônes : même rendu partout).</summary>
        private void Icone(DrawingContext ctx, Point c, Color couleur)
        {
            var crayon = new Pen(Habillage.Pinceau(couleur), 2.6, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round);
            if (icone == "nettoyer")
            {
                // étincelles : une grande étoile à quatre branches et deux petites
                Etoile(ctx, c, 22, couleur);
                Etoile(ctx, new Point(c.X + 24, c.Y - 18), 8, couleur);
                Etoile(ctx, new Point(c.X - 24, c.Y + 16), 6, couleur);
            }
            else if (icone == "verifier")
            {
                // bouclier avec une coche
                var g = new StreamGeometry();
                using (var x = g.Open())
                {
                    x.BeginFigure(new Point(c.X, c.Y - 24), false);
                    x.LineTo(new Point(c.X + 19, c.Y - 16)); x.LineTo(new Point(c.X + 19, c.Y + 1));
                    x.QuadraticBezierTo(new Point(c.X + 17, c.Y + 18), new Point(c.X, c.Y + 25));
                    x.QuadraticBezierTo(new Point(c.X - 17, c.Y + 18), new Point(c.X - 19, c.Y + 1));
                    x.LineTo(new Point(c.X - 19, c.Y - 16)); x.LineTo(new Point(c.X, c.Y - 24));
                    x.EndFigure(true);
                }
                ctx.DrawGeometry(null, crayon, g);
                ctx.DrawLine(crayon, new Point(c.X - 8, c.Y), new Point(c.X - 2, c.Y + 7));
                ctx.DrawLine(crayon, new Point(c.X - 2, c.Y + 7), new Point(c.X + 10, c.Y - 7));
            }
            else
            {
                // deux personnes : un proche qui aide
                ctx.DrawEllipse(null, crayon, new Point(c.X - 10, c.Y - 12), 8, 8);
                ctx.DrawEllipse(null, crayon, new Point(c.X + 13, c.Y - 8), 6.5, 6.5);
                var g = new StreamGeometry();
                using (var x = g.Open()) { x.BeginFigure(new Point(c.X - 26, c.Y + 20), false); x.QuadraticBezierTo(new Point(c.X - 10, c.Y - 6), new Point(c.X + 6, c.Y + 20)); x.EndFigure(false); }
                ctx.DrawGeometry(null, crayon, g);
                var g2 = new StreamGeometry();
                using (var x = g2.Open()) { x.BeginFigure(new Point(c.X + 4, c.Y + 8), false); x.QuadraticBezierTo(new Point(c.X + 14, c.Y - 2), new Point(c.X + 26, c.Y + 18)); x.EndFigure(false); }
                ctx.DrawGeometry(null, crayon, g2);
            }
        }

        private static void Etoile(DrawingContext ctx, Point c, double r, Color couleur)
        {
            var g = new StreamGeometry();
            double i = r * 0.28;
            using (var x = g.Open())
            {
                x.BeginFigure(new Point(c.X, c.Y - r), true);
                x.QuadraticBezierTo(new Point(c.X + i, c.Y - i), new Point(c.X + r, c.Y));
                x.QuadraticBezierTo(new Point(c.X + i, c.Y + i), new Point(c.X, c.Y + r));
                x.QuadraticBezierTo(new Point(c.X - i, c.Y + i), new Point(c.X - r, c.Y));
                x.QuadraticBezierTo(new Point(c.X - i, c.Y - i), new Point(c.X, c.Y - r));
                x.EndFigure(true);
            }
            ctx.DrawGeometry(Habillage.Pinceau(couleur), null, g);
        }
    }
}
