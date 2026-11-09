#ifndef AppVersion
#define AppVersion "0.3.2-winlite"
#endif
[Setup]
AppId={{BF30D1A7-D03B-434C-A51B-718507439EC8}
AppName=Paperang Studio WinLite
AppVersion={#AppVersion}
VersionInfoVersion=0.3.2.0
AppPublisher=ELT Group
AppPublisherURL=https://github.com/ELT17604/Paperang-Studio
DefaultDirName={localappdata}\Programs\Paperang Studio WinLite
DefaultGroupName=Paperang Studio WinLite
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#OutputPath}
OutputBaseFilename=PaperangStudio-{#AppVersion}-win-x64-setup
SetupIconFile=..\..\assets\AppIcon.ico
UninstallDisplayIcon={app}\PaperangStudio.exe
LicenseFile=..\..\LICENSE
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
[Files]
Source: "{#Payload}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs
[Icons]
Name: "{group}\Paperang Studio WinLite"; Filename: "{app}\PaperangStudio.exe"
Name: "{group}\卸载 Paperang Studio WinLite"; Filename: "{uninstallexe}"
[Run]
Filename: "{app}\PaperangStudio.exe"; Parameters: "--background"; Description: "启动 Paperang Studio WinLite"; Flags: nowait postinstall skipifsilent
[UninstallRun]
Filename: "{app}\PaperangStudio.exe"; Parameters: "--shutdown"; Flags: runhidden waituntilterminated; RunOnceId: "StopWinLite"
[Code]
var PurgeData: Boolean;
function StopClient(): Boolean;
var ExitCode, Attempt: Integer;
begin
  if FileExists(ExpandConstant('{app}\PaperangStudio.exe')) then
    Exec(ExpandConstant('{app}\PaperangStudio.exe'), '--shutdown', '', SW_HIDE, ewWaitUntilTerminated, ExitCode);
  for Attempt := 1 to 100 do begin
    if not CheckForMutexes('Local\ELT.PaperangStudio.WinLite') then begin Result := True; Exit; end;
    Sleep(100);
  end;
  Result := False;
end;
function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not StopClient() then Result := '请先从托盘退出 Paperang Studio，再重试安装。';
end;
function InitializeUninstall(): Boolean;
begin
  Result := StopClient();
  if not Result then begin
    if not UninstallSilent then MsgBox('请先从托盘退出 Paperang Studio，再重试卸载。', mbError, MB_OK);
    Exit;
  end;
  PurgeData := False;
  if not UninstallSilent then
    PurgeData := MsgBox('是否同时永久删除本机配置、模板、待回报记录和本程序数据目录中的所有者私钥？选择“否”可保留数据以便重新安装。外部路径的私钥不会删除。', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES;
end;
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usPostUninstall then begin
    RegDeleteValue(HKCU, 'Software\Microsoft\Windows\CurrentVersion\Run', 'PaperangStudioWinLite');
    if PurgeData then DelTree(ExpandConstant('{localappdata}\ELT\PaperangStudioWinLite'), True, True, True);
  end;
end;
