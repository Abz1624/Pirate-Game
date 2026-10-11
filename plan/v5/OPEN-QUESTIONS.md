# Open questions (7 Oct 2026)

The older questions 1-45 (`DECISIONS-WHILE-AWAY.md`) were reviewed against the playtest decisions D18-D20. Most are
settled or moot; these still need your answer. Each has a suggestion (my default if you just say "go with your
suggestions").

## Gameplay

| # | Question | Suggested |
|---|---|---|
| Q16 | Pickup sparkles on loot barrels: fewer particles, or keep them? | Keep (desktop is the main target now) |
| Q17 | The Knockback upgrade card: six weapons can roll it and it does nothing for them. | Take it out of those weapons' upgrade pools |
| Q19 | Ramming Prow needs the boost button, against the "auto-attacks" rule. Keep it? Should enemies still hurt the ship while it rams? | Keep it; the ship takes no contact damage while ramming |
| Q20 | New weapons' rank 4/8/12 milestones (an extra anchor, a second pulse...): keep as upgrade steps, or drop? | Keep |
| Q23 | Should the Harpoon's tar become a real slow? | Yes |
| Q24 | The Reef Crab (miniboss): slowed and knocked back like regular enemies, or treated like the boss (half slow, immune to knockback)? | Like the boss |
| Q25 | Should slows also shorten enemies' charge dashes? | No (a dash keeps its telegraphed reach) |
| Q26 | The flame reaches exactly its damage cone (9.5 m). Does it look right and dense enough? | Check in your playtest |
| Q27 | Luck: one scale for level-up cards and chests? | One scale (a fraction) |
| Q28 | The four designed powerups (Battle Signal, Tailwind, Salvage Chime, Full Sail): which mix? | All four, at today's numbers |
| Q32 | Admiral: elites only, or the boss too? | Elites and the boss |
| Q5 | New enemy types with new art: which phase, and who makes the models? | After M9; the art team |

## Menu and content

| # | Question | Suggested |
|---|---|---|
| Q37 | Collection: from Profile (built), or its own button on the art menu (needs the art team)? | From Profile |
| Q38 | "New" badges and sales: which content, and when? | Later, when locks come back |

## Platforms and performance (phones are on request; desktop WebGL is the main build)

| # | Question | Suggested |
|---|---|---|
| Q33 | Quality tiers: the same gameplay caps on every device, or fewer player projectiles on a Low phone? | Same caps everywhere |
| Q34 | The 1024 texture cap: mobile only, or the desktop web build too? | Mobile only (desktop gets sharper textures) |
| Q42 | Health bars sort far to near every 8 frames (a near bar can sit under a far one briefly). Fine, or every frame? | Every 8 frames |
| Q40, Q44 | Phone builds: ASTC effects and UI art; the Android 1024 UI cap. | When you next ask for a phone build |

## Settled or moot (no answer needed)

1, 4, 6, 7, 8, 9, 11, 12, 21 (D18: the playtest rules replaced M3c/M3d and the evolution level); 2, 14 (the enemy cap is
the playtest's 108); 3, 10 (overtime follows the playtest's Drowned Pursuit ramp); 13 (D19: an early boss keeps its 14:00
hull); 15 (bars on elites, minibosses and the boss); 18, 29, 36 (D18: everything free for now); 22 (D20 retuned offers);
30, 31 (armor and gold gain apply); 35 (desktop is the main build); 39, 41, 43, 45 (answered 2 Oct).

## While you were away (11 Oct): decisions taken and questions

Decisions I took to keep going (say if you want any changed); questions marked **Q** need your answer.

- M9b closes on your 11 Oct re-test (smooth enemies, tab switching fine, no pop-ins); the ram jolt is tracked in TODO.
- Railway redeploy during a voyage (11 Oct 02:50): the client got `end ServerClosing` and was back in the lobby at once,
  so D9b.1's "a redeploy ends the session cleanly" is met (`plan/v5/phase-09b-netcode/railway/wss-redeploy/`).
- Railway clean-up (11 Oct, D23/D26): the `match-test` service is deleted and the API's `RP_MATCH_TICKET_SECRET`,
  `VOYAGE_SERVER_URL` and `VOYAGE_MATCH_ID` are removed (also from `.railway/railway.ts`). The whole M9b Railway test
  cost about $0.17 (38 metered hours).
- **Q (small):** Railway's own usage export needs your account (the project token can't read it). If you want it in the
  M9b memo, download it from the workspace Usage page; otherwise the $0.17 estimate from Railway's metrics stands.
- M9b independent review (11 Oct): fail as it stood, pass with fixes. Being fixed: the determinism test (likely the
  shark and treasure-raider timers reading frame time inside the simulation, which also made those phases last about
  twice as long on the server), timing-free gate tests, a steer clamp, leash settings in config with a safe fallback.
- **Q:** do you accept M9b's frame-rate target as met at the page's normal size (58-60 fps; full-screen 1080p is 40-42 on
  this kind of laptop GPU, where the normal game does 15-17), and the 0.5 m correction target with enemy rams excepted
  (the ram jolt to-do)? My default: yes, recorded as D29 (provisional until you confirm).
- **Q:** ticket replay across server restarts: a ticket caught within its 2-minute life could be replayed on the next
  server process. Nothing of value moves yet. My default: fix it in M13b (each server process gets its own match id from
  the API), not M9c.
- **Q:** the Unity Transport fix is a local patch to Unity's package (Unity's licence says Unity owns the modified copy,
  which is fine for us). Should I report the bug to Unity publicly (a forum post or bug report under your account)?
  My default: no public post until you say so; the patch file makes re-applying it easy.
