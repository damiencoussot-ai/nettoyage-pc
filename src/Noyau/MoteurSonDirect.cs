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
                string nom = NomSeul(AudioJeu.NomSortie(guidDefaut));
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
            }
            catch { }
            return e;
        }

        private static void EcrireEtat(bool marche, int latence, double reduction, bool jeu, string erreur, int canaux = 0, double reductionArme = 0)
        {
            try
            {
                var c = CultureInfo.InvariantCulture;
                File.WriteAllText(FichierEtat, (marche ? "marche" : "arret") + "|" + latence.ToString(c) + "|" + reduction.ToString("F1", c) + "|" + (jeu ? "1" : "0") + "|" + (erreur ?? "").Replace('|', ' ').Replace('\n', ' ') + "|" + canaux.ToString(c) + "|" + reductionArme.ToString("F1", c));
            }
            catch { }
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

        private sealed class Flux
        {
            public IMMDevice Appareil; public IAudioClient Client; public IAudioCaptureClient Capture; public IAudioRenderClient Rendu;
            public int Images;
            public void Fermer()
            {
                try { if (Client != null) Client.Stop(); } catch { }
                AudioWindows.Liberer(Capture); AudioWindows.Liberer(Rendu); AudioWindows.Liberer(Client); AudioWindows.Liberer(Appareil);
                Capture = null; Rendu = null; Client = null; Appareil = null;
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
                try { Boucle(); }
                catch (Exception ex) { JournalDemarrage.Erreur("moteur audio", ex); EcrireEtat(false, 0, 0, false, ex.Message); return 1; }
                finally { try { verrou.ReleaseMutex(); } catch { } }
            }
            EcrireEtat(false, 0, 0, false, "");
            return 0;
        }

        private static void Boucle()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            IMMDeviceEnumerator e = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
            Flux cap = null, ren = null;
            IntPtr format = IntPtr.Zero;
            var evenement = new AutoResetEvent(false);
            var montre = Stopwatch.StartNew();
            long derniereLecture = -10000, dernierJeu = -10000, dernierEtat = 0, derniereErreur = -10000;
            DateTime dateReglages = DateTime.MinValue;
            var r = Lire();
            CompresseurPas comp = null;
            int canaux = 2, fe = 48000; bool flottant = true; int bits = 32;
            bool jeu = false; string erreur = "";
            float[] tampon = new float[0];
            try
            {
                while (true)
                {
                    long t = montre.ElapsedMilliseconds;
                    // réglages relus quand ils changent (la page les écrit) ; « coupé » : le moteur s'arrête
                    if (t - derniereLecture >= 300)
                    {
                        derniereLecture = t;
                        DateTime d = DateTime.MinValue;
                        try { d = File.GetLastWriteTimeUtc(FichierReglages); } catch { }
                        if (d != dateReglages)
                        {
                            dateReglages = d;
                            var nr = Lire();
                            if (!nr.Actif) return;
                            bool appareils = nr.Source != r.Source || nr.Destination != r.Destination;
                            if (comp != null && (nr.Force != r.Force || nr.Arme != r.Arme)) comp = new CompresseurPas(nr.Force, fe, nr.Arme);
                            r = nr;
                            if (appareils) { if (cap != null) cap.Fermer(); if (ren != null) ren.Fermer(); cap = null; ren = null; }
                        }
                    }
                    if (r.JeuxSeulement && t - dernierJeu >= 2000)
                    {
                        dernierJeu = t;
                        jeu = JeuEnCours();
                    }
                    if (comp != null) comp.MelangeCible = !r.JeuxSeulement || jeu ? 1 : 0;
                    // ouverture (ou réouverture après une erreur, une seconde plus tard)
                    if ((cap == null || ren == null) && t - derniereErreur >= 1000)
                    {
                        if (cap != null) cap.Fermer(); if (ren != null) ren.Fermer(); cap = null; ren = null;
                        if (format != IntPtr.Zero) { Marshal.FreeCoTaskMem(format); format = IntPtr.Zero; }
                        erreur = Ouvrir(e, r, evenement, out cap, out ren, out format, out canaux, out fe, out flottant, out bits);
                        if (erreur != "") { derniereErreur = t; if (cap != null) cap.Fermer(); if (ren != null) ren.Fermer(); cap = null; ren = null; }
                        else comp = new CompresseurPas(r.Force, fe, r.Arme);
                    }
                    if (t - dernierEtat >= 500)
                    {
                        dernierEtat = t;
                        int lat = 0;
                        try { int pad; if (ren != null && ren.Client.GetCurrentPadding(out pad) == 0) lat = (int)(1000.0 * pad / fe) + 10 + (comp != null ? 2 : 0); } catch { }
                        double red = comp != null ? comp.ReductionMax : 0; if (comp != null) comp.ReductionMax = 0;
                        double redA = comp != null ? comp.ReductionArmeMax : 0; if (comp != null) comp.ReductionArmeMax = 0;
                        EcrireEtat(cap != null && ren != null, lat, red, !r.JeuxSeulement || jeu, erreur, cap != null ? canaux : 0, redA);
                    }
                    if (cap == null || ren == null) { Thread.Sleep(100); continue; }
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
                            comp.Traiter(tampon, images, canaux);
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
                if (cap != null) cap.Fermer(); if (ren != null) ren.Fermer();
                if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
                AudioWindows.Liberer(e);
                evenement.Dispose();
            }
        }

        /// <summary>
        /// Écrit le son traité sur le casque. Retard visé : 20 ms dans le tampon du casque. Les horloges du câble et du casque
        /// ne battent jamais exactement au même rythme : si le tampon dépasse 60 ms, le surplus est sauté ; s'il se vide,
        /// 10 ms de silence sont remis pour repartir sans craquements à répétition.
        /// </summary>
        private static void Jouer(Flux ren, float[] t, int images, int canaux, int fe, bool flottant)
        {
            int pad;
            if (ren.Client.GetCurrentPadding(out pad) != 0) throw new InvalidOperationException("casque indisponible");
            int vise = fe / 50, maxi = fe * 3 / 50, debut = 0;
            if (pad == 0) EcrireRendu(ren, null, fe / 100, canaux, flottant, 0);   // tampon vide : 10 ms de marge
            if (pad + images > maxi) { int trop = pad + images - vise; debut = Math.Min(images, Math.Max(0, trop)); }
            int restant = images - debut;
            if (ren.Client.GetCurrentPadding(out pad) != 0) throw new InvalidOperationException("casque indisponible");
            int place = ren.Images - pad;
            if (restant > place) restant = place;
            if (restant > 0) EcrireRendu(ren, t, restant, canaux, flottant, debut);
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
            return "";
        }

        /// <summary>Vrai si un jeu reconnu (Warzone, Valorant, CS2…) tourne.</summary>
        private static bool JeuEnCours()
        {
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    try { if (AudioJeu.EstJeu(p.ProcessName + ".exe")) return true; }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            return false;
        }
    }

    [ComImport, Guid("F294ACFC-3146-4483-A7BF-ADDCA7C260E2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioRenderClient
    {
        [PreserveSig] int GetBuffer(int images, out IntPtr donnees);
        [PreserveSig] int ReleaseBuffer(int images, int drapeaux);
    }
}
