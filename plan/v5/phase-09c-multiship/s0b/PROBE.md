# M9c S0b: early go/no-go probe on the M9b server (11 Oct)

Branch `v5-live-game`, on top of tag `v5-m9c-s0` (not committed). Plan: `../PLAN.md`, review change 4. Targets (D9c.1, at 4
ships and 200 enemies): down <= 20 KB/s per client on the wire (p95 <= 30), up <= 2 KB/s, server tick p95 <= 16 ms on
Railway. Review changes 1 (wire bytes with a quality floor), 2 (108-enemy device cap per view), 11 (frames under 1,400 B)
apply.

## Provisional verdict: GO with conditions

Bandwidth passes with room to spare: about 12 KB/s p50 and 13-15 KB/s p95 per client on the wire, at most 18.7 KB/s in
any 1 s window. That needs the delta encoding and the 108 cap. Today's v5 protocol sends the same 200 enemies to one
client at **27-29 KB/s**, so the new encoding is required for M9c, not optional.

The server tick fits, but with less room. Locally, in Docker on Mono with one real ship firing, 200 enemies, 3 ghost
ships' effects and 4 per-client encoders, it is **6.6 ms p50 and 9.1 ms p95**. My 4-ship estimate is about 10.6 ms p95
locally, and **10.6-14 ms p95 on Railway** against the 16 ms target. The conditions are listed at the end.

## What was built (test-only)

- **`load200` preset.** `Assets/_Game/V5/Net/LoadProbe/NetLoadProbe.cs` (`NetTestPresets`, `NetLoadProbe`).
  - It is taken only when `RP_ALLOW_TEST_PRESETS=1` and `RP_TEST_PRESET=load200` are set on the server, or by a test
    (`NetTestPresets.AllowForTests`). It is refused with a warning otherwise.
  - No release build can reach it: the V5 net assembly compiles only in the Editor or with `V5_NET`.
  - What it does:
    - makes the ship invulnerable;
    - turns ambient spawning off;
    - applies level-ups without the cards' pause;
    - keeps 200 enemies (13 ordinary kinds, 5% elites, hull x8) 28-44 m around the ship, inside the 45 m leash, adding
      at most 8 per tick;
    - gives the real ship a six-weapon level-8 build: swivel guns, deck cannons, broadside battery, mortar, lightning
      mast, parrot squadron.
  - The enemy count held at 200 (p50 and minimum while measuring) for 316 s. There were 0 failed spawns and 0
    quantization clamps.
- **Ghost ships.** Three stand-ins orbit the real ship at 25/45/65 m (±10 m wobble, about 9 m/s). Each replays the real
  ship's recorded effects 23, 47 and 71 ticks later from its own position. So the effect rate comes from the real weapon
  configs firing for real, not from an estimate.
  - `NetFxRecorder.cs` records the run's own presentation sinks: `IWeaponEffects`, `IProjectileViews`,
    `IHostileShotViews`, `IEnemyEffects`, `IPickupViews`, `IPowerupViews`.
  - It returns what the headless forwarders return, so the run behaves as on the M9b server.
- **Measurement encoder.** Nothing is sent. For each of the 4 views (the real ship and each ghost), every snapshot
  (15 Hz) it builds:
  - **own snapshot:** the real v5 `snap` size;
  - **ships:** 11 B per other ship, 15 Hz within 75 m and 5 Hz beyond;
  - **enemies:** the nearest 108 within 130 m. Those within 75 m are refreshed every snapshot, the rest every 3rd. They
    are delta-encoded against the client's acked baseline (`NetEnemyDelta.cs`; the ack arrives one round trip late,
    270 ms by default, so the baseline is 333 ms old);
  - **fx:**
    - a salvo is one event (12 B plus 1 B per extra projectile);
    - projectiles are dead-reckoned, with a 9 B correction only when the heading turns more than 5.6 degrees, at most
      10 a second, and a 3 B end;
    - effects are 7-9 B; a kill's drop is one event plus 3 B per pickup; summons cost 4 B per snapshot;
    - events from the own ship are always sent; others' and the world's (enemy shots, splashes) only within 80 m;
  - **threats:** the minimap's out-of-view enemies at 2 Hz, 2 B each.
