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

## In progress

- **a.** The PvP agent: descriptions on the draft cards, a full read of the PvP flow, and two reviewer findings on the draft
  screen (saved drafts can still send the flamethrower; the card hover blocked scrolling on phones).
- **d.** The APK starts once (a) is in.

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
