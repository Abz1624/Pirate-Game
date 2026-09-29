# Service pricing at ~100 monthly players (devnet) — research, 30 Sep 2026

Collected by a research agent from the vendors' pricing pages (opened unless marked). Assumptions: ~100 monthly active
users, 5–15 peak concurrent, ~8 sessions per user per month of ~12 minutes, real-time matches of 2–4 players.

## Real-time netcode

| Option | Licence / plan | Cost at ~100 MAU | Notes |
|---|---|---|---|
| Photon Fusion 2 | Free Launch plan: 100 CCU, 0.3 TB/month, commercial use, one app ([pricing](https://www.photonengine.com/fusion/pricing)); $95/12 months for 100 CCU; $125/month for 500 CCU | $0 | Photon recommends Shared Mode (client authority) on WebGL. Server Mode with WebGL clients works when enabled in `NetworkProjectConfig`; all WebGL traffic goes through Photon's relay. Photon does not host game servers: we host a headless server (Edgegap, a VPS). The "WebGL clients + our dedicated server" setup is not explicitly documented (unverified). |
| Photon Quantum 3 | Same price list | $0 client-side; server validation needs Photon Enterprise (custom price) | Deterministic predict/rollback; gameplay must be rewritten in Quantum's ECS with fixed-point math. |
| PUN 2 | Maintenance mode | — | Photon points new projects to Fusion or Quantum. |
| Unity Netcode for GameObjects + Unity Transport | Free packages; Relay free to 50 average CCU, then $0.16/CCU ([UGS pricing](https://unity.com/products/gaming-services/pricing)) | $0 | WebGL clients connect over WebSockets to a Linux headless server. No built-in lag compensation. |
| Fish-Net + Bayou | Free for commercial use (custom licence); Bayou MIT; Fish-Net Pro $10 + $2/month or $60 lifetime (lag compensation, code stripping) | $0–60 once | Server-authoritative; Edgegap WebGL samples. |
| Nakama | Open source self-hosted (a VM); Heroic Cloud price not shown (Satori from $600/month) | €5.49–$12/month VM | Match logic in Go/TypeScript/Lua (a port of our gameplay). Also gives leaderboards, friends, notifications. |

Game-server hosting: Unity Multiplay closed 31 Mar 2026; Hathora closed 5 May 2026. [Edgegap](https://edgegap.com/pricing):
free tier (1.5 vCPU, 60-minute deployments), then $0.00115 per vCPU-minute, egress $0.10/GB — about $4–5/month at this
scale. [Gameye](https://gameye.com/pricing/): $0.07 per vCPU-hour, egress included, ~$4/month. A VPS: Hetzner CX23
€5.49/month, DigitalOcean 2 GB $12/month.

## Backend hosting

[Railway](https://railway.com/pricing): Hobby $5/month (includes $5 usage), Pro $20/month; CPU ~$20 per vCPU-month, RAM
~$10 per GB-month, egress $0.05/GB. Postgres is an ordinary service (~$1–3/month idle, unverified). WebSockets are
supported with no timeout (reconnect on redeploys); TCP proxy yes; **no inbound UDP** (fine for WebGL). Realistic: $8–15
per month for the API + Postgres (+ a small headless game server).

## Sign-in and email

| Option | Cost at ~100 MAU | Notes |
|---|---|---|
| Sign in with Google (Google Identity Services, our server verifies the ID token) | $0 | Basic scopes need no Google verification; while the OAuth app is in "Testing" it is limited to 100 test users |
| Firebase Auth (Spark) | $0 | Google sign-in free; email-link sign-in only 5 emails/day on the free plan |
| Supabase Auth free | $0 | Project pauses after a week of inactivity |
| Auth0 free | $0 | 25k MAU |
| Privy | $0 under 500 MAU | Email OTP, Google, wallets; server wallets listed as an Enterprise add-on (unverified) |
| [Resend](https://resend.com/pricing) | $0 (3,000 emails/month, 100/day) | Pro $20/month for 50k |
| [Postmark](https://postmarkapp.com/pricing) | $0 (100/month) | $15/month for 10k |
| [Amazon SES](https://aws.amazon.com/ses/pricing/) | ~$0.01 | $0.10 per 1,000 |
| SendGrid | no free plan since mid-2025 | |

## Push, RPC, wallets

- Firebase Cloud Messaging (web push) and the Spark plan: $0 ([pricing](https://firebase.google.com/pricing)).
- Solana devnet RPC: public endpoint rate-limited per IP (100 requests / 10 s) and "not for production"; Helius Free
  (1M credits/month, 10 RPS) $0; QuickNode free only for one month; Triton no free tier.
- MagicBlock devnet endpoints: free.
- Custodial keys: self-hosted on our server $0 (we carry the custody risk); Privy $0 under 500 MAU; Turnkey
  ~$0.10 per signature (~$97/month at 1,000 signatures).

**Realistic total on devnet at ~100 MAU: about $5–20 per month** (Railway Hobby plus a small game server).
