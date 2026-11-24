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

# NOTE: an earlier version of this script flattened the Debian package layout
# (which scatters files across /usr/lib/postgresql/16/{bin,lib},
# /usr/share/postgresql/16, and /usr/lib/x86_64-linux-gnu) into a new
# pgbin/{bin,lib,share} sibling structure. That was reverted after a real
# functional test (PostgresManagerIntegrationTests run against the
# freshly-vendored binaries) failed with:
#
#   initdb: error: file "/usr/share/postgresql/16/postgres.bki" does not exist
#
# Debian's postgres binaries are compiled with fixed bindir/datadir paths and
# only support relocation when the ORIGINAL nested directory structure
# (usr/lib/postgresql/16/bin, usr/share/postgresql/16, etc.) is preserved
# intact under a new root — confirmed working via the same functional test
# run against the dev-provisioned bundle at
# ~/.local/share/organizatodo-pgbin-dev, which keeps this exact nesting.
# So: leave the dpkg-deb -x output exactly as extracted under $DEST/usr/...
# — do not flatten it. Program.cs's ResolvePgBinDirectory() fallback points
# at this same usr/lib/postgresql/16/bin path.
echo "Vendored Postgres binaries at $DEST/usr/lib/postgresql/16/bin"
"$DEST/usr/lib/postgresql/16/bin/postgres" --version
