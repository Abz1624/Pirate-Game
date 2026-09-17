using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.Weapons;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    public static class V3MultiShipWeaponLoadouts
    {
        public static WeaponController FindHumanWeaponController(PlayerShipController humanPlayer)
        {
            var all = Object.FindObjectsByType<WeaponController>(FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (all[i].GetComponent<PlayerShipController>() != null)
                    continue;
                return all[i];
            }

            return humanPlayer != null ? humanPlayer.GetComponent<WeaponController>() : null;
        }

        public static WeaponController GetWeaponControllerForShip(PlayerShipController ship)
        {
            if (ship == null) return null;
            var onShip = ship.GetComponent<WeaponController>();
            if (onShip != null) return onShip;

            var combatant = ship.GetComponent<V3ShipCombatant>();
            if (combatant != null && combatant.IsLocalHuman)
                return FindHumanWeaponController(ship);

            return null;
        }

        public static void ApplyLoadout(PlayerShipController ship, GameDatabase database, string[] loadout)
        {
            if (ship == null || database == null || loadout == null || loadout.Length == 0)
                return;

            var weapons = GetWeaponControllerForShip(ship);
            if (weapons == null) return;

            weapons.ApplyCombatExperiment(CombatExperimentContext.Active);
            weapons.EnsureWeaponSlotCapacity(loadout.Length);
            weapons.Initialize(database, loadout[0]);
            weapons.SetWeaponLoadout(loadout);
        }

        /// <summary>Co-op: independent random 6-weapon loadouts for every player ship in the scene.</summary>
        public static int RollAndApplyRandomLoadoutsToAllShips(GameDatabase database)
        {
            if (database == null || !V3RunSession.IsCoOp)
                return 0;

            var rng = RunManager.Instance?.Rng;
            var ships = Object.FindObjectsByType<PlayerShipController>(FindObjectsSortMode.None);
            var count = 0;
            string[] humanLoadout = null;

            for (var i = 0; i < ships.Length; i++)
            {
                var ship = ships[i];
                if (ship == null) continue;

                var loadout = V3PvPWeaponLoadout.RollRandomWeapons(database, rng);
                if (loadout.Length == 0) continue;

                ApplyLoadout(ship, database, loadout);
                count++;

                var combatant = ship.GetComponent<V3ShipCombatant>();
                if (combatant != null && combatant.IsLocalHuman)
                    humanLoadout = loadout;
            }

            if (humanLoadout != null)
                LevelSystem.Instance?.SetPlaytestWeaponLoadout(humanLoadout);

            CombatExperimentContext.ShowAimVisuals = true;
            return count;
        }
    }
}
