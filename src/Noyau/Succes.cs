// Nettoyage PC - niveaux et succès (profil Gamer, pour le plaisir) : de l'expérience (XP) gagnée en entretenant le PC,
// un niveau, et des succès débloqués. Calculé seulement à partir de ce que l'application a déjà noté (nettoyages,
// indices, parties en mode jeu) : rien n'est envoyé, rien n'est modifié.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal class SuccesJeu
    {
        public string Icone = "", Nom = "", Condition = "";
        public bool Obtenu;
    }

    internal class ProgressionJeu
    {
        public int Xp, Niveau, XpNiveau, XpSuivant;
        public List<SuccesJeu> Succes = new List<SuccesJeu>();
        public double Avancement { get { return XpSuivant > XpNiveau ? (double)(Xp - XpNiveau) / (XpSuivant - XpNiveau) : 1; } }
        public string Titre
        {
            get
            {
                return Niveau >= 30 ? "Légende du PC" : Niveau >= 20 ? "Maître du nettoyage" : Niveau >= 12 ? "Pro de l'optimisation" : Niveau >= 6 ? "Technicien confirmé" : Niveau >= 3 ? "Apprenti technicien" : "Recrue";
            }
        }
    }

    internal static class Succes
    {
        /// <summary>XP nécessaire pour atteindre un niveau (courbe douce : 0, 100, 300, 600, 1000...).</summary>
        public static int XpPourNiveau(int n) { return 50 * (n - 1) * n; }

        public static ProgressionJeu Calculer()
        {
            var p = new ProgressionJeu();
            long octets = 0; int nettoyages = 0; int semainesPropres = 0;
            var datesNettoyage = new List<DateTime>();
            try
            {
                string f = Chemins.Fichier("statistiques.csv");
                if (File.Exists(f))
                    foreach (var l in File.ReadAllLines(f))
                    {
                        var c = l.Split(';'); DateTime d; long o;
                        if (c.Length >= 2 && DateTime.TryParseExact(c[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && long.TryParse(c[1].Trim(), out o) && o >= 0)
                        { octets += o; nettoyages++; datesNettoyage.Add(d); }
                    }
            }
            catch { }
            // semaines (lundi) où au moins un nettoyage a eu lieu
            semainesPropres = datesNettoyage.Select(d => d.Date.AddDays(-(((int)d.DayOfWeek + 6) % 7))).Distinct().Count();
            int meilleurIndice = 0;
            try
            {
                string f = Chemins.Fichier("scores.csv");
                if (File.Exists(f))
                    foreach (var l in File.ReadAllLines(f)) { var c = l.Split(';'); int s; if (c.Length >= 2 && int.TryParse(c[1].Trim(), out s)) meilleurIndice = Math.Max(meilleurIndice, s); }
            }
            catch { }
            int parties = 0;
            try { parties = ModeJeuAuto.Sessions().Count; } catch { }
            int meilleurGain = 0;
            try { meilleurGain = AvantApres.Liste().Select(c => c.Gain).DefaultIfEmpty(0).Max(); } catch { }

            // XP : 10 par nettoyage, 1 par 100 Mo libérés, 5 par partie, 20 par semaine entretenue, bonus pour un bon indice
            long xp = nettoyages * 10L + octets / (100L * 1024 * 1024) + parties * 5L + semainesPropres * 20L + (meilleurIndice >= 90 ? 100 : meilleurIndice >= 80 ? 50 : 0);
            p.Xp = (int)Math.Min(int.MaxValue, xp);
            p.Niveau = 1;
            while (XpPourNiveau(p.Niveau + 1) <= p.Xp && p.Niveau < 99) p.Niveau++;
            p.XpNiveau = XpPourNiveau(p.Niveau); p.XpSuivant = XpPourNiveau(p.Niveau + 1);

            Action<string, string, string, bool> s = (icone, nom, cond, ok) => p.Succes.Add(new SuccesJeu { Icone = icone, Nom = nom, Condition = cond, Obtenu = ok });
            const long Go = 1073741824L;
            s("◆", "Premier coup de balai", "Faire un premier nettoyage", nettoyages >= 1);
            s("◆", "Un giga de gagné", "Libérer 1 Go au total", octets >= Go);
            s("◆", "Dix gigas", "Libérer 10 Go au total", octets >= 10 * Go);
            s("◆", "Centurion", "Libérer 100 Go au total", octets >= 100 * Go);
            s("◆", "Habitué", "Faire 10 nettoyages", nettoyages >= 10);
            s("◆", "Régulier", "Entretenir le PC 4 semaines différentes", semainesPropres >= 4);
            s("◆", "En forme", "Atteindre un indice de 80", meilleurIndice >= 80);
            s("◆", "Machine de guerre", "Atteindre un indice de 90", meilleurIndice >= 90);
            s("◆", "Coup de boost", "Gagner 10 points d'indice en un nettoyage", meilleurGain >= 10);
            s("◆", "Joueur", "Jouer une partie en mode jeu automatique", parties >= 1);
            s("◆", "Marathonien", "Jouer 25 parties en mode jeu automatique", parties >= 25);
            return p;
        }
    }
}
