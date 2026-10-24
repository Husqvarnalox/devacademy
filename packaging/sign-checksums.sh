#!/usr/bin/env bash
# Signs <dir>/SHA256SUMS with a detached armored GPG signature (SHA256SUMS.asc) and exports the public key next to it
# (DevAcademy-release-key.asc). Users must check the key fingerprint against the one published in README.md.
# Usage: GPG_PRIVATE_KEY=<armored secret key> [GPG_PASSPHRASE=...] packaging/sign-checksums.sh [dir]
set -euo pipefail
dir="${1:-$(cd "$(dirname "$0")/.." && pwd)/artifacts/package}"
[ -f "$dir/SHA256SUMS" ] || { echo "no $dir/SHA256SUMS (run packaging/checksums.sh first)" >&2; exit 1; }
: "${GPG_PRIVATE_KEY:?set GPG_PRIVATE_KEY to an armored secret key}"
GNUPGHOME="$(mktemp -d)"; export GNUPGHOME
trap 'gpgconf --kill gpg-agent 2>/dev/null || true; rm -rf "$GNUPGHOME"' EXIT
chmod 700 "$GNUPGHOME"
printf '%s\n' "$GPG_PRIVATE_KEY" | gpg --batch --quiet --import
pass=()
[ -n "${GPG_PASSPHRASE:-}" ] && pass=(--pinentry-mode loopback --passphrase-fd 0)
printf '%s' "${GPG_PASSPHRASE:-}" | gpg --batch --yes ${pass[@]+"${pass[@]}"} --armor --detach-sign \
  --output "$dir/SHA256SUMS.asc" "$dir/SHA256SUMS"
gpg --batch --verify "$dir/SHA256SUMS.asc" "$dir/SHA256SUMS"
gpg --batch --armor --export > "$dir/DevAcademy-release-key.asc"
gpg --batch --fingerprint
