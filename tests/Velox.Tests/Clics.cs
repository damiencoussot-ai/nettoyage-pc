// Clics : le moteur ne doit jamais changer un gain d'un coup (un saut de gain sur un son s'entend comme un « tic »).
using System;
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Clics
    {
        /// <summary>Fond fort et pas renforcé tout près du plafond : le renfort doit être réduit, mais en glissant (avant
        /// 5.25 il tombait d'un échantillon à l'autre), et le limiteur ne saute pas non plus.</summary>
        [Theory]
        [InlineData(65)] [InlineData(100)] [InlineData(130)]
        public void Renfort_et_limiteur_glissent(int force)
        {
            var s = new Scene(2);
            s.AjouterSinus(220, 0.8);
            for (double t = 0.3; t < 1.8; t += 0.25) s.Ajouter(Scene.Pas(0.15, (int)(t * 100)), t, 1, 0.3);
            var c = new CompresseurPas(force, Scene.Fe, 1, true, CompresseurPas.StylePuissant);
            var un = new float[2];
            double k0 = 1, l0 = 1, sautK = 0, sautL = 0, kMin = 1;
            for (int i = 0; i < s.Images; i++)
            {
                un[0] = s.Son[2 * i]; un[1] = s.Son[2 * i + 1];
                c.Traiter(un, 1, 2);
                sautK = Math.Max(sautK, k0 - c.PartRenfort); sautL = Math.Max(sautL, l0 - c.GainLimiteur);
                k0 = c.PartRenfort; l0 = c.GainLimiteur; kMin = Math.Min(kMin, k0);
                Assert.True(Math.Abs(un[0]) <= CompresseurPas.Plafond + 1e-6);
            }
            Assert.True(kMin < 0.9, $"le renfort n'a pas eu à être réduit (part mini {kMin:F2}) : la scène n'essaie rien");
            Assert.True(sautK <= 0.1, $"part du renfort : saut de {sautK:F2} d'un échantillon à l'autre");
            Assert.True(sautL <= 0.05, $"limiteur : saut de {sautL:F3} d'un échantillon à l'autre");
        }

        /// <summary>Puissant : le renfort d'un pas dure au moins 8 ms (plus court, il ne touchait que l'attaque du son et
        /// la changeait en « tic »).</summary>
        [Theory]
        [InlineData(65)] [InlineData(100)]
        public void Renfort_d_un_pas_pas_trop_bref(int force)
        {
            var s = new Scene(1);
            s.AjouterSouffle(0.01, 3);
            s.Ajouter(Scene.Pas(0.05, 11), 0.5, 1, 0.3);
            var c = new CompresseurPas(force, Scene.Fe, 1, true, CompresseurPas.StylePuissant);
            var un = new float[2];
            int duree = 0;
            for (int i = 0; i < s.Images; i++)
            {
                un[0] = s.Son[2 * i]; un[1] = s.Son[2 * i + 1];
                c.Traiter(un, 1, 2);
                if (c.DernierRenfort > 6) duree++;
            }
            Assert.True(duree >= 8 * Scene.Fe / 1000, $"renfort au-dessus de 6 dB pendant {duree * 1000.0 / Scene.Fe:F1} ms");
        }
    }
}
