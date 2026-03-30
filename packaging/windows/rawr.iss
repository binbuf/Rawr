#ifndef APP_VERSION
  #define APP_VERSION "0.0.0"
#endif

[Setup]
AppName=Rawr
AppVersion={#APP_VERSION}
AppVerName=Rawr {#APP_VERSION}
AppPublisher=binbuf
DefaultDirName={autopf}\Rawr
DefaultGroupName=Rawr
OutputDir=Output
OutputBaseFilename=Rawr-Setup
SetupIconFile=..\..\src\Rawr.UI\Assets\dinosaur.ico
UninstallDisplayIcon={app}\Rawr.UI.exe
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern

[Files]
Source: "..\..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\Rawr"; Filename: "{app}\Rawr.UI.exe"
Name: "{group}\Uninstall Rawr"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Rawr"; Filename: "{app}\Rawr.UI.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\Rawr.UI.exe"; Description: "Launch Rawr"; Flags: nowait postinstall skipifsilent
