# Rogue Pirates — Cursor Combat Redesign Implementation Spec

**Version:** 0.2  
**Date:** 15 September 2026  
**Purpose:** Cursor-ready implementation handoff for the six-weapon / higher-engagement combat redesign, including the expanded Weapon, enemy, Item, Powerup, and risk-event content library  
**Status:** Prototype implementation directive; tuning values are starting points, not production balance  
**Primary project references:** `Rogue_Pirates_GDD_v3_1`, `Rogue_Pirates_Combat_Content_v3_1`, `Rogue_Pirates_Captains_and_Mastery`, `Rogue Pirates — Narrative Bible v1.2`

---

# 0. Read this before changing code

This document is an **implementation patch to the current Rogue Pirates design**, not a request to rewrite the game.

The playtest finding driving this change is:

- 2 simultaneous Weapons became boring quickly.
- 6 simultaneous Weapons felt substantially more engaging.
- Rogue Pirates is top-down, auto-attacking, and movement-first. Unlike a third-person survivor-like, the player does not spend attention managing a camera or limited forward visibility.
- Therefore Rogue Pirates needs additional **combat density, spatial decision-making, build expression, target-priority pressure, and short-term player agency**.

The goal is **controlled chaos**:

> The ship should eventually feel like a ridiculous floating arsenal, but the player must still understand why they are steering, which threats matter, and what can kill them.

## Do not change these existing foundations unless a later section explicitly says so

- Unity / 3D implementation.
- Top-down follow camera.
- WASD / joystick direct movement.
- No manual aiming as a baseline requirement.
- Auto-attacks.
- Facing follows last non-zero movement direction.
- Direct steering; do not introduce realistic sailing inertia.
- 10-minute soft timer per stage.
- Overtime after 10:00.
- Boss victory secures the objective but does not end the run.
- Multi-stage Voyages carry the current build forward.
- Existing XP, Gold, chest, POI, Territory, raid, Ledger, Item-holding, economy, and persistence systems unless affected below.
- Existing deterministic simulation and combat-event guardrails.
- Existing Weapon max rank of 12 and milestone ranks 4 / 8 / 12.
- Existing two Logbook slots for this prototype. Do **not** expand Logbooks yet.

## Explicit overrides introduced by this document

1. **Standard Weapon capacity changes from 2 to 6.**
2. Weapon-slot capacity is no longer a permanent account progression gate for the core combat prototype.
3. A normal run starts with a Captain Signature Weapon and quickly receives a second Weapon draft.
4. Preserve the six existing production Weapons as a regression/test baseline: **Deck Cannon, Broadside Battery, Powder Kegs, Flying Cutlasses, Flamethrower, Lightning Mast**. Build the larger experimental Weapon library in this document as new data-driven content; do not depend on the previous expansion-Weapon list.
5. Several Weapons gain directional / heading-sensitive targeting so ship navigation becomes part of attacking.
6. Spawn pressure increases primarily through density and enemy composition before relying on HP inflation.
7. Captain presentation uses the current narrative names: **Rook, Hank, Richie, Sparky, Ember, Roxie**. Preserve stable internal IDs if older names already exist in code or saves.
8. A Captain Active Ability system is P1 and must be implemented behind a feature flag. It is not required to ship in the first six-slot A/B test.

---

# 1. Cursor execution rules

Before editing anything:

1. Search the repository for the actual implementations of:
   - Weapon inventory / slot capacity.
   - Weapon runtime instances.
   - level-up offer generation.
   - Weapon targeting.
   - Captain definitions.
   - Spawn Director / wave spawning.
   - enemy definitions.
   - damage attribution.
   - VFX pooling / projectile caps.
   - run snapshot / save serialization.
   - post-run stats / telemetry if present.
2. Do not create duplicate parallel systems if equivalents already exist.
3. Prefer extending current data-driven definitions / ScriptableObjects / config records over adding Weapon-specific conditionals to central controllers.
4. Preserve stable content IDs and save compatibility.
5. Put all new balance values in data/config, not hard-coded in gameplay loops.
6. Add feature flags / experiment profiles so the old and new combat can be compared without reverting code.
7. Keep gameplay deterministic at the simulation layer.
8. Do not modify public-economy or leaderboard authority rules as part of this task.
9. Placeholder VFX / primitive meshes are acceptable for prototype-only new behavior. Mechanics validation comes first.
10. After each implementation phase, run the acceptance checks in this document before moving on.

If class names differ from the names used below, adapt to the existing architecture rather than renaming the repository around this document.

---

# 2. High-level target experience

The player should be managing these decisions during combat:

1. **Where am I moving?**
2. **Which direction is the ship facing?**
3. **Which Weapon geometry do I want to line up?**
4. **Which enemy is currently the priority threat?**
5. **Do I break my safe route for XP, Gold, a chest, a POI, or a temporary Powerup?**
6. **Is my build strong enough to accept a greed/risk interaction?**
7. P1: **When should I use my Captain Skill?**

The player should *not* need to manually aim six weapons.

The desired combat equation is:

`fun = movement + heading + threat-reading + build-expression + escalation + payoff`

Six Weapons supply build expression and spectacle. Directional targeting, enemy roles, POIs, and telegraphs supply the missing action/decision load.

---

# 3. Build the experiment framework first

Create or extend a data-driven `CombatExperimentProfile` equivalent.

Minimum tunable fields:

```text
weaponSlotCap
startingWeaponCount
openingWeaponDraftEnabled
directionalWeaponTargetingEnabled
directorV2Enabled
captainActivesEnabled
weaponEvolutionsEnabled
spawnDensityMultiplier
enemyHpMultiplier
enemyDamageMultiplier
playerVfxIntensityMultiplier
telemetryEnabled
```

Create four initial profiles:

| Profile | Slots | Directional geometry | Director V2 | Purpose |
|---|---:|---|---|---|
| A_CURRENT | 2 | Off | Off | Existing baseline |
| B_SIX_ONLY | 6 | Off | Off | Test whether spectacle alone fixes boredom |
| C_SIX_DIRECTIONAL | 6 | On | Off | Test whether heading creates meaningful agency |
| D_FULL_COMBAT_V2 | 6 | On | On | Target redesign |

Do not fork gameplay code per profile. Profiles should only change configuration.

---

# 4. Six-Weapon Arsenal System — P0

## 4.1 Capacity

Set the normal run maximum to:

```text
MAX_WEAPON_SLOTS = 6
```

All six slots exist from the beginning of the run but can be empty.

Do **not** require permanent purchases to unlock slot 3–6 in this prototype.

If a permanent Weapon-slot purchase system already exists:

- Disable it for new combat profiles.
- Hide it from prototype UI.
- Do not delete save fields yet.
- Preserve existing serialized values for migration safety.
- Do not spend time designing compensation/refunds for pre-release test data.

Future Captains may modify slot count as a character rule, but the baseline is six.

## 4.2 Starting arsenal

Normal Voyage:

```text
Weapon 1 = Captain Signature Weapon, rank 1
Weapon 2 = opening draft choice from 3 eligible Weapons
Weapon 3–6 = empty
```

The opening draft should happen immediately after the run begins or after a very short safe introduction, before the combat has time to feel sparse.

For first conquest/tutorial:

- Preserve forced Deck Cannon teaching.
- Present the opening Weapon draft after the first short combat beat or first level-up.
- Target: player has 2 Weapons within the first 30–45 active seconds.

## 4.3 Weapon acquisition pacing target

Target median arsenal size:

