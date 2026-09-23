# Saltwind independent follow-up — 2026-09-23

Reviewed branch `game-balance-experiment`, HEAD `c69d5668af04297021e7dced23a49ac2435d9747`. The index previously pinned `2bd9ca2abdf05548c57e5f39f8765cbda048e2fe`; both `1893e86a` and `c69d5668` contain subsequent changes. This report supplements, and does not rewrite, the original review or its Resolution addendum.

**Verdict: correctness work and independent Unity verification remain open.** Numerical lab band warnings are calibration observations, not release blockers. No gameplay, tuning, scene, prefab, or test-source fixes were made during this review.

## Severity-ordered code findings

Paths below are relative to `Pirate Game Unity/Assets/_Game/`.

### F1 — P1: Failed boss requests can still spawn an untracked boss

- Location: `Scripts/POI/POIManager.cs:790–795`; `Scripts/Enemies/EncounterSpawner.cs:261–310`; `Scripts/Encounters/RunEncounterTracker.cs:86–90`.
- `TriggerProfileImmediate` retains an unsuccessful request for retries. POIManager immediately marks that encounter Failed and clears its local ID. The queue never checks whether the encounter became terminal. A later successful spawn is created in EnemyWorld, but `RegisterSpawnedAgent` rejects its failed encounter. Its death therefore cannot secure victory. Retrying the beacon can also leave multiple boss requests queued.
- Reproduction: block boss placement or fill the hard cap; activate the beacon; free a valid position/slot before the ten-second retry deadline; allow the queue to tick; kill the spawned boss. Check failed encounter state, missing agent mapping, and objective remaining unsecured. Repeat activation while blocked to check duplicate bosses.
- Missing evidence: an actual POIManager → blocked spawn → delayed retry → EnemyWorld death integration test. Existing failed-tracker tests do not cover the retained queue.

### F2 — P2: Interacting with an uncleared cache replaces its tracked encounter

- Location: `Scripts/POI/POIManager.cs:564–570, 702–706`.
- Every interaction while the current cache is uncleared calls `SpawnPowderCacheFight`, requests three more enemies, and replaces `_powderCacheTrackerEncounterId`. It does not limit this to a failed encounter. Killing the original ambush no longer completes the currently tracked cache; repeated interactions add more fights.
- Reproduction: trigger a cache, interact again before killing its enemies, then kill only the original tracked group. Compare encounter IDs before/after and queued/spawned enemy counts.
- Missing evidence: repeated-interaction test asserting stable encounter identity until completion or a deliberate failed-request retry. The partial-cache tests exercise the spawner directly.

### F3 — P2: Cannonboat population limit is bypassed

- Location: `Scripts/Balance/SaltwindAmbientCompositionLogic.cs:17–24, 75, 118–122`.
- Cannonboats are selected through `OrdinaryFamilies`, passing `specialistPool=false`. `IsWithinCap` returns true before reaching the cannonboat check. The authored maximum of four therefore does not constrain ambient cannonboats.
- Reproduction: use band 1 or later, report four active cannonboats through the count callback, and roll many seeds. Cannonboats remain eligible; confirm live fifth-spawn rejection after repair.
- Missing evidence: a cap regression using the actual ordinary-pool selection path, plus a live population assertion. Existing majority/cap helper evidence is insufficient.

### F4 — P2: Ordinary species kills consume scheduled-elite Marks credits

- Location: `Scripts/Enemies/EnemyWorld.cs:1287–1288`; `Scripts/Balance/RunMarksTracker.cs:141–148`.
- Every kill passes only `def.id` to the scheduled-elite credit function. Ambient hammerheads occur from band 1 and kraken spawns from band 3; killing these awards the scheduled five Marks and consumes the species key even when the scheduled encounter has not occurred or was skipped. Later killing the actual scheduled elite cannot earn a distinct credit.
- Reproduction: kill an ambient hammerhead between 2:00 and 3:00, inspect the Marks record, then skip the 3:00 elite through boss combat. The scheduled-elite credit remains despite no scheduled-elite kill. Repeat for ambient kraken before 8:00.
- Missing evidence: credit based on the scheduled spawn/encounter identity, with ambient, failed, skipped, repeated, and actual elite kills tested separately.

