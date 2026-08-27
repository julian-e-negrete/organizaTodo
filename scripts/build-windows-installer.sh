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
