#!/usr/bin/env bash
# Regenerates PNG sizes, .icns (macOS only: iconutil) and .ico from devacademy.svg. Needs rsvg-convert (librsvg) and python3.
# Outputs are committed so release builds do not need this step.
set -euo pipefail
cd "$(dirname "$0")"
command -v rsvg-convert >/dev/null || { echo "rsvg-convert not found (brew install librsvg / apt install librsvg2-bin)"; exit 1; }
mkdir -p png
for s in 16 24 32 48 64 128 256 512 1024; do rsvg-convert -w "$s" -h "$s" devacademy.svg -o "png/$s.png"; done
if command -v iconutil >/dev/null; then
  set_dir=$(mktemp -d)/DevAcademy.iconset
  mkdir -p "$set_dir"
  for s in 16 32 128 256 512; do
    cp "png/$s.png" "$set_dir/icon_${s}x${s}.png"
    d=$((s * 2)); cp "png/$d.png" "$set_dir/icon_${s}x${s}@2x.png" 2>/dev/null \
      || rsvg-convert -w "$d" -h "$d" devacademy.svg -o "$set_dir/icon_${s}x${s}@2x.png"
  done
  iconutil -c icns "$set_dir" -o DevAcademy.icns
else
  echo "iconutil unavailable (not macOS); keeping committed DevAcademy.icns"
fi
python3 - <<'PY'
# Minimal PNG-in-ICO writer (Vista+ format): sizes 16..256.
import struct
sizes = [16, 24, 32, 48, 64, 128, 256]
data = [open(f"png/{s}.png", "rb").read() for s in sizes]
out = struct.pack("<HHH", 0, 1, len(sizes))
off = 6 + 16 * len(sizes)
for s, d in zip(sizes, data):
    out += struct.pack("<BBBBHHII", s % 256, s % 256, 0, 0, 1, 32, len(d), off)
    off += len(d)
open("DevAcademy.ico", "wb").write(out + b"".join(data))
PY
echo "icons written in $(pwd)"
