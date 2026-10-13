using System.Collections.Generic;
using RoguePirates.V4.Config;
using UnityEngine;
using Stats = RoguePirates.V4.Weapons.WeaponStatResolver;

namespace RoguePirates.V4.Weapons
{
    /// <summary>One chained mace in flight (the simulation's; presentation draws the ball and its chain from it).</summary>
    public sealed class ChainedMace
    {
        public WeaponSlot Source;
        public string WeaponId;
        /// <summary>The ball, in world space.</summary>
        public Vector3 Position;
        /// <summary>Where it flies to: its target's position while the target lives, else where it last was.</summary>
        public Vector3 TargetPoint;
        public int TargetIndex = -1, TargetUnit = -1;
        /// <summary>Aimed this far beside its target (a second mace at the same enemy flies beside the first).</summary>
        public Vector3 AimOffset;
        public bool Returning;
        public float OutSeconds;
        /// <summary>Seconds it has been reeled in (a boosting ship can outrun it: it snaps home after <see cref="ChainedMaceBehavior.MaxReturnSeconds"/>).</summary>
        public float ReturnSeconds;
        public float BaseDamage, DamageBonus, ProcCoefficient, HitRadius, Knockback, OutSpeed, ReturnSpeed, Height, MaxOutSeconds;
        /// <summary>Unit ids this throw has hit (each enemy once a throw).</summary>
        public readonly List<int> HitUnits = new List<int>(8);
    }

    /// <summary>
    /// Hackathon (12 Oct, owner: "change the anchor flail from rotating to something like a chained cannonball that is
    /// thrown at an enemy starting from 1 per attack ... a mace that hits the enemy and comes back but has a chain
    /// attached. as it levels up, it can go to max 4 at once"). Each attack throws one mace per point of the weapon's
    /// quantity (base 1, +1 at ranks 4, 8 and 12; at most <see cref="MaceBlock.maxPerAttack"/>), each at a different
    /// enemy when there are enough (the nearest first). A mace flies out, hits every enemy it touches on the way (each
    /// once a throw, its target included), and is reeled back to the ship; the next attack waits until every mace of
    /// the last one is back. Deterministic: no random draws besides the damage rolls, enemies in slot order.
    /// </summary>
    public sealed class ChainedMaceBehavior : WeaponBehavior
    {
        const float SideStep = 0.9f;
        readonly List<int> _targets = new List<int>(4);
        readonly Stack<ChainedMace> _free = new Stack<ChainedMace>();

        /// <summary>13 Oct (review): maces made up front, 4 (the most one attack throws) for each of 6 weapon slots, so none
        /// is created mid-run.</summary>
        public const int PrewarmPerSlot = 4, PrewarmSlots = 6;

        /// <summary>13 Oct (review): a mace reeled in for longer than this snaps home (a boosting ship, 34 m/s, outruns the
        /// 28 m/s reel and would block the next throw for as long as it boosts).</summary>
        public static float MaxReturnSeconds(ChainedMace mace) => Mathf.Max(0.5f, mace.MaxOutSeconds * 1.5f);

        public ChainedMaceBehavior(WeaponCombat c) : base(c)
        {
            for (var i = 0; i < PrewarmPerSlot * PrewarmSlots; i++) _free.Push(new ChainedMace());
        }

        /// <summary>Maces ready for the next throws (tests).</summary>
        public int FreeCount => _free.Count;

        /// <summary>Maces one attack of this slot throws (1 at rank 1, up to the cap).</summary>
        public static int MacesPerAttack(WeaponSlot w)
        {
            if (w?.Config == null) return 1;
            return Mathf.Clamp(w.Config.baseQuantity + w.QuantityBonus, 1, Mathf.Max(1, w.Config.mace.maxPerAttack));
        }

        public bool HasOut(WeaponSlot w)
        {
            var list = C.Maces;
            for (var i = 0; i < list.Count; i++) if (ReferenceEquals(list[i].Source, w)) return true;
            return false;
        }

        /// <summary>A new throw only once the last one's maces are all back.</summary>
        public override bool CanFire(WeaponSlot w) => !HasOut(w);

        public override WeaponFireOutcome Fire(WeaponSlot w)
        {
            if (HasOut(w) || C.Ship == null || !C.Ship.Exists) return WeaponFireOutcome.NoTarget;
            var def = w.Config;
            if (!C.Targeting.TryResolveTarget(def, out var first, out var aim)) return WeaponFireOutcome.NoTarget;
            var count = MacesPerAttack(w);
            PickTargets(first, def.range * w.SizeMult * C.Mods.Size, count); // 13 Oct (review): the range the Size cards scale, as the ball
            C.NotifyAimed(w, aim, first);
            var m = def.mace;
            var origin = C.Ship.Position;
            origin.y = m.height;
            for (var k = 0; k < count; k++)
            {
                var target = _targets[k % _targets.Count]; // distinct while there are enough enemies
                // A mace sent at an enemy that already has one flies beside it (still within its reach), so each shows.
                var round = k / _targets.Count;
                var toTarget = C.Enemies.Position(target) - origin;
                toTarget.y = 0f;
                var side = toTarget.sqrMagnitude > 0.0001f ? Vector3.Cross(Vector3.up, toTarget.normalized) : Vector3.right;
                var offset = round == 0 ? Vector3.zero : side * (round % 2 == 1 ? SideStep : -SideStep) * ((round + 1) / 2);
                var mace = _free.Count > 0 ? _free.Pop() : new ChainedMace();
                mace.Source = w;
                mace.WeaponId = def.weaponId;
                mace.Position = origin;
                mace.TargetIndex = target;
                mace.TargetUnit = C.Enemies.UnitId(target);
                mace.AimOffset = offset;
                mace.TargetPoint = C.Enemies.Position(target) + offset;
                mace.TargetPoint.y = m.height;
                mace.Returning = false;
                mace.OutSeconds = 0f;
                mace.ReturnSeconds = 0f;
                mace.BaseDamage = def.baseDamage * m.damageMultiplier;
                mace.DamageBonus = Stats.LevelMult(w);
                mace.ProcCoefficient = def.procCoefficient;
                mace.HitRadius = m.hitRadius * w.SizeMult * C.Mods.Size;
                mace.Knockback = m.knockbackMeters * w.KnockbackMult;
                mace.OutSpeed = m.outSpeed * w.SpeedMult * C.Mods.ProjectileSpeed;
                mace.ReturnSpeed = m.returnSpeed * w.SpeedMult * C.Mods.ProjectileSpeed;
                mace.Height = m.height;
                mace.MaxOutSeconds = m.maxOutSeconds;
                mace.HitUnits.Clear();
                C.Maces.Add(mace);
            }
            C.Hooks.RecordConcurrentTargets(def.weaponId, Mathf.Min(count, _targets.Count));
            return WeaponFireOutcome.Emitted;
        }

