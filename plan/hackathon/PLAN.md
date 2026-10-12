# Hackathon demo plan (12 Oct 2026, due 18:00 IST)

Branch `hackathon-demo` (from `v5-live-game` at the M9c pause, tag `v5-m9c-s1`). After the submission we return to
`plan/v5/V5_PLAN.md`; nothing here changes the V5 plan. Targets: desktop WebGL and an Android APK. Solana **devnet**
only. The public build is made at the very end (session downloads off then).

## Owner decisions (12 Oct)

- Web3 mode only for the demo: the wallet address is the primary profile key; email is optional.
- Unity wallet SDK: MagicBlock's Solana Unity SDK (`Solana.Unity-SDK`): browser wallets on WebGL, Mobile Wallet
  Adapter on Android.
- Wagers in devnet SOL only (no SKR on devnet). Hunt mode skipped for the demo.
- The AI captain is an AI opponent ship (named captain on the existing PvP bot): 1v1 opponent and island defender.
- Island NFTs on devnet with an in-game escrow marketplace (server escrow wallet).

## Features (demo quality: the happy path works end to end on devnet)

1. **Wallet login / sign-up** (devnet): connect a wallet, sign a one-time message (sign-in with Solana); the API checks
   the ed25519 signature and issues a session; profile keyed by the wallet address; optional email field.
2. **Islands**: claim an island (mints an Island NFT on devnet to the player's wallet), base building (a few structures
   upgraded with RUM: a RUM store that fills over time, a vault, defences), raiding another player's island (a combat
   run against the island's AI-captain defender; a win loots part of the exposed RUM; optional SOL wager on the raid).
3. **1 AI captain**: a named AI opponent ship (built on the PvP bot) for 1v1 and island defence.
4. **Daily island async race**: one seeded course per day, everyone races the same seed; best time on a daily board
   (run checks in shadow mode as today).
5. **Weekly kill leaderboard with devnet SOL rewards**: the existing weekly board; the server pays the top places from
   the devnet treasury at the week's end (and an owner-triggered payout for the demo).
6. **1v1 PvP with SOL wagers**: both players deposit the stake to the escrow wallet (wallet-signed transfer, verified
   by the server on devnet) before the match; the server pays the winner (minus nothing for the demo); refund on
   cancel. Vs the AI captain also works (the house matches the stake).
7. **Island NFT marketplace**: list your island NFT (it moves to escrow), others buy with devnet SOL, the server swaps;
   cancel returns it.

## Architecture

- **Server** (`server/RoguePirates.Api`, .NET 10): `SolanaService` on Solnet (devnet RPC): signature verify, deposit
  verification (`getTransaction`), payouts and NFT mint/transfer from a devnet treasury/escrow keypair kept only in a
  Railway variable (never in the repo). New tables via EF migrations (both providers). Deploy with `server/deploy.ps1`.
- **Unity**: `Solana.Unity-SDK` package; a `Web3Session` in a new `Assets/_Game/Hackathon/` assembly behind a
  `HACKATHON` path so V4/V5 code stays untouched where possible; menu entries for Islands, Market, Daily Race, 1v1
  wager, Rewards.
- One Unity Editor (RAM is the limit), so Unity work runs one agent at a time; server work runs in parallel.

## Schedule (IST)

| Time | Server | Unity |
|---|---|---|
| 07:10-09:30 | S-A: schema for every feature, wallet auth, SolanaService, treasury, deposits/payouts | U-1: SDK install, wallet login WebGL + Android, session, profile |
| 09:30-13:00 | S-B: 1v1 wagers, weekly rewards, daily race, islands, raids, NFT mint, marketplace | U-2: 1v1 wager flow, AI captain, daily race, rewards panel |
| 13:00-16:00 | fixes, deploy | U-3: islands screen (claim, build, raid), marketplace |
| 16:00-17:30 | deploy, devnet end-to-end | WebGL desktop build + Android APK, end-to-end test, owner test |
| 17:30-18:00 | buffer | buffer |

## Risks

Scope is very large for one day; every feature is built to its happy path. Devnet faucet limits (the treasury needs
devnet SOL); public devnet RPC rate limits; wallet behaviour differences between WebGL and Android (MWA); one Unity
Editor for all Unity work.
