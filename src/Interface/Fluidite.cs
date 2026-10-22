// Nettoyage PC - fluidité de l'interface : animations douces (survols, sélection), défilement doux à la molette,
// affichage d'une page « d'un coup » (sans voir les morceaux se dessiner). Les animations suivent le réglage
// « Effets d'animation » de Windows (Accessibilité) : désactivé, tout change instantanément.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal interface IAnimation { bool Avancer(long maintenant); }

    internal static class Animations
    {
        [DllImport("user32.dll")] private static extern bool SystemParametersInfo(int action, int param, ref bool valeur, int ini);
        private const int SPI_GETCLIENTAREAANIMATION = 0x1042;

        private static readonly List<IAnimation> enCours = new List<IAnimation>();
        private static readonly Stopwatch horloge = Stopwatch.StartNew();
        private static Timer minuteur;
        private static bool? actives;

        public static long Maintenant { get { return horloge.ElapsedMilliseconds; } }

        /// <summary>Animations autorisées (réglage de Windows « Afficher les animations »).</summary>
        public static bool Actives
        {
            get
            {
                if (!actives.HasValue)
                {
                    bool v = true;
                    try { if (Natif.EstWindows) SystemParametersInfo(SPI_GETCLIENTAREAANIMATION, 0, ref v, 0); } catch { v = true; }
                    actives = v;
                    try { SystemEvents.UserPreferenceChanged += (s, e) => actives = null; } catch { }
                }
                return actives.Value;
            }
        }

        public static void Lancer(IAnimation a)
        {
            if (!enCours.Contains(a)) enCours.Add(a);
            if (minuteur == null) { minuteur = new Timer { Interval = 15 }; minuteur.Tick += Tic; }
            if (!minuteur.Enabled) minuteur.Start();
        }

        private static void Tic(object s, EventArgs e)
        {
            long t = Maintenant;
            for (int i = enCours.Count - 1; i >= 0; i--)
            {
                bool continuer;
                try { continuer = enCours[i].Avancer(t); } catch { continuer = false; }
                if (!continuer) enCours.RemoveAt(i);
            }
            if (enCours.Count == 0) minuteur.Stop();
        }

        /// <summary>Courbe « ralentie à l'arrivée » (0 à 1).</summary>
        public static float Douce(float p) { p = Math.Max(0f, Math.Min(1f, p)); float q = 1 - p; return 1 - q * q * q; }
    }

    /// <summary>Valeur de 0 à 1 qui glisse en douceur vers sa cible (survol, sélection) et redessine son contrôle.</summary>
    internal class Fondu : IAnimation
    {
        private readonly Control proprio;
        private float depart, cible;
        private long debut;
        public float Valeur;
        public int Duree = 150;

        public Fondu(Control proprio, float initiale = 0f) { this.proprio = proprio; Valeur = cible = initiale; }

        public void Vers(float v)
        {
            if (Math.Abs(v - cible) < 0.001f) return;
            cible = v;
            if (!Animations.Actives || !proprio.IsHandleCreated || !proprio.Visible) { Valeur = v; proprio.Invalidate(); return; }
            depart = Valeur; debut = Animations.Maintenant;
            Animations.Lancer(this);
        }

        /// <summary>Saute directement à la valeur (sans animation).</summary>
        public void Fixer(float v) { cible = Valeur = v; }

        public bool Avancer(long t)
        {
            if (proprio.IsDisposed) return false;
            float p = (t - debut) / (float)Duree;
            Valeur = depart + (cible - depart) * Animations.Douce(p);
            if (p >= 1f) Valeur = cible;
            proprio.Invalidate();
            return p < 1f;
        }
    }

    /// <summary>Animation d'un seul passage (0 à 1) : reflet sur un bouton, balayage lumineux. Valeur -1 quand elle est finie.</summary>
    internal class Impulsion : IAnimation
    {
        private readonly Control proprio;
        private long debut;
        public int Duree = 600;
        public float Valeur = -1;
        public bool Active { get { return Valeur >= 0; } }

        public Impulsion(Control proprio) { this.proprio = proprio; }

        public void Lancer()
        {
            if (!Animations.Actives || !proprio.IsHandleCreated || !proprio.Visible) return;
            debut = Animations.Maintenant; Valeur = 0;
            Animations.Lancer(this);
        }

        public bool Avancer(long t)
        {
            if (proprio.IsDisposed) return false;
            float p = (t - debut) / (float)Duree;
            Valeur = p >= 1f ? -1 : p;
            proprio.Invalidate();
            return p < 1f;
        }
    }

    /// <summary>Titre qui « se décode » en un instant (lettres aléatoires remplacées une à une par le vrai texte).</summary>
    internal class Decodage : IAnimation
    {
        private readonly Etiquette cible;
        private string texte = "";
        private long debut;
        private const int Duree = 320;
        private static readonly Random hasard = new Random();
        private const string Signes = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

        public Decodage(Etiquette cible) { this.cible = cible; }

        public void Montrer(string nouveau)
        {
            texte = Langue.T(nouveau ?? "");
            if (!Animations.Actives || !cible.IsHandleCreated || !cible.Visible || texte.Length == 0) { cible.TexteBrut(texte); return; }
            debut = Animations.Maintenant;
            Animations.Lancer(this);
        }

        public bool Avancer(long t)
        {
            if (cible.IsDisposed) return false;
            float p = Math.Min(1f, (t - debut) / (float)Duree);
            int reveles = (int)Math.Round(texte.Length * Animations.Douce(p));
            if (p >= 1f || reveles >= texte.Length) { cible.TexteBrut(texte); return false; }
            var sb = new System.Text.StringBuilder(texte.Length);
            for (int i = 0; i < texte.Length; i++)
                sb.Append(i < reveles || char.IsWhiteSpace(texte[i]) ? texte[i] : Signes[hasard.Next(Signes.Length)]);
            cible.TexteBrut(sb.ToString());
            return true;
        }
    }

    /// <summary>Défilement doux d'une zone qui défile (molette de la souris).</summary>
    internal class DefilementDoux : IAnimation
    {
        private readonly ScrollableControl zone;
        private int depart, cible;
        private long debut;
        private bool enCours;
        private const int Duree = 170;

        public DefilementDoux(ScrollableControl zone) { this.zone = zone; }

        /// <summary>true si la molette a été prise en charge.</summary>
        public bool Molette(int delta)
        {
            if (!Animations.Actives || !zone.VerticalScroll.Visible) return false;
            int max = Math.Max(0, zone.DisplayRectangle.Height - zone.ClientSize.Height);
            int actuel = -zone.AutoScrollPosition.Y;
            int lignes = SystemInformation.MouseWheelScrollLines;
            int pas = lignes < 0 ? zone.ClientSize.Height - 40 : Math.Max(1, lignes) * 36;   // 3 lignes : environ 110 pixels par cran
            int base_ = enCours ? cible : actuel;
            int nouvelle = Math.Max(0, Math.Min(max, base_ - delta * pas / 120));
            if (nouvelle == actuel && !enCours) return true;
            cible = nouvelle; depart = actuel; debut = Animations.Maintenant; enCours = true;
            Animations.Lancer(this);
            return true;
        }

        public void Arreter() { enCours = false; }

        public bool Avancer(long t)
        {
            if (!enCours || zone.IsDisposed || !zone.Visible) { enCours = false; return false; }
            float p = (t - debut) / (float)Duree;
            int y = p >= 1f ? cible : (int)Math.Round(depart + (cible - depart) * Animations.Douce(p));
            zone.AutoScrollPosition = new System.Drawing.Point(zone.HorizontalScroll.Value, y);
            if (p >= 1f) enCours = false;
            return enCours;
        }
    }

    /// <summary>Gèle le dessin d'un contrôle pendant qu'on le transforme, puis l'affiche d'un coup.</summary>
    internal static class Dessin
    {
        [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr h, IntPtr zone, IntPtr region, uint options);
        private const int WM_SETREDRAW = 0x000B;
        private const uint RDW_INVALIDATE = 0x0001, RDW_ERASE = 0x0004, RDW_ALLCHILDREN = 0x0080, RDW_UPDATENOW = 0x0100, RDW_FRAME = 0x0400;

        public static IDisposable Geler(params Control[] controles) { return new Gel(controles); }

        private sealed class Gel : IDisposable
        {
            private readonly List<Control> geles = new List<Control>();
            public Gel(Control[] controles)
            {
                if (!Natif.EstWindows) return;
                foreach (var c in controles)
                {
                    if (c == null || !c.IsHandleCreated || !c.Visible) continue;
                    var f = c.FindForm();
                    if (f == null || !f.Visible || f.WindowState == FormWindowState.Minimized) continue;
                    try { Natif.SendMessage(c.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero); geles.Add(c); } catch { }
                }
            }
            public void Dispose()
            {
                foreach (var c in geles)
                {
                    try
                    {
                        if (c.IsDisposed) continue;
                        Natif.SendMessage(c.Handle, WM_SETREDRAW, (IntPtr)1, IntPtr.Zero);
                        RedrawWindow(c.Handle, IntPtr.Zero, IntPtr.Zero, RDW_INVALIDATE | RDW_ERASE | RDW_FRAME | RDW_ALLCHILDREN | RDW_UPDATENOW);
                    }
                    catch { }
                }
                geles.Clear();
            }
        }
    }
}

