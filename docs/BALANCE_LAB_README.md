# High Seas Balance Lab

The Balance Lab is a deterministic, single-player Monte Carlo tool for tuning Rogue Pirates without editing production assets or playing every candidate build manually.

Open it in Unity with **HighSeas → Balance Lab**.

## Standard workflow

1. Select a weapon, enemy, and boss in **Authored content snapshot**. Their current `GameDatabase` values are copied into the scenario only; the lab never writes back to assets.
2. Click **Snapshot all weapons as equipped loadout**, **Snapshot full enabled enemy roster**, and **Snapshot authored scripted waves** when testing the full current stage.
3. Choose a player-skill preset. Use `Average` as the tuning baseline; check `Conservative` and `Expert` before accepting a change.
4. Set seed count to at least 100 and click **Run Batch**.
5. Read survival, boss completion, level, gold, peak enemy pressure, automatic warnings, percentile reliability, and the sample-run curves.
6. Click **Capture Baseline**, change one or more parameters, then **Compare to Baseline**. Treat a survival or boss-win decrease greater than five points as a likely regression unless it is intentional.

## Balancing routes

- **Combat:** weapon loadout entries use separate damage, cadence, range, projectile speed, target coverage, and size. Upgrade-path settings apply a per-level improvement and an evolution power spike.
- **Director:** the lab can use weighted authored roster spawning or replay the snapshot of `SpawnDirectorSettings.waves`.
- **POIs and economy:** configure route rewards, travel time, interaction risk/failure, gold drops, merchant timing, item/reroll prices, and item power.
- **Bosses:** boss hull, damage, speed, behavior class, attack cadence, preferred range, and spawn time are part of the scenario.

## Regression presets and targets

Use the regression dropdown for the first three minutes, minute-four pulse, minute-seven pulse, boss arrival, or overtime. Set target bands for average-player survival and merchant affordability. The lab reports `PASS`, `OUTSIDE`, or `LOW` after each batch.

Save a scenario with **Save Scenario Asset**. Save it under `Assets/BalanceScenarios/` (or another project folder) so it can be committed with its tuning change. Load it later with **Load Scenario Asset**.

## Live calibration

During a V3 run, `RunCombatTelemetry` exports JSON under the platform persistent-data `telemetry` folder. The export includes damage, offers, items, gold, POIs, captain actives, pressure events, overtime events, and a timestamped event stream.

Run the corresponding lab batch, then click **Import Live Telemetry** and select the exported JSON. The report compares simulated and live gold, peak enemy pressure, and event-timeline coverage. Use this to correct assumptions before changing balance values.

## Sensitivity and build diversity

Click **Sensitivity + Diversity** after a batch. It reports how a ten-percent change to hull, weapon damage, enemy hull, enemy damage, or spawn density changes survival. It also identifies whether the configured loadout contains duplicate weapon slots. Use **Analyze Offers** to inspect offer frequencies and likely trap paths.

## CI

`BalanceLabCiGate.MeetsTargets(scenario, out report)` is a pure deterministic pass/fail API for a versioned scenario. Add a project EditMode test that loads your approved scenario and calls this API. Then run:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Kshitij\Work\X2C\web3\GitHub\Pirate-Game\Pirate Game Unity" -runTests -testPlatform EditMode -testResults balance-test-results.xml
```

The included EditMode test verifies the CI gate interface is deterministic. Keep production balance targets in explicit scenario tests so intentional difficulty changes are reviewed rather than silently accepted.

## Limits

The lab is a decision tool, not a substitute for playtests. It simulates authored behavior and timing deterministically, but visual readability, input feel, player understanding, and unusual edge-case interactions still require real single-player sessions.
