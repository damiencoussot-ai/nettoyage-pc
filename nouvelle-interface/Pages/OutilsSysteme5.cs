// Velox 5.11 - deux outils gratuits :
//  - Test du Wi-Fi : signal, bande, canal, réseaux voisins et conseils (lecture seule) ;
//  - Restes d'un programme désinstallé (onglet Applications) : proposés à la corbeille après la désinstallation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    // ================================================================ test du Wi-Fi
    internal sealed class Wifi5 : StackPanel
    {
        /// <summary>(captures) état d'exemple : rien n'est lu.</summary>
        public static EtatWifi Exemple;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var p = new Wifi5(f);
            var fen = f.Ouvrir("Test du Wi-Fi", p, 820);
            fen.Fermee += () => p.minuteur.Stop();
        }

        private readonly FenetrePrincipale F;
        private readonly TextBlock verdict, detail;
        private readonly StackPanel conseils = new StackPanel { Spacing = 8 };
        private readonly Border jauge = new Border { Height = 10, CornerRadius = new CornerRadius(5), HorizontalAlignment = HorizontalAlignment.Left };
        private readonly Grid piste = new Grid { Height = 10 };
        private readonly TextBlock pourcent;
        private readonly DispatcherTimer minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        private bool enCours;

        private Wifi5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(Premium5.Intro("La qualité de ta connexion Wi-Fi entre le PC et la box : force du signal, bande utilisée, canal et réseaux voisins qui gênent. Lecture seule : rien n'est modifié. Le signal est remesuré toutes les 3 secondes : déplace le PC ou l'antenne pour trouver le meilleur endroit."));
            verdict = new TextBlock { Text = Langue.T("Lecture du Wi-Fi…"), FontFamily = Habillage.Affichage, FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            Children.Add(verdict);
            var ligne = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            piste.Children.Add(new Border { Height = 10, CornerRadius = new CornerRadius(5), Background = Habillage.Pinceau(P.Bordure) });
            piste.Children.Add(jauge);
            ligne.Children.Add(piste);
            pourcent = new TextBlock { FontFamily = Habillage.Console, FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(P.Texte), Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(pourcent, 1); ligne.Children.Add(pourcent);
            Children.Add(ligne);
            detail = new TextBlock { FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(detail);
            Children.Add(Premium5.Titre("Conseils"));
            Children.Add(conseils);
            piste.SizeChanged += (s, e) => { if (dernier != null) Jauge(dernier); };
            minuteur.Tick += (s, e) => Lire();
            Lire();
            if (Exemple == null) minuteur.Start();
        }

        private EtatWifi dernier;

        private static bool CableBranche()
        {
            try
            {
                return NetworkInterface.GetAllNetworkInterfaces().Any(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType == NetworkInterfaceType.Ethernet
                    && n.GetIPProperties().GatewayAddresses.Any(g => g.Address != null && !g.Address.ToString().StartsWith("0.")));
            }
            catch { return false; }
        }

        private void Lire()
        {
            if (enCours) return;
            if (Exemple != null) { Afficher(Exemple, false); return; }
            enCours = true;
            Task.Run(() => Tuple.Create(Wifi.Lire(), CableBranche())).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                enCours = false;
                if (t.IsFaulted) return;
                Afficher(t.Result.Item1, t.Result.Item2);
            }));
        }

        private void Jauge(EtatWifi e)
        {
            var P = Habillage.P;
            double part = e.Connecte && e.Signal >= 0 ? e.Signal / 100.0 : 0;
            jauge.Width = Math.Max(0, piste.Bounds.Width * part);
            jauge.Background = Habillage.Pinceau(e.Signal >= 75 ? P.Vert : e.Signal >= 55 ? P.Accent : P.Orange);
        }

        private void Afficher(EtatWifi e, bool cable)
        {
            var P = Habillage.P;
            dernier = e;
            string v; int niveau;
            var liste = Wifi.Conseils(e, out v, out niveau);
            verdict.Text = (e.Connecte && e.Nom != "" ? "« " + e.Nom + " » : " : "") + Langue.T(v) + (cable ? " " + Langue.T("Ton PC est aussi branché en câble réseau : c'est lui que Windows utilise en priorité.") : "");
            verdict.Foreground = Habillage.Pinceau(!e.Connecte ? P.Texte : niveau == 0 ? P.Vert : niveau == 1 ? P.Texte : P.Orange);
            pourcent.Text = e.Connecte && e.Signal >= 0 ? e.Signal + " %" : "—";
            Jauge(e);
            var infos = new List<string>();
            if (e.Connecte)
            {
                if (e.Bande > 0) infos.Add(Langue.F("Bande {0} GHz", e.Bande.ToString(e.Bande == 2.4 ? "0.0" : "0", Langue.Culture)));
                if (e.Canal > 0) infos.Add(Langue.F("canal {0}", e.Canal));
                if (e.Norme != "") infos.Add(e.Norme);
                if (e.DebitMbps > 0) infos.Add(Langue.F("liaison {0} Mb/s", Math.Round(e.DebitMbps)));
                infos.Add(e.VoisinsLus ? Langue.F("{0} réseau(x) voisin(s) visible(s), dont {1} sur ton canal", e.Voisins.Select(x => x.Nom).Distinct().Count(), e.Genants) : Langue.T("réseaux voisins non lus (Windows demande l'autorisation « Localisation » pour les montrer)"));
            }
            else if (e.Disponible && e.Carte != "") infos.Add(e.Carte);
            detail.Text = string.Join("  ·  ", infos);
            conseils.Children.Clear();
            foreach (string c in liste)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("22,*") };
                g.Children.Add(new TextBlock { Text = "▸", FontSize = 13, Foreground = Habillage.Pinceau(P.Accent) });
                var t = new TextBlock { Text = Langue.T(c), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(t, 1); g.Children.Add(t);
                conseils.Children.Add(g);
            }
        }
    }

    // ================================================================ restes d'un programme désinstallé
    /// <summary>Après une désinstallation lancée depuis l'onglet Applications : attend qu'elle soit finie, puis montre les restes.</summary>
    internal sealed class Restes5 : StackPanel
    {
        /// <summary>(captures) restes d'exemple : rien n'est lu ni retiré.</summary>
        public static bool Exemple;

        /// <summary>Surveille la disparition du programme de la liste de Windows (20 minutes au plus), puis cherche ses restes.</summary>
        public static void Suivre(FenetrePrincipale f, ProgrammeInstalle p, List<ProgrammeInstalle> tous, Action fini)
        {
            string cle = (p.Cle ?? "") + "|" + (p.Nom ?? "");
            if (!suivis.Add(cle)) return;   // déjà surveillé (deux clics sur « Désinstaller »)
            var limite = DateTime.Now.AddMinutes(20);
            var minuteur = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            bool enCours = false;
            minuteur.Tick += (s, e) =>
            {
                if (enCours) return;
                enCours = true;
                bool expire = DateTime.Now > limite;
                Task.Run(() => Desinstalleur.EncoreInstalle(p)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    enCours = false;
                    bool encore = t.IsFaulted || t.Result;
                    if (encore && !expire) return;
                    minuteur.Stop();
                    suivis.Remove(cle);
                    if (encore) { if (fini != null) fini(); return; }
                    f.Statut(Langue.F("« {0} » est désinstallé. Recherche de ce qu'il a laissé…", p.Nom));
                    var autres = tous.Where(x => x != p).ToList();
                    Task.Run(() => Desinstalleur.ChercherRestes(p, autres)).ContinueWith(r => Dispatcher.UIThread.Post(() =>
                    {
                        if (fini != null) fini();
                        var restes = r.IsFaulted ? new List<Reste>() : r.Result;
                        if (restes.Count == 0) { f.Statut(Langue.F("« {0} » est désinstallé proprement : il n'a rien laissé.", p.Nom)); return; }
                        Ouvrir(f, p.Nom, restes);
                    }));
                }));
            };
            minuteur.Start();
        }

        private static readonly HashSet<string> suivis = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static void Ouvrir(FenetrePrincipale f, string nom, List<Reste> restes)
        {
            f.Ouvrir(Langue.F("Ce que « {0} » a laissé", nom), new Restes5(f, nom, restes), 860);
        }

        public static void OuvrirExemple(FenetrePrincipale f)
        {
            Exemple = true;
            Ouvrir(f, "LEQ Control Panel", new List<Reste>
            {
                new Reste { Chemin = @"C:\Users\Damien\AppData\Roaming\LEQ Control Panel", Genre = "Réglages et données du programme", Taille = 18.4 * 1048576 },
                new Reste { Chemin = @"C:\Users\Damien\Desktop\LEQ Control Panel.lnk", Genre = "Raccourci du Bureau", Taille = 1920 },
                new Reste { Chemin = @"C:\Program Files\LEQ Control Panel", Genre = "Dossier du programme", Taille = 212.0 * 1048576, Protege = true },
            });
        }

        private readonly FenetrePrincipale F;
        private readonly string nom;

        private Restes5(FenetrePrincipale f, string nom, List<Reste> restes)
        {
            F = f; this.nom = nom;
            Spacing = 8;
            Montrer(restes);
        }

        private void Montrer(List<Reste> restes)
        {
            var P = Habillage.P;
            Children.Clear();
            Children.Add(Premium5.Intro("Le programme est désinstallé, mais il a laissé ceci. Coche ce que tu veux retirer : tout part à la corbeille (récupérable), rien n'est supprimé définitivement."));
            if (restes.Count == 0) { Children.Add(Premium5.Intro("Il ne reste plus rien.")); return; }
            var cases = new List<Tuple<CheckBox, Reste>>();
            foreach (var r in restes)
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto") };
                var c = new CheckBox { IsChecked = !r.Protege && r.Genre.StartsWith("Raccourci"), IsEnabled = !r.Protege, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                g.Children.Add(c);
                var textes = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
                textes.Children.Add(new TextBlock { Text = Langue.T(r.Genre) + (r.Protege ? "  ·  " + Langue.T("dossier protégé : à retirer toi-même") : ""), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(r.Protege ? P.Orange : P.Texte) });
                textes.Children.Add(new TextBlock { Text = r.Chemin, FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(P.Discret), TextTrimming = TextTrimming.CharacterEllipsis });
                Grid.SetColumn(textes, 1); g.Children.Add(textes);
                var taille = new TextBlock { Text = Format.Taille(r.Taille), FontFamily = Habillage.Console, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Texte), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
                Grid.SetColumn(taille, 2); g.Children.Add(taille);
                var voir = new BoutonNoyau("Voir", false, 0, 30) { TaillePolice = 12 };
                var rr = r;
                voir.Clic += () => { if (!Exemple) { try { Programmes.MontrerDansExplorateur(rr.Chemin); } catch { } } };
                Grid.SetColumn(voir, 3); g.Children.Add(voir);
                Children.Add(new Border { Child = g, Padding = new Thickness(12, 8), CornerRadius = new CornerRadius(4), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1) });
                if (!r.Protege) cases.Add(Tuple.Create(c, r));
            }
            Children.Add(Premium5.Intro("Les dossiers de réglages peuvent contenir tes sauvegardes ou tes préférences : ne les coche que si tu ne comptes pas réinstaller ce programme."));
            var bRetirer = new BoutonNoyau("Mettre à la corbeille ce qui est coché", true, 0, 38) { TaillePolice = 13.5, HorizontalAlignment = HorizontalAlignment.Left };
            bRetirer.Clic += () =>
            {
                if (Exemple) return;
                var choisis = cases.Where(x => x.Item1.IsChecked == true).Select(x => x.Item2).ToList();
                if (choisis.Count == 0) { F.Statut(Langue.T("Rien n'est coché.")); return; }
                F.Demander(Langue.F("Mettre {0} élément(s) à la corbeille ?", choisis.Count), string.Join("\n", choisis.Select(x => x.Chemin)), "Mettre à la corbeille", "Annuler", oui =>
                {
                    if (!oui) return;
                    bRetirer.IsEnabled = false;
                    Task.Run(() => choisis.Where(Desinstalleur.Retirer).ToList()).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                    {
                        var faits = t.IsFaulted ? new List<Reste>() : t.Result;
                        if (faits.Count > 0) Historique.Ajouter("corbeille", Langue.F("Restes de « {0} » : {1} élément(s) mis à la corbeille", nom, faits.Count), new Dictionary<string, object> { { "Fichiers", faits.Select(x => x.Chemin).ToArray() } });
                        F.Statut(Langue.F("{0} élément(s) mis à la corbeille ({1}).", faits.Count, Format.Taille(faits.Sum(x => x.Taille))) + (faits.Count < choisis.Count ? " " + Langue.F("{0} refusé(s) par Windows.", choisis.Count - faits.Count) : ""), faits.Count < choisis.Count);
                        Montrer(restes.Where(x => !faits.Contains(x)).ToList());
                    }));
                }, true);
            };
            Children.Add(bRetirer);
        }
    }
}
