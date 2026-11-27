// Enregistrement long avec marqueurs (src/Noyau/EnregistrementLong.cs) : fichiers WAV complets et exacts, marqueurs au
// bon instant, arrêt tout seul à la durée prévue, évènements répétés notés une seule fois.
using System;
using System.IO;
using NettoyagePC;
using Xunit;

namespace Velox.Tests
{
    public class Marqueurs
    {
        private static string Dossier()
        {
            return Path.Combine(Path.GetTempPath(), "velox-long-" + Guid.NewGuid().ToString("N"));
        }

        [Fact]
        public void Fichiers_complets_et_marqueurs_au_bon_instant()
        {
            const int fe = 48000, canaux = 2, paquet = 480;
            string d = Dossier();
            try
            {
                var enr = new EnregistrementLong(d, canaux, fe, 3);
                var sans = new float[paquet * canaux]; var avec = new float[paquet * canaux];
                long image = 0; int paquets = 0;
                while (!enr.Fini && paquets < 1000)
                {
                    for (int i = 0; i < paquet; i++)
                    {
                        float v = (float)Math.Sin((image + i) * 0.01) * 0.5f;
                        sans[i * 2] = v; sans[i * 2 + 1] = -v; avec[i * 2] = v * 0.5f; avec[i * 2 + 1] = 0.25f;
                    }
                    enr.Ajouter(sans, avec, paquet); image += paquet; paquets++;
                    if (image == fe) enr.Noter("tic");                 // à 1,000 s
                    if (image == 2 * fe) { enr.Noter("ennemi"); enr.Noter("manque casque"); enr.Noter("manque casque"); }
                }
                Assert.True(enr.Fini);                                // arrêt tout seul à 3 s
                Assert.Equal(300, paquets);
                Assert.True(enr.Attendre(5000));
                Assert.StartsWith("ok|", enr.Resultat);
                Assert.Equal(1, enr.Tics); Assert.Equal(1, enr.Ennemis);

                var octets = File.ReadAllBytes(enr.CheminSans);
                Assert.Equal(44 + 3 * fe * canaux * 2, octets.Length);
                Assert.Equal(3 * fe * canaux * 2, BitConverter.ToInt32(octets, 40));
                Assert.Equal(36 + 3 * fe * canaux * 2, BitConverter.ToInt32(octets, 4));
                for (long i = 0; i < 3 * fe; i += 997)
                {
                    short g = BitConverter.ToInt16(octets, (int)(44 + i * 4)), dr = BitConverter.ToInt16(octets, (int)(44 + i * 4 + 2));
                    short attendu = (short)Math.Round((float)Math.Sin(i * 0.01) * 0.5f * 32767);
                    Assert.InRange(g, attendu - 1, attendu + 1); Assert.InRange(dr, -attendu - 1, -attendu + 1);
                }
                var oa = File.ReadAllBytes(enr.CheminAvec);
                Assert.Equal(octets.Length, oa.Length);
                Assert.Equal((short)Math.Round(0.25 * 32767), BitConverter.ToInt16(oa, 44 + 2));

                var lignes = File.ReadAllText(enr.CheminMarqueurs).TrimEnd('\n').Split('\n');
                Assert.Equal(3, lignes.Length);                       // le manque répété n'est noté qu'une fois
                Assert.Equal("1.000\t1.000\tTIC (F9)", lignes[0]);
                Assert.Equal("2.000\t2.000\tENNEMI RATE (F10)", lignes[1]);
                Assert.Equal("2.000\t2.000\tmanque casque", lignes[2]);
                string resume = File.ReadAllText(enr.CheminResume);
                Assert.Contains("00:01.000  TIC (F9)", resume);
                Assert.Contains("1 tic(s)", resume);
            }
            finally { try { Directory.Delete(d, true); } catch { } }
        }

        [Fact]
        public void Arret_demande_avant_la_fin()
        {
            string d = Dossier();
            try
            {
                var enr = new EnregistrementLong(d, 1, 48000, 300);
                var p = new float[480];
                for (int i = 0; i < 100; i++) enr.Ajouter(p, p, 480);
                enr.Finir();
                enr.Ajouter(p, p, 480);                                // ignoré : déjà arrêté
                enr.Noter("tic");                                     // ignoré aussi
                Assert.True(enr.Attendre(5000));
                Assert.Equal(44 + 48000 * 2, new FileInfo(enr.CheminSans).Length);
                Assert.Equal("", File.ReadAllText(enr.CheminMarqueurs));
                Assert.Equal(0, enr.Tics);
            }
            finally { try { Directory.Delete(d, true); } catch { } }
        }

        [Fact]
        public void Etat_du_moteur_avec_enregistrement_long()
        {
            var e = MoteurSon.LireLigneEtat("marche|30|6.0|1||2|0.0|0|0|10|cod|83|300|4|2");
            Assert.Equal(83, e.LongSecondes); Assert.Equal(300, e.LongMax); Assert.Equal(4, e.LongTics); Assert.Equal(2, e.LongEnnemis);
            var ancien = MoteurSon.LireLigneEtat("marche|30|6.0|1||2|0.0|0|0|10|cod");
            Assert.Equal(0, ancien.LongMax); Assert.Equal("cod", ancien.JeuCle);
        }
    }
}
