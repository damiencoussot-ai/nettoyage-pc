// Nettoyage PC - profil : pseudo affiché à l'accueil.
// Le profil reste sur ce PC (parametres.json) : pas de compte en ligne, pas de mot de passe.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal static class Profil
    {
        /// <summary>Pseudo accepté : lettres, chiffres, espaces et quelques signes, 24 caractères au plus.</summary>
        public static string Nettoyer(string pseudo)
        {
            string p = Regex.Replace(pseudo ?? "", @"[^\p{L}\p{N} '._-]", "").Trim();
            p = Regex.Replace(p, @"\s{2,}", " ");
            return p.Length > 24 ? p.Substring(0, 24).Trim() : p;
        }

        /// <summary>« Bonjour » ou « Bonsoir » selon l'heure.</summary>
        public static string Salut(DateTime? maintenant = null)
        {
            int h = (maintenant ?? DateTime.Now).Hour;
            return Langue.T(h >= 18 || h < 5 ? "Bonsoir" : "Bonjour");
        }

        /// <summary>Titre de l'accueil : « Bonsoir, Damien. » (ou « Bonsoir. » sans pseudo).</summary>
        public static string Accueil(string pseudo)
        {
            return string.IsNullOrEmpty(pseudo) ? Salut() + "." : Salut() + ", " + pseudo + ".";
        }
    }
}
