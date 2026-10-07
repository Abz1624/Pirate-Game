# Rogue Pirates — V5 Plan (Live Game)

Started 29 Sep 2026 on branch `v5-live-game` (from `v4-modular-architecture` at `f1b9a4cf`). Status: **rev 10 (6 Oct): playtest detour recorded; M8z (playtest integration) added before M9; P1-P11 answered (D18, D19)
(section 9)**. Explanations of every answer: `plan/v5/ANSWERS.md`.

**Demo interlude (30 Sep, merged at `27a238bc`, V5 resumed at `86724e6b`).** Work paused after M3 for a demo on
branch `demo`, then merged back. Kept: the art team's menu, Captain select, SaltWind HUD and level-up cards
(`ArtMenuView`, `SaltWindHudPresenter`, art mode in `LevelUpPresenter`); per-weapon/ship/enemy VFX profiles and the VFX
editor; POI prefabs (`Assets/_Game/V4/Demo/POI`) and level-up card prefabs; music; the floating touch joystick (app and
mobile browsers); safe-area HUD; mobile render settings; smarter enemies (island routing, intercept, surround, led
shots, shot reach); shots stop at islands; 3x walls and a camera clamped to the walls and the storm; dark minimap fog.
The demo's switches live in `DemoMode` (`DemoJuiceSettings`, `V4SaltWindHudSettings`; off in tests): the boss rises out
of the whirlpool after the beacon summon (no auto-boss); POIs, Captain active, boost, barks, tutorial, Logbook cards and
the flamethrower are back; the beacon pointer and the start-of-run message stay hidden. **Balance re-check (1 Oct,
`plan/v5/balance-recheck/`):** half XP and the 1.625x ship speed moved into config (`PickupConfig.xpPickupMultiplier`,
`RunConfig.shipSpeedMultiplier`) and the simulator; half XP alone took the model from 2 to 24 of 76 targets missed; the
owner kept half XP and chose a retune around it (enemy HP growth x0.55, damage growth x0.7, evolution from weapon
level 6, Lightning Mast -2%, the director curve re-measured, level and level-up targets at the slower pace): 2 of 76
missed again, the same two as M3. Plan effects: **M4** fixes the flamethrower VFX; **M7** starts from the floating joystick, safe-area HUD and mobile render settings (the 174 MB web
data download still needs M7's budget work); **M8** builds its hub on the art menu (its Leaderboard, Ledger, Raid,
Profile and Settings buttons are placeholders). Later: fold the remaining switches into configs and retire
`DemoMode`.

**Playtest detour (1-6 Oct, branch `playtest` = `v5-live-game` + 32 commits; logs `plan/playtest/NOTES.md`,
`ROUND8-LOG.md`, review `plan/playtest/V5-RESUME.md`).** Nine owner playtest rounds outside the phase gate turned the
voyage into a Survivor.io-style 15-minute run (one main enemy per band, hordes, crab and Kraken minibosses, a kill-band
difficulty, timed heals, equal level-1 weapon damage, all 19 weapons free, Survivor.io XP curve, HP growing with time),
reworked enemies, weapons and visuals, and built ahead of the plan: guest profiles, a weekly kills leaderboard, and 1v1
PvP (lobby, invites, draft, relayed match, bot, rematch) on an ASP.NET API live on Railway. This **supersedes** parts of
M3c (level rhythm), M3d (no HP growth, the 10-minute sketch), M4 (role ±20%), M7 (Esc pause, minimap, barks) and M8
(mode select), and D17's evolution level and Logbook ranks (pending the owner's confirmation, question P1). Tests on
5 Oct: EditMode 438/499, PlayMode 51/63 (the failures pin old values; four are real rule violations), server 70/70.
Before M9 the playtest work is merged and gated as **M8z** (below). Desktop WebGL is the main test build; WebGL mobile
and Android only on request.

V5 turns the finished V4 voyage into a live game: a balanced voyage, a menu with a shop, the rest of the content, mobile
controls, verified accounts with custodial Solana devnet wallets, a player island with defenses, async raids and races,
real-time PvP modes, wagers, a creator economy (sails, islands, marketplace), brand bounties and retention systems.

It keeps the V4 way of working: one module per phase, config as data, one commit per passed phase, and a fixed gate after
every phase (tests, playable check, independent review, owner playtest).

- Tracking page (artifact): "Rogue Pirates V5 Plan" — the owner reads that page; this file is the agents' source of truth.
  The page is generated from this file and `status.json` by `plan/v5/tools/build_artifact.py`.
