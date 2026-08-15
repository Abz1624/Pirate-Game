#!/usr/bin/env bash
# Install Solana/Anchor/MagicBlock toolchain inside WSL for Pirate Game testing.
set -euo pipefail

ONCHAIN_DIR="/mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain"
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$PATH"

echo "========================================"
echo " Pirate Game WSL toolchain setup"
echo "========================================"
echo "User: $(whoami)  Home: $HOME"
echo "Distro: $(. /etc/os-release && echo "$PRETTY_NAME")"
echo

# ---- apt packages ----
echo "==> [1/6] Installing system packages..."
sudo apt-get update -y
sudo DEBIAN_FRONTEND=noninteractive apt-get install -y \
  build-essential pkg-config libssl-dev libudev-dev \
  clang cmake curl git ca-certificates \
  protobuf-compiler python3 unzip

# ---- Node.js 20 LTS (via NodeSource) ----
echo "==> [2/6] Installing Node.js 20..."
if ! command -v node >/dev/null 2>&1 || [[ "$(node -v | sed 's/v//' | cut -d. -f1)" -lt 18 ]]; then
  curl -fsSL https://deb.nodesource.com/setup_20.x | sudo -E bash -
  sudo DEBIAN_FRONTEND=noninteractive apt-get install -y nodejs
fi
echo "Node: $(node -v)  npm: $(npm -v)"

# ---- Rust ----
echo "==> [3/6] Installing Rust (rustup)..."
if ! command -v rustc >/dev/null 2>&1; then
  curl --proto '=https' --tlsv1.2 -sSf https://sh.rustup.rs | sh -s -- -y
fi
# shellcheck disable=SC1091
source "$HOME/.cargo/env"
rustup default stable
rustup component add rustfmt clippy
echo "Rust: $(rustc --version)  Cargo: $(cargo --version)"

# ---- Solana CLI ----
echo "==> [4/6] Installing Solana CLI..."
if ! command -v solana >/dev/null 2>&1; then
  # Anza installer (current Solana CLI distribution)
  sh -c "$(curl -sSfL https://release.anza.xyz/stable/install)"
fi
export PATH="$HOME/.local/share/solana/install/active_release/bin:$PATH"
# Ensure PATH persists in future shells
if ! grep -q 'solana/install/active_release/bin' "$HOME/.bashrc" 2>/dev/null; then
  echo 'export PATH="$HOME/.local/share/solana/install/active_release/bin:$PATH"' >> "$HOME/.bashrc"
fi
if ! grep -q '.cargo/env' "$HOME/.bashrc" 2>/dev/null; then
  echo 'source "$HOME/.cargo/env"' >> "$HOME/.bashrc"
fi
solana --version
solana config set --url localhost
# Generate a default keypair if missing
if [[ ! -f "$HOME/.config/solana/id.json" ]]; then
  solana-keygen new --no-bip39-passphrase --silent -o "$HOME/.config/solana/id.json" || true
fi

# ---- Anchor 0.30.1 ----
echo "==> [5/6] Installing Anchor CLI v0.30.1..."
# Prefer AVM when available; fall back to cargo install
if ! command -v avm >/dev/null 2>&1; then
  cargo install --git https://github.com/coral-xyz/anchor avm --locked --force
fi
avm install 0.30.1
avm use 0.30.1
# Ensure cargo bin is on PATH for anchor
if ! grep -q 'avm' "$HOME/.bashrc" 2>/dev/null; then
  echo '# Anchor Version Manager' >> "$HOME/.bashrc"
fi
anchor --version

# ---- MagicBlock ephemeral-validator ----
echo "==> [6/6] Installing MagicBlock ephemeral-validator + onchain npm deps..."
sudo npm install -g @magicblock-labs/ephemeral-validator@latest || \
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
echo "ephemeral-validator: $(command -v ephemeral-validator || echo 'check npm global bin')"
npm list -g --depth=0 @magicblock-labs/ephemeral-validator 2>/dev/null || true
echo
echo "Next steps:"
echo "  cd $ONCHAIN_DIR"
echo "  cargo test -p pirate-sim"
echo "  cargo run -p pirate-simcli -- stress-cu --capacity 4"
echo "  # Terminal 1: ./scripts/start-local-stack.sh"
echo "  # Terminal 2: anchor build && anchor deploy --provider.cluster localnet"
echo "========================================"
