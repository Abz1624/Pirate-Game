using System.Collections.Generic;
using RoguePirates.V4.Config;
using UnityEngine;
using Stats = RoguePirates.V4.Weapons.WeaponStatResolver;
using Effect = RoguePirates.V4.Config.ConfigEnums.EvolutionEffect;
using Mode = RoguePirates.V4.Config.ConfigEnums.TargetingMode;

namespace RoguePirates.V4.Weapons
{
    /// <summary>
    /// One firing logic. Behaviors are stateless: per-weapon state is on the <see cref="WeaponSlot"/>, in-flight state
    /// (kegs, strikes, skiffs, follow-ups) on the shared <see cref="WeaponCombat"/>. Each is a port of the matching
    /// V3 WeaponController methods, with the same order of random draws, damage calls and effects.
    /// </summary>
    public abstract class WeaponBehavior
    {
        protected readonly WeaponCombat C;
        protected WeaponBehavior(WeaponCombat combat) { C = combat; }

        /// <summary>Most weapons wait for a target before spending their cooldown (V3: all but orbit, mines, trails and streams).</summary>
        public virtual bool NeedsTarget => true;

        /// <summary>A cooldown-driven shot. Emitted starts the cooldown.</summary>
        public abstract WeaponFireOutcome Fire(WeaponSlot w);

        /// <summary>"Fire all weapons" (captain active, Captain's Bell): one emission without the target gate.</summary>
        public virtual void FireOnce(WeaponSlot w) => Fire(w);

        protected Vector3 Muzzle => C.Ship.Position + Vector3.up * 0.35f;
    }

    // ---- Projectiles, bursts and lanes (V3 FireProjectile, FireSwivelShot, FirePierceLane) ----------------------------

    public sealed class ProjectileBehavior : WeaponBehavior
    {
        public ProjectileBehavior(WeaponCombat c) : base(c) { }

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            if (w.Config.projectile.burstShots > 1) return BeginBurst(w);
            // M5 intended difference: no cooldown without an emission (V3 spent it when the projectile pool was full).
            return FireProjectile(w) ? WeaponFireOutcome.Emitted : WeaponFireOutcome.CapacityBlocked;
        }

        public WeaponFireOutcome BeginBurst(WeaponSlot w)
        {
            var outcome = FireBurstShot(w);
            if (outcome != WeaponFireOutcome.Emitted) return outcome;
            w.BurstShotsLeft = w.Config.projectile.burstShots - 1;
            w.CooldownTimer = w.Config.projectile.burstGapSeconds; // V3: the manager then starts the full cooldown (R-87)
            return WeaponFireOutcome.Emitted;
        }

        /// <summary>V3 FireSwivelShot: one shot at the resolved target, aimed with the config speed's lead.</summary>
        public WeaponFireOutcome FireBurstShot(WeaponSlot w)
        {
            var def = w.Config;
            if (!C.Targeting.TryResolveTarget(def, out var target, out var dir))
                return WeaponFireOutcome.NoTarget;
            if (C.Projectiles.ActiveCount >= C.Projectiles.Capacity) return WeaponFireOutcome.CapacityBlocked;

            var isElite = C.Enemies.IsElite(target);
            C.NotifyAimed(w, dir, target);
            var p = def.projectile;
            var speed = Stats.ProjectileSpeed(w, C.Mods);
            var size = Mathf.Max(p.minSize, p.sizeBase * w.SizeMult * C.Mods.Size * p.sizeMultiplier);
            var dmg = C.Calculator.Roll(def.baseDamage * p.damageMultiplier, Stats.LevelMult(w), isElite, 0f, def.procCoefficient, def.weaponId);
            var origin = Muzzle;
            C.Effects?.Fired(def.weaponId, origin, dir);
            if (!C.Projectiles.TrySpawn(origin, dir, speed, dmg.Amount, p.lifeSeconds, size, dmg.IsCrit, def.projectileColor, def.weaponId, C.TraitsOf(def), target))
                return WeaponFireOutcome.CapacityBlocked;
            return WeaponFireOutcome.Emitted;
        }

        public override void FireOnce(WeaponSlot w)
        {
            if (w.Config.projectile.burstShots > 1) BeginBurst(w);
            else FireProjectile(w);
        }

        /// <summary>True when a projectile or lane went out.</summary>
        bool FireProjectile(WeaponSlot w)
        {
            var def = w.Config;
            var hasEnemyTarget = C.Targeting.TryResolveTarget(def, out var target, out var dir);
            if (!hasEnemyTarget && !C.Hooks.TryResolveShipAim(def.weaponId, out dir))
                return false;

            if (!hasEnemyTarget)
                return FireVolley(w, dir, false) > 0;

            if (def.lane.enabled || w.EvolutionEffect == Effect.LaneWithBurst)
            {
                FireLane(w, dir, target);
                return true;
            }

            var isElite = C.Enemies.IsElite(target);
            C.NotifyAimed(w, dir, target);
            return FireVolley(w, dir, isElite, target) > 0;
        }

        /// <summary>Returns the number of projectiles spawned.</summary>
        int FireVolley(WeaponSlot w, Vector3 dir, bool isElite, int tetherEnemyIndex = -1)
        {
            var def = w.Config;
            var p = def.projectile;
            // Pool full: retry next frame without rolling damage or playing effects (V3 rolled, failed and spent the cooldown).
            if (C.Projectiles.ActiveCount >= C.Projectiles.Capacity) return 0;
            var qty = Stats.VolleyQuantity(w, C.Mods);
            var speed = Stats.ProjectileSpeed(w, C.Mods);
            var size = Mathf.Max(p.minSize, p.sizeBase * w.SizeMult * C.Mods.Size * p.sizeMultiplier);
            if (p.leadWithActualSpeed && tetherEnemyIndex >= 0 && tetherEnemyIndex < C.Enemies.Capacity && C.Enemies.IsActive(tetherEnemyIndex))
                dir = WeaponTargetingService.ComputeLeadDirection(C.Ship.Position, C.Enemies.Position(tetherEnemyIndex), C.Enemies.Velocity(tetherEnemyIndex), speed);

            var traits = C.TraitsOf(def);
            var spawned = 0;
            for (var i = 0; i < qty; i++)
            {
                var spread = Quaternion.Euler(0f, (i - qty * 0.5f) * p.spreadStepDegrees, 0f) * dir;
                var dmg = C.Calculator.Roll(def.baseDamage * p.damageMultiplier, Stats.LevelMult(w), isElite, 0f, def.procCoefficient, def.weaponId);
                var origin = Muzzle;
                C.Effects?.Fired(def.weaponId, origin, spread);
                if (C.Projectiles.TrySpawn(origin, spread, speed, dmg.Amount, p.lifeSeconds, size, dmg.IsCrit, def.projectileColor, def.weaponId, traits, tetherEnemyIndex))
                    spawned++;
            }
            return spawned;
        }

