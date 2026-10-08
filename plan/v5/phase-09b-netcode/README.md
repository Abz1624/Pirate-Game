# M9b — Netcode server voyage (evidence)

Branch `v5-live-game`. Decisions D18 P5, D19 (2), D22 (N5, N7), D23. Plan item I9b.1.

## Status

| Part | What | Status |
|---|---|---|
| I9b.1 part 1 | Everything that runs in-process in the Editor: packages, `RoguePirates.V5.Net`, scene `V5NetSpike`, protocol, join ticket check, measurement hook, PlayMode tests | done 8 Oct (this page) |
| I9b.1 part 2 | Linux Dedicated Server build in Docker (local, then a Railway test service), the API's ticket endpoint, the WebGL client build against Railway | to do |
| D9b.1 | Measurements on Railway and desktop WebGL (`REPORT.md`) | to do (part 2); in-process numbers below |

## Packages (pinned in `Packages/manifest.json`)

| Package | Version | Why this one |
|---|---|---|
| `com.unity.netcode.gameobjects` | **2.13.3** (21 Sep 2026) | Newest 2.x. NGO 3.0.0 and 3.1.0 need Unity 6000.7 (this project is 6000.3) and pull in `com.unity.netcode` 7.x; the plan pins a tested 2.x. |
| `com.unity.transport` | **2.7.4** (13 Jul 2026) | Newest 2.x; NGO 2.13.3 asks for 2.6.0 or later. |

Resolved with them: `com.unity.collections` 2.6.2, `com.unity.burst` 1.8.28 (`packages-lock.json`). NGO also created
`Assets/DefaultNetworkPrefabs.asset` (its empty default prefab list; NGO recreates it if deleted, so it is kept).

WebSockets: `UnityTransport.UseWebSockets` works on both sides in the Editor (every in-process test runs over WS on a
loopback port). The server listens on plain WS (`NetEndpoint.ApplyServer`: `0.0.0.0:$PORT`, no certificate: Railway's
edge ends TLS). The client config switches on the URL: `ws://host:port/` is plain (local), `wss://host/` sets
`UseEncryption` and `SetClientSecrets(host)`, which is UTP's `WithSecureClientParameters(serverName)` (port 443 by
default). Hostnames are passed through (UTP resolves them; the browser does on WebGL). Covered by
`V5NetEditModeTests.Endpoint_LocalWsAndRailwayWss`.

## Layout and release exclusion

| Path | What |
|---|---|
| `Assets/_Game/V5/Net/` | Assembly `RoguePirates.V5.Net` (references V4 Runtime, Config, Foundation, NGO, UTP, Collections, Input System) |
| `Assets/_Game/V5/Net/Scenes/V5NetSpike.unity` | Spike scene: one `V5NetSpike` object (roles InProcess, Server) and a camera |
| `Assets/_Game/V4/Runtime/Ports/ShipMotionPort.cs` | The net layer's port to the ship: take control, capture/restore motion, one step per input, the prediction copy |
| `Assets/_Game/V4/DevTools/Scenarios/Scenarios/net_voyage_*.asset` | Scenario Lab states the net tests start from |
| `Assets/_Game/Tests/V5Net/{EditMode,PlayMode}` | `RoguePirates.V5.Net.Tests.EditMode` / `.PlayMode` |

Kept out of release builds by the assembly's define constraint `UNITY_EDITOR || V5_NET`: no player build compiles it
unless its build profile adds the scripting define `V5_NET` (part 2: the Linux server profile and the WebGL net-test
profile only). The spike scene is not in the build list. `V5NetEditModeTests.NetAssembly_IsKeptOutOfReleaseBuilds`
checks the constraint, the build list and that WebGL, Android and Standalone define no `V5_NET`. The NGO and UTP
package code itself still ships in every build (packages cannot be excluded per build): its size in the WebGL build is to
be measured in part 2.

## Server composition (no fork of gameplay code)

The server runs the shipped run: `V4Game` in `V4SaltwindRun` with its modules, ports and config. Two switches are set
before the run loads (`NetServerComposition.PrepareHeadlessRun`):