| Active stage time | Median equipped Weapons |
|---:|---:|
| 0:30 | 2 |
| 1:30 | 3 |
| 3:00 | 4 |
| 5:00 | 5 |
| 7:00 | 6 |
| 7:00–10:00 | Upgrade / capstone phase |
| Overtime | Fully developed build under escalating pressure |

Do not auto-grant Weapons to hit the target. Use offer weighting and pity guarantees so the player still chooses.

## 4.4 Offer-generation changes

Replace the fixed new-Weapon weighting with fill-state-aware weighting.

Recommended category weights before normalization:

```text
if equippedWeapons <= 2:
    equippedWeaponUpgrade = 25
    newWeapon              = 45
    equippedLogbookUpgrade = 20
    newLogbook             = 10

if equippedWeapons == 3:
    equippedWeaponUpgrade = 35
    newWeapon              = 35
    equippedLogbookUpgrade = 20
    newLogbook             = 10

if equippedWeapons == 4:
    equippedWeaponUpgrade = 42
    newWeapon              = 28
    equippedLogbookUpgrade = 20
    newLogbook             = 10

if equippedWeapons == 5:
    equippedWeaponUpgrade = 50
    newWeapon              = 20
    equippedLogbookUpgrade = 20
    newLogbook             = 10

if equippedWeapons >= 6:
    equippedWeaponUpgrade = 65
    newWeapon              = 0
    equippedLogbookUpgrade = 25
    newLogbook             = 10
```

Continue to remove unavailable categories and renormalize.

### New Weapon pity

Guarantee **an option**, not an automatic pickup.

Suggested thresholds:

```text
by player level 4  -> offer path to at least 3 Weapons
by player level 8  -> offer path to at least 4 Weapons
by player level 12 -> offer path to at least 5 Weapons
by player level 16 -> offer path to at least 6 Weapons
```

If the player is below the target count, one of the three level-up cards must be a new eligible Weapon until the player accepts one or deliberately removes all eligible Weapons through valid banish rules.

The player may still intentionally refuse the Weapon.

## 4.5 Do not dilute upgrade depth

Weapon ranks remain 1–12.

Do not reduce rank count merely because there are more Weapons.

The run should have three phases:

```text
EARLY  = fill arsenal
MID    = specialize / rank Weapons
LATE   = reach rank-12 milestones and optional evolutions
```

---

# 5. Weapon Content Plan — baseline six + new experimental roster

The combat redesign is allowed to **redo the Weapon roster**. Do not treat the previous 12-Weapon list as sacred.

However, keep these six existing Weapons fully functional because they are valuable regression anchors and let us compare the new combat model against mechanics that already work:

1. **Deck Cannon**
2. **Broadside Battery**
3. **Powder Kegs**
4. **Flying Cutlasses**
5. **Flamethrower**
6. **Lightning Mast**

Do not delete their IDs, prefabs, data, unlocks, or tests. They remain valid content even if new Weapons become the preferred production roster.

## 5.1 Recommended first 12-Weapon playtest pool

To test six-slot variety without implementing the entire backlog, use the six existing Weapons plus six new ones with very different geometry:

| Weapon | Role | Why it belongs in the first test |
|---|---|---|
| Deck Cannon | Forward generalist | Familiar baseline |
| Broadside Battery | Lateral burst | Makes heading matter |
| Powder Kegs | Aft trap / area denial | Rewards kiting and route planning |
| Flying Cutlasses | Close orbit | Passive defensive coverage |
| Flamethrower | Forward sustained cone | Directional commitment / DoT |
| Lightning Mast | Omni chain | Reliable low-attention background damage |
| **Long Nine** | Long forward precision line | Elite / boss priority and lining-up skill |
| **Swivel Guns** | Rapid 360-degree nearest-target fire | Reliable horde cleanup and proc test |
| **Grapeshot** | Short broad cone | Aggressive close-range clearing |
| **Harpoon Gun** | Tether / line control | Turns enemy placement into a weapon |
| **Mortar** | Delayed predicted AoE | Forces route prediction and creates audiovisual peaks |
| **Boarding Party** | Autonomous skiff summons | Tests summon archetype and single-target pressure |

This 12-Weapon test roster intentionally includes directional, omni, orbit, trap, delayed AoE, control, sustained, precision, and summon behaviors.

## 5.2 Full experimental Weapon library

These are the new content targets to keep in the Cursor document. They are **original Rogue Pirates concepts** and can be rebalanced or renamed during implementation.

| Weapon | Spatial behavior | Gameplay role | Evolution / capstone direction |
|---|---|---|---|
| **Long Nine** | Long forward line | Elite/boss killer | **Dead Man's Eye** — weak-point shot pierces every target in its lane |
| **Swivel Guns** | 360° nearest target | Reliable rapid fire | **Lead Hurricane** — shots ricochet through nearby targets |
| **Grapeshot** | Short broad cone | Close-range clearing | **Iron Tempest** — wider cone + stronger knockback |
| **Chainshot** | Forward arc | Slow/control | **Iron Web** — hit target tethers nearby enemies for a short duration |
| **Harpoon Gun** | Forward projectile + tether | Position/control | **Leviathan Chain** — tether becomes a damaging line enemies can cross |
| **Ricochet Cannon** | Heavy bouncing cannonball | Dense packs / geometry | **Devil's Marbles** — kills grant bounded extra bounces |
| **Mortar** | Delayed target circle | Predictive AoE | **Royal Bombard** — cluster shells land around the primary impact |
| **Sea Mines** | Drop behind ship | Kiting/control | **Devil's Wake** — nearby mines chain-detonate with bounded propagation |
| **Fire Pots** | Lob + burning patch | Area denial | **Hellfire Rum** — enemies killed while burning create a small blast |
| **Burning Wake** | Fire trail behind ship | Movement weapon | **Hellwake** — distance sailed charges stronger flame segments |
| **Storm Rod** | Chains nearby enemies | Density scaling | **Tempest Crown** — every N chain events calls a delayed lightning strike |
| **Frost Cannon** | Impact/cone slow | Crowd control | **White Squall** — heavily slowed kills shatter for bounded AoE |
| **Phantom Cannon** | Piercing spectral shot | Lines / terrain pressure | **Wraith Battery** — projectiles return through enemies |
| **Cursed Cannon** | Stronger at low Hull | Risk DPS | **Last Breath** — short firing frenzy delays fatal damage, then resolves the debt |
| **Captain's Pistol** | Targets strongest nearby enemy | Priority targeting | **Executioner's Mark** — damage rises as target Hull falls |
| **Powder Monkey** | Crew throws randomized bombs | Chaotic AoE | **Mad Monkey Crew** — multiple bomb types with strict proc caps |
| **Boarding Party** | Summoned skiffs chase targets | Summon / single target | **Black Flag Armada** — elite kills add temporary boarders up to a hard cap |
| **Cutlass Crew** | Rotating close-range blades | Defensive orbit | **Bladed Tide** — orbit radius rhythmically expands and contracts |
| **Anchor Flail** | Heavy orbiting anchor | Knockback / impact | **World Anchor** — periodic radial slam |
| **Whirlpool Relic** | Pull field around target area | Grouping | **Maelstrom** — grouped enemies take escalating bounded damage |
| **Kraken Tentacle** | Periodic directional slam | Heavy crowd control | **Deep One's Grasp** — slams leave brief grasping zones |
| **Ghost Fleet** | Autonomous spectral ship | Summon/support | **Dread Armada** — additional temporary ghost ship from elite kills, capped |
| **Keg Raft** | Slow homing explosive | Boss/crowd burst | **Floating Catastrophe** — splits into mini-kegs on detonation |

