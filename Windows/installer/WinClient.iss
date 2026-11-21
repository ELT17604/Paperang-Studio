#ifndef AppVersion
#define AppVersion "0.3.3-winclient-dev5"
#endif
[Setup]
AppId={{B9251EAC-830D-4DF6-BD30-A4FC8BD4A7DC}
AppName=Paperang Studio WinClient
AppVersion={#AppVersion}
VersionInfoVersion=0.3.3.0
AppPublisher=ELT Group
AppPublisherURL=https://github.com/ELT17604/Paperang-Studio
DefaultDirName={localappdata}\Programs\Paperang Studio
DefaultGroupName=Paperang Studio
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputPath}
OutputBaseFilename=PaperangStudio-{#AppVersion}-win-x64-setup
SetupIconFile=..\..\assets\AppIcon.ico
UninstallDisplayIcon={app}\Paperang.WinClient.exe
LicenseFile=..\..\LICENSE
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
[Icons]
Name: "{group}\Paperang Studio"; Filename: "{app}\Paperang.WinClient.exe"
Name: "{group}\卸载 Paperang Studio"; Filename: "{uninstallexe}"
[InstallDelete]
Type: files; Name: "{app}\WINCLIENT.md"
Type: files; Name: "{app}\WINCLIENT.zh-CN.md"
Type: files; Name: "{app}\WINDOWS.md"
Type: files; Name: "{app}\WINDOWS.zh-CN.md"
Type: files; Name: "{app}\MAC-WINCLIENT-CONTROLS.zh-CN.md"
[Run]
Filename: "{app}\Paperang.WinClient.exe"; Description: "启动 Paperang Studio"; Flags: nowait postinstall skipifsilent
[Code]
function StopClient(): Boolean;
var ExitCode, Attempt: Integer;
begin
  if FileExists(ExpandConstant('{app}\Paperang.WinClient.exe')) then
    Exec(ExpandConstant('{app}\Paperang.WinClient.exe'), '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  for Attempt := 1 to 100 do begin
    if not CheckForMutexes('Local\ELT.PaperangStudio.WinLite') then begin Result := True; Exit; end;
    Sleep(100);
  end;
  Result := False;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopClient() then Result := 'Paperang Studio 正在运行，请退出客户端后重试。';
end;
function InitializeUninstall(): Boolean;
begin
  Result := StopClient();
  if not Result and not UninstallSilent then MsgBox('请先退出 Paperang Studio，再重试卸载。', mbError, MB_OK);
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var Command: String;
begin
  if CurUninstallStep = usPostUninstall then begin
    if RegQueryStringValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PaperangStudioWinLite', Command) then
      if CompareText(Command, '"' + ExpandConstant('{app}\Paperang.WinClient.exe') + '" --background') = 0 then
        RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PaperangStudioWinLite');
  end;
end;
