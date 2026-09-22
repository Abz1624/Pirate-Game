# Codex verification: Saltwind implementation

Reviewed 2026-09-23 at commit `6a21ba63` (`finished all 14 steps`), against baseline `701bfa7d` and the approved Cursor handoff. The implementation spans 205 changed files. This is a focused correctness and completion audit, not a claim that every changed line has been exhaustively reviewed.

**Verdict: implementation is substantial, but the handoff is not complete. Fix runtime correctness and test coverage before balancing difficulty or running the newcomer panel.** Performance and human playtests are not the only remaining tasks.

No gameplay code, tuning assets, or scenes were changed during this review. Only this report and its evidence JSON were added. The connected Unity Editor was available and used for independent tests and targeted probes.

## Independently verified evidence

- Unity 6000.3.11f1, connected Editor ready and stopped, no compilation in progress.
- Editor test rerun: **150 total, 148 passed, 2 failed**. Both failures are PlayMode fixtures discovered/executed in EditMode and calling `Destroy` there: `LevelUpPlayModeTests.LevelUpModal_ShowAndSelect_PausesRunManager` and `PlayModeSmokeTests.RunManager_CanBeginRun`.
- PlayMode test rerun: **0 tests discovered**. Passing two tracker tests in EditMode is not evidence of a real gameplay integration run.
- The authored profile reports **balance version 1**, hash `3a45b3c6a7c278a8b3dfbd5b85f2d3bcb5237cfcc20c1d880c26070fc8dd917d`. The decisions document labels later runtime changes as version 2, but this is not reflected in the profile version.
- The profile's referenced schedule is `Assets/_Game/Data/EncounterSpawnSchedule.asset`.
- An Editor probe of the authored schedule found nominal aggregate ambient rates of 0.6 at 1:00, 1.8 at 3:00, 3.6 at 5:00, 6 at 7:00, and approximately 8.857 at 9:00. These are before population/placement/frame-time restrictions and pressure modifiers, not measured live spawn counts.
- A controlled in-memory probe reproduced `RunMarksTracker.BuildRecord()` increasing overtime credits from 0 to 1 and then 2 on two reads without completing another minute. The probe restored the fields afterward and wrote no player save.
- The fresh-save AreaControl lab policy resolves to only `deck_cannons`; the lab scenario automatically schedules its boss at 600 seconds. These are not representative of the approved six-slot exploration/build policy.
- No `balance-lab/` output directory or 1,200-run result package was present in the inspected verification directory.
- No changes to `PlayerShipController.cs` or `FollowCamera.cs` were found relative to the baseline; the targeted scene diff did not show the searched protected handling/camera fields changing. This is a positive static check, not a replacement for a complete before/after runtime settings capture.

Machine-readable results: [CODEX_REVIEW_EVIDENCE_2026-09-23.json](CODEX_REVIEW_EVIDENCE_2026-09-23.json).

Source paths below are relative to `Pirate Game Unity/Assets/_Game/`. Line numbers refer to the reviewed revision.

## A. Fix first: runtime correctness

### R1 — P1: ambient bands stack instead of replacing each other

**Evidence:** all five `Data/SpawnProfiles/Saltwind/SaltwindAmbientBand*.asset` files have `timing.durationSeconds: 0` (line 24). `Scripts/Enemies/EncounterScheduleRunLogic.cs:20` treats a continuous profile with zero duration as never complete. `EncounterSpawner.cs:264` ticks all started profiles. The next band therefore adds to every previous band.

At minute 9, five ambient profiles are active. The 0.35-second minimum per-profile gap also clamps the final band's nominal 3/s to about 2.857/s. The initial planned rates cannot be assessed against the current schedule because the schedule is implementing cumulative pressure.

**Fix:** use explicit exclusive time windows or one total rate budget with time-based composition. Close expired windows before cap/placement checks; currently the cap `continue` occurs before completion, so an expired capped profile can remain eligible. Ensure the total-rate mechanism can represent 3/s and the intended swarm multiplier without unintended per-profile clamping.

**Regression gate:** run the actual scheduler at 119.9/120/239.9/240/479.9/480/600 seconds, with and without a full cap. Assert one active ambient band, correct total opportunities, no late catch-up spawns, and no ambient spawns after the deadline. Verify measured counts over sufficiently long windows, not only a rate helper's return value.

### R2 — P1: required encounter retries, reservation, and final placement safety are incomplete

