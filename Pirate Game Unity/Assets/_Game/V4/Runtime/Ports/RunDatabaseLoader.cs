using RoguePirates.V4.Foundation.Balance;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Enemies;
using RoguePirates.V4.Foundation.Pickups;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.POI;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Foundation.VFX;
using RoguePirates.V4.Foundation.Audio;
using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.V4.Ports
{
    // Each adapter wraps one legacy area that V4 still depends on. The attribute names the phase that deletes it;
    // DeletedInPhaseTests fails if an adapter outlives that phase. Method bodies are ported from V3RunBootstrap
    // with the call order unchanged, so M0 runs are identical to V3 runs.

    /// <summary>Game database load and the runtime catalogs. M0 applies them exactly as V3 does.</summary>
    public sealed class RunDatabaseLoader
    {
        public GameDatabase Database { get; private set; }

        public void Load(GameDatabase serialized)
        {
            Database = serialized;
            if (Database == null || !GameDatabaseLoader.IsValid(Database))
                Database = GameDatabaseLoader.Load();
        }

        /// <summary>The runtime weapon and specialist catalogs for the active combat experiment.</summary>
        public void ApplyCatalogs()
        {
            var experiment = CombatExperimentContext.Active;
            CombatWeaponCatalog.ApplyToDatabase(Database, experiment);
            CombatSpecialistEnemyCatalog.ApplyToDatabase(Database, experiment);
        }
    }
}
