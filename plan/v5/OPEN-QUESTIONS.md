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
