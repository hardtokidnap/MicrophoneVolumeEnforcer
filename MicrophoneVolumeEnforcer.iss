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
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
UninstallDisplayIcon={app}\MicrophoneVolumeEnforcer.exe

[Messages]
WelcomeLabel2=This will install [name/ver] on your computer.%n%nIMPORTANT: This application requires .NET 8.0 Runtime and WebView2 Runtime. The installer will check for these components and guide you through installation if needed.%n%nIt is recommended that you close all other applications before continuing.

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
function IsDotNet8Installed(): Boolean;
var
  ResultCode: Integer;
begin
  // Check if .NET 8.0 Runtime is installed using dotnet --list-runtimes
  Result := Exec('cmd', '/c dotnet --list-runtimes | findstr "Microsoft.WindowsDesktop.App 8."', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

function IsWebView2Installed(): Boolean;
var
  Version: String;
begin
  // Check if WebView2 is installed by looking for the registry key
  Result := RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version) or
            RegQueryStringValue(HKEY_LOCAL_MACHINE, 'SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}', 'pv', Version);
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
  MissingComponents: String;
  Response: Integer;
begin
  Result := True;
  MissingComponents := '';
  
  // Check for .NET 8.0 Runtime
  if not IsDotNet8Installed() then
  begin
    MissingComponents := MissingComponents + '• .NET 8.0 Desktop Runtime' + #13#10;
  end;
  
  // Check for WebView2 Runtime  
  if not IsWebView2Installed() then
  begin
    MissingComponents := MissingComponents + '• Microsoft Edge WebView2 Runtime' + #13#10;
  end;
  
  // If components are missing, show dialog
  if MissingComponents <> '' then
  begin
    Response := MsgBox('The following required components are missing:' + #13#10#13#10 + 
                      MissingComponents + #13#10 + 
                      'Would you like to download and install them now?' + #13#10#13#10 +
                      'Click Yes to open download pages, No to continue anyway, or Cancel to exit.',
                      mbConfirmation, MB_YESNOCANCEL);
    
    case Response of
      IDYES:
        begin
          // Open download pages
          if not IsDotNet8Installed() then
          begin
            ShellExec('open', 'https://dotnet.microsoft.com/en-us/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
          end;
          
          if not IsWebView2Installed() then
          begin
            ShellExec('open', 'https://developer.microsoft.com/en-us/microsoft-edge/webview2/', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
          end;
          
          MsgBox('Please install the required components and run this installer again.', mbInformation, MB_OK);
          Result := False; // Exit installer
        end;
      IDNO:
        begin
          // Continue with installation but warn user
          MsgBox('Installation will continue, but the application may not work correctly without the required components.', mbInformation, MB_OK);
        end;
      IDCANCEL:
        Result := False; // Exit installer
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    // Clean up any existing startup entries if not selected
    if not WizardIsTaskSelected('startupicon') then
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