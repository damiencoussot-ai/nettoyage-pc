// Nettoyage PC - textes légaux (licence, conditions de vente, confidentialité, mentions légales)
// Les textes sont intégrés au programme (dossier legal\ des sources) ; une copie est aussi installée dans legal\.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal static class DocumentsLegaux
    {
        /// <summary>Clé (nom du fichier sans .txt) et titre affiché.</summary>
        public static readonly List<KeyValuePair<string, string>> Liste = new List<KeyValuePair<string, string>>
        {
            new KeyValuePair<string, string>("LICENCE", "Contrat de licence"),
            new KeyValuePair<string, string>("CONDITIONS-DE-VENTE", "Conditions de vente"),
            new KeyValuePair<string, string>("CONFIDENTIALITE", "Confidentialité"),
            new KeyValuePair<string, string>("MENTIONS-LEGALES", "Mentions légales")
        };

        private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        public static string Texte(string cle)
        {
            string t;
            if (cache.TryGetValue(cle, out t)) return t;
            t = "";
            try
            {
                using (var s = typeof(DocumentsLegaux).Assembly.GetManifestResourceStream("legal." + cle + ".txt"))
                    if (s != null) using (var r = new StreamReader(s, Encoding.UTF8, true)) t = r.ReadToEnd();
            }
            catch { }
            if (t == "")
                foreach (var f in new[] { Path.Combine(Chemins.Ici, "legal", cle + ".txt"), Path.Combine(Chemins.Ici, cle + ".txt") })
                    try { if (File.Exists(f)) { t = File.ReadAllText(f, Encoding.UTF8); break; } } catch { }
            t = t.TrimStart('﻿').Replace("\r\n", "\n");
            cache[cle] = t;
            return t;
        }

        public static string Titre(string cle)
        {
            foreach (var kv in Liste) if (kv.Key == cle) return kv.Value;
            return cle;
        }

        /// <summary>Le texte contient encore des passages à compléter par l'éditeur ([PRÉNOM NOM], [ADRESSE POSTALE]...).</summary>
        public static bool AComplete(string texte) { return Regex.IsMatch(texte ?? "", @"\[(PRÉNOM|ADRESSE|E-MAIL|NUMÉRO|NOM DU MÉDIATEUR)"); }

        /// <summary>Adresse e-mail de contact de l'éditeur (mentions légales), ou "" tant qu'elle n'est pas renseignée.</summary>
        public static string Contact
        {
            get
            {
                var m = Regex.Match(Texte("MENTIONS-LEGALES"), @"E-mail\s*:\s*([^\s\[\]]+@[^\s\[\]]+)");
                return m.Success ? m.Groups[1].Value.Trim().TrimEnd('.') : "";
            }
        }
    }
}
