**PROJECT HIGH SEAS**

**Pirate Bullet-Heaven Game Design Document + Build Specification**

Version 0.2 \| 21 August 2026

| **Binding product rule —** The player avatar is always the ship. There is no on-foot pirate gameplay. Ship movement is intentionally simple: responsive WASD on WebGL and a single virtual joystick on Android. The game’s depth comes from combat, buildcraft, items, map decisions, economy, progression and escalating enemy density—not realistic sailing. |
|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

Target platforms: WebGL + Android \| Engine assumption: Unity 6 LTS + URP \| Orientation: Landscape

*Working title only. All names, tuning values and content counts in the proposed game are editable defaults.*

# Document Map

1\. Executive Summary & Non-Negotiables

2\. Megabonk Research Baseline

3\. Design Principles to Preserve (Without Copying Expression)

4\. Original Game Vision: Project High Seas

5\. Core Loop, Run Structure & Stage Director

6\. Controls, Camera & Ship Movement

7\. Player Ships / Pirate Archetypes

8\. Combat Model & Statistics

9\. Weapons

10\. Logbooks (Run-Scoped Stat Builds)

11\. Relics / Items & Synergies

12\. Temporary Powerups & Status Effects

13\. Enemies, Elites, Minibosses & Bosses

14\. World, Biomes, Procedural Generation & POIs

15\. Economy, Shops, Loot & Risk/Reward

16\. Progression, Unlocks, Quests & Challenges

17\. UI/UX, Onboarding & Accessibility

18\. Art, VFX & Audio Direction

19\. Technical Architecture for WebGL + Android

20\. Data Schemas, State Machines & Pseudocode

21\. Performance Budgets & Quality Scaling

22\. Analytics, Balance Telemetry & Live Tuning

23\. MVP, Vertical Slice & 8-Week Roadmap

24\. QA Plan & Acceptance Criteria

25\. AI Agent Implementation Contract

Appendix A. Default Balance Tables

Appendix B. Research Sources & Confidence Notes

| **How to use this document —** Sections 1–3 explain what is factual research versus inference. Sections 4–18 define the game. Sections 19–25 are implementation instructions. An AI coding agent should treat every statement labeled MUST / MUST NOT as a hard requirement and every number labeled DEFAULT as tunable data. |
|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

# 1. Executive Summary & Non-Negotiables

Project High Seas is a 3D pirate-themed bullet-heaven / action-roguelite inspired by the systemic appeal of Megabonk: roam a compact procedural map, auto-destroy growing hordes, collect XP, assemble a ridiculous build from random upgrades, exploit synergistic items, interact with risk/reward landmarks, defeat bosses, and unlock more content for future runs. The proposed game is mechanically original and must not reproduce Megabonk’s characters, names, art, maps, writing, or exact content.

| **Priority** | **Requirement**                                                                                                                                                                                  |
|--------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| P0           | The player is the ship at all times. The “pirate” is represented by the ship/captain archetype, flag, crew fantasy and passive; no disembarking or on-foot controller exists.                    |
| P0           | Movement is basic and forgiving: WASD / arrows on WebGL, one virtual joystick on Android. No wind, tacking, rudder mastery, wave physics, drifting, stamina, boarding movement or momentum-tech. |
| P0           | Auto-combat is the default. The player’s meaningful decisions are positioning, route choice, upgrade selection, POI interaction, spending, greed and build direction.                            |
| P0           | WebGL and Android are first-class targets from day one. Performance architecture cannot be “optimized later.”                                                                                    |
| P1           | Runs must create an “I broke the game” arc: modest start → visible synergy → screen-filling overpowered spectacle → high-pressure boss/overtime.                                                 |
| P1           | Exploration competes with survival. Valuable POIs sit off the safest route and the stage timer continuously raises pressure.                                                                     |
| P1           | Content unlocks should encourage unusual play instead of only grind: use specific weapons, survive under restrictions, activate map systems, defeat bosses in special ways.                      |
| P2           | No multiplayer/network dependency in MVP. Runs are local and deterministic enough to reproduce bugs from a seed.                                                                                 |

## 1.1 Design Pillars

| **Pillar**                     | **Player promise**                                                                    | **Design consequence**                                                                     |
|--------------------------------|---------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------|
| Build a Broken Pirate Ship     | Turn a humble vessel into an absurd floating weapons platform.                        | Stack multiplicative and proc-based synergies; show dramatic visual escalation.            |
| Survive Escalating Naval Chaos | Enemy counts, elite pressure, hazards and projectiles intensify throughout the stage. | Use a threat-budget director and overtime mode instead of scripted waves only.             |
| Explore, Plunder & Get Greedy  | Leave the safest route to hunt treasure, shrines, merchants and risky rewards.        | Procedural POIs, escalating chest prices, challenge events, optional difficulty increases. |
| Create Wild Synergies          | Weapons, Logbooks and Relics should interact in memorable combinations.               | Tag-driven proc system, status effects, quantity/size/duration scaling and economy builds. |
| Unlock More Ways to Break Runs | Meta progression widens the sandbox without making new players permanently weak.      | Unlock content, slots and decision tools; minimize raw permanent stat power.               |

## 1.2 Product Fantasy

“I am a pirate ship surrounded by an impossible fleet. Every minute I find more treasure and bolt more nonsense onto my deck until cannons, ghosts, sharks, lightning and cursed relics are firing everywhere. I keep choosing greed over safety, then somehow survive the boss because my build became ridiculous.”

# 2. Megabonk Research Baseline

| **Source status —** Official Steam is authoritative for the broad pitch and marketing counts. Community wikis/guides are used for system-level detail because the developer does not publish a complete design specification. Patch-dependent counts conflict slightly across sources; those conflicts are noted rather than resolved by guesswork. |
|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

## 2.1 Verified / High-Confidence Findings

| **System**          | **Research summary**                                                                                                                                                                                                                                                                                             |
|---------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Genre & loop        | Steam describes Megabonk as a roguelike survival game with hordes/bosses, randomly generated maps, loot, XP leveling, weapon upgrades and randomized upgrade rarity. \[S1\]                                                                                                                                      |
| Meta breadth        | Steam advertises 240 in-game quests, 20 characters and 70+ items. \[S1\] A current unofficial guide reviewed Aug 2026 reports 21 playable characters in v1.0.69, indicating live-patch drift. \[S12\]                                                                                                            |
| Character model     | Community references describe each character as starting with a weapon and a unique passive. \[S12\]                                                                                                                                                                                                             |
| Weapons             | Community wiki currently lists 29 weapons spanning slashes, trails, lightning, explosive projectiles, orbitals, homing, mines, auras, crowd-control and manual-aim exceptions. Weapon level-ups can improve weapon-specific stats such as damage, quantity, bounces, speed, knockback, duration and size. \[S2\] |
| Tomes               | Tomes are limited-slot, run-scoped passive stat scalers. They can be repeatedly leveled and cover damage, attack speed, movement, crit, size, quantity, duration, XP, gold, pickup, armor, regen, etc. \[S3\]                                                                                                    |
| Items               | Community wiki lists roughly 77–78 items depending on patch/source; items are obtained from chests/vendors and create many conditional/proc synergies. Many must be unlocked by a quest and then purchased with Silver to enter the loot pool. \[S4\]                                                            |
| Upgrade choice      | Level-ups present randomized offers with rarity. Screenshots and guides show Skip / Refresh / Banish tools becoming available through meta progression. \[S1\]\[S13\]                                                                                                                                            |
| World structure     | Community wiki documents themed maps, stage/tier progression, bosses and an overtime “Final Swarm” if the boss is not handled before the timer. Forest/Desert use three tiers; Graveyard differs with a longer timer/two-stage structure. \[S5\]                                                                 |
| Exploration systems | POIs include charge shrines that require remaining in an area for a timed stat reward, challenge encounters, greed/risk interactions, XP vacuum shrines, item-granting landmarks, escalating-price chests, merchants, duplicators and boss/teleport interactions. \[S6\]\[S10\]                                  |
| Currencies          | Gold is used during a run for chest/vendor purchases; Silver is permanent currency used for unlocks/upgrades. \[S4\]\[S10\]                                                                                                                                                                                      |
| Chest pressure      | Normal chests begin at a low price and increase in price after purchases, turning map exploration and gold gain into a scaling resource decision. \[S9\]                                                                                                                                                         |
| Quest design        | Unlock conditions include cumulative kills, weapon-specific kills, hitting stat levels, environmental feats, boss restrictions and unusual challenge runs. \[S7\]                                                                                                                                                |
| Challenge modifiers | Optional challenge rules alter a run and increase Silver gain, adding replay targets beyond simply clearing stages. \[S8\]                                                                                                                                                                                       |
| Temporary powerups  | Guides document short-lived heal, speed, attack-speed/rage, gold, protection, magnet, nuke and time-freeze style pickups. \[S13\]                                                                                                                                                                                |

## 2.2 What Makes the Structure Work (Design Inference)

- Parallel progression channels make almost every action valuable: kills → XP, exploration → items/stats, gold → more items, bosses → stage progression, objectives → permanent unlocks.

- Random offers create uncertainty, but reroll/skip/banish and slot unlocks gradually give the player more control over randomness.

- Weapon-specific upgrade stats create identity. Quantity is transformative on a projectile weapon, while size/duration may matter more on area/trail effects.

- Map objects turn an otherwise passive “circle the horde” loop into route planning and risk decisions under time pressure.

- Greed is often self-reinforcing: more difficulty can create more enemies → more XP/gold → faster scaling, but only if the build can keep up.

- Meta quests act as a discovery system. Players learn mechanics because unlock requirements deliberately point them toward underused weapons, items, maps or constraints.

- The fun ceiling is intentionally permissive. The system tolerates extremely strong combinations because “becoming broken” is part of the reward.

## 2.3 What We Are Deliberately Not Copying

- No Megabonk character names, weapon names, item names, jokes, quotes, enemy designs, map layouts or quest text.

- No exact reproduction of Megabonk’s UI layout, rarity presentation, chest-price curve or stat formulas.

- No assumption that Megabonk’s counts or current balance are “correct” for mobile/browser session length.

- No high-skill movement focus, jumping system or manual-camera requirement. Project High Seas uses a simplified control model by design.

# 3. Design Principles to Preserve (Without Copying Expression)

| **Principle**                         | **Rule for Project High Seas**                                                                                                                                    |
|---------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Low input, high decision density      | Attacks fire automatically; the player continuously chooses where to move, what to loot, what to buy, which risk to accept and which upgrade direction to pursue. |
| Three-layer buildcraft                | Weapons define output patterns; Logbooks scale universal stats; Relics add rules/procs/conditional interactions. All three layers must matter.                    |
| Randomness with agency                | Offer three choices. Add reroll, skip and banish as progression tools. Avoid dead levels by weighting offers toward equipped systems once slots are full.         |
| Exploration under pressure            | The stage timer keeps rising pressure while POIs tempt the player away from safe loops.                                                                           |
| Multiple economies                    | XP powers the current run; Gold buys run power; Doubloons unlock future content; Bounty is a voluntary difficulty/reward meter.                                   |
| Visible exponentiality                | Upgrade math should feel multiplicative even when internally controlled. Quantity × attack speed × proc chance × area creates the fantasy.                        |
| Quest-led discovery                   | Unlocks teach advanced systems and encourage unusual runs, instead of only asking for raw hours played.                                                           |
| Spectacle with performance discipline | Use pooled/instanced visuals and simplified swarm logic so the screen can look crowded without simulating every object expensively.                               |

# 4. Original Game Vision: Project High Seas

## 4.1 Genre / Camera / Audience

