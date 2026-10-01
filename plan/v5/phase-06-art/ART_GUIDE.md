# Rogue Pirates: Art Guide (V5 M6, 1 Oct 2026)

Hand-off for the art and VFX team: the style rules, the mobile-web budgets, and for every piece of content what it does
in play, how it should read, what exists today and what is missing. Plan: `plan/v5/V5_PLAN.md` "#### M6". The markdown source is
`plan/v5/phase-06-art/ART_GUIDE.md`.

How to read it:

- **Paths** are under `Pirate Game Unity/` unless they start with `plan/`. "Data" means `Assets/_Game/V4/Data/`.
- **Status**: **exists** (production-style art in the game, maybe over budget), **placeholder** (a primitive, a borrowed
  model, a tint of another model or a reused effect), **needed** (nothing in the project).
- **Priority**: **P0** hurts readability or the phone budget today; **P1** needed for the next content phases (M4, M4b,
  M5, M7); **P2** later phases (M14 island, M25 sails, M27 brands); **P3** later biomes and polish.
- **Triangles** were counted from the model files in this repo (FBX polygon indices or Unity mesh assets), the whole
  model file at LOD0; a prefab may draw less. **Particles** are the sum of the effect's authored `maxParticles` caps (an
  upper bound, not a live count) and the number of particle systems ("PS").
- Gameplay ships on placeholders tagged `PLACEHOLDER_v5_*` (plan D13); this guide is what replaces them.

---

## 1. Style rules

### 1.1 Look

| Rule | Detail |
|---|---|
| Stylized low-poly | Flat or gently graded colour, chunky shapes, bevels only where they catch light. The islands already use a 128×128 colour atlas (`Assets/Texture2D/ColorAtlas.png`, `Assets/Material/Coloratlas.mat`, 23 prefabs): new props and decorations should sample the same palette atlas rather than carry their own textures. |
| Saturated, warm sea | Turquoise shallows, deep teal water, sand, green palms; the player and rewards warm (gold, cream, red sails), enemies cooler or dirtier, hazards hot (orange, red). |
| Readable at the game camera | Top-down follow camera, scene offset (0, 25, -20), pitch about 51-56° (`plan/v5/phase-03e-performance/AUDIT.md`); the view spans about 32 m around the ship, up to ~55-60 m at boost zoom. Design every model to read from **above and slightly behind**: the top silhouette carries the identity, not the side profile. Check every asset in a screenshot at gameplay scale on a phone (6 inch, portrait and landscape). |
| One silhouette per role | Each enemy that attacks differently gets its own model (V5 M3d rule I3d.3: "an enemy that looks the same always behaves the same"). Tint and size alone are a stopgap. |
| Size tells threat | Fodder small, specialists medium, elites large with a crown or trim, bosses fill a sixth of the screen. Elites are drawn 2.15× with the Rich affix today. |
| No UI text in world art | Names and numbers live in UI. UI text rule: no em dashes (owner rule). |
| Accessible | Never rely on red vs green alone: hazards add shape (rings, stripes, spikes); friendly rewards add sparkle and bob. |

### 1.2 Colour language

| Meaning | Colour | Used by |
|---|---|---|
| Player weapons | Each weapon family its own hue (table 3); today five weapons share the default cyan (0.65, 1, 1) | Projectiles, aim overlay |
| Enemy attack warning | Orange-red (1, 0.35, 0.1, alpha 0.55), the boss telegraph colour in `Data/Stage/BossConfig_BarnacleKing.asset` | All enemy telegraphs |
| Danger zone active | Bright red-orange, opaque edge | Strike moment, burning patches |
| Gold | (1, 0.85, 0.15) | Gold coins, gold barrels, Rich elites |
| XP | Cyan (0.2, 0.95, 1) | XP gems |
| Heal | Green (0.18, 1, 0.42) | Health barrels, Repair Supplies, repair tender |
| Rarity | Common grey, Uncommon green, Rare blue, Epic purple, Legendary gold | Card frames `Assets/UI/New_items/Upgrade Cards/*.png` |
| Player damage flash | (1, 0.25, 0.2), 0.12 s | `Assets/_Game/Resources/DemoJuiceSettings.asset` |

### 1.3 Telegraph language (enemy attacks and boss)

Every enemy attack shows its shape before it lands, on the water, in the warning colour:

| Attack shape | Telegraph | Duration (today) |
|---|---|---|
| Circle strike (kraken, mortar-like, boss slam) | Flat ring on the water, filling from the centre; edge solid, inside 20-30% alpha | 1.0-1.5 s |
| Line charge (swordfish, rammer, boss charge) | Arrow or lane rectangle from the enemy along the dash | 0.6-1.5 s |
| Fan volley (manta, boss spread) | Wedge with the shot lanes as gaps | 0.9-1.2 s |
| Projectile | The shot itself is the warning: bright core, dark outline, 0.13-0.45 m visual radius | - |
| Self-explosion (bomb boat) | Flashing fuse plus a ring at the blast radius | contact |

Today rings are drawn by code (`HazardRingPresenter`, trap rings R-85, burning patches R-104). The mortar telegraph is
the Lightning Mast effect tinted red and the boss telegraph ring is the Fire Barrel prefab
(`DemoJuiceSettings.asset`: `mortarTelegraphPrefab`, `bossTelegraphRingPrefab`): both placeholders. **Needed (P1):** one
telegraph kit: ring, filled disc, lane, wedge, a fill-over-time shader parameter and a "strike" flash, one material.

### 1.4 Technical rules for WebGL mobile

| Rule | Why |
|---|---|
| **No VFX Graph.** Shuriken particle systems only | WebGL has no compute shaders: `Assets/_Game/VFX/Whirlpool/PF_Whirlpool.prefab` (VFX Graph) draws nothing on phones and falls back to a water splash (`VfxProfilePlayer.NeedsFallback`) |
| No lights inside effects; no realtime point lights | Explosion prefabs carry lights (intensity 0) today (AUDIT) |
| Unlit or SimpleLit URP shaders, SRP-Batcher compatible; one material per model where possible | Batches run 450-980 in the Editor; target 100-150 draw calls on the reference phone |
| No per-instance material copies for tints; tint through a shared tinted material or vertex colour | `EnemyViewTint` shares one copy per (material, tint) to keep batching |
| Effects do not cast shadows; pickups do not cast shadows | Done for pickups in M3e |
| Textures ≤1024 (hero), 512 (enemies, POIs), 256 (props, VFX sheets), power of two, with mobile import overrides (ASTC/ETC2) | 1145 of 1171 textures are 2048 with no mobile override today (AUDIT) |
| VFX texture sheets share one or two atlases; additive or alpha-blend, soft particles off | Fill rate on Mali-G57 |
| Pivot at the waterline centre, +Z forward, 1 unit = 1 m, scale 1 on import | Pools and the simulation place by pivot |
| Every model ships LOD0 plus a 50% LOD1 for anything over 2,000 triangles | Unity generated Mesh LODs are only on the 14 heaviest FBX today (`Assets/_Game/V4/Editor/V5MeshLods.cs`) |
| Effects reset cleanly when pooled: no `Play On Awake` surprises, loop off for one-shots, lifetime ≤ 2 s for hits | Everything is pooled |
| Deliver editable source (FBX/Blend, PSD/Krita), export, and a gameplay-scale screenshot | Acceptance record in `Rogue_Pirates_Asset_Checklist.md` |

---

## 2. Budgets (reference phone: Galaxy A15/A16 4G, Helio G99, Mali-G57 MC2, 30 fps)

Scene totals from `plan/v5/research/DEVICE_TARGETS.md`: **100-150k visible triangles**, **100-150 draw calls**, **≤
1,000-1,500 live particles**, 60-80 active enemies, compressed textures under ~100 MB, Unity heap ~256 MB. Measured in
the Editor at 9:00 with ~72 enemies (M3e): 1.45M triangles, 784 batches (`plan/v5/phase-03e-performance/PLAN.md`), so
today's art is roughly 10× over the triangle budget.

| Asset class | Triangles LOD0 | Materials | Texture | Live particles | Today (worst) |
|---|---|---|---|---|---|
| Player ship (hull + sails + flag) | 3,000-5,000 | 2-3 | 1024 hull, 512 sail | wake ≤ 40 | Brig 20,008 tris, **48 PS** (cap 1,536) |
| Ordinary enemy | 300-800 | 1 | 256-512 | 0 idle (shared wake trail) | RamShip 11,462 |
| Specialist enemy | 600-1,200 | 1 | 512 | ≤ 10 idle | Ghostboat 5,258 |
| Elite / miniboss | 1,500-2,500 | 1-2 | 512 | ≤ 20 aura | Crab 3,386 |
| Boss | 4,000-6,000 | 2 | 1024 | ≤ 150 in an attack | Crab 3,386 (shared with the miniboss) |
| Projectile | ≤ 100 (or a billboard) | shared | atlas | trail ≤ 8 | Cannonball cube 192 |
| Weapon mount on the ship | ≤ 500 | shared | atlas | muzzle flash ≤ 20 | Deck Cannons muzzle 11,992, Long Nine muzzle 18,266 |
| Orbit or summon model (cutlass, anchor, parrot) | ≤ 300 (turret ≤ 600) | shared | atlas | trail ≤ 10 | Cutlass 1,812 |
| Weapon hit / explosion | - | 1 sheet | atlas | ≤ 30 per hit, ≤ 3 PS | Explosion_Large 17 PS |
| Persistent weapon effect (stream, aura, beam) | ≤ 200 | 1 | atlas | ≤ 80 | Lightning Mast **15 PS, cap 7,120** |
| Pickup (barrel, coin, gem) | ≤ 150 | shared | atlas | ≤ 3 sparkle | Barrels 1,752 tris, 3-6 PS each, up to ~200 pooled |
| POI | 1,000-2,500 | 1-2 | 512 | idle ≤ 30 | **Salvage Chest 77,650**, 12 per stage |
| Island section (generated stage) | 3,000-6,000 | atlas | 128 atlas | 0 | Island_Tropical_7 20,718 (45-50 per stage) |
| Home-island decoration | 200-1,500 | atlas | atlas | ≤ 20 | - |
| Defense (M14) | 800-1,500 | 1-2 | 512 | muzzle ≤ 20 | - |
| Water | ≤ 8,000 visible | 1 | 512 | - | `Assets/Prefabs/VFX/Water.prefab` OceanGrid_LOD0 131,072 (in the run scene; to be checked in M7) |

