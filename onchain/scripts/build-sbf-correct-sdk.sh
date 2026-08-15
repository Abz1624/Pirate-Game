#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$PATH"

SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
DEST="$SDK/dependencies/platform-tools"
ARCHIVE="$HOME/.cache/solana/platform-tools-v1.54.tar.bz2"

echo "=== Install platform-tools into SDK path cargo-build-sbf uses ==="
echo "SDK=$SDK"
mkdir -p "$SDK/dependencies"
rm -rf "$DEST" "$DEST.tmp" "$SDK/dependencies/platform-tools-"* 2>/dev/null || true
mkdir -p "$DEST"
tar -xjf "$ARCHIVE" -C "$DEST"
if [[ -d "$DEST/platform-tools" ]]; then
  shopt -s dotglob
  mv "$DEST/platform-tools"/* "$DEST"/
  rmdir "$DEST/platform-tools" || true
fi
"$DEST/rust/bin/rustc" --version
"$DEST/rust/bin/cargo" --version

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== cargo-build-sbf --tools-version v1.54 ==="
cargo-build-sbf \
  --manifest-path programs/pirate_coop/Cargo.toml \
  --tools-version v1.54 \
  --sbf-sdk "$SDK" \
  -v

echo "=== ANCHOR BUILD DONE ==="
find . -name 'pirate_coop.so' 2>/dev/null
ls -la target/deploy/ 2>/dev/null || true
