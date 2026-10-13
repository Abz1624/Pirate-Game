# Hackathon retro (12-13 Oct 2026)

Branch `hackathon-demo`: 88 commits after tag `v5-m9c-s1` (`git log v5-m9c-s1..a5a8be02`), fast-forwarded into
`v5-live-game` at `a5a8be02` (13 Oct). `main` was still at `63e6962a` when this was written. Plan: `PLAN.md`; builds:
`BUILDS.md`; devnet smoke test: `devnet-smoke.md` (8/8 on the live API, 12 Oct); API reference: `server/README.md`
("Hackathon: ..." sections). Everything was built to **demo quality**: the happy path works end to end on Solana devnet.
None of it went through the V5 phase gate (no phase review, no owner playtest gate, no full suites after 12 Oct 20:00).
How it folds into V5: `plan/v5/V5_PLAN.md` rev 17 ("Hackathon interlude", phases M9h and M9i).

## 1. What was built

### Server and web3 (`server/RoguePirates.Api`, Solnet, devnet; server tests 176 -> 211)

| Feature | What exists | Commits |
|---|---|---|
| Wallet sign-in (SIWS) | Nonce (5 min, single use) + ed25519 signature check; profile keyed by wallet, optional email; the message names the page origin when it is an allowed origin (Phantom refused a fixed `roguepirates.fun` domain on localhost) | `f89164e0`, `0f13b061` |
| Treasury / escrow | One keypair (`TREASURY_SECRET_KEY`, Railway variable) is treasury, escrow, payout signer and NFT mint authority | `f89164e0`, `00ad7316` |
| Deposits and payouts | `deposits/verify` (confirmed tx, signer, transfers to treasury, memo, each signature credited once); payouts with retry (10x), never paid twice; admin payout and devnet airdrop | `f89164e0` |
| Island NFTs | Claim (free, one island at a time, so claim-sell-claim repeats the treasury-paid mint) mints a 1-of-1 Token Metadata NFT (metadata + master edition, **0 bps royalties**, symbol `RPISL`) to the player's wallet; metadata JSON with RUM per hour and defence record traits; ~0.018 SOL per mint | `8b4de135`, `cf9826e2` |
| Islands, base building, RUM | 24 world slots, one island at a time per player; buildings `rum_store` (brews RUM per hour up to the store cap), `vault` (60% never raidable), `cannon_tower`, `walls`; collect; voyages pay `kills/10` RUM (linear, unchecked) to wallet players | `8b4de135` |
| Raids | 25 RUM fee, conquest: survive the island's designed waves and sink its guardian (Barnacle King); won 25% of exposed RUM (partial 12.5%), 4 h shield, one raider at a time; result **client-reported** (`runLog` accepted, not checked); SOL raid stakes switched off (old staked raids still settle) | `8b4de135`, `f428d782`, `d041c5ca` |
| Defence designer | 15 one-minute windows, points budget per window (grows per minute and with buildings), enemy cost table from game data, guardian + arrival minute, presets, default design, server validation | `d041c5ca` |
| Practice raids | Test your own island: no fee, loot, record or voyage RUM | `cf9826e2`, `7aeebfc6` |
| Daily race | One HMAC-seeded course per UTC day, fastest time to 100 kills, best of many attempts, run checks in shadow mode; `DAILY_RACE_SALT` has a fixed default | `8b4de135` |
| Weekly SOL rewards | Top 3 wallet players of the weekly kills board paid 0.5/0.3/0.2 devnet SOL; hourly auto-payout job **on**; `WEEKLY_REWARD_CHECKED_ONLY=false` (shadow-mode runs win) | `8b4de135` |
| 1v1 SOL wagers | Stake 0.001-0.5 SOL, deposit bundled with create/join, winner paid 2x (**no rake**), refunds on cancel/leave/expiry/draw, deposits for an invalid seat refunded once (`409 deposit_refunded`); vs Captain Marrow the house matches the stake; result from the relay referee (sinking client-reported) | `8b4de135`, `38c56cee`, `7aeebfc6` |
| Marketplace | Server escrow: NFT sent to the treasury, buyer pays with memo, server moves NFT + island and pays the seller; cancel returns it; island locked while listed | `8b4de135` |
| Run checks v8 | Rules regenerated for config `8ea655e3` (enemy damage x2, flail); older demo builds accepted with merged bounds; 0.05% false positives | `72918c94` |
| Admin | Payouts, airdrop, wager sweep, weekly reward run (dry run), demo reset (`X-Admin-Key`) | `f89164e0`, `a50b6318` |

