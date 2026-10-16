#!/usr/bin/env bash
# Writes <dir>/SHA256SUMS covering every release file in <dir> (not recursive). Usage: packaging/checksums.sh [dir]
set -euo pipefail
dir="${1:-$(cd "$(dirname "$0")/.." && pwd)/artifacts/package}"
cd "$dir"
if command -v sha256sum >/dev/null; then sum() { sha256sum "$@"; }; else sum() { shasum -a 256 -b "$@"; }; fi
files=$(find . -maxdepth 1 -type f \( -name '*.dmg' -o -name '*.deb' -o -name '*.AppImage' -o -name '*.zip' -o -name '*-setup.exe' \) | sed 's|^\./||' | sort)
[ -n "$files" ] || { echo "no release files in $dir" >&2; exit 1; }
# shellcheck disable=SC2086
sum $files > SHA256SUMS
cat SHA256SUMS
