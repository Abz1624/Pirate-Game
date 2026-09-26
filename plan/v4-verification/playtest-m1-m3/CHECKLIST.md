# M1–M3 combined playtest: decisions and fixes

| Item | Value |
|---|---|
| Branch | `v4-modular-architecture` |
| Start commit | `a637154c` (M3 playtest steps) |
| Status | **Passed** (27 Sep): traps visible, overtime feel accepted for now; values to be tuned during balancing |

## Your decisions, as applied

| Id | Decision | What changed |
|---|---|---|
| R-02 | Keep the Reef Crab at 2:30 | Nothing (V3 value kept) |
| R-03 | Real elites | The 3:00 and 8:00 beats spawn `elite_hammerhead` / `elite_kraken_spawn`: their own run-database definitions (appended after V3's slots), flagged elite by their beat spawners (Rich affix, elite HP, elite bar, forced elite drop). Their elite combat profiles now run: second charge, sequential traps. Marks still credit them |
| R-05 | Drowned enemies in overtime, placeholder models | The overtime spawners anchor `drowned_pursuer` / `drowned_harrier`. **Placeholder models, to replace later:** pursuer = SmuggleBoat ×1.05, harrier = CannonboatNavy ×0.95, marked `visual.placeholderModel` on the archetype assets |
| R-57 | Steady storm damage | `storm.damageMode = Steady`: HP drops every frame, no hit immunity given or used, so storm damage no longer blocks enemy hits. Feedback (damage numbers, hit flash, on-damage relics) and telemetry get the summed amount every 0.5 s |
| R-75 | No timeout; overtime never held back | Overtime spawns ignore arenas (EditMode test `Overtime_IsNotHeldBackByAnArena`) |

The `v3-parity` preset reverts all of it, so V4 can still be compared with V3 exactly.

## Your playtest issues

| Issue | Finding | Fix |
|---|---|---|
| Editor crashed on F1 in V4Boot | A D3D12 GPU hang while four trace players were rendering at the same time. It was not the console | Trace players and Editor play sessions never overlap now |
| Skip on level-up cards did nothing | V3 bug (R-83): skipping the last pending level-up resumes the run but never hides the cards | V4 hides cards left showing while the run is unpaused with nothing pending |
| Hammerhead values stayed after Reset | "Write back" had saved them into the asset | `hammerhead.asset` restored to the defaults (HP 14, damage 7, speed 5) |
| F1 window too small | — | Drag any edge or corner to resize; size and position are remembered (saved on close too), always kept on screen. Default 640 px wide, full height |
| FPS dropped to 12 | The concurrent trace players. V4 and V3 both run at about 200 FPS; the open console costs about 35 % | — |
| Invulnerable at low HP | The storm's per-frame hits used the 0.2 s hit immunity, so enemies could not hit you (R-57) | Steady storm damage (above). God mode in F1 is the only other invulnerability |

## Independent review

No blockers. Fixed:
- **Major:** steady storm damage fired the "player hit" feedback 60 times a second → throttled to 0.5 s; now also on the V4 `PlayerDamaged` event (B = 1).
- The per-frame skip check re-wired the level-up modal every frame → pause/pending checks first, modal cached (and looked up again when V3 replaces it).
- The elite Kraken would ram for damage (V3's elite entry left the default on) → keeps the plain Kraken's rule (R-77). The elites lose their beat-spawner first-attack timers (V3 code rule, recorded in R-77, revisit in M8).
- Missing tests → PlayMode `Overtime_SpawnsTheDrownedEnemies` (jump to 10:01), `SteadyStormDamage_RespectsGodMode_ThrottlesFeedback_AndCanKill`.
- F1 window: saved on close, clamped fully on screen, resize ends if released outside the game view.
- Register: R-77 resolved with R-03; R-57 and R-81 updated.
- Re-running the M3 migrator now re-applies these decisions; the parity preset's phase indices are guarded by a test; the runtime database can be built twice safely.

## Results

| Check | Result | Evidence |
|---|---|---|
| V4 EditMode | 83 / 83 | `editmode-results.json` |
| V4 PlayMode | 14 / 14 (new: elite beat and overtime enemies, Skip hides the cards, forced overtime spawns Drowned, steady storm damage) | `playmode-results.json` |
| Traces | V4 with the v3-parity preset == V3 goldens, seeds 101/202/303 (MATCH ×3); replay MATCH. Default runs differ from M3 on purpose from the 3:00 elite beat. Recorded on the build before the review fixes; those fixes don't touch the parity path (storm `PerFrameHit`, no elites/Drowned under the preset), so traces were not re-run | `trace-comparison.txt`, `player/` |
| Playable check (targeted, about 2 min) | Console at its default size; elite beat → `elite_hammerhead`, elite; jump to 10:01 → 2 pursuers + 2 harriers alive at 10:06. Console: 0 errors | `playable-check/` |

**Trace policy from now on (your request):** individual fixes are verified with targeted tests that jump to the moment (for example, overtime at 10:01) instead of full runs. The short parity trace set runs once per phase, at its end, for phases that replace V3 code. The 15-minute long runs are no longer routine; overtime and storm are covered by jump-to-time tests.

## Re-check in the playtest

1. Level up and press **Skip** on the last card: the cards disappear and the game continues.
2. F1: drag the window's edges and corners, close and reopen it (the size is kept).
3. At 3:00 the elite Hammerhead (elite health bar, charges twice); at 8:00 the elite Kraken (sequential traps, no ramming damage).
4. F1 → Jump to 600: overtime brings Drowned ships (placeholder models).
5. Jump to about 740 and sail outside the safe water: HP drains steadily, damage numbers about twice a second, and enemies can still hit you.

## Re-check feedback (second pass)

| Your note | Finding | Change |
|---|---|---|
| Skip, F1 resize | Working | — |
| No traps from the elite Kraken | V3 defect R-06: each Kraken trap cast reserves one of two area-denial slots and never releases it, so after two casts per run no Kraken lays traps again; ordinary Krakens use both slots before 8:00 | Slots are freed after 2 s (`timeline.areaDenialReleaseSeconds`). Measured: the elite Kraken lays a 3-circle trap every ~6 s (`balance-probe-traps/`) |
| Overtime felt slow; died instantly at ~12:20 | Measured at 12:43 (`balance-probe/`): a Drowned pursuer hit for 145 and a harrier for 212 against 100 hull (V3's never-used Drowned stats, about 2× damage and 3–4× HP of the shark/cannonboat V3 used). The storm drains only 10–14 hull/s, 7–10 s to kill from full | Your choice: base stats = shark/cannonboat, plus a smooth overtime ramp 1× at 10:00 → 2× at 15:00 on HP and damage, evaluated at each spawn. V3's growth curve kept (your choice) |

Pursuer damage per hit after the change (hull 100), measured (`balance-probe-after/`): **47 at 10:42, 124 at 12:50**; harrier 65 and 164. Expected from the formulas: 36 at 10:00, 59 at 11:00, 90 at 12:00, 133 at 13:00, 190 at 14:00. So from about 12:00 one Drowned hit still takes most or all of a 100-hull ship: that is V3's growth curve times the ramp you chose. Note that a jump starts you at level 2 with no items; in a real run you have more weapons (enemies die faster) but the same 100 hull unless you picked hull upgrades. Both are console values (`difficulty` curves, `timeline.overtime.spawnStatRamp*`) if it feels too harsh.

Tests: EditMode 85 / 85 (new: ramp shape and preset, area-denial release and V3 mode), PlayMode 14 / 14. Traces not re-run: with the v3-parity preset the ramp is 1 and the release is off, so V3 behavior is unchanged (EditMode tests cover both); the short parity set runs at the end of M4.

## Third pass: elite Kraken traps still not visible

The traps were firing (a 3-circle trap every ~6 s after the R-06 fix), but V3 draws nothing for a pending hazard: no marker while the fuse runs, only an explosion if the ship is still inside. So they were invisible (R-85). V4 now draws every pending hazard (Kraken traps red, mines amber, nets blue) as a ground ring plus an inner ring that grows to the outline as the fuse runs down. Screenshot: `kraken-trap-visual/01-kraken-trap.png` (8:04, two rings beside the ship, the third under it). Visual only, so traces are unchanged. Tests: EditMode 85 / 85, PlayMode 14 / 14.
