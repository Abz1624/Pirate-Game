# V5 M3 — Build balance: progress

Started 30 Sep 2026 at `4112e7af` (V5 M2), built unattended at the owner's request ("as far as I can", commit and push
each phase that passes its tests and review; playtests when the owner is back).

| Step | Content | Status |
|---|---|---|
| G3.1 | Evolutions reachable (Q18b): the evolution card from weapon level 8 with its item, and the evolved weapon keeps levelling to 12; Rare and Epic cards grant 2 levels, Legendary 3 (capped at the max; Epic +2 is an assumption: Q18b names Rare and Legendary only); a pity rule (after 6 card deals without a Rare-or-better card, one weapon upgrade in the next deal is at least Rare; a deal is every set of cards shown, refreshes included) | Done in the game's code, each a `ProgressionConfig` switch whose V3 value (off) is in the v3-parity preset: `evolutionUnlockLevel`, `levelsPerRarity`, `rarePityLevelUps`/`pityMinRarity`. `OfferGenerator` deals them; `ProgressionLoadout.ApplyUpgradePackage` applies the levels |
| I3.1 | Weapon model: power per level on one target and on a crowd | Done: `DevTools/Balance/WeaponModel.cs`. The upgrade path is the game's own (`RollRarity`, a new thin `OfferGenerator.WeaponUpgradeCard`, `ProgressionLoadout`), 300 seeded paths per weapon, medians; crowd power = × the behaviour's coverage × size upgrades (× duration for streams, trails, auras and mines: a stated model assumption) |
| G3.2 | Weapon parity: each weapon within ±20% of its role (single target, crowd, zone) at levels 1, 4 and 12 (and 8, where evolution unlocks, added after the review) | Done: all ten within ±20% at 1, 4, 8 and 12 (worst ×1.16), by `WeaponModel.TuneParity` in two passes (base damage for the level, the upgrade increments for the growth; each pass keeps growth changes within ×0.5–×2) — `evidence/weapon-parity.md`. The target is relative (each role's geometric mean); evolved weapons are outside parity (their effects are not in the numbers) |
| G3.3 | Logbooks and items in rarity bands (Common +5–8% build power, Uncommon +7–11%, Rare +10–15%, Epic +15–25%); Luck and rarity weights reviewed | Plain pieces in band (Gunnery Manual, Carpenter's Notes, Dragon's Breath Nozzle retuned); five flagged (below); synergy items not banded (below): `DevTools/Balance/BuildModel.cs`, `evidence/build-pieces.md`. Luck and rarity weights reviewed, unchanged: rarity weights 60/26/10/3.2/0.8 now also set how many levels a card grants; luck sources are small (+0.03–0.08 per item), so luck barely moves rarity (+0.08 luck ≈ +4% on Rare weights) |
| G3.4 | Captains within ±10% of each other | Done on survival: Flint's hull 90 → 120; every Captain's median run length within ±10% of the Captains' median (200 simulated runs each, the baseline personality). The plan says "power": measured as survival because the Captains trade offense for hull. In the tested sample (200 runs each, `evidence/captains.md`) run length is ×0.92–×1.07 of the median, while offense at 10:00 is ×0.80 (Brine) to ×1.16 (Roxie): offense alone is not within ±10% — owner decision |
| I3.2 | Item flow: 4–6 items by 10:00; evolution items findable | Done: chest prices 20, then 45 × 1.35ⁿ (were 25 × 1.22ⁿ): 5 items by 10:00 for the baseline (the Glass Cannon and Treasure Hound, who buy every chest, get 7). New in the game's chest roll (`ChestEconomy`, a `StageContentConfig` switch, off in the preset, no extra Loot draws when off): a 40% chance that a chest gives the missing evolution item of a carried, unevolved weapon at the evolution card's level (8) or higher |
| T3.x | Tests | See Results |

## What changed and why

**Evolutions were almost unreachable.** In M2's model only ~15% of focused runs evolved: an evolution needed a weapon
at level 12 and its specific item from random chests. M2's simulator also let evolution-minded players pick their
item from a chest half the time, which the game does not allow (a chest rolls one random item); that shortcut is gone
and the game itself now weights the chest toward a ready weapon's missing item.

| Rule set | Build Planner evolves / first at | Sea Legend | Steady Captain |
|---|---|---|---|
| M2 (level 12, simulator shortcut) | 14% / 7.9 min | 16% / 8.4 | 0% |
| Level 8 + multi-level cards, no item help (shortcut removed) | 21% / 7.3 | 27% / 6.9 | 21% / 8.6 |
| **Adopted:** + 40% item chance from level 8 | ~61% / ~5.9 | ~71% / ~5.8 | ~42% / ~7.2 |
| Alternative: unlock at level 10, 50% item chance from 10 | 53% / 6.7 | 55% / 6.1 | 19% / 8.3 |

The plan hoped a focused build would evolve around minute 7–9. With the owner's confirmed rules (level 8; Rare +2,
Legendary +3), a focused weapon reaches level 8 by about 5:00, so focused builds evolve earlier (~6 min). The owner
decides whether that is good (the power fantasy arrives sooner) or whether to unlock at 10 (numbers above).