The six preserved baseline Weapons can also coexist with these rather than being replaced. Avoid creating near-identical duplicates without a distinct spatial reason. For example, Powder Kegs and Sea Mines may share trap code but should feel different in timing/placement; Flying Cutlasses and Cutlass Crew should be consolidated if playtests cannot distinguish them.

## 5.3 Weapon families

Add tags now even if family bonuses come later:

- **Gunnery:** Deck Cannon, Broadside Battery, Long Nine, Grapeshot, Swivel Guns, Ricochet Cannon.
- **Explosive:** Powder Kegs, Mortar, Sea Mines, Keg Raft, Fire Pots.
- **Fire:** Flamethrower, Fire Pots, Burning Wake.
- **Storm:** Lightning Mast, Storm Rod, Frost Cannon, Chainshot.
- **Hunter:** Harpoon Gun, Long Nine, Captain's Pistol.
- **Crew:** Boarding Party, Powder Monkey, Cutlass Crew, Ghost Fleet.
- **Cursed:** Phantom Cannon, Cursed Cannon, Kraken Tentacle, Whirlpool Relic.
- **CloseRange:** Flying Cutlasses, Grapeshot, Anchor Flail, Cutlass Crew.
- **Control:** Chainshot, Harpoon Gun, Frost Cannon, Whirlpool Relic, Sea Mines.

Do not ship simple spreadsheet set bonuses by default. If family bonuses are tested, make them behavioral. Examples:

- 3 Gunnery Weapons: firing a Broadside reloads forward cannons by 10%.
- 3 Explosive Weapons: every fifth explosion drops one inert Powder Keg that another explosion can detonate.
- 3 Crew Weapons: summons inherit a reduced portion of player attack-speed bonuses.

Every family interaction must obey proc/event caps.

## 5.4 Proc coefficients are mandatory

Six Weapons plus conditional Items can create runaway proc chains. Every damaging Weapon/effect needs a proc coefficient. Starting guidance:

| Source type | Suggested coefficient |
|---|---:|
| Slow cannon / precision shot | 1.0 |
| Mortar explosion | 0.8 |
| Broadside individual ball | 0.7 |
| Chain lightning target | 0.5 |
| Rapid swivel shot | 0.35 |
| Burning-ground tick | 0.2 |
| Persistent Whirlpool tick | 0.15 |

`effectiveProcChance = itemBaseChance * sourceProcCoefficient`

Triggered Item effects normally cannot trigger themselves or unrestricted chains of other Item effects. Exceptions must be explicit, generation-capped, and tested.

---

# 6. Directional Weapon Geometry — P0

## 6.1 Principle

Navigation should function as soft aiming.

Do not make every Weapon 360-degree nearest-target auto-fire.

Add/extend a reusable targeting profile instead of custom logic per Weapon.

Suggested targeting modes:

```csharp
OmniNearest
ForwardArc
AftArc
PortStarboard
ForwardCone
AftDeploy
OrbitContact
OmniClusterGround
OmniPulse
TetherTarget
BounceProjectile
TrailDeploy
SummonAutonomous
DirectionalSlam
PullField
```

Suggested common fields:

```text
targetingMode
range
arcDegrees
sideArcDegrees
preferElite
preferHighestHull
preferDensestCluster
requiresTarget
usesHeadingSnapshot
predictionSeconds
procCoefficient
familyTags[]
```

Heading should come from the existing last-nonzero-movement facing.

## 6.2 Preserved baseline Weapon assignments

### Deck Cannon

```text
Mode: ForwardArc
Arc: 120 degrees
Range: existing 22m
Priority: nearest valid enemy in arc
```

If no enemy is in the arc, do not silently rotate the ship or projectile toward an enemy behind the player.

### Broadside Battery

Keep port/starboard behavior and snapshot heading at emission.

### Powder Kegs

Keep aft deployment. Kegs reward kiting and route planning.

### Flying Cutlasses

Keep orbit behavior. This is a low-attention defensive Weapon.

### Flamethrower

Keep forward-facing cone. Do not auto-track independently of heading.

### Lightning Mast

Keep omni nearest-target/chaining behavior. Not every Weapon should demand heading.

## 6.3 New Weapon geometry examples

- **Long Nine:** ForwardArc, ~35–45°; prefers elite/highest-Hull target.
- **Swivel Guns:** OmniNearest; low per-shot proc coefficient.
- **Grapeshot:** ForwardCone, ~90–120°, short range.
- **Chainshot:** ForwardArc, ~100°, control priority.
- **Harpoon Gun:** ForwardArc/TetherTarget, ~70–80°, prefers elite/high-Hull.
- **Ricochet Cannon:** ForwardArc or OmniNearest depending test profile; bounce resolution deterministic.
- **Mortar:** OmniClusterGround with predicted destination targeting.
- **Sea Mines:** AftDeploy / TrailDeploy.
- **Fire Pots:** OmniClusterGround or forward lob; leaves bounded hazard.
- **Burning Wake:** TrailDeploy behind ship.
- **Storm Rod:** OmniNearest chain.
- **Frost Cannon:** ForwardCone or impact-splash ForwardArc.
- **Phantom Cannon:** ForwardArc; may ignore ordinary terrain if clearly signaled.
- **Cursed Cannon:** ForwardArc; low-Hull modifier is stat logic, not targeting logic.
- **Captain's Pistol:** OmniNearest with `preferHighestHull=true`.
- **Powder Monkey:** SummonAutonomous / randomized valid target selection.
- **Boarding Party:** SummonAutonomous; skiffs physically chase their target.
- **Cutlass Crew / Anchor Flail:** OrbitContact.
- **Whirlpool Relic:** PullField around densest local cluster.
- **Kraken Tentacle:** DirectionalSlam around predicted target sectors.
- **Ghost Fleet:** SummonAutonomous.
- **Keg Raft:** autonomous slow homing explosive.

## 6.4 Desired arsenal mix

A good six-Weapon build should usually contain some combination of:

- 2–3 Weapons that care strongly about heading.
- 1–2 passive/reliable Weapons.
- 1 crowd-control or route-planning Weapon.
- 1 high-impact / delayed / boss-priority Weapon.

Do not force this composition through hard slot categories yet. Let the draft system produce it and measure outcomes.

---

# 7. Difficulty / Spawn Director V2 — P0

## 7.1 Design rule

Six Weapons must not be countered only by turning enemies into HP sponges.

Scale difficulty in this priority order:

1. enemy density,
2. enemy composition,
3. simultaneous threat types,
4. elite frequency / dangerous formations,
5. HP,
6. damage,
7. movement speed last.

The player should frequently feel overpowered for short windows. Do not rubber-band enemy HP directly to current player DPS.

## 7.2 Preserve current 10-minute stage structure

Keep:

- 3:00 elite pressure beat.
- 4:00 temporary director-rate pulse.
- 7:00 temporary director-rate pulse.
- 8:00 elite pressure beat.
- 10:00 overtime transition.

The new director changes **what is spawned and how much pressure is present**, not the basic stage timer.

## 7.3 P0 scaling starting point

Keep current enemy HP and damage formulas initially so we can isolate the effect of six Weapons and denser spawns.

Keep current normal-stage HP growth:

```text
HP scale includes: (1 + 0.12 * minute)
```

Keep current normal-stage damage growth:

```text
Damage scale includes: (1 + 0.08 * minute)
```

Increase density curve for six-Weapon profiles.

Recommended normal-stage base spawn rate:

