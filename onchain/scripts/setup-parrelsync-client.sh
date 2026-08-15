#!/usr/bin/env bash
# Creates/funds player2.json for ParrelSync clone editor co-op testing.
set -euo pipefail
source "$HOME/.cargo/env" 2>/dev/null || true
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$PATH"

PLAYER2="$HOME/.config/solana/player2.json"
RPC="${SOLANA_URL:-http://localhost:8899}"

mkdir -p "$HOME/.config/solana"
if [[ ! -f "$PLAYER2" ]]; then
  solana-keygen new -o "$PLAYER2" --no-bip39-passphrase --force
fi

PUBKEY="$(solana-keygen pubkey "$PLAYER2")"
echo "player2 pubkey: $PUBKEY"

solana config set --url "$RPC" >/dev/null
solana airdrop 10 "$PUBKEY" --url "$RPC" || true
solana balance "$PUBKEY" --url "$RPC"
