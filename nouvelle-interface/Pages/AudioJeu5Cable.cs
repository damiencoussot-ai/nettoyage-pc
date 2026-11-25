// Velox - Studio son : installation de VB-Cable par Velox (voir InstallationCable), et, s'il manque aussi, d'Equalizer APO
// dans la foulée : un seul redémarrage, proposé à la fin.
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace NettoyagePC.Nouveau
{
    internal sealed partial class AudioJeu5
    {
        private static bool installationCable;

        /// <summary>
        /// Un clic sur « Installer VB-Cable » : Velox explique ce qu'il va faire (VB-Cable est un logiciel de VB-Audio,
        /// donationware : sa licence demande de le dire), puis installe VB-Cable, et Equalizer APO avant lui s'il manque.
        /// À la fin, un seul redémarrage est proposé.
        /// </summary>
        private void InstallerCable(ReleveAudioJeu v)
        {
            if (exemple || installationCable || installationApo) return;
            if (simulation) { F.Statut(Langue.T("Simulation : VB-Cable serait téléchargé et installé, rien n'est modifié."), true); return; }
            if (!OperatingSystem.IsWindows()) return;
            if (InstallationCable.PiloteInstalle())
            {
                // pilote là, câble pas encore visible : il manque seulement le redémarrage
                ProposerRedemarrage(Langue.T("VB-Cable est déjà installé, mais Windows ne l'a pas encore activé."));
                return;
            }
            bool avecApo = v != null && r.Moteur != "vm" && !v.Etat.Present;
            string nomSortie = v != null && !string.IsNullOrEmpty(v.NomDefaut) ? v.NomDefaut : Langue.T("ton casque ou tes haut-parleurs");
            string texte = Langue.T("Velox va télécharger VB-Cable (environ 1 Mo) depuis le site de VB-Audio, vérifier que l'installateur est bien signé par VB-Audio, puis l'installer. Windows te demandera l'autorisation, puis « Voulez-vous installer ce logiciel de périphérique ? » : clique « Installer ».") + "\n\n";
            if (avecApo)
                texte += Langue.F("Equalizer APO manque aussi : Velox l'installe d'abord. Une fenêtre d'Equalizer APO s'ouvrira : coche « {0} », puis clique sur OK.", nomSortie) + "\n\n"
                    + Langue.T("Un seul redémarrage à la fin, pour les deux.") + "\n\n";
            texte += Langue.T("VB-Cable est un logiciel de VB-Audio (vb-cable.com). C'est un donationware : gratuit, mais si tu t'en sers souvent, tu peux soutenir ses auteurs sur leur site.") + "\n\n"
                + Langue.F("Pour le désinstaller plus tard : Velox garde son installateur dans « {0} » ; lance-le en administrateur, puis « Remove Driver ».", InstallationCable.Dossier);
            F.Demander(Langue.T(avecApo ? "Installer Equalizer APO et VB-Cable" : "Installer VB-Cable"), texte, Langue.T("Installer"), Langue.T("Annuler"), oui =>
            {
                if (!oui) return;
                if (!avecApo) { LancerInstallationCable(false); return; }
                LancerInstallationApo(nomSortie, errApo =>
                {
                    // Equalizer APO refusé ou en échec : on s'arrête là (la raison est déjà affichée)
                    if (errApo == "") LancerInstallationCable(true);
                });
            });
        }

        private void LancerInstallationCable(bool apoAussi)
        {
            installationCable = true; page?.MettreAJour();
            F.Statut(Langue.T("Téléchargement de VB-Cable…"));
            var f = F;
            Task.Run(() =>
            {
                string dossier = InstallationCable.Telecharger(p => Dispatcher.UIThread.Post(() => f.Statut(p >= 0 ? Langue.F("Téléchargement de VB-Cable… {0} %", p) : Langue.T("Téléchargement de VB-Cable…"))), CancellationToken.None);
                Dispatcher.UIThread.Post(() => f.Statut(Langue.T("Installation de VB-Cable : accepte l'autorisation de Windows, puis clique « Installer » quand Windows demande s'il faut installer le pilote.")));
                return InstallationCable.Installer(dossier);
            }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
            {
                installationCable = false;
                string err = t.IsFaulted ? (t.Exception.InnerException ?? t.Exception).Message : t.Result;
                signatureSorties = null; signaturePas = null; signatureGuide = null;
                Relever();
                page?.MettreAJour();
                if (err != "")
                {
                    f.Statut(Langue.T(err), true);
                    if (!err.StartsWith("Installation annulée"))
                        f.Demander(Langue.T("VB-Cable n'a pas pu être installé"), Langue.T(err) + "\n\n" + Langue.T("Réessaie dans un instant (même bouton), ou installe-le toi-même depuis son site officiel : une fois installé, Velox le reconnaît tout seul."),
                            Langue.T("Ouvrir le site officiel"), Langue.T("Fermer"), oui => { if (oui) Programmes.Ouvrir(MoteurSon.SiteCable); });
                    return;
                }
                ProposerRedemarrage(Langue.T(apoAussi ? "Equalizer APO et VB-Cable sont installés." : "VB-Cable est installé."));
            }));
        }

        /// <summary>Propose de redémarrer le PC dans une minute (annulable), pour finir l'installation de VB-Cable.</summary>
        private void ProposerRedemarrage(string fait)
        {
            F.Demander(Langue.T("Redémarrer le PC"),
                fait + " " + Langue.T("Windows doit redémarrer pour l'activer. Enregistre ton travail : le PC redémarre une minute après ton clic. Au retour, ouvre le Studio son : Velox branche tout lui-même."),
                Langue.T("Redémarrer dans 1 minute"), Langue.T("Plus tard"), oui =>
                {
                    if (!oui) { F.Statut(Langue.T("Pense à redémarrer le PC : VB-Cable n'apparaîtra qu'après.")); return; }
                    string err = Extinction.Programmer(1, true);
                    if (err == "") F.Statut(Langue.T("Le PC redémarre dans 1 minute. Pour annuler : Windows + R, tape « shutdown /a », Entrée."));
                    else F.Statut(Langue.F("Windows a refusé le redémarrage ({0}) : redémarre le PC toi-même.", err), true);
                });
        }
    }
}