```text
x = min(minute, 10)
spawnRate = min(
    16,
    (2.5 + 0.55*x + 0.025*x*x)
    * (1 + 0.25*(stageDepth-1))
    * experiment.spawnDensityMultiplier
)
```

Approximate stage-1 values at multiplier 1.0:

| Minute | Enemies/sec |
|---:|---:|
| 0 | 2.5 |
| 2 | 3.7 |
| 4 | 5.1 |
| 6 | 6.7 |
| 8 | 8.5 |
| 10 | 10.5 |

Keep current ordinary-enemy cap of 180 for the first implementation. Do not create spawn debt while capped.

If performance or readability fails, reduce spawn rate before increasing HP.

## 7.4 Add threat costs to enemy definitions

Add a `threatCost` field even if the first version still uses roster weights.

Recommended initial values:

| Enemy | Threat cost |
|---|---:|
| Shark | 1.0 |
| Swordfish | 1.6 |
| Cannonboat | 2.0 |
| Manta | 2.3 |
| Reef Crab | 3.0 |
| Hammer Head | 4.5 |
| Kracken Spawn | 5.5 |
| Needle Sloop | 2.0 |
| Ram Raider | 3.0 |
| Broadside Brig | 3.5 |
| Long-Nine Gunboat | 4.0 |
| Bomb Ketch | 5.0 |
| Net Cutter | 4.0 |
| Powder Barge | 4.0 |
| Standard Bearer | 5.0 |
| Repair Tender | 6.0 |
| Shield Frigate | 7.0 |
| Boarder Skiff | 4.5 |
| Treasure Raider | 4.0 |
| Ghost Ship | 5.5 |
| Stormcaller | 6.5 |
| Natural Elite | base cost × 4 |

This enables future budget-based formation spawning without rewriting enemy content again.

## 7.5 Enemy composition progression

Use time to unlock tactical combinations, not just bigger crowds.

### 0:00–2:00 — learn and build

- Sharks
- Cannonboats
- Swordfish

Primary player task: movement + learning Weapon geometry.

### 2:00–4:00 — lanes and flanking

- add Mantas
- slightly increase Swordfish weight
- begin mixed ranged + charger groups

Primary task: stop holding one direction forever.

### 4:00–6:00 — space control

- add Reef Crabs
- begin Hammer Heads
- pair slow tanks with ranged enemies

Primary task: path selection and priority.

### 6:00–8:00 — overlapping threat roles

- add Kracken Spawn
- more Hammer Heads
- formations should contain at least two behavior types

Primary task: manage delayed zones while dodging charges / projectiles.

### 8:00–10:00 — build test

- higher special-enemy weighting
- scheduled elite at 8:00
- favor mixed groups instead of homogeneous fodder

Primary task: expose weaknesses in the player's build before overtime.

## 7.6 Formation / pack spawning

Add reusable Pack definitions behind Director V2.

A Pack is a composition request, not a new enemy type.

Initial packs:

```text
SWARM:
  8–16 Sharks

CHARGE_SCREEN:
  4 Sharks + 2 Swordfish

GUNLINE:
  4 Sharks + 2 Cannonboats + 1 Manta

ANCHOR_AND_FIRE:
  1 Reef Crab + 2 Cannonboats + 4 Sharks

HUNTER_GROUP:
  1 Hammer Head + 2 Swordfish + 4 Sharks

ZONE_PRESSURE:
  1 Kracken Spawn + 2 Mantas + 4 Sharks

LATE_MIX:
  1 Hammer Head + 1 Kracken Spawn + 2 ranged + 4 fodder
```

Spawn groups from multiple valid offscreen anchors so every pack does not appear as one blob.

Do not spawn geometry that removes every safe lane.

## 7.7 Anti-passivity rule

The director should periodically interrupt a single perpetual orbit.

Do this through enemy behavior, not invisible punishment.

At least one of these should be present in medium/high intensity compositions:

- charge line,
- delayed circle,
- ranged firing lane,
- armored body blocking ideal path,
- slow zone,
- priority target.

Do not add an arbitrary anti-circle damage debuff.

---

# 8. Enemy Content Plan — preserve seven existing + add role-driven enemies

Only these existing enemy families are required to remain from the current combat spec:

1. **Cannonboat**
2. **Shark**
3. **Swordfish**
4. **Manta**
5. **Reef Crab**
6. **Hammer Head**
7. **Kracken Spawn**

Do not carry forward other existing enemy families merely because they are already in the old design. If old content such as Siren/Fire Eel exists in code, it can remain disabled or be repurposed later; it is not part of the new baseline roster.

The core rule for every new enemy is:

> **What does this enemy make the player do differently?**

If the answer is only “deal more DPS,” redesign it.

## 8.1 Roles for the seven preserved enemies

| Enemy | Core behavior | Player response |
|---|---|---|
| **Shark** | Direct swarm pursuit | Maintain spacing / fuels early kills |
| **Swordfish** | Telegraph → fast charge | Sidestep / change route |
| **Cannonboat** | Ranged telegraphed shot | Break firing lane / close or evade |
| **Manta** | Lateral fan/sweep projectiles | Read lanes / avoid predictable orbit |
| **Reef Crab** | Armored body + punishable slam window | Bait attack then punish weak window |
| **Hammer Head** | Heavy telegraphed charge / displacement | Respect line / reposition early |
| **Kracken Spawn** | Delayed tentacle zones | Route planning / leave open space |

## 8.2 New enemy backlog

Keep these concepts in data/backlog even if only a subset is implemented for the first slice.

| Enemy | Behavior | What it forces the player to do | Priority |
|---|---|---|---|
| **Deckhand Dinghy** | Cheap basic swarm boat | Maintain spacing; director filler | Later / may overlap Shark |
| **Needle Sloop** | Fast flanker that approaches from side/rear angles | Turn/change route rather than orbit forever | High |
| **Ram Raider** | Predicts heading, telegraphs, then commits to a charge | Late sidestep; weapon alignment tradeoff | High |
| **Broadside Brig** | Telegraphs dangerous lateral cannon arcs | Avoid sailing parallel; cross bow/stern lanes | High |
| **Long-Nine Gunboat** | Long-range line shot from off-center | Break line / prioritize distant threat | High |
| **Bomb Ketch** | Mortars predicted destination ahead of player | Change direction before impact | High |
| **Net Cutter** | Fires a visible slowing net across likely escape path | Dodge projectile / kill priority | High |
| **Powder Barge** | Slow volatile boat; explodes after death warning, damaging nearby enemies too | Set up chain reactions / choose kill timing | High |
| **Shield Frigate** | Strong frontal armor, vulnerable rear/side | Reposition and exploit facing | High |
| **Standard Bearer** | Buffs nearby enemies' speed/damage; visible aura | Priority target | Medium |
| **Repair Tender** | Heals lowest-Hull nearby non-boss enemy with visible beam | Priority target / burst through protection | High |
| **Boarder Skiff** | Rushes and latches onto player, applying pressure until destroyed/shaken off | Emergency close-range target | Medium |
| **Treasure Raider** | Steals loose Gold/treasure and flees | Optional greed chase vs safe route | High |
| **Ghost Ship** | Periodically phases; cannot be damaged while fully phased but telegraphs return | Time burst window | Medium |
| **Stormcaller** | Creates delayed storm zones / moving hazard corridors | Route around shrinking safe space | Medium |
| **Mine Layer** | Leaves visible mines across common turning paths | Alter turning radius / shoot or avoid mines | Medium |
| **Salvage Leech** | Pulls nearby uncollected XP/Gold toward itself, drops conserved value on death | Decide whether to chase loot thief | Medium |
| **Fog Cutter** | Lowers contrast/visibility around nearby enemies without hiding telegraphs | Kill support unit / fight with constrained information | Later |
| **Whirlpool Caller** | Places telegraphed pull fields that bend movement, never hard-stun | Counter-steer / avoid bad geometry | Later |
| **Mimic Wreck** | Appears like salvage/POI until proximity activation, then attacks | Approach rewards carefully / recognize tells | Later |

