#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

ACTIVE="$HOME/.local/share/solana/install/active_release/bin/sdk/sbf/dependencies"
CACHE="$HOME/.cache/solana"
mkdir -p "$CACHE" "$ACTIVE"

VER=v1.54
URL="https://github.com/anza-xyz/platform-tools/releases/download/${VER}/platform-tools-linux-x86_64.tar.bz2"
ARCHIVE="$CACHE/platform-tools-${VER}.tar.bz2"

echo "=== Download platform-tools ${VER} ==="
if [[ ! -f "$ARCHIVE" ]]; then
  curl -L --retry 5 --retry-delay 5 --connect-timeout 30 --max-time 1200 -o "$ARCHIVE" "$URL"
fi
ls -lh "$ARCHIVE"

echo "=== Install into SDK ==="
DEST="$ACTIVE/platform-tools"
rm -rf "$DEST"
mkdir -p "$DEST"
tar -xjf "$ARCHIVE" -C "$DEST"
# normalize nested folder if present
if [[ -d "$DEST/platform-tools" ]]; then
  shopt -s dotglob
  mv "$DEST/platform-tools"/* "$DEST"/
  rmdir "$DEST/platform-tools" || true
fi
"$DEST/rust/bin/rustc" --version
"$DEST/rust/bin/cargo" --version

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

# wipe edition2024 crates that confuse old caches
rm -rf "$HOME/.cargo/registry/src"/*/block-buffer-0.12* 2>/dev/null || true

rm -f Cargo.lock
cargo generate-lockfile
# Confirm blake3 pin stuck
grep -A1 'name = "blake3"' Cargo.lock | head -6
grep -A1 'name = "block-buffer"' Cargo.lock | head -20

echo "=== cargo-build-sbf / anchor build ==="
# Avoid Anchor reinstalling older platform-tools: call build-sbf directly first
anchor build || cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml
echo "=== ANCHOR BUILD DONE ==="