The heaviest models in use (measured 1 Oct 2026; Mesh LODs added in M3e for the FBX ones only):

| Model | Triangles | Used by | Note |
|---|---|---|---|
| `Assets/Mesh/Cursed/Treasure_chest/Treasure_chest.fbx` | 77,650 | Salvage Chest (12 per stage) | A 2,354-triangle chest already exists: `Assets/Prefabs/VFX/SalvageChest.prefab` (`trusher_chest.fbx` + `Chest.fbx`) |
| `Assets/Mesh/Cursed/stone-arch/storn_arch.fbx` | 68,393 | `Assets/Prefabs/VFX/BossBeacon.prefab` (not the live beacon) | Do not use as is |
| `Assets/Mesh/Brig.asset` (+ Sail.001, Flag) | 20,008 | Player ship `Assets/_Game/Prefabs/Player/CutlassSloop.prefab` | Mesh asset: no generated LOD |
| `Assets/Stylish Cannon Pack/Cannon Pack/3.FBX`, `16.FBX` | 11,448 / 6,274 | Deck Cannons and Long Nine muzzle effects (whole guns used as muzzle flashes) | Replace with mounts |
| `Assets/3D Models/ocean_buoy/ocean_buoy.fbx` | 12,240 | Signal Buoy | |
| `Assets/3D Models/light_house/light_house.fbx` | 11,597 | Lighthouse of Fortune | |
| `Assets/Mesh/RamShip.asset` | 11,198 | 5 enemy ids | Mesh asset: no generated LOD |
| `Assets/Mesh/Cursed/repair wreck/repair_wreck.fbx` | 10,484 | Repair Wreck | |
| Island meshes `Assets/Mesh/Island_Mesh_1..10.asset` (via `Assets/GameObject/Islands/Island_Tropical_*.prefab`) | 8,121-20,510 each | Every stage, 45-50 islands | Not LOD'd, not static-batched (D25) |

---

## 3. Weapons and their effects

Effects are bound in `Assets/_Game/Resources/DemoJuiceSettings.asset` (`weaponVfxBindings` and the shared slots) and
can be overridden per weapon in `Assets/_Game/V4/Resources/V4VfxProfiles.asset` (all ten weapon profiles are empty
today: every weapon uses the defaults). Configs: `Data/Weapons/<id>.asset`. Icons: section 13. Every weapon needs:
**mount** (small model on the ship), **muzzle**, **projectile or body**, **hit**, and where listed a **persistent**
effect.

### 3.1 Built weapons

| id | Name | In play | Look and silhouette brief | States | Colour (today → proposed) | Size | Budget | What exists | Status | Pri |
|---|---|---|---|---|---|---|---|---|---|---|
| deck_cannons | Deck Cannons | Forward shot, steer to aim, 16 m | Twin bow cannons; heavy round shot with a short smoke trail | mount idle, recoil, muzzle, shot, impact splash/hit | gold (1, 0.82, 0.25) → keep | shot 0.3 m, size 1.15 | mount 500, shot 100, muzzle 20 p, hit 30 p | shot `Assets/GameObject/Guns/Cannonball.prefab` (192); muzzle `Assets/GameObject/Guns/VFX/DeckCannons_Muzzle_VFX.prefab` (11,992 tris: a whole cannon, 4 PS); impact `Assets/GameObject/Guns/VFX/PowderKeg_Explosion_VFX.prefab` (6 PS) | exists, muzzle over budget | P0 |
| broadside_battery | Broadside Battery | Fan volleys port and starboard into clusters, 12 m | Row of side gun ports, short wide smoke puffs both sides | mount, volley, shot, hit | grey-blue (0.7, 0.7, 0.8) → steel white | 12 m | as above | shot `Assets/GameObject/Guns/VFX/Broadside_Cannonball_VFX.prefab`; muzzle `Assets/GameObject/Guns/VFX/Broadside_Muzzle_VFX.prefab` (544, 4 PS) | exists | P1 |
| powder_kegs | Powder Kegs | Drops mines astern; 3 m trigger, 3.2 m blast | Black-powder keg with a lit fuse and a red glow ring on the water | dropped, armed (blink), blast | red (1, 0, 0) → keep | keg 0.8 m, blast 3.2 m | keg 300, blast 30 p | keg `Assets/GameObject/FireBarrel.prefab` (1,445, 1 PS); blast `PowderKeg_Explosion_VFX.prefab` | exists, keg over budget | P1 |
| flying_cutlasses | Flying Cutlasses | Two blades orbit at 5 m and cut on contact | Spinning cutlass with a white arc trail | orbit, hit spark | white → keep | blade 1 m, orbit r 5 m | blade 300, trail 10 p | `Assets/GameObject/Guns/Flying Cutlasses.prefab` (`cutlass_sword.fbx` 1,620 + cube) | exists, over budget | P1 |
| flamethrower | Flamethrower | Cone stream 9.5 m, 52°, 2.75 s on | Nozzle at the bow; a thick turbulent flame that fills exactly the damage cone | idle, ignite, stream loop, burn-out; enemy burning | orange (1, 0.4, 0) → keep | 9.5 m × 52° | stream ≤ 80 p, ≤ 3 PS | stream `Assets/GameObject/Guns/VFX/Flamethrower_Stream_VFX.prefab` (nozzle 1,572 + 1 PS); M4a A7 scales it to the cone | **placeholder: being redone by the owner's VFX artist** (register R-89, `plan/v5/phase-04-weapons/PLAN.md` A7) | P0 |
| lightning_mast | Lightning Mast | Strikes and chains 4 targets, 13 m, 3.5 m strike | Mast-top coil; a jagged bolt from the sky to the target, short arcs between chained enemies | charge glow, strike, chain arc, shock on enemy | cyan (0, 1, 1) → electric blue-white | strike r 3.5 m | ≤ 60 p per strike, ≤ 3 PS | `Assets/GameObject/Guns/Lightning Mast.prefab` (**15 PS, cap 7,120**) | exists, far over budget | P0 |
| long_nine | Long Nine | Long forward beam-lane shot that prefers elites, 18 m | A single long gun; a tight bright tracer lane | aim, fire, lane flash, hit | default cyan → amber tracer | lane half-width 0.32 m, 18 m | mount 500, beam ≤ 20 p | beam `Assets/GameObject/Guns/LaserBeam.prefab`; muzzle `Assets/GameObject/Guns/Long Nine.prefab` (**18,266 tris**) | exists, muzzle far over budget | P0 |
| mortar | Mortar | Lobbed shell onto the densest cluster after 0.6 s | Squat mortar on deck; arcing shell; landing marker | marker, shell arc, impact | default cyan → player-friendly yellow marker (must not look like an enemy telegraph) | radius 4.25 m | marker 1 material, impact 30 p | muzzle `Assets/GameObject/Guns/Mortar.prefab` (1,462, 4 PS); impact `Assets/GameObject/Guns/VFX/Mortar_Explosion_VFX.prefab`; marker = Lightning Mast tinted red | marker placeholder | P1 |
| swivel_guns | Swivel Guns | Rapid small shots all around, 12 m | Small rail-mounted swivel guns; small pellets | fire, pellet, hit | default cyan → pale gold | pellet 0.55× cannonball | mount 400, pellet billboard | pellet `Assets/GameObject/Guns/Cannonball small.prefab`; muzzle `Assets/GameObject/Guns/Swivel Guns.prefab` (1,228, 5 PS) | exists | P2 |
| harpoon_gun | Harpoon Gun | Forward tether shot that slows priority targets, 13 m | Harpoon with a trailing rope line | fire, flight with rope, stuck, reel | default cyan → rope tan with a cyan tip | 13 m | harpoon 150 | `Assets/GameObject/Guns/Harpoon.prefab` (266, 1 PS); no mount | exists, mount needed | P2 |

Shared effects (`DemoJuiceSettings.asset`): enemy hit, kill, crit and level-up all use
`Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` (5 PS, a 5-system spark per hit, also on burn ticks: AUDIT);
elite and boss kills `Assets/GameObject/Explosion_Large.prefab` (17 PS); water splash
`Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` (2 PS, cap 1,050); damage highlight
`Assets/Prefabs/VFX/06_DamageHighlightShader.prefab` (non-URP shader). **Needed (P0):** a hit kit of 3 sizes (small
spark ≤ 8 p, medium ≤ 20 p, kill burst ≤ 30 p), a crit variant, a burn-tick flicker (shader, no particles), a level-up
burst; all on one atlas.

Burning patches (Hellfire Nozzle kills, and future burn effects): `Assets/Prefabs/VFX/14_BurningTrail_VFX.prefab`
renders with a **broken pink material** (register R-104 in `plan/v4-verification/V3_DEFECT_REGISTER.md`); V4 draws a
ground ring instead. **Needed (P1):** a burning-patch effect matched to the flamethrower redo.

