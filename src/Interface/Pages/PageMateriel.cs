// Nettoyage PC - page Matériel : mémoire vive bridée (EXPO / XMP) avec le guide du BIOS, santé détaillée des disques
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class PageMateriel : Page
    {
        private readonly Carte carteRam = new Carte(600, 168, true) { Location = new Point(28, 4) };
        private readonly Etiquette lblRam, lblRamDetail, lblCarteMere, lblDisques;
        private readonly Bouton bGuide, bBios, bAnalyse;
        private readonly ListeSombre lvRam = new ListeSombre { MultiSelect = false, Height = 130 };
        private readonly ListeSombre lvDisques = new ListeSombre { MultiSelect = false, Height = 150, ShowItemToolTips = true };
        private readonly Etiquette titreDisques;
        private InfosMateriel infos;

        public PageMateriel(FenetrePrincipale f) : base(f, "Materiel", "Matériel", "Mémoire vive (vitesse réelle) et santé détaillée des disques.", 0xE950)
        {
            carteRam.Controls.Add(new Etiquette("Mémoire vive", Theme.CarteTitre, Role.Texte, 20, 18));
            lblRam = new Etiquette("Analyse des barrettes...", Theme.Grand, Role.Texte, 20, 42);
            lblRamDetail = new Etiquette("", Theme.Normal, Role.Discret, 22, 82) { MaximumSize = new Size(560, 0) };
            lblCarteMere = new Etiquette("", Theme.Petit, Role.Discret, 22, 140);
            bGuide = new Bouton("Comment l'activer (guide)", true, 250, 36) { Visible = false };
            bGuide.Click += (s, e) => Guide();
            bBios = new Bouton("Redémarrer dans le BIOS", false, 250, 32) { Visible = false };
            bBios.Click += (s, e) => RedemarrerBios();
            Infobulle.Definir(bBios, "Windows te demandera l'autorisation (bouclier) : clique sur « Oui ».\nLe PC redémarre 15 secondes après, directement dans les réglages du BIOS.");
            carteRam.Controls.AddRange(new Control[] { lblRam, lblRamDetail, lblCarteMere, bGuide, bBios });
            Controls.Add(carteRam);

            lvRam.Colonne("Emplacement", 150); lvRam.Colonne("Capacité", 90, HorizontalAlignment.Right); lvRam.Colonne("Type", 70);
            lvRam.Colonne("Vitesse actuelle", 120, HorizontalAlignment.Right); lvRam.Colonne("Fabricant", 130); lvRam.Colonne("Référence", 220);
            Controls.Add(lvRam);

            titreDisques = Titre("Santé des disques", 28, 0);
            bAnalyse = BoutonAction("Analyse détaillée", true, 180, 32, 0, 0, "santedisques");
            Infobulle.Definir(bAnalyse, "Lit les compteurs de fiabilité de chaque disque : usure (SSD), température, heures d'utilisation, erreurs. Sans rien modifier.");
            lvDisques.Colonne("Disque", 250); lvDisques.Colonne("Type", 90); lvDisques.Colonne("Taille", 90, HorizontalAlignment.Right); lvDisques.Colonne("État", 110);
            lvDisques.Colonne("Température", 100, HorizontalAlignment.Right); lvDisques.Colonne("Usure", 70, HorizontalAlignment.Right); lvDisques.Colonne("Utilisation", 110, HorizontalAlignment.Right); lvDisques.Colonne("Erreurs", 70, HorizontalAlignment.Right); lvDisques.ColonneElastique = 0;
            Controls.Add(lvDisques);
            lblDisques = new Etiquette("", Theme.Normal, Role.Discret) { MaximumSize = new Size(900, 0) };
            Controls.Add(lblDisques);
            Resize += (s, e) => Disposer();
            f.DonneesRecues += (cle, v) => { if (cle == "disques") DetailsDisques(v as Dictionary<string, object>); };
        }

        private void Disposer()
        {
            int w = Math.Max(500, ClientSize.Width - 56);
            carteRam.Width = w;
            bGuide.Location = new Point(w - 270, 22); bBios.Location = new Point(w - 270, 66);
            lblRamDetail.MaximumSize = new Size(Math.Max(300, w - 320), 0);
            carteRam.Height = Math.Max(168, Math.Max(lblRamDetail.Bottom, 110) + 34);
            lblCarteMere.Location = new Point(22, carteRam.Height - 28);
            lvRam.Location = new Point(28, carteRam.Bottom + 12); lvRam.Width = w;
            titreDisques.Location = new Point(28, lvRam.Bottom + 22);
            bAnalyse.Location = new Point(28 + w - 180, lvRam.Bottom + 16);
            lvDisques.Location = new Point(28, lvRam.Bottom + 56); lvDisques.Width = w;
            lblDisques.Location = new Point(28, lvDisques.Bottom + 10); lblDisques.MaximumSize = new Size(w, 0);
        }

        public override void PremiereFois() { Charger(); }

        private void Charger()
        {
            EnFond(InfosMateriel.Lire, i =>
            {
                infos = i ?? new InfosMateriel();
                AfficherRam();
                AfficherDisques(infos.Disques);
                lblDisques.Text = "État de santé indiqué par Windows. « Analyse détaillée » lit en plus l'usure, la température et les erreurs (avec l'autorisation de Windows).";
                Disposer();
            });
        }

        private void AfficherRam()
        {
            var b = infos.Barrettes;
            lvRam.BeginUpdate(); lvRam.Items.Clear();
            foreach (var x in b)
            {
                var it = lvRam.Ajouter(x, x.Emplacement + (x.Banque != "" && !x.Emplacement.Contains(x.Banque) ? " (" + x.Banque + ")" : ""), Format.Taille(x.Capacite), x.Type,
                    x.VitesseConfiguree > 0 ? x.VitesseConfiguree + " MT/s" : "", x.Fabricant, x.Reference);
                it.SubItems[1].Tag = x.Capacite;
            }
            lvRam.EndUpdate();
            lvRam.Height = Math.Min(170, 34 + Math.Max(1, b.Count) * 24);
            long total = b.Sum(x => x.Capacite);
            string type = b.Select(x => x.Type).FirstOrDefault() ?? "";
            if (b.Count == 0) { lblRam.Text = "Barrettes non détectées"; lblRamDetail.Text = "Windows ne donne pas d'information sur les barrettes de ce PC (machine virtuelle ?)."; return; }
            lblRam.Text = Format.Taille(total) + " " + type + " · " + (infos.Reelle > 0 ? infos.Reelle + " MT/s" : "vitesse inconnue");
            lblCarteMere.Text = (infos.CarteMere != "" ? "Carte mère : " + infos.CarteMere : "") + (infos.Bios != "" ? "   ·   BIOS " + infos.Bios : "");
            Role r = Role.Discret;
            if (infos.Bridee)
            {
                int gain = (int)Math.Round(100.0 * (infos.Prevue - infos.Reelle) / infos.Reelle);
                lblRam.Text = Format.Taille(total) + " " + type + " · bridée à " + infos.Reelle + " MT/s";
                lblRamDetail.Text = "Tes barrettes sont prévues pour " + infos.Prevue + " MT/s mais tournent à " + infos.Reelle + " MT/s : le profil " + infos.Profil +
                    " n'est pas activé dans le BIOS. L'activer donne environ " + gain + " % de bande passante mémoire en plus (plus de FPS minimum dans les jeux, Windows plus réactif). C'est un réglage prévu par le fabricant, sans risque pour les barrettes.";
                r = Role.Orange;
                bGuide.Visible = true; bBios.Visible = Natif.Build >= 10240 || !Natif.EstWindows;
            }
            else if (infos.Prevue > 0)
            {
                lblRamDetail.Text = "✔ Les barrettes tournent à leur vitesse prévue (" + infos.Prevue + " MT/s) : le profil " + infos.Profil + " est bien activé.";
                r = Role.Vert; bGuide.Visible = false; bBios.Visible = false;
            }
            else
            {
                lblRamDetail.Text = "La vitesse prévue par le fabricant n'est pas indiquée dans la référence des barrettes : vérifie sur la boîte ou l'étiquette (par ex. « 6000 » ou « 3200 »). Si elle est plus élevée que " + (infos.Reelle > 0 ? infos.Reelle + " MT/s" : "la vitesse actuelle") + ", active le profil " + infos.Profil + " dans le BIOS.";
                bGuide.Visible = true; bBios.Visible = false;
            }
            if (b.Count == 1 && total >= 8L * 1073741824) lblRamDetail.Text += "\nUne seule barrette : une deuxième identique (double canal) peut nettement améliorer les performances.";
            lblRamDetail.RoleTexte = r; lblRamDetail.AppliquerTheme();
        }

        private void AfficherDisques(List<DisquePhysique> l)
        {
            lvDisques.BeginUpdate(); lvDisques.Items.Clear();
            foreach (var d in l)
            {
                var it = lvDisques.Ajouter(d, d.Nom, d.Type + (d.Bus != "" && d.Bus != "autre" ? " (" + d.Bus + ")" : ""), Format.Taille(d.Taille), NomSante(d.Sante), "", "", "", "");
                it.SubItems[2].Tag = d.Taille;
                if (!d.EnBonneSante) it.ForeColor = Theme.P.Rouge;
            }
            lvDisques.EndUpdate();
            lvDisques.Height = Math.Min(200, 34 + Math.Max(1, l.Count) * 24);
        }

        private static string NomSante(string s) { return s == "Healthy" ? "Bonne" : s == "Warning" ? "À surveiller" : s == "Unhealthy" ? "Mauvaise" : s == "" ? "" : "Inconnue"; }

        private void DetailsDisques(Dictionary<string, object> d)
        {
            if (d == null) return;
            var alertes = new List<string>();
            lvDisques.BeginUpdate(); lvDisques.Items.Clear();
            foreach (var x in Json.Objets(Json.Valeur(d, "Disques")))
            {
                string nom = Json.Texte(x, "Nom"), type = Json.Texte(x, "Type"), sante = Json.Texte(x, "Sante");
                object t = Json.Valeur(x, "Temperature"), tm = Json.Valeur(x, "TemperatureMax"), u = Json.Valeur(x, "Usure"), h = Json.Valeur(x, "Heures");
                long err = Json.Long(x, "ErreursLecture") + Json.Long(x, "ErreursEcriture");
                double? temp = t == null ? (double?)null : Convert.ToDouble(t), usure = u == null ? (double?)null : Convert.ToDouble(u);
                long? heures = h == null ? (long?)null : Convert.ToInt64(h);
                string tt = temp.HasValue ? temp.Value.ToString("0") + " °C" : "—";
                var it = lvDisques.Ajouter(x, nom, type + (Json.Texte(x, "Bus") != "" && Json.Texte(x, "Bus") != "autre" ? " (" + Json.Texte(x, "Bus") + ")" : ""), Format.Taille(Json.Long(x, "Taille")), NomSante(sante), tt,
                    usure.HasValue ? usure.Value.ToString("0") + " %" : "—", heures.HasValue ? Format.Nombre(heures.Value) + " h" : "—", Json.Valeur(x, "ErreursLecture") == null && Json.Valeur(x, "ErreursEcriture") == null ? "—" : err.ToString());
                it.SubItems[2].Tag = Json.Long(x, "Taille");
                var bulles = new List<string>();
                if (tm != null) bulles.Add("Température maximale atteinte : " + Convert.ToDouble(tm).ToString("0") + " °C");
                if (heures.HasValue) bulles.Add("Environ " + Format.Nombre(heures.Value / 24.0, 0) + " jours d'utilisation");
                if (usure.HasValue && type == "SSD") bulles.Add("Usure : " + usure.Value.ToString("0") + " % de la durée de vie prévue consommée");
                it.ToolTipText = string.Join("\n", bulles);
                bool alerte = sante != "Healthy" && sante != "";
                if (alerte) alertes.Add(nom + " : état de santé « " + NomSante(sante) + " ». Sauvegarde tes données.");
                if (type == "SSD" && usure >= 80) { alerte = true; alertes.Add(nom + " : usure à " + usure.Value.ToString("0") + " %, prévois son remplacement."); }
                if (temp >= 70) { alerte = true; alertes.Add(nom + " : température élevée (" + temp.Value.ToString("0") + " °C), vérifie la ventilation."); }
                if (err > 0) { alerte = true; alertes.Add(nom + " : " + err + " erreur(s) non corrigée(s). Sauvegarde tes données."); }
                if (alerte) it.ForeColor = Theme.P.Orange;
            }
            lvDisques.EndUpdate();
            if (Json.Bool(d, "Panne")) alertes.Insert(0, "Windows prévoit une panne prochaine sur un disque (SMART) : sauvegarde tes données dès maintenant.");
            if (!Json.Bool(d, "Admin")) lblDisques.Text = "Autorisation non accordée : seuls les états de santé sont affichés.";
            else if (alertes.Count == 0) lblDisques.Text = "✔ Aucun problème détecté sur tes disques.";
            else lblDisques.Text = "ATTENTION :\n- " + string.Join("\n- ", alertes);
            lblDisques.RoleTexte = alertes.Count > 0 ? Role.Orange : Json.Bool(d, "Admin") ? Role.Vert : Role.Discret; lblDisques.AppliquerTheme();
            Disposer();
        }

        private void RedemarrerBios()
        {
            if (!BoiteMessage.Question("Redémarrer le PC directement dans le BIOS ?\n\nEnregistre et ferme ton travail d'abord. Le PC redémarre 15 secondes après ta confirmation.\n\nUne fois dans le BIOS, suis le guide (garde-le sous les yeux sur ton téléphone, ou note les étapes).")) return;
            if (F.ChkSimu.Checked) { BoiteMessage.Info("Mode simulation : le PC ne redémarre pas."); return; }
            if (Elevation.Commande(60, "redemarrer-bios")) F.Statut("Redémarrage vers le BIOS dans 15 secondes...", true);
            else BoiteMessage.Attention("Le redémarrage vers le BIOS n'a pas été lancé (autorisation refusée, ou PC sans BIOS UEFI).\n\nRedémarre normalement et appuie plusieurs fois sur Suppr (ou F2) dès l'allumage.");
        }

        private void Guide()
        {
            if (infos == null) return;
            string p = infos.Profil, etapes;
            switch (infos.MarqueCarteMere)
            {
                case "ASUS":
                    etapes = "2. Appuie sur F7 pour passer en mode avancé (« Advanced Mode »), si l'écran est simplifié.\n3. Onglet « Ai Tweaker » : réglage « Ai Overclock Tuner », choisis « " + (p == "XMP" ? "XMP I" : p == "EXPO" ? "EXPO I" : "D.O.C.P.") + " ».\n   (En mode simple « EZ Mode », c'est le réglage « " + p + " » à droite de l'écran.)\n4. Appuie sur F10, puis « OK » : le PC enregistre et redémarre.";
                    break;
                case "MSI":
                    etapes = "2. En haut de l'écran principal, clique sur le bouton « " + (p == "XMP" ? "XMP" : "EXPO") + " » (ou « A-XMP ») pour l'allumer, profil 1.\n3. Appuie sur F10, puis « Yes » : le PC enregistre et redémarre.";
                    break;
                case "Gigabyte":
                    etapes = "2. Appuie sur F2 pour passer en mode avancé, si l'écran est simplifié.\n3. Onglet « Tweaker » : réglage « Extreme Memory Profile (X.M.P.) »" + (p == "EXPO" ? " ou « EXPO »" : "") + ", choisis « Profile 1 ».\n4. Appuie sur F10, puis « Save & Exit » : le PC enregistre et redémarre.";
                    break;
                case "ASRock":
                    etapes = "2. Appuie sur F6 pour passer en mode avancé, si l'écran est simplifié.\n3. Onglet « OC Tweaker », partie « DRAM Configuration » : « Load " + (p == "EXPO" ? "EXPO" : "XMP") + " Setting », choisis « Profile 1 ».\n4. Appuie sur F10, puis « Yes » : le PC enregistre et redémarre.";
                    break;
                case "Marque":
                    etapes = "2. Les PC de marque (Dell, HP, Lenovo...) ne proposent pas toujours ce réglage. Cherche dans les menus « Memory », « Performance » ou « Overclocking » un réglage « XMP », « EXPO » ou « Memory Profile » et choisis « Profile 1 ».\n3. Enregistre avec F10 (« Save & Exit »). Si tu ne trouves pas ce réglage, ton PC ne le permet pas : rien à faire.";
                    break;
                default:
                    etapes = "2. Cherche dans les menus « OC », « Tweaker », « Memory » ou « Overclocking » le réglage « " + p + " » (ou « XMP », « Memory Profile ») et choisis « Profile 1 ».\n3. Appuie sur F10 pour enregistrer (« Save & Exit ») : le PC redémarre.";
                    break;
            }
            string texte = "Activer le profil " + p + " de ta mémoire vive" + (infos.CarteMere != "" ? " (carte mère " + infos.CarteMere + ")" : "") + " :\n\n" +
                "1. Clique sur « Redémarrer dans le BIOS » (ou redémarre et appuie plusieurs fois sur Suppr ou F2 dès l'allumage).\n" + etapes + "\n\n" +
                "Bon à savoir :\n- Au premier démarrage après le changement, l'écran peut rester noir 1 à 3 minutes : le PC « apprend » la mémoire, n'éteins pas.\n" +
                "- Si le PC ne démarre plus (rare), il revient tout seul aux réglages d'origine après 2 ou 3 essais ; sinon, choisis « Load Optimized Defaults » dans le BIOS.\n" +
                "- Reviens ensuite sur cette page : elle doit afficher " + (infos.Prevue > 0 ? infos.Prevue + " MT/s" : "la nouvelle vitesse") + ".";
            BoiteMessage.Afficher(texte, "Nettoyage PC - activer " + p, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
