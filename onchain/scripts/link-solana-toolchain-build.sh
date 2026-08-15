#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$PATH"
# IMPORTANT: do NOT put platform-tools cargo ahead of rustup cargo

SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
PT="$SDK/dependencies/platform-tools"
ARCHIVE="$HOME/.cache/solana/platform-tools-v1.54.tar.bz2"

echo "=== Ensure platform-tools present ==="
if [[ ! -x "$PT/rust/bin/rustc" ]]; then
  mkdir -p "$SDK/dependencies/platform-tools-v1.43"
  tar -xjf "$ARCHIVE" -C "$SDK/dependencies/platform-tools-v1.43"
  if [[ -d "$SDK/dependencies/platform-tools-v1.43/platform-tools" ]]; then
    shopt -s dotglob
    mv "$SDK/dependencies/platform-tools-v1.43/platform-tools"/* "$SDK/dependencies/platform-tools-v1.43"/
    rmdir "$SDK/dependencies/platform-tools-v1.43/platform-tools" || true
  fi
  ln -sfn platform-tools-v1.43 "$PT"
fi
"$PT/rust/bin/rustc" --version
"$PT/rust/bin/cargo" --version

echo "=== Link rustup toolchain 'solana' -> platform-tools rust ==="
rustup toolchain unlink solana 2>/dev/null || true
rustup toolchain link solana "$PT/rust"
rustup run solana rustc --version
rustup run solana cargo --version

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== cargo-build-sbf ==="
cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --sbf-sdk "$SDK" -v

echo "=== ANCHOR BUILD DONE ==="
find "$ONCHAIN" -name 'pirate_coop.so' 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true
