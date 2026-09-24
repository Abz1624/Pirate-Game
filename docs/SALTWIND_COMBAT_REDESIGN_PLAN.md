# Saltwind combat redesign: distinct enemies, expressive weapons, and verifiable behavior

Implementation and verification handoff for Cursor. Created 24 September 2026 from the agreed conversation plan. This document is a plan, not a claim that the changes have been implemented or tested.

## 1. Direction and scope

Redesign the current Saltwind roster around **readable action and mostly automatic offense**. Enemies should create different movement decisions; weapons should feel different through their firing rhythm, targeting, damage shape, and effects.

Agreed direction:

- Cover all 20 playtest enemies, Reef Crab, Barnacle King, both overtime enemies, and the two scheduled elite variants.
- Cover the six starter weapons and four Marks-unlocked weapons. Keep `grapeshot` and `boarding_party` disabled.
- Weapons automatically choose their attack orientation, including Broadside. **Powder Kegs retain their current trailing-mine behavior.**
- Preserve six weapon slots, starting Swivel Guns, existing unlock prices, progression, rewards, and the ten-minute overtime transition.
- Aim for similar overall difficulty. Stronger enemy abilities should be balanced through exposure, recovery, and specialist density.
- Prove mechanics using existing assets and procedural telegraphs. Include production art briefs, but do not block functional completion on bespoke assets.
- This plan supersedes conflicting Saltwind combat behavior descriptions in older documents. Other biomes and their bosses remain outside this implementation.

**Success criterion:** players can recognize an enemy's role from its movement and attack sequence, anticipate its threat, and explain how they avoided or exploited it. A different color, speed, or health total alone does not qualify.

### Findings that motivate the work

Source inspection found several concrete causes of the current feel:

- Ambient composition can select a new enemy definition while `SpawnProfileResolver` still derives movement and attacks from the original profile enemy.
- The profile ranged-attack path emits a single projectile for supported attack patterns instead of implementing their distinct burst, spread, and area behavior.
- Several specialist aliases only change stats and a generic behavior enum. Several listed ambient aliases are absent from actual ambient pools.
- Ranged movement can approach a stationary player inside its own minimum firing distance.
- Shared speed caps can suppress charge speeds; charges can also continue tracking the player during execution.
- Reef Crab currently repeats a bubble shot. Barnacle King's enraged selection repeatedly chooses spread/bombardment, dropping charge from the sequence.
- Weapon targeting adds a large boss preference to every weapon. Cooldowns and emission telemetry can advance even when nothing fires.
- Harpoon applies its control status at launch; the inspected status system does not translate that timer into movement slowing.

These are source findings. Unity was not playtested during planning because `unity` was unavailable on the planning shell's PATH. Recheck the environment at implementation time.

## 2. Runtime foundation and weapon redesign

### Establish one authoritative behavior path

Implement this before balancing individual enemies:

- Add an `EnemyCombatProfile` referenced by each resolved enemy definition. It owns movement role, attack sequence, timing, ranges, support settings, and presentation cues.
- Resolve behavior from the **actual spawned definition**, including runtime aliases and overtime IDs. Spawn profiles retain placement, scheduling, scaling, grouping, and explicit encounter overrides.
- Make encounter overrides opt-in. An ambient anchor must never silently replace a selected enemy's combat identity.
- Manual spawns and scheduled spawns use the same resolver. Retain legacy behavior only as a fallback for content outside the migrated roster.
- Register runtime definitions idempotently, preserve appropriate visual/template references, and avoid mutating shared authoring assets during play.

Use explicit per-agent states: **Approach, Reposition, Telegraph, Execute, Recover**, plus specialized support/flee states where needed. Simple fodder does not need every state.

The per-agent runtime state must hold:

- Stable unit ID, target ID, current state and timer.
- Attack sequence number/attack ID.
- Committed direction or marked position.
- Formation/support ownership and spawned hazard references.

Timers advance once per combat tick. Presentation reads this state; animation events do not authorize damage.

### Shared combat rules