| **Field**                 | **Specification**                                                                                                                         |
|---------------------------|-------------------------------------------------------------------------------------------------------------------------------------------|
| Genre                     | 3D bullet-heaven action roguelite with procedural exploration and run/meta progression.                                                   |
| Perspective               | Elevated 3/4 third-person follow camera. Camera yaw is fixed in MVP; no manual rotation required.                                         |
| Platforms                 | WebGL desktop browsers + Android phones/tablets, landscape.                                                                               |
| Session                   | MVP: one 10-minute stage. Full standard voyage: 3 stages totaling ~24–30 minutes, with optional overtime.                                 |
| Input complexity          | Move + pause only during combat. All primary weapons auto-fire/auto-target. No aim stick in MVP.                                          |
| Target audience           | Players who enjoy Vampire Survivors / Brotato / Megabonk-style scaling, build discovery, short roguelite runs and absurd power fantasies. |
| Business model assumption | Not specified by this GDD. Core progression is designed to function without pay-to-win requirements.                                      |

## 4.2 Theme Rules

- The playable “character” is always represented by a ship. A named pirate/captain archetype may appear as portrait, flag, voice bark and passive theme, but never becomes an independently controlled entity.

- Ships are deliberately stylized, compact and readable from the elevated camera: chunky silhouettes, oversized cannons, exaggerated flags, bright impact effects.

- The ocean is a gameplay floor, not a sailing simulation. Islands/reefs create route geometry; waves are visual only.

- Tone: playful pirate fantasy with spooky/cursed elements. Avoid realistic naval warfare complexity.

# 5. Core Loop, Run Structure & Stage Director

## 5.1 Moment-to-Moment Loop

> **1.** Move through open water while weapons auto-fire at nearby enemies.
>
> **2.** Kill enemies → collect Infamy shards (XP) + Gold drops.
>
> **3.** Level up → choose 1 of 3 randomized Weapon or Logbook offers.
>
> **4.** Explore map → spend Gold / take risks at POIs → acquire Relics or permanent-for-run stat bonuses.
>
> **5.** Build synergy increases kill speed → denser enemies become profitable rather than merely dangerous.
>
> **6.** Decide when to trigger the Boss Beacon versus farming/exploring longer.
>
> **7.** Defeat boss → collect reward → continue to next stage or finish voyage.
>
> **8.** Run ends → bank Doubloons, complete quests, unlock new ships/weapons/relics/logbooks/challenges.

## 5.2 Standard Voyage

| **Phase**           | **Default timing**          | **Director behavior**                                                     | **Player goal**                                                 |
|---------------------|-----------------------------|---------------------------------------------------------------------------|-----------------------------------------------------------------|
| Opening             | 0:00–2:00                   | Low-density melee swarms; high basic XP; first POIs are reachable.        | Secure second weapon / first Logbook and scout POIs.            |
| Build formation     | 2:00–5:00                   | Introduce ranged/charger enemies; first elite/miniboss.                   | Commit to 1–2 build synergies; spend first Gold.                |
| Acceleration        | 5:00–7:30                   | Higher spawn budget; mixed formations; more elite chance.                 | Exploit synergy, clear Trial POIs, finish route.                |
| Boss window         | 7:30–8:00 (or 10:00 in MVP) | Boss Beacon warning and stronger waves.                                   | Summon and defeat boss; optionally greed for more farm.         |
| Dread Tide overtime | After timer expires         | Threat multiplier rises every 60s; more elites; no new normal POIs spawn. | Survive for bonus payout or kill boss before the run collapses. |

| **Default full-run pacing —** For launch, use three 8-minute stages (plus boss time). For the MVP, use a single 10-minute stage so content and performance can be validated before stage transitions are built. |
|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

## 5.3 Stage Director Formula

```text
minute = elapsedSeconds / 60
baseThreatPerSecond = StageBaseThreat
timeCurve = 1 + 0.11*minute + 0.022*minute*minute
tierMult = [1.00, 1.35, 1.80][tier-1]
stageMult = [1.00, 1.22, 1.48][stageIndex]
bountyMult = 1 + 0.06*BountyLevel
overtimeMult = elapsed <= stageLimit ? 1 : 1.50 * pow(1.20, overtimeMinutes)
ThreatPerSecond = baseThreatPerSecond * timeCurve * tierMult * stageMult * bountyMult * overtimeMult
```

Threat is accumulated into a budget. Each enemy archetype has a threat cost. The director spends budget on weighted compositions, enforcing per-archetype caps so one cheap unit does not consume the entire simulation. This is easier to tune and more performance-safe than hard-coded wave counts.

# 6. Controls, Camera & Ship Movement

| **Movement priority —** Movement exists to support combat positioning and exploration. It is not intended to be the main mastery layer. |
|-----------------------------------------------------------------------------------------------------------------------------------------|

## 6.1 WebGL Controls

| **Input**        | **Action**                                                 |
|------------------|------------------------------------------------------------|
| W / Up           | Move toward screen-up / camera-forward on the water plane. |
| S / Down         | Move screen-down / camera-back.                            |
| A / Left         | Move screen-left.                                          |
| D / Right        | Move screen-right.                                         |
| Esc / P          | Pause.                                                     |
| M (optional)     | Toggle expanded map overlay.                               |
| Mouse / touch UI | Select level-up choices, shops and menus.                  |

## 6.2 Android Controls

- Single floating virtual joystick on lower-left.

- Pause/settings button top-right.

- All weapon fire is automatic. No second aiming stick in MVP.

- Level-up and shop cards are large tap targets; combat pauses while a choice modal is open.

## 6.3 Kinematic Movement Spec

```text
input = Vector2(moveX, moveY)
if input.sqrMagnitude > 1: input.Normalize()
worldDir = cameraPlanarRight * input.x + cameraPlanarForward * input.y
targetVelocity = worldDir * MoveSpeed
velocity = Vector3.SmoothDamp(velocity, targetVelocity, ref accelRef, 0.10s)
position += velocity * deltaTime
visualYaw = LerpAngle(visualYaw, atan2(worldDir.x, worldDir.z), 1-exp(-12*dt))
```

- No Rigidbody-driven propulsion. Player movement is a custom kinematic controller on the XZ plane.

- Diagonal input is normalized. Target default MoveSpeed = 6.0 world units/s; ship variants should stay roughly 5.4–6.8 before upgrades.

- Acceleration/deceleration smoothing target: 0.08–0.15 s. Releasing the stick should stop quickly.

- Hull rotation is visual. A ship must not need to “turn around” before it can move in the new direction.

- Optional roll/lean \<= 5 degrees is cosmetic only. Waves/bobbing are cosmetic child-transform animation.

- When colliding with static islands, project movement along the obstacle tangent so the ship slides rather than catches.

## 6.4 Camera

| **Property** | **Default**                                                                                 |
|--------------|---------------------------------------------------------------------------------------------|
| Projection   | Perspective; FOV 48–55 degrees.                                                             |
| Pitch        | ~42 degrees downward.                                                                       |
| Yaw          | Fixed. Choose one art-approved angle; input remains screen-relative.                        |
| Follow       | Critically damped follow, 0.12–0.18 s smoothing.                                            |
| Look-ahead   | 0.5–1.0 m in movement direction, capped.                                                    |
| Shake        | Small impulse-based shake for crits/boss attacks; accessibility toggle and strength slider. |
| Zoom         | Static in MVP; optional 5–10% zoom-out during extreme density if readability demands it.    |

# 7. Player Ships / Pirate Archetypes

Each selectable avatar is a Captain + Ship fantasy packaged as one unit. The captain appears in menus/portrait/voice, while the ship is the only controllable body. Base-stat differences are intentionally modest so run buildcraft matters more than character selection.

| **Ship**           | **Archetype**        | **Start weapon**  | **Passive**                                                                                          | **Base identity** | **Unlock**                      |
|--------------------|----------------------|-------------------|------------------------------------------------------------------------------------------------------|-------------------|---------------------------------|
| Cutlass Sloop      | All-round freebooter | Deck Cannons      | Sea Legs: +1.5% global damage every 5 levels.                                                        | 100 HP, 6.2 speed | Starter                         |
| Ironjaw Galleon    | Armored gunner       | Broadside Battery | Iron Belly: +1 Armor every level; -7% move speed.                                                    | 140 HP, 5.6 speed | Defeat first boss               |
| Red Fuse Ketch     | Explosives expert    | Powder Kegs       | Hot Deck: explosions gain +2% size per level.                                                        | 90 HP, 6.1 speed  | 500 explosive kills             |
| Needle Corsair     | Crit / precision     | Long Nine         | Deadeye: +0.35% crit chance per level.                                                               | 90 HP, 6.6 speed  | Crit 200 times in one run       |
| Gravewake Frigate  | Ghost / sustain      | Phantom Cannon    | No Rest: +0.25% lifesteal every level; healing above max becomes temporary shield at 25% efficiency. | 105 HP, 5.9 speed | Clear Gravewater stage          |
| Smuggler Cutter    | Economy / luck       | Swivel Guns       | False Bottom: +1% Gold gain per level and first chest per stage is 20% cheaper.                      | 85 HP, 6.8 speed  | Open 25 chests                  |
| Storm Junk         | Lightning / quantity | Lightning Mast    | Storm Crew: every 8 levels, lightning attacks gain +1 chain target.                                  | 100 HP, 6.0 speed | Shock 1,000 enemies             |
| Kraken Barge       | Area / control       | Kraken Lash       | Deep Pact: +1.5% Size every 3 levels; -10% projectile speed.                                         | 125 HP, 5.5 speed | Defeat Reef Maw                 |
| Shark Hunter       | Summon / pet         | Harpoon Gun       | Blood in the Water: summons deal +1% damage per player level against wounded targets.                | 100 HP, 6.3 speed | Kill 500 sharks/sea beasts      |
| Black Ledger Barge | Gold-scaling greed   | Coinshot          | Compound Interest: held Gold grants up to +40% damage (1% per 100 Gold).                             | 110 HP, 5.7 speed | Finish stage holding 1,000 Gold |
| Witchlight Skiff   | Curse / status       | Cursed Lantern    | Hexwake: status duration +1% per level.                                                              | 95 HP, 6.4 speed  | Apply 5,000 status stacks       |
| Royal Privateer    | Boss hunter          | Chainshot         | Letter of Marque: +20% damage to Elites/Bosses; normal enemies drop 5% less Gold.                    | 105 HP, 6.1 speed | Kill 10 bosses                  |

| **MVP roster —** Implement only Cutlass Sloop first. Add Ironjaw Galleon and Red Fuse Ketch for the vertical slice after all core systems are stable. |
|-------------------------------------------------------------------------------------------------------------------------------------------------------|

# 8. Combat Model & Statistics

## 8.1 Core Player Stats

| **Stat**         | **Default** | **Function**                                                      |
|------------------|-------------|-------------------------------------------------------------------|
| Hull             | 100         | Maximum health.                                                   |
| Hull Regen       | 0 / sec     | Continuous recovery.                                              |
| Shield           | 0           | Regenerating layer that recharges after 4 s without damage.       |
| Armor            | 0           | Reduces incoming damage with diminishing returns.                 |
| Evasion          | 0           | Chance to ignore a hit, capped.                                   |
| Damage           | 1.00x       | Global outgoing damage multiplier.                                |
| Attack Speed     | 1.00x       | Inverse cooldown multiplier for compatible weapons.               |
| Crit Chance      | 5%          | Chance for crit. Values above 100% may overcrit if enabled later. |
| Crit Damage      | 1.50x       | Critical multiplier.                                              |
| Quantity         | 0           | Extra projectiles/attack instances where supported.               |
| Size             | 1.00x       | Projectile/AoE/orbit radius scale.                                |
| Duration         | 1.00x       | Lifetime of trails, zones, summons and lingering projectiles.     |
| Projectile Speed | 1.00x       | Projectile travel speed.                                          |
| Knockback        | 1.00x       | Push force where supported.                                       |
| Pickup Radius    | 2.25m       | Infamy/Gold attraction radius.                                    |
| Luck             | 0%          | Biases rarity rolls and selected loot tables.                     |
| XP Gain          | 1.00x       | Infamy earned.                                                    |
| Gold Gain        | 1.00x       | Run Gold earned.                                                  |
| Doubloon Gain    | 1.00x       | Meta currency earned.                                             |
| Elite Damage     | 1.00x       | Separate multiplier vs elites/bosses.                             |

