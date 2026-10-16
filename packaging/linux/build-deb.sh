#!/usr/bin/env bash
# Builds devacademy_<version>_<arch>.deb: /opt/devacademy (app + content), /usr/bin/devacademy symlink, .desktop file, icons.
# Usage: packaging/linux/build-deb.sh <linux-x64|linux-arm64> [outdir]    (needs dpkg-deb)
# CI-built; not run-verified on the author's machine (macOS has no dpkg-deb).
set -euo pipefail
rid="${1:?usage: build-deb.sh <linux-x64|linux-arm64> [outdir]}"
case "$rid" in linux-x64) arch=amd64;; linux-arm64) arch=arm64;; *) echo "unsupported RID $rid" >&2; exit 2;; esac
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
out="${2:-$root/artifacts/package}"
version="$("$root/packaging/version.sh")"
pub="$root/artifacts/publish/$rid"
"$root/packaging/publish.sh" "$rid" "$pub"

pkg="$(mktemp -d)/devacademy_${version}_${arch}"
trap 'rm -rf "$(dirname "$pkg")"' EXIT
install -d "$pkg/DEBIAN" "$pkg/opt/devacademy" "$pkg/usr/bin" "$pkg/usr/share/applications" "$pkg/usr/share/doc/devacademy"
cp -R "$pub/." "$pkg/opt/devacademy/"
chmod 755 "$pkg/opt/devacademy/DevAcademy.Desktop"
ln -s /opt/devacademy/DevAcademy.Desktop "$pkg/usr/bin/devacademy"
install -m 644 "$here/devacademy.desktop" "$pkg/usr/share/applications/devacademy.desktop"
for s in 16 24 32 48 64 128 256 512; do
  install -D -m 644 "$root/packaging/icon/png/$s.png" "$pkg/usr/share/icons/hicolor/${s}x${s}/apps/devacademy.png"
done
install -m 644 "$root/LICENSE" "$pkg/usr/share/doc/devacademy/copyright"
size_kb="$(du -sk "$pkg" | cut -f1)"
cat > "$pkg/DEBIAN/control" <<CTRL
Package: devacademy
Version: $version
Section: education
Priority: optional
Architecture: $arch
Installed-Size: $size_kb
Maintainer: DevAcademy contributors <noreply@devacademy.invalid>
Depends: libc6 (>= 2.31), libstdc++6, zlib1g, libfontconfig1, libx11-6, libice6, libsm6, libicu74 | libicu72 | libicu70 | libicu76 | libicu67 | libicu66
Recommends: python3, nodejs
Description: DevAcademy - desktop learning IDE
 Courses with an editor, a code runner and reviews. Language toolchains
 (Python, Node.js, Go, JDK, ...) are detected, never installed.
CTRL
find "$pkg" -type d -exec chmod 755 {} +
mkdir -p "$out"
dpkg-deb --root-owner-group --build "$pkg" "$out/devacademy_${version}_${arch}.deb" >/dev/null
echo "deb: $out/devacademy_${version}_${arch}.deb"