**Evidence:** `EncounterSpawner.cs:245–261` processes and clears the trigger queue immediately. Zero successful spawns mark the encounter failed immediately; partial success is discarded without scheduling the remainder. The configured 10-second encounter timeout and eight reserved slots are not enforced by this path: ambient and required spawns both use the same hard cap (`:368–370`, `:414`, `:444`, `:583`).

`POIManager.cs:660–675` accepts a cache attempt when **any** enemy spawned, although the tracker expects three. At 79/80 active enemies, one cache enemy can spawn and its death cannot complete the encounter. A further interaction requests a new encounter rather than finishing the original request.

`EncounterSpawner.cs:469` explicitly bypasses failed final formation-position validation for tracked encounters and bosses. A position valid before formation offsets can end up on land or outside bounds without rejection.

**Fix:** retain required requests with remaining count, stable handle, next retry time, and deadline; reserve ambient capacity; fail only on terminal configuration errors or the timeout. Retrying a pending cache must not replace its handle or create another complete ambush. Revalidate physical clearance/bounds for every final position even when allowing a visible boss summon.

**Regression gate:** zero capacity, one-slot capacity, blocked terrain, partial formation placement, pool reuse, timeout, and retry. Verify the expected three tracked cache enemies eventually spawn or the POI becomes retryable with no reward. Verify a boss cannot appear inside an island.

### R3 — P1: Marks can be awarded without the required completed event

There are three connected errors:

- `V3PressureBeatDirector.cs:107` calls `TryRegisterScheduledElite()` immediately after a successful **spawn**, rather than the elite's kill. `RunMarksTracker.cs:132` stores only one boolean, so killing both scheduled elites cannot award two credits.
- `RunMarksTracker.cs:146–149` calls `TryCreditOvertimeMinute()` from `BuildRecord()`. Reading the record, including snapshot capture, mutates earned credit and can pay an unfinished minute repeatedly. The Editor probe reproduced this.
- `RunMarksTracker.cs:89–92` rejects ordinary-minute closure based on whether the run is in overtime **now**. When the clock reaches 600 seconds, the completed `[540,600)` bucket is skipped, even though it belongs to the ordinary stage.

**Fix:** make record/snapshot reads pure. Close each elapsed bucket exactly once by its own time range. Credit elites on actual tracked death using a set of event/agent IDs, not one per-run boolean. Do not count partial buckets at death.

**Regression gate:** spawning an elite awards 0; killing both scheduled elites awards 10 Marks; repeat death callbacks award 0 more. At 10:00 the final qualifying ordinary minute pays. Ten kills in an unfinished overtime minute pay 0; repeated record/snapshot reads leave totals unchanged; completing the minute pays exactly 3 once.

### R4 — P1: unlock ownership is not consistently honored

**Evidence:** `V3ContentUnlocks.cs:26–32` allows starters plus `HasMarksOwned` while the profile is active. `SaveService.cs:93–100` preserves legacy ownership arrays but initializes `marksOwnedContentIds` empty; `HasMarksOwned` checks only that new array (`:125`). A legacy owned non-starter can therefore become unavailable during solo play despite being preserved in the serialized save.

`SaltwindStageProgressionLogic.cs:91–96` checks evolution-item eligibility against the **starter** weapon pool only. Purchasing Lightning Mast/Flamethrower does not make their ingredients eligible, blocking their evolution progression through this filter.

`SaltwindMarksUnlockLogic.cs:17–24` resolves only `WeaponUnlocks`. Brine/Flint costs exist in `CaptainUnlocks`, but the purchase logic does not support them and the character selector does not consume Marks ownership to enforce their planned gates.

**Fix:** define one effective ownership/content-eligibility service for menu, captain selection, level offers, chest loot, and purchases. Include existing legitimate ownership, new Marks ownership, canonical aliases, and captain entries. Check ingredient eligibility against this effective weapon pool.

**Regression gate:** migrated saves retain previously owned weapons/captains without payment; fresh saves have only intended access; buying Lightning Mast permits Copper Conductor and its evolution; Brine/Flint purchase and selection work at the approved prices; duplicate purchases do not charge.

## B. Complete the actual stage behavior

### R5 — P2: pressure events still execute legacy timing and boss-conflict rules

**Evidence:** `V3PressureBeatDirector.cs:17` still hard-codes 20-second pulses instead of the profile's 30 seconds. `TickPulses` does not suppress new swarms during boss combat. `TickBeat` defers an elite for up to 20 seconds, allowing it after a short boss encounter rather than skipping the conflicting scheduled event. Pure timeline tests do not exercise this director.

**Fix:** consume the profile's event schedule in the runtime director. Skip boss-conflicting events once, record the skip, and do not release them later. Add five-second swarm warnings as well as elite warnings.

