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
    /// <summary>
    /// The game database load (the shared definitions the runtime database is built from). M16: V3's runtime catalogs
    /// are gone — the runtime database creates the specialist aliases from config and every value comes from config.
    /// </summary>
    public sealed class RunDatabaseLoader
    {
        public GameDatabase Database { get; private set; }

        public void Load(GameDatabase serialized)
        {
            Database = serialized;
            if (Database == null || !GameDatabaseLoader.IsValid(Database))
                Database = GameDatabaseLoader.Load();
        }

    }
}
