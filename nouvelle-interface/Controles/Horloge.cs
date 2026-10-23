// Nettoyage PC 5 - horloge des animations : une seule minuterie pour toute la fenêtre.
// Elle s'arrête quand la fenêtre est réduite ou en arrière-plan (rien ne tourne pour rien),
// et quand plus aucune animation n'en a besoin.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    public static class Horloge
    {
        private static readonly List<Func<long, bool>> animations = new List<Func<long, bool>>();
        private static readonly List<Action<long>> ambiances = new List<Action<long>>();
        private static readonly Stopwatch montre = Stopwatch.StartNew();
        private static DispatcherTimer minuteur;
        private static bool enPause;

        public static long Maintenant { get { return montre.ElapsedMilliseconds; } }

        /// <summary>Animation ponctuelle : appelée à chaque image tant qu'elle renvoie true.</summary>
        public static void Lancer(Func<long, bool> pas)
        {
            if (!Habillage.Animations) { pas(long.MaxValue / 4); return; }
            animations.Add(pas);
            Demarrer();
        }

        /// <summary>Animation d'ambiance (anneau qui tourne, halo) : tourne tant que la fenêtre est au premier plan.</summary>
        public static void Ambiance(Action<long> pas)
        {
            ambiances.Add(pas);
            if (Habillage.Animations) Demarrer();
        }

        public static void RetirerAmbiance(Action<long> pas) { ambiances.Remove(pas); }

        /// <summary>Fenêtre réduite ou en arrière-plan : les ambiances s'arrêtent (les animations en cours se terminent).</summary>
        public static void Pause(bool pause)
        {
            enPause = pause;
            if (!pause) Demarrer();
        }

        private static void Demarrer()
        {
            if (minuteur == null)
            {
                minuteur = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
                minuteur.Tick += (s, e) => Tic();
            }
            if (!minuteur.IsEnabled) minuteur.Start();
        }

        private static void Tic()
        {
            long t = Maintenant;
            for (int i = animations.Count - 1; i >= 0; i--)
            {
                bool continuer;
                try { continuer = animations[i](t); } catch { continuer = false; }
                if (!continuer) animations.RemoveAt(i);
            }
            if (!enPause && Habillage.Animations)
                foreach (var a in ambiances.ToArray()) { try { a(t); } catch { } }
            if (animations.Count == 0 && (enPause || ambiances.Count == 0 || !Habillage.Animations)) minuteur.Stop();
        }
    }
}
