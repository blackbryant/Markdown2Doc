#define MyAppName "Markdown2Doc"
#define MyAppPublisher "Bryant.lin"
#define MyAppURL "https://github.com/blackbryant/Markdown2Doc"
#define MyAppExeName "Markdown2Doc.exe"

#ifndef MyAppVersion
  #define MyAppVersion "1.0.2"
#endif
#ifndef MyPublishDir
  #define MyPublishDir "artifacts\win-x64"
#endif

#ifndef ThirdPartyDir
  #define ThirdPartyDir "..\Markdown2Doc\bin\Debug\net8.0-windows"
#endif


[Setup]
AppId={{6F2A1A9E-3A7F-4B4C-9AE2-7E0C2FC2B6C3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={pf}\{#MyAppName}
OutputBaseFilename={#MyAppName}-Setup-{#MyAppVersion}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#ThirdPartyDir}\x64\Scintilla.dll"; DestDir: "{app}\x64"; Flags: ignoreversion
Source: "{#ThirdPartyDir}\x64\Lexilla.dll"; DestDir: "{app}\x64"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{commondesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Run]
Filename: "{app}\{#MyAppExeName}"; Flags: nowait postinstall skipifsilent



