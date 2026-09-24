using HighSeas.Core;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.Combat
{
    public static class SaltwindHazardTelegraph
    {
        sealed class PendingHazard
        {
            public bool active;
            public Vector3 center;
            public float radius;
            public float timer;
            public float damage;
        }

        static readonly PendingHazard[] Slots = new PendingHazard[24];
        static PlayerShipController _player;

        public static void Tick(float dt)
        {
            _player ??= Object.FindFirstObjectByType<PlayerShipController>();
            for (var i = 0; i < Slots.Length; i++)
            {
                var slot = Slots[i];
                if (slot == null || !slot.active) continue;
                slot.timer -= dt;
                if (slot.timer > 0f) continue;
                if (_player?.Damage != null
                    && Vector3.Distance(slot.center, _player.Position) <= slot.radius)
                {
                    _player.Damage.TakeDamage(slot.damage, slot.center, false);
                }

                slot.active = false;
            }
        }

        public static void SpawnCircle(Vector3 center, float radius, float fuseSeconds, float damage)
        {
            center.y = 0f;
            fuseSeconds = Mathf.Max(fuseSeconds, SaltwindCombatEncounterBudget.MinExplosiveWarning);
            for (var i = 0; i < Slots.Length; i++)
            {
                Slots[i] ??= new PendingHazard();
                if (Slots[i].active) continue;
                Slots[i].active = true;
                Slots[i].center = center;
                Slots[i].radius = radius;
                Slots[i].timer = fuseSeconds;
                Slots[i].damage = damage;
                return;
            }
        }

        public static void ClearAll()
        {
            for (var i = 0; i < Slots.Length; i++)
            {
                if (Slots[i] != null)
                    Slots[i].active = false;
            }
        }
    }
}
