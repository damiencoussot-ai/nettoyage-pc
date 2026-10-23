// Nettoyage PC 5 - réglages de Windows (confidentialité, jeux) : état de chacun, cases à cocher, question avant,
// chaque réglage noté dans l'Historique (réversible). En simulation : on montre ce qui serait fait.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed class ReglagesWindows5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly List<Tuple<CheckBox, ReglageJeu>> cases = new List<Tuple<CheckBox, ReglageJeu>>();
        private readonly BoutonNoyau bOk;
        private FenetreInterieure fenetre;

        public static void Confidentialite(FenetrePrincipale f)
        {
            Ouvrir(f, "Confidentialité", "Réglages de ton compte Windows qui limitent la publicité et le pistage. Rien n'est désactivé côté sécurité : Windows Update, l'antivirus et le pare-feu ne sont pas touchés.", ReglagesWindows.Confidentialite);
        }

        public static void Jeux(FenetrePrincipale f)
        {
            Ouvrir(f, "Réglages des jeux", "Réglages de Windows qui donnent plus d'images par seconde. Tout est réversible depuis l'Historique.", ReglagesWindows.Jeux);
        }

        private static void Ouvrir(FenetrePrincipale f, string titre, string intro, Func<List<ReglageJeu>> definitions)
        {
            List<ReglageJeu> l;
            try { l = definitions(); } catch (Exception ex) { JournalDemarrage.Erreur(titre + " (version 5)", ex); l = new List<ReglageJeu>(); }
            var r = new ReglagesWindows5(f, intro, l);
            r.fenetre = f.Ouvrir(titre, r, 780);
        }

        private ReglagesWindows5(FenetrePrincipale f, string intro, List<ReglageJeu> definitions)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(new TextBlock { Text = Langue.T(intro), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            foreach (var d in definitions)
            {
                bool ok = false; try { ok = d.Etat(); } catch { }
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(0, 4, 0, 0) };
                var s = new StackPanel { Spacing = 2 };
                var cb = new CheckBox { IsChecked = !ok, IsEnabled = !ok, Content = new TextBlock { Text = Langue.T(d.Libelle), FontFamily = Habillage.Corps, FontSize = 14, FontWeight = FontWeight.SemiBold } };
                s.Children.Add(cb);
                s.Children.Add(new TextBlock { Text = Langue.T(d.Gain), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(30, 0, 0, 0) });
                g.Children.Add(s);
                var etat = new TextBlock { Text = Langue.T(ok ? "✔ déjà fait" : "à régler"), FontFamily = Habillage.Console, FontSize = 11.5, Foreground = Habillage.Pinceau(ok ? P.Vert : P.Orange), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(14, 6, 0, 0) };
                Grid.SetColumn(etat, 1); g.Children.Add(etat);
                Children.Add(new Border { Child = g, Padding = new Thickness(14, 10), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1) });
                cases.Add(Tuple.Create(cb, d));
                cb.IsCheckedChanged += (o, e) => bOk.IsEnabled = cases.Any(c => c.Item1.IsEnabled && c.Item1.IsChecked == true);
            }
            if (definitions.Count == 0) Children.Add(new TextBlock { Text = Langue.T("Aucun réglage à proposer sur ce PC."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret) });
            Children.Add(new TextBlock { Text = Langue.T("Chaque réglage est enregistré dans l'Historique et peut être annulé. Certains prennent effet à la prochaine ouverture de session ou après un redémarrage."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) });
            bOk = new BoutonNoyau("Appliquer les réglages cochés", true, 0, 40) { TaillePolice = 14, HorizontalAlignment = HorizontalAlignment.Left, IsEnabled = cases.Any(c => c.Item1.IsEnabled) };
            bOk.Clic += Appliquer;
            Children.Add(bOk);
        }

        private void Appliquer()
        {
            var choisis = cases.Where(c => c.Item1.IsEnabled && c.Item1.IsChecked == true).Select(c => c.Item2).ToList();
            if (choisis.Count == 0) return;
            string liste = string.Join("\n", choisis.Select(d => "- " + Langue.T(d.Libelle)));
            if (F.Simulation)
            {
                F.Demander("Mode simulation", Langue.T("Ces réglages seraient appliqués :") + "\n\n" + liste + "\n\n" + Langue.T("Passe en mode réel (pastille en haut) pour les appliquer vraiment."), "Compris", null, r => { });
                return;
            }
            F.Demander("Appliquer ces réglages ?", liste + "\n\n" + Langue.T("Chaque réglage est enregistré dans l'Historique et peut être annulé."), "Appliquer", "Annuler", oui =>
            {
                if (!oui) return;
                bOk.IsEnabled = false;
                F.Statut("Application des réglages…");
                // hors du fil de l'interface : un réglage de tout le PC demande l'autorisation de Windows
                Task.Run(() =>
                {
                    int faits = 0; var erreurs = new List<string>();
                    foreach (var d in choisis) { try { d.Appliquer(); faits++; } catch (Exception ex) { erreurs.Add(Langue.T(d.Libelle) + " : " + ex.Message); } }
                    return Tuple.Create(faits, erreurs);
                }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    if (fenetre != null) fenetre.Fermer();
                    string msg = Langue.T("Certains prennent effet à la prochaine ouverture de session ou après un redémarrage.");
                    if (t.Result.Item2.Count > 0) msg += "\n\n" + Langue.T("Non appliqué :") + "\n" + string.Join("\n", t.Result.Item2);
                    F.Statut(Langue.F("{0} réglage(s) appliqué(s).", t.Result.Item1));
                    F.Demander(Langue.F("{0} réglage(s) appliqué(s).", t.Result.Item1), msg, "Compris", null, r => { }, t.Result.Item2.Count > 0);
                }));
            });
        }
    }
}
