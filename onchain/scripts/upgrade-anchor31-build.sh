#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

echo "=== Ensure Solana 2.1.21 active ==="
# Re-activate 2.1.21 if Anchor previously downgraded us
if ! solana --version 2>/dev/null | grep -q '2.1.21'; then
  sh -c "$(curl -sSfL --connect-timeout 30 --max-time 600 https://release.anza.xyz/v2.1.21/install)"
fi
export PATH="$HOME/.local/share/solana/install/active_release/bin:$PATH"
solana --version

echo "=== Install Anchor 0.31.1 (Solana 2.x compatible) ==="
avm install 0.31.1
avm use 0.31.1
anchor --version

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

# Update manifests for Anchor 0.31 + MagicBlock SDK with anchor-compat
python3 - <<'PY'
from pathlib import Path

# program Cargo.toml
p = Path("programs/pirate_coop/Cargo.toml")
p.write_text("""[package]
name = "pirate_coop"
version.workspace = true
description = "Pirate Game co-op match program with MagicBlock ER delegation"
edition.workspace = true

[lib]
crate-type = ["cdylib", "lib"]
name = "pirate_coop"

[features]
no-entrypoint = []
no-idl = []
no-log-ix-name = []
cpi = ["no-entrypoint"]
default = []
idl-build = ["anchor-lang/idl-build"]

[dependencies]
anchor-lang = "0.31.1"
ephemeral-rollups-sdk = { version = "0.2.1", features = ["anchor"] }
""")

# Anchor.toml toolchain pin
at = Path("Anchor.toml")
text = at.read_text()
if "[toolchain]" not in text:
    text = "[toolchain]\nanchor_version = \"0.31.1\"\nsolana_version = \"2.1.21\"\n\n" + text
else:
    # leave existing; we'll rewrite top
    pass
# Force rewrite toolchain header
lines = text.splitlines()
# strip old toolchain block
out = []
skip = False
for line in lines:
    if line.strip() == "[toolchain]":
        skip = True
        continue
    if skip and line.startswith("["):
        skip = False
    if skip and (line.startswith("anchor_version") or line.startswith("solana_version") or line.strip() == ""):
        continue
    if skip:
        skip = False
    if not skip:
        out.append(line)
text = "[toolchain]\nanchor_version = \"0.31.1\"\nsolana_version = \"2.1.21\"\n\n" + "\n".join(out).lstrip() + "\n"
at.write_text(text)
print("Updated Cargo.toml + Anchor.toml")
PY

rm -f Cargo.lock
cargo generate-lockfile

echo "=== anchor build ==="
anchor build
echo "=== ANCHOR BUILD DONE ==="
