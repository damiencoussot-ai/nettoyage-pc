// Nettoyage PC - page Réglages : apparence, langue, entretien automatique, journaux, raccourci, à propos
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class PageReglages : Page
    {
        private readonly Bouton bTheme, bEntretien;
        private readonly ListeDeroulante cbLangue = new ListeDeroulante(264) { Location = new Point(18, 128) };
        private bool chargement;

        public PageReglages(FenetrePrincipale f) : base(f, "Reglages", "Réglages", "Apparence, langue, entretien automatique et informations sur l'application.", 0xE713)
        {
            var g = NouvelleGrille();
            g.Controls.Add(NouvelleCarte("Mon profil", "Ton pseudo, ta couleur, et le mode simple (trois gros boutons) ou complet.", "Modifier mon profil", Cible.Faire(() => FenetreProfil.Ouvrir(F, false)), true));
            var carteTheme = NouvelleCarte("Apparence", "Thème clair, sombre, ou automatique (suit le réglage de Windows). Le bouton lune / soleil en haut bascule aussi en un clic.", f.LibelleTheme(), Cible.Faire(() => { F.ChangerModeTheme(); ActualiserTheme(); }), true);
            bTheme = carteTheme.Controls.OfType<Bouton>().First();
            g.Controls.Add(carteTheme);

            // Couleur de l'interface : « Aurore » offerte, les autres avec Premium
            var carteCouleur = new Carte(300, 176) { Icone = 0xE790 };
            carteCouleur.Controls.Add(new Etiquette("Couleur de l'interface", Theme.CarteTitre, Role.Texte, 18, 18));
            carteCouleur.Controls.Add(new Etiquette("Noyau (cyan) est la couleur d'origine. Océan, Émeraude, Améthyste, Coucher de soleil et Rubis font partie de Premium.", Theme.Normal, Role.Discret, 18, 46) { AutoSize = false, Size = new Size(266, 64) });
            carteCouleur.Controls.Add(new SelecteurAccent(f) { Location = new Point(14, 118) });
            g.Controls.Add(carteCouleur);

            // Langue : la carte a une liste à la place du bouton
            var carteLangue = new Carte(300, 176) { Icone = 0xE774 };
            carteLangue.Controls.Add(new Etiquette("Langue", Theme.CarteTitre, Role.Texte, 18, 18));
            carteLangue.Controls.Add(new Etiquette("Langue de l'interface. Les journaux détaillés des actions restent en français.", Theme.Normal, Role.Discret, 18, 46) { AutoSize = false, Size = new Size(266, 76) });
            chargement = true;
            foreach (var l in Langue.Disponibles) cbLangue.Items.Add(l.Value);
            cbLangue.SelectedIndex = Math.Max(0, Langue.Disponibles.FindIndex(l => l.Key == Langue.Code));
            chargement = false;
            cbLangue.SelectedIndexChanged += (s, e) => ChangerLangue();
            carteLangue.Controls.Add(cbLangue);
            g.Controls.Add(carteLangue);

            g.Controls.Add(NouvelleCarte("Mode simple", "Pour un débutant (un parent, par exemple) : un seul écran avec trois gros boutons, sans les options avancées.", "Activer le mode simple", Cible.Faire(() =>
            {
                if (BoiteMessage.Question("Passer au mode simple ?\n\nL'écran ne montrera plus que trois gros boutons : Nettoyer mon PC, Vérifier mon PC, Appeler à l'aide. Le lien « Afficher toutes les options » en bas de l'écran permet de revenir au mode complet.")) F.AppliquerModeSimple(true);
            })));
            var carteEntretien = NouvelleCarte("Entretien automatique", "Nettoyage rapide chaque dimanche, optimisation complète toutes les 4 semaines et surveillance des disques, même application fermée.", "Activer", Cible.A("planifier"), true);
            bEntretien = carteEntretien.Controls.OfType<Bouton>().First();
            g.Controls.Add(carteEntretien);
            f.ActionFinie += cle => { if (cle == "planifier" || cle == "deplanifier") ActualiserEntretien(); };
            EnPlus(g, NouvelleCarte("Raccourci sur le Bureau", "Ajoute « Nettoyage PC » sur ton Bureau pour lancer l'application en un double-clic.", "Créer le raccourci", Cible.Faire(CreerRaccourci)));
            EnPlus(g, NouvelleCarte("À propos", "Version, éditeur, page officielle et engagements de Nettoyage PC.", "Afficher", Cible.Faire(() => APropos(F))));
            g.Controls.Add(NouvelleCarte("Informations légales", "Contrat de licence, conditions de vente, politique de confidentialité et mentions légales.", "Consulter", Cible.Faire(() => FenetreLegal.Ouvrir(F))));
            EnPlus(g, NouvelleCarte("Compatibilité de ce PC", "Version de Windows, matériel, disques, antivirus : ce que Nettoyage PC a détecté et les fonctions disponibles sur ce PC.", "Vérifier", Cible.A("compat")));
        }

        public override void Montrer() { ActualiserTheme(); ActualiserEntretien(); }

        /// <summary>Un seul bouton « Activer » / « Désactiver » selon que l'entretien automatique est programmé.</summary>
        private void ActualiserEntretien()
        {
            EnFond(() => Moteur.TacheExiste(Moteur.TacheHebdo), actif =>
            {
                bEntretien.Text = actif ? "Désactiver" : "Activer";
                bEntretien.Principal = !actif;
                bEntretien.Cible = Cible.A(actif ? "deplanifier" : "planifier");
                bEntretien.Invalidate();
            });
        }
        public override void ThemeChange() { ActualiserTheme(); }
        private void ActualiserTheme() { bTheme.Text = F.LibelleTheme(); }

        private void ChangerLangue()
        {
            if (chargement) return;
            string code = Langue.Disponibles[cbLangue.SelectedIndex].Key;
            if (code == F.Reglages.Langue) return;
            F.Reglages.Langue = code; F.Reglages.Ecrire();
            string q = code == "en" ? "The language will change when Nettoyage PC restarts. Restart now?" : code == "es" ? "El idioma cambiará al reiniciar Nettoyage PC. ¿Reiniciar ahora?" : "La langue changera au redémarrage de Nettoyage PC. Redémarrer maintenant ?";
            if (F.ActionEnCours) { BoiteMessage.Info(q.Split('.')[0] + "."); return; }
            if (!BoiteMessage.Question(q)) return;
            try
            {
                Process.Start(new ProcessStartInfo(Chemins.Exe, "--relance") { UseShellExecute = false, WorkingDirectory = Chemins.Ici });
                F.FermetureSansQuestion = true;
                F.Close();
            }
            catch (Exception ex) { BoiteMessage.Attention("Impossible de redémarrer : " + ex.Message); }
        }

        private void CreerRaccourci()
        {
            try
            {
                string bureau = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                dynamic wsh = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                dynamic lnk = wsh.CreateShortcut(Path.Combine(bureau, "Nettoyage PC.lnk"));
                lnk.TargetPath = Chemins.Exe;
                lnk.WorkingDirectory = Chemins.Ici;
                lnk.Description = "Nettoyage PC : nettoyage, optimisation et sécurité";
                string ico = Path.Combine(Chemins.Ici, "NettoyagePC.ico");
                lnk.IconLocation = File.Exists(ico) ? ico + ",0" : Chemins.Exe + ",0";
                lnk.Save();
                System.Threading.Tasks.Task.Run(() => Programmes.RafraichirIcones());
                BoiteMessage.Info("Raccourci « Nettoyage PC » créé sur le Bureau." + (Chemins.EstInstalle ? "" : "\n\nNe déplace pas le dossier de l'application, sinon le raccourci ne fonctionnera plus."));
            }
            catch (Exception ex) { BoiteMessage.Afficher("Impossible de créer le raccourci : " + ex.Message, "Nettoyage PC", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        public static void APropos(Form proprietaire)
        {
            string texte = "Nettoyage PC " + Appli.Version + "\n\nNettoyage, optimisation, sécurité, disques et mémoire vive pour Windows 10 et 11.\n\nÉditeur : Damien\nPage officielle : https://github.com/" + MisesAJour.Depot +
                "\nJournaux : " + Chemins.Donnees + "\n\nEngagements :\n- Tes documents, photos et vidéos ne sont jamais supprimés.\n- Un point de restauration est créé avant les opérations importantes.\n- Chaque modification peut être annulée (onglet Historique).\n- Nettoyage PC ne tourne jamais en administrateur : Windows demande l'autorisation pour chaque action qui touche tout le PC.\n- Les mises à jour sont vérifiées (SHA256) avant installation.\n- Ni publicité, ni pistage, ni télémétrie : seuls la vérification des mises à jour, la licence Premium achetée, le téléchargement de PresentMon (Preuve en FPS) et les tests de connexion que tu lances utilisent Internet. L'assistant fonctionne sur ton PC.\n\nWindows et Microsoft Defender sont des marques de Microsoft Corporation. Nettoyage PC est un logiciel indépendant, non affilié à Microsoft.\nLicence, conditions de vente et confidentialité : Réglages > Informations légales.";
            BoiteMessage.Afficher(texte, "À propos de Nettoyage PC", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
