// Velox - Studio son : guide pas à pas. Chaque étape, de l'installation aux réglages dans le jeu, avec ce que Velox voit
// (fait ou à faire) et le bouton qui la fait. Rien n'est supposé : une étape que Velox ne peut pas vérifier (réglages
// dans le jeu) est marquée « à vérifier toi-même ».
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace NettoyagePC.Nouveau
{
    internal sealed partial class AudioJeu5
    {
        private StackPanel zoneGuide;
        private string signatureGuide;

        private Control ConstruireGuide()
        {
            zoneGuide = new StackPanel { Spacing = 12 };
            return zoneGuide;
        }

        /// <summary>Une étape du guide : numéro, coche, explication, et un bouton facultatif.</summary>
        private Control EtapeGuide(int n, bool fait, bool verifiable, string titre, string detail, string bouton = null, Action action = null)
        {
            var bloc = new StackPanel { Spacing = 6 };
            bloc.Children.Add(Etape(fait, n + ". " + titre, detail, !verifiable));
            if (!fait && bouton != null && action != null)
                bloc.Children.Add(new WrapPanel { Margin = new Thickness(28, 0, 0, 0), Children = { Studio.Bouton(bouton, false, action) } });
            return bloc;
        }

        private void MajGuide(ReleveAudioJeu v)
        {
            if (zoneGuide == null) return;
            var m = v.Moteur ?? new ReglagesMoteurSon();
            var et = v.MoteurEtat ?? new MoteurSon.EtatMoteur();
            bool vm = r.Moteur == "vm";
            int etat; Cible(v, out etat);
            bool apo = vm || v.Etat.Present;
            bool branche = vm || (v.Etat.Present && (etat == 0 || etat == 4));
            bool calibre = r.CalibFiltres != null && r.CalibFiltres.Count > 0 && r.CalibActive;
            bool cable = v.Cables.Count > 0;
            bool pas = m.Actif && et.Marche && et.Erreur == "";
            var studio = SortieStudio(v);
            bool nomme = studio != null && PorteNomStudio(studio.Nom);
            bool septUn = pas && et.Canaux >= 8;
            var jeu = AudioJeu.JeuVise(v.Jeu, r);
            string sig = apo + "|" + branche + "|" + calibre + "|" + cable + "|" + pas + "|" + nomme + "|" + septUn + "|" + jeu.Cle + "|" + pasOccupe + "|" + nomOccupe;
            if (sig == signatureGuide) return;
            signatureGuide = sig;
            zoneGuide.Children.Clear();
            int faites = new[] { apo, branche, cable, pas, nomme }.Count(x => x);
            zoneGuide.Children.Add(Studio.T(Langue.F("Tout pour entendre les pas, dans l'ordre. {0} étapes essentielles sur 5 sont faites. Velox coche ce qu'il peut vérifier lui-même.", faites), 12.5, Studio.Discret));

            zoneGuide.Children.Add(EtapeGuide(1, apo, true, Langue.T("Installer Equalizer APO (gratuit)"),
                Langue.T("C'est lui qui applique l'égaliseur de Velox. Velox le télécharge depuis son site officiel ; Windows demande l'autorisation."),
                Langue.T("Installer Equalizer APO"), () => InstallerApo(v.NomDefaut)));
            zoneGuide.Children.Add(EtapeGuide(2, branche, true, Langue.T("Brancher Equalizer APO sur ton casque"),
                Langue.T("Dans la fenêtre d'Equalizer APO, coche ton casque (pas « Velox Studio » ni « CABLE Input »), clique OK, puis redémarre le PC. Laisse les « Améliorations audio » de Windows activées sur ton casque : Equalizer APO en a besoin."),
                Langue.T("Ouvrir la fenêtre d'Equalizer APO"), OuvrirConfigurator));
            zoneGuide.Children.Add(EtapeGuide(3, calibre, false, Langue.T("Choisir ton casque (facultatif, conseillé)"),
                Langue.T("Plus bas, module « Calibration du casque » : tape ton modèle. Velox corrige ses défauts d'après des mesures de laboratoire.")));
            zoneGuide.Children.Add(EtapeGuide(4, cable, true, Langue.T("Installer VB-Cable (gratuit)"),
                Langue.T("Il fait passer le son du jeu par Velox. Décompresse le fichier téléchargé, fais un clic droit sur VBCABLE_Setup_x64.exe, « Exécuter en tant qu'administrateur », clique « Install Driver », puis redémarre le PC."),
                Langue.T("Télécharger VB-Cable"), () => { if (!exemple) Programmes.Ouvrir(MoteurSon.SiteCable); }));
            zoneGuide.Children.Add(EtapeGuide(5, pas, true, Langue.T("Allumer « Pas boostés »"),
                Langue.T("Force conseillée : Fort (Extrême si tu veux encore plus de pas). Ton arme : Réduite. Velox met le câble en sortie par défaut et joue le son traité sur ton casque."),
                Langue.T("Allumer Pas boostés"), () => { if (cable) BasculerPas(true); }));
            zoneGuide.Children.Add(EtapeGuide(6, nomme, true, Langue.T("Donner le nom « Velox Studio »"),
                Langue.T("Le câble apparaît alors sous ce nom dans Windows, Discord et tes jeux. Windows demande l'autorisation. Relance ton jeu après."),
                Langue.T("Nommer « Velox Studio »"), () => { if (studio != null) NommerStudio(studio); }));
            zoneGuide.Children.Add(EtapeGuide(7, septUn, false, Langue.T("Windows : Velox Studio en 7.1 (facultatif, pour aller plus loin)"),
                Langue.T("Panneau de configuration > Son > onglet Lecture > Velox Studio (ou CABLE Input) > Configurer > 7.1 Surround > Suivant jusqu'à Terminer. Tes tirs arrivent alors sur leur propre voie : « Ton arme » les isole mieux. Dans ce cas, coupe le mode casque du jeu (étape 8) et allume « Son 7.1 virtuel » (mode Expert)."),
                Langue.T("Ouvrir les réglages Son de Windows"), () => { if (!exemple) Programmes.Ouvrir("control.exe", "mmsys.cpl"); }));
            string spatial = jeu.Spatial != "" ? " " + Langue.F("Option « {0} » : allume-la si Velox Studio est en stéréo, coupe-la si tu l'as mis en 7.1 (étape 7).", jeu.Spatial) : "";
            string dans = jeu.DansLeJeu != "" ? " " + Langue.F("Conseillé : {0}", Langue.T(jeu.DansLeJeu)) : "";
            zoneGuide.Children.Add(EtapeGuide(8, false, false, Langue.F("Dans {0} (à vérifier toi-même)", jeu.Nom),
                Langue.T("Menu Audio du jeu : sortie (périphérique de son) sur « Velox Studio » ou « Par défaut ». Chat vocal sur ton casque directement, pour garder des voix naturelles.") + spatial + dans));
            zoneGuide.Children.Add(EtapeGuide(9, false, false, Langue.T("Vérifier à l'oreille"),
                Langue.T("Lance une partie, puis « Réécouter sans / avec Velox » juste après un combat. Fais aussi le test de direction (module « Tests d'écoute »).")));
        }
    }
}
