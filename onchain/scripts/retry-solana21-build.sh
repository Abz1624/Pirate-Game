#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"

echo "=== Retry install Agave 2.1.21 ==="
# Longer curl timeout via installer env if supported; retry once on failure
if ! sh -c "$(curl -sSfL --connect-timeout 30 --max-time 600 https://release.anza.xyz/v2.1.21/install)"; then
  echo "Retrying install..."
  sleep 5
  sh -c "$(curl -sSfL --connect-timeout 30 --max-time 600 https://release.anza.xyz/v2.1.21/install)"
fi

export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
# Ensure PATH persists
grep -qxF 'export PATH="$HOME/.local/share/solana/install/active_release/bin:$PATH"' "$HOME/.bashrc" \
  || echo 'export PATH="$HOME/.local/share/solana/install/active_release/bin:$PATH"' >> "$HOME/.bashrc"

solana --version
cargo-build-sbf --version || true

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"
rm -f Cargo.lock
cargo generate-lockfile

echo "=== anchor build ==="
anchor build
echo "=== ANCHOR BUILD DONE ==="
