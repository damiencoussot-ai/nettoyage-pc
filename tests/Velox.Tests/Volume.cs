// Le traitement ne doit ni « pomper » (le fond qui monte et descend à chaque pas ou tir) ni baisser le volume général.
using System;
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Volume
    {
        /// <summary>Un fond continu (sinusoïde à 400 Hz, suivie à part) avec des pas toutes les 250 ms : son niveau ne doit
        /// presque pas bouger. Ultra est connu pour pomper davantage (notes de version 5.15) : tolérance plus large.</summary>
        [Theory]
        [InlineData(CompresseurPas.StyleNet, 35, 1)] [InlineData(CompresseurPas.StyleNet, 65, 1)]
        [InlineData(CompresseurPas.StyleNet, 100, 1)] [InlineData(CompresseurPas.StyleNet, 130, 1)]
        [InlineData(CompresseurPas.StylePuissant, 35, 1)] [InlineData(CompresseurPas.StylePuissant, 65, 1.5)]
        [InlineData(CompresseurPas.StylePuissant, 100, 2.5)] [InlineData(CompresseurPas.StylePuissant, 130, 4.5)]
        public void Pas_de_pompage_sur_les_pas(int style, int force, double maxDb)
        {
            var s = new Scene(3); s.AjouterSinus(400, 0.03);
            for (double t = 0.5; t < 2.8; t += 0.25) s.Ajouter(Scene.Pas(0.03, (int)(t * 100)), t, 1, 0.7);
            var y = Mesure.Traiter(new CompresseurPas(force, Scene.Fe, 0, true, style), s.Son, 2);
            double mini = double.MaxValue, maxi = double.MinValue;
            for (double t = 0.5; t < 2.7; t += 0.02)
            {
                double v = Mesure.Sinus(y, 2, 400, t, t + 0.02);
                mini = Math.Min(mini, v); maxi = Math.Max(maxi, v);
            }
            Assert.True(maxi - mini <= maxDb, $"le fond varie de {maxi - mini:F2} dB");
        }

        /// <summary>Après un tir fort, le fond revient à son niveau en moins d'une demi-seconde.</summary>
        [Theory]
        [InlineData(CompresseurPas.StyleNet, 65)] [InlineData(CompresseurPas.StyleNet, 130)]
        [InlineData(CompresseurPas.StylePuissant, 65)] [InlineData(CompresseurPas.StylePuissant, 130)]
        public void Fond_revient_apres_un_tir(int style, int force)
        {
            var s = new Scene(3); s.AjouterSinus(400, 0.03);
            s.Ajouter(Scene.Tir(0.8, 5), 1.0, 1, 1);
            var y = Mesure.Traiter(new CompresseurPas(force, Scene.Fe, 0, true, style), s.Son, 2);
            double avant = Mesure.Sinus(y, 2, 400, 0.7, 0.95), apres = Mesure.Sinus(y, 2, 400, 1.5, 1.55);
            Assert.InRange(apres - avant, -1, 1);
        }

        /// <summary>Une scène de jeu ordinaire (ambiance, pas, un tir) n'est jamais plus faible après traitement.</summary>
        [Theory]
        [InlineData(CompresseurPas.StyleNet, 35)] [InlineData(CompresseurPas.StyleNet, 65)] [InlineData(CompresseurPas.StyleNet, 100)] [InlineData(CompresseurPas.StyleNet, 130)]
        [InlineData(CompresseurPas.StylePuissant, 35)] [InlineData(CompresseurPas.StylePuissant, 65)] [InlineData(CompresseurPas.StylePuissant, 100)] [InlineData(CompresseurPas.StylePuissant, 130)]
        public void Volume_general_pas_baisse(int style, int force)
        {
            var s = new Scene(3); s.AjouterSouffle(0.01, 8); s.AjouterSinus(120, 0.05);
            for (double t = 0.3; t < 2.8; t += 0.4) s.Ajouter(Scene.Pas(0.03, (int)(t * 10)), t, 1, 0.5);
            s.Ajouter(Scene.Tir(0.5, 1), 1.5, 1, 1);
            var y = Mesure.Traiter(new CompresseurPas(force, Scene.Fe, 1, true, style), s.Son, 2);
            double ecart = Mesure.Niveau(y, 2, 0.3, 2.9) - Mesure.Niveau(s.Son, 2, 0.3, 2.9);
            Assert.True(ecart >= -0.5, $"volume général {ecart:F2} dB");
        }

        /// <summary>Une ambiance calme seule (pas de son fort) n'est pas baissée, quel que soit le réglage.</summary>
        [Theory]
        [InlineData(CompresseurPas.StyleNet, 35)] [InlineData(CompresseurPas.StyleNet, 130)]
        [InlineData(CompresseurPas.StylePuissant, 35)] [InlineData(CompresseurPas.StylePuissant, 130)]
        public void Ambiance_calme_pas_baissee(int style, int force)
        {
            var s = new Scene(2); s.AjouterSouffle(0.02, 9);
            var y = Mesure.Traiter(new CompresseurPas(force, Scene.Fe, 1, true, style), s.Son, 2);
            Assert.True(Mesure.Niveau(y, 2, 0.5, 1.9) >= Mesure.Niveau(s.Son, 2, 0.5, 1.9) - 0.5);
        }
    }
}
