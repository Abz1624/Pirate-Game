#!/usr/bin/env bash
set -euo pipefail
PT="$HOME/.cache/solana/v1.43/platform-tools"
echo "=== rustlib targets in current tools ==="
ls "$PT/rust/lib/rustlib" 2>/dev/null || echo missing
find "$PT/rust/lib/rustlib" -maxdepth 1 -type d 2>/dev/null

echo "=== Also check original v1.43 archive if we still have it ==="
ls -lh "$HOME/.cache/solana/"*.tar.bz2
