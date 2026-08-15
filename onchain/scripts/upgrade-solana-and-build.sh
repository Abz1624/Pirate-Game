#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"

echo "=== Install Agave/Solana 2.1 (newer platform-tools) ==="
sh -c "$(curl -sSfL https://release.anza.xyz/v2.1.21/install)"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
solana --version
cargo-build-sbf --version

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

# Prefer host cargo lockfile (v4 is fine for Solana 2.1 platform-tools)
echo "=== Regenerate lockfile with stable cargo ==="
rm -f Cargo.lock
cargo generate-lockfile

echo "=== Clear broken registry entries ==="
rm -rf "$HOME/.cargo/registry/src"/*/block-buffer-0.12* \
       "$HOME/.cargo/registry/src"/*/zeroize_derive-1.5* 2>/dev/null || true

echo "=== anchor build ==="
anchor build
echo "=== ANCHOR BUILD DONE ==="
echo "solana: $(solana --version)"
echo "anchor: $(anchor --version)"
