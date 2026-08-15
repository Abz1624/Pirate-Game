#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$PATH"

SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
DEP="$SDK/dependencies"
ARCHIVE="$HOME/.cache/solana/platform-tools-v1.54.tar.bz2"
ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain

echo "=== Ensure tools + rustup link ==="
cd "$DEP"
if [[ ! -x platform-tools-v1.54/rust/bin/rustc ]]; then
  rm -rf platform-tools-v1.54
  mkdir -p platform-tools-v1.54
  tar -xjf "$ARCHIVE" -C platform-tools-v1.54
  if [[ -d platform-tools-v1.54/platform-tools ]]; then
    shopt -s dotglob
    mv platform-tools-v1.54/platform-tools/* platform-tools-v1.54/
    rmdir platform-tools-v1.54/platform-tools || true
  fi
fi
ln -sfn platform-tools-v1.54 platform-tools-v1.43
ln -sfn platform-tools-v1.43 platform-tools
rustup toolchain uninstall solana 2>/dev/null || true
rustup toolchain link solana "$(pwd)/platform-tools/rust"
echo "solana toolchain: $(rustup run solana cargo --version)"

cd "$ONCHAIN"
echo "=== Prefetch crates (online) ==="
cargo fetch
rustup run solana cargo fetch --manifest-path programs/pirate_coop/Cargo.toml || cargo fetch

echo "=== cargo-build-sbf (online ok; tools already present) ==="
cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --sbf-sdk "$SDK" -v

echo "=== ANCHOR BUILD DONE ==="
find "$ONCHAIN" -name 'pirate_coop.so' 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true
