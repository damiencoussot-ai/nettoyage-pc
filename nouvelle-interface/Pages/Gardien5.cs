// Nettoyage PC 5 - réglages du gardien en temps réel et de la sauvegarde automatique (Premium). Les réglages sont ceux
// de la version 4 (même fichier) : dans l'aperçu, le gardien lui-même reste celui de la version 4 installée, c'est
// donc elle qui l'active (la clé de démarrage de Windows doit pointer vers le programme installé).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal sealed class Gardien5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly ReglagesGardien R;
        private readonly TextBlock tEtat, tSauvegarde;
        private readonly BoutonNoyau bActiver;
        private readonly CheckBox cDemarrage, cDisque, cNettoyage, cAntivirus, cMemoire, cChaleur, cArnaque, cSante, cEnergie, cSauvegarde;
        private readonly ComboBox cbDest = new ComboBox { MinWidth = 380 }, cbFrequence = new ComboBox { Width = 240 };
        private readonly List<CheckBox> casesDossiers = new List<CheckBox>();
        private readonly List<string> racines = new List<string>();
        private FenetreInterieure fenetre;

        /// <summary>(captures) réglages d'exemple, sans lire ni écrire le fichier des réglages.</summary>
        public static ReglagesGardien Exemple;

        public static void Ouvrir(FenetrePrincipale f) { var g = new Gardien5(f); g.fenetre = f.Ouvrir("Gardien en temps réel", g, 820); }

        private Gardien5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            try { R = Exemple ?? ReglagesGardien.Lire(); } catch { R = new ReglagesGardien(); }
            Spacing = 8;
            Children.Add(Premium5.Intro("Une petite icône près de l'horloge veille sur ton PC et te prévient d'un problème, sans ralentir le PC (une vérification par minute)."));
            var etat = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 4, 0, 6) };
            tEtat = new TextBlock { FontFamily = Habillage.Affichage, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            etat.Children.Add(tEtat);
            bActiver = new BoutonNoyau("Activer le gardien", true, 230, 38) { TaillePolice = 14 };
            bActiver.Clic += Basculer;
            Grid.SetColumn(bActiver, 1); etat.Children.Add(bActiver);
            Children.Add(Premium5.Carte(etat, Habillage.Alpha(P.Accent, 90)));

            Children.Add(Premium5.Titre("Me prévenir quand :"));
            cDemarrage = Case("un programme s'ajoute au démarrage de Windows", R.Demarrage);
            cDisque = Case(Langue.F("le disque de Windows est presque plein (moins de {0} % libres)", R.SeuilDisque), R.Disque);
            cNettoyage = Case("et faire alors un nettoyage léger automatique (temporaires et caches : jamais la Corbeille ni les documents)", R.NettoyageAuto, 26);
            cAntivirus = Case("l'antivirus est désactivé", R.Antivirus);
            cMemoire = Case("la mémoire vive est saturée", R.Memoire);
            cChaleur = Case("la carte graphique surchauffe (plus de 90 °C)", R.Temperature);
            cArnaque = Case("un logiciel de prise en main à distance s'ouvre (anti-arnaque)", R.Arnaque);
            cSante = Case("le matériel montre des signes de faiblesse (erreurs du disque, du processeur, arrêts brutaux)", R.Sante);
            cEnergie = Case("Estimer la consommation électrique du PC et de chaque application (Coût en électricité)", R.Energie);
            cEnergie.Margin = new Thickness(0, 6, 0, 0);
            cDisque.IsCheckedChanged += (s, e) => cNettoyage.IsEnabled = cDisque.IsChecked == true;
            cNettoyage.IsEnabled = cDisque.IsChecked == true;

            var titreSauvegarde = Premium5.Titre("Sauvegarde automatique"); titreSauvegarde.Margin = new Thickness(0, 12, 0, 0);
            Children.Add(titreSauvegarde);
            cSauvegarde = Case("Sauvegarder automatiquement dès que ma clé USB ou mon disque de sauvegarde est branché", R.SauvegardeAuto);
            var ligne = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(26, 4, 0, 0) };
            ligne.Children.Add(Etiquette("Sur :"));
            cbDest.Margin = new Thickness(0, 0, 12, 6); ligne.Children.Add(cbDest);
            foreach (var t in new[] { "au plus une fois par jour", "une fois par semaine" }) cbFrequence.Items.Add(Langue.T(t));
            cbFrequence.SelectedIndex = R.SauvegardeJours >= 7 ? 1 : 0; cbFrequence.Margin = new Thickness(0, 0, 0, 6);
            ligne.Children.Add(cbFrequence);
            Children.Add(ligne);
            var dossiers = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(26, 0, 0, 0) };
            dossiers.Children.Add(Etiquette("Dossiers :"));
            foreach (var kv in new[] { new[] { "Documents", "Documents" }, new[] { "Images", "Images" }, new[] { "Bureau", "Bureau" }, new[] { "Videos", "Vidéos" }, new[] { "Musique", "Musique" } })
            {
                var c = new CheckBox { Content = Langue.T(kv[1]), IsChecked = R.SauvegardeDossiers.Contains(kv[0]), Tag = kv[0], Margin = new Thickness(0, 0, 16, 4) };
                dossiers.Children.Add(c); casesDossiers.Add(c);
            }
            Children.Add(dossiers);
            tSauvegarde = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(26, 4, 0, 0) };
            Children.Add(tSauvegarde);
            cSauvegarde.IsCheckedChanged += (s, e) => ActualiserSauvegarde();
            RemplirDestinations();

            var bas = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Margin = new Thickness(0, 12, 0, 0) };
            var bEnregistrer = new BoutonNoyau("Enregistrer", true, 150, 38) { TaillePolice = 14 };
            bEnregistrer.Clic += () => { if (Enregistrer(true) && fenetre != null) fenetre.Fermer(); };
            bas.Children.Add(bEnregistrer);
            Children.Add(bas);
            ActualiserEtat(); ActualiserSauvegarde();
        }

        private static TextBlock Etiquette(string t) { return new TextBlock { Text = Langue.T(t), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(Habillage.P.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 6) }; }

        private CheckBox Case(string texte, bool coche, double retrait = 0)
        {
            var c = new CheckBox { IsChecked = coche, Margin = new Thickness(retrait, 0, 0, 0), Content = new TextBlock { Text = Langue.T(texte), FontFamily = Habillage.Corps, FontSize = 13.5, TextWrapping = TextWrapping.Wrap } };
            Children.Add(c);
            return c;
        }

        private void RemplirDestinations()
        {
            string systeme = Path.GetPathRoot(Chemins.Windows ?? "C:\\");
            string actuelle = null;
            try { actuelle = Exemple != null ? null : Gardien.TrouverDestination(R.SauvegardeId); } catch { }
            cbDest.Items.Clear(); racines.Clear();
            if (Exemple == null && Natif.EstWindows)
                foreach (var d in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!d.IsReady || string.Equals(d.Name, systeme, StringComparison.OrdinalIgnoreCase)) continue;
                        if (d.DriveType != DriveType.Removable && d.DriveType != DriveType.Fixed && d.DriveType != DriveType.Network) continue;
                        cbDest.Items.Add(d.Name.TrimEnd('\\') + "  " + (d.DriveType == DriveType.Removable ? Langue.T("Clé USB / carte") : Langue.T("Disque")) + (d.VolumeLabel != "" ? " « " + d.VolumeLabel + " »" : "") + " — " + Format.Taille(d.AvailableFreeSpace) + " " + Langue.T("libres"));
                        racines.Add(d.RootDirectory.FullName);
                    }
                    catch { }
                }
            if (racines.Count == 0) { cbDest.Items.Add(Langue.T("Branche une clé USB ou un disque externe")); racines.Add(""); }
            int i = actuelle == null ? -1 : racines.FindIndex(r => string.Equals(r, actuelle, StringComparison.OrdinalIgnoreCase));
            if (i < 0) i = racines.FindIndex(r => { try { return r != "" && new DriveInfo(r).DriveType == DriveType.Removable; } catch { return false; } });
            cbDest.SelectedIndex = Math.Max(0, i);
        }

        private bool EnCours { get { try { return Exemple == null && Gardien.EnCours; } catch { return false; } } }

        private void ActualiserEtat()
        {
            var P = Habillage.P;
            bool marche = EnCours, actif = R.Actif;
            tEtat.Text = Langue.T(marche ? "● Le gardien veille sur ce PC." : actif ? "● Le gardien est activé : il démarre à l'ouverture de session." : "Le gardien est arrêté.");
            tEtat.Foreground = Habillage.Pinceau(marche || actif ? P.Vert : P.Discret);
            bActiver.Changer(Langue.T(actif ? "Désactiver le gardien" : "Activer le gardien"));
            bActiver.Principal = !actif; bActiver.InvalidateVisual();
        }

        private void ActualiserSauvegarde()
        {
            bool on = cSauvegarde.IsChecked == true;
            cbDest.IsEnabled = on; cbFrequence.IsEnabled = on; foreach (var c in casesDossiers) c.IsEnabled = on;
            Dictionary<string, object> der = null;
            try { der = Exemple != null ? null : Gardien.DerniereSauvegarde(); } catch { }
            DateTime d;
            string derniere = der != null && DateTime.TryParse(Json.Texte(der, "Date"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d)
                ? Langue.T("Dernière sauvegarde automatique") + " : " + d.ToLocalTime().ToString("dd/MM/yyyy HH:mm") + " (" + Format.Taille(Json.Long(der, "Octets")) + " " + Langue.T("de nouveautés") + ")."
                : Langue.T("Aucune sauvegarde automatique pour l'instant.");
            tSauvegarde.Text = Langue.F("Seuls les fichiers nouveaux ou modifiés sont copiés, dans le dossier « {0} ». Un fichier effacé du PC reste dans la sauvegarde. La copie se fait en arrière-plan : la clé peut être débranchée dès que la notification « terminée » apparaît.", "NettoyagePC-Sauvegarde\\" + Environment.MachineName + "\\Automatique") + "\n" + derniere;
        }

        private const string AvecVersion4 = "Dans cet aperçu de la version 5, le gardien s'active depuis la version 4 installée (Premium > Gardien en temps réel) : Windows doit le lancer depuis le programme installé, pas depuis ce fichier d'aperçu. Tes réglages ci-dessous sont communs aux deux versions.";

        private void Basculer()
        {
            if (Exemple != null) return;
            if (R.Actif)
            {
                F.Demander("Désactiver le gardien ?", "Il ne démarrera plus avec Windows. Tu peux le réactiver à tout moment.", "Désactiver", "Annuler", oui =>
                {
                    if (!oui) return;
                    try { Gardien.Desactiver(); R.Actif = false; } catch (Exception ex) { F.Demander("Gardien", ex.Message, "Compris", null, r => { }, true); }
                    ActualiserEtat();
                    F.Statut("Gardien désactivé : il ne démarrera plus avec Windows.");
                });
            }
            else
            {
                if (!Enregistrer(false)) return;
                if (Installation5.Apercu) { F.Demander("Activer le gardien", AvecVersion4, "Compris", null, r => { }); return; }
                string r = Gardien.Activer();
                if (r != "") { F.Demander("Le gardien n'a pas pu être activé", Langue.T(r), "Compris", null, x => { }, true); return; }
                R.Actif = true;
                ActualiserEtat();
                F.Demander("Gardien activé ✔", "Son icône apparaît près de l'horloge (parfois cachée derrière la petite flèche « ^ »). Il démarrera tout seul à chaque ouverture de session.", "Compris", null, x => { });
            }
        }

        private bool Enregistrer(bool prevenir)
        {
            if (Exemple != null) return true;
            ReglagesGardien r;
            try { r = ReglagesGardien.Lire(); } catch { r = new ReglagesGardien(); }
            r.Demarrage = cDemarrage.IsChecked == true; r.Disque = cDisque.IsChecked == true; r.NettoyageAuto = cNettoyage.IsChecked == true; r.Antivirus = cAntivirus.IsChecked == true;
            r.Memoire = cMemoire.IsChecked == true; r.Temperature = cChaleur.IsChecked == true; r.Arnaque = cArnaque.IsChecked == true; r.Sante = cSante.IsChecked == true; r.Energie = cEnergie.IsChecked == true;
            r.SauvegardeJours = cbFrequence.SelectedIndex == 1 ? 7 : 1;
            r.SauvegardeDossiers = casesDossiers.Where(c => c.IsChecked == true).Select(c => (string)c.Tag).ToList();
            r.SauvegardeAuto = cSauvegarde.IsChecked == true;
            if (r.SauvegardeAuto)
            {
                string racine = cbDest.SelectedIndex >= 0 && cbDest.SelectedIndex < racines.Count ? racines[cbDest.SelectedIndex] : "";
                if (racine == "" || !Directory.Exists(racine)) { F.Demander("Sauvegarde automatique", "Branche la clé USB ou le disque externe de sauvegarde, puis choisis-le dans la liste.", "Compris", null, x => { }); return false; }
                if (r.SauvegardeDossiers.Count == 0) { F.Demander("Sauvegarde automatique", "Choisis au moins un dossier à sauvegarder.", "Compris", null, x => { }); return false; }
                try
                {
                    r.SauvegardeId = Gardien.MarquerDestination(racine, string.Equals(Gardien.TrouverDestination(r.SauvegardeId), racine, StringComparison.OrdinalIgnoreCase) ? r.SauvegardeId : "");
                    var di = new DriveInfo(racine); r.SauvegardeLibelle = di.VolumeLabel != "" ? di.VolumeLabel : racine.TrimEnd('\\');
                }
                catch (Exception ex) { F.Demander("Sauvegarde automatique", Langue.T("Impossible de préparer cette destination :") + " " + ex.Message, "Compris", null, x => { }, true); return false; }
            }
            try { r.Ecrire(); Gardien.Recharger(); } catch (Exception ex) { F.Demander("Réglages du gardien", ex.Message, "Compris", null, x => { }, true); return false; }
            F.Statut("Réglages du gardien enregistrés.");
            if (prevenir && r.SauvegardeAuto && !r.Actif)
            {
                if (Installation5.Apercu) F.Demander("Le gardien est arrêté", Langue.T("La sauvegarde automatique est faite par le gardien.") + "\n\n" + Langue.T(AvecVersion4), "Compris", null, x => { });
                else F.Demander("Le gardien est arrêté", "La sauvegarde automatique est faite par le gardien, qui est arrêté. L'activer maintenant ?", "Activer", "Plus tard", oui =>
                {
                    if (!oui) return;
                    string e = Gardien.Activer();
                    if (e != "") F.Demander("Le gardien n'a pas pu être activé", Langue.T(e), "Compris", null, x => { }, true);
                    else F.Statut("Gardien activé ✔");
                });
            }
            return true;
        }
    }
}
