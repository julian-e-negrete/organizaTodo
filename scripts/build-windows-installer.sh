#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT"

# WINE_BIN / ISCC_PATH: override these to point at an alternate Wine + Inno Setup
# install. Defaults assume the conventional path: `apt-get install wine`, then
# `wine innosetup-installer.exe` (installs to the default $HOME/.wine prefix). A
# real Windows machine or CI runner doesn't need this script's Wine layer at all —
# just run ISCC.exe directly.
#
# Sandbox-without-sudo recipe (used to develop and verify this script): some
# environments have no working `sudo` (`apt-get install wine` is then impossible)
# but do have a pre-existing Wine runtime bundled inside an unrelated snap package
# (e.g. the `notepad-plus-plus` snap ships its own Wine 11.0 under
# `/snap/bin/notepad-plus-plus.wine`, backed by a real, already-initialized 64-bit
# prefix at `$HOME/snap/notepad-plus-plus/common/.wine`). That Wine binary works
# for running arbitrary Windows .exes (not just Notepad++) when invoked as
# `notepad-plus-plus.wine <path-to-exe> <args>` — its wrapper script treats the
# first argument as the target executable. Steps that were run once, manually, to
# get Inno Setup installed into that prefix (not part of this script — a one-time
# setup step, same as a normal Wine + Inno Setup install would be):
#   1. Download the real Inno Setup installer (confirm current version at
#      https://jrsoftware.org/isdl.php first):
#        curl -fL -o ~/innosetup-installer.exe \
#          https://github.com/jrsoftware/issrc/releases/download/is-6_7_3/innosetup-6.7.3.exe
#      (must be copied under $HOME, not /tmp — the snap's confinement only grants
#      access to the user's home directory)
#   2. Install it silently into the snap's Wine prefix:
#        /snap/bin/notepad-plus-plus.wine ~/innosetup-installer.exe \
#          /VERYSILENT /SUPPRESSMSGBOXES /NORESTART
#      This lands ISCC.exe at:
#        $HOME/snap/notepad-plus-plus/common/.wine/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe
# Once that one-time setup is done, this script can be pointed at it on every
# subsequent run via:
#   WINE_BIN=/snap/bin/notepad-plus-plus.wine \
#   ISCC_PATH="$HOME/snap/notepad-plus-plus/common/.wine/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe" \
#   ./scripts/build-windows-installer.sh
# (Do not try to invoke the snap's underlying wine-platform binary directly,
# bypassing its wrapper script — it hangs indefinitely on `wineboot --init`,
# apparently missing environment/library setup the wrapper normally provides.)
WINE_BIN="${WINE_BIN:-wine}"
ISCC_PATH="${ISCC_PATH:-$HOME/.wine/drive_c/Program Files (x86)/Inno Setup 6/ISCC.exe}"

if ! command -v "$WINE_BIN" >/dev/null; then
  echo "wine is required to compile the Inno Setup script on Linux (apt-get install wine)," >&2
  echo "or set WINE_BIN to an alternate Wine binary (see comments in this script)." >&2
  exit 1
fi

if [ ! -f "$ISCC_PATH" ]; then
  echo "Inno Setup isn't installed under Wine. Download the installer from" >&2
  echo "https://jrsoftware.org/isdl.php and run: $WINE_BIN innosetup-installer.exe" >&2
  echo "(or set ISCC_PATH to an alternate ISCC.exe location, see comments in this script)" >&2
  exit 1
fi

# Always republish immediately before packaging (same reasoning as
# build-linux-installer.sh's equivalent step): a stale publish/win-x64/
# would otherwise ship silently, with no error. This also re-verifies the
# WebView2.Runtime.X64 package's large runtime binaries actually survived
# `dotnet publish` (see scripts/publish-desktop.sh for the completeness
# check and why it's needed — that package's content files get silently
# dropped from the publish tree, though not the build tree, by the
# .NET SDK's NuGet contentFiles handling).
echo "Publishing OrganizaTodo.Desktop for win-x64..."
dotnet publish "$REPO_ROOT/src/OrganizaTodo.Desktop/OrganizaTodo.Desktop.csproj" \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$REPO_ROOT/publish/win-x64"

BIN_WEBVIEW2="$REPO_ROOT/src/OrganizaTodo.Desktop/bin/Release/net8.0/win-x64/WebView2"
if [ ! -f "$BIN_WEBVIEW2/msedgewebview2.exe" ]; then
  echo "ERROR: $BIN_WEBVIEW2/msedgewebview2.exe not found after build — WebView2.Runtime.X64 package not resolved correctly." >&2
  exit 1
fi
rm -rf "$REPO_ROOT/publish/win-x64/WebView2"
cp -a "$BIN_WEBVIEW2" "$REPO_ROOT/publish/win-x64/WebView2"
for f in msedgewebview2.exe msedge.dll; do
  if [ ! -f "$REPO_ROOT/publish/win-x64/WebView2/$f" ]; then
    echo "ERROR: publish/win-x64/WebView2/$f missing after copy — WebView2 runtime is incomplete." >&2
    exit 1
  fi
done
echo "WebView2 runtime completeness verified in publish/win-x64/WebView2/"

mkdir -p dist
"$WINE_BIN" "$ISCC_PATH" installer/windows/OrganizaTodo.iss
echo "Built dist/OrganizaTodo-Setup.exe"
