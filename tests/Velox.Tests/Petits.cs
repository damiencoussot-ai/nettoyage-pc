// Petites vérifications hors du son : signataire de VB-Cable, options du moteur mal écrites.
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Petits
    {
        [Theory]
        [InlineData("CN=Vincent Burel, O=Vincent Burel, L=Paris, C=FR", true)]
        [InlineData("CN=VB-Audio Software, O=VB-Audio Software, C=FR", true)]
        [InlineData("CN=\"VB-Audio Software\", C=FR", true)]
        // sujet réel du certificat de VBCABLE_Setup_x64.exe (paquet 46)
        [InlineData("CN=BUREL VINCENT Entrepreneur individuel, O=BUREL VINCENT Entrepreneur individuel, STREET=21 avenue des Marguerites, L=Eymet, S=Dordogne, C=FR, OID.1.3.6.1.4.1.311.60.2.1.3=FR, SERIALNUMBER=423 734 177, OID.2.5.4.15=Private Organization", true)]
        [InlineData("CN=BUREL VINCENT Pirate, O=Pirate, C=FR", false)]
        [InlineData("CN=Faux BUREL VINCENT Entrepreneur individuel, C=FR", false)]
        [InlineData("CN=Autre Editeur, O=Autre, C=FR", false)]
        [InlineData("CN=Faux, O=Faux, OU=VB-Audio", false)]
        [InlineData("CN=Pas VB-Audio, O=Pirate, C=FR", false)]
        [InlineData("CN=VB-Audiophile, O=Pirate", false)]
        [InlineData("CN=Faux Vincent Burel, C=FR", false)]
        public void Signataire_de_VB_Cable(string sujet, bool accepte)
        {
            Assert.Equal(accepte, InstallationCable.Signataire.IsMatch(sujet));
        }

        /// <summary>Une durée illisible garde la valeur par défaut (avant : 0 jour, soit « tous les fichiers »).</summary>
        [Fact]
        public void Option_illisible_garde_la_valeur_par_defaut()
        {
            var o = OptionsMoteur.Lire(new[] { "--jours-telechargements", "abc", "--jours-temp", "-3", "--seuil-mo", "" });
            Assert.Equal(90, o.JoursTelechargements);
            Assert.Equal(2, o.JoursTemp);
            Assert.Equal(10, o.SeuilMo);
            Assert.Equal(30, OptionsMoteur.Lire(new[] { "--jours-telechargements", "30" }).JoursTelechargements);
        }
    }
}
