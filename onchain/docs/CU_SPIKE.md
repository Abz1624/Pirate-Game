# CU spike results (estimated — verify with `cargo run -p pirate-simcli -- stress-cu`)

| Capacity | Enemies | Projectiles | CU/tick (est.) | CU/player-sec @10Hz |
|----------|---------|-------------|----------------|---------------------|
| 4        | 140     | 240         | ~78,000        | ~780,000            |
| 12       | 200     | 720         | ~217,000       | ~2,170,000          |

Budget: 1.4M CU per transaction (four phase instructions).

Run locally:
```bash
cargo run -p pirate-simcli -- stress-cu --capacity 4
cargo run -p pirate-simcli -- stress-cu --capacity 12
```

Minimum crank interval: confirm with local ER stack (target 100ms for 10Hz).
