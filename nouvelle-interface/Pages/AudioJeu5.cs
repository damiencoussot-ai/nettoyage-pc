// Velox 5 - Audio jeu (gratuit) : égaliseur de pas pour Call of Duty / Warzone, appliqué par Equalizer APO.
// Vue simple : quatre profils, la force de l'effet, un interrupteur et l'état (jeu, sortie, profil). « Plus d'options » :
// gain de chaque bande, sortie visée, niveau, balance, largeur stéréo, coupe-explosions, inversion, Auto Warzone.
// Velox n'écrit que son fichier dans le dossier d'Equalizer APO (et la ligne qui l'inclut) : le jeu n'est jamais touché.
// Rien n'est écrit en mode simulation ; chaque changement est noté dans l'Historique et s'annule de là.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    /// <summary>Ce qui a été relevé sur le PC (lecture seule) : dossier d'Equalizer APO, sorties, jeu en cours.</summary>
    internal sealed class ReleveAudioJeu
    {
        public EtatAudioJeu Etat = new EtatAudioJeu();
        public List<SortieAudio> Sorties = new List<SortieAudio>();
        /// <summary>Sortie par défaut de Windows (identifiant et nom), et programme du jeu en cours (null : aucun).</summary>
        public string Defaut = "", NomDefaut = "", Jeu;
        public string Dossier = "";
        public bool Configurator;

        public static ReleveAudioJeu Lire()
        {
            var r = new ReleveAudioJeu();
            try
            {
                r.Dossier = AudioJeu.DossierConfig;
                r.Etat = AudioJeu.Etat();
                if (!r.Etat.Present) return r;
                r.Sorties = AudioJeu.Sorties();
                r.Defaut = AudioJeu.SortieParDefaut();
                var d = r.Sorties.FirstOrDefault(s => s.Guid == r.Defaut);
                r.NomDefaut = d != null ? d.Libelle : AudioJeu.NomSortie(r.Defaut);
                r.Jeu = AudioJeu.JeuDetecte();
                r.Configurator = AudioJeu.Configurator != null;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("relevé de l'audio jeu", ex); }
            return r;
        }
    }

    internal sealed class AudioJeu5 : StackPanel
    {
        // ================================================================ veille et écriture (une seule pour toute la fenêtre)

        /// <summary>(captures) relevé et réglages d'exemple : rien n'est lu ni écrit.</summary>
        public static ReleveAudioJeu Exemple;
        public static ReglagesAudioJeu ExempleReglages;

        private static FenetrePrincipale principale;
        private static ReglagesAudioJeu reglages;   // les vrais réglages (ceux du fichier de réglages)
        private static ReleveAudioJeu releve;       // dernier relevé
        private static DispatcherTimer veille;
        private static AudioJeu5 page;              // fenêtre de l'outil ouverte, sinon null
        private static bool enReleve, dejaReleve, enEcriture, aRefaire, refaireUtilisateur, refaireToujours, accord, autoBloque;
        private static string refaireMessage;
        private static int version;                 // avance à chaque changement : un relevé commencé avant est périmé

        private static ReglagesAudioJeu Reglages
        {
            get
            {
                if (reglages == null) { try { reglages = AudioJeu.Lire(); } catch { reglages = new ReglagesAudioJeu(); } }
                return reglages;
            }
        }

        /// <summary>L'égaliseur est actif (bulle « activé » des favoris).</summary>
        public static bool Actif
        {
            get
            {
                if (Exemple != null) return Exemple.Etat.Actif;
                return principale != null && !principale.Source.Immediat && Reglages.Actif;
            }
        }

        /// <summary>À l'ouverture de Velox : veille de l'Auto Warzone et de la sortie par défaut, tant que la fenêtre est
        /// ouverte (même réduite). Rien n'est lu tant que l'outil ne sert pas.</summary>
        public static void Demarrer(FenetrePrincipale f)
        {
            principale = f;
            if (f.Source.Immediat || !OperatingSystem.IsWindows() || veille != null) return;
            AudioJeu.Annule += () => Dispatcher.UIThread.Post(ApresAnnulation);
            veille = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            veille.Tick += (s, e) => Veiller();
            veille.Start();
            f.Closed += (s, e) => Fermeture();
        }

        private static void Veiller()
        {
            var r = Reglages;
            // un premier relevé à l'ouverture (l'état réel du fichier), puis seulement si l'outil sert
            if (dejaReleve && page == null && !r.AutoJeu && !r.Actif) return;
            Relever();
        }

        private static void Relever()
        {
            if (enReleve || Exemple != null) return;
            enReleve = true;
            int v = version;
            Task.Run(() => ReleveAudioJeu.Lire()).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enReleve = false;
                if (t.IsFaulted || t.Result == null) return;
                if (v != version) { if (page != null && !enEcriture) Relever(); return; }   // un changement est arrivé entre-temps : relevé périmé
                dejaReleve = true;
                releve = t.Result;
                try { Reagir(); } catch (Exception ex) { JournalDemarrage.Erreur("veille de l'audio jeu", ex); }
                if (page != null) page.Rafraichir();
            }));
        }

        /// <summary>Après chaque relevé : l'état réel du fichier fait foi ; Auto Warzone suit le jeu ; le fichier suit la
        /// sortie par défaut de Windows. Rien n'est écrit en simulation.</summary>
        private static void Reagir()
        {
            var f = principale; var r = Reglages; var v = releve;
            if (f == null || v == null || !v.Etat.Present || enEcriture || (page != null && page.attente)) return;
            if (f.Simulation || autoBloque) return;
            if (r.AutoJeu)
            {
                bool voulu = v.Jeu != null;
                if (v.Etat.Actif != voulu)
                {
                    r.Actif = voulu;
                    Ecrire(f, false, false, voulu ? Langue.F("Auto Warzone : {0} détecté, égaliseur actif.", v.Jeu) : Langue.T("Auto Warzone : jeu fermé, égaliseur coupé."));
                    return;
                }
            }
            r.Actif = v.Etat.Actif;   // (un autre outil a pu changer le fichier)
            // sortie par défaut changée (casque branché…) : le fichier écrit par Velox la suit ; celui d'un autre outil n'est pas touché
            if (r.Actif && v.Etat.Contenu == AudioJeu.Generer(r, r.CibleEcrite))
            {
                int etat;
                if (AudioJeu.Resoudre(r.Cible, v.Sorties, v.Defaut, out etat) != r.CibleEcrite) Ecrire(f, false, false, null);
            }
        }

        /// <summary>Écrit le fichier d'après les vrais réglages, hors du fil de l'interface. Une demande qui arrive pendant
        /// une écriture est rejouée juste après, avec les réglages du moment.</summary>
        private static void Ecrire(FenetrePrincipale f, bool utilisateur, bool toujours, string message)
        {
            if (Exemple != null || f == null) return;
            if (enEcriture)
            {
                aRefaire = true; refaireUtilisateur |= utilisateur; refaireToujours |= toujours; refaireMessage = message ?? refaireMessage;
                return;
            }
            enEcriture = true; version++;
            if (utilisateur) autoBloque = false;
            var r = Reglages;
            var copie = r.Copie();
            Task.Run(() =>
            {
                int etat;
                string guid = AudioJeu.Resoudre(copie.Cible, AudioJeu.Sorties(), AudioJeu.SortieParDefaut(), out etat);
                string erreur = AudioJeu.Appliquer(copie, guid, utilisateur, toujours);
                return Tuple.Create(erreur, copie.CibleEcrite, ReleveAudioJeu.Lire());
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enEcriture = false; version++;
                string erreur = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result.Item1;
                if (!t.IsFaulted) { releve = t.Result.Item3; dejaReleve = true; }
                if (erreur != "")
                {
                    // rien n'a été changé : l'interrupteur revient à ce que dit le fichier, et la veille ne réessaie pas en boucle
                    if (releve != null) r.Actif = releve.Etat.Actif;
                    if (!utilisateur) autoBloque = true;
                    aRefaire = false;
                    f.Statut(Langue.T(erreur) + (erreur == AudioJeu.Refus ? " " + Langue.T("Vérifie les droits du dossier (clic droit > Propriétés > Sécurité), ou réinstalle Equalizer APO.") : ""), true);
                }
                else
                {
                    r.CibleEcrite = t.Result.Item2;
                    if (message != null) f.Statut(message);
                }
                if (page != null) page.Rafraichir();
                if (aRefaire)
                {
                    bool u = refaireUtilisateur, tj = refaireToujours; string m = refaireMessage;
                    aRefaire = refaireUtilisateur = refaireToujours = false; refaireMessage = null;
                    Ecrire(f, u, tj, m);
                }
            }));
        }

        /// <summary>Un changement a été annulé depuis l'Historique : réglages et fichier sont relus.</summary>
        private static void ApresAnnulation()
        {
            reglages = null; version++;
            if (page != null) page.Recharger();
            Relever();
        }

        /// <summary>Velox se ferme : Auto Warzone ne suit le jeu que pendant que la fenêtre est ouverte, l'égaliseur n'est
        /// donc pas laissé actif sans personne pour le couper.</summary>
        private static void Fermeture()
        {
            try
            {
                if (veille != null) veille.Stop();
                var f = principale; var r = reglages;
                if (f == null || r == null || !r.AutoJeu || f.Simulation) return;
                var c = r.Copie(); c.Actif = false;
                AudioJeu.Appliquer(c, "", false);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("fermeture (audio jeu)", ex); }
        }

        // ================================================================ fenêtre de l'outil

        public static void Ouvrir(FenetrePrincipale f)
        {
            if (principale == null) principale = f;
            f.Ouvrir("Audio jeu", new AudioJeu5(f), 900);
        }

        private readonly FenetrePrincipale F;
        private readonly bool exemple, simulation;
        private ReglagesAudioJeu r;   // réglages montrés : les vrais, ou une copie en simulation (rien n'en est gardé)
        private readonly DispatcherTimer delai = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        private bool attente, attenteToujours, enMaj;
        private string attenteMessage;
        private bool? construit;   // vue construite : Equalizer APO présent ou absent

        private TextBlock tEtat, tJeu, tApo, tProfil, tForce, tNiveau, tBalance, tLargeur, tChemin, tAide;
        private TextBlock[] tBandes;
        private Curseur5[] cBandes;
        private Curseur5 cForce;
        private Interrupteur5 cEq, cAuto, cInversion;
        private StackPanel avis;
        private readonly List<Tuple<ProfilAudio, Cadre5, TextBlock>> cartes = new List<Tuple<ProfilAudio, Cadre5, TextBlock>>();
        private readonly List<Tuple<int, Border>> coupes = new List<Tuple<int, Border>>();
        private ComboBox cbSortie;
        private List<string> ciblesListe = new List<string>();
        private string signatureListe;
        private TextBox tFichier;

        private ReleveAudioJeu Releve { get { return exemple ? Exemple : releve; } }

        private AudioJeu5(FenetrePrincipale f)
        {
            F = f;
            exemple = Exemple != null;
            simulation = f.Simulation && !exemple;
            Spacing = 12;
            ChargerReglages();
            delai.Tick += (s, e) => Appliquer();
            AttachedToVisualTree += (s, e) =>
            {
                page = this;
                if (exemple || releve != null) Rafraichir();
                if (!exemple) Relever();
            };
            DetachedFromVisualTree += (s, e) =>
            {
                Appliquer();   // un réglage encore en attente n'est pas perdu
                if (page == this) page = null;
            };
            Children.Add(Premium5.Intro("Lecture d'Equalizer APO…"));
        }

        private void ChargerReglages()
        {
            r = exemple ? (ExempleReglages ?? new ReglagesAudioJeu()) : simulation ? Reglages.Copie() : Reglages;
        }

        /// <summary>Les réglages ont changé d'ailleurs (annulation depuis l'Historique) : la vue est refaite.</summary>
        private void Recharger()
        {
            delai.Stop(); attente = false;
            ChargerReglages();
            construit = null;
            Rafraichir();
        }

        internal void Rafraichir()
        {
            var v = Releve;
            if (v == null) return;
            if (construit != v.Etat.Present) Construire(v.Etat.Present);
            if (v.Etat.Present) MettreAJour(v);
        }

        // ---------------------------------------------------------------- construction

        private static TextBlock Texte(double taille = 13.5, bool discret = false)
        {
            return new TextBlock { FontFamily = Habillage.Corps, FontSize = taille, Foreground = Habillage.Pinceau(discret ? Habillage.P.Discret : Habillage.P.Texte), TextWrapping = TextWrapping.Wrap };
        }

        private static TextBlock Valeur() { return new TextBlock { FontFamily = Habillage.Console, FontSize = 13, Foreground = Habillage.Pinceau(Habillage.P.Texte), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center }; }

        private static string Db(double v) { return (v > 0 ? "+" : v < 0 ? "−" : "") + Math.Abs(v).ToString("0.0", Langue.Culture) + " dB"; }

        /// <summary>Une ligne de réglage : nom (et précision en petit), curseur, valeur.</summary>
        private static Control Rangee(string nom, string sous, Curseur5 curseur, TextBlock valeur)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*,96"), MinHeight = 34 };
            var noms = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var t = Texte(); t.Text = Langue.T(nom); noms.Children.Add(t);
            if (sous != null) noms.Children.Add(new TextBlock { Text = sous, FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(Habillage.P.Discret) });
            g.Children.Add(noms);
            curseur.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(curseur, 1); g.Children.Add(curseur);
            Grid.SetColumn(valeur, 2); g.Children.Add(valeur);
            return g;
        }

        /// <summary>Une option marche / arrêt : titre, explication, interrupteur à droite.</summary>
        private static Control Option(string titre, string explication, Interrupteur5 interrupteur)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var s = new StackPanel { Spacing = 2 };
            var t = Texte(); t.Text = Langue.T(titre); t.FontWeight = FontWeight.SemiBold; s.Children.Add(t);
            var e = Texte(12.5, true); e.Text = Langue.T(explication); s.Children.Add(e);
            g.Children.Add(s);
            interrupteur.VerticalAlignment = VerticalAlignment.Center; interrupteur.Margin = new Thickness(18, 0, 0, 0);
            Grid.SetColumn(interrupteur, 1); g.Children.Add(interrupteur);
            return g;
        }

        private void Construire(bool present)
        {
            var P = Habillage.P;
            construit = present;
            Children.Clear(); cartes.Clear(); coupes.Clear(); signatureListe = null;
            Children.Add(Premium5.Intro("Fais ressortir les pas et les recharges dans Call of Duty / Warzone. Velox écrit un petit fichier de réglages pour Equalizer APO (logiciel libre) : le jeu n'est jamais touché, et chaque changement s'annule dans l'Historique."));
            if (!present) { Children.Add(VueAbsent()); return; }

            // ---- état et interrupteur
            tEtat = new TextBlock { FontFamily = Habillage.Affichage, FontSize = 18, FontWeight = FontWeight.SemiBold, LetterSpacing = Habillage.Hud ? 1 : 0 };
            tJeu = Texte(); tApo = Texte(); tProfil = Texte();
            var lignes = new StackPanel { Spacing = 5, Children = { tEtat, tJeu, tApo, tProfil } };
            cEq = new Interrupteur5(r.Actif, 68, 34);
            cEq.Demande += BasculerEq;
            ToolTip.SetTip(cEq, Langue.T("Activer ou couper l'égaliseur"));
            var droite = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(18, 0, 0, 0) };
            droite.Children.Add(new TextBlock { Text = "EQ", FontFamily = Habillage.Console, FontSize = 11, LetterSpacing = 1.5, Foreground = Habillage.Pinceau(P.Discret), HorizontalAlignment = HorizontalAlignment.Center });
            droite.Children.Add(cEq);
            var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            haut.Children.Add(lignes);
            Grid.SetColumn(droite, 1); haut.Children.Add(droite);
            Children.Add(new Cadre5 { Child = haut, Padding = new Thickness(20, 16), Coupe = 16, CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 100), BorderThickness = new Thickness(1) });
            avis = new StackPanel { Spacing = 5 };
            Children.Add(avis);

            // ---- les quatre profils
            var grille = new UniformGrid { Columns = 4 };
            foreach (var p in AudioJeu.Profils.Take(4)) grille.Children.Add(CarteProfil(p));
            grille.SizeChanged += (o, a) => { int col = a.NewSize.Width >= 640 ? 4 : 2; if (grille.Columns != col) grille.Columns = col; };
            Children.Add(grille);

            // ---- force de l'effet
            tForce = Valeur();
            cForce = new Curseur5(0, 150, 5, r.Intensite);
            cForce.Change += v => { r.Intensite = (int)v; Changer(); };
            Children.Add(Premium5.Carte(Rangee("Force de l'effet", null, cForce, tForce)));

            // ---- plus d'options
            var options = Options();
            bool ouvert = F.OptionOuverte("AudioJeu");
            options.IsVisible = ouvert;
            var bascule = new BoutonNoyau(ouvert ? "Moins d'options  ▴" : "Plus d'options  ▾", false, 170, 32) { TaillePolice = 13, HorizontalAlignment = HorizontalAlignment.Left, Capitales = Habillage.Hud };
            bascule.Clic += () =>
            {
                bool o = !options.IsVisible;
                options.IsVisible = o;
                bascule.Changer(o ? "Moins d'options  ▴" : "Plus d'options  ▾");
                F.OptionOuverte("AudioJeu", o);
            };
            Children.Add(bascule);
            Children.Add(options);
        }

        private Control VueAbsent()
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 10 };
            s.Children.Add(Premium5.Titre("Equalizer APO n'est pas installé sur ce PC"));
            s.Children.Add(Premium5.Intro("Audio jeu a besoin d'Equalizer APO, un égaliseur libre et gratuit qui s'installe sur ta carte son. Installe-le, coche ta sortie (casque ou haut-parleurs) dans la fenêtre qu'il ouvre, redémarre le PC, puis reviens ici. Tant qu'il n'est pas là, Velox ne modifie rien."));
            s.Children.Add(new TextBlock { Text = AudioJeu.Site.Replace("https://", ""), FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Accent) });
            var boutons = new WrapPanel { Orientation = Orientation.Horizontal };
            var bSite = new BoutonNoyau("Ouvrir la page d'Equalizer APO", true, 0, 38) { TaillePolice = 14, Margin = new Thickness(0, 0, 10, 6) };
            bSite.Clic += () => { if (!exemple) Programmes.Ouvrir(AudioJeu.Site); };
            var bRevoir = new BoutonNoyau("Vérifier à nouveau", false, 0, 38) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 10, 6) };
            bRevoir.Clic += () => { if (!exemple) { F.Statut(Langue.T("Recherche d'Equalizer APO…")); Relever(); } };
            boutons.Children.Add(bSite); boutons.Children.Add(bRevoir);
            s.Children.Add(boutons);
            return Premium5.Carte(s, Habillage.Alpha(P.Orange, 130));
        }

        private Control CarteProfil(ProfilAudio p)
        {
            var P = Habillage.P;
            string nom = Langue.T(p.Carte);
            var titre = new TextBlock { Text = Habillage.Hud ? nom.ToUpper(Langue.Culture) : nom, FontFamily = Habillage.Affichage, FontSize = 16, FontWeight = FontWeight.Bold, LetterSpacing = Habillage.Hud ? 1 : 0, TextWrapping = TextWrapping.Wrap };
            var texte = Texte(12.5, true); texte.Text = Langue.T(p.Texte);
            var c = new Cadre5
            {
                Child = new StackPanel { Spacing = 5, Children = { titre, texte } }, Padding = new Thickness(16, 14), Margin = new Thickness(0, 0, 10, 10), Coupe = 14, CornerRadius = new CornerRadius(3),
                BorderThickness = new Thickness(1), MinHeight = 86, Cursor = new Cursor(StandardCursorType.Hand), Focusable = true,
            };
            ToolTip.SetTip(c, string.Join("   ", AudioJeu.Bandes.Select((b, i) => Langue.T(b.Nom) + " " + Db(p.Gains[i]))));
            c.PointerReleased += (s, e) => ChoisirProfil(p);
            c.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; ChoisirProfil(p); } };
            cartes.Add(Tuple.Create(p, c, titre));
            return c;
        }

        private Control Options()
        {
            var P = Habillage.P;
            var pile = new StackPanel { Spacing = 12 };

            // ---- gain de chaque bande
            var bandes = new StackPanel { Spacing = 2 };
            bandes.Children.Add(Premium5.Titre("Gain de chaque bande"));
            cBandes = new Curseur5[AudioJeu.Bandes.Length]; tBandes = new TextBlock[AudioJeu.Bandes.Length];
            for (int i = 0; i < AudioJeu.Bandes.Length; i++)
            {
                int k = i;
                var b = AudioJeu.Bandes[i];
                tBandes[i] = Valeur();
                cBandes[i] = new Curseur5(-12, 12, 0.5, r.Gains[i]) { DepuisZero = true };
                cBandes[i].Change += v => { r.Gains[k] = v; Changer(); };
                bandes.Children.Add(Rangee(b.Nom, b.Fc + " Hz", cBandes[i], tBandes[i]));
            }
            tAide = Texte(12.5, true);
            bandes.Children.Add(tAide);
            var autres = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            foreach (var p in AudioJeu.Profils.Skip(4))
            {
                var pp = p;
                var b = new BoutonNoyau(Langue.F("Profil « {0} »", Langue.T(p.Nom)), false, 0, 32) { TaillePolice = 12.5, Margin = new Thickness(0, 0, 8, 6) };
                ToolTip.SetTip(b, Langue.T(p.Texte));
                b.Clic += () => ChoisirProfil(pp);
                autres.Children.Add(b);
            }
            bandes.Children.Add(autres);
            pile.Children.Add(Premium5.Carte(bandes));

            // ---- sortie visée
            var sortie = new StackPanel { Spacing = 8 };
            sortie.Children.Add(Premium5.Titre("Sortie visée"));
            cbSortie = new ComboBox { MinWidth = 420, HorizontalAlignment = HorizontalAlignment.Left };
            cbSortie.SelectionChanged += (o, e) =>
            {
                if (enMaj || cbSortie.SelectedIndex < 0 || cbSortie.SelectedIndex >= ciblesListe.Count) return;
                string c = ciblesListe[cbSortie.SelectedIndex];
                if (c == r.Cible) return;
                r.Cible = c;
                Changer(Langue.T("Sortie visée changée."), false, true);
            };
            sortie.Children.Add(cbSortie);
            var explication = Texte(12.5, true);
            explication.Text = Langue.T("« Auto » suit la sortie par défaut de Windows. Pour ajouter une sortie à la liste, coche-la dans le Configurator d'Equalizer APO, puis redémarre le PC. Laisse les « Améliorations audio » de Windows activées sur ta sortie : Equalizer APO en a besoin.");
            sortie.Children.Add(explication);
            var bConfig = new BoutonNoyau("Ouvrir le Configurator", false, 0, 34) { TaillePolice = 13, HorizontalAlignment = HorizontalAlignment.Left };
            bConfig.Clic += OuvrirConfigurator;
            sortie.Children.Add(bConfig);
            pile.Children.Add(Premium5.Carte(sortie));

            // ---- niveau, balance, largeur, coupe-explosions, inversion
            var son = new StackPanel { Spacing = 4 };
            son.Children.Add(Premium5.Titre("Son"));
            tNiveau = Valeur(); tBalance = Valeur(); tLargeur = Valeur();
            var cNiveau = new Curseur5(-12, 6, 0.5, r.Niveau) { DepuisZero = true };
            cNiveau.Change += v => { r.Niveau = v; Changer(); };
            son.Children.Add(Rangee("Niveau de sortie", null, cNiveau, tNiveau));
            var cBalance = new Curseur5(-6, 6, 0.5, r.Balance) { DepuisZero = true };
            cBalance.Change += v => { r.Balance = v; Changer(); };
            son.Children.Add(Rangee("Balance", null, cBalance, tBalance));
            var cLargeur = new Curseur5(0, 100, 5, r.Largeur);
            cLargeur.Change += v => { r.Largeur = (int)v; Changer(); };
            son.Children.Add(Rangee("Largeur stéréo", null, cLargeur, tLargeur));
            var coupe = new Grid { ColumnDefinitions = new ColumnDefinitions("170,*"), Margin = new Thickness(0, 6, 0, 0) };
            var nomCoupe = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var tc = Texte(); tc.Text = Langue.T("Coupe-explosions"); nomCoupe.Children.Add(tc);
            coupe.Children.Add(nomCoupe);
            var choix = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (int hz in new[] { 0, 60, 90, 120 })
            {
                int h = hz;
                var b = new Border
                {
                    Child = new TextBlock { Text = hz == 0 ? "OFF" : hz + " Hz", FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Texte) },
                    Padding = new Thickness(14, 6), Margin = new Thickness(0, 0, 8, 4), CornerRadius = new CornerRadius(Habillage.Hud ? 0 : 4), BorderThickness = new Thickness(1.5), Cursor = new Cursor(StandardCursorType.Hand),
                };
                b.PointerReleased += (o, e) => { if (r.CoupeBas == h) return; r.CoupeBas = h; Changer(null, false, true); };
                coupes.Add(Tuple.Create(hz, b));
                choix.Children.Add(b);
            }
            Grid.SetColumn(choix, 1); coupe.Children.Add(choix);
            son.Children.Add(coupe);
            var aideCoupe = Texte(12.5, true);
            aideCoupe.Text = Langue.T("Le coupe-explosions retire les sons très graves (explosions, grondements) sous la fréquence choisie, pour qu'ils ne couvrent pas les pas.");
            son.Children.Add(aideCoupe);
            cInversion = new Interrupteur5(r.Inversion);
            cInversion.Demande += v => { r.Inversion = v; cInversion.Actif = v; Changer(null, false, true); };
            son.Children.Add(new Border { Child = Option("Inversion gauche / droite", "Échange les deux canaux : utile si ton casque est branché ou porté à l'envers.", cInversion), Margin = new Thickness(0, 8, 0, 0) });
            pile.Children.Add(Premium5.Carte(son));

            // ---- Auto Warzone et réglage automatique
            var auto = new StackPanel { Spacing = 12 };
            cAuto = new Interrupteur5(r.AutoJeu);
            cAuto.Demande += BasculerAuto;
            auto.Children.Add(Option("Auto Warzone", "L'égaliseur s'active quand Call of Duty / Warzone tourne et se coupe quand tu le fermes. Fonctionne tant que Velox est ouvert (même réduit) ; quand tu fermes Velox, l'égaliseur est coupé.", cAuto));
            var bAuto = new BoutonNoyau("Réglage automatique", false, 0, 36) { TaillePolice = 13.5, HorizontalAlignment = HorizontalAlignment.Left };
            ToolTip.SetTip(bAuto, Langue.T("Profil compétitif, force 100 %, sortie Auto, tout le reste remis à zéro."));
            bAuto.Clic += ReglageAutomatique;
            auto.Children.Add(bAuto);
            pile.Children.Add(Premium5.Carte(auto));

            // ---- le fichier écrit
            var fichier = new StackPanel { Spacing = 6 };
            fichier.Children.Add(Premium5.Titre("Fichier écrit pour Equalizer APO"));
            tChemin = new TextBlock { FontFamily = Habillage.Console, FontSize = 11.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            fichier.Children.Add(tChemin);
            // (zone de texte en lecture seule : le contenu se copie, et il n'est pas pris pour une phrase à traduire)
            tFichier = new TextBox { IsReadOnly = true, AcceptsReturn = true, FontFamily = Habillage.Console, FontSize = 12, MaxHeight = 230, TextWrapping = TextWrapping.NoWrap };
            fichier.Children.Add(tFichier);
            pile.Children.Add(Premium5.Carte(fichier));
            return pile;
        }

        // ---------------------------------------------------------------- affichage

        /// <summary>Sortie que le fichier viserait avec ces réglages ("" = toutes), et où en est Equalizer APO sur elle.</summary>
        private string Cible(ReleveAudioJeu v, out int etat) { return AudioJeu.Resoudre(r.Cible, v.Sorties, v.Defaut, out etat); }

        private void MettreAJour(ReleveAudioJeu v)
        {
            var P = Habillage.P;
            if (tEtat == null) return;
            bool hud = Habillage.Hud;
            // en simulation, l'état montré est celui des essais faits ici ; sinon celui du fichier
            string etatTexte = Langue.T(r.Actif ? "Égaliseur actif" : "Égaliseur coupé");
            tEtat.Text = "● " + (hud ? etatTexte.ToUpper(Langue.Culture) : etatTexte);
            tEtat.Foreground = Habillage.Pinceau(r.Actif ? P.Vert : P.Discret);
            cEq.Actif = r.Actif;
            if (cAuto != null) cAuto.Actif = r.AutoJeu;
            tJeu.Text = v.Jeu != null ? Langue.F("Jeu : {0} détecté.", v.Jeu) : Langue.T("Jeu : Call of Duty / Warzone n'est pas lancé.");
            tJeu.Foreground = Habillage.Pinceau(v.Jeu != null ? P.Texte : P.Discret);

            int etat; string guid = Cible(v, out etat);
            var visee = v.Sorties.FirstOrDefault(s => s.Guid == guid);
            string nomDefaut = v.NomDefaut != "" ? v.NomDefaut : Langue.T("inconnue");
            string apo; bool alerte = etat != 0;
            if (etat == 3) apo = Langue.T("Equalizer APO n'est installé sur aucune sortie : ouvre le Configurator, coche ta sortie, puis redémarre le PC.");
            else if (etat == 2) apo = Langue.T("La sortie choisie n'a plus Equalizer APO : choisis-en une autre, ou coche-la de nouveau dans le Configurator.");
            else if (etat == 1) apo = Langue.F("Ta sortie par défaut ({0}) n'a pas Equalizer APO : l'égaliseur vise toutes les sorties où il est installé. Si tu n'entends aucune différence, ouvre le Configurator, coche ta sortie, puis redémarre le PC.", nomDefaut);
            else if (r.Cible == "toutes") apo = Langue.F("Equalizer APO : toutes les sorties où il est installé ({0}).", v.Sorties.Count);
            else if (r.Cible == "auto") apo = Langue.F("Equalizer APO : installé sur ta sortie par défaut ({0}).", visee != null ? visee.Libelle : nomDefaut);
            else apo = Langue.F("Equalizer APO : installé sur la sortie choisie ({0}).", visee != null ? visee.Libelle : guid);
            tApo.Text = apo;
            tApo.Foreground = Habillage.Pinceau(alerte ? P.Orange : P.Texte);
            tProfil.Text = Langue.F("Profil en cours : {0} · force {1} %", Langue.T(AudioJeu.NomProfil(r)), r.Intensite);

            // ---- avis
            avis.Children.Clear();
            Action<string, bool> avertir = (t, orange) =>
            {
                var b = Texte(13); b.Text = (orange ? "⚠  " : "ℹ  ") + t; b.Foreground = Habillage.Pinceau(orange ? P.Orange : P.Discret);
                avis.Children.Add(b);
            };
            if (simulation) avertir(Langue.T("Mode simulation : tes essais ici ne sont pas écrits, le fichier d'Equalizer APO ne change pas."), true);
            if (v.Etat.Autres.Count > 0) avertir(Langue.F("D'autres traitements sont actifs dans Equalizer APO ({0}) : leurs effets s'ajoutent à cet égaliseur.", string.Join(", ", v.Etat.Autres)), false);
            var vrais = exemple ? r : Reglages;
            if (v.Etat.Actif && !v.Etat.Include) avertir(Langue.T("Le fichier config.txt d'Equalizer APO n'inclut pas cet égaliseur : il n'a aucun effet. Coupe puis réactive l'interrupteur pour remettre la ligne."), true);
            else if (v.Etat.Actif && !enEcriture && !attente && v.Etat.Contenu != AudioJeu.Generer(vrais, vrais.CibleEcrite))
                avertir(Langue.T("Le fichier en place ne vient pas de ces réglages (autre outil, ou modifié à la main) : ton prochain réglage ici le remplacera."), false);
            if (r.Niveau > 0) avertir(Langue.T("Niveau au-dessus de 0 dB : le son peut saturer (grésillements). Baisse-le si tu en entends."), true);

            // ---- profils et réglages
            var courant = AudioJeu.ProfilDe(r.Gains);
            foreach (var c in cartes)
            {
                bool choisi = courant == c.Item1;
                c.Item2.Background = Habillage.Pinceau(choisi ? Habillage.Melanger(P.Carte, P.Accent, P.Sombre ? 0.12 : 0.08) : P.Carte);
                c.Item2.BorderBrush = Habillage.Pinceau(choisi ? P.Accent : P.Bordure);
                c.Item2.TraitHaut = choisi && hud ? P.Accent : (Color?)null;
                c.Item3.Foreground = Habillage.Pinceau(choisi ? P.Accent : P.Texte);
            }
            cForce.Valeur = r.Intensite;
            tForce.Text = r.Intensite + " %";
            if (cBandes != null)
            {
                for (int i = 0; i < cBandes.Length; i++)
                {
                    cBandes[i].Valeur = r.Gains[i];
                    tBandes[i].Text = Db(r.Gains[i]);
                }
                tAide.Text = r.Intensite == 100 ? Langue.T("Chaque gain va de −12 à +12 dB. La force de l'effet les multiplie tous.")
                    : Langue.F("Avec la force à {0} %, les gains réellement appliqués sont : {1}.", r.Intensite, string.Join("  ", AudioJeu.Bandes.Select((b, i) => Db(AudioJeu.GainEffectif(r, i)))));
                tNiveau.Text = Db(r.Niveau);
                tNiveau.Foreground = Habillage.Pinceau(r.Niveau > 0 ? P.Orange : P.Texte);
                tBalance.Text = r.Balance == 0 ? Langue.T("centre") : Langue.F(r.Balance > 0 ? "droite {0}" : "gauche {0}", Db(Math.Abs(r.Balance)));
                tLargeur.Text = r.Largeur == 0 ? Langue.T("normale") : "+" + r.Largeur + " %";
                foreach (var c in coupes)
                {
                    bool choisi = c.Item1 == r.CoupeBas;
                    c.Item2.BorderBrush = Habillage.Pinceau(choisi ? P.Accent : P.Bordure);
                    c.Item2.Background = Habillage.Pinceau(choisi ? Habillage.Melanger(P.Carte, P.Accent, 0.12) : P.Carte);
                }
                cInversion.Actif = r.Inversion;
                ListerSorties(v);
                tChemin.Text = Path.Combine(v.Dossier, AudioJeu.NomFichier);
                string contenu = AudioJeu.Generer(r, guid).TrimEnd();
                if (tFichier.Text != contenu) tFichier.Text = contenu;
            }
        }

        /// <summary>Liste des sorties (relue régulièrement : elle change dans le temps). Refaite seulement si elle a changé.</summary>
        private void ListerSorties(ReleveAudioJeu v)
        {
            string signature = string.Join("|", v.Sorties.Select(s => s.Guid + "=" + s.Libelle)) + "|" + v.Defaut + "|" + r.Cible;
            if (signature == signatureListe || cbSortie.IsDropDownOpen) return;
            signatureListe = signature;
            enMaj = true;
            try
            {
                var cibles = new List<string> { "auto", "toutes" };
                cbSortie.Items.Clear();
                cbSortie.Items.Add(Langue.T("Auto (la sortie par défaut de Windows)"));
                cbSortie.Items.Add(Langue.T("Toutes les sorties où Equalizer APO est installé"));
                foreach (var s in v.Sorties) { cibles.Add(s.Guid); cbSortie.Items.Add(s.Libelle + (s.Guid == v.Defaut ? "  ·  " + Langue.T("par défaut") : "")); }
                // sortie choisie qui n'a plus Equalizer APO : elle reste affichée, pour que le choix se voie
                if (!cibles.Contains(r.Cible)) { cibles.Add(r.Cible); cbSortie.Items.Add(Langue.T("Sortie choisie (sans Equalizer APO)")); }
                ciblesListe = cibles;
                cbSortie.SelectedIndex = cibles.IndexOf(r.Cible);
            }
            finally { enMaj = false; }
        }

        // ---------------------------------------------------------------- changements

        /// <summary>Un réglage vient de changer : l'affichage suit tout de suite, l'écriture attend un court instant sans
        /// nouveau changement (un curseur qu'on glisse ne réécrit pas le fichier à chaque pas).</summary>
        private void Changer(string message = null, bool toujours = false, bool toutDeSuite = false)
        {
            var v = Releve;
            if (v != null) MettreAJour(v);
            if (exemple) return;
            if (simulation) { F.Statut(Langue.T("Simulation : le fichier d'Equalizer APO serait réécrit, rien n'est modifié."), true); return; }
            version++;
            attente = true; attenteToujours |= toujours;
            if (message != null) attenteMessage = message;
            delai.Stop();
            if (toutDeSuite) Appliquer(); else delai.Start();
        }

        private void Appliquer()
        {
            delai.Stop();
            if (!attente) return;
            attente = false;
            string m = attenteMessage ?? Langue.T(r.Actif ? "Réglage appliqué." : "Réglage gardé : il s'entendra dès que l'égaliseur sera actif.");
            bool tj = attenteToujours;
            attenteMessage = null; attenteToujours = false;
            Ecrire(F, true, tj, m);
        }

        /// <summary>Avant la première écriture dans config.txt (ligne Include absente) : la question est posée, une fois.</summary>
        private void AvecAccord(Action suite)
        {
            var v = Releve;
            if (simulation || accord || (v != null && v.Etat.Include)) { suite(); return; }
            F.Demander("Activer l'égaliseur avec Equalizer APO ?",
                Langue.F("Velox va écrire le fichier {0} dans le dossier d'Equalizer APO et ajouter la ligne « {1} » à la fin de son config.txt. Rien d'autre n'y est modifié, le jeu n'est pas touché, et tout s'annule dans l'Historique.", AudioJeu.NomFichier, AudioJeu.LigneInclude),
                "Activer", "Annuler", oui => { if (!oui) { Rafraichir(); return; } accord = true; suite(); });
        }

        private void BasculerEq(bool voulu)
        {
            if (exemple) return;
            Action faire = () =>
            {
                bool auto = r.AutoJeu;
                r.AutoJeu = false; r.Actif = voulu;
                Changer(Langue.T(voulu ? "Égaliseur actif." : "Égaliseur coupé.") + (auto ? " " + Langue.T("Auto Warzone est désactivé : c'est toi qui pilotes l'interrupteur.") : ""), false, true);
            };
            if (voulu) AvecAccord(faire); else faire();
        }

        private void BasculerAuto(bool voulu)
        {
            if (exemple) return;
            Action faire = () =>
            {
                var v = Releve;
                r.AutoJeu = voulu;
                if (voulu) r.Actif = v != null && v.Jeu != null;
                string m = !voulu ? "Auto Warzone désactivé : l'égaliseur reste comme il est."
                    : r.Actif ? "Auto Warzone activé : le jeu tourne, l'égaliseur est actif." : "Auto Warzone activé : l'égaliseur se mettra en marche au lancement du jeu.";
                Changer(Langue.T(m), voulu, true);
            };
            if (voulu) AvecAccord(faire); else faire();
        }

        private void ChoisirProfil(ProfilAudio p)
        {
            if (exemple) return;
            r.Gains = (double[])p.Gains.Clone();
            string nom = Langue.T(p.Nom);
            Changer(r.Actif ? Langue.F("Profil « {0} » appliqué.", nom)
                : r.AutoJeu ? Langue.F("Profil « {0} » choisi : il s'appliquera au lancement du jeu.", nom)
                : Langue.F("Profil « {0} » choisi. L'égaliseur est coupé : active-le pour l'entendre.", nom), false, true);
        }

        private void ReglageAutomatique()
        {
            if (exemple) return;
            r.Gains = (double[])AudioJeu.Profils[0].Gains.Clone();
            r.Intensite = 100; r.Cible = "auto"; r.Niveau = 0; r.Balance = 0; r.Largeur = 0; r.CoupeBas = 0; r.Inversion = false;
            // tous les curseurs reprennent leur place : la vue est refaite
            construit = null;
            Rafraichir();
            Changer(Langue.T("Réglage automatique : profil compétitif, force 100 %, sortie Auto, le reste remis à zéro.") + (r.Actif || r.AutoJeu ? "" : " " + Langue.T("L'égaliseur est coupé : active-le pour l'entendre.")), false, true);
        }

        private void OuvrirConfigurator()
        {
            if (exemple) return;
            string c = null;
            try { c = AudioJeu.Configurator; } catch { }
            if (c == null) { F.Statut(Langue.T("Le Configurator d'Equalizer APO est introuvable sur ce PC."), true); return; }
            // (c'est le Configurator lui-même qui demande l'autorisation de Windows)
            Programmes.Ouvrir(c);
            F.Statut(Langue.T("Configurator ouvert : coche ta sortie, valide, puis redémarre le PC."));
        }
    }
}
