// Velox 5.12 :
//  - Boîte noire du jeu (Premium, avec les FPS en direct) : enregistre la partie, trouve la cause des saccades, rapport de fin de partie ;
//  - « Qui ralentit mon PC ? » (gratuit) : observe le PC 12 secondes et désigne le programme qui le ralentit en ce moment.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    // ================================================================ Boîte noire du jeu
    internal static class BoiteNoire5
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

        /// <summary>(captures) partie d'exemple : rien n'est lu.</summary>
        public static SessionBoite Exemple;

        private static DispatcherTimer minuteur;
        private static bool enCours, actif;
        /// <summary>Dernier résultat des FPS en direct (lu une fois par seconde ici, et affiché par l'overlay) :
        /// plus de 0 = images par seconde, -2 = pas de nouvelle image, -1 = rien.</summary>
        public static double FpsActuel = -1;

        /// <summary>À l'ouverture de Velox : la Boîte noire enregistre dès que les FPS en direct tournent.</summary>
        public static void Demarrer(FenetrePrincipale f)
        {
            if (minuteur != null || f.Source.Immediat || !OperatingSystem.IsWindows()) return;
            BoiteNoire.PartieFinie = s => Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    f.Statut(Langue.F("Boîte noire : partie de {0} enregistrée ({1} saccade(s), note {2}/100). Rapport dans le QG Gamer.", BoiteNoire.NomJeu(s.Jeu), s.Saccades.Count, s.Note));
                    Task.Run(() => JournalJeu.Noter("boitenoire"));
                }
                catch { }
            });
            minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            minuteur.Tick += (s, e) => Seconde(f);
            minuteur.Start();
            f.Closed += (s, e) => { try { minuteur.Stop(); BoiteNoire.Terminer(); } catch { } };
        }

        private static void Seconde(FenetrePrincipale f)
        {
            if (!Fps.DirectActif)
            {
                if (actif) { actif = false; FpsActuel = -1; Task.Run(() => BoiteNoire.Terminer()); }
                return;
            }
            actif = true;
            if (enCours) return;
            enCours = true;
            Task.Run(() =>
            {
                Mesures m = null; double g = -1, usage = -1, fps = -1; string app = ""; uint pid = 0;
                try { m = f.Source.Mesurer(true); } catch { }
                try { g = TemperatureGpu.Lire(); } catch { }
                try { usage = UsageGpu.Lire(); } catch { }
                try { GetWindowThreadProcessId(GetForegroundWindow(), out pid); } catch { }
                try { fps = Fps.LireDirect(Fps.CsvDirect, (int)pid, out app); } catch { fps = -1; }
                var images = Fps.ImagesDirect; int pidJeu = Fps.PidDirect;
                var e = new SecondeBoite { Heure = DateTime.Now, TempGpu = g, Gpu = usage };
                if (m != null) { e.Cpu = m.Processeur; e.Ram = m.Memoire; e.TempCpu = m.Temperature; }
                try
                {
                    var genant = BoiteNoire.PlusGenant(ActiviteProgrammes.Lire(), pidJeu, app);
                    if (genant != null) { e.Programme = genant.Nom; e.ProgrammeCoeur = genant.Coeur; e.ProgrammeDisque = genant.Disque; }
                    e.Disque = ActiviteProgrammes.Disque();
                    BoiteNoire.SeuilCpu = Overlay5.SeuilCpu();
                    // seules comptent les images du programme au premier plan, et jamais celles d'un navigateur ou d'un lanceur
                    bool jeuDevant = fps > 0 && pidJeu != 0 && pidJeu == (int)pid && EstUnJeu(app);
                    BoiteNoire.Seconde(e, jeuDevant ? images : null, app, pidJeu);
                }
                catch (Exception ex) { JournalDemarrage.Erreur("boîte noire", ex); }
                return fps;
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false;
                if (!t.IsFaulted) FpsActuel = t.Result;
            }));
        }

        /// <summary>Un programme qui affiche des images n'est pas forcément un jeu : navigateurs, lanceurs, Discord, l'Explorateur… sont écartés.</summary>
        public static bool EstUnJeu(string application)
        {
            string nom = BoiteNoire.NomJeu(application);
            if (nom == "") return false;
            return ActiviteProgrammes.NomLisible(nom) == nom && !ActiviteProgrammes.Ignore(nom);
        }

        // ---------------------------------------------------------------- rapport
        public static void Ouvrir(FenetrePrincipale f)
        {
            var contenu = new StackPanel { Spacing = 12 };
            Remplir(f, contenu, null);
            f.Ouvrir("Boîte noire du jeu", contenu, 860);
        }

        private static string Duree(TimeSpan d) { return d.TotalHours >= 1 ? string.Format("{0} h {1:00}", (int)d.TotalHours, d.Minutes) : Math.Max(1, (int)Math.Round(d.TotalMinutes)) + " min"; }

        private static string Etiquette(Saccade s)
        {
            switch (s.Cause)
            {
                case "chaleur": return Langue.T(s.Programme == "carte graphique" ? "carte graphique trop chaude" : "processeur trop chaud");
                case "programme": return Langue.T(ActiviteProgrammes.NomLisible(s.Programme));
                case "memoire": return Langue.T("mémoire vive pleine");
                case "disque": return Langue.T("disque très occupé");
                case "processeur": return Langue.T("processeur à fond");
            }
            return Langue.T("le jeu lui-même");
        }

        private static Control Tuile(string nom, string valeur, Color? couleur = null)
        {
            var P = Habillage.P;
            var c = new StackPanel { Spacing = 2 };
            c.Children.Add(new TextBlock { Text = Langue.T(nom), FontFamily = Habillage.Console, FontSize = 10.5, LetterSpacing = 1, Foreground = Habillage.Pinceau(P.Discret) });
            c.Children.Add(new TextBlock { Text = valeur, FontFamily = Habillage.Affichage, FontSize = 26, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(couleur ?? P.Texte) });
            return new Cadre5 { Child = c, Coupe = 10, Padding = new Thickness(14, 10), Margin = new Thickness(0, 0, 10, 10), Background = Habillage.Pinceau(P.Accent, P.Sombre ? 14 : 10), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1) };
        }

        private static void Remplir(FenetrePrincipale f, StackPanel s, SessionBoite choisie)
        {
            var P = Habillage.P;
            s.Children.Clear();
            s.Children.Add(Premium5.Intro("Pendant que les FPS en direct tournent, Velox note chaque seconde de ta partie. À chaque saccade, il regarde ce qui se passait sur le PC à cet instant et te dit pourquoi. Rien n'est fermé ni modifié, et tout reste sur ton PC."));

            List<SessionBoite> parties = Exemple != null ? new List<SessionBoite> { Exemple } : BoiteNoire.Parties();
            var courante = Exemple != null ? null : BoiteNoire.Courante;
            if (courante != null)
                s.Children.Add(new TextBlock { Text = "● " + Langue.F("Enregistrement en cours : {0}, {1}, {2} saccade(s) pour l'instant.", BoiteNoire.NomJeu(courante.Jeu), Duree(courante.Duree), courante.Saccades.Count), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Vert), TextWrapping = TextWrapping.Wrap });

            if (parties.Count == 0)
            {
                s.Children.Add(new TextBlock { Text = Langue.T(courante != null ? "Le rapport apparaîtra ici à la fin de la partie (2 minutes après avoir quitté le jeu)." : Fps.DirectActif ? "Les FPS en direct tournent : lance un jeu, la Boîte noire enregistre toute seule. Une partie est gardée à partir d'une minute de jeu." : "Aucune partie enregistrée pour l'instant. Démarre les FPS en direct, puis lance ton jeu : la Boîte noire enregistre toute seule."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                if (!Fps.DirectActif)
                {
                    var demarrer = new BoutonNoyau("Démarrer les FPS en direct", true, 0, 42) { TaillePolice = 14, Capitales = true, HorizontalAlignment = HorizontalAlignment.Left };
                    demarrer.Clic += () => Overlay5.BasculerFps(f, ok => { if (ok) Remplir(f, s, null); });
                    s.Children.Add(demarrer);
                }
                return;
            }

            var p = choisie ?? parties[0];
            int niveau;
            string verdict = BoiteNoire.Verdict(p, out niveau);
            s.Children.Add(new TextBlock { Text = BoiteNoire.NomJeu(p.Jeu) + "  ·  " + p.Debut.ToString("d MMM, HH:mm", Langue.Culture) + "  ·  " + Duree(p.Duree), FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret) });
            s.Children.Add(new TextBlock { Text = Langue.T(verdict), FontFamily = Habillage.Affichage, FontSize = 19, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(niveau == 0 ? P.Vert : niveau == 1 ? P.Texte : P.Orange), TextWrapping = TextWrapping.Wrap });

            var grille = new Avalonia.Controls.Primitives.UniformGrid { Columns = 3 };
            grille.Children.Add(Tuile("NOTE DE FLUIDITÉ", p.Note >= 0 ? p.Note + " / 100" : "—", p.Note >= 80 ? P.Vert : p.Note >= 55 ? P.Texte : P.Orange));
            grille.Children.Add(Tuile("FPS MOYEN", p.FpsMoyen > 0 ? Math.Round(p.FpsMoyen).ToString() : "—"));
            grille.Children.Add(Tuile("FPS MINIMUM (1 %)", p.Fps1 > 0 ? Math.Round(p.Fps1).ToString() : "—"));
            grille.Children.Add(Tuile("SACCADES", p.Saccades.Count.ToString()));
            grille.Children.Add(Tuile("TEMP. PROCESSEUR MAX", p.TempCpuMax > 0 ? Math.Round(p.TempCpuMax) + " °C" : "—"));
            grille.Children.Add(Tuile("TEMP. CARTE GRAPHIQUE MAX", p.TempGpuMax > 0 ? Math.Round(p.TempGpuMax) + " °C" : "—"));
            s.Children.Add(grille);

            var causes = BoiteNoire.Causes(p);
            if (causes.Count > 0)
            {
                s.Children.Add(Premium5.Titre("Ce qui a fait saccader ta partie"));
                foreach (var c in causes)
                {
                    var bloc = new StackPanel { Spacing = 6 };
                    string titre = Langue.F(BoiteNoire.Titre(c), c.Nombre, Langue.T(ActiviteProgrammes.NomLisible(c.Programme)));
                    bloc.Children.Add(new TextBlock { Text = titre, FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                    bloc.Children.Add(new TextBlock { Text = Langue.F("La plus longue : {0} ms.", Math.Round(c.PireMs)) + " " + Langue.T(BoiteNoire.Conseil(c)), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                    string bouton, ou = BoiteNoire.Correction(c, out bouton);
                    if (ou != "" && Exemple == null)
                    {
                        var b = new BoutonNoyau(bouton, false, 0, 34) { TaillePolice = 12.5, HorizontalAlignment = HorizontalAlignment.Left };
                        string cible = ou;
                        b.Clic += () => Corriger(f, cible);
                        bloc.Children.Add(b);
                    }
                    else if (ou != "") bloc.Children.Add(new BoutonNoyau(bouton, false, 0, 34) { TaillePolice = 12.5, HorizontalAlignment = HorizontalAlignment.Left });
                    s.Children.Add(new Border { Child = bloc, Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), BorderBrush = Habillage.Pinceau(P.Bordure), Background = Habillage.Pinceau(P.Accent, P.Sombre ? 8 : 6) });
                }

                s.Children.Add(Premium5.Titre("Le film de la partie"));
                var film = new StackPanel { Spacing = 3 };
                foreach (var k in p.Saccades.Take(40))
                    film.Children.Add(new TextBlock { Text = k.Heure.ToString("HH:mm:ss") + "   " + Math.Round(k.Ms).ToString().PadLeft(4) + " ms   " + Etiquette(k), FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(k.Cause == "jeu" ? P.Discret : P.Texte) });
                if (p.Saccades.Count > 40) film.Children.Add(new TextBlock { Text = Langue.F("… et {0} autre(s).", p.Saccades.Count - 40), FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret) });
                s.Children.Add(film);
            }

            string texte = Resume(p);
            var boutons = new WrapPanel();
            var copier = new BoutonNoyau("Copier pour Discord", true, 0, 40) { TaillePolice = 13.5, Capitales = true, Margin = new Thickness(0, 4, 8, 0) };
            copier.Clic += async () => { try { if (f.Clipboard != null) await f.Clipboard.SetTextAsync(texte); f.Statut(Langue.T("Rapport copié : colle-le sur Discord avec Ctrl+V.")); } catch { } };
            boutons.Children.Add(copier);
            if (Exemple == null)
            {
                var fps = new BoutonNoyau(Fps.DirectActif ? "Arrêter les FPS en direct" : "Démarrer les FPS en direct", false, 0, 40) { TaillePolice = 13.5, Margin = new Thickness(0, 4, 8, 0) };
                fps.Clic += () => Overlay5.BasculerFps(f, ok => Remplir(f, s, null));
                boutons.Children.Add(fps);
            }
            s.Children.Add(boutons);

            if (parties.Count > 1)
            {
                s.Children.Add(Premium5.Titre("Parties précédentes"));
                foreach (var autre in parties.Take(12))
                {
                    var a = autre;
                    var ligne = new BoutonNoyau(a.Debut.ToString("d MMM HH:mm", Langue.Culture) + "  ·  " + BoiteNoire.NomJeu(a.Jeu) + "  ·  " + Duree(a.Duree) + "  ·  " + Langue.F("{0} saccade(s)", a.Saccades.Count) + "  ·  " + a.Note + "/100", a == p, 0, 34) { TaillePolice = 12, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 0, 4) };
                    ligne.Clic += () => Remplir(f, s, a);
                    s.Children.Add(ligne);
                }
            }
        }

        private static void Corriger(FenetrePrincipale f, string ou)
        {
            try
            {
                if (ou.StartsWith("action:")) { f.ViderCalques(); f.LancerAction(ou.Substring(7)); }
                else if (ou.StartsWith("outil:")) { f.ViderCalques(); f.OuvrirOutil(ou.Substring(6)); }
                else if (ou.StartsWith("uri:")) Programmes.Ouvrir(ou.Substring(4));
            }
            catch { }
        }

        /// <summary>Résumé d'une partie, à coller sur Discord.</summary>
        public static string Resume(SessionBoite p)
        {
            var l = new List<string>
            {
                "🎮 " + BoiteNoire.NomJeu(p.Jeu) + " · " + Duree(p.Duree),
                "• " + Langue.T("FPS MOYEN") + " : " + Math.Round(p.FpsMoyen) + "  ·  " + Langue.T("FPS MINIMUM (1 %)") + " : " + Math.Round(p.Fps1),
                "• " + Langue.T("SACCADES") + " : " + p.Saccades.Count + "  ·  " + Langue.T("NOTE DE FLUIDITÉ") + " : " + p.Note + "/100",
            };
            if (p.TempCpuMax > 0 || p.TempGpuMax > 0)
                l.Add("• " + Langue.T("TEMP. PROCESSEUR MAX") + " : " + (p.TempCpuMax > 0 ? Math.Round(p.TempCpuMax) + " °C" : "—") + "  ·  " + Langue.T("TEMP. CARTE GRAPHIQUE MAX") + " : " + (p.TempGpuMax > 0 ? Math.Round(p.TempGpuMax) + " °C" : "—"));
            l.Add(Langue.T("Analysé par la Boîte noire de Velox · damiencoussot-ai.github.io/nettoyage-pc"));
            return string.Join("\n", l);
        }
    }

    // ================================================================ « Qui ralentit mon PC ? »
    internal sealed class Coupable5 : StackPanel
    {
        /// <summary>(captures) résultat d'exemple : rien n'est mesuré.</summary>
        public static ResultatCoupable Exemple;
        private const int Secondes = 12;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var p = new Coupable5(f);
            var fen = f.Ouvrir("Qui ralentit mon PC ?", p, 780);
            fen.Fermee += () => p.ferme = true;
        }

        private readonly FenetrePrincipale F;
        private readonly TextBlock verdict;
        private readonly StackPanel resultats = new StackPanel { Spacing = 10 };
        private readonly Border jauge = new Border { Height = 8, CornerRadius = new CornerRadius(4), HorizontalAlignment = HorizontalAlignment.Left, Width = 0 };
        private readonly Grid piste = new Grid { Height = 8 };
        private bool ferme, enCours;
        private double avancement;

        private Coupable5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("Ton PC rame maintenant ? Velox l'observe 12 secondes et te dit quel programme occupe le processeur, la mémoire ou le disque en ce moment. Lecture seule : rien n'est fermé ni modifié."));
            verdict = new TextBlock { Text = Langue.T("Observation du PC…"), FontFamily = Habillage.Affichage, FontSize = 19, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            Children.Add(verdict);
            piste.Children.Add(new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = Habillage.Pinceau(P.Bordure) });
            jauge.Background = Habillage.Pinceau(P.Accent);
            piste.Children.Add(jauge);
            piste.SizeChanged += (s, e) => jauge.Width = Math.Max(0, piste.Bounds.Width * avancement);
            Children.Add(piste);
            Children.Add(resultats);
            if (Exemple != null) { avancement = 1; Afficher(Exemple); }
            else Observer();
        }

        private void Observer()
        {
            if (enCours) return;
            enCours = true;
            resultats.Children.Clear();
            piste.IsVisible = true; avancement = 0; jauge.Width = 0;
            verdict.Text = Langue.T("Observation du PC…");
            verdict.Foreground = Habillage.Pinceau(Habillage.P.Texte);
            Func<SecondeBoite> etat = () =>
            {
                var m = F.Source.Mesurer(true);
                return m == null ? null : new SecondeBoite { Heure = DateTime.Now, Cpu = m.Processeur, Ram = m.Memoire, TempCpu = m.Temperature };
            };
            Task.Run(() => Coupable.Observer(Secondes, etat, a => Dispatcher.UIThread.Post(() => { avancement = a; jauge.Width = Math.Max(0, piste.Bounds.Width * a); }), () => ferme))
                .ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    enCours = false;
                    if (ferme) return;
                    if (t.IsFaulted) { verdict.Text = Langue.T("L'observation n'a pas abouti. Réessaie dans un instant."); return; }
                    Afficher(t.Result);
                }));
        }

        private static string Conseil(string programme, int niveau)
        {
            if (programme == "") return niveau == 0 ? "Si le PC te semble lent quand même, le ralentissement vient peut-être d'un seul programme qui ne répond plus, ou de la connexion Internet." : "";
            switch (ActiviteProgrammes.Famille(programme))
            {
                case "antivirus": return "C'est l'antivirus de Windows qui analyse des fichiers. Laisse-le finir : c'est passager, et il ne faut pas le désactiver.";
                case "windowsupdate": return "Windows installe une mise à jour. Laisse-le finir, puis redémarre le PC s'il le demande.";
                case "indexation": return "Windows met à jour son index de recherche. C'est passager, surtout après avoir copié beaucoup de fichiers.";
                case "onedrive": return "La synchronisation de tes fichiers travaille. Elle se calme une fois tout envoyé ; tu peux aussi la mettre en pause depuis son icône.";
                case "navigateur": return "C'est ton navigateur. Ferme les onglets dont tu n'as pas besoin, surtout ceux avec des vidéos, ou redémarre-le.";
                case "lanceur": return "Le lanceur de jeux télécharge ou installe sans doute quelque chose. Mets-le en pause si tu as besoin du PC maintenant.";
                case "windows": return "C'est un service de Windows. Si ça dure plus de quelques minutes, redémarre le PC.";
            }
            return "Si tu n'as pas besoin de ce programme maintenant, ferme-le. S'il reste actif sans raison, redémarre-le.";
        }

        private void Afficher(ResultatCoupable r)
        {
            var P = Habillage.P;
            piste.IsVisible = false;
            int niveau; string programme;
            string v = Coupable.Verdict(r, out niveau, out programme);
            verdict.Text = Langue.F(v, Langue.T(ActiviteProgrammes.NomLisible(programme)));
            verdict.Foreground = Habillage.Pinceau(niveau == 0 ? P.Vert : niveau == 1 ? P.Texte : P.Orange);
            resultats.Children.Clear();
            string conseil = Conseil(programme, niveau);
            if (conseil != "") resultats.Children.Add(new TextBlock { Text = Langue.T(conseil), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });

            var infos = new List<string>();
            if (r.Cpu >= 0) infos.Add(Langue.F("processeur {0} %", Math.Round(r.Cpu)));
            if (r.Ram >= 0) infos.Add(Langue.F("mémoire vive {0} %", Math.Round(r.Ram)));
            if (r.Disque >= 0) infos.Add(Langue.F("disque occupé à {0} %", Math.Round(r.Disque)));
            if (r.TempCpu > 0) infos.Add(Langue.F("processeur à {0} °C", Math.Round(r.TempCpu)));
            resultats.Children.Add(new TextBlock { Text = string.Join("  ·  ", infos), FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });

            if (r.Programmes.Count > 0)
            {
                resultats.Children.Add(Premium5.Titre("Les programmes les plus actifs"));
                foreach (var p in r.Programmes)
                {
                    string lisible = ActiviteProgrammes.NomLisible(p.Nom);
                    var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                    g.Children.Add(new TextBlock { Text = Langue.T(lisible) + (lisible != p.Nom ? "  (" + p.Nom + ")" : ""), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), TextTrimming = TextTrimming.CharacterEllipsis });
                    var d = new TextBlock { Text = Langue.F("{0} % du processeur", Math.Round(p.Coeur / Math.Max(1, r.Coeurs), 1).ToString("0.#", Langue.Culture)) + (p.Disque >= 1048576 ? "  ·  " + Langue.F("disque {0}/s", Format.Taille(p.Disque)) : ""), FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), Margin = new Thickness(12, 0, 0, 0) };
                    Grid.SetColumn(d, 1); g.Children.Add(d);
                    resultats.Children.Add(g);
                }
            }
            if (Exemple == null)
            {
                var boutons = new WrapPanel();
                var encore = new BoutonNoyau("Observer à nouveau", true, 0, 40) { TaillePolice = 13.5, Capitales = true, Margin = new Thickness(0, 6, 8, 0) };
                encore.Clic += () => Observer();
                boutons.Children.Add(encore);
                var taches = new BoutonNoyau("Gestionnaire des tâches", false, 0, 40) { TaillePolice = 13.5, Margin = new Thickness(0, 6, 8, 0) };
                taches.Clic += () => { try { Programmes.Ouvrir("taskmgr.exe"); } catch { } };
                boutons.Children.Add(taches);
                resultats.Children.Add(boutons);
            }
        }
    }
}
