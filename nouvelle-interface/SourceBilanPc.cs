// Nettoyage PC 5 - vrais relevés du PC pour l'accueil, par le moteur partagé avec la version 4
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
            if (m != null) { b.Processeur = m.Processeur; b.Memoire = m.Memoire; b.Temperature = m.Temperature; b.DisqueLibre = m.DisqueLibre; }
            try { FichierSur.AjouterLigne(Chemins.Fichier("scores.csv"), DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ";" + b.Score); } catch { }
            return b;
        }

        private string sourceTemperature;

        public Mesures Mesurer(bool avecTemperature)
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
                if (dq.IsReady && dq.TotalSize > 0) m.DisqueLibre = Format.Taille(dq.AvailableFreeSpace) + " libres";
            }
            catch { }
            if (avecTemperature)
            {
                try
                {
                    string source;
                    m.Temperature = InfosPc.Temperature(out source);
                    sourceTemperature = source;
                }
                catch { }
            }
            m.TitreTemperature = sourceTemperature == "gpu" ? "CARTE GRAPHIQUE" : "TEMPÉRATURE";
            return m;
        }
    }
}
