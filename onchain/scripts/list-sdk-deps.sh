#!/usr/bin/env bash
set -euo pipefail
for ver in 0.2.1 0.6.5 0.15.5 0.16.2; do
  echo "=== $ver ==="
  curl -sL "https://crates.io/api/v1/crates/ephemeral-rollups-sdk/${ver}/dependencies" -o /tmp/er-deps.json
  python3 -c 'import json; d=json.load(open("/tmp/er-deps.json"));
[print(" ", dep["crate_id"], dep["req"], "opt="+str(dep.get("optional"))) for dep in d.get("dependencies",[]) if dep["crate_id"] in ("anchor-lang","solana-program","solana-system-interface") or "anchor" in dep["crate_id"]]'
done
