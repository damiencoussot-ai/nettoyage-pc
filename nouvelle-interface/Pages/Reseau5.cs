// Nettoyage PC 5 - réseau : test de la connexion (ping vers la box et Internet, stabilité, débits) et serveurs DNS
// (comparaison des temps de réponse, changement réversible avec l'autorisation de Windows).
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
    internal sealed class Reseau5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tConnexion, tDetail, tConseil, tDns;
        private readonly BoutonNoyau bTester, bComparer, bUtiliser, bAuto;
        private readonly Tuile tBox, tPing, tGigue, tDesc, tMont;
        private readonly Tableau tableau;
        private List<CarteReseau> cartes = new List<CarteReseau>();
        private bool test, comparaison;

        private sealed class Tuile : Border
        {
            private readonly TextBlock valeur, commentaire;
            public Tuile(string titre)
            {
                var P = Habillage.P;
                var s = new StackPanel { Spacing = 3 };
                s.Children.Add(new TextBlock { Text = Langue.T(titre).ToUpperInvariant(), FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(P.Discret) });
                valeur = new TextBlock { Text = "—", FontFamily = Habillage.Affichage, FontSize = 21, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) };
                commentaire = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextTrimming = TextTrimming.CharacterEllipsis };
                s.Children.Add(valeur); s.Children.Add(commentaire);
                Child = s; Padding = new Thickness(14, 10); Margin = new Thickness(0, 0, 10, 10); MinWidth = 150; CornerRadius = new CornerRadius(3);
                Background = Habillage.Pinceau(P.Carte); BorderBrush = Habillage.Pinceau(P.Bordure); BorderThickness = new Thickness(1);
            }
            public void Definir(string v, string c = "", Color? couleur = null) { valeur.Text = v; commentaire.Text = Langue.T(c); commentaire.Foreground = Habillage.Pinceau(couleur ?? Habillage.P.Discret); }
        }

        public Reseau5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12; Margin = new Thickness(0, 0, 12, 0);
            var carte = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var s = new StackPanel { Spacing = 4 };
            s.Children.Add(new TextBlock { Text = Langue.T("Ta connexion"), FontFamily = Habillage.Console, FontSize = 11, Foreground = Habillage.Pinceau(P.Discret) });
            tConnexion = new TextBlock { Text = Langue.T("Recherche de la connexion…"), FontFamily = Habillage.Affichage, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            tDetail = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            s.Children.Add(tConnexion); s.Children.Add(tDetail);
            carte.Children.Add(s);
            bTester = new BoutonNoyau("Tester ma connexion", true, 220, 40) { TaillePolice = 14.5, IsEnabled = false, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 0, 0, 0) };
            ToolTip.SetTip(bTester, Langue.T("Mesure le ping vers ta box et vers Internet, puis le débit (environ 10 secondes ; télécharge jusqu'à 200 Mo depuis Cloudflare : évite avec une connexion limitée)."));
            bTester.Clic += Tester;
            Grid.SetColumn(bTester, 1); carte.Children.Add(bTester);
            Children.Add(new Border { Child = carte, Padding = new Thickness(20, 16), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1) });
            var tuiles = new WrapPanel { Orientation = Orientation.Horizontal };
            tBox = new Tuile("Ping vers la box"); tPing = new Tuile("Ping Internet"); tGigue = new Tuile("Stabilité (gigue)"); tDesc = new Tuile("Téléchargement"); tMont = new Tuile("Envoi");
            foreach (var t in new[] { tBox, tPing, tGigue, tDesc, tMont }) tuiles.Children.Add(t);
            Children.Add(tuiles);
            tConseil = new TextBlock { Text = Langue.T("Clique sur « Tester ma connexion » pour mesurer ta connexion."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tConseil);
            Children.Add(new TextBlock { Text = Langue.T("Serveurs DNS (l'« annuaire » d'Internet)"), FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), Margin = new Thickness(0, 10, 0, 0) });
            tableau = new Tableau(false, false, new Colonne("Serveur", "1.6*"), new Colonne("Adresses", "1.5*", false, true),
                new Colonne("Temps de réponse", "130", true, true, o => ((ServeurDns)o).Temps < 0 ? double.MaxValue : ((ServeurDns)o).Temps), new Colonne("Particularité", "2.5*"));
            tableau.HauteurListe = 260;
            tableau.SelectionChangee += () => { var x = tableau.Selection as ServeurDns; bUtiliser.IsEnabled = x != null && x.Ip1 != ""; };
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            var boutons = new WrapPanel { Orientation = Orientation.Horizontal };
            bComparer = new BoutonNoyau("Comparer les serveurs", false, 0, 34) { TaillePolice = 13.5, Margin = new Thickness(0, 0, 10, 6) };
            bComparer.Clic += Comparer;
            bUtiliser = new BoutonNoyau("Utiliser le serveur choisi", true, 0, 34) { TaillePolice = 13.5, IsEnabled = false, Margin = new Thickness(0, 0, 10, 6) };
            ToolTip.SetTip(bUtiliser, Langue.T("Windows te demandera l'autorisation (bouclier) : clique sur « Oui ». Réversible depuis l'Historique ou avec « Revenir au DNS automatique »."));
            bUtiliser.Clic += Utiliser;
            bAuto = new BoutonNoyau("Revenir au DNS automatique", false, 0, 34) { TaillePolice = 13, IsEnabled = false, Margin = new Thickness(0, 0, 10, 6) };
            bAuto.Clic += RevenirAuto;
            boutons.Children.Add(bComparer); boutons.Children.Add(bUtiliser); boutons.Children.Add(bAuto);
            Children.Add(boutons);
            tDns = new TextBlock { Text = Langue.T("Un serveur DNS plus rapide rend l'ouverture des sites un peu plus vive (il ne change pas le débit). Clique sur « Comparer les serveurs » : ton PC mesure le temps de réponse de chacun."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tDns);
            AttachedToVisualTree += (o, e) => ChargerConnexion();
        }

        /// <summary>(captures) connexion et mesures d'exemple : rien n'est mesuré ni modifié.</summary>
        public static bool Exemple;

        private void ChargerConnexion()
        {
            if (Exemple) { AfficherExemple(); return; }
            Task.Run(() => { try { return Reseau.CartesActives(); } catch { return new List<CarteReseau>(); } })
                .ContinueWith(t => Dispatcher.UIThread.Post(() => AfficherConnexion(t.Result)));
        }

        private void AfficherConnexion(List<CarteReseau> l)
        {
            cartes = l ?? new List<CarteReseau>();
            var c = cartes.FirstOrDefault();
            if (c == null) { tConnexion.Text = Langue.T("Aucune connexion Internet"); tDetail.Text = Langue.T("Branche le câble réseau ou connecte-toi au Wi-Fi, puis reviens sur cette page."); bTester.IsEnabled = false; bComparer.IsEnabled = false; }
            else
            {
                bTester.IsEnabled = !test; bComparer.IsEnabled = !comparaison;
                string genre = Langue.T(c.Wifi ? "Wi-Fi" : "Câble (Ethernet)");
                // nom de la carte (« Ethernet 2 », nom du Wi-Fi...) seulement s'il apporte quelque chose
                tConnexion.Text = string.IsNullOrWhiteSpace(c.Nom) || genre.IndexOf(c.Nom.Trim(), StringComparison.OrdinalIgnoreCase) >= 0 ? genre : genre + " · " + c.Nom;
                tDetail.Text = c.Description + "\n" + Langue.T("Box (passerelle) :") + " " + c.Passerelle + "   ·   " + Langue.T("DNS :") + " " + (c.Dns.Count > 0 ? string.Join(", ", c.Dns) : Langue.T("aucun")) + " " + Langue.T(c.DnsAutomatique ? "(automatique)" : "(choisi manuellement)") +
                    (cartes.Count > 1 ? "\n" + Langue.T("Autres connexions actives :") + " " + string.Join(", ", cartes.Skip(1).Select(x => x.Nom)) : "");
            }
            AfficherServeurs(null);
        }

        private void AfficherExemple()
        {
            var c = new CarteReseau { Nom = "Ethernet", Description = "Realtek Gaming 2.5GbE Family Controller", Passerelle = "192.168.1.254", Index = 12, DnsAutomatique = true };
            c.Dns.Add("192.168.1.254");
            AfficherConnexion(new List<CarteReseau> { c });
            var box = new ResultatPing { Moyenne = 0.6, Gigue = 0.2 };
            var net = new ResultatPing { Moyenne = 11.4, Gigue = 1.3 };
            AfficherPing(box, net, c); AfficherDebit(tDesc, 912); AfficherDebit(tMont, 604);
            ConclureTest(box, net, 912, 604, c, null);
            var l = Reseau.ServeursConnus();
            l.Insert(0, new ServeurDns { Nom = "Ton fournisseur (actuel)", Ip1 = "", Description = "Serveur DNS utilisé aujourd'hui" });
            var temps = new[] { 24.0, 6.2, 9.8, 8.1, 14.5, 17.3, 21.0, 12.4, 19.6 };
            for (int i = 0; i < l.Count; i++) l[i].Temps = temps[i % temps.Length];
            Conclure(l, c);
        }

        private void Conseil(string t, Color c) { tConseil.Text = t; tConseil.Foreground = Habillage.Pinceau(c); }

        private void Tester()
        {
            var P = Habillage.P;
            if (test) return;
            var c = cartes.FirstOrDefault(); if (c == null) return;
            test = true; bTester.IsEnabled = false;
            foreach (var t in new[] { tBox, tPing, tGigue, tDesc, tMont }) t.Definir("…");
            Conseil(Langue.T("Mesure du ping…"), P.Accent);
            ResultatPing box = null, net = null;
            double desc = -1, mont = -1;
            string erreur = null;
            Task.Run(() =>
            {
                box = Reseau.Ping(c.Passerelle, "box");
                net = Reseau.Ping("1.1.1.1", "Internet");
                if (net.Moyenne < 0) net = Reseau.Ping("8.8.8.8", "Internet");
                Dispatcher.UIThread.Post(() => { AfficherPing(box, net, c); Conseil(Langue.T("Mesure du débit de téléchargement…"), P.Accent); });
                try { desc = Reseau.DebitDescendant(v => Dispatcher.UIThread.Post(() => tDesc.Definir(v.ToString("0") + " Mbit/s", "mesure…"))); } catch (Exception ex) { erreur = ex.Message; }
                Dispatcher.UIThread.Post(() => { AfficherDebit(tDesc, desc); Conseil(Langue.T("Mesure du débit d'envoi…"), P.Accent); });
                try { mont = Reseau.DebitMontant(); } catch (Exception ex) { if (erreur == null) erreur = ex.Message; }
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                test = false; bTester.IsEnabled = true;
                AfficherDebit(tDesc, desc); AfficherDebit(tMont, mont);
                if (box != null && net != null) ConclureTest(box, net, desc, mont, c, erreur);
            }));
        }

        private void AfficherPing(ResultatPing box, ResultatPing net, CarteReseau c)
        {
            var P = Habillage.P;
            if (box.Moyenne >= 0) tBox.Definir(box.Moyenne.ToString("0") + " ms", box.Perte > 0 ? Langue.F("{0} % de pertes", box.Perte) : box.Moyenne <= (c.Wifi ? 8 : 3) ? "Parfait" : "Un peu lent", box.Perte > 0 || box.Moyenne > (c.Wifi ? 8 : 3) ? P.Orange : P.Vert);
            else tBox.Definir("—", "La box ne répond pas au ping");
            if (net.Moyenne >= 0)
            {
                string q = net.Moyenne < 25 ? "Excellent pour jouer" : net.Moyenne < 50 ? "Bon" : net.Moyenne < 100 ? "Moyen" : "Élevé";
                tPing.Definir(net.Moyenne.ToString("0") + " ms", Langue.T(q) + (net.Perte > 0 ? " · " + Langue.F("{0} % perdus", net.Perte) : ""), net.Moyenne < 50 && net.Perte == 0 ? P.Vert : P.Orange);
                tGigue.Definir(net.Gigue.ToString("0") + " ms", net.Gigue < 5 ? "Très stable" : net.Gigue < 15 ? "Stable" : "Instable", net.Gigue < 15 ? P.Vert : P.Orange);
            }
            else { tPing.Definir("—", "Internet ne répond pas", P.Rouge); tGigue.Definir("—"); }
        }

        private static void AfficherDebit(Tuile t, double v)
        {
            var P = Habillage.P;
            if (v < 0) { t.Definir("—", "mesure impossible"); return; }
            t.Definir(v >= 100 ? v.ToString("0") + " Mbit/s" : v.ToString("0.0") + " Mbit/s", v >= 100 ? "Très rapide" : v >= 30 ? "Rapide" : v >= 8 ? "Correct" : "Lent", v >= 8 ? P.Vert : P.Orange);
        }

        private void ConclureTest(ResultatPing box, ResultatPing net, double desc, double mont, CarteReseau c, string erreur)
        {
            var l = new List<string>();
            bool pb = false;
            if (net.Moyenne < 0) { l.Add("Internet ne répond pas : vérifie la box (voyants), puis redémarre-la si besoin."); pb = true; }
            if (c.Wifi && (box.Perte > 0 || box.Moyenne > 8 || box.Gigue > 10)) { l.Add("Ton Wi-Fi est un peu faible ou instable : rapproche le PC de la box, ou utilise un câble réseau (idéal pour jouer)."); pb = true; }
            else if (!c.Wifi && box.Moyenne > 3) { l.Add("La box répond lentement : redémarrer la box peut aider."); pb = true; }
            if (net.Moyenne >= 0 && net.Gigue >= 15) { l.Add("Connexion instable (gigue élevée) : un téléchargement en cours ou un autre appareil sature peut-être la ligne."); pb = true; }
            if (desc >= 0 && desc < 8) { l.Add("Le débit est faible : ferme les téléchargements en cours, ou compare avec le débit prévu par ton abonnement."); pb = true; }
            var lignes = l.Select(Langue.T).ToList();
            if (erreur != null && (desc < 0 || mont < 0)) lignes.Add("(" + Langue.T("Une partie du test de débit n'a pas pu se faire :") + " " + erreur + ")");
            if (!pb) lignes.Insert(0, Langue.T("✔ Ta connexion est en bonne forme."));
            Conseil(string.Join("\n", lignes), pb ? Habillage.P.Orange : Habillage.P.Vert);
        }

        private void AfficherServeurs(List<ServeurDns> mesures)
        {
            var c = cartes.FirstOrDefault();
            List<ServeurDns> liste;
            try { liste = mesures ?? Reseau.ServeursConnus(); } catch { liste = new List<ServeurDns>(); }
            tableau.Remplir(liste.OrderBy(x => x.Temps < 0 ? double.MaxValue : x.Temps).Cast<object>(), o =>
            {
                var s = (ServeurDns)o;
                bool actuel = c != null && c.Dns.Contains(s.Ip1);
                string nom = s.Ip1 == "" ? Langue.T(s.Nom) : s.Nom + (actuel ? "  " + Langue.T("(actuel)") : "");
                return new[] { nom, s.Ip1 == "" ? string.Join(", ", c == null ? new List<string>() : c.Dns) : s.Ip1 + ", " + s.Ip2, s.Temps >= 0 ? s.Temps.ToString("0") + " ms" : mesures == null ? "" : Langue.T("ne répond pas"), Langue.T(s.Description) };
            });
            bUtiliser.IsEnabled = false;
            bAuto.IsEnabled = c != null && !c.DnsAutomatique;
        }

        private void Comparer()
        {
            var P = Habillage.P;
            if (comparaison) return;
            var c = cartes.FirstOrDefault(); if (c == null) return;
            comparaison = true; bComparer.IsEnabled = false;
            tDns.Text = Langue.T("Mesure du temps de réponse de chaque serveur…"); tDns.Foreground = Habillage.Pinceau(P.Accent);
            Task.Run(() =>
            {
                var l = Reseau.ServeursConnus();
                bool connu = c.Dns.Any(d => l.Any(s => s.Ip1 == d || s.Ip2 == d));
                if (!connu && c.Dns.Count > 0) l.Insert(0, new ServeurDns { Nom = c.DnsAutomatique ? "Ton fournisseur (actuel)" : "Ton réglage actuel", Ip1 = "", Description = "Serveur DNS utilisé aujourd'hui", Temps = -1 });
                // deux passes : la première « réveille » les serveurs, la seconde est mesurée
                foreach (var s in l) { string ip = s.Ip1 == "" ? c.Dns[0] : s.Ip1; Reseau.TempsDns(ip); s.Temps = Reseau.TempsDns(ip); }
                return l;
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                comparaison = false; bComparer.IsEnabled = true;
                if (t.IsFaulted || t.Result == null) return;
                Conclure(t.Result, c);
            }));
        }

        private void Conclure(List<ServeurDns> l, CarteReseau c)
        {
            var P = Habillage.P;
            AfficherServeurs(l);
            var meilleur = l.Where(s => s.Temps >= 0).OrderBy(s => s.Temps).FirstOrDefault();
            var actuel = l.FirstOrDefault(s => s.Ip1 == "" || c.Dns.Contains(s.Ip1));
            string texte;
            if (meilleur == null) texte = Langue.T("Aucun serveur n'a répondu : la connexion bloque peut-être les serveurs DNS publics.");
            else if (actuel != null && actuel.Temps >= 0 && (meilleur == actuel || actuel.Temps - meilleur.Temps < 5)) texte = Langue.T("✔ Ton serveur DNS actuel est déjà parmi les plus rapides : inutile de changer.");
            else texte = (actuel != null && actuel.Temps >= 0 ? Langue.F("{0} est le plus rapide ({1} ms contre {2} ms aujourd'hui).", meilleur.Nom, meilleur.Temps.ToString("0"), actuel.Temps.ToString("0"))
                : Langue.F("{0} est le plus rapide ({1} ms).", meilleur.Nom, meilleur.Temps.ToString("0"))) + " " + Langue.T("Sélectionne-le puis « Utiliser le serveur choisi ».");
            tDns.Text = texte; tDns.Foreground = Habillage.Pinceau(texte.StartsWith("✔") ? P.Vert : P.Discret);
            if (meilleur != null && meilleur.Ip1 != "") tableau.Selectionner(meilleur);
        }

        private void Utiliser()
        {
            var s = tableau.Selection as ServeurDns;
            if (s == null || s.Ip1 == "") return;
            string msg = s.Nom + " (" + s.Ip1 + ", " + s.Ip2 + ")\n\n" + Langue.T(s.Description) + ".\n" + Langue.T("Réversible à tout moment (Historique, ou « Revenir au DNS automatique »).");
            if (F.Simulation) { F.Demander("Mode simulation : rien n'est modifié", msg, "Compris", null, r => { }); return; }
            F.Demander("Utiliser ces serveurs DNS ?", msg, "Utiliser", "Annuler", oui =>
            {
                if (!oui) return;
                F.Statut("Changement des serveurs DNS… (Windows demande l'autorisation)");
                Task.Run(() => { try { return Reseau.Appliquer(s); } catch (Exception ex) { return ex.Message; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    if (t.Result == "") F.Statut(Langue.F("Serveurs DNS de {0} activés.", s.Nom));
                    else F.Demander("Changement des serveurs DNS", t.Result, "Compris", null, r => { }, true);
                    ChargerConnexion();
                }));
            });
        }

        private void RevenirAuto()
        {
            var aChanger = cartes.Where(c => !c.DnsAutomatique && c.Index > 0).ToList();
            if (aChanger.Count == 0) return;
            F.Demander("Revenir aux serveurs DNS automatiques ?", "Ce sont ceux de ta box.", "Revenir", "Annuler", oui =>
            {
                if (!oui) return;
                Task.Run(() =>
                {
                    var erreurs = new List<string>();
                    foreach (var c in aChanger)
                    {
                        if (!Elevation.Commande(60, "dns", c.Index.ToString(), "auto")) { erreurs.Add(c.Nom); continue; }
                        var id = c.Id;
                        Historique.MarquerAnnulees(m => m.Type == "dns" && m.D("Carte") == id);
                    }
                    return erreurs;
                }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    if (t.Result.Count > 0) F.Demander("Non modifié", Langue.T("Non modifié pour :") + " " + string.Join(", ", t.Result) + " " + Langue.T("(autorisation refusée ?)"), "Compris", null, r => { }, true);
                    else F.Statut("Serveurs DNS automatiques rétablis.");
                    ChargerConnexion();
                }));
            });
        }
    }
}