**Regression gate:** a boss spanning 4:00/7:00 gets no new pulse; a boss spanning 3:00/8:00 does not produce a deferred elite afterward; normal pulses last exactly the configured duration under pause/resume.

### R6 — P2: map/storm reachability guarantees are not established

**Evidence:** `V3StagePoiPlacer.cs:95` and `:171` fall back to clamped near-spawn positions after valid sampling fails without revalidating their water/path clearance. Other POIs are point-checked rather than checked for a connected traversable route. `SaltwindPoiLayoutLogic.Validate` primarily checks counts, bounds, and straight-line distance; it does not validate every route or the minimum storm arena.

`PlayBounds.cs:72–80` contracts a rectangle around the map center, not a validated navigable region around the beacon. The 100-seed test calls the limited validator, so its success does not establish the promised connectivity or safe-water guarantee.

**Fix:** validate connected navigable water with hull/turning clearance; reject bad layouts or use a known-valid fallback. Select and validate the final storm area from reachable water; all shrinking boundaries must preserve a usable route/arena. Do not change handling to compensate.

**Regression gate:** adversarial island layouts, enclosed pockets, blocked opening-chest routes, exhausted placement attempts, and final storm regions containing land. Repeat the seed sweep with actual route/minimum-region assertions, then sail the worst seeds.

### R7 — P2: marked bombardment and world telegraphs are not implemented as specified

**Evidence:** `BarnacleKingBossAttackDirector.cs:151–162` implements “MarkedBombardment” as three ordinary traveling shots toward a stored point. It does not create a marked area blast there. Its telegraph method (`:176`) displays a toast, not the spatial warning/impact footprint promised by the plan.

**Fix:** implement a visible marked danger area with the agreed warning interval and area damage at the committed location, plus readable spread/charge direction telegraphs. Ensure friendly six-weapon effects cannot obscure them. Keep the existing attack-state helper only if it supports the actual runtime behavior.

**Regression gate:** no damage before the warning ends; the rendered footprint matches the damaged area; moving away from the marker avoids impact; enrage preserves recovery and maneuverable gaps. Record a normal-speed six-weapon fight.

### R8 — P2: normal-play debug grants and profile lifetime need isolation

**Evidence:** `V3PlaytestFlags.cs` still enables run hotkeys/debug UI. `V3RunBootstrap.cs:249` installs the hotkeys and `V3RunPlaytestHotkeys.cs:71–107` permits free item/logbook/loadout grants and spawn toggling during normal solo play. These are not gated by a named developer profile.

`V3RunBootstrap.OnDestroy` (`:81`) only clears its bootstrap singleton; it does not end the static stage-balance context. `StageBalanceProfileContext.EndRun()` is called on the next V3 bootstrap, so a visit to the menu or a legacy scene can retain solo state. Conversely, before the first solo run, menu eligibility follows the global unlock-all path. This makes content presentation depend on session history.

**Fix:** separate persistent mode/catalogue selection from active-run state; clear transient context when leaving the run and explicitly resolve the menu's catalogue. Enable cheats only in a dedicated developer preset/build path. Verify the legacy scene path rather than assuming the next V3 bootstrap always runs.

**Regression gate:** normal solo hotkeys cannot grant content; debug preset still works; fresh boot and return-to-menu show identical ownership; solo → legacy/co-op/PvP → solo does not leak profile/reward rules.

## C. Repair calibration and verification before tuning

### R9 — P1 for balance sign-off: the lab does not model the approved gameplay

**Evidence:** `Simulation/Combat/CombatSimulator.cs:585–645` grants predetermined weapons on even player levels, gives them the same generic weapon stats, increases every weapon's damage each level, and applies an automatic evolution multiplier based on player level without the real item requirement. It does not select only from actual level-up offers. The fresh-save AreaControl queue contains just Deck Cannons; other queues have at most four entries.

`SaltwindBalanceLabScenarioFactory` uses a generic scenario with boss arrival at 600 seconds. Sharing chest/XP quote functions is useful, but it is not encounter, progression, economy, or route parity. The profile export includes schedule **keys** (`StageBalanceProfile.cs:281`) rather than the referenced schedule/content values, so changing a spawn duration or weapon stat need not change the exported content hash.

The matrix test runs 2 seeds × 12 cells = 24 runs, checking shape/determinism rather than calibration targets. No required 1,200-run outputs or live-runtime comparison were present. Simply running the existing menu action would not make its gameplay model representative.

