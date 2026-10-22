// Nettoyage PC - pages Nettoyage, Optimisation, Démarrage, Applications
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class ElementDemarrage { public string Nom, Commande, Lieu, Appro, NomAppro; public bool Actif; }

    internal static class ElementsDemarrage
    {
        public static List<ElementDemarrage> Lister()
        {
            var l = new List<ElementDemarrage>();
            foreach (var s in GestionDemarrage.Sources)
                foreach (var n in Registre.Valeurs(s.Cle))
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    l.Add(new ElementDemarrage { Nom = n, Commande = Convert.ToString(Registre.Lire(s.Cle, n)), Lieu = s.Lieu, Appro = s.Appro, NomAppro = n, Actif = GestionDemarrage.EstApprouve(s.Appro, n) });
                }
            foreach (var d in new[]
            {
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.Startup), GestionDemarrage.ApproDossierUtilisateur, "Dossier Démarrage (ton compte)"),
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), GestionDemarrage.ApproDossierTous, "Dossier Démarrage (tous)")
            })
            {
                if (string.IsNullOrEmpty(d.Item1) || !Directory.Exists(d.Item1)) continue;
                try
                {
                    foreach (var f in new DirectoryInfo(d.Item1).GetFiles())
                    {
                        if (f.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        l.Add(new ElementDemarrage { Nom = Path.GetFileNameWithoutExtension(f.Name), Commande = f.FullName, Lieu = d.Item3, Appro = d.Item2, NomAppro = f.Name, Actif = GestionDemarrage.EstApprouve(d.Item2, f.Name) });
                    }
                }
                catch { }
            }
            return l;
        }
    }

    internal class PageNettoyage : Page
    {
        public PageNettoyage(FenetrePrincipale f) : base(f, "Nettoyage", "Nettoyage", "Libère de l'espace sans toucher à tes documents.", 0xE74D)
        {
            var g = NouvelleGrille();
            g.Controls.Add(NouvelleCarte("Nettoyage rapide", "Fichiers temporaires, caches de Windows, des navigateurs et des applications, corbeille, vieux téléchargements rangés à part.", "Nettoyer", Cible.A("standard"), true));
            g.Controls.Add(NouvelleCarte("Nettoyage profond", "Tout le nettoyage rapide, plus Windows Update, Nettoyage de disque Windows et DISM, avec un point de restauration avant.", "Nettoyer en profondeur", Cible.A("complet")));
            g.Controls.Add(NouvelleCarte("Voir ce qui serait nettoyé", "Analyse tout ce que l'optimisation complète ferait et affiche l'espace récupérable, sans rien supprimer.", "Analyser", Cible.A("analyse")));
            EnPlus(g, NouvelleCarte("Anciens pilotes", "Windows garde les anciennes versions de chaque pilote (souvent plusieurs Go pour la carte graphique). Tu choisis lesquelles retirer.", "Voir les anciens pilotes", Cible.Faire(() => FenetreAnciensPilotes.Ouvrir(F))));
            EnPlus(g, NouvelleCarte("Rapport complet du PC", "Espace disque, gros fichiers, doublons, dossiers lourds, programmes volumineux. Enregistré dans tes Documents.", "Générer le rapport", Cible.A("rapport")));
            EnPlus(g, NouvelleCarte("Téléchargements à vérifier", "Les téléchargements de plus de 90 jours sont rangés ici au lieu d'être supprimés. Garde ou supprime-les toi-même.", "Ouvrir le dossier", Cible.Faire(OuvrirAVerifier)));
        }

        private void OuvrirAVerifier()
        {
            string d = Path.Combine(Chemins.Telechargements, "_A_verifier");
            if (Directory.Exists(d)) Programmes.Ouvrir(d);
            else BoiteMessage.Info("Le dossier « _A_verifier » n'existe pas encore : il est créé au premier nettoyage, quand des téléchargements de plus de 90 jours sont rangés.");
        }
    }

    internal class PageOptimisation : Page
    {
        private Bouton bModeJeu, bAlim;
        private Etiquette lblAlim;
        public PageOptimisation(FenetrePrincipale f) : base(f, "Optimisation", "Optimisation", "Performances, mémoire vive et mises à jour.", 0xE945)
        {
            var g = NouvelleGrille();
            var carteJeu = NouvelleCarte("Mode jeu", "Avant de jouer : performances maximales et mémoire vive libérée. Un clic pour revenir comme avant après ta partie.", "Activer le mode jeu", Cible.A("modejeu"), true);
            bModeJeu = carteJeu.Controls.OfType<Bouton>().First();
            g.Controls.Add(carteJeu);
            g.Controls.Add(NouvelleCarte("Bilan d'optimisation", "Durée du démarrage, programmes au démarrage, mode d'alimentation, mises à jour disponibles : des conseils concrets, sans rien modifier.", "Lancer le bilan", Cible.A("optim"), true));
            g.Controls.Add(NouvelleCarte("Mettre à jour les applications", "Met à jour tes logiciels avec winget, l'outil officiel de Microsoft. Corrige aussi des failles de sécurité.", "Mettre à jour", Cible.A("majapps")));
            var carteAlim = NouvelleCarte("Mode d'alimentation", "Performances élevées : PC plus réactif. Équilibré : plus économe, conseillé sur portable. Réversible à tout moment.", "Passer en Performances", Cible.A("perf"));
            bAlim = carteAlim.Controls.OfType<Bouton>().First();
            lblAlim = carteAlim.Controls.OfType<Etiquette>().Skip(1).First();
            g.Controls.Add(carteAlim);
            EnPlus(g, NouvelleCarte("Libérer la RAM", "Libère la mémoire vive inutilisée, idéal juste avant un jeu ou une application gourmande. Effet temporaire.", "Libérer", Cible.A("ramlib")));
            EnPlus(g, NouvelleCarte("Infos mémoire vive", "Tes barrettes (capacité, type, vitesse), applications les plus gourmandes et erreurs matérielles.", "Afficher", Cible.A("raminfo")));
            EnPlus(g, NouvelleCarte("Tester les barrettes", "Lance le Diagnostic de mémoire Windows pour détecter une barrette défectueuse. Demande un redémarrage.", "Tester", Cible.A("ramtest")));
            f.ActionFinie += cle => { ActualiserModeJeu(); ActualiserAlimentation(); };
        }

        public override void Montrer() { ActualiserModeJeu(); ActualiserAlimentation(); }

        /// <summary>Un seul bouton pour le mode d'alimentation : il propose l'autre mode que celui en cours.</summary>
        private void ActualiserAlimentation()
        {
            string id = Registre.Texte(@"HKLM\SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActivePowerScheme").ToLowerInvariant();
            bool perf = id.StartsWith("8c5e7fda") || id.StartsWith("e9a42b02");
            string actuel = perf ? "Performances élevées" : id.StartsWith("381b4222") ? "Équilibré" : id.StartsWith("a1841308") ? "Économie d'énergie" : id != "" ? "personnalisé" : "";
            lblAlim.Text = (actuel != "" ? Langue.F("Mode actuel : {0}.", Langue.T(actuel)) + " " : "") + Langue.T(perf ? "Équilibré consomme moins (conseillé sur portable)." : "Performances élevées rend le PC plus réactif.");
            bAlim.Text = perf ? "Revenir en Équilibré" : "Passer en Performances";
            bAlim.Cible = Cible.A(perf ? "equilibre" : "perf");
        }

        /// <summary>Le bouton devient « Terminer le mode jeu » tant que le mode jeu est actif.</summary>
        public void ActualiserModeJeu()
        {
            bool actif = File.Exists(Chemins.Fichier("modejeu.json"));
            bModeJeu.Text = actif ? "Terminer le mode jeu" : "Activer le mode jeu";
            bModeJeu.Cible = Cible.A(actif ? "finmodejeu" : "modejeu");
        }
    }

    internal class PageDemarrage : Page
    {
        private readonly ListeSombre lv = new ListeSombre { CheckBoxes = true, Location = new Point(28, 36), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Bouton bActu, bDossier;
        private bool chargement;

        public PageDemarrage(FenetrePrincipale f) : base(f, "Demarrage", "Démarrage", "Choisis les programmes qui se lancent avec Windows (réversible).", 0xE7E8)
        {
            AutoScroll = false;
            Texte("Décoche un programme pour l'empêcher de se lancer avec Windows. Rien n'est supprimé : recoche-le pour le réactiver. Effet au prochain démarrage.", 28, 4).MaximumSize = new Size(900, 0);
            lv.Colonne("Programme", 240); lv.Colonne("État", 90); lv.Colonne("Emplacement", 190); lv.Colonne("Commande", 460);
            lv.ActiverTri();
            Controls.Add(lv);
            bActu = NouveauBouton("Actualiser", false, 110, 32, 28, 0, Charger);
            bDossier = NouveauBouton("Ouvrir l'emplacement", false, 170, 32, 150, 0, OuvrirEmplacement);
            Resize += (s, e) =>
            {
                lv.Size = new Size(ClientSize.Width - 56, Math.Max(100, ClientSize.Height - 96));
                bActu.Location = new Point(28, ClientSize.Height - 48); bDossier.Location = new Point(150, ClientSize.Height - 48);
            };
            lv.ItemChecked += CaseChangee;
        }

        public override void PremiereFois() { Charger(); }

        private bool enLecture;

        /// <summary>Liste lue en arrière-plan (registre, dossiers de démarrage, tâches) : la page reste fluide.</summary>
        public void Charger()
        {
            if (enLecture) return;
            enLecture = true;
            EnFond(ElementsDemarrage.Lister, l => { enLecture = false; Remplir(l ?? new List<ElementDemarrage>()); });
        }

        private void Remplir(List<ElementDemarrage> elements)
        {
            chargement = true;
            lv.BeginUpdate(); lv.Items.Clear();
            foreach (var el in elements.OrderBy(x => x.Nom, StringComparer.CurrentCultureIgnoreCase))
            {
                var it = lv.Ajouter(el, el.Nom, el.Actif ? "Activé" : "Désactivé", el.Lieu, el.Commande);
                it.Checked = el.Actif;
                if (!el.Actif) it.ForeColor = Theme.P.Discret;
            }
            lv.EndUpdate();
            chargement = false;
        }

        private void CaseChangee(object s, ItemCheckedEventArgs e)
        {
            if (chargement) return;
            var it = e.Item; var el = (ElementDemarrage)it.Tag;
            try
            {
                GestionDemarrage.Approuver(el.Appro, el.NomAppro, it.Checked);
                el.Actif = it.Checked;
                if (it.Checked)
                {
                    it.SubItems[1].Text = "Activé"; it.ForeColor = Theme.P.Texte;
                    F.Statut("« " + el.Nom + " » se lancera de nouveau au démarrage.");
                    Historique.MarquerAnnulees(m => m.Type == "demarrage" && Registre.Normaliser(m.D("Appro")).Equals(el.Appro, StringComparison.OrdinalIgnoreCase) && m.D("NomAppro") == el.NomAppro);
                }
                else
                {
                    it.SubItems[1].Text = "Désactivé"; it.ForeColor = Theme.P.Discret;
                    F.Statut("« " + el.Nom + " » ne se lancera plus au démarrage (recoche-le pour annuler).");
                    Historique.Ajouter("demarrage", "Programme au démarrage désactivé : " + el.Nom, new Dictionary<string, object> { { "Appro", el.Appro }, { "NomAppro", el.NomAppro }, { "Nom", el.Nom } });
                }
            }
            catch (Exception ex)
            {
                F.Statut("Impossible de modifier « " + el.Nom + " » : " + ex.Message, true);
                chargement = true;
                try { it.Checked = !it.Checked; } finally { chargement = false; }
            }
        }

        private void OuvrirEmplacement()
        {
            if (lv.SelectedItems.Count == 0) return;
            string cmd = ((ElementDemarrage)lv.SelectedItems[0].Tag).Commande ?? "";
            string chemin = cmd;
            var m = Regex.Match(cmd, "^\"([^\"]+)\""); if (m.Success) chemin = m.Groups[1].Value;
            else { m = Regex.Match(cmd, @"^(\S+\.exe)", RegexOptions.IgnoreCase); if (m.Success) chemin = m.Groups[1].Value; }
            chemin = Environment.ExpandEnvironmentVariables(chemin);
            if (File.Exists(chemin)) Programmes.MontrerDansExplorateur(chemin);
            else BoiteMessage.Info("Fichier introuvable :\n" + chemin);
        }
    }

    internal class PageApplications : Page
    {
        private readonly ZoneTexte recherche = new ZoneTexte(260) { Location = new Point(110, 5) };
        private readonly ListeDeroulante tri = new ListeDeroulante(170) { Location = new Point(462, 5) };
        private readonly Etiquette nb;
        private readonly ListeSombre lv = new ListeSombre { MultiSelect = false, Location = new Point(28, 40), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Bouton bDesinst, bActu, bParam, bInutiles;
        private List<ProgrammeInstalle> applications = new List<ProgrammeInstalle>();

        public PageApplications(FenetrePrincipale f) : base(f, "Applications", "Applications", "Trouve et désinstalle les programmes dont tu ne te sers plus.", 0xE71D)
        {
            AutoScroll = false;
            Texte("Rechercher :", 28, 8, Role.Texte);
            Controls.Add(recherche);
            Texte("Trier par :", 392, 8, Role.Texte);
            tri.Items.AddRange(new object[] { "Taille", "Nom", "Date d'installation" }); tri.SelectedIndex = 0;
            Controls.Add(tri);
            nb = Texte("", 652, 8);
            lv.Colonne("Application", 330); lv.Colonne("Éditeur", 210); lv.Colonne("Taille", 90, HorizontalAlignment.Right); lv.Colonne("Installée le", 110); lv.Colonne("Version", 130); lv.ColonneElastique = 0;
            lv.ActiverTri();
            Controls.Add(lv);
            bDesinst = NouveauBouton("Désinstaller", true, 140, 32, 28, 0, Desinstaller);
            bActu = NouveauBouton("Actualiser", false, 110, 32, 180, 0, Charger);
            bParam = NouveauBouton("Ouvrir Paramètres > Applications", false, 240, 32, 302, 0, () => Programmes.Ouvrir("ms-settings:appsfeatures"));
            bInutiles = NouveauBouton("Applications préinstallées inutiles", false, 250, 32, 554, 0, () => { using (var w = new FenetreAppsInutiles(F)) w.ShowDialog(F); });
            Resize += (s, e) =>
            {
                lv.Size = new Size(ClientSize.Width - 56, Math.Max(100, ClientSize.Height - 100));
                int y = ClientSize.Height - 48;
                bDesinst.Location = new Point(28, y); bActu.Location = new Point(180, y); bParam.Location = new Point(302, y); bInutiles.Location = new Point(554, y);
            };
            recherche.TextChanged += (s, e) => Afficher();
            tri.SelectedIndexChanged += (s, e) => { if (applications.Count > 0) Afficher(); };
            lv.DoubleClick += (s, e) => Desinstaller();
        }

        public override void PremiereFois() { Charger(); }

        public void Charger()
        {
            nb.Text = "Chargement de la liste des applications...";
            bActu.Enabled = false;
            EnFond(ProgrammesInstalles.Lister, l => { applications = l ?? new List<ProgrammeInstalle>(); bActu.Enabled = true; Afficher(); });
        }

        private void Afficher()
        {
            string filtre = recherche.Text.Trim();
            IEnumerable<ProgrammeInstalle> l = applications;
            if (filtre != "") l = l.Where(a => a.Nom.IndexOf(filtre, StringComparison.CurrentCultureIgnoreCase) >= 0 || a.Editeur.IndexOf(filtre, StringComparison.CurrentCultureIgnoreCase) >= 0);
            switch (tri.SelectedIndex)
            {
                case 0: l = l.OrderByDescending(a => a.Octets); break;
                case 1: l = l.OrderBy(a => a.Nom, StringComparer.CurrentCultureIgnoreCase); break;
                case 2: l = l.OrderByDescending(a => a.Date ?? DateTime.MinValue); break;
            }
            var liste = l.ToList();
            lv.BeginUpdate(); lv.Items.Clear();
            long total = 0;
            foreach (var a in liste)
            {
                var it = lv.Ajouter(a, a.Nom, a.Editeur, a.Octets > 0 ? Format.Taille(a.Octets) : "", a.Date.HasValue ? a.Date.Value.ToString("dd/MM/yyyy") : "", a.Version);
                it.SubItems[2].Tag = a.Octets; it.SubItems[3].Tag = a.Date ?? DateTime.MinValue;
                total += a.Octets;
            }
            lv.EndUpdate();
            nb.Text = Langue.F("{0} application(s), {1} au total (taille estimée)", liste.Count, Format.Taille(total));
        }

        private void Desinstaller()
        {
            if (lv.SelectedItems.Count == 0) { BoiteMessage.Info("Sélectionne d'abord une application dans la liste."); return; }
            var a = (ProgrammeInstalle)lv.SelectedItems[0].Tag;
            if (!BoiteMessage.Question("Désinstaller « " + a.Nom + " » ?\n\nLe programme de désinstallation de l'application va s'ouvrir : suis ses étapes. Si tu as un doute sur ce programme, réponds Non et renseigne-toi d'abord.")) return;
            string cmd = a.Desinstaller;
            if (Regex.IsMatch(cmd, "msiexec", RegexOptions.IgnoreCase)) cmd = Regex.Replace(cmd, @"/I\s*\{", "/X{", RegexOptions.IgnoreCase);
            // Les désinstallateurs demandent eux-mêmes l'autorisation de Windows s'ils en ont besoin
            Programmes.LancerCommande(cmd);
            F.Statut("Désinstallation de « " + a.Nom + " » lancée. Clique sur « Actualiser » quand elle est terminée.");
        }
    }
}
