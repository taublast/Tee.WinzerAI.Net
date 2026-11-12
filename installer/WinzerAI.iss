; Setup für WinzerAI (Inno Setup 6). Gebaut von installer\build.ps1 und der Release-Pipeline:
;   ISCC /DAppVersion=0.1.0 /DArch=x64 /DSourceDir=..\artifacts\publish\win-x64 installer\WinzerAI.iss
; Installiert für den angemeldeten Benutzer ohne Administratorrechte. Spielstände liegen unter
; %LOCALAPPDATA%\WinzerAI\Spielstände und bleiben bei Update und Deinstallation erhalten.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef Arch
  #define Arch "x64"
#endif
#ifndef SourceDir
  #define SourceDir "..\artifacts\publish\win-" + Arch
#endif
#ifndef OutputDir
  #define OutputDir "..\artifacts"
#endif

[Setup]
AppId={{6F0B7C0E-6C5B-4E0C-9F4B-57A3D1C2A9E1}
AppName=WinzerAI
AppVersion={#AppVersion}
AppPublisher=teebeast
DefaultDirName={localappdata}\Programs\WinzerAI
DefaultGroupName=WinzerAI
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=WinzerAI-{#AppVersion}-{#Arch}-setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
LicenseFile=..\LICENSE
UninstallDisplayIcon={app}\WinzerAI.exe
UninstallDisplayName=WinzerAI
CloseApplications=yes
#if Arch == "arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "german"; MessagesFile: "compiler:Languages\German.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\WinzerAI"; Filename: "{app}\WinzerAI.exe"
Name: "{autodesktop}\WinzerAI"; Filename: "{app}\WinzerAI.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\WinzerAI.exe"; Description: "{cm:LaunchProgram,WinzerAI}"; Flags: nowait postinstall skipifsilent

[InstallDelete]
; Dateien einer älteren Fassung, die es in der neuen nicht mehr gibt, sollen nicht liegen bleiben
Type: filesandordirs; Name: "{app}\*"
