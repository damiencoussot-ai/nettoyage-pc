// Nettoyage PC 5 - mesure des performances (Premium) : processeur (1 cœur et tous les cœurs), mémoire vive, disque
// (lecture, écriture, accès 4 Ko), environ une minute. Les fichiers de test sont supprimés à la fin ; rien d'autre n'est écrit.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class Benchmark5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly ComboBox cbDisque = new ComboBox { MinWidth = 300 };
        private readonly TextBox note = new TextBox { Width = 220 };
        private readonly BoutonNoyau bLancer;
        private readonly ProgressBar barre = new ProgressBar { Minimum = 0, Maximum = 100, Height = 6, IsVisible = false };
        private readonly TextBlock tEtape, tComparaison;
        private readonly Tableau tableau;
        private readonly List<string> racines = new List<string>();
        private bool enCours;

        /// <summary>(captures) mesures d'exemple : rien n'est mesuré.</summary>
        public static List<ResultatBenchmark> Exemple;

        public static void Ouvrir(FenetrePrincipale f)
        {
            f.Ouvrir("Mesure des performances", new Benchmark5(f), 1060);
        }

        private Benchmark5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(Premium5.Intro("Processeur, mémoire vive et disque mesurés en chiffres. Compare avant / après un réglage (EXPO, pilote, nettoyage) pour voir le vrai gain."));
            var l = new WrapPanel { Orientation = Orientation.Horizontal };
            l.Children.Add(Etiquette("Disque :"));
            try
            {
                foreach (var d in DriveInfo.GetDrives().Where(d => { try { return d.IsReady && d.DriveType == DriveType.Fixed; } catch { return false; } }))
                {
                    cbDisque.Items.Add(d.Name.TrimEnd('\\') + "  " + d.VolumeLabel + " (" + Langue.F("{0} libres", Format.Taille(d.AvailableFreeSpace)) + ")");
                    racines.Add(d.Name);
                }
            }
            catch { }
            if (cbDisque.Items.Count > 0) cbDisque.SelectedIndex = 0;
            cbDisque.Margin = new Thickness(0, 0, 16, 6);
            l.Children.Add(cbDisque);
            l.Children.Add(Etiquette("Note :"));
            note.Watermark = Langue.T("par ex. « avant EXPO »");
            ToolTip.SetTip(note, Langue.T("Facultatif : par exemple « avant EXPO » ou « après mise à jour du pilote », pour t'y retrouver."));
            note.Margin = new Thickness(0, 0, 16, 6);
            l.Children.Add(note);
            bLancer = new BoutonNoyau("Lancer la mesure (1 min)", true, 220, 36) { TaillePolice = 14, Margin = new Thickness(0, 0, 0, 6) };
            bLancer.Clic += Lancer;
            l.Children.Add(bLancer);
            Children.Add(l);
            tEtape = new TextBlock { Text = Langue.T("Ferme les jeux et les gros programmes pendant la mesure, pour un résultat juste."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tEtape);
            Children.Add(barre);
            tableau = new Tableau(false, false, new Colonne("Date", "145", false, true), new Colonne("Note", "1.4*"), new Colonne("Processeur 1 cœur", "118", true, true), new Colonne("Tous les cœurs", "108", true, true),
                new Colonne("Mémoire", "90", true, true), new Colonne("Disque lecture", "102", true, true), new Colonne("Disque écriture", "108", true, true), new Colonne("Accès 4 Ko", "86", true, true));
            tableau.HauteurListe = 260;
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            tComparaison = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13, LineHeight = 20, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            Children.Add(tComparaison);
            AttachedToVisualTree += (o, e) => Afficher();
            DetachedFromVisualTree += (o, e) => { if (enCours) Benchmark.Arret = true; };
        }

        private static TextBlock Etiquette(string t) { return new TextBlock { Text = Langue.T(t), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(Habillage.P.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 6) }; }

        private void Afficher()
        {
            List<ResultatBenchmark> h;
            try { h = Exemple ?? Benchmark.Historique(); } catch { h = new List<ResultatBenchmark>(); }
            tableau.Remplir(h.AsEnumerable().Reverse().Cast<object>(), o =>
            {
                var r = (ResultatBenchmark)o;
                return new[] { r.Date, r.Note + (r.RamMts > 0 ? (r.Note != "" ? " · " : "") + "RAM " + r.RamMts : ""), Benchmark.Points(r.Cpu1), Benchmark.Points(r.CpuN), r.Memoire.ToString("0.0", Appli.Fr) + " Go/s",
                    Format.Nombre(r.DisqueLecture) + " Mo/s", Format.Nombre(r.DisqueEcriture) + " Mo/s", r.DisqueAleatoire > 0 ? Format.Nombre(r.DisqueAleatoire) + " /s" : "—" };
            });
            tComparaison.Text = Langue.T(Benchmark.Comparaison(h));
            tComparaison.Foreground = Habillage.Pinceau(h.Count >= 2 ? Habillage.P.Texte : Habillage.P.Discret);
        }

        private void Lancer()
        {
            if (enCours) { Benchmark.Arret = true; return; }
            if (Exemple != null) return;
            if (F.Moteur.EnCours) { F.Demander("Une action est en cours", "Attends qu'elle se termine : la mesure serait faussée.", "Compris", null, r => { }); return; }
            int i = cbDisque.SelectedIndex; if (i < 0 || i >= racines.Count) return;
            string dossier = Benchmark.DossierTest(racines[i]);
            enCours = true; Benchmark.Arret = false; bLancer.Changer(Langue.T("Arrêter")); barre.Value = 0; barre.IsVisible = true;
            string n = (note.Text ?? "").Trim();
            Task.Run(() =>
            {
                ResultatBenchmark r = null;
                try { r = Benchmark.Lancer(dossier, (t, p) => Dispatcher.UIThread.Post(() => { tEtape.Text = Langue.T(t); barre.Value = p; })); }
                catch (Exception ex) { JournalDemarrage.Erreur("mesure des performances (version 5)", ex); }
                try { if (dossier.EndsWith("NettoyagePC-test")) Directory.Delete(dossier, false); } catch { }
                return r;
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false; bLancer.Changer(Langue.T("Lancer la mesure (1 min)")); barre.IsVisible = false;
                var r = t.Result;
                if (r == null) { tEtape.Text = Langue.T("Mesure arrêtée."); return; }
                r.Note = n; Benchmark.Enregistrer(r);
                tEtape.Text = Langue.T("Mesure terminée et enregistrée.");
                Afficher();
            }));
        }
    }
}
