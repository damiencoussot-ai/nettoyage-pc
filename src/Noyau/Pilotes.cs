// Nettoyage PC - pilotes : carte graphique (outil officiel du fabricant) et recherche Windows Update avec tri intelligent
// (ce qui est recommandé est coché, le reste est expliqué). Partagé par les deux interfaces.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class MajWindows
    {
        public string Id = "", Titre = "", Classe = "", Fournisseur = "", Kb = "";
        public double Taille;
        public bool Facultative, Pilote, Redemarrage;
        public DateTime? DatePilote;
        public List<string> Categories = new List<string>();
    }

    internal class ClassementMaj { public string Type = "Windows", Conseil = "Recommandé"; public bool Coche; public int Ordre = 3; }

    /// <summary>Outil officiel du fabricant de la carte graphique (le plus sûr pour les pilotes graphiques).</summary>
    internal class OutilPilote
    {
        public string Exe = "", Nom = "", Site = "";

        /// <summary>Texte du bouton : « Ouvrir AMD Software », « Télécharger NVIDIA App »...</summary>
        public string Bouton { get { return Exe != "" ? "Ouvrir " + Nom : Nom != "" ? "Télécharger " + Nom : "Pilotes facultatifs de Windows"; } }

        public static OutilPilote Pour(string marque)
        {
            var o = new OutilPilote();
            string pf = Chemins.ProgramFiles;
            switch (marque)
            {
                case "AMD":
                    o.Nom = "AMD Software"; o.Site = "https://www.amd.com/fr/support/download/drivers.html";
                    if (File.Exists(Path.Combine(pf, @"AMD\CNext\CNext\RadeonSoftware.exe"))) o.Exe = Path.Combine(pf, @"AMD\CNext\CNext\RadeonSoftware.exe");
                    break;
                case "NVIDIA":
                    o.Nom = "NVIDIA App"; o.Site = "https://www.nvidia.com/fr-fr/software/nvidia-app/";
                    foreach (var c in new[] { Path.Combine(pf, @"NVIDIA Corporation\NVIDIA app\CEF\NVIDIA app.exe"), Path.Combine(pf, @"NVIDIA Corporation\NVIDIA GeForce Experience\NVIDIA GeForce Experience.exe") })
                        if (File.Exists(c)) { o.Exe = c; if (c.Contains("GeForce")) o.Nom = "GeForce Experience"; break; }
                    break;
                case "Intel":
                    o.Nom = "Intel Driver & Support Assistant"; o.Site = "https://www.intel.fr/content/www/fr/fr/support/detect.html";
                    break;
                default: o.Site = "ms-settings:windowsupdate-optionalupdates"; break;
            }
            return o;
        }

        /// <summary>Message affiché quand l'outil du fabricant s'ouvre.</summary>
        public string Explication { get { return Nom + " s'ouvre.\n\nClique sur « Rechercher des mises à jour » (ou la section Pilotes), puis installe la version proposée.\nC'est l'outil officiel du fabricant : il installe le bon pilote pour ta carte, sans risque. L'écran peut clignoter pendant l'installation."; } }
    }

    internal static class Pilotes
    {
        /// <summary>Âge et conseil pour le pilote de la carte graphique principale. vieux : plus de 4 mois.</summary>
        public static string ResumeGpu(List<CarteGraphique> gpus, out bool vieux)
        {
            vieux = false;
            if (gpus == null || gpus.Count == 0) return "Écran virtuel ou machine virtuelle : les pilotes sont gérés par Windows Update.";
            var g = gpus[0];
            string txt = "Pilote " + g.VersionPilote;
            if (g.DatePilote.HasValue)
            {
                int mois = (int)Math.Floor((DateTime.Now - g.DatePilote.Value).TotalDays / 30.4);
                txt += " du " + g.DatePilote.Value.ToString("dd/MM/yyyy");
                if (mois >= 1) txt += " (il y a " + mois + " mois)";
                vieux = mois >= 4;
            }
            txt += vieux ? ". Les fabricants sortent un nouveau pilote presque chaque mois : une version plus récente existe sûrement (meilleures performances dans les jeux, corrections)." : ". Pilote récent.";
            if (gpus.Count > 1) txt += "  ·  Autre(s) : " + string.Join(", ", gpus.Skip(1).Select(x => x.Nom));
            return txt;
        }

        /// <summary>Demande à Windows Update les mises à jour et pilotes non installés (lecture seule). null si Windows Update ne répond pas.</summary>
        public static List<MajWindows> ChercherMaj(out string erreur)
        {
            erreur = null;
            var l = new List<MajWindows>();
            try
            {
                dynamic session = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session"));
                session.ClientApplicationID = "Nettoyage PC";
                dynamic res = session.CreateUpdateSearcher().Search("IsInstalled=0 and IsHidden=0");
                int n = res.Updates.Count;
                for (int i = 0; i < n; i++)
                {
                    dynamic u = res.Updates.Item(i);
                    var m = new MajWindows { Id = u.Identity.UpdateID, Titre = u.Title, Taille = (double)u.MaxDownloadSize, Facultative = u.BrowseOnly, Pilote = (int)u.Type == 2 };
                    try { int nc = u.Categories.Count; for (int k = 0; k < nc; k++) m.Categories.Add((string)u.Categories.Item(k).CategoryID); } catch { }
                    try { int nk = u.KBArticleIDs.Count; var kbs = new List<string>(); for (int k = 0; k < nk; k++) kbs.Add("KB" + (string)u.KBArticleIDs.Item(k)); m.Kb = string.Join(", ", kbs); } catch { }
                    if (m.Pilote) { try { m.Classe = u.DriverClass; m.DatePilote = (DateTime)u.DriverVerDate; m.Fournisseur = u.DriverProvider; } catch { } }
                    try { m.Redemarrage = (int)u.InstallationBehavior.RebootBehavior > 0; } catch { }
                    l.Add(m);
                }
            }
            catch (Exception ex) { erreur = ex.Message; return null; }
            return l;
        }

        /// <summary>Tri intelligent : ce qui est recommandé est coché, le reste est expliqué. boutonOutil : texte du bouton de l'outil du fabricant.</summary>
        public static ClassementMaj Classer(MajWindows m, List<CarteGraphique> gpus, string boutonOutil)
        {
            var c = new ClassementMaj { Coche = !m.Facultative };
            string t = m.Titre;
            Func<string, bool> re = motif => Regex.IsMatch(t, motif, RegexOptions.IgnoreCase);
            if (m.Categories.Contains("3689bdc8-b205-4af4-8d4a-a63924c5e9d5") || re("feature update|mise à jour des fonctionnalités|Windows 1\\d, version"))
            { c.Type = "Nouvelle version"; c.Coche = false; c.Conseil = "Gros changement : à faire depuis Windows Update, quand tu as le temps"; c.Ordre = 6; }
            else if (m.Categories.Contains("e0789628-ce08-4437-be74-2495b842f43b") || re("defender|security intelligence|veille de sécurité|KB2267602"))
            { c.Type = "Antivirus"; c.Coche = true; c.Conseil = "Recommandé : protection à jour"; c.Ordre = 0; }
            else if (m.Pilote && (m.Classe == "Display" || re("display|radeon|geforce|nvidia|intel.*graphics|graphique")))
            {
                c.Type = "Pilote graphique"; c.Ordre = 4;
                var noms = new Dictionary<string, string> { { "AMD", "AMD|Advanced Micro Devices|ATI " }, { "NVIDIA", "NVIDIA" }, { "Intel", "Intel" } };
                var installe = (gpus ?? new List<CarteGraphique>()).FirstOrDefault(g => noms.ContainsKey(g.Marque) && Regex.IsMatch(t + " " + m.Fournisseur, noms[g.Marque], RegexOptions.IgnoreCase));
                if (installe != null && installe.DatePilote.HasValue && m.DatePilote.HasValue && m.DatePilote <= installe.DatePilote) { c.Coche = false; c.Conseil = "Plus ancien que ton pilote actuel : à ignorer"; }
                else if (installe != null && (installe.Marque == "AMD" || installe.Marque == "NVIDIA")) { c.Coche = false; c.Conseil = "Préfère le bouton « " + boutonOutil + " » en haut (pilote complet)"; }
                else { c.Coche = !m.Facultative; c.Conseil = "Plus récent que ton pilote actuel"; }
            }
            else if (m.Pilote) { c.Type = "Pilote"; c.Ordre = 5; if (m.Facultative) { c.Coche = false; c.Conseil = "Facultatif : utile seulement si un appareil pose problème"; } else c.Conseil = "Recommandé par Windows"; }
            else if (m.Categories.Contains("0fa1201d-4330-4fa8-8ae9-b877473b6441") || re("sécurité|security|cumulative"))
            {
                c.Type = "Sécurité"; c.Coche = true; c.Conseil = "Important : corrige des failles"; c.Ordre = 1;
                if (re("preview|aperçu|préliminaire")) { c.Type = "Windows (aperçu)"; c.Coche = false; c.Conseil = "Version d'essai : pas nécessaire"; c.Ordre = 5; }
            }
            else if (m.Categories.Contains("e6cf1350-c01b-414d-a61f-263d14d133b4")) { c.Type = "Critique"; c.Coche = true; c.Conseil = "Important"; c.Ordre = 1; }
            else if (re("preview|aperçu|préliminaire")) { c.Type = "Windows (aperçu)"; c.Coche = false; c.Conseil = "Version d'essai : pas nécessaire"; c.Ordre = 5; }
            else if (m.Facultative) { c.Coche = false; c.Conseil = "Facultative"; c.Ordre = 5; }
            else if (re("\\.NET")) { c.Type = ".NET"; c.Ordre = 2; }
            if (m.Redemarrage && c.Coche) c.Conseil += " · redémarrage";
            return c;
        }

        /// <summary>Arguments du moteur pour installer ces mises à jour (identifiants vérifiés), et la question à poser avant.</summary>
        public static string[] ArgumentsInstallation(List<MajWindows> choix, out string question)
        {
            double total = choix.Sum(m => m.Taille);
            var ids = choix.Select(m => m.Id).Where(id => Regex.IsMatch(id, "^[0-9a-fA-F-]{36}$")).ToList();
            question = "Installer " + choix.Count + " mise(s) à jour (" + Format.Taille(total) + ") ?\n\n" + string.Join("\n", choix.Take(8).Select(m => "- " + m.Titre));
            if (choix.Count > 8) question += "\n- ... et " + (choix.Count - 8) + " autre(s)";
            question += "\n\nUn point de restauration est créé d'abord. Le téléchargement et l'installation passent par Windows Update et peuvent prendre du temps : laisse le PC allumé.";
            if (choix.Any(m => m.Redemarrage)) question += "\n\nUn redémarrage sera nécessaire à la fin (tu choisis quand).";
            return new[] { "--maj-windows", "--seul", "--point-restauration", "--ids-maj", string.Join(",", ids) };
        }
    }
}