### 3.2 New weapons (M4 and M4b, built on placeholders)

Design numbers: M4 from `Rogue_Pirates_Combat_Content_v3_1.md` §4.7 and `plan/v5/phase-04-weapons/PLAN.md`; M4b from
`plan/v5/phase-04b-weapons/PLAN.md`. All nine are built (M4 `eaf71a9d`, M4b `3e633720`) and run on
**borrowed placeholder art**: listed per row. The code supports the M4 mechanics
(M4a: slow, pierce, knockback, several orbits with their own model via `visual.orbitModel`, aura pulse, boost).

| id | Name | In play | Look and silhouette brief | States | Colour | Size | Budget | What exists | Status | Pri |
|---|---|---|---|---|---|---|---|---|---|---|
| anchor_flail | Anchor Flail | An anchor on a chain circles the ship, smashes and knocks back 2 m | Heavy iron anchor, chain links drawn as a stretched strip; water spray where it skims | orbit, hit (dull clang + spray) | iron grey, rust | orbit r 3 m, anchor 1.2 m | anchor 300, chain 1 strip, spray 10 p | orbit model = `Assets/GameObject/Cannonball.prefab`; icon = the Flying Cutlasses icon | placeholder | P1 |
| chainshot | Chainshot | Wide spinning shot, 2 pierces, 25% slow 2 s, 16 m | Two balls joined by a chain, spinning flat; reads as a bar | flight spin, hit, slow on enemy | dark iron with a white motion blur | 1.5 m wide | shot 150, blur sprite | shot = `Assets/GameObject/Guns/Cannonball.prefab`; icon = the Deck Cannons icon | placeholder | P1 |
| siren_bell | Siren Bell | Damaging pulse around the ship every 3 s, 20% slow (no charm) | Brass bell on a post at the stern; a teal ring wave rolling out on the water | idle sway, ring, pulse | brass, sea-teal ring | r 5 m | bell 400, ring 1 mesh + 20 p | aura pulse = the area-impact ring (M4a A5); icon = the Lightning Mast icon; candidate model `Assets/GameObject/fish_bell.prefab` (8,650: too heavy) | placeholder | P1 |
| ramming_prow | Ramming Prow | While boosting the ship becomes a ram: contact damage and push | Iron prow/beak fitted to the bow; bow wave and sparks while boosting | idle, armed (glint), boosting (bow wave), impact | iron, white foam | bow, 2 m | prow 400, bow wave 30 p | aura behaviour, no model; icon = the Broadside Battery icon; candidate rams `Assets/GameObject/Guns/SpikeRam.prefab` (1,600), `BluntRam.prefab` (2,172), unused | placeholder | P1 |
| parrot_squadron | Parrot Squadron | Parrots dive-bomb the nearest enemies; more per level | Bright parrots (red, green, blue) with a little bomb; circle then dive | perch/circle, dive, drop, return | tropical saturated | 0.6 m birds | 200 each, ≤ 6 live | none | needed | P1 |
| monkey_gunners | Monkey Gunners | Drops a floating barrel turret with a monkey that fires for a few seconds | Barrel raft with a tiny cannon and a monkey in a bandana | drop splash, firing, expire (sinks) | brown, red bandana | 1.2 m | 600, muzzle 10 p | none | needed | P1 |
| maelstrom_charge | Maelstrom Charge | Lobbed charge opens a whirlpool that drags enemies in, then implodes | Glowing charge; spiral water decal; implosion spout | lob, open, pull loop, implode | deep blue with white foam | r 4 m, 3 s, then implodes | decal + ≤ 60 p, **Shuriken only** | `Assets/_Game/VFX/Whirlpool/PF_Whirlpool.prefab` is VFX Graph (no WebGL); `Assets/Prefabs/VFX/whirlpool.prefab` (322 tris, 2 PS) is a Shuriken candidate | needed | P1 |
| lighthouse_lantern | Lighthouse Lantern | A light beam sweeps around the ship, burning along its length | Lantern on a mast; a warm cone beam on the water | sweep loop, burn on contact | warm yellow-white | length 9 m, 60°/s | beam 1 mesh, ≤ 20 p | none | needed | P1 |
| cursed_doubloons | Cursed Doubloons | Coins ricochet between enemies; kills drop extra gold (capped) | Spinning green-gold coins with a ghostly trail | throw, bounce, kill gold pop | cursed green on gold (not the pickup gold) | coin 0.3 m | billboard, trail 8 p | gold coin model `Assets/Prefabs/VFX/03_GoldCoin.prefab` (128) can be tinted | needed | P1 |

Weapon mounts on the ship (P1): every weapon needs a small mount at a socket (bow, sides, stern, mast). Today the
muzzle effects are whole cannon models (`Assets/GameObject/Guns/Deck Cannons.prefab` has 14 renderers and 14
colliders, AUDIT).

---

## 4. Evolutions

An evolution unlocks when its weapon reaches the evolution level with its paired item (level 5 today, D11; owner to confirm).
Today the six evolutions have **no art of their own**: they reuse the base weapon's effects, and their level-up card
shows the base weapon's icon (`LevelUpPresenter.IconOf`). Each needs: an icon (the base weapon's icon with an evolved
frame and a new motif), a stronger variant of the base projectile or body, and one signature effect.

| id | Name | Base weapon + item | In play | Look brief (signature) | Colour | Status | Pri |
|---|---|---|---|---|---|---|---|
| stormshot_cannon | Stormshot Cannon | deck_cannons + grapeshot_pouch | Forward arc pierce; first hit bursts grapeshot | Shot with a crackling blue-gold core; pellet burst fan on first hit | gold + electric blue | placeholder | P1 |
| kings_broadside | King's Broadside | broadside_battery + broadside_drum | Volley plus a delayed opposite-side follow-up | Gilded gun ports; the follow-up volley with a crown-gold muzzle | gold, royal red | placeholder | P1 |
| devils_wake | Devil's Wake | powder_kegs + double_fuse_cord | Wider aft keg pattern, chained detonations | Red-horned kegs; a visible fuse line linking kegs that chain | hellfire red | placeholder | P1 |
| bladed_tide | Bladed Tide | flying_cutlasses + serrated_scabbard | Blade orbit pulses between close and medium radius | Serrated blades with a water-ribbon trail that breathes in and out | steel, sea foam | placeholder | P1 |
| hellfire_nozzle | Hellfire Nozzle | flamethrower + dragons_breath_nozzle | Wider cone; kills leave flame patches | Dragon-head nozzle; blue-cored flame; burning patches (R-104 effect) | orange with blue core | placeholder (patch is a ring) | P1 |
| tempest_mast | Tempest Mast | lightning_mast + copper_conductor | Extra chain jump and a delayed cluster strike | Storm cloud above the mast; delayed strike marker (friendly colour) | violet-white | placeholder | P1 |
| kraken_chain | Kraken Chain | anchor_flail + rusted_shackle (M4) | A second anchor; both swing wider and hit harder | Two anchors wrapped in tentacles | iron + kraken purple | placeholder (M4) | P1 |
| bar_shot_barrage | Bar Shot Barrage | chainshot + chain_locker (M4) | Three chained shots that pass through two more foes | Three chain bars fanned | iron, white blur | placeholder (M4) | P1 |
| drowned_choir | Drowned Choir | siren_bell + siren_scale (M4) | The bell tolls faster and wider, slows harder | Ghostly singing rings, a drowned bell | ghost teal | placeholder (M4) | P1 |
| ironclad_ram | Ironclad Ram | ramming_prow + iron_figurehead (M4) | A heavier prow: wider, harder strikes while boosting | Armoured figurehead, wake of sparks | iron, orange sparks | placeholder (M4) | P1 |
| whalers_volley | Whaler's Volley | harpoon_gun + whale_oil_barrel (M4) | Two harpoons at once that pass through a foe | Twin barbed harpoons, oily sheen on the rope | tan rope, oil black | placeholder (M4) | P1 |
| ship_of_the_line | Ship of the Line | long_nine + powder_horn_of_the_line (M4) | A wider, heavier beam that fires more often | Thicker amber tracer, heavy muzzle ring | amber | placeholder (M4) | P1 |
| siege_bombard | Siege Bombard | mortar + siege_quadrant (M4) | Bigger, harder shells, more often | Large spiked shell, bigger marker | yellow marker, iron | placeholder (M4) | P1 |
| swivel_battery | Swivel Battery | swivel_guns + swivel_mount (M4) | Two more swivel shots, faster | Twin-barrel swivels, denser pellets | pale gold | placeholder (M4) | P1 |
| parrot_armada | Parrot Armada | parrot_squadron + captains_perch (M4b) | Two more parrots, and they hit harder | Larger flock, one captain parrot with a hat | green, red | placeholder (M4b) | P1 |
| monkey_battery | Monkey Battery | monkey_gunners + banana_crate (M4b) | A second turret that fires faster | Two barrel turrets, faster muzzle puffs | brown, banana yellow | placeholder (M4b) | P1 |
| abyssal_vortex | Abyssal Vortex | maelstrom_charge + storm_conch (M4b) | A wider whirlpool that drags and implodes harder | Darker, wider spiral with lightning flecks | deep navy, white | placeholder (M4b) | P1 |
| beacon_of_the_deep | Beacon of the Deep | lighthouse_lantern + fresnel_lens (M4b) | A second, longer beam | Two opposite beams, prism glints | warm white, cyan rim | placeholder (M4b) | P1 |
| pirate_kings_hoard | Pirate King's Hoard | cursed_doubloons + cursed_chest_key (M4b) | Coins bounce two more times and hit harder | Bigger cursed coins, green trail | green-gold | placeholder (M4b) | P1 |

