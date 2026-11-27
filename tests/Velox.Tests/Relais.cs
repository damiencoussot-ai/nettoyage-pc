// Relais : changer de réglage en jeu (force, ton arme, style, réglage propre au jeu) ne doit pas faire d'à-coup de volume.
using System;
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Relais
    {
        /// <summary>Fond de jeu chargé (souffle, ronflement) avec des pas réguliers : la scène où un moteur « à froid »
        /// remontait le fond d'un coup.</summary>
        private static Scene Fond()
        {
            var s = new Scene(4);
            s.AjouterSouffle(0.05, 7);
            s.AjouterSinus(110, 0.15);
            int g = 1;
            for (double t = 0.1; t < 3.9; t += 0.23) s.Ajouter(Scene.Pas(0.1, g++), t, 1, t % 0.46 < 0.23 ? 1 : 0.4);
            return s;
        }

        /// <summary>Joue la scène par paquets de 10 ms ; à « bascule » (en s), le réglage passe de « a » à « b » ; avec
        /// « froid », comme avant : moteur neuf branché tout de suite en fondu de 30 ms.</summary>
        private static float[] Jouer(Scene s, double bascule, Func<CompresseurPas> a, Func<CompresseurPas> b, bool froid, out double relaisA)
        {
            var t = s.Copie(); int bloc = Scene.Fe / 100, ib = (int)(bascule * Scene.Fe);
            var r = new RelaisReglage(2, Scene.Fe, froid ? 1e-9 : 0.5);
            r.Demarrer(a());
            relaisA = -1;
            var p = new float[bloc * 2];
            for (int deb = 0; deb < s.Images; deb += bloc)
            {
                if (deb == ib && b != null) r.Changer(b());
                Array.Copy(t, deb * 2, p, 0, bloc * 2);
                var avant = r.Actuel;
                r.Traiter(p, bloc);
                if (r.Actuel != avant && relaisA < 0) relaisA = (deb - ib) / (double)Scene.Fe;
                Array.Copy(p, 0, t, deb * 2, bloc * 2);
            }
            return t;
        }

        /// <summary>Plus grand écart (dB, fenêtres de 5 ms) entre deux sons sur [t0, t1].</summary>
        private static double Ecart(float[] x, float[] y, double t0, double t1)
        {
            double pire = 0; int w = Scene.Fe / 200;
            for (int d = (int)(t0 * Scene.Fe); d + w <= (int)(t1 * Scene.Fe); d += w)
            {
                double ex = 1e-12, ey = 1e-12;
                for (int i = d * 2; i < (d + w) * 2; i++) { ex += x[i] * x[i]; ey += y[i] * y[i]; }
                pire = Math.Max(pire, Math.Abs(10 * Math.Log10(ex / ey)));
            }
            return pire;
        }

        [Theory]
        [InlineData(65, 1, 1, 100, 1, 1)]   // Fort → Extrême
        [InlineData(100, 1, 1, 65, 1, 1)]   // Extrême → Fort
        [InlineData(65, 1, 1, 65, 2, 1)]    // ton arme 1 → 2
        [InlineData(65, 1, 0, 65, 1, 1)]    // Net → Puissant
        [InlineData(35, 0, 1, 130, 1, 1)]   // Léger sans arme → Ultra
        public void Changer_de_reglage_sans_a_coup(int fa, int aa, int sa, int fb, int ab, int sb)
        {
            var s = Fond();
            Func<CompresseurPas> A = () => new CompresseurPas(fa, Scene.Fe, aa, true, sa), B = () => new CompresseurPas(fb, Scene.Fe, ab, true, sb);
            double rel, relFroid;
            var chaud = Jouer(s, 0, B, null, false, out rel);          // le nouveau réglage, en route depuis le début
            var froid = Jouer(s, 2.0, A, B, true, out relFroid);
            var neuf = Jouer(s, 2.0, A, B, false, out rel);
            Assert.True(relFroid >= 0 && relFroid < 0.011, $"à froid, le relais devait être immédiat ({relFroid:F3} s)");
            Assert.True(rel > 0.05 && rel <= 0.35, $"relais {rel:F3} s après le changement (attendu : après la préparation, avant 0,35 s)");
            double debut = 2.0 + rel + 0.03;                          // fondu fini
            double eFroid = Ecart(froid, chaud, 2.0 + relFroid + 0.03, debut + 0.5);
            double eNeuf = Ecart(neuf, chaud, debut, debut + 0.5);
            Assert.True(eFroid > 4, $"la scène ne montre pas l'à-coup d'avant (écart à froid {eFroid:F1} dB)");
            Assert.True(eNeuf < 1, $"à-coup au changement de réglage : écart {eNeuf:F1} dB au réglage déjà en route (à froid : {eFroid:F1} dB)");
        }

        /// <summary>Sans changement de réglage, le relais rend exactement le son du moteur seul.</summary>
        [Fact]
        public void Sans_changement_meme_son()
        {
            var s = Fond(); double rel;
            var avec = Jouer(s, 0, () => new CompresseurPas(65, Scene.Fe, 1, true, 1), null, false, out rel);
            var seul = s.Copie(); var c = new CompresseurPas(65, Scene.Fe, 1, true, 1); int bloc = Scene.Fe / 100; var p = new float[bloc * 2];
            for (int deb = 0; deb < s.Images; deb += bloc) { Array.Copy(seul, deb * 2, p, 0, bloc * 2); c.Traiter(p, bloc, 2); Array.Copy(p, 0, seul, deb * 2, bloc * 2); }
            Assert.Equal(seul, avec);
        }

        /// <summary>Deux changements rapprochés (touches enchaînées) : c'est le dernier qui prend le relais, sans à-coup.</summary>
        [Fact]
        public void Changements_enchaines()
        {
            var s = Fond(); double rel;
            var chaud = Jouer(s, 0, () => new CompresseurPas(130, Scene.Fe, 1, true, 1), null, false, out rel);
            var t = s.Copie(); int bloc = Scene.Fe / 100; var p = new float[bloc * 2];
            var r = new RelaisReglage(2, Scene.Fe); r.Demarrer(new CompresseurPas(35, Scene.Fe, 1, true, 1));
            for (int deb = 0, k = 0; deb < s.Images; deb += bloc, k++)
            {
                if (k == 200) r.Changer(new CompresseurPas(65, Scene.Fe, 1, true, 1));
                if (k == 205) r.Changer(new CompresseurPas(100, Scene.Fe, 1, true, 1));
                if (k == 210) r.Changer(new CompresseurPas(130, Scene.Fe, 1, true, 1));
                Array.Copy(t, deb * 2, p, 0, bloc * 2); r.Traiter(p, bloc); Array.Copy(p, 0, t, deb * 2, bloc * 2);
            }
            Assert.False(r.EnCours);
            Assert.True(Ecart(t, chaud, 2.5, 3.9) < 1, "le dernier réglage demandé n'est pas celui en service");
        }

        /// <summary>Son à 8 canaux (7.1) : la préparation garde bien tous les canaux.</summary>
        [Fact]
        public void Son_7_1()
        {
            var s = new Scene(3, 8); s.AjouterSouffle(0.05, 3);
            for (double x = 0.1; x < 2.9; x += 0.2) s.Ajouter(Scene.Pas(0.1, (int)(x * 10)), x, 1, 1, 0.5, 0, 0.3, 0.3, 0.2, 0.2);
            int bloc = Scene.Fe / 100; var p = new float[bloc * 8];
            Func<CompresseurPas> B = () => new CompresseurPas(100, Scene.Fe, 1, true, 1);
            float[] Passe(bool changer)
            {
                var t = s.Copie(); var r = new RelaisReglage(8, Scene.Fe); r.Demarrer(changer ? new CompresseurPas(35, Scene.Fe, 1, true, 1) : B());
                for (int deb = 0, k = 0; deb < s.Images; deb += bloc, k++)
                {
                    if (changer && k == 150) r.Changer(B());
                    Array.Copy(t, deb * 8, p, 0, bloc * 8); r.Traiter(p, bloc); Array.Copy(p, 0, t, deb * 8, bloc * 8);
                }
                return t;
            }
            var a = Passe(false); var b = Passe(true);
            double pire = 0;
            for (int i = (int)(2.2 * Scene.Fe) * 8; i < a.Length; i++) pire = Math.Max(pire, Math.Abs(a[i] - b[i]));
            Assert.True(pire < 0.01, $"7.1 : écart {pire:F4} après le relais");
        }

        [Fact]
        public void Prechauffage_sans_erreur()
        {
            MoteurSon.Prechauffer();
        }
    }
}
