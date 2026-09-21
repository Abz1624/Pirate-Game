# Saltwind — Cursor implementation and Codex verification handoff

Prepared: 2026-09-22. Status: **implementation instructions only; no gameplay changes made by this handoff.**

## 1. Start here

Implement this document in numbered order. It expands the owner-approved Saltwind plan into reviewable increments. Do not replace the game architecture or rebuild scenes wholesale. Reuse compatible existing systems and assets.

The owner will use Codex for independent verification after Cursor implements the work. Leave reproducible evidence, not just completion claims. Keep each step's code, tests, and evidence together in a small commit when practical; do not commit unrelated changes.

### Instructions to paste into Cursor

> Read AGENTS.md and plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md. Implement the numbered steps in order, using their checkpoints as gates. Preserve ship handling, camera, unrelated modes, existing assets, and user changes. Use the connected Unity Editor for scene/asset authoring where possible. Start by recording the baseline and resolving the Unity toolchain. Maintain plan/saltwind-verification/IMPLEMENTATION_LOG.md with completed steps, commands, outcomes, and pending checks. Do not describe simulation results as human playtests, and do not claim balance targets are achieved without measurements. Deliver the implementation, tests, versioned tuning assets, evidence, and the final verification index for Codex. Do not deploy or implement commerce, raids, or multiplayer changes.

### Authority and fixed decisions

- This approved plan supersedes older documents where they conflict about this solo stage.
- Target the normal solo `V3SaltwindRun` flow. Other scenes/modes retain their existing rules unless a shared correctness fix is necessary and covered by regression tests.
- Preserve scene-authored ship movement, input mapping, camera, and starting hull. Do not silently change them to hit difficulty targets.
- Start with one weapon; six weapon slots, two Logbook slots, three level-up choices, and existing weapon level caps.
- Players should have opportunities to reach 4–6 weapons by minutes 4–6, then specialize. Offers do not force acceptance.
- Boss death secures victory; combat continues until player death. A boss killed after 10:00 still counts. No extraction or automatic finish on boss victory.
- Drowned Pursuit starts at 10:00 irrespective of boss state. Typical successful sessions should last 12–15 minutes; this is a measured target, not a forced kill timer.
- Run gold funds the current build and expires at run end. Persistent Marks buy options, not permanent damage/hull upgrades.
- Exploration rewards are finite and placed at stage start. Discovering them does not create them.
- Primary tuning audience: PC players learning the game, with first boss victory targeted around attempts 3–5.
- Preserve ownership and treasury balances on old saves. No reference-game art or binaries should be imported into production.

### What constitutes completion

There are three distinct gates:

1. **Correctness:** deterministic formula fixtures, lifecycle tests, scene integration, save compatibility, no duplicate rewards, no false boss victory.
2. **Calibration:** measured pacing/economy/build results from the current runtime and a calibrated simulation model.
3. **Experience:** human playtests establish readable combat, understandable deaths, worthwhile exploration, and retry interest.

Cursor may complete implementation before human testers are available. In that case mark the experience gate **pending**, never passed. Codex should independently verify the implementation and evidence; it cannot manufacture missing human evidence.

## 2. Repository findings and source map

These were observed during planning. Recheck them against the implementation baseline before editing.

- `EncounterSpawner` is the current spawn system. Older balance documentation still describes an obsolete director.
- The Resources encounter schedule contains one ambient entry and empty boss, ambush, pressure, and overtime references. The inspected solo scene has no explicit schedule assignment and therefore uses the Resources fallback.
- `POIManager` can infer boss victory from the absence of live bosses after a summon request. Failed/queued spawns must not satisfy victory.
- Chests currently become visible through gold/progress-triggered spawning. This conflicts with the approved exploration rule.
- `PickupWorld` applies hidden gold scaling. `RunManager` banks unspent gold into doubloons.
- `V3DiscoverySystem` currently uses later beacon hints than the approved plan.
- `V3ContentUnlocks` and playtest flags do not yet represent the approved six-weapon starter pool and Marks unlocks.
- Balance Lab includes abstractions based on old director/wave behavior. Its existing output is not proof of the new runtime's balance.
- The Unity CLI was not on the inspected shell PATH. Unity 6000.3.11f1 is installed under `C:/Program Files/Unity/Hub/Editor/`.
- `tools/run-highseas-tests.bat` invokes `HighSeasBootstrap.BootstrapAll` before tests. **Do not use it as the verification runner for this task.** That setup operation is broader than a test run.

### Read first

- `AGENTS.md` and `Rogue_Pirates_Cursor_Implementation.md` for repository rules; explicit owner decisions above take precedence.
- `references/Megabonk Deepdive/replication/stage_run_loops.json`, `stage_run_events.csv`, `progression_curves.json`, and the economy directory for source evidence.
- `references/It's About Pirates Deepdive/analysis/extracted/enemies.jsonl` and its documentation for enemy concepts and extraction confidence.
- `docs/GAME_BALANCE_AND_PROGRESSION.md`, `docs/BALANCE_LAB_README.md`, and existing tests as historical context to reconcile with code.

The pirate extraction contains incomplete/malformed records. Do not manufacture missing enemy stats or treat enum IDs as tuning. Preserve provenance when deriving a value.

### Primary implementation surfaces