The thirteen M4 and M4b evolution configs (`Data/Evolutions/<id>.asset`) use a generic stat-boost effect (Empowered) and
no art of their own.

Budget: evolutions stay inside the base weapon's budget plus ≤ 20 particles for the signature.

---

## 5. Items

Items are icon-first (no world model, `plan/10_ASSET_PRODUCTION.md`: "icons and reusable effects carry most Items").
Each needs: a **1024 master icon** (imported at 256), drawn on the rarity card frame, readable at 64 px on the HUD;
items with a visible proc need a small shared effect (listed). Configs: `Data/Build/Items/<id>.asset`. Icons:
`Assets/UI/New_items/Items/*.png` (1000×1000), listed in `Assets/_Game/V4/Resources/V4IconCatalog.asset`.

### 5.1 Built items (19)

| id | Name | Rarity | In play | Icon brief | Proc effect | Icon | Status | Pri |
|---|---|---|---|---|---|---|---|---|
| lucky_doubloon | Lucky Doubloon | Common | +Luck | Gold coin with a four-leaf stamp | - | `Lucky Doubloon.png` | exists | - |
| gunners_gloves | Gunner's Gloves | Common | Heading changes give a brief attack-speed surge | Leather gloves, powder smudge | Short speed lines on the ship | `Gunner_s Gloves.png` | exists | P3 |
| captains_bell | Captain's Bell | Uncommon | Every 12 kills a free weapon burst | Ship's bell | Bell ring pulse | `Captain_s Bell.png` | exists | P3 |
| double_fuse_cord | Double-Fuse Cord | Uncommon | Evolves Powder Kegs | Two twisted fuses | - | `Double-Fuse Cord.png` | exists | - |
| ghost_lantern | Ghost Lantern | Uncommon | Dodging telegraphs raises crit briefly | Green ghost lantern | Ghost glint on the ship | `Ghost Lantern.png` | exists | P3 |
| grapeshot_pouch | Grapeshot Pouch | Uncommon | +Deck Cannons damage; evolves it | Pouch of shot | - | `Grapeshot Pouch.png` | exists | - |
| reinforced_keel | Reinforced Keel | Uncommon | Damage reduction above cruise speed | Iron-banded keel | Armour shimmer while fast | `Reinforced Keel.png` | exists | P3 |
| serrated_scabbard | Serrated Scabbard | Uncommon | Evolves Flying Cutlasses | Scabbard with teeth | - | `Serrated Scabbard.png` | exists | - |
| admirals_ledger | Admiral's Ledger | Rare | More weapon families, modest damage | Ledger with a seal | - | `Admiral_s Ledger.png` | exists | - |
| black_powder_keg | Black Powder Keg | Rare | Crits amplify explosives | Black keg, skull | - | `Black Powder Keg.png` | exists | - |
| golden_figurehead | Golden Figurehead | Rare | Unspent gold raises Luck | Gold figurehead | - | `Golden Figurehead.png` | exists | - |
| leviathan_tooth | Leviathan Tooth | Rare | Crowd-controlled enemies take more damage | Huge tooth | - | `Leviathan Tooth.png` | exists | - |
| powder_fuse | Powder Fuse | Rare | Distance without hits charges the next explosion | Lit fuse coil | Charge glow on the ship | `Powder Fuse.png` | exists | P3 |
| sea_witchs_eye | Sea Witch's Eye | Rare | Status effects apply harder | Glowing eye charm | - | `Sea Witch_s Eye.png` | exists | - |
| stormglass | Stormglass | Rare | +1 chain jump | Storm in a glass | - | `Stormglass.png` | exists | - |
| broadside_drum | Broadside Drum | Epic | Evolves Broadside Battery | War drum | - | `Broadside Drum.png` | exists | - |
| copper_conductor | Copper Conductor | Epic | Evolves Lightning Mast | Copper rod and coil | - | `Copper Conductor.png` | exists | - |
| cursed_compass | Cursed Compass | Epic | More Luck, denser fights | Compass with a skull needle | - | `Cursed Compass.png` | exists | - |
| dragons_breath_nozzle | Dragon's Breath Nozzle | Epic | Evolves Flamethrower | Dragon-head nozzle | - | `Dragon_s Breath Nozzle.png` | exists | - |

Duplicates of the same 19 icons sit in `Assets/UI/Item Icons/` (older copy).

### 5.2 Planned items, not built

M4 evolution items (8, configs in `Data/Build/Items/`; all eight use the **Grapeshot Pouch icon as a placeholder** in
`V4IconCatalog.asset`): status placeholder, P1.

| id | Name | Evolves | Icon brief |
|---|---|---|---|
| rusted_shackle | Rusted Shackle | Anchor Flail | Broken iron shackle with rust |
| chain_locker | Chain Locker | Chainshot | Wooden locker with chain spilling out |
| siren_scale | Siren Scale | Siren Bell | Iridescent fish scale |
| iron_figurehead | Iron Figurehead | Ramming Prow | Iron figurehead bust |
| whale_oil_barrel | Whale Oil Barrel | Harpoon Gun | Small barrel with an oil drip |
| powder_horn_of_the_line | Powder Horn of the Line | Long Nine | Navy powder horn with a crest |
| siege_quadrant | Siege Quadrant | Mortar | Brass gunner's quadrant |
| swivel_mount | Swivel Mount | Swivel Guns | Iron swivel yoke |

M4b evolution items (5, placeholder icon: Lucky Doubloon's or Grapeshot Pouch's):

| id | Name | Evolves | Icon brief |
|---|---|---|---|
| captains_perch | Captain's Perch | Parrot Squadron | Wooden perch with a feather |
| banana_crate | Banana Crate | Monkey Gunners | Small crate of bananas |
| storm_conch | Storm Conch | Maelstrom Charge | Spiral conch with sparks |
| fresnel_lens | Fresnel Lens | Lighthouse Lantern | Ribbed glass lens |
| cursed_chest_key | Cursed Chest Key | Cursed Doubloons | Green-glowing iron key |

M5 items (15 built on 1 Oct with the Lucky Doubloon icon as a placeholder; 5 deferred, D33; the 20 missing from `Rogue_Pirates_Combat_Content_v3_1.md` §7.1-7.5):

| id (proposed) | Name | Rarity | In play | Icon brief | Proc effect needed | Status | Pri |
|---|---|---|---|---|---|---|---|
| spare_planks | Spare Planks | Common | +Hull, small heal | Stacked planks and nails | heal sparkle (shared) | needed | P1 |
| powder_monkey | Powder Monkey | Common | +attack speed | Monkey hugging a powder bag | - | needed | P1 |
| shark_tooth_necklace | Shark Tooth Necklace | Common | +damage to healthy targets | Tooth necklace | - | needed | P1 |
| manta_sail | Manta Sail | Common | +speed, burst after a near miss | Sail shaped like a manta | speed lines (shared) | needed | P1 |
| reef_carapace | Reef Carapace | Common | +armour | Coral-crusted shell | - | needed | P1 |
| crows_nest | Crow's Nest | Common | +pickup radius, discovery | Lookout basket | - | needed | P1 |
| skeleton_key | Skeleton Key | Uncommon | Chance of a free chest | Bone key | "free" stamp on chest | needed | P1 |
| hammerhead_helm | Hammerhead Helm | Uncommon | Less knockback, ram damage | Hammerhead-shaped helm | bow spark (shared with Ramming Prow) | deferred (D33) | P2 |
| smugglers_ledger | Smuggler's Ledger | Rare | Pricier chests, better rarity | Ledger with a dagger | - | needed | P1 |
| black_spot | Black Spot | Rare | Elite bargain, elite rewards | Paper with a black spot | elite marker | needed | P1 |
| dead_mans_compass | Dead Man's Compass | Rare | Reveals the Beacon, more spawns, XP | Bone compass | - | needed | P1 |
| kraken_ink_sac | Kraken Ink Sac | Rare | Elite deaths leave slowing ink pools | Ink sac dripping | ink pool decal (≤ 4 live) | deferred (D33) | P2 |
| cursed_coin | Cursed Coin | Rare | More gold, less max Hull | Cracked green coin | - | needed | P1 |
| storm_in_a_bottle | Storm in a Bottle | Epic | Burst of 5 lightning strikes | Bottle with a cloud | reuse Lightning strike (after its budget fix) | deferred (D33) | P2 |
| devils_match | Devil's Match | Epic | Ignites; burning deaths explode | Single burning match | small blast (shared hit kit) | deferred (D33) | P2 |
| queen_annes_fuse | Queen Anne's Fuse | Epic | Keg explosions spawn secondary blasts | Ornate fuse | reuse keg blast | deferred (D33) | P2 |
| barnacle_crown | Barnacle Crown | Epic | Armour and damage stacks while surrounded | Crown of barnacles | stack ring on the ship | needed | P1 |
| jolly_roger | Jolly Roger | Epic | Kill streak damage stacks | Skull and crossbones flag | streak counter (UI) | needed | P1 |
| leviathan_heart | Leviathan Heart | Legendary | Big periodic cluster pulse | Glowing sea-monster heart | telegraphed friendly pulse r 5 m | needed | P1 |
| captains_last_rum | Captain's Last Rum | Legendary | Cheat death once | Last bottle of rum | revive flash, 2 s shield | needed | P1 |

---

## 6. Logbooks

Logbooks are stat books (UI only). Each needs a **book icon** in one family style (a leather book with a stat emblem on
the cover; the cover colour shows the stat group: offence red, defence blue, movement teal, economy gold). Configs:
`Data/Build/Logbooks/<id>.asset`. **No logbook icons exist**: `IconCatalog` has no logbook list and level-up cards show no
icon for logbooks (`LevelUpPresenter.IconOf`). The catalogue needs a `logbooks` list (code, M5).

