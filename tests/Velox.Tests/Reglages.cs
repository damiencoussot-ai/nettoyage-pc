// Réglages du moteur (fichier moteur-son.json) et réécoute « avant / après ».
using System.Collections.Generic;
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Reglages
    {
        private const string Sortie = "{0.0.0.00000000}.{12345678-90ab-cdef-1234-567890abcdef}";
        private const string Entree = "{0.0.1.00000000}.{12345678-90ab-cdef-1234-567890abcdef}";

        [Fact]
        public void Aller_retour_json()
        {
            var r = new ReglagesMoteurSon
            {
                Actif = true, Force = 100, JeuxSeulement = false, Source = Entree, Destination = Sortie, DefautAvant = Sortie,
                Arme = 2, Style = CompresseurPas.StyleNet, ParJeuActif = false, RenommerAuRallumage = true,
                ParJeu = new Dictionary<string, int[]> { { "cod", new[] { 130, 0 } }, { "valorant", new[] { 35, 1 } } },
            };
            var l = ReglagesMoteurSon.Lire(r.Json());
            Assert.True(l.Actif); Assert.Equal(100, l.Force); Assert.False(l.JeuxSeulement);
            Assert.Equal(Entree, l.Source); Assert.Equal(Sortie, l.Destination); Assert.Equal(Sortie, l.DefautAvant);
            Assert.Equal(2, l.Arme); Assert.Equal(CompresseurPas.StyleNet, l.Style); Assert.False(l.ParJeuActif); Assert.True(l.RenommerAuRallumage);
            Assert.Equal(new[] { 130, 0 }, l.ParJeu["cod"]); Assert.Equal(new[] { 35, 1 }, l.ParJeu["valorant"]);
        }

        [Fact]
        public void Valeurs_hors_limites_bornees()
        {
            var l = ReglagesMoteurSon.Lire("{\"Force\": 500, \"Arme\": -3, \"Style\": 9, \"Source\": \"C:\\\\x\", \"Destination\": \"n'importe quoi\"," +
                                           " \"ParJeu\": {\"cod\": [999, 7], \"PAS VALIDE\": [10, 1], \"bf\": [1, 2, 3]}}");
            Assert.Equal(130, l.Force); Assert.Equal(0, l.Arme); Assert.Equal(1, l.Style);
            Assert.Equal("", l.Source); Assert.Equal("", l.Destination);
            Assert.Equal(new[] { 130, 2 }, l.ParJeu["cod"]);
            Assert.False(l.ParJeu.ContainsKey("PAS VALIDE")); Assert.False(l.ParJeu.ContainsKey("bf"));
        }

        [Theory]
        [InlineData("")] [InlineData("pas du json")] [InlineData("[1, 2]")] [InlineData("{\"Force\": \"abc\"}")]
        public void Fichier_abime_donne_les_reglages_par_defaut(string json)
        {
            var l = ReglagesMoteurSon.Lire(json);
            var d = new ReglagesMoteurSon();
            Assert.Equal(d.Force, l.Force); Assert.Equal(d.Arme, l.Arme); Assert.Equal(d.Style, l.Style); Assert.False(l.Actif);
        }

        [Fact]
        public void Reglage_par_jeu()
        {
            var r = new ReglagesMoteurSon { Force = 65, Arme = 1, ParJeu = new Dictionary<string, int[]> { { "cod", new[] { 100, 2 } } } };
            r.Effectifs("cod", out int f, out int a); Assert.Equal((100, 2), (f, a));
            r.Effectifs("valorant", out f, out a); Assert.Equal((65, 1), (f, a));
            r.Effectifs("", out f, out a); Assert.Equal((65, 1), (f, a));
            r.ParJeuActif = false;
            r.Effectifs("cod", out f, out a); Assert.Equal((65, 1), (f, a));
        }

        /// <summary>Réécoute : il faut 2 s gardées ; les deux versions sont rejouées au même volume moyen.</summary>
        [Fact]
        public void Reecoute_au_meme_volume()
        {
            var m = new MemoireReecoute(2, Scene.Fe, 10);
            var s = new Scene(1); s.AjouterSouffle(0.1, 1);
            var fort = new float[s.Son.Length]; for (int i = 0; i < fort.Length; i++) fort[i] = s.Son[i] * 2;
            m.GarderSec(s.Son, s.Images); m.GarderTraite(fort, s.Images);
            Assert.False(m.Lancer());   // 1 s seulement
            m.GarderSec(s.Son, s.Images); m.GarderTraite(fort, s.Images);
            Assert.True(m.Lancer());
            Assert.Equal(1f, m.GainSec);
            Assert.Equal(0.5f, m.GainTraite, 3);   // le plus fort des deux est baissé
            Assert.Equal(1, m.Phase);
        }
    }
}
