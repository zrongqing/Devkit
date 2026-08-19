#ifndef AppVersion
  #error AppVersion must be provided by Package-Devkit.ps1
#endif
#ifndef VersionInfoVersion
  #error VersionInfoVersion must be provided by Package-Devkit.ps1
#endif
#ifndef SourceDir
  #error SourceDir must be provided by Package-Devkit.ps1
#endif
#ifndef OutputDir
  #error OutputDir must be provided by Package-Devkit.ps1
#endif
#ifndef OutputBaseFilename
  #error OutputBaseFilename must be provided by Package-Devkit.ps1
#endif

[Setup]
AppId={{D5D7A7CC-0C1E-4D31-9E2E-FD94B185840E}
AppName=Devkit
AppVersion={#AppVersion}
AppVerName=Devkit {#AppVersion}
AppPublisher=Devkit
DefaultDirName={localappdata}\Programs\Devkit
DefaultGroupName=Devkit
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir={#OutputDir}
OutputBaseFilename={#OutputBaseFilename}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
SetupLogging=yes
CloseApplications=yes
RestartApplications=no
UninstallDisplayIcon={app}\Devkit.exe
VersionInfoVersion={#VersionInfoVersion}
VersionInfoProductName=Devkit
VersionInfoProductVersion={#VersionInfoVersion}
VersionInfoDescription=Devkit Windows x64 installer

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "其他任务："; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Devkit"; Filename: "{app}\Devkit.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\Devkit"; Filename: "{app}\Devkit.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Devkit.exe"; Description: "启动 Devkit"; WorkingDir: "{app}"; Flags: nowait postinstall skipifsilent

[Code]
const
  DesktopRuntimeDownloadUrl = 'https://dotnet.microsoft.com/download/dotnet/10.0/runtime';

function HasDesktopRuntimeAt(const DotNetRoot: String): Boolean;
var
  RuntimeRoot: String;
  FindRec: TFindRec;
begin
  Result := False;
  if DotNetRoot = '' then
    Exit;

  RuntimeRoot := AddBackslash(DotNetRoot) + 'shared\Microsoft.WindowsDesktop.App';
  if FindFirst(AddBackslash(RuntimeRoot) + '10.*', FindRec) then
  begin
    try
      repeat
        if ((FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY) <> 0) and
           (Pos('10.', FindRec.Name) = 1) then
        begin
          Result := True;
          Exit;
        end;
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

function IsDesktopRuntimeInstalled(): Boolean;
begin
  Result :=
    HasDesktopRuntimeAt(ExpandConstant('{pf64}\dotnet')) or
    HasDesktopRuntimeAt(ExpandConstant('{localappdata}\Microsoft\dotnet')) or
    HasDesktopRuntimeAt(GetEnv('DOTNET_ROOT_X64')) or
    HasDesktopRuntimeAt(GetEnv('DOTNET_ROOT'));
end;

function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
begin
  Result := IsDesktopRuntimeInstalled();
  if Result then
    Exit;

  Log('Microsoft.WindowsDesktop.App 10.x x64 was not found.');
  if WizardSilent() then
    Exit;

  if MsgBox(
       '安装 Devkit 前必须先安装 .NET 10 Desktop Runtime x64。' + #13#10 + #13#10 +
       '是否打开微软官方下载页面？',
       mbError, MB_YESNO) = IDYES then
  begin
    ShellExec('open', DesktopRuntimeDownloadUrl, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
  end;
end;