### Game (Unity)

| Area | What exists | Commits |
|---|---|---|
| Hackathon assembly | `Assets/_Game/Hackathon/` (`RoguePirates.Hackathon`, Editor, EditMode tests 33): `Web3Session` (MagicBlock Solana Unity SDK pinned `bd0d30f6`; WebGL wallet adapter, Android Mobile Wallet Adapter, Editor test wallet), API clients, wallet menu, Islands, Defence, Market, Daily Race, 1v1 wager, rewards strip, raid HUD | `7ae926cd`, `64386ca1`, `91906f71`, `fad370cf` |
| Parchment UI kit | `MenuKit` (V4 Presentation) + `HackSkin`: every hackathon screen, V4 settings, pause menu, hub Shop and Profile, results, item popup, toasts, HUD meters, minimap frame, 1v1 hub/lobby/draft/HUD/end card | `b690aec0`..`741125e3`, `ef16d496`, `e826c743`, `de3f8047` |
| Captain Marrow | Named AI captain on the PvP bot (banner, barks); fires its draft's real weapons and Logbooks via `PvpBotArsenal`; also the island defender name | `64386ca1`, `c153ef3f` |
| Anchor Flail | Chained maces thrown out and reeled back (1 per throw, +1 at ranks 4/8/12, max 4), Kraken Chain makes them heavier; level-1 DPS matched to the starters (baseDamage 20 -> 12.1) | `3a94e3fa`, `7b82a2d4`, `6fcbfad1` |
| Gameplay fixes | Enemy damage x2 (`ExperimentConfig.enemyDamageMultiplier`), enemy shot spread scales with target speed, orbit weapons spin in world space, elite drops magnet within 5 m, boss/miniboss no longer shoved by spawns, daily race ends at the goal, XP gems without the coin sparkle | `c153ef3f`, `e122811a` |
| Minimap | Snapshot minimap (one-time island render, 10 Hz icons; ship, POIs, boss only) | `e122811a`, `c153ef3f` |
| Storm edge | HUD warning instead of the red boundary walls | `5709f36f` |
| Content switches | Signal Buoy off (dropped from the layout); flamethrower, lighthouse lantern, squid ink pots, siren bell off via `UnlockConfig.disabledWeaponIds`; Cursed Chest Key and content without an icon out of the pools | `1afc511c`, `c2dfd925`, `d5c98f94`, `f072e21a` |
| Renames | `admiral` -> Big Game Hunter, `extra_hands` -> Echo Arsenal, `cuttlefish_bone` -> Kraken Ink Sack (ids unchanged) | `f072e21a` |
| Art team | Models, VFX and assets from `origin/gameplay` (`78b11f5c`), wired presentation-only (`DemoJuiceSettings` art slots, ship lift), UI icons by content id (`V4IconCatalog`), Cursed Doubloons coin particles | `4a318b5d`, `98791316`, `7c970526`, `fc6ab20b`, `b7e3ec86` |
| App | Logo app icon (all Android kinds), poster splash, Unity logo off; Ledger (shop) button and voyage download buttons hidden | `5d79d592`, `f9d321d5`, `fb53fb38`, `2b26b5aa` |
| Pause | HUD pause button: voyage/race/raid pause menu; 1v1 "Match menu" (match keeps running, forfeit confirm) | `4b1e0a49` |
| Builds | Batch entries `HackathonApkCli.Build` (APK) and `.BuildWebGL`; final APK 194.9 MB, desktop WebGL 142.5 MB at `a5a8be02` (compile check only, not installed) | `63e6962a`, `4f91f801`, `a5a8be02` |

## 2. Against the V5 plan

**Delivered fully (demo or better, reusable as is):** wallet connect with the Solana Unity SDK on WebGL and Android
(I13.1 connect part, but in the Android app rather than Chrome), SIWS sign-in (I13.1), the treasury/vault split, 25%
raid loot, 4 h shield, attacking keeps your shield, raid fee (D19/D24 numbers), deposit verification and idempotent
payouts as building blocks for M24/M26b, the parchment UI kit (D9 layout rules applied at 20:9 and 960x600).

