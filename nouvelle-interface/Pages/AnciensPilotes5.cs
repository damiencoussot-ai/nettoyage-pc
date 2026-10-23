// Nettoyage PC 5 - anciens pilotes : les versions que Windows garde « au cas où » après chaque mise à jour d'un pilote.
// La plus récente de chaque pilote et celles que Windows utilise sont toujours gardées. Le retrait passe par le moteur
// (autorisation de Windows, question avant).
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
    internal sealed class AnciensPilotes5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Tableau tableau;
        private readonly TextBlock info;
        private readonly BoutonNoyau bRetirer;
        private readonly HashSet<PilotesAnciens.Pilote> coches = new HashSet<PilotesAnciens.Pilote>();
        private FenetreInterieure fenetre;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var a = new AnciensPilotes5(f);
            a.fenetre = f.Ouvrir("Anciens pilotes", a, 980);
        }

        private AnciensPilotes5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(new TextBlock { Text = Langue.T("À chaque mise à jour d'un pilote, Windows garde l'ancienne version « au cas où ». Ces versions inutilisées peuvent être retirées : la plus récente de chaque pilote et celles que Windows utilise sont toujours gardées."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            tableau = new Tableau(true, false,
                new Colonne("Fournisseur", "2*"), new Colonne("Type", "1.2*"), new Colonne("Version", "1.3*", false, true),
                new Colonne("Date", "100", false, true, o => ((PilotesAnciens.Pilote)o).Date),
                new Colonne("Taille", "90", true, true, o => ((PilotesAnciens.Pilote)o).Taille), new Colonne("État", "1.6*"));
            tableau.HauteurListe = 380;
            tableau.CaseChangee = (o, v) =>
            {
                var p = (PilotesAnciens.Pilote)o;
                if (v && !p.Supprimable) return false;   // une version gardée ne peut pas être cochée
                if (v) coches.Add(p); else coches.Remove(p);
                Compter();
                return true;
            };
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            info = new TextBlock { Text = Langue.T("Analyse du magasin de pilotes…"), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap };
            Children.Add(info);
            bRetirer = new BoutonNoyau("Retirer les versions cochées", true, 0, 38) { TaillePolice = 14, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left };
            ToolTip.SetTip(bRetirer, Langue.T("Windows te demandera l'autorisation (bouclier) : clique sur « Oui ». Un pilote encore utilisé est toujours gardé (Windows refuse de le retirer)."));
            bRetirer.Clic += Retirer;
            Children.Add(bRetirer);
            AttachedToVisualTree += (s, e) => Charger();
        }

        private void Charger()
        {
            Task.Run(() => { try { return PilotesAnciens.Analyser(); } catch (Exception ex) { JournalDemarrage.Erreur("anciens pilotes (version 5)", ex); return new List<PilotesAnciens.Pilote>(); } }).ContinueWith(t => Dispatcher.UIThread.Post(() => Afficher(t.Result)));
        }

        private void Afficher(List<PilotesAnciens.Pilote> l)
        {
            var P = Habillage.P;
            // les versions retirables d'abord, puis les versions gardées des mêmes pilotes (pour comparer)
            var familles = new HashSet<string>(l.Where(p => p.Supprimable).Select(p => p.Original), StringComparer.OrdinalIgnoreCase);
            var liste = l.Where(p => p.Supprimable || (familles.Contains(p.Original) && p.PlusRecent)).OrderByDescending(p => p.Supprimable).ThenByDescending(p => p.Taille).ToList();
            coches.Clear();
            foreach (var p in liste.Where(p => p.Supprimable)) coches.Add(p);
            tableau.Remplir(liste.Cast<object>(), o =>
            {
                var p = (PilotesAnciens.Pilote)o;
                string etat = p.Supprimable ? "Ancienne version, inutilisée" : p.Utilise ? "Utilisée : gardée" : "Plus récente : gardée";
                return new[] { p.Fournisseur, p.Classe, p.Version, p.DateTexte, Format.Taille(p.Taille), Langue.T(etat) };
            }, o => coches.Contains((PilotesAnciens.Pilote)o), o => !((PilotesAnciens.Pilote)o).Supprimable);
            if (!l.Any(p => p.Supprimable))
            {
                info.Text = Langue.T("✔ Aucune ancienne version de pilote à retirer : le magasin de pilotes est propre.");
                info.Foreground = Habillage.Pinceau(P.Vert); bRetirer.IsEnabled = false;
                return;
            }
            Compter();
        }

        private void Compter()
        {
            info.Text = Langue.F("{0} ancienne(s) version(s) cochée(s) : {1} à récupérer.", coches.Count, Format.Taille(coches.Sum(p => p.Taille)));
            info.Foreground = Habillage.Pinceau(Habillage.P.Discret);
            bRetirer.IsEnabled = coches.Count > 0 && !F.Moteur.EnCours;
        }

        private void Retirer()
        {
            var c = coches.Where(p => p.Supprimable).ToList();
            if (c.Count == 0) return;
            var args = new List<string>();
            foreach (var p in c) { args.Add("--pilote"); args.Add(p.Inf); }
            string conf = "Retirer " + c.Count + " ancienne(s) version(s) de pilotes (" + Format.Taille(c.Sum(p => p.Taille)) + ") ?\n\nLa version la plus récente de chaque pilote est gardée, et Windows refuse de retirer un pilote encore utilisé. Un point de restauration n'est pas nécessaire : ces versions ne servent plus.";
            fenetre.Fermer();
            F.LancerAction("pilotes", args, conf);
        }
    }
}
