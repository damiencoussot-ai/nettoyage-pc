// Nettoyage PC - Premium : page de présentation (bandeau, garanties, outils, comparatif, questions), achat, essai gratuit et clé,
// et les outils pro (analyse du démarrage, mesure des performances, kit de réinstallation ; les autres sont dans OutilsPremium.cs)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class PagePremium : Page
    {
        private readonly BandeauPremium bandeau;
        private readonly RangeeGaranties garanties = new RangeeGaranties();
        private readonly Etiquette titreOutils, titreComparatif, titreFaq, faq;
        private readonly List<CarteOutilPremium> cartes = new List<CarteOutilPremium>();
        private readonly TableauComparatif tableau = new TableauComparatif();
        private readonly List<Lien> liens = new List<Lien>();
        private readonly Etiquette fin;

        public PagePremium(FenetrePrincipale f) : base(f, "Premium", "Premium", "Les outils des pros : gardien, enquêteur, santé prédictive, assistant, FPS, sauvegarde automatique.", 0xE734)
        {
            bandeau = new BandeauPremium(f);
            Controls.Add(bandeau);
            Controls.Add(garanties);
            titreOutils = Titre("8 outils de pro, débloqués d'un coup", 28, 0);
            Outil(0xEA18, "Gardien en temps réel", "Veille sur ton PC en continu : programme ajouté au démarrage, disque presque plein (avec nettoyage léger automatique), antivirus coupé, mémoire saturée, surchauffe.",
                "Régler le gardien", () => FenetreGardien.Ouvrir(F), () => Gardien.EnCours ? "● Actif sur ce PC" : ReglagesGardien.Lire().Actif ? "● Activé" : "");
            Outil(0xE721, "Pourquoi mon PC ralentit ?", "L'enquêteur retrace tout ce qui a changé (applications, pilotes, mises à jour, démarrage, réglages) et le compare aux performances : il désigne le coupable.",
                "Enquêter", () => FenetreEnqueteur.Ouvrir(F), null);
            Outil(0xE95E, "Santé prédictive", "Repère les signes qui annoncent une panne avant qu'elle arrive : erreurs du matériel et du disque, usure des SSD, batterie, chauffe. Avec la tendance.",
                "Vérifier", () => FenetreSante.Ouvrir(F), null);
            Outil(0xE99A, "Assistant local", "Pose tes questions sur ton PC avec tes mots. L'IA tourne sur ton PC (Ollama) : rien ne part sur Internet. Compatible assistant vocal (ORION).",
                "Discuter", () => FenetreAssistant.Ouvrir(F, null), null);
            Outil(0xE7FC, "Preuve en FPS", "Mesure les images par seconde réelles de tes jeux (moyenne, 1 % et 0,1 % les plus lents) avant et après un réglage : le vrai gain, en chiffres.",
                "Mesurer", () => FenetreFps.Ouvrir(F), null);
            Outil(0xE945, "Coût en électricité", "Combien ton PC te coûte chaque mois en euros, quelles applications consomment le plus, et comment payer moins.",
                "Voir", () => FenetreEnergie.Ouvrir(F), () => Gardien.EnCours && ReglagesGardien.Lire().Energie ? "● Mesure en cours" : "");
            Outil(0xE895, "Sauvegarde automatique", "Dès que ta clé USB ou ton disque externe est branché, les nouvelles photos et les documents modifiés sont copiés. Rien n'est jamais effacé de la sauvegarde.",
                "Configurer", () => FenetreGardien.Ouvrir(F, true), () => ReglagesGardien.Lire().SauvegardeAuto ? "● Activée" : "");
            Outil(0xE7BA, "Analyse des plantages", "Écrans bleus, arrêts brutaux, applications qui plantent, erreurs matérielles : la cause expliquée simplement, et quoi faire.",
                "Analyser", () => new FenetrePlantages(F).Show(F), null);
            Outil(0xE916, "Analyse du démarrage", "Combien de temps met ton PC à démarrer, et qui le ralentit : le pilote, le service ou le programme précis, sur les 90 derniers jours.",
                "Analyser", () => new FenetreAnalyseDemarrage(F).Show(F), null);
            Outil(0xEC4A, "Mesure des performances", "Processeur, mémoire vive et disque mesurés en chiffres. Compare avant / après un réglage (EXPO, pilote, nettoyage) pour voir le vrai gain.",
                "Mesurer", () => { using (var w = new FenetreBenchmark(F)) w.ShowDialog(F); }, null);
            Outil(0xE88E, "Kit de réinstallation", "Avant de réinstaller Windows ou de changer de PC : tes applications (réinstallées d'un coup), tes pilotes et tes réseaux Wi-Fi, avec un script qui remet tout en place.",
                "Préparer le kit", () => FenetreKit.Ouvrir(F), null);
            Outil(0xE83F, "Santé de la batterie", "Pour les portables : usure de la batterie, nombre de cycles, autonomie réelle comparée à celle d'origine, et conseils pour la faire durer.",
                "Vérifier", () => { using (var w = new FenetreBatterie()) w.ShowDialog(F); }, null);
            Outil(0xE790, "Thèmes de couleur", "Cinq couleurs exclusives pour l'interface : Océan, Émeraude, Améthyste, Coucher de soleil et Rubis, en clair comme en sombre.",
                "Choisir", () => F.AfficherPage("Reglages"), () => Theme.AccentActuel != "aurore" ? "● " + Langue.T(Theme.Accents.First(a => a.Cle == Theme.AccentActuel).Nom) : "");
            titreOutils.Text = Langue.F("{0} outils de pro, débloqués d'un coup", cartes.Count);
            titreComparatif = Titre("Gratuit ou Premium ?", 28, 0);
            Controls.Add(tableau);
            titreFaq = Titre("Questions fréquentes", 28, 0);
            faq = Texte("", 30, 0, Role.Discret);
            foreach (var kv in new[] { new[] { "Conditions de vente", "CONDITIONS-DE-VENTE" }, new[] { "Contrat de licence", "LICENCE" }, new[] { "Confidentialité", "CONFIDENTIALITE" }, new[] { "Mentions légales", "MENTIONS-LEGALES" } })
            {
                string cle = kv[1];
                var l = new Lien(kv[0], 30, 0);
                l.LinkClicked += (s, e) => FenetreLegal.Ouvrir(F, cle);
                Controls.Add(l); liens.Add(l);
            }
            fin = Texte(" ", 30, 0);
            Licence.Change += () => { try { if (IsHandleCreated) BeginInvoke(new Action(Actualiser)); else Actualiser(); } catch { } };
            Resize += (s, e) => Disposer();
            Actualiser();
        }

        public override void Montrer() { Actualiser(); }
        public override void ThemeChange() { Actualiser(); }

        private void Outil(int glyphe, string titre, string texte, string bouton, Action ouvrir, Func<string> etat)
        {
            var c = new CarteOutilPremium(glyphe, titre, texte, bouton, etat);
            c.Bouton.Click += (s, e) => { if (!Licence.Premium) { FenetreAchat.Ouvrir(F); return; } ouvrir(); Actualiser(); };
            Controls.Add(c); cartes.Add(c);
        }

        private void Actualiser()
        {
            bandeau.Actualiser();
            faq.Text = string.Join("\n\n", new[]
            {
                "Et si ça ne me convient pas ?  Essaie d'abord gratuitement 7 jours. Après l'achat, tu es satisfait ou remboursé pendant 14 jours, sans justification.",
                Langue.F("Est-ce un abonnement ?  Non : tu payes {0} une seule fois, et Premium reste actif pour toutes les versions 4.", Boutique.Prix),
                Langue.F("Sur combien de PC ?  {0} en même temps. Avant de donner un PC, « Gérer la licence » libère sa place.", Boutique.NombrePc),
                "Faut-il Internet ?  Pour activer la clé, puis une petite vérification par semaine. Sans Internet, Premium reste actif 45 jours.",
                "Qu'est-ce qui reste gratuit ?  Tout le reste : nettoyage, sécurité, mises à jour, sauvegarde, jeux, aide à distance."
            }.Select(Langue.T));
            foreach (var c in cartes) c.Actualiser();
            Disposer();
        }

        private void Disposer()
        {
            int oy = AutoScrollPosition.Y;
            int w = Math.Max(300, ClientSize.Width - 56);
            bandeau.Width = w; bandeau.AjusterHauteur();
            bandeau.Location = new Point(28, 6 + oy);
            garanties.Width = w; garanties.AjusterHauteur();
            garanties.Location = new Point(28, bandeau.Bottom + 14);
            titreOutils.Location = new Point(28, garanties.Bottom + 22);
            int colonnes = Math.Max(1, Math.Min(4, (w + 16) / (290 + 16)));
            int lc = (w - (colonnes - 1) * 16) / colonnes;
            int y = titreOutils.Bottom + 12;
            for (int debut = 0; debut < cartes.Count; debut += colonnes)
            {
                var rang = cartes.Skip(debut).Take(colonnes).ToList();
                foreach (var c in rang) { c.Imposer(0); c.Width = lc; }
                int hRang = rang.Max(c => c.HauteurNaturelle);
                for (int k = 0; k < rang.Count; k++) { rang[k].Imposer(hRang); rang[k].Location = new Point(28 + k * (lc + 16), y); }
                y += hRang + 16;
            }
            y += 14;
            titreComparatif.Location = new Point(28, y);
            tableau.Width = Math.Min(w, 760); tableau.Location = new Point(28, titreComparatif.Bottom + 12);
            titreFaq.Location = new Point(28, tableau.Bottom + 30);
            faq.MaximumSize = new Size(w, 0);
            faq.Location = new Point(30, titreFaq.Bottom + 10);
            int x = 30;
            foreach (var l in liens) { l.Location = new Point(x, faq.Bottom + 22); x = l.Right + 22; }
            fin.Location = new Point(30, (liens.Count > 0 ? liens[0].Bottom : faq.Bottom) + 12);
        }
    }

    // ================================================================ Bandeau du haut (dessiné entièrement : dégradé, prix, boutons)
    internal class BandeauPremium : Control, IThemable
    {
        private class Zone { public Rectangle R; public string Texte; public int Style; public Action Clic; }   // style : 0 blanc, 1 verre, 2 lien
        private readonly FenetrePrincipale F;
        private readonly List<Zone> zones = new List<Zone>();
        private Zone survol;
        private string pastille = "", titre = "", texte = "";
        private bool prix;
        private static readonly Font PoliceTitre = new Font("Segoe UI Semibold", 22), PolicePrix = new Font("Segoe UI Semibold", 30), PoliceBouton = new Font("Segoe UI Semibold", 10.5f);

        public BandeauPremium(FenetrePrincipale f)
        {
            F = f; Height = 230; Cursor = Cursors.Default;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }

        public void AppliquerTheme() { Invalidate(); }

        public void Actualiser()
        {
            zones.Clear();
            string qui = Licence.Titulaire != "" ? Licence.Titulaire : Licence.Courriel;
            if (Licence.Premium && !Licence.EnEssai)
            {
                pastille = "★  PREMIUM ACTIF"; titre = "Merci pour ton soutien !"; prix = false;
                texte = (qui != "" ? Langue.F(Licence.Type == "offerte" ? "Licence de {0} (clé offerte)." : "Licence de {0}.", qui) + " " : "") + Langue.T("Tous les outils Premium sont débloqués sur ce PC, pour toutes les versions 4.");
                zones.Add(new Zone { Texte = "Gérer la licence", Style = 1, Clic = () => PageLicence.Gerer(F) });
            }
            else
            {
                prix = true;
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
                if (!Licence.EnEssai && Licence.EssaiDisponible) zones.Add(new Zone { Texte = "Essayer gratuitement 7 jours", Style = 0, Clic = () => FenetreAchat.LancerEssai(F) });
                zones.Add(new Zone { Texte = Langue.F("Acheter  ·  {0}", Boutique.Prix), Style = zones.Count == 0 ? 0 : 1, Clic = () => FenetreAchat.Ouvrir(F) });
                zones.Add(new Zone { Texte = "J'ai une clé", Style = 2, Clic = () => FenetreAchat.Ouvrir(F, true) });
            }
            foreach (var z in zones) z.Texte = Langue.T(z.Texte);
            pastille = Langue.T(pastille); titre = Langue.T(titre);
            AjusterHauteur();
            Invalidate();
        }

        private int LargeurTexte { get { return Math.Max(240, Width - (prix ? 300 : 80)); } }

        public void AjusterHauteur()
        {
            int ht = TextRenderer.MeasureText(texte, Theme.Normal, new Size(LargeurTexte, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            int h = 100 + ht + 30 + 42 + 28;
            if (Height != h) Height = h;
            // boutons en bas à gauche
            int x = 30, yb = Height - 42 - 26;
            foreach (var z in zones)
            {
                int l = TextRenderer.MeasureText(z.Texte, PoliceBouton).Width + (z.Style == 2 ? 16 : 44);
                z.R = new Rectangle(x, yb, l, 42); x += l + 12;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var z = zones.FirstOrDefault(x => x.R.Contains(e.Location));
            if (z != survol) { survol = z; Cursor = z != null ? Cursors.Hand : Cursors.Default; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e) { survol = null; Cursor = Cursors.Default; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseClick(MouseEventArgs e)
        {
            var z = zones.FirstOrDefault(x => x.R.Contains(e.Location));
            if (z != null && z.Clic != null) z.Clic();
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Width, h = Height;
            if (w < 20 || h < 20) return;
            Color c1 = Theme.EstSombre ? Theme.Melanger(P.Lueur2, Color.Black, 0.52) : Theme.Melanger(P.Lueur2, Color.Black, 0.10);
            Color c2 = Theme.EstSombre ? Theme.Melanger(P.Lueur1, Color.Black, 0.58) : Theme.Melanger(P.Lueur1, Color.Black, 0.15);
            using (var ch = Theme.CheminArrondi(0.5f, 0.5f, w - 1.5f, h - 1.5f, 16))
            {
                using (var b = new LinearGradientBrush(new Rectangle(0, 0, w, h), c1, c2, 20f)) g.FillPath(b, ch);
                g.SetClip(ch);
                using (var b = new SolidBrush(Color.FromArgb(22, 255, 255, 255))) { g.FillEllipse(b, w - 260, -120, 380, 380); g.FillEllipse(b, w - 140, h - 110, 220, 220); }
                using (var b = new SolidBrush(Color.FromArgb(14, 255, 255, 255))) g.FillEllipse(b, w * 0.45f, -h * 0.9f, h * 1.4f, h * 1.4f);
                g.ResetClip();
                using (var p = new Pen(Color.FromArgb(60, 255, 255, 255))) g.DrawPath(p, ch);
            }
            var blanc = Color.White; var blancDoux = Color.FromArgb(225, 255, 255, 255);
            // pastille
            var tp = TextRenderer.MeasureText(pastille, Theme.PetitGras);
            Theme.RemplirArrondi(g, Color.FromArgb(46, 255, 255, 255), 30, 24, tp.Width + 22, 24, 12);
            Theme.Texte(g, pastille, Theme.PetitGras, new Rectangle(30, 24, tp.Width + 22, 24), blanc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            Theme.Texte(g, titre, PoliceTitre, new Rectangle(28, 54, LargeurTexte, 44), blanc, TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            Theme.Texte(g, texte, Theme.Normal, new Rectangle(30, 100, LargeurTexte, h - 100 - 82), blancDoux, TextFormatFlags.Left | TextFormatFlags.WordBreak);
            if (prix)
            {
                var r = new Rectangle(w - 260, 34, 232, 50);
                Theme.Texte(g, Boutique.Prix, PolicePrix, r, blanc, TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                Theme.Texte(g, "TTC  ·  paiement unique, sans abonnement", Theme.Petit, new Rectangle(w - 300, 88, 272, 18), blancDoux, TextFormatFlags.Right | TextFormatFlags.SingleLine);
                Theme.Texte(g, Langue.F("{0} PC  ·  toutes les versions 4", Boutique.NombrePc), Theme.Petit, new Rectangle(w - 300, 106, 272, 18), blancDoux, TextFormatFlags.Right | TextFormatFlags.SingleLine);
                Theme.Texte(g, "Satisfait ou remboursé 14 jours", Theme.PetitGras, new Rectangle(w - 300, 128, 272, 18), blanc, TextFormatFlags.Right | TextFormatFlags.SingleLine);
            }
            foreach (var z in zones)
            {
                bool s = z == survol;
                var r = z.R;
                if (z.Style == 0)
                {
                    Theme.RemplirArrondi(g, s ? Color.FromArgb(235, 255, 255, 255) : Color.White, r.X, r.Y, r.Width, r.Height, 11);
                    Theme.Texte(g, z.Texte, PoliceBouton, r, Theme.Melanger(P.Lueur2, Color.Black, 0.35), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
                else if (z.Style == 1)
                {
                    Theme.RemplirArrondi(g, Color.FromArgb(s ? 70 : 38, 255, 255, 255), r.X, r.Y, r.Width, r.Height, 11);
                    Theme.Contour(g, Color.FromArgb(140, 255, 255, 255), r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1, 11);
                    Theme.Texte(g, z.Texte, PoliceBouton, r, blanc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
                else
                {
                    using (var police = new Font(Theme.Normal, s ? FontStyle.Underline : FontStyle.Regular))
                        Theme.Texte(g, z.Texte, police, r, blanc, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
            }
        }
    }

    // ================================================================ Garanties (rangée de pastilles)
    internal class RangeeGaranties : Control, IThemable
    {
        private static readonly string[] Elements = { "Paiement sécurisé (Lemon Squeezy)", "Satisfait ou remboursé 14 jours", "Sans abonnement", "3 PC, toutes les versions 4", "Ni publicité, ni pistage" };
        private readonly List<Rectangle> rects = new List<Rectangle>();

        public RangeeGaranties()
        {
            Height = 34;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        public void AppliquerTheme() { Invalidate(); }

        public void AjusterHauteur()
        {
            rects.Clear();
            int x = 0, y = 0;
            foreach (var t in Elements)
            {
                int l = TextRenderer.MeasureText(Langue.T(t), Theme.Petit).Width + 42;
                if (x > 0 && x + l > Width) { x = 0; y += 38; }
                rects.Add(new Rectangle(x, y, l, 32));
                x += l + 10;
            }
            int h = y + 34;
            if (Height != h) Height = h;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            for (int i = 0; i < rects.Count && i < Elements.Length; i++)
            {
                var r = rects[i];
                Theme.RemplirArrondi(g, P.Carte, r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1, 16);
                Theme.Contour(g, P.Bordure, r.X + 0.5f, r.Y + 0.5f, r.Width - 1, r.Height - 1, 16);
                Theme.RemplirDegrade(g, P.Lueur1, P.Lueur2, r.X + 8, r.Y + 7, 18, 18, 9);
                using (var s = new Pen(Theme.TexteSur(Theme.Melanger(P.Lueur1, P.Lueur2, 0.5)), 1.8f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(s, new[] { new PointF(r.X + 12.5f, r.Y + 16.5f), new PointF(r.X + 15.5f, r.Y + 19.5f), new PointF(r.X + 21.5f, r.Y + 12.5f) });
                Theme.Texte(g, Elements[i], Theme.Petit, new Rectangle(r.X + 32, r.Y, r.Width - 36, r.Height), P.Texte, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    // ================================================================ Carte d'outil (icône, titre, description, état, bouton)
    internal class CarteOutilPremium : Carte
    {
        private readonly int glyphe;
        private readonly Etiquette lblTitre, lblTexte, lblEtat;
        private readonly Func<string> etat;
        private readonly string libelle;
        public readonly Bouton Bouton;

        public CarteOutilPremium(int glyphe, string titre, string texte, string bouton, Func<string> etat) : base(300, 214)
        {
            this.glyphe = glyphe; this.etat = etat; libelle = bouton;
            lblTitre = new Etiquette(titre, Theme.CarteTitre, Role.Texte, 72, 18) { AutoSize = false, Size = new Size(210, 44) };
            lblTexte = new Etiquette(texte, Theme.Normal, Role.Discret, 18, 68) { AutoSize = false, Size = new Size(264, 76) };
            lblEtat = new Etiquette("", Theme.PetitGras, Role.Vert, 18, 146) { AutoSize = false, Size = new Size(264, 18) };
            Bouton = new Bouton(bouton, true, 264, 36) { Location = new Point(18, 164) };
            Controls.AddRange(new Control[] { lblTitre, lblTexte, lblEtat, Bouton });
            Resize += (s, e) => Disposer();
        }

        private int hauteurImposee;
        /// <summary>Hauteur naturelle (texte compris), sans tenir compte de la hauteur imposée par la rangée.</summary>
        public int HauteurNaturelle { get; private set; }

        /// <summary>Toutes les cartes d'une rangée prennent la même hauteur : le bouton reste en bas.</summary>
        public void Imposer(int h) { hauteurImposee = h; Disposer(); }

        private void Disposer()
        {
            int l = Math.Max(120, Width - 36);
            lblTitre.Width = l - 54 - (Licence.Premium ? 0 : 52);
            lblTexte.Width = l; lblEtat.Width = l; Bouton.Width = l;
            int ht = TextRenderer.MeasureText(lblTexte.Text, lblTexte.Font, new Size(l, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix).Height;
            lblTexte.Height = Math.Max(40, ht + 2);
            HauteurNaturelle = lblTexte.Bottom + 2 + 18 + 4 + Bouton.Height + 18;
            int h = Math.Max(HauteurNaturelle, hauteurImposee);
            Bouton.Top = h - 18 - Bouton.Height;
            lblEtat.Top = Bouton.Top - 4 - lblEtat.Height;
            if (Height != h) Height = h;
        }

        public void Actualiser()
        {
            Bouton.Text = Licence.Premium ? libelle : "🔒  " + Langue.T(libelle);
            Bouton.Principal = Licence.Premium;
            string e = Licence.Premium && etat != null ? etat() : "";
            lblEtat.Text = e;
            Disposer();
            Bouton.Invalidate(); Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics; var P = Theme.P;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.RemplirDegrade(g, P.Lueur1, P.Lueur2, 18, 18, 42, 42, 12);
            if (Theme.IconesGrand != null) Theme.Icone(g, glyphe, Theme.IconesGrand, new Rectangle(18, 18, 42, 42), Theme.TexteSur(Theme.Melanger(P.Lueur1, P.Lueur2, 0.5)));
            if (!Licence.Premium)
            {
                var r = new Rectangle(Width - 64, 16, 48, 20);
                Theme.RemplirArrondi(g, Theme.Melanger(P.Carte, P.Lueur2, 0.18), r.X, r.Y, r.Width, r.Height, 10);
                Theme.Texte(g, "PRO", Theme.PetitGras, r, P.AccentVif, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }
        }
    }

    // ================================================================ Comparatif gratuit / Premium
    internal class TableauComparatif : Control, IThemable
    {
        private static readonly string[][] Lignes =
        {
            new[] { "Nettoyage, optimisation des disques et de la mémoire", "1", "1" }, new[] { "Sécurité, mises à jour, pilotes", "1", "1" },
            new[] { "Sauvegarde manuelle, jeux, réseau, aide à distance", "1", "1" }, new[] { "Gardien en temps réel et nettoyage automatique", "0", "1" },
            new[] { "Bouclier anti-arnaque, plein potentiel du matériel", "1", "1" },
            new[] { "Enquêteur « Pourquoi mon PC ralentit ? », santé prédictive", "0", "1" }, new[] { "Assistant local (IA sur ton PC), preuve en FPS, coût en électricité", "0", "1" },
            new[] { "Sauvegarde automatique sur clé USB", "0", "1" }, new[] { "Analyse des plantages et du démarrage", "0", "1" },
            new[] { "Mesure des performances, kit de réinstallation", "0", "1" }, new[] { "Santé de la batterie, thèmes de couleur", "0", "1" },
            new[] { "Nouveaux outils Premium des versions 4", "0", "1" }
        };
        private const int HLigne = 34, HEntete = 44, LCol = 110;

        public TableauComparatif()
        {
            Height = HEntete + Lignes.Length * HLigne + 8;
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        }
        public void AppliquerTheme() { Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Width, h = Height;
            Theme.RemplirArrondi(g, P.Carte, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 12);
            Theme.Contour(g, P.Bordure, 0.5f, 0.5f, w - 1.5f, h - 1.5f, 12);
            int xPrem = w - LCol - 12, xGrat = xPrem - LCol;
            // colonne Premium mise en valeur
            Theme.RemplirArrondi(g, Theme.Melanger(P.Carte, P.Lueur2, Theme.EstSombre ? 0.16 : 0.08), xPrem, 6, LCol, h - 12, 10);
            Theme.Texte(g, "Gratuit", Theme.PetitGras, new Rectangle(xGrat, 0, LCol, HEntete), P.Discret, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            var rp = new Rectangle(xPrem + 14, 12, LCol - 28, 22);
            Theme.RemplirDegrade(g, P.Lueur1, P.Lueur2, rp.X, rp.Y, rp.Width, rp.Height, 11);
            Theme.Texte(g, "Premium", Theme.PetitGras, rp, Theme.TexteSur(Theme.Melanger(P.Lueur1, P.Lueur2, 0.5)), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            for (int i = 0; i < Lignes.Length; i++)
            {
                int y = HEntete + i * HLigne;
                using (var p = new Pen(P.Bordure)) g.DrawLine(p, 16, y, w - 16, y);
                Theme.Texte(g, Lignes[i][0], Theme.Normal, new Rectangle(18, y, xGrat - 24, HLigne), P.Texte, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine);
                Coche(g, Lignes[i][1] == "1", new Rectangle(xGrat, y, LCol, HLigne), false);
                Coche(g, Lignes[i][2] == "1", new Rectangle(xPrem, y, LCol, HLigne), true);
            }
        }

        private static void Coche(Graphics g, bool oui, Rectangle r, bool premium)
        {
            var P = Theme.P;
            float cx = r.X + r.Width / 2f, cy = r.Y + r.Height / 2f;
            if (!oui) { using (var p = new Pen(P.Discret, 1.6f)) g.DrawLine(p, cx - 5, cy, cx + 5, cy); return; }
            using (var p = new Pen(premium ? P.AccentVif : P.Vert, 2.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(p, new[] { new PointF(cx - 6, cy), new PointF(cx - 2, cy + 4), new PointF(cx + 6, cy - 5) });
        }
    }

    // ================================================================ Licence (gestion, retrait)
    internal static class PageLicence
    {
        public static void Gerer(FenetrePrincipale f)
        {
            if (Licence.EnEssai) { FenetreAchat.Ouvrir(f); return; }
            string info = Langue.T("Licence Premium active sur ce PC.") + "\n\n" + Langue.T(Licence.Type == "offerte" ? "Type : clé offerte" : "Type : achat sur la boutique") +
                (Licence.Titulaire != "" ? "\n" + Langue.T("Titulaire") + " : " + Licence.Titulaire : "") + (Licence.Courriel != "" ? "\nE-mail : " + Licence.Courriel : "") + "\n" + Langue.T("Clé") + " : " + Licence.CleMasquee +
                "\n\n" + Langue.T("Retirer la licence de ce PC ? (par exemple avant de le donner ou de le vendre : la place est libérée pour un autre PC)");
            if (BoiteMessage.Afficher(info, "Nettoyage PC Premium", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes) return;
            f.Cursor = Cursors.WaitCursor;
            string r = Licence.Retirer();
            f.Cursor = Cursors.Default;
            if (r == "") { Gardien.Desactiver(); BoiteMessage.Info("Licence retirée de ce PC. Tu peux l'activer sur un autre PC avec la même clé."); }
            else BoiteMessage.Attention(Langue.T("La licence n'a pas pu être retirée") + " : " + Langue.T(r));
        }
    }

    // ================================================================ Achat, essai gratuit et activation
    internal class FenetreAchat : FenetreTheme
    {
        private readonly ZoneTexte zone = new ZoneTexte(560) { Multiline = true, Height = 54, ScrollBars = ScrollBars.Vertical, WordWrap = true };
        private readonly Etiquette resultat;
        private readonly Bouton bActiver, bAcheter;
        private readonly CaseACocher chkConditions;

        public static void Ouvrir(FenetrePrincipale f, bool cle = false)
        {
            using (var w = new FenetreAchat()) { if (cle) w.Shown += (s, e) => w.zone.Focus(); w.ShowDialog(f); }
        }

        /// <summary>Lance l'essai gratuit de 7 jours (avec confirmation).</summary>
        public static void LancerEssai(Form proprietaire)
        {
            if (!BoiteMessage.Question("Essayer Premium gratuitement pendant 7 jours ?\n\nTous les outils Premium sont débloqués tout de suite. Aucun paiement, aucune adresse e-mail : à la fin de l'essai, ils se verrouillent simplement, sans rien facturer.\n\nL'essai ne peut être lancé qu'une fois sur ce PC.")) return;
            string r = Licence.DemarrerEssai();
            if (r == "") BoiteMessage.Info("Essai gratuit activé ✔\n\nTu as 7 jours pour tout essayer. Commence par le gardien en temps réel ou l'analyse des plantages.", "Nettoyage PC Premium");
            else BoiteMessage.Attention(Langue.T("L'essai gratuit n'a pas pu démarrer") + " : " + Langue.T(r) + ".");
        }

        internal FenetreAchat() : base("Nettoyage PC Premium", 680, 640)
        {
            Controls.Add(new Etiquette("Nettoyage PC Premium", Theme.Grand, Role.Texte, 24, 18));
            Controls.Add(new Etiquette(Langue.F("{0} TTC une seule fois · sans abonnement · toutes les versions 4 · {1} PC", Boutique.Prix, Boutique.NombrePc), Theme.CarteTitre, Role.AccentVif, 26, 58) { MaximumSize = new Size(620, 0) });
            Controls.Add(new Etiquette(
                "✦  Gardien en temps réel, avec nettoyage léger automatique quand le disque se remplit\n" +
                "✦  « Pourquoi mon PC ralentit ? » : l'enquêteur qui trouve le coupable\n" +
                "✦  Santé prédictive : les signes d'une panne, repérés avant qu'elle arrive\n" +
                "✦  Assistant local (IA sur ton PC), preuve en FPS, coût en électricité\n" +
                "✦  Sauvegarde automatique, plantages, démarrage, performances, kit de réinstallation, batterie, thèmes", Theme.Normal, Role.Texte, 26, 90) { MaximumSize = new Size(620, 0) });
            int y = Math.Max(206, Controls[Controls.Count - 1].Bottom + 16);
            if (!Licence.Premium && Licence.EssaiDisponible)
            {
                var bEssai = new Bouton("Essayer gratuitement 7 jours", false, 260, 40) { Location = new Point(26, y), Font = Theme.CarteTitre };
                bEssai.Click += (s, e) => { LancerEssai(this); if (Licence.Premium) DialogResult = DialogResult.OK; };
                Controls.Add(bEssai);
                Controls.Add(new Etiquette("Aucun paiement ni adresse e-mail. Une fois par PC.", Theme.Petit, Role.Discret, 300, y + 12) { MaximumSize = new Size(340, 0) });
                y += 58;
            }
            chkConditions = new CaseACocher("J'ai lu et j'accepte les conditions de vente et le contrat de licence", 26, y);
            Controls.Add(chkConditions);
            var lCgv = new Lien("Lire les conditions de vente", 48, y + 26);
            lCgv.LinkClicked += (s, e) => FenetreLegal.Ouvrir(this, "CONDITIONS-DE-VENTE");
            var lLic = new Lien("Lire le contrat de licence", 260, y + 26);
            lLic.LinkClicked += (s, e) => FenetreLegal.Ouvrir(this, "LICENCE");
            Controls.Add(lCgv); Controls.Add(lLic);
            y += 58;
            bAcheter = new Bouton(Langue.F("Acheter ({0} TTC)", Boutique.Prix), true, 260, 42) { Location = new Point(26, y), Font = Theme.CarteTitre, Enabled = false };
            chkConditions.CheckedChanged += (s, e) => bAcheter.Enabled = chkConditions.Checked;
            bAcheter.Click += (s, e) =>
            {
                if (!chkConditions.Checked) return;
                if (Boutique.UrlAchat == "") { BoiteMessage.Info("La boutique ouvre bientôt. En attendant, tu peux essayer Premium gratuitement, ou demander une clé à l'éditeur."); return; }
                Programmes.Ouvrir(Boutique.UrlAchat);
                resultat.Text = "La page de paiement s'ouvre dans ton navigateur. Après le paiement, tu reçois ta clé par e-mail : colle-la ci-dessous.";
                resultat.RoleTexte = Role.AccentVif; resultat.AppliquerTheme();
                zone.Focus();
            };
            Controls.Add(bAcheter);
            Controls.Add(new Etiquette("Paiement sécurisé par Lemon Squeezy, revendeur officiel (carte bancaire, PayPal...). La clé arrive par e-mail juste après. Satisfait ou remboursé 14 jours.", Theme.Petit, Role.Discret, 300, y + 2) { MaximumSize = new Size(350, 0) });
            y += 66;
            Controls.Add(new Etiquette("J'ai une clé :", Theme.CarteTitre, Role.Texte, 24, y));
            zone.Location = new Point(26, y + 28); zone.Width = 610;
            Controls.Add(zone);
            bActiver = new Bouton("Activer", true, 140, 36) { Location = new Point(26, y + 92) };
            bActiver.Click += (s, e) => Activer();
            Controls.Add(bActiver);
            resultat = new Etiquette("", Theme.Normal, Role.Discret, 180, y + 96) { MaximumSize = new Size(460, 0) };
            Controls.Add(resultat);
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(536, y + 148), DialogResult = DialogResult.Cancel };
            Controls.Add(bFermer); CancelButton = bFermer;
            ClientSize = new Size(ClientSize.Width, y + 198);
            Theme.Appliquer(this);
        }

        private void Activer()
        {
            string cle = zone.Text;
            bActiver.Enabled = false; Cursor = Cursors.WaitCursor;
            resultat.Text = "Vérification de la clé..."; resultat.RoleTexte = Role.AccentVif; resultat.AppliquerTheme();
            System.Threading.Tasks.Task.Run(() =>
            {
                string r; try { r = Licence.Activer(cle); } catch (Exception ex) { r = ex.Message; }
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        bActiver.Enabled = true; Cursor = Cursors.Default;
                        if (r == "")
                        {
                            BoiteMessage.Info("Premium activé ✔\n\nMerci ! Tous les outils Premium sont débloqués sur ce PC.", "Nettoyage PC Premium");
                            DialogResult = DialogResult.OK;
                        }
                        else { resultat.Text = Langue.T("Clé non activée") + " : " + Langue.T(r); resultat.RoleTexte = Role.Rouge; resultat.AppliquerTheme(); }
                    }));
                }
                catch { }
            });
        }
    }

    // ================================================================ Analyse du démarrage
    internal class GraphiqueDemarrages : Control, IThemable
    {
        public List<Tuple<string, long, long>> Valeurs = new List<Tuple<string, long, long>>();   // date, avant session, après session (ms)
        public GraphiqueDemarrages() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true); }
        public void AppliquerTheme() { Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics; var P = Theme.P;
            g.Clear(Theme.FondDerriere(this)); g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.RemplirArrondi(g, P.Carte, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 9);
            Theme.Contour(g, P.Bordure, 0.5f, 0.5f, Width - 1.5f, Height - 1.5f, 9);
            if (Valeurs.Count == 0) { Theme.Texte(g, "Aucun démarrage chronométré.", Theme.Normal, new Rectangle(16, 16, Width - 32, 30), P.Discret, TextFormatFlags.Left); return; }
            int haut = 20, bas = Height - 28;
            double max = Math.Max(1, Valeurs.Max(v => v.Item2 + v.Item3));
            float l = (Width - 40f) / Valeurs.Count;
            for (int i = 0; i < Valeurs.Count; i++)
            {
                var v = Valeurs[i];
                float x = 20 + i * l, lb = Math.Max(3, l * 0.65f);
                float h1 = (float)((bas - haut) * v.Item2 / max), h2 = (float)((bas - haut) * v.Item3 / max);
                Theme.RemplirDegrade(g, P.Lueur1, P.Accent, x, bas - h1, lb, Math.Max(1, h1), 2, true);
                if (h2 > 1) Theme.RemplirArrondi(g, Color.FromArgb(150, P.Lueur2), x, bas - h1 - h2, lb, h2, 2);
                if (Valeurs.Count <= 12 || i % Math.Max(1, Valeurs.Count / 8) == 0)
                    Theme.Texte(g, v.Item1.Length >= 10 ? v.Item1.Substring(8, 2) + "/" + v.Item1.Substring(5, 2) : v.Item1, Theme.Petit, new Rectangle((int)(x + lb / 2 - 24), bas + 4, 48, 16), P.Discret, TextFormatFlags.HorizontalCenter);
            }
            Theme.Texte(g, "■ avant l'ouverture de session   ■ après (programmes au démarrage)", Theme.Petit, new Rectangle(16, 2, Width - 32, 16), P.Discret, TextFormatFlags.Right);
        }
    }

    internal class FenetreAnalyseDemarrage : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly Etiquette lblResume, lblConseil;
        private readonly GraphiqueDemarrages graph = new GraphiqueDemarrages { Location = new Point(20, 112), Size = new Size(904, 170), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        private readonly ListeSombre lv = new ListeSombre { MultiSelect = false, ShowItemToolTips = true, Location = new Point(20, 296), Size = new Size(904, 230), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Action<string, object> recevoir;

        public FenetreAnalyseDemarrage(FenetrePrincipale f) : base("Analyse du démarrage", 960, 640, true)
        {
            F = f; MinimizeBox = true; ShowInTaskbar = true;
            Controls.Add(new Etiquette("Analyse du démarrage", Theme.Grand, Role.Texte, 20, 14));
            lblResume = new Etiquette("Windows te demande l'autorisation pour lire le chronométrage des démarrages...", Theme.CarteTitre, Role.AccentVif, 22, 54) { MaximumSize = new Size(900, 0) };
            lblConseil = new Etiquette("", Theme.Normal, Role.Discret, 22, 80) { MaximumSize = new Size(900, 0) };
            Controls.Add(lblResume); Controls.Add(lblConseil); Controls.Add(graph);
            lv.Colonne("Ce qui ralentit le démarrage", 380); lv.Colonne("Type", 120); lv.Colonne("Retard moyen", 110, HorizontalAlignment.Right); lv.Colonne("Fois", 60, HorizontalAlignment.Right); lv.Colonne("Dernière fois", 110);
            lv.ColonneElastique = 0; lv.ActiverTri();
            Controls.Add(lv);
            var bDemarrage = new Bouton("Gérer les programmes au démarrage", false, 280, 34) { Location = new Point(20, 540), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            bDemarrage.Click += (s, e) => { F.AfficherPage("Demarrage"); F.Activate(); };
            var bRelancer = new Bouton("Actualiser", false, 120, 34) { Location = new Point(310, 540), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            bRelancer.Click += (s, e) => Lancer();
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(824, 540), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            bFermer.Click += (s, e) => Close();
            Controls.AddRange(new Control[] { bDemarrage, bRelancer, bFermer });
            Theme.Appliquer(this);
            recevoir = (cle, v) => { if (cle == "demarrage" && !IsDisposed) Afficher(v as Dictionary<string, object>); };
            F.DonneesRecues += recevoir;
            FormClosed += (s, e) => F.DonneesRecues -= recevoir;
            Shown += (s, e) => { if (Programme.DossierAutotest == null) Lancer(); };
        }

        private void Lancer()
        {
            if (!F.LancerAction("demarrage")) { lblResume.Text = "Analyse non lancée (autorisation refusée ou action déjà en cours)."; lblResume.RoleTexte = Role.Orange; lblResume.AppliquerTheme(); }
        }

        private static string Sec(double ms) { return (ms / 1000.0).ToString(ms < 10000 ? "0.0" : "0", Appli.Fr) + " s"; }

        private void Afficher(Dictionary<string, object> d)
        {
            if (d == null) return;
            if (!Json.Bool(d, "Admin")) { lblResume.Text = "Sans l'autorisation de Windows, le chronométrage des démarrages ne peut pas être lu."; lblResume.RoleTexte = Role.Orange; lblResume.AppliquerTheme(); return; }
            var dem = Json.Objets(Json.Valeur(d, "Demarrages"));
            graph.Valeurs = dem.AsEnumerable().Reverse().Select(x => Tuple.Create(Json.Texte(x, "Date"), Json.Long(x, "Principal"), Json.Long(x, "Apres"))).ToList();
            graph.Invalidate();
            if (dem.Count == 0) { lblResume.Text = "Windows n'a encore chronométré aucun démarrage complet (ou le journal a été vidé)."; lblResume.RoleTexte = Role.Discret; }
            else
            {
                var dernier = dem[0];
                double moy = dem.Take(10).Average(x => Json.Long(x, "Total"));
                lblResume.Text = "Dernier démarrage : " + Sec(Json.Long(dernier, "Total")) + "  (" + Sec(Json.Long(dernier, "Principal")) + " jusqu'au bureau + " + Sec(Json.Long(dernier, "Apres")) + " de programmes au démarrage)   ·   moyenne des 10 derniers : " + Sec(moy);
                lblResume.RoleTexte = moy > 90000 ? Role.Orange : Role.Vert;
            }
            lblResume.AppliquerTheme();
            var ral = Json.Objets(Json.Valeur(d, "Ralentissements"));
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var r in ral)
            {
                long m = Json.Long(r, "Moyenne");
                var it = lv.Ajouter(r, Json.Texte(r, "Nom"), Json.Texte(r, "Type"), Sec(m), Json.Long(r, "Fois").ToString(), Json.Texte(r, "Dernier"));
                it.SubItems[2].Tag = m; it.SubItems[3].Tag = Json.Long(r, "Fois");
                if (m >= 5000) it.ForeColor = Theme.P.Orange;
            }
            lv.EndUpdate();
            var conseils = new List<string>();
            var pires = ral.Where(r => Json.Long(r, "Moyenne") >= 3000).Take(3).Select(r => Json.Texte(r, "Nom") + " (" + Sec(Json.Long(r, "Moyenne")) + ")").ToList();
            if (pires.Count > 0) conseils.Add("Les plus gros freins : " + string.Join(", ", pires) + ". Pour une application, décoche-la dans Accélérer > Démarrage ; pour un pilote, mets-le à jour.");
            if (!Json.Bool(d, "DemarrageRapide")) conseils.Add("Le démarrage rapide de Windows est désactivé : l'activer (Panneau de configuration > Options d'alimentation) peut faire gagner plusieurs secondes.");
            if (conseils.Count == 0 && dem.Count > 0) conseils.Add("✔ Aucun ralentissement important repéré.");
            lblConseil.Text = string.Join("\n", conseils);
            graph.Top = lblConseil.Bottom + 12; lv.Top = graph.Bottom + 12; lv.Height = Math.Max(120, ClientSize.Height - lv.Top - 60);
        }
    }

    // ================================================================ Mesure des performances
    internal class FenetreBenchmark : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly ListeDeroulante cbDisque = new ListeDeroulante(260) { Location = new Point(110, 70) };
        private readonly ZoneTexte note = new ZoneTexte(190) { Location = new Point(470, 70) };
        private readonly Bouton bLancer;
        private readonly BarreProgression barre = new BarreProgression { Location = new Point(24, 112), Size = new Size(860, 22), Visible = false };
        private readonly Etiquette lblEtape;
        private readonly ListeSombre lv = new ListeSombre { MultiSelect = false, Location = new Point(20, 150), Size = new Size(864, 300), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Etiquette lblComparaison;
        private readonly List<DriveInfo> disques;
        private bool enCours;

        public FenetreBenchmark(FenetrePrincipale f) : base("Mesure des performances", 920, 600, true)
        {
            F = f;
            Controls.Add(new Etiquette("Mesure des performances", Theme.Grand, Role.Texte, 20, 14));
            Controls.Add(new Etiquette("Disque :", Theme.Normal, Role.Texte, 22, 74));
            disques = DriveInfo.GetDrives().Where(d => { try { return d.IsReady && d.DriveType == DriveType.Fixed; } catch { return false; } }).ToList();
            foreach (var d in disques) cbDisque.Items.Add(d.Name.TrimEnd('\\') + "  " + d.VolumeLabel + " (" + Langue.F("{0} libres", Format.Taille(d.AvailableFreeSpace)) + ")");
            if (cbDisque.Items.Count > 0) cbDisque.SelectedIndex = 0;
            Controls.Add(cbDisque);
            Controls.Add(new Etiquette("Note :", Theme.Normal, Role.Texte, 410, 74));
            note.Text = "";
            Infobulle.Definir(note, "Facultatif : par exemple « avant EXPO » ou « après mise à jour du pilote », pour t'y retrouver.");
            Controls.Add(note);
            bLancer = new Bouton("Lancer la mesure (1 min)", true, 200, 34) { Location = new Point(684, 66), Anchor = AnchorStyles.Top | AnchorStyles.Right };
            bLancer.Click += (s, e) => Lancer();
            Controls.Add(bLancer);
            lblEtape = new Etiquette("Ferme les jeux et les gros programmes pendant la mesure, pour un résultat juste.", Theme.Petit, Role.Discret, 22, 116);
            Controls.Add(lblEtape); Controls.Add(barre);
            lv.Colonne("Date", 120); lv.Colonne("Note", 150); lv.Colonne("Processeur 1 cœur", 120, HorizontalAlignment.Right); lv.Colonne("Tous les cœurs", 110, HorizontalAlignment.Right);
            lv.Colonne("Mémoire", 90, HorizontalAlignment.Right); lv.Colonne("Disque lecture", 100, HorizontalAlignment.Right); lv.Colonne("Disque écriture", 100, HorizontalAlignment.Right); lv.Colonne("Accès 4 Ko", 80, HorizontalAlignment.Right);
            lv.ColonneElastique = 1;
            Controls.Add(lv);
            lblComparaison = new Etiquette("", Theme.Normal, Role.Discret, 22, 462) { MaximumSize = new Size(860, 0), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            Controls.Add(lblComparaison);
            var bFermer = new Bouton("Fermer", false, 100, 34) { Location = new Point(784, 516), Anchor = AnchorStyles.Bottom | AnchorStyles.Right };
            bFermer.Click += (s, e) => Close();
            Controls.Add(bFermer); CancelButton = bFermer;
            FormClosing += (s, e) => { if (enCours) { Benchmark.Arret = true; } };
            Theme.Appliquer(this);
            Afficher();
        }

        private static string Pts(double mo) { return Format.Nombre(mo * 10) + " pts"; }

        private void Afficher()
        {
            var h = Benchmark.Historique();
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var r in h.AsEnumerable().Reverse())
                lv.Ajouter(r, r.Date, r.Note + (r.RamMts > 0 ? (r.Note != "" ? " · " : "") + "RAM " + r.RamMts : ""), Pts(r.Cpu1), Pts(r.CpuN), r.Memoire.ToString("0.0", Appli.Fr) + " Go/s",
                    Format.Nombre(r.DisqueLecture) + " Mo/s", Format.Nombre(r.DisqueEcriture) + " Mo/s", r.DisqueAleatoire > 0 ? Format.Nombre(r.DisqueAleatoire) + " /s" : "—");
            lv.EndUpdate();
            if (h.Count == 0) { lblComparaison.Text = "Aucune mesure pour l'instant. Astuce : fais une mesure AVANT un réglage (par exemple l'EXPO), puis une APRÈS : la différence s'affiche ici."; return; }
            var der = h[h.Count - 1];
            if (h.Count == 1) { lblComparaison.Text = "Première mesure enregistrée : refais-en une après un réglage pour voir la différence."; return; }
            var ref0 = h[h.Count - 2];
            Func<double, double, string> pct = (a, b) => b <= 0 ? "—" : ((a - b) / b * 100).ToString("+0;-0;0", Appli.Fr) + " %";
            lblComparaison.Text = "Par rapport à la mesure précédente (" + ref0.Date + (ref0.Note != "" ? ", " + ref0.Note : "") + ") :   processeur " + pct(der.CpuN, ref0.CpuN) +
                "   ·   mémoire " + pct(der.Memoire, ref0.Memoire) + "   ·   disque " + pct(der.DisqueLecture, ref0.DisqueLecture) +
                (Math.Abs(der.Memoire - ref0.Memoire) / Math.Max(0.1, ref0.Memoire) > 0.1 && der.RamMts != ref0.RamMts && der.RamMts > 0 ? "\nLa mémoire est passée de " + ref0.RamMts + " à " + der.RamMts + " MT/s." : "") +
                "\n(Écarts de moins de 3 % : variation normale d'une mesure à l'autre.)";
            lblComparaison.RoleTexte = Role.Texte; lblComparaison.AppliquerTheme();
        }

        private void Lancer()
        {
            if (enCours) { Benchmark.Arret = true; return; }
            if (F.ActionEnCours) { BoiteMessage.Info("Une action est en cours : attends qu'elle se termine, la mesure serait faussée."); return; }
            int i = cbDisque.SelectedIndex; if (i < 0 || i >= disques.Count) return;
            string dossier = disques[i].Name == Path.GetPathRoot(Path.GetTempPath()) ? Path.GetTempPath() : Path.Combine(disques[i].Name, "NettoyagePC-test");
            try { Directory.CreateDirectory(dossier); } catch { dossier = Path.GetTempPath(); }
            enCours = true; bLancer.Text = "Arrêter"; barre.Demarrer(); barre.Visible = true;
            string n = note.Text.Trim();
            System.Threading.Tasks.Task.Run(() =>
            {
                ResultatBenchmark r = null;
                try { r = Benchmark.Lancer(dossier, (t, p) => { try { BeginInvoke(new Action(() => { lblEtape.Text = t; barre.Etape(p, Math.Min(100, p + 15)); })); } catch { } }); }
                catch (Exception ex) { JournalDemarrage.Erreur("mesure des performances", ex); }
                try { if (dossier.EndsWith("NettoyagePC-test")) Directory.Delete(dossier, false); } catch { }
                try
                {
                    BeginInvoke(new Action(() =>
                    {
                        enCours = false; bLancer.Text = "Lancer la mesure (1 min)"; barre.Arreter();
                        if (r == null) { lblEtape.Text = "Mesure arrêtée."; return; }
                        r.Note = n; Benchmark.Enregistrer(r);
                        lblEtape.Text = "Mesure terminée et enregistrée.";
                        Afficher();
                    }));
                }
                catch { }
            });
        }
    }

    // ================================================================ Kit de réinstallation
    internal class FenetreKit : FenetreTheme
    {
        private readonly ListeDeroulante cb = new ListeDeroulante(480) { Location = new Point(24, 92) };
        private readonly CaseACocher chkWifi;
        private readonly List<string> chemins = new List<string>();
        public string Destination; public bool Wifi;

        public static void Ouvrir(FenetrePrincipale f)
        {
            string dest = null; bool wifi = false;
            using (var w = new FenetreKit()) { if (w.ShowDialog(f) == DialogResult.OK) { dest = w.Destination; wifi = w.Wifi; } }
            if (dest == null) return;
            var args = new List<string> { "--destination", dest };
            if (wifi) args.Add("--wifi");
            Action<string, object> recevoir = null;
            recevoir = (cle, v) =>
            {
                if (cle != "kit") return;
                f.DonneesRecues -= recevoir;
                string dossier = Json.Texte(v as Dictionary<string, object>, "Dossier");
                if (Directory.Exists(dossier) && BoiteMessage.Question("Le kit de réinstallation est prêt :\n" + dossier + "\n\nGarde-le sur une clé USB ou un disque externe. Ouvrir le dossier ?")) Programmes.Ouvrir(dossier);
            };
            f.DonneesRecues += recevoir;
            if (!f.LancerAction("kit", args, "Préparer le kit de réinstallation dans « " + dest + " » ?\n\nLa liste de tes applications, tes pilotes" + (wifi ? ", tes réseaux Wi-Fi (avec leurs mots de passe)" : "") + " et un script de remise en place sont enregistrés. Rien n'est modifié sur ton PC. Cela peut prendre quelques minutes."))
                f.DonneesRecues -= recevoir;
        }

        internal FenetreKit() : base("Kit de réinstallation", 600, 380)
        {
            Controls.Add(new Etiquette("Kit de réinstallation", Theme.Grand, Role.Texte, 20, 14));
            Controls.Add(new Etiquette("Où enregistrer le kit ? (de préférence une clé USB ou un disque externe)", Theme.Normal, Role.Discret, 22, 62));
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady || !(d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed)) continue;
                    cb.Items.Add(d.Name.TrimEnd('\\') + "  " + (d.DriveType == DriveType.Removable ? Langue.T("Clé USB / carte") : Langue.T("Disque")) + (d.VolumeLabel != "" ? " « " + d.VolumeLabel + " »" : "") + " — " + Langue.F("{0} libres", Format.Taille(d.AvailableFreeSpace)));
                    chemins.Add(d.RootDirectory.FullName);
                }
                catch { }
            }
            cb.Items.Add("Choisir un dossier..."); chemins.Add("");
            int amovible = chemins.FindIndex(c => { try { return c != "" && new DriveInfo(c).DriveType == DriveType.Removable; } catch { return false; } });
            cb.SelectedIndex = amovible >= 0 ? amovible : 0;
            cb.SelectionChangeCommitted += (s, e) =>
            {
                if (cb.SelectedIndex != chemins.Count - 1) return;
                using (var dlg = new FolderBrowserDialog())
                    if (dlg.ShowDialog(this) == DialogResult.OK) { chemins[chemins.Count - 1] = dlg.SelectedPath; cb.Items[cb.Items.Count - 1] = "Dossier : " + dlg.SelectedPath; }
                    else cb.SelectedIndex = 0;
            };
            Controls.Add(cb);
            chkWifi = new CaseACocher("Inclure les mots de passe des réseaux Wi-Fi", 24, 140) { Checked = true };
            Infobulle.Definir(chkWifi, "Pratique pour se reconnecter tout de suite après la réinstallation. Le kit contiendra alors ces mots de passe : garde-le en lieu sûr.");
            Controls.Add(chkWifi);
            Controls.Add(new Etiquette("Le kit contient : la liste de tes applications (réinstallables d'un coup), tes pilotes, tes réseaux Wi-Fi et un script « Reinstaller.cmd ». Les pilotes peuvent occuper plusieurs Go. Tes documents n'y sont pas : utilise aussi Protéger > Sauvegarde.", Theme.Petit, Role.Discret, 22, 176) { MaximumSize = new Size(540, 0) });
            var ok = new Bouton("Préparer le kit", true, 180, 36) { Location = new Point(24, 276) };
            ok.Click += (s, e) =>
            {
                string c = chemins[cb.SelectedIndex];
                if (c == "" || !Directory.Exists(c)) { BoiteMessage.Info("Choisis où enregistrer le kit."); return; }
                try { if (new DriveInfo(Path.GetPathRoot(c)).AvailableFreeSpace < 6L * 1073741824 && !BoiteMessage.Question("Il reste moins de 6 Go sur cette destination : les pilotes risquent de ne pas tenir. Continuer quand même ?")) return; } catch { }
                if (string.Equals(Path.GetPathRoot(c), Path.GetPathRoot(Chemins.Windows), StringComparison.OrdinalIgnoreCase) && !BoiteMessage.Question("Ce dossier est sur le disque de Windows : en cas de réinstallation complète, le kit serait effacé avec lui. Préfère une clé USB.\n\nContinuer quand même ?")) return;
                Destination = c; Wifi = chkWifi.Checked; DialogResult = DialogResult.OK;
            };
            var an = new Bouton("Annuler", false, 100, 36) { Location = new Point(470, 276), DialogResult = DialogResult.Cancel };
            Controls.Add(ok); Controls.Add(an); CancelButton = an;
            Theme.Appliquer(this);
        }
    }
}
