# 01 — Decision Register

Entries marked **LOCKED** are implementation requirements. A change requires a new dated entry, affected-document updates, migration analysis and regression tests.

| ID | Status | Decision |
|---|---|---|
| DEC-001 | LOCKED | Runs continue after boss success and end only when the player dies. |
| DEC-002 | LOCKED | Each stage has a 10-minute soft timer and Drowned Pursuit overtime. |
| DEC-003 | LOCKED | Tier 1/2/3 Voyages contain 1/2/3 stages; bosses open intermediate portals. |
| DEC-004 | LOCKED | One global weekly Voyage board ranks best single-run kills; no biome, tier or progression brackets. |
| DEC-005 | LOCKED | Fresh ordinary Voyage layouts; persistent concentric home; raids permute the owner's section set, attachments and authored encounters. |
| DEC-006 | LOCKED | No mandatory functional-building placement. Progression uses the Captain's Ledger and one Treasury Beacon. |
| DEC-007 | LOCKED | Valid death banks 100% of unspent run Gold. |
| DEC-008 | LOCKED | Gold and Rum are the only gameplay currencies. |
| DEC-009 | LOCKED | No player trading, marketplace, wallet, blockchain inventory or external trading link. |
| DEC-010 | LOCKED | Items/cosmetics are account-bound; permanent Item ranks give at most +5% to one magnitude. |
| DEC-011 | LOCKED | Three initial home masteries: Fast Flag, Lightly Armed, Steady Hands. |
| DEC-012 | LOCKED | A player may change Crew freely outside an active Crew event; active-event membership is locked; queued leave executes at event end before a new lock. |
| DEC-013 | LOCKED | Event submissions are timely when the complete run reaches the server by the cutoff; verification may finish later. |
| DEC-014 | LOCKED | External partner rewards are manually administered; no integrated payouts. |
| DEC-015 | LOCKED | Monetization is known-value account-bound cosmetics and a cosmetic pass; no paid power, revival, score, Gold/Rum, Item progress or acceleration. |
| DEC-016 | LOCKED | Steam has no ads; native/direct has optional rewarded ads; portals may show bounded between-run interstitials. |
| DEC-017 | LOCKED | Unity with 3D assets targets Android, iOS, desktop/mobile web, Steam, CrazyGames and Poki. |
| DEC-018 | LOCKED | Use stylized modular 3D without compromising animation, VFX, audio or interaction polish. |
| DEC-019 | LOCKED | Target players aged 13+; event prize eligibility can be stricter. |
| DEC-020 | LOCKED | Use a quality-gated development program and one coordinated public launch. |
| DEC-021 | LOCKED | Use “The Sea Keeps Receipts,” Mags and Crumb as the light narrative baseline. |
| DEC-022 | LOCKED | All assets under `references/**` are excluded from production use. |
| DEC-023 | LOCKED | PostgreSQL is the canonical database; client storage is only cache/local prototype state. |
| DEC-024 | LOCKED | Boss/player death in the same tick secures the boss objective, then ends the run. |
| DEC-025 | LOCKED | Raids settle theft exactly once at boss death and start protection immediately; later survival cannot steal again. |

## Recorded implementation defaults

These defaults are mandatory for the first measurable build and may change only through telemetry-backed decision entries.

| ID | Default |
|---|---|
| DEF-001 | 30 Hz simulation; stable integer entity IDs and named saved RNG streams. |
| DEF-002 | 180 ordinary enemies, 12 elites, one boss, 600 projectiles, 80 area effects, 200 pickup entities. |
| DEF-003 | Landscape 1920×1080 UI reference, 48 logical-pixel minimum target, safe inset ≥24 logical pixels. |
| DEF-004 | Pause allowance for competition is 30 unioned wall-clock minutes; 24 continuous paused hours expires a run. |
| DEF-005 | Input uploads every two active seconds and at choice, boss, portal, pause and death boundaries. |
| DEF-006 | Save/checkpoint every ten active seconds and at every permanent milestone. |
| DEF-007 | Ordinary replay retention 30 days; prize/disputed replay retention 90 days, pending privacy review. |
| DEF-008 | Minimum performance is stable 30 FPS; 60 FPS is recommended-device target. |
| DEF-009 | Initial asset/download target ≤20 MB compressed and WebAssembly heap target ≤256 MB, subject to Gate 0 measurements. |
| DEF-010 | Direct web uses guest + email magic link; native uses Apple/Google; Steam and portals use their platform identities. |