        void FireLane(WeaponSlot w, Vector3 dir, int primaryTarget)
        {
            var volleys = Stats.ProjectileQuantity(w, C.Mods);
            for (var v = 0; v < volleys; v++)
            {
                var laneDir = dir;
                if (volleys > 1)
                {
                    var spread = (v - (volleys - 1) * 0.5f) * w.Config.lane.volleySpreadDegrees;
                    laneDir = Quaternion.Euler(0f, spread, 0f) * dir;
                }
                FireLaneVolley(w, laneDir, v == 0 ? primaryTarget : -1);
            }
        }

        void FireLaneVolley(WeaponSlot w, Vector3 dir, int primaryTarget)
        {
            var def = w.Config;
            C.NotifyAimed(w, dir, primaryTarget);
            var origin = C.Ship.Position;
            origin.y = 0f;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) return;
            dir.Normalize();

            var muzzle = Muzzle;
            var hits = C.LaneIndexScratch;
            if (def.lane.beam)
            {
                var halfWidth = def.lane.beamHalfWidth * w.SizeMult * C.Mods.Size;
                CollectInBeam(origin, dir, def.range, halfWidth, hits);
                C.Effects?.Fired(def.weaponId, muzzle, dir);
                C.Effects?.Beam(muzzle, dir, def.range, halfWidth);
            }
            else
            {
                // R-18 fix: an evolved lane (Stormshot) has its own arc; V3 used the weapon's 360° and hit everything in range.
                var laneArc = w.EvolutionEffect == Effect.LaneWithBurst && w.Evolution.laneBurst.laneArcDegrees > 0f
                    ? w.Evolution.laneBurst.laneArcDegrees : WeaponTargetingService.ResolveArcDegrees(def);
                CollectInLane(origin, dir, def.range, laneArc, hits);
            }

            var hitCount = 0;
            for (var i = 0; i < hits.Count; i++)
            {
                var index = hits[i];
                if (!C.Enemies.IsActive(index)) continue;
                hitCount++;
                var isElite = C.Enemies.IsElite(index);
                var dmg = C.Calculator.Roll(def.baseDamage, Stats.LevelMult(w), isElite, 0f, def.procCoefficient, def.weaponId);
                C.ApplyWeaponDamage(index, def.weaponId, dmg, C.Enemies.IsElite(index), C.Enemies.IsBoss(index));
                if (!def.lane.beam)
                    C.Effects?.Fired(def.weaponId, C.Enemies.Position(index) + Vector3.up * 0.35f, dir);

                if (i == 0 && w.EvolutionEffect == Effect.LaneWithBurst)
                    EmitLaneBurst(w, C.Enemies.Position(index), dir);
            }