## 8.3 Encounter composition is more important than raw stats

Individual enemies are vocabulary. Encounters are sentences. Examples:

- **Repair Tender + 2 Shield Frigates + Bomb Ketch** → target-priority puzzle under movement pressure.
- **Ram Raider + Sea Mines / Mine Layer** → evasive movement into dangerous terrain.
- **Shield Frigate + rear-fire player build** → forces circular maneuvering and flanking.
- **Treasure Raider + Shark swarm** → greed-versus-survival.
- **Net Cutter + Bomb Ketch** → getting slowed becomes terrifying because artillery predicts destination.
- **Standard Bearer + Swordfish pack** → obvious priority target before chargers become overwhelming.
- **Powder Barge + dense fodder** → player can intentionally cause a satisfying chain clear.

The Director should unlock new **combinations**, not merely higher enemy stats.

## 8.4 First specialist-enemy implementation set

For the first Director V2 content pass, build these six because each exercises a different system:

1. **Needle Sloop** — flanking.
2. **Ram Raider** — predictive charge.
3. **Bomb Ketch** — destination artillery.
4. **Net Cutter** — movement-control threat.
5. **Repair Tender** — target priority/support.
6. **Powder Barge** — player-triggered environmental chain reaction.

Then add **Shield Frigate** and **Treasure Raider** once the core set is readable.

## 8.5 New enemy implementation contract

Every enemy definition should support:

```text
enemyId
roleTags[]
threatCost
baseHull
baseDamage
baseSpeed
attackBehaviorId
telegraphProfileId
preferredRange
spawnPhaseMin
spawnPhaseMax
eliteEligible
rewardProfileId
formationTags[]
prioritySignalLevel
```

Behavior state machines should be reusable (charge, artillery, support beam, flank, flee, phase, deploy hazard) instead of one monolithic script per enemy.

---

# 9. Captain system redesign

## 9.1 Narrative naming

Current playable display names are:

- Rook — Generalist
- Hank — Tank
- Richie — Smuggler
- Sparky — Powderrunner
- Ember — Firekeeper
- Roxie — Stormchaser

Penny remains the Quartermaster / systems character and is not part of the six playable-Captain roster in this implementation.

If code already uses earlier internal IDs, **do not rename stable IDs** merely to match display names.

Suggested migration mapping:

```text
rook    -> Rook
brine   -> Hank
vale    -> Richie
flint   -> Sparky
cinder  -> Ember
tempest -> Roxie
```

Use display/localization fields for the narrative names.

## 9.2 Keep existing passive identities for P0

Do not simultaneously rewrite all Captain balance while testing six Weapons.

Preserve the existing passive logic initially:

- Rook: general damage scaling.
- Hank: extra Hull / Armor, slower movement.
- Richie: Gold pickup + unspent-Gold damage tension.
- Sparky: speed + Powder Keg synergy.
- Ember: Burn scaling + earned healing.
- Roxie: Lightning scaling + near-miss burst.

These already cover distinct build incentives.

## 9.3 P1 Captain Active Ability system

Add one Captain Skill button only after P0 is stable.

Input:

```text
Desktop: Space by default
Controller: face button
Mobile: one large skill button
```

Do not add six manually fired Weapon buttons.

Suggested skills:

### Rook — All Guns!

```text
Cooldown: 18s
Effect: immediately request one legal emission from every equipped Weapon.
Rules: does not reset persistent orbit Weapons; they instead receive a short +20% hit-rate / rotation bonus for 2s.
```

Fantasy: classic all-in broadside moment.

### Hank — Brace!

```text
Cooldown: 20s
Duration: 2.0s
Effect: 60% incoming damage reduction, knockback immunity.
Tradeoff: -25% movement speed during Brace.
```

Fantasy: survive something other Captains must dodge.

### Richie — Cut and Run

```text
Cooldown: 16s
Duration: 3.0s
Effect: +35% movement speed and +50% pickup radius.
Extra: Gold collected during the effect contributes to a short 5s +10% damage buff, capped once per cast.
```

Fantasy: greed creates the movement objective.

### Sparky — Light the Fuse

```text
Cooldown: 18s
Effect: safely arms all currently deployed owner Kegs for near-immediate detonation.
For 4s: +20% explosion radius, not additional damage.
```

Respect existing chain/event caps.

### Ember — Stoke the Flames

```text
Cooldown: 18s
Duration: 5s
Effect: +30% Burn application damage and +20% Flamethrower cone length.
Do not reduce tick interval.
```

### Roxie — Ride the Gale

```text
Cooldown: 15s
Duration: 2.5s
Effect: +40% movement speed.
On cast: emit one Lightning Mast-style chain at reduced base damage even if Lightning Mast is not equipped.
Near misses during the effect reduce remaining cooldown by a bounded amount.
```

All Active values are prototype data and must live in Captain config.

---

# 10. Weapon Evolutions / Capstones — P1

Do not replace the current 12-rank system.

Add an optional **Evolution** layer after rank 12.

Eligibility:

```text
Weapon rank == 12
AND required synergy Item is present in current run
AND evolution has not already been taken
```

When eligible, future Weapon-upgrade offers may present the Evolution card.

An Evolution should modify behavior, not just add damage.

Prototype only the first six core Weapons initially.

## Deck Cannon + Grapeshot Pouch -> Stormshot Cannon

- retains forward arc.
- main projectile gains +1 pierce.
- first enemy hit emits a wider bounded Grapeshot burst.
- fragments remain non-recursive.

## Broadside Battery + Broadside Drum -> King's Broadside

- a normal volley fires as usual.
- 0.35s later the opposite side fires a smaller follow-up volley.
- follow-up cannot itself trigger another follow-up.

## Powder Kegs + Double-Fuse Cord -> Devil's Wake

- Kegs deploy in a slightly wider aft pattern.
- natural Keg explosions trigger a short visual chain wave to nearby owner Kegs within existing propagation limits.
- no infinite detonation recursion.

## Flying Cutlasses + Serrated Scabbard -> Bladed Tide

- orbit periodically expands from close radius to medium radius and returns.
- bleed strength remains bounded by existing stack limits.
- creates a rhythm where moving close to enemies at the right moment is valuable.

## Flamethrower + Dragon's Breath Nozzle -> Hellfire Nozzle

- cone width increases.
- burning enemies killed by direct Flamethrower damage leave a short-lived flame patch.
- cap active patches.
- patches do not trigger generic on-hit procs.

## Lightning Mast + Copper Conductor -> Tempest Mast

- one additional native jump.
- every Nth native chain creates a visible delayed lightning marker on a nearby cluster.
- marker strike respects secondary-event caps and cannot charge itself.

Implement Evolution as data wherever possible:

```text
evolutionId
baseWeaponId
requiredItemId
behaviorModifierId
visualOverrideId
```

Do not hard-code a giant `switch(weaponId)` in the level-up controller.

---

# 11. Items / Modules and build structure

Keep the conceptual separation:

```text
Weapon   = what attacks
Logbook  = which global stat grows
Item     = conditional interaction / synergy / build rule
Powerup  = temporary spike
Captain  = starting identity / rule bias
```

