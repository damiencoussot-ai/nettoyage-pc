// Sûreté du traitement : jamais de saturation, pas de plantage sur un son invalide, retard connu, résultat
// indépendant de la taille des paquets.
using System;
using System.Collections.Generic;
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Limiteur
    {
        public static IEnumerable<object[]> Reglages()
        {
            foreach (int style in new[] { CompresseurPas.StyleNet, CompresseurPas.StylePuissant })
                foreach (int force in new[] { 0, 35, 65, 100, 130 })
                    foreach (int arme in new[] { 0, 2 })
                        yield return new object[] { style, force, arme };
        }

        /// <summary>Scène très forte (explosion qui sature déjà, rafale, pas) : rien ne dépasse le plafond, en stéréo et en 7.1.</summary>
        [Theory, MemberData(nameof(Reglages))]
        public void Aucune_saturation(int style, int force, int arme)
        {
            foreach (int canaux in new[] { 2, 8 })
            {
                var s = new Scene(2, canaux);
                var gains = new double[canaux]; for (int c = 0; c < canaux; c++) gains[c] = 1;
                s.AjouterSouffle(0.05, 1);
                for (double t = 0.2; t < 1.0; t += 0.09) s.Ajouter(Scene.Tir(1.2, (int)(t * 100)), t, gains);
                s.AjouterSinus(45, 0.4);
                s.Ajouter(Scene.Pas(0.05, 3), 1.2, gains);
                var y = Mesure.Traiter(new CompresseurPas(force, Scene.Fe, arme, true, style), s.Son, canaux);
                Assert.True(Mesure.Crete(y) <= CompresseurPas.Plafond + 1e-6, $"crête {Mesure.Crete(y)} en {canaux} canaux");
            }
        }

        /// <summary>Un pilote en défaut peut envoyer NaN ou l'infini : remplacé par du silence, et le son qui suit reste
        /// normal (les filtres ne sont pas empoisonnés pour toujours).</summary>
        [Fact]
        public void Echantillons_invalides_sans_consequence()
        {
            var comp = new CompresseurPas(65, Scene.Fe, 1, true, CompresseurPas.StylePuissant);
            var s = new Scene(1); s.AjouterSouffle(0.05, 2);
            var t = s.Copie();
            t[1000] = float.NaN; t[2001] = float.PositiveInfinity; t[3000] = float.NegativeInfinity;
            var y = Mesure.Traiter(comp, t, 2);
            foreach (var v in y) Assert.False(float.IsNaN(v) || float.IsInfinity(v));
            var suite = Mesure.Traiter(comp, s.Son, 2);
            Assert.True(Mesure.Niveau(suite, 2, 0.2, 0.9) > -40, "le son après l'incident doit être normal");
        }

        [Fact]
        public void Silence_reste_silence()
        {
            var s = new Scene(1);
            var y = Mesure.Traiter(new CompresseurPas(130, Scene.Fe, 2, true, CompresseurPas.StylePuissant), s.Son, 2);
            Assert.Equal(0f, Mesure.Crete(y));
        }

        /// <summary>Hors jeu (« jeux seulement ») : le son passe tel quel, simplement retardé de Retard images.</summary>
        [Fact]
        public void Sans_melange_le_son_passe_tel_quel()
        {
            var comp = new CompresseurPas(65, Scene.Fe, 1, true, CompresseurPas.StylePuissant) { MelangeCible = 0 };
            var amorce = new Scene(3); amorce.AjouterSouffle(0.01, 5);
            Mesure.Traiter(comp, amorce.Son, 2);   // le mélange glisse vers 0 en ~100 ms
            var s = new Scene(1); s.AjouterSouffle(0.2, 6); s.Ajouter(Scene.Tir(0.8, 1), 0.3, 1, 0.5);
            var y = Mesure.Traiter(comp, s.Son, 2);
            for (int i = 0; i < y.Length - 2 * comp.Retard; i++) Assert.Equal(s.Son[i], y[i], 4);
        }

        [Fact]
        public void Retard_court()
        {
            var comp = new CompresseurPas(65, Scene.Fe);
            Assert.InRange(comp.Retard * 1000.0 / Scene.Fe, 1, 2);   // 1,5 ms d'anticipation du limiteur
        }

        /// <summary>Le moteur reçoit des paquets de taille variable : le résultat ne doit pas en dépendre.</summary>
        [Fact]
        public void Resultat_independant_des_paquets()
        {
            var s = new Scene(1.5); s.AjouterSouffle(0.01, 7);
            s.Ajouter(Scene.Tir(0.8, 2), 0.4, 1, 1); s.Ajouter(Scene.Pas(0.02, 3), 0.9, 1, 0.2);
            var entier = s.Copie();
            new CompresseurPas(100, Scene.Fe, 1, true, CompresseurPas.StylePuissant).Traiter(entier, s.Images, 2);
            var paquets = s.Copie();
            var comp = new CompresseurPas(100, Scene.Fe, 1, true, CompresseurPas.StylePuissant);
            var alea = new Random(3); int p = 0;
            while (p < s.Images)
            {
                int n = Math.Min(s.Images - p, 1 + alea.Next(1000));
                var b = new float[n * 2]; Array.Copy(paquets, p * 2, b, 0, n * 2);
                comp.Traiter(b, n, 2);
                Array.Copy(b, 0, paquets, p * 2, n * 2); p += n;
            }
            Assert.Equal(entier, paquets);
        }

        /// <summary>Courbe du compresseur : un son plus fort en entrée reste plus fort (ou égal) en sortie.</summary>
        [Theory]
        [InlineData(CompresseurPas.StyleNet, 35)] [InlineData(CompresseurPas.StyleNet, 100)]
        [InlineData(CompresseurPas.StylePuissant, 65)] [InlineData(CompresseurPas.StylePuissant, 130)]
        public void Courbe_croissante(int style, int force)
        {
            var comp = new CompresseurPas(force, Scene.Fe, 0, true, style);
            double avant = double.NegativeInfinity;
            for (double x = -90; x <= 6; x += 0.25)
            {
                double y = x + comp.Gain(x);
                Assert.True(y >= avant - 1e-9, $"sortie {y:F2} dB à {x} dB, plus basse que {avant:F2}");
                avant = y;
            }
            Assert.Equal(comp.Remontee, comp.Gain(-80), 6);   // sons faibles : remontés de « Remontee »
        }
    }
}
