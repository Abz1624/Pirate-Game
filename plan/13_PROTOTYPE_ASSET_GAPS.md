# Prototype asset gap register (V3 Saltwind)

**Purpose:** Track production art/audio still missing from the V3 prototype. Every row maps to a tagged placeholder in the Unity scene so art can be swapped without hunting through code.

**How placeholders work**

- World/POI/pickup objects are spawned by `V3PlaceholderFactory` and tagged `PLACEHOLDER_v3_{category}_{assetId}` via `PlaceholderMarker`.
- Search the hierarchy or project for `PLACEHOLDER_v3` to list every stand-in.
- Replace the GameObject (or assign a prefab reference once a factory hook exists) and remove `PlaceholderMarker` when the production asset is integrated.

**Status vocabulary**

| Status | Meaning |
|---|---|
| `PLACEHOLDER` | Colored primitive or generic UI in use |
| `NEEDED` | No approved production asset; placeholder required until G2 |
| `CANDIDATE` | File exists in project but not validated (see Appendix C) |

---

## Floor powerups (M2-024 / RP-CONT-014)

| Asset ID | Display name | Placeholder tag | Status | Production deliverable |
|---|---|---|---|---|
| `floor_powerup_repair_supplies` | Repair Supplies | `PLACEHOLDER_v3_pickup_floor_powerup_repair_supplies` | PLACEHOLDER | Green supply crate mesh + collect burst VFX/SFX |
| `floor_powerup_battle_signal` | Battle Signal | `PLACEHOLDER_v3_pickup_floor_powerup_battle_signal` | PLACEHOLDER | Red signal flare mesh + short buff aura |
| `floor_powerup_tailwind` | Tailwind | `PLACEHOLDER_v3_pickup_floor_powerup_tailwind` | PLACEHOLDER | Wind swirl mesh + speed trail tint |
| `floor_powerup_salvage_chime` | Salvage Chime | `PLACEHOLDER_v3_pickup_floor_powerup_salvage_chime` | PLACEHOLDER | Bell/chime mesh + magnet-pull SFX |

---

## POI kits (M2 finite exploration)

| Asset ID | POI type | Placeholder tag | Status | Production deliverable |
|---|---|---|---|---|
| `poi_salvage_chest` | Salvage Chest | `PLACEHOLDER_v3_poi_poi_salvage_chest` | PLACEHOLDER | Closed/open/consumed chest kit |
| `poi_repair_wreck` | Repair Wreck | `PLACEHOLDER_v3_poi_poi_repair_wreck` | PLACEHOLDER | Wreck hull + repair VFX |
| `poi_signal_buoy` | Signal Buoy | `PLACEHOLDER_v3_poi_poi_signal_buoy` | PLACEHOLDER | Buoy + charge ring telegraph |
| `poi_treasury_beacon` | Treasury Beacon | `PLACEHOLDER_v3_poi_poi_treasury_beacon` | PLACEHOLDER | Beacon vault + boss summon VFX |
| `poi_smugglers_oath` | Smuggler's Oath (Greed shrine) | `PLACEHOLDER_v3_poi_poi_smugglers_oath` | PLACEHOLDER | Shrine / lighthouse variant |
| `poi_powder_cache` | Powder Cache | — | NEEDED | Combat arena marker + cache prop (uses ambush spawn, no dedicated mesh yet) |
| `poi_treasure_chart` | Treasure Chart | — | NEEDED | Chart scroll / idol prop for CursedIdol mapping |

---

## Combat and enemies

