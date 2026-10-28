#!/usr/bin/env bash
# Regenerates background.tiff (1x + @2x in one file, so Retina screens get the sharp one) from background.svg.
# Needs rsvg-convert (librsvg) and tiffutil (macOS). The output is committed so release builds do not need this step.
set -euo pipefail
cd "$(dirname "$0")"
command -v rsvg-convert >/dev/null || { echo "rsvg-convert not found (brew install librsvg)"; exit 1; }
tmp="$(mktemp -d)"
trap 'rm -rf "$tmp"' EXIT
rsvg-convert -w 660 -h 400 background.svg -o "$tmp/background.png"
rsvg-convert -w 1320 -h 800 background.svg -o "$tmp/background@2x.png"
tiffutil -cathidpicheck "$tmp/background.png" "$tmp/background@2x.png" -out background.tiff
echo "wrote $(pwd)/background.tiff"