Paths below are relative to `Pirate Game Unity/Assets/_Game/`.

- Orchestration: `Scripts/V3/V3RunBootstrap.cs`, `Scripts/Core/RunManager.cs`, V3 session/context classes.
- Spawning: `Scripts/Enemies/EncounterSpawner.cs`, `EncounterSpawnSchedule.cs`, `SpawnProfile.cs`, `SpawnProfileModules.cs`, `SpawnPlacementService.cs`, and `EnemyWorld.cs`.
- Events: V3 pressure, overtime, route-event, discovery, and POI placement classes; `Scripts/POI/POIManager.cs`.
- Builds: `Scripts/UI/LevelSystem.cs`, `LevelOfferGenerator.cs`, `Scripts/Combat/CombatWeaponCatalog.cs`, evolution/proc systems, V3 item/unlock catalogues.
- Economy/presentation: pickup systems, V3 economy/powerup/results/menu classes, `Scripts/Save/SaveService.cs`, `RunSnapshot.cs`, combat telemetry.
- Calibration: `Simulation/Balance/`, `Scripts/Editor/HighSeasBalanceLabWindow.cs`, EditMode and PlayMode test assemblies.

## 3. Starting configuration and tuning policy

Author these values in the solo profile. Do not scatter independent copies across runtime, UI, and lab code.

### Mandatory starting values

- Grace: 2 seconds. Stage deadline: 600 seconds. Ambient rate bands: 0.6, 1.2, 1.8, 2.4, 3.0 enemies/second, each lasting 120 seconds.
- Hard population cap: 80 including bosses/encounters. Boss ambient multiplier: 0.5.
- Scheduled elites: 180 and 480 seconds. Swarms: 240 and 420 seconds, lasting 30 seconds. Warnings: 5 seconds before events.
- Cannonboats: at most 4 simultaneously; area-denial enemies: at most 2; simultaneous charge windups: at most 2.
- Charge warning: at least 1 second. Explosive/area warning: at least 1.2 seconds. Adjust upward if unchanged handling cannot avoid the attack.
- Overtime initial interval: 1.4 seconds; pressure increases every 30 seconds; enemy strength increases each overtime minute. Storm begins at 720 seconds.
- Chest count: 12. One each of Repair Wreck, Powder Cache, Signal Buoy, Treasure Chart, and Boss Beacon.
- Chest price for `k` completed purchases: 20 when `k == 0`; otherwise `ceil(25 * 1.22^k)`. No additional first-chest discount. First five prices: 20, 31, 38, 46, 56.
- Normal enemy: 50% chance of 1 gold. Specialist: 2 gold guaranteed. Scheduled elite: 25 gold guaranteed. These classifications are exclusive; do not add all three rewards to an elite.
- Repair Wreck: 25% maximum hull. Powder Cache: one eligible item plus 20 gold. Signal Buoy: 8 seconds of cumulative occupancy and one existing run bonus.
- Beacon direction hint: 300 seconds; exact marker: 420 seconds. Local discovery always works earlier.
- Random powerup chance: 1% per eligible kill, with a 20-second global minimum interval. Scheduled elites guarantee one powerup.
- Repair Supplies: 20% maximum hull. Powder Rush: +30% attack speed for 8 seconds. Magnetic Tide: collect existing loose XP and gold. Dead Calm: 30% ordinary-enemy slow for 5 seconds.
- Starter weapons: `deck_cannons`, `broadside_battery`, `powder_kegs`, `flying_cutlasses`, `long_nine`, `swivel_guns`.
- Weapon unlocks: `lightning_mast` 60 Marks; `flamethrower` 80; `mortar` 100; `harpoon_gun` 100. Preserve the existing oil/flamethrower alias without duplicate unlocks.
- Captain unlocks: Brine 100 Marks, Flint 140. Rook remains available.
- Marks: 2 per completed pre-overtime minute with at least 10 kills; 3 per completed special POI; 5 per scheduled elite; 25 once for boss victory; 3 per completed overtime minute, maximum 15 overtime Marks.

### Explicit implementation defaults where the short plan left a detail open

These are initial engineering/tuning defaults, not extracted reference-game facts. Include them in the profile and calibration log.

- Reserve 8 of the 80 enemy slots for required encounters; ordinary ambient spawning stops at 72. Required spawns never exceed 80 or delete living enemies to make room.
- Swarm intensity: 1.25 times the current ambient rate, using ordinary enemies; not a second unrestricted spawning loop.
- Spawn retry interval: 0.35 seconds; required encounter timeout: 10 seconds of active gameplay. Failure restores the relevant POI to a retryable state and grants nothing.
- Scheduled boss-conflicting events are skipped at their scheduled time, recorded in telemetry, and do not reappear after the boss. Already-live enemies remain.
- Overtime interval: `max(0.35, 1.4 * 0.85^floor(overtimeSeconds / 30))`. Strength multipliers at spawn: HP `2^(0.5 * floor(overtimeSeconds / 60))`, damage `1 + 0.12 * floor(overtimeSeconds / 60)`, on top of the single documented ordinary scaling application.
- Pursuer/Harrier selection: 80/20 during the first two overtime minutes, then 60/40. Retain the global cap and spawn safety rules.
- Storm warning starts 10 seconds before contraction. Contract a validated safe region from 100% at minute 12 toward 40% at minute 15; hold that minimum afterward. Initial outside damage: 8 hull/second, using elapsed active time rather than frame count. Enemies continue escalating after minute 15.
- Ordinary gold uses the reward RNG stream, not visual randomness. Ambient enemies flagged as elite do not automatically get the scheduled-elite Marks reward.
- Special-POI Marks apply once each to Repair Wreck, Powder Cache, Signal Buoy, and Treasure Chart; not salvage chests or the boss beacon.
- Completed minute buckets are half-open intervals: `[0,60)`, `[60,120)`, etc. An incomplete bucket at death does not pay. Overtime buckets also require at least 10 kills, preventing passive waiting from earning Marks.
- Guaranteed elite powerups bypass random chance and the minimum interval, then reset the random-drop interval. Timed pickups refresh to their normal duration; they neither multiply nor add durations. Repair clamps at maximum hull; boss enemies are excluded from Dead Calm.
- Marks unlock purchases are available in the menu. No new reroll economy, permanent-stat shop, paid revive, or additional currencies.

