// Nettoyage PC - essai automatique de l'interface (--autotest <dossier>) : chaque page, les deux thèmes,
// les fenêtres d'outils ; une capture par écran et un compte rendu des erreurs. Utilisé avant chaque publication.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NettoyagePC
{
    internal static class Autotest
    {
        private static readonly List<string> rapport = new List<string>();
        private static int erreurs;

        public static void Lancer(FenetrePrincipale f)
        {
            premiereMesure = Programme.Chrono.ElapsedMilliseconds;
            string dossier = Programme.DossierAutotest;
            try { Directory.CreateDirectory(dossier); } catch { }
            var etapes = new List<Tuple<string, Action>>();
            foreach (var sombre in new[] { true, false })
            {
                bool s = sombre;
                etapes.Add(Tuple.Create("theme-" + (s ? "sombre" : "clair"), (Action)(() => f.AppliquerTheme(s))));
                foreach (var sc in f.Sections)
                    foreach (var pg in sc.Pages)
                    {
                        string cle = pg;
                        etapes.Add(Tuple.Create((s ? "sombre-" : "clair-") + cle, (Action)(() => f.AfficherPage(cle))));
                    }
            }
            Func<Form>[] fenetres =
            {
                () => new FenetreExtensions(f), () => new FenetreCarteEspace(f), () => new FenetreAppsInutiles(f), () => new FenetreAnciensPilotes(f),
                () => new FenetreAchat(), () => new FenetreBenchmark(f), () => new FenetreKit(), () => new FenetreAnalyseDemarrage(f), () => new FenetreGenerateurCles(null),
                () => new FenetreLegal("CONDITIONS-DE-VENTE"), () => new FenetrePlantages(f), () => new FenetreBatterie(), () => new FenetreGardien(),
                () => new FenetreBouclier(f), () => new FenetreEnqueteur(f), () => new FenetreSante(f), () => new FenetreEnergie(f), () => new FenetreFps(f), () => FenetreAssistant.PourAutotest(f), () => new FenetreProfil(f, true)
            };
            foreach (var fabrique in fenetres)
            {
                var fab = fabrique;
                Form ouverte = null;
                etapes.Add(Tuple.Create("fenetre", (Action)(() => { ouverte = fab(); ouverte.StartPosition = FormStartPosition.Manual; ouverte.Location = new Point(f.Left + 40, f.Top + 40); ouverte.Show(f); })));
                etapes.Add(Tuple.Create("fermeture", (Action)(() => { if (ouverte != null) { Capturer(ouverte, Path.Combine(dossier, "fenetre-" + ouverte.GetType().Name + ".png")); ouverte.Close(); ouverte.Dispose(); } })));
            }
            // Page Premium dans chaque état de licence (rien n'est enregistré), et une couleur Premium
            etapes.Add(Tuple.Create("premium-essai", (Action)(() => { Licence.SimulerPourAutotest("essai"); f.AfficherPage("Premium"); })));
            etapes.Add(Tuple.Create("premium-actif", (Action)(() => { Licence.SimulerPourAutotest("offerte"); f.ChangerAccent("emeraude"); f.AfficherPage("Premium"); })));
            etapes.Add(Tuple.Create("premium-bas", (Action)(() => { var pg = f.Pages["Premium"]; pg.AutoScrollPosition = new Point(0, 5000); })));
            etapes.Add(Tuple.Create("reglages-couleur", (Action)(() => f.AfficherPage("Reglages"))));
            etapes.Add(Tuple.Create("premium-retour", (Action)(() => { f.ChangerAccent("aurore"); Licence.SimulerPourAutotest(""); f.AfficherPage("Premium"); })));
            etapes.Add(Tuple.Create("options-ouvertes", (Action)(() => { f.AfficherPage("Securite"); foreach (var g in f.Pages["Securite"].Controls.OfType<Grille>()) g.DefinirOuvert(true); })));
            etapes.Add(Tuple.Create("options-fermees", (Action)(() => { foreach (var g in f.Pages["Securite"].Controls.OfType<Grille>()) g.DefinirOuvert(false); f.AfficherPage("Optimisation"); })));
            etapes.Add(Tuple.Create("bandeau-maj", (Action)(() => { ((PageMisesAJour)f.Pages["MisesAJour"]).SimulerPourAutotest(true); f.AfficherPage("Accueil"); })));
            etapes.Add(Tuple.Create("bandeau-maj-simple", (Action)(() => f.AppliquerModeSimple(true, false))));
            etapes.Add(Tuple.Create("bandeau-fin", (Action)(() => { f.AppliquerModeSimple(false, false); ((PageMisesAJour)f.Pages["MisesAJour"]).SimulerPourAutotest(false); })));
            etapes.Add(Tuple.Create("mode-simple", (Action)(() => f.AppliquerModeSimple(true))));
            etapes.Add(Tuple.Create("mode-simple-page", (Action)(() => f.AfficherPage("Materiel"))));
            etapes.Add(Tuple.Create("theme-fin", (Action)(() => { f.AppliquerModeSimple(false); f.AppliquerTheme(true); })));
            etapes.Add(Tuple.Create("accueil-defile", (Action)(() => { f.AfficherPage("Accueil"); ((PageAccueil)f.Pages["Accueil"]).VerifierEntetePourAutotest(); })));
            etapes.Add(Tuple.Create("mesures", (Action)(() => Mesurer(f))));
            etapes.Add(Tuple.Create("palette", (Action)(() => f.OuvrirPalette())));
            etapes.Add(Tuple.Create("fin-palette", (Action)(() => { foreach (var p in Application.OpenForms.OfType<PaletteCommandes>().ToList()) p.Close(); })));

            // (développement) NP_AUTOTEST_MESURES=1 : seulement les mesures de rapidité
            if (Environment.GetEnvironmentVariable("NP_AUTOTEST_MESURES") == "1") etapes = etapes.Where(x => x.Item1 == "mesures").ToList();
            int i = 0;
            var t = new Timer { Interval = 1800 };
            t.Tick += (s, e) =>
            {
                // capture de l'étape précédente, puis étape suivante
                if (i > 0)
                {
                    string nom = etapes[i - 1].Item1;
                    if (nom != "fenetre" && nom != "fermeture" && !nom.StartsWith("theme-") && nom != "fin-palette") Capturer(f, Path.Combine(dossier, nom + ".png"));
                }
                if (i >= etapes.Count)
                {
                    t.Stop();
                    rapport.Add("Fin : " + etapes.Count + " étapes, " + erreurs + " erreur(s).");
                    try { File.WriteAllLines(Path.Combine(dossier, "autotest.txt"), rapport); } catch { }
                    f.FermetureSansQuestion = true;
                    f.Close();
                    return;
                }
                var et = etapes[i++];
                try { et.Item2(); rapport.Add("OK : " + et.Item1); }
                catch (Exception ex) { erreurs++; rapport.Add("ERREUR : " + et.Item1 + " : " + ex); JournalDemarrage.Erreur("autotest " + et.Item1, ex); }
            };
            t.Start();
        }

        /// <summary>Temps d'affichage de chaque page (changement + dessin complet) et du défilement, pour comparer les versions.</summary>
        private static void Mesurer(FenetrePrincipale f)
        {
            rapport.Add("Ouverture de la fenêtre : " + premiereMesure + " ms");
            long total = 0;
            foreach (var cle in f.Pages.Keys.Where(k => k != "Simple").ToList())
            {
                long meilleur = long.MaxValue;
                for (int n = 0; n < 3; n++)
                {
                    f.AfficherPage("Accueil"); f.Refresh();
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    f.AfficherPage(cle); f.Refresh();
                    meilleur = Math.Min(meilleur, sw.ElapsedMilliseconds);
                }
                total += meilleur;
                var p = f.Pages[cle];
                string defil = "";
                if (p.VerticalScroll.Visible)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    for (int k = 1; k <= 8; k++) { p.AutoScrollPosition = new Point(0, k * 60); p.Refresh(); }
                    defil = ", défilement " + sw.ElapsedMilliseconds / 8 + " ms par pas";
                    p.AutoScrollPosition = new Point(0, 0);
                }
                rapport.Add("Mesure " + cle + " : " + meilleur + " ms" + defil);
                if (Environment.GetEnvironmentVariable("NP_AUTOTEST_DETAIL") == "1")
                    foreach (Control c in p.Controls)
                    {
                        var sw2 = System.Diagnostics.Stopwatch.StartNew(); c.Refresh();
                        rapport.Add("   " + c.GetType().Name + " " + c.Bounds + " : " + sw2.ElapsedMilliseconds + " ms");
                        if (c is Grille) foreach (Control x in c.Controls) rapport.Add("      " + x.GetType().Name + " " + x.Bounds + (x.Visible ? "" : " (cachée)"));
                    }
            }
            rapport.Add("Mesure totale des pages : " + total + " ms");
            f.AfficherPage("Accueil");
        }
        private static long premiereMesure;

        private static void Capturer(Form f, string fichier)
        {
            try
            {
                Application.DoEvents();
                using (var b = new Bitmap(Math.Max(1, f.Width), Math.Max(1, f.Height)))
                {
                    using (var g = Graphics.FromImage(b)) g.CopyFromScreen(f.Left, f.Top, 0, 0, b.Size);
                    b.Save(fichier, ImageFormat.Png);
                }
            }
            catch (Exception ex) { rapport.Add("(capture impossible : " + ex.Message + ")"); }
        }
    }
}
