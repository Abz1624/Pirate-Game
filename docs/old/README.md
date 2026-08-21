# Tides of Plunder — Design Documentation

A survivor-like naval action game built on the [Pirate Nation](https://github.com/proofofplay/piratenation-art) art library, with single-player, co-op, and PvPvE modes and an ORE-style fixed-emission token economy.

Working title. Not final.

## Status

Design phase. No engine has been chosen, no code has been written, and the token layer is deliberately deferred. These documents exist to make those decisions deliberately rather than by accident.

## Reading order

Read `01` and `02` first regardless of what you came here for. Everything else assumes you know what the game is and what art already exists.

| Doc | What it covers | Read it if |
| --- | --- | --- |
| [01 — Game Overview](01-game-overview.md) | Pillars, core fantasy, the three modes, session lengths, audience | You are new to the project |
| [02 — Asset Audit and Gaps](02-asset-audit-and-gaps.md) | Every art category mapped to a game system, with paths; plus the production backlog | You are planning art, or want to know what is free versus what costs money |
| [03 — Core Combat and Progression](03-core-combat-and-progression.md) | Broadside Doctrine, weapon and passive slots, evolutions, elements, the level-up draft | You are building or balancing gameplay |
| [04 — Solo and Co-op](04-modes-solo-and-coop.md) | Dungeon Hunt run structure, Tide Levels, co-op convoy and revive | You are building PvE |
| [05 — PvPvE: Bounty Waters](05-mode-pvpve.md) | Heat, Plunder, the dual-track draft, flashpoints, the storm, spectacle | You are building multiplayer |
| [06 — Economy and Tokenomics](06-economy-and-tokenomics.md) | Dual currency, fixed emission, Contribution Score, sybil resistance, sinks | You are working on the economy or talking to investors |
| [07 — Economy Simulation Spec](07-economy-simulation-spec.md) | Parameters, starting values, and the stress tests to run before committing | You are about to build the economy simulation |
| [08 — Engine and Tech](08-engine-and-tech.md) | Unity vs Defold vs Godot vs web, asset pipeline, Quantum + SpacetimeDB netcode split | You are choosing the stack |
| [09 — Roadmap](09-roadmap.md) | Milestones, asset backlog priorities, prototype ordering | You are planning the schedule |

## The one-paragraph version

You captain a voxel pirate ship viewed top-down. Your cannons fire automatically from the port and starboard sides, so **turning to present your broadside is aiming** — movement and targeting are the same input. Waves of sea monsters escalate for fifteen minutes; every level-up offers three power-up cards and you pick one. Solo and co-op are dungeon runs for gold, which buys ship cosmetics from a library of hundreds. PvPvE drops eight to twelve captains on one small map where monsters are safe XP and other captains are dangerous XP, and where dying makes you drop what you banked. A fixed-emission token rewards participation proportionally, so no amount of grinding can inflate the supply.

## Conventions used in these docs

- Asset paths are relative to `references/piratenation-art-full/` unless stated otherwise. That directory is gitignored; see [references/README.md](../references/README.md) for how it was assembled.
- **Bold** marks a design term with a specific meaning in this project (Heat, Plunder, Tide Level, Contribution Score). These are capitalised consistently.
- Numbers in brackets like `[TUNE]` are placeholders that need playtesting, not decisions that have been made.
- Where a design is contentious or has a known cost, there is an explicit "the tradeoff" or "open question" callout. Nothing load-bearing is buried.

## Browsing the art

There is a local asset browser at [tools/asset-browser/serve.mjs](../tools/asset-browser/serve.mjs):

```bash
node tools/asset-browser/serve.mjs
```

It serves a filterable grid of every model, image, and video in the library at `http://127.0.0.1:8765/`, with a `model-viewer` lightbox for GLTF files.