## 8.2 Damage Formula

```text
weaponRoll = BaseDamage * WeaponLevelDamage * (1 + GlobalDamageBonus) * WeaponDamageBonus
if IsCrit: weaponRoll *= CritDamage
if target.IsElite: weaponRoll *= EliteDamageMultiplier
statusAndTagMult = product(all applicable vulnerability/tag multipliers)
rawDamage = weaponRoll * statusAndTagMult
armorMultiplier = 100 / (100 + max(0, target.Armor))
finalDamage = max(1, rawDamage * armorMultiplier)
```

- Armor uses diminishing returns so it can scale without reaching immunity.

- Player evasion uses: evadeChance = min(0.75, Evasion / (Evasion + 100)).

- Crit chance defaults to a hard cap of 100% for MVP. Overcrit is an optional post-MVP system, not required.

- Proc-on-hit effects should use proc coefficients for extremely fast/multi-hit weapons to prevent runaway CPU and balance problems.

## 8.3 Contact Damage & Invulnerability

- Enemies deal contact damage using a per-enemy contact cooldown (default 0.8 s).

- Player gains 0.20 s post-hit invulnerability for readability, not long enough to enable easy immunity loops.

- Enemy projectiles and melee contact share the same damage receiver but use independent source cooldown IDs.

- Boss telegraphs must remain readable at Low quality and on a 6-inch phone.

# 9. Weapons

## 9.1 Weapon System Rules

- Every ship starts with one Weapon. Base capacity: 2; meta unlocks raise to 3 then 4.

- Weapons attack automatically. A WeaponDefinition specifies targeting mode, fire pattern, stat affinities, tags and level-up roll table.

- Each weapon supports only the upgrade stats that meaningfully change it. Do not offer Projectile Speed to an aura or Quantity to a purely continuous beam unless explicitly implemented.

- Weapons are allowed to become visually absurd, but simulation counts are capped. Quantity overflow can be represented by higher damage, wider volleys or batched projectiles when caps are reached.

- Max weapon level DEFAULT: 12. After level 12, the weapon leaves normal level-up offers unless a Relic extends its cap.

| **Weapon**        | **Pattern**              | **Targeting**            | **Tags**             | **Identity**                                                                                   | **Upgrade axes**                                    |
|-------------------|--------------------------|--------------------------|----------------------|------------------------------------------------------------------------------------------------|-----------------------------------------------------|
| Deck Cannons      | Auto projectile          | Nearest                  | Kinetic, Cannon      | Reliable cannonball; modest knockback.                                                         | Damage, cooldown, quantity, speed, knockback        |
| Broadside Battery | Fan volley               | Densest cluster          | Kinetic, Cannon, AoE | Alternates left/right visual gunports but auto-aims toward threat; no hull-facing requirement. | Damage, cooldown, pellets, spread, size             |
| Chainshot         | Piercing line            | Nearest elite \> nearest | Kinetic, Control     | Pierces; applies Breach and small slow.                                                        | Damage, cooldown, pierce, duration, status chance   |
| Harpoon Gun       | Pierce / tether          | Nearest                  | Kinetic, Control     | Harpoon pierces first targets and tugs light enemies inward.                                   | Damage, cooldown, quantity, pull, speed             |
| Powder Kegs       | Mine drop                | Player trail             | Explosive, Fire      | Drops timed/proximity kegs behind/around ship.                                                 | Damage, cooldown, quantity, size, duration          |
| Sea Mines         | Proximity mine           | Predicted clusters       | Explosive, Control   | Throws mines into nearby empty water; detonates on contact.                                    | Damage, quantity, size, arm speed, duration         |
| Mortar            | Lobbed AoE               | Densest cluster          | Explosive, AoE       | Slow shell lands on predicted cluster.                                                         | Damage, cooldown, shells, size, travel time         |
| Long Nine         | Precision projectile     | Lowest angle / elite     | Kinetic, Crit        | Very high damage, piercing, slower rate.                                                       | Damage, cooldown, crit, crit damage, pierce         |
| Swivel Guns       | Rapid projectile         | Nearest                  | Kinetic, Rapid       | Fast low-damage spray.                                                                         | Damage, cooldown, quantity, crit, speed             |
| Phantom Cannon    | Ghost projectile         | Nearest                  | Curse, Pierce        | Passes through targets and terrain props; applies Curse.                                       | Damage, cooldown, quantity, duration, curse chance  |
| Lightning Mast    | Chain lightning          | Nearest                  | Storm, Shock         | Instant strike jumps to nearby targets.                                                        | Damage, cooldown, chains, range, shock chance       |
| Kraken Lash       | Radial sweep             | Nearest side             | Beast, Melee, AoE    | Tentacle strikes arcs around ship.                                                             | Damage, cooldown, lashes, size, knockback           |
| Cursed Lantern    | Aura pulse               | Area around ship         | Curse, Aura          | Periodic curse damage in a radius; stacks vulnerability.                                       | Damage, cooldown, size, duration, curse strength    |
| Siren Song        | Persistent aura          | Area around ship         | Mystic, Control      | Low damage; slows non-boss enemies and increases damage they take from summons.                | Damage, size, slow, duration                        |
| Burning Oil       | Trail zone               | Player trail             | Fire, Trail          | Leaves burning slicks; no steering complexity.                                                 | Damage, tick rate, size, duration, burn stacks      |
| Razor Wake        | Trail hits               | Player trail             | Kinetic, Trail       | Damaging wake follows ship path.                                                               | Damage, cooldown, width, duration                   |
| Flying Cutlasses  | Orbit                    | Player-centered          | Kinetic, Orbit       | Cutlasses orbit hull and hit enemies.                                                          | Damage, orbit count, orbit speed, size, knockback   |
| Shark Pack        | Summon                   | Nearest wounded          | Beast, Summon        | Sharks periodically surface and bite targets.                                                  | Damage, summon count, attack rate, duration         |
| Bombardment Flare | Zone barrage             | Densest cluster          | Explosive, AoE       | Marks a zone; offscreen allied fire rains down.                                                | Damage, cooldown, shells, radius, duration          |
| Coinshot          | Gold scaling projectile  | Nearest                  | Greed, Kinetic       | Damage scales with current held Gold, capped.                                                  | Damage, cooldown, quantity, gold ratio, crit        |
| Maelstrom Bomb    | Pulling AoE              | Densest cluster          | Mystic, Control      | Creates short vortex that pulls light units and deals ticks.                                   | Damage, cooldown, size, duration, pull              |
| Powder Parrots    | Homing summon projectile | Random nearby            | Summon, Explosive    | Parrots drop tiny bombs; whimsical high-quantity weapon.                                       | Damage, cooldown, parrots, blast size, homing speed |

## 9.2 Targeting Modes

| **Mode**         | **Implementation**                                                                 |
|------------------|------------------------------------------------------------------------------------|
| Nearest          | Query spatial hash; select nearest valid target inside range.                      |
| Nearest Elite    | Prioritize elite/boss if within 1.25x base range, else nearest.                    |
| Densest Cluster  | Sample up to N nearby enemies; choose cell/centroid with highest weighted density. |
| Random Nearby    | Select random candidate from closest 8 using run RNG.                              |
| Player Trail     | No target query. Spawn effect relative to player position history.                 |
| Area Around Ship | Periodic radius query; effects may be visual-only between ticks.                   |

# 10. Logbooks (Run-Scoped Stat Builds)

Logbooks are Project High Seas’ passive scaling layer. They occupy limited slots and can be upgraded repeatedly from level-up offers. They are intentionally generic enough to combine with multiple weapons.

| **Logbook**           | **Build axis** | **Default level effect**                                             |
|-----------------------|----------------|----------------------------------------------------------------------|
| Gunnery Manual        | Damage         | +8% global Damage / level                                            |
| Powder Drill          | Attack Speed   | +7.5% attack speed / level                                           |
| Oversized Shot Chart  | Size           | +10% Size / level                                                    |
| Extra Hands Ledger    | Quantity       | +1 compatible projectile every 2 levels; odd levels grant +8% damage |
| Long-Fuse Primer      | Duration       | +15% Duration / level                                                |
| Deadeye Almanac       | Crit           | +6% Crit Chance / level                                              |
| Reinforced Hull Plans | Max Hull       | +20 Hull / level                                                     |
| Carpenter’s Notes     | Regen          | +0.35 HP/sec / level                                                 |
| Iron Plating Ledger   | Armor          | +8 Armor / level                                                     |
| Blood Rum Contract    | Lifesteal      | +4% lifesteal / level; uses proc coefficient                         |
| Quartermaster Ledger  | Gold Gain      | +12% Gold Gain / level                                               |
| Treasure Map Folio    | Luck           | +7% Luck / level                                                     |
| Salvager’s Chart      | Pickup         | +35% Pickup Radius / level                                           |
| Infamy Charter        | XP Gain        | +8% XP Gain / level                                                  |
| Knockback Rigging     | Knockback      | +18% knockback / level                                               |
| Storm Codex           | Status         | +10% status damage + 8% duration / level                             |
| Doubloon Ledger       | Meta gain      | +8% Doubloon Gain / level                                            |
| Cursed Captain’s Log  | Difficulty     | +4% enemy threat, +5% Gold/XP and +3% Doubloon payout / level        |

- Base Logbook capacity: 2. Meta unlocks raise to 3 and 4.

- Max level DEFAULT: 10 for standard Logbooks; Cursed Captain’s Log max 8.

- Rarity multiplies a single level’s effect. Suggested multipliers: Common 1.00, Uncommon 1.25, Rare 1.55, Epic 1.90, Legendary 2.50.

- Logbook upgrades never create a new slot; slot upgrades are meta progression only.

# 11. Relics / Items & Synergies

## 11.1 Relic Rules

- Relics are acquired mainly from chests, merchants, challenge POIs and bosses—not normal level-ups.

- Relics may stack unless marked Unique. Stack behavior must be explicit in data.

- Every Relic has 1–4 tags used by the synergy system (e.g., Cannon, Explosive, Greed, Fire, Summon, Shrine, LowHealth).

- Avoid “+5% stat only” on high-rarity Relics. Rare+ items should change rules, create procs, or enable a build direction.

