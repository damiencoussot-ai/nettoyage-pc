// Nettoyage PC 5 - mises à jour : version installée, recherche sur GitHub (source officielle), nouveautés de chaque
// version. Dans l'aperçu, l'installation passe par la version 4 (c'est elle qui est installée sur le PC).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal static class RubriqueMisesAJour
    {
        public static Control Creer(FenetrePrincipale f)
        {
            return new Onglets(f, "MisesAJour", new[]
            {
                Tuple.Create<string, string, Func<Control>>("MisesAJour", "Nettoyage PC", () => new PageMisesAJour5(f)),
                Tuple.Create<string, string, Func<Control>>("Pilotes", "Windows et pilotes", () => Pilotes(f)),
            });
        }

        private static Control Pilotes(FenetrePrincipale f)
        {
            string gpu = "";
            try { var g = InfosPc.CarteGraphiquePrincipale(); if (g != null) gpu = g.Nom + (g.DatePilote.HasValue ? " — " + Langue.T("pilote du") + " " + g.DatePilote.Value.ToString("dd/MM/yyyy") : ""); } catch { }
            return Cartes.Page(f, null, new[]
            {
                CarteAction.Code("Pilote de la carte graphique", (gpu != "" ? gpu + ". " : "") + "L'outil officiel du fabricant (AMD, NVIDIA, Intel) installe le dernier pilote en toute sécurité.", "Mettre à jour le pilote", () => f.Bientot("Pilote de la carte graphique"), true),
                CarteAction.Code("Mises à jour Windows et pilotes", "Windows Update : mises à jour de sécurité, pilotes et mises à jour facultatives. Tu choisis ce qui est installé.", "Ouvrir Windows Update", () => Programmes.Ouvrir("ms-settings:windowsupdate")),
                CarteAction.A("Mettre à jour les applications", "Met à jour tes logiciels avec winget, l'outil officiel de Microsoft. Corrige aussi des failles de sécurité.", "Mettre à jour", "majapps"),
            }, "Pilotes");
        }
    }

    internal sealed class PageMisesAJour5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tEtat, tDetail;
        private readonly BoutonNoyau bVerifier;
        private readonly StackPanel notes = new StackPanel { Spacing = 14 };
        private static List<VersionPubliee> versions = new List<VersionPubliee>();
        private static string etat = "inconnu", erreur = "";
        private static DateTime? dateVerif;
        private bool enCours;

        public PageMisesAJour5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 16; Margin = new Thickness(28, 6, 28, 24);
            var carte = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var s = new StackPanel { Spacing = 6 };
            s.Children.Add(new TextBlock { Text = "Nettoyage PC 5 (aperçu)", FontFamily = Habillage.Affichage, FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            s.Children.Add(new TextBlock { Text = Langue.F("Moteur de la version {0}", Appli.Version), FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret) });
            tEtat = new TextBlock { FontFamily = Habillage.Affichage, FontSize = 16, FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
            tDetail = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            s.Children.Add(tEtat); s.Children.Add(tDetail);
            carte.Children.Add(s);
            var boutons = new StackPanel { Spacing = 8, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 0, 0, 0) };
            bVerifier = new BoutonNoyau("Rechercher les mises à jour", false, 230, 36) { TaillePolice = 13.5 };
            bVerifier.Clic += () => Verifier(true);
            var bGithub = new BoutonNoyau("Toutes les versions sur GitHub", false, 230, 32) { TaillePolice = 12.5 };
            bGithub.Clic += () => Programmes.Ouvrir("https://github.com/" + MisesAJour.Depot + "/releases");
            boutons.Children.Add(bVerifier); boutons.Children.Add(bGithub);
            Grid.SetColumn(boutons, 1); carte.Children.Add(boutons);
            Children.Add(new Border { Child = carte, Padding = new Thickness(22, 20), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1) });
            Children.Add(new TextBlock { Text = Langue.T("Nouveautés et historique des versions"), FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), Margin = new Thickness(0, 6, 0, 0) });
            Children.Add(new Border { Child = notes, Padding = new Thickness(22, 18), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1) });
            Afficher();
        }

        private void Verifier(bool manuel)
        {
            if (enCours) return;
            enCours = true; etat = "verification"; Afficher();
            Task.Run(() => { try { return MisesAJour.Verifier(); } catch (Exception ex) { erreur = ex.Message; return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false; dateVerif = DateTime.Now;
                var l = t.Result;
                if (l == null) etat = "erreur";
                else
                {
                    versions = l;
                    var dernier = l.FirstOrDefault();
                    etat = dernier != null && MisesAJour.Comparer(dernier.Version, Appli.Version) > 0 && dernier.UrlSetup != "" ? "disponible" : "ajour";
                }
                Afficher();
                if (manuel && etat == "erreur") F.Demander("Impossible de vérifier les mises à jour", Langue.T("Connexion Internet ?") + "\n\n" + erreur, "Compris", null, r => { }, true);
            }));
        }

        private void Afficher()
        {
            var P = Habillage.P;
            bVerifier.IsEnabled = !enCours;
            var dernier = versions.FirstOrDefault();
            switch (etat)
            {
                case "verification": tEtat.Text = Langue.T("Recherche des mises à jour…"); tEtat.Foreground = Habillage.Pinceau(P.Accent); break;
                case "disponible":
                    tEtat.Text = Langue.F("Nouvelle version disponible : {0}", dernier.Version); tEtat.Foreground = Habillage.Pinceau(P.Orange);
                    break;
                case "ajour": tEtat.Text = Langue.T("✔  Tu as la dernière version."); tEtat.Foreground = Habillage.Pinceau(P.Vert); break;
                case "erreur": tEtat.Text = Langue.T("Vérification impossible (pas de connexion Internet ?)"); tEtat.Foreground = Habillage.Pinceau(P.Rouge); break;
                default: tEtat.Text = Langue.T("Clique sur « Rechercher les mises à jour »."); tEtat.Foreground = Habillage.Pinceau(P.Discret); break;
            }
            string detail = dateVerif.HasValue ? Langue.T("Dernière vérification :") + " " + dateVerif.Value.ToString("dd/MM/yyyy HH:mm") + "   ·   " : "";
            detail += Langue.F("Source officielle : {0}", "github.com/" + MisesAJour.Depot);
            if (etat == "disponible") detail += "\n" + Langue.T("Dans cet aperçu, la mise à jour s'installe depuis la version 4 de Nettoyage PC (bandeau « Installer maintenant » en haut de sa fenêtre).");
            tDetail.Text = detail;

            notes.Children.Clear();
            if (versions.Count > 0)
                foreach (var v in versions.Take(10))
                {
                    string titre = Langue.T("Version") + " " + v.Version + (v.Date.HasValue ? "   —   " + v.Date.Value.ToString("dd/MM/yyyy") : "");
                    int c = MisesAJour.Comparer(v.Version, Appli.Version);
                    var lignes = Regex.Split(v.Notes ?? "", "\r?\n").Select(n => n.Trim()).Where(n => n != "" && !Regex.IsMatch(n, @"^\s*SHA256") && !n.StartsWith("Nettoyage PC")).Select(n => n.TrimStart('-', '•', '*', ' ')).ToList();
                    var integre = JournalVersions.Liste.FirstOrDefault(x => x.Key == v.Version);
                    // les notes publiées sur GitHub sont en français : en anglais / espagnol, les nouveautés traduites passent avant
                    if (integre.Value != null && (lignes.Count == 0 || Langue.Code != "fr")) lignes = integre.Value.Select(Langue.T).ToList();
                    notes.Children.Add(Bloc(titre, c == 0 ? Langue.T("installée") : c > 0 ? Langue.T("nouvelle") : null, lignes));
                }
            else
                foreach (var kv in JournalVersions.Liste.Take(12))
                    notes.Children.Add(Bloc(Langue.T("Version") + " " + kv.Key, kv.Key == Appli.Version ? Langue.T("installée") : null, kv.Value.Select(Langue.T).ToList()));
        }

        private static Control Bloc(string titre, string pastille, List<string> lignes)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 5 };
            var t = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            t.Children.Add(new TextBlock { Text = titre, FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            if (pastille != null)
                t.Children.Add(new Border
                {
                    Child = new TextBlock { Text = pastille.ToUpperInvariant(), FontFamily = Habillage.Console, FontSize = 10, Foreground = Habillage.Pinceau(P.SurAccent) },
                    Background = Habillage.Pinceau(P.Accent), CornerRadius = new CornerRadius(2), Padding = new Thickness(6, 2), VerticalAlignment = VerticalAlignment.Center,
                });
            s.Children.Add(t);
            if (lignes.Count == 0) lignes.Add(Langue.T("(pas de description)"));
            foreach (var l in lignes)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("18,*") };
                g.Children.Add(new TextBlock { Text = "✦", FontSize = 10, Foreground = Habillage.Pinceau(P.Accent), Margin = new Thickness(0, 4, 0, 0) });
                var tb = new TextBlock { Text = l, FontFamily = Habillage.Corps, FontSize = 13, LineHeight = 19, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(tb, 1); g.Children.Add(tb);
                s.Children.Add(g);
            }
            return s;
        }
    }
}
