// Velox - « Pas boostés » : passage d'un réglage à un autre sans à-coup de volume.
using System;

namespace NettoyagePC
{
    /// <summary>
    /// Tient le moteur de traitement (CompresseurPas) en service et le remplace sans à-coup quand le réglage change
    /// (touche de force, « ton arme », style, réglage propre au jeu qui démarre).
    ///
    /// Un moteur neuf ne sait rien du son en cours : ses niveaux suivis partent de zéro. Branché tel quel (même avec un
    /// fondu de 30 ms), il prenait le fond du jeu pour du silence et le remontait d'un coup : mesuré sur l'enregistrement
    /// long du 27/11 (20 bascules de Fort à Extrême, Fort à « arme 2 », Net à Puissant…), +9 à +14 dB en moyenne
    /// pendant ~0,1 s après le fondu, jusqu'à +24 dB. Le nouveau moteur est donc d'abord « chauffé » : il traite en
    /// coulisse les 0,5 dernière seconde du son du jeu (gardée ici, son brut), puis rattrape le direct, et seulement
    /// alors il prend le relais en fondu. Mesuré : écart au moteur déjà en route ≤ 0,2 dB.
    ///
    /// Le rattrapage est étalé : à chaque paquet, au plus « rythme » paquets de retard sont traités (3 par défaut, soit
    /// ~1 ms de calcul pour 10 ms de son) : jamais de gros calcul d'un coup dans le fil du son, qui ferait un craquement.
    /// Le relais se fait ainsi environ 0,25 s après le changement de réglage.
    /// </summary>
    internal sealed class RelaisReglage
    {
        public readonly int Canaux, Frequence;
        private readonly int rythme, histoImages;
        private readonly float[] histo;          // son brut récent (anneau)
        private long ecrit, lu;                  // images écrites dans l'anneau, et lues par le moteur en préparation
        private CompresseurPas ancien, prochain;
        private int fondu, fonduLong;
        private float[] copie = new float[0], bout = new float[0];

        /// <summary>Moteur en service (c'est lui qui donne les relevés : réduction des explosions, de ton arme…).</summary>
        public CompresseurPas Actuel { get; private set; }

        /// <summary>Vrai tant qu'un nouveau réglage se prépare ou que le fondu n'est pas fini.</summary>
        public bool EnCours { get { return prochain != null || ancien != null; } }

        public double MelangeCible = 1;
        public bool JoueurBouge;

        public RelaisReglage(int canaux, int frequence, double secondes = 0.5, int rythme = 3)
        {
            Canaux = Math.Max(1, canaux); Frequence = Math.Max(8000, frequence);
            this.rythme = Math.Max(2, rythme);
            histoImages = Math.Max(1, (int)(secondes * Frequence));
            histo = new float[histoImages * Canaux];
            fonduLong = Math.Max(1, Frequence * 30 / 1000);
        }

        /// <summary>Premier moteur (son qui démarre ou appareil rouvert) : en service tout de suite.</summary>
        public void Demarrer(CompresseurPas c)
        {
            Actuel = c; ancien = null; prochain = null; ecrit = 0; lu = 0;
        }

        /// <summary>Nouveau réglage : le moteur donné se prépare sur le son récent, puis prend le relais. Un autre
        /// changement avant la fin remplace celui en préparation.</summary>
        public void Changer(CompresseurPas c)
        {
            if (Actuel == null) { Demarrer(c); return; }
            prochain = c;
            lu = Math.Max(0, ecrit - histoImages);
        }

        /// <summary>Traite un paquet (« t » : son brut, remplacé par le son traité).</summary>
        public void Traiter(float[] t, int images)
        {
            Garder(t, images);
            // le nouveau moteur a rattrapé le direct (et l'éventuel fondu d'avant est fini) : il prend le relais
            if (prochain != null && ancien == null && lu >= ecrit - images)
            {
                ancien = Actuel; Actuel = prochain; prochain = null; fondu = 0;
            }
            Appliquer(Actuel); Appliquer(ancien);
            if (ancien == null) { Actuel.Traiter(t, images, Canaux); return; }
            int n = images * Canaux;
            if (copie.Length < n) copie = new float[n];
            Array.Copy(t, copie, n);
            ancien.Traiter(copie, images, Canaux);
            Actuel.Traiter(t, images, Canaux);
            for (int i = 0; i < images; i++, fondu++)
            {
                float a = Math.Min(1f, fondu / (float)fonduLong);
                for (int q = 0; q < Canaux; q++) { int j = i * Canaux + q; t[j] = copie[j] + (t[j] - copie[j]) * a; }
            }
            if (fondu >= fonduLong) ancien = null;
        }

        /// <summary>Garde le son brut d'un paquet sans le traiter (réécoute en cours), et avance la préparation.</summary>
        public void Garder(float[] t, int images)
        {
            int p = (int)(ecrit % histoImages);
            for (int i = 0; i < images; i++)
            {
                Array.Copy(t, i * Canaux, histo, p * Canaux, Canaux);
                if (++p == histoImages) p = 0;
            }
            ecrit += images;
            if (prochain == null) return;
            // préparation : le son d'avant ce paquet, au plus « rythme » paquets d'un coup
            long fin = ecrit - images;
            if (lu < ecrit - histoImages) lu = ecrit - histoImages;   // (paquet plus long que l'anneau)
            int budget = rythme * Math.Max(1, images);
            Appliquer(prochain);
            while (lu < fin && budget > 0)
            {
                int k = (int)Math.Min(Math.Min(fin - lu, budget), 480);
                if (bout.Length < k * Canaux) bout = new float[k * Canaux];
                int q = (int)(lu % histoImages);
                int a = Math.Min(k, histoImages - q);
                Array.Copy(histo, q * Canaux, bout, 0, a * Canaux);
                if (a < k) Array.Copy(histo, 0, bout, a * Canaux, (k - a) * Canaux);
                prochain.Traiter(bout, k, Canaux);   // résultat jeté : seuls ses niveaux suivis comptent
                lu += k; budget -= k;
            }
        }

        private void Appliquer(CompresseurPas c)
        {
            if (c == null) return;
            c.MelangeCible = MelangeCible; c.JoueurBouge = JoueurBouge;
        }
    }
}
