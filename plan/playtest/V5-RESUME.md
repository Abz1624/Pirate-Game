# Back to the V5 plan: review, questions and to-dos (6 Oct 2026)

From the independent reviewer (read-only, 6 Oct; server tests 70/70) plus the open items of the playtest rounds.

## Where things stand

- **Branch**: `playtest` is `v5-live-game` plus 32 commits (nothing behind). About 13k lines of C# went in without a phase
  gate, with only 24 new unit tests (online and PvP).
- **V5 phases**: M0 to M3 done. M3b, M3c, M3d, M3e, M4, M4b, M5, M7 and M8 were built and are waiting for your playtest. The playtest
  **reversed** some of them:
  - M3c's level rhythm became the Survivor.io curve (about 31 level-ups by 10:00 instead of 13).
  - M3d's "no HP growth" and the 10-minute sketch became HP growth and the 15-minute timeline.
  - D17's evolution at weapon level 8 became "at max level"; its 10 to 12 Logbook ranks became 5.
  - M4's "role ±20%" became "equal level-1 damage"; all 19 weapons are free starters.
  - M7: Esc pause, minimap and barks removed. M8: no mode select (Set Sail goes to the Captain select).
- **Built ahead of the plan** (M9 to M28 are not started officially): guest profiles (M10b), weekly leaderboard (M18), lobbies
  and invite codes (M19/M21), 1v1 Arena with draft and Elo (M22), and the API live on Railway.
- **Not in the plan at all**: the phone performance phases M7b, M7c, M7d (done, with evidence).

**Works now**: the 15-minute voyage (bands, hordes, crab and Kraken minibosses, kill-band difficulty, timed heals, item popup,
compact cards), all 19 weapons with visuals, guest profiles, weekly leaderboard, 1v1 PvP (lobby, invites, draft with
descriptions, relayed match, bot, rematch, reconnect), server tests 70/70, Android and WebGL builds.

## Problems the reviewer found

**Blockers before V5 phase work resumes** (none is a crash):
1. The playtest code went in without the phase gate and is barely covered by tests (no tests for the kill band, timed
   heals, PvP match controller, item popup or island registry).
2. Every PlayMode test runs with the demo switches **off**, so what you actually play (the Beacon or timed boss,
   chests-only, the whirlpool) is never tested.
3. Overtime Rum has no cap any more, against decision D4 (rewards that scale with a run must flatten and cap). Farmable
   once Rum becomes server value (M10).

**Other findings**:
- Architecture rules broken: enemy colours keyed by enemy id in code, elites recognised by an `elite_` id prefix, the
  weapon switch-off list in code, the card stat lines reading `RunManager` directly, `PvpSession`/`OnlineService` as
  singletons. Two tests catch the first ones; they are real failures, not stale ones.
- Every weapon is both a free starter and a priced unlock, so the shop lists each weapon twice, and Ember's "own the
  flamethrower" lock is always met.
- Both balance simulators still model the old rules; the in-engine one says a steady player survives about 28 minutes.
- Some tests jump the clock to hard-coded times from the old 10-minute voyage; they should read the times from config.
- The API runs on .NET 8 (support ends 10 Nov 2026), and its database is created with `EnsureCreated`, which cannot add
  tables or columns later. Move to .NET 10 with migrations before any schema change.
- The new deploy workflow deploys production on every push to `playtest`/`main` with no approval step.
- `status.json` and `V5_PLAN.md` were last updated 2 Oct.
- With the Beacon back, the boss can be summoned as soon as the Beacon is found; its halved health (245 hull, 4.75 s
  floor) was tuned for a 14:00 arrival and hasn't been rechecked.

## Proposed next steps (in order, each small and checkable)

1. Bring the notes up to date for today's change (all points of interest and the Beacon back) and re-run the tests on it.
2. Tag `playtest-r9`, then merge `playtest` into `v5-live-game` (no conflicts possible; one revertible merge commit). Don't
   merge to `main` yet (it is 355 commits behind). Point the deploy workflow at `v5-live-game` and add an approval step.
3. **M8z, a playtest integration phase**, before M9:
   - **a. Architecture fixes**: colours and an elite flag into the enemy config; card stat lines through views; the weapon
     switch-off list into config; `PvpSession`/`OnlineService` owned by the run's composition root.
   - **b. Demo switches into a "playtest" preset**, so tests cover what ships.
   - **c. A Scenario Lab**: a scenario asset plus a test scene that sets the seed, Captain, loadout, level, clock time,
     chosen enemies at a set distance, forced points of interest, switches and a PvP bot over a fake relay. The same
     scenarios work from the F1 console and in 2 to 10 s PlayMode tests (combat, director, stage, economy and PvP tests
     listed in the review). The Module Lab and the F1 console already cover part of this, interactively only.
   - **d. Test triage** of the 73 failing tests: fix the real ones, rewrite value pins as rules that read a balance-targets
     asset, retire the rest, and mark slow tests so they run only at the gate.
   - **e. Shop and economy data** (needs your answers below).
   - **f. One simulator**, recalibrated on your play sessions (the session recorder in progress feeds this).
   - Then the reviewer, your playtest and the commit.
4. **Plan revision 10**: record the playtest decisions and which plan decisions they replace, add M7b to M7d, recast M9
   around the existing relay, and fold the existing API into M10, M10b, M18, M21 and M22.
5. Then M9.

## Questions for you

1. **Do the playtest rules replace the plan?** M3c (level-up rhythm), M3d (no HP growth, the 10-minute sketch), D17
   (10 to 12 Logbook ranks, evolution at level 8). Or should any come back?
2. **Overtime Rum**: cap it again (D4), or record an exception before Rum becomes server value?
3. **Weapons**: all free (no weapon shop, Ember effectively free), or back to Rum unlocks? If back, which are starters?
4. **The v3-parity preset** (the "matches V3" regression baseline, D16): keep it, or retire it now the game has moved on?
5. **Multiplayer**: keep the WebSocket relay for unwagered 1v1 and test Netcode for GameObjects in M9 only for Hunt and
   wagers? Or replace the relay?
6. **PvP anti-cheat** for rated matches now: the three cheap server checks (damage per second cap by level, weapon must be
   in your draft, rough range check)? Needs a redeploy.
7. **Boss**: summonable at the Beacon at any time (now), or only after a minimum time? Re-check its health for an earlier
   fight?
8. **Phone rules**: with desktop WebGL as the main test target, do the touch rules (48/72 px buttons, safe areas) still
   apply to new screens?
9. **Auto-deploy**: deploy the API on every push, or only after a manual approval in GitHub?
10. **Merge**: OK to merge `playtest` into `v5-live-game` and continue there (step 2)?
11. **Railway secret**: please add the `RAILWAY_TOKEN` repository secret in GitHub so the deploy workflow can run (and tell
    me if `server/deploy.ps1` still fails, with the error text).

## Your to-dos

- Add the `RAILWAY_TOKEN` GitHub secret (question 11), or run `server/deploy.ps1` once. Either one deploys the PvP fix for the result after a reconnect.
- Play a voyage with the session recorder once its build is ready (the file lands in `plan/playtest/sessions/`, or downloads
  in the browser); I compare it to the simulator.
- Answer questions 1 to 10.
- Later: the PvP draft layout on phones (tapping a card to read it also picks it); you said it can wait.
