// Nettoyage PC - bilan de santé du PC (accueil) : collecte, points à vérifier, note sur 100 et secteurs de l'anneau.
// Sans interface : partagé par la version 4 (WinForms) et la version 5 (Avalonia). Rien n'est modifié sur le PC.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal class DonneesBilan
    {
        public double DisqueLibre, DisqueTotal, RamTotal, RamLibre;
        public long Corbeille, Temp;
        public DateTime? DernierJournal, Demarre, PointDate, DatePiloteGpu;
        public bool Tache, MpPresent, MpTempsReel, PointConnu, RedemarrageAttente;
        public int MpAge, NbDemarrage, RamPrevue, RamReelle;
        public List<string> Malades = new List<string>(), AutresAv = new List<string>(), PareFeuOff = new List<string>();
        public string NomGpu = "";
        public List<PointPotentiel> Potentiel = new List<PointPotentiel>();
        public List<AccesDistant> AccesDistants = new List<AccesDistant>();
    }

    /// <summary>
    /// Un point du bilan. Etat : "probleme", "attention" ou "ok". Cible (ce que fait le bouton) :
    /// "action:cle" (action du moteur), "page:Cle", "uri:adresse", "code:redemarrer" ou "code:bouclier" ; null sans bouton.
    /// </summary>
    internal class PointBilanPc { public string Etat, Texte, Bouton, Cible; }

    internal static class BilanPc
    {
        public static DonneesBilan Collecter()
        {
            var d = new DonneesBilan();
            try { var dq = new DriveInfo(Environment.GetEnvironmentVariable("SystemDrive") ?? Path.GetPathRoot(Chemins.Windows)); if (dq.IsReady) { d.DisqueLibre = dq.AvailableFreeSpace; d.DisqueTotal = dq.TotalSize; } } catch { }
            long n; d.Corbeille = Natif.TailleCorbeille(out n);
            d.Temp = TailleFichiers.Dossier(Path.GetTempPath());
            try { var j = new DirectoryInfo(Chemins.Donnees).GetFiles("journal_*.txt").OrderByDescending(x => x.LastWriteTime).FirstOrDefault(); if (j != null) d.DernierJournal = j.LastWriteTime; } catch { }
            d.Tache = Moteur.TacheExiste(Moteur.TacheHebdo);
            ulong t, l; Natif.Memoire(out t, out l); d.RamTotal = t; d.RamLibre = l;
            var os = Wmi.Requete("SELECT LastBootUpTime FROM Win32_OperatingSystem").FirstOrDefault();
            if (os != null) d.Demarre = Wmi.Date(os, "LastBootUpTime");
            d.NbDemarrage = ElementsDemarrage.Lister().Count(x => x.Actif);
            d.Malades = DisquePhysique.Lister(false).Where(x => !x.EnBonneSante).Select(x => x.Nom).ToList();
            var mp = Wmi.Requete("SELECT RealTimeProtectionEnabled, AntivirusSignatureAge FROM MSFT_MpComputerStatus", @"root\Microsoft\Windows\Defender").FirstOrDefault();
            if (mp != null) { d.MpPresent = true; d.MpTempsReel = Wmi.Bool(mp, "RealTimeProtectionEnabled"); d.MpAge = (int)Wmi.Long(mp, "AntivirusSignatureAge"); }
            d.AutresAv = InfosPc.Antivirus().Where(x => x.IndexOf("Defender", StringComparison.OrdinalIgnoreCase) < 0).ToList();
            d.PareFeuOff = Moteur.PareFeuDesactive();
            if (Appli.EstAdmin) { var p = Moteur.DernierPoint(); if (p != null) d.PointDate = p.Item3; d.PointConnu = true; }
            else
            {
                // sans droits administrateur : le journal de Windows garde la trace des points créés
                try
                {
                    var date = Evenements.DatePremier("Application", "*[System[EventID=8194]]", 30, "SystemRestore");
                    if (date.HasValue) { d.PointDate = date; d.PointConnu = true; }
                }
                catch { }
            }
            var b = Barrette.Lister(); d.RamPrevue = Barrette.VitessePrevue(b); d.RamReelle = Barrette.VitesseReelle(b);
            var gpu = InfosPc.CarteGraphiquePrincipale();
            if (gpu != null && gpu.DatePilote.HasValue) { d.NomGpu = gpu.Nom; d.DatePiloteGpu = gpu.DatePilote; }
            d.RedemarrageAttente = InfosPc.RedemarrageEnAttente();
            try { d.Potentiel = Potentiel.Analyser(); } catch { }
            try { d.AccesDistants = Bouclier.ASignaler(); } catch { }
            return d;
        }

        /// <summary>Points du bilan, dans l'ordre où ils sont vérifiés.</summary>
        public static List<PointBilanPc> Points(DonneesBilan d)
        {
            var pts = new List<PointBilanPc>();
            Action<string, string, string, string> Point = (etat, texte, bouton, cible) => pts.Add(new PointBilanPc { Etat = etat, Texte = texte, Bouton = bouton, Cible = cible });
            string sd = Environment.GetEnvironmentVariable("SystemDrive") ?? "C:";
            if (d.DisqueTotal > 0)
            {
                double pct = 100 * d.DisqueLibre / d.DisqueTotal;
                string txt = "Disque " + sd + " " + Format.Taille(d.DisqueLibre) + " libres (" + pct.ToString("0", Appli.Fr) + " %)";
                if (pct < 10) { Point("probleme", txt + " : presque plein, Windows ralentit.", "Libérer de l'espace", "action:complet"); }
                else if (pct < 20) { Point("attention", txt + " : il commence à se remplir.", "Nettoyer", "action:standard"); }
                else Point("ok", txt, null, null);
            }
            long recup = d.Corbeille + d.Temp;
            if (recup >= 1073741824L) { Point("attention", Format.Taille(recup) + " récupérables tout de suite (corbeille et fichiers temporaires).", "Nettoyer", "action:standard"); }
            else Point("ok", "Corbeille et fichiers temporaires : " + Format.Taille(recup), null, null);
            if (!d.DernierJournal.HasValue) { Point("attention", "Aucun nettoyage effectué avec Nettoyage PC pour l'instant.", "Nettoyer", "action:standard"); }
            else
            {
                int jours = (int)(DateTime.Now - d.DernierJournal.Value).TotalDays;
                if (jours > 14) { Point("attention", "Dernière action il y a " + jours + " jours.", "Nettoyer", "action:standard"); }
                else Point("ok", "Dernière action : " + d.DernierJournal.Value.ToString("dd/MM/yyyy HH:mm"), null, null);
            }
            // matériel bridé (écran à 60 Hz, écran sur la carte mère, Resizable BAR...) : la mémoire bridée a déjà son propre point
            foreach (var p in d.Potentiel.Where(x => !x.Ok && x.Cle != "expo")) { Point("attention", p.Titre + (p.Gain != "" ? " (" + p.Gain + ")" : ""), "Corriger", "page:Potentiel"); }
            if (d.Tache) Point("ok", "Entretien automatique activé (chaque dimanche).", null, null);
            else { Point("attention", "Entretien automatique désactivé : ton PC ne se nettoie pas tout seul.", "Activer", "action:planifier"); }
            if (d.RamTotal > 0)
            {
                double pr = 100 * (d.RamTotal - d.RamLibre) / d.RamTotal;
                if (pr >= 85) { Point("attention", "Mémoire vive utilisée à " + pr.ToString("0") + " %.", "Libérer la RAM", "action:ramlib"); }
                else Point("ok", "Mémoire vive utilisée à " + pr.ToString("0") + " %.", null, null);
            }
            if (d.Demarre.HasValue && (DateTime.Now - d.Demarre.Value).TotalDays >= 7)
            {
                Point("attention", "PC non redémarré depuis " + (DateTime.Now - d.Demarre.Value).TotalDays.ToString("0") + " jours : un redémarrage le rendra plus rapide.", "Redémarrer", "code:redemarrer");
            }
            if (d.NbDemarrage >= 10) { Point("attention", d.NbDemarrage + " programmes se lancent au démarrage : Windows démarre plus lentement.", "Gérer", "page:Demarrage"); }
            else Point("ok", d.NbDemarrage + " programme(s) au démarrage.", "Gérer", "page:Demarrage");
            if (d.Malades.Count > 0) { Point("probleme", "Disque en mauvaise santé : " + string.Join(", ", d.Malades) + ". Sauvegarde tes données.", "Voir les disques", "page:Materiel"); }
            else Point("ok", "Disques en bonne santé.", "Détails", "page:Materiel");
            if (d.MpPresent && !d.MpTempsReel && d.AutresAv.Count == 0) { Point("probleme", "Protection antivirus en temps réel désactivée.", "Ouvrir Sécurité Windows", "uri:windowsdefender://threat"); }
            else if (d.MpPresent && d.MpTempsReel && d.MpAge >= 7) { Point("attention", "Signatures antivirus vieilles de " + d.MpAge + " jours.", "Mettre à jour", "action:antivirus"); }
            else if (d.AutresAv.Count > 0) Point("ok", "Antivirus actif : " + string.Join(", ", d.AutresAv), null, null);
            else Point("ok", "Antivirus Microsoft Defender actif et à jour.", null, null);
            foreach (var a in d.AccesDistants)
            {
                Point(a.Connexions > 0 ? "probleme" : "attention", Langue.F(a.Connexions > 0 ? "{0} est ouvert et connecté : quelqu'un peut voir ton écran." : "{0} est ouvert : il permet de prendre la main sur ton PC.", a.Nom), "Vérifier", "code:bouclier");
            }
            if (d.PareFeuOff.Count > 0) { Point("probleme", "Pare-feu désactivé (" + string.Join(", ", d.PareFeuOff) + ").", "Ouvrir le pare-feu", "uri:windowsdefender://network"); }
            else Point("ok", "Pare-feu Windows actif.", null, null);
            if (d.PointConnu || d.PointDate.HasValue)
            {
                if (!d.PointDate.HasValue) { Point("attention", "Aucun point de restauration : impossible de revenir en arrière en cas de problème.", "Créer", "action:point"); }
                else if ((DateTime.Now - d.PointDate.Value).TotalDays > 30) { Point("attention", "Dernier point de restauration le " + d.PointDate.Value.ToString("dd/MM/yyyy") + ".", "Créer", "action:point"); }
                else Point("ok", "Point de restauration du " + d.PointDate.Value.ToString("dd/MM/yyyy") + ".", null, null);
            }
            if (d.RedemarrageAttente) { Point("attention", "Des mises à jour Windows attendent un redémarrage.", null, null); }
            if (d.DatePiloteGpu.HasValue)
            {
                int mois = (int)Math.Floor((DateTime.Now - d.DatePiloteGpu.Value).TotalDays / 30.4);
                if (mois >= 4) { Point("attention", "Pilote graphique (" + d.NomGpu + ") vieux de " + mois + " mois : une version plus récente existe sûrement.", "Mettre à jour", "page:Pilotes"); }
                else Point("ok", "Pilote graphique récent (" + d.NomGpu + ").", "Voir", "page:Pilotes");
            }
            if (d.RamPrevue > 0 && d.RamReelle > 0 && d.RamReelle < d.RamPrevue - 100)
                { Point("attention", "Mémoire vive à " + d.RamReelle + " MT/s au lieu de " + d.RamPrevue + " MT/s : active le profil EXPO / XMP dans le BIOS pour plus de performances.", "Comment faire", "page:Materiel"); }
            return pts;
        }

        /// <summary>Note sur 100 : -20 par problème, -7 par point à améliorer.</summary>
        public static int Score(IEnumerable<string> etats)
        {
            int score = 100;
            foreach (var e in etats) score -= e == "probleme" ? 20 : e == "attention" ? 7 : 0;
            return Math.Max(0, Math.Min(100, score));
        }

        /// <summary>Santé des quatre secteurs de l'anneau (stockage, mémoire, sécurité, matériel), de 0 à 100 ; -1 si inconnue.</summary>
        public static double[] Secteurs(DonneesBilan d)
        {
            var s = new double[4];
            Func<double, double> borne = v => Math.Max(0, Math.Min(100, v));
            s[0] = d.DisqueTotal > 0 ? borne(100.0 * d.DisqueLibre / d.DisqueTotal / 25 * 100) : -1;
            double pr = d.RamTotal > 0 ? 100 * (d.RamTotal - d.RamLibre) / d.RamTotal : -1;
            s[1] = pr < 0 ? -1 : borne(pr <= 70 ? 100 : 100 - (pr - 70) * 2.4);
            double secu = 100;
            if (d.MpPresent && !d.MpTempsReel && d.AutresAv.Count == 0) secu -= 60;
            else if (d.MpPresent && d.MpTempsReel && d.MpAge >= 7) secu -= 15;
            if (d.PareFeuOff.Count > 0) secu -= 40;
            if (d.AccesDistants.Count > 0) secu -= 30;
            if (d.PointConnu && !d.PointDate.HasValue) secu -= 10;
            else if (d.PointDate.HasValue && (DateTime.Now - d.PointDate.Value).TotalDays > 30) secu -= 5;
            s[2] = borne(secu);
            double mat = 100;
            if (d.Malades.Count > 0) mat -= 60;
            if (d.DatePiloteGpu.HasValue && (DateTime.Now - d.DatePiloteGpu.Value).TotalDays >= 120) mat -= 15;
            if (d.RamPrevue > 0 && d.RamReelle > 0 && d.RamReelle < d.RamPrevue - 100) mat -= 15;
            mat -= 10 * d.Potentiel.Count(x => !x.Ok && x.Cle != "expo");
            if (d.RedemarrageAttente) mat -= 5;
            s[3] = borne(mat);
            return s;
        }
    }
}
