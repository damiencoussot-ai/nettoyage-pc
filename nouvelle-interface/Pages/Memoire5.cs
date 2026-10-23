// Nettoyage PC 5 - mémoire vive et BIOS : vitesse réelle des barrettes comparée à leur vitesse prévue (profil EXPO / XMP),
// guide du BIOS selon la carte mère, et redémarrage direct dans le BIOS (question avant ; rien en simulation).
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
    internal sealed class Memoire5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tTitre, tDetail, tCarteMere;
        private readonly BoutonNoyau bGuide, bBios;
        private readonly Tableau tableau;
        private InfosMateriel infos;

        /// <summary>(captures) matériel d'exemple : rien n'est lu sur le PC.</summary>
        public static InfosMateriel Exemple;

        public static void Ouvrir(FenetrePrincipale f) { f.Ouvrir("Mémoire vive et BIOS", new Memoire5(f), 900); }

        private Memoire5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            var carte = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var s = new StackPanel { Spacing = 5 };
            s.Children.Add(new TextBlock { Text = Langue.T("Mémoire vive").ToUpperInvariant(), FontFamily = Habillage.Console, FontSize = 10.5, Foreground = Habillage.Pinceau(P.Discret) });
            tTitre = new TextBlock { Text = Langue.T("Analyse des barrettes…"), FontFamily = Habillage.Affichage, FontSize = 21, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            tDetail = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13.5, LineHeight = 20, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            tCarteMere = new TextBlock { FontFamily = Habillage.Console, FontSize = 11.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
            s.Children.Add(tTitre); s.Children.Add(tDetail); s.Children.Add(tCarteMere);
            carte.Children.Add(s);
            var boutons = new StackPanel { Spacing = 8, Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Top };
            bGuide = new BoutonNoyau("Comment l'activer (guide)", true, 240, 38) { TaillePolice = 14, IsVisible = false };
            bGuide.Clic += Guide;
            bBios = new BoutonNoyau("Redémarrer dans le BIOS", false, 240, 34) { TaillePolice = 13.5, IsVisible = false };
            ToolTip.SetTip(bBios, Langue.T("Windows te demandera l'autorisation (bouclier) : clique sur « Oui ». Le PC redémarre 15 secondes après, directement dans les réglages du BIOS."));
            bBios.Clic += RedemarrerBios;
            boutons.Children.Add(bGuide); boutons.Children.Add(bBios);
            Grid.SetColumn(boutons, 1); carte.Children.Add(boutons);
            Children.Add(new Border { Child = carte, Padding = new Thickness(20, 16), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1) });
            tableau = new Tableau(false, false, new Colonne("Emplacement", "1.4*"), new Colonne("Capacité", "90", true, true, o => ((Barrette)o).Capacite), new Colonne("Type", "70"),
                new Colonne("Vitesse actuelle", "120", true, true, o => ((Barrette)o).VitesseConfiguree), new Colonne("Fabricant", "1.1*"), new Colonne("Référence", "1.8*", false, true));
            tableau.HauteurListe = 150;
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            Children.Add(new TextBlock { Text = Langue.T("La vitesse prévue est lue dans la référence des barrettes. Activer le profil du fabricant (EXPO chez AMD, XMP chez Intel) est un réglage prévu pour ça, sans risque pour le matériel."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            AttachedToVisualTree += (o, e) => Charger();
        }

        private void Charger()
        {
            if (Exemple != null) { Afficher(Exemple); return; }
            Task.Run(() => { try { return InfosMateriel.Lire(); } catch (Exception ex) { JournalDemarrage.Erreur("mémoire vive (version 5)", ex); return new InfosMateriel(); } })
                .ContinueWith(t => Dispatcher.UIThread.Post(() => Afficher(t.Result)));
        }

        private void Afficher(InfosMateriel i)
        {
            var P = Habillage.P;
            infos = i ?? new InfosMateriel();
            var b = infos.Barrettes;
            tableau.Remplir(b.Cast<object>(), o =>
            {
                var x = (Barrette)o;
                return new[] { x.Emplacement + (x.Banque != "" && !x.Emplacement.Contains(x.Banque) ? " (" + x.Banque + ")" : ""), Format.Taille(x.Capacite), x.Type,
                    x.VitesseConfiguree > 0 ? x.VitesseConfiguree + " MT/s" : "", x.Fabricant, x.Reference };
            });
            tableau.HauteurListe = Math.Min(190, 40 + Math.Max(1, b.Count) * 30);
            int niveau;
            tTitre.Text = Langue.T(infos.TitreMemoire());
            tDetail.Text = Langue.T(infos.ExplicationMemoire(out niveau));
            tDetail.Foreground = Habillage.Pinceau(niveau == 1 ? P.Orange : niveau == 2 ? P.Vert : P.Discret);
            tCarteMere.Text = b.Count == 0 ? "" : ((infos.CarteMere != "" ? Langue.T("Carte mère :") + " " + infos.CarteMere : "") + (infos.Bios != "" ? "   ·   BIOS " + infos.Bios : "")).Trim();
            tCarteMere.IsVisible = tCarteMere.Text != "";
            bGuide.IsVisible = b.Count > 0 && niveau != 2;
            bBios.IsVisible = b.Count > 0 && niveau == 1 && (Natif.Build >= 10240 || !Natif.EstWindows);
        }

        private void Guide()
        {
            if (infos == null) return;
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 12 };
            s.Children.Add(new SelectableTextBlock { Text = Langue.T(infos.GuideProfil()), FontFamily = Habillage.Corps, FontSize = 14, LineHeight = 22, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
            s.Children.Add(new TextBlock { Text = Langue.T("Astuce : prends cette fenêtre en photo avec ton téléphone pour garder les étapes sous les yeux dans le BIOS."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            if (bBios.IsVisible)
            {
                var b = new BoutonNoyau("Redémarrer dans le BIOS", true, 0, 38) { TaillePolice = 14, HorizontalAlignment = HorizontalAlignment.Left };
                b.Clic += RedemarrerBios;
                s.Children.Add(b);
            }
            F.Ouvrir(Langue.T("Activer") + " " + infos.Profil, s, 700);
        }

        private void RedemarrerBios()
        {
            F.Demander("Redémarrer le PC directement dans le BIOS ?", "Enregistre et ferme ton travail d'abord. Le PC redémarre 15 secondes après ta confirmation.\n\nUne fois dans le BIOS, suis le guide (garde-le sous les yeux sur ton téléphone, ou note les étapes).", "Redémarrer", "Annuler", oui =>
            {
                if (!oui) return;
                if (F.Simulation) { F.Demander("Mode simulation", "Le PC ne redémarre pas. Passe en mode réel (pastille en haut) pour redémarrer vraiment dans le BIOS.", "Compris", null, r => { }); return; }
                F.Statut("Autorisation de Windows demandée…");
                // hors du fil de l'interface : la commande attend la réponse de Windows (bouclier)
                Task.Run(() => { try { return Elevation.Commande(60, "redemarrer-bios"); } catch { return false; } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
                {
                    if (t.Result) F.Statut("Redémarrage vers le BIOS dans 15 secondes…", true);
                    else F.Demander("Redémarrage non lancé", "Le redémarrage vers le BIOS n'a pas été lancé (autorisation refusée, ou PC sans BIOS UEFI).\n\nRedémarre normalement et appuie plusieurs fois sur Suppr (ou F2) dès l'allumage.", "Compris", null, r => { }, true);
                }));
            }, true);
        }
    }
}