| id | Name | Stat | Emblem brief | Status | Pri |
|---|---|---|---|---|---|
| gunnery_manual | Gunner's Manual | Damage | Crossed cannons | needed | P1 |
| powder_drill | Powdermaster's Drill | Attack speed | Powder horn with motion lines | needed | P1 |
| navigators_charts | Navigator's Charts | Move speed | Compass rose | needed | P1 |
| carpenters_notes | Carpenter's Notes | Hull | Hammer and plank | needed | P1 |
| lookouts_glass | Lookout's Glass | Crit chance | Spyglass with a target | needed | P1 |
| quartermasters_ledger | Quartermaster's Ledger | Gold gain | Coin stack | needed | P1 |
| scavengers_net | Scavenger's Net | Pickup radius | Net with a barrel | needed | P1 |
| cartographers_folio | Cartographer's Folio | XP gain | Map with a gem | needed | P1 |
| extra_hands | Extra Hands | Quantity | Three hands on a rope | needed | P1 |
| oversized_shot | Oversized Shot | Size | Huge cannonball | needed | P1 |
| ironclad (M5) | Ironclad | Armour | Riveted shield | needed | P1 |
| fortune (M5) | Fortune | Luck | Four-leaf doubloon | needed | P1 |
| tidecaller (M5) | Tidecaller | Effect duration | Hourglass in a wave | needed | P1 |
| admiral (M5) | Admiral | Damage to elites and bosses | Admiral's bicorne | needed | P1 |
| plunderer (M5) | Plunderer | Boss gold | Treasure sack | needed | P1 |
| chaos (M5) | Chaos | Random stat | Dice with a skull | deferred (D33) | P2 |

---

## 7. Powerups (floor pickups)

Configs: `Data/Build/Powerups_Saltwind.asset` (the pool today: Repair Supplies, Powder Rush, Magnetic Tide, Dead Calm,
25 each; M5 adds Battle Signal, Tailwind, Salvage Chime and Full Sail at the design mix 35/35/20/10). Collected by
passing within 1.5 m; up to 6 per stage; 30 s life. **All eight are code-built primitives** with a floating label
(`Assets/_Game/V4/Foundation/World/PlaceholderFactory.cs` `CreateFloorPowerup`); Powder Rush, Magnetic Tide, Dead Calm
and Full Sail fall to the grey "unknown" sphere.

Each needs: a floating pickup model (≤ 300 triangles, bob and spin, a coloured ring on the water so it reads from above),
a pickup burst (≤ 20 p), a buff indicator on the ship while active, and a HUD buff icon.

| Kind | Name | Effect | Look brief | Colour | Placeholder today | Status | Pri |
|---|---|---|---|---|---|---|---|
| RepairSupplies | Repair Supplies | Heal | Crate with a green cross and planks | green (0.2, 0.85, 0.45) | green cube | placeholder | P1 |
| PowderRush | Powder Rush | +attack speed 8 s | Powder keg with a lightning fuse | orange | grey sphere | placeholder | P1 |
| MagneticTide | Magnetic Tide | Pulls pickups | Horseshoe magnet on a buoy | cyan | grey sphere | placeholder | P1 |
| DeadCalm | Dead Calm | Slows enemies 5 s | Still-water bottle, frost-blue ripple | pale blue | grey sphere | placeholder | P1 |
| BattleSignal | Battle Signal | +25% damage 8 s | Red signal flare on a float | red (0.95, 0.35, 0.2) | red cylinder | placeholder | P1 |
| Tailwind | Tailwind | +20% speed 6 s | Wind swirl around a small sail | sky blue (0.35, 0.75, 1) | blue sphere | placeholder | P1 |
| SalvageChime | Salvage Chime | Pulls XP | Small chime bell | yellow (0.95, 0.85, 0.25) | yellow capsule | placeholder | P1 |
| FullSail | Full Sail | +45% speed 8 s | Billowing full sail token | white and gold | grey sphere | placeholder | P1 |

### 7.1 Run pickups

| Pickup | Look | What exists | Triangles | Status | Pri |
|---|---|---|---|---|---|
| XP gem | Cyan crystal | `Assets/Prefabs/VFX/04_XPCrystal.prefab` (`crystaltest.fbx`) | 624 | exists, over budget (up to 200 live) | P1 |
| Gold coin | Gold coin | `Assets/Prefabs/VFX/03_GoldCoin.prefab` (3 PS) | 128 | exists | P2 |
| Gold barrel | Floating barrel with gold | `Assets/GameObject/LootBarrel.prefab` (3 PS, cap 215) | 1,752 | exists, over budget | P1 |
| XP barrel | Barrel with gems | `Assets/GameObject/GemBarrel.prefab` | 1,752 | exists, over budget | P1 |
| Health barrel | Barrel with a cross | `Assets/GameObject/HealthBarrel.prefab` (6 PS) | 1,752 | exists, over budget | P1 |
| Enemy health bar | Small bar over enemies | `Assets/Prefabs/VFX/05_EnemyHealthIndicator.prefab` (a world Canvas each, one batch each) | - | exists, costly (questions 15-16) | P1 |

---

## 8. Points of interest (POIs)

Prefabs: `Assets/_Game/V4/Demo/POI/POI_*.prefab`, bound in `DemoJuiceSettings.asset`. Each POI needs states: **unseen
(minimap only), idle, in range (prompt), active/charging, used (visibly spent)**, and a minimap glyph. A POI must read as
"interactable" from 30 m: a vertical element (mast, beam, flag) plus a ground ring.

