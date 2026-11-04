// Velox 5 - bouton du thème Noyau : principal (dégradé lumineux, reflet qui le traverse au survol)
// ou secondaire (fond de carte, liseré qui s'allume au survol). Clavier : Entrée ou Espace.
using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    public sealed class BoutonNoyau : Control
    {
        public string Texte;
        public bool Principal;
        public double TaillePolice = 14;
        /// <summary>Style HUD : texte en capitales, police d'affichage (boutons des alertes, « Analyser »).</summary>
        public bool Capitales;
        /// <summary>Icône dessinée à la place du texte : "soleil" ou "lune" (pas d'emoji : même rendu partout).</summary>
        public string Icone;
        public event Action Clic;

        private double survol;          // 0 à 1, glisse en douceur
        private double cibleSurvol;
        private double reflet = -1;     // progression du reflet (-1 : aucun)
        private bool appui;

        public BoutonNoyau(string texte, bool principal, double largeur, double hauteur)
        {
            Texte = Langue.T(texte); Principal = principal; Height = hauteur;
            largeurPrevue = largeur;
            if (largeur > 0) Width = largeur;   // sinon : largeur du texte (MeasureOverride)
            Focusable = true; Cursor = new Cursor(StandardCursorType.Hand);
        }

        // largeur demandée : un minimum, élargi si le texte (traduit : souvent plus long en anglais ou en espagnol) ne tient pas
        private readonly double largeurPrevue;

        /// <summary>Change le texte du bouton (sa largeur suit si elle est automatique).</summary>
        public void Changer(string texte) { texte = Langue.T(texte); if (texte == Texte) return; Texte = texte; Ajuster(); InvalidateMeasure(); InvalidateVisual(); }

        private double LargeurTexte()
        {
            var ft = new FormattedText(TexteAffiche, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Police, TaillePolice, Brushes.White);
            return Math.Ceiling(ft.WidthIncludingTrailingWhitespace + 2 * Math.Max(18, Height * 0.5));
        }

        private bool Hud5 { get { return Habillage.Hud && (Principal || Capitales); } }
        private string TexteAffiche { get { return Hud5 ? (Texte ?? "").ToUpper(CultureInfo.CurrentCulture) : (Texte ?? ""); } }
        private Typeface Police { get { return Hud5 ? new Typeface(Habillage.Affichage, FontStyle.Normal, Principal ? FontWeight.Bold : FontWeight.SemiBold) : new Typeface(Habillage.Corps, FontStyle.Normal, Principal ? FontWeight.SemiBold : FontWeight.Normal); } }

        private void Ajuster()
        {
            if (largeurPrevue <= 0 || Icone != null) return;
            try { Width = Math.Max(largeurPrevue, LargeurTexte()); } catch { }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            Ajuster();   // ici : la taille de police (réglée après le constructeur) est connue
        }

        protected override Size MeasureOverride(Size disponible)
        {
            if (!double.IsNaN(Width) || Icone != null) return base.MeasureOverride(disponible);
            return new Size(Math.Min(disponible.Width, LargeurTexte()), Height);
        }

        protected override void OnPointerEntered(PointerEventArgs e)
        {
            base.OnPointerEntered(e);
            VersSurvol(1);
            if (Principal && reflet < 0 && Habillage.Animations)
            {
                long debut = Horloge.Maintenant;
                reflet = 0;
                Horloge.Lancer(t => { reflet = (t - debut) / 650.0; if (reflet >= 1) reflet = -1; InvalidateVisual(); return reflet >= 0; });
            }
        }

        protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); appui = false; VersSurvol(0); }
        protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); appui = true; InvalidateVisual(); e.Handled = true; }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            bool dedans = new Rect(Bounds.Size).Contains(e.GetPosition(this));
            bool declencher = appui && dedans;
            appui = false; InvalidateVisual();
            if (declencher) { var c = Clic; if (c != null) c(); }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; var c = Clic; if (c != null) c(); }
        }

        protected override void OnGotFocus(GotFocusEventArgs e) { base.OnGotFocus(e); InvalidateVisual(); }
        protected override void OnLostFocus(Avalonia.Interactivity.RoutedEventArgs e) { base.OnLostFocus(e); InvalidateVisual(); }

        private void VersSurvol(double v)
        {
            cibleSurvol = v;
            if (!Habillage.Animations) { survol = v; InvalidateVisual(); return; }
            double depart = survol; long debut = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double p = Math.Min(1, (t - debut) / 140.0);
                survol = depart + (cibleSurvol - depart) * Habillage.Douce(p);
                InvalidateVisual();
                return p < 1 && Math.Abs(cibleSurvol - v) < 0.001;
            });
        }

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            var r = new Rect(Bounds.Size).Deflate(0.5);
            double rayon = Math.Min(10, Bounds.Height / 2 - 1);
            // HUD : coins coupés ; classique : coins arrondis
            bool hud = Habillage.Hud;
            var arrondi = new RoundedRect(r, rayon);
            var forme = hud ? Habillage.Chanfrein(r, Math.Min(12, Bounds.Height * 0.3)) : null;
            Action<IBrush, IPen> dessiner = (b, c) => { if (hud) ctx.DrawGeometry(b, c, forme); else ctx.DrawRectangle(b, c, arrondi); };
            Color texte;
            if (!IsEffectivelyEnabled)
            {
                dessiner(Habillage.Pinceau(P.Bordure), null);
                texte = P.Discret;
            }
            else if (Principal)
            {
                Color c1 = P.Accent, c2 = P.Accent2;
                if (appui) { c1 = Habillage.Melanger(c1, Colors.Black, 0.15); c2 = Habillage.Melanger(c2, Colors.Black, 0.15); }
                else if (survol > 0) { c1 = Habillage.Melanger(c1, Colors.White, 0.18 * survol); c2 = Habillage.Melanger(c2, Colors.White, 0.18 * survol); }
                IBrush fond = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(c1, 0), new GradientStop(c2, 1) },
                };
                if (Habillage.Hud)
                {
                    // HUD : aplat lumineux et halo autour (deux contours translucides)
                    fond = Habillage.Pinceau(c1);
                    if (P.Sombre)
                    {
                        ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(c1, (int)(40 + 50 * survol)), 7), forme);
                        ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(c1, (int)(70 + 60 * survol)), 3), forme);
                    }
                }
                dessiner(fond, null);
                if (reflet >= 0)
                {
                    double bande = Math.Max(36, Bounds.Height * 1.5), x = -bande + Habillage.Douce(reflet) * (Bounds.Width + 2 * bande);
                    var lumiere = new LinearGradientBrush
                    {
                        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0.3, RelativeUnit.Relative),
                        GradientStops = { new GradientStop(Habillage.Alpha(Colors.White, 0), 0), new GradientStop(Habillage.Alpha(Colors.White, P.Sombre ? 110 : 90), 0.5), new GradientStop(Habillage.Alpha(Colors.White, 0), 1) },
                    };
                    using (hud ? ctx.PushGeometryClip(forme) : ctx.PushClip(arrondi)) ctx.DrawRectangle(lumiere, null, new Rect(x - bande / 2, 0, bande, Bounds.Height));
                }
                texte = Habillage.TexteSur(Habillage.Melanger(c1, c2, 0.5));
            }
            else
            {
                var fond = appui ? Habillage.Melanger(P.CarteSurvol, P.Fond, 0.4) : Habillage.Melanger(P.Carte, P.CarteSurvol, survol);
                var bord = Habillage.Melanger(P.Bordure, P.Accent, survol * 0.8);
                dessiner(Habillage.Pinceau(hud ? Habillage.Alpha(fond, P.Sombre ? 200 : 255) : fond), new Pen(Habillage.Pinceau(hud && P.Sombre ? Habillage.Melanger(Habillage.Alpha(P.Accent, 110), P.Accent, survol) : bord), 1));
                texte = Habillage.Hud && Capitales && Bounds.Height <= 34 ? P.Accent : P.Texte;
            }
            if (Icone != null)
            {
                var c = new Point(Bounds.Width / 2, Bounds.Height / 2);
                var crayon = new Pen(Habillage.Pinceau(texte), 1.6, lineCap: PenLineCap.Round);
                if (Icone == "soleil")
                {
                    ctx.DrawEllipse(null, crayon, c, 4.2, 4.2);
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        ctx.DrawLine(crayon, new Point(c.X + 7 * Math.Cos(a), c.Y + 7 * Math.Sin(a)), new Point(c.X + 9.5 * Math.Cos(a), c.Y + 9.5 * Math.Sin(a)));
                    }
                }
                else
                {
                    var lune = new CombinedGeometry(GeometryCombineMode.Exclude,
                        new EllipseGeometry(new Rect(c.X - 7.5, c.Y - 7.5, 15, 15)), new EllipseGeometry(new Rect(c.X - 3, c.Y - 11, 15, 15)));
                    ctx.DrawGeometry(Habillage.Pinceau(texte), null, lune);
                }
                if (IsFocused) ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(P.Accent, 160), 1, new DashStyle(new double[] { 2, 2 }, 0)), new RoundedRect(r.Deflate(3), Math.Max(1, rayon - 2)));
                return;
            }
            var ft = new FormattedText(TexteAffiche, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Police, TaillePolice, Habillage.Pinceau(texte))
            { MaxTextWidth = Math.Max(10, Bounds.Width - 16), Trimming = TextTrimming.CharacterEllipsis, MaxLineCount = 1 };
            ctx.DrawText(ft, new Point((Bounds.Width - ft.Width) / 2, (Bounds.Height - ft.Height) / 2));
            if (IsFocused) ctx.DrawRectangle(null, new Pen(Habillage.Pinceau(P.Accent, 160), 1, new DashStyle(new double[] { 2, 2 }, 0)), new RoundedRect(r.Deflate(3), Math.Max(1, rayon - 2)));
        }
    }
}
