// Velox 5.13 : fenêtre de l'Autopilote du mode jeu (Premium). Il essaie des réglages sur tes vraies parties,
// garde ceux qui marchent sur ton PC et annule les autres, avec la preuve mesurée par la Boîte noire.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal static class Autopilote5
    {
        /// <summary>L'Autopilote est activé (copie du réglage, lue une fois : la Boîte noire la consulte chaque seconde).</summary>
        public static bool Actif;
        /// <summary>(captures) données d'exemple : rien n'est lu.</summary>
        public static List<JeuAutopilote> Exemple;

        public static void Demarrer(FenetrePrincipale f)
        {
            try { Actif = Parametres.Lire().Autopilote && Licence.Premium; } catch { Actif = false; }
            Task.Run(() => Autopilote.RetablirApresArret());
            Autopilote.Verdict = (jeu, e) => Dispatcher.UIThread.Post(() =>
            {
                try { f.Statut(Langue.T("Autopilote") + " · " + BoiteNoire.NomJeu(jeu) + " : " + Langue.T(NomReglage(e.Reglage)) + " → " + Langue.T(Resume(e))); }
                catch { }
            });
        }

        public static string NomReglage(string r)
        {
            switch (r)
            {
                case "retrait": return "Programme gênant mis en retrait";
                case "priorite": return "Jeu en priorité haute";
                case "alimentation": return "Mode « Performances élevées »";
            }
            return r;
        }

        private static string Explication(string r, string cible)
        {
            switch (r)
            {
                case "retrait": return Langue.F("{0} a fait saccader ce jeu d'après la Boîte noire : pendant la partie, il passe en priorité « inférieure ». Il reste ouvert et fonctionne normalement.", Langue.T(ActiviteProgrammes.NomLisible(cible)));
                case "priorite": return Langue.T("Windows donne la priorité au jeu sur les autres programmes. Jamais essayé si un anti-triche tourne.");
                case "alimentation": return Langue.T("Le processeur reste à pleine vitesse pendant le jeu. Essayé seulement si ton PC n'utilise pas déjà ce mode, et hors mode jeu.");
            }
            return "";
        }

        /// <summary>Verdict court d'un essai (non traduit, sauf les nombres).</summary>
        public static string Resume(EssaiAutopilote e)
        {
            switch (e.Etat)
            {
                case "garde": return "gardé";
                case "annule": return e.Raison == "pire" ? "annulé : moins bien avec" : "annulé : aucun effet mesurable";
                case "impossible": return "impossible sur ce jeu";
                case "test": return "en test";
            }
            return "en attente";
        }

        private static string Pourcent(double v) { return (v >= 0 ? "+" : "−") + Math.Abs(Math.Round(v * 100)).ToString("0", Langue.Culture) + " %"; }

        public static void Ouvrir(FenetrePrincipale f)
        {
            var contenu = new StackPanel { Spacing = 12 };
            Remplir(f, contenu);
            f.Ouvrir("Autopilote du mode jeu", contenu, 860);
        }

        private static void Remplir(FenetrePrincipale f, StackPanel s)
        {
            var P = Habillage.P;
            s.Children.Clear();
            s.Children.Add(Premium5.Intro("Les optimiseurs promettent des FPS sans jamais le vérifier. L'Autopilote fait l'inverse : il essaie chaque réglage sur tes vraies parties, par blocs de 3 minutes avec et sans, mesure le FPS minimum et les saccades, puis garde seulement ce qui marche vraiment sur TON PC. Rien n'est jamais fermé, tout est rétabli à la fin de la partie, et l'antivirus, le pare-feu et Windows Update ne sont jamais touchés."));

            bool exemple = Exemple != null;
            bool actif = exemple || Actif;
            var etat = new TextBlock { Text = "● " + Langue.T(actif ? "Autopilote activé" : "Autopilote désactivé"), FontFamily = Habillage.Affichage, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(actif ? P.Vert : P.Discret) };
            s.Children.Add(etat);
            if (actif && !exemple)
            {
                bool avec; string r = Autopilote.Etat(out avec);
                string ligne = !Fps.DirectActif ? Langue.T("Les FPS en direct sont arrêtés : l'Autopilote attend qu'ils tournent pour mesurer.")
                    : r != "" ? Langue.F("En ce moment : essai « {0} », bloc {1}.", Langue.T(NomReglage(r)), Langue.T(avec ? "avec" : "sans"))
                    : Langue.T("Les FPS en direct tournent : lance un jeu, l'Autopilote commence après une minute de jeu.");
                s.Children.Add(new TextBlock { Text = ligne, FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
            }

            var boutons = new WrapPanel();
            if (!exemple)
            {
                var basculer = new BoutonNoyau(Actif ? "Désactiver l'Autopilote" : "Activer l'Autopilote", !Actif, 0, 42) { TaillePolice = 14, Capitales = !Actif, Margin = new Thickness(0, 0, 8, 8) };
                basculer.Clic += () => Basculer(f, () => Remplir(f, s));
                boutons.Children.Add(basculer);
                if (!Fps.DirectActif)
                {
                    var fps = new BoutonNoyau("Démarrer les FPS en direct", false, 0, 42) { TaillePolice = 14, Margin = new Thickness(0, 0, 8, 8) };
                    fps.Clic += () => Overlay5.BasculerFps(f, ok => Remplir(f, s));
                    boutons.Children.Add(fps);
                }
            }
            else boutons.Children.Add(new BoutonNoyau("Désactiver l'Autopilote", false, 0, 42) { TaillePolice = 14, Margin = new Thickness(0, 0, 8, 8) });
            s.Children.Add(boutons);

            var jeux = exemple ? Exemple : Autopilote.Charger();
            if (jeux.Count == 0)
            {
                s.Children.Add(new TextBlock { Text = Langue.T("Aucun jeu essayé pour l'instant. Compte environ 40 minutes de jeu par réglage avant un verdict : deux moments d'une partie ne se ressemblent jamais, il faut plusieurs blocs pour que la mesure soit fiable."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                return;
            }

            foreach (var j in jeux)
            {
                Autopilote.Preparer(j);
                var bloc = new StackPanel { Spacing = 8 };
                bloc.Children.Add(new TextBlock { Text = BoiteNoire.NomJeu(j.Jeu), FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
                foreach (var e in j.Essais)
                {
                    var ligne = new StackPanel { Spacing = 2 };
                    var haut = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                    haut.Children.Add(new TextBlock { Text = Langue.T(NomReglage(e.Reglage)), FontFamily = Habillage.Corps, FontSize = 14, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                    Color couleur = e.Etat == "garde" ? P.Vert : e.Etat == "test" ? P.Accent : e.Etat == "annule" && e.Raison == "pire" ? P.Orange : P.Discret;
                    var verdict = new TextBlock { Text = Langue.T(Resume(e)).ToUpper(Langue.Culture), FontFamily = Habillage.Console, FontSize = 11.5, LetterSpacing = 1, Foreground = Habillage.Pinceau(couleur), Margin = new Thickness(12, 2, 0, 0) };
                    Grid.SetColumn(verdict, 1); haut.Children.Add(verdict);
                    ligne.Children.Add(haut);
                    string detail;
                    if (e.Etat == "garde" || e.Etat == "annule")
                        detail = Langue.F("FPS minimum {0}, saccades {1}, mesuré sur {2} blocs.", Pourcent(e.Gain1), Pourcent(-e.GainSaccades), e.Blocs.Count);
                    else if (e.Etat == "test")
                        detail = Langue.F("Mesures : {0}/{2} blocs avec, {1}/{2} sans.", Math.Min(e.NbAvec, Autopilote.BlocsMin), Math.Min(e.NbSans, Autopilote.BlocsMin), Autopilote.BlocsMin);
                    else if (e.Etat == "impossible")
                        detail = Langue.T("Windows ou l'anti-triche du jeu a refusé la modification : rien n'a été changé.");
                    else detail = Explication(e.Reglage, j.Cible);
                    ligne.Children.Add(new TextBlock { Text = detail, FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                    bloc.Children.Add(ligne);
                }
                if (j.Essais.Count == 0)
                    bloc.Children.Add(new TextBlock { Text = Langue.T("Rien à essayer sur ce jeu : ton PC est déjà réglé au mieux pour lui."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                var refaire = new BoutonNoyau("Recommencer les essais", false, 0, 32) { TaillePolice = 12, HorizontalAlignment = HorizontalAlignment.Left };
                ToolTip.SetTip(refaire, Langue.T("Utile après une mise à jour du jeu ou des pilotes : les verdicts de ce jeu sont oubliés et les essais reprennent à la prochaine partie."));
                if (!exemple)
                {
                    string nom = j.Jeu;
                    refaire.Clic += () => f.Demander("Recommencer les essais ?", Langue.F("Les verdicts de {0} seront oubliés et les essais reprendront à la prochaine partie.", BoiteNoire.NomJeu(nom)), "Recommencer", "Annuler", oui =>
                    {
                        if (!oui) return;
                        Task.Run(() => Autopilote.Recommencer(nom)).ContinueWith(t => Dispatcher.UIThread.Post(() => Remplir(f, s)));
                    });
                }
                bloc.Children.Add(refaire);
                s.Children.Add(new Border { Child = bloc, Padding = new Thickness(14, 12), CornerRadius = new CornerRadius(4), BorderThickness = new Thickness(1), BorderBrush = Habillage.Pinceau(P.Bordure), Background = Habillage.Pinceau(P.Accent, P.Sombre ? 8 : 6) });
            }
        }

        /// <summary>Active (Premium, avec confirmation) ou désactive l'Autopilote.</summary>
        public static void Basculer(FenetrePrincipale f, Action fini)
        {
            if (Actif)
            {
                Actif = false;
                f.EnregistrerReglage(x => x.Autopilote = false);
                Task.Run(() => Autopilote.Terminer());
                f.Statut(Langue.T("Autopilote désactivé : tout ce qu'il avait réglé est rétabli."));
                if (fini != null) fini();
                return;
            }
            if (!Licence.Premium) { Achat5.Ouvrir(f); return; }
            f.Demander("Activer l'Autopilote ?", "Pendant tes parties (avec les FPS en direct), Velox essaiera un réglage à la fois, par blocs de 3 minutes : mettre en retrait le programme qui fait saccader le jeu, passer le jeu en priorité haute, ou le mode « Performances élevées ». Rien n'est fermé, tout est rétabli à la fin de la partie, et seuls les réglages qui ont fait leurs preuves sur ton PC sont gardés. Tu peux le désactiver à tout moment.", "Activer", "Annuler", oui =>
            {
                if (!oui) return;
                Actif = true;
                f.EnregistrerReglage(x => x.Autopilote = true);
                f.Statut(Langue.T("Autopilote activé : il commencera ses essais à ta prochaine partie avec les FPS en direct."));
                if (fini != null) fini();
                if (!Fps.DirectActif) Overlay5.BasculerFps(f, ok => { if (fini != null) fini(); });
            });
        }
    }
}
