// Velox 5.10 - trois outils gratuits :
//  - Ping des serveurs de jeu : latence, stabilité et verdict vers les grandes régions de centres de données ;
//  - Mes jeux : tous les jeux installés (Steam, Epic, Xbox, Riot, Ubisoft, EA, Battle.net, GOG), la place qu'ils prennent,
//    un bouton pour les lancer ou ouvrir leur dossier (rien n'est supprimé par Velox) ;
//  - Minuteur d'extinction : éteindre ou redémarrer le PC dans X minutes, annulable.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    // ================================================================ ping des serveurs de jeu
    internal sealed class PingJeu5 : StackPanel
    {
        /// <summary>(captures) résultats d'exemple : rien n'est mesuré.</summary>
        public static List<ResultatPing> Exemple;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var p = new PingJeu5(f);
            var fen = f.Ouvrir("Ping des serveurs de jeu", p, 860);
            fen.Fermee += () => p.arret = true;
        }

        private readonly FenetrePrincipale F;
        private readonly StackPanel lignes = new StackPanel { Spacing = 4 };
        private readonly TextBlock bilan;
        private readonly BoutonNoyau bMesurer;
        private volatile bool arret;
        private bool enCours;
        private readonly Dictionary<string, ResultatPing> resultats = new Dictionary<string, ResultatPing>();

        private PingJeu5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("Mesure le temps de réponse (ping) entre ton PC et les grandes régions de serveurs où sont hébergés de nombreux jeux en ligne. Plus le chiffre est petit, moins il y a de retard en jeu. Choisis dans tes jeux la région la plus rapide."));
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            bilan = new TextBlock { Text = Langue.T("Clique sur « Mesurer » : environ 15 secondes."), FontFamily = Habillage.Affichage, FontSize = 16.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            haut.Children.Add(bilan);
            bMesurer = new BoutonNoyau("Mesurer", true, 170, 40) { TaillePolice = 14.5, Margin = new Thickness(14, 0, 0, 0) };
            bMesurer.Clic += Mesurer;
            Grid.SetColumn(bMesurer, 1); haut.Children.Add(bMesurer);
            Children.Add(haut);
            Children.Add(Premium5.Carte(lignes, Habillage.Alpha(P.Accent, 70)));
            Children.Add(Premium5.Intro("Mesure faite en ouvrant une connexion vers les centres de données d'Amazon Web Services de chaque région (rien d'autre n'est envoyé). Le ping réel dans un jeu dépend aussi de ses propres serveurs : sers-toi de ce tableau pour comparer les régions entre elles. En Wi-Fi, un câble réseau donne presque toujours un ping plus bas et plus stable."));
            if (Exemple != null) { foreach (var r in Exemple) resultats[r.Cible] = r; }
            Afficher(null);
            if (Exemple != null) Conclure();
        }

        private void Afficher(string enMesure)
        {
            var P = Habillage.P;
            lignes.Children.Clear();
            double pire = Math.Max(120, resultats.Values.Where(r => r.Moyenne >= 0).Select(r => r.Min).DefaultIfEmpty(0).Max());
            string groupe = "";
            foreach (var reg in Reseau.RegionsJeu)
            {
                if (reg[1] != groupe)
                {
                    groupe = reg[1];
                    lignes.Children.Add(new TextBlock { Text = Langue.T(groupe).ToUpper(Langue.Culture), FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Discret), Margin = new Thickness(0, lignes.Children.Count == 0 ? 0 : 8, 0, 2) });
                }
                ResultatPing r; resultats.TryGetValue(Reseau.HoteRegion(reg[2]), out r);
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("190,80,*,1.6*"), Height = 26 };
                g.Children.Add(new TextBlock { Text = Langue.T(reg[0]), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                bool ok = r != null && r.Moyenne >= 0;
                Color c = !ok ? P.Discret : r.Min <= 30 ? P.Vert : r.Min <= 100 ? P.Accent : P.Orange;
                var ms = new TextBlock { Text = r == null ? (enMesure == reg[2] ? "…" : "—") : ok ? Math.Round(r.Min) + " ms" : "—", FontFamily = Habillage.Console, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(ok ? c : P.Discret), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 14, 0) };
                Grid.SetColumn(ms, 1); g.Children.Add(ms);
                var fond = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Habillage.Pinceau(P.Bordure), VerticalAlignment = VerticalAlignment.Center };
                var piste = new Grid(); piste.Children.Add(fond);
                if (ok)
                {
                    double part = Math.Max(0.02, Math.Min(1, r.Min / pire));
                    var jauge = new Grid();
                    jauge.ColumnDefinitions.Add(new ColumnDefinition(part, GridUnitType.Star));
                    jauge.ColumnDefinitions.Add(new ColumnDefinition(1 - part, GridUnitType.Star));
                    jauge.Children.Add(new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Habillage.Pinceau(c), VerticalAlignment = VerticalAlignment.Center });
                    piste.Children.Add(jauge);
                }
                Grid.SetColumn(piste, 2); g.Children.Add(piste);
                string verdict = r == null ? "" : Langue.T(Reseau.VerdictJeu(ok ? r.Min : -1, r.Gigue, r.Perte)) + (ok && r.Gigue >= 1 ? "  ·  ±" + Math.Round(r.Gigue) + " ms" : "");
                var tv = new TextBlock { Text = verdict, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(tv, 3); g.Children.Add(tv);
                lignes.Children.Add(g);
            }
        }

        private void Mesurer()
        {
            if (enCours || Exemple != null) return;
            enCours = true; arret = false; resultats.Clear();
            bMesurer.IsEnabled = false;
            bilan.Text = Langue.T("Mesure en cours…");
            Afficher(Reseau.RegionsJeu[0][2]);
            Task.Run(() =>
            {
                for (int i = 0; i < Reseau.RegionsJeu.Length && !arret; i++)
                {
                    var reg = Reseau.RegionsJeu[i];
                    ResultatPing r;
                    try { r = Reseau.PingTcp(Reseau.HoteRegion(reg[2]), reg[0]); } catch { r = new ResultatPing { Cible = Reseau.HoteRegion(reg[2]), Nom = reg[0], Perte = 100 }; }
                    string suivant = i + 1 < Reseau.RegionsJeu.Length ? Reseau.RegionsJeu[i + 1][2] : null;
                    Dispatcher.UIThread.Post(() => { if (arret) return; resultats[r.Cible] = r; Afficher(suivant); });
                }
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false;
                if (arret) return;
                bMesurer.IsEnabled = true; bMesurer.Changer("Mesurer à nouveau");
                Conclure();
            }));
        }

        private void Conclure()
        {
            var bons = resultats.Values.Where(r => r.Moyenne >= 0).OrderBy(r => r.Min).ToList();
            if (bons.Count == 0) { bilan.Text = Langue.T("Aucune région n'a répondu : vérifie ta connexion Internet (ou ton pare-feu), puis réessaie."); return; }
            var m = bons[0];
            bilan.Text = Langue.F("Région la plus rapide : {0}, {1} ms.", Langue.T(m.Nom), Math.Round(m.Min)) + " " + Langue.T(Reseau.VerdictJeu(m.Min, m.Gigue, m.Perte)) + ".";
            if (Exemple == null) Task.Run(() => JournalJeu.Noter("ping"));
        }
    }

    // ================================================================ mes jeux
    internal sealed class MesJeux5 : StackPanel
    {
        /// <summary>(captures) jeux d'exemple : rien n'est lu.</summary>
        public static List<Jeu> Exemple;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var p = new MesJeux5(f);
            var fen = f.Ouvrir("Mes jeux", p, 920);
            fen.Fermee += () => p.arret = true;
        }

        private readonly FenetrePrincipale F;
        private readonly Tableau tableau;
        private readonly TextBlock resume;
        private readonly BoutonNoyau bLancer, bDossier;
        private List<Jeu> jeux = new List<Jeu>();
        private volatile bool arret;

        private MesJeux5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("Tous tes jeux installés, toutes plateformes confondues, avec la place qu'ils prennent sur le disque. Clique sur « Place » pour trier : les plus gros en premier, pour savoir lequel retirer quand le disque est plein."));
            resume = new TextBlock { Text = Langue.T("Recherche des jeux installés…"), FontFamily = Habillage.Affichage, FontSize = 16.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            Children.Add(resume);
            tableau = new Tableau(false, false, new Colonne("Jeu", "2.4*"), new Colonne("Plateforme", "1.1*"),
                new Colonne("Place", "110", true, true, o => -((Jeu)o).Taille), new Colonne("Disque", "70", false, true));
            tableau.HauteurListe = 380;
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            var boutons = new WrapPanel { Orientation = Orientation.Horizontal };
            bLancer = new BoutonNoyau("Lancer le jeu", true, 0, 36) { TaillePolice = 13.5, IsEnabled = false, Margin = new Thickness(0, 0, 10, 6) };
            bDossier = new BoutonNoyau("Ouvrir son dossier", false, 0, 36) { TaillePolice = 13.5, IsEnabled = false, Margin = new Thickness(0, 0, 10, 6) };
            var bDesinstaller = new BoutonNoyau("Désinstaller un jeu…", false, 0, 36) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 10, 6) };
            ToolTip.SetTip(bDesinstaller, Langue.T("Ouvre la liste des applications de Windows : c'est Windows (ou le lanceur du jeu) qui désinstalle proprement, Velox ne supprime aucun jeu."));
            bLancer.Clic += () => { var j = tableau.Selection as Jeu; if (j != null) Lancer(j); };
            bDossier.Clic += () => { var j = tableau.Selection as Jeu; if (j != null && j.Dossier != "" && Directory.Exists(j.Dossier)) { try { Programmes.Ouvrir(j.Dossier); } catch { } } };
            bDesinstaller.Clic += () => { try { Programmes.Ouvrir("ms-settings:appsfeatures"); } catch { } };
            boutons.Children.Add(bLancer); boutons.Children.Add(bDossier); boutons.Children.Add(bDesinstaller);
            Children.Add(boutons);
            tableau.SelectionChangee += () =>
            {
                var j = tableau.Selection as Jeu;
                bLancer.IsEnabled = j != null && j.Lancer != "";
                bDossier.IsEnabled = j != null && j.Dossier != "";
            };
            tableau.DoubleClic += o => { var j = o as Jeu; if (j != null && j.Lancer != "") Lancer(j); };
            if (Exemple != null) { jeux = Exemple; Afficher(false); return; }
            Task.Run(() => { try { return DetectionJeux.Lister(); } catch { return new List<Jeu>(); } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (arret) return;
                jeux = t.Result ?? new List<Jeu>();
                Afficher(jeux.Any(j => j.Taille <= 0 && j.Dossier != ""));
                CalculerTailles();
            }));
        }

        private void Lancer(Jeu j)
        {
            F.Demander(Langue.F("Lancer {0} ?", j.Nom), Langue.T("Le jeu s'ouvre avec son lanceur habituel."), "Lancer", "Annuler", oui =>
            {
                if (!oui) return;
                try { Programmes.Ouvrir(j.Lancer, j.Arguments == "" ? null : j.Arguments); F.Statut(Langue.F("{0} se lance…", j.Nom)); }
                catch (Exception ex) { F.Statut(Langue.T("Impossible de lancer ce jeu :") + " " + ex.Message, true); }
            });
        }

        /// <summary>Les jeux dont le lanceur ne donne pas la taille : leur dossier est mesuré, en lecture seule, l'un après l'autre.</summary>
        private void CalculerTailles()
        {
            var aMesurer = jeux.Where(j => j.Taille <= 0 && j.Dossier != "").ToList();
            if (aMesurer.Count == 0) return;
            Task.Run(() =>
            {
                foreach (var j in aMesurer)
                {
                    if (arret) return;
                    double t = DetectionJeux.TailleDossier(j.Dossier, () => arret);
                    var jj = j;
                    Dispatcher.UIThread.Post(() => { if (arret) return; jj.Taille = t > 0 ? t : 0; Afficher(jj != aMesurer[aMesurer.Count - 1]); });
                }
            });
        }

        private static string Lecteur(Jeu j) { try { return j.Dossier.Length >= 2 && j.Dossier[1] == ':' ? j.Dossier.Substring(0, 2).ToUpperInvariant() : ""; } catch { return ""; } }

        private void Afficher(bool calcul)
        {
            var sel = tableau.Selection;
            tableau.Remplir(jeux.Cast<object>(), o => { var j = (Jeu)o; return new[] { j.Nom, j.Plateforme, j.Taille > 0 ? Format.Taille(j.Taille) : (calcul && j.Dossier != "" ? "…" : "—"), Lecteur(j) }; });
            if (sel != null) tableau.Selectionner(sel);
            if (jeux.Count == 0) { resume.Text = Langue.T("Aucun jeu trouvé (Steam, Epic, Xbox, Riot, Ubisoft, EA, Battle.net, GOG)."); return; }
            double total = jeux.Sum(j => Math.Max(0, j.Taille));
            var gros = jeux.OrderByDescending(j => j.Taille).First();
            resume.Text = Langue.F("{0} jeu(x) installé(s) : {1} au total.", jeux.Count, Format.Taille(total)) + (gros.Taille > 0 ? " " + Langue.F("Le plus gros : {0} ({1}).", gros.Nom, Format.Taille(gros.Taille)) : "");
        }
    }

    // ================================================================ minuteur d'extinction
    internal sealed class Extinction5 : StackPanel
    {
        /// <summary>(captures) pas de lecture de l'état réel.</summary>
        public static bool Exemple;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var p = new Extinction5(f);
            var fen = f.Ouvrir("Minuteur d'extinction", p, 720);
            fen.Fermee += () => p.minuteur.Stop();
        }

        private readonly FenetrePrincipale F;
        private readonly TextBlock etat;
        private readonly BoutonNoyau bAnnuler;
        private readonly DispatcherTimer minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        private int minutes = 60;
        private bool redemarrer;
        private readonly List<Border> choix = new List<Border>();
        private static readonly int[] Durees = { 15, 30, 60, 90, 120, 180 };
        private Border cEteindre, cRedemarrer;

        private Extinction5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("Éteins ou redémarre ton PC tout seul dans un moment : pratique pour laisser finir un téléchargement, une mise à jour de jeu ou un film. Tu peux annuler à tout moment, et Windows prévient avant de s'éteindre."));
            etat = new TextBlock { FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            Children.Add(etat);
            Children.Add(Premium5.Titre("Dans combien de temps ?"));
            var durees = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (int d in Durees)
            {
                int dd = d;
                var b = Pastille(d < 60 ? d + " min" : (d % 60 == 0 ? (d / 60) + " h" : (d / 60) + " h " + (d % 60)), () => { minutes = dd; Marquer(); });
                b.Tag = d; choix.Add(b); durees.Children.Add(b);
            }
            Children.Add(durees);
            Children.Add(Premium5.Titre("Que faire ?"));
            var actions = new WrapPanel { Orientation = Orientation.Horizontal };
            cEteindre = Pastille(Langue.T("Éteindre"), () => { redemarrer = false; Marquer(); });
            cRedemarrer = Pastille(Langue.T("Redémarrer"), () => { redemarrer = true; Marquer(); });
            actions.Children.Add(cEteindre); actions.Children.Add(cRedemarrer);
            Children.Add(actions);
            var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
            var bGo = new BoutonNoyau("Programmer", true, 0, 42) { TaillePolice = 15 };
            bGo.Clic += Programmer;
            bAnnuler = new BoutonNoyau("Annuler l'extinction prévue", false, 0, 42) { TaillePolice = 13.5 };
            bAnnuler.Clic += () =>
            {
                Task.Run(() => Extinction.Annuler()).ContinueWith(t => Dispatcher.UIThread.Post(() => { F.Statut(Langue.T("Extinction annulée : ton PC reste allumé.")); MajEtat(); }));
            };
            boutons.Children.Add(bGo); boutons.Children.Add(bAnnuler);
            Children.Add(boutons);
            Children.Add(Premium5.Intro("Pense à enregistrer ton travail avant : à l'heure prévue, Windows ferme les applications ouvertes."));
            Marquer(); MajEtat();
            minuteur.Tick += (s, e) => MajEtat();
            minuteur.Start();
        }

        private static Border Pastille(string texte, Action clic)
        {
            var P = Habillage.P;
            var b = new Border
            {
                Child = new TextBlock { Text = texte, FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) },
                Padding = new Thickness(18, 9), Margin = new Thickness(0, 0, 10, 8), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1.5), Background = Habillage.Pinceau(P.Carte),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
            };
            b.PointerReleased += (o, e) => clic();
            return b;
        }

        private void Marquer()
        {
            var P = Habillage.P;
            Action<Border, bool> m = (b, oui) => { b.BorderBrush = Habillage.Pinceau(oui ? P.Accent : P.Bordure); b.Background = Habillage.Pinceau(oui ? Habillage.Melanger(P.Carte, P.Accent, 0.10) : P.Carte); };
            foreach (var b in choix) m(b, (int)b.Tag == minutes);
            m(cEteindre, !redemarrer); m(cRedemarrer, redemarrer);
        }

        private void MajEtat()
        {
            var p = Exemple ? Tuple.Create(DateTime.Today.AddHours(23).AddMinutes(40), false) : Extinction.Prevue();
            etat.Text = p == null ? Langue.T("Aucune extinction prévue.") : Langue.F(p.Item2 ? "Redémarrage prévu à {0}." : "Extinction prévue à {0}.", p.Item1.ToString("HH:mm"));
            etat.Foreground = Habillage.Pinceau(p == null ? Habillage.P.Texte : Habillage.P.Orange);
            bAnnuler.IsEnabled = p != null;
        }

        private void Programmer()
        {
            if (Exemple) return;
            string quand = DateTime.Now.AddMinutes(minutes).ToString("HH:mm");
            F.Demander(Langue.F(redemarrer ? "Redémarrer le PC à {0} ?" : "Éteindre le PC à {0} ?", quand),
                Langue.T("Enregistre ton travail avant l'heure prévue. Tu peux annuler à tout moment depuis cette fenêtre."), "Programmer", "Annuler", oui =>
            {
                if (!oui) return;
                int m = minutes; bool r = redemarrer;
                Task.Run(() => Extinction.Programmer(m, r)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    string err = t.IsFaulted ? "erreur" : t.Result;
                    if (err == "") F.Statut(Langue.F(r ? "Redémarrage programmé à {0}." : "Extinction programmée à {0}.", quand));
                    else F.Statut(Langue.T("Minuteur non programmé :") + " " + err, true);
                    MajEtat();
                }));
            }, true);
        }
    }
}
