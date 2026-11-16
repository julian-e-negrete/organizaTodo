# Desktop Installer Packaging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the already-working `OrganizaTodo.Desktop` app (built in the `2026-08-16-desktop-app-runtime` plan) into two real, offline, double-click installers — a Windows `.exe` and a Linux self-extracting `.run` — each fully self-contained: no .NET SDK, no PostgreSQL, no internet connection required on the target machine.

**Architecture:** Two independent, platform-specific artifacts, no cross-bundling. Each bundles: a self-contained single-file `dotnet publish` of `OrganizaTodo.Desktop` for that RID, a portable PostgreSQL binary tree vendored at build time (placed so it lands at `<install-dir>/pgbin/bin` — the exact path `Program.cs`'s `ResolvePgBinDirectory()` already falls back to when `ORGANIZATODO_PG_BIN_DIR` isn't set), and — Windows only — the WebView2 Fixed Version offline redistributable. Windows packaging uses Inno Setup; Linux packaging uses a `makeself` self-extracting shell archive.

**Tech Stack:** .NET 8 (`dotnet publish -r win-x64`/`-r linux-x64 --self-contained`), Inno Setup (compiled via Wine in this Linux dev environment), `makeself`, bash build scripts.

**Spec:** [docs/superpowers/specs/2026-08-16-self-hosted-offline-installer-design.md](../specs/2026-08-16-self-hosted-offline-installer-design.md) (`## Build & packaging` section — architecture already approved; this plan implements it, not redesigns it)

## Global Constraints

