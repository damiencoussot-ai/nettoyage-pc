// Nettoyage PC - bulletin mensuel du PC (Premium) : une page claire, enregistrée dans Documents\NettoyagePC\Bulletins,
// à garder, imprimer ou envoyer à un proche : note du PC, espace gagné, démarrage, températures, santé, parties jouées.
// Rien n'est envoyé nulle part. Sans interface : partagé v4 / v5.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace NettoyagePC
{
    internal static class Bulletin
    {
        public static string Dossier { get { return Path.Combine(Chemins.Donnees, "Bulletins"); } }

        public static string Fichier(DateTime mois) { return Path.Combine(Dossier, "Bulletin-" + mois.ToString("yyyy-MM") + ".html"); }

        /// <summary>Bulletins déjà créés (le plus récent d'abord).</summary>
        public static List<FileInfo> Liste()
        {
            try { return Directory.Exists(Dossier) ? new DirectoryInfo(Dossier).GetFiles("Bulletin-*.html").OrderByDescending(f => f.Name).ToList() : new List<FileInfo>(); }
            catch { return new List<FileInfo>(); }
        }

        private static string H(string s) { return WebUtility.HtmlEncode(s ?? ""); }

        private static string NomMois(DateTime m)
        {
            string s = m.ToString("MMMM yyyy", Langue.Culture);
            return s.Length > 0 ? char.ToUpper(s[0], Langue.Culture) + s.Substring(1) : s;
        }

        /// <summary>Espace libéré et nombre de nettoyages pendant la période (statistiques.csv).</summary>
        public static Tuple<long, int> GainsPeriode(DateTime debut, DateTime fin)
        {
            long o = 0; int n = 0;
            try
            {
                string f = Chemins.Fichier("statistiques.csv");
                if (File.Exists(f))
                    foreach (var l in File.ReadAllLines(f))
                    {
                        var c = l.Split(';'); DateTime d; long x;
                        if (c.Length >= 2 && DateTime.TryParseExact(c[0].Trim(), "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && d >= debut && d < fin && long.TryParse(c[1].Trim(), out x)) { o += x; n++; }
                    }
            }
            catch { }
            return Tuple.Create(o, n);
        }

        private static List<double> Demarrages(DateTime debut, DateTime fin)
        {
            var l = new List<double>();
            try
            {
                foreach (var x in Json.Objets(Json.LireFichier(Path.Combine(Chemins.Local, "demarrages.json"))))
                {
                    DateTime d;
                    if (DateTime.TryParse(Json.Texte(x, "Date"), CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && d >= debut && d < fin && Json.Long(x, "Total") > 0) l.Add(Json.Long(x, "Total") / 1000.0);
                }
            }
            catch { }
            return l;
        }

        private static double TemperatureMax(DateTime debut, DateTime fin)
        {
            double m = 0;
            try
            {
                var h = Json.LireFichier(Path.Combine(Chemins.Local, "sante-temperatures.json")) as Dictionary<string, object>;
                if (h != null)
                    foreach (var kv in h)
                    {
                        DateTime d;
                        if (DateTime.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d) && d >= debut && d < fin)
                            m = Math.Max(m, Convert.ToDouble(kv.Value, CultureInfo.InvariantCulture));
                    }
            }
            catch { }
            return m;
        }

        /// <summary>Crée (ou remplace) le bulletin du mois donné. Long (bilan, santé) : hors du fil de l'interface. Retourne le fichier.</summary>
        public static string Creer(DateTime mois)
        {
            var debut = new DateTime(mois.Year, mois.Month, 1);
            var fin = debut.AddMonths(1);
            var avantDebut = debut.AddMonths(-1);

            DonneesBilan d = null; List<PointBilanPc> points = new List<PointBilanPc>(); int note = -1;
            try { d = BilanPc.Collecter(); points = BilanPc.Points(d); note = BilanPc.Score(points.Select(p => p.Etat)); } catch { }
            var gains = GainsPeriode(debut, fin);
            var demar = Demarrages(debut, fin); var demarAvant = Demarrages(avantDebut, debut);
            double temp = TemperatureMax(debut, fin);
            List<IndicateurSante> sante = new List<IndicateurSante>(); string resumeSante = ""; int niveauSante = 0;
            try { sante = Sante.Ordonner(Sante.Analyser()); resumeSante = Sante.Resume(sante, out niveauSante); } catch { }
            var parties = ModeJeuAuto.Sessions().Where(s => s.Debut >= debut && s.Debut < fin).ToList();

            var sb = new StringBuilder();
            Action<string> e = x => sb.Append(x);
            string titre = Langue.T("Bulletin du PC") + " · " + NomMois(debut);
            e("<!doctype html><html lang=\"" + Langue.Code + "\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>" + H(titre) + "</title><style>");
            e("body{font-family:'Segoe UI',system-ui,sans-serif;background:#f4f7fa;color:#14202b;margin:0;padding:28px 16px}main{max-width:860px;margin:0 auto;background:#fff;border:1px solid #dbe3ea;border-radius:6px;padding:28px 32px}");
            e("h1{margin:0 0 4px;font-size:26px}h2{font-size:17px;margin:26px 0 10px;border-bottom:1px solid #e3e9ee;padding-bottom:6px}.sous{color:#5b6b78;margin:0 0 18px}");
            e(".tuiles{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:10px}.tuile{border:1px solid #dbe3ea;border-left:4px solid #0aa3b5;border-radius:4px;padding:10px 12px}");
            e(".tuile b{display:block;font-size:24px}.tuile span{color:#5b6b78;font-size:13px}ul{padding-left:20px;margin:6px 0}li{margin:4px 0}.ok{color:#11804a}.att{color:#b35c00}.pb{color:#b3261e}");
            e("footer{color:#7a8894;font-size:12px;margin-top:26px}@media print{body{background:#fff;padding:0}main{border:0}}</style></head><body><main>");
            e("<h1>" + H(titre) + "</h1><p class=\"sous\">" + H(Langue.F("PC « {0} » · Windows {1} · créé le {2} par Nettoyage PC {3}", Environment.MachineName, InfosPc.Edition, DateTime.Now.ToString("dd/MM/yyyy HH:mm"), Appli.Version)) + "</p>");

            e("<div class=\"tuiles\">");
            Action<string, string> tuile = (v, l) => e("<div class=\"tuile\"><b>" + H(v) + "</b><span>" + H(l) + "</span></div>");
            tuile(note >= 0 ? note + " / 100" : "—", Langue.T("note du PC aujourd'hui"));
            tuile(Format.Taille(gains.Item1), Langue.F("libérés ce mois-ci ({0} nettoyage(s))", gains.Item2));
            if (demar.Count > 0) tuile(demar.Average().ToString("0", Langue.Culture) + " s", Langue.T("démarrage moyen de Windows"));
            if (temp > 0) tuile(temp.ToString("0") + " °C", Langue.T("carte graphique, au plus chaud"));
            if (d != null && d.DisqueTotal > 0) tuile(Format.Taille(d.DisqueLibre), Langue.T("libres sur le disque de Windows"));
            if (parties.Count > 0) tuile(ModeJeuAuto.TexteDuree(TimeSpan.FromTicks(parties.Sum(p => p.Duree.Ticks))), Langue.F("de jeu en mode jeu automatique ({0} partie(s))", parties.Count));
            e("</div>");

            if (demar.Count > 0 && demarAvant.Count > 0)
            {
                double a = demarAvant.Average(), b = demar.Average();
                e("<p>" + H(Math.Abs(b - a) < 2 ? Langue.F("Démarrage stable : {0} s en moyenne, comme le mois d'avant.", b.ToString("0", Langue.Culture))
                    : b < a ? Langue.F("Démarrage plus rapide : {0} s en moyenne, contre {1} s le mois d'avant.", b.ToString("0", Langue.Culture), a.ToString("0", Langue.Culture))
                    : Langue.F("Démarrage plus lent : {0} s en moyenne, contre {1} s le mois d'avant. L'Analyse du démarrage (Premium) dit ce qui le ralentit.", b.ToString("0", Langue.Culture), a.ToString("0", Langue.Culture))) + "</p>");
            }

            e("<h2>" + H(Langue.T("Le bilan du PC")) + "</h2><ul>");
            foreach (var p in points.Where(x => x.Etat != "ok")) e("<li class=\"" + (p.Etat == "probleme" ? "pb" : "att") + "\">" + H(Langue.T(p.Texte)) + "</li>");
            int bons = points.Count(x => x.Etat == "ok");
            if (bons > 0) e("<li class=\"ok\">" + H(Langue.F("{0} autres points vérifiés : tout va bien.", bons)) + "</li>");
            if (points.Count == 0) e("<li>" + H(Langue.T("Bilan indisponible.")) + "</li>");
            e("</ul>");

            if (sante.Count > 0)
            {
                e("<h2>" + H(Langue.T("Santé du matériel")) + "</h2><p class=\"" + (niveauSante == 2 ? "pb" : niveauSante == 1 ? "att" : "ok") + "\">" + H(Langue.T(resumeSante)) + "</p><ul>");
                foreach (var i in sante.Where(x => x.Etat != "inconnu").Take(8))
                    e("<li class=\"" + (i.Etat == "probleme" ? "pb" : i.Etat == "attention" ? "att" : "ok") + "\"><b>" + H(Langue.T(i.Nom)) + "</b> : " + H(Langue.T(i.Valeur)) + (i.Conseil != "" && i.Etat != "ok" ? " — " + H(Langue.T(i.Conseil)) : "") + "</li>");
                e("</ul>");
            }

            if (parties.Count > 0)
            {
                e("<h2>" + H(Langue.T("Parties jouées (mode jeu automatique)")) + "</h2><ul>");
                foreach (var g in parties.GroupBy(p => p.Jeu).OrderByDescending(g => g.Sum(p => p.Duree.Ticks)).Take(8))
                    e("<li>" + H(g.Key) + " : " + H(ModeJeuAuto.TexteDuree(TimeSpan.FromTicks(g.Sum(p => p.Duree.Ticks)))) + "</li>");
                e("</ul>");
            }

            e("<footer>" + H(Langue.T("Ce bulletin est enregistré sur ce PC seulement (Documents\\NettoyagePC\\Bulletins). Nettoyage PC n'envoie rien sur Internet.")) + "</footer></main></body></html>");
            Directory.CreateDirectory(Dossier);
            string fichier = Fichier(debut);
            File.WriteAllText(fichier, sb.ToString(), new UTF8Encoding(false));
            return fichier;
        }
    }
}