### Calibration targets, not hard guarantees

- First kill: within 10 seconds; first level-up: 15–25 seconds; first affordable chest: 30–60 seconds.
- Levels: 6–8 at minute 2, 14–18 at minute 5, 30–36 at minute 10 for representative successful routes.
- Arsenal: opportunities for 4–6 weapons at minutes 4–6. A focused build can reasonably reach one evolution before overtime.
- Ordinary enemies: 1–3 suitable hits. Specialists: 3–6 seconds of focus. Scheduled elites: 15–25 seconds.
- Boss: 45–75 seconds against a representative minute-7–9 build.
- Purchased chests: 6–9 by minute 10 for an average successful exploration route.
- Typical successful session: 12–15 minutes. First boss victory for attentive newcomers: approximately attempts 3–5.

When measurements miss a target, first fix runtime/lab discrepancies and correctness failures. Then change the smallest related profile values, preserve the before/after evidence, and increment balance version. Never change protected handling/camera settings or secretly weaken enemies based on current player DPS.

## 4. Step-by-step implementation

### Step 00 — Establish baseline and verification tooling

**Do**

1. Read applicable instructions; record branch, commit, `git status`, Unity version, package versions, and enabled build scenes.
2. Record the current solo player movement and camera serialized values, starting hull, and the baseline behavior of other modes. Store a machine-comparable capture rather than relying on screenshots alone.
3. Locate the Unity CLI installation; use its full path if only PATH is missing. Check `unity status --json`, `unity pipeline list`, and editor readiness. If unavailable, use the installed Editor for compilation/tests; do not claim a connection exists.
4. Run the existing EditMode and PlayMode suites without running project bootstrap. Record pre-existing failures separately.
5. Create the evidence directory and implementation log. Save captures outside production Assets unless they are test fixtures.

**Test / checkpoint C00**

- Baseline test results and editor logs exist, including failures.
- The normal boot/menu/solo scene route is identified.
- There is a reproducible test command and a record of all protected values.
- Stop dependent editor verification if Unity cannot compile; fix or document the actual cause before claiming later gates.

### Step 01 — Introduce the solo balance profile and shared data contract

**Do**

1. Add `StageBalanceProfile` as an editor-authored asset with a stable profile ID, schema version, balance version, and references to content/schedule definitions.
2. Put the approved constants, rate bands, reward definitions, discovery times, and overtime settings in this profile.
3. Export a plain serializable representation usable by Balance Lab without Unity runtime objects. Include content IDs and a content hash.
4. Resolve this profile explicitly for solo Saltwind. Do not use a global scene-name substring or a flag that accidentally enables it in co-op/PvP.
5. Instantiate per-run mutable state. Do not mutate shared ScriptableObjects or permanently retune the database through gameplay.
6. Add validation for missing content, duplicate IDs, invalid rates/durations, negative rewards, incompatible encounter types, and unresolved profile references. Missing required configuration must stop run start with a useful development error.

**Test / checkpoint C01**

- Export round trip preserves all gameplay values and IDs.
- Same profile produces the same hash; changed gameplay data changes it.
- Solo receives the profile; other modes do not.
- Two runs do not share mutable runtime profile state.
- Runtime and lab formula fixtures consume the same exported data.

### Step 02 — Make spawn scheduling correct before adding content

**Do**

1. Replace implicit counters with explicit schedule state: scheduled start, active window, next attempt, actual spawned count, loop cycle, and completion.
2. Implement schedule-entry `loop` and loop interval semantics. Anchor each loop to its scheduled start; skip expired cycles after a large time advance rather than catching up in a burst.
3. Use gameplay elapsed time for duration. A density multiplier changes cadence, not how quickly a profile's lifetime expires.
4. Return actual successful spawn counts. A 10-enemy request with capacity for 3 must report 3, not 10.
5. Validate every final formation position for camera exclusion, navigable water, collision clearance, and map bounds after offsets are applied.
6. Apply global and per-profile caps to every spawn path, including triggered bursts and overtime.
7. Add named spawn modifiers with multiplicative composition and independent expiry/reset. Preserve one compatibility wrapper for existing callers where necessary.
8. Enforce the ambient rate as a total budget across composition, not the full rate independently for every enemy type. Bound budget accumulation while blocked.

