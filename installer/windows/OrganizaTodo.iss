; OrganizaTodo Windows installer — Inno Setup script.
; Wraps the self-contained win-x64 publish (Task 1), vendored Postgres
; binaries (Task 2), and the WebView2 Fixed Version runtime (Task 3) into a
; single offline installer. No service registration — nothing runs until the
; user launches the app (per the approved design spec).

#define MyAppName "OrganizaTodo"
#define MyAppVersion "1.0.0"
#define MyAppExeName "OrganizaTodo.Desktop.exe"
#define PublishDir "..\..\publish\win-x64"

[Setup]
AppId={{B6F2C9A1-6E4A-4A3B-9C1D-9F1E5A7C2B44}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\..\dist
OutputBaseFilename=OrganizaTodo-Setup
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=lowest

[Files]
; The self-contained publish, including the vendored pgbin\ subtree from Task 2
; and (via the WebView2.Runtime.X64 NuGet package, Task 3) the WebView2 Fixed
; Version runtime files — all of it lands directly under {#PublishDir} from
; `dotnet publish`, so one recursive copy covers everything. Verified against
; the actual publish/win-x64/ output (Task 4): OrganizaTodo.Desktop.exe, Database\,
; pgbin\ (Task 2's vendored Postgres 16 binaries), runtimes\, WebView2\ (Task 3's
; NuGet-sourced Fixed Version runtime, landing directly here — no separate
; webview2-fixed\ folder was produced), and wwwroot\ are all present as direct
; children of {#PublishDir}, so this single recursive copy is complete.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
