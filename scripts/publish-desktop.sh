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

# The WebView2.Runtime.X64 package (see OrganizaTodo.Desktop.csproj) delivers
# the Fixed Version runtime via NuGet's contentFiles convention rather than a
# .targets file. `dotnet publish` copies those content files to the ordinary
# build output (bin/.../win-x64/WebView2/, ~506MB, includes
# msedgewebview2.exe and msedge.dll) but silently drops the large binaries
# when producing the publish tree — publish/win-x64/WebView2/ ends up with
# only ~548KB of small metadata/.sig files, no msedgewebview2.exe or
# msedge.dll. Discovered during this plan's final whole-branch review: the
# earlier task-scoped check only confirmed the WebView2/ folder existed, not
# that it was complete. Since dotnet publish always builds first, the
# complete runtime is already sitting in bin/ right after the loop above —
# copy it over the incomplete publish output rather than fight the NuGet
# package's publish-time content behavior.
BIN_WEBVIEW2="src/OrganizaTodo.Desktop/bin/Release/net8.0/win-x64/WebView2"
if [ ! -f "$BIN_WEBVIEW2/msedgewebview2.exe" ]; then
  echo "ERROR: $BIN_WEBVIEW2/msedgewebview2.exe not found after build — WebView2.Runtime.X64 package not resolved correctly." >&2
  exit 1
fi
rm -rf "publish/win-x64/WebView2"
cp -a "$BIN_WEBVIEW2" "publish/win-x64/WebView2"
for f in msedgewebview2.exe msedge.dll; do
  if [ ! -f "publish/win-x64/WebView2/$f" ]; then
    echo "ERROR: publish/win-x64/WebView2/$f missing after copy — WebView2 runtime is incomplete." >&2
    exit 1
  fi
done
echo "WebView2 runtime completeness verified in publish/win-x64/WebView2/ ($(du -sh publish/win-x64/WebView2 | cut -f1))"

echo "Published to publish/linux-x64/ and publish/win-x64/"