- Both installers must work with **zero network access** at install time and at first run — every dependency (runtime, Postgres, WebView2 on Windows) is vendored into the artifact itself.
- Vendored Postgres binaries must end up at `<install-dir>/pgbin/bin` (containing `initdb`, `postgres`, `pg_ctl`, `pg_ctl`'s sibling `lib`/`share` directories alongside `bin`, not just the `bin` executables alone — Postgres needs its `share/postgresql` data files at runtime) — this is the exact fallback path `src/OrganizaTodo.Desktop/Program.cs:52` already resolves to when `ORGANIZATODO_PG_BIN_DIR` isn't set. Do not change that fallback path; build the packaging around it.
- **This Linux sandbox cannot run a Windows executable.** Every Windows-side artifact in this plan (the published `win-x64` binary, the compiled `.exe` installer) can be *built* and *sanity-checked* here (file structure, Inno Setup script compiles cleanly under Wine) but **cannot be launched and functionally verified** here. That final check is the user's to do on a real Windows machine — say so explicitly in each Windows-facing task's acceptance criteria, don't claim more than what was actually verified.
- The Linux artifact (published `linux-x64` binary, the `.run` self-extracting installer) **can and must** be fully built AND functionally verified end-to-end in this environment — it has no such limitation.
- Follow the project's superpowers TDD/verification discipline: every task's acceptance criteria must be something you actually ran and observed, not asserted.

---

### Task 1: Cross-platform self-contained publish

**Files:**
- Create: `scripts/publish-desktop.sh`
- Modify: `src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj` (only if publish requires property changes discovered during this task — don't add anything speculative)

**Interfaces:**
- Produces: `publish/win-x64/` and `publish/linux-x64/` directories (gitignored build output, not committed), each a self-contained, single-file publish of `OrganizaTodo.Desktop` for that RID.

- [ ] **Step 1: Publish for `linux-x64` and verify it's genuinely self-contained**

```bash
dotnet publish src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish/linux-x64
```

Verify the output is self-contained (no dependency on a system-installed .NET runtime):

```bash
file publish/linux-x64/OrganizaTodo.Desktop
ls -la publish/linux-x64/
# Confirm Photino's native WebKitGTK interop .so lands in the output (check the
# actual filename via `dotnet nuget locals global-packages -l` -> Photino.NET
# package contents, since exact native asset names vary by package version).
```

Run it for real (this machine has a working X11 display and WebKitGTK, already proven earlier in this project):

```bash
mv ~/.local/share/organizatodo ~/.local/share/organizatodo.bak 2>/dev/null  # don't clobber real data
export ORGANIZATODO_PG_BIN_DIR=/home/haraidasan/.local/share/organizatodo-pgbin-dev/usr/lib/postgresql/16/bin
./publish/linux-x64/OrganizaTodo.Desktop &
sleep 15
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5157/Account/Login  # expect 200 once it's up (port may differ; check the process's own log/stdout for the actual bound port)
kill %1
mv ~/.local/share/organizatodo.bak ~/.local/share/organizatodo 2>/dev/null
```

- [ ] **Step 2: Publish for `win-x64` and sanity-check the output structure**

```bash
dotnet publish src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish/win-x64
```

This should succeed even cross-compiled from Linux (no native C/C++ compilation happens for this project — it's all managed .NET code plus prebuilt native assets NuGet already ships per-RID). Verify:

```bash
ls -la publish/win-x64/
file publish/win-x64/OrganizaTodo.Desktop.exe   # `file` should identify it as a PE32+ executable
```

If the build fails because a NuGet package (Photino.NET or a transitive dependency) doesn't ship win-x64 native assets when restored/published from a linux-x64 SDK, that's a real, concrete blocker — stop and report it (`BLOCKED`) rather than working around it with a fabricated fix; this needs to be understood, not guessed at.

You cannot run `OrganizaTodo.Desktop.exe` here. Do not claim it works — only that it built and the PE header is well-formed.

- [ ] **Step 3: Wrap both publish commands in a script**

`scripts/publish-desktop.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

for RID in linux-x64 win-x64; do
  echo "Publishing OrganizaTodo.Desktop for $RID..."
  dotnet publish src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj \
    -c Release -r "$RID" --self-contained true \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
    -o "publish/$RID"
done

echo "Published to publish/linux-x64/ and publish/win-x64/"
```

```bash
chmod +x scripts/publish-desktop.sh
```

- [ ] **Step 4: Add `publish/` to `.gitignore`**

Check `.gitignore` doesn't already cover it (`bin/`/`obj/` patterns likely don't match a top-level `publish/` dir); add `publish/` if missing.

- [ ] **Step 5: Commit**

```bash
git add scripts/publish-desktop.sh .gitignore
git commit -m "add cross-platform self-contained publish script for OrganizaTodo.Desktop"
```

---

### Task 2: Vendor portable PostgreSQL binaries for both RIDs

**Files:**
- Create: `scripts/vendor-postgres-linux.sh`
- Create: `scripts/vendor-postgres-windows.sh`
- Test: manual verification per platform (Step 2 for Linux is a real functional test; Windows is structural-only per Global Constraints)

**Interfaces:**
- Produces: `publish/linux-x64/pgbin/{bin,lib,share}/...` and `publish/win-x64/pgbin/{bin,lib,share}/...` — each a complete, runnable (on its own OS) portable Postgres tree.

- [ ] **Step 1: Vendor Linux Postgres binaries, reusing the exact recipe already proven in this repo's own dev setup**

This repo already has a working, tested example of exactly this: `/home/haraidasan/.local/share/organizatodo-pgbin-dev/` was built earlier in this project's history via `apt-get download` + `dpkg-deb -x` of four packages (`postgresql-16`, `postgresql-client-16`, `postgresql-common`, `libpq5`) — extracting all four together is what makes the binaries actually runnable (Postgres needs `libpq`'s shared library and `postgresql-common`'s support files, not just the `postgresql-16` package alone). Verify that reference tree still exists and still works before scripting against it:

```bash
ls /home/haraidasan/.local/share/organizatodo-pgbin-dev/usr/lib/postgresql/16/bin/
/home/haraidasan/.local/share/organizatodo-pgbin-dev/usr/lib/postgresql/16/bin/postgres --version
```

`scripts/vendor-postgres-linux.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="$REPO_ROOT/publish/linux-x64/pgbin"
SCRATCH="$(mktemp -d)"
trap 'rm -rf "$SCRATCH"' EXIT

echo "Downloading Postgres 16 .deb packages..."
cd "$SCRATCH"
apt-get download postgresql-16 postgresql-client-16 postgresql-common libpq5

echo "Extracting into $DEST..."
mkdir -p "$DEST"
for deb in *.deb; do
  dpkg-deb -x "$deb" "$DEST"
done

# The Debian layout scatters files across /usr/lib/postgresql/16/{bin,lib},
# /usr/share/postgresql/16, /usr/lib/x86_64-linux-gnu (libpq.so.5). Flatten
# into pgbin/{bin,lib,share} so PostgresManager's pgBinDirectory (pgbin/bin)
# finds everything it needs via the standard relative bin/../lib,
# bin/../share layout Postgres expects.
mkdir -p "$DEST/bin" "$DEST/lib" "$DEST/share"
cp -a "$DEST/usr/lib/postgresql/16/bin/." "$DEST/bin/"
cp -a "$DEST/usr/lib/postgresql/16/lib/." "$DEST/lib/" 2>/dev/null || true
cp -a "$DEST/usr/share/postgresql/16/." "$DEST/share/"
cp -a "$DEST/usr/lib/x86_64-linux-gnu/libpq.so"* "$DEST/lib/" 2>/dev/null || true
rm -rf "$DEST/usr"

echo "Vendored Postgres binaries at $DEST/bin"
"$DEST/bin/postgres" --version
```

- [ ] **Step 2: Run it and verify the vendored tree is actually usable — real functional test**

```bash
mkdir -p publish/linux-x64
./scripts/vendor-postgres-linux.sh
export ORGANIZATODO_PG_BIN_DIR="$PWD/publish/linux-x64/pgbin/bin"
```

Reuse the existing test infra to prove this vendored copy (not the pre-existing dev-only copy at `~/.local/share/organizatodo-pgbin-dev`) actually works end to end:

```bash
dotnet test tests/OrganizaTodo.Desktop.Tests --filter PostgresManagerIntegrationTests
```

Expected: PASS, using the freshly-vendored binaries — this is the real acceptance test for this step, not just "the script ran without error."

- [ ] **Step 3: Vendor Windows Postgres binaries (structural verification only — see Global Constraints)**

`scripts/vendor-postgres-windows.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DEST="$REPO_ROOT/publish/win-x64/pgbin"
SCRATCH="$(mktemp -d)"
trap 'rm -rf "$SCRATCH"' EXIT

# EnterpriseDB publishes zip archives of the Postgres Windows binaries with no
# installer, intended for embedding in another application's installer — see
# https://www.enterprisedb.com/download-postgresql-binaries. This URL was
# resolved and verified (via `curl -I`, confirmed `content-type: application/zip`,
# ~332MB) on 2026-08-26 — it points to PostgreSQL 16.15. EDB's download links
# are versioned and will eventually go stale; re-verify before relying on this
# for a new release (`curl -sIL https://sbp.enterprisedb.com/getfile.jsp?fileid=1260422`
# and follow the redirect) rather than assuming it's still current indefinitely.
POSTGRES_WINDOWS_ZIP_URL="https://get.enterprisedb.com/postgresql/postgresql-16.15-1-windows-x64-binaries.zip"

echo "Downloading Postgres Windows binaries..."
cd "$SCRATCH"
curl -fL -o postgres-windows.zip "$POSTGRES_WINDOWS_ZIP_URL"
unzip -q postgres-windows.zip

# EDB's zip extracts to a pgsql/ directory containing bin/, lib/, share/ already
# in the layout Postgres expects — copy that whole tree directly.
mkdir -p "$REPO_ROOT/publish/win-x64"
cp -a pgsql "$DEST"

echo "Vendored Postgres binaries at $DEST/bin"
ls "$DEST/bin"
```

`POSTGRES_WINDOWS_ZIP_URL` above is a real, already-verified value (not a placeholder) — confirmed live via `curl -sIL` during planning (see the comment above it). If this plan sits unimplemented long enough that PostgreSQL 16.15 is no longer current, re-verify with the same `curl -sIL` command against `https://sbp.enterprisedb.com/getfile.jsp?fileid=1260422` (or find the current fileid from `https://www.enterprisedb.com/download-postgresql-binaries` if that specific id stops working) before trusting it — don't assume it stays valid indefinitely.

Verify structurally (no execution possible here):

```bash
mkdir -p publish/win-x64
./scripts/vendor-postgres-windows.sh
ls publish/win-x64/pgbin/bin/   # expect postgres.exe, initdb.exe, pg_ctl.exe among the contents
file publish/win-x64/pgbin/bin/postgres.exe   # expect "PE32+ executable"
```

- [ ] **Step 4: Commit**

```bash
git add scripts/vendor-postgres-linux.sh scripts/vendor-postgres-windows.sh
git commit -m "add scripts to vendor portable Postgres binaries for both RIDs"
```

---

### Task 3: Bundle the WebView2 Fixed Version offline redistributable (Windows only)

**Files:**
- Modify: `src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj` (add a Windows-only `PackageReference`)
- Create: `scripts/vendor-webview2.sh` (fallback path only — see Step 1)

**Interfaces:**
- Produces: the Fixed Version WebView2 Runtime files present in `publish/win-x64/` automatically as part of `dotnet publish`, ready for the Inno Setup script (Task 4) to install alongside the app.

- [ ] **Step 1: Prefer the NuGet-packaged runtime over manually downloading a CAB file**

Manually downloading a WebView2 Fixed Version CAB requires driving a JavaScript version/architecture picker on Microsoft's page — there's no stable, predictable direct-download URL (confirmed during planning: Microsoft only ever keeps the latest 2 versions available, and the picker doesn't expose a plain URL). A community-maintained NuGet package, [`WebView2.Runtime.X64`](https://www.nuget.org/packages/WebView2.Runtime.X64/), wraps this exact Fixed Version runtime as a normal versioned dependency (confirmed during planning: 1.2M total downloads, updated within the last day, depends on Microsoft's own `Microsoft.Web.WebView2` package, 358 GitHub stars) — this fits how every other dependency in this project (Photino.NET, Dapper, Npgsql) is already managed, and its version is pinned in source control like the rest, rather than a hand-picked URL that silently goes stale.

Add it as a Windows-only package reference to `OrganizaTodo.Desktop.csproj`:

```xml
<ItemGroup Condition="'$(RuntimeIdentifier)' == 'win-x64'">
  <PackageReference Include="WebView2.Runtime.X64" Version="*" />
</ItemGroup>
```

Publish for `win-x64` and inspect the output for the runtime files this package drops in:

```bash
dotnet publish src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
find publish/win-x64 -iname "*webview2*" -o -iname "*msedgewebview2*"
```

If the package's output isn't something Inno Setup can cleanly install alongside the app (e.g. it needs to be run as its own setup step rather than just being present as files), read its actual `.nuspec`/content layout (`~/.nuget/packages/webview2.runtime.x64/`) to understand exactly what it ships and adjust Task 4's `[Run]` section to match — don't guess at the install mechanism, inspect the real package contents.

- [ ] **Step 2: Fallback — manual CAB download, only if Step 1 doesn't pan out**

If the NuGet package turns out to be unusable (wrong content shape, doesn't support silent/unattended install, or is pulled from NuGet), fall back to manually resolving the current Fixed Version download from `https://developer.microsoft.com/en-us/microsoft-edge/webview2/` (Fixed Version section) at implementation time via `WebFetch`/browser automation, and write `scripts/vendor-webview2.sh` to `curl` and `cabextract` it into `publish/win-x64/webview2-fixed/`, matching the structure of `scripts/vendor-postgres-linux.sh`. Only build this fallback if Step 1 is confirmed not to work — don't build both speculatively.

- [ ] **Step 3: Document what was and wasn't verified**

Cannot verify the WebView2 runtime actually installs/initializes on a real Windows machine from this Linux sandbox — note that limitation explicitly in the report, per Global Constraints. What CAN be verified here: the package restores, publishes without error, and its files are present in `publish/win-x64/` in a shape Task 4's Inno Setup script can reference.

- [ ] **Step 4: Commit**

```bash
git add src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj
# plus scripts/vendor-webview2.sh only if the Step 2 fallback was actually needed
git commit -m "bundle WebView2 Fixed Version runtime via NuGet for the Windows installer"
```

---

### Task 4: Inno Setup script for the Windows installer

**Files:**
- Create: `installer/windows/OrganizaTodo.iss`
- Create: `scripts/build-windows-installer.sh`

**Interfaces:**
- Produces: `installer/windows/OrganizaTodo.iss` (Inno Setup source script) and, once compiled, `dist/OrganizaTodo-Setup.exe`.

- [ ] **Step 1: Write the Inno Setup script**

`installer/windows/OrganizaTodo.iss`:

```ini
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
; `dotnet publish`, so one recursive copy covers everything. If Task 3 ended up
; using the manual-CAB fallback instead (a separate webview2-fixed\ folder,
; not merged into the publish output), add a second Source line here for it
; and a matching [Run] step below — check what Task 3 actually produced before
; assuming this single-line form is complete.
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent
```

Whether the WebView2 runtime needs an explicit `[Run]` install step (like the CAB-fallback form commented out above) or just needs its files present alongside the exe depends entirely on what Task 3 discovered about the NuGet package's actual content shape — resolve this when implementing Task 4, using Task 3's findings, not by guessing here.

- [ ] **Step 2: Write the build script**

`scripts/build-windows-installer.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if ! command -v wine >/dev/null; then
  echo "wine is required to compile the Inno Setup script on Linux (apt-get install wine)" >&2
  exit 1
fi

ISCC="$HOME/.wine/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe"
if [ ! -f "$ISCC" ]; then
  echo "Inno Setup isn't installed under Wine. Download the installer from" >&2
  echo "https://jrsoftware.org/isdl.php and run: wine innosetup-installer.exe" >&2
  exit 1
fi

mkdir -p dist
wine "$ISCC" installer/windows/OrganizaTodo.iss
echo "Built dist/OrganizaTodo-Setup.exe"
```

```bash
chmod +x scripts/build-windows-installer.sh
```

- [ ] **Step 3: Install Wine + Inno Setup in this sandbox and attempt a real compile**

```bash
sudo apt-get install -y wine
```

Download the Inno Setup installer (`https://jrsoftware.org/isdl.php` — confirm the current version link via `WebFetch` before running) and install it under Wine:

```bash
wine innosetup-installer.exe /VERYSILENT
```

Then attempt the actual build:

```bash
./scripts/build-windows-installer.sh
ls -la dist/OrganizaTodo-Setup.exe
file dist/OrganizaTodo-Setup.exe   # expect a PE32 executable
```

**If Wine or the Inno Setup installer fails to run in this sandbox** (real possibility — Wine under a constrained/sandboxed environment sometimes doesn't work), report this clearly as `DONE_WITH_CONCERNS` rather than fabricating a successful compile: the `.iss` script and build script are still real, correct deliverables even if this environment can't prove they compile — say exactly what was and wasn't verified.

- [ ] **Step 4: Commit**

```bash
git add installer/windows/OrganizaTodo.iss scripts/build-windows-installer.sh
git commit -m "add Inno Setup script and build script for the Windows installer"
```

---

### Task 5: Self-extracting Linux installer

**Files:**
- Create: `installer/linux/organizatodo.desktop`
- Create: `installer/linux/install.sh`
- Create: `scripts/build-linux-installer.sh`

**Interfaces:**
- Produces: `dist/OrganizaTodo-Linux-Installer.run`, a `makeself` self-extracting archive.

- [ ] **Step 1: Write the `.desktop` launcher entry**

`installer/linux/organizatodo.desktop`:

```ini
[Desktop Entry]
Type=Application
Name=OrganizaTodo
Comment=Personal finance tracker (offline desktop app)
Exec=%h/.local/share/organizatodo-app/OrganizaTodo.Desktop
Icon=%h/.local/share/organizatodo-app/icon.png
Terminal=false
Categories=Office;Finance;
```

Note: `%h` isn't expanded by `.desktop` file parsers (that's shell/systemd-unit syntax, not freedesktop `.desktop` syntax) — this needs to be rewritten with the literal absolute path at install time (`sed`-substituted in `install.sh`, Step 2), not shipped with a placeholder that silently never resolves. Flag this and fix it when implementing — don't ship the `%h` form as final.

- [ ] **Step 2: Write the installer's own extraction-time setup script**

`installer/linux/install.sh` (this is the script `makeself` runs after self-extracting the archive contents into a temp dir — it does the actual "install to a real location" work):

```bash
#!/usr/bin/env bash
set -euo pipefail

INSTALL_DIR="$HOME/.local/share/organizatodo-app"
APPLICATIONS_DIR="$HOME/.local/share/applications"

echo "Installing OrganizaTodo to $INSTALL_DIR..."
mkdir -p "$INSTALL_DIR"
cp -a ./payload/. "$INSTALL_DIR/"
chmod +x "$INSTALL_DIR/OrganizaTodo.Desktop"

mkdir -p "$APPLICATIONS_DIR"
sed "s#%h/.local/share/organizatodo-app#$INSTALL_DIR#g" ./organizatodo.desktop > "$APPLICATIONS_DIR/organizatodo.desktop"

echo "Installed. Launch OrganizaTodo from your applications menu, or run:"
echo "  $INSTALL_DIR/OrganizaTodo.Desktop"
```

- [ ] **Step 3: Write the build script wrapping `makeself`**

`scripts/build-linux-installer.sh`:

```bash
#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

if ! command -v makeself >/dev/null; then
  echo "makeself is required (apt-get install makeself)" >&2
  exit 1
fi

STAGE="$(mktemp -d)"
trap 'rm -rf "$STAGE"' EXIT

mkdir -p "$STAGE/payload"
cp -a publish/linux-x64/. "$STAGE/payload/"
cp installer/linux/organizatodo.desktop "$STAGE/"
cp installer/linux/install.sh "$STAGE/"
chmod +x "$STAGE/install.sh"

mkdir -p dist
makeself --gzip "$STAGE" dist/OrganizaTodo-Linux-Installer.run "OrganizaTodo Installer" ./install.sh

echo "Built dist/OrganizaTodo-Linux-Installer.run"
```

```bash
chmod +x scripts/build-linux-installer.sh
```

- [ ] **Step 4: Install `makeself`, build, and run the real installer — end-to-end, genuinely verifiable here**

```bash
sudo apt-get install -y makeself
./scripts/publish-desktop.sh          # if not already run in Task 1
./scripts/vendor-postgres-linux.sh    # if not already run in Task 2
./scripts/build-linux-installer.sh
ls -la dist/OrganizaTodo-Linux-Installer.run
```

Run the actual installer in a way that proves it works without touching your real `~/.local/share/organizatodo-app` or any pre-provisioned Postgres binaries — use a throwaway `HOME`:

```bash
export TEST_HOME="$(mktemp -d)"
HOME="$TEST_HOME" ./dist/OrganizaTodo-Linux-Installer.run
ls "$TEST_HOME/.local/share/organizatodo-app/"
cat "$TEST_HOME/.local/share/applications/organizatodo.desktop"   # confirm %h was substituted, not literal
```

- [ ] **Step 5: Commit**

```bash
git add installer/linux/organizatodo.desktop installer/linux/install.sh scripts/build-linux-installer.sh
git commit -m "add self-extracting Linux installer via makeself"
```

---

### Task 6: End-to-end offline verification (Linux — the platform this sandbox can actually prove)

**Files:** None created — this task is pure verification, tying Tasks 1, 2, and 5 together into one real proof.

- [ ] **Step 1: Run the built Linux installer in a fully isolated environment and launch the real app — no pre-provisioned Postgres, no dev environment variables**

```bash
export TEST_HOME="$(mktemp -d)"
unset ORGANIZATODO_PG_BIN_DIR   # the whole point: prove the vendored pgbin/ is what gets used, not a dev fallback
HOME="$TEST_HOME" ./dist/OrganizaTodo-Linux-Installer.run
HOME="$TEST_HOME" "$TEST_HOME/.local/share/organizatodo-app/OrganizaTodo.Desktop" &
APP_PID=$!
sleep 15
```

- [ ] **Step 2: Verify real end-to-end functionality**

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:5157/Account/Login   # expect 200; check actual bound port from the app's own log if different
ps aux | grep -v grep | grep "$TEST_HOME.*postgres"   # confirm postgres is running FROM the vendored pgbin path, not /usr or the dev-provisioned copy
```

- [ ] **Step 3: Confirm clean shutdown and no orphaned processes**

```bash
kill "$APP_PID"
sleep 2
ps aux | grep -v grep | grep "$TEST_HOME.*postgres"   # expect no output — postgres should have stopped with the app
rm -rf "$TEST_HOME"
```

- [ ] **Step 4: Write up the verification result**

No commit needed for this task (nothing new to add to git) — but write the final task report clearly stating: Linux end-to-end offline operation is **fully verified** in this sandbox (real Postgres from vendored binaries, real HTTP 200 from Kestrel, clean shutdown with no orphaned processes). Windows end-to-end operation is **not verified** — only structural/build-level checks were possible per this plan's Global Constraints; the user needs to run `dist\OrganizaTodo-Setup.exe` on a real Windows machine before calling this plan fully done.

---

## What this plan deliberately does not cover

- Code-signing either installer (both will trigger OS/browser "unknown publisher" warnings — Windows SmartScreen, Linux has no analogous concept for a `.run` file but some distros warn on unsigned/unfamiliar binaries). Out of scope; a follow-up if it matters.
- Auto-update — matches the original design spec's non-goals (reinstall via a new installer for now).
- macOS packaging — never in scope for this project.
