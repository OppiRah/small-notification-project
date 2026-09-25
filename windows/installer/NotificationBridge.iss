; Inno Setup script for the Notification Bridge Windows app. Build with build-installer.ps1, which
; publishes the app self-contained first and passes the publish folder in as PublishDir.

#define AppName "Notification Bridge"
#define AppVersion "0.1.0"
#define AppExe "NotificationBridge.Windows.exe"
#ifndef PublishDir
  #define PublishDir "publish"
#endif

[Setup]
AppId={{BDF074D9-62E9-4CBB-8880-0F232EC2F58D}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppName}
; Per-user install (no admin prompt): with PrivilegesRequired=lowest, {autopf} resolves to
; %LOCALAPPDATA%\Programs. This matches the app's per-user autostart and per-user data.
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
OutputDir=..\..\dist
OutputBaseFilename=NotificationBridge-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#AppExe}
; Close a running copy during an upgrade so its files can be replaced.
CloseApplications=yes

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; The app itself writes this value when "Start with Windows" is ticked. Removing it on uninstall
; stops Windows trying to launch a deleted program at every sign-in.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: none; ValueName: "NotificationBridge"; Flags: dontcreatekey uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

[Code]
// Pairing data (trusted devices, the TLS certificate) and settings live outside the install folder,
// so upgrades keep the phone paired. On an interactive uninstall, offer to delete them too: they
// include the shared secrets for paired phones. A silent uninstall must never delete them: Inno
// Setup auto-answers MsgBox with "Yes" in silent mode, which would silently wipe the pairing.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataDir := ExpandConstant('{localappdata}\NotificationBridge');
    if (not UninstallSilent()) and DirExists(DataDir) then
    begin
      if MsgBox('Also delete your pairing data and settings?' + #13#10 + #13#10 +
                'Choose No to keep them, so your phone stays paired if you reinstall.',
                mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(DataDir, True, True, True);
    end;
  end;
end;
