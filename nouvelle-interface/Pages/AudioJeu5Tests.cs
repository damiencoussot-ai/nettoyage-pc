// Velox - Studio son : tests d'écoute (direction des pas, audition des deux oreilles). Le calcul est dans
// src/Noyau/TestsEcoute.cs ; ici, l'écran et le déroulé. Les sons sont joués directement sur le casque (pas par le câble
// de « Pas boostés »), pour que le traitement en direct ne fausse pas le test.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed partial class AudioJeu5
    {
        private TextBlock tDir, tAud;
        private Grid grilleDir;
        private StackPanel zoneAudResultat;
        private Border bEntends;
        private CancellationTokenSource lectureTest;
        private readonly Random hasardTest = new Random();
        // direction
        private int[] ordreDir; private readonly List<int> repDir = new List<int>(); private int etapeDir = -1; private bool dirSeptUn;
        private float[] sonDir;
        // audition
        private List<Tuple<int, int>> essaisAud; private int etapeAud = -1; private double?[] seuilG, seuilD; private long debutBip; private bool repondu;

        private Control ConstruireTests()
        {
            var pile = new StackPanel { Spacing = 10 };
            // ---- direction
            pile.Children.Add(Studio.T(Langue.T("Test de direction : 8 pas joués autour de toi, chacun d'une direction différente. Clique d'où vient chaque pas. Le score montre si ton réglage place vraiment les ennemis (devant, derrière, sur les côtés)."), 12.5, Studio.Discret));
            var lancerDir = Studio.Bouton(Langue.T("Lancer le test de direction"), false, LancerDir);
            grilleDir = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), HorizontalAlignment = HorizontalAlignment.Left, IsVisible = false };
            int[,] places = { { 7, 0, 1 }, { 6, -1, 2 }, { 5, 4, 3 } };
            for (int l = 0; l < 3; l++)
                for (int c = 0; c < 3; c++)
                {
                    int d = places[l, c];
                    var b = d < 0 ? Puce(Langue.T("↻ Rejouer"), RejouerDir) : Puce(Langue.T(TestDirection.Noms[d]), () => RepondreDir(d));
                    b.HorizontalAlignment = HorizontalAlignment.Stretch;
                    if (b.Child is TextBlock tb) tb.HorizontalAlignment = HorizontalAlignment.Center;
                    Grid.SetRow(b, l); Grid.SetColumn(b, c);
                    grilleDir.Children.Add(b);
                }
            tDir = Studio.T("", 12.5, Studio.Texte);
            pile.Children.Add(new WrapPanel { Children = { lancerDir } });
            pile.Children.Add(grilleDir);
            pile.Children.Add(tDir);
            // ---- audition
            pile.Children.Add(Studio.T(Langue.T("Test d'audition : un bip très faible monte doucement dans une oreille ; clique « J'entends » dès que tu l'entends. 14 bips, environ 2 minutes, au calme. Velox compare tes deux oreilles et compense si l'une entend nettement moins bien à une fréquence."), 12.5, Studio.Discret));
            var lancerAud = Studio.Bouton(Langue.T("Lancer le test d'audition"), false, LancerAud);
            bEntends = Studio.Bouton(Langue.T("J'entends"), true, Entendu);
            bEntends.IsVisible = false;
            tAud = Studio.T("", 12.5, Studio.Texte);
            zoneAudResultat = new StackPanel { Spacing = 6 };
            pile.Children.Add(new WrapPanel { Children = { lancerAud, bEntends } });
            pile.Children.Add(tAud);
            pile.Children.Add(zoneAudResultat);
            AfficherCompensation();
            return pile;
        }

        // ------------------------------------------------------------------ lecture

        private void JouerTest(float[] son, int canaux, Action<long> debut, Action fin)
        {
            ArreterTest();
            if (!OperatingSystem.IsWindows()) return;
            var cts = new CancellationTokenSource(); lectureTest = cts;
            string sortie = MoteurSon.SortieCasque();
            var f = F;
            Task.Run(() => MoteurSon.JouerSur(sortie, son, canaux, DemoAudio.Frequence, cts.Token, debut)).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                if (err != "") f.Statut(Langue.T(err), true);
                if (!cts.IsCancellationRequested && fin != null) fin();
            }));
        }

        private void ArreterTest()
        {
            var c = lectureTest; lectureTest = null;
            if (c != null) { try { c.Cancel(); } catch { } }
        }

        // ------------------------------------------------------------------ direction

        private void LancerDir()
        {
            if (exemple) return;
            if (!OperatingSystem.IsWindows()) { F.Statut(Langue.T("Windows uniquement."), true); return; }
            ArreterAud(true);
            var v = Releve ?? new ReleveAudioJeu();
            dirSeptUn = v.HeSuVi != null && v.HeSuVi.Actif && v.HeSuVi.Inclus;
            ordreDir = TestDirection.Ordre(hasardTest); repDir.Clear(); etapeDir = 0;
            grilleDir.IsVisible = true;
            JouerDir();
        }

        private void JouerDir()
        {
            if (etapeDir < 0 || ordreDir == null || etapeDir >= ordreDir.Length) return;
            tDir.Text = Langue.F("Pas {0} sur 8 : d'où vient-il ?", etapeDir + 1);
            sonDir = TestDirection.Son(ordreDir[etapeDir], DemoAudio.Frequence, hasardTest.Next());
            JouerTest(sonDir, 8, null, null);
        }

        private void RejouerDir() { if (etapeDir >= 0 && sonDir != null) JouerTest(sonDir, 8, null, null); }

        private void RepondreDir(int d)
        {
            if (etapeDir < 0 || ordreDir == null) return;
            ArreterTest();
            repDir.Add(d); etapeDir++;
            if (etapeDir < ordreDir.Length) { JouerDir(); return; }
            etapeDir = -1; grilleDir.IsVisible = false;
            var res = TestDirection.Noter(ordreDir, repDir, dirSeptUn);
            tDir.Text = Langue.F("Score : {0} / 8", res.Score.ToString("0.#", Langue.Culture)) + " · " + Langue.F("{0} exacts", res.Exacts)
                + (res.DevantDerriere > 0 ? " · " + Langue.F("{0} confusions devant/derrière", res.DevantDerriere) : "")
                + "\n" + Langue.T(res.Conseil);
        }

        // ------------------------------------------------------------------ audition

        private void LancerAud()
        {
            if (exemple) return;
            if (!OperatingSystem.IsWindows()) { F.Statut(Langue.T("Windows uniquement."), true); return; }
            var v = Releve ?? new ReleveAudioJeu();
            if (v.HeSuVi != null && v.HeSuVi.Actif && v.HeSuVi.Inclus)
            {
                tAud.Text = Langue.T("Coupe d'abord « Son 7.1 virtuel » : il mélange les deux oreilles, le test serait faux. Rallume-le après.");
                return;
            }
            etapeDir = -1; grilleDir.IsVisible = false;
            int n = TestAudition.Frequences.Length;
            seuilG = new double?[n]; seuilD = new double?[n];
            essaisAud = new List<Tuple<int, int>>();
            for (int i = 0; i < n; i++) { essaisAud.Add(Tuple.Create(i, 0)); essaisAud.Add(Tuple.Create(i, 1)); }
            essaisAud = essaisAud.OrderBy(x => hasardTest.Next()).ToList();
            etapeAud = 0; zoneAudResultat.Children.Clear();
            Contourner(F, true);   // l'égaliseur (et une compensation déjà en place) est coupé pendant le test
            bEntends.IsVisible = true;
            JouerAud();
        }

        private void JouerAud()
        {
            if (etapeAud < 0 || essaisAud == null) return;
            if (etapeAud >= essaisAud.Count) { FinirAud(); return; }
            var e = essaisAud[etapeAud];
            tAud.Text = Langue.F("Bip {0} sur {1} : oreille {2}, {3} Hz. Clique « J'entends » dès que tu l'entends.", etapeAud + 1, essaisAud.Count,
                Langue.T(e.Item2 == 0 ? "gauche" : "droite"), TestAudition.Frequences[e.Item1].ToString("N0", Langue.Culture));
            repondu = false; debutBip = 0;
            JouerTest(TestAudition.Rampe(TestAudition.Frequences[e.Item1], e.Item2, DemoAudio.Frequence), 2, t => debutBip = t, () =>
            {
                // bip arrivé au bout sans clic : pas entendu (même au plus fort)
                if (etapeAud < 0 || repondu) return;
                Noter(null);
            });
        }

        private void Entendu()
        {
            if (etapeAud < 0 || essaisAud == null || repondu) return;
            long d = debutBip;
            double sec = d == 0 ? 0 : (Environment.TickCount64 - d) / 1000.0;
            ArreterTest();
            Noter(TestAudition.NiveauAuClic(sec));
        }

        private void Noter(double? niveau)
        {
            repondu = true;
            var e = essaisAud[etapeAud];
            if (e.Item2 == 0) seuilG[e.Item1] = niveau; else seuilD[e.Item1] = niveau;
            etapeAud++;
            var minuterie = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(800) };
            minuterie.Tick += (o, a) => { minuterie.Stop(); JouerAud(); };
            minuterie.Start();
        }

        private void ArreterAud(bool remettre)
        {
            if (etapeAud < 0) return;
            etapeAud = -1; ArreterTest(); bEntends.IsVisible = false;
            if (remettre) Contourner(F, false);
        }

        private void FinirAud()
        {
            ArreterAud(true);
            double[] cg, cd; TestAudition.Compensation(seuilG, seuilD, out cg, out cd);
            zoneAudResultat.Children.Clear();
            var c = Langue.Culture;
            for (int i = 0; i < TestAudition.Frequences.Length; i++)
            {
                Func<double?, string> s = x => x.HasValue ? x.Value.ToString("0", c) + " dB" : Langue.T("non entendu");
                string ligne = TestAudition.Frequences[i].ToString("N0", c) + " Hz · " + Langue.T("gauche") + " " + s(seuilG[i]) + " · " + Langue.T("droite") + " " + s(seuilD[i]);
                if (cg[i] < 0) ligne += " → " + Langue.F("oreille gauche baissée de {0} dB", (-cg[i]).ToString("0.#", c));
                if (cd[i] < 0) ligne += " → " + Langue.F("oreille droite baissée de {0} dB", (-cd[i]).ToString("0.#", c));
                zoneAudResultat.Children.Add(Studio.T(ligne, 12, Studio.Texte));
            }
            bool rien = cg.All(x => x == 0) && cd.All(x => x == 0);
            tAud.Text = rien ? Langue.T("Tes deux oreilles entendent pareil (moins de 10 dB d'écart partout) : aucune compensation n'est utile.")
                : Langue.T("Une oreille entend nettement moins bien à certaines fréquences : Velox peut baisser l'autre au même endroit, pour que les pas sonnent pareil des deux côtés.");
            if (!rien)
                zoneAudResultat.Children.Add(new WrapPanel { Children = { Studio.Bouton(Langue.T("Appliquer la compensation"), true, () => AppliquerOreilles(cg, cd)) } });
            AfficherCompensation();
        }

        private void AppliquerOreilles(double[] cg, double[] cd)
        {
            if (exemple) return;
            r.OreilleG = (double[])cg.Clone(); r.OreilleD = (double[])cd.Clone(); r.Borner();
            Changer(Langue.T(r.Oreilles ? "Compensation des oreilles appliquée." : "Compensation des oreilles retirée."), true, true);
            AfficherCompensation();
        }

        /// <summary>Rappel de la compensation en place, avec de quoi la retirer.</summary>
        private void AfficherCompensation()
        {
            if (zoneAudResultat == null || r == null) return;
            foreach (var x in zoneAudResultat.Children.OfType<Control>().Where(x => x.Tag as string == "compensation").ToList()) zoneAudResultat.Children.Remove(x);
            if (!r.Oreilles) return;
            var ligne = new WrapPanel { Tag = "compensation" };
            var t = Studio.T(Langue.T("Compensation des oreilles active."), 12, Studio.Vert);
            t.VerticalAlignment = VerticalAlignment.Center; t.Margin = new Thickness(0, 0, 10, 6);
            ligne.Children.Add(t);
            ligne.Children.Add(Puce(Langue.T("Retirer"), () => AppliquerOreilles(new double[TestAudition.Frequences.Length], new double[TestAudition.Frequences.Length])));
            zoneAudResultat.Children.Add(ligne);
        }
    }
}
