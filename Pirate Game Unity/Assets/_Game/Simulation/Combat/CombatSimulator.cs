using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using RoguePirates.Simulation.Combat;

namespace RoguePirates.Simulation
{
    public sealed class CombatSimulator
    {
        readonly PauseReasonSet _pause = new();
        readonly SimClock _clock = new();
        readonly LastRumSystem _lastRum = new();
        readonly WeaponSystem _weapon = new();
        readonly List<SimActor> _actors = new();
        readonly List<SimProjectile> _projectiles = new();
        readonly List<SimEvent> _events = new();
        readonly SimRng _mapRng;
        readonly SimRng _combatRng;
        readonly SimRng _lootRng;

        int _nextActorId = 1;
        int _nextProjectileId = 1;
        SimActor _player;
        bool _objectiveSecured;
        SimRunState _state = SimRunState.Playing;
        float _xp;
        int _level = 1;
        int _kills;

        public int Seed { get; }
        public IReadOnlyList<SimEvent> Events => _events;
        public SimRunState State => _state;
        public bool ObjectiveSecured => _objectiveSecured;
        public int Tick => _clock.Tick;
        public float SimSeconds => _clock.SimSeconds;
        public int Kills => _kills;
        public int Level => _level;

        public CombatSimulator(int seed)
        {
            Seed = seed;
            _mapRng = SimRng.CreateStream(seed, 73856093);
            _lootRng = SimRng.CreateStream(seed, 19349663);
            _combatRng = SimRng.CreateStream(seed, 50331653);

            _player = CreatePlayer();
            _actors.Add(_player);
            SpawnShark(new SimVec2(SimFixed.FromInt(8), SimFixed.FromInt(0)));
        }

        SimActor CreatePlayer()
        {
            return new SimActor
            {
                Id = _nextActorId++,
                IsPlayer = true,
                Position = SimVec2.Zero,
                Hull = SimFixed.FromFloat(SimConstants.DefaultHull),
                MaxHull = SimFixed.FromFloat(SimConstants.DefaultHull)
            };
        }

        void SpawnShark(SimVec2 pos)
        {
            _actors.Add(new SimActor
            {
                Id = _nextActorId++,
                Position = pos,
                Hull = SimFixed.FromFloat(SimConstants.SharkBaseHp),
                MaxHull = SimFixed.FromFloat(SimConstants.SharkBaseHp)
            });
        }

        void SpawnBoss()
        {
            _actors.Add(new SimActor
            {
                Id = _nextActorId++,
                IsBoss = true,
                Position = new SimVec2(SimFixed.FromInt(12), SimFixed.FromInt(4)),
                Hull = SimFixed.FromFloat(500f),
                MaxHull = SimFixed.FromFloat(500f)
            });
            _state = SimRunState.BossFight;
        }

        public void SetPause(SimPauseReason reason, bool active)
        {
            if (active) _pause.Add(reason);
            else _pause.Remove(reason);
        }

        public void SetMoveInput(float x, float y)
        {
            if (_pause.BlocksSimulation || _state == SimRunState.PlayerDead || _state == SimRunState.RunComplete)
                return;

            var v = new SimVec2(SimFixed.FromFloat(x), SimFixed.FromFloat(y)).Normalized();
            _player.Velocity = v.Mul(SimConstants.DefaultMoveSpeed);
        }

        public void Step(float wallDeltaSeconds)
        {
            _clock.AdvanceWall(wallDeltaSeconds);
            if (_pause.BlocksSimulation || _state == SimRunState.PlayerDead || _state == SimRunState.RunComplete)
                return;

            _lastRum.BeginTick();
            var dt = SimConstants.TickDurationSeconds;
            _clock.AdvanceTick(false);

            MoveActors(dt);
            _weapon.Tick(dt, _player, _projectiles, ref _nextProjectileId, _events, _clock.Tick);
            AdvanceProjectiles(dt);
            ResolveContacts(dt);
            TickEnemies(dt);

            if (_clock.Tick == 600)
                SpawnBoss();

            if (_state == SimRunState.RunComplete)
                return;

            if (_player.IsDead)
            {
                if (!_objectiveSecured)
                    _state = SimRunState.PlayerDead;
                else
                    _state = SimRunState.RunComplete;
            }
        }

        /// <summary>Advance wall time at a render rate; simulation ticks use fixed 30 Hz accumulation.</summary>
        public void StepRenderFrames(int renderFps, float wallSeconds)
        {
            var accumulator = 0f;
            var frameTime = 1f / renderFps;
            var frames = (int)(wallSeconds * renderFps);
            for (var i = 0; i < frames; i++)
            {
                accumulator += frameTime;
                while (accumulator >= SimConstants.TickDurationSeconds)
                {
                    accumulator -= SimConstants.TickDurationSeconds;
                    Step(SimConstants.TickDurationSeconds);
                }
            }
        }

        void MoveActors(float dt)
        {
            foreach (var a in _actors)
            {
                if (a.IsDead) continue;
                a.Position = a.Position.Add(a.Velocity.Mul(dt));
            }
        }

