# Pirate Game — On-Chain Co-op (MagicBlock ER)

Authoritative combat simulation on MagicBlock Ephemeral Rollups.

## Prerequisites

- Rust 1.75+
- Solana CLI 1.18+
- Anchor 0.30.1
- Node 18+
- `npm install -g @magicblock-labs/ephemeral-validator@latest`

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