| POI type (code) | Name in game | In play | Look brief | What exists | Tris / PS | Status | Pri |
|---|---|---|---|---|---|---|---|
| SalvageChest | Salvage Chest | Paid run chest, 12 per stage, price grows | Iron-banded chest on a sandbar, gold glow | `POI_SalvageChest.prefab` → `Assets/GameObject/Treasure_chest.prefab` | **77,650** / 3 PS | exists, **far over budget**; swap to the 2,354-triangle `Assets/Prefabs/VFX/SalvageChest.prefab` or a new 1,500 chest; add opened state | P0 |
| BossBeacon | Treasury Beacon | Summons the boss | Stone beacon with a vault door and a purple beam; states sealed, discovered, activating, active, defeated (asset checklist) | `POI_TreasuryBeacon.prefab` → `Assets/Prefabs/VFX/18_BossBeacon.prefab` (3 Unity primitives: ring, beam, marker) | primitives | placeholder | P1 |
| TrialBuoy | Signal Buoy | Stay 4 m for 8 s to charge, pick a run buff | Red-white buoy with a bell; a charge ring fills around it | `POI_SignalBuoy.prefab` → `Assets/Prefabs/VFX/TrialBuoy.prefab` (`ocean_buoy.fbx`) | 12,240 / 0 | exists, over budget; charge ring needed | P1 |
| GildedWreck | Repair Wreck | One-use heal 25% | Half-sunk hull with planks and a green lantern | `POI_RepairWreck.prefab` → `Assets/GameObject/repair_wreck.prefab` | 10,484 / 0 | exists, over budget | P1 |
| MerchantSkiff | Powder Cache | Springs an elite ambush; reward on clear | Stack of kegs under a tarp, skull flag | `POI_PowderCache.prefab` → `Assets/GameObject/pirate_tombstone.prefab` + fire FX | 7,285 / **10 PS, cap 6,325** | placeholder (a tombstone), over budget | P1 |
| CursedIdol | Treasure Chart | Reveals the 3 nearest chests | Rolled map on a post, gold sparkle | `POI_TreasureChart.prefab` → `Assets/GameObject/Treasure map.prefab` | 8,110 / 2 PS | exists, over budget | P1 |
| LighthouseOfFortune | Lighthouse of Fortune (placeholder id `poi_smugglers_oath`, the design's greed shrine) | +8% damage for the run; **not placed in Saltwind** (`StageContentConfig`) | Small lighthouse on a rock | `POI_LighthouseOfFortune.prefab` → `Assets/3D Models/light_house/light_house.fbx` | 11,597 / 0 | exists, over budget, unused | P3 |
| ExitMaelstrom | Exit maelstrom / portal | Stage exit (enum only, not built) | Whirlpool portal, locked and open | none (VFX Graph whirlpool is not WebGL-safe) | - | needed | P3 |
| (design) | Davy Jones' Contract | Boss-risk shrine | Sea chest with a glowing contract | none | - | needed | P3 |
| (design) | Salvage Bell | Pulls XP | Bell on a wreck post | candidate `Assets/GameObject/fish_bell.prefab` (8,650) | - | needed | P3 |
| (design) | Castaway Idol | Random blessing | Carved tiki idol | none | - | needed | P3 |
| (boss arrival) | Boss arrival whirlpool | 2.5 s entrance | Spinning whirlpool | `Assets/_Game/VFX/Whirlpool/PF_Whirlpool.prefab` (VFX Graph: draws a splash on WebGL) | - | placeholder on phones | P1 |

---

## 9. Enemies

Configs: `Data/Enemies/<id>.asset` (`visual.visualPrefab`, `tint`, `viewSize`) and `Data/Variants/<id>.asset` (`look`).
A look = model + tint + size (M3d I3d.3); the test only demands distinct looks, so several ids pass today by **tint and
size on a shared model**. Under the owner's rule every id that attacks differently must have **its own model**; the
rows marked "own model needed" are those. Each enemy needs: model with a readable top silhouette, swim/sail idle,
attack wind-up pose or effect (telegraph, section 1.3), hit flash (shader), death (sink or burst ≤ 30 p).

### 9.1 Shared models today

| Model (mesh) | Triangles | Ids drawn with it | Attacks |
|---|---|---|---|
| `Assets/Mesh/Cannonboat.asset` via `Assets/GameObject/SmuggleBoat.prefab` (+ barrel on `FireBoat.prefab`) | 5,399 | cutthroat_skiff, deckhand_dinghy, drowned_pursuer, needle_sloop, treasure_raider, bomb_boat, bomb_ketch | contact flank, contact swarm, contact chase, single shot, contact, suicide blast, area denial |
| `Assets/Mesh/Ghostboat.asset` via `Assets/GameObject/Cannonboat.prefab` / `CannonboatNavy.prefab` | 5,258 (4 PS) | cannonboat, gunner_cog, drowned_harrier, net_cutter, shield_frigate | single shot, burst volley ×3, burst, area denial, burst + tank |
| `Assets/Mesh/RamShip.asset` via `Assets/GameObject/RamShip.prefab` | 11,462 | boarding_barge, rammer_raft, ram_raider, powder_barge, repair_tender | single shot, charge, charge, area denial, none (support) |
| `Assets/Mesh/Crab.asset` via `Crab.prefab` / `CrabElite.prefab` | 3,386 | reef_crab_miniboss, barnacle_king (scale 2.4) | miniboss vs boss |
| `Assets/Mesh/Shark.002.asset` (`HammerHead.prefab`) | 2,458 | hammerhead, elite_hammerhead | charge vs double charge |
| `Assets/Mesh/Kraken.asset` (`Kraken.prefab`) | 6,724 | kraken_spawn, elite_kraken_spawn | area denial vs sequential traps |

Unused candidate models in the repo (check rights and budget before use): `Assets/3D Models/Ships/Pirate_ship/pirateship.fbx`
(1,287), `Assets/Mesh/Cursed/small_wooden_boat/Wooden Boat.fbx` (2,572), `Assets/3D Models/Octopus/Octopus.fbx`
(2,752), `Assets/GameObject/Eel.prefab` (3,646, for Fire Eel), `Assets/GameObject/FireSwordFish.prefab`, and the
`Assets/GameObject/cursed/*` set (cursed reskins, ~21k-triangle ship hulls: too heavy).

### 9.2 Enemy table

Tints are (r, g, b); "-" means white. All in Saltwind (biome `emerald`).

| id | Name | In play | Look and silhouette brief | Colours (tint today) | Size | Model today (tris) | Status | Pri |
|---|---|---|---|---|---|---|---|---|
| shark | Shark | Direct chase, contact; early fodder | Fin and back above water, grey | grey (-) | 1 | `Assets/GameObject/Shark.prefab` (1,896) | exists, over budget | P2 |
| swordfish | Swordfish | Dive-lunge dash, contact | Long bill, blue back, clear dash pose | blue (-) | 1 | `Assets/GameObject/SwordFish.prefab` (2,580) | exists, over budget | P2 |
| hammerhead | Hammerhead | Telegraphed charge | Wide T-head readable from above | grey-brown (-) | 1 | `Assets/GameObject/HammerHead.prefab` (2,458) | exists | P2 |
| manta_ray | Manta Ray | Strafes, 3-shot spread volley | Diamond wings from above, white belly flash on attack | dark blue (-) | 1.05 | `Assets/GameObject/MantaRay.prefab` (1,372) | exists | P2 |
| kraken_spawn | Kraken Spawn | Slow tank, tentacle circles (area denial) | Tentacle cluster, bulbous head; tentacles rise at the strike | purple (-) | 0.65 | `Assets/GameObject/Kraken.prefab` (6,724) | exists, over budget | P1 |
| cannonboat | Cannonboat | Keeps range, single shot | Small gunboat, one cannon, navy-free colours | (-) | 1 | `Assets/GameObject/Cannonboat.prefab` (Ghostboat 5,258) | exists, over budget | P1 |
| gunner_cog | Gunner Cog | Formation, 3-shot burst volley | Round-hulled cog, three gun ports on the side facing the player | crimson (0.85, 0.35, 0.35) | 1.15 | `CannonboatNavy.prefab` (shared hull) | own model needed | P0 |
| cutthroat_skiff | Cutthroat Skiff | Flanks, contact | Narrow rowing skiff with raised cutlasses | salmon (1, 0.62, 0.55) | 1 (scale 0.92) | `SmuggleBoat.prefab` (shared) | own model needed | P0 |
| deckhand_dinghy | Deckhand Dinghy | Fast swarm, contact | Tiny round dinghy, one figure; reads as fodder | (-) | 1 (scale 0.82) | `SmuggleBoat.prefab` (shared) | own model needed | P0 |
| bomb_boat | Bomb Boat | Runs in and explodes | Boat loaded with a big keg, lit fuse, sparks | (-) | 1 | `Assets/GameObject/FireBoat.prefab` (shared hull + barrel) | own model needed (hull shared) | P1 |
| rammer_raft | Rammer Raft | Telegraphed ram charge | Log raft with a sharpened log ram | (-) | 1 | `RamShip.prefab` (shared, 11,462) | own model needed | P0 |
| boarding_barge | Boarding Barge | Keeps range, single shot | Wide flat barge with a boarding plank and one gun | blue (0.6, 0.75, 1) | 1.15 | `RamShip.prefab` (shared) | own model needed | P0 |
| drowned_harrier | Drowned Harrier | Keeps range, burst volley (drowned pursuit) | Waterlogged ghost gunboat, green glow | sea green (0.7, 1, 0.8) | 1 | `CannonboatNavy.prefab`, `placeholderModel` on | placeholder | P1 |
| drowned_pursuer | Drowned Pursuer | Direct chase, contact (drowned pursuit) | Barnacled drowned rowboat with lantern eyes | sea green (0.7, 1, 0.8) | 1 | `SmuggleBoat.prefab`, `placeholderModel` on | placeholder | P1 |
| reef_crab_miniboss | Reef Crab | Miniboss: telegraphed charge, contact | Armoured crab, big claws, armour plates that open on its weak window | red-orange (-) | 1 | `Assets/GameObject/Crab.prefab` (3,386) | exists, shares the boss's mesh | P1 |
| barnacle_king | Barnacle King | Boss (section 10) | | | | | | |

Variants (`Data/Variants/`; all specialists except the two elites):

| id | Name | Base id | In play | Look and silhouette brief | Tint today | Size | Status | Pri |
|---|---|---|---|---|---|---|---|---|
| needle_sloop | needle sloop | cutthroat_skiff | Keeps range, single shot | Slim sloop with one long needle gun | yellow (1, 1, 0.35) | 0.9 | own model needed (ranged on a melee hull) | P0 |
| net_cutter | net cutter | gunner_cog | Flanks, area denial (nets) | Boat with a net boom and drag nets | orange (1, 0.7, 0.4) | 1 | own model needed | P0 |
| shield_frigate | shield frigate | gunner_cog | Tank, burst volley | Frigate with a front shield wall of planks and iron | steel blue (0.65, 0.7, 0.85) | 1.15 | own model needed | P0 |
| powder_barge | powder barge | boarding_barge | Chases, area denial (powder) | Barge stacked with kegs, smoking | orange (1, 0.6, 0.3) | 1.25 | own model needed | P0 |
| ram_raider | ram raider | rammer_raft | Telegraphed charge | Raider with an iron ram | red (1, 0.45, 0.45) | 1.05 | could share rammer's new model with a red sail (same attack) | P1 |
| repair_tender | repair tender | boarding_barge | No attack, heals allies | Tender with a crane, green cross flag, repair beam | green (0.55, 1, 0.55) | 1 | own model needed (support role) | P0 |
| bomb_ketch | bomb ketch | bomb_boat | Keeps range, area denial (lobbed bombs) | Ketch with a mortar on deck | violet (0.8, 0.55, 1) | 1.2 | own model needed | P0 |
| treasure_raider | treasure raider | deckhand_dinghy | Swarm, contact; carries loot (not in the voyage timeline today) | Dinghy piled with gold, sparkle | (-) | 1 | placeholder | P2 |
| elite_hammerhead | Elite Hammerhead | hammerhead | Double charge | Scarred hammerhead with armour plates and a crown-gold trim | base look, elite scale | 2.15× (Rich) | elite treatment needed | P1 |
| elite_kraken_spawn | Elite Kraken Spawn | kraken_spawn | Sequential tentacle traps | Larger spawn with glowing runes | base look, elite scale | 2.15× (Rich) | elite treatment needed | P1 |

Elite treatment (P1): one shared kit on top of the base model: gold trim or crown, a ground aura ring in the elite's
affix colour, a health bar always shown; affix colours for Armored, Swift, Volatile, Rich, Cursed, Captain
(`ConfigEnums.EliteAffix`).

Later biomes (P3, `Rogue_Pirates_Combat_Content_v3_1.md` §8.2-8.3; material and prop variants on shared geometry is
allowed there because each changes one behaviour and must still read differently): Gravewater (Drowned Shark, Bone
Swordfish, Ghost Cannonboat, Wraith Manta, Drowned Crab, Spectral Hammerhead, Gravewater Kraken Spawn, **Siren** new
model), Devil's Maw (Hellfin Shark, Ember Swordfish, Infernal Cannonboat, Ash Manta, Magma Crab, Abyssal Hammerhead,
Infernal Kraken Spawn, **Fire Eel** new model). All needed.

---

## 10. Bosses

