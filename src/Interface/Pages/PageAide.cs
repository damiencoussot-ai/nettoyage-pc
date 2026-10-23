// Nettoyage PC - page Aide : aide à distance (Assistance rapide de Windows), résumé du PC à transmettre, guide
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class PageAide : Page
    {
        private const string IdStoreAssistance = "9P7BP5VNWKX5";

        public PageAide(FenetrePrincipale f) : base(f, "Aide", "Aide à distance", "Un proche peut voir ton écran et t'aider depuis chez lui, en toute sécurité.", 0xE8D4, "Aide à distance")
        {
            var g = NouvelleGrille();
            var c1 = new Carte(616, 300, true);
            c1.Controls.Add(new Etiquette("Recevoir de l'aide d'un proche", Theme.CarteTitre, Role.Texte, 20, 18));
            var explication = new Etiquette(
                "1.  La personne qui t'aide ouvre l'Assistance rapide sur son PC et clique sur « Aider quelqu'un » : elle obtient un code de sécurité.\n" +
                "2.  Elle te donne ce code (par téléphone ou par message).\n" +
                "3.  Toi, clique sur « Ouvrir l'Assistance rapide », tape le code, puis accepte le partage de ton écran.\n\n" +
                "Tu vois tout ce qu'elle fait, et tu peux arrêter à tout moment avec le bouton « Quitter ».", Theme.Normal, Role.Discret, 20, 48) { MaximumSize = new Size(576, 0) };
            c1.Controls.Add(explication);
            var b1 = new Bouton("Ouvrir l'Assistance rapide", true, 260, 40) { Location = new Point(20, Math.Max(150, explication.Bottom + 16)), Font = Theme.CarteTitre };
            b1.Click += (s, e) => OuvrirAssistance();
            c1.Controls.Add(b1);
            var alerte = new Etiquette("⚠  N'accepte jamais une demande que tu n'as pas faite toi-même : Microsoft, ta banque ou un « technicien » ne t'appellent jamais pour prendre la main sur ton PC. Si on t'appelle pour ça, raccroche.", Theme.PetitGras, Role.Orange, 20, b1.Bottom + 14) { MaximumSize = new Size(576, 0) };
            c1.Controls.Add(alerte);
            c1.Height = Math.Max(c1.Height, alerte.Bottom + 18);
            g.Controls.Add(c1);

            g.Controls.Add(NouvelleCarte("Aider quelqu'un", "Pour dépanner un proche : ouvre l'Assistance rapide, clique sur « Aider quelqu'un » (compte Microsoft demandé), puis donne-lui le code.", "Ouvrir l'Assistance rapide", Cible.Faire(OuvrirAssistance)));
            g.Controls.Add(NouvelleCarte("Résumé de ce PC", "Windows, processeur, mémoire, disques, carte graphique et problèmes détectés : copié en un clic, à coller dans un message pour la personne qui t'aide.", "Copier le résumé", Cible.Faire(CopierResume), true));
            EnPlus(g, NouvelleCarte("Journal de démarrage", "Si Nettoyage PC se ferme ou affiche une erreur, ce fichier explique pourquoi : envoie-le à la personne qui t'aide.", "Montrer le fichier", Cible.Faire(() =>
            {
                if (File.Exists(JournalDemarrage.Fichier)) Programmes.MontrerDansExplorateur(JournalDemarrage.Fichier); else BoiteMessage.Info("Le journal de démarrage est vide pour l'instant.");
            })));
            EnPlus(g, NouvelleCarte("Journaux des actions", "Le détail de chaque nettoyage et de chaque bilan, dans Documents\\NettoyagePC (gardés 60 jours).", "Ouvrir le dossier", Cible.Faire(() => { Chemins.CreerDonnees(); Programmes.Ouvrir(Chemins.Donnees); })));
            EnPlus(g, NouvelleCarte("Premiers pas", "Commence par « Analyser sans rien modifier » sur l'Accueil. Coche « Mode simulation » en haut pour tester n'importe quelle action sans risque.", "Lire le guide", Cible.Faire(() =>
            {
                string l = Path.Combine(Chemins.Ici, "LISEZMOI.txt");
                if (File.Exists(l)) Programmes.Ouvrir(Path.Combine(Chemins.Windows, "notepad.exe"), "\"" + l + "\"");
                else BoiteMessage.Info("Le guide (LISEZMOI.txt) est introuvable dans le dossier de l'application.");
            })));
        }

        /// <summary>Assistance rapide : application du Microsoft Store (Windows 10 et 11), ou ancienne version intégrée ; sinon la page du Store.</summary>
        public void OuvrirAssistance()
        {
            try
            {
                var p = Paquets.Lister(true).FirstOrDefault(x => x.Nom.Equals("MicrosoftCorporationII.QuickAssist", StringComparison.OrdinalIgnoreCase) && x.AppId != "");
                if (p != null)
                {
                    Process.Start(new ProcessStartInfo(Path.Combine(Chemins.Windows, "explorer.exe"), "shell:AppsFolder\\" + p.Famille + "!" + p.AppId) { UseShellExecute = false });
                    F.Statut("L'Assistance rapide s'ouvre...");
                    return;
                }
                string ancien = Path.Combine(Chemins.Systeme32, "quickassist.exe");
                if (File.Exists(ancien)) { Programmes.Ouvrir(ancien); F.Statut("L'Assistance rapide s'ouvre..."); return; }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("assistance rapide", ex); }
            if (BoiteMessage.Question("L'Assistance rapide n'est pas installée sur ce PC.\n\nC'est une application gratuite de Microsoft. L'installer depuis le Microsoft Store ?"))
                Programmes.Ouvrir("ms-windows-store://pdp/?ProductId=" + IdStoreAssistance);
        }

        private void CopierResume()
        {
            F.Statut("Préparation du résumé du PC...", false, Role.AccentVif);
            EnFond(ResumePc, texte =>
            {
                if (texte == null) { F.Statut("Résumé impossible.", true); return; }
                try { Clipboard.SetText(texte); } catch { }
                F.Statut("Résumé du PC copié : colle-le (Ctrl+V) dans un message.", false, Role.Vert);
                BoiteMessage.Afficher("Ce résumé est copié : colle-le (Ctrl+V) dans un e-mail ou un message pour la personne qui t'aide.\nIl ne contient ni mot de passe ni fichier personnel.\n\n" + texte, "Résumé de ce PC", MessageBoxButtons.OK, MessageBoxIcon.Information);
            });
        }

        public static string ResumePc() { return ResumeDuPc.Creer(); }
    }
}
