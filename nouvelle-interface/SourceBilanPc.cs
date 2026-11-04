// Velox 5 - vrais relevés du PC pour l'accueil, par le moteur partagé avec la version 4
// (BilanPc : mêmes vérifications, même note sur 100, mêmes secteurs). Lecture seule : rien n'est modifié.
using System;
using System.IO;
using System.Linq;

namespace NettoyagePC.Nouveau
{
    public sealed class SourceBilanPc : ISourceBilan
    {
        public bool Immediat { get { return false; } }

        public Bilan Calculer()
        {
            var d = BilanPc.Collecter();
            var points = BilanPc.Points(d);
            var b = new Bilan
            {
                Score = BilanPc.Score(points.Select(p => p.Etat)),
                Secteurs = BilanPc.Secteurs(d),
                NomDisque = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:",
                Date = DateTime.Now,
            };
            var ordre = new System.Collections.Generic.Dictionary<string, int> { { "probleme", 0 }, { "attention", 1 }, { "ok", 2 } };
            foreach (var p in points.OrderBy(x => ordre.ContainsKey(x.Etat) ? ordre[x.Etat] : 3))
            {
                var etat = p.Etat == "probleme" ? EtatPoint.Probleme : p.Etat == "attention" ? EtatPoint.Attention : EtatPoint.Ok;
                b.Points.Add(new PointBilan(etat, Langue.T(p.Texte), p.Bouton == null ? null : Langue.T(p.Bouton), p.Cible));
            }
            var m = Mesurer(true);
            if (m != null) { b.Processeur = m.Processeur; b.Memoire = m.Memoire; b.Temperature = m.Temperature; b.DisqueLibre = m.DisqueLibre; b.DisquePourcentLibre = m.DisquePourcentLibre; }
            try { FichierSur.AjouterLigne(Chemins.Fichier("scores.csv"), DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ";" + b.Score); } catch { }
            return b;
        }

        private string sourceTemperature;

        private readonly object verrou = new object();
        private Mesures derniere;
        private DateTime dateDerniere;

        /// <summary>
        /// Mesures en direct. L'accueil, le moniteur et le widget les demandent en même temps : une seule mesure à la fois
        /// (le calcul du processeur compare deux relevés successifs), et la dernière est réutilisée pendant 0,8 seconde.
        /// </summary>
        public Mesures Mesurer(bool avecTemperature)
        {
            lock (verrou)
            {
                if (derniere != null && (DateTime.Now - dateDerniere).TotalMilliseconds < 800 && (!avecTemperature || derniere.Temperature > 0 || sourceTemperature == "aucune"))
                    return Copie(derniere);
                var m = MesurerSansCache(avecTemperature);
                if (!avecTemperature && derniere != null) { m.Temperature = derniere.Temperature; m.TitreTemperature = derniere.TitreTemperature; }
                derniere = m; dateDerniere = DateTime.Now;
                return Copie(m);
            }
        }

        private static Mesures Copie(Mesures m)
        {
            return new Mesures { Processeur = m.Processeur, Memoire = m.Memoire, Temperature = m.Temperature, DisqueLibre = m.DisqueLibre, DisquePourcentLibre = m.DisquePourcentLibre, TitreTemperature = m.TitreTemperature };
        }

        private Mesures MesurerSansCache(bool avecTemperature)
        {
            var m = new Mesures();
            try { m.Processeur = Natif.Processeur(); } catch { }
            try
            {
                ulong total, dispo;
                Natif.Memoire(out total, out dispo);
                if (total > 0) m.Memoire = 100.0 * (total - dispo) / total;
            }
            catch { }
            try
            {
                var dq = new DriveInfo(Environment.GetEnvironmentVariable("SystemDrive") ?? Path.GetPathRoot(Chemins.Windows));
                if (dq.IsReady && dq.TotalSize > 0) { m.DisqueLibre = Format.Taille(dq.AvailableFreeSpace) + " libres"; m.DisquePourcentLibre = 100.0 * dq.AvailableFreeSpace / dq.TotalSize; }
            }
            catch { }
            if (avecTemperature)
            {
                try
                {
                    string source;
                    m.Temperature = InfosPc.Temperature(out source);
                    sourceTemperature = m.Temperature > 0 ? source : "aucune";
                }
                catch { }
            }
            m.TitreTemperature = sourceTemperature == "gpu" ? "CARTE GRAPHIQUE" : "TEMPÉRATURE";
            return m;
        }
    }
}
