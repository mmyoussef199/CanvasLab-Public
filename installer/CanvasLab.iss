#define MyAppName "CanvasLab"
#define MyAppVersion "0.1.0-beta.1"
#define MyAppPublisher "Mohamed Youssef"
#define MyAppURL "https://github.com/mmyoussef199/CanvasLab-Public"
#define MyAppExeName "CanvasLab.exe"

[Setup]
AppId={{C2028630-AA0C-4802-B2C0-3FB401661801}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}
AppCopyright=Copyright (C) 2026 Mohamed Youssef
VersionInfoVersion=0.1.0.0
VersionInfoProductVersion=0.1.0.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription=CanvasLab Installer
VersionInfoCopyright=Copyright (C) 2026 Mohamed Youssef
DefaultDirName={localappdata}\Programs\CanvasLab
DefaultGroupName=CanvasLab
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=output
OutputBaseFilename=CanvasLab-Setup-{#MyAppVersion}
SetupIconFile=..\assets\CanvasLab.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName} {#MyAppVersion}
LicenseFile=..\LICENSE
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\.artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\CanvasLab"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{autodesktop}\CanvasLab"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
