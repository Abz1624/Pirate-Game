# Your TODOs — V5 (updated 20 Oct, plan rev 20)

# Now (20 Oct): M9j, the server decides or checks results (plan rev 20)

- [ ] **Answer J1-J5** (plan section 9; details `plan/v5/phase-09j-server-results/PLAN.md`):
      J1 when (if ever) the server may sink a ship in wagered 1v1 from its hull estimate (shadow now);
      J2 what happens to a raid or race whose run log fails its checks (shadow now: the loot still moves);
      J3 the cost of the strongest step (server sessions on the M13b match server, about $1.5-1.9 per 1,000 sessions);
      J4 raid limits, built and off (`RAID_PAIR_LIMIT_PER_DAY`, `RAID_RECORD_ONE_PER_DAY`);
      J5 confirm the narrow exception to D19(2)/(8), and that a stake decided by client reports (a room lost on a
      restart) is refunded, even for an honest winner.
- [ ] **M9j check** on a desktop build made after `1a791034`: one raid and one daily race, then read the verdicts from
      the API (`/api/raids/mine` `logVerdict`, `/api/race/daily/board` `raceCheck`); the game does not show them yet. The early-guardian refusal is covered by the server tests (no live raid
      was run: it needs a wallet, an island and RUM).
- [ ] Note: the live check left a test entry "M9jCheck" (120 s, flagged `too_fast`, not checked) on the 20 Oct race
      board; there is no delete endpoint, it drops off with the day (the race pays nothing).
- [x] Migration `AddServerResultChecks` deployed 20 Oct: new nullable columns and a new table only (nothing overwritten).

# Now (14 Oct): M9h server safety done and deployed (plan rev 19)

- [x] H2 answered (13 Oct, D31): nothing switched off. Each flow is now a switch that defaults on: `WAGERS_HUMAN_ENABLED`,
      `RAID_LOOT_ENABLED`, `ISLAND_SALE_CARRIES_RUM`, `DAILY_RACE_FIXED_SALT` (state at `GET /api/switches`).
- [ ] **Read the M9h server report** in `plan/v5/phase-09h-safety/`: the live checks, every payout (15, 2.551 SOL, all
      sent) and Island NFT mint (8) since 12 Oct, the treasury (22.6 SOL; float 2 SOL, alarm below it). The top-up rule is
      in `server/README.md` (top up to at least twice the float the day it reports low).
- [ ] Optional: to turn a flow off, set its variable to `false` in Railway (api service, Variables) and let it restart;
      all four are already listed in `.railway/railway.ts`, so an apply keeps them.
- [ ] **Back up the Railway database** before the next migration. Note: the M9h migrations (AddSessionExpiry,
      AddMintSafety) went out on 13 Oct without a fresh backup: both add columns; one rotates the own token of wallet
      profiles (guests that linked a wallet now use their wallet session), one marks island owners and sellers as having
      used their free claim.
- [ ] **Two M9h review questions**: (1) the session migration replaced the own token of wallet profiles (guests that linked a wallet) without a fresh backup, and this cannot be undone: approve after the fact, and from now on count overwriting a column as data-destroying (backup and your OK first)? (2) Should the demo reset give a reset wallet its free island claim back (today it stays used)?
- [ ] M9h's gate: the full EditMode and PlayMode suites, the independent review, then your check (read the report; one
      voyage and one unstaked 1v1 vs Captain Marrow on the desktop build).


# Now (13 Oct, evening): review fixes landed, content back, suites clean (plan rev 18)

- [x] H8 and H9 answered (D30): the four weapons, the Cursed Chest Key, the four designed items and the Chaos Logbook
      are back; Ember keeps the flamethrower. Done in `2182f198` and `abb6ab02` (M5c content done early).
- [x] Deep review of the hackathon: blockers fixed (server money hotfix, client money path, gameplay); clean full run at
      `e851dcd3`: EditMode 835/835, PlayMode 132/134, server 227/229 (`plan/hackathon/review/tests-2026-10-13.md`).
- [x] Answer H2 for what is still live: answered 13 Oct, keep everything on (D31; done in rev 19, above).
- [ ] **Answer H10**: keep enemy damage x2 (then the simulator is re-fitted) or back to x1?
- [ ] **Answer the review questions** (plan section 9, R1-R4): R1 island value for SOL vs the RUM rule (D4); R2 NFT vs
      database ownership and royalties; R3 legal review of wagers, house stakes and treasury prizes; R4 a server treasury
      scan as a safety net.
