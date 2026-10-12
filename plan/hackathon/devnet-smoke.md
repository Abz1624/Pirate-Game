# Hackathon devnet smoke test

Run 2026-10-12 02:13 UTC against `https://api-production-dad8.up.railway.app` (Solana devnet) by `server/tools/DevnetSmoke`.
Player A `DcUmg69FiPbf49Z1SMUBxNXEQszuum3d1dQ2bbsDcBkr`, player B `3cm6nYP9gUmSuQ2GyWSNSNXwUvEBKKFUGEhyXSp9irGB`, treasury `Gw2ao9v6cAB8oQiJJsy7h1wZuVS8wDyZNj6bU8z5GsG7`.

**Result: all 8 steps PASS (0 FAIL). No server bugs found; nothing redeployed.**

How to run again (from the repo root; keys are read at runtime from `%USERPROFILE%\.rp-devnet`, never printed):

```powershell
dotnet run --project server/tools/DevnetSmoke -- --out plan/hackathon/devnet-smoke-rerun.md
# options: --api <url> (default the Railway API), --rpc <url>, --keys <dir>, --skip-weekly-pay
```

Notes:
- Raid loot and the first collect are 0 RUM because both islands had just been claimed (nothing brewed yet); the loot math is covered by the server tests.
- The 1v1 match itself needs a game client (relay WebSocket), so step 5 proves stake -> deposit -> funded -> lobby closed -> on-chain refund.
- Step 6 paid the **current** week (2026-10-12) for the demo. That week is now recorded, so the hourly job will not pay it again after Monday 19 Oct; the voyages behind it (4000 and 1500 kills) are smoke-test runs by A and B.
- Written to be re-runnable (not re-run yet): B claims a new island (its first one now belongs to A), the weekly step would find the week recorded and check nothing is paid twice.
- Known limit (not a bug for the demo): an Island NFT moved wallet-to-wallet outside the game does not move the island in the API; ownership changes only through the market.

No secrets in this file: addresses, ids and transaction signatures are public.

| Step | Result |
|---|---|
| 0. Setup: keys, treasury, fund wallet B | PASS |
| 1. Wallet sign-in (A and B), profile email for A | PASS |
| 2. Islands: claim + NFT on chain, collect, RUM from a voyage, build, world list | PASS |
| 3. Raid: A raids B's island with a 0.01 SOL wager, won -> loot, shield, 2x payout | PASS |
| 4. Market: B lists island NFT, A buys; A lists and cancels | PASS |
| 5. 1v1 wager vs the house: stake, deposit, funded, close lobby -> refund | PASS |
| 6. Weekly kill rewards: dry run, real run (current week), public view | PASS |
| 7. Daily island race: race, entry, board | PASS |
| 8. Final balances | PASS |

## 0. Setup: keys, treasury, fund wallet B: PASS

