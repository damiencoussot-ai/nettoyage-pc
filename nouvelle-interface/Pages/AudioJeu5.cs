// Velox 5 - rubrique Audio (« Studio son », Premium) : égaliseur de pas pour Call of Duty / Warzone. Habillage à part
// (console de mixage) : courbe de l'égaliseur et spectre en direct, quatre profils, molettes, faders, sorties audio de
// Windows (VU-mètre, sortie par défaut), Voicemeeter (niveaux, égaliseur natif de Banana / Potato), Auto Warzone et
// raccourcis clavier globaux.
// L'égaliseur passe par Equalizer APO (Velox écrit son fichier MW_optimizer.txt et la ligne qui l'inclut) ou par
// Voicemeeter : le jeu n'est jamais touché. Rien n'est écrit en mode simulation ; chaque changement est noté dans
// l'Historique et s'annule de là. Sans Premium : la page présente l'outil, et on peut toujours couper l'égaliseur.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
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
    /// <summary>Ce qui a été relevé sur le PC (lecture seule) : Equalizer APO, sorties, jeu en cours.</summary>
    internal sealed class ReleveAudioJeu
    {
        public EtatAudioJeu Etat = new EtatAudioJeu();
        /// <summary>Sorties où Equalizer APO est installé, et toutes les sorties actives de Windows.</summary>
        public List<SortieAudio> Sorties = new List<SortieAudio>();
        public List<SortieWindows> Windows = new List<SortieWindows>();
        /// <summary>Sortie par défaut de Windows (identifiant court et nom), et programme du jeu en cours (null : aucun).</summary>
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
                r.Jeu = AudioJeu.JeuDetecte();
                r.Windows = AudioWindows.Lister();
                r.Defaut = AudioJeu.SortieParDefaut();
                if (r.Etat.Present)
                {
                    r.Sorties = AudioJeu.Sorties();
                    r.Configurator = AudioJeu.Configurator != null;
                }
                var d = r.Sorties.FirstOrDefault(s => s.Guid == r.Defaut);
                r.NomDefaut = d != null ? d.Libelle : AudioJeu.NomSortie(r.Defaut);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("relevé de l'audio jeu", ex); }
            return r;
        }
    }

    internal sealed class AudioJeu5 : Panel
    {
        // ================================================================ veille et écriture (une seule pour toute la fenêtre)

        /// <summary>(captures) relevé, réglages, spectre et Voicemeeter d'exemple : rien n'est lu ni écrit.</summary>
        public static ReleveAudioJeu Exemple;
        public static ReglagesAudioJeu ExempleReglages;
        public static EtatVoicemeeter ExempleVoicemeeter;
        /// <summary>(captures) montrer la page comme avec Premium.</summary>
        public static bool ApercuPremium;

        private static FenetrePrincipale principale;
        private static ReglagesAudioJeu reglages;   // les vrais réglages (ceux du fichier de réglages)
        private static ReleveAudioJeu releve;       // dernier relevé
        private static DispatcherTimer veille;
        private static AudioJeu5 page;              // page affichée, sinon null
        private static bool enReleve, dejaReleve, enEcriture, aRefaire, refaireUtilisateur, refaireToujours, accord, autoBloque;
        private static string refaireMessage;
        private static int version;                 // avance à chaque changement : un relevé commencé avant est périmé

        private static bool Premium { get { return Licence.Premium || ApercuPremium; } }

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

        /// <summary>À l'ouverture de Velox : veille de l'Auto Warzone et de la sortie par défaut, raccourcis clavier, tant
        /// que la fenêtre est ouverte (même réduite). Rien n'est lu tant que l'outil ne sert pas.</summary>
        public static void Demarrer(FenetrePrincipale f)
        {
            principale = f;
            if (f.Source.Immediat || !OperatingSystem.IsWindows() || veille != null) return;
            AudioJeu.Annule += () => Dispatcher.UIThread.Post(ApresAnnulation);
            veille = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            veille.Tick += (s, e) => Veiller();
            veille.Start();
            f.Closed += (s, e) => Fermeture();
            // Premium perdu ou retrouvé : raccourcis et page suivent
            Licence.Change += () => Dispatcher.UIThread.Post(() =>
            {
                RaccourcisAudio.Activer(Premium && Reglages.Raccourcis);
                if (principale != null && principale.PageActive == "Audio") principale.Afficher("Audio", false);
            });
            RaccourcisAudio.Activer(Premium && Reglages.Raccourcis);
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
                if (page != null) page.MettreAJour();
            }));
        }

        /// <summary>Après chaque relevé : l'état réel du fichier fait foi ; Auto Warzone suit le jeu ; le fichier suit la
        /// sortie par défaut de Windows. Rien n'est écrit en simulation, ni sans Premium.</summary>
        private static void Reagir()
        {
            var f = principale; var r = Reglages; var v = releve;
            if (f == null || v == null || enEcriture || (page != null && page.attente)) return;
            if (f.Simulation || autoBloque || !Licence.Premium) return;
            bool vm = r.Moteur == "vm";
            if (!vm && !v.Etat.Present) return;
            if (r.AutoJeu)
            {
                bool voulu = v.Jeu != null, actuel = vm ? r.Actif : v.Etat.Actif;
                if (actuel != voulu)
                {
                    r.Actif = voulu;
                    Ecrire(f, false, false, voulu ? Langue.F("Auto Warzone : {0} détecté, égaliseur actif.", v.Jeu) : Langue.T("Auto Warzone : jeu fermé, égaliseur coupé."));
                    return;
                }
            }
            if (vm) return;
            r.Actif = v.Etat.Actif;   // (un autre outil a pu changer le fichier)
            // sortie par défaut changée (casque branché…) : le fichier écrit par Velox la suit ; celui d'un autre outil n'est pas touché
            if (r.Actif && v.Etat.Contenu == AudioJeu.Generer(r, r.CibleEcrite))
            {
                int etat;
                if (AudioJeu.Resoudre(r.Cible, v.Sorties, v.Defaut, out etat) != r.CibleEcrite) Ecrire(f, false, false, null);
            }
        }

        /// <summary>Applique les vrais réglages (Equalizer APO ou Voicemeeter), hors du fil de l'interface. Une demande qui
        /// arrive pendant une écriture est rejouée juste après, avec les réglages du moment.</summary>
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
                string erreur;
                if (copie.Moteur == "vm")
                {
                    // l'égaliseur passe par Voicemeeter : celui d'Equalizer APO est coupé, pour ne pas doubler l'effet
                    if (AudioJeu.Present) { var sansApo = copie.Copie(); sansApo.Actif = false; AudioJeu.Appliquer(sansApo, "", utilisateur); }
                    if (copie.VmEcrit >= 0 && copie.VmEcrit != copie.BusVm) Voicemeeter.Couper(copie.VmEcrit, utilisateur);
                    erreur = Voicemeeter.Appliquer(copie, copie.BusVm, utilisateur);
                    if (erreur == "") copie.VmEcrit = copie.Actif ? copie.BusVm : -1;
                }
                else
                {
                    int etat;
                    string guid = AudioJeu.Resoudre(copie.Cible, AudioJeu.Sorties(), AudioJeu.SortieParDefaut(), out etat);
                    erreur = AudioJeu.Appliquer(copie, guid, utilisateur, toujours);
                    if (erreur == "" && copie.VmEcrit >= 0 && Voicemeeter.Couper(copie.VmEcrit, utilisateur) == "") copie.VmEcrit = -1;
                }
                if (erreur == "") AudioJeu.Enregistrer(copie);
                return Tuple.Create(erreur, copie, ReleveAudioJeu.Lire());
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enEcriture = false; version++;
                string erreur = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result.Item1;
                if (!t.IsFaulted) { releve = t.Result.Item3; dejaReleve = true; }
                if (erreur != "")
                {
                    // rien n'a été changé : l'interrupteur revient à ce que dit le fichier, et la veille ne réessaie pas en boucle
                    if (releve != null && r.Moteur != "vm") r.Actif = releve.Etat.Actif;
                    if (!utilisateur) autoBloque = true;
                    aRefaire = false;
                    f.Statut(Langue.T(erreur) + (erreur == AudioJeu.Refus ? " " + Langue.T("Vérifie les droits du dossier (clic droit > Propriétés > Sécurité), ou réinstalle Equalizer APO.") : ""), true);
                }
                else
                {
                    r.CibleEcrite = t.Result.Item2.CibleEcrite; r.VmEcrit = t.Result.Item2.VmEcrit;
                    if (message != null) f.Statut(message);
                }
                if (page != null) page.MettreAJour();
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
            if (principale != null && principale.PageActive == "Audio" && page != null) principale.Afficher("Audio", false);
            Relever();
        }

        /// <summary>Velox se ferme : Auto Warzone ne suit le jeu que pendant que la fenêtre est ouverte, l'égaliseur n'est
        /// donc pas laissé actif sans personne pour le couper.</summary>
        private static void Fermeture()
        {
            try
            {
                if (veille != null) veille.Stop();
                RaccourcisAudio.Activer(false);
                var f = principale; var r = reglages;
                if (f == null || r == null || !r.AutoJeu || f.Simulation) return;
                var c = r.Copie(); c.Actif = false;
                if (c.Moteur == "vm") Voicemeeter.Couper(c.BusVm, false); else AudioJeu.Appliquer(c, "", false);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("fermeture (audio jeu)", ex); }
        }

        /// <summary>Raccourci clavier global (Ctrl+Alt+F9 : égaliseur ; Ctrl+Alt+F10 : profil suivant).</summary>
        internal static void Raccourci(int quoi)
        {
            var f = principale;
            if (f == null || !Licence.Premium) return;
            if (f.Simulation) { f.Statut(Langue.T("Simulation : le fichier d'Equalizer APO serait réécrit, rien n'est modifié."), true); return; }
            var r = Reglages;
            string message;
            if (quoi == 0)
            {
                r.AutoJeu = false; r.Actif = !r.Actif;
                message = Langue.T(r.Actif ? "Égaliseur actif." : "Égaliseur coupé.");
                Bip(r.Actif ? new[] { 880 } : new[] { 440 });
            }
            else
            {
                var p = AudioJeu.ProfilSuivant(r.Gains);
                r.Gains = (double[])p.Gains.Clone();
                message = Langue.F("Profil « {0} » appliqué.", Langue.T(p.Nom));
                Bip(Enumerable.Repeat(660, Array.IndexOf(AudioJeu.Profils, p) + 1).ToArray());
            }
            if (page != null) page.Recharger();
            Ecrire(f, true, false, message);
        }

        /// <summary>Petits bips pour savoir, en plein jeu, ce que le raccourci a fait (un bip par numéro de profil).</summary>
        private static void Bip(int[] notes)
        {
            if (!OperatingSystem.IsWindows()) return;
            Task.Run(() => { try { foreach (var n in notes) { Console.Beep(n, 70); Thread.Sleep(40); } } catch { } });
        }

        public static void Ouvrir(FenetrePrincipale f)
        {
            if (principale == null) principale = f;
            f.ViderCalques();
            f.Afficher("Audio", true);
        }

        /// <summary>La rubrique Audio.</summary>
        public static Control Creer(FenetrePrincipale f)
        {
            if (principale == null) principale = f;
            return new AudioJeu5(f);
        }

        // ================================================================ la page

        private readonly FenetrePrincipale F;
        private readonly bool exemple, simulation, deverrouille;
        private ReglagesAudioJeu r;   // réglages montrés : les vrais, ou une copie en simulation (rien n'en est gardé)
        private readonly DispatcherTimer delai = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        private bool attente, attenteToujours, enDirect;
        private string attenteMessage;
        private Action<long> ambiance;
        private long dernierImage;

        private Marche5 marche;
        private TextBlock tMarche, tProfil, tFichier, tChemin, tAideBandes, tMoteur, tEcoute;
        private CourbeEq5 courbe;
        private WrapPanel temoins;
        private StackPanel avis, zoneSorties, zoneVm, zoneAuto, zoneFichier;
        private VuMetre5 vu;
        private readonly List<Tuple<ProfilAudio, Border, CourbeEq5>> cartes = new List<Tuple<ProfilAudio, Border, CourbeEq5>>();
        private readonly List<Tuple<Molette5, TextBlock, Func<string>>> molettes = new List<Tuple<Molette5, TextBlock, Func<string>>>();
        private readonly List<Tuple<int, Border>> coupes = new List<Tuple<int, Border>>();
        private Border bInversion;
        private Fader5[] faders;
        private TextBlock[] tFaders;
        private readonly List<Tuple<BusVoicemeeter, VuMetre5>> vuBus = new List<Tuple<BusVoicemeeter, VuMetre5>>();
        private string signatureSorties, signatureVm, signatureAuto;
        private bool? deuxColonnes;
        private WrapPanel boutonsVerrou;
        private Control bCouper;
        private Grid corps;
        private Control gauche, droite;

        private ReleveAudioJeu Releve { get { return exemple ? Exemple : releve; } }
        private EtatVoicemeeter Vm { get { return exemple ? (ExempleVoicemeeter ?? new EtatVoicemeeter()) : (Voicemeeter.Etat ?? new EtatVoicemeeter()); } }

        private AudioJeu5(FenetrePrincipale f)
        {
            F = f;
            exemple = Exemple != null;
            simulation = f.Simulation && !exemple;
            deverrouille = Premium;
            r = exemple ? (ExempleReglages ?? new ReglagesAudioJeu()) : simulation ? Reglages.Copie() : Reglages;
            delai.Tick += (s, e) => Appliquer();
            Margin = new Thickness(24, 6, 24, 24);
            Children.Add(Studio.Console(deverrouille ? (Control)Construire() : Verrou()));
            AttachedToVisualTree += (s, e) =>
            {
                page = this;
                if (exemple || releve != null) MettreAJour();
                if (!exemple) Relever();
                Direct(true);
            };
            DetachedFromVisualTree += (s, e) =>
            {
                Appliquer();   // un réglage encore en attente n'est pas perdu
                Direct(false);
                if (page == this) page = null;
            };
        }

        /// <summary>Spectre, VU-mètres et niveaux de Voicemeeter en direct, tant que la page est affichée (jamais pendant
        /// les essais ni sans Premium).</summary>
        private void Direct(bool oui)
        {
            if (exemple || !deverrouille || F.Source.Immediat || !OperatingSystem.IsWindows()) return;
            if (oui == enDirect) return;
            enDirect = oui;
            if (oui)
            {
                CapteurAudio.Demarrer(); Voicemeeter.Suivre(true);
                ambiance = t =>
                {
                    if (t - dernierImage < 33) return;   // 30 images par seconde
                    dernierImage = t;
                    if (courbe != null) { courbe.Spectre = CapteurAudio.Spectre; courbe.InvalidateVisual(); }
                    if (vu != null) vu.Definir(CapteurAudio.CreteG, CapteurAudio.CreteD);
                    if (tEcoute != null) { string err = CapteurAudio.Erreur == "" ? "" : Langue.T(CapteurAudio.Erreur); if (tEcoute.Text != err) tEcoute.Text = err; }
                    var e = Voicemeeter.Etat;
                    foreach (var x in vuBus) { var b = e.Bus.FirstOrDefault(y => y.Index == x.Item1.Index); if (b != null) x.Item2.Definir(b.G, b.D); }
                    if (t % 1000 < 40) MajVoicemeeter();
                };
                Horloge.Ambiance(ambiance);
            }
            else
            {
                CapteurAudio.Arreter(); Voicemeeter.Suivre(false);
                if (ambiance != null) Horloge.RetirerAmbiance(ambiance);
                ambiance = null;
            }
        }

        private void Recharger()
        {
            delai.Stop(); attente = false;
            r = simulation ? Reglages.Copie() : Reglages;
            foreach (var c in molettes) c.Item1.Valeur = ValeurMolette(c.Item1);
            MettreAJour();
        }

        // ---------------------------------------------------------------- construction

        private static readonly double[] SpectreExemple = Enumerable.Range(0, AnalyseurSpectre.NbBandes).Select(i =>
        {
            double f = AnalyseurSpectre.Centres[i];
            return -22 - 9 * Math.Log10(f / 80) * Math.Log10(f / 80) - (f > 6000 ? (f - 6000) / 900 : 0) + (Math.Abs(Math.Log(f / 2600)) < 0.25 ? 8 : 0) + 3 * Math.Sin(i * 1.7);
        }).ToArray();

        private Control EnTete(bool avecMarche)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 0, 0, 14) };
            var s = new StackPanel { Spacing = 4 };
            s.Children.Add(new TextBlock { Text = Langue.T("STUDIO SON") + "  ·  " + Langue.T("AUDIO JEU"), FontFamily = Studio.Chiffres, FontSize = 11, LetterSpacing = 3, Foreground = Studio.P(Studio.Ambre) });
            s.Children.Add(new TextBlock { Text = Langue.T("Entends chaque pas."), FontFamily = Studio.Titre, FontSize = 32, FontWeight = FontWeight.Bold, Foreground = Studio.Degrade(0, Studio.Texte, Color.FromRgb(255, 200, 230), Studio.Magenta), TextWrapping = TextWrapping.Wrap });
            s.Children.Add(Studio.T(Langue.T("Égaliseur de pas pour Call of Duty / Warzone, appliqué par Equalizer APO ou Voicemeeter. Le jeu n'est jamais touché, et chaque changement s'annule dans l'Historique."), 13, Studio.Discret));
            temoins = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            s.Children.Add(temoins);
            g.Children.Add(s);
            if (avecMarche)
            {
                marche = new Marche5(r.Actif);
                ToolTip.SetTip(marche, Langue.T("Activer ou couper l'égaliseur"));
                marche.Demande += BasculerEq;
                tMarche = new TextBlock { FontFamily = Studio.Chiffres, FontSize = 11, LetterSpacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
                var droiteEq = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(18, 4, 0, 0), Children = { marche, tMarche } };
                Grid.SetColumn(droiteEq, 1); g.Children.Add(droiteEq);
            }
            return g;
        }

        private Control Construire()
        {
            var pile = new StackPanel();
            pile.Children.Add(EnTete(true));
            avis = new StackPanel { Spacing = 8, Margin = new Thickness(0, 0, 0, 8) };
            pile.Children.Add(avis);

            // ---------------- colonne principale
            var g = new StackPanel();
            courbe = new CourbeEq5 { Reglages = r, Allume = r.Actif };
            courbe.GainChange += (k, db) =>
            {
                if (r.Intensite <= 0) { F.Statut(Langue.T("La force de l'effet est à 0 % : monte-la pour régler les bandes."), true); return; }
                r.Gains[k] = Math.Max(-12, Math.Min(12, Math.Round(db / (r.Intensite / 100.0) * 2, MidpointRounding.AwayFromZero) / 2));
                Changer();
            };
            if (exemple) courbe.Spectre = SpectreExemple.Select(x => (float)x).ToArray();
            var legende = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            legende.Children.Add(Legende(Studio.Cyan, "Spectre en direct"));
            legende.Children.Add(Legende(Studio.Magenta, "Égaliseur"));
            var bloc = new StackPanel { Spacing = 8, Children = { courbe, Studio.T(Langue.T("Glisse un point pour régler sa bande (ou flèches du clavier). En fond, ce que joue ta sortie en ce moment."), 12, Studio.Faible) } };
            g.Children.Add(Studio.Module("Réponse en fréquence", bloc, legende));

            // profils
            var grille = new UniformGrid { Columns = 4 };
            foreach (var p in AudioJeu.Profils.Take(4)) grille.Children.Add(CarteProfil(p));
            grille.SizeChanged += (o, a) => { int col = a.NewSize.Width >= 600 ? 4 : 2; if (grille.Columns != col) grille.Columns = col; };
            var autres = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var p in AudioJeu.Profils.Skip(4)) { var pp = p; autres.Children.Add(Studio.Bouton(Langue.T(p.Nom), false, () => ChoisirProfil(pp), Langue.T(p.Texte))); }
            autres.Children.Add(Studio.Bouton(Langue.T("Réglage automatique"), false, ReglageAutomatique, Langue.T("Profil compétitif, force 100 %, sortie Auto, tout le reste remis à zéro.")));
            tProfil = Studio.T("", 12.5, Studio.Discret);
            g.Children.Add(Studio.Module("Profils", new StackPanel { Children = { grille, autres, tProfil } }));

            // console : molettes, coupe-explosions, inversion
            var rang = new WrapPanel();
            rang.Children.Add(Molette("Force", 0, 150, 5, 100, false, () => r.Intensite + " %", v => r.Intensite = (int)v));
            rang.Children.Add(Molette("Niveau", -12, 6, 0.5, 0, true, () => Studio.Db(r.Niveau), v => r.Niveau = v));
            rang.Children.Add(Molette("Balance", -6, 6, 0.5, 0, true, () => r.Balance == 0 ? Langue.T("centre") : Langue.F(r.Balance > 0 ? "droite {0}" : "gauche {0}", Studio.Db(Math.Abs(r.Balance))), v => r.Balance = v));
            rang.Children.Add(Molette("Largeur", 0, 100, 5, 0, false, () => r.Largeur == 0 ? Langue.T("normale") : "+" + r.Largeur + " %", v => r.Largeur = (int)v));
            var options = new StackPanel { Spacing = 10, Margin = new Thickness(0, 6, 0, 0) };
            var coupe = new WrapPanel();
            coupe.Children.Add(new TextBlock { Text = Langue.T("Coupe-explosions"), FontFamily = Studio.Titre, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = Studio.P(Studio.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 8) });
            foreach (int hz in new[] { 0, 60, 90, 120 })
            {
                int h = hz;
                var b = Puce(hz == 0 ? "OFF" : hz + " Hz", () => { if (r.CoupeBas == h) return; r.CoupeBas = h; Changer(null, false, true); });
                coupes.Add(Tuple.Create(hz, b)); coupe.Children.Add(b);
            }
            options.Children.Add(coupe);
            bInversion = Puce(Langue.T("Inversion gauche / droite"), () => { r.Inversion = !r.Inversion; Changer(null, false, true); });
            ToolTip.SetTip(bInversion, Langue.T("Échange les deux canaux : utile si ton casque est branché ou porté à l'envers."));
            options.Children.Add(new WrapPanel { Children = { bInversion } });
            options.Children.Add(Studio.T(Langue.T("Le coupe-explosions retire les sons très graves (explosions, grondements) sous la fréquence choisie, pour qu'ils ne couvrent pas les pas. Au-dessus de 0 dB, le niveau peut faire saturer le son."), 12, Studio.Faible));
            tMoteur = Studio.T("", 12, Studio.Ambre);
            options.Children.Add(tMoteur);
            g.Children.Add(Studio.Module("Console", new StackPanel { Children = { rang, options } }));

            // faders des bandes
            var bandes = new UniformGrid { Columns = AudioJeu.Bandes.Length };
            faders = new Fader5[AudioJeu.Bandes.Length]; tFaders = new TextBlock[AudioJeu.Bandes.Length];
            for (int i = 0; i < AudioJeu.Bandes.Length; i++)
            {
                int k = i; var b = AudioJeu.Bandes[i];
                faders[i] = new Fader5(-12, 12, 0.5, r.Gains[i]) { HorizontalAlignment = HorizontalAlignment.Center };
                faders[i].Change += v => { r.Gains[k] = v; Changer(); };
                tFaders[i] = new TextBlock { FontFamily = Studio.Chiffres, FontSize = 12, Foreground = Studio.P(Studio.Texte), HorizontalAlignment = HorizontalAlignment.Center };
                bandes.Children.Add(new StackPanel
                {
                    Spacing = 6, Children =
                    {
                        tFaders[i], faders[i],
                        new TextBlock { Text = Langue.T(b.Nom), FontFamily = Studio.Titre, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Studio.P(Studio.Texte), HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap },
                        new TextBlock { Text = b.Fc >= 1000 ? (b.Fc / 1000.0).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + " kHz" : b.Fc + " Hz", FontFamily = Studio.Chiffres, FontSize = 10.5, Foreground = Studio.P(Studio.Faible), HorizontalAlignment = HorizontalAlignment.Center },
                    },
                });
            }
            tAideBandes = Studio.T("", 12, Studio.Faible);
            g.Children.Add(Studio.Module("Bandes", new StackPanel { Spacing = 10, Children = { bandes, tAideBandes } }));
            gauche = g;

            // ---------------- colonne de droite
            var d = new StackPanel();
            vu = new VuMetre5();
            zoneSorties = new StackPanel { Spacing = 8 };
            var sortie = new StackPanel { Spacing = 10 };
            sortie.Children.Add(Studio.T(Langue.T("Ce que joue ta sortie par défaut"), 11.5, Studio.Faible));
            sortie.Children.Add(vu);
            tEcoute = Studio.T("", 11.5, Studio.Faible);
            sortie.Children.Add(tEcoute);
            sortie.Children.Add(zoneSorties);
            d.Children.Add(Studio.Module("Sorties", sortie));
            zoneVm = new StackPanel { Spacing = 8 };
            d.Children.Add(Studio.Module("Voicemeeter", zoneVm));
            zoneAuto = new StackPanel { Spacing = 12 };
            d.Children.Add(Studio.Module("Automatismes", zoneAuto));
            zoneFichier = new StackPanel { Spacing = 8 };
            tChemin = new TextBlock { FontFamily = Studio.Chiffres, FontSize = 11, Foreground = Studio.P(Studio.Faible), TextWrapping = TextWrapping.Wrap };
            tFichier = new TextBlock { FontFamily = Studio.Chiffres, FontSize = 11, Foreground = Studio.P(Studio.Discret), TextWrapping = TextWrapping.NoWrap };
            var defile = new ScrollViewer { Content = tFichier, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, IsVisible = false };
            var voir = Studio.Bouton(Langue.T("Voir le fichier écrit"), false, () => { defile.IsVisible = !defile.IsVisible; });
            zoneFichier.Children.Add(tChemin); zoneFichier.Children.Add(new WrapPanel { Children = { voir } }); zoneFichier.Children.Add(defile);
            d.Children.Add(Studio.Module("Fichier d'Equalizer APO", zoneFichier));
            droite = d;

            corps = new Grid();
            corps.Children.Add(gauche); corps.Children.Add(droite);
            Disposer(1000);
            corps.SizeChanged += (o, a) => Disposer(a.NewSize.Width);
            pile.Children.Add(corps);
            Construire(zoneAuto);
            return pile;
        }

        /// <summary>Deux colonnes en grand, une seule (la droite passe dessous) en étroit.</summary>
        private void Disposer(double largeur)
        {
            bool deux = largeur >= 920;
            if (deuxColonnes == deux) return;
            deuxColonnes = deux;
            corps.ColumnDefinitions = new ColumnDefinitions(deux ? "*,16,330" : "*");
            corps.RowDefinitions = deux ? new RowDefinitions() : new RowDefinitions("Auto,Auto");
            Grid.SetColumn(gauche, 0); Grid.SetRow(gauche, 0);
            Grid.SetColumn(droite, deux ? 2 : 0); Grid.SetRow(droite, deux ? 0 : 1);
        }

        private static Control Legende(Color c, string texte)
        {
            return new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new Border { Width = 10, Height = 3, CornerRadius = new CornerRadius(2), Background = Studio.P(c), VerticalAlignment = VerticalAlignment.Center }, new TextBlock { Text = Langue.T(texte), FontFamily = Studio.Chiffres, FontSize = 10.5, Foreground = Studio.P(Studio.Discret) } } };
        }

        private Border Puce(string texte, Action clic)
        {
            var b = new Border
            {
                Child = new TextBlock { Text = texte, FontFamily = Studio.Chiffres, FontSize = 12, Foreground = Studio.P(Studio.Texte) },
                Padding = new Thickness(14, 7), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1.2), Cursor = new Cursor(StandardCursorType.Hand), Focusable = true,
            };
            b.PointerReleased += (o, e) => clic();
            b.KeyDown += (o, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; clic(); } };
            return b;
        }

        private static void Marquer(Border b, bool choisi, Color? c = null)
        {
            var k = c ?? Studio.Ambre;
            b.Background = choisi ? Studio.P(k, 40) : Studio.P(Studio.Violet, 14);
            b.BorderBrush = Studio.P(choisi ? k : Studio.Bord);
        }

        private Control Molette(string nom, double min, double max, double pas, double defaut, bool bipolaire, Func<string> texte, Action<double> regler)
        {
            var m = new Molette5(min, max, pas, 0, defaut) { Bipolaire = bipolaire, Tag = nom, HorizontalAlignment = HorizontalAlignment.Center };
            m.Valeur = ValeurMolette(m);
            var t = new TextBlock { FontFamily = Studio.Chiffres, FontSize = 12, Foreground = Studio.P(Studio.Ambre), HorizontalAlignment = HorizontalAlignment.Center };
            m.Change += v => { regler(v); Changer(); };
            ToolTip.SetTip(m, Langue.T("Glisse vers le haut ou le bas, ou utilise la molette de la souris. Double clic : valeur par défaut."));
            molettes.Add(Tuple.Create(m, t, texte));
            return new StackPanel
            {
                Spacing = 4, Width = 112, Margin = new Thickness(0, 0, 8, 6),
                Children = { m, new TextBlock { Text = Langue.T(nom).ToUpper(Langue.Culture), FontFamily = Studio.Chiffres, FontSize = 11, LetterSpacing = 1.5, Foreground = Studio.P(Studio.Discret), HorizontalAlignment = HorizontalAlignment.Center }, t },
            };
        }

        private double ValeurMolette(Molette5 m)
        {
            switch ((string)m.Tag)
            {
                case "Force": return r.Intensite;
                case "Niveau": return r.Niveau;
                case "Balance": return r.Balance;
                default: return r.Largeur;
            }
        }

        private Control CarteProfil(ProfilAudio p)
        {
            var mini = new CourbeEq5(true) { Reglages = new ReglagesAudioJeu { Actif = true, Gains = (double[])p.Gains.Clone() } };
            string nom = Langue.T(p.Carte);
            var c = new Border
            {
                Padding = new Thickness(14, 12), Margin = new Thickness(0, 0, 10, 10), CornerRadius = new CornerRadius(16), BorderThickness = new Thickness(1.4), Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, MinHeight = 120,
                Child = new StackPanel
                {
                    Spacing = 6, Children =
                    {
                        mini,
                        new TextBlock { Text = nom.ToUpper(Langue.Culture), FontFamily = Studio.Titre, FontSize = 15, FontWeight = FontWeight.Bold, LetterSpacing = 1, Foreground = Studio.P(Studio.Texte), TextWrapping = TextWrapping.Wrap },
                        Studio.T(Langue.T(p.Texte), 12, Studio.Discret),
                    },
                },
            };
            ToolTip.SetTip(c, string.Join("   ", AudioJeu.Bandes.Select((b, i) => Langue.T(b.Nom) + " " + Studio.Db(p.Gains[i]))));
            c.PointerReleased += (s, e) => ChoisirProfil(p);
            c.KeyDown += (s, e) => { if (e.Key == Key.Enter || e.Key == Key.Space) { e.Handled = true; ChoisirProfil(p); } };
            cartes.Add(Tuple.Create(p, c, mini));
            return c;
        }

        /// <summary>Automatismes : Auto Warzone et raccourcis clavier globaux.</summary>
        private void Construire(StackPanel z)
        {
            string sig = r.AutoJeu + "|" + r.Raccourcis + "|" + RaccourcisAudio.Erreur;
            if (sig == signatureAuto) return;
            signatureAuto = sig;
            z.Children.Clear();
            z.Children.Add(Ligne("Auto Warzone", "L'égaliseur s'active quand Call of Duty / Warzone tourne et se coupe quand tu le fermes, tant que Velox est ouvert (même réduit).", r.AutoJeu, BasculerAuto));
            var touches = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            foreach (var t in new[] { Tuple.Create("F9", "égaliseur marche / arrêt"), Tuple.Create("F10", "profil suivant") })
            {
                var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 14, 4) };
                foreach (var k in new[] { "Ctrl", "Alt", t.Item1 }) s.Children.Add(new Border { Padding = new Thickness(7, 2), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1), BorderBrush = Studio.P(Studio.Bord), Background = Studio.P(Studio.Fond1), Child = new TextBlock { Text = k, FontFamily = Studio.Chiffres, FontSize = 11, Foreground = Studio.P(Studio.Texte) } });
                s.Children.Add(new TextBlock { Text = Langue.T(t.Item2), FontFamily = Studio.Corps, FontSize = 12, Foreground = Studio.P(Studio.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(4, 0, 0, 0) });
                touches.Children.Add(s);
            }
            z.Children.Add(Ligne("Raccourcis clavier", "Même en plein jeu : un bip aigu quand l'égaliseur s'allume, un grave quand il se coupe, et autant de bips que le numéro du profil choisi.", r.Raccourcis, BasculerRaccourcis));
            z.Children.Add(touches);
            if (RaccourcisAudio.Erreur != "") z.Children.Add(Studio.T(Langue.T(RaccourcisAudio.Erreur), 12, Studio.Rouge));
        }

        private Control Ligne(string titre, string texte, bool actif, Action<bool> demande)
        {
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            g.Children.Add(new StackPanel { Spacing = 3, Children = { Studio.T(Langue.T(titre), 14, Studio.Texte, true, Studio.Titre), Studio.T(Langue.T(texte), 12, Studio.Discret) } });
            var b = Puce(Langue.T(actif ? "ACTIVÉ" : "DÉSACTIVÉ"), () => demande(!actif));
            Marquer(b, actif, Studio.Vert);
            b.VerticalAlignment = VerticalAlignment.Top; b.Margin = new Thickness(12, 0, 0, 0);
            Grid.SetColumn(b, 1); g.Children.Add(b);
            return g;
        }

        /// <summary>Sans Premium : la présentation de l'outil, et le moyen de couper un égaliseur resté actif.</summary>
        private Control Verrou()
        {
            var pile = new StackPanel();
            pile.Children.Add(EnTete(false));
            var demo = new CourbeEq5 { Reglages = new ReglagesAudioJeu { Actif = true, Gains = (double[])AudioJeu.Profils[0].Gains.Clone() }, Spectre = SpectreExemple.Select(x => (float)x).ToArray(), IsHitTestVisible = false, Focusable = false };
            pile.Children.Add(Studio.Module("Réponse en fréquence", demo));
            var s = new StackPanel { Spacing = 10 };
            s.Children.Add(new TextBlock { Text = Langue.T("Le Studio son fait partie de Velox Premium"), FontFamily = Studio.Titre, FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Studio.P(Studio.Texte), TextWrapping = TextWrapping.Wrap });
            foreach (var t in new[]
            {
                "Quatre profils pensés pour Call of Duty / Warzone : les pas et les recharges passent devant, les explosions reculent.",
                "Courbe de l'égaliseur et spectre de ton son en direct, molettes et faders pour tout régler à l'oreille.",
                "Auto Warzone : l'égaliseur s'allume avec le jeu et s'éteint avec lui. Raccourcis clavier en plein jeu.",
                "Sortie par défaut de Windows en un clic, Voicemeeter reconnu (niveaux, égaliseur de Banana et Potato).",
            }) s.Children.Add(Studio.T("◆  " + Langue.T(t), 13, Studio.Discret));
            boutonsVerrou = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            boutonsVerrou.Children.Add(Studio.Bouton(Langue.T("Essayer Premium"), true, () => Achat5.Ouvrir(F)));
            // un égaliseur actif (écrit avant, ou par le prototype) peut toujours être coupé, même sans Premium
            bCouper = Studio.Bouton(Langue.T("Couper l'égaliseur"), false, () =>
            {
                if (exemple) return;
                if (simulation) { F.Statut(Langue.T("Simulation : le fichier d'Equalizer APO serait réécrit, rien n'est modifié."), true); return; }
                var c = Reglages; c.AutoJeu = false; c.Actif = false; c.Moteur = "apo";
                Ecrire(F, true, false, Langue.T("Égaliseur coupé."));
            });
            s.Children.Add(boutonsVerrou);
            pile.Children.Add(Studio.Module(null, s));
            return pile;
        }

        // ---------------------------------------------------------------- mise à jour

        internal void MettreAJour()
        {
            var v = Releve ?? new ReleveAudioJeu();
            if (!deverrouille || marche == null)
            {
                if (temoins != null) { temoins.Children.Clear(); temoins.Children.Add(Studio.Temoin(v.Etat.Actif ? Langue.T("ÉGALISEUR ACTIF") : Langue.T("ÉGALISEUR COUPÉ"), Studio.Vert, v.Etat.Actif)); }
                if (boutonsVerrou != null && bCouper != null)
                {
                    bool montrer = v.Etat.Actif && !exemple;
                    if (montrer && !boutonsVerrou.Children.Contains(bCouper)) boutonsVerrou.Children.Add(bCouper);
                    if (!montrer) boutonsVerrou.Children.Remove(bCouper);
                }
                return;
            }
            var vm = Vm;
            bool moteurVm = r.Moteur == "vm";
            // ---- témoins
            temoins.Children.Clear();
            temoins.Children.Add(Studio.Temoin(moteurVm ? vm.Nom.ToUpperInvariant() : "EQUALIZER APO", Studio.Vert, moteurVm ? vm.EgaliseurNatif : v.Etat.Present));
            temoins.Children.Add(Studio.Temoin(v.Jeu != null ? Langue.F("JEU : {0}", v.Jeu.ToUpperInvariant()) : Langue.T("JEU NON LANCÉ"), Studio.Ambre, v.Jeu != null));
            temoins.Children.Add(Studio.Temoin(vm.Lance ? vm.Nom.ToUpperInvariant() : vm.Installe ? Langue.T("VOICEMEETER ARRÊTÉ") : Langue.T("SANS VOICEMEETER"), Studio.Cyan, vm.Lance));
            if (r.AutoJeu) temoins.Children.Add(Studio.Temoin("AUTO WARZONE", Studio.Magenta, true));
            marche.Actif = r.Actif;
            tMarche.Text = Langue.T(r.Actif ? "ACTIF" : "COUPÉ");
            tMarche.Foreground = Studio.P(r.Actif ? Studio.Ambre : Studio.Faible);

            // ---- avis
            avis.Children.Clear();
            Action<string, Color> avertir = (t, c) => avis.Children.Add(new Border
            {
                Padding = new Thickness(14, 9), CornerRadius = new CornerRadius(12), Background = Studio.P(c, 22), BorderBrush = Studio.P(c, 120), BorderThickness = new Thickness(1),
                Child = Studio.T(t, 12.5, Studio.Texte),
            });
            if (simulation) avertir(Langue.T("Mode simulation : tes essais ici ne sont pas écrits, le fichier d'Equalizer APO ne change pas."), Studio.Ambre);
            if (!moteurVm && !v.Etat.Present)
            {
                avertir(Langue.T("Equalizer APO n'est pas installé sur ce PC. Installe-le (gratuit, sourceforge.net/projects/equalizerapo), coche ta sortie (casque ou haut-parleurs) dans la fenêtre qu'il ouvre, redémarre le PC, puis reviens ici. Tant qu'il n'est pas là, Velox ne modifie rien."), Studio.Rouge);
                avis.Children.Add(new WrapPanel { Children = { Studio.Bouton(Langue.T("Ouvrir la page d'Equalizer APO"), true, () => { if (!exemple) Programmes.Ouvrir(AudioJeu.Site); }), Studio.Bouton(Langue.T("Vérifier à nouveau"), false, () => { if (!exemple) { F.Statut(Langue.T("Recherche d'Equalizer APO…")); Relever(); } }) } });
            }
            if (!moteurVm && v.Etat.Present)
            {
                int etat; Cible(v, out etat);
                if (etat == 3) avertir(Langue.T("Equalizer APO n'est installé sur aucune sortie : ouvre le Configurator, coche ta sortie, puis redémarre le PC."), Studio.Rouge);
                else if (etat == 2) avertir(Langue.T("La sortie choisie n'a plus Equalizer APO : choisis-en une autre, ou coche-la de nouveau dans le Configurator."), Studio.Ambre);
                else if (etat == 1) avertir(Langue.F("Ta sortie par défaut ({0}) n'a pas Equalizer APO : l'égaliseur vise toutes les sorties où il est installé. Si tu n'entends aucune différence, ouvre le Configurator, coche ta sortie, puis redémarre le PC.", v.NomDefaut != "" ? v.NomDefaut : Langue.T("inconnue")), Studio.Ambre);
                if (v.Etat.Autres.Count > 0) avertir(Langue.F("D'autres traitements sont actifs dans Equalizer APO ({0}) : leurs effets s'ajoutent à cet égaliseur.", string.Join(", ", v.Etat.Autres)), Studio.Violet);
                var vrais = exemple ? r : Reglages;
                if (v.Etat.Actif && !v.Etat.Include) avertir(Langue.T("Le fichier config.txt d'Equalizer APO n'inclut pas cet égaliseur : il n'a aucun effet. Coupe puis réactive l'interrupteur pour remettre la ligne."), Studio.Ambre);
                else if (v.Etat.Actif && !enEcriture && !attente && v.Etat.Contenu != AudioJeu.Generer(vrais, vrais.CibleEcrite))
                    avertir(Langue.T("Le fichier en place ne vient pas de ces réglages (autre outil, ou modifié à la main) : ton prochain réglage ici le remplacera."), Studio.Violet);
            }
            if (r.Niveau > 0) avertir(Langue.T("Niveau au-dessus de 0 dB : le son peut saturer (grésillements). Baisse-le si tu en entends."), Studio.Ambre);

            // ---- courbe, profils, molettes, bandes
            courbe.Reglages = r; courbe.Allume = r.Actif; courbe.Actualiser();
            var courant = AudioJeu.ProfilDe(r.Gains);
            foreach (var c in cartes)
            {
                bool choisi = courant == c.Item1;
                c.Item2.Background = choisi ? (IBrush)Studio.Degrade(1, Habillage.Alpha(Studio.Magenta, 46), Habillage.Alpha(Studio.Violet, 26)) : Studio.P(Studio.Fond1, 150);
                c.Item2.BorderBrush = choisi ? (IBrush)Studio.Degrade(0, Studio.Ambre, Studio.Magenta) : Studio.P(Studio.Bord);
                c.Item2.BoxShadow = choisi ? BoxShadows.Parse("0 0 18 0 " + Habillage.Alpha(Studio.Magenta, 90)) : default(BoxShadows);
            }
            tProfil.Text = Langue.F("Profil en cours : {0} · force {1} %", Langue.T(AudioJeu.NomProfil(r)), r.Intensite);
            foreach (var m in molettes) { m.Item1.Valeur = ValeurMolette(m.Item1); m.Item2.Text = m.Item3(); }
            foreach (var c in coupes) Marquer(c.Item2, c.Item1 == r.CoupeBas);
            Marquer(bInversion, r.Inversion);
            tMoteur.Text = moteurVm ? Langue.T("Égaliseur dans Voicemeeter : la largeur stéréo et l'inversion n'y existent pas, et le coupe-explosions prend la place de la bande Graves (6 cellules par bus).") : "";
            for (int i = 0; i < faders.Length; i++) { faders[i].Valeur = r.Gains[i]; tFaders[i].Text = Studio.Db(r.Gains[i]); tFaders[i].Foreground = Studio.P(r.Gains[i] > 0 ? Studio.Ambre : r.Gains[i] < 0 ? Studio.Cyan : Studio.Texte); }
            tAideBandes.Text = r.Intensite == 100 ? Langue.T("Chaque gain va de −12 à +12 dB. La force de l'effet les multiplie tous.")
                : Langue.F("Avec la force à {0} %, les gains réellement appliqués sont : {1}.", r.Intensite, string.Join("  ", AudioJeu.Bandes.Select((b, i) => Studio.Db(AudioJeu.GainEffectif(r, i)))));

            // ---- sorties, Voicemeeter, automatismes, fichier
            if (exemple) vu.Definir(0.42f, 0.36f);
            MajSorties(v);
            MajVoicemeeter();
            Construire(zoneAuto);
            int e2; string guid = Cible(v, out e2);
            tChemin.Text = moteurVm ? Langue.T("L'égaliseur passe par Voicemeeter : le fichier d'Equalizer APO est coupé.") : Path.Combine(v.Dossier, AudioJeu.NomFichier);
            tFichier.Text = AudioJeu.Generer(r, guid).TrimEnd();
        }

        private string Cible(ReleveAudioJeu v, out int etat) { return AudioJeu.Resoudre(r.Cible, v.Sorties, v.Defaut, out etat); }

        /// <summary>Les sorties de Windows : la sortie par défaut, Equalizer APO présent ou non, et les boutons
        /// « Viser » (l'égaliseur s'applique à elle) et « Par défaut » (Windows l'utilise). Refaite seulement si elle change.</summary>
        private void MajSorties(ReleveAudioJeu v)
        {
            string sig = string.Join("|", v.Windows.Select(s => s.Id + s.Nom + s.ParDefaut)) + "#" + string.Join("|", v.Sorties.Select(s => s.Guid)) + "#" + r.Cible + r.Moteur + v.Etat.Present;
            if (sig == signatureSorties) return;
            signatureSorties = sig;
            zoneSorties.Children.Clear();
            bool apo = r.Moteur != "vm" && v.Etat.Present;
            if (apo)
            {
                var choix = new WrapPanel();
                var bAuto = Puce(Langue.T("Auto"), () => ViserSortie("auto")); Marquer(bAuto, r.Cible == "auto"); ToolTip.SetTip(bAuto, Langue.T("Suit la sortie par défaut de Windows."));
                var bToutes = Puce(Langue.T("Toutes"), () => ViserSortie("toutes")); Marquer(bToutes, r.Cible == "toutes"); ToolTip.SetTip(bToutes, Langue.T("Toutes les sorties où Equalizer APO est installé"));
                choix.Children.Add(new TextBlock { Text = Langue.T("Égaliseur sur :"), FontFamily = Studio.Corps, FontSize = 12.5, Foreground = Studio.P(Studio.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) });
                choix.Children.Add(bAuto); choix.Children.Add(bToutes);
                zoneSorties.Children.Add(choix);
            }
            var liste = v.Windows.Count > 0 ? v.Windows : v.Sorties.Select(s => new SortieWindows { Guid = s.Guid, Nom = s.Libelle, ParDefaut = s.Guid == v.Defaut }).ToList();
            if (liste.Count == 0) zoneSorties.Children.Add(Studio.T(Langue.T("Aucune sortie audio active trouvée."), 12.5, Studio.Discret));
            foreach (var s in liste)
            {
                var ss = s;
                bool aApo = v.Sorties.Any(x => x.Guid == s.Guid), visee = r.Cible == s.Guid;
                var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                haut.Children.Add(Studio.T(s.Nom != "" ? s.Nom : s.Guid, 13, Studio.Texte, true));
                if (s.ParDefaut) { var badge = Studio.Temoin(Langue.T("PAR DÉFAUT"), Studio.Ambre, true); badge.Margin = new Thickness(8, 0, 0, 0); Grid.SetColumn(badge, 1); haut.Children.Add(badge); }
                var bas = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
                bas.Children.Add(new TextBlock { Text = aApo ? "● Equalizer APO" : "○ " + Langue.T("sans Equalizer APO"), FontFamily = Studio.Chiffres, FontSize = 11, Foreground = Studio.P(aApo ? Studio.Vert : Studio.Faible), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 8) });
                if (apo && aApo)
                {
                    var bv = Puce(Langue.T(visee ? "Visée" : "Viser"), () => ViserSortie(ss.Guid)); Marquer(bv, visee);
                    ToolTip.SetTip(bv, Langue.T("L'égaliseur s'applique seulement à cette sortie."));
                    bas.Children.Add(bv);
                }
                if (!s.ParDefaut && s.Id != "")
                {
                    var bd = Puce(Langue.T("Par défaut"), () => SortieParDefaut(ss));
                    Marquer(bd, false, Studio.Cyan);
                    ToolTip.SetTip(bd, Langue.T("Windows joue le son sur cette sortie (comme Paramètres > Son). S'annule dans l'Historique."));
                    bas.Children.Add(bd);
                }
                zoneSorties.Children.Add(new Border { Padding = new Thickness(12, 10, 12, 4), CornerRadius = new CornerRadius(12), Background = Studio.P(s.ParDefaut ? Studio.Ambre : Studio.Violet, s.ParDefaut ? 18 : 10), BorderBrush = Studio.P(visee ? Studio.Ambre : Studio.Bord, visee ? 200 : 160), BorderThickness = new Thickness(1), Child = new StackPanel { Children = { haut, bas } } });
            }
            if (v.Etat.Present)
            {
                zoneSorties.Children.Add(Studio.T(Langue.T("Pour qu'une sortie ait Equalizer APO, coche-la dans son Configurator puis redémarre le PC. Laisse les « Améliorations audio » de Windows activées sur elle : Equalizer APO en a besoin."), 12, Studio.Faible));
                zoneSorties.Children.Add(new WrapPanel { Children = { Studio.Bouton(Langue.T("Ouvrir le Configurator"), false, OuvrirConfigurator) } });
            }
        }

        /// <summary>Voicemeeter : détection, niveaux de ses bus, choix du moteur de l'égaliseur (Banana, Potato).</summary>
        private void MajVoicemeeter()
        {
            if (zoneVm == null) return;
            var e = Vm;
            string sig = e.Installe + "|" + e.Lance + "|" + e.Type + "|" + e.Version + "|" + string.Join(",", e.Bus.Select(b => b.Nom + b.Label + b.Appareil)) + "|" + r.Moteur + r.BusVm;
            if (sig == signatureVm) return;
            signatureVm = sig;
            zoneVm.Children.Clear(); vuBus.Clear();
            if (!e.Installe && !e.Lance)
            {
                zoneVm.Children.Add(Studio.T(Langue.T("Voicemeeter n'est pas installé : rien à régler ici. L'égaliseur passe par Equalizer APO."), 12.5, Studio.Discret));
                return;
            }
            if (!e.Lance)
            {
                zoneVm.Children.Add(Studio.T(Langue.T("Voicemeeter est installé mais pas lancé. Lance-le pour voir ses niveaux ici."), 12.5, Studio.Discret));
                return;
            }
            zoneVm.Children.Add(Studio.T(e.Nom + (e.Version != "" ? "  ·  " + e.Version : ""), 14, Studio.Texte, true, Studio.Titre));
            foreach (var b in e.Bus)
            {
                var m = new VuMetre5 { Height = 16 };
                vuBus.Add(Tuple.Create(b, m));
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("38,*") };
                g.Children.Add(new TextBlock { Text = b.Nom, FontFamily = Studio.Chiffres, FontSize = 12, Foreground = Studio.P(b.Physique ? Studio.Ambre : Studio.Cyan), VerticalAlignment = VerticalAlignment.Center });
                var droiteBus = new StackPanel { Spacing = 2, Children = { m } };
                string detail = string.Join(" · ", new[] { b.Label, b.Appareil }.Where(x => !string.IsNullOrEmpty(x)));
                if (detail != "") droiteBus.Children.Add(new TextBlock { Text = detail, FontFamily = Studio.Corps, FontSize = 11, Foreground = Studio.P(Studio.Faible), TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(droiteBus, 1); g.Children.Add(droiteBus);
                zoneVm.Children.Add(g);
                if (exemple) m.Definir(b.G, b.D);
            }
            var a = e.Bus.FirstOrDefault(b => b.Physique);
            zoneVm.Children.Add(Studio.T(Langue.F("Avec Voicemeeter, le son du jeu passe d'abord par « Voicemeeter Input », puis sort sur l'appareil du bus {0}{1}. Equalizer APO agit sur la sortie où il est installé : vise celle où tu entends l'effet.", a != null ? a.Nom : "A", a != null && a.Appareil != "" ? " (" + a.Appareil + ")" : ""), 12, Studio.Faible));
            if (!e.EgaliseurNatif)
            {
                zoneVm.Children.Add(Studio.T(Langue.T("L'égaliseur des bus n'existe que dans Voicemeeter Banana et Potato : ici, l'égaliseur passe par Equalizer APO."), 12, Studio.Discret));
                return;
            }
            var moteur = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            moteur.Children.Add(new TextBlock { Text = Langue.T("Égaliseur :"), FontFamily = Studio.Corps, FontSize = 12.5, Foreground = Studio.P(Studio.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) });
            var bApo = Puce("Equalizer APO", () => ChoisirMoteur("apo", r.BusVm)); Marquer(bApo, r.Moteur != "vm");
            moteur.Children.Add(bApo);
            foreach (var b in e.Bus.Where(x => x.Physique))
            {
                var bb = b;
                var p = Puce("Voicemeeter " + b.Nom, () => ChoisirMoteur("vm", bb.Index)); Marquer(p, r.Moteur == "vm" && r.BusVm == b.Index);
                ToolTip.SetTip(p, Langue.T("L'égaliseur du bus de Voicemeeter (canaux gauche et droit) remplace celui d'Equalizer APO."));
                moteur.Children.Add(p);
            }
            zoneVm.Children.Add(moteur);
        }

        // ---------------------------------------------------------------- changements

        /// <summary>Un réglage vient de changer : l'affichage suit tout de suite, l'écriture attend un court instant sans
        /// nouveau changement (une molette qu'on tourne ne réécrit pas le fichier à chaque pas).</summary>
        private void Changer(string message = null, bool toujours = false, bool toutDeSuite = false)
        {
            MettreAJour();
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
            if (simulation || accord || r.Moteur == "vm" || (v != null && v.Etat.Include)) { suite(); return; }
            F.Demander("Activer l'égaliseur avec Equalizer APO ?",
                Langue.F("Velox va écrire le fichier {0} dans le dossier d'Equalizer APO et ajouter la ligne « {1} » à la fin de son config.txt. Rien d'autre n'y est modifié, le jeu n'est pas touché, et tout s'annule dans l'Historique.", AudioJeu.NomFichier, AudioJeu.LigneInclude),
                "Activer", "Annuler", oui => { if (!oui) { MettreAJour(); return; } accord = true; suite(); });
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

        private void BasculerRaccourcis(bool voulu)
        {
            if (exemple) return;
            r.Raccourcis = voulu; signatureAuto = null;
            if (!simulation)
            {
                AudioJeu.Enregistrer(Reglages);
                RaccourcisAudio.Activer(voulu && Premium);
            }
            F.Statut(Langue.T(voulu ? (RaccourcisAudio.Erreur == "" ? "Raccourcis activés : Ctrl+Alt+F9 pour l'égaliseur, Ctrl+Alt+F10 pour le profil suivant." : RaccourcisAudio.Erreur) : "Raccourcis désactivés."), voulu && RaccourcisAudio.Erreur != "");
            Construire(zoneAuto);
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
            Changer(Langue.T("Réglage automatique : profil compétitif, force 100 %, sortie Auto, le reste remis à zéro.") + (r.Actif || r.AutoJeu ? "" : " " + Langue.T("L'égaliseur est coupé : active-le pour l'entendre.")), false, true);
        }

        private void ViserSortie(string cible)
        {
            if (exemple || r.Cible == cible) return;
            r.Cible = cible;
            Changer(Langue.T("Sortie visée changée."), false, true);
        }

        private void ChoisirMoteur(string moteur, int bus)
        {
            if (exemple || (r.Moteur == moteur && r.BusVm == bus)) return;
            r.Moteur = moteur; r.BusVm = bus;
            Changer(Langue.T(moteur == "vm" ? "L'égaliseur passe maintenant par Voicemeeter (celui d'Equalizer APO est coupé)." : "L'égaliseur passe maintenant par Equalizer APO (celui de Voicemeeter est coupé)."), false, true);
        }

        private void SortieParDefaut(SortieWindows s)
        {
            if (exemple) return;
            string nom = s.Nom != "" ? s.Nom : s.Guid;
            if (simulation) { F.Statut(Langue.F("Simulation : « {0} » deviendrait la sortie par défaut, rien n'est modifié.", nom), true); return; }
            F.Statut(Langue.T("Changement de la sortie par défaut…"));
            Task.Run(() => AudioWindows.DefinirParDefaut(s.Id)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                if (err == "") F.Statut(Langue.F("« {0} » est maintenant la sortie par défaut (annulable dans l'Historique).", nom));
                else F.Statut(Langue.T(err), true);
                signatureSorties = null;
                Relever();
            }));
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

    /// <summary>Raccourcis clavier globaux de l'audio (Premium, désactivés par défaut) : Ctrl+Alt+F9 et Ctrl+Alt+F10,
    /// enregistrés par un petit fil qui attend les messages de Windows (même principe que Ctrl+Alt+O de l'overlay).
    /// Touches de fonction choisies exprès : Ctrl+Alt+lettre, c'est AltGr+lettre sur un clavier français (€ avec E).</summary>
    internal static class RaccourcisAudio
    {
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        [DllImport("user32.dll")] private static extern int GetMessageW(out MSG msg, IntPtr hWnd, uint min, uint max);
        [DllImport("user32.dll")] private static extern bool PeekMessageW(out MSG m, IntPtr h, uint a, uint b, uint remove);
        [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint thread, uint msg, IntPtr w, IntPtr l);
        [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
        [StructLayout(LayoutKind.Sequential)] private struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }
        private const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_NOREPEAT = 0x4000, WM_HOTKEY = 0x312, WM_QUIT = 0x12, VK_F9 = 0x78, VK_F10 = 0x79;
        private const int IdEq = 0x5611, IdProfil = 0x5612;
        private static Thread fil;
        private static volatile uint idFil;
        private static readonly ManualResetEventSlim pret = new ManualResetEventSlim(false);
        /// <summary>Raison si un raccourci n'a pas pu être pris ("" sinon).</summary>
        public static string Erreur = "";

        public static void Activer(bool oui)
        {
            if (!OperatingSystem.IsWindows()) return;
            if (!oui)
            {
                if (fil != null)
                {
                    pret.Wait(1000);
                    if (idFil != 0) PostThreadMessageW(idFil, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
                    fil.Join(1000);
                    fil = null;
                }
                Erreur = "";
                return;
            }
            if (fil != null && fil.IsAlive) return;
            pret.Reset(); Erreur = "";
            var prise = new ManualResetEventSlim(false);
            fil = new Thread(() =>
            {
                try
                {
                    MSG p; PeekMessageW(out p, IntPtr.Zero, 0, 0, 0);   // file de messages créée avant d'annoncer que le fil est prêt
                    idFil = GetCurrentThreadId();
                    pret.Set();
                    bool eq = RegisterHotKey(IntPtr.Zero, IdEq, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_F9);
                    bool profil = RegisterHotKey(IntPtr.Zero, IdProfil, MOD_CONTROL | MOD_ALT | MOD_NOREPEAT, VK_F10);
                    if (!eq || !profil) Erreur = !eq && !profil ? "Ctrl+Alt+F9 et Ctrl+Alt+F10 sont déjà pris par un autre logiciel." : !eq ? "Ctrl+Alt+F9 est déjà pris par un autre logiciel." : "Ctrl+Alt+F10 est déjà pris par un autre logiciel.";
                    prise.Set();
                    MSG m;
                    while (GetMessageW(out m, IntPtr.Zero, 0, 0) > 0)
                        if (m.message == WM_HOTKEY) { int id = m.wParam.ToInt32(); Dispatcher.UIThread.Post(() => AudioJeu5.Raccourci(id == IdEq ? 0 : 1)); }
                    if (eq) UnregisterHotKey(IntPtr.Zero, IdEq);
                    if (profil) UnregisterHotKey(IntPtr.Zero, IdProfil);
                }
                catch (Exception ex) { JournalDemarrage.Erreur("raccourcis de l'audio", ex); }
                finally { idFil = 0; pret.Set(); prise.Set(); }
            }) { IsBackground = true, Name = "Raccourcis audio" };
            fil.Start();
            prise.Wait(1500);   // pour dire tout de suite si une touche est déjà prise
        }
    }
}