### F5 — P2: Boss-conflicted swarm events still fire after the boss

- Location: `Scripts/Balance/SaltwindPressureBeatLogic.cs:38–46`; `Scripts/V3/V3PressureBeatDirector.cs:148–161`.
- Elite events now mark boss conflicts done, but swarm events do not. `ShouldStartSwarmPulse` merely returns false during the fight; once the fight ends, `elapsed >= start` starts the old pulse and grants a fresh full duration.
- Reproduction: keep the boss active from 3:55 through 4:40. After victory, inspect the `v3_swarm_pulse` modifier and `pressure_pulse_started` telemetry: the minute-4 event starts late instead of being skipped. Also cross both scheduled swarm times in one fight.
- Missing evidence: a director-level boss overlap test asserting no deferred pulse after victory, not just a helper assertion while the boss is active.

### F6 — P2: PlayMode settlement tests mutate the owner's persistent save

- Location: `Tests/PlayMode/SaltwindSettlementPlayModeTests.cs:18, 36–52`; `Tests/PlayMode/SaltwindScenePlayModeTests.cs:99–146`; `Scripts/Core/AppRoot.cs:36–39`.
- These tests initialize the production `SaveService`, settle positive Marks into PlayerPrefs, and do not restore persisted balances/receipts in teardown. The shared lifecycle harness also initializes AppRoot's real save. Repeated test runs alter the next real playthrough and undermine fresh-save calibration.
- Reproduction: compare `highseas_meta_v1` before/after `PlayerDeath_SettlesMarksOnce_ThroughRunManager`; Marks and receipt IDs change. Run against an isolated backend or backed-up disposable profile only.
- Missing evidence: suite-wide save isolation/restoration, including scene tests, failure paths, first-conquest flag, and suspend snapshot. This review backed up the game's Editor PlayerPrefs before attempting PlayMode; the attempt timed out before reporting execution.

### F7 — P2: Preserved legacy ownership can still be charged for again

- Location: `Scripts/Balance/SaltwindMarksUnlockLogic.cs:19–25, 58–66`; `Scripts/Save/SaveService.cs` methods `HasEffectiveContentOwned` and `TryPurchaseMarksUnlock`; results caller `Scripts/V3/V3RunResultsScreen.cs:143, 193`.
- Offer eligibility now honors legacy ownership, but unlock-goal selection and purchase still check only `HasMarksOwned`. A weapon present in migrated `unlockedIds`/`purchasedIds` but absent from `marksOwnedContentIds` is offered as the next locked goal and can consume Marks again.
- Reproduction: create an old save owning `lightning_mast`, migrate it with empty Marks-owned IDs, earn 60 Marks, then select/purchase the suggested results unlock. The already usable weapon is charged for again.
- Missing evidence: migrate → results goal → purchase round-trip proving effective ownership prevents duplicate charging, not just offer eligibility.

## Evidence gaps, separate from confirmed gameplay defects

### G1 — P2: R6's named storm sail test does not verify contracted navigability

`Tests/PlayMode/SaltwindStormArenaPlayModeTests.cs:43–74` checks spawn/beacon against uncontracted `PlayBounds.IsInside`, then sets the safe scale to 0.4 and only checks that scalar. It never calls `IsInsideStormSafeZone` after contraction, moves a ship, checks collision/turning, or applies storm damage. `Scripts/World/PlayBounds.cs:84–93` still contracts an axis-aligned rectangle around the extents center without selecting navigable water.

Reproduction/checkpoint: run the named test with spawn/beacon outside the final rectangle; those positions do not affect its final assertions. Separately sail the authored scene at 720/810/900 seconds, inspect connected water and escape routes with the actual hull, and verify visual/damage boundaries. This report does **not** claim a specific shipped seed is impassable; that evidence is missing. Keep C05/C06 storm/navigation sign-off open.

R1's `NineMinuteAmbient_UsesSingleBandFourExportRate` is also narrower than its name suggests: it checks helper quotes at 540 seconds, not nine minutes of actual EncounterSpawner output. The exclusive-window source fix is present, but live rate/blocked-placement behavior still needs integration evidence.

### G2 — P2: Matrix summary omits important live/simulation differences