- Telegraphs use the same position, radius, direction, and timing as damage.
- Charges warn for at least the existing one-second minimum; explosions for at least 1.2 seconds. Other aimed attacks warn for at least 0.8 seconds.
- Lock charge trajectories at telegraph start. Stop on terrain and enter recovery. Do not home or bend around islands after commitment.
- Apply state-specific speed limits so charge speed survives movement processing. Disable lateral separation steering during committed charges while retaining terrain collision.
- Use swept collision for fast attacks. One player hit per attack ID; a fan is one attack, not several overlapping full-damage hits.
- Disable ordinary contact damage during a special attack's telegraph and recovery. During execution, avoid applying both contact and special damage for the same hit.
- Ranged enemies reposition into a useful firing band, including when the player is stationary. Blocked line of sight causes repositioning.
- Queued attacks cancel on death. Already emitted projectiles finish their existing lifetime. Persistent support effects end with their owner; hazards follow their declared lifetime.
- Clear all state on death, despawn, pool reuse, scene changes, and run reset.

Reuse the existing damage/proc pipeline. Add optional attack context—source unit, attack ID, origin/direction, and damage kind—so guard direction, damage attribution, and duplicate-hit prevention work consistently.

### Weapon reliability fixes

- Make firing return a result distinguishing no target, successful emission, and unavailable projectile capacity.
- A ready weapon stays ready when no eligible target exists. Record emissions only when an attack actually starts.
- Apply attack-speed modifiers once; verify current timer decrement and cooldown calculation do not compound the same modifier.
- Move gameplay damage, collision dimensions, and burst timing out of visual-effects settings.
- Remove universal boss priority. Give each weapon its own target policy with stable unit-ID tie-breaking.
- Use actual projectile speed for predictive aiming. Instant beams aim at current target geometry.
- Stop direct projectiles and beams at terrain. Mortar may arc over terrain but must land in valid combat space.
- Apply Harpoon effects on confirmed impact, using a stable target identity rather than a reusable array index.

### Six starter identities

Keep current damage/range values as the initial balance baseline. Change targeting and behavior first, then tune from measured results.

- **Swivel Guns — dependable personal defense.** Keep the short two-shot burst. Auto-target nearby enemies in all directions, preferring threats within six meters before more distant targets. Reacquire between shots if the target dies. Small rounds and light impacts distinguish it from heavy artillery.
- **Deck Cannons — reliable heavy shots.** Auto-aim 360° at the nearest valid enemy. Fire heavy projectiles with predictive aim and a brief, bounded hit reaction on ordinary enemies. Give each volley a clear firing pause; avoid matching Swivel's rapid cadence.
- **Broadside Battery — automatic crowd-clearing salvo.** Replace hull-relative target restrictions with selection of the best enemy cluster anywhere around the ship. Orient a three-shot, 50° fan toward that cluster. Use real damaging projectiles instead of immediate ray damage followed by zero-damage visuals. Animate the corresponding battery firing toward the selected direction.
- **Powder Kegs — route-based traps.** Preserve current aft deployment, arming, triggering, and explosion behavior. Improve armed/unarmed readability and telemetry only. Do not add auto-targeted placement.
- **Flying Cutlasses — close-range space control.** Keep orbiting blades, but make damage correspond to blade sweeps and a per-target hit interval. Add a small ordinary-enemy knockback on a valid hit, respecting elite/boss resistance. Avoid continuous damage merely because an enemy is somewhere inside the orbit radius.
- **Long Nine — precision lane piercing.** Auto-select the highest-current-health target within range, with distance then unit ID as tie-breakers. Orient a narrow piercing beam toward it regardless of ship heading. Keep a strong report, visible lane, and long recovery; no predictive lead for the instantaneous hit.

### Four unlocked identities

- **Lightning Mast — dispersed-group damage.** Auto-target the nearest enemy and chain to distinct nearby targets. Never hit one target twice in a chain. Quantity increases chain coverage rather than repeating single-target hits.
- **Flamethrower — sustained crowd pressure.** Select the densest nearby cone in any direction. Turn the nozzle toward it at a bounded rate and hold that sector for each stream. Keep stream/recharge rhythm and existing burn rules; the damage cone follows nozzle orientation.
- **Mortar — delayed cluster clearing.** Target the densest cluster, show the landing area, then strike after 0.9 seconds. Lock the landing point when the marker appears. Additional shells select distinct available clusters before sharing a cluster.
- **Harpoon — single-target control.** Auto-aim at the nearest mobile enemy. On impact, apply 25% slow for 1.2 seconds; strongest slow wins. Bosses receive half strength, respecting the existing boss cap. Show a temporary tether, but introduce no forced dragging or hard root.

