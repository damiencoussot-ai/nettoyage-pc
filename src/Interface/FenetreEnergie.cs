// Nettoyage PC - fenêtre « Coût en électricité » (Premium) : estimation en kWh et en euros, par jour et par application
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreEnergie : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly TuileChiffre tJour = new TuileChiffre(), tSemaine = new TuileChiffre(), tMois = new TuileChiffre(), tAn = new TuileChiffre();
        private readonly CourbeTemps courbe = new CourbeTemps { Titre = "Consommation par jour (kWh), 30 derniers jours", Barres = true };
        private readonly BarresClassement apps = new BarresClassement { Titre = "Ce qui consomme le plus (30 derniers jours)" };
        private readonly Etiquette lblEtat, lblConseils, lblModele;
        private readonly ZoneTexte txtPrix = new ZoneTexte(80);
        private readonly Bouton bGardien;
        private List<JourEnergie> jours = new List<JourEnergie>();

        public static void Ouvrir(FenetrePrincipale f) { using (var w = new FenetreEnergie(f)) w.ShowDialog(f); }

        internal FenetreEnergie(FenetrePrincipale f) : base("Coût en électricité", 980, 760, true)
        {
            F = f; MinimumSize = new Size(860, 640);
            Controls.Add(new Etiquette("Coût en électricité", Theme.Grand, Role.Texte, 20, 14));
            Controls.Add(new Etiquette("Combien ton PC te coûte en électricité, et quelles applications consomment le plus. C'est une estimation (activité du processeur et de la carte graphique, sans les écrans) : seule une prise wattmètre donne la valeur exacte.",
                Theme.Normal, Role.Discret, 22, 52) { MaximumSize = new Size(920, 0) });
            Controls.Add(new Etiquette("Prix du kWh (voir ta facture) :", Theme.Normal, Role.Texte, 22, 104));
            txtPrix.Location = new Point(236, 101); txtPrix.Text = Energie.Prix.ToString("0.####", Appli.Fr);
            txtPrix.Leave += (s, e) => EnregistrerPrix();
            txtPrix.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; EnregistrerPrix(); } };
            Controls.Add(txtPrix);
            Controls.Add(new Etiquette("€", Theme.Normal, Role.Texte, 320, 104));
            lblEtat = new Etiquette("", Theme.PetitGras, Role.Discret, 352, 105) { MaximumSize = new Size(600, 0) };
            Controls.Add(lblEtat);
            int x = 20;
            foreach (var t in new[] { tJour, tSemaine, tMois, tAn }) { t.Location = new Point(x, 140); t.Size = new Size(226, 78); Controls.Add(t); x += 236; }
            courbe.Location = new Point(20, 232); courbe.Size = new Size(460, 220); courbe.Format = v => v.ToString("0.#", Appli.Fr);
            apps.Location = new Point(492, 232); apps.Size = new Size(472, 220); apps.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(courbe); Controls.Add(apps);
            Controls.Add(new Etiquette("Pour payer moins", Theme.CarteTitre, Role.Texte, 20, 466));
            lblConseils = new Etiquette("", Theme.Normal, Role.Texte, 22, 494) { MaximumSize = new Size(920, 0) };
            lblModele = new Etiquette("", Theme.Petit, Role.Discret, 22, 640) { MaximumSize = new Size(920, 0), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            Controls.Add(lblConseils); Controls.Add(lblModele);
            bGardien = new Bouton("Régler le gardien", false, 170, 36) { Location = new Point(20, 676), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            bGardien.Click += (s, e) => { FenetreGardien.Ouvrir(F); Charger(); };
            var bVeille = new Bouton("Réglages de mise en veille", false, 220, 36) { Location = new Point(200, 676), Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            bVeille.Click += (s, e) => Programmes.Ouvrir("ms-settings:powersleep");
            var bFermer = new Bouton("Fermer", false, 110, 36) { Location = new Point(854, 676), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            Controls.AddRange(new Control[] { bGardien, bVeille, bFermer }); CancelButton = bFermer;
            ClientSize = new Size(984, 722);
            Theme.Appliquer(this);
            Resize += (s, e) => Disposer();
            Shown += (s, e) => Charger();
        }

        private void EnregistrerPrix()
        {
            double p;
            if (double.TryParse(txtPrix.Text.Trim().Replace(',', '.').Replace("€", ""), NumberStyles.Float, CultureInfo.InvariantCulture, out p) && p > 0 && p < 5) { Energie.Prix = p; Afficher(); }
            else txtPrix.Text = Energie.Prix.ToString("0.####", Appli.Fr);
        }

        private void Charger()
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                List<JourEnergie> l = null; ModeleEnergie m = null;
                try { l = Energie.Lire(); m = Energie.Modele(); } catch { }
                try { BeginInvoke(new Action(() => { jours = l ?? new List<JourEnergie>(); Afficher(m); })); } catch { }
            });
        }

        private static string Kwh(double wh) { return (wh / 1000).ToString(wh < 10000 ? "0.00" : "0.0", Appli.Fr) + " kWh"; }
        private static string Euros(double wh) { double e = wh / 1000 * Energie.Prix; return e.ToString(e < 10 ? "0.00" : "0", Appli.Fr) + " €"; }

        private ModeleEnergie modele;
        private void Afficher(ModeleEnergie m = null)
        {
            if (m != null) modele = m;
            var reg = ReglagesGardien.Lire();
            bool actif = Gardien.EnCours && reg.Energie;
            lblEtat.Text = actif ? "● Mesure en cours (le gardien relève l'activité chaque minute)" : !reg.Actif || !Gardien.EnCours ? "Le gardien est arrêté : active-le pour mesurer la consommation." : "La mesure de consommation est désactivée dans les réglages du gardien.";
            lblEtat.RoleTexte = actif ? Role.Vert : Role.Orange; lblEtat.AppliquerTheme();
            bGardien.Principal = !actif; bGardien.Invalidate();
            var auj = jours.FirstOrDefault(j => j.Jour == DateTime.Today);
            var sem = jours.Where(j => j.Jour > DateTime.Today.AddDays(-7)).ToList();
            var mois = jours.Where(j => j.Jour > DateTime.Today.AddDays(-30)).ToList();
            double whMois = mois.Sum(j => j.Wh);
            int nbJours = mois.Count(j => j.Minutes >= 30);
            double parJour = nbJours > 0 ? mois.Where(j => j.Minutes >= 30).Average(j => j.Wh) : 0;
            Tuile(tJour, auj == null ? -1 : auj.Wh, "Aujourd'hui", auj != null ? " · " + Langue.F("{0} h allumé", (auj.Minutes / 60.0).ToString("0.#", Appli.Fr)) : "");
            Tuile(tSemaine, sem.Count == 0 ? -1 : sem.Sum(j => j.Wh), "7 derniers jours");
            Tuile(tMois, mois.Count == 0 ? -1 : whMois, "30 derniers jours");
            Tuile(tAn, nbJours >= 3 ? parJour * 365 : -1, "Sur un an, à ce rythme");
            courbe.Debut = DateTime.Today.AddDays(-29);
            courbe.Points = mois.Select(j => Tuple.Create(j.Jour, j.Wh / 1000)).ToList();
            courbe.VideTexte = "Pas encore de mesure : le gardien relève l'activité du PC chaque minute.";
            courbe.Invalidate();
            var parApp = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            foreach (var j in mois) foreach (var a in j.Apps) { double v; parApp.TryGetValue(a.Key, out v); parApp[a.Key] = v + a.Value; }
            apps.Lignes = parApp.OrderByDescending(a => a.Value).Take(8).Select(a => Tuple.Create(a.Key == Energie.NomSysteme ? Langue.T("Windows et PC allumé (au repos)") : a.Key, a.Value, Euros(a.Value))).ToList();
            apps.Invalidate();

            var conseils = new List<string>();
            int minInactif = mois.Sum(j => j.MinutesInactif); double whInactif = mois.Sum(j => j.WhInactif);
            if (nbJours >= 3 && minInactif / (double)Math.Max(1, nbJours) >= 60)
                conseils.Add(Langue.F("•  Ton PC reste allumé {0} h par jour en moyenne sans que personne ne s'en serve. Une mise en veille automatique après 15 à 30 minutes économiserait environ {1} par an.",
                    (minInactif / 60.0 / nbJours).ToString("0.#", Appli.Fr), Euros(whInactif / nbJours * 365 * 0.9)));
            var jeu = parApp.Where(a => a.Key != Energie.NomSysteme).OrderByDescending(a => a.Value).FirstOrDefault();
            if (jeu.Key != null && whMois > 0 && jeu.Value / whMois >= 0.25)
                conseils.Add(Langue.F("•  « {0} » représente {1} % de la consommation. Si c'est un jeu : limiter les images par seconde à la fréquence de ton écran (et activer FreeSync / G-Sync) réduit nettement la consommation de la carte graphique, sans différence visible.",
                    jeu.Key, (jeu.Value / whMois * 100).ToString("0")));
            conseils.Add("•  Éteins (ou mets en veille) le PC la nuit : un PC de bureau allumé pour rien consomme autant qu'une ampoule halogène.");
            conseils.Add("•  Le mode d'alimentation « Équilibré » consomme moins que « Performances élevées » sans ralentir l'usage courant (Accélérer > Optimisation).");
            lblConseils.Text = string.Join("\n", conseils);
            if (modele != null)
                lblModele.Text = Langue.F("Base de calcul : processeur {0} ({1} W), carte graphique {2}, reste du PC {3} W, pertes de l'alimentation comprises. Écrans non comptés.",
                    modele.Processeur == "" ? "?" : modele.Processeur, modele.Tdp, modele.Tbp > 0 ? modele.Carte + " (" + modele.Tbp + " W)" : Langue.T("intégrée"), modele.Base);
            Disposer();
        }

        private static void Tuile(TuileChiffre t, double wh, string libelle, string suite = "")
        {
            t.Valeur = wh < 0 ? "—" : Euros(wh);
            t.Libelle = Langue.T(libelle) + suite + (wh < 0 ? "" : " · " + Kwh(wh));
            t.Couleur = Role.Texte;
            t.Invalidate();
        }

        private void Disposer()
        {
            int w = ClientSize.Width - 40;
            int lt = (w - 3 * 10) / 4, x = 20;
            foreach (var t in new[] { tJour, tSemaine, tMois, tAn }) { t.Location = new Point(x, 140); t.Width = lt; x += lt + 10; }
            courbe.Width = (w - 12) / 2; apps.Left = courbe.Right + 12; apps.Width = w - courbe.Width - 12;
            lblConseils.MaximumSize = new Size(w, 0); lblModele.MaximumSize = new Size(w, 0);
            lblModele.Top = bGardien.Top - lblModele.Height - 10;
        }
    }
}
