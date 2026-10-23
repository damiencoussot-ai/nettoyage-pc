// Nettoyage PC - fenêtre « Santé prédictive » (Premium) : signes de faiblesse du matériel, tendances et prévisions
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreSante : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly Etiquette lblResume, lblDetail;
        private readonly Panel liste = new Panel { AutoScroll = true, BackColor = Color.Transparent };
        private List<IndicateurSante> indicateurs = new List<IndicateurSante>();
        private Action<string, object> recevoir;
        private bool enCours;

        public static void Ouvrir(FenetrePrincipale f) { using (var w = new FenetreSante(f)) w.ShowDialog(f); }

        internal FenetreSante(FenetrePrincipale f) : base("Santé prédictive", 980, 740, true)
        {
            F = f; MinimumSize = new Size(820, 560);
            Controls.Add(new Etiquette("Santé prédictive", Theme.Grand, Role.Texte, 20, 14));
            Controls.Add(new Etiquette("Les petits signes qui annoncent une panne, repérés avant qu'elle arrive : erreurs du matériel, du disque, arrêts brutaux, usure des SSD, batterie et chauffe.",
                Theme.Normal, Role.Discret, 22, 52) { MaximumSize = new Size(920, 0) });
            lblResume = new Etiquette("Analyse en cours...", Theme.CarteTitre, Role.AccentVif, 22, 92) { MaximumSize = new Size(920, 0) };
            lblDetail = new Etiquette("Les prévisions sont des tendances (« à ce rythme ») : elles aident à anticiper, sans certitude.", Theme.Petit, Role.Discret, 22, 120) { MaximumSize = new Size(920, 0) };
            Controls.Add(lblResume); Controls.Add(lblDetail);
            liste.Location = new Point(20, 148); liste.Size = new Size(924, 500); liste.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(liste);
            var bActualiser = new Bouton("Actualiser", false, 120, 36) { Location = new Point(20, 656), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            bActualiser.Click += (s, e) => Charger();
            var bFermer = new Bouton("Fermer", false, 110, 36) { Location = new Point(834, 656), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            Controls.Add(bActualiser); Controls.Add(bFermer); CancelButton = bFermer;
            ClientSize = new Size(964, 702);
            Theme.Appliquer(this);
            recevoir = (cle, v) => { if (cle == "disques" && !IsDisposed) BeginInvoke(new Action(Charger)); };
            F.DonneesRecues += recevoir;
            FormClosed += (s, e) => F.DonneesRecues -= recevoir;
            liste.Resize += (s, e) => { foreach (Control c in liste.Controls) c.Width = Largeur(); };
            Shown += (s, e) => Charger();
        }

        private int Largeur() { return Math.Max(600, liste.ClientSize.Width - 4); }

        private void Charger()
        {
            if (enCours) return;
            enCours = true;
            lblResume.Text = "Analyse en cours..."; lblResume.RoleTexte = Role.AccentVif; lblResume.AppliquerTheme();
            System.Threading.Tasks.Task.Run(() =>
            {
                List<IndicateurSante> l = null;
                try { l = Sante.Analyser(); } catch (Exception ex) { JournalDemarrage.Erreur("santé prédictive", ex); }
                try { BeginInvoke(new Action(() => { enCours = false; indicateurs = l ?? new List<IndicateurSante>(); Afficher(); })); } catch { }
            });
        }

        private void Afficher()
        {
            int niveau;
            lblResume.Text = Sante.Resume(indicateurs, out niveau);
            lblResume.RoleTexte = niveau == 2 ? Role.Rouge : niveau == 1 ? Role.Orange : Role.Vert; lblResume.AppliquerTheme();
            liste.SuspendLayout();
            foreach (Control c in liste.Controls.Cast<Control>().ToList()) c.Dispose();
            liste.Controls.Clear();
            int y = 0, w = Largeur();
            foreach (var i in Sante.Ordonner(indicateurs))
            {
                var ind = i;
                bool courbe = i.Points.Count >= (i.Barres ? 1 : 2) && (!i.Barres || i.Points.Any(p => p.Item2 > 0));
                int lt = courbe ? w - 360 : w - 40;
                var carte = new Carte(w, 10) { Location = new Point(0, y), Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
                Role r = i.Etat == "probleme" ? Role.Rouge : i.Etat == "attention" ? Role.Orange : i.Etat == "ok" ? Role.Vert : Role.Discret;
                carte.Controls.Add(new Etiquette(i.Etat == "probleme" ? "✖" : i.Etat == "attention" ? "!" : i.Etat == "ok" ? "✔" : "?", Theme.CarteTitre, r, 14, 12));
                carte.Controls.Add(new Etiquette(i.Nom, Theme.CarteTitre, Role.Texte, 40, 12) { MaximumSize = new Size(lt - 30, 0) });
                int yy = 38;
                var val = new Etiquette(i.Valeur, Theme.PetitGras, r, 42, yy) { MaximumSize = new Size(lt - 30, 0) }; carte.Controls.Add(val); yy = val.Bottom + 6;
                foreach (var t in new[] { Tuple.Create(i.Explication, Role.Discret), Tuple.Create(i.Prevision, Role.AccentVif), Tuple.Create(i.Conseil, Role.Texte) })
                {
                    if (string.IsNullOrEmpty(t.Item1)) continue;
                    var e = new Etiquette(t.Item1, Theme.Normal, t.Item2, 42, yy) { MaximumSize = new Size(lt - 30, 0) };
                    carte.Controls.Add(e); yy = e.Bottom + 4;
                }
                if (i.Bouton != "" && i.Cible != "")
                {
                    var b = new Bouton(i.Bouton, i.Etat == "probleme" || i.Etat == "inconnu", 180, 32) { Location = new Point(42, yy + 6) };
                    b.Click += (s, e) => Agir(ind);
                    carte.Controls.Add(b); yy = b.Bottom;
                }
                int h = Math.Max(courbe ? 150 : 70, yy + 14);
                carte.Height = h;
                if (courbe)
                {
                    var c = new CourbeTemps
                    {
                        Titre = i.Unite == "par semaine" ? "par semaine, sur 3 mois" : i.Unite == "°C" ? "maximum du jour" : "évolution",
                        Points = i.Points, Barres = i.Barres, DepuisZero = i.Unite != "°C", Pente = i.Pente, PrevisionJusqua = i.PrevisionJusqua, Seuil = i.Seuil,
                        Format = v => i.Unite == "%" ? v.ToString("0", Appli.Fr) + " %" : i.Unite == "°C" ? v.ToString("0", Appli.Fr) + " °C" : v.ToString("0", Appli.Fr),
                        Location = new Point(w - 336, 10), Size = new Size(320, h - 20), Anchor = AnchorStyles.Top | AnchorStyles.Right
                    };
                    carte.Controls.Add(c);
                }
                liste.Controls.Add(carte);
                y += h + 10;
            }
            liste.ResumeLayout();
            Theme.Appliquer(liste);
        }

        private void Agir(IndicateurSante i)
        {
            if (i.Cible.StartsWith("action:")) { if (!F.LancerAction(i.Cible.Substring(7))) return; }
            else if (i.Cible.StartsWith("page:")) { Close(); F.AfficherPage(i.Cible.Substring(5)); F.Activate(); }
            else if (i.Cible.StartsWith("outil:")) F.OuvrirOutil(i.Cible.Substring(6));
            else if (i.Cible.StartsWith("uri:")) Programmes.Ouvrir(i.Cible.Substring(4));
        }
    }
}