Do not waste Item slots on effects that are only `+X% generic damage`; those belong in Logbooks/Crew Drills.

## 11.1 Item tags

Standardize these tags for analytics and future offer logic:

```text
Gunnery
Explosive
Crew
Storm
Fire
Control
Cursed
Mobility
Crit
Economy
Summon
CloseRange
LongRange
Tank
Risk
Exploration
```

## 11.2 Proposed Item / Module library

Keep these concepts in the Cursor content backlog. Exact values must be data-driven and tuned through playtests.

| Item | Core effect | Build identity |
|---|---|---|
| **Powder Magazine** | +1 projectile; individual projectile damage reduced | Multi-projectile |
| **Gunner's Gloves** | Fire rate rises briefly after meaningful heading changes | Active steering |
| **Bloody Doubloon** | Rerolls consume Hull before Gold/charges | Risk/economy |
| **Cursed Compass** | More enemies spawn; treasure quality improves | High-density greed |
| **Ghost Lantern** | Successfully dodging a telegraphed attack fires a spectral shot | Evasion |
| **Reinforced Keel** | Damage reduction while above a movement-speed threshold | Mobility/tank |
| **Barnacled Hull** | Taking collision/contact damage retaliates with bounded damage | Tank/ram |
| **Captain's Bell** | Every N legitimate kills requests one free legal emission from cooldown Weapons | Kill-speed |
| **Black Powder Keg** | Critical explosive hits can cause a bounded secondary blast | Crit/explosive |
| **Stormglass** | Chain effects may jump +1 target, respecting target caps | Storm |
| **Hangman's Rope** | First hit on full-Hull enemy marks it; marked kill grants temporary crit | Opener/burst |
| **Smuggler's Hold** | +1 shop/chest choice; purchases raise Notoriety | Economy/risk |
| **Navigator's Sextant** | Damage charges with distance sailed since last hit taken | Mobility |
| **Rum Barrel** | Elite kill heals a small amount + brief speed burst | Aggressive sustain |
| **Golden Figurehead** | Luck increases with unspent run Gold | Greed |
| **Loaded Dice** | Every Nth critical becomes a bounded mega-crit | Crit |
| **Leviathan Tooth** | Crowd-controlled enemies take increased damage | Control |
| **Shark Charm** | Moving toward injured priority enemies grants speed | Aggressive navigation |
| **Powder Fuse** | Distance sailed charges next explosion's radius/damage | Mobility/explosive |
| **Royal Warrant** | More elites appear; elite rewards improve | Voluntary risk |
| **Dead Man's Map** | Treasure events reveal earlier but spawn guarded | Exploration |
| **Blood-Soaked Flag** | Lifesteal/overheal beyond max Hull converts to temporary armor | Sustain |
| **Crewman's Whistle** | Summon deaths reduce summon cooldown, bounded per second | Summon |
| **Broken Crown** | Damage rises per Notoriety tier; armor falls per tier | Glass cannon/risk |
| **Sea Witch's Eye** | Status effects can spread to nearby enemies with proc coefficient | DoT/status |
| **Powder Saint** | First explosive hit every N seconds gains massively larger area | Burst |
| **Phantom Rudder** | Perfect/near-miss dodge briefly increases projectile quantity where compatible | Evasion |
| **Admiral's Ledger** | More distinct Weapon families equipped increase global damage modestly | Rainbow build |
| **Old Salt's Pipe** | Repeated/same-family Weapons gain attack speed | Specialisation |
| **Kraken Heart** | Boss damage dealt charges periodic tentacle strike | Boss build |

Do not implement all Items at once. The first test set should cover at least: steering, proc, greed, tank, summon, control, crit, and explosion interactions.

## 11.3 Suggested first 12 new Items for implementation

1. Gunner's Gloves
2. Cursed Compass
3. Ghost Lantern
4. Reinforced Keel
5. Captain's Bell
6. Black Powder Keg
7. Stormglass
8. Golden Figurehead
9. Leviathan Tooth
10. Powder Fuse
11. Sea Witch's Eye
12. Admiral's Ledger

These give enough interaction variety to test whether Items actually change sailing/build decisions.

---

# 12. Temporary Powerups — P1

Powerups occupy no inventory slot and should radically change the next few seconds. They are intentionally separate from Items, Logbooks, POIs, and permanent progression.

Keep the existing prototype Powerups where useful, but expand the content pool with the following concepts:

| Powerup | Temporary effect | Design purpose |
|---|---|---|
| **Full Sail** | Huge movement-speed + turn-response boost | Repositioning spike |
| **Powder Rush** | Reset cooldown-based Weapons; rapid reload briefly | Damage spike |
| **All Guns Loaded** | Every eligible equipped Weapon immediately performs one legal emission | Spectacle / burst |
| **Dead Calm** | Enemy/projectile movement dramatically slows for a short window; bosses get reduced effect | Breathing room / precision |
| **Sea Shanty** | Temporary regeneration + knockback resistance | Sustain peak |
| **Magnetic Tide** | Pull current-stage XP/Gold/salvage toward ship | Route reward / cleanup |
| **Treasure Fever** | Short generated Gold/loot multiplier | Greed spike |
| **Ghost Fog** | Brief phase/invulnerability; cannot be chained indefinitely | Escape / clutch moment |
| **Kraken's Favour** | Huge tentacles strike around the player | Fantasy burst |
| **Royal Powder** | Explosions gain dramatically larger area for a few seconds | Explosive build peak |
| **Rum Frenzy** | Attack speed + movement spike; slightly looser turning/handling | Controlled chaos |
| **Black Flag** | Ordinary enemies briefly flee; elites become enraged/stronger | Reposition + risk |

Existing **Repair Supplies**, **Battle Signal**, **Tailwind**, and **Salvage Chime** may remain in the pool as simpler common Powerups.

## 12.1 Powerup rules

- Temporary effects should generally last ~4–12 seconds.
- Duplicate timed Powerups refresh duration unless a definition explicitly stacks magnitude.
- Bosses/elite CC uses reduced effectiveness.
- No Powerup may generate unlimited invulnerability loops.
- `All Guns Loaded` and `Powder Rush` must obey projectile/event caps and cannot duplicate persistent orbit actors.
- `Magnetic Tide` attracts existing pickups; it never creates reward value.
- Powerup rarity/drop rules must be data-driven.

---

# 13. Moment-to-moment objective pressure

Do not create an optimal strategy where the player can simply find open water and orbit clockwise for ten minutes.

Use the existing finite chests / POIs / Beacon system to create route changes.

P0:

- Keep 12 stage chests and 4 POIs.
- Ensure the opening section does not cluster all rewards on one natural circular route.
- Place at least one meaningful POI off the shortest path toward the expected Beacon sector.
- Preserve minute-7 sector hint and minute-9 exact Beacon marker.

P1 optional events:

- Treasure Raider.
- short-lived floating treasure cache.
- dangerous Powder Cache variant.
- rival Captain challenge.

All optional events must present visible reward vs risk. Do not secretly punish the player for ignoring them.

---

## 13.1 Micro-objective / risk-event backlog

Use these to break the optimal clockwise orbit and make map routing economically meaningful:

