#!/usr/bin/env bash
# Creates a compressed (UDZO) disk image that opens as a drag-to-install window: the .app, an Applications link,
# a background with an arrow and a hint, a volume icon, fixed icon positions and no toolbar/sidebar.
# Layout lives in dmg/dmg-settings.py; the background is dmg/background.tiff (regenerate with dmg/build-background.sh).
# Usage: packaging/macos/build-dmg.sh <path/to/DevAcademy.app> <out.dmg>
# Needs python3; dmgbuild is installed (pinned, hash-checked) into a venv under $DMGBUILD_VENV (default: artifacts/.dmgbuild-venv).
set -euo pipefail
app="${1:?usage: build-dmg.sh <app> <out.dmg>}"
dmg="${2:?usage: build-dmg.sh <app> <out.dmg>}"
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
venv="${DMGBUILD_VENV:-$root/artifacts/.dmgbuild-venv}"
req="$here/dmg/requirements.txt"
if ! cmp -s "$req" "$venv/requirements.txt" 2>/dev/null; then
  rm -rf "$venv"
  python3 -m venv "$venv"
  "$venv/bin/python" -m pip install -q --disable-pip-version-check --require-hashes --only-binary :all: -r "$req"
  cp "$req" "$venv/requirements.txt"
fi
rm -f "$dmg"
"$venv/bin/dmgbuild" -s "$here/dmg/dmg-settings.py" -D app="$app" -D root="$here/dmg" "DevAcademy" "$dmg" >/dev/null
echo "created $dmg"