**Weapons.** Growth shape, not base power, set weapons apart: the Flamethrower started strongest and ended weakest
(×1.40 → ×0.47 of its role), the Powder Kegs lagged everywhere (×0.38 at 12), the Lightning Mast ran away late (×2.25).
Net changes after both passes:

| Weapon | Base damage | Upgrade growth |
|---|---|---|
| Broadside Battery | ×1.39 | ×0.54 |
| Deck Cannons | ×0.99 | ×1.57 |
| Flamethrower | ×0.81 | ×1.40 |
| Flying Cutlasses | ×0.94 | ×0.50 |
| Harpoon Gun | ×0.87 | ×1.24 |
| Lightning Mast | ×0.78 | ×0.64 |
| Long Nine | ×0.89 | ×1.52 |
| Mortar | ×0.66 | ×1.66 |
| Powder Kegs | ×1.30 | ×2.06 |
| Swivel Guns | ×0.91 | ×1.79 |

**Logbooks and items.** Changed: Gunnery Manual +8% → +6.5% damage per rank; Carpenter's Notes +10 → +6.5 hull per
rank; Dragon's Breath Nozzle size ×1.08 → ×1.32 (Epic). Items whose worth depends on the weapons carried or on hoarding
gold (Black Powder Keg, Powder Fuse, Sea Witch's Eye, Leviathan's Tooth, Stormglass, Golden Figurehead) are **not
banded and not changed**: a first pass had tuned five of them on a generic build (explosions ~25% of damage, and so
on), which the review showed misprices them — near-useless without their weapon, far above band with it, and the Powder
Fuse's charge is spent by one explosion. They are listed for the owner. Duplicate item stacks add up uncapped (as V4),
so two Dragon's Breath Nozzles give ×1.74 size: worth a look in the playtest.

**Flagged for the owner, not changed:**
- **Volley pieces** (Extra Hands, +1 projectile per rank, ten ranks; Broadside Drum, an Epic item): +50% build power
  each, far above any band, because a volley step cannot be fractional. Options: fewer ranks (Vampire Survivors'
  Duplicator has 2), or keep them as the rare big pick.
