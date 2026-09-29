# Survivor-like enemy flow and balance — research for V5 M1/M2

Collected 29 Sep 2026 by a research agent (web). Each fact carries the page it came from; `[snippet]` marks facts seen
only in a search summary (unverified). The Vampire Survivors facts come from https://vampire.survivors.wiki (the
fandom wikis refused the connection).

## Vampire Survivors (VS) — enemy flow

- One wave per minute. Each wave has an enemy list, a **minimum** alive count and a **spawn interval**. On each tick: if
  fewer than the minimum are alive, spawn up to the minimum; otherwise spawn one of each enemy type in the wave.
  https://vampire.survivors.wiki/w/Enemies
- Periodic spawning stops at 300 alive (bosses and events still spawn). Enemies spawn just off-screen and despawn when
  far away; bosses are teleported back instead. (same page)
- Curse raises HP, speed and quantity; interval = interval ÷ totalCurse. https://vampire.survivors.wiki/w/Curse

Mad Forest, minutes 0–10 (https://vampire.survivors.wiki/w/Mad_Forest):

| Min | Enemies (HP) | Minimum alive | Interval | Boss | Event |
|---|---|---|---|---|---|
| 0 | Red-eyed bat (5) | 15 | 1.0 s | – | – |
| 1 | Zombie (10), little bat (1) | 30 | 1.0 s | Glowing Bat | 1st chest (can evolve) |
| 2 | Three bat types | 50 | 0.5 s | – | Bat swarm 100% ×3 |
| 3 | Skeleton (15), Glowing Bat | 40 | 0.25 s | Glowing Bat | Bat swarm 10% ×2 |
| 4 | Skeleton, Ghost (10, fast) | 30 | 1.0 s | – | Bat swarm 10% ×2 |
| 5 | Green Mudman (150), Mantichana | **10** | 1.0 s | **Mantichana** | Flower Wall |
| 6 | Zombie, Green Mudman | 20 | 0.5 s | – | Bat swarm 10% ×2 |
| 7 | Bats, Gray Mudman (70) | 80 | 0.5 s | Glowing Bat | Bat swarm 80% ×6 |
| 8 | Zombie | **100** | 1.5 s | Giant Bat | Bat swarm 80% ×3 |
| 9 | Giant Bat, Zombie | 30 | 0.5 s | Silver Bat | Bat swarm 70% ×3 |
| 10 | Gray and Green Mudman | **10** | 0.5 s | **Giant Mantichana** | Flower Wall |

- Later minimums reach 150–300 at 0.1 s. At 30:00 the screen clears and the Reaper (unkillable) ends the run.
- **The minimum is jagged, not a ramp**: it swings 10–100 in the first 10 minutes and drops to 10 on the big-boss minutes.
- Fodder HP rises about 30× in 10 minutes (bat 1–5 → mudman 150), roughly doubling every 2 minutes.
- Boss HP = base × player level at spawn (Mantichana 150 × level). **Not adopted**: the GDD's "never" list forbids
  difficulty that inflates boss HP based on player strength.

## VS — XP, weapons, passives

- Level 2 costs 5 XP; each later level +10 up to 20, +13 to 40, +16 after; levels 20 and 40 cost +600/+2400 XP with +100%
  Growth until the next level. Total to level L (L ≤ 20) ≈ 5·(L−1)². https://vampire.survivors.wiki/w/Level_up
- 3 choices (4 with luck). About 78 upgrades in 30 minutes: one every ~23 s.
  https://jboger.substack.com/p/the-secret-sauce-of-vampire-survivors
- Weapons max at level 8 (Whip: 10 → 40 damage, 1 → 2 projectiles; ~8× per cycle from levels alone).
  https://vampire.survivors.wiki/w/Whip
- Evolution = max weapon + paired passive, from a boss chest after 10:00. https://vampire.survivors.wiki/w/Evolution
- Chests: 1 item 50%, 3 items 10%, 5 items 3% (luck-scaled); the first six chests are scripted 1-1-3-1-5.
  https://vampire.survivors.wiki/w/Treasure_Chest
- 17 passives at +10% per level, max level 5 (Spinach +10% Might, Empty Tome −8% cooldown, Duplicator +1 amount max 2).
  https://vampire.survivors.wiki/w/Passive_items
- Meta upgrades total about 2.5× damage and 2× HP.

## Survivor.io

- Early chapters ~15 minutes: horde alerts at 2, 4, 8 minutes; elites around 3, 10.5, 11:30, 12; bosses at 5, 10, 15;
  elites and bosses drop chests. https://simplegameguide.com/how-to-beat-chapter-1-survivor-io/
- Skills have 5 levels; level 6 is the evolution, needing the paired support skill and a boss chest.
  https://onechilledgamer.com/survivor-io-skill-guide/
- Support skills linear per level (Energy Cube −8% cooldown, Fitness Guide +20% HP, Sports Shoes +10% speed).
- Meta: 6 equipment slots, merge duplicates; energy 5 per run; patrol idle income; daily challenges.
  https://www.pocketgamer.com/survivor-io/guide/

## Principles

- Tension and release: comfortable domination followed by bigger groups; near misses pull the retry.
  https://theconversation.com/vampire-survivors-how-developers-used-gambling-psychology-to-create-a-bafta-winning-game-203613
- A steady trickle of small power gains beats step-like difficulty; the fantasy is overwhelming power.
- Time-to-kill pecking order: fodder ~1 s, tanky 2×, elites 4–8 s, bosses 45–90 s with phases past 2 minutes.
  https://www.tumblr.com/askagamedev/184716817936/enemy-design-tips-time-to-kill
- Kill-driven replacement (Megabonk, hard cap 550). https://www.megabonkinfo.org/player-stats/difficulty

## Other games

- Brotato: 20 waves of 20–90 s; enemy HP linear per wave. https://brotato.wiki.spellsandguns.com/Waves
- 20 Minutes Till Dawn: alive cap 20 → 600 over 20 min; basic HP ~20×; boss barrier traps the player for 1 minute.
  https://20minutestilldawn.wiki.gg/wiki/Forest
- Megabonk (closest structure): 10-minute stage, boss at a fixed map spot the player chooses when to fight, "Final Swarm"
  ghosts at 10:00 with new tiers at +6 and +12 minutes.
  https://www.megabonk.ninja/guides/dexafire/surviving-the-final-swarm-boss-late-game-strategy-guide

## Rules proposed for Rogue Pirates (input to M1/M2; each becomes a config value and a model target)

1. Minimum-alive floor per minute with a **jagged** curve (about 12/25/40/35/30/55/70/90/60/100 before scaling to our
   cap and mobile budget), topped up on every interval tick.
2. While the Beacon boss is active, the floor drops to ~30% (floor ~10); 20–30 s calm after the kill, then the minute's peak.
3. Fodder HP roughly ×2 every 2 minutes; count grows separately, so the screen fills instead of enemies getting spongy.
4. Hard alive cap (performance-bound on mobile WebGL; measured in M1) and an XP-pickup merge above a pickup cap.
5. Time-to-kill targets for an on-track build: fodder ≤ 1 s (1–2 hits), elites 4–8 s, Barnacle King 45–90 s.
6. XP: about one upgrade every 6–10 s in minute 1, one per ~15 s on average to 10:00; 2 weapons maxed plus one evolution
   by about 10:00 for a focused build.
7. Horde alerts every 2–3 minutes away from the boss window (fast packs from one side, ring or line); elites at ~3, 6, 9.
8. Overtime in tiers (a new Drowned tier every 2–3 minutes) and a run-ending unkillable pursuer after a fixed time,
   which also caps overtime gold farming.
9. Passive (Logbook) steps near +10% per rank with 5 ranks; weapons 8 levels.
