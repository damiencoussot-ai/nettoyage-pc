// Nettoyage PC - inventaire sans interface : matériel (mémoire, carte mère, BIOS, disques) et éléments du démarrage.
// Partagé par l'interface de la version 4 et par la nouvelle interface (version 5).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
}
