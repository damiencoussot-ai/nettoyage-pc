// Velox - audio de Windows pour l'outil « Audio jeu » (Premium) :
//  - les sorties audio actives, la sortie par défaut (lecture, et changement noté dans l'Historique, annulable) ;
//  - le niveau de crête de la sortie par défaut (VU-mètre) et le son qu'elle joue (spectre en direct), lus par
//    l'API audio de Windows sans rien modifier ;
//  - Voicemeeter (VB-Audio) : détection, niveaux de ses bus, et son égaliseur paramétrique (Banana et Potato),
//    par sa bibliothèque officielle VoicemeeterRemote64.dll (noms des paramètres : Voicemeeter Remote API, VB-Audio).
// Sans interface : partagé par la fenêtre et l'Historique.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace NettoyagePC
{
    /// <summary>Une sortie audio active de Windows.</summary>
    internal sealed class SortieWindows
    {
        public string Id = "", Guid = "", Nom = "";
        public bool ParDefaut;
    }

    internal static class AudioWindows
    {
        /// <summary>Rôles d'une sortie par défaut : usage général, multimédia (les deux réglés ensemble, comme le fait
        /// Windows dans Paramètres > Son), communications (laissé tel quel).</summary>
        public const int RoleConsole = 0, RoleMultimedia = 1;

        // identifiant d'une sortie (rendu) : {0.0.0.00000000}.{guid}
        private static readonly Regex RegexId = new Regex(@"^\{0\.0\.0\.00000000\}\.\{([0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12})\}$", RegexOptions.CultureInvariant);

        public static bool IdValide(string id) { return id != null && RegexId.IsMatch(id); }

        /// <summary>Identifiant court (guid en minuscules) d'une sortie, "" si l'identifiant n'en est pas un.</summary>
        public static string GuidDe(string id) { var m = RegexId.Match(id ?? ""); return m.Success ? m.Groups[1].Value.ToLowerInvariant() : ""; }

        /// <summary>Sorties actives (branchées et activées), avec leur nom. Lecture seule.</summary>
        public static List<SortieWindows> Lister()
        {
            var l = new List<SortieWindows>();
            if (!Natif.EstWindows) return l;
            IMMDeviceEnumerator e = null; IMMDeviceCollection c = null;
            try
            {
                e = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                string defaut = Defaut(e, RoleConsole);
                if (e.EnumAudioEndpoints(0, 1, out c) != 0 || c == null) return l;   // sorties (eRender), actives (DEVICE_STATE_ACTIVE)
                int n;
                if (c.GetCount(out n) != 0) return l;
                for (int i = 0; i < n; i++)
                {
                    IMMDevice d = null;
                    try
                    {
                        string id;
                        if (c.Item(i, out d) != 0 || d == null || d.GetId(out id) != 0 || !IdValide(id)) continue;
                        string g = GuidDe(id);
                        l.Add(new SortieWindows { Id = id, Guid = g, Nom = AudioJeu.NomSortie(g), ParDefaut = id == defaut });
                    }
                    catch { }
                    finally { Liberer(d); }
                }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("liste des sorties audio", ex); }
            finally { Liberer(c); Liberer(e); }
            return l.OrderByDescending(s => s.ParDefaut).ThenBy(s => s.Nom, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        private static string Defaut(IMMDeviceEnumerator e, int role)
        {
            IMMDevice d = null;
            try
            {
                string id;
                if (e.GetDefaultAudioEndpoint(0, role, out d) != 0 || d == null || d.GetId(out id) != 0) return "";
                return IdValide(id) ? id : "";
            }
            catch { return ""; }
            finally { Liberer(d); }
        }

        /// <summary>Identifiant complet de la sortie par défaut pour ce rôle ("" si aucune ou illisible).</summary>
        public static string ParDefaut(int role = RoleConsole)
        {
            if (!Natif.EstWindows) return "";
            IMMDeviceEnumerator e = null;
            try { e = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom(); return Defaut(e, role); }
            catch { return ""; }
            finally { Liberer(e); }
        }

        internal static void Liberer(object o) { try { if (o != null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); } catch { } }

        private static string NomOuId(string id) { string n = AudioJeu.NomSortie(GuidDe(id)); return n != "" ? n : GuidDe(id); }

        /// <summary>
        /// Fait de cette sortie la sortie par défaut de Windows (usage général et multimédia), comme dans Paramètres > Son.
        /// L'ancienne est notée dans l'Historique (« Annuler » la remet). Renvoie "" si c'est fait, sinon la raison.
        /// </summary>
        public static string DefinirParDefaut(string id)
        {
            try
            {
                if (!Natif.EstWindows) return "Windows uniquement.";
                if (!IdValide(id) || !Lister().Any(s => s.Id == id)) return "Cette sortie n'est plus branchée ou n'est plus active.";
                string avantConsole = ParDefaut(RoleConsole), avantMultimedia = ParDefaut(RoleMultimedia);
                if (avantConsole == id && avantMultimedia == id) return "";
                string err = Changer(id, RoleConsole, RoleMultimedia);
                if (err != "") return err;
                Historique.Ajouter("sortieaudio", "Sortie audio par défaut : " + NomOuId(id),
                    new Dictionary<string, object> { { "Console", avantConsole }, { "Multimedia", avantMultimedia }, { "Nouvelle", id } });
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }

        private static string Changer(string id, params int[] roles)
        {
            IPolicyConfig p = null;
            try
            {
                p = (IPolicyConfig)new PolicyConfigCom();
                foreach (int r in roles) { int hr = p.SetDefaultEndpoint(id, r); if (hr != 0) return "Windows a refusé le changement de sortie (code " + hr.ToString("X8", CultureInfo.InvariantCulture) + ")."; }
                return ParDefaut(roles[0]) == id ? "" : "Windows n'a pas pris en compte le changement de sortie.";
            }
            catch (Exception ex) { return "Changement de sortie impossible sur ce PC : " + ex.Message; }
            finally { Liberer(p); }
        }

        /// <summary>Annule un changement de sortie par défaut noté dans l'Historique (identifiants vérifiés avant usage).</summary>
        public static string Annuler(Modif e)
        {
            string console = e.D("Console"), multimedia = e.D("Multimedia");
            if ((console != "" && !IdValide(console)) || (multimedia != "" && !IdValide(multimedia))) return "entrée de l'historique invalide (refusée par sécurité)";
            if (console == "" && multimedia == "") return "";
            if (!Natif.EstWindows) return "Windows uniquement.";
            var actives = Lister().Select(s => s.Id).ToList();
            foreach (var x in new[] { Tuple.Create(console, RoleConsole), Tuple.Create(multimedia, RoleMultimedia) })
            {
                if (x.Item1 == "") continue;
                if (!actives.Contains(x.Item1)) return "la sortie d'avant n'est plus branchée : branche-la, puis annule de nouveau";
                string err = Changer(x.Item1, x.Item2);
                if (err != "") return err;
            }
            return "";
        }
    }

    // ================================================================ spectre (calcul, testable sans Windows)

    internal static class AnalyseurSpectre
    {
        public const int Taille = 2048;
        public const int NbBandes = 56;
        /// <summary>Fréquence centrale de chaque bande (de 20 Hz à 20 kHz, espacées régulièrement à l'oreille).</summary>
        public static readonly double[] Centres = Enumerable.Range(0, NbBandes).Select(i => 20 * Math.Pow(1000, (i + 0.5) / NbBandes)).ToArray();
        private static readonly double[] fenetre = Enumerable.Range(0, Taille).Select(n => 0.5 * (1 - Math.Cos(2 * Math.PI * n / (Taille - 1)))).ToArray();

        /// <summary>Niveau de chaque bande en dB (0 dB = une sinusoïde à pleine échelle), à partir des Taille derniers
        /// échantillons (mono, de −1 à 1) joués à la fréquence « fe ».</summary>
        public static float[] Analyser(float[] echantillons, int fe)
        {
            var re = new double[Taille]; var im = new double[Taille];
            int decalage = Math.Max(0, echantillons.Length - Taille);
            for (int n = 0; n < Taille && decalage + n < echantillons.Length; n++) re[n] = echantillons[decalage + n] * fenetre[n];
            Fft(re, im);
            // amplitude d'une sinusoïde : 2|X| / (N × 0,5) (gain de la fenêtre de Hann = 0,5)
            var amp = new double[Taille / 2];
            for (int k = 1; k < Taille / 2; k++) amp[k] = 4 * Math.Sqrt(re[k] * re[k] + im[k] * im[k]) / Taille;
            var r = new float[NbBandes];
            double pas = (double)fe / Taille, rapport = Math.Pow(1000, 0.5 / NbBandes);
            for (int b = 0; b < NbBandes; b++)
            {
                double bas = Centres[b] / rapport, haut = Centres[b] * rapport, m = 0;
                int k1 = Math.Max(1, (int)Math.Ceiling(bas / pas)), k2 = Math.Min(Taille / 2 - 1, (int)Math.Floor(haut / pas));
                if (k1 <= k2) for (int k = k1; k <= k2; k++) m = Math.Max(m, amp[k]);
                else
                {
                    // bande plus étroite qu'une case de l'analyse (graves) : interpolée entre les deux cases voisines
                    double x = Centres[b] / pas; int k = Math.Max(1, Math.Min(Taille / 2 - 2, (int)Math.Floor(x)));
                    m = amp[k] + (amp[k + 1] - amp[k]) * (x - k);
                }
                r[b] = (float)(m > 1e-9 ? 20 * Math.Log10(m) : -180);
            }
            return r;
        }

        /// <summary>Transformée de Fourier rapide (en place, taille puissance de 2).</summary>
        internal static void Fft(double[] re, double[] im)
        {
            int n = re.Length;
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1) j ^= bit;
                j ^= bit;
                if (i < j) { double t = re[i]; re[i] = re[j]; re[j] = t; t = im[i]; im[i] = im[j]; im[j] = t; }
            }
            for (int len = 2; len <= n; len <<= 1)
            {
                double a = -2 * Math.PI / len, wr = Math.Cos(a), wi = Math.Sin(a);
                for (int i = 0; i < n; i += len)
                {
                    double cr = 1, ci = 0;
                    for (int k = 0; k < len / 2; k++)
                    {
                        int p = i + k, q = p + len / 2;
                        double tr = re[q] * cr - im[q] * ci, ti = re[q] * ci + im[q] * cr;
                        re[q] = re[p] - tr; im[q] = im[p] - ti; re[p] += tr; im[p] += ti;
                        double ncr = cr * wr - ci * wi; ci = cr * wi + ci * wr; cr = ncr;
                    }
                }
            }
        }
    }

    // ================================================================ écoute de la sortie par défaut (lecture seule)

    /// <summary>
    /// Écoute le son joué par la sortie par défaut (capture « loopback » de Windows, sans rien modifier) pour le spectre
    /// en direct, et lit son niveau de crête. Un seul fil (sans interface) fait tout ; il ne tourne que tant que la page
    /// Audio est affichée. Les valeurs sont relues par l'interface (tableaux remplacés d'un bloc).
    /// </summary>
    internal static class CapteurAudio
    {
        private static int demandes;
        private static Thread fil;
        private static volatile bool arret;
        private static readonly object verrou = new object();

        /// <summary>Spectre lissé en dB (une valeur par bande d'AnalyseurSpectre), null tant que rien n'est capté.</summary>
        public static volatile float[] Spectre;
        /// <summary>Crête de la sortie par défaut, gauche et droite (0 à 1).</summary>
        public static volatile float CreteG, CreteD;
        /// <summary>Raison si l'écoute est impossible ("" sinon).</summary>
        public static volatile string Erreur = "";

        public static void Demarrer()
        {
            if (!Natif.EstWindows) return;
            lock (verrou)
            {
                demandes++;
                arret = false;   // (un fil en train de s'arrêter continue)
                if (fil != null && fil.IsAlive) return;
                fil = new Thread(Boucle) { IsBackground = true, Name = "Audio jeu : écoute" };
                fil.SetApartmentState(ApartmentState.MTA);
                fil.Start();
            }
        }

        public static void Arreter()
        {
            lock (verrou)
            {
                demandes = Math.Max(0, demandes - 1);
                if (demandes == 0) arret = true;
            }
        }

        private const int AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000, AUDCLNT_BUFFERFLAGS_SILENT = 0x2, CLSCTX_ALL = 0x17;
        private static readonly Guid IID_IAudioClient = new Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2");
        private static readonly Guid IID_IAudioCaptureClient = new Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317");
        private static readonly Guid IID_IAudioMeterInformation = new Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064");
        private static readonly Guid FormatFlottant = new Guid("00000003-0000-0010-8000-00aa00389b71");

        private sealed class Ecoute
        {
            public string Id = "";
            public IMMDevice Appareil; public IAudioClient Client; public IAudioCaptureClient Capture; public IAudioMeterInformation Metre;
            public int Canaux, Bits, Fe = 48000; public bool Flottant;
            public void Fermer()
            {
                try { if (Client != null) Client.Stop(); } catch { }
                AudioWindows.Liberer(Capture); AudioWindows.Liberer(Client); AudioWindows.Liberer(Metre); AudioWindows.Liberer(Appareil);
                Capture = null; Client = null; Metre = null; Appareil = null;
            }
        }

        private static Ecoute Ouvrir(IMMDeviceEnumerator e, string id)
        {
            var x = new Ecoute { Id = id };
            IntPtr format = IntPtr.Zero;
            try
            {
                if (e.GetDevice(id, out x.Appareil) != 0 || x.Appareil == null) return x;
                object o;
                var iid = IID_IAudioMeterInformation;
                if (x.Appareil.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o) == 0) x.Metre = o as IAudioMeterInformation;
                iid = IID_IAudioClient;
                if (x.Appareil.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o) != 0 || !(o is IAudioClient)) { Erreur = "Écoute du son indisponible sur cette sortie."; return x; }
                x.Client = (IAudioClient)o;
                if (x.Client.GetMixFormat(out format) != 0 || format == IntPtr.Zero) { Erreur = "Format audio illisible."; return x; }
                int tag = Marshal.ReadInt16(format, 0) & 0xFFFF;
                x.Canaux = Marshal.ReadInt16(format, 2); x.Fe = Marshal.ReadInt32(format, 4); x.Bits = Marshal.ReadInt16(format, 14);
                if (tag == 3) x.Flottant = true;
                else if (tag == 0xFFFE && Marshal.ReadInt16(format, 16) >= 22)
                {
                    var g = new byte[16]; Marshal.Copy(format + 24, g, 0, 16);
                    x.Flottant = new Guid(g) == FormatFlottant;
                }
                if (x.Canaux <= 0 || x.Fe <= 0 || (x.Flottant ? x.Bits != 32 : x.Bits != 16)) { Erreur = "Format audio non pris en charge par le spectre."; x.Fermer(); return x; }
                // tampon de 200 ms, en mode partagé, en écoute de ce que joue la sortie
                if (x.Client.Initialize(0, AUDCLNT_STREAMFLAGS_LOOPBACK, 2000000, 0, format, IntPtr.Zero) != 0) { Erreur = "Windows refuse l'écoute de cette sortie."; x.Fermer(); return x; }
                iid = IID_IAudioCaptureClient;
                if (x.Client.GetService(ref iid, out o) != 0 || !(o is IAudioCaptureClient)) { Erreur = "Écoute du son indisponible."; x.Fermer(); return x; }
                x.Capture = (IAudioCaptureClient)o;
                if (x.Client.Start() != 0) { Erreur = "Écoute du son indisponible."; x.Fermer(); return x; }
                Erreur = "";
            }
            catch (Exception ex) { Erreur = "Écoute du son indisponible : " + ex.Message; x.Fermer(); }
            finally { if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format); }
            return x;
        }

        private static void Boucle()
        {
            IMMDeviceEnumerator e = null; Ecoute x = null;
            var anneau = new float[AnalyseurSpectre.Taille]; int pos = 0;
            float[] lisse = null;
            var montre = System.Diagnostics.Stopwatch.StartNew();
            long dernierDefaut = -10000, derniereAnalyse = 0, dernierPaquet = 0;
            var lecture = new float[AnalyseurSpectre.Taille];
            var crete = new float[2];
            try
            {
                e = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                while (!arret)
                {
                    long t = montre.ElapsedMilliseconds;
                    // la sortie par défaut a changé (casque branché...) : l'écoute la suit
                    if (t - dernierDefaut >= 1000)
                    {
                        dernierDefaut = t;
                        string id = AudioWindows.ParDefaut();
                        if (x == null || x.Id != id) { if (x != null) x.Fermer(); x = id == "" ? null : Ouvrir(e, id); if (id == "") Erreur = "Aucune sortie audio par défaut."; }
                    }
                    bool recu = false;
                    if (x != null && x.Capture != null)
                    {
                        try
                        {
                            int paquet;
                            while (x.Capture.GetNextPacketSize(out paquet) == 0 && paquet > 0)
                            {
                                IntPtr donnees; int images, drapeaux; long p1, p2;
                                if (x.Capture.GetBuffer(out donnees, out images, out drapeaux, out p1, out p2) != 0) break;
                                recu = true;
                                for (int i = 0; i < images; i++)
                                {
                                    float v = 0;
                                    if ((drapeaux & AUDCLNT_BUFFERFLAGS_SILENT) == 0)
                                    {
                                        for (int c = 0; c < x.Canaux; c++)
                                        {
                                            int o = (i * x.Canaux + c) * (x.Bits / 8);
                                            v += x.Flottant ? BitConverter.Int32BitsToSingle(Marshal.ReadInt32(donnees, o)) : Marshal.ReadInt16(donnees, o) / 32768f;
                                        }
                                        v /= x.Canaux;
                                    }
                                    anneau[pos] = v; pos = (pos + 1) % anneau.Length;
                                }
                                x.Capture.ReleaseBuffer(images);
                            }
                        }
                        catch (Exception ex) { Erreur = "Écoute interrompue : " + ex.Message; x.Fermer(); x = null; }
                    }
                    // rien n'est joué : Windows n'envoie rien, on compte du silence (le spectre retombe)
                    if (recu) dernierPaquet = t;
                    else if (t - dernierPaquet > 120) { int n = Math.Min(anneau.Length, (int)((x != null ? x.Fe : 48000) * 0.012)); for (int i = 0; i < n; i++) { anneau[pos] = 0; pos = (pos + 1) % anneau.Length; } }
                    if (x != null && x.Metre != null)
                    {
                        try
                        {
                            int nb; float g;
                            if (x.Metre.GetMeteringChannelCount(out nb) == 0 && nb >= 2 && x.Metre.GetChannelsPeakValues(2, crete) == 0) { CreteG = crete[0]; CreteD = crete[1]; }
                            else if (x.Metre.GetPeakValue(out g) == 0) { CreteG = g; CreteD = g; }
                        }
                        catch { }
                    }
                    else { CreteG = 0; CreteD = 0; }
                    if (t - derniereAnalyse >= 30)
                    {
                        double dt = Math.Max(0.001, (t - derniereAnalyse) / 1000.0);
                        derniereAnalyse = t;
                        for (int i = 0; i < lecture.Length; i++) lecture[i] = anneau[(pos + i) % anneau.Length];
                        var s = AnalyseurSpectre.Analyser(lecture, x != null ? x.Fe : 48000);
                        if (lisse == null) lisse = s.Select(v => Math.Max(v, -100f)).ToArray();
                        // montée immédiate, descente douce (36 dB par seconde)
                        for (int i = 0; i < s.Length; i++) lisse[i] = Math.Max(Math.Max(s[i], -100f), lisse[i] - (float)(36 * dt));
                        Spectre = (float[])lisse.Clone();
                    }
                    Thread.Sleep(10);
                }
            }
            catch (Exception ex) { Erreur = "Écoute du son indisponible : " + ex.Message; JournalDemarrage.Erreur("écoute audio", ex); }
            finally
            {
                if (x != null) x.Fermer();
                AudioWindows.Liberer(e);
                Spectre = null; CreteG = 0; CreteD = 0;
            }
        }
    }

    // ================================================================ Voicemeeter (VB-Audio)

    internal sealed class BusVoicemeeter
    {
        public int Index, PremierCanal;
        public string Nom = "", Label = "", Appareil = "";
        public bool Physique;
        public float G, D;   // niveaux (0 à 1)
    }

    internal sealed class EtatVoicemeeter
    {
        public bool Installe, Lance;
        /// <summary>1 = Voicemeeter, 2 = Banana, 3 = Potato (0 = inconnu).</summary>
        public int Type;
        public string Version = "", Erreur = "";
        public List<BusVoicemeeter> Bus = new List<BusVoicemeeter>();
        public string Nom { get { return Type == 3 ? "Voicemeeter Potato" : Type == 2 ? "Voicemeeter Banana" : Type == 1 ? "Voicemeeter" : ""; } }
        /// <summary>Égaliseur paramétrique des bus : Banana et Potato seulement.</summary>
        public bool EgaliseurNatif { get { return Lance && Type >= 2; } }
    }

    internal static class Voicemeeter
    {
        public const string NomEssai = "Voicemeeter";
        private const string CleDesinstallation = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\VB:Voicemeeter {17359A74-1236-5467}";

        /// <summary>Bus de chaque version : index du paramètre « Bus[i] », nom affiché, premier canal de ses niveaux
        /// (VBVMR_GetLevel, type 3). D'après la Voicemeeter Remote API.</summary>
        public static List<BusVoicemeeter> Disposition(int type)
        {
            string[] noms = type == 3 ? new[] { "A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3" } : type == 2 ? new[] { "A1", "A2", "A3", "B1", "B2" } : type == 1 ? new[] { "A", "B" } : new string[0];
            return noms.Select((n, i) => new BusVoicemeeter { Index = i, Nom = n, PremierCanal = 8 * i, Physique = n.StartsWith("A") }).ToList();
        }

        // ---- noms de paramètres que Velox écrit (seuls ceux-là peuvent être rétablis par l'Historique)
        private static readonly Regex RegexParam = new Regex(@"^Bus\[[0-7]\]\.EQ\.(on|channel\[[01]\]\.(cell\[[0-5]\]\.(on|type|f|gain|q)|Trim))$", RegexOptions.CultureInvariant);
        public static bool ParametreAutorise(string nom) { return nom != null && RegexParam.IsMatch(nom); }

        /// <summary>
        /// Réglages de l'égaliseur du bus, pour les deux premiers canaux (gauche, droite) : six cellules pour les six
        /// bandes (plateau grave pour les Graves, cloche pour les autres). Coupe-explosions actif : la première cellule
        /// devient un passe-haut (Voicemeeter n'a que six cellules). Le niveau compense la crête des filtres (« Trim »),
        /// et la balance baisse un canal. Largeur stéréo et inversion n'existent pas dans Voicemeeter.
        /// </summary>
        public static Dictionary<string, float> ScriptEq(ReglagesAudioJeu r, int bus)
        {
            var d = new Dictionary<string, float>();
            string b = "Bus[" + bus.ToString(CultureInfo.InvariantCulture) + "].EQ";
            d[b + ".on"] = r.Actif ? 1 : 0;
            var sansLargeur = r.Copie(); sansLargeur.Largeur = 0;
            double pre = r.Niveau - AudioJeu.Crete(AudioJeu.Filtres(sansLargeur));
            for (int c = 0; c < 2; c++)
            {
                string ch = b + ".channel[" + c + "]";
                for (int k = 0; k < AudioJeu.Bandes.Length && k < 6; k++)
                {
                    string cell = ch + ".cell[" + k + "]";
                    var bande = AudioJeu.Bandes[k];
                    if (k == 0 && r.CoupeBas > 0)
                    {
                        d[cell + ".on"] = 1; d[cell + ".type"] = 4; d[cell + ".f"] = r.CoupeBas; d[cell + ".gain"] = 0; d[cell + ".q"] = 1;
                        continue;
                    }
                    double g = AudioJeu.GainEffectif(r, k);
                    d[cell + ".on"] = Math.Abs(g) > 0.01 ? 1 : 0;
                    d[cell + ".type"] = bande.Type == "LSC" ? 5 : 0;
                    d[cell + ".f"] = bande.Fc;
                    d[cell + ".gain"] = (float)Math.Round(g, 1);
                    d[cell + ".q"] = (float)Math.Max(1, Math.Min(100, bande.Q));   // (Voicemeeter : de 1 à 100)
                }
                double baisse = c == 0 ? Math.Max(0, r.Balance) : Math.Max(0, -r.Balance);
                d[ch + ".Trim"] = (float)Math.Round(Math.Max(-24, Math.Min(24, pre - baisse)), 1);
            }
            return d;
        }

        /// <summary>Script de réglage Voicemeeter (« nom=valeur; ... »), nombres avec un point.</summary>
        public static string Script(Dictionary<string, float> valeurs)
        {
            return string.Join(";", valeurs.Where(v => ParametreAutorise(v.Key) && !float.IsNaN(v.Value) && !float.IsInfinity(v.Value))
                .Select(v => v.Key + "=" + v.Value.ToString("0.###", CultureInfo.InvariantCulture)));
        }

        // ---------------------------------------------------------------- bibliothèque de Voicemeeter

        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnVide();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnEntier(out int valeur);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnLireFloat(byte[] nom, out float valeur);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnLireTexte(byte[] nom, byte[] texte);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnNiveau(int type, int canal, out float valeur);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int FnScript(byte[] script);

        private static FnVide login, logout, sale;
        private static FnEntier lireType, lireVersion;
        private static FnLireFloat lireFloat;
        private static FnLireTexte lireTexte;
        private static FnNiveau niveau;
        private static FnScript script;
        private static bool charge, connecte;

        private static string dossierLu;
        private static DateTime dossierDate = DateTime.MinValue;

        /// <summary>Dossier d'installation de Voicemeeter (celui de son désinstalleur, comme l'indique VB-Audio), ou null.
        /// Relu au plus toutes les 30 secondes.</summary>
        public static string Dossier()
        {
            if (!Natif.EstWindows) return null;
            if ((DateTime.Now - dossierDate).TotalSeconds < 30) return dossierLu;
            dossierLu = LireDossier(); dossierDate = DateTime.Now;
            return dossierLu;
        }

        private static string LireDossier()
        {
            foreach (var vue in new[] { Microsoft.Win32.RegistryView.Registry64, Microsoft.Win32.RegistryView.Registry32 })
            {
                try
                {
                    using (var b = Microsoft.Win32.RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, vue))
                    using (var k = b.OpenSubKey(CleDesinstallation))
                    {
                        string s = k == null ? null : k.GetValue("UninstallString") as string;
                        if (string.IsNullOrWhiteSpace(s)) continue;
                        string d = Path.GetDirectoryName(s.Trim().Trim('"'));
                        if (!string.IsNullOrEmpty(d) && File.Exists(Path.Combine(d, "VoicemeeterRemote64.dll"))) return d;
                    }
                }
                catch { }
            }
            return null;
        }

        private static T Fonction<T>(IntPtr bib, string nom) where T : class
        {
            IntPtr p;
            return NativeLibrary.TryGetExport(bib, nom, out p) ? Marshal.GetDelegateForFunctionPointer(p, typeof(T)) as T : null;
        }

        private static bool Charger()
        {
            if (charge) return login != null;
            charge = true;
            try
            {
                string d = Dossier();
                if (d == null) return false;
                IntPtr bib = NativeLibrary.Load(Path.Combine(d, "VoicemeeterRemote64.dll"));
                login = Fonction<FnVide>(bib, "VBVMR_Login"); logout = Fonction<FnVide>(bib, "VBVMR_Logout"); sale = Fonction<FnVide>(bib, "VBVMR_IsParametersDirty");
                lireType = Fonction<FnEntier>(bib, "VBVMR_GetVoicemeeterType"); lireVersion = Fonction<FnEntier>(bib, "VBVMR_GetVoicemeeterVersion");
                lireFloat = Fonction<FnLireFloat>(bib, "VBVMR_GetParameterFloat"); lireTexte = Fonction<FnLireTexte>(bib, "VBVMR_GetParameterStringA");
                niveau = Fonction<FnNiveau>(bib, "VBVMR_GetLevel"); script = Fonction<FnScript>(bib, "VBVMR_SetParameters");
                if (login == null || lireType == null || sale == null) { login = null; return false; }
                // une seule connexion pour toute la vie de Velox, comme le demande VB-Audio
                int r = login();
                connecte = r >= 0;
                if (connecte) AppDomain.CurrentDomain.ProcessExit += (s, e) => { try { if (logout != null) logout(); } catch { } };
                return connecte;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("Voicemeeter", ex); login = null; return false; }
        }

        private static byte[] Ascii(string s) { var o = Encoding.ASCII.GetBytes(s ?? ""); Array.Resize(ref o, o.Length + 1); return o; }

        // ---------------------------------------------------------------- un seul fil pour Voicemeeter (comme l'exige sa bibliothèque)

        private static readonly BlockingCollection<Action> file = new BlockingCollection<Action>();
        private static Thread fil;
        private static int suivis;
        private static readonly object verrou = new object();
        /// <summary>Dernier état relevé (remplacé d'un bloc).</summary>
        public static volatile EtatVoicemeeter Etat = new EtatVoicemeeter();

        /// <summary>Suivre les niveaux en direct (page Audio affichée) : relevé toutes les 40 ms au lieu de toutes les 2 s.</summary>
        public static void Suivre(bool oui) { lock (verrou) { suivis = Math.Max(0, suivis + (oui ? 1 : -1)); } Demarrer(); }

        private static void Demarrer()
        {
            if (!Natif.EstWindows) return;
            lock (verrou)
            {
                if (fil != null) return;
                fil = new Thread(Boucle) { IsBackground = true, Name = "Audio jeu : Voicemeeter" };
                fil.Start();
            }
        }

        /// <summary>Exécute une opération sur le fil de Voicemeeter et attend son résultat (null si délai dépassé).</summary>
        private static T Executer<T>(Func<T> f, int delaiMs = 5000) where T : class
        {
            Demarrer();
            T r = null; var fait = new ManualResetEventSlim(false);
            file.Add(() => { try { r = f(); } catch (Exception ex) { JournalDemarrage.Erreur("Voicemeeter", ex); } finally { fait.Set(); } });
            return fait.Wait(delaiMs) ? r : null;
        }

        private static void Boucle()
        {
            long dernier = -100000;
            var montre = System.Diagnostics.Stopwatch.StartNew();
            while (true)
            {
                Action a;
                int attente;
                lock (verrou) attente = suivis > 0 ? 40 : 2000;
                if (file.TryTake(out a, attente)) { a(); continue; }
                try
                {
                    long t = montre.ElapsedMilliseconds;
                    bool complet = t - dernier >= 1000;
                    if (complet) dernier = t;
                    Etat = Relever(complet, Etat);
                }
                catch (Exception ex) { JournalDemarrage.Erreur("Voicemeeter (relevé)", ex); }
            }
        }

        private static string Texte(string nom)
        {
            if (lireTexte == null) return "";
            var b = new byte[1024];
            try { if (lireTexte(Ascii(nom), b) != 0) return ""; } catch { return ""; }
            int n = Array.IndexOf(b, (byte)0); if (n < 0) n = b.Length;
            return Encodages.Ansi.GetString(b, 0, n).Trim();
        }

        private static EtatVoicemeeter Relever(bool complet, EtatVoicemeeter avant)
        {
            var e = new EtatVoicemeeter { Installe = Dossier() != null };
            if (!e.Installe || !Charger()) { e.Erreur = e.Installe ? "Bibliothèque de Voicemeeter illisible." : ""; return e; }
            try { sale(); } catch { }   // (à appeler régulièrement : met à jour les paramètres lus)
            int type;
            if (lireType(out type) != 0 || type < 1) return e;
            e.Lance = true; e.Type = Math.Min(3, type);
            if (!complet && avant != null && avant.Lance && avant.Type == e.Type)
            {
                // relevé rapide : seulement les niveaux
                e.Version = avant.Version;
                e.Bus = avant.Bus.Select(x => new BusVoicemeeter { Index = x.Index, Nom = x.Nom, Label = x.Label, Appareil = x.Appareil, Physique = x.Physique, PremierCanal = x.PremierCanal }).ToList();
            }
            else
            {
                int v;
                if (lireVersion != null && lireVersion(out v) == 0) e.Version = ((v >> 24) & 0xFF) + "." + ((v >> 16) & 0xFF) + "." + ((v >> 8) & 0xFF) + "." + (v & 0xFF);
                e.Bus = Disposition(e.Type);
                foreach (var b in e.Bus)
                {
                    b.Label = Texte("Bus[" + b.Index + "].Label");
                    if (b.Physique) b.Appareil = Texte("Bus[" + b.Index + "].device.name");
                }
            }
            if (niveau != null)
                foreach (var b in e.Bus)
                {
                    float g, d;
                    if (niveau(3, b.PremierCanal, out g) == 0) b.G = g;
                    if (niveau(3, b.PremierCanal + 1, out d) == 0) b.D = d;
                }
            return e;
        }

        /// <summary>Relevé immédiat (pour décider avant d'écrire). Null si Voicemeeter ne répond pas.</summary>
        public static EtatVoicemeeter Lire() { return Natif.EstWindows ? Executer(() => Etat = Relever(true, null)) : new EtatVoicemeeter(); }

        // ---------------------------------------------------------------- égaliseur natif (Banana, Potato)

        /// <summary>
        /// Écrit l'égaliseur dans le bus choisi de Voicemeeter. Les valeurs d'avant sont lues puis notées dans
        /// l'Historique (« Annuler » les remet). « utilisateur » : comme pour Equalizer APO, une bascule automatique ne
        /// crée pas de ligne. Renvoie "" si c'est fait, sinon la raison.
        /// </summary>
        public static string Appliquer(ReglagesAudioJeu r, int bus, bool utilisateur)
        {
            var valeurs = ScriptEq(r, bus);
            return Ecrire(valeurs, utilisateur ? AudioJeu.Titre(r) : null);
        }

        /// <summary>Coupe l'égaliseur de ce bus (sans toucher à ses réglages).</summary>
        public static string Couper(int bus, bool utilisateur)
        {
            return Ecrire(new Dictionary<string, float> { { "Bus[" + bus + "].EQ.on", 0 } }, utilisateur ? "Audio jeu : égaliseur Voicemeeter coupé" : null);
        }

        private static string Ecrire(Dictionary<string, float> valeurs, string titre)
        {
            if (!Natif.EstWindows) return "Windows uniquement.";
            var r = Executer(() =>
            {
                var e = Relever(true, null); Etat = e;
                if (!e.Lance) return "Voicemeeter n'est pas lancé : rien n'a été modifié.";
                if (!e.EgaliseurNatif) return "Cette version de Voicemeeter n'a pas d'égaliseur sur ses bus (Banana ou Potato seulement).";
                if (script == null || lireFloat == null) return "Bibliothèque de Voicemeeter incomplète : rien n'a été modifié.";
                var avant = new Dictionary<string, object>();
                foreach (var k in valeurs.Keys)
                {
                    float v;
                    if (lireFloat(Ascii(k), out v) != 0) return "Voicemeeter ne connaît pas le réglage « " + k + " » (version trop ancienne ?) : rien n'a été modifié.";
                    avant[k] = (double)v;
                }
                int res = script(Ascii(Script(valeurs)));
                if (res != 0) return "Voicemeeter a refusé le réglage (code " + res + ") : rien n'a été modifié.";
                if (titre != null) Noter(titre, avant);
                return "";
            });
            return r ?? "Voicemeeter ne répond pas.";
        }

        private static void Noter(string titre, Dictionary<string, object> avant)
        {
            string maintenant = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            Historique.Modifier(l =>
            {
                var d = l.LastOrDefault(m => m.Type == "voicemeeter");
                DateTime maj;
                if (d != null && !d.Annule && DateTime.TryParse(d.D("Maj"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out maj) && (DateTime.Now - maj).TotalSeconds >= 0 && (DateTime.Now - maj).TotalSeconds < AudioJeu.FusionSecondes)
                {
                    // changements rapprochés : une seule ligne ; les valeurs d'avant le premier sont gardées
                    var a = Json.Dict(d.Donnees, "Avant") ?? new Dictionary<string, object>();
                    foreach (var kv in avant) if (!a.ContainsKey(kv.Key)) a[kv.Key] = kv.Value;
                    d.Donnees["Avant"] = a; d.Titre = titre; d.Donnees["Maj"] = maintenant;
                    return true;
                }
                l.Add(new Modif { Type = "voicemeeter", Titre = titre, Donnees = new Dictionary<string, object> { { "Avant", avant }, { "Maj", maintenant } } });
                return true;
            });
        }

        /// <summary>Remet les valeurs d'avant (noms de paramètres et valeurs vérifiés avant usage).</summary>
        public static string Annuler(Modif e)
        {
            var avant = Json.Dict(e.Donnees, "Avant");
            if (avant == null || avant.Count == 0 || avant.Count > 200) return "entrée de l'historique invalide (refusée par sécurité)";
            var valeurs = new Dictionary<string, float>();
            foreach (var kv in avant)
            {
                double v;
                try { v = Convert.ToDouble(kv.Value, CultureInfo.InvariantCulture); } catch { return "entrée de l'historique invalide (refusée par sécurité)"; }
                if (!ParametreAutorise(kv.Key) || double.IsNaN(v) || double.IsInfinity(v) || Math.Abs(v) > 100000) return "entrée de l'historique invalide (refusée par sécurité)";
                valeurs[kv.Key] = (float)v;
            }
            if (!Natif.EstWindows) return "Windows uniquement.";
            var r = Executer(() =>
            {
                var et = Relever(true, null); Etat = et;
                if (!et.Lance) return "Voicemeeter n'est pas lancé : lance-le, puis annule de nouveau";
                int res = script(Ascii(Script(valeurs)));
                return res == 0 ? "" : "Voicemeeter a refusé le réglage (code " + res + ")";
            });
            return r ?? "Voicemeeter ne répond pas";
        }
    }

    // ================================================================ interfaces de l'API audio de Windows
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flux, int etats, out IMMDeviceCollection appareils);
        [PreserveSig] int GetDefaultAudioEndpoint(int flux, int role, out IMMDevice appareil);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice appareil);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int nombre);
        [PreserveSig] int Item(int index, out IMMDevice appareil);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid interfaceVoulue, int contexte, IntPtr parametres, [MarshalAs(UnmanagedType.IUnknown)] out object objet);
        [PreserveSig] int OpenPropertyStore(int acces, out IntPtr proprietes);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int etat);
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"), ClassInterface(ClassInterfaceType.None)]
    internal class MMDeviceEnumeratorCom { }

    [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioMeterInformation
    {
        [PreserveSig] int GetPeakValue(out float crete);
        [PreserveSig] int GetMeteringChannelCount(out int canaux);
        [PreserveSig] int GetChannelsPeakValues(int canaux, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] float[] cretes);
        [PreserveSig] int QueryHardwareSupport(out int masque);
    }

    [ComImport, Guid("1CB9AD4C-DBFA-4c32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioClient
    {
        [PreserveSig] int Initialize(int partage, int drapeaux, long dureeTampon, long periode, IntPtr format, IntPtr session);
        [PreserveSig] int GetBufferSize(out int images);
        [PreserveSig] int GetStreamLatency(out long latence);
        [PreserveSig] int GetCurrentPadding(out int images);
        [PreserveSig] int IsFormatSupported(int partage, IntPtr format, out IntPtr proche);
        [PreserveSig] int GetMixFormat(out IntPtr format);
        [PreserveSig] int GetDevicePeriod(out long defaut, out long minimum);
        [PreserveSig] int Start();
        [PreserveSig] int Stop();
        [PreserveSig] int Reset();
        [PreserveSig] int SetEventHandle(IntPtr evenement);
        [PreserveSig] int GetService(ref Guid interfaceVoulue, [MarshalAs(UnmanagedType.IUnknown)] out object service);
    }

    [ComImport, Guid("C8ADBD64-E71E-48a0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IAudioCaptureClient
    {
        [PreserveSig] int GetBuffer(out IntPtr donnees, out int images, out int drapeaux, out long position, out long horloge);
        [PreserveSig] int ReleaseBuffer(int images);
        [PreserveSig] int GetNextPacketSize(out int images);
    }

    /// <summary>Changement de la sortie par défaut : interface de Windows non documentée par Microsoft, mais stable depuis
    /// Windows 7 et utilisée par les outils de changement de sortie (EndPointController, AudioDeviceCmdlets...).</summary>
    [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr format);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, int defaut, IntPtr format);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr format, IntPtr mix);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, int defaut, IntPtr p1, IntPtr p2);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr periode);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr cle, IntPtr valeur);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr cle, IntPtr valeur);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string id, int role);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string id, int visible);
    }

    [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9"), ClassInterface(ClassInterfaceType.None)]
    internal class PolicyConfigCom { }
}
