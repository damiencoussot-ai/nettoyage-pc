// Nettoyage PC - thème : palettes clair / sombre « futur », polices, outils de dessin
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal interface IThemable { void AppliquerTheme(); }

    internal enum Role { Fond, Carte, Barre, Texte, Discret, Accent, AccentVif, Vert, Orange, Rouge, BarreTexte, Blanc, Transparent, Console, EnTete, Piste, Survol }

    internal class Palette
    {
        public Color Fond, Carte, Bordure, Survol, Barre, BarreSurvol, BarreSel, BarreTexte, Accent, AccentFonce, AccentVif;
        public Color Texte, Discret, Piste, EnTete, Vert, Orange, Rouge, Console, Lueur1, Lueur2;

        public Color Couleur(Role r)
        {
            switch (r)
            {
                case Role.Fond: return Fond; case Role.Carte: return Carte; case Role.Barre: return Barre; case Role.Texte: return Texte;
                case Role.Discret: return Discret; case Role.Accent: return Accent; case Role.AccentVif: return AccentVif; case Role.Vert: return Vert;
                case Role.Orange: return Orange; case Role.Rouge: return Rouge; case Role.BarreTexte: return BarreTexte; case Role.Blanc: return Color.White;
                case Role.Console: return Console; case Role.EnTete: return EnTete; case Role.Piste: return Piste; case Role.Survol: return Survol;
                default: return Color.Transparent;
            }
        }
    }

    /// <summary>Couleur d'accent de l'interface (lueurs des boutons, des cartes et de la barre latérale).</summary>
    internal class Accent
    {
        public string Cle, Nom;
        public bool Premium;
        public Color L1Sombre, L2Sombre, AccentSombre, VifSombre, L1Clair, L2Clair, AccentClair, VifClair;
    }

    internal static class Theme
    {
        private static Color C(int r, int g, int b) { return Color.FromArgb(r, g, b); }

        // « Aurore » (cyan et violet) est la couleur d'origine ; les autres font partie de Premium
        public static readonly List<Accent> Accents = new List<Accent>
        {
            new Accent { Cle = "aurore", Nom = "Noyau", L1Sombre = C(61, 245, 255), L2Sombre = C(48, 186, 255), AccentSombre = C(61, 245, 255), VifSombre = C(61, 245, 255), L1Clair = C(0, 122, 158), L2Clair = C(0, 98, 168), AccentClair = C(0, 112, 150), VifClair = C(0, 122, 168) },
            new Accent { Cle = "ocean", Nom = "Océan", Premium = true, L1Sombre = C(56, 189, 248), L2Sombre = C(37, 99, 235), AccentSombre = C(59, 130, 246), VifSombre = C(56, 189, 248), L1Clair = C(14, 165, 233), L2Clair = C(37, 99, 235), AccentClair = C(29, 78, 216), VifClair = C(37, 99, 235) },
            new Accent { Cle = "emeraude", Nom = "Émeraude", Premium = true, L1Sombre = C(52, 211, 153), L2Sombre = C(13, 148, 136), AccentSombre = C(16, 185, 129), VifSombre = C(52, 211, 153), L1Clair = C(16, 185, 129), L2Clair = C(13, 148, 136), AccentClair = C(5, 150, 105), VifClair = C(4, 120, 87) },
            new Accent { Cle = "amethyste", Nom = "Améthyste", Premium = true, L1Sombre = C(192, 132, 252), L2Sombre = C(124, 58, 237), AccentSombre = C(147, 51, 234), VifSombre = C(192, 132, 252), L1Clair = C(168, 85, 247), L2Clair = C(109, 40, 217), AccentClair = C(126, 34, 206), VifClair = C(126, 34, 206) },
            new Accent { Cle = "soleil", Nom = "Coucher de soleil", Premium = true, L1Sombre = C(251, 146, 60), L2Sombre = C(236, 72, 153), AccentSombre = C(244, 114, 182), VifSombre = C(253, 186, 116), L1Clair = C(249, 115, 22), L2Clair = C(219, 39, 119), AccentClair = C(219, 39, 119), VifClair = C(194, 65, 12) },
            new Accent { Cle = "rubis", Nom = "Rubis", Premium = true, L1Sombre = C(251, 113, 133), L2Sombre = C(190, 18, 60), AccentSombre = C(225, 29, 72), VifSombre = C(253, 164, 175), L1Clair = C(244, 63, 94), L2Clair = C(159, 18, 57), AccentClair = C(190, 18, 60), VifClair = C(190, 18, 60) }
        };
        public static string AccentActuel { get; private set; } = "aurore";

        /// <summary>Applique une couleur d'accent aux deux palettes (clair et sombre). Il faut ensuite redessiner les fenêtres.</summary>
        public static void ChoisirAccent(string cle)
        {
            var a = Accents.FirstOrDefault(x => x.Cle == cle) ?? Accents[0];
            AccentActuel = a.Cle;
            Sombre.Lueur1 = a.L1Sombre; Sombre.Lueur2 = a.L2Sombre; Sombre.Accent = a.AccentSombre; Sombre.AccentVif = a.VifSombre; Sombre.AccentFonce = Melanger(a.AccentSombre, Color.Black, 0.2);
            Clair.Lueur1 = a.L1Clair; Clair.Lueur2 = a.L2Clair; Clair.Accent = a.AccentClair; Clair.AccentVif = a.VifClair; Clair.AccentFonce = Melanger(a.AccentClair, Color.Black, 0.2);
        }

        // Style « Noyau » (4.5) : interface HUD, lignes fines, coins presque droits, une seule couleur d'accent
        public static readonly Palette Clair = new Palette
        {
            Fond = C(238, 243, 247), Carte = C(250, 252, 254), Bordure = C(200, 214, 224), Survol = C(226, 238, 245),
            Barre = C(7, 12, 22), BarreSurvol = C(13, 26, 38), BarreSel = C(14, 40, 52), BarreTexte = C(185, 202, 218),
            Accent = C(0, 112, 150), AccentFonce = C(0, 90, 122), AccentVif = C(0, 122, 168),
            Texte = C(14, 24, 36), Discret = C(78, 96, 114), Piste = C(220, 230, 238), EnTete = C(244, 248, 251),
            Vert = C(0, 135, 80), Orange = C(176, 98, 0), Rouge = C(196, 40, 50), Console = C(6, 10, 18),
            Lueur1 = C(0, 122, 158), Lueur2 = C(0, 98, 168)
        };

        // Thème sombre « Noyau » : nuit profonde, traits cyan, alertes ambre
        public static readonly Palette Sombre = new Palette
        {
            Fond = C(6, 10, 18), Carte = C(10, 18, 32), Bordure = C(20, 46, 58), Survol = C(14, 30, 44),
            Barre = C(7, 12, 22), BarreSurvol = C(12, 26, 38), BarreSel = C(14, 40, 52), BarreTexte = C(185, 202, 218),
            Accent = C(61, 245, 255), AccentFonce = C(30, 160, 190), AccentVif = C(61, 245, 255),
            Texte = C(221, 235, 245), Discret = C(138, 160, 180), Piste = C(16, 32, 46), EnTete = C(9, 16, 28),
            Vert = C(108, 240, 176), Orange = C(255, 181, 71), Rouge = C(255, 107, 122), Console = C(4, 8, 14),
            Lueur1 = C(61, 245, 255), Lueur2 = C(48, 186, 255)
        };

        /// <summary>Rayon maximal des coins (style Noyau : presque droits). Les pastilles rondes passent par CheminRond.</summary>
        public const float RayonMax = 3.5f;

        /// <summary>Texte lisible (sombre ou blanc) sur une couleur d'accent.</summary>
        public static Color TexteSur(Color fond)
        {
            double l = (0.2126 * fond.R + 0.7152 * fond.G + 0.0722 * fond.B) / 255.0;
            return l > 0.45 ? C(4, 18, 26) : Color.White;
        }

        /// <summary>Coins lumineux du style Noyau (équerres de 14 px aux quatre coins).</summary>
        public static void Crochets(Graphics g, Color c, float x, float y, float l, float h, float taille = 12, float epaisseur = 2)
        {
            using (var p = new Pen(c, epaisseur) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
            {
                float e = epaisseur / 2;
                g.DrawLines(p, new[] { new PointF(x + e, y + taille), new PointF(x + e, y + e), new PointF(x + taille, y + e) });
                g.DrawLines(p, new[] { new PointF(x + l - taille, y + e), new PointF(x + l - e, y + e), new PointF(x + l - e, y + taille) });
                g.DrawLines(p, new[] { new PointF(x + e, y + h - taille), new PointF(x + e, y + h - e), new PointF(x + taille, y + h - e) });
                g.DrawLines(p, new[] { new PointF(x + l - taille, y + h - e), new PointF(x + l - e, y + h - e), new PointF(x + l - e, y + h - taille) });
            }
        }

        public static Palette P = Sombre;
        public static bool EstSombre { get { return P == Sombre; } }
        public static event Action Change;

        public static void Choisir(bool sombre)
        {
            P = sombre ? Sombre : Clair;
            Natif.ModeApplication(sombre);
            var c = Change; if (c != null) c();
        }

        /// <summary>Réglage clair / sombre de Windows (thème « automatique »).</summary>
        public static bool WindowsEnSombre()
        {
            object v = Registre.Lire(@"HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme");
            return v is int && (int)v == 0;
        }

        /// <summary>Recolore une fenêtre et tous ses contrôles.</summary>
        public static void Appliquer(Control racine)
        {
            if (racine == null) return;
            racine.SuspendLayout();
            AppliquerRec(racine);
            racine.ResumeLayout();
            var f = racine as Form;
            if (f != null)
            {
                f.BackColor = P.Fond;
                if (f.IsHandleCreated) Natif.BarreTitreSombre(f.Handle, EstSombre);
            }
            racine.Invalidate(true);
        }

        private static void AppliquerRec(Control c)
        {
            var t = c as IThemable;
            if (t != null) { try { t.AppliquerTheme(); } catch { } }
            foreach (Control e in c.Controls) AppliquerRec(e);
        }

        // ---------------- Polices
        private static Font F(string nom, float taille, FontStyle style = FontStyle.Regular) { return new Font(nom, taille, style); }
        /// <summary>Première police installée de la liste (Bahnschrift et Cascadia Mono sont fournies avec Windows 10 et 11).</summary>
        private static Font Premiere(float taille, params string[] noms)
        {
            foreach (var n in noms)
            {
                try { var f = new Font(n, taille); if (f.Name.Equals(n, StringComparison.OrdinalIgnoreCase)) return f; f.Dispose(); } catch { }
            }
            return new Font(noms[noms.Length - 1], taille);
        }
        public static readonly Font TitreApp = Premiere(13.5f, "Bahnschrift SemiBold", "Segoe UI Semibold");
        public static readonly Font TitrePage = Premiere(17, "Bahnschrift SemiBold", "Segoe UI Semibold");
        public static readonly Font Grand = Premiere(18, "Bahnschrift SemiBold", "Segoe UI Semibold");
        public static readonly Font CarteTitre = Premiere(11, "Bahnschrift SemiBold", "Segoe UI Semibold");
        public static readonly Font Normal = F("Segoe UI", 9.5f);
        public static readonly Font Petit = F("Segoe UI", 8.5f);
        public static readonly Font PetitGras = F("Segoe UI Semibold", 9);
        public static readonly Font Nav = Premiere(10.5f, "Bahnschrift", "Segoe UI");
        public static readonly Font NavActif = Premiere(10.5f, "Bahnschrift SemiBold", "Segoe UI Semibold");
        public static readonly Font Score = Premiere(50, "Bahnschrift SemiBold", "Segoe UI Light");
        public static readonly Font Console = Premiere(9.5f, "Cascadia Mono", "Consolas");
        /// <summary>Petites étiquettes techniques (majuscules) du style Noyau.</summary>
        public static readonly Font Mono = Premiere(8, "Cascadia Mono", "Consolas");
        public static readonly Font MonoGras = Premiere(8.5f, "Cascadia Mono SemiBold", "Cascadia Mono", "Consolas");
        public static readonly Font Icones, IconesGrand, IconesTresGrand;

        static Theme()
        {
            // Icônes : Segoe Fluent Icons (Windows 11) ou Segoe MDL2 Assets (Windows 10)
            foreach (var nom in new[] { "Segoe Fluent Icons", "Segoe MDL2 Assets" })
            {
                try
                {
                    var essai = new Font(nom, 12);
                    if (essai.Name == nom) { Icones = essai; IconesGrand = new Font(nom, 13); IconesTresGrand = new Font(nom, 20); break; }
                    essai.Dispose();
                }
                catch { }
            }
        }

        // ---------------- Dessin
        public static Color Melanger(Color a, Color b, double t, int alpha = 255)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb(alpha, (int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static GraphicsPath CheminArrondi(float x, float y, float l, float h, float r) { return CheminRond(x, y, l, h, Math.Min(r, RayonMax)); }

        /// <summary>Rectangle aux coins arrondis sans limite de rayon (pastilles, interrupteurs).</summary>
        public static GraphicsPath CheminRond(float x, float y, float l, float h, float r)
        {
            var ch = new GraphicsPath();
            r = Math.Max(0.5f, Math.Min(r, Math.Min(l, h) / 2));
            float d = 2 * r;
            ch.AddArc(x, y, d, d, 180, 90);
            ch.AddArc(x + l - d, y, d, d, 270, 90);
            ch.AddArc(x + l - d, y + h - d, d, d, 0, 90);
            ch.AddArc(x, y + h - d, d, d, 90, 90);
            ch.CloseFigure();
            return ch;
        }

        public static void RemplirArrondi(Graphics g, Color c, float x, float y, float l, float h, float r)
        {
            if (l <= 0 || h <= 0) return;
            using (var ch = CheminArrondi(x, y, l, h, r)) using (var b = new SolidBrush(c)) g.FillPath(b, ch);
        }

        public static void RemplirDegrade(Graphics g, Color c1, Color c2, float x, float y, float l, float h, float r, bool vertical = false)
        {
            if (l <= 0.5f || h <= 0.5f) return;
            using (var ch = CheminArrondi(x, y, l, h, r))
            using (var b = new LinearGradientBrush(new RectangleF(x - 1, y - 1, l + 2, h + 2), c1, c2, vertical ? LinearGradientMode.Vertical : LinearGradientMode.Horizontal))
                g.FillPath(b, ch);
        }

        public static void Contour(Graphics g, Color c, float x, float y, float l, float h, float r, float epaisseur = 1)
        {
            using (var ch = CheminArrondi(x, y, l, h, r)) using (var s = new Pen(c, epaisseur)) g.DrawPath(s, ch);
        }

        public static void ContourLumineux(Graphics g, float x, float y, float l, float h, float r, float epaisseur = 1.2f)
        {
            using (var ch = CheminArrondi(x, y, l, h, r))
            using (var b = new LinearGradientBrush(new RectangleF(x, y, Math.Max(1, l), Math.Max(1, h)), P.Lueur1, P.Lueur2, LinearGradientMode.ForwardDiagonal))
            using (var s = new Pen(b, epaisseur)) g.DrawPath(s, ch);
        }

        public static void Texte(Graphics g, string texte, Font police, Rectangle r, Color c, TextFormatFlags f)
        {
            TextRenderer.DrawText(g, Langue.T(texte), police, r, c, f | TextFormatFlags.NoPrefix);
        }

        public static void Icone(Graphics g, int glyphe, Font police, Rectangle r, Color c)
        {
            if (police == null) return;
            TextRenderer.DrawText(g, ((char)glyphe).ToString(), police, r, c, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }

        public static Color Niveau(double valeur, double orange, double rouge)
        {
            if (valeur >= rouge) return P.Rouge;
            if (valeur >= orange) return P.Orange;
            return P.Vert;
        }

        /// <summary>Peint sous un contrôle « transparent » le fond de son parent, sans redessiner tout le parent
        /// (beaucoup plus rapide quand une carte contient des libellés et des boutons). false : cas non géré.</summary>
        public static bool PeindreFondRapide(Control c, Graphics g)
        {
            var p = c.Parent;
            if (p == null) return false;
            var carte = p as Carte;
            if (carte != null) { carte.PeindreFondSous(g, c.Bounds); return true; }
            if ((p is Surface || p is Grille) && p.BackColor.A == 255) { g.Clear(p.BackColor); return true; }
            return false;
        }

        /// <summary>Couleur de fond réellement visible derrière un contrôle (parents transparents ignorés).</summary>
        public static Color FondDerriere(Control c)
        {
            var p = c.Parent;
            while (p != null && p.BackColor.A < 255) p = p.Parent;
            return p == null ? P.Fond : p.BackColor;
        }
    }
}