- `RunRequest.Headless = true`: `V4PresentationRoot` and the Tuning Console do not install (no presenters);
- a `run.tickRate = 30` preset (applied by `V4Game.Awake` before anything reads config): the modules tick at 30 Hz.

`NetServer.Attach(game)` takes the ship through `ShipMotionPort.TakeControl` (a dedicated steer input, the ship's own
`FixedUpdate` off, `DirectStep` on) and steps it once per applied input with `PlayerShipController.TickMovement`. The
client's prediction steps a stripped copy of the same ship (`ShipMotionPort.CreatePredictionCopy`: handling, hull
shape and model only) with the same code. Foundation changes, all off by default so the voyage is unchanged:

- `PlayerShipController.DirectStep`: body and transform written at once per step (several steps per frame each start
  from the last), and the island cooldowns on the ship's own step clock instead of `Time.time`;
- `PlayerShipController.MotionState` with `CaptureMotion` / `RestoreMotion`: everything one step reads and writes.

Hull, damage, armor, regeneration, items and sinking stay in the run's modules on the server. The client's ship copy has
a hull of its own that nothing damages (island impacts on it are dropped); the hull it shows is the snapshot's.

## Protocol (`NetProtocol`, named messages, no NetworkObject per ship or enemy)

| Message | Direction | Delivery | Content |
|---|---|---|---|
| connection payload | client to server | approval | `1` + join ticket (UTF-8), or `2` + 16-byte resume token |
| `rp9b.in` | client to server | unreliable, 30 Hz | version, newest tick (u32), count, then up to 8 inputs newest first (steer i8, throttle i8, buttons u8): 30 bytes |
| `rp9b.welcome` | server to client | reliable | resume token, server tick, tick rate, snapshot interval, seed, ship snapshot |
| `rp9b.snap` | server to client | unreliable, 15 Hz | server tick, acknowledged input tick, the ship's full motion state (28 floats), hull, max hull, run time, level, kills, gold, flags: 137 bytes |
| `rp9b.enemies` | server to client | unreliable, 15 Hz | batches of at most 96: header (tick, batch i/n, full flag, origin x/z, count; 17 bytes), then per enemy unit id (varint), kind (u8), x/z offset from the ship (i16 each, 2 cm steps), heading (u8), hull fraction (u8), flags (elite, boss): 9-11 bytes |
| `rp9b.defs` | server to client | reliable | enemy kind index to content id, each kind once per session |
| `rp9b.end` | server to client | reliable | reason (sunk, run ended, server closing, session expired), run time, kills |
| `rp9b.report` | client to server | any | what a tampered client might send (a hull); the server counts and drops it unread |

Rates: server simulation and client input 30 Hz; snapshots every 2nd tick (15 Hz). Both clocks run on real time
(`Time.deltaTime` is capped at a third of a second a frame), so the tick counts stay in step after a hitch. Inputs: each
packet repeats the 8 newest (a lost packet costs nothing while the next arrives). Server input buffer: starts once 2
inputs are in; exactly one ship step per server tick, whatever the client sends; a missing input repeats the last one
(counted as guessed); a backlog over 6 is skipped unapplied, oldest first (a client running its clock fast gains no
speed). A client that stalls catches its ticks up (up to 8 a frame); after a stall over 2 s, or whenever a snapshot
acknowledges a tick ahead of its own, it takes the server's state and moves its tick ahead of it by its usual lead. Prediction: the
client steps its ship copy on each quantized input at once; each snapshot names the newest input applied, the client
measures its prediction for that tick against the server's state (prediction error), restores the server's state and
replays its later inputs (the correction is how far that moved the ship now), and the view eases the correction out
(`NetClient.VisualOffset`). Quantization: inputs to 1/127; enemy positions to 2 cm (error at most 1 cm per axis; i16
gives +-655 m around the ship); heading to 1.4 degrees; hull fraction to 1/255; the ship's state is sent unquantized so
the replay starts from exactly the server's state. Interest management: enemies within 60 m of the ship in every
snapshot, 60 to 120 m in every 3rd (a full snapshot), none beyond; nearest first, at most 384 a snapshot. The client
drops an enemy a complete snapshot should have listed and did not (any on a full one, near ones otherwise).

