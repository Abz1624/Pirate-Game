#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain

echo "=== versions ==="
echo "node:   $(node -v)"
echo "rustc:  $(rustc --version)"
echo "solana: $(solana --version)"
echo "anchor: $(anchor --version)"
echo "ephemeral-validator: $(command -v ephemeral-validator || echo MISSING)"

echo
echo "=== cargo test -p pirate-sim ==="
cargo test -p pirate-sim

echo
echo "=== simcli stress-cu ==="
cargo run -p pirate-simcli --release -- stress-cu --capacity 4

echo
echo "=== VERIFY OK ==="
