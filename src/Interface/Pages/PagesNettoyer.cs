// Nettoyage PC - pages Doublons et Disques (gros fichiers)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal static class Corbeille
    {
        /// <summary>Met un fichier à la corbeille (récupérable). Vrai si le fichier n'est plus là.</summary>
        public static bool Envoyer(string chemin)
        {
            try { Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(chemin, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin); }
            catch { return false; }
            return !File.Exists(chemin);
        }
    }

    // ================================================================ Disques et gros fichiers
    internal class PageDisques : Page
    {
        private readonly ZoneTexte dossier = new ZoneTexte(360) { Location = new Point(94, 241) };
        private readonly ListeDeroulante seuil = new ListeDeroulante(96) { Location = new Point(658, 241) };
        private readonly ListeSombre lv = new ListeSombre { Location = new Point(28, 278) };
        private readonly Etiquette info;
        private readonly Bouton bAnalyser, bOuvrir, bCorbeille;
        private bool enCours;

        public PageDisques(FenetrePrincipale f) : base(f, "Disques", "Disques et fichiers", "Santé des disques, SSD et gros fichiers.", 0xEDA2, "Disques et gros fichiers")
        {
            var g = NouvelleGrille();
            g.Controls.Add(NouvelleCarte("Optimiser les disques", "Santé SMART, usure et température, TRIM, ReTrim des SSD (jamais de défragmentation), défragmentation des disques durs.", "Optimiser", Cible.A("disques"), true));
            g.Controls.Add(NouvelleCarte("Vérifier les disques", "Comme « Optimiser », plus l'analyse du système de fichiers de chaque disque, en lecture seule.", "Vérifier", Cible.A("verifier")));
            g.Controls.Add(NouvelleCarte("Carte de l'espace disque", "Chaque dossier est un rectangle proportionnel à sa taille : tu vois d'un coup d'œil ce qui remplit ton disque. Clique pour entrer dans un dossier.", "Afficher la carte", Cible.Faire(() => new FenetreCarteEspace(F).Show(F))));
            Titre("Gros fichiers", 28, 210);
            Texte("Dossier :", 28, 244, Role.Texte);
            dossier.Text = Chemins.Profil;
            Controls.Add(dossier);
            NouveauBouton("Parcourir...", false, 100, 28, 462, 239, () =>
            {
                using (var d = new FolderBrowserDialog { SelectedPath = dossier.Text }) if (d.ShowDialog(F) == DialogResult.OK) dossier.Text = d.SelectedPath;
            });
            Texte("Taille min. :", 578, 244, Role.Texte);
            seuil.Items.AddRange(new object[] { "50" + Format.Mo, "100" + Format.Mo, "500" + Format.Mo, "1" + Format.Go }); seuil.SelectedIndex = 1;
            Controls.Add(seuil);
            bAnalyser = NouveauBouton("Analyser", true, 110, 28, 766, 239, Analyser);
            lv.Colonne("Taille", 90, HorizontalAlignment.Right); lv.Colonne("Fichier", 300); lv.Colonne("Dossier", 420); lv.Colonne("Modifié le", 100); lv.ColonneElastique = 2;
            lv.ActiverTri();
            Controls.Add(lv);
            info = Texte("Choisis un dossier puis clique sur « Analyser ». Les fichiers OneDrive non téléchargés sont ignorés.", 28, 0);
            bOuvrir = NouveauBouton("Ouvrir l'emplacement", false, 170, 32, 28, 0, () => { if (lv.SelectedItems.Count > 0) Programmes.MontrerDansExplorateur((string)lv.SelectedItems[0].Tag); });
            bCorbeille = NouveauBouton("Mettre à la corbeille", false, 170, 32, 206, 0, MettreACorbeille);
            bCorbeille.RoleTexte = Role.Rouge;
            // tout ce qui est sous les cartes suit la hauteur de la grille (une ou deux lignes selon la largeur)
            var dessous = Controls.Cast<Control>().Where(c => c != g && c != lv && c != info && c != bOuvrir && c != bCorbeille).Select(c => Tuple.Create(c, c.Top)).ToList();
            Action placer = () =>
            {
                int dy = g.Bottom - 200;
                foreach (var t in dessous) t.Item1.Top = t.Item2 + dy;
                int haut = 278 + dy;
                int h = Math.Max(220, ClientSize.Height - (haut - AutoScrollPosition.Y) - 90);
                lv.SetBounds(28, haut, ClientSize.Width - 56, h);
                int yb = lv.Bottom + 10;
                info.Location = new Point(28, yb + 40); bOuvrir.Location = new Point(28, yb); bCorbeille.Location = new Point(206, yb);
            };
            Resize += (s, e) => placer();
            g.SizeChanged += (s, e) => placer();
        }

        private void Analyser()
        {
            if (enCours) return;
            string racine = dossier.Text.Trim();
            if (!Directory.Exists(racine)) { BoiteMessage.Attention("Ce dossier n'existe pas."); return; }
            long s = new[] { 50L, 100L, 500L, 1024L }[seuil.SelectedIndex] * 1048576;
            enCours = true; bAnalyser.Enabled = false; lv.Items.Clear();
            info.Text = "Analyse en cours... (cela peut prendre quelques minutes sur un gros dossier)";
            EnFond(() =>
            {
                var l = new List<FileInfo>();
                var pile = new Stack<DirectoryInfo>(); pile.Push(new DirectoryInfo(racine));
                while (pile.Count > 0)
                {
                    var d = pile.Pop();
                    try
                    {
                        foreach (var e in d.EnumerateFileSystemInfos())
                        {
                            FileAttributes a; try { a = e.Attributes; } catch { continue; }
                            if ((a & FileAttributes.ReparsePoint) != 0) continue;
                            if ((a & FileAttributes.Directory) != 0) pile.Push((DirectoryInfo)e);
                            else if (!TailleFichiers.EstCloud(a)) { var fi = (FileInfo)e; try { if (fi.Length >= s) l.Add(fi); } catch { } }
                        }
                    }
                    catch { }
                }
                return l.OrderByDescending(x => x.Length).Take(300).ToList();
            }, res =>
            {
                enCours = false; bAnalyser.Enabled = true;
                res = res ?? new List<FileInfo>();
                lv.BeginUpdate();
                long total = 0;
                foreach (var fi in res)
                {
                    var it = lv.Ajouter(fi.FullName, Format.Taille(fi.Length), fi.Name, fi.DirectoryName, fi.LastWriteTime.ToString("dd/MM/yyyy"));
                    it.SubItems[0].Tag = fi.Length; it.SubItems[3].Tag = fi.LastWriteTime;
                    total += fi.Length;
                }
                lv.EndUpdate();
                info.Text = res.Count + " gros fichier(s) trouvé(s), " + Format.Taille(total) + " au total. Sélectionne-en pour les ouvrir ou les mettre à la corbeille (récupérables).";
            });
        }

        private void MettreACorbeille()
        {
            if (lv.SelectedItems.Count == 0) return;
            var choisis = lv.SelectedItems.Cast<ListViewItem>().ToList();
            long total = 0; foreach (var it in choisis) { try { total += new FileInfo((string)it.Tag).Length; } catch { } }
            if (!BoiteMessage.Question("Mettre " + choisis.Count + " fichier(s) à la corbeille (" + Format.Taille(total) + ") ?\n\nTu pourras les récupérer depuis la corbeille tant qu'elle n'est pas vidée.")) return;
            int ok = 0, refus = 0;
            var faits = new List<string>();
            foreach (var it in choisis)
            {
                string c = (string)it.Tag;
                if (Protection.CheminProtege(c)) { refus++; continue; }
                if (Corbeille.Envoyer(c)) { lv.Items.Remove(it); ok++; faits.Add(c); } else refus++;
            }
            if (faits.Count > 0) Historique.Ajouter("corbeille", "Gros fichiers : " + faits.Count + " fichier(s) mis à la corbeille", new Dictionary<string, object> { { "Fichiers", faits.ToArray() } });
            info.Text = ok + " fichier(s) mis à la corbeille (restaurables depuis l'Historique)." + (refus > 0 ? " " + refus + " ignoré(s) (fichiers système protégés ou en cours d'utilisation)." : "");
        }
    }

    // ================================================================ Doublons
    internal class CopieDoublon { public string Chemin, Nom, Dossier, Raison = ""; public DateTime Modif, Creation; public bool Supprimer; }
    internal class GroupeDoublons { public string Hash; public long Taille; public List<CopieDoublon> Fichiers; public bool Actif; public string Proposition = ""; }

    internal class PageDoublons : Page
    {
        private readonly ListeDeroulante ou = new ListeDeroulante(440) { Location = new Point(124, 56) };
        private readonly ListeDeroulante seuil = new ListeDeroulante(80) { Location = new Point(660, 56) };
        private readonly Bouton bAnalyser, bOuvrir, bEmpl, bIntel, bAucun, bSuppr;
        private readonly ListeSombre lvGrp = new ListeSombre { CheckBoxes = true, MultiSelect = false, Location = new Point(28, 96) };
        private readonly ListeSombre lvCop = new ListeSombre { CheckBoxes = true, MultiSelect = false };
        private readonly Etiquette lblCop, lbl;
        private string dossierPerso = "";
        private List<GroupeDoublons> groupes = new List<GroupeDoublons>();
        private bool maj, enCours;
        private volatile bool arret;
        private int etFichiers, etFait, etTotal; private string etPhase = "";
        private readonly System.Windows.Forms.Timer minuteur = new System.Windows.Forms.Timer { Interval = 300 };

        public PageDoublons(FenetrePrincipale f) : base(f, "Doublons", "Doublons", "Fichiers identiques : Nettoyage PC propose, tu vérifies et tu confirmes.", 0xE8C8)
        {
            AutoScroll = false;
            Texte("Trouve les fichiers au contenu identique (comparés octet par octet grâce à une empreinte SHA-256, pas seulement le nom). Nettoyage PC propose la copie à garder : vérifie, change les cases si besoin, puis confirme. Les copies vont à la corbeille et peuvent être restaurées depuis l'Historique.", 28, 4).MaximumSize = new Size(900, 0);
            Texte("Où chercher :", 28, 59, Role.Texte);
            ou.Items.AddRange(new object[] { "Mes dossiers (Documents, Images, Vidéos, Musique, Bureau, Téléchargements)", "Tout mon dossier utilisateur", "Choisir un dossier..." }); ou.SelectedIndex = 0;
            ou.SelectionChangeCommitted += (s, e) =>
            {
                if (ou.SelectedIndex != 2) return;
                using (var d = new FolderBrowserDialog { Description = "Dossier où chercher les fichiers en double", SelectedPath = dossierPerso })
                {
                    if (d.ShowDialog(F) == DialogResult.OK) { dossierPerso = d.SelectedPath; ou.Items[2] = "Dossier : " + d.SelectedPath; }
                    else if (dossierPerso == "") ou.SelectedIndex = 0;
                }
            };
            Controls.Add(ou);
            Texte("Taille min. :", 580, 59, Role.Texte);
            seuil.Items.AddRange(new object[] { "100" + Format.Ko, "1" + Format.Mo, "10" + Format.Mo, "100" + Format.Mo }); seuil.SelectedIndex = 1;
            Controls.Add(seuil);
            bAnalyser = NouveauBouton("Rechercher les doublons", true, 190, 30, 752, 54, Rechercher);
            lvGrp.Colonne("Fichier en double", 330); lvGrp.Colonne("Copies", 70); lvGrp.Colonne("Taille", 90, HorizontalAlignment.Right); lvGrp.Colonne("Récupérable", 100, HorizontalAlignment.Right); lvGrp.Colonne("Proposition", 300);
            lblCop = Titre("Copies du fichier sélectionné : coche celles à mettre à la corbeille", 28, 0);
            lvCop.Colonne("Corbeille ?", 300); lvCop.Colonne("Dossier", 380); lvCop.Colonne("Modifié le", 120); lvCop.Colonne("Créé le", 120); lvCop.ColonneElastique = 1;
            Controls.Add(lvGrp); Controls.Add(lvCop);
            bOuvrir = NouveauBouton("Ouvrir le fichier", false, 140, 32, 28, 0, () => { var c = CopieSelectionnee(); if (c != null) Programmes.OuvrirFichierSur(c.Chemin); });
            bEmpl = NouveauBouton("Ouvrir l'emplacement", false, 170, 32, 176, 0, () => { var c = CopieSelectionnee(); if (c != null) Programmes.MontrerDansExplorateur(c.Chemin); });
            bIntel = NouveauBouton("Sélection intelligente", false, 170, 32, 354, 0, () => { foreach (var g in groupes) Proposer(g); AfficherGroupes(); });
            bAucun = NouveauBouton("Tout décocher", false, 130, 32, 532, 0, () => { foreach (var g in groupes) ToutGarder(g); AfficherGroupes(); });
            bSuppr = NouveauBouton("Vérifier et mettre à la corbeille", true, 250, 32, 0, 0, Supprimer);
            bSuppr.Enabled = false;
            lbl = new Etiquette("Choisis où chercher puis clique sur « Rechercher les doublons ». Rien n'est supprimé sans ta confirmation.", Theme.Normal, Role.Discret) { AutoSize = false, AutoEllipsis = true };
            Controls.Add(lbl);
            Resize += (s, e) => Disposer();
            lvGrp.SelectedIndexChanged += (s, e) => { if (!maj) AfficherCopies(); };
            lvGrp.ItemChecked += GroupeCoche;
            lvCop.ItemChecked += CopieCochee;
            lvCop.DoubleClick += (s, e) => { if (lvCop.SelectedItems.Count > 0) Programmes.OuvrirFichierSur(((CopieDoublon)lvCop.SelectedItems[0].Tag).Chemin); };
            minuteur.Tick += (s, e) =>
            {
                if (!enCours) { minuteur.Stop(); return; }
                if (etPhase == "liste") Info("Recherche des fichiers... " + Format.Nombre(etFichiers) + " fichier(s) examiné(s)");
                else if (etPhase == "comparaison") Info("Comparaison du contenu... " + Format.Nombre(etFait) + " / " + Format.Nombre(etTotal) + " fichier(s) de même taille");
                else if (etPhase == "verification") Info("Vérification et mise à la corbeille... " + Format.Nombre(etFait) + " / " + Format.Nombre(etTotal));
            };
        }

        private void Disposer()
        {
            int w = ClientSize.Width - 56;
            int hDispo = Math.Max(300, ClientSize.Height - 96 - 100);
            int hGrp = (int)(hDispo * 0.48);
            lvGrp.Size = new Size(w, hGrp);
            lblCop.Location = new Point(28, 96 + hGrp + 10);
            lvCop.Location = new Point(28, 96 + hGrp + 36); lvCop.Size = new Size(w, hDispo - hGrp - 36);
            int yb = 96 + hDispo + 10;
            bOuvrir.Location = new Point(28, yb); bEmpl.Location = new Point(176, yb); bIntel.Location = new Point(354, yb); bAucun.Location = new Point(532, yb);
            bSuppr.Location = new Point(ClientSize.Width - 28 - 250, yb);
            lbl.Location = new Point(28, yb + 42); lbl.Size = new Size(w, 22);
        }

        private void Info(string t, Role r = Role.Discret) { lbl.Text = t; lbl.RoleTexte = r; lbl.AppliquerTheme(); }

        // ------------------------------------------------------------ Recherche : même taille -> même début -> même contenu
        private void Rechercher()
        {
            if (enCours) { arret = true; bAnalyser.Enabled = false; Info("Arrêt en cours..."); return; }
            var racines = new List<string>();
            switch (ou.SelectedIndex)
            {
                case 0: racines.AddRange(new[] { Chemins.Documents, Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Environment.GetFolderPath(Environment.SpecialFolder.MyMusic), Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Chemins.Telechargements }); break;
                case 1: racines.Add(Chemins.Profil); break;
                case 2: racines.Add(dossierPerso); break;
            }
            racines = racines.Where(r => !string.IsNullOrEmpty(r) && Directory.Exists(r)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (racines.Count == 0) { BoiteMessage.Info("Choisis un dossier qui existe."); return; }
            long s = new[] { 100L * 1024, 1048576L, 10L * 1048576, 100L * 1048576 }[seuil.SelectedIndex];
            enCours = true; arret = false; etFichiers = etFait = etTotal = 0; etPhase = "liste";
            groupes = new List<GroupeDoublons>(); AfficherGroupes();
            bAnalyser.Text = "Arrêter la recherche"; bSuppr.Enabled = false;
            minuteur.Start();
            EnFond(() => ChercherDoublons(racines, s), res =>
            {
                enCours = false; minuteur.Stop();
                bAnalyser.Text = "Rechercher les doublons"; bAnalyser.Enabled = true;
                if (res == null || arret) { Info("Recherche arrêtée."); return; }
                groupes = res;
                foreach (var g in groupes) Proposer(g);
                AfficherGroupes();
                if (groupes.Count == 0) Info("Aucun doublon trouvé parmi " + Format.Nombre(etFichiers) + " fichier(s). ✔", Role.Vert);
            });
        }

        private List<GroupeDoublons> ChercherDoublons(List<string> racines, long seuilOctets)
        {
            var exclus = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AppData", "$Recycle.Bin", "System Volume Information", "node_modules", ".git", "Program Files", "Program Files (x86)", "ProgramData", "Windows" };
            var parTaille = new Dictionary<long, List<FileInfo>>();
            var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pile = new Stack<DirectoryInfo>();
            foreach (var r in racines) pile.Push(new DirectoryInfo(r));
            while (pile.Count > 0)
            {
                if (arret) return null;
                var d = pile.Pop();
                DirectoryInfo[] sous = new DirectoryInfo[0]; FileInfo[] fichiers = new FileInfo[0];
                try { sous = d.GetDirectories(); } catch { }
                foreach (var s in sous) { if (((int)s.Attributes & 0x400) != 0 || exclus.Contains(s.Name)) continue; pile.Push(s); }
                try { fichiers = d.GetFiles(); } catch { }
                foreach (var f in fichiers)
                {
                    long l; try { l = f.Length; } catch { continue; }
                    if (l < seuilOctets || l <= 0 || ((int)f.Attributes & 0x441404) != 0 || !vus.Add(f.FullName)) continue;
                    Interlocked.Increment(ref etFichiers);
                    List<FileInfo> lot; if (!parTaille.TryGetValue(l, out lot)) parTaille[l] = lot = new List<FileInfo>();
                    lot.Add(f);
                }
            }
            etTotal = parTaille.Values.Where(x => x.Count >= 2).Sum(x => x.Count);
            etPhase = "comparaison";
            var res = new List<GroupeDoublons>();
            using (var sha = SHA256.Create())
            {
                var tampon = new byte[65536];
                Func<string, bool, string> empreinte = (chemin, complet) =>
                {
                    try
                    {
                        using (var fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            if (complet) return BitConverter.ToString(sha.ComputeHash(fs));
                            int n = fs.Read(tampon, 0, tampon.Length);
                            return BitConverter.ToString(sha.ComputeHash(tampon, 0, n));
                        }
                    }
                    catch { return null; }
                };
                foreach (var lot in parTaille.Values.Where(x => x.Count >= 2))
                {
                    var parDebut = new Dictionary<string, List<FileInfo>>();
                    foreach (var f in lot)
                    {
                        if (arret) return null;
                        Interlocked.Increment(ref etFait);
                        string h = empreinte(f.FullName, false); if (h == null) continue;
                        List<FileInfo> l; if (!parDebut.TryGetValue(h, out l)) parDebut[h] = l = new List<FileInfo>();
                        l.Add(f);
                    }
                    foreach (var sl in parDebut.Values.Where(x => x.Count >= 2))
                    {
                        var parContenu = new Dictionary<string, List<FileInfo>>();
                        foreach (var f in sl)
                        {
                            if (arret) return null;
                            string h = empreinte(f.FullName, true); if (h == null) continue;
                            List<FileInfo> l; if (!parContenu.TryGetValue(h, out l)) parContenu[h] = l = new List<FileInfo>();
                            l.Add(f);
                        }
                        foreach (var kv in parContenu.Where(x => x.Value.Count >= 2))
                            res.Add(new GroupeDoublons
                            {
                                Hash = kv.Key, Taille = kv.Value[0].Length,
                                Fichiers = kv.Value.Select(f => new CopieDoublon { Chemin = f.FullName, Nom = f.Name, Dossier = f.DirectoryName, Modif = f.LastWriteTime, Creation = f.CreationTime }).ToList()
                            });
                    }
                }
            }
            return res.OrderByDescending(g => g.Taille * (g.Fichiers.Count - 1)).ToList();
        }

        // ------------------------------------------------------------ Proposition intelligente : quelle copie garder ?
        private static int Penalite(CopieDoublon f, List<string> raisons, out bool sauvegarde)
        {
            int pen = 0;
            string nom = Path.GetFileNameWithoutExtension(f.Nom);
            if (Regex.IsMatch(nom, @"\s?\(\d+\)$") || Regex.IsMatch(nom, @"\s?\[\d+\]$")) { pen += 30; raisons.Add("nom en « (1) »"); }
            if (Regex.IsMatch(nom, @"(?i)(^|[\s_-])(copie|copy)([\s_-]?\d+)?$") || Regex.IsMatch(nom, @"(?i)^(copie de|copy of)\s")) { pen += 30; raisons.Add("nom « copie »"); }
            if (f.Chemin.StartsWith(Chemins.Telechargements.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) { pen += 20; raisons.Add("dans Téléchargements"); }
            if (Regex.IsMatch(f.Chemin, @"(?i)\\_A_verifier\\")) { pen += 15; raisons.Add("dans _A_verifier"); }
            if (Regex.IsMatch(f.Chemin, @"(?i)\\(temp|tmp|cache)\\")) { pen += 25; raisons.Add("dossier temporaire"); }
            sauvegarde = Regex.IsMatch(f.Chemin, @"(?i)\\(sauvegardes?|backups?|NettoyagePC-Sauvegarde)[^\\]*\\");
            return pen;
        }

        private static void Proposer(GroupeDoublons g)
        {
            var notes = new Dictionary<CopieDoublon, Tuple<int, List<string>, bool>>();
            foreach (var f in g.Fichiers) { var r = new List<string>(); bool sv; int p = Penalite(f, r, out sv); notes[f] = Tuple.Create(p, r, sv); }
            // on garde la copie la moins suspecte, puis la plus ancienne, puis le chemin le plus court
            var ordre = g.Fichiers.OrderBy(f => notes[f].Item1).ThenBy(f => f.Creation).ThenBy(f => f.Chemin.Length).ToList();
            var garde = ordre[0];
            garde.Supprimer = false; garde.Raison = "À garder : original probable";
            if (ordre.Count > 1 && notes[garde].Item1 < notes[ordre[1]].Item1) garde.Raison = "À garder : nom et emplacement d'origine";
            else if (ordre.Count > 1 && garde.Creation < ordre[1].Creation) garde.Raison = "À garder : la plus ancienne";
            int nb = 0, sv2 = 0;
            foreach (var f in ordre.Skip(1))
            {
                var n = notes[f];
                if (n.Item3) { f.Supprimer = false; f.Raison = "Gardée : copie de sauvegarde volontaire"; sv2++; }
                else { f.Supprimer = true; f.Raison = n.Item2.Count > 0 ? "Copie : " + string.Join(", ", n.Item2) : "Copie : créée plus tard"; nb++; }
            }
            g.Actif = nb > 0;
            g.Proposition = nb > 0 ? "Garder 1, mettre " + nb + " copie(s) à la corbeille" : "Tout garder (copies de sauvegarde)";
            if (sv2 > 0 && nb > 0) g.Proposition += " ; " + sv2 + " sauvegarde(s) gardée(s)";
        }

        private static void ToutGarder(GroupeDoublons g)
        {
            foreach (var f in g.Fichiers) { f.Supprimer = false; f.Raison = "Gardée"; }
            g.Actif = false; g.Proposition = "Tout garder";
        }

        // ------------------------------------------------------------ Affichage
        private void AfficherGroupes()
        {
            maj = true;
            int sel = lvGrp.SelectedIndices.Count > 0 ? lvGrp.SelectedIndices[0] : -1;
            lvGrp.BeginUpdate(); lvGrp.Items.Clear();
            foreach (var g in groupes)
            {
                int n = g.Fichiers.Count(x => x.Supprimer);
                var it = lvGrp.Ajouter(g, g.Fichiers[0].Nom, g.Fichiers.Count.ToString(), Format.Taille(g.Taille), Format.Taille(g.Taille * n), g.Proposition);
                it.SubItems[2].Tag = g.Taille; it.SubItems[3].Tag = g.Taille * n;
                it.Checked = g.Actif;
                if (!g.Actif) it.ForeColor = Theme.P.Discret;
            }
            lvGrp.EndUpdate();
            if (lvGrp.Items.Count > 0)
            {
                if (sel < 0 || sel >= lvGrp.Items.Count) sel = 0;
                lvGrp.Items[sel].Selected = true; lvGrp.Items[sel].EnsureVisible();
            }
            maj = false;
            AfficherCopies();
            Resume();
        }

        private void AfficherCopies()
        {
            maj = true;
            lvCop.BeginUpdate(); lvCop.Items.Clear();
            if (lvGrp.SelectedItems.Count > 0)
            {
                var g = (GroupeDoublons)lvGrp.SelectedItems[0].Tag;
                foreach (var f in g.Fichiers)
                {
                    var it = lvCop.Ajouter(f, f.Nom + "   —   " + f.Raison, f.Dossier, f.Modif.ToString("dd/MM/yyyy HH:mm"), f.Creation.ToString("dd/MM/yyyy HH:mm"));
                    it.Checked = f.Supprimer;
                    if (!f.Supprimer) it.ForeColor = Theme.P.Vert;
                }
            }
            lvCop.EndUpdate();
            maj = false;
        }

        private void Selection(out int fichiers, out long octets)
        {
            fichiers = 0; octets = 0;
            foreach (var g in groupes.Where(x => x.Actif)) { int n = g.Fichiers.Count(x => x.Supprimer); fichiers += n; octets += g.Taille * n; }
        }

        private void Resume()
        {
            if (groupes.Count == 0) { bSuppr.Enabled = false; return; }
            int nb; long oct; Selection(out nb, out oct);
            int copies = groupes.Sum(g => g.Fichiers.Count - 1); long total = groupes.Sum(g => g.Taille * (g.Fichiers.Count - 1));
            Info(groupes.Count + " fichier(s) en double, " + copies + " copie(s) en trop (" + Format.Taille(total) + "). Sélection : " + nb + " fichier(s) à mettre à la corbeille, " + Format.Taille(oct) + " libérés.");
            bSuppr.Enabled = nb > 0 && !enCours;
            bSuppr.Text = nb > 0 ? "Vérifier et mettre " + nb + " fichier(s) à la corbeille" : "Vérifier et mettre à la corbeille";
        }

        private void GroupeCoche(object s, ItemCheckedEventArgs e)
        {
            if (maj) return;
            var g = (GroupeDoublons)e.Item.Tag;
            bool avert = false;
            if (e.Item.Checked)
            {
                Proposer(g);
                if (!g.Actif) { Info("Ce fichier n'a que des copies de sauvegarde : rien n'est proposé. Coche toi-même la copie à mettre à la corbeille en dessous.", Role.Orange); avert = true; }
            }
            else ToutGarder(g);
            maj = true;
            e.Item.Checked = g.Actif;
            e.Item.SubItems[3].Text = Format.Taille(g.Taille * g.Fichiers.Count(x => x.Supprimer));
            e.Item.SubItems[4].Text = g.Proposition;
            e.Item.ForeColor = g.Actif ? Theme.P.Texte : Theme.P.Discret;
            maj = false;
            if (lvGrp.SelectedItems.Count > 0 && lvGrp.SelectedItems[0].Tag == g) AfficherCopies();
            if (!avert) Resume();
        }

        private void CopieCochee(object s, ItemCheckedEventArgs e)
        {
            if (maj || lvGrp.SelectedItems.Count == 0) return;
            var f = (CopieDoublon)e.Item.Tag; var g = (GroupeDoublons)lvGrp.SelectedItems[0].Tag;
            if (e.Item.Checked)
            {
                // il reste toujours au moins une copie de chaque fichier
                if (g.Fichiers.Count(x => !x.Supprimer && x != f) == 0)
                {
                    maj = true; e.Item.Checked = false; maj = false;
                    Info("Impossible : il faut garder au moins une copie de chaque fichier.", Role.Orange);
                    return;
                }
                f.Supprimer = true; f.Raison = "Choisie par toi : corbeille"; e.Item.ForeColor = Theme.P.Texte;
                g.Actif = true;
            }
            else { f.Supprimer = false; f.Raison = "Gardée (choisie par toi)"; e.Item.ForeColor = Theme.P.Vert; }
            e.Item.Text = f.Nom + "   —   " + f.Raison;
            maj = true;
            var ig = lvGrp.SelectedItems[0];
            int n = g.Fichiers.Count(x => x.Supprimer);
            ig.SubItems[3].Text = Format.Taille(g.Taille * n);
            g.Proposition = "Garder " + (g.Fichiers.Count - n) + ", mettre " + n + " copie(s) à la corbeille";
            if (n == 0) { g.Actif = false; g.Proposition = "Tout garder"; }
            ig.SubItems[4].Text = g.Proposition;
            ig.Checked = g.Actif; ig.ForeColor = g.Actif ? Theme.P.Texte : Theme.P.Discret;
            maj = false;
            Resume();
        }

        private CopieDoublon CopieSelectionnee()
        {
            if (lvCop.SelectedItems.Count > 0) return (CopieDoublon)lvCop.SelectedItems[0].Tag;
            if (lvGrp.SelectedItems.Count > 0) return ((GroupeDoublons)lvGrp.SelectedItems[0].Tag).Fichiers[0];
            return null;
        }

        // ------------------------------------------------------------ Vérification et mise à la corbeille
        private class Lot { public string Garde, Hash; public long Taille; public List<string> Supprimer; }

        private void Supprimer()
        {
            if (enCours) return;
            var lots = new List<Lot>();
            foreach (var g in groupes.Where(x => x.Actif))
            {
                var suppr = g.Fichiers.Where(x => x.Supprimer).Select(x => x.Chemin).ToList();
                var garde = g.Fichiers.FirstOrDefault(x => !x.Supprimer);
                if (suppr.Count == 0 || garde == null) continue;
                lots.Add(new Lot { Garde = garde.Chemin, Hash = g.Hash, Taille = g.Taille, Supprimer = suppr });
            }
            if (lots.Count == 0 || !Confirmer(lots)) return;
            enCours = true; arret = false; etFait = 0; etTotal = lots.Sum(l => l.Supprimer.Count); etPhase = "verification";
            bSuppr.Enabled = false; bAnalyser.Enabled = false;
            minuteur.Start();
            EnFond(() => MettreACorbeille(lots), r =>
            {
                enCours = false; minuteur.Stop(); bAnalyser.Enabled = true;
                if (r == null) { Info("Erreur pendant la mise à la corbeille.", Role.Rouge); return; }
                var faits = r.Item1; var ignores = r.Item2; long octets = r.Item3;
                if (faits.Count > 0)
                {
                    Historique.Ajouter("corbeille", "Doublons : " + faits.Count + " fichier(s) mis à la corbeille (" + Format.Taille(octets) + ")", new Dictionary<string, object> { { "Fichiers", faits.ToArray() } });
                    var l = new List<string> { "Nettoyage PC - doublons mis à la corbeille le " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"), "", "Mis à la corbeille :" };
                    l.AddRange(faits.Select(x => "  " + x));
                    if (ignores.Count > 0) { l.Add(""); l.Add("Non touchés :"); l.AddRange(ignores.Select(x => "  " + x)); }
                    FichierSur.Ecrire(Chemins.Fichier("doublons_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm") + ".txt"), string.Join("\r\n", l) + "\r\n");
                }
                var partis = new HashSet<string>(faits, StringComparer.OrdinalIgnoreCase);
                var restants = new List<GroupeDoublons>();
                foreach (var g in groupes)
                {
                    g.Fichiers = g.Fichiers.Where(x => !partis.Contains(x.Chemin)).ToList();
                    if (g.Fichiers.Count >= 2) { Proposer(g); restants.Add(g); }
                }
                groupes = restants;
                AfficherGroupes();
                string msg = faits.Count + " fichier(s) mis à la corbeille, " + Format.Taille(octets) + " libérés.";
                if (ignores.Count > 0) msg += "\n\n" + ignores.Count + " fichier(s) non touché(s) par sécurité :\n" + string.Join("\n", ignores.Take(10));
                msg += "\n\nTu peux tout restaurer depuis l'onglet Historique (« Annuler la sélection »), tant que la corbeille n'est pas vidée.";
                Info(faits.Count + " fichier(s) mis à la corbeille, " + Format.Taille(octets) + " libérés. Restaurables depuis l'Historique.", Role.Vert);
                BoiteMessage.Info(msg, "Nettoyage PC - doublons");
            });
        }

        private Tuple<List<string>, List<string>, long> MettreACorbeille(List<Lot> lots)
        {
            var faits = new List<string>(); var ignores = new List<string>(); long octets = 0;
            foreach (var lot in lots)
            {
                if (arret) break;
                // la copie gardée doit toujours exister et être identique
                if (!File.Exists(lot.Garde) || Empreinte(lot.Garde) != lot.Hash)
                {
                    foreach (var c in lot.Supprimer) { ignores.Add(c + " (la copie gardée a changé ou a disparu)"); Interlocked.Increment(ref etFait); }
                    continue;
                }
                foreach (var c in lot.Supprimer)
                {
                    Interlocked.Increment(ref etFait);
                    if (Protection.CheminProtege(c)) { ignores.Add(c + " (dossier protégé)"); continue; }
                    if (!File.Exists(c)) { ignores.Add(c + " (déjà supprimé ou déplacé)"); continue; }
                    long l; try { l = new FileInfo(c).Length; } catch { l = -1; }
                    if (l != lot.Taille || Empreinte(c) != lot.Hash) { ignores.Add(c + " (modifié depuis la recherche)"); continue; }
                    if (Corbeille.Envoyer(c)) { faits.Add(c); octets += lot.Taille; } else ignores.Add(c + " (refusé)");
                }
            }
            return Tuple.Create(faits, ignores, octets);
        }

        private static string Empreinte(string chemin)
        {
            try { using (var sha = SHA256.Create()) using (var fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) return BitConverter.ToString(sha.ComputeHash(fs)); }
            catch { return null; }
        }

        private bool Confirmer(List<Lot> lots)
        {
            int nb; long oct; Selection(out nb, out oct);
            using (var f = new FenetreTheme("Vérifier avant de mettre à la corbeille", 860, 600, true))
            {
                f.MinimumSize = new Size(700, 480);
                f.Controls.Add(new Etiquette(nb + " fichier(s) vont aller à la corbeille (" + Format.Taille(oct) + " libérés)", Theme.Grand, Role.Texte, 20, 14));
                f.Controls.Add(new Etiquette("Vérifie la liste : une copie de chaque fichier est toujours gardée. Juste avant, chaque fichier est revérifié (contenu toujours identique à la copie gardée) ; s'il a changé, il n'est pas touché.", Theme.Normal, Role.Discret, 22, 56) { MaximumSize = new Size(800, 0) });
                var liste = new RichTextBox { ReadOnly = true, BorderStyle = BorderStyle.None, BackColor = Theme.P.Carte, ForeColor = Theme.P.Texte, Font = Theme.Normal, DetectUrls = false, Location = new Point(22, 104), Size = new Size(800, 360), Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right };
                liste.HandleCreated += (s, e) => Natif.ThemeControle(liste.Handle, Theme.EstSombre, "DarkMode_Explorer", "Explorer");
                Action<string, Color> ecrire = (t, c) => { liste.SelectionStart = liste.TextLength; liste.SelectionLength = 0; liste.SelectionColor = c; liste.AppendText(t); };
                foreach (var l in lots)
                {
                    ecrire("✔  Garder : " + l.Garde + "\n", Theme.P.Vert);
                    foreach (var c in l.Supprimer) ecrire("      →  corbeille : " + c + "\n", Theme.P.Rouge);
                    ecrire("      (" + Format.Taille(l.Taille) + " chacun)\n\n", Theme.P.Discret);
                }
                liste.SelectionStart = 0;
                var chk = new CaseACocher("J'ai vérifié la liste", 22, 482) { Anchor = AnchorStyles.Bottom | AnchorStyles.Left };
                var ok = new Bouton("Mettre " + nb + " fichier(s) à la corbeille", true, 280, 36) { Location = new Point(452, 476), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, Enabled = false, DialogResult = DialogResult.OK };
                var annuler = new Bouton("Annuler", false, 90, 36) { Location = new Point(742, 476), Anchor = AnchorStyles.Bottom | AnchorStyles.Right, DialogResult = DialogResult.Cancel };
                chk.CheckedChanged += (s, e) => ok.Enabled = chk.Checked;
                f.Controls.AddRange(new Control[] { liste, chk, ok, annuler });
                f.CancelButton = annuler;
                return f.ShowDialog(F) == DialogResult.OK;
            }
        }
    }
}
