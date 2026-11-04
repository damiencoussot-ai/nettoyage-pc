// Velox 5 - extras du profil Gamer : bannière « synthwave » animée (soleil néon, sol quadrillé qui défile, titre
// glitch), cinématique « BOOST » quand le mode jeu est lancé, et annonces « SUCCÈS DÉBLOQUÉ » / « NIVEAU SUPÉRIEUR »
// façon console. Les animations s'arrêtent quand la fenêtre est réduite ou en arrière-plan (horloge commune).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    /// <summary>Paysage néon : ciel violet, soleil rayé, sol quadrillé en perspective qui avance.
    /// Pinceaux préparés une fois, et 60 images par seconde au plus (même sur un écran 144 Hz) : léger pour le PC.</summary>
    internal sealed class FondSynthwave : Control
    {
        private double decalage;
        private long derniere = -1000;
        private readonly Action<long> ambiance;
        /// <summary>Position du soleil (0 = à gauche, 1 = à droite).</summary>
        public double Soleil = 0.62;

        private static Color C(int r, int g, int b, int a = 255) { return Color.FromArgb((byte)a, (byte)r, (byte)g, (byte)b); }
        private static readonly IBrush Ciel = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(C(10, 4, 26), 0), new GradientStop(C(48, 10, 72), 0.85), new GradientStop(C(90, 14, 96), 1) },
        }.ToImmutable();
        private static readonly IBrush Halo = new RadialGradientBrush { GradientStops = { new GradientStop(C(255, 43, 214, 70), 0), new GradientStop(C(255, 43, 214, 0), 1) } }.ToImmutable();
        private static readonly IBrush Astre = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(C(255, 214, 92), 0), new GradientStop(C(255, 92, 140), 0.55), new GradientStop(C(255, 43, 214), 1) },
        }.ToImmutable();
        private static readonly IBrush Bande = new SolidColorBrush(C(48, 10, 72)).ToImmutable();
        private static readonly IBrush Sol = new SolidColorBrush(C(8, 2, 20)).ToImmutable();
        private static readonly IBrush Horizon = new SolidColorBrush(C(255, 120, 230, 220)).ToImmutable();
        private static readonly IBrush Lueur = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = { new GradientStop(C(255, 43, 214, 90), 0), new GradientStop(C(255, 43, 214, 0), 1) },
        }.ToImmutable();
        private static readonly IBrush Voile = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops = { new GradientStop(C(6, 2, 16, 232), 0), new GradientStop(C(6, 2, 16, 150), 0.42), new GradientStop(C(6, 2, 16, 0), 0.62) },
        }.ToImmutable();
        // crayons : 16 niveaux de transparence et d'épaisseur pour les lignes qui avancent
        private static readonly IPen[] Verticales = new IPen[15], Horizontales = new IPen[16];
        static FondSynthwave()
        {
            for (int i = 0; i < 15; i++) Verticales[i] = new ImmutablePen(new SolidColorBrush(C(255, 43, 214, Math.Max(40, 150 - i * 6))).ToImmutable(), 1);
            for (int k = 0; k < 16; k++) { double z = k / 15.0; Horizontales[k] = new ImmutablePen(new SolidColorBrush(C(255, 43, 214, (int)(30 + 200 * z))).ToImmutable(), 1 + z); }
        }

        public FondSynthwave()
        {
            ClipToBounds = true;
            ambiance = t =>
            {
                if (t - derniere < 16) return;
                derniere = t; decalage = (t % 1400) / 1400.0; InvalidateVisual();
            };
            AttachedToVisualTree += (s, e) => Horloge.Ambiance(ambiance);
            DetachedFromVisualTree += (s, e) => Horloge.RetirerAmbiance(ambiance);
        }

        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w < 10 || h < 10) return;
            double horizon = h * 0.58;
            ctx.DrawRectangle(Ciel, null, new Rect(0, 0, w, horizon));
            double cx = w * Soleil, r = Math.Min(h * 0.46, 120);
            using (ctx.PushClip(new Rect(0, 0, w, horizon)))
            {
                ctx.DrawEllipse(Halo, null, new Point(cx, horizon), r * 1.9, r * 1.4);
                ctx.DrawEllipse(Astre, null, new Point(cx, horizon), r, r);
                for (int k = 0; k < 6; k++)
                {
                    double y = horizon - r * 0.08 - k * r * 0.13, e = 1.5 + (5 - k) * 0.9;
                    ctx.DrawRectangle(Bande, null, new Rect(cx - r, y - e, 2 * r, e));
                }
            }
            ctx.DrawRectangle(Sol, null, new Rect(0, horizon, w, h - horizon));
            double vx = w * 0.5;
            for (int i = -14; i <= 14; i++)
                ctx.DrawLine(Verticales[Math.Abs(i)], new Point(vx + i * w / 60.0, horizon), new Point(vx + i * w / 9.0, h));
            for (int k = 0; k < 9; k++)
            {
                double z = (k + decalage) / 9.0, y = horizon + (h - horizon) * z * z;
                ctx.DrawLine(Horizontales[Math.Min(15, (int)(z * 15))], new Point(0, y), new Point(w, y));
            }
            ctx.DrawRectangle(Horizon, null, new Rect(0, horizon - 1, w, 2));
            ctx.DrawRectangle(Lueur, null, new Rect(0, horizon, w, 26));
            ctx.DrawRectangle(Voile, null, new Rect(0, 0, w, h));
        }
    }

    internal static class Gamer5
    {
        private static Color Magenta { get { return Color.FromRgb(255, 43, 214); } }
        private static Color Cyan { get { return Color.FromRgb(34, 230, 255); } }

        public static bool ModeJeuActif()
        {
            try { return File.Exists(Chemins.Fichier("modejeu.json")); } catch { return false; }
        }

        /// <summary>Titre « glitch » : deux copies décalées (cyan et magenta) qui tremblent de temps en temps.</summary>
        private static Control TitreGlitch(string texte, double taille)
        {
            var cyan = new TranslateTransform(-2, 0); var mag = new TranslateTransform(2, 0);
            Func<Color, TranslateTransform, TextBlock> copie = (c, tr) => new TextBlock { Text = texte, FontFamily = Habillage.Affichage, FontSize = taille, FontWeight = FontWeight.Bold, LetterSpacing = 2, Foreground = new SolidColorBrush(Color.FromArgb(200, c.R, c.G, c.B)), RenderTransform = tr };
            var p = new Panel();
            p.Children.Add(copie(Cyan, cyan)); p.Children.Add(copie(Magenta, mag));
            p.Children.Add(new TextBlock { Text = texte, FontFamily = Habillage.Affichage, FontSize = taille, FontWeight = FontWeight.Bold, LetterSpacing = 2, Foreground = Brushes.White });
            var hasard = new Random();
            Action<long> pas = t =>
            {
                long cycle = t % 3600;
                if (cycle < 160) { cyan.X = -2 - hasard.Next(0, 7); cyan.Y = hasard.Next(-2, 3); mag.X = 2 + hasard.Next(0, 7); mag.Y = hasard.Next(-2, 3); }
                else if (cyan.X != -2 || mag.X != 2) { cyan.X = -2; cyan.Y = 0; mag.X = 2; mag.Y = 0; }
            };
            p.AttachedToVisualTree += (s, e) => Horloge.Ambiance(pas);
            p.DetachedFromVisualTree += (s, e) => Horloge.RetirerAmbiance(pas);
            return p;
        }

        /// <summary>Bannière du QG GAMER (haut de l'accueil) : paysage néon, titre glitch, rang, et BOOST.</summary>
        public static Control Banniere(FenetrePrincipale f)
        {
            var cadre = new Cadre5 { Coupe = 22, Height = 168, BorderBrush = new SolidColorBrush(Color.FromArgb(160, 255, 43, 214)), BorderThickness = new Thickness(1), ClipToBounds = true };
            var g = new Panel();
            g.Children.Add(new FondSynthwave());
            var contenu = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(26, 18, 26, 18) };
            var gauche = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
            gauche.Children.Add(new TextBlock { Text = "▸ " + Langue.T("MODE GAMER ACTIVÉ") + "  ·  " + Langue.T("SYSTÈME PRÊT"), FontFamily = Habillage.Console, FontSize = 11, LetterSpacing = 2, Foreground = new SolidColorBrush(Cyan) });
            gauche.Children.Add(TitreGlitch("VELOX // GAMER", 40));
            var rang = new TextBlock { Text = Langue.T("Calcul de ton rang…"), FontFamily = Habillage.Console, FontSize = 12, LetterSpacing = 1.5, Foreground = new SolidColorBrush(Color.FromRgb(255, 170, 238)) };
            gauche.Children.Add(rang);
            contenu.Children.Add(gauche);
            var boost = new BoutonNoyau(ModeJeuActif() ? "Mode jeu actif" : "Boost", true, 190, 56) { TaillePolice = 19, Capitales = true, VerticalAlignment = VerticalAlignment.Center };
            boost.Clic += () => Boost(f, () => boost.Changer(ModeJeuActif() ? "Mode jeu actif" : "Boost"));
            ToolTip.SetTip(boost, Langue.T("Lance le mode jeu : alimentation « Performances élevées » et mémoire vive libérée. Rien n'est fermé."));
            // halo qui pulse derrière BOOST (invite à cliquer), 30 images par seconde
            var halo = new Border { Margin = new Thickness(6), Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)), BoxShadow = BoxShadows.Parse("0 0 28 6 #FF2BD6"), IsHitTestVisible = false };
            var zoneBoost = new Panel { VerticalAlignment = VerticalAlignment.Center, Children = { halo, boost } };
            long derniere = -1000;
            bool actifPouls = ModeJeuActif(); long lu = 0;
            Action<long> pouls = t => { if (t - derniere < 33) return; derniere = t; if (t - lu > 2000) { lu = t; actifPouls = ModeJeuActif(); } halo.Opacity = actifPouls ? 0.15 : 0.35 + 0.45 * (0.5 + 0.5 * Math.Sin(t / 420.0)); };
            zoneBoost.AttachedToVisualTree += (s, e) => Horloge.Ambiance(pouls);
            zoneBoost.DetachedFromVisualTree += (s, e) => Horloge.RetirerAmbiance(pouls);
            Grid.SetColumn(zoneBoost, 1); contenu.Children.Add(zoneBoost);
            g.Children.Add(contenu);
            cadre.Child = g;
            Survol.Lumineux(cadre, Magenta, 160, 255);
            Action<ProgressionJeu> afficher = p => rang.Text = Langue.F("RANG : {0}  ·  NIV. {1}  ·  {2} XP  ·  {3}/{4} SUCCÈS", Langue.T(p.Titre).ToUpper(Langue.Culture), p.Niveau, p.Xp, p.Succes.Count(x => x.Obtenu), p.Succes.Count);
            if (QgGamer5.Exemple != null) afficher(QgGamer5.Exemple);
            else Task.Run(() => Succes.Calculer()).ContinueWith(t => { if (!t.IsFaulted) Dispatcher.UIThread.Post(() => afficher(t.Result)); });
            return cadre;
        }

        /// <summary>Lance (ou termine) le mode jeu ; s'il est bien lancé, la cinématique « BOOST » se joue.</summary>
        public static void Boost(FenetrePrincipale f, Action apres)
        {
            bool actif = ModeJeuActif();
            DateTime? debut = actif ? SessionJeu5.Debut() : null;
            f.LancerAction(actif ? "finmodejeu" : "modejeu", () => Dispatcher.UIThread.Post(() =>
            {
                if (!actif && ModeJeuActif()) { SessionJeu5.Nouvelle(); JournalJeu.Noter("boost"); Cinematique(f); }
                else if (actif && !ModeJeuActif()) { try { SessionJeu5.Rapport(f, debut); } catch (Exception ex) { JournalDemarrage.Erreur("rapport de session", ex); } }
                if (apres != null) apres();
            }));
        }

        /// <summary>Cinématique plein écran : « BOOST ENGAGÉ », barre de chargement, ce qui a été fait. Un clic la ferme.</summary>
        public static void Cinematique(FenetrePrincipale f)
        {
            var calque = new Panel { Background = new SolidColorBrush(Color.FromArgb(215, 4, 2, 12)), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
            var fond = new FondSynthwave { Opacity = 0.35, Soleil = 0.5 };
            calque.Children.Add(fond);
            var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 14, Width = 560 };
            s.Children.Add(new TextBlock { Text = "> " + Langue.T("INITIALISATION DU MODE JEU"), FontFamily = Habillage.Console, FontSize = 13, LetterSpacing = 2, Foreground = new SolidColorBrush(Cyan), HorizontalAlignment = HorizontalAlignment.Center });
            var echelle = new ScaleTransform(1.6, 1.6);
            var titre = TitreGlitch(Langue.T("BOOST ENGAGÉ"), 64);
            titre.HorizontalAlignment = HorizontalAlignment.Center; titre.RenderTransform = echelle;
            s.Children.Add(titre);
            var piste = new Border { Height = 10, Background = new SolidColorBrush(Color.FromArgb(40, 255, 43, 214)), BorderBrush = new SolidColorBrush(Color.FromArgb(140, 255, 43, 214)), BorderThickness = new Thickness(1) };
            var barre = new Border { HorizontalAlignment = HorizontalAlignment.Left, Width = 0, Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = { new GradientStop(Cyan, 0), new GradientStop(Color.FromRgb(124, 92, 255), 0.5), new GradientStop(Magenta, 1) } } };
            piste.Child = barre;
            s.Children.Add(piste);
            var pourcent = new TextBlock { Text = "0 %", FontFamily = Habillage.Console, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(255, 170, 238)), HorizontalAlignment = HorizontalAlignment.Right };
            s.Children.Add(pourcent);
            string[] lignes =
            {
                "Alimentation : performances élevées",
                "Mémoire vive libérée",
                "Applications gourmandes repérées (rien n'est fermé)",
                "Prêt. Bonne partie, GG !",
            };
            var textes = new List<TextBlock>();
            foreach (var l in lignes)
            {
                var t = new TextBlock { Text = "✔  " + Langue.T(l), FontFamily = Habillage.Console, FontSize = 14, Foreground = new SolidColorBrush(Color.FromRgb(61, 255, 162)), Opacity = 0 };
                textes.Add(t); s.Children.Add(t);
            }
            s.Children.Add(new TextBlock { Text = Langue.T("Clique n'importe où pour fermer · « Terminer le mode jeu » après ta partie"), FontFamily = Habillage.Corps, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(150, 140, 190)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
            calque.Children.Add(s);
            bool ferme = false;
            Action fermer = () =>
            {
                if (ferme) return; ferme = true;
                if (!Habillage.Animations) { f.RetirerCalque(calque); return; }
                long t1 = Horloge.Maintenant;
                Horloge.Lancer(t => { double k = Math.Min(1, (t - t1) / 260.0); calque.Opacity = 1 - k; if (k >= 1) f.RetirerCalque(calque); return k < 1; });
            };
            calque.PointerReleased += (o, e) => fermer();
            f.AjouterCalque(calque);
            Sons5.Boost();
            if (!Habillage.Animations)
            {
                // captures : l'état final, sans fermeture automatique
                echelle.ScaleX = echelle.ScaleY = 1; barre.Width = double.NaN; barre.HorizontalAlignment = HorizontalAlignment.Stretch;
                pourcent.Text = "100 %"; foreach (var x in textes) x.Opacity = 1;
                return;
            }
            long t0 = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double ms = t - t0;
                double k = Math.Min(1, ms / 1700.0);
                double e = Habillage.Douce(Math.Min(1, ms / 450.0));
                echelle.ScaleX = echelle.ScaleY = 1.6 - 0.6 * e;
                barre.Width = Habillage.Douce(k) * Math.Max(0, piste.Bounds.Width - 2);
                pourcent.Text = Math.Round(Habillage.Douce(k) * 100) + " %";
                for (int i = 0; i < textes.Count; i++) textes[i].Opacity = Math.Max(0, Math.Min(1, (ms - 500 - i * 300) / 200.0));
                if (ms > 5200) { fermer(); return false; }
                return !ferme;
            });
        }

        // ------------------------------------------------------------ succès débloqués et niveau supérieur

        /// <summary>Compare avec ce qui a déjà été annoncé et montre les nouveautés (au plus 3 à la fois).</summary>
        public static void AnnoncerNouveautes(FenetrePrincipale f, ProgressionJeu p)
        {
            if (QgGamer5.Exemple != null) return;
            Parametres r;
            try { r = Parametres.Lire(); } catch { return; }
            var obtenus = p.Succes.Where(x => x.Obtenu).Select(x => x.Nom).ToList();
            string niveau = "niv" + p.Niveau;
            string vus = r.SuccesVus;
            var annonces = new List<Tuple<string, string, string>>();
            if (vus == null)
            {
                // première fois : un seul message de bienvenue avec le bilan
                if (obtenus.Count > 0) annonces.Add(Tuple.Create(Langue.T("BIENVENUE AU QG"), Langue.F("{0} succès déjà débloqués", obtenus.Count), Langue.F("Niveau {0} · {1}", p.Niveau, Langue.T(p.Titre))));
            }
            else
            {
                var deja = new HashSet<string>(vus.Split('|'));
                foreach (var n in obtenus.Where(x => !deja.Contains(x)).Take(3))
                {
                    var su = p.Succes.First(x => x.Nom == n);
                    annonces.Add(Tuple.Create(Langue.T("SUCCÈS DÉBLOQUÉ"), Langue.T(su.Nom), Langue.T(Progression5.NomRarete(su.Rarete)) + " · +" + su.Xp + " XP · " + Langue.T(su.Condition)));
                }
                // niveau supérieur : animation plein écran (avec le nouveau rang s'il change)
                var ancien = deja.Where(x => x.StartsWith("niv")).Select(x => { int v; return int.TryParse(x.Substring(3), out v) ? v : 0; }).DefaultIfEmpty(0).Max();
                if (ancien > 0 && p.Niveau > ancien)
                {
                    bool nouveauRang = RangsJeu.Rang(ancien).Palier != p.Rang.Palier;
                    DispatcherTimer.RunOnce(() => Progression5.NiveauSuperieur(f, p, nouveauRang), TimeSpan.FromMilliseconds(400));
                }
            }
            string nouveau = string.Join("|", obtenus.Concat(new[] { niveau }));
            if (nouveau != vus) f.EnregistrerReglage(x => x.SuccesVus = nouveau);
            for (int i = 0; i < annonces.Count; i++) { var a = annonces[i]; int d = i; DispatcherTimer.RunOnce(() => Trophee(f, a.Item1, a.Item2, a.Item3), TimeSpan.FromMilliseconds(900 + d * 1400)); }
        }

        /// <summary>Annonce façon console, en bas à droite : glisse, reste 4,5 secondes, repart.</summary>
        public static void Trophee(FenetrePrincipale f, string entete, string titre, string detail, bool fixe = false)
        {
            var decal = new TranslateTransform(420, 0);
            var icone = new Border { Width = 46, Height = 46, Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Cyan, 0), new GradientStop(Magenta, 1) } }, Child = new TextBlock { Text = "◆", FontSize = 24, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            var textes = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 280 };
            textes.Children.Add(new TextBlock { Text = entete, FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 2, Foreground = new SolidColorBrush(Magenta) });
            textes.Children.Add(new TextBlock { Text = titre, FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis });
            textes.Children.Add(new TextBlock { Text = detail, FontFamily = Habillage.Corps, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(190, 180, 220)), TextTrimming = TextTrimming.CharacterEllipsis });
            var ligne = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14, Children = { icone, textes } };
            var carte = new Cadre5
            {
                Child = ligne, Coupe = 12, Padding = new Thickness(14, 12, 20, 12), Background = new SolidColorBrush(Color.FromArgb(245, 14, 6, 30)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(200, 255, 43, 214)), BorderThickness = new Thickness(1), TraitHaut = Magenta,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 24, 46),
                RenderTransform = decal, IsHitTestVisible = false,
            };
            f.AjouterCalque(carte);
            if (!Habillage.Animations || fixe) { decal.X = 0; return; }
            if (entete == Langue.T("NIVEAU SUPÉRIEUR")) Sons5.Niveau(); else Sons5.Succes();
            long t0 = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double ms = t - t0;
                if (ms < 380) decal.X = 420 * (1 - Habillage.Douce(ms / 380.0));
                else if (ms < 4900) decal.X = 0;
                else if (ms < 5300) decal.X = 420 * Habillage.Douce((ms - 4900) / 400.0);
                else { f.RetirerCalque(carte); return false; }
                return true;
            });
        }
    }
}