**Tests**

- Finite, continuous, burst, and looping entries; delayed start; pause/resume; skipped time; retry; lifetime expiry while capped.
- A partially successful group produces exactly the actual count and cannot exceed caps on retry.
- Formation offsets that place a member on land or in view are rejected.
- Boss and event modifiers compose and expire independently.
- A blocked population does not produce a stored-up flood when space opens.

**Checkpoint C02**

Spawn tests pass. A short seeded live capture shows counts consistent with telemetry. No boss/POI reward logic is allowed to infer completion from these raw counts yet.

### Step 03 — Track encounters and repair boss/cache completion

**Do**

1. Add a stable per-run encounter ID and handle with `Requested`, `Spawned`, `Completed`, and `Failed` states. Track the expected count, actual spawned stable agent IDs, and killed IDs.
2. Distinguish a kill from despawn, pool recycling, unload, and reset. Required enemies disappearing without a kill must not count as a win.
3. Reserve capacity for boss/cache requests. Retry valid placement for up to the configured active-time timeout. A partial request may not complete until all required enemies were spawned and killed.
4. Change boss summoning so the beacon is only committed when the encounter is accepted; on terminal spawn failure restore its interaction and combat state. No payment or reward is involved in a failed summon.
5. Remove empty-boss-array victory checks. Subscribe to the tracked boss death/completion event.
6. Track Powder Cache enemies with the same lifecycle; do not use reused pool indices as identity.
7. Process a legitimate simultaneous boss/player death as objective secured, then run settlement. Unrelated boss entities cannot secure this objective.
8. Make completion and reward callbacks idempotent. Reset/cancel all outstanding handles on a new run.

**Tests**

- Null boss profile, missing definition, full cap, blocked placement, delayed spawn, and timeout all grant zero victory/rewards.
- Boss spawned and alive: no success. Actual tracked death: success once. Despawn/reset/unrelated death: no success.
- Partial cache spawning, pooled-slot reuse, duplicate death event, and retry after failed encounter.
- Boss/player die in the same simulation update; boss dies after 10:00; restart while a request is queued.

**Checkpoint C03**

Automated PlayMode coverage reproduces the previous false-win case and now passes. A real boss death secures victory and leaves a living player in combat.

### Step 04 — Author the full spawn roster and timeline

**Do**

1. Create authored profiles using existing enemy definitions and visuals. Register placeholder presentation where a required definition lacks art.
2. Use a five-band composition: first sharks/swordfish; then cannonboats/hammerheads; then manta flanking plus occasional bomb boats and boarding barges; finally limited kraken area denial.
3. Start with these ordinary/specialist proportions by two-minute band: 100/0, 80/20, 70/30, 65/35, 60/40. Within eligible ordinary families split equally; within eligible specialist families split equally, subject to caps. If a specialist cap blocks selection, spend that opportunity on an eligible ordinary family.
4. At minute 3 spawn the scheduled elite hammerhead; at minute 8 spawn the scheduled elite kraken. Keep them distinct from ordinary variants for reward attribution.
5. Wire the boss, cache ambush, swarm, pursuer, and harrier profiles into the solo schedule. No required slot may remain null.
6. Implement warnings, 30-second swarm modifiers, and boss suppression. Skip events that fall during the boss rather than saving them for later.
7. Apply existing ordinary HP/damage scaling once at spawn. Freeze spawn-scaled values for that enemy; do not reapply every frame or both in the profile and world layer.
8. Enforce the two-windup charge limit through shared attack permission, not by permanently disabling enemies.

**Tests / checkpoint C04**

- Validate the authored schedule and all referenced definitions.
- Advance through 0:00, 2:00, 3:00, 4:00, 6:00, 7:00, 8:00, and 10:00; verify rates, eligible roster, warnings, and event counts.
- Ensure no ambient spawning starts after 10:00, and boss pressure is halved before overtime.
- Confirm cannonboat/area/charge caps under stress. Check a corner and island-adjacent route for escape space.
- Capture a full timeline with cheats disabled; fast-forward tests supplement rather than replace normal-speed verification.

### Step 05 — Complete boss attacks and the survival finale

**Do**

1. Build Barnacle King's attack state machine: aimed spread, committed charge, marked bombardment, recovery. Telegraph before damage and use stable target positions for committed attacks.
2. Above 50% hull, cycle the three patterns individually. Below 50%, shorten recovery modestly and permit spread followed by bombardment, keeping charge exclusive and preserving a navigable gap.
3. Start with existing boss base stats plus ordinary time-at-summon scaling. Tune toward the fight-duration target using representative loadouts, not a runtime player-DPS adjustment.
4. Implement overtime interval/strength rules from the profile. Existing ordinary enemies remain; no new ordinary ambient waves are created.
5. Derive a storm safe region from the actual stage bounds and connected navigable water around the validated beacon arena. Validate its minimum region during generation; reject/regenerate layouts that cannot support it.
6. Show the safe-region boundary and advance warning. Damage must match the visible boundary and respect gameplay pauses.
7. Reset boss, storm, overtime timers, warnings, and damage accumulators on retry; stop their updates after death.

**Tests / checkpoint C05**

