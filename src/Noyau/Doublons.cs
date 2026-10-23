// Nettoyage PC - doublons : recherche par empreinte SHA-256, proposition de la copie à garder, mise à la corbeille
// revérifiée. Partagé par la version 4 et la version 5. Rien n'est supprimé définitivement : tout va à la corbeille.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;

namespace NettoyagePC
{
    internal class CopieDoublon { public string Chemin, Nom, Dossier, Raison = ""; public DateTime Modif, Creation; public bool Supprimer; }
    internal class GroupeDoublons { public string Hash; public long Taille; public List<CopieDoublon> Fichiers; public bool Actif; public string Proposition = ""; }

    /// <summary>Lot à mettre à la corbeille : la copie gardée, son empreinte, et les copies à retirer.</summary>
    internal class LotDoublons { public string Garde, Hash; public long Taille; public List<string> Supprimer; }

    /// <summary>Avancement (lu par l'interface pendant la recherche) et demande d'arrêt.</summary>
    internal class EtatDoublons
    {
        public volatile bool Arret;
        public int Fichiers, Fait, Total;
        public string Phase = "";
    }

    internal static class RechercheDoublons
    {
        /// <summary>Recherche : même taille, puis même début (64 Ko), puis même contenu (SHA-256 complet). Null si arrêtée.</summary>
        public static List<GroupeDoublons> Chercher(List<string> racines, long seuilOctets, EtatDoublons e)
        {
            var exclus = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "AppData", "$Recycle.Bin", "System Volume Information", "node_modules", ".git", "Program Files", "Program Files (x86)", "ProgramData", "Windows" };
            var parTaille = new Dictionary<long, List<FileInfo>>();
            var vus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pile = new Stack<DirectoryInfo>();
            foreach (var r in racines) pile.Push(new DirectoryInfo(r));
            while (pile.Count > 0)
            {
                if (e.Arret) return null;
                var d = pile.Pop();
                DirectoryInfo[] sous = new DirectoryInfo[0]; FileInfo[] fichiers = new FileInfo[0];
                try { sous = d.GetDirectories(); } catch { }
                foreach (var s in sous) { if (((int)s.Attributes & 0x400) != 0 || exclus.Contains(s.Name)) continue; pile.Push(s); }
                try { fichiers = d.GetFiles(); } catch { }
                foreach (var f in fichiers)
                {
                    long l; try { l = f.Length; } catch { continue; }
                    if (l < seuilOctets || l <= 0 || ((int)f.Attributes & 0x441404) != 0 || !vus.Add(f.FullName)) continue;
                    Interlocked.Increment(ref e.Fichiers);
                    List<FileInfo> lot; if (!parTaille.TryGetValue(l, out lot)) parTaille[l] = lot = new List<FileInfo>();
                    lot.Add(f);
                }
            }
            e.Total = parTaille.Values.Where(x => x.Count >= 2).Sum(x => x.Count);
            e.Phase = "comparaison";
            var res = new List<GroupeDoublons>();
            using (var sha = SHA256.Create())
            {
                var tampon = new byte[65536];
                Func<string, bool, string> empreinte = (chemin, complet) =>
                {
                    try
                    {
                        using (var fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            if (complet) return BitConverter.ToString(sha.ComputeHash(fs));
                            int n = fs.Read(tampon, 0, tampon.Length);
                            return BitConverter.ToString(sha.ComputeHash(tampon, 0, n));
                        }
                    }
                    catch { return null; }
                };
                foreach (var lot in parTaille.Values.Where(x => x.Count >= 2))
                {
                    var parDebut = new Dictionary<string, List<FileInfo>>();
                    foreach (var f in lot)
                    {
                        if (e.Arret) return null;
                        Interlocked.Increment(ref e.Fait);
                        string h = empreinte(f.FullName, false); if (h == null) continue;
                        List<FileInfo> l; if (!parDebut.TryGetValue(h, out l)) parDebut[h] = l = new List<FileInfo>();
                        l.Add(f);
                    }
                    foreach (var sl in parDebut.Values.Where(x => x.Count >= 2))
                    {
                        var parContenu = new Dictionary<string, List<FileInfo>>();
                        foreach (var f in sl)
                        {
                            if (e.Arret) return null;
                            string h = empreinte(f.FullName, true); if (h == null) continue;
                            List<FileInfo> l; if (!parContenu.TryGetValue(h, out l)) parContenu[h] = l = new List<FileInfo>();
                            l.Add(f);
                        }
                        foreach (var kv in parContenu.Where(x => x.Value.Count >= 2))
                            res.Add(new GroupeDoublons
                            {
                                Hash = kv.Key, Taille = kv.Value[0].Length,
                                Fichiers = kv.Value.Select(f => new CopieDoublon { Chemin = f.FullName, Nom = f.Name, Dossier = f.DirectoryName, Modif = f.LastWriteTime, Creation = f.CreationTime }).ToList()
                            });
                    }
                }
            }
            return res.OrderByDescending(g => g.Taille * (g.Fichiers.Count - 1)).ToList();
        }