            C.Hooks.RecordConcurrentTargets(def.weaponId, hitCount);
        }

        void CollectInBeam(Vector3 origin, Vector3 forward, float range, float halfWidth, List<int> results)
        {
            results.Clear();
            var positions = C.LaneHitScratch;
            positions.Clear();
            for (var i = 0; i < C.Enemies.Capacity; i++)
            {
                if (!C.Enemies.IsActive(i)) continue;
                var position = C.Enemies.Position(i);
                if (!IsInsideBeam(origin, forward, range, halfWidth, position, C.Enemies.Radius(i))) continue;
                results.Add(i);
                positions.Add(position);
            }
            SortAlong(origin, forward, results, positions);
        }

        void CollectInLane(Vector3 origin, Vector3 forward, float range, float arcDegrees, List<int> results)
        {
            results.Clear();
            var positions = C.LaneHitScratch;
            positions.Clear();
            for (var i = 0; i < C.Enemies.Capacity; i++)
            {
                if (!C.Enemies.IsActive(i)) continue;
                var position = C.Enemies.Position(i);
                var delta = position - origin;
                delta.y = 0f;
                if (delta.sqrMagnitude > range * range) continue;
                if (!WeaponTargetingService.IsWithinForwardArc(origin, forward, position, arcDegrees)) continue;
                results.Add(i);
                positions.Add(position);
            }
            SortAlong(origin, forward, results, positions);
        }

        /// <summary>V3's exchange sort by distance along the lane (kept: its order decides hit order on ties).</summary>
        static void SortAlong(Vector3 origin, Vector3 forward, List<int> results, List<Vector3> positions)
        {
            for (var a = 0; a < results.Count - 1; a++)
            {
                for (var b = a + 1; b < results.Count; b++)
                {
                    var da = Vector3.Dot(positions[a] - origin, forward);
                    var db = Vector3.Dot(positions[b] - origin, forward);
                    if (da <= db) continue;
                    (results[a], results[b]) = (results[b], results[a]);
                    (positions[a], positions[b]) = (positions[b], positions[a]);
                }
            }
        }

        public static bool IsInsideBeam(Vector3 origin, Vector3 forward, float range, float halfWidth, Vector3 targetPosition, float targetRadius)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return false;
            forward.Normalize();
            var delta = targetPosition - origin;
            delta.y = 0f;
            var along = Vector3.Dot(delta, forward);
            if (along < 0f || along > range) return false;
            var lateral = delta - forward * along;
            var limit = halfWidth + Mathf.Max(0f, targetRadius);
            return lateral.sqrMagnitude <= limit * limit;
        }

        void EmitLaneBurst(WeaponSlot w, Vector3 hitPoint, Vector3 forward)
        {
            var def = w.Config;
            var b = w.Evolution.laneBurst;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();
            var pellets = b.pellets;
            var spread = b.spreadDegrees;
            var halfSpread = spread * 0.5f;
            var speed = Stats.ProjectileSpeed(w, C.Mods);
            var size = b.sizeBase * w.SizeMult * C.Mods.Size;
            for (var i = 0; i < pellets; i++)
            {
                var angle = pellets <= 1 ? 0f : -halfSpread + (spread / (pellets - 1)) * i;
                var dir = Quaternion.Euler(0f, angle, 0f) * forward;
                var origin = hitPoint + Vector3.up * 0.35f;
                var end = origin + dir * b.rayLength;
                var hitIndex = C.FindFirstEnemyAlongRay(origin, end, b.hitRadius);
                if (hitIndex < 0) continue;
                var dmg = C.Calculator.Roll(def.baseDamage * b.damageMultiplier, w.DamageBonus, C.Enemies.IsElite(hitIndex), 0f, b.procCoefficient, def.weaponId);
                C.Projectiles.TrySpawn(origin, dir, speed, dmg.Amount, b.lifeSeconds, size, dmg.IsCrit, def.projectileColor, null, ProjectileTraits.Credited(def.weaponId));
            }
        }
    }

    // ---- Pellet fans (V3 FireFan, TickPendingBroadsides) --------------------------------------------------------------

    public sealed class FanBehavior : WeaponBehavior
    {
        readonly FollowUpBlock _defaultFollowUp = new FollowUpBlock();
        public FanBehavior(WeaponCombat c) : base(c) { }

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            var f = w.Config.fan;
            // M5 intended difference: a fan that finds nothing to fire at keeps its cooldown (V3 spent it).
            return FireFan(w, f.basePellets + (f.quantityAddsPellets ? w.QuantityBonus : 0) + Mathf.FloorToInt(C.Mods.Quantity))
                ? WeaponFireOutcome.Emitted : WeaponFireOutcome.NoTarget;
        }

        public override void FireOnce(WeaponSlot w)
        {
            var f = w.Config.fan;
            FireFan(w, f.basePellets + (f.quantityAddsPellets ? w.QuantityBonus : 0) + Mathf.FloorToInt(C.Mods.Quantity));
        }

        bool FireFan(WeaponSlot w, int pellets)
        {
            var def = w.Config;
            var f = def.fan;
            Vector3 baseDir;
            var notifyTarget = -1;
            if (def.targeting == Mode.PortStarboard)
            {
                if (!C.Targeting.TryResolveTarget(def, out notifyTarget, out baseDir) && !C.Hooks.TryResolveShipAim(def.weaponId, out baseDir))
                    return false;
            }
            else if (def.targeting == Mode.ForwardCone)
            {
                baseDir = C.Ship.MovementForward;
                baseDir.y = 0f;
                if (baseDir.sqrMagnitude < 0.0001f) return false;
                baseDir.Normalize();
                if (!C.Targeting.HasEnemyInForwardArc(C.Ship.Position, baseDir, def.range, WeaponTargetingService.ResolveArcDegrees(def)))
                    return false;
            }
            else if (!C.Targeting.TryResolveTarget(def, out var fanTarget, out baseDir))
            {
                return false;
            }
            else
            {
                notifyTarget = fanTarget;
            }

            C.NotifyAimed(w, baseDir, notifyTarget);
            if (!f.muzzlePerPellet)
                C.Effects?.SideSalvo(def.weaponId, C.Ship.Position, baseDir);

            var speed = Mathf.Max(Stats.ProjectileSpeed(w, C.Mods), f.minSpeed);
            var size = f.sizeBase * w.SizeMult * C.Mods.Size;
            var spread = def.fanSpreadDegrees;
            var halfSpread = spread * 0.5f;
            var traits = C.TraitsOf(def);
            traits.SplashOnDespawn = f.splashOnDespawn;

            var hits = 0;
            for (var i = 0; i < pellets; i++)
            {
                var angle = pellets <= 1 ? 0f : -halfSpread + (spread / (pellets - 1)) * i;
                var dir = Quaternion.Euler(0f, angle, 0f) * baseDir;
                var origin = Muzzle;
                if (def.targeting == Mode.PortStarboard)
                {
                    var side = Vector3.Cross(Vector3.up, C.Ship.MovementForward).normalized;
                    var sideSign = Vector3.Dot(dir, side) >= 0f ? 1f : -1f;
                    origin += Vector3.Cross(Vector3.up, C.Ship.MovementForward).normalized * sideSign * f.sideMuzzleOffset;
                }

                var end = origin + dir * def.range;
                var hitIndex = C.FindFirstEnemyAlongRay(origin, end, f.hitRadius);
                var isElite = hitIndex >= 0 && C.Enemies.IsActive(hitIndex) && C.Enemies.IsElite(hitIndex);
                var dmg = C.Calculator.Roll(def.baseDamage * f.damageMultiplier, Stats.LevelMult(w), isElite, 0f, def.procCoefficient, def.weaponId);
                if (f.muzzlePerPellet)
                    C.Effects?.Fired(def.weaponId, origin, dir);
                if (C.Projectiles.TrySpawn(origin, dir, speed, dmg.Amount, f.lifeSeconds, size, dmg.IsCrit, def.projectileColor, def.weaponId, traits,
                        f.trackPelletTargets ? hitIndex : -1))
                {
                    if (f.trackPelletTargets && hitIndex >= 0)
                        hits++;
                }
            }
            C.Hooks.RecordConcurrentTargets(def.weaponId, hits);

            if (w.EvolutionEffect == Effect.OppositeSideFollowUp)
            {
                var forward = C.Ship.MovementForward;
                forward.y = 0f;
                forward.Normalize();
                var right = Vector3.Cross(Vector3.up, forward).normalized;
                var opposite = Vector3.Dot(baseDir, right) >= 0f ? -right : right;
                C.FollowUps.Add(new PendingFollowUp { Active = true, Timer = w.Evolution.followUp.delaySeconds, SideDirection = opposite, Weapon = w });
            }
            return true;
        }

        /// <summary>V3 TickPendingBroadsides (King's Broadside follow-ups).</summary>
        public void TickFollowUps(float dt)
        {
            var list = C.FollowUps;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var pending = list[i];
                if (!pending.Active) { list.RemoveAt(i); continue; }

                pending.Timer -= dt;
                if (pending.Timer > 0f) { list[i] = pending; continue; }

                var w = pending.Weapon;
                if (w?.Config == null) { list.RemoveAt(i); continue; }
                var def = w.Config;
                var f = w.Evolution != null ? w.Evolution.followUp : _defaultFollowUp;
                var pellets = Mathf.Max(f.minPellets, Stats.ProjectileQuantity(w, C.Mods));
                var speed = Stats.ProjectileSpeed(w, C.Mods);
                var size = f.sizeBase * w.SizeMult * C.Mods.Size;
                var hits = 0;
                for (var p = 0; p < pellets; p++)
                {
                    var angle = (p - pellets * 0.5f) * f.angleStepDegrees;
                    var dir = Quaternion.Euler(0f, angle, 0f) * pending.SideDirection;
                    var origin = Muzzle;
                    var end = origin + dir * def.range;
                    var hitIndex = C.FindFirstEnemyAlongRay(origin, end, f.hitRadius);
                    if (hitIndex < 0) continue;
                    hits++;
                    var dmg = C.Calculator.Roll(def.baseDamage * f.damageMultiplier, w.DamageBonus, C.Enemies.IsElite(hitIndex), 0f, def.procCoefficient, def.weaponId);
                    C.Projectiles.TrySpawn(origin, dir, speed, dmg.Amount, f.lifeSeconds, size, dmg.IsCrit, def.projectileColor, null, ProjectileTraits.Credited(def.weaponId));
                }

                C.Hooks.RecordConcurrentTargets(def.weaponId, hits);
                list.RemoveAt(i);
            }
        }
    }

    // ---- Orbiting blades (V3 FireOrbit, TickCutlassDamage, WeaponJuiceController.SyncCutlasses) -----------------------

    public sealed class OrbitBehavior : WeaponBehavior
    {
        public struct HitSphere
        {
            public Vector3 Position;
            public float Radius;
        }

        readonly List<HitSphere> _hits = new List<HitSphere>();
        readonly HashSet<int> _hitThisTick = new HashSet<int>();
        float _hitTimer;

        public OrbitBehavior(WeaponCombat c) : base(c) { }
        public override bool NeedsTarget => false;
        public IReadOnlyList<HitSphere> Hits => _hits;

        /// <summary>Blades deal damage continuously (<see cref="TickHits"/>); the cooldown only counts emissions.</summary>
        public override WeaponFireOutcome Fire(WeaponSlot w) => WeaponFireOutcome.Emitted;

        /// <summary>
        /// Blade positions for the next hit tick. V3 computed them in a presentation Update at order 50 from Time.time and
        /// the blade transforms parented to the ship, and the weapon read them the frame after; V4 keeps that timing
        /// (its order-50 slot calls this).
        /// </summary>
        public void ComputeHits(IReadOnlyList<WeaponSlot> slots, float time)
        {
            WeaponSlot w = null;
            for (var i = 0; i < slots.Count; i++)
                if (slots[i].Config != null && slots[i].Config.behavior == ConfigEnums.WeaponBehavior.Orbit) { w = slots[i]; break; }
            _hits.Clear();
            if (w == null || C.Ship == null || !C.Ship.Exists) return;

            var o = w.Config.orbit;
            var count = o.baseBlades + w.QuantityBonus;
            var orbitRadius = o.orbitRadius * w.SizeMult;
            if (w.EvolutionEffect == Effect.PulsingOrbit)
            {
                var pulse = w.Evolution.orbitPulse;
                orbitRadius *= pulse.minimum + pulse.amplitude * (0.5f + 0.5f * Mathf.Sin(time * pulse.frequency));
            }
            for (var index = 0; index < count; index++)
            {
                var angle = (time * o.angularSpeedDegrees + index * (360f / Mathf.Max(1, count))) * Mathf.Deg2Rad;
                var localOffset = new Vector3(Mathf.Sin(angle), o.heightOffset, Mathf.Cos(angle)) * orbitRadius;
                _hits.Add(new HitSphere { Position = C.Ship.TransformPoint(localOffset), Radius = o.hitRadius });
            }
        }

        public void ClearHits() => _hits.Clear();

        public void TickHits(IReadOnlyList<WeaponSlot> slots, float dt)
        {
            if (_hits.Count == 0) return;
            WeaponSlot w = null;
            for (var i = 0; i < slots.Count; i++)
                if (slots[i].Config != null && slots[i].Config.behavior == ConfigEnums.WeaponBehavior.Orbit) { w = slots[i]; break; }
            if (w == null) return;

            var o = w.Config.orbit;
            _hitTimer -= dt;
            if (_hitTimer > 0f) return;
            _hitTimer = C.OrbitVolleyTimer > 0f ? o.volleyHitIntervalSeconds : o.hitIntervalSeconds;

            var def = w.Config;
            _hitThisTick.Clear();
            for (var h = 0; h < _hits.Count; h++)
            {
                var sphere = _hits[h];
                var radius = sphere.Radius * w.SizeMult * C.Mods.Size;
                var radiusSq = radius * radius;
                for (var i = 0; i < C.Enemies.Capacity; i++)
                {
                    if (!C.Enemies.IsActive(i) || _hitThisTick.Contains(i)) continue;
                    var delta = C.Enemies.Position(i) - sphere.Position;
                    if (delta.sqrMagnitude > radiusSq) continue;
                    _hitThisTick.Add(i);
                    var dmg = C.Calculator.RollAmount(def.baseDamage * o.damageMultiplier, Stats.LevelMult(w), C.Enemies.IsElite(i), 0f, def.procCoefficient, def.weaponId);
                    C.DamageAndRecord(i, def.weaponId, dmg, false);
                }
            }
        }
    }

    // ---- Flame streams (V3 TickFlamethrowerWeapon, ApplyFlamethrowerTick, FireFlamethrower) ---------------------------

    public sealed class ConeStreamBehavior : WeaponBehavior
    {
        public ConeStreamBehavior(WeaponCombat c) : base(c) { }
        public override bool NeedsTarget => false;

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            FireOnce(w);
            return WeaponFireOutcome.Emitted;
        }

        public override void FireOnce(WeaponSlot w)
        {
            var s = w.Config.stream;
            if (w.StreamRemaining <= 0f && w.RechargeRemaining <= 0f)
            {
                w.StreamRemaining = StreamDuration(w);
                w.StreamTickTimer = s.tickIntervalSeconds;
            }
            ApplyTick(w, 1f, s.tickIntervalSeconds);
        }

        /// <summary>Streams run on their own clock instead of the cooldown.</summary>
        public void TickSlot(WeaponSlot w, float dt, float itemAttackSpeed)
        {
            if (C.Enemies == null || !C.Enemies.Exists) return;
            var s = w.Config.stream;
            // R-22 fix: the recharge time already divides by attack speed; V3 also sped up its countdown (twice).
            var rules = C.Rules();
            var attackSpeed = rules != null && rules.itemAttackSpeedAppliedOnce ? 1f : C.Mods.AttackSpeed * itemAttackSpeed;
            if (w.RechargeRemaining > 0f)
            {
                w.RechargeRemaining = Mathf.Max(0f, w.RechargeRemaining - dt * attackSpeed);
                return;
            }

            if (w.StreamRemaining <= 0f)
            {
                if (!CanStart(w)) return;
                w.StreamRemaining = StreamDuration(w);
                w.StreamTickTimer = 0f;
                C.RecordEmission(w);
                C.NotifyAimed(w, C.Ship.MovementForward);
            }

            w.StreamRemaining = Mathf.Max(0f, w.StreamRemaining - dt);
            w.StreamTickTimer += dt;
            while (w.StreamTickTimer >= s.tickIntervalSeconds)
            {
                w.StreamTickTimer -= s.tickIntervalSeconds;
                ApplyTick(w, itemAttackSpeed, s.tickIntervalSeconds);
            }

            if (w.StreamRemaining <= 0f)
                w.RechargeRemaining = RechargeDuration(w, itemAttackSpeed);
        }

        float Range(WeaponSlot w) => w.Config.range * w.SizeMult * C.Mods.Size * C.Mods.FlameRangeMultiplier;

        float Arc(WeaponSlot w)
        {
            var arc = WeaponTargetingService.ResolveArcDegrees(w.Config);
            if (w.EvolutionEffect == Effect.WideConeWithPatches) arc *= w.Evolution.wideCone.arcMultiplier;
            return arc;
        }

        bool CanStart(WeaponSlot w)
        {
            var forward = C.Ship.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return false;
            forward.Normalize();
            return C.Targeting.HasEnemyInForwardArc(C.Ship.Position, forward, Range(w), Arc(w));
        }

        float StreamDuration(WeaponSlot w) => w.Config.stream.streamSeconds * w.DurationMult * C.Mods.Duration;

        float RechargeDuration(WeaponSlot w, float itemAttackSpeed)
        {
            var cd = Stats.Cooldown(w, C.Mods, itemAttackSpeed);
            return Mathf.Max(w.Config.stream.rechargeMinSeconds, cd * w.Config.stream.rechargeCooldownFactor);
        }

        float DamagePerTick(WeaponSlot w, float itemAttackSpeed)
        {
            var stream = StreamDuration(w);
            var recharge = RechargeDuration(w, itemAttackSpeed);
            var effectiveCd = Stats.Cooldown(w, C.Mods, itemAttackSpeed);
            var hitsInStream = stream / w.Config.stream.tickIntervalSeconds;
            if (hitsInStream < 1f) return w.Config.baseDamage;
            var hitsPerCycleOld = (stream + recharge) / Mathf.Max(0.05f, effectiveCd);
            return w.Config.baseDamage * hitsPerCycleOld / hitsInStream;
        }

        void ApplyTick(WeaponSlot w, float itemAttackSpeed, float tickSeconds)
        {
            var def = w.Config;
            var forward = C.Ship.MovementForward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) return;
            forward.Normalize();

            var origin = C.Ship.Position + forward * def.stream.forwardOffset;
            var range = Range(w);
            var halfAngle = Arc(w) * 0.5f;
            var burnDuration = def.duration * w.DurationMult * C.Mods.Duration;
            var damagePerTick = DamagePerTick(w, itemAttackSpeed);
            var patches = w.EvolutionEffect == Effect.WideConeWithPatches && w.Evolution.wideCone.patchesOnKill;

            C.Effects?.FlameBurst(origin, forward);
            for (var i = 0; i < C.Enemies.Capacity; i++)
            {
                if (!C.Enemies.IsActive(i)) continue;
                var position = C.Enemies.Position(i);
                var toEnemy = position - origin;
                toEnemy.y = 0f;
                if (toEnemy.sqrMagnitude < 0.01f) continue;
                if (toEnemy.magnitude > range) continue;
                if (Vector3.Angle(forward, toEnemy) > halfAngle) continue;

                var hpBefore = C.Enemies.Hp(i);
                var dmg = C.Calculator.RollAmount(damagePerTick, Stats.LevelMult(w), C.Enemies.IsElite(i), 0f,
                    def.procCoefficient * (tickSeconds / def.stream.tickIntervalSeconds), def.weaponId);
                dmg *= C.Mods.BurnDamageMultiplier;
                C.DamageAndRecord(i, def.weaponId, dmg, false);
                if (patches && hpBefore > 0f && hpBefore <= dmg)
                    C.Hooks.SpawnFlamePatch(position, def.weaponId);
                C.Damage.ApplyStatus(i, WeaponStatus.Burn, burnDuration);
            }
        }
    }

    // ---- Burning trail (V3 FireTrail) ----------------------------------------------------------------------------------

    public sealed class TrailBehavior : WeaponBehavior
    {
        public TrailBehavior(WeaponCombat c) : base(c) { }
        public override bool NeedsTarget => false;

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            var def = w.Config;
            var t = def.trail;
            var back = C.Ship.Velocity.sqrMagnitude > 0.01f ? -C.Ship.Velocity.normalized : -C.Ship.MovementForward;
            var pos = C.Ship.Position + back * t.backOffset;
            pos.y = t.height;

            var radius = t.radiusBase * w.SizeMult * C.Mods.Size;
            var burnDuration = def.duration * w.DurationMult * C.Mods.Duration;
            C.Effects?.BurningPatch(pos, radius);
            for (var i = 0; i < C.Enemies.Capacity; i++)
            {
                if (!C.Enemies.IsActive(i)) continue;
                if (Vector3.Distance(C.Enemies.Position(i), pos) > radius) continue;
                var dmg = C.Calculator.RollAmount(def.baseDamage * t.damageMultiplier, Stats.LevelMult(w), C.Enemies.IsElite(i), 0f, def.procCoefficient, def.weaponId);
                C.DamageAndRecord(i, def.weaponId, dmg, false);
                C.Damage.ApplyStatus(i, WeaponStatus.Burn, burnDuration);
            }
            return WeaponFireOutcome.Emitted;
        }
    }

    // ---- Powder kegs (V3 FireMine, TickPowderKegs, DetonatePowderKeg) -------------------------------------------------

    public sealed class MineBehavior : WeaponBehavior
    {
        public MineBehavior(WeaponCombat c) : base(c) { }
        public override bool NeedsTarget => false;

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            var m = w.Config.mine;
            var back = C.Ship.Velocity.sqrMagnitude > 0.01f ? -C.Ship.Velocity.normalized : -C.Ship.MovementForward;
            var pos = C.Ship.Position + back * m.backOffset;
            pos.y = m.height;
            var view = C.Effects != null ? C.Effects.MineDropped(pos) : -1;
            C.NotifyAimed(w, back);
            AddKeg(w, pos, view);
            if (w.EvolutionEffect == Effect.WakeKegs)
            {
                var right = Vector3.Cross(Vector3.up, back).normalized;
                var sidePos = pos + right * w.Evolution.wakeKegs.sideOffset;
                AddKeg(w, sidePos, C.Effects != null ? C.Effects.MineDropped(sidePos) : -1);
            }
            return WeaponFireOutcome.Emitted;
        }

        void AddKeg(WeaponSlot w, Vector3 pos, int view)
        {
            var m = w.Config.mine;
            C.Kegs.Add(new PowderKeg
            {
                Active = true,
                Position = pos,
                ArmTime = m.armSeconds,
                TriggerRadius = m.triggerRadius * w.SizeMult * C.Mods.Size,
                BlastRadius = m.blastRadius * w.SizeMult * C.Mods.Size,
                BaseDamage = w.Config.baseDamage,
                DamageBonus = Stats.LevelMult(w),
                ProcCoefficient = w.Config.procCoefficient,
                EliteReach = m.eliteReachBonus,
                BlastDamageMultiplier = m.blastDamageMultiplier,
                Source = w,
                View = view,
            });
        }

        bool Touches(Vector3 kegPosition, float kegRadius, int agent, float eliteReach)
        {
            var reach = C.Enemies.Radius(agent) + (C.Enemies.IsElite(agent) ? eliteReach : 0f);
            return WeaponCombat.HorizontalDistance(kegPosition, C.Enemies.Position(agent)) <= kegRadius + reach;
        }

        public void TickKegs(float dt)
        {
            if (C.Enemies == null || !C.Enemies.Exists) return;
            var list = C.Kegs;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var keg = list[i];
                if (!keg.Active) { list.RemoveAt(i); continue; }

                keg.ArmTime -= dt;
                if (keg.ArmTime > 0f) { list[i] = keg; continue; }

                var triggered = false;
                for (var ai = 0; ai < C.Enemies.Capacity; ai++)
                {
                    if (!C.Enemies.IsActive(ai)) continue;
                    if (!Touches(keg.Position, keg.TriggerRadius, ai, keg.EliteReach)) continue;
                    triggered = true;
                    break;
                }

                if (!triggered) { list[i] = keg; continue; }

                Detonate(keg);
                list.RemoveAt(i);
            }
        }

        void Detonate(PowderKeg keg)
        {
            var id = keg.Source?.Id;
            var blastRadius = Mathf.Max(keg.BlastRadius, keg.TriggerRadius) * C.Mods.PowderKegBlastRadiusMultiplier;
            C.Hooks.BeginDamageResolution();
            for (var ai = 0; ai < C.Enemies.Capacity; ai++)
            {
                if (!C.Enemies.IsActive(ai)) continue;
                if (!Touches(keg.Position, blastRadius, ai, keg.EliteReach)) continue;
                var dmg = C.Calculator.RollAmount(keg.BaseDamage * keg.BlastDamageMultiplier, keg.DamageBonus, C.Enemies.IsElite(ai), 0f,
                    keg.ProcCoefficient, id, WeaponDamageKind.TriggeredItem);
                if (C.Hooks.TryRegisterProcEvent()) C.DamageAndRecord(ai, id, dmg, false, id);
            }

            var shipBlastDamage = C.Calculator.RollAmount(keg.BaseDamage * keg.BlastDamageMultiplier, keg.DamageBonus, false, 0f,
                keg.ProcCoefficient, id, WeaponDamageKind.TriggeredItem);
            C.Hooks.ApplyShipRadiusDamage(keg.Position, blastRadius, shipBlastDamage, id);

            C.Effects?.Explosion(keg.Position, blastRadius);
            if (keg.Source != null && keg.Source.EvolutionEffect == Effect.WakeKegs)
                ChainNearbyKegs(keg.Position, keg.Source.Evolution.wakeKegs);
            if (keg.View >= 0) C.Effects?.MineRemoved(keg.View);
        }

        void ChainNearbyKegs(Vector3 origin, WakeKegsBlock wake)
        {
            var list = C.Kegs;
            for (var i = 0; i < list.Count; i++)
            {
                var keg = list[i];
                if (!keg.Active) continue;
                if (Vector3.Distance(keg.Position, origin) > wake.chainRadius) continue;
                keg.ArmTime = Mathf.Min(keg.ArmTime, wake.chainArmSeconds);
                list[i] = keg;
            }
        }

        public void ArmAll()
        {
            var list = C.Kegs;
            for (var i = 0; i < list.Count; i++)
            {
                var keg = list[i];
                if (!keg.Active) continue;
                keg.ArmTime = 0f;
                list[i] = keg;
            }
        }
    }

    // ---- Area strikes (V3 FireLightning) -------------------------------------------------------------------------------

    public sealed class AreaStrikeBehavior : WeaponBehavior
    {
        public AreaStrikeBehavior(WeaponCombat c) : base(c) { }

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            var def = w.Config;
            var a = def.areaStrike;
            if (!C.Targeting.TryResolveTarget(def, out var target, out var aimDir))
            {
                if (!C.Enemies.TryGetNearest(C.Ship.Position, def.range, out target, false))
                    return WeaponFireOutcome.NoTarget;
                aimDir = (C.Enemies.Position(target) - C.Ship.Position).normalized;
            }

            C.NotifyAimed(w, aimDir, target);
            var strikePos = C.Enemies.Position(target);
            strikePos.y = 0f;
            var radius = (a.radiusBase + w.QuantityBonus * a.radiusPerQuantity) * w.SizeMult * C.Mods.Size;
            if (a.chain)
            {
                FireChain(w, target);
                if (w.EvolutionEffect == Effect.JumpAndDelayedStrike) QueueDelayedStrike(w, radius);
                return WeaponFireOutcome.Emitted;
            }
            C.Effects?.LightningStrike(strikePos, radius);

            for (var i = 0; i < C.Enemies.Capacity; i++)
            {
                if (!C.Enemies.IsActive(i)) continue;
                if (Vector3.Distance(C.Enemies.Position(i), strikePos) > radius) continue;
                var dmg = C.Calculator.RollAmount(def.baseDamage, Stats.LevelMult(w), C.Enemies.IsElite(i), 0f, def.procCoefficient, def.weaponId);
                C.DamageAndRecord(i, def.weaponId, dmg, false);
                C.Damage.ApplyStatus(i, WeaponStatus.Shock, a.shockSeconds * w.DurationMult);
            }

            if (w.EvolutionEffect != Effect.JumpAndDelayedStrike) return WeaponFireOutcome.Emitted;
            var j = w.Evolution.jumpStrike;

            var jumpTarget = -1;
            var bestDist = float.MaxValue;
            for (var k = 0; k < C.Enemies.Capacity; k++)
            {
                if (!C.Enemies.IsActive(k) || k == target) continue;
                var dist = Vector3.Distance(C.Enemies.Position(k), strikePos);
                if (dist > j.jumpRange || dist >= bestDist) continue;
                bestDist = dist;
                jumpTarget = k;
            }

            if (jumpTarget >= 0)
            {
                var jumpPos = C.Enemies.Position(jumpTarget);
                jumpPos.y = 0f;
                var jumpRadius = radius * j.jumpRadiusFactor;
                C.Effects?.LightningStrike(jumpPos, jumpRadius);
                for (var i = 0; i < C.Enemies.Capacity; i++)
                {
                    if (!C.Enemies.IsActive(i)) continue;
                    if (Vector3.Distance(C.Enemies.Position(i), jumpPos) > jumpRadius) continue;
                    var dmg = C.Calculator.RollAmount(def.baseDamage * j.jumpDamageMultiplier, Stats.LevelMult(w), C.Enemies.IsElite(i), 0f,
                        def.procCoefficient * j.jumpProcMultiplier, def.weaponId);
                    C.DamageAndRecord(i, def.weaponId, dmg, false);
                }
            }

            QueueDelayedStrike(w, radius);
            return WeaponFireOutcome.Emitted;
        }

        void QueueDelayedStrike(WeaponSlot w, float radius)
        {
            var def = w.Config;
            var j = w.Evolution.jumpStrike;
            var cluster = C.Targeting.PredictClusterGround(def.range);
            C.Strikes.Add(new DelayedStrike
            {
                Active = true,
                Position = cluster,
                Timer = j.delayedSeconds,
                Radius = radius * j.delayedRadiusFactor,
                BaseDamage = def.baseDamage * j.delayedDamageMultiplier,
                DamageBonus = Stats.LevelMult(w),
                ProcCoefficient = def.procCoefficient * j.delayedProcMultiplier,
                WeaponId = def.weaponId,
            });
        }

        readonly HashSet<int> _chained = new HashSet<int>();

        /// <summary>
        /// R-19 fix: chain lightning. Strikes the target, then jumps to the nearest enemy not yet hit within chainRange,
        /// each jump dealing chainFalloff of the previous one (Tempest Mast adds jumps).
        /// </summary>
        void FireChain(WeaponSlot w, int first)
        {
            var def = w.Config;
            var a = def.areaStrike;
            var hits = a.chainTargets + a.chainTargetsPerQuantity * w.QuantityBonus;
            if (w.EvolutionEffect == Effect.JumpAndDelayedStrike) hits += w.Evolution.jumpStrike.extraChainTargets;
            _chained.Clear();
            var current = first;
            var damageScale = 1f;
            var strikeRadius = a.chainStrikeRadius * w.SizeMult * C.Mods.Size;
            for (var k = 0; k < hits && current >= 0; k++)
            {
                _chained.Add(current);
                var at = C.Enemies.Position(current);
                at.y = 0f;
                C.Effects?.LightningStrike(at, strikeRadius);
                var dmg = C.Calculator.RollAmount(def.baseDamage * damageScale, Stats.LevelMult(w), C.Enemies.IsElite(current), 0f, def.procCoefficient, def.weaponId);
                C.DamageAndRecord(current, def.weaponId, dmg, false);
                C.Damage.ApplyStatus(current, WeaponStatus.Shock, a.shockSeconds * w.DurationMult);
                damageScale *= a.chainFalloff;

                var next = -1;
                var best = a.chainRange;
                for (var i = 0; i < C.Enemies.Capacity; i++)
                {
                    if (!C.Enemies.IsActive(i) || _chained.Contains(i)) continue;
                    var d = Vector3.Distance(C.Enemies.Position(i), at);
                    if (d > best) continue;
                    best = d;
                    next = i;
                }
                current = next;
            }
        }

        /// <summary>V3 CaptainFireLightningSalvo's fallback strike (Roxie's active).</summary>
        public void FireSalvo(WeaponRulesConfig rules)
        {
            if (rules == null || !C.Enemies.TryGetNearest(C.Ship.Position, rules.salvoRange, out var target, false)) return;
            var strikePos = C.Enemies.Position(target);
            strikePos.y = 0f;
            var radius = rules.salvoRadius;
            C.Effects?.LightningStrike(strikePos, radius);
            for (var i = 0; i < C.Enemies.Capacity; i++)
            {
                if (!C.Enemies.IsActive(i)) continue;
                if (Vector3.Distance(C.Enemies.Position(i), strikePos) > radius) continue;
                // V3 passes 0 as the weapon level factor, so every salvo hit deals the 1-damage floor (register R-88).
                var dmg = C.Calculator.RollAmount(rules.salvoDamage, rules.salvoLevelFactor, C.Enemies.IsElite(i), 0f, rules.salvoProcCoefficient, rules.salvoLedgerId);
                C.Damage.ApplyDamage(i, dmg, false);
            }
        }
    }

    // ---- Aura (V3 FireAura) --------------------------------------------------------------------------------------------

    public sealed class AuraBehavior : WeaponBehavior
    {
        public AuraBehavior(WeaponCombat c) : base(c) { }

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            var def = w.Config;
            var a = def.aura;
            var radius = a.radiusBase * w.SizeMult * C.Mods.Size;
            for (var i = 0; i < C.Enemies.Capacity; i++)
            {
                if (!C.Enemies.IsActive(i)) continue;
                if (Vector3.Distance(C.Enemies.Position(i), C.Ship.Position) > radius) continue;
                var isElite = C.Enemies.IsElite(i);
                var dmg = C.Calculator.Roll(def.baseDamage * a.damageMultiplier, Stats.LevelMult(w), isElite, 0f, def.procCoefficient, def.weaponId);
                C.ApplyWeaponDamage(i, def.weaponId, dmg, isElite, C.Enemies.IsBoss(i));
                C.Damage.ApplyStatus(i, WeaponStatus.Curse, a.curseSeconds * w.DurationMult);
            }
            return WeaponFireOutcome.Emitted;
        }
    }

    // ---- Predicted area shells (V3 FireMortar; impacts in TickDelayedStrikes) -----------------------------------------

    public sealed class PredictedAreaBehavior : WeaponBehavior
    {
        public PredictedAreaBehavior(WeaponCombat c) : base(c) { }

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            if (C.Hooks.IsPvP) return WeaponFireOutcome.Emitted;
            var def = w.Config;
            var target = C.Targeting.PredictClusterGround(def.range);
            target.y = def.predictedArea.telegraphHeight;
            C.NotifyAimed(w, (target - C.Ship.Position).normalized);
            var radius = def.size * w.SizeMult * C.Mods.Size;
            // R-27 fix: a ring that lasts exactly the impact delay (V3's reticle showed 1.1 s for a 0.6 s shell).
            if (def.predictedArea.telegraphMatchesImpact) C.Effects?.TimedRing(target, radius, def.predictedArea.impactDelaySeconds);
            else C.Effects?.AreaTelegraph(target, radius);
            C.Strikes.Add(new DelayedStrike
            {
                Active = true,
                Position = target,
                Timer = def.predictedArea.impactDelaySeconds,
                Radius = def.size * w.SizeMult * C.Mods.Size,
                BaseDamage = def.baseDamage,
                DamageBonus = Stats.LevelMult(w),
                ProcCoefficient = def.procCoefficient,
                WeaponId = def.weaponId,
                AreaShell = true,
                ImpactDraws = def.predictedArea.v3ImpactEffectDraws,
            });
            return WeaponFireOutcome.Emitted;
        }
    }

    // ---- Boarding skiffs (V3 FireBoardingParty, TickBoardingSkiffs) ---------------------------------------------------

    public sealed class SummonBehavior : WeaponBehavior
    {
        public SummonBehavior(WeaponCombat c) : base(c) { }

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            var def = w.Config;
            var s = def.summon;
            if (!C.Targeting.TryResolveTarget(def, out var target, out var aim))
                return WeaponFireOutcome.NoTarget;
            C.NotifyAimed(w, aim, target);
            var back = -C.Ship.MovementForward;
            back.y = 0f;
            if (back.sqrMagnitude < 0.0001f) back = -aim;
            back.Normalize();
            var lateral = Vector3.Cross(Vector3.up, back).normalized;
            var side = C.Random.CombatFloat() >= 0.5f ? 1f : -1f;
            var spawn = C.Ship.Position + back * s.backOffset + lateral * side * s.lateralOffset;
            spawn.y = s.spawnDepth;
            C.Effects?.WaterSplash(spawn + Vector3.up * 0.35f);
            var view = C.Effects != null ? C.Effects.SkiffSpawned(spawn, aim) : -1;
            C.Skiffs.Add(new BoardingSkiff
            {
                Active = true,
                TargetIndex = target,
                Position = spawn,
                SpawnStart = spawn,
                SpawnAnimTime = s.spawnSeconds,
                SpawnAnimDuration = s.spawnSeconds,
                StrikeCooldown = s.firstStrikeDelay,
                LifeTime = s.lifetimeSeconds,
                BaseDamage = def.baseDamage,
                DamageBonus = Stats.LevelMult(w),
                ProcCoefficient = def.procCoefficient,
                SurfaceHeight = s.surfaceHeight,
                MoveSpeed = s.moveSpeed,
                StrikeRange = s.strikeRange,
                StrikeInterval = s.strikeInterval,
                WeaponId = def.weaponId,
                View = view,
            });
            return WeaponFireOutcome.Emitted;
        }

        public void TickSkiffs(float dt)
        {
            var list = C.Skiffs;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var skiff = list[i];
                if (!skiff.Active) { list.RemoveAt(i); continue; }

                skiff.LifeTime -= dt;
                if (skiff.LifeTime <= 0f || skiff.TargetIndex < 0 || !C.Enemies.IsActive(skiff.TargetIndex))
                {
                    if (skiff.View >= 0) C.Effects?.SkiffRemoved(skiff.View);
                    list.RemoveAt(i);
                    continue;
                }

                if (skiff.SpawnAnimTime > 0f)
                {
                    skiff.SpawnAnimTime -= dt;
                    var duration = Mathf.Max(0.01f, skiff.SpawnAnimDuration);
                    var t = 1f - Mathf.Clamp01(skiff.SpawnAnimTime / duration);
                    var eased = t * t * (3f - 2f * t);
                    var surface = skiff.SpawnStart;
                    surface.y = skiff.SurfaceHeight;
                    skiff.Position = Vector3.Lerp(skiff.SpawnStart, surface, eased);
                }

                var targetPos = C.Enemies.Position(skiff.TargetIndex);
                targetPos.y = 0f;
                var toTarget = targetPos - skiff.Position;
                toTarget.y = 0f;
                var dist = toTarget.magnitude;
                if (skiff.SpawnAnimTime <= 0f && dist > 0.05f)
                    skiff.Position += toTarget / dist * (skiff.MoveSpeed * dt);

                if (skiff.View >= 0)
                    C.Effects?.SkiffMoved(skiff.View, skiff.Position + Vector3.up * 0.2f, dist > 0.05f ? toTarget.normalized : Vector3.zero);

                skiff.StrikeCooldown -= dt;
                if (dist <= skiff.StrikeRange && skiff.StrikeCooldown <= 0f)
                {
                    skiff.StrikeCooldown = skiff.StrikeInterval;
                    var isElite = C.Enemies.IsElite(skiff.TargetIndex);
                    var dmg = C.Calculator.Roll(skiff.BaseDamage, skiff.DamageBonus, isElite, 0f, skiff.ProcCoefficient, skiff.WeaponId);
                    C.ApplyWeaponDamage(skiff.TargetIndex, skiff.WeaponId, dmg, isElite, C.Enemies.IsBoss(skiff.TargetIndex));
                }

                list[i] = skiff;
            }
        }
    }
}