- M9b determinism fix (11 Oct, `4c050833`): the same seed gave different spawns because gameplay read the ship's
  drawn (visually smoothed) position and the drawn camera; gameplay now reads the simulated position and a hidden
  simulation camera. What you see is unchanged.
- **Q:** sharks and treasure raiders now recover and flee on the game's own 1.2 s timer whatever the frame rate. Before,
  on 120-144 Hz screens those phases lasted 2-2.4 times longer (and half as long at 30 fps), so on fast screens sharks
  now come back sooner. My default: keep the fixed timer (it is what the config says, and the server matches it).
- **Q:** is replaying a run log only at a fixed frame time enough, or must it replay identically at any frame rate (only
  needed if the server ever re-simulates runs)? My default: fixed frame time is enough for now.
- **Q:** after a page hitch (e.g. a 1 s freeze) the server may guess your inputs for up to about 2.5 s before the client
  catches up (measured 0.1-1.4 s alone, 2.3-2.5 s inside the full test suite; the test allows 3 s). My default: accept.

### M9c design (11 Oct) — defaults I'm building with; please confirm or change

Full plan: `plan/v5/phase-09c-multiship/PLAN.md` ("Product decisions"). The ones that most shape the game:

- **Q (M9c-1):** shared voyage = co-op PvE (no friendly fire, ships pass through each other). PvP stays in M13b/M22.
- **Q (M9c-2):** level-up cards in a shared voyage: the voyage never pauses; your ship keeps sailing and firing while a
  compact card strip is up (pick with 1/2/3, click or tap; no timeout; sets queue). Solo server voyages keep the pause.
- **Q (M9c-3):** hidden tab in a shared voyage: your ship is stowed (out of play, faint marker), returns with a 3 s
  shield; removed after 10 minutes.
- **Q (M9c-4):** difficulty per extra ship: about 75% more spawns (cap 200 enemies), elites +50% HP, boss +75% HP,
  scheduled fights +50% count; normal enemy HP and enemy damage unchanged.
- **Q (M9c-5, flagged):** XP, gold and health go to whoever collects them (a config switch can make XP shared instead).
  Co-op games often share XP; which do you want?
- **Q (M9c-6):** chests: everyone gets the prompt, no pause, first Take wins and the taker pays.
- **Q (M9c-7):** sinking: you spectate the others; the voyage ends when everyone has sunk; no revives in M9c; anyone's
  Beacon summons the boss.
- **Q (M9c-8):** shared voyages get run-check verdicts per player but don't go on the weekly leaderboard.
- Smaller, decided by default: seats fixed per match (no late join), start when all are in or 20 s after the first;
  several sessions per server process with separate physics scenes; ram jolt smoothed (not predicted); a hard per-player
  bandwidth cap; shared fog and Beacon discovery; Railway spend under $1; keep NGO 2.13.3 and the patched UTP 2.7.4.
- M9c plan review (11 Oct): approved with changes; adopted in `PLAN.md` ("Changes from the plan review"). Notably: two
  gated commits (M9c-1 after the single-player-neutral refactor, M9c-2 after shared voyages), an early bandwidth/CPU
  probe before the big refactor, the hidden-tab default changed to "hold, still targetable", shared-voyage run checks
  kept offline (no live API change), ram jolt smoothing now required.
- **Q (M9c-9):** does each player's view keep the 108-enemy device cap in shared voyages (my default: yes; the server can
  still simulate up to 200)?
- **Q (M9c-10):** is co-op PvE meant to become a product mode, or is it only the test vehicle for real-time play? If only
  a test vehicle, I'll keep its rules minimal.
- **Q (M9c-11):** several voyages per server process: in-process (separate physics scenes, saves 150-200 MB per extra
  voyage, about 20 h) or a supervisor running several server processes in one container (about 5 h, more memory)? My
  default: in-process, as M13b needs it cheap.
- **Q (M9c-12):** accept the M9c-1 / M9c-2 split, with your solo playtest at M9c-1? My default: yes.
- M9c S0 (11 Oct): three single-player fingerprints frozen (plain voyage, an eventful voyage that ends sunk in
  overtime, a solo server voyage). Three tiny determinism fixes in shipped code (the ship's collision size read in a way
  that wobbled with its drawn rotation; two hit-immunity timers rounding differently late in a session); no change you
  would feel. Open: the solo server fingerprint gave a second, repeatable result in 2 of 13 runs; until S1 finds the
  cause, that test accepts the golden or that one known variant and logs which.
- M9c S0b (11 Oct): early 4-ship probe on the M9b server: **GO with conditions** (`phase-09c-multiship/s0b/PROBE.md`).
  Bandwidth about 12 KB/s p50, 13-15 KB/s p95 per player (target 20), but only with a new delta encoding and the 108
  cap per view (today's protocol would send 27-29 KB/s). Server tick about 9 ms p95 locally with one real ship; estimated
  10.6-14 ms on Railway at 4 ships against 16: the tight part. Conditions: S1 steps the enemy world once per 30 Hz tick
  (target <= 7 ms p95 locally), S3 re-runs the probe with real ships (<= 12 ms locally).
- The Unity Editor hung on a Package Manager dialog during S0b; I stopped it and restarted it. Its scene-recovery
  backup (one small file, most likely the test runner's temporary scene) is kept in
  `Documents/RoguePirates-scene-backups/20261011-1318` rather than recovered or discarded. Delete it if you don't need it.
