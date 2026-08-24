; Script Inno Setup — Gestionnaire Système Pro
; Compilation : "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\setup.iss

#define MyAppName "Gestionnaire Système Pro"
#define MyAppVersion "2.1.1"
#define MyAppPublisher "illama"
#define MyAppURL "https://github.com/illama/illama_windowsusefulapps"
#define MyAppExeName "SystemManagerPro.exe"

[Setup]
AppId={{F90FF9FD-CE6E-4E25-80B0-0FB4FA69727F}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}/releases
DefaultDirName={autopf}\GestionnaireSystemePro
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\installer-output
OutputBaseFilename=GestionnaireSystemeProSetup
SetupIconFile=..\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=yes
CloseApplicationsFilter=SystemManagerPro.exe
DisableWelcomePage=no
DisableDirPage=no

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "Créer un raccourci sur le Bureau"; GroupDescription: "Raccourcis supplémentaires :"

[Files]
Source: "..\publish-installer\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Désinstaller {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Nettoie l'entrée de démarrage automatique éventuellement créée par l'application elle-même.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "GestionnaireSystemePro"; Flags: deletevalue uninsdeletevalue

[Run]
; "shellexec" est indispensable ici : l'exécutable a son propre manifeste requireAdministrator,
; et un CreateProcess direct (comportement par défaut d'Inno Setup) échoue avec l'erreur 740
; "L'opération demandée nécessite une élévation". ShellExecute gère correctement ce cas.
Filename: "{app}\{#MyAppExeName}"; Description: "Lancer {#MyAppName}"; Flags: nowait postinstall skipifsilent shellexec

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\GestionnaireSystemePro"
