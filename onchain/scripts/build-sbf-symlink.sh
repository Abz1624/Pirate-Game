#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$PATH"

SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
DEP="$SDK/dependencies"
cd "$DEP"

echo "=== Normalize platform-tools layout (versioned dir + symlink) ==="
rm -rf platform-tools-v1.54 platform-tools platform-tools.tmp platform-tools-v1.43 2>/dev/null || true
mkdir -p platform-tools-v1.54
tar -xjf "$HOME/.cache/solana/platform-tools-v1.54.tar.bz2" -C platform-tools-v1.54
if [[ -d platform-tools-v1.54/platform-tools ]]; then
  shopt -s dotglob
  mv platform-tools-v1.54/platform-tools/* platform-tools-v1.54/
  rmdir platform-tools-v1.54/platform-tools || true
fi
ln -sfn platform-tools-v1.54 platform-tools
ls -la
platform-tools/rust/bin/rustc --version

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== cargo-build-sbf offline with v1.54 ==="
cargo-build-sbf \
  --manifest-path programs/pirate_coop/Cargo.toml \
  --tools-version v1.54 \
  --sbf-sdk "$SDK" \
  --offline \
  -v

echo "=== ANCHOR BUILD DONE ==="
find "$ONCHAIN" -name 'pirate_coop.so' 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true
