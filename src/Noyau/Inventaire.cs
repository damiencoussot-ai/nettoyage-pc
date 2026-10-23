// Nettoyage PC - inventaire sans interface : matériel (mémoire, carte mère, BIOS, disques) et éléments du démarrage.
// Partagé par l'interface de la version 4 et par la nouvelle interface (version 5).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class InfosMateriel
    {
        public List<Barrette> Barrettes = new List<Barrette>();
        public string CarteMere = "", MarqueCarteMere = "", Bios = "", Processeur = "";
        public int Prevue, Reelle;
        public List<DisquePhysique> Disques = new List<DisquePhysique>();
        public bool AMD { get { return Processeur.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0; } }
        public bool Bridee { get { return Prevue > 0 && Reelle > 0 && Reelle < Prevue - 100; } }
        public string Profil { get { return AMD && Barrettes.Any(b => b.TypeSmbios == 34) ? "EXPO" : AMD && MarqueCarteMere == "ASUS" ? "D.O.C.P." : "XMP"; } }

        public static InfosMateriel Lire()
        {
            var i = new InfosMateriel();
            try { i.Barrettes = Barrette.Lister(); i.Prevue = Barrette.VitessePrevue(i.Barrettes); i.Reelle = Barrette.VitesseReelle(i.Barrettes); } catch { }
            try
            {
                var b = Wmi.Requete("SELECT Manufacturer, Product FROM Win32_BaseBoard").FirstOrDefault();
                if (b != null)
                {
                    string fab = Wmi.Texte(b, "Manufacturer"), prod = Wmi.Texte(b, "Product");
                    i.CarteMere = (fab + " " + prod).Trim();
                    string t = fab + " " + prod;
                    i.MarqueCarteMere = Regex.IsMatch(t, "ASUS", RegexOptions.IgnoreCase) ? "ASUS" : Regex.IsMatch(t, "Micro-Star|MSI", RegexOptions.IgnoreCase) ? "MSI"
                        : Regex.IsMatch(t, "Gigabyte|AORUS", RegexOptions.IgnoreCase) ? "Gigabyte" : Regex.IsMatch(t, "ASRock", RegexOptions.IgnoreCase) ? "ASRock"
                        : Regex.IsMatch(t, "Dell|Alienware|HP|Hewlett|Lenovo|Acer|Medion|Fujitsu", RegexOptions.IgnoreCase) ? "Marque" : "";
                }
                var bios = Wmi.Requete("SELECT SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS").FirstOrDefault();
                if (bios != null) { i.Bios = Wmi.Texte(bios, "SMBIOSBIOSVersion"); var d = Wmi.Date(bios, "ReleaseDate"); if (d.HasValue) i.Bios += " du " + d.Value.ToString("dd/MM/yyyy"); }
            }
            catch { }
            i.Processeur = InfosPc.Processeur();
            try { i.Disques = DisquePhysique.Lister(false); } catch { }
            return i;
        }

        /// <summary>Titre de la mémoire vive : « 32 Go DDR5 · 6000 MT/s » (ou « bridée à … »).</summary>
        public string TitreMemoire()
        {
            if (Barrettes.Count == 0) return "Barrettes non détectées";
            string debut = Format.Taille(Barrettes.Sum(x => x.Capacite)) + " " + (Barrettes.Select(x => x.Type).FirstOrDefault() ?? "");
            if (Bridee) return debut + " · bridée à " + Reelle + " MT/s";
            return debut + " · " + (Reelle > 0 ? Reelle + " MT/s" : "vitesse inconnue");
        }

        /// <summary>Explication de la vitesse de la mémoire vive. niveau : 0 neutre, 1 à corriger, 2 tout va bien.</summary>
        public string ExplicationMemoire(out int niveau)
        {
            niveau = 0;
            if (Barrettes.Count == 0) return "Windows ne donne pas d'information sur les barrettes de ce PC (machine virtuelle ?).";
            string t;
            if (Bridee)
            {
                int gain = (int)Math.Round(100.0 * (Prevue - Reelle) / Reelle);
                t = "Tes barrettes sont prévues pour " + Prevue + " MT/s mais tournent à " + Reelle + " MT/s : le profil " + Profil +
                    " n'est pas activé dans le BIOS. L'activer donne environ " + gain + " % de bande passante mémoire en plus (plus de FPS minimum dans les jeux, Windows plus réactif). C'est un réglage prévu par le fabricant, sans risque pour les barrettes.";
                niveau = 1;
            }
            else if (Prevue > 0) { t = "✔ Les barrettes tournent à leur vitesse prévue (" + Prevue + " MT/s) : le profil " + Profil + " est bien activé."; niveau = 2; }
            else t = "La vitesse prévue par le fabricant n'est pas indiquée dans la référence des barrettes : vérifie sur la boîte ou l'étiquette (par ex. « 6000 » ou « 3200 »). Si elle est plus élevée que " + (Reelle > 0 ? Reelle + " MT/s" : "la vitesse actuelle") + ", active le profil " + Profil + " dans le BIOS.";
            if (Barrettes.Count == 1 && Barrettes.Sum(x => x.Capacite) >= 8L * 1073741824) t += "\nUne seule barrette : une deuxième identique (double canal) peut nettement améliorer les performances.";
            return t;
        }

        /// <summary>Guide pas à pas pour activer le profil de la mémoire vive (EXPO / XMP / D.O.C.P.), selon la marque de la carte mère.</summary>
        public string GuideProfil()
        {
            string p = Profil, etapes;
            switch (MarqueCarteMere)
            {
                case "ASUS":
                    etapes = "2. Appuie sur F7 pour passer en mode avancé (« Advanced Mode »), si l'écran est simplifié.\n3. Onglet « Ai Tweaker » : réglage « Ai Overclock Tuner », choisis « " + (p == "XMP" ? "XMP I" : p == "EXPO" ? "EXPO I" : "D.O.C.P.") + " ».\n   (En mode simple « EZ Mode », c'est le réglage « " + p + " » à droite de l'écran.)\n4. Appuie sur F10, puis « OK » : le PC enregistre et redémarre.";
                    break;
                case "MSI":
                    etapes = "2. En haut de l'écran principal, clique sur le bouton « " + (p == "XMP" ? "XMP" : "EXPO") + " » (ou « A-XMP ») pour l'allumer, profil 1.\n3. Appuie sur F10, puis « Yes » : le PC enregistre et redémarre.";
                    break;
                case "Gigabyte":
                    etapes = "2. Appuie sur F2 pour passer en mode avancé, si l'écran est simplifié.\n3. Onglet « Tweaker » : réglage « Extreme Memory Profile (X.M.P.) »" + (p == "EXPO" ? " ou « EXPO »" : "") + ", choisis « Profile 1 ».\n4. Appuie sur F10, puis « Save & Exit » : le PC enregistre et redémarre.";
                    break;
                case "ASRock":
                    etapes = "2. Appuie sur F6 pour passer en mode avancé, si l'écran est simplifié.\n3. Onglet « OC Tweaker », partie « DRAM Configuration » : « Load " + (p == "EXPO" ? "EXPO" : "XMP") + " Setting », choisis « Profile 1 ».\n4. Appuie sur F10, puis « Yes » : le PC enregistre et redémarre.";
                    break;
                case "Marque":
                    etapes = "2. Les PC de marque (Dell, HP, Lenovo...) ne proposent pas toujours ce réglage. Cherche dans les menus « Memory », « Performance » ou « Overclocking » un réglage « XMP », « EXPO » ou « Memory Profile » et choisis « Profile 1 ».\n3. Enregistre avec F10 (« Save & Exit »). Si tu ne trouves pas ce réglage, ton PC ne le permet pas : rien à faire.";
                    break;
                default:
                    etapes = "2. Cherche dans les menus « OC », « Tweaker », « Memory » ou « Overclocking » le réglage « " + p + " » (ou « XMP », « Memory Profile ») et choisis « Profile 1 ».\n3. Appuie sur F10 pour enregistrer (« Save & Exit ») : le PC redémarre.";
                    break;
            }
            return "Activer le profil " + p + " de ta mémoire vive" + (CarteMere != "" ? " (carte mère " + CarteMere + ")" : "") + " :\n\n" +
                "1. Clique sur « Redémarrer dans le BIOS » (ou redémarre et appuie plusieurs fois sur Suppr ou F2 dès l'allumage).\n" + etapes + "\n\n" +
                "Bon à savoir :\n- Au premier démarrage après le changement, l'écran peut rester noir 1 à 3 minutes : le PC « apprend » la mémoire, n'éteins pas.\n" +
                "- Si le PC ne démarre plus (rare), il revient tout seul aux réglages d'origine après 2 ou 3 essais ; sinon, choisis « Load Optimized Defaults » dans le BIOS.\n" +
                "- Reviens ensuite sur cette page : elle doit afficher " + (Prevue > 0 ? Prevue + " MT/s" : "la nouvelle vitesse") + ".";
        }
    }

    internal class ElementDemarrage { public string Nom, Commande, Lieu, Appro, NomAppro; public bool Actif; }

    internal static class ElementsDemarrage
    {
        public static List<ElementDemarrage> Lister()
        {
            var l = new List<ElementDemarrage>();
            foreach (var s in GestionDemarrage.Sources)
                foreach (var n in Registre.Valeurs(s.Cle))
                {
                    if (string.IsNullOrEmpty(n)) continue;
                    l.Add(new ElementDemarrage { Nom = n, Commande = Convert.ToString(Registre.Lire(s.Cle, n)), Lieu = s.Lieu, Appro = s.Appro, NomAppro = n, Actif = GestionDemarrage.EstApprouve(s.Appro, n) });
                }
            foreach (var d in new[]
            {
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.Startup), GestionDemarrage.ApproDossierUtilisateur, "Dossier Démarrage (ton compte)"),
                Tuple.Create(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup), GestionDemarrage.ApproDossierTous, "Dossier Démarrage (tous)")
            })
            {
                if (string.IsNullOrEmpty(d.Item1) || !Directory.Exists(d.Item1)) continue;
                try
                {
                    foreach (var f in new DirectoryInfo(d.Item1).GetFiles())
                    {
                        if (f.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) continue;
                        l.Add(new ElementDemarrage { Nom = Path.GetFileNameWithoutExtension(f.Name), Commande = f.FullName, Lieu = d.Item3, Appro = d.Item2, NomAppro = f.Name, Actif = GestionDemarrage.EstApprouve(d.Item2, f.Name) });
                    }
                }
                catch { }
            }
            return l;
        }
    }

    /// <summary>Résumé du PC en texte (Windows, processeur, mémoire, disques, carte graphique, antivirus), à coller dans un message.
    /// Ni mot de passe ni fichier personnel.</summary>
    internal static class ResumeDuPc
    {
        public static string Creer()
        {
            var sb = new StringBuilder();
            Action<string> l = s => sb.AppendLine(s);
            l("Résumé du PC « " + Environment.MachineName + " » - Nettoyage PC " + Appli.Version + " - " + DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
            l("Windows : " + InfosPc.Edition + " (build " + Natif.Build + ", " + (Environment.Is64BitOperatingSystem ? "64" : "32") + " bits)" + (InfosPc.EstPortable ? ", PC portable" : ""));
            try { l("Démarré depuis : " + Math.Floor(TimeSpan.FromMilliseconds(Environment.TickCount & int.MaxValue).TotalHours) + " h" + (InfosPc.RedemarrageEnAttente() ? " (redémarrage en attente)" : "")); } catch { }
            l("Processeur : " + InfosPc.Processeur());
            try
            {
                var m = InfosMateriel.Lire();
                long total = m.Barrettes.Sum(b => b.Capacite);
                string ram = Format.Taille(total > 0 ? total : InfosPc.RamGo * 1073741824.0) + (m.Barrettes.Count > 0 ? " " + m.Barrettes[0].Type + ", " + m.Barrettes.Count + " barrette(s)" : "");
                if (m.Reelle > 0) ram += ", " + m.Reelle + " MT/s" + (m.Bridee ? " (bridée : prévue pour " + m.Prevue + ", profil " + m.Profil + " désactivé)" : "");
                l("Mémoire vive : " + ram);
                if (m.CarteMere != "") l("Carte mère : " + m.CarteMere + (m.Bios != "" ? " (BIOS " + m.Bios + ")" : ""));
                foreach (var d in m.Disques) l("Disque : " + d.Nom + " - " + d.Type + ", " + Format.Taille(d.Taille) + ", santé " + d.Sante);
            }
            catch { }
            foreach (var g in InfosPc.CartesGraphiques()) l("Carte graphique : " + g.Nom + " (pilote " + g.VersionPilote + (g.DatePilote.HasValue ? " du " + g.DatePilote.Value.ToString("dd/MM/yyyy") : "") + ")");
            foreach (var v in DriveInfo.GetDrives())
            {
                try { if (v.DriveType == DriveType.Fixed && v.IsReady) l("Lecteur " + v.Name.Substring(0, 2) + " " + Format.Taille(v.AvailableFreeSpace) + " libres sur " + Format.Taille(v.TotalSize)); } catch { }
            }
            var av = InfosPc.Antivirus();
            l("Antivirus : " + (av.Count > 0 ? string.Join(", ", av) : "non détecté"));
            return sb.ToString();
        }
    }
}