| id | Name | In play | Look and silhouette brief | States | Colours | Size | Budget | What exists | Status | Pri |
|---|---|---|---|---|---|---|---|---|---|---|
| barnacle_king | Barnacle King | Saltwind boss, 2,800 HP (M3d); attacks: spread (5 shots, 1.2 s), charge (1.5 s, second charge in phase 3), bombard (r 5.5 m, 1.5 s), shell slam (r 5 m, 1.2 s); phases at 65% and 30% | Giant hermit crab under a barnacle-crusted shell, one huge claw, crown of barnacles; shell opens in its weak window | entrance (whirlpool), idle, 4 attack wind-ups, recover (armour off), phase change, enrage, death | sand, coral red, barnacle white | 2.4× crab, ~6 m | 4,000-6,000 tris, ≤ 150 p | `Assets/GameObject/CrabElite.prefab` = the Reef Crab mesh ×2.4; death `FX_Fire_05` (4 PS, cap 4,000); spawn `Assets/TornadoVFX/Tornado_VFX.prefab` (7 PS) | **placeholder: shares the miniboss model** | P0 |
| reef_crab_miniboss | Reef Crab | Miniboss (section 9.2) | Distinct from the king: smaller, no shell, two equal claws | as enemy plus armour-down window | coral red | 1 | 1,500-2,500 | `Assets/GameObject/Crab.prefab` | exists | P1 |
| (later) | Drowned Admiral | Gravewater boss (§10.2) | Ghost galleon presenting broadsides | | ghost green | | | none | needed | P3 |
| (later) | Infernal Kraken | Devil's Maw boss (§10.3) | Stationary kraken body with burning tentacles | | magma | | | none | needed | P3 |

Boss telegraphs use section 1.3; the boss telegraph ring prefab today is the Fire Barrel (placeholder).

---

## 11. Island decorations (request 22) and the stage

### 11.1 What the voyage stage uses today

Islands are generated from `Assets/GameObject/Islands/Island_Tropical_1..10.prefab` (listed in
`Assets/_Game/Resources/TropicalIslandPrefabCatalog.asset`), 8,289-20,718 triangles each, 45-50 per stage, on the shared
colour atlas. Props from `Assets/_Game/Resources/IslandVisualLibrary.asset`: `Assets/3D Models/Tree/Tree_01.prefab` and
`Tree_02.prefab` (1,232 each), `Assets/3D Models/Rocks/Rock_*.prefab` (Rock_Large 400), `Ground_Sand_Small` (285).
Other dressing prefabs in `Assets/GameObject/` not used by the V4 stage: `Cliffs_Tropical 1-10` (802), `CoralReef_1-4`
(17,560), `MermaidStatue` (34,502), `Shipwreck` (42,388), `RockCluster`/`Rock_Gate` (252), `Pillar`, `SW3_ShoalGrass`,
`Tower_Windward` (25,046), `Islands/Island_Vulcano` (38,990). **Needed (P2):** island sections at 3,000-6,000 triangles
with an LOD1, and the 12 section templates of the asset checklist when M14 splits the home island into sections.

### 11.2 Decoration catalogue (cosmetic only, D14; sold for RUM, web3 copies marked by origin)

Each decoration: ≤ 1,500 triangles (small ≤ 500), colour atlas, pivot at the ground centre, fits a socket footprint
(S 2×2 m, M 4×4 m, L 6×6 m; proposed), optional idle animation (sway, flicker) and ≤ 20 particles. Needs a store
thumbnail (512×512) and a "placed" and "preview (ghost)" state. All **needed, P2 (M14)** unless noted.

| id (proposed) | Family | Name | Brief | Footprint | Existing candidate |
|---|---|---|---|---|---|
| deco_jetty | Dock and harbour | Jetty | Wooden pier, 3 segments | L | `Assets/Mesh/Cursed/wooden_bridge/wooden_bridge.fbx` (45,468: too heavy) |
| deco_post_lantern | Dock and harbour | Lantern on a post | Flickering lantern | S | - |
| deco_rope_coil | Dock and harbour | Rope coils | Coiled rope pile | S | - |
| deco_crates_barrels | Dock and harbour | Crates and barrels | Stacked cargo | S | `Assets/Mesh/Cursed/Baral/Baral.fbx` (488) |
| deco_crane | Dock and harbour | Small crane | Wooden cargo crane | M | - |
| deco_bollards | Dock and harbour | Mooring bollards | Iron posts with rope | S | - |
| deco_tavern | Pirate life | Tavern hut | Thatched hut with a sign | L | - |
| deco_rum_still | Pirate life | Rum still | Copper still, steam | M | - |
| deco_hammocks | Pirate life | Hammocks between palms | Two palms and a hammock | M | - |
| deco_campfire | Pirate life | Campfire with a spit | Fire (≤ 20 p) and a roast | S | - |
| deco_lookout | Pirate life | Lookout tower | Tall crow's nest tower | M | - |
| deco_flagpole | Pirate life | Flag pole | Uses the player's flag texture (section 12) | S | `Assets/Mesh/Flag.asset` |
| deco_trophy_pedestal | Treasure and trophies | Trophy pedestals | Raid wins, leaderboard titles (variants) | S | - |
| deco_treasure_pile | Treasure and trophies | Treasure pile | Gold heap with sparkle | M | - |
| deco_captain_statue | Treasure and trophies | Golden Captain statue | One per Captain | M | - |
| deco_bounty_board | Treasure and trophies | Bounty board | Board showing wins (UI texture slot) | S | - |
| deco_palms | Nature | Palm variants | 3 palm shapes | S | `Assets/3D Models/Tree/Tree_01/02.prefab` (1,232) |
| deco_flower_bush | Nature | Flowering bushes | Hibiscus colours | S | - |
| deco_tide_pool | Nature | Tide pools | Rock ring with water | M | - |
| deco_coral_arch | Nature | Coral arch | Arch over water | M | `CoralReef_*` (17,560: too heavy) |
| deco_waterfall_rock | Nature | Waterfall rock | Rock with a water sheet | L | - |
| deco_bird_nest | Nature | Sea-bird nests | Nest with gulls | S | - |
| deco_glow_shells | Nature | Glowing shells | Emissive shells | S | - |
| deco_ghost_lantern | Spooky (Gravewater) | Ghost lanterns | Green-flame lantern | S | - |
| deco_skeleton_bench | Spooky (Gravewater) | Skeleton crew bench | Skeletons on a bench | M | - |
| deco_wrecked_hull | Spooky (Gravewater) | Wrecked hull | Broken hull on sand | L | `Assets/GameObject/repair_wreck.prefab` (10,484) |
| deco_wisps | Spooky (Gravewater) | Will-o'-wisps | Floating lights (≤ 10 p) | S | - |
| deco_bunting | Seasonal | Festival bunting | Flag strings | M | - |
| deco_lantern_festival | Seasonal | Lantern festival | Paper lanterns | M | - |
| deco_frosted_palms | Seasonal | Frosted palms | Snowy palm | S | - |
| deco_fireworks | Seasonal | Anniversary fireworks platform | Fireworks on tap (≤ 40 p burst) | M | - |
| skin_lighthouse | Skins | Lighthouse skin | Skins the defense lighthouse | M | `light_house.fbx` (11,597) |
| skin_beacon | Skins | Beacon skin | Skins the home Beacon | M | - |
| skin_dock | Skins | Dock skin | Skins the dock | L | - |
| skin_path_tiles | Skins | Path tiles | Stone, plank, sand | S | - |
| skin_fences_walls | Skins | Fences and walls | Picket, stone, bone | S | - |
| deco_chippy_stash | Chippy's corner | Stash of shiny things | Mascot's treasure pile | S | - |
| deco_chippy_house | Chippy's corner | Chipmunk house | Tiny hut | S | - |

---

## 12. Sails and flags (canvas spec for M25)

### 12.1 What exists

| Part | Mesh | Material / shader | Texture today |
|---|---|---|---|
| Player sails | `Assets/Mesh/Sail.001.asset` (221 vertices, 362 triangles) on `CutlassSloop.prefab` / `Brigg.prefab` | `Assets/Material/EnemySail.mat`, shader `Assets/Shader/Shader Graphs_EnemySailShader.shader` (`_BaseTexture`, `Cull Off`, wind sway) | `Assets/Texture2D/PlayerSail (1).png` 512×512 |
| Captain sails | same | `Assets/UI/New_items/character ui/<Captain>/_Sail_Mat.mat` (via `V4SaltWindHudSettings.asset`) | `<Captain> sail colour.png` 1080×1080 (non power of two) |
| Player flag | `Assets/Mesh/Flag.asset` (45 vertices, 64 triangles) | `Assets/Material/SkullBandanaFlag.mat`; Captains `_Flag_Mat.mat` | `Assets/Texture2D/PlayerSail.png` 512×512 |
| Enemy sails and flags | `Assets/Mesh/Sail.asset` (RamShip), `Flag.asset` | `SmugglerFlag.mat` etc. | `Assets/Texture2D/SmugglerFlag.png` 120×80 |
| Sail damage | same shader `_DamageTexture_1..3` | | `Assets/Texture2D/sail damage1..3.png` 1024 |

### 12.2 `Sail.001` UV layout (read from the mesh data)

- **Two sail panels** (a 100-vertex 10×10 grid and a 121-vertex 11×11 grid), and **each maps the whole 0-1 UV square**:
  the same texture prints on both sails. The smaller panel is about 4.96 × 3.31 m, the larger 6.12 × 4.19 m (both
  about 1.5 : 1 wide).
- **V = 1 is the top edge** (the yard): the sail object is rotated +90° on X, so mesh +Z points down, and the shader's
  wind fade (`_TopFadeStart` 0.85 on `uv.y`) pins the top. V = 0 is the foot of the sail.
