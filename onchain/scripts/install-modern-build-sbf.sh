#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

echo "=== Install modern cargo-build-sbf (supports sbpf + tools v1.54) ==="
cargo install cargo-build-sbf --locked 2>&1 | tail -20
hash -r
which cargo-build-sbf
cargo-build-sbf --version || true
cargo-build-sbf --help 2>&1 | head -60

CACHE_PT="$HOME/.cache/solana/v1.54/platform-tools"
ARCHIVE="$HOME/.cache/solana/platform-tools-v1.54.tar.bz2"
mkdir -p "$HOME/.cache/solana/v1.54"
if [[ ! -x "$CACHE_PT/rust/bin/rustc" ]]; then
  rm -rf "$CACHE_PT"
  mkdir -p "$CACHE_PT"
  tar -xjf "$ARCHIVE" -C "$CACHE_PT"
  if [[ -d "$CACHE_PT/platform-tools" ]]; then
    shopt -s dotglob
    mv "$CACHE_PT/platform-tools"/* "$CACHE_PT"/
    rmdir "$CACHE_PT/platform-tools" || true
  fi
fi

# Also put under v1.54 expected cache key
ln -sfn "$CACHE_PT" "$HOME/.cache/solana/v1.54/platform-tools" 2>/dev/null || true

rustup toolchain uninstall solana 2>/dev/null || true
rustup toolchain link solana "$CACHE_PT/rust"

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"
rm -rf target/sbf-solana-solana target/sbpf* 2>/dev/null || true

echo "=== Build with --tools-version v1.54 ==="
# Try common arch flags
if cargo-build-sbf --help 2>&1 | grep -q -- '--arch'; then
  cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --tools-version v1.54 --arch v1 -v \
    || cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --tools-version v1.54 --arch sbfv1 -v \
    || cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --tools-version v1.54 -v
else
  cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --tools-version v1.54 -v
fi

echo "=== ANCHOR BUILD DONE ==="
find "$ONCHAIN" -name 'pirate_coop.so' 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true