Update descriptions and targeting overlays to match actual behavior. Preserve existing upgrade axes and evolution ownership; adapt inherited targeting to the new auto-orientation rules.

## 3. Enemy and boss personality specifications

The following timings are initial implementation values. Retain existing base health and damage initially; do not transplant the older document's unrelated stat scale.

### Ordinary enemies

- **`deckhand_dinghy` — reckless amateur.** Travel in loose groups of three, wobbling slightly along their approach. Close directly and deal ordinary contact damage. On contact, recoil and reapproach after 0.8 seconds. This is readable, fragile fodder rather than another precision attacker.
- **`shark` — relentless hunter.** Use smooth pursuit with gentle lead prediction and separation. Accelerate modestly at close range, without orbiting or a special dash. After contact, peel away briefly before reapproaching. Its pressure is persistence.
- **`swordfish` — nervous duelist.** Approach to ten meters, hover laterally, show a one-second narrow line, dash along that fixed line at 12 m/s for 0.65 seconds, then retreat/recover for 1.2 seconds. One dash hit maximum. Its vulnerability is the missed pass.
- **`cutthroat_skiff` — opportunistic flanker.** Approach a rear-quarter position relative to player travel, commit to a short crossing pass, then sail out to reset. Show a 0.8-second preparation before the close pass. Do not continuously curve inward like a generic chaser.
- **`hammerhead` — territorial bruiser.** Move slowly to intercept a route ahead of the player. Telegraph a wide lane for 1.2 seconds, charge at 10 m/s for one second, then recover for two seconds. Wider attack and longer commitment distinguish it from Swordfish. Terrain ends the charge.
- **`cannonboat` — methodical gunner.** Seek a 12–16-meter firing band, show a 0.8-second aiming line, fire one straight projectile, then relocate laterally during a two-second reload. Its movement alternates between firing position and repositioning.
- **`manta_ray` — sweeping dancer.** Glide laterally around a ten-meter radius. Telegraph a fan for 0.9 seconds, fire three projectiles with 18° spacing, then reverse its orbit direction during recovery. Preserve visible gaps; never silently fire backward.

### Aggressive specialists

- **`needle_sloop` — cowardly sniper.** Keep 16–20 meters away, aim a thin line for one second, fire one fast shot, then retreat diagonally. Getting within eight meters forces an escape maneuver before its next attack.
- **`rammer_raft` — clumsy battering ram.** Slowly align, warn for 1.4 seconds, then perform one long straight charge. Recover for 2.2 seconds with a pronounced wobble. It has poor turning and no corrective second pass.
- **`ram_raider` — calculating jouster.** Circle to a flank before its one-second charge warning. After passing, disengage and perform a separately telegraphed second pass; then recover for two seconds. Never turn instantly into the second attack.
- **`bomb_boat` — panicked kamikaze.** Rush toward the player. At four meters, stop steering, light a 1.2-second fuse, and show a three-meter blast circle. Killing it before detonation defuses it and uses normal kill credit. Self-detonation grants no farming rewards.
- **`bomb_ketch` — distant bombardier.** Keep 14–18 meters away. Every four seconds, mark one three-meter landing circle near the player's predicted route; detonate after 1.2 seconds. It repositions while reloading and never becomes a contact rusher.
- **`powder_barge` — stubborn mine-layer.** Slowly cross the player's likely route, dropping at most three stationary mines, one every three seconds. Each mine visibly arms for 1.2 seconds and expires after six seconds. Killing the barge disarms its mines. No additional death explosion.

### Control, support, and formation specialists