`Simulation/Combat/CombatSimulator.cs:868–874` ends Saltwind simulations immediately on boss victory. The matrix cannot measure post-victory survival, Drowned Pursuit, storm deaths, or a 12–15-minute successful-run median. `Simulation/Balance/BalanceLabResults.cs:99–107` labels a result survived when it did not end in PlayerDead; a boss-clear stop is therefore counted as survival. Do not interpret this as survival to 15 minutes.

The simulation also clears ambient enemies and snaps the player at boss spawn (`CombatSimulator.cs:324–329`), stops ambient pressure during boss combat (`SaltwindLabSpawnParityLogic.cs:28–32`), and varies hull by skill/content pool (`SaltwindLabMatrixCalibration.cs`). These are accepted calibration abstractions, not proposed reversions to v5/v6. However, `SaltwindBalanceLabBatchRunner.cs:32–34` includes only movement, collection, and offer disclaimers in the summary. Even the existing `BossTiming` disclaimer is omitted, and it does not explain victory termination or skill-dependent hull values.

Needed evidence: explicit summary labels for the above limitations; matching live telemetry for economy, spawns, progression and encounters. `SaltwindLabTraceParityLogic` compares helper formulas, not captured runtime traces. Preserve band warnings as advisory.

### G3 — P2: Independent Unity test reruns could not complete

The connected Editor initially reported ready/stopped, Unity 6000.3.11f1. `unity` was not available on this shell's PATH, so equivalent official Unity MCP commands were attempted:

1. `run_tests(mode=editor, filter=Saltwind, async_tests=true, timeout=300)` — MCP timed out after 300 seconds; no result set returned.
2. `cancel_tests` — main-thread command timed out after 60 seconds.
3. Targeted `NineMinuteAmbient_UsesSingleBandFourExportRate`, editor/testName, 30 seconds — main-thread timeout.
4. `run_tests(mode=playmode, filter=Saltwind, async_tests=true, timeout=30)` — main-thread timeout.

`test_status` continued reporting running. The Editor log independently confirmed main-thread operation timeout. The Editor was not force-closed or restarted, and no result was inferred from Cursor's summaries. R1, R2, R6 and the exact Conservative/Expert NUnit gates remain **not independently passed**. No current all-assembly pass is claimed. Recover/cancel the Editor run safely, then rerun narrow correctness fixtures before the slow matrix fixtures, preserving XML/results and timestamps.

The game's complete Editor PlayerPrefs registry key was backed up outside the repository before the PlayMode attempt. A subsequent export was byte-identical, so no save restoration was needed. Raw save contents were not added to review artifacts. Machine-readable attempt evidence: `CODEX_INDEPENDENT_EVIDENCE_2026-09-23.json`.

One source-level test mismatch is already visible: `SaltwindBalanceLabGateTests.cs:83` asserts Conservative boss hull is less than fresh Expert hull, whereas intentional v6 factors are 1.28 versus 1.0. Reconcile that assertion with the accepted v6 design; do not undo the arena snap/retune merely to satisfy a stale assertion. No NUnit failure result is claimed for this inspection.

## Confirmed changes and scope checks

- Authored Saltwind scene contains `StageBalanceProfileHost` pointing to `SaltwindSoloStageBalanceProfile.asset` (scene lines 6090–6093). Bootstrap resolves/begins it for solo; multiplayer bypass and context teardown code are present.
- The spawner's scene schedule field is null and loads the Resources schedule. Its serialized contents currently match the Data schedule referenced by the balance profile byte-for-byte; they remain separate asset identities. No current value mismatch was found in that comparison.
- Profile balance version is 2. SHA-256 of the trimmed canonical export bytes equals `e4ffa52b4475d137e89c960eebf2de9ee42a3280c9f859cb5ca1761c1baa7eb7`. Direct authored settings match the stated rates, counts, prices, slots, timings and unlock costs. Full live `ToExport()` recomputation is pending while the Editor is blocked.
- Schedule expiry is checked before cap rejection; pending encounters retain remaining counts and have a timeout; ambient reserve exists; post-formation placement validation is present. These address substantial portions of original R1/R2, but F1/F2 remain.
- Marks record reads are now pure; the final ordinary-minute condition is corrected; settlement receipts guard duplicate payments. Effective ownership includes legacy unlock/purchase arrays. Complete old-save/menu/snapshot/other-mode round trips remain unverified.
- Marked boss area damage and a world telegraph now exist; normal debug controls require a developer preset. Scene boss-death integration tests now exist, and PlayMode assembly configuration changed. Their presence is not execution evidence.
- Baseline-to-HEAD diffs of `Scripts/Player/PlayerShipController.cs` and `Scripts/Player/FollowCamera.cs` are empty. Captured ship/damage/camera scene field names were compared to baseline `701bfa7d` with no changes found. This is source/serialized-field evidence, not a new live handling test. Project-wide `runInBackground` changed from 0 to 1.
- The original review was left untouched. Checkpoint coverage was inspected through the handoff, implementation log, index, resolution log and associated evidence files; there are no separate C02–C12/C14-named checkpoint files in this checkout. Several older evidence notes still describe the superseded zero-PlayMode/1200-run state.

