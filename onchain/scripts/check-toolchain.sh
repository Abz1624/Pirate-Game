#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
echo "solana: $(solana --version 2>/dev/null || echo MISSING)"
echo "anchor: $(anchor --version 2>/dev/null || echo MISSING)"
echo "which solana: $(command -v solana)"
echo "active_release: $(readlink -f "$HOME/.local/share/solana/install/active_release" 2>/dev/null || ls -ld "$HOME/.local/share/solana/install/active_release" 2>/dev/null)"
ls -la "$HOME/.local/share/solana/install/releases" 2>/dev/null | tail -20
echo "---"
ls -la /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain/idls/pirate_coop.json
