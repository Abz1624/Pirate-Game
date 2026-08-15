#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

echo "=== Fixing Solana platform-tools ==="
# Clear broken/partial platform-tools so cargo-build-sbf redownloads them
PT_DIR="$HOME/.local/share/solana/install/releases/1.18.17/solana-release/bin/sdk/sbf/dependencies/platform-tools"
CACHE="$HOME/.cache/solana"
rm -rf "$PT_DIR" "$CACHE/platform-tools"* "$CACHE/solana-release"* 2>/dev/null || true
ls -la "$(dirname "$PT_DIR")" 2>/dev/null || echo "(dependencies dir missing — will be created)"

echo "=== Trigger platform-tools download via cargo-build-sbf ==="
# This downloads platform-tools on first run
cargo-build-sbf --version || true

cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
echo "=== anchor build ==="
anchor build
echo "=== ANCHOR BUILD DONE ==="
