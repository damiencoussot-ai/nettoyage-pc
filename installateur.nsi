; ============================================================
;  Nettoyage PC - installateur Windows (NSIS)
;  /DV5 : installateur de la version 5 (programme .NET 10 autonome, nouvelle interface). Même dossier, même
;  programme NettoyagePC.exe, mêmes réglages et même historique : la version 4 en place est remplacée (et gardée
;  dans « precedente » jusqu'à ce que la version 5 ait passé son contrôle de démarrage).
; ============================================================
Unicode true
SetCompressor /SOLID lzma
!include "MUI2.nsh"
!include "x64.nsh"
!include "LogicLib.nsh"
!include "WinVer.nsh"

!define NOM        "Nettoyage PC"
!ifndef VERSION
  !define VERSION "4.6"
!endif
!ifndef VERSION4
  !define VERSION4 "4.6.0.0"
!endif
!define EDITEUR    "Damien"
!define CLE_DESINST "Software\Microsoft\Windows\CurrentVersion\Uninstall\NettoyagePC"
Var Precedente

Name "${NOM} ${VERSION}"
OutFile "NettoyagePC-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES64\${NOM}"
InstallDirRegKey HKLM "${CLE_DESINST}" "InstallLocation"
RequestExecutionLevel admin
BrandingText "${NOM} ${VERSION}"

VIProductVersion "${VERSION4}"
VIAddVersionKey /LANG=1036 "ProductName" "${NOM}"
VIAddVersionKey /LANG=1036 "FileDescription" "Installation de ${NOM}"
VIAddVersionKey /LANG=1036 "CompanyName" "${EDITEUR}"
VIAddVersionKey /LANG=1036 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1036 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=1036 "LegalCopyright" "${EDITEUR}"

; --- Apparence
!define MUI_ICON "NettoyagePC.ico"
!define MUI_UNICON "NettoyagePC.ico"
!define MUI_HEADERIMAGE
!define MUI_HEADERIMAGE_RIGHT
!define MUI_HEADERIMAGE_BITMAP "bandeau.bmp"
!define MUI_HEADERIMAGE_UNBITMAP "bandeau.bmp"
!define MUI_WELCOMEFINISHPAGE_BITMAP "panneau.bmp"
!define MUI_UNWELCOMEFINISHPAGE_BITMAP "panneau.bmp"
!define MUI_ABORTWARNING

!define MUI_WELCOMEPAGE_TITLE "Installation de ${NOM} ${VERSION}"
!define MUI_WELCOMEPAGE_TEXT "Nettoyage, optimisation, sécurité, disques et mémoire vive pour Windows 10 et 11.$\r$\n$\r$\nTes documents ne sont jamais supprimés, et un point de restauration est créé avant les opérations importantes.$\r$\n$\r$\nSi une version précédente est installée, ton historique, tes réglages et l'entretien automatique sont conservés.$\r$\n$\r$\nClique sur Suivant pour continuer."
!define MUI_FINISHPAGE_RUN
!define MUI_FINISHPAGE_RUN_TEXT "Lancer ${NOM} maintenant"
!define MUI_FINISHPAGE_RUN_FUNCTION LancerAppli
!define MUI_FINISHPAGE_SHOWREADME "$INSTDIR\LISEZMOI.txt"
!define MUI_FINISHPAGE_SHOWREADME_TEXT "Afficher le mode d'emploi"
!define MUI_FINISHPAGE_SHOWREADME_NOTCHECKED

!insertmacro MUI_PAGE_WELCOME
!define MUI_LICENSEPAGE_TEXT_BOTTOM "Clique sur « J'accepte » pour accepter le contrat de licence et continuer l'installation."
!insertmacro MUI_PAGE_LICENSE "legal\LICENCE.txt"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_UNPAGE_FINISH

!insertmacro MUI_LANGUAGE "French"

; --- Vérifications avant l'installation
Function .onInit
    ${IfNot} ${RunningX64}
        MessageBox MB_ICONSTOP "${NOM} nécessite une version 64 bits de Windows 10 ou 11."
        Abort
    ${EndIf}
    ${IfNot} ${AtLeastWin10}
        MessageBox MB_ICONSTOP "${NOM} est conçu pour Windows 10 et Windows 11.$\r$\nCe système est trop ancien."
        Abort
    ${EndIf}
    SetRegView 64
!ifndef V5
    ; .NET Framework 4.8 (présent dans Windows 10 1903 et plus récent, et dans Windows 11)
    ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
    ${If} $0 < 528040
        MessageBox MB_YESNO|MB_ICONEXCLAMATION "${NOM} a besoin de .NET Framework 4.8, qui manque sur ce PC (Windows pas à jour ?).$\r$\n$\r$\nOuvrir la page de téléchargement de Microsoft ?" /SD IDNO IDNO +2
            ExecShell "open" "https://dotnet.microsoft.com/download/dotnet-framework/net48"
        Abort
    ${EndIf}
!endif
    ; (version 5 : le programme contient tout ce qu'il lui faut, rien à installer en plus)
FunctionEnd

; Mise à jour silencieuse (/S, lancée depuis l'application) : Nettoyage PC est relancé à la fin, avec les droits normaux
Function .onInstSuccess
    ; Windows rafraîchit les icônes (nouveau logo) du menu Démarrer, du Bureau et de la barre des tâches :
    ; sans cela, il garde l'ancienne image en cache
    ${DisableX64FSRedirection}
    IfFileExists "$SYSDIR\ie4uinit.exe" 0 +2
        ExecWait '"$SYSDIR\ie4uinit.exe" -show'
    ${EnableX64FSRedirection}
    System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0x1000, p 0, p 0)'
    ${If} ${Silent}
        Exec '"$WINDIR\explorer.exe" "$INSTDIR\NettoyagePC.exe"'
    ${EndIf}
FunctionEnd

Function LancerAppli
    ; lancé par l'Explorateur : l'application démarre avec les droits normaux, pas ceux de l'installateur
    Exec '"$WINDIR\explorer.exe" "$INSTDIR\NettoyagePC.exe"'
FunctionEnd

; Attend que Nettoyage PC soit fermé (mise à jour lancée depuis l'application) avant de remplacer le programme
Function AttendreFermeture
    ; le gardien Premium (icône près de l'horloge) est prié de se fermer : il garde le programme ouvert
    System::Call 'kernel32::OpenEventW(i 0x0002, i 0, w "Local\NettoyagePC-Gardien-Arret") p .r2'
    ${If} $2 P<> 0
        System::Call 'kernel32::SetEvent(p r2)'
        System::Call 'kernel32::CloseHandle(p r2)'
        Sleep 1500
    ${EndIf}
    StrCpy $1 0
    boucle:
        IfFileExists "$INSTDIR\NettoyagePC.exe" 0 fini
        ClearErrors
        FileOpen $0 "$INSTDIR\NettoyagePC.exe" a
        IfErrors occupe
        FileClose $0
        Goto fini
    occupe:
        IntOp $1 $1 + 1
        IntCmp $1 40 trop 0 trop
        Sleep 500
        Goto boucle
    trop:
        ${If} ${Silent}
            nsExec::Exec '"$SYSDIR\taskkill.exe" /F /IM NettoyagePC.exe'
            Pop $0
            Sleep 1500
            Goto fini
        ${EndIf}
        MessageBox MB_RETRYCANCEL|MB_ICONEXCLAMATION "${NOM} est encore ouvert.$\r$\n$\r$\nFerme-le, puis clique sur Réessayer." IDRETRY recommencer
        Abort
    recommencer:
        StrCpy $1 0
        Goto boucle
    fini:
FunctionEnd

; --- Sections
Section "${NOM} (obligatoire)" SecPrincipal
    SectionIn RO
    SetShellVarContext all
    SetOutPath "$INSTDIR"
    Call AttendreFermeture

    ; --- Retour arrière : la version en place est mise de côté avant d'être remplacée
    StrCpy $Precedente 0
    RMDir /r "$INSTDIR\precedente"
    IfFileExists "$INSTDIR\NettoyagePC.exe" 0 sansPrecedente
        CreateDirectory "$INSTDIR\precedente"
        CopyFiles /SILENT "$INSTDIR\NettoyagePC.exe" "$INSTDIR\precedente"
        CopyFiles /SILENT "$INSTDIR\LISEZMOI.txt" "$INSTDIR\precedente"
        IfFileExists "$INSTDIR\NettoyagePC.ps1" 0 +5
            CopyFiles /SILENT "$INSTDIR\NettoyagePC.ps1" "$INSTDIR\precedente"
            CopyFiles /SILENT "$INSTDIR\nettoyage-pc-complet.ps1" "$INSTDIR\precedente"
            CreateDirectory "$INSTDIR\precedente\modules"
            CopyFiles /SILENT "$INSTDIR\modules\*.*" "$INSTDIR\precedente\modules"
        StrCpy $Precedente 1
    sansPrecedente:

    File "NettoyagePC.exe"
    File "NettoyagePC.ico"
    File "LISEZMOI.txt"
    File "mise-a-jour.json"
    SetOutPath "$INSTDIR\legal"
    File "legal\LICENCE.txt"
    File "legal\CONDITIONS-DE-VENTE.txt"
    File "legal\CONFIDENTIALITE.txt"
    File "legal\MENTIONS-LEGALES.txt"
    SetOutPath "$INSTDIR"

    ; --- Auto-contrôle de la nouvelle version (fenêtre et pages créées sans être affichées) ; échec = retour à la version précédente
    ExecWait '"$INSTDIR\NettoyagePC.exe" --autocontrole' $0
    ${If} $0 != 0
        ${If} $Precedente == 1
            CopyFiles /SILENT "$INSTDIR\precedente\NettoyagePC.exe" "$INSTDIR"
            CopyFiles /SILENT "$INSTDIR\precedente\LISEZMOI.txt" "$INSTDIR"
            IfFileExists "$INSTDIR\precedente\NettoyagePC.ps1" 0 +5
                CopyFiles /SILENT "$INSTDIR\precedente\NettoyagePC.ps1" "$INSTDIR"
                CopyFiles /SILENT "$INSTDIR\precedente\nettoyage-pc-complet.ps1" "$INSTDIR"
                CreateDirectory "$INSTDIR\modules"
                CopyFiles /SILENT "$INSTDIR\precedente\modules\*.*" "$INSTDIR\modules"
            MessageBox MB_ICONEXCLAMATION "La nouvelle version de ${NOM} n'a pas passé son contrôle de démarrage (code $0) : la version précédente a été remise en place, rien n'est perdu.$\r$\n$\r$\nDétails : %LOCALAPPDATA%\NettoyagePC\demarrage.log" /SD IDOK
            SetErrorLevel 2
            Goto fin
        ${Else}
            MessageBox MB_ICONEXCLAMATION "${NOM} est installé, mais son contrôle de démarrage a signalé un problème (code $0).$\r$\nS'il ne s'ouvre pas, envoie le fichier %LOCALAPPDATA%\NettoyagePC\demarrage.log à la personne qui t'aide." /SD IDOK
        ${EndIf}
    ${EndIf}

    ; Fichiers de la version 3 (PowerShell) : remplacés par le programme unique
    Delete "$INSTDIR\NettoyagePC.ps1"
    Delete "$INSTDIR\nettoyage-pc-complet.ps1"
    RMDir /r "$INSTDIR\modules"
    RMDir /r "$INSTDIR\cache"
    WriteUninstaller "$INSTDIR\Desinstaller.exe"

    ; Menu Démarrer
    CreateShortcut "$SMPROGRAMS\${NOM}.lnk" "$INSTDIR\NettoyagePC.exe" "" "$INSTDIR\NettoyagePC.ico" 0 SW_SHOWNORMAL "" "Nettoyage, optimisation et sécurité du PC"
    Delete "$SMPROGRAMS\${NOM} (mode limité).lnk"

    ; Paramètres > Applications installées
    WriteRegStr HKLM "${CLE_DESINST}" "DisplayName" "${NOM}"
    WriteRegStr HKLM "${CLE_DESINST}" "DisplayVersion" "${VERSION}"
    WriteRegStr HKLM "${CLE_DESINST}" "Publisher" "${EDITEUR}"
    WriteRegStr HKLM "${CLE_DESINST}" "DisplayIcon" "$INSTDIR\NettoyagePC.exe,0"
    WriteRegStr HKLM "${CLE_DESINST}" "InstallLocation" "$INSTDIR"
    WriteRegStr HKLM "${CLE_DESINST}" "UninstallString" '"$INSTDIR\Desinstaller.exe"'
    WriteRegStr HKLM "${CLE_DESINST}" "QuietUninstallString" '"$INSTDIR\Desinstaller.exe" /S'
    WriteRegDWORD HKLM "${CLE_DESINST}" "NoModify" 1
    WriteRegDWORD HKLM "${CLE_DESINST}" "NoRepair" 1
!ifdef V5
    WriteRegDWORD HKLM "${CLE_DESINST}" "EstimatedSize" 48000
!else
    WriteRegDWORD HKLM "${CLE_DESINST}" "EstimatedSize" 1200
!endif

    ; Si l'entretien automatique existait (version 3 ou 4), ses tâches sont recréées pour le nouveau programme
    nsExec::Exec '"$SYSDIR\schtasks.exe" /Query /TN "Nettoyage PC - hebdomadaire"'
    Pop $0
    nsExec::Exec '"$SYSDIR\schtasks.exe" /Query /TN "Nettoyage PC hebdomadaire"'
    Pop $1
    ${If} $0 == 0
    ${OrIf} $1 == 0
        nsExec::ExecToLog '"$INSTDIR\NettoyagePC.exe" --moteur --planifier'
        Pop $0
    ${EndIf}
    fin:
SectionEnd

Section "Raccourci sur le Bureau" SecBureau
    SetShellVarContext all
    CreateShortcut "$DESKTOP\${NOM}.lnk" "$INSTDIR\NettoyagePC.exe" "" "$INSTDIR\NettoyagePC.ico" 0 SW_SHOWNORMAL "" "Nettoyage, optimisation et sécurité du PC"
SectionEnd

LangString DESC_Principal ${LANG_FRENCH} "Le logiciel (interface, moteur et entretien automatique), l'entrée du menu Démarrer et la désinstallation."
LangString DESC_Bureau ${LANG_FRENCH} "Ajoute l'icône Nettoyage PC sur le Bureau."
!insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SecPrincipal} $(DESC_Principal)
    !insertmacro MUI_DESCRIPTION_TEXT ${SecBureau} $(DESC_Bureau)
!insertmacro MUI_FUNCTION_DESCRIPTION_END

; --- Désinstallation
Function un.onInit
    SetRegView 64
FunctionEnd

Section "Uninstall"
    SetShellVarContext all
    ; Retire l'entretien automatique (tâches planifiées de Nettoyage PC)
    IfFileExists "$INSTDIR\NettoyagePC.exe" 0 +3
        nsExec::ExecToLog '"$INSTDIR\NettoyagePC.exe" --moteur --deplanifier --deplanifier-apps'
        Pop $0
    nsExec::ExecToLog '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -Command "Get-ScheduledTask -TaskName ''Nettoyage PC*'' -ErrorAction SilentlyContinue | Unregister-ScheduledTask -Confirm:$$false"'
    Pop $0

    ; Arrête le gardien Premium et retire son lancement automatique
    System::Call 'kernel32::OpenEventW(i 0x0002, i 0, w "Local\NettoyagePC-Gardien-Arret") p .r2'
    ${If} $2 P<> 0
        System::Call 'kernel32::SetEvent(p r2)'
        System::Call 'kernel32::CloseHandle(p r2)'
        Sleep 1500
    ${EndIf}
    DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "NettoyagePC-Gardien"

    Delete "$SMPROGRAMS\${NOM}.lnk"
    Delete "$SMPROGRAMS\${NOM} (mode limité).lnk"
    Delete "$DESKTOP\${NOM}.lnk"
    SetShellVarContext current
    Delete "$DESKTOP\${NOM}.lnk"

    Delete "$INSTDIR\NettoyagePC.exe"
    Delete "$INSTDIR\NettoyagePC.ps1"
    Delete "$INSTDIR\nettoyage-pc-complet.ps1"
    Delete "$INSTDIR\NettoyagePC.ico"
    Delete "$INSTDIR\LISEZMOI.txt"
    Delete "$INSTDIR\mise-a-jour.json"
    RMDir /r "$INSTDIR\modules"
    RMDir /r "$INSTDIR\cache"
    RMDir /r "$INSTDIR\maj"
    RMDir /r "$INSTDIR\precedente"
    RMDir /r "$INSTDIR\legal"
    RMDir /r "$INSTDIR\outils"   ; PresentMon (Preuve en FPS), téléchargé au premier usage
    Delete "$INSTDIR\Desinstaller.exe"
    RMDir "$INSTDIR"

    DeleteRegKey HKLM "${CLE_DESINST}"

    ; Journaux : supprimés seulement si l'utilisateur le demande
    IfFileExists "$DOCUMENTS\NettoyagePC\*.*" 0 fin
        MessageBox MB_YESNO|MB_ICONQUESTION|MB_DEFBUTTON2 "Supprimer aussi les journaux et rapports de Nettoyage PC ?$\r$\n($DOCUMENTS\NettoyagePC)" /SD IDNO IDNO fin
        RMDir /r "$DOCUMENTS\NettoyagePC"
    fin:
SectionEnd
