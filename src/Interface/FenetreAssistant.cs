// Nettoyage PC - fenêtre de l'assistant local (Premium) : questions sur le PC, réponses d'un modèle d'IA qui tourne
// sur le PC (Ollama), et boutons pour les actions proposées (toujours confirmées).
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreAssistant : FenetreTheme
    {
        private static FenetreAssistant ouverte;
        private readonly FenetrePrincipale F;
        private readonly TexteRiche conversation = new TexteRiche { RoleFond = Role.Carte };
        private readonly ZoneTexte saisie = new ZoneTexte(700);
        private readonly Bouton bEnvoyer, bNouvelle;
        private readonly ListeDeroulante cbModele = new ListeDeroulante(240);
        private readonly Etiquette lblEtat;
        private readonly FlowLayoutPanel propositions = new FlowLayoutPanel { AutoSize = false, WrapContents = true, BackColor = Color.Transparent };
        private readonly FlowLayoutPanel suggestions = new FlowLayoutPanel { AutoSize = false, WrapContents = true, BackColor = Color.Transparent };
        private readonly Panel guide = new Panel { BackColor = Color.Transparent, Visible = false };
        private readonly List<MessageAssistant> messages = new List<MessageAssistant>();
        private readonly StringBuilder enCours = new StringBuilder();
        private readonly Timer rafraichir = new Timer { Interval = 90 };
        private string questionEnAttente, contexte;
        private bool occupe, arreter;
        private int debutReponse = -1;

        public static void Ouvrir(FenetrePrincipale f, string question)
        {
            if (ouverte == null || ouverte.IsDisposed) { ouverte = new FenetreAssistant(f); ouverte.Show(f); }
            else { if (ouverte.WindowState == FormWindowState.Minimized) ouverte.WindowState = FormWindowState.Normal; ouverte.Activate(); }
            if (!string.IsNullOrWhiteSpace(question)) ouverte.Poser(question);
        }

        internal static FenetreAssistant PourAutotest(FenetrePrincipale f) { return new FenetreAssistant(f); }

        private FenetreAssistant(FenetrePrincipale f) : base("Assistant", 900, 720, true)
        {
            F = f; MinimumSize = new Size(760, 560); MinimizeBox = true; ShowInTaskbar = true;
            Controls.Add(new Etiquette("Assistant", Theme.Grand, Role.Texte, 20, 14));
            Controls.Add(new Etiquette("Pose ta question sur ton PC, avec tes mots. L'assistant fonctionne sur ton PC (Ollama) : rien n'est envoyé sur Internet. Il propose des actions, tu décides.",
                Theme.Normal, Role.Discret, 22, 52) { MaximumSize = new Size(840, 0) });
            lblEtat = new Etiquette("Recherche d'Ollama...", Theme.PetitGras, Role.Discret, 22, 92);
            Controls.Add(lblEtat);
            cbModele.Location = new Point(624, 86); cbModele.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cbModele.SelectedIndexChanged += (s, e) => { if (cbModele.SelectedItem != null) Assistant.ModeleChoisi = cbModele.SelectedItem.ToString(); };
            Controls.Add(cbModele);
            conversation.Location = new Point(20, 122); conversation.Size = new Size(844, 380); conversation.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(conversation);
            propositions.Location = new Point(20, 508); propositions.Size = new Size(844, 44); propositions.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(propositions);
            suggestions.Location = new Point(20, 556); suggestions.Size = new Size(844, 40); suggestions.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            foreach (var q in new[] { "Pourquoi mon PC est lent ?", "Mon PC est-il bien protégé ?", "Comment libérer de la place ?", "Mon matériel est-il bien réglé pour jouer ?" })
            {
                string question = Langue.T(q);
                var b = new Bouton(q, false, TextRenderer.MeasureText(question, Theme.Petit).Width + 30, 30) { Margin = new Padding(0, 0, 8, 6), Font = Theme.Petit };
                b.Click += (s, e) => Poser(question);
                suggestions.Controls.Add(b);
            }
            Controls.Add(suggestions);
            saisie.Location = new Point(20, 606); saisie.Width = 640; saisie.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            saisie.Font = new Font("Segoe UI", 11);
            Controls.Add(saisie);
            bEnvoyer = new Bouton("Envoyer", true, 110, 32) { Location = new Point(670, 604), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            bEnvoyer.Click += (s, e) => { if (occupe) arreter = true; else Poser(saisie.Text); };
            bNouvelle = new Bouton("Effacer", false, 80, 32) { Location = new Point(786, 604), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            Infobulle.Definir(bNouvelle, "Nouvelle conversation (l'assistant oublie la précédente).");
            bNouvelle.Click += (s, e) => { if (occupe) return; messages.Clear(); conversation.Clear(); propositions.Controls.Clear(); contexte = null; Accueil(); };
            Controls.Add(bEnvoyer); Controls.Add(bNouvelle);
            AcceptButton = bEnvoyer;
            var lienOrion = new Lien("Commandes pour un assistant vocal (ORION...)", 20, 650) { Anchor = AnchorStyles.Bottom | AnchorStyles.Left, Font = Theme.Petit };
            lienOrion.LinkClicked += (s, e) => AideVocale();
            Controls.Add(lienOrion);
            ConstruireGuide();
            Controls.Add(guide);
            ClientSize = new Size(884, 680);
            Theme.Appliquer(this);
            rafraichir.Tick += (s, e) => AfficherEnCours();
            FormClosed += (s, e) => { arreter = true; rafraichir.Stop(); ouverte = null; };
            Shown += (s, e) => { Accueil(); Connecter(); saisie.Focus(); };
        }

        private void Accueil()
        {
            conversation.Ajouter(Langue.T("Assistant"), Theme.PetitGras, Theme.P.AccentVif);
            conversation.Ajouter(Langue.T("Bonjour ! Je connais l'état de ton PC (disque, mémoire, sécurité, bilan). Demande-moi par exemple pourquoi il est lent, ou comment gagner de la place.") + "\n", Theme.Normal, Theme.P.Texte);
        }

        private void ConstruireGuide()
        {
            guide.Location = new Point(20, 122); guide.Size = new Size(844, 470); guide.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            var c = new Carte(844, 300, true) { Location = new Point(0, 0), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
            c.Controls.Add(new Etiquette("Installer le cerveau de l'assistant (gratuit, une seule fois)", Theme.CarteTitre, Role.Texte, 20, 16));
            c.Controls.Add(new Etiquette("L'assistant utilise Ollama, un logiciel gratuit qui fait tourner une intelligence artificielle directement sur ton PC, sans Internet ni compte.\n\n" +
                "1.  Télécharge et installe Ollama depuis son site officiel (bouton ci-dessous).\n" +
                "2.  Ouvre le Terminal de Windows et tape :   ollama pull qwen2.5:7b\n      (sur un PC modeste avec 8 Go de mémoire vive : ollama pull llama3.2:3b)\n" +
                "3.  Reviens ici et clique sur « Réessayer ».", Theme.Normal, Role.Discret, 22, 48) { MaximumSize = new Size(800, 0) });
            var bSite = new Bouton("Ouvrir le site d'Ollama", true, 210, 34) { Location = new Point(20, 240) };
            bSite.Click += (s, e) => Programmes.Ouvrir("https://ollama.com/download");
            var bCopier = new Bouton("Copier la commande", false, 180, 34) { Location = new Point(240, 240) };
            bCopier.Click += (s, e) => { try { Clipboard.SetText("ollama pull qwen2.5:7b"); BoiteMessage.Info("Commande copiée : colle-la dans le Terminal (clic droit)."); } catch { } };
            var bReessayer = new Bouton("Réessayer", false, 120, 34) { Location = new Point(430, 240) };
            bReessayer.Click += (s, e) => Connecter();
            c.Controls.AddRange(new Control[] { bSite, bCopier, bReessayer });
            guide.Controls.Add(c);
        }

        private void Connecter()
        {
            lblEtat.Text = "Recherche d'Ollama..."; lblEtat.RoleTexte = Role.Discret; lblEtat.AppliquerTheme();
            System.Threading.Tasks.Task.Run(() =>
            {
                var m = Assistant.Modeles();
                Invoquer(() =>
                {
                    bool pret = m != null && m.Count > 0;
                    guide.Visible = !pret; guide.BringToFront();
                    conversation.Visible = suggestions.Visible = propositions.Visible = pret;
                    saisie.Enabled = bEnvoyer.Enabled = pret;
                    cbModele.Items.Clear();
                    if (m != null) foreach (var x in m) cbModele.Items.Add(x);
                    cbModele.Visible = pret;
                    if (pret) cbModele.SelectedItem = Assistant.Conseille(m);
                    lblEtat.Text = m == null ? "Ollama n'est pas installé ou pas démarré." : m.Count == 0 ? "Ollama fonctionne, mais aucun modèle n'est installé (étape 2)." : Langue.F("● Prêt, sur ton PC ({0} modèle(s) installé(s)) :", m.Count);
                    lblEtat.RoleTexte = pret ? Role.Vert : Role.Orange; lblEtat.AppliquerTheme();
                    cbModele.Left = Math.Min(ClientSize.Width - cbModele.Width - 20, lblEtat.Right + 10);
                    if (pret && questionEnAttente != null) { string q = questionEnAttente; questionEnAttente = null; Poser(q); }
                });
            });
        }

        private void Invoquer(Action a) { try { if (!IsDisposed && IsHandleCreated) BeginInvoke(a); } catch { } }

        public void Poser(string question)
        {
            question = (question ?? "").Trim();
            if (question == "") return;
            if (occupe) return;
            if (!saisie.Enabled || cbModele.SelectedItem == null) { questionEnAttente = question; return; }
            saisie.Text = "";
            propositions.Controls.Clear();
            conversation.Ajouter(Langue.T("Toi"), Theme.PetitGras, Theme.P.Discret);
            conversation.Ajouter(question + "\n", Theme.Normal, Theme.P.Texte);
            conversation.Ajouter(Langue.T("Assistant"), Theme.PetitGras, Theme.P.AccentVif);
            debutReponse = conversation.TextLength;
            conversation.Ajouter(Langue.T("Je réfléchis..."), Theme.Normal, Theme.P.Discret);
            messages.Add(new MessageAssistant { Role = "user", Texte = question });
            occupe = true; arreter = false; bEnvoyer.Text = "Arrêter";
            enCours.Clear();
            string modele = cbModele.SelectedItem.ToString();
            var points = BilanActuel();
            rafraichir.Start();
            System.Threading.Tasks.Task.Run(() =>
            {
                string reponse = null, erreur = null;
                try
                {
                    if (contexte == null) contexte = Assistant.Contexte(points);
                    reponse = Assistant.Discuter(modele, contexte, messages.Skip(Math.Max(0, messages.Count - 12)).ToList(), m => { lock (enCours) enCours.Append(m); }, () => arreter);
                }
                catch (Exception ex) { erreur = ex.Message; }
                Invoquer(() => Fin(reponse, erreur));
            });
        }

        private List<string> BilanActuel()
        {
            try
            {
                var a = F.Pages.ContainsKey("Accueil") ? F.Pages["Accueil"] as PageAccueil : null;
                return a == null ? new List<string>() : a.Points.Where(p => p.Etat != "ok").Select(p => p.Texte).Take(12).ToList();
            }
            catch { return new List<string>(); }
        }

        private void AfficherEnCours()
        {
            if (debutReponse < 0) return;
            string t; lock (enCours) t = enCours.ToString();
            if (t == "") return;
            Remplacer(Assistant.SansBalises(t), Theme.P.Texte);
        }

        private void Remplacer(string texte, Color couleur)
        {
            conversation.SuspendLayout();
            conversation.Select(debutReponse, conversation.TextLength - debutReponse);
            conversation.ReadOnly = false;
            conversation.SelectionFont = Theme.Normal; conversation.SelectionColor = couleur;
            conversation.SelectedText = texte + "\n\n";
            conversation.ReadOnly = true;
            conversation.SelectionStart = conversation.TextLength;
            conversation.ScrollToCaret();
            conversation.ResumeLayout();
        }

        private void Fin(string reponse, string erreur)
        {
            rafraichir.Stop();
            occupe = false; bEnvoyer.Text = "Envoyer";
            if (erreur != null)
            {
                Remplacer(Langue.T("Je n'ai pas pu répondre : ") + erreur + "\n" + Langue.T("Vérifie qu'Ollama est bien démarré (icône près de l'horloge), puis réessaie."), Theme.P.Orange);
                if (messages.Count > 0) messages.RemoveAt(messages.Count - 1);
                return;
            }
            reponse = reponse ?? "";
            if (arreter && reponse == "") reponse = Langue.T("(réponse arrêtée)");
            Remplacer(Assistant.SansBalises(reponse) == "" ? Langue.T("(pas de réponse)") : Assistant.SansBalises(reponse), Theme.P.Texte);
            messages.Add(new MessageAssistant { Role = "assistant", Texte = reponse });
            debutReponse = -1;
            foreach (var p in Assistant.Propositions(reponse)) AjouterProposition(p.Item1, p.Item2);
            Theme.Appliquer(propositions);
            saisie.Focus();
        }

        private void AjouterProposition(string type, string cle)
        {
            string texte;
            if (type == "action") { ActionMoteur a; texte = "▶  " + (F.Actions.TryGetValue(cle, out a) ? a.Titre : cle); }
            else if (type == "page") texte = "→  " + Langue.T("Ouvrir") + " : " + Langue.T(F.Pages.ContainsKey(cle) ? F.Pages[cle].NomOnglet : cle);
            else texte = "→  " + Langue.T(Assistant.OutilsPermis[cle].Split('(')[0].Trim());
            var b = new Bouton(texte, type == "action", TextRenderer.MeasureText(Langue.T(texte), Theme.Normal).Width + 36, 34) { Margin = new Padding(0, 4, 8, 4) };
            b.Click += (s, e) =>
            {
                if (type == "action")
                {
                    ActionMoteur a;
                    if (!F.Actions.TryGetValue(cle, out a)) return;
                    F.LancerAction(cle, null, Langue.F("L'assistant propose : « {0} ».", a.Titre) + "\n\n" + Langue.T(a.Confirmer ?? "Lancer cette action maintenant ?"));
                    F.Activate();
                }
                else if (type == "page") { F.AfficherPage(cle); F.Activate(); }
                else F.OuvrirOutil(cle);
            };
            propositions.Controls.Add(b);
        }

        private void AideVocale()
        {
            string exe = "\"" + Chemins.Exe + "\"";
            BoiteMessage.Afficher(Langue.T("Un assistant vocal (comme ORION) peut piloter Nettoyage PC avec ces commandes :") + "\n\n" +
                exe + " --etat\n   " + Langue.T("écrit l'état du PC (disque, mémoire, sécurité) au format JSON") + "\n\n" +
                exe + " --assistant-texte \"" + Langue.T("ma question") + "\"\n   " + Langue.T("écrit la réponse de l'assistant (rien n'est exécuté)") + "\n\n" +
                exe + " --assistant \"" + Langue.T("ma question") + "\"\n   " + Langue.T("ouvre l'assistant avec la question") + "\n\n" +
                exe + " --page Nettoyage   |   --ouvrir bouclier\n   " + Langue.T("ouvre une page ou un outil") + "\n\n" +
                exe + " --action standard\n   " + Langue.T("prépare une action : elle doit toujours être confirmée devant le PC") + "\n\n" +
                Langue.T("Actions possibles : ") + string.Join(", ", Assistant.ActionsPermises.Keys), "Commandes pour un assistant vocal", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
