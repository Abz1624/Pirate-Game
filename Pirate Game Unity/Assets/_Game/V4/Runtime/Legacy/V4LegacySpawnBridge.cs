using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Data;
using HighSeas.Enemies;
using HighSeas.Player;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// Spawn half of the bridge: with <see cref="V4LegacyBridge"/> the only V4 code that drives the legacy
    /// <see cref="EncounterSpawner"/> and V3 spawn directors. Deleted in M3 when the V4 Spawn Controller takes over.
    /// </summary>
    [DeletedInPhase(3, "V4 Spawn Controller and Wave Timeline")]
    public sealed class V4LegacySpawnBridge
    {
        /// <summary>V3RunBootstrap.ResetSaltwindDirectors, spawner half (overtime/storm run here too for order).</summary>
        public void ResetSpawnDirectors(EnemyWorld enemyWorld, PlayerShipController ship)
        {
            // Static and only cleared by V3PressureBeatDirector.ResetForRun, which does not exist yet on a fresh scene
            // load; dying mid-arena would otherwise block ambient spawns in the reloaded run (R-68).
            V3MinibossArena.ResetForRun();
            var spawner = Object.FindFirstObjectByType<EncounterSpawner>();
            spawner?.ResetForNewRun();
            ConfigureEncounterSpawner(CombatExperimentContext.Active);
            Object.FindFirstObjectByType<V3PressureBeatDirector>()?.ResetForRun();
            Object.FindFirstObjectByType<V3RouteEventDirector>()?.ResetForRun();
            var overtime = V3OvertimeDirector.Ensure(enemyWorld, spawner, ship);
            overtime.ResetForRun();
            V3SaltwindStormDirector.Ensure().ResetForRun();
        }

        /// <summary>V3RunBootstrap first-run director creation: pressure beats, overtime, route events.</summary>
        public void EnsureSpawnDirectorsFirstRun(EnemyWorld enemyWorld, PlayerShipController ship, System.Action resetBossDirector)
        {
            var systems = GameObject.Find("Systems");
            if (systems != null)
            {
                var spawner = systems.GetComponent<EncounterSpawner>();
                if (spawner == null) spawner = Object.FindFirstObjectByType<EncounterSpawner>();
                var beats = systems.GetComponent<V3PressureBeatDirector>();
                if (beats == null) beats = systems.AddComponent<V3PressureBeatDirector>();
                beats.Initialize(enemyWorld, spawner, ship != null ? ship.transform : null);
            }

            var spawnerForOvertime = Object.FindFirstObjectByType<EncounterSpawner>();
            var overtime = V3OvertimeDirector.Ensure(enemyWorld, spawnerForOvertime, ship);
            overtime.ResetForRun();
            V3SaltwindStormDirector.Ensure().ResetForRun();
            resetBossDirector?.Invoke();
            V3RouteEventDirector.Ensure();
        }

        static void ConfigureEncounterSpawner(CombatExperimentProfile profile)
        {
            var spawner = Object.FindFirstObjectByType<EncounterSpawner>();
            spawner?.ConfigureV3Pacing(true);
            spawner?.ApplyCombatExperiment(profile);
        }

    }
}
