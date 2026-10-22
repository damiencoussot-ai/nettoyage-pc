// Nettoyage PC - fenêtre « Informations légales » : licence, conditions de vente, confidentialité, mentions légales
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal class FenetreLegal : FenetreTheme
    {
        private readonly List<Bouton> onglets = new List<Bouton>();
        private readonly ZoneTexte zone = new ZoneTexte(600) { Multiline = true, ReadOnly = true, WordWrap = true, ScrollBars = ScrollBars.Vertical, BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, TabStop = false };
        private readonly Carte cadre = new Carte(600, 420) { Padding = new Padding(16, 14, 6, 14), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Etiquette lblNote;
        private string actuel = "LICENCE";
        private bool avecNote;

        public static void Ouvrir(Form proprietaire, string cle = "LICENCE")
        {
            using (var w = new FenetreLegal(cle)) w.ShowDialog(proprietaire);
        }

        public FenetreLegal(string cle = "LICENCE") : base("Informations légales", 940, 660, true)
        {
            MinimumSize = new Size(760, 480);
            Controls.Add(new Etiquette("Informations légales", Theme.Grand, Role.Texte, 20, 14));
            string sous = Langue.T("Contrat de licence, conditions de vente, données personnelles et éditeur de Nettoyage PC.");
            if (Langue.Code != "fr") sous += "  " + Langue.T("Ces documents sont rédigés en français (droit français).");
            Controls.Add(new Etiquette(sous, Theme.Normal, Role.Discret, 22, 52) { MaximumSize = new Size(880, 0) });
            int y = 92;
            foreach (var kv in DocumentsLegaux.Liste)
            {
                var b = new Bouton(kv.Value, false, 200, 40) { Location = new Point(20, y), Tag = kv.Key };
                b.Click += (s, e) => Montrer((string)((Bouton)s).Tag);
                Controls.Add(b); onglets.Add(b);
                y += 48;
            }
            lblNote = new Etiquette("Modèle : les passages entre crochets [ ] seront remplacés par les coordonnées de l'éditeur.", Theme.Petit, Role.Orange, 236, 92) { MaximumSize = new Size(660, 0), Visible = false };
            Controls.Add(lblNote);
            cadre.Controls.Add(zone);
            cadre.Location = new Point(236, 92);
            Controls.Add(cadre);
            var bCopie = new Bouton("Enregistrer une copie", false, 200, 36) { Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
            Infobulle.Definir(bCopie, "Enregistre ce document dans un fichier texte, pour le garder ou l'imprimer.");
            bCopie.Click += (s, e) => Enregistrer();
            var bFermer = new Bouton("Fermer", false, 110, 36) { Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
            bFermer.Click += (s, e) => Close();
            Controls.Add(bCopie); Controls.Add(bFermer); CancelButton = bFermer;
            Resize += (s, e) =>
            {
                int w = ClientSize.Width, h = ClientSize.Height;
                int haut = avecNote ? lblNote.Bottom + 8 : 92;
                cadre.Location = new Point(236, haut);
                cadre.Size = new Size(Math.Max(200, w - 256), Math.Max(120, h - haut - 66));
                bCopie.Location = new Point(20, h - 52); bFermer.Location = new Point(w - 130, h - 52);
            };
            Theme.Appliquer(this);
            Montrer(cle);
        }

        private void Montrer(string cle)
        {
            actuel = cle;
            foreach (var b in onglets) { b.Principal = (string)b.Tag == cle; b.Invalidate(); }
            string t = DocumentsLegaux.Texte(cle);
            if (t == "") t = Langue.T("Document introuvable dans cette version de Nettoyage PC.");
            zone.Text = t.Replace("\n", "\r\n");
            zone.SelectionStart = 0; zone.SelectionLength = 0; zone.ScrollToCaret();
            avecNote = DocumentsLegaux.AComplete(t);
            lblNote.Visible = avecNote;
            OnResize(EventArgs.Empty);
        }

        private void Enregistrer()
        {
            using (var dlg = new SaveFileDialog { Filter = Langue.T("Texte") + " (*.txt)|*.txt", FileName = "Nettoyage PC - " + DocumentsLegaux.Titre(actuel) + ".txt" })
            {
                try { dlg.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments); } catch { }
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try { File.WriteAllText(dlg.FileName, DocumentsLegaux.Texte(actuel).Replace("\n", "\r\n"), new UTF8Encoding(true)); BoiteMessage.Info("Document enregistré :\n" + dlg.FileName); }
                catch (Exception ex) { BoiteMessage.Attention("Impossible d'enregistrer le document : " + ex.Message); }
            }
        }
    }
}
