using HighSeas.Balance;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Debugging;
using HighSeas.Encounters;
using HighSeas.Enemies;
using HighSeas.Pickups;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.V3;
using HighSeas.V3.MultiShip;
using HighSeas.VFX;
using HighSeas.Audio;
using HighSeas.Weapons;
using HighSeas.World;
using UnityEngine;

namespace RoguePirates.V4.Legacy
{
    // Each adapter wraps one legacy area that V4 still depends on. The attribute names the phase that deletes it;
    // DeletedInPhaseTests fails if an adapter outlives that phase. Method bodies are ported from V3RunBootstrap
    // with the call order unchanged, so M0 runs are identical to V3 runs.

    /// <summary>Game database load and the runtime catalogs. M0 applies them exactly as V3 does.</summary>
    [DeletedInPhase(15, "The V4 enemy world and card texts still read the shared GameDatabase definition types (moved in M15)")]
    public sealed class LegacyDatabaseAdapter
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