**Fix:** export the actual relevant referenced content; snapshot weapon stats and effects; model the real eligible offers and requirement-based evolutions; align spawn/event windows, caps, rewards, collection assumptions, and boss activation policy with runtime. Record remaining approximation limits. Bump balance version for delivered tuning changes.

**Regression gate:** shared fixed-seed traces reconcile runtime and lab at spawn/level/chest/reward checkpoints. Then run 100 seeds × 3 viable build policies × 2 skill presets × 2 ownership pools, save per-run outputs, summarize all-run and survivor-conditioned metrics, and compare to real telemetry. Do not tune runtime based on the current generic model's win rates.

### R10 — verification blocker: passing helpers are being treated as integration evidence

**Evidence:** `Tests/PlayMode/HighSeas.Tests.PlayMode.asmdef:9–10` restricts the purported PlayMode assembly to Editor. Independently, the test runner discovers those fixtures in EditMode and discovers zero in PlayMode. The two “boss PlayMode” tests only call `RunEncounterTracker` and yield; neither spawns the real boss, drives `POIManager`, or observes `RunManager` victory/settlement.

Other gates similarly test helpers or constants: e.g. the retry test manually resets the tracker; the proc-cascade gate calls a synthetic bounded loop. The added `DamageProcPolicy` event-budget methods have no runtime callers in the inspected Scripts tree. This does not prove an actual infinite proc bug, but it invalidates the claim that the new shared budget is verified in runtime.

**Fix:** correct the test-assembly configuration; run real PlayMode scene/component fixtures for the affected lifecycle paths, with isolated saves and cleanup. Do not silence the two current errors and count that as equivalent to PlayMode coverage. Wire the intended proc policy where required or document and test the existing recursion guards instead.

**Regression gate:** nonzero PlayMode discovery and passing actual spawn → boss death → objective → player death → single settlement tests, plus partial-cache retry, pause, same-tick death, late victory, three scene retries, and mode transitions. Test the real proc callback path, not a separate mathematical demonstration.

## Recommended Cursor work order

1. **Correctness patch:** R1, R2, R3. These change the effective economy/difficulty and can invalidate all subsequent measurements. Add their runtime regression tests while fixing the test assembly in R10.
2. **Progression and mode patch:** R4 and R8. Test fresh/migrated saves, unlocks, menus, and mode transitions with debug grants disabled.
3. **Stage completion patch:** R5, R6, R7. Verify warnings, actual boss attacks, reachable rewards, and the final safe region in PlayMode.
4. **Calibration patch:** R9 and remaining R10 integration coverage. Export/version the real data, reconcile live/model traces, then produce the 1,200-run matrix.
5. **Performance and player validation:** standalone desktop 80-enemy/six-weapon traces; mobile hardware evidence when available; at least five uncoached newcomers where feasible. Record first-boss attempts, successful-run duration, and retry interest separately from simulation.

Do not call desktop performance or newcomer testing “optional” for acceptance against the approved handoff. They can remain explicitly pending while engineering work proceeds.

## Documentation corrections required

- Reopen C02/C03 for schedule and encounter runtime gaps; C04/C05/C06 for actual event/telegraph/navigation behavior; C07/C09 for progression/reward gaps; C10/C11 for mode and ownership consistency; C12/C14 for parity/integration evidence.
- Keep genuinely passing unit tests recorded as passed, but do not promote the entire checkpoint from that alone.
- Mark C13 **pending external playtest/device** rather than “passed (directional human).” Preserve developer observations as useful informal evidence.
- Update the delivery commit to the reviewed/fixed revision; the index still pins `0ee6b0fc` while HEAD is `6a21ba63`.
- Reconcile balance version 1 in the asset with the claimed version-2 changes, and hash the referenced gameplay content required for reproducible calibration.
- Add a complete protected-settings comparison and the missing lab/live/performance evidence after corrections. Do not overwrite this review's observations with later success claims; append resolution evidence with commits.

## Prompt for Cursor

> Read plan/saltwind-verification/CODEX_REVIEW_2026-09-23.md and its evidence JSON. Implement the recommended work order, starting with R1/R2/R3 and real PlayMode coverage from R10. Reproduce each failure before fixing it; use the specified regression gates. Preserve handling/camera and unrelated modes. Treat previously passed helper tests as unit evidence, not proof that the whole checkpoint is complete. Maintain a resolution log mapping R1–R10 to changes, tests, commit/profile/hash, and remaining limitations. Complete actual runtime/lab parity before producing calibration runs. Keep external human/device checks honestly pending. Do not remove failing tests or weaken their assertions merely to restore green status.

