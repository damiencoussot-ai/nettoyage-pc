// Nettoyage PC - lecture des résultats de l'analyse des plantages et de l'analyse du démarrage (données envoyées par le
// moteur : « @@DONNEES plantages » et « @@DONNEES demarrage »). Partagé par les deux interfaces.
using System;
using System.Collections.Generic;
using System.Linq;

namespace NettoyagePC
{
    /// <summary>Ligne d'un tableau de résultats. Niveau : 0 normal, 1 à surveiller (orange), 2 grave (rouge), 3 tout va bien (vert).</summary>
    internal class LigneAnalyse
    {
        public string Quoi = "", Type = "", Fois = "", Derniere = "", Detail = "";
        public long Nombre, Valeur;
        public int Niveau;
    }

    internal static class AnalysePlantages
    {
        private static readonly Dictionary<string, string> NomsCategories = new Dictionary<string, string>
        {
            { "pilote", "Pilote" }, { "memoire", "Mémoire vive" }, { "disque", "Disque" }, { "graphique", "Carte graphique" }, { "materiel", "Matériel" }, { "systeme", "Windows" }
        };

        public static string Date(string d) { DateTime x; return DateTime.TryParse(d, out x) ? x.ToString("dd/MM/yyyy HH:mm") : d; }

        private static List<Dictionary<string, object>> Liste(Dictionary<string, object> d, string cle) { return Json.Objets(Json.Valeur(d, cle)); }

        /// <summary>Les quatre chiffres clés : écrans bleus, arrêts brutaux, erreurs matérielles ou de disque, applications. niveaux : 3 vert, 1 orange, 2 rouge.</summary>
        public static long[] Chiffres(Dictionary<string, object> d, out int[] niveaux)
        {
            var ecrans = Liste(d, "Ecrans"); var arrets = Liste(d, "Arrets"); var materiel = Liste(d, "Materiel"); var disques = Liste(d, "Disques"); var applis = Liste(d, "Applications");
            long affichage = Json.Long(d, "Affichage");
            int coupures = arrets.Count(a => Json.Texte(a, "Type") != "ecran-bleu");
            long nbMat = materiel.Sum(m => Json.Long(m, "Fois")) + disques.Sum(m => Json.Long(m, "Fois")) + affichage;
            niveaux = new[]
            {
                ecrans.Count > 0 ? 2 : 3, coupures > 0 ? 1 : 3,
                materiel.Any(m => Json.Bool(m, "Grave")) || disques.Count > 0 ? 2 : nbMat > 0 ? 1 : 3, applis.Count > 5 ? 1 : 3,
            };
            return new long[] { ecrans.Count, coupures, nbMat, applis.Count };
        }

        /// <summary>Résumé des 90 jours. niveau : 3 stable, 1 à surveiller, 2 grave.</summary>
        public static string Resume(Dictionary<string, object> d, out int niveau)
        {
            var ecrans = Liste(d, "Ecrans"); var arrets = Liste(d, "Arrets"); var materiel = Liste(d, "Materiel"); var disques = Liste(d, "Disques"); var applis = Liste(d, "Applications");
            int[] n; var c = Chiffres(d, out n);
            long coupures = c[1], nbMat = c[2];
            bool stable = ecrans.Count == 0 && coupures == 0 && nbMat == 0;
            if (stable && applis.Count == 0) { niveau = 3; return Langue.T("✔ Aucun plantage en 90 jours : ton PC est stable."); }
            if (stable) { niveau = 3; return Langue.F("✔ Windows est stable. {0} application(s) ont planté ou se sont figées en 90 jours.", applis.Count); }
            var p = new List<string>();
            if (ecrans.Count > 0) p.Add(Langue.F("{0} écran(s) bleu(s)", ecrans.Count));
            if (coupures > 0) p.Add(Langue.F("{0} arrêt(s) brutal(aux)", coupures));
            if (nbMat > 0) p.Add(Langue.F("{0} erreur(s) matérielle(s) ou de disque", nbMat));
            if (applis.Count > 0) p.Add(Langue.F("{0} application(s) qui plantent", applis.Count));
            niveau = ecrans.Count > 0 || materiel.Any(m => Json.Bool(m, "Grave")) || disques.Count > 0 ? 2 : 1;
            return string.Join(", ", p) + " " + Langue.T("en 90 jours.");
        }

        /// <summary>13 semaines : [système, applications, matériel].</summary>
        public static List<int[]> Semaines(Dictionary<string, object> d)
        {
            var l = new List<int[]>();
            var sem = Json.Valeur(d, "Semaines") as System.Collections.IEnumerable;
            if (sem != null) foreach (var s in sem) { var v = s as System.Collections.IEnumerable; if (v == null || s is string) continue; var a = v.Cast<object>().Select(o => Convert.ToInt32(o)).ToArray(); if (a.Length == 3) l.Add(a); }
            return l;
        }

        /// <summary>Indice de stabilité de Windows (0 à 10), -1 s'il est inconnu.</summary>
        public static double Indice(Dictionary<string, object> d)
        {
            try { return Convert.ToDouble(Json.Valeur(d, "Indice") ?? -1); } catch { return -1; }
        }

