#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$PATH"

ARCHIVE="$HOME/.cache/solana/platform-tools-v1.54.tar.bz2"
CACHE_PT="$HOME/.cache/solana/v1.43/platform-tools"
SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain

echo "=== Replace ~/.cache/solana/v1.43/platform-tools with v1.54 binaries ==="
rm -rf "$CACHE_PT"
mkdir -p "$CACHE_PT"
tar -xjf "$ARCHIVE" -C "$CACHE_PT"
if [[ -d "$CACHE_PT/platform-tools" ]]; then
  shopt -s dotglob
  mv "$CACHE_PT/platform-tools"/* "$CACHE_PT"/
  rmdir "$CACHE_PT/platform-tools" || true
fi
"$CACHE_PT/rust/bin/rustc" --version
"$CACHE_PT/rust/bin/cargo" --version

echo "=== Point SDK deps + rustup at cache tools ==="
mkdir -p "$SDK/dependencies"
ln -sfn "$CACHE_PT" "$SDK/dependencies/platform-tools"
ln -sfn "$CACHE_PT" "$SDK/dependencies/platform-tools-v1.43"
rustup toolchain uninstall solana 2>/dev/null || true
rustup toolchain link solana "$CACHE_PT/rust"
rustup run solana cargo --version

cd "$ONCHAIN"
echo "=== cargo-build-sbf ==="
cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --sbf-sdk "$SDK" -v

echo "=== Verify tools not reverted ==="
"$CACHE_PT/rust/bin/cargo" --version
rustup run solana cargo --version

echo "=== ANCHOR BUILD DONE ==="
find "$ONCHAIN" -name 'pirate_coop.so' 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true
