#!/usr/bin/env bash
set -euo pipefail

# mb-test-validator shells out to solana-test-validator; ensure Solana CLI is on PATH.
export PATH="${HOME}/.local/share/solana/install/active_release/bin:${HOME}/.npm-global/bin:${PATH}"

# Solana validators create a Unix socket (test-ledger/admin.rpc). That cannot
# live on /mnt/c (Windows/DrvFs), so run the ledger on the Linux filesystem.
LEDGER_DIR="${PIRATE_LEDGER_DIR:-$HOME/.pirate-game/local-stack}"
mkdir -p "$LEDGER_DIR"
cd "$LEDGER_DIR"

echo "Ledger directory: $LEDGER_DIR"
echo "Starting mb-test-validator on :8899 ..."
mb-test-validator --reset &
BASE_PID=$!

echo "Waiting for base RPC http://127.0.0.1:8899 ..."
for i in $(seq 1 60); do
  if curl -sS -m 1 http://127.0.0.1:8899 -o /dev/null 2>/dev/null; then
    echo "Base RPC is up."
    break
  fi
  if ! kill -0 "$BASE_PID" 2>/dev/null; then
    echo "mb-test-validator exited before RPC came up."
    echo "If you still see admin.rpc errors, stop this script, then:"
    echo "  cd ~ && mb-test-validator --reset"
    exit 1
  fi
  sleep 2
done

echo "Starting ephemeral-validator on :7799 ..."
RUST_LOG=info ephemeral-validator \
  --no-tui \
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
