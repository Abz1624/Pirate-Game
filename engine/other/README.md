# Tides of Plunder — Web Prototype

A browser-based prototype of the core game loop, built with **Three.js** and **Vite**. This lives in `engine/other/` as the non-Unity alternative described in [docs/08-engine-and-tech.md](../../docs/08-engine-and-tech.md).

## What this implements

This is Spike #3 from the tech doc — a playable proof that **Broadside Doctrine** is fun:

- **Movement is aiming** — cannons fire from port and starboard; steering presents your broadside to enemies
- **Momentum-based ship controls** — WASD / arrow keys, no aim stick or fire button
- **Enemy waves** — swarmers, chargers, bruisers, elites with elemental variants
- **Auto-firing weapon arcs** — broadside, chaser, deck, and orbital families
- **XP orbs** with magnetized pickup
- **Level-up draft** — pick 1 of 3 cards (weapons, passives, upgrades)
- **Tide Levels 1–4** — scaling enemy density, HP, and rewards
- **Element affinity** — damage multipliers with floating combat numbers

## Quick start

```bash
cd engine/other
npm install
npm run dev
```

Open http://localhost:5173

## Controls

| Input | Action |
| --- | --- |
| W / ↑ | Steer north |
| S / ↓ | Steer south |
| A / ← | Steer west |
| D / → | Steer east |

There is no fire button. Turn so enemies are to your port or starboard side.

## Project structure

```
src/
  main.ts          Entry point
  game.ts          Main game loop and state machine
  types.ts         Hull/weapon/passive definitions (from design docs)
  ship.ts          Player ship with momentum physics
  enemies.ts       Enemy spawning and AI archetypes
  combat.ts        Broadside Doctrine firing logic
  projectiles.ts   Projectiles and XP orbs
  draft.ts         Level-up card draft system
  scene.ts         Three.js scene, camera, effects
  ui.ts            HUD, menu, draft, game-over screens
  input.ts         Keyboard input
```

## Asset loading

The prototype uses procedural geometry (voxel-style ship and icosahedron enemies) for fast iteration. GLTF assets from the Pirate Nation library can be loaded directly — they embed textures as base64 and need no preprocessing. Configure Vite's `fs.allow` in `vite.config.ts` to point at the asset directories.

## What's not here yet

- Co-op / PvPvE networking
- GLTF model loading (placeholder geometry only)
- Boss fights, descend/extract flow
- Contribution Score / economy layer
- Mobile touch controls

These are intentionally out of scope for this spike.
