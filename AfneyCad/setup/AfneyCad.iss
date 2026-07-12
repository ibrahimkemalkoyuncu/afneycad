; AfneyCAD Inno Setup Script
; Derlemeden once: dotnet publish src/Afney.Cad.Presentation/Afney.Cad.Presentation.csproj -c Release -r win-x64 --self-contained true -o publish
; Derleme: iscc setup/AfneyCad.iss  (Inno Setup Compiler gerekir: https://jrsoftware.org/isinfo.php)

#define MyAppName "AfneyCAD"
#define MyAppVersion "4.0.0"
#define MyAppPublisher "Ibrahim Kemal Koyuncu"
#define MyAppExeName "Afney.Cad.Presentation.exe"
#define MyPublishDir "..\publish"

[Setup]
AppId={{9C1B9E9E-6B7A-4C3D-9C4D-AFNEYCAD40000}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=AfneyCad-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
UninstallDisplayIcon={app}\{#MyAppExeName}
SetupIconFile=..\src\Afney.Cad.Presentation\Resources\afneycad_icon.ico

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
