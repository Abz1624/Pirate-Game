#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== Install Rust 1.75 for Solana 1.18 lockfile compat ==="
rustup install 1.75.0
rm -f Cargo.lock
cargo +1.75.0 generate-lockfile
head -5 Cargo.lock

echo "=== anchor build ==="
anchor build
echo "=== ANCHOR BUILD DONE ==="
