# 00 — Master Implementation Specification

## Product definition

Rogue Pirates is a polished, stylized 3D ship-based bullet-heaven roguelite. A player steers while Weapons auto-attack, gathers XP and run Gold, builds through randomized offers and finite exploration, activates a hidden Treasury Beacon, defeats bosses and continues through escalating overtime until death. Persistent progression expands a concentric Territory, unlocks content, improves a Captain's Ledger and lets the player author defenses. Asynchronous raids rearrange recognizable sections of another player's Territory. Crews, events, cosmetics and a light comic story support retention and marketing.

## Success definition

- First-conquest completion in observed onboarding tests: at least 70%.
- Immediate retry after an early weak-build death: at least 40%.
- At least 80% of testers can explain Beacon, boss success, portal choice, post-boss survival and death settlement without staff help.
- Product validation targets: D1 ≥35%, D7 ≥12%, D30 ≥5%, segmented by platform and acquisition source. These are gates, not forecasts.
- Crash-free sessions ≥99.5%; ANR/hang rate <0.5%.
- Stable 30 FPS on minimum devices and 60 FPS on recommended devices.
- Zero duplicated currency, theft, purchase, ad or prize grants in release testing.
- Ads contribute <5% of Gold and Rum supply for the median engaged cohort.
- Paid ownership has no statistically meaningful score advantage after controlling for playtime and account progression.

## Release and team assumptions

- Four-to-eight-person core team: product/economy design, gameplay/simulation, Unity/platform, backend, 3D/technical art, UI/2D, QA/release; use contractors for audio, animation, writing, localization, marketing and legal review.
- Quality-gated schedule. Milestone exits control progress; no date overrides a failed gate.
- One coordinated public launch across Android, iOS, direct web, Steam, CrazyGames and Poki. All named channels must pass their own certification and signed commercial terms before the coordinated date.
- Landscape-only gameplay for launch. Menus support the same orientation.
- Audience is 13+. Real-money/SOL event prizes use separately published age and region eligibility.
- Unity 6000.3.11f1, URP and Input System remain pinned until Gate 0 proves or rejects them.
- Stylized modular low-to-mid-poly 3D, with premium timing, feedback, audio and VFX. `references/**` assets are excluded.

## Non-negotiable rules

- Fixed 30 Hz authoritative simulation; rendering never changes outcomes.
- Boss victory never extracts the player. Death is the only normal run ending.
- Final-boss victory secures the objective, then enemies continue until death.
- Intermediate boss victory opens a portal; the player decides when to enter.
- Each stage has a 10-minute soft timer followed by escalating Drowned Pursuit overtime.
- Chests and POIs are allocated once at stage creation and never replenish.
- Ordinary Voyages use fresh maps; the home remains persistent; raids permute the owner's recognizable sections.
- One weekly global board uses the best verified kill count from a single ordinary Voyage. All biomes, tiers and account progression compete together.
- Player trading, marketplace, wallets and blockchain inventory do not exist.
- Gold and Rum cannot be bought directly. Paid products are account-bound cosmetics or the cosmetic Chronicle track.
- Valid death banks 100% of unspent run Gold after chest spending.
- Ads never interrupt combat, revive, multiply a score or grant permanent Item progress.
- All reward-bearing state is server-authoritative and idempotent.

## Complete player journey

1. Boot validates client/config versions, applies safe-area/accessibility defaults and opens guest or platform sign-in.
2. A new player may watch three skippable narrative panels, then begins first conquest with Deck Cannon.
3. Context prompts teach movement, XP, level choice, chest cost, minimap discovery, Beacon activation and boss telegraphs.
4. Barnacle King death immediately secures Saltwind and starts the beginner shield; play continues until player death.
5. Results show success separately from death, bank run Gold, grant boss Gold/Rum, reveal permanent progress and introduce the Ledger.
6. Home navigation exposes Play, Territory, Ledger, Collection, Defense, Crew, Events, Ship & Cosmetics, Store and Settings as they unlock.
7. The player alternates Voyages, expansion, home mastery, raids, defense setup, collection goals, Crew events and cosmetic customization.
8. One ordinary run can advance quests, contracts, daily/weekly goals, Crew/event standings, Chronicle Marks and personal records.

## Architecture

- Unity assemblies: `Simulation`, `Content`, `Presentation`, `AccountClient`, `PlatformAdapters`, `EditorValidation`, `Tests`.
- Authoritative Simulation is pure C#: no MonoBehaviour, Unity physics, `Time.deltaTime`, animation callbacks or `UnityEngine.Random`.
- Compile the same deterministic source into Unity native/WebAssembly and a .NET 10 verifier.
- Backend is an ASP.NET Core modular monolith plus independently scalable verifier worker.
- PostgreSQL is canonical. Railway hosts initial API/worker/database; Cloudflare CDN/R2 hosts versioned public assets and private replay objects.
- Use HTTPS request/response, an outbox table and background jobs. Do not add Redis/Kafka before measurements require them.
- ScriptableObjects are authoring views exported to validated, readable JSON with schema and manifest versions.

## Delivery gates

- **G0 Feasibility:** deterministic parity, device budgets, package pinning, CI and backend cost benchmark.
- **G1 Foundation:** pure simulation, pause stack, tick ordering, content pipeline and golden tests.
- **G2 Saltwind slice:** polished first conquest and one-stage replay loop with local-only progression.
- **G3 Persistence:** accounts, verifier, receipts, Ledger, holdings and cross-device resume.
- **G4 Full game:** three biomes, multi-stage Voyages, home expansion, mastery, Defense Director and raids.
- **G5 Social:** Crews, verified events, cosmetic inventory, operator tools and narrative package.
- **G6 Commerce:** platform adapters, ads, IAP, Chronicle, consent and restoration/refund paths.
- **G7 Launch:** production assets, load/security/accessibility QA, store approval and operational rehearsal.

No public reward-bearing account, leaderboard, raid or purchase may bypass its preceding gate.

