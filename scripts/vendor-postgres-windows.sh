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
