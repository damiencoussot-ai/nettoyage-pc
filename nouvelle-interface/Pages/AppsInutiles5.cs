// Nettoyage PC 5 - applications préinstallées inutiles : celles conseillées sont cochées, tu gardes celles dont tu te sers.
// Chacune se réinstalle depuis le Microsoft Store (et depuis l'Historique). Question avant ; en simulation, rien n'est retiré.
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
    internal sealed class AppsInutiles5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly Tableau tableau;
        private readonly TextBlock info;
        private readonly BoutonNoyau bRetirer;
        private readonly HashSet<Tuple<Paquet, AppInutile>> coches = new HashSet<Tuple<Paquet, AppInutile>>();
        private List<Tuple<Paquet, AppInutile>> liste = new List<Tuple<Paquet, AppInutile>>();
        private bool travail;

        public static void Ouvrir(FenetrePrincipale f) { f.Ouvrir("Applications préinstallées inutiles", new AppsInutiles5(f), 880); }

        private static string Nom(Tuple<Paquet, AppInutile> x) { return x.Item2.Motif.Contains("*") && x.Item1.NomAffiche != x.Item1.Nom ? x.Item1.NomAffiche : x.Item2.Libelle; }

        private AppsInutiles5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            Children.Add(new TextBlock { Text = Langue.T("Applications livrées avec Windows ou par le fabricant, souvent publicitaires. Celles conseillées sont cochées ; garde celles dont tu te sers. Chacune se réinstalle en un clic depuis le Microsoft Store (et depuis l'Historique)."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            tableau = new Tableau(true, false, new Colonne("Application", "1.6*"), new Colonne("Pourquoi", "3*"), new Colonne("Conseil", "110"));
            tableau.HauteurListe = 360;
            tableau.CaseChangee = (o, v) => { if (travail) return false; var x = (Tuple<Paquet, AppInutile>)o; if (v) coches.Add(x); else coches.Remove(x); Compter(); return true; };
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            info = new TextBlock { Text = Langue.T("Recherche des applications installées…"), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap };
            Children.Add(info);
            bRetirer = new BoutonNoyau("Retirer les applications cochées", true, 0, 38) { TaillePolice = 14, IsEnabled = false, HorizontalAlignment = HorizontalAlignment.Left };
            bRetirer.Clic += Retirer;
            Children.Add(bRetirer);
            AttachedToVisualTree += (s, e) => Charger();
        }

        private void Charger()
        {
            Task.Run(() => AppsInutiles.Trouver()).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                var P = Habillage.P;
                liste = (t.Result ?? new List<Tuple<Paquet, AppInutile>>()).OrderByDescending(x => x.Item2.Conseille).ThenBy(x => x.Item2.Libelle).ToList();
                coches.Clear();
                foreach (var x in liste.Where(x => x.Item2.Conseille)) coches.Add(x);
                tableau.Remplir(liste.Cast<object>(), o =>
                {
                    var x = (Tuple<Paquet, AppInutile>)o;
                    return new[] { Nom(x), Langue.T(x.Item2.Description), Langue.T(x.Item2.Conseille ? "Retirer" : "À toi de voir") };
                }, o => coches.Contains((Tuple<Paquet, AppInutile>)o), null);
                if (liste.Count == 0) { info.Text = Langue.T("✔ Aucune application préinstallée inutile sur ce PC."); info.Foreground = Habillage.Pinceau(P.Vert); bRetirer.IsEnabled = false; return; }
                Compter();
            }));
        }

        private void Compter()
        {
            if (liste.Count == 0) { bRetirer.IsEnabled = false; return; }
            info.Text = Langue.F("{0} application(s) trouvée(s), {1} cochée(s).", liste.Count, coches.Count);
            info.Foreground = Habillage.Pinceau(Habillage.P.Discret);
            bRetirer.IsEnabled = coches.Count > 0 && !travail;
        }

        private void Retirer()
        {
            var choix = liste.Where(x => coches.Contains(x)).ToList();
            if (choix.Count == 0) return;
            string txt = string.Join("\n", choix.Select(c => "- " + Nom(c)));
            if (F.Simulation) { F.Demander("Mode simulation", Langue.T("Ces applications seraient retirées :") + "\n\n" + txt, "Compris", null, r => { }); return; }
            F.Demander("Retirer ces applications de ton compte ?", txt + "\n\n" + Langue.T("Tu pourras les réinstaller depuis le Microsoft Store (l'Historique t'y emmène)."), "Retirer", "Annuler", oui =>
            {
                if (!oui) return;
                travail = true; bRetirer.IsEnabled = false;
                Task.Run(() =>
                {
                    var echecs = new List<string>(); int ok = 0;
                    foreach (var c in choix)
                    {
                        string nom = Nom(c);
                        Dispatcher.UIThread.Post(() => { info.Text = Langue.F("Désinstallation de « {0} »…", nom); info.Foreground = Habillage.Pinceau(Habillage.P.Accent); });
                        string r; try { r = AppsInutiles.Retirer(c.Item1, nom); } catch (Exception ex) { r = ex.Message; }
                        if (r == "") ok++; else echecs.Add(nom + " : " + r);
                    }
                    return Tuple.Create(ok, echecs);
                }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    travail = false;
                    F.Demander(Langue.F("{0} application(s) retirée(s).", t.Result.Item1), t.Result.Item2.Count > 0 ? Langue.T("Non retirée(s) :") + "\n- " + string.Join("\n- ", t.Result.Item2) : null, "Compris", null, r => { }, t.Result.Item2.Count > 0);
                    Charger();
                }));
            }, true);
        }
    }
}
