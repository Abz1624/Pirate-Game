#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

echo "solana: $(solana --version)"
echo "anchor: $(anchor --version)"

# Find where cargo-build-sbf expects platform-tools
SBF_SDK=$(dirname "$(dirname "$(readlink -f "$(which cargo-build-sbf)")")")/sdk/sbf || true
# Common location
PT_BASE="$HOME/.cache/solana"
mkdir -p "$PT_BASE"

# Also check active release SDK path
ACTIVE="$HOME/.local/share/solana/install/active_release/bin/sdk/sbf/dependencies"
mkdir -p "$ACTIVE"

URL="https://github.com/anza-xyz/platform-tools/releases/download/v1.43/platform-tools-linux-x86_64.tar.bz2"
ARCHIVE="$PT_BASE/platform-tools-v1.43.tar.bz2"

echo "=== Download platform-tools v1.43 (long timeout) ==="
if [[ ! -f "$ARCHIVE" ]]; then
  curl -L --retry 5 --retry-delay 5 --connect-timeout 30 --max-time 1200 \
    -o "$ARCHIVE" "$URL"
fi
ls -lh "$ARCHIVE"

echo "=== Extract into Solana SDK dependencies ==="
DEST="$ACTIVE/platform-tools"
rm -rf "$DEST"
mkdir -p "$DEST"
tar -xjf "$ARCHIVE" -C "$DEST" --strip-components=0
# Some archives extract into a top-level folder; normalize
if [[ -d "$DEST/platform-tools" ]]; then
  shopt -s dotglob
  mv "$DEST/platform-tools"/* "$DEST"/
  rmdir "$DEST/platform-tools"
fi
# Expected layout includes rust/lib
ls -la "$DEST" | head -20
test -d "$DEST/rust/lib" -o -d "$DEST/rust" && echo "rust toolchain present" || echo "WARNING: rust dir missing — listing:"; find "$DEST" -maxdepth 3 -type d | head -40

# Pin SDK exactly
ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"
sed -i 's/ephemeral-rollups-sdk = { version = "0.2.1"/ephemeral-rollups-sdk = { version = "=0.2.1"/' programs/pirate_coop/Cargo.toml || true
# Ensure exact pin line
grep ephemeral-rollups-sdk programs/pirate_coop/Cargo.toml

rm -f Cargo.lock
cargo generate-lockfile

echo "=== anchor build ==="
anchor build
echo "=== ANCHOR BUILD DONE ==="
