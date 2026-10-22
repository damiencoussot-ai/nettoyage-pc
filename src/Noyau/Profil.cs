// Nettoyage PC - profil (4.5) : pseudo affiché et prononcé, briefing vocal à l'ouverture.
// Le profil reste sur ce PC (parametres.json) : pas de compte en ligne, pas de mot de passe.
// La voix est celle de Windows (SAPI), elle fonctionne sans Internet ; rien n'est exécuté par le briefing.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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

        /// <summary>Texte du briefing : état du PC et améliorations proposées, à lire à voix haute.</summary>
        public static string TexteBriefing(string pseudo, int score, List<PointBilan> points)
        {
            var a = points.Where(p => p.Etat != "ok").OrderBy(p => p.Etat == "probleme" ? 0 : 1).ToList();
            var paroles = a.Select(p => p.Parole != "" ? p.Parole : Langue.T("régler un point de la liste")).Distinct().ToList();
            string debut = Salut() + (string.IsNullOrEmpty(pseudo) ? "." : " " + pseudo + ".");
            bool probleme = a.Any(p => p.Etat == "probleme");
            if (paroles.Count == 0) return debut + " " + Langue.F("Ton PC est en pleine forme : {0} sur 100. Rien à faire aujourd'hui.", score);
            string etat = probleme ? Langue.F("Ton PC a besoin d'attention : {0} sur 100.", score) : score >= 85 ? Langue.F("Ton PC est en forme : {0} sur 100.", score) : Langue.F("Ton PC peut être amélioré : {0} sur 100.", score);
            string suite;
            if (paroles.Count == 1) suite = Langue.F("J'ai une amélioration à te proposer : {0}.", paroles[0]);
            else
            {
                suite = Langue.F("J'ai {0} améliorations à te proposer. D'abord, {1}. Ensuite, {2}.", paroles.Count, paroles[0], paroles[1]);
                if (paroles.Count > 2) suite += " " + Langue.F(paroles.Count == 3 ? "Et une autre dans la liste." : "Et {0} autres dans la liste.", paroles.Count - 2);
            }
            return debut + " " + etat + " " + suite + " " + Langue.T("Clique sur « Optimiser en 1 clic », ou sur les boutons de la liste.");
        }

        /// <summary>Faut-il lire le briefing maintenant (selon le réglage « Quand ? ») ?</summary>
        public static bool BriefingAttendu(Parametres r, bool aFaire)
        {
            if (!r.ProfilCree || r.Briefing == "jamais") return false;
            if (r.Briefing == "ouverture") return true;
            if (r.Briefing == "besoin") return aFaire && r.DernierBriefing != DateTime.Now.ToString("yyyy-MM-dd");
            return r.DernierBriefing != DateTime.Now.ToString("yyyy-MM-dd");
        }
    }

    /// <summary>Synthèse vocale de Windows (SAPI, voix installées avec Windows). Inactive si elle n'existe pas.</summary>
    internal static class Voix
    {
        private static object sapi;
        private static bool indisponible;

        public static bool Disponible { get { return Natif.EstWindows && Objet() != null; } }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static object Objet()
        {
            if (sapi != null || indisponible) return sapi;
            try
            {
                var t = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (t != null) sapi = Activator.CreateInstance(t);
            }
            catch (Exception ex) { JournalDemarrage.Erreur("synthèse vocale", ex); }
            if (sapi == null) indisponible = true;
            return sapi;
        }

        /// <summary>Voix installées : nom affiché et code de langue (409 anglais, 40C français, C0A espagnol...).</summary>
        public static List<Tuple<string, string>> Lister()
        {
            var l = new List<Tuple<string, string>>();
            try
            {
                dynamic v = Objet(); if (v == null) return l;
                dynamic jetons = v.GetVoices();
                int n = jetons.Count;
                for (int i = 0; i < n; i++)
                {
                    dynamic j = jetons.Item(i);
                    string nom = j.GetDescription(); string langue = "";
                    try { langue = j.GetAttribute("Language"); } catch { }
                    l.Add(Tuple.Create(nom, (langue ?? "").ToUpperInvariant()));
                }
            }
            catch (Exception ex) { JournalDemarrage.Erreur("liste des voix", ex); }
            return l;
        }

        private static readonly Dictionary<string, string[]> CodesLangue = new Dictionary<string, string[]>
        {
            { "fr", new[] { "40C", "C0C", "80C", "100C" } }, { "en", new[] { "409", "809", "C09", "1009" } }, { "es", new[] { "C0A", "40A", "80A" } }
        };

        /// <summary>Lit un texte (en arrière-plan : rend la main tout de suite). Coupe la lecture précédente.</summary>
        public static bool Lire(string texte, string nomVoix)
        {
            try
            {
                dynamic v = Objet(); if (v == null) return false;
                dynamic jetons = v.GetVoices();
                int n = jetons.Count; dynamic choisi = null, parLangue = null;
                string[] codes; CodesLangue.TryGetValue(Langue.Code, out codes);
                for (int i = 0; i < n; i++)
                {
                    dynamic j = jetons.Item(i);
                    string nom = j.GetDescription();
                    if (!string.IsNullOrEmpty(nomVoix) && nom == nomVoix) { choisi = j; break; }
                    if (parLangue == null && codes != null)
                    {
                        string lg = ""; try { lg = ((string)j.GetAttribute("Language") ?? "").ToUpperInvariant(); } catch { }
                        if (lg.Split(';').Any(x => codes.Contains(x.Trim()))) parLangue = j;
                    }
                }
                if (choisi == null) choisi = parLangue;
                if (choisi != null) try { v.Voice = choisi; } catch (Exception ex) { JournalDemarrage.Erreur("choix de la voix", ex); }
                v.Rate = 0; v.Volume = 100;
                v.Speak(texte, 1 | 2);   // SVSFlagsAsync | SVSFPurgeBeforeSpeak
                debut = DateTime.UtcNow;
                return true;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("lecture vocale", ex); return false; }
        }

        public static void Arreter()
        {
            debut = DateTime.MinValue;
            try { dynamic v = sapi; if (v != null) v.Speak("", 1 | 2); } catch { }
        }

        // La voix peut mettre un instant à démarrer (premier usage) : la lecture compte comme « en cours » pendant ce délai.
        private static DateTime debut = DateTime.MinValue;

        public static bool EnCours
        {
            get
            {
                if (sapi == null) return false;
                if ((DateTime.UtcNow - debut).TotalMilliseconds < 2500) return true;
                try { dynamic v = sapi; return (int)v.Status.RunningState == 2; } catch { return false; }
            }
        }
    }
}
