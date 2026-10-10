; Inno Setup script for YT Transcript
; Built by GitHub Actions (.github/workflows/build.yml). To build manually:
;   dotnet publish -c Release -r win-x64 --self-contained -o publish
;   iscc /DMyAppVersion=1.0.0 installer\YtTranscript.iss

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

#define MyAppName "YT Transcript"
#define MyAppExe "YtTranscript.exe"

[Setup]
AppId={{8C2F4A1E-6B3D-4E5F-9A7C-2D1B3E4F5A6B}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher=ulebule
AppPublisherURL=https://github.com/ulebule/yt-transcript
AppSupportURL=https://github.com/ulebule/yt-transcript/issues
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
; per-user install without admin rights (the user can still choose an all-users install)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
OutputDir=Output
OutputBaseFilename=YtTranscript-Setup-{#MyAppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}

[Languages]
Name: "slovenian"; MessagesFile: "compiler:Languages\Slovenian.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "..\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; files and shortcuts from versions before the rename (YT Prepis)
Type: files; Name: "{app}\YtPrepis.*"
Type: files; Name: "{autoprograms}\YT Prepis.lnk"
Type: files; Name: "{autodesktop}\YT Prepis.lnk"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent
