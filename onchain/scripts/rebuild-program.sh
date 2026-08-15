#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== Switch Anchor CLI to 0.30.1 ==="
avm use 0.30.1
anchor --version
cargo-build-sbf --version
echo "=== cargo-build-sbf arch help ==="
cargo-build-sbf --help 2>&1 | grep -A2 -i arch || true

echo "=== cargo-build-sbf (platform-tools v1.54, arch v0 for Agave 2.1 verifier) ==="
# --arch v1 writes r10 (dynamic stack frames). Local mb-test-validator is Agave
# 2.1.21 and rejects that: "cannot write into register r10 (insn #0)".
cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --tools-version v1.54 --arch v0

echo "=== BUILD OK ==="
find "$ONCHAIN" -name 'pirate_coop.so' -ls 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true

echo "=== Install stub IDL for anchor test log streamer ==="
mkdir -p target/idl
cp -f idls/pirate_coop.json target/idl/pirate_coop.json
ls -la target/idl