- Independent reviewer: `.claude/agents/v5-reviewer.md` (read-only; reviews this plan and every phase's code).
- Design source of truth: the GDD Claude Doc "Rogue Pirates — Game Design Document"; every decision made here is
  written into its Decision log the same day.
- Evidence: `plan/v5/phase-NN-<name>/` per phase. Owner to-dos: `plan/v5/TODO-FOR-YOU.md`. Research:
  `plan/v5/research/`.

---

## 1. Request map

Every owner request (29 Sep 2026) and the phase(s) that deliver it. The reviewer checks this table each plan review.

| # | Request | Phases |
|---|---|---|
| 1 | Balance the game; Vampire Survivors / Survivor.io enemy flow and curves | M1 (flow), M2 (model and retune), M3 (builds); every content phase balances against the M2 model |
| 2 | Main menu with shops and items | M8 (web2, local RUM) → M10b (server) → M11 (token prices) |
| 3 | Remaining weapons, logbooks and items, unlockable in the shop | M4, M4b (nine new weapons; D21: the Ramming Prow replaced by the Squid Ink Pots in M8z), M5 (logbooks, items; unlock prices), M8 (shop) |
| 4 | Art guide for weapons, effects, items, logbooks, POIs; what exists and what is missing | M6 (Claude Doc); updated in M14 (defenses, decorations), M25 (sail stickers), M27 (brand kits) |
| 5 | Mobile UI for mobile and desktop browsers | M7 (touch controls, responsive HUD, safe area, WebGL mobile profile); every later screen follows D9 |
| 6 | Base building: defenses plus a defender-authored spawn schedule (when and how many, not where) | M14 |
| 7 | Island raiding of other players' bases | M13b (server sessions), M15 |
| 8 | 1v1 Arena with a free pick of every weapon and logbook | M13b, M21 (multi-ship runtime, lobby), M22 |
| 9 | Hunt: 4-player free-for-all, last one standing, with enemies and bosses | M13b, M21, M23 |
| 10 | Async Island Race: one attempt to conquer a base in the fastest time | M13b (server sessions), M17 |
| 11 | Wagers in SOL/SKR/ORE (web3) or RUM (web2) on Arena, Hunt and Race; winner takes all; 5% house cut | M24 |
| 12 | Invites to raids, Arena, Hunt and Race (web2 and web3) | M19 (links, friends, raid and race invites), M21 (lobby by invite code), M22 and M23 (Arena and Hunt invites), M24 (wagered invites) |
| 13 | Weekly leaderboard by kills in a single run | M13b (ranked server sessions), M18 |
| 14 | AI captains that raid and defend for the player, with configurable parameters | M16 |
| 15 | Design and paint sails; trade them for RUM and tokens | M25 (designer), M26 (trading) |
| 16 | Email sign-up that creates a server (custodial) wallet; the game pays devnet fees | M10 (server), M10b (sign-up), M11 (wallets, fee payer) |
| 17 | MagicBlock for VRF and record keeping, seamless, devnet only | M12 |
| 18 | Connect an external wallet and migrate custodial records and inventory to it (player pays fees) | M13 (tokens, records, inventory), M26 (NFTs) |
| 19 | Marketplace for islands and sails, web2 and web3; NFTs tradable on external Solana marketplaces with 5% royalties | M26 (NFTs, royalties, ownership sync), M26b (marketplace) |
| 20 | Bounty islands funded by brands; top kill players share the bounty; brand assets in levels | M13b (server sessions), M27 (+ ideas in section 7) |
| 21 | RUM powers the web2 economy; SOL/SKR/ORE power the onchain economy (devnet) | D4; enforced by tests from M8 on; audited in M28 |
| 22 | Island decorations sold in the store (web2 and web3); list them and add them to the art guide | M6 (catalogue), M14 (placement and store) |
| 23 | Notifications and share cards to show off and invite on socials | M19 |
| 24 | Retention loops and mechanics | M20 (+ section 8) |
| 25 | (1 Oct) a: fewer level-ups, gaps growing like a Fibonacci series | M3c |
| 26 | (1 Oct) b: unblown powder kegs sink and are gone after 45 s | M3b |
| 27 | (1 Oct) c: a round touch button for the Captain's special ability | M3b (M7 keeps the other touch buttons) |
| 28 | (1 Oct) d: pooling and garbage for everything spawned | M3e |
| 29 | (1 Oct) e: a peak-valley difficulty curve rising to the 10:00 climax (sketch) | M3d |
| 30 | (1 Oct) f, i: static and dynamic batching | M3e |
| 31 | (1 Oct) g: an island hit bounces the ship back and costs hull | M3b |
| 32 | (1 Oct) h: difficulty from enemy count, stronger distinct types, a higher cap | M3d (count, types), M3e (cap) |
| 33 | (1 Oct) j: occlusion culling | M3e (assessed, not applicable; the far clip and frustum culling instead; per-layer cull distances left for M7) |
| 34 | (1 Oct) k: an enemy that looks the same always behaves the same | M3d |

---

## 2. Decisions

Proposed decisions (P) take effect when the owner approves the plan. Owner-confirmed decisions are marked (C).

| Id | Decision |
|---|---|
| D1 | **Branch and commits.** Branch `v5-live-game`; one commit per passed phase, named `V5 M<n>: <module>`. Playtest-round fixes may get their own commit. (P) |
| D2 | **Gate after every phase** (section 4): targeted tests, a scripted playable check, the independent `v5-reviewer`, then the owner's playtest. No phase starts before the previous one is committed. (C, standing rule) |
| D3 | **Web2 first, web3 as a layer.** Every feature works for a web2-only account with RUM. A web3 account adds a wallet, token prices, NFTs and onchain records. The account's mode is visible and switchable in Settings. (P) |
| D4 | **Two economies, no bridge.** RUM is off-chain and never converts to or from SOL, SKR or ORE, directly or through items: anything bought, earned or traded with RUM stays a web2 item and can never be minted or sold for tokens; only items bought with tokens or created by the player without RUM can be minted (Q24), and a minted item can never be listed or bought for RUM. Token flows never mint RUM. Rewards that scale with a run's output flatten and cap (cap × (1 − ½^(amount ÷ half))); the owner's exceptions: unwagered Arena and Hunt pay a fixed RUM amount per match with no cap (Q10), and AI captains raid up to 6 times a day, each paying the same as a raid you play (Q9), so the raid loot cap and the defender's protection still apply. A test suite enforces this from M8 on. (P) |
| D5 | **Devnet only.** No mainnet RPC, mint or program id anywhere in code or config; a test scans for them. SKR does not exist on devnet and ORE's devnet mint cannot be minted by us, so both use game-owned devnet stand-in mints (classic SPL Token, same decimals: SKR 6, ORE 11) with a tester faucet; mint addresses live in config. (P) |
| D6 | **Server authority for value.** Anything that moves value (RUM, tokens, NFTs, leaderboard prizes, bounties, raid loot, wager pots) is decided by the server's authoritative simulation or checked by the server, then settled once with an idempotency key and recorded onchain. Clients never submit totals. Until a run is checked, its RUM is *pending* and cannot be spent, transferred or wagered. Every run that earns value uses a **server-issued seed**, so seeds cannot be rerolled. How results are checked (Q29, owner's rule: exact replay only if Photon Quantum is chosen, otherwise plausibility checks): ordinary voyages pay RUM after **plausibility checks** (kills, gold and damage within what the seed's spawns and the build allow); everything that moves value *between* players or pays prizes (raids, races, wagers, leaderboard prizes, bounties) is **played on the authoritative match server** (M13b), not on the client. Plausibility-checked voyage RUM can still reach other players once spent in wagers, the marketplace or raids; on devnet it can, right away (Q35); the M28 audit revisits this before anything has value. (C) |
| D7 | **Custody.** Sign-up creates a custodial devnet wallet held by our own server: a keypair per player, the private key encrypted with a master key kept in a Railway secret, only the ciphertext in Postgres, decrypted in memory only to sign the game's own instructions. The master key has an offline backup kept by the owner (Q33) and a key id so it can be rotated (losing it loses every custodial wallet); the fee payer, the treasury and the escrow settle authority use separate secrets. There is no key export for now (Q32); players move their assets to their own wallet instead (M13). The owner keeps the offline master-key backup (Q33). Before any mainnet the master key moves to a key vault or a wallet provider. The game's fee payer pays devnet fees, rate-limited and restricted to the game's own instructions. Keys never reach the client. Migration moves assets to an external wallet at the player's cost. (C, Q3, Q32, Q33) |
| D8 | **Reversals of earlier canon, requested by the owner on 29 Sep** (confirmed by the owner on 30 Sep, Q26): (a) PvP modes (Arena, Hunt) replace "no PvP"; (b) player-vs-player wagers in SOL/SKR/ORE or RUM replace "no stakes"; (c) winning tokens from wagers and brand bounties is a form of play-to-earn, which the GDD ruled out; (d) players hold token balances beside RUM (external assets, not a game currency; RUM stays the only game currency); (e) NFTs and a marketplace replace DEC-009; (f) the Arena lets players draft a build, an exception to "randomized builds, no guaranteed perfect build" that applies to the Arena only — the voyage keeps random builds. No game token is created. Before any mainnet release, real-value wagering, prize pools and bounties need a legal review per launch region (geo-fencing, age checks). (C, Q26) |
| D9 | **Mobile-first UI.** Every new screen: 48 px minimum touch target (72 px primary), `max(24 px, safe-area inset)`, landscape 16:9 to 20:9, no hover-only affordances, 1920×1080 reference scaler. (P) |
| D10 | **V4 architecture still binds**: config assets with id, schema and balance version; no content-name comparisons; no new singletons; modules talk through contracts and events; presentation never writes back; nothing created outside pools during play; Runtime never references DevTools. Direction: Config ← Runtime ← Meta, Chain, Net ← Presentation ← DevTools. New layers: Meta services, Chain services and Netcode, each behind interfaces with a local fake for tests. (P) |
| D11 | **Nothing is deployed and no external account is created without the owner.** Backend, match servers and databases run locally (Docker). **Railway only** (Q17): the web build, the API and Postgres, and the match server all run on Railway, each only from the phase that needs it (the web build whenever the owner wants testers, a test headless server in M9, the API from M10, the match server from M13b); hosting for outside testers (Q17) and third-party accounts (costs in `ANSWERS.md`, Q30) are owner decisions; the owner creates accounts and keeps keys in Railway variables or an uncommitted `.env`. Devnet transactions are allowed. (P) |
| D12 | **Traces stay rare.** V3 is gone, so there are no parity traces. Balance is verified by the balance model (EditMode), targeted PlayMode checks at chosen run times and the owner's playtest. No autopilot sweeps (Q20): instead a **new Monte Carlo balance simulator written from scratch on V4** in M2 (no V3 code, including the old Balance Lab) runs thousands of simulated runs across builds, the player personalities in `PLAYER_PERSONALITIES.md` and enemy mixes. (C, Q20) |
| D13 | **Art.** Gameplay ships on placeholders tagged `PLACEHOLDER_v5_*`; production art follows the M6 art guide. (P) |
| D14 | **Cosmetics never change stats.** Decorations, sails, flags and brand assets are cosmetic only; the AI captain and defenses use the same rules for everyone. Tokens never buy power that RUM cannot (Q25). Decorations stay cosmetic (Q15). (C) |
| D15 | **Anti-collusion, deferred** (Q27: "not for now"). The data model keeps linked-account signals (device, wallet, payment trail) and config switches for limits (linked-account wagers and trades, RUM price bands, daily transfer caps, referral rules, race-owner wagers), all off by default. Every phase that adds a hook also tests it with its switch on. The M28 audit revisits them before anything has real value. (C) |
| D16 | **The v3-parity preset** stays as a regression baseline until M28; every new V5 mechanic is switched off under it. (P) |
| D17 | **Owner answers of 30 Sep** (explained in `ANSWERS.md`): stage order as planned (Q1); fixed token prices (Q5); 5% royalty only (Q6); island NFT = a plot (Q7); bounty split top 10 on a curve (Q11); no sail moderation on devnet (Q13); four new weapons (Q19); one enemy cap for every device, sized for a low-end phone (Q21); endless overtime ramp (Q22); RUM items never become tokens (Q24); tokens buy cosmetics and RUM-equivalent unlocks only (Q25); reversals confirmed (Q26); Hunt wager winner takes all (Q28); custody on our Railway server, no key export, owner keeps the master-key backup (Q3, Q32, Q33, D7); AI captains raid 6 times a day at full loot (Q9); unwagered PvP pays fixed RUM per match with no cap, plus a rating (Q10); Sign in with Google plus email codes (Q12); one weekly board across voyage maps with adaptive difficulty (Q16); Railway only (Q17); evolution at level 8 plus multi-level rare cards (Q18b); weapons: Anchor Flail, Chainshot, Siren Bell, Parrot Squadron, Maelstrom Charge, Lighthouse Lantern, Ramming Prow, Cursed Doubloons, Monkey Gunners, no Ghost Cannons (Q19, Q19b); web3 players earn RUM too (Q24b); plausibility checks unless Quantum (Q29); Unity Netcode for GameObjects for multiplayer (Q2, chosen over Fish-Net to avoid the $60 Pro licence); treasury protection: vault 80%, treasury 20%, a raid takes 20% of the treasury, 8-hour shield, at most 10% of RUM lost per day, 72 hours for new players (Q23b); reference phone Galaxy A15 / A16 4G at a steady 30 fps, floor Galaxy A06 at 20+ fps, iPhone 11 and desktop at 60 fps (Q31); forfeits pay no RUM but a genuine short match does (Q34); plausibility-checked voyage RUM usable right away on devnet, revisited in M28 (Q35); 12 weapon levels and 10–12 logbook ranks kept (Q18, M3); raids steal RUM from an exposed treasury, a vault stays safe, with shields (Q23, M14). (C) |
| D18 | **Owner answers of 6 Oct (after the playtest detour, P1-P10)**: the plan always follows the game as played: the playtest rules replace M3c, M3d and D17's evolution level and Logbook ranks (P1); overtime Rum stays uncapped, a recorded exception to D4 (P2); every weapon, Captain, Logbook and item is free for now (`DemoJuiceSettings.unlockEverything`), locks come back later (P3); the v3-parity preset is retired, and D16 with it (P4); multiplayer moves to Unity Netcode for GameObjects only, the API relay stays until the Netcode 1v1 replaces it (P5, D17 Q2); rated 1v1 gets server anti-cheat now (P6); an early Beacon summon keeps the boss's 14:00 hull (`DirectorConfig.bossHpAsIfAtSeconds`, P7); the D9 touch rules still bind new screens (P8); the API deploys on every push (P9); the playtest is merged into `v5-live-game` and work continues there (P10, merge `b5936c4d`, tag `playtest-r10`). Also: anti-collusion stays off for now (Q27 trade-off accepted); Railway and the domain (roguepirates.fun) exist; Alchemy replaces Helius as the devnet RPC (Q30); the treasury rules (Q23b) are reopened for a livelier economy (question P11). (C) |
| D19 | **Owner answers of 6 Oct, second batch**: (1) **Raid economy (P11) is option B with 4-hour shields**, replacing Q23b: the vault keeps 60% of an island's RUM (never raidable), a successful raid takes 25% of the exposed treasury (half on a partial raid), a 4-hour shield after being raided, attacking ends your own shield, and a small raid entry fee as a RUM sink; no daily loss cap, no new-player protection and no free revenge raid (owner, 6 Oct); all in config, built in M14/M15. (2) **Netcode matches are server-authoritative**: the match server runs the simulation and owns hull, damage, armor, regeneration, items and sinking; clients send inputs only and render the server's state, so no client report decides a result (M9 proves it, M13b hosts it, M21/M22 use it). Until then the API relay's referee checks hits (weapon in draft, range, damage cap) but sinking stays client-reported. (3) Health pickups stay as they were (dropped at kills). (4) Accounts beyond Railway and the domain are created when their phase needs them. (5) The API is deployed by the agent with `server/deploy.ps1` when a server change needs to go live; no automatic deploy on push (CI runs the server tests only). (6) Arena fights (minibosses, scheduled elites) keep the waves paused until they end, however long they take; an on-screen pointer shows them when off screen (no limit on the number of arrows). (7) Every weapon is allowed in PvP, Mortar and Maelstrom included. (8) The relay's damage cap stays until the Netcode match server replaces the relay; no further work on relay exploits, since server-authoritative Netcode removes them (7 Oct). (C) |
| D20 | **Owner answers of 7 Oct (M8z review)**: XP per kill is the same at every graphics quality and with Reduced Effects (the burst pieces split the barrel's XP exactly; the XP table is retuned to keep the measured pace); the darker red Cutthroat Skiff and greener Needle Sloop are kept; Ironclad joins the Saltwind Logbook pool; voyage session files download only in playtest builds (a build switch; the owner says when a public build is made); every weapon has the same level-1 single-target DPS (round 6's weapon roles are undone; the test is enforced again). (C) |
| D21 | **Owner answers of 7 Oct (open questions)**: loot-barrel sparkles removed (Q16); the Knockback card removed from weapons it does nothing for (Q17); **Ramming Prow removed** and replaced by an auto-attacking weapon that slows enemies, especially the back of the pack, as a strategic tool (Q19, plus the owner's slow-weapon idea; the plan and the art guide follow); the new weapons' rank 4/8/12 milestones stay (Q20); the Harpoon's tar becomes a real slow (Q23); the Reef Crab takes slows and knockback like the boss (half slow, no knockback) (Q24); slows don't shorten charge dashes (Q25); the flame's look is replaced by the owner later (Q26); one Luck scale for cards and chests (Q27); Admiral works on elites and the boss (Q32); new enemy types later, models by the art team (Q5); Collection from Profile (Q37); New badges and sales when locks return (Q38); the same gameplay caps on every device (Q33); the 1024 texture cap on every web build, desktop too (Q34); health bars sort every 8 frames (Q42); phone texture checks at the next phone build (Q40, Q44). Q28: Battle Signal and Full Sail join Powder Rush, Magnetic Tide and Dead Calm in the drop pool (Tailwind and Salvage Chime stay out); the Unity splash logo is off (Unity 6 allows it without Pro); the 1024 texture cap on desktop is fine for now. (C) |

---

## 3. Architecture additions

```
Config (ScriptableObjects, hashed)            ← designers edit only this
  ├─ Voyage: V4 configs + BalanceTargets, WaveTimeline minute waves, new content
  ├─ Meta: StoreCatalog, Decorations, SailRules, RetentionConfig, LeaderboardConfig, CollusionRules
  └─ Modes: BaseRules, RaidRules, RaceRules, ArenaRules, HuntRules, WagerRules, AiCaptainProfile
Runtime (V4 run)  ── contracts ──  Meta services  ── HTTPS/WebSocket ──  Game server (ASP.NET Core, .NET 10 LTS)
  + input recorder                  IAccountService                       accounts, ledger, inventory, store,
                                    IEconomyService (RUM)                 leaderboards, raids, races, wagers,
                                    IInventoryService                     notifications, plausibility checker
                                    IChainService (web3)                  │
                                    IMatchService (netcode)               ├─ Postgres (local Docker)
Presentation (uGUI, mobile-first)                                          ├─ Solana devnet: custodial wallets, fee payer,
                                                                           │   Anchor program, Metaplex Core NFTs
                                                                           ├─ MagicBlock: VRF seeds, Ephemeral Rollup records
                                                                           └─ Match server (authoritative, headless Unity)
```

- **New assemblies**: `RoguePirates.V5.Meta` (service contracts, local fakes, client), `RoguePirates.V5.Chain` (client
  wallet connect only; no keys), `RoguePirates.V5.Net` (netcode client), `RoguePirates.V5.Presentation.Meta` (menus,
  shop, base editor). Direction: Config ← Runtime ← Meta, Chain, Net ← Presentation ← DevTools (as D10).
- **Server**: the existing `server/RoguePirates.Api` stub (.NET 8, in-memory) moves to .NET 10 LTS (.NET 8 support ends
  10 Nov 2026) and grows into a modular monolith with Postgres (`server/migrations/001_initial.sql` is the starting
  schema). Money-moving commands: authenticated, idempotency key, expected version, typed results, one settlement
  enforced by database constraints.
- **Onchain**: one Anchor 1.0 program `rogue_pirates` (player record, match record, wager escrow, bounty escrow, NFT
  listings), MagicBlock delegation for live match records and VRF for seeds; Metaplex Core for sails and islands
  (Royalties plugin at 5%, allow-list including our program, Tensor and Magic Eden). Old reference: branch
  `Solana-Integration` (`onchain/pirate_coop`).
- **Verification** (D6, Q29): ordinary voyages are checked for plausibility on the server; modes that move value
  between players are played on the authoritative match server. (Exact replay verification would have needed Photon
  Quantum's deterministic simulation; Q2 chose Netcode for GameObjects.) The WebGL client and the headless server are two builds
  of the same project; both build ids are recorded with every run. Known obstacles today:
  the run seed is chosen on the client (`HomeSeed.cs:24`, `WorldPort.cs:57`; value-earning runs will use server-issued
  seeds), a visual-only global draw (`SalvageChestWaterFloat.cs:35`), and Rigidbody physics for the ship and enemies.
  `server/RoguePirates.Verifier` is a placeholder (it hashes a seed, it does not replay); M10 replaces it with the
  plausibility checker.

---

## 4. Phase gate (every phase)

1. **Pre-flight**: clean tree on `v5-live-game`; Editor `ready`; no compile errors; phase start commit recorded in the
   phase `PLAN.md`.
2. **Detail the phase**: expand the task list in `phase-NN-*/PLAN.md` from this plan and the code; the reviewer
   re-reviews any phase whose scope changed.
3. **Implement** in task order, recompiling after each batch; zero console errors before continuing.
4. **Data** through config assets and editor generators; the config validator passes.
5. **Scene changes** only through `unity command` (no hand-edited YAML).
6. **Tests**: new tests for every behaviour change (targeted: jump the run clock to the moment being checked), then the
   full EditMode and PlayMode suites; server tests (`dotnet test`) and program tests (`anchor test`) where the phase
   touches them.
7. **Automated playable check**: a scripted Play Mode run (or scripted client/server session) of the phase's feature
   with screenshots and an error-free console; a WebGL build check from M7 on for UI phases.
8. **Evidence** in `plan/v5/phase-NN-*/`: `PLAN.md` progress table, test results, console log, screenshots, playable
   check notes, write-set summary.
9. **Independent review** by `v5-reviewer` (code mode). Every finding is fixed or recorded with a reason; affected tests
   and the playable check run again.
10. **Tracking page** updated (progress log, phase chip, results, proof, review table, TODOs) and the GDD Decision log.
11. **Owner playtest** — work stops here. The report says what changed, what to try, and what needs a decision.
12. **Close**: apply feedback (re-review if large), tick TODOs, commit `V5 M<n>: <module>`, then the next phase.

Test commands (repo root, Editor open):

```powershell
$env:UNITY_PROJECT_PATH = Join-Path (Get-Location) 'Pirate Game Unity'
unity command run_tests --mode EditMode --async_tests true
unity command test_status
unity command run_tests --mode PlayMode --async_tests true
unity command console --tail 200 --level error
dotnet test server/RoguePirates.Api.Tests        # from M10
anchor test                                      # from M12, in onchain/
```

---

## 5. Phases

Task codes: **I** implement · **G** generate or tune data · **N** integrate into scenes and wiring · **T** test ·
**C** cleanup or setup · **D** document. From M9 on, each phase's task list is detailed further at phase start (gate
step 2) and re-reviewed.

### Stage A — The voyage

#### M0 — Setup and baseline

Goal: the plan, reviewer, tracking page and evidence scaffolding exist, and the current game's test baseline is recorded.

- C0.1 Branch `v5-live-game` from `v4-modular-architecture` (`f1b9a4cf`).
- C0.2 Independent reviewer agent `.claude/agents/v5-reviewer.md` (read-only; plan mode and phase code mode).
- D0.1 This plan, `TODO-FOR-YOU.md`, `phase-00-setup/PLAN.md`, research notes; the tracking page and its generator.
- T0.1 Baseline: full EditMode and PlayMode suites in the open Editor; results saved as evidence.
- T0.2 Plan review by the independent reviewer; findings fixed in the plan and listed on the tracking page.
- D0.2 GDD: Decision log rows for the new direction, the V5 roadmap, new questions on the "Questions for you" board.

Automated check: both suites green; the reviewer's findings closed in the plan.
Owner check: read the plan, answer the questions, approve or change the route.
Done when: the owner approves the plan (or its first stage).

#### M1 — Enemy flow

Goal: a voyage that paces like Vampire Survivors and Survivor.io: a steady crowd whose size swings minute by minute, a
new enemy mix each minute, horde events and elites on a rhythm, breathing room after big fights; overtime stays the endless ramp
(Q22).

- I1.1 **Minimum-alive floor** on ambient phases (`TimelinePhase.minAlive`): on each spawn tick, when fewer ambient
  enemies are alive than the floor, the spawner tops up (respecting the gap, tag caps, the ambient cap and placement).
  The floor curve is jagged, not a ramp (Mad Forest swings 10–100 in 10 minutes).
- I1.2 **Minute waves**: the five 2-minute bands become ten 1-minute waves, each with its own pool mix, rate, interval
  and floor (tiers enter by wave; the adaptive tier lead still works).
- I1.3 **Horde events** every 2–3 minutes away from the boss window: new focus placements `Ring` (a closing circle),
  `Line` (a sweeping wall) and `Cluster` (a fast pack from one side); warning toast and edge markers.
- I1.4 **Release windows**: while the Barnacle King is active the floor drops to about 30%; after a miniboss, elite
  beat, boss kill or horde the ambient rate drops for 20–30 s, then the minute's peak resumes.
- I1.5 **Overtime** stays the endless ramp (Q22); only its first-pass numbers are retuned in M2. Overtime Rum (max 15)
  and leftover gold (max 60) are already capped, so it cannot be farmed.
- I1.6 **Enemy cap, memory and pickups**: one alive cap for every device, sized for the reference phone (Q31,
  confirmed: Samsung Galaxy A15 / A16 4G, Helio G99, 4 GB, a steady 30 fps; floor Galaxy A06 at ≥ 20 fps
  on Low; `research/DEVICE_TARGETS.md`), so phone and desktop players compete equally. M1 measures a WebGL smoke build
  on that phone — frame time at 60–80 enemies and **memory** (Chrome on 4–6 GB phones is 32-bit, so the heap must stay
  near 256 MB; today's build allows 2 GB) — and sets the cap before any tuning (at most today's 80). Floors and targets
  are then shares of the cap, so a later change does not reopen M1–M3. New XP merges into one large pickup above a
  pickup cap.
- G1.1 First-pass data for the new mechanics (floors, waves, hordes, windows) from
  `research/BALANCE_RESEARCH.md`; boss HP stays fixed (the GDD forbids boss HP that grows with player strength).
- T1.x EditMode: floor top-up (count, caps, gap, boss pause), minute-wave hand-over, each horde placement, release
  window, pickup merge; every mechanic off under the v3-parity preset (D16). PlayMode:
  jump the clock to 1:00, 3:00, 6:00, 9:00 and 13:00 (invulnerable) and check the alive count and the active wave.

Automated check: a scripted run with screenshots at 1, 3, 6, 9 and 13 minutes; zero console errors; frame time logged.
(Built 30 Sep without the phone: the owner chose a stand-in, so the phone checks below moved to M7.) On the reference phone (Q31): at least 95% of frames under 34 ms over a 15-minute run at the chosen cap, and the floor
phone (Galaxy A06) holding 30 fps on the Low preset, never below 20 fps; memory recorded against the 256 MB ceiling.
Owner playtest: three runs (weak, average, strong build): does the crowd feel constant and changing, do hordes read, is
there breathing room after elites, does overtime still feel like a fair ramp?
Done when: the mechanics work as data, their tests pass, and the owner accepts the flow.

#### M2 — Balance model and retune

Goal: the numbers behind the flow are designed, not guessed: a balance model computes each minute from the configs and
checks it against targets that are data.

- I2.1 `BalanceTargetsConfig`: per-minute targets (0:00–15:00) for enemies alive (min and max, as shares of the cap), kills per minute,
  expected player level, fodder and elite time-to-kill for an on-track build, damage taken per minute, gold per minute.
  Seeded from `research/BALANCE_RESEARCH.md` (fodder ≤ 1 s, elites 4–8 s, Barnacle King 45–90 s; an upgrade every 6–10 s
  in minute 1 and every ~15 s on average to 10:00) and the V4 M10–M13 measurements.
- I2.2 **Balance model** (pure C#, EditMode): per minute, enemy HP and damage, spawn and kill rates, XP income, expected
  level, an on-track build's DPS (weapon configs × expected upgrade path × stat resolver), time-to-kill and incoming
  damage, each compared with its target.
- G2.1 Retune the difficulty curves, XP curve, wave rates and floors, horde sizes and the endless overtime ramp (Q22) so the model
  meets its targets.
- I2.3 Tuning Console **Balance** section: targets, model and live values per minute on one graph.
- I2.4 **Monte Carlo balance simulator, new on V4** (Q20; no V3 code, not even the old Balance Lab): a pure C#
  simulation that reads the V4 configs directly (waves, enemies, weapons, logbooks, items, Captains, XP and rewards) and
  plays a run second by second with seeded random draws. Each run is played by one of the ten **player personalities**
  in `plan/v5/PLAYER_PERSONALITIES.md` (a `PlayerPersonalityConfig` asset: dodge skill, engage distance, pickup greed,
  chest and landmark habits, boss timing, card policy, active use). It runs every weapon pair and thousands of full
  builds per personality and reports death time, level at 5:00 and 10:00, kills and Rum per run, evolution rate and the
  strongest and weakest picks, weighted by the personality mix. Front end: the F1 Tuning Console's Balance section plus
  a Markdown report. Calibrated against scripted real runs and the owner's playtests.
- T2.x Model math on hand-computed fixtures, "the default config meets its targets", targets validation, simulator
  determinism (same seed, same result), personality parameters change outcomes in the expected direction (a higher dodge
  skill survives longer), and a calibration test against a scripted real run.

Automated check: the model report (`phase-02-model/model-report.md`) shows every target met; a scripted run confirms
the live alive count stays inside the target band at 1, 3, 6 and 9 minutes.
(Built 30 Sep: the simulator lives in the DevTools assembly, `V4/DevTools/Balance`, rather than a new assembly; it was
calibrated against the V4 autopilot's recorded runs, no new traces. 72 of 76 revised targets met, 66 of 76 of the
first proposals (nine bands were revised after the first run; the owner decides which set stands). The scripted run had
4 of 4 alive checkpoints in band (6:00 on the ceiling) but could not confirm the live XP pace (a standing ship). See
`phase-02-model/PLAN.md`.)
Owner playtest: the same three runs; do level-ups arrive at a good pace and does the late voyage feel powerful?
Done when: targets met, owner accepts the pacing.

#### M3 — Build balance

Goal: every weapon, logbook, item, evolution and Captain is worth taking, builds reach evolutions in a normal run, and
power grows faster than enemies late (the survivor-like power fantasy) without trivialising overtime.

- I3.1 Model extension: DPS per weapon at each level with area coverage and uptime (a coverage factor per behaviour),
  for single-target and crowd scenarios.
- G3.1 Weapon levels and evolutions: weapons keep 12 levels and logbooks 10–12 ranks (Q18). To make evolutions
  reachable (Q18b, confirmed): an evolution unlocks at weapon level 8 with its paired item and the evolved weapon keeps
  levelling to 12; rarer cards grant more than one level (Rare +2, Legendary +3) with a pity rule (M3c, 1 Oct: 2/2/3/3/4 levels and the evolution from weapon level 5, for the slower level-up rhythm). The Monte Carlo
  simulator shows a focused build evolving around minute 7–9.
- G3.2 Weapon parity band: each weapon's DPS per slot at levels 1, 4 and max within ±20% of its role's target curve
  (single-target, crowd, zone).
- G3.3 Logbooks and items against the model (a common roughly +5–8% build power, rare +10–15%, epic +15–25%); Luck and
  rarity weights reviewed.
- G3.4 Captains: hull, speed and actives balanced so every Captain's median model power is within ±10%.
- I3.2 Chest pricing and item flow retuned so a run sees 4–6 items by 10:00.
- T3.x Parity tests per weapon and role, evolution reachability (a scripted focused build reaches an evolution offer by
  minute 8 in the model), regression tests for register rows touched.

Automated check: model parity and Monte Carlo reports; a scripted run per Captain to 5:00 with no errors.
(Built 30 Sep: evolution from level 8 with Rare/Epic +2 and Legendary +3 levels and a pity rule; a 40% chance that a
chest gives a ready weapon's evolution item; weapons within ±20% of their role at levels 1, 4, 8 and 12; plain Logbooks
and items in their bands, volley and crit pieces flagged, synergy items not banded; Captains within ±10% on run length;
5 items by 10:00. Focused builds evolve around minute 6, not 7–9, with the confirmed rules. See
`phase-03-builds/PLAN.md`.)
Owner playtest: one run per Captain; name any weapon that feels useless or broken.

#### M3b — Feel fixes: island impact, sinking kegs, ability button

Added 1 Oct 2026 from the owner's notes (b, c, g), built while the owner was away (decisions in
`plan/v5/DECISIONS-WHILE-AWAY.md`).

- I3b.1 **Island impact** (g): a hit faster than 2 m/s into a shore pushes the ship back off it (60% of the impact speed
  plus 3 m/s, fading over 0.5 s, at most every 0.75 s), so the player never has to reverse off a shore, and costs 5 hull
  at most every 2 s, through the build's incoming-damage multiplier and with the usual hit feedback. Values in
  `RunConfig`; all 0 under the v3-parity preset (V3 slid along the shore).
- I3b.2 **Sinking kegs** (b): an unblown powder keg sinks (down and tilting over 1.5 s) and is gone 45 s after it was
  dropped (`WeaponConfig.mine.lifeSeconds`; 0 = never, V3).
- I3b.3 **Ability button** (c): on touch devices a round button bottom right, drawn with the touch stick's own art, fires
  the Captain's special ability through the same frame step as the keys (pause and cooldown rules unchanged), with a
  radial cooldown shade and a glow when ready. The actives are on in the data (checked).
- T3b.x EditMode: keg lifetime and parity, the sinking view; PlayMode: an island hit bounces and costs hull, holding
  into a shore costs hull at most every 2 s through the multiplier, V3 slides along, the button shows only on touch
  devices, a real touch reaches the Captain, and it hides and comes back.

Automated check: the targeted tests, both suites, a scripted run with screenshots (the button, an island hit).
Owner playtest: steer into an island (bounce, hull), drop kegs and wait 45 s, use the button in a phone build.

#### M3c — Level-up rhythm

Added 1 Oct 2026 from the owner's note a and sketch ("1 1 2 3 5"): fewer level-ups whose gaps grow. Split from the
pressure work after the plan review (P6), so each can be reverted alone.

- I3c.1 **XP table**: a per-level XP table in `ProgressionConfig` (empty = the formula, as before; the v3-parity preset
  empties it). Target gaps: the first level with the first kill (~12 s), then 10, 10, 20, 30, 50 s (the Fibonacci series
  × 10 s), then 60 s each: 4 level-ups in the first minute as before, then far fewer (about 13 by 10:00 instead of 24).
  A first try in 5 s units gave minute 1 more level-ups and let one pickup chain cards (`DECISIONS-WHILE-AWAY.md` D6).
- G3c.1 A DevTools tuner fits the table to those times in the simulator (unsinkable runs of the baseline personality,
  from the curve, damped rounds scored on the mean time error over the 10-minute voyage).
- G3c.2 M3's goals re-checked on the new rhythm and held: weapon cards grant 2/2/3/3/4 levels by rarity and the
  evolution card comes from weapon level 5 (focused builds evolve in most runs by minute 6; supersedes M3's Q18b card
  levels, a question for the owner), Roxie's hull 104 (Captains within ±10%), items by 10:00; M2's outcome targets (run
  length, boss wins) held for every personality with an interim flat enemy scale (HP and damage × 0.85, not tied to
  time) that M3d replaces; the pace targets follow the new gaps (±20%, owner 1 Oct).
- T3c.x EditMode: the gap series, the table maths, the data, parity, each level near its target time on another seed,
  every personality's outcome targets (two small known misses bounded), the level and pace targets.

Automated check: the simulator report; a scripted run with the level and the clock at each level-up.
Exit items carried to M3d: remove the interim enemy scale; clear the bounded known misses (Build Planner boss wins,
per-minute crowd and kills from minute 5, the Reef Crab fight pushing the 3:00 elite beat).
Owner playtest: do level-ups come less often without builds feeling starved?

#### M3d — Pressure from enemy count, peaks and valleys, distinct enemies

Added 1 Oct 2026 from the owner's notes e, h, k and sketch: a difficulty curve rising in peaks and valleys (small close
peaks early, a bigger one at 3–4 minutes, a long valley, then two big peaks to the 10:00 climax; logarithmic early,
steeper late) with ~60 s waves riding on it; pressure from enemy count, never from time-scaled HP or damage; an enemy
that looks the same always behaves the same.

- I3d.1 **Fixed enemy stats** (h): no HP or damage growth with time in the voyage (per-minute and ordinary-time curves
  flat); the director keeps only its count levers (spawn rate and floor) and elite chance (elites are drawn 2.15× larger
  with the Rich affix); its toughness, cap-HP and focus levers stay at 1. Later minutes are harder through more enemies
  and the stronger types that already enter them from today's roster (manta ray, gunner cog, boarding barge, kraken
  spawn, shield frigate, powder barge: their own fixed stats); new enemy art is listed in the M6 art guide (plan review
  P5). The Barnacle King, the Reef Crab and the elites get fixed HP that keeps their fight lengths at both the earliest
  and the latest likely summon (the boss rises after the beacon summon, P4). Overtime (after 10:00) keeps its endless
  ramp (`DECISIONS-WHILE-AWAY.md` D8).