        // ---- proposition intelligente : quelle copie garder ?
        private static int Penalite(CopieDoublon f, List<string> raisons, out bool sauvegarde)
        {
            int pen = 0;
            string nom = Path.GetFileNameWithoutExtension(f.Nom);
            if (Regex.IsMatch(nom, @"\s?\(\d+\)$") || Regex.IsMatch(nom, @"\s?\[\d+\]$")) { pen += 30; raisons.Add("nom en « (1) »"); }
            if (Regex.IsMatch(nom, @"(?i)(^|[\s_-])(copie|copy)([\s_-]?\d+)?$") || Regex.IsMatch(nom, @"(?i)^(copie de|copy of)\s")) { pen += 30; raisons.Add("nom « copie »"); }
            if (f.Chemin.StartsWith(Chemins.Telechargements.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)) { pen += 20; raisons.Add("dans Téléchargements"); }
            if (Regex.IsMatch(f.Chemin, @"(?i)\\_A_verifier\\")) { pen += 15; raisons.Add("dans _A_verifier"); }
            if (Regex.IsMatch(f.Chemin, @"(?i)\\(temp|tmp|cache)\\")) { pen += 25; raisons.Add("dossier temporaire"); }
            sauvegarde = Regex.IsMatch(f.Chemin, @"(?i)\\(sauvegardes?|backups?|NettoyagePC-Sauvegarde)[^\\]*\\");
            return pen;
        }

        public static void Proposer(GroupeDoublons g)
        {
            var notes = new Dictionary<CopieDoublon, Tuple<int, List<string>, bool>>();
            foreach (var f in g.Fichiers) { var r = new List<string>(); bool sv; int p = Penalite(f, r, out sv); notes[f] = Tuple.Create(p, r, sv); }
            // on garde la copie la moins suspecte, puis la plus ancienne, puis le chemin le plus court
            var ordre = g.Fichiers.OrderBy(f => notes[f].Item1).ThenBy(f => f.Creation).ThenBy(f => f.Chemin.Length).ToList();
            var garde = ordre[0];
            garde.Supprimer = false; garde.Raison = "À garder : original probable";
            if (ordre.Count > 1 && notes[garde].Item1 < notes[ordre[1]].Item1) garde.Raison = "À garder : nom et emplacement d'origine";
            else if (ordre.Count > 1 && garde.Creation < ordre[1].Creation) garde.Raison = "À garder : la plus ancienne";
            int nb = 0, sv2 = 0;
            foreach (var f in ordre.Skip(1))
            {
                var n = notes[f];
                if (n.Item3) { f.Supprimer = false; f.Raison = "Gardée : copie de sauvegarde volontaire"; sv2++; }
                else { f.Supprimer = true; f.Raison = n.Item2.Count > 0 ? "Copie : " + string.Join(", ", n.Item2) : "Copie : créée plus tard"; nb++; }
            }
            g.Actif = nb > 0;
            g.Proposition = nb > 0 ? "Garder 1, mettre " + nb + " copie(s) à la corbeille" : "Tout garder (copies de sauvegarde)";
            if (sv2 > 0 && nb > 0) g.Proposition += " ; " + sv2 + " sauvegarde(s) gardée(s)";
        }

        public static void ToutGarder(GroupeDoublons g)
        {
            foreach (var f in g.Fichiers) { f.Supprimer = false; f.Raison = "Gardée"; }
            g.Actif = false; g.Proposition = "Tout garder";
        }

        /// <summary>Chaque copie est revérifiée (même taille, même empreinte que la copie gardée, qui doit exister) avant la corbeille.
        /// Renvoie : mis à la corbeille, non touchés (avec la raison), octets libérés.</summary>
        public static Tuple<List<string>, List<string>, long> MettreACorbeille(List<LotDoublons> lots, EtatDoublons e)
        {
            var faits = new List<string>(); var ignores = new List<string>(); long octets = 0;
            foreach (var lot in lots)
            {
                if (e.Arret) break;
                // la copie gardée doit toujours exister et être identique
                if (!File.Exists(lot.Garde) || Empreinte(lot.Garde) != lot.Hash)
                {
                    foreach (var c in lot.Supprimer) { ignores.Add(c + " (la copie gardée a changé ou a disparu)"); Interlocked.Increment(ref e.Fait); }
                    continue;
                }
                foreach (var c in lot.Supprimer)
                {
                    Interlocked.Increment(ref e.Fait);
                    if (Protection.CheminProtege(c)) { ignores.Add(c + " (dossier protégé)"); continue; }
                    if (!File.Exists(c)) { ignores.Add(c + " (déjà supprimé ou déplacé)"); continue; }
                    long l; try { l = new FileInfo(c).Length; } catch { l = -1; }
                    if (l != lot.Taille || Empreinte(c) != lot.Hash) { ignores.Add(c + " (modifié depuis la recherche)"); continue; }
                    if (Corbeille.Envoyer(c)) { faits.Add(c); octets += lot.Taille; } else ignores.Add(c + " (refusé)");
                }
            }
            return Tuple.Create(faits, ignores, octets);
        }

        public static string Empreinte(string chemin)
        {
            try { using (var sha = SHA256.Create()) using (var fs = new FileStream(chemin, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) return BitConverter.ToString(sha.ComputeHash(fs)); }
            catch { return null; }
        }
    }
}
