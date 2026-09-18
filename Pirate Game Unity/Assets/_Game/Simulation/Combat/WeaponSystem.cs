using System;
using System.Collections.Generic;

namespace RoguePirates.Simulation.Combat
{
    public sealed class SimActor
    {
        public int Id;
        public bool IsPlayer;
        public bool IsBoss;
        public SimVec2 Position;
        public SimVec2 Velocity;
        public SimFixed Hull;
        public SimFixed MaxHull;
        public float ContactCooldown;
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

        public void SetDamageMultiplier(float multiplier) =>
            _damageMultiplier = MathF.Max(0.1f, multiplier);

        public void Tick(float dt, SimActor player, List<SimProjectile> projectiles, ref int nextProjectileId, List<SimEvent> events, int tick)
        {
            if (player.IsDead) return;
            _cooldown -= dt;
            if (_cooldown > 0f) return;
            _cooldown = SimConstants.DeckCannonCooldownSeconds;

            var dir = player.Velocity;
            if (dir.LengthSq().Raw < SimFixed.FromFloat(0.01f).Raw)
                dir = new SimVec2(SimFixed.FromInt(1), SimFixed.FromInt(0));
            else
                dir = dir.Normalized();

            var speed = SimFixed.FromFloat(SimConstants.ProjectileSpeed);
            var vel = new SimVec2(
                SimFixed.FromFloat(dir.X.ToFloat() * speed.ToFloat()),
                SimFixed.FromFloat(dir.Y.ToFloat() * speed.ToFloat()));

            var proj = new SimProjectile
            {
                Id = nextProjectileId++,
                OwnerId = player.Id,
                Position = player.Position,
                Velocity = vel,
                Damage = SimMath.ComputeDamage(
                    SimConstants.DeckCannonBaseDamage, 1f, 0f, _damageMultiplier, false, 1.5f, false, 1f, 1f, 0f),
                Radius = SimConstants.ProjectileRadiusMeters
            };
            projectiles.Add(proj);
            events.Add(new SimEvent(SimEventKind.ProjectileSpawned, tick, player.Id, proj.Id));
        }
    }
}
