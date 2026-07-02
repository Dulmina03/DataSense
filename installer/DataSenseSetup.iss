; DataSense Installer Script
; Created with Inno Setup 6

#define MyAppName "DataSense"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "DataSense"
#define MyAppExeName "DataSense.UI.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=output
OutputBaseFilename=DataSenseSetup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Main application files from publish folder
Source: "..\publish\DataSense.UI.exe"; DestDir: "{app}"; Flags: ignoreversion
; Npcap installer (bundled for offline install)
Source: "npcap-installer.exe"; DestDir: "{tmp}"; Flags: deleteafterinstall

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
; Launch app after install
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
function NpcapInstalled: Boolean;
begin
  Result := DirExists(ExpandConstant('{sys}\Npcap')) or DirExists(ExpandConstant('{syswow64}\Npcap'));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
  NpcapPath: String;
begin
  Result := '';
  
  if not NpcapInstalled then
  begin
    NpcapPath := ExpandConstant('{tmp}\npcap-installer.exe');
    
    // Extract the Npcap installer to temp first
    ExtractTemporaryFile('npcap-installer.exe');
    
    MsgBox('The Npcap network driver is required for DataSense to work.' + #13#10 + #13#10 + 
           'The Npcap installer will now open.' + #13#10 +
           'Please complete the Npcap installation, then DataSense setup will continue.' + #13#10 + #13#10 +
           'IMPORTANT: Make sure to check "Install in WinPcap API-compatible Mode" during Npcap setup.',
           mbInformation, MB_OK);
    
    // Run Npcap installer and wait for it to finish
    if not ShellExec('', NpcapPath, '', '', SW_SHOW, ewWaitUntilTerminated, ResultCode) then
    begin
      Result := 'Failed to launch the Npcap installer. Please install Npcap manually from https://npcap.com';
    end
    else
    begin
      // Verify Npcap installed successfully
      if not NpcapInstalled then
      begin
        if MsgBox('Npcap does not appear to be installed.' + #13#10 + #13#10 +
                   'DataSense requires Npcap to monitor network traffic.' + #13#10 +
                   'Do you want to continue installing DataSense anyway?' + #13#10 +
                   '(You can install Npcap manually later from https://npcap.com)',
                   mbConfirmation, MB_YESNO) = IDNO then
        begin
          Result := 'Installation cancelled. Please install Npcap first from https://npcap.com';
        end;
      end;
    end;
  end;
end;
