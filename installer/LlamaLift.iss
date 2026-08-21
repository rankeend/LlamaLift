#define MyAppName "LlamaLift"
#define MyAppVersion "1.1.0"
#define MyAppChannel "preview"
#define MyAppPublisher "RankeeNd-Masen Hu"
#define MyAppExeName "LlamaLift.exe"
#define MyUpgradeAppId "{{BDE1C8B1-4E9B-4F54-B2A7-7B82B7DF42A0}"

[Setup]
; MyUpgradeAppId is a permanent upgrade identity. Never change it between releases.
AppId={#MyUpgradeAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion} Preview
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/rankeend/LlamaLift
AppSupportURL=https://github.com/rankeend/LlamaLift/issues
AppUpdatesURL=https://github.com/rankeend/LlamaLift/releases
DefaultDirName={autopf}\LlamaLift
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\release
OutputBaseFilename=LlamaLift-v{#MyAppVersion}-{#MyAppChannel}-Setup
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppName}
SetupIconFile=..\assets\LlamaServerManager-llama-icon-v2.ico
VersionInfoVersion={#MyAppVersion}
VersionInfoProductName={#MyAppName}
VersionInfoDescription=本地模型，一键起飞。
SetupLogging=yes
; One package handles both clean installs and in-place upgrades.
UsePreviousAppDir=yes
UsePreviousGroup=yes
UsePreviousTasks=yes
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加快捷方式："; Flags: unchecked

[Files]
; The installed build never contains portable.flag, so configuration remains in
; %LOCALAPPDATA%\LlamaLift and is not overwritten or removed during upgrades.
Source: "..\dist-installer\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