- All attacks deal no damage before their telegraphs finish.
- Charge commits rather than tracking the player through the entire lunge.
- Half-hull transition does not double-fire or remove recovery.
- Overtime begins at 600 seconds, not boss death; storm begins at 720 seconds.
- Safe region contains maneuverable water on seeded layouts and matches the rendered indicator.
- Boss can be won before or after overtime. A player can survive beyond 15 minutes without forced termination.

### Step 06 — Implement finite exploration and safe POI placement

**Do**

1. Generate exactly 12 chests and the five named special/objective POIs at stage start. Remove gold/progress-triggered creation for this profile.
2. Separate existence, discovery, availability, and consumption. Reopening the minimap or restoring state must not recreate rewards.
3. Place the first chest within approximately 15 seconds of clear travel from spawn, using measured unchanged ship handling rather than raw inspector maximum speed.
4. Use a navigability representation expanded by the ship footprint and maneuver margin. Validate connected routes, interaction approaches, and the boss/storm arena; use a bounded retry and a known-valid fallback layout.
5. Implement beacon hints at 5:00/7:00. Treasure Chart reveals three unopened chests and beacon direction; nearby physical discovery remains available immediately.
6. Preserve the existing Signal Buoy bonus choices and eight-second cumulative occupancy rule. Leaving pauses charge; it does not reset or continue charging remotely.
7. Disable the automatic route-event sequence for this profile. Powder Cache remains a finite player-triggered encounter; do not add a replacement timed reward generator.
8. Add two optional explosive barrels near alternate reward approaches, outside mandatory interaction/escape space. Use a readable 1.2-second fuse and the same blast radius/damage rule for player and enemies. Initial blast: radius 4, damage 20, one explosion per barrel. Mark both as placeholders until final presentation is supplied.

**Tests / checkpoint C06**

- For 100 layout seeds, assert exact counts, reachability, opening route, POI clearance, and usable minimum storm region.
- Gold changes do not create chests. Used chests never return.
- Full-health repair interaction does not consume the wreck; completed use pays once.
- Cache rewards require the tracked fight; buoy charge is cumulative only inside its radius; chart handles fewer than three remaining chests.
- Barrels cannot trigger repeatedly or start recursive unlimited explosions; damage is applied once to each actor.
- Walk/sail representative seeds at normal speed, including the worst measured route seed.

### Step 07 — Wire six-slot progression and useful build choices

**Do**

1. Implement the fresh-save starter pool and leave disabled weapons out. Preserve captain starting weapon identity where compatible; every run still starts with exactly one weapon.
2. Define the XP contract explicitly: level starts at 0, level 0 to 1 costs 7; for current level `L >= 1`, cost is `round(5 + 5 * 1.065000057220459 * (L + 1)^1.065000057220459)` using one explicit rounding convention. Compare against the supplied extracted table and document any source rounding discrepancy before tuning.
3. Apply this curve once in solo Saltwind. Do not multiply it again through legacy demo pacing, and do not silently multiply XP drops to compensate.
4. Preserve fill-state weighting and new-weapon offer protection at levels 4, 8, 12, and 16. Remove unconditional demo guarantees from the normal profile where they bypass these rules.
5. Preserve separate weapon roles and current targeting behavior. Tune effective hit coverage, rate, and damage with representative routes; do not balance only on base damage/cooldown division.
6. Filter items that require locked/disabled content. General-purpose items remain eligible even if they also benefit a particular weapon. Evolution-only ingredients require their base weapon to be available in this run's content pool.
7. Ensure all six evolution ingredients have an immediate, described benefit for their associated weapon. Start with a modest +10% associated-weapon damage where no working immediate benefit already exists; do not stack a replacement bonus onto an existing useful one.
8. Audit proc ancestry. Proc-generated damage may not re-trigger the same chain recursively; use a shared bounded policy rather than independent unlimited callbacks.

**Tests / checkpoint C07**

- Source XP fixtures, level indexing, rounding, and multi-level XP grants.
- Fresh save can receive all six starter weapons; cannot receive locked/disabled weapons.
- Slot caps, maxed upgrades, duplicate cards, declined new-weapon offers, pity thresholds, and exhausted-pool fallback.
- Evolution triggers once only with its actual requirements; ingredient description matches applied behavior.
- A dense chain-proc fixture terminates with bounded damage-event count.
- Log weapon acquisition/evolution times in actual fresh-save runs, not only unlock-all scenarios.

### Step 08 — Make run gold and powerups consistent

**Do**

1. Remove hidden gold multiplication only for the new profile. Assign ordinary/specialist/scheduled-elite reward classification explicitly.
2. Centralize chest quoting and purchase. Prepare a valid item award first, then atomically consume gold, increment successful purchase count, mark the chest used, and grant the item. If the operation cannot complete, leave all four unchanged.
3. Show the exact authoritative price in the interaction UI. Repeated hover/quote calls cannot advance it.
4. Ensure kill drop splitting/merging/pooling conserves XP and gold totals.
5. Limit the solo temporary pool to the four approved powerups. Implement drop cooldown, guaranteed elite exception, expiry, and refresh behavior.
6. Magnetic Tide collects existing eligible loose rewards; it does not open chests, detonate barrels, collect future drops, or synthesize currency.
7. Record gross gold earned, spent, and remaining separately. No run gold is converted into Marks or treasury at settlement.