- I3d.2 **Peak-valley flow** (e): the M1 waves become keyframes that the floor and spawn rate follow smoothly, authored
  to the sketch with ~60 s waves (about 40 s building, 20 s easing) and the hordes on the peaks. Floors stay shares of
  the enemy cap (80 today), so a later cap change keeps the shape (P1).
- I3d.3 **Distinct enemies** (k): every enemy id that attacks differently has its own look: its own model where one
  exists, else its own tint and size (a look = model + tint + size); the per-band encounter overrides that changed an
  anchor enemy's attack timing are removed; a test fails when two ids with different attacks share a look.
- G3d.1 **Retune with the simulator**: counts (floors, rates, hordes), the fixed HP of the boss, miniboss and elites.
  Outcome targets (run length, boss wins) stay; per-minute alive and kills targets follow the new shape; new targets:
  some hull lost in each minute from 1:00 to 4:00 (not on autopilot) and no minute after 5:00 that sinks an average
  build outright. Targets are derived from the design (the keyframes), never fitted to the simulator (review M3d).
- Every M3d change is off under the v3-parity preset (D16): the old curves, levers, waves and overrides (P7).
- M3d also closes M3c's exit items: the interim flat enemy scale goes (fixed per-type stats replace it) and the bounded
  known misses are cleared.
