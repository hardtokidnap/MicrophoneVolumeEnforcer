[Setup]
AppId={{8B2E4F5A-9C3D-4E6F-8A1B-2C5D7E9F1A3B}
AppName=Microphone Volume Enforcer
AppVersion=2.1.0
AppVerName=Microphone Volume Enforcer 2.1.0
AppPublisher=Hardtokidnap
AppPublisherURL=https://github.com/hardtokidnap
AppSupportURL=https://github.com/hardtokidnap/MicrophoneVolumeEnforcer
AppUpdatesURL=https://github.com/hardtokidnap/MicrophoneVolumeEnforcer/releases
DefaultDirName={autopf}\MicrophoneVolumeEnforcer
DefaultGroupName=Microphone Volume Enforcer
AllowNoIcons=yes
LicenseFile=LICENSE.txt
InfoBeforeFile=README.md
OutputDir=installer
OutputBaseFilename=MicrophoneVolumeEnforcer-Setup
SetupIconFile=app.ico
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0.17763
UninstallDisplayIcon={app}\MicrophoneVolumeEnforcer.exe

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode
Name: "startupicon"; Description: "Launch at Windows startup"; GroupDescription: "Startup Options"; Flags: unchecked

[Files]
Source: "bin\Release\net8.0-windows\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; NOTE: Don't use "Flags: ignoreversion" on any shared system files

[Icons]
Name: "{group}\Microphone Volume Enforcer"; Filename: "{app}\MicrophoneVolumeEnforcer.exe"
Name: "{group}\{cm:UninstallProgram,Microphone Volume Enforcer}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Microphone Volume Enforcer"; Filename: "{app}\MicrophoneVolumeEnforcer.exe"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\Microphone Volume Enforcer"; Filename: "{app}\MicrophoneVolumeEnforcer.exe"; Tasks: quicklaunchicon

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "MicrophoneVolumeEnforcer"; ValueData: """{app}\MicrophoneVolumeEnforcer.exe"""; Flags: uninsdeletevalue; Tasks: startupicon

[Run]
Filename: "{app}\MicrophoneVolumeEnforcer.exe"; Description: "{cm:LaunchProgram,Microphone Volume Enforcer}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{userappdata}\MicrophoneVolumeEnforcer\settings.json"
Type: dirifempty; Name: "{userappdata}\MicrophoneVolumeEnforcer"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Clean up any existing startup entries if not selected
    if not IsTaskSelected('startupicon') then
    begin
      RegDeleteValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', 'MicrophoneVolumeEnforcer');
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then
  begin
    // Clean up startup registry entry
    RegDeleteValue(HKEY_CURRENT_USER, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Run', 'MicrophoneVolumeEnforcer');
    
    // Optionally ask if user wants to keep settings
    if MsgBox('Do you want to keep your application settings?', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDNO then
    begin
      DelTree(ExpandConstant('{userappdata}\MicrophoneVolumeEnforcer'), True, True, True);
    end;
  end;
end; 