- wallet B generated and saved to walletb.json
- ok: admin key available (value not shown)
- A = DcUmg69FiPbf49Z1SMUBxNXEQszuum3d1dQ2bbsDcBkr
- B = 3cm6nYP9gUmSuQ2GyWSNSNXwUvEBKKFUGEhyXSp9irGB
- ok: API health (https://api-production-dad8.up.railway.app): {"ok":true,"db":"postgres"}
- ok: treasury Gw2ao9v6cAB8oQiJJsy7h1wZuVS8wDyZNj6bU8z5GsG7 on devnet, 24.4643948 SOL
- start balances: A 9.999995, B 0, treasury 24.464395 SOL
- ok: B funded: 2 SOL

Transactions:
- fund B with 2 SOL from A: [2kSBkmmkFWVp...](https://explorer.solana.com/tx/2kSBkmmkFWVp7edj1YnSaGWeKNMw2J4dtXYdYrnY1Ln2yuDZ19voVeHcuuKHMDprqRzs7UEBwVK4pJ3SDECANHu9?cluster=devnet)

## 1. Wallet sign-in (A and B), profile email for A: PASS

- ok: A signed in as Captain DcUm #9534 (player bea820fe-4e6d-4917-a7a9-2df29b771f8f, isNew true)
- ok: A replayed signature refused (401 nonce_used)
- ok: B signed in as Captain 3cm6 #3545 (player 2ba39fb9-1b21-495f-850d-9257ba9008cd, isNew true)
- ok: B replayed signature refused (401 nonce_used)
- ok: A profile: email smoke-a@example.com, wallet DcUmg69FiPbf49Z1SMUBxNXEQszuum3d1dQ2bbsDcBkr, rum 0

## 2. Islands: claim + NFT on chain, collect, RUM from a voyage, build, world list: PASS

- A claimed island Smoke Cove A (slot 11 Gallows Point)
- ok: A Island NFT minted, mint 6fHK7oRnziLtqEK9FDRjh1prQ1B6o7uRMUbpkUhaYtAu
- ok: on chain: A's wallet holds the Island NFT (https://explorer.solana.com/address/6fHK7oRnziLtqEK9FDRjh1prQ1B6o7uRMUbpkUhaYtAu?cluster=devnet)
- ok: A NFT metadata served: Rogue Island: Smoke Cove A
- B claimed island Smoke Cove B (slot 17 Powder Keg)
- ok: B Island NFT minted, mint E7ZGHiesWXHUhVguoWydq88VnbuTzk9FhnMjcXcmdqMg
- ok: on chain: B's wallet holds the Island NFT (https://explorer.solana.com/address/E7ZGHiesWXHUhVguoWydq88VnbuTzk9FhnMjcXcmdqMg?cluster=devnet)
- ok: B NFT metadata served: Rogue Island: Smoke Cove B
- A collected 0 RUM (store 0, vault 0); balance 0
- ok: A voyage 4000 kills accepted, rumEarned 400
- B voyage 1500 kills accepted, rumEarned 150
- ok: A upgraded walls to level 1 for 200 RUM; RUM 400 -> 200
- ok: world list: 2 islands, 22 free slots, both A's and B's present

Transactions:
- A Island NFT mint: [4fzCFBHygX3L...](https://explorer.solana.com/tx/4fzCFBHygX3L8SCubwg5DbNDP93yqqpi6CJ2trwGJ3aNoTT2FzxoENCtShTqCAbbvszMHG5eeAyDwAjBnads8r8J?cluster=devnet)
- B Island NFT mint: [2MXtsi6s4eVC...](https://explorer.solana.com/tx/2MXtsi6s4eVCfwi2RcUAXLGJYimsRS7z8b6vtmxw3tGixsHBgJFtsdFgmrvfkV4BgwCHkFV6XpKCCi7fYvsrJ6eu?cluster=devnet)

## 3. Raid: A raids B's island with a 0.01 SOL wager, won -> loot, shield, 2x payout: PASS

- B's island: raidable true, shielded false, exposed RUM 0
- ok: raid 68689272-37e0-481d-9cb7-a0f8c2cd9593 started, fee 25 RUM, defender difficulty 1, wager memo raid:68689272-37e0-481d-9cb7-a0f8c2cd9593
- ok: raid wager funded
- ok: raid won in 6s, looted 0 RUM, A RUM 175, B shield until 2026-10-12T06:12:47.037635Z
- ok: 2x payout 0.02 SOL status sent
- ok: on chain: payout tx moves 0.02 SOL treasury -> A
- A balance 7.99999 -> 8.009985 SOL (net +0.01 minus fees)
- ok: B's island is shielded and no longer raidable
- ok: raiding again refused: 409 island_shielded

Transactions:
- A raid stake 0.01 SOL: [5hWHwHH9NSRf...](https://explorer.solana.com/tx/5hWHwHH9NSRfn4HbDmEaSFUUMRPXPoxtUPuHnZTTU2PxqoXxfo93kd3qZSsYj1Jh9fFxZWWMqHM8ahiU9gtVApjY?cluster=devnet)
- raid 2x payout to A: [3DSSn4KsCA7G...](https://explorer.solana.com/tx/3DSSn4KsCA7GTa5yQRA6VcbCFWon1p4i8ieqPMcCpnbTxaoGBXMkZ3JyKcLREtQ8GDmjiQDHexfZEwr9C81rB8cw?cluster=devnet)

## 4. Market: B lists island NFT, A buys; A lists and cancels: PASS

- ok: on chain: treasury (escrow) holds B's NFT
- ok: listing 29144204-e89d-4d17-b9d5-41db3606f271 active at 0.02 SOL
- ok: listing shown in GET /api/market
- ok: listed island locked (collect -> 409 island_listed)
- ok: listing sold
- ok: on chain: A's wallet now holds the bought Island NFT
- ok: on chain: escrow no longer holds it
- ok: B paid: 1.998507 -> 2.018507 SOL (+0.02)
- ok: island now owned by A in the API
- ok: A's listing ccdde93a-774a-4920-8f8b-2b1ec5d26828 active
- ok: listing the same island again refused (409 already_listed)
- ok: listing cancelled 
- ok: on chain: the NFT is back in A's wallet

Transactions:
- B sends Island NFT to escrow: [38d3GbmjUjjC...](https://explorer.solana.com/tx/38d3GbmjUjjCiskXdD6iSyHXZkHmF2Ds6vseNEEfYVcozzzUx6xei915hdT4GQNNuhdFXpYrAERae8iNBPyAhNk2?cluster=devnet)
- A pays 0.02 SOL (memo buy:<id>): [5ipr8mMcqKAk...](https://explorer.solana.com/tx/5ipr8mMcqKAky7pfPrpuM1ggMSSBggPQEevNLcTvusNRqoF7DavuGTeomEaFj9JACh9DMrs1zGBLvR6SntpUCeDR?cluster=devnet)
- NFT escrow -> A: [4RnNj4AMFWoB...](https://explorer.solana.com/tx/4RnNj4AMFWoBjbP1nPueMW2jiBe3PM7Cvxa6Fk7QetszUNy5htfbLgDLg2kHCoosZsuFLQBJ96ngiD9eP1y56tNB?cluster=devnet)
- seller payout to B: [5HZjSaNx5XYs...](https://explorer.solana.com/tx/5HZjSaNx5XYsCob3sRsn3A9mbqz9F6MfUGsaHJu1oJdwRkVdTajRoJnEvyhuymiqcKgRcZ46wuXqB6D56RPwUKta?cluster=devnet)
- A sends the NFT to escrow: [5HSuMLZ5xuYD...](https://explorer.solana.com/tx/5HSuMLZ5xuYDRPuk5uJ7D5hU8RfzvEUJs1eLk5f6HCGkwTSWLvZpX2aFNzRcHreaJTuVbDgEkoXpNs61eQ9bjrFA?cluster=devnet)
- NFT escrow -> A (cancel): [5kipZ5oouz5L...](https://explorer.solana.com/tx/5kipZ5oouz5LSCXvGnXYfEnoCpKXmtjr1xkn9XZZPyxNmnTSGRmewgY4pdh3Xt8iS7TijTMz4dQ87X8fm75MzhYw?cluster=devnet)

## 5. 1v1 wager vs the house: stake, deposit, funded, close lobby -> refund: PASS

- ok: lobby 76489d43-0cb2-415d-9a16-f8e53788d341 staked 0.01 SOL, memo wager:76489d43-0cb2-415d-9a16-f8e53788d341
- ok: practice bot seated; the house matches the stake
- ok: wager status funded, funded true, pot 20000000
- ok: lobby closed
- ok: wager status refunded, result refunded
- ok: refund payout 0.01 SOL sent
- ok: on chain: refund tx moves 0.01 SOL treasury -> A
- A balance 7.97997 -> 7.98997 SOL

Transactions:
- A wager stake 0.01 SOL (memo wager:<lobbyId>): [tBFVojxTLqXM...](https://explorer.solana.com/tx/tBFVojxTLqXMQrMZTZY5Mv9ZzewENVYUm3HNpJpSHBHCwsM9muodRj1FajQ8gxk3B4RSWUZeX5FhqwBZArEmMtK?cluster=devnet)
- wager refund to A: [5xKXvS4eBURE...](https://explorer.solana.com/tx/5xKXvS4eBUREKBiKUGioywjMbwtw4CP3pPDzYtDFbCBADrhst9b1r44GJYHMEyye9LxQQCD8h95CnzxRpN4JordM?cluster=devnet)

## 6. Weekly kill rewards: dry run, real run (current week), public view: PASS

- week 2026-10-12: alreadyRecorded false, 2 winners (dry run)
-   #1 Captain DcUm DcUm...cBkr 4000 kills -> 0.5 SOL
-   #2 Captain 3cm6 3cm6...irGB 1500 kills -> 0.3 SOL
- ok: A is among the dry-run winners
-   #1 DcUm...cBkr: 0.5 SOL sent
-   #2 3cm6...irGB: 0.3 SOL sent
- ok: week recorded
- ok: all 2 reward payouts sent
- ok: on chain: A's reward transfer confirmed
- ok: a second real run pays nothing new (same payouts)
- leaderboard top: Captain DcUm 4000 (DcUm...cBkr, reward 0.5), Captain 3cm6 1500 (3cm6...irGB, reward 0.3)

Transactions:
- weekly reward #1 to DcUm...cBkr: [4D8ZyhNELm8w...](https://explorer.solana.com/tx/4D8ZyhNELm8ws6H54iqEgGy8W8Fyo673162wQ8RZj6PmxuUSJvuDharG5BxFAw6d4tQmHsfZnkddWbBRfBgufgsZ?cluster=devnet)
- weekly reward #2 to 3cm6...irGB: [4jAroDRctD3g...](https://explorer.solana.com/tx/4jAroDRctD3g2JvpXxwaR8Qznv7D3mLrAznagQaJnbx5HGTV6DdmMpRMHpf6fj76RJGhBqrAaHEsXyz5yjrqbZ5s?cluster=devnet)

## 7. Daily island race: race, entry, board: PASS

- race 2026-10-12: seed 1614732695, map saltwind, target 100 kills, 0 entries
- ok: wrong seed refused (400 seed_mismatch)
- ok: A entry accepted: rank 1, best 187.4s, improved true
- ok: board has A and B (2 total); me rank 1

## 8. Final balances: PASS

- A 7.99999 -> 8.48997 SOL (+0.48998)
- B 2 -> 2.318507 SOL (+0.318507)
- treasury 24.464395 -> 23.61877 SOL (-0.845625)
- A payouts recorded by the API: 3
