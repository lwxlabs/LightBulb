#preproc ispp

; LightBulb extended fork single-file installer/bootstrapper (Inno Setup 6+)
; Compiling this ISS first publishes a self-contained win-x64 payload.
#ifndef SkipPublish
#define PrepareExitCode Exec("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File Installer\Prepare-InstallerPayload.ps1", SourcePath, 1, SW_SHOWNORMAL)
#if PrepareExitCode != 0
  #error "LightBulb Release preparation failed. Fix the build error above and compile this .iss again."
#endif
#endif

#define AppName "LightBulb"
#define AppDisplayVersion "2.7.1-forked-0.21"
#define AppFileVersion "2.7.1.21"
#define AppPublisher "LwXLabs"
#define PayloadDir "artifacts\InstallerPayload"

[Setup]
AppId={{892F745F-A497-42ED-B503-8D74936D0BEB}
AppName={#AppName}
AppVersion={#AppDisplayVersion}
AppVerName={#AppName} {#AppDisplayVersion}
AppPublisher={#AppPublisher}
AppPublisherURL=https://github.com/lwxlabs/LightBulb
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
AllowNoIcons=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
DisableWelcomePage=yes
DisableProgramGroupPage=yes
; Always show the real destination path. The previous 0.19 screenshot was the
; Start Menu folder page ("LightBulb"), not the application directory.
DisableDirPage=no
UsePreviousAppDir=yes
DisableReadyPage=yes
; Keep Inno Setup's standard Windows Restart Manager flow. If LightBulb is
; running, Setup pauses on the standard Preparing to Install page and lets the
; user choose whether Setup should close it. "force" makes the selected automatic
; close reliable even though LightBulb normally stays alive in the tray.
CloseApplications=force
CloseApplicationsFilter=LightBulb.exe
; Do not ask Restart Manager to relaunch the old process. The [Run] entry below
; starts the newly installed version after Setup completes.
RestartApplications=no
SetupIconFile=favicon.ico
UninstallDisplayIcon={app}\LightBulb.exe
OutputDir=artifacts\Installer
OutputBaseFilename=LightBulb-{#AppDisplayVersion}-Setup
VersionInfoVersion={#AppFileVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription=LightBulb Extended Edition setup
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppFileVersion}
VersionInfoProductTextVersion={#AppDisplayVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\LightBulb.exe"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"

[Registry]
Root: HKLM; Subkey: "Software\Microsoft\Windows NT\CurrentVersion\ICM"; ValueType: dword; ValueName: "GdiICMGammaRange"; ValueData: "256"

[Run]
Filename: "{app}\LightBulb.exe"; Description: "{cm:LaunchProgram,{#StringChange(AppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent runasoriginaluser

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    { Remove the exact per-user HKCU Run value used by Settings -> Start with Windows. }
    if RegValueExists(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LightBulb') then
      RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'LightBulb');
  end;
end;
