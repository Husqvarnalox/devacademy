#!/usr/bin/env bash
# Downloads appimagetool (host architecture) from a pinned release and verifies its SHA-256. Prints the tool's path.
# Hashes were taken from the release's published asset digests (GitHub API) for tag 1.9.1.
set -euo pipefail
version=1.9.1
case "$(uname -m)" in
  x86_64)  asset=appimagetool-x86_64.AppImage;  sha=ed4ce84f0d9caff66f50bcca6ff6f35aae54ce8135408b3fa33abfc3cb384eb0;;
  aarch64|arm64) asset=appimagetool-aarch64.AppImage; sha=f0837e7448a0c1e4e650a93bb3e85802546e60654ef287576f46c71c126a9158;;
  *) echo "unsupported host architecture $(uname -m)" >&2; exit 2;;
esac
dir="${APPIMAGETOOL_DIR:-${TMPDIR:-/tmp}/appimagetool-$version}"
mkdir -p "$dir"
tool="$dir/$asset"
if [ ! -x "$tool" ] || ! echo "$sha  $tool" | sha256sum -c - >/dev/null 2>&1; then
  curl -fsSL --retry 3 -o "$tool" "https://github.com/AppImage/appimagetool/releases/download/$version/$asset"
  echo "$sha  $tool" | sha256sum -c - >&2
  chmod +x "$tool"
fi
echo "$tool"