- T3d.x EditMode: flat curves, lever limits, the keyframed floor, the distinct-look rule, the targets, parity;
  PlayMode: the crowd at the sketch's peaks and valleys (clock jumped), enemy HP the same at 1:00 and 8:00.

Automated check: the simulator report against the targets; a scripted run with screenshots at 0:45, 3:30, 5:00, 7:30
and 9:30.
Owner playtest: three runs: is minute 1–3 busy, is 6–8 fair, does each enemy read?

#### M3e — Performance: pooling, garbage, batching, culling, then the enemy cap

Added 1 Oct 2026 from the owner's notes d, f, i, j and the cap answer (h). Audit: `phase-03e-performance/AUDIT.md`.

- I3e.1 **Pooling** (d): everything spawned per gameplay event already comes from a pool; the gaps close: capped pools
  where a spike would instantiate, prewarmed VFX-profile effects and the flame stream, component stripping moved into
  prewarm, marker glyphs reused; a test lists the per-event spawn sites.
- I3e.2 **Garbage** (d): zero bytes allocated per frame in the run's hot paths in a steady state (enemy wake effects,
  the spatial hash, config lookups, closures, event strings, HUD strings), measured with the profiler's GC allocation
  counter in a scripted run; the O(n²) enemy overlap and projectile checks use the spatial hash.
