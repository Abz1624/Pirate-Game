# Weapon prefabs (hackathon, 13 Oct)

Every weapon's look is now a set of prefabs you can edit in the Unity Editor without code. The 19 weapons are
anchor_flail, broadside_battery, chainshot, cursed_doubloons, deck_cannons, flamethrower, flying_cutlasses,
harpoon_gun, lighthouse_lantern, lightning_mast, long_nine, maelstrom_charge, monkey_gunners, mortar, parrot_squadron,
powder_kegs, siren_bell, squid_ink_pots and swivel_guns. Each one draws everything (shots, muzzle flash, hits, splashes,
blades, flame, kegs, strikes, rings, whirlpool, aura, summons, anchor and chain, coins) from its own prefabs.

An evolution keeps its base weapon's id, so it uses the same prefabs. Examples: Stormshot pellets use the Long Nine
projectile, King's Broadside follow-ups use the Broadside shots and muzzle, Devil's Wake side kegs use the Powder Kegs
keg and blast, Hellfire Nozzle patches use the Flamethrower patch ring, Bladed Tide uses the cutlass blades, and Tempest
Mast jump strikes use the Lightning Mast strike.

## Where things are

| What | Path |
|---|---|
| Prefabs, one folder per weapon | `Assets/_Game/V4/Weapons/Prefabs/<weapon_id>/<weapon_id>_<Visual>.prefab` (plus the materials and textures the code used to build at run time, beside them) |
| The catalog (weapon id to prefabs) | `Assets/_Game/V4/Weapons/Resources/V4WeaponVisuals.asset` (`WeaponVisualCatalog`; the game loads it from Resources) |
| Tuning on each prefab | `WeaponVisualTuning` component on the prefab root |
| Builder | **Tools > Rogue Pirates > Weapons > Build Weapon Prefabs (missing ones)**. It keeps prefabs that are already on disk, so your edits are safe. **Rebuild All Weapon Prefabs (discards edits)** remakes every prefab from the current shared look. Code: `Assets/_Game/V4/Editor/HackathonWeaponPrefabs.cs` |
| Art team pieces wired in | `weaponvisual` lines in `Assets/_Game/V4/Demo/Art/ArtTeamModels.txt` (weapon id; visual; asset; optional child) |
| Test scenarios | `Assets/_Game/V4/DevTools/Scenarios/Scenarios/weapon_prefabs_*.asset` |

Each prefab is a full, unpacked **copy** of what the game drew for that weapon before. Usually that is the juice
settings' shared prefab or the art team's model. For the visuals the code used to build at run time (the coloured
Monkey shot, the coin sprite, pulse rings, landing and patch rings, the chain line), the prefab is the same object,
saved with its material and texture as assets. The look is unchanged, and an edit to one weapon's prefab changes only
that weapon. The other side of this is that an art-team change to a shared source prefab no longer reaches the
weapons. To pick it up, delete the weapon's copy and run Build Weapon Prefabs (missing ones). The builder removed
missing scripts and found no V3 scripts in any source. The only script on the PowderKeg and Mortar explosions is URP's
`UniversalAdditionalLightData` on their light, which is kept. No prefab uses VFX Graph, so all of them are WebGL safe.

An empty slot in the catalog (or a prefab you clear) falls back to the shared look from `DemoJuiceSettings`.

## How to tweak a weapon

1. Open `Assets/_Game/V4/Weapons/Prefabs/<weapon_id>/` and double-click the prefab, for example `mortar_Impact.prefab`.
2. Change what you like: particle systems (colour, size, count, lifetime), materials, meshes, child offsets, or add
   children. The root's pose is set by code. Child poses are yours.
3. For the numbers the code applies, use **WeaponVisualTuning** on the root:

