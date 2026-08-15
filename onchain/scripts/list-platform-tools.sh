#!/usr/bin/env bash
set -euo pipefail
curl -sL "https://api.github.com/repos/anza-xyz/platform-tools/releases?per_page=15" \
  | python3 -c 'import sys,json; rs=json.load(sys.stdin); [print(r["tag_name"], r["published_at"][:10]) for r in rs]'
