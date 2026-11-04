// Velox 5 - « Signaler un problème » : la personne décrit ce qui s'est passé, voit le rapport complet (anonyme)
// avant tout envoi, puis choisit : l'envoyer par son e-mail (nettoyagepcai@gmail.com), le copier ou l'enregistrer.
// Rien ne part tout seul : c'est sa propre messagerie qui envoie, après qu'elle a cliqué « Envoyer ».
using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal static class Signaler5
    {
        public static void Ouvrir(FenetrePrincipale f)
        {
            var P = Habillage.P;
            var s = new StackPanel { Spacing = 12 };
            s.Children.Add(new TextBlock { Text = Langue.T("Le rapport contient la version de Velox, celle de Windows et les dernières erreurs du journal. Ton nom, celui du PC, tes adresses e-mail et tes dossiers personnels sont masqués. Tu le vois en entier avant de l'envoyer."), FontFamily = Habillage.Corps, FontSize = 13.5, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            s.Children.Add(new TextBlock { Text = Langue.T("Que s'est-il passé ? (facultatif)"), FontFamily = Habillage.Corps, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            var description = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 70, MaxLength = 1500, Watermark = Langue.T("Par exemple : la fenêtre se ferme quand je clique sur Nettoyage profond.") };
            s.Children.Add(description);
            s.Children.Add(new TextBlock { Text = Langue.T("Le rapport qui sera envoyé :"), FontFamily = Habillage.Corps, FontSize = 13.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
            var apercu = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 220, FontFamily = Habillage.Console, FontSize = 11.5 };
            Func<string> rapport = () => RapportBug.Creer(description.Text ?? "");
            apercu.Text = rapport();
            description.TextChanged += (o, e) => apercu.Text = rapport();
            s.Children.Add(apercu);
            var boutons = new WrapPanel { Orientation = Orientation.Horizontal };
            FenetreInterieure fen = null;
            var envoyer = new BoutonNoyau("Envoyer par e-mail", true, 0, 40) { TaillePolice = 14, Margin = new Thickness(0, 0, 10, 8) };
            envoyer.Clic += async () =>
            {
                string texte = apercu.Text ?? "";
                // un lien « mailto » ne peut pas porter un texte trop long : le rapport complet est aussi copié, à coller si besoin
                await Copier(f, texte);
                string corps = texte.Length > 1600 ? texte.Substring(0, 1600) + "\n…\n" + Langue.T("(rapport complet copié : colle-le ici avec Ctrl+V)") : texte;
                string lien = "mailto:" + RapportBug.Adresse + "?subject=" + Uri.EscapeDataString("Velox " + Appli.Version + " : problème") + "&body=" + Uri.EscapeDataString(corps);
                try { Programmes.Ouvrir(lien); f.Statut("Ta messagerie s'ouvre avec le rapport : vérifie-le, puis clique sur Envoyer."); }
                catch { f.Statut(Langue.F("Aucune messagerie n'est configurée : le rapport est copié, colle-le dans un e-mail à {0}.", RapportBug.Adresse), true); }
                if (fen != null) fen.Fermer();
            };
            var copier = new BoutonNoyau("Copier le rapport", false, 0, 40) { TaillePolice = 14, Margin = new Thickness(0, 0, 10, 8) };
            copier.Clic += async () => { await Copier(f, apercu.Text ?? ""); f.Statut(Langue.F("Rapport copié : colle-le dans un e-mail à {0}.", RapportBug.Adresse)); };
            var enregistrer = new BoutonNoyau("Enregistrer sur le Bureau", false, 0, 40) { TaillePolice = 14, Margin = new Thickness(0, 0, 10, 8) };
            enregistrer.Clic += () =>
            {
                try
                {
                    string fichier = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "NettoyagePC-rapport-" + DateTime.Now.ToString("yyyy-MM-dd-HHmm") + ".txt");
                    File.WriteAllText(fichier, apercu.Text ?? "", new System.Text.UTF8Encoding(true));
                    f.Statut(Langue.F("Rapport enregistré sur le Bureau : {0}", Path.GetFileName(fichier)));
                }
                catch (Exception ex) { f.Statut(Langue.T("Impossible d'enregistrer le rapport :") + " " + ex.Message, true); }
            };
            boutons.Children.Add(envoyer); boutons.Children.Add(copier); boutons.Children.Add(enregistrer);
            s.Children.Add(boutons);
            fen = f.Ouvrir("Signaler un problème", s, 820);
        }

        private static async System.Threading.Tasks.Task Copier(FenetrePrincipale f, string texte)
        {
            try { if (f.Clipboard != null) await f.Clipboard.SetTextAsync(texte); } catch { }
        }
    }
}
