// Velox - lecture d'un son sur une sortie précise (tests de direction et d'audition).
// PlaySound (winmm) ne sait jouer que sur la sortie par défaut et en stéréo simple. Ici, le son est joué par WASAPI, en
// mode partagé, sur la sortie choisie (le casque lui-même, sans passer par le câble de « Pas boostés »), en 2 ou 8 canaux
// (7.1, ordre Windows : avant gauche, avant droite, centre, caisson, arrière gauche, arrière droite, côté gauche, côté
// droit). Windows convertit fréquence et canaux pour la sortie.
using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace NettoyagePC
{
    internal static partial class MoteurSon
    {
        /// <summary>
        /// Joue « t » (images entrelacées de « canaux » canaux, 2 ou 8, à « fe » Hz) sur la sortie « idSortie » ("" : sortie
        /// par défaut), et attend la fin, ou l'arrêt demandé par « stop ». « debut » reçoit l'instant (horloge Environment.TickCount64)
        /// où le son commence à sortir. "" si c'est fait, sinon la raison.
        /// </summary>
        public static string JouerSur(string idSortie, float[] t, int canaux, int fe, CancellationToken stop, Action<long> debut = null)
        {
            if (!Natif.EstWindows) return "Windows uniquement.";
            if (t == null || (canaux != 2 && canaux != 8) || fe < 8000) return "Son invalide.";
            IMMDeviceEnumerator e = null; var ren = new Flux(); IntPtr format = IntPtr.Zero;
            try
            {
                e = (IMMDeviceEnumerator)new MMDeviceEnumeratorCom();
                string id = string.IsNullOrEmpty(idSortie) ? AudioWindows.ParDefaut() : idSortie;
                if (string.IsNullOrEmpty(id) || e.GetDevice(id, out ren.Appareil) != 0 || ren.Appareil == null) return "Sortie audio introuvable.";
                object o; Guid iid = IID_IAudioClient;
                if (ren.Appareil.Activate(ref iid, CLSCTX_ALL, IntPtr.Zero, out o) != 0 || !(o is IAudioClient)) return "Lecture impossible sur cette sortie.";
                ren.Client = (IAudioClient)o;
                // WAVEFORMATEXTENSIBLE, flottant 32 bits
                format = Marshal.AllocCoTaskMem(40);
                Marshal.WriteInt16(format, 0, unchecked((short)0xFFFE)); Marshal.WriteInt16(format, 2, (short)canaux);
                Marshal.WriteInt32(format, 4, fe); Marshal.WriteInt32(format, 8, fe * canaux * 4);
                Marshal.WriteInt16(format, 12, (short)(canaux * 4)); Marshal.WriteInt16(format, 14, 32);
                Marshal.WriteInt16(format, 16, 22); Marshal.WriteInt16(format, 18, 32);
                Marshal.WriteInt32(format, 20, canaux == 8 ? 0x63F : 0x3);
                Marshal.Copy(FormatFlottant.ToByteArray(), 0, format + 24, 16);
                int hr = ren.Client.Initialize(0, AUDCLNT_STREAMFLAGS_AUTOCONVERTPCM | AUDCLNT_STREAMFLAGS_SRC_DEFAULT_QUALITY, 1000000, 0, format, IntPtr.Zero);
                if (hr != 0) return "Windows refuse la lecture (code " + hr.ToString("X8", CultureInfo.InvariantCulture) + ").";
                if (ren.Client.GetBufferSize(out ren.Images) != 0 || ren.Images <= 0) return "Lecture impossible sur cette sortie.";
                iid = IID_IAudioRenderClient;
                if (ren.Client.GetService(ref iid, out o) != 0 || !(o is IAudioRenderClient)) return "Lecture impossible sur cette sortie.";
                ren.Rendu = (IAudioRenderClient)o;
                int total = t.Length / canaux, ecrit = 0;
                // premier remplissage, puis départ
                int pad = 0;
                int n0 = Math.Min(total, ren.Images);
                if (n0 > 0) { EcrireRendu(ren, t, n0, canaux, true, 0); ecrit = n0; }
                if (ren.Client.Start() != 0) return "Le son n'a pas pu démarrer.";
                if (debut != null) debut(Environment.TickCount64);
                while (!stop.IsCancellationRequested)
                {
                    if (ren.Client.GetCurrentPadding(out pad) != 0) return "Sortie audio indisponible.";
                    if (ecrit >= total) { if (pad == 0) break; Thread.Sleep(10); continue; }
                    int place = ren.Images - pad, n = Math.Min(place, total - ecrit);
                    if (n > 0) { EcrireRendu(ren, t, n, canaux, true, ecrit); ecrit += n; }
                    Thread.Sleep(10);
                }
                return "";
            }
            catch (Exception ex) { return ex.Message; }
            finally
            {
                ren.Fermer();
                if (format != IntPtr.Zero) Marshal.FreeCoTaskMem(format);
                AudioWindows.Liberer(e);
            }
        }

        /// <summary>Sortie où jouer un test « au casque, sans traitement » : le casque de « Pas boostés » s'il est allumé, sinon
        /// la sortie par défaut.</summary>
        public static string SortieCasque()
        {
            var r = Lire();
            return r.Actif && AudioWindows.IdValide(r.Destination) ? r.Destination : "";
        }
    }
}
