#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$PATH"

SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
DEP="$SDK/dependencies"
ARCHIVE="$HOME/.cache/solana/platform-tools-v1.54.tar.bz2"

echo "=== Re-seed platform-tools-v1.43 with 1.89 binaries ==="
cd "$DEP"
rm -rf platform-tools platform-tools-v1.43
mkdir -p platform-tools-v1.43
tar -xjf "$ARCHIVE" -C platform-tools-v1.43
if [[ -d platform-tools-v1.43/platform-tools ]]; then
  shopt -s dotglob
  mv platform-tools-v1.43/platform-tools/* platform-tools-v1.43/
  rmdir platform-tools-v1.43/platform-tools || true
fi
ln -sfn platform-tools-v1.43 platform-tools
echo "tools cargo: $(platform-tools/rust/bin/cargo --version)"
echo "tools rustc: $(platform-tools/rust/bin/rustc --version)"

# Put tools bins first on PATH so any accidental host cargo isn't used for parsing
export PATH="$DEP/platform-tools/rust/bin:$PATH"

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== regenerate lockfile with pins ==="
rm -rf "$HOME/.cargo/registry/src"/*/zeroize_derive-1.5* 2>/dev/null || true
rm -f Cargo.lock
cargo generate-lockfile
grep -A1 'name = "zeroize"' Cargo.lock | head -20

echo "=== cargo-build-sbf ==="
cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --sbf-sdk "$SDK" -v

echo "=== ANCHOR BUILD DONE ==="
find "$ONCHAIN" -name 'pirate_coop.so' 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true