- **`boarding_barge` — overconfident grappler.** Approach to six–nine meters, warn for one second, then launch a visible rope projectile. A hit applies 20% slow for 1.5 seconds; distance above 12 meters or terrain blockage breaks it. No root, pull, or control lock.
- **`net_cutter` — route trapper.** Move ahead and sideways relative to player travel. Mark a six-meter net segment for 1.2 seconds; leave it active for three seconds. Crossing causes a one-second 20% slow. One active segment per cutter.
- **`repair_tender` — evasive medic.** Seek injured nearby allies rather than the player. Every four seconds, telegraph a one-second repair pulse, healing up to three ordinary non-support allies by 3% maximum health within eight meters. Taking damage interrupts that pulse. Never heal itself, other tenders, elites, or bosses; flee when no valid ally exists.
- **`shield_frigate` — protective escort.** Stay between the player and a nearby ranged/support ally. A visible forward shield reduces direct-hit damage by 40% across a 120° arc. Turn at no more than 90°/second. Every five seconds lower the shield for 1.5 seconds while firing a clearly telegraphed shot. All weapons remain effective; flanking and open windows improve damage.
- **`gunner_cog` — disciplined battery.** Hold a formation offset with up to two other cogs. Telegraph and fire a three-shot burst along a committed direction, 0.2 seconds between shots. Stagger members' firing windows. Losing the leader causes survivors to break formation and reposition; a lone cog uses the same burst independently.
- **`treasure_raider` — greedy coward.** Cross the play area toward a navigable escape point, avoiding close approaches. Damage triggers a brief speed burst with a three-second cooldown. Never orbit forever at the boundary. Preserve existing encounter rewards and escape handling; do not steal persistent currency.
- **`kraken_spawn` — patient territory controller.** Move slowly toward an intercept position, then anchor. Every six seconds, mark two 1.5-meter tentacle circles with 1.2-second warnings; strikes last 0.5 seconds. Move between attack cycles. No hard grab and no attack placement that seals every escape route.

### Scheduled elites

- **Elite Hammerhead, approximately 3:00 — dominant charger.** Use the normal heavy charge followed by one freshly telegraphed second charge, then a three-second recovery. Retain scheduled elite scaling and rewards; do not also apply natural-elite scaling.
- **Elite Kraken Spawn, approximately 8:00 — deliberate trap setter.** Telegraph three circles sequentially, 0.35 seconds apart, each with its full warning. Alternate left/right route pressure between cycles. All circles participate in the shared hazard budget.

### Reef Crab miniboss — defensive bully

Replace the repeating bubble-shot loop with three readable behaviors:

1. **Side-step and guard:** move laterally at five–eight meters, keeping its claws toward the player. Apply 30% frontal direct-hit reduction.
2. **Claw slam:** when the player is close, show a four-meter circle for 1.2 seconds, slam once, then drop the guard for two seconds.
3. **Bubble fan:** when the player stays outside slam range, warn for one second and fire three slow bubbles, then reposition.

At 50% health, add a second alternating claw slam with its own full telegraph. Finish the current attack before changing phase. Never shorten warnings to create difficulty.

### Barnacle King — arrogant ruler becoming desperate

Evolve the existing spread, charge, and bombardment instead of adding another summon system.

Use health phases at 65% and 30%, applied only after the current attack/recovery completes:

- **Phase 1:** Volley → Shell Slam → Volley → Bombardment.
- **Phase 2:** Volley → Bombardment → Charge → Shell Slam.
- **Phase 3:** Charge → Volley → Bombardment → Shell Slam; Charge gains a separately warned second pass.

Attack definitions:

- **Volley:** 1.2-second visible fan; five shells spaced 15° apart. Display actual projectile lanes.
- **Shell Slam:** 1.2-second warning, five-meter radius, followed by two seconds of exposed recovery.
- **Charge:** 1.5-second corridor warning, committed movement at the existing boss charge multiplier for 0.8 seconds, ending on terrain.
- **Bombardment:** two three-meter circles, marked 0.5 seconds apart; each receives 1.5 seconds before detonation. Cap damage to one hit per bombardment sequence.

Use existing boss base damage and current attack multipliers as the first tuning baseline. Recovery is two seconds, or 2.5 seconds after bombardment. Phase 3 can reduce ordinary recovery to 1.5 seconds; exposed slam recovery stays two seconds.

Give the boss slow, purposeful pursuit between attacks and visible exhaustion after slam/charge. Replace text-only attack warnings with world-space geometry. Preserve objective credit, double-death handling, continued play after victory, and overtime behavior.

### Overtime personalities

- **`drowned_pursuer` — inexorable hunter.** Maintain pursuit with an intercept reposition every three seconds. Cross other ordinary enemies without being slowed by separation during that reposition, but remain damageable and terrain-bound. Avoid teleportation and unannounced dashes.
- **`drowned_harrier` — escape-route punisher.** Keep a ranged flank position and fire a two-shot burst at a committed predicted crossing point after a one-second warning. Relocate during reload.

