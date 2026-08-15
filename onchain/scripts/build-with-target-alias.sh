#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$PATH"

CACHE_PT="$HOME/.cache/solana/v1.43/platform-tools"
ARCHIVE="$HOME/.cache/solana/platform-tools-v1.54.tar.bz2"
SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain

# Ensure v1.54 binaries in the cache path cargo-build-sbf uses
if [[ ! -x "$CACHE_PT/rust/bin/rustc" ]] || ! "$CACHE_PT/rust/bin/rustc" --version | grep -q '1.89'; then
  rm -rf "$CACHE_PT"
  mkdir -p "$CACHE_PT"
  tar -xjf "$ARCHIVE" -C "$CACHE_PT"
  if [[ -d "$CACHE_PT/platform-tools" ]]; then
    shopt -s dotglob
    mv "$CACHE_PT/platform-tools"/* "$CACHE_PT"/
    rmdir "$CACHE_PT/platform-tools" || true
  fi
fi

RUSTLIB="$CACHE_PT/rust/lib/rustlib"
echo "=== Add legacy sbf-solana-solana alias -> sbpfv1-solana-solana ==="
ln -sfn sbpfv1-solana-solana "$RUSTLIB/sbf-solana-solana"
ls -la "$RUSTLIB" | head -20

mkdir -p "$SDK/dependencies"
ln -sfn "$CACHE_PT" "$SDK/dependencies/platform-tools"
ln -sfn "$CACHE_PT" "$SDK/dependencies/platform-tools-v1.43"
rustup toolchain uninstall solana 2>/dev/null || true
rustup toolchain link solana "$CACHE_PT/rust"
rustup run solana rustc --version

cd "$ONCHAIN"
# Clean stale target that mixed toolchains
rm -rf target/sbf-solana-solana target/sbpf-solana-solana target/sbpfv1-solana-solana 2>/dev/null || true

echo "=== cargo-build-sbf ==="
cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --sbf-sdk "$SDK" --arch sbfv1 -v

echo "=== ANCHOR BUILD DONE ==="
find "$ONCHAIN" -name 'pirate_coop.so' 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true
