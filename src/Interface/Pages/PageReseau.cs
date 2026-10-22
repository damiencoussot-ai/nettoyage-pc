// Nettoyage PC - page Réseau : test de la connexion (ping, débit) et serveurs DNS (comparaison, changement réversible)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace NettoyagePC
{
    /// <summary>Petite tuile de résultat : libellé, grande valeur, commentaire coloré.</summary>
    internal class TuileMesure : Carte
    {
        private readonly Etiquette valeur, commentaire;
        public TuileMesure(string titre) : base(170, 104)
        {
            Controls.Add(new Etiquette(titre, Theme.PetitGras, Role.Discret, 16, 14));
            valeur = new Etiquette("—", Theme.Grand, Role.Texte, 14, 34);
            commentaire = new Etiquette("", Theme.Petit, Role.Discret, 16, 76);
            Controls.Add(valeur); Controls.Add(commentaire);
        }
        public void Definir(string v, string c = "", Role r = Role.Discret)
        {
            valeur.Text = v; commentaire.Text = c; commentaire.RoleTexte = r; commentaire.AppliquerTheme();
        }
    }

    internal class PageReseau : Page
    {
        private readonly Carte carte = new Carte(600, 120, true) { Location = new Point(28, 4) };
        private readonly Etiquette lblConnexion, lblDetail, lblConseil, lblDns;
        private readonly Bouton bTester, bComparer, bUtiliser, bAuto;
        private readonly TuileMesure tBox = new TuileMesure("Ping vers la box"), tPing = new TuileMesure("Ping Internet"), tGigue = new TuileMesure("Stabilité (gigue)"),
            tDesc = new TuileMesure("Téléchargement"), tMont = new TuileMesure("Envoi");
        private readonly Grille tuiles;
        private readonly ListeSombre lv = new ListeSombre { MultiSelect = false, Height = 150 };
        private readonly Etiquette titreDns;
        private List<CarteReseau> cartes = new List<CarteReseau>();
        private bool test, comparaison;

        public PageReseau(FenetrePrincipale f) : base(f, "Reseau", "Réseau", "Teste ta connexion (ping, débit) et choisis des serveurs DNS plus rapides.", 0xE701)
        {
            carte.Controls.Add(new Etiquette("Ta connexion", Theme.CarteTitre, Role.Texte, 20, 18));
            lblConnexion = new Etiquette("Recherche de la connexion...", Theme.Grand, Role.Texte, 20, 42);
            lblDetail = new Etiquette("", Theme.Petit, Role.Discret, 22, 82) { MaximumSize = new Size(560, 0) };
            bTester = new Bouton("Tester ma connexion", true, 230, 38) { Font = Theme.CarteTitre };
            bTester.Click += (s, e) => Tester();
            Infobulle.Definir(bTester, "Mesure le ping vers ta box et vers Internet, puis le débit (environ 10 secondes ; télécharge jusqu'à 200 Mo depuis Cloudflare : évite avec une connexion limitée).");
            carte.Controls.AddRange(new Control[] { lblConnexion, lblDetail, bTester });
            Controls.Add(carte);
            tuiles = new Grille(this, 140, false);
            foreach (var t in new[] { tBox, tPing, tGigue, tDesc, tMont }) tuiles.Controls.Add(t);
            lblConseil = new Etiquette("Clique sur « Tester ma connexion » pour mesurer ta connexion.", Theme.Normal, Role.Discret) { MaximumSize = new Size(900, 0) };
            Controls.Add(lblConseil);

            titreDns = Titre("Serveurs DNS (l'« annuaire » d'Internet)", 28, 0);
            lv.Colonne("Serveur", 210); lv.Colonne("Adresses", 190); lv.Colonne("Temps de réponse", 140, HorizontalAlignment.Right); lv.Colonne("Particularité", 330);
            Controls.Add(lv);
            bComparer = NouveauBouton("Comparer les serveurs", false, 190, 32, 28, 0, Comparer);
            bUtiliser = NouveauBouton("Utiliser le serveur choisi", true, 210, 32, 228, 0, Utiliser);
            bAuto = NouveauBouton("Revenir au DNS automatique", false, 220, 32, 448, 0, RevenirAuto);
            Infobulle.Definir(bUtiliser, "Windows te demandera l'autorisation (bouclier) : clique sur « Oui ». Réversible depuis l'Historique ou avec « Revenir au DNS automatique ».");
            lblDns = new Etiquette("Un serveur DNS plus rapide rend l'ouverture des sites un peu plus vive (il ne change pas le débit). Clique sur « Comparer les serveurs » : ton PC mesure le temps de réponse de chacun.", Theme.Petit, Role.Discret) { MaximumSize = new Size(900, 0) };
            Controls.Add(lblDns);
            Resize += (s, e) => Disposer();
            lv.SelectedIndexChanged += (s, e) => bUtiliser.Enabled = lv.SelectedItems.Count > 0 && lv.SelectedItems[0].Tag is ServeurDns && ((ServeurDns)lv.SelectedItems[0].Tag).Ip1 != "";
        }

        private void Disposer()
        {
            int w = Math.Max(500, ClientSize.Width - 56);
            carte.Width = w; bTester.Location = new Point(w - 250, 22);
            lblDetail.MaximumSize = new Size(Math.Max(300, w - 290), 0);
            carte.Height = Math.Max(120, lblDetail.Bottom + 18);
            tuiles.Location = new Point(28, carte.Bottom + 14); tuiles.Disponible = w; tuiles.Agencer();
            lblConseil.Location = new Point(28, tuiles.Bottom + 4); lblConseil.MaximumSize = new Size(w, 0);
            titreDns.Location = new Point(28, lblConseil.Bottom + 22);
            lv.Location = new Point(28, titreDns.Bottom + 10); lv.Width = w;
            int yb = lv.Bottom + 10;
            bComparer.Location = new Point(28, yb); bUtiliser.Location = new Point(228, yb); bAuto.Location = new Point(448, yb);
            lblDns.Location = new Point(28, yb + 42); lblDns.MaximumSize = new Size(w, 0);
        }

        public override void PremiereFois() { ChargerConnexion(); }

        private void ChargerConnexion()
        {
            EnFond(Reseau.CartesActives, l =>
            {
                cartes = l ?? new List<CarteReseau>();
                var c = cartes.FirstOrDefault();
                if (c == null) { lblConnexion.Text = "Aucune connexion Internet"; lblDetail.Text = "Branche le câble réseau ou connecte-toi au Wi-Fi, puis reviens sur cette page."; bTester.Enabled = false; }
                else
                {
                    bTester.Enabled = true;
                    lblConnexion.Text = (c.Wifi ? "Wi-Fi" : "Câble (Ethernet)") + " · " + c.Nom;
                    lblDetail.Text = c.Description + "\nBox (passerelle) : " + c.Passerelle + "   ·   DNS : " + (c.Dns.Count > 0 ? string.Join(", ", c.Dns) : "aucun") + (c.DnsAutomatique ? " (automatique)" : " (choisi manuellement)") +
                        (cartes.Count > 1 ? "\nAutres connexions actives : " + string.Join(", ", cartes.Skip(1).Select(x => x.Nom)) : "");
                }
                AfficherServeurs(null);
                Disposer();
            });
        }

        // ------------------------------------------------------------ Test de la connexion
        private void Tester()
        {
            if (test) return;
            var c = cartes.FirstOrDefault(); if (c == null) return;
            test = true; bTester.Enabled = false;
            foreach (var t in new[] { tBox, tPing, tGigue, tDesc, tMont }) t.Definir("...", "");
            Conseil("Mesure du ping...", Role.AccentVif);
            ResultatPing box = null, net = null;
            double desc = -1, mont = -1;
            string erreur = null;
            EnFond(() =>
            {
                box = Reseau.Ping(c.Passerelle, "box");
                net = Reseau.Ping("1.1.1.1", "Internet");
                if (net.Moyenne < 0) net = Reseau.Ping("8.8.8.8", "Internet");
                Invoquer(() => { AfficherPing(box, net, c); Conseil("Mesure du débit de téléchargement...", Role.AccentVif); });
                try { desc = Reseau.DebitDescendant(v => Invoquer(() => tDesc.Definir(v.ToString("0") + " Mbit/s", "mesure..."))); } catch (Exception ex) { erreur = ex.Message; }
                Invoquer(() => { AfficherDebit(tDesc, desc); Conseil("Mesure du débit d'envoi...", Role.AccentVif); });
                try { mont = Reseau.DebitMontant(); } catch (Exception ex) { if (erreur == null) erreur = ex.Message; }
                return true;
            }, ok =>
            {
                test = false; bTester.Enabled = true;
                AfficherDebit(tDesc, desc); AfficherDebit(tMont, mont);
                Bilan(box, net, desc, mont, c, erreur);
            });
        }

        private void Invoquer(Action a) { try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); } catch { } }
        private void Conseil(string t, Role r) { lblConseil.Text = t; lblConseil.RoleTexte = r; lblConseil.AppliquerTheme(); Disposer(); }

        private void AfficherPing(ResultatPing box, ResultatPing net, CarteReseau c)
        {
            if (box.Moyenne >= 0) tBox.Definir(box.Moyenne.ToString("0") + " ms", box.Perte > 0 ? box.Perte + " % de pertes" : box.Moyenne <= (c.Wifi ? 8 : 3) ? "Parfait" : "Un peu lent", box.Perte > 0 || box.Moyenne > (c.Wifi ? 8 : 3) ? Role.Orange : Role.Vert);
            else tBox.Definir("—", "La box ne répond pas au ping", Role.Discret);
            if (net.Moyenne >= 0)
            {
                string q = net.Moyenne < 25 ? "Excellent pour jouer" : net.Moyenne < 50 ? "Bon" : net.Moyenne < 100 ? "Moyen" : "Élevé";
                tPing.Definir(net.Moyenne.ToString("0") + " ms", q + (net.Perte > 0 ? " · " + net.Perte + " % perdus" : ""), net.Moyenne < 50 && net.Perte == 0 ? Role.Vert : Role.Orange);
                tGigue.Definir(net.Gigue.ToString("0") + " ms", net.Gigue < 5 ? "Très stable" : net.Gigue < 15 ? "Stable" : "Instable", net.Gigue < 15 ? Role.Vert : Role.Orange);
            }
            else { tPing.Definir("—", "Internet ne répond pas", Role.Rouge); tGigue.Definir("—", ""); }
        }

        private static void AfficherDebit(TuileMesure t, double v)
        {
            if (v < 0) { t.Definir("—", "mesure impossible", Role.Discret); return; }
            t.Definir(v >= 100 ? v.ToString("0") + " Mbit/s" : v.ToString("0.0") + " Mbit/s", v >= 100 ? "Très rapide" : v >= 30 ? "Rapide" : v >= 8 ? "Correct" : "Lent", v >= 8 ? Role.Vert : Role.Orange);
        }

        private void Bilan(ResultatPing box, ResultatPing net, double desc, double mont, CarteReseau c, string erreur)
        {
            var l = new List<string>();
            bool pb = false;
            if (net.Moyenne < 0) { l.Add("Internet ne répond pas : vérifie la box (voyants), puis redémarre-la si besoin."); pb = true; }
            if (c.Wifi && (box.Perte > 0 || box.Moyenne > 8 || box.Gigue > 10)) { l.Add("Ton Wi-Fi est un peu faible ou instable : rapproche le PC de la box, ou utilise un câble réseau (idéal pour jouer)."); pb = true; }
            else if (!c.Wifi && box.Moyenne > 3) { l.Add("La box répond lentement : redémarrer la box peut aider."); pb = true; }
            if (net.Moyenne >= 0 && net.Gigue >= 15) { l.Add("Connexion instable (gigue élevée) : un téléchargement en cours ou un autre appareil sature peut-être la ligne."); pb = true; }
            if (desc >= 0 && desc < 8) { l.Add("Le débit est faible : ferme les téléchargements en cours, ou compare avec le débit prévu par ton abonnement."); pb = true; }
            if (erreur != null && (desc < 0 || mont < 0)) l.Add("(Une partie du test de débit n'a pas pu se faire : " + erreur + ")");
            if (!pb) l.Insert(0, "✔ Ta connexion est en bonne forme.");
            Conseil(string.Join("\n", l), pb ? Role.Orange : Role.Vert);
        }

        // ------------------------------------------------------------ DNS
        private void AfficherServeurs(List<ServeurDns> mesures)
        {
            var c = cartes.FirstOrDefault();
            var liste = mesures ?? Reseau.ServeursConnus();
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var s in liste.OrderBy(x => x.Temps < 0 ? double.MaxValue : x.Temps))
            {
                bool actuel = c != null && c.Dns.Contains(s.Ip1);
                string nom = s.Ip1 == "" ? s.Nom : s.Nom + (actuel ? "  (actuel)" : "");
                var it = lv.Ajouter(s, nom, s.Ip1 == "" ? string.Join(", ", c == null ? new List<string>() : c.Dns) : s.Ip1 + ", " + s.Ip2, s.Temps >= 0 ? s.Temps.ToString("0") + " ms" : mesures == null ? "" : "ne répond pas", s.Description);
                it.SubItems[2].Tag = s.Temps < 0 ? double.MaxValue : s.Temps;
                if (actuel || s.Ip1 == "") it.ForeColor = Theme.P.AccentVif;
            }
            lv.EndUpdate();
            bUtiliser.Enabled = false;
            bAuto.Enabled = c != null && !c.DnsAutomatique;
        }

        private void Comparer()
        {
            if (comparaison) return;
            var c = cartes.FirstOrDefault(); if (c == null) return;
            comparaison = true; bComparer.Enabled = false;
            lblDns.Text = "Mesure du temps de réponse de chaque serveur..."; lblDns.RoleTexte = Role.AccentVif; lblDns.AppliquerTheme();
            EnFond(() =>
            {
                var l = Reseau.ServeursConnus();
                bool connu = c.Dns.Any(d => l.Any(s => s.Ip1 == d || s.Ip2 == d));
                if (!connu && c.Dns.Count > 0) l.Insert(0, new ServeurDns { Nom = c.DnsAutomatique ? "Ton fournisseur (actuel)" : "Ton réglage actuel", Ip1 = "", Description = "Serveur DNS utilisé aujourd'hui", Temps = -1 });
                // deux passes : la première « réveille » les serveurs, la seconde est mesurée
                foreach (var s in l) { string ip = s.Ip1 == "" ? c.Dns[0] : s.Ip1; Reseau.TempsDns(ip); s.Temps = Reseau.TempsDns(ip); }
                return l;
            }, l =>
            {
                comparaison = false; bComparer.Enabled = true;
                if (l == null) return;
                AfficherServeurs(l);
                var meilleur = l.Where(s => s.Temps >= 0).OrderBy(s => s.Temps).FirstOrDefault();
                var actuel = l.FirstOrDefault(s => s.Ip1 == "" || c.Dns.Contains(s.Ip1));
                string t;
                if (meilleur == null) t = "Aucun serveur n'a répondu : la connexion bloque peut-être les serveurs DNS publics.";
                else if (actuel != null && actuel.Temps >= 0 && (meilleur == actuel || actuel.Temps - meilleur.Temps < 5)) t = "✔ Ton serveur DNS actuel est déjà parmi les plus rapides : inutile de changer.";
                else t = meilleur.Nom + " est le plus rapide (" + meilleur.Temps.ToString("0") + " ms" + (actuel != null && actuel.Temps >= 0 ? " contre " + actuel.Temps.ToString("0") + " ms aujourd'hui" : "") + "). Sélectionne-le puis « Utiliser le serveur choisi ».";
                lblDns.Text = t; lblDns.RoleTexte = t.StartsWith("✔") ? Role.Vert : Role.Discret; lblDns.AppliquerTheme();
                foreach (ListViewItem it in lv.Items) if (meilleur != null && it.Tag == meilleur) it.Selected = true;
                Disposer();
            });
        }

        private void Utiliser()
        {
            if (lv.SelectedItems.Count == 0) return;
            var s = lv.SelectedItems[0].Tag as ServeurDns;
            if (s == null || s.Ip1 == "") return;
            string msg = "Utiliser les serveurs DNS de " + s.Nom + " (" + s.Ip1 + ", " + s.Ip2 + ") ?\n\n" + s.Description + ".\nRéversible à tout moment (Historique, ou « Revenir au DNS automatique »).";
            if (F.ChkSimu.Checked) { BoiteMessage.Info("Mode simulation : rien n'est modifié.\n\n" + msg); return; }
            if (!BoiteMessage.Question(msg)) return;
            string r = Reseau.Appliquer(s);
            if (r == "") { F.Statut("Serveurs DNS de " + s.Nom + " activés.", false, Role.Vert); }
            else BoiteMessage.Attention("Changement des serveurs DNS : " + r);
            ChargerConnexion();
        }

        private void RevenirAuto()
        {
            var aChanger = cartes.Where(c => !c.DnsAutomatique && c.Index > 0).ToList();
            if (aChanger.Count == 0) return;
            if (!BoiteMessage.Question("Revenir aux serveurs DNS automatiques (ceux de ta box) ?")) return;
            var erreurs = new List<string>();
            foreach (var c in aChanger)
            {
                if (!Elevation.Commande(60, "dns", c.Index.ToString(), "auto")) { erreurs.Add(c.Nom); continue; }
                var id = c.Id;
                Historique.MarquerAnnulees(m => m.Type == "dns" && m.D("Carte") == id);
            }
            if (erreurs.Count > 0) BoiteMessage.Attention("Non modifié pour : " + string.Join(", ", erreurs) + " (autorisation refusée ?)");
            else F.Statut("Serveurs DNS automatiques rétablis.", false, Role.Vert);
            ChargerConnexion();
        }
    }
}
