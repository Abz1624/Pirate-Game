# Recorded sessions

One JSON file per voyage, written by the game's session recorder
(`Pirate Game Unity/Assets/_Game/V4/Runtime/Telemetry/SessionRecorder.cs`). It records voyages only, not 1v1 matches or
the Module Lab. The file is saved once, when the ship sinks, when the results screen shows, or when you leave the voyage
(menu or restart).

## Playtest builds only (switch it off for a public build)

Session files are for playtest builds (V5 M8z, 7 Oct, D20). One switch controls them:
`downloadSessionFiles` on `Pirate Game Unity/Assets/_Game/Resources/DemoJuiceSettings.asset` (Inspector: the "Download Session Files" box,
under "Unlock Everything"). It is **on** now.

- **On:** every voyage records its session; the finished voyage saves or downloads its file (below), and the results
  screen shows "Download voyage data" and the main menu shows "Download last voyage".
- **Off (public build):** nothing is recorded, saved or downloaded, and both buttons are hidden. To make a public
  build, untick the box (or set `downloadSessionFiles: 0` in the asset), save, then build. Tick it again for the next
  playtest build.

Tests run with the demo switches off, so they never record sessions.

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

`data.json` is the game's config set exported by the Unity Editor (menu RoguePirates > V5 > Balance: export the Monte
Carlo data, or `unity command eval "return RoguePirates.V4.Editor.MonteCarloExport.Run();"`); `node check_export.mjs`
compares it with the YAML fallback (`export_data.py`) and lists any field that differs. `node fit_sessions.mjs` refits
the model's constants on the recorded sessions in this folder.

The script runs `sim.mjs` for the session's captain and starting weapon, replaying the session's level-up picks. A
voyage listed in `montecarlo/session_data.mjs` (the five fitted voyages) is replayed on the config it was played on
(`montecarlo/session-data/`, rebuilt with `node session_data.mjs`; `--today` forces `data.json`); any other session runs
on `data.json`, today's config. When you record a new voyage, add it to `SESSIONS` in `session_data.mjs` (its commit and
the export to snapshot) and rerun `node fit_sessions.mjs`. It uses 200 seeds and summons the
boss at the session's Beacon time. It prints the session and the model side by side: level, kills, hull and damage each
minute, level-up times, elite, miniboss and boss fights, damage by source, chests and the end. Then it lists the biggest
mismatches and which model constants would close them. It measures this by changing each constant by +30%.

Options:

- `--export` rebuilds `data.json` from the YAML fallback first (`python export_data.py`), when no Editor is open. The script warns you when the
  assets are newer than `data.json`.
- `--seeds 400` runs more seeds.
- `--set accuracy=0.5` tries a model constant. You can repeat it.
