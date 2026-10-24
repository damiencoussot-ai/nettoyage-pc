// Nettoyage PC - gardien en temps réel (Premium) : icône près de l'horloge, vérifications légères toutes les minutes,
// notifications Windows. Lancé avec --gardien (à l'ouverture de session), sans droits administrateur, sans fenêtre.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NettoyagePC
{
    internal class GardienProcessus : ApplicationContext, ISurfaceGardien
    {
        private static Mutex verrou;
        private readonly NotifyIcon icone = new NotifyIcon();
        private readonly System.Windows.Forms.Timer minuteur = new System.Windows.Forms.Timer { Interval = 60000 };
        private readonly Control invoqueur = new Control();
        private readonly GardienCoeur coeur;
        private Action actionClic;
        private RegisteredWaitHandle attenteArret, attenteRecharger;
        private EventWaitHandle evArret, evRecharger;

        public static int Executer()
        {
            bool nouveau;
            verrou = new Mutex(true, Gardien.NomMutex, out nouveau);
            if (!nouveau) return 0;
            try
            {
                if (!GardienCoeur.DoitTourner()) return 0;
                JournalDemarrage.Noter("Gardien démarré.");
                Application.EnableVisualStyles();
                Application.Run(new GardienProcessus());
                JournalDemarrage.Noter("Gardien arrêté.");
                return 0;
            }
            catch (Exception ex) { JournalDemarrage.Erreur("gardien", ex); return 1; }
            finally { try { verrou.ReleaseMutex(); } catch { } }
        }

        private GardienProcessus()
        {
            invoqueur.CreateControl();
            coeur = new GardienCoeur(this);
            try { icone.Icon = Icon.ExtractAssociatedIcon(Chemins.Exe); } catch { icone.Icon = SystemIcons.Shield; }
            icone.Text = Langue.T("Nettoyage PC : gardien actif");
            icone.ContextMenuStrip = Menu();
            icone.DoubleClick += (s, e) => GardienCoeur.OuvrirAppli(null);
            icone.BalloonTipClicked += (s, e) => { var a = actionClic; actionClic = null; if (a != null) try { a(); } catch { } };
            icone.Visible = true;

            evArret = new EventWaitHandle(false, EventResetMode.AutoReset, Gardien.NomArret);
            evRecharger = new EventWaitHandle(false, EventResetMode.AutoReset, Gardien.NomRecharger);
            attenteArret = ThreadPool.RegisterWaitForSingleObject(evArret, (o, t) => SurUi(Quitter), null, -1, true);
            attenteRecharger = ThreadPool.RegisterWaitForSingleObject(evRecharger, (o, t) => SurUi(coeur.Recharger), null, -1, false);

            minuteur.Tick += (s, e) => coeur.Verifier();
            minuteur.Start();
            // première vérification une demi-minute après l'ouverture de session
            var premier = new System.Windows.Forms.Timer { Interval = 30000 };
            premier.Tick += (s, e) => { premier.Stop(); premier.Dispose(); coeur.Verifier(); };
            premier.Start();
        }

        public void SurUi(Action a) { try { if (invoqueur.IsHandleCreated) invoqueur.BeginInvoke(a); } catch { } }

        public void Infobulle(string texte) { try { icone.Text = texte.Length > 63 ? texte.Substring(0, 63) : texte; } catch { } }

        public void Notifier(string titre, string texte, int type, Action clic)
        {
            actionClic = clic;
            try { icone.ShowBalloonTip(15000, titre, texte, type == 2 ? ToolTipIcon.Error : type == 1 ? ToolTipIcon.Warning : ToolTipIcon.Info); } catch { }
        }

        private ContextMenuStrip Menu()
        {
            var m = new ContextMenuStrip();
            m.Items.Add(new ToolStripMenuItem(Langue.T("Nettoyage PC : gardien actif")) { Enabled = false });
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Langue.T("Ouvrir Nettoyage PC"), null, (s, e) => GardienCoeur.OuvrirAppli(null));
            m.Items.Add(Langue.T("Sauvegarder maintenant"), null, (s, e) => coeur.SauvegarderMaintenant());
            m.Items.Add(Langue.T("Réglages du gardien..."), null, (s, e) => GardienCoeur.OuvrirAppli("--ouvrir gardien"));
            m.Items.Add(Langue.T("Pause d'une heure"), null, (s, e) => coeur.Pause());
            m.Items.Add(new ToolStripSeparator());
            m.Items.Add(Langue.T("Arrêter le gardien"), null, (s, e) => Quitter());
            Theme.P = Theme.WindowsEnSombre() ? Theme.Sombre : Theme.Clair;
            m.Renderer = new ToolStripProfessionalRenderer(new CouleursMenu());
            m.BackColor = Theme.P.Carte; m.ForeColor = Theme.P.Texte;
            return m;
        }

        private class CouleursMenu : ProfessionalColorTable
        {
            public override Color MenuItemSelected { get { return Theme.P.Survol; } }
            public override Color MenuItemBorder { get { return Theme.P.Bordure; } }
            public override Color ToolStripDropDownBackground { get { return Theme.P.Carte; } }
            public override Color ImageMarginGradientBegin { get { return Theme.P.Carte; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.P.Carte; } }
            public override Color ImageMarginGradientEnd { get { return Theme.P.Carte; } }
            public override Color MenuBorder { get { return Theme.P.Bordure; } }
            public override Color SeparatorDark { get { return Theme.P.Bordure; } }
        }

        public void Quitter()
        {
            minuteur.Stop();
            coeur.Arreter();
            try { attenteArret.Unregister(null); attenteRecharger.Unregister(null); } catch { }
            icone.Visible = false; icone.Dispose();
            ExitThread();
        }
    }
}
