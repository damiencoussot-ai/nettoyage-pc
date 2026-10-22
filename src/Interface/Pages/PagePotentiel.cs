// Nettoyage PC - page « Plein potentiel » (Outils) : les réglages qui brident le matériel, avec la correction de chacun
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class PagePotentiel : Page
    {
        private readonly Carte entete = new Carte(600, 104, true) { Location = new Point(28, 4) };
        private readonly Etiquette lblTitre, lblDetail;
        private readonly Bouton bActualiser;
        private readonly Panel liste = new Panel { Location = new Point(28, 124), BackColor = Color.Transparent };
        private List<PointPotentiel> points = new List<PointPotentiel>();
        private bool enCours;

        public PagePotentiel(FenetrePrincipale f) : base(f, "Potentiel", "Plein potentiel", "Les réglages qui brident ton matériel sans que tu le saches, et comment les corriger.", 0xEC4A, "Plein potentiel")
        {
            lblTitre = new Etiquette("Analyse du matériel...", Theme.Grand, Role.Texte, 20, 16);
            lblDetail = new Etiquette("Écrans, carte graphique et mémoire vive : Nettoyage PC vérifie qu'ils donnent tout ce qu'ils peuvent. Rien n'est modifié.", Theme.Normal, Role.Discret, 22, 58) { MaximumSize = new Size(560, 0) };
            bActualiser = new Bouton("Revérifier", false, 140, 34);
            bActualiser.Click += (s, e) => Charger();
            entete.Controls.AddRange(new Control[] { lblTitre, lblDetail, bActualiser });
            Controls.Add(entete);
            Controls.Add(liste);
            Resize += (s, e) => Disposer();
        }

        public override void PremiereFois() { Charger(); }

        private void Charger()
        {
            if (enCours) return;
            enCours = true; bActualiser.Enabled = false;
            lblTitre.Text = "Analyse du matériel...";
            EnFond(() => Potentiel.Analyser(), l =>
            {
                enCours = false; bActualiser.Enabled = true;
                points = l ?? new List<PointPotentiel>();
                int n = points.Count(p => !p.Ok);
                lblTitre.Text = points.Count == 0 ? "Matériel non analysable sur ce PC" : n == 0 ? "✔  Ton matériel tourne à plein régime" : Langue.F(n == 1 ? "{0} réglage bride ton matériel" : "{0} réglages brident ton matériel", n);
                lblTitre.RoleTexte = points.Count == 0 ? Role.Discret : n == 0 ? Role.Vert : Role.Orange; lblTitre.AppliquerTheme();
                lblDetail.Text = n == 0 ? "Écrans, carte graphique et mémoire vive donnent tout ce qu'ils peuvent. Revérifie après avoir changé de matériel ou de câble."
                    : "Chaque point indique ce que tu gagnes et comment corriger. Ce sont des réglages prévus par les fabricants, sans risque pour le matériel.";
                Afficher();
            });
        }

        private void Afficher()
        {
            liste.SuspendLayout();
            foreach (Control c in liste.Controls.Cast<Control>().ToList()) c.Dispose();
            liste.Controls.Clear();
            int w = Math.Max(500, ClientSize.Width - 56), y = 0;
            foreach (var p in points.OrderBy(x => x.Ok))
            {
                var ligne = new Carte(w, p.Ok ? 46 : 120) { Location = new Point(0, y) };
                ligne.Controls.Add(new Etiquette(p.Ok ? "✔" : "!", Theme.CarteTitre, p.Ok ? Role.Vert : Role.Orange, 14, p.Ok ? 11 : 13));
                var titre = new Etiquette(p.Titre, p.Ok ? Theme.Normal : Theme.CarteTitre, Role.Texte, 44, p.Ok ? 13 : 12) { AutoSize = false, Size = new Size(w - 250, 24), AutoEllipsis = true, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                ligne.Controls.Add(titre);
                if (!p.Ok)
                {
                    if (p.Gain != "") ligne.Controls.Add(new Etiquette("→ " + p.Gain, Theme.PetitGras, Role.AccentVif, w - 196, 15) { Anchor = AnchorStyles.Top | AnchorStyles.Right });
                    var d = new Etiquette(p.Detail, Theme.Normal, Role.Discret, 44, 40) { AutoSize = false, Size = new Size(w - 230, 44), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                    ligne.Controls.Add(d);
                    var corr = new Etiquette(p.Correction, Theme.Petit, Role.Texte, 44, 86) { AutoSize = false, Size = new Size(w - 230, 30), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                    ligne.Controls.Add(corr);
                    if (p.Reglage != "")
                    {
                        string uri = p.Reglage;
                        var b = new Bouton("Ouvrir le réglage", true, 170, 32) { Location = new Point(w - 182, 76), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                        b.Click += (s, e) => Programmes.Ouvrir(uri);
                        ligne.Controls.Add(b);
                    }
                    else if (p.Cle == "rebar" || p.Cle == "expo")
                    {
                        var b = new Bouton("Onglet Matériel", false, 170, 32) { Location = new Point(w - 182, 76), Anchor = AnchorStyles.Top | AnchorStyles.Right };
                        b.Click += (s, e) => F.AfficherPage("Materiel");
                        ligne.Controls.Add(b);
                    }
                }
                liste.Controls.Add(ligne);
                y += ligne.Height + 8;
            }
            liste.Size = new Size(w, Math.Max(10, y));
            liste.ResumeLayout();
            Theme.Appliquer(liste);
            Disposer();
        }

        private void Disposer()
        {
            int w = Math.Max(500, ClientSize.Width - 56);
            entete.Width = w;
            bActualiser.Location = new Point(w - 160, 20);
            lblDetail.MaximumSize = new Size(Math.Max(300, w - 200), 0);
            entete.Height = Math.Max(104, lblDetail.Bottom + 18);
            liste.Location = new Point(28, entete.Bottom + 14);
            if (liste.Width != w)
            {
                liste.Width = w;
                foreach (Control c in liste.Controls) c.Width = w;
            }
        }
    }
}