Reconnect: on a dropped socket the server keeps the session (the ship coasts) for 30 s; the client reconnects with the
resume token from its welcome every 0.5 s for up to 10 s, and stops ticking until the new welcome; the welcome carries
the current state and a fresh resume token, and the ship waits for the client's first new input, so prediction restarts
in step. A silent socket counts as gone after 5 s (UTP's default is 30 s); a resume that arrives while the server still
holds the old socket takes the session over and the old socket is disconnected.

## Join ticket (the contract for the API, part 2)

```
ticket  = "v1." + b64url(payloadJson) + "." + b64url(HMAC-SHA256(key, "v1." + b64url(payloadJson)))
key     = UTF-8 bytes of RP_MATCH_TICKET_SECRET (at least 32 characters), the same secret on the API and the match server
payload = {"v":1,"tid":"<unique id>","sub":"<player id>","mid":"<match id>","mode":"voyage","aud":"rp-match","iat":<unix s>,"exp":<unix s>}
b64url  = base64 with - and _, no = padding; JSON field order free
```

The server (`JoinTicketVerifier`) checks, in order: the signature (constant-time, before reading the body), version
1, audience `rp-match`, `iat` not in the future and `exp` not past (5 s clock skew each), lifetime `exp - iat` at most
120 s, the match id when the server has one (`RP_MATCH_ID` on a Server role), and single use (the `tid` is remembered
until it expires). The match server then refuses a ticket for another mode and a voyage already taken by another
player; a ticket is marked used only once the join goes through. Used ids live in the process: a restarted server
could take a ticket again inside its lifetime (at most 125 s); the match id narrows that to the same session. Refusals go
back to the client as the approval reason (`ticket Replayed`, `ticket Expired`, ...). The secret is read from the
environment only and never logged; the Editor spike and the tests use a random per-session key.
`V5NetEditModeTests.Ticket_ContractVector` holds a fixed vector the API's `dotnet test` should reproduce.

## Tests (8 Oct)

EditMode `V5NetEditModeTests` (14): ticket valid once then replayed; tampered or wrong key; time window and match;
malformed, no secret, wrong audience; contract vector; a refused join does not burn the ticket; ws/wss endpoint switch
on a real `UnityTransport`; input codec with smuggled bytes not read; ship snapshot round trip; enemy quantization (at
most 1 cm per axis, at most 11 bytes an enemy); enemy mirror removals; a capped snapshot removes nothing; the 30 Hz
preset on the default config set; the release exclusion (asmdef, build list, PlayerSettings and Build Profile defines).

PlayMode `V5NetPlayModeTests` (13), each from a Scenario Lab state, server and clients in one Editor over WebSockets:

| Test | Scenario | Checks |
|---|---|---|
| Spawn_TheClientSailsTheServersShip | net_voyage_open_water | headless 30 Hz run, prediction ship at the server ship (< 5 cm), snapshots, hull, the dinghy mirrored with its kind |
| Input_MovesTheServersShipAuthoritatively | net_voyage_open_water | no input no movement; 2 s full throttle moves the server's ship > 10 m forward; acks; prediction within 0.5 m |
| Damage_OnlyTheServerDealsIt | net_voyage_rammed | dinghies ram the server's ship; the client shows the server's hull; the client's ship copy never took damage |
| ClientSentHull_IsIgnored | net_voyage_open_water | two hull reports and hull bytes smuggled after an input: counted, unread; hull not raised, ship not sunk |
| Reconnect_Within10Seconds_Resumes | net_voyage_open_water | server-side and client-side drops resume (about 0.3 s each), inputs move the ship again, prediction follows; the used ticket is refused (`Replayed`) |
| JoinTicket_ForgedExpiredOrForeign_IsRefused | net_voyage_open_water | forged, expired and garbage tickets refused with their reasons; a good one joins |
| Latency150msLoss2Percent_PredictionWithinHalfAMetre | net_voyage_open_water | UTP simulator 75 ms each way, 2% loss both ways: max prediction error and max correction at most 0.5 m; the 150 ms is there (input lag p50 > 150 ms) |
| Jitter40msLoss5Percent_Measured | net_voyage_open_water | worse than the gate (75 +- 40 ms, 5% loss): p95 prediction error at most 0.5 m |
| Measure_BusyVoyageAt10Minutes | net_voyage_busy | 10:00, the game's ambient spawning, no weapons: bandwidth and tick time with about 30 enemies near |
| ClientStall_InputsDriveTheShipAgain | net_voyage_open_water | the client alone stalls 1 s, then 3 s (the server guesses meanwhile); afterwards its inputs drive the ship again (no more guesses, the server steers as the client does), the long stall resyncs, prediction back within 0.5 m |
| ForgedFastClock_GainsNoSpeed | net_voyage_open_water | a client running its tick clock 3x: the server never steps faster than its tick, the surplus is skipped |
| ResumeWhileTheOldSocketLingers_TakesOver | net_voyage_open_water | a second socket with the resume token takes over while the first is still connected; the old one is let go, the token is fresh, the new socket's inputs drive the ship |
| Boost_DivergesAndIsCorrected_Measured | net_voyage_open_water | 150 ms, 2% loss, a boost: the hook sees the divergence (the boost is not predicted yet) and reconciliation brings the ship back within 0.5 m |

The link simulator (`NetLinkSimulator`) is UTP's `SimulatorPipelineStage` added to the transport's default pipelines
through a driver constructor: NGO 2.x's own debug simulator is a no-op now (moved to the Multiplayer Tools package).

Why the clean prediction numbers are exactly 0: server and client step the same handling code from the same state on
the same quantized inputs, so the replay reproduces the server bit for bit; an error only comes from what the client
does not model (server-only effects such as the boost, slows and knockback from enemies, guessed inputs). The boost test
shows the hook measuring such a divergence. In-process, both sides share the island colliders and `RunManager`, so the
real client build (part 2) is the test of a client that has to build its own stage.

## Measurements (in-process, Editor, Windows; JSON in this folder)

Server tick time is all of the server's Update work (the run's composition root, the gameplay components, the net
server, the frame pipeline: execution order -101 to 11) per 30 Hz tick, in the Editor (Mono, Editor overhead); the
Railway number (target p95 <= 8 ms) is part 2. Bytes are message payloads; "wire" adds NGO's named-message framing (12
bytes a message), before the WebSocket and transport headers.

