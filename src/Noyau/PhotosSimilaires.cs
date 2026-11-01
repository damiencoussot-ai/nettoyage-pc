// Nettoyage PC - photos en double et presque identiques (Premium) : rafales, copies redimensionnées ou un peu retouchées.
// Chaque photo est réduite à une petite empreinte visuelle (9 × 8 points en niveaux de gris) ; deux photos dont les
// empreintes diffèrent de très peu se ressemblent. Rien n'est supprimé ici : la page propose, toi tu choisis, et les
// photos choisies vont dans la Corbeille (récupérables). Sans interface : partagé v4 / v5.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace NettoyagePC
{
    internal class PhotoSimilaire
    {
        public string Chemin = "";
        public long Taille;
        public int Largeur, Hauteur;
        public DateTime Modif;
        public ulong Empreinte;
        /// <summary>Coché = à mettre à la Corbeille (proposition : tout sauf la meilleure).</summary>
        public bool Supprimer;
        public string Nom { get { return Path.GetFileName(Chemin); } }
        public string Dossier { get { return Path.GetDirectoryName(Chemin) ?? ""; } }
        public long Pixels { get { return (long)Largeur * Hauteur; } }
    }

    internal class GroupePhotos
    {
        public List<PhotoSimilaire> Photos = new List<PhotoSimilaire>();
        public bool Identiques;   // même empreinte exacte
        public long Recuperable { get { return Photos.Where(p => p.Supprimer).Sum(p => p.Taille); } }
    }

    internal class EtatPhotos
    {
        public volatile int Total, Faites;
        public volatile bool Annuler;
    }

    internal static class PhotosSimilaires
    {
        public static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff" };
        /// <summary>Différence maximale entre deux empreintes (sur 64) pour dire « presque identiques ».</summary>
        public const int Seuil = 5;

        public static List<string> Lister(string racine, EtatPhotos e)
        {
            var l = new List<string>();
            var pile = new Stack<string>(); pile.Push(racine);
            while (pile.Count > 0 && !e.Annuler)
            {
                string d = pile.Pop();
                try
                {
                    var info = new DirectoryInfo(d);
                    if ((info.Attributes & FileAttributes.ReparsePoint) != 0 && d != racine) continue;
                    foreach (var f in info.GetFiles())
                    {
                        // fichiers OneDrive « en ligne seulement » : jamais téléchargés pour rien
                        if ((f.Attributes & (FileAttributes)0x00400000) != 0 || (f.Attributes & FileAttributes.Offline) != 0) continue;
                        if (Extensions.Contains(f.Extension.ToLowerInvariant()) && f.Length >= 20 * 1024) l.Add(f.FullName);
                    }
                    foreach (var s in info.GetDirectories()) if ((s.Attributes & (FileAttributes.Hidden | FileAttributes.System)) == 0) pile.Push(s.FullName);
                }
                catch { }
            }
            return l;
        }

        /// <summary>Empreinte visuelle (dHash 64 bits) et dimensions. false si l'image ne peut pas être lue.</summary>
        public static bool Analyser(string chemin, out ulong empreinte, out int largeur, out int hauteur)
        {
            empreinte = 0; largeur = hauteur = 0;
            try
            {
                using (var fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var img = Image.FromStream(fs, false, false))
                {
                    largeur = img.Width; hauteur = img.Height;
                    using (var petit = new Bitmap(9, 8, PixelFormat.Format24bppRgb))
                    {
                        using (var g = Graphics.FromImage(petit))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                            g.DrawImage(img, 0, 0, 9, 8);
                        }
                        ulong h = 0; int bit = 0;
                        for (int y = 0; y < 8; y++)
                            for (int x = 0; x < 8; x++)
                            {
                                if (Gris(petit.GetPixel(x, y)) > Gris(petit.GetPixel(x + 1, y))) h |= 1UL << bit;
                                bit++;
                            }
                        empreinte = h;
                    }
                }
                return true;
            }
            catch { return false; }
        }

        private static int Gris(Color c) { return (c.R * 299 + c.G * 587 + c.B * 114) / 1000; }

        public static int Distance(ulong a, ulong b)
        {
            ulong x = a ^ b; int n = 0;
            while (x != 0) { x &= x - 1; n++; }
            return n;
        }

        /// <summary>Recherche complète (bloquante) : liste, empreintes (en parallèle), groupes, proposition.</summary>
        public static List<GroupePhotos> Chercher(string racine, EtatPhotos e)
        {
            var fichiers = Lister(racine, e);
            e.Total = fichiers.Count;
            var photos = new ConcurrentBag<PhotoSimilaire>();
            var opts = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Math.Min(4, Environment.ProcessorCount - 1)) };
            Parallel.ForEach(fichiers, opts, (f, etat) =>
            {
                if (e.Annuler) { etat.Stop(); return; }
                ulong h; int w, ht;
                if (Analyser(f, out h, out w, out ht) && w >= 64 && ht >= 64)
                {
                    long taille = 0; DateTime m = DateTime.MinValue;
                    try { var fi = new FileInfo(f); taille = fi.Length; m = fi.LastWriteTime; } catch { }
                    photos.Add(new PhotoSimilaire { Chemin = f, Empreinte = h, Largeur = w, Hauteur = ht, Taille = taille, Modif = m });
                }
                Interlocked.Increment(ref e.Faites);
            });
            return Grouper(photos.ToList());
        }

        /// <summary>Regroupe les photos proches (même forme d'image, empreintes à {Seuil} près) et propose quoi garder.</summary>
        public static List<GroupePhotos> Grouper(List<PhotoSimilaire> photos)
        {
            int n = photos.Count;
            var parent = Enumerable.Range(0, n).ToArray();
            Func<int, int> racine = null;
            racine = i => { while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; } return i; };
            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    if (Distance(photos[i].Empreinte, photos[j].Empreinte) > Seuil) continue;
                    // même orientation et proportions proches (une photo et son recadrage très différent ne sont pas groupés)
                    double ri = (double)photos[i].Largeur / photos[i].Hauteur, rj = (double)photos[j].Largeur / photos[j].Hauteur;
                    if (Math.Abs(ri - rj) > 0.08 * Math.Max(ri, rj)) continue;
                    int a = racine(i), b = racine(j); if (a != b) parent[a] = b;
                }
            var groupes = Enumerable.Range(0, n).GroupBy(racine).Where(g => g.Count() > 1)
                .Select(g => new GroupePhotos { Photos = g.Select(i => photos[i]).ToList() }).ToList();
            foreach (var g in groupes)
            {
                g.Identiques = g.Photos.All(p => p.Empreinte == g.Photos[0].Empreinte);
                Proposer(g);
            }
            return groupes.OrderByDescending(g => g.Recuperable).ToList();
        }

        /// <summary>Garde la meilleure : plus grande définition, puis plus gros fichier (moins compressé), puis la plus ancienne.</summary>
        public static void Proposer(GroupePhotos g)
        {
            var meilleure = g.Photos.OrderByDescending(p => p.Pixels).ThenByDescending(p => p.Taille).ThenBy(p => p.Modif).First();
            foreach (var p in g.Photos) p.Supprimer = p != meilleure;
        }

        /// <summary>Met les photos cochées à la Corbeille (jamais toutes celles d'un groupe). Retourne (nombre, octets).</summary>
        public static Tuple<int, long> MettreACorbeille(List<GroupePhotos> groupes)
        {
            int n = 0; long o = 0;
            foreach (var g in groupes)
            {
                if (g.Photos.All(p => p.Supprimer)) continue;   // sécurité : au moins une photo reste toujours
                foreach (var p in g.Photos.Where(x => x.Supprimer))
                {
                    try { if (File.Exists(p.Chemin) && Corbeille.Envoyer(p.Chemin)) { n++; o += p.Taille; } } catch { }
                }
            }
            return Tuple.Create(n, o);
        }
    }
}
