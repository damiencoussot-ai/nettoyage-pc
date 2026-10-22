// Nettoyage PC - base des pages
using System;
using System.Drawing;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal abstract class Page : Surface
    {
        public string Cle, Nom, Sous, NomOnglet;
        public int Icone;
        public bool Chargee;
        protected readonly FenetrePrincipale F;
        private readonly DefilementDoux defilement;

        protected Page(FenetrePrincipale f, string cle, string nom, string sous, int icone, string onglet = null) : base(Role.Fond)
        {
            F = f; Cle = cle; Nom = Langue.T(nom); Sous = Langue.T(sous); Icone = icone; NomOnglet = Langue.T(onglet ?? nom);
            AutoScroll = true;
            defilement = new DefilementDoux(this);
        }

        /// <summary>Molette : défilement doux (sauf si les animations de Windows sont désactivées).</summary>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (defilement.Molette(e.Delta)) { var h = e as HandledMouseEventArgs; if (h != null) h.Handled = true; return; }
            base.OnMouseWheel(e);
        }

        protected override void OnVisibleChanged(EventArgs e) { defilement.Arreter(); base.OnVisibleChanged(e); }

        /// <summary>Carte rangée derrière « Plus d'options » de la grille.</summary>
        protected void EnPlus(Grille g, Control c) { g.AjouterEnPlus(c); }

        /// <summary>Premier affichage (chargement des listes, analyses).</summary>
        public virtual void PremiereFois() { }
        /// <summary>À chaque affichage.</summary>
        public virtual void Montrer() { }
        public virtual void ThemeChange() { }
        public virtual void Fermeture() { }

        protected Grille NouvelleGrille(int y = 8, bool adaptable = true) { return new Grille(this, y, adaptable); }

        /// <summary>Icône d'une carte, choisie d'après son titre (glyphes Segoe Fluent Icons / MDL2 Assets).</summary>
        private static readonly string[][] IconesCartes =
        {
            new[] { "propos", "E946" }, new[] { "légales", "E8A5" }, new[] { "confidentialit", "E72E" }, new[] { "aide|premiers pas|lisezmoi", "E897" }, new[] { "aider quelqu", "E8D4" },
            new[] { "journa", "E8A5" }, new[] { "raccourci", "E71B" }, new[] { "langue", "E774" }, new[] { "apparence|couleur", "E790" }, new[] { "mode simple", "E80F" },
            new[] { "entretien|planif", "E787" }, new[] { "compatib|résumé de ce pc|rapport", "E7F4" }, new[] { "restaur", "E81C" }, new[] { "réparer", "E90F" },
            new[] { "antivirus|sécurité", "EA18" }, new[] { "extension", "EA86" }, new[] { "mettre à jour|mise à jour|mises à jour", "E895" },
            new[] { "pilote", "E950" }, new[] { @"barrette|\bram\b|mémoire", "E9D9" }, new[] { "disque|espace", "EDA2" }, new[] { "jeu", "E7FC" },
            new[] { "performances|équilibré|optimis", "EC4A" }, new[] { "téléchargement", "E896" }, new[] { "nettoy|voir ce qui serait", "E74D" }, new[] { "sauvegard", "E8F1" }
        };

        public static int IconePour(string titre)
        {
            string t = (titre ?? "").ToLowerInvariant();
            foreach (var ic in IconesCartes)
                if (System.Text.RegularExpressions.Regex.IsMatch(t, ic[0])) return Convert.ToInt32(ic[1], 16);
            return 0;
        }

        /// <summary>Carte : titre, description, bouton.</summary>
        protected Carte NouvelleCarte(string titre, string texte, string texteBouton, Cible cible, bool principal = false)
        {
            var c = new Carte(300, 176, principal) { Icone = IconePour(titre) };
            c.Controls.Add(new Etiquette(titre, Theme.CarteTitre, Role.Texte, 18, 18));
            c.Controls.Add(new Etiquette(texte, Theme.Normal, Role.Discret, 18, 46) { AutoSize = false, Size = new Size(266, 76) });
            var b = new Bouton(texteBouton, principal, 264, 36) { Location = new Point(18, 124), Cible = cible };
            b.Click += (s, e) => F.Executer(((Bouton)s).Cible as Cible);
            c.Controls.Add(b);
            F.EnregistrerBoutonAction(b);
            return c;
        }

        protected Etiquette Titre(string texte, int x, int y) { var l = new Etiquette(texte, Theme.CarteTitre, Role.Texte, x, y); Controls.Add(l); return l; }
        protected Etiquette Texte(string texte, int x, int y, Role role = Role.Discret, Font police = null)
        {
            var l = new Etiquette(texte, police ?? Theme.Normal, role, x, y); Controls.Add(l); return l;
        }

        protected Bouton NouveauBouton(string texte, bool principal, int largeur, int hauteur, int x, int y, Action clic)
        {
            var b = new Bouton(texte, principal, largeur, hauteur) { Location = new Point(x, y) };
            if (clic != null) b.Click += (s, e) => clic();
            Controls.Add(b);
            return b;
        }

        protected Bouton BoutonAction(string texte, bool principal, int largeur, int hauteur, int x, int y, string action)
        {
            var b = new Bouton(texte, principal, largeur, hauteur) { Location = new Point(x, y), Cible = Cible.A(action) };
            b.Click += (s, e) => F.Executer(((Bouton)s).Cible as Cible);
            Controls.Add(b);
            F.EnregistrerBoutonAction(b);
            return b;
        }

        /// <summary>Exécute un travail en arrière-plan puis revient sur la fenêtre avec le résultat.</summary>
        protected void EnFond<T>(Func<T> travail, Action<T> suite)
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                T r = default(T); Exception err = null;
                try { r = travail(); } catch (Exception ex) { err = ex; JournalDemarrage.Erreur("travail en arrière-plan (" + Cle + ")", ex); }
                // une page jamais affichée n'a pas encore de handle : le résultat passe alors par la fenêtre principale
                Control cible = !IsDisposed && IsHandleCreated ? (Control)this : F;
                try { if (cible != null && !cible.IsDisposed && cible.IsHandleCreated) cible.BeginInvoke(new Action(() => { if (IsDisposed) return; if (err == null) suite(r); else suite(default(T)); })); } catch { }
            });
        }
    }
}