| Run (`inprocess/*.json`) | Link | Down B/s (wire est.) | Up B/s (wire est.) | Snapshot bytes p50 / max | Enemies a snapshot max | Server tick ms p50 / p95 | Prediction error max (m) | Correction max (m) | Input lag p50 (ms) |
|---|---|---|---|---|---|---|---|---|---|
| input-clean | clean | 2,444 (2,806) | 846 (1,194) | 162 / 162 | 1 | 1.58 / 1.80 | 0 | 0 | 33 |
| latency-150ms-2pct (gate) | 75 ms each way, 2% loss | 2,428 (2,787) | 874 (1,225) | 162 / 162 | 1 | 1.57 / 1.79 | **0** (249 compared) | **0** | 200 |
| jitter-40ms-5pct | 75 +- 40 ms, 5% loss | 2,443 (2,805) | 854 (1,198) | 162 / 162 | 1 | 1.57 / 1.83 | 0 (139) | 0 | 200 |
| boost-150ms-2pct | 75 ms, 2% loss, a boost | 2,324 (2,668) | 804 (1,128) | 162 / 162 | 1 | 1.74 / 2.23 | 0.138 | 0.666 | 200 |
| rammed-clean | clean, 6 dinghies ramming | 3,110 (3,473) | 792 (1,127) | 207 / 207 | 6 | 1.84 / 1.99 | 0 | 0 | 33 |
| busy-10min-clean | clean, 10:00 ambient spawns, no weapons | 5,701 (6,062) | 900 (1,260) | 396 / 450 | 33 | 3.02 / 3.69 | 0 | 0 | 33 |
| client-stall | clean, client stalls 1 s and 3 s | 2,433 | 547 | 162 / 162 | 1 | 1.71 / 2.00 | 0 (127) | 0 | 33 |
| reconnect | clean, two drops | - | - | 162 | 1 | 1.52 / 1.74 | 0 | 0 | 33 |

