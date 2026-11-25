// Velox - Studio son, mode Simple (maquette A « Console ») : un seul écran pour l'essentiel.
//  - en-tête : jeu détecté, interrupteur de l'égaliseur et choix Simple / Expert ;
//  - grande carte : gros bouton « Pas boostés », quatre touches de force, style Puissant / Net, réécoute, et le direct
//    (jauges des explosions tenues et de ton arme, retard ajouté, stéréo ou 7.1) ;
//  - trois cartes avec leur icône : mon casque, profil, vérification (« Changer » ouvre le module voulu juste en dessous) ;
//  - les dernières parties jouées avec Pas boostés (journal écrit par le moteur).
// Tout le reste (ton arme, réglage par jeu, tests, égaliseur détaillé, sorties…) est dans le mode Expert : rien n'est
// retiré, seulement rangé. Le guide reste affiché tant que l'installation n'est pas finie.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal sealed partial class AudioJeu5
    {
        private readonly List<Control> modulesSimple = new List<Control>();
        private readonly List<Control> modulesSurDemande = new List<Control>();
        private readonly HashSet<Control> ouverts = new HashSet<Control>();
        private StackPanel zoneSimple;
        private string signatureSimple;
        private TextBlock tSimpleReecoute, tTuileRetard, tTuileExplosions, tTuileArme, tTuileSon;
        private Control moduleGuide, moduleVerif, moduleProfils, moduleCasque;
        private Grid grilleHeros, grilleCartes;
        private Control tuilesHeros;
        private Grid gJaugeExplosions, gJaugeArme;
        private bool guideComplet;

        /// <summary>Visible seulement en mode Simple.</summary>
        private Control SimpleSeul(Control c) { modulesSimple.Add(c); return c; }

        /// <summary>Toujours visible en Expert ; en Simple, seulement une fois ouvert depuis une carte (« Changer »).</summary>
        private Control SurDemande(Control c) { if (c != null) modulesSurDemande.Add(c); return c; }

        private Control ConstruireSimple()
        {
            zoneSimple = new StackPanel { Spacing = 14, Margin = new Thickness(0, 0, 0, 16) };
            zoneSimple.SizeChanged += (o, e) => AjusterSimple(e.NewSize.Width);
            return zoneSimple;
        }

        // ---------------------------------------------------------------- briques du style E

        private static Border CarteE(Control contenu, double rayon = 16, Thickness? marge = null)
        {
            return new Border
            {
                Child = contenu, CornerRadius = new CornerRadius(rayon), Padding = marge ?? new Thickness(18, 16),
                Background = Studio.Degrade(1, Habillage.Alpha(Studio.PanneauHaut, 235), Habillage.Alpha(Studio.Panneau, 235)),
                BorderBrush = Studio.P(Studio.Bord, 210), BorderThickness = new Thickness(1),
            };
        }

        private static TextBlock Etiquette(string texte, double taille = 11)
        {
            return new TextBlock { Text = Langue.T(texte).ToUpper(Langue.Culture), FontFamily = Studio.Corps, FontWeight = FontWeight.SemiBold, FontSize = taille, LetterSpacing = 1.4, Foreground = Studio.P(Studio.Discret), TextWrapping = TextWrapping.Wrap };
        }

        /// <summary>Pastille arrondie (point lumineux + texte), cliquable si une action est donnée.</summary>
        private static Border Pastille(string texte, Color couleur, bool allume, Control aDroite = null, Action clic = null)
        {
            var point = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Studio.P(allume ? couleur : Studio.Faible), VerticalAlignment = VerticalAlignment.Center };
            if (allume) point.BoxShadow = BoxShadows.Parse("0 0 8 0 " + couleur.ToString());
            var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { point, new TextBlock { Text = texte, FontFamily = Studio.Titre, FontWeight = FontWeight.SemiBold, FontSize = 13, Foreground = Studio.P(Studio.Texte), VerticalAlignment = VerticalAlignment.Center } } };
            if (aDroite != null) { aDroite.VerticalAlignment = VerticalAlignment.Center; s.Children.Add(aDroite); }
            var b = new Border
            {
                Child = s, Padding = new Thickness(14, 8), CornerRadius = new CornerRadius(20), BorderThickness = new Thickness(1), Margin = new Thickness(8, 0, 0, 8),
                Background = Studio.P(allume ? couleur : Studio.Violet, allume ? 26 : 14), BorderBrush = Studio.P(allume ? couleur : Studio.Bord, allume ? 140 : 255),
            };
            if (clic != null)
            {
                b.Cursor = new Cursor(StandardCursorType.Hand); b.Focusable = true;
                b.PointerReleased += (o, e) => clic();
                b.KeyDown += (o, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; clic(); } };
            }
            return b;
        }

        /// <summary>Petit interrupteur (dessin seulement : la pastille qui le contient reçoit le clic).</summary>
        private static Control Interrupteur(bool allume)
        {
            var bouton = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = Studio.P(Colors.White), HorizontalAlignment = allume ? HorizontalAlignment.Right : HorizontalAlignment.Left, Margin = new Thickness(3) };
            return new Border { Width = 38, Height = 20, CornerRadius = new CornerRadius(10), Background = allume ? Studio.Degrade(0, Studio.Violet, Studio.Magenta) : Studio.P(Studio.Bord), Child = bouton };
        }

        /// <summary>Bouton plein au dégradé violet-rose de la maquette E.</summary>
        private static Border BoutonE(string texte, Action clic, string bulle = null)
        {
            var b = Studio.Bouton(texte, true, clic, bulle);
            b.Background = Studio.Degrade(0, Studio.Violet, Studio.Magenta);
            b.CornerRadius = new CornerRadius(12); b.Padding = new Thickness(18, 10);
            if (b.Child is TextBlock t) t.Foreground = Studio.P(Colors.White);
            return b;
        }

        /// <summary>Choix segmenté (Puissant / Net) : l'option choisie est pleine.</summary>
        private Control Segment(IEnumerable<Tuple<string, bool, Action, string>> choix)
        {
            var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
            foreach (var c in choix)
            {
                var cc = c;
                var b = new Border
                {
                    Child = new TextBlock { Text = Langue.T(c.Item1), FontFamily = Studio.Titre, FontWeight = c.Item2 ? FontWeight.Bold : FontWeight.SemiBold, FontSize = 13.5, Foreground = Studio.P(c.Item2 ? Colors.White : Studio.Discret) },
                    Padding = new Thickness(16, 7), CornerRadius = new CornerRadius(9), Cursor = new Cursor(StandardCursorType.Hand), Focusable = true,
                    Background = c.Item2 ? Studio.P(Studio.Violet) : Brushes.Transparent,
                };
                if (c.Item4 != null) ToolTip.SetTip(b, c.Item4);
                AutomationProperties.SetName(b, Langue.T(c.Item1));
                b.PointerReleased += (o, e) => cc.Item3();
                b.KeyDown += (o, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; cc.Item3(); } };
                s.Children.Add(b);
            }
            return new Border { Child = s, Padding = new Thickness(3), CornerRadius = new CornerRadius(12), Background = Studio.P(Studio.Fond1, 220), BorderBrush = Studio.P(Studio.Bord), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 8), VerticalAlignment = VerticalAlignment.Center };
        }

        /// <summary>Jauge du direct : nom et valeur, puis une barre remplie de 0 à 1 (voir <see cref="RemplirJauge"/>).</summary>
        private static Control Jauge(string titre, Color couleur, out TextBlock valeur, out Grid barre)
        {
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 6) };
            haut.Children.Add(new TextBlock { Text = Langue.T(titre), FontFamily = Studio.Corps, FontSize = 12.5, Foreground = Studio.P(Studio.Texte), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center });
            valeur = new TextBlock { Text = "—", FontFamily = Studio.Chiffres, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = Studio.P(couleur), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(valeur, 1); haut.Children.Add(valeur);
            barre = new Grid { ColumnDefinitions = new ColumnDefinitions("0*,1*") };
            barre.Children.Add(new Border { CornerRadius = new CornerRadius(4), Background = Studio.Degrade(0, Habillage.Alpha(couleur, 150), couleur), BoxShadow = BoxShadows.Parse("0 0 8 0 " + Habillage.Alpha(couleur, 120).ToString()) });
            var piste = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Studio.P(Studio.Bord, 200), ClipToBounds = true, Child = barre };
            return new StackPanel { Children = { haut, piste } };
        }

        private static void RemplirJauge(Grid barre, double part)
        {
            if (barre == null) return;
            part = double.IsNaN(part) ? 0 : Math.Max(0, Math.Min(1, part));
            barre.ColumnDefinitions[0].Width = new GridLength(part, GridUnitType.Star);
            barre.ColumnDefinitions[1].Width = new GridLength(1 - part, GridUnitType.Star);
        }

        /// <summary>Le direct, à droite du gros bouton : explosions tenues, ton arme, retard ajouté, stéréo ou 7.1.</summary>
        private Control PanneauDirect()
        {
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            haut.Children.Add(Etiquette("En direct"));
            tTuileSon = new TextBlock { Text = "—", FontFamily = Studio.Chiffres, FontSize = 11.5, FontWeight = FontWeight.Bold, Foreground = Studio.P(Studio.Discret), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tTuileSon, 1); haut.Children.Add(tTuileSon);
            var retard = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            retard.Children.Add(new TextBlock { Text = Langue.T("Retard ajouté"), FontFamily = Studio.Corps, FontSize = 12.5, Foreground = Studio.P(Studio.Discret), VerticalAlignment = VerticalAlignment.Center });
            tTuileRetard = new TextBlock { Text = "—", FontFamily = Studio.Chiffres, FontSize = 13, FontWeight = FontWeight.Bold, Foreground = Studio.P(Studio.Texte), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(tTuileRetard, 1); retard.Children.Add(tTuileRetard);
            var pile = new StackPanel
            {
                Spacing = 13, Children =
                {
                    haut,
                    Jauge("Explosions tenues", Studio.Ambre, out tTuileExplosions, out gJaugeExplosions),
                    Jauge("Ton arme", Studio.Violet, out tTuileArme, out gJaugeArme),
                    new Border { BorderBrush = Studio.P(Studio.Bord, 200), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(0, 10, 0, 0), Child = retard },
                },
            };
            return new Border
            {
                Child = pile, Width = 300, Padding = new Thickness(16, 14), CornerRadius = new CornerRadius(14), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0),
                Background = Studio.P(Studio.Fond1, 220), BorderBrush = Studio.P(Studio.Bord), BorderThickness = new Thickness(1),
            };
        }

        /// <summary>Les quatre forces (Léger, Fort, Extrême, Ultra) en grosses touches : la force choisie est pleine.</summary>
        private Control TouchesForce(int forceVue)
        {
            var details = new[] { "discret", "conseillé", "encore plus", "maximum" };
            var grille = new Grid { ColumnDefinitions = new ColumnDefinitions(string.Join(",8,", MoteurSon.Niveaux.Select(n => "*"))) };
            for (int i = 0; i < MoteurSon.Niveaux.Length; i++)
            {
                var n = MoteurSon.Niveaux[i];
                bool choisi = n.Item2 == forceVue;
                var b = new Border
                {
                    Padding = new Thickness(13, 9), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1), Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, IsEnabled = !pasOccupe,
                    Background = choisi ? Studio.Degrade(0, Studio.Violet, Studio.Magenta) : Studio.P(Studio.Fond1, 220),
                    BorderBrush = choisi ? Brushes.Transparent : Studio.P(Studio.Bord),
                    Child = new StackPanel
                    {
                        Spacing = 1, Children =
                        {
                            new TextBlock { Text = Langue.T(n.Item1), FontFamily = Studio.Titre, FontSize = 14.5, FontWeight = choisi ? FontWeight.Bold : FontWeight.SemiBold, Foreground = Studio.P(choisi ? Colors.White : Studio.Texte), TextTrimming = TextTrimming.CharacterEllipsis },
                            new TextBlock { Text = i < details.Length ? Langue.T(details[i]) : "", FontFamily = Studio.Corps, FontSize = 11.5, Foreground = choisi ? Studio.P(Colors.White, 225) : Studio.P(Studio.Discret), TextTrimming = TextTrimming.CharacterEllipsis },
                        },
                    },
                };
                AutomationProperties.SetName(b, Langue.T("Force des pas") + " : " + Langue.T(n.Item1));
                int force = n.Item2;
                Action choisir = () => { if (!choisi && !pasOccupe) ReglerPas(force, null); };
                b.PointerReleased += (o, e) => choisir();
                b.KeyDown += (o, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; choisir(); } };
                if (!choisi)
                {
                    b.PointerEntered += (o, e) => b.BorderBrush = Studio.P(Studio.Violet, 180);
                    b.PointerExited += (o, e) => b.BorderBrush = Studio.P(Studio.Bord);
                }
                Grid.SetColumn(b, i * 2); grille.Children.Add(b);
            }
            ToolTip.SetTip(grille, Langue.T("Conseil : Puissant et Fort. Monte à Extrême si tu veux encore plus de pas."));
            return grille;
        }

        /// <summary>Lien discret « Changer › » : ouvre le module (mode Simple) et y descend.</summary>
        private Control Lien(string texte, Action clic)
        {
            var t = new TextBlock { Text = Langue.T(texte) + " ›", FontFamily = Studio.Corps, FontSize = 12.5, Foreground = Studio.P(Studio.Violet), Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, Margin = new Thickness(0, 10, 0, 0) };
            t.PointerReleased += (o, e) => clic();
            t.KeyDown += (o, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; clic(); } };
            return t;
        }

        private void Ouvrir(Control module)
        {
            if (module == null) return;
            ouverts.Add(module); module.IsVisible = true;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => module.BringIntoView(), Avalonia.Threading.DispatcherPriority.Background);
        }

        /// <summary>Nom du niveau de force (« Fort »…), ou le pourcentage s'il ne correspond à aucun niveau.</summary>
        internal static string NomForce(int force)
        {
            var n = MoteurSon.Niveaux.FirstOrDefault(x => x.Item2 == force);
            return n != null ? Langue.T(n.Item1) : force.ToString(CultureInfo.InvariantCulture) + " %";
        }

        /// <summary>Heure d'une partie : « 20:08 » aujourd'hui, « hier 20:08 », sinon la date.</summary>
        internal static string QuandPartie(DateTime debut, DateTime maintenant)
        {
            string h = debut.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (debut.Date == maintenant.Date) return h;
            if (debut.Date == maintenant.Date.AddDays(-1)) return Langue.F("hier {0}", h);
            return debut.ToString("dd/MM", CultureInfo.InvariantCulture);
        }

        // ---------------------------------------------------------------- mise à jour

        private void MajSimple(ReleveAudioJeu v)
        {
            if (zoneSimple == null) return;
            var m = v.Moteur ?? new ReglagesMoteurSon();
            var et = v.MoteurEtat ?? new MoteurSon.EtatMoteur();
            bool cable = v.Cables.Count > 0;
            int forceVue, armeVue; m.Effectifs(et.Marche ? et.JeuCle : "", out forceVue, out armeVue);
            string jeu = et.Marche ? NomJeuCle(et.JeuCle) : "";
            int etatCible; Cible(v, out etatCible);
            var diag = Diagnostic(r, v, Vm, etatCible);
            bool ok = diag.All(x => x.Item1);
            string probleme = diag.Where(x => !x.Item1).Select(x => x.Item2).FirstOrDefault() ?? "";
            var p = AudioJeu.ProfilDe(r.Gains);
            var parties = (v.Parties ?? new List<PartieJouee>()).AsEnumerable().Reverse().Take(5).ToList();
            string sig = string.Join("|", cable, m.Actif, forceVue, armeVue, m.Style, pasOccupe, et.Marche, et.Erreur, guideComplet, jeu, r.Actif, r.CalibActive, r.CalibNom, r.CalibSource,
                AudioJeu.NomProfil(r), ok, probleme, string.Join(";", parties.Select(x => x.Ligne())), DateTime.Now.Date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
            if (sig != signatureSimple)
            {
                signatureSimple = sig;
                zoneSimple.Children.Clear();
                zoneSimple.Children.Add(EnTeteSimple(jeu));
                zoneSimple.Children.Add(Heros(m, et, cable, forceVue));
                zoneSimple.Children.Add(Cartes(p, ok, probleme));
                zoneSimple.Children.Add(DernieresParties(parties));
                AjusterSimple(zoneSimple.Bounds.Width > 0 ? zoneSimple.Bounds.Width : 1000);
            }
            // tuiles et réécoute : à chaque relevé, sans tout reconstruire
            bool marche = cable && m.Actif && et.Marche && et.Erreur == "";
            if (tTuileRetard != null)
            {
                var c = CultureInfo.InvariantCulture;
                tTuileRetard.Text = marche && et.LatenceMs > 0 ? et.LatenceMs.ToString(c) + " ms" : "—";
                tTuileExplosions.Text = marche && et.Reduction >= 0.5 ? "−" + et.Reduction.ToString("0", c) + " dB" : marche ? "0 dB" : "—";
                tTuileArme.Text = !marche ? "—" : armeVue == 0 ? Langue.T("Normale") : et.ReductionArme >= 0.5 ? "−" + et.ReductionArme.ToString("0", c) + " dB" : "0 dB";
                tTuileSon.Text = !marche ? "—" : "● " + (et.Canaux >= 6 ? "7.1" : Langue.T("Stéréo"));
                tTuileSon.Foreground = Studio.P(marche ? Studio.Vert : Studio.Discret);
                // jauges : 24 dB de baisse remplissent la barre
                RemplirJauge(gJaugeExplosions, marche ? et.Reduction / 24.0 : 0);
                RemplirJauge(gJaugeArme, marche && armeVue != 0 ? et.ReductionArme / 24.0 : 0);
            }
            if (tSimpleReecoute != null)
            {
                if (et.Reecoute == 1) tSimpleReecoute.Text = Langue.F("▶ Sans Velox… ({0} s)", et.ReecouteSecondes);
                else if (et.Reecoute == 3) tSimpleReecoute.Text = Langue.F("▶ Avec Velox ({0} s)", et.ReecouteSecondes);
                else if (marche && !et.Jeu) tSimpleReecoute.Text = Langue.T("Lance une partie, puis reviens ici.");
                else tSimpleReecoute.Text = "";
                tSimpleReecoute.IsVisible = tSimpleReecoute.Text != "";
            }
        }

        private Control EnTeteSimple(string jeu)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
            var badge = new Border
            {
                Width = 44, Height = 44, CornerRadius = new CornerRadius(12), Background = Studio.P(Studio.PanneauHaut), BorderBrush = Studio.P(Studio.Bord), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 0),
                Child = new TextBlock { Text = "🎧", FontSize = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
                VerticalAlignment = VerticalAlignment.Top,
            };
            // le titre « Studio son » est déjà en haut de la page : ici, seulement le jeu détecté et l'égaliseur
            badge.IsVisible = false;
            g.Children.Add(badge);
            var titre = new TextBlock { FontFamily = Studio.Titre, FontSize = 24, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
            string nomStudio = Langue.T("Studio son"); int coupe = nomStudio.LastIndexOf(' ');   // le dernier mot en violet
            titre.Inlines = new Avalonia.Controls.Documents.InlineCollection
            {
                new Avalonia.Controls.Documents.Run(coupe > 0 ? nomStudio.Substring(0, coupe + 1) : "") { Foreground = Studio.P(Studio.Texte) },
                new Avalonia.Controls.Documents.Run(coupe > 0 ? nomStudio.Substring(coupe + 1) : nomStudio) { Foreground = Studio.P(Studio.Violet) },
            };
            var gauche = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, IsVisible = false, Children = { titre, Studio.T(Langue.T("Entends les ennemis avant qu'ils te voient"), 12.5, Studio.Discret) } };
            Grid.SetColumn(gauche, 1); g.Children.Add(gauche);
            var droite = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            droite.Children.Add(Pastille(jeu != "" ? Langue.F("{0} détecté", jeu) : Langue.T("Aucun jeu détecté"), Studio.Vert, jeu != ""));
            var eq = Pastille(Langue.T("Égaliseur"), Studio.Violet, false, Interrupteur(r.Actif), () => BasculerEq(!r.Actif));
            ToolTip.SetTip(eq, Langue.T("Allume ou coupe l'égaliseur (profil et calibration du casque)."));
            AutomationProperties.SetName(eq, Langue.T(r.Actif ? "Égaliseur : actif" : "Égaliseur : coupé"));
            droite.Children.Add(eq);
            var mode = Segment(new[]
            {
                Tuple.Create("Simple", true, (Action)(() => { }), Langue.T("L'essentiel sur un seul écran.")),
                Tuple.Create("Expert", false, (Action)(() => ChoisirMode(true)), Langue.T("Tous les réglages, rangés en onglets.")),
            });
            mode.Margin = new Thickness(8, 0, 0, 8);
            AutomationProperties.SetName(mode, Langue.T("Affichage"));
            droite.Children.Add(mode);
            Grid.SetColumn(droite, 2); g.Children.Add(droite);
            return g;
        }

        private Control Heros(ReglagesMoteurSon m, MoteurSon.EtatMoteur et, bool cable, int forceVue)
        {
            grilleHeros = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto") };
            // le gros bouton : Pas boostés
            var bouton = new Marche5(cable && m.Actif) { Width = 150, Height = 150, IsEnabled = cable && !pasOccupe, Violet = true, HorizontalAlignment = HorizontalAlignment.Center };
            AutomationProperties.SetName(bouton, Langue.T("Pas boostés"));
            ToolTip.SetTip(bouton, Langue.T(cable ? "Allume ou coupe « Pas boostés » : Velox remonte les pas et calme les explosions." : "Installe d'abord VB-Cable (guide ci-dessous)."));
            bouton.Demande += voulu => BasculerPas(voulu);
            string etat = !cable ? Langue.T("À INSTALLER") : pasOccupe ? Langue.T("BRANCHEMENT…") : m.Actif ? (et.Erreur != "" ? Langue.T("ERREUR") : Langue.T("ACTIF")) : Langue.T("COUPÉ");
            var couleurEtat = !cable || (m.Actif && et.Erreur != "") ? Studio.Ambre : m.Actif ? Studio.Vert : Studio.Faible;
            var gaucheH = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 26, 0), VerticalAlignment = VerticalAlignment.Center, Children =
            {
                bouton,
                new TextBlock { Text = etat, FontFamily = Studio.Chiffres, FontSize = 12, LetterSpacing = 2.5, FontWeight = FontWeight.Bold, Foreground = Studio.P(couleurEtat), HorizontalAlignment = HorizontalAlignment.Center },
            } };
            grilleHeros.Children.Add(gaucheH);

            // le centre : force, style, réécoute (ou ce qu'il reste à faire)
            var centre = new StackPanel { Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
            centre.Children.Add(new StackPanel
            {
                Spacing = 2, Children =
                {
                    new TextBlock { Text = Langue.T("Pas boostés"), FontFamily = Studio.Titre, FontSize = 24, FontWeight = FontWeight.Bold, Foreground = Studio.P(Studio.Texte) },
                    Studio.T(Langue.T("Les pas remontent, les explosions restent tenues."), 13, Studio.Discret),
                },
            });
            if (!cable)
            {
                centre.Children.Add(Studio.T(Langue.T("Pas boostés n'est pas encore installé : suis le guide ci-dessous (2 minutes)."), 16, Studio.Texte, true, Studio.Titre));
                centre.Children.Add(new WrapPanel { Children = { BoutonE(Langue.T("Suivre le guide"), () => Ouvrir(moduleGuide)) } });
            }
            else
            {
                if (m.Actif && et.Erreur != "") centre.Children.Add(Studio.T(Langue.T(et.Erreur), 12.5, Studio.Ambre));
                else if (!m.Actif && !pasOccupe) centre.Children.Add(Studio.T(Langue.T("Pas boostés est coupé : allume-le pour entendre les pas."), 12.5, Studio.Ambre));
                centre.Children.Add(Etiquette("Force des pas"));
                centre.Children.Add(TouchesForce(forceVue));
                var ligne = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
                ligne.Children.Add(Segment(new[]
                {
                    Tuple.Create("Puissant", m.Style == CompresseurPas.StylePuissant, (Action)(() => ReglerStyle(CompresseurPas.StylePuissant)), Langue.T("Tout est plus fort, les pas très en avant.")),
                    Tuple.Create("Net", m.Style != CompresseurPas.StylePuissant, (Action)(() => ReglerStyle(CompresseurPas.StyleNet)), Langue.T("Les pas gardent leur relief, le fond monte peu.")),
                }));
                var reecoute = BoutonE("▶ " + Langue.T("Réécouter sans / avec"), BasculerReecoute, Langue.T("Rejoue les 10 dernières secondes du jeu : d'abord sans Velox, puis avec, au même volume."));
                reecoute.IsEnabled = m.Actif && et.Marche && !pasOccupe;
                reecoute.Opacity = reecoute.IsEnabled ? 1 : 0.5;
                ligne.Children.Add(reecoute);
                centre.Children.Add(ligne);
                tSimpleReecoute = Studio.T("", 12, Studio.Discret);
                centre.Children.Add(tSimpleReecoute);
            }
            Grid.SetColumn(centre, 1); grilleHeros.Children.Add(centre);

            // le direct
            var tuiles = PanneauDirect();
            ToolTip.SetTip(tuiles, Langue.T("En direct, pendant que Pas boostés tourne : retard ajouté, baisse des explosions et de tes tirs, nombre de voies reçues du jeu."));
            tuilesHeros = tuiles;
            Grid.SetColumn(tuiles, 2); grilleHeros.Children.Add(tuiles);
            return CarteE(grilleHeros, 18, new Thickness(20, 18));
        }

        private Control Cartes(ProfilAudio p, bool ok, string probleme)
        {
            grilleCartes = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*,12,*") };
            Func<string, Color, Control> icone = (cle, c) => new Border
            {
                Width = 40, Height = 40, CornerRadius = new CornerRadius(12), Margin = new Thickness(0, 0, 14, 0), VerticalAlignment = VerticalAlignment.Top,
                Background = Studio.P(Studio.PanneauHaut), BorderBrush = Studio.P(Studio.Bord), BorderThickness = new Thickness(1),
                Child = new Icone5 { Cle = cle, Couleur = c, Epaisseur = 1.9, Width = 20, Height = 20, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            Func<Control, Control, Control> avecIcone = (ic, texte) =>
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
                g.Children.Add(ic); Grid.SetColumn(texte, 1); g.Children.Add(texte);
                return g;
            };
            Func<string, Color, string, string, Color, string, string, Action, Control> carte = (cle, couleurIcone, titre, valeur, couleur, detail, lien, clic) =>
                CarteE(avecIcone(icone(cle, couleurIcone), new StackPanel
                {
                    Children =
                    {
                        Etiquette(titre),
                        new TextBlock { Text = valeur, FontFamily = Studio.Titre, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Studio.P(couleur), Margin = new Thickness(0, 7, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis },
                        new TextBlock { Text = detail, FontFamily = Studio.Corps, FontSize = 12, Foreground = Studio.P(Studio.Discret), Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis },
                        Lien(lien, clic),
                    },
                }));
            bool calibre = r.CalibActive && !string.IsNullOrEmpty(r.CalibNom);
            var c1 = carte("Audio", Studio.Violet, "Mon casque", calibre ? r.CalibNom : Langue.T("Pas encore choisi"), Studio.Texte,
                calibre ? (string.IsNullOrEmpty(r.CalibSource) ? Langue.T("Calibré") : Langue.F("Calibré · mesures {0}", r.CalibSource)) : Langue.T("Choisis ton modèle : Velox corrige ses défauts."),
                calibre ? "Changer" : "Choisir", () => Ouvrir(moduleCasque));
            var c2 = carte("Curseurs", Studio.Violet, "Profil", p != null ? Langue.T(p.Carte) : Langue.T(AudioJeu.NomProfil(r)), Studio.Texte,
                p != null ? Langue.T(p.Texte) : Langue.T("Réglage personnalisé"), "Changer", () => Ouvrir(moduleProfils));
            var c3 = carte(ok ? "Coche" : "Alerte", ok ? Studio.Vert : Studio.Ambre, "Vérification", Langue.T(ok ? "Tout fonctionne" : "À vérifier"), ok ? Studio.Vert : Studio.Ambre,
                ok ? Langue.T("Égaliseur, câble, casque") : probleme, "Détails", () => Ouvrir(moduleVerif));
            Grid.SetColumn(c2, 2); Grid.SetColumn(c3, 4);
            grilleCartes.Children.Add(c1); grilleCartes.Children.Add(c2); grilleCartes.Children.Add(c3);
            return grilleCartes;
        }

        private Control DernieresParties(List<PartieJouee> parties)
        {
            var pile = new StackPanel { Spacing = 0 };
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 8) };
            haut.Children.Add(Etiquette("Dernières parties"));
            var expert = Lien("Tous les réglages (Expert)", () => ChoisirMode(true));
            expert.Margin = new Thickness(0);
            Grid.SetColumn(expert, 1); haut.Children.Add(expert);
            pile.Children.Add(haut);
            if (parties.Count == 0)
            {
                pile.Children.Add(Studio.T(Langue.T("Tes parties apparaîtront ici après ta première partie avec Pas boostés (une minute au moins)."), 12.5, Studio.Faible));
                return CarteE(pile);
            }
            const string colonnes = "0.8*,1.3*,1*,1*,1.5*,0.8*";
            Func<string[], bool, Grid> rangee = (cases, entete) =>
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions(colonnes) };
                for (int i = 0; i < cases.Length; i++)
                {
                    var t = new TextBlock { Text = cases[i], FontFamily = entete ? Studio.Corps : (i == 0 || i == 5 ? Studio.Chiffres : Studio.Corps), FontSize = entete ? 11.5 : 13, Foreground = Studio.P(entete ? Studio.Discret : Studio.Texte), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(t, i); g.Children.Add(t);
                }
                return g;
            };
            Func<Control, Control> ligne = c => new Border { Child = c, Padding = new Thickness(0, 7), BorderBrush = Studio.P(Studio.Bord, 150), BorderThickness = new Thickness(0, 0, 0, 1) };
            pile.Children.Add(ligne(rangee(new[] { Langue.T("Heure"), Langue.T("Jeu"), Langue.T("Style"), Langue.T("Force"), Langue.T("Explosions tenues"), Langue.T("Retard") }, true)));
            var c = CultureInfo.InvariantCulture;
            var maintenant = DateTime.Now;
            foreach (var x in parties)
            {
                string nom = NomJeuCle(x.Jeu); if (nom == "") nom = x.Jeu;
                var g = rangee(new[]
                {
                    QuandPartie(x.Debut, maintenant), nom, Langue.T(x.Style == CompresseurPas.StylePuissant ? "Puissant" : "Net"), "",
                    x.Explosions >= 0.5 ? Langue.F("jusqu'à −{0} dB", x.Explosions.ToString("0", c)) : "—",
                    x.Retard > 0 ? x.Retard.ToString(c) + " ms" : "—",
                }, false);
                ToolTip.SetTip(g, Langue.F("{0} min de jeu", x.Minutes));
                var couleur = x.Style == CompresseurPas.StylePuissant ? Studio.Ambre : Studio.Vert;
                var etiquette = new Border
                {
                    Child = new TextBlock { Text = NomForce(x.Force), FontFamily = Studio.Titre, FontWeight = FontWeight.Bold, FontSize = 12, Foreground = Studio.P(couleur) },
                    Padding = new Thickness(9, 2), CornerRadius = new CornerRadius(8), Background = Studio.P(couleur, 30), BorderBrush = Studio.P(couleur, 120), BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(etiquette, 3); g.Children.Add(etiquette);
                pile.Children.Add(ligne(g));
            }
            return CarteE(pile);
        }

        /// <summary>En étroit : le direct passe sous la force, et les trois cartes l'une sous l'autre.</summary>
        private void AjusterSimple(double largeur)
        {
            if (grilleHeros != null && tuilesHeros != null)
            {
                bool etroit = largeur < 860;
                Grid.SetColumn(tuilesHeros, etroit ? 1 : 2); Grid.SetRow(tuilesHeros, etroit ? 1 : 0);
                tuilesHeros.Margin = etroit ? new Thickness(0, 14, 0, 0) : new Thickness(18, 0, 0, 0);
                tuilesHeros.Width = etroit ? double.NaN : 300;
            }
            if (grilleCartes != null)
            {
                bool une = largeur < 640;
                var enfants = grilleCartes.Children.ToList();
                grilleCartes.ColumnDefinitions = new ColumnDefinitions(une ? "*" : "*,12,*,12,*");
                grilleCartes.RowDefinitions = new RowDefinitions(une ? "Auto,Auto,Auto" : "Auto");
                for (int i = 0; i < enfants.Count; i++)
                {
                    Grid.SetColumn(enfants[i], une ? 0 : i * 2); Grid.SetRow(enfants[i], une ? i : 0);
                    enfants[i].Margin = une && i > 0 ? new Thickness(0, 12, 0, 0) : new Thickness(0);
                }
            }
        }

        /// <summary>Le guide : toujours en mode Expert ; en mode Simple, tant que l'installation n'est pas finie, ou rouvert.</summary>
        private void AppliquerVisibiliteGuide()
        {
            if (moduleGuide != null) moduleGuide.IsVisible = r.ModeExpert || !guideComplet || ouverts.Contains(moduleGuide);
        }
    }
}
