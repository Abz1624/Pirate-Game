# WSL setup (Ubuntu) — Pirate Game on-chain

Recommended path for Solana/Anchor/MagicBlock testing on Windows.

## One-time install

From PowerShell:

```powershell
# System packages (as root)
wsl -u root -e bash /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain/scripts/setup-wsl-root.sh

# User toolchain (Rust, Solana, Anchor, MagicBlock)
wsl -e bash /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain/scripts/setup-wsl-user.sh

# Modern BPF builder (required for current crates.io + platform-tools v1.54)
wsl -e bash -lc 'source $HOME/.cargo/env; cargo install cargo-build-sbf --locked'
```

If `platform-tools` download times out during first `cargo-build-sbf`, pre-fetch:

```bash
mkdir -p ~/.cache/solana
curl -L --retry 5 --max-time 1200 \
  -o ~/.cache/solana/platform-tools-v1.54.tar.bz2 \
  https://github.com/anza-xyz/platform-tools/releases/download/v1.54/platform-tools-linux-x86_64.tar.bz2
```

## Verify sim

```powershell
wsl -e bash /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain/scripts/verify-wsl.sh
```

Expected: `pirate-sim` tests pass, `stress-cu` under budget.

## Build the on-chain program

```bash
source ~/.cargo/env
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain

avm use 0.30.1
./scripts/rebuild-program.sh
# → target/deploy/pirate_coop.so
```

Working stack for BPF:

| Piece | Version |
|-------|---------|
| Anchor | 0.30.1 |
| ephemeral-rollups-sdk | 0.2.1 |
| cargo-build-sbf | 4.1.0 |
| platform-tools | v1.54 (`--arch v0` for Agave 2.1 local verifier) |

`--arch v1` produces dynamic stack frames (writes `r10`). Local `mb-test-validator` is Agave **2.1.21** and rejects that with `cannot write into register r10 (insn #0)`. Rebuild with `./scripts/rebuild-program.sh` (v0), then deploy.

## Local MagicBlock stack + deploy

**Terminal 1 — validators**

The ledger must live on the Linux filesystem (`~/.pirate-game/local-stack`), not `/mnt/c`. Unix sockets like `test-ledger/admin.rpc` cannot be created on the Windows mount.

If a previous run is still spinning on `admin.rpc`, stop it with Ctrl+C first.

```bash
source ~/.cargo/env
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$HOME/.npm-global/bin:$PATH"
cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain
./scripts/start-local-stack.sh
```

Endpoints:

| Layer | RPC | WebSocket |
|-------|-----|-----------|
| Base | http://localhost:8899 | ws://localhost:8900 |
| ER | http://localhost:7799 | ws://localhost:7800 |

**Terminal 2 — deploy**

```bash
source ~/.cargo/env
export PATH="$HOME/.cargo/bin:$HOME/.local/share/solana/install/active_release/bin:$HOME/.avm/bin:$HOME/.npm-global/bin:$PATH"
cd /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain

solana config set --url localhost
solana airdrop 10 || true
avm use 0.30.1

# Prefer direct deploy of the .so we already built:
solana program deploy target/deploy/pirate_coop.so \
  --program-id target/deploy/pirate_coop-keypair.json

# Or full Anchor path (may re-trigger SBF build):
# anchor deploy --provider.cluster localnet
```

Then:

```bash
# Once: copy stub IDL so Anchor's log streamer can find it
mkdir -p target/idl && cp idls/pirate_coop.json target/idl/pirate_coop.json

npm install   # once; ts-mocha lives in onchain/node_modules
anchor test --skip-local-validator --skip-build --skip-deploy
cargo run -p pirate-simcli --release -- stress-cu --capacity 4
```

`anchor test` looks for `target/idl/pirate_coop.json` even with `--skip-build`. We build the `.so` with `cargo-build-sbf`, which does not emit an IDL, so a stub lives in `idls/`.

## Versions on this machine

| Tool | Version |
|------|---------|
| Ubuntu (WSL2) | 26.04 |
| Node | 20.x |
| Rust | stable (host) |
| Agave/Solana CLI | 2.1.21 |
| Anchor CLI | 0.30.1 (via avm) |
| cargo-build-sbf | 4.1.0 (`~/.cargo/bin`) |
| platform-tools | v1.54 |
| ephemeral-validator / mb-test-validator | npm global |

Put `~/.cargo/bin` **before** Solana’s `active_release/bin` on `PATH`.

## Next after local deploy

1. Wire Unity `ErClient` to `localhost:7799` / `ws://localhost:7800`
2. Scene bootstrap: `CoopMatchBootstrap`, `NetworkedShipController`
3. Only then switch `BlockchainConfig` to MagiBlock Asia ER / devnet
