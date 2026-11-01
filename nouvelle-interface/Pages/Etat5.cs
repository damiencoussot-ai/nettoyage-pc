// Nettoyage PC 5 - « Ce qui est activé » : sur l'accueil, une pastille par fonction automatique (verte = activée,
// grise = désactivée, orange = à vérifier) ; la fenêtre « Ce qui est activé » détaille chaque fonction, vérifie que
// l'application fonctionne bien, et propose la correction quand quelque chose cloche (toujours sur ton clic).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal static class Etat5
    {
        /// <summary>(captures) relevé d'exemple : rien n'est lu sur le PC.</summary>
        public static List<EtatFonction> Exemple;
        private static List<EtatFonction> dernier;
        private static DateTime dateDernier;
        private static bool enCours;
        private static event Action Pret;

        /// <summary>Relevé en arrière-plan (au plus un à la fois) ; « Pret » prévient les affichages.</summary>
        public static void Actualiser(FenetrePrincipale f)
        {
            if (Exemple != null) { dernier = Exemple; dateDernier = DateTime.Now; var p0 = Pret; if (p0 != null) p0(); return; }
            if (enCours) return;
            enCours = true;
            Task.Run(() => { try { return EtatFonctions.Collecter(); } catch (Exception ex) { JournalDemarrage.Erreur("état des fonctions", ex); return null; } })
                .ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    enCours = false;
                    if (t.Result != null) { dernier = t.Result; dateDernier = DateTime.Now; }
                    var p = Pret; if (p != null) p();
                }));
        }

        /// <summary>Le mode (réel / simulation) vient de la fenêtre, pas du moteur.</summary>
        private static List<EtatFonction> AvecMode(FenetrePrincipale f)
        {
            var l = new List<EtatFonction>();
            if (dernier == null) return l;
            l.Add(new EtatFonction { Id = "mode", Nom = f.Simulation ? "Mode simulation" : "Mode réel", Groupe = EtatFonctions.Application, Actif = !f.Simulation,
                Detail = Langue.T(f.Simulation ? "Rien n'est modifié : chaque action montre ce qu'elle ferait." : "Chaque action te demande ton accord avant de se lancer.") });
            l.AddRange(dernier);
            return l;
        }

        private static Color Couleur(EtatFonction e) { var P = Habillage.P; return !e.Ok ? P.Orange : e.Actif ? P.Vert : P.Discret; }

        private static string TexteEtat(EtatFonction e)
        {
            if (!e.Ok) return Langue.T("à vérifier");
            if (e.Id == "premium") return Langue.T(Licence.EnEssai ? "essai" : e.Actif ? "actif" : "gratuit");
            return Langue.T(e.Actif ? "activé" : "désactivé");
        }

        // ---------------------------------------------------------------- bande de l'accueil

        public static Control Bande(FenetrePrincipale f)
        {
            var bloc = new StackPanel { Spacing = 10, Margin = new Thickness(12, 0, 0, 0) };
            Action remplir = () =>
            {
                var P = Habillage.P;
                bloc.Children.Clear();
                var l = AvecMode(f);
                var titre = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto") };
                titre.Children.Add(new TextBlock { Text = "●  " + Langue.T("Ce qui est activé"), FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center });
                int problemes = l.Count(e => !e.Ok);
                var resume = new TextBlock
                {
                    Text = dernier == null ? Langue.T("vérification…") : problemes == 0 ? Langue.F("{0} fonction(s) activée(s) · tout fonctionne", l.Count(e => e.Actif && e.Groupe == EtatFonctions.Automatique)) : Langue.F("{0} point(s) à vérifier", problemes),
                    FontFamily = Habillage.Console, FontSize = 12, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Habillage.Pinceau(dernier == null ? P.Discret : problemes == 0 ? P.Vert : P.Orange),
                };
                Grid.SetColumn(resume, 1); titre.Children.Add(resume);
                var voir = new BoutonNoyau(problemes > 0 ? "Voir et corriger" : "Vérifier le fonctionnement", problemes > 0, 210, 28) { TaillePolice = 12 };
                voir.Clic += () => Ouvrir(f);
                Grid.SetColumn(voir, 2); titre.Children.Add(voir);
                bloc.Children.Add(titre);
                var pastilles = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var e in l.Where(x => x.Groupe == EtatFonctions.Automatique || x.Id == "premium" || x.Id == "mode" || !x.Ok)) pastilles.Children.Add(Pastille(f, e));
                bloc.Children.Add(pastilles);
            };
            remplir();
            Action surPret = () => remplir();
            Action surLicence = () => Dispatcher.UIThread.Post(() => Actualiser(f));
            bloc.AttachedToVisualTree += (s, e) =>
            {
                Pret += surPret; Licence.Change += surLicence;
                if (dernier == null || (DateTime.Now - dateDernier).TotalSeconds > 30) Actualiser(f); else remplir();
            };
            bloc.DetachedFromVisualTree += (s, e) => { Pret -= surPret; Licence.Change -= surLicence; };
            return bloc;
        }

        private static Control Pastille(FenetrePrincipale f, EtatFonction e)
        {
            var P = Habillage.P;
            var c = Couleur(e);
            var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            s.Children.Add(new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Habillage.Pinceau(c), VerticalAlignment = VerticalAlignment.Center });
            s.Children.Add(new TextBlock { Text = Langue.T(e.Nom), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(e.Actif || !e.Ok ? P.Texte : P.Discret), VerticalAlignment = VerticalAlignment.Center });
            s.Children.Add(new TextBlock { Text = TexteEtat(e), FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(c), VerticalAlignment = VerticalAlignment.Center });
            var b = new Border
            {
                Child = s, Padding = new Thickness(10, 6), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(14), Cursor = new Cursor(StandardCursorType.Hand),
                Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(e.Ok ? P.Bordure : Habillage.Alpha(P.Orange, 140)), BorderThickness = new Thickness(1),
            };
            ToolTip.SetTip(b, Langue.T(e.Detail) + (e.Ok ? "" : "\n" + e.Probleme));
            b.PointerReleased += (x, a) => Ouvrir(f);
            return b;
        }

        // ---------------------------------------------------------------- fenêtre détaillée

        public static void Ouvrir(FenetrePrincipale f)
        {
            var contenu = new StackPanel { Spacing = 12 };
            FenetreInterieure fen = null;
            Action remplir = null;
            remplir = () =>
            {
                var P = Habillage.P;
                contenu.Children.Clear();
                var l = AvecMode(f);
                var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                int problemes = l.Count(e => !e.Ok);
                haut.Children.Add(new TextBlock
                {
                    Text = dernier == null || enCours ? Langue.T("Vérification en cours…") : problemes == 0 ? "✔  " + Langue.T("Tout fonctionne : chaque fonction activée est en place.") : Langue.F("{0} point(s) à vérifier : la correction est proposée à droite.", problemes),
                    FontFamily = Habillage.Affichage, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Habillage.Pinceau(dernier == null || enCours ? P.Discret : problemes == 0 ? P.Vert : P.Orange),
                });
                var revoir = new BoutonNoyau("Revérifier", false, 130, 32) { TaillePolice = 13, IsEnabled = !enCours, Margin = new Thickness(14, 0, 0, 0) };
                revoir.Clic += () => { Actualiser(f); remplir(); };
                Grid.SetColumn(revoir, 1); haut.Children.Add(revoir);
                contenu.Children.Add(haut);
                if (dernier != null)
                    contenu.Children.Add(new TextBlock { Text = Langue.F("Vérifié à {0}. Rien n'est modifié sans ton clic.", dateDernier.ToString("HH:mm:ss")), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret) });
                foreach (var groupe in new[] { EtatFonctions.Automatique, EtatFonctions.Application })
                {
                    var elements = l.Where(e => e.Groupe == groupe).ToList();
                    if (elements.Count == 0) continue;
                    contenu.Children.Add(new TextBlock { Text = Langue.T(groupe), FontFamily = Habillage.Affichage, FontSize = 15.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), Margin = new Thickness(0, 8, 0, 0) });
                    foreach (var e in elements) contenu.Children.Add(Ligne(f, e, () => { if (fen != null) fen.Fermer(); }));
                }
            };
            remplir();
            Action surPret = () => remplir();
            Pret += surPret;
            fen = f.Ouvrir("Ce qui est activé", contenu, 900);
            fen.Fermee += () => Pret -= surPret;
            if (dernier == null || (DateTime.Now - dateDernier).TotalSeconds > 10) { Actualiser(f); remplir(); }
        }

        private static Control Ligne(FenetrePrincipale f, EtatFonction e, Action fermer)
        {
            var P = Habillage.P;
            var c = Couleur(e);
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("22,*,Auto") };
            g.Children.Add(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(5), Background = Habillage.Pinceau(c), VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) });
            var s = new StackPanel { Spacing = 3 };
            var titre = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            titre.Children.Add(new TextBlock { Text = Langue.T(e.Nom), FontFamily = Habillage.Corps, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            titre.Children.Add(new TextBlock { Text = TexteEtat(e), FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(c), VerticalAlignment = VerticalAlignment.Center });
            if (e.Premium) titre.Children.Add(new TextBlock { Text = "PREMIUM", FontFamily = Habillage.Console, FontSize = 10, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center });
            s.Children.Add(titre);
            s.Children.Add(new TextBlock { Text = Langue.T(e.Detail), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            if (!e.Ok) s.Children.Add(new TextBlock { Text = "⚠  " + e.Probleme, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Orange), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(s, 1); g.Children.Add(s);
            var action = Bouton(f, e, fermer);
            if (action != null) { Grid.SetColumn(action, 2); g.Children.Add(action); }
            return new Border { Child = g, Padding = new Thickness(14, 11), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(e.Ok ? P.Bordure : Habillage.Alpha(P.Orange, 120)), BorderThickness = new Thickness(1) };
        }

        /// <summary>Bouton à droite : la correction s'il y a un problème, sinon « Régler » vers l'endroit qui gère la fonction.</summary>
        private static Control Bouton(FenetrePrincipale f, EtatFonction e, Action fermer)
        {
            string texte = null; Action faire = null; bool principal = false;
            Action<string> outil = cle => { fermer(); f.OuvrirOutil(cle); };
            Action<string, string> page = (rubrique, onglet) => { fermer(); if (onglet != null) f.SousPage(rubrique, onglet); f.Afficher(rubrique, true); };
            switch (e.Reparation)
            {
                case "gardien-demarrer":
                    texte = "Relancer le gardien"; principal = true;
                    faire = () => { try { Gardien.Demarrer(); f.Statut("Le gardien se relance…"); } catch (Exception ex) { f.Statut(ex.Message, true); } DispatcherTimer.RunOnce(() => Actualiser(f), TimeSpan.FromSeconds(3)); };
                    break;
                case "gardien-activer":
                    texte = "Réactiver le gardien"; principal = true;
                    faire = () => { string r = Gardien.Activer(); if (r != "") f.Statut(Langue.T(r), true); else f.Statut("Le gardien est réactivé : il se lancera à chaque ouverture de session."); DispatcherTimer.RunOnce(() => Actualiser(f), TimeSpan.FromSeconds(3)); };
                    break;
                case "gardien": texte = "Régler le gardien"; principal = true; faire = () => outil("gardien"); break;
                case "entretien": texte = "Réactiver"; principal = true; faire = () => { fermer(); f.LancerAction("planifier", () => Actualiser(f)); }; break;
                case "finmodejeu": texte = "Terminer le mode jeu"; principal = true; faire = () => { fermer(); f.LancerAction("finmodejeu", () => Actualiser(f)); }; break;
                case "journal": texte = "Montrer le journal"; faire = () => { if (System.IO.File.Exists(JournalDemarrage.Fichier)) Programmes.MontrerDansExplorateur(JournalDemarrage.Fichier); }; break;
            }
            if (faire == null)
                switch (e.Id)
                {
                    case "premium": texte = Licence.Premium ? "Voir" : "Découvrir"; faire = () => page("Premium", null); break;
                    case "entretien": texte = "Régler"; faire = () => page("Reglages", null); break;
                    case "verifmaj": texte = "Voir"; faire = () => page("MisesAJour", "MisesAJour"); break;
                    case "gardien": case "sauvegardeauto": texte = "Régler"; faire = () => outil("gardien"); break;
                    case "modejeuauto": texte = "Régler"; faire = () => outil("modejeu"); break;
                    case "majappsauto": texte = "Régler"; faire = () => outil("majapps"); break;
                    case "bulletin": texte = "Voir"; faire = () => outil("bulletin"); break;
                    case "appareils": texte = "Voir"; faire = () => outil("reseau"); break;
                    case "modejeu": texte = "Terminer"; faire = () => { fermer(); f.LancerAction("finmodejeu", () => Actualiser(f)); }; break;
                    case "donnees": texte = "Ouvrir"; faire = () => { Chemins.CreerDonnees(); Programmes.Ouvrir(Chemins.Donnees); }; break;
                }
            if (faire == null) return null;
            var b = new BoutonNoyau(texte, principal, 0, 32) { TaillePolice = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            b.Clic += () => { try { faire(); } catch (Exception ex) { JournalDemarrage.Erreur("état : " + e.Id, ex); f.Statut(Langue.T("Une erreur s'est produite :") + " " + ex.Message, true); } };
            return b;
        }
    }
}
