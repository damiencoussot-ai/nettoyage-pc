// « Ton arme » : tes propres tirs (forts, au centre) sont baissés, pas ceux des ennemis (sur le côté).
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class TonArme
    {
        /// <summary>Tu tires en rafale pendant qu'un ennemi marche sur ta gauche : le pas ressort davantage.</summary>
        [Theory]
        [InlineData(1, false, 2.5)] [InlineData(2, false, 4.5)]
        [InlineData(1, true, 4)] [InlineData(2, true, 7)]
        public void Pas_ennemi_pendant_que_tu_tires(int arme, bool septUn, double gainMin)
        {
            MoteurSon.MesurerArme(arme, septUn, out double sans, out double avec, out float crete);
            Assert.True(avec >= sans + gainMin, $"part du pas {sans:F1} → {avec:F1} dB");
            Assert.True(crete <= CompresseurPas.Plafond + 1e-6);
        }

        /// <summary>Un tir ennemi venu d'un côté n'est pas pris pour le tien : même niveau avec ou sans « ton arme ».</summary>
        [Theory]
        [InlineData(CompresseurPas.StyleNet)] [InlineData(CompresseurPas.StylePuissant)]
        public void Tir_ennemi_sur_le_cote_garde(int style)
        {
            var s = new Scene(2); var reference = new Scene(2);
            for (double t = 0.5; t < 1.5; t += 0.12) { var tir = Scene.Tir(0.5, (int)(t * 100)); s.Ajouter(tir, t, 1, 0.1); reference.Ajouter(tir, t, 1, 0.1); }
            var sans = Mesure.Traiter(new CompresseurPas(65, Scene.Fe, 0, true, style), s.Son, 2);
            var avec = Mesure.Traiter(new CompresseurPas(65, Scene.Fe, 2, true, style), s.Son, 2);
            double ecart = Mesure.GainSur(avec, reference.Son, 2, 0.5, 1.5) - Mesure.GainSur(sans, reference.Son, 2, 0.5, 1.5);
            Assert.InRange(ecart, -1, 1);
        }

        /// <summary>Un ennemi juste derrière toi, proche : son pas arrive fort et au centre, comme ton arme. Il a la forme
        /// d'un pas (presque rien dans les graves) : « ton arme » le baisse bien moins (PR 16 avant correction : −5,4 dB avec le réglage 2 ; maintenant −3,4).</summary>
        [Theory]
        [InlineData(1)] [InlineData(2)]
        public void Pas_fort_au_centre_epargne(int arme)
        {
            var s = new Scene(3); var reference = new Scene(3);
            s.AjouterSouffle(0.01, 7);
            for (double t = 0.5; t < 2.8; t += 0.4) { var p = Scene.Pas(0.4, (int)(t * 100)); s.Ajouter(p, t, 1, 1); reference.Ajouter(p, t, 1, 1); }
            var sans = Mesure.Traiter(new CompresseurPas(65, Scene.Fe, 0, true, CompresseurPas.StylePuissant), s.Son, 2);
            var avec = Mesure.Traiter(new CompresseurPas(65, Scene.Fe, arme, true, CompresseurPas.StylePuissant), s.Son, 2);
            double ecart = Mesure.GainSur(avec, reference.Son, 2, 0.5, 2.8) - Mesure.GainSur(sans, reference.Son, 2, 0.5, 2.8);
            Assert.True(ecart >= -4, $"pas centré et fort baissé de {-ecart:F1} dB par « ton arme » {arme}");
        }

        /// <summary>Ta rafale (au centre) est bien baissée.</summary>
        [Fact]
        public void Ta_rafale_est_baissee()
        {
            var s = new Scene(2); var reference = new Scene(2);
            for (double t = 0.5; t < 1.5; t += 0.09) { var tir = Scene.Tir(0.5, (int)(t * 100)); s.Ajouter(tir, t, 1, 1); reference.Ajouter(tir, t, 1, 1); }
            var normale = Mesure.Traiter(new CompresseurPas(65, Scene.Fe, 0), s.Son, 2);
            var tresReduite = Mesure.Traiter(new CompresseurPas(65, Scene.Fe, 2), s.Son, 2);
            double ecart = Mesure.GainSur(tresReduite, reference.Son, 2, 0.5, 1.5) - Mesure.GainSur(normale, reference.Son, 2, 0.5, 1.5);
            Assert.True(ecart <= -2, $"rafale baissée de {-ecart:F1} dB seulement");
        }
    }
}
