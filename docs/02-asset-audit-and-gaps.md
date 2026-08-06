# 02 — Asset Audit and Gaps

Every path below is relative to `references/piratenation-art-full/`. That directory holds the complete, path-sanitised, LFS-resolved copy of the Pirate Nation art library — about 154,000 files and 43.5 GB. It is gitignored; see [references/README.md](../references/README.md) for how it was built.

This document is organised by **game system**, not by folder, because the useful question is "what do I need to build the enemy spawner" rather than "what is in `world items`".

## Headline verdict

The library is extraordinarily strong in exactly the places a survivor-like is expensive, and empty in exactly four places.

**Effectively free:** the entire enemy roster, the entire boss roster, the playable ship roster, several hundred cosmetic ship skins, the power-up card art, all currency and pickup iconography, terrain tiles with transitions, a modular character customisation system, and a ranked trophy ladder.

**Must be produced:** ship combat animation, a real VFX library, all audio, and UI chrome. Audio is the largest single gap — the repository contains zero sound.

The practical consequence is that a small team can reach a playable, good-looking vertical slice unusually fast, and the budget should be concentrated on feel (VFX, audio, game juice) rather than on content volume.

## Playable ships

**Source:** `Voxel Game Assets/ships/` — 73 theme folders (69 unique; four are `_2` revisions), 456 GLTF exports, roughly 286 distinct ship builds.

Five hull classes recur across themes and become the playable roster:

| Hull | Role | Example path |
| --- | --- | --- |
| Skiff | Fast, fragile, tight turning | `ships/Voyager Ships/item_2x2_pirateskiffvoyager/VE/item_2x2_pirateskiffvoyager.gltf` |
| Sloop | Balanced all-rounder, the default | `ships/Apex Sloop/model.gltf` |
| Frigate | Heavy broadside, slow turn | `ships/AbstractFrigate/` |
| Galleon | Tanky, wide arcs, poor acceleration | `ships/Ironclad Ship/` |
| Marauder | Glass cannon, aggressive | `ships/The Dread Peel/` |

Twenty-eight themes carry a complete five-class set — Agean, Barbary, Berserker, Cape Cod, Disco, Glitterfin, Juanga, Mecha, Pirate, Royal Navy, SquareHull, Trade Federation, Turtle, Viking, Voyager, Yucatan, and the plain colour sets among them. Those are the safest themes to build against because a player can take any hull without a missing model.

Others are partial and are better treated as cosmetic skins for the hulls they do cover: Angel, Demon, Mage, Navy, Steampunk, Undead and Rainbow have no skiff; Gothic and Orca Heritage have no galleon; Pink Power, Polly Roger and Tiger have no marauder.

