# Your answers and explanations (30 Sep 2026)

All three rounds of your answers to the V5 questions, what each changes, and explanations with costs. Research behind them:
`plan/v5/research/` (PRICING_RESEARCH, HUNT_NETCODE, DEVICE_TARGETS, MEGABONK_LEVELS, STACK_RESEARCH). Prices are for
about 100 monthly players on Solana devnet.

## Decided

| # | Your answer | What changes in the plan |
|---|---|---|
| Q1 | Stage order as planned | Voyage (with mobile and the shop) → accounts and chain → island → social → real-time → creator |
| Q3 | Custodial keys on our Railway server and Postgres | Encrypted keys in Postgres, master key in a Railway secret; no wallet provider |
| Q5 | Fixed token prices | Store prices per token are config values |
| Q6 | 5% royalty only | No other marketplace fee |
| Q7 | An island NFT is a plot | One plot is your active home; a sale never moves the treasury or vault |
| Q9 | 6 AI raids a day at full loot | Your AI captain can raid 6 times a day and each raid pays like one you play |
| Q10 | No cap or shrinking on PvP RUM | Unwagered Arena and Hunt pay a fixed RUM amount per match, plus a skill rating |
| Q11 | Bounty split top 10 on a curve | 30/20/12/9/7/6/5/4/4/3 % |
| Q12 | Sign in with Google plus email codes | No passwords; $0 at this size |
| Q13 | No sail moderation on devnet | A report flag only; review before any mainnet |
| Q15 | Decorations are cosmetic only | No gameplay effect |
| Q16 | One weekly board across voyage maps, adaptive | Counts your best run on any map as maps are released |
| Q17 | Railway only, when needed | Web build, server and database, and match server all on Railway, each from the phase that needs it |
| Q18 | Keep 12 levels; evolution at level 8 plus multi-level rare cards | A weapon evolves at level 8 with its item and keeps levelling to 12; Rare cards give +2, Legendary +3 |
| Q19 | Anchor Flail, Chainshot, Siren Bell + Parrot Squadron, Maelstrom Charge, Lighthouse Lantern, Ramming Prow, Cursed Doubloons, Monkey Gunners | Nine new weapons in M4 and M4b; Ghost Cannons dropped |
| Q20 | Monte Carlo, written new on V4; personalities defined now | A new simulator (no V3 code); ten personalities in `PLAYER_PERSONALITIES.md` |
| Q21 | One enemy cap a low-end phone handles | The same cap everywhere, set on the reference phone (Q31) |
| Q22 | Endless overtime ramp | As today; overtime Rum and leftover gold are already capped |
| Q24 | RUM items never become tokens; web3 players still earn and spend RUM | Everyone earns RUM; web3 players also use tokens |
| Q25 | Tokens buy cosmetics and RUM-equivalent unlocks | Nothing tokens buy is out of reach with RUM |
| Q26 | Reversals confirmed | PvP, token wagers and winnings, token balances, NFTs and a marketplace, the Arena draft |
| Q27 | No anti-collusion rules for now | Switches kept, off, each tested; revisited in the M28 audit |
| Q28 | Hunt wager winner takes all | Pot minus 5% to the last ship afloat |
| Q29 | Exact replays only with Photon Quantum, otherwise plausibility checks | Voyages are checked for plausibility; raids, races, wagers, ranked runs and bounties are played on our match server (M13b, moved up from Stage E) |
| Q32 | No key export for now | Players move assets to their own wallet instead |
| Q33 | You keep the master-key backup | Offline backup with you |
| Q2 | Unity Netcode for GameObjects | Free; we write "only send nearby enemies" and lag compensation ourselves (budgeted in M13b) |
| Q23b | Vault 80% / treasury 20%, raid takes 20%, 8 h shield, max 10% lost a day, 72 h for new players | Built in M14 |
| Q31 | Galaxy A15 / A16 4G at a steady 30 fps; floor Galaxy A06 at 20+ fps; iPhone 11 and desktop at 60 fps | The enemy cap and memory budget are set on the A15/A16 in M1 |
| Q34 | Forfeits pay no RUM, but a genuine short match pays | Whoever surrenders, leaves, doesn't reconnect or idles 30 s gets nothing; if the others leave within 60 s nobody is paid (stops instant-forfeit farming), after 60 s the player left is paid; a match that ends in under 60 s by real combat (players, PvE, the storm) pays in full |
| Q35 | Checked voyage RUM usable right away on devnet | The M28 audit revisits it before anything has value |

## What we use Railway for, and what it costs

