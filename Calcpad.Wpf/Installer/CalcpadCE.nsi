!include "MUI2.nsh"
!include "FileFunc.nsh"

;--------------------------------
; General

!define APP_NAME "CalcpadCE"
!ifndef APP_VERSION
  !define APP_VERSION "dev"
!endif
!define APP_PUBLISHER "CalcpadCE Community"
!define APP_URL "https://calcpad-ce.org/"
!define APP_EXE "CalcpadCE.exe"
!define PUBLISH_DIR "..\bin\Release\net10.0-windows\win-x64\publish"
!define DOTNET_MAJOR "10"
!define DOTNET_URL "https://aka.ms/dotnet/${DOTNET_MAJOR}.0/windowsdesktop-runtime-win-x64.exe"
!define DOTNET_INSTALLER "$TEMP\dotnet-windowsdesktop-runtime.exe"

Name "${APP_NAME} ${APP_VERSION}"
OutFile "Output\CalcpadCE-setup-${APP_VERSION}.exe"
InstallDir "$LOCALAPPDATA\Programs\${APP_NAME}"
InstallDirRegKey HKCU "Software\${APP_NAME}" "InstallPath"
RequestExecutionLevel user
SetCompressor /SOLID lzma
Unicode True
ManifestDPIAware System

;--------------------------------
; Interface Settings

!define MUI_ICON "..\resources\calcpad.ico"
!define MUI_UNICON "..\resources\calcpad.ico"
!define MUI_ABORTWARNING
!define MUI_FINISHPAGE_RUN "$INSTDIR\${APP_EXE}"
!define MUI_FINISHPAGE_RUN_TEXT "Launch ${APP_NAME}"

; Language dialog settings — remember choice in registry for upgrades
!define MUI_LANGDLL_REGISTRY_ROOT "HKCU"
!define MUI_LANGDLL_REGISTRY_KEY "Software\${APP_NAME}"
!define MUI_LANGDLL_REGISTRY_VALUENAME "InstallerLanguage"

;--------------------------------
; Pages

!insertmacro MUI_PAGE_LICENSE "..\..\LICENSE"
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

;--------------------------------
; Languages

!insertmacro MUI_LANGUAGE "English"
!insertmacro MUI_LANGUAGE "Bulgarian"
!insertmacro MUI_LANGUAGE "SimpChinese"

;--------------------------------
; Installer init — show language selection dialog

Function .onInit
  !insertmacro MUI_LANGDLL_DISPLAY
  Call CheckAndInstallDotNet
FunctionEnd

;--------------------------------
; .NET Runtime detection and installation

Function CheckAndInstallDotNet
  ; Check if .NET Desktop Runtime 10.x is installed by scanning the filesystem.
  ; This works regardless of install method (standalone installer, SDK, or Visual Studio).
  ; We verify an actual runtime DLL exists inside the version folder to avoid
  ; false positives from empty leftover directories after uninstall.
  FindFirst $0 $1 "$PROGRAMFILES64\dotnet\shared\Microsoft.WindowsDesktop.App\${DOTNET_MAJOR}.*"
  dotnet_check_loop:
    StrCmp $1 "" dotnet_not_found
    IfFileExists "$PROGRAMFILES64\dotnet\shared\Microsoft.WindowsDesktop.App\$1\System.Windows.Forms.dll" dotnet_found_close
    FindNext $0 $1
    Goto dotnet_check_loop
  dotnet_not_found:
    FindClose $0
    Goto dotnet_missing
  dotnet_found_close:
    FindClose $0
    Goto dotnet_found

dotnet_missing:
  MessageBox MB_YESNO|MB_ICONEXCLAMATION \
    ".NET Desktop Runtime ${DOTNET_MAJOR} is required but was not detected.$\n$\nDownload and install it now?" \
    IDYES dotnet_download
  Goto dotnet_found

dotnet_download:
  DetailPrint "Downloading .NET Desktop Runtime ${DOTNET_MAJOR}..."
  NScurl::http GET "${DOTNET_URL}" "${DOTNET_INSTALLER}" /POPUP /CANCEL /INSIST /END
  Pop $0
  StrCmp $0 "OK" dotnet_install
  MessageBox MB_OK|MB_ICONSTOP "Failed to download .NET Desktop Runtime: $0"
  Delete "${DOTNET_INSTALLER}"
  Abort

