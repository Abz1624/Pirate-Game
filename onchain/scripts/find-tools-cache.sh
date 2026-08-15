#!/usr/bin/env bash
set -euo pipefail
# Find where cargo-build-sbf looks for cached platform-tools downloads
strings "$(which cargo-build-sbf)" 2>/dev/null | grep -iE 'platform-tools|cache/solana|\.cache' | head -40
echo "---"
# Also check common cache layouts
ls -laR "$HOME/.cache/solana" 2>/dev/null | head -60
