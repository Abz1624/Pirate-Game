# Decisions made while you were away (1 Oct 2026)

You asked me (before a 7-8 hour AFK) to keep working, take decisions on your behalf, log them here, and ask for next
steps when you are back. Newest decisions at the bottom of each section. Every one can be reverted: each phase is its
own commit on `v5-live-game`.

## Your inputs before leaving

| Item | Your words (short) | Your answer to my questions |
|---|---|---|
| a | Too many level-ups break the flow | Level-up gaps grow like a Fibonacci series (your sketch: 1 1 2 3 5) |
| b | Powder keg sinks and disappears after 45 s | - |
| c | Captain ability button like the joystick pad, right side | Touch devices only |
| d | Pooling and garbage for everything spawned | - |
| e | Boring to 3-4 min, too hard after 6; peak-valley pattern growing upwards (sketch) | ~60 s cycle like Survivor.io, plus the sketch |
| f, i | Batching (static and dynamic) | - |
| g | Island hit: bounce back a bit, take damage | - |
| h | Difficulty from enemy count, not HP or damage | Both: stronger, visually distinct new types later, and a higher enemy cap |
| j | Occlusion culling | - |
| k | Same-looking enemy never changes behaviour over time | - |

## Decisions

| # | Decision | Why | Alternatives I did not take |
|---|---|---|---|
| D1 | Your sketch read as: steady upward trend, small close peaks early (~1 and ~2 min), a bigger peak at ~3-4 min, a long gentle valley, then two big peaks building to the climax at 10:00; ~60 s minor waves ride on top | The sketch and your "~60 s like Survivor.io" answer together | A strict identical 60 s cycle all run long |
| D2 | Four new phases before M4 (split after the plan review): **M3b** feel fixes (b, c, g), **M3c** level-up rhythm (a), **M3d** pressure from count, peak-valley flow, distinct enemies (e, h, k), **M3e** performance and the enemy cap (d, f, i, j, cap); M4 (new weapons) after them | Pacing and difficulty change the balance every later content phase is tuned against, so they go first; the feel fixes are small and independent; performance follows once the enemy counts (cap) are known | Folding the notes into M4 or M7 |
| D3 | Island hit: faster than 2 m/s into the shore → a push back off the shore of 60% of the impact speed plus 3 m/s that fades over 0.5 s (outside the steering limits, so it works even when the ship faces the shore), at most every 0.75 s; 5 hull lost at most every 2 s, through the build's incoming-damage multiplier (Brace!, Reinforced Keel); values in `RunConfig`, off under the v3-parity preset | Enough to clear the shore and turn without reversing; 5 hull (about 4-5% of a Captain's hull) stings without deciding a run; the cooldown stops a stuck ship from losing hull every frame | Percentage damage; damage scaled by speed |
| D4 | Kegs: sink over 1.5 s (going down and tilting), gone 45 s after being dropped; sinking deals no damage | Your 45 s; a short visible sink reads better than a pop | A fade-out; a small blast when they expire |
| D5 | Ability button: round, bottom right, 200 px, drawn with the touch stick's own base and knob art, a radial cooldown shade and a glow when ready; touch devices only; it goes through the same Captain step as the keys | Your answer (touch only, like the joystick pad); reusing the stick art keeps the look without new art | A separate new icon (no art for it yet) |
| D6 | Level-up gaps: the first level comes with the first kill (~12 s; XP cannot come sooner), then 10, 10, 20, 30, 50 s (your "1 1 2 3 5" × 10 s), then 60 s each: 4 level-ups in the first minute (as before), then far fewer: about 13 by 10:00 instead of ~24. A per-level XP table fitted in the simulator (M3c) | Your "1 1 2 3 5". A first try in 5 s units gave minute 1 more level-ups than before (5 instead of 4) and let one early pickup chain several cards; 10 s units fix both. A pure Fibonacci series would leave 3-minute gaps by level 9 and stall builds | 5 s units; pure Fibonacci; stronger cards with the old pace |
| D7 | Enemy cap: stays 80 through M3d (floors are shares of the cap); raised in M3e only if the frame time measured after the performance work allows, and only up to what a phone near the reference Galaxy A15 could hold | You chose a higher cap; the earlier decision Q21/Q31 sized one cap for the reference phone; doing performance first keeps the retune from being redone | Raising it now in the Editor only |
| D8 | Overtime (after 10:00) keeps its endless HP ramp; the voyage itself (0:00-10:00) has no HP or damage growth | Q22 (endless overtime) needs something to end runs; your note h is about the voyage | Count-only overtime (runs could last forever at the cap) |
| D9 | The "stronger, visually distinct types later" come from today's roster (manta ray, gunner cog, boarding barge, kraken spawn, shield frigate, powder barge), each with its own look; new enemy art goes on the M6 art guide | No new enemy art exists yet | New enemy types now (needs art) |
| D10 | The ability button sits on the canvas with the screen's safe-area insets (not inside the SaltWind HUD, which can be switched off) | Review finding: it was invisible when that HUD was off | - |
| D11 | Builds keep their power with fewer picks: weapon cards grant 2/2/3/3/4 levels by rarity (was 1/1/2/2/3) and the evolution card comes from weapon level 5 (was 6) | With ~13 level-ups by 10:00 the old cards left builds far weaker and evolutions rare (the Steady Captain evolving in ~3%); now focused builds evolve in 65% of runs (by minute 5.6) and the Steady Captain in 53% | Stronger upgrade lines (weaker effect); rarer cards; accepting later evolutions |
| D12 | Roxie's hull 88 → 104 | Fewer levels shrank her per-level damage bonus; her runs fell to 0.88 of the median (the Captains are held within ±10%); at 104 she is at 0.94 | Changing her passive |
| D13 | An interim flat enemy scale in M3c: enemy HP and damage × 0.85 (`ExperimentConfig`, not tied to time); M3d replaces it with fixed per-type stats and count-based pressure | Card strength alone could not hold the balance with ~13 level-ups (the median run fell to ~7.5 min; the Thumb Sailor sank at 3.6 min); with × 0.85 every personality's run length and boss wins are back in band except two small known misses (below); a commit must leave the game balanced | Committing M3c unbalanced until M3d; merging M3c into M3d |
| D14 | Known small misses left to M3d (bounded in the tests): the Glass Cannon runs 13.0 min (band 8-13, as since M3); the Build Planner beats the boss in 79% (band 80-100%); the crowd a little above and kills a little below the per-minute bands from minute 5; the Reef Crab fight at 2:30 can run past 3:00 and push the elite hammerhead beat | M3d reshapes the crowd and sets the minibosses' fixed HP; tuning the old flow now would be thrown away | Tuning them in M3c |

## Questions for you (when you are back)

1. Level-up gaps (D6): the first level with the first kill, then 10, 10, 20, 30, 50 s ("1 1 2 3 5" × 10 s), then 60 s each: is that what you meant? And "(log) e" on the sketch: I read it as the difficulty trend (logarithmic early, steeper late); did you mean the XP growth?
2. Enemy cap (D7): raise it only after measuring (my choice), or straight away?
3. Overtime (D8): keep the endless HP ramp after 10:00?
4. Fewer level-ups make evolutions later: I kept "a focused build evolves by minute 8" by making weapon cards grant more levels (D11). That changes your confirmed Q18b ("Rare +2, Legendary +3"): keep the new card levels, or go back and accept later evolutions?
5. New enemy types with new art (D9): which phase, and who makes the models?
6. The first minute keeps 4 level-ups (the cut starts from minute 2): is that the reading you want, or should the first minute be quieter too?
7. Past level 14 (about 10:00) each level costs a little more (the table's average step, about 12 XP a level): levelling slows gently in overtime. Fine?
8. M3c holds the balance with an interim flat enemy scale (HP and damage × 0.85, D13) until M3d replaces it with fixed per-type stats: fine for now?
