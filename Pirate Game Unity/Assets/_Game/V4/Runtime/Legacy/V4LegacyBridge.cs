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
    /// Weapon half of the bridge: with <see cref="V4LegacySpawnBridge"/> the only V4 code that drives the legacy
    /// <see cref="WeaponController"/>. Reduced to a switched-off loadout record in M5, deleted in M9.
    /// </summary>
    [DeletedInPhase(9, "Weapon record removed with LevelSystem in M9")]
    public sealed class V4LegacyBridge
    {
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

        /// <summary>
        /// M5: the controller becomes the loadout record. Unbound from the ship, its captain fire paths return at once
        /// (WeaponController L460, L490); switched off, it never updates. The level system still equips and upgrades
        /// weapons on it (found by type, not by owner) and V4's Weapon Manager reads it.
        /// </summary>
        public void MakeRecordOnly()
        {
            if (Weapons == null) return;
            Weapons.BindOwner(null);
            Weapons.enabled = false;
        }

        /// <summary>LevelSystem mirrors its owned loadout onto the weapon controller (V3RunBootstrap L256).</summary>
        public void SyncLevelSystemLoadout() => HighSeas.UI.LevelSystem.Instance?.SyncWeaponControllerFromOwnedLoadout(Weapons);
    }
}
