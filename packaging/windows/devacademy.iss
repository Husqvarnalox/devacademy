; Inno Setup script. Per-user install (no admin rights), Start menu shortcut, uninstaller.
; Compile: iscc /DAppVersion=0.1.0 /DRid=win-x64 /DSourceDir=<publish dir> /DOutputDir=<dir> /DRepoRoot=<repo root> devacademy.iss
; CI-built; not run-verified on the author's machine (macOS).
#ifndef AppVersion
  #error AppVersion is required (/DAppVersion=...)
#endif
#ifndef Rid
  #define Rid "win-x64"
#endif

[Setup]
AppId={{B7D0B1E4-6E41-4C5B-9C0B-6A1F6A3E5D21}
AppName=DevAcademy
AppVersion={#AppVersion}
AppPublisher=DevAcademy contributors
DefaultDirName={autopf}\DevAcademy
DefaultGroupName=DevAcademy
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir={#OutputDir}
OutputBaseFilename=DevAcademy-{#AppVersion}-{#Rid}-setup
SetupIconFile={#RepoRoot}\packaging\icon\DevAcademy.ico
UninstallDisplayIcon={app}\DevAcademy.Desktop.exe
LicenseFile={#RepoRoot}\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
#if Rid == "win-arm64"
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
#else
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
#endif

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; The whole publish dir: DevAcademy.Desktop.exe, native libraries, content\, THIRD-PARTY-NOTICES.md, LICENSE.
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{autoprograms}\DevAcademy"; Filename: "{app}\DevAcademy.Desktop.exe"
Name: "{autodesktop}\DevAcademy"; Filename: "{app}\DevAcademy.Desktop.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\DevAcademy.Desktop.exe"; Description: "{cm:LaunchProgram,DevAcademy}"; Flags: nowait postinstall skipifsilent

; User data (%LOCALAPPDATA%\DevAcademy: progress, settings) is deliberately NOT removed on uninstall.