- **U runs across the sail and is mirrored against mesh +X** (U = 0 at the ship's starboard edge). From the game camera
  (behind the ship) a texture therefore appears **mirrored left to right**, and reads correctly from the bow. The shader
  draws both faces (`Cull Off`). To confirm in M25 with a test texture (an arrow and "TOP LEFT" text) before any
  sticker work.

### 12.3 Canvas spec (proposed for M25)

| Item | Spec |
|---|---|
| Canvas | 512 × 512 RGBA (M25 plan), power of two, sRGB; the Captain sails at 1080 should be re-exported at 1024 or 512 |
| Aspect | The square stretches to about 1.5 : 1 on the sail: a circle painted on the canvas shows as an ellipse 1.5× wide. The editor should preview on the 3D sail, or paint on a 768 × 512 view squeezed into 512 × 512 |
| Mirroring | Export mirrored on U (or flip in the shader) so text and emblems read correctly from the camera |
| Safe area | Keep emblems and text inside U 0.10-0.90, V 0.12-0.85 (about 51-461 px wide, 77-450 px from the bottom); the top 15% is the pinned and least visible strip, the edges curl and are seen at a slant from the camera. The hero emblem centre at about (0.5, 0.45) |
| Both sails | One texture shows on both panels; a design per panel needs a UV change (two atlas halves) |
| Damage | The shader **replaces** the sail with the damage textures as `_Damage` rises (lerp to `_DamageTexture_1..3`), so a painted sail disappears when damaged. M25 needs the damage as an alpha overlay instead (code and art: three tear masks with alpha) |
| Flag | `Flag.asset` uses U 0.012-0.988 and V 0.176-0.824 of its texture: on a 512 canvas the visible band is about 506 × 332 px (rows 90-422 from the top), about 1.5 : 1; U runs against mesh +X as on the sail; keep the emblem inside the centre 70% |
| Stickers and emblems (from the art team, D25.1) | 256 × 256 RGBA each, transparent background, 4 px clear margin, readable when 64 px tall on the sail; sets: skulls, animals, crests, letters/numbers, borders, patterns; tint-able (white on transparent) and full-colour variants |
| Sizes | Painted sail PNG ≤ 200 KB; plus layer JSON (M25 I25.3) |

---

## 13. Defenses (M14 home island; reuse the weapon behaviours)

All **needed, P2**. Each: ≤ 1,500 triangles, 3 upgrade looks (levels), states idle, aiming, firing, destroyed, a
socket footprint, a muzzle ≤ 20 p, and a store icon.

| id (proposed) | Name | Behaviour (weapon reused) | Look brief | Existing candidate |
|---|---|---|---|---|
| def_cannon_tower | Cannon tower | Projectile (Deck Cannons) | Stone tower with a rotating cannon | `Assets/Stylish Cannon Pack` (11-12k: too heavy), `Assets/Mesh/Cursed/Cannon/cannon.fbx` (2,736) |
| def_mortar_nest | Mortar nest | Predicted area (Mortar) | Sandbag ring with a mortar | `Assets/GameObject/Guns/Mortar.prefab` (1,462) |
| def_net_launcher | Net launcher | Slow projectile | Ballista with a net | - |
| def_mine_field | Mine field | Mine (Powder Kegs) | Floating mines, chained | `Assets/GameObject/FireBarrel.prefab` (1,445) |
| def_harpoon_post | Harpoon post | Tether projectile (Harpoon Gun) | Whaler's harpoon gun on a post | `Assets/GameObject/HarpoonLauncher.prefab` |
| def_lighthouse | Lighthouse | Reveals the raider | Small lighthouse with a sweeping beam | `Assets/3D Models/light_house/light_house.fbx` (11,597: too heavy) |

---

## 14. UI icons

Icon catalog: `Assets/_Game/V4/Resources/V4IconCatalog.asset` (lists `weapons` and `items` only). Weapon level-up card
art: `Assets/_Game/V4/Demo/LevelUpCards/LevelUpCard_<Weapon>.prefab` (10). Rarity frames:
`Assets/UI/New_items/Upgrade Cards/{common,uncommon,rare,epic,legendary 2}.png`. Masters are 1000×1000 today; deliver
1024×1024 masters, imported at 256 for mobile.

| Group | Count built / planned | Icons today | Status | Pri |
|---|---|---|---|---|
| Weapons | 10 / +4 (M4) +5 (M4b) | `Assets/UI/New_items/weapons/*.png` (10, plus unused Boarding Party, Grapeshot); level-up card prefabs (10); the 4 M4 weapons borrow other weapons' icons | exists for 10; 9 needed (4 M4 placeholders) | P1 |
| Evolutions | 6 / +8 (M4) +5 (M4b) | none (cards reuse the base weapon icon) | needed (19) | P1 |
| Items | 19 / +8 (M4) +20 (M5) +5 (M4b) | `Assets/UI/New_items/Items/*.png` (19); the 8 M4 items borrow the Grapeshot Pouch icon | exists for 19; 33 needed (8 M4 placeholders) | P1 |
| Logbooks | 10 / +6 (M5) | none; no catalog list | needed (16) | P1 |
| Powerups (HUD buff) | 8 | none | needed (8) | P1 |
| Captains | 6 | `Assets/UI/New_items/character ui/<Captain>/` (select, portrait, ability, ship logo, sail, flag) | exists | - |
| HUD glyphs | - | `Assets/UI/New_items/ui/` (coin, heart, skull, xp, hourglass, bars), `Assets/UI/Stats and quest/` (Gold, Rum) | exists | - |
| Minimap glyphs (POIs, boss, elites) | ~12 | `Assets/UI/Mini Map/Map elements.png`, skull markers | partial | P2 |
| Elite affixes | 6 | none | needed | P1 |
| Status effects (burn, slow, shock, bleed, curse) | 5 | none | needed | P1 |
| Decorations and defenses (store thumbnails) | ~38 + 6 | none | needed | P2 |
| Currencies (RUM, run gold, tokens) | 3 | `Rum icon.png`, `Gold Icon.png`; token none | partial | P2 |

Captains (`Data/Build/Captains/<id>.asset`): brine (Hank, "Brace!"), ember (Ember, "Stoke the Flames"), flint (Sparky,
"Light the Fuse"), richie (Richie, "Cut and Run"), rook (Rook, "All Guns!"), roxie (Roxie, "Ride the Gale"): art exists
for all six; their sail PNGs are 1080×1080 and should be re-exported to the section 12 spec.

---

## 15. Brand asset slots (M27 bounty islands)

Designed per brand as custom elements at first (owner's choice, plan section 7). Sizes and placement rules are a
proposal for the M27 brand kit. All **needed, P2**.

| Slot | Where | Spec (proposed) | Rules |
|---|---|---|---|
| Hero landmark | The bounty island's special landmark | 3D, ≤ 6,000 triangles, ≤ 2 materials, 1024 texture, footprint L (6×6 m) | Must not block navigation lanes or hide telegraphs; no text that must be read |
| Bounty elite and boss sails | Enemy sails and flags | Sail 512×512 on the `Sail.001` UV (section 12); enemy sail on `Sail.asset` (UV to be measured); flag band 506×332 | Enemy colours must stay distinct from the player's |
| Billboards and banners | Island edges, docks | 1024×512 (2:1) and 512×1024 banners, on a frame mesh ≤ 200 triangles | Placed on decoration sockets only; never over water lanes |
| Branded chest and drop skins | Salvage Chest and gold barrels | Chest ≤ 2,500 triangles; texture swap on the existing barrel UV | Rarity glow and the gold colour language stay as in section 1.2 |
| Themed decoration set | Winnable decorations | 3-6 items to the section 11.2 spec | Cosmetic only (D14) |
| Loading card | Loading screen | 1920×1080 and 1080×1920, safe area 10% | Brand logo ≤ 25% of the area |
| Share-card frame | Run result card (M19) | 1200×630 PNG frame with transparent centre | Leave the stats area clear |
| Penny line | NPC dialogue | text only | No em dashes |
| Limited sail | Bounty finishers only | 512×512 sail to section 12 | Mintable only if no RUM was spent (D4) |

---

## 16. Top priorities

1. **Salvage Chest** model (77,650 triangles, 12 per stage): swap to `Assets/Prefabs/VFX/SalvageChest.prefab` (2,354) or
   a new ≤ 1,500 chest with an opened state. P0.
2. **Own models for enemies that share a hull** (section 9.1): needle sloop, net cutter, shield frigate, powder barge,
   repair tender, bomb ketch, boarding barge, rammer raft, gunner cog, cutthroat skiff, deckhand dinghy, all at
   300-1,200 triangles (today 5-11k). P0.
3. **Barnacle King** own boss model (today the Reef Crab mesh scaled 2.4) with its four attack wind-ups. P0.
4. **Flamethrower stream** redo by the owner's VFX artist (R-89, M4a A7), plus the burning-patch effect (R-104 broken
   pink material). P0/P1.
5. **Lightning Mast** effect (15 PS, cap 7,120) and the shared hit spark (5 PS on every hit) rebuilt to budget. P0.
6. **Weapon mounts** to replace the whole-gun muzzle effects (Deck Cannons 11,992, Long Nine 18,266). P0.
7. **Telegraph kit** (ring, disc, lane, wedge) and a friendly mortar marker, replacing the Lightning Mast and Fire Barrel
   stand-ins. P1.
8. **Icons**: 16 logbooks, 19 evolutions, 8 powerups, the 33 planned items and 9 planned weapons. P1.
9. **New weapon art for M4 and M4b** (anchor, chainshot, bell, prow, parrots, monkey turret, Shuriken whirlpool,
   lantern beam, cursed coins). P1.
10. **Powerup models** (8, all primitives today) and the Treasury Beacon kit (3 primitives today); a Shuriken boss
    arrival whirlpool (the VFX Graph one is invisible on phones). P1.
