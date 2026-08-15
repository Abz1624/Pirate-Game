#!/usr/bin/env bash
set -euo pipefail
source "$HOME/.cargo/env"
export PATH="$HOME/.local/share/solana/install/active_release/bin:$HOME/.cargo/bin:$PATH"

SDK="$HOME/.local/share/solana/install/releases/2.1.21/solana-release/bin/sdk/sbf"
DEP="$SDK/dependencies"

echo "=== deps layout ==="
ls -la "$DEP"
echo "=== platform-tools rustc ==="
"$DEP/platform-tools/rust/bin/rustc" --version || true
echo "=== platform-tools-v1.54 rustc ==="
"$DEP/platform-tools-v1.54/rust/bin/rustc" --version || true
echo "=== platform-tools-v1.43 rustc ==="
"$DEP/platform-tools-v1.43/rust/bin/rustc" --version || true
echo "=== rustup solana ==="
rustup run solana rustc --version || true
rustup toolchain list
echo "=== find all cargo 1.79 ==="
find "$HOME/.local/share/solana" "$HOME/.rustup/toolchains" -name cargo -type f 2>/dev/null | while read -r c; do
  echo -n "$c -> "
  "$c" --version 2>/dev/null || echo "?"
done
