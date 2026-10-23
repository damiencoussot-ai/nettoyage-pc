// Nettoyage PC 5 - Premium : bandeau (état de la licence, essai, achat, clé), outils Premium, gratuit ou Premium ?,
// questions fréquentes. Achat et activation : même service de licences que la version 4. Rien n'est payé sans le vouloir :
// l'achat se fait dans le navigateur, après avoir accepté les conditions de vente.
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
    internal sealed class PagePremium5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Action surChange;

        public PagePremium5(FenetrePrincipale f)
        {
            F = f;
            Spacing = 18; Margin = new Thickness(28, 6, 28, 28);
            Construire();
            surChange = () => Dispatcher.UIThread.Post(Construire);
            AttachedToVisualTree += (s, e) => Licence.Change += surChange;
            DetachedFromVisualTree += (s, e) => Licence.Change -= surChange;
        }

        private void Construire()
        {
            var P = Habillage.P;
            Children.Clear();
            Children.Add(Bandeau());
            Children.Add(Garanties());
            var outils = new List<CarteAction>
            {
                Outil("Gardien en temps réel", "Veille sur ton PC en continu : programme ajouté au démarrage, disque presque plein (avec nettoyage léger automatique), antivirus coupé, mémoire saturée, surchauffe.", "Régler le gardien", null),
                Outil("Pourquoi mon PC ralentit ?", "L'enquêteur retrace tout ce qui a changé (applications, pilotes, mises à jour, démarrage, réglages) et le compare aux performances : il désigne le coupable.", "Enquêter", null),
                Outil("Santé prédictive", "Repère les signes qui annoncent une panne avant qu'elle arrive : erreurs du matériel et du disque, usure des SSD, batterie, chauffe. Avec la tendance.", "Vérifier", null),
                Outil("Assistant local", "Pose tes questions sur ton PC avec tes mots. L'IA tourne sur ton PC (Ollama) : rien ne part sur Internet.", "Discuter", null),
                Outil("Preuve en FPS", "Mesure les images par seconde réelles de tes jeux (moyenne, 1 % et 0,1 % les plus lents) avant et après un réglage : le vrai gain, en chiffres.", "Mesurer", null, () => Fps5.Ouvrir(F)),
                Outil("Coût en électricité", "Combien ton PC te coûte chaque mois en euros, quelles applications consomment le plus, et comment payer moins.", "Voir", null),
                Outil("Sauvegarde automatique", "Dès que ta clé USB ou ton disque externe est branché, les nouvelles photos et les documents modifiés sont copiés. Rien n'est jamais effacé de la sauvegarde.", "Configurer", null),
                Outil("Analyse des plantages", "Écrans bleus, arrêts brutaux, applications qui plantent, erreurs matérielles : la cause expliquée simplement, et quoi faire.", "Analyser", "plantages"),
                Outil("Analyse du démarrage", "Combien de temps met ton PC à démarrer, et qui le ralentit : le pilote, le service ou le programme précis, sur les 90 derniers jours.", "Analyser", "demarrage"),
                Outil("Mesure des performances", "Processeur, mémoire vive et disque mesurés en chiffres. Compare avant / après un réglage (EXPO, pilote, nettoyage) pour voir le vrai gain.", "Mesurer", null),
                Outil("Kit de réinstallation", "Avant de réinstaller Windows ou de changer de PC : tes applications (réinstallées d'un coup), tes pilotes et tes réseaux Wi-Fi, avec un script qui remet tout en place.", "Préparer le kit", "kit"),
                Outil("Santé de la batterie", "Pour les portables : usure de la batterie, nombre de cycles, autonomie réelle comparée à celle d'origine, et conseils pour la faire durer.", "Vérifier", null),
            };
            Children.Add(Titre(Langue.F("{0} outils de pro, débloqués d'un coup", outils.Count)));
            Children.Add(Cartes.Grille(F, outils));
            Children.Add(Titre(Langue.T("Gratuit ou Premium ?")));
            Children.Add(Comparatif());
            Children.Add(Titre(Langue.T("Questions fréquentes")));
            var faq = new StackPanel { Spacing = 12, Margin = new Thickness(0, 0, 12, 0) };
            foreach (var q in new[]
            {
                new[] { "Et si ça ne me convient pas ?", "Essaie d'abord gratuitement 7 jours. Après l'achat, tu es satisfait ou remboursé pendant 14 jours, sans justification." },
                new[] { "Est-ce un abonnement ?", Langue.F("Non : tu payes {0} une seule fois, et Premium reste actif pour toutes les versions 4.", Boutique.Prix) },
                new[] { "Sur combien de PC ?", Langue.F("{0} en même temps. Avant de donner un PC, « Gérer la licence » libère sa place.", Boutique.NombrePc) },
                new[] { "Faut-il Internet ?", "Pour activer la clé, puis une petite vérification par semaine. Sans Internet, Premium reste actif 45 jours." },
                new[] { "Qu'est-ce qui reste gratuit ?", "Tout le reste : nettoyage, sécurité, mises à jour, sauvegarde, jeux, aide à distance." },
            })
            {
                var s = new StackPanel { Spacing = 3 };
                s.Children.Add(new TextBlock { Text = Langue.T(q[0]), FontFamily = Habillage.Affichage, FontSize = 14.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
                s.Children.Add(new TextBlock { Text = Langue.T(q[1]), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                faq.Children.Add(s);
            }
            var liens = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
            foreach (var kv in DocumentsLegaux.Liste)
            {
                string cle = kv.Key;
                var b = new BoutonNoyau(Langue.T(kv.Value), false, 0, 30) { TaillePolice = 12.5, Margin = new Thickness(0, 0, 8, 6) };
                b.Clic += () => PageReglages5.Legal5(F, cle);
                liens.Children.Add(b);
            }
            faq.Children.Add(liens);
            Children.Add(faq);
        }

        private static TextBlock Titre(string t)
        {
            return new TextBlock { Text = t, FontFamily = Habillage.Affichage, FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(Habillage.P.Texte), Margin = new Thickness(0, 8, 0, -4) };
        }

        /// <summary>Carte d'un outil : sans Premium, le bouton ouvre la fenêtre d'achat (essai gratuit possible).</summary>
        private CarteAction Outil(string titre, string texte, string bouton, string action, Action ouvrir = null)
        {
            return CarteAction.Code(titre, texte, bouton, () =>
            {
                if (!Licence.Premium) { Achat5.Ouvrir(F); return; }
                if (ouvrir != null) ouvrir();
                else if (action != null) F.LancerAction(action);
                else F.Bientot(titre);
            });
        }

        private Control Bandeau()
        {
            var P = Habillage.P;
            string pastille, titre, texte;
            var boutons = new List<BoutonNoyau>();
            string qui = Licence.Titulaire != "" ? Licence.Titulaire : Licence.Courriel;
            if (Licence.Premium && !Licence.EnEssai)
            {
                pastille = "★  PREMIUM ACTIF"; titre = "Merci pour ton soutien !";
                texte = (qui != "" ? Langue.F(Licence.Type == "offerte" ? "Licence de {0} (clé offerte)." : "Licence de {0}.", qui) + " " : "") + Langue.T("Tous les outils Premium sont débloqués sur ce PC, pour toutes les versions 4.");
                var g = new BoutonNoyau("Gérer la licence", false, 0, 42) { TaillePolice = 14 };
                g.Clic += GererLicence;
                boutons.Add(g);
            }
            else
            {
                if (Licence.EnEssai)
                {
                    pastille = Licence.JoursEssaiRestants <= 1 ? Langue.T("ESSAI GRATUIT  ·  DERNIER JOUR") : Langue.F("ESSAI GRATUIT  ·  {0} JOURS RESTANTS", Licence.JoursEssaiRestants);
                    titre = "Tu essaies Premium";
                    texte = "Tous les outils sont débloqués pendant l'essai. Ensuite, ils se verrouillent simplement : rien n'est facturé. Pour les garder, achète Premium une seule fois.";
                }
                else
                {
                    pastille = "★  PREMIUM"; titre = "Nettoyage PC Premium";
                    texte = Langue.T("Les outils des pros pour garder ton PC rapide, stable et sauvegardé : un gardien qui veille en continu, la sauvegarde automatique, l'analyse des plantages et du démarrage, et plus encore.") +
                        (!Licence.EssaiDisponible ? "\n" + Langue.T("Ton essai gratuit est terminé : merci de l'avoir essayé !") : "");
                }
                if (Licence.Remarque != "") texte += "\n" + Langue.T(Licence.Remarque);
                if (!Licence.EnEssai && Licence.EssaiDisponible)
                {
                    var e = new BoutonNoyau("Essayer gratuitement 7 jours", true, 0, 42) { TaillePolice = 14 };
                    e.Clic += () => Achat5.LancerEssai(F);
                    boutons.Add(e);
                }
                var a = new BoutonNoyau(Langue.F("Acheter  ·  {0}", Boutique.Prix), boutons.Count == 0, 0, 42) { TaillePolice = 14 };
                a.Clic += () => Achat5.Ouvrir(F);
                boutons.Add(a);
                var c = new BoutonNoyau("J'ai une clé", false, 0, 42) { TaillePolice = 14 };
                c.Clic += () => Achat5.Ouvrir(F, true);
                boutons.Add(c);
            }
            var s = new StackPanel { Spacing = 8 };
            s.Children.Add(new Border
            {
                Child = new TextBlock { Text = Langue.T(pastille), FontFamily = Habillage.Console, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.SurAccent) },
                Background = Habillage.DegradeAccent(), CornerRadius = new CornerRadius(2), Padding = new Thickness(10, 4), HorizontalAlignment = HorizontalAlignment.Left,
            });
            s.Children.Add(new TextBlock { Text = Langue.T(titre), FontFamily = Habillage.Affichage, FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            s.Children.Add(new TextBlock { Text = Langue.T(texte), FontFamily = Habillage.Corps, FontSize = 14, LineHeight = 21, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap, MaxWidth = 640, HorizontalAlignment = HorizontalAlignment.Left });
            var rangee = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            foreach (var b in boutons) { b.Margin = new Thickness(0, 0, 10, 6); rangee.Children.Add(b); }
            s.Children.Add(rangee);
            var g2 = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            g2.Children.Add(s);
            if (!(Licence.Premium && !Licence.EnEssai))
            {
                var prix = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(24, 0, 10, 0) };
                prix.Children.Add(new TextBlock { Text = Boutique.Prix, FontFamily = Habillage.Affichage, FontSize = 40, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent), HorizontalAlignment = HorizontalAlignment.Right });
                prix.Children.Add(new TextBlock { Text = Langue.T("TTC, une seule fois"), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), HorizontalAlignment = HorizontalAlignment.Right });
                Grid.SetColumn(prix, 1); g2.Children.Add(prix);
            }
            return new Border
            {
                Child = g2, Padding = new Thickness(28, 24), CornerRadius = new CornerRadius(4),
                Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative), EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                    GradientStops = { new GradientStop(Habillage.Melanger(P.Carte, P.Accent, P.Sombre ? 0.10 : 0.06), 0), new GradientStop(Habillage.Melanger(P.Carte, P.Accent2, P.Sombre ? 0.04 : 0.02), 1) },
                },
                BorderBrush = Habillage.Pinceau(P.Accent, 120), BorderThickness = new Thickness(1),
            };
        }

        private static Control Garanties()
        {
            var P = Habillage.P;
            var w = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (var t in new[] { "Paiement sécurisé (Stripe)", "Satisfait ou remboursé 14 jours", "Sans abonnement", "3 PC, toutes les versions 4", "Ni publicité, ni pistage" })
                w.Children.Add(new Border
                {
                    Child = new TextBlock { Text = "✔  " + Langue.T(t), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Texte) },
                    Padding = new Thickness(12, 7), Margin = new Thickness(0, 0, 8, 8), CornerRadius = new CornerRadius(3),
                    Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1),
                });
            return w;
        }

        private static Control Comparatif()
        {
            var P = Habillage.P;
            string[][] lignes =
            {
                new[] { "Nettoyage, optimisation des disques et de la mémoire", "1", "1" }, new[] { "Sécurité, mises à jour, pilotes", "1", "1" },
                new[] { "Sauvegarde manuelle, jeux, réseau, aide à distance", "1", "1" }, new[] { "Bouclier anti-arnaque, plein potentiel du matériel", "1", "1" },
                new[] { "Gardien en temps réel et nettoyage automatique", "0", "1" },
                new[] { "Enquêteur « Pourquoi mon PC ralentit ? », santé prédictive", "0", "1" }, new[] { "Assistant local (IA sur ton PC), preuve en FPS, coût en électricité", "0", "1" },
                new[] { "Sauvegarde automatique sur clé USB", "0", "1" }, new[] { "Analyse des plantages et du démarrage", "0", "1" },
                new[] { "Mesure des performances, kit de réinstallation", "0", "1" }, new[] { "Santé de la batterie", "0", "1" },
                new[] { "Nouveaux outils Premium des versions 4", "0", "1" },
            };
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,110,110") };
            for (int i = 0; i <= lignes.Length; i++) g.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            // colonne Premium mise en valeur
            var fond = new Border { Background = Habillage.Pinceau(P.Accent, P.Sombre ? 22 : 16), CornerRadius = new CornerRadius(3) };
            Grid.SetColumn(fond, 2); Grid.SetRowSpan(fond, lignes.Length + 1); g.Children.Add(fond);
            var tg = new TextBlock { Text = Langue.T("Gratuit"), FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(P.Discret), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 12) };
            Grid.SetColumn(tg, 1); g.Children.Add(tg);
            var tp = new Border
            {
                Child = new TextBlock { Text = "Premium", FontFamily = Habillage.Console, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.SurAccent) },
                Background = Habillage.DegradeAccent(), CornerRadius = new CornerRadius(2), Padding = new Thickness(10, 3), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            Grid.SetColumn(tp, 2); g.Children.Add(tp);
            for (int i = 0; i < lignes.Length; i++)
            {
                var sep = new Border { Height = 1, Background = Habillage.Pinceau(P.Bordure), VerticalAlignment = VerticalAlignment.Top };
                Grid.SetRow(sep, i + 1); Grid.SetColumnSpan(sep, 3); g.Children.Add(sep);
                var t = new TextBlock { Text = Langue.T(lignes[i][0]), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 12, 9) };
                Grid.SetRow(t, i + 1); g.Children.Add(t);
                for (int c = 1; c <= 2; c++)
                {
                    bool oui = lignes[i][c] == "1";
                    var m = new TextBlock { Text = oui ? "✔" : "—", FontSize = oui ? 15 : 13, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(oui ? (c == 2 ? P.Accent : P.Vert) : P.Discret), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetRow(m, i + 1); Grid.SetColumn(m, c); g.Children.Add(m);
                }
            }
            return new Border { Child = g, MaxWidth = 820, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(20, 4, 12, 8), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1) };
        }

        private void GererLicence()
        {
            string info = Langue.T("Licence Premium active sur ce PC.") + "\n\n" + Langue.T(Licence.Type == "offerte" ? "Type : clé offerte" : "Type : achat sur la boutique") +
                (Licence.Titulaire != "" ? "\n" + Langue.T("Titulaire") + " : " + Licence.Titulaire : "") + (Licence.Courriel != "" ? "\nE-mail : " + Licence.Courriel : "") + "\n" + Langue.T("Clé") + " : " + Licence.CleMasquee +
                "\n\n" + Langue.T("Retirer la licence de ce PC ? (par exemple avant de le donner ou de le vendre : la place est libérée pour un autre PC)");
            F.Demander("Nettoyage PC Premium", info, "Retirer la licence de ce PC", "Garder", oui =>
            {
                if (!oui) return;
                F.Statut("Retrait de la licence…");
                Task.Run(() => { try { return Licence.Retirer(); } catch (Exception ex) { return ex.Message; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    if (t.Result == "")
                    {
                        try { Gardien.Desactiver(); } catch { }
                        F.Demander("Licence retirée de ce PC", "Tu peux l'activer sur un autre PC avec la même clé.", "Compris", null, r => { });
                    }
                    else F.Demander("La licence n'a pas pu être retirée", Langue.T(t.Result), "Compris", null, r => { }, true);
                }));
            }, true);
        }
    }

    /// <summary>Fenêtre d'achat : essai gratuit, conditions de vente à accepter, achat dans le navigateur, activation d'une clé.</summary>
    internal sealed class Achat5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBox zone = new TextBox { AcceptsReturn = false, TextWrapping = TextWrapping.Wrap, Height = 58, Watermark = "NPC-… ou la clé reçue par e-mail" };
        private readonly TextBlock resultat;
        private readonly BoutonNoyau bActiver, bAcheter;
        private readonly CheckBox conditions;
        private FenetreInterieure fenetre;
        private DispatcherTimer attente;
        private bool interrogation;

        public static void Ouvrir(FenetrePrincipale f, bool cle = false)
        {
            var a = new Achat5(f);
            a.fenetre = f.Ouvrir("Nettoyage PC Premium", a, 700);
            a.fenetre.Fermee += () => { if (a.attente != null) a.attente.Stop(); };
            if (cle) Dispatcher.UIThread.Post(() => a.zone.Focus());
        }

        /// <summary>Essai gratuit de 7 jours (avec question).</summary>
        public static void LancerEssai(FenetrePrincipale f, Action apres = null)
        {
            f.Demander("Essayer Premium gratuitement pendant 7 jours ?",
                "Tous les outils Premium sont débloqués tout de suite. Aucun paiement, aucune adresse e-mail : à la fin de l'essai, ils se verrouillent simplement, sans rien facturer.\n\nL'essai ne peut être lancé qu'une fois sur ce PC.",
                "Lancer l'essai", "Annuler", oui =>
                {
                    if (!oui) return;
                    string r; try { r = Licence.DemarrerEssai(); } catch (Exception ex) { r = ex.Message; }
                    if (r == "") { f.Demander("Essai gratuit activé ✔", "Tu as 7 jours pour tout essayer. Commence par le gardien en temps réel ou l'analyse des plantages.", "C'est parti", null, x => { }); if (apres != null) apres(); }
                    else f.Demander("L'essai gratuit n'a pas pu démarrer", Langue.T(r) + ".", "Compris", null, x => { }, true);
                });
        }

        private Achat5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(new TextBlock { Text = Langue.F("{0} TTC une seule fois · sans abonnement · toutes les versions 4 · {1} PC", Boutique.Prix, Boutique.NombrePc), FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap });
            Children.Add(new TextBlock
            {
                Text = Langue.T("✦  Gardien en temps réel, avec nettoyage léger automatique quand le disque se remplit\n" +
                    "✦  « Pourquoi mon PC ralentit ? » : l'enquêteur qui trouve le coupable\n" +
                    "✦  Santé prédictive : les signes d'une panne, repérés avant qu'elle arrive\n" +
                    "✦  Assistant local (IA sur ton PC), preuve en FPS, coût en électricité\n" +
                    "✦  Sauvegarde automatique, plantages, démarrage, performances, kit de réinstallation, batterie, thèmes"),
                FontFamily = Habillage.Corps, FontSize = 13.5, LineHeight = 21, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap,
            });
            if (!Licence.Premium && Licence.EssaiDisponible)
            {
                var essai = new BoutonNoyau("Essayer gratuitement 7 jours", false, 0, 40) { TaillePolice = 14, HorizontalAlignment = HorizontalAlignment.Left };
                essai.Clic += () => LancerEssai(F, () => { if (fenetre != null) fenetre.Fermer(); });
                Children.Add(essai);
                Children.Add(new TextBlock { Text = Langue.T("Aucun paiement ni adresse e-mail. Une fois par PC."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret) });
            }
            conditions = new CheckBox { Content = new TextBlock { Text = Langue.T("J'ai lu et j'accepte les conditions de vente et le contrat de licence"), FontFamily = Habillage.Corps, FontSize = 13, TextWrapping = TextWrapping.Wrap }, Margin = new Thickness(0, 8, 0, 0) };
            Children.Add(conditions);
            var liens = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            var lCgv = new BoutonNoyau("Lire les conditions de vente", false, 0, 28) { TaillePolice = 12 };
            lCgv.Clic += () => PageReglages5.Legal5(F, "CONDITIONS-DE-VENTE");
            var lLic = new BoutonNoyau("Lire le contrat de licence", false, 0, 28) { TaillePolice = 12 };
            lLic.Clic += () => PageReglages5.Legal5(F, "LICENCE");
            liens.Children.Add(lCgv); liens.Children.Add(lLic);
            Children.Add(liens);
            bAcheter = new BoutonNoyau(Langue.F("Acheter ({0} TTC)", Boutique.Prix), true, 0, 42) { TaillePolice = 14.5, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 4, 0, 0) };
            conditions.IsCheckedChanged += (s, e) => bAcheter.IsEnabled = conditions.IsChecked == true;
            bAcheter.Clic += Acheter;
            Children.Add(bAcheter);
            Children.Add(new TextBlock
            {
                Text = Langue.T(Boutique.AchatAutomatique || Boutique.UrlAchat == ""
                    ? "Paiement sécurisé par Stripe (carte bancaire, Apple Pay, Google Pay...). Premium s'active tout seul après le paiement, et ta clé s'affiche pour tes autres PC. Satisfait ou remboursé 14 jours."
                    : "Paiement sécurisé par Lemon Squeezy, revendeur officiel (carte bancaire, PayPal...). La clé arrive par e-mail juste après. Satisfait ou remboursé 14 jours."),
                FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap,
            });
            Children.Add(new TextBlock { Text = Langue.T("J'ai une clé :"), FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), Margin = new Thickness(0, 10, 0, 0) });
            Children.Add(zone);
            var ligne = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 14 };
            bActiver = new BoutonNoyau("Activer", true, 130, 36) { TaillePolice = 14 };
            bActiver.Clic += Activer;
            resultat = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap, MaxWidth = 480 };
            ligne.Children.Add(bActiver); ligne.Children.Add(resultat);
            Children.Add(ligne);
        }

        private void Message(string texte, Color c) { resultat.Text = texte; resultat.Foreground = Habillage.Pinceau(c); }

        private void Acheter()
        {
            if (conditions.IsChecked != true) return;
            if (Boutique.UrlAchat == "") { F.Demander("La boutique ouvre bientôt", "En attendant, tu peux essayer Premium gratuitement, ou demander une clé à l'éditeur.", "Compris", null, r => { }); return; }
            Programmes.Ouvrir(Licence.AdresseAchat());
            if (Boutique.AchatAutomatique)
            {
                Message(Langue.T("La page de paiement s'ouvre dans ton navigateur. Dès que le paiement est validé, Premium s'active tout seul ici : garde cette fenêtre ouverte."), Habillage.P.Accent);
                AttendrePaiement();
            }
            else
            {
                Message(Langue.T("La page de paiement s'ouvre dans ton navigateur. Après le paiement, tu reçois ta clé par e-mail : colle-la ci-dessous."), Habillage.P.Accent);
                zone.Focus();
            }
        }

        /// <summary>Après l'achat : on demande au service de licences, toutes les 5 secondes pendant 30 minutes, si le paiement est arrivé.</summary>
        private void AttendrePaiement()
        {
            if (attente != null) return;
            DateTime fin = DateTime.Now.AddMinutes(30);
            attente = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            attente.Tick += (s, e) =>
            {
                if (interrogation) return;
                if (DateTime.Now > fin) { attente.Stop(); return; }
                interrogation = true;
                Task.Run(() =>
                {
                    string cle, r;
                    try { r = Licence.RecupererAchat(out cle); } catch (Exception ex) { r = ex.Message; cle = ""; }
                    Dispatcher.UIThread.Post(() => { interrogation = false; ResultatPaiement(r, cle); });
                });
            };
            attente.Start();
        }

        private void ResultatPaiement(string r, string cle)
        {
            if (r == "pending" || r.StartsWith("service de licences injoignable")) return;   // on réessaie dans 5 secondes
            attente.Stop();
            if (r == "paid")
            {
                if (fenetre != null) fenetre.Fermer();
                F.Demander("Premium activé ✔", Langue.T("Merci ! Tous les outils Premium sont débloqués sur ce PC.") + "\n\n" + Langue.T("Ta clé, pour tes autres PC (elle marche sur 3 PC à la fois) :") + "\n" + cle, "Super", null, x => { });
                return;
            }
            Message(r == "disabled" ? Langue.T("Ce paiement a été remboursé ou contesté : la clé n'est pas active.")
                : r == "none" ? Langue.T("Aucun achat en attente sur ce PC.") : Langue.T("Clé non activée") + " : " + Langue.T(r), Habillage.P.Rouge);
        }

        private void Activer()
        {
            string cle = zone.Text ?? "";
            if (cle.Trim() == "") { zone.Focus(); return; }
            bActiver.IsEnabled = false;
            Message(Langue.T("Vérification de la clé…"), Habillage.P.Accent);
            Task.Run(() => { try { return Licence.Activer(cle); } catch (Exception ex) { return ex.Message; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                bActiver.IsEnabled = true;
                if (t.Result == "")
                {
                    if (fenetre != null) fenetre.Fermer();
                    F.Demander("Premium activé ✔", Langue.T("Merci ! Tous les outils Premium sont débloqués sur ce PC."), "Super", null, x => { });
                }
                else Message(Langue.T("Clé non activée") + " : " + Langue.T(t.Result), Habillage.P.Rouge);
            }));
        }
    }
}
