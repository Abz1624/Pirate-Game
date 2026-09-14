# 03 — Deterministic Simulation and Combat

## Runtime contract

- Fixed 30 Hz; integer ticks and entity IDs. Positions/velocities use documented fixed-point units; damage uses hundredths and rounds only for display.
- Named RNG streams: layout, enemy spawn, level offer, chest, combat proc, powerup, permanent reward and cosmetic-only. Persist authoritative states.
- Player baseline: 100 Hull, 6 m/s, 0.8 m radius, 3 m pickup, 5% crit, 1.5× crit, zero armor/regen.
- Steering: acceleration 30 m/s², braking 40, turn 720°/s, normalized diagonals, surface sliding, retained facing.
- Armor multiplier `100/(100+armor)`; player armor cap 150, enemy 100; crit chance 75%, crit multiplier 3×; attack interval floor 0.12 s.
- Bosses receive half normal slow, maximum 20%; immune to knockback/charm/displacement.

## Tick order

1. Expire effects ending this tick; apply committed choices; accept legal input; advance movement/timers.
2. Resolve completed healing pickups.
3. Generate attacks/contact/DOT from eligible actors; already emitted attacks remain valid after source death.
4. Sort damage by propagation generation, source, attack, target and event ID; apply mitigation/immunity.
5. On lethal damage, consume Last Rum once or mark dead. Later healing cannot revive.
6. Resolve bounded procs and deaths; grant each valid kill once.
7. Secure all valid boss deaths, including simultaneous player death.
8. If player dead, cancel interactions/choices and settle death; no portal/level rescue.
9. Otherwise resolve interactions, pickups and XP; open at most one modal.
10. Process portal after choices and renewed interaction validation.

## Pause and lifecycle

Maintain a set of `Manual`, `LevelChoice`, `POIChoice`, `FocusLost`, `Suspended`, `Disconnected`, `Takeover`, `LoadingBarrier`. Any member stops all simulation progress. Clearing one never clears another. Boss entrance is not paused. Require explicit Continue after lifecycle interruption. Unioned competitive pause time is 30 minutes, with warnings at 20/25/29; excess preserves rewards but invalidates score. Continuous 24-hour interruption expires unsecured state.

## Damage/proc budgets

- Direct Weapon hits may crit/proc. DOT, reflected, environment and triggered Item damage do not unless whitelisted.
- Chance uses `clamp(baseStackChance × procCoefficient,0,receiverCap)`.
- One source may emit 64 secondary events and three generations; global secondary cap 2,000/sec.
- On overflow retain nearest targets then lowest IDs. Never drop primary authoritative hits.
- Contact/hazard immunity is 0.5 s; Dread Tide bypasses it.
- Burn: 20% of parent pre-armor noncritical damage over 3 s, 0.5 s ticks, five stacks.
- Bleed: 12% over 4 s, five stacks. Shock: 2 s, +5% lightning damage, nonstacking.
- Use strongest Slow; player floor 70% speed, enemy floor 40%.

## Level offers

XP to next level at level L is `5+2L`; cap 200. Offer category weights are equipped Weapon 45%, new Weapon 20%, equipped Logbook 25%, new Logbook 10%, renormalized after exclusions. Upgrade rarity is 70/22/6.5/1.3/0.2% with multipliers 1/1.2/1.5/1.8/2.2. Fill missing useful offers with 10% Hull heal, +2% damage or +1% movement; show Continue when none helps.

## Required fixtures

Golden replay parity at 30/60/120 render FPS and Unity native/WebAssembly/server; fatal+heal; two fatal hits with one prevention; boss/player double death; DOT boss death; portal+lethal; multiple queued levels; POI+level same tick; reconnect reward duplication; proc saturation; cap retirement; lifecycle pause overlap.