- [ ] **Answer the other open questions**: H1, H3-H7, H11-H14 (plan section 9) and the 11 Oct ones in
      `plan/v5/OPEN-QUESTIONS.md`.
- [x] Check the pre-hotfix `unconfirmed` payouts: none left (14 Oct report: every payout since 12 Oct is sent).
- [ ] Back up the Railway database (moved up: see 14 Oct).
- [ ] **Device checks**: deposit, buy, list, cancel and an app kill mid-transfer on desktop WebGL and on Android (MWA);
      shop taps on a phone; the pause menu, level-up cards and storm edge warning on a device. Note: the 13 Oct hackathon
      builds answer `outdated_client` under run checks v9; use the next build.
- [ ] Close the stray Unity instance (pid 18388) if it is still running.
- [ ] Art: the pending list for the art team is `plan/hackathon/ART-PENDING.md` (page
      https://claude.ai/artifact/FxC5XjXzQ1nArDTAXtQ3cZ); weapon visuals are now editable prefabs
      (`plan/hackathon/WEAPON-PREFABS.md`).

# Now (13 Oct): hackathon folded into V5 (plan rev 17)

- [x] Hackathon demo (12-13 Oct): submitted; APK and desktop WebGL in `plan/hackathon/BUILDS.md`; merged into
      `v5-live-game` at `a5a8be02`. Retro and inventory: `plan/hackathon/RETRO.md`.
- [x] (Partly, see above: bot stakes off and weekly SOL for checked runs only, by the review hotfix.) **Answer H2 first** (plan section 9): which live devnet value flows to switch off in M9h (weekly SOL auto-payout on
      unchecked runs, staked 1v1 vs Captain Marrow and between players, raid loot, island sales carrying RUM, the fixed
      daily race salt, free claims whose mints the treasury pays). Then H8-H10 (weapons back on, Ember, enemy damage x2)
      before M9i.
- [ ] Answer H1 and H3-H14 (wallets going forward, wager rake, weekly prizes, islands, D4 and island sales, the daily race,
      the four switched-off weapons, Captain Ember without the flamethrower, enemy damage x2, the parchment UI kit as the
      standard, the renames, the other demo switches, the Hackathon assembly).
- [x] Read the deep review's findings when they land in `plan/hackathon/review/` (blockers in M9h, the rest in M9i).
- [ ] Say whether to drop the git stash "ui-redesign WIP" (superseded by the parchment kit redraws) and when to
      fast-forward `main` (still at `63e6962a`) to `a5a8be02`.
- [x] (Done early, 13 Oct, D30.) Parked content (4 items and the Chaos Logbook) is saved in `plan/hackathon/parked/` and planned as M5c after M9c;
      say if you want it sooner.

# 13 Oct: hackathon art (now part of M9i)

- [ ] Weapon art mapping: tell me which art goes on which weapon (some weapons were renamed). My guesses:
      Maelstrom Charge art -> `maelstrom_charge`; Mortar Barrage art -> `mortar`; Tidal Aura + fish bell -> `siren_bell`.
      Also say whether the not-yet-wired prefabs (Broadside Battery, Deck Cannons, Flamethrower, Flying Cutlasses, Harpoon
      Gun / Harpoon, Grapeshot) and the weapons with no art yet (Lightning Mast, Long Nine, Powder Kegs, Swivel Guns) should
      get art. Asset locations (under `Pirate Game Unity/Assets/`): `GameObject/Guns/Maelstrom Charge.prefab` +
      `GameObject/Guns/MaelstromCharge/`; `GameObject/Guns/Mortar Barrage.prefab` + `GameObject/Guns/MortarBarrage/Prefabs/`
      + `GameObject/Guns/VFX/Mortar_Explosion_VFX.prefab` (launcher `GameObject/Guns/Mortar.prefab`);
      `GameObject/Guns/TidalAura/TidalAura_VFX.prefab`; `GameObject/fish_bell.prefab` + `Mesh/Cursed/bell/fish_bell.fbx`.
- [ ] Icon mapping (13 Oct, the art team's UI icons): wired by content id in `V4IconCatalog`; these are waiting for you
      (files in `Assets/_Game/V4/Demo/Art/Icons/Unmapped/`, not in any build until mapped):
      weapons `Aura` (siren_bell?), `Ramming Prow`, `Mortar.png` (newer mortar? `mortar` keeps the current one, identical to
      `mortar(1).png`), `Boarding Party` and `Grapeshot` (already in `UI/New_items/weapons`); items `chain shott`
      (chain_locker?), `Devil's Match`, `Hammerhead Helm`, `Kraken Ink Sac` (cuttlefish_bone?), `Queen Anne's Fuse`,
      `Storm in a Bottle`; Logbooks `Big Game Hunter`, `Chaos`, `Echo Arsenal`. Please confirm `Shackle` -> `rusted_shackle`.
      No icon yet: weapons `siren_bell`, `squid_ink_pots`; items `chain_locker`, `cuttlefish_bone`, `cursed_chest_key`;
      Logbooks `admiral`, `extra_hands`. To wire one: save it as `Icons/<Weapons|Items|Logbooks>/<content id>.png`, then
      Tools > Rogue Pirates > Demo > Wire Icons.
- [ ] Check the art in the Editor: `Assets/_Game/Scenes/V4/Hackathon_ArtShowcase.unity` (Play), or V4Boot > Set Sail.
- [ ] Art team: Kraken_Crown `Kraken.mat` white emission (pale in game); CutthroatSkiff wake trail has no material; ship
      textures are 2048 px (WebGL caps at 1024).

# Now (11 Oct): M9b closing, M9c next

- [ ] Ram jolt (online): your ship can jolt 1 to 2.6 m when an enemy rams it. The server's knockback isn't predicted on
      your side yet. Tell me if it bothers you (owner asked to track it, 11 Oct).
- [ ] Answer the open questions in `plan/v5/OPEN-QUESTIONS.md` that are marked for you (added while you were away).
- [x] Re-test one server voyage (done 11 Oct: enemies smooth, tab switching fine, no pop-ins). Was: Re-test one server voyage from desktop Chrome once the main session has the new image and client on Railway (still
      protocol 5; the API needs the v5 run-check rules first): watch the enemies in normal sailing after a few card picks
      (the stutter came from the enemy clock after each pick; fixed), and whether any enemy ever appears out of nowhere on
      screen (the leash now puts them just outside the view). Details: `plan/v5/phase-09b-netcode/README.md`, "Enemy
      stutter fix and leash outside the view".
- [x] Decide (leash): (b), each enemy just outside the view along its own direction (owner, 10 Oct, D28 extended). Done: a
      4 m margin past the view of the widest screen (2.4:1) in `RunConfig`; 0 of 163,080 test placements land in view on
      4:3 to 2.4:1 screens, standing to full boost, the online lead included.

# Now (8 Oct): M8z closed, M9a next

- [x] Install the .NET 10 SDK (installed by the agent, 8 Oct) (`winget install Microsoft.DotNet.SDK.10`, then `dotnet --list-sdks` shows 10.0.x) so the
      M9a server upgrade can continue; Docker Desktop is optional.
- [x] Railway spend (set by you, 8 Oct): a $15 hard limit and a $10 alert (workspace Settings > Usage; D23). The limit covers the whole
      workspace, including the live API and Postgres.
- [x] Answer (8 Oct, D24: they pay out, flagged and logged): while run checks are in shadow mode, does a flagged voyage's Rum settle, or stay pending until you decide
      on enforcement? (Needed before M10.)
- [x] Answer questions P1-P10 (answered 6-7 Oct: D18, D19, D20).
- [ ] Answer the open questions in `plan/v5/OPEN-QUESTIONS.md` (17, each with a suggestion).
- [x] Deploy the API: deploys are done by the agent with `server/deploy.ps1` (D19).
- [x] Railway config migration (done by the agent, 8 Oct): `server/railway.json` replaced by `.railway/railway.ts`
      before Railway stops reading it on 1 Dec; deployed with the same settings. Nothing for you to do.
- [ ] Play a recorded voyage (Editor or the desktop WebGL build); the session file goes to `plan/playtest/sessions/`.
- [x] Playtest rounds 1-9 (1-6 Oct), see `plan/playtest/NOTES.md` and `ROUND8-LOG.md`.


## Next

- **Phone performance (2 Oct, while you were away)**: read the report https://claude.ai/artifact/RgXzkpGtFQ1GWicAx3LMSN.
  The APK "Rogue Pirates" is installed on your phone (the playtest build, no benchmark code). Playtest it and the web build;
  check water, fire, smoke, distortion and the UI art (questions 40 and 44); answer questions 39-45 in `DECISIONS-WHILE-AWAY.md`
  (256 MB heap, WebAssembly 2023, bar draw order, Android texture cap, Google Play).
- **Playtest M7 and M8 on the phone build** (committed, awaiting your playtest). Build: `Pirate Game Unity/Builds/WebGL-Mobile`
  (made after M8); serve it to your phone on the Wi-Fi with `python plan/v5/tools/serve-webgl.py "Pirate Game Unity/Builds/WebGL-Mobile/RoguePirates"` and open the address it
  prints. M7: the BOOST and PAUSE buttons, the pause menu, Settings (UI scale, stick on the right, quality), a notch phone.
  M8: SET SAIL (mode select, then Captains), LEDGER (buy a Logbook or item with RUM, then see it offered in a run),
  PROFILE (your collection). On the reference phone, note the frame rate and whether it ever stutters.
- **Playtest M4, M4b and M5** (committed): the nine new weapons with their Empowered evolutions, five new Logbooks and the
  new items (all bought with RUM, so buy them in M8's shop first).
- **Answer questions 1-38** in `DECISIONS-WHILE-AWAY.md` (decisions D1-D36 there say what I chose meanwhile; 33-35 are
  M7's, 36-38 M8's).

- **Read what I did while you were away** (1 Oct): `plan/v5/DECISIONS-WHILE-AWAY.md` lists every decision I took on
  your behalf (D1-D27) and 16 questions for you. New phases M3b (feel fixes), M3c (level-up rhythm), M3d
  (count-based pressure, peaks and valleys, distinct enemies) and M3e (performance, then the enemy cap) come before M4.
- **Playtest M3b** (committed, awaiting your playtest): steer into an island (the ship bounces off and loses 5 hull,
  at most every 2 s), drop powder kegs and wait 45 s (unblown ones sink), and on a phone build use the round ability
  button bottom right.
- **Playtest M3c** (committed, awaiting your playtest): the first level with the first kill, then gaps of 10, 10, 20,
  30, 50 s, then 60 s (about 13 level-ups by 10:00, was ~24); weapon cards worth more levels; evolutions from weapon
  level 5. Does the slower rhythm feel right, and do builds still feel strong?
- **Playtest M3d** (committed, awaiting your playtest): enemy HP no longer grows with time; the crowd follows your
  sketch (an early horde at 0:35, small peaks, a big one at 3:35-3:45, a quieter valley to 6:30, peaks at 7:30 and
  9:35). Is 0:00-3:00 busy enough, is 6:00-8:00 fair, do you feel the 3:45 peak, and can you tell every enemy apart
  (crimson gunner cog vs white cannon boat, orange net cutter, yellow needle sloop)? Overtime after 10:00 is much
  harder (question 10).
- **Playtest M3e** (committed, awaiting your playtest): does anything look different? Pickups no longer cast shadows,
  the heavy models (chests, buoys, the wreck) draw lighter versions at a distance, the camera draws nothing past 250 m.
  Then answer questions 14-16 (enemy cap, health bars on every enemy, pickup sparkles).
- ~~**Playtest M2 and M3 on the demo baseline.**~~ Done 1 Oct: "fine for now, we'll finetune it later". The demo round (30 Sep) changed the voyage and you chose to keep it:
  pickups give half XP, the ship is 1.625x faster (acceleration too), player shooting ranges are lower (Deck Cannons
  22 -> 16 and so on), enemies are smarter (they path around islands, cut you off, surround you and lead their shots),
  the hammerhead and rammer attack sooner, and cannon boats appear from 1:00. You kept half XP and chose a retune around
  it (1 Oct, `plan/v5/balance-recheck/README.md`): enemies grow gentler (HP growth x0.55, damage growth x0.7), the
  evolution card comes from weapon level 6 (was 8), the Lightning Mast is 2% weaker. In the simulator runs last as long
  as in M3 and the boss is beaten as often; levels come about 40% slower. Check in the playtest: does the slower
  levelling feel good, do evolutions still arrive (around 6-7 minutes for a focused build), and is the mid-run still a
  fight with the gentler enemies?
- **Flamethrower VFX:** fixed in M4a (committed, awaiting your playtest): the flame now reaches exactly as far as its damage
  (9.5 m) and is shaped from the first frame. Does it look right (question 26)?
- **M4 weapons:** built on my defaults after your "proceed till M8" (D31, D32); questions 17-26 can still change them.
- **Read the Railway breakdown**: tracking page section "Your answers, explained", or `plan/v5/ANSWERS.md` (what runs
  on Railway, what each part costs, where to save).
- **Get the reference phone** for M1's measurement: a Samsung Galaxy A15 or A16 4G (about $130–150; any Helio G99 phone
  such as a Galaxy A24 works).
- **Say go for M1** (enemy flow) when you are ready.
- **Accounts** (final list and costs in `ANSWERS.md`): Railway by M9; a domain, a Google Cloud project and Resend by
  M10b; Helius by M11; Firebase by M19. Keys go into Railway variables or an uncommitted `.env`, never into chat.

## Still open

- **Later: asset streaming and 256 MB phones** (your answer to questions 39/43, 2 Oct). The web build needs ~330 MB at load;
  older 32-bit phone browsers allow 256 MB. Needs assets loaded as needed (Addressables) and a smaller first download: its own phase.
- **Later: Google Play** (question 45, 2 Oct; playtests only for now): an Android App Bundle (AAB), an upload keystore that you
  create and keep outside the repo, Play App Signing, a target API level and a store listing.
- **After your playtest, answer**: question 40 (water, fire, smoke, distortion and UI art on the phone web build), 42 (health-bar
  draw order), 44 (Android texture cap; UI art at 1024 too?).

- **M2 and M3 balance decisions** (details on each phase's page): which balance targets stand (M2 widened nine after
  the first run); the Turtle personality; evolution at level 8 (focused builds evolve around minute 6) or at 10;
  Captains balanced on run length (all within ±10%) or on offense (Brine ×0.80, Roxie ×1.16 at 10:00; Flint's hull went 90 → 120); the synergy items (Black Powder Keg,
  Powder Fuse, Sea Witch's Eye, Leviathan's Tooth, Stormglass, Golden Figurehead); Epic cards +2 levels; the volley
  pieces (Extra Hands, Broadside Drum: fewer ranks?) and crit (a ×2 crit-damage base?); whether refreshes count toward
  the pity rule.

- **Phone memory (found in M1):** today's WebGL build needs a 344 MB WebAssembly heap and a 160 MB data download at the
  start of a run. A Galaxy A15/A16 (32-bit Chrome) allows about 256 MB, so the game would most likely not load there.
  M7 (mobile) must fix this; the phone checks from M1 moved there because there was no reference phone yet.

- Earlier GDD questions (21 left) are still on the board; the raid entry fee (question 4) feeds M15 and the pass price
  (question 5) feeds M20.

## Done

- **Demo round (30 Sep), merged into V5:** the art team's menu, Captain select, HUD and level-up cards; the VFX editor
  (Tools > Rogue Pirates > VFX Editor); POI and level-up card prefabs; background music; the floating joystick (app and
  mobile browsers); safe-area HUD; mobile render settings (60 fps on a Pixel 9a); app name Rogue Pirates, logo icon,
  package com.x2c.roguepirates; the boss rises out of a whirlpool after the beacon summon; bug fixes (shots through
  islands, pink trails, trail streaks, minimap fog, the orange warning square). Details: `plan/demo/README.md`.

- **V5 questions, round 3** (30 Sep): netcode (Unity Netcode for GameObjects), treasury protection, reference phone,
  forfeits and checked RUM decided. Every V5 question is answered.
- **V5 questions, round 2** (30 Sep): 16 decided, the Hunt netcode and the reference phone researched, player
  personalities defined (`PLAYER_PERSONALITIES.md`).
- **V5 questions, round 1** (30 Sep): 19 decided, the rest explained.
- **V5 M0 setup** (29 Sep): branch `v5-live-game`, the independent reviewer agent, the plan and research, a green
  baseline (EditMode 307/307, PlayMode 49/49), plan reviews.
