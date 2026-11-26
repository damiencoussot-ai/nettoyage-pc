// Le son traité est réécrit sur le casque : les horloges du câble et du casque ne battent pas au même rythme, et le casque
// prend le son par paquets à son propre instant. Rien ne doit être sauté d'un coup (un saut = un « tic »), sans laisser le
// retard grandir ni le tampon se vider.
using System;
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Casque
    {
        const int Fe = 48000, Paquet = 480;

        /// <summary>Résultat d'une simulation de 2 minutes : sauts secs (ancienne façon), manques, tampon plein, retard.</summary>
        sealed class Bilan { public int Sauts, Manques, Pleins, ReserveFin; public double RetardMoyen, RetardMax, CorrectionMax; }

        /// <summary>
        /// Câble : un paquet de 10 ms toutes les 480 images (horloge du câble), traité 0 à 3 ms plus tard, parfois après un
        /// à-coup de 15 ms (PC chargé). Casque : prend 480 images toutes les 480 × (1 + dérive) images, tampon de 80 ms.
        /// « ancien » : le surplus au-delà de réserve + 12 ms est sauté d'un coup (version d'avant).
        /// </summary>
        static Bilan Simuler(double derive, bool ancien, int graine)
        {
            var r = new Random(graine);
            var b = new Bilan();
            int tampon = Fe * 80 / 1000, pad = 0, reserve = -1; bool amorce = false;
            var niveau = new MoteurSon.Remplissage();
            double prochainePrise = Paquet * 0.37, dernierTraitement = 0, sommeRetard = 0; int mesures = 0;
            int paquets = 120 * Fe / Paquet;
            for (int i = 1; i <= paquets; i++)
            {
                double pret = i * Paquet, retard = r.NextDouble() * Fe * 0.003;
                if (r.Next(300) == 0) retard += Fe * 0.015;
                double quand = Math.Max(dernierTraitement, pret + retard);
                dernierTraitement = quand;
                while (prochainePrise <= quand)
                {
                    if (pad < Paquet) { if (amorce) b.Manques++; pad = 0; } else pad -= Paquet;
                    prochainePrise += Paquet * (1 + derive);
                }
                // Jouer()
                if (reserve < 0) reserve = Fe * 12 / 1000;
                if (pad == 0)
                {
                    if (amorce) reserve = Math.Min(Fe * 40 / 1000, reserve + Fe * 4 / 1000);
                    amorce = true;
                    pad = Math.Min(tampon, reserve);
                }
                int sortie = Paquet;
                if (ancien)
                {
                    if (pad + Paquet > reserve + Fe * 12 / 1000) { int trop = Math.Min(Paquet, pad + Paquet - reserve); if (trop > 0) b.Sauts++; sortie = Paquet - trop; }
                }
                else
                {
                    sortie = niveau.Suivre(pad, Paquet, reserve, Fe);
                    b.CorrectionMax = Math.Max(b.CorrectionMax, Math.Abs(sortie - Paquet) / (double)Paquet);
                }
                if (i > 5 * Fe / Paquet) { double ms = 1000.0 * pad / Fe; sommeRetard += ms; mesures++; b.RetardMax = Math.Max(b.RetardMax, ms); }
                if (sortie > tampon - pad) { b.Pleins++; sortie = tampon - pad; }
                pad += sortie;
            }
            b.RetardMoyen = sommeRetard / mesures; b.ReserveFin = reserve * 1000 / Fe;
            return b;
        }

        /// <summary>
        /// Le scénario reproduit bien le « tic » : avec l'ancienne façon, une petite dérive d'horloge (carte son USB comme la
        /// GoXLR) fait sauter du son des dizaines de fois en 2 minutes. Avec la nouvelle : aucun saut sec, aucun manque
        /// en plus (les à-coups du PC en font quelques-uns, comme avant), correction de 2 % au plus par paquet, retard moyen qui
        /// reste près de la réserve, réserve pas plus grande qu'avant.
        /// </summary>
        [Theory]
        [InlineData(0.0005)] [InlineData(-0.0005)] [InlineData(0.0001)] [InlineData(-0.0001)] [InlineData(0.002)] [InlineData(-0.002)] [InlineData(0.0)]
        public void Derive_d_horloge_sans_saut(double derive)
        {
            for (int graine = 1; graine <= 3; graine++)
            {
                var avant = Simuler(derive, true, graine);
                var apres = Simuler(derive, false, graine);
                Assert.True(avant.Sauts >= 5, $"scénario trop doux : {avant.Sauts} sauts avant");
                Assert.Equal(0, apres.Pleins);
                Assert.True(apres.Manques <= avant.Manques, $"dérive {derive} : {apres.Manques} manques (avant : {avant.Manques})");
                Assert.True(apres.ReserveFin <= avant.ReserveFin, $"réserve {apres.ReserveFin} ms (avant : {avant.ReserveFin} ms)");
                Assert.True(apres.CorrectionMax <= 0.021, $"correction {apres.CorrectionMax:P1} par paquet");
                Assert.True(apres.RetardMoyen <= apres.ReserveFin + 8, $"retard moyen {apres.RetardMoyen:F1} ms pour une réserve de {apres.ReserveFin} ms");
            }
        }

        /// <summary>Le ré-échantillonnage garde la forme du son (erreur infime sur un 1 kHz) et ne crée aucun saut entre
        /// deux paquets.</summary>
        [Theory]
        [InlineData(475)] [InlineData(485)] [InlineData(470)] [InlineData(490)]
        public void Reechantillonnage_sans_saut(int sortie)
        {
            const double f = 1000;
            var src = new float[Paquet * 2]; var dst = new float[sortie * 2];
            double ecartMax = 0, sautMax = 0, pente = 2 * Math.PI * f / Fe * 1.02;   // plus grand pas naturel (son un peu accéléré)
            double precedent = double.NaN, phase = 0;
            for (int p = 0; p < 20; p++)
            {
                // paquet qui commence là où le précédent s'est arrêté (une image plus loin)
                for (int i = 0; i < Paquet; i++) { double v = Math.Sin(phase + 2 * Math.PI * f * i / Fe); src[2 * i] = (float)v; src[2 * i + 1] = (float)(0.5 * v); }
                MoteurSon.Reechantillonner(src, Paquet, 2, dst, sortie);
                Assert.Equal(src[0], dst[0]); Assert.Equal(src[2 * (Paquet - 1)], dst[2 * (sortie - 1)]);
                double pas = (Paquet - 1) / (double)(sortie - 1);
                for (int j = 0; j < sortie; j++)
                {
                    double attendu = Math.Sin(phase + 2 * Math.PI * f * j * pas / Fe);
                    ecartMax = Math.Max(ecartMax, Math.Abs(dst[2 * j] - attendu));
                    Assert.Equal(0.5 * dst[2 * j], dst[2 * j + 1], 5);
                    if (!double.IsNaN(precedent)) sautMax = Math.Max(sautMax, Math.Abs(dst[2 * j] - precedent));
                    precedent = dst[2 * j];
                }
                phase += 2 * Math.PI * f * Paquet / Fe;
            }
            Assert.True(ecartMax < 2e-3, $"écart {ecartMax:E2}");
            Assert.True(sautMax <= pente * 1.01, $"saut {sautMax:F4} pour un pas naturel de {pente:F4}");
        }
    }
}
