# Rogue Pirates — Captains and Mastery

13 September 2026 • concrete prototype proposals from the owner-requested Megabonk polish pass

## 1. Captain identity

Select one unlocked Captain before a run. Captain changes are free between runs. Selection freezes in the RunSnapshot and appears in score/run summaries without creating Captain-specific leaderboard brackets. Captains command the existing ship; no on-foot character controller is added.

Every Captain starts at player Level 0. Level-based passives reset with the run; no permanent Captain ranks, duplicate merging, stat purchases or trading are introduced. Ship skins remain cosmetic and work with every Captain sharing the rig/slot compatibility. A named NPC such as Mags can appear in narrative without being an additional playable character.

Starting Weapon is fixed by Captain, consumes one normal Weapon slot, and starts at run rank 1. Later Weapons and Item drops use normal random pools with no Captain weighting. First conquest always uses Deck Cannon, overriding signature Weapon if necessary. Other Captain unlocks require the signature Weapon already licensed, so selection cannot bypass its purchase/unlock gate. Two starter Captains both use the default Cannon.

## 2. Six original profiles

All numbers are proposed. Base Hull/speed modifiers apply before run additions; other bonuses occupy the stated normal buckets. `L` means current run player level. Global caps remain in force.

### Captain Rook — generalist starter

Default unlocked; Deck Cannon. No starting-stat penalties. **Steady Aim:** gain +0.5 percentage points of additive damage per level, maximum +25%. This is a simple low-friction baseline for learning and testing. Portrait: weathered coat, overconfident grin. No separate rig required.

### Captain Brine — defensive starter

Default unlocked; Deck Cannon. Base Hull 110 instead of 100, speed 5.7 instead of 6. **Hold Fast:** +0.5 armor per level, maximum +25 armor. Enables a more forgiving approach without immunity or passive healing. The movement cost creates a real overtime tradeoff. Portrait: broad hat, repair patches and barnacle insignia.

### Captain Vale — the Smuggler

Unlock after licensing Broadside and opening 40 paid chests cumulatively; automatically grant the Captain license, no second Rum purchase. Broadside starting Weapon. Base Hull 90. **Heavy Pockets:** +0.5% run Gold pickup value per level, maximum +25%; additionally gain `min(0.30, unspent_run_gold / 2000)` additive damage. Thus 200 Gold gives +10% and 600 gives the +30% cap. Recalculate when Gold changes; treasury, passive production, pending raid theft and marketplace value do not count. The Gold bonus is additive with Quartermaster and respects +150% global cap. Portrait: polished buttons and far too many keys.

### Captain Flint — the Powderrunner

Unlock after licensing Kegs and scoring 100 cumulative Keg kills; automatically grant. Starts Kegs. Base Hull 90, speed 6.3. **Running Fuse:** above 4 m/s for two continuous active seconds grants +15% additive Keg damage; drops immediately below that threshold. Adds +0.5% Keg damage per player level, cap +25%, in the same additive source bucket. No Gold from movement, no extra explosions, and no benefit while paused. Portrait: scorched cuffs and an unlit fuse tucked in a hat.

### Captain Cinder — the Firekeeper

Unlock after licensing Flamethrower and recording 300 burn applications; automatically grant. Starts Flamethrower. Base Hull 90. **Banked Embers:** +0.5% burn tick damage per level, cap +30%, as a named burn-only additive bucket. No shorter ticks or automatic extra stacks. On each 50th rewardable burning-enemy death heal 5 Hull; counter persists across stages, never advances on zero-credit summons, healing capped at max Hull. This is earned small recovery, not damage-based lifesteal. Portrait: brass mask and a soot-blackened lantern.

### Captain Tempest — the Stormchaser

Unlock after licensing Lightning Mast and surviving 60 active seconds of overtime in one stage; automatically grant. Starts Lightning Mast. Base Hull 85, speed 6.3. **Conductive Wake:** +0.5% lightning damage per level, cap +30%, in a named elemental additive bucket. A qualifying near miss increases lightning damage by an additional +20% for two active seconds, with a six-second cooldown. Multiple near misses do not stack/extend the window. The bonus affects lightning damage, not proc chance, extra targets or persistent holding ranks. Portrait: storm-glass monocle and wind-torn scarf.

