#define MyAppName "JAN Label Windows Shell"
#define MyAppPublisher "WSL043"
#define MyAppExeName "JanLabel.WindowsShell.exe"
#define MyDefaultDirName "{localappdata}\Programs\JAN Label Windows Shell"

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0-preview"
#endif

#ifndef MyOutputDir
  #define MyOutputDir "."
#endif

#ifndef MyOutputBaseFilename
  #define MyOutputBaseFilename "JAN-Label-Windows-Shell-Setup"
#endif

#ifndef MySourceDir
  #define MySourceDir "..\bin\Release\net8.0-windows\win-x64\publish"
#endif

[Setup]
AppId={{7B88671F-6251-4A38-9A94-74BEAE8BFE5C}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={#MyDefaultDirName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir={#MyOutputDir}
OutputBaseFilename={#MyOutputBaseFilename}
Compression=lzma
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
SetupIconFile=..\..\desktop-shell\src-tauri\icons\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#MySourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