dotnet_install:
  DetailPrint "Installing .NET Desktop Runtime ${DOTNET_MAJOR}..."
  ExecWait '"${DOTNET_INSTALLER}" /install /passive /norestart' $0
  Delete "${DOTNET_INSTALLER}"
  ; Exit code 0 = success, 3010 = success but reboot needed
  StrCmp $0 0 dotnet_found
  StrCmp $0 3010 dotnet_found
  MessageBox MB_OK|MB_ICONSTOP "Failed to install .NET Desktop Runtime (exit code: $0)."
  Abort

dotnet_found:
FunctionEnd

;--------------------------------
; Install Section

Section "Install"
  SetOutPath "$INSTDIR"
  File /r "${PUBLISH_DIR}\*.*"

  ; Create Start Menu shortcuts
  CreateDirectory "$SMPROGRAMS\${APP_NAME}"
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0
  CreateShortCut "$SMPROGRAMS\${APP_NAME}\Uninstall ${APP_NAME}.lnk" "$INSTDIR\Uninstall.exe"

  ; Create Desktop shortcut
  CreateShortCut "$DESKTOP\${APP_NAME}.lnk" "$INSTDIR\${APP_EXE}" "" "$INSTDIR\${APP_EXE}" 0

  ; .cpd file association
  WriteRegStr HKCU "Software\Classes\.cpd" "" "Calcpad.CpdFile"
  WriteRegStr HKCU "Software\Classes\Calcpad.CpdFile" "" "Calcpad Document"
  WriteRegStr HKCU "Software\Classes\Calcpad.CpdFile\DefaultIcon" "" "$INSTDIR\${APP_EXE},0"
  WriteRegStr HKCU "Software\Classes\Calcpad.CpdFile\shell\open\command" "" '"$INSTDIR\${APP_EXE}" "%1"'

  ; .cpdz file association
  WriteRegStr HKCU "Software\Classes\.cpdz" "" "Calcpad.CpdzFile"
  WriteRegStr HKCU "Software\Classes\Calcpad.CpdzFile" "" "Calcpad Protected Document"
  WriteRegStr HKCU "Software\Classes\Calcpad.CpdzFile\DefaultIcon" "" "$INSTDIR\${APP_EXE},0"
  WriteRegStr HKCU "Software\Classes\Calcpad.CpdzFile\shell\open\command" "" '"$INSTDIR\${APP_EXE}" "%1"'

  ; Notify shell of association changes
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'

  ; Write install path to registry
  WriteRegStr HKCU "Software\${APP_NAME}" "InstallPath" "$INSTDIR"
  WriteRegStr HKCU "Software\${APP_NAME}" "Version" "${APP_VERSION}"

  ; Map NSIS language ID to app culture string and write to registry
  StrCmp $LANGUAGE 1026 0 +3
    WriteRegStr HKCU "Software\${APP_NAME}" "Language" "bg"
    Goto lang_done
  StrCmp $LANGUAGE 2052 0 +3
    WriteRegStr HKCU "Software\${APP_NAME}" "Language" "zh"
    Goto lang_done
  WriteRegStr HKCU "Software\${APP_NAME}" "Language" "en"
  lang_done:

  ; Write uninstall information
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayName" "${APP_NAME}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayVersion" "${APP_VERSION}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "Publisher" "${APP_PUBLISHER}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "DisplayIcon" "$INSTDIR\${APP_EXE}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "URLInfoAbout" "${APP_URL}"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "URLUpdateInfo" "https://github.com/imartincei/CalcpadCE"
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoModify" 1
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "NoRepair" 1

  ; Estimate installed size (KB)
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}" "EstimatedSize" $0
SectionEnd

;--------------------------------
; Uninstaller init — restore language for uninstall UI

Function un.onInit
  !insertmacro MUI_UNGETLANGUAGE
FunctionEnd

;--------------------------------
; Uninstall Section

Section "Uninstall"
  ; Remove files and directories
  RMDir /r "$INSTDIR"

  ; Remove Start Menu shortcuts
  RMDir /r "$SMPROGRAMS\${APP_NAME}"

  ; Remove Desktop shortcut
  Delete "$DESKTOP\${APP_NAME}.lnk"

  ; Remove file associations
  DeleteRegKey HKCU "Software\Classes\.cpd"
  DeleteRegKey HKCU "Software\Classes\Calcpad.CpdFile"
  DeleteRegKey HKCU "Software\Classes\.cpdz"
  DeleteRegKey HKCU "Software\Classes\Calcpad.CpdzFile"

  ; Notify shell of association changes
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'

  ; Remove registry entries
  DeleteRegKey HKCU "Software\${APP_NAME}"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\${APP_NAME}"
SectionEnd