| **Relic**           | **Rarity** | **Effect**                                                                                         | **Tags**        |
|---------------------|------------|----------------------------------------------------------------------------------------------------|-----------------|
| Lucky Doubloon      | Common     | +7% Luck.                                                                                          | Greed           |
| Gold Tooth          | Common     | +12% Gold Gain.                                                                                    | Greed           |
| Carpenter Kit       | Common     | +18 Max Hull and +0.15 HP/s.                                                                       | Defense         |
| Powder Flask        | Common     | +10% explosive damage.                                                                             | Explosive       |
| Spyglass            | Common     | +5% Crit Chance; +10% projectile range.                                                            | Crit            |
| Tar Bucket          | Common     | On hit: 12% chance to Tar for 2s (slow).                                                           | Status          |
| Magnet Compass      | Common     | Every 35s pulls all Infamy shards within 30m.                                                      | Utility         |
| Plunderer’s Key     | Common     | +9% effective chance for a paid chest to be free; free chest does not increase next chest price.   | Greed           |
| Chain Locker        | Uncommon   | Knockback hits apply Breach: -4 Armor for 4s, stacks 5.                                            | Control         |
| Double Rammer       | Uncommon   | Every 8th Cannon-tag attack repeats at 55% damage.                                                 | Cannon          |
| Black Powder Barrel | Uncommon   | Explosions have 15% chance to chain a second 50% blast.                                            | Explosive       |
| Shark Tooth         | Uncommon   | Summons deal +30% damage to enemies below 50% HP.                                                  | Summon          |
| Stormglass          | Uncommon   | Shock can jump one extra time; +10% status duration.                                               | Storm           |
| Cursed Dice         | Uncommon   | +12% Crit Chance, -8% Max Hull.                                                                    | Crit, Curse     |
| Lighthouse Lens     | Uncommon   | Charge POIs complete 12% faster and their reward values are +10%.                                  | Shrine          |
| Smuggler’s Map      | Uncommon   | Reveal one extra POI category on minimap; merchants 10% cheaper.                                   | Greed, Explore  |
| Blood Rum           | Rare       | Lifesteal overheal becomes temporary shield up to 30% max Hull.                                    | Sustain         |
| Ghost Figurehead    | Rare       | Taking damage releases a spectral broadside; 2.5s internal cooldown.                               | Curse, Defense  |
| Powder Monkey       | Rare       | Every 20 weapon attacks drops a free mini-keg near the densest cluster.                            | Explosive       |
| Siren Pearl         | Rare       | Slowed/Cursed enemies take +20% damage from summons and auras.                                     | Status          |
| King’s Tax Seal     | Rare       | Gold pickups also grant +0.02% run Damage, max +35%.                                               | Greed           |
| Drydock Warrant     | Rare       | First Relic acquired each stage is duplicated at 35% effectiveness. Unique.                        | Explore         |
| Black Flag          | Rare       | +18% enemy threat and +25% Gold/XP.                                                                | Risk            |
| Emergency Bilge     | Rare       | At \<25% Hull, gain +60% attack speed for 6s; 25s cooldown.                                        | LowHealth       |
| Shipwright’s Anvil  | Epic       | Weapon level-ups roll one additional upgrade stat; choose the better package. Unique.              | Weapon          |
| Endless Magazine    | Epic       | Compatible Quantity above projectile cap converts to +12% damage per overflow unit. Unique.        | Quantity        |
| Leviathan Heart     | Epic       | +80 Max Hull; healing pulses damage nearby enemies for 15% of heal amount.                         | Defense, Aura   |
| Davy’s Contract     | Epic       | The first lethal hit each stage leaves you at 1 Hull and curses all nearby enemies. Unique.        | Curse, Defense  |
| Crown of Plunder    | Epic       | Opening a chest permanently raises Luck by +2% for the run, but all future chest prices +8%.       | Greed           |
| Storm Crown         | Epic       | Every 30 Shock applications call a screen-wide lightning strike; 3s cooldown.                      | Storm           |
| Dead Man’s Flag     | Legendary  | Damage rises as Hull falls: up to +100% at 1 HP. Healing reduced 35%.                              | LowHealth       |
| Royal Powder Vault  | Legendary  | Explosions can critically strike and inherit 50% of global Crit Chance. Unique.                    | Explosive, Crit |
| Kraken Idol         | Legendary  | Every 12s summon a giant tentacle sweep; Size affects sweep width.                                 | Beast, Size     |
| Midas Figurehead    | Legendary  | Gold no longer drops normally; killed enemies build a bank paid in bursts every 20s at +50% value. | Greed           |
| Ghost Fleet Bell    | Legendary  | Every boss/miniboss killed summons a permanent ghost escort for the rest of the stage, max 5.      | Summon          |

## 11.2 Example Build Synergies

| **Build**      | **Core pieces**                                                                                             | **Why it works**                                                                                                                            |
|----------------|-------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------|
| Powder Tsunami | Powder Kegs + Mortar + Powder Drill + Oversized Shot Chart + Black Powder Barrel + Royal Powder Vault       | Attack speed creates more explosives; size multiplies coverage; chain blasts and crit-enabled explosions turn density into a damage engine. |
| Greed Cannon   | Coinshot + Quartermaster Ledger + Treasure Map Folio + Plunderer’s Key + King’s Tax Seal + Crown of Plunder | Gold simultaneously buys Relics and increases damage/luck, creating a deliberate spend-vs-hold tension.                                     |
| Ghost Tank     | Phantom Cannon + Cursed Lantern + Reinforced Hull + Blood Rum Contract + Ghost Figurehead + Davy’s Contract | Sustain and damage-on-hit let the player survive contact while Curse amplifies retaliation.                                                 |
| Storm Flood    | Lightning Mast + Chainshot + Powder Drill + Storm Codex + Stormglass + Storm Crown                          | Fast application stacks Shock/Breach and triggers periodic screen-clearing strikes.                                                         |
| Shark Swarm    | Shark Pack + Siren Song + Extra Hands Ledger + Long-Fuse Primer + Shark Tooth + Siren Pearl                 | Duration and quantity keep more summons active while crowd-control makes their execution damage reliable.                                   |
| One-HP Pirate  | Long Nine + Deadeye Almanac + Emergency Bilge + Dead Man’s Flag + Davy’s Contract                           | High-risk crit build that spikes attack speed/damage at low Hull and gets one safety catch per stage.                                       |

# 12. Temporary Powerups & Status Effects

## 12.1 Temporary Powerups

| **Powerup**          | **Effect**                                                                          |
|----------------------|-------------------------------------------------------------------------------------|
| Rum Ration           | Heal 35% max Hull.                                                                  |
| Powder Frenzy        | +80% attack speed for 8s.                                                           |
| Golden Hour          | +100% Gold Gain for 12s.                                                            |
| Ironclad Charm       | Invulnerable for 3.5s; no proc-on-damage triggers while invulnerable.               |
| Magnet Compass Burst | Pull all Infamy/Gold pickups within the active map radius.                          |
| Kraken Roar          | Deal massive damage to non-boss enemies in a 14m radius; bosses take capped damage. |
| Dead Calm            | Freeze normal enemies for 4s and slow bosses 35%.                                   |
| Full Sail            | +50% Move Speed for 7s; utility only, not a movement mastery system.                |

## 12.2 Status Effects

| **Status** | **Rule**                                                                                                             |
|------------|----------------------------------------------------------------------------------------------------------------------|
| Burn       | Stacking damage-over-time. Each stack has independent remaining duration; cap 20 stacks per target.                  |
| Tarred     | Movement slow. Normal enemies cap 55%; bosses cap 20%.                                                               |
| Breach     | Reduces Armor by flat points, max 5 stacks unless modified.                                                          |
| Shock      | On application, chance/guarantee to arc damage to another nearby target. Internal cooldown prevents chain loops.     |
| Curse      | Target takes increased damage from all sources; cap +60% vulnerability.                                              |
| Soaked     | Optional post-MVP tag: Shock damage +25% and Burn duration -20%. Do not add unless content needs another combo axis. |

# 13. Enemies, Elites, Minibosses & Bosses

## 13.1 Enemy Design Rules

- Most enemies should be understandable by silhouette and one behavior. Complexity comes from mixtures, not individually complex AI.

- All standard enemies move on the XZ water plane using lightweight steering; no NavMesh agent per enemy.

- Prefer 6–10 simultaneously active archetypes per biome. New types enter over time so the composition evolves.

- Ranged projectiles are slow and telegraphed; high density is acceptable only if dodge space remains legible on mobile.

- Elites are stat-modified or behavior-enhanced variants using a small affix library rather than entirely separate AI.

| **Enemy**       | **Threat cost** | **Behavior**           | **Purpose**                                                            |
|-----------------|-----------------|------------------------|------------------------------------------------------------------------|
| Deckhand Dinghy | 1               | Direct chaser          | Cheap swarm body; drops basic XP.                                      |
| Cutthroat Skiff | 2               | Offset/flanking chaser | Approaches from side vector to reduce perfect circling.                |
| Rammer Raft     | 3               | Telegraphed charge     | Locks direction for 0.5s then dashes; vulnerable after miss.           |
| Gunner Cog      | 4               | Maintain 6–9m range    | Slow cannon projectile; lower HP.                                      |
| Bomb Boat       | 3               | Suicide approach       | Flashes, accelerates, explodes; can be killed early to damage enemies. |
| Boarding Barge  | 6               | Slow tank chaser       | High HP, contact pressure, weak to Breach.                             |
| Corsair Cutter  | 7               | Strafe + burst shot    | Moves tangentially and fires short volleys.                            |
| Navy Brig       | 9               | Formation ranged       | Fires 3-shot broadside; later-stage threat.                            |
| Piranha School  | 2               | Fast swarm             | Rendered as one clustered agent with multi-fish VFX to save CPU.       |
| Shark           | 4               | Dive / surface lunge   | Untargetable only during very short telegraphed dive.                  |
| Jelly Wisp      | 3               | Drift / pulse          | Applies Shock on contact; low movement speed.                          |
| Reef Crab       | 5               | Stationary turret      | Spawns near islands; arcing shell.                                     |
| Ghost Eel       | 5               | Phase dash             | Brief fade then straight-line dash.                                    |
| Kraken Spawn    | 8               | Tentacle zone          | Creates short radial hazard around itself.                             |
| Siren Wisp      | 6               | Support                | Buffs move speed of nearby enemies; low HP priority target.            |

## 13.2 Elite Affixes

| **Affix** | **Effect**                                                      |
|-----------|-----------------------------------------------------------------|
| Armored   | +80% HP, +25 Armor.                                             |
| Swift     | +35% move speed; -10% HP.                                       |
| Volatile  | Explodes on death after 0.8s telegraph.                         |
| Rich      | +100% Gold drop value; +25% HP.                                 |
| Cursed    | Nearby enemies gain +15% damage; elite emits purple/ghost aura. |
| Captain   | Periodic rally that spawns 2 cheap units; cooldown 8s.          |

## 13.3 Miniboss Timing

Default stage spawns one miniboss around 30% elapsed and another around 72% elapsed. Tier 3 may add a third. Minibosses drop a high-value chest or Relic. Spawn time should have ±20s deterministic jitter from the run seed.

## 13.4 Boss Framework

| **Boss**             | **Biome**       | **Fantasy**            | **Core mechanics**                                                                     |
|----------------------|-----------------|------------------------|----------------------------------------------------------------------------------------|
| Barnacle King        | Emerald Reaches | Armored pirate galleon | Rotating cannon arcs, summon dinghies, breakable armor plates.                         |
| Reef Maw             | Emerald Reaches | Giant sea beast        | Telegraphed bite lanes, whirlpool pull zones, reef-crab adds.                          |
| Admiral Brassbeard   | Emerald Reaches | Flagship final boss    | Alternates broadside barrages and moving safe lanes; elite escort phase.               |
| Widow of the Fog     | Gravewater      | Ghost frigate          | Vanish/reappear, phantom volleys, cursed wake.                                         |
| Bone Leviathan       | Gravewater      | Skeletal serpent       | Segment sweeps, bone projectiles, arena-crossing dive.                                 |
| Drowned Armada Queen | Gravewater      | Ghost fleet commander  | Summons ghost escorts and marks danger zones; final enrage.                            |
| Powder Saint         | Ashen Strait    | Explosive fire-ship    | Keg fields, chain detonations, fire trails.                                            |
| Molten Kraken        | Ashen Strait    | Volcanic kraken        | Tentacle slams, lava zones, projectile spit.                                           |
| Caldera Dreadnought  | Ashen Strait    | Heavy naval fortress   | Slow rotating batteries, armored turrets, final full-screen salvo with safe corridors. |

| **Boss readability rule —** A boss pattern must be beatable with movement alone even if the player has zero defensive Relics. Boss mechanics may pressure positioning, but must not require advanced sailing, manual aiming or camera rotation. |
|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

# 14. World, Biomes, Procedural Generation & POIs

## 14.1 Biomes

