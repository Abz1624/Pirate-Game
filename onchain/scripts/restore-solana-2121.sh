#!/usr/bin/env bash
set -euo pipefail
REL="$HOME/.local/share/solana/install/releases/2.1.21/solana-release"
ACTIVE="$HOME/.local/share/solana/install/active_release"
if [[ ! -x "$REL/bin/solana" ]]; then
  echo "2.1.21 release not found at $REL"
  exit 1
fi
ln -sfn "$REL" "$ACTIVE"
export PATH="$HOME/.cargo/bin:$ACTIVE/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
hash -r
echo "solana: $(solana --version)"
echo "which:  $(command -v solana)"
echo "active: $(readlink -f "$ACTIVE")"