namespace NettoyagePC
{
    using System.Drawing;

    /// <summary>Bandeau en haut de la fenêtre, sur toutes les pages : « Nouvelle version disponible », avec
    /// « Installer maintenant », « Voir les nouveautés » et « Plus tard » (il revient à l'ouverture suivante).</summary>
    internal class BandeauMiseAJour : Surface
    {
        private readonly Carte carte = new Carte(600, 64, true) { Dock = DockStyle.Fill, Margin = new Padding(0) };
        private readonly Etiquette titre, detail;
        public readonly Bouton BInstaller, BNouveautes;
        private readonly Lien lPlusTard;
        public string VersionCachee = "";

        public BandeauMiseAJour() : base(Role.Fond)
        {
            Dock = DockStyle.Top; Height = 84; Padding = new Padding(28, 10, 28, 10); Visible = false;
            titre = new Etiquette("", Theme.CarteTitre, Role.AccentVif, 18, 11);
            detail = new Etiquette("", Theme.Normal, Role.Discret, 18, 34) { AutoSize = false, AutoEllipsis = true, Size = new Size(300, 20) };
            BInstaller = new Bouton("Installer maintenant", true, 190, 36) { Font = Theme.CarteTitre };
            BNouveautes = new Bouton("Voir les nouveautés", false, 170, 36);
            lPlusTard = new Lien("Plus tard", 0, 0);
            lPlusTard.LinkClicked += (s, e) => Cacher(true);
            Infobulle.Definir(lPlusTard, "Le bandeau revient à la prochaine ouverture de Nettoyage PC.");
            carte.Controls.AddRange(new Control[] { titre, detail, BInstaller, BNouveautes, lPlusTard });
            Controls.Add(carte);
            carte.Resize += (s, e) => Placer();
        }

