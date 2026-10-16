#!/usr/bin/env bash
# Builds DevAcademy.app (+ DevAcademy-<version>-<rid>.dmg) for one macOS RID.
# Usage: packaging/macos/build-app.sh <osx-arm64|osx-x64> [outdir]      (default outdir: artifacts/package)
#
# Signing (all optional; nothing secret ever lives in the repo):
#   default                      ad-hoc signature (`codesign -s -`): runs on this Mac, Gatekeeper warns elsewhere.
#   MACOS_SIGN_IDENTITY          e.g. "Developer ID Application: Name (TEAMID)" -> hardened runtime + entitlements + timestamp.
#   Notarization (needs MACOS_SIGN_IDENTITY and either):
#     MACOS_NOTARY_PROFILE       a `xcrun notarytool store-credentials` keychain profile, or
#     APPLE_ID + APPLE_TEAM_ID + APPLE_APP_PASSWORD   (app-specific password)
#   MACOS_KEYCHAIN               optional keychain path to pass to codesign.
set -euo pipefail
rid="${1:?usage: build-app.sh <osx-arm64|osx-x64> [outdir]}"
case "$rid" in osx-arm64|osx-x64) ;; *) echo "unsupported RID $rid" >&2; exit 2;; esac
here="$(cd "$(dirname "$0")" && pwd)"
root="$(cd "$here/../.." && pwd)"
out="${2:-$root/artifacts/package}"
version="$("$root/packaging/version.sh")"
pub="$root/artifacts/publish/$rid"
app="$out/$rid/DevAcademy.app"
dmg="$out/DevAcademy-$version-$rid.dmg"

"$root/packaging/publish.sh" "$rid" "$pub"

rm -rf "$out/$rid"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources"
# Everything except the data files goes next to the executable; data goes into Resources (found by FindContentRoot).
(cd "$pub" && tar --exclude=./content --exclude=./THIRD-PARTY-NOTICES.md --exclude=./LICENSE -cf - .) | (cd "$app/Contents/MacOS" && tar -xf -)
cp -R "$pub/content" "$pub/THIRD-PARTY-NOTICES.md" "$pub/LICENSE" "$app/Contents/Resources/"
cp "$root/packaging/icon/DevAcademy.icns" "$app/Contents/Resources/DevAcademy.icns"

cat > "$app/Contents/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key><string>ru</string>
  <key>CFBundleExecutable</key><string>DevAcademy.Desktop</string>
  <key>CFBundleIconFile</key><string>DevAcademy</string>
  <key>CFBundleIdentifier</key><string>org.devacademy.app</string>
  <key>CFBundleInfoDictionaryVersion</key><string>6.0</string>
  <key>CFBundleName</key><string>DevAcademy</string>
  <key>CFBundleDisplayName</key><string>DevAcademy</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>$version</string>
  <key>CFBundleVersion</key><string>$version</string>
  <key>LSApplicationCategoryType</key><string>public.app-category.education</string>
  <!-- .NET 10 supports macOS 14 (Sonoma) and later. -->
  <key>LSMinimumSystemVersion</key><string>14.0</string>
  <key>NSHighResolutionCapable</key><true/>
  <key>NSHumanReadableCopyright</key><string>MIT License</string>
</dict>
</plist>
PLIST
plutil -lint "$app/Contents/Info.plist" >/dev/null

sign_args=()
[ -n "${MACOS_KEYCHAIN:-}" ] && sign_args+=(--keychain "$MACOS_KEYCHAIN")
notary_args=()
if [ -n "${MACOS_NOTARY_PROFILE:-}" ]; then notary_args=(--keychain-profile "$MACOS_NOTARY_PROFILE")
elif [ -n "${APPLE_ID:-}" ] && [ -n "${APPLE_TEAM_ID:-}" ] && [ -n "${APPLE_APP_PASSWORD:-}" ]; then
  notary_args=(--apple-id "$APPLE_ID" --team-id "$APPLE_TEAM_ID" --password "$APPLE_APP_PASSWORD"); fi

notarize() { # <file>
  [ -n "${MACOS_SIGN_IDENTITY:-}" ] && [ ${#notary_args[@]} -gt 0 ] || return 1
  echo "notarizing $1"
  xcrun notarytool submit "$1" "${notary_args[@]}" --wait
}

if [ -n "${MACOS_SIGN_IDENTITY:-}" ]; then
  echo "signing with Developer ID identity (hardened runtime)"
  # Inside-out: every nested Mach-O first, then the bundle. Same identity everywhere => library validation passes.
  while IFS= read -r f; do
    codesign --force --timestamp --options runtime --entitlements "$here/entitlements.plist" -s "$MACOS_SIGN_IDENTITY" ${sign_args[@]+"${sign_args[@]}"} "$f"
  done < <(find "$app/Contents/MacOS" -type f -exec sh -c 'file "$1" | grep -q "Mach-O"' _ {} \; -print)
  codesign --force --timestamp --options runtime --entitlements "$here/entitlements.plist" -s "$MACOS_SIGN_IDENTITY" ${sign_args[@]+"${sign_args[@]}"} "$app"
else
  echo "ad-hoc signing (set MACOS_SIGN_IDENTITY for Developer ID signing)"
  codesign --force --deep -s - "$app"
fi
codesign --verify --deep --strict --verbose=1 "$app"

if [ -n "${MACOS_SIGN_IDENTITY:-}" ] && [ ${#notary_args[@]} -gt 0 ]; then
  zip="$out/$rid/DevAcademy-notarize.zip"
  ditto -c -k --keepParent "$app" "$zip"
  notarize "$zip"
  xcrun stapler staple "$app"
  rm -f "$zip"
fi

"$here/build-dmg.sh" "$app" "$dmg"
if [ -n "${MACOS_SIGN_IDENTITY:-}" ]; then
  codesign --force --timestamp -s "$MACOS_SIGN_IDENTITY" ${sign_args[@]+"${sign_args[@]}"} "$dmg"
  if [ ${#notary_args[@]} -gt 0 ]; then notarize "$dmg"; xcrun stapler staple "$dmg"; fi
fi
echo "app: $app"
echo "dmg: $dmg"
