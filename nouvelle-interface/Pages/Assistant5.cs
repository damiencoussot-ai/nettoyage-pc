// Nettoyage PC 5 - assistant local (Premium) : questions sur le PC, réponses d'un modèle d'IA qui tourne sur le PC
// (Ollama, rien ne part sur Internet). Il ne peut proposer que des actions, rubriques et outils d'une liste fixe
// (moteur), et chaque action proposée passe par la question habituelle avant de modifier quoi que ce soit.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class Assistant5 : StackPanel
    {
        private static Assistant5 ouverte;
        private readonly FenetrePrincipale F;
        private readonly TextBlock tEtat;
        private readonly ComboBox cbModele = new ComboBox { MinWidth = 220 };
        private readonly StackPanel fil = new StackPanel { Spacing = 10 };
        private readonly ScrollViewer defilement;
        private readonly WrapPanel propositions = new WrapPanel { Orientation = Orientation.Horizontal };
        private readonly WrapPanel suggestions = new WrapPanel { Orientation = Orientation.Horizontal };
        private readonly TextBox saisie = new TextBox { Watermark = "Ta question…", AcceptsReturn = false };
        private readonly BoutonNoyau bEnvoyer, bEffacer;
        private readonly Border guide, zoneConversation;
        private readonly List<MessageAssistant> messages = new List<MessageAssistant>();
        private readonly StringBuilder enCours = new StringBuilder();
        private readonly DispatcherTimer rafraichir = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(90) };
        private SelectableTextBlock reponse;
        private string questionEnAttente, contexte;
        private bool occupe, pret, fermee;
        private volatile bool arreter;

        /// <summary>(captures) ouvre la fenêtre sans vérifier la licence.</summary>
        public static bool Essai;

        public static void Ouvrir(FenetrePrincipale f, string question = null)
        {
            if (!Licence.Premium && !Essai) { Achat5.Ouvrir(f); return; }
            if (ouverte == null)
            {
                ouverte = new Assistant5(f);
                f.Ouvrir("Assistant", ouverte, 920);
            }
            if (!string.IsNullOrWhiteSpace(question)) ouverte.Poser(question);
        }

        private Assistant5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(Premium5.Intro("Pose ta question sur ton PC, avec tes mots. L'assistant fonctionne sur ton PC (Ollama) : rien n'est envoyé sur Internet. Il propose des actions, tu décides."));
            var etat = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            tEtat = new TextBlock { Text = Langue.T("Recherche d'Ollama…"), FontFamily = Habillage.Corps, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            etat.Children.Add(tEtat);
            cbModele.IsVisible = false;
            cbModele.SelectionChanged += (s, e) => { if (cbModele.SelectedItem != null) Assistant.ModeleChoisi = cbModele.SelectedItem.ToString(); };
            Grid.SetColumn(cbModele, 1); etat.Children.Add(cbModele);
            Children.Add(etat);

            // guide d'installation d'Ollama (quand il manque)
            var g = new StackPanel { Spacing = 10 };
            g.Children.Add(Premium5.Titre("Installer le cerveau de l'assistant (gratuit, une seule fois)"));
            g.Children.Add(new TextBlock
            {
                Text = Langue.T("L'assistant utilise Ollama, un logiciel gratuit qui fait tourner une intelligence artificielle directement sur ton PC, sans Internet ni compte.\n\n" +
                    "1.  Télécharge et installe Ollama depuis son site officiel (bouton ci-dessous).\n" +
                    "2.  Ouvre le Terminal de Windows et tape :   ollama pull qwen2.5:7b\n      (sur un PC modeste avec 8 Go de mémoire vive : ollama pull llama3.2:3b)\n" +
                    "3.  Reviens ici et clique sur « Réessayer »."),
                FontFamily = Habillage.Corps, FontSize = 13.5, LineHeight = 21, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap,
            });
            var bg = new WrapPanel { Orientation = Orientation.Horizontal };
            var bSite = new BoutonNoyau("Ouvrir le site d'Ollama", true, 0, 34) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 10, 6) };
            bSite.Clic += () => Programmes.Ouvrir("https://ollama.com/download");
            var bCopier = new BoutonNoyau("Copier la commande", false, 0, 34) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bCopier.Clic += async () =>
            {
                try { var top = TopLevel.GetTopLevel(this); if (top != null && top.Clipboard != null) await top.Clipboard.SetTextAsync("ollama pull qwen2.5:7b"); F.Statut("Commande copiée : colle-la dans le Terminal (clic droit)."); } catch { }
            };
            var bReessayer = new BoutonNoyau("Réessayer", false, 0, 34) { TaillePolice = 13, Margin = new Thickness(0, 0, 10, 6) };
            bReessayer.Clic += Connecter;
            bg.Children.Add(bSite); bg.Children.Add(bCopier); bg.Children.Add(bReessayer);
            g.Children.Add(bg);
            guide = Premium5.Carte(g, Habillage.Alpha(P.Accent, 90));
            guide.IsVisible = false;
            Children.Add(guide);

            // conversation
            defilement = new ScrollViewer { Content = new Border { Child = fil, Padding = new Thickness(16, 12) }, Height = 340, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
            zoneConversation = new Border { Child = defilement, CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1) };
            Children.Add(zoneConversation);
            Children.Add(propositions);
            foreach (var q in new[] { "Pourquoi mon PC est lent ?", "Mon PC est-il bien protégé ?", "Comment libérer de la place ?", "Mon matériel est-il bien réglé pour jouer ?" })
            {
                string question = Langue.T(q);
                var b = new BoutonNoyau(question, false, 0, 30) { TaillePolice = 12.5, Margin = new Thickness(0, 0, 8, 6) };
                b.Clic += () => Poser(question);
                suggestions.Children.Add(b);
            }
            Children.Add(suggestions);
            var bas = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
            saisie.Watermark = Langue.T("Ta question…");
            saisie.KeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; Envoyer(); } };
            bas.Children.Add(saisie);
            bEnvoyer = new BoutonNoyau("Envoyer", true, 120, 34) { TaillePolice = 13.5, Margin = new Thickness(10, 0, 0, 0) };
            bEnvoyer.Clic += Envoyer;
            Grid.SetColumn(bEnvoyer, 1); bas.Children.Add(bEnvoyer);
            bEffacer = new BoutonNoyau("Effacer", false, 100, 34) { TaillePolice = 13, Margin = new Thickness(10, 0, 0, 0) };
            ToolTip.SetTip(bEffacer, Langue.T("Nouvelle conversation (l'assistant oublie la précédente)."));
            bEffacer.Clic += () => { if (occupe) return; messages.Clear(); fil.Children.Clear(); propositions.Children.Clear(); contexte = null; Accueil(); };
            Grid.SetColumn(bEffacer, 2); bas.Children.Add(bEffacer);
            Children.Add(bas);
            rafraichir.Tick += (s, e) => AfficherEnCours();
            Accueil();
            MontrerPret(false);
            AttachedToVisualTree += (o, e) => { Connecter(); saisie.Focus(); };
            // fenêtre fermée (bouton Fermer, changement de thème...) : la réponse en cours s'arrête
            DetachedFromVisualTree += (o, e) => { fermee = true; arreter = true; rafraichir.Stop(); if (ouverte == this) ouverte = null; };
        }

        private void Bulle(string qui, string texte, Color couleurNom, out SelectableTextBlock corps, Color? couleurTexte = null)
        {
            var s = new StackPanel { Spacing = 2 };
            s.Children.Add(new TextBlock { Text = Langue.T(qui), FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(couleurNom) });
            corps = new SelectableTextBlock { Text = texte, FontFamily = Habillage.Corps, FontSize = 14, LineHeight = 21, Foreground = Habillage.Pinceau(couleurTexte ?? Habillage.P.Texte), TextWrapping = TextWrapping.Wrap };
            s.Children.Add(corps);
            fil.Children.Add(s);
            Dispatcher.UIThread.Post(() => defilement.ScrollToEnd(), DispatcherPriority.Background);
        }

        private void Accueil()
        {
            SelectableTextBlock t;
            Bulle("Assistant", Langue.T("Bonjour ! Je connais l'état de ton PC (disque, mémoire, sécurité, bilan). Demande-moi par exemple pourquoi il est lent, ou comment gagner de la place."), Habillage.P.Accent, out t);
        }

        private void MontrerPret(bool p)
        {
            pret = p;
            guide.IsVisible = !p;
            zoneConversation.IsVisible = suggestions.IsVisible = propositions.IsVisible = p;
            saisie.IsEnabled = p; bEnvoyer.IsEnabled = p; bEffacer.IsEnabled = p;
            cbModele.IsVisible = p;
        }

        private void Connecter()
        {
            var P = Habillage.P;
            tEtat.Text = Langue.T("Recherche d'Ollama…"); tEtat.Foreground = Habillage.Pinceau(P.Discret);
            Task.Run(() => { try { return Assistant.Modeles(); } catch { return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                if (fermee) return;
                var m = t.Result;
                bool ok = m != null && m.Count > 0;
                cbModele.Items.Clear();
                if (m != null) foreach (var x in m) cbModele.Items.Add(x);
                if (ok) cbModele.SelectedItem = Assistant.Conseille(m);
                MontrerPret(ok);
                tEtat.Text = m == null ? Langue.T("Ollama n'est pas installé ou pas démarré.") : m.Count == 0 ? Langue.T("Ollama fonctionne, mais aucun modèle n'est installé (étape 2).") : Langue.F("● Prêt, sur ton PC ({0} modèle(s) installé(s)) :", m.Count);
                tEtat.Foreground = Habillage.Pinceau(ok ? P.Vert : P.Orange);
                if (ok && questionEnAttente != null) { string q = questionEnAttente; questionEnAttente = null; Poser(q); }
            }));
        }

        private void Envoyer() { if (occupe) arreter = true; else Poser(saisie.Text); }

        private List<string> BilanActuel()
        {
            try { var b = F.Bilan; return b == null ? new List<string>() : b.Points.Where(p => p.Etat != EtatPoint.Ok).Select(p => p.Texte).Take(12).ToList(); }
            catch { return new List<string>(); }
        }

        public void Poser(string question)
        {
            var P = Habillage.P;
            question = (question ?? "").Trim();
            if (question == "" || occupe) return;
            if (!pret || cbModele.SelectedItem == null) { questionEnAttente = question; return; }
            saisie.Text = "";
            propositions.Children.Clear();
            SelectableTextBlock t;
            Bulle("Toi", question, P.Discret, out t);
            Bulle("Assistant", Langue.T("Je réfléchis…"), P.Accent, out reponse, P.Discret);
            messages.Add(new MessageAssistant { Role = "user", Texte = question });
            occupe = true; arreter = false; bEnvoyer.Changer(Langue.T("Arrêter"));
            lock (enCours) enCours.Clear();
            string modele = cbModele.SelectedItem.ToString();
            var points = BilanActuel();
            var historique = messages.Skip(Math.Max(0, messages.Count - 12)).ToList();
            rafraichir.Start();
            string erreur = null;
            Task.Run(() =>
            {
                try
                {
                    if (contexte == null) contexte = Assistant.Contexte(points);
                    return Assistant.Discuter(modele, contexte, historique, m => { lock (enCours) enCours.Append(m); }, () => arreter);
                }
                catch (Exception ex) { erreur = ex.Message; return null; }
            }).ContinueWith(x => Dispatcher.UIThread.Post(() => Fin(x.Result, erreur)));
        }

        private void AfficherEnCours()
        {
            if (reponse == null) return;
            string t; lock (enCours) t = enCours.ToString();
            if (t == "") return;
            reponse.Text = Assistant.SansBalises(t); reponse.Foreground = Habillage.Pinceau(Habillage.P.Texte);
            defilement.ScrollToEnd();
        }

        private void Fin(string texte, string erreur)
        {
            var P = Habillage.P;
            rafraichir.Stop();
            occupe = false; bEnvoyer.Changer(Langue.T("Envoyer"));
            if (fermee || reponse == null) return;
            if (erreur != null)
            {
                reponse.Text = Langue.T("Je n'ai pas pu répondre : ") + erreur + "\n" + Langue.T("Vérifie qu'Ollama est bien démarré (icône près de l'horloge), puis réessaie.");
                reponse.Foreground = Habillage.Pinceau(P.Orange);
                if (messages.Count > 0) messages.RemoveAt(messages.Count - 1);
                reponse = null;
                return;
            }
            texte = texte ?? "";
            if (arreter && texte == "") texte = Langue.T("(réponse arrêtée)");
            string propre = Assistant.SansBalises(texte);
            reponse.Text = propre == "" ? Langue.T("(pas de réponse)") : propre; reponse.Foreground = Habillage.Pinceau(P.Texte);
            messages.Add(new MessageAssistant { Role = "assistant", Texte = texte });
            reponse = null;
            foreach (var p in Assistant.Propositions(texte)) AjouterProposition(p.Item1, p.Item2);
            defilement.ScrollToEnd();
            saisie.Focus();
        }

        private void AjouterProposition(string type, string cle)
        {
            string texte;
            ActionMoteur a = null;
            if (type == "action") texte = "▶  " + (F.Actions.TryGetValue(cle, out a) ? a.Titre : cle);
            else if (type == "page")
            {
                var o = FenetrePrincipale.Onglets5.FirstOrDefault(x => x[1] == cle);
                texte = "→  " + Langue.T("Ouvrir") + " : " + Langue.T(o != null ? o[2] : cle);
            }
            else texte = "→  " + Langue.T(Assistant.OutilsPermis[cle].Split('(')[0].Trim());
            var b = new BoutonNoyau(texte, type == "action", 0, 34) { TaillePolice = 13, Margin = new Thickness(0, 4, 8, 4) };
            b.Clic += () =>
            {
                if (type == "action")
                {
                    if (a == null) return;
                    // la question habituelle de l'action, précédée de qui la propose (en simulation : rien n'est modifié)
                    F.LancerAction(cle, null, Langue.F("L'assistant propose : « {0} ».", a.Titre) + "\n\n" + Langue.T(a.Confirmer ?? "Lancer cette action maintenant ?"));
                }
                else if (type == "page") F.Executer("page:" + cle, texte);
                else F.OuvrirOutil(cle);
            };
            propositions.Children.Add(b);
        }
    }
}
