// Nettoyage PC - « Plein potentiel » : réglages qui brident le matériel sans qu'on le sache.
// Écrans (fréquence réglée plus basse que possible, résolution non native, écran branché sur la puce graphique
// du processeur), Resizable BAR / Smart Access Memory, liaison PCIe de la carte graphique, mémoire en simple canal,
// profil EXPO / XMP. Lecture seule, sans droits administrateur.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class PointPotentiel
    {
        public string Cle = "", Titre = "", Detail = "", Correction = "", Reglage = "";
        public bool Ok;
        /// <summary>Gain attendu, en mots (affiché à côté du titre).</summary>
        public string Gain = "";
    }

    internal class EcranInfo
    {
        public string Nom = "", Adaptateur = "", Connexion = "", Gdi = "";
        public int Largeur, Hauteur, Hz, HzMax, LargeurNative, HauteurNative, HzMaxNative, HzMaxAutreDef;
        public bool Principal, SurPuceIntegree;
    }

    internal static class Potentiel
    {
        // ------------------------------------------------------------ Écrans
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAY_DEVICE
        {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DeviceKey;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string device, uint num, ref DISPLAY_DEVICE dd, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplaySettings(string device, int mode, ref DEVMODE dm);

        [StructLayout(LayoutKind.Sequential)] private struct LUID { public uint Bas; public int Haut; }
        [StructLayout(LayoutKind.Sequential)]
        private struct PATH_INFO
        {
            public LUID SourceAdaptateur; public uint SourceId, SourceMode, SourceEtat;
            public LUID CibleAdaptateur; public uint CibleId, CibleMode, Technologie, Rotation, Echelle, HzNum, HzDen, Balayage; public int Disponible; public uint CibleEtat;
            public uint Options;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct MODE_INFO { public uint Type, Id; public LUID Adaptateur; [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)] public byte[] Donnees; }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct TARGET_NAME
        {
            public uint Type, Taille; public LUID Adaptateur; public uint Id;
            public uint Options, Technologie; public ushort Fabricant, Produit; public uint Instance;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string NomEcran;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Chemin;
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct SOURCE_NAME
        {
            public uint Type, Taille; public LUID Adaptateur; public uint Id;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Gdi;
        }
        [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint nbChemins, out uint nbModes);
        [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint nbChemins, [Out] PATH_INFO[] chemins, ref uint nbModes, [Out] MODE_INFO[] modes, IntPtr topologie);
        [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref TARGET_NAME t);
        [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(ref SOURCE_NAME s);

        private static string NomConnexion(uint t)
        {
            switch (t)
            {
                case 0: return "VGA"; case 4: return "DVI"; case 5: return "HDMI"; case 10: return "DisplayPort"; case 11: return "DisplayPort intégré";
                case 12: return "UDI"; case 15: return "Miracast"; case 0x80000000: return "écran intégré";
                default: return "";
            }
        }

        /// <summary>Écrans allumés : nom, branchement, résolution et fréquence actuelles, maximum possible.</summary>
        public static List<EcranInfo> Ecrans()
        {
            var l = new List<EcranInfo>();
            if (!Natif.EstWindows) return l;
            // noms et branchements (HDMI, DisplayPort) des écrans actifs, par nom de périphérique GDI (\\.\DISPLAY1)
            var noms = new Dictionary<string, Tuple<string, string, double>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                uint nc, nm;
                if (GetDisplayConfigBufferSizes(2, out nc, out nm) == 0)
                {
                    var chemins = new PATH_INFO[nc]; var modes = new MODE_INFO[nm];
                    if (QueryDisplayConfig(2, ref nc, chemins, ref nm, modes, IntPtr.Zero) == 0)
                        for (int i = 0; i < nc; i++)
                        {
                            var p = chemins[i];
                            var s = new SOURCE_NAME { Type = 1, Adaptateur = p.SourceAdaptateur, Id = p.SourceId };
                            s.Taille = (uint)Marshal.SizeOf(typeof(SOURCE_NAME));
                            var t = new TARGET_NAME { Type = 2, Adaptateur = p.CibleAdaptateur, Id = p.CibleId };
                            t.Taille = (uint)Marshal.SizeOf(typeof(TARGET_NAME));
                            if (DisplayConfigGetDeviceInfo(ref s) != 0) continue;
                            DisplayConfigGetDeviceInfo(ref t);
                            double hz = p.HzDen > 0 ? (double)p.HzNum / p.HzDen : 0;
                            noms[s.Gdi ?? ""] = Tuple.Create((t.NomEcran ?? "").Trim(), NomConnexion(t.Technologie != 0 ? t.Technologie : p.Technologie), hz);
                        }
                }
            }
            catch { }
            var adaptateur = new DISPLAY_DEVICE { cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)) };
            for (uint i = 0; EnumDisplayDevices(null, i, ref adaptateur, 0); i++, adaptateur.cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)))
            {
                if ((adaptateur.StateFlags & 0x1) == 0) continue;   // DISPLAY_DEVICE_ATTACHED_TO_DESKTOP
                var e = new EcranInfo { Gdi = adaptateur.DeviceName, Adaptateur = (adaptateur.DeviceString ?? "").Trim(), Principal = (adaptateur.StateFlags & 0x4) != 0 };
                var mon = new DISPLAY_DEVICE { cb = Marshal.SizeOf(typeof(DISPLAY_DEVICE)) };
                if (EnumDisplayDevices(adaptateur.DeviceName, 0, ref mon, 0)) e.Nom = (mon.DeviceString ?? "").Trim();
                Tuple<string, string, double> n;
                if (noms.TryGetValue(adaptateur.DeviceName, out n)) { if (n.Item1 != "") e.Nom = n.Item1; e.Connexion = n.Item2; }
                var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                if (!EnumDisplaySettings(adaptateur.DeviceName, -1, ref dm)) continue;
                e.Largeur = dm.dmPelsWidth; e.Hauteur = dm.dmPelsHeight; e.Hz = dm.dmDisplayFrequency;
                if (n != null && n.Item3 > 1) e.Hz = (int)Math.Round(n.Item3);
                var tous = new List<Tuple<int, int, int>>();
                var m = new DEVMODE { dmSize = (short)Marshal.SizeOf(typeof(DEVMODE)) };
                for (int k = 0; k < 2000 && EnumDisplaySettings(adaptateur.DeviceName, k, ref m); k++) tous.Add(Tuple.Create(m.dmPelsWidth, m.dmPelsHeight, m.dmDisplayFrequency));
                if (tous.Count > 0)
                {
                    var natif = tous.OrderByDescending(x => (long)x.Item1 * x.Item2).First();
                    e.LargeurNative = natif.Item1; e.HauteurNative = natif.Item2;
                    e.HzMaxNative = tous.Where(x => x.Item1 == natif.Item1 && x.Item2 == natif.Item2).Max(x => x.Item3);
                    e.HzMax = tous.Where(x => x.Item1 == e.Largeur && x.Item2 == e.Hauteur).Select(x => x.Item3).DefaultIfEmpty(e.Hz).Max();
                    e.HzMaxAutreDef = tous.Max(x => x.Item3);
                }
                e.SurPuceIntegree = new CarteGraphique { Nom = e.Adaptateur }.Integree;
                if (e.Nom == "" || Regex.IsMatch(e.Nom, "Generic|générique|PnP", RegexOptions.IgnoreCase)) e.Nom = Langue.T("Écran") + " " + (l.Count + 1);
                l.Add(e);
            }
            return l;
        }

        // ------------------------------------------------------------ Carte graphique : Resizable BAR et liaison PCIe
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern int CM_Locate_DevNodeW(out uint devInst, string id, uint flags);
        [DllImport("cfgmgr32.dll")] private static extern int CM_Get_First_Log_Conf(out IntPtr logConf, uint devInst, uint flags);
        [DllImport("cfgmgr32.dll")] private static extern int CM_Get_Next_Res_Des(out IntPtr resDes, IntPtr courant, uint type, out uint typeTrouve, uint flags);
        [DllImport("cfgmgr32.dll")] private static extern int CM_Get_Res_Des_Data_Size(out uint taille, IntPtr resDes, uint flags);
        [DllImport("cfgmgr32.dll")] private static extern int CM_Get_Res_Des_Data(IntPtr resDes, byte[] tampon, uint taille, uint flags);
        [DllImport("cfgmgr32.dll")] private static extern int CM_Free_Res_Des_Handle(IntPtr resDes);
        [DllImport("cfgmgr32.dll")] private static extern int CM_Free_Log_Conf_Handle(IntPtr logConf);
        [DllImport("cfgmgr32.dll")] private static extern int CM_Get_Parent(out uint parent, uint devInst, uint flags);
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern int CM_Get_Device_IDW(uint devInst, StringBuilder tampon, int taille, uint flags);
        [StructLayout(LayoutKind.Sequential)] private struct DEVPROPKEY { public Guid Fmtid; public uint Pid; }
        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)] private static extern int CM_Get_DevNode_PropertyW(uint devInst, ref DEVPROPKEY cle, out uint type, byte[] tampon, ref uint taille, uint flags);
        private static readonly Guid ProprietesPci = new Guid("3ab22e31-8264-4b4e-9af5-a8d2d8e33e62");

        /// <summary>Plus grande plage mémoire de la carte vue par le processeur (Mo) ; 0 : inconnue.
        /// Resizable BAR / SAM actif : plusieurs Go (toute la mémoire de la carte) ; inactif : 256 Mo.</summary>
        public static long PlageMemoireMo(string pnpId)
        {
            if (!Natif.EstWindows || string.IsNullOrEmpty(pnpId)) return 0;
            try
            {
                uint dev;
                if (CM_Locate_DevNodeW(out dev, pnpId, 0) != 0) return 0;
                IntPtr conf;
                if (CM_Get_First_Log_Conf(out conf, dev, 2) != 0 && CM_Get_First_Log_Conf(out conf, dev, 3) != 0) return 0;   // ALLOC, sinon BOOT
                ulong max = 0;
                try
                {
                    foreach (uint type in new uint[] { 1, 7 })   // ResType_Mem, ResType_MemLarge
                    {
                        IntPtr courant = conf; bool premier = true;
                        while (true)
                        {
                            IntPtr rd; uint trouve;
                            if (CM_Get_Next_Res_Des(out rd, courant, type, out trouve, 0) != 0) break;
                            uint taille;
                            if (CM_Get_Res_Des_Data_Size(out taille, rd, 0) == 0 && taille >= 24)
                            {
                                var b = new byte[taille];
                                if (CM_Get_Res_Des_Data(rd, b, taille, 0) == 0)
                                {
                                    ulong debut = BitConverter.ToUInt64(b, 8), fin = BitConverter.ToUInt64(b, 16);
                                    if (fin > debut) max = Math.Max(max, fin - debut + 1);
                                }
                            }
                            if (!premier) CM_Free_Res_Des_Handle(courant);
                            courant = rd; premier = false;
                        }
                        if (!premier) CM_Free_Res_Des_Handle(courant);
                    }
                }
                finally { CM_Free_Log_Conf_Handle(conf); }
                return (long)(max / 1048576);
            }
            catch { return 0; }
        }

        private static uint Propriete(uint dev, uint pid)
        {
            var k = new DEVPROPKEY { Fmtid = ProprietesPci, Pid = pid };
            var b = new byte[4]; uint taille = 4, type;
            return CM_Get_DevNode_PropertyW(dev, ref k, out type, b, ref taille, 0) == 0 && taille == 4 ? BitConverter.ToUInt32(b, 0) : 0;
        }

        private static string Id(uint dev)
        {
            var sb = new StringBuilder(400);
            return CM_Get_Device_IDW(dev, sb, sb.Capacity, 0) == 0 ? sb.ToString() : "";
        }

        /// <summary>Liaison PCIe entre la carte graphique et la carte mère : (largeur actuelle, largeur possible, génération actuelle, génération possible).
        /// Lue sur le port de la carte mère (au-dessus des éventuels ponts internes de la carte graphique).</summary>
        public static int[] LiaisonPcie(string pnpId)
        {
            if (!Natif.EstWindows || string.IsNullOrEmpty(pnpId)) return null;
            try
            {
                uint dev;
                if (CM_Locate_DevNodeW(out dev, pnpId, 0) != 0) return null;
                var chaine = new List<uint> { dev };
                uint courant = dev, parent;
                for (int i = 0; i < 6 && CM_Get_Parent(out parent, courant, 0) == 0; i++)
                {
                    if (!Id(parent).StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase)) break;
                    chaine.Add(parent); courant = parent;
                }
                uint port = chaine[chaine.Count - 1];                                     // port de la carte mère
                uint carte = chaine.Count >= 2 ? chaine[chaine.Count - 2] : dev;           // premier élément de la carte graphique
                uint largeur = Propriete(port, 10), largeurMax = Propriete(port, 12), vitesse = Propriete(port, 9), vitesseMax = Propriete(port, 11);
                uint largeurCarte = Propriete(carte, 12), vitesseCarte = Propriete(carte, 11);
                if (largeur == 0) { largeur = Propriete(dev, 10); largeurMax = Propriete(dev, 12); vitesse = Propriete(dev, 9); vitesseMax = Propriete(dev, 11); }
                if (largeur == 0) return null;
                int possible = (int)(largeurCarte > 0 && largeurMax > 0 ? Math.Min(largeurCarte, largeurMax) : Math.Max(largeurCarte, largeurMax));
                int genPossible = (int)(vitesseCarte > 0 && vitesseMax > 0 ? Math.Min(vitesseCarte, vitesseMax) : Math.Max(vitesseCarte, vitesseMax));
                return new[] { (int)largeur, possible, (int)vitesse, genPossible };
            }
            catch { return null; }
        }

        // ------------------------------------------------------------ Bilan
        public static List<PointPotentiel> Analyser(InfosMateriel materiel = null)
        {
            var l = new List<PointPotentiel>();
            List<EcranInfo> ecrans = new List<EcranInfo>();
            try { ecrans = Ecrans(); } catch { }
            List<CarteGraphique> cartes = new List<CarteGraphique>();
            try { cartes = InfosPc.CartesGraphiques(); } catch { }
            bool dediee = cartes.Any(c => !c.Integree);

            foreach (var e in ecrans)
            {
                string nom = e.Nom + (e.Connexion != "" ? " (" + e.Connexion + ")" : "");
                if (e.HzMax >= e.Hz + 8)
                    l.Add(new PointPotentiel
                    {
                        Cle = "hz", Titre = Langue.F("{0} : réglé à {1} Hz au lieu de {2} Hz", nom, e.Hz, e.HzMax), Gain = Langue.T("image plus fluide"),
                        Detail = Langue.F("Cet écran peut afficher {0} images par seconde, mais Windows lui en envoie {1}. Tout paraît moins fluide : la souris, le défilement, les jeux.", e.HzMax, e.Hz),
                        Correction = Langue.F("Paramètres > Système > Écran > Affichage avancé : choisis {0} Hz dans « Choisir une fréquence d'actualisation ».", e.HzMax), Reglage = "ms-settings:display-advanced"
                    });
                else if (e.HzMaxAutreDef >= e.Hz + 20 && e.Largeur == e.LargeurNative && e.HzMaxNative < e.HzMaxAutreDef)
                    l.Add(new PointPotentiel
                    {
                        Cle = "cable", Titre = Langue.F("{0} : le câble limite l'écran à {1} Hz", nom, e.HzMaxNative), Gain = Langue.T("image plus fluide"),
                        Detail = Langue.F("Dans sa résolution normale, l'écran ne propose que {0} Hz, alors qu'il monte à {1} Hz en plus basse résolution : le câble ou la prise ne laissent pas passer assez d'informations.", e.HzMaxNative, e.HzMaxAutreDef),
                        Correction = Langue.T("Utilise un câble DisplayPort (ou HDMI 2.0 et plus récent) branché sur la carte graphique, puis choisis la fréquence la plus haute dans les paramètres d'affichage."), Reglage = "ms-settings:display-advanced"
                    });
                else
                    l.Add(new PointPotentiel { Cle = "hz", Ok = true, Titre = Langue.F("{0} : {1} Hz, le maximum pour cet écran", nom, e.Hz) });
                if (e.LargeurNative > 0 && (long)e.Largeur * e.Hauteur < (long)e.LargeurNative * e.HauteurNative)
                    l.Add(new PointPotentiel
                    {
                        Cle = "resolution", Titre = Langue.F("{0} : résolution {1} × {2} au lieu de {3} × {4}", nom, e.Largeur, e.Hauteur, e.LargeurNative, e.HauteurNative), Gain = Langue.T("image plus nette"),
                        Detail = Langue.T("L'écran n'est pas dans sa résolution d'origine : l'image est un peu floue. Pour agrandir les textes, utilise plutôt « Mise à l'échelle »."),
                        Correction = Langue.T("Paramètres > Système > Écran : choisis la résolution marquée « (recommandé) »."), Reglage = "ms-settings:display"
                    });
                if (dediee && e.SurPuceIntegree)
                    l.Add(new PointPotentiel
                    {
                        Cle = "branchement", Titre = Langue.F("{0} est branché sur la carte mère, pas sur la carte graphique", nom), Gain = Langue.T("beaucoup plus de FPS"),
                        Detail = Langue.F("Cet écran est relié à la puce graphique du processeur ({0}), bien moins puissante que ta carte graphique : les jeux affichés sur cet écran perdent une grande partie de leurs performances.", e.Adaptateur),
                        Correction = Langue.T("Débranche le câble de l'écran à l'arrière du PC et rebranche-le sur les prises de la carte graphique (celles qui sont horizontales, plus bas, sous les prises USB de la carte mère).")
                    });
            }

            foreach (var c in cartes.Where(x => !x.Integree && (x.Marque == "AMD" || x.Marque == "NVIDIA" || x.Marque == "Intel")).Take(1))
            {
                long plage = PlageMemoireMo(c.PnpId);
                string nom = c.Marque == "AMD" ? "Smart Access Memory (Resizable BAR)" : "Resizable BAR";
                if (plage >= 1024)
                    l.Add(new PointPotentiel { Cle = "rebar", Ok = true, Titre = Langue.F("{0} : activé", nom) });
                else if (plage > 0)
                    l.Add(new PointPotentiel
                    {
                        Cle = "rebar", Titre = Langue.F("{0} : désactivé", nom), Gain = Langue.T("jusqu'à 10 % de FPS en plus"),
                        Detail = Langue.F("Le processeur n'accède qu'à {0} Mo de la mémoire de la carte graphique à la fois, au lieu de toute sa mémoire. Avec ce réglage activé, beaucoup de jeux récents gagnent quelques images par seconde.", plage),
                        Correction = Langue.T("Dans le BIOS : active « Above 4G Decoding » puis « Re-Size BAR Support » (souvent dans les réglages PCI ou Avancés), et vérifie que le démarrage est en mode UEFI (CSM désactivé). Le bouton « Redémarrer dans le BIOS » se trouve dans l'onglet Matériel.")
                    });
                var lien = LiaisonPcie(c.PnpId);
                if (lien != null)
                {
                    string gen = lien[2] > 0 ? " · PCIe " + lien[2] + ".0" : "";
                    if (lien[1] >= 16 && lien[0] < lien[1])
                        l.Add(new PointPotentiel
                        {
                            Cle = "pcie", Titre = Langue.F("Carte graphique branchée en x{0} au lieu de x{1}", lien[0], lien[1]), Gain = Langue.T("quelques FPS en plus"),
                            Detail = Langue.F("La carte graphique ne reçoit que {0} lignes de données sur les {1} possibles. Cela arrive quand elle est dans le mauvais emplacement, ou quand un SSD M.2 partage ses lignes.", lien[0], lien[1]),
                            Correction = Langue.T("Mets la carte graphique dans l'emplacement PCIe le plus proche du processeur (le plus haut), et vérifie dans le manuel de la carte mère quels emplacements M.2 partagent ses lignes.")
                        });
                    else
                        l.Add(new PointPotentiel { Cle = "pcie", Ok = true, Titre = Langue.F("Carte graphique branchée en x{0}{1}", lien[0], gen) });
                }
            }

            if (materiel == null) try { materiel = InfosMateriel.Lire(); } catch { }
            if (materiel != null && materiel.Barrettes.Count > 0)
            {
                var b = materiel.Barrettes;
                var canaux = b.Select(x => Regex.Match(x.Banque + " " + x.Emplacement, @"CHANNEL\s*([A-H])", RegexOptions.IgnoreCase)).Where(m => m.Success).Select(m => m.Groups[1].Value.ToUpperInvariant()).Distinct().ToList();
                if (b.Count == 1)
                    l.Add(new PointPotentiel
                    {
                        Cle = "canal", Titre = Langue.T("Mémoire vive en simple canal (une seule barrette)"), Gain = Langue.T("jusqu'à 2× plus de bande passante"),
                        Detail = Langue.T("Avec une seule barrette, la mémoire n'utilise qu'un canal sur deux : le processeur et la puce graphique attendent plus souvent les données."),
                        Correction = Langue.T("Ajoute une deuxième barrette identique (même capacité et même vitesse), dans l'emplacement indiqué par le manuel de la carte mère (souvent A2 et B2).")
                    });
                else if (b.Count >= 2 && canaux.Count == 1 && b.Count(x => Regex.IsMatch(x.Banque + " " + x.Emplacement, "CHANNEL", RegexOptions.IgnoreCase)) == b.Count)
                    l.Add(new PointPotentiel
                    {
                        Cle = "canal", Titre = Langue.T("Les barrettes sont toutes sur le même canal"), Gain = Langue.T("jusqu'à 2× plus de bande passante"),
                        Detail = Langue.T("Les barrettes sont dans des emplacements du même canal : la mémoire fonctionne comme une seule barrette."),
                        Correction = Langue.T("Déplace les barrettes dans les emplacements indiqués par le manuel de la carte mère pour deux barrettes (souvent A2 et B2).")
                    });
                else if (b.Count >= 2)
                    l.Add(new PointPotentiel { Cle = "canal", Ok = true, Titre = Langue.F("Mémoire vive en double canal ({0} barrettes)", b.Count) });
                if (materiel.Bridee)
                    l.Add(new PointPotentiel
                    {
                        Cle = "expo", Titre = Langue.F("Mémoire vive bridée à {0} MT/s au lieu de {1}", materiel.Reelle, materiel.Prevue), Gain = Langue.T("plus de FPS minimum"),
                        Detail = Langue.F("Le profil {0} n'est pas activé dans le BIOS : les barrettes tournent moins vite que prévu par le fabricant.", materiel.Profil),
                        Correction = Langue.T("Onglet Matériel > « Comment l'activer (guide) » : le guide pas à pas pour ta carte mère.")
                    });
                else if (materiel.Reelle > 0)
                    l.Add(new PointPotentiel { Cle = "expo", Ok = true, Titre = Langue.F("Mémoire vive à sa vitesse prévue ({0} MT/s)", materiel.Reelle) });
            }
            return l;
        }
    }
}