**Tests / checkpoint C08**

- Chest fixtures: 20, 31, 38, 46, 56; insufficient funds; missing eligible loot; duplicate interaction; repeated price queries.
- Currency conservation across merged/split pickups and run end.
- Statistical seeded gold/drop checks with expected confidence bounds, not exact probabilistic counts.
- Powerup cooldown and guaranteed-elite behavior; two Powder Rush pickups refresh rather than become +60%; Dead Calm excludes boss; healing clamps.
- At death, remaining run gold is shown as run-only and does not increase persistent balances.

### Step 09 — Add Marks, unlock purchases, and safe settlement

**Do**

1. Extend the local save schema with Marks, selected unlock goal, required content ownership IDs, and settled-run receipt IDs. Preserve all existing fields during migration.
2. Give each new run a unique run ID separate from its seed. A retry with the same seed is a new run; resuming the same run preserves its ID.
3. Track kill-qualified minute buckets and one-time event credits as gameplay occurs. Derive the settlement breakdown from these records, not client UI counters.
4. Settle Marks once at death, even on a failed objective. Persist balance update and receipt together in one save document; duplicate settlement is a no-op.
5. Implement idempotent unlock purchase: require enough Marks and not already owned; deduct and grant together. Preserve all pre-existing owned weapons/captains.
6. Disable permanent stat purchases within this slice. Keep treasury and its existing uses outside this solo progression path.
7. Turn off unlock-all/debug grants for the normal profile while retaining named development presets.

**Tests / checkpoint C09**

- Legacy-save migration preserves doubloons, IDs, quests, and ownership and starts Marks at zero.
- Reward fixture: five qualifying ordinary minutes + two special POIs + one scheduled elite + boss + two qualifying overtime minutes = **52 Marks**.
- Partial minute, fewer than 10 kills, idle overtime, duplicate special POI, duplicate boss/elite, and more than five overtime minutes.
- Double death callback, repeated results opening, reload after settlement, same seed/new run, and purchase double click.
- Existing ownership is still selectable without repurchase. A fresh profile uses the intended starter pool.

### Step 10 — Reset, snapshot, and mode-isolation integration

**Do**

1. Make reset order explicit: end/cancel old encounters and effects, clear world/pickups, initialize new profile/run ID/RNG, build layout, reset directors, initialize player/build, then enable gameplay.
2. Snapshot the profile/version/hash, run ID, reward counters and credited IDs, consumed/discovered POIs, chest count, boss/encounter states, event schedule state, overtime/storm state, pickup cooldown, loadout, and evolution mappings.
3. Extend the actual restore path if it exists; do not merely add serializable fields that are never restored. Serialize sufficient RNG/actor/timer state for any claimed gameplay resume support.
4. Do not interpret an old/incomplete snapshot as an active encounter with zero enemies. Reject unsupported active-run snapshots cleanly without awarding a win or deleting persistent progression. Document supported snapshot versions and behavior.
5. Ensure named pause reasons do not allow gameplay timers/reward buckets to progress while a choice, manual pause, or suspend reason remains active.
6. Run co-op, PvP, and legacy-mode regression checks for shared code changes. Restore old rules outside the explicit solo profile.

**Tests / checkpoint C10**

- Three consecutive retries in one loaded scene reset all transient state.
- Pause during warning, encounter retry, boss attack, buoy charging, powerup duration, and storm damage.
- Snapshot serialization/restore tests for each supported state; clean rejection of unsupported/incompatible state.
- Stable agent IDs and receipts cannot be confused across retries or pooled slots.
- Protected camera/handling capture matches baseline; other modes retain their profile and currency behavior.

### Step 11 — Results, unlock UX, telemetry, and readability

**Do**

1. Present objective outcome separately from death: “Victory secured” remains true after a successful player dies.
2. Show elapsed time, final build, per-weapon damage contribution, gold earned/spent, Marks source breakdown, total Marks, and selected-unlock progress.
3. Provide direct retry and menu unlock purchase. Persist the chosen goal; selecting a goal does not purchase it.
4. Emit structured events for profile/hash, encounter lifecycle and blocked spawns, actual rewards, POI outcomes, weapon acquisition/evolution, boss summon/death, overtime transitions, settlement, and death source.
5. Keep hostile warning shapes visible above water/friendly effects. Prioritize incoming-damage audio and limit redundant friendly impacts/damage-number clutter. Do not change camera tuning or movement.
6. Create a placeholder catalogue with stable ID, gameplay role, current visual, silhouette/color, replacement asset needed, and where it is referenced.

**Tests / checkpoint C11**

- UI totals reconcile to the underlying ledger and save; a failed run still shows legitimately earned Marks.
- Damage percentages handle zero damage and multi-source/proc attribution without double counting.
- Retry starts a clean run; menu purchase updates all relevant locks and offers.
- Full six-weapon capture shows telegraphs visible at normal gameplay scale. Captures identify build, seed, time, and profile version.
- User-facing labels contain gameplay information, not debug IDs or internal implementation details.

### Step 12 — Bring Balance Lab into parity and run calibration

**Do**

