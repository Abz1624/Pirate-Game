#!/usr/bin/env bash
set -euo pipefail

echo "Starting mb-test-validator on :8899 ..."
mb-test-validator --reset &
sleep 5

echo "Starting ephemeral-validator on :7799 ..."
RUST_LOG=info ephemeral-validator \
  --remotes "http://localhost:8899" \
  --remotes "ws://localhost:8900" \
  -l "7799" \
  --lifecycle ephemeral &

cat <<'EOF'

Local stack endpoints:
  Base layer RPC:  http://localhost:8899
  Base layer WS:   ws://localhost:8900
  ER RPC:          http://localhost:7799
  ER WS:           ws://localhost:7800
  ER validator:    mAGicPQYBMvcYveUZA5F5UNNwyHvfYh5xkLS2Fr1mev

EOF

wait
