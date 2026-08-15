#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"

PT="$HOME/.local/share/solana/install/active_release/bin/sdk/sbf/dependencies/platform-tools"
echo "=== Current platform-tools ==="
ls -la "$PT"
cat "$PT/version.md" 2>/dev/null || echo "no version.md"
"$PT/rust/bin/rustc" --version

# cargo-build-sbf for Solana 2.1 expects v1.43 in version.md — lie so it skips reinstall
# while keeping the newer 1.54 toolchain binaries
echo "v1.43" > "$PT/version.md"
# Also common cache location
mkdir -p "$HOME/.cache/solana/v1.43"
# Some versions look here:
if [[ -d "$HOME/.cache/solana/platform-tools" ]]; then
  echo "cache platform-tools exists"
fi

# Clear any half-downloaded install that causes EEXIST
rm -rf "$HOME/.cache/solana/platform-tools-tmp" \
       "$HOME/.local/share/solana/install/releases"/*/solana-release/bin/sdk/sbf/dependencies/platform-tools.tmp* \
       2>/dev/null || true

ONCHAIN=/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
cd "$ONCHAIN"

echo "=== Direct cargo-build-sbf ==="
cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml --tools-version v1.43 2>&1 || \
cargo-build-sbf --manifest-path programs/pirate_coop/Cargo.toml 2>&1

echo "=== ANCHOR BUILD DONE ==="
ls -la target/deploy/ 2>/dev/null || ls -la programs/pirate_coop/target/deploy/ 2>/dev/null || true
