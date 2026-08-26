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
