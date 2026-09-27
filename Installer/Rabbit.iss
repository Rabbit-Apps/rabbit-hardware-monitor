#ifndef PublishDir
  #define PublishDir "..\dist\Rabbit"
#endif
#define AppVersion "0.2.0"

[Setup]
SetupArchitecture=x64
AppId={{DB3CDDAF-9523-4E9E-A2FB-5066B01114F0}
AppName=Rabbit Hardware Monitor
AppVersion={#AppVersion}
AppPublisher=Rabbit Apps
AppPublisherURL=https://github.com/Rabbit-Apps/rabbit-hardware-monitor
DefaultDirName={autopf}\Rabbit Hardware Monitor
UsePreviousAppDir=no
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\dist\Installer
OutputBaseFilename=RabbitHardwareMonitor-{#AppVersion}-Setup
SetupIconFile=..\App\Assets\RabbitIcon.ico
UninstallDisplayIcon={app}\HardwareMonitor.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
LicenseFile=..\LICENSE.md
InfoBeforeFile=README.txt
SetupLogging=yes

[Tasks]
Name: "autostart"; Description: "Start Rabbit Hardware Monitor when this administrator account signs in"; Flags: checkedonce
Name: "desktopicon"; Description: "Create a desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

Source: "Configure-Startup.ps1"; DestDir: "{app}\Installer"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\Rabbit Hardware Monitor"; Filename: "{app}\HardwareMonitor.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Rabbit Hardware Monitor"; Filename: "{app}\HardwareMonitor.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[UninstallDelete]
Type: files; Name: "{app}\Installer\startup-result.log"

[Code]
var
  StartupFailed: Boolean;

procedure ConfigureStartup(Mode: string);
var
  Code: Integer;
begin
  if not Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -File "' +
    ExpandConstant('{app}\Installer\Configure-Startup.ps1') + '" -Mode ' + Mode,
    ExpandConstant('{app}'), SW_HIDE, ewWaitUntilTerminated, Code) then
    RaiseException('Could not start startup configuration.');
  if Code <> 0 then
    RaiseException('Startup configuration failed. See Installer\startup-result.log in the installation folder.');
end;

function PrepareToInstall(var NeedsRestart: Boolean): string;
begin
  Result := '';
  { Never permit an elevated startup task to point into an arbitrary /DIR location. }
  if CompareText(RemoveBackslash(ExpandConstant('{app}')),
    RemoveBackslash(ExpandConstant('{autopf}\Rabbit Hardware Monitor'))) <> 0 then
    Result := 'Install Rabbit Hardware Monitor in its default Program Files location.';
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then begin
    try
      if WizardIsTaskSelected('autostart') then ConfigureStartup('Enable')
      else ConfigureStartup('Disable');
    except
      StartupFailed := True;
      Log('Startup configuration failed: ' + GetExceptionMessage);
      SuppressibleMsgBox('Rabbit was installed, but startup could not be configured. ' + GetExceptionMessage,
        mbError, MB_OK, IDOK);
    end;
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  if StartupFailed then Result := 10 else Result := 0;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then begin
    try ConfigureStartup('Disable'); except
      SuppressibleMsgBox(GetExceptionMessage, mbError, MB_OK, IDOK);
      Abort;
    end;
  end;
end;

