// Nettoyage PC 5 - données de l'accueil : le bilan (note, secteurs, points à vérifier) et les mesures en direct.
// Deux sources : les vrais relevés du PC (SourceBilanPc, moteur partagé avec la version 4)
// et un bilan d'exemple pour les aperçus et les essais (rien n'est lu sur le PC).
using System;
using System.Collections.Generic;

namespace NettoyagePC.Nouveau
{
    public enum EtatPoint { Ok, Attention, Probleme }

    public sealed class PointBilan
    {
        public EtatPoint Etat;
        public string Texte = "", Bouton;
        /// <summary>Ce que fait le bouton : "action:cle", "page:Cle", "uri:adresse", "code:..." (voir BilanPc du moteur).</summary>
        public string Cible;
        public PointBilan(EtatPoint etat, string texte, string bouton = null, string cible = null) { Etat = etat; Texte = texte; Bouton = bouton; Cible = cible; }
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
        public DateTime Date = DateTime.Now;
    }

    /// <summary>Mesures en direct (rafraîchies toutes les deux secondes à l'accueil).</summary>
    public sealed class Mesures
    {
        public double Processeur = -1, Memoire = -1, Temperature = -1;
        public string DisqueLibre = "", TitreTemperature = "TEMPÉRATURE";
    }

    public interface ISourceBilan
    {
        /// <summary>Vrai si Calculer est instantané (exemple) ; sinon il est appelé en arrière-plan.</summary>
        bool Immediat { get; }
        /// <summary>Bilan complet (quelques secondes sur un vrai PC : à appeler hors du fil de l'interface).</summary>
        Bilan Calculer();
        /// <summary>Mesures en direct ; null si la source n'en a pas.</summary>
        Mesures Mesurer(bool avecTemperature);
    }

    /// <summary>Bilan d'exemple, réaliste, pour les aperçus et les essais (rien n'est lu sur le PC).</summary>
    public sealed class BilanExemple : ISourceBilan
    {
        public bool Immediat { get { return true; } }
        public Mesures Mesurer(bool avecTemperature) { return null; }

        public Bilan Calculer()
        {
            var b = new Bilan { Score = 72, Processeur = 9, Memoire = 41, Temperature = 46, DisqueLibre = "27,6 Go libres" };
            b.Secteurs = new double[] { 44, 100, 60, 100 };
            b.Points.Add(new PointBilan(EtatPoint.Attention, "Disque C: 27,6 Go libres (11 %) : il commence à se remplir.", "Nettoyer", "action:standard"));
            b.Points.Add(new PointBilan(EtatPoint.Attention, "1,03 Go récupérables tout de suite (corbeille et fichiers temporaires).", "Nettoyer", "action:standard"));
            b.Points.Add(new PointBilan(EtatPoint.Attention, "Entretien automatique désactivé : ton PC ne se nettoie pas tout seul.", "Activer", "action:planifier"));
            b.Points.Add(new PointBilan(EtatPoint.Attention, "AnyDesk est ouvert : il permet de prendre la main sur ton PC.", "Vérifier", "code:bouclier"));
            b.Points.Add(new PointBilan(EtatPoint.Attention, "Aucun point de restauration : impossible de revenir en arrière en cas de problème.", "Créer", "action:point"));
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
            if (p > 0) { couleur = Habillage.P.Rouge; return "Ton PC a besoin d'attention"; }
            if (a > 0) { couleur = Habillage.P.Orange; return "Ton PC peut être amélioré"; }
            couleur = Habillage.P.Vert; return "Ton PC est en pleine forme";
        }

        public static string EtatDetail(Bilan b)
        {
            int p = 0, a = 0;
            foreach (var x in b.Points) { if (x.Etat == EtatPoint.Probleme) p++; else if (x.Etat == EtatPoint.Attention) a++; }
            string fin = " " + Langue.T("Clique sur « Optimiser en 1 clic » pour tout faire d'un coup, ou choisis ci-dessous.");
            if (p == 0 && a == 0) return Langue.T("Rien à corriger pour l'instant. L'entretien automatique s'occupe du reste.");
            if (p > 0) return (p == 1 ? Langue.T("1 point à régler en priorité.") : Langue.F("{0} points à régler en priorité.", p)) + fin;
            return (a == 1 ? Langue.T("1 amélioration possible.") : Langue.F("{0} améliorations possibles.", a)) + fin;
        }

        public static string Niveau(int score) { return score >= 85 ? "en pleine forme" : score >= 55 ? "à améliorer" : "à soigner"; }
    }
}
