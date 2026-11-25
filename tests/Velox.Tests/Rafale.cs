// Pendant une rafale (tirs répétés), les pas qui tombent entre deux tirs doivent rester remontés : le renforceur ne
// doit pas se couper pour toute la rafale.
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Rafale
    {
        private const string DefautConnu =
            "Défaut connu (Velox 5.23), cause probable : après un tir, le niveau « lent » du renforceur (60 ms) reste au-dessus d'un pas faible " +
            "pendant ~400 ms ; entre deux tirs d'une rafale, un pas n'est plus jugé « bref » et n'est presque plus remonté " +
            "(Net, Fort : +0,7 dB au lieu de +7 dB). À réactiver quand le moteur sera corrigé.";

        private static void Tirs(Scene s)
        {
            for (double t = 0.5; t < 1.6; t += 0.15) s.Ajouter(Scene.Tir(0.6, (int)(t * 100)), t, 1, 1);
        }

        /// <summary>Un pas sur le côté, 25 ms après la fin d'un tir de la rafale, garde au moins la moitié de sa remontée
        /// habituelle.</summary>
        [Theory(Skip = DefautConnu)]
        [InlineData(CompresseurPas.StyleNet, 65)] [InlineData(CompresseurPas.StyleNet, 100)]
        [InlineData(CompresseurPas.StylePuissant, 65)] [InlineData(CompresseurPas.StylePuissant, 100)]
        public void Pas_entre_deux_tirs_remontes(int style, int force)
        {
            double seul = Pas.RemonteeRenfort(style, force, 1, 0.15);
            double enRafale = Pas.RemonteeRenfort(style, force, 1, 0.15, Tirs, 0.5 + 0.15 * 3 + 0.085);
            Assert.True(enRafale >= seul / 2, $"pas pendant la rafale +{enRafale:F1} dB, seul +{seul:F1} dB");
        }

        /// <summary>Après la rafale, le renforceur revient : un pas 600 ms après le dernier tir est remonté presque comme
        /// un pas seul.</summary>
        [Theory]
        [InlineData(CompresseurPas.StyleNet, 65)] [InlineData(CompresseurPas.StylePuissant, 65)]
        public void Renforceur_revient_apres_la_rafale(int style, int force)
        {
            double seul = Pas.RemonteeRenfort(style, force, 1, 0.15);
            double apres = Pas.RemonteeRenfort(style, force, 1, 0.15, Tirs, 1.55 + 0.06 + 0.6);
            Assert.True(apres >= seul * 0.75, $"pas après la rafale +{apres:F1} dB, seul +{seul:F1} dB");
        }
    }
}