Resume after a dropped socket: 0.29 s (server-side drop) and 0.30 s (client-side drop), against the 10 s limit.
Snapshot cost: 137 bytes for the ship and run state plus 17 bytes a batch header plus 9-11 bytes an enemy, at 15 Hz;
input 30 bytes at 30 Hz. At 33 enemies near, 5.7 KB/s down; by the per-enemy cost, 200 enemies in range would be about
35 KB/s before any delta compression, against M9c's 20 KB/s (p95 30) budget at 4 ships: interest radius, the far-ring
rate and delta encoding are the levers for M9c. The boost's correction (0.67 m) is above the 0.5 m gate: boost
prediction is a part 2 item. Server tick time is the Editor's (Mono, Editor overhead) with the whole run at 30 Hz,
well under the 8 ms p95 target; Railway is part 2.

## Gate (part 1)

- EditMode full suite: 597/597 (`editmode-full.json`; 583 before plus 14 new).
- PlayMode V5 net: 13/13 (`playmode-v5net.json`).
- PlayMode full suite: 105/105 (`playmode-full.json`; 92 before plus 13 new), after the review fixes.
- Independent review (v5-reviewer, 8 Oct): 3 blockers, all fixed: a client stall left the server guessing for good
  (real-time clocks, catch-up, resync on a forward ack; test `ClientStall_InputsDriveTheShipAgain`); a client could gain
  speed by numbering ticks fast (one step per server tick, backlog skipped; test `ForgedFastClock_GainsNoSpeed`); the
  evidence did not match the claims (this page, the JSON in this folder, reruns after every change). Majors fixed:
  resume while the old socket lingers (takeover, 5 s disconnect timeout; test), prediction tests that could not fail
  (the boost test shows a measured divergence; the zero is explained above). Minors fixed: no ticks before the new
  welcome, ticket used only on a successful join, match id from `RP_MATCH_ID` and the mode checked, resume token rotated,
  no level-up cards on a server's own run, truncated snapshots remove nothing, Build Profile defines checked, the
  comment's file name. Recorded, not fixed: the protocol's rates, radii and caps are constants in `NetProtocol` for the
  spike (an exception to the V4 config rule) and move to a config asset before I13b.1 promotes the code; the link
  simulator drops packets UDP-style, while a WebSocket on Railway shows loss as bursts of delay (part 2 measures the real
  link).

## Part 2 needs

- Linux Dedicated Server build of this project with `V5_NET`, booting the `V5NetSpike` Server role (or a server boot
  scene) headless (`-batchmode -nographics`), listening on `$PORT`, `RP_MATCH_TICKET_SECRET` from the environment.
- Dockerfile for it; the scripted server voyage on local Docker; a Railway test service (usage checked against the
  month's baseline first, the service deleted after the session, D23); Railway's WebSocket idle timeout and redeploy
  behaviour checked (a redeploy ends the session cleanly: `rp9b.end` reason ServerClosing, back to the lobby).
- API: `POST` endpoint in the lobby issuing the ticket above (same secret as a Railway shared variable), with
  `dotnet test` for expiry, replay (the server's job, but the API must give each ticket a fresh `tid`) and the contract
  vector.
- WebGL client build (`V5_NET`, desktop) against `wss://` on Railway: a client composition that builds the stage
  from the seed in the welcome (islands for prediction and visuals) and draws enemies from the mirror with the real
  enemy views; level-up choices from the client (the server currently runs the scenario's no-card level-ups); boost
  prediction (the boost's ease runs on the server's frame clock, so a boost costs a correction today); client fps on
  desktop WebGL; cold start, memory and sessions per process on Railway; the NGO and UTP size in the WebGL build.