        private void Placer()
        {
            int w = carte.ClientSize.Width, h = carte.ClientSize.Height;
            lPlusTard.Location = new Point(w - lPlusTard.Width - 18, (h - lPlusTard.Height) / 2);
            BInstaller.Location = new Point(lPlusTard.Left - 16 - BInstaller.Width, (h - BInstaller.Height) / 2);
            BNouveautes.Location = new Point(BInstaller.Left - 10 - BNouveautes.Width, (h - BNouveautes.Height) / 2);
            BNouveautes.Visible = BNouveautes.Left > 340;
            detail.Width = Math.Max(120, (BNouveautes.Visible ? BNouveautes.Left : BInstaller.Left) - 30);
        }

        /// <summary>Affiche le bandeau pour cette version (sauf si l'utilisateur a dit « Plus tard » pour elle pendant cette session).</summary>
        public void Montrer(string version, string resume)
        {
            if (version == VersionCachee) return;
            this.version = version;
            titre.Text = Langue.F("Nettoyage PC {0} est disponible", version);
            detail.Text = resume;
            BInstaller.Enabled = BNouveautes.Enabled = true;
            BInstaller.Text = "Installer maintenant";
            if (!Visible) using (Dessin.Geler(Parent)) { Visible = true; Placer(); }
        }

        public void Installation(string texte) { titre.Text = texte; BInstaller.Enabled = BNouveautes.Enabled = false; }

        private string version = "";
        public void Cacher(bool plusTard)
        {
            if (plusTard) VersionCachee = version;
            if (Visible) using (Dessin.Geler(Parent)) Visible = false;
        }
    }
}
