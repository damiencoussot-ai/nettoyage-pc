// Nettoyage PC - santé prédictive (Premium) : signes de faiblesse du matériel et tendances
// (erreurs matérielles corrigées, erreurs du disque, arrêts brutaux, usure des SSD, batterie, température de la carte graphique).
// Les prévisions sont de simples tendances : elles disent « à ce rythme », jamais une certitude.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal class IndicateurSante
    {
        public string Cle = "", Nom = "", Valeur = "", Explication = "", Prevision = "", Conseil = "";
        /// <summary>« ok », « attention » ou « probleme » (« inconnu » : pas de données).</summary>
        public string Etat = "ok";
        public List<Tuple<DateTime, double>> Points = new List<Tuple<DateTime, double>>();
        public bool Barres;
        public string Unite = "";
        public double? Pente, Seuil;
        public DateTime? PrevisionJusqua;
        /// <summary>Action proposée : « action:cle », « page:Cle », « uri:... » ou vide.</summary>
        public string Cible = "", Bouton = "";
    }

    internal static class Sante
    {
        /// <summary>Résumé des indicateurs. niveau : 0 tout va bien, 1 à surveiller, 2 sérieux.</summary>
        public static string Resume(List<IndicateurSante> indicateurs, out int niveau)
        {
            int pb = indicateurs.Count(i => i.Etat == "probleme"), at = indicateurs.Count(i => i.Etat == "attention");
            niveau = pb > 0 ? 2 : at > 0 ? 1 : 0;
            return pb > 0 ? Langue.F(pb == 1 ? "1 signe sérieux à traiter" : "{0} signes sérieux à traiter", pb) + (at > 0 ? Langue.F(", {0} à surveiller", at) : "")
                : at > 0 ? Langue.F(at == 1 ? "Rien de grave : 1 point à surveiller" : "Rien de grave : {0} points à surveiller", at) : Langue.T("✔  Aucun signe de faiblesse : ton matériel est en bonne santé");
        }

        /// <summary>Indicateurs du plus grave au moins grave.</summary>
        public static List<IndicateurSante> Ordonner(List<IndicateurSante> indicateurs)
        {
            var ordre = new Dictionary<string, int> { { "probleme", 0 }, { "attention", 1 }, { "ok", 2 }, { "inconnu", 3 } };
            return indicateurs.OrderBy(x => ordre.ContainsKey(x.Etat) ? ordre[x.Etat] : 4).ToList();
        }

        private static string FichierDisques { get { return Path.Combine(Chemins.Local, "sante-disques.json"); } }
        private static string FichierTemperatures { get { return Path.Combine(Chemins.Local, "sante-temperatures.json"); } }

        // ------------------------------------------------------------ Enregistrements
        /// <summary>Usure des SSD envoyée par « Santé détaillée des disques » (autorisation administrateur) : un point par disque et par jour.</summary>
        public static void EnregistrerDisques(object donnees)
        {
            try
            {
                var d = donnees as Dictionary<string, object>;
                if (d == null || !Json.Bool(d, "Admin")) return;
                var h = Json.LireFichier(FichierDisques) as Dictionary<string, object> ?? new Dictionary<string, object>();
                string jour = DateTime.Now.ToString("yyyy-MM-dd");
                foreach (var x in Json.Objets(Json.Valeur(d, "Disques")))
                {
                    object u = Json.Valeur(x, "Usure");
                    if (u == null) continue;
                    string id = Json.Texte(x, "Nom") + "|" + Json.Texte(x, "Id");
                    var disque = Json.Dict(h, id) ?? new Dictionary<string, object>();
                    disque["Nom"] = Json.Texte(x, "Nom");
                    var pts = Json.Dict(disque, "Usure") ?? new Dictionary<string, object>();
                    pts[jour] = Convert.ToDouble(u, CultureInfo.InvariantCulture);
                    disque["Usure"] = pts;
                    h[id] = disque;
                }
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(FichierDisques, Json.Ecrire(h));
            }
            catch (Exception ex) { JournalDemarrage.Erreur("santé des disques", ex); }
        }

        /// <summary>(Gardien) température maximale de la carte graphique du jour.</summary>
        public static void EnregistrerTemperature(DateTime jour, double max)
        {
            try
            {
                if (max <= 0 || max > 130) return;
                var h = Json.LireFichier(FichierTemperatures) as Dictionary<string, object> ?? new Dictionary<string, object>();
                string k = jour.ToString("yyyy-MM-dd");
                if (Json.Double(h, k) >= max) return;
                h[k] = Math.Round(max, 1);
                foreach (var ancien in h.Keys.Where(x => string.CompareOrdinal(x, DateTime.Now.AddDays(-400).ToString("yyyy-MM-dd")) < 0).ToList()) h.Remove(ancien);
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(FichierTemperatures, Json.Ecrire(h));
            }
            catch { }
        }

        // ------------------------------------------------------------ Calculs
        /// <summary>Droite des moindres carrés : pente par jour et valeur au dernier point. false si moins de 2 points ou moins de 7 jours d'écart.</summary>
        public static bool Tendance(List<Tuple<DateTime, double>> pts, out double pente)
        {
            pente = 0;
            if (pts.Count < 2 || (pts.Max(p => p.Item1) - pts.Min(p => p.Item1)).TotalDays < 7) return false;
            DateTime t0 = pts.Min(p => p.Item1);
            var x = pts.Select(p => (p.Item1 - t0).TotalDays).ToList(); var y = pts.Select(p => p.Item2).ToList();
            double mx = x.Average(), my = y.Average(), sxx = 0, sxy = 0;
            for (int i = 0; i < x.Count; i++) { sxx += (x[i] - mx) * (x[i] - mx); sxy += (x[i] - mx) * (y[i] - my); }
            if (sxx <= 0) return false;
            pente = sxy / sxx;
            return true;
        }

        private static List<Tuple<DateTime, double>> ParSemaine(IEnumerable<DateTime> dates, int semaines = 13)
        {
            var debut = DateTime.Today.AddDays(-7 * semaines + 1);
            var l = new List<Tuple<DateTime, double>>();
            var d = dates.Where(x => x >= debut).ToList();
            for (int i = 0; i < semaines; i++)
            {
                var a = debut.AddDays(7 * i); var b = a.AddDays(7);
                l.Add(Tuple.Create(a, (double)d.Count(x => x >= a && x < b)));
            }
            return l;
        }

        private static string Duree(double jours)
        {
            if (jours < 60) return Langue.F("environ {0} jours", Math.Max(1, (int)jours));
            if (jours < 730) return Langue.F("environ {0} mois", (int)Math.Round(jours / 30.4));
            return Langue.F("environ {0} ans", (jours / 365.0).ToString("0.#", Appli.Fr));
        }

        public static List<IndicateurSante> Analyser()
        {
            var l = new List<IndicateurSante>();

            // 1. Erreurs matérielles corrigées (WHEA)
            var whea = Journaux.Lister("System", "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and " + Journaux.Depuis(91) + "]]", 3000, false);
            {
                var i = new IndicateurSante { Cle = "whea", Nom = "Erreurs matérielles", Barres = true, Unite = "par semaine" };
                i.Points = ParSemaine(whea.Select(e => e.Date));
                int fatales = whea.Count(e => e.Id == 18 || e.Id == 20 || e.Id == 46);
                int mois = whea.Count(e => e.Date >= DateTime.Now.AddDays(-30));
                i.Valeur = mois == 0 ? Langue.T("aucune ce mois-ci") : Langue.F("{0} ce mois-ci", mois);
                if (whea.Count == 0) { i.Explication = "Le processeur, la mémoire et la carte graphique n'ont signalé aucune erreur depuis 3 mois."; }
                else
                {
                    i.Etat = fatales > 0 || mois >= 10 ? "probleme" : mois > 0 ? "attention" : "ok";
                    int cpu = whea.Count(e => e.Id == 19 || e.Id == 18), pcie = whea.Count(e => e.Id == 17), ram = whea.Count(e => e.Id == 47);
                    string origine = cpu >= pcie && cpu >= ram ? "le processeur ou la mémoire vive (souvent un profil EXPO/XMP ou un réglage de tension trop juste dans le BIOS)"
                        : pcie >= ram ? "la liaison PCI Express (carte graphique ou SSD NVMe : réglage « PCIe Gen » du BIOS, rallonge, carte mal enfoncée)" : "la mémoire vive (profil EXPO/XMP trop rapide, barrette fatiguée)";
                    i.Explication = Langue.F("Windows a corrigé {0} erreur(s) en 3 mois. Elles viennent surtout de {1}.", whea.Count, Langue.T(origine));
                    i.Conseil = fatales > 0 ? "Des erreurs non récupérables ont eu lieu : remets le BIOS à ses réglages par défaut pour tester, et lance le test des barrettes." : "Quelques erreurs isolées ne sont pas graves. Si elles augmentent : mets le BIOS à jour et teste la mémoire.";
                    i.Cible = "action:ramtest"; i.Bouton = "Tester la mémoire";
                }
                double pente;
                if (Tendance(i.Points, out pente) && pente > 0.15 && i.Points.Skip(9).Sum(p => p.Item2) > i.Points.Take(9).Sum(p => p.Item2) / 2.0)
                { i.Prevision = "En hausse ces dernières semaines : surveille-les."; if (i.Etat == "ok") i.Etat = "attention"; }
                l.Add(i);
            }

            // 2. Erreurs du disque (blocs défectueux, lectures répétées, réinitialisations)
            var disque = Journaux.Lister("System", "*[System[(EventID=7 or EventID=51 or EventID=52 or EventID=153 or EventID=129 or EventID=55) and " + Journaux.Depuis(91) + "]]", 2000, false)
                .Where(e => System.Text.RegularExpressions.Regex.IsMatch(e.Source, @"^(disk|Ntfs|stornvme|storahci|iaStorA|iaStorAC|amdsata|amdnvme|nvme|Microsoft-Windows-Ntfs|Microsoft-Windows-StorPort)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToList();
            {
                var i = new IndicateurSante { Cle = "disque", Nom = "Erreurs du disque", Barres = true, Unite = "par semaine" };
                i.Points = ParSemaine(disque.Select(e => e.Date));
                int blocs = disque.Count(e => e.Id == 7 && e.Source.Equals("disk", StringComparison.OrdinalIgnoreCase));
                int mois = disque.Count(e => e.Date >= DateTime.Now.AddDays(-30));
                i.Valeur = mois == 0 ? Langue.T("aucune ce mois-ci") : Langue.F("{0} ce mois-ci", mois);
                if (disque.Count == 0) i.Explication = "Aucune erreur de lecture ou d'écriture depuis 3 mois.";
                else
                {
                    i.Etat = blocs > 0 || mois >= 5 ? "probleme" : "attention";
                    i.Explication = blocs > 0 ? Langue.F("{0} bloc(s) défectueux signalé(s) : la surface du disque s'abîme.", blocs) : Langue.F("{0} erreur(s) ou réinitialisation(s) du disque en 3 mois (câble, alimentation ou disque fatigué).", disque.Count);
                    i.Conseil = "Sauvegarde tes documents dès maintenant, puis lance la vérification des disques.";
                    i.Cible = "page:Sauvegarde"; i.Bouton = "Sauvegarder";
                }
                l.Add(i);
            }

            // 3. Arrêts brutaux et écrans bleus
            var arrets = Journaux.Lister("System", "*[System[(Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=41 or Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting'] and EventID=1001) and " + Journaux.Depuis(91) + "]]", 500, false);
            {
                var i = new IndicateurSante { Cle = "arrets", Nom = "Arrêts brutaux et écrans bleus", Barres = true, Unite = "par semaine" };
                i.Points = ParSemaine(arrets.Where(e => e.Id == 41 || e.Id == 1001).Select(e => e.Date));
                int bleus = arrets.Count(e => e.Id == 1001), brutaux = arrets.Count(e => e.Id == 41);
                int mois = arrets.Count(e => e.Date >= DateTime.Now.AddDays(-30));
                i.Valeur = mois == 0 ? Langue.T("aucun ce mois-ci") : Langue.F("{0} ce mois-ci", mois);
                if (arrets.Count == 0) i.Explication = "Le PC ne s'est jamais arrêté brutalement depuis 3 mois.";
                else
                {
                    i.Etat = bleus >= 2 || mois >= 3 ? "probleme" : "attention";
                    i.Explication = Langue.F("{0} écran(s) bleu(s) et {1} redémarrage(s) inattendu(s) en 3 mois (coupure de courant, bouton maintenu, surchauffe ou instabilité).", bleus, brutaux);
                    i.Conseil = "L'analyse des plantages explique la cause de chaque écran bleu.";
                    i.Cible = "outil:plantages"; i.Bouton = "Analyser";
                }
                l.Add(i);
            }

            // 4. Usure des SSD (mesurée avec l'autorisation administrateur)
            var hd = Json.LireFichier(FichierDisques) as Dictionary<string, object>;
            bool usureVue = false;
            if (hd != null)
                foreach (var kv in hd)
                {
                    var dd = kv.Value as Dictionary<string, object>;
                    var u = Json.Dict(dd, "Usure");
                    if (u == null || u.Count == 0) continue;
                    usureVue = true;
                    var i = new IndicateurSante { Cle = "ssd", Nom = Langue.F("Usure du SSD {0}", Json.Texte(dd, "Nom")), Unite = "%", Seuil = 100 };
                    foreach (var p in u) { DateTime d; if (DateTime.TryParseExact(p.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) i.Points.Add(Tuple.Create(d, Convert.ToDouble(p.Value, CultureInfo.InvariantCulture))); }
                    i.Points = i.Points.OrderBy(p => p.Item1).ToList();
                    var der = i.Points.Last();
                    i.Valeur = Langue.F("{0} % d'usure", der.Item2.ToString("0"));
                    i.Etat = der.Item2 >= 90 ? "probleme" : der.Item2 >= 70 ? "attention" : "ok";
                    i.Explication = Langue.F("Mesuré le {0}. Un SSD est garanti pour un certain volume d'écritures : à 100 %, il peut commencer à faiblir.", der.Item1.ToString("dd/MM/yyyy"));
                    double pente;
                    if (Tendance(i.Points, out pente) && pente > 0.0005)
                    {
                        double reste = (100 - der.Item2) / pente;
                        i.Pente = pente; i.PrevisionJusqua = der.Item1.AddDays(Math.Min(reste, 3650));
                        i.Prevision = reste > 3650 ? Langue.T("À ce rythme, plus de 10 ans avant d'atteindre 100 %.") : Langue.F("À ce rythme, 100 % d'usure dans {0} (vers {1}).", Duree(reste), der.Item1.AddDays(reste).ToString("MM/yyyy"));
                        if (reste < 365 && i.Etat == "ok") i.Etat = "attention";
                    }
                    else if (i.Points.Count < 2 || (der.Item1 - i.Points[0].Item1).TotalDays < 7) i.Prevision = "Remesure dans quelques semaines pour connaître sa durée de vie restante.";
                    else i.Prevision = "Usure stable : il a encore de la marge.";
                    i.Cible = "action:santedisques"; i.Bouton = "Remesurer";
                    l.Add(i);
                }
            if (!usureVue)
                l.Add(new IndicateurSante { Cle = "ssd", Nom = "Usure des SSD", Etat = "inconnu", Valeur = Langue.T("pas encore mesurée"), Explication = "Windows connaît l'usure de chaque SSD, mais il faut ton autorisation pour la lire.", Cible = "action:santedisques", Bouton = "Mesurer" });

            // 5. Batterie (PC portable)
            try
            {
                var b = Batterie.Lire();
                if (b.Presente && b.Sante > 0)
                {
                    var i = new IndicateurSante { Cle = "batterie", Nom = "Batterie", Unite = "%", Valeur = Langue.F("{0} % de sa capacité d'origine", b.Sante.ToString("0")) };
                    i.Points = b.Historique.Where(p => p.Item3 > 0 && p.Item2 > 0).Select(p => Tuple.Create(p.Item1, 100.0 * p.Item2 / p.Item3)).OrderBy(p => p.Item1).ToList();
                    if (i.Points.Count == 0 || i.Points.Last().Item1.Date != DateTime.Today) i.Points.Add(Tuple.Create(DateTime.Today, b.Sante));
                    i.Etat = b.Sante < 60 ? "probleme" : b.Sante < 75 ? "attention" : "ok";
                    i.Explication = b.Cycles > 0 ? Langue.F("{0} cycles de charge.", b.Cycles) : "Une batterie perd un peu de capacité à chaque cycle de charge.";
                    i.Seuil = 60;
                    double pente;
                    if (Tendance(i.Points, out pente) && pente < -0.002 && b.Sante > 60)
                    {
                        double reste = (b.Sante - 60) / -pente;
                        i.Pente = pente; i.PrevisionJusqua = DateTime.Today.AddDays(Math.Min(reste, 3650));
                        i.Prevision = reste > 3650 ? Langue.T("À ce rythme, elle tiendra encore très longtemps.") : Langue.F("À ce rythme, elle descendra à 60 % (à remplacer) dans {0}.", Duree(reste));
                    }
                    i.Conseil = "Pour la ménager : évite de la laisser à 100 % branchée en permanence, et la chaleur.";
                    i.Cible = "outil:batterie"; i.Bouton = "Détails";
                    l.Add(i);
                }
            }
            catch { }

            // 6. Température de la carte graphique (relevée par le gardien)
            {
                var i = new IndicateurSante { Cle = "chaleur", Nom = "Température de la carte graphique", Unite = "°C" };
                var h = Json.LireFichier(FichierTemperatures) as Dictionary<string, object>;
                if (h != null)
                    foreach (var p in h) { DateTime d; if (DateTime.TryParseExact(p.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) i.Points.Add(Tuple.Create(d, Convert.ToDouble(p.Value, CultureInfo.InvariantCulture))); }
                i.Points = i.Points.Where(p => p.Item1 >= DateTime.Today.AddDays(-120)).OrderBy(p => p.Item1).ToList();
                if (i.Points.Count == 0)
                {
                    i.Etat = "inconnu"; i.Valeur = Langue.T("pas encore suivie");
                    i.Explication = "Le gardien relève chaque jour la température maximale de la carte graphique. Active-le pour voir si elle chauffe de plus en plus (poussière, pâte thermique).";
                    i.Cible = "outil:gardien"; i.Bouton = "Activer le gardien";
                }
                else
                {
                    var recents = i.Points.Where(p => p.Item1 >= DateTime.Today.AddDays(-14)).Select(p => p.Item2).ToList();
                    var anciens = i.Points.Where(p => p.Item1 < DateTime.Today.AddDays(-30)).Select(p => p.Item2).ToList();
                    double max = recents.Count > 0 ? recents.Max() : i.Points.Last().Item2;
                    i.Valeur = Langue.F("{0} °C au maximum ces 2 semaines", max.ToString("0"));
                    i.Etat = max >= 90 ? "probleme" : max >= 84 ? "attention" : "ok";
                    i.Explication = "Une carte graphique tourne normalement jusqu'à 80-85 °C en jeu.";
                    if (recents.Count >= 3 && anciens.Count >= 5 && recents.Average() - anciens.Average() >= 5)
                    {
                        i.Prevision = Langue.F("Elle chauffe de {0} °C de plus qu'il y a un mois : la poussière s'accumule sûrement.", (recents.Average() - anciens.Average()).ToString("0"));
                        if (i.Etat == "ok") i.Etat = "attention";
                    }
                    i.Conseil = "Dépoussière les ventilateurs et les filtres (bombe d'air sec, PC éteint et débranché).";
                }
                l.Add(i);
            }
            return l;
        }

        /// <summary>(Gardien) signes nouveaux et sérieux des dernières 24 heures, pour une alerte.</summary>
        public static Tuple<string, string> AlerteDuJour()
        {
            var disque = Journaux.Lister("System", "*[System[Provider[@Name='disk'] and (EventID=7 or EventID=51) and " + Journaux.Depuis(1) + "]]", 50, false);
            if (disque.Count > 0) return Tuple.Create("Ton disque signale des erreurs", "Des secteurs du disque sont illisibles : sauvegarde tes documents dès maintenant. Clique pour voir la santé du PC.");
            var whea = Journaux.Lister("System", "*[System[Provider[@Name='Microsoft-Windows-WHEA-Logger'] and " + Journaux.Depuis(1) + "]]", 200, false);
            if (whea.Any(e => e.Id == 18 || e.Id == 20 || e.Id == 46) || whea.Count >= 5) return Tuple.Create("Erreurs matérielles détectées", "Le processeur, la mémoire ou la carte graphique ont signalé des erreurs aujourd'hui. Clique pour voir d'où elles viennent.");
            var arrets = Journaux.Lister("System", "*[System[(Provider[@Name='Microsoft-Windows-Kernel-Power'] and EventID=41 or Provider[@Name='Microsoft-Windows-WER-SystemErrorReporting'] and EventID=1001) and " + Journaux.Depuis(1) + "]]", 20, false);
            if (arrets.Count >= 2) return Tuple.Create("Le PC s'est arrêté brutalement", "Plusieurs écrans bleus ou redémarrages inattendus aujourd'hui. Clique pour voir la santé du PC.");
            return null;
        }
    }
}