Keep existing overtime escalation. Greater speed must not reduce minimum warnings.

## 4. Encounter pacing, presentation, and implementation stages

### Make variety appear in actual runs

Preserve existing population caps, reserved encounter slots, schedule times, and boss ambient reduction. Keep the existing five-band ordinary/specialist proportions initially.

Update eligible pools:

- **0–2 minutes:** Swordfish and Dinghy teach movement; retain current opening constraints.
- **2–4:** introduce Skiff, Hammerhead, Needle Sloop, and Ram Raider.
- **4–6:** add Shark, Cannonboat, Bomb Boat, Rammer Raft, Bomb Ketch, and Gunner Cog.
- **6–8:** add Manta, Boarding Barge, Net Cutter, Repair Tender, and Kraken Spawn.
- **8–10:** add Shield Frigate and Powder Barge while retaining prior families.
- **Treasure Raider:** keep as a deliberate route encounter rather than ordinary ambient filler.
- **10+:** preserve the overtime-only roster transition.

Respect time/level eligibility; classify new support/control aliases explicitly rather than letting them inherit the default fodder tier.

Add role-aware selection and attack limits:

- Maximum two simultaneous committed charge sequences.
- Maximum two active area-denial sequences, counting elite and boss sequences.
- Maximum one active player-slow source.
- Maximum one Repair Tender and one Shield Frigate.
- Maximum six living specialists; retain stricter existing family caps.
- Down-weight a family after two consecutive selections; use the run RNG.
- If no eligible candidate exists, skip rather than spawning an uncapped anchor fallback.

Reserve attack capacity before starting a telegraph. While waiting, enemies reposition instead of freezing. Use a rotating queue so the same actors do not monopolize attacks.

Validate hazardous geometry against nearby terrain and existing committed hazards. Require at least one navigable escape corridor wide enough for the player hull plus margin. If validation fails, reposition or defer; never emit unannounced substitute damage.

### Presentation and art briefs

Provide each enemy with a short asset brief containing silhouette, locomotion, preparation, execution, recovery, hit reaction, death, and sound cues.

Prioritize:

- Swordfish: compressed preparation, needle dash, retreat flick.
- Hammerhead: broad head brace, heavy straight rush, stunned recovery.
- Manta: banking glide, wing spread before firing.
- Crab: lateral steps, raised claw, exposed shell after slam.
- Support ships: repair beacon, visible rope/net equipment, rotating shield plate.
- Barnacle King: deliberate approach, shell inflation before volley, planted slam, exhausted recovery.

Use procedural transforms and existing effects for the functional pass. Reuse only approved assets; do not import from `references/**`.

Telegraphs must remain visible on water, at normal gameplay zoom, and in reduced-effects mode. Use shape, motion, and timing as well as color. Friendly attack effects must not obscure enemy warnings.

### Delivery stages and gates

1. **Baseline and resolver repair.** Capture existing behavior, reproduce spawn-path mismatches, implement resolved enemy identity, and prove manual/scheduled parity.
2. **Weapon reliability and auto-orientation.** Implement targeting policies, actual-emission cooldowns, damage/presentation separation, and all ten weapon identities.
3. **Ordinary enemies and offensive specialists.** Implement distinct movement/attack cycles and their single-enemy tests.
4. **Support, control, elites, bosses, and overtime.** Add shared hazard/slow/guard rules, role caps, and encounter composition.
5. **Integrated balance and polish.** Tune difficulty, complete readability checks, produce art briefs, and update documentation.

Each stage must compile and pass its relevant tests before the next begins.

Keep changes enabled through the Saltwind combat profile and its playtest harness. Shared correctness fixes must preserve other modes. Increment the combat balance version, update exports/hashes and relevant snapshot compatibility, and synchronize authoring/runtime database copies through Editor tooling.

Extend the existing simulation/export layer with the new combat parameters. Where the balance lab cannot reproduce spatial behavior, explicitly label its results as numerical balance evidence, not proof of movement or dodge fairness.

## 5. Verification and Cursor completion contract

### Extend the existing playtest scene

Retain current hotkeys and add an on-screen test panel with:

