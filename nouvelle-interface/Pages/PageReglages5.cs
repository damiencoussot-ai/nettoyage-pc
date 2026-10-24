// Nettoyage PC 5 - réglages : profil (pseudo), apparence, langue, entretien automatique, raccourci, à propos,
// informations légales. Mêmes réglages que la version 4 (même fichier parametres.json).
using System;
using System.Diagnostics;
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
    internal static class PageReglages5
    {
        /// <summary>Entretien automatique programmé ? (null : pas encore lu ; remis à null après « planifier » ou « déplanifier »)</summary>
        public static bool? EntretienActif;

        public static Control Creer(FenetrePrincipale f)
        {
            var pile = new StackPanel { Spacing = 16, Margin = new Thickness(28, 6, 16, 24) };
            Parametres r;
            try { r = Parametres.Lire(); } catch { r = new Parametres(); }
            string theme = r.Theme ?? "Sombre";
            string libelleTheme = theme == "Auto" ? "Automatique (comme Windows)" : theme == "Clair" ? "Thème clair" : "Thème sombre";
            var entretien = CarteAction.A("Entretien automatique", "Nettoyage rapide chaque dimanche, optimisation complète toutes les 4 semaines et surveillance des disques, même application fermée.", "Activer", "planifier", true);
            pile.Children.Add(Cartes.Grille(f, new[]
            {
                CarteAction.Code("Mon profil", "Ton pseudo, affiché à l'accueil. Il reste sur ce PC : pas de compte, pas de mot de passe.", "Modifier mon profil", () => Profil5(f), true),
                CarteAction.Code("Apparence", Langue.T("Thème clair, sombre, ou automatique (suit le réglage de Windows). Le bouton soleil / lune en haut bascule aussi en un clic.") + " " + Langue.F("Actuellement : {0}.", Langue.T(libelleTheme)),
                    theme == "Sombre" ? "Passer en clair" : theme == "Clair" ? "Passer en automatique" : "Passer en sombre", () => ChangerTheme(f, theme)),
                entretien,
            }));
            pile.Children.Add(CarteLangue(f, r));
            pile.Children.Add(Cartes.Page(f, null, new[]
            {
                CarteAction.Code("Mode simple", "Pour un débutant (un parent, par exemple) : un seul écran avec trois gros boutons, sans les options avancées.", "Activer le mode simple", () =>
                    f.Demander("Passer au mode simple ?", "L'écran ne montrera plus que trois gros boutons : Nettoyer mon PC, Vérifier mon PC, Appeler à l'aide. Le lien « Afficher toutes les options » en bas de l'écran permet de revenir au mode complet.", "Passer au mode simple", "Annuler", oui => { if (oui) f.AppliquerModeSimple(true); })),
                CarteAction.Code("Informations légales", "Contrat de licence, conditions de vente, politique de confidentialité et mentions légales.", "Consulter", () => Legal5(f)),
                CarteAction.Code("À propos", "Version, éditeur, page officielle et engagements de Nettoyage PC.", "Afficher", () => APropos(f)),
                CarteAction.Code("Raccourci sur le Bureau", "Ajoute « Nettoyage PC » sur ton Bureau pour lancer l'application en un double-clic.", "Créer le raccourci", () => CreerRaccourci(f)).Plus(),
                CarteAction.A("Compatibilité de ce PC", "Version de Windows, matériel, disques, antivirus : ce que Nettoyage PC a détecté et les fonctions disponibles sur ce PC.", "Vérifier", "compat").Plus(),
            }, "Reglages"));
            // entretien : « Activer » ou « Désactiver » selon que les tâches sont programmées (lu une fois en arrière-plan)
            if (EntretienActif == true) { entretien.Bouton = "Désactiver"; entretien.Cible = "action:deplanifier"; entretien.Principal = false; }
            if (EntretienActif == null && !f.Source.Immediat)
                Task.Run(() => { try { return Moteur.TacheExiste(Moteur.TacheHebdo); } catch { return false; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    EntretienActif = t.Result;
                    if (t.Result && f.PageActive == "Reglages") f.Afficher("Reglages", false);
                }));
            return pile;
        }

        private static void ChangerTheme(FenetrePrincipale f, string actuel)
        {
            string suivant = actuel == "Sombre" ? "Clair" : actuel == "Clair" ? "Auto" : "Sombre";
            f.EnregistrerReglage(r => r.Theme = suivant);
            bool sombre = suivant == "Sombre" || (suivant == "Auto" && !Programme.WindowsEnClair());
            f.ChangerTheme(sombre);
            f.Afficher("Reglages", false);
        }

        private static Control CarteLangue(FenetrePrincipale f, Parametres r)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 8 };
            s.Children.Add(new TextBlock { Text = Langue.T("Langue"), FontFamily = Habillage.Affichage, FontSize = 16.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            s.Children.Add(new TextBlock { Text = Langue.T("Langue de l'interface. Les journaux détaillés des actions restent en français."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            var cb = new ComboBox { Width = 260, Margin = new Thickness(0, 6, 0, 0) };
            foreach (var l in Langue.Disponibles) cb.Items.Add(l.Value);
            cb.SelectedIndex = Math.Max(0, Langue.Disponibles.FindIndex(l => l.Key == Langue.Code));
            cb.SelectionChanged += (o, e) =>
            {
                if (cb.SelectedIndex < 0) return;
                string code = Langue.Disponibles[cb.SelectedIndex].Key;
                if (code == (r.Langue ?? "fr")) return;
                r.Langue = code;
                f.EnregistrerReglage(x => x.Langue = code);
                string q = code == "en" ? "The language will change when Nettoyage PC restarts. Restart now?" : code == "es" ? "El idioma cambiará al reiniciar Nettoyage PC. ¿Reiniciar ahora?" : "La langue changera au redémarrage de Nettoyage PC. Redémarrer maintenant ?";
                if (f.Moteur.EnCours) { f.Statut(q.Split('.')[0] + "."); return; }
                f.Demander(q, null, code == "en" ? "Restart" : code == "es" ? "Reiniciar" : "Redémarrer", code == "en" ? "Later" : code == "es" ? "Más tarde" : "Plus tard", oui =>
                {
                    if (!oui) return;
                    try
                    {
                        Process.Start(new ProcessStartInfo(Chemins.Exe, "--relance") { UseShellExecute = false, WorkingDirectory = Chemins.Ici });
                        f.Close();
                    }
                    catch (Exception ex) { f.Statut("Impossible de redémarrer : " + ex.Message, true); }
                });
            };
            s.Children.Add(cb);
            return new Border { Child = s, Padding = new Thickness(20, 18), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 0) };
        }

        /// <summary>Profil : le pseudo (24 caractères au plus), enregistré sur ce PC.</summary>
        private static void Profil5(FenetrePrincipale f)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 10 };
            s.Children.Add(new TextBlock { Text = Langue.T("Comment veux-tu qu'on t'appelle ? (facultatif)"), FontFamily = Habillage.Corps, FontSize = 14, Foreground = Habillage.Pinceau(P.Texte) });
            var tb = new TextBox { Text = f.Pseudo ?? "", MaxLength = 24, Width = 320, HorizontalAlignment = HorizontalAlignment.Left, Watermark = Langue.T("Ton pseudo") };
            s.Children.Add(tb);
            s.Children.Add(new TextBlock { Text = Langue.T("Il s'affiche à l'accueil (« Bonjour, … »). Il reste sur ce PC."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret) });
            var ok = new BoutonNoyau("Enregistrer", true, 140, 36) { TaillePolice = 14, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
            s.Children.Add(ok);
            var w = f.Ouvrir("Mon profil", s, 520);
            ok.Clic += () =>
            {
                string p = Profil.Nettoyer(tb.Text);
                f.Pseudo = p;
                f.EnregistrerReglage(r => { r.Pseudo = p; r.ProfilCree = true; });
                w.Fermer();
                f.Statut(p == "" ? "Pseudo retiré." : Langue.F("Pseudo enregistré : {0}.", p));
                f.Afficher(f.PageActive, false);
            };
        }

        /// <summary>Textes légaux intégrés au programme, un onglet par document.</summary>
        public static void Legal5(FenetrePrincipale f, string premiere = null)
        {
            var P = Habillage.P;
            var texte = new SelectableTextBlock { FontFamily = Habillage.Corps, FontSize = 13, LineHeight = 20, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            var onglets = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(0, 0, 0, 12) };
            var pile = new StackPanel();
            pile.Children.Add(onglets);
            pile.Children.Add(texte);
            Action<string> montrer = null;
            montrer = cle =>
            {
                onglets.Children.Clear();
                foreach (var kv in DocumentsLegaux.Liste)
                {
                    string k = kv.Key;
                    var b = new BoutonNoyau(Langue.T(kv.Value), k == cle, Math.Max(130, kv.Value.Length * 7.6 + 34), 30) { TaillePolice = 12.5 };
                    b.Clic += () => montrer(k);
                    onglets.Children.Add(b);
                }
                string t = DocumentsLegaux.Texte(cle);
                texte.Text = t == "" ? Langue.T("Texte introuvable.") : t;
            };
            montrer(premiere != null && DocumentsLegaux.Liste.Any(kv => kv.Key == premiere) ? premiere : DocumentsLegaux.Liste[0].Key);
            f.Ouvrir("Informations légales", pile, 860);
        }

        private static void APropos(FenetrePrincipale f)
        {
            string texte = "Nettoyage, optimisation, sécurité, disques et mémoire vive pour Windows 10 et 11.\n\nÉditeur : Damien\nPage officielle : https://github.com/" + MisesAJour.Depot +
                "\nJournaux : " + Chemins.Donnees + "\n\nEngagements :\n- Tes documents, photos et vidéos ne sont jamais supprimés.\n- Un point de restauration est créé avant les opérations importantes.\n- Chaque modification peut être annulée (Historique).\n- Nettoyage PC ne tourne jamais en administrateur : Windows demande l'autorisation pour chaque action qui touche tout le PC.\n- Les mises à jour sont vérifiées (SHA256) avant installation.\n- Ni publicité, ni pistage, ni télémétrie.\n\nWindows et Microsoft Defender sont des marques de Microsoft Corporation. Nettoyage PC est un logiciel indépendant, non affilié à Microsoft.";
            f.Demander(Langue.F("Nettoyage PC 5 (aperçu) — moteur {0}", Appli.Version), texte, "Fermer", null, r => { });
        }

        private static void CreerRaccourci(FenetrePrincipale f)
        {
            try
            {
                string bureau = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                dynamic wsh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic lnk = wsh.CreateShortcut(Path.Combine(bureau, "Nettoyage PC.lnk"));
                lnk.TargetPath = Chemins.Exe;
                lnk.WorkingDirectory = Chemins.Ici;
                lnk.Description = "Nettoyage PC : nettoyage, optimisation et sécurité";
                lnk.IconLocation = Chemins.Exe + ",0";
                lnk.Save();
                Task.Run(() => { try { Programmes.RafraichirIcones(); } catch { } });
                f.Demander("Raccourci créé", "Le raccourci « Nettoyage PC » est sur le Bureau." + (Chemins.EstInstalle ? "" : "\n\nNe déplace pas le dossier de l'application, sinon le raccourci ne fonctionnera plus."), "Compris", null, r => { });
            }
            catch (Exception ex) { f.Demander("Impossible de créer le raccourci", ex.Message, "Compris", null, r => { }, true); }
        }
    }
}
