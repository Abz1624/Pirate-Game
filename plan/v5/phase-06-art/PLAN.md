# V5 M6: art guide (phase note)

Plan: `V5_PLAN.md` "#### M6". Draft: `ART_GUIDE.md` (markdown; the deliverable is a Claude Doc built from it). No code,
asset or Unity file was changed.

## Covered

| Plan item | Where in the guide |
|---|---|
| D6.1 style rules, telegraph language, WebGL mobile budgets | sections 1-2 (budgets from `research/DEVICE_TARGETS.md`, measured costs from `phase-03e-performance/`) |
| D6.1 tables per category | 3 weapons and effects (10 built, 4 M4 on placeholders, 5 M4b), 4 evolutions (6 + 8 M4 + 5 M4b), 5 items (19 + 8 M4 + 20 M5), 6 logbooks (10 + 6 M5), 7 powerups (8) and pickups, 8 POIs (8 types + 3 designed + boss arrival), 9 enemies (16 + 10 variants, shared-hull list, later biomes), 10 bosses, 13 defenses, 14 UI icons, captains |
| D6.2 island decoration catalogue (request 22) | section 11 (38 entries from plan section 7, with candidates) |
| D6.3 sail and flag canvas spec, `Sail.001` UV layout | section 12 (UV read from `Assets/Mesh/Sail.001.asset`) |
| D6.4 brand slots | section 15 (proposed sizes) |
| Register items | R-89 flamethrower redo (owner's VFX artist), R-104 burning-trail pink material |

Every content id in `Data/Weapons`, `Evolutions`, `Build/Items`, `Build/Logbooks`, `Enemies`, `Variants`,
`Build/Captains`, every powerup kind and POI type appears in the guide (checked by script, 1 Oct).

## How the numbers were taken

Triangle counts: read from the model files (FBX polygon indices, Unity mesh assets' index counts), the whole file at
LOD0; the Salvage Chest's 77,650 matches the M3e measurement. Particle figures: sums of the prefabs' authored
`maxParticles` and particle-system counts (caps, not live counts). Prefab GUIDs resolved through the `.meta` files.
The Unity Editor was busy (main-thread timeouts), so nothing was measured in the Editor.

## Unknown or to confirm

- The sail's left-right mirroring (computed from the mesh and the sail's +90° rotation; confirm with a test texture in
  M25) and the enemy sail mesh `Assets/Mesh/Sail.asset` UV (not measured).
- The water mesh (`Prefabs/VFX/Water.prefab`, OceanGrid_LOD0 131,072 triangles, in the run scene): how much is drawn per
  frame; for M7.
- Names and numbers of the M4b weapons' evolutions and items; M5 item ids are proposed.
- Decoration footprints (S/M/L), brand-slot sizes and the elite affix colours are proposals for the owner.
- Rights and provenance of the candidate models (asset checklist rule) were not checked.
- M4 proper was being built in the working tree while this was written; its weapon, evolution and item rows reflect the
  uncommitted state on 1 Oct.
