#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
avm use 0.30.1
mkdir -p target/idl target/types
# cargo-build-sbf does not emit IDL; anchor test's log streamer requires
# target/idl/<lib>.json. Full `anchor idl build` currently fails on this
# rustc + anchor-syn 0.30.1 combo (Span::source_file).
cp -f idls/pirate_coop.json target/idl/pirate_coop.json
ls -la target/idl
echo "=== Running tests ==="
anchor test --skip-local-validator --skip-build --skip-deploy
echo "=== TEST EXIT $? ==="
