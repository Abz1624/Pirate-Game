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
    /// The only V4 class that drives the legacy <see cref="EncounterSpawner"/> and <see cref="WeaponController"/>.
    /// Spawn path: deleted in M3 (Spawn Controller). Weapon path: reduced to a switched-off loadout record in M5,
    /// deleted with the class in M9 (Build &amp; Progression).
    /// </summary>
    [DeletedInPhase(9, "Spawn path removed in M3; weapon record removed with LevelSystem in M9")]
    public sealed class V4LegacyBridge
    {
        // ---- Spawn path (deleted in M3) -------------------------------------------------------------

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

        // ---- Weapon path (record-only from M5, deleted in M9) ------------------------------------

        public WeaponController Weapons { get; private set; }

        /// <summary>V3RunBootstrap weapon setup: bind owner, experiment slot cap, profile slot cap, starting weapon.</summary>
        public WeaponController SetUpWeapons(PlayerShipController player, CombatExperimentProfile experiment, GameDatabase database, string startingWeapon)
        {
            Weapons = V3MultiShipWeaponLoadouts.FindLocalHumanWeaponController() ?? Object.FindFirstObjectByType<WeaponController>();
            if (player != null) Weapons?.BindOwner(player);
            Weapons?.ApplyCombatExperiment(experiment);
            if (StageBalanceProfileContext.IsActive && StageBalanceProfileContext.Export?.Progression != null)
                Weapons?.EnsureWeaponSlotCapacity(StageBalanceProfileContext.Export.Progression.WeaponSlotCap);
            Weapons?.Initialize(database, startingWeapon);
            return Weapons;
        }

        /// <summary>LevelSystem mirrors its owned loadout onto the weapon controller (V3RunBootstrap L256).</summary>
        public void SyncLevelSystemLoadout() => HighSeas.UI.LevelSystem.Instance?.SyncWeaponControllerFromOwnedLoadout(Weapons);
    }
}
