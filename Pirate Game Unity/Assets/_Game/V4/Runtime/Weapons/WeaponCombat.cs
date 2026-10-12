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
        /// <summary>V5 M8z (7 Oct, D21): the Slow cards' total from the record (added to the weapon's slow share).</summary>
        public float SlowBonus;
        /// <summary>V5 M4: an Empowered evolution's damage factor, extra pierce and extra slow (1, 0, 0 otherwise); V5 M8z
        /// (D21): the reached milestones' extra slow is in <see cref="ExtraSlow"/> too.</summary>
        public float EvolutionDamage = 1f, ExtraSlow;
        public int ExtraPierce;
        /// <summary>V5 M4: an Empowered evolution's extra shots on a burst weapon (Swivel Battery), which fires bursts, not volleys.</summary>
        public int ExtraBurstShots;
        /// <summary>V5 M8z (7 Oct, Q20): the reached milestones' orbit spin factor, extra slow seconds, and echoes (every
        /// firing repeats <see cref="Echoes"/> more times, <see cref="EchoDelaySeconds"/> apart). 1, 0, 0 without milestones.</summary>
        public float SpinMult = 1f, ExtraSlowSeconds, EchoDelaySeconds;
        public int Echoes;

        public float CooldownTimer;
        /// <summary>Shots of the current burst still to fire (Swivel Guns' second shot).</summary>
        public int BurstShotsLeft;
        public float StreamRemaining, RechargeRemaining, StreamTickTimer;
        /// <summary>Time since the flame's last burning patch on the water (Hellfire Nozzle, M8 playtest).</summary>
        public float StreamPatchTimer;
        /// <summary>V5 M4: the flame cone's reach from the ship (m) and full width (degrees) at its last tick, for its visual.</summary>
        public float StreamRange, StreamArcDegrees;

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
            (w.Config.levelDamageFactor ? w.DamageBonus * w.Level * 0.1f + 1f : w.DamageBonus) * w.EvolutionDamage;

        public static float ProjectileSpeed(WeaponSlot w, IWeaponModifiers m) =>
            w.Config.projectileSpeed * w.SpeedMult * m.ProjectileSpeed;

        public static float Sized(float baseValue, WeaponSlot w, IWeaponModifiers m) => baseValue * w.SizeMult * m.Size;

        /// <summary>Volley size: base + upgrades + the Quantity stat (V3 does not clamp this one).</summary>
        public static int VolleyQuantity(WeaponSlot w, IWeaponModifiers m) =>
            w.Config.baseQuantity + w.QuantityBonus + Mathf.FloorToInt(m.Quantity);

        /// <summary>
        /// V5 M4: an Empowered evolution's numbers on top of the record's upgrades (the game's manager and the simulator
        /// call it after copying the upgrades, every sync, so it never stacks). Extra quantity lengthens a burst weapon's
        /// burst (its volley is one shot) and adds to everyone else's volley.
        /// </summary>
        public static void ApplyEvolution(WeaponSlot slot)
        {
            slot.EvolutionDamage = 1f;
            slot.ExtraPierce = 0;
            slot.ExtraSlow = 0f;
            slot.ExtraBurstShots = 0;
            slot.SpinMult = 1f; // V5 M8z (7 Oct, Q20)
            slot.ExtraSlowSeconds = 0f;
            slot.Echoes = 0;
            slot.EchoDelaySeconds = 0f;
            ApplyMilestones(slot);
            if (slot.EvolutionEffect != ConfigEnums.EvolutionEffect.Empowered) return;
            var e = slot.Evolution.empower;
            if (slot.Config != null && slot.Config.projectile.burstShots > 1) slot.ExtraBurstShots = e.extraQuantity;
            else slot.QuantityBonus += e.extraQuantity;
            slot.EvolutionDamage = e.damageMultiplier;
            slot.CooldownMult *= e.cooldownMultiplier;
            slot.SizeMult *= e.sizeMultiplier;
            slot.KnockbackMult *= e.knockbackMultiplier;
            // V5 M8z (7 Oct, Q20): on top of the milestones'. V5 M8z (8 Oct, close): Black Tide's slow stacks with the Ink
            // Pots' rank 12 slow (both add to ExtraSlow); Slow() caps the share at 0.9 and the enemy world at the weapon
            // rules' maxSlow.
            slot.ExtraPierce += e.extraPierce;
            slot.ExtraSlow += e.extraSlow;
        }

        /// <summary>
        /// V5 M8z (7 Oct, D21, Q20): the milestones the weapon's level has reached, on top of the record's upgrades (called
        /// from <see cref="ApplyEvolution"/> after the upgrades were copied, every sync, so they never stack).
        /// </summary>
        public static void ApplyMilestones(WeaponSlot slot)
        {
            var milestones = slot.Config != null ? slot.Config.milestones : null;
            if (milestones == null) return;
            for (var i = 0; i < milestones.Length; i++)
            {
                var m = milestones[i];
                if (m == null || slot.Level < m.level) continue;
                slot.QuantityBonus += m.extraQuantity;
                slot.SizeMult *= m.sizeMultiplier;
                slot.DurationMult *= m.durationMultiplier;
                slot.ExtraSlow += m.extraSlow;
                // V5 M8z (7 Oct, Q20): the other new weapons' milestones.
                slot.ExtraPierce += m.extraPierce;
                slot.SpinMult *= m.spinMultiplier > 0f ? m.spinMultiplier : 1f;
                slot.ExtraSlowSeconds += m.extraSlowSeconds;
                if (m.echoes > 0)
                {
                    slot.Echoes += m.echoes;
                    slot.EchoDelaySeconds = Mathf.Max(0.05f, m.echoDelaySeconds);
                }
            }
        }

        /// <summary>V5 M8z (7 Oct, Q20): a slow's seconds with the milestones' extra seconds, × the Duration card.</summary>
        public static float SlowSeconds(WeaponSlot w, float baseSeconds) =>
            w.ExtraSlowSeconds > 0f ? (baseSeconds + w.ExtraSlowSeconds) * w.DurationMult : baseSeconds * w.DurationMult;

        /// <summary>V5 M8z (7 Oct, D21): a weapon's slow share with the Slow cards, milestones and evolution (0 when the
        /// weapon itself does not slow), at most 0.9 (the enemy world caps it further).</summary>
        public static float Slow(WeaponSlot w, float baseFraction) =>
            baseFraction > 0f || w.ExtraSlow > 0f || w.SlowBonus > 0f ? Mathf.Min(0.9f, baseFraction + w.ExtraSlow + w.SlowBonus) : 0f;

        /// <summary>V5 M4b: the ticks a whirlpool deals before it implodes (whole ticks, whatever the frame rate).</summary>
        public static int WhirlpoolTicks(PredictedAreaBlock p) => WhirlpoolTicks(p, 1f);

        /// <summary>V5 M8z (7 Oct, D21): the ticks of a whirlpool lengthened by the Duration card (× <paramref name="durationMult"/>).</summary>
        public static int WhirlpoolTicks(PredictedAreaBlock p, float durationMult) =>
            p.whirlpoolTickSeconds > 0f ? Mathf.FloorToInt(p.whirlpoolSeconds * durationMult / p.whirlpoolTickSeconds + 1e-4f) : 0;

        /// <summary>
        /// V5 M4b: hits one firing deals per target relative to the nominal one, so damage per cooldown (the director's
        /// offense, the balance model) matches play: a summon strikes over its life (busy half of it: travel and
        /// retargeting); a whirlpool ticks, then implodes; a per-target orbit hits an enemy only while its blade or beam
        /// sweeps past (an enemy at 60% of the radius), not every interval. 1 for today's ten weapons.
        /// </summary>
        public static float HitsPerFiring(WeaponConfig w)
        {
            if (w == null) return 1f;
            if (w.behavior == ConfigEnums.WeaponBehavior.Summon && (w.summon.perQuantity || w.summon.retarget || w.summon.shotSpeed > 0f))
                return Mathf.Max(1f, 0.5f * w.summon.lifetimeSeconds / Mathf.Max(0.05f, w.summon.strikeInterval));
            if (w.behavior == ConfigEnums.WeaponBehavior.PredictedArea && w.predictedArea.whirlpoolSeconds > 0f)
            {
                var p = w.predictedArea;
                return 1f + WhirlpoolTicks(p) * p.whirlpoolTickDamageMultiplier + p.implodeDamageMultiplier;
            }
            if (w.behavior == ConfigEnums.WeaponBehavior.Orbit && w.orbit.perTargetIntervalSeconds > 0f)
            {
                var o = w.orbit;
                var omega = Mathf.Abs(o.angularSpeedDegrees) * Mathf.Deg2Rad;
                if (omega <= 0f) return 1f;
                var interval = o.perTargetIntervalSeconds;
                var r = Mathf.Max(0.5f, o.orbitRadius * 0.6f);
                var inContact = 2f * o.hitRadius / (r * omega);
                var passes = omega / (2f * Mathf.PI) * Mathf.Max(1, o.baseBlades);
                var hitsPerSecond = Mathf.Min(1f / interval, passes * Mathf.Max(1f, inContact / interval));
                return hitsPerSecond * interval;
            }
            return 1f;
        }

        public static int ProjectileQuantity(WeaponSlot w, IWeaponModifiers m) =>
            w.Config == null ? 1 : Mathf.Max(1, w.Config.baseQuantity + w.QuantityBonus + Mathf.FloorToInt(m.Quantity));
    }

    public struct PowderKeg
    {
        public bool Active;
        public Vector3 Position;
        public float ArmTime, TriggerRadius, BlastRadius, BaseDamage, DamageBonus, ProcCoefficient, EliteReach, BlastDamageMultiplier;
        /// <summary>Seconds until an unblown keg sinks (V5; infinity = never).</summary>
        public float LifeLeft, SinkSeconds;
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
        /// <summary>V5 M4b: the shell's weapon when its impact opens a whirlpool (null otherwise).</summary>
        public WeaponConfig Whirlpool;
        /// <summary>V5 M8z (7 Oct, D21): the slow the shell and its whirlpool apply (share, seconds; 0 = none) and the
        /// whirlpool's Duration multiplier, resolved when it was fired.</summary>
        public float SlowFraction, SlowSeconds, DurationMult;
        /// <summary>V5 M8z (D21): lands as a ring in its weapon's colour, not an explosion (WeaponEffectsVisual.pulseImpact).</summary>
        public bool PulseImpact;
    }

    /// <summary>V5 M8z (7 Oct, Q20): a weapon's firing to repeat after <see cref="Timer"/> seconds, <see cref="Left"/> more times.</summary>
    public struct PendingEcho
    {
        public WeaponSlot Weapon;
        public float Timer;
        public int Left;
    }

    /// <summary>V5 M4b: a whirlpool that drags enemies in and hurts them, then implodes (Maelstrom Charge).</summary>
    public struct Whirlpool
    {
        public bool Active;
        public Vector3 Position;
        public float Radius, Left, TickTimer, BaseDamage, DamageBonus, ProcCoefficient;
        public int TicksDone;
        public WeaponConfig Config;
        /// <summary>V5 M8z (7 Oct, D21): its whole ticks (the Duration card lengthens it) and the slow each tick applies.</summary>
        public int Ticks;
        public float SlowFraction, SlowSeconds;
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
        /// <summary>V5 M4b: retargets when its target is gone (range), fires shots instead of striking (speed, size).</summary>
        public bool Retarget;
        public float RetargetRange, ShotSpeed, ShotSize;
        public Color ShotColor;
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
        public readonly List<Whirlpool> Whirlpools = new List<Whirlpool>();
        /// <summary>V5 M8z (7 Oct, Q20): firings still to repeat (a milestone's echoes: the Siren Bell's second pulse).</summary>
        public readonly List<PendingEcho> Echoes = new List<PendingEcho>();
        /// <summary>Hackathon (12 Oct): chained maces in flight (the Anchor Flail).</summary>
        public readonly List<ChainedMace> Maces = new List<ChainedMace>();
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
            dmg = new DamageRoll(Healthy(enemyIndex, dmg.Amount), dmg.IsCrit);
            Hooks.RecordHit(weaponId, dmg.Amount, dmg.IsCrit, isElite, isBoss);
            Damage.ApplyDamage(enemyIndex, dmg.Amount, dmg.IsCrit, weaponId: weaponId);
        }

        /// <summary>
        /// A weapon hit outside the proc path. V3 recorded only lane, aura, strike and skiff hits in the damage ledger; V4
        /// records every weapon hit so the results' damage share covers all weapons (M5 intended difference; ledger only).
        /// V5 M8z (8 Oct, close): the hit's weapon (kill credit, the Hit event, the coins' kill gold) defaults to the ledger
        /// id, so orbit, strike, trail, cone and whirlpool kills are credited to their weapon (they were left uncredited).
        /// </summary>
        public void DamageAndRecord(int index, string ledgerId, float amount, bool isCrit, string weaponId = null)
        {
            amount = Healthy(index, amount);
            if (ledgerId != null) Hooks.RecordHit(ledgerId, amount, isCrit, Enemies.IsElite(index), Enemies.IsBoss(index));
            Damage.ApplyDamage(index, amount, isCrit, weaponId: weaponId ?? ledgerId); // V5 M8z (8 Oct, close)
        }

        /// <summary>V5 M5 Shark Tooth Necklace: more damage to an enemy still above the threshold share of its hull (read
        /// before the hit). × 1 without the item.</summary>
        public float Healthy(int index, float amount)
        {
            var threshold = 1f;
            var mult = Mods != null ? Mods.HealthyTargetDamageMultiplier(out threshold) : 1f;
            return mult != 1f && Enemies.HpFraction(index) > threshold ? amount * mult : amount;
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

        /// <summary>A slot's projectile traits: its config's plus an Empowered evolution's pierce and slow.</summary>
        public ProjectileTraits TraitsOf(WeaponSlot w)
        {
            var t = TraitsOf(w.Config);
            t.Pierce = Mathf.Min(7, t.Pierce + w.ExtraPierce);
            if (w.ExtraSlow > 0f || w.SlowBonus > 0f)
            {
                t.SlowFraction = WeaponStatResolver.Slow(w, t.SlowFraction);
                if (t.SlowSeconds <= 0f) t.SlowSeconds = 1.5f;
            }
            // V5 M8z (7 Oct, Q20): a milestone's longer slow (Chainshot rank 12); the shots' slow is not on the Duration card.
            if (t.SlowFraction > 0f && w.ExtraSlowSeconds > 0f) t.SlowSeconds += w.ExtraSlowSeconds;
            return t;
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
                TarSlowSeconds = p.tarSlowSeconds,
                ControlSecondsPerHit = p.controlSecondsPerHit,
                SplashOnDespawn = p.splashOnDespawn,
                Pierce = p.pierce,
                SlowFraction = p.slowFraction,
                SlowSeconds = p.slowSeconds,
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
            Whirlpools.Clear();
            Echoes.Clear();
            Maces.Clear();
        }
    }
}
