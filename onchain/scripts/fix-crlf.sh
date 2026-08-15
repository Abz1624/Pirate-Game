#!/usr/bin/env bash
# Strip CRLF from shell scripts under onchain/scripts (Windows checkout fix).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"
for f in "$ROOT"/*.sh; do
  [ -f "$f" ] || continue
  tr -d '\r' < "$f" > /tmp/crlf-fix.sh
  cp /tmp/crlf-fix.sh "$f"
  chmod +x "$f"
  echo "fixed: $f"
done
file "$ROOT/start-local-stack.sh"
