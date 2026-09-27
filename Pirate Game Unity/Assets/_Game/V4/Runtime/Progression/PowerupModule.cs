using System;
using HighSeas.Core;
using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Progression
{
    /// <summary>The powerup draws (V3 RunRng Powerup stream).</summary>
    public interface IPowerupRandom
    {
        float PowerupFloat();
    }

    /// <summary>What powerups read from and do to the run and the ship.</summary>
    public interface IPowerupWorld
    {
        /// <summary>Inside the play bounds by <paramref name="margin"/> and clear of islands by <paramref name="clearance"/>.</summary>
        bool IsFreeSpot(Vector3 position, float margin, float clearance);
        bool IsBossFight { get; }
        bool ShipAlive { get; }
        float ShipHull { get; }
        float ShipMaxHull { get; }
        void HealShip(float amount);
        /// <summary>Re-read the run's move speed (V3 SyncRunSpeedMultiplier).</summary>
        void SyncShipSpeed();
    }

    /// <summary>Powerup models on the water (V3 V3PlaceholderFactory.CreateFloorPowerup; destroyed on despawn).</summary>
    public interface IPowerupViews
    {
        object Create(PowerupKind kind, Vector3 position);
        void Move(object view, Vector3 position);
        void Destroy(object view);
    }

    /// <summary>
    /// Floor powerups (M9 step 9.4): port of V3's V3FloorPowerupWorld — the drop roll on kills (Powerup stream), the spot
    /// search, lifetime and bobbing, collection, and each effect: Repair Supplies, Battle Signal and Powder Rush (one
    /// damage slot, R-43), Tailwind and Full Sail (one speed slot), Salvage Chime and Magnetic Tide (the pickups), Dead
    /// Calm (the V4 spawn rate). Reads <see cref="PowerupConfig"/>.
    /// </summary>
    public sealed class PowerupModule
    {
        const string DamageSlot = "powerup:damage", SpeedSlot = "powerup:speed", RushSlot = "powerup:powder_rush";

        struct Agent
        {
            public bool Active;
            public PowerupKind Kind;
            public Vector3 Position;
            public float Lifetime;
            public object View;
        }

        readonly Func<PowerupConfig> _config;
        readonly IPowerupRandom _rng;
        readonly IStatModifierSink _stats;
        readonly IPowerupWorld _world;
        readonly IPowerupViews _views;
        Agent[] _agents = new Agent[0];
        int _spawnedThisStage;
        float _dropCooldown, _bobTime;
        float _battleSignalTimer, _tailwindTimer, _powderRushTimer, _deadCalmTimer;
        bool _damageBuffActive, _speedBuffActive, _powderRushActive, _rushSlotActive;

        /// <summary>A powerup dropped (its toast).</summary>
        public event Action<PowerupKind> Dropped;
        /// <summary>A powerup was collected (its toast).</summary>
        public event Action<PowerupKind> Collected;
        /// <summary>Salvage Chime: loose XP homes in.</summary>
        public event Action AttractXp;
        /// <summary>Magnetic Tide: loose XP and gold (and barrels) home in.</summary>
        public event Action AttractLooseRewards;

        public PowerupModule(Func<PowerupConfig> config, IPowerupRandom rng, IStatModifierSink stats, IPowerupWorld world, IPowerupViews views)
        {
            _config = config;
            _rng = rng;
            _stats = stats;
            _world = world;
            _views = views;
        }

        PowerupConfig C => _config();

        /// <summary>Dead Calm's cut to the enemy spawn rate (V3 wrote it to the legacy spawner, clamped at 0.1).</summary>
        public float SpawnRateMultiplier { get; private set; } = 1f;
        public int SpawnedThisStage => _spawnedThisStage;
        public int ActiveCount { get { var n = 0; foreach (var a in _agents) if (a.Active) n++; return n; } }

        /// <summary>V3 ResetForRun (and the buffs cleared).</summary>
        public void ResetForRun()
        {
            var c = C;
            if (_agents.Length != c.maxPerStage)
            {
                for (var i = 0; i < _agents.Length; i++) Despawn(i);
                _agents = new Agent[c.maxPerStage];
            }
            for (var i = 0; i < _agents.Length; i++) Despawn(i);
            _spawnedThisStage = 0;
            _dropCooldown = 0f;
            _powderRushTimer = 0f;
            _deadCalmTimer = 0f;
            _powderRushActive = false;
            ClearBuffs();
        }

        /// <summary>V3 TryRollOnKill.</summary>
        public void TryRollOnKill(Vector3 position, bool isBoss, bool isElite)
        {
            var c = C;
            if (isBoss || _spawnedThisStage >= c.maxPerStage) return;
            var forced = isElite && c.elitesAlwaysDrop;
            if (!forced)
            {
                if (_dropCooldown > 0f) return;
                if (_rng.PowerupFloat() >= c.dropChancePerKill) return;
            }
            var kind = RollKind();
            if (!TrySpawn(kind, position)) return;
            _dropCooldown = c.minIntervalSeconds;
            Dropped?.Invoke(kind);
        }

        /// <summary>V3 V3PowerupCatalog.RollSaltwindKind generalised to the pool: one Powerup draw, entries in order.</summary>
        PowerupKind RollKind()
        {
            var pool = C.pool;
            var total = 0f;
            foreach (var e in pool) total += e.weight;
            var roll = _rng.PowerupFloat() * total;
            var acc = 0f;
            foreach (var e in pool)
            {
                acc += e.weight;
                if (roll < acc) return e.kind;
            }
            return pool[pool.Length - 1].kind;
        }

        bool TrySpawn(PowerupKind kind, Vector3 origin)
        {
            var slot = -1;
            for (var i = 0; i < _agents.Length; i++) if (!_agents[i].Active) { slot = i; break; }
            if (slot < 0 || !TryFindSpot(origin, out var spot)) return false;
            _agents[slot] = new Agent { Active = true, Kind = kind, Position = spot, Lifetime = C.lifetimeSeconds, View = _views?.Create(kind, spot) };
            _spawnedThisStage++;
            return true;
        }

        bool TryFindSpot(Vector3 origin, out Vector3 position)
        {
            var c = C;
            position = origin;
            position.y = 0.35f;
            if (_world.IsFreeSpot(position, c.boundsMargin, c.islandClearance)) return true;
            for (var ring = 1; ring <= c.searchRings; ring++)
            {
                for (var i = 0; i < c.searchPointsPerRing; i++)
                {
                    var angle = (i / (float)c.searchPointsPerRing) * Mathf.PI * 2f;
                    var candidate = origin + new Vector3(Mathf.Cos(angle) * ring * c.searchRingSpacing, 0f, Mathf.Sin(angle) * ring * c.searchRingSpacing);
                    candidate.y = 0.35f;
                    if (!_world.IsFreeSpot(candidate, c.boundsMargin, c.islandClearance)) continue;
                    position = candidate;
                    return true;
                }
            }
            return false;
        }

        /// <summary>V3 Tick (skipped while paused or after the run ends; the caller checks).</summary>
        public void Tick(float dt, Vector3 playerPos)
        {
            var c = C;
            if (_dropCooldown > 0f) _dropCooldown -= dt;
            _bobTime += dt;
            TickBuffs(dt);
            TickPowderRush(dt);
            TickDeadCalm(dt);
            for (var i = 0; i < _agents.Length; i++)
            {
                if (!_agents[i].Active) continue;
                _agents[i].Lifetime -= dt;
                if (_agents[i].Lifetime <= 0f)
                {
                    Despawn(i);
                    continue;
                }
                var bob = Mathf.Sin(_bobTime * 4f + i) * 0.1f;
                if (_agents[i].View != null) _views.Move(_agents[i].View, _agents[i].Position + Vector3.up * bob);
                var flat = playerPos - _agents[i].Position;
                flat.y = 0f;
                if (flat.sqrMagnitude > c.collectRadius * c.collectRadius) continue;
                var kind = _agents[i].Kind;
                Apply(kind);
                Despawn(i);
            }
        }

        void Apply(PowerupKind kind)
        {
            var c = C;
            switch (kind)
            {
                case PowerupKind.RepairSupplies:
                    if (_world.ShipAlive) _world.HealShip(Mathf.Min(_world.ShipMaxHull * c.repairMaxHullFraction, _world.ShipMaxHull - _world.ShipHull));
                    break;
                case PowerupKind.BattleSignal:
                    _battleSignalTimer = c.battleSignalSeconds;
                    ApplyDamageBuff(c.battleSignalDamageMultiplier);
                    break;
                case PowerupKind.Tailwind:
                    _tailwindTimer = c.tailwindSeconds;
                    ApplySpeedBuff(c.tailwindSpeedMultiplier);
                    break;
                case PowerupKind.SalvageChime:
                    AttractXp?.Invoke();
                    break;
                case PowerupKind.FullSail:
                    _tailwindTimer = c.fullSailSeconds;
                    ApplySpeedBuff(c.fullSailSpeedMultiplier);
                    break;
                case PowerupKind.PowderRush:
                    _powderRushTimer = c.powderRushSeconds;
                    _powderRushActive = true;
                    if (c.powderRushIsDamageInBattleSignalSlot) ApplyDamageBuff(1f + c.powderRushBonus);
                    else
                    {
                        _stats.RemoveSource(RushSlot);
                        _stats.Add(RushSlot, (int)StatId.AttackSpeed, 0f, 1f + c.powderRushBonus);
                        _rushSlotActive = true;
                    }
                    break;
                case PowerupKind.MagneticTide:
                    AttractLooseRewards?.Invoke();
                    break;
                case PowerupKind.DeadCalm:
                    if (!_world.IsBossFight)
                    {
                        _deadCalmTimer = c.deadCalmSeconds;
                        SpawnRateMultiplier = Mathf.Max(0.1f, 1f - c.deadCalmSlowFraction);
                    }
                    break;
            }
            Collected?.Invoke(kind);
        }

        void TickBuffs(float dt)
        {
            if (_battleSignalTimer > 0f)
            {
                _battleSignalTimer -= dt;
                if (_battleSignalTimer <= 0f) RemoveDamageBuff();
            }
            if (_tailwindTimer > 0f)
            {
                _tailwindTimer -= dt;
                if (_tailwindTimer <= 0f) RemoveSpeedBuff();
            }
        }

        void TickPowderRush(float dt)
        {
            if (!_powderRushActive) return;
            _powderRushTimer -= dt;
            if (_powderRushTimer > 0f) return;
            _powderRushActive = false;
            if (_rushSlotActive) { _stats.RemoveSource(RushSlot); _rushSlotActive = false; }
            else RemoveDamageBuff(); // V3: expiring Powder Rush also ends a Battle Signal in the shared slot
        }

        void TickDeadCalm(float dt)
        {
            if (_deadCalmTimer <= 0f) return;
            _deadCalmTimer -= dt;
            if (_deadCalmTimer <= 0f) SpawnRateMultiplier = 1f;
        }

        void ApplyDamageBuff(float multiplier)
        {
            RemoveDamageBuff();
            _stats.Add(DamageSlot, (int)StatId.Damage, 0f, multiplier);
            _damageBuffActive = true;
        }

        void RemoveDamageBuff()
        {
            if (!_damageBuffActive) return;
            _stats.RemoveSource(DamageSlot);
            _damageBuffActive = false;
        }

        void ApplySpeedBuff(float multiplier)
        {
            RemoveSpeedBuff();
            _stats.Add(SpeedSlot, (int)StatId.MoveSpeed, 0f, multiplier);
            _speedBuffActive = true;
            _world.SyncShipSpeed();
        }

        void RemoveSpeedBuff()
        {
            if (!_speedBuffActive) return;
            _stats.RemoveSource(SpeedSlot);
            _speedBuffActive = false;
            _world.SyncShipSpeed();
        }

        void ClearBuffs()
        {
            _battleSignalTimer = 0f;
            _tailwindTimer = 0f;
            _powderRushTimer = 0f;
            _powderRushActive = false;
            _deadCalmTimer = 0f;
            RemoveDamageBuff();
            RemoveSpeedBuff();
            if (_rushSlotActive) { _stats.RemoveSource(RushSlot); _rushSlotActive = false; }
            SpawnRateMultiplier = 1f;
        }

        void Despawn(int index)
        {
            if (index < 0 || index >= _agents.Length || !_agents[index].Active) return;
            if (_agents[index].View != null) _views?.Destroy(_agents[index].View);
            _agents[index] = default;
        }
    }
}