1. Replace old-director assumptions for this scenario with the shared profile export and current encounter timing, caps, boss suppression, reward classifications, XP curve, chest prices, and Marks accounting.
2. Explicitly label modeled player behavior: movement/collection efficiency, route choice, offer choice, weapon coverage, boss activation time, and damage avoidance. Do not imply abstract navigation is a real playthrough.
3. Add representative precision, broadside, and area/control build policies. Policies choose only offered/unlocked content and acquire weapons over time; do not start with a perfect six-weapon build.
4. Run **100 seeds for each of three build policies × two skill presets (Average and Expert)**: at least 600 runs. Test fresh-save and unlocked pools separately, for at least 1,200 total runs. Use the same seed list for comparisons.
5. Report survival and boss completion independently, boss timing, level/weapon counts at checkpoints, evolutions, chest purchases, gold sources/sinks, Marks, peak population, and blocked requests.
6. Show all-run results as well as successful-run conditional metrics. Include sample counts and percentiles; exclude deaths before a checkpoint explicitly instead of treating them as zero-valued survivors.
7. Import matching live telemetry and compare economy, event timestamps, spawn counts, and progression. Fix model disagreement before interpreting win-rate differences as tuning advice.
8. Tune one subsystem at a time and save baseline/candidate exports and reports. Numerical changes must include rationale and a new balance version.

**Tests / checkpoint C12**

- Same seed/profile/policy reproduces the report.
- A runtime/lab fixture agrees on deterministic schedule windows, XP thresholds, chest price, reward totals, and overtime formulas.
- The lab does not write production assets or player saves.
- Batch outputs and scenario definitions are attached, not screenshots of a summary alone.
- No single build policy dominates every tested situation without an explained tradeoff. Flag suspicious results for live review rather than hiding them in averages.

### Step 13 — Performance and human verification

**Do**

1. Profile a standalone desktop build through opening, swarm, boss, six-weapon combat, and overtime. Record hardware, resolution, build type, graphics settings, profile/hash, and seed.
2. Stress the 80-enemy limit with representative projectiles, pickups, and effects. Measure median/p95/p99 frame time, main-thread time, GC allocations, peak memory, and pool growth after warm-up.
3. Aim for stable 60 FPS on the recorded desktop target. Record mobile performance against the project's 30 FPS goal on real hardware if available; editor emulation is not device evidence.
4. Optimize pooling, queries, and presentation before changing gameplay population caps. Re-run relevant balance checks if a gameplay limit changes.
5. Have at least five newcomers play up to five attempts each where feasible. Record prior genre experience, attempt number, seed, build version, boss result/time, death cause, discoveries, and voluntary retry interest.
6. Ask whether they understood the objective, why they died, what they would change next run, and whether exploring felt worthwhile. Do not coach the first attempt beyond the normal onboarding.
7. Have an experienced tester check late boss victory and multiple build families. Confirm that long-surviving builds are possible without exploit loops.

**Checkpoint C13**

- Performance evidence includes actual device/build context and traces.
- Human observations are reported separately from simulation policies. Small samples are labeled directional evidence.
- First victory and successful-duration targets are evaluated honestly. Unmet targets trigger focused tuning or remain explicitly open.
- Missing testers/devices are marked pending; do not substitute fabricated results.

### Step 14 — Final regression and Cursor delivery

**Do**

1. Run all relevant EditMode and PlayMode assemblies, not just newly added tests.
2. Run the normal menu → fresh-save solo → boss → death/results → unlock → retry flow.
3. Repeat with an existing save, a failed run, late boss victory, and multiple retries. Recheck other modes and protected movement/camera values.
4. Validate asset references, metadata, profile export/hash, and placeholder IDs. Ensure runtime works from a clean checkout/import and does not depend on an uncommitted generated asset.
5. Update balance/progression and Balance Lab documentation. Link superseded sections to this new solo profile rather than describing obsolete systems as current.
6. Produce the delivery index below and list unresolved issues with reproduction steps and severity.

**Checkpoint C14**

Implementation is ready for Codex review only when correctness evidence is reproducible. Calibration, hardware, and human gates may be separately pending, but the final report must say so prominently.

## 5. Test commands and execution precautions

Run commands from the repository root. Prefer the connected Editor/CLI workflow after checking readiness. Consult the installed CLI's help if flags differ; record the actual command used.

```powershell
$env:UNITY_PROJECT_PATH = Join-Path (Get-Location) 'Pirate Game Unity'
unity status --json
unity pipeline list
unity test 'Pirate Game Unity' --mode EditMode --output 'plan/saltwind-verification/editmode-results.xml'
```

Use the installed Editor directly when needed. Do not launch batch mode against the same project while an interactive Editor owns it. Use the connected Editor test runner or close the Editor cleanly first. Do not terminate it and discard unsaved work.

```powershell
$saltwindEditor = 'C:/Program Files/Unity/Hub/Editor/6000.3.11f1/Editor/Unity.exe'
$saltwindProject = Join-Path (Get-Location) 'Pirate Game Unity'
$saltwindEvidence = Join-Path (Get-Location) 'plan/saltwind-verification'
New-Item -ItemType Directory -Force -Path $saltwindEvidence | Out-Null

& $saltwindEditor -batchmode -projectPath $saltwindProject -runTests -testPlatform EditMode -testResults "$saltwindEvidence/editmode-results.xml" -logFile "$saltwindEvidence/editmode.log"

& $saltwindEditor -batchmode -projectPath $saltwindProject -runTests -testPlatform PlayMode -testResults "$saltwindEvidence/playmode-results.xml" -logFile "$saltwindEvidence/playmode.log"
```