Railway bills only for what runs: **$20 per vCPU per month** and **$10 per GB of memory per month**, metered by the
minute, plus **$0.05 per GB** of data sent out ([Railway plans](https://docs.railway.com/pricing/plans)). The Hobby plan
is $5 a month and includes the first $5 of usage, so the bill is $5 or your usage, whichever is higher. A service in
**Serverless mode** sleeps after 5–10 minutes without traffic and costs nothing while asleep
([Serverless](https://docs.railway.com/reference/app-sleeping)).

We'd run four things on it:

| Service | What it does | From | How it runs | Monthly cost |
|---|---|---|---|---|
| **Web build** | Serves the game files to browsers | Whenever you want testers | A tiny file server, asleep when idle; ~25 MB per fresh download | ~$1–2 |
| **Game API** (+ the run checker inside it) | Accounts, RUM, store, raids, leaderboard, wallets | M10 | Always on, small (~0.05 vCPU, 256 MB) | ~$3–5 |
| **Postgres** | The database | M10 | Always on, small (256 MB, 1 GB of storage) | ~$3–4 |
| **Match server** | Plays raids, races, ranked runs, bounties, Arena and Hunt on the server, and runs AI raids | M13b | Only while sessions run, asleep otherwise | ~$10–33 (below) |

**The match server is the part that grows with players.** A session is one raid, race, ranked voyage or match played
on the server. Assumptions, to be replaced by measurements in M9: a session uses about 0.25 vCPU and 300 MB while it
runs; an AI raid runs headless at 5× speed, about 1 vCPU-minute; each daily player plays about 35 minutes of server
sessions a day (2 raids, 1 race attempt, 1 ranked voyage, a share of an Arena and a Hunt match); about 10 KB/s of
traffic per player.

| Scenario | Server session time | AI raids | Match server | **Railway bill in total** |
|---|---|---|---|---|
| Before M10 (web build only) | — | — | — | **$5** (the minimum) |
| M10–M13 (API and database, no match server) | — | — | — | **~$8–11** |
| 100 players a month, ~30 of them playing on a given day | ~1,050 min/day | 50 captains × 6 a day | ~$11–15 | **~$20–25** |
| All 100 players playing every day | ~3,500 min/day | 100 captains × 6 a day | ~$30–33 | **~$40–45** |

The biggest unknown is how much CPU one server session really uses: if it's double my guess, the match server costs
double. M9 measures it before anything depends on it. (My earlier "$30–60" assumed every player plays daily and
mostly on the server; this breakdown replaces it.)

**Where we can save**, largest first:

| Saving | How | Saves (busy case) | Trade-off |
|---|---|---|---|
| Resolve AI raids with the balance simulator | Your offline AI captain's raid is calculated by the fast simulator (M2) instead of a full Unity match | ~$9/month | The outcome is a close estimate, not a played match (fine when nobody is watching) |
| Lower server simulation rate | Enemy AI thinks 30 times a second instead of 60; the player's ship stays smooth | ~$3–5/month | Needs a feel check in the playtest |
| Several sessions per server process | One Unity process hosts several sessions and shares memory | ~$3–5/month | More engineering in M13b |
| Serverless sleeping (already assumed above) | Web build and match server sleep when idle | Already in the numbers | Railway promises only "a small delay" to wake up; M9 measures the wake-up time for a headless Unity server |
| Web build on free static hosting | Cloudflare Pages, GitHub Pages or itch.io instead of Railway | ~$1–2/month | Not "Railway only" |
| A spend limit | Railway's usage alerts and hard limit | Caps surprises | Services stop if the limit is hit |

With the first three (AI raids by simulator, a lower server simulation rate, several sessions per process), the busy
case drops to about **$26–33 a month** and the quiet case to about **$14–20**. Casual voyages already stay in the browser
and only ranked attempts run on the server, so that is not an extra saving. None of
them needs deciding now: they come up when M2, M13b and M18 are detailed.

## A few of your answers have a trade-off

- **PvP RUM with no cap (Q10) and 6 full-loot AI raids (Q9).** With anti-collusion off (Q27), two accounts could trade
  wins to create RUM. The forfeit rule (Q34) stops the instant version; the M28 audit can switch the collusion rules on
  before anything has value.
- **Server-played sessions (Q29).** Raids, races, ranked runs and bounties are played on our match server. They need a
  steady connection and they are the main hosting cost (above).

## Q2 — Can each option handle the 4-player Hunt? (you chose Netcode for GameObjects)

The Hunt has 4 players on one sea. Our server simulates about 200 enemies, and each player sees only the 60–80 near
them. Enemies go out as compact batched updates (position, heading, HP, about 8 bytes each), only for nearby ones: about
**6–17 KB per second per player**, roughly $0.01 of Railway traffic per match-hour.

| Option | Handles the Hunt? | How players connect | Main risk | On Railway |
|---|---|---|---|---|
| **Unity Netcode for GameObjects (chosen)** | **Yes** | Straight to our server (secure WebSockets) | We write "only send what's near" and lag compensation ourselves; pin a tested 2.x version (3.0 is brand new) | **Yes** |
| Fish-Net + Bayou | Yes; built-in "only send what's near" | Straight to our server | Small team; lag compensation needs Pro ($60 once) | Yes |
| Photon Fusion 2 (Server Mode) | Yes, with enemies grouped by sea sector | Through Photon's relay | Photon calls this "not recommended" for browsers | Likely, untested |
| Photon Quantum 3 | Hard | Through Photon | Every phone simulates all enemies; a full rewrite; map hacks | Partly |
| Nakama | Yes, after rewriting enemy AI in Go | Straight to Nakama | Two copies of the gameplay | Likely |

Netcode for GameObjects costs nothing and is Unity's own. The extra work (interest management and lag compensation) is
budgeted in M13b. M9 measures it with real browser clients, including the reference phone, before anything is built on
it. Because Quantum is out, runs use plausibility checks plus server-played sessions (Q29).

## Q31 — The reference phone (confirmed)

| Role | Phone | Target |
|---|---|---|
| **Reference low-end Android** | **Samsung Galaxy A15 or A16 4G** (Helio G99, 4 GB; about 70–80% of players have this or better) | **A steady 30 fps** (95% of frames on time over 15 minutes) |
| Floor Android | Samsung Galaxy A06 or Redmi 14C (Helio G85/G81, 4 GB) | Playable on Low, never below 20 fps |
| Reference iPhone | iPhone 11 / SE 2nd gen | 60 fps |
| Mid-range phones and desktop | — | 60 fps, quality picked automatically |

What it means: about 60–80 enemies at once (today's cap of 80 is at the top); a hard **256 MB** memory limit for mobile
browsers (32-bit Chrome), where today's build allows 2 GB; 100–150 draw calls; a 20–30 MB first download. **You need
the phone** for M1's measurement: a Galaxy A15 or A16 4G (about $130–150; any Helio G99 phone such as a Galaxy A24
works).

## Q30 — The accounts to create (final)

You create them and wire them in, in the order the phases need them. Keys go into Railway variables (or a local `.env`
that is never committed), never into chat.

| # | Account | Needed from | What to set up | Cost |
|---|---|---|---|---|
| 1 | Railway | M9 (netcode test), then M10 | Hobby plan; one project; Serverless mode on the web build and match server; a usage alert | $5 minimum; ~$8–11 from M10; ~$20–45 from M13b (breakdown above) |
| 2 | A domain you own | M10b | For the game's web address and the email sender (for example `roguepirates.gg`) | ~$10–40/year |
| 3 | Google Cloud project | M10b | An OAuth "Web client" for Sign in with Google; add the game's web address as an allowed origin; publish the consent screen (basic profile only, no review needed) | $0 |
| 4 | Resend | M10b | Add and verify your domain (DNS records); create an API key | $0 (3,000 emails/month) |
| 5 | Helius | M11 | Free plan; a devnet RPC key | $0 |
| 6 | Firebase | M19 | A project on the free Spark plan; a web app; a web-push (VAPID) key | $0 |

No Photon, Fish-Net, Edgegap, wallet provider or other hosting is needed. I'll give the exact variable names and
settings for each when its phase starts.

## Round 1 explanations (kept for reference)

- **AI raids (Q9):** the AI captain raids other islands while you're offline; you chose 6 a day, each paying like a raid
  you play.
- **Sign-in (Q12):** Google sign-in is one button checked by our own server; the email code is a 6-digit code sent by
  Resend; passwords would still need emails for verification and resets, so they save nothing.
- **Megabonk (Q18):** weapons go to level 40 with random upgrades scaled by rarity, and there are no evolutions. Players
  seem to reach about level 25–40 in total by 10:00 (a community estimate), so no weapon nears 40 in one stage.
- **Monte Carlo (Q20):** a simplified model of a run in plain code, played thousands of times with random choices by
  different personalities, in minutes of computer time. It is written new on V4 and checked against the real game.
- **Replay verification (Q29):** checking a run by replaying the player's inputs on the server. That needs exact
  determinism, which only Quantum's rewritten simulation would give; so we use plausibility checks and server-played
  sessions instead.
- **Treasury (Q23b, decided):** vault 80% (never raidable), treasury 20%; a raid takes 20% of the treasury; an 8-hour
  shield after any raid; at most 10% of your RUM lost per day; 72 hours for new players. Example: with 1,000 RUM, a raid
  takes 40 and the worst possible day costs 100.
