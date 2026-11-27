// Ce que « Pas boostés » doit faire aux pas : les remonter quand ils viennent d'un côté (un ennemi), ne pas remonter
// tes propres sons au centre (en Net), les faire ressortir sous une explosion, ne pas les noyer dans le fond.
using System;
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Pas
    {
        /// <summary>Remontée (dB) que le renforceur ajoute à un pas faible, posé à « debut », gauche/droite pondérés.</summary>
        internal static double RemonteeRenfort(int style, int force, double gauche, double droite, Action<Scene> decor = null, double debut = 1.0)
        {
            var g = new double[2];
            for (int renfort = 0; renfort < 2; renfort++)
            {
                double duree = Math.Max(2, debut + 0.5);
                var s = new Scene(duree); var reference = new Scene(duree);
                s.AjouterSouffle(0.001, 3);
                if (decor != null) decor(s);
                var p = Scene.Pas(0.01, 9);
                s.Ajouter(p, debut, gauche, droite); reference.Ajouter(p, debut, gauche, droite);
                var y = Mesure.Traiter(new CompresseurPas(force, Scene.Fe, 0, renfort == 1, style), s.Son, 2);
                g[renfort] = Mesure.GainSur(y, reference.Son, 2, debut, debut + 0.06);
            }
            return g[1] - g[0];
        }

        [Fact]
        public void Poids_selon_le_cote()
        {
            Assert.Equal(0, CompresseurPas.PoidsCote(-30, -30, 0), 9);           // centré, Net : rien
            Assert.Equal(0.5, CompresseurPas.PoidsCote(-30, -30, 0.5), 9);
            Assert.Equal(1, CompresseurPas.PoidsCote(-30, -30 - CompresseurPas.EcartPlein, 0), 9);
            Assert.Equal(1, CompresseurPas.PoidsCote(-50, -20, 0), 9);           // nettement d'un côté : plein
            Assert.Equal(CompresseurPas.PoidsCote(-30, -34, 0), CompresseurPas.PoidsCote(-34, -30, 0), 9);   // symétrique
        }

        /// <summary>Net (5.24) : un son bref au centre est remonté à moitié (un ennemi juste devant ou derrière arrive au
        /// centre, comme tes propres pas) ; le même son sur le côté (un ennemi) l'est davantage.</summary>
        [Theory]
        [InlineData(35, 1.5)] [InlineData(65, 4)] [InlineData(100, 6)] [InlineData(130, 7)]
        public void Net_remonte_le_centre_a_moitie(int force, double coteMin)
        {
            double centre = RemonteeRenfort(CompresseurPas.StyleNet, force, 1, 1);
            double cote = RemonteeRenfort(CompresseurPas.StyleNet, force, 1, 0.15);
            Assert.True(centre >= coteMin * 0.3, $"pas au centre : +{centre:F1} dB (attendu au moins +{coteMin * 0.3:F1})");
            Assert.True(centre <= cote - 0.5, $"centre +{centre:F1} dB, côté +{cote:F1} dB : le côté doit rester devant");
            Assert.True(cote >= coteMin, $"pas sur le côté : +{cote:F1} dB (attendu au moins +{coteMin})");
        }

        /// <summary>Puissant (5.24) : un pas au centre est remonté autant qu'un pas sur le côté. En stéréo, un ennemi juste
        /// devant ou derrière toi arrive au centre, comme tes propres pas : on ne peut pas remonter l'un sans l'autre, et
        /// c'est l'ennemi derrière qu'on veut entendre. (Net garde un tri, plus doux : voir Net_remonte_le_centre_a_moitie.)</summary>
        [Theory]
        [InlineData(35)] [InlineData(65)] [InlineData(100)] [InlineData(130)]
        public void Puissant_remonte_aussi_le_centre(int force)
        {
            double centre = RemonteeRenfort(CompresseurPas.StylePuissant, force, 1, 1);
            double cote = RemonteeRenfort(CompresseurPas.StylePuissant, force, 1, 0.15);
            Assert.True(Math.Abs(cote - centre) <= 1, $"côté +{cote:F1} dB, centre +{centre:F1} dB");
            Assert.True(centre >= 3, $"centre +{centre:F1} dB");
        }

        /// <summary>Des pas faibles sur une ambiance continue (vent) ressortent avec le renforceur ; une rafale forte n'est pas
        /// remontée par lui.</summary>
        [Theory]
        [InlineData(35, 0.5)] [InlineData(65, 2)] [InlineData(100, 3)]
        public void Renforceur_detache_les_pas_du_fond(int force, double gainMin)
        {
            MoteurSon.MesurerRenfort(force, out double partSans, out double partAvec, out double creteSans, out double creteAvec);
            Assert.True(partAvec >= partSans + gainMin, $"part des pas {partSans:F1} → {partAvec:F1} dB");
            Assert.True(20 * Math.Log10(creteAvec / creteSans) <= 1.5, $"crête de la rafale {creteSans:F3} → {creteAvec:F3}");
        }

        /// <summary>Un son continu dans la bande des pas (souffle, pluie), venu d'un côté, n'est pas bref : le renforceur ne
        /// le remonte pas. Il reste l'accent des côtés (élargissement fixe de la bande des pas, voulu) : moins de 1,5 dB.</summary>
        [Theory]
        [InlineData(CompresseurPas.StyleNet)] [InlineData(CompresseurPas.StylePuissant)]
        public void Renforceur_ignore_les_sons_continus(int style)
        {
            var s = new Scene(2); var b = Scene.Bruit(12); var souffle = new float[s.Images];
            for (int i = 0; i < souffle.Length; i++) souffle[i] = (float)(0.01 * b());
            s.Ajouter(souffle, 0, 1, 0.3);
            var sans = Mesure.Traiter(new CompresseurPas(100, Scene.Fe, 0, false, style), s.Son, 2);
            var avec = Mesure.Traiter(new CompresseurPas(100, Scene.Fe, 0, true, style), s.Son, 2);
            double e = Mesure.Niveau(avec, 2, 0.5, 1.9) - Mesure.Niveau(sans, 2, 0.5, 1.9);
            Assert.True(e > -1 && e < 1.5, $"écart {e:F2} dB");
        }

        /// <summary>Un pas pendant une explosion forte ressort davantage avec le traitement.</summary>
        [Theory]
        [InlineData(35, 2)] [InlineData(65, 4)] [InlineData(100, 5)]
        public void Pas_sous_une_explosion(int force, double gainMin)
        {
            MoteurSon.MesurerSousExplosion(force, out double sans, out double avec);
            Assert.True(avec >= sans + gainMin, $"part du pas {sans:F1} → {avec:F1} dB");
        }

        /// <summary>Le pas qui suit une explosion est remonté autant que celui d'avant : le compresseur s'est relâché.</summary>
        [Theory]
        [InlineData(35)] [InlineData(65)] [InlineData(100)]
        public void Pas_apres_une_explosion(int force)
        {
            MoteurSon.Mesurer(force, out double ecartSans, out double ecartAvec, out double retourPas, out float crete);
            Assert.True(ecartAvec < ecartSans - 5, $"écart explosion/pas {ecartSans:F1} → {ecartAvec:F1} dB");
            Assert.InRange(retourPas, -2, 2);
            Assert.True(crete <= CompresseurPas.Plafond + 1e-6);
        }

        /// <summary>Des pas proches sur un fond faible : le traitement ne doit pas noyer les pas dans le fond remonté
        /// (défaut des premières versions, vu sur de vrais enregistrements).</summary>
        [Theory]
        [InlineData(CompresseurPas.StyleNet, 35)] [InlineData(CompresseurPas.StyleNet, 65)] [InlineData(CompresseurPas.StyleNet, 100)] [InlineData(CompresseurPas.StyleNet, 130)]
        [InlineData(CompresseurPas.StylePuissant, 35)] [InlineData(CompresseurPas.StylePuissant, 65)] [InlineData(CompresseurPas.StylePuissant, 100)] [InlineData(CompresseurPas.StylePuissant, 130)]
        public void Pas_pas_noyes_dans_le_fond(int style, int force)
        {
            MoteurSon.MesurerContraste(force, out double sans, out double avec, style);
            Assert.True(avec >= sans - 1, $"contraste des pas {sans:F1} → {avec:F1} dB");
        }
    }
}
