#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
KEYPAIR="$ONCHAIN/target/deploy/pirate_coop-keypair.json"
mkdir -p "$ONCHAIN/target/deploy"

if [[ ! -f "$KEYPAIR" ]]; then
  solana-keygen new --no-bip39-passphrase --silent -o "$KEYPAIR" --force
fi
PUBKEY=$(solana-keygen pubkey "$KEYPAIR")
echo "Program ID: $PUBKEY"

# Update declare_id and Anchor.toml
sed -i "s/declare_id!(\"[^\"]*\")/declare_id!(\"$PUBKEY\")/" "$ONCHAIN/programs/pirate_coop/src/lib.rs"
# Replace both localnet and devnet program IDs
sed -i "s/pirate_coop = \"[^\"]*\"/pirate_coop = \"$PUBKEY\"/g" "$ONCHAIN/Anchor.toml"

echo "Updated declare_id + Anchor.toml"
grep -n "declare_id\|pirate_coop" "$ONCHAIN/programs/pirate_coop/src/lib.rs" "$ONCHAIN/Anchor.toml" | head -20

cd "$ONCHAIN"
anchor build
echo "=== ANCHOR BUILD DONE ==="
