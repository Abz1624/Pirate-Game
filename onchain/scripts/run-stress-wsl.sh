#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cargo run -p pirate-simcli --release -- stress-cu --capacity 4
echo "=== STRESS OK ==="