- Run the two test commands sequentially and wait for completion. If the Windows shell returns before the Editor process exits, use an appropriate waiting process invocation.
- Do not add `-quit` if it prevents the test runner completing; verify against the installed runner behavior.
- Do not filter only `HighSeas.Tests.EditMode`: the repository also contains the `RoguePirates.Tests.EditMode` assembly.
- Check XML existence, timestamp, test count, failures, and logs. A process exit code alone is not proof tests ran.
- Do not call broad bootstrap/scene rebuild methods as a test prerequisite unless inspection proves they are needed and their exact affected assets are intended.
- Do not run baseline playtests against the owner's real profile without backing it up. Use isolated test saves/backends and restore them afterward.

## 6. Required evidence package

Create `plan/saltwind-verification/` during implementation with:

- `IMPLEMENTATION_LOG.md`: baseline commit, step/checkpoint status, changes, commands, pass/fail/pending outcomes, and deviations.
- `VERIFICATION_INDEX.md`: links to every deliverable, reproduction steps, final commit/profile/hash, and outstanding issues.
- `PROTECTED_SETTINGS.json`: before/after handling, camera, starting hull, and mode/profile checks.
- `BALANCE_DECISIONS.md`: reference-derived versus proposed values, initial/final values, reasons for changes, source paths, and version history.
- `PLACEHOLDERS.md`: stable content IDs and asset replacement needs.
- Test XML and relevant logs; large captures/traces may live outside Git with stable paths identified in the index.
- Versioned scenario/profile exports, seed lists, all batch outputs, and before/after calibration reports.
- Live telemetry for representative seeds and the lab/runtime comparison.
- `PLAYTEST_REPORT.md`: actual human and device evidence, or explicitly pending sections with instructions for running them.

For every checkpoint use one of **not started**, **implemented / unverified**, **failed**, **passed**, or **pending external playtest/device**. Explain any non-passed state. Do not mark all tasks complete merely because code compiles.

## 7. Independent Codex verification checklist

Codex should re-run and inspect evidence independently rather than accepting Cursor's summary.

### Correctness and scope

- [ ] C00–C14 have traceable evidence or explicit pending status.
- [ ] Diff preserves handling, camera, existing assets, saves, and unrelated modes.
- [ ] Solo profile is the actual loaded runtime configuration, not an unused asset.
- [ ] Resource copies/runtime exports match the authored profile; there are no hidden overrides.
- [ ] Required encounter profiles exist and are referenced in the actual scene/run flow.
- [ ] Spawn limits, retries, partial counts, expiry, loops, and pause behavior hold.
- [ ] Failed boss/cache spawning cannot produce victory or rewards.
- [ ] Only actual tracked kills complete encounters; pooled IDs cannot leak across runs.
- [ ] Boss victory before/after 10:00 and simultaneous player death follow the agreed rules.
- [ ] Overtime/storm timing, safe water, visuals, and damage agree.

### Economy and progression

- [ ] Fresh saves can fill six slots from the starter pool without unlock-all.
- [ ] XP indexing and rounding match fixtures; no extra hidden multiplier.
- [ ] Item eligibility, immediate ingredient benefits, evolutions, and bounded procs work.
- [ ] All finite POIs exist at stage start and are reachable; gold cannot spawn extra chests.
- [ ] Displayed prices match transactions; failed purchases leave state unchanged.
- [ ] Drop splitting/merging conserves value; powerups refresh correctly.
- [ ] Marks are earned from the defined sources and settle once; no run-gold banking leaks into solo.
- [ ] Migration preserves old balances/ownership; purchase and settlement are repeat-safe.
- [ ] Retry/reset and supported snapshot behavior preserve identity and rewards correctly.

### Evidence quality

- [ ] Tests actually ran at the delivered revision and include all relevant assemblies.
- [ ] Calibration uses at least 1,200 reproducible runs covering both content pools, both skill presets, and all three build policies.
- [ ] Lab abstractions and runtime disagreements are disclosed.
- [ ] Reported averages do not hide failed runs or exclude them without explanation.
- [ ] Performance claims identify device/build/settings and include measured traces.
- [ ] Human playtest claims have actual observations; otherwise experience targets remain pending.
- [ ] Placeholder catalogue is complete and no reference-game binary/art was silently imported.

### Prompt to use with Codex after Cursor finishes

> Verify Cursor's Saltwind implementation against plan/SALTWIND_CURSOR_IMPLEMENTATION_HANDOFF.md and plan/saltwind-verification/VERIFICATION_INDEX.md. Inspect the actual diff, runtime wiring, tuning assets, save migration, and test evidence. Re-run relevant tests and independently check encounter lifecycle, spawn caps/timing, finite POIs, six-slot progression, Marks settlement, late boss victory, reset/snapshot behavior, and mode isolation. Confirm protected handling/camera values remain unchanged. Distinguish code correctness, measured calibration, device performance, and human experience evidence. Report actionable findings with file/line references, severity, reproduction, and missing evidence. Do not treat simulation success as proof of fun or fabricated human playtesting. Do not make fixes until the review findings are presented unless I explicitly request fixes.

