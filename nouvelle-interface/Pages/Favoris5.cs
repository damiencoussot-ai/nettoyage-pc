// Nettoyage PC 5 - favoris : tu choisis tes outils préférés (gratuits et Premium) et ils s'affichent en haut de l'accueil,
// pour les avoir sous les yeux et les lancer en un clic. Une étoile sur chaque carte d'outil ajoute ou retire un favori ;
// « Choisir mes favoris » les montre tous, par rubrique, et permet de changer leur ordre. Gardés dans parametres.json.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    /// <summary>Un outil qu'on peut mettre en favori.</summary>
    internal sealed class OutilFavori
    {
        public string Id, Titre, Texte, Rubrique;
        public bool Premium;
        public Action<FenetrePrincipale> Ouvrir;
        /// <summary>Autres titres de carte qui désignent le même outil (ex. « Mode jeu (actif) »).</summary>
        public string[] Alias = new string[0];
    }

    internal static class Favoris5
    {
        public const int Maximum = 12;
        /// <summary>Favoris proposés tant que l'utilisateur n'a rien choisi.</summary>
        private static readonly string[] Proposes = { "rapide", "modejeu", "secu", "carteespace" };
        private static List<string> courants;
        /// <summary>Les favoris ont changé (accueil et étoiles des cartes se mettent à jour).</summary>
        public static event Action Change;

        private static OutilFavori O(string id, string rubrique, string titre, string texte, Action<FenetrePrincipale> ouvrir, bool premium = false, params string[] alias)
        { return new OutilFavori { Id = id, Rubrique = rubrique, Titre = titre, Texte = texte, Ouvrir = ouvrir, Premium = premium, Alias = alias }; }

        private static Action<FenetrePrincipale> Act(string cle) { return f => f.LancerAction(cle); }
        private static Action<FenetrePrincipale> Page(string rubrique, string onglet) { return f => { if (onglet != null) f.SousPage(rubrique, onglet); f.Afficher(rubrique, true); }; }
        private static Action<FenetrePrincipale> Outil(string cle) { return f => f.OuvrirOutil(cle); }

        /// <summary>Tous les outils, dans l'ordre des rubriques.</summary>
        public static readonly List<OutilFavori> Catalogue = new List<OutilFavori>
        {
            O("rapide", "Nettoyer", "Nettoyage rapide", "Fichiers temporaires, caches, corbeille.", Act("standard")),
            O("profond", "Nettoyer", "Nettoyage profond", "Tout le rapide, plus Windows Update et DISM.", Act("complet")),
            O("apercu", "Nettoyer", "Voir ce qui serait nettoyé", "L'espace récupérable, sans rien supprimer.", Act("analyse")),
            O("anciens", "Nettoyer", "Anciens pilotes", "Les anciennes versions gardées par Windows.", f => AnciensPilotes5.Ouvrir(f)),
            O("averifier", "Nettoyer", "Téléchargements à vérifier", "Les vieux téléchargements rangés à part.", f => PageNettoyer.OuvrirAVerifier(f)),
            O("disques", "Nettoyer", "Optimiser les disques", "Santé, TRIM des SSD, défragmentation des disques durs.", Act("disques")),
            O("verifdisques", "Nettoyer", "Vérifier les disques", "Analyse du système de fichiers, en lecture seule.", Act("verifier")),
            O("carteespace", "Nettoyer", "Carte de l'espace disque", "Ce qui remplit ton disque, d'un coup d'œil.", f => CarteEspace5.Ouvrir(f)),
            O("doublons", "Nettoyer", "Doublons", "Les fichiers en double, à garder ou à retirer.", Page("Nettoyer", "Doublons")),

            O("modejeu", "Accelerer", "Mode jeu", "Performances maximales avant de jouer.", f => f.LancerAction(File.Exists(Chemins.Fichier("modejeu.json")) ? "finmodejeu" : "modejeu"), false, "Mode jeu (actif)"),
            O("optim", "Accelerer", "Bilan d'optimisation", "Des conseils concrets, sans rien modifier.", Act("optim")),
            O("demarrage", "Accelerer", "Programmes au démarrage", "Ce qui se lance avec Windows, et ce qui peut attendre.", Page("Accelerer", "Demarrage")),
            O("majapps", "Accelerer", "Mettre à jour les applications", "Tes logiciels à jour avec winget.", Act("majapps")),
            O("alim", "Accelerer", "Mode d'alimentation", "Performances élevées ou Équilibré.", f => f.LancerAction(PageAccelerer.ModeAlimentation() == "Performances élevées" ? "equilibre" : "perf")),
            O("ramlib", "Accelerer", "Libérer la RAM", "Juste avant un jeu ou une application gourmande.", Act("ramlib")),
            O("raminfo", "Accelerer", "Infos mémoire vive", "Barrettes, applications gourmandes, erreurs.", Act("raminfo")),
            O("ramtest", "Accelerer", "Tester les barrettes", "Diagnostic de mémoire Windows (redémarrage).", Act("ramtest")),
            O("reglagesjeux", "Accelerer", "Réglages des jeux", "Mode jeu de Windows, GPU, jeux en fenêtré.", f => ReglagesWindows5.Jeux(f)),

            O("secu", "Proteger", "Bilan de sécurité", "Antivirus, pare-feu, Secure Boot, mises à jour.", Act("secu")),
            O("antivirus", "Proteger", "Analyse antivirus rapide", "Microsoft Defender à jour, puis analyse.", Act("antivirus")),
            O("bouclier", "Proteger", "Bouclier anti-arnaque", "Logiciels de prise en main à distance.", f => Bouclier5.Ouvrir(f)),
            O("point", "Proteger", "Créer un point de restauration", "Pour revenir en arrière en cas de problème.", Act("point")),
            O("reparer", "Proteger", "Réparer Windows", "DISM et SFC, avec un point de restauration avant.", Act("reparer")),
            O("extensions", "Proteger", "Extensions des navigateurs", "Celles à vérifier dans Chrome, Edge, Firefox...", f => Extensions5.Ouvrir(f)),
            O("confidentialite", "Proteger", "Confidentialité", "Moins de publicité et de pistage.", f => ReglagesWindows5.Confidentialite(f)),
            O("sauvegarde", "Proteger", "Sauvegarde", "Copier tes documents et tes photos.", Page("Proteger", "Sauvegarde")),

            O("memoire", "Outils", "Mémoire vive et BIOS", "Vitesse réelle des barrettes, profil EXPO / XMP.", f => Memoire5.Ouvrir(f)),
            O("santedisques", "Outils", "Santé détaillée des disques", "Usure, température et erreurs (SMART).", Act("santedisques")),
            O("rapport", "Outils", "Rapport du PC", "Espace, gros fichiers, doublons, dans tes Documents.", Act("rapport"), false, "Rapport complet du PC"),
            O("potentiel", "Outils", "Plein potentiel", "Les réglages qui brident ton matériel.", Page("Outils", "Potentiel")),
            O("reseau", "Outils", "Test de la connexion", "Débit, serveurs DNS et réseau.", Page("Outils", "Reseau")),
            O("aide", "Outils", "Aide à distance", "Recevoir de l'aide d'un proche, en sécurité.", Page("Outils", "Aide")),
            O("compat", "Outils", "Compatibilité de ce PC", "Windows 11, TPM, Secure Boot, virtualisation.", Act("compat")),

            O("gardien", "Premium", "Gardien en temps réel", "Veille sur ton PC en continu.", Outil("gardien"), true),
            O("enqueteur", "Premium", "Pourquoi mon PC ralentit ?", "L'enquêteur désigne le coupable.", Outil("enqueteur"), true),
            O("sante", "Premium", "Santé prédictive", "Les signes qui annoncent une panne.", Outil("sante"), true),
            O("assistant", "Premium", "Assistant local", "Tes questions sur ton PC, sans Internet.", Outil("assistant"), true),
            O("fps", "Premium", "Preuve en FPS", "Les images par seconde, avant et après.", Outil("fps"), true),
            O("energie", "Premium", "Coût en électricité", "Ce que ton PC te coûte chaque mois.", Outil("energie"), true),
            O("sauvegardeauto", "Premium", "Sauvegarde automatique", "Dès que ton disque externe est branché.", Outil("sauvegardeauto"), true),
            O("plantages", "Premium", "Analyse des plantages", "Écrans bleus et arrêts brutaux expliqués.", Outil("plantages"), true),
            O("analysedemarrage", "Premium", "Analyse du démarrage", "Qui ralentit le démarrage de Windows.", Outil("demarrage"), true),
            O("benchmark", "Premium", "Mesure des performances", "Processeur, mémoire et disque en chiffres.", Outil("benchmark"), true),
            O("kit", "Premium", "Kit de réinstallation", "Applications, pilotes et Wi-Fi à remettre en place.", f => { if (!Licence.Premium) Achat5.Ouvrir(f); else f.LancerAction("kit"); }, true),
            O("modejeuauto", "Premium", "Mode jeu automatique", "Le mode jeu tout seul, à chaque partie.", Outil("modejeu"), true),
            O("majappsauto", "Premium", "Mises à jour automatiques", "Tes applications à jour chaque semaine.", Outil("majapps"), true),
            O("photos", "Premium", "Photos en double", "Rafales et copies : garde la meilleure.", Outil("photos"), true),
            O("bulletin", "Premium", "Bulletin mensuel du PC", "Une page claire sur ton PC, chaque mois.", Outil("bulletin"), true),
            O("surveillance", "Premium", "Surveillance du réseau", "Les appareils inconnus sur ta box.", Outil("reseau"), true),
            O("batterie", "Premium", "Santé de la batterie", "Usure, cycles et autonomie réelle.", Outil("batterie"), true),
        };

        public static OutilFavori Par(string id) { return Catalogue.FirstOrDefault(o => o.Id == id); }

        /// <summary>Outil désigné par le titre d'une carte (null si cette carte ne peut pas être un favori).</summary>
        public static OutilFavori ParTitre(string titre) { return Catalogue.FirstOrDefault(o => o.Titre == titre || o.Alias.Contains(titre)); }

        /// <summary>Favoris dans l'ordre choisi.</summary>
        public static List<string> Liste()
        {
            if (courants == null)
            {
                string brut = null;
                try { brut = Parametres.Lire().Favoris; } catch { }
                courants = brut == null ? Proposes.ToList() : brut.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Where(id => Par(id) != null).Distinct().Take(Maximum).ToList();
            }
            return courants.ToList();
        }

        public static bool Est(string id) { return Liste().Contains(id); }

        private static void Enregistrer(FenetrePrincipale f, List<string> l)
        {
            courants = l;
            f.EnregistrerReglage(r => r.Favoris = string.Join(",", l));
            var c = Change; if (c != null) c();
        }

        /// <summary>Ajoute ou retire un favori. Faux si la liste est pleine.</summary>
        public static bool Basculer(FenetrePrincipale f, string id)
        {
            var l = Liste();
            if (l.Contains(id)) { l.Remove(id); f.Statut(Langue.F("« {0} » retiré de tes favoris.", Langue.T(Par(id).Titre))); }
            else
            {
                if (l.Count >= Maximum) { f.Statut(Langue.F("Tu as déjà {0} favoris : retires-en un pour en ajouter un autre.", Maximum), true); return false; }
                l.Add(id);
                f.Statut(Langue.F("« {0} » ajouté à tes favoris : il est sur l'accueil.", Langue.T(Par(id).Titre)));
            }
            Enregistrer(f, l);
            return true;
        }

        public static void Deplacer(FenetrePrincipale f, string id, int sens)
        {
            var l = Liste();
            int i = l.IndexOf(id), j = i + sens;
            if (i < 0 || j < 0 || j >= l.Count) return;
            l[i] = l[j]; l[j] = id;
            Enregistrer(f, l);
        }

        public static void Lancer(FenetrePrincipale f, OutilFavori o)
        {
            try { o.Ouvrir(f); }
            catch (Exception ex) { JournalDemarrage.Erreur("favori " + o.Id, ex); f.Statut(Langue.T("Une erreur s'est produite :") + " " + ex.Message, true); }
        }

        public static string NomRubrique(FenetrePrincipale f, string cle)
        {
            var r = f.Rubriques.FirstOrDefault(x => x.Cle == cle);
            return r != null ? r.Nom : cle;
        }

        // ---------------------------------------------------------------- étoile des cartes

        /// <summary>Étoile cliquable (☆ / ★) qui ajoute ou retire l'outil des favoris ; se met à jour toute seule.</summary>
        public static Control Etoile(FenetrePrincipale f, string id, double taille = 18)
        {
            var P = Habillage.P;
            var t = new TextBlock { FontSize = taille, Cursor = new Cursor(StandardCursorType.Hand), Background = Brushes.Transparent, Padding = new Thickness(4, 0, 2, 2), VerticalAlignment = VerticalAlignment.Top };
            bool survol = false;
            Action maj = () =>
            {
                bool est = Est(id);
                t.Text = est ? "★" : "☆";
                t.Foreground = Habillage.Pinceau(est ? P.Orange : survol ? P.Texte : P.Discret);
                ToolTip.SetTip(t, Langue.T(est ? "Retirer de mes favoris" : "Ajouter à mes favoris (affiché sur l'accueil)"));
            };
            maj();
            t.PointerEntered += (s, e) => { survol = true; maj(); };
            t.PointerExited += (s, e) => { survol = false; maj(); };
            t.PointerReleased += (s, e) => { e.Handled = true; Basculer(f, id); };
            t.AttachedToVisualTree += (s, e) => { Change += maj; maj(); };
            t.DetachedFromVisualTree += (s, e) => Change -= maj;
            return t;
        }

        // ---------------------------------------------------------------- bande de l'accueil

        /// <summary>« Mes favoris » sur l'accueil : une tuile par favori (clic = ouvrir), et « Choisir mes favoris ».</summary>
        public static Control Bande(FenetrePrincipale f)
        {
            var bloc = new StackPanel { Spacing = 10, Margin = new Thickness(12, 0, 0, 0) };
            Action remplir = null;
            remplir = () =>
            {
                var P = Habillage.P;
                bloc.Children.Clear();
                var titre = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                titre.Children.Add(new TextBlock { Text = "★  " + Langue.T("Mes favoris"), FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center });
                var choisir = new BoutonNoyau("Choisir mes favoris", false, 170, 28) { TaillePolice = 12 };
                choisir.Clic += () => Choisir(f);
                Grid.SetColumn(choisir, 1); titre.Children.Add(choisir);
                bloc.Children.Add(titre);
                var l = Liste().Select(Par).Where(o => o != null).ToList();
                if (l.Count == 0)
                {
                    var vide = new Border
                    {
                        Padding = new Thickness(16, 12), CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1), BorderBrush = Habillage.Pinceau(P.Bordure), Background = Habillage.Pinceau(P.Carte),
                        Cursor = new Cursor(StandardCursorType.Hand),
                        Child = new TextBlock { Text = Langue.T("Mets ici les outils que tu utilises le plus : clique sur l'étoile ☆ d'un outil, ou sur « Choisir mes favoris »."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap },
                    };
                    vide.PointerReleased += (s, e) => Choisir(f);
                    bloc.Children.Add(vide);
                    return;
                }
                var tuiles = new WrapPanel { Orientation = Orientation.Horizontal };
                foreach (var o in l) tuiles.Children.Add(Tuile(f, o));
                bloc.Children.Add(tuiles);
            };
            remplir();
            Action surChange = () => Avalonia.Threading.Dispatcher.UIThread.Post(remplir);
            bloc.AttachedToVisualTree += (s, e) => { Change += surChange; Licence.Change += surChange; };
            bloc.DetachedFromVisualTree += (s, e) => { Change -= surChange; Licence.Change -= surChange; };
            return bloc;
        }

        private static Control Tuile(FenetrePrincipale f, OutilFavori o)
        {
            var P = Habillage.P;
            bool verrou = o.Premium && !Licence.Premium;
            var b = new Border
            {
                Width = 214, Height = 70, Margin = new Thickness(0, 0, 10, 10), Padding = new Thickness(14, 9, 10, 9), CornerRadius = new CornerRadius(3),
                Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            var g = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
            var haut = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            haut.Children.Add(new TextBlock { Text = NomRubrique(f, o.Rubrique).ToUpper(Langue.Culture), FontFamily = Habillage.Console, FontSize = 10, Foreground = Habillage.Pinceau(o.Premium ? P.Orange : P.Accent) });
            if (verrou) haut.Children.Add(new TextBlock { Text = Langue.T("essai gratuit"), FontFamily = Habillage.Console, FontSize = 10, Foreground = Habillage.Pinceau(P.Discret) });
            g.Children.Add(haut);
            var nom = new TextBlock { Text = Langue.T(o.Titre), FontFamily = Habillage.Affichage, FontSize = 14.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap, MaxLines = 2, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 3, 0, 0) };
            Grid.SetRow(nom, 1); g.Children.Add(nom);
            b.Child = g;
            ToolTip.SetTip(b, Langue.T(o.Texte));
            b.PointerEntered += (s, e) => b.BorderBrush = Habillage.Pinceau(Habillage.Alpha(P.Accent, 140));
            b.PointerExited += (s, e) => b.BorderBrush = Habillage.Pinceau(P.Bordure);
            b.PointerReleased += (s, e) => Lancer(f, o);
            return b;
        }

        // ---------------------------------------------------------------- fenêtre « Choisir mes favoris »

        public static void Choisir(FenetrePrincipale f)
        {
            var contenu = new StackPanel { Spacing = 14 };
            Action remplir = null;
            remplir = () =>
            {
                var P = Habillage.P;
                contenu.Children.Clear();
                var l = Liste();
                contenu.Children.Add(new TextBlock
                {
                    Text = Langue.F("Coche jusqu'à {0} outils : ils s'affichent en haut de l'accueil, pour les avoir sous les yeux et les lancer en un clic. L'étoile ☆ sur chaque outil fait la même chose.", Maximum),
                    FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap
                });

                // ordre des favoris
                contenu.Children.Add(Sous(Langue.F("Mes favoris ({0} / {1}), dans l'ordre de l'accueil", l.Count, Maximum)));
                if (l.Count == 0) contenu.Children.Add(new TextBlock { Text = Langue.T("Aucun favori pour l'instant : choisis-en dans la liste ci-dessous."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret) });
                for (int i = 0; i < l.Count; i++)
                {
                    var o = Par(l[i]); if (o == null) continue;
                    var ligne = new Grid { ColumnDefinitions = new ColumnDefinitions("28,*,Auto"), Margin = new Thickness(0, 0, 0, 2) };
                    ligne.Children.Add(new TextBlock { Text = (i + 1).ToString(), FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center });
                    var nom = new TextBlock { Text = Langue.T(o.Titre) + "   ·   " + NomRubrique(f, o.Rubrique), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
                    Grid.SetColumn(nom, 1); ligne.Children.Add(nom);
                    var bs = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                    string id = o.Id;
                    var haut = new BoutonNoyau("▲", false, 34, 28) { TaillePolice = 11, IsEnabled = i > 0 };
                    haut.Clic += () => Deplacer(f, id, -1);
                    var bas = new BoutonNoyau("▼", false, 34, 28) { TaillePolice = 11, IsEnabled = i < l.Count - 1 };
                    bas.Clic += () => Deplacer(f, id, 1);
                    var retirer = new BoutonNoyau("Retirer", false, 90, 28) { TaillePolice = 12 };
                    retirer.Clic += () => Basculer(f, id);
                    bs.Children.Add(haut); bs.Children.Add(bas); bs.Children.Add(retirer);
                    Grid.SetColumn(bs, 2); ligne.Children.Add(bs);
                    contenu.Children.Add(ligne);
                }

                // tous les outils, par rubrique
                foreach (var groupe in Catalogue.GroupBy(o => o.Rubrique))
                {
                    contenu.Children.Add(Sous(NomRubrique(f, groupe.Key) + (groupe.Key == "Premium" && !Licence.Premium ? "  ·  " + Langue.T("essai gratuit 7 jours") : "")));
                    var grille = new Avalonia.Controls.Primitives.UniformGrid { Columns = 2 };
                    foreach (var o in groupe) grille.Children.Add(Choix(f, o, l.Contains(o.Id)));
                    contenu.Children.Add(grille);
                }
            };
            remplir();
            Action surChange = () => Avalonia.Threading.Dispatcher.UIThread.Post(remplir);
            Change += surChange;
            var fen = f.Ouvrir("Choisir mes favoris", contenu, 860);
            fen.Fermee += () => Change -= surChange;
        }

        private static TextBlock Sous(string t)
        {
            return new TextBlock { Text = t, FontFamily = Habillage.Affichage, FontSize = 15.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(Habillage.P.Texte), Margin = new Thickness(0, 8, 0, 0) };
        }

        private static Control Choix(FenetrePrincipale f, OutilFavori o, bool est)
        {
            var P = Habillage.P;
            var b = new Border
            {
                Margin = new Thickness(0, 0, 8, 8), Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(3), Cursor = new Cursor(StandardCursorType.Hand),
                Background = Habillage.Pinceau(est ? Habillage.Alpha(P.Accent, 26) : P.Carte),
                BorderBrush = Habillage.Pinceau(est ? Habillage.Alpha(P.Accent, 130) : P.Bordure), BorderThickness = new Thickness(1),
            };
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("26,*") };
            g.Children.Add(new TextBlock { Text = est ? "★" : "☆", FontSize = 17, Foreground = Habillage.Pinceau(est ? P.Orange : P.Discret), VerticalAlignment = VerticalAlignment.Top });
            var s = new StackPanel { Spacing = 2 };
            s.Children.Add(new TextBlock { Text = Langue.T(o.Titre), FontFamily = Habillage.Corps, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
            s.Children.Add(new TextBlock { Text = Langue.T(o.Texte), FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(s, 1); g.Children.Add(s);
            b.Child = g;
            string id = o.Id;
            b.PointerReleased += (x, e) => Basculer(f, id);
            return b;
        }
    }
}
