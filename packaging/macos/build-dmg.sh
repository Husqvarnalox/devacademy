#!/usr/bin/env bash
# Creates a compressed (UDZO) disk image with the .app and an Applications symlink.
# Usage: packaging/macos/build-dmg.sh <path/to/DevAcademy.app> <out.dmg>
set -euo pipefail
app="${1:?usage: build-dmg.sh <app> <out.dmg>}"
dmg="${2:?usage: build-dmg.sh <app> <out.dmg>}"
stage="$(mktemp -d)"
trap 'rm -rf "$stage"' EXIT
cp -R "$app" "$stage/"
ln -s /Applications "$stage/Applications"
rm -f "$dmg"
hdiutil create -volname "DevAcademy" -srcfolder "$stage" -fs HFS+ -format UDZO -imagekey zlib-level=9 -ov "$dmg" >/dev/null
echo "created $dmg"
