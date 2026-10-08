# M9b — D9b.1 single-ship measurements (report)

Branch `v5-live-game`. Plan item D9b.1 (`plan/v5/V5_PLAN.md`, M9b). Method and evidence files: `README.md` in this folder.
Started 8 Oct (part 2b): **the in-process, local Docker and desktop WebGL (against local Docker) numbers are in; the
Railway column is empty** until the Railway test session (steps at the end of `README.md`).

## Targets (D9b.1)

| Measure | Target |
|---|---|
| Down and up bandwidth per client | measured (M9c's budget at 4 ships: down <= 20 KB/s, p95 <= 30; up <= 2 KB/s) |
| Server tick p95 | <= 8 ms for a single-player voyage on Railway |
| Client frame rate | steady 60 fps on desktop WebGL while connected |
| Prediction | error <= 0.5 m and no correction above 0.5 m in a frame at 150 ms RTT and 2% loss |
| Dropped socket | resumes within 10 s |
| Railway redeploy | ends the session cleanly (no settlement, back to the lobby) |
| Cold start, memory, sessions per process | measured |

## Results

`-`: not measured in that setting. **open**: to measure on Railway.

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

### Level-up pause entry (outside the sailing gate)

The server's run pauses on level-up cards as a voyage does; the ship holds on both sides from the first input tick the
server had not applied. The client learns of the level-up only when the cards arrive, so the ticks it predicted after that
point are taken back once: about the ship's speed times the input lead, 0.76-1.14 m locally (67-100 ms input delay), up to
1.7 m while boosting; at 150 ms and 2% loss in-process 1.5-1.9 m (`LevelUp_At150ms2Percent_Measured`). The cards cover
the screen at that moment. The resume is exact in separate processes (the pick names the tick the ship moves again from);
a pick delayed by a lost packet costs one more take-back there. The owner's playtest decides whether this shows; easing the
ship's drawn position (as `NetClient.VisualOffset` already computes) would hide it.

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