        public static List<LigneAnalyse> Lignes(Dictionary<string, object> d)
        {
            var l = new List<LigneAnalyse>();
            foreach (var e in Liste(d, "Ecrans"))
            {
                string cat = Json.Texte(e, "Categorie"), nom;
                NomsCategories.TryGetValue(cat, out nom);
                l.Add(new LigneAnalyse
                {
                    Detail = Langue.T(Json.Texte(e, "Explication")) + "\n\n" + Langue.T("Code technique") + " : " + Json.Texte(e, "Code") + " " + Json.Texte(e, "Nom") + (Json.Texte(e, "Dump") != "" ? "  ·  " + Json.Texte(e, "Dump") : ""),
                    Quoi = Langue.T("Écran bleu") + " : " + (Json.Texte(e, "Nom") != "" ? Json.Texte(e, "Nom") : Json.Texte(e, "Code")), Type = Langue.T("Écran bleu") + " · " + Langue.T(nom ?? "Pilote"),
                    Fois = "1", Nombre = 1, Derniere = Date(Json.Texte(e, "Date")), Niveau = 2,
                });
            }
            foreach (var g in Liste(d, "Arrets").Where(a => Json.Texte(a, "Type") != "ecran-bleu").GroupBy(a => Json.Texte(a, "Type")))
            {
                bool bouton = g.Key == "bouton";
                l.Add(new LigneAnalyse
                {
                    Detail = bouton ? Langue.T("Le PC a été éteint en maintenant le bouton d'alimentation, souvent parce qu'il ne répondait plus (blocage complet).") : Langue.T("Le PC s'est éteint d'un coup, sans écran bleu : coupure de courant, alimentation fatiguée, surchauffe, ou blocage complet."),
                    Quoi = bouton ? Langue.T("Arrêt forcé avec le bouton d'alimentation") : Langue.T("Arrêt brutal (coupure)"), Type = Langue.T("Arrêt brutal"),
                    Fois = g.Count().ToString(), Nombre = g.Count(), Derniere = Date(g.Max(a => Json.Texte(a, "Date"))), Niveau = 1,
                });
            }
            foreach (var m in Liste(d, "Materiel"))
            {
                bool grave = Json.Bool(m, "Grave");
                l.Add(new LigneAnalyse
                {
                    Detail = Langue.T(grave ? "Erreur matérielle grave signalée par le processeur ou la carte mère : souvent un overclocking ou un profil EXPO / XMP instable, une surchauffe ou l'alimentation." : "Erreur matérielle corrigée automatiquement : sans conséquence si elle reste rare.") + "\n(" + Json.Texte(m, "Detail") + ")",
                    Quoi = Langue.T(Json.Texte(m, "Nom")), Type = Langue.T(grave ? "Matériel · grave" : "Matériel · corrigée"),
                    Fois = Json.Long(m, "Fois").ToString(), Nombre = Json.Long(m, "Fois"), Derniere = Date(Json.Texte(m, "Dernier")), Niveau = grave ? 2 : 1,
                });
            }
            foreach (var m in Liste(d, "Disques"))
                l.Add(new LigneAnalyse
                {
                    Detail = Langue.T("Un disque a signalé des erreurs : sauvegarde tes fichiers et vérifie sa santé (Outils > Matériel).") + "\n(" + Json.Texte(m, "Detail") + ")",
                    Quoi = Langue.T(Json.Texte(m, "Nom")), Type = Langue.T("Disque"), Fois = Json.Long(m, "Fois").ToString(), Nombre = Json.Long(m, "Fois"), Derniere = Date(Json.Texte(m, "Dernier")), Niveau = 2,
                });
            long affichage = Json.Long(d, "Affichage");
            if (affichage > 0)
                l.Add(new LigneAnalyse
                {
                    Detail = Langue.T("Le pilote de la carte graphique a cessé de répondre, puis Windows l'a relancé (écran noir quelques secondes). Mets le pilote à jour et surveille la température en jeu."),
                    Quoi = Langue.T("Pilote graphique arrêté puis relancé"), Type = Langue.T("Carte graphique"), Fois = affichage.ToString(), Nombre = affichage, Derniere = Date(Json.Texte(d, "DernierAffichage")), Niveau = 1,
                });
            foreach (var a in Liste(d, "Applications"))
            {
                long n = Json.Long(a, "Plantages"), fig = Json.Long(a, "Figee");
                string module = Json.Texte(a, "Module"), expl = Json.Texte(a, "Explication");
                string detail = (n > 0 ? Langue.T("Plantages") + " : " + n : "") + (fig > 0 ? (n > 0 ? "  ·  " : "") + Langue.T("blocages (l'application ne répondait plus)") + " : " + fig : "") +
                    (module != "" ? "\n" + Langue.T("Composant en cause") + " : " + module + (expl != "" ? " (" + Langue.T(expl) + ")" : "") : "") +
                    "\n" + Langue.T(Json.Texte(a, "Categorie") == "graphique" ? "Conseil : mets à jour le pilote de la carte graphique (Accélérer > Pilotes)." : Json.Texte(a, "Categorie") == "vcpp" ? "Conseil : réinstalle « Microsoft Visual C++ Redistributable » (site de Microsoft)." : "Conseil : mets l'application à jour (Accélérer > Applications), ou réinstalle-la.");
                l.Add(new LigneAnalyse { Detail = detail, Quoi = Json.Texte(a, "Nom"), Type = Langue.T(n > 0 ? "Application" : "Application figée"), Fois = (n + fig).ToString(), Nombre = n + fig, Derniere = Date(Json.Texte(a, "Dernier")), Niveau = 0 });
            }
            return l;
        }

