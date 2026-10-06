# Recorded sessions

One JSON file per voyage, written by the game's session recorder
(`Pirate Game Unity/Assets/_Game/V4/Runtime/Telemetry/SessionRecorder.cs`). It records voyages only, not 1v1 matches or
the Module Lab. The file is saved once, when the ship sinks, when the results screen shows, or when you leave the voyage
(menu or restart).

## Where the files land

- **Editor:** in this folder, as `session-<yyyyMMdd-HHmmss>-<captain>.json`. The Console logs the full path.
- **Desktop build:** in this folder when the build is inside the repo. Otherwise in
  `<persistentDataPath>/sessions/`. The log shows the path.
- **WebGL:** the browser downloads the file, so look in your Downloads folder and move it here. The last session is also
  kept in PlayerPrefs (`rp.session.last`), and the browser console logs a one-line notice.

## What is in a file

- `header`: date, platform, captain, starting weapon, seed, config hash, balance version, end reason
  (`sunk`, `quit` or `victory`), end time, and when the boss was summoned and beaten.
- `samples`: one every 10 s of run time, with level, XP, total kills, gold, hull and max hull, damage taken and heals so
  far, enemies alive, elites alive, theoretical DPS, offense and Pressure.
- `events`: level-ups, picks (kind and id), chests, points of interest, items, evolutions, heals, the Beacon and the boss.
- `focus`: every elite, miniboss and boss, with its spawn time, kill time and HP at spawn.
- `damageTaken.bySource`: damage taken by enemy id. Other sources are `island`, `storm`, `hazard`, `stage_hit` (the storm
  in hit mode), `boss_director` (the boss's slams, marks and spread shots) and `other` (barrels and anything untagged).
- `heals` and `weapons`: heals split into pickups and regen, and each weapon's damage, kills and hits.

## Comparing a session with the balance model

```
cd plan/playtest/montecarlo
node compare_session.mjs ../sessions/session-20261006-153000-rook.json
```

The script runs `sim.mjs` on `data.json` for the session's captain and starting weapon. It uses 200 seeds and summons the
boss at the session's Beacon time. It prints the session and the model side by side: level, kills, hull and damage each
minute, level-up times, elite, miniboss and boss fights, damage by source, chests and the end. Then it lists the biggest
mismatches and which model constants would close them. It measures this by changing each constant by +30%.

Options:

- `--export` rebuilds `data.json` from the Unity assets first (`python export_data.py`). The script warns you when the
  assets are newer than `data.json`.
- `--seeds 400` runs more seeds.
- `--set accuracy=0.5` tries a model constant. You can repeat it.
