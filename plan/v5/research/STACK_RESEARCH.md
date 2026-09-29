# Web3, accounts and multiplayer stack — research for V5 M8–M11, M19–M24

Collected 29 Sep 2026 by a research agent (web plus live devnet/mainnet RPC queries). Items it could not confirm are
marked (unverified).

## Findings that change the plan

1. **MagicBlock BOLT ECS is deprecated** — build on plain Anchor plus the Ephemeral Rollups SDK.
   https://github.com/magicblock-labs/bolt
2. **Unity Multiplay hosting ended 31 Mar 2026; Hathora game hosting ended May 2026.** Edgegap (WebGL over wss, free
   tier) and Gameye remain. https://status.unity.com/info_notices/362941 · https://edgegap.com/gaming/webGL-html5
3. **SKR is not on devnet** (mainnet mint `SKRbvo6Gf7GondiT3BbTfuRDPqLWei4j2Qy2NPGZhW3`, 6 decimals). **ORE's devnet
   mint** (`oreoU2P8bN6jkk3jbaiVxYnG1dCXcYxwhwyK9jSybcp`, 11 decimals) exists but its mint authority is a PDA we cannot
   use. → game-owned devnet stand-in mints (classic SPL Token, same decimals), mint addresses in config.

## MagicBlock

- Ephemeral Rollups: delegate an account to `DELeGGvXpWV2fqJUhqcF5ZSYMS4JTLjteaAMARRSaeSh`, run transactions on the ER
  (sub-50 ms, 0 SOL), commit and undelegate. Anchor: `ephemeral-rollups-sdk` v0.17.3 with `#[ephemeral]`, `#[delegate]`,
  `#[commit]`. Devnet router `https://devnet-router.magicblock.app`. Session cost 0.0003 SOL + 0.0001 per extra commit.
  https://docs.magicblock.gg/pages/ephemeral-rollups-ers/how-to-guide/quickstart
- VRF: `ephemeral-vrf-sdk` 0.3.x, request with a caller seed and a callback that checks `VRF_PROGRAM_IDENTITY`; free on
  the ER, 0.0005–0.0008 SOL on the base layer. https://github.com/magicblock-labs/solana-vrf
- No official C# ER SDK; the ER is a Solana RPC endpoint, so our .NET server can send to the router (inference).
- Game pattern: create on Solana, delegate for the session, commit and undelegate at the end; keep token escrow on the
  base layer. https://docs.magicblock.gg/pages/get-started/use-cases/games

## Solana Unity SDK

- `package.json` 1.3.0 on main; last tag v1.2.9 (Sep 2024) — pin a commit. WebGL wallet adapter for extension wallets;
  Android Mobile Wallet Adapter; WalletConnect via Reown; Phantom/Solflare deeplinks.
  https://github.com/magicblock-labs/Solana.Unity-SDK
- Extension wallets do not inject in mobile browsers (use the wallet's in-app browser or deeplinks); MWA on the web works
  only in Android Chrome. https://docs.solanamobile.com/developers/mobile-wallet-adapter-web
- Web3Auth is now MetaMask Embedded Wallets; its Unity SDK lists iOS and Android, not WebGL.

## Custodial wallets at email sign-up

| Option | Devnet | Email OTP | Export | Notes |
|---|---|---|---|---|
| Privy | Yes | Yes | Seed export | Server wallets; Unity SDK; free to 499 MAU, then $299/mo to 2,500. No Solana gas sponsorship (we pay fees ourselves). https://docs.privy.io/wallets/overview/chains |
| Turnkey | Yes | Yes | Mnemonic export | Enclave signing, Solana transaction parsing; 25 free signatures then $0.10 each (Pro $99/mo + $0.05). https://docs.turnkey.com/networks/solana |
| Coinbase CDP server wallets | Yes | No | Not documented | Solana sponsorship enterprise-only preview |
| Crossmint | Yes | Email signer | Limited | Default is a smart wallet (program address) |
| Magic.link / Dynamic | Yes | Yes | Yes | Alternatives |
| Own keypairs in a KMS | Yes | We build it | We build it | Most liability; AWS KMS supports Ed25519 |

- Fee sponsorship is native (`feePayer`); Kora is the Solana Foundation's fee relayer.
  https://solana.com/docs/tools/kora/getting-started — whitelist our program ids and instructions or the sponsor can be
  drained through rent and fees.

## NFTs and marketplace

- Metaplex Core Royalties plugin: 500 bps, up to 5 creators, rule set None / allow-list / deny-list; only an allow-list
  enforces payment, and it must include Tensor's and Magic Eden's programs or they cannot trade the asset.
  https://metaplex.com/docs/core/plugins/royalties
- Core mint ≈ 0.003 SOL; Bubblegum v2 compressed NFTs for bulk cosmetics (16k tree ≈ 0.34 SOL).
- In-game listings: our Anchor program with Core Freeze Delegate + Transfer Delegate (no escrow, buy pays 5% royalty), or
  Tensor's open-source marketplace program on devnet `TCMPhJdwDryooaGtiocG1u3xcYbRpiJzb283XfCZsDp`.

## Wagers

- Anchor 1.0.0 (Apr 2026) escrow pattern: create match PDA + vault, join (deposit), `settle(winner)` signed by the server
  oracle (5% treasury, 95% winner), permissionless refund after timeout. Use `token_interface` for SPL and Token-2022.
- Legal: US skill contests allowed federally but restricted in several states (Triumph excludes AZ, CT, DE, LA, ME, MI,
  MT, NV, SD, TN); mainnet needs geo-fencing, age/KYC and counsel. Devnet tokens have no value.
  https://docs.triumpharcade.com/frequently-asked-questions-regulatory-and-legal

## Invites, push, share cards

- Solana Actions/Blinks work on devnet but need Dialect registration to unfurl on X and are weak for discovery; use https
  invite links with OG images first, an Actions endpoint second. https://solana.com/docs/tools/actions
- Web push: service worker + FCM on desktop and Android; iOS only for installed PWAs (16.4+).
- Share cards: server-rendered PNG (SkiaSharp/ImageSharp in ASP.NET) or a client RenderTexture upload.

## Real-time multiplayer in WebGL (server authority required for wagers)

- NGO + Unity Transport WebSockets (wss) with a headless Linux server build; Relay has no authority; Distributed
  Authority is client-owned (unsuitable).
- Photon Fusion 2 on WebGL recommends Shared Mode (client-authoritative) — poor fit for money matches.
- Fish-Net + Bayou and Mirror + SimpleWebTransport: server-authoritative, Edgegap WebGL samples.
- Nakama: authoritative matches in Go/TS/Lua plus leaderboards, friends, notifications, tournaments; self-hostable.
- WebSockets are TCP: interest-managed, quantized delta snapshots are required for hundreds of enemies.

## Recommended stack (small team)

> Superseded on 30 Sep 2026 by the owner's answers (`plan/v5/ANSWERS.md`): custody on our own Railway server
> (no Privy or Turnkey, no key export), Railway-only hosting (no Edgegap), and Fish-Net + Bayou suggested for netcode
> (`HUNT_NETCODE.md`). Kept for reference.

Privy (email OTP + wallets + export) or Turnkey; our backend or Kora as fee payer with an allow-list; one Anchor 1.0
program (match records, escrow, listings); Metaplex Core with 5% royalties and an allow-list incl. Tensor and Magic Eden;
MagicBlock VRF for seeds and ER for match records only; headless Unity server with NGO/UTP WebSockets or Fish-Net + Bayou,
hosted on Edgegap when approved; https invite links + OG cards; FCM web push.