Do not apply Cinder/Tempest's elemental bonuses twice to child hits whose damage already derives from a resolved parent hit of the same element. Content events must record which multipliers are already included.

## 3. Unlock and run state

Quest/license ownership is account progression, not Item holding ownership. Selling an Item does not remove a Captain, and unlocking a Captain does not grant its associated Items. Qualification checks after verified events; each grant has a unique receipt. Current runs retain their selected Captain even if another unlocks mid-run.

Persist Captain ID/version, selected starting Weapon, near-miss timestamps, continuous movement duration, counted burn deaths and last threshold reward. Derived per-level bonuses need not be independently granted each reconnect; recompute from saved L once. Prevent repeated level/Hull grants when a snapshot is restored.

## 4. Mastery challenges on the home map

Owner confirmed these three presets as the initial catalogue during the 13 September readiness audit. They replace the earlier four challenge families. First-completion rewards are once per account/preset, never per biome/tier. Multi-stage home challenges remain a later extension; multi-stage Voyages are unchanged.

Reuse the owner's stable home terrain and the existing home-challenge mode. Select one preset before starting. No altered home challenge contributes to global or community Voyage scoreboards. Contracts/ordinary home rewards follow existing eligible-mode rules.

### Fast Flag

Tier 1, one-stage objective. Defeat the boss by active stage time 6:00 inclusive. Unlock after first claim. It does not require the player to die by 6:00; after success keep fighting until death. Passing the deadline marks the objective failed but does not kill the player. First completion: title **Quick on the Draw** plus 30 Rum, subject to normal overflow.

### Lightly Armed

Tier 1, cap Weapon capacity at one for this run. Logbooks/Items remain normal. Defeat the objective boss. Requires first claim, no specific Captain. First completion: decorative **Single Cannon Pennant** plus 30 Rum. Skin/trophy instances follow ordinary cosmetic ownership rules; the achievement remains with the player if the decoration is sold.

### Steady Hands

Tier 1, guarantee exactly one Signal Buoy within the existing four-POI allocation, alongside Repair Wreck and Powder Cache; choose the last eligible type normally. Finish its eight-second charge without leaving the circle, then defeat the boss. Once charging starts, leaving the radius fails this challenge objective even though the buoy can still be completed normally. Damage alone does not fail it if the ship remains inside. First completion: title **Steady Hands** plus 30 Rum. No new shrine is spawned after the layout begins.

One-time grants do not repeat when replaying the preset. Ordinary home payouts remain available; no permanent resource multiplier is added. Challenge failures do not force death or offer extraction. UI separates failed challenge, secured boss objective and eventual run death. Whether more presets are worthwhile is a playtest result, not a launch requirement.

## 5. Overtime milestones and run reflection

Record personal survival checkpoints at 60, 120 and 240 active overtime seconds within one stage; portal reset restarts this counter for the next stage. First 60-second milestone qualifies Tempest when other prerequisites are met. The later milestones initially grant profile achievement records only, not repeatable resource rewards or additional leaderboards.

Post-run summary should explain Captain contribution, major damage sources, healing, proc counts, stage departure times and killing hit/source. Include optional pinning of an unlocked goal for the next attempt. A build summary is a record of what happened; a shared build does not guarantee the same random drops.

## 6. Scope and tests

Prototype Rook, Brine and Flint first. Defer Vale/Cinder/Tempest's production portraits until the profiles show meaningfully different routing or choices. All six are defined so the full roster can be implemented consistently if retained after testing.

Tests: Captain/skin independence, first-conquest Cannon override, locked signature Weapon, per-level recomputation on resume, temporary bonus expiry at pause boundaries, Gold spending decreasing Vale's damage, burning-kill threshold not double-granted, and no Captain-specific exclusion from the shared Voyage leaderboard. Compare best-build and weak-build performance; later unlocks should expand playstyles rather than strictly replace the starters.
