// Velox - enregistrement long « avec marqueurs » (Studio son > Pas boostés) : quelques minutes de son du jeu, sans et avec
// Velox, pendant lesquelles le joueur appuie sur une touche (F9 : « j'entends un tic », F10 : « j'ai raté un ennemi »).
// Les fichiers vont sur le Bureau, dans un dossier « Velox-long-… » : deux WAV, les marqueurs (format des étiquettes
// d'Audacity) et un résumé. But : séparer les vrais pas des « tic tic » sur le son réel du joueur.
//
// Le fil du son ne touche jamais au disque : il copie chaque paquet dans un anneau en mémoire (quelques secondes), un
// autre fil écrit les fichiers. Si le disque bloque plus longtemps que l'anneau, le paquet est noté « perdu » au lieu de
// retarder le son du casque.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace NettoyagePC
{
    internal sealed class EnregistrementLong
    {
        /// <summary>Un moment noté pendant l'enregistrement : marqueur du joueur ou évènement du moteur.</summary>
        internal struct Moment
        {
            /// <summary>Position dans les fichiers (en images, depuis le début).</summary>
            public long Image;
            /// <summary>« tic », « ennemi » (touches du joueur) ou un évènement du moteur (« manque casque »…).</summary>
            public string Type;
        }

        public readonly int Canaux, Frequence;
        public readonly long ImagesMax;
        public readonly string Dossier;
        /// <summary>Texte libre ajouté en tête du résumé (réglage, jeu, retard…).</summary>
        public string Contexte = "";

        private readonly float[] anneauSans, anneauAvec;
        private readonly int capacite;              // images
        private long poussees, ecrites;             // images confiées par le fil du son / écrites sur le disque
        private long perdues;                       // images jamais écrites (disque bloqué plus longtemps que l'anneau)
        private readonly List<Moment> moments = new List<Moment>();
        private int tics, ennemis;
        private volatile bool fin;
        private readonly Thread ecrivain;
        private readonly ManualResetEventSlim travail = new ManualResetEventSlim(false);
        private readonly DateTime debut = DateTime.Now;
        /// <summary>Fin du travail : « ok|dossier », « erreur|message » ; null tant que les fichiers ne sont pas fermés.</summary>
        public volatile string Resultat;
        /// <summary>Appelé (par le fil d'écriture) quand tout est écrit, avec le résultat.</summary>
        public Action<string> QuandFini;

        public EnregistrementLong(string dossier, int canaux, int frequence, int secondes, int secondesAnneau = 8)
        {
            Canaux = Math.Max(1, canaux); Frequence = Math.Max(8000, frequence);
            ImagesMax = (long)Frequence * Math.Max(1, secondes);
            capacite = Math.Max(Frequence / 2, Math.Min(Frequence * Math.Max(1, secondesAnneau), 4000000 / Canaux));
            anneauSans = new float[(long)capacite * Canaux]; anneauAvec = new float[(long)capacite * Canaux];
            Dossier = dossier;
            ecrivain = new Thread(Ecrire) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Velox enregistrement long" };
            ecrivain.Start();
        }

        /// <summary>Images enregistrées (position actuelle dans les fichiers).</summary>
        public long Images { get { return Interlocked.Read(ref poussees); } }
        public int Secondes { get { return (int)(Images / Frequence); } }
        public int SecondesMax { get { return (int)(ImagesMax / Frequence); } }
        public int Tics { get { return Volatile.Read(ref tics); } }
        public int Ennemis { get { return Volatile.Read(ref ennemis); } }
        public long Perdues { get { return Interlocked.Read(ref perdues); } }
        /// <summary>Vrai dès que l'enregistrement est arrêté (durée atteinte ou demande) : plus rien n'est ajouté.</summary>
        public bool Fini { get { return fin; } }

        /// <summary>
        /// (fil du son) Ajoute un paquet : le son reçu du jeu (« sans ») et le son traité par Velox (« avec »), entrelacés.
        /// Jamais bloquant. Arrête l'enregistrement tout seul une fois la durée atteinte.
        /// </summary>
        public void Ajouter(float[] sans, float[] avec, int images)
        {
            if (fin || images <= 0) return;
            long p = poussees;
            images = (int)Math.Min(images, ImagesMax - p);
            if (images <= 0) { Finir(); return; }
            if (p + images - Interlocked.Read(ref ecrites) > capacite)
            {
                // disque bloqué : ce paquet ne rentre pas, il est noté (le son du casque n'attend jamais le disque)
                Interlocked.Add(ref perdues, images); Noter("perte enregistrement");
                return;
            }
            int debutAnneau = (int)(p % capacite);
            int premier = Math.Min(images, capacite - debutAnneau);
            Array.Copy(sans, 0, anneauSans, (long)debutAnneau * Canaux, (long)premier * Canaux);
            Array.Copy(avec, 0, anneauAvec, (long)debutAnneau * Canaux, (long)premier * Canaux);
            if (premier < images)
            {
                Array.Copy(sans, (long)premier * Canaux, anneauSans, 0, (long)(images - premier) * Canaux);
                Array.Copy(avec, (long)premier * Canaux, anneauAvec, 0, (long)(images - premier) * Canaux);
            }
            Interlocked.Exchange(ref poussees, p + images);
            travail.Set();
            if (p + images >= ImagesMax) Finir();
        }

        /// <summary>(n'importe quel fil) Note un marqueur du joueur (« tic », « ennemi ») ou un évènement, à la position actuelle.</summary>
        public void Noter(string type)
        {
            if (fin) return;
            lock (moments)
            {
                // un même évènement répété dans la même demi-seconde n'est noté qu'une fois (un manque en entraîne souvent d'autres)
                if (type != "tic" && type != "ennemi")
                    for (int i = moments.Count - 1; i >= 0 && Images - moments[i].Image < Frequence / 2; i--)
                        if (moments[i].Type == type) return;
                moments.Add(new Moment { Image = Images, Type = type });
            }
            if (type == "tic") Interlocked.Increment(ref tics);
            else if (type == "ennemi") Interlocked.Increment(ref ennemis);
        }

        /// <summary>Arrête l'enregistrement : le fil d'écriture vide l'anneau, ferme les fichiers et écrit les marqueurs.</summary>
        public void Finir() { fin = true; travail.Set(); }

        /// <summary>Attend (au plus « ms ») que tout soit écrit. Vrai si c'est fait.</summary>
        public bool Attendre(int ms) { return ecrivain.Join(ms); }

        public List<Moment> Moments() { lock (moments) return new List<Moment>(moments); }

        public string CheminSans { get { return Path.Combine(Dossier, "sans-velox.wav"); } }
        public string CheminAvec { get { return Path.Combine(Dossier, "avec-velox.wav"); } }
        public string CheminMarqueurs { get { return Path.Combine(Dossier, "marqueurs.txt"); } }
        public string CheminResume { get { return Path.Combine(Dossier, "resume.txt"); } }

        private void Ecrire()
        {
            string res;
            try
            {
                Directory.CreateDirectory(Dossier);   // ici, pas dans le constructeur : jamais de disque dans le fil du son
                using (var fs = new FileStream(CheminSans, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16))
                using (var fa = new FileStream(CheminAvec, FileMode.Create, FileAccess.Write, FileShare.Read, 1 << 16))
                {
                    EnTete(fs, 0); EnTete(fa, 0);
                    var octets = new byte[Frequence / 10 * Canaux * 2];
                    while (true)
                    {
                        bool arrete = fin;   // lu AVANT les images : tout ce qui a été poussé avant l'arrêt est écrit
                        long dispo = Interlocked.Read(ref poussees) - ecrites;
                        if (dispo <= 0)
                        {
                            if (arrete) break;
                            travail.Wait(200); travail.Reset();
                            continue;
                        }
                        int n = (int)Math.Min(dispo, octets.Length / (Canaux * 2));
                        n = Math.Min(n, capacite - (int)(ecrites % capacite));   // jusqu'au bout de l'anneau, le reste au tour suivant
                        long a = (ecrites % capacite) * Canaux;
                        Convertir(anneauSans, a, n * Canaux, octets); fs.Write(octets, 0, n * Canaux * 2);
                        Convertir(anneauAvec, a, n * Canaux, octets); fa.Write(octets, 0, n * Canaux * 2);
                        Interlocked.Add(ref ecrites, n);
                    }
                    long donnees = ecrites * Canaux * 2;
                    fs.Seek(0, SeekOrigin.Begin); EnTete(fs, donnees);
                    fa.Seek(0, SeekOrigin.Begin); EnTete(fa, donnees);
                }
                var liste = Moments();
                File.WriteAllText(CheminMarqueurs, Etiquettes(liste, Frequence), new UTF8Encoding(false));
                File.WriteAllText(CheminResume, Resume(liste), new UTF8Encoding(false));
                res = "ok|" + Dossier;
            }
            catch (Exception ex) { res = "erreur|" + ex.Message.Replace('|', ' ').Replace('\n', ' ').Replace('\r', ' '); }
            Resultat = res;
            try { if (QuandFini != null) QuandFini(res); } catch { }
        }

        private static void Convertir(float[] src, long debut, int n, byte[] dst)
        {
            for (int i = 0; i < n; i++)
            {
                float v = src[debut + i];
                if (float.IsNaN(v)) v = 0;
                short s = (short)Math.Round(Math.Max(-1f, Math.Min(1f, v)) * 32767);
                dst[2 * i] = (byte)s; dst[2 * i + 1] = (byte)(s >> 8);
            }
        }

        /// <summary>En-tête WAV 16 bits (44 octets) ; réécrit à la fin avec la vraie taille.</summary>
        private void EnTete(Stream f, long donnees)
        {
            var w = new BinaryWriter(f, Encoding.ASCII, true);
            int d = (int)Math.Min(int.MaxValue - 36, donnees);
            w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + d); w.Write(Encoding.ASCII.GetBytes("WAVE"));
            w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16); w.Write((short)1); w.Write((short)Canaux); w.Write(Frequence);
            w.Write(Frequence * Canaux * 2); w.Write((short)(Canaux * 2)); w.Write((short)16);
            w.Write(Encoding.ASCII.GetBytes("data")); w.Write(d);
            w.Flush();
        }

        /// <summary>Libellé lisible d'un moment.</summary>
        internal static string Libelle(string type)
        {
            switch (type)
            {
                case "tic": return "TIC (F9)";
                case "ennemi": return "ENNEMI RATE (F10)";
                default: return type;
            }
        }

        /// <summary>
        /// Marqueurs au format des étiquettes d'Audacity (Fichier > Importer > Étiquettes) : « début [tab] fin [tab] nom »,
        /// en secondes avec un point. Ouvre « sans-velox.wav » ou « avec-velox.wav », importe ce fichier : chaque marqueur
        /// s'affiche à son instant.
        /// </summary>
        internal static string Etiquettes(List<Moment> liste, int frequence)
        {
            var c = CultureInfo.InvariantCulture; var sb = new StringBuilder();
            foreach (var m in liste)
            {
                string s = (m.Image / (double)frequence).ToString("F3", c);
                sb.Append(s).Append('\t').Append(s).Append('\t').Append(Libelle(m.Type)).Append('\n');
            }
            return sb.ToString();
        }

        internal static string Temps(long image, int frequence)
        {
            double s = image / (double)frequence;
            int min = (int)(s / 60);
            return min.ToString("D2", CultureInfo.InvariantCulture) + ":" + (s - 60 * min).ToString("00.000", CultureInfo.InvariantCulture);
        }

        private string Resume(List<Moment> liste)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Velox - enregistrement long avec marqueurs");
            sb.AppendLine("Début : " + debut.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            sb.AppendLine("Durée : " + Temps(ecrites, Frequence) + " (" + Canaux + " voies, " + Frequence + " Hz, 16 bits)");
            if (Contexte != "") sb.AppendLine(Contexte.TrimEnd());
            sb.AppendLine("Marqueurs : " + Tics + " tic(s) (F9), " + Ennemis + " ennemi(s) raté(s) (F10)");
            if (Perdues > 0) sb.AppendLine("Son non enregistré (disque trop lent) : " + Temps(Perdues, Frequence));
            sb.AppendLine();
            sb.AppendLine("Fichiers : sans-velox.wav (son reçu du jeu), avec-velox.wav (son traité, avant le volume de Windows),");
            sb.AppendLine("marqueurs.txt (étiquettes Audacity : Fichier > Importer > Étiquettes).");
            sb.AppendLine("Un marqueur est noté quand la touche est enfoncée : le son en cause est juste AVANT (temps de réaction).");
            sb.AppendLine("Évènements du moteur : « manque casque » = le casque n'avait plus de son (craquement possible) ;");
            sb.AppendLine("« son perdu casque » = tampon du casque plein, un bout de son jeté ; « trou capture » = Windows signale");
            sb.AppendLine("un trou dans le son du câble ; « réglage » = réglage changé pendant l'enregistrement.");
            sb.AppendLine();
            foreach (var m in liste) sb.AppendLine(Temps(m.Image, Frequence) + "  " + Libelle(m.Type));
            return sb.ToString();
        }
    }
}
