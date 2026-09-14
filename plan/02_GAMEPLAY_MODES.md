# 02 — Core Loop, Modes and Progression

## Run state machine

`Setup → StageIntro → Playing ↔ ChoicePaused → BossActive → BossSecured → Playing/PortalReady → StageTransition|PlayerDead → Verifying → Results`

- Start every run at player Level 0. Freeze Captain, signature Weapon, eligible Items, permanent ranks, mode, biome, tier, affiliations, content version and RNG states.
- Carry level, XP remainder, Hull fraction, Weapons, Logbooks, Item stacks, run Gold, RNG-tool charges and chest purchase count through portals.
- Do not heal, reset chest price or restore RNG tools at a portal.
- Final boss sets `ObjectiveSecured`; it never creates extraction UI. Death opens Results once.
- Results separate objective, stages, kills, run Gold banked, boss grants, theft, overflow, permanent progress, contracts, mastery/events, damage sources and killing blow.

## Modes

- **First conquest:** fixed reserved home geometry across retries; new encounter/reward seeds; Deck Cannon override; Barnacle King secures Saltwind, Ledger rank 1, 500 Gold, 180 Rum and 24-hour shield.
- **Voyage:** free entry; player chooses unlocked biome and tier; fresh valid layout; Tier 1/2/3 has 1/2/3 stages; leaderboard eligible.
- **Expansion:** requires prior Zone and Defense rank 2/4; reveal one persistent outer band; no fee; success saves immediately; failure loses no owned geography.
- **Home mastery:** persistent home geometry, Tier 1 one-stage, no leaderboard. Fast Flag: boss by 6:00; Lightly Armed: one Weapon slot; Steady Hands: complete Signal Buoy charge without leaving, then boss.
- **Raid:** fee 40/80/120 Gold by target Zones; immutable attack layout; one highest-Zone boss; generated rewards plus once-only theft; no Voyage leaderboard.
- **Defense practice:** saved configuration/sample permutation; route and combat metrics; zero permanent rewards or event progress.

## Unlock progression

- Saltwind is first. Gravewater and Devil's Maw are concentric expansions.
- Ledger ranks 1–3/4–6/7–9 require Saltwind/Gravewater/Devil's Maw.
- New Voyage tiers unlock by defeating the current tier's final required boss; death afterward does not revoke it.
- Content quests are permanent qualification records. Rum purchases grant initial licensed ownership where specified.
- Captains change freely between runs. Rook and Brine start unlocked; Vale, Flint, Cinder and Tempest require their documented Weapon plus gameplay condition.

## Tutorial sequence

Use contextual, dismissible prompts; never stop post-boss survival for narrative.

1. Move for five seconds.
2. Observe auto-fire and collect XP.
3. Resolve first level choice.
4. Discover/open a nearby affordable chest.
5. Read minimap and one POI prompt.
6. Find/activate Beacon.
7. Read one boss telegraph prompt.
8. On boss death show `TERRITORY CLAIMED — SURVIVE AS LONG AS YOU CAN`.
9. On death explain banked Gold, Rum, permanent progress, Ledger and Next Voyage.

## Acceptance

- No mode offers voluntary extraction.
- Boss milestones survive later death/reconnect.
- No finite object replenishes.
- Mode eligibility, rewards and leaderboard inclusion are data-driven and covered by fixtures.

