#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
solana config set --url localhost
solana program deploy target/deploy/pirate_coop.so \
  --program-id target/deploy/pirate_coop-keypair.json
solana program show EeWGiecEDWgBB3eUxgQfTLjQJcZnLRYsXvbCcYfxP6pC
