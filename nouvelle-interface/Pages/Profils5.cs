// Velox 5 - profils d'utilisation : « Gamer », « Bureautique », « PC familial ». Un profil règle d'un coup
// les favoris de l'accueil et, avec Premium, ce que le gardien surveille. Tout est montré avant (question),
// rien n'est supprimé, et chaque réglage reste modifiable ensuite à la main.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal static class Profils5
    {
        private sealed class Profil
        {
            public string Cle, Nom, Pour;
            public string[] Favoris;
            public string[] Changements;            // ce qui est réglé, en clair
            public Action<ReglagesGardien> Gardien;   // réglages du gardien (Premium)
        }

        private static readonly Profil[] Liste =
        {
            new Profil
            {
                Cle = "gamer", Nom = "Gamer", Pour = "Pour jouer : performances, mode jeu, FPS.",
                Favoris = new[] { "modejeu", "fps", "ramlib", "reglagesjeux", "majapps", "moniteur" },
                Changements = new[] { "Favoris : Mode jeu, Preuve en FPS, Libérer la RAM, Réglages des jeux, Mettre à jour les applications, Moniteur en direct", "Gardien (Premium) : mode jeu automatique, OneDrive en pause pendant les parties", "Bonus : look néon, QG GAMER sur l'accueil, niveaux et succès" },
                Gardien = r => { r.ModeJeuAuto = true; r.PauseOneDrive = true; r.Temperature = true; },
            },
            new Profil
            {
                Cle = "bureautique", Nom = "Bureautique", Pour = "Pour travailler : un PC propre, à jour et sauvegardé.",
                Favoris = new[] { "rapide", "majapps", "sauvegarde", "secu", "carteespace", "extensions" },
                Changements = new[] { "Favoris : Nettoyage rapide, Mettre à jour les applications, Sauvegarde, Bilan de sécurité, Carte de l'espace disque, Extensions des navigateurs", "Gardien (Premium) : bulletin mensuel et surveillance du disque" },
                Gardien = r => { r.Bulletin = true; r.Disque = true; r.NettoyageAuto = true; },
            },
            new Profil
            {
                Cle = "famille", Nom = "PC familial", Pour = "Pour toute la famille : sécurité, anti-arnaque, aide à distance.",
                Favoris = new[] { "rapide", "secu", "bouclier", "antivirus", "sauvegarde", "aide" },
                Changements = new[] { "Favoris : Nettoyage rapide, Bilan de sécurité, Bouclier anti-arnaque, Analyse antivirus rapide, Sauvegarde, Aide à distance", "Gardien (Premium) : alerte anti-arnaque, appareils inconnus sur la box, antivirus coupé" },
                Gardien = r => { r.Arnaque = true; r.NouveauxAppareils = true; r.Antivirus = true; },
            },
        };

        /// <summary>Barre de menu : « // PROFIL » et les trois profils en pastilles ; un clic pose la question habituelle.</summary>
        public static Control Barre(FenetrePrincipale f)
        {
            var A = Habillage.P.AccentBarre;
            string actuel = "";
            try { actuel = Parametres.Lire().ProfilUsage ?? ""; } catch { }
            var s = new StackPanel { Spacing = 6, Margin = new Thickness(20, 0, 14, 16) };
            s.Children.Add(new TextBlock { Text = (Habillage.Hud ? "// " : "") + Langue.T("PROFIL"), FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(Color.FromRgb(79, 127, 146)), Margin = new Thickness(11, 0, 0, 0) });
            var g = new UniformGrid { Columns = 3 };
            string[][] noms = { new[] { "gamer", "Gamer" }, new[] { "bureautique", "Travail" }, new[] { "famille", "Famille" } };
            foreach (var n in noms)
            {
                var p = Liste.First(x => x.Cle == n[0]);
                bool est = p.Cle == actuel;
                var texte = new TextBlock { Text = Langue.T(n[1]).ToUpper(Langue.Culture), FontFamily = Habillage.Console, FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = est ? Habillage.Pinceau(Habillage.TexteSur(A)) : Habillage.Pinceau(Color.FromRgb(169, 199, 211)) };
                var b = new Border
                {
                    Height = 28, Margin = new Thickness(0, 0, 4, 0), Child = texte, BorderThickness = new Thickness(1),
                    Background = est ? Habillage.Pinceau(A) : Habillage.Pinceau(A, 12), BorderBrush = Habillage.Pinceau(A, est ? 255 : 70),
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                };
                ToolTip.SetTip(b, Langue.T(p.Nom) + " : " + Langue.T(p.Pour) + (est ? "\n" + Langue.T("Profil actuel") : ""));
                b.PointerEntered += (o, e) => { if (!est) b.Background = Habillage.Pinceau(A, 40); };
                b.PointerExited += (o, e) => { if (!est) b.Background = Habillage.Pinceau(A, 12); };
                b.PointerReleased += (o, e) => { if (est) f.Statut(Langue.F("Le profil « {0} » est déjà choisi.", Langue.T(p.Nom))); else Appliquer(f, p, () => { }); };
                g.Children.Add(b);
            }
            s.Children.Add(g);
            return s;
        }

        public static string Actuel()
        {
            try { var p = Liste.FirstOrDefault(x => x.Cle == Parametres.Lire().ProfilUsage); return p != null ? Langue.T(p.Nom) : ""; } catch { return ""; }
        }

        public static void Ouvrir(FenetrePrincipale f)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 14 };
            s.Children.Add(new TextBlock { Text = Langue.T("Un profil règle d'un coup tes favoris et, avec Premium, ce que le gardien surveille. Rien n'est supprimé, et tu peux tout changer ensuite à la main."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            string actuel = "";
            try { actuel = Parametres.Lire().ProfilUsage; } catch { }
            var grille = new UniformGrid { Columns = 3 };
            FenetreInterieure fen = null;
            foreach (var p in Liste)
            {
                var pp = p;
                var c = new StackPanel { Spacing = 8 };
                c.Children.Add(new TextBlock { Text = Langue.T(p.Nom), FontFamily = Habillage.Affichage, FontSize = 18, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Texte) });
                c.Children.Add(new TextBlock { Text = Langue.T(p.Pour), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                foreach (var ch in p.Changements) c.Children.Add(new TextBlock { Text = "▸ " + Langue.T(ch), FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                bool est = p.Cle == actuel;
                var b = new BoutonNoyau(est ? "Profil actuel" : "Choisir ce profil", !est, 0, 36) { TaillePolice = 13, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
                b.Clic += () => Appliquer(f, pp, () => { if (fen != null) fen.Fermer(); });
                c.Children.Add(b);
                grille.Children.Add(new Cadre5 { Child = c, Coupe = 14, Padding = new Thickness(16, 14), Margin = new Thickness(0, 0, 10, 10), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(est ? P.Accent : Habillage.Alpha(P.Accent, 50)), BorderThickness = new Thickness(1), TraitHaut = est ? P.Accent : (Color?)null });
            }
            s.Children.Add(grille);
            fen = f.Ouvrir("Profil d'utilisation", s, 1000);
        }

        private static void Appliquer(FenetrePrincipale f, Profil p, Action fermer)
        {
            string texte = string.Join("\n", p.Changements.Select(c => "•  " + Langue.T(c)));
            if (!Licence.Premium) texte += "\n\n" + Langue.T("Sans Premium, seuls les favoris changent.");
            f.Demander(Langue.F("Choisir le profil « {0} » ?", Langue.T(p.Nom)), texte, "Choisir", "Annuler", oui =>
            {
                if (!oui) return;
                Favoris5.Remplacer(f, p.Favoris);
                if (Licence.Premium && !f.Source.Immediat)
                {
                    try { var r = ReglagesGardien.Lire(); p.Gardien(r); r.Ecrire(); Gardien.Recharger(); }
                    catch (Exception ex) { JournalDemarrage.Erreur("profil " + p.Cle, ex); }
                }
                f.EnregistrerReglage(r => r.ProfilUsage = p.Cle);
                fermer();
                // profil Gamer : look néon, ligne RGB, QG GAMER, niveaux et succès (l'interface est reconstruite)
                bool etaitGamer = Habillage.Gamer;
                Habillage.Gamer = p.Cle == "gamer";
                // l'interface est reconstruite : profil actuel en surbrillance dans la barre de gauche, look Gamer ou non
                if (etaitGamer != Habillage.Gamer) Habillage.ChoisirCouleur(Habillage.CouleurActuelle);
                f.ChangerTheme(Habillage.P.Sombre);
                f.Statut(Habillage.Gamer && !etaitGamer ? Langue.T("Mode Gamer activé : look néon, QG GAMER sur l'accueil, niveaux et succès. GG !")
                    : Langue.F("Profil « {0} » appliqué : tes favoris sont sur l'accueil.", Langue.T(p.Nom)));
            });
        }
    }
}
