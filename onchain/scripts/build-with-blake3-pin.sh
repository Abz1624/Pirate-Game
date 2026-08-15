#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== Using blake3 pin to avoid edition2024 ==="
grep -A5 '\[dependencies\]' programs/pirate_coop/Cargo.toml

rm -rf "$HOME/.cargo/registry/src"/*/block-buffer-0.12* 2>/dev/null || true
rm -f Cargo.lock
cargo generate-lockfile

echo "blake3:"
grep -A1 'name = "blake3"' Cargo.lock | head -6
echo "block-buffer versions:"
grep -A1 'name = "block-buffer"' Cargo.lock

# Ensure platform-tools v1.43 is present (already downloaded)
PT="$HOME/.local/share/solana/install/active_release/bin/sdk/sbf/dependencies/platform-tools"
if [[ ! -x "$PT/rust/bin/rustc" ]]; then
  echo "platform-tools missing — extracting cached v1.43"
  mkdir -p "$PT"
  tar -xjf "$HOME/.cache/solana/platform-tools-v1.43.tar.bz2" -C "$PT"
fi
"$PT/rust/bin/rustc" --version

echo "=== anchor build ==="
anchor build
echo "=== ANCHOR BUILD DONE ==="
