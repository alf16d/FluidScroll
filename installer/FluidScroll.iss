#define AppName "FluidScroll"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef AppFileVersion
  #define AppFileVersion "1.0.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\publish"
#endif

[Setup]
AppId={{22B80702-B919-49AF-B35C-B81F3F3EC0B9}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=alf16d
AppPublisherURL=https://github.com/alf16d/FluidScroll
AppSupportURL=https://github.com/alf16d/FluidScroll/issues
AppUpdatesURL=https://github.com/alf16d/FluidScroll/releases
DefaultDirName={localappdata}\Programs\FluidScroll
DefaultGroupName=FluidScroll
DisableProgramGroupPage=yes
OutputDir=..\dist
OutputBaseFilename=FluidScrollSetup
SetupIconFile=..\imgs\icon_256.ico
UninstallDisplayIcon={app}\FluidScroll.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest
CloseApplications=yes
RestartApplications=no
VersionInfoVersion={#AppFileVersion}
VersionInfoCompany=alf16d
VersionInfoDescription=FluidScroll installer
VersionInfoProductName=FluidScroll
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\FluidScroll.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\FluidScroll"; Filename: "{app}\FluidScroll.exe"
Name: "{group}\Uninstall FluidScroll"; Filename: "{uninstallexe}"
Name: "{autodesktop}\FluidScroll"; Filename: "{app}\FluidScroll.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\FluidScroll.exe"; Description: "Launch FluidScroll"; Flags: nowait postinstall skipifsilent

[InstallDelete]
Type: files; Name: "{app}\FluidScroll.exe"

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM FluidScroll.exe /F >NUL 2>NUL"; Flags: runhidden
Filename: "{cmd}"; Parameters: "/C reg delete HKCU\Software\Microsoft\Windows\CurrentVersion\Run /v FluidScroll /f >NUL 2>NUL"; Flags: runhidden

[UninstallDelete]
Type: files; Name: "{userappdata}\FluidScroll\settings.json"
Type: dirifempty; Name: "{userappdata}\FluidScroll"

[Code]
function StopFluidScroll(): Boolean;
var
  ResultCode: Integer;
begin
  Exec(
    ExpandConstant('{cmd}'),
    '/C taskkill /IM FluidScroll.exe /F >NUL 2>NUL',
    '',
    SW_HIDE,
    ewWaitUntilTerminated,
    ResultCode);
  Result := True;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  StopFluidScroll();
  Result := '';
end;
