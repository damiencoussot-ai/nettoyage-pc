// Velox - moteur audio « Pas boostés » en direct (Windows) : câble virtuel capté, son traité, joué sur le casque.
// Tourne dans un processus à part (« NettoyagePC.exe --moteur-son »), un seul à la fois, indépendant de la fenêtre :
// fermer Velox ne coupe pas le son. Si ce processus s'arrêtait, le gardien (ou Velox à son ouverture) le relance ; et
// couper « Pas boostés », comme désinstaller Velox, remet la sortie par défaut d'avant.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;

namespace NettoyagePC
{
    internal static partial class MoteurSon
    {
        private const string NomVerrou = @"Local\VeloxMoteurSon";
        /// <summary>Signal d'arrêt (l'installateur le donne avant une mise à jour) : le moteur s'arrête sans rien changer à
        /// ses réglages ; Velox le relance à son prochain lancement.</summary>
        public const string NomArret = @"Local\VeloxMoteurSon-Arret";
        private static readonly Regex NomSortieCable = new Regex(@"^CABLE(-[A-D])? Input$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex NomEntreeCable = new Regex(@"^CABLE(-[A-D])? Output$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private const string CleEntrees = @"HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture";

        // ------------------------------------------------------------------ appareils

        /// <summary>Nom d'une entrée (capture) de Windows, lu dans le registre ("" si inconnu).</summary>
        public static string NomEntree(string id)
        {
            var m = Regex.Match(id ?? "", @"\{([0-9a-fA-F-]{36})\}$");
            if (!m.Success || !Natif.EstWindows) return "";
            try { return Registre.Texte(CleEntrees + @"\{" + m.Groups[1].Value.ToLowerInvariant() + @"}\Properties", "{a45c254e-df1c-4efd-8020-67d146a850e0},2"); }
            catch { return ""; }
        }

        /// <summary>Entrées actives (identifiants complets).</summary>
        public static List<string> Entrees()
        {
            var l = new List<string>();
            if (!Natif.EstWindows) return l;
            IMMDeviceEnumerator e = null; IMMDeviceCollection c = null;
            try
            {
                e = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                if (e.EnumAudioEndpoints(1, 1, out c) != 0 || c == null) return l;   // entrées (eCapture), actives
                int n; if (c.GetCount(out n) != 0) return l;
                for (int i = 0; i < n; i++)
                {
                    IMMDevice d = null;
                    try { string id; if (c.Item(i, out d) == 0 && d != null && d.GetId(out id) == 0 && AudioWindows.IdEntreeValide(id)) l.Add(id); }
                    catch { }
                    finally { AudioWindows.Liberer(d); }
                }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("liste des entrées audio", ex); }
            finally { AudioWindows.Liberer(c); AudioWindows.Liberer(e); }
            return l;
        }

        /// <summary>Câbles virtuels VB-Audio (VB-Cable, et ses variantes A à D) présents et actifs, appariés sortie / entrée.</summary>
        public static List<CableVirtuel> Cables(List<SortieWindows> sorties = null)
        {
            var l = new List<CableVirtuel>();
            if (!Natif.EstWindows) return l;
            sorties = sorties ?? AudioWindows.Lister();
            var entrees = Entrees().Select(id => Tuple.Create(id, NomEntree(id))).ToList();
            foreach (var s in sorties)
            {
                string suffixe;
                if (!EstCable(s.Nom, out suffixe)) continue;
                var en = entrees.FirstOrDefault(x => NomEntreeCable.Match(x.Item2) is Match me && me.Success && string.Equals(me.Groups[1].Value, suffixe, StringComparison.OrdinalIgnoreCase));
                if (en != null) l.Add(new CableVirtuel { Nom = "VB-Cable" + suffixe.ToUpperInvariant(), Sortie = s.Id, Entree = en.Item1 });
            }
            return l;
        }

        /// <summary>
        /// (égaliseur en mode Auto) La sortie par défaut est le câble de « Pas boostés » : le son finit sur le casque, c'est
        /// lui que l'égaliseur doit viser. Renvoie le guid du casque, ou le guid donné tel quel.
        /// </summary>
        public static string DefautEffectif(string guidDefaut)
        {
            try
            {
                if (!Natif.EstWindows || guidDefaut == "") return guidDefaut;
                var r = Lire();
                if (!r.Actif || r.Destination == "") return guidDefaut;
                string suffixe;
                return EstCable(AudioJeu.NomSortie(guidDefaut), out suffixe) ? AudioWindows.GuidDe(r.Destination) : guidDefaut;
            }
            catch { return guidDefaut; }
        }

        private static readonly Regex PiloteCable = new Regex(@"^VB-Audio (Virtual Cable|Cable ([A-D]))$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Vrai si cette sortie (« Nom (pilote) ») est un câble VB-Cable : d'après son nom (« CABLE Input », « CABLE-A
        /// Input »), ou d'après son pilote si elle a été renommée (« Velox Studio (VB-Audio Virtual Cable) »). « suffixe » :
        /// "" ou « -A » à « -D », pour l'apparier à son entrée « CABLE… Output ».
        /// </summary>
        public static bool EstCable(string libelle, out string suffixe)
        {
            suffixe = "";
            var m = NomSortieCable.Match(NomSeul(libelle));
            if (m.Success) { suffixe = m.Groups[1].Value; return true; }
            string l = (libelle ?? "").Trim();
            int i = l.LastIndexOf(" (", StringComparison.Ordinal);
            if (i < 0 || !l.EndsWith(")")) return false;
            var p = PiloteCable.Match(l.Substring(i + 2, l.Length - i - 3));
            if (!p.Success) return false;
            if (p.Groups[2].Success) suffixe = "-" + p.Groups[2].Value.ToUpperInvariant();
            return true;
        }

        public static bool EstCable(string libelle) { string s; return EstCable(libelle, out s); }

        /// <summary>« CABLE Input (VB-Audio Virtual Cable) » → « CABLE Input ».</summary>
        private static string NomSeul(string libelle)
        {
            string n = (libelle ?? "").Trim();
            int i = n.IndexOf(" (", StringComparison.Ordinal);
            return i > 0 ? n.Substring(0, i) : n;
        }

        // ------------------------------------------------------------------ état (écrit par le moteur, lu par la page)

        internal sealed class EtatMoteur
        {
            public bool Marche, Jeu; public int LatenceMs, Canaux; public double Reduction, ReductionArme; public string Erreur = "";
            /// <summary>Réécoute en cours : 0 non, 1 « sans Velox », 2 pause, 3 « avec Velox » ; secondes restantes de l'étape.</summary>
            public int Reecoute, ReecouteSecondes;
            /// <summary>Secondes de son gardées pour la réécoute (jusqu'à 10).</summary>
            public int Memoire;
            /// <summary>Jeu reconnu en cours (clé : « cod », « valorant »…), "" si aucun.</summary>
            public string JeuCle = "";
        }

        public static bool EnMarche()
        {
            if (!Natif.EstWindows) return false;
            try { Mutex m; if (Mutex.TryOpenExisting(NomVerrou, out m)) { m.Dispose(); return true; } } catch { }
            return false;
        }

        public static EtatMoteur LireEtat()
        {
            var e = new EtatMoteur();
            try
            {
                if (!File.Exists(FichierEtat) || (DateTime.UtcNow - File.GetLastWriteTimeUtc(FichierEtat)).TotalSeconds > 4) { e.Marche = false; return e; }
                var p = File.ReadAllText(FichierEtat).Split('|');
                if (p.Length < 5) return e;
                var c = CultureInfo.InvariantCulture;
                e.Marche = p[0] == "marche"; e.LatenceMs = int.Parse(p[1], c); e.Reduction = double.Parse(p[2], c); e.Jeu = p[3] == "1"; e.Erreur = p[4].Length > 300 ? p[4].Substring(0, 300) : p[4];
                int nc; double ra;
                if (p.Length >= 7 && int.TryParse(p[5], NumberStyles.Integer, c, out nc) && double.TryParse(p[6], NumberStyles.Float, c, out ra)) { e.Canaux = Math.Max(0, Math.Min(16, nc)); e.ReductionArme = ra; }
                int re, rs, me;
                if (p.Length >= 11 && int.TryParse(p[7], NumberStyles.Integer, c, out re) && int.TryParse(p[8], NumberStyles.Integer, c, out rs) && int.TryParse(p[9], NumberStyles.Integer, c, out me))
                { e.Reecoute = Math.Max(0, Math.Min(3, re)); e.ReecouteSecondes = Math.Max(0, Math.Min(30, rs)); e.Memoire = Math.Max(0, Math.Min(10, me)); e.JeuCle = Regex.IsMatch(p[10], "^[a-z0-9]{1,24}$") ? p[10] : ""; }
            }
            catch { }
            return e;
        }

        private static string LigneEtat(bool marche, int latence, double reduction, bool jeu, string erreur, int canaux = 0, double reductionArme = 0,
            int reecoute = 0, int reecouteSecondes = 0, int memoire = 0, string jeuCle = "")
        {
            var c = CultureInfo.InvariantCulture;
            return (marche ? "marche" : "arret") + "|" + latence.ToString(c) + "|" + reduction.ToString("F1", c) + "|" + (jeu ? "1" : "0") + "|" + (erreur ?? "").Replace('|', ' ').Replace('\n', ' ').Replace('\r', ' ') + "|" + canaux.ToString(c) + "|" + reductionArme.ToString("F1", c)
                + "|" + reecoute.ToString(c) + "|" + reecouteSecondes.ToString(c) + "|" + memoire.ToString(c) + "|" + (jeuCle ?? "");
        }

        /// <summary>Écrit l'état d'un coup (fichier temporaire puis remplacement) : la page ne lit jamais une ligne à moitié écrite.</summary>
        private static void EcrireLigneEtat(string ligne)
        {
            try
            {
                string temp = FichierEtat + ".tmp";
                File.WriteAllText(temp, ligne);
                File.Move(temp, FichierEtat, true);
            }
            catch { try { File.WriteAllText(FichierEtat, ligne); } catch { } }
        }

        private static void EcrireEtat(bool marche, int latence, double reduction, bool jeu, string erreur, int canaux = 0, double reductionArme = 0,
            int reecoute = 0, int reecouteSecondes = 0, int memoire = 0, string jeuCle = "")
        {
            EcrireLigneEtat(LigneEtat(marche, latence, reduction, jeu, erreur, canaux, reductionArme, reecoute, reecouteSecondes, memoire, jeuCle));
        }

        // ------------------------------------------------------------------ allumer, couper, surveiller (côté Velox)

        /// <summary>
        /// Allume « Pas boostés » : le casque (sortie par défaut actuelle) devient la destination, le câble devient la sortie
        /// par défaut de Windows (noté dans l'Historique), et le moteur démarre. "" si c'est fait, sinon la raison.
        /// </summary>
        public static string Allumer(int force, bool jeuxSeulement)
        {
            if (!Natif.EstWindows) return "Windows uniquement.";
            var sorties = AudioWindows.Lister();
            var cables = Cables(sorties);
            if (cables.Count == 0) return "VB-Cable n'est pas installé : installe-le (gratuit), redémarre, puis réessaie.";
            var cable = cables[0];
            var r = Lire();
            string defaut = AudioWindows.ParDefaut();
            string casque = defaut != "" && defaut != cable.Sortie && !cables.Any(x => x.Sortie == defaut) ? defaut : r.Destination;
            if (casque == "" || !sorties.Any(x => x.Id == casque)) return "Mets d'abord ton casque comme sortie par défaut de Windows, puis allume « Pas boostés ».";
            if (AudioJeu.EstVoicemeeter(AudioJeu.NomSortie(AudioWindows.GuidDe(casque))))
                return "Ta sortie par défaut est Voicemeeter : « Pas boostés » doit jouer directement sur ton casque. Mets ton casque par défaut, ou garde Voicemeeter sans « Pas boostés ».";
            r.Source = cable.Entree; r.Destination = casque; r.Force = force; r.JeuxSeulement = jeuxSeulement; r.Actif = true;
            if (defaut != cable.Sortie) r.DefautAvant = defaut;
            Ecrire(r);
            string err = Demarrer();
            if (err != "") { r.Actif = false; Ecrire(r); return err; }
            if (defaut != cable.Sortie)
            {
                err = AudioWindows.DefinirParDefaut(cable.Sortie);
                if (err != "") { Couper(false); return "Windows a refusé de mettre le câble par défaut : " + err; }
            }
            return "";
        }

        /// <summary>Coupe « Pas boostés » : le moteur s'arrête et, si le câble est toujours la sortie par défaut, le casque
        /// le redevient (sinon le son n'arriverait plus nulle part).</summary>
        public static string Couper(bool remettreSortie = true)
        {
            var r = Lire();
            r.Actif = false;
            try { Ecrire(r); } catch { }
            for (int i = 0; i < 30 && EnMarche(); i++) Thread.Sleep(50);
            if (!remettreSortie || !Natif.EstWindows) return "";
            string defaut = AudioWindows.ParDefaut();
            var cables = Cables();
            if (!cables.Any(c => c.Sortie == defaut)) return "";
            string retour = r.Destination != "" ? r.Destination : r.DefautAvant;
            if (retour == "" || cables.Any(c => c.Sortie == retour)) return "Choisis ton casque comme sortie par défaut de Windows : le câble ne mène plus nulle part.";
            return AudioWindows.DefinirParDefaut(retour);
        }

        /// <summary>Lance le moteur (processus à part) s'il ne tourne pas. "" si c'est fait.</summary>
        public static string Demarrer()
        {
            if (!Natif.EstWindows) return "Windows uniquement.";
            if (EnMarche()) return "";
            try
            {
                var psi = new ProcessStartInfo(Chemins.Exe, "--moteur-son") { UseShellExecute = false, CreateNoWindow = true };
                using (var p = Process.Start(psi)) { }
                for (int i = 0; i < 40 && !EnMarche(); i++) Thread.Sleep(50);
                return EnMarche() ? "" : "Le moteur audio n'a pas démarré.";
            }
            catch (Exception ex) { return "Le moteur audio n'a pas démarré : " + ex.Message; }
        }

        /// <summary>
        /// (gardien, ouverture de la page) « Pas boostés » allumé mais moteur arrêté : relancé. Câble disparu (désinstallé) :
        /// « Pas boostés » est coupé et le casque remis par défaut.
        /// </summary>
        public static void Surveiller()
        {
            try
            {
                if (!Natif.EstWindows) return;
                var r = Lire();
                if (!r.Actif) return;
                if (Cables().Count == 0) { Couper(true); return; }
                if (!EnMarche() && Demarrer() != "")
                {
                    // le moteur ne démarre pas : le son partirait dans le câble sans arriver nulle part. Le casque est remis.
                    JournalDemarrage.Erreur("moteur audio", new InvalidOperationException("le moteur ne démarre pas : « Pas boostés » coupé, casque remis par défaut"));
                    Couper(true);
                    return;
                }
                // moteur en marche mais bloqué sur une erreur (casque débranché…) depuis plus de 20 s : même sécurité
                var et = LireEtat();
                if (et.Erreur != "" && !et.Marche) { if (erreurDepuis == DateTime.MinValue) erreurDepuis = DateTime.UtcNow; else if ((DateTime.UtcNow - erreurDepuis).TotalSeconds > 20) { erreurDepuis = DateTime.MinValue; Couper(true); } }
                else erreurDepuis = DateTime.MinValue;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("moteur audio (surveillance)", ex); }
        }

        private static DateTime erreurDepuis = DateTime.MinValue;

        /// <summary>(désinstallation) moteur arrêté, sortie par défaut remise.</summary>
        public static void Retirer()
        {
            try { if (Lire().Actif) Couper(true); } catch { }
            try { if (File.Exists(FichierEtat)) File.Delete(FichierEtat); } catch { }
        }

        // ------------------------------------------------------------------ le moteur lui-même (processus à part)

        private const int AUDCLNT_STREAMFLAGS_EVENTCALLBACK = 0x00040000, AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM = unchecked((int)0x80000000),
            AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY = 0x08000000, AUDCLNT_BUFFERFLAGS_SILENT = 0x2, CLSCTX_ALL = 0x17;
        private static readonly Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static readonly Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
        private static readonly Guid IID_IAudioRenderClient = new Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");
        private static readonly Guid FormatFlottant = new Guid("00000003-0000-0010-8000-00aa00389b71");
        private static readonly Guid IID_IAudioEndpointVolume = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");

        /// <summary>
        /// Volume de Windows sur le câble (touches de volume, curseur de la barre des tâches : le câble est la sortie par
        /// défaut) : il baisse le son AVANT Velox. Sans correction, à 30 % de volume le jeu arrive ~20 dB trop bas, rien ne
        /// dépasse le seuil du compresseur, tout est remonté pareil et les pas ne ressortent pas. Velox remonte donc l'entrée
        /// de ce que le volume a retiré (au plus 40 dB), traite au niveau plein, puis remet le volume à la sortie : le
        /// volume de Windows reste le volume d'écoute, sans affaiblir le traitement. Gains (entrée, sortie) pour ce volume.
        /// </summary>
        internal static void CompensationVolume(float db, out float entree, out float sortie)
        {
            if (float.IsNaN(db) || db >= 0) { entree = 1; sortie = 1; return; }
            float d = Math.Max(-40f, db);
            entree = (float)Math.Pow(10, -d / 20.0);
            sortie = (float)Math.Pow(10, db / 20.0);
        }

        private sealed class Flux
        {
            public IMMDevice Appareil; public IAudioClient Client; public IAudioCaptureClient Capture; public IAudioRenderClient Rendu;
            /// <summary>(capture) volume de Windows sur la sortie du câble, et l'appareil qui le porte.</summary>
            public IMMDevice SortieCable; public IAudioEndpointVolume Volume;
            public int Images;
            /// <summary>Réserve visée dans le tampon du casque (images) : petite au départ, agrandie seulement si le son
            /// manque (le tampon s'est vidé). -1 : pas encore fixée.</summary>
            public int Reserve = -1;
            public bool Amorce;
            /// <summary>Remplissage moyen du tampon du casque juste avant d'écrire (images), et le paquet ré-échantillonné.</summary>
            public Remplissage Niveau = new Remplissage();
            public float[] Etire = new float[0];
            public void Fermer()
            {
                try { if (Client != null) Client.Stop(); } catch { }
                AudioWindows.Liberer(Capture); AudioWindows.Liberer(Rendu); AudioWindows.Liberer(Client); AudioWindows.Liberer(Appareil);
                AudioWindows.Liberer(Volume); AudioWindows.Liberer(SortieCable);
                Capture = null; Rendu = null; Client = null; Appareil = null; Volume = null; SortieCable = null;
            }
        }

        /// <summary>« --moteur-son » : tourne tant que « Pas boostés » est allumé. Code 0 à l'arrêt normal.</summary>
        public static int Executer()
        {
            if (!Natif.EstWindows) { Console.Out.WriteLine("Windows uniquement."); return 2; }
            bool seul;
            using (var verrou = new Mutex(true, NomVerrou, out seul))
            {
                if (!seul) return 0;   // déjà en marche
                EventWaitHandle arret = null;
                try { arret = new EventWaitHandle(false, EventResetMode.ManualReset, NomArret); } catch { }
                try { Boucle(arret); }
                catch (Exception ex) { JournalDemarrage.Erreur("moteur audio", ex); EcrireEtat(false, 0, 0, false, ex.Message); return 1; }
                finally { try { verrou.ReleaseMutex(); } catch { } if (arret != null) arret.Dispose(); }
            }
            EcrireEtat(false, 0, 0, false, "");
            return 0;
        }

        [DllImport("avrt.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr AvSetMmThreadCharacteristicsW(string tache, ref int index);
        [DllImport("avrt.dll", SetLastError = true)]
        private static extern bool AvRevertMmThreadCharacteristics(IntPtr poignee);

        /// <summary>Jeu en cours, cherché par un fil à part (parcourir les processus prend parfois des dizaines de ms : dans le
        /// fil du son, cela ferait des trous).</summary>
        private static volatile string jeuDetecte = "";

        private static void Boucle(EventWaitHandle arret)
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            // priorité « audio pro » de Windows (MMCSS) : le fil du son passe devant le reste, même quand le jeu charge le PC
            IntPtr mmcss = IntPtr.Zero;
            try { int idx = 0; mmcss = AvSetMmThreadCharacteristicsW("Pro Audio", ref idx); } catch { }
            var fini = new ManualResetEventSlim(false);
            var chercheur = new Thread(() =>
            {
                while (!fini.IsSet) { try { jeuDetecte = JeuEnCours(); } catch { } fini.Wait(2000); }
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Velox jeu en cours" };
            chercheur.Start();
            DeplacementJoueur.Demarrer(fini);   // « mes pas plus discrets » : touches de déplacement et manette, lues à part
            // fichiers (réglages écrits par la page, demande de réécoute) lus par un fil à part : un fichier retenu quelques
            // dizaines de ms par l'antivirus faisait déborder la capture du câble (20 ms de réserve) dans le fil du son
            var demandes = new System.Collections.Concurrent.ConcurrentQueue<string>();
            ReglagesMoteurSon reglagesLus = null; var verrouReglages = new object();
            var veilleur = new Thread(() =>
            {
                DateTime vu = DateTime.MinValue;
                try { vu = File.GetLastWriteTimeUtc(FichierReglages); } catch { }
                while (!fini.IsSet)
                {
                    try
                    {
                        DateTime d = DateTime.MinValue;
                        try { d = File.GetLastWriteTimeUtc(FichierReglages); } catch { }
                        if (d != vu) { vu = d; var nr = Lire(); lock (verrouReglages) reglagesLus = nr; }
                        if (File.Exists(FichierReecoute))
                        {
                            string dem = "";
                            try { dem = File.ReadAllText(FichierReecoute).Trim(); File.Delete(FichierReecoute); } catch { }
                            if (dem != "") demandes.Enqueue(dem);
                        }
                    }
                    catch { }
                    fini.Wait(300);
                }
            }) { IsBackground = true, Priority = ThreadPriority.BelowNormal, Name = "Velox réglages du son" };
            veilleur.Start();
            string etatEnAttente = null; bool ecritureEnCours = false; var verrouEtat = new object();
            IMMDeviceEnumerator e = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            Flux cap = null, ren = null;
            IntPtr format = IntPtr.Zero;
            var evenement = new AutoResetEvent(false);
            var montre = Stopwatch.StartNew();
            long dernierJeu = -10000, dernierEtat = 0, derniereErreur = -10000;
            var r = Lire();
            CompresseurPas comp = null, ancienComp = null;
            int fondu = 0, fonduLong = 1; float[] copieFondu = new float[0];
            int canaux = 2, fe = 48000; bool flottant = true; int bits = 32;
            bool jeu = false; string erreur = "", jeuCle = "";
            float[] tampon = new float[0];
            int forceEnCours = -1, armeEnCours = -1, styleEnCours = -1;
            MemoireReecoute memoire = null;
            long dernierVolume = -10000; float gainEntree = 1, gainSortie = 1, volumeDb = 0;
            // garde-fou : si le pilote du câble n'appliquait pas le volume de Windows, la compensation dépasserait le niveau
            // plein (un son de jeu ne dépasse pas ~1) : au 3e dépassement net (×2), elle est coupée pour la session
            int depassements = 0; bool sansCompensation = false; long derniereVariation = -10000;
            var suivi = new SuiviPartie();   // « Dernières parties » : jeu, durée, réglage, explosions tenues, retard
            try
            {
                while (true)
                {
                    if (arret != null && arret.WaitOne(0)) return;   // mise à jour : arrêt demandé par l'installateur
                    long t = montre.ElapsedMilliseconds;
                    // réglages relus quand ils changent (la page les écrit ; le fil « réglages du son » les lit) ; « coupé » : arrêt
                    ReglagesMoteurSon nr = null;
                    lock (verrouReglages) { nr = reglagesLus; reglagesLus = null; }
                    if (nr != null)
                    {
                        if (!nr.Actif) return;
                        bool appareils = nr.Source != r.Source || nr.Destination != r.Destination;
                        r = nr;
                        if (appareils) { if (cap != null) cap.Fermer(); if (ren != null) ren.Fermer(); cap = null; ren = null; }
                    }
                    // demande de réécoute (fichier écrit par la page) : « go » la lance, « stop » l'arrête
                    string dem;
                    while (demandes.TryDequeue(out dem))
                    {
                        if (memoire == null && dem == "export") ThreadPool.QueueUserWorkItem(_ => EcrireResultatExport("vide"));
                        if (memoire != null)
                        {
                            if (dem == "stop") memoire.Arreter();
                            else if (dem == "go") memoire.Lancer();
                            else if (dem == "export")
                            {
                                // copie en mémoire ici (quelques ms) ; les fichiers sont écrits par un autre fil : un disque lent ou
                                // l'antivirus ne doivent pas couper le son du casque
                                float[] sans, avec; int ce = memoire.Canaux, fx = memoire.Frequence;
                                if (!memoire.Copier(out sans, out avec)) ThreadPool.QueueUserWorkItem(_ => EcrireResultatExport("vide"));
                                else ThreadPool.QueueUserWorkItem(_ =>
                                {
                                    try { string pf = PrefixeExport(); MemoireReecoute.EcrireWav(pf + "-sans.wav", sans, ce, fx); MemoireReecoute.EcrireWav(pf + "-avec.wav", avec, ce, fx); EcrireResultatExport("ok|" + pf); }
                                    catch (Exception ex) { JournalDemarrage.Erreur("enregistrement du son pour analyse", ex); EcrireResultatExport("erreur|" + ex.Message); }
                                });
                            }
                        }
                    }
                    if (t - dernierJeu >= 250)
                    {
                        dernierJeu = t;
                        jeuCle = jeuDetecte;
                        jeu = jeuCle != "";
                    }
                    // réglages effectifs : ceux retenus pour le jeu en cours (« un réglage par jeu »), sinon les réglages généraux
                    if (cap != null && ren != null)
                    {
                        int fv, av; r.Effectifs(jeuCle, out fv, out av);
                        if (comp == null || fv != forceEnCours || av != armeEnCours || r.Style != styleEnCours)
                        {
                            // réglage changé (ou réglage propre au jeu qui démarre) : le nouveau moteur prend le relais en
                            // fondu sur 30 ms, l'ancien continue pendant ce temps (repartir de zéro faisait un trou, donc un clic)
                            if (comp != null) { ancienComp = comp; fondu = 0; fonduLong = Math.Max(1, fe * 30 / 1000); }
                            comp = new CompresseurPas(fv, fe, av, true, r.Style); forceEnCours = fv; armeEnCours = av; styleEnCours = r.Style;
                            if (ancienComp != null) { ancienComp.MelangeCible = comp.MelangeCible; }
                        }
                    }
                    if (comp != null) comp.MelangeCible = !r.JeuxSeulement || jeu ? 1 : 0;
                    bool bouge = r.MesPas && DeplacementJoueur.Bouge;
                    if (comp != null) comp.JoueurBouge = bouge;
                    if (ancienComp != null) ancienComp.JoueurBouge = bouge;
                    // ouverture (ou réouverture après une erreur, une seconde plus tard)
                    if ((cap == null || ren == null) && t - derniereErreur >= 1000)
                    {
                        if (cap != null) cap.Fermer(); if (ren != null) ren.Fermer(); cap = null; ren = null;
                        if (format != IntPtr.Zero) { Marshal.FreeCoTaskMem(format); format = IntPtr.Zero; }
                        erreur = Ouvrir(e, r, evenement, out cap, out ren, out format, out canaux, out fe, out flottant, out bits);
                        if (erreur != "") { derniereErreur = t; if (cap != null) cap.Fermer(); if (ren != null) ren.Fermer(); cap = null; ren = null; }
                        else
                        {
                            int fv, av; r.Effectifs(jeuCle, out fv, out av);
                            comp = new CompresseurPas(fv, fe, av, true, r.Style); ancienComp = null; forceEnCours = fv; armeEnCours = av; styleEnCours = r.Style;
                            if (memoire == null || memoire.Canaux != canaux || memoire.Frequence != fe) memoire = new MemoireReecoute(canaux, fe, 10);
                        }
                    }
                    if (t - dernierEtat >= 500)
                    {
                        dernierEtat = t;
                        int lat = 0;
                        try { int pad; if (ren != null && ren.Client.GetCurrentPadding(out pad) == 0) lat = (int)(1000.0 * pad / fe) + 10 + (comp != null ? 2 : 0); } catch { }
                        double red = comp != null ? comp.ReductionMax : 0; if (comp != null) comp.ReductionMax = 0;
                        double redA = comp != null ? comp.ReductionArmeMax : 0; if (comp != null) comp.ReductionArmeMax = 0;
                        var finie = suivi.Relever(DateTime.Now, jeuCle, styleEnCours, forceEnCours, red, lat);
                        if (finie != null) ThreadPool.QueueUserWorkItem(_ => NoterPartie(finie));   // fichier : jamais dans le fil du son
                        // écrit par un fil à part (un fichier peut être retenu quelques ms par l'antivirus : pas dans le fil du son)
                        string ligne = LigneEtat(cap != null && ren != null, lat, red, !r.JeuxSeulement || jeu, erreur, cap != null ? canaux : 0, redA,
                            memoire != null ? memoire.Phase : 0, memoire != null ? memoire.SecondesRestantes : 0, memoire != null ? memoire.Secondes : 0, jeuCle);
                        lock (verrouEtat)
                        {
                            etatEnAttente = ligne;
                            if (!ecritureEnCours)
                            {
                                ecritureEnCours = true;
                                ThreadPool.QueueUserWorkItem(_ =>
                                {
                                    while (true)
                                    {
                                        string a;
                                        lock (verrouEtat) { a = etatEnAttente; etatEnAttente = null; if (a == null) { ecritureEnCours = false; return; } }
                                        EcrireLigneEtat(a);
                                    }
                                });
                            }
                        }
                    }
                    if (cap == null || ren == null) { Thread.Sleep(100); continue; }
                    if (t - dernierVolume >= 250 && cap.Volume != null && !sansCompensation)
                    {
                        dernierVolume = t;
                        try
                        {
                            float db;
                            if (cap.Volume.GetMasterVolumeLevel(out db) == 0)
                            {
                                // volume qui vient de changer : le son déjà en route a été produit à l'ancien volume (un dépassement
                                // passager est normal) ; le garde-fou ne compte que quand le volume est stable depuis 500 ms
                                if (Math.Abs(db - volumeDb) > 0.5f) { derniereVariation = t; depassements = 0; }
                                volumeDb = db; CompensationVolume(db, out gainEntree, out gainSortie);
                            }
                        }
                        catch { gainEntree = 1; gainSortie = 1; }
                    }
                    evenement.WaitOne(100);
                    try
                    {
                        int paquet;
                        while (cap.Capture.GetNextPacketSize(out paquet) == 0 && paquet > 0)
                        {
                            IntPtr donnees; int images, drapeaux; long p1, p2;
                            int hr = cap.Capture.GetBuffer(out donnees, out images, out drapeaux, out p1, out p2);
                            if (hr != 0) throw new InvalidOperationException("capture (code " + hr.ToString("X8", CultureInfo.InvariantCulture) + ")");
                            int n = images * canaux;
                            if (tampon.Length < n) tampon = new float[n];
                            if ((drapeaux & AUDCLNT_BUFFERFLAGS_SILENT) != 0) Array.Clear(tampon, 0, n);
                            else if (flottant) Marshal.Copy(donnees, tampon, 0, n);
                            else for (int i = 0; i < n; i++) tampon[i] = Marshal.ReadInt16(donnees, i * 2) / 32768f;
                            cap.Capture.ReleaseBuffer(images);
                            if (gainEntree != 1)
                            {
                                float crete = 0;
                                for (int i = 0; i < n; i++) { tampon[i] *= gainEntree; float a = Math.Abs(tampon[i]); if (a > crete) crete = a; }   // le volume de Windows retiré
                                if (crete > 2f && montre.ElapsedMilliseconds - derniereVariation > 500 && ++depassements >= 3)
                                {
                                    sansCompensation = true; JournalDemarrage.Noter("Moteur audio : le pilote du câble n'applique pas le volume de Windows, compensation coupée.");
                                    for (int i = 0; i < n; i++) tampon[i] /= gainEntree;
                                    gainEntree = 1; gainSortie = 1;
                                }
                            }
                            if (memoire == null || memoire.Phase == 0)
                            {
                                if (memoire != null) memoire.GarderSec(tampon, images);
                                if (ancienComp != null)
                                {
                                    if (copieFondu.Length < n) copieFondu = new float[n];
                                    Array.Copy(tampon, copieFondu, n);
                                    ancienComp.MelangeCible = comp.MelangeCible;
                                    ancienComp.Traiter(copieFondu, images, canaux);
                                    comp.Traiter(tampon, images, canaux);
                                    for (int i = 0; i < images; i++, fondu++)
                                    {
                                        float a = Math.Min(1f, fondu / (float)fonduLong);
                                        for (int q = 0; q < canaux; q++) { int j = i * canaux + q; tampon[j] = copieFondu[j] + (tampon[j] - copieFondu[j]) * a; }
                                    }
                                    if (fondu >= fonduLong) ancienComp = null;
                                }
                                else comp.Traiter(tampon, images, canaux);
                                if (memoire != null) memoire.GarderTraite(tampon, images);
                            }
                            else memoire.Remplir(tampon, images);   // réécoute : le son du jeu est remplacé par l'enregistrement
                            if (gainSortie != 1) for (int i = 0; i < n; i++) tampon[i] *= gainSortie;   // et remis : c'est le volume d'écoute
                            Jouer(ren, tampon, images, canaux, fe, flottant);
                        }
                    }
                    catch (Exception ex)
                    {
                        erreur = "Son interrompu (appareil débranché ?) : " + ex.Message;
                        derniereErreur = montre.ElapsedMilliseconds;
                        cap.Fermer(); ren.Fermer(); cap = null; ren = null;
                    }
                }
            }
            finally
            {
                fini.Set();
                try { NoterPartie(suivi.Finir(DateTime.Now)); } catch { }   // moteur arrêté en pleine partie
                if (cap != null) cap.Fermer(); if (ren != null) ren.Fermer();
                if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
                AudioWindows.Liberer(e);
                evenement.Dispose();
                if (mmcss != IntPtr.Zero) { try { AvRevertMmThreadCharacteristics(mmcss); } catch { } }
                // dernière écriture de l'état en attente, avant celle de l'arrêt (sinon elle pourrait passer après)
                for (int i = 0; i < 40; i++) { lock (verrouEtat) { if (!ecritureEnCours) break; } Thread.Sleep(25); }
            }
        }

        /// <summary>
        /// Écrit le son traité sur le casque. Réserve visée : 12 ms dans le tampon du casque au départ (le retard ajouté
        /// est surtout là). Si le tampon se vide (PC chargé : un craquement), la réserve grandit de 4 ms, jusqu'à 40 ms au
        /// plus : chaque PC trouve ainsi le plus petit retard sans craquements. Les horloges du câble et du casque ne
        /// battent jamais exactement au même rythme : le remplissage MOYEN du tampon est suivi, et s'il s'écarte de la
        /// réserve, chaque paquet est très légèrement accéléré ou ralenti (1 à 2 %, inaudible) jusqu'à y revenir. Avant, le
        /// surplus était sauté d'un coup dès qu'un paquet trouvait le tampon trop plein : avec une carte son qui ne prend
        /// pas le son au même instant que le câble le donne (GoXLR, carte USB), ça arrivait sans arrêt et chaque saut
        /// s'entendait comme un « tic ».
        /// </summary>
        private static void Jouer(Flux ren, float[] t, int images, int canaux, int fe, bool flottant)
        {
            int pad;
            if (ren.Client.GetCurrentPadding(out pad) != 0) throw new InvalidOperationException("casque indisponible");
            if (ren.Reserve < 0) ren.Reserve = fe * 12 / 1000;
            if (pad == 0)
            {
                // tampon vide : après le tout premier paquet, c'est un manque (craquement) ; la réserve grandit
                if (ren.Amorce) ren.Reserve = Math.Min(fe * 40 / 1000, ren.Reserve + fe * 4 / 1000);
                ren.Amorce = true;
                int silence = Math.Min(ren.Images, ren.Reserve);
                EcrireRendu(ren, null, silence, canaux, flottant, 0);
                pad = silence;
            }
            int sortie = ren.Niveau.Suivre(pad, images, ren.Reserve, fe);
            float[] src = t;
            if (sortie != images)
            {
                if (ren.Etire.Length < sortie * canaux) ren.Etire = new float[sortie * canaux + 64 * canaux];
                Reechantillonner(t, images, canaux, ren.Etire, sortie);
                src = ren.Etire;
            }
            if (ren.Client.GetCurrentPadding(out pad) != 0) throw new InvalidOperationException("casque indisponible");
            int restant = Math.Min(sortie, ren.Images - pad);   // tampon plein (80 ms) : seul cas où du son est encore perdu
            if (restant > 0) EcrireRendu(ren, src, restant, canaux, flottant, 0);
        }

        /// <summary>
        /// Suivi du remplissage du tampon du casque. Le casque prend le son par paquets (10 ms en général) à son propre
        /// rythme : juste avant d'écrire, le tampon paraît tantôt plein d'un paquet de plus, tantôt d'un de moins. Seule la
        /// moyenne (sur ~0,3 s) dit si les horloges dérivent. Renvoie le nombre d'images à écrire pour ce paquet.
        /// </summary>
        internal sealed class Remplissage
        {
            public double Moyenne = -1;

            public int Suivre(int pad, int images, int reserve, int fe)
            {
                if (images <= 1) return images;
                double a = Math.Min(1.0, images / (0.3 * fe));
                Moyenne = Moyenne < 0 ? pad : Moyenne + a * (pad - Moyenne);
                double ecart = Moyenne - reserve, zone = fe * 6 / 1000.0;
                if (Math.Abs(ecart) <= zone) return images;
                // 1 % (≈ 17 cents, inaudible sur un bruit de jeu), 2 % si l'écart dépasse 18 ms (après un manque)
                int k = Math.Max(1, (int)Math.Round(images * (Math.Abs(ecart) > 3 * zone ? 0.02 : 0.01)));
                return ecart > 0 ? images - k : images + k;
            }
        }

        /// <summary>
        /// Ré-échantillonne un paquet de « images » images en « sortie » images (interpolation cubique de Catmull-Rom).
        /// La première et la dernière image restent les mêmes : aucun saut avec le paquet d'avant ni celui d'après.
        /// </summary>
        internal static void Reechantillonner(float[] src, int images, int canaux, float[] dst, int sortie)
        {
            if (sortie <= 0) return;
            if (images <= 1 || sortie == 1)
            {
                for (int j = 0; j < sortie; j++) for (int c = 0; c < canaux; c++) dst[j * canaux + c] = images > 0 ? src[c] : 0;
                return;
            }
            double pas = (images - 1) / (double)(sortie - 1);
            for (int j = 0; j < sortie; j++)
            {
                double x = j * pas;
                int i = Math.Min(images - 2, (int)x);
                double f = x - i;
                int i0 = Math.Max(0, i - 1), i2 = i + 1, i3 = Math.Min(images - 1, i + 2);
                for (int c = 0; c < canaux; c++)
                {
                    double p0 = src[i0 * canaux + c], p1 = src[i * canaux + c], p2 = src[i2 * canaux + c], p3 = src[i3 * canaux + c];
                    dst[j * canaux + c] = (float)(p1 + 0.5 * f * (p2 - p0 + f * (2 * p0 - 5 * p1 + 4 * p2 - p3 + f * (3 * (p1 - p2) + p3 - p0))));
                }
            }
        }

        private static void EcrireRendu(Flux ren, float[] t, int images, int canaux, bool flottant, int debut)
        {
            IntPtr dest;
            int hr = ren.Rendu.GetBuffer(images, out dest);
            if (hr != 0) throw new InvalidOperationException("rendu (code " + hr.ToString("X8", CultureInfo.InvariantCulture) + ")");
            if (t == null) { ren.Rendu.ReleaseBuffer(images, AUDCLNT_BUFFERFLAGS_SILENT); return; }
            int n = images * canaux;
            if (flottant) Marshal.Copy(t, debut * canaux, dest, n);
            else for (int i = 0; i < n; i++) Marshal.WriteInt16(dest, i * 2, (short)Math.Round(Math.Max(-1, Math.Min(1, t[debut * canaux + i])) * 32767));
            ren.Rendu.ReleaseBuffer(images, 0);
        }

        /// <summary>Ouvre la capture du câble (mode partagé, à l'évènement) et la lecture sur le casque, au même format
        /// (Windows convertit fréquence et canaux pour le casque). "" si c'est fait.</summary>
        private static string Ouvrir(IMMDeviceEnumerator e, ReglagesMoteurSon r, AutoResetEvent evenement, out Flux cap, out Flux ren,
            out IntPtr format, out int canaux, out int fe, out bool flottant, out int bits)
        {
            cap = new Flux(); ren = new Flux(); format = IntPtr.Zero; canaux = 2; fe = 48000; flottant = true; bits = 32;
            if (r.Source == "" || r.Destination == "") return "Câble ou casque non choisi.";
            object o; Guid iid;
            if (e.GetDevice(r.Source, out cap.Appareil) != 0 || cap.Appareil == null) return "Le câble virtuel n'est plus là.";
            if (e.GetDevice(r.Destination, out ren.Appareil) != 0 || ren.Appareil == null) return "Le casque n'est plus branché.";
            iid = IID_IAudioClient;
            if (cap.Appareil.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o) != 0 || !(o is IAudioClient)) return "Capture du câble impossible.";
            cap.Client = (IAudioClient)o;
            iid = IID_IAudioClient;
            if (ren.Appareil.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o) != 0 || !(o is IAudioClient)) return "Lecture sur le casque impossible.";
            ren.Client = (IAudioClient)o;
            if (cap.Client.GetMixFormat(out format) != 0 || format == IntPtr.Zero) return "Format du câble illisible.";
            int tag = Marshal.ReadInt16(format, 0) & 0xFFFF;
            canaux = Marshal.ReadInt16(format, 2); fe = Marshal.ReadInt32(format, 4); bits = Marshal.ReadInt16(format, 14);
            flottant = tag == 3;
            if (tag == 0xFFFE && Marshal.ReadInt16(format, 16) >= 22) { var g = new byte[16]; Marshal.Copy(format + 24, g, 0, 16); flottant = new Guid(g) == FormatFlottant; }
            if (canaux < 1 || canaux > 16 || fe < 8000 || (flottant ? bits != 32 : bits != 16)) return "Format du câble non pris en charge (règle-le en 16 bits ou plus, 48 kHz).";
            // capture : tampon de 20 ms, réveil à chaque paquet
            if (cap.Client.Initialize(0, AUDCLNT_STREAMFLAGS_EVENTCALLBACK, 200000, 0, format, IntPtr.Zero) != 0) return "Windows refuse la capture du câble.";
            if (cap.Client.SetEventHandle(evenement.SafeWaitHandle.DangerousGetHandle()) != 0) return "Windows refuse la capture du câble.";
            iid = IID_IAudioCaptureClient;
            if (cap.Client.GetService(ref iid, out o) != 0 || !(o is IAudioCaptureClient)) return "Capture du câble impossible.";
            cap.Capture = (IAudioCaptureClient)o;
            // casque : même format que le câble, Windows convertit (fréquence, nombre de canaux) ; tampon de 80 ms
            int hr = ren.Client.Initialize(0, AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY, 800000, 0, format, IntPtr.Zero);
            if (hr != 0) return "Windows refuse la lecture sur le casque (code " + hr.ToString("X8", CultureInfo.InvariantCulture) + ").";
            if (ren.Client.GetBufferSize(out ren.Images) != 0 || ren.Images <= 0) return "Lecture sur le casque impossible.";
            iid = IID_IAudioRenderClient;
            if (ren.Client.GetService(ref iid, out o) != 0 || !(o is IAudioRenderClient)) return "Lecture sur le casque impossible.";
            ren.Rendu = (IAudioRenderClient)o;
            if (ren.Client.Start() != 0 || cap.Client.Start() != 0) return "Le son n'a pas pu démarrer.";
            // volume de Windows sur la sortie du câble (compensé par le moteur) ; sans lui, le traitement se fait tel quel
            try
            {
                var c = Cables().FirstOrDefault(x => x.Entree == r.Source);
                if (c != null && e.GetDevice(c.Sortie, out cap.SortieCable) == 0 && cap.SortieCable != null)
                {
                    iid = IID_IAudioEndpointVolume;
                    if (cap.SortieCable.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o) == 0 && o is IAudioEndpointVolume v) cap.Volume = v;
                }
            }
            catch { }
            return "";
        }

        private static void EcrireResultatExport(string texte)
        {
            try { string tmp = FichierExport + ".tmp"; File.WriteAllText(tmp, texte); File.Move(tmp, FichierExport, true); } catch { }
        }

        /// <summary>Début du nom des fichiers d'analyse (Bureau) : « Velox-son-AAAA-MM-JJ-HHmmss ».</summary>
        internal static string PrefixeExport()
        {
            string bureau = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(bureau) || !Directory.Exists(bureau)) bureau = Chemins.Donnees;
            Directory.CreateDirectory(bureau);
            return Path.Combine(bureau, "Velox-son-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss", CultureInfo.InvariantCulture));
        }

        /// <summary>Clé du jeu reconnu qui tourne (« cod », « valorant »…), "" si aucun.</summary>
        private static string JeuEnCours()
        {
            string cle = "";
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try { if (cle == "") { var j = AudioJeu.JeuDe(p.ProcessName + ".exe"); if (j != null) cle = j.Cle; } }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            return cle;
        }
    }

    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr notif);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr notif);
        [PreserveSig] int GetChannelCount(out int nombre);
        [PreserveSig] int SetMasterVolumeLevel(float db, ref Guid contexte);
        [PreserveSig] int SetMasterVolumeLevelScalar(float niveau, ref Guid contexte);
        [PreserveSig] int GetMasterVolumeLevel(out float db);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float niveau);
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(int images, out IntPtr donnees);
        [PreserveSig] int ReleaseBuffer(int images, int drapeaux);
    }
}