- **Treasure Ship** appears and tries to escape.
- **Merchant Convoy** offers a rare Item/reward but increases pursuit/Notoriety.
- **Navy Beacon** can be activated for extra treasure and higher Wanted/Notoriety pressure.
- **Cursed Chest** spawns an elite fleet; clear it for guaranteed high-rarity loot.
- **Bounty Flag** temporarily increases enemy density for boosted XP/Gold/chest chance.
- **Kraken Altar** empowers the next boss/sea-monster encounter for an evolution/capstone reward.
- **Royal Treasury** grants a large Gold cache but causes elite pursuit for the rest of the stage.
- **Whirlpool Treasure** appears inside a dangerous pull field.
- **Sea Shrine** requires staying near it while enemies attack.
- **Golden Whale** appears and flees with a valuable reward.
- **Storm Pocket** temporarily closes around a map region containing treasure.
- **Rival Pirate Captain** challenges the player to a short duel/miniboss.
- **Powder Depot** can be detonated to kill enemies but may destroy nearby loot.
- **Ghost Wreck** has a short interaction window before it sinks.

The player should repeatedly ask: **“Is it worth breaking my safe route for that?”**

Do not make these mandatory for base stage completion.

---

# 14. Readable-chaos rules — P0

Six Weapons can make the game worse if VFX hides threats.

Rendering / VFX priority must be:

1. boss lethal telegraphs,
2. enemy attack telegraphs,
3. player ship / collision readability,
4. elites / high-priority enemies,
5. ordinary enemies,
6. player Weapon projectiles,
7. repeated hit flashes,
8. damage numbers / decorative particles.

## Requirements

- Enemy telegraphs render above player decorative effects.
- Telegraph colors/shapes must remain readable with reduced-color perception; do not rely only on hue.
- Repeated player projectiles may reduce opacity at high density.
- Merge / simplify overlapping explosion VFX without changing simulation hitboxes.
- Damage numbers should aggregate when the same source hits the same enemy repeatedly in a short window.
- Audio must prioritize boss/enemy warnings over six looping Weapon sounds.
- Keep reduced flashes, reduced shake, and reduced decorative particles options.
- Add a player-Weapon VFX intensity slider if feasible.

## Performance budgets

Keep current simulation safety caps initially:

```text
active projectiles <= 600
active area effects <= 80
secondary events <= 2,000 / sec
propagation generations <= 3
```

If visuals exceed device budgets, cull presentation only. Never cull score-affecting enemies or authoritative hits.

---

# 15. Post-run Weapon contribution report — P0

Six Weapons need to feel meaningful even when they serve different functions.

Record per Weapon:

```text
totalDamage
kills
bossDamage
eliteDamage
hits
critHits
controlApplications
controlSeconds
procEventsGenerated
maxConcurrentTargetsHit
uptime / emissions
```

Do not judge balance only by total DPS.

A control Weapon can be successful with low damage if it contributes meaningful slows / grouping / safety.

Post-run prototype output should be capable of showing examples like:

```text
Broadside Battery
31% total damage

Powder Kegs
18% damage
220 chain-reaction kills

Chainshot
8% damage
1,120 slows applied

Harpoon Gun
10% total damage
44% of elite/boss damage
```

---

# 16. Telemetry required for playtests — P0

Add a local structured run telemetry export if no analytics backend is ready.

Minimum fields:

## Arsenal progression

```text
weaponCountAtTime
weaponCountAtLevel
timeToWeapon2
timeToWeapon3
timeToWeapon4
timeToWeapon5
timeToWeapon6
newWeaponOffersShown
newWeaponOffersAccepted
rerollsUsed
banishesUsed
```

## Combat activity

```text
enemiesAliveSampled
spawnRateSampled
killsPerMinute
playerDpsSampled
eliteTimeToKill
bossTimeToKill
damageTakenPerMinute
deathCause
nearMissCount
```

## Movement / boredom proxies

```text
distanceTravelled
headingChangeDegreesPerMinute
majorDirectionChangesPerMinute
secondsMovingWithoutMeaningfulHeadingChange
longestSameTurnDirectionWindow
secondsWithNoThreatWithinCombatRange
```

The most useful custom metric is **Continuous Orbit Time**:

> Approximate how long the player maintains the same broad turning direction around the local encounter without a meaningful route reversal or target-driven change.

It does not need perfect geometric detection for the prototype. A stable heuristic is enough for relative A/B comparison.

## Build contribution

```text
damageByWeapon
killsByWeapon
bossDamageByWeapon
controlByWeapon
itemProcCounts
captainPassiveContribution
captainActiveUses (P1)
```

## Readability / performance

```text
fpsP50
fpsP90Low
fpsP99Low
peakProjectileCount
peakEnemyCount
peakAreaEffectCount
damageTakenWhileProjectileCountHigh
damageTakenWhileAreaEffectCountHigh
```

## Playtest survey fields

At the end of a run ask testers to rate 1–5:

- first 2 minutes excitement,
- mid-run excitement,
- late-run excitement,
- build satisfaction,
- feeling of control,
- visual readability,
- desire to immediately start another run.

---

# 17. Difficulty balance targets

These are prototype heuristics, not final numbers.

## Ordinary fodder TTK

With an average build:

```text
0–2 min: most fodder dies in < 1 sec of focused coverage
4–6 min: fodder dies quickly but survives incidental weak hits
8–10 min: fodder remains killable fast enough to create chain reactions / XP flow
```

Do not let ordinary fodder become multi-second HP sponges simply because six Weapons exist.

## Specialist TTK

```text
mid-run specialist: ~1.5–4 sec depending on build matchup
late elite: ~5–12 sec
```

## Boss target

A reasonable build should kill the stage objective boss without requiring a single dominant Weapon.

Track median boss TTK by build strength rather than hard-coding a target around one exact DPS number.

## Player survivability

Goal:

- early hits are recoverable mistakes,
- late specialist overlaps can punish bad routing,
- boss attacks are dangerous but readable,
- no unavoidable death from stacked telegraphs,
- movement remains primary defense.

---

# 18. Captain / build identity validation

A Captain is successful only if it changes what choices feel valuable.

For each Captain, compare Weapon / Item pick rates.

Expected directional differences:

## Rook

- broad generalist spread.
- no single mandatory family.

## Hank

- more willingness to take close-range / orbit / tank Items.
- lower movement and higher survivability should be visible in telemetry.

## Richie

- higher unspent Gold.
- more tension between opening chests and retaining damage.

## Sparky

- Kegs / Explosive effects over-index.
- more high-speed routing.

## Ember

- Burn / Fire effects over-index.
- healing thresholds should be visible but not make death irrelevant.

## Roxie

- Lightning / movement / near-miss effects over-index.
- higher movement and near-miss frequency.

If Captain choice barely changes successful builds, strengthen behavior-specific mechanics later rather than only increasing passive percentages.

---

# 19. Future Captain archetypes — P2 content backlog

Do not implement these before the current six are validated. These are future archetype slots, not canon names.

1. **Rammer / Brawler** — damage from speed + collision; wants close passes.
2. **Artillery Captain** — fewer/faster long-range bombardment attacks; fragile at close range.
3. **Summoner Captain** — crew boats / ghost ships inherit bounded Item effects.
4. **Crit Hunter** — elite weak-point / first-hit / execution play.
5. **Cooldown Specialist** — active skill and cooldown manipulation; lower base damage.
6. **Challenge Duelist** — only 2–3 Weapon slots but dramatically stronger upgrade effectiveness.

The last archetype is how the old two-Weapon experience can later become a deliberate high-skill character rather than the normal game.

---

# 20. P0 implementation order

Implement in this exact order unless repository dependencies require a minor reordering.

## Step 1 — Repository mapping

Produce a short engineering note listing the existing files/classes responsible for:

- arsenal capacity,
- offer generation,
- Weapon definitions,
- targeting,
- spawn director,
- Captain config,
- serialization,
- VFX limits,
- run stats.

Do not change behavior yet.

