// Nettoyage PC - mise à la corbeille (toujours récupérable). Partagé par la version 4 et la version 5.
using System;
using System.IO;

namespace NettoyagePC
{
    internal static class Corbeille
    {
        /// <summary>Met un fichier à la corbeille (récupérable). Vrai si le fichier n'est plus là.</summary>
        public static bool Envoyer(string chemin)
        {
#if NET5_0_OR_GREATER
            // version 5 : appel direct à Windows. Seulement sur un disque local (ailleurs, Windows supprimerait
            // définitivement au lieu de mettre à la corbeille) ; et si Windows ne peut pas, il le demande.
            if (!OperatingSystem.IsWindows()) return false;
            try
            {
                string complet = Path.GetFullPath(chemin);
                var lecteur = new DriveInfo(Path.GetPathRoot(complet));
                if (lecteur.DriveType != DriveType.Fixed) return false;
                var op = new SHFILEOPSTRUCT
                {
                    wFunc = 3,   // FO_DELETE
                    pFrom = complet + "\0",
                    fFlags = 0x40 | 0x10 | 0x4 | 0x400 | 0x4000,   // corbeille, sans question, silencieux, sans boîte d'erreur, prévenir si définitif
                };
                if (SHFileOperation(ref op) != 0 || op.fAnyOperationsAborted) return false;
            }
            catch { return false; }
#else
            try { Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(chemin, Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin); }
            catch { return false; }
#endif
            return !File.Exists(chemin);
        }

#if NET5_0_OR_GREATER
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string pFrom;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string pTo;
            public ushort fFlags;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] public string lpszProgressTitle;
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT op);
#endif
    }
}
