#!/usr/bin/env bash
# Self-contained single-file publish of the desktop app for one RID, staged for packaging:
#   <outdir>/DevAcademy.Desktop[.exe] (+ native libs), content/, THIRD-PARTY-NOTICES.md, LICENSE
# Usage: packaging/publish.sh <rid> [outdir]     (default outdir: artifacts/publish/<rid>)
set -euo pipefail
rid="${1:?usage: publish.sh <rid> [outdir]}"
root="$(cd "$(dirname "$0")/.." && pwd)"
out="${2:-$root/artifacts/publish/$rid}"
rm -rf "$out"
dotnet publish "$root/src/DevAcademy.Desktop" -c Release -r "$rid" --self-contained true \
  -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o "$out" ${DEVACADEMY_PUBLISH_ARGS:-}
# The csproj already copies content/ with the build output; replace it with the authoritative tree (minus schema/).
rm -rf "$out/content"
mkdir -p "$out/content"
(cd "$root/content" && tar --exclude=./schema -cf - .) | (cd "$out/content" && tar -xf -)
cp "$root/THIRD-PARTY-NOTICES.md" "$root/LICENSE" "$out/"
# Pinned npm manifest for course packages (React/Vitest); NodeEnvironmentService looks for node-content/ next to the
# executable and installs into the app data dir on explicit user action. Only the manifest ships, never node_modules.
mkdir -p "$out/node-content"
cp "$root/tools/node-content/package.json" "$root/tools/node-content/package-lock.json" "$out/node-content/"
echo "published $rid -> $out"

# Portability guard (macOS): a distro/Homebrew-built SDK ships its own runtime pack whose single-file host links against
# /opt/homebrew/... dylibs, so the app would crash on machines without them. Official packs only reference /usr/lib and /System.
if [ "$(uname -s)" = "Darwin" ] && [ "${rid%%-*}" = "osx" ] && command -v otool >/dev/null; then
  bad="$(otool -L "$out/DevAcademy.Desktop" | tail -n +2 | awk '{print $1}' | grep -vE '^(/usr/lib/|/System/|@)' || true)"
  if [ -n "$bad" ]; then
    echo "ERROR: host links to non-system libraries (not redistributable):" >&2; echo "$bad" >&2
    echo "Your SDK uses a locally built runtime pack. Re-run with the official one from NuGet, e.g.:" >&2
    echo "  DEVACADEMY_PUBLISH_ARGS=-p:RuntimeFrameworkVersion=<latest 10.0.x patch> $0 $rid" >&2
    exit 1
  fi
fi
