#!/usr/bin/env bash
# Prints the product version (single source of truth: <Version> in Directory.Build.props).
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
sed -n 's|.*<Version>\(.*\)</Version>.*|\1|p' "$root/Directory.Build.props" | head -n1