## Calibration, performance and human experience

- v5 boss arena snap is present in `SpawnBoss`; v6 hull constants are present; the menu sanity text says **Lab calibration v6 boss hull retune**. The former 0% Conservative result is not being re-raised as an unresolved defect.
- Committed `balance-lab/` reports 1800 runs with Conservative mostly 99%, consistent with the previously described post-v5 output. Both it and the v6 source were last committed in `c69d5668` at 2026-09-23 16:08 +07:00. Its metadata has no generation timestamp or calibration revision/commit; therefore this review cannot establish whether those particular artifacts were rerun after v6. Profile hash alone cannot distinguish lab-only changes. Use the separately captured current-assembly results below instead of assuming the committed matrix proves v6.
- An independent existing-assembly .NET run against the committed export yielded **7/8 Conservative/Broadside/FreshSave boss victories**, seeds 12000–12007. This supports the arena fix but is not an execution of the named Unity NUnit test. A separate **completed 1800-run matrix** is recorded under `independent-v6-net/`, with assembly/runtime provenance and `RERUN_INDEPENDENT_LAB.ps1`; the original committed matrix is preserved. Conservative cells are 99% except AreaControl/MarksUnlocked at 96%. Expert cells are 71%/72% Precision, 68%/54% Broadside, 71%/68% AreaControl (FreshSave/MarksUnlocked). These band warnings remain advisory. The compiled assembly exposes the v6 Conservative boss scale 1.28; this is not a fresh Unity rebuild or exact NUnit execution.
- Cross-runtime note: .NET and Unity/Mono serialize several floats differently. The .NET codec re-hash differs despite equivalent parsed settings. The pinned hash above was verified from the actual canonical bytes; do not replace it with the .NET reserialization hash.
- A separate 12-seed-per-cell .NET sample (216 runs) applying the Expert band condition found Broadside/MarksUnlocked **4/12 = 33.3%**, below 40%; the other five Expert cells were inside the band. See `independent-v6-net/expert-12-sample.json`. This is advisory sample calibration, not a passed/failed execution of the Unity NUnit fixture, which uses a built-in profile rather than this authored export.
- **Device performance remains pending:** `PERF_TRACE_PENDING.md`. No standalone desktop frame-time/GC/memory trace or mobile hardware measurement was produced.
- **Human experience remains pending:** `C13_EXTERNAL_PLAYTEST.md`. No newcomer sessions, first-victory attempt count, death readability, exploration preference or voluntary retry observations were fabricated or inferred from simulation.

## Recommended next order (not implemented)

1. Repair F1/F2 and add real POI integration regressions; isolate PlayMode saves before rerunning settlement tests.
2. Repair F3–F5 and F7 with identity/cap/timeline/migration regressions. Strengthen G1's storm verification.
3. Recover the Editor test runner; run targeted R1/R2/R6, full relevant assemblies, actual boss death/late victory/simultaneous death, pause/retry and settlement gates. Recompute the authored export live.
4. Update matrix provenance/disclaimers and compare representative live telemetry. Tune remaining win-band warnings only as desired; perfect band fit is not a blocker.
5. Capture desktop/mobile performance and conduct the external newcomer panel. Then assess the 3–5-attempt and 12–15-minute experience targets.
