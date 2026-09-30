# Your TODOs — V5 (updated 30 Sep)

## Next

- **Playtest M2 and M3 on the demo baseline.** The demo round (30 Sep) changed the voyage and you chose to keep it:
  pickups give half XP, the ship is 1.625x faster (acceleration too), player shooting ranges are lower (Deck Cannons
  22 -> 16 and so on), enemies are smarter (they path around islands, cut you off, surround you and lead their shots),
  the hammerhead and rammer attack sooner, and cannon boats appear from 1:00. Before M4 I re-run the M2/M3 balance model
  and simulator on this baseline and report what moved; nothing is retuned without your OK.
- **Flamethrower VFX:** it is offered again, but its effect is still broken; I fix it in M4 (weapon VFX work).
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
