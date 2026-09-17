using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.V3.MultiShip
{
    public static class V3ShipCombatRegistry
    {
        static readonly List<V3ShipCombatant> Combatants = new(8);

        public static void Register(V3ShipCombatant combatant)
        {
            if (combatant == null || Combatants.Contains(combatant)) return;
            Combatants.Add(combatant);
        }

        public static void Unregister(V3ShipCombatant combatant)
        {
            if (combatant == null) return;
            Combatants.Remove(combatant);
        }

        public static bool TryResolveAim(
            PlayerShipController attacker,
            WeaponDefinition def,
            out V3ShipCombatant target,
            out Vector3 aimDirection)
        {
            target = null;
            aimDirection = attacker != null ? attacker.MovementForward : Vector3.forward;
            if (!V3RunSession.AllowShipVsShipCombat || attacker == null || def == null) return false;

            var attackerCombatant = attacker.GetComponent<V3ShipCombatant>();
            var origin = attacker.Position;
            var forward = attacker.MovementForward;
            var range = def.range;
            var bestDist = float.MaxValue;

            for (var i = 0; i < Combatants.Count; i++)
            {
                var c = Combatants[i];
                if (c == null || c.Ship == null || c.Damage == null || !c.Damage.IsAlive) continue;
                if (attackerCombatant != null && !attackerCombatant.IsHostileTo(c)) continue;

                var delta = c.Ship.Position - origin;
                delta.y = 0f;
                var dist = delta.magnitude;
                if (dist > range || dist >= bestDist) continue;

                if (def.targeting == TargetingMode.ForwardArc || def.targeting == TargetingMode.ForwardCone
                    || def.targeting == TargetingMode.TetherTarget)
                {
                    var arc = WeaponTargeting.ResolveArcDegrees(def);
                    if (!WeaponTargeting.IsWithinForwardArc(origin, forward, c.Ship.Position, arc))
                        continue;
                }

                bestDist = dist;
                target = c;
            }

            if (target == null) return false;
            aimDirection = (target.Ship.Position - origin).normalized;
            return true;
        }

        public static void ApplyRadiusDamage(
            PlayerShipController attacker,
            Vector3 center,
            float radius,
            float amount,
            bool isCrit,
            string weaponId)
        {
            if (!V3RunSession.AllowShipVsShipCombat || radius <= 0f || amount <= 0f) return;
            var attackerCombatant = attacker != null ? attacker.GetComponent<V3ShipCombatant>() : null;

            for (var i = 0; i < Combatants.Count; i++)
            {
                var c = Combatants[i];
                if (c?.Ship == null || c.Damage == null || !c.Damage.IsAlive) continue;
                if (attackerCombatant != null && !attackerCombatant.IsHostileTo(c)) continue;
                if (!WithinHorizontalRadius(center, c.Ship.Position, radius + 1.15f)) continue;
                c.Damage.TakeDamage(amount, c.Ship.Position, isCrit);
                if (!string.IsNullOrEmpty(weaponId))
                    WeaponDamageLedger.RecordHit(weaponId, amount, isCrit, false, false);
            }
        }

        public static bool TrySegmentHit(
            PlayerShipController attacker,
            Vector3 origin,
            Vector3 end,
            float hitRadius,
            out V3ShipCombatant hit)
        {
            hit = null;
            if (!V3RunSession.AllowShipVsShipCombat || attacker == null) return false;
            var attackerCombatant = attacker.GetComponent<V3ShipCombatant>();
            var seg = end - origin;
            seg.y = 0f;
            var segLen = seg.magnitude;
            if (segLen < 0.01f) return false;
            var segDir = seg / segLen;

            for (var i = 0; i < Combatants.Count; i++)
            {
                var c = Combatants[i];
                if (c?.Ship == null || c.Damage == null || !c.Damage.IsAlive) continue;
                if (attackerCombatant != null && !attackerCombatant.IsHostileTo(c)) continue;

                var to = c.Ship.Position - origin;
                to.y = 0f;
                var t = Mathf.Clamp(Vector3.Dot(to, segDir), 0f, segLen);
                var closest = origin + segDir * t;
                if (Vector3.Distance(closest, c.Ship.Position) > hitRadius + 1.15f) continue;
                hit = c;
                return true;
            }

            return false;
        }

        public static bool TryProjectileHit(PlayerShipController firingShip, Vector3 position, float radius, float damage, bool isCrit)
        {
            if (!V3RunSession.AllowShipVsShipCombat || firingShip == null) return false;
            var attackerCombatant = firingShip.GetComponent<V3ShipCombatant>();
            var hit = false;

            for (var i = 0; i < Combatants.Count; i++)
            {
                var c = Combatants[i];
                if (c?.Ship == null || c.Damage == null || !c.Damage.IsAlive) continue;
                if (attackerCombatant != null && !attackerCombatant.IsHostileTo(c)) continue;
                var d = Vector3.Distance(position, c.Ship.Position);
                if (d > radius + 1.15f) continue;
                c.Damage.TakeDamage(damage, position, isCrit);
                hit = true;
            }

            return hit;
        }

        static bool WithinHorizontalRadius(Vector3 center, Vector3 target, float radius)
        {
            var dx = target.x - center.x;
            var dz = target.z - center.z;
            return dx * dx + dz * dz <= radius * radius;
        }
    }
}
