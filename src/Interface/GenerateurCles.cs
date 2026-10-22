// Nettoyage PC - (éditeur) fenêtre « Créer une clé offerte », sans ligne de commande.
// S'ouvre quand on glisse le fichier de la clé privée sur NettoyagePC.exe, avec Creer-une-cle-offerte.cmd,
// ou avec NettoyagePC.exe --generer-licence (sans autre argument). La clé privée n'est jamais copiée ailleurs.
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreGenerateurCles : FenetreTheme
    {
        public const string NomJournal = "cles-offertes-creees.txt";
        private readonly ZoneTexte zFichier = new ZoneTexte(440) { ReadOnly = true, TabStop = false };
        private readonly ZoneTexte zNom = new ZoneTexte(300) { MaxLength = 60 };
        private readonly CaseACocher chkFin;
        private readonly DateTimePicker dtFin = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 140, Enabled = false };
        private readonly ZoneTexte zCle = new ZoneTexte(560) { Multiline = true, ReadOnly = true, WordWrap = true, Height = 76, ScrollBars = ScrollBars.Vertical };
        private readonly Etiquette lblFichier, lblEtat;
        private readonly Bouton bCreer, bCopier, bUtiliser;
        private readonly Lien lJournal;
        private string fichier = "";

        /// <summary>Ouvre la fenêtre seule (sans la fenêtre principale), aux couleurs choisies dans les réglages.</summary>
        public static int Lancer(string fichierCle)
        {
            try
            {
                var r = Parametres.Lire();
                Langue.Choisir(r.Langue);
                Licence.Charger();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool sombre = r.Theme == "Auto" ? Theme.WindowsEnSombre() : r.Theme != "Clair";
                Theme.P = sombre ? Theme.Sombre : Theme.Clair;
                using (var w = new FenetreGenerateurCles(fichierCle)) { w.StartPosition = FormStartPosition.CenterScreen; Application.Run(w); }
                return 0;
            }
            catch (Exception ex)
            {
                JournalDemarrage.Erreur("générateur de clés", ex);
                MessageBox.Show(Langue.T("Impossible d'ouvrir le générateur de clés") + " : " + ex.Message, "Nettoyage PC", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        /// <summary>Le fichier ressemble à une clé privée RSA (pour reconnaître un fichier glissé sur l'application).</summary>
        public static bool EstClePrivee(string chemin)
        {
            try
            {
                if (string.IsNullOrEmpty(chemin) || !chemin.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || !File.Exists(chemin) || new FileInfo(chemin).Length > 20000) return false;
                string t = File.ReadAllText(chemin);
                return t.Contains("<RSAKeyValue>") && t.Contains("<D>");
            }
            catch { return false; }
        }

        /// <summary>"" si le fichier est la clé privée de Nettoyage PC (elle correspond à la clé publique intégrée), sinon la raison.</summary>
        public static string ControlerClePrivee(string chemin)
        {
            try
            {
                if (string.IsNullOrEmpty(chemin) || !File.Exists(chemin)) return "fichier introuvable";
                using (var rsa = new RSACryptoServiceProvider())
                using (var pub = new RSACryptoServiceProvider())
                {
                    rsa.PersistKeyInCsp = false; pub.PersistKeyInCsp = false;
                    rsa.FromXmlString(File.ReadAllText(chemin));
                    if (rsa.PublicOnly) return "ce fichier ne contient que la clé publique : choisis le fichier cle-privee-NE-JAMAIS-PUBLIER.xml";
                    pub.FromXmlString(Boutique.ClePublique);
                    if (!rsa.ExportParameters(false).Modulus.SequenceEqual(pub.ExportParameters(false).Modulus)) return "cette clé privée ne correspond pas à Nettoyage PC";
                }
                return "";
            }
            catch { return "ce fichier n'est pas une clé privée valide"; }
        }

        internal FenetreGenerateurCles(string fichierCle) : base("Créer une clé offerte", 640, 560)
        {
            ShowInTaskbar = true; MinimizeBox = true; KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Chemins.Exe); } catch { }
            Controls.Add(new Etiquette("Créer une clé Premium offerte", Theme.Grand, Role.Texte, 22, 16));
            Controls.Add(new Etiquette("Pour offrir Premium à un proche ou à un testeur. La clé fonctionne sans Internet, sur le PC où elle est collée.", Theme.Normal, Role.Discret, 24, 54) { MaximumSize = new Size(580, 0) });

            // 1. la clé privée
            Controls.Add(new Etiquette("1.  Fichier de la clé privée", Theme.CarteTitre, Role.Texte, 22, 104));
            zFichier.Location = new Point(24, 134);
            Controls.Add(zFichier);
            var bParcourir = new Bouton("Choisir...", false, 120, 30) { Location = new Point(476, 132) };
            bParcourir.Click += (s, e) => Parcourir();
            Controls.Add(bParcourir);
            lblFichier = new Etiquette("", Theme.Petit, Role.Discret, 24, 166) { MaximumSize = new Size(580, 0) };
            Controls.Add(lblFichier);

            // 2. la personne
            Controls.Add(new Etiquette("2.  Prénom ou nom de la personne", Theme.CarteTitre, Role.Texte, 22, 200));
            zNom.Location = new Point(24, 230);
            zNom.TextChanged += (s, e) => MettreAJourBoutons();
            Controls.Add(zNom);
            chkFin = new CaseACocher("Clé limitée dans le temps, valable jusqu'au :", 24, 266);
            chkFin.CheckedChanged += (s, e) => dtFin.Enabled = chkFin.Checked;
            Controls.Add(chkFin);
            dtFin.MinDate = DateTime.Today.AddDays(1);
            dtFin.Value = DateTime.Today.AddMonths(1);
            dtFin.Location = new Point(390, 264);
            Controls.Add(dtFin);
            Controls.Add(new Etiquette("Sans date, la clé est valable à vie.", Theme.Petit, Role.Discret, 46, 292));

            // 3. la clé
            bCreer = new Bouton("Créer la clé", true, 180, 40) { Location = new Point(24, 324), Font = Theme.CarteTitre, Enabled = false };
            bCreer.Click += (s, e) => Creer();
            Controls.Add(bCreer);
            lblEtat = new Etiquette("", Theme.Normal, Role.Discret, 220, 334) { MaximumSize = new Size(390, 0) };
            Controls.Add(lblEtat);
            zCle.Location = new Point(24, 378);
            zCle.Width = 576;
            Controls.Add(zCle);
            bCopier = new Bouton("Copier la clé", false, 160, 36) { Location = new Point(24, 464), Enabled = false };
            bCopier.Click += (s, e) => Copier(true);
            Controls.Add(bCopier);
            bUtiliser = new Bouton("L'activer sur ce PC", false, 180, 36) { Location = new Point(194, 464), Enabled = false };
            Infobulle.Definir(bUtiliser, "Active Premium sur ce PC avec cette clé (pour toi, ou pour l'installer directement sur le PC de la personne).");
            bUtiliser.Click += (s, e) => UtiliserIci();
            Controls.Add(bUtiliser);
            lJournal = new Lien("Clés déjà créées", 392, 473);
            lJournal.LinkClicked += (s, e) => OuvrirJournal();
            Controls.Add(lJournal);
            var bFermer = new Bouton("Fermer", false, 100, 36) { Location = new Point(500, 464), DialogResult = DialogResult.Cancel };
            bFermer.Click += (s, e) => Close();
            Controls.Add(bFermer); CancelButton = bFermer; AcceptButton = bCreer;   // Entrée dans le champ du prénom : crée la clé
            ClientSize = new Size(624, 520);
            Theme.Appliquer(this);

            // fichier glissé sur l'application, sinon une clé privée posée à côté de l'application
            if (string.IsNullOrEmpty(fichierCle))
                foreach (var d in new[] { Environment.CurrentDirectory, Chemins.Ici })
                {
                    try { fichierCle = Directory.GetFiles(d, "cle-privee*.xml").FirstOrDefault(); } catch { }
                    if (!string.IsNullOrEmpty(fichierCle)) break;
                }
            ChoisirFichier(fichierCle);
            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) => { var f = e.Data.GetData(DataFormats.FileDrop) as string[]; if (f != null && f.Length > 0) ChoisirFichier(f[0]); };
            Shown += (s, e) => { if (fichier != "") zNom.Focus(); };
        }

        private void ChoisirFichier(string chemin)
        {
            fichier = "";
            if (string.IsNullOrEmpty(chemin))
            {
                zFichier.Text = "";
                Etat(lblFichier, "Clique sur « Choisir... » et prends le fichier cle-privee-NE-JAMAIS-PUBLIER.xml (ou glisse-le sur cette fenêtre).", Role.Discret);
            }
            else
            {
                zFichier.Text = chemin;
                string r = ControlerClePrivee(chemin);
                if (r == "") { fichier = chemin; Etat(lblFichier, "Clé privée reconnue ✔  Garde ce fichier pour toi : ne le publie jamais.", Role.Vert); }
                else Etat(lblFichier, Langue.T("Fichier refusé") + " : " + Langue.T(r) + ".", Role.Rouge);
            }
            lJournal.Visible = fichier != "" && File.Exists(Journal);
            MettreAJourBoutons();
        }

        private string Journal { get { return fichier == "" ? "" : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(fichier)), NomJournal); } }

        private void MettreAJourBoutons() { bCreer.Enabled = fichier != "" && zNom.Text.Trim().Length > 0; }

        private void Parcourir()
        {
            using (var dlg = new OpenFileDialog { Filter = Langue.T("Clé privée") + " (*.xml)|*.xml", Title = Langue.T("Choisir le fichier de la clé privée") })
            {
                try { if (fichier != "") dlg.InitialDirectory = Path.GetDirectoryName(fichier); } catch { }
                if (dlg.ShowDialog(this) == DialogResult.OK) ChoisirFichier(dlg.FileName);
            }
        }

        private void Creer()
        {
            string nom = zNom.Text.Trim();
            if (fichier == "" || nom == "") return;
            string fin = chkFin.Checked ? dtFin.Value.ToString("yyyy-MM-dd") : null;
            try
            {
                string cle = Licence.GenererCleOfferte(fichier, nom, fin);
                string titulaire, controle = Licence.VerifierCleOfferte(cle, out titulaire);
                if (controle != "") throw new InvalidOperationException(Langue.T(controle));
                zCle.Text = cle;
                bCopier.Enabled = bUtiliser.Enabled = true;
                try
                {
                    File.AppendAllText(Journal, DateTime.Now.ToString("yyyy-MM-dd HH:mm") + "\t" + nom + "\t" + (fin ?? "à vie") + "\t" + cle + "\r\n", new UTF8Encoding(true));
                    lJournal.Visible = true;
                }
                catch { }
                bool copiee = Copier(false);
                Etat(lblEtat, Langue.F(copiee ? "Clé créée pour « {0} » et copiée ✔ Colle-la dans un message (Ctrl+V)." : "Clé créée pour « {0} » ✔", nom), Role.Vert);
            }
            catch (Exception ex) { Etat(lblEtat, Langue.T("Impossible de créer la clé") + " : " + ex.Message, Role.Rouge); }
        }

        private bool Copier(bool afficher)
        {
            if (zCle.Text == "") return false;
            try
            {
                Clipboard.SetText(zCle.Text);
                if (afficher) Etat(lblEtat, "Clé copiée ✔ Colle-la dans un e-mail ou un SMS (Ctrl+V). La personne la colle dans Nettoyage PC > Premium > « J'ai une clé ».", Role.Vert);
                return true;
            }
            catch { zCle.Focus(); zCle.SelectAll(); return false; }
        }

        private void UtiliserIci()
        {
            if (zCle.Text == "") return;
            if (!BoiteMessage.Question("Activer Premium sur ce PC avec cette clé ?\n\nSi Nettoyage PC est ouvert, ferme-le et rouvre-le ensuite.")) return;
            string r = Licence.Activer(zCle.Text);
            if (r == "") Etat(lblEtat, "Premium est activé sur ce PC ✔", Role.Vert);
            else Etat(lblEtat, Langue.T("Clé non activée") + " : " + Langue.T(r), Role.Rouge);
        }

        private void OuvrirJournal()
        {
            if (File.Exists(Journal)) Programmes.Ouvrir(Path.Combine(Chemins.Windows, "notepad.exe"), "\"" + Journal + "\"");
        }

        private static void Etat(Etiquette e, string texte, Role role)
        {
            e.Text = texte; e.RoleTexte = role; e.AppliquerTheme();
        }
    }
}
