// Nettoyage PC - fenêtre du bouclier anti-arnaque : logiciels de prise en main à distance ouverts, conseils,
// et le bouton « On me demande de prendre la main sur mon PC ».
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreBouclier : FenetreTheme
    {
        private readonly FenetrePrincipale F;
        private readonly Etiquette lblEtat, lblDetail;
        private readonly Panel liste = new Panel { BackColor = Color.Transparent };
        private readonly Bouton bUrgence, bActualiser, bFermer;
        private List<AccesDistant> detectes = new List<AccesDistant>();

        public static void Ouvrir(FenetrePrincipale f, bool urgence = false)
        {
            using (var w = new FenetreBouclier(f)) { if (urgence) w.Shown += (s, e) => w.Urgence(); w.ShowDialog(f); }
        }

        internal FenetreBouclier(FenetrePrincipale f) : base("Bouclier anti-arnaque", 720, 560)
        {
            F = f;
            Controls.Add(new Etiquette("Bouclier anti-arnaque", Theme.Grand, Role.Texte, 24, 16));
            Controls.Add(new Etiquette("Les arnaques au « faux technicien » (Microsoft, banque, antivirus...) demandent presque toujours d'installer un logiciel qui permet de prendre la main sur le PC. Le bouclier les repère.",
                Theme.Normal, Role.Discret, 26, 56) { MaximumSize = new Size(660, 0) });
            lblEtat = new Etiquette("", Theme.CarteTitre, Role.Texte, 24, 108);
            lblDetail = new Etiquette("", Theme.Normal, Role.Discret, 26, 136) { MaximumSize = new Size(660, 0) };
            Controls.Add(lblEtat); Controls.Add(lblDetail);
            liste.Location = new Point(24, 176); liste.Size = new Size(656, 230);
            liste.AutoScroll = true;
            Controls.Add(liste);
            bUrgence = new Bouton("On me demande de prendre la main sur mon PC", true, 400, 44) { Location = new Point(24, 418), Font = Theme.CarteTitre };
            bUrgence.Click += (s, e) => Urgence();
            Controls.Add(bUrgence);
            Controls.Add(new Etiquette("Au téléphone, par message ou par une fenêtre qui fait peur ? Clique ici.", Theme.Petit, Role.Discret, 438, 432) { MaximumSize = new Size(250, 0) });
            bActualiser = new Bouton("Revérifier", false, 120, 34) { Location = new Point(24, 478) };
            bActualiser.Click += (s, e) => Actualiser();
            bFermer = new Bouton("Fermer", false, 110, 34) { Location = new Point(570, 478), DialogResult = DialogResult.Cancel };
            Controls.Add(bActualiser); Controls.Add(bFermer); CancelButton = bFermer;
            ClientSize = new Size(704, 526);
            Theme.Appliquer(this);
            Actualiser();
        }

        private void Actualiser()
        {
            try { detectes = Bouclier.Detecter(); } catch { detectes = new List<AccesDistant>(); }
            var confiance = Bouclier.Confiance();
            var aSignaler = detectes.Where(a => !confiance.Contains(a.Nom)).ToList();
            if (detectes.Count == 0)
            {
                lblEtat.Text = "✔  Aucun logiciel de prise en main à distance n'est ouvert"; lblEtat.RoleTexte = Role.Vert;
                lblDetail.Text = "Personne ne peut voir ni contrôler ton écran en ce moment.";
            }
            else if (aSignaler.Count == 0)
            {
                lblEtat.Text = "✔  Seuls des logiciels que tu as déclarés de confiance sont ouverts"; lblEtat.RoleTexte = Role.Vert;
                lblDetail.Text = "Tu peux retirer la confiance à tout moment ci-dessous.";
            }
            else
            {
                lblEtat.Text = aSignaler.Count == 1 ? Langue.F("⚠  {0} est ouvert", aSignaler[0].Nom) : Langue.F("⚠  {0} logiciels de prise en main à distance sont ouverts", aSignaler.Count);
                lblEtat.RoleTexte = Role.Orange;
                lblDetail.Text = "Si c'est toi qui l'as ouvert pour une personne de confiance (un proche, ton informaticien), tout va bien. Sinon, ferme-le : quelqu'un pourrait voir ton écran ou prendre la main.";
            }
            lblEtat.AppliquerTheme();
            liste.SuspendLayout();
            foreach (Control c in liste.Controls.Cast<Control>().ToList()) c.Dispose();
            liste.Controls.Clear();
            int y = 0, w = liste.ClientSize.Width - 4;
            foreach (var a in detectes)
            {
                var acc = a;
                bool conf = confiance.Contains(a.Nom);
                var ligne = new Carte(w, 64) { Location = new Point(0, y) };
                ligne.Controls.Add(new Etiquette(a.Nom, Theme.CarteTitre, Role.Texte, 16, 10));
                string etat = a.Connexions > 0 ? Langue.T("ouvert et connecté à Internet") : Langue.T("ouvert");
                if (a.Microsoft) etat += " · " + Langue.T("outil de Microsoft, utilisé aussi par des arnaqueurs");
                if (conf) etat += " · " + Langue.T("de confiance");
                ligne.Controls.Add(new Etiquette(etat, Theme.Petit, conf ? Role.Vert : a.Connexions > 0 ? Role.Orange : Role.Discret, 16, 36) { MaximumSize = new Size(w - 330, 0) });
                var bF = new Bouton("Fermer", !conf, 100, 32) { Location = new Point(w - 312, 16) };
                bF.Click += (s, e) => FermerLogiciel(acc);
                var bC = new Bouton(conf ? "Retirer la confiance" : "Je lui fais confiance", false, 190, 32) { Location = new Point(w - 202, 16) };
                Infobulle.Definir(bC, "Pour un logiciel que tu utilises toi-même (par exemple pour aider un proche) : le bouclier ne le signalera plus.");
                bC.Click += (s, e) => { Bouclier.DefinirConfiance(acc.Nom, !conf); Actualiser(); };
                ligne.Controls.Add(bF); ligne.Controls.Add(bC);
                liste.Controls.Add(ligne);
                y += 72;
            }
            liste.ResumeLayout();
            Theme.Appliquer(liste);
        }

        private void FermerLogiciel(AccesDistant a)
        {
            if (!BoiteMessage.Question(Langue.F("Fermer {0} maintenant ?\n\nSi quelqu'un était connecté à ton PC avec ce logiciel, la connexion sera coupée.", a.Nom))) return;
            bool ok = Bouclier.Fermer(a);
            Actualiser();
            if (!ok || Bouclier.Detecter().Any(x => x.Nom == a.Nom))
                BoiteMessage.Attention(Langue.F("{0} n'a pas pu être fermé complètement : il tourne comme un service de Windows.\n\nSi tu ne t'en sers pas, désinstalle-le : Accélérer > Applications, puis « Désinstaller ».", a.Nom));
        }

        /// <summary>Écran d'urgence : quoi faire tout de suite, et fermeture (avec confirmation) des logiciels ouverts.</summary>
        private void Urgence()
        {
            var aFermer = detectes.Where(a => !Bouclier.Confiance().Contains(a.Nom)).ToList();
            string conseils =
                "STOP : c'est très probablement une arnaque.\n\n" +
                "1.  Raccroche, ou ferme la conversation. Microsoft, ta banque, la police ou un antivirus ne t'appellent JAMAIS pour réparer ton PC.\n" +
                "2.  Ne donne aucun code, aucun mot de passe, aucun numéro de carte bancaire.\n" +
                "3.  N'installe rien et ne paie rien, même si on te fait peur (virus, compte bloqué, amende).\n" +
                "4.  Si une page Internet bloque l'écran avec une alarme : appuie sur Ctrl + Alt + Suppr, ouvre le Gestionnaire des tâches et ferme le navigateur. C'est une fausse alerte.\n" +
                "5.  Appelle un proche de confiance pour en parler. Si tu as déjà donné tes coordonnées bancaires, appelle tout de suite ta banque.";
            if (aFermer.Count > 0) conseils += "\n\n" + Langue.F("Logiciel(s) ouvert(s) en ce moment : {0}. Les fermer maintenant ?", string.Join(", ", aFermer.Select(a => a.Nom)));
            if (aFermer.Count > 0)
            {
                if (BoiteMessage.Afficher(conseils, "Bouclier anti-arnaque", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    foreach (var a in aFermer) Bouclier.Fermer(a);
                    Actualiser();
                    BoiteMessage.Info(Bouclier.ASignaler().Count == 0 ? "Les logiciels de prise en main à distance sont fermés. Personne ne voit plus ton écran." : "Certains logiciels n'ont pas pu être fermés (services de Windows) : désinstalle-les depuis Accélérer > Applications si tu ne t'en sers pas.");
                }
            }
            else BoiteMessage.Afficher(conseils + "\n\n" + Langue.T("Aucun logiciel de prise en main à distance n'est ouvert : personne ne voit ton écran en ce moment."), "Bouclier anti-arnaque", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