        /// <summary>The resolved target, then the nearest other enemies in range (slot order breaks ties).</summary>
        void PickTargets(int first, float range, int count)
        {
            _targets.Clear();
            _targets.Add(first);
            var origin = C.Ship.Position;
            while (_targets.Count < count)
            {
                var best = -1;
                var bestDist = float.MaxValue;
                for (var i = 0; i < C.Enemies.Capacity; i++)
                {
                    if (!C.Enemies.IsActive(i) || _targets.Contains(i)) continue;
                    var d = WeaponCombat.HorizontalDistance(origin, C.Enemies.Position(i));
                    if (d > range + C.Enemies.Radius(i) || d >= bestDist) continue;
                    bestDist = d;
                    best = i;
                }
                if (best < 0) break;
                _targets.Add(best);
            }
        }

        /// <summary>Moves every mace, hits what each touches, reels them in and drops the ones that are back.</summary>
        public void TickMaces(float dt)
        {
            var list = C.Maces;
            if (list.Count == 0) return;
            var shipAlive = C.Ship != null && C.Ship.Exists;
            for (var i = list.Count - 1; i >= 0; i--)
            {
                var mace = list[i];
                if (!shipAlive) { Recycle(i); continue; }
                var home = C.Ship.Position;
                home.y = mace.Height;
                if (!mace.Returning)
                {
                    mace.OutSeconds += dt;
                    if (mace.TargetIndex >= 0 && mace.TargetIndex < C.Enemies.Capacity && C.Enemies.IsActive(mace.TargetIndex)
                        && C.Enemies.UnitId(mace.TargetIndex) == mace.TargetUnit)
                    {
                        mace.TargetPoint = C.Enemies.Position(mace.TargetIndex) + mace.AimOffset;
                        mace.TargetPoint.y = mace.Height;
                    }
                    else mace.TargetIndex = -1; // gone: fly on to where it was
                    mace.Position = Vector3.MoveTowards(mace.Position, mace.TargetPoint, mace.OutSpeed * dt);
                    HitTouching(mace);
                    if (WeaponCombat.HorizontalDistance(mace.Position, mace.TargetPoint) <= 0.05f || mace.OutSeconds >= mace.MaxOutSeconds)
                    {
                        mace.Returning = true;
                        C.Effects?.WaterSplash(mace.Position);
                    }
                }
                else
                {
                    mace.ReturnSeconds += dt;
                    mace.Position = Vector3.MoveTowards(mace.Position, home, mace.ReturnSpeed * dt);
                    HitTouching(mace);
                    if (WeaponCombat.HorizontalDistance(mace.Position, home) <= 0.6f || mace.ReturnSeconds >= MaxReturnSeconds(mace)) Recycle(i);
                }
            }
        }

        void HitTouching(ChainedMace mace)
        {
            for (var e = 0; e < C.Enemies.Capacity; e++)
            {
                if (!C.Enemies.IsActive(e)) continue;
                var reach = mace.HitRadius + C.Enemies.Radius(e);
                if (!WeaponCombat.WithinHorizontalRadius(mace.Position, C.Enemies.Position(e), reach)) continue;
                var unit = C.Enemies.UnitId(e);
                if (mace.HitUnits.Contains(unit)) continue;
                mace.HitUnits.Add(unit);
                var dmg = C.Calculator.RollAmount(mace.BaseDamage, mace.DamageBonus, C.Enemies.IsEliteDamageTarget(e), 0f, mace.ProcCoefficient, mace.WeaponId);
                C.DamageAndRecord(e, mace.WeaponId, dmg, false);
                if (mace.Knockback > 0f && C.Ship != null && C.Ship.Exists) C.Damage.Knockback(e, C.Ship.Position, mace.Knockback);
                if (e == mace.TargetIndex && !mace.Returning)
                {
                    // The target is struck: the mace turns back at once.
                    mace.Returning = true;
                    C.Effects?.WaterSplash(mace.Position);
                }
            }
        }

        /// <summary>13 Oct (review): every mace in flight back to the free stack (the run clears what is in the air).</summary>
        public void RecycleAll()
        {
            for (var i = C.Maces.Count - 1; i >= 0; i--) Recycle(i);
        }

        void Recycle(int index)
        {
            var mace = C.Maces[index];
            C.Maces.RemoveAt(index);
            mace.Source = null;
            mace.HitUnits.Clear();
            _free.Push(mace);
        }
    }
}
