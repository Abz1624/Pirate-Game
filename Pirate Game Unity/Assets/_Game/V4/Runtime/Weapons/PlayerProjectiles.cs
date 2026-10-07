using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.V4.Weapons
{
    /// <summary>What a player projectile does besides damage (from the firing weapon's config).</summary>
    public struct ProjectileTraits
    {
        public bool Homing;
        public bool TarOnHit;
        public float TarSlow, TarSlowBoss, ControlSecondsPerHit;
        /// <summary>V5 M8z (7 Oct, D21): seconds the tar slows its enemy (0 = the tar does not slow).</summary>
        public float TarSlowSeconds;
        public bool SplashOnDespawn;
        /// <summary>Damage-ledger credit (bursts and follow-ups credit their weapon; V3 recorded no projectile hits).</summary>
        public string LedgerId;
        /// <summary>V5 M4: enemies passed through after the first hit (0 = stop at the first).</summary>
        public int Pierce;
        /// <summary>V5 M4: a slow each hit applies (share of speed, seconds).</summary>
        public float SlowFraction, SlowSeconds;

        public static ProjectileTraits Plain => new ProjectileTraits { SplashOnDespawn = true };
        public static ProjectileTraits Credited(string ledgerId) => new ProjectileTraits { SplashOnDespawn = true, LedgerId = ledgerId };
    }

    /// <summary>
    /// Player projectiles, port of V3 ProjectileWorld: segment-against-circle hits (enemy position advanced half a
    /// step), subtle homing within 10° of the launch direction, pierce, Tarred on harpoon hits, splash on despawn.
    /// Storage is a plain array allocated lowest-free-slot first and iterated in index order, like V3: the order
    /// decides which enemy dies first, and kill order feeds the loot random stream the traces compare.
    /// </summary>
    public sealed class PlayerProjectiles
    {
        /// <summary>V5 M5: a hit's damage after target-dependent item bonuses (Shark Tooth Necklace); null = unchanged.</summary>
        public System.Func<int, float, float> HitAdjust;
        const float MinGameplayHitRadius = 0.32f;
        const float HitSlop = 0.16f;
        const float MaxVerticalHitSeparation = 4f;
        const float HomingTurnRateDegPerSec = 20f;
        const float HomingMinAngleDeg = 8f;
        const float MaxHomingDeviationDeg = 10f;

        struct Projectile
        {
            public bool Active;
            public Vector3 Position, PreviousPosition, Velocity, SpawnDirection;
            public float Damage, Life, Radius;
            public int PierceLeft;
            public bool IsCrit;
            public string WeaponId;
            public ProjectileTraits Traits;
            public int HomingEnemy;
            public int View;
            /// <summary>V5 M4: the enemies this shot has hit (unit ids), so a piercing shot hits each once.</summary>
            public int Hit0, Hit1, Hit2, Hit3, Hit4, Hit5, Hit6, Hit7, HitCount;
        }

        /// <summary>The most enemies one shot can hit (pierce is clamped to this minus one).</summary>
        public const int MaxHitsPerShot = 8;

        static bool HasHit(in Projectile p, int enemy)
        {
            if (p.HitCount > 0 && p.Hit0 == enemy) return true;
            if (p.HitCount > 1 && p.Hit1 == enemy) return true;
            if (p.HitCount > 2 && p.Hit2 == enemy) return true;
            if (p.HitCount > 3 && p.Hit3 == enemy) return true;
            if (p.HitCount > 4 && p.Hit4 == enemy) return true;
            if (p.HitCount > 5 && p.Hit5 == enemy) return true;
            if (p.HitCount > 6 && p.Hit6 == enemy) return true;
            return p.HitCount > 7 && p.Hit7 == enemy;
        }

        static void RecordHit(ref Projectile p, int enemy)
        {
            switch (p.HitCount)
            {
                case 0: p.Hit0 = enemy; break;
                case 1: p.Hit1 = enemy; break;
                case 2: p.Hit2 = enemy; break;
                case 3: p.Hit3 = enemy; break;
                case 4: p.Hit4 = enemy; break;
                case 5: p.Hit5 = enemy; break;
                case 6: p.Hit6 = enemy; break;
                case 7: p.Hit7 = enemy; break;
                default: return;
            }
            p.HitCount++;
        }

        readonly Projectile[] _pool;
        readonly IEnemyQuery _enemies;
        readonly IEnemyDamageSink _damage;
        readonly IWeaponCombatHooks _hooks;
        readonly IWeaponEffects _effects;

        /// <summary>Views are looked up lazily: the presentation layer registers them after the run starts.</summary>
        public System.Func<IProjectileViews> Views = () => null;

        public int ActiveCount { get; private set; }
        public int Capacity => _pool.Length;

        public PlayerProjectiles(int capacity, IEnemyQuery enemies, IEnemyDamageSink damage, IWeaponCombatHooks hooks, IWeaponEffects effects)
        {
            _pool = new Projectile[Mathf.Max(1, capacity)];
            _enemies = enemies;
            _damage = damage;
            _hooks = hooks;
            _effects = effects;
        }

        public bool TrySpawn(Vector3 pos, Vector3 dir, float speed, float damage, float life, float radius, bool isCrit, Color color,
            string weaponId, ProjectileTraits traits, int homingEnemyIndex = -1)
        {
            if (ActiveCount >= _pool.Length) return false;
            var slot = -1;
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) { slot = i; break; }
            }
            if (slot < 0) return false;

            var views = Views();
            var view = views != null ? views.Acquire(weaponId, pos, color, radius) : -1;
            var normalizedDir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector3.forward;
            _pool[slot] = new Projectile
            {
                Active = true,
                Position = pos,
                PreviousPosition = pos,
                Velocity = normalizedDir * speed,
                Damage = damage,
                Life = life,
                Radius = radius,
                PierceLeft = 1 + Mathf.Clamp(traits.Pierce, 0, MaxHitsPerShot - 1),
                IsCrit = isCrit,
                WeaponId = weaponId,
                Traits = traits,
                View = view,
                HomingEnemy = traits.Homing && homingEnemyIndex >= 0 && !string.IsNullOrEmpty(weaponId) ? homingEnemyIndex : -1,
                SpawnDirection = normalizedDir
            };
            ActiveCount++;
            return true;
        }

        public void Tick(float dt)
        {
            var views = Views();
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) continue;
                ref var p = ref _pool[i];
                ApplySubtleHoming(ref p, dt);
                var segmentStart = p.Position;
                var next = p.Position + p.Velocity * dt;
                // Islands stop shots like enemy shots: the flight segment (no tunnelling at speed) or the shot's body.
                var hitIsland = TryHitIsland(segmentStart, next, p.Radius, out var stopAt);
                p.Position = hitIsland ? stopAt : next;
                p.PreviousPosition = segmentStart;
                p.Life -= dt;
                if (p.View >= 0) views?.Move(p.View, p.Position, p.Velocity);

                if (_enemies != null && _enemies.Exists && p.PierceLeft > 0)
                {
                    for (var ei = 0; ei < _enemies.Capacity; ei++)
                    {
                        if (!_enemies.IsActive(ei) || (p.HitCount > 0 && HasHit(p, _enemies.UnitId(ei)))) continue;
                        var agentPos = _enemies.Position(ei) + _enemies.Velocity(ei) * (dt * 0.5f);
                        if (!TryHitAgentAlongSegment(segmentStart, p.Position, p.Radius, agentPos, _enemies.Radius(ei)))
                            continue;

                        var amount = HitAdjust != null ? HitAdjust(ei, p.Damage) : p.Damage; // V5 M5: Shark Tooth Necklace
                        if (p.Traits.LedgerId != null) _hooks.RecordHit(p.Traits.LedgerId, amount, p.IsCrit, _enemies.IsElite(ei), _enemies.IsBoss(ei));
                        _damage.ApplyDamage(ei, amount, p.IsCrit, weaponId: p.WeaponId);
                        if (p.Traits.TarOnHit)
                        {
                            var slow = _enemies.IsBoss(ei) ? p.Traits.TarSlowBoss : p.Traits.TarSlow;
                            _damage.ApplyStatus(ei, WeaponStatus.Tarred, slow);
                            _hooks.RecordControl(p.WeaponId, p.Traits.ControlSecondsPerHit);
                            // V5 M8z (7 Oct, D21, Q23): the tar is a real slow. The enemy world gives bosses and minibosses
                            // their share of it (half, at most bossMaxSlow), which is the tarSlowBoss the status records.
                            if (p.Traits.TarSlowSeconds > 0f) _damage.ApplySlow(ei, p.Traits.TarSlow, p.Traits.TarSlowSeconds);
                        }
                        if (p.Traits.SlowFraction > 0f) _damage.ApplySlow(ei, p.Traits.SlowFraction, p.Traits.SlowSeconds);
                        RecordHit(ref p, _enemies.UnitId(ei));
                        p.HomingEnemy = -1; // a piercing shot flies on instead of turning back to the enemy it hit
                        p.PierceLeft--;
                        if (p.PierceLeft <= 0)
                            break;
                    }
                }

                if (hitIsland || p.Life <= 0f || p.PierceLeft <= 0)
                    Despawn(i, views);
            }
        }

        /// <summary>
        /// True when the shot's flight from <paramref name="from"/> to <paramref name="to"/> meets an island collider;
        /// <paramref name="stopAt"/> is where it stops (the contact point, or the end when only its body overlaps).
        /// </summary>
        static bool TryHitIsland(Vector3 from, Vector3 to, float radius, out Vector3 stopAt)
        {
            stopAt = to;
            if (!IslandObstacles.IsAvailable) return false;
            if (Physics.Linecast(from, to, out var hit, IslandObstacles.Mask, QueryTriggerInteraction.Ignore))
            {
                stopAt = hit.point;
                return true;
            }
            return IslandObstacles.IsPositionBlocked(to, radius);
        }

        static bool TryHitAgentAlongSegment(Vector3 segmentStart, Vector3 segmentEnd, float projectileRadius, Vector3 agentPosition, float agentRadius)
        {
            var hitRadius = Mathf.Max(projectileRadius, MinGameplayHitRadius) + agentRadius + HitSlop;
            var closest = ClosestPointOnSegmentXZ(segmentStart, segmentEnd, agentPosition);
            var flat = agentPosition - closest;
            flat.y = 0f;
            if (flat.sqrMagnitude > hitRadius * hitRadius)
                return false;
            return Mathf.Abs(agentPosition.y - closest.y) <= MaxVerticalHitSeparation;
        }

        static Vector3 ClosestPointOnSegmentXZ(Vector3 a, Vector3 b, Vector3 point)
        {
            var ab = b - a;
            ab.y = 0f;
            if (ab.sqrMagnitude < 0.0001f)
                return a;
            var ap = point - a;
            ap.y = 0f;
            var t = Mathf.Clamp01(Vector3.Dot(ap, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }

        void ApplySubtleHoming(ref Projectile p, float dt)
        {
            if (p.HomingEnemy < 0 || _enemies == null || !_enemies.Exists) return;
            if (p.HomingEnemy >= _enemies.Capacity || !_enemies.IsActive(p.HomingEnemy)) return;

            var target = _enemies.Position(p.HomingEnemy);
            target.y = p.Position.y;
            var toTarget = target - p.Position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.04f) return;

            var speed = p.Velocity.magnitude;
            if (speed < 0.01f) return;

            var current = p.Velocity;
            current.y = 0f;
            var desired = toTarget.normalized * speed;
            var alignment = Vector3.Dot(current.normalized, desired.normalized);
            if (alignment > 0.992f)
                return;

            var angleDeg = Mathf.Acos(Mathf.Clamp(alignment, -1f, 1f)) * Mathf.Rad2Deg;
            if (angleDeg < HomingMinAngleDeg)
                return;

            var turnScale = Mathf.Clamp01(angleDeg / 40f);
            var maxRadians = HomingTurnRateDegPerSec * Mathf.Deg2Rad * dt * turnScale;
            var steered = Vector3.RotateTowards(current, desired, maxRadians, 0f);
            steered.y = 0f;
            if (steered.sqrMagnitude < 0.01f) return;
            steered.Normalize();

            var spawnDir = p.SpawnDirection;
            spawnDir.y = 0f;
            if (spawnDir.sqrMagnitude < 0.01f)
                spawnDir = current.normalized;
            else
                spawnDir.Normalize();

            var maxDevRad = MaxHomingDeviationDeg * Mathf.Deg2Rad;
            var clampedDir = Vector3.RotateTowards(spawnDir, steered, maxDevRad, 0f);
            if (Vector3.Angle(spawnDir, current.normalized) >= MaxHomingDeviationDeg - 0.5f)
                return;

            p.Velocity = clampedDir * speed;
        }

        void Despawn(int i, IProjectileViews views, bool effects = true)
        {
            if (!_pool[i].Active) return;
            var position = _pool[i].Position;
            var splash = _pool[i].Traits.SplashOnDespawn;
            if (_pool[i].View >= 0) views?.Release(_pool[i].View);
            _pool[i].Active = false;
            _pool[i].View = -1;
            ActiveCount--;
            if (splash && effects) _effects?.WaterSplash(position);
        }

        /// <summary>Removes every projectile; no splashes when the scene is closing (effects = false).</summary>
        public void ClearAll(bool effects = true)
        {
            var views = Views();
            for (var i = 0; i < _pool.Length; i++)
                if (_pool[i].Active) Despawn(i, views, effects);
        }
    }
}
