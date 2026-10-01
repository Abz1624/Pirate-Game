# Your TODOs — V5 (updated 1 Oct)

## Next

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
- **M4 weapons:** I built only the mechanics (M4a: slow, pierce, knockback, several orbit weapons, a pulsing aura); the
  four weapons and eight evolutions wait for your answers to questions 17-26 in `DECISIONS-WHILE-AWAY.md` (prices, the
  Ramming Prow's boost button, milestones, the evolution level, offer weights, minibosses).
- **Read the Railway breakdown**: tracking page section "Your answers, explained", or `plan/v5/ANSWERS.md` (what runs
  on Railway, what each part costs, where to save).
- **Get the reference phone** for M1's measurement: a Samsung Galaxy A15 or A16 4G (about $130–150; any Helio G99 phone
  such as a Galaxy A24 works).
- **Say go for M1** (enemy flow) when you are ready.
- **Accounts** (final list and costs in `ANSWERS.md`): Railway by M9; a domain, a Google Cloud project and Resend by
  M10b; Helius by M11; Firebase by M19. Keys go into Railway variables or an uncommitted `.env`, never into chat.

## Still open

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
