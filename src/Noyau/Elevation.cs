// Nettoyage PC - autorisation administrateur à la demande, et canal de sortie du moteur
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace NettoyagePC
{
    /// <summary>Sortie d'un moteur en cours : les lignes arrivent par un tube nommé, local, au nom aléatoire.</summary>
    internal sealed class CanalSortie : IDisposable
    {
        public readonly ConcurrentQueue<string> Lignes = new ConcurrentQueue<string>();
        public volatile bool Fini = true;
        private volatile bool connecte;
        public readonly string Nom;
        private readonly string nomComplet;

        public CanalSortie()
        {
            Nom = "NettoyagePC-" + Guid.NewGuid().ToString("N");
            nomComplet = @"\\.\pipe\" + Nom;
        }

        public bool Ecouter()
        {
            if (!Natif.EstWindows) return EcouterPortable();
            // entrée seule + première instance obligatoire ; octets, bloquant, clients distants refusés ; 1 instance
            SafeFileHandle h = Natif.CreateNamedPipe(nomComplet, 0x00000001 | 0x00080000, 0x00000008, 1, 0, 65536, 0, IntPtr.Zero);
            if (h.IsInvalid) return false;
            Fini = false;
            var t = new Thread(() =>
            {
                try
                {
                    bool ok = Natif.ConnectNamedPipe(h, IntPtr.Zero) || System.Runtime.InteropServices.Marshal.GetLastWin32Error() == 535;
                    if (ok)
                    {
                        connecte = true;
                        using (var fs = new FileStream(h, FileAccess.Read, 4096, false))
                        using (var r = new StreamReader(fs, new UTF8Encoding(false)))
                        {
                            string l;
                            while ((l = r.ReadLine()) != null) Lignes.Enqueue(l);
                        }
                    }
                }
                catch { }
                finally { try { h.Dispose(); } catch { } Fini = true; }
            }) { IsBackground = true, Name = "Canal moteur" };
            t.Start();
            return true;
        }

        // Hors Windows (tests) : tube .NET classique
        private bool EcouterPortable()
        {
            try
            {
                var serveur = new NamedPipeServerStream(Nom, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.None);
                Fini = false;
                var t = new Thread(() =>
                {
                    try
                    {
                        serveur.WaitForConnection(); connecte = true;
                        using (var r = new StreamReader(serveur, new UTF8Encoding(false)))
                        { string l; while ((l = r.ReadLine()) != null) Lignes.Enqueue(l); }
                    }
                    catch { }
                    finally { Fini = true; }
                }) { IsBackground = true };
                t.Start();
                return true;
            }
            catch { return false; }
        }

        /// <summary>Débloque l'attente quand le programme ne viendra jamais (autorisation refusée...).</summary>
        public void Fermer()
        {
            if (Fini || connecte) return;
            try { using (var c = new NamedPipeClientStream(".", Nom, PipeDirection.Out)) c.Connect(300); } catch { }
        }

        public void Dispose() { Fermer(); }
    }

    /// <summary>Côté moteur : écrit chaque ligne dans le tube de l'interface (ou sur la console si aucun tube).</summary>
    internal static class Sortie
    {
        private static StreamWriter ecrivain;
        private static readonly object verrou = new object();
        public static Action<string> Supplementaire;

        public static void Connecter(string nom)
        {
            if (string.IsNullOrEmpty(nom)) { try { Console.OutputEncoding = new UTF8Encoding(false); } catch { } return; }
            try
            {
                var c = new NamedPipeClientStream(".", nom, PipeDirection.Out, PipeOptions.None, TokenImpersonationLevel.Identification);
                c.Connect(20000);
                ecrivain = new StreamWriter(c, new UTF8Encoding(false)) { AutoFlush = true };
            }
            catch { ecrivain = null; }
        }

        public static void Ligne(string texte)
        {
            lock (verrou)
            {
                if (ecrivain != null) { try { ecrivain.WriteLine(texte); } catch { ecrivain = null; } }
                else { try { Console.WriteLine(texte); } catch { } }
                var s = Supplementaire; if (s != null) { try { s(texte); } catch { } }
            }
        }

        public static void Fermer() { lock (verrou) { try { if (ecrivain != null) ecrivain.Dispose(); } catch { } ecrivain = null; } }
    }

    /// <summary>Lancement du moteur (même programme, option --moteur) et des commandes qui demandent l'autorisation.</summary>
    internal static class Elevation
    {
        /// <summary>Arguments qui transmettent au moteur les dossiers de la personne qui utilise l'application.</summary>
        public static List<string> ArgumentsUtilisateur()
        {
            var a = new List<string>
            {
                "--documents", Chemins.Documents, "--telechargements", Chemins.Telechargements,
                "--profil", Chemins.Profil, "--localappdata", Chemins.LocalAppData, "--appdata", Chemins.AppData,
                "--temp", Path.GetTempPath().TrimEnd('\\'), "--sid", SidActuel()
            };
            return a;
        }

        public static string SidActuel() { try { return WindowsIdentity.GetCurrent().User.Value; } catch { return ""; } }

        public static string Joindre(IEnumerable<string> args)
        {
            var sb = new StringBuilder();
            foreach (var a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) { sb.Append(a); continue; }
                sb.Append('"');
                int barres = 0;
                foreach (char c in a)
                {
                    if (c == '\\') { barres++; continue; }
                    if (c == '"') { sb.Append('\\', barres * 2 + 1); sb.Append('"'); barres = 0; continue; }
                    sb.Append('\\', barres); barres = 0; sb.Append(c);
                }
                sb.Append('\\', barres * 2);
                sb.Append('"');
            }
            return sb.ToString();
        }

        /// <summary>
        /// Démarre le moteur. Si l'action touche tout le PC et que l'application n'a pas l'autorisation, Windows la
        /// demande (fenêtre « Nettoyage PC »). Renvoie null si l'autorisation est refusée.
        /// </summary>
        public static Process LancerMoteur(IEnumerable<string> options, bool besoinAdmin, CanalSortie canal, out string erreur)
        {
            erreur = null;
            var args = new List<string> { "--moteur" };
            args.AddRange(options);
            args.Add("--progression");
            args.Add("--tube"); args.Add(canal.Nom);
            args.AddRange(ArgumentsUtilisateur());
            bool eleve = besoinAdmin && !Appli.EstAdmin;
            try
            {
                if (Appli.SousMono)
                {
                    var psiM = new ProcessStartInfo("mono", "\"" + Chemins.Exe + "\" " + Joindre(args)) { UseShellExecute = false, CreateNoWindow = true };
                    return Process.Start(psiM);
                }
                var psi = new ProcessStartInfo(Chemins.Exe, Joindre(args)) { WorkingDirectory = Chemins.Ici };
                if (eleve) { psi.UseShellExecute = true; psi.Verb = "runas"; psi.WindowStyle = ProcessWindowStyle.Hidden; }
                else { psi.UseShellExecute = false; psi.CreateNoWindow = true; }
                return Process.Start(psi);
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { erreur = "refus"; return null; }
            catch (Exception ex) { erreur = ex.Message; return null; }
        }

        /// <summary>
        /// Exécute une commande précise (liste fermée, voir CommandesAdmin) avec l'autorisation administrateur, et
        /// attend sa fin en gardant la fenêtre réactive. Renvoie vrai si elle a réussi.
        /// </summary>
        /// <summary>Comme Commande, mais pour un fil d'exécution de fond (sans pomper les messages de la fenêtre). -1 : autorisation refusée.</summary>
        public static int CommandeAttendre(int delaiSecondes, params string[] args)
        {
            if (Appli.EstAdmin) return CommandesAdmin.Executer(args);
            try
            {
                var psi = new ProcessStartInfo(Chemins.Exe, "--commande " + Joindre(args.Concat(ArgumentsUtilisateur())))
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Chemins.Ici };
                using (var p = Process.Start(psi))
                {
                    if (!p.WaitForExit(delaiSecondes * 1000)) return 6;
                    try { return p.ExitCode; } catch { return 0; }
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return -1; }
            catch (Exception ex) { JournalDemarrage.Erreur("commande administrateur", ex); return 1; }
        }

        public static bool Commande(int delaiSecondes, params string[] args)
        {
            if (Appli.EstAdmin) return CommandesAdmin.Executer(args) == 0;
            try
            {
                var psi = new ProcessStartInfo(Chemins.Exe, "--commande " + Joindre(args.Concat(ArgumentsUtilisateur())))
                { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = Chemins.Ici };
                using (var p = Process.Start(psi))
                {
                    var limite = DateTime.Now.AddSeconds(delaiSecondes);
                    while (!p.HasExited && DateTime.Now < limite) { Application.DoEvents(); Thread.Sleep(50); }
                    if (!p.HasExited) return false;
                    try { return p.ExitCode == 0; } catch { return true; }
                }
            }
            catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) { return false; }
            catch (Exception ex) { JournalDemarrage.Erreur("commande administrateur", ex); return false; }
        }
    }

    /// <summary>
    /// Commandes exécutées avec l'autorisation administrateur (programme lancé par Elevation.Commande).
    /// Liste fermée : seules ces opérations précises sont possibles, avec des paramètres vérifiés.
    /// </summary>
    internal static class CommandesAdmin
    {
        // Clés « tout le PC » que l'application modifie elle-même
        public static readonly string[] ClesAutorisees =
        {
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32",
            @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder",
            @"HKLM\SYSTEM\CurrentControlSet\Control\GraphicsDrivers"
        };

        public static int Executer(string[] args)
        {
            try
            {
                if (args.Length == 0) return 2;
                switch (args[0])
                {
                    case "registre-ecrire":   // cle nom type valeur(base64 pour binaire)
                        {
                            if (args.Length < 5 || !ClesAutorisees.Contains(args[1], StringComparer.OrdinalIgnoreCase)) return 3;
                            if (args[1].EndsWith("GraphicsDrivers", StringComparison.OrdinalIgnoreCase) && args[2] != "HwSchMode") return 3;
                            object v; RegistryValueKind k;
                            switch (args[3])
                            {
                                case "DWord": v = int.Parse(args[4]); k = RegistryValueKind.DWord; break;
                                case "Binary": v = Convert.FromBase64String(args[4]); k = RegistryValueKind.Binary; break;
                                default: return 3;
                            }
                            Registre.EcrireDirect(args[1], args[2], v, k);
                            return 0;
                        }
                    case "registre-supprimer":
                        {
                            if (args.Length < 3 || !ClesAutorisees.Contains(args[1], StringComparer.OrdinalIgnoreCase)) return 3;
                            Registre.SupprimerDirect(args[1], args[2]);
                            return 0;
                        }
                    case "arreter":   // pid : arrête un moteur lancé par l'application, puis relance Windows Update
                        {
                            int pid;
                            if (args.Length < 2 || !int.TryParse(args[1], out pid)) return 3;
                            try
                            {
                                var p = Process.GetProcessById(pid);
                                string chemin = ""; try { chemin = p.MainModule.FileName; } catch { }
                                if (!chemin.Equals(Chemins.Exe, StringComparison.OrdinalIgnoreCase)) return 4;   // seulement notre moteur
                                Programmes.Executer(Path.Combine(Chemins.Systeme32, "taskkill.exe"), "/PID " + pid + " /T /F", 20000);
                            }
                            catch (ArgumentException) { }
                            Services.RelancerMisesAJour();
                            return 0;
                        }
                    case "installer-maj":   // fichier empreinte
                        return Installation.InstallerEnAdmin(args.Length > 1 ? args[1] : "", args.Length > 2 ? args[2] : "");
                    case "pilote-supprimer":   // oemNN.inf (sans forcer : refusé par Windows si le pilote sert)
                        {
                            if (args.Length < 2 || !System.Text.RegularExpressions.Regex.IsMatch(args[1], @"^oem\d{1,4}\.inf$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) return 3;
                            int code;
                            Programmes.Executer(Path.Combine(Chemins.Systeme32, "pnputil.exe"), "/delete-driver " + args[1], 120000, null, out code);
                            return code == 0 ? 0 : 5;
                        }
                    case "dns":   // indexInterface adresses|auto
                        return Reseau.ChangerDnsAdmin(args);
                    case "mesurer-fps":   // source|- empreinte processus.exe secondes délai (PresentMon, voir Fps)
                        return Fps.MesurerEnAdmin(args);
                    case "redemarrer-bios":   // redémarrage vers les réglages du BIOS / UEFI (dans 15 s, annulable avec shutdown /a)
                        {
                            int code;
                            Programmes.Executer(Path.Combine(Chemins.Systeme32, "shutdown.exe"), "/r /fw /t 15 /c \"Nettoyage PC : redémarrage vers le BIOS\"", 30000, null, out code);
                            return code == 0 ? 0 : 5;
                        }
                }
                return 2;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("commande " + string.Join(" ", args.Take(1)), ex); return 1; }
        }
    }

    internal static class Services
    {
        public static void RelancerMisesAJour()
        {
            foreach (var n in new[] { "wuauserv", "bits" })
            {
                try
                {
                    using (var sc = new System.ServiceProcess.ServiceController(n))
                    {
                        if (sc.Status == System.ServiceProcess.ServiceControllerStatus.Stopped && Registre.TypeDemarrageService(n) != 4) sc.Start();
                    }
                }
                catch { }
            }
        }
    }

    /// <summary>Registre : HKCU directement ; HKLM (tout le PC) en passant par l'autorisation de Windows.</summary>
    internal static class Registre
    {
        private static RegistryKey Racine(string cle, out string sousCle)
        {
            int i = cle.IndexOf('\\');
            string r = cle.Substring(0, i).TrimEnd(':').ToUpperInvariant();
            sousCle = cle.Substring(i + 1);
            switch (r)
            {
                case "HKCU": case "HKEY_CURRENT_USER": return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64);
                case "HKLM": case "HKEY_LOCAL_MACHINE": return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            }
            throw new ArgumentException("racine inconnue : " + r);
        }

        public static string Normaliser(string cle) { return cle.Replace("HKCU:\\", "HKCU\\").Replace("HKLM:\\", "HKLM\\"); }

        public static object Lire(string cle, string nom)
        {
            try
            {
                string s; using (var b = Racine(Normaliser(cle), out s)) using (var k = b.OpenSubKey(s)) return k == null ? null : k.GetValue(nom, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            }
            catch { return null; }
        }

        public static RegistryValueKind? Type(string cle, string nom)
        {
            try
            {
                string s; using (var b = Racine(Normaliser(cle), out s)) using (var k = b.OpenSubKey(s))
                { if (k == null || !k.GetValueNames().Contains(nom, StringComparer.OrdinalIgnoreCase)) return null; return k.GetValueKind(nom); }
            }
            catch { return null; }
        }

        public static bool Existe(string cle, string nom) { return Type(cle, nom) != null; }

        public static string[] Valeurs(string cle)
        {
            try { string s; using (var b = Racine(Normaliser(cle), out s)) using (var k = b.OpenSubKey(s)) return k == null ? new string[0] : k.GetValueNames(); }
            catch { return new string[0]; }
        }

        public static string[] SousCles(string cle)
        {
            try { string s; using (var b = Racine(Normaliser(cle), out s)) using (var k = b.OpenSubKey(s)) return k == null ? new string[0] : k.GetSubKeyNames(); }
            catch { return new string[0]; }
        }

        public static string Texte(string cle, string nom) { var v = Lire(cle, nom); return v == null ? "" : Convert.ToString(v).Trim(); }

        /// <summary>Modifie une valeur en gardant l'ancienne dans l'Historique (annulable). Faux si elle avait déjà cette valeur.</summary>
        public static bool EcrireSuivi(string cle, string nom, object valeur, RegistryValueKind type, string titre)
        {
            var typeAvant = Type(cle, nom);
            object avant = typeAvant == null ? null : Lire(cle, nom);
            if (typeAvant != null && Convert.ToString(avant) == Convert.ToString(valeur)) return false;
            Ecrire(cle, nom, valeur, type);
            // chemin au format de l'historique 3.x (HKCU:\...) : compatible dans les deux sens
            string clePs = Normaliser(cle).Replace(@"HKCU\", @"HKCU:\").Replace(@"HKLM\", @"HKLM:\");
            Historique.Ajouter("registre", titre, new Dictionary<string, object>
            {
                { "Cle", clePs }, { "Nom", nom }, { "Existait", typeAvant != null }, { "Avant", avant is byte[] ? null : avant }, { "TypeAvant", typeAvant == null ? "" : typeAvant.Value.ToString() }
            });
            return true;
        }

        public static void EcrireDirect(string cle, string nom, object valeur, RegistryValueKind type)
        {
            string s; using (var b = Racine(Normaliser(cle), out s)) using (var k = b.CreateSubKey(s)) k.SetValue(nom, valeur, type);
        }

        public static void SupprimerDirect(string cle, string nom)
        {
            string s; using (var b = Racine(Normaliser(cle), out s)) using (var k = b.OpenSubKey(s, true)) if (k != null) k.DeleteValue(nom, false);
        }

        public static bool EstMachine(string cle) { return Normaliser(cle).StartsWith("HKLM", StringComparison.OrdinalIgnoreCase); }

        /// <summary>Écrit une valeur ; pour HKLM sans autorisation, Windows la demande. Exception si refusé.</summary>
        public static void Ecrire(string cle, string nom, object valeur, RegistryValueKind type)
        {
            cle = Normaliser(cle);
            if (!EstMachine(cle) || Appli.EstAdmin) { EcrireDirect(cle, nom, valeur, type); return; }
            string texte;
            if (type == RegistryValueKind.Binary) texte = Convert.ToBase64String((byte[])valeur);
            else if (type == RegistryValueKind.DWord) texte = Convert.ToInt32(valeur).ToString();
            else throw new InvalidOperationException("type de valeur non pris en charge");
            if (!Elevation.Commande(120, "registre-ecrire", cle, nom, type.ToString(), texte)) throw new UnauthorizedAccessException("autorisation de Windows refusée : rien n'a été modifié");
        }

        public static void Supprimer(string cle, string nom)
        {
            cle = Normaliser(cle);
            if (!EstMachine(cle) || Appli.EstAdmin) { SupprimerDirect(cle, nom); return; }
            if (!Elevation.Commande(120, "registre-supprimer", cle, nom)) throw new UnauthorizedAccessException("autorisation de Windows refusée : rien n'a été modifié");
        }

        public static int TypeDemarrageService(string nom)
        {
            object v = Lire(@"HKLM\SYSTEM\CurrentControlSet\Services\" + nom, "Start");
            return v is int ? (int)v : -1;
        }
    }
}
