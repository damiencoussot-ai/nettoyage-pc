// Nettoyage PC 5 - rubrique Outils : matériel, plein potentiel (réglages qui brident le matériel), réseau, aide à distance.
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    internal static class PageOutils
    {
        public static Control Creer(FenetrePrincipale f)
        {
            return new Onglets(f, "Outils", new[]
            {
                Tuple.Create<string, string, Func<Control>>("Materiel", "Matériel", () => Materiel(f)),
                Tuple.Create<string, string, Func<Control>>("Potentiel", "Plein potentiel", () => new PagePotentiel5(f)),
                Tuple.Create<string, string, Func<Control>>("Reseau", "Réseau", () => Reseau(f)),
                Tuple.Create<string, string, Func<Control>>("Aide", "Aide à distance", () => new PageAide5(f)),
            });
        }

        private static Control Materiel(FenetrePrincipale f)
        {
            return Cartes.Page(f, null, new[]
            {
                CarteAction.Code("Mémoire vive et BIOS", "Vitesse réelle des barrettes comparée à leur vitesse prévue (profil EXPO / XMP), avec le guide du BIOS de ta carte mère.", "Voir le détail", () => f.Bientot("Mémoire vive et BIOS"), true),
                CarteAction.A("Santé détaillée des disques", "Usure, température, erreurs et heures de fonctionnement de chaque disque, d'après leurs capteurs (SMART).", "Analyser", "santedisques"),
                CarteAction.A("Rapport du PC", "Espace disque, gros fichiers, doublons, dossiers lourds, programmes volumineux. Enregistré dans tes Documents.", "Générer le rapport", "rapport"),
                CarteAction.A("Compatibilité de ce PC", "Ce que ton matériel permet : Windows 11, TPM, Secure Boot, virtualisation.", "Vérifier", "compat").Plus(),
                CarteAction.A("Analyse des plantages", "Écrans bleus, redémarrages brutaux et applications qui se ferment : les causes probables, d'après les journaux de Windows.", "Analyser", "plantages").Plus(),
            }, "Materiel");
        }

        private static Control Reseau(FenetrePrincipale f)
        {
            return Cartes.Page(f, null, new[]
            {
                CarteAction.Code("Serveurs DNS", "L'« annuaire » d'Internet : compare la vitesse de ton fournisseur avec Cloudflare, Google et Quad9, et choisis le plus rapide. Réversible.", "Comparer", () => f.Bientot("Serveurs DNS"), true),
                CarteAction.Code("Paramètres réseau de Windows", "Wi-Fi, Ethernet, VPN, mode avion : les réglages de Windows.", "Ouvrir", () => Programmes.Ouvrir("ms-settings:network")),
            }, "Reseau");
        }
    }

    /// <summary>Plein potentiel : écrans, carte graphique, mémoire vive. Chaque point dit ce qu'on gagne et comment corriger.</summary>
    internal sealed class PagePotentiel5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tTitre, tDetail;
        private readonly StackPanel liste = new StackPanel { Spacing = 8 };
        private readonly BoutonNoyau bRevoir;
        private bool enCours;

        public PagePotentiel5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 14; Margin = new Thickness(0, 0, 12, 0);
            var entete = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            var s = new StackPanel { Spacing = 6 };
            tTitre = new TextBlock { Text = "Analyse du matériel…", FontFamily = Habillage.Affichage, FontSize = 20, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            tDetail = new TextBlock { Text = Langue.T("Écrans, carte graphique et mémoire vive : Nettoyage PC vérifie qu'ils donnent tout ce qu'ils peuvent. Rien n'est modifié."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            s.Children.Add(tTitre); s.Children.Add(tDetail);
            entete.Children.Add(s);
            bRevoir = new BoutonNoyau("Revérifier", false, 130, 34) { TaillePolice = 13, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(16, 0, 0, 0) };
            bRevoir.Clic += Charger;
            Grid.SetColumn(bRevoir, 1); entete.Children.Add(bRevoir);
            Children.Add(new Border { Child = entete, Padding = new Thickness(20, 18), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1) });
            Children.Add(liste);
            AttachedToVisualTree += (o, e) => Charger();
        }

        private void Charger()
        {
            if (enCours) return;
            enCours = true; bRevoir.IsEnabled = false;
            tTitre.Text = "Analyse du matériel…";
            Task.Run(() => { try { return Potentiel.Analyser(); } catch { return new List<PointPotentiel>(); } }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                var P = Habillage.P;
                enCours = false; bRevoir.IsEnabled = true;
                var points = t.Result ?? new List<PointPotentiel>();
                int n = points.Count(p => !p.Ok);
                tTitre.Text = points.Count == 0 ? Langue.T("Matériel non analysable sur ce PC") : n == 0 ? Langue.T("✔  Ton matériel tourne à plein régime") : Langue.F(n == 1 ? "{0} réglage bride ton matériel" : "{0} réglages brident ton matériel", n);
                tTitre.Foreground = Habillage.Pinceau(points.Count == 0 ? P.Discret : n == 0 ? P.Vert : P.Orange);
                tDetail.Text = Langue.T(n == 0 ? "Écrans, carte graphique et mémoire vive donnent tout ce qu'ils peuvent. Revérifie après avoir changé de matériel ou de câble."
                    : "Chaque point indique ce que tu gagnes et comment corriger. Ce sont des réglages prévus par les fabricants, sans risque pour le matériel.");
                Afficher(points);
            }));
        }

        private void Afficher(List<PointPotentiel> points)
        {
            var P = Habillage.P;
            liste.Children.Clear();
            foreach (var p in points.OrderBy(x => x.Ok))
            {
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("30,*,Auto") };
                g.Children.Add(new TextBlock { Text = p.Ok ? "✔" : "!", FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Habillage.Pinceau(p.Ok ? P.Vert : P.Orange), VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 1, 0, 0) });
                var s = new StackPanel { Spacing = 4 };
                var titre = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
                titre.Children.Add(new TextBlock { Text = Langue.T(p.Titre), FontFamily = p.Ok ? Habillage.Corps : Habillage.Affichage, FontSize = p.Ok ? 13.5 : 15, FontWeight = p.Ok ? FontWeight.Normal : FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                if (!p.Ok && p.Gain != "") titre.Children.Add(new TextBlock { Text = "→ " + Langue.T(p.Gain), FontFamily = Habillage.Console, FontSize = 12, Foreground = Habillage.Pinceau(P.Accent), VerticalAlignment = VerticalAlignment.Center });
                s.Children.Add(titre);
                if (!p.Ok)
                {
                    if (p.Detail != "") s.Children.Add(new TextBlock { Text = Langue.T(p.Detail), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
                    if (p.Correction != "") s.Children.Add(new TextBlock { Text = Langue.T(p.Correction), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap });
                }
                Grid.SetColumn(s, 1); g.Children.Add(s);
                if (!p.Ok && (p.Reglage != "" || p.Cle == "rebar" || p.Cle == "expo"))
                {
                    string uri = p.Reglage;
                    var b = new BoutonNoyau(uri != "" ? "Ouvrir le réglage" : "Onglet Matériel", uri != "", 170, 32) { TaillePolice = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
                    b.Clic += () => { if (uri != "") Programmes.Ouvrir(uri); else F.Executer("page:Materiel", "Matériel"); };
                    Grid.SetColumn(b, 2); g.Children.Add(b);
                }
                liste.Children.Add(new Border { Child = g, Padding = new Thickness(16, p.Ok ? 11 : 14), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(p.Ok ? P.Bordure : Habillage.Alpha(P.Orange, 110)), BorderThickness = new Thickness(1) });
            }
        }
    }

    /// <summary>Aide à distance : recevoir de l'aide d'un proche avec l'Assistance rapide de Microsoft, résumé du PC à copier.</summary>
    internal sealed class PageAide5 : StackPanel
    {
        private const string IdStoreAssistance = "9P7BP5VNWKX5";
        private readonly FenetrePrincipale F;

        public PageAide5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 14; Margin = new Thickness(0, 0, 12, 0);
            var c = new StackPanel { Spacing = 10 };
            c.Children.Add(new Border { Height = 2, Width = 36, HorizontalAlignment = HorizontalAlignment.Left, Background = Habillage.DegradeAccent() });
            c.Children.Add(new TextBlock { Text = Langue.T("Recevoir de l'aide d'un proche"), FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            c.Children.Add(new TextBlock
            {
                Text = Langue.T("1.  La personne qui t'aide ouvre l'Assistance rapide sur son PC et clique sur « Aider quelqu'un » : elle obtient un code de sécurité.\n" +
                    "2.  Elle te donne ce code (par téléphone ou par message).\n" +
                    "3.  Toi, clique sur « Ouvrir l'Assistance rapide », tape le code, puis accepte le partage de ton écran.\n\n" +
                    "Tu vois tout ce qu'elle fait, et tu peux arrêter à tout moment avec le bouton « Quitter »."),
                FontFamily = Habillage.Corps, FontSize = 13.5, LineHeight = 21, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap,
            });
            var b = new BoutonNoyau("Ouvrir l'Assistance rapide", true, 260, 42) { TaillePolice = 14.5, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
            b.Clic += OuvrirAssistance;
            c.Children.Add(b);
            c.Children.Add(new TextBlock { Text = Langue.T("⚠  N'accepte jamais une demande que tu n'as pas faite toi-même : Microsoft, ta banque ou un « technicien » ne t'appellent jamais pour prendre la main sur ton PC. Si on t'appelle pour ça, raccroche."), FontFamily = Habillage.Corps, FontSize = 13, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Orange), TextWrapping = TextWrapping.Wrap });
            Children.Add(new Border { Child = c, Padding = new Thickness(20, 18), CornerRadius = new CornerRadius(3), Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(P.Accent, 90), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 12, 0) });
            Children.Add(Cartes.Page(f, null, new[]
            {
                CarteAction.Code("Résumé de ce PC", "Windows, processeur, mémoire, disques, carte graphique et problèmes détectés : copié en un clic, à coller dans un message pour la personne qui t'aide.", "Copier le résumé", CopierResume, true),
                CarteAction.Code("Aider quelqu'un", "Pour dépanner un proche : ouvre l'Assistance rapide, clique sur « Aider quelqu'un » (compte Microsoft demandé), puis donne-lui le code.", "Ouvrir l'Assistance rapide", OuvrirAssistance),
                CarteAction.Code("Journal de démarrage", "Si Nettoyage PC se ferme ou affiche une erreur, ce fichier explique pourquoi : envoie-le à la personne qui t'aide.", "Montrer le fichier", () =>
                {
                    if (File.Exists(JournalDemarrage.Fichier)) Programmes.MontrerDansExplorateur(JournalDemarrage.Fichier);
                    else F.Demander("Journal de démarrage", "Le journal de démarrage est vide pour l'instant.", "Compris", null, r => { });
                }).Plus(),
                CarteAction.Code("Journaux des actions", "Le détail de chaque nettoyage et de chaque bilan, dans Documents\\NettoyagePC (gardés 60 jours).", "Ouvrir le dossier", () => { Chemins.CreerDonnees(); Programmes.Ouvrir(Chemins.Donnees); }).Plus(),
            }, "Aide"));
        }

        /// <summary>Assistance rapide : application du Microsoft Store, ou ancienne version intégrée ; sinon la page du Store (avec question).</summary>
        private void OuvrirAssistance() { Ouvrir(F); }

        public static void Ouvrir(FenetrePrincipale F)
        {
            try
            {
                var p = Paquets.Lister(true).FirstOrDefault(x => x.Nom.Equals("MicrosoftCorporationII.QuickAssist", StringComparison.OrdinalIgnoreCase) && x.AppId != "");
                if (p != null)
                {
                    Process.Start(new ProcessStartInfo(Path.Combine(Chemins.Windows, "explorer.exe"), "shell:AppsFolder\\" + p.Famille + "!" + p.AppId) { UseShellExecute = false });
                    F.Statut("L'Assistance rapide s'ouvre…");
                    return;
                }
                string ancien = Path.Combine(Chemins.Systeme32, "quickassist.exe");
                if (File.Exists(ancien)) { Programmes.Ouvrir(ancien); F.Statut("L'Assistance rapide s'ouvre…"); return; }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("assistance rapide (version 5)", ex); }
            F.Demander("L'Assistance rapide n'est pas installée", "C'est une application gratuite de Microsoft. L'installer depuis le Microsoft Store ?", "Ouvrir le Store", "Annuler", oui =>
            {
                if (oui) Programmes.Ouvrir("ms-windows-store://pdp/?ProductId=" + IdStoreAssistance);
            });
        }

        private void CopierResume()
        {
            F.Statut("Préparation du résumé du PC…");
            Task.Run(() => { try { return ResumeDuPc.Creer(); } catch { return null; } }).ContinueWith(t => Dispatcher.UIThread.Post(async () =>
            {
                string texte = t.Result;
                if (texte == null) { F.Statut("Résumé impossible.", true); return; }
                try { var top = TopLevel.GetTopLevel(this); if (top != null && top.Clipboard != null) await top.Clipboard.SetTextAsync(texte); } catch { }
                F.Statut("Résumé du PC copié : colle-le (Ctrl+V) dans un message.");
                F.Demander("Résumé copié", Langue.T("Colle-le (Ctrl+V) dans un e-mail ou un message pour la personne qui t'aide. Il ne contient ni mot de passe ni fichier personnel.") + "\n\n" + texte, "Compris", null, r => { });
            }));
        }
    }
}
