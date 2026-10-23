// Nettoyage PC 5 - bouclier anti-arnaque : logiciels de prise en main à distance ouverts, conseils,
// et le bouton « On me demande de prendre la main sur mon PC ». Rien n'est fermé sans le demander.
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal sealed class Bouclier5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tEtat, tDetail;
        private readonly StackPanel liste = new StackPanel { Spacing = 8 };
        private List<AccesDistant> detectes = new List<AccesDistant>();
        /// <summary>(captures) liste fictive affichée à la place de la vraie détection.</summary>
        public static List<AccesDistant> Exemple;

        public static void Ouvrir(FenetrePrincipale f, bool urgence = false)
        {
            var b = new Bouclier5(f);
            var w = f.Ouvrir("Bouclier anti-arnaque", b, 760);
            w.Fermee += () => f.ActualiserBilan();
            if (urgence) b.Urgence();
        }

        private Bouclier5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 12;
            Children.Add(new TextBlock { Text = Langue.T("Les arnaques au « faux technicien » (Microsoft, banque, antivirus...) demandent presque toujours d'installer un logiciel qui permet de prendre la main sur le PC. Le bouclier les repère."), FontFamily = Habillage.Corps, FontSize = 13.5, LineHeight = 20, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap });
            tEtat = new TextBlock { FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 0) };
            tDetail = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13.5, LineHeight = 20, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tEtat); Children.Add(tDetail);
            Children.Add(liste);
            var urgence = new BoutonNoyau("On me demande de prendre la main sur mon PC", true, 420, 46) { TaillePolice = 15, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 8, 0, 0) };
            urgence.Clic += Urgence;
            Children.Add(urgence);
            Children.Add(new TextBlock { Text = Langue.T("Au téléphone, par message ou par une fenêtre qui fait peur ? Clique ici."), FontFamily = Habillage.Corps, FontSize = 12.5, Foreground = Habillage.Pinceau(P.Discret) });
            var reverifier = new BoutonNoyau("Revérifier", false, 120, 32) { TaillePolice = 13, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 6, 0, 0) };
            reverifier.Clic += Actualiser;
            Children.Add(reverifier);
            Actualiser();
        }

        private void Actualiser()
        {
            var P = Habillage.P;
            try { detectes = Exemple ?? Bouclier.Detecter(); } catch { detectes = new List<AccesDistant>(); }
            HashSet<string> confiance;
            try { confiance = new HashSet<string>(Bouclier.Confiance()); } catch { confiance = new HashSet<string>(); }
            var aSignaler = detectes.Where(a => !confiance.Contains(a.Nom)).ToList();
            if (detectes.Count == 0)
            {
                tEtat.Text = Langue.T("✔  Aucun logiciel de prise en main à distance n'est ouvert"); tEtat.Foreground = Habillage.Pinceau(P.Vert);
                tDetail.Text = Langue.T("Personne ne peut voir ni contrôler ton écran en ce moment.");
            }
            else if (aSignaler.Count == 0)
            {
                tEtat.Text = Langue.T("✔  Seuls des logiciels que tu as déclarés de confiance sont ouverts"); tEtat.Foreground = Habillage.Pinceau(P.Vert);
                tDetail.Text = Langue.T("Tu peux retirer la confiance à tout moment ci-dessous.");
            }
            else
            {
                tEtat.Text = aSignaler.Count == 1 ? Langue.F("⚠  {0} est ouvert", aSignaler[0].Nom) : Langue.F("⚠  {0} logiciels de prise en main à distance sont ouverts", aSignaler.Count);
                tEtat.Foreground = Habillage.Pinceau(P.Orange);
                tDetail.Text = Langue.T("Si c'est toi qui l'as ouvert pour une personne de confiance (un proche, ton informaticien), tout va bien. Sinon, ferme-le : quelqu'un pourrait voir ton écran ou prendre la main.");
            }
            liste.Children.Clear();
            foreach (var a in detectes)
            {
                var acc = a;
                bool conf = confiance.Contains(a.Nom);
                var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
                var s = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center };
                s.Children.Add(new TextBlock { Text = a.Nom, FontFamily = Habillage.Affichage, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) });
                string etat = a.Connexions > 0 ? Langue.T("ouvert et connecté à Internet") : Langue.T("ouvert");
                if (a.Microsoft) etat += " · " + Langue.T("outil de Microsoft, utilisé aussi par des arnaqueurs");
                if (conf) etat += " · " + Langue.T("de confiance");
                s.Children.Add(new TextBlock { Text = etat, FontFamily = Habillage.Corps, FontSize = 12.5, TextWrapping = TextWrapping.Wrap, Foreground = Habillage.Pinceau(conf ? P.Vert : a.Connexions > 0 ? P.Orange : P.Discret) });
                g.Children.Add(s);
                var boutons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
                var bF = new BoutonNoyau("Fermer", !conf, 100, 32) { TaillePolice = 13 };
                bF.Clic += () => FermerLogiciel(acc);
                var bC = new BoutonNoyau(conf ? "Retirer la confiance" : "Je lui fais confiance", false, 190, 32) { TaillePolice = 13 };
                ToolTip.SetTip(bC, Langue.T("Pour un logiciel que tu utilises toi-même (par exemple pour aider un proche) : le bouclier ne le signalera plus."));
                bC.Clic += () => { try { Bouclier.DefinirConfiance(acc.Nom, !conf); } catch { } Actualiser(); };
                boutons.Children.Add(bF); boutons.Children.Add(bC);
                Grid.SetColumn(boutons, 1); g.Children.Add(boutons);
                liste.Children.Add(new Border
                {
                    Child = g, Padding = new Thickness(16, 12), CornerRadius = new CornerRadius(3),
                    Background = Habillage.Pinceau(P.Carte), BorderBrush = Habillage.Pinceau(conf ? P.Bordure : Habillage.Alpha(P.Orange, 120)), BorderThickness = new Thickness(1),
                });
            }
        }

        private void FermerLogiciel(AccesDistant a)
        {
            F.Demander(Langue.F("Fermer {0} maintenant ?", a.Nom), Langue.T("Si quelqu'un était connecté à ton PC avec ce logiciel, la connexion sera coupée."), "Fermer", "Annuler", oui =>
            {
                if (!oui) return;
                bool ok = false;
                try { ok = Exemple == null && Bouclier.Fermer(a); } catch { }
                Actualiser();
                bool encore = false;
                try { encore = Exemple == null && Bouclier.Detecter().Any(x => x.Nom == a.Nom); } catch { }
                if (!ok || encore)
                    F.Demander(Langue.F("{0} n'a pas pu être fermé complètement", a.Nom), Langue.T("Il tourne comme un service de Windows. Si tu ne t'en sers pas, désinstalle-le : Accélérer > Applications, puis « Désinstaller »."), "Compris", null, r => { }, true);
            }, true);
        }

        /// <summary>Écran d'urgence : quoi faire tout de suite, et fermeture (avec confirmation) des logiciels ouverts.</summary>
        public void Urgence()
        {
            HashSet<string> confiance;
            try { confiance = new HashSet<string>(Bouclier.Confiance()); } catch { confiance = new HashSet<string>(); }
            var aFermer = detectes.Where(a => !confiance.Contains(a.Nom)).ToList();
            string conseils = Langue.T(
                "1.  Raccroche, ou ferme la conversation. Microsoft, ta banque, la police ou un antivirus ne t'appellent JAMAIS pour réparer ton PC.\n" +
                "2.  Ne donne aucun code, aucun mot de passe, aucun numéro de carte bancaire.\n" +
                "3.  N'installe rien et ne paie rien, même si on te fait peur (virus, compte bloqué, amende).\n" +
                "4.  Si une page Internet bloque l'écran avec une alarme : appuie sur Ctrl + Alt + Suppr, ouvre le Gestionnaire des tâches et ferme le navigateur. C'est une fausse alerte.\n" +
                "5.  Appelle un proche de confiance pour en parler. Si tu as déjà donné tes coordonnées bancaires, appelle tout de suite ta banque.");
            if (aFermer.Count > 0)
            {
                conseils += "\n\n" + Langue.F("Logiciel(s) ouvert(s) en ce moment : {0}. Les fermer maintenant ?", string.Join(", ", aFermer.Select(a => a.Nom)));
                F.Demander(Langue.T("STOP : c'est très probablement une arnaque."), conseils, "Fermer ces logiciels", "Ne rien fermer", oui =>
                {
                    if (!oui) return;
                    if (Exemple == null) foreach (var a in aFermer) { try { Bouclier.Fermer(a); } catch { } }
                    Actualiser();
                    bool reste = false;
                    try { reste = Exemple == null && Bouclier.ASignaler().Count > 0; } catch { }
                    F.Demander(reste ? "Certains logiciels n'ont pas pu être fermés" : "C'est fait",
                        Langue.T(reste ? "Ce sont des services de Windows : désinstalle-les depuis Accélérer > Applications si tu ne t'en sers pas." : "Les logiciels de prise en main à distance sont fermés. Personne ne voit plus ton écran."), "Compris", null, r => { }, reste);
                }, true);
            }
            else
                F.Demander(Langue.T("STOP : c'est très probablement une arnaque."), conseils + "\n\n" + Langue.T("Aucun logiciel de prise en main à distance n'est ouvert : personne ne voit ton écran en ce moment."), "Compris", null, r => { }, true);
        }
    }
}
