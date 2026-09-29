using System.Collections.Generic;
using System.Reflection;
using HighSeas.Core;
using HighSeas.Pickups;
using HighSeas.Platform;
using HighSeas.Player;
using HighSeas.POI;
using HighSeas.UI;
using HighSeas.World;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Stage;
using RoguePirates.V4.Views;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>
    /// A pilot that plays like a person (playtest round 1, for calibration and measurement; the V3-comparison traces keep
    /// <see cref="PlayerPilot"/>): it dodges shots and hazards, fights packs from weapon range while circling them, picks
    /// up XP/gold when it is safe, explores water it has not seen, buys chests it can afford, uses landmarks (the Repair
    /// Wreck when hurt), and — once its build is ready — finds the Beacon, summons the Barnacle King and fights him. It
    /// reads only simulation state and the run's views, and makes no random draws, so a seed always gives the same run.
    /// </summary>
    public sealed class HumanPilot : IShipSteerInput
    {
        public const string PatternId = "human-v1";

        const float EngageRange = 28f;      // packs it goes after
        const float FightDistance = 11f;    // where it likes to fight from
        const float TooClose = 6.5f;        // enemies nearer than this push it away
        const float BossDistance = 14f;
        const float ChargeSpeed = 9f;       // an enemy closing faster than this is charging
        const float BigEnemyHpRatio = 4f;   // max HP × the average from which an enemy counts as a big hitter
        const float ProjectileRange = 26f;
        const float PickupRange = 22f;
        const float EdgeComfort = 18f;
        const float IslandProbe = 16f;
        const float GoalEverySeconds = 1f;

        readonly PlayerShipController _ship;
        RoguePirates.V4.Enemies.IEnemyWorldView _enemies;
        RoguePirates.V4.Enemies.IHostileProjectileView _shots;
        RoguePirates.V4.Enemies.IHazardView _hazards;
        IBossView _boss;
        float _averageHp;        // enemies' average max HP (last frame)
        bool _escape;            // inside a blast circle / a charge lane: dodging beats everything else
        RoguePirates.V4.Progression.PickupModule _pickups;
        IRunViews _views;
        IStageContent _stage;
        float _orbitSign = 1f, _steerSign, _lastGoalAt = -10f, _stuckClock, _reverseUntil, _graceUntil, _reverseSteer = 1f, _lastTime = -1f;
        Vector3 _stuckAnchor;

        /// <summary>What it is heading for now (console, measurement logs).</summary>
        public string Goal { get; private set; } = "explore";
        /// <summary>The landmark it is heading for, or -1 (the player component holds E when in range).</summary>
        public int GoalPoi { get; private set; } = -1;
        Vector3 _goalPoint;
        bool _hasGoal;
        int _crowd;              // enemies within the kiting radius (last frame)
        Vector3 _crowdCentre;

        /// <summary>Level from which it goes for the Beacon (else from <see cref="BeaconAtSeconds"/>).</summary>
        public int BeaconAtLevel = 9;
        public float BeaconAtSeconds = 360f;

        public HumanPilot(PlayerShipController ship)
        {
            _ship = ship;
            _stuckAnchor = ship != null ? ship.Position : Vector3.zero;
        }

        public Vector2 ReadSteerThrottle()
        {
            if (_ship == null || RunManager.Instance == null) return new Vector2(0f, 1f);
            Resolve();
            var t = RunManager.Instance.ElapsedSeconds;
            var pos = Flat(_ship.Position);
            var forward = Flat(_ship.MovementForward).normalized;
            if (forward.sqrMagnitude < 0.5f) forward = Vector3.forward;
            if (UpdateStuck(t, pos, forward)) return new Vector2(_reverseSteer, -1f);
            if (t - _lastGoalAt >= GoalEverySeconds) { _lastGoalAt = t; ChooseGoal(t, pos); }

            var danger = 0f;
            var avoid = Vector3.zero;
            _escape = false;
            AddProjectiles(pos, ref avoid, ref danger);
            AddHazards(pos, forward, ref avoid, ref danger);
            AddBossAttacks(pos, forward, ref avoid, ref danger);
            var fight = Fight(pos, ref avoid, ref danger);
            if (_escape) fight *= 0.2f; // a person stops shooting-and-circling to get out of the marked water

            var hull = _views?.Ship != null && _views.Ship.MaxHp > 0f ? _views.Ship.Hp / _views.Ship.MaxHp : 1f;
            var desired = avoid * 1.6f + fight;
            // Hurt: back away from the pack (people retreat, heal from barrels, then go back in).
            if (hull < 0.4f && _crowd > 0) desired += (pos - _crowdCentre).normalized * 1.5f;
            var pickup = !_escape && (danger < 1.2f || hull < 0.5f) ? PickupPull(pos) : Vector3.zero;
            if (pickup.sqrMagnitude > 0f) desired += pickup * (hull < 0.5f ? 1.6f : 1.1f);
            else if (_hasGoal && !_escape)
            {
                var to = _goalPoint - pos;
                var d = to.magnitude;
                // A goal close by is pushed through (chests, the Beacon) unless something is on top of it; far ones wait
                // while it is busy fighting.
                var w = danger > 1.5f ? 0.3f : d < 30f ? 2f : danger < 0.8f ? 1.2f : 0.6f;
                if (d > 1f) desired += to / d * w;
            }
            desired += EdgePush(pos);
            if (desired.sqrMagnitude < 0.04f) desired = forward;

            var dir = desired.normalized;
            // IslandObstacles.ApplyAvoidanceSteering returns the ship's current velocity when no island is ahead (it ignores
            // the preferred direction) — using that result unconditionally made the trace pilot sail straight until the
            // map edge turned it, lapping the outskirts. Use it only when it actually steered around an island.
            var velocity = _ship.Velocity;
            var avoided = IslandObstacles.ApplyAvoidanceSteering(pos, velocity, dir, _ship.IslandCollisionRadius, IslandProbe);
            if ((Flat(avoided) - Flat(velocity)).sqrMagnitude > 0.01f && Flat(avoided).sqrMagnitude > 0.0001f)
                dir = (Flat(avoided).normalized + dir * 0.5f).normalized;
            dir = FindFreeDirection(pos, dir);

            var angle = Vector3.SignedAngle(forward, dir, Vector3.up);
            var sign = Mathf.Sign(angle);
            if (_steerSign != 0f && sign != _steerSign && Mathf.Abs(angle) < 12f) sign = _steerSign;
            var steer = Mathf.Abs(angle) < 3f ? 0f : sign * Mathf.Clamp01(Mathf.Abs(angle) / 45f);
            if (steer != 0f) _steerSign = Mathf.Sign(steer);
            // Slows into tight turns and when it has arrived at a landmark it must hold.
            var throttle = Mathf.Abs(angle) > 110f && !_escape ? 0.5f : 1f;
            if (GoalPoi >= 0 && _hasGoal && (_goalPoint - pos).magnitude < 4f && danger < 1f) throttle = 0.25f;
            return new Vector2(steer, throttle);
        }

        // ---- goals ------------------------------------------------------------------------------------------------

        void ChooseGoal(float t, Vector3 pos)
        {
            _hasGoal = false;
            GoalPoi = -1;
            var stage = _views?.Stage;
            if (stage == null || !stage.Exists) { Goal = "fight"; return; }
            if (stage.BossSummoned && !stage.BossDefeated) { Goal = "boss"; return; }

            var hull = _views.Ship != null && _views.Ship.MaxHp > 0f ? _views.Ship.Hp / _views.Ship.MaxHp : 1f;
            var gold = _views.Run != null ? _views.Run.Gold : 0;
            var level = _views.Progression != null ? _views.Progression.Level : 0;
            var price = _stage != null && _stage.Module != null ? _stage.Module.QuoteChestPrice() : int.MaxValue;

            // The Beacon, once the build is ready (and a hurt ship repairs first).
            if (!stage.BossSummoned && stage.BeaconPosition.HasValue && (level >= BeaconAtLevel || t >= BeaconAtSeconds) && hull > 0.45f)
            {
                var beacon = FindPoi(stage, pos, p => p.type == POIType.BossBeacon, 1e9f);
                SetGoal(beacon >= 0 ? stage.Poi(beacon).position : stage.BeaconPosition.Value, beacon, "beacon");
                return;
            }
            var best = -1;
            var bestScore = 0f;
            for (var i = 0; i < stage.PoiCount; i++)
            {
                var p = stage.Poi(i);
                if (p.used || !stage.IsPoiVisible(i)) continue;
                var value = 0f;
                switch (p.type)
                {
                    case POIType.SalvageChest: value = gold >= price ? 3f : 0f; break;
                    case POIType.GildedWreck: value = hull < 0.6f ? 5f : 0f; break;           // Repair Wreck
                    case POIType.TrialBuoy: value = 1.6f; break;                              // Signal Buoy
                    case POIType.CursedIdol: value = 1.4f; break;                             // Treasure Chart
                    case POIType.MerchantSkiff: value = level >= 6 && hull > 0.6f ? 1.2f : 0f; break; // Powder Cache challenge
                }
                if (value <= 0f) continue;
                var d = (Flat(p.position) - pos).magnitude;
                if (!PlayBounds.IsInsideStormSafeZone(p.position)) continue;
                var score = value / (1f + d / 40f);
                if (score > bestScore) { bestScore = score; best = i; }
            }
            if (best >= 0)
            {
                var p = stage.Poi(best);
                SetGoal(p.position, best, p.type == POIType.SalvageChest ? "chest" : "landmark:" + p.type);
                return;
            }
            // Explore: the nearest water it has not seen yet, searched in rings.
            if (stage.HasDiscovery && FindUnexplored(stage, pos, out var target)) { SetGoal(target, -1, "explore"); return; }
            Goal = "fight";
        }

        void SetGoal(Vector3 point, int poi, string name)
        {
            _goalPoint = Flat(point);
            GoalPoi = poi;
            Goal = name;
            _hasGoal = true;
        }

        static int FindPoi(IStageView stage, Vector3 pos, System.Predicate<PoiMarker> match, float maxDistance)
        {
            var best = -1;
            var bestD = maxDistance;
            for (var i = 0; i < stage.PoiCount; i++)
            {
                var p = stage.Poi(i);
                if (p.used || !match(p)) continue;
                var d = (Flat(p.position) - pos).magnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        bool FindUnexplored(IStageView stage, Vector3 pos, out Vector3 target)
        {
            var step = Mathf.Max(6f, stage.ExploreCellSize * 2f);
            for (var ring = 1; ring <= 14; ring++)
            {
                var r = ring * step;
                var samples = 8 + ring * 4;
                var bestD = float.MaxValue;
                target = Vector3.zero;
                var found = false;
                for (var k = 0; k < samples; k++)
                {
                    var a = k * Mathf.PI * 2f / samples;
                    var p = pos + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                    if (!PlayBounds.IsInsideStormSafeZone(p) || stage.IsCellExplored(p) || IslandObstacles.IsPositionBlocked(p, _ship.IslandCollisionRadius)) continue;
                    // Prefer the way it is already facing (people do not zig-zag across the map).
                    var d = (p - pos).magnitude - Vector3.Dot((p - pos).normalized, Flat(_ship.MovementForward).normalized) * step;
                    if (d < bestD) { bestD = d; target = p; found = true; }
                }
                if (found) return true;
            }
            target = Vector3.zero;
            return false;
        }

        // ---- fighting and dodging ---------------------------------------------------------------------------------

        /// <summary>Goes after the nearest pack, fights it from weapon range while circling, and backs off from anything
        /// too close (the boss from further away).</summary>
        Vector3 Fight(Vector3 pos, ref Vector3 avoid, ref float danger)
        {
            if (_enemies == null || !_enemies.Exists) return Vector3.zero;
            var centroid = Vector3.zero;
            var weight = 0f;
            var bossPos = Vector3.zero;
            var boss = false;
            var capacity = _enemies.Capacity;
            var hpSum = 0f;
            var hpCount = 0;
            for (var i = 0; i < capacity; i++)
            {
                if (!_enemies.TryGet(i, out var agent)) continue;
                var ep = Flat(agent.position);
                var away = pos - ep;
                var d = away.magnitude;
                if (agent.isBoss) { boss = true; bossPos = ep; }
                // Big hitters (minibosses, elites: they ram for a fifth of the hull) get a wider berth.
                hpSum += agent.maxHp;
                hpCount++;
                var big = agent.isElite || (_averageHp > 0f && agent.maxHp >= _averageHp * BigEnemyHpRatio);
                var near = (agent.isBoss ? BossDistance - 3f : big ? TooClose + 5f : TooClose) + agent.radius;
                // Something charging straight at it (a ram, a charge): step sideways out of the lane, as people do.
                var closing = Flat(agent.velocity);
                if (d < (big ? 16f : 9f) && d > 0.001f && closing.sqrMagnitude > ChargeSpeed * ChargeSpeed
                    && Vector3.Dot(closing.normalized, away / d) > 0.85f)
                {
                    var side = Vector3.Cross(Vector3.up, away / d);
                    if (Vector3.Dot(side, Flat(_ship.MovementForward)) < 0f) side = -side;
                    var w = big ? 5f : 2f;
                    avoid += side * w;
                    danger += w;
                    if (big) _escape = true;
                }
                if (d < near && d > 0.001f)
                {
                    var w = 1f - d / near;
                    w = w * w * (agent.isBoss ? 3f : big ? 3f : 1f) * 2.5f;
                    avoid += away / d * w;
                    danger += w;
                }
                if (d < EngageRange)
                {
                    var w = agent.isElite ? 2f : 1f;
                    centroid += ep * w;
                    weight += w;
                }
            }
            _averageHp = hpCount > 0 ? hpSum / hpCount : 0f;
            Vector3 target;
            float distance;
            _crowd = 0;
            _crowdCentre = pos;
            if (weight > 0f) { _crowdCentre = centroid / weight; _crowd = Mathf.RoundToInt(weight); }
            if (boss) { target = bossPos; distance = BossDistance; }
            else if (weight > 0f) { target = centroid / weight; distance = FightDistance; }
            else return Vector3.zero;
            // Crowded (a full screen): kite along the pack's edge instead of circling inside it.
            if (!boss && weight >= 10f) distance = FightDistance + Mathf.Min(10f, weight * 0.4f);

            var to = target - pos;
            var dist = to.magnitude;
            if (dist < 0.01f) return Vector3.zero;
            var dirTo = to / dist;
            var tangent = Vector3.Cross(Vector3.up, dirTo) * _orbitSign;
            // Circling blocked by an island: go round the other way.
            if (IslandObstacles.IsPositionBlocked(pos + tangent * 8f, _ship.IslandCollisionRadius)) _orbitSign = -_orbitSign;
            var radial = Mathf.Clamp((dist - distance) / distance, -1f, 1f); // + closes in, − backs off
            return dirTo * radial * 0.9f + tangent * 0.8f;
        }

        void AddProjectiles(Vector3 pos, ref Vector3 avoid, ref float danger)
        {
            if (_shots == null) return;
            for (var i = 0; i < _shots.Capacity; i++)
            {
                if (!_shots.TryGet(i, out var shot)) continue;
                var rel = pos - Flat(shot.position);
                if (rel.sqrMagnitude > ProjectileRange * ProjectileRange) continue;
                var v = Flat(shot.velocity);
                var speed2 = v.sqrMagnitude;
                if (speed2 < 0.01f) continue;
                var tClosest = Vector3.Dot(rel, v) / speed2;
                if (tClosest < 0f || tClosest > 2.2f) continue;
                var miss = rel - v * tClosest;
                var missDist = miss.magnitude;
                if (missDist > 3.5f) continue;
                var side = missDist > 0.05f ? miss / missDist : Vector3.Cross(Vector3.up, v.normalized);
                var w = 3f * (1f - missDist / 3.5f) * (1f - tClosest / 2.2f);
                avoid += side * w;
                danger += w;
            }
        }

        void AddHazards(Vector3 pos, Vector3 forward, ref Vector3 avoid, ref float danger)
        {
            if (_hazards == null) return;
            for (var i = 0; i < _hazards.Capacity; i++)
            {
                if (!_hazards.TryGet(i, out var hazard)) continue;
                AvoidCircle(pos, forward, Flat(hazard.center), hazard.radius, hazard.kind != HazardKind.NetSlow, ref avoid, ref danger);
            }
        }

        /// <summary>Gets out of a marked circle the shortest way (straight ahead when it is centred on the ship, since
        /// turning first loses the time the fuse gives), and gives circles it is not in a wide berth.</summary>
        void AvoidCircle(Vector3 pos, Vector3 forward, Vector3 centre, float radius, bool lethal, ref Vector3 avoid, ref float danger)
        {
            var away = pos - centre;
            var reach = radius + 4f;
            var d = away.magnitude;
            if (d > reach) return;
            var dir = d > 0.8f ? away / d : forward;
            var inside = d < radius + 1.5f;
            var w = (lethal ? 6f : 2f) * (1f - d / reach) + (inside ? 2f : 0.5f);
            avoid += dir * w;
            danger += w;
            if (inside && lethal) _escape = true;
        }

        /// <summary>The Barnacle King's telegraphs: leaves the marked circle, and steps sideways out of a charge lane.</summary>
        void AddBossAttacks(Vector3 pos, Vector3 forward, ref Vector3 avoid, ref float danger)
        {
            if (_boss == null || !_boss.Active) return;
            if (_boss.MarkedTelegraphVisible)
                AvoidCircle(pos, forward, Flat(_boss.MarkedTelegraphCenter), _boss.MarkedTelegraphRadius, true, ref avoid, ref danger);
            if (_boss.CurrentAttack == BossAttackKind.CommittedCharge && _boss.Phase != BossPhase.Recovery && _enemies != null)
            {
                for (var i = 0; i < _enemies.Capacity; i++)
                {
                    if (!_enemies.TryGet(i, out var agent) || !agent.isBoss) continue;
                    var lane = pos - Flat(agent.position);
                    if (lane.sqrMagnitude < 0.01f) break;
                    var side = Vector3.Cross(Vector3.up, lane.normalized);
                    if (Vector3.Dot(side, forward) < 0f) side = -side; // keep the way it is already heading
                    avoid += side * 5f;
                    danger += 5f;
                    _escape = true;
                    break;
                }
            }
        }

        Vector3 PickupPull(Vector3 pos)
        {
            if (_pickups == null) return Vector3.zero;
            var best = Vector3.zero;
            var bestScore = 0f;
            for (var i = 0; i < _pickups.Capacity; i++)
            {
                if (!_pickups.TryGet(i, out var type, out _, out var position, out var homing) || homing) continue;
                var value = type switch
                {
                    PickupType.XpBarrel => 3f,
                    PickupType.GoldBarrel => 2.5f,
                    PickupType.Powerup => 3f,
                    PickupType.Infamy => 1f,
                    PickupType.Gold => 0.8f,
                    _ => 0.4f, // loose XP crystals too, when nothing better is near
                };
                var to = Flat(position) - pos;
                var d = to.magnitude;
                if (d > PickupRange || d < 0.5f || !PlayBounds.IsInsideStormSafeZone(position)) continue;
                var score = value / (1f + d * 0.15f);
                if (score > bestScore) { bestScore = score; best = to / d; }
            }
            return best;
        }

        // ---- islands, edges, getting stuck (as the trace pilot) --------------------------------------------------

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
            var left = Blocked(pos + Quaternion.Euler(0f, -60f, 0f) * forward * 6f);
            var right = Blocked(pos + Quaternion.Euler(0f, 60f, 0f) * forward * 6f);
            _reverseSteer = left && !right ? 1f : !left && right ? -1f : (_steerSign == 0f ? 1f : -_steerSign);
            _reverseUntil = t + 1.4f;
            _graceUntil = _reverseUntil + 3f;
            return true;
        }

        Vector3 FindFreeDirection(Vector3 pos, Vector3 dir)
        {
            for (var k = 0; k <= 7; k++)
                for (var side = 0; side < (k == 0 ? 1 : 2); side++)
                {
                    var sign = side == 0 ? 1f : -1f;
                    if (_steerSign < 0f) sign = -sign;
                    var candidate = Quaternion.Euler(0f, sign * k * 25f, 0f) * dir;
                    if (!Blocked(pos + candidate * 5f) && !Blocked(pos + candidate * 10f)) return candidate;
                }
            return dir;
        }

        bool Blocked(Vector3 point) => IslandObstacles.IsPositionBlocked(point, _ship.IslandCollisionRadius) || !PlayBounds.IsInside(point, 2f);

        static Vector3 EdgePush(Vector3 pos)
        {
            if (!PlayBounds.IsConfigured) return Vector3.zero;
            var center = Flat(PlayBounds.Center);
            var halfX = (PlayBounds.HalfExtentX - PlayBounds.StormEdgeMargin) * PlayBounds.StormSafeScale;
            var halfZ = (PlayBounds.HalfExtentZ - PlayBounds.StormEdgeMargin) * PlayBounds.StormSafeScale;
            var off = pos - center;
            var clearance = Mathf.Min(halfX - Mathf.Abs(off.x), halfZ - Mathf.Abs(off.z));
            if (clearance >= EdgeComfort) return Vector3.zero;
            return (center - pos).normalized * (2.5f * (1f - Mathf.Clamp(clearance, -EdgeComfort, EdgeComfort) / EdgeComfort));
        }

        /// <summary>What is near the ship right now: the two nearest enemies (edge distance), enemies within 4 m, enemy
        /// shots within 3 m and the hazard it is inside (measurement runs log this with every hit).</summary>
        public string DescribeThreats()
        {
            Resolve();
            if (_ship == null) return "";
            var pos = Flat(_ship.Position);
            string n1 = "-", n2 = "-";
            float d1 = 999f, d2 = 999f;
            var close = 0;
            if (_enemies != null && _enemies.Exists)
                for (var i = 0; i < _enemies.Capacity; i++)
                {
                    if (!_enemies.TryGet(i, out var a)) continue;
                    var d = (Flat(a.position) - pos).magnitude - a.radius;
                    if (d < 4f) close++;
                    if (d < d1) { d2 = d1; n2 = n1; d1 = d; n1 = a.enemyId; }
                    else if (d < d2) { d2 = d; n2 = a.enemyId; }
                }
            var shots = 0;
            if (_shots != null)
                for (var i = 0; i < _shots.Capacity; i++)
                    if (_shots.TryGet(i, out var shot) && (Flat(shot.position) - pos).sqrMagnitude < 9f) shots++;
            var hazard = "-";
            if (_hazards != null)
                for (var i = 0; i < _hazards.Capacity; i++)
                    if (_hazards.TryGet(i, out var h) && (Flat(h.center) - pos).magnitude < h.radius) { hazard = h.kind + (h.armed ? "!" : ""); break; }
            return $"{n1}@{d1:0.0} {n2}@{d2:0.0} close={close} shots={shots} hazard={hazard} goal={Goal}";
        }

        void Resolve()
        {
            if (_views != null) return;
            var game = Object.FindFirstObjectByType<V4Game>();
            if (game == null || game.Context == null) return;
            game.Context.TryGet(out _enemies);
            game.Context.TryGet(out _shots);
            game.Context.TryGet(out _hazards);
            game.Context.TryGet(out _boss);
            game.Context.TryGet(out _views);
            game.Context.TryGet(out _stage);
            _pickups = game.Build != null ? game.Build.Pickups : null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }

    /// <summary>
    /// The rest of playing like a person, outside the ship's fixed step (which stops during pauses): picks level-up cards
    /// (a sensible order — evolutions, new weapons until six, then the best weapon's upgrades — or none for a "weak"
    /// build), takes chests it can afford, holds E on the landmark it is heading for, and for a "strong" build is handed six
    /// weapons early. Used by human trace runs and by the console's autopilot.
    /// </summary>
    public sealed class HumanPlayer : MonoBehaviour
    {
        public enum BuildProfile { Average = 0, Weak = 1, Strong = 2 }


        public HumanPilot Pilot { get; private set; }
        public BuildProfile Profile;
        public float StrongBuildAtSeconds = 60f;
        // The strong build: the six always-unlocked weapons from the run's unlock config (no content ids in V4 code).
        static List<string> StrongWeapons(RunContext context) =>
            new List<string>(context?.Config?.Single<UnlockConfig>()?.starterWeaponIds ?? System.Array.Empty<string>());
        PlayerShipController _ship;
        IStageContent _stage;
        V4Game _game;
        int _modalFrames, _chestFrames;
        bool _strongGiven;

        public static HumanPlayer Install(PlayerShipController ship, BuildProfile profile)
        {
            var go = new GameObject("V4HumanPlayer");
            var player = go.AddComponent<HumanPlayer>();
            player._ship = ship;
            player.Profile = profile;
            player.Pilot = new HumanPilot(ship);
            ship.ConfigureBotControl(player.Pilot);
            if (ship.Damage != null) ship.Damage.Damaged += player.OnDamaged;
            return player;
        }

        /// <summary>Hits of 2+ damage: "time|damage|hull after|threats" (the trace recorder drains these into "hurt" lines).</summary>
        public readonly List<string> Hurts = new List<string>();

        void OnDamaged(float amount, bool crit)
        {
            if (amount < 2f || Hurts.Count >= 500 || _ship == null) return;
            var t = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            Hurts.Add($"{t:0.00}|{amount:0.0}|{_ship.Damage.CurrentHP:0}|{Pilot.DescribeThreats()}");
        }

        void OnDestroy()
        {
            if (_ship != null && _ship.Damage != null) _ship.Damage.Damaged -= OnDamaged;
        }

        public void Uninstall()
        {
            if (_ship != null) _ship.ConfigureBotControl(null);
            if (_stage != null) _stage.Interaction.TouchHolding = false;
            Destroy(gameObject);
        }

        void Update()
        {
            var run = RunManager.Instance;
            if (run == null || _ship == null) return;
            if (_game == null) { _game = FindFirstObjectByType<V4Game>(); if (_game != null && _game.Context != null) _game.Context.TryGet(out _stage); }
            if (run.IsRunEnded) return;

            if (Profile == BuildProfile.Strong && !_strongGiven && run.ElapsedSeconds >= StrongBuildAtSeconds && _game?.Build != null)
            {
                _strongGiven = true;
                _game.Build.Progression.DevSetWeapons(StrongWeapons(_game.Context));
            }
            RespondToLevelUp();
            RespondToChest(run);
            // Hold E on the landmark it is heading for (Beacon, Signal Buoy, Treasure Chart, Repair Wreck, Powder Cache).
            if (_stage != null)
                _stage.Interaction.TouchHolding = Pilot.GoalPoi >= 0 && _stage.PromptTarget == Pilot.GoalPoi && !run.IsPaused;
        }

        void RespondToLevelUp()
        {
            // M14.11: the level-up presenter's pick/skip API (was reflection on V3's LevelUpModal).
            RoguePirates.V4.Progression.IOfferChoice cards = null;
            _game?.Context?.TryGet(out cards);
            var offers = cards != null && cards.IsShowing ? cards.Shown : null;
            if (offers == null || offers.Count == 0) { _modalFrames = 0; return; }
            if (++_modalFrames < 20) return; // a person takes a moment to read the cards
            _modalFrames = 0;
            // Weak: keeps its one weapon (upgrades, items and Logbooks only), like a player who never adds guns.
            var pick = BestCard(offers);
            if (Profile == BuildProfile.Weak && offers[pick].Kind == RoguePirates.V4.Progression.OfferKind.NewWeapon) { cards.Skip(); return; }
            cards.Pick(pick);
        }

        int BestCard(IReadOnlyList<RoguePirates.V4.Progression.Offer> offers)
        {
            var weapons = _game?.Build?.Progression?.OwnedWeapons?.Count ?? 1;
            var best = 0;
            var bestScore = float.MinValue;
            for (var i = 0; i < offers.Count; i++)
            {
                var o = offers[i];
                var score = o.Kind switch
                {
                    RoguePirates.V4.Progression.OfferKind.WeaponEvolution => 100f,
                    RoguePirates.V4.Progression.OfferKind.NewWeapon => Profile == BuildProfile.Weak ? -100f : weapons < 6 ? 80f - weapons * 4f : 5f,
                    RoguePirates.V4.Progression.OfferKind.WeaponUpgrade => 50f + o.CurrentLevel * 2f,
                    RoguePirates.V4.Progression.OfferKind.NewRunItem => 45f,
                    RoguePirates.V4.Progression.OfferKind.Logbook => 35f + o.CurrentLevel,
                    _ => 10f,
                } + o.Rarity * 3f;
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        void RespondToChest(RunManager run)
        {
            if (_stage == null || run.PauseReason != PauseReason.POIChoice) { _chestFrames = 0; return; }
            if (++_chestFrames < 15) return;
            _chestFrames = 0;
            if (_stage.Module.CanAffordSalvageChest() && Profile != BuildProfile.Weak) _stage.Interaction.TouchPressed();
            else _stage.SkipChest();
        }
    }
}
