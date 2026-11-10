// Velox - test du Wi-Fi : réseau connecté, force du signal, bande (2,4 / 5 / 6 GHz), canal, débit de liaison et
// réseaux voisins sur le même canal, lus avec l'interface officielle de Windows (wlanapi), sans droits administrateur.
// Lecture seule : rien n'est modifié, rien n'est envoyé.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NettoyagePC
{
    internal class ReseauVoisin { public string Nom = ""; public int Signal, Canal; public double Bande; }

    internal class EtatWifi
    {
        public bool Disponible;          // une carte Wi-Fi existe et le service répond
        public bool Connecte;
        public string Carte = "", Nom = "", Norme = "", Erreur = "";
        public int Signal = -1;          // 0 à 100
        public int Canal;
        public double Bande;             // 2.4, 5 ou 6 (GHz) ; 0 = inconnue
        public double DebitMbps = -1;
        public bool VoisinsLus;
        /// <summary>Windows a refusé de donner les détails (depuis Windows 11 24H2, il faut l'autorisation « Localisation »).</summary>
        public bool Refuse;
        public List<ReseauVoisin> Voisins = new List<ReseauVoisin>();
        /// <summary>Autres réseaux qui gênent le canal utilisé (même canal en 5/6 GHz, canaux qui se chevauchent en 2,4 GHz).</summary>
        public int Genants;
        /// <summary>Le même réseau est visible sur une bande plus rapide (5 ou 6 GHz) avec un signal correct.</summary>
        public bool BandeRapideDisponible;
    }

    internal static class Wifi
    {
        [DllImport("wlanapi.dll")] private static extern uint WlanOpenHandle(uint version, IntPtr reserve, out uint negociee, out IntPtr client);
        [DllImport("wlanapi.dll")] private static extern uint WlanCloseHandle(IntPtr client, IntPtr reserve);
        [DllImport("wlanapi.dll")] private static extern uint WlanEnumInterfaces(IntPtr client, IntPtr reserve, out IntPtr liste);
        [DllImport("wlanapi.dll")] private static extern uint WlanQueryInterface(IntPtr client, ref Guid carte, int code, IntPtr reserve, out uint taille, out IntPtr donnees, IntPtr type);
        [DllImport("wlanapi.dll")] private static extern uint WlanGetNetworkBssList(IntPtr client, ref Guid carte, IntPtr ssid, int typeBss, [MarshalAs(UnmanagedType.Bool)] bool securise, IntPtr reserve, out IntPtr liste);
        [DllImport("wlanapi.dll")] private static extern void WlanFreeMemory(IntPtr p);

        private const int TailleInterface = 532, TailleBss = 360;

        public static int CanalDepuisFrequence(long kHz, out double bande)
        {
            double mhz = kHz / 1000.0;
            bande = 0;
            if (mhz >= 2400 && mhz <= 2500) { bande = 2.4; return mhz >= 2484 ? 14 : (int)Math.Round((mhz - 2407) / 5); }
            if (mhz >= 5150 && mhz < 5925) { bande = 5; return (int)Math.Round((mhz - 5000) / 5); }
            if (mhz >= 5925 && mhz <= 7125) { bande = 6; return (int)Math.Round((mhz - 5950) / 5); }
            return 0;
        }

        private static string NomNorme(int phy)
        {
            switch (phy)
            {
                case 4: return "Wi-Fi 2 (802.11a)"; case 5: return "Wi-Fi 1 (802.11b)"; case 6: return "Wi-Fi 3 (802.11g)";
                case 7: return "Wi-Fi 4 (802.11n)"; case 8: return "Wi-Fi 5 (802.11ac)"; case 10: return "Wi-Fi 6 (802.11ax)"; case 11: return "Wi-Fi 7 (802.11be)";
            }
            return "";
        }

        private static string LireSsid(IntPtr p, int decalage)
        {
            int n = Marshal.ReadInt32(p, decalage);
            if (n <= 0 || n > 32) return "";
            var o = new byte[n];
            Marshal.Copy(IntPtr.Add(p, decalage + 4), o, 0, n);
            try { return Encoding.UTF8.GetString(o); } catch { return ""; }
        }

        public static EtatWifi Lire()
        {
            var e = new EtatWifi();
            if (!OperatingSystem.IsWindows()) { e.Erreur = "Windows seulement"; return e; }
            IntPtr client = IntPtr.Zero, liste = IntPtr.Zero;
            try
            {
                uint v;
                uint r = WlanOpenHandle(2, IntPtr.Zero, out v, out client);
                if (r != 0) { e.Erreur = "service Wi-Fi absent (" + r + ")"; return e; }
                if (WlanEnumInterfaces(client, IntPtr.Zero, out liste) != 0 || liste == IntPtr.Zero) { e.Erreur = "aucune carte"; return e; }
                int nombre = Marshal.ReadInt32(liste, 0);
                if (nombre <= 0) { e.Erreur = "aucune carte"; return e; }
                e.Disponible = true;
                // la première carte connectée (sinon la première tout court)
                int choisie = 0;
                for (int i = 0; i < nombre && i < 8; i++)
                    if (Marshal.ReadInt32(liste, 8 + i * TailleInterface + 528) == 1) { choisie = i; break; }   // wlan_interface_state_connected
                IntPtr pi = IntPtr.Add(liste, 8 + choisie * TailleInterface);
                var guid = Marshal.PtrToStructure<Guid>(pi);
                e.Carte = (Marshal.PtrToStringUni(IntPtr.Add(pi, 16), 256) ?? "").TrimEnd('\0');
                int fin = e.Carte.IndexOf('\0'); if (fin >= 0) e.Carte = e.Carte.Substring(0, fin);
                e.Connecte = Marshal.ReadInt32(pi, 528) == 1;
                byte[] bssid = null;
                if (e.Connecte)
                {
                    uint taille; IntPtr donnees;
                    uint rc = WlanQueryInterface(client, ref guid, 7, IntPtr.Zero, out taille, out donnees, IntPtr.Zero);   // connexion en cours
                    if (rc == 5) e.Refuse = true;   // accès refusé
                    if (rc == 0 && donnees != IntPtr.Zero)
                    {
                        try
                        {
                            if (taille >= 588)
                            {
                                e.Nom = LireSsid(donnees, 520);
                                bssid = new byte[6]; Marshal.Copy(IntPtr.Add(donnees, 560), bssid, 0, 6);
                                e.Norme = NomNorme(Marshal.ReadInt32(donnees, 568));
                                e.Signal = Math.Max(0, Math.Min(100, Marshal.ReadInt32(donnees, 576)));
                                e.DebitMbps = Math.Min((uint)Marshal.ReadInt32(donnees, 580), (uint)Marshal.ReadInt32(donnees, 584)) / 1000.0;
                            }
                        }
                        finally { WlanFreeMemory(donnees); }
                    }
                    if (WlanQueryInterface(client, ref guid, 8, IntPtr.Zero, out taille, out donnees, IntPtr.Zero) == 0 && donnees != IntPtr.Zero)   // numéro de canal
                    {
                        try { if (taille >= 4) e.Canal = Marshal.ReadInt32(donnees, 0); } finally { WlanFreeMemory(donnees); }
                    }
                }
                // réseaux visibles (Windows 11 récent peut le refuser sans l'autorisation « Localisation » : on fait sans)
                IntPtr bss;
                if (WlanGetNetworkBssList(client, ref guid, IntPtr.Zero, 3, false, IntPtr.Zero, out bss) == 0 && bss != IntPtr.Zero)
                {
                    try
                    {
                        int n = Marshal.ReadInt32(bss, 4);
                        e.VoisinsLus = true;
                        for (int i = 0; i < n && i < 400; i++)
                        {
                            IntPtr b = IntPtr.Add(bss, 8 + i * TailleBss);
                            var id = new byte[6]; Marshal.Copy(IntPtr.Add(b, 40), id, 0, 6);
                            double bande;
                            int canal = CanalDepuisFrequence((uint)Marshal.ReadInt32(b, 92), out bande);
                            var voisin = new ReseauVoisin { Nom = LireSsid(b, 0), Signal = Math.Max(0, Math.Min(100, Marshal.ReadInt32(b, 60))), Canal = canal, Bande = bande };
                            if (bssid != null && id.SequenceEqual(bssid)) { e.Bande = bande; if (e.Canal == 0) e.Canal = canal; continue; }   // c'est notre box
                            e.Voisins.Add(voisin);
                        }
                    }
                    finally { WlanFreeMemory(bss); }
                }
                if (e.Connecte && e.Bande == 0 && e.Canal > 0) e.Bande = e.Canal <= 14 ? 2.4 : 5;
                if (e.Connecte && e.Canal > 0)
                {
                    // gêne : signal d'au moins 35 %, d'un autre réseau, sur le même canal (ou à moins de 5 canaux en 2,4 GHz)
                    e.Genants = e.Voisins.Count(x => x.Signal >= 35 && x.Bande == e.Bande && x.Nom != e.Nom && (e.Bande == 2.4 ? Math.Abs(x.Canal - e.Canal) < 5 : x.Canal == e.Canal));
                    e.BandeRapideDisponible = e.Bande == 2.4 && e.Nom != "" && e.Voisins.Any(x => x.Nom == e.Nom && x.Bande >= 5 && x.Signal >= 45);
                }
            }
            catch (DllNotFoundException) { e.Erreur = "wlanapi absent"; }
            catch (EntryPointNotFoundException) { e.Erreur = "wlanapi incomplet"; }
            catch (Exception ex) { e.Erreur = ex.GetType().Name + " : " + ex.Message; }
            finally
            {
                try { if (liste != IntPtr.Zero) WlanFreeMemory(liste); } catch { }
                try { if (client != IntPtr.Zero) WlanCloseHandle(client, IntPtr.Zero); } catch { }
            }
            return e;
        }

        /// <summary>Verdict (phrase) et conseils, du plus important au moins important.</summary>
        public static List<string> Conseils(EtatWifi e, out string verdict, out int niveau)
        {
            var c = new List<string>();
            niveau = 0;
            if (!e.Disponible) { verdict = "Ce PC n'a pas de Wi-Fi (ou il est désactivé)."; c.Add("Si ton PC est branché à la box par un câble réseau, c'est la meilleure connexion possible pour jouer : rien à faire."); return c; }
            if (!e.Connecte) { verdict = "Le Wi-Fi n'est connecté à aucun réseau."; c.Add("Si tu es en câble réseau, c'est parfait : le Wi-Fi n'est pas utilisé."); return c; }
            if (e.Signal < 0)
            {
                verdict = "Wi-Fi connecté, mais Windows ne laisse pas lire le signal."; niveau = 1;
                c.Add("Windows 11 demande l'autorisation « Localisation » pour montrer les informations Wi-Fi : ouvre Paramètres > Confidentialité et sécurité > Localisation, active « Services de localisation » et « Autoriser les applications de bureau à accéder à votre position », puis rouvre ce test.");
                c.Add("Velox ne lit jamais ta position : Windows range simplement les noms des réseaux Wi-Fi dans cette autorisation.");
                return c;
            }
            if (e.Signal >= 75) verdict = "Signal excellent.";
            else if (e.Signal >= 55) { verdict = "Signal correct."; niveau = 1; }
            else { verdict = "Signal faible : c'est lui qui ralentit ta connexion."; niveau = 2; }
            if (e.Signal >= 0 && e.Signal < 55) c.Add("Rapproche le PC de la box, ou enlève les obstacles entre les deux (murs épais, meubles, aquarium, micro-ondes).");
            if (e.BandeRapideDisponible) { c.Add("Ta box émet aussi en 5 GHz : connecte-toi à ce réseau-là, il est plus rapide et moins encombré que le 2,4 GHz."); niveau = Math.Max(niveau, 1); }
            else if (e.Bande == 2.4 && e.Signal >= 55) c.Add("Tu es en 2,4 GHz : cette bande porte loin mais elle est lente et encombrée. Si ta box propose le 5 GHz, préfère-le quand tu es près d'elle.");
            if (e.Genants >= 4) { c.Add("Ton canal est partagé avec beaucoup de réseaux voisins : dans les réglages de ta box, choisis un autre canal Wi-Fi (ou laisse-la choisir automatiquement)."); niveau = Math.Max(niveau, 1); }
            if (e.DebitMbps >= 0 && e.DebitMbps < 60 && e.Signal >= 55) c.Add("Le débit entre le PC et la box est bas malgré un bon signal : un pilote Wi-Fi à jour ou une box plus récente (Wi-Fi 6) aideraient.");
            c.Add("Pour jouer en ligne, un câble réseau reste ce qu'il y a de mieux : ping plus bas et aucune coupure.");
            return c;
        }
    }
}
