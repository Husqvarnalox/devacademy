#!/usr/bin/env bash
# Builds DevAcademy-<version>-<rid>.zip and, when Inno Setup (iscc) is available, DevAcademy-<version>-<rid>-setup.exe.
# Usage: packaging/windows/build-windows.sh <win-x64|win-arm64> [outdir]   (run in Git Bash on Windows; CI-built, not run-verified)
set -euo pipefail
rid="${1:?usage: build-windows.sh <win-x64|win-arm64> [outdir]}"
case "$rid" in win-x64|win-arm64) ;; *) echo "unsupported RID $rid" >&2; exit 2;; esac
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
out="${2:-$root/artifacts/package}"
version="$("$root/packaging/version.sh")"
pub="$root/artifacts/publish/$rid"
"$root/packaging/publish.sh" "$rid" "$pub"
mkdir -p "$out"
zip="$out/DevAcademy-$version-$rid.zip"
rm -f "$zip"
if command -v 7z >/dev/null; then (cd "$pub" && 7z a -tzip -bd "$zip" . >/dev/null)
elif command -v powershell >/dev/null; then powershell -NoProfile -Command "Compress-Archive -Path '$(cygpath -w "$pub")\\*' -DestinationPath '$(cygpath -w "$zip")'"
else (cd "$pub" && zip -qr "$zip" .); fi
echo "zip: $zip"
iscc="$(command -v iscc || true)"
[ -n "$iscc" ] || for p in "/c/Program Files (x86)/Inno Setup 6/ISCC.exe" "/c/Program Files/Inno Setup 6/ISCC.exe" "$LOCALAPPDATA/Programs/Inno Setup 6/ISCC.exe"; do [ -x "$p" ] && iscc="$p" && break; done
if [ -n "$iscc" ]; then
  MSYS2_ARG_CONV_EXCL='*' "$iscc" "/DAppVersion=$version" "/DRid=$rid" "/DSourceDir=$(cygpath -w "$pub")" \
    "/DOutputDir=$(cygpath -w "$out")" "/DRepoRoot=$(cygpath -w "$root")" "$(cygpath -w "$here/devacademy.iss")"
  echo "installer: $out/DevAcademy-$version-$rid-setup.exe"
else
  echo "Inno Setup (iscc) not found: skipped the installer (zip only)" >&2
fi
