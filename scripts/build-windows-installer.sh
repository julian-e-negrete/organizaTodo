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

mkdir -p dist
"$WINE_BIN" "$ISCC_PATH" installer/windows/OrganizaTodo.iss
echo "Built dist/OrganizaTodo-Setup.exe"
