using System;
using System.Collections.Generic;
using RoguePirates.Simulation.Balance;

namespace RoguePirates.Simulation.Combat
{
    public sealed class SimActor
    {
        public int Id;
        public bool IsPlayer;
        public bool IsBoss;
        public string ArchetypeId;
        public BalanceEnemyBehavior Behavior;
        public float BaseDamage;
        public float BaseMoveSpeed;
        public float AttackCooldownSeconds;
        public float PreferredRange;
        public int XpValue;
        public int GoldValue;
        public SimVec2 Position;
        public SimVec2 Velocity;
        public SimFixed Hull;
        public SimFixed MaxHull;
        public float ContactCooldown;
        public bool DeathReported;
        public bool IsDead => Hull.Raw <= 0;
    }

    public sealed class SimProjectile
    {
        public int Id;
        public int OwnerId;
        public SimVec2 Position;
        public SimVec2 Velocity;
        public SimFixed Damage;
        public float Radius;
        public bool Active = true;
    }

    public sealed class WeaponSystem
    {
        float _cooldown;
        float _damageMultiplier = 1f;
        float _cooldownMultiplier = 1f;
        float _projectileSpeedMultiplier = 1f;
        float _projectileRadiusMultiplier = 1f;
        float _baseDamage = SimConstants.DeckCannonBaseDamage;
        float _baseCooldown = SimConstants.DeckCannonCooldownSeconds;
        float _range = 22f;
        float _baseProjectileSpeed = SimConstants.ProjectileSpeed;

        public void SetDamageMultiplier(float multiplier) =>
            _damageMultiplier = MathF.Max(0.1f, multiplier);

        /// <summary>Approximates a pattern's simultaneous targets/AOE in the deterministic lab.</summary>
        public void SetPatternTargetMultiplier(float multiplier) =>
            _damageMultiplier *= MathF.Max(0.1f, multiplier);

        public void MultiplyDamage(float multiplier) =>
            _damageMultiplier *= MathF.Max(0.1f, multiplier);

        public void ApplyLabTuning(float cooldownMultiplier, float projectileSpeedMultiplier, float projectileRadiusMultiplier)
        {
            _cooldownMultiplier = MathF.Max(0.01f, cooldownMultiplier);
            _projectileSpeedMultiplier = MathF.Max(0.01f, projectileSpeedMultiplier);
            _projectileRadiusMultiplier = MathF.Max(0.01f, projectileRadiusMultiplier);
        }

        public void SetAuthoredWeapon(float damage, float cooldown, float range, float projectileSpeed)
        {
            _baseDamage = MathF.Max(0.01f, damage);
            _baseCooldown = MathF.Max(0.01f, cooldown);
            _range = MathF.Max(0.01f, range);
            _baseProjectileSpeed = MathF.Max(0.01f, projectileSpeed);
        }

        public void Tick(float dt, SimActor player, List<SimActor> actors, List<SimProjectile> projectiles, ref int nextProjectileId, List<SimEvent> events, int tick)
        {
            if (player.IsDead) return;
            _cooldown -= dt;
            if (_cooldown > 0f) return;
            _cooldown = _baseCooldown * _cooldownMultiplier;

            var dir = FindAutoTargetDirection(player, actors);
            if (dir.LengthSq().Raw < SimFixed.FromFloat(0.01f).Raw)
            {
                dir = player.Velocity;
                if (dir.LengthSq().Raw < SimFixed.FromFloat(0.01f).Raw)
                    dir = new SimVec2(SimFixed.FromInt(1), SimFixed.FromInt(0));
                else
                    dir = dir.Normalized();
            }

            var speed = SimFixed.FromFloat(_baseProjectileSpeed * _projectileSpeedMultiplier);
            var damage = SimMath.ComputeDamage(
                _baseDamage, 1f, 0f, _damageMultiplier, false, 1.5f, false, 1f, 1f, 0f);
            var target = FindClosestTarget(player, actors);
            if (target != null && target.Position.Sub(player.Position).LengthSq().Raw <= SimFixed.FromFloat(_range * _range).Raw)
            {
                target.Hull = target.Hull.Sub(damage);
                events.Add(new SimEvent(SimEventKind.DamageDealt, tick, player.Id, target.Id, damage));
                if (target.Hull.Raw <= 0)
                {
                    target.Hull = SimFixed.FromInt(0);
                    events.Add(new SimEvent(SimEventKind.ActorDied, tick, target.Id));
                }
                return;
            }
            var vel = new SimVec2(
                SimFixed.FromFloat(dir.X.ToFloat() * speed.ToFloat()),
                SimFixed.FromFloat(dir.Y.ToFloat() * speed.ToFloat()));

            var proj = new SimProjectile
            {
                Id = nextProjectileId++,
                OwnerId = player.Id,
                Position = player.Position,
                Velocity = vel,
                Damage = damage,
                Radius = SimConstants.ProjectileRadiusMeters * _projectileRadiusMultiplier
            };
            projectiles.Add(proj);
            events.Add(new SimEvent(SimEventKind.ProjectileSpawned, tick, player.Id, proj.Id));
        }

        static SimVec2 FindAutoTargetDirection(SimActor player, List<SimActor> actors)
        {
            var target = FindClosestTarget(player, actors);
            return target == null ? SimVec2.Zero : target.Position.Sub(player.Position).Normalized();
        }

        static SimActor FindClosestTarget(SimActor player, List<SimActor> actors)
        {
            SimActor target = null;
            var bestDistance = long.MaxValue;
            for (var i = 0; i < actors.Count; i++)
            {
                var candidate = actors[i];
                if (candidate.IsPlayer || candidate.IsDead) continue;
                var distance = candidate.Position.Sub(player.Position).LengthSq().Raw;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                target = candidate;
            }

            return target;
        }
    }
}