- I3e.3 **Batching** (f, i): the SRP Batcher is already on; the generated islands are combined once the stage is
  built (static batching at run start); per-instance material copies that break batching are replaced; GPU instancing
  only where a shader is not SRP-Batcher compatible (Unity's order: static batching, SRP Batcher, instancing).
- I3e.4 **Culling** (j): baked occlusion culling does not apply (a stage generated at run start under a steep top-down
  camera over open sea has nothing to bake and few occluders; WebGL has no GPU occlusion): recorded as not applicable
  after measuring; the camera's far clip (1000 m for a ~32 m view) and per-layer cull distances are used instead.
- I3e.5 Quick wins from the audit: the second realtime directional light, lights inside explosion prefabs, the mobile
  opaque texture, shadows on pickups and effects, the damage-highlight shader.
- I3e.6 **Enemy cap** (owner's answer to h): raised from 80 only if the measured frame time allows after the work
  above, and only up to what a device near the reference phone can hold; the Pixel 9a is not a budget check (P2); the
  final phone budget stays with M7.
- T3e.x Draw calls, batches, frame time and GC allocations before and after in a scripted Editor run at 1, 5 and 9
  minutes.

Automated check: the before/after table; both suites.
Owner playtest: a run in the Editor and on the Pixel 9a; does anything look different?

#### M4 — New weapons, batch 1, and the missing evolutions

Split on 1 Oct 2026 while the owner was away (D30, `phase-04-weapons/PLAN.md`): **M4a** (built: the mechanics the new
weapons need, opt-in with today's weapons proved unchanged, and the flamethrower VFX fix R-89) and **M4 proper** (the
weapons and evolutions below, after the owner's answers to questions 17-26). The flame-stream prewarm left by M3e (D27)
goes with M4 proper.

Goal: the first new weapons exist with evolutions, balanced by the model and the simulator and unlockable with RUM, and
every existing weapon has an evolution.

- I4.1 Weapons (Q19): **Anchor Flail** (an anchor on a chain circles the ship, smashes and knocks back), **Chainshot**
  (a wide spinning shot that cuts through a line and slows), **Siren Bell** (a damaging pulse around the ship with a 20%
  slow; no charm), all from `Rogue_Pirates_Combat_Content_v3_1.md` §4.7 (design numbers only), and ~~Ramming Prow~~
  (boosting turned the ship into a battering ram) **replaced by the Squid Ink Pots** (D21, 7 Oct: the Prow needed the
  boost button, against the auto-attack rule; the owner asked for "a weapon that slows down the enemies, especially those
  in the back"): it auto-fires every 2.5 s, lobs a pot of ink (0.9 s flight, landing where the target will be) onto the
  back of the pack (the enemy in its 22 m range scoring highest on distance + 1 m per neighbour within the slick, so not a
  lone straggler), and leaves a 3.5 m ink slick for 3 s that slows everything in it by 35% (each 0.5 s tick renews a 1 s
  slow) and deals modest damage (17.68 x level factor on landing, 0.15 of it each tick: 14.78 level-1 single-target DPS,
  the D20 rule). Bosses and minibosses take half the slow (at most 20%). Cards: Duration (slick and slow), Slow (+4% a
  card, at most +20%), Size, Cooldown; rank milestones 4 (a second pot on the next pack back), 8 (25% wider slicks),
  12 (+10% slow); evolution Black Tide with the Iron Figurehead (the Prow's item, reused): one more pot, x1.3 damage,
  x0.85 cooldown, x1.3 size, +10% slow. Placeholder look: violet rings on the water. Chosen over a tide wave pushed out
  from the ship because a wave hits the front of the pack first, while a lob reaches the rear directly and lets the
  front still come to the guns. Ghost Cannons is dropped (it reads as an evolution of the cannons).
- I4.2 New behaviours as needed (`Trail`, `Aura`, `Summon` exist in the V4 code but are unused). D21: the boost-only
  aura (`requiresBoost`) went with the Prow; the Ink Pots are a predicted-area shell with `rearTargeting` and a slowing,
  pull-free whirlpool (`predictedArea.slowFraction`); rank milestones are data (`WeaponConfig.milestones`, Q20); a new
  upgrade axis `Slow`.
- I4.3 Rank milestones for every new weapon (Q20, 7 Oct, `// V5 M8z (7 Oct, Q20)`), data in `WeaponConfig.milestones`,
  shown on the upgrade card as "Rank N: ..." (CardStatLines). From the design (§4.7): **Anchor Flail** 4 a wider swing
  (+0.5 m), 8 another anchor (three), 12 +25°/s; **Chainshot** 4 +1 pierce, 8 a 0.5 m wider shot, 12 the slow lasts
  1 s longer; **Siren Bell** 4 the slow lasts 0.5 s longer, 8 +1 m radius, 12 a second pulse 0.35 s later. Chosen (no
  design numbers; "more of its thing"): **Parrot Squadron** 4 and 12 one more parrot, 8 parrots stay 25% longer;
  **Monkey Gunners** 4 turrets stay 25% longer, 8 a second turret, 12 a third (cap 3 -> 4 so the Monkey Battery still
  adds one); **Maelstrom Charge** 4 20% wider whirlpools, 8 whirlpools last 1 s longer, 12 a second charge on the next
  densest pack; **Lighthouse Lantern** 4 15% longer beams, 8 a 25% faster sweep, 12 one more beam; **Cursed Doubloons**
  4 and 8 one more jump each, 12 a second coin 0.35 s later. The milestone gained generic fields for these
  (`extraPierce`, `spinMultiplier`, `extraSlowSeconds`, `echoes` + `echoDelaySeconds`: a firing repeats, queued in
  `WeaponCombat.Echoes`); a summon's life and a cluster-shell's whirlpool now follow the weapon's Duration (milestones
  only: no summon or Maelstrom has the Duration card); the Mortar-style cluster path lobs one more shell per point of
  the weapon's own quantity at the next densest pack (the Quantity stat is still not read there, as before); an
  evolution's extra pierce and slow now add to the milestones' (Black Tide used to replace the Ink Pots' rank 12 slow).
  Level 1 is unchanged (D20); the simulator export carries the milestones and `sim.mjs` applies them.
- G4.1 Evolutions for every weapon without one: Harpoon Gun, Long Nine, Mortar, Swivel Guns and the four new ones (each
  unlocks at level 8 with its paired item, per Q18b).
- G4.2 Unlock prices in `UnlockConfig` (RUM), cards, placeholder icons and VFX.
- T4.x Behaviour tests per weapon, evolution tests, model and simulator parity band, no content-name comparisons.

Automated check: Module Lab script fires each new weapon and evolution with screenshots.
Owner playtest: Module Lab — fire each new weapon and evolution; one run with a new weapon.

#### M4b — New weapons, batch 2

Goal: five more weapons that bring new play patterns (summons, pull, beam, bounce), each with an evolution (Q19b).

- I4b.1 **Parrot Squadron** (parrots dive-bomb the nearest enemies; more parrots per level), **Monkey Gunners** (drops a
  floating barrel turret with a monkey that fires for a few seconds), **Maelstrom Charge** (a lobbed charge opens a
  whirlpool that drags enemies in, then implodes), **Lighthouse Lantern** (a light beam sweeps around the ship, burning
  along its length), **Cursed Doubloons** (coins ricochet between enemies; kills drop extra gold, within the capped gold
  curve). New designs: numbers written at phase start and checked by the model and the simulator.
- G4b.1 Evolutions, unlock prices, cards, placeholder icons and VFX.
- T4b.x Behaviour tests (summon lifetimes and caps, pull strength, beam sweep, ricochet chains), evolution tests, parity
  band, pooled views for summons and beams.

Automated check: Module Lab script fires each weapon and evolution with screenshots.
Owner playtest: Module Lab, then one run each with a summon and a beam weapon.

#### M5 — New logbooks, items and powerups

Goal: the rest of the designed logbooks, items and powerups exist, balanced by the model and unlockable with RUM.

- I5.1 Logbooks (§5.2 "unlockable six"): Ironclad, Fortune, Tidecaller, Admiral, Plunderer, Chaos.
- I5.2 Items (§7.1–7.5): the 20 missing, including the first two Legendaries (Leviathan Heart, Captain's Last Rum).
- I5.3 Powerups: Battle Signal, Tailwind, Salvage Chime and Full Sail join the pool (design mix 35/35/20/10).
- G5.1 Unlock prices (RUM); which items are in the base pool and which are unlockable.
- T5.x Effect tests per logbook and item, model values, pool and unlock tests.

Automated check: Module Lab script grants each new logbook and item and logs the stat change.
Owner playtest: Module Lab grants plus one run with a new item build.

#### M6 — Art guide (document)

Goal: a hand-off document for the art team: what exists, what is placeholder and what is missing for every weapon,
weapon effect, evolution, item, logbook, POI, enemy, boss, decoration, sail, defense, UI icon and brand slot.

- D6.1 A Claude Doc "Rogue Pirates — Art Guide": style rules (stylized low-poly, saturated, readable at the ~42° top-down
  camera, telegraph language, VFX budgets), then one table per category: id, name, what it does in play, look and
  silhouette brief, states, colours, size, triangle and particle budgets, what exists (file path), status (exists /
  placeholder / needed), priority.
- D6.2 Island decoration catalogue (request 22) with briefs (section 7).
- D6.3 Sail and flag canvas spec (texture size, the `Sail.001` UV layout, safe areas) for M25.
- D6.4 Brand asset slots (section 7) with sizes and placement rules.

Automated check: none (document); the reviewer checks every content id in the V5 configs appears in the guide.
Owner check: read and hand over; comments on the doc are answered in place.

#### M7 — Mobile UI and input

Goal: the game plays well on phones and tablets in mobile browsers and on desktop browsers, with one UI.

- I7.1 Input: the virtual joystick upgraded (floating, left or right, deadzone); touch buttons for boost, the Captain
  active (built in M3b), interact (E/USE) and pause; keyboard, mouse and gamepad unchanged; `MoveInput` becomes an input router.
- I7.2 Safe area and scaling: a `SafeAreaFitter` on every canvas; the HUD reflows for 16:9 to 20:9 and 4:3 tablets; a
  UI scale setting; text minimums.
- I7.3 Level-up cards, chest popup, results and menu buttons as 72 px targets; long-press instead of hover.
- I7.4 Settings: controls layout, UI scale, quality tier, reduced effects, audio.
- I7.5 WebGL mobile profile (budget in `research/DEVICE_TARGETS.md`): a maximum memory of 256 MB for mobile browsers (budget about 200 MB;
  32-bit Chrome cannot grow past ~256 MB; 512 MB only where the browser is 64-bit, such as iOS Safari and desktop), compressed textures
  ≤ 1K, 20–30 MB first download, 100–150 draw calls, render scale 0.6–0.75 on phones, 30 fps on the reference phone and
  60 fps where the device allows, automatic quality tier from measured frame time, particle caps, touch detection; desktop
  and mobile WebGL builds; local HTTPS serving for phone testing; the enemy cap, frame time (95% of frames under 34 ms over 15 minutes) and memory measured on the reference phone and the floor phone (Q21, Q31; moved here from M1, which had only a stand-in). M1 found the current build needs a 344 MB WebAssembly heap and a 160 MB data download, so this phase must bring both under the 256 MB and 20–30 MB budgets.
- T7.x Layout tests at 5 resolutions (every touch target ≥ 48 px, nothing outside the safe area), input router tests;
  PlayMode touch simulation; the WebGL build succeeds.

Automated check: WebGL build plus a scripted touch-simulated run with screenshots at three aspect ratios; on the
reference phone, 95% of frames under 34 ms over 15 minutes and memory under 256 MB; the floor phone at 30 fps on Low.
Owner playtest: the WebGL build on a phone (local network) and in a desktop browser.

#### M8 — Main menu, shop and inventory (web2, local)

Goal: a hub the player returns to: Play (modes), Captains, Shop, Collection, Settings, Profile. The shop sells weapon,
logbook, item and Captain unlocks (later decorations and sails) for RUM, on a meta-service interface so the server
version (M10) and token prices (M11) drop in.

- I8.1 `RoguePirates.V5.Meta`: `IAccountService`, `IEconomyService` (RUM), `IInventoryService`, `IStoreService`, with a
  local implementation over the existing save (no data loss; the V4 M16 save guard stays).
- I8.2 `StoreCatalogConfig`: every unlockable with price, category, rarity, requirements, "new" and sale flags, and a
  web2/web3 origin flag for D4.
- I8.3 Menu hub (mobile-first): Play → mode select (Voyage now; Raids, Race, Arena, Hunt shown as "coming"), the
  Captain carousel, Shop tabs (Weapons, Logbooks, Items, Captains; later Decorations and Sails), Collection, Settings
  (M7), Profile (stats, run records).
- I8.4 Only unlocked content enters the run's offer and chest pools (the V4 rule for weapons, extended to logbooks and
  items); starter sets stay free.
- T8.x Purchase tests (price, insufficient RUM, idempotent buy, never drops ownership), D4 rule tests, menu layout
  tests; PlayMode: buy an item, start a run, see it offered.

Automated check: scripted menu session (buy, equip, start a run) with screenshots on desktop and phone layouts.
Owner playtest: buy something, play, see it in a run.

#### M8z — Playtest integration (rounds 1-9)

Goal: bring the playtest work (branch `playtest`) under the gate before M9: merged, architecture rules restored, the
shipped switches tested, a scenario lab for fast tests, the test suite describing the game again, the economy rules
settled and one simulator calibrated on the owner's play.

- I8z.1 Tag `playtest-r9`, merge `playtest` into `v5-live-game` (`--no-ff`, no conflicts possible); point the API deploy
  workflow at `v5-live-game` with an approval step.
- I8z.2 Architecture fixes: enemy colours and an elite flag in the enemy config (no content ids in code), card stat lines
  through `IRunViews`, the weapon switch-off list in config, `PvpSession`/`OnlineService` owned by the composition root.
- I8z.3 The `DemoMode` switches the owner plays (Beacon or timed boss, chests-only, whirlpool) as a "playtest" config
  preset, so tests cover what ships.
  (7 Oct, as built: a `[UseShippedPlaytestSettings]` test attribute plus scenarios with `playtest: Shipped`; the switches stay
  in `DemoJuiceSettings` behind `DemoMode`, moving them into a V4 config preset is left for later.)
- I8z.4 Scenario Lab: a `ScenarioConfig` asset, a `V5ScenarioLab` scene and a `ScenarioRunner` (seed, Captain, loadout and
  level, clock time, chosen enemies at a set distance, forced points of interest, switches, a PvP bot over a fake relay);
  the same scenarios from the F1 console and in 2-10 s PlayMode tests.
- I8z.5 Test triage of the 73 failing tests: fix the real ones, rewrite value pins as rules reading a balance-targets
  asset, retire the rest; slow full-loop tests marked `Slow` (gate only).
- I8z.6 Shop and economy data after the owner's answers (starters vs unlocks, Ember's lock, the overtime Rum cap, D4).
- I8z.7 One simulator (owner, 7 Oct): the JavaScript Monte Carlo model (`plan/playtest/montecarlo`) is kept and the in-engine
  SimRun retired; the model reads the game's real configs (an Editor export of the resolved config values, checked against
  `export_data.py`), and is recalibrated on the recorded owner sessions (`plan/playtest/sessions`, `compare_session.mjs`).
- T8z.x Fast scenario tests per area: combat (starter one-shots a dinghy, equal level-1 DPS, bars only on elites,
  minibosses and the boss), director (beats at their times, kill band), stage (Beacon summon, POIs, overtime and storm
  from config), economy (Rum caps, no duplicate store rows), PvP (draft to end over a fake relay, reconnect after the end).

Automated check: both suites green except tests retired with a reason; a desktop WebGL build.
Owner playtest: a recorded voyage compared with the simulator; a PvP match against the bot.

### Stage B — Accounts and chain

Stack research: `plan/v5/research/STACK_RESEARCH.md`.

#### M9 — Run checks and the netcode test (go/no-go)

Goal: before any account holds value, decide how runs are checked and prove that browser players can reach an
authoritative match server well enough for the Arena and the Hunt.

- I9.1 Run log: seed, config hash, both build ids, per-minute summaries (spawns, kills, gold, XP, damage taken and dealt,
  level, pickups) and the inputs (for disputes and future replay), compact.
- I9.2 Seeding: every gameplay draw goes through the run's seeded streams (the remaining client-side seed choices are
  `HomeSeed.cs:24` and `WorldPort.cs:57`; the draw in `SalvageChestWaterFloat.cs:35` is visual only); the run seed can be
  injected from outside (the server, from M10).
- I9.3 Plausibility rules (D6, Q29): bounds taken from the M2 balance model and simulator, not from the seed alone
  (the minimum-alive floor and the adaptive director spawn more as a player kills more): the most kills per minute any
  build can reach at the enemy cap, the most gold, XP and level per minute, consistency between them (level follows XP,
  XP follows kills, gold follows kills and chests), and the reported build checked against the level and the offers the
  seed allows; written as shared config so the client and the server compute the same limits.
- I9.4 Netcode test with the headless build (Q2: Unity Netcode for GameObjects with Unity Transport WebSockets;
  `research/HUNT_NETCODE.md`): a headless server on Railway and two, then four, WebGL clients (one on the reference
  phone) over secure WebSockets; ~200 enemies simulated, each client receiving only nearby enemies as batched, quantized
  snapshots. Also a single-player server voyage (the mode raids, races and ranked runs use) with a prediction-ready
  ship: movement predicted on the client and reconciled with the server. Go limits: ≤ 20 KB/s per client; server tick
  ≤ 16 ms at 4 players and 200 enemies; the reference phone still at a steady 30 fps while connected; the ship feels
  responsive at 150 ms round trip (a simulated mobile network); how many single-player sessions one headless process
  hosts and the memory per session (for the Railway cost estimate); reconnect after a Railway redeploy.
- D9.1 Report and go/no-go memo: Netcode for GameObjects against the limits above (Q2). Photon Quantum (the only option with exact replays) is not
  recommended (full rewrite; every phone simulates every enemy), so runs use plausibility checks and server-played
  sessions (D6).
- T9.x Plausibility rules accept 20 scripted real runs and reject edited logs (inflated kills, gold, impossible
  levels) and a forged log that is internally consistent at the 99th percentile of the simulator's strongest
  personality; a source scan shows every gameplay draw uses the seeded streams.

Automated check: the plausibility report and the netcode measurements in `phase-09-checks/REPORT.md`.
Owner check: read the report and confirm the netcode choice. No playtest needed (no gameplay change beyond seeding).

#### M10 — Game server foundation and ledger

Goal: a local game server that owns RUM, inventory and receipts, settles runs only after verification, and is ready for
accounts.

- I10.1 Server on .NET 10 LTS (the three server projects target net8.0 today): `server/RoguePirates.Api` on Postgres
  (Docker compose locally; Railway when hosted); migrations from `001_initial.sql`; modules Ledger (RUM with pending, settled and legacy balances),
  Inventory, Store, Receipts; a command envelope (idempotency key, expected version, typed results); rate limits.
- I10.2 Server-issued seeds: every run that earns value starts with a seed and run id from the server (D6).
- I10.3 Plausibility checker: the M9 rules as a queued background worker, replacing the placeholder
  `server/RoguePirates.Verifier`; its results settle pending RUM (usually within seconds).
- T10.x `dotnet test` (idempotency, double settlement, forged totals rejected, pending never spendable, seed mismatch
  rejected, implausible runs rejected, concurrent purchases, worker retries).

Automated check: a scripted run with a server seed is recorded, uploaded, checked by the worker and settled.
Owner check: read the phase report (no player-facing change yet).

#### M10b — Accounts, email sign-up and save migration

Goal: guest and email accounts on the server; the client moves from the local save to the server without losing
unlocks, and nothing unverified becomes value that can reach other players.

- I10b.1 Auth (Q12, confirmed): a guest account on first launch; **Sign in with Google** (Google Identity Services;
  our server verifies Google's ID token) as the main button, and an **email one-time code** (sent through Resend's free
  tier) for everyone else; no passwords. A local mail catcher (Mailpit) in development. Linking a guest to an account
  keeps progress.
- I10b.2 Client: `ServerMetaServices` implement the M8 interfaces over HTTPS; nothing that moves value is queued offline.
- I10b.3 One-time migration of the local save: unlocks, receipts, Captain, home seed and run records as they are; local
  RUM comes in as **legacy RUM**, capped (config, default 300), spendable in the store only and never transferred,
  wagered or exposed to raids (the local save can be edited, so its balance is a client-submitted total). The local save
  stays as a backup.
- I10b.4 Run settlement from the client: upload the run log and run receipt; RUM stays pending until the worker
  checks it (D6).
- T10b.x Migration once, the legacy-RUM cap and restrictions, linking, token verification, offline behaviour; Unity
  EditMode with a fake server; PlayMode against the local server.

Automated check: a scripted client session (sign up, migrate, play a short run, see pending then settled RUM, buy).
Owner playtest: sign up with an email (local mail catcher), play, see RUM settle, sign in on another browser.

#### M11 — Custodial devnet wallets, fee payer and tokens

Goal: every email account gets a devnet wallet created and held for it (request 16); the game pays the fees; SOL, SKR
and ORE balances show in the menu; the store can price items in tokens for web3 accounts.

- I11.1 Wallet at sign-up, held by our server (D7, Q3): a keypair per player, the private key encrypted (AES-256-GCM)
  under a master key in a Railway secret, ciphertext in Postgres, decrypted in memory only to sign the game's own
  instruction set; no key export for now (Q32). Keys never reach the client, logs or the repo.
- I11.2 Fee payer: the backend (or a self-hosted Kora relayer) pays devnet fees and rent through a devnet RPC (Helius
  free tier; the public endpoint rate-limits a single server); an allow-list of program ids
  and instructions; per-account and global rate limits; a balance monitor with scripted devnet top-ups.
- I11.3 Tokens: devnet SOL; game-owned stand-in mints for SKR (6 decimals) and ORE (11 decimals) (D5); a tester faucet
  with daily limits; a token registry config (mint, decimals, symbol, icon).
- I11.4 Web2/web3 mode per account; the menu shows balances, the wallet address (copy), a devnet banner, explorer links.
- I11.5 Fixed store token prices in config (Q5) for cosmetics and RUM-equivalent unlocks (Q25); a purchase is an SPL transfer to the treasury plus a server
  receipt; token-bought items carry the web3 origin flag (D4).
- T11.x Server tests against `solana-test-validator`: wallet creation, fee-payer limits, foreign instructions rejected,
  purchase idempotency, the D5 mainnet-id scan, D4 origin flags.

Automated check: a scripted session creates a wallet, drips faucet tokens, buys with devnet SKR, and shows the receipt.
Owner playtest: turn on web3, see the wallet, take test tokens from the faucet, buy a store item priced in devnet SKR.

#### M12 — MagicBlock: VRF seeds and onchain records

Goal: match seeds come from verifiable randomness and value-bearing results are recorded onchain, and the player never
waits for it.

- I12.1 Anchor 1.0 program `rogue_pirates` (new `onchain/` workspace; the `Solana-Integration` branch's `pirate_coop` is
  reference only; no BOLT, which is deprecated): player record PDA (runs, best kills, raids won and lost, reputation),
  match record PDA, config and authority.
- I12.2 What gets recorded: raids, races, wagered matches, bounty runs, and the weekly leaderboard top N; ordinary
  voyages update only the player record (batched). Match accounts close after settlement to reclaim rent.
- I12.3 MagicBlock VRF (`ephemeral-vrf-sdk`): randomness for race layouts, raid permutations, Hunt maps, bounty islands
  and wagered matches; the callback checks the VRF identity; a flagged server seed if VRF times out (never for wagered
  matches).
- I12.4 Ephemeral Rollups (`ephemeral-rollups-sdk`): a match record is delegated while the match runs, then committed
  and undelegated; escrow stays on the base layer. The .NET server talks to the devnet router as a normal Solana RPC.
- I12.5 Seamless: the server and fee payer do all of it; the client shows a small "recorded onchain" explorer link.
- T12.x `anchor test` on localnet (records, authority, account closing), a tagged devnet test for VRF and the rollup
  (run once per phase), server tests for timeouts and fallbacks.

Automated check: a scripted session records a race seed via VRF and a match record through the rollup on devnet, with
explorer links saved as evidence.
Owner playtest: finish a recorded run and open its onchain record; open a race seed's VRF proof.

#### M13 — Connect wallet and migrate

Goal: connect Phantom, Solflare or Backpack and move tokens, records and inventory from the custodial wallet to it,
paying the fees (request 18). NFTs join the migration in M26, when they exist.

- I13.1 Connect: the Solana Unity SDK's WebGL wallet adapter for desktop extension wallets (pin a commit; last tag
  v1.2.9); in mobile browsers the wallet's in-app browser or Phantom/Solflare deeplinks (extensions do not inject
  there); Android Mobile Wallet Adapter in Chrome only. A sign-in-with-Solana message links the wallet to the account.
