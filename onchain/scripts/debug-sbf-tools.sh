#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$PATH"

echo "=== which cargo-build-sbf ==="
which cargo-build-sbf
readlink -f "$(which cargo-build-sbf)"

echo "=== find platform-tools dirs ==="
find "$HOME/.local/share/solana" "$HOME/.cache/solana" -name 'platform-tools' 2>/dev/null | head -40

echo "=== strace-like: run with RUST_LOG ==="
export RUST_LOG=solana_cargo_build_sbf=debug
cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
# Just trigger install logic briefly
timeout 20 cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml 2>&1 | tail -80 || true