**Delivered partly (demo quality; what is missing against the plan's acceptance):**

| V5 phase | Hackathon piece | Missing |
|---|---|---|
| M10 ledger | `Players.rum` balance, deposits, payouts | Ledger with pending/settled RUM, command envelope (idempotency key, expected version), server seeds; RUM is credited from client-reported runs (`kills/10`) and raid loot |
| M10b accounts | Wallet profiles, optional (unverified) email | Google and email-code sign-in, save migration, legacy RUM |
| M11 wallets | Self-custody wallets only | Custodial wallets (D7), fee payer, SKR/ORE stand-ins, token prices |
| M13 connect + migrate | Connect and sign-in | Migration from custodial (nothing custodial exists yet) |
| M14 base | 24-slot islands, 4 buildings, 15-window defence designer, guardian boss | Sections and sockets that survive permutation, defences as structures, decorations, base snapshots, test-raid before publish (practice raid exists) |
| M15 raids | Full loop on the client | Server session (M13b), VRF permutation, Captain's Log, revenge, receipt uniqueness under concurrency tests |
| M16 AI captains | Captain Marrow (fixed bot) | `AiCaptainProfile`, runtime brain, raiding for you, presets |
| M17 race | Daily race to 100 kills | Plan's race is one attempt to conquer a published base, server timed |
| M18 weekly board | SOL prizes paid from the treasury | Server-session ranked runs, capped RUM rewards; prizes today ride on shadow-mode client runs (D6 breach) |
| M24 wagers | 1v1 SOL wagers with server escrow wallet | Anchor escrow, 5% rake, server-authoritative result (relay sinking is client-reported), no stakes vs AI seats (I24.4) |
| M26 NFTs | Island NFTs (Token Metadata) | Metaplex Core, 5% royalties allow-list, D4 minting rules, ownership sync, NFT migration |
| M26b market | Escrow market for islands | Listing program (Core freeze + transfer delegate), royalties, web2 RUM listings, D4 separation |
| M28 | - | Security review of all the above |

**Contradicts the plan or a decision (owner questions H1-H14 in V5_PLAN section 9):**

- **D7 / M11 custody**: the plan creates custodial devnet wallets at email sign-up; the hackathon made self-custody
  wallet sign-in the web3 path (MagicBlock SDK, Phantom, Seed Vault via MWA).
- **D6 server authority**: SOL moves on client-reported results: human-vs-human stakes (relay sinking), staked 1v1 vs Captain Marrow (the bot runs in the
  human's client; a modified client can fake the bot's sinking and win the house's 2x), weekly SOL prizes from
  shadow-mode runs (auto job on), raid loot from a client-reported conquest, voyage RUM `kills/10`.
- **D7 separate secrets**: one key is treasury, escrow, payout signer and mint authority.
- **D4 two economies**: an island claimed for free and upgraded with RUM is sold for SOL with its buildings and its store
  **and vault** RUM (the sale only changes the owner, `MarketService.cs:206`); I26.2 says RUM-bought parts and the RUM
  stay with the seller. Voyage RUM is linear (`kills/10`), not flattened and capped. Brewed RUM is capped per store but
  not per day.
- **D8**: house-matched 1v1 stakes (betting against the operator) and treasury-paid weekly prizes are outside D8's
  player-vs-player wagers and need its legal review before any mainnet.
- **M24**: no 5% rake (winner 2x); stakes against the house/AI (I24.4 forbids stakes against AI seats); wagers exist
  before server-authoritative 1v1 (D19(2) order).
- **M26**: Token Metadata NFTs with 0 bps royalties, not Metaplex Core at 5%.
- **M14/M15 design**: 15 one-minute windows and a guardian boss to sink vs the plan's 0:00-5:00 schedule and Treasury
  Beacon; 24 world-map slots vs the home-seed island.
- **M17**: daily kills race with unlimited attempts vs one-attempt island conquest race.
- **D27 naming**: Cuttlefish Bone is now shown as Kraken Ink Sack.
- **Balance**: enemy damage x2 and the flail rework changed the config the M2 model and the M9c fingerprints were
  measured on (goldens re-frozen in `c284e105`/`6fcbfad1`).

## 3. Known limits (from PLAN.md, BUILDS.md, server/README.md)

- Staked 1v1 vs Captain Marrow is client-reported (fix: server runs the bot, or bot matches carry no stake).
- Practice raids pay nothing.
- APK signed with the Unity debug key; ARM64 only; WebGL wallet adapter script loaded from jsdelivr at runtime with no
  integrity check (it drives the signing prompts). Both tracked in V5 M13.
- Island NFTs are standard Token Metadata NFTs: a trade outside the game leaves the game's owner record behind.
- Release builds have no in-game test wallet; wallets must be on devnet with devnet SOL.
- `server/README.md` "Limits of this playtest API" still says "Nothing here moves value": stale since the hackathon.

## 4. Pending and paused (carry-over: V5 phases M9h (safety, gate) and M9i (run checks, fixes, art) unless noted)

1. **Live API safety** (M9h): weekly SOL auto-payout on unchecked runs, staked bot and human 1v1s, client-reported raid
   loot, island sales carrying RUM, the fixed daily race salt (seeds predictable), free claims with treasury-paid mints,
   the single treasury key. Owner decision H2.
2. **Gate the hackathon diff**: no full EditMode/PlayMode run since `c284e105` (12 Oct: EditMode 787/791, PlayMode
   130/134, failures fixed in `72918c94` by targeted runs); 13 Oct commits were compile-checked only. The deep review
   (`plan/hackathon/review/`, running 13 Oct) adds its findings to M9h.
3. **Run checks behind the config**: v8 rules are for `8ea655e3`; later config changes (switched-off weapons, pool
   changes) need a re-export and regenerated rules, then a deploy.
4. **Captain Ember** starts with the flamethrower, which is switched off (`ember.asset` `startingWeaponId`,
   `prerequisiteWeaponId`). Owner question H9.
5. **Paused new content**: items `devils_match`, `hammerhead_helm`, `queen_annes_fuse`, `storm_in_a_bottle` and the
   Chaos Logbook. **Paused weapon VFX prefabs** task (`WeaponPrefabLibrary`, `WeaponVisualCatalog`,
   `HackathonWeaponPrefabs`, tests). Both in one patch, copied into the repo at `plan/hackathon/parked/`
   (`stopped-agents-wip.patch` + `stopped-agents-untracked/`; the originals sit in a temporary scratchpad). The patch
   no longer applies cleanly (5 files conflict: `cuttlefish_bone`, `admiral`, `extra_hands` assets, `OfferCards.cs`,
   `WeaponVfxPresenter.cs`): re-apply with a 3-way merge. New content goes in V5 phase M5c (after M9c), VFX prefabs in M9i.
6. **Weapon art mapping** (owner): Maelstrom Charge -> `maelstrom_charge`, Mortar Barrage -> `mortar`, Tidal Aura + fish
   bell -> `siren_bell`? Unmapped icons in `Assets/_Game/V4/Demo/Art/Icons/Unmapped/` (TODO-FOR-YOU).
7. **Art team issues**: Kraken_Crown `Kraken.mat` white emission (pale), CutthroatSkiff wake trail without a material,
   2048 px ship textures (web cap 1024).
8. **Big Game Hunter** (Admiral): check that its bonus damage reaches the boss (D21 Q32: elites and the boss).
9. **Phone UI**: menu side buttons small on phones; unchecked on device: pause menu, level-up cards, storm edge warning.
10. **Git stash** `stash@{0}` "ui-redesign WIP (stopped 13 Oct, owner kept placeholder UI)" (04:35, 7 Hackathon UI
    files): superseded by the parchment kit redraws (05:00-11:00, same files); drop it after the owner agrees.
11. **M9c S1b**: parked on `wip/m9c-s1b` (`3ea93ddd`, lost-ship investigation, not reviewed or tested). It overlaps the
    hackathon in `EnemySimulation.Tick.cs` (enemy aim spread) and the voyage fingerprints were re-frozen during the
    hackathon: rebase onto `v5-live-game` and re-run the S1 probes before continuing.
12. **AGENTS.md**: the code layout table now lists the Hackathon assembly (rev 17); `server/README.md` limits section
    to update in M9h.
13. **`main`** is at `63e6962a`, not at `a5a8be02`: fast-forward when the owner says.
