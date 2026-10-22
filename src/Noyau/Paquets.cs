// Nettoyage PC - applications du Microsoft Store (paquets de l'utilisateur), sans PowerShell
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NettoyagePC
{
    internal class Paquet
    {
        public string NomComplet = "", Nom = "", Editeur = "", Version = "", Dossier = "", Famille = "", NomAffiche = "", AppId = "";
        public bool Cadre, Systeme;
    }

    internal static class Paquets
    {
        private const string Depot = @"HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
        public static readonly Regex NomCompletValide = new Regex(@"^[A-Za-z0-9][\w.\-]{1,100}_[0-9.]{3,30}_(x86|x64|arm|arm64|neutral)_[\w.\-~]*_[a-z0-9]{13}$");

        /// <summary>Paquets installés pour l'utilisateur (registre du dépôt des applications).</summary>
        public static List<Paquet> Lister(bool lireManifeste = true)
        {
            var l = new List<Paquet>();
            foreach (var n in Registre.SousCles(Depot))
            {
                try
                {
                    if (!NomCompletValide.IsMatch(n)) continue;
                    var morceaux = n.Split('_');
                    var p = new Paquet { NomComplet = n, Nom = morceaux[0], Version = morceaux[1], Famille = morceaux[0] + "_" + morceaux[morceaux.Length - 1] };
                    p.Dossier = Registre.Texte(Depot + "\\" + n, "PackageRootFolder");
                    if (n.Contains("_split.")) continue;   // paquets de ressources (langues, tailles d'écran)
                    p.Systeme = p.Dossier.IndexOf(@"\SystemApps\", StringComparison.OrdinalIgnoreCase) >= 0;
                    string affiche = Registre.Texte(Depot + "\\" + n, "DisplayName");
                    p.NomAffiche = affiche.StartsWith("@") || affiche == "" ? p.Nom : affiche;
                    if (lireManifeste && p.Dossier != "") LireManifeste(p);
                    l.Add(p);
                }
                catch { }
            }
            // une seule version de chaque application (la plus récente)
            return l.GroupBy(p => p.Famille, StringComparer.OrdinalIgnoreCase).Select(g => g.OrderByDescending(p => VersionNum(p.Version)).First()).ToList();
        }

        private static Version VersionNum(string v) { Version r; return System.Version.TryParse(v, out r) ? r : new Version(0, 0); }

        private static void LireManifeste(Paquet p)
        {
            string f = Path.Combine(p.Dossier, "AppxManifest.xml");
            if (!File.Exists(f)) return;
            var x = XDocument.Load(f);
            var props = x.Root.Elements().FirstOrDefault(e => e.Name.LocalName == "Properties");
            if (props != null)
            {
                var cadre = props.Elements().FirstOrDefault(e => e.Name.LocalName == "Framework");
                p.Cadre = cadre != null && cadre.Value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
                var ed = props.Elements().FirstOrDefault(e => e.Name.LocalName == "PublisherDisplayName");
                if (ed != null && !ed.Value.StartsWith("ms-resource")) p.Editeur = ed.Value.Trim();
                var dn = props.Elements().FirstOrDefault(e => e.Name.LocalName == "DisplayName");
                if (dn != null && !dn.Value.StartsWith("ms-resource") && p.NomAffiche == p.Nom) p.NomAffiche = dn.Value.Trim();
            }
            var app = x.Descendants().FirstOrDefault(e => e.Name.LocalName == "Application");
            if (app != null) p.AppId = (string)app.Attribute("Id") ?? "";
        }

        /// <summary>Désinstalle un paquet pour l'utilisateur (comme « Désinstaller » dans Paramètres). Renvoie "" si réussi.</summary>
        public static string Retirer(string nomComplet)
        {
            if (!NomCompletValide.IsMatch(nomComplet)) return "nom de paquet invalide";
            int code;
            string sortie = Programmes.Executer(Programmes.Ps, "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"$ErrorActionPreference='Stop'; try { Remove-AppxPackage -Package '" + nomComplet + "'; exit 0 } catch { Write-Output $_.Exception.Message; exit 1 }\"", 180000, null, out code);
            if (code == 0) return "";
            sortie = sortie.Trim();
            return sortie == "" ? "Windows a refusé la désinstallation" : sortie.Split('\n')[0].Trim();
        }
    }
}
