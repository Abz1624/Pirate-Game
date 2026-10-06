# Round 8 log (4 Oct, while the owner is away)

Requests: (a) PvP draft shows weapon and Logbook descriptions, and check that 1v1 PvP works; (b) health bars only on elites,
minibosses and bosses; (c) independent review of the project; (d) Android APK for testing.

## Done

- **b. Health bars**: elites (an elite affix, or the elite_hammerhead / elite_kraken_spawn variants), minibosses and the
  boss now get a bar; ordinary enemies do not. Commit `49e777be`.
- **c. Independent review**: done (read-only reviewer). No blockers. Server tests 69/69 pass. Fixes applied in `4909be9a`:
  - **Mistake of mine, now fixed**: the Barnacle King commit (`ebe52d58`) also halved its **damage** (you asked only for
    health). The cancelled "damage" command had already edited the file before you stopped it, and I committed it without noticing.
    Damage is back to 1.125; health stays halved (245 hull, boss floor now 4.75 s after today's floor halving).
  - The flamethrower could still enter a 1v1 through a draft saved before round 7 (or a rematch). The match now skips
    switched-off weapons for the starting weapon and the drafted loadout. (The draft screen fix is with the PvP agent.)
  - The island registry (only real islands sink you) is now cleared per scene; it grew every run.
  - The boss now keeps an exact aim like elites (the 8 degree spread was meant for ordinary shooters only).
  - Release builds accept `?api=` only for the production API host, not any `*.up.railway.app` site (a crafted link could
    otherwise collect guest tokens).
  - A 1v1 run that starts without its match now goes back to the menu instead of hanging.
  - The in-engine balance simulator honours the flamethrower switch.
  - The balance simulator's data is refreshed (local file only; the published page is not republished yet).
- Earlier today (for reference): Barnacle King hull halved `ebe52d58`; all fight-length floors halved `dc0f3f7c`
  (boss 4.75 s, arena 3 s, scheduled 1.5 s, kill-band elites 1.25 s).

- **a. PvP** (commit `9103bbd7`, compiled in the Editor, server tests 70/70):
  - **Draft descriptions**: each card shows its name and a short line (weapons: their firing line; Logbooks: e.g. "+10%
    Damage per rank"). A detail strip above the message line shows the hovered card (desktop) or last tapped card (phone):
    full description and level 1 numbers for weapons; bonus per rank and rank cap for Logbooks. Picking works as before.
  - Saved drafts drop switched-off weapons (flamethrower); a draft left short counts as incomplete. Card hover no longer
    blocks scrolling on phones.
  - **Bugs fixed** from reading the whole 1v1 flow:
    1. A player who reconnected around the end got no result and kept sailing. **Needs the server redeploy** (see below);
       until then the client shows the end screen after 3 s without real stats.
    2. A match that ended while the run scene was loading (opponent left during the countdown) never ended.
    3. A voyage started while still sitting in a lobby could lose a rated match on time; voyages now leave the lobby.
    4. A guest stayed on a dead lobby after the host closed it; now goes back to the PvP hub with the message.
  - Left as is: pausing in a 1v1 stops your own ship while hits still land (pause is the only in-match menu; Quit forfeits).

## Build

- **d. Android APK**: built 03:23 (37 min, succeeded, 0 errors; IL2CPP ARM64, ASTC textures, 131.6 MB). Copy for testing:
  `Pirate Game Unity/Builds/Playtest-2026-10-03/RoguePirates-Playtest-Android-r8.apk` (same file as
  `Builds/Android/RoguePirates-Playtest.apk`). Includes everything up to commit `9103bbd7`. Not installed or run on a phone.

## Needs you

- **Railway redeploy of the API** (for PvP fix 1). My deploy was blocked by the permission check (production deploy), so I
  left it. From `server/`, with `.env` loaded: `railway up --service api --ci`. Everything else works without it.

## Decisions

1. **"Health indicators only on elites, minibosses and bosses"**: read as adding elites (they had no bar since round 7);
   ordinary enemies stay without one.
2. **Ember stays free while the flamethrower is off.** Her lock was "own the flamethrower", which can no longer be bought.
   See question 1.
3. **Not fixed now (logged for later)**, all minor or needing a server redeploy:
   - PvP referee trusts the damage each client reports (up to 500 per hit batch, 20 per second; no range or draft check). A
     modified client could win rated matches. See question 2.
   - A failed rematch creation on the server can't be retried (needs a server fix and redeploy).
   - Damage over 500 in one 0.1 s batch is dropped, and "damage dealt" counts hits the server refused.
   - Attack warnings share 48 slots with damaging circles and mines; if all are full a damaging hazard is dropped.
   - Elites are recognised by the `elite_` id prefix (a config flag would be cleaner).
   - Dragon's Breath Nozzle (the flamethrower's evolution item) may still drop from chests.
   - One server test failed once and passed on three reruns (timing-sensitive; worth hardening later).

## Questions for you

1. **Ember**: keep her free while the flamethrower is off, or lock her behind something else (Rum price, another weapon)?
2. **PvP anti-cheat**: how much server-side checking do rated 1v1s need during the playtest? A damage-per-second cap by
   level, a "weapon must be in your draft" check and a rough range check are cheap; they need a server redeploy.

## Unity tests likely broken by today's and round 7's changes (not run)

- `V4RewardsM12Tests.CaptainLocks_FollowTheAdoptedRule_AndV3sUnderThePreset` and
  `MenuServices_ApplyTheSessionPreset_ToTheCaptainLocks`: Ember no longer locked.
- `V5MetaShopTests` (every weapon unlock sold): the flamethrower is not sold.
- `V5PressureTests.TheData_HoldsEveryM3dValue`: balance values changed (probably failing since round 2 already).
- `V5FeelPlayModeTests.HittingAnIsland_BouncesBack_AndCostsHull`: may pick an enemy model as the "shore".
- From round 7: `V4HudM6Tests.IconCatalog_HasAnIconForEveryWeaponAndChestItem`, `V5WeaponMechanicsTests.TheNewWeapons_AreBuyable...`
  (icons removed on purpose), possibly `V4WorldViewsM14Tests` (hazard and shot shapes).

---

# Rounds 8b and 9 (4-5 Oct)

## Changes

- **8b** (`5225c24e`): Set Sail opens the Captain select directly (no mode panel); PvP keeps its own menu button; PvP,
  Leaderboard, Ledger and Raid share one width that fits "LEADERBOARD" (the PvP button had squeezed the column).
  Builds r9: Android, WebGL desktop and WebGL mobile in `Builds/Playtest-2026-10-03/`.
- **9** (`203e1874`): salvage chests are back (landmarks, the Beacon and the Powder Cache stay out); the flamethrower is
  back on, so Ember is locked behind owning it again (supersedes Decision 2 and Question 1 above).
- **Review fixes** (`955b82ea`): "chests only" is a DemoJuiceSettings switch (`chestsOnlyPointsOfInterest`, on), so the
  Module Lab and tests keep every point of interest; the landmark tutorial step is skipped and the Beacon step stays
  silent until the boss rises; the quest line reads "Survive until the Barnacle King rises (14:00)"; a PvP player who
  reconnects after the end sees "Match over" (not "Draw") until the result arrives, and the panel refreshes when it does.

## Unity tests (5 Oct, in the Editor; no builds)

- **EditMode: 438/499 pass** (`plan/playtest/tests/editmode-r9c.json`). The first run had 90 failures; about 30 came from
  one stale entry in the v3-parity test preset (it zeroed an opening-wave pool that round 6 removed), fixed in
  `9e8e01ae`. Before the playtest rounds (1 Oct) 462/463 passed.
- **PlayMode: 51/63 pass** (`plan/playtest/tests/playmode-r9.json`).
- **Every remaining failure asserts an old rule or number the playtest changed on purpose**; none shows a crash or broken
  behaviour: balance values and pacing targets (enemy HP and damage, XP table, boss HP, spawn density, levers), all
  weapons free starters (shop and Ember-lock tests), icons removed on purpose, health bars only on elites, minibosses and
  the boss, summons waiting for their set to despawn, evolution at max level, the 15-minute voyage (overtime 15:00, storm
  17:00; tests jump to the old 10:00), content ids in the enemy colour table (architecture rule), and CardStatLines
  reading RunManager (architecture rule).
- These tests need rewriting to the playtest rules once the playtest settles (or the playtest values moved into a preset).

## Still open

- **Railway redeploy** of the API (PvP result resend after a reconnect): `railway up --service api --ci` from `server/`.
- The balance simulators (in-engine SimRun and `plan/playtest/montecarlo`) still model landmarks and the Beacon, so
  forecasts are a bit optimistic (no Repair Wreck heal, Signal Buoy boosts or landmark Rum in voyages now).
- Losing landmarks removes the in-run Repair Wreck heal and landmark Rum. Question: move a heal or Rum to chests?
- On phones, reading a draft card's description in PvP also picks it (tap twice to undo); a separate info button would fix it.