- I13.2 Migration: the server builds token transfers and the record-authority change with the external wallet as fee
  payer; the player signs; the server confirms, flips the account to self-custody and empties the custodial wallet;
  receipts per step; resumable after an interruption. No key export (Q32).
- I13.3 Linked-wallet play: actions that need a signature ask the wallet; the rest stays seamless (session keys if
  pop-ups get in the way).
- T13.x Partial migration, replay and double submission on the server; an automated devnet end-to-end migration with a
  test keypair.

Automated check: the scripted devnet migration with a test keypair; balances and record authority verified on devnet.
Owner playtest: connect Phantom (devnet) in a desktop browser, migrate, see the tokens and the record in the wallet.

#### M13b — Match server and server sessions

Goal: an authoritative match server on Railway that browser players connect to, starting with single-player server
sessions: because results are not replay-verified (Q29), every session that moves value between players is played on
it (raids, races, ranked voyages for the leaderboard, bounties).

- I13b.1 Unity Netcode for GameObjects (Q2) with Unity Transport WebSockets: server authority, client prediction for
  your ship, interpolation for enemies; our own interest management (each client receives only nearby enemies, never
  more than the device cap) and our own lag compensation for player-vs-player hits (NGO has neither built in); enemies
  sent as batched, quantized custom messages rather than one network object each.
- I13b.2 Prediction-ready ship controller: the ship's movement (today Rigidbody physics) stepped deterministically each
  tick so the client can predict and reconcile with the server, keeping the current feel (speed curves, turn rates,
  knockback) within measured tolerances.
- I13b.3 Server sessions: a single-player session mode (the player's browser is a client of a server-run voyage) for
  raids (M15), races (M17), ranked voyages (M18) and bounties (M27); AI raids (M16) run headless on the server faster
  than real time, with no client.
- I13b.4 Hosting on Railway (Q17): a match-server service in Railway's Serverless mode (sleeps when no session runs, so
  idle time costs nothing), several sessions per process (the number measured in M9), reconnect on redeploys; the
  measured cost per session checked against the estimate in `ANSWERS.md` before the phase closes; a Railway usage alert
  and spend limit set.
- T13b.x Server tick tests, prediction and reconciliation, disconnect and reconnect, a server-run voyage's outcome inside
  the plausibility bounds, sessions per process under load.

Automated check: a scripted single-player server voyage completes on a local server and on Railway; bandwidth, CPU and
memory per session logged.
Owner check: play one server-run voyage from your browser and your phone; does the ship feel the same?

### Stage C — The island

#### M14 — Home island: base building and decorations

Goal: the player's island is a persistent base: defenses on sockets that move with their island sections, a
defender-authored raid schedule (which enemies, how many and when — never where, because the layout changes), a
treasury and decorations.

- I14.1 Island model: the home-seed island split into sections (the `StageGenerator` grid); defense and decoration
  sockets belong to sections, so a raid's section permutation keeps them attached (non-negotiable rule).
- I14.2 Defenses (config): cannon tower, mortar nest, net launcher, mine field, harpoon post, lighthouse (reveals the
  raider); levels, RUM costs, a limit per island rank; they reuse the weapon behaviours.
- I14.3 **Raid schedule editor**: a 0:00–5:00 raid timeline where the owner places waves (enemy type, count, time; entry
  side random) within a threat budget by island rank. Stored in the base snapshot as a schedule DTO and turned into an
  in-memory timeline overlay at raid start (the designer-owned `WaveTimelineConfig` asset is never written); the
  schedule's hash enters raid records. Validation: budget, max alive, the first 20 s quiet.
- I14.4 Treasury (Q23, Q23b confirmed): a vault holds 80% of the island's RUM and is never raidable;
  the treasury holds 20%, capped by island rank; a raid takes 20% of the treasury; an 8-hour shield after any raid;
  a daily loss cap of 10% of total RUM; 72 hours of new-player protection.
- I14.5 Decorations (request 22): placed on decoration sockets and free slots, rotation and scale steps, cosmetic only
  (D14); a Decorations shop tab (RUM; tokens for web3 accounts, marked web3-origin per D4).
- I14.6 Base editor (mobile-first): section view, drag to socket, and a test raid of your own base before a changed
  base is published.
- I14.7 Server: versioned base snapshots (sections, sockets, defenses, schedule, decorations), validated server-side.
- D14.1 Art guide updated with the defenses, decoration sets and base-editor UI (request 4).
- T14.x Sockets survive permutations, budget validation, schedule DTO → overlay, snapshot round trip, layout tests.

Automated check: a scripted session builds a base, sets a schedule, publishes and test-raids it with screenshots.
Owner playtest: build a base, set a schedule, decorate, test-raid it.

#### M15 — Island raids

Goal: raid another player's published base: its defenses and schedule run against you on a permuted layout; sink the
Treasury Beacon within the time limit to take a capped share.

- I15.1 Raid mode, played as a server session (M13b): a run built from the defender's snapshot (sections permuted by a VRF seed, defenses as enemy
  structures, the schedule overlay as the timeline, decorations as props); the raider's Captain and loadout.
- I15.2 Matchmaking: three targets in a power band; revenge targets from the Captain's Log.
- I15.3 Settlement: the server's result (the raid ran on the match server); capped loot, entry fee and shield (GDD questions 2–4); one theft receipt per
  raid; failed raids raise the island's reputation; linked-account hooks (D15, off for now); onchain record (M12).
- I15.4 Captain's Log: who raided, what held, what was taken, replay, revenge.
- T15.x Snapshot → run, loot cap and shield races (concurrent raids), receipt uniqueness, a raid's result comes only
  from the server session.

