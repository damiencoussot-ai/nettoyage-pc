// Velox - son 7.1 virtuel au casque : pilotage de HeSuVi (logiciel libre, licence MIT, installé à part dans le dossier
// d'Equalizer APO). HeSuVi transforme le son 7.1 (ou stéréo) des jeux en son spatial au casque grâce à des HRIR
// (empreintes de virtualiseurs : Dolby Atmos, GSX, DTS…). Velox ne livre aucun de ces fichiers et ne touche ni à
// config.txt ni aux fichiers de HeSuVi : seulement aux trois petits réglages que la fenêtre de HeSuVi écrit elle-même
// (deact.txt : actif ou non ; conv.txt et conv44.txt : le modèle choisi), avec leur contenu d'avant dans l'Historique.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal sealed class ModeleHeSuVi
    {
        public string Nom = "", Description = "";
    }

    internal sealed class EtatHeSuVi
    {
        /// <summary>HeSuVi est installé (hesuvi.txt) ; inclus dans config.txt (sinon il n'agit pas).</summary>
        public bool Present, Inclus, Actif;
        public string Modele = "", Appareil = "";
        public List<ModeleHeSuVi> Modeles = new List<ModeleHeSuVi>();
    }

    internal static class HeSuVi
    {
        public const string Site = "https://sourceforge.net/projects/hesuvi/";

        /// <summary>Modèles conseillés pour les jeux (s'ils sont installés), dans cet ordre.</summary>
        public static readonly string[] Conseilles = { "gsx", "atmos", "dtshx", "razer", "sbx", "cmss_game", "dh-", "oal" };

        private static readonly Regex NomModele = new Regex(@"^[a-z0-9_+\-]{1,40}$", RegexOptions.CultureInvariant);
        private static readonly Regex LigneConv = new Regex(@"^\s*Convolution:\s*hrir\\(?:44\\)?([a-z0-9_+\-]{1,40})\.wav\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex LigneActif = new Regex(@"^\s*Eval:\s*active\s*=\s*(true|false)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex LigneDevice = new Regex(@"^\s*Eval:\s*device=""([^""\r\n]{0,200})""", RegexOptions.CultureInvariant);
        private static readonly Regex IncludeHeSuVi = new Regex(@"^\s*Include:\s*HeSuVi[\\/]hesuvi\.txt\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant);

        public static string Dossier { get { return Path.Combine(AudioJeu.DossierConfig, "HeSuVi"); } }
        private static string Fichier(string nom) { return Path.Combine(Dossier, nom); }

        public static bool ModeleValide(string nom) { return nom != null && NomModele.IsMatch(nom); }

        private static string Lire(string chemin) { try { return File.Exists(chemin) ? File.ReadAllText(chemin, Encoding.ASCII) : null; } catch { return null; } }

        /// <summary>Ce que dit HeSuVi en ce moment (lecture seule).</summary>
        public static EtatHeSuVi Etat()
        {
            var e = new EtatHeSuVi();
            try
            {
                if (!File.Exists(Fichier("hesuvi.txt"))) return e;
                e.Present = true;
                string config = Lire(Path.Combine(AudioJeu.DossierConfig, "config.txt")) ?? "";
                e.Inclus = IncludeHeSuVi.IsMatch(config);
                string deact = Lire(Fichier("deact.txt")) ?? "";
                e.Actif = Regex.IsMatch(deact, @"^\s*Eval:\s*active\s*=\s*true\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase);
                foreach (var l in (Lire(Fichier("conv.txt")) ?? "").Split('\n')) { var m = LigneConv.Match(l.TrimEnd('\r')); if (m.Success) { e.Modele = m.Groups[1].Value.ToLowerInvariant(); break; } }
                foreach (var l in (Lire(Fichier("devna.txt")) ?? "").Split('\n')) { var m = LigneDevice.Match(l); if (m.Success) { e.Appareil = m.Groups[1].Value; break; } }
                e.Modeles = Modeles();
            }
            catch { }
            return e;
        }

        /// <summary>Modèles installés (hrir\*.wav présents en 48 kHz), avec la description de HeSuVi (hrir\info.csv).</summary>
        public static List<ModeleHeSuVi> Modeles()
        {
            var desc = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var l in File.ReadAllLines(Fichier(Path.Combine("hrir", "info.csv")), Encoding.UTF8))
                {
                    int i = l.IndexOf(';');
                    if (i > 0) desc[l.Substring(0, i).Trim().TrimStart('﻿')] = l.Substring(i + 1).Trim();
                }
            }
            catch { }
            var l2 = new List<ModeleHeSuVi>();
            try
            {
                foreach (var f in Directory.GetFiles(Fichier("hrir"), "*.wav"))
                {
                    string n = Path.GetFileNameWithoutExtension(f).ToLowerInvariant();
                    if (!ModeleValide(n) || n == "none") continue;
                    string d; desc.TryGetValue(n, out d);
                    l2.Add(new ModeleHeSuVi { Nom = n, Description = (d ?? "").Length <= 200 ? d ?? "" : d.Substring(0, 200) });
                }
            }
            catch { }
            return l2.OrderBy(m => Array.IndexOf(Conseilles, m.Nom) is int k && k >= 0 ? k : 100).ThenBy(m => m.Nom, StringComparer.Ordinal).ToList();
        }

        /// <summary>Contenus des réglages de HeSuVi que Velox écrit (exactement ceux de la fenêtre de HeSuVi).</summary>
        public static string TexteActif(bool actif) { return "Eval: active=" + (actif ? "true" : "false") + "\r\n"; }
        public static string TexteConv(string modele, bool en44) { return "Convolution: hrir\\" + (en44 ? "44\\" : "") + modele + ".wav\r\n"; }

        /// <summary>
        /// Allume ou coupe le 7.1 virtuel et / ou choisit le modèle (null : inchangé). Seulement si HeSuVi est installé et que
        /// le modèle existe (en 48 et 44,1 kHz). Contenu d'avant gardé dans l'Historique (« Annuler » le remet). "" si fait.
        /// </summary>
        public static string Appliquer(bool? actif, string modele)
        {
            try
            {
                var e = Etat();
                if (!e.Present) return "HeSuVi n'est pas installé : rien n'a été modifié.";
                var ecrire = new Dictionary<string, string>();
                if (actif.HasValue) ecrire["deact.txt"] = TexteActif(actif.Value);
                if (modele != null)
                {
                    if (!ModeleValide(modele) || !File.Exists(Fichier(Path.Combine("hrir", modele + ".wav")))) return "Ce modèle de HeSuVi n'est pas installé : rien n'a été modifié.";
                    ecrire["conv.txt"] = TexteConv(modele, false);
                    if (File.Exists(Fichier(Path.Combine("hrir", "44", modele + ".wav")))) ecrire["conv44.txt"] = TexteConv(modele, true);
                }
                var avant = new Dictionary<string, object>();
                foreach (var k in ecrire.Keys) { string a = Lire(Fichier(k)); avant[k] = a == null ? "" : Convert.ToBase64String(Encoding.ASCII.GetBytes(a)); }
                foreach (var kv in ecrire) EcrireFichier(Fichier(kv.Key), kv.Value);
                string titre = actif == false ? "Son 7.1 virtuel coupé (HeSuVi)" : "Son 7.1 virtuel : " + (modele ?? e.Modele) + " (HeSuVi)";
                Noter(titre, avant);
                return "";
            }
            catch (UnauthorizedAccessException) { return "Windows refuse l'écriture dans le dossier de HeSuVi : rien n'a été modifié."; }
            catch (Exception ex) { return "HeSuVi : " + ex.Message; }
        }

        private static void EcrireFichier(string chemin, string texte)
        {
            string temp = chemin + ".velox-tmp";
            File.WriteAllText(temp, texte, Encoding.ASCII);
            File.Move(temp, chemin, true);
        }

        private static void Noter(string titre, Dictionary<string, object> avant)
        {
            string maintenant = DateTime.Now.ToString("o", CultureInfo.InvariantCulture);
            Historique.Modifier(l =>
            {
                var d = l.LastOrDefault(m => m.Type == "hesuvi");
                DateTime maj;
                if (d != null && !d.Annule && DateTime.TryParse(d.D("Maj"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out maj) && (DateTime.Now - maj).TotalSeconds >= 0 && (DateTime.Now - maj).TotalSeconds < AudioJeu.FusionSecondes)
                {
                    var a = Json.Dict(d.Donnees, "Avant") ?? new Dictionary<string, object>();
                    foreach (var kv in avant) if (!a.ContainsKey(kv.Key)) a[kv.Key] = kv.Value;
                    d.Donnees["Avant"] = a; d.Titre = titre; d.Donnees["Maj"] = maintenant;
                    return true;
                }
                l.Add(new Modif { Type = "hesuvi", Titre = titre, Donnees = new Dictionary<string, object> { { "Avant", avant }, { "Maj", maintenant } } });
                return true;
            });
        }

        /// <summary>Remet les réglages de HeSuVi d'avant (seulement ces trois fichiers, contenus vérifiés).</summary>
        public static string Annuler(Modif e)
        {
            var avant = Json.Dict(e.Donnees, "Avant");
            if (avant == null || avant.Count == 0 || avant.Count > 3) return "entrée de l'historique invalide (refusée par sécurité)";
            var remettre = new Dictionary<string, string>();
            foreach (var kv in avant)
            {
                if (kv.Key != "deact.txt" && kv.Key != "conv.txt" && kv.Key != "conv44.txt") return "entrée de l'historique invalide (refusée par sécurité)";
                string t;
                try { t = Encoding.ASCII.GetString(Convert.FromBase64String(Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? "")); } catch { return "entrée de l'historique invalide (refusée par sécurité)"; }
                bool deact = kv.Key == "deact.txt";
                if (t.Length > 400 || t.Split('\n').Any(x => x.Trim() != "" && !x.TrimStart().StartsWith("#") && !(deact ? LigneActif.IsMatch(x.TrimEnd('\r')) : LigneConv.IsMatch(x.TrimEnd('\r'))))) return "entrée de l'historique invalide (refusée par sécurité)";
                remettre[kv.Key] = t;
            }
            if (!File.Exists(Fichier("hesuvi.txt"))) return "HeSuVi n'est plus installé";
            try
            {
                foreach (var kv in remettre)
                {
                    if (kv.Value == "") { if (File.Exists(Fichier(kv.Key))) File.Delete(Fichier(kv.Key)); }   // il n'existait pas avant
                    else EcrireFichier(Fichier(kv.Key), kv.Value);
                }
                return "";
            }
            catch (Exception ex) { return ex.Message; }
        }
    }
}
