// « Mes pas plus discrets » : pendant que tu te déplaces, un son bref au centre (tes propres pas) est moins remonté ;
// à l'arrêt, un ennemi juste devant ou derrière toi (au centre aussi) l'est toujours en plein. Un pas venu d'un côté
// garde sa remontée dans les deux cas, et rien ne change d'un coup (pas de « tic »).
using System;
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class MesPas
    {
        /// <summary>Remontée (dB) que le renforceur ajoute à un pas faible, avec le joueur immobile ou en mouvement.</summary>
        private static double Remontee(int style, int force, double gauche, double droite, bool bouge)
        {
            var g = new double[2];
            for (int renfort = 0; renfort < 2; renfort++)
            {
                var s = new Scene(2); var reference = new Scene(2);
                s.AjouterSouffle(0.001, 3);
                var p = Scene.Pas(0.01, 9);
                s.Ajouter(p, 1.0, gauche, droite); reference.Ajouter(p, 1.0, gauche, droite);
                var c = new CompresseurPas(force, Scene.Fe, 0, renfort == 1, style) { JoueurBouge = bouge };
                var y = Mesure.Traiter(c, s.Son, 2);
                g[renfort] = Mesure.GainSur(y, reference.Son, 2, 1.0, 1.06);
            }
            return g[1] - g[0];
        }

        /// <summary>Tu marches : ton pas (au centre) est remonté bien moins qu'à l'arrêt (au plus 40 %).</summary>
        [Theory]
        [InlineData(CompresseurPas.StylePuissant, 65)] [InlineData(CompresseurPas.StylePuissant, 100)] [InlineData(CompresseurPas.StylePuissant, 130)]
        [InlineData(CompresseurPas.StyleNet, 65)]
        public void Pas_au_centre_moins_remonte_quand_tu_bouges(int style, int force)
        {
            double arret = Remontee(style, force, 1, 1, false), marche = Remontee(style, force, 1, 1, true);
            Assert.True(arret >= 3, $"à l'arrêt, pas au centre : +{arret:F1} dB seulement");
            Assert.True(marche <= arret * 0.4 + 0.3, $"en marchant, pas au centre +{marche:F1} dB (à l'arrêt +{arret:F1})");
            Assert.True(marche >= 0, $"en marchant, pas au centre baissé ({marche:F1} dB) : il doit seulement être moins remonté");
        }

        /// <summary>Tu marches et un ennemi est juste derrière toi : son pas arrive au centre, mais spatialisé par le jeu
        /// (gauche et droite différents, pas mono comme tes pas). Il garde toute sa remontée.</summary>
        [Theory]
        [InlineData(65)] [InlineData(100)]
        public void Ennemi_au_centre_garde_quand_tu_bouges(int force)
        {
            var g = new double[2];
            for (int bouge = 0; bouge < 2; bouge++)
            {
                var gg = new double[2];
                for (int renfort = 0; renfort < 2; renfort++)
                {
                    var s = new Scene(2); var reference = new Scene(2);
                    s.AjouterSouffle(0.001, 3);
                    var pg = Scene.Pas(0.01, 9); var pd = Scene.Pas(0.01, 23);   // même niveau, gauche et droite différents
                    s.Ajouter(pg, 1.0, 1, 0); s.Ajouter(pd, 1.0, 0, 1); reference.Ajouter(pg, 1.0, 1, 0); reference.Ajouter(pd, 1.0, 0, 1);
                    var c = new CompresseurPas(force, Scene.Fe, 0, renfort == 1, CompresseurPas.StylePuissant) { JoueurBouge = bouge == 1 };
                    gg[renfort] = Mesure.GainSur(Mesure.Traiter(c, s.Son, 2), reference.Son, 2, 1.0, 1.06);
                }
                g[bouge] = gg[1] - gg[0];
            }
            Assert.True(g[0] >= 3, $"à l'arrêt : +{g[0]:F1} dB");
            Assert.True(g[1] >= g[0] - 1, $"ennemi au centre : +{g[0]:F1} dB à l'arrêt, +{g[1]:F1} dB en marchant");
        }

        /// <summary>Tu marches : un ennemi sur le côté est remonté autant qu'à l'arrêt.</summary>
        [Theory]
        [InlineData(CompresseurPas.StylePuissant, 65)] [InlineData(CompresseurPas.StylePuissant, 100)] [InlineData(CompresseurPas.StyleNet, 65)]
        public void Pas_sur_le_cote_garde_quand_tu_bouges(int style, int force)
        {
            double arret = Remontee(style, force, 1, 0.15, false), marche = Remontee(style, force, 1, 0.15, true);
            Assert.True(Math.Abs(marche - arret) <= 1, $"pas sur le côté : +{arret:F1} dB à l'arrêt, +{marche:F1} dB en marchant");
        }

        /// <summary>Puissant, à l'arrêt : un pas au centre (ennemi juste derrière) garde toute sa remontée, comme avant.</summary>
        [Theory]
        [InlineData(65)] [InlineData(100)]
        public void A_l_arret_le_centre_reste_en_plein(int force)
        {
            double centre = Remontee(CompresseurPas.StylePuissant, force, 1, 1, false);
            double cote = Remontee(CompresseurPas.StylePuissant, force, 1, 0.15, false);
            Assert.True(Math.Abs(cote - centre) <= 1, $"à l'arrêt : côté +{cote:F1} dB, centre +{centre:F1} dB");
        }

        /// <summary>Tu t'arrêtes ou tu repars : le changement glisse (30 ms à la montée, 150 ms à la descente), sans saut.</summary>
        [Fact]
        public void Le_changement_glisse()
        {
            var s = new Scene(2);
            s.AjouterSouffle(0.001, 3);
            for (double t = 0.2; t < 1.9; t += 0.11) s.Ajouter(Scene.Pas(0.01, (int)(t * 100)), t, 1, 1);
            var c = new CompresseurPas(65, Scene.Fe, 1, true, CompresseurPas.StylePuissant);
            var un = new float[2];
            double avant = 0, saut = 0;
            for (int i = 0; i < s.Images; i++)
            {
                double t = (double)i / Scene.Fe;
                c.JoueurBouge = t >= 0.5 && t < 1.2;
                un[0] = s.Son[2 * i]; un[1] = s.Son[2 * i + 1];
                c.Traiter(un, 1, 2);
                saut = Math.Max(saut, Math.Abs(c.PartBouge - avant)); avant = c.PartBouge;
                if (Math.Abs(t - 0.6) < 1e-4) Assert.True(c.PartBouge > 0.95, $"100 ms après le départ : {c.PartBouge:F2}");
                if (Math.Abs(t - 1.8) < 1e-4) Assert.True(c.PartBouge < 0.05, $"600 ms après l'arrêt : {c.PartBouge:F2}");
            }
            Assert.True(saut < 0.01, $"saut de {saut:F3} d'un échantillon à l'autre");
        }

        /// <summary>Ce réglage ne fait que retirer une partie du renfort ajouté au centre : il ne baisse jamais un son sous son
        /// niveau sans renforceur. Ni ta rafale ni le pas fort d'un ennemi juste derrière toi (le défaut corrigé dans « ton
        /// arme ») ne passent sous ce niveau, et rien n'est remonté en plus.</summary>
        [Theory]
        [InlineData(false)] [InlineData(true)]
        public void Jamais_sous_le_son_sans_renfort(bool pasFort)
        {
            var s = new Scene(3);
            s.AjouterSouffle(0.01, 7);
            if (pasFort) for (double t = 0.5; t < 2.8; t += 0.4) s.Ajouter(Scene.Pas(0.4, (int)(t * 100)), t, 1, 1);
            else for (double t = 0.5; t < 1.5; t += 0.09) s.Ajouter(Scene.Tir(0.5, (int)(t * 100)), t, 1, 1);
            double t1 = pasFort ? 2.8 : 1.5;
            for (int arme = 0; arme <= 2; arme++)
            {
                double sansRenfort = Mesure.Niveau(Mesure.Traiter(new CompresseurPas(65, Scene.Fe, arme, false, CompresseurPas.StylePuissant), s.Son, 2), 2, 0.5, t1);
                double immobile = Mesure.Niveau(Mesure.Traiter(new CompresseurPas(65, Scene.Fe, arme, true, CompresseurPas.StylePuissant), s.Son, 2), 2, 0.5, t1);
                double enMarche = Mesure.Niveau(Mesure.Traiter(new CompresseurPas(65, Scene.Fe, arme, true, CompresseurPas.StylePuissant) { JoueurBouge = true }, s.Son, 2), 2, 0.5, t1);
                Assert.True(enMarche >= sansRenfort - 0.3, $"ton arme {arme} : {enMarche:F1} dB en marchant, {sansRenfort:F1} dB sans renforceur");
                Assert.True(enMarche <= immobile + 0.3, $"ton arme {arme} : {enMarche:F1} dB en marchant, {immobile:F1} dB à l'arrêt");
            }
        }

        [Fact]
        public void Touches_et_manette()
        {
            Assert.False(DeplacementJoueur.EnDeplacement(vk => false, 0, 0, 0));
            Assert.True(DeplacementJoueur.EnDeplacement(vk => vk == 'Z', 0, 0, 0));     // AZERTY
            Assert.True(DeplacementJoueur.EnDeplacement(vk => vk == 'W', 0, 0, 0));     // QWERTY
            Assert.True(DeplacementJoueur.EnDeplacement(vk => vk == 0x26, 0, 0, 0));    // flèche haut
            Assert.False(DeplacementJoueur.EnDeplacement(vk => vk == 'R', 0, 0, 0));    // recharger : tu ne bouges pas
            Assert.False(DeplacementJoueur.EnDeplacement(null, 1, 5000, -5000));        // stick au repos (zone morte)
            Assert.True(DeplacementJoueur.EnDeplacement(null, 1, 0, 20000));            // stick poussé
            Assert.False(DeplacementJoueur.EnDeplacement(null, 0, 0, 20000));           // aucune manette lue
            Assert.False(DeplacementJoueur.Bouge);                                      // rien de lancé ici : jamais « bouge »
        }

        [Fact]
        public void Reglage_retenu()
        {
            Assert.True(new ReglagesMoteurSon().MesPas);
            var r = new ReglagesMoteurSon { MesPas = false };
            Assert.False(ReglagesMoteurSon.Lire(r.Json()).MesPas);
            Assert.True(ReglagesMoteurSon.Lire("{\"Actif\":true}").MesPas);   // ancien fichier : activé
        }
    }
}
