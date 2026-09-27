using System.Collections.Generic;
using HighSeas.Core;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>What the Hellfire patches need from the run (V3 read RunManager, the evolution catalog and the juice controller).</summary>
    public interface IHellfireEnvironment
    {
        /// <summary>The run exists, is not paused and has not ended.</summary>
        bool RunActive { get; }
        /// <summary>A patch tick's damage (V3: DamageSystem.RollDamage with base 4 and the Flamethrower's weapon id, on the combat stream).</summary>
        float RollTickDamage(bool targetIsElite);
        /// <summary>The weapon carries the Hellfire Nozzle evolution.</summary>
        bool IsHellfireWeapon(string weaponId);
        /// <summary>The burning-oil effect (presentation).</summary>
        void OilDropped(Vector3 position, float radius);
    }

    /// <summary>
    /// Hellfire Nozzle flame patches (port of HellfirePatchWorld): up to 8 patches of 2.4 s that burn enemies within
    /// 2.1 m every 0.35 s. Ticked where V3's component updated (a frame slot added where V3 ensured it).
    /// </summary>
    public sealed class HellfirePatches
    {
        const int MaxPatches = 8;
        const float PatchLifetime = 2.4f;
        const float PatchRadius = 2.1f;
        const float TickInterval = 0.35f;

        struct Patch
        {
            public bool active;
            public Vector3 position;
            public float life;
            public float tickTimer;
        }

        readonly Patch[] _patches = new Patch[MaxPatches];
        readonly EnemySimulation _enemies;
        readonly EnemyStatuses _statuses;
        readonly IHellfireEnvironment _env;

        public HellfirePatches(EnemySimulation enemies, EnemyStatuses statuses, IHellfireEnvironment env)
        {
            _enemies = enemies;
            _statuses = statuses;
            _env = env;
        }

        public int ActiveCount
        {
            get
            {
                var n = 0;
                for (var i = 0; i < _patches.Length; i++) if (_patches[i].active) n++;
                return n;
            }
        }

        public void TrySpawnOnDirectFlameKill(Vector3 position, string weaponId)
        {
            if (!_env.IsHellfireWeapon(weaponId)) return;
            AddPatch(position);
        }

        void AddPatch(Vector3 position)
        {
            position.y = 0.2f;
            var slot = -1;
            for (var i = 0; i < _patches.Length; i++)
            {
                if (!_patches[i].active) { slot = i; break; }
            }

            if (slot < 0)
            {
                var oldest = 0;
                var oldestLife = float.MaxValue;
                for (var i = 0; i < _patches.Length; i++)
                {
                    if (!_patches[i].active || _patches[i].life >= oldestLife) continue;
                    oldestLife = _patches[i].life;
                    oldest = i;
                }
                slot = oldest;
            }

            _patches[slot] = new Patch
            {
                active = true,
                position = position,
                life = PatchLifetime,
                tickTimer = 0f
            };

            _env.OilDropped(position, PatchRadius * 0.85f);
        }

        /// <param name="dt">V3 used Time.deltaTime in its own Update.</param>
        public void Tick(float dt)
        {
            if (!_env.RunActive) return;
            for (var i = 0; i < _patches.Length; i++)
            {
                ref var patch = ref _patches[i];
                if (!patch.active) continue;
                patch.life -= dt;
                if (patch.life <= 0f)
                {
                    patch.active = false;
                    continue;
                }

                patch.tickTimer -= dt;
                if (patch.tickTimer > 0f) continue;
                patch.tickTimer = TickInterval;

                var agents = _enemies.Agents;
                for (var ai = 0; ai < agents.Length; ai++)
                {
                    if (!agents[ai].active) continue;
                    if (Vector3.Distance(agents[ai].position, patch.position) > PatchRadius) continue;
                    var dmg = _env.RollTickDamage(agents[ai].isElite);
                    _enemies.ApplyDamage(ai, dmg, false, showHitFeedback: false);
                    _statuses.ApplyStatus(ai, StatusType.Burn, 0.45f);
                }
            }
        }
    }

    /// <summary>What explosive barrels need from the run (V3 read the stage profile, RunManager and the toast HUD).</summary>
    public interface IBarrelEnvironment
    {
        /// <summary>V3's gate: the stage profile is active, the run exists and is not paused or ended.</summary>
        bool RunActive { get; }
        float FuseSeconds { get; }
        float Radius { get; }
        int Damage { get; }
        /// <summary>V3 SaltwindExplosiveBarrelLogic.ShouldDetonate.</summary>
        bool ShouldDetonate(float fuse);
        void Toast(string text, float seconds);
    }

    /// <summary>
    /// Explosive barrels (port of V3ExplosiveBarrelWorld's update; only placed by the v3-parity preset, the adopted
    /// decision removed them): sailing within 2.5 m arms a barrel, its fuse burns, the blast hurts the ship and every
    /// enemy within the radius. The barrel markers stay V3's (made when V3 made them: they bob with Unity's global
    /// random state).
    /// </summary>
    public sealed class ExplosiveBarrels
    {
        sealed class BarrelState
        {
            public Vector3 Position;
            public bool Consumed;
            public float Fuse;
            public bool Armed;
        }

        readonly List<BarrelState> _barrels = new List<BarrelState>();
        readonly EnemySimulation _enemies;
        readonly IEnemyTarget _player;
        readonly IBarrelEnvironment _env;

        public ExplosiveBarrels(EnemySimulation enemies, IEnemyTarget player, IBarrelEnvironment env)
        {
            _enemies = enemies;
            _player = player;
            _env = env;
        }

        public int Count => _barrels.Count;

        public void ResetForLayout(IReadOnlyList<Vector3> positions)
        {
            _barrels.Clear();
            if (positions == null) return;
            var fuse = _env.FuseSeconds;
            for (var i = 0; i < positions.Count; i++)
                _barrels.Add(new BarrelState { Position = positions[i], Fuse = fuse });
        }

        /// <param name="dt">V3 used Time.deltaTime in its own Update.</param>
        public void Tick(float dt)
        {
            if (!_env.RunActive) return;
            var player = _player != null && _player.Exists ? _player : null;
            var radius = _env.Radius;
            var damage = _env.Damage;
            for (var i = 0; i < _barrels.Count; i++)
            {
                var barrel = _barrels[i];
                if (barrel.Consumed) continue;

                if (!barrel.Armed)
                {
                    if (player != null && Vector3.Distance(player.Position, barrel.Position) <= 2.5f)
                        barrel.Armed = true;
                    continue;
                }

                barrel.Fuse -= dt;
                if (!_env.ShouldDetonate(barrel.Fuse))
                    continue;

                Detonate(barrel, player, radius, damage);
                barrel.Consumed = true;
            }
        }

        void Detonate(BarrelState barrel, IEnemyTarget player, float radius, int damage)
        {
            if (player != null && Vector3.Distance(player.Position, barrel.Position) <= radius && player.HasDamageReceiver)
                player.TakeDamage(damage, barrel.Position, false);

            var agents = _enemies.Agents;
            for (var i = 0; i < agents.Length; i++)
            {
                if (!agents[i].active) continue;
                if (Vector3.Distance(agents[i].position, barrel.Position) <= radius)
                    _enemies.ApplyDamage(i, damage, false, false);
            }

            _env.Toast("Powder barrel detonation!", 1.5f);
        }
    }
}
