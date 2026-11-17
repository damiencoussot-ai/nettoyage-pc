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
using Avalonia.Automation;
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
        public EtatHeSuVi HeSuVi = new EtatHeSuVi();
        /// <summary>« Pas boostés » : câbles virtuels trouvés, réglages, état du moteur.</summary>
        public List<CableVirtuel> Cables = new List<CableVirtuel>();
        public ReglagesMoteurSon Moteur = new ReglagesMoteurSon();
        public MoteurSon.EtatMoteur MoteurEtat = new MoteurSon.EtatMoteur();

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
                r.Cables = MoteurSon.Cables(r.Windows);
                r.Moteur = MoteurSon.Lire();
                r.MoteurEtat = MoteurSon.LireEtat();
                // allumé mais moteur arrêté (PC redémarré sans gardien…) : relancé
                if (r.Moteur.Actif && r.Cables.Count > 0 && !MoteurSon.EnMarche()) MoteurSon.Surveiller();   // relancé, ou coupé (casque remis) s'il ne démarre pas
                if (r.Etat.Present)
                {
                    r.Sorties = AudioJeu.Sorties();
                    r.Configurator = AudioJeu.Configurator != null;
                    r.HeSuVi = NettoyagePC.HeSuVi.Etat();
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
            // « Comparer » interrompu (Velox fermé de force pendant l'appui) : l'égaliseur revient au démarrage suivant.
            // Seulement si les réglages enregistrés le disent actif et que le fichier en place est la ligne « coupé » de Velox.
            if (!f.Simulation) Task.Run(() =>
            {
                try
                {
                    var r0 = AudioJeu.Lire();
                    var e0 = AudioJeu.Etat();
                    if (r0.Actif && r0.Moteur != "vm" && AudioJeu.Present && e0.Fichier && !e0.Actif)
                        AudioJeu.Comparer(false, AudioJeu.Generer(r0, r0.CibleEcrite));
                }
                catch { }
            });
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
            if (f.Simulation || autoBloque || comparaison || !Licence.Premium) return;
            bool vm = r.Moteur == "vm";
            if (!vm && !v.Etat.Present) return;
            if (r.AutoJeu)
            {
                bool actuel = vm ? r.Actif : v.Etat.Actif;
                var jeu = AudioJeu.JeuDe(v.Jeu);
                if (AudioJeu.SuivreJeu(r, v.Jeu, actuel))
                {
                    if (page != null) page.MettreAJourBientot();
                    if (jeu != null) avisApres = Tuple.Create(Langue.T(r.ProfilParJeu ? "Profil du jeu appliqué" : "Égaliseur actif"), jeu.Nom, true);
                    Ecrire(f, false, false, jeu != null ? (r.ProfilParJeu ? Langue.F("Auto jeux : {0} détecté, égaliseur actif avec son profil.", jeu.Nom) : Langue.F("Auto jeux : {0} détecté, égaliseur actif.", jeu.Nom)) : Langue.T("Auto jeux : jeu fermé, égaliseur coupé."));
                    return;
                }
            }
            if (vm) return;
            r.Actif = v.Etat.Actif;   // (un autre outil a pu changer le fichier)
            // sortie par défaut changée (casque branché…) : le fichier écrit par Velox la suit ; celui d'un autre outil n'est pas touché
            if (r.Actif && v.Etat.Contenu == AudioJeu.Generer(r, r.CibleEcrite))
            {
                int etat;
                if (AudioJeu.Resoudre(r.Cible, v.Sorties, v.Defaut, out etat, v.NomDefaut, r.AppareilVm) != r.CibleEcrite) Ecrire(f, false, false, null);
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
                string erreur, avert = "", guidEcrit = "";
                List<SortieAudio> sortiesLues = null;
                if (copie.Moteur == "vm")
                {
                    // l'égaliseur passe par Voicemeeter. Celui d'Equalizer APO n'est coupé (pour ne pas doubler l'effet) qu'une
                    // fois Voicemeeter réglé : si Voicemeeter est fermé ou refuse, l'égaliseur d'APO reste en place.
                    if (copie.VmEcrit >= 0 && copie.VmEcrit != copie.BusVm) Voicemeeter.Couper(copie.VmEcrit, utilisateur);
                    erreur = Voicemeeter.Appliquer(copie, copie.BusVm, utilisateur);
                    if (erreur == "")
                    {
                        copie.VmEcrit = copie.Actif ? copie.BusVm : -1;
                        if (AudioJeu.Present && AudioJeu.Etat().Actif)
                        {
                            var sansApo = copie.Copie(); sansApo.Actif = false;
                            string e2 = AudioJeu.Appliquer(sansApo, "", utilisateur);
                            if (e2 != "") avert = Langue.T("Voicemeeter est réglé, mais l'égaliseur d'Equalizer APO n'a pas pu être coupé : les deux s'ajoutent.") + " " + Langue.T(e2);
                        }
                    }
                }
                else
                {
                    int etat;
                    string defaut = AudioJeu.SortieParDefaut();
                    sortiesLues = AudioJeu.Sorties();
                    string guid = AudioJeu.Resoudre(copie.Cible, sortiesLues, defaut, out etat, AudioJeu.NomSortie(defaut), copie.AppareilVm);
                    guidEcrit = guid;
                    erreur = AudioJeu.Appliquer(copie, guid, utilisateur, toujours);
                    if (erreur == "" && copie.VmEcrit >= 0 && Voicemeeter.Couper(copie.VmEcrit, utilisateur) == "") copie.VmEcrit = -1;
                }
                if (erreur == "") { AudioJeu.Enregistrer(copie); EtiqueterVm(copie, guidEcrit, sortiesLues); }
                return Tuple.Create(erreur, copie, ReleveAudioJeu.Lire(), avert);
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enEcriture = false; version++;
                string erreur = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result.Item1;
                if (!t.IsFaulted) { releve = t.Result.Item3; dejaReleve = true; }
                if (erreur != "")
                {
                    avisApres = null;
                    // rien n'a été changé : l'interrupteur revient à ce que dit le fichier, et la veille ne réessaie pas en boucle
                    if (releve != null && r.Moteur != "vm") r.Actif = releve.Etat.Actif;
                    // passage vers Voicemeeter refusé : on reste sur le moteur enregistré (Equalizer APO n'a pas été coupé)
                    if (!t.IsFaulted && t.Result.Item2.Moteur == "vm") { try { var lu = AudioJeu.Lire(); r.Moteur = lu.Moteur; r.BusVm = lu.BusVm; } catch { } }
                    if (!utilisateur) autoBloque = true;
                    aRefaire = false;
                    f.Statut(Langue.T(erreur) + (erreur == AudioJeu.Refus ? " " + Langue.T("Vérifie les droits du dossier (clic droit > Propriétés > Sécurité), ou réinstalle Equalizer APO.") : ""), true);
                }
                else
                {
                    r.CibleEcrite = t.Result.Item2.CibleEcrite; r.VmEcrit = t.Result.Item2.VmEcrit;
                    if (t.Result.Item4 != "") f.Statut(t.Result.Item4, true);
                    else if (message != null) f.Statut(message);
                    // le profil est vraiment écrit : la confirmation s'affiche maintenant (pas avant)
                    var a = avisApres; avisApres = null;
                    if (a != null) Confirmer(f, a.Item1, a.Item2, a.Item3);
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

        /// <summary>
        /// Nom « Velox Studio » dans Voicemeeter sur ce que l'égaliseur traite : le bus égalisé par Voicemeeter, l'entrée
        /// virtuelle (Voicemeeter Input…) ou le bus dont l'appareil est la sortie égalisée par Equalizer APO. Seulement si
        /// Voicemeeter est lancé ; un nom donné par l'utilisateur n'est jamais remplacé. Égaliseur coupé : le nom est retiré.
        /// </summary>
        private static void EtiqueterVm(ReglagesAudioJeu c, string guid, List<SortieAudio> sorties)
        {
            try
            {
                // état relu (page fermée : celui reçu de l'écoute peut être ancien) ; Voicemeeter absent : rien n'est chargé
                if (Voicemeeter.Dossier() == null) return;
                var e = Voicemeeter.Lire();
                if (e == null || !e.Lance) return;
                int bus = -1, entree = -1;
                if (c.Actif && !c.SansEtiquetteVm)
                {
                    if (c.Moteur == "vm") bus = c.BusVm;
                    else if (!string.IsNullOrEmpty(guid) && sorties != null)
                    {
                        entree = Voicemeeter.EntreeVirtuelle(AudioJeu.NomSortie(guid), e.Type);
                        if (entree < 0)
                        {
                            var b = e.Bus.FirstOrDefault(x => x.Physique && x.Appareil != "" && AudioJeu.SortieVoicemeeter(x.Appareil, sorties) == guid);
                            if (b != null) bus = b.Index;
                        }
                    }
                }
                Voicemeeter.Etiqueter(bus, entree);
            }
            catch { }
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
                // le gardien (Premium) suit le jeu même Velox fermé : l'égaliseur reste comme il est
                if (r.Moteur != "vm" && Licence.Premium && Gardien.EnCours) return;
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
                BulleAudio.Montrer(Langue.T(r.Actif ? "ÉGALISEUR ACTIF" : "ÉGALISEUR COUPÉ"), r.Actif ? Langue.T(AudioJeu.NomProfil(r)) : "", r.Actif);
            }
            else
            {
                var p = AudioJeu.ProfilSuivant(r.Gains);
                r.Gains = (double[])p.Gains.Clone();
                message = Langue.F("Profil « {0} » appliqué.", Langue.T(p.Nom));
                Bip(Enumerable.Repeat(660, Array.IndexOf(AudioJeu.Profils, p) + 1).ToArray());
                BulleAudio.Montrer(Langue.T(p.Carte).ToUpper(Langue.Culture), Langue.T(r.Actif ? "profil appliqué" : "profil choisi (égaliseur coupé)"), r.Actif);
            }
            if (page != null) page.Recharger();
            Ecrire(f, true, false, message);
        }

        // ---- bouton « Comparer » : tant qu'il est maintenu, l'égaliseur est coupé (rien n'est enregistré ni noté)
        private static bool comparaison;
        private static Task fileComparaison = Task.CompletedTask;

        internal static void Comparer(FenetrePrincipale f, bool appuye)
        {
            if (Exemple != null || f == null || !Licence.Premium || appuye == comparaison) return;
            var r = Reglages;
            if (appuye && !r.Actif) { f.Statut(Langue.T("L'égaliseur est coupé : rien à comparer.")); return; }
            if (f.Simulation) { if (appuye) f.Statut(Langue.T("Simulation : le fichier d'Equalizer APO serait réécrit, rien n'est modifié."), true); return; }
            comparaison = appuye;
            var copie = r.Copie();
            Task<string> etape = fileComparaison.ContinueWith(_ =>
            {
                if (copie.Moteur == "vm") return appuye ? Voicemeeter.Couper(copie.BusVm, false) : Voicemeeter.Appliquer(copie, copie.BusVm, false);
                // « sans » au même volume que « avec » : on n'entend que le travail de l'égaliseur (comparaison honnête)
                return AudioJeu.Comparer(appuye, AudioJeu.Generer(copie, copie.CibleEcrite), AudioJeu.VolumeSeul(copie, copie.CibleEcrite, DemoAudio.EcartNiveau(copie)));
            });
            fileComparaison = etape.ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                if (err != "") { f.Statut(Langue.T(err), true); return; }
                f.Statut(Langue.T(appuye ? "Comparaison : égaliseur coupé (au même volume) tant que tu maintiens le bouton." : "Comparaison terminée : l'égaliseur est de nouveau actif."));
            }));
        }

        private static bool contourne;

        /// <summary>
        /// Duel à l'oreille : l'égaliseur en place est entièrement coupé pendant l'écoute (les candidats sont déjà passés
        /// dans leurs filtres, calibration comprise), puis remis. Même file d'attente que « Comparer ».
        /// </summary>
        internal static void Contourner(FenetrePrincipale f, bool couper)
        {
            if (Exemple != null || f == null || couper == contourne) return;
            var copie = Reglages.Copie();
            if (couper && !copie.Actif) return;
            contourne = couper;
            Task<string> etape = fileComparaison.ContinueWith(_ =>
            {
                if (copie.Moteur == "vm") return couper ? Voicemeeter.Couper(copie.BusVm, false) : Voicemeeter.Appliquer(copie, copie.BusVm, false);
                return AudioJeu.Comparer(couper, AudioJeu.Generer(copie, copie.CibleEcrite));
            });
            fileComparaison = etape.ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                if (err != "") f.Statut(Langue.T(err), true);
            }));
        }

        // ---- confirmation visible d'un changement de profil

        /// <summary>Confirmation à afficher dès que le prochain changement est écrit : titre, détail, actif ou non.</summary>
        private static Tuple<string, string, bool> avisApres;
        private static Control toast;
        private static DispatcherTimer minuterieToast;

        /// <summary>Le prochain changement écrit affichera cette confirmation (en simulation : tout de suite, rien n'étant écrit).</summary>
        private void AnnoncerApres(string titre, string detail)
        {
            if (exemple) return;
            if (simulation) { Confirmer(F, titre, detail + " · " + Langue.T("simulation : rien n'est écrit"), false); return; }
            avisApres = Tuple.Create(titre, detail, r.Actif || r.AutoJeu);
        }

        /// <summary>
        /// « ✔ Profil appliqué » : bandeau en haut de la fenêtre pendant 2,5 s ; si Velox n'est pas au premier plan (en
        /// jeu, Auto jeux), la petite bulle à l'écran le dit aussi.
        /// </summary>
        /// <summary>(captures) Retire tout de suite la confirmation affichée.</summary>
        internal static void RetirerConfirmation(FenetrePrincipale f)
        {
            if (minuterieToast != null) minuterieToast.Stop();
            if (toast != null && f != null) f.RetirerCalque(toast);
            toast = null;
        }

        internal static void Confirmer(FenetrePrincipale f, string titre, string detail, bool actif)
        {
            if (f == null) return;
            try
            {
                if (!f.IsActive || f.WindowState == WindowState.Minimized) BulleAudio.Montrer(titre.ToUpper(Langue.Culture), detail, actif);
                if (toast != null) f.RetirerCalque(toast);
                var coche = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = Studio.P(actif ? Studio.Vert : Studio.Ambre, 220), VerticalAlignment = VerticalAlignment.Center,
                    Child = new TextBlock { Text = actif ? "✔" : "•", FontSize = 14, FontWeight = FontWeight.Bold, Foreground = Studio.P(Studio.Fond1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
                var textes = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                textes.Children.Add(new TextBlock { Text = titre, FontFamily = Studio.Titre, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Studio.P(Studio.Texte) });
                if (!string.IsNullOrEmpty(detail)) textes.Children.Add(new TextBlock { Text = detail, FontFamily = Studio.Corps, FontSize = 12, Foreground = Studio.P(Studio.Ambre) });
                var cadre = new Border
                {
                    Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Children = { coche, textes } },
                    Padding = new Thickness(16, 10, 20, 10), CornerRadius = new CornerRadius(16), Background = Studio.P(Color.FromRgb(18, 10, 32), 238),
                    BorderBrush = Studio.Degrade(0, Studio.Ambre, Studio.Magenta), BorderThickness = new Thickness(1.5),
                    BoxShadow = BoxShadows.Parse("0 6 26 0 " + Habillage.Alpha(Studio.Magenta, 90)),
                    HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 70, 0, 0),
                    IsHitTestVisible = false, Opacity = 0,
                    Transitions = new Avalonia.Animation.Transitions { new Avalonia.Animation.DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromMilliseconds(180) } },
                };
                Avalonia.Automation.AutomationProperties.SetLiveSetting(cadre, Avalonia.Automation.AutomationLiveSetting.Polite);
                Avalonia.Automation.AutomationProperties.SetName(cadre, titre + (string.IsNullOrEmpty(detail) ? "" : ". " + detail));
                toast = cadre;
                f.AjouterCalque(cadre);
                Dispatcher.UIThread.Post(() => cadre.Opacity = 1, DispatcherPriority.Background);
                if (minuterieToast != null) minuterieToast.Stop();
                minuterieToast = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2500) };
                minuterieToast.Tick += (o, e) =>
                {
                    minuterieToast.Stop();
                    cadre.Opacity = 0;
                    var fin = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(260) };
                    fin.Tick += (o2, e2) => { fin.Stop(); f.RetirerCalque(cadre); if (toast == cadre) toast = null; };
                    fin.Start();
                };
                minuterieToast.Start();
            }
            catch (Exception ex) { JournalDemarrage.Erreur("confirmation de l'audio", ex); }
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
        private readonly DispatcherTimer delai = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        private bool attente, attenteToujours, enDirect;
        private string attenteMessage;

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

        // ---- table de mixage de Voicemeeter (entrées et bus)
        private sealed class Tranche
        {
            public string Prefixe = "";   // « Strip[2] » ou « Bus[0] »
            public int Index;
            public bool EstBus, Muet;
            public Fader5 Fader;
            public TextBlock TGain;
            public Border BMute;
            public VuMetre5 Vu;
            public readonly List<Tuple<string, Border>> Envois = new List<Tuple<string, Border>>();
            public bool[] EtatEnvois = new bool[0];
        }
        private StackPanel zoneMix, zoneCasque, zoneSurround;
        private TextBlock tDansLeJeu, tPasDirect;
        private StackPanel zoneDemarrage, zoneVerif, zoneNom;
        private string signatureNom;
        private bool nomOccupe;
        private readonly List<Control> modulesExpert = new List<Control>();
        private Border bSimple, bExpert;

        private Control Expert(Control c) { modulesExpert.Add(c); return c; }

        /// <summary>Mode Simple : seulement l'essentiel (démarrage rapide, vérification, courbe, écoute, profils, Pas boostés,
        /// calibration, automatismes). Mode Expert : tout. Rien n'est changé dans le son, seulement l'affichage.</summary>
        private void AppliquerMode()
        {
            foreach (var c in modulesExpert) c.IsVisible = r.ModeExpert;
            if (bSimple != null) { Marquer(bSimple, !r.ModeExpert, Studio.Cyan); Marquer(bExpert, r.ModeExpert, Studio.Magenta); }
        }

        private void ChoisirMode(bool expert)
        {
            if (r.ModeExpert == expert) return;
            r.ModeExpert = expert;
            AppliquerMode();
            if (!exemple && !simulation) { try { AudioJeu.Enregistrer(Reglages); } catch { } }
            F.Statut(Langue.T(expert ? "Mode Expert : tous les réglages sont affichés." : "Mode Simple : l'essentiel seulement (le son ne change pas)."));
        }
        private string signatureVerif;
        private int testVolume;   // 0 : rien ; 1 : volume baissé en cours ; 2 : fini, en attente de la réponse
        private Control moduleDemarrage;
        private string signatureDemarrage;
        private List<CasqueAutoEq> casquesDevines;
        private string sortieDevinee;
        private bool devinette, casqueRefuse, configEnCours;
        private StackPanel zonePas;
        private string signaturePas;
        private bool pasOccupe;
        private string signatureSurround;
        private bool tousModeles;
        private readonly List<Tuple<JeuAudio, Border>> puceJeux = new List<Tuple<JeuAudio, Border>>();
        private string signatureCasque;
        private bool rechercheCasque;
        private TextBox saisieCasque;
        private StackPanel resultatsCasque;
        private TextBlock tCasque;
        private DispatcherTimer delaiCasque;
        private string signatureMix;
        private readonly List<Tranche> tranches = new List<Tranche>();
        private static readonly Dictionary<string, float> mixEnAttente = new Dictionary<string, float>();
        private static DateTime mixLocal = DateTime.MinValue;
        private DispatcherTimer delaiMix;
        private string signatureSorties, signatureVm, signatureAuto, signatureTemoins, signatureAvis;
        private ProfilAudio profilAffiche;
        private bool cartesFaites;
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
                ArreterDemo();
                ArreterDuel(false);
                if (comparaison) Comparer(F, false);   // bouton « Comparer » encore maintenu : l'égaliseur revient
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
            // Minuterie propre à la page (et non l'horloge des animations) : le direct continue quand Velox n'est pas au
            // premier plan (Voicemeeter ou le jeu dans une autre fenêtre), plus lentement, et s'arrête si Velox est réduit.
            if (oui)
            {
                EcouteDirecte.Demarrer();
                if (minuterieDirect == null)
                {
                    minuterieDirect = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
                    minuterieDirect.Tick += (s, a) => ImageDirect();
                }
                minuterieDirect.Start();
            }
            else
            {
                EcouteDirecte.Arreter();
                if (minuterieDirect != null) minuterieDirect.Stop();
            }
        }

        private DispatcherTimer minuterieDirect;
        private int imagesFond;
        private EtatVoicemeeter dernierEtatVm;
        private long derniereMajVm;

        /// <summary>Une image du direct : spectre, VU-mètres, niveaux de Voicemeeter, et la table suit Voicemeeter dès
        /// qu'un nouvel état arrive. 30 images par seconde au premier plan, 10 derrière, aucune si Velox est réduit.</summary>
        private void ImageDirect()
        {
            if (F.WindowState == WindowState.Minimized) return;
            if (!F.IsActive && ++imagesFond % 3 != 0) return;
            if (courbe != null) courbe.DefinirSpectre(EcouteDirecte.Spectre);
            if (vu != null) vu.Definir(EcouteDirecte.CreteG, EcouteDirecte.CreteD);
            if (tEcoute != null) { string err = EcouteDirecte.Erreur == "" ? "" : Langue.T(EcouteDirecte.Erreur); if (tEcoute.Text != err) { tEcoute.Text = err; tEcoute.IsVisible = err != ""; } }
            var e = Voicemeeter.Etat;
            if (e == null) return;
            foreach (var x in vuBus) { var b = Bus(e, x.Item1.Index); if (b != null) x.Item2.Definir(b.G, b.D); }
            foreach (var tr in tranches)
            {
                if (tr.EstBus) { var b = Bus(e, tr.Index); if (b != null) tr.Vu.Definir(b.G, b.D); }
                else { var x = Entree(e, tr.Index); if (x != null) tr.Vu.Definir(x.G, x.D); }
            }
            // nouvel état de Voicemeeter (jusqu'à 10 par seconde) : la table et le module suivent sans attendre
            long t = Horloge.Maintenant;
            if (!ReferenceEquals(e, dernierEtatVm) && t - derniereMajVm >= 90) { dernierEtatVm = e; derniereMajVm = t; MajVoicemeeter(); }
        }

        private static BusVoicemeeter Bus(EtatVoicemeeter e, int i) { foreach (var b in e.Bus) if (b.Index == i) return b; return null; }
        private static EntreeVoicemeeter Entree(EtatVoicemeeter e, int i) { foreach (var x in e.Entrees) if (x.Index == i) return x; return null; }

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
            s.Children.Add(Studio.T(Langue.T("Égaliseur de pas pour tes jeux de tir (Warzone, Valorant, CS2, Fortnite, Apex…), appliqué par Equalizer APO ou Voicemeeter. Le jeu n'est jamais touché, et chaque changement s'annule dans l'Historique."), 13, Studio.Discret));
            temoins = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            s.Children.Add(temoins);
            g.Children.Add(s);
            if (avecMarche)
            {
                marche = new Marche5(r.Actif);
                ToolTip.SetTip(marche, Langue.T("Activer ou couper l'égaliseur"));
                AutomationProperties.SetName(marche, Langue.T("Égaliseur"));
                marche.Demande += BasculerEq;
                tMarche = new TextBlock { FontFamily = Studio.Chiffres, FontSize = 11, LetterSpacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
                // « Comparer » : maintenu, l'égaliseur se coupe ; relâché, il revient (pour juger un réglage à l'oreille)
                var comparer = new Border
                {
                    Child = new TextBlock { Text = Langue.T("COMPARER"), FontFamily = Studio.Chiffres, FontSize = 10.5, LetterSpacing = 1.5, Foreground = Studio.P(Studio.Texte), HorizontalAlignment = HorizontalAlignment.Center },
                    Padding = new Thickness(10, 6), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1), BorderBrush = Studio.P(Studio.Violet, 160), Background = Studio.P(Studio.Violet, 22),
                    Cursor = new Cursor(StandardCursorType.Hand), Focusable = true, Margin = new Thickness(0, 4, 0, 0),
                };
                ToolTip.SetTip(comparer, Langue.T("Maintiens ce bouton (ou la barre d'espace) pour entendre ton jeu sans l'égaliseur ; relâche pour le retrouver."));
                AutomationProperties.SetName(comparer, Langue.T("Comparer sans l'égaliseur"));
                comparer.PointerPressed += (o, e) => { e.Pointer.Capture(comparer); comparer.Background = Studio.P(Studio.Magenta, 60); Comparer(F, true); e.Handled = true; };
                comparer.PointerReleased += (o, e) => { e.Pointer.Capture(null); comparer.Background = Studio.P(Studio.Violet, 22); Comparer(F, false); };
                comparer.PointerCaptureLost += (o, e) => { comparer.Background = Studio.P(Studio.Violet, 22); Comparer(F, false); };
                comparer.KeyDown += (o, e) => { if (e.Key == Key.Space || e.Key == Key.Enter) { e.Handled = true; Comparer(F, true); } };
                comparer.KeyUp += (o, e) => { if (e.Key == Key.Space || e.Key == Key.Enter) { e.Handled = true; Comparer(F, false); } };
                comparer.LostFocus += (o, e) => Comparer(F, false);
                var droiteEq = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(18, 4, 0, 0), Children = { marche, tMarche, comparer } };
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
            zoneDemarrage = new StackPanel { Spacing = 10 };
            moduleDemarrage = Studio.Module("Démarrage rapide", zoneDemarrage);
            g.Children.Add(moduleDemarrage);
            courbe = new CourbeEq5 { Reglages = r, Allume = r.Actif };
            AutomationProperties.SetName(courbe, Langue.T("Courbe de l'égaliseur"));
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
            zoneVerif = new StackPanel { Spacing = 8 };
            g.Children.Add(Studio.Module("Vérification du son", zoneVerif));
            g.Children.Add(Studio.Module("Avant / après", ConstruireDemo()));

            // profils
            var grille = new UniformGrid { Columns = 4 };
            foreach (var p in AudioJeu.Profils.Take(4)) grille.Children.Add(CarteProfil(p));
            grille.SizeChanged += (o, a) => { int col = a.NewSize.Width >= 600 ? 4 : 2; if (grille.Columns != col) grille.Columns = col; };
            var autres = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
            foreach (var p in AudioJeu.Profils.Skip(4)) { var pp = p; autres.Children.Add(Studio.Bouton(Langue.T(p.Nom), false, () => ChoisirProfil(pp), Langue.T(p.Texte))); }
            autres.Children.Add(Studio.Bouton(Langue.T("Réglage automatique"), false, ReglageAutomatique, Langue.T("Profil compétitif, force 100 %, sortie Auto, tout le reste remis à zéro.")));
            autres.Children.Add(Studio.Bouton(Langue.T("Démarrage rapide"), false, () => { if (exemple) return; r.AssistantFait = false; casqueRefuse = false; signatureDemarrage = null; MajDemarrage(Releve ?? new ReleveAudioJeu()); Changer(null, false, false); }, Langue.T("Rouvre le démarrage rapide : Velox vérifie et règle tout pour toi.")));
            // profils par jeu (les mêmes que charge Auto jeux)
            var parJeu = new WrapPanel { Margin = new Thickness(0, 4, 0, 0) };
            parJeu.Children.Add(new TextBlock { Text = Langue.T("Par jeu :"), FontFamily = Studio.Corps, FontSize = 12.5, Foreground = Studio.P(Studio.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) });
            foreach (var j in AudioJeu.Jeux)
            {
                var jj = j;
                var b = Puce(j.Nom, () => ChoisirJeu(jj));
                ToolTip.SetTip(b, Langue.T(j.Texte));
                Marquer(b, false, Studio.Cyan);
                puceJeux.Add(Tuple.Create(j, b));
                parJeu.Children.Add(b);
            }
            autres = new WrapPanel { Children = { autres, parJeu } };
            tProfil = Studio.T("", 12.5, Studio.Discret);
            tDansLeJeu = Studio.T("", 12, Studio.Faible);
            tDansLeJeu.Margin = new Thickness(0, 6, 0, 0);
            g.Children.Add(Studio.Module("Profils", new StackPanel { Children = { grille, autres, tProfil, tDansLeJeu } }));
            zoneNom = new StackPanel { Spacing = 8 };
            g.Children.Add(Studio.Module("Velox Studio dans tes jeux", zoneNom));
            zonePas = new StackPanel { Spacing = 10 };
            g.Children.Add(Studio.Module("Pas boostés (bêta)", zonePas));
            zoneCasque = new StackPanel { Spacing = 10 };
            g.Children.Add(Studio.Module("Calibration du casque", zoneCasque));
            zoneSurround = new StackPanel { Spacing = 10 };
            g.Children.Add(Expert(Studio.Module("Son 7.1 virtuel", zoneSurround)));

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
            g.Children.Add(Expert(Studio.Module("Console", new StackPanel { Children = { rang, options } })));

            // faders des bandes
            var bandes = new UniformGrid { Columns = AudioJeu.Bandes.Length };
            faders = new Fader5[AudioJeu.Bandes.Length]; tFaders = new TextBlock[AudioJeu.Bandes.Length];
            for (int i = 0; i < AudioJeu.Bandes.Length; i++)
            {
                int k = i; var b = AudioJeu.Bandes[i];
                faders[i] = new Fader5(-12, 12, 0.5, r.Gains[i]) { HorizontalAlignment = HorizontalAlignment.Center };
                faders[i].Change += v => { r.Gains[k] = v; Changer(); };
                AutomationProperties.SetName(faders[i], Langue.T(b.Nom) + " " + b.Fc + " Hz");
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
            g.Children.Add(Expert(Studio.Module("Bandes", new StackPanel { Spacing = 10, Children = { bandes, tAideBandes } })));
            gauche = g;

            // ---------------- colonne de droite
            var d = new StackPanel();
            vu = new VuMetre5();
            zoneSorties = new StackPanel { Spacing = 8 };
            var sortie = new StackPanel { Spacing = 10 };
            sortie.Children.Add(Studio.T(Langue.T("Ce que joue ta sortie par défaut"), 11.5, Studio.Faible));
            sortie.Children.Add(vu);
            tEcoute = Studio.T("", 11.5, Studio.Faible); tEcoute.IsVisible = false;   // (pas d'espace vide sous le VU-mètre)
            sortie.Children.Add(tEcoute);
            sortie.Children.Add(zoneSorties);
            d.Children.Add(Expert(Studio.Module("Sorties", sortie)));
            zoneVm = new StackPanel { Spacing = 8 };
            d.Children.Add(Expert(Studio.Module("Voicemeeter", zoneVm)));
            zoneAuto = new StackPanel { Spacing = 12 };
            d.Children.Add(Studio.Module("Automatismes", zoneAuto));
            zoneFichier = new StackPanel { Spacing = 8 };
            tChemin = new TextBlock { FontFamily = Studio.Chiffres, FontSize = 11, Foreground = Studio.P(Studio.Faible), TextWrapping = TextWrapping.Wrap };
            tFichier = new TextBlock { FontFamily = Studio.Chiffres, FontSize = 11, Foreground = Studio.P(Studio.Discret), TextWrapping = TextWrapping.NoWrap };
            var defile = new ScrollViewer { Content = tFichier, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, IsVisible = false };
            var voir = Studio.Bouton(Langue.T("Voir le fichier écrit"), false, () => { defile.IsVisible = !defile.IsVisible; });
            zoneFichier.Children.Add(tChemin); zoneFichier.Children.Add(new WrapPanel { Children = { voir } }); zoneFichier.Children.Add(defile);
            d.Children.Add(Expert(Studio.Module("Fichier d'Equalizer APO", zoneFichier)));
            droite = d;

            // mode Simple (l'essentiel) ou Expert (tous les réglages)
            var choixMode = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            choixMode.Children.Add(new TextBlock { Text = Langue.T("Affichage :"), FontFamily = Studio.Corps, FontSize = 12.5, Foreground = Studio.P(Studio.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) });
            bSimple = Puce(Langue.T("Simple"), () => ChoisirMode(false));
            ToolTip.SetTip(bSimple, Langue.T("L'essentiel : profils, calibration, Pas boostés, vérification et écoute."));
            bExpert = Puce(Langue.T("Expert"), () => ChoisirMode(true));
            ToolTip.SetTip(bExpert, Langue.T("Tous les réglages : console, bandes, sorties, Voicemeeter, 7.1 virtuel, table de mixage, fichier."));
            choixMode.Children.Add(bSimple); choixMode.Children.Add(bExpert);
            pile.Children.Add(choixMode);
            corps = new Grid();
            corps.Children.Add(gauche); corps.Children.Add(droite);
            Disposer(1000);
            corps.SizeChanged += (o, a) => Disposer(a.NewSize.Width);
            pile.Children.Add(corps);
            zoneMix = new StackPanel { Spacing = 10 };
            pile.Children.Add(Expert(Studio.Module("Table de mixage", zoneMix)));
            Construire(zoneAuto);
            AppliquerMode();
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
            AutomationProperties.SetName(m, Langue.T(nom));
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
            string sig = r.AutoJeu + "|" + r.ProfilParJeu + "|" + r.Raccourcis + "|" + RaccourcisAudio.Erreur;
            if (sig == signatureAuto) return;
            signatureAuto = sig;
            z.Children.Clear();
            z.Children.Add(Ligne("Auto jeux", "L'égaliseur s'active quand un jeu reconnu tourne (Warzone, Valorant, CS2, Fortnite, Apex, PUBG, Rainbow Six, Tarkov, Overwatch, Battlefield) et se coupe quand tu le fermes. Avec le gardien activé, ça marche même Velox fermé ; sinon, tant que Velox est ouvert (même réduit).", r.AutoJeu, BasculerAuto));
            z.Children.Add(Ligne("Profil adapté à chaque jeu", "Au lancement d'un jeu, Velox charge le profil prévu pour lui (là où tombent ses pas). Tu peux l'ajuster pendant la partie : ton réglage n'est pas écrasé.", r.ProfilParJeu, BasculerProfilJeu));
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
                "Des profils pensés pour chaque jeu de tir (Warzone, Valorant, CS2, Fortnite, Apex…) : les pas et les recharges passent devant, les explosions reculent.",
                "Calibration de ton casque : plus de 6 000 modèles mesurés en laboratoire, pour un son neutre de studio.",
                "Courbe de l'égaliseur et spectre de ton son en direct, molettes et faders pour tout régler à l'oreille.",
                "Auto jeux : l'égaliseur s'allume avec le jeu, avec son profil, et s'éteint avec lui. Raccourcis clavier en plein jeu.",
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
            // ---- témoins (refaits seulement s'ils changent)
            string t1 = moteurVm ? vm.Nom.ToUpperInvariant() : "EQUALIZER APO", t2 = v.Jeu != null ? Langue.F("JEU : {0}", (AudioJeu.JeuDe(v.Jeu)?.Nom ?? v.Jeu).ToUpperInvariant()) : Langue.T("JEU NON LANCÉ"),
                t3 = vm.Lance ? vm.Nom.ToUpperInvariant() : vm.Installe ? Langue.T("VOICEMEETER ARRÊTÉ") : Langue.T("SANS VOICEMEETER");
            bool a1 = moteurVm ? vm.EgaliseurNatif : v.Etat.Present;
            string sigTemoins = t1 + a1 + "|" + t2 + "|" + t3 + vm.Lance + "|" + r.AutoJeu;
            if (sigTemoins != signatureTemoins)
            {
                signatureTemoins = sigTemoins;
                temoins.Children.Clear();
                var tApo = Studio.Temoin(t1, Studio.Vert, a1);
                if (!moteurVm)
                {
                    // un clic sur « EQUALIZER APO » : absent, Velox l'installe ; présent, sa fenêtre s'ouvre (choix des sorties)
                    tApo.Cursor = new Cursor(StandardCursorType.Hand);
                    ToolTip.SetTip(tApo, Langue.T(a1 ? "Ouvrir la fenêtre d'Equalizer APO (choix des sorties)." : "Installer Equalizer APO : Velox le télécharge et l'installe pour toi."));
                    AutomationProperties.SetName(tApo, Langue.T(a1 ? "Ouvrir la fenêtre d'Equalizer APO" : "Installer Equalizer APO"));
                    tApo.PointerPressed += (o, e) => { e.Handled = true; ApoClique(Releve); };
                }
                temoins.Children.Add(tApo);
                temoins.Children.Add(Studio.Temoin(t2, Studio.Ambre, v.Jeu != null));
                temoins.Children.Add(Studio.Temoin(t3, Studio.Cyan, vm.Lance));
                if (r.AutoJeu) temoins.Children.Add(Studio.Temoin(Langue.T("AUTO JEUX"), Studio.Magenta, true));
            }
            marche.Actif = r.Actif;
            tMarche.Text = Langue.T(r.Actif ? "ACTIF" : "COUPÉ");
            tMarche.Foreground = Studio.P(r.Actif ? Studio.Ambre : Studio.Faible);

            // ---- avis (préparés, puis refaits seulement s'ils changent : pas de clignotement ni de mise en page à chaque relevé)
            var messages = new List<Tuple<string, Color>>();
            int boutonsApo = -1;
            Action<string, Color> avertir = (t, c) => messages.Add(Tuple.Create(t, c));
            if (simulation) avertir(Langue.T("Mode simulation : tes essais ici ne sont pas écrits, le fichier d'Equalizer APO ne change pas."), Studio.Ambre);
            if (!moteurVm && !v.Etat.Present)
            {
                avertir(Langue.T("Equalizer APO n'est pas installé sur ce PC. Velox l'installe pour toi : gratuit, depuis son site officiel, vérifié avant d'être lancé. Tant qu'il n'est pas là, Velox ne modifie rien."), Studio.Rouge);
                boutonsApo = messages.Count;
            }
            if (!moteurVm && v.Etat.Present)
            {
                int etat; Cible(v, out etat);
                if (etat == 3) avertir(Langue.T("Equalizer APO n'est installé sur aucune sortie : ouvre le Configurator, coche ta sortie, puis redémarre le PC."), Studio.Rouge);
                else if (etat == 2) avertir(Langue.T("La sortie choisie n'a plus Equalizer APO : choisis-en une autre, ou coche-la de nouveau dans le Configurator."), Studio.Ambre);
                else if (etat == 4) avertir(Langue.F("Ta sortie par défaut est Voicemeeter : l'égaliseur vise l'appareil de son bus A ({0}).", r.AppareilVm), Studio.Violet);
                else if (etat == 1) avertir(Langue.F("Ta sortie par défaut ({0}) n'a pas Equalizer APO : l'égaliseur vise toutes les sorties où il est installé. Si tu n'entends aucune différence, ouvre le Configurator, coche ta sortie, puis redémarre le PC.", v.NomDefaut != "" ? v.NomDefaut : Langue.T("inconnue")), Studio.Ambre);
                if (v.Etat.Autres.Count > 0) avertir(Langue.F("D'autres traitements sont actifs dans Equalizer APO ({0}) : leurs effets s'ajoutent à cet égaliseur.", string.Join(", ", v.Etat.Autres)), Studio.Violet);
                var vrais = exemple ? r : Reglages;
                if (v.Etat.Actif && !v.Etat.Include) avertir(Langue.T("Le fichier config.txt d'Equalizer APO n'inclut pas cet égaliseur : il n'a aucun effet. Coupe puis réactive l'interrupteur pour remettre la ligne."), Studio.Ambre);
                else if (v.Etat.Actif && !enEcriture && !attente && v.Etat.Contenu != AudioJeu.Generer(vrais, vrais.CibleEcrite))
                    avertir(Langue.T("Le fichier en place ne vient pas de ces réglages (autre outil, ou modifié à la main) : ton prochain réglage ici le remplacera."), Studio.Violet);
            }
            if (r.Niveau > 0) avertir(Langue.T("Niveau au-dessus de 0 dB : le son peut saturer (grésillements). Baisse-le si tu en entends."), Studio.Ambre);
            string sigAvis = string.Join("\n", messages.Select(m => m.Item1 + m.Item2)) + "|" + boutonsApo + installationApo + v.NomDefaut;
            if (sigAvis != signatureAvis)
            {
                signatureAvis = sigAvis;
                avis.Children.Clear();
                for (int i = 0; i < messages.Count; i++)
                {
                    var m = messages[i];
                    avis.Children.Add(new Border { Padding = new Thickness(14, 9), CornerRadius = new CornerRadius(12), Background = Studio.P(m.Item2, 22), BorderBrush = Studio.P(m.Item2, 120), BorderThickness = new Thickness(1), Child = Studio.T(m.Item1, 12.5, Studio.Texte) });
                    if (i + 1 == boutonsApo)
                    {
                        string defaut = v.NomDefaut;
                        var bInstaller = Studio.Bouton(Langue.T(installationApo ? "Installation en cours…" : "Installer Equalizer APO pour moi"), true, () => InstallerApo(defaut));
                        avis.Children.Add(new WrapPanel { Children = { bInstaller, Studio.Bouton(Langue.T("Vérifier à nouveau"), false, () => { if (!exemple) { F.Statut(Langue.T("Recherche d'Equalizer APO…")); Relever(); } }), Studio.Bouton(Langue.T("Ouvrir la page d'Equalizer APO"), false, () => { if (!exemple) Programmes.Ouvrir(AudioJeu.Site); }) } });
                    }
                }
            }

            // ---- courbe, profils, molettes, bandes
            courbe.Reglages = r; courbe.Allume = r.Actif; courbe.InvalidateVisual();   // (la courbe se recalcule seule si les réglages ont changé)
            var courant = AudioJeu.ProfilDe(r.Gains);
            if (courant != profilAffiche || !cartesFaites)
            foreach (var c in cartes)
            {
                bool choisi = courant == c.Item1;
                c.Item2.Background = choisi ? (IBrush)Studio.Degrade(1, Habillage.Alpha(Studio.Magenta, 46), Habillage.Alpha(Studio.Violet, 26)) : Studio.P(Studio.Fond1, 150);
                c.Item2.BorderBrush = choisi ? (IBrush)Studio.Degrade(0, Studio.Ambre, Studio.Magenta) : Studio.P(Studio.Bord);
                c.Item2.BoxShadow = choisi ? BoxShadows.Parse("0 0 18 0 " + Habillage.Alpha(Studio.Magenta, 90)) : default(BoxShadows);
            }
            profilAffiche = courant; cartesFaites = true;
            foreach (var pj in puceJeux) Marquer(pj.Item2, pj.Item1.Gains.SequenceEqual(r.Gains) && pj.Item1.CoupeBas == r.CoupeBas, Studio.Cyan);
            tProfil.Text = Langue.F("Profil en cours : {0} · force {1} %", Langue.T(AudioJeu.NomProfil(r)), r.Intensite);
            {
                var jv = AudioJeu.JeuVise(Releve?.Jeu, r);
                tDansLeJeu.Text = jv.DansLeJeu == "" ? "" : Langue.F("Dans {0} : {1}", jv.Nom, Langue.T(jv.DansLeJeu));
                tDansLeJeu.IsVisible = tDansLeJeu.Text != "";
            }
            foreach (var m in molettes) { m.Item1.Valeur = ValeurMolette(m.Item1); m.Item2.Text = m.Item3(); }
            foreach (var c in coupes) Marquer(c.Item2, c.Item1 == r.CoupeBas);
            Marquer(bInversion, r.Inversion);
            tMoteur.Text = moteurVm ? Langue.T("Égaliseur dans Voicemeeter : la largeur stéréo et l'inversion n'y existent pas, et le coupe-explosions prend la place de la bande Graves (6 cellules par bus).") : "";
            for (int i = 0; i < faders.Length; i++) { faders[i].Valeur = r.Gains[i]; tFaders[i].Text = Studio.Db(r.Gains[i]); tFaders[i].Foreground = Studio.P(r.Gains[i] > 0 ? Studio.Ambre : r.Gains[i] < 0 ? Studio.Cyan : Studio.Texte); }
            tAideBandes.Text = r.Intensite == 100 ? Langue.T("Chaque gain va de −12 à +12 dB. La force de l'effet les multiplie tous.")
                : Langue.F("Avec la force à {0} %, les gains réellement appliqués sont : {1}.", r.Intensite, string.Join("  ", AudioJeu.Bandes.Select((b, i) => Studio.Db(AudioJeu.GainEffectif(r, i)))));

            // ---- sorties, Voicemeeter, automatismes, fichier
            if (exemple) vu.Definir(0.42f, 0.36f);
            MajCasque();
            MajDemarrage(v);
            MajNom(v);
            MajVerif(v);
            MajPas(v);
            MajSurround(v);
            MajSorties(v);
            MajVoicemeeter();
            Construire(zoneAuto);
            int e2; string guid = Cible(v, out e2);
            tChemin.Text = moteurVm ? Langue.T("L'égaliseur passe par Voicemeeter : le fichier d'Equalizer APO est coupé.") : Path.Combine(v.Dossier, AudioJeu.NomFichier);
            tFichier.Text = AudioJeu.Generer(r, guid).TrimEnd();
        }

        private string Cible(ReleveAudioJeu v, out int etat) { return AudioJeu.Resoudre(r.Cible, v.Sorties, v.Defaut, out etat, v.NomDefaut, r.AppareilVm); }

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
                if (!aApo && r.Moteur != "vm" && !AudioJeu.EstVoicemeeter(s.Nom))
                {
                    var bAj = Puce(Langue.T(v.Etat.Present ? "Ajouter Equalizer APO" : installationApo ? "Installation en cours…" : "Installer Equalizer APO"), () => ApoClique(v, ss.Nom));
                    Marquer(bAj, false, Studio.Vert);
                    ToolTip.SetTip(bAj, Langue.T(v.Etat.Present ? "Ouvre la fenêtre d'Equalizer APO pour l'ajouter à cette sortie." : "Velox télécharge Equalizer APO depuis son site officiel, le vérifie et l'installe."));
                    bas.Children.Add(bAj);
                }
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
            MajMixage();
            var rel = Releve;
            var premiereVirt = e.Entrees.FirstOrDefault(x => x.Virtuelle);
            var vmEntree = rel == null || premiereVirt == null ? null : rel.Windows.FirstOrDefault(w => Voicemeeter.EntreeVirtuelle(w.Nom, e.Type) == premiereVirt.Index);
            bool vmEntreeApo = vmEntree != null && rel.Sorties.Any(x => x.Guid == vmEntree.Guid);
            string sig = e.Installe + "|" + e.Lance + "|" + e.Type + "|" + e.Version + "|" + string.Join(",", e.Bus.Select(b => b.Nom + b.Label + b.Appareil)) + "|" + r.Moteur + r.BusVm
                + "|" + (vmEntree != null ? vmEntree.Guid : "") + vmEntreeApo + "|" + r.Cible + "|" + r.SansEtiquetteVm;
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
            // appareil du bus A gardé : la veille et le gardien visent la bonne sortie quand Voicemeeter est la sortie par défaut
            if (!exemple && a != null && a.Appareil != "" && a.Appareil != Reglages.AppareilVm)
            {
                Reglages.AppareilVm = a.Appareil; r.AppareilVm = a.Appareil;
                if (!simulation) AudioJeu.Enregistrer(Reglages);
                signatureSorties = null;
            }
            zoneVm.Children.Add(Studio.T(Langue.F("Avec Voicemeeter, le son du jeu passe d'abord par « Voicemeeter Input », puis sort sur l'appareil du bus {0}{1}. Equalizer APO agit sur la sortie où il est installé : vise celle où tu entends l'effet.", a != null ? a.Nom : "A", a != null && a.Appareil != "" ? " (" + a.Appareil + ")" : ""), 12, Studio.Faible));

            // égaliser AVANT le mixage : Equalizer APO installé sur « Voicemeeter Input »
            if (vmEntree != null && r.Moteur != "vm")
            {
                var avant = new WrapPanel();
                if (vmEntreeApo)
                {
                    string g = vmEntree.Guid;
                    bool vise = AudioJeu.NormaliserGuid(r.Cible) == AudioJeu.NormaliserGuid(g);
                    var bAvant = Puce(Langue.T("Égaliser avant le mixage (Voicemeeter Input)"), () => { if (exemple) return; r.Cible = vise ? "auto" : g; Changer(Langue.T(vise ? "Sortie visée : automatique." : "L'égaliseur agit maintenant sur Voicemeeter Input, avant le mixage."), false, true); });
                    Marquer(bAvant, vise, Studio.Cyan);
                    ToolTip.SetTip(bAvant, Langue.T("Tout ce qui entre dans Voicemeeter par cette entrée est égalisé, avant tes réglages de mixage. Reclique pour revenir au choix automatique."));
                    avant.Children.Add(bAvant);
                    zoneVm.Children.Add(avant);
                }
                else
                {
                    zoneVm.Children.Add(Studio.T(Langue.T("Pour égaliser avant le mixage, coche « Voicemeeter Input » dans le Configurator d'Equalizer APO, puis redémarre le PC : Velox pourra alors viser cette entrée."), 12, Studio.Faible));
                    avant.Children.Add(Studio.Bouton(Langue.T("Ouvrir le Configurator"), false, OuvrirConfigurator));
                    zoneVm.Children.Add(avant);
                }
            }

            // nom « Velox Studio » dans Voicemeeter
            var bNom = Puce(Langue.T("Nom « Velox Studio » dans Voicemeeter"), () => { if (exemple) return; r.SansEtiquetteVm = !r.SansEtiquetteVm; Changer(Langue.T(r.SansEtiquetteVm ? "Velox ne met plus son nom dans Voicemeeter." : "Voicemeeter affiche « Velox Studio » sur ce que l'égaliseur traite."), false, true); });
            Marquer(bNom, !r.SansEtiquetteVm, Studio.Magenta);
            ToolTip.SetTip(bNom, Langue.T("Le bus ou l'entrée que l'égaliseur de Velox traite s'appelle « Velox Studio » dans Voicemeeter (seulement s'il n'a pas déjà un nom). Le nom est retiré quand l'égaliseur est coupé."));
            zoneVm.Children.Add(new WrapPanel { Children = { bNom } });
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

        // ---------------------------------------------------------------- test avant / après à l'oreille

        private TextBlock tDemo, tDemoDetail;
        private Border bDemo, carteSans, carteAvec, bDuel;
        private StackPanel zoneDuel;
        // duel à l'oreille : candidats (nom, réglages), sons rendus, champion, challenger, ordre d'écoute, étape
        private List<Tuple<string, ReglagesAudioJeu>> duelCandidats;
        private List<byte[]> duelSons;
        private int duelChampion, duelChallenger, duelEtape, duelNumero;
        private bool duelInverse;
        private DispatcherTimer minuterieDuel;
        private static readonly Random hasardDuel = new Random();
        private DispatcherTimer minuterieDemo;
        private int etapeDemo;

        private Control ConstruireDemo()
        {
            var pile = new StackPanel { Spacing = 12 };
            pile.Children.Add(Studio.T(Langue.T("Une scène de jeu de six secondes (des pas qui passent de gauche à droite, une explosion qui les couvre, des tirs, une recharge), jouée sans le Studio son puis avec : écoute la différence au casque."), 12.5, Studio.Discret));
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,12,*") };
            carteSans = CarteDemo(Langue.T("SANS"), Langue.T("le son d'origine"), Studio.Faible);
            carteAvec = CarteDemo(Langue.T("AVEC"), Langue.T("le Studio son"), Studio.Magenta);
            Grid.SetColumn(carteAvec, 2);
            g.Children.Add(carteSans); g.Children.Add(carteAvec);
            pile.Children.Add(g);
            bDemo = Studio.Bouton(Langue.T("▶  Écouter la différence"), true, LancerDemo, Langue.T("Joue la scène deux fois : sans, puis avec le Studio son (calibration du casque comprise)."));
            bDuel = Studio.Bouton(Langue.T("Duel à l'oreille"), false, LancerDuel, Langue.T("Choisis ton profil à l'aveugle : la même scène passée par plusieurs profils, au même volume, deux par deux. Tu gardes celui où tu entends le mieux les pas."));
            pile.Children.Add(new WrapPanel { Children = { bDemo, bDuel } });
            zoneDuel = new StackPanel { Spacing = 10, IsVisible = false };
            pile.Children.Add(zoneDuel);
            tDemoDetail = Studio.T(Langue.T("Monte un peu le volume. La scène passe par ta sortie par défaut de Windows."), 11.5, Studio.Faible);
            pile.Children.Add(tDemoDetail);
            MarquerDemo(0);
            return pile;
        }

        private Border CarteDemo(string titre, string sous, Color c)
        {
            var s = new StackPanel { Spacing = 2, HorizontalAlignment = HorizontalAlignment.Center };
            s.Children.Add(new TextBlock { Text = titre, FontFamily = Studio.Titre, FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Studio.P(c), HorizontalAlignment = HorizontalAlignment.Center });
            s.Children.Add(new TextBlock { Text = sous, FontFamily = Studio.Corps, FontSize = 12, Foreground = Studio.P(Studio.Discret), HorizontalAlignment = HorizontalAlignment.Center });
            return new Border { Child = s, Padding = new Thickness(12, 14), CornerRadius = new CornerRadius(14), BorderThickness = new Thickness(1.5) };
        }

        /// <summary>0 : au repos ; 1 : « sans » en cours ; 2 : « avec » en cours.</summary>
        private void MarquerDemo(int phase)
        {
            if (carteSans == null) return;
            carteSans.Background = phase == 1 ? Studio.P(Studio.Texte, 22) : Studio.P(Studio.Fond1, 150);
            carteSans.BorderBrush = phase == 1 ? Studio.P(Studio.Texte, 200) : Studio.P(Studio.Bord);
            carteAvec.Background = phase == 2 ? Studio.Degrade(1, Habillage.Alpha(Studio.Magenta, 60), Habillage.Alpha(Studio.Violet, 30)) : Studio.P(Studio.Fond1, 150);
            carteAvec.BorderBrush = phase == 2 ? Studio.Degrade(0, Studio.Ambre, Studio.Magenta) : Studio.P(Studio.Bord);
            carteAvec.BoxShadow = phase == 2 ? BoxShadows.Parse("0 0 22 0 " + Habillage.Alpha(Studio.Magenta, 110)) : default(BoxShadows);
            if (bDemo != null && bDemo.Child is TextBlock tb) tb.Text = Langue.T(phase == 0 ? "▶  Écouter la différence" : "■  Arrêter");
        }

        /// <summary>
        /// Sans (égaliseur contourné comme avec « Comparer ») puis avec : chaque fois, un court instant pour qu'Equalizer
        /// APO relise son fichier, puis la scène. Arrêt à tout moment ; l'égaliseur revient toujours à la fin.
        /// </summary>
        private void LancerDemo()
        {
            if (exemple) return;
            if (etapeDemo != 0) { ArreterDemo(); F.Statut(Langue.T("Écoute arrêtée.")); return; }
            ArreterDuel(false);
            if (!OperatingSystem.IsWindows()) return;
            if (simulation) { F.Statut(Langue.T("Simulation : l'écoute avant / après a besoin de couper l'égaliseur un instant, rien n'est modifié ici."), true); return; }
            if (!r.Actif) { LancerDemoPreparee(); return; }
            var v = Releve;
            if (v != null && r.Moteur != "vm")
            {
                int etat; string cible = Cible(v, out etat);
                if (cible != "" && AudioJeu.NormaliserGuid(cible) != AudioJeu.NormaliserGuid(v.Defaut))
                    F.Statut(Langue.T("L'égaliseur vise une autre sortie que celle de Windows : la scène s'entend sur la sortie par défaut, sans l'effet."), true);
            }
            if (minuterieDemo == null) { minuterieDemo = new DispatcherTimer(); minuterieDemo.Tick += (o, a) => EtapeDemo(); }
            etapeDemo = 1;
            Comparer(F, true);   // sans le Studio son
            MarquerDemo(1);
            minuterieDemo.Interval = TimeSpan.FromMilliseconds(450);
            minuterieDemo.Start();
        }

        private List<byte[]> demoPreparee;

        /// <summary>
        /// Égaliseur pas actif en ce moment (Auto jeux attend un jeu, ou coupé) : Velox prépare lui-même les deux sons, sans
        /// puis avec tes réglages (calibration comprise), au même volume, et les joue. Equalizer APO n'est pas touché.
        /// </summary>
        private void LancerDemoPreparee()
        {
            var sans = r.Copie(); sans.Gains = new double[AudioJeu.Bandes.Length]; sans.CalibActive = false; sans.CoupeBas = 0; sans.Actif = true;
            var avec = r.Copie(); avec.Actif = true;
            etapeDemo = 10; MarquerDemo(1);
            F.Statut(Langue.T("Préparation de la scène…"));
            Task.Run(() => DemoAudio.Duel(new List<ReglagesAudioJeu> { sans, avec })).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (etapeDemo != 10) return;
                if (t.IsFaulted) { ArreterDemo(); return; }
                demoPreparee = t.Result;
                if (minuterieDemo == null) { minuterieDemo = new DispatcherTimer(); minuterieDemo.Tick += (o, a) => EtapeDemo(); }
                DemoAudio.Jouer(demoPreparee[0]); etapeDemo = 11;
                minuterieDemo.Interval = TimeSpan.FromSeconds(DemoAudio.Duree + 0.3); minuterieDemo.Start();
            }));
        }

        private void EtapeDemo()
        {
            if (etapeDemo >= 10)
            {
                minuterieDemo.Stop();
                if (etapeDemo == 11 && demoPreparee != null) { MarquerDemo(2); DemoAudio.Jouer(demoPreparee[1]); etapeDemo = 12; minuterieDemo.Interval = TimeSpan.FromSeconds(DemoAudio.Duree + 0.3); minuterieDemo.Start(); return; }
                ArreterDemo(); demoPreparee = null; F.Statut(Langue.T("Écoute terminée. Rejoue-la après chaque réglage pour comparer.")); return;
            }
            minuterieDemo.Stop();
            switch (etapeDemo)
            {
                case 1: DemoAudio.Jouer(); etapeDemo = 2; minuterieDemo.Interval = TimeSpan.FromSeconds(DemoAudio.Duree + 0.3); break;
                case 2: Comparer(F, false); MarquerDemo(2); etapeDemo = 3; minuterieDemo.Interval = TimeSpan.FromMilliseconds(450); break;   // avec
                case 3: DemoAudio.Jouer(); etapeDemo = 4; minuterieDemo.Interval = TimeSpan.FromSeconds(DemoAudio.Duree + 0.3); break;
                default: ArreterDemo(); F.Statut(Langue.T("Écoute terminée. Rejoue-la après chaque réglage pour comparer.")); return;
            }
            minuterieDemo.Start();
        }

        private void ArreterDemo()
        {
            if (etapeDemo == 0) return;
            if (minuterieDemo != null) minuterieDemo.Stop();
            DemoAudio.Arreter();
            if (comparaison) Comparer(F, false);   // l'égaliseur revient toujours
            etapeDemo = 0;
            MarquerDemo(0);
        }

        // ---------------------------------------------------------------- « Velox Studio » dans Windows et les jeux

        /// <summary>La sortie par laquelle Velox traite le son : le câble de « Pas boostés » s'il est allumé, sinon celle où
        /// agit l'égaliseur (la sortie visée, ou la sortie par défaut). Null si aucune.</summary>
        private SortieWindows SortieStudio(ReleveAudioJeu v)
        {
            if (v.Moteur != null && v.Moteur.Actif && v.Cables.Count > 0) return v.Windows.FirstOrDefault(w => w.Id == v.Cables[0].Sortie);
            int etat; string g = Cible(v, out etat);
            if (string.IsNullOrEmpty(g)) g = v.Defaut;
            return v.Windows.FirstOrDefault(w => w.Guid == g);
        }

        internal static bool PorteNomStudio(string libelle) { return libelle != null && (libelle == NomStudio.Nom || libelle.StartsWith(NomStudio.Nom + " (", StringComparison.Ordinal)); }

        private void MajNom(ReleveAudioJeu v)
        {
            if (zoneNom == null) return;
            var s = SortieStudio(v);
            bool nomme = s != null && PorteNomStudio(s.Nom);
            string sig = (s?.Id ?? "-") + "|" + (s?.Nom ?? "") + "|" + nomme + "|" + nomOccupe;
            if (sig == signatureNom) return;
            signatureNom = sig;
            zoneNom.Children.Clear();
            if (s == null) { zoneNom.Children.Add(Studio.T(Langue.T("Aucune sortie audio active trouvée."), 12, Studio.Discret)); return; }
            if (nomme)
            {
                zoneNom.Children.Add(Studio.T(Langue.F("✔  Ta sortie s'appelle « {0} » dans Windows, Discord et tes jeux. Dans le jeu, choisis-la comme sortie audio (ou laisse « Par défaut »).", s.Nom), 12.5, Studio.Vert));
                zoneNom.Children.Add(new WrapPanel { Children = { Studio.Bouton(Langue.T(nomOccupe ? "Un instant…" : "Remettre le nom d'origine"), false, () => NommerStudio(null)) } });
            }
            else
            {
                zoneNom.Children.Add(Studio.T(Langue.F("La sortie par laquelle Velox traite ton son (« {0} ») peut s'afficher « Velox Studio » dans Windows, Discord et tes jeux, comme un vrai appareil audio. Seul son nom change (annulable dans l'Historique).", s.Nom), 12.5, Studio.Discret));
                zoneNom.Children.Add(new WrapPanel { Children = { Studio.Bouton(Langue.T(nomOccupe ? "Un instant…" : "Afficher « Velox Studio » dans mes jeux"), true, () => NommerStudio(s)) } });
            }
        }

        /// <summary>Nomme la sortie « Velox Studio » (s) ou remet les noms d'origine (null). Autorisation administrateur.</summary>
        private void NommerStudio(SortieWindows s)
        {
            if (exemple || nomOccupe) return;
            if (simulation) { F.Statut(Langue.T("Simulation : le nom de la sortie changerait, rien n'est modifié."), true); return; }
            nomOccupe = true; signatureNom = null; MajNom(Releve ?? new ReleveAudioJeu());
            var f = F; string id = s?.Id;
            Task.Run(() => id != null ? NomStudio.Appliquer(id) : NomStudio.Retablir()).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                nomOccupe = false; signatureNom = null;
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                if (err != "") f.Statut(Langue.T(err), true);
                else if (id != null) { Confirmer(f, Langue.T("Velox Studio"), Langue.T("visible dans Windows et tes jeux"), true); f.Statut(Langue.T("Ta sortie s'appelle maintenant « Velox Studio » (annulable dans l'Historique). Si un jeu était ouvert, relance-le pour voir le nouveau nom.")); }
                else f.Statut(Langue.T("Nom d'origine remis."));
                Relever();
            }));
        }

        // ---------------------------------------------------------------- vérification : l'égaliseur agit-il vraiment ?

        /// <summary>
        /// Chaque maillon entre le jeu et le casque, d'après ce que Velox voit (rien n'est supposé) : égaliseur allumé,
        /// Equalizer APO installé et branché sur la sortie qu'utilise Windows, ligne Include en place, Voicemeeter lancé
        /// s'il est la sortie par défaut, Equalizer APO pas mis sur le casque derrière Voicemeeter (Voicemeeter le contourne).
        /// Puis un test d'oreille : le volume baisse fortement 3 secondes par l'égaliseur ; pas entendu = il n'agit pas.
        /// </summary>
        internal static List<Tuple<bool, string>> Diagnostic(ReglagesAudioJeu r, ReleveAudioJeu v, EtatVoicemeeter vm, int etatCible)
        {
            var l = new List<Tuple<bool, string>>();
            bool moteurVm = r.Moteur == "vm";
            bool defautVm = AudioJeu.EstVoicemeeter(v.NomDefaut) && !MoteurSon.EstCable(v.NomDefaut);
            var jeu = AudioJeu.JeuDe(v.Jeu);
            if (r.Actif) l.Add(Tuple.Create(true, Langue.T("Égaliseur allumé.")));
            else if (r.AutoJeu) l.Add(Tuple.Create(false, Langue.T("Égaliseur en attente : Auto jeux ne l'allume que pendant un jeu reconnu. Lance ton jeu (ou fais le test ci-dessous).")));
            else l.Add(Tuple.Create(false, Langue.T("Égaliseur coupé : allume-le (gros bouton rond en haut).")));
            if (defautVm && !vm.Lance) l.Add(Tuple.Create(false, Langue.T("Ta sortie par défaut est Voicemeeter, mais Voicemeeter n'est pas lancé : aucun son ne sort. Lance Voicemeeter.")));
            if (moteurVm)
            {
                l.Add(Tuple.Create(vm.EgaliseurNatif, Langue.T(vm.EgaliseurNatif ? "Égaliseur de Voicemeeter disponible (bus du casque)." : "L'égaliseur de Voicemeeter n'existe que dans Banana et Potato, lancés : choisis Equalizer APO dans le module Voicemeeter.")));
                return l;
            }
            if (!v.Etat.Present) { l.Add(Tuple.Create(false, Langue.T("Equalizer APO n'est pas installé : clique sur le témoin « EQUALIZER APO » en haut."))); return l; }
            l.Add(Tuple.Create(true, Langue.T("Equalizer APO installé.")));
            bool branche = etatCible == 0 || etatCible == 4;
            if (branche) l.Add(Tuple.Create(true, Langue.F("Equalizer APO est sur la sortie qu'utilise Windows ({0}).", v.NomDefaut)));
            else
            {
                // Voicemeeter par défaut, Equalizer APO sur le casque seulement : Voicemeeter (pilote WDM) le contourne
                bool surCasqueDerriereVm = defautVm && v.Sorties.Count > 0 && !v.Sorties.Any(x => AudioJeu.EstVoicemeeter(x.Libelle) && !MoteurSon.EstCable(x.Libelle));
                l.Add(Tuple.Create(false, surCasqueDerriereVm
                    ? Langue.T("Equalizer APO est sur ton casque, mais le son passe par Voicemeeter, qui le contourne : ajoute Equalizer APO à « Voicemeeter Input » (bouton dans Sorties), puis redémarre.")
                    : Langue.F("Equalizer APO n'est pas sur la sortie qu'utilise Windows ({0}) : ajoute-le à cette sortie (bouton dans Sorties), puis redémarre.", v.NomDefaut)));
            }
            if (!v.Etat.Include) l.Add(Tuple.Create(false, Langue.T("L'égaliseur n'a encore jamais été écrit dans Equalizer APO : allume-le une première fois.")));
            if (jeu != null) l.Add(Tuple.Create(true, Langue.F("{0} tourne : vérifie que sa sortie audio est sur « Par défaut » dans ses réglages.", jeu.Nom)));
            return l;
        }

        private void MajVerif(ReleveAudioJeu v)
        {
            if (zoneVerif == null) return;
            int etat; Cible(v, out etat);
            var d = Diagnostic(r, v, Vm, etat);
            string sig = string.Join("|", d.Select(x => x.Item1 + x.Item2)) + "|" + testVolume;
            if (sig == signatureVerif) return;
            signatureVerif = sig;
            zoneVerif.Children.Clear();
            bool ok = d.All(x => x.Item1);
            zoneVerif.Children.Add(Studio.T(Langue.T(ok ? "Tout est branché : l'égaliseur agit sur ton son." : "Quelque chose empêche l'égaliseur d'agir sur ton son :"), 13, ok ? Studio.Vert : Studio.Ambre));
            foreach (var x in d.Where(x => !ok || !x.Item1))
                zoneVerif.Children.Add(Studio.T((x.Item1 ? "✔  " : "✖  ") + x.Item2, 12, x.Item1 ? Studio.Discret : Studio.Texte));
            var ligne = new WrapPanel();
            if (testVolume == 2)
            {
                zoneVerif.Children.Add(Studio.T(Langue.T("As-tu entendu le volume baisser fortement pendant 3 secondes ?"), 12.5, Studio.Texte));
                ligne.Children.Add(Studio.Bouton(Langue.T("Oui"), true, () => { testVolume = 0; signatureVerif = null; F.Statut(Langue.T("Parfait : l'égaliseur agit bien. Pour entendre son effet, maintiens « COMPARER » pendant une partie.")); MajVerif(Releve ?? new ReleveAudioJeu()); }));
                ligne.Children.Add(Studio.Bouton(Langue.T("Non"), false, () => { testVolume = 0; signatureVerif = null; F.Demander(Langue.T("L'égaliseur n'agit pas sur ce que tu écoutes"),
                    Langue.T("Le son que tu écoutes ne passe pas par la sortie où Equalizer APO est installé. Vérifie : 1) dans le jeu, sortie audio sur « Par défaut » ; 2) dans Sorties, la sortie par défaut affiche « ● Equalizer APO » (sinon « Ajouter Equalizer APO », puis redémarre) ; 3) avec Voicemeeter, Equalizer APO doit être sur « Voicemeeter Input », pas sur le casque."), Langue.T("Compris"), "", _ => { }); MajVerif(Releve ?? new ReleveAudioJeu()); }));
            }
            else if (r.Moteur != "vm" && (Releve?.Etat.Present ?? false) && (Releve?.Etat.Include ?? false))
                ligne.Children.Add(Studio.Bouton(Langue.T(testVolume == 1 ? "Test en cours…" : "Tester à l'oreille (3 s)"), false, TesterVolume, Langue.T("Lance un son (vidéo, jeu), puis clique : Velox baisse fortement le volume par l'égaliseur pendant 3 secondes. Si tu l'entends, l'égaliseur agit.")));
            if (ligne.Children.Count > 0) zoneVerif.Children.Add(ligne);
        }

        /// <summary>Test d'oreille : 3 s de volume à −12 dB écrit par Velox dans le fichier de l'égaliseur (sur la sortie
        /// visée), puis l'égaliseur revient tel qu'il était. Rien n'est noté dans l'Historique.</summary>
        private void TesterVolume()
        {
            if (exemple || testVolume != 0) return;
            if (simulation) { F.Statut(Langue.T("Simulation : le test écrirait dans le fichier d'Equalizer APO, rien n'est modifié."), true); return; }
            var copie = Reglages.Copie();
            string attendu = AudioJeu.Generer(copie, copie.CibleEcrite), baisse = AudioJeu.VolumeSeul(copie, copie.CibleEcrite, -12);
            testVolume = 1; signatureVerif = null; MajVerif(Releve ?? new ReleveAudioJeu());
            F.Statut(Langue.T("Test : le volume baisse 3 secondes…"));
            var f = F;
            fileComparaison = fileComparaison.ContinueWith(_ =>
            {
                string e1 = AudioJeu.Comparer(true, attendu, baisse);
                if (e1 == "") System.Threading.Thread.Sleep(3000);
                string e2 = AudioJeu.Comparer(false, attendu);
                return e1 != "" ? e1 : e2;
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                testVolume = err == "" ? 2 : 0; signatureVerif = null;
                if (err != "") f.Statut(Langue.T(err), true);
                MajVerif(Releve ?? new ReleveAudioJeu());
            }));
        }

        // ---------------------------------------------------------------- démarrage rapide (première utilisation)

        /// <summary>
        /// Démarrage rapide : un bouton, et Velox fait tout ce qu'il faut, dans l'ordre. Chaque étape se coche toute seule
        /// d'après ce que Velox voit sur le PC (rien n'est supposé) :
        /// 1. Equalizer APO installé (Velox le télécharge et l'installe) ;
        /// 2. branché sur ta sortie (sa fenêtre s'ouvre, tu coches ton casque) ;
        /// 3. casque reconnu et calibré (Velox devine le modèle d'après le nom de la sortie, tu confirmes) ;
        /// 4. profil pour les pas, Auto jeux et profil adapté à chaque jeu ;
        /// 5. écoute : avant / après, et duel à l'oreille.
        /// </summary>
        private void MajDemarrage(ReleveAudioJeu v)
        {
            if (zoneDemarrage == null) return;
            moduleDemarrage.IsVisible = !r.AssistantFait;
            if (r.AssistantFait) return;
            bool vm = r.Moteur == "vm";
            int etat; Cible(v, out etat);
            bool apo = vm || v.Etat.Present;
            bool branche = vm || (v.Etat.Present && (etat == 0 || etat == 4));
            bool calibre = r.CalibFiltres != null && r.CalibFiltres.Count > 0 && r.CalibActive;
            bool regle = r.AutoJeu && r.ProfilParJeu && (vm || v.Etat.Include);
            // le casque d'après le nom de la sortie (une fois par sortie), dès qu'Equalizer APO est là
            string sortie = v.NomDefaut ?? "";
            if (!calibre && !casqueRefuse && !exemple && sortie != "" && sortie != sortieDevinee && !devinette)
            {
                sortieDevinee = sortie; devinette = true;
                Task.Run(() => Casques.RequeteDepuisSortie(sortie) == "" ? new List<CasqueAutoEq>() : Casques.Deviner(Casques.Index(), sortie, 3))
                    .ContinueWith(t => Dispatcher.UIThread.Post(() => { devinette = false; casquesDevines = t.IsFaulted ? new List<CasqueAutoEq>() : t.Result; signatureDemarrage = null; MajDemarrage(Releve ?? new ReleveAudioJeu()); }));
            }
            string sig = apo + "|" + branche + "|" + calibre + "|" + regle + "|" + configEnCours + "|" + installationApo + "|" + casqueRefuse + "|" + (casquesDevines == null ? "-" : string.Join(",", casquesDevines.Select(c => c.Nom))) + "|" + sortie;
            if (sig == signatureDemarrage) return;
            signatureDemarrage = sig;
            zoneDemarrage.Children.Clear();
            bool fini = apo && branche && regle;
            zoneDemarrage.Children.Add(Studio.T(Langue.T(fini ? "C'est prêt. Écoute la différence, puis lance ton jeu : Velox s'occupe du reste." : "Un clic, et Velox fait tout ce qu'il faut pour que tu entendes mieux les pas. Tu n'as qu'à suivre ce qui s'affiche."), 13, Studio.Texte));
            var liste = new StackPanel { Spacing = 6 };
            liste.Children.Add(Etape(apo, Langue.T("Equalizer APO installé"), apo ? "" : Langue.T("Velox le télécharge depuis son site officiel et l'installe (Windows demandera l'autorisation).")));
            liste.Children.Add(Etape(branche, Langue.T("Branché sur ton casque") + (sortie != "" ? " · " + sortie : ""), branche || !apo ? "" : Langue.T("Une fenêtre d'Equalizer APO s'ouvre : coche ton casque, clique sur OK, puis redémarre le PC si elle le demande.")));
            string detailCasque = calibre ? r.CalibNom : casqueRefuse ? Langue.T("Facultatif : tape ton modèle dans « Calibration du casque », plus bas.") : casquesDevines == null ? Langue.T("Recherche de ton modèle…") : casquesDevines.Count == 0 ? Langue.T("Modèle pas reconnu d'après Windows : tape-le dans « Calibration du casque », plus bas (facultatif).") : "";
            liste.Children.Add(Etape(calibre, Langue.T("Casque reconnu et calibré"), detailCasque, !calibre && (casqueRefuse || (casquesDevines != null && casquesDevines.Count == 0))));
            if (!calibre && !casqueRefuse && casquesDevines != null && casquesDevines.Count > 0)
            {
                var choix = new WrapPanel { Margin = new Thickness(28, 0, 0, 0) };
                choix.Children.Add(new TextBlock { Text = Langue.T("C'est ton casque ?"), FontFamily = Studio.Corps, FontSize = 12.5, Foreground = Studio.P(Studio.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) });
                foreach (var c in casquesDevines) { var cc = c; var b = Puce(c.Nom, () => ChoisirCasque(cc)); Marquer(b, false, Studio.Ambre); choix.Children.Add(b); }
                var bNon = Puce(Langue.T("Aucun de ceux-là"), () => { casqueRefuse = true; signatureDemarrage = null; MajDemarrage(Releve ?? new ReleveAudioJeu()); });
                Marquer(bNon, false); choix.Children.Add(bNon);
                liste.Children.Add(choix);
            }
            liste.Children.Add(Etape(regle, Langue.T("Profil pour les pas, Auto jeux et profil adapté à chaque jeu"), regle ? Langue.T("L'égaliseur s'allume tout seul au lancement de tes jeux, avec le bon profil.") : ""));
            zoneDemarrage.Children.Add(liste);
            var boutons = new WrapPanel();
            if (!fini)
            {
                string libelle = configEnCours || installationApo ? Langue.T("Configuration en cours…") : !apo ? Langue.T("Tout configurer pour moi") : !branche ? Langue.T("Brancher Equalizer APO sur mon casque") : Langue.T("Terminer la configuration");
                boutons.Children.Add(Studio.Bouton(libelle, true, () => ConfigurerTout(Releve ?? new ReleveAudioJeu())));
            }
            else
            {
                boutons.Children.Add(Studio.Bouton(Langue.T("▶  Écouter la différence"), true, LancerDemo, Langue.T("La même scène de jeu sans puis avec le Studio son, au même volume.")));
                boutons.Children.Add(Studio.Bouton(Langue.T("Trouver mon profil à l'oreille"), false, LancerDuel, Langue.T("Duel à l'aveugle entre plusieurs profils, au même volume.")));
                boutons.Children.Add(Studio.Bouton(Langue.T("C'est bon, masquer"), false, () => { if (exemple) return; r.AssistantFait = true; Changer(Langue.T("Démarrage rapide masqué : le bouton « Démarrage rapide » (Profils) le rouvre."), false, true); }));
            }
            zoneDemarrage.Children.Add(boutons);
            if (fini) zoneDemarrage.Children.Add(Studio.T(Langue.T("Pour aller plus loin, plus bas : « Pas boostés » (les pas lointains remontent face aux explosions) et le son 7.1 virtuel."), 11.5, Studio.Faible));
        }

        private Control Etape(bool fait, string titre, string detail, bool facultatif = false)
        {
            var coche = new Border
            {
                Width = 20, Height = 20, CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 8, 0),
                Background = fait ? Studio.P(Studio.Vert, 220) : Studio.P(Studio.Fond1, 200), BorderBrush = Studio.P(fait ? Studio.Vert : facultatif ? Studio.Bord : Studio.Ambre, 200), BorderThickness = new Thickness(1.5),
                Child = new TextBlock { Text = fait ? "✔" : "", FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Studio.P(Studio.Fond1), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
            };
            var textes = new StackPanel { Spacing = 1 };
            textes.Children.Add(new TextBlock { Text = titre, FontFamily = Studio.Corps, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Studio.P(fait ? Studio.Texte : Studio.Discret), TextWrapping = TextWrapping.Wrap });
            if (!string.IsNullOrEmpty(detail)) textes.Children.Add(Studio.T(detail, 11.5, Studio.Faible));
            var l = new DockPanel();
            DockPanel.SetDock(coche, Dock.Left);
            l.Children.Add(coche); l.Children.Add(textes);
            AutomationProperties.SetName(l, (fait ? Langue.T("Fait") : Langue.T("À faire")) + " : " + titre);
            return l;
        }

        /// <summary>Le gros bouton : fait l'étape suivante (et toutes celles qui peuvent se faire d'un coup).</summary>
        private void ConfigurerTout(ReleveAudioJeu v)
        {
            if (exemple || configEnCours || installationApo) return;
            if (simulation) { F.Statut(Langue.T("Simulation : Velox configurerait tout, rien n'est modifié."), true); return; }
            bool vm = r.Moteur == "vm";
            int etat; Cible(v, out etat);
            if (!vm && !v.Etat.Present) { InstallerApo(v.NomDefaut); return; }
            if (!vm && etat != 0 && etat != 4)
            {
                F.Demander(Langue.T("Brancher Equalizer APO sur ton casque"),
                    Langue.F("Une fenêtre d'Equalizer APO va s'ouvrir. Coche « {0} » (ta sortie actuelle), clique sur OK, et redémarre le PC si elle te le demande. Puis reviens ici : la suite se fait toute seule.", v.NomDefaut != "" ? v.NomDefaut : Langue.T("ton casque")),
                    Langue.T("Ouvrir"), Langue.T("Annuler"), oui => { if (oui) OuvrirConfigurator(); });
                return;
            }
            AvecAccord(() =>
            {
                configEnCours = true;
                // profil pour les pas (celui du jeu s'il tourne), force normale, sortie suivie automatiquement
                var jeu = AudioJeu.JeuDe(v.Jeu);
                r.Cible = "auto"; r.Intensite = 100;
                r.Gains = (double[])(jeu != null ? jeu.Gains : AudioJeu.Profils[0].Gains).Clone();
                if (jeu != null) r.CoupeBas = jeu.CoupeBas;
                r.AutoJeu = true; r.ProfilParJeu = true; r.JeuEnCours = "";
                AudioJeu.SuivreJeu(r, v.Jeu, r.Actif);
                AnnoncerApres(Langue.T("Studio son configuré"), jeu != null ? jeu.Nom : Langue.T("Auto jeux actif"));
                Changer(Langue.T(jeu != null ? "Configuré : ton jeu tourne, l'égaliseur est actif avec son profil." : "Configuré : l'égaliseur s'allumera tout seul au lancement de tes jeux, avec le bon profil."), true, true);
                configEnCours = false; signatureDemarrage = null;
                MettreAJourBientot();
            });
        }

        // ---------------------------------------------------------------- « Pas boostés » : moteur audio de Velox

        private void MajPas(ReleveAudioJeu v)
        {
            if (zonePas == null) return;
            var m = v.Moteur ?? new ReglagesMoteurSon();
            var et = v.MoteurEtat ?? new MoteurSon.EtatMoteur();
            bool cable = v.Cables.Count > 0;
            string sig = cable + "|" + m.Actif + "|" + m.Force + "|" + m.JeuxSeulement + "|" + pasOccupe + "|" + (et.Erreur != "");
            if (sig != signaturePas)
            {
                signaturePas = sig;
                zonePas.Children.Clear();
                zonePas.Children.Add(Studio.T(Langue.T("Le vrai saut pour entendre les pas : Velox traite lui-même le son en direct. Les sons faibles (pas lointains, recharges) remontent, les explosions et les tirs sont tenus, et rien ne sature. L'égaliseur et la calibration de ton casque continuent par-dessus."), 12.5, Studio.Discret));
                if (!cable)
                {
                    zonePas.Children.Add(Studio.T(Langue.T("Il faut un câble audio virtuel : VB-Cable, gratuit. Installe-le, redémarre le PC, puis reviens ici : Velox branche tout lui-même."), 12, Studio.Ambre));
                    zonePas.Children.Add(new WrapPanel { Children = { Studio.Bouton(Langue.T("Télécharger VB-Cable (gratuit)"), true, () => { if (!exemple) Programmes.Ouvrir(MoteurSon.SiteCable); }) } });
                }
                else
                {
                    var ligne = new WrapPanel();
                    var bOn = Puce(Langue.T(m.Actif ? "Pas boostés : actif" : "Pas boostés : coupé"), () => BasculerPas(!m.Actif));
                    Marquer(bOn, m.Actif, Studio.Vert);
                    ligne.Children.Add(bOn);
                    foreach (var n in MoteurSon.Niveaux)
                    {
                        var nn = n;
                        var b = Puce(Langue.T(n.Item1), () => ReglerPas(nn.Item2, null));
                        Marquer(b, m.Force == n.Item2, Studio.Magenta);
                        ligne.Children.Add(b);
                    }
                    var bJeux = Puce(Langue.T("Seulement en jeu"), () => ReglerPas(null, !m.JeuxSeulement));
                    ToolTip.SetTip(bJeux, Langue.T("Le son est traité seulement quand un jeu reconnu tourne (Warzone, Valorant, CS2…) : musique et vidéos restent intactes."));
                    Marquer(bJeux, m.JeuxSeulement, Studio.Cyan);
                    ligne.Children.Add(bJeux);
                    zonePas.Children.Add(ligne);
                    tPasDirect = Studio.T("", 12, Studio.Texte);
                    zonePas.Children.Add(tPasDirect);
                    zonePas.Children.Add(Studio.T(Langue.T("Léger : +9 dB sur les pas lointains face aux explosions. Fort : +16 dB. Extrême : +23 dB (mesuré sur une scène de jeu). Le son passe par VB-Cable, puis Velox le joue sur ton casque : environ 25 ms de retard en plus. Dans le jeu, laisse la sortie audio sur « Par défaut »."), 11.5, Studio.Faible));
                }
            }
            if (tPasDirect != null && cable)
            {
                if (pasOccupe) tPasDirect.Text = Langue.T("Branchement en cours…");
                else if (!m.Actif) tPasDirect.Text = Langue.T("Coupé : le son va directement au casque.");
                else if (et.Erreur != "") tPasDirect.Text = Langue.T(et.Erreur);
                else if (!et.Marche) tPasDirect.Text = Langue.T("Démarrage du moteur…");
                else if (!et.Jeu) tPasDirect.Text = Langue.F("En attente d'un jeu (le son passe tel quel) · retard {0} ms", et.LatenceMs);
                else tPasDirect.Text = Langue.F("En marche · sons forts tenus jusqu'à −{0} dB · retard {1} ms", et.Reduction.ToString("F0", Langue.Culture), et.LatenceMs);
                tPasDirect.Foreground = Studio.P(m.Actif && et.Erreur != "" ? Studio.Ambre : Studio.Texte);
            }
        }

        private void BasculerPas(bool allumer)
        {
            if (exemple || pasOccupe) return;
            if (simulation) { F.Statut(Langue.T(allumer ? "Simulation : « Pas boostés » serait allumé, rien n'est modifié." : "Simulation : « Pas boostés » serait coupé, rien n'est modifié."), true); return; }
            var m = (Releve ?? new ReleveAudioJeu()).Moteur ?? new ReglagesMoteurSon();
            int force = m.Force; bool jeux = m.JeuxSeulement;
            pasOccupe = true; signaturePas = null; MajPas(Releve ?? new ReleveAudioJeu());
            var f = F;
            Task.Run(() => allumer ? MoteurSon.Allumer(force, jeux) : MoteurSon.Couper(true)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                pasOccupe = false; signaturePas = null;
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                if (err != "") { f.Statut(Langue.T(err), true); Relever(); return; }
                Confirmer(f, Langue.T(allumer ? "Pas boostés allumé" : "Pas boostés coupé"), allumer ? Langue.T("VB-Cable → Velox → ton casque") : Langue.T("le son va directement au casque"), allumer);
                f.Statut(Langue.T(allumer ? "« Pas boostés » allumé : le jeu joue dans VB-Cable, Velox traite le son et le joue sur ton casque (changement de sortie annulable dans l'Historique)." : "« Pas boostés » coupé : ton casque est de nouveau la sortie par défaut."));
                Relever();
            }));
        }

        private void ReglerPas(int? force, bool? jeux)
        {
            if (exemple) return;
            if (simulation) { F.Statut(Langue.T("Simulation : rien n'est modifié."), true); return; }
            try
            {
                var m = MoteurSon.Lire();
                if (force.HasValue) m.Force = force.Value;
                if (jeux.HasValue) m.JeuxSeulement = jeux.Value;
                MoteurSon.Ecrire(m);   // le moteur relit ses réglages dans les 300 ms
                if (Releve != null) Releve.Moteur = m;
                if (force.HasValue) Confirmer(F, Langue.T("Pas boostés"), Langue.T(MoteurSon.Niveaux.First(x => x.Item2 == force.Value).Item1), true);
                signaturePas = null; MajPas(Releve ?? new ReleveAudioJeu());
            }
            catch (Exception ex) { F.Statut(ex.Message, true); }
        }

        // ---------------------------------------------------------------- duel à l'oreille (choix du profil à l'aveugle)

        /// <summary>Candidats du duel : les trois profils pour les pas, celui du jeu visé, et tes réglages s'ils sont
        /// différents ; chacun avec ta calibration, ta force et ton coupe-explosions. Au plus quatre, dans le désordre.</summary>
        internal static List<Tuple<string, ReglagesAudioJeu>> CandidatsDuel(ReglagesAudioJeu r, string exeJeu)
        {
            var l = new List<Tuple<string, ReglagesAudioJeu>>();
            Action<string, double[]> ajouter = (nom, gains) =>
            {
                if (l.Count >= 4 || l.Any(x => x.Item2.Gains.SequenceEqual(gains))) return;
                var c = r.Copie(); c.Actif = true; c.Gains = (double[])gains.Clone();
                l.Add(Tuple.Create(nom, c));
            };
            var jeu = AudioJeu.JeuVise(exeJeu, r);
            ajouter(Langue.F("Tes réglages ({0})", Langue.T(AudioJeu.NomProfil(r))), r.Gains);
            foreach (var cle in new[] { "competitif", "extreme", "equilibre" }) { var p = AudioJeu.Profils.First(x => x.Cle == cle); ajouter(Langue.T(p.Nom), p.Gains); }
            ajouter(jeu.Nom, jeu.Gains);
            return l.OrderBy(x => hasardDuel.Next()).ToList();
        }

        private void LancerDuel()
        {
            if (exemple) return;
            if (duelEtape != 0) { ArreterDuel(true); F.Statut(Langue.T("Duel arrêté : tes réglages n'ont pas changé.")); return; }
            if (!OperatingSystem.IsWindows()) return;
            if (simulation) { F.Statut(Langue.T("Simulation : le duel a besoin de couper l'égaliseur un instant, rien n'est modifié ici."), true); return; }
            ArreterDemo();
            duelCandidats = CandidatsDuel(r, Releve?.Jeu);
            duelEtape = 1; duelChampion = 0; duelChallenger = 1; duelNumero = 1;
            AfficherDuel(Langue.T("Préparation des sons…"), false);
            var cands = duelCandidats.Select(x => x.Item2).ToList();
            Task.Run(() => DemoAudio.Duel(cands)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (duelEtape != 1) return;
                if (t.IsFaulted) { ArreterDuel(true); F.Statut(Langue.T("Le duel n'a pas pu préparer les sons."), true); return; }
                duelSons = t.Result;
                Contourner(F, true);   // les sons sont déjà égalisés : rien ne doit s'ajouter par-dessus
                JouerDuel();
            }));
        }

        /// <summary>Joue le duel en cours : son 1 puis son 2 (ordre tiré au hasard), puis attend ton choix.</summary>
        private void JouerDuel()
        {
            if (duelEtape == 0) return;
            if (minuterieDuel == null) { minuterieDuel = new DispatcherTimer(); minuterieDuel.Tick += (o, a) => EtapeDuel(); }
            duelInverse = hasardDuel.Next(2) == 1;
            duelEtape = 2;
            AfficherDuel(Langue.F("Duel {0} sur {1} : écoute le son 1…", duelNumero, duelCandidats.Count - 1), false);
            minuterieDuel.Interval = TimeSpan.FromMilliseconds(500);   // (Equalizer APO relit son fichier)
            minuterieDuel.Start();
        }

        private void EtapeDuel()
        {
            minuterieDuel.Stop();
            int a = duelInverse ? duelChallenger : duelChampion, b = duelInverse ? duelChampion : duelChallenger;
            switch (duelEtape)
            {
                case 2: DemoAudio.Jouer(duelSons[a]); duelEtape = 3; MarquerDuel(1); minuterieDuel.Interval = TimeSpan.FromSeconds(DemoAudio.Duree + 0.4); break;
                case 3: DemoAudio.Jouer(duelSons[b]); duelEtape = 4; MarquerDuel(2); AfficherDuel(Langue.F("Duel {0} sur {1} : écoute le son 2…", duelNumero, duelCandidats.Count - 1), false); minuterieDuel.Interval = TimeSpan.FromSeconds(DemoAudio.Duree + 0.4); break;
                default: duelEtape = 5; MarquerDuel(0); AfficherDuel(Langue.T("Dans lequel entends-tu le mieux les pas (et d'où ils viennent) ?"), true); return;
            }
            minuterieDuel.Start();
        }

        /// <summary>Ton choix : 1 ou 2. Le gagnant affronte le suivant ; à la fin, le profil gagnant est proposé.</summary>
        private void ChoisirDuel(int son)
        {
            if (duelEtape != 5) return;
            int a = duelInverse ? duelChallenger : duelChampion, b = duelInverse ? duelChampion : duelChallenger;
            duelChampion = son == 1 ? a : b;
            duelChallenger++; duelNumero++;
            if (duelChallenger < duelCandidats.Count) { JouerDuel(); return; }
            // fin : l'égaliseur revient, le gagnant est dévoilé
            Contourner(F, false);
            duelEtape = 6;
            var g = duelCandidats[duelChampion];
            zoneDuel.Children.Clear();
            zoneDuel.Children.Add(Studio.T(Langue.F("Ton oreille a choisi : {0}", g.Item1), 15, Studio.Ambre));
            bool deja = g.Item2.Gains.SequenceEqual(r.Gains);
            zoneDuel.Children.Add(Studio.T(Langue.T(deja ? "C'est déjà ton réglage : il est fait pour toi." : "Choisi à l'aveugle, au même volume que les autres : c'est vraiment le profil qui te fait le mieux entendre les pas avec ton casque."), 12, Studio.Discret));
            var ligne = new WrapPanel();
            if (!deja) ligne.Children.Add(Studio.Bouton(Langue.T("Appliquer ce profil"), true, () =>
            {
                r.Gains = (double[])g.Item2.Gains.Clone();
                AnnoncerApres(Langue.T(r.Actif ? "Profil appliqué" : "Profil choisi"), g.Item1);
                ArreterDuel(false);
                Changer(Langue.F("Profil « {0} » appliqué (choisi au duel à l'oreille).", g.Item1), false, true);
            }));
            ligne.Children.Add(Studio.Bouton(Langue.T(deja ? "Fermer" : "Garder mes réglages"), false, () => ArreterDuel(false)));
            zoneDuel.Children.Add(ligne);
            if (bDuel != null && bDuel.Child is TextBlock tb) tb.Text = Langue.T("Duel à l'oreille");
        }

        private void AfficherDuel(string texte, bool choix)
        {
            zoneDuel.IsVisible = true;
            zoneDuel.Children.Clear();
            zoneDuel.Children.Add(Studio.T(texte, 13.5, Studio.Texte));
            if (choix)
            {
                var l = new WrapPanel();
                l.Children.Add(Studio.Bouton(Langue.T("Le son 1"), true, () => ChoisirDuel(1)));
                l.Children.Add(Studio.Bouton(Langue.T("Le son 2"), true, () => ChoisirDuel(2)));
                l.Children.Add(Studio.Bouton(Langue.T("Réécouter"), false, JouerDuel));
                zoneDuel.Children.Add(l);
            }
            zoneDuel.Children.Add(Studio.T(Langue.T("À l'aveugle : les noms des profils sont cachés, et tous les sons ont le même volume."), 11.5, Studio.Faible));
            if (bDuel != null && bDuel.Child is TextBlock tb) tb.Text = Langue.T("■  Arrêter le duel");
        }

        /// <summary>Allume la carte du son en cours (1 ou 2, 0 : aucune), avec les cartes SANS / AVEC de la démo.</summary>
        private void MarquerDuel(int son)
        {
            if (carteSans == null) return;
            foreach (var c in new[] { carteSans, carteAvec })
            {
                bool on = (c == carteSans && son == 1) || (c == carteAvec && son == 2);
                c.Background = on ? Studio.Degrade(1, Habillage.Alpha(Studio.Cyan, 50), Habillage.Alpha(Studio.Violet, 30)) : Studio.P(Studio.Fond1, 150);
                c.BorderBrush = on ? Studio.P(Studio.Cyan, 220) : Studio.P(Studio.Bord);
                if (c.Child is StackPanel sp && sp.Children.Count == 2 && sp.Children[0] is TextBlock t0 && sp.Children[1] is TextBlock t1)
                {
                    t0.Text = duelEtape == 0 ? Langue.T(c == carteSans ? "SANS" : "AVEC") : Langue.T(c == carteSans ? "SON 1" : "SON 2");
                    t1.Text = duelEtape == 0 ? Langue.T(c == carteSans ? "le son d'origine" : "le Studio son") : Langue.T("profil caché");
                }
            }
        }

        private void ArreterDuel(bool remettre)
        {
            if (duelEtape == 0) return;
            if (minuterieDuel != null) minuterieDuel.Stop();
            DemoAudio.Arreter();
            Contourner(F, false);   // l'égaliseur revient toujours
            duelEtape = 0; duelSons = null;
            if (zoneDuel != null) { zoneDuel.Children.Clear(); zoneDuel.IsVisible = false; }
            MarquerDuel(0);
            MarquerDemo(0);
            if (bDuel != null && bDuel.Child is TextBlock tb) tb.Text = Langue.T("Duel à l'oreille");
        }

        // ---------------------------------------------------------------- son 7.1 virtuel (pilotage de HeSuVi)

        /// <summary>
        /// HeSuVi (gratuit, libre) spatialise le son au casque : allumé ou coupé, et le modèle de virtualisation (GSX, Dolby
        /// Atmos, DTS…) choisi ici. Velox n'écrit que les trois petits réglages de HeSuVi, annulables dans l'Historique.
        /// </summary>
        private void MajSurround(ReleveAudioJeu v)
        {
            if (zoneSurround == null) return;
            var h = v.HeSuVi ?? new EtatHeSuVi();
            bool youSound = v.Etat.Autres.Contains("YouSound");
            var jeuVise = AudioJeu.JeuVise(v.Jeu, r);
            string sig = h.Present + "|" + h.Inclus + "|" + h.Actif + "|" + h.Modele + "|" + h.Modeles.Count + "|" + youSound + "|" + tousModeles + "|" + jeuVise.Cle;
            if (sig == signatureSurround) return;
            signatureSurround = sig;
            zoneSurround.Children.Clear();
            zoneSurround.Children.Add(Studio.T(Langue.T("Le son spatial au casque : en 7.1, tu entends d'où viennent les pas (devant, derrière, sur les côtés). Velox pilote HeSuVi, gratuit et libre, déjà prévu pour Equalizer APO."), 12.5, Studio.Discret));
            if (!h.Present)
            {
                zoneSurround.Children.Add(Studio.T(Langue.T("HeSuVi n'est pas installé. Installe-le (gratuit), puis reviens ici : Velox l'allume, l'éteint et choisit son modèle pour toi."), 12, Studio.Ambre));
                zoneSurround.Children.Add(new WrapPanel { Children = { Studio.Bouton(Langue.T("Ouvrir la page de HeSuVi"), false, () => { if (!exemple) Programmes.Ouvrir(NettoyagePC.HeSuVi.Site); }) } });
                return;
            }
            if (!h.Inclus)
            {
                zoneSurround.Children.Add(Studio.T(Langue.T("HeSuVi est installé mais pas branché dans Equalizer APO : ouvre HeSuVi une fois, il s'y branche lui-même."), 12, Studio.Ambre));
                zoneSurround.Children.Add(new WrapPanel { Children = { Studio.Bouton(Langue.T("Ouvrir HeSuVi"), false, () => { if (!exemple) Programmes.Ouvrir(Path.Combine(NettoyagePC.HeSuVi.Dossier, "HeSuVi.exe")); }) } });
                return;
            }
            var bAct = Puce(Langue.T(h.Actif ? "Son 7.1 virtuel actif" : "Son 7.1 virtuel coupé"), () => ReglerSurround(!h.Actif, null));
            Marquer(bAct, h.Actif, Studio.Vert);
            zoneSurround.Children.Add(new WrapPanel { Children = { bAct } });
            var modeles = h.Modeles;
            var conseilles = modeles.Where(m => NettoyagePC.HeSuVi.Conseilles.Contains(m.Nom)).ToList();
            var montres = tousModeles ? modeles : (conseilles.Count > 0 ? conseilles : modeles.Take(8).ToList());
            if (!montres.Any(m => m.Nom == h.Modele)) { var c = modeles.FirstOrDefault(m => m.Nom == h.Modele); if (c != null) montres = new[] { c }.Concat(montres).ToList(); }
            var choix = new WrapPanel();
            choix.Children.Add(new TextBlock { Text = Langue.T("Modèle :"), FontFamily = Studio.Corps, FontSize = 12.5, Foreground = Studio.P(Studio.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 8) });
            foreach (var m in montres)
            {
                var mm = m;
                var b = Puce(m.Nom.ToUpperInvariant(), () => ReglerSurround(null, mm.Nom));
                if (m.Description != "") ToolTip.SetTip(b, m.Description);
                AutomationProperties.SetName(b, m.Nom + (m.Description != "" ? " : " + m.Description : ""));
                Marquer(b, m.Nom == h.Modele, Studio.Cyan);
                choix.Children.Add(b);
            }
            if (modeles.Count > conseilles.Count && conseilles.Count > 0)
            {
                var bTous = Puce(tousModeles ? Langue.T("Moins de modèles") : Langue.F("Tous les modèles ({0})", modeles.Count), () => { tousModeles = !tousModeles; signatureSurround = null; MajSurround(Releve ?? new ReleveAudioJeu()); });
                Marquer(bTous, false);
                choix.Children.Add(bTous);
            }
            zoneSurround.Children.Add(choix);
            var actuel = modeles.FirstOrDefault(m => m.Nom == h.Modele);
            if (actuel != null && actuel.Description != "") zoneSurround.Children.Add(Studio.T(Langue.F("En ce moment : {0}", actuel.Description), 11.5, Studio.Faible));
            if (youSound && h.Actif) zoneSurround.Children.Add(Studio.T(Langue.T("YouSound fait aussi sa propre spatialisation : n'en garde qu'une, sinon le son devient flou."), 12, Studio.Ambre));
            if (h.Actif && jeuVise.Spatial != "")
                zoneSurround.Children.Add(Studio.T(Langue.F("Dans {0}, coupe l'option « {1} » tant que le 7.1 virtuel est allumé : deux spatialisations l'une sur l'autre brouillent la direction des pas. Ou fais l'inverse : garde l'option du jeu et coupe le 7.1 virtuel. Essaie les deux, garde celle où tu situes le mieux les pas.", jeuVise.Nom, Langue.T(jeuVise.Spatial)), 12, Studio.Ambre));
            else if (h.Actif && jeuVise.Cle == "cs2")
                zoneSurround.Children.Add(Studio.T(Langue.T("Counter-Strike 2 spatialise déjà le son au casque : coupe le 7.1 virtuel pendant tes parties de CS2."), 12, Studio.Ambre));
            if (h.Actif) zoneSurround.Children.Add(Studio.T(Langue.T("Une seule spatialisation à la fois : laisse aussi coupés le son spatial de Windows (Windows Sonic, Dolby Atmos, DTS) sur cette sortie et le surround virtuel du logiciel de ton casque."), 11.5, Studio.Faible));
            zoneSurround.Children.Add(Studio.T(Langue.T("Pour un vrai 7.1, ta sortie Windows doit être réglée en 7.1 (Paramètres > Son > ta sortie > Configurer), ou passe par Voicemeeter Input en 7.1. En stéréo, HeSuVi spatialise quand même la gauche et la droite. Le 7.1 maison de Velox arrive dans une prochaine version."), 11.5, Studio.Faible));
        }

        private void ReglerSurround(bool? actif, string modele)
        {
            if (exemple) return;
            if (simulation) { F.Statut(Langue.T("Simulation : HeSuVi serait réglé, rien n'est modifié."), true); return; }
            var f = F;
            Task.Run(() => NettoyagePC.HeSuVi.Appliquer(actif, modele)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                if (err != "") { f.Statut(Langue.T(err), true); return; }
                if (actif == false) Confirmer(f, Langue.T("Son 7.1 virtuel coupé"), "HeSuVi", false);
                else Confirmer(f, Langue.T(modele != null ? "Modèle de spatialisation appliqué" : "Son 7.1 virtuel actif"), (modele ?? (Releve?.HeSuVi?.Modele ?? "")).ToUpperInvariant() + " · HeSuVi", true);
                f.Statut(Langue.T("Réglage de HeSuVi appliqué (annulable dans l'Historique)."));
                signatureSurround = null;
                Relever();
            }));
        }

        // ---------------------------------------------------------------- calibration du casque (base AutoEq)

        /// <summary>
        /// Le modèle du casque : sa correction (mesures de laboratoire, base libre AutoEq) rend le son neutre, et le profil
        /// pas s'ajoute par-dessus. Recherche dans plus de 6 000 modèles ; la correction s'écoute tout de suite et s'annule
        /// dans l'Historique comme le reste.
        /// </summary>
        private void MajCasque()
        {
            if (zoneCasque == null) return;
            string sig = r.CalibActive + "|" + r.CalibNom + "|" + r.CalibSource + "|" + (r.CalibFiltres?.Count ?? 0) + "|" + rechercheCasque + "|" + r.Moteur;
            if (sig == signatureCasque) return;
            signatureCasque = sig;
            zoneCasque.Children.Clear();
            zoneCasque.Children.Add(Studio.T(Langue.T("Chaque casque colore le son à sa façon. Choisis le tien : Velox corrige ses défauts d'après des mesures de laboratoire (base libre AutoEq, plus de 6 000 modèles) pour un son neutre de studio, puis le profil pas s'ajoute par-dessus."), 12.5, Studio.Discret));
            bool aCasque = !string.IsNullOrEmpty(r.CalibNom) && r.CalibFiltres != null && r.CalibFiltres.Count > 0;
            if (aCasque)
            {
                var carte = new StackPanel { Spacing = 4 };
                carte.Children.Add(new TextBlock { Text = r.CalibNom, FontFamily = Studio.Titre, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Studio.P(r.CalibActive ? Studio.Ambre : Studio.Discret), TextWrapping = TextWrapping.Wrap });
                carte.Children.Add(Studio.T(Langue.F("Mesuré par {0} · {1} filtres de correction", r.CalibSource, r.CalibFiltres.Count), 11.5, Studio.Faible));
                var boutons = new WrapPanel { Margin = new Thickness(0, 6, 0, 0) };
                var bAct = Puce(Langue.T(r.CalibActive ? "Calibration active" : "Calibration coupée"), () =>
                {
                    if (exemple) return;
                    r.CalibActive = !r.CalibActive;
                    AnnoncerApres(Langue.T(r.CalibActive ? "Calibration active" : "Calibration coupée"), r.CalibNom);
                    Changer(Langue.T(r.CalibActive ? "Calibration du casque active." : "Calibration du casque coupée."), false, true);
                });
                Marquer(bAct, r.CalibActive, Studio.Vert);
                ToolTip.SetTip(bAct, Langue.T("Allume ou coupe la correction du casque (le profil pas reste)."));
                boutons.Children.Add(bAct);
                var bChanger = Puce(Langue.T(rechercheCasque ? "Fermer la recherche" : "Changer de casque"), () => { rechercheCasque = !rechercheCasque; signatureCasque = null; MajCasque(); if (rechercheCasque) saisieCasque?.Focus(); });
                Marquer(bChanger, false);
                boutons.Children.Add(bChanger);
                carte.Children.Add(boutons);
                zoneCasque.Children.Add(new Border { Child = carte, Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(12), Background = Studio.P(r.CalibActive ? Studio.Ambre : Studio.Violet, r.CalibActive ? 16 : 10), BorderBrush = Studio.P(r.CalibActive ? Studio.Ambre : Studio.Bord, r.CalibActive ? 150 : 160), BorderThickness = new Thickness(1) });
            }
            if (!aCasque || rechercheCasque)
            {
                saisieCasque = new TextBox
                {
                    Watermark = Langue.T("Tape le modèle de ton casque (ex. Arctis Nova Pro, HD 650, Cloud II)"), FontFamily = Studio.Corps, FontSize = 13.5,
                    Background = Studio.P(Studio.Fond1, 200), Foreground = Studio.P(Studio.Texte), BorderBrush = Studio.P(Studio.Bord), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8),
                };
                AutomationProperties.SetName(saisieCasque, Langue.T("Modèle de ton casque"));
                saisieCasque.TextChanged += (o, a) =>
                {
                    if (delaiCasque == null) { delaiCasque = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(220) }; delaiCasque.Tick += (s2, a2) => { delaiCasque.Stop(); ChercherCasque(); }; }
                    delaiCasque.Stop(); delaiCasque.Start();
                };
                resultatsCasque = new StackPanel { Spacing = 4 };
                tCasque = Studio.T("", 11.5, Studio.Faible); tCasque.IsVisible = false;
                zoneCasque.Children.Add(saisieCasque);
                zoneCasque.Children.Add(tCasque);
                zoneCasque.Children.Add(resultatsCasque);
            }
            if (r.Moteur == "vm") zoneCasque.Children.Add(Studio.T(Langue.T("La calibration passe par Equalizer APO : avec l'égaliseur de Voicemeeter, elle n'est pas appliquée."), 12, Studio.Ambre));
            zoneCasque.Children.Add(Studio.T(Langue.T("Mesures : projet AutoEq (licence MIT) et ses sources (oratory1990, crinacle, Rtings…)."), 10.5, Studio.Faible));
        }

        private void ChercherCasque()
        {
            if (saisieCasque == null || resultatsCasque == null) return;
            string q = saisieCasque.Text ?? "";
            resultatsCasque.Children.Clear();
            if (q.Trim().Length < 2) { tCasque.IsVisible = false; return; }
            if (exemple) return;
            tCasque.Text = Langue.T("Recherche…"); tCasque.IsVisible = true;
            Task.Run(() => Casques.Chercher(Casques.Index(), q, 8)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (saisieCasque == null || saisieCasque.Text != q) return;   // la recherche a changé entre-temps
                resultatsCasque.Children.Clear();
                if (t.IsFaulted) { tCasque.Text = Langue.T((t.Exception.InnerException ?? t.Exception).Message.StartsWith("La base") ? (t.Exception.InnerException ?? t.Exception).Message : "La base des casques ne répond pas : vérifie ta connexion Internet."); return; }
                var l = t.Result;
                tCasque.Text = l.Count == 0 ? Langue.T("Aucun modèle trouvé : essaie avec moins de mots (marque et modèle).") : Langue.T("Choisis ton modèle :");
                foreach (var c in l)
                {
                    var cc = c;
                    var b = Puce(c.Nom + "  ·  " + c.Source, () => ChoisirCasque(cc));
                    b.Margin = new Thickness(0); b.HorizontalAlignment = HorizontalAlignment.Left;
                    Marquer(b, false);
                    resultatsCasque.Children.Add(b);
                }
            }));
        }

        private void ChoisirCasque(CasqueAutoEq c)
        {
            if (exemple) return;
            F.Statut(Langue.F("Calibration de « {0} »…", c.Nom));
            Task.Run(() => Casques.Correction(c)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (t.IsFaulted) { F.Statut(Langue.T((t.Exception.InnerException ?? t.Exception).Message), true); return; }
                r.CalibNom = c.Nom; r.CalibSource = c.Source; r.CalibFiltres = t.Result; r.CalibActive = true;
                rechercheCasque = false; signatureCasque = null;
                AnnoncerApres(Langue.T("Casque calibré"), c.Nom);
                Changer(Langue.F("Casque calibré : {0}. Écoute la différence avec le bouton « Calibration active ».", c.Nom), false, true);
            }));
        }

        // ---------------------------------------------------------------- table de mixage de Voicemeeter

        /// <summary>
        /// Entrées et bus de Voicemeeter, pilotés d'ici : volume (fader), mute, envois vers les bus (A1, B1…). Chaque
        /// réglage passe par Voicemeeter et s'annule dans l'Historique. Les valeurs suivent celles de Voicemeeter, sauf
        /// pendant un réglage fait ici.
        /// </summary>
        private void MajMixage()
        {
            if (zoneMix == null) return;
            var e = Vm;
            string sig = e.Lance + "|" + e.Type + "|" + string.Join(",", e.Entrees.Select(x => x.Label)) + "|" + string.Join(",", e.Bus.Select(b => b.Label + b.Appareil));
            if (sig != signatureMix)
            {
                signatureMix = sig;
                ConstruireMixage(e);
                return;
            }
            if (mixEnAttente.Count > 0 || envoiMix || (DateTime.Now - mixLocal).TotalMilliseconds < 600) return;   // réglage en cours ici : Voicemeeter n'a pas encore suivi
            foreach (var t in tranches)
            {
                float gain; bool muet; bool[] envois = null;
                if (t.EstBus) { var b = Bus(e, t.Index); if (b == null) continue; gain = b.Gain; muet = b.Muet; }
                else { var x = Entree(e, t.Index); if (x == null) continue; gain = x.Gain; muet = x.Muet; envois = x.Envois; }
                if (t.Fader.Valeur != gain) { t.Fader.Valeur = gain; t.TGain.Text = TexteGain(gain); }
                if (t.Muet != muet) { t.Muet = muet; Marquer(t.BMute, muet, Studio.Rouge); }
                if (envois != null)
                    for (int k = 0; k < t.Envois.Count && k < envois.Length; k++)
                        if (t.EtatEnvois[k] != envois[k]) { t.EtatEnvois[k] = envois[k]; Marquer(t.Envois[k].Item2, envois[k], Studio.Cyan); }
            }
        }

        private static string TexteGain(double g) { return (g > 0 ? "+" : "") + g.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " dB"; }

        private void ConstruireMixage(EtatVoicemeeter e)
        {
            zoneMix.Children.Clear(); tranches.Clear();
            if (!e.Lance)
            {
                zoneMix.Children.Add(Studio.T(Langue.T(e.Installe ? "Lance Voicemeeter pour piloter ses entrées et ses bus d'ici." : "Voicemeeter n'est pas installé : la table de mixage pilote Voicemeeter (gratuit, VB-Audio)."), 12.5, Studio.Discret));
                return;
            }
            zoneMix.Children.Add(Studio.T(Langue.F("{0} : volume, mute et envois de chaque entrée vers les bus. Chaque réglage s'annule dans l'Historique. Double-clic sur un fader : 0 dB.", e.Nom), 12, Studio.Faible));
            var rang = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var routes = Voicemeeter.Routes(e.Type);
            var nomsBus = e.Bus.Select(b => b.Nom).ToArray();
            var ent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            rang.Children.Add(new StackPanel { Spacing = 6, Children = { Etiquette(Langue.T("Entrées")), ent } });
            foreach (var x in e.Entrees)
            {
                string nom = x.Label != "" ? x.Label : x.Virtuelle ? x.Nom : Langue.F("Entrée {0}", x.Index + 1);
                string sous = x.Label != "" ? (x.Virtuelle ? x.Nom : Langue.F("Entrée {0}", x.Index + 1)) : (x.Virtuelle ? Langue.T("virtuelle") : Langue.T("physique"));
                var t = new Tranche { Prefixe = "Strip[" + x.Index + "]", Index = x.Index, Muet = x.Muet, EtatEnvois = (bool[])x.Envois.Clone() };
                var bloc = CreerTranche(t, nom, sous, x.Virtuelle ? Studio.Cyan : Studio.Ambre, x.Gain, x.G, x.D);
                ent.Children.Add(bloc);
                var envois = new WrapPanel { MaxWidth = 92, HorizontalAlignment = HorizontalAlignment.Center };
                for (int k = 0; k < routes.Length && k < nomsBus.Length; k++)
                {
                    int kk = k; var tt = t;
                    var p = PetitePuce(nomsBus[k], () => { tt.EtatEnvois[kk] = !tt.EtatEnvois[kk]; Marquer(tt.Envois[kk].Item2, tt.EtatEnvois[kk], Studio.Cyan); Planifier(tt.Prefixe + "." + routes[kk], tt.EtatEnvois[kk] ? 1 : 0); });
                    AutomationProperties.SetName(p, Langue.F("Envoyer « {0} » vers le bus {1}", nom, nomsBus[k]));
                    Marquer(p, k < x.Envois.Length && x.Envois[k], Studio.Cyan);
                    t.Envois.Add(Tuple.Create(routes[k], p));
                    envois.Children.Add(p);
                }
                ((StackPanel)bloc.Child).Children.Add(envois);
                tranches.Add(t);
            }
            rang.Children.Add(new Border { Width = 1, Background = Studio.P(Studio.Bord), Margin = new Thickness(6, 22, 6, 0) });
            var sorties = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            rang.Children.Add(new StackPanel { Spacing = 6, Children = { Etiquette(Langue.T("Bus")), sorties } });
            foreach (var b in e.Bus)
            {
                string nom = b.Label != "" ? b.Label : b.Nom;
                string sous = b.Label != "" ? b.Nom + (b.Appareil != "" ? " · " + b.Appareil : "") : (b.Appareil != "" ? b.Appareil : Langue.T(b.Physique ? "physique" : "virtuel"));
                var t = new Tranche { Prefixe = "Bus[" + b.Index + "]", Index = b.Index, EstBus = true, Muet = b.Muet };
                sorties.Children.Add(CreerTranche(t, nom, sous, b.Physique ? Studio.Ambre : Studio.Cyan, b.Gain, b.G, b.D));
                tranches.Add(t);
            }
            zoneMix.Children.Add(new ScrollViewer { Content = rang, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        }

        private static Control Etiquette(string texte)
        {
            return new TextBlock { Text = texte.ToUpper(Langue.Culture), FontFamily = Studio.Chiffres, FontSize = 10.5, LetterSpacing = 2, Foreground = Studio.P(Studio.Faible), Margin = new Thickness(4, 0, 0, 0) };
        }

        private Border CreerTranche(Tranche t, string nom, string sous, Color couleur, float gain, float g, float d)
        {
            var pile = new StackPanel { Spacing = 6, Width = 92 };
            pile.Children.Add(new TextBlock { Text = nom, FontFamily = Studio.Titre, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Studio.P(couleur), TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center });
            var ts = new TextBlock { Text = sous, FontFamily = Studio.Corps, FontSize = 10.5, Foreground = Studio.P(Studio.Faible), TextTrimming = TextTrimming.CharacterEllipsis, HorizontalAlignment = HorizontalAlignment.Center };
            ToolTip.SetTip(ts, sous);
            pile.Children.Add(ts);
            t.Vu = new VuMetre5 { Height = 10, MinWidth = 70, Width = 76, HorizontalAlignment = HorizontalAlignment.Center };
            if (exemple) t.Vu.Definir(g, d);
            pile.Children.Add(t.Vu);
            t.Fader = new Fader5(-60, 12, 0.5, gain) { Height = 150, HorizontalAlignment = HorizontalAlignment.Center };
            AutomationProperties.SetName(t.Fader, Langue.F("Volume de « {0} »", nom));
            t.TGain = new TextBlock { Text = TexteGain(t.Fader.Valeur), FontFamily = Studio.Chiffres, FontSize = 11.5, Foreground = Studio.P(Studio.Texte), HorizontalAlignment = HorizontalAlignment.Center };
            t.Fader.Change += v => { t.TGain.Text = TexteGain(v); Planifier(t.Prefixe + ".Gain", (float)v); };
            pile.Children.Add(t.Fader); pile.Children.Add(t.TGain);
            t.BMute = PetitePuce("MUTE", () => { t.Muet = !t.Muet; Marquer(t.BMute, t.Muet, Studio.Rouge); Planifier(t.Prefixe + ".Mute", t.Muet ? 1 : 0); });
            t.BMute.HorizontalAlignment = HorizontalAlignment.Center;
            AutomationProperties.SetName(t.BMute, Langue.F("Couper « {0} »", nom));
            Marquer(t.BMute, t.Muet, Studio.Rouge);
            pile.Children.Add(t.BMute);
            return new Border { Child = pile, Padding = new Thickness(6, 10), CornerRadius = new CornerRadius(12), Background = Studio.P(Studio.Violet, 10), BorderBrush = Studio.P(Studio.Bord, 160), BorderThickness = new Thickness(1) };
        }

        private Border PetitePuce(string texte, Action clic)
        {
            var b = Puce(texte, clic);
            b.Padding = new Thickness(7, 3); b.Margin = new Thickness(2); b.CornerRadius = new CornerRadius(8);
            if (b.Child is TextBlock tb) tb.FontSize = 10.5;
            return b;
        }

        /// <summary>Un réglage de la table : envoyé à Voicemeeter après un court instant sans autre changement (un fader
        /// qu'on glisse n'envoie pas chaque pas).</summary>
        private void Planifier(string parametre, float valeur)
        {
            if (exemple) return;
            if (simulation) { F.Statut(Langue.T("Simulation : Voicemeeter serait réglé, rien n'est modifié."), true); signatureMix = null; return; }
            mixEnAttente[parametre] = valeur;
            mixLocal = DateTime.Now;
            if (delaiMix == null)
            {
                delaiMix = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(40) };
                delaiMix.Tick += (o, a) => { delaiMix.Stop(); EnvoyerMixage(); };
            }
            // pendant le geste : envoyé tout de suite, puis au plus toutes les 40 ms (Voicemeeter suit le fader en direct)
            if (!envoiMix && (DateTime.Now - dernierEnvoiMix).TotalMilliseconds >= 40) EnvoyerMixage();
            else if (!delaiMix.IsEnabled) delaiMix.Start();
        }

        private static bool envoiMix;
        private static DateTime dernierEnvoiMix = DateTime.MinValue;

        private void EnvoyerMixage()
        {
            if (mixEnAttente.Count == 0) return;
            if (envoiMix) { if (!delaiMix.IsEnabled) delaiMix.Start(); return; }   // un envoi à la fois
            var envoi = new Dictionary<string, float>(mixEnAttente);
            mixEnAttente.Clear();
            envoiMix = true; dernierEnvoiMix = DateTime.Now;
            mixLocal = DateTime.Now;
            Task.Run(() => Voicemeeter.Mixer(envoi)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                envoiMix = false; mixLocal = DateTime.Now;
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                if (err != "") { F.Statut(Langue.T(err), true); mixLocal = DateTime.MinValue; }
                if (mixEnAttente.Count > 0 && delaiMix != null && !delaiMix.IsEnabled) delaiMix.Start();
            }, DispatcherPriority.Input));
        }

        // ---------------------------------------------------------------- changements

        /// <summary>Un réglage vient de changer : l'affichage suit tout de suite, l'écriture attend un court instant sans
        /// nouveau changement (une molette qu'on tourne ne réécrit pas le fichier à chaque pas).</summary>
        private void Changer(string message = null, bool toujours = false, bool toutDeSuite = false)
        {
            MettreAJourBientot();
            if (exemple) return;
            if (simulation) { F.Statut(Langue.T("Simulation : le fichier d'Equalizer APO serait réécrit, rien n'est modifié."), true); return; }
            version++;
            attente = true; attenteToujours |= toujours;
            if (message != null) attenteMessage = message;
            delai.Stop();
            // pendant qu'on tourne une molette ou glisse un fader : écrit tout de suite, puis au plus toutes les 150 ms
            // (on entend l'effet en direct) ; la dernière valeur part quand on s'arrête
            if (toutDeSuite || (!enEcriture && (DateTime.Now - derniereEcriture).TotalMilliseconds >= 150)) Appliquer(); else delai.Start();
        }

        private static DateTime derniereEcriture = DateTime.MinValue;
        private bool majPrevue;

        /// <summary>Affichage remis à jour une seule fois pour toute une rafale de réglages (une image), pas à chaque pas.</summary>
        private void MettreAJourBientot()
        {
            if (majPrevue) return;
            majPrevue = true;
            Dispatcher.UIThread.Post(() => { majPrevue = false; MettreAJour(); }, DispatcherPriority.Render);
        }

        private void Appliquer()
        {
            delai.Stop();
            if (!attente) return;
            attente = false;
            derniereEcriture = DateTime.Now;
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
                Changer(Langue.T(voulu ? "Égaliseur actif." : "Égaliseur coupé.") + (auto ? " " + Langue.T("Auto jeux est désactivé : c'est toi qui pilotes l'interrupteur.") : ""), false, true);
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
                if (voulu) AudioJeu.SuivreJeu(r, v != null ? v.Jeu : null, r.Actif); else r.JeuEnCours = "";
                string m = !voulu ? "Auto jeux désactivé : l'égaliseur reste comme il est."
                    : r.Actif ? "Auto jeux activé : le jeu tourne, l'égaliseur est actif." : "Auto jeux activé : l'égaliseur se mettra en marche au lancement d'un jeu.";
                Changer(Langue.T(m), voulu, true);
            };
            if (voulu) AvecAccord(faire); else faire();
        }

        private void BasculerProfilJeu(bool voulu)
        {
            if (exemple) return;
            r.ProfilParJeu = voulu; signatureAuto = null;
            r.JeuEnCours = "";   // le profil du jeu en cours sera chargé au prochain relevé (s'il tourne)
            Changer(Langue.T(voulu ? "Profil adapté à chaque jeu : chargé au lancement de chaque jeu reconnu." : "Profil adapté à chaque jeu désactivé : ton profil reste le même pour tous les jeux."), false, true);
        }

        private void ChoisirJeu(JeuAudio j)
        {
            if (exemple) return;
            r.Gains = (double[])j.Gains.Clone(); r.CoupeBas = j.CoupeBas;
            AnnoncerApres(Langue.T(r.Actif ? "Profil appliqué" : "Profil choisi"), j.Nom + (r.Actif ? "" : " · " + Langue.T("égaliseur coupé")));
            Changer(r.Actif ? Langue.F("Profil « {0} » appliqué.", j.Nom) : Langue.F("Profil « {0} » choisi. L'égaliseur est coupé : active-le pour l'entendre.", j.Nom), false, true);
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
            AnnoncerApres(Langue.T(r.Actif ? "Profil appliqué" : "Profil choisi"), nom + (r.Actif ? "" : " · " + Langue.T(r.AutoJeu ? "au lancement du jeu" : "égaliseur coupé")));
            Changer(r.Actif ? Langue.F("Profil « {0} » appliqué.", nom)
                : r.AutoJeu ? Langue.F("Profil « {0} » choisi : il s'appliquera au lancement du jeu.", nom)
                : Langue.F("Profil « {0} » choisi. L'égaliseur est coupé : active-le pour l'entendre.", nom), false, true);
        }

        private void ReglageAutomatique()
        {
            if (exemple) return;
            r.Gains = (double[])AudioJeu.Profils[0].Gains.Clone();
            r.Intensite = 100; r.Cible = "auto"; r.Niveau = 0; r.Balance = 0; r.Largeur = 0; r.CoupeBas = 0; r.Inversion = false;
            AnnoncerApres(Langue.T("Réglage automatique appliqué"), Langue.T("Pas / compétitif") + " · 100 %");
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
            if (moteur == "apo" && !(Releve?.Etat.Present ?? true)) { InstallerApo(Releve?.NomDefaut); return; }
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

        // ---- installation d'Equalizer APO par Velox
        private static bool installationApo;

        /// <summary>
        /// Télécharge l'installateur officiel d'Equalizer APO, le vérifie et l'installe (autorisation de Windows). À la fin,
        /// la fenêtre d'Equalizer APO demande de cocher la sortie : Velox dit laquelle (la sortie par défaut).
        /// </summary>
        private void InstallerApo(string sortie)
        {
            if (exemple || installationApo) return;
            if (simulation) { F.Statut(Langue.T("Simulation : Equalizer APO serait téléchargé et installé, rien n'est modifié."), true); return; }
            if (!OperatingSystem.IsWindows()) return;
            string nomSortie = string.IsNullOrEmpty(sortie) ? Langue.T("ton casque ou tes haut-parleurs") : sortie;
            F.Demander(Langue.T("Installer Equalizer APO"),
                Langue.F("Velox va télécharger Equalizer APO {0} (logiciel libre, environ 12 Mo) depuis son site officiel, vérifier que c'est bien le fichier officiel, puis l'installer. Windows te demandera l'autorisation.", InstallationApo.Version) + "\n\n"
                + Langue.F("Ensuite, une fenêtre d'Equalizer APO s'ouvre : coche « {0} », puis clique sur OK. Il relance le son de Windows et vérifie que tout marche.", nomSortie) + "\n\n"
                + Langue.T("Désinstallable à tout moment dans Paramètres > Applications."),
                Langue.T("Installer"), Langue.T("Annuler"), oui =>
                {
                    if (!oui) return;
                    installationApo = true; page?.MettreAJour();
                    F.Statut(Langue.T("Téléchargement d'Equalizer APO…"));
                    var f = F;
                    Task.Run(() =>
                    {
                        string fichier = InstallationApo.Telecharger(p => Dispatcher.UIThread.Post(() => f.Statut(p >= 0 ? Langue.F("Téléchargement d'Equalizer APO… {0} %", p) : Langue.T("Téléchargement d'Equalizer APO…"))), CancellationToken.None);
                        Dispatcher.UIThread.Post(() => f.Statut(Langue.F("Installation d'Equalizer APO : accepte l'autorisation de Windows, puis coche « {0} » dans sa fenêtre.", nomSortie)));
                        return InstallationApo.Installer(fichier);
                    }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                    {
                        installationApo = false;
                        string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                        if (err != "")
                        {
                            f.Statut(Langue.T(err), true);
                            if (!err.StartsWith("Installation annulée"))
                                f.Demander(Langue.T("Equalizer APO n'a pas pu être installé"), Langue.T(err) + "\n\n" + Langue.T("Réessaie dans un instant (même bouton), ou télécharge-le toi-même depuis son site officiel : une fois installé, Velox le reconnaît tout seul."),
                                    Langue.T("Ouvrir le site officiel"), Langue.T("Fermer"), oui => { if (oui) Programmes.Ouvrir(AudioJeu.Site); });
                        }
                        else f.Statut(Langue.T("Equalizer APO est installé. Si tu n'entends aucune différence une fois l'égaliseur allumé, redémarre le PC."));
                        signatureSorties = null;
                        Relever();
                        page?.MettreAJour();
                    }));
                });
        }

        /// <summary>Un clic sur « Equalizer APO » (témoin du haut, ligne d'une sortie) : absent, Velox l'installe ; présent, sa
        /// fenêtre s'ouvre pour cocher la sortie voulue.</summary>
        private void ApoClique(ReleveAudioJeu v, string sortie = null)
        {
            if (exemple || installationApo) return;
            if (v == null || !v.Etat.Present) { InstallerApo(sortie ?? v?.NomDefaut); return; }
            F.Demander(Langue.T("Ajouter Equalizer APO à une sortie"),
                Langue.F("La fenêtre d'Equalizer APO va s'ouvrir. Coche « {0} », clique sur OK, et redémarre le PC si elle te le demande.", !string.IsNullOrEmpty(sortie) ? sortie : !string.IsNullOrEmpty(v.NomDefaut) ? v.NomDefaut : Langue.T("ton casque")),
                Langue.T("Ouvrir"), Langue.T("Annuler"), oui => { if (oui) OuvrirConfigurator(); });
        }

        private void OuvrirConfigurator()
        {
            if (exemple) return;
            string c = null;
            try { c = AudioJeu.Configurator; } catch { }
            if (c == null) { F.Statut(Langue.T("Le Configurator d'Equalizer APO est introuvable sur ce PC."), true); return; }
            // ouverte par Velox avec l'autorisation administrateur, et le chemin exact de ses modules Qt (sinon, sur certains
            // PC, elle refuse de s'ouvrir : « no Qt platform plugin could be initialized »)
            var f = F;
            f.Statut(Langue.T("Ouverture de la fenêtre d'Equalizer APO : accepte l'autorisation de Windows."));
            Task.Run(() => Elevation.CommandeAttendre(60, "selecteur-apo")).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                int code = t.IsFaulted ? 5 : t.Result;
                if (code == 0) f.Statut(Langue.T("Fenêtre d'Equalizer APO ouverte : coche ta sortie, valide, puis redémarre le PC si elle le demande."));
                else if (code == -1) f.Statut(Langue.T("Fenêtre d'Equalizer APO pas ouverte : Windows n'a pas eu l'autorisation administrateur."), true);
                else f.Statut(Langue.T("La fenêtre d'Equalizer APO n'a pas pu s'ouvrir : réinstalle Equalizer APO (témoin « EQUALIZER APO » en haut)."), true);
            }));
        }
    }

    /// <summary>
    /// Petite bulle en haut de l'écran pendant deux secondes, après un raccourci clavier (« ÉGALISEUR ACTIF · Compétitif ») :
    /// toujours au premier plan, elle laisse passer les clics et ne prend jamais le focus (même principe que l'overlay).
    /// Visible dans les jeux en « fenêtré plein écran » ; un jeu en plein écran exclusif passe par-dessus (les bips restent).
    /// </summary>
    internal static class BulleAudio
    {
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);
        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TRANSPARENT = 0x20, WS_EX_LAYERED = 0x80000, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x8000000;
        private static Window fenetre;
        private static DispatcherTimer minuteur;

        public static void Montrer(string titre, string detail, bool actif)
        {
            if (!OperatingSystem.IsWindows()) return;
            try
            {
                if (fenetre != null) fenetre.Close();
                var point = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = Studio.P(actif ? Studio.Vert : Studio.Faible), VerticalAlignment = VerticalAlignment.Center };
                var ligne = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
                ligne.Children.Add(point);
                ligne.Children.Add(new TextBlock { Text = titre, FontFamily = Studio.Titre, FontSize = 16, FontWeight = FontWeight.Bold, Foreground = Studio.P(Studio.Texte), VerticalAlignment = VerticalAlignment.Center });
                if (!string.IsNullOrEmpty(detail)) ligne.Children.Add(new TextBlock { Text = "· " + detail, FontFamily = Studio.Corps, FontSize = 14, Foreground = Studio.P(Studio.Ambre), VerticalAlignment = VerticalAlignment.Center });
                var cadre = new Border { Child = ligne, Padding = new Thickness(18, 10), CornerRadius = new CornerRadius(20), Background = new SolidColorBrush(Color.FromArgb(225, 18, 10, 32)), BorderBrush = Studio.Degrade(0, Studio.Ambre, Studio.Magenta), BorderThickness = new Thickness(1.5) };
                var w = new Window
                {
                    SizeToContent = SizeToContent.WidthAndHeight, SystemDecorations = SystemDecorations.None, Topmost = true, CanResize = false, ShowInTaskbar = false, ShowActivated = false,
                    Background = Brushes.Transparent, TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent }, Title = "Velox audio", Content = cadre, Focusable = false,
                };
                w.Opened += (s, e) =>
                {
                    try
                    {
                        Placer(w);
                        Dispatcher.UIThread.Post(() => { try { if (fenetre == w) Placer(w); } catch { } }, DispatcherPriority.Background);
                        var h = w.TryGetPlatformHandle();
                        if (h != null) SetWindowLongPtr(h.Handle, GWL_EXSTYLE, new IntPtr(GetWindowLongPtr(h.Handle, GWL_EXSTYLE).ToInt64() | WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE));
                    }
                    catch (Exception ex) { JournalDemarrage.Erreur("bulle de l'audio", ex); }
                };
                w.Closed += (s, e) => { if (fenetre == w) fenetre = null; };
                fenetre = w;
                w.Show();
                if (minuteur != null) minuteur.Stop();
                minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                minuteur.Tick += (s, e) => { minuteur.Stop(); if (fenetre == w) w.Close(); };
                minuteur.Start();
            }
            catch (Exception ex) { JournalDemarrage.Erreur("bulle de l'audio", ex); }
        }

        /// <summary>En haut, au centre de l'écran principal (zone de travail).</summary>
        private static void Placer(Window w)
        {
            var ecran = w.Screens.Primary;
            if (ecran == null) return;
            var z = ecran.WorkingArea;
            int l = (int)Math.Ceiling(w.Bounds.Width * ecran.Scaling);
            w.Position = new PixelPoint(z.X + (z.Width - l) / 2, z.Y + (int)(48 * ecran.Scaling));
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
