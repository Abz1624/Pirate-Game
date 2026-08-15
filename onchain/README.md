# Pirate Game — On-Chain Co-op (MagicBlock ER)

Authoritative combat simulation on MagicBlock Ephemeral Rollups.

## Prerequisites

- Rust 1.75+
- Agave/Solana CLI 2.1+
- Anchor 0.31.1 (via `avm`)
- `cargo install cargo-build-sbf` (v4.x; uses platform-tools v1.54)
- Node 18+
- `npm install -g @magicblock-labs/ephemeral-validator@latest`

### WSL setup (recommended on Windows)

Full guide: [docs/SETUP_WSL.md](docs/SETUP_WSL.md)

```bash
# From PowerShell / CMD:
wsl -u root -e bash /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain/scripts/setup-wsl-root.sh
wsl -e bash /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain/scripts/setup-wsl-user.sh
wsl -e bash -lc 'source $HOME/.cargo/env; cargo install cargo-build-sbf --locked'

# Verify:
wsl -e bash /mnt/c/Kshitij/Work/X2C/web3/GitHub/Pirate-Game/onchain/scripts/verify-wsl.sh
```

Or open an Ubuntu terminal and run the same scripts from the repo path under `/mnt/c/...`.

## Local development (default)

```bash
# Terminal 1
./scripts/start-local-stack.sh   # or start-local-stack.ps1 on Windows

# Terminal 2
cd onchain
anchor build
anchor deploy --provider.cluster localnet
cargo run -p pirate-simcli -- stress-cu --capacity 4
anchor test --skip-local-validator
```

### Endpoints (local)

| Layer | RPC | WebSocket |
|-------|-----|-----------|
| Base | http://localhost:8899 | ws://localhost:8900 |
| ER | http://localhost:7799 | ws://localhost:7800 |

ER validator: `mAGicPQYBMvcYveUZA5F5UNNwyHvfYh5xkLS2Fr1mev`

## Devnet (release only)

After local stack is fully working:

```bash
solana config set --url devnet
anchor deploy --provider.cluster devnet
# Pin Asia ER: MAS1Dt9qreoRMQ14YQuhg8UTZMMzDdKhmkZMECCzk57
# ER RPC: https://devnet-as.magicblock.app
```

## Structure

- `crates/sim` — pure Rust fixed-point simulation (shared logic)
- `crates/simcli` — headless runner + CU stress + balance sweeps
- `programs/pirate_coop` — Anchor program (lobby, delegation, ticks)
- `tests/` — Anchor integration tests + CU CI gate

## CU budget gate

```bash
cargo test -p pirate-sim stress_4_players_under_budget
cargo test -p pirate-sim stress_12_players_under_budget
```

Fails the build if estimated CU per tick exceeds 1.4M.
