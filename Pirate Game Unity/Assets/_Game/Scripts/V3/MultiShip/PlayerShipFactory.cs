using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Platform;
using HighSeas.Player;
using HighSeas.VFX;
using HighSeas.Weapons;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    public static class PlayerShipFactory
    {
        public static PlayerShipController SpawnFromTemplate(
            PlayerShipController template,
            string name,
            Vector3 position,
            Quaternion rotation,
            V3ShipTeam team,
            bool localHuman,
            GameDatabase database,
            string startingWeaponId,
            PlayerShipController followTarget,
            IReadOnlyList<string> weaponLoadout = null,
            ShipKeyboardBinding? keyboardBinding = null,
            bool useBotBrain = true)
        {
            if (template == null) return null;

            var safePosition = V3MultiShipSpawnUtil.ResolveSafeSpawn(position);
            var instance = Object.Instantiate(template.gameObject, safePosition, rotation);
            instance.name = name;
            if (!localHuman)
                instance.tag = "Untagged";

            var ship = instance.GetComponent<PlayerShipController>();
            var damage = instance.GetComponent<DamageReceiver>();
            var hull = RunManager.Instance != null ? RunManager.Instance.Stats.MaxHull : damage.MaxHP;
            ship.InitializeFromRun(hull);
            ship.SetWorldPosition(safePosition);

            var combatant = instance.GetComponent<V3ShipCombatant>();
            if (combatant == null)
                combatant = instance.AddComponent<V3ShipCombatant>();
            combatant.Configure(team, localHuman);

            if (localHuman && keyboardBinding.HasValue)
                V3LocalShipInput.Bind(ship, keyboardBinding.Value);

            if (localHuman)
            {
                AttachShipWeapons(ship, database, startingWeaponId, weaponLoadout);
                if (instance.GetComponent<PlayerHealthBarBinder>() == null)
                    instance.AddComponent<PlayerHealthBarBinder>();
            }
            else if (useBotBrain)
            {
                var brain = instance.GetComponent<BotShipBrain>();
                if (brain == null)
                    brain = instance.AddComponent<BotShipBrain>();
                brain.Initialize(ship, team, followTarget);
                if (team == V3ShipTeam.AllyBot && name == "AllyShip_A")
                    brain.SetFormationOffset(new Vector3(8f, 0f, -5f));
                else if (team == V3ShipTeam.AllyBot && name == "AllyShip_B")
                    brain.SetFormationOffset(new Vector3(-8f, 0f, -5f));

                AttachShipWeapons(ship, database, startingWeaponId, weaponLoadout);

                if (instance.GetComponent<PlayerHealthBarBinder>() == null)
                    instance.AddComponent<PlayerHealthBarBinder>();
            }

            return ship;
        }

        static void AttachShipWeapons(
            PlayerShipController ship,
            GameDatabase database,
            string startingWeaponId,
            IReadOnlyList<string> weaponLoadout)
        {
            if (ship == null || database == null)
                return;

            var weapons = ship.GetComponent<WeaponController>();
            if (weapons == null)
                weapons = ship.gameObject.AddComponent<WeaponController>();
            weapons.BindOwner(ship);
            weapons.ApplyCombatExperiment(CombatExperimentContext.Active);
            if (weaponLoadout != null && weaponLoadout.Count > 0)
            {
                weapons.EnsureWeaponSlotCapacity(weaponLoadout.Count);
                weapons.Initialize(database, weaponLoadout[0]);
                weapons.SetWeaponLoadout(weaponLoadout);
            }
            else
                weapons.Initialize(database, startingWeaponId);
        }
    }
}