| Asset ID | Role | Placeholder / code path | Status | Production deliverable |
|---|---|---|---|---|
| `enemy_cannonboat` | Cannonboat | `GameDatabase` + pooled view | CANDIDATE | Readable small vessel + cannon tell |
| `enemy_barnacle_king` | Barnacle King boss | `barnacle_king` definition | CANDIDATE | Boss rig, phase tells, death |
| `projectile_hostile_cannon` | Enemy cannonball | `HostileProjectileWorld` primitive | PLACEHOLDER | Cannonball mesh + splash |
| `elite_hammerhead_m3` | Minute-3 elite | Pressure beat spawn | CANDIDATE | Elite scale/VFX treatment |
| `overtime_pursuer` | Drowned Pursuer | `V3OvertimeDirector` | NEEDED | Distinct OT threat silhouette |
| `overtime_harrier` | Drowned Harrier | `V3OvertimeDirector` | NEEDED | Ranged OT tell + projectile |

---

## Player ship and weapons

| Asset ID | Role | Placeholder / code path | Status | Production deliverable |
|---|---|---|---|---|
| `ship_player_cutlass_sloop` | Player ship | Scene `CutlassSloop` / bootstrap | CANDIDATE | Approved starter hull kit |
| `weapon_deck_cannons` | Deck Cannons VFX | `WeaponController` + juice | CANDIDATE | Muzzle flash, projectile, impact |
| `weapon_flying_cutlasses` | Flying Cutlasses | Locked until conquest | NEEDED | Orbiting blade meshes |
| `wake_player` | Ship wake | Scene water | CANDIDATE | Stylized wake VFX |

---

## World and environment

| Asset ID | Role | Placeholder / code path | Status | Production deliverable |
|---|---|---|---|---|
| `layout_saltwind_island` | Saltwind island layout | `V3SaltwindIslandLayout` | PLACEHOLDER | Modular 48 m section templates |
| `storm_edge_visual` | Dread storm boundary | `V3StormEdgeVisual` | PLACEHOLDER | Storm wall mesh + lightning |
| `water_saltwind` | Ocean surface | Scene water shader | CANDIDATE | Single approved URP water treatment |
| `minimap_fog` | Minimap unexplored fog | `V3MinimapHud` | PLACEHOLDER | Painted fog-of-war texture |

---

## UI (prototype panels)

| Asset ID | Screen / widget | Placeholder tag prefix | Status | Production deliverable |
|---|---|---|---|---|
| `ui_run_hud` | Run HUD chrome | `PLACEHOLDER_v3_ui_*` | PLACEHOLDER | Wireframe-approved HUD kit |
| `ui_level_up_modal` | Level-up choice modal | `LevelUpModal` | PLACEHOLDER | Card frames, rarity frames |
| `ui_run_results` | Death / results screen | `V3RunResultsScreen` | PLACEHOLDER | Results layout + conquest badge |
| `ui_main_menu_collection` | Main menu collection | `V3MainMenuCollectionView` | PLACEHOLDER | Captain/weapon gallery art |

---

## Audio (all NEEDED — no approved clips in prototype)

| Asset ID | Trigger | Status | Production deliverable |
|---|---|---|---|
| `sfx_cannon_fire` | Player/enemy cannons | NEEDED | Layered cannon shots |
| `sfx_pickup_infamy` | XP collect | NEEDED | Short sparkle ping |
| `sfx_floor_powerup` | Floor powerup collect | NEEDED | Per-kind collect sting |
| `sfx_boss_summon` | Beacon activated | NEEDED | Boss entrance sting |
| `sfx_storm_edge` | Out-of-bounds damage | NEEDED | Storm rumble loop |
| `music_run_saltwind` | V3 run loop | NEEDED | Combat exploration stem |

---

## Replacement workflow for art team

1. Find the placeholder tag in Unity (`PLACEHOLDER_v3_…`).
2. Note `PlaceholderMarker.ReplacementHint` on the component.
3. Import production prefab; keep transform/socket alignment.
4. Swap via prefab reference in the owning factory or scene.
5. Remove `PlaceholderMarker` and update this table to `INTEGRATED`.

Cross-reference: full production checklist in [Appendix C](appendices/C_ASSET_CHECKLIST.md). Machine-readable specs: [placeholder_registry.json](placeholder_registry.json). G2 exit requires licensed assets for all `NEEDED` rows above.
