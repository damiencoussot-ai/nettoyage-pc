// Velox - « Mon PC » : fiche claire du matériel (processeur, mémoire, carte graphique, disques, carte mère,
// Windows), âge approximatif du PC, état de chaque pièce et conseils d'amélioration concrets. Lecture seule.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NettoyagePC
{
    internal class PieceMonPc
    {
        public string Categorie = "", Nom = "", Detail = "";
        /// <summary>0 : bien, 1 : à surveiller, 2 : problème.</summary>
        public int Etat;
    }

    internal class ConseilMonPc
    {
        public string Titre = "", Texte = "";
        /// <summary>1 : le plus utile d'abord.</summary>
        public int Priorite = 2;
    }

    internal class FicheMonPc
    {
        public string Resume = "";
        public int AgeAnnees = -1;
        public List<PieceMonPc> Pieces = new List<PieceMonPc>();
        public List<ConseilMonPc> Conseils = new List<ConseilMonPc>();
    }

    internal static class MonPc
    {
        /// <summary>Lecture complète (quelques secondes : WMI). À appeler hors du fil de l'interface.</summary>
        public static FicheMonPc Lire()
        {
            var f = new FicheMonPc();
            InfosMateriel m = null;
            try { m = InfosMateriel.Lire(); } catch { }
            bool portable = false;
            try { portable = InfosPc.EstPortable; } catch { }

            // Windows et âge
            DateTime? installation = null, bios = null;
            try { var o = Wmi.Requete("SELECT InstallDate FROM Win32_OperatingSystem").FirstOrDefault(); if (o != null) installation = Wmi.Date(o, "InstallDate"); } catch { }
            try { var o = Wmi.Requete("SELECT ReleaseDate FROM Win32_BIOS").FirstOrDefault(); if (o != null) bios = Wmi.Date(o, "ReleaseDate"); } catch { }
            string edition = "";
            try { edition = InfosPc.Edition; } catch { }
            bool w10 = Natif.Build > 0 && Natif.Build < 22000;
            f.Pieces.Add(new PieceMonPc
            {
                Categorie = "Windows", Nom = edition != "" ? edition : "Windows", Detail = "build " + Natif.Build + (installation.HasValue ? " · " + Langue.F("installé le {0}", installation.Value.ToString("dd/MM/yyyy")) : ""),
                Etat = w10 ? 1 : 0,
            });

            // processeur
            string cpu = m != null ? m.Processeur : "";
            f.Pieces.Add(new PieceMonPc { Categorie = "Processeur", Nom = cpu != "" ? cpu : Langue.T("Non détecté"), Detail = Langue.F("{0} cœurs logiques", Environment.ProcessorCount) });

            // mémoire vive
            double ram = 0;
            try { ram = InfosPc.RamGo; } catch { }
            var pm = new PieceMonPc { Categorie = "Mémoire vive", Nom = m != null && m.Barrettes.Count > 0 ? m.TitreMemoire() : Math.Round(ram) + " Go", Detail = m != null && m.Barrettes.Count > 0 ? Langue.F("{0} barrette(s)", m.Barrettes.Count) : "" };
            if (ram > 0 && ram < 15) pm.Etat = 1;
            if (m != null && m.Bridee) pm.Etat = 1;
            f.Pieces.Add(pm);

            // carte graphique
            CarteGraphique gpu = null;
            try { gpu = InfosPc.CarteGraphiquePrincipale(); } catch { }
            f.Pieces.Add(new PieceMonPc { Categorie = "Carte graphique", Nom = gpu != null ? gpu.Nom : Langue.T("Non détectée"), Detail = gpu != null ? (gpu.Integree ? Langue.T("intégrée au processeur") : Langue.T("carte dédiée")) + (gpu.VersionPilote != "" ? " · " + Langue.F("pilote {0}", gpu.VersionPilote) : "") : "" });

            // carte mère
            if (m != null && m.CarteMere != "") f.Pieces.Add(new PieceMonPc { Categorie = "Carte mère", Nom = m.CarteMere, Detail = m.Bios != "" ? "BIOS " + m.Bios : "" });

            // disques
            bool hddSysteme = false; int usureMax = 0; bool disqueMalade = false;
            if (m != null)
                foreach (var d in m.Disques)
                {
                    var p = new PieceMonPc { Categorie = "Disque", Nom = d.Nom, Detail = d.Type + " · " + Format.Taille(d.Taille) };
                    if (d.Usure.HasValue) { p.Detail += " · " + Langue.F("usure {0} %", Math.Round(d.Usure.Value)); usureMax = Math.Max(usureMax, (int)d.Usure.Value); if (d.Usure.Value >= 80) p.Etat = 1; }
                    if (!d.EnBonneSante) { p.Etat = 2; disqueMalade = true; p.Detail += " · " + Langue.T("santé à vérifier"); }
                    if (d.Type == "HDD") hddSysteme |= m.Disques.Count == 1;
                    f.Pieces.Add(p);
                }
            double librePct = -1;
            try
            {
                var dq = new DriveInfo(Environment.GetEnvironmentVariable("SystemDrive") ?? "C:");
                if (dq.IsReady && dq.TotalSize > 0)
                {
                    librePct = 100.0 * dq.AvailableFreeSpace / dq.TotalSize;
                    f.Pieces.Add(new PieceMonPc { Categorie = "Disque de Windows", Nom = dq.Name.TrimEnd('\\'), Detail = Langue.F("{0} libres sur {1}", Format.Taille(dq.AvailableFreeSpace), Format.Taille(dq.TotalSize)), Etat = librePct < 10 ? 2 : librePct < 15 ? 1 : 0 });
                }
            }
            catch { }

            // âge (d'après la date du BIOS d'origine, sinon de l'installation de Windows)
            var origine = bios ?? installation;
            if (origine.HasValue && origine.Value.Year > 2000) f.AgeAnnees = Math.Max(0, (int)Math.Floor((DateTime.Now - origine.Value).TotalDays / 365.25));
            f.Resume = Langue.T(portable ? "PC portable" : "PC fixe") + (f.AgeAnnees >= 0 ? " · " + (f.AgeAnnees == 0 ? Langue.T("moins d'un an") : Langue.F("environ {0} an(s)", f.AgeAnnees)) : "")
                + (ram > 0 ? " · " + Math.Round(ram) + " Go " + Langue.T("de mémoire vive") : "");

            // conseils, du plus utile au moins urgent
            if (disqueMalade) Conseil(f, 1, "Sauvegarde tes fichiers", "Un disque signale un problème de santé. Copie tes documents et photos sur un autre support, puis prévois son remplacement.");
            if (hddSysteme) Conseil(f, 1, "Passe à un SSD", "Windows est sur un disque dur classique. Un SSD rend le démarrage et l'ouverture des applications 3 à 5 fois plus rapides : c'est l'amélioration la plus visible.");
            if (ram > 0 && ram < 7.5) Conseil(f, 1, "Ajoute de la mémoire vive", "Moins de 8 Go, c'est trop juste pour Windows 11 et un navigateur. Passer à 16 Go évite les ralentissements quand plusieurs applications sont ouvertes.");
            else if (ram >= 7.5 && ram < 15) Conseil(f, 2, "Pense à 16 Go de mémoire vive", "8 Go suffisent pour la bureautique, mais les jeux récents et les nombreux onglets profitent de 16 Go.");
            if (m != null && m.Bridee) Conseil(f, 1, Langue.F("Active le profil {0}", m.Profil), "Tes barrettes tournent moins vite que prévu. Le guide pas à pas est dans Outils > Matériel > Mémoire vive et BIOS.");
            if (librePct >= 0 && librePct < 15) Conseil(f, 1, "Libère de la place sur le disque de Windows", "Moins de 15 % libres ralentit Windows et ses mises à jour. Lance un nettoyage, ou ajoute un second disque pour tes jeux et vidéos.");
            if (usureMax >= 80 && !disqueMalade) Conseil(f, 2, "Ton SSD vieillit", "Il a consommé une grande partie de son endurance. Garde une sauvegarde à jour et prévois son remplacement.");
            if (w10) Conseil(f, 2, "Passe à Windows 11", "Windows 10 ne reçoit plus de mises à jour de sécurité gratuites. La page Compatibilité de ce PC dit si ton matériel peut passer à Windows 11.");
            if (gpu != null && gpu.Integree && !portable) Conseil(f, 3, "Pour les jeux : une carte graphique dédiée", "La puce graphique intégrée suffit pour la vidéo et la bureautique ; pour jouer, une carte graphique dédiée change tout.");
            if (f.AgeAnnees >= 8) Conseil(f, 3, "Un PC de plus de 8 ans", "S'il te convient, garde-le : un SSD et de la mémoire vive lui donnent souvent une seconde jeunesse.");
            f.Conseils = f.Conseils.OrderBy(c => c.Priorite).ToList();
            return f;
        }

        private static void Conseil(FicheMonPc f, int priorite, string titre, string texte) { f.Conseils.Add(new ConseilMonPc { Priorite = priorite, Titre = titre, Texte = texte }); }
    }
}
