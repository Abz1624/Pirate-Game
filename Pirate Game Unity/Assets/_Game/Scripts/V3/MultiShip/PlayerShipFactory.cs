using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
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
            IReadOnlyList<string> weaponLoadout = null)
        {
            if (template == null) return null;

            var instance = Object.Instantiate(template.gameObject, position, rotation);
            instance.name = name;
            if (!localHuman)
                instance.tag = "Untagged";

            var ship = instance.GetComponent<PlayerShipController>();
            var damage = instance.GetComponent<DamageReceiver>();
            var hull = RunManager.Instance != null ? RunManager.Instance.Stats.MaxHull : damage.MaxHP;
            ship.InitializeFromRun(hull);

            var combatant = instance.GetComponent<V3ShipCombatant>();
            if (combatant == null)
                combatant = instance.AddComponent<V3ShipCombatant>();
            combatant.Configure(team, localHuman);

            if (!localHuman)
            {
                var brain = instance.GetComponent<BotShipBrain>();
                if (brain == null)
                    brain = instance.AddComponent<BotShipBrain>();
                brain.Initialize(ship, team, followTarget);

                var weapons = instance.GetComponent<WeaponController>();
                if (weapons == null)
                    weapons = instance.AddComponent<WeaponController>();
                weapons.BindOwner(ship);
                weapons.ApplyCombatExperiment(CombatExperimentContext.Active);
                if (weaponLoadout != null && weaponLoadout.Count > 0)
                {
                    weapons.Initialize(database, weaponLoadout[0]);
                    weapons.SetWeaponLoadout(weaponLoadout);
                }
                else
                    weapons.Initialize(database, startingWeaponId);

                if (instance.GetComponent<PlayerHealthBarBinder>() == null)
                    instance.AddComponent<PlayerHealthBarBinder>();
            }

            return ship;
        }
    }
}
