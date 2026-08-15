#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== Clear broken crates from registry cache ==="
rm -rf "$HOME/.cargo/registry/src"/*/block-buffer-0.12* || true

# Pin digest stack so SBF cargo 1.75 never pulls edition2024 crates
# Append pins into the program Cargo.toml if not present
if ! grep -q 'block-buffer' programs/pirate_coop/Cargo.toml; then
  cat >> programs/pirate_coop/Cargo.toml <<'EOF'

# Pins for Solana 1.18 / platform-tools cargo 1.75 (no edition2024)
block-buffer = "=0.10.4"
crypto-common = "=0.1.6"
digest = "=0.10.7"
EOF
fi

echo "=== Regenerate lockfile with Rust 1.75 ==="
rm -f Cargo.lock
cargo +1.75.0 generate-lockfile
grep -A2 'name = "block-buffer"' Cargo.lock | head -10

echo "=== anchor build ==="
anchor build
echo "=== ANCHOR BUILD DONE ==="
