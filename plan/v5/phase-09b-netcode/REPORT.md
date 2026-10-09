# M9b — D9b.1 single-ship measurements (report)

Branch `v5-live-game`. Plan item D9b.1 (`plan/v5/V5_PLAN.md`, M9b). Method and evidence files: `README.md` in this folder.
Started 8 Oct (part 2b): **the in-process, local Docker and desktop WebGL (against local Docker) numbers are in; the
Railway column is empty** until the Railway test session (steps at the end of `README.md`).

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

`-`: not measured in that setting. **open**: to measure on Railway.

**Bandwidth scope:** every down/up figure here covers the ship's snapshots, the enemies, level-up messages and inputs
only. Shots (the player's and the enemies'), pickups, chests and the minimap's enemies are not mirrored yet; the owner
decided on 9 Oct that their mirroring comes in M9c and that M9c's bandwidth go/no-go (D9c.1) is judged with it included,
so these figures are a lower bound for M9c.

| Measure | In-process (Editor; part 1, 2b) | Local Docker, Mono server image (part 2a, 2b) | Desktop WebGL client in Chrome vs local Docker (part 2b) | Railway |
|---|---|---|---|---|
| Down / up per client (payload B/s) | 2,480-2,590 / 830-870 (1 enemy); 5,810 / 900 (33 enemies near, 10:00) | 2,870-3,320 / 840-890 (up to 16 enemies near) | **3,080 / 897** (wire estimate 3,452 / 1,258), up to 11 enemies near | open |
| Snapshot bytes p50 / max | 170 / 170 (1 enemy); 404 / 458 (33 enemies) | 188-234 / 260-269 | - | open |
| Server tick p50 / p95 (ms) | 1.4-3.0 / 1.7-3.6 (Editor, Mono, Editor overhead) | 0.52-0.73 / **0.87-1.48** | 0.53 / 0.79 (the server of the WebGL run) | open (target <= 8) |
| Client fps, desktop WebGL | - | - | **59.7 mean, 1% low 58.8, 0.41% of frames over 20 ms** (45 s; the over-20 ms frames are the first seconds after the stage load) | open (same client; the link differs) |
| Prediction error max, 150 ms / 2% loss | **0** sailing (249 compared); boosting **0** (120 compared) | sailing 0; 0.38-0.76 m once at a level-up pause entry (clean local link) | 0.38 m (a level-up pause entry; 0 otherwise) | open |
| Correction max, 150 ms / 2% loss | **0** sailing; boosting **0** (part 1, boost unpredicted: 0.67 m) | sailing 0; 1.1 m once per level-up pause entry | 0.76 m (a level-up pause entry; p95 0) | open |
| Dropped socket resume | 0.29-0.30 s | 0.40-0.50 s | - (no drop in the browser run) | open (target <= 10 s) |
| Server stop / redeploy | `ServerClosing_EndsTheSessionCleanly` | `docker stop`: end ServerClosing, client in the lobby 0.27-0.35 s later, exit 0 | `docker stop`: "The server closed" end screen **0.26 s** later, back to the menu by its button | open (a redeploy during a voyage) |
| Join | - | welcomed 0.25-0.27 s | ticket and welcome 0.16-0.60 s, stage built 1.32-1.65 s (the server's run waits for the first inputs) | open |
| Cold start (`docker run` to READY) | - | 1.7-2.4 s (0.8-1.0 s in the process); a fresh process after each voyage with `--restart always` | - | open (plus the image pull) |
| Server memory | - | 142-150 MB container, RSS 176-179 MB | RSS 177 MB | open |
| Server CPU | - | 7-11% of one core (mean) | - | open |
| Sessions per process | 1 (by design) | 1; 3 processes side by side cost 3x one | 1 | 1 |

Host for the local numbers: Windows 11, 16 logical CPUs, Docker Desktop 29.8.2, desktop Chrome (a separate profile,
background throttling off), canvas 1200x750. Files: `inprocess-part2b/*.json`, `docker/part2b-voyage-60s/summary.json`,
`webgl/webgl-client-report.json`, `webgl/chrome-autopilot-45s-then-docker-stop.log`, `session/`.

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

The desktop WebGL row above (0.38 m / 0.76 m at a level-up entry) predates the fix; the browser build is to be rebuilt for
protocol 3 before the Railway session.

## Build sizes (desktop WebGL, Brotli download)

| Build | Download | wasm | data |
|---|---|---|---|
| Before M9b (8 Oct 09:19, M9a code, no NGO or UTP) | 113.4 MB (118,936,241 B) | 10,537,124 | 108,199,767 |
| Normal desktop build now (NGO and UTP in the project; the net assembly not compiled) | 114.2 MB (119,787,231 B) | 11,189,429 | 108,397,583 |
| Server voyage client (`V5_NET`) | 114.3 MB (119,830,005 B) | 11,226,581 | 108,403,230 |

The packages cost the normal build about 0.85 MB of download (wasm +0.65 MB; the gap also holds M9a's last changes); the
stripped managed code still holds Netcode (641 KB IL) and Transport (214 KB IL). The client's own code adds 42 KB.

## Cost

Local only so far: about 0.1 vCPU and 0.15 GB while a voyage runs, under $0.01 per voyage-hour at Railway's rates. The
Railway usage export, against the month's baseline, goes here after the Railway session (D23).

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

The Railway column above was measured before these fixes; the main session's next Railway session reruns it with the new
image and client.