| **Sea**         | **Art direction**           | **Gameplay modifier**                                                 | **Content purpose**                           |
|-----------------|-----------------------------|-----------------------------------------------------------------------|-----------------------------------------------|
| Emerald Reaches | Bright tropical archipelago | Reefs/islets create lanes; occasional treasure shoals.                | Starter biome; clean silhouettes.             |
| Gravewater      | Foggy cursed sea            | Fog Pulse briefly reduces distant enemy visibility; ghost wreck POIs. | More ranged/phase enemies; curse items.       |
| Ashen Strait    | Volcanic ocean              | Static lava vents / cannon-fort hazards telegraph zones.              | Higher projectile pressure; explosive themes. |

## 14.2 Map Size / Generation

| **Parameter**      | **Default**                                                                                                                           |
|--------------------|---------------------------------------------------------------------------------------------------------------------------------------|
| Playable footprint | ~220m x 220m for MVP; full stages may vary 200–280m.                                                                                  |
| Generation basis   | Seeded grid of 14 x 14 logical cells, each ~16m.                                                                                      |
| Water              | Single low-cost plane / tiled mesh. No dynamic ocean physics.                                                                         |
| Land obstacles     | Place from modular island/reef prefab library; use simple convex/static colliders.                                                    |
| Navigability       | Reserve one connected “water backbone” plus multiple loops. Validate path connectivity using 2D occupancy grid before accepting seed. |
| Spawn annulus      | Enemies spawn ~13–22m from player, outside camera where possible, never inside static land.                                           |
| POI spacing        | Minimum 18m between major POIs; avoid placing POIs within 8m of stage edge/land choke.                                                |
| Determinism        | Map layout, POI types and scheduled miniboss jitter derive from run seed + stage index.                                               |

## 14.3 POI Catalogue

| **POI**               | **Function**                                                                                |
|-----------------------|---------------------------------------------------------------------------------------------|
| Salvage Chest         | Pay escalating Gold → random Relic. Luck affects rarity.                                    |
| Gilded Wreck          | One free Relic; rare spawn; Luck affects rarity.                                            |
| Merchant Skiff        | Shows 3 Relics; buy one. Inventory fixed when stage loads so scouting has value.            |
| Lighthouse of Fortune | Remain inside ring ~5s → choose 1 of 3 permanent-for-run stat bonuses.                      |
| Cursed Idol           | Increase Bounty +1 immediately; grants instant Gold + improves end-run Doubloon multiplier. |
| Trial Buoy            | Starts a 20–30s challenge wave; clear marked enemies → free chest.                          |
| Siren Bell            | Instantly vacuum all Infamy within current stage; one use.                                  |
| Drydock Crane         | Duplicate one owned Relic by sacrificing a different Relic of same rarity; limited uses.    |
| Captain’s Grave       | Choose 1 of 2 free utility Relics; can be locked behind a short kill challenge.             |
| Powder Cache          | Spawns one temporary Powerup pickup.                                                        |
| Boss Beacon           | Interact to summon stage boss. Always map-visible after first 60s.                          |
| Exit Maelstrom        | Appears after boss death; proceed to next stage / end run.                                  |

## 14.4 Generation Algorithm

```text
GenerateStage(seed):
rng = Seed(seed, stageIndex, tier)
occupancy = CreateWaterGrid(14,14)
PlaceBoundaryDecorOnly()
PlaceIslandClusters(rng, targetCoverage=18%..26%)
CarveConnectedWaterBackbone()
if !ValidateReachability(minOpenWidth=2 cells): regenerate with seed+salt
candidates = FindOpenCells(clearance >= POIClearance)
PlaceBossBeacon(farthestValidCandidateFromSpawn)
PlaceMajorPOIs(weightedTable, minDistance=18m)
PlaceMinorDecorAndBreakables()
BakeStaticObstacleGridForEnemyAvoidance()
Return StageLayout
```

# 15. Economy, Shops, Loot & Risk/Reward

## 15.1 Currencies

| **Resource** | **Scope**      | **Earned from**                         | **Spent on / purpose**                                             |
|--------------|----------------|-----------------------------------------|--------------------------------------------------------------------|
| Infamy       | Run only       | Enemy kills, POIs                       | XP / level-ups automatically.                                      |
| Gold         | Run only       | Enemies, breakables, POIs               | Chests, merchants, Drydock interactions.                           |
| Doubloons    | Meta           | Run payout, boss kills, quests          | Unlock ships, weapons, Logbooks, Relics, decision tools, slots.    |
| Bounty       | Run risk meter | Voluntary Cursed Idols / Cursed Logbook | Not spent. Raises enemy threat and reward multipliers.             |
| Run Seed     | Debug/meta     | Generated at run start                  | Reproduce map/director randomness; shown on end screen/debug menu. |

## 15.2 Chest Price Curve

```text
paidChestIndex n starts at 1
ChestCost(n) = round(25 + 34*(n-1) + 4.5*(n-1)^2)
StageCostMult = [1.00, 1.15, 1.30][stageIndex]
FinalCost = round(ChestCost(n) * StageCostMult * RelicPriceModifiers)
```

This is intentionally not the reference game’s exact curve. It creates early affordability and late scarcity while remaining easy to reason about. Free-chest effects do not increment paidChestIndex.

## 15.3 Relic Rarity Roll

| **Rarity** | **Base weight** | **Single-level effect multiplier** |
|------------|-----------------|------------------------------------|
| Common     | 70.0            | 1.00x                              |
| Uncommon   | 22.5            | 1.25x                              |
| Rare       | 6.0             | 1.55x                              |
| Epic       | 1.35            | 1.90x                              |
| Legendary  | 0.15            | 2.50x                              |

```text
for each rarity r:
rarityExponent = [0,1,2,3,4][r]
adjustedWeight[r] = baseWeight[r] * pow(1 + Luck, 0.55 * rarityExponent)
normalize(adjustedWeight)
# Clamp Legendary final probability <= 4% in standard loot sources.
```

## 15.4 Bounty Risk/Reward

| **Bounty level** | **Enemy threat** | **Gold + XP** | **Doubloon payout** |
|------------------|------------------|---------------|---------------------|
| 0                | 1.00x            | 1.00x         | 1.00x               |
| 1                | 1.06x            | 1.08x         | 1.05x               |
| 2                | 1.12x            | 1.16x         | 1.10x               |
| 3                | 1.18x            | 1.24x         | 1.16x               |
| 4                | 1.24x            | 1.32x         | 1.22x               |
| 5                | 1.30x            | 1.40x         | 1.30x               |
| 6+               | +6% each         | +8% each      | +8% each; cap 2.0x  |

# 16. Progression, Unlocks, Quests & Challenges

## 16.1 Meta Philosophy

- Meta progression should expand possibility more than raw power. A skilled new account must still be able to clear early content.

- Unlock pipeline: complete objective → entry becomes purchasable → spend Doubloons → content enters future selection pools.

- Unlocked content can be toggled out of the pool only after a later meta upgrade, preventing early players from immediately pruning randomness.

- Quest text should teach a mechanic. Prefer “apply 1,000 Shock” over “play 10 hours.”

## 16.2 Suggested Milestones

| **Completed quests** | **Unlock**                                                                       |
|----------------------|----------------------------------------------------------------------------------|
| 0                    | 2 Weapon slots, 2 Logbook slots; no reroll/skip/banish.                          |
| 8                    | Reroll: 1 free reroll per level-up; extra rerolls cost escalating Gold.          |
| 15                   | Third Weapon slot purchasable.                                                   |
| 22                   | Skip option.                                                                     |
| 30                   | Third Logbook slot purchasable.                                                  |
| 40                   | Banish option: remove one offer from current run pool.                           |
| 52                   | Fourth Weapon slot purchasable.                                                  |
| 65                   | Fourth Logbook slot purchasable.                                                 |
| 80                   | Pool Toggler: disable a limited number of unlocked content entries before a run. |

## 16.3 Quest Examples

| **Quest type**    | **Example**                                                                             |
|-------------------|-----------------------------------------------------------------------------------------|
| Weapon discovery  | Kill 750 enemies with Deck Cannons → unlock Long Nine purchase.                         |
| Status learning   | Apply 1,000 Burn stacks → unlock Burning Oil.                                           |
| Economy           | Open 25 paid chests → unlock Plunderer’s Key.                                           |
| Risk              | Clear a stage at Bounty 4+ → unlock Black Flag.                                         |
| Shrine mastery    | Complete every Lighthouse of Fortune in one stage → unlock Lighthouse Lens.             |
| Purist challenge  | Defeat a stage boss without opening a chest or merchant → unlock a high-skill Relic.    |
| Low health        | Kill a boss while below 15% Hull → unlock Dead Man’s Flag.                              |
| Exploration       | Find three Gilded Wrecks across runs → unlock Smuggler Cutter.                          |
| Build target      | Reach Quantity +8 in one run → unlock Endless Magazine.                                 |
| Overtime          | Survive Dread Tide for 3 minutes → unlock Ghost Fleet Bell purchase.                    |
| Character mastery | Clear a full voyage with Ironjaw Galleon → alternate skin + 5 Doubloons, not raw power. |
| Biome feat        | Clear Gravewater without taking Curse damage → cosmetic flag / quest completion.        |

## 16.4 Challenge Modifiers

| **Challenge**  | **Rule**                                                                              | **Reward modifier** |
|----------------|---------------------------------------------------------------------------------------|---------------------|
| Glass Hull     | Player dies on the second damaging hit each stage; shield does not reset the counter. | +60% Doubloons      |
| Powder Rush    | Chest prices -75%; enemy threat ramps 4x faster.                                      | +45%                |
| No Helm Needed | Ship movement locked after first 30s; build must carry the run.                       | +55%                |
| Poor Pirate    | Gold drops disabled; chests are free but only 3 may be opened per stage.              | +35%                |
| Cursed Waters  | Start at Bounty 5.                                                                    | +40%                |
| One Gun        | Weapon capacity fixed at 1; weapon levels have +25% effect.                           | +35%                |

# 17. UI/UX, Onboarding & Accessibility

## 17.1 Screens

| **Screen**      | **Required content**                                                                                                   |
|-----------------|------------------------------------------------------------------------------------------------------------------------|
| Boot            | Logo, loading progress, version/build hash.                                                                            |
| Main Menu       | Play, Unlocks, Quests, Challenges, Settings, Credits.                                                                  |
| Run Setup       | Ship, Sea, Tier, Challenge, content toggler (when unlocked), seed field in debug builds.                               |
| HUD             | Hull/shield, level + XP bar, timer/overtime, Gold, Bounty, minimap/POI indicators, weapon/logbook/relic quick strip.   |
| Level-Up Modal  | Three large cards; rarity; current→next stat delta; Reroll/Skip/Banish if unlocked. Combat paused.                     |
| POI Shop/Choice | Large tap-friendly cards; Gold price; compare/stack note.                                                              |
| Pause           | Resume, settings, abandon run with confirmation.                                                                       |
| Run End         | Result, stage reached, boss kills, total damage by weapon, Gold spent, Doubloons earned, quests completed, seed.       |
| Unlocks         | Filter by Ship / Weapon / Logbook / Relic; locked entries show objective; unlocked-but-unpurchased show Doubloon cost. |

## 17.2 HUD Layout

- Top-left: Hull + shield, level/XP.

- Top-center: stage timer; turns red and labels DREAD TIDE in overtime.

- Top-right: Gold and Bounty; pause button on Android.

- Bottom-center: compact weapon icons with levels; Logbook strip beneath on larger screens.

- Minimap: circular or square 150–180 px desktop; 120–150 px mobile; major POIs only by default.

- Never require reading small descriptive text during live combat. Full item text appears only in paused choice screens.

## 17.3 Onboarding

> **1.** First launch: 3-card tutorial: MOVE / AUTO-FIRE / LEVEL UP. No more than 20 seconds.
>
> **2.** First stage spawns a safe XP cluster and 6 low-threat enemies within first 15 seconds.
>
> **3.** First level-up always offers Deck Cannons upgrade + one new Weapon + one Logbook so player sees all upgrade categories.
>
> **4.** First Salvage Chest is placed within 25m and cannot roll above Common/Uncommon so the concept is clear.
>
> **5.** Boss Beacon is highlighted with a directional indicator at 60 seconds remaining.
>
> **6.** After first run, Unlocks/Quests tab receives a single guided highlight; no forced multi-screen tutorial.

