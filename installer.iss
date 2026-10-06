; Inno Setup script for Make my Web Screen Recorder. Built by build.ps1.

#define AppName "Make my Web Screen Recorder"
; The version comes from src\Program.cs (build.ps1 passes /DAppVersion=...).
#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#define AppExe "MakeMyWebScreenRecorder.exe"

[Setup]
AppId={{6F2C1E4A-8B7D-4C55-9E3A-2D1F0B6A7C91}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=Make my Web
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Installs for the current user without needing admin rights; the user can choose "all users" instead.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir=dist
OutputBaseFilename=MakeMyWebScreenRecorder-Setup-{#AppVersion}
SetupIconFile=assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/ultra64
SolidCompression=yes
LZMAUseSeparateProcess=yes
WizardStyle=modern
LicenseFile=LICENSE
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "build\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "build\ffmpeg\*"; DestDir: "{app}\ffmpeg"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; After a silent self-update, start the new version again.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: WizardSilent
