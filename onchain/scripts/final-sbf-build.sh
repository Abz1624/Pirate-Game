#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$PATH"

SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
DEP="$SDK/dependencies"
ARCHIVE="$HOME/.cache/solana/platform-tools-v1.54.tar.bz2"

echo "=== Force platform-tools-v1.54 and link as rustup solana toolchain ==="
cd "$DEP"
rm -rf platform-tools platform-tools-v1.43 platform-tools-v1.54
mkdir -p platform-tools-v1.54
tar -xjf "$ARCHIVE" -C platform-tools-v1.54
if [[ -d platform-tools-v1.54/platform-tools ]]; then
  shopt -s dotglob
  mv platform-tools-v1.54/platform-tools/* platform-tools-v1.54/
  rmdir platform-tools-v1.54/platform-tools || true
fi
# Pretend it's the expected v1.43 so cargo-build-sbf won't reinstall
ln -sfn platform-tools-v1.54 platform-tools-v1.43
ln -sfn platform-tools-v1.43 platform-tools
platform-tools/rust/bin/rustc --version
platform-tools/rust/bin/cargo --version

rustup toolchain uninstall solana 2>/dev/null || true
rustup toolchain link solana "$(pwd)/platform-tools/rust"
rustup run solana cargo --version

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

rm -rf "$HOME/.cargo/registry/src"/*/zeroize_derive-1.5* 2>/dev/null || true
rm -f Cargo.lock
cargo generate-lockfile
echo "zeroize_derive in lock:"
grep -A1 'name = "zeroize_derive"' Cargo.lock || echo "(none)"

echo "=== cargo-build-sbf ==="
# Prevent network reinstall attempts for tools
cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --sbf-sdk "$SDK" --offline -v

echo "=== ANCHOR BUILD DONE ==="
find "$ONCHAIN" -name 'pirate_coop.so' 2>/dev/null
ls -la "$ONCHAIN/target/deploy/" 2>/dev/null || true