Automated check: a scripted raid on a seeded test base, run as a server session and settled, with the Captain's Log entry.
Owner playtest: raid a seeded test base; get raided by a test account; take revenge.

#### M16 — AI captains

Goal: an AI captain defends your island and raids for you, and you tune how it plays (request 14).

- I16.1 `AiCaptainProfile` config, using the same parameters as the simulator's player personalities
  (`PLAYER_PERSONALITIES.md`): dodge skill, contact avoidance, engage distance, movement style, pickup greed, chest
  policy, landmark use, boss timing, card policy, reroll use, active use, plus raid-only fields (target priority:
  nearest, weakest, structures first; retreat threshold).
- I16.2 `AiCaptainBrain` in the Runtime assembly: the behaviour of the DevTools autopilots (`PlayerPilot`,
  `HumanPilot`, which never ship) rewritten as a runtime module; a test proves Runtime never references DevTools.
- I16.3 Defend: your AI captain's ship patrols and fights during raids on your island (a defense slot), simulated as an
  enemy unit driven by the AI brain, so it needs no second player ship.
- I16.4 Raid for you: up to **6 AI raids a day at full loot** (Q9), run headless on the server against real
  snapshots; defenders stay protected by the treasury shield and daily loss cap (M14).
- I16.5 UI: sliders and presets (Cautious, Balanced, Reckless), a test arena, raid reports.
- T16.x Parameters change behaviour measurably (engage distance, retreats), daily caps, headless raids repeatable.

Automated check: the test arena runs three presets and logs engage distance, kills and retreats per preset.
Owner playtest: tune a captain, watch it defend your island and raid for you.

#### M17 — Async Island Race

Goal: a speed-run on a published base with one attempt per player; the fastest verified conquest wins (request 10).

- I17.1 Races (server): a published base, a VRF seed, a window (for example 24 h), one attempt per account (a server
  lease).
- I17.2 Race mode, played as a server session (M13b): fixed seed and loadout rules; the timer runs from first input to
  Beacon sunk; no pause.
- I17.3 The server's time is the result; ties by milliseconds, then kills.
- I17.4 Optional ghost of the leader.
- T17.x One-attempt enforcement (reconnect, crash, second device), timing, verification, ordering.

Automated check: two scripted entrants finish a race; a third attempt by the same account is refused.
Owner playtest: enter a race twice (the second is refused) and check the board.

### Stage D — Social and retention

#### M18 — Weekly leaderboard (kills in a single run)

Goal: a fair weekly board of each player's best single-run kills (request 13).

- I18.1 Server board of each account's best single-voyage kills per UTC week (Monday reset), across every voyage biome
  as they ship (Q16, confirmed), on server-issued seeds, ranked only under the published rules (adaptive director version, no dev tools, no forced Pressure;
  the V4 `RunScoreRecord`); a run counts for prizes only when played as a ranked server session (M13b).
- I18.2 Client: global, friends and around-me views; the reset timer; last week's podium.
- I18.3 Capped RUM rewards and titles; the weekly top 10 recorded onchain.
- T18.x Week boundaries, ties, failed verification removes an entry, reward caps.

Automated check: scripted entries across a week boundary; the board and rewards match expectations.
Owner playtest: play two runs and see the better one ranked; check the reset timer.

#### M19 — Notifications, invites and share cards

Goal: players hear about what matters to them and bring friends in with links and cards (requests 12 and 23).

- I19.1 Notifications: an in-game inbox; web push (service worker + FCM on desktop and Android; iOS only as an installed
  web app; needs a Firebase project, Q30); opt-in email digests; events: raided, shield ending, race closing, rank
  changes, invites, sales.
- I19.2 Friends and invites: the invite-link framework (`/join/<code>`, OG image) with raid and race links (Arena and
  Hunt links arrive with those modes in M22 and M23); a Solana
  Actions endpoint on the same URL for wallet users (optional; Blinks need Dialect registration to unfurl on X).
- I19.3 Share cards: server-rendered PNG (SkiaSharp) for run results, raid wins, ranks and sails, with a link; Web
  Share where the browser allows, else copy link and save image; referral rewards capped for both players; linked-account hook, off (D15).
- T19.x Link parsing and expiry, referral caps, card rendering, push subscription (local).

Automated check: a scripted invite round trip (create link, open it in a second client, land in the raid target) and
rendered cards saved as evidence.
Owner playtest: share a run card, open an invite link on your phone, receive a push when raided.

#### M20 — Retention loops

Goal: reasons to come back every day, every week and every season, paying capped RUM or cosmetics (request 24).

- I20.1 Daily: a 7-day login calendar with a restore token; 3 daily charts (quests) with capped RUM.
- I20.2 Weekly: Admiralty orders, the leaderboard, a weekly race, raid-defense streaks.
- I20.3 Long: Captain mastery (cosmetic rewards), Collection completion, the Captain's Pass season track (free track now;
  the premium price is GDD question 5).
- I20.4 Social pull: revenge, shield reminders, friends' races; brand bounty events are wired in M27.
- I20.5 A returning-player bonus and capped catch-up.
- T20.x Reset boundaries, caps, streak restore, quest progress from events.

Automated check: a scripted clock-skip session across days and a week shows streaks, quests and resets.
Owner playtest: play two "days" (clock skip in dev builds) and claim the rewards.

### Stage E — Real-time modes

The match server and single-player server sessions moved up to M13b (raids, races and ranked runs need them); this
stage adds several ships per session.

#### M21 — Multi-ship runtime and lobby

Goal: two to four player ships in one server session, with a lobby, matchmaking and invite codes, ready for the Arena
and the Hunt.

- I21.1 Multi-ship runtime: the V4 run assumes one player ship (`RunManager.Instance`); ships, stats, pickups, damage,
  XP and level-ups become per-player.
- I21.2 Lobby and matchmaking: quick match, private match by invite code (the M19 link framework), ready check.
- D21.1 No-go path: if M9's four-client test failed, the Arena ships with light or no PvE and the Hunt is reduced
  (fewer enemies, a smaller sea); the owner decides from the M9 memo.
