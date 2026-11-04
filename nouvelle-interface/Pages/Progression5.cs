// Velox 5 - progression du profil Gamer : emblème de rang (Bronze à Légende), couleurs de rareté, défis du jour et
// série, carte de joueur à partager (image PNG), animation plein écran « NIVEAU SUPÉRIEUR ».
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace NettoyagePC.Nouveau
{
    internal static class Progression5
    {
        private static Color C(int r, int g, int b) { return Color.FromRgb((byte)r, (byte)g, (byte)b); }

        /// <summary>Couleurs du rang (clair, foncé) : bronze, argent, or, platine, diamant, légende.</summary>
        public static Color[] CouleursRang(int palier)
        {
            switch (palier)
            {
                case 0: return new[] { C(232, 160, 96), C(122, 66, 28) };
                case 1: return new[] { C(236, 242, 250), C(118, 130, 152) };
                case 2: return new[] { C(255, 222, 102), C(184, 120, 12) };
                case 3: return new[] { C(150, 255, 240), C(36, 140, 160) };
                case 4: return new[] { C(140, 210, 255), C(124, 72, 255) };
                default: return new[] { C(255, 190, 70), C(255, 43, 214) };
            }
        }

        public static Color CouleurRarete(Rarete r)
        {
            switch (r)
            {
                case Rarete.Rare: return C(60, 170, 255);
                case Rarete.Epique: return C(170, 90, 255);
                case Rarete.Legendaire: return C(255, 176, 40);
                default: return C(150, 165, 180);
            }
        }

        public static string NomRarete(Rarete r)
        {
            return r == Rarete.Legendaire ? "LÉGENDAIRE" : r == Rarete.Epique ? "ÉPIQUE" : r == Rarete.Rare ? "RARE" : "COMMUN";
        }

        // ------------------------------------------------------------ défis du jour et série

        /// <summary>Bandeau « DÉFIS DU JOUR » : trois défis cochés ou non, et la série de jours.</summary>
        public static Control Defis(ProgressionJeu p)
        {
            var P = Habillage.P;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,Auto"), Margin = new Thickness(0, 14, 0, 0) };
            int faits = p.DefisDuJour.Count(x => x.Fait);
            for (int i = 0; i < p.DefisDuJour.Count && i < 3; i++)
            {
                var d = p.DefisDuJour[i];
                var s = new StackPanel { Spacing = 3 };
                s.Children.Add(new TextBlock { Text = (i == 0 ? Langue.F("DÉFIS DU JOUR · {0}/{1}", faits, p.DefisDuJour.Count) : " "), FontFamily = Habillage.Console, FontSize = 10, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Accent, 200) });
                var ligne = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                ligne.Children.Add(new Border { Width = 16, Height = 16, BorderThickness = new Thickness(1.5), BorderBrush = Habillage.Pinceau(d.Fait ? P.Vert : P.Discret), Background = d.Fait ? Habillage.Pinceau(P.Vert, 50) : Brushes.Transparent, VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = d.Fait ? "✔" : "", FontSize = 11, Foreground = Habillage.Pinceau(P.Vert), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } });
                ligne.Children.Add(new TextBlock { Text = Langue.T(d.Nom), FontFamily = Habillage.Corps, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(d.Fait ? P.Discret : P.Texte), TextDecorations = d.Fait ? TextDecorations.Strikethrough : null, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                ligne.Children.Add(new TextBlock { Text = "+" + d.Xp + " XP", FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(d.Fait ? P.Vert : P.Accent2), VerticalAlignment = VerticalAlignment.Center });
                s.Children.Add(ligne);
                Grid.SetColumn(s, i); g.Children.Add(s);
            }
            // série
            var serie = new StackPanel { Spacing = 0, HorizontalAlignment = HorizontalAlignment.Right, MinWidth = 160 };
            serie.Children.Add(new TextBlock { Text = Langue.T("SÉRIE"), FontFamily = Habillage.Console, FontSize = 10, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Orange), HorizontalAlignment = HorizontalAlignment.Right });
            var n = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
            n.Children.Add(new Flamme { Width = 18, Height = 24, VerticalAlignment = VerticalAlignment.Center, Allumee = p.Serie > 0 });
            n.Children.Add(new TextBlock { Text = Langue.F(p.Serie == 1 ? "{0} jour" : "{0} jours", p.Serie), FontFamily = Habillage.Affichage, FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(p.Serie > 0 ? P.Orange : P.Discret) });
            serie.Children.Add(n);
            serie.Children.Add(new TextBlock { Text = Langue.F("record : {0}", p.MeilleureSerie), FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(P.Discret), HorizontalAlignment = HorizontalAlignment.Right });
            ToolTip.SetTip(serie, Langue.T("Ouvre Velox ou fais un nettoyage chaque jour pour garder ta série : +5 XP par jour."));
            Grid.SetColumn(serie, 3); g.Children.Add(serie);
            var cadre = new Border { Child = g, BorderBrush = Habillage.Pinceau(P.Accent, 45), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 4, 0, 0) };
            ToolTip.SetTip(g.Children[0], Langue.T("Trois nouveaux défis chaque jour : +30 XP chacun, +50 XP si tu réussis les trois."));
            return cadre;
        }

        // ------------------------------------------------------------ carte de joueur

        /// <summary>Carte à partager (Discord, réseaux) : pseudo, rang, niveau, XP, série, succès et matériel.</summary>
        public static Control Carte(ProgressionJeu p, string pseudo, string cpu, string gpu)
        {
            var cr = CouleursRang(p.Rang.Palier);
            var racine = new Panel { Width = 960, Height = 500, ClipToBounds = true };
            racine.Children.Add(new FondSynthwave { Soleil = 0.82 });
            racine.Children.Add(new Border { Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.FromArgb(235, 6, 2, 16), 0), new GradientStop(Color.FromArgb(120, 6, 2, 16), 0.65), new GradientStop(Color.FromArgb(0, 6, 2, 16), 1) } } });
            racine.Children.Add(new Border { BorderBrush = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.FromRgb(34, 230, 255), 0), new GradientStop(Color.FromRgb(255, 43, 214), 1) } }, BorderThickness = new Thickness(3) });
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), Margin = new Thickness(40, 36, 40, 30) };
            var gauche = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 36, 0) };
            gauche.Children.Add(new Embleme { Width = 190, Height = 210, Palier = p.Rang.Palier, Niveau = p.Niveau });
            gauche.Children.Add(new TextBlock { Text = Langue.T(p.Rang.Nom).ToUpper(Langue.Culture), FontFamily = Habillage.Affichage, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(cr[0]), HorizontalAlignment = HorizontalAlignment.Center, LetterSpacing = 3 });
            g.Children.Add(gauche);
            var d = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            var tete = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            try { tete.Children.Add(new Image { Source = new Bitmap(AssetLoader.Open(new Uri("avares://NettoyagePC/Assets/logo.png"))), Width = 30, Height = 30 }); } catch { }
            tete.Children.Add(new TextBlock { Text = "VELOX // " + Langue.T("CARTE DE JOUEUR"), FontFamily = Habillage.Console, FontSize = 14, LetterSpacing = 2, Foreground = new SolidColorBrush(Color.FromRgb(34, 230, 255)), VerticalAlignment = VerticalAlignment.Center });
            d.Children.Add(tete);
            d.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(pseudo) ? Langue.T("Joueur") : pseudo, FontFamily = Habillage.Affichage, FontSize = 52, FontWeight = FontWeight.Bold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis });
            d.Children.Add(new TextBlock { Text = Langue.F("NIVEAU {0}  ·  {1} XP", p.Niveau, p.Xp), FontFamily = Habillage.Console, FontSize = 18, LetterSpacing = 1.5, Foreground = new SolidColorBrush(Color.FromRgb(255, 170, 238)) });
            var barre = new Grid { Height = 12, Width = 520, HorizontalAlignment = HorizontalAlignment.Left };
            barre.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(50, 255, 43, 214)) });
            barre.Children.Add(new Border { HorizontalAlignment = HorizontalAlignment.Left, Width = 520 * Math.Max(0.02, Math.Min(1, p.Avancement)), Background = new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.FromRgb(34, 230, 255), 0), new GradientStop(Color.FromRgb(255, 43, 214), 1) } } });
            d.Children.Add(barre);
            var stats = new UniformGrid { Columns = 3, Margin = new Thickness(0, 10, 0, 0), Width = 560, HorizontalAlignment = HorizontalAlignment.Left };
            Action<string, string> stat = (t, v) =>
            {
                var s = new StackPanel();
                s.Children.Add(new TextBlock { Text = t, FontFamily = Habillage.Console, FontSize = 12, LetterSpacing = 1.5, Foreground = new SolidColorBrush(Color.FromRgb(170, 160, 210)) });
                s.Children.Add(new TextBlock { Text = v, FontFamily = Habillage.Affichage, FontSize = 30, FontWeight = FontWeight.Bold, Foreground = Brushes.White });
                stats.Children.Add(s);
            };
            stat(Langue.T("SUCCÈS"), p.Succes.Count(x => x.Obtenu) + " / " + p.Succes.Count);
            stat(Langue.T("SÉRIE RECORD"), Langue.F(p.MeilleureSerie == 1 ? "{0} jour" : "{0} jours", p.MeilleureSerie));
            stat(Langue.T("DÉFIS RÉUSSIS"), p.DefisFaits.ToString());
            d.Children.Add(stats);
            string mat = string.Join("  ·  ", new[] { cpu, gpu }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (mat != "") d.Children.Add(new TextBlock { Text = mat, FontFamily = Habillage.Console, FontSize = 13, Foreground = new SolidColorBrush(Color.FromRgb(150, 140, 190)), Margin = new Thickness(0, 8, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 600, HorizontalAlignment = HorizontalAlignment.Left });
            Grid.SetColumn(d, 1); g.Children.Add(d);
            racine.Children.Add(g);
            return racine;
        }

        /// <summary>Fenêtre « Carte de joueur » : aperçu, et image PNG enregistrée dans Images\Velox.</summary>
        public static void OuvrirCarte(FenetrePrincipale f)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 12 };
            var zone = new Panel { Height = 330 };
            s.Children.Add(new TextBlock { Text = Langue.T("Ta carte de joueur, à partager sur Discord ou sur tes réseaux. Elle ne contient que ton pseudo, ta progression et le nom de ton processeur et de ta carte graphique."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            s.Children.Add(zone);
            var bouton = new BoutonNoyau("Enregistrer l'image", true, 0, 42) { TaillePolice = 14, Capitales = true, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = false };
            s.Children.Add(bouton);
            f.Ouvrir("Carte de joueur", s, 760);
            Action<ProgressionJeu, string, string> afficher = (p, cpu, gpu) =>
            {
                Func<Control> creer = () => Carte(p, f.Pseudo, cpu, gpu);
                zone.Children.Add(new Viewbox { Child = creer(), Stretch = Stretch.Uniform });
                bouton.IsEnabled = true;
                bouton.Clic += () =>
                {
                    try
                    {
                        var carte = creer();
                        carte.Measure(new Size(960, 500)); carte.Arrange(new Rect(0, 0, 960, 500));
                        using (var rtb = new RenderTargetBitmap(new PixelSize(1920, 1000), new Vector(192, 192)))
                        {
                            rtb.Render(carte);
                            string dossier = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Velox");
                            Directory.CreateDirectory(dossier);
                            string fichier = Path.Combine(dossier, "carte-joueur-" + DateTime.Now.ToString("yyyy-MM-dd-HHmm") + ".png");
                            rtb.Save(fichier);
                            f.Statut(Langue.F("Carte enregistrée : Images\\Velox\\{0}", Path.GetFileName(fichier)));
                            try { Programmes.Ouvrir(dossier); } catch { }
                        }
                    }
                    catch (Exception ex) { f.Statut(Langue.T("Impossible d'enregistrer la carte :") + " " + ex.Message, true); }
                };
            };
            if (QgGamer5.Exemple != null) { afficher(QgGamer5.Exemple, "AMD Ryzen 7 7800X3D", "AMD Radeon RX 7900 XTX"); return; }
            System.Threading.Tasks.Task.Run(() =>
            {
                var p = Succes.Calculer(); string cpu = "", gpu = "";
                try { var m = MonPc.Lire(); cpu = m.Pieces.Where(x => x.Categorie == "Processeur").Select(x => x.Nom).FirstOrDefault() ?? ""; gpu = m.Pieces.Where(x => x.Categorie == "Carte graphique").Select(x => x.Nom).FirstOrDefault() ?? ""; } catch { }
                return Tuple.Create(p, cpu, gpu);
            }).ContinueWith(t => { if (!t.IsFaulted) Avalonia.Threading.Dispatcher.UIThread.Post(() => afficher(t.Result.Item1, t.Result.Item2, t.Result.Item3)); });
        }

        // ------------------------------------------------------------ niveau supérieur

        /// <summary>Animation plein écran : emblème, « NIVEAU N », et le nouveau rang s'il change. Un clic la ferme.</summary>
        public static void NiveauSuperieur(FenetrePrincipale f, ProgressionJeu p, bool nouveauRang)
        {
            var cr = CouleursRang(p.Rang.Palier);
            var calque = new Panel { Background = new SolidColorBrush(Color.FromArgb(220, 4, 2, 12)), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand) };
            calque.Children.Add(new FondSynthwave { Opacity = 0.3, Soleil = 0.5 });
            var s = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Spacing = 10 };
            var echelle = new ScaleTransform(0.3, 0.3);
            var emb = new Embleme { Width = 170, Height = 190, Palier = p.Rang.Palier, Niveau = p.Niveau, HorizontalAlignment = HorizontalAlignment.Center, RenderTransform = echelle };
            s.Children.Add(emb);
            s.Children.Add(new TextBlock { Text = Langue.T("NIVEAU SUPÉRIEUR"), FontFamily = Habillage.Console, FontSize = 15, LetterSpacing = 4, Foreground = new SolidColorBrush(Color.FromRgb(34, 230, 255)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 0) });
            s.Children.Add(new TextBlock { Text = Langue.F("NIVEAU {0}", p.Niveau), FontFamily = Habillage.Affichage, FontSize = 64, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center });
            if (nouveauRang) s.Children.Add(new TextBlock { Text = Langue.F("NOUVEAU RANG : {0}", Langue.T(p.Rang.Nom).ToUpper(Langue.Culture)), FontFamily = Habillage.Affichage, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = new SolidColorBrush(cr[0]), HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = Langue.T("Clique n'importe où pour continuer"), FontFamily = Habillage.Corps, FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(150, 140, 190)), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 14, 0, 0) });
            calque.Children.Add(s);
            bool ferme = false;
            Action fermer = () => { if (ferme) return; ferme = true; f.RetirerCalque(calque); };
            calque.PointerReleased += (o, e) => fermer();
            f.AjouterCalque(calque);
            Sons5.Niveau();
            if (!Habillage.Animations) { echelle.ScaleX = echelle.ScaleY = 1; return; }
            long t0 = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double ms = t - t0;
                // rebond de l'emblème
                double k = Math.Min(1, ms / 600.0), e = 1 + 0.18 * Math.Sin(k * Math.PI) * (1 - k);
                double v = 0.3 + 0.7 * Habillage.Douce(k);
                echelle.ScaleX = echelle.ScaleY = v * e;
                if (ms > 6000) { fermer(); return false; }
                return !ferme;
            });
        }
    }

    /// <summary>Emblème de rang : bouclier hexagonal aux couleurs du rang, avec le niveau au centre.</summary>
    internal sealed class Embleme : Control
    {
        public int Palier, Niveau;

        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width, h = Bounds.Height;
            if (w < 4) return;
            var cr = Progression5.CouleursRang(Palier);
            double cx = w / 2, cy = h / 2, r = Math.Min(w, h / 1.1) / 2;
            Func<double, StreamGeometry> hexa = rr =>
            {
                var g = new StreamGeometry();
                using (var c = g.Open())
                {
                    for (int i = 0; i < 6; i++)
                    {
                        double a = Math.PI / 180 * (60 * i - 90);
                        var pt = new Point(cx + rr * Math.Cos(a), cy + rr * 1.1 * Math.Sin(a));
                        if (i == 0) c.BeginFigure(pt, true); else c.LineTo(pt);
                    }
                    c.EndFigure(true);
                }
                return g;
            };
            // halo, contour lumineux, puis cœur sombre
            ctx.DrawGeometry(new RadialGradientBrush { GradientStops = { new GradientStop(Color.FromArgb(110, cr[0].R, cr[0].G, cr[0].B), 0), new GradientStop(Color.FromArgb(0, cr[0].R, cr[0].G, cr[0].B), 1) } }, null, hexa(r * 1.0));
            ctx.DrawGeometry(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(cr[0], 0), new GradientStop(cr[1], 1) } }, null, hexa(r * 0.86));
            ctx.DrawGeometry(new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.FromRgb(26, 14, 44), 0), new GradientStop(Color.FromRgb(8, 4, 18), 1) } }, new Pen(new SolidColorBrush(Color.FromArgb(160, 255, 255, 255)), Math.Max(1, r * 0.02)), hexa(r * 0.7));
            // étoiles du palier (1 à 6)
            for (int i = 0; i <= Palier; i++)
            {
                double x = cx + (i - Palier / 2.0) * r * 0.16, y = cy + r * 0.62;
                ctx.DrawEllipse(new SolidColorBrush(cr[0]), null, new Point(x, y), r * 0.045, r * 0.045);
            }
            var texte = new FormattedText(Niveau.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(Habillage.Affichage, FontStyle.Normal, FontWeight.Bold), r * 0.62, Brushes.White);
            ctx.DrawText(texte, new Point(cx - texte.Width / 2, cy - texte.Height / 2 - r * 0.06));
        }
    }

    /// <summary>Petite flamme (série de jours).</summary>
    internal sealed class Flamme : Control
    {
        public bool Allumee = true;
        public override void Render(DrawingContext ctx)
        {
            double w = Bounds.Width, h = Bounds.Height;
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(new Point(w * 0.5, 0), true);
                c.CubicBezierTo(new Point(w * 0.95, h * 0.35), new Point(w, h * 0.7), new Point(w * 0.5, h));
                c.CubicBezierTo(new Point(0, h * 0.7), new Point(w * 0.1, h * 0.45), new Point(w * 0.3, h * 0.3));
                c.CubicBezierTo(new Point(w * 0.35, h * 0.45), new Point(w * 0.45, h * 0.4), new Point(w * 0.5, 0));
                c.EndFigure(true);
            }
            IBrush b = Allumee
                ? new LinearGradientBrush { StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative), GradientStops = { new GradientStop(Color.FromRgb(255, 214, 92), 0), new GradientStop(Color.FromRgb(255, 92, 60), 0.6), new GradientStop(Color.FromRgb(255, 43, 214), 1) } }
                : new SolidColorBrush(Color.FromArgb(90, 150, 150, 170));
            ctx.DrawGeometry(b, null, g);
        }
    }
}
