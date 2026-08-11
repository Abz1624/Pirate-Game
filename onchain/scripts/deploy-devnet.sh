#!/usr/bin/env bash
# Deploy to devnet ONLY after local stack validation is complete.
set -euo pipefail

echo "=== Devnet deployment (release) ==="
solana config set --url devnet

cd "$(dirname "$0")/.."
anchor build
anchor deploy --provider.cluster devnet

cat <<'EOF'

Post-deploy checklist:
  1. Pin ER validator: MAS1Dt9qreoRMQ14YQuhg8UTZMMzDdKhmkZMECCzk57
  2. ER RPC: https://devnet-as.magicblock.app
  3. ER WS:  wss://devnet-as.magicblock.app
  4. Unity BlockchainConfig.mode = Devnet
  5. Verify session-key lamports top-up requirement
  6. Measure snapshot bandwidth + RTT from target region

EOF
