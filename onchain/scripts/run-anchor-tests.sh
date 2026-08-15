#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
avm use 0.30.1
mkdir -p target/idl
cp -f idls/pirate_coop.json target/idl/pirate_coop.json
solana --version
solana program show EeWGiecEDWgBB3eUxgQfTLjQJcZnLRYsXvbCcYfxP6pC
anchor test --skip-local-validator --skip-build --skip-deploy
