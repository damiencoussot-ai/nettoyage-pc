// Velox - installation d'Equalizer APO pour le Studio son : l'utilisateur n'a rien à chercher ni à télécharger.
// Fichier officiel du projet « equalizerapo » sur SourceForge (logiciel libre, licence GPL). Version et empreintes SHA256
// figées dans Velox (celles publiées par SourceForge, vérifiées) : le fichier est vérifié une fois téléchargé, puis de
// nouveau avec l'autorisation administrateur juste avant d'être lancé, depuis une copie dans un dossier réservé aux
// administrateurs (un autre programme du compte ne peut pas l'échanger entre-temps).
// L'installateur tourne en silencieux (/S) ; à la fin, sa fenêtre « Device Selector » demande sur quelle sortie installer
// Equalizer APO (une case à cocher), relance le son de Windows et vérifie que tout marche. Ce choix reste celui de
// l'utilisateur : Velox ne modifie pas lui-même les réglages audio protégés de Windows.
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace NettoyagePC
{
    internal static class InstallationApo
    {
        public const string Version = "1.4.2";
        public const string Depot = "https://sourceforge.net/projects/equalizerapo/";

        /// <summary>Installateur officiel pour l'architecture de Windows (« X64 » ou « Arm64 ») : nom et empreinte SHA256.</summary>
        public static bool Fichier(string architecture, out string nom, out string empreinte)
        {
            nom = empreinte = null;
            if (architecture == "X64") { nom = "EqualizerAPO-x64-1.4.2.exe"; empreinte = "7403BE7427BBE1936A40DDED082829B6E217FC4F5990FEE5CBA501F0AE055AFA"; }
            else if (architecture == "Arm64") { nom = "EqualizerAPO-ARM64-1.4.2.exe"; empreinte = "0DBFC64DEE60F94CF77B297FEB1F54D2633D49669BB474086FFFB8DB23E8F066"; }
            return nom != null;
        }

        /// <summary>Architecture de Windows (pas celle de Velox : sur un PC ARM, Velox x64 tourne en émulation).</summary>
        public static string Architecture { get { return RuntimeInformation.OSArchitecture.ToString(); } }

        public static string Adresse(string nom) { return "https://downloads.sourceforge.net/project/equalizerapo/" + Version + "/" + nom; }

        /// <summary>Adresses officielles essayées dans l'ordre (SourceForge choisit un miroir proche) : si la première ne
        /// répond pas, la seconde ; le fichier est de toute façon vérifié par son empreinte.</summary>
        public static string[] Adresses(string nom)
        {
            return new[] { Adresse(nom), "https://sourceforge.net/projects/equalizerapo/files/" + Version + "/" + nom + "/download" };
        }

        /// <summary>
        /// Télécharge l'installateur officiel et vérifie son empreinte. « progres » reçoit le pourcentage. Renvoie le chemin
        /// du fichier (dossier temporaire) ; en cas de problème, une exception avec la raison (rien n'est installé).
        /// </summary>
        public static string Telecharger(Action<int> progres, CancellationToken annulation)
        {
            string nom, empreinte;
            if (!Fichier(Architecture, out nom, out empreinte)) throw new InvalidOperationException("Equalizer APO n'existe pas pour ce type de Windows.");
            string dossier = Path.Combine(Path.GetTempPath(), "NettoyagePC-apo-" + Guid.NewGuid().ToString("N").Substring(0, 10));
            Directory.CreateDirectory(dossier);
            string f = Path.Combine(dossier, nom);
            try
            {
                Exception derniere = null;
                bool fait = false;
                foreach (string adresse in Adresses(nom))
                {
                    for (int essai = 0; essai < 2 && !fait; essai++)
                    {
                        annulation.ThrowIfCancellationRequested();
                        try { TelechargerDepuis(adresse, f, progres, annulation); fait = Moteur.Empreinte(f) == empreinte; if (!fait) derniere = new InvalidOperationException("Le fichier téléchargé ne correspond pas à la version officielle d'Equalizer APO (téléchargement corrompu ou modifié) : rien n'a été installé."); }
                        catch (OperationCanceledException) when (annulation.IsCancellationRequested) { throw; }   // (un délai dépassé, lui, est réessayé)
                        catch (Exception ex) { derniere = ex; }
                        if (!fait) { try { File.Delete(f); } catch { } if (essai == 0) Thread.Sleep(1500); }
                    }
                    if (fait) break;
                }
                if (!fait)
                {
                    string raison = derniere is HttpRequestException || derniere is TaskCanceledException ? "Pas de connexion au site d'Equalizer APO : vérifie Internet, puis réessaie." : derniere != null ? derniere.Message : "Téléchargement impossible.";
                    throw new InvalidOperationException(raison);
                }
                if (Moteur.Empreinte(f) != empreinte) throw new InvalidOperationException("Le fichier téléchargé ne correspond pas à la version officielle d'Equalizer APO (téléchargement corrompu ou modifié) : rien n'a été installé.");
                return f;
            }
            catch
            {
                try { File.Delete(f); Directory.Delete(dossier); } catch { }
                throw;
            }
        }

        private static void TelechargerDepuis(string adresse, string f, Action<int> progres, CancellationToken annulation)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("NettoyagePC/" + Appli.Version);
                using (var rep = client.GetAsync(adresse, HttpCompletionOption.ResponseHeadersRead, annulation).GetAwaiter().GetResult())
                {
                    if (!rep.IsSuccessStatusCode) throw new InvalidOperationException("Le site d'Equalizer APO ne répond pas (code " + (int)rep.StatusCode + ") : réessaie plus tard.");
                    long total = rep.Content.Headers.ContentLength ?? 0;
                    if (total > 100L * 1024 * 1024) throw new InvalidOperationException("Le fichier proposé n'est pas l'installateur attendu : rien n'a été installé.");
                    using (var lecture = rep.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (var ecriture = new FileStream(f, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var tampon = new byte[81920];
                        long lu = 0; int n, dernier = -1;
                        while ((n = lecture.Read(tampon, 0, tampon.Length)) > 0)
                        {
                            annulation.ThrowIfCancellationRequested();
                            ecriture.Write(tampon, 0, n); lu += n;
                            if (lu > 100L * 1024 * 1024) throw new InvalidOperationException("Le fichier proposé n'est pas l'installateur attendu : rien n'a été installé.");
                            int p = total > 0 ? (int)(lu * 100 / total) : -1;
                            if (p != dernier && progres != null) { dernier = p; progres(p); }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Lance l'installation (autorisation administrateur demandée par Windows) et attend la fin, fenêtre d'Equalizer APO
        /// comprise. Renvoie "" si Equalizer APO est installé, sinon la raison. Le fichier téléchargé est supprimé.
        /// </summary>
        public static string Installer(string fichier)
        {
            int code;
            try { code = Elevation.CommandeAttendre(1800, "installer-apo", fichier); }
            finally { try { File.Delete(fichier); Directory.Delete(Path.GetDirectoryName(fichier)); } catch { } }
            if (code == -1) return "Installation annulée : Windows n'a pas eu l'autorisation administrateur.";
            if (code == 4) return "Le fichier ne correspond pas à la version officielle d'Equalizer APO : rien n'a été installé.";
            if (code == 6) return "L'installation d'Equalizer APO prend trop de temps : vérifie sa fenêtre, puis rouvre le Studio son.";
            if (code != 0) return "L'installation d'Equalizer APO n'a pas abouti (code " + code + ").";
            return AudioJeu.Present ? "" : "Equalizer APO ne s'est pas installé : réessaie, ou installe-le depuis son site officiel.";
        }

        /// <summary>
        /// (administrateur) « installer-apo fichier » : copie le fichier dans un dossier réservé aux administrateurs, revérifie
        /// son empreinte avec celle figée dans Velox (jamais une empreinte reçue), puis lance l'installateur officiel en
        /// silencieux et attend sa fin. 0 : fait ; 3 : demande invalide ; 4 : fichier non officiel ; 5 : échec.
        /// </summary>
        /// <summary>
        /// La fenêtre d'Equalizer APO 1.4 (« Device Selector ») est faite avec Qt. Sur certains PC, Qt ne retrouve pas tout
        /// seul son module d'affichage et elle refuse de s'ouvrir (« no Qt platform plugin could be initialized »), alors
        /// que tous ses fichiers sont là. On lui donne le chemin exact de ses modules (dossier « qt » d'Equalizer APO), et on
        /// retire toute variable Qt laissée par un autre programme.
        /// </summary>
        public static void PreparerQt(ProcessStartInfo psi, string dossierApo)
        {
            foreach (var cle in psi.Environment.Keys.Where(k => k.StartsWith("QT_", StringComparison.OrdinalIgnoreCase)).ToList()) psi.Environment.Remove(cle);
            psi.Environment["QT_PLUGIN_PATH"] = Path.Combine(dossierApo, "qt");
            psi.Environment["QT_QPA_PLATFORM_PLUGIN_PATH"] = Path.Combine(dossierApo, "qt", "platforms");
        }

        /// <summary>(administrateur) « selecteur-apo » : ouvre la fenêtre d'Equalizer APO (choix des sorties) depuis son
        /// dossier d'installation, avec le chemin de ses modules Qt. 0 si elle est lancée.</summary>
        public static int SelecteurEnAdmin()
        {
            try
            {
                string exe = AudioJeu.Configurator;
                if (exe == null) return 3;
                string nomExe = Path.GetFileName(exe);
                if (!string.Equals(nomExe, "DeviceSelector.exe", StringComparison.OrdinalIgnoreCase) && !string.Equals(nomExe, "Configurator.exe", StringComparison.OrdinalIgnoreCase)) return 3;
                string dossier = Path.GetDirectoryName(exe);
                var psi = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = dossier };
                PreparerQt(psi, dossier);
                using (Process.Start(psi)) { }
                return 0;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("fenêtre d'Equalizer APO", ex); return 5; }
        }

        public static int InstallerEnAdmin(string[] args)
        {
            string nom, empreinte;
            if (args.Length < 2 || !Fichier(Architecture, out nom, out empreinte)) return 3;
            string source = args[1];
            if (!File.Exists(source) || !string.Equals(Path.GetFileName(source), nom, StringComparison.OrdinalIgnoreCase)) return 3;
            Directory.CreateDirectory(Fps.DossierOutils);
            string copie = Path.Combine(Fps.DossierOutils, nom);
            try
            {
                if (File.Exists(copie)) File.Delete(copie);
                File.Copy(source, copie);
                if (Moteur.Empreinte(copie) != empreinte) return 4;
                var psi = new ProcessStartInfo(copie, "/S") { UseShellExecute = false, WorkingDirectory = Fps.DossierOutils };
                // la fenêtre « Device Selector » que l'installateur ouvre à la fin hérite de ces réglages (voir PreparerQt)
                PreparerQt(psi, Path.Combine(Chemins.ProgramFiles, "EqualizerAPO"));
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(1800 * 1000)) return 5;
                }
                return AudioJeu.Present ? 0 : 5;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("installation d'Equalizer APO", ex); return 5; }
            finally { try { if (File.Exists(copie)) File.Delete(copie); } catch { } }
        }
    }
}
