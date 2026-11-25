// Velox - installation de VB-Cable pour « Pas boostés » : l'utilisateur n'a plus rien à décompresser ni à lancer lui-même.
// VB-Cable est un logiciel de VB-Audio (www.vb-cable.com), donationware. Sa licence (vb-audio.com/Services/licensing.htm)
// permet de le distribuer tel quel avec une application et de l'installer en silencieux, à condition que l'utilisateur
// sache que c'est VB-Cable, de VB-Audio, et puisse soutenir le projet : Velox l'affiche avant l'installation.
// Le paquet officiel est téléchargé depuis le site de VB-Audio (jamais d'ailleurs), décompressé, puis copié avec
// l'autorisation administrateur dans un dossier réservé aux administrateurs ; l'installateur n'est lancé que si sa
// signature numérique (vérifiée par Windows) est celle de VB-Audio. Il tourne en silencieux (-i -h). Windows affiche
// quand même sa propre question « Voulez-vous installer ce logiciel de périphérique ? » : elle ne peut pas être cachée
// (c'est un pilote), et Velox ne l'esquive pas (il n'ajoute pas VB-Audio aux éditeurs approuvés de Windows).
// Un redémarrage est conseillé ensuite par VB-Audio : Velox le propose une seule fois, à la fin de tout ce qu'il installe.
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal static class InstallationCable
    {
        public const string Paquet = "VBCABLE_Driver_Pack46.zip";
        public const string Adresse = "https://download.vb-audio.com/Download_CABLE/" + Paquet;
        /// <summary>Mention demandée par VB-Audio à ceux qui distribuent VB-Cable.</summary>
        public const string Site = "https://vb-cable.com";
        private const long TailleMax = 20L * 1024 * 1024;
        /// <summary>Copie de l'installateur gardée après l'installation (dossier réservé aux administrateurs) : VB-Cable
        /// n'a pas de désinstalleur dans Paramètres > Applications, on le retire en relançant son installateur en
        /// administrateur, bouton « Remove Driver ».</summary>
        public static string Dossier { get { return Path.Combine(Fps.DossierOutils, "vbcable"); } }

        /// <summary>Signataire de l'installateur officiel (sujet du certificat) : VB-Audio, ou Vincent Burel, son auteur.</summary>
        public static readonly Regex Signataire = new Regex(@"(^|,\s*)(CN|O)=""?[^,]*(VB-Audio|Vincent Burel)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>Installateur du paquet pour l'architecture de Windows (« X64 », « Arm64 », « X86 ») parmi les noms de
        /// fichiers donnés (sans dossier), ou null : VBCABLE_Setup_x64.exe, VBCABLE_Setup_ARM64.exe, VBCABLE_Setup.exe.</summary>
        public static string Installateur(string architecture, string[] noms)
        {
            string suffixe = architecture == "X64" ? "_x64" : architecture == "Arm64" ? "_arm64" : architecture == "X86" ? "" : null;
            if (suffixe == null || noms == null) return null;
            return noms.FirstOrDefault(n => string.Equals(n, "VBCABLE_Setup" + suffixe + ".exe", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Vrai si le pilote de VB-Cable est installé, même si son câble n'apparaît pas encore dans les sorties (en attente
        /// du redémarrage) : un appareil de la classe « Son, vidéo et jeux » décrit « VB-Audio Virtual Cable ».
        /// </summary>
        public static bool PiloteInstalle()
        {
            if (!Natif.EstWindows) return false;
            try
            {
                using (var classe = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e96c-e325-11ce-bfc1-08002be10318}"))
                {
                    if (classe == null) return false;
                    foreach (string n in classe.GetSubKeyNames())
                    {
                        if (!Regex.IsMatch(n, @"^\d{4}$")) continue;
                        try
                        {
                            using (var k = classe.OpenSubKey(n))
                                if (k != null && string.Equals((k.GetValue("DriverDesc") as string ?? "").Trim(), "VB-Audio Virtual Cable", StringComparison.OrdinalIgnoreCase)) return true;
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return false;
        }

        /// <summary>
        /// Télécharge le paquet officiel de VB-Cable et le décompresse dans un dossier temporaire. « progres » reçoit le
        /// pourcentage. Renvoie le dossier ; en cas de problème, une exception avec la raison (rien n'est installé).
        /// </summary>
        public static string Telecharger(Action<int> progres, CancellationToken annulation)
        {
            string dossier = Path.Combine(Path.GetTempPath(), "NettoyagePC-cable-" + Guid.NewGuid().ToString("N").Substring(0, 10));
            Directory.CreateDirectory(dossier);
            string zip = Path.Combine(dossier, Paquet), contenu = Path.Combine(dossier, "paquet");
            try
            {
                Exception derniere = null;
                bool fait = false;
                for (int essai = 0; essai < 2 && !fait; essai++)
                {
                    annulation.ThrowIfCancellationRequested();
                    try { TelechargerDepuis(Adresse, zip, progres, annulation); fait = true; }
                    catch (OperationCanceledException) when (annulation.IsCancellationRequested) { throw; }
                    catch (Exception ex) { derniere = ex; try { File.Delete(zip); } catch { } if (essai == 0) Thread.Sleep(1500); }
                }
                if (!fait)
                {
                    string raison = derniere is HttpRequestException || derniere is TaskCanceledException ? "Pas de connexion au site de VB-Audio : vérifie Internet, puis réessaie." : derniere != null ? derniere.Message : "Téléchargement impossible.";
                    throw new InvalidOperationException(raison);
                }
                try
                {
                    using (var archive = ZipFile.OpenRead(zip))
                    {
                        if (archive.Entries.Count > 200 || archive.Entries.Sum(e => e.Length) > 4 * TailleMax) throw new InvalidDataException();
                    }
                    ZipFile.ExtractToDirectory(zip, contenu);   // (refuse tout chemin qui sortirait du dossier)
                }
                catch (Exception) { throw new InvalidOperationException("Le fichier téléchargé n'est pas le paquet de VB-Cable attendu : rien n'a été installé."); }
                File.Delete(zip);
                string dossierSetup = DossierInstallateur(contenu);
                if (dossierSetup == null) throw new InvalidOperationException("Le paquet de VB-Cable ne contient pas d'installateur pour ce type de Windows : installe-le depuis son site officiel.");
                string setup = Path.Combine(dossierSetup, Installateur(InstallationApo.Architecture, Directory.GetFiles(dossierSetup).Select(x => Path.GetFileName(x)).ToArray()));
                if (!Fps.SignePar(setup, Signataire)) throw new InvalidOperationException("L'installateur téléchargé n'est pas signé par VB-Audio (téléchargement corrompu ou modifié) : rien n'a été installé.");
                return dossierSetup;
            }
            catch
            {
                try { Directory.Delete(dossier, true); } catch { }
                throw;
            }
        }

        /// <summary>Dossier (le paquet décompressé, ou un de ses sous-dossiers) qui contient l'installateur pour ce Windows.</summary>
        private static string DossierInstallateur(string racine)
        {
            foreach (string d in new[] { racine }.Concat(Directory.GetDirectories(racine, "*", SearchOption.AllDirectories)))
                if (Installateur(InstallationApo.Architecture, Directory.GetFiles(d).Select(x => Path.GetFileName(x)).ToArray()) != null) return d;
            return null;
        }

        private static void TelechargerDepuis(string adresse, string f, Action<int> progres, CancellationToken annulation)
        {
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("NettoyagePC/" + Appli.Version);
                using (var rep = client.GetAsync(adresse, HttpCompletionOption.ResponseHeadersRead, annulation).GetAwaiter().GetResult())
                {
                    if (!rep.IsSuccessStatusCode) throw new InvalidOperationException("Le site de VB-Audio ne répond pas (code " + (int)rep.StatusCode + ") : réessaie plus tard.");
                    // redirigé hors du site de VB-Audio : refusé
                    var finale = rep.RequestMessage != null ? rep.RequestMessage.RequestUri : null;
                    if (finale == null || finale.Scheme != "https" || !(finale.Host == "vb-audio.com" || finale.Host.EndsWith(".vb-audio.com", StringComparison.OrdinalIgnoreCase)))
                        throw new InvalidOperationException("Le paquet de VB-Cable n'est pas venu du site de VB-Audio : rien n'a été installé.");
                    long total = rep.Content.Headers.ContentLength ?? 0;
                    if (total > TailleMax) throw new InvalidOperationException("Le fichier proposé n'est pas le paquet de VB-Cable attendu : rien n'a été installé.");
                    using (var lecture = rep.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                    using (var ecriture = new FileStream(f, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        var tampon = new byte[81920];
                        long lu = 0; int n, dernier = -1;
                        while ((n = lecture.Read(tampon, 0, tampon.Length)) > 0)
                        {
                            annulation.ThrowIfCancellationRequested();
                            ecriture.Write(tampon, 0, n); lu += n;
                            if (lu > TailleMax) throw new InvalidOperationException("Le fichier proposé n'est pas le paquet de VB-Cable attendu : rien n'a été installé.");
                            int p = total > 0 ? (int)(lu * 100 / total) : -1;
                            if (p != dernier && progres != null) { dernier = p; progres(p); }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Lance l'installation (autorisation administrateur, puis question de Windows sur le pilote) et attend la fin.
        /// Renvoie "" si le pilote est installé, sinon la raison. Le dossier téléchargé est supprimé.
        /// </summary>
        public static string Installer(string dossier)
        {
            int code;
            try { code = Elevation.CommandeAttendre(900, "installer-cable", dossier); }
            finally { try { Directory.Delete(DossierTelechargement(dossier), true); } catch { } }
            if (code == -1) return "Installation annulée : Windows n'a pas eu l'autorisation administrateur.";
            if (code == 4) return "L'installateur n'est pas signé par VB-Audio : rien n'a été installé.";
            if (code == 6) return "L'installation de VB-Cable prend trop de temps : vérifie si Windows te pose une question (fenêtre « Sécurité de Windows »), puis rouvre le Studio son.";
            if (code != 0 && code != 5) JournalDemarrage.Erreur("installation de VB-Cable", new InvalidOperationException("code " + code));
            return PiloteInstalle() ? "" : "VB-Cable ne s'est pas installé : si Windows a demandé « Voulez-vous installer ce logiciel de périphérique ? », il fallait cliquer « Installer ». Réessaie, ou installe-le depuis son site officiel.";
        }

        /// <summary>Dossier « NettoyagePC-cable-… » créé par Telecharger, au-dessus de celui de l'installateur.</summary>
        private static string DossierTelechargement(string dossier)
        {
            var d = new DirectoryInfo(dossier);
            while (d != null && !d.Name.StartsWith("NettoyagePC-cable-", StringComparison.Ordinal)) d = d.Parent;
            return d != null ? d.FullName : dossier;
        }

        /// <summary>
        /// (administrateur) « installer-cable dossier » : copie le paquet (installateur et fichiers du pilote) dans un dossier
        /// réservé aux administrateurs, vérifie la signature de VB-Audio sur la copie de l'installateur, puis le lance en
        /// silencieux (-i -h) et attend sa fin. 0 : pilote installé ; 3 : demande invalide ; 4 : installateur non signé par
        /// VB-Audio ; 5 : échec ; 6 : trop long.
        /// </summary>
        public static int InstallerEnAdmin(string[] args)
        {
            if (args.Length < 2) return 3;
            string source;
            try { source = Path.GetFullPath(args[1]); } catch { return 3; }
            // seulement un dossier préparé par Telecharger, dans le dossier temporaire
            if (!Directory.Exists(source) || source.IndexOf(Path.DirectorySeparatorChar + "NettoyagePC-cable-", StringComparison.OrdinalIgnoreCase) < 0) return 3;
            var fichiers = Directory.GetFiles(source);
            string nom = Installateur(InstallationApo.Architecture, fichiers.Select(x => Path.GetFileName(x)).ToArray());
            if (nom == null || fichiers.Length > 100 || fichiers.Sum(f => new FileInfo(f).Length) > TailleMax) return 3;
            string copie = Dossier;
            bool garder = false;
            try
            {
                if (Directory.Exists(copie)) Directory.Delete(copie, true);
                Directory.CreateDirectory(copie);
                foreach (string f in fichiers) File.Copy(f, Path.Combine(copie, Path.GetFileName(f)));
                string setup = Path.Combine(copie, nom);
                if (!Fps.SignePar(setup, Signataire)) return 4;
                var psi = new ProcessStartInfo(setup, "-i -h") { UseShellExecute = false, WorkingDirectory = copie };
                using (var p = Process.Start(psi))
                {
                    // la question de Windows sur le pilote attend l'utilisateur : délai large
                    if (!p.WaitForExit(840 * 1000)) return 6;
                }
                garder = PiloteInstalle();
                return garder ? 0 : 5;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("installation de VB-Cable", ex); return 5; }
            // installé : la copie reste, c'est avec elle qu'on le désinstalle (« Remove Driver », voir Dossier)
            finally { try { if (!garder && Directory.Exists(copie)) Directory.Delete(copie, true); } catch { } }
        }
    }
}