- **Wire and budget.**
  - Wire = payload + 12 B per named message, as M9b defined it (M9b's wire/payload ratio was 1.12).
  - One per-snapshot budget (1,250 B of wire) is applied inside the server tick. 0 (no cap), 1,000, 850 and 700 B are
    replayed after the tick on the same captured snapshots.
  - Fill order: own ship and other ships first, then own fx, enemies (close > near > far, plus 40 per snapshot of
    staleness), others' fx, far enemies, threats.
  - Effects that do not fit wait up to 7 snapshots and are then dropped.
- **Hooks in shipped V5 net code** (null on every real server):
  - `NetServer.Probe` (called at the end of each tick, inside the tick time) and `NetServer.LastShipSnapshotBytes`;
  - `NetMetrics.ClientsForReport()`;
  - `V5NetSpike` starts the preset from the environment (Server role only);
  - `NetServerHost` writes the probe's final report.
- **Harness.** `server/match/local_voyage_test.py` now takes `--server-env NAME=VALUE` (repeatable) and
  `--skip-extra-checks`.

## Numbers

Files are in this folder. `summarize.py` prints the tables from any `probe-*.json`. Every probe ran the real v5 client
(S-turns at full throttle) against the server.

### Per-client wire bytes (budget 1,250 B per snapshot; the uncapped run is identical except the max)

Values are B/s over 1 s windows: p50 / p95 / max. The category columns are p50.

| Run | View | Wire p50 / p95 / max | Own | Ships | Enemies (p95) | Fx own | Fx others + world | Threats | Framing |
|---|---|---|---|---|---|---|---|---|---|
| **Docker, Mono, 316 s** (`docker-load200/server-1/probe-load200.json`; 3.7 kills/s) | real | **12,023 / 13,745 / 16,525** | 2,175 | 525 | 7,154 (8,300) | 221 | 950 | 374 | 744 |
| | ghost 1 | 12,180 / 13,719 / 15,809 | 2,175 | 525 | 7,409 (8,350) | 292 | 785 | 374 | 744 |
| | ghost 2 | 11,960 / 13,456 / 16,140 | 2,175 | 415 | 7,506 (8,225) | 290 | 706 | 374 | 744 |
| | ghost 3 | 11,486 / 13,049 / 14,894 | 2,175 | 415 | 6,980 (8,097) | 294 | 608 | 374 | 744 |
| In-process, Editor, 302 s (`probe-load200-inprocess.json`; 5.9 kills/s, the owner's late-game peak is 4-7) | real | 13,320 / 14,857 / 17,749 | 2,190 | 525 | 8,099 (8,762) | 270 | 1,172 | 374 | 744 |
| | ghosts | 12,461-13,389 / 14,370-14,949 / 16,777-17,153 | 2,190 | 415-525 | 7,730-8,256 | 350 | 823-1,007 | 374 | 744 |

- **Uncapped, the highest 1 s window was 18,664 B/s** (in-process, real view). With the 1,250 B cap it was 17,749.
- **Frame size.** Wire per snapshot is 772-875 B p50 and 997-1,097 B p95. Uncapped frames reach 1,509-1,660 B, which
  breaks the 1,400 B rule (review change 11). The peaks come from leash relocations and new enemies arriving together.
  The 1,250 budget holds every frame at or under 1,250.
- **Enemies in view:** 108 (the cap) in every view. Under the 1,250 budget all 108 are written: 0 near updates stale,
  and the near and close update age is 0 ms p95 and max.
- **Cost per enemy:** about 4.5-5 B per moving enemy per snapshot (483-556 B for 108 enemies), with a 333 ms baseline.
- **Up:** the real client sends 809-889 B/s of payload. On the wire, with a 2 B baseline ack per input message, that
  is **1,187-1,305 B/s** (target <= 2,000).
- **Not counted.** TCP/IP and TLS headers, which M9b's wire figure also left out: about 60-70 B per frame, so about
  +1 KB/s at 15 Hz. Even then the p95 is 14-16 KB/s.

### What a smaller budget would starve (Docker; in-process in brackets)

| Budget (wire B per snapshot) | Wire p50 / p95 / max (B/s) | Near enemies stale | Near update age p95 / max | Own fx delay | Others' fx delay p95 / max | Fx dropped | Threats |
|---|---|---|---|---|---|---|---|
| none | 11,486-12,180 / 13,049-13,745 / 15,449-17,013 | 0 | 0 / 0 ms | 0 | 0 / 0 | 0 | sent |
| **1,250 (in tick)** | as above / max 14,894-16,525 | 0 | 0 / 0 ms | 0 | 0 / 200 ms | 0 | sent |
| 1,000 | 11,437-12,067 / 12,719-13,347 / 13,646-14,967 | 0 (0.1-0.2%) | 0 / 0 (67) ms | 0 | 0 / 267 ms | 0 | **mostly starved** |
| 850 | 11,109-11,779 / 12,596-12,733 / 12,751 | 0.2-1.2% (2.8-5.3%) | 0 / 67 ms | 0 | 133-333 / 333 ms | 0 | **starved** |
| 700 | 10,458-10,480 / 10,491-10,503 / 10,516 | 13-16% (22-30%) | 67 / 133-200 ms | 0 | 267-333 / 400 ms | 0 (7) | **starved** |

### Server tick (all Update work per 30 Hz tick, including the probe's in-tick part)

| Run | Tick p50 / p95 / p99 / max (ms) | Notes |
|---|---|---|
| **Docker, Mono, 60 fps** (`docker-load200`) | **6.60 / 9.06 / 10.57 / 35.76** | CPU 30% of a core mean, 58% max; RSS 239 MB. Includes the v5 send of all 200 enemies to the real client (27.5 KB/s), the probe's in-tick part (0.22 / 0.41 ms) and the 4 encoders (0.33 ms per snapshot) |
| Docker, Mono, no weapons (`docker-load200-noweapons`) | 7.60 / 9.41 / 11.39 / 36.39 | Nothing dies, so the 200 enemies crowd the ship: enemy shots rose from 5 to 24 a second. The build's own cost cannot be separated this way |
| Docker, Mono, `RP_SERVER_FPS=30` (`docker-load200-30fps`) | 4.27 / 10.79 / 17.63 / 34.42 | Inconclusive. One frame per tick halves the p50, but p95 and p99 got worse. The enemies also drifted off the real ship's view (6 in view, 1.2 kills/s), so this is not comparable. S1's driven 30 Hz clock is the right test of this lever |
| In-process, Editor (`probe-load200-inprocess`) | 14.74 / 17.64 / 18.82 / 25.26 | Editor overhead (M9b: Editor 1.7-3.6 vs Docker 0.87-1.48 p95) |

The run's own profiler markers in the Editor (`probe-load200-inprocess-markers.json`, 122 s, 8.7 kills/s) show where the
time goes:

- **the enemy world is 93%** of server work: `V4.Enemies.Frame` 354 of 380 ms/s. Of that, move is 133 ms/s, overlaps
  129 ms/s and behaviour 50 ms/s;
- **one ship's build is 3%:** weapons 2.8 ms/s plus player projectiles 9.3 ms/s.

The enemy world runs once per Unity frame, so at the server's 60 fps it steps twice per net tick.

**4-ship estimate.**

- Per added ship: its build (about 3% of the tick, 0.3 ms p95 in Docker), plus ship step, damage, pickups and its
  seat's encode (0.08 ms). Call it about 0.5 ms each, so +1.5 ms for 3 ships.
- Local Docker p95 is then about **10.6 ms**.
- Railway, using the M9b Railway/local ratio:
  - M9b p95 per voyage was 0.42-1.96 ms on Railway vs 0.87-1.48 locally, so the ratio is about 1.0 and at most 1.32;
  - that gives **10.6-14.0 ms p95** against 16 ms;
  - with a pessimistic 2x for a noisy shared vCPU it would be about 21 ms, which would fail.
- Not in the estimate:
  - enemies choosing between 4 targets (S3);
  - more enemy shots and contacts spread over 4 hulls;
  - the max: single ticks reached 34-36 ms locally, against Railway's 11-36 ms per voyage in M9b.

### Effect rates of the six-weapon level-8 build (calls per second, Docker / in-process)

- Projectiles 5.2 / 7.0, with as many ends; fired 2.4 / 3.2; projectile corrections 0.8 / 0.1.
- Lightning 1.0 / 1.5; rings and impacts 0.6 / 0.7 each; side salvos 0.3 / 0.5; splashes 3.2 / 4.1.
- Pickups 2.1 / 7.3; summon moves 92 / 272 (the frame rate's calls, sent as 4 B per summon per snapshot).
- Enemy shots and splashes 5.2 / 9.2 each.
- On the wire, all effects are 0.9-1.4 KB/s per client: own 0.2-0.35 KB/s, others' and the world's 0.6-1.2 KB/s.

## What the design should take from this

1. **Keep the plan's 1,250 B per-snapshot budget.** At this load it never starves anything. Its job is to cap frame
   peaks: uncapped frames reach 1.5-1.7 KB, above review change 11's 1,400 B. A 1,000 B budget would also meet D9c.1 with
   almost no stale enemies. 850 is the floor before near enemies go stale (1-5%). 700 fails the quality floor (13-30%
   stale, near enemies up to 200 ms old).
2. **Threats need aging.** At budgets of 1,000 and below, the 2 Hz minimap message (about 200 B) was starved almost
   entirely, because it sits last in the order without growing in priority. S6 should age it as the enemies are aged, or
   keep a small reserved slot.
3. **Delta encoding and the 108 cap are required.** v5 at 200 enemies is 27-29 KB/s for one client. Enemies are 60% of
   the delta-encoded stream (7-8 KB/s). The interest radii (75 m near, 130 m far) can stay. Over WSS (TCP: in order),
   encoding against the previous snapshot instead of the acked one would shrink the steps further. The acked baseline
   only matters on a lossy transport, which also needs a resend of entities changed since the baseline (noted in
   `NetEnemyDelta`).
4. **Fx aggregation as prototyped is enough.** All effects together are under 1.5 KB/s per client. Folding salvos and a
   kill's drop into one event matters most (pickups were 7-75 calls a second, depending on the kill rate). Summons
   (parrots) should go as small entities per snapshot, not per frame.
5. **The tick is the tighter target, so S1, S3 and S9-S10 must watch it.**
   - S1's driven 30 Hz clock should step the enemy world once per tick instead of once per frame at 60 fps. That is the
     largest lever, since the enemy world is 93% of the work.
   - S3 must measure nearest-ship targeting at 4 ships against this baseline.
   - S9-S10 must confirm on Railway. Fallbacks, in order: the 30 Hz world step, the overlap pass (a third of the enemy
     time), IL2CPP (untried), then a lower server cap (160).
6. **Several sessions per process (S7)** cannot fit two `load200` sessions in a 16 ms frame on Mono at this cost (9 ms
   p95 each). Plan one heavy session per process, or more only after the S1 saving.

## Conditions for continuing M9c

- **S1:** measure the tick at load200 with the driven clock. The enemy world should step once per net tick; target p95
  of 7 ms or less locally.
- **S3:** re-run this probe with real ships instead of ghosts. Local Docker p95 must stay at or under 12 ms (16 ms
  divided by Railway's 1.32).
- **S6:** implement the 1,250 B budget with priority aging for threats, keep frames under 1,400 B, and use the delta
  encoding prototyped here.
- **Load test:** the go/no-go stays with the 4-real-client load test on Railway (S9/S10, D9c.1).

## Tests and checks

- **EditMode `V5NetLoadProbeEditModeTests`: 3/3 passed** (`editmode-probe.json`).
  - Delta round trip over 400 snapshots with 5-snapshot-late baselines, 30% starvation, removals and returns: the
    client's table equals the server's model every snapshot.
  - Per-enemy cost: 3 B moving, 5 B turned and hurt, 9 B new.
  - The preset is refused without `RP_ALLOW_TEST_PRESETS=1` (any other value refused too).
- **EditMode `RoguePirates.V5.Net.Tests`: 36/36 passed** (`editmode-v5net.json`).
- **PlayMode `Load200_KeepsTwoHundredEnemies_AndMeasuresFourViews`: passed in 17.6 s** (`playmode-smoke.json`).
- **PlayMode measurement tests.** `Probe_Load200_FourShips_FiveMinutes` and `Probe_Load200_NoWeapons` are skipped
  unless `RP_PROBE_SECONDS` is set in the Editor's environment. The 300 s run passed (`playmode-probe-inprocess.json`).
- **Docker.**
  - Image: `rogue-pirates-match:local`, also tagged `ghcr.io/x2c-fun/rogue-pirates-match:s0b-probe-local`. Local only;
    not pushed, nothing on Railway.
  - Command:
    `python server/match/local_voyage_test.py --seconds 320 --drop-at 0 --skip-extra-checks --server-env RP_ALLOW_TEST_PRESETS=1 --server-env RP_TEST_PRESET=load200 [--server-env RP_PROBE_WEAPONS=0 | RP_SERVER_FPS=30]`.
- **Fingerprints (`V5VoyageFingerprintPlayModeTests`, `V5NetVoyageFingerprintPlayModeTests`): NOT re-run.**
  - The run started at 12:27 IST and never got going. The Editor's main thread hung behind a modal "Package Manager
    (busy ...)" dialog, which appeared after the Linux server build's platform switch and the revert of its settings
    churn. Its timer froze at about 39 minutes, and pipeline commands time out.
  - The Editor was left as it is; it was not killed.
  - Risk: the shipped-path change is a null-checked hook call and a size property in `NetServer`, and no gameplay code
    changed. Re-run both after the Editor is restarted, before S1 starts.
