// Velox 5 - icônes du style arrondi (menu, en-têtes) : tracés vectoriels simples sur une grille de 24, dessinés au
// trait. Pas d'emoji : le même rendu sur tous les PC, dans les deux thèmes.
using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;

namespace NettoyagePC.Nouveau
{
    internal static class Icones5
    {
        private static readonly Dictionary<string, string> traces = new Dictionary<string, string>
        {
            { "Accueil", "M3.5 11 L12 4 L20.5 11 M5.5 9.5 V20 H18.5 V9.5 M10 20 V14.5 H14 V20" },
            { "Nettoyer", "M12 3 L13.9 9.1 L20 11 L13.9 12.9 L12 19 L10.1 12.9 L4 11 L10.1 9.1 Z M19 3.5 V7.5 M17 5.5 H21" },
            { "Accelerer", "M13.5 2.5 L4.5 13.5 H11 L10 21.5 L19.5 9.5 H13 Z" },
            { "Proteger", "M12 3 L19.5 6 V11 C19.5 15.8 16.3 19.2 12 21 C7.7 19.2 4.5 15.8 4.5 11 V6 Z M9 12 L11.2 14.2 L15.2 10" },
            { "Outils", "M3.5 9 H20.5 V19.5 H3.5 Z M8.5 9 V6 C8.5 5.2 9.2 4.5 10 4.5 H14 C14.8 4.5 15.5 5.2 15.5 6 V9 M3.5 13.5 H20.5 M10.5 12 V15 M13.5 12 V15" },
            { "Premium", "M12 3 L14.6 8.6 L20.6 9.3 L16.1 13.4 L17.4 19.4 L12 16.4 L6.6 19.4 L7.9 13.4 L3.4 9.3 L9.4 8.6 Z" },
            { "Audio", "M4.5 15.5 V12 C4.5 7.9 7.9 4.5 12 4.5 C16.1 4.5 19.5 7.9 19.5 12 V15.5 M4.5 14 H7.5 V20 H5.5 C5 20 4.5 19.5 4.5 19 Z M19.5 14 H16.5 V20 H18.5 C19 20 19.5 19.5 19.5 19 Z" },
            { "MisesAJour", "M19.5 12 C19.5 16.1 16.1 19.5 12 19.5 C9.2 19.5 6.8 18 5.5 15.7 M4.5 12 C4.5 7.9 7.9 4.5 12 4.5 C14.8 4.5 17.2 6 18.5 8.3 M18.8 4.5 V8.6 H14.7 M5.2 19.5 V15.4 H9.3" },
            { "Historique", "M12 3.5 C16.7 3.5 20.5 7.3 20.5 12 C20.5 16.7 16.7 20.5 12 20.5 C7.3 20.5 3.5 16.7 3.5 12 C3.5 7.3 7.3 3.5 12 3.5 Z M12 7.5 V12 L15 14" },
            { "Reglages", "M12 9 C13.7 9 15 10.3 15 12 C15 13.7 13.7 15 12 15 C10.3 15 9 13.7 9 12 C9 10.3 10.3 9 12 9 Z M12 2.5 V5.5 M12 18.5 V21.5 M2.5 12 H5.5 M18.5 12 H21.5 M5.3 5.3 L7.4 7.4 M16.6 16.6 L18.7 18.7 M5.3 18.7 L7.4 16.6 M16.6 7.4 L18.7 5.3" },
            // Studio son (cartes du mode Simple)
            { "Curseurs", "M5 20.5 V13.5 M5 9.5 V3.5 M12 20.5 V11.5 M12 7.5 V3.5 M19 20.5 V15.5 M19 11.5 V3.5 M2.5 13.5 H7.5 M9.5 7.5 H14.5 M16.5 15.5 H21.5" },
            { "Coche", "M5 12.5 L10 17.5 L19 7" },
            { "Alerte", "M12 3.5 L21 19.5 H3 Z M12 10 V14 M12 16.8 V17" },
        };

        private static readonly Dictionary<string, Geometry> geometries = new Dictionary<string, Geometry>();

        public static bool Existe(string cle) { return cle != null && traces.ContainsKey(cle); }

        /// <summary>Dessine l'icône « cle » dans le carré r (rien si elle n'existe pas).</summary>
        public static void Dessiner(DrawingContext ctx, string cle, Rect r, Color couleur, double epaisseur = 1.7)
        {
            Geometry g;
            if (cle == null || !traces.ContainsKey(cle)) return;
            lock (geometries) { if (!geometries.TryGetValue(cle, out g)) { g = Geometry.Parse(traces[cle]); geometries[cle] = g; } }
            double k = Math.Min(r.Width, r.Height) / 24.0;
            using (ctx.PushTransform(Matrix.CreateScale(k, k) * Matrix.CreateTranslation(r.X + (r.Width - 24 * k) / 2, r.Y + (r.Height - 24 * k) / 2)))
                ctx.DrawGeometry(null, new Pen(Habillage.Pinceau(couleur), epaisseur / Math.Max(0.1, k) * 1.0, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), g);
        }
    }

    /// <summary>Contrôle qui affiche une icône (en-têtes, cartes).</summary>
    public sealed class Icone5 : Avalonia.Controls.Control
    {
        public string Cle; public Color? Couleur; public double Epaisseur = 1.7;
        public override void Render(DrawingContext ctx) { Icones5.Dessiner(ctx, Cle, new Rect(Bounds.Size), Couleur ?? Habillage.P.Texte, Epaisseur); }
    }
}