## 17.4 Accessibility

- Damage flashes and screen shake sliders; option to disable shake.

- Colorblind-safe rarity mode adds icons/text labels, not color alone.

- Large UI scale presets: 90/100/115/130%.

- Android joystick size/opacity/position settings and left/right-handed preset.

- Optional high-contrast enemy outline and projectile outline.

- Reduced VFX mode separate from graphics quality.

- No rapid tapping requirement during core combat.

# 18. Art, VFX & Audio Direction

## 18.1 Visual Direction

- Low-poly/stylized 3D with chunky silhouettes and saturated readability. Avoid photoreal water, realistic rigging simulation or fine thin geometry.

- Ships use modular hull + mast + weapon sockets. Silhouette and sail/flag color identify the avatar; enemies use distinct hull profiles.

- Ocean shader: opaque or near-opaque stylized surface, one normal/detail layer max, cheap vertex bob. No planar reflections in Low/Mid quality.

- Use atlas textures and shared materials aggressively. A biome should aim for one environment atlas, one ship/enemy atlas and a small VFX atlas.

- Projectiles exaggerate size and trail brightness so they remain readable from the elevated camera.

## 18.2 VFX Language

| **Event**       | **Feedback**                                                           |
|-----------------|------------------------------------------------------------------------|
| Normal hit      | Small flash + tiny impact sprite; no camera shake.                     |
| Crit            | Larger impact, brief number emphasis, micro camera impulse if enabled. |
| Kill            | Compact burst; XP shard pops outward then settles.                     |
| Elite kill      | Distinct gold/purple burst + stronger audio.                           |
| Level up        | Short radial light pulse and UI sting; gameplay pauses immediately.    |
| Legendary Relic | Gold beam / flag burst; capped to one dramatic effect.                 |
| Boss phase      | Large telegraph ring/line with minimum 0.7s readable lead time.        |
| Overtime        | Ocean tint + alarm bell + HUD state; avoid heavy fullscreen post FX.   |

## 18.3 Audio

- Music layers can intensify with stage time/Bounty, but do not require runtime stem mixing in MVP.

- Weapon SFX must be short and voice-limited. Repeated cannons use 4–6 variants with pitch jitter.

- At high fire rates, aggregate audio: max N simultaneous instances per weapon family; extra shots are silent.

- Boss telegraphs use unique audio cues that remain audible under weapon noise.

- Android vibration: optional light hit / level-up / boss telegraph haptics.

# 19. Technical Architecture for WebGL + Android

## 19.1 Engine / Rendering Assumptions

| **Area**     | **Decision**                                                                                                      |
|--------------|-------------------------------------------------------------------------------------------------------------------|
| Engine       | Unity 6 LTS; lock exact patch at project kickoff.                                                                 |
| Renderer     | URP, Forward rendering, SRP Batcher on.                                                                           |
| Color        | LDR by default. HDR/post-processing off on Low; minimal on High.                                                  |
| Physics      | Static colliders for islands/POIs; custom 2D-on-XZ collision for swarm/projectiles where practical.               |
| Input        | Unity Input System: keyboard composite + virtual joystick abstraction.                                            |
| Networking   | None for MVP. Analytics may send events asynchronously if a backend is later connected.                           |
| Save         | Versioned local JSON behind ISaveBackend; WebGL uses PlayerPrefs/IndexedDB path, Android persistent storage.      |
| Content      | ScriptableObjects for authoring; runtime immutable data copies/IDs. Addressables optional for post-MVP map packs. |
| Orientation  | Landscape locked on Android.                                                                                      |
| Frame target | 60 FPS preferred; 30 FPS minimum acceptable on low-end Android via automatic quality scaling.                     |

## 19.2 Scene Architecture

| **Scene**        | **Contents**                                                                                                     |
|------------------|------------------------------------------------------------------------------------------------------------------|
| Boot             | Persistent AppRoot, SaveService, AudioService, ConfigService, scene loader.                                      |
| MainMenu         | Menus and lightweight ship preview; no gameplay systems.                                                         |
| Run              | WorldRoot, PlayerShip, CameraRig, EnemySimulation, WeaponSystem, PickupSystem, SpawnDirector, POIManager, RunUI. |
| Test_Combat      | Developer-only micro scene for weapon/enemy testing.                                                             |
| Test_Performance | Developer-only stress harness with controllable enemy/projectile caps.                                           |

## 19.3 Runtime Services

| **Service**      | **Responsibility**                                                                                       |
|------------------|----------------------------------------------------------------------------------------------------------|
| RunManager       | Own run state, stage transitions, pause reasons, win/death.                                              |
| RunRng           | Deterministic seeded random streams; separate streams for loot/map/director to reduce cascading changes. |
| PlayerStats      | Base + additive + multiplicative modifiers; raises stat-changed events only when values change.          |
| WeaponController | Own equipped WeaponInstances and tick schedules.                                                         |
| EnemyWorld       | Central registry/arrays for enemy positions, HP, tags and spatial hash membership.                       |
| SpawnDirector    | Threat budget, composition selection, spawn annulus and caps.                                            |
| ProjectileWorld  | Pooled projectiles; custom movement/collision; hard caps.                                                |
| PickupWorld      | Infamy/Gold pooling, merge/attraction logic.                                                             |
| POIManager       | POI lifecycle, interactable choices and minimap registration.                                            |
| LootService      | Rarity rolls, pool filtering, Relic selection, duplicate rules.                                          |
| QuestService     | Event-driven objective progress; persists meta progress after run.                                       |
| SaveService      | Atomic save/load/migration.                                                                              |
| AnalyticsService | Buffered optional events; no hard dependency for gameplay.                                               |

## 19.4 Hard Technical Rules

- MUST NOT instantiate/destroy enemies, projectiles, pickups or combat VFX during normal play; use pools.

- MUST NOT use one NavMeshAgent per enemy.

- MUST NOT call FindObjectOfType / GameObject.Find in gameplay loops.

- MUST avoid LINQ, boxing and per-frame heap allocations in enemy/projectile/weapon update paths.

- MUST use capped queries and spatial partitioning for target acquisition; no full-enemy scan per weapon per shot.

- MUST pause gameplay systems explicitly on modal choices; do not rely only on Time.timeScale if timers use unscaled time.

- MUST support WebGL without threads as a correctness requirement. Jobs/Burst may be optional optimizations, never required for game logic.

- MUST keep gameplay logic independent of platform UI/input through interfaces.

# 20. Data Schemas, State Machines & Pseudocode

## 20.1 Suggested Folder Structure

```text
Assets/_Game/
Art/
Ships/ Enemies/ Environment/ VFX/ UI/
Audio/
Data/
Ships/ Weapons/ Logbooks/ Relics/ Enemies/ Bosses/ Stages/ LootTables/ Quests/
Prefabs/
Player/ Enemies/ Projectiles/ Pickups/ POI/ UI/
Scenes/
Boot.unity MainMenu.unity Run.unity Tests/
Scripts/
Core/ Data/ Player/ Combat/ Weapons/ Enemies/ World/ POI/ Meta/ UI/ Save/ Analytics/ Platform/
Tests/
EditMode/ PlayMode/ Performance/
```

## 20.2 ShipDefinition

```csharp
ShipDefinition {
string id;
string displayName;
GameObject visualPrefab;
float baseHull;
float baseMoveSpeed;
float baseArmor;
float baseEvasion;
string startingWeaponId;
string passiveId;
string unlockQuestId;
int unlockDoubloonCost;
}
```

## 20.3 WeaponDefinition

```csharp
WeaponDefinition {
string id; string displayName;
WeaponPattern pattern; TargetingMode targeting;
string[] tags;
float baseDamage; float baseCooldown; float range;
int baseQuantity; float projectileSpeed; float duration; float size; float knockback;
int maxLevel = 12;
UpgradeAxis[] validUpgradeAxes;
string projectilePrefabId; string vfxId; string sfxId;
float procCoefficient = 1.0;
}
```

## 20.4 RelicDefinition

```csharp
RelicDefinition {
string id; string displayName; Rarity rarity;
string[] tags; bool unique; int maxStacks;
Modifier[] passiveModifiers;
TriggerEffect[] triggers; // OnHit, OnKill, OnDamageTaken, OnChestOpen, OnBossKill...
string unlockQuestId; int unlockDoubloonCost;
}
```

## 20.5 EnemyDefinition

```csharp
EnemyDefinition {
string id; string biomeTag;
float baseHP; float baseDamage; float moveSpeed; float radius;
int threatCost; int xpValue; int goldValue;
EnemyBehavior behavior;
string projectileId; float attackCooldown; float preferredRange;
string[] tags;
}
```

## 20.6 Run State Machine

```text
Boot -> MainMenu -> RunSetup -> StageIntro -> Playing
Playing -> LevelUpPaused -> Playing
Playing -> POIChoicePaused -> Playing
Playing -> BossFight -> BossDefeated -> StageReward
StageReward -> NextStage OR RunComplete
Playing/BossFight -> PlayerDead -> RunComplete
Playing -> Paused -> Playing
RunComplete -> MainMenu
```

## 20.7 Upgrade Offer Algorithm

```text
BuildOfferPool():
candidates = []
if weaponSlotsAvailable: candidates += unlockedWeaponsNotOwned
candidates += equippedWeaponsWhere(level < maxLevel)
if logbookSlotsAvailable: candidates += unlockedLogbooksNotOwned
candidates += equippedLogbooksWhere(level < maxLevel)
removeBannedEntriesForRun(candidates)
applyContextWeights(candidates) // prevent 3 dead/incompatible choices

GenerateThreeOffers():
repeat until 3 unique offers:
entry = WeightedPick(candidates)
rarity = RollRarity(player.Luck, source=LEVEL_UP)
offer = BuildUpgradePackage(entry, rarity)
if no offers possible: convert level-up to Gold + small full-heal
```

## 20.8 Enemy Steering Pseudocode

```text
for enemy in activeEnemies (at simulationHz):
toPlayer = playerPos - enemy.pos
desired = BehaviorDesiredVelocity(enemy, toPlayer)
avoid = StaticGridAvoidance(enemy.pos, desired)
separation = SampleNearbySeparation(enemy, maxNeighbors=6)
enemy.velocity = clamp(desired + avoid + separation, enemy.maxSpeed)
enemy.pos += enemy.velocity * simDt

# Render transform interpolates between simulation updates.
# Low quality may simulate distant enemies at 10–15 Hz; near enemies at 30 Hz.
```

# 21. Performance Budgets & Quality Scaling

| **Performance definition of done —** The game is not “done” if it only reaches spectacle on a development PC. The same build must degrade gracefully on Android and browser hardware without changing the core rules. |
|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|

## 21.1 Target Budgets

| **Metric**                        | **Budget**                                                                                                   |
|-----------------------------------|--------------------------------------------------------------------------------------------------------------|
| Initial WebGL compressed download | ≤ 35 MB target for core MVP; stretch ≤ 45 MB.                                                                |
| Android AAB                       | ≤ 120 MB MVP target; under Play asset limits without external packs.                                         |
| WebGL runtime memory              | Target \< 450 MB peak; investigate any sustained \> 550 MB.                                                  |
| Android runtime memory            | Target \< 550 MB peak on mid device; Low profile aims \< 400 MB.                                             |
| Draw calls                        | Low ≤ 100; Mid ≤ 140; High/Web desktop ≤ 200 typical.                                                        |
| Active enemy agents               | Low 160; Mid 260; High 420. Boss/adds reserve 20 slots.                                                      |
| Active projectile sim objects     | Low 280; Mid 500; High 900. Quantity overflow converts to batched visual/damage.                             |
| Pickup objects                    | Hard cap 180 visible; merge nearby same-type pickups when cap approached.                                    |
| Particles                         | Low ~1,500 visible particles; Mid 3,000; High 6,000, mostly GPU/simple.                                      |
| Main-thread GC                    | 0 B/frame during steady combat target; no spikes \>1 MB caused by combat loops.                              |
| Frame time                        | 60 FPS target: 16.6 ms; Low can auto-lock 30 FPS if sustained \>25 ms.                                       |
| Loading                           | First meaningful menu \<8 s on a 10 Mbps connection after HTML load, excluding browser compilation variance. |