        /// <summary>Conseils (déjà traduits, précédés de « • »).</summary>
        public static List<string> Conseils(Dictionary<string, object> d)
        {
            var conseils = new List<string>();
            foreach (var o in (Json.Valeur(d, "Conseils") as System.Collections.IEnumerable ?? new object[0]))
            {
                var parts = o is string ? null : (o as System.Collections.IEnumerable)?.Cast<object>().ToList();
                conseils.Add("•  " + (parts == null || parts.Count == 0 ? Langue.T(Convert.ToString(o)) : Langue.F(Convert.ToString(parts[0]), parts.Skip(1).ToArray())));
            }
            return conseils;
        }
    }

    internal static class AnalyseDemarrage
    {
        public static string Sec(double ms) { return (ms / 1000.0).ToString(ms < 10000 ? "0.0" : "0", Appli.Fr) + " s"; }

        /// <summary>Résumé. niveau : 0 rien de chronométré, 1 lent (orange), 2 normal (vert), -1 sans autorisation.</summary>
        public static string Resume(Dictionary<string, object> d, out int niveau)
        {
            if (!Json.Bool(d, "Admin")) { niveau = -1; return "Sans l'autorisation de Windows, le chronométrage des démarrages ne peut pas être lu."; }
            var dem = Json.Objets(Json.Valeur(d, "Demarrages"));
            if (dem.Count == 0) { niveau = 0; return "Windows n'a encore chronométré aucun démarrage complet (ou le journal a été vidé)."; }
            var dernier = dem[0];
            double moy = dem.Take(10).Average(x => Json.Long(x, "Total"));
            niveau = moy > 90000 ? 1 : 2;
            return "Dernier démarrage : " + Sec(Json.Long(dernier, "Total")) + "  (" + Sec(Json.Long(dernier, "Principal")) + " jusqu'au bureau + " + Sec(Json.Long(dernier, "Apres")) + " de programmes au démarrage)   ·   moyenne des 10 derniers : " + Sec(moy);
        }

        /// <summary>Démarrages du plus ancien au plus récent : date, durée jusqu'au bureau, durée des programmes au démarrage (ms).</summary>
        public static List<Tuple<string, long, long>> Demarrages(Dictionary<string, object> d)
        {
            return Json.Objets(Json.Valeur(d, "Demarrages")).AsEnumerable().Reverse().Select(x => Tuple.Create(Json.Texte(x, "Date"), Json.Long(x, "Principal"), Json.Long(x, "Apres"))).ToList();
        }

        public static List<LigneAnalyse> Lignes(Dictionary<string, object> d)
        {
            return Json.Objets(Json.Valeur(d, "Ralentissements")).Select(r =>
            {
                long m = Json.Long(r, "Moyenne");
                return new LigneAnalyse { Quoi = Json.Texte(r, "Nom"), Type = Json.Texte(r, "Type"), Valeur = m, Fois = Json.Long(r, "Fois").ToString(), Nombre = Json.Long(r, "Fois"), Derniere = Json.Texte(r, "Dernier"), Niveau = m >= 5000 ? 1 : 0 };
            }).ToList();
        }

        /// <summary>Conseils (non traduits).</summary>
        public static List<string> Conseils(Dictionary<string, object> d)
        {
            var ral = Json.Objets(Json.Valeur(d, "Ralentissements"));
            var dem = Json.Objets(Json.Valeur(d, "Demarrages"));
            var conseils = new List<string>();
            var pires = ral.Where(r => Json.Long(r, "Moyenne") >= 3000).Take(3).Select(r => Json.Texte(r, "Nom") + " (" + Sec(Json.Long(r, "Moyenne")) + ")").ToList();
            if (pires.Count > 0) conseils.Add("Les plus gros freins : " + string.Join(", ", pires) + ". Pour une application, décoche-la dans Accélérer > Démarrage ; pour un pilote, mets-le à jour.");
            if (!Json.Bool(d, "DemarrageRapide")) conseils.Add("Le démarrage rapide de Windows est désactivé : l'activer (Panneau de configuration > Options d'alimentation) peut faire gagner plusieurs secondes.");
            if (conseils.Count == 0 && dem.Count > 0) conseils.Add("✔ Aucun ralentissement important repéré.");
            return conseils;
        }
    }
}
