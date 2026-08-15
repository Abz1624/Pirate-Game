#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== Clear edition2024-broken crates ==="
rm -rf "$HOME/.cargo/registry/src"/*/block-buffer-0.12* \
       "$HOME/.cargo/registry/src"/*/digest-0.11* \
       "$HOME/.cargo/registry/src"/*/crypto-common-0.2* \
       "$HOME/.cargo/registry/src"/*/anchor-lang-1.* 2>/dev/null || true

echo "=== Regenerate lockfile (Rust 1.75) ==="
rm -f Cargo.lock
cargo +1.75.0 generate-lockfile
echo "block-buffer versions in lock:"
grep -A1 'name = "block-buffer"' Cargo.lock || true
echo "anchor-lang versions in lock:"
grep -A1 'name = "anchor-lang"' Cargo.lock || true
echo "ephemeral-rollups-sdk:"
grep -A1 'name = "ephemeral-rollups-sdk"' Cargo.lock | head -6

echo "=== anchor build ==="
anchor build
echo "=== ANCHOR BUILD DONE ==="