## 21.2 Quality Profiles

| **Feature**       | **Low**           | **Mid**               | **High**                       |
|-------------------|-------------------|-----------------------|--------------------------------|
| Enemy cap         | 160               | 260                   | 420                            |
| Projectile cap    | 280               | 500                   | 900                            |
| Shadow            | Off               | One 1024 main light   | One 2048 main light            |
| Water             | Opaque unlit/toon | 1 normal + foam       | Normal + foam + light fresnel  |
| VFX density       | 0.45x             | 0.75x                 | 1.0x                           |
| Damage numbers    | Boss/crit only    | Capped 20/s           | Capped 40/s                    |
| Enemy sim Hz far  | 10                | 15                    | 20                             |
| Enemy sim Hz near | 20                | 30                    | 30                             |
| Post FX           | Off               | Optional color adjust | Color adjust + tiny bloom only |

## 21.3 Scalability Techniques

- Object pools sized from quality profile; resize only between stages, not during combat.

- Merge pickup shards: when \>cap, combine the nearest low-value shards into one higher-value pickup.

- Projectile overflow: if a volley would exceed cap, spawn capped visuals but preserve expected DPS through batched hit rays / damage multiplier.

- Distant enemies: lower simulation frequency and disable expensive VFX; never disable damage/HP logic in a way players can exploit.

- Audio voice limits per family; cap cannon/explosion voices.

- Use texture atlases and shared materials; avoid material-per-enemy-instance.

- Strip unused shader variants and packages from WebGL builds.

- Use Brotli compression for production WebGL hosting with correct Content-Encoding headers.

# 22. Analytics, Balance Telemetry & Live Tuning

## 22.1 Events

| **Event**             | **Key fields**                                                                                           |
|-----------------------|----------------------------------------------------------------------------------------------------------|
| run_start             | build, platform, deviceTier, shipId, biome, tier, challenge, seed                                        |
| run_end               | result, deathSource, stage, time, level, kills, bossKills, goldEarned/spent, doubloons, avgFPS, p1LowFPS |
| level_up_offer        | level, offer IDs, rarities, rerollCount                                                                  |
| level_up_pick         | level, selectedId, rarity, currentBuildSummary                                                           |
| relic_acquired        | source, relicId, rarity, stackCount                                                                      |
| poi_used              | poiType, stageTime, cost, bounty                                                                         |
| boss_spawn            | bossId, stageTime, playerLevel, DPS snapshot                                                             |
| boss_kill             | bossId, duration, damageTaken, playerLevel                                                               |
| weapon_damage_summary | weaponId, totalDamage, kills, procCount                                                                  |
| performance_sample    | fps, frameMs, activeEnemies, projectiles, memoryEstimate, qualityProfile                                 |

## 22.2 Balance Dashboards / Questions

- Stage death curve: where do players fail by minute and by device tier?

- Weapon pick rate vs win/clear rate vs average damage share. High pick + high win indicates dominance; low pick + low damage indicates dead content.

- Relic co-occurrence pairs that massively raise clear rate may be intentional “broken” builds; only nerf if they erase choice or crash performance.

- Average level by minute; target early level cadence ~20–30s, then slower as run matures.

- Gold earned/spent/chest count; players should regularly face “buy now vs save” decisions.

- Boss time-to-kill distribution; bosses should test builds without becoming HP sponges.

- Performance correlated with build tags (Quantity, Summon, Explosive) to catch systemic cost problems.

## 22.3 Remote Tuning

For MVP, store balance in ScriptableObjects and ship builds. Before live operation, add a versioned JSON override layer for numeric tuning only (enemy HP, spawn weights, rarity weights, costs, weapon coefficients). Do not allow remote changes to prefab references or code paths in the first implementation.

# 23. MVP, Vertical Slice & 8-Week Roadmap

## 23.1 MVP Scope — “One Good 10-Minute Run”

| **Area**    | **MVP content**                                                                                 |
|-------------|-------------------------------------------------------------------------------------------------|
| Player      | 1 ship: Cutlass Sloop.                                                                          |
| Movement    | WASD + Android joystick; fixed camera.                                                          |
| Weapons     | 6: Deck Cannons, Broadside Battery, Powder Kegs, Lightning Mast, Flying Cutlasses, Burning Oil. |
| Logbooks    | 8: Damage, Attack Speed, Size, Quantity, Max Hull, Regen, XP Gain, Luck.                        |
| Relics      | 12: 6 Common, 4 Uncommon, 2 Rare.                                                               |
| Enemies     | 6 standard + 1 elite affix + 1 miniboss.                                                        |
| Boss        | Barnacle King.                                                                                  |
| Map         | Emerald Reaches single generated stage, 10 min.                                                 |
| POIs        | Salvage Chest, Lighthouse of Fortune, Trial Buoy, Boss Beacon.                                  |
| Economy     | Infamy, Gold, Doubloons.                                                                        |
| Meta        | 10 quests, 5 purchases, 1 reroll unlock.                                                        |
| Platforms   | Playable WebGL deployment + Android APK/AAB from same content branch.                           |
| Performance | Low/Mid/High profiles and stress test scene included.                                           |

## 23.2 Vertical Slice

- 3 ships (Cutlass Sloop, Ironjaw Galleon, Red Fuse Ketch).

- 10 weapons, 12 Logbooks, 20 Relics.

- 8 standard enemies + 3 elite affixes + 2 minibosses.

- One complete 3-stage Emerald Reaches voyage with 3 bosses.

- 7 POI types including merchant and cursed risk system.

- 25 quests, reroll/skip, third weapon slot purchase.

- Polished first-run tutorial, end-run breakdown, audio/VFX pass.

- WebGL + Android performance certification on defined test hardware.

## 23.3 Full Initial Content Target

- 12 ships, 22 weapons, 18 Logbooks, ~35–45 Relics.

- 3 Seas, each with 3 stages / 3 bosses; Tiers 1–3 reuse biome content with scaling and composition changes.

- 12+ standard enemy archetypes plus biome variants, 6+ minibosses, 9 bosses.

- 80–120 quests and 6–10 challenge modifiers.

- 4 Weapon / 4 Logbook max slots via meta progression.

## 23.4 8-Week Build Plan

| **Milestone**                  | **Build work**                                                                                          | **Exit criterion**                                                                        |
|--------------------------------|---------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------|
| Week 1 — Foundation            | Boot/Menu/Run scenes; input abstraction; kinematic ship; camera; save skeleton; pools; performance HUD. | Ship moves identically on WebGL and Android; no per-frame GC in movement loop.            |
| Week 2 — Combat Sandbox        | EnemyWorld + spatial hash; 3 enemies; damage; pickups; 3 weapons; XP + level-up modal.                  | 5-minute sandbox is fun with placeholder art and stable at target caps.                   |
| Week 3 — Buildcraft            | 6 weapons, 8 Logbooks, rarity offers, reroll, 12 Relics, status/proc framework.                         | At least 3 distinct viable builds emerge in internal testing.                             |
| Week 4 — World / Economy       | Procedural Emerald map, chest, lighthouse, trial, Gold curve, boss beacon, minimap.                     | Seed reproduces layout and POIs; no unreachable POI seeds in 1,000 automated generations. |
| Week 5 — Boss / Run End        | Barnacle King, miniboss, overtime, run summary, Doubloons, quests/unlocks.                              | Complete 10-minute run loop from menu to payout.                                          |
| Week 6 — Vertical Content      | Add 2 ships, 4 weapons, 8 Relics, more enemies, stage transition foundation.                            | One polished stage + second stage graybox; content authoring is data-driven.              |
| Week 7 — Platform Optimization | Quality profiles, VFX/audio caps, WebGL compression, Android profiling, memory cleanup.                 | Pass performance budgets on test hardware.                                                |
| Week 8 — Polish / QA           | Tutorial, accessibility, balance, bug burn, analytics, deployment docs.                                 | Release-candidate vertical slice with acceptance checklist green.                         |

# 24. QA Plan & Acceptance Criteria

## 24.1 Automated Tests

| **Test**                | **Acceptance**                                                                                                            |
|-------------------------|---------------------------------------------------------------------------------------------------------------------------|
| Movement normalization  | Diagonal movement speed differs from cardinal by \<1%.                                                                    |
| Platform input          | Keyboard and virtual joystick produce same normalized movement vector.                                                    |
| Damage formula          | Golden test vectors match expected results for armor/crit/elite modifiers.                                                |
| Rarity roll             | 1,000,000 seeded rolls stay within tolerance of configured weights; Luck monotonically increases higher-tier probability. |
| XP progression          | No level can require \<= previous level XP; level-up triggers exactly once per threshold crossing.                        |
| Pool reuse              | 10,000 spawn/despawn cycles produce no missing references and no steady memory growth.                                    |
| Procedural reachability | 1,000 generated MVP stages: player spawn, boss beacon and all major POIs reachable in occupancy grid.                     |
| Determinism             | Same seed + same ordered inputs creates same map, POI list and director schedule.                                         |
| Save migration          | Old schema vN loads into current schema with defaults and no loss of unlocked IDs.                                        |
| Offer validity          | No level-up card references locked/disabled content; no duplicate card IDs in same offer unless explicitly allowed.       |
| Cap handling            | Projectile/pickup overflow preserves damage/value and does not instantiate above profile hard cap.                        |

## 24.2 Manual / Playtest Acceptance

- Within 60 seconds, a new player can move, understand auto-fire and make a level-up choice without explanation from a tester.

- At minute 1 the screen is readable and not empty; at minute 8–10 it feels crowded and powerful without becoming visually incoherent.

- At least three MVP builds can clear the boss under competent play; no single weapon exceeds 55% pick rate across internal runs once all are available.

- No mandatory content requires complex sailing, manual aim, camera rotation or on-foot control.

- WebGL build launches in Chrome/Edge/Firefox target versions used by QA; browser refresh does not corrupt save.

- Android build handles pause/resume, phone notifications/app backgrounding and resolution/safe-area differences.

- No normal 10-minute run causes memory to grow monotonically after the first 2 minutes; pooled counts stabilize.

- Low quality remains mechanically equivalent to High quality. VFX can be reduced, but hazards/telegraphs cannot disappear.

- Boss can be defeated with starter ship and no meta stat advantage, given a reasonable build.

- Abandon/run-end always banks only intended meta rewards and never duplicates payout on scene reload.

## 24.3 Device Test Matrix

| **Class**      | **Representative target**      | **Pass bar**                                                         |
|----------------|--------------------------------|----------------------------------------------------------------------|
| Android Low    | 4–6 GB RAM, older midrange GPU | 30 FPS average in minute 8+ on Low; no thermal crash in 20-min soak. |
| Android Mid    | 6–8 GB RAM modern midrange     | 45–60 FPS typical on Mid.                                            |
| Android High   | Recent flagship                | 60 FPS High except brief boss/VFX spikes.                            |
| WebGL Laptop   | Integrated GPU, 8 GB RAM       | 30–60 FPS Low/Mid; initial load target met.                          |
| WebGL Desktop  | Discrete GPU                   | 60 FPS High at 1080p; high enemy cap.                                |
| Browser memory | All QA browsers                | No out-of-memory in 30-min overtime soak using Low profile.          |

# 25. AI Agent Implementation Contract

