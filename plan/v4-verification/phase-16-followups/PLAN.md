# M16 — M15 playtest follow-ups: plan and progress

Started 29 Sep 2026 at `4e74a42a` (M15 committed after your playtest). Scope from your M15 playtest feedback:

1. **Save safety** — your Hank unlock was lost between the M14 and M15 playtests (you re-bought him for 100 Rum). The save
   code did not change in M15 and the cause could not be found from what was left to inspect (no standalone save, no
   prefs-guard backup, no pending trace-session snapshot; every PlayMode test class runs on an in-memory save). So M16
   makes a silent loss impossible and reports any attempt.
2. **Edge wall** — your decision: steady damage while outside the red edge wall, at any time, with a warning toast
   (before: V3's open edge — the ship could sail past the wall and nothing happened until the storm).
3. **Content ids into config** — the 17 Foundation files that still key V3's data tables by content id move onto the
   V4 configs with no gameplay change.

Verification (phase protocol): targeted tests per step, full V4 suites, an independent review, one trace set at the end
(this phase replaces V3 data tables; under the v3-parity preset the runs should equal M15's traces), then your playtest.

## Sub-steps
| Step | Content | Status |
|---|---|---|
| 16.1 | Save safety | **Done**: `SaveService.Save()` never drops owned content (unlocks, purchases, Rum-bought content) or settled-run receipts that the stored save holds; such a write keeps them and logs an error with the writer's stack trace, so a repeat names the culprit. Snapshot restores by the test helper (`V4TestSaveIsolation`) and the Editor trace session go through the same guard (`SaveService.RestoreSnapshot`), and the trace session never deletes the save key. The V3-era `tools/prefs-guard.ps1` (replaced the whole registry hive around V3 test runs) is deleted. Note: the guard protects ownership and receipts, not the Rum balance (a stale copy could still write an older balance; it would now be reported). Tests `V4SaveSafetyTests` (stale copy, snapshot restore, normal writes silent). |
| 16.2 | Edge-wall damage | **Done**: `StormSettings.edgeWallDamage` (code default off = V3; on in the Saltwind timeline; the v3-parity preset turns it off), `edgeWallDamagePerSecond` 8 (+8 % per metre beyond the wall, like the storm), `edgeWallToast` "OFF THE MAP — turn back to open water!". Applied in the storm-edge frame step after the 8 s run grace; during the storm, outside its safe zone, only the storm's damage applies. Test `EdgeWall_DamagesOutsideItAtAnyTime_ButNotTwiceInTheStorm`. |
| 16.3 | Content ids → config | **Done** — the content-id allowlist is empty (was 17 files). Audit: `AUDIT.md`. **Dead, deleted:** `WeaponDisplayNames`, `EnemyRangedAttackRules`, `SpawnProfileResolver` (the struct stays), `EnemyCombatProfileRegistry` (the run uses per-run profiles from config), `WeaponEvolutionCatalog` (with the uncalled item/logbook unlock checks), the ship's starting-weapon initializer, `EnemyTierLogic.CanSpawn*`, `StageBalanceProfile.CreateBuiltInSaltwindDefaults`, the V3 weapon feature flags (two V3 weapons V4 does not have). **Unreachable fallbacks removed, with guards:** the GameDatabase stub (both database assets tested valid; a missing one now fails the run with an error), the stage profile's built-in id lists (always overwritten by `StageContentConfig.sharedProfile`; starters tested equal to `UnlockConfig`), V3's unlock table outside a stage-profile run (never asked in V4), the "deck_cannons" empty-starter fallback, the proxy-model table (every runtime enemy tested to have its model; the archetypes tested to carry V3's proxies), the harpoon trail check by id (now by prefab; the binding tested). **Into config:** the stage boss id (`sharedProfile.bossEnemyId`; the pinned profile hash is unchanged), the aim overlay colours (`WeaponConfig.aimOverlayColor`, filled from V3's palette; tested), the spawn-warning tier (from each archetype's/variant's `spawnTier`; compared with V3's table for every runtime enemy before the table was deleted; the registry elites stay Soldier as V3 read them). **Catalogs removed:** the runtime database now creates the eight specialist aliases itself (`EnemyVariantConfig.databaseSlotOrder` keeps V3's catalog order) — proved equal to the catalog path slot for slot and field for field (and the simulation rules) on both the Editor and the player database before both catalogs were deleted. Only visible change: the results-footer profile hash on the second and later runs of a session now equals the first run's (register R-73: the weapon catalog mutated the loaded database). EditMode 305/305. |
| 16.4 | Suites, review, trace set, playtest | Final after the review fixes: EditMode 307/307 (V4 285 + Simulation 22), PlayMode 49/49. The one trace set (v3-parity preset) **matches M15 exactly** on all three seeds (`traces/trace-comparison.txt`). Independent review done (below). |

## Independent review (29 Sep)

A read-only reviewer checked the three parts against this plan, the audit and the code. No blockers. Edge wall: clean (with
the switch off the storm-edge step is V3's, same grace and order; the bounds test is the red wall's inner face; no double
damage; god mode, pause and run end respected). Content ids: no hidden gameplay change (only the spawn warning reads the
new tiers; the catalogs fed nothing V4 reads except the profile hash taken before them; the Editor and player databases
both tested). The trace set confirms it: identical to M15.

| # | Finding | Fix |
|---|---|---|
| 1 | Major: the Editor trace session restored a pending prefs snapshot whenever *any* Play session ended (the player's own playtest) and at Editor start, and the manual test-run guard never restored itself — a stale snapshot could write old Rum, lifetime gold, first conquest, tutorial and home seed back (the save guard only kept ownership and receipts). **This is the likely cause of your lost Hank unlock** (it also refunds the Rum spent, matching the re-buy). | The session marks when it starts a trace or playable check and restores only then; any other pending snapshot is discarded with a warning; crash recovery at Editor start restores only a snapshot under 3 hours old; starting a trace discards a stale pending snapshot instead of restoring it; the manual guard (only for V3's test suites) is removed. Tests: `AStaleTraceSnapshot_IsDiscarded_NeverRestored` (both paths), your real save checked untouched afterwards. |
| 2 | Minor: the save guard merged from the stored save without migrating it (a pre-v2 save) | The stored copy is migrated first |
| 3 | Minor: a corrupt stored save made a snapshot restore throw | `RestoreSnapshot` logs and leaves the stored save as it is |
| 4 | Minor: a snapshot restore brings back the old Rum balance (keeps new unlocks) | Documented; with finding 1 fixed, restores happen only after this session's own traces |
| 5 | Minor: `run-traces-parallel.ps1` replaces the standalone player's registry key around trace sets | Not changed: trace players run on an in-memory save and no standalone save exists; noted |
| 6 | Minor: a config set without a stage config would now give empty starters and boss id silently | The run fails with a clear error |
| 7 | Minor: stale comments and test helper names mentioning the catalogs | Fixed |
| 8 | Minor: the alias test checks ids, tags and names only (the field-by-field proof ran before the catalogs were deleted) | Kept as is; the trace set also matched |

## Your playtest (29 Sep)

| Check | Result |
|---|---|
| Sailing past the red wall early: the hull drops, the warning shows, it stops back inside, god mode blocks it | Yes |
| Stop and restart Play mode: Captains and Rum exactly as left | Yes |
| A normal run (specialists, spawn warnings, the boss) plays as before | Yes |

M16 passed your playtest → committed `V4 M16: M15 playtest follow-ups`.
