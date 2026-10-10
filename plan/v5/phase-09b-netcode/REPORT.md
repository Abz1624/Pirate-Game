# M9b — D9b.1 single-ship measurements (report)

Branch `v5-live-game`. Plan item D9b.1 (`plan/v5/V5_PLAN.md`, M9b). Method and evidence files: `README.md` in this folder.
**Final, 11 Oct**: the Railway column is filled from the scripted voyages against the `match-test` service
(`railway/*/client.json`), the owner's sessions, the server's own `[V5 Net]` lines for every deployment from 9 to 11 Oct
(`railway/server-logs/match-test-v5net.log`, from `railway logs --service match-test --json <deployment>`) and Railway's
metrics (`railway/server-logs/match-test-metrics.txt`). Every path below is relative to this folder.

Note: commit `1c84ad2f` replaced this file's earlier content with its own section by mistake; the earlier sections (from
`e01a2e87`) are restored below the summary, unchanged except for the Railway column and the sizes and cost filled in.

## Summary against D9b.1

Real link: the scripted client and the owner played from India to Railway's edge, about 270 ms round trip (input delay
p50 266.7-300 ms in `railway/{tcpproxy-ws-90s-drop,wss-v5-60s,wss-1c84-60s,wss-redeploy}/client.json`).

| Measure | Target | Railway result | Verdict | Evidence |
|---|---|---|---|---|
| Down / up per client (payload) | measured (M9c budget at 4 ships: down <= 20 KB/s, p95 <= 30; up <= 2 KB/s) | **2,696-3,492 / 845-873 B/s** (wire estimate 3,041-3,843 / 1,185-1,224) over 6 scripted voyages of 27-103 s. Ship, enemies, level-ups and inputs only: shots, pickups, chests and the minimap's enemies are not mirrored (D26 c), so this is M9c's lower bound | measured | `railway/{tcpproxy-ws-90s-drop,wss-90s-drop,wss-v4-60s,wss-v5-60s,wss-1c84-60s,wss-redeploy}/client.json`, `clients[0]` |
| Server tick p95 | <= 8 ms | per voyage **0.42-1.96 ms** (p50 0.18-0.95; 11 voyages); worst 10 s window while connected 3.82 ms (the first window after a join, while the stage loads); single-tick max per voyage 11.3-36.3 ms; server frame rate 59-60 while connected | **met** | `railway/server-logs/match-test-v5net.log` (`final` and `session=connected` `stats` lines); `railway/owner-playtest-server.log` |
| Client fps, desktop WebGL | steady 60 | 960x600 canvas (the page's default): **58.0 fps**, 1% low 58.8 (local Chrome, current client); 59.7 in the part 2b run. 1920x1080 full screen on an integrated GPU: **42.0 fps** here, about 40 on the owner's PC (98% of frames over 20 ms); the normal game at that canvas runs 15.0-17.1 fps, so this is fill rate, not the net client. The link does not change the client's frame work | met at the page's canvas; not at full-screen 1080p on integrated graphics (the normal game is worse there) | `owner-retest-fixes/webgl/after-chrome-tab-hides-canvas-960x600.log`, `owner-retest-fixes/webgl/after-net-client-canvas-1920x1080.log`, `owner-retest-fixes/webgl/normal-game-bench-canvas-1920x1080.log`; `webgl/webgl-client-report.json`; `railway/owner-retest/notes.md` |
| Prediction error and largest single-frame correction | <= 0.5 m at 150 ms RTT and 2% loss | 150 ms / 2% (in-process, the gate's setting): **0 / 0 m** sailing, boosting and through a level-up. Railway at ~270 ms, protocol 4 and 5 scripted runs: error max **0.001-0.006 m**, correction max **0.003-0.086 m**. **Known exception, the enemy ram**: the server's knockback is not predicted, so contact pulls the ship back 1.0-2.6 m in one correction (local Chrome at 270 ms, ship alive: up to 2.57 m; Railway 9 Oct protocol 3 run: 3.10 m and 2.05 m, outside any pause). Tracked in `plan/v5/TODO-FOR-YOU.md` (the owner asked, 11 Oct) | **met** for sailing, boosting and level-ups; ram knockback open | `inprocess-review/{latency,boost,levelup}-150ms-2pct.json`; `railway/*/client.json` (`predictionErrorMetres`, `correctionMetres`); `railway/wss-90s-drop/client.log:114-115`; `enemy-stutter-fix/webgl/after-fix-270ms-voyage4.log` |
| Dropped socket resume | <= 10 s | **1.37-1.97 s** (4 drops mid-voyage, 0 resyncs); the TCP-proxy run's drop came 1.5 s before the ship sank | **met** | `railway/{wss-90s-drop,wss-v4-60s,wss-v5-60s,wss-1c84-60s}/client.json`, `resumeSeconds` |
| Railway redeploy during a voyage | ends cleanly, no settlement, back to the lobby | client got `end ServerClosing` and was in the lobby **0.001 s** after the end showed; server: `closing: SIGTERM (stop file)`, final stats, clean `EXIT`; the new deployment was READY 0.74 s into its process | **met** | `railway/wss-redeploy/client.json`; `railway/server-logs/match-test-v5net.log` (deployments `9c357649` and `71ecfe49`, 10 Oct 21:20-21:22 UTC) |
| Cold start | measured | deployment created to `READY`: **6.3-8.3 s** (7 deployments, image pull included); a new process after a voyage (`EXIT` to `READY`, restart policy Always): **1.6-3.1 s**; inside the process **0.71-1.40 s**; join to welcome (scripted client) 1.40-3.72 s | measured | `railway/server-logs/match-test-metrics.txt` (deployment times), `railway/server-logs/match-test-v5net.log` (`READY`, `EXIT`); `railway/*/client.json`, `joinSeconds` |
| Memory | measured | process RSS **181-223 MB** at the end of a voyage (Mono heap 5-21 MB), 177-230 MB idle; Railway's memory metric 0.16-0.21 GB hourly mean with one deployment (0.35-0.38 GB in hours with a redeploy overlap). Unexplained: the metric reads 739 MB for the current deployment while its process reports RSS 203-206 MB | measured | `railway/server-logs/match-test-v5net.log` (`rssMB`); `railway/server-logs/match-test-metrics.txt` |
| Sessions per process | measured | **1 by design**; a fresh process after every voyage | measured (M9c: several per process) | `railway/server-logs/match-test-v5net.log` |
| WebGL size cost of NGO + UTP | measured | normal desktop build **+0.85 MB** download (113.4 to 114.2 MB; wasm +0.65 MB; stripped IL: Netcode 641 KB, Transport 211-214 KB); the net client 114.3 MB | measured | `webgl/baseline-desktop-before-m9b.txt`, `webgl/normal-desktop-now.json`, `enemy-stutter-fix/webgl/net-build.json` |
| Cost per voyage-hour on Railway | measured (D23) | about **$0.007 per voyage-hour** (one process: 0.11 vCPU and 0.22 GB, plus 14 MB of egress; section "Cost"). The whole M9b Railway test (`match-test`, 38 metered hours): about **$0.17**. The workspace usage export could not be read (`railway usage`: Unauthorized with the project token) | measured from metrics; usage export open | `railway/server-logs/match-test-metrics.txt`; rates `plan/v5/ANSWERS.md:45-46` |

Owner sessions on Railway, matched to the server log by time and events (the owner played; the server measured):

| Session (UTC) | Server build | Server tick p50 / p95 / max (ms) | RSS (MB) | End | Owner's verdict |
|---|---|---|---|---|---|
| 9 Oct 11:18 and 11:21 | protocol 3 | 0.22 / 0.42 / 36.3; 0.42 / 0.72 / 20.5 | 214.3; 213.1 | Sunk; Sunk | stutter, enemy jumps, stuck after a hidden tab (fixed: `playtest-fixes/`) |
| 9 Oct 16:59 | `e1f56626` (protocol 4) | 0.18 / 1.25 / 28.4 | 193.1 | session expired after 163.6 s away | stutter gone; stuck after a tab switch, pause after a pick, 40 fps (fixed: `owner-retest-fixes/`) |
| 10 Oct 02:22 | `e01a2e87` (protocol 5) | 0.38 / 0.91 / 11.3 | 216.2 | session expired | enemies stuttering a bit (fixed: `enemy-stutter-fix/`) |
| 10 Oct 21:07 (11 Oct 02:37 IST) | `1c84ad2f` | 0.91 / 1.41 / 16.8 | 223.1 | session expired (page closed) | **enemies smooth, tab switching fine, no pop-ins** (11 Oct); asked to track the ram jolt |

Server lines: `railway/server-logs/match-test-v5net.log` (`final` lines) and `railway/owner-playtest-server.log`. Owner's
words: `railway/owner-retest/notes.md`, `README.md` and `plan/v5/TODO-FOR-YOU.md`.

## Recommendation for M9c: GO

A single-ship voyage on an authoritative NGO server on Railway meets every numeric D9b.1 target at a real 270 ms round
trip: tick p95 under 2 ms against 8, resume under 2 s against 10, a redeploy ends cleanly, prediction within 0.1 m outside
rams, about 3.5 KB/s down, under a cent per voyage-hour, and the owner's 11 Oct re-test found it smooth. The frame-rate
shortfall is the full-screen canvas on integrated graphics, which hits the normal game harder. Go to M9c (multi-ship
runtime and the 4-ship go/no-go), carrying these risks into it:

1. **Bandwidth at 4 ships with mirroring.** 3.5 KB/s down covers one ship and its enemies only. M9c adds 3 more ships,
   shots, pickups, chests and the minimap's enemies, against 20 KB/s down (p95 30) and 2 KB/s up. Up is 0.85-0.87 KB/s
   for one ship's inputs; it should not grow with other ships, but the margin is 2.3x.
2. **UTP Host-header patch fork.** `Pirate Game Unity/Packages/com.unity.transport` is an embedded Unity Transport 2.7.4
   with a patch (`Runtime/WebSocket/WebSocket.cs`, `Runtime/Layers/WebSocketLayer.cs`) so native WebSocket clients send the
   server's hostname as Host (Railway's edge refused the resolved IP: `railway/voyage-90s-drop`). Browsers do not need it;
   the native test client does. Every UTP update means re-applying it until Unity fixes it upstream.
3. **Mono, not IL2CPP**, on the server. The tick budget is far from tight (p95 under 2 ms), but the Linux IL2CPP build is
   untried (toolchain packages; `README.md`, "Server build"), and 4 ships and 200 enemies cost more.
4. **Per-session clock.** The server still pauses through the global `Time.timeScale` for level-up cards and holds; a
   shared run needs a per-session clock and hold, cards that do not pause others, and several sessions per process (plan,
   M9c).
5. **Ram knockback not predicted** (1-2.6 m jolts at 270 ms); more contact with more ships.
6. **The server's stats list grows while idle.** `NetServerHost.StatsLine` (`Assets/_Game/V5/Net/NetServerHost.cs:152`)
   takes percentiles over every tick measured since the session began; with no session every idle tick is kept (1.72 M
   at the end of a 16 h idle process). Railway's CPU for an idle process climbed from about 0.02 to 0.11 vCPU over 16 h and
   dropped at each restart, which fits this (not confirmed). Cap the list, as the client does (`NetMetrics.AddCapped`),
   before long-running servers in M13b.
7. **Test service removed (11 Oct).** `match-test` is deleted and the API's ticket settings are removed (D23, D26 d);
   proof in `railway/cleanup-proof.md`. A later Railway test adds them again (README, Railway steps).

## Targets (D9b.1)

| Measure | Target |
|---|---|
| Down and up bandwidth per client | measured (M9c's budget at 4 ships: down <= 20 KB/s, p95 <= 30; up <= 2 KB/s; judged in M9c with shots, pickups, chests and the minimap mirrored) |
| Server tick p95 | <= 8 ms for a single-player voyage on Railway |
| Client frame rate | steady 60 fps on desktop WebGL while connected |
| Prediction | error <= 0.5 m and no correction above 0.5 m in a frame at 150 ms RTT and 2% loss |
| Dropped socket | resumes within 10 s |
| Railway redeploy | ends the session cleanly (no settlement, back to the lobby) |
| Cold start, memory, sessions per process | measured |

## Results

`-`: not measured in that setting.

**Bandwidth scope:** every down/up figure here covers the ship's snapshots, the enemies, level-up messages and inputs
only. Shots (the player's and the enemies'), pickups, chests and the minimap's enemies are not mirrored yet; the owner
decided on 9 Oct that their mirroring comes in M9c and that M9c's bandwidth go/no-go (D9c.1) is judged with it included,
so these figures are a lower bound for M9c.

| Measure | In-process (Editor; part 1, 2b) | Local Docker, Mono server image (part 2a, 2b) | Desktop WebGL client in Chrome vs local Docker (part 2b) | Railway (9-11 Oct, ~270 ms round trip) |
|---|---|---|---|---|
| Down / up per client (payload B/s) | 2,480-2,590 / 830-870 (1 enemy); 5,810 / 900 (33 enemies near, 10:00) | 2,870-3,320 / 840-890 (up to 16 enemies near) | **3,080 / 897** (wire estimate 3,452 / 1,258), up to 11 enemies near | **2,696-3,492 / 845-873** (wire estimate 3,041-3,843 / 1,185-1,224), 6 scripted voyages (`railway/*/client.json`) |
| Snapshot bytes p50 / max | 170 / 170 (1 enemy); 404 / 458 (33 enemies) | 188-234 / 260-269 | - | - (not logged by the Railway client or the server's stats line) |
| Server tick p50 / p95 (ms) | 1.4-3.0 / 1.7-3.6 (Editor, Mono, Editor overhead) | 0.52-0.73 / **0.87-1.48** | 0.53 / 0.79 (the server of the WebGL run) | **0.18-0.95 / 0.42-1.96** per voyage (11 voyages); single-tick max 11.3-36.3 (`railway/server-logs/match-test-v5net.log`) |
| Client fps, desktop WebGL | - | - | **59.7 mean, 1% low 58.8, 0.41% of frames over 20 ms** (45 s; the over-20 ms frames are the first seconds after the stage load) | the owner at 1920x1080: about 40, 98% of frames over 20 ms (`railway/owner-retest/notes.md`; fill rate, see the summary) |
| Prediction error max | **0** sailing (249 compared); boosting **0** (120 compared), 150 ms / 2% loss | sailing 0; 0.38-0.76 m once at a level-up pause entry (clean local link; fixed 9 Oct, below) | 0.38 m (a level-up pause entry; 0 otherwise; fixed 9 Oct) | 0.001-0.006 m (protocol 4 and 5 runs); 0.201 m (protocol 3 run) |
| Correction max | **0** sailing; boosting **0**, 150 ms / 2% loss (part 1, boost unpredicted: 0.67 m) | sailing 0; 1.1 m once per level-up pause entry (fixed 9 Oct) | 0.76 m (a level-up pause entry; p95 0; fixed 9 Oct) | 0.003-0.086 m (protocol 4 and 5 runs); 3.10 m and 2.05 m in the protocol 3 run (`railway/wss-90s-drop/client.log:114-115`, not paused: the ram knockback's signature) |
| Dropped socket resume | 0.29-0.30 s | 0.40-0.50 s | - (no drop in the browser run) | **1.37-1.97 s** (4 drops) |
| Server stop / redeploy | `ServerClosing_EndsTheSessionCleanly` | `docker stop`: end ServerClosing, client in the lobby 0.27-0.35 s later, exit 0 | `docker stop`: "The server closed" end screen **0.26 s** later, back to the menu by its button | redeploy mid-voyage: end ServerClosing, lobby **0.001 s** after, server `EXIT SIGTERM (stop file)` (`railway/wss-redeploy/`) |
| Join | - | welcomed 0.25-0.27 s | ticket and welcome 0.16-0.60 s, stage built 1.32-1.65 s (the server's run waits for the first inputs) | welcomed 1.40-3.72 s (scripted client); first inputs 0.38-10.98 s after the join (stage loading) |
| Cold start | - | `docker run` to READY 1.7-2.4 s (0.8-1.0 s in the process); a fresh process after each voyage with `--restart always` | - | deployment to READY 6.3-8.3 s (pull included); new process after a voyage 1.6-3.1 s; in the process 0.71-1.40 s |
| Server memory | - | 142-150 MB container, RSS 176-179 MB | RSS 177 MB | RSS 181-223 MB at a voyage's end; Railway metric 0.16-0.21 GB hourly |
| Server CPU | - | 7-11% of one core (mean) | - | hourly mean 0.012-0.109 vCPU (rises while idle, risk 6; `railway/server-logs/match-test-metrics.txt`) |
| Sessions per process | 1 (by design) | 1; 3 processes side by side cost 3x one | 1 | 1 |

Host for the local numbers: Windows 11, 16 logical CPUs, Docker Desktop 29.8.2, desktop Chrome (a separate profile,
background throttling off), canvas 1200x750. Files: `inprocess-part2b/*.json`, `docker/part2b-voyage-60s/summary.json`,
`webgl/webgl-client-report.json`, `webgl/chrome-autopilot-45s-then-docker-stop.log`, `session/`.

Railway runs (the scripted test client, a native Windows player on this PC; the server build is in the folder name):

| Run (`railway/`) | Date (UTC) | Seconds | Down / up B/s | Pred / corr max (m) | Resume (s) | End |
|---|---|---|---|---|---|---|
| `voyage-90s-drop` | 9 Oct 08:37 | - | - | - | - | join failed: Railway's edge refused the native client's Host header (fixed by the UTP patch) |
| `tcpproxy-ws-90s-drop` (plain WS through Railway's TCP proxy) | 9 Oct 09:10 | 47.9 | 2,918 / 845 | 0.001 / 0.003 | drop 1.5 s before sinking | Sunk, lobby 0.002 s |
| `wss-90s-drop` (protocol 3) | 9 Oct 10:41 | 98.9 | 3,430 / 873 | 0.201 / 3.098 | 1.365 | Sunk, lobby 0.002 s |
| `wss-v4-60s` | 9 Oct 13:49 | 95.3 | 3,281 / 845 | 0.001 / 0.010 | 1.964 | Sunk, lobby 0.001 s |
| `wss-v5-60s` | 9 Oct 23:08 | 103.4 | 3,492 / 871 | 0.001 / 0.003 | 1.685 | Sunk, lobby 0.002 s |
| `wss-1c84-60s` | 10 Oct 05:14 | 73.3 | 3,047 / 848 | 0.001 / 0.004 | 1.920 | Sunk, lobby 0.002 s |
| `wss-redeploy` | 10 Oct 21:21 | 27.0 | 2,696 / 849 | 0.006 / 0.086 | - | **ServerClosing**, lobby 0.001 s |

### Level-up pause entry and resume (fixed 9 Oct, review finding b)

Before the fix the ship held on both sides from the first input tick the server had not applied, so the ticks the client
had predicted past it were taken back when the cards arrived: 0.76-1.14 m locally, up to 1.7 m while boosting, 1.5-1.9 m
in-process at 150 ms and 2% loss (part 2b numbers, above the 0.5 m gate). Now (protocol version 3):

- the server keeps applying the client's inputs while the cards travel; the client names its hold tick when they arrive
  (the tick after the one it has reached, `rp9b.pick` action Hold); the server takes it, clamped to at least the tick the
  cards were dealt at and at most the newest input received plus 6 ticks, applies the inputs up to there and holds;
- a hold that arrives late (a lost packet resent) rewinds the server's ship to its state before the hold tick (the run is
  paused, nothing else moved); a pick that arrives late steps the held ticks on the inputs already received (at most 30
  ticks back, never before the hold); a pick's resume tick is capped at 12 ticks ahead;
- the client does not correct against snapshots sent before the server had its hold or pick (snapshot flag HoldPending,
  or still paused past the client's resume tick); no hold within 1 s holds from the next unapplied tick and tells the
  client.

| Run (`inprocess-review/*.json`) | Link | Correction max | Prediction error max | Notes |
|---|---|---|---|---|
| `LevelUp_At150ms2Percent_NoCorrectionAboveHalfAMetre` (asserts the 0.5 m gate), x4 runs | 75 ms each way, 2% loss | **0** (sailing entry and resume 0; while boosting at 12.9 m/s 0) | **0** | 118-128 snapshots compared each; in 3 of 4 runs the hold arrived late (the 2% loss) and was rewound, still 0 m |
| `LevelUp_LateHoldAndLatePick_StayExact` (hold and pick 0.3 s late) | clean | **0** | **0** | hold rewound 8 ticks, pick caught up 8-9 ticks |
| `LevelUp_RealCardsOverTheNetwork_PauseAndPick` | clean | 0 | 0 | |
| Local Docker scripted voyage (`docker/review-voyage/`), 2 level-ups, one with a socket drop while the cards were up | local | **0** | **0** | 552 snapshots; resume during the cards 0.42 s, the cards and the hold came back |

On Railway after the fix (the protocol 4 and 5 runs above, 4-6 level-ups each): correction max 0.003-0.086 m.

## Build sizes (desktop WebGL, Brotli download)

| Build | Download | wasm | data |
|---|---|---|---|
| Before M9b (8 Oct 09:19, M9a code, no NGO or UTP) | 113.4 MB (118,936,241 B) | 10,537,124 | 108,199,767 |
| Normal desktop build now (NGO and UTP in the project; the net assembly not compiled) | 114.2 MB (119,787,231 B) | 11,189,429 | 108,397,583 |
| Server voyage client (`V5_NET`), part 2b | 114.3 MB (119,830,005 B) | 11,226,581 | 108,403,230 |
| Server voyage client (`V5_NET`), enemy stutter fix (`enemy-stutter-fix/webgl/net-build.json`) | 114.3 MB (119,857,467 B) | 11,259,108 | 108,398,000 |

The packages cost the normal build about 0.85 MB of download (wasm +0.65 MB; the gap also holds M9a's last changes); the
stripped managed code still holds Netcode (641 KB IL) and Transport (211-214 KB IL). The client's own code adds 42 KB.

## Cost

Railway rates (`plan/v5/ANSWERS.md:45-46`): $20 per vCPU-month and $10 per GB-month, metered by the minute (730 hours a
month here), and $0.05 per GB sent out. Usage from `railway metrics --service match-test` (`railway/server-logs/match-test-metrics.txt`).

| Item | Measured | Cost |
|---|---|---|
| One voyage-hour (one process) | at most 0.11 vCPU (the highest hourly mean, idle creep included) and 0.22 GB (RSS 223 MB at most); 3.8 KB/s down, 14 MB an hour | 0.11 x 20 / 730 + 0.22 x 10 / 730 + 0.014 x 0.05 = **about $0.007** |
| The whole M9b Railway test (`match-test`, 38 metered hours, 9 Oct 08:00 to 10 Oct 21:00 UTC) | mean 0.057 vCPU and 0.22 GB | **about $0.17** |
| Usage export against the month's baseline (D23) | `railway usage` answers Unauthorized with the project token in `.env` | open: read in the workspace's Usage page |

Local Docker before Railway: about 0.1 vCPU and 0.15 GB while a voyage runs, the same order.

## Playtest fixes (9 Oct)

After the owner's Railway playtest (stutter, enemy teleports, a hidden tab during the cards left the ship uncontrollable).
Causes, fixes and tests: `README.md`, section "Playtest fixes". Protocol version 4; the image and the WebGL client go
together. Numbers (files in `playtest-fixes/`):

| Measure | Before | After | Where |
|---|---|---|---|
| Drawn ship, per-frame speed jitter mean / p95 (m/s), 11 m/s, 60 fps | 18.1 / 23.6 | **0.17 / 0.12** | in-process, 270 ms round trip |
| Drawn ship, frames standing still | 98 of 241 | **0** | same |
| On-screen enemies, per-frame speed jitter mean / p95 (m/s) | 1.03 / 4.76 | **0.15 / 0.58** | in-process busy voyage, 270 ms, 15 ms jitter, 1% loss |
| On-screen enemies, frames standing still | 855 | **1** | same |
| On-screen enemies faster than any enemy sails / sliding | 61 / 20 | **0 / 0** (65 relocations by the server, drawn at once) | same |
| Control back after the server's card timeout, hidden tab, socket open | never | **0.00 s** in-process, 0.002 s in Docker | 270 ms; Docker local |
| Away 35 s during cards (solo voyage) | voyage ran on, cards timed out | held, timeout paused, resynced 0.05 s after back | Docker local |
| WebGL in Chrome (local) | - | 59.6 fps mean, 1% low 58.8; ship jitter 0.10 / 0.14; enemy jitter 0.21 / 0.71; 0 fast or sliding enemies; input delay p50 67 ms | throwaway profile, window minimised 20 s mid-voyage |
| Down bandwidth with the 75 m near band | 5.7 KB/s (33 enemies, 60 m) | 5.9 KB/s (up to 38 enemies) | in-process busy voyage |

## Owner re-test fixes (10 Oct)

After the owner's re-test on Railway (image `e1f56626d9b5`). Causes, fixes and tests: `README.md`, "Owner re-test fixes".
Protocol version 5; the image and the WebGL client go together. Files in `owner-retest-fixes/`.

| Measure | Before | After | Where |
|---|---|---|---|
| Ship held by the server for good after sailing into a chest with gold (the owner's stuck ship) | correction 1.14 m every snapshot, kills frozen (local Chrome, old build) | choice skipped at once, no correction, no gold spent | in-process, local Chrome |
| Resyncs around tab switches and hitches | 66, in bursts of 9 at each hide (local Chrome, 7 hides) | 3 (one a hide at most); in-process control back 0.005-0.037 s | local Chrome, in-process 270 ms |
| On-screen enemies after a level-up pick | back over half speed 436 ms after the pick, 24 frames standing still | 0 ms, 0 frames | in-process busy voyage, 270 ms, 60 fps |
| Client fps, this PC (Intel UHD), 960x600 canvas | 56.8 | 58.0 (normal game: 29.6-32.9) | local Chrome |
| Client fps, this PC, 1920x1080 canvas (full screen) | - | 42.0, 82% of frames over 20 ms (normal game: 15.0-17.1) | local Chrome |
| Local Docker voyage: away 20 s during cards / hidden 8 s during cards / pick then hidden 5 s | - | resynced 0.050 s, cards back / control 0.018 s / control 0.034 s after the welcome, 0 resyncs | `owner-retest-fixes/docker-voyage/summary.json` |

The 40 fps the owner saw is the full-screen canvas on an integrated GPU, not the net client: the normal game is slower
at the same canvas. (The 31 m leash note that stood here is superseded by the next section.)

## Enemy stutter fix and leash outside the view (10 Oct)

After the owner's re-test of `e01a2e87` ("the enemies seem to be stuttering now a bit"). Cause, fix and tests: `README.md`,
"Enemy stutter fix and leash outside the view". Protocol unchanged (5); rules v5 (same bounds). Files in `enemy-stutter-fix/`.

| Measure | Before (e01a2e87) | After | Where |
|---|---|---|---|
| On-screen enemy jitter in normal sailing after picks, mean / p99 (m/s); e1f56626's clock 0.142 / 1.370 | 3.176 / 28.96 | 0.131 / 1.014 | in-process, 270 ms, 60 fps |
| Frames drawn past the newest snapshot after picks | 49.9% | 0% | same |
| On-screen enemy speed jumps (over 3 m/s in a frame) after picks | 24.3% of samples | 0.03% | same |
| Frames the enemies stood still after a pick (e1f56626: 48) | 0 | 0 | same |
| Enemy jitter mean / p95 in local Chrome, 270 ms (3 voyages each) | 0.15-3.46 / 0.46-24.3 | 0.11-0.16 / 0.26-0.74 | `enemy-stutter-fix/webgl/` |
| Render delay at the end of a voyage, local Chrome | 173-350 ms | 144-197 ms | `enemy-stutter-fix/webgl/` |
| On-screen frames faster than any enemy sails (leash pop-ins), local Chrome | 24-116 a voyage | 0 | `enemy-stutter-fix/webgl/` |
| Leash relocations in view, all headings, 4:3 to 2.4:1 screens | 34.5-65.3% (31 m rule, standing) | 0 of 163,080 (4 m clear past the hull) | EditMode (`enemy-stutter-fix/inprocess/leash-coverage.txt`) |
| Local Chrome with 3 tab hides | - | 0 resyncs, 0 on-screen pop-ins, jitter 0.121 / 0.443 | `enemy-stutter-fix/webgl/` |

The owner re-tested this build on Railway on 11 Oct: enemies smooth, tab switching fine, no pop-ins.