- T21.x Per-player state isolation (one ship's level-up never affects another), lobby flows, disconnect of one of four
  players.

Automated check: four headless clients join one lobby by code and play 5 minutes; per-player results recorded.
Owner check: two browsers on the local network join one match by invite code.

#### M22 — 1v1 Arena

Goal: a fair 1v1 fight where both players draft any weapons and logbooks from the whole game (request 8).

- I22.1 Draft: each player picks N weapons and M logbooks from the full catalogue, ignoring unlocks (request 8; D8f).
- I22.2 Arena map, PvP damage scaling and invulnerability frames (no one-shots), light PvE, best of three rounds.
- I22.3 Results from the match server (M13b, M21), onchain match record (M12), a seasonal rating; unwagered matches pay a fixed RUM amount per match (for example 10 for a win, 3 for a loss) with
  no daily cap (Q10), plus the rating and cosmetic season ranks; Q34 payout rule (Arena and Hunt, unwagered): a player who surrenders, leaves or fails to reconnect gets nothing,
  and a player idle for 30 s (no inputs) counts as having left, at any point in the match; if a match ends within 60 s
  because the opponents left, nobody is paid (this is what stops instant-forfeit farming), and after 60 s the players
  still in the match are paid normally; a match that ends in under 60 s by real combat (every player active, the loser
  sunk by a player's weapons, PvE, the storm or the edge wall) pays in full.
- I22.4 Invites: challenge a friend with an M19 link or code.
- T22.x Draft validation, PvP damage rules, forfeit and reconnect, Q34 payouts: a genuine 40-second win pays both
  players; a loser sunk by PvE at 45 s is paid; an instant forfeit pays nobody; a forfeit at 90 s pays only the winner;
  a player idle for 30 s at 90 s is treated as leaving.

Automated check: two scripted clients draft and play a best of three to a recorded result.
Owner playtest: two browsers on the local network: draft and fight.

#### M23 — Hunt (4-player free-for-all)

Goal: four players on one sea, enemies and bosses everywhere, last ship afloat wins (request 9).

- I23.1 A large sea from a VRF seed, a shrinking storm (the existing storm), POIs and chests.
- I23.2 PvE: ambient enemies, elites and bosses on a Hunt timeline; bosses drop big loot.
- I23.3 The last ship afloat wins; spectate after death; a fixed RUM amount per placement for unwagered matches, no daily cap (Q10); the Q34 payout rule in M22 applies
  (sinking by players, PvE, the storm or the edge wall counts as combat; leaving, idling or not reconnecting pays
  nothing; if the others leave within 60 s, nobody is paid).
- I23.4 On the M13b match server: the server may simulate more enemies across the whole sea, but each client's view (what it
  receives and renders) stays within the device cap (Q21); interest management; bandwidth per client measured.
- I23.5 Invites: fill a Hunt lobby with friends through M19 links.
- T23.x A 4-client local test, storm and placement rules, Q34 payouts (three players leave at 30 s: nobody paid; three
  leave at 5 minutes: the last ship paid as the winner; a ship sunk by the storm at 50 s gets its placement); AI
  captains (M16) fill seats in tests only.

Automated check: four scripted clients (or AI seats) play to a winner; bandwidth and CPU logged.
Owner playtest: a 4-player match on the local network (with AI fills if needed).

#### M24 — Wagers

Goal: optional stakes on Arena, Hunt and Race, winner takes all, 5% house cut; formats defined per mode.

- I24.1 Formats: **Arena** — two equal stakes; the winner takes the pot minus 5%. **Hunt** — four equal stakes; the last
  ship afloat takes the pot minus 5% (Q28, confirmed); a match that starts with fewer than 4 humans refunds everyone. **Race** — equal
  stakes from every entrant in the race window; entries close when the window opens for play; the fastest verified run
  takes the pot minus 5%; fewer than 2 entrants refunds; an entrant without a verified finish loses the stake.
- I24.2 Web2: RUM stakes held in server escrow (settled RUM only; legacy RUM excluded, D6); payout = pot − 5% (a RUM sink).
- I24.3 Web3: Anchor escrow for SOL and SPL (SKR, ORE stand-ins): create, join, cancel before start, `settle(winner)`
  signed by the server authority after the match record is final, permissionless refund after a timeout; 5% to the
  treasury; integer math with the remainder rule written down.
- I24.4 Rules: stakes only between accounts in the same mode; tiers and daily limits; no stakes against AI seats; linked-account hooks
  (D15, off for now).
- I24.5 UI: stake choice, pot, escrow link, result, payout; wagered invites (challenge a friend with a stake).
- T24.x Escrow tests (settle once, refunds, rounding, unauthorised settle rejected) for each format; ledger tests;
  fuzzed pot math; timeouts.

Automated check: scripted wagered Arena, Hunt and Race matches settle on devnet with explorer links saved.
Owner playtest: wager devnet SKR in an Arena match against a second tester's account.

### Stage F — Creator economy and brands

#### M25 — Sail designer

Goal: players design and paint their own sails and sail with them (request 15).

- I25.1 Paint tool (mobile-first): layers, brush, fill, shapes, stickers and emblems (from the art team), symmetry,
  palette, undo; 512×512 canvas on the `Sail.001` UV (M6 spec); optional flag.
- I25.2 Live preview on the ship (`EnemySailShader._BaseTexture`), in the menu and in runs (pooled material instances).
- I25.3 Save to the server (PNG + layer JSON); no moderation on devnet (Q13); report and review before any mainnet.
- I25.4 Origin (D4): a sail painted with no RUM spent (stickers bought with RUM count as RUM) can be minted; otherwise it
  stays a web2 item.
- D25.1 Art guide updated with the sticker and emblem sets.
- T25.x Tool operations, texture export, size limits, a stored report flag (no review on devnet), origin flags.

Automated check: a scripted paint session saves a sail and shows it on the ship in a run, with screenshots.
Owner playtest: paint a sail and sail with it.

#### M26 — NFTs, royalties and ownership sync

Goal: sails and islands can become NFTs with 5% royalties, the game follows whoever holds them, and they migrate to an
external wallet (completes request 18).

- I26.1 Metaplex Core assets in two collections (sails, islands); Royalties plugin at 500 bps to the treasury with an
  allow-list rule set that includes our program, Tensor and Magic Eden (only an allow-list enforces royalties; it also
  blocks transfers by unlisted programs, so migration and gifting go through our program).
- I26.2 Minting rules (D4): only web3-origin items; an island NFT is a base plot (Q7: one plot is your active home) that carries its layout, its
  web3-origin defenses and decorations and its record; RUM-bought parts return to the seller's inventory on a sale, and the RUM in the treasury and vault stays with the seller.
- I26.3 Ownership sync: an indexer or webhook plus a reconcile job keep the game's base and inventory owner in step with
  the NFT holder, including transfers and sales outside the game.
- I26.4 NFT migration to an external wallet through our program.
- T26.x Mint rules, royalty configuration, ownership sync after an outside transfer, NFT migration, D4 origin rules.

Automated check: scripted devnet flows (mint, outside transfer reconciled, migrate) with explorer links.
Owner playtest: mint a sail, move it to your Phantom wallet, see the game follow it.

#### M26b — Marketplace

Goal: players trade islands and sails in the game for RUM (web2) or tokens (web3), and NFTs also trade on outside
Solana marketplaces (request 19).

- I26b.1 Web2 listings in RUM for web2-origin items only (server escrow; D15 limits off for now).
- I26b.2 Web3 listings in SOL/SKR/ORE for NFTs through our Anchor listing program using Core Freeze + Transfer
  Delegates (no escrow; the buy pays the 5% royalty); no other fee (Q6); a minted item can never be listed for RUM (D4).
- I26b.3 Custodial accounts list and buy with server signing; self-custody accounts sign with their wallet.
- I26b.4 Outside marketplaces: verify listing through Tensor's open-source marketplace program on devnet; a sale on
  Magic Eden or Tensor themselves is verified only when a devnet market exists (neither was confirmed).
- T26b.x List, buy, cancel, royalty on a secondary sale, double-sell prevention, web2/web3 separation.

Automated check: scripted devnet flows (list, buy with royalty, cancel) and a web2 RUM trade.
Owner playtest: list a sail, buy it from a second tester's account, check the royalty.

#### M27 — Brand bounty islands

Goal: brands fund bounty islands dressed in their assets; the top kill players share the bounty (request 20).

- I27.1 Sponsor tools (admin): a bounty island (VRF layout, brand theme pack, prize pool in escrow, window, payout
  split top 10 at 30/20/12/9/7/6/5/4/4/3 % (Q11), eligibility).
- I27.2 Brand assets in the level (section 7).
- I27.3 Bounty board: single-run kills on that island in the window, played as server sessions; program payout to the
  top 10.
- D27.1 Art guide updated with the brand kit slots and a sample kit.
- T27.x Escrow and split math, verification before payout, theme pack loading.

Automated check: a scripted bounty from creation to payout on devnet.
Owner playtest: play a test bounty island with a sample brand theme and see the payout.

#### M28 — Economy audit and launch hardening

- An economy model of every RUM source and sink and every token flow; anti-farming and anti-collusion caps; D4 proven
  by tests.
- A security review (keys, fee payer, auth, rate limits, replays), load tests of the server and the match server, a
  devnet end-to-end rehearsal, and a legal checklist for any future mainnet (not a launch).

Automated check: the audit report, load-test results and the rehearsal log.
Owner check: read the audit and decide what is next.

---

## 6. Risks

| Risk | Where | Mitigation |
|---|---|---|
| Plausibility checks can be fooled by results that look believable (a modified client or a bot), and that voyage RUM can then reach other players through wagers, the marketplace or raids | M10, M15, M24, M26b | Bounds from the balance model and simulator, tested against forged logs; modes that move value between players are played on the match server (D6); Q35 decides whether checked voyage RUM is usable there right away |
| Server-run sessions for raids, races, ranked voyages and 6 AI raids per player per day cost server CPU and memory, and need a steady connection | M9, M13b, M15–M18 | Sessions per process and memory measured in M9; AI raids run headless faster than real time; the per-component Railway estimate in `ANSWERS.md` (about $20–25/month if ~30 of 100 players play daily, $40–45 if all do), measured before M13b closes; Serverless sleeping and a spend limit; reconnect handling |
| Real-time multiplayer in WebGL is the largest single item (server authority, WebSockets only, hundreds of enemies) | M9, M13b, M22, M23 | Go/no-go spike first; interest management; local only until hosting is approved |
| Custodial keys, held on our Railway server, and the fee payer are attack targets; the master key and the database live in the same Railway project, so one leaked Railway account exposes both | M11 | Devnet only; encrypted keys with the master key in a Railway secret; an offline master-key backup and rotation (key id); separate secrets for the fee payer, treasury and settle authority; allow-listed instructions and rate limits; a key vault or provider before any mainnet (D7) |
| Netcode for GameObjects has no built-in interest management or lag compensation, and version 3.0 is brand new | M9, M13b, M22, M23 | We write both (budgeted in M13b); pin a tested 2.x release; measured in M9 against numeric limits (I9.4); enemies sent as our own batched messages, so a later netcode switch keeps most of our code |
| Browser memory: Chrome on 4–6 GB Android phones is 32-bit, capping the game near 256 MB (today's build allows 2 GB) | M1, M7 | Measured on the reference phone in M1; the budget enforced in M7 (`research/DEVICE_TARGETS.md`) |
| Real-value wagering is regulated | M24 | Devnet only; legal review, geo-fencing and age checks before any mainnet (D8) |
| RUM leaking into tokens through items | M14, M25, M26 | Origin flags and tests (D4) |
| Collusion between alt accounts to move RUM or tokens (raids, AI raids, leaderboard prizes, referrals, wagers, the marketplace, bounties) | M15, M16, M18, M19, M24, M26b, M27 | Deferred by the owner (D15); hooks kept, each tested with its switch on; audit in M28 before real value |
| Hosting vendors changed in 2026 (Multiplay, Hathora gone); Railway has no inbound UDP | M13b | Railway only (Q17): browser clients use secure WebSockets, which Railway supports; the match server is a headless build that could move elsewhere |
| Royalty enforcement blocks unlisted programs | M26 | Allow-list includes our program, Tensor and Magic Eden; migration through our program |
| .NET 8 end of support on 10 Nov 2026 | M10 | Target .NET 10 LTS |
| Scope: 33 phases | all | Stage cut lines; the owner can reorder stages (Q1) |
| Art lags content | M4, M5, M14, M25 | Placeholders + the M6 art guide |
| Mobile browser memory and frame rate | M1, M7, M23 | One enemy cap set on the reference low-end phone in M1 (Q21, Q31); quality tiers reduce effects, never the cap |

---

## 7. Ideas: decorations and brand assets

**Island decorations** (cosmetic only, D14), by family — each goes into the art guide:

- Dock and harbour: jetties, lanterns on posts, rope coils, crates and barrels, a small crane, mooring bollards.
- Pirate life: a tavern hut, a rum still, hammocks between palms, a campfire with a spit, a lookout tower, flag poles.
- Treasure and trophies: trophy pedestals (raids won, leaderboard titles), a treasure pile, a golden statue of your
  Captain, a bounty board showing wins.
- Nature: palm variants, flowering bushes, tide pools, coral arches, a waterfall rock, sea-bird nests, glowing shells.
- Spooky (Gravewater): ghost lanterns, a skeleton crew bench, a wrecked hull, will-o'-wisps.
- Seasonal: festival bunting, a lantern festival, frosted palms, an anniversary fireworks platform.
- Skins: lighthouse, Beacon, dock, path tiles, fences and walls.
- Chippy's corner: a stash of shiny things, a chipmunk house.

**Brand assets on a bounty island** (M27): the brand's hero object as the island's special landmark; sails and flags on
the bounty elite and boss; billboards and banners on islands and docks; a branded treasure chest and drop skins; a
themed decoration set players can win; a loading card and share-card frame; a sponsored line from Penny; a limited sail
only bounty finishers earn. Designed per brand as custom elements at first (owner's choice).

## 8. Ideas: retention loops

Daily charts, login streaks with a restore, the weekly leaderboard, raid revenge and shield timers, weekly races,
Captain mastery, Collection completion, the season track, friends' activity, brand bounty events, first-conquest
milestones, and returning-player bonuses — all paying capped RUM (D4) or cosmetics, never power (D14).

---

## 9. Questions for the owner

All V5 questions are answered (three rounds on 30 Sep; decision D17 and `ANSWERS.md`). The playtest detour raised the
questions below (6 Oct, `plan/playtest/V5-RESUME.md`).

| # | Question | Options (suggested first) |
|---|---|---|
| P1 | Do the playtest rules replace M3c (level rhythm), M3d (no HP growth, 10-minute sketch) and D17 (Logbook ranks 10-12, evolution at level 8)? | Answered 6 Oct: yes, the plan follows the game as played (D18) |
| P2 | Overtime Rum has no cap since the playtest (D4 says it must flatten and cap). | Answered 6 Oct: keep it uncapped (D18) |
| P3 | Weapons: all 19 are free starters now (no weapon shop, Ember's lock always met). | Answered 6 Oct: all free for now, including Captains, Logbooks and items; locks later (D18) |
| P4 | The v3-parity preset (D16, "matches V3" regression baseline) no longer fits the game. | Answered 6 Oct: retire it (D18) |
| P5 | Multiplayer: the playtest built a WebSocket relay on the API for 1v1. | Answered 6 Oct: Netcode for GameObjects only; the relay stays until the Netcode 1v1 replaces it (D18) |
| P6 | PvP anti-cheat for rated 1v1 now (clients report their damage). | Answered 6 Oct: add anti-cheat now (D18) |
| P7 | Boss: summonable at the Beacon at any time (now); its health was halved for a 14:00 arrival. | Answered 6 Oct: an early summon keeps the 14:00 hull (D18) |
| P8 | With desktop WebGL as the main test build, do the D9 touch rules still bind new screens? | Answered 6 Oct: yes (D18) |
| P9 | API auto-deploy: on every push, or after a manual approval in GitHub? | Answered 6 Oct: every push (D18); changed 6 Oct: manual deploys by the agent (D19) |
| P10 | Merge `playtest` into `v5-live-game` and continue there (M8z)? | Answered 6 Oct: yes, merged (D18) |
| P11 | Treasury and raids (Q23b reopened, 6 Oct): you want a livelier economy than vault 80% / treasury 20%, a raid takes 20% of the treasury, 8 h shield, at most 10% lost a day. | Answered 6 Oct: option B with 4-hour shields (D19) |
| Q16 | Pickup sparkles on loot barrels: fewer particles, or keep them? | Answered 7 Oct: Remove them (D21) |
| Q17 | The Knockback upgrade card: six weapons can roll it and it does nothing for them. | Answered 7 Oct: Remove it (D21) |
| Q19 | Ramming Prow needs the boost button, against the "auto-attacks" rule. Keep it? Should enemies still hurt the ship while it rams? | Answered 7 Oct: Remove the Ramming Prow; replace it with an auto-attack slowing weapon (D21) |
| Q20 | New weapons' rank 4/8/12 milestones (an extra anchor, a second pulse...): keep as upgrade steps, or drop? | Answered 7 Oct: Keep (D21) |
| Q23 | Should the Harpoon's tar become a real slow? | Answered 7 Oct: Yes (D21) |
| Q24 | The Reef Crab (miniboss): slowed and knocked back like regular enemies, or treated like the boss (half slow, immune to knockback)? | Answered 7 Oct: Like the boss (D21) |
| Q25 | Should slows also shorten enemies' charge dashes? | Answered 7 Oct: No (D21) |
| Q26 | The flame reaches exactly its damage cone (9.5 m). Does it look right and dense enough? | Answered 7 Oct: The owner replaces the effect later (D21) |
| Q27 | Luck: one scale for level-up cards and chests? | Answered 7 Oct: One scale (D21) |
| Q28 | The four designed powerups (Battle Signal, Tailwind, Salvage Chime, Full Sail): which mix? | Answered 7 Oct: add Battle Signal and Full Sail; leave out Tailwind and Salvage Chime (D21) |
| Q32 | Admiral: elites only, or the boss too? | Answered 7 Oct: Elites and the boss (D21) |
| Q5 | New enemy types with new art: which phase, and who makes the models? | Answered 7 Oct: Later; the art team (D21) |
| Q37 | Collection: from Profile (built), or its own button on the art menu (needs the art team)? | Answered 7 Oct: From Profile (D21) |
| Q38 | "New" badges and sales: which content, and when? | Answered 7 Oct: Later, when locks return (D21) |
| Q33 | Quality tiers: the same gameplay caps on every device, or fewer player projectiles on a Low phone? | Answered 7 Oct: Same caps everywhere (D21) |
| Q34 | The 1024 texture cap: mobile only, or the desktop web build too? | Answered 7 Oct: All web builds, desktop too (D21) |
| Q42 | Health bars sort far to near every 8 frames (a near bar can sit under a far one briefly). Fine, or every frame? | Answered 7 Oct: Every 8 frames (D21) |
| Q40, Q44 | Phone builds: ASTC effects and UI art; the Android 1024 UI cap. | Answered 7 Oct: At the next phone build (D21) |
