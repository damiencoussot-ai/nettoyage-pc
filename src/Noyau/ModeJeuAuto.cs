// Nettoyage PC - mode jeu automatique (Premium, par le gardien) : quand un jeu se lance, le PC passe en mode
// d'alimentation « Performances élevées » (et OneDrive se met en pause si tu l'as choisi) ; quand le jeu se ferme,
// tout revient comme avant. Rien n'est fermé d'autre, rien n'est désinstallé. Sans interface : partagé v4 / v5.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace NettoyagePC
{
    internal class SessionJeu
    {
        public string Jeu = "";
        public DateTime Debut, Fin;
        public bool Performances, OneDrive;
        public TimeSpan Duree { get { return Fin > Debut ? Fin - Debut : TimeSpan.Zero; } }
    }

    internal static class ModeJeuAuto
    {
        private static string FichierEtat { get { return Path.Combine(Chemins.Local, "modejeu-auto.json"); } }
        private static string FichierSessions { get { return Path.Combine(Chemins.Local, "modejeu-sessions.json"); } }

        private static HashSet<string> exesJeux;
        private static DateTime exesLus = DateTime.MinValue;

        /// <summary>Jeu ouvert en ce moment (titre de sa fenêtre), ou null : programme d'un jeu détecté (Steam, Epic...).</summary>
        public static string JeuEnCours()
        {
            if (exesJeux == null || (DateTime.Now - exesLus).TotalHours >= 1)
            {
                try { exesJeux = new HashSet<string>(DetectionJeux.Lister().Where(j => j.Exe != "").Select(j => Path.GetFileName(j.Exe)), StringComparer.OrdinalIgnoreCase); }
                catch { exesJeux = new HashSet<string>(StringComparer.OrdinalIgnoreCase); }
                exesLus = DateTime.Now;
            }
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.MainWindowHandle == IntPtr.Zero) continue;
                    string exe = p.ProcessName + ".exe";
                    // les lanceurs eux-mêmes ne sont pas des jeux
                    if (Regex.IsMatch(p.ProcessName, @"^(steam|steamwebhelper|EpicGamesLauncher|EADesktop|Origin|UbisoftConnect|upc|Battle\.net|RiotClientServices|GalaxyClient|XboxPcApp)$", RegexOptions.IgnoreCase)) continue;
                    bool jeu = exesJeux.Contains(exe);
                    if (!jeu)
                    {
                        string chemin = ""; try { chemin = p.MainModule.FileName; } catch { }
                        jeu = Regex.IsMatch(chemin, @"steamapps\\common|\\Epic Games\\|\\XboxGames\\|\\Riot Games\\|Ubisoft Game Launcher\\games|\\EA Games\\|GOG Galaxy\\Games", RegexOptions.IgnoreCase);
                    }
                    if (jeu) { string t = (p.MainWindowTitle ?? "").Trim(); return t != "" ? t : p.ProcessName; }
                }
                catch { }
                finally { p.Dispose(); }
            }
            return null;
        }

        public static bool Actif { get { return File.Exists(FichierEtat); } }

        /// <summary>Jeu pour lequel le mode jeu est actif ("" si inactif).</summary>
        public static string JeuActif()
        {
            try { return Json.Texte(Json.LireFichier(FichierEtat) as Dictionary<string, object>, "Jeu"); } catch { return ""; }
        }

        /// <summary>Active le mode jeu. Retourne ce qui a été fait (pour la notification).</summary>
        public static string Demarrer(string jeu, bool pauseOneDrive)
        {
            if (Actif) return "";
            var faits = new List<string>();
            string avant = "", oneDrive = "";
            try
            {
                avant = Alimentation.PlanActif();
                if (avant != "" && avant != Alimentation.Performances && Alimentation.Liste().Contains(Alimentation.Performances) && Alimentation.Activer(Alimentation.Performances))
                    faits.Add(Langue.T("mode « Performances élevées »"));
                else avant = "";
            }
            catch { avant = ""; }
            if (pauseOneDrive)
            {
                try
                {
                    var od = Process.GetProcessesByName("OneDrive").FirstOrDefault();
                    if (od != null)
                    {
                        string chemin = ""; try { chemin = od.MainModule.FileName; } catch { }
                        if (chemin != "" && File.Exists(chemin))
                        {
                            Programmes.Executer(chemin, "/shutdown", 20000);
                            oneDrive = chemin;
                            faits.Add(Langue.T("OneDrive en pause"));
                        }
                    }
                }
                catch { }
            }
            try
            {
                Directory.CreateDirectory(Chemins.Local);
                File.WriteAllText(FichierEtat, Json.Ecrire(new Dictionary<string, object> { { "Jeu", jeu ?? "" }, { "Debut", DateTime.Now.ToString("o") }, { "PlanAvant", avant }, { "OneDrive", oneDrive } }));
            }
            catch { }
            return string.Join(", ", faits);
        }

        /// <summary>Remet tout comme avant le jeu et note la session. Retourne la session (null si le mode jeu n'était pas actif).</summary>
        public static SessionJeu Terminer()
        {
            if (!Actif) return null;
            Dictionary<string, object> e = null;
            try { e = Json.LireFichier(FichierEtat) as Dictionary<string, object>; } catch { }
            try { File.Delete(FichierEtat); } catch { }
            if (e == null) return null;
            var s = new SessionJeu { Jeu = Json.Texte(e, "Jeu"), Fin = DateTime.Now };
            DateTime d; if (DateTime.TryParse(Json.Texte(e, "Debut"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d)) s.Debut = d; else s.Debut = s.Fin;
            string avant = Json.Texte(e, "PlanAvant");
            if (Regex.IsMatch(avant, @"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$"))
            {
                // seulement si personne n'a changé le mode entre-temps
                try { if (Alimentation.PlanActif() == Alimentation.Performances) { Alimentation.Activer(avant); s.Performances = true; } } catch { }
            }
            string od = Json.Texte(e, "OneDrive");
            if (od != "" && File.Exists(od) && Path.GetFileName(od).Equals("OneDrive.exe", StringComparison.OrdinalIgnoreCase))
            {
                try { if (Process.GetProcessesByName("OneDrive").Length == 0) { Process.Start(new ProcessStartInfo(od, "/background") { UseShellExecute = false }); s.OneDrive = true; } } catch { }
            }
            try
            {
                var l = Sessions(); l.Insert(0, s);
                File.WriteAllText(FichierSessions, Json.Ecrire(l.Take(40).Select(x => (object)new Dictionary<string, object>
                    { { "Jeu", x.Jeu }, { "Debut", x.Debut.ToString("o") }, { "Fin", x.Fin.ToString("o") }, { "Performances", x.Performances }, { "OneDrive", x.OneDrive } }).ToList()));
            }
            catch { }
            return s;
        }

        /// <summary>Dernières parties (la plus récente d'abord).</summary>
        public static List<SessionJeu> Sessions()
        {
            var l = new List<SessionJeu>();
            try
            {
                foreach (var o in Json.Objets(Json.LireFichier(FichierSessions)))
                {
                    DateTime d, f;
                    DateTime.TryParse(Json.Texte(o, "Debut"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out d);
                    DateTime.TryParse(Json.Texte(o, "Fin"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out f);
                    l.Add(new SessionJeu { Jeu = Json.Texte(o, "Jeu"), Debut = d, Fin = f, Performances = Json.Bool(o, "Performances"), OneDrive = Json.Bool(o, "OneDrive") });
                }
            }
            catch { }
            return l;
        }

        public static string TexteDuree(TimeSpan t)
        {
            return t.TotalHours >= 1 ? Langue.F("{0} h {1:00}", (int)t.TotalHours, t.Minutes) : Langue.F("{0} min", Math.Max(1, (int)t.TotalMinutes));
        }
    }
}