## Step 2 — Experiment profile

Add profiles A/B/C/D and ensure the current game can still run under A.

Acceptance:

- A behaves like current build.
- profile selection is logged in run telemetry.

## Step 3 — Six Weapon slots

Implement baseline capacity 6 under B/C/D.

Acceptance:

- six unique Weapon instances can be equipped simultaneously.
- no UI overlap.
- save/resume preserves all six.
- stage transition preserves all six.
- proc attribution remains correct.

## Step 4 — Opening Weapon draft + offer weighting

Acceptance:

- Captain signature starts normally.
- opening choice cannot duplicate equipped Weapon as a new entry.
- no more than six Weapons.
- once six are equipped, new-Weapon cards disappear.
- pity guarantee produces an option rather than auto-equipping.

## Step 5 — Preserve baseline six + enable first experimental 12-Weapon pool

Acceptance:

- the six existing Weapons remain functional with unchanged stable IDs.
- the first six new test Weapons can appear when eligible: Long Nine, Swivel Guns, Grapeshot, Harpoon Gun, Mortar, Boarding Party.
- unfinished art may use clear prototype placeholders.
- every Weapon has a valid targeting mode, family tags, and proc coefficient.
- the larger Weapon backlog is defined in data/config but does not need production assets yet.

## Step 6 — Directional geometry

Acceptance:

- Deck Cannon / Long Nine / Harpoon Gun respect forward arcs.
- Broadside Battery snapshots heading.
- Powder Kegs deploy aft.
- Flamethrower follows facing.
- Lightning Mast / Swivel Guns remain reliable omni options.
- Mortar uses predicted cluster/destination targeting.
- Boarding Party uses autonomous summon targeting.
- no Weapon secretly rotates the ship.

## Step 7 — Director V2

Acceptance:

- higher density is visible by mid-run.
- enemy cap remains respected.
- no spawn debt.
- 3/4/7/8-minute pressure beats still trigger.
- mixed packs begin later in the stage.
- no formation creates an unavoidable fully sealed hazard.

## Step 8 — Readability pass

Acceptance:

- enemy telegraphs remain visible under six-Weapon builds.
- reduced-particle settings preserve simulation.
- strongest six-Weapon build stays within hard gameplay event caps.

## Step 9 — Telemetry and post-run contribution

Acceptance:

- A/B/C/D runs can be compared from exported data.
- per-Weapon contribution is available.
- time-to-fill-six is available.
- movement / orbit proxy is available.
- performance counters are available.

---

# 21. P1 implementation order

Only start after P0 A/B/C/D playtest results are reviewed.

1. Six specialist enemies: Needle Sloop, Ram Raider, Bomb Ketch, Net Cutter, Repair Tender, Powder Barge.
2. Captain Active Skill framework.
3. Six prototype Captain Skills.
4. First six Weapon Evolutions plus shared evolution data framework.
5. Expanded Powerup pool.
6. First 12 interaction-driven Items/Modules.
7. Shield Frigate + Treasure Raider.
8. Optional route-diversion / Notoriety risk events.
9. Additional Weapons from the experimental content library based on playtest gaps.

Each P1 feature must be independently feature-flagged so its value can be measured.

---

# 22. Automated / deterministic tests

Add or update tests for:

## Arsenal

- six-slot equip cap.
- seventh new Weapon rejected / not offered.
- six slots serialize and restore.
- Captain starting Weapon consumes one slot.
- opening draft fills second slot.

## Offers

- category weights change by current Weapon count.
- no new Weapon offers at six.
- pity guarantee produces at least one eligible new Weapon card at threshold.
- banished content remains banished.
- capped upgrades remain filtered as before.

## Targeting

- ForwardArc rejects targets outside arc.
- AftArc rejects front targets.
- Broadside uses one heading snapshot per volley.
- targeting tie breaks remain deterministic.
- geometry never depends on render FPS.

## Spawning

- spawn curve equals config samples at minute 0/2/4/6/8/10.
- cap prevents additional spawns.
- no stored spawn debt after cap clears.
- pressure pulse multiplies rate correctly.
- pack composition remains deterministic for saved RNG state.

## Combat safety

- all existing proc-generation caps still hold with six Weapons.
- attribution remains correct through evolution effects.
- secondary-event ancestors cannot recurse infinitely.

## Save / resume

- experiment profile ID/version saved.
- six-Weapon loadout resumes exactly.
- Captain active cooldown resumes in P1.
- evolution state resumes in P1.

---

# 23. Manual playtest matrix

Run each profile with at least the following Captains:

- Rook — baseline.
- Hank — survivability / close-range test.
- Sparky — high-VFX explosive test.
- Roxie — movement / near-miss test.

Minimum comparison:

```text
A_CURRENT
B_SIX_ONLY
C_SIX_DIRECTIONAL
D_FULL_COMBAT_V2
```

For each run record:

- excitement minute 2 / 5 / 9,
- Weapon count at those times,
- continuous orbit time,
- damage distribution,
- death cause,
- boss TTK,
- FPS lows,
- whether the player could explain what killed them,
- whether the player wanted to immediately restart.

## Interpretation

### If B >> A but C ≈ B

The primary problem was audiovisual/build density. Keep six slots, but do not overcomplicate directional targeting.

### If C >> B

Heading geometry is materially solving top-down passivity. Expand directional Weapon design.

### If D >> C

Enemy composition / pressure was a major missing ingredient. Invest in specialist enemies and formation design.

### If B feels exciting for 5 minutes but boring later

Slot count solved the opening but not long-term build transformation. Prioritize Evolutions and late-run composition.

### If D feels exhausting

Reduce simultaneous specialist roles and VFX before reducing build variety.

---

# 24. Success criteria for this redesign

Do not declare the redesign successful merely because the screen is busier.

Target outcomes:

1. Six-slot profiles clearly outperform the two-slot baseline on excitement and replay intent.
2. Players reach 3–4 Weapons early enough that the opening does not drag.
3. By minute 5 the player is making heading/route decisions, not only avoiding contact.
4. Most successful builds have at least 3 meaningful Weapon contributions.
5. A low-DPS control Weapon can still demonstrate value in telemetry.
6. Continuous Orbit Time drops in C/D compared with B.
7. Enemy telegraphs remain understandable during the strongest builds.
8. Ordinary enemies still die satisfyingly fast; difficulty comes from composition/density rather than universal sponge HP.
9. Different Captains materially change build choices or movement behavior.
10. The game remains performant under six-Weapon proc-heavy builds.
11. Boss victory + post-boss overtime behavior remains unchanged.
12. No changes in this combat pass break Territory/economy/persistence invariants.

---

# 25. Non-goals for this implementation pass

Do not use this task to:

- add manual twin-stick aiming,
- add realistic sailing inertia,
- redesign Territory generation,
- redesign raids,
- redesign permanent Item ownership,
- change Gold/Rum economic rules,
- change the 10-minute stage timer,
- add co-op,
- add on-foot combat,
- add large permanent Captain stat trees,
- add mandatory meta-stat grinding,
- create dozens of new production assets before mechanics validation.

---

# 26. Final implementation principle

The target is not:

> “Rogue Pirates has six Weapons and more particles.”

The target is:

> **“Rogue Pirates turns steering a top-down pirate ship into the combat skill: line up broadsides, point forward Weapons, kite enemies into the wake, chase priority targets, break safe routes for greed, and survive increasingly complex fleet pressure while a ridiculous six-Weapon build comes online.”**

Build the systems so the game can become visually absurd **without becoming strategically passive or visually unreadable**.

