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
# --nox11: this is a *build-time* makeself flag, not just a runtime one — it
# bakes nox11=y into the generated archive's header. Without it, makeself's
# default runtime behavior on any invocation where stdout isn't a TTY but
# $DISPLAY is set (e.g. double-clicking the .run file from a GUI file
# manager, or running it from a non-interactive script/CI with an X session
# present — verified for real against this exact archive) tries to relaunch
# itself inside a spawned terminal via `exec gnome-terminal -e "..."`. Modern
# gnome-terminal has deprecated -e and silently no-ops it, so the installer
# exits 0 having done nothing — no error, no install, no indication anything
# went wrong. Baking --nox11 in at build time makes the shipped installer run
# install.sh directly and reliably regardless of how the end user launches it.
makeself --gzip --nox11 "$STAGE" dist/OrganizaTodo-Linux-Installer.run "OrganizaTodo Installer" ./install.sh

echo "Built dist/OrganizaTodo-Linux-Installer.run"
