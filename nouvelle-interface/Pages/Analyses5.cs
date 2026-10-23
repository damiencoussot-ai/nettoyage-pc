// Nettoyage PC 5 - analyse des plantages et analyse du démarrage (Premium) : le moteur lit les journaux de Windows
// (lecture seule) et envoie ses résultats, affichés ici avec l'explication de chaque ligne et quoi faire.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    /// <summary>Chiffre clé : grand nombre coloré et libellé.</summary>
    internal sealed class TuileChiffre5 : Border
    {
        private readonly TextBlock valeur, libelle;
        private readonly Border trait;
        public TuileChiffre5(string titre)
        {
            var P = Habillage.P;
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("3,*") };
            trait = new Border { Background = Habillage.Pinceau(P.Discret), Margin = new Thickness(0, 4), CornerRadius = new CornerRadius(1) };
            g.Children.Add(trait);
            var s = new StackPanel { Spacing = 2, Margin = new Thickness(12, 0, 0, 0) };
            valeur = new TextBlock { Text = "—", FontFamily = Habillage.Affichage, FontSize = 26, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Texte) };
            libelle = new TextBlock { Text = Langue.T(titre), FontFamily = Habillage.Corps, FontSize = 12, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            s.Children.Add(valeur); s.Children.Add(libelle);
            Grid.SetColumn(s, 1); g.Children.Add(s);
            Child = g; Padding = new Thickness(10, 10, 14, 10); CornerRadius = new CornerRadius(3); Margin = new Thickness(0, 0, 10, 0);
            Background = Habillage.Pinceau(P.Carte); BorderBrush = Habillage.Pinceau(P.Bordure); BorderThickness = new Thickness(1);
        }
        /// <summary>niveau : 0 normal, 1 orange, 2 rouge, 3 vert.</summary>
        public void Definir(string v, int niveau)
        {
            var P = Habillage.P;
            Color c = niveau == 2 ? P.Rouge : niveau == 1 ? P.Orange : niveau == 3 ? P.Vert : P.Texte;
            valeur.Text = v; valeur.Foreground = Habillage.Pinceau(c); trait.Background = Habillage.Pinceau(c);
        }
    }

    /// <summary>Histogramme empilé par semaine (13 semaines) : système, applications, matériel.</summary>
    internal sealed class GraphiqueSemaines5 : Control
    {
        public List<int[]> Semaines = new List<int[]>();

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height;
            Dessin.Cadre(ctx, new Rect(0, 0, w, h));
            Color[] couleurs = { P.Rouge, P.Orange, P.Accent2 };
            string[] noms = { "Système (écrans bleus, arrêts)", "Applications", "Matériel, disques, affichage" };
            double xl = 16;
            for (int i = 0; i < 3; i++)
            {
                ctx.DrawEllipse(Habillage.Pinceau(couleurs[i]), null, new Point(xl + 4.5, 16.5), 4.5, 4.5);
                var n = Dessin.Texte(Langue.T(noms[i]), 11.5, P.Discret);
                ctx.DrawText(n, new Point(xl + 13, 16.5 - n.Height / 2));
                xl += 13 + n.Width + 18;
            }
            double haut = 34, bas = h - 24;
            var g0 = Dessin.Texte(Langue.T("il y a 13 semaines"), 10.5, P.Discret);
            ctx.DrawText(g0, new Point(16, bas + 5));
            var g1 = Dessin.Texte(Langue.T("cette semaine"), 10.5, P.Discret);
            ctx.DrawText(g1, new Point(w - 16 - g1.Width, bas + 5));
            if (Semaines.Count == 0) return;
            int max = Math.Max(1, Semaines.Max(s => s.Sum()));
            double l = (w - 32) / Semaines.Count;
            for (int i = 0; i < Semaines.Count; i++)
            {
                double x = 16 + i * l + l * 0.18, lb = Math.Max(3, l * 0.64), y = bas;
                for (int k = 0; k < 3; k++)
                {
                    double hk = (bas - haut) * Semaines[i][k] / max;
                    if (hk < 0.5) continue;
                    ctx.DrawRectangle(Habillage.Pinceau(couleurs[k]), null, new RoundedRect(new Rect(x, y - hk, lb, hk), 2));
                    y -= hk;
                }
                if (Semaines[i].Sum() == 0) ctx.DrawRectangle(Habillage.Pinceau(Habillage.Alpha(P.Discret, 50)), null, new RoundedRect(new Rect(x, bas - 2, lb, 2), 1));
            }
        }
    }

    /// <summary>Barres empilées des derniers démarrages : jusqu'au bureau, puis programmes au démarrage.</summary>
    internal sealed class GraphiqueDemarrages5 : Control
    {
        public List<Tuple<string, long, long>> Valeurs = new List<Tuple<string, long, long>>();

        public override void Render(DrawingContext ctx)
        {
            var P = Habillage.P;
            double w = Bounds.Width, h = Bounds.Height;
            Dessin.Cadre(ctx, new Rect(0, 0, w, h));
            Color[] couleurs = { P.Accent, P.Orange };
            string[] noms = { "Jusqu'au bureau", "Programmes au démarrage" };
            double xl = 16;
            for (int i = 0; i < 2; i++)
            {
                ctx.DrawEllipse(Habillage.Pinceau(couleurs[i]), null, new Point(xl + 4.5, 16.5), 4.5, 4.5);
                var n = Dessin.Texte(Langue.T(noms[i]), 11.5, P.Discret);
                ctx.DrawText(n, new Point(xl + 13, 16.5 - n.Height / 2));
                xl += 13 + n.Width + 18;
            }
            var v = Valeurs.Skip(Math.Max(0, Valeurs.Count - 30)).ToList();
            if (v.Count == 0) { ctx.DrawText(Dessin.Texte(Langue.T("Aucun démarrage chronométré."), 13, P.Discret), new Point(16, h / 2 - 8)); return; }
            double haut = 34, bas = h - 24, gauche = 50;
            double max = Math.Max(1000, v.Max(x => x.Item2 + x.Item3));
            for (int i = 0; i <= 2; i++)
            {
                double val = max * i / 2, y = bas - (bas - haut) * i / 2;
                ctx.DrawLine(new Pen(Habillage.Pinceau(Habillage.Alpha(P.Discret, 45)), 1, new DashStyle(new double[] { 1, 3 }, 0)), new Point(gauche, y), new Point(w - 14, y));
                var t = Dessin.Texte(AnalyseDemarrage.Sec(val), 10, P.Discret, false, true);
                ctx.DrawText(t, new Point(gauche - 6 - t.Width, y - t.Height / 2));
            }
            double l = (w - gauche - 20) / v.Count;
            for (int i = 0; i < v.Count; i++)
            {
                double x = gauche + 4 + i * l + l * 0.15, lb = Math.Max(3, l * 0.7);
                double h1 = (bas - haut) * v[i].Item2 / max, h2 = (bas - haut) * v[i].Item3 / max;
                if (h1 >= 0.5) ctx.DrawRectangle(Habillage.Pinceau(couleurs[0]), null, new RoundedRect(new Rect(x, bas - h1, lb, h1), 1.5));
                if (h2 >= 0.5) ctx.DrawRectangle(Habillage.Pinceau(couleurs[1]), null, new RoundedRect(new Rect(x, bas - h1 - h2, lb, h2), 1.5));
            }
            var d0 = Dessin.Texte(v[0].Item1, 10, P.Discret, false, true);
            ctx.DrawText(d0, new Point(gauche, bas + 5));
            var d1 = Dessin.Texte(v[v.Count - 1].Item1, 10, P.Discret, false, true);
            ctx.DrawText(d1, new Point(w - 14 - d1.Width, bas + 5));
        }
    }

    internal sealed class Plantages5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tResume, tIndice, tDetail, tConseils, tTitreConseils;
        private readonly TuileChiffre5[] tuiles;
        private readonly GraphiqueSemaines5 graph = new GraphiqueSemaines5 { Height = 130 };
        private readonly Tableau tableau;
        private readonly Action<string, object> recevoir;

        /// <summary>(captures) résultats d'exemple : rien n'est lancé.</summary>
        public static Dictionary<string, object> Exemple;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var p = new Plantages5(f);
            f.Ouvrir("Analyse des plantages", p, 1020);
        }

        private Plantages5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            var tete = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            tResume = new TextBlock { Text = Langue.T("Lecture des journaux de Windows (90 derniers jours)…"), FontFamily = Habillage.Affichage, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap };
            tete.Children.Add(tResume);
            tIndice = new TextBlock { FontFamily = Habillage.Corps, FontSize = 12.5, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Discret), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
            Grid.SetColumn(tIndice, 1); tete.Children.Add(tIndice);
            Children.Add(tete);
            var g = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*,*") };
            tuiles = new[] { new TuileChiffre5("écrans bleus"), new TuileChiffre5("arrêts brutaux"), new TuileChiffre5("erreurs matérielles ou de disque"), new TuileChiffre5("applications qui ont planté") };
            for (int i = 0; i < 4; i++) { Grid.SetColumn(tuiles[i], i); g.Children.Add(tuiles[i]); }
            tuiles[3].Margin = new Thickness(0);
            Children.Add(g);
            Children.Add(graph);
            tableau = new Tableau(false, false, new Colonne("Quoi", "2.6*"), new Colonne("Type", "1.3*"), new Colonne("Fois", "60", true, true, o => ((LigneAnalyse)o).Nombre), new Colonne("Dernière fois", "130", false, true));
            tableau.HauteurListe = 220;
            tableau.SelectionChangee += () => { var l = tableau.Selection as LigneAnalyse; if (l != null) tDetail.Text = l.Detail; };
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            tDetail = new TextBlock { Text = Langue.T("Clique sur une ligne pour voir l'explication."), FontFamily = Habillage.Corps, FontSize = 13, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tDetail);
            tTitreConseils = Premium5.Titre("Que faire ?"); tTitreConseils.IsVisible = false;
            Children.Add(tTitreConseils);
            tConseils = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13, LineHeight = 20, Foreground = Habillage.Pinceau(P.Texte), TextWrapping = TextWrapping.Wrap };
            Children.Add(tConseils);
            var bas = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var bActualiser = new BoutonNoyau("Actualiser", false, 0, 34) { TaillePolice = 13 };
            bActualiser.Clic += Lancer;
            var bFiabilite = new BoutonNoyau("Moniteur de fiabilité de Windows", false, 0, 34) { TaillePolice = 13 };
            ToolTip.SetTip(bFiabilite, Langue.T("L'historique de fiabilité de Windows, jour par jour."));
            bFiabilite.Clic += () => Programmes.Ouvrir(Path.Combine(Chemins.Systeme32, "perfmon.exe"), "/rel");
            bas.Children.Add(bActualiser); bas.Children.Add(bFiabilite);
            Children.Add(bas);
            recevoir = (cle, v) => { if (cle == "plantages") Afficher(v as Dictionary<string, object>); };
            // abonné aux résultats du moteur tant que la fenêtre est affichée
            AttachedToVisualTree += (o, e) => { F.Moteur.Donnees += recevoir; if (Exemple != null) Afficher(Exemple); else Lancer(); };
            DetachedFromVisualTree += (o, e) => F.Moteur.Donnees -= recevoir;
        }

        private void Lancer()
        {
            if (Exemple != null) return;
            if (F.Moteur.EnCours) { tResume.Text = Langue.T("Analyse non lancée (une autre action est peut-être en cours)."); tResume.Foreground = Habillage.Pinceau(Habillage.P.Orange); return; }
            tResume.Text = Langue.T("Lecture des journaux de Windows (90 derniers jours)…"); tResume.Foreground = Habillage.Pinceau(Habillage.P.Accent);
            F.LancerAction("plantages");
        }

        private static Color Couleur(int n) { var P = Habillage.P; return n == 2 ? P.Rouge : n == 1 ? P.Orange : n == 3 ? P.Vert : P.Texte; }

        private void Afficher(Dictionary<string, object> d)
        {
            if (d == null) return;
            var P = Habillage.P;
            int[] niveaux;
            var chiffres = AnalysePlantages.Chiffres(d, out niveaux);
            for (int i = 0; i < 4; i++) tuiles[i].Definir(chiffres[i].ToString(), niveaux[i]);
            graph.Semaines = AnalysePlantages.Semaines(d); graph.InvalidateVisual();
            double indice = AnalysePlantages.Indice(d);
            tIndice.Text = indice >= 0 ? Langue.T("Indice de stabilité de Windows") + " : " + indice.ToString("0.0", Appli.Fr) + " / 10" : "";
            tIndice.Foreground = Habillage.Pinceau(indice >= 8 ? P.Vert : indice >= 5 ? P.Orange : P.Rouge);
            int niveau;
            tResume.Text = AnalysePlantages.Resume(d, out niveau); tResume.Foreground = Habillage.Pinceau(Couleur(niveau));
            var lignes = AnalysePlantages.Lignes(d);
            if (lignes.Count == 0) lignes.Add(new LigneAnalyse { Quoi = Langue.T("Rien à signaler sur les 90 derniers jours."), Niveau = 3 });
            tableau.Remplir(lignes.Cast<object>(), o =>
            {
                var l = (LigneAnalyse)o;
                return new[] { (l.Niveau == 2 ? "✖  " : l.Niveau == 1 ? "!  " : l.Niveau == 3 ? "✔  " : "") + l.Quoi, l.Type, l.Fois, l.Derniere };
            }, null, o => ((LigneAnalyse)o).Niveau == 0);
            var conseils = AnalysePlantages.Conseils(d);
            tConseils.Text = string.Join("\n", conseils); tTitreConseils.IsVisible = tConseils.IsVisible = conseils.Count > 0;
            tDetail.Text = Langue.T("Clique sur une ligne pour voir l'explication.");
        }
    }

    internal sealed class Demarrage5 : StackPanel
    {
        private readonly FenetrePrincipale F;
        private readonly TextBlock tResume, tConseils;
        private readonly GraphiqueDemarrages5 graph = new GraphiqueDemarrages5 { Height = 170 };
        private readonly Tableau tableau;
        private readonly Action<string, object> recevoir;
        private FenetreInterieure fenetre;

        /// <summary>(captures) résultats d'exemple : rien n'est lancé.</summary>
        public static Dictionary<string, object> Exemple;

        public static void Ouvrir(FenetrePrincipale f)
        {
            var p = new Demarrage5(f);
            p.fenetre = f.Ouvrir("Analyse du démarrage", p, 980);
        }

        private Demarrage5(FenetrePrincipale f)
        {
            F = f;
            var P = Habillage.P;
            Spacing = 10;
            tResume = new TextBlock { Text = Langue.T("Windows te demande l'autorisation pour lire le chronométrage des démarrages…"), FontFamily = Habillage.Affichage, FontSize = 16, FontWeight = FontWeight.SemiBold, Foreground = Habillage.Pinceau(P.Accent), TextWrapping = TextWrapping.Wrap };
            Children.Add(tResume);
            tConseils = new TextBlock { FontFamily = Habillage.Corps, FontSize = 13, LineHeight = 20, Foreground = Habillage.Pinceau(P.Discret), TextWrapping = TextWrapping.Wrap };
            Children.Add(tConseils);
            Children.Add(graph);
            tableau = new Tableau(false, false, new Colonne("Ce qui ralentit le démarrage", "2.6*"), new Colonne("Type", "1.1*"), new Colonne("Retard moyen", "110", true, true, o => ((LigneAnalyse)o).Valeur),
                new Colonne("Fois", "60", true, true, o => ((LigneAnalyse)o).Nombre), new Colonne("Dernière fois", "120", false, true));
            tableau.HauteurListe = 220;
            Children.Add(new Border { Child = tableau, BorderBrush = Habillage.Pinceau(P.Bordure), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(3), ClipToBounds = true });
            var bas = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var bDemarrage = new BoutonNoyau("Gérer les programmes au démarrage", false, 0, 34) { TaillePolice = 13 };
            bDemarrage.Clic += () => { if (fenetre != null) fenetre.Fermer(); F.Executer("page:Demarrage", "Démarrage"); };
            var bActualiser = new BoutonNoyau("Actualiser", false, 0, 34) { TaillePolice = 13 };
            bActualiser.Clic += Lancer;
            bas.Children.Add(bDemarrage); bas.Children.Add(bActualiser);
            Children.Add(bas);
            recevoir = (cle, v) => { if (cle == "demarrage") Afficher(v as Dictionary<string, object>); };
            AttachedToVisualTree += (o, e) => { F.Moteur.Donnees += recevoir; if (Exemple != null) Afficher(Exemple); else Lancer(); };
            DetachedFromVisualTree += (o, e) => F.Moteur.Donnees -= recevoir;
        }

        private void Lancer()
        {
            if (Exemple != null) return;
            if (F.Moteur.EnCours) { tResume.Text = Langue.T("Analyse non lancée (autorisation refusée ou action déjà en cours)."); tResume.Foreground = Habillage.Pinceau(Habillage.P.Orange); return; }
            F.LancerAction("demarrage");
        }

        private void Afficher(Dictionary<string, object> d)
        {
            if (d == null) return;
            var P = Habillage.P;
            int niveau;
            tResume.Text = Langue.T(AnalyseDemarrage.Resume(d, out niveau));
            tResume.Foreground = Habillage.Pinceau(niveau == -1 || niveau == 1 ? P.Orange : niveau == 2 ? P.Vert : P.Discret);
            if (niveau == -1) return;
            graph.Valeurs = AnalyseDemarrage.Demarrages(d); graph.InvalidateVisual();
            tableau.Remplir(AnalyseDemarrage.Lignes(d).Cast<object>(), o =>
            {
                var l = (LigneAnalyse)o;
                return new[] { (l.Niveau == 1 ? "!  " : "") + l.Quoi, Langue.T(l.Type), AnalyseDemarrage.Sec(l.Valeur), l.Fois, l.Derniere };
            });
            tConseils.Text = string.Join("\n", AnalyseDemarrage.Conseils(d).Select(Langue.T));
        }
    }
}
