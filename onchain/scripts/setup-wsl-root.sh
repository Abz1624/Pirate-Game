#!/usr/bin/env bash
# Root portion: system packages + Node.js for Pirate Game WSL setup
set -euo pipefail

echo "==> [root] Installing system packages..."
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y \
  build-essential pkg-config libssl-dev libudev-dev \
  clang cmake curl git ca-certificates \
  protobuf-compiler python3 unzip sudo

echo "==> [root] Installing Node.js 20..."
if ! command -v node >/dev/null 2>&1; then
  curl -fsSL https://deb.nodesource.com/setup_20.x | bash -
  apt-get install -y nodejs
fi

# Allow user shara passwordless sudo for future tooling (WSL convenience)
if id shara >/dev/null 2>&1; then
  echo "shara ALL=(ALL) NOPASSWD:ALL" > /etc/sudoers.d/shara
  chmod 440 /etc/sudoers.d/shara
  echo "==> [root] Passwordless sudo enabled for shara"
fi

echo "Node: $(node -v 2>/dev/null || echo missing)  npm: $(npm -v 2>/dev/null || echo missing)"
echo "==> [root] DONE"
