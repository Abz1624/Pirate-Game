using HighSeas.Core;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.Combat
{
    public static class SaltwindHazardTelegraph
    {
        enum HazardKind
        {
            Explosion,
            NetSlow,
            Mine
        }

        sealed class PendingHazard
        {
            public bool active;
            public HazardKind kind;
            public Vector3 center;
            public float radius;
            public float timer;
            public float damage;
            public float slowStrength;
            public float slowDuration;
            public bool armed;
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
                if (slot.kind == HazardKind.Mine)
                {
                    if (!slot.armed)
                    {
                        slot.timer -= dt;
                        if (slot.timer <= 0f)
                            slot.armed = true;
                    }
                    else
                    {
                        slot.slowDuration -= dt;
                        var playerPos = _player != null ? _player.Position : Vector3.zero;
                        playerPos.y = 0f;
                        var hit = _player != null && Vector3.Distance(slot.center, playerPos) <= slot.radius;
                        if (hit || slot.slowDuration <= 0f)
                        {
                            if (hit && _player?.Damage != null)
                                _player.Damage.TakeDamage(slot.damage, slot.center, false);
                            SaltwindCombatEffects.PlayExplosion(slot.center, slot.radius);
                            slot.active = false;
                        }
                    }

                    continue;
                }

                slot.timer -= dt;
                if (slot.kind == HazardKind.NetSlow && slot.timer <= 0f)
                {
                    slot.active = false;
                    continue;
                }

                if (slot.timer > 0f) continue;

                if (_player != null)
                {
                    var playerPos = _player.Position;
                    playerPos.y = 0f;
                    if (Vector3.Distance(slot.center, playerPos) <= slot.radius)
                    {
                        if (slot.kind == HazardKind.NetSlow)
                            _player.ApplySlow(1f - slot.slowStrength, slot.slowDuration);
                        else if (_player.Damage != null)
                        {
                            _player.Damage.TakeDamage(slot.damage, slot.center, false);
                            if (slot.kind == HazardKind.Explosion || slot.kind == HazardKind.Mine)
                                SaltwindCombatEffects.PlayExplosion(slot.center, slot.radius);
                        }
                    }
                }

                slot.active = false;
            }
        }

        public static void SpawnNetSegment(Vector3 center, float length, float activeSeconds, float slowStrength, float slowDuration)
        {
            center.y = 0f;
            for (var i = 0; i < Slots.Length; i++)
            {
                Slots[i] ??= new PendingHazard();
                if (Slots[i].active) continue;
                Slots[i].active = true;
                Slots[i].kind = HazardKind.NetSlow;
                Slots[i].center = center;
                Slots[i].radius = length * 0.5f;
                Slots[i].timer = activeSeconds;
                Slots[i].slowStrength = slowStrength;
                Slots[i].slowDuration = slowDuration;
                return;
            }
        }

        public static void SpawnMine(Vector3 center, float armSeconds, float lifeSeconds, float damage)
        {
            center.y = 0f;
            for (var i = 0; i < Slots.Length; i++)
            {
                Slots[i] ??= new PendingHazard();
                if (Slots[i].active) continue;
                Slots[i].active = true;
                Slots[i].kind = HazardKind.Mine;
                Slots[i].center = center;
                Slots[i].radius = 2.2f;
                Slots[i].timer = armSeconds;
                Slots[i].damage = damage;
                Slots[i].armed = false;
                Slots[i].slowDuration = lifeSeconds;
                return;
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
                Slots[i].kind = HazardKind.Explosion;
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