        void AdvanceProjectiles(float dt)
        {
            for (var i = _projectiles.Count - 1; i >= 0; i--)
            {
                var p = _projectiles[i];
                if (!p.Active) continue;
                p.Position = p.Position.Add(p.Velocity.Mul(dt));
                if (p.Position.X.ToFloat() > 40f || p.Position.X.ToFloat() < -40f ||
                    p.Position.Y.ToFloat() > 40f || p.Position.Y.ToFloat() < -40f)
                {
                    p.Active = false;
                    _projectiles.RemoveAt(i);
                }
            }
        }

        void ResolveContacts(float dt)
        {
            for (var pi = _projectiles.Count - 1; pi >= 0; pi--)
            {
                var p = _projectiles[pi];
                if (!p.Active) continue;
                foreach (var a in _actors)
                {
                    if (a.IsDead || a.Id == p.OwnerId) continue;
                    if (!CirclesOverlap(p.Position, p.Radius, a.Position, SimConstants.EnemyRadiusMeters)) continue;
                    ApplyDamage(a, p.Damage, p.OwnerId);
                    p.Active = false;
                    _projectiles.RemoveAt(pi);
                    break;
                }
            }
        }

        void TickEnemies(float dt)
        {
            foreach (var e in _actors)
            {
                if (e.IsDead || e.IsPlayer) continue;
                e.ContactCooldown = MathF.Max(0f, e.ContactCooldown - dt);
                var toPlayer = _player.Position.Sub(e.Position);
                if (toPlayer.LengthSq().Raw < SimFixed.FromFloat(0.01f).Raw) continue;
                var dir = toPlayer.Normalized();
                var speed = e.IsBoss ? 3.5f : SimConstants.SharkMoveSpeed;
                e.Velocity = dir.Mul(speed);
                e.Position = e.Position.Add(e.Velocity.Mul(dt));

                if (CirclesOverlap(e.Position, SimConstants.EnemyRadiusMeters, _player.Position, SimConstants.PlayerRadiusMeters))
                {
                    if (e.ContactCooldown <= 0f)
                    {
                        var dmg = SimFixed.FromFloat(e.IsBoss ? 20f : SimConstants.SharkBaseDamage);
                        ApplyDamage(_player, dmg, e.Id);
                        e.ContactCooldown = SimConstants.EnemyContactCooldownSeconds;
                    }
                }

                if (e.IsDead)
                {
                    _kills++;
                    GrantXp(e.IsBoss ? 120f : 8f);
                    if (e.IsBoss)
                        SecureObjective();
                }
            }
        }

        void ApplyDamage(SimActor target, SimFixed damage, int sourceId)
        {
            if (target.IsDead) return;
            target.Hull = target.Hull.Sub(damage);
            _events.Add(new SimEvent(SimEventKind.DamageDealt, _clock.Tick, sourceId, target.Id, damage));

            if (target.IsPlayer && target.Hull.Raw <= 0)
                _lastRum.TryPreventLethal(ref target.Hull);

            if (target.Hull.Raw <= 0)
            {
                target.Hull = SimFixed.FromInt(0);
                _events.Add(new SimEvent(SimEventKind.ActorDied, _clock.Tick, target.Id));
                if (target.IsBoss)
                    SecureObjective();
            }
        }

        void GrantXp(float amount)
        {
            _xp += amount;
            _events.Add(new SimEvent(SimEventKind.XpGained, _clock.Tick, _player.Id, value: SimFixed.FromFloat(amount)));
            var needed = SimMath.XPToNext(_level);
            while (_xp >= needed)
            {
                _xp -= needed;
                _level++;
                _events.Add(new SimEvent(SimEventKind.LevelUpOffer, _clock.Tick, _player.Id, value: SimFixed.FromInt(_level)));
                needed = SimMath.XPToNext(_level);
            }
        }

        public void SecureObjective()
        {
            if (_objectiveSecured) return;
            _objectiveSecured = true;
            _state = SimRunState.ObjectiveSecured;
            _events.Add(new SimEvent(SimEventKind.ObjectiveSecured, _clock.Tick, _player.Id));
        }

        /// <summary>Fixture: apply simultaneous lethal to player and boss on same tick.</summary>
        public void ApplySameTickLethalFixture()
        {
            var boss = _actors.Find(a => a.IsBoss);
            if (boss == null) return;
            var lethal = SimFixed.FromFloat(9999f);
            ApplyDamage(boss, lethal, _player.Id);
            ApplyDamage(_player, lethal, boss.Id);
        }

        public string ComputeStateHash()
        {
            var json = string.Join("|",
                Seed.ToString(),
                _clock.Tick.ToString(),
                _state.ToString(),
                _objectiveSecured.ToString(),
                _player.Hull.Raw.ToString(),
                _kills.ToString(),
                _level.ToString(),
                _actors.Count.ToString(),
                _mapRng.StateSnapshot.ToString(),
                _combatRng.StateSnapshot.ToString(),
                _lootRng.StateSnapshot.ToString());
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(json));
            return ToHex(bytes);
        }

        static string ToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (var i = 0; i < bytes.Length; i++)
                sb.Append(bytes[i].ToString("X2"));
            return sb.ToString();
        }

        static bool CirclesOverlap(SimVec2 a, float radiusA, SimVec2 b, float radiusB)
        {
            var dx = a.X.ToFloat() - b.X.ToFloat();
            var dy = a.Y.ToFloat() - b.Y.ToFloat();
            var r = radiusA + radiusB;
            return dx * dx + dy * dy <= r * r;
        }
    }
}
