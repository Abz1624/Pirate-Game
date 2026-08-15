#!/usr/bin/env bash
# User portion: Rust, Solana, Anchor, MagicBlock for Pirate Game testing
set -euo pipefail

ONCHAIN_DIR="/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain"

echo "========================================"
echo " Pirate Game WSL user toolchain"
echo "========================================"
echo "User: $(whoami)  Home: $HOME"
echo

# Ensure cargo/solana on PATH for this session and future shells
ensure_bashrc() {
  local line="$1"
  grep -qxF "$line" "$HOME/.bashrc" 2>/dev/null || echo "$line" >> "$HOME/.bashrc"
}

# ---- Rust ----
echo "==> [1/4] Installing Rust (rustup)..."
if ! command -v rustc >/dev/null 2>&1; then
  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y
fi
# shellcheck disable=SC1091
source "$HOME/.cargo/env"
rustup default stable
rustup component add rustfmt clippy
ensure_bashrc 'source "$HOME/.cargo/env"'
echo "Rust: $(rustc --version)"

# ---- Solana CLI ----
echo "==> [2/4] Installing Solana CLI..."
export PATH="$HOME/.local/share/solana/install/active_release/bin:$PATH"
if ! command -v solana >/dev/null 2>&1; then
  sh -c "$(curl -sSfL https://release.anza.xyz/stable/install)"
fi
export PATH="$HOME/.local/share/solana/install/active_release/bin:$PATH"
ensure_bashrc 'export PATH="$HOME/.local/share/solana/install/active_release/bin:$PATH"'
solana --version
solana config set --url localhost
mkdir -p "$HOME/.config/solana"
if [[ ! -f "$HOME/.config/solana/id.json" ]]; then
  solana-keygen new --no-bip39-passphrase --silent -o "$HOME/.config/solana/id.json"
fi
echo "Solana pubkey: $(solana-keygen pubkey "$HOME/.config/solana/id.json")"

# ---- Anchor 0.30.1 via AVM ----
echo "==> [3/4] Installing Anchor CLI v0.30.1 (this takes several minutes)..."
export PATH="$HOME/.cargo/bin:$PATH"
if ! command -v avm >/dev/null 2>&1; then
  cargo install --git https://github.com/coral-xyz/anchor avm --locked --force
fi
avm install 0.30.1
avm use 0.30.1
anchor --version

# ---- MagicBlock + npm deps ----
echo "==> [4/4] Installing MagicBlock ephemeral-validator + onchain npm deps..."
# Prefer user-global npm prefix to avoid sudo
mkdir -p "$HOME/.npm-global"
npm config set prefix "$HOME/.npm-global"
ensure_bashrc 'export PATH="$HOME/.npm-global/bin:$PATH"'
export PATH="$HOME/.npm-global/bin:$PATH"
npm install -g @magicblock-labs/ephemeral-validator@latest

cd "$ONCHAIN_DIR"
if [[ -f package.json ]]; then
  npm install
fi

echo
echo "========================================"
echo " Setup complete — versions"
echo "========================================"
echo "node:    $(node -v)"
echo "npm:     $(npm -v)"
echo "rustc:   $(rustc --version)"
echo "cargo:   $(cargo --version)"
echo "solana:  $(solana --version)"
echo "anchor:  $(anchor --version)"
npm list -g --depth=0 @magicblock-labs/ephemeral-validator 2>/dev/null || true
echo
echo "Test commands:"
echo "  cd $ONCHAIN_DIR"
echo "  cargo test -p pirate-sim"
echo "  cargo run -p pirate-simcli -- stress-cu --capacity 4"
echo "========================================"
