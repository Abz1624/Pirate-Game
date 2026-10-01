# V5 M5: new Logbooks, items and powerups (phase plan and results)

Plan: `V5_PLAN.md` "#### M5". Start commit: `3e633720` (M4b). Built after the owner's "proceed till M8" (1 Oct); decisions
D33 in `plan/v5/DECISIONS-WHILE-AWAY.md`.

## Logbooks (design 5.2), bought with Rum

| Logbook (Rum) | Per rank | Max |
|---|---|---|
| Ironclad (180) | +2 armor | 10 ranks |
| Fortune (200) | +5 Luck (better chest rarity) | 10 |
| Tidecaller (220) | +5% duration | 10 |
| Admiral (240) | +3% damage to elites and bosses | 10 |
| Plunderer (240) | +3% boss gold ("Boss Loot", the unused Doubloon Gain stat, read by the kill payout) | 20 (+60%) |

Chaos is deferred (D33): its card must sample and show a random stat before you take it (and keep it on reconnect), which
needs a level-up card change. New `UnlockConfig.logbookUnlocks`; the offers skip a priced Logbook until it is owned in the
save (`OfferGenerator.LogbookUnlocked`, `ContentUnlocks.IsContentOwned`); M8's shop sells them.

## Items (design 7.1-7.5)

Free starter pool (Common): Spare Planks (+7 hull), Powder Monkey (+6.5% attack speed), Shark Tooth Necklace (+12% damage
to enemies above 90% hull, a new effect), Manta Sail (+3% speed), Reef Carapace (+4 armor), Crow's Nest (+10% pickup radius).

Bought with Rum (`UnlockConfig.itemUnlocks`; the chest skips them until owned): Skeleton Key (140: 6% a chest is free per
copy, at most 35%; a free chest does not raise the next price), Smuggler's Ledger (180: chests 20% dearer, Uncommon and
better 10% likelier per copy, at most 50%), Black Spot (190: elite kills +10% gold and XP per copy, at most 50%), Dead
Man's Compass (180: +5% XP, 10% more enemies), Cursed Coin (180: +10% gold, -10% max hull), Barnacle Crown (240: +2% damage
a second while 4+ enemies are within 5 m, up to +20%), Jolly Roger (240: +1% damage per 10 kills up to +20%, a hit takes
half), Leviathan Heart (300: a 45-damage blast in 5 m on the densest crowd every 12 s after a 0.6 s warning), Captain's
Last Rum (300: once a run a fatal hit leaves you at 25% hull, 2 s safe, +30% damage for 8 s).

Deferred (D33): Hammerhead Helm, Kraken Ink Sac, Storm in a Bottle, Devil's Match, Queen Anne's Fuse. Each needs proc
chains with the design's guardrails (7.6: at most 64 secondary events per source, three generations) or ram contact damage,
which V4 does not have yet.

Prices are on the same scale as the weapons (D31: today's ladder, not the design's 160-650).

## Powerups

Battle Signal, Tailwind, Salvage Chime and Full Sail (their effects existed since M9) join V3's four: Repair Supplies 25,
Battle Signal 25, Tailwind 13, Powder Rush 10, Magnetic Tide 8, Dead Calm 7, Salvage Chime 7, Full Sail 5. The parity
preset keeps V3's four at 25 each.

## Code

New `ItemEffectKind`s 13-20 in `ItemModule` (streak, surrounded stacks, pulse event, cheat death, healthy-target, free
chest, chest price and rarity, elite rewards); `DamageReceiver.PreventDeath` (the Last Rum); `IWeaponModifiers.
HealthyTargetDamageMultiplier` and `IEnemyQuery.HpFraction`; `IWeaponManager.ItemPulse`; `ChestEconomy` hooks;
`KillFanout.EliteRewardMultiplier`, `BossGoldMultiplier`; the build model scores the new effects; the simulator's regular
player owns only free and up-to-100-Rum content. Placeholder icons: Lucky Doubloon's for the 15 items; Logbooks have no
icons (the catalogue needs a list: art guide).

Tests: `V5ContentM5Tests` (content priced and reachable, no em dashes; Jolly Roger, Barnacle Crown, Leviathan Heart, the
Last Rum through the real hull, chest and reward multipliers); guards updated (counts, the powerup pool and its parity).
The content tool: `tools/V5Content5.cs.txt`.

## After the balance check and the review (D34)

The simulator showed that adding the six free Commons, or the four designed powerups, to every pool pushes the weakest and
strongest player profiles past their bands (both sat at the edges). So: the Commons are bought (120 Rum) like the rest; the
four designed powerups stay out of the floor pool until a rebalance with the owner (questions 28-29). Fortune gives +0.03
Luck a rank (the level-up scale); Ironclad, Reef Carapace (armor is never applied, R-49) and Cursed Coin (gold gain is
ignored in stage runs) are out of the pools (questions 30-31); Admiral is elites only (question 32).

Review (1 Oct, 3 blockers, 5 majors, 9 minors):

| # | Finding | Done |
|---|---|---|
| 1 | Fortune on the wrong Luck scale | +0.03 a rank; question 27 |
| 2 | Armor does nothing | Ironclad and Reef Carapace out of the pools; question 30 |
| 3 | Cursed Coin's gold bonus is ignored in runs | Out of the pool; question 31 |
| 4 | Shark Tooth skipped projectile hits | `PlayerProjectiles.HitAdjust` applies it |
| 5 | Admiral misses bosses | Elites only, recorded; question 32 |
| 6 | Black Spot's XP rounded away; it multiplied the coins' capped gold | Fractions carried; the coins' gold added after |
| 7 | Tests stop at the item module | Recorded: the chest, kill-payout and card paths are checked by the M8 shop work and the playtest |
| 8 | Evidence missing | Full suites run once on the final data (below) |
| 9 | The chest pool rebuilt on every read | Built once per chest |
| 10 | Skeleton Key needed the full price in hand; no feedback | The free roll comes first; a "Free chest" toast |
| 11 | The Last Rum's safety let island knocks through | Knocks skipped during it |
| 12 | Two items with one effect kind do not stack | Recorded (first held applies) |
| 13 | The simulator's Logbook list is fixed at construction | Recorded |
| 14 | The results screen may show `leviathan_heart` raw | Recorded for the playtest |
| 15 | Powerup mix differed from the plan | D34 |
| 16 | Dead helper; the new-Logbook check ignored ownership | Removed; filtered |
| 17 | An em dash in Cursed Compass's description | Replaced |

M5 results: EditMode 448/448; PlayMode 59/60 in the full run, the one failure `V4HudPlayModeTests.Presenters_ReplaceTheV3Views_AndShowTheRun` (the Beacon quest already found 3 s into a run whose seed put the Beacon beside the start) passing 8/8 on its rerun (`t-hudplay.json`): a seed-dependent test, recorded. The Module Lab grant check was not built: the item effects are covered by `V5ContentM5Tests` through the real item module and hull; the in-run grant is for the playtest (bought content needs the M8 shop or the unlock-all switch).