- **Crit** (Lookout's Glass, Serrated Scabbard, Ghost Lantern): +1–2%, because crit damage is only ×1.5 everywhere.
  Options: base crit damage ×2 (a global change), or crit pieces that also raise crit damage.
- **Utility pieces** (XP, gold, pickup radius, move speed, luck, projectile speed; Lucky Doubloon, Cursed Compass,
  Copper Conductor) are not scored against the bands; Lucky Doubloon (+0.03 luck only) is likely too weak to be worth
  a pick.

## Results (after the review fixes)

| Check | Result | Evidence |
|---|---|---|
| Weapon parity (300 paths each) | 10 of 10 within ±20% of their role at levels 1, 4, 8 and 12 (worst ×1.16) | `evidence/weapon-parity.md` |
| Logbook and item bands | every plain piece in band except the five flagged (volley, crit); six synergy items shown, not banded; utility pieces named | `evidence/build-pieces.md` |
| Captains within ±10% (the test: 200 simulated runs per Captain, the baseline personality) | all six within ±10% of the Captains' median run length (×0.92–×1.07); offense at 10:00 ×0.80–×1.16 | `evidence/captains.md`; `V5BuildM3Tests.Captains_…` in `evidence/editmode.json` |
| Simulator report (10 personalities × 200 runs, weapon pairs) | **74 of 76 M2 targets met** (was 72): the Glass Cannon lasts 13.8 min (band 8–13) and the Sea Legend ends at 16.5 (17+). Baseline: 5 items by 10:00, evolves in ~44% of runs (first ~7.4 min); Build Planner ~62% (~5.8), Sea Legend ~71% (~5.5). The report's per-Captain table uses ~33 runs each and is indicative only (Roxie 10.5, Brine 13.9 there) | `model-report.md` |
| EditMode, all assemblies | **367 / 367** passed (106.5 s), incl. 15 `V5BuildM3Tests` and the M2 calibration (which now restores the pre-V5 values of both retunes) | `evidence/editmode.json` |
| PlayMode, all assemblies | **51 / 51** passed (172.2 s), incl. the evolution chain in the real run | `evidence/playmode.json` |
| Playable check: each of the six Captains in a real Play session (restarted as each, like the console's "restart as"; invulnerable; XP pulled; active used; clock to 1, 3, 5 min) | all six ran to 5:00 with their actives firing; **0 game errors**; the saved Captain choice restored; the check refuses to start on unsaved scene edits and reloads only a scene it dirtied | `evidence/playable/` |

Older tests changed: V4 tests that pinned pre-M3 values as V3's (the chest price ladder, the R-40 item effect values,
M2's balance version) now read the v3-parity preset or the configured values.

## Independent review

The reviewer (`v5-reviewer`, read-only) found **no blockers, 3 majors and 10 minors**.

| # | Severity | Finding | What was done |
|---|---|---|---|
| 1 | Major | Captains were balanced on run length, not "power"; offense spreads −27%…+12%; run counts overstated | Recorded as a metric decision for the owner (G3.4 row); 200 runs per Captain in the test and the report text corrected. Flint's hull re-measured after the other fixes: 90 → 120 |
| 2 | Major | Synergy item retunes rested on a generic build; Powder Fuse modelled wrongly | Those five items reverted to their values before M3; synergy items marked "not banded"; the fuse's assumption corrected; listed for the owner |
| 3 | Major | New rules not tested through the game's code path | Tests: a Rare card taken through `ProgressionModule.Select` grants 2 levels; an evolved weapon levels on to 12 through cards; a refresh counts as a deal; a PlayMode test in the real run: level 8 → the stage's chest gives the evolution item (the build's items wired) → the progression deals the evolution card → taken → levels on to 12 |
| 4 | Minor | Pity counts deals, not level-ups | Documented on the config field and tested (a refresh is a deal) |
| 5 | Minor | Pity raised every weapon card in a deal | One card per deal; tested |
| 6 | Minor | Two settings for the evolution level could disagree | The chest follows the card's level (`evolutionItemMinWeaponLevel` 0); tested |
| 7 | Minor | Preset test partial; no zero-draw check | Every weapon's increments and caps, both Logbooks checked; a draw-counting test (off = no extra Loot draws) |
| 8 | Minor | The evolved-levelling test would pass without M3 | Replaced (see #3) |
| 9 | Minor | Parity skipped level 8 | Level 8 added (a second tuning pass; Lightning Mast was ×1.26 there) |
| 10 | Minor | "Items by 10:00" claim | Stated for the baseline; other personalities named |
| 11 | Minor | Duplicate stacks uncapped | Noted for the playtest |
| 12 | Minor | `V5_PLAN.md` and `TODO-FOR-YOU.md` not updated; luck review unrecorded | Updated; the luck review is in the G3.3 row |
| 13 | Minor | A Captain id in an Editor generator | Kept (Editor tooling, commented) |

A second pass by the reviewer on the fixes: 12 of 13 resolved, #1 partly (the decision is the owner's). It found 1
new major and 3 minors, all fixed: the Captain decision quoted stale offense numbers (now the tested 200-run table,
`evidence/captains.md`); `playable-check-m2.ps1`'s scene reload ran before leaving Play mode (moved after, as in M3's;
the M2 script's scene guards were added in this phase); a pity-raised card replaced by the new-weapon guarantee delays
the pity to the next deal (documented on the field); the public `Stage` accessor on `V4Game` exists for tests (kept,
read-only).

Owner questions from the review: the evolution timing (level 8 as confirmed, or 10); Captains balanced on survival or
on offense; the synergy items and the Golden Figurehead; Epic +2 levels; the volley and crit pieces; whether refreshes
should count toward the pity rule.
