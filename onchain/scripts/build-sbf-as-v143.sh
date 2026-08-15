#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$PATH"

SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
DEP="$SDK/dependencies"
ARCHIVE="$HOME/.cache/solana/platform-tools-v1.54.tar.bz2"

echo "=== Place tools as v1.43 (Solana 2.1.21 default) so install is skipped ==="
cd "$DEP"
rm -rf platform-tools platform-tools-v1.43 platform-tools-v1.54 platform-tools.tmp 2>/dev/null || true
mkdir -p platform-tools-v1.43
tar -xjf "$ARCHIVE" -C platform-tools-v1.43
if [[ -d platform-tools-v1.43/platform-tools ]]; then
  shopt -s dotglob
  mv platform-tools-v1.43/platform-tools/* platform-tools-v1.43/
  rmdir platform-tools-v1.43/platform-tools || true
fi
ln -sfn platform-tools-v1.43 platform-tools
ls -la
platform-tools/rust/bin/rustc --version

# Also seed download cache in case it still checks
mkdir -p "$HOME/.cache/solana/v1.43"
cp -n "$ARCHIVE" "$HOME/.cache/solana/v1.43/platform-tools-linux-x86_64.tar.bz2" 2>/dev/null || \
  cp "$ARCHIVE" "$HOME/.cache/solana/v1.43/platform-tools-linux-x86_64.tar.bz2"

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== cargo-build-sbf (default tools, no reinstall) ==="
cargo-build-sbf \
  --manifest-path programs/pirate_coop/Cargo.toml \
  --sbf-sdk "$SDK" \
  -v

echo "=== ANCHOR BUILD DONE ==="
find "$ONCHAIN" -name 'pirate_coop.so' 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true
