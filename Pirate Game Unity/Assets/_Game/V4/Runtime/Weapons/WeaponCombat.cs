using System.Collections.Generic;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Weapons
{
    public enum WeaponFireOutcome { Emitted, NoTarget, CapacityBlocked }

    /// <summary>One equipped weapon: its configs, the record's upgrade values, and the firing state V4 keeps.</summary>
    public sealed class WeaponSlot
    {
        public object Key;
        public WeaponConfig Config;
        public WeaponEvolutionConfig Evolution;
        public int Level = 1;
        public float DamageBonus = 1f;
        public int QuantityBonus;
        public float CooldownMult = 1f, SpeedMult = 1f, SizeMult = 1f, DurationMult = 1f, KnockbackMult = 1f;

        public float CooldownTimer;
        /// <summary>Shots of the current burst still to fire (Swivel Guns' second shot).</summary>
        public int BurstShotsLeft;
        public float StreamRemaining, RechargeRemaining, StreamTickTimer;
        /// <summary>Time since the flame's last burning patch on the water (Hellfire Nozzle, M8 playtest).</summary>
        public float StreamPatchTimer;

        public string Id => Config != null ? Config.weaponId : null;
        public ConfigEnums.EvolutionEffect EvolutionEffect => Evolution != null ? Evolution.effect : ConfigEnums.EvolutionEffect.None;
    }

    /// <summary>
    /// Weapon values from config × record upgrades × player stats, in V3's expression order (the floats must be identical
    /// for the traces). V3 quirks are kept: the level damage factor only where a weapon's config asks for it (R-21).
    /// </summary>
    public static class WeaponStatResolver
    {
        public static float Cooldown(WeaponSlot w, IWeaponModifiers m, float itemAttackSpeed) =>
            w.Config.baseCooldown * w.CooldownMult / (m.AttackSpeed * itemAttackSpeed);

        public static float LevelMult(WeaponSlot w) =>
            w.Config.levelDamageFactor ? w.DamageBonus * w.Level * 0.1f + 1f : w.DamageBonus;

        public static float ProjectileSpeed(WeaponSlot w, IWeaponModifiers m) =>
            w.Config.projectileSpeed * w.SpeedMult * m.ProjectileSpeed;

        public static float Sized(float baseValue, WeaponSlot w, IWeaponModifiers m) => baseValue * w.SizeMult * m.Size;

        /// <summary>Volley size: base + upgrades + the Quantity stat (V3 does not clamp this one).</summary>
        public static int VolleyQuantity(WeaponSlot w, IWeaponModifiers m) =>
            w.Config.baseQuantity + w.QuantityBonus + Mathf.FloorToInt(m.Quantity);

        public static int ProjectileQuantity(WeaponSlot w, IWeaponModifiers m) =>
            w.Config == null ? 1 : Mathf.Max(1, w.Config.baseQuantity + w.QuantityBonus + Mathf.FloorToInt(m.Quantity));
    }

    public struct PowderKeg
    {
        public bool Active;
        public Vector3 Position;
        public float ArmTime, TriggerRadius, BlastRadius, BaseDamage, DamageBonus, ProcCoefficient, EliteReach, BlastDamageMultiplier;
        public WeaponSlot Source;
        public int View;
    }

    public struct DelayedStrike
    {
        public bool Active;
        public Vector3 Position;
        public float Timer, Radius, BaseDamage, DamageBonus, ProcCoefficient;
        public string WeaponId;
        /// <summary>Mortar shells: horizontal radius, impact effect (and V3's effect draws); otherwise an explosion.</summary>
        public bool AreaShell;
        public bool ImpactDraws;
    }

    public struct BoardingSkiff
    {
        public bool Active;
        public int TargetIndex;
        public Vector3 Position, SpawnStart;
        public float SpawnAnimTime, SpawnAnimDuration, StrikeCooldown, LifeTime, BaseDamage, DamageBonus, ProcCoefficient;
        public float SurfaceHeight, MoveSpeed, StrikeRange, StrikeInterval;
        public string WeaponId;
        public int View;
    }

    public struct PendingFollowUp
    {
        public bool Active;
        public float Timer;
        public Vector3 SideDirection;
        public WeaponSlot Weapon;
    }

    /// <summary>
    /// Services and the in-flight state weapon behaviors share: kegs, delayed strikes (mortar shells and Tempest markers
    /// share one list, as in V3), boarding skiffs and follow-up volleys.
    /// </summary>
    public sealed class WeaponCombat
    {
        public IEnemyQuery Enemies;
        public IEnemyDamageSink Damage;
        public IShipState Ship;
        public IWeaponModifiers Mods;
        public IWeaponCombatHooks Hooks;
        public IWeaponEffects Effects;
        public ICombatRandom Random;
        public DamageCalculator Calculator;
        public WeaponTargetingService Targeting;
        public PlayerProjectiles Projectiles;
        public System.Func<WeaponRulesConfig> Rules = () => null;
        /// <summary>Fired events (tick index etc. are the event buffer's).</summary>
        public System.Action<WeaponSlot> OnEmission;

        public readonly List<PowderKeg> Kegs = new List<PowderKeg>();
        public readonly List<DelayedStrike> Strikes = new List<DelayedStrike>();
        public readonly List<BoardingSkiff> Skiffs = new List<BoardingSkiff>();
        public readonly List<PendingFollowUp> FollowUps = new List<PendingFollowUp>();
        public readonly List<Vector3> LaneHitScratch = new List<Vector3>();
        public readonly List<int> LaneIndexScratch = new List<int>();
        public float OrbitVolleyTimer;

        public void RecordEmission(WeaponSlot w)
        {
            Hooks.RecordEmission(w.Id);
            OnEmission?.Invoke(w);
        }

        /// <summary>V3 ApplyWeaponDamage: a fresh proc resolution, the ledger, then the hit.</summary>
        public void ApplyWeaponDamage(int enemyIndex, string weaponId, DamageRoll dmg, bool isElite, bool isBoss)
        {
            Hooks.BeginDamageResolution();
            if (!Hooks.TryRegisterProcEvent()) return;
            Hooks.RecordHit(weaponId, dmg.Amount, dmg.IsCrit, isElite, isBoss);
            Damage.ApplyDamage(enemyIndex, dmg.Amount, dmg.IsCrit, weaponId: weaponId);
        }

        /// <summary>
        /// A weapon hit outside the proc path. V3 recorded only lane, aura, strike and skiff hits in the damage ledger; V4
        /// records every weapon hit so the results' damage share covers all weapons (M5 intended difference; ledger only).
        /// </summary>
        public void DamageAndRecord(int index, string ledgerId, float amount, bool isCrit, string weaponId = null)
        {
            if (ledgerId != null) Hooks.RecordHit(ledgerId, amount, isCrit, Enemies.IsElite(index), Enemies.IsBoss(index));
            Damage.ApplyDamage(index, amount, isCrit, weaponId: weaponId);
        }

        /// <summary>The aim overlay (V3 NotifyFire).</summary>
        public void NotifyAimed(WeaponSlot w, Vector3 aimDirection, int targetIndex = -1)
        {
            if (w?.Config == null || Ship == null || !Ship.Exists) return;
            var hasTarget = targetIndex >= 0 && targetIndex < Enemies.Capacity && Enemies.IsActive(targetIndex);
            var targetPos = hasTarget ? Enemies.Position(targetIndex) : Ship.Position + aimDirection * w.Config.range;
            var predicted = w.Config.behavior == ConfigEnums.WeaponBehavior.PredictedArea;
            if (predicted) targetPos = Targeting.PredictClusterGround(w.Config.range);
            Effects?.Aimed(w.Id, aimDirection, targetPos, hasTarget || predicted);
        }

        public int FindFirstEnemyAlongRay(Vector3 origin, Vector3 end, float radius)
        {
            var best = -1;
            var bestDist = float.MaxValue;
            for (var i = 0; i < Enemies.Capacity; i++)
            {
                if (!Enemies.IsActive(i)) continue;
                var position = Enemies.Position(i);
                var closest = ClosestPointOnSegment(origin, end, position);
                if (HorizontalDistance(closest, position) > radius + Enemies.Radius(i) * 0.35f) continue;
                var dist = (closest - origin).sqrMagnitude;
                if (dist >= bestDist) continue;
                bestDist = dist;
                best = i;
            }
            return best;
        }

        public static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            var ab = b - a;
            ab.y = 0f;
            if (ab.sqrMagnitude < 0.0001f) return a;
            var t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / ab.sqrMagnitude);
            return a + ab * t;
        }

        public static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        public static bool WithinHorizontalRadius(Vector3 center, Vector3 target, float radius)
        {
            var dx = target.x - center.x;
            var dz = target.z - center.z;
            return dx * dx + dz * dz <= radius * radius;
        }

        public ProjectileTraits TraitsOf(WeaponConfig config)
        {
            var p = config.projectile;
            return new ProjectileTraits
            {
                LedgerId = config.weaponId,
                Homing = p.homing,
                TarOnHit = p.tarOnHit,
                TarSlow = p.tarSlow,
                TarSlowBoss = p.tarSlowBoss,
                ControlSecondsPerHit = p.controlSecondsPerHit,
                SplashOnDespawn = p.splashOnDespawn,
            };
        }

        public void ClearInFlight()
        {
            foreach (var k in Kegs) if (k.View >= 0) Effects?.MineRemoved(k.View);
            foreach (var s in Skiffs) if (s.View >= 0) Effects?.SkiffRemoved(s.View);
            Kegs.Clear();
            Strikes.Clear();
            Skiffs.Clear();
            FollowUps.Clear();
        }
    }
}