- All ten enabled weapons and all roster enemies, including elites and overtime.
- Weapon level/build presets.
- Single enemy, group, formation, and support-pair scenarios.
- Stationary, straight-line, circular, reversing, and obstacle-adjacent player scenarios.
- Invulnerability, weapons-off, enemy health lock, and normal-damage toggles.
- Identical manual-versus-scheduled spawn comparison.
- Current state, target, committed geometry, next attack, and block/defer reason.
- Seeded reset without changing the player's real save.

Boss testing must initialize the required combat context; merely spawning a boss model is insufficient.

### Automated tests

**Resolver and lifecycle**

- Every roster ID resolves to its intended behavior through manual, ambient, scheduled, and overtime paths.
- Selected ambient enemies do not inherit anchor behavior.
- Catalog application twice creates no duplicate definitions or changed identities.
- Death, despawn, pool reuse, pause, and scene reset leave no stale attacks, shields, healing, statuses, or telegraphs.

**Enemy behavior**

- Correct state order, warning duration, commitment, and recovery at 30 and 60 FPS.
- No homing after commitment; terrain ends charges.
- No windup damage or contact-plus-special double hit.
- Ranged enemies engage stationary targets and recover from being too close or blocked.
- Supports choose valid allies, obey caps, and terminate effects correctly.
- Formations handle missing/dead leaders.
- Escape behavior terminates correctly.
- Attack-budget and escape-corridor rejection defer damage safely.

**Weapons**

- Eligible targets at front, side, and rear are attacked by every auto-oriented weapon.
- Powder Kegs retain aft placement.
- No target consumes no shot cooldown or emission count.
- Attack-speed scaling is applied once.
- Swivel protects against nearby threats even with a boss present.
- Broadside damages through projectile collision once.
- Long Nine matches its visible lane and terrain occlusion.
- Cutlasses obey sweep geometry and hit intervals.
- Lightning uses distinct targets; Mortar honors its locked marker.
- Harpoon slows only after impact and respects resistance.
- Upgrade Quantity, Speed, Size, and Duration affect their intended mechanics.

**Integration**

- Spawn eligibility, role caps, scheduled elites, cache ambush, boss activation, and overtime work in the real Saltwind scene.
- Boss thresholds do not cancel warnings.
- Boss victory, same-tick double death, one-time rewards, and continued play remain correct.
- Existing progression, save, scene lifecycle, and multi-ship smoke tests remain passing.

Run Unity EditMode and PlayMode suites through the available Editor/CLI workflow. If the CLI remains unavailable, use the Unity Test Runner and export results. Never report tests as passed without execution.

### Measured playtest acceptance

Use fixed seeds `101`, `202`, and `303` with starter-only, mixed three-weapon, and six-weapon builds.

For each hostile archetype:

- Record at least three completed signature cycles with weapons disabled.
- Show its attack threatening an intentionally unresponsive target.
- Show its intended movement response avoiding damage in five repeat attempts from the same test setup.
- Show a missed attack producing the specified recovery opportunity.
- Test in open water, beside an island, and in a mixed encounter.

Supports instead demonstrate a measurable ally benefit and the player action that removes it.

Compare matched baseline/redesign runs using damage taken per minute, survival duration, specialist density, weapon contribution, and boss duration. Use a provisional ±15% band for damage pressure and survival duration; investigate exceptions before tuning. Do not preserve a broken baseline behavior merely to hit that band.

Log attack starts, executions, hits, misses, interruptions, deferred reasons, support uptime, weapon targetless time, actual emissions, and damage contribution. Aggregate in normal runs; detailed traces belong in the harness.

Profile the existing 80-enemy cap with six weapons, a boss, and the maximum allowed hazards. Require no steady-state allocations in new per-agent behavior logic after warmup. Target 30 FPS on the selected mobile/browser reference device; record actual hardware and measured frame times. Desktop results alone do not establish mobile readiness.

### Final handoff evidence

Cursor must deliver:

- Implemented changes with the combat balance version.
- Updated roster and combat documentation that match runtime behavior and spawn eligibility.
- EditMode/PlayMode result files and remaining failures.
- Short clips of every enemy signature and weapon role.
- Seed/build information and before/after balance measurements.
- Performance captures and unresolved asset briefs.
- A checklist separating **implemented**, **automatically verified**, **visually verified**, and **still requiring player feedback**.

Mechanical correctness is an automated gate. "Distinct and enjoyable" requires observed play and player feedback; neither compilation nor a damage increase establishes it.