**How this maps to design.** Hulls are the character roster. Themes are the cosmetic layer and therefore the gold sink — roughly 286 ship builds is more cosmetic content than most live games ship in three years, and it already exists. See [01](01-game-overview.md#what-the-player-keeps).

### Ship bundle anatomy

Most ships follow a nested pattern. Taking the Voyager Skiff as the reference case:

```
ships/Voyager Ships/item_2x2_pirateskiffvoyager/
├── ui_item_2x2_pirateskiffvoyager.png      # store/UI icon
├── PNG/                                     # orthographic renders: Front, Back, Left, Right, A, B
└── VE/
    ├── item_2x2_pirateskiffvoyager.gltf     # engine-ready export
    └── item_2x2_pirateskiffvoyager/
        ├── item_2x2_pirateskiffvoy.idle.vxa # animation
        ├── item_2x2_pirateskiffvoy.vxr      # rig
        └── *.vxm, *.vxm.png                 # voxel parts and part previews
```

Some bundles are flat instead (`ships/Apex Sloop/` has `model.gltf`, `ipfs.json`, `thumbnail.png`, `thumbnail_animated.mp4`, and thumbnails at 64/128/256/512). The importer needs to handle both shapes.

### Ship shards

**Source:** `Voxel Game Assets/ship_shards/` — 24 theme folders, 893 files, standardised into `GLTF/`, `PNGs/`, `VE/` subfolders.

Example: `ship_shards/shards_voyager/shard_voyager_skiff/GLTF/shard_voyager_skiff.gltf`

These become the **hull unlock currency**: collect shards of a theme to unlock that theme's hulls. It is a progression system with finished art attached, requiring no invention.

## Enemies

**Source:** `Voxel Game Assets/Mob Enemies/` — 25 creatures, 15,854 files, 336 GLTF, 508 `.vxa` animations.

The 25 creatures:

Anglerfish, Blowfish, Charybdis, Crasher (Living Wave), Deep One, Demogorgon Whale, Foam Monster, Giant Squid, Hammerdead Shark, Harpy, Hexa Croc, Hippogriff, Kelpling, Mecha Charybdis, Mecha Hammerhead, Megasquito, Mist Monster, Mutant Jellyfish, Sea Lion, Shipwrecked Spirit, Spout (fka Stormling), Trilobite, undeadcharybdis, undeadmegasquito, Wyvern.

**Nearly every one ships in six elemental variants** — neutral, fire, water, air, earth, lightning — giving roughly 150 distinct enemy visuals. Most carry a full `Idle` / `Attack` / `Hit` animation set.

```
Mob Enemies/Kelpling/Air Affinity/
├── model/
│   ├── Body.vxm  (+ other parts, each with a .vxm.png)
│   └── model.Attack.vxa
├── model.vxr
├── model.Idle.vxa
├── model.Hit.vxa
└── model.gltf
```

Two special cases worth knowing. **Mist Monster** has two silhouettes per element (`mistmonster_creature` and `mistmonster_mist`), which is free variety for a single enemy type. **Wyvern** is the only creature explicitly tiered Rare across all its variants (`Mob Enemies/Wyvern/Wyvern Fire Rare/model.gltf`), making it the natural elite unit.

Some bundles are export-only — a `model.gltf` plus thumbnails and `ipfs.json`, with no source voxels. Those are fine for rendering and bad for re-animating.

**How this maps to design.** The elemental variants are not reskins to be dismissed; they are the backbone of the affinity system in [03](03-core-combat-and-progression.md). Twenty-five behaviours multiplied by five elements is the entire enemy content plan, and the art cost is zero.

## Bosses

**Source:** `Voxel Game Assets/world items/World Bosses/` — 8 bosses.

| Boss | Path | Suggested use |
| --- | --- | --- |
| Kraken (Urza) | `world items/World Bosses/Kraken (Urza)/Kraken.gltf` | Bounty Waters flashpoint |
| Megalodon (Bruce) | `world items/World Bosses/Megalodon (Bruce)/Megalodon.gltf` | Tide 1–3 run boss |
| Sea Serpent (Jorgamandr) | `world items/World Bosses/Sea Serpent (Jorgamandr)/SeaSerpent.gltf` | Tide 4–6 run boss |
| C'thulhu | `world items/World Bosses/C_thulhu/Cthulu.gltf` | Deep Tide finale |
| Giant Crab | `world items/World Bosses/Giant Crab/giant_crab/model.gltf` | Mid-run elite |
| Giant Turtle | `world items/World Bosses/Giant Turtle/GLTF/Giant_Turtle_Evil.gltf` | Mid-run elite |
| Giant Goldfish | `world items/World Bosses/Giant Goldfish/` | Event boss |
| Kevin | `world items/World Bosses/Kevin/creature_12x12_kevin.gltf` | Event / joke boss |

Eight bosses is enough for launch. It is not enough for two years of live operation, but that is a season-three problem.

## Power-up cards

**Source:** `Combat Cards/` — 151 flat PNGs named `Card_Art_{Name}.png`.

This is the single luckiest thing in the library. The names map onto survivor-like power-ups so cleanly that the draft system barely needs invention:

- **Direct damage (42)** — `Broadside`, `Cannon_Blast`, `Rapid_Fire`, `Scatter_Shot`, `Double_Shot`, `Storm_of_Iron`, `Rain_of_Iron`, `Hull_Piercer`, `Long_9s`, `Carronade`
- **Counter and retaliation (8)** — `Counter_Attack`, `Riposte_Stance`, `Retaliate`, `Roll_with_the_Blow`
- **Lifesteal (4)** — `Vampiric_Strike`, `Vampiric_Rain`, `Leeching_Chomp`, `Leeching_Rain`
- **Repair and heal (15)** — `Quick_Fix`, `Patch_Work`, `Continuous_Repairs`, `Mariner_s_Mend`, `Total_Relief`
- **Defence (14)** — `Reactive_Armor`, `Hardened_Shield`, `Phoenix_Armor`, `Barnacle_Barrier`, `Poseidons_Shield`, `Thorny_Guard`
- **Evasion and mobility (15)** — `Evasive_Maneuvers`, `Fleetfoot`, `Hit_and_Run`, `Ghost_Mode`, `Slippery_like_an_Eel`, `Swift_Winds`
- **Buffs (12)** — `Extra_Power`, `Inspired_Attack`, `The_Power_of_Rum`, `Poseidon_s_Blessing`, `Amplify_Affinity`
- **Tactics and draft manipulation (18)** — `Stack_The_Deck`, `Do_It_Again`, `Careful_Planning`, `Rummage`, `DejaVu`, `Time_Rewritten`
- **Targeting (7)** — `DeadEye`, `Steady_Aim`, `Critical_Target`, `Expose_Weakness`, `Overwatch`
- **Ammo and resource (4)** — `Reload`, `Refill_Cannon`, `Supply_Run`, `Sticky_Fingers`
- **Utility (12)** — `Ace_In_The_Hole`, `Last_Ditch`, `Quick_Thinking`, `Sober_Up`, and the four `AP01`–`AP04` placeholders

The tactics group is unusually valuable because draft-manipulation power-ups (reroll, banish, duplicate) normally need bespoke art. `Stack_The_Deck`, `Do_It_Again` and `Rummage` already exist. `Ace_In_The_Hole` is the obvious face-down gamble card described in [05](05-mode-pvpve.md).

Note `Card_Art_Hit the Weak Spot.png` and `Card_Art_Rapid Strike.png` use spaces rather than underscores, and `Advenced_Attack_Plan` is misspelled in the source. The importer should not assume a naming convention.

## Arena and terrain

**Source:** `Voxel Game Assets/world items/Terrain Textures/` — 56 files.

Top-down tiles with transition pieces already authored: `top_grass.png`, `top_sand_light.png`, `top_sand_dark.png`, `top_stone.png`, `top_snow.png`, `top_lava.png`, `top_magma.png`, `top_lavarock.png`, `top_woodDeck.png`, `top_metalPlate.png`, plus grass-to-sand and stone-to-grass corners and straights, `side_*` cliff faces, and a `bottom.png`. A `jumbo_textures/` subfolder holds twelve larger variants.

Five biomes fall straight out of this: shallows (sand), reef (stone), overgrown island (grass), volcanic (lava/magma), and frozen (snow). That is the Tide Level visual progression with no art cost.

**Obstacles and dressing:** `world items/Decorations/` (10,400 files, 100+ categories — shipwrecks, ruins, coral reef, tiki totems, elemental altars, lava pools) and `world items/Sets/` (six themed sets: Graveyard, Haunted, Mecha, Skull Cave, Zombie, gems_barrel). Far more than needed; treat as a library to cherry-pick from rather than content to consume.

**Objective markers:** `world items/Buoys/` — 13 variants including `buoydavid`, `buoyhaunted`, `buoymecha`, `Mage Buoy`, `NYE Buoy`, `Daily Dungeons`. These are purpose-built floating markers and become the Bounty Waters capture points.

## Loot, pickups, and currency

**Chests:** `Voxel Game Assets/chests/` — 5,010 files. A clean rarity ladder in `chests/Tiered Chests/` with `chest_common`, `chest_uncommon`, `chest_rare`, `chest_epic`, each in open and closed states (`chests/Tiered Chests/chest_rare/GLTF/chest_rare.gltf`), plus 52 themed and event chests.

**Pickups:** `Voxel Game Assets/resources/` — gold coin (`resources/Gold Coin/ui_icon_coin.gltf`), gold bag and stack, iron ore at three levels, energy, essence, silk, cotton, and five mystery-bag colour variants.

**Collectibles:** `Voxel Game Assets/collectibles/` — 318 GLTF across 70+ types. Directly useful: `pirate_key_free` and `pirate_key_paid` (the economy's per-account entry gate), `Mausoleum Key`, the copper/silver/gold gacha tokens, `trophy_pvp`, and eleven `achievement_pvp` rank objects.

**UI iconography:** `world items/UI/` — 6,057 files across 35 categories. Currency icons for gems (handful through mountain), `Booty_Coin`, `Roguelite_Coin`, a `PIRATE` token icon, wood/ore/rum/cotton, `XP_Star`, `Tome_of_Experience`, `Roguelite_Key`, `PVP Trophy`, and raffle tickets in four PvP tiers.

The `XP_Star` and `Roguelite_Coin` icons alone cover the two pickups the player will see ten thousand times per run.

## Captain customisation

**Source:** `Voxel Game Assets/Avatar/` — a genuine modular slot system, roughly 291 numbered pieces.

| Slot | Variants |
| --- | --- |
| tops | 78 |
| headwear | 66 |
| hair | 29 |
| eyewear | 23 |
| bottoms | 22 |
| face | 20 |
| facialhair | 18 |
| shoes | 16 |
| species | 13 |
| back | 3 |
| ears | 1 |

Each variant ships `model.vox`, `model.fbx`, `model.gltf`, `model.blend`, `thumbnail.png`, and an `obj/` export — for example `Avatar/hair/1/model.gltf`. Slot pieces are mesh-only; the base rig comes from `Avatar/Naked Pirate/Naked Pirate Templates/` or `Avatar/animation/starter_036/starter_pirate_01.gltf`, which carries a skeleton with `Head`, `Hand.L`, `Chest` and similar named bones.

That this is a real equip system rather than a pile of models is confirmed by the assembled NFT exports: `Founder's Pirate NFT/model3d/1.gltf` contains mesh nodes literally named `tops 40`, `species 4`, `facialhair 7`, `eyewear 2` on an `Original Armature` skin with 16 joints.

Also available: 34 full-body themed replacements in `Voxel Game Assets/Pirate Skins/` (knight, ninja, samurai, astronaut, luchador, scarecrow, seawraith, hotdog), 10 lore NPCs in `Voxel Game Assets/Lore Characters/`, and 9,999 pre-assembled founder portraits in three formats (`illustrated/*.svg`, `model3d/*.gltf`, `voxel/*.png`).

**How this maps to design.** The captain is a cosmetic identity layer visible in Port, on the results screen, and as your Bounty Waters portrait. It never affects gameplay. It is a second gold sink that costs no art.

## Ranked ladder and achievements

`Voxel Game Assets/Achievements/achievement_pvp_1/` holds eleven rank trophies — wood, bronze, silver, gold, sapphire, emerald, ruby, diamond, master, dreadlord, supremelord (`Achievements/achievement_pvp_1/gltf_1/pvp_diamond_1.gltf`). That is a complete competitive ladder's worth of art, already tiered, matching the eleven-tier `achievement_pvp` set in `collectibles/`.

Sixteen further achievement categories exist for meta goals.

## What exists that we are deliberately not using

Stating this explicitly so nobody "discovers" it later and expands scope.

- **Crafting and harvesting** — `world items/harvestables/` (3,557 files), `world items/Crafting/` (2,689), `New Crafted Items/` (3,920). A complete gather-craft-equip pipeline with tiered tools and materials. It is a different game. Out of scope per [01](01-game-overview.md#non-goals).
- **Buildings** — `world items/buildings/` (8,780 files, 26 building types). Port is a menu, not a settlement.
- **Saga map** — `world items/SagaMap/` is FBX and blend source only, not runtime-ready.
- **Event content** — `Voxel Game Assets/Events/` (Forgotten Gods, Glitterfin, SBL). Excellent live-ops material for later seasons, irrelevant now.

## Duplicate folders and gotchas

The library contains several near-identical `_2` folders, presumably re-exports. Pick one and ignore the other, but check file counts first because they are not always identical:

- `Voxel Game Assets/Mob Enemies_2`
- `Voxel Game Assets/Achievements_2`
- `world items/Sets_2` (1,404 files vs 1,455 in `Sets`)
- `world items/World Bosses_2` (942 vs 945)
- Ship themes: `Broken Skiff_2`, `Cosmik Battle Frigate Ship Skin_2`, `Dynasty Ships and Shards_2`, `Rogue_s Galleon (Heirloom Community Giveaway Ship)_2`

Other traps:

- **Path characters.** The original repository contains trailing spaces, `'` and other Windows-illegal characters in directory names. The local copy has been sanitised (211 renames), so paths here will not match the upstream repository exactly.
- **Git LFS.** All media in the upstream repo are LFS pointers. The local copy has them resolved. Any fresh clone will need `git lfs fetch --all` before the files are real.
- **Naming inconsistency.** `Shipwrecked Spirit` has a `Lighting` folder (typo for Lightning). Several mobs use a `mob_6x6_*` prefix for the neutral variant instead of a `Neutral` folder. Do not build the importer on a naming convention; build it on a manifest.

## Format and pipeline notes

**GLTF is production-ready.** All 456 ship GLTFs and 336 mob GLTFs are self-contained: geometry buffers embedded as `data:application/octet-stream;base64,...` and textures as `data:image/png;base64,...`. There are **zero** external `.bin` files and **zero** external texture references in the entire library. Copy a single file and it renders.

That property is convenient for most engines and is a specific problem for Defold, which has historically not supported embedded textures. See [08](08-engine-and-tech.md).

**Voxel sources are optional.** The `.vxm` (parts), `.vxr` (rig), and `.vxa` (animation) files are MagicaVoxel/VoxEdit-family formats. They matter only if new animations need authoring. Nothing at runtime needs them.

**Pre-rendered sprites are a hidden second pipeline.** Almost every asset carries orthographic renders (`PNG/Front.png`, `Back`, `Left`, `Right`) and thumbnails at 64/128/256/512, plus 159 `thumbnail_animated.mp4` files under `ships/` alone. A 2D sprite version of this game could be built almost entirely from art that already exists. This is a real strategic option, not a fallback — see [08](08-engine-and-tech.md).

## The production backlog

Four gaps. Priority is ordered by impact on whether the game feels good, not by size.

### P0 — VFX

**What exists:** nine effect groups in `world items/VFX/` — `boom02` (explosion with sparks), `cannon_fire03.gltf`, `vfx_cannon_fire/`, `vfx_ship_wake/`, `vfx_splash03/`, `vfx_boss_splash_flat.gltf`, `wave_crests/`, `cloud/`, `Bubbles/`.

**What a survivor-like needs:** roughly forty. The genre's entire feel lives in effects, because the player's dopamine comes from screen-filling feedback rather than from the models.

Required, in rough priority: cannonball and projectile variants per element (5+); muzzle flash variants; impact and hit sparks; enemy death dissolve and gib puff; AoE ground telegraphs (circle, cone, line); persistent aura and burn zones; chain lightning arcs; orbital and spinning projectile trails; XP and gold pickup trails and absorb flashes; level-up burst; evolution transformation flash; damage-number popups; screen-edge damage vignette; boss telegraph and enrage effects; storm and rain overlay for Bounty Waters; revive beam for co-op.

**Estimate:** the largest ongoing art cost. Mitigable — much of it is shader and particle work rather than modelled assets, and the existing `boom02` and `cannon_fire03` establish the visual language to match.

### P0 — Audio

**What exists:** nothing. The repository contains no `.wav`, `.mp3`, or `.ogg` files of any kind.

**What is needed:** cannon fire layers per weapon tier; impact and hull-hit; enemy vocalisations across 25 creature archetypes; death sounds; pickup blips (the most-heard sound in the game, and it has to stay pleasant after ten thousand repetitions); level-up sting; card select and hover; boss roars and telegraph cues; ambient ocean bed; three to five music tracks with an intensity system; UI clicks; the Bounty Waters kill-feed stinger and cannon salute described in [05](05-mode-pvpve.md).

**Estimate:** the single largest gap by absolute volume of missing content. Licensing a library plus commissioning bespoke hero sounds and music is the realistic path.

### P1 — Ship combat animation

**What exists:** 320 `.vxa` files under `ships/`, of which roughly 96% are `idle` and thirteen are `moving` or `move`. There is **no firing, recoil, damage, list, or sinking animation anywhere in the library.**

**Recommendation: do not author these.** For the prototype and probably for launch, fake all of it procedurally in-engine:

- Firing — a short positional recoil kick on the hull plus a muzzle VFX at a socket. Reads better than a baked animation anyway because it can scale with fire rate.
- Turning — roll the hull into the turn with a tween.
- Sailing — bob and pitch driven by a sine wave against a fake wave height, plus the existing `vfx_ship_wake`.
- Damage — material flash and smoke particles at damage sockets.
- Sinking — rotate, translate down, spawn `vfx_splash03`.

This converts a large animation budget into a few days of engineering. Revisit only if the ships feel inert once everything else is in.

### P1 — UI chrome

**What exists:** thousands of icons. `world items/UI/` alone has 6,057 files.

**What is missing:** every frame, panel, button, and container that holds those icons. Specifically: card frames per rarity for the draft screen, health and XP bars, the wave and timer HUD, minimap frame and blips, the results screen, Port navigation, the cosmetics shop grid, the Bounty Waters scoreboard and kill feed, and a damage-number font.

**Estimate:** moderate and well-bounded. One competent UI artist and a defined style guide covers it. The card frames are the highest-value piece because the draft screen is the emotional peak of every level-up.

### P2 — Everything else

- **Enemy portraits** for a bestiary. Thumbnails exist at `thumbnail_x512.png` on most mobs and are probably good enough.
- **Additional bosses** beyond the eight, needed for season two rather than launch.
- **Elemental ship VFX skins** so an equipped element visibly changes your cannon fire. Nice-to-have that materially improves build identity.

## Summary table

| System | Coverage | Source |
| --- | --- | --- |
| Playable ships | Complete | `ships/` — 69 themes, 5 hulls |
| Ship cosmetics | Vastly oversupplied | `ships/` + `ship_shards/` |
| Enemies | Complete | `Mob Enemies/` — 25 × ~6 elements |
| Bosses | Sufficient for launch | `world items/World Bosses/` — 8 |
| Power-up card art | Complete | `Combat Cards/` — 151 |
| Terrain | Complete | `world items/Terrain Textures/` |
| Obstacles and dressing | Oversupplied | `world items/Decorations/`, `Sets/` |
| Pickups and currency | Complete | `resources/`, `collectibles/`, `world items/UI/` |
| Chests and loot | Complete | `chests/` |
| Captain customisation | Complete | `Avatar/`, `Pirate Skins/` |
| Ranked ladder art | Complete | `Achievements/achievement_pvp_1/` |
| Ship combat animation | **Absent** — fake procedurally | — |
| VFX | **~20% of need** | `world items/VFX/` |
| UI chrome | **Icons only** | — |
| Audio | **Absent** | — |
