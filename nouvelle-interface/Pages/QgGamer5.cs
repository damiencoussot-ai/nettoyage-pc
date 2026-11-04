// Velox 5 - « QG GAMER » (profil Gamer, accueil HUD) : le PC est-il prêt pour jouer (score et liste de points),
// le niveau et l'XP gagnés en entretenant le PC, les succès débloqués, et le mode jeu en un clic.
// Tout est lu sur le PC (rien n'est envoyé) ; le mode jeu passe par le moteur comme d'habitude, avec sa question.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class QgGamer5 : Cadre5
    {
        /// <summary>(captures) progression d'exemple : les fichiers de l'application ne sont pas lus.</summary>
        public static ProgressionJeu Exemple;

        private readonly FenetrePrincipale F;
        private readonly TextBlock tScore, tNiveau, tTitreNiveau, tXp, tSucces;
        private readonly StackPanel checklist = new StackPanel { Spacing = 5 };
        private readonly Border remplissage;
        private readonly Grid barreXp;
        private readonly WrapPanel badges = new WrapPanel { Orientation = Orientation.Horizontal };
        private readonly BoutonNoyau bModeJeu;
        private double avancement;
        private double memoire = -1, temperature = -1, gpu = -1; private int indice = -1;

        public QgGamer5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Coupe = 18; UnCoin = false;
            Background = Habillage.Pinceau(P.Carte, P.Sombre ? 230 : 255);
            BorderBrush = Habillage.Pinceau(P.Accent, 80); BorderThickness = new Thickness(1);
            TraitHaut = P.Accent;
            Padding = new Thickness(18, 14, 18, 16);
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,24,1.15*,24,Auto") };

            // ---- prêt pour jouer
            var a = new StackPanel { Spacing = 6 };
            a.Children.Add(Etiquette("PRÊT POUR JOUER"));
            tScore = new TextBlock { Text = "…", FontFamily = Habillage.Affichage, FontSize = 38, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Accent), Margin = new Thickness(0, -4, 0, 0) };
            a.Children.Add(tScore);
            a.Children.Add(checklist);
            g.Children.Add(a);

            // ---- niveau, XP et succès
            var b = new StackPanel { Spacing = 6 };
            b.Children.Add(Etiquette("NIVEAU DU TECHNICIEN"));
            var ligneNiveau = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
            tNiveau = new TextBlock { Text = "NIV. 1", FontFamily = Habillage.Affichage, FontSize = 30, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Bottom };
            tTitreNiveau = new TextBlock { Text = "", FontFamily = Habillage.Console, FontSize = 12, LetterSpacing = 1, Foreground = Habillage.Pinceau(P.Accent2), VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 6) };
            ligneNiveau.Children.Add(tNiveau); ligneNiveau.Children.Add(tTitreNiveau);
            b.Children.Add(ligneNiveau);
            // barre d'XP à coins coupés, remplie d'un dégradé magenta → violet
            barreXp = new Grid { Height = 12 };
            barreXp.Children.Add(new Border { Background = Habillage.Pinceau(P.Accent, 26), BorderBrush = Habillage.Pinceau(P.Accent, 70), BorderThickness = new Thickness(1) });
            remplissage = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left, Width = 0, Margin = new Thickness(2),
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(P.Accent, 0), new GradientStop(P.Accent2, 1) },
                },
            };
            if (P.Sombre) remplissage.BoxShadow = BoxShadows.Parse("0 0 10 0 " + Habillage.Alpha(P.Accent, 150).ToString());
            barreXp.Children.Add(remplissage);
            barreXp.SizeChanged += (o, e) => PlacerXp();
            b.Children.Add(barreXp);
            tXp = new TextBlock { Text = "", FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(P.Discret) };
            b.Children.Add(tXp);
            var ligneSucces = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 4, 0, 0) };
            var gaucheSucces = new StackPanel { Spacing = 4 };
            tSucces = new TextBlock { Text = "", FontFamily = Habillage.Console, FontSize = 11, LetterSpacing = 1, Foreground = Habillage.Pinceau(P.Accent, 210) };
            gaucheSucces.Children.Add(tSucces); gaucheSucces.Children.Add(badges);
            ligneSucces.Children.Add(gaucheSucces);
            var voir = new BoutonNoyau("Succès", false, 0, 30) { TaillePolice = 12, Capitales = true, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(10, 0, 0, 0) };
            voir.Clic += () => Succes5.Ouvrir(F);
            Grid.SetColumn(voir, 1); ligneSucces.Children.Add(voir);
            b.Children.Add(ligneSucces);
            Grid.SetColumn(b, 2); g.Children.Add(b);

            // ---- actions
            var c = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Center, MinWidth = 200 };
            bool actif = ModeJeuActif();
            bModeJeu = new BoutonNoyau(actif ? "Terminer le mode jeu" : "Lancer le mode jeu", !actif, 0, 50) { TaillePolice = 15, Capitales = true, HorizontalAlignment = HorizontalAlignment.Stretch };
            bModeJeu.Clic += () => Gamer5.Boost(F, MajBouton);
            c.Children.Add(bModeJeu);
            var fps = new BoutonNoyau("Preuve en FPS", false, 0, 40) { TaillePolice = 13, Capitales = true, HorizontalAlignment = HorizontalAlignment.Stretch };
            fps.Clic += () => F.OuvrirOutil("fps");
            c.Children.Add(fps);
            var jeux = new BoutonNoyau("Réglages des jeux", false, 0, 40) { TaillePolice = 13, Capitales = true, HorizontalAlignment = HorizontalAlignment.Stretch };
            jeux.Clic += () => F.OuvrirOutil("reglagesjeux");
            c.Children.Add(jeux);
            // réglages rapides : sons (coupés par défaut) et overlay en jeu
            var options = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 };
            var bSons = new BoutonNoyau(Sons5.Actifs ? "Sons : oui" : "Sons : non", false, 0, 32) { TaillePolice = 11.5, Capitales = true, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 6, 0) };
            ToolTip.SetTip(bSons, Langue.T("Petits sons au BOOST et quand un succès se débloque."));
            bSons.Clic += () =>
            {
                Sons5.Actifs = !Sons5.Actifs; bool v = Sons5.Actifs;
                F.EnregistrerReglage(r => r.SonsGamer = v);
                bSons.Changer(v ? "Sons : oui" : "Sons : non");
                if (v) Sons5.Succes();
            };
            var bOverlay = new BoutonNoyau("Overlay", false, 0, 32) { TaillePolice = 11.5, Capitales = true, HorizontalAlignment = HorizontalAlignment.Stretch };
            ToolTip.SetTip(bOverlay, Langue.T("Processeur, carte graphique et mémoire par-dessus tes jeux. Ctrl+Maj+O pour l'afficher ou le cacher, même en jeu."));
            bOverlay.Clic += () => Overlay5.Basculer(F, false);
            options.Children.Add(bSons); options.Children.Add(bOverlay);
            c.Children.Add(options);
            Grid.SetColumn(c, 4); g.Children.Add(c);

            Child = g;
            Recalculer();
            ChargerProgression();
        }

        private static TextBlock Etiquette(string texte)
        {
            return new TextBlock { Text = "▸ " + Langue.T(texte), FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(Habillage.P.Accent, 200) };
        }

        private static bool ModeJeuActif()
        {
            try { return File.Exists(Chemins.Fichier("modejeu.json")); } catch { return false; }
        }

        private void MajBouton()
        {
            bool actif = ModeJeuActif();
            bModeJeu.Changer(actif ? "Terminer le mode jeu" : "Lancer le mode jeu");
            Recalculer();
        }

        /// <summary>Bilan reçu par l'accueil (indice, mémoire, température).</summary>
        public void MajBilan(Bilan b)
        {
            if (b == null) return;
            indice = b.Score;
            if (Exemple != null) gpu = 63;
            if (b.Memoire >= 0) memoire = b.Memoire;
            if (b.Temperature > 0) temperature = b.Temperature;
            Recalculer();
        }

        /// <summary>Mesures en direct (toutes les deux secondes) : seuls les seuils franchis changent l'affichage.</summary>
        public void MajMesures(Mesures m)
        {
            if (m == null) return;
            bool avant = Etat(memoire, 70) , avantT = Etat(temperature, 80), avantG = Etat(gpu, 85);
            double g = Exemple != null ? 63 : TemperatureGpu.Lire();
            bool nouveauGpu = (g > 0) != (gpu > 0);
            if (g > 0) gpu = g;
            if (m.Memoire >= 0) memoire = m.Memoire;
            if (m.Temperature > 0) temperature = m.Temperature;
            if (nouveauGpu || avant != Etat(memoire, 70) || avantT != Etat(temperature, 80) || avantG != Etat(gpu, 85)) Recalculer();
        }

        private static bool Etat(double v, double seuil) { return v >= 0 && v < seuil; }

        private void Recalculer()
        {
            var P = Habillage.P;
            bool auto = false;
            try { auto = Licence.Premium && ReglagesGardien.Lire().ModeJeuAuto; } catch { }
            // (point, réussi ?, connu ?)
            var points = new List<Tuple<string, bool, bool>>
            {
                Tuple.Create(Langue.T("Mode jeu lancé"), ModeJeuActif(), true),
                Tuple.Create(Licence.Premium ? Langue.T("Mode jeu automatique (gardien)") : Langue.T("Mode jeu automatique (Premium)"), auto, true),
                Tuple.Create(memoire >= 0 ? Langue.F("Mémoire vive : {0} % utilisée", Math.Round(memoire)) : Langue.T("Mémoire vive"), memoire >= 0 && memoire < 70, memoire >= 0),
                Tuple.Create(temperature > 0 ? Langue.F("Processeur : {0} °C", Math.Round(temperature)) : Langue.T("Température : non lue"), temperature > 0 && temperature < 80, temperature > 0),
                Tuple.Create(gpu > 0 ? Langue.F("Carte graphique : {0} °C", Math.Round(gpu)) : Langue.T("Carte graphique : température non lue"), gpu > 0 && gpu < 85, gpu > 0),
                Tuple.Create(indice >= 0 ? Langue.F("Indice du PC : {0} / 100", indice) : Langue.T("Indice du PC : analyse…"), indice >= 80, indice >= 0),
            };
            var connus = points.Where(x => x.Item3).ToList();
            int score = connus.Count == 0 ? 0 : (int)Math.Round(100.0 * connus.Count(x => x.Item2) / connus.Count);
            tScore.Text = score + " %";
            tScore.Foreground = Habillage.Pinceau(score >= 80 ? P.Vert : score >= 50 ? P.Accent : P.Orange);
            checklist.Children.Clear();
            foreach (var p in points)
            {
                var l = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                Color coul = !p.Item3 ? P.Discret : p.Item2 ? P.Vert : P.Orange;
                l.Children.Add(new TextBlock { Text = !p.Item3 ? "·" : p.Item2 ? "✔" : "✖", Width = 14, FontSize = 12.5, Foreground = Habillage.Pinceau(coul), VerticalAlignment = VerticalAlignment.Center });
                l.Children.Add(new TextBlock { Text = p.Item1, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(p.Item3 ? P.Texte : P.Discret), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                checklist.Children.Add(l);
            }
        }

        private void ChargerProgression()
        {
            if (Exemple != null) { Afficher(Exemple); return; }
            Task.Run(() => Succes.Calculer()).ContinueWith(t => { if (!t.IsFaulted) Dispatcher.UIThread.Post(() => Afficher(t.Result)); });
        }

        private void Afficher(ProgressionJeu p)
        {
            var P = Habillage.P;
            tNiveau.Text = Langue.T("NIV.") + " " + p.Niveau;
            tTitreNiveau.Text = Langue.T(p.Titre).ToUpper(Langue.Culture);
            tXp.Text = Langue.F("{0} / {1} XP · encore {2} XP pour le niveau {3}", p.Xp, p.XpSuivant, Math.Max(0, p.XpSuivant - p.Xp), p.Niveau + 1);
            int obtenus = p.Succes.Count(s => s.Obtenu);
            tSucces.Text = Langue.F("SUCCÈS {0} / {1}", obtenus, p.Succes.Count);
            badges.Children.Clear();
            foreach (var s in p.Succes)
            {
                var losange = new Border
                {
                    Width = 13, Height = 13, Margin = new Thickness(0, 0, 9, 4), RenderTransform = new RotateTransform(45),
                    Background = s.Obtenu ? Habillage.Pinceau(P.Accent) : Habillage.Pinceau(P.Discret, 40),
                    BorderBrush = Habillage.Pinceau(s.Obtenu ? P.Accent2 : P.Discret, s.Obtenu ? 255 : 110), BorderThickness = new Thickness(1),
                };
                if (s.Obtenu && P.Sombre) losange.BoxShadow = BoxShadows.Parse("0 0 6 0 " + Habillage.Alpha(P.Accent, 160).ToString());
                ToolTip.SetTip(losange, Langue.T(s.Nom) + (s.Obtenu ? " ✔" : "") + "\n" + Langue.T(s.Condition));
                badges.Children.Add(losange);
            }
            // succès débloqués ou niveau supérieur depuis la dernière fois : annonce façon console
            try { Gamer5.AnnoncerNouveautes(F, p); } catch (Exception ex) { JournalDemarrage.Erreur("annonce des succès", ex); }
            // la barre se remplit en douceur
            double cible = p.Avancement, depart = avancement; long t0 = Horloge.Maintenant;
            Horloge.Lancer(t =>
            {
                double k = Math.Min(1, (t - t0) / 900.0);
                avancement = depart + (cible - depart) * Habillage.Douce(k);
                PlacerXp();
                return k < 1;
            });
        }

        private void PlacerXp()
        {
            double w = Math.Max(0, barreXp.Bounds.Width - 4);
            remplissage.Width = Math.Max(0, Math.Min(1, avancement)) * w;
        }
    }

    /// <summary>Fenêtre « Succès » : chaque succès, débloqué ou non, avec sa condition.</summary>
    internal static class Succes5
    {
        public static void Ouvrir(FenetrePrincipale f)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 12 };
            var tete = new TextBlock { Text = Langue.T("Calcul…"), FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Accent) };
            s.Children.Add(tete);
            s.Children.Add(new TextBlock { Text = Langue.T("Tu gagnes de l'XP en entretenant ton PC : 10 par nettoyage, 1 par 100 Mo libérés, 5 par partie en mode jeu automatique, 20 par semaine entretenue, et un bonus pour un bon indice."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            var grille = new UniformGrid { Columns = 3 };
            s.Children.Add(grille);
            f.Ouvrir("Succès", s, 900);
            Action<ProgressionJeu> afficher = p =>
            {
                tete.Text = Langue.F("NIVEAU {0} · {1} · {2} XP", p.Niveau, Langue.T(p.Titre).ToUpper(Langue.Culture), p.Xp) + "   ·   " + Langue.F("{0} / {1} succès débloqués", p.Succes.Count(x => x.Obtenu), p.Succes.Count);
                foreach (var x in p.Succes)
                {
                    var c = new StackPanel { Spacing = 4 };
                    c.Children.Add(new TextBlock { Text = (x.Obtenu ? "◆  " : "◇  ") + Langue.T(x.Nom), FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(x.Obtenu ? P.Texte : P.Discret) });
                    c.Children.Add(new TextBlock { Text = Langue.T(x.Condition), FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                    c.Children.Add(new TextBlock { Text = Langue.T(x.Obtenu ? "DÉBLOQUÉ" : "VERROUILLÉ"), FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(x.Obtenu ? P.Vert : P.Discret) });
                    grille.Children.Add(new Cadre5
                    {
                        Child = c, Coupe = 10, Padding = new Thickness(14, 12), Margin = new Thickness(0, 0, 10, 10),
                        Background = Habillage.Pinceau(x.Obtenu ? Habillage.Alpha(P.Accent, P.Sombre ? 20 : 14) : P.Carte),
                        BorderBrush = Habillage.Pinceau(x.Obtenu ? P.Accent : P.Bordure, x.Obtenu ? 150 : 255), BorderThickness = new Thickness(1),
                        TraitHaut = x.Obtenu ? P.Accent : (Color?)null,
                    });
                }
            };
            if (QgGamer5.Exemple != null) { afficher(QgGamer5.Exemple); return; }
            Task.Run(() => Succes.Calculer()).ContinueWith(t => { if (!t.IsFaulted) Dispatcher.UIThread.Post(() => afficher(t.Result)); });
        }
    }
}
