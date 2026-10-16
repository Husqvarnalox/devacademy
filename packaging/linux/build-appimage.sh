#!/usr/bin/env bash
# Builds DevAcademy-<version>-<arch>.AppImage with appimagetool.
# Usage: packaging/linux/build-appimage.sh <linux-x64|linux-arm64> [outdir]
# appimagetool: $APPIMAGETOOL, else `appimagetool` on PATH, else packaging/linux/fetch-appimagetool.sh downloads a
# checksum-pinned release. CI-built; not run-verified on the author's machine (macOS).
set -euo pipefail
rid="${1:?usage: build-appimage.sh <linux-x64|linux-arm64> [outdir]}"
case "$rid" in linux-x64) arch=x86_64;; linux-arm64) arch=aarch64;; *) echo "unsupported RID $rid" >&2; exit 2;; esac
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
out="${2:-$root/artifacts/package}"
version="$("$root/packaging/version.sh")"
pub="$root/artifacts/publish/$rid"
"$root/packaging/publish.sh" "$rid" "$pub"

tool="${APPIMAGETOOL:-$(command -v appimagetool || true)}"
[ -n "$tool" ] || tool="$("$here/fetch-appimagetool.sh")"

appdir="$(mktemp -d)/DevAcademy.AppDir"
trap 'rm -rf "$(dirname "$appdir")"' EXIT
install -d "$appdir/usr/lib/devacademy" "$appdir/usr/bin"
cp -R "$pub/." "$appdir/usr/lib/devacademy/"
cat > "$appdir/AppRun" <<'RUN'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/lib/devacademy/DevAcademy.Desktop" "$@"
RUN
chmod 755 "$appdir/AppRun" "$appdir/usr/lib/devacademy/DevAcademy.Desktop"
cp "$here/devacademy.desktop" "$appdir/devacademy.desktop"
cp "$root/packaging/icon/png/256.png" "$appdir/devacademy.png"
cp "$root/packaging/icon/png/256.png" "$appdir/.DirIcon"
mkdir -p "$out"
dest="$out/DevAcademy-$version-$arch.AppImage"
# APPIMAGE_EXTRACT_AND_RUN: CI runners have no FUSE, so appimagetool itself must run extracted.
APPIMAGE_EXTRACT_AND_RUN=1 ARCH="$arch" "$tool" "$appdir" "$dest"
chmod 755 "$dest"
echo "appimage: $dest"