| **Agent objective —** Build the smallest complete fun loop first, then expand content. Never add simulation complexity that is not required by this specification. |
|--------------------------------------------------------------------------------------------------------------------------------------------------------------------|

## 25.1 Hard Instructions for the Agent

> **1.** The PlayerShip GameObject is the only player-controlled gameplay body. DO NOT implement an on-foot pirate, boarding controller, walking state or disembark system.
>
> **2.** Use WASD / camera-relative planar movement and Android virtual joystick. DO NOT implement wind, sail angle, rudder physics, tacking, wave propulsion, drift mechanics or realistic turn radius.
>
> **3.** All MVP weapons auto-fire. DO NOT add manual aiming unless a later task explicitly requests it.
>
> **4.** Target WebGL + Android continuously. Every milestone must build successfully for both targets before proceeding.
>
> **5.** Use object pools for enemies, projectiles, pickups and repeated VFX. Do not “temporarily” use Instantiate/Destroy in combat.
>
> **6.** Use data definitions for content. Adding a new Weapon/Relic/Enemy should not require editing unrelated switch statements.
>
> **7.** Keep deterministic random streams for map, loot and director. Log run seed to the run-end screen/debug console.
>
> **8.** Implement performance caps before high-quantity weapons are added.
>
> **9.** Compile and run automated tests after each system task; fix regressions before adding features.
>
> **10.** Use placeholder primitives/materials when art is missing. Do not block gameplay implementation on final assets.

## 25.2 Ordered Build Tasks

| **Task**                           | **Deliverable**                                                                                                                        |
|------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------|
| Task 0: Project setup              | Unity URP project, Input System, target settings, quality profiles, folder structure, Git ignore/LFS policy, Boot/MainMenu/Run scenes. |
| Task 1: Ship + camera              | PlayerShip kinematic controller, camera-relative movement, virtual joystick, island slide collision, fixed follow camera.              |
| Task 2: Combat primitives          | Health/DamageReceiver, EnemyWorld, enemy pool, spatial hash, one direct chaser, contact damage.                                        |
| Task 3: Projectiles + first weapon | Projectile pool/world, auto-target nearest, Deck Cannons, hit/kill flow.                                                               |
| Task 4: XP / Gold                  | Pickup pooling/merging, attraction, XP threshold, level system, Gold counter.                                                          |
| Task 5: Level-up UI                | Pause-safe three-card modal, weapon upgrade, new weapon slot, Logbook, rarity roll, reroll placeholder.                                |
| Task 6: Spawn director             | Threat budget, stage timer, compositions, elite scaffolding, performance caps.                                                         |
| Task 7: Build system               | Modifier stack, weapon-specific upgrade axes, 6 weapons, 8 Logbooks, statuses.                                                         |
| Task 8: Relics / triggers          | LootService, RelicDefinition, trigger event bus, 12 MVP Relics.                                                                        |
| Task 9: Procedural stage           | Grid generator, islands, reachability validation, spawn points, minimap.                                                               |
| Task 10: POIs / economy            | Chest price curve, Lighthouse, Trial, Boss Beacon.                                                                                     |
| Task 11: Boss / overtime           | Barnacle King, boss state, Dread Tide, win transition.                                                                                 |
| Task 12: Meta                      | Run payout, Doubloons, quest progress, Unlocks menu, save migration.                                                                   |
| Task 13: Platform optimization     | Quality auto-select, stress scene, projectile overflow, pickup merge, audio/VFX caps, WebGL compression.                               |
| Task 14: Polish                    | Tutorial, settings/accessibility, end-run stats, analytics hooks, bug burn.                                                            |

## 25.3 Master Prompt for an AI Coding Agent

```text
You are implementing Project High Seas from the attached GDD. Treat sections marked MUST / MUST NOT as hard constraints.

Core rule: the player is ALWAYS the ship. There is no on-foot pirate mode. Movement is simple camera-relative WASD / virtual joystick kinematic translation. Do not create wind, tacking, rudder, sailing momentum, boarding movement or manual aim.

Target Unity WebGL + Android using URP. Build for both targets after every milestone. Favor simple, data-driven, pooled systems over simulation-heavy components. The game must remain fun with placeholder assets.

Implementation workflow:
1. Read the current task and relevant GDD sections.
2. List files/classes to create or modify.
3. Implement the smallest working version.
4. Add EditMode/PlayMode tests where deterministic logic exists.
5. Run compile/tests.
6. Run the relevant scene and verify acceptance criteria.
7. Update IMPLEMENTATION_STATUS.md with completed requirements, known issues and next task.
8. Do not silently invent new mechanics. Put optional ideas in NOTES.md instead.

Performance rules: no combat Instantiate/Destroy, no NavMeshAgent swarm, no per-frame full enemy scans, no steady-state GC allocations in combat loops, and hard caps for enemies/projectiles/pickups based on quality profile.

When data is missing, use the DEFAULT numbers from the GDD. Keep all tunable values in ScriptableObjects/config rather than magic numbers.
```

## 25.4 Agent Definition of Done for Any Feature

- Feature is reachable in a normal run or test scene.

- No compile warnings introduced by the feature except explicitly documented platform warnings.

- WebGL and Android builds still succeed.

- Feature uses data/config rather than unexplained hard-coded constants.

- Feature respects pause state and run reset.

- Pooled objects return to a clean state.

- At least one automated test covers deterministic math/state logic when applicable.

- IMPLEMENTATION_STATUS.md is updated.

# Appendix A. Default Balance Tables

## A1. XP Curve

```text
XPToNext(level L) = round(12 + 5.5*L + 1.35*pow(L, 1.32))
XP gained = baseEnemyXP * XP_Gain_Multiplier
When multiple levels are crossed by one pickup, queue level-up modals sequentially.
```

| **Enemy class**  | **Base XP** | **Base Gold**      |
|------------------|-------------|--------------------|
| Cheap swarm      | 1           | 0–1                |
| Standard melee   | 2           | 1                  |
| Ranged / charger | 3           | 1–2                |
| Heavy            | 5           | 2–3                |
| Elite            | 12          | 8–15               |
| Miniboss         | 35          | 40–60              |
| Boss             | 100         | 150 + Relic reward |

## A2. Enemy Scaling

```text
enemyHP = baseHP * (1 + 0.09*minute + 0.018*minute^2) * TierHPMult * StageHPMult
enemyDamage = baseDamage * (1 + 0.055*minute) * TierDamageMult * StageDamageMult
moveSpeed = baseMoveSpeed * min(1.35, 1 + 0.012*minute)

TierHPMult = [1.00, 1.45, 2.05]
TierDamageMult = [1.00, 1.25, 1.60]
StageHPMult = [1.00, 1.22, 1.50]
StageDamageMult= [1.00, 1.12, 1.28]
```

## A3. Weapon Upgrade Package

| **Rarity** | **Stat multiplier** | **Visual treatment**  |
|------------|---------------------|-----------------------|
| Common     | 1.00x               | Green / plain         |
| Uncommon   | 1.25x               | Blue                  |
| Rare       | 1.55x               | Purple                |
| Epic       | 1.90x               | Orange/magenta        |
| Legendary  | 2.50x               | Gold + distinct sting |

For each WeaponDefinition, author 3–6 valid upgrade axes with base increments. When the weapon is selected on level-up, roll 1 axis at Common/Uncommon, 1–2 axes at Rare, 2 axes at Epic and 2–3 axes at Legendary. Shipwright’s Anvil modifies this package generation rather than directly multiplying final damage.

## A4. Starting Difficulty Defaults

| **Parameter**               | **Value**                                           |
|-----------------------------|-----------------------------------------------------|
| StageBaseThreat             | 2.6 threat/sec                                      |
| Spawn budget cap            | 3.5 seconds of current threat generation            |
| Elite base chance           | 2% per eligible spawn                               |
| Elite chance growth         | +0.8 percentage points per minute; cap 18% standard |
| Contact i-frame             | 0.20s                                               |
| Player pickup radius        | 2.25m                                               |
| Chest 1 price               | 25 Gold                                             |
| Lighthouse base charge      | 5.0s uninterrupted                                  |
| Trial duration              | 25s                                                 |
| Boss Beacon auto-highlight  | 60s remaining                                       |
| Dread Tide first multiplier | 1.50x threat                                        |
| Dread Tide growth           | +20% multiplicative threat per overtime minute      |

# Appendix B. Research Sources & Confidence Notes

Research was conducted against publicly available material current to August 2026. Community-maintained sources can lag patches or disagree; use them to understand system structure, not as authoritative source code or exact balance.

| **Source**                                          | **URL**                                                                                                      | **Use / confidence**                                                              |
|-----------------------------------------------------|--------------------------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------|
| \[S1\] Official Steam store — Megabonk              | https://store.steampowered.com/app/3405340/Megabonk/                                                         | High confidence for broad premise and official marketing counts.                  |
| \[S2\] Megabonk Wiki — Weapons                      | https://megabonk.wiki/wiki/Weapons                                                                           | Community source; useful for weapon categories, upgrade axes and unlock examples. |
| \[S3\] Megabonk Wiki — Tomes                        | https://megabonk.wiki/wiki/Tomes                                                                             | Community source; useful for passive-stat layer and repeated leveling.            |
| \[S4\] Megabonk Wiki — Items                        | https://megabonk.wiki/wiki/Items                                                                             | Community source; item acquisition, unlock-to-pool loop and synergy examples.     |
| \[S5\] Megabonk Wiki — World / Interactables        | https://megabonk.wiki/wiki/Interactables                                                                     | Community source; map/tier/stage structure, biomes, timer/final-swarm concepts.   |
| \[S6\] Megabonk Wiki — Charge Shrine                | https://megabonk.wiki/wiki/Charge_Shrine                                                                     | Community source; charge-in-zone POI and rarity-based stat rewards.               |
| \[S7\] Megabonk Wiki — Quests                       | https://megabonk.wiki/wiki/Quests                                                                            | Community source; variety of unlock objective patterns.                           |
| \[S8\] Megabonk Wiki — Challenges                   | https://megabonk.wiki/wiki/Challenges                                                                        | Community source; optional run modifiers tied to currency gain.                   |
| \[S9\] Megabonk Wiki — Chest                        | https://megabonk.wiki/wiki/Chest                                                                             | Community source; escalating run chest cost and free-chest concept.               |
| \[S10\] Pro Game Guides — Megabonk Beginner’s Guide | https://progameguides.com/megabonk/megabonk-beginners-guide-how-to-farm-silver-unlock-extra-slots/           | Secondary guide; useful inventory of POI types and run/meta currency behavior.    |
| \[S11\] Megabonk.org — Mechanics Guide              | https://megabonk.org/guides/mechanics/                                                                       | Unofficial current mechanics index, reviewed July 2026.                           |
| \[S12\] Megabonk.org — Characters                   | https://megabonk.org/guides/characters/                                                                      | Unofficial current character count/passives guide, reviewed Aug 2026.             |
| \[S13\] KeenGamer — Megabonk Beginner’s Guide       | https://www.keengamer.com/articles/guides/megabonk-beginners-guide-how-to-play-build-survive-your-first-run/ | Secondary source for temporary powerup categories and beginner loop observations. |

## B1. Patch-Dependent Uncertainties

- Official Steam marketing lists 20 characters; at least one current unofficial database reports 21 in v1.0.69. This document therefore avoids depending on the exact count.

- Community item totals vary around 77–78 depending on patch and database timing. The important design lesson is breadth and synergy density, not the precise number.

- Some wiki values were last edited in 2025 while newer fan databases were reviewed in 2026. Exact Megabonk tuning values should not be treated as current unless re-verified against the live build.

- Project High Seas formulas and content numbers are original defaults derived from design goals, not reverse-engineered Megabonk values.

| **Copyright / inspiration boundary —** Use Megabonk as a reference for genre structure and systemic lessons only. Project High Seas must ship with original names, art, audio, UI, text, maps, enemies, ships, weapon presentation and tuning. |
|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
