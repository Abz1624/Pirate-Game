# Netcode for the 4-player Hunt — research, 30 Sep 2026

Question: can each option run a Hunt with 4 browser (WebGL) players, ~200 enemies simulated on the server and at most
~60–80 enemies visible per player, with server authority (wagers) and hosting on Railway (WebSockets and TCP, outbound
UDP, no inbound UDP)? Collected by a research agent; Photon pages read in a browser (they block plain fetches).

## Findings

- **Photon Fusion 2, Server Mode.** Photon's topology guide: "We strongly recommend Shared Mode on WebGL… Server and Host
  mode are not recommended and by default disabled"; the switch "can lead to degraded performance"; Shared is "the only
  mode that does not need to run resimulations", so Server-Mode browser clients pay for prediction resimulation on one
  thread ([fusion-choose](https://doc.photonengine.com/fusion/v2/fusion-choose)). Browsers can't do direct UDP, so all
  traffic goes through Photon's relay. Railway allows outbound UDP (Railway staff, Apr 2025), so the server can reach the
  relay; untested. Many units: Photon advises against one NetworkObject per projectile and recommends a networked array
  in one behaviour ([projectiles essentials](https://doc.photonengine.com/fusion/v2/technical-samples/projectiles-essentials));
  area of interest works per NetworkObject, so enemies would be grouped in sea sectors.
- **Photon Quantum 3.** Deterministic ECS; only inputs are sent, but **every client simulates every enemy** (plus
  rollbacks), on one thread in WebGL; no published entity benchmark on low-end phones. Gameplay must be rewritten in
  Quantum's DSL with fixed-point math. Results can be checked without Enterprise by replaying captured inputs in a .NET
  runner fed by replay webhooks ([cheat protection](https://doc.photonengine.com/quantum/v3/manual/cheat-protection),
  [replay](https://doc.photonengine.com/quantum/v3/manual/replay)); the SDK licence for self-hosting the runner was not
  checked. Every client holds the full state, so hidden positions can be read ("perfect information problem").
- **Unity Netcode for GameObjects 2.x / 3.0** (3.0 released 21 Sep 2026). Interest management is manual (visibility
  callbacks). Community: one serialized stream for 100 units cut traffic from 250 KiB/s to 25 KiB/s with compression.
  WebGL clients connect over WebSockets (WSS) to a Linux server.
- **Fish-Net 4.7 + Bayou.** Built-in observer conditions: distance, grid (hash grid), scene, match; non-observers get
  nothing. Prediction v2 built in. Pro ($60 lifetime): lag compensation, distance-based send-rate LOD, code stripping.
  Bayou: WebSocket transport with WSS, Linux servers; last commit Sep 2025.
- **Nakama.** Match loops at 1–60 Hz in Go/TypeScript; enemy AI and collision would be rewritten outside Unity (two
  gameplay codebases).
- **Bandwidth estimate** (after [Gaffer on Games](https://gafferongames.com/post/snapshot_compression/)): ~8 bytes per
  enemy quantized, 80 enemies with delta compression at 10–20 Hz ≈ **6–17 KB/s per player** including WebSocket and TLS
  overhead; the server sends ~60 KB/s for 4 players; Railway egress $0.05/GB ≈ $0.01 per match-hour.

## Summary

| Option | 4-player Hunt with ~200 server enemies? | What we build | Main risk | Railway |
|---|---|---|---|---|
| Fish-Net 4.7 + Bayou | Yes, with batched enemy snapshots (or the grid condition + Pro LOD) | Headless Unity server; direct WSS | Small maintainer team; Bayou not updated for a year; key features in Pro ($60) | Yes |
| Netcode for GameObjects | Yes, with batched enemy snapshots | Headless Unity server; direct WSS; our own interest management | Hand-written interest management; 3.0 is brand new | Yes |
| Photon Fusion 2 (Server Mode) | Yes, with sector arrays | Headless Unity server; all browser traffic via Photon relay | Photon calls it "not recommended" for WebGL; relay latency; resimulation cost on low-end phones | Likely (untested) |
| Photon Quantum 3 | Hard | Full gameplay rewrite in fixed-point ECS | Every low-end phone simulates all enemies; full rewrite; whole map visible to hackers | Game server is Photon's; verifier on Railway |
| Nakama | Yes, after rewriting enemy AI | Go match handler; Unity as view only | Two gameplay codebases | Likely |