| Field | Default | What it does |
|---|---|---|
| `scale` | 1 | Multiplies the scale the code gives it (from the Size stat, blast radius or juice-settings scale) |
| `lifetime` | 1 | Multiplies how long the code shows it (a burst's seconds, a pulse's sweep, the beam, the coins' fall). It does not change shots or summons, because the simulation ends those |
| `spin` | 1 | Multiplies its spin: orbit models 540 deg/s, coins 1080 deg/s in flight, the anchor's roll 420 deg/s, the whirlpool 90 deg/s, the aura 15 deg/s |
| `arc` | 1 | Multiplies the arc height of a lobbed shell or ink pot (5 m) and a thrown coin |
| `width` | 1 | Multiplies line width (pulse, landing and patch rings) |
| `heightOffset` | 0 | Metres added above where the code puts it (bursts, splashes, hits, pulses, aura, whirlpool, flame) |
| `overrideColor` / `color` | off | Pulse and ring lines use this colour instead of the code's: the weapon's aim colour for pulses, orange for landing and patch rings. The code still fades the alpha |
| `keepOwnColours` | per shot | Shots only. When off, the code tints every material with the weapon's `projectileColor`, as the cannonballs always were. Turn it on to use the prefab's own colours |
| `baseRadius` | measured | Aura and whirlpool only: the model's radius at scale 1. The code scales the model to the pulse or whirlpool radius. Set by the builder |
| `faceCamera` | builder | The Cursed Doubloons coin sprite turns to the camera |

4. Press Play, or reload a scenario (see below). The presenters read the prefab and its tuning each time they use it.
   A tuning change during Play shows on the next shot. A prefab change needs the pools to rebuild, so reload the
   scenario or start a new voyage.

## What the code still sets (and why)

These come from the simulation or the run, so they stay in code. The table for each weapon lists them in full.

- **Pose**: position, facing and parenting come from the simulation (the shot, blade, keg or summon). Muzzles face the shot.
- **Scale from stats**: shot size (radius x 2 x `projectileSizeMultiplier` x the config's `scaleMultiplier`), blast
  radius for kegs, strikes, shell bursts, whirlpool and aura, and juice-settings scales (`enemyHitScale`,
  `cannonImpactScale`, `mineBarrelScale`, `cutlassScale`, `lightningStrikeScale`, and so on). Tuning `scale`
  multiplies on top of these.
- **Beam length and width**: Long Nine runs from the muzzle to the weapon's range, with width from the beam half-width.
- **Flame reach and spread**: the flame's start speed and cone angle follow the cone's live range and arc (Size, items, Hellfire).
- **Ring geometry**: the points of pulse, landing and patch rings, the fill progress over the shell's delay, the fade, and the colour per weapon (aim colour, unless `overrideColor` is on).
- **Chain**: sag (up to 0.45 m), the 12 curve points, texture tiling (one tile per 0.35 m), and links laid every `maceChainLinkSpacing` m (juice settings). Each link is turned 90 degrees from the last.
- **Tint**: cannonball-style shots get the weapon's `projectileColor` (see `keepOwnColours`), and cutlass particles are pulled toward gold every frame.
- **Durations**: muzzle 0.35 s (salvo 0.4 s), hit and impact bursts, strike 1.15 s, splash 0.55 s, keg blast 1.5 s, beam `laserBeamDuration`. Tuning `lifetime` multiplies all of them.
- **Gun prefabs** (muzzles copied from a gun with a `CannonSmoke` child): only that child shows. Edit `CannonSmoke` to change the flash.
- **Keg blast**: only the `Impact small`, `Ring` and `Smoke` children show.
- **Cutlass blade**: only its `Blade` child and particles show. The root mesh is hidden.

### Which weapon an effect belongs to

Some effect calls from the simulation carry no weapon id, and the simulation contract was not changed. In those cases
the presenter works out the weapon from the cause:

| Effect | How the weapon is found |
|---|---|
| Landing ring | The weapon that aimed this frame. A later ring at the same spot (a whirlpool or ink slick opening) belongs to that shell's weapon |
| Shell impact and whirlpool implosion | The shell that landed at that spot |
| Water splash | The shot that ended there this frame, else the shell that landed there, else an anchor in the water there, else a summon weapon that aimed this frame. Otherwise the shared splash is used, as for enemy shots |
| Keg and keg blast | The equipped keg weapon. A blast at a keg's spot is that keg's |
| Strike | The strike weapon that aimed this frame, else the equipped one |
| Beam | The weapon that fired this frame |

These still use the shared look because no weapon is involved: enemy blasts and splashes, the Leviathan Heart item
ring and blast, the Tempest Mast delayed strike blast (it has no weapon id), and kill explosions (the enemy's death).
Burning Trail and the Mortar reticle are also shared, because no current weapon uses them: every shell weapon now
draws a landing ring instead of the reticle.

### Pooling

All prefabs are pooled and prewarmed when the run binds, so nothing is created mid-run. Each weapon now has its own
copies, so prewarm counts are per weapon: muzzle 4, hit 12, crit 3, splash 3, pulse 6 (24 for the ink slick, which pulses every tick), landing ring 8, keg 32, keg
blast 12, strike 8, shell burst 12, lob 6 (8 for ink pots), launch 3, whirlpool 3, aura 1, plus the projectile view
prewarm from the pool config for each weapon. Pools grow past these if a run needs more. With the catalog in place,
the shared pools used only by weapons are registered without a prewarm. The keg blast and splash pools stay
prewarmed because enemies use them.

There is one exception, which predates this change. The flame stream is built once, when a stream weapon is first
equipped (during the level-up pause).

## The art team's weapon art

Wired in by this change (WebGL safe, no VFX Graph, no V3 scripts):

| Weapon | Visual | Art | Notes |
|---|---|---|---|
| siren_bell | Aura | `Assets/GameObject/Guns/TidalAura/TidalAura_VFX.prefab` | Follows the ship while the Siren Bell is equipped, sized to the pulse radius, turning slowly. It appears with the first pulse |
| maelstrom_charge | Whirlpool | `Assets/GameObject/Guns/Maelstrom Charge.prefab` (its `Strike/Whirlpool` child) | Drawn on the water for the whirlpool's seconds, sized to its radius. The shader's `_SpinOffset` and `_Fade` are driven by code, in place of the art's missing `MaelstromChargeVfx` script. The prefab has its own copy of the whirlpool material, queued after the sea (3000), because the art's queue (2990) drew it under the water. The landing ring is still drawn too |
| mortar | Lob | `Assets/GameObject/Guns/MortarBarrage/Prefabs/Mortar_FireShell_VFX.prefab` | A flaming shell thrown to each landing ring, landing as the shell lands |
| mortar | Launch | `Assets/GameObject/Guns/MortarBarrage/Prefabs/Mortar_Muzzle_VFX.prefab` | Smoke at the ship's rail as each shell is thrown |
| mortar, maelstrom_charge | Impact | `Assets/GameObject/Guns/VFX/Mortar_Explosion_VFX.prefab` | Already the shell burst. Now each weapon has its own copy |
| broadside_battery, deck_cannons | Projectile, Muzzle | `Guns/VFX/Broadside_Cannonball_VFX`, `Broadside_Muzzle_VFX`, `DeckCannons_Muzzle_VFX` | Already wired through the juice settings. `Broadside Battery.prefab` and `Deck Cannons.prefab` are the same art |
| flamethrower | Stream | `Guns/VFX/Flamethrower_Stream_VFX.prefab` | Already wired. `Flamethrower.prefab` is the same art |
| flying_cutlasses | Orbit | `Guns/Flying Cutlasses.prefab` | Already wired |
| harpoon_gun | Projectile | `Guns/Harpoon.prefab` | Already wired |

Not wired:

| Art | Why |
|---|---|
| `GameObject/fish_bell.prefab` | 8,650 triangles, and the art guide says to avoid it. It is a borrowed POI landmark with no place on a ship-borne aura. The Tidal Aura is the Siren Bell's look |
| `Guns/Harpoon Gun.prefab` | A gun model to mount on the ship. V4 draws no weapon mounts on the hull, so it has no slot |
| `Guns/MortarBarrage/Prefabs/Mortar_Telegraph_VFX.prefab` | It would replace the landing ring, but that ring fills over the shell's exact delay (R-27). The art does not show the timing |
| `Guns/MortarBarrage/Prefabs/Mortar_Shell_VFX.prefab` | The plain shell. The fire shell is used instead |
| `Guns/Mortar Barrage.prefab` | Only empty mount and target transforms, with no visuals |
| Rest of `Guns/Maelstrom Charge.prefab` (Bolt, Strike flash, Splash) | It needs its missing driver script to play in sequence. Only the whirlpool is used |

To wire another piece, add a line `weaponvisual;<weapon id>;<Visual>;<asset path>[;<child path>]` to
`ArtTeamModels.txt`, delete that weapon's prefab for the visual if it exists, and run Build Weapon Prefabs. `<Visual>`
is a catalog slot name: Projectile, Muzzle, Hit, CritHit, WaterSplash, Beam, Orbit, Stream, PatchRing, Mine, Blast,
Strike, Coin, PulseRing, Aura, LandingRing, Impact, Lob, Launch, Whirlpool, Summon, Mace, ChainLink, Chain.

## How to test

- **In a voyage**: V4Boot > Set Sail > F1 (Tuning Console) > Weapons > pick the weapon > **Solo**, then **Fire** or
  **+1** (rank up). Edit the weapon's prefab, then start a new voyage, or reload a scenario from the Tuning Console.
- **Scenarios** (Tuning Console > Scenarios), all at rank 8, invulnerable, among sturdy sharks:
  - `weapon_prefabs_1_guns`: Deck Cannons, Broadside Battery, Chainshot, Swivel Guns, Harpoon Gun, Long Nine
  - `weapon_prefabs_2_close`: Flying Cutlasses, Lighthouse Lantern, Flamethrower, Powder Kegs, Anchor Flail, Cursed Doubloons
  - `weapon_prefabs_3_areas`: Lightning Mast, Siren Bell, Mortar, Maelstrom Charge, Squid Ink Pots
  - `weapon_prefabs_4_summons`: Parrot Squadron, Monkey Gunners
  - `weapon_prefabs_pick_one`: one weapon (Mortar). Change `weaponId` (and `level`, or an `evolutionId`) in its loadout to look at any weapon on its own
- **Automated**: EditMode `HackathonWeaponPrefabsTests`. It checks that every weapon has each prefab in its own
  folder, linked in the catalog, with no missing, V3 or VFX Graph script. It checks that the copies match their source
  (meshes, materials, particle systems, scales, tint), that the code-built ones keep the code's settings, and that the
  presenters draw the weapon's prefabs from pools prewarmed before the run, with the tuning applied.

## Each weapon's prefabs

Generated by `HackathonWeaponPrefabs.ListMarkdown()`. "Made from" is what the game drew before this change.

### anchor_flail (Anchor Flail, ChainedMace)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Mace | `Assets/_Game/V4/Weapons/Prefabs/anchor_flail/anchor_flail_Mace.prefab` | copy of `Assets/_Game/V4/Demo/Art/Weapons/Art_AnchorFlail_Anchor.prefab` | position (the simulation's mace), flukes first away from the ship, rolling 420 deg/s; scale 1 (1.25 without a ChainAttach child); physics removed; its ChainAttach child is where the chain meets it |
| ChainLink | `Assets/_Game/V4/Weapons/Prefabs/anchor_flail/anchor_flail_ChainLink.prefab` | copy of `Assets/_Game/V4/Demo/Art/Weapons/Art_AnchorFlail_ChainLink.prefab` | only its mesh and material (GPU instanced): one link every maceChainLinkSpacing m along the sagging chain, each turned 90 deg from the last, sized spacing x 1.3 (x the tuning's scale) |
| Chain | `Assets/_Game/V4/Weapons/Prefabs/anchor_flail/anchor_flail_Chain.prefab` | built as the code built it (Chain) | its 12 points (the ship's rail to the anchor, sagging up to 0.45 m) and texture tiling (one tile per 0.35 m); hidden while chain links are drawn |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/anchor_flail/anchor_flail_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/anchor_flail/anchor_flail_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/anchor_flail/anchor_flail_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### broadside_battery (Broadside Battery, Fan)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Projectile | `Assets/_Game/V4/Weapons/Prefabs/broadside_battery/broadside_battery_Projectile.prefab` | copy of `Assets/GameObject/Guns/VFX/Broadside_Cannonball_VFX.prefab` | position and facing (the shot's), uniform scale = shot radius x 2 x projectileSizeMultiplier x the config's scaleMultiplier (min 0.2); physics removed; cannonball trails cleared and boosted; every material tinted with the weapon's projectileColor |
| Muzzle | `Assets/_Game/V4/Weapons/Prefabs/broadside_battery/broadside_battery_Muzzle.prefab` | copy of `Assets/GameObject/Guns/VFX/Broadside_Muzzle_VFX.prefab` | position 1.1 m out on the firing side, facing out; scale shootingVfxScale x 1.15 x the juice binding's muzzle scale; particles restarted; 0.4 s (a gun prefab shows only its CannonSmoke child) |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/broadside_battery/broadside_battery_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/broadside_battery/broadside_battery_Hit.prefab` | copy of `Assets/GameObject/Guns/VFX/PowderKeg_Explosion_VFX.prefab` | position (0.45 m up), scale cannonImpactScale (x1.12 on a crit); 0.55 s |

### chainshot (Chainshot, Projectile)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Projectile | `Assets/_Game/V4/Weapons/Prefabs/chainshot/chainshot_Projectile.prefab` | copy of `Assets/_Game/V4/Demo/Art/Weapons/Art_Chainshot.prefab` | position and facing (the shot's), uniform scale = shot radius x 2 x projectileSizeMultiplier x the config's scaleMultiplier (min 0.2); physics removed; cannonball trails cleared and boosted x projectileScale (juice settings art) |
| Muzzle | `Assets/_Game/V4/Weapons/Prefabs/chainshot/chainshot_Muzzle.prefab` | copy of `Assets/GameObject/Guns/Deck Cannons.prefab` | position and facing (the shot's), scale shootingVfxScale x the juice binding's muzzle scale (else the config's fallbackMuzzleScale); particles restarted; 0.35 s (a gun prefab shows only its CannonSmoke child) |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/chainshot/chainshot_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/chainshot/chainshot_Hit.prefab` | copy of `Assets/GameObject/Guns/VFX/PowderKeg_Explosion_VFX.prefab` | position (0.45 m up), scale cannonImpactScale (x1.12 on a crit); 0.55 s |

### cursed_doubloons (Cursed Doubloons, AreaStrike)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Coin | `Assets/_Game/V4/Weapons/Prefabs/cursed_doubloons/cursed_doubloons_Coin.prefab` | built as the code built it (CoinSprite) | 3 a strike thrown from the ship (1.1 m up) to the enemy in a fast low arc (0.4 m + 3% of the distance), spinning; they glance off and fall; 0.45 s after landing; faces the camera, its width following the spin; size 1.1 |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/cursed_doubloons/cursed_doubloons_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/cursed_doubloons/cursed_doubloons_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/cursed_doubloons/cursed_doubloons_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### deck_cannons (Deck Cannons, Projectile)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Projectile | `Assets/_Game/V4/Weapons/Prefabs/deck_cannons/deck_cannons_Projectile.prefab` | copy of `Assets/GameObject/Guns/Cannonball.prefab` | position and facing (the shot's), uniform scale = shot radius x 2 x projectileSizeMultiplier x the config's scaleMultiplier (min 0.2); physics removed; cannonball trails cleared and boosted; every material tinted with the weapon's projectileColor |
| Muzzle | `Assets/_Game/V4/Weapons/Prefabs/deck_cannons/deck_cannons_Muzzle.prefab` | copy of `Assets/GameObject/Guns/VFX/DeckCannons_Muzzle_VFX.prefab` | position and facing (the shot's), scale shootingVfxScale x the juice binding's muzzle scale (else the config's fallbackMuzzleScale); particles restarted; 0.35 s (a gun prefab shows only its CannonSmoke child) |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/deck_cannons/deck_cannons_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/deck_cannons/deck_cannons_Hit.prefab` | copy of `Assets/GameObject/Guns/VFX/PowderKeg_Explosion_VFX.prefab` | position (0.45 m up), scale cannonImpactScale (x1.12 on a crit); 0.55 s |

### flamethrower (Flamethrower, ConeStream)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Stream | `Assets/_Game/V4/Weapons/Prefabs/flamethrower/flamethrower_Stream.prefab` | copy of `Assets/GameObject/Guns/VFX/Flamethrower_Stream_VFX.prefab` | its Flame particle system only (else the first): posed stream forwardOffset ahead of the ship, 0.45 m up; scale flamethrowerScale x 1.35; start speed scaled so the flame reaches the cone's range and cone angle = half the arc (from the Size stat and items); looping, emission switched on while streaming |
| PatchRing | `Assets/_Game/V4/Weapons/Prefabs/flamethrower/flamethrower_PatchRing.prefab` | built as the code built it (Ring) | Hellfire Nozzle patches: the outline (radius) and a ring shrinking as the patch burns out; orange, widths 0.3 and 0.45 (x the tuning's width) |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/flamethrower/flamethrower_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/flamethrower/flamethrower_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### flying_cutlasses (Flying Cutlasses, Orbit)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Orbit | `Assets/_Game/V4/Weapons/Prefabs/flying_cutlasses/flying_cutlasses_Orbit.prefab` | copy of `Assets/GameObject/Guns/Flying Cutlasses.prefab` | position (each blade where the simulation puts it), lying flat along the ship's heading; scale cutlassScale; its Blade child and particles shown, its root mesh hidden; particle colours tinted gold every frame |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/flying_cutlasses/flying_cutlasses_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/flying_cutlasses/flying_cutlasses_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### harpoon_gun (Harpoon Gun, Projectile)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Projectile | `Assets/_Game/V4/Weapons/Prefabs/harpoon_gun/harpoon_gun_Projectile.prefab` | copy of `Assets/GameObject/Guns/Harpoon.prefab` | position and facing (the shot's), uniform scale = shot radius x 2 x projectileSizeMultiplier x the config's scaleMultiplier (min 0.2); physics removed; cannonball trails cleared and boosted; every material tinted with the weapon's projectileColor |
| Muzzle | `Assets/_Game/V4/Weapons/Prefabs/harpoon_gun/harpoon_gun_Muzzle.prefab` | copy of `Assets/GameObject/Guns/Cannonball.prefab` | position and facing (the shot's), scale shootingVfxScale x the juice binding's muzzle scale (else the config's fallbackMuzzleScale); particles restarted; 0.35 s (a gun prefab shows only its CannonSmoke child) |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/harpoon_gun/harpoon_gun_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/harpoon_gun/harpoon_gun_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/harpoon_gun/harpoon_gun_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### lighthouse_lantern (Lighthouse Lantern, Orbit)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Orbit | `Assets/_Game/V4/Weapons/Prefabs/lighthouse_lantern/lighthouse_lantern_Orbit.prefab` | copy of `Assets/_Game/V4/Demo/Art/Weapons/Art_LighthouseLantern.prefab` | position (each blade where the simulation puts it), spinning 540 deg/s about Y; scale = config orbitModelScale; physics removed |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/lighthouse_lantern/lighthouse_lantern_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/lighthouse_lantern/lighthouse_lantern_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### lightning_mast (Lightning Mast, AreaStrike)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Strike | `Assets/_Game/V4/Weapons/Prefabs/lightning_mast/lightning_mast_Strike.prefab` | copy of `Assets/GameObject/Guns/Lightning Mast.prefab` | position on the water, scale lightningStrikeScale x strike radius / 3.5 (0.85-1.35); particles restarted; 1.15 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/lightning_mast/lightning_mast_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/lightning_mast/lightning_mast_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### long_nine (Long Nine, Projectile)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Projectile | `Assets/_Game/V4/Weapons/Prefabs/long_nine/long_nine_Projectile.prefab` | copy of `Assets/GameObject/Guns/Cannonball.prefab` | position and facing (the shot's), uniform scale = shot radius x 2 x projectileSizeMultiplier x the config's scaleMultiplier (min 0.2); physics removed; cannonball trails cleared and boosted; every material tinted with the weapon's projectileColor |
| Muzzle | `Assets/_Game/V4/Weapons/Prefabs/long_nine/long_nine_Muzzle.prefab` | copy of `Assets/GameObject/Guns/Long Nine.prefab` | position and facing (the shot's), scale shootingVfxScale x the juice binding's muzzle scale (else the config's fallbackMuzzleScale); particles restarted; 0.35 s (a gun prefab shows only its CannonSmoke child) |
| Beam | `Assets/_Game/V4/Weapons/Prefabs/long_nine/long_nine_Beam.prefab` | copy of `Assets/GameObject/Guns/LaserBeam.prefab` | both end points (muzzle to the weapon's range, 0.32 m up) and width (beam half-width x 2.4, 0.12-1.4 m); shown laserBeamDuration (0.22 s) |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/long_nine/long_nine_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/long_nine/long_nine_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/long_nine/long_nine_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### maelstrom_charge (Maelstrom Charge, PredictedArea)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| LandingRing | `Assets/_Game/V4/Weapons/Prefabs/maelstrom_charge/maelstrom_charge_LandingRing.prefab` | built as the code built it (Ring) | the outline (the shell's radius) and a ring filling over the shell's delay; for a whirlpool or ink slick the same over its seconds; orange (alpha rising), widths 0.3 and 0.45 (x the tuning's width) |
| Impact | `Assets/_Game/V4/Weapons/Prefabs/maelstrom_charge/maelstrom_charge_Impact.prefab` | copy of `Assets/GameObject/Guns/VFX/Mortar_Explosion_VFX.prefab` | 4 bursts a shell spread inside the ring (no random draw); scale weaponExplosionScale x mortarExplosionScaleMult x radius / 11 (0.18-0.34); particles replayed; 1.05 s; also the whirlpool's implosion |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/maelstrom_charge/maelstrom_charge_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/maelstrom_charge/maelstrom_charge_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/maelstrom_charge/maelstrom_charge_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |
| Whirlpool | `Assets/_Game/V4/Weapons/Prefabs/maelstrom_charge/maelstrom_charge_Whirlpool.prefab` | art team: `Assets/GameObject/Guns/Maelstrom Charge.prefab` (its Strike/Whirlpool child) | at the whirlpool's centre (0.12 m up) for its seconds; scale = whirlpool radius / baseRadius; spins 90 deg/s (shader _SpinOffset) and fades in and out (_Fade) |

### monkey_gunners (Monkey Gunners, Summon)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Summon | `Assets/_Game/V4/Weapons/Prefabs/monkey_gunners/monkey_gunners_Summon.prefab` | copy of `Assets/_Game/V4/Demo/Art/Weapons/Art_MonkeyBuccaneer.prefab` | position and facing (the simulation's summon), 0.2 m up; scale = config summonModelScale; physics removed |
| Projectile | `Assets/_Game/V4/Weapons/Prefabs/monkey_gunners/monkey_gunners_Projectile.prefab` | built as the code built it (MonkeyShot) | position and facing (the shot's), uniform scale = shot radius x 2 x projectileSizeMultiplier x the config's scaleMultiplier (min 0.2); physics removed; cannonball trails cleared and boosted |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/monkey_gunners/monkey_gunners_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/monkey_gunners/monkey_gunners_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/monkey_gunners/monkey_gunners_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### mortar (Mortar, PredictedArea)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| LandingRing | `Assets/_Game/V4/Weapons/Prefabs/mortar/mortar_LandingRing.prefab` | built as the code built it (Ring) | the outline (the shell's radius) and a ring filling over the shell's delay; for a whirlpool or ink slick the same over its seconds; orange (alpha rising), widths 0.3 and 0.45 (x the tuning's width) |
| Impact | `Assets/_Game/V4/Weapons/Prefabs/mortar/mortar_Impact.prefab` | copy of `Assets/GameObject/Guns/VFX/Mortar_Explosion_VFX.prefab` | 4 bursts a shell spread inside the ring (no random draw); scale weaponExplosionScale x mortarExplosionScaleMult x radius / 11 (0.18-0.34); particles replayed; 1.05 s; also the whirlpool's implosion |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/mortar/mortar_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/mortar/mortar_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/mortar/mortar_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |
| Lob | `Assets/_Game/V4/Weapons/Prefabs/mortar/mortar_Lob.prefab` | art team: `Assets/GameObject/Guns/MortarBarrage/Prefabs/Mortar_FireShell_VFX.prefab` | thrown from the ship's rail (1.6 m up) to each shell's ring in an arc of 5 m (x tuning arc), landing as the shell lands; scale 1 (x tuning scale) |
| Launch | `Assets/_Game/V4/Weapons/Prefabs/mortar/mortar_Launch.prefab` | art team: `Assets/GameObject/Guns/MortarBarrage/Prefabs/Mortar_Muzzle_VFX.prefab` | played at the ship's rail facing the target when a shell is thrown; scale 1; 1.2 s |

### parrot_squadron (Parrot Squadron, Summon)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Summon | `Assets/_Game/V4/Weapons/Prefabs/parrot_squadron/parrot_squadron_Summon.prefab` | copy of `Assets/_Game/V4/Demo/Art/Weapons/Art_ParrotSquadron_Parrot.prefab` | position and facing (the simulation's summon), 0.2 m up; scale = config summonModelScale; physics removed |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/parrot_squadron/parrot_squadron_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/parrot_squadron/parrot_squadron_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/parrot_squadron/parrot_squadron_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### powder_kegs (Powder Kegs, Mine)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Mine | `Assets/_Game/V4/Weapons/Prefabs/powder_kegs/powder_kegs_Mine.prefab` | copy of `Assets/GameObject/FireBarrel.prefab` | position (where the keg drops), scale mineBarrelScale; physics removed; an unblown keg sinks 1.4 m and tilts as it times out |
| Blast | `Assets/_Game/V4/Weapons/Prefabs/powder_kegs/powder_kegs_Blast.prefab` | copy of `Assets/GameObject/Guns/VFX/PowderKeg_Explosion_VFX.prefab` | position (0.45 m up), only its 'Impact small', 'Ring' and 'Smoke' children; scale max(1.15, powderKegExplosionScale x 2.35 x binding x blast radius / 2.8 (0.9-1.35)); particles restarted; 1.5 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/powder_kegs/powder_kegs_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale x 0.8; its particles' own length |

### siren_bell (Siren Bell, Aura)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| PulseRing | `Assets/_Game/V4/Weapons/Prefabs/siren_bell/siren_bell_PulseRing.prefab` | built as the code built it (PulseRing) | its 48 points (a circle sweeping out to the pulse radius, 0.2 m up), colour (the weapon's aimOverlayColor, fading), width multiplier (shrinking); a second ring at 80% follows |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/siren_bell/siren_bell_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/siren_bell/siren_bell_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |
| Aura | `Assets/_Game/V4/Weapons/Prefabs/siren_bell/siren_bell_Aura.prefab` | art team: `Assets/GameObject/Guns/TidalAura/TidalAura_VFX.prefab` | follows the ship (0.05 m up) while the weapon is equipped, from its first pulse; scale = pulse radius / baseRadius; turns 15 deg/s |

### squid_ink_pots (Squid Ink Pots, PredictedArea)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| LandingRing | `Assets/_Game/V4/Weapons/Prefabs/squid_ink_pots/squid_ink_pots_LandingRing.prefab` | built as the code built it (Ring) | the outline (the shell's radius) and a ring filling over the shell's delay; for a whirlpool or ink slick the same over its seconds; orange (alpha rising), widths 0.3 and 0.45 (x the tuning's width) |
| PulseRing | `Assets/_Game/V4/Weapons/Prefabs/squid_ink_pots/squid_ink_pots_PulseRing.prefab` | built as the code built it (PulseRing) | its 48 points (a circle sweeping out to the pulse radius, 0.2 m up), colour (the weapon's aimOverlayColor, fading), width multiplier (shrinking); a second ring at 80% follows |
| Lob | `Assets/_Game/V4/Weapons/Prefabs/squid_ink_pots/squid_ink_pots_Lob.prefab` | copy of `Assets/_Game/V4/Demo/Art/Weapons/Art_InkPot.prefab` | thrown from the ship's rail (1.6 m up) to each shell's ring, landing as the shell lands; scale lobScale (juice settings), arc lobArcHeight m; trails cleared |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/squid_ink_pots/squid_ink_pots_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/squid_ink_pots/squid_ink_pots_Hit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale enemyHitScale; its particles' own length |
| CritHit | `Assets/_Game/V4/Weapons/Prefabs/squid_ink_pots/squid_ink_pots_CritHit.prefab` | copy of `Assets/Prefabs/VFX/02_FishEnemy_DamageVFX.prefab` | position (0.35 m up), scale critHitScale; its particles' own length |

### swivel_guns (Swivel Guns, Projectile)

| Visual | Prefab | Made from | Code sets at run time |
|---|---|---|---|
| Projectile | `Assets/_Game/V4/Weapons/Prefabs/swivel_guns/swivel_guns_Projectile.prefab` | copy of `Assets/GameObject/Guns/Cannonball small.prefab` | position and facing (the shot's), uniform scale = shot radius x 2 x projectileSizeMultiplier x the config's scaleMultiplier (min 0.2); physics removed; cannonball trails cleared and boosted; every material tinted with the weapon's projectileColor |
| Muzzle | `Assets/_Game/V4/Weapons/Prefabs/swivel_guns/swivel_guns_Muzzle.prefab` | copy of `Assets/GameObject/Guns/Swivel Guns.prefab` | position and facing (the shot's), scale shootingVfxScale x the juice binding's muzzle scale (else the config's fallbackMuzzleScale); particles restarted; 0.35 s (a gun prefab shows only its CannonSmoke child) |
| WaterSplash | `Assets/_Game/V4/Weapons/Prefabs/swivel_guns/swivel_guns_WaterSplash.prefab` | copy of `Assets/Prefabs/VFX/19_WaterImpactSplash.prefab` | position (0.35 m up), scale waterImpactSplashScale; 0.55 s; merged with any splash within 1.35 m and 0.14 s |
| Hit | `Assets/_Game/V4/Weapons/Prefabs/swivel_guns/swivel_guns_Hit.prefab` | copy of `Assets/GameObject/Guns/VFX/PowderKeg_Explosion_VFX.prefab` | position (0.45 m up), scale cannonImpactScale (x1.12 on a crit); 0.55 s |

