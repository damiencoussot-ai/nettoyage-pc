// Nettoyage PC - pages Sécurité et Sauvegarde
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class PageSecurite : Page
    {
        public PageSecurite(FenetrePrincipale f) : base(f, "Securite", "Sécurité", "Antivirus, réparation de Windows, points de restauration et confidentialité.", 0xEA18)
        {
            var g = NouvelleGrille();
            g.Controls.Add(NouvelleCarte("Bilan de sécurité", "Antivirus, pare-feu, contrôle de compte, Secure Boot, BitLocker, SMB1, compte Invité, Windows Update. Sans rien modifier.", "Lancer le bilan", Cible.A("secu"), true));
            g.Controls.Add(NouvelleCarte("Analyse antivirus rapide", "Met à jour Microsoft Defender puis analyse les emplacements où se cachent le plus souvent les menaces.", "Analyser", Cible.A("antivirus")));
            EnPlus(g, NouvelleCarte("Réparer Windows", "Répare l'image de Windows (DISM) et les fichiers système (SFC), avec un point de restauration avant. 20 à 50 min.", "Réparer", Cible.A("reparer")));
            g.Controls.Add(NouvelleCarte("Bouclier anti-arnaque", "Faux technicien au téléphone ? Repère les logiciels qui permettent de prendre la main sur ton PC (AnyDesk, TeamViewer...) et dit quoi faire.", "Vérifier", Cible.Faire(() => FenetreBouclier.Ouvrir(F))));
            g.Controls.Add(NouvelleCarte("Créer un point de restauration", "Une photo de Windows pour revenir en arrière en cas de problème après une installation ou une modification.", "Créer", Cible.A("point")));
            EnPlus(g, NouvelleCarte("Extensions des navigateurs", "Liste les extensions de Chrome, Edge, Brave, Vivaldi et Firefox, et signale celles à vérifier (hors boutique, accès à tous les sites).", "Vérifier", Cible.Faire(() => { using (var w = new FenetreExtensions(F)) w.ShowDialog(F); })));
            EnPlus(g, NouvelleCarte("Confidentialité", "Moins de publicité et de pistage : identifiant publicitaire, suggestions de Windows, recherche Bing du menu Démarrer. Réversible.", "Régler", Cible.Faire(() => FenetreReglagesWindows.Confidentialite(F))));
            EnPlus(g, NouvelleCarte("Restaurer le système", "Revenir à un point de restauration précédent si quelque chose ne fonctionne plus (outil de Windows).", "Ouvrir", Cible.Faire(() => Programmes.Ouvrir(Path.Combine(Chemins.Systeme32, "rstrui.exe")))));
            EnPlus(g, NouvelleCarte("Sécurité Windows", "Ouvre le centre de sécurité de Windows : antivirus, pare-feu, protection des applications et du navigateur.", "Ouvrir", Cible.Lien("windowsdefender:")));
        }
    }

    internal class Destination { public string Texte, Chemin; public long Libre; public override string ToString() { return Texte; } }

    internal class PageSauvegarde : Page
    {
        private static readonly string[][] Dossiers =
        {
            new[] { "Documents", "Documents", "1" }, new[] { "Images", "Images (photos)", "1" }, new[] { "Bureau", "Bureau", "1" },
            new[] { "Videos", "Vidéos", "0" }, new[] { "Musique", "Musique", "0" }, new[] { "Telechargements", "Téléchargements", "0" }
        };
        private readonly Carte carte = new Carte(760, 420) { Location = new Point(28, 4) };
        private readonly ListeDeroulante cbDest = new ListeDeroulante(430) { Location = new Point(22, 46) };
        private readonly Etiquette lblDest, lblTotal, lblDerniere;
        private readonly Bouton bSauvegarder;
        private readonly Dictionary<string, CaseACocher> cases = new Dictionary<string, CaseACocher>();
        private readonly List<Destination> autres = new List<Destination>();
        private Dictionary<string, long> tailles = new Dictionary<string, long>();
        private bool calcul;
        private string dernierDossier;

        public PageSauvegarde(FenetrePrincipale f) : base(f, "Sauvegarde", "Sauvegarde", "Copie tes photos et documents sur une clé USB ou un disque externe.", 0xE8C8)
        {
            Controls.Add(carte);
            carte.Controls.Add(new Etiquette("1.  Où sauvegarder ?", Theme.CarteTitre, Role.Texte, 20, 16));
            carte.Controls.Add(cbDest);
            var bActu = new Bouton("Actualiser", false, 100, 28) { Location = new Point(462, 45) };
            var bAutre = new Bouton("Autre dossier...", false, 130, 28) { Location = new Point(570, 45) };
            bActu.Click += (s, e) => ActualiserDestinations();
            bAutre.Click += (s, e) => AutreDossier();
            lblDest = new Etiquette("Branche ta clé USB ou ton disque externe, puis clique sur « Actualiser ».", Theme.Petit, Role.Discret, 22, 80);
            carte.Controls.AddRange(new Control[] { bActu, bAutre, lblDest });

            carte.Controls.Add(new Etiquette("2.  Quoi sauvegarder ?", Theme.CarteTitre, Role.Texte, 20, 112));
            int y = 142, col = 0;
            foreach (var d in Dossiers)
            {
                var cb = new CaseACocher(d[1], 22 + 360 * col, y) { Checked = d[2] == "1", AutoSize = false, Size = new Size(340, 26), Tag = d[0] };
                cb.CheckedChanged += (s, e) => MettreAJourTotal();
                carte.Controls.Add(cb); cases[d[0]] = cb;
                if (col == 1) { col = 0; y += 30; } else col = 1;
            }
            lblTotal = new Etiquette("", Theme.Normal, Role.Texte, 22, 238) { MaximumSize = new Size(700, 0) };
            carte.Controls.Add(lblTotal);

            carte.Controls.Add(new Etiquette("3.  Lancer la sauvegarde", Theme.CarteTitre, Role.Texte, 20, 278));
            bSauvegarder = new Bouton("Sauvegarder maintenant", true, 240, 44) { Location = new Point(22, 308), Font = Theme.CarteTitre, Cible = Cible.A("sauvegarde") };
            bSauvegarder.Click += (s, e) => Sauvegarder();
            F.EnregistrerBoutonAction(bSauvegarder);
            var bOuvrir = new Bouton("Ouvrir mes sauvegardes", false, 200, 44) { Location = new Point(274, 308) };
            bOuvrir.Click += (s, e) => OuvrirSauvegardes();
            lblDerniere = new Etiquette("", Theme.Petit, Role.Discret, 22, 362);
            var info = new Etiquette("Les fichiers sont copiés : rien n'est supprimé de ton PC. Chaque sauvegarde va dans un dossier daté. Les fichiers OneDrive restés « en ligne » ne sont pas copiés (ils sont déjà sauvegardés dans le cloud).", Theme.Petit, Role.Discret, 22, 384) { MaximumSize = new Size(720, 0) };
            carte.Controls.AddRange(new Control[] { bSauvegarder, bOuvrir, lblDerniere, info });
            cbDest.SelectedIndexChanged += (s, e) => MettreAJourTotal();
            f.ActionFinie += cle => { if (cle == "sauvegarde") { AfficherDerniere(); ActualiserDestinations(); } else MettreAJourTotal(); };
        }

        public override void PremiereFois() { ActualiserDestinations(); CalculerTailles(); AfficherDerniere(); }
        public override void Montrer() { MettreAJourTotal(); }

        private Destination Choisie { get { return cbDest.SelectedItem as Destination; } }

        private bool lectureDestinations;

        /// <summary>Les disques sont interrogés en arrière-plan : un disque externe en veille met parfois plusieurs secondes à répondre.</summary>
        private void ActualiserDestinations()
        {
            if (lectureDestinations) return;
            lectureDestinations = true;
            EnFond(LireDestinations, l => { lectureDestinations = false; RemplirDestinations(l ?? new List<Destination>()); });
        }

        private static List<Destination> LireDestinations()
        {
            var l = new List<Destination>();
            string systeme = (Path.GetPathRoot(Chemins.Windows) ?? "C:\\").Substring(0, 1).ToUpperInvariant();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    if (!d.IsReady) continue;
                    string lettre = d.Name.Substring(0, 1).ToUpperInvariant();
                    if (lettre == systeme || !(d.DriveType == DriveType.Removable || d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Network)) continue;
                    string type = Langue.T(d.DriveType == DriveType.Removable ? "Clé USB / carte" : d.DriveType == DriveType.Network ? "Réseau" : "Disque");
                    string vol = d.VolumeLabel != "" ? " « " + d.VolumeLabel + " »" : "";
                    l.Add(new Destination
                    {
                        Texte = lettre + ":  " + type + vol + "  —  " + Langue.F("{0} libres sur {1}", Format.Taille(d.AvailableFreeSpace), Format.Taille(d.TotalSize)),
                        Chemin = d.RootDirectory.FullName, Libre = d.AvailableFreeSpace
                    });
                }
                catch { }
            }
            return l;
        }

        private void RemplirDestinations(List<Destination> disques)
        {
            string avant = Choisie == null ? null : Choisie.Chemin;
            cbDest.BeginUpdate(); cbDest.Items.Clear();
            foreach (var x in disques) cbDest.Items.Add(x);
            foreach (var a in autres) cbDest.Items.Add(a);
            cbDest.EndUpdate();
            if (cbDest.Items.Count > 0)
            {
                cbDest.SelectedIndex = 0;
                if (avant != null) for (int i = 0; i < cbDest.Items.Count; i++) if (((Destination)cbDest.Items[i]).Chemin == avant) cbDest.SelectedIndex = i;
                lblDest.Text = "La sauvegarde sera rangée dans un dossier « NettoyagePC-Sauvegarde », daté, sur cette destination.";
            }
            else lblDest.Text = "Aucune clé USB ni disque externe détecté : branche-en un puis clique sur « Actualiser », ou choisis « Autre dossier... ».";
            MettreAJourTotal();
        }

        private void AutreDossier()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Choisis le dossier où ranger la sauvegarde (de préférence sur un autre disque que celui du PC)." })
            {
                if (dlg.ShowDialog(F) != DialogResult.OK) return;
                string chemin = dlg.SelectedPath;
                long libre = 0; try { libre = new DriveInfo(Path.GetPathRoot(chemin)).AvailableFreeSpace; } catch { }
                var d = new Destination { Texte = chemin + "  —  " + Langue.F("{0} libres", Format.Taille(libre)), Chemin = chemin, Libre = libre };
                autres.Add(d);
                ActualiserDestinations();
                cbDest.SelectedIndex = cbDest.Items.Count - 1;
                if (string.Equals(Path.GetPathRoot(chemin), Path.GetPathRoot(Chemins.Windows), StringComparison.OrdinalIgnoreCase))
                    BoiteMessage.Attention("Ce dossier est sur le même disque que Windows : si ce disque tombe en panne, la sauvegarde sera perdue avec lui.\n\nPour une vraie sauvegarde, préfère une clé USB ou un disque externe.");
            }
        }

        /// <summary>Taille de chaque dossier, calculée en arrière-plan (sans les fichiers OneDrive restés en ligne).</summary>
        private void CalculerTailles()
        {
            if (calcul) return;
            calcul = true;
            lblTotal.Text = "Calcul de la taille des dossiers...";
            var connus = Moteur.DossiersPersonnels();
            EnFond(() =>
            {
                var r = new Dictionary<string, long>();
                foreach (var kv in connus) if (Directory.Exists(kv.Value)) r[kv.Key] = TailleFichiers.Dossier(kv.Value);
                return r;
            }, r => { calcul = false; if (r != null) tailles = r; MettreAJourTotal(); });
        }

        private void MettreAJourTotal()
        {
            long total = 0;
            foreach (var d in Dossiers)
            {
                var cb = cases[d[0]]; long t;
                if (tailles.TryGetValue(d[0], out t)) { cb.Text = d[1] + "  (" + Format.Taille(t) + ")"; if (cb.Checked) total += t; }
                else cb.Text = d[1];
            }
            if (calcul) return;
            var dest = Choisie;
            string txt = "À copier : " + Format.Taille(total);
            bool ok = dest != null && cases.Values.Any(c => c.Checked);
            if (dest != null)
            {
                txt += "   ·   Place libre sur la destination : " + Format.Taille(dest.Libre);
                if (total > dest.Libre - 200L * 1048576) { txt += "   ·   PAS ASSEZ DE PLACE : décoche des dossiers ou prends une clé plus grande."; ok = false; }
            }
            lblTotal.Text = txt;
            lblTotal.RoleTexte = ok ? Role.Texte : Role.Rouge; lblTotal.AppliquerTheme();
            if (!F.ActionEnCours) bSauvegarder.Enabled = ok;
        }

        private void AfficherDerniere()
        {
            lblDerniere.Text = "Aucune sauvegarde faite avec Nettoyage PC pour l'instant.";
            try
            {
                string f = Chemins.Fichier("sauvegardes.csv");
                if (!File.Exists(f)) return;
                var l = File.ReadAllLines(f).Where(x => x.Trim() != "").LastOrDefault();
                if (l == null) return;
                var c = l.TrimStart('\uFEFF').Split(';');
                long o; if (c.Length < 3 || !long.TryParse(c[1], out o)) return;
                lblDerniere.Text = "Dernière sauvegarde : " + c[0] + "  —  " + Format.Taille(o) + "  —  " + c[2];
                dernierDossier = c[2];
            }
            catch { }
        }

        private void OuvrirSauvegardes()
        {
            string dossier = null;
            if (dernierDossier != null && Directory.Exists(dernierDossier)) dossier = Path.GetDirectoryName(dernierDossier);
            else if (Choisie != null) dossier = Path.Combine(Choisie.Chemin, "NettoyagePC-Sauvegarde");
            if (dossier != null && Directory.Exists(dossier)) Programmes.Ouvrir(dossier);
            else BoiteMessage.Info("Aucune sauvegarde trouvée sur cette destination (clé débranchée ?).");
        }

        private void Sauvegarder()
        {
            var dest = Choisie;
            var liste = Dossiers.Where(d => cases[d[0]].Checked).ToList();
            if (dest == null || liste.Count == 0) { BoiteMessage.Info("Choisis une destination et au moins un dossier."); return; }
            string conf = "Sauvegarder " + string.Join(", ", liste.Select(d => d[1])) + " vers " + dest.Chemin + " ?\n\nLes fichiers sont copiés, rien n'est supprimé de ton PC. Laisse la clé branchée jusqu'à la fin.";
            F.LancerAction("sauvegarde", new[] { "--sauvegarde", "--seul", "--destination", dest.Chemin, "--dossiers", string.Join(",", liste.Select(d => d[0])) }, conf);
        }
    }
}
