using System;
using System.Reflection;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Enemies;
using HighSeas.Pickups;
using HighSeas.Platform;
using HighSeas.Player;
using HighSeas.World;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>
    /// Player-like autopilot for trace runs: kites nearby enemies (away + circling), dodges incoming hostile shots and
    /// hazard circles, collects XP/gold/barrels when it is safe, stays inside the storm-safe rectangle, steers around
    /// islands and backs out when stuck. It reads only simulation state (agents, pools, physics islands, bounds), so
    /// the same seed gives the same path in V3 and V4. The private legacy pools are read by reflection because V3 code
    /// is not edited (D1); this class lives in DevTools and never ships.
    /// </summary>
    public sealed class PlayerPilot : IShipSteerInput
    {
        public const string PatternId = "player-v1";

        const float ThreatRange = 20f;
        const float ProjectileRange = 14f;
        const float PickupRange = 34f;
        const float EdgeComfort = 22f;
        const float IslandProbe = 16f;

        static readonly FieldInfo PickupPool = typeof(PickupWorld).GetField("_pool", BindingFlags.Instance | BindingFlags.NonPublic);

        readonly PlayerShipController _ship;
        RoguePirates.V4.Enemies.IEnemyWorldView _enemies;
        RoguePirates.V4.Progression.PickupModule _pickups;
        RoguePirates.V4.Enemies.IHostileProjectileView _shots;
        RoguePirates.V4.Enemies.IHazardView _hazards;
        float _steerSign;
        Vector3 _stuckAnchor;
        float _stuckClock;
        float _reverseUntil;
        float _graceUntil;
        float _reverseSteer = 1f;
        float _lastTime = -1f;

        public PlayerPilot(PlayerShipController ship)
        {
            _ship = ship;
            _stuckAnchor = ship != null ? ship.Position : Vector3.zero;
        }

        public Vector2 ReadSteerThrottle()
        {
            if (_ship == null || RunManager.Instance == null) return new Vector2(0f, 1f);
            var t = RunManager.Instance.ElapsedSeconds;
            var pos = Flat(_ship.Position);
            var forward = Flat(_ship.MovementForward).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.forward;

            if (UpdateStuck(t, pos, forward))
                return new Vector2(_reverseSteer, -1f); // back out; reversing with this steer swings the bow toward open water

            var threat = Vector3.zero;
            var danger = 0f;
            AddEnemies(pos, ref threat, ref danger);
            AddProjectiles(pos, ref threat, ref danger);
            AddHazards(pos, ref threat, ref danger);

            var desired = threat;
            // Kiting: circle the threat instead of fleeing in a straight line into a wall or island.
            if (threat.sqrMagnitude > 0.0001f)
                desired += Vector3.Cross(Vector3.up, threat.normalized) * (0.6f * threat.magnitude);

            if (danger < 1.2f) desired += PickupPull(pos) * (1.2f - danger);
            desired += EdgePush(pos);
            if (desired.sqrMagnitude < 0.04f)
                desired += Vector3.Cross(Vector3.up, Flat(PlayBounds.Center) - pos).normalized * 0.5f + forward * 0.3f; // patrol loop

            var dir = desired.sqrMagnitude > 0.0001f ? desired.normalized : forward;
            var avoided = IslandObstacles.ApplyAvoidanceSteering(pos, _ship.Velocity, dir, _ship.IslandCollisionRadius, IslandProbe);
            if (Flat(avoided).sqrMagnitude > 0.0001f) dir = Flat(avoided).normalized;
            dir = FindFreeDirection(pos, dir);

            var angle = Vector3.SignedAngle(forward, dir, Vector3.up);
            // Hysteresis: flipping steer sign costs the ship a turn delay, so only flip for a clear reason.
            var sign = Mathf.Sign(angle);
            if (_steerSign != 0f && sign != _steerSign && Mathf.Abs(angle) < 15f) sign = _steerSign;
            var steer = Mathf.Abs(angle) < 3f ? 0f : sign * Mathf.Clamp01(Mathf.Abs(angle) / 50f);
            if (steer != 0f) _steerSign = Mathf.Sign(steer);
            var throttle = Mathf.Abs(angle) > 110f ? 0.55f : 1f;
            return new Vector2(steer, throttle);
        }

        /// <summary>
        /// Backs out only when the ship is barely moving AND an island is right ahead of the bow; after a back-out
        /// the ship gets a grace period to brake and accelerate before being judged again.
        /// </summary>
        bool UpdateStuck(float t, Vector3 pos, Vector3 forward)
        {
            if (_lastTime < 0f) { _lastTime = t; _stuckAnchor = pos; }
            if (t < _reverseUntil) return true;
            _stuckClock += Mathf.Max(0f, t - _lastTime);
            _lastTime = t;
            if (t < _graceUntil || _stuckClock < 2f) return false;
            var moved = (pos - _stuckAnchor).magnitude;
            _stuckClock = 0f;
            _stuckAnchor = pos;
            if (moved >= 3f || !Blocked(pos + forward * (_ship.IslandCollisionRadius + 2.5f))) return false;

            // Reversing with steer s turns the bow by -s relative to forward motion; pick the side whose bow-ward
            // quarter is open, so after backing out the ship points at water.
            var left = Blocked(pos + Quaternion.Euler(0f, -60f, 0f) * forward * 6f);
            var right = Blocked(pos + Quaternion.Euler(0f, 60f, 0f) * forward * 6f);
            _reverseSteer = left && !right ? 1f : !left && right ? -1f : (_steerSign == 0f ? 1f : -_steerSign);
            _reverseUntil = t + 1.4f;
            _graceUntil = _reverseUntil + 3f;
            return true;
        }

        /// <summary>Keeps the heading if the water along it is open, else fans out in 25° steps toward the preferred side.</summary>
        Vector3 FindFreeDirection(Vector3 pos, Vector3 dir)
        {
            for (var k = 0; k <= 7; k++)
            {
                for (var side = 0; side < (k == 0 ? 1 : 2); side++)
                {
                    var sign = side == 0 ? 1f : -1f;
                    if (_steerSign < 0f) sign = -sign;
                    var candidate = Quaternion.Euler(0f, sign * k * 25f, 0f) * dir;
                    if (!Blocked(pos + candidate * 5f) && !Blocked(pos + candidate * 10f)) return candidate;
                }
            }
            return dir;
        }

        bool Blocked(Vector3 point) =>
            IslandObstacles.IsPositionBlocked(point, _ship.IslandCollisionRadius) || !PlayBounds.IsInside(point, 2f);

        void AddEnemies(Vector3 pos, ref Vector3 threat, ref float danger)
        {
            ResolveViews();
            if (!_enemies.Exists) return;
            var capacity = _enemies.Capacity;
            for (var i = 0; i < capacity; i++)
            {
                if (!_enemies.TryGet(i, out var agent)) continue;
                var away = pos - Flat(agent.position);
                var range = ThreatRange + agent.radius + (agent.isBoss ? 10f : 0f);
                var d = away.magnitude;
                if (d > range || d < 0.001f) continue;
                var w = (1f - d / range);
                w *= w * (agent.isBoss ? 2.5f : agent.isElite ? 1.6f : 1f);
                threat += away / d * w;
                danger += w;
            }
        }

        void AddProjectiles(Vector3 pos, ref Vector3 threat, ref float danger)
        {
            ResolveViews();
            var capacity = _shots.Capacity;
            for (var i = 0; i < capacity; i++)
            {
                if (!_shots.TryGet(i, out var shot)) continue;
                var rel = pos - Flat(shot.position);
                if (rel.sqrMagnitude > ProjectileRange * ProjectileRange) continue;
                var v = Flat(shot.velocity);
                var speed2 = v.sqrMagnitude;
                if (speed2 < 0.01f) continue;
                var tClosest = Vector3.Dot(rel, v) / speed2; // seconds until closest approach
                if (tClosest < 0f || tClosest > 1.5f) continue;
                var miss = rel - v * tClosest;
                var missDist = miss.magnitude;
                if (missDist > 3.5f) continue;
                var side = missDist > 0.05f ? miss / missDist : Vector3.Cross(Vector3.up, v.normalized);
                var w = 3f * (1f - missDist / 3.5f) * (1f - tClosest / 1.5f);
                threat += side * w;
                danger += w;
            }
        }

        void AddHazards(Vector3 pos, ref Vector3 threat, ref float danger)
        {
            ResolveViews();
            var capacity = _hazards.Capacity;
            for (var i = 0; i < capacity; i++)
            {
                if (!_hazards.TryGet(i, out var hazard)) continue;
                var away = pos - Flat(hazard.center);
                var reach = hazard.radius + 3f;
                var d = away.magnitude;
                if (d > reach) continue;
                var w = 3f * (1f - d / reach) + 0.5f;
                threat += (d > 0.05f ? away / d : Vector3.forward) * w;
                danger += w;
            }
        }

        /// <summary>The run's enemy views; in V3 scenes (golden recordings, no V4 run context) the legacy ones.</summary>
        void ResolveViews()
        {
            if (_enemies != null && _shots != null && _hazards != null) return;
            var game = UnityEngine.Object.FindFirstObjectByType<RoguePirates.V4.V4Game>();
            var context = game != null ? game.Context : null;
            _pickups = game != null && game.Build != null ? game.Build.Pickups : null; // M9: the V4 pickups
            if (context != null)
            {
                context.TryGet(out _enemies);
                context.TryGet(out _shots);
                context.TryGet(out _hazards);
            }
            if (game != null && (_enemies == null || _shots == null || _hazards == null))
                Debug.LogError("PlayerPilot: the V4 run does not register the enemy views");
            if (game != null) return;
            // V3 scenes (the golden recordings) have no V4 run: the legacy world through the same views.
            _enemies ??= new RoguePirates.V4.Legacy.LegacyEnemyWorldView();
            _shots ??= new RoguePirates.V4.Legacy.LegacyHostileProjectileView();
            _hazards ??= new RoguePirates.V4.Legacy.LegacyHazardView();
        }

        /// <summary>The pickups on the water: the V4 module (M9), else V3's PickupWorld (golden recordings).</summary>
        int PickupCount(out PickupAgent[] legacy)
        {
            legacy = null;
            if (_pickups != null) return _pickups.Capacity;
            if (PickupWorld.Instance == null || !(PickupPool?.GetValue(PickupWorld.Instance) is PickupAgent[] pool)) return 0;
            legacy = pool;
            return pool.Length;
        }

        bool TryGetPickup(PickupAgent[] legacy, int i, out PickupType type, out Vector3 position, out bool homing)
        {
            if (legacy == null) return _pickups.TryGet(i, out type, out _, out position, out homing);
            type = legacy[i].type;
            position = legacy[i].position;
            homing = legacy[i].homingToPlayer;
            return legacy[i].active;
        }

        Vector3 PickupPull(Vector3 pos)
        {
            var count = PickupCount(out var legacy);
            var best = Vector3.zero;
            var bestScore = 0f;
            for (var i = 0; i < count; i++)
            {
                if (!TryGetPickup(legacy, i, out var type, out var position, out var homing) || homing) continue;
                var value = type switch
                {
                    PickupType.XpBarrel => 3f,
                    PickupType.GoldBarrel => 2.5f,
                    PickupType.Powerup => 2.5f,
                    PickupType.Infamy => 1f,
                    PickupType.Gold => 0.6f,
                    _ => 0f
                };
                if (value <= 0f) continue;
                var to = Flat(position) - pos;
                var d = to.magnitude;
                if (d > PickupRange || d < 0.5f) continue;
                if (!PlayBounds.IsInsideStormSafeZone(position)) continue;
                var score = value / (1f + d * 0.15f);
                if (score > bestScore) { bestScore = score; best = to / d; }
            }
            return best * 0.9f;
        }

        static Vector3 EdgePush(Vector3 pos)
        {
            if (!PlayBounds.IsConfigured) return Vector3.zero;
            var center = Flat(PlayBounds.Center);
            var halfX = (PlayBounds.HalfExtentX - PlayBounds.StormEdgeMargin) * PlayBounds.StormSafeScale;
            var halfZ = (PlayBounds.HalfExtentZ - PlayBounds.StormEdgeMargin) * PlayBounds.StormSafeScale;
            var off = pos - center;
            var clearance = Mathf.Min(halfX - Mathf.Abs(off.x), halfZ - Mathf.Abs(off.z));
            if (clearance >= EdgeComfort) return Vector3.zero;
            var toCenter = (center - pos).normalized;
            return toCenter * (2.5f * (1f - Mathf.Clamp(clearance, -EdgeComfort, EdgeComfort) / EdgeComfort));
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
