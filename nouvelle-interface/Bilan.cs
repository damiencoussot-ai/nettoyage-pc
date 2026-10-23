// Nettoyage PC 5 - données de l'accueil. Pour l'instant : données d'exemple (aperçus, essais) ;
// le moteur de la version 4 (vrais relevés du PC) sera branché ensuite derrière la même interface.
using System;
using System.Collections.Generic;

namespace NettoyagePC.Nouveau
{
    public enum EtatPoint { Ok, Attention, Probleme }

    public sealed class PointBilan
    {
        public EtatPoint Etat;
        public string Texte = "", Bouton;
        public PointBilan(EtatPoint etat, string texte, string bouton = null) { Etat = etat; Texte = texte; Bouton = bouton; }
    }

    public sealed class Bilan
    {
        public int Score;
        /// <summary>Santé des quatre secteurs de l'anneau (0 à 100, -1 si inconnue) : stockage, mémoire, sécurité, matériel.</summary>
        public double[] Secteurs = { -1, -1, -1, -1 };
        public List<PointBilan> Points = new List<PointBilan>();
        public double Processeur = -1, Memoire = -1, Temperature = -1;
        public string DisqueLibre = "";
        public string NomDisque = "C:";
    }

    public interface ISourceBilan { Bilan Calculer(); }

    /// <summary>Bilan d'exemple, réaliste, pour les aperçus et les essais (rien n'est lu sur le PC).</summary>
    public sealed class BilanExemple : ISourceBilan
    {
        public Bilan Calculer()
        {
            var b = new Bilan { Score = 72, Processeur = 9, Memoire = 41, Temperature = 46, DisqueLibre = "27,6 Go libres" };
            b.Secteurs = new double[] { 44, 100, 60, 100 };
            b.Points.Add(new PointBilan(EtatPoint.Attention, "Disque C: 27,6 Go libres (11 %) : il commence à se remplir.", "Nettoyer"));
            b.Points.Add(new PointBilan(EtatPoint.Attention, "1,03 Go récupérables tout de suite (corbeille et fichiers temporaires).", "Nettoyer"));
            b.Points.Add(new PointBilan(EtatPoint.Attention, "Entretien automatique désactivé : ton PC ne se nettoie pas tout seul.", "Activer"));
            b.Points.Add(new PointBilan(EtatPoint.Attention, "AnyDesk est ouvert : il permet de prendre la main sur ton PC.", "Vérifier"));
            b.Points.Add(new PointBilan(EtatPoint.Attention, "Aucun point de restauration : impossible de revenir en arrière en cas de problème.", "Créer"));
            foreach (var t in new[] { "Mémoire vive utilisée à 41 %.", "Disques en bonne santé.", "Antivirus Microsoft Defender actif et à jour.", "Pare-feu Windows actif.", "8 programme(s) au démarrage." })
                b.Points.Add(new PointBilan(EtatPoint.Ok, t));
            return b;
        }
    }

    public static class Textes
    {
        public static string Salut(string pseudo, DateTime maintenant)
        {
            string s = maintenant.Hour >= 18 || maintenant.Hour < 5 ? "Bonsoir" : "Bonjour";
            return string.IsNullOrEmpty(pseudo) ? s + "." : s + ", " + pseudo + ".";
        }

        public static string EtatCourt(Bilan b, out Avalonia.Media.Color couleur)
        {
            int p = 0, a = 0;
            foreach (var x in b.Points) { if (x.Etat == EtatPoint.Probleme) p++; else if (x.Etat == EtatPoint.Attention) a++; }
            if (p > 0) { couleur = Theme.P.Rouge; return "Ton PC a besoin d'attention"; }
            if (a > 0) { couleur = Theme.P.Orange; return "Ton PC peut être amélioré"; }
            couleur = Theme.P.Vert; return "Ton PC est en pleine forme";
        }

        public static string EtatDetail(Bilan b)
        {
            int p = 0, a = 0;
            foreach (var x in b.Points) { if (x.Etat == EtatPoint.Probleme) p++; else if (x.Etat == EtatPoint.Attention) a++; }
            const string fin = " Clique sur « Optimiser en 1 clic » pour tout faire d'un coup, ou choisis ci-dessous.";
            if (p == 0 && a == 0) return "Rien à corriger pour l'instant. L'entretien automatique s'occupe du reste.";
            if (p > 0) return (p == 1 ? "1 point à régler en priorité." : p + " points à régler en priorité.") + fin;
            return (a == 1 ? "1 amélioration possible." : a + " améliorations possibles.") + fin;
        }

        public static string Niveau(int score) { return score >= 85 ? "en pleine forme" : score >= 55 ? "à améliorer" : "à soigner"; }
    }
}
