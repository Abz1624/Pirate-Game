using System;
using System.Collections.Generic;
using HighSeas.Core;
using RoguePirates.V4.Config;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RoguePirates.V4.Progression
{
    /// <summary>Pickup models (V3's PickupViewPool and its renderer-bounds barrel alignment, kept until M14).</summary>
    public interface IPickupViews
    {
        /// <summary>V3 PickupViewPool.Acquire and ApplyUniformPickupScale.</summary>
        object Acquire(PickupType type, Vector3 position, int value);
        /// <summary>V3 AlignBarrelSubmerged: moves the barrel so it floats at its submerged depth; returns its position (register R-74).</summary>
        Vector3 AlignBarrel(object view);
        /// <summary>V3 BeginHomingPickup's scale change.</summary>
        void BeginHoming(object view, PickupType type);
        /// <summary>V3 Tick's view step: position, crystal scale, spin.</summary>
        void Update(object view, PickupType type, Vector3 position, bool homing, float dt);
        void Release(object view, PickupType type, int value);
    }

    /// <summary>Where pickups pay out (V3: LevelSystem.AddXP, the stage gold grant, the ship's heal).</summary>
    public interface IPickupRewards
    {
        bool RunEnded { get; }
        /// <summary>The run's XP gain stat (V3 ComputeXpGrant).</summary>
        float XpGain { get; }
        void GrantXp(int amount);
        void GrantGold(int amount);
        void HealShip(int amount);
        /// <summary>V3's health-drop draw on the Director stream (false without a run).</summary>
        bool RollHealthDrop(float chance);
        void PlaySound(string id);
    }

    /// <summary>The random draws pickups make (drop jitter, bob phase, burst count, scatter, fly delay).</summary>
    public interface IPickupRandom
    {
        float Range(float min, float max);
        int Range(int minInclusive, int maxExclusive);
        Vector3 InsideUnitSphere();
    }

    /// <summary>A seed-derived stream for the pickup draws (M14; its own, so presentation can never move a pickup).</summary>
    public sealed class SeededPickupRandom : IPickupRandom
    {
        readonly System.Random _random;
        public SeededPickupRandom(int seed) { _random = new System.Random(seed); }
        public float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
        public int Range(int minInclusive, int maxExclusive) => _random.Next(minInclusive, maxExclusive);

        public Vector3 InsideUnitSphere()
        {
            while (true)
            {
                var v = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));
                if (v.sqrMagnitude <= 1f) return v;
            }
        }
    }

    /// <summary>
    /// Pickups (M9 step 9.7): port of V3's PickupWorld — kill drops (XP, gold and health barrels), merging when full,
    /// collection radii, homing, barrel bursts into flying pieces, the per-frame budget and the grants, with every
    /// random draw where V3 made it. Magnetic Tide can open the barrels (R-103). M14 (intended change): the draws come from
    /// the module's own seed-derived stream instead of UnityEngine.Random, which presentation code (damage numbers, camera
    /// shake, VFX, the chest bob) also drew from — moving or removing any VFX used to move the pickups.
    /// </summary>
    public sealed class PickupModule
    {
        /// <summary>The pickup draws (a seeded stream in runs; a parity test plugs in UnityEngine.Random, V3's source).</summary>
        public IPickupRandom Random = new SeededPickupRandom(0);

        /// <summary>Seeds the pickup stream for a run (run start; the run seed decides every scatter and delay).</summary>
        public void Seed(int runSeed) => Random = new SeededPickupRandom(runSeed ^ 0x71C0B5);

        float Range(float min, float max) => Random.Range(min, max);
        int Range(int minInclusive, int maxExclusive) => Random.Range(minInclusive, maxExclusive);
        Vector3 InsideUnitSphere() => Random.InsideUnitSphere();

        struct Agent
        {
            public bool Active, Homing;
            public PickupType Type;
            public int Value;
            public Vector3 Position, BasePosition;
            public float BobPhase, FlyDelay;
            public object View;
        }

        struct Pending
        {
            public PickupType Type;
            public Vector3 Position;
            public int Value;
            public float FlyDelay;
        }

        readonly Func<PickupConfig> _config;
        readonly IPickupRewards _rewards;
        readonly IPickupViews _views;
        readonly Func<bool> _tideOpensBarrels;
        readonly Func<float> _vfxDensity;
        readonly Queue<Pending> _pending = new Queue<Pending>();
        Agent[] _pool = new Agent[0];
        int _active, _pendingXp, _pendingGold, _pendingHeal;
        float _time;

        public PickupModule(Func<PickupConfig> config, int capacity, IPickupRewards rewards, IPickupViews views, Func<bool> magneticTideOpensBarrels, Func<float> vfxDensity)
        {
            _config = config;
            _pool = new Agent[Mathf.Max(1, capacity)];
            _rewards = rewards;
            _views = views;
            _tideOpensBarrels = magneticTideOpensBarrels;
            _vfxDensity = vfxDensity;
        }

        PickupConfig C => _config();
        public int ActiveCount => _active;
        public int Capacity => _pool.Length;

        /// <summary>A pickup's state (tests and the trace pilot).</summary>
        public bool TryGet(int slot, out PickupType type, out int value, out Vector3 position, out bool homing)
        {
            var a = _pool[slot];
            type = a.Type; value = a.Value; position = a.Position; homing = a.Homing;
            return a.Active;
        }

        // ---- Drops ---------------------------------------------------------------------------------------------------

        /// <summary>V3 SpawnFromEnemy under the stage profile (XP and gold as authored; no rich doubling).</summary>
        public void SpawnFromEnemy(Vector3 position, int xp, int gold)
        {
            var c = C;
            var basePos = position;
            basePos.y = 0f;
            var health = _rewards.RollHealthDrop(c.healthDropChance);
            var count = 0;
            if (xp > 0) count++;
            if (gold > 0) count++;
            if (health) count++;
            var index = 0;
            if (xp > 0) Spawn(PickupType.XpBarrel, Mathf.Max(1, Mathf.RoundToInt(xp * 1f)), basePos + DropOffset(index++, count));
            if (gold > 0) Spawn(PickupType.GoldBarrel, gold, basePos + DropOffset(index++, count));
            if (health) Spawn(PickupType.Powerup, c.healthDropValue, basePos + DropOffset(index, count));
        }

        Vector3 DropOffset(int index, int count)
        {
            if (count <= 1) return Vector3.zero;
            var c = C;
            var angle = (index * (360f / count) + Range(-c.dropAngleJitterDegrees, c.dropAngleJitterDegrees)) * Mathf.Deg2Rad;
            var radius = c.dropSpreadRadius + Range(-c.dropRadiusJitter, c.dropRadiusJitter);
            return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
        }

        /// <summary>V3 Spawn.</summary>
        public void Spawn(PickupType type, int value, Vector3 position)
        {
            if (_active >= _pool.Length) MergeNearby(type, value, position);
            var slot = FreeSlot();
            if (slot < 0) return;
            position.y = Height(type);
            var view = _views?.Acquire(type, position, value);
            if (IsBarrel(type) && view != null) position = _views.AlignBarrel(view);
            _pool[slot] = new Agent
            {
                Active = true, Homing = false, Type = type, Value = value, Position = position, BasePosition = position,
                BobPhase = Range(0f, Mathf.PI * 2f), View = view,
            };
            _active++;
        }

        static bool IsBarrel(PickupType type) => type is PickupType.GoldBarrel or PickupType.XpBarrel or PickupType.Powerup;

        static float Height(PickupType type) => type switch
        {
            PickupType.GoldBarrel or PickupType.XpBarrel or PickupType.Powerup => 0f,
            PickupType.FlyingGold or PickupType.FlyingHealth or PickupType.FlyingXp => 0.45f,
            _ => 0.35f,
        };

        int FreeSlot()
        {
            for (var i = 0; i < _pool.Length; i++) if (!_pool[i].Active) return i;
            return -1;
        }

        void MergeNearby(PickupType type, int value, Vector3 position)
        {
            var radius = C.mergeRadius;
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active || _pool[i].Type != type) continue;
                if (Vector3.Distance(_pool[i].Position, position) > radius) continue;
                _pool[i].Value += value;
                return;
            }
        }

        // ---- Powerups that pull pickups ---------------------------------------------------------------------------------

        /// <summary>Salvage Chime (V3 AttractAllInfamy).</summary>
        public void AttractAllXp()
        {
            for (var i = 0; i < _pool.Length; i++)
                if (_pool[i].Active && _pool[i].Type == PickupType.Infamy) BeginHoming(i);
        }

        /// <summary>Magnetic Tide (V3 AttractLooseRewards; R-103: then every barrel opens where it lies).</summary>
        public void AttractLooseRewards()
        {
            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) continue;
                switch (_pool[i].Type)
                {
                    case PickupType.Infamy:
                    case PickupType.Gold:
                    case PickupType.FlyingGold:
                    case PickupType.FlyingXp:
                    case PickupType.GoldBarrel:
                    case PickupType.XpBarrel:
                        BeginHoming(i);
                        break;
                }
            }
            if (_tideOpensBarrels == null || !_tideOpensBarrels()) return;
            for (var i = 0; i < _pool.Length; i++)
                if (_pool[i].Active && IsBarrel(_pool[i].Type)) Collect(i);
        }

        public void ClearAll()
        {
            _pending.Clear();
            _pendingXp = _pendingGold = _pendingHeal = 0;
            for (var i = 0; i < _pool.Length; i++) if (_pool[i].Active) Release(i);
        }

        // ---- The frame (V3 Tick) -------------------------------------------------------------------------------------------

        public void Tick(float dt, Vector3 playerPos, float pickupRadius)
        {
            if (_rewards.RunEnded) return;
            var c = C;
            ProcessPending();
            _time += dt;
            var xpRadius = Mathf.Max(c.xpAutoPickupRadius, pickupRadius > 0f ? pickupRadius : GameConstants.DefaultPickupRadius);
            var goldRadius = c.goldAutoPickupRadius;
            var touchRadius = c.barrelTouchRadius;

            for (var i = 0; i < _pool.Length; i++)
            {
                if (!_pool[i].Active) continue;
                var toPlayer = playerPos - _pool[i].Position;
                toPlayer.y = 0f;
                var dist = toPlayer.magnitude;
                var type = _pool[i].Type;

                if (_pool[i].Homing || type == PickupType.FlyingGold || type == PickupType.FlyingHealth || type == PickupType.FlyingXp)
                {
                    if (_pool[i].FlyDelay > 0f) _pool[i].FlyDelay -= dt;
                    else
                    {
                        var flySpeed = Mathf.Lerp(c.flySpeedMin, c.flySpeedMax, 1f - Mathf.Clamp01(dist / Mathf.Max(goldRadius, 1f)));
                        _pool[i].Position += toPlayer.normalized * flySpeed * dt;
                        _pool[i].BasePosition = _pool[i].Position;
                        if (dist <= c.collectDistance)
                        {
                            CompleteHoming(i);
                            continue;
                        }
                    }
                }
                else if (type == PickupType.Infamy && dist <= xpRadius) BeginHoming(i);
                else if ((type == PickupType.Gold || type == PickupType.FlyingGold) && dist <= goldRadius) BeginHoming(i);
                else if (IsBarrel(type) && dist <= touchRadius)
                {
                    Collect(i);
                    continue;
                }
                else
                {
                    var bob = Mathf.Sin(_time * c.bobSpeed + _pool[i].BobPhase) * c.bobAmplitude;
                    _pool[i].Position = _pool[i].BasePosition + Vector3.up * bob;
                }

                if (_pool[i].View != null) _views.Update(_pool[i].View, type, _pool[i].Position, _pool[i].Homing, dt);
            }

            FlushGrants();
        }

        void BeginHoming(int i)
        {
            _pool[i].Homing = true;
            _pool[i].FlyDelay = 0f;
            if (_pool[i].View != null) _views.BeginHoming(_pool[i].View, _pool[i].Type);
        }

        void CompleteHoming(int i)
        {
            if (_rewards.RunEnded)
            {
                Release(i);
                return;
            }
            switch (_pool[i].Type)
            {
                case PickupType.FlyingXp:
                case PickupType.Infamy:
                    _pendingXp += XpGrant(_pool[i].Value);
                    _rewards.PlaySound("pickup_xp");
                    break;
                case PickupType.Gold:
                case PickupType.FlyingGold:
                    _pendingGold += _pool[i].Value;
                    _rewards.PlaySound("pickup_gold");
                    break;
                case PickupType.FlyingHealth:
                    _pendingHeal += _pool[i].Value;
                    _rewards.PlaySound("pickup_health");
                    break;
            }
            Release(i); // a homing barrel pays nothing (V3; Magnetic Tide's R-103 fix opens barrels instead)
        }

        int XpGrant(int value) => Mathf.Max(1, Mathf.RoundToInt(value * _rewards.XpGain));

        void FlushGrants()
        {
            if (_pendingXp > 0) { var xp = _pendingXp; _pendingXp = 0; _rewards.GrantXp(xp); }
            if (_pendingGold > 0) { var gold = _pendingGold; _pendingGold = 0; _rewards.GrantGold(gold); }
            if (_pendingHeal > 0) { var heal = _pendingHeal; _pendingHeal = 0; _rewards.HealShip(heal); }
        }

        // ---- Barrels and flying pieces ------------------------------------------------------------------------------------

        void Collect(int i)
        {
            var p = _pool[i];
            switch (p.Type)
            {
                case PickupType.GoldBarrel: Burst(PickupType.FlyingGold, p.Position, p.Value); break;
                case PickupType.XpBarrel: Burst(PickupType.FlyingXp, p.Position, p.Value); break;
                case PickupType.Powerup: Burst(PickupType.FlyingHealth, p.Position, p.Value); break;
                default: return;
            }
            Release(i);
        }

        void Burst(PickupType flying, Vector3 origin, int total)
        {
            var c = C;
            var min = c.burstCountMin;
            var max = Mathf.Max(min, c.burstCountMax);
            var count = Mathf.Max(c.burstMinimum, Mathf.RoundToInt(Range(min, max + 1) * _vfxDensity()));
            var perPiece = Mathf.Max(1, total / count);
            var remainder = total - perPiece * count;
            for (var k = 0; k < count; k++)
            {
                var scatter = InsideUnitSphere() * 0.8f;
                scatter.y = 0f;
                var pos = origin + scatter;
                pos.y = 0.45f;
                _pending.Enqueue(new Pending
                {
                    Type = flying, Position = pos, Value = perPiece + (k == 0 ? remainder : 0),
                    FlyDelay = Range(0.05f, 0.25f) + k * 0.02f,
                });
            }
        }

        void ProcessPending()
        {
            var budget = C.maxFlyingSpawnsPerFrame;
            while (budget-- > 0 && _pending.Count > 0)
            {
                var p = _pending.Dequeue();
                if (!TrySpawnFlying(p)) PayDirectly(p.Type, p.Value);
            }
        }

        bool TrySpawnFlying(Pending p)
        {
            if (_active >= _pool.Length) return false;
            var slot = FreeSlot();
            if (slot < 0) return false;
            var view = _views?.Acquire(p.Type, p.Position, p.Value);
            _pool[slot] = new Agent
            {
                Active = true, Homing = true, Type = p.Type, Value = p.Value, Position = p.Position, BasePosition = p.Position,
                BobPhase = Range(0f, Mathf.PI * 2f), FlyDelay = p.FlyDelay, View = view,
            };
            _active++;
            return true;
        }

        void PayDirectly(PickupType type, int value)
        {
            switch (type)
            {
                case PickupType.FlyingXp: _pendingXp += XpGrant(value); break;
                case PickupType.FlyingGold: _pendingGold += value; break;
                case PickupType.FlyingHealth: _pendingHeal += value; break;
            }
        }

        void Release(int i)
        {
            if (_pool[i].View != null) _views.Release(_pool[i].View, _pool[i].Type, _pool[i].Value);
            _pool[i].Active = false;
            _pool[i].Homing = false;
            _pool[i].View = null;
            _active--;
        }
    }
}
