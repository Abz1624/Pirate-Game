using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using RoguePirates.V4;
using RoguePirates.V4.Config;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.World;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Spawning;
using RoguePirates.V4.Weapons;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace RoguePirates.V5.Net
{
    /// <summary>V5 M9c S0b: a measurement hook the server calls at the end of every net tick (inside its tick time).</summary>
    public interface INetServerProbe
    {
        void OnServerTick(NetServer server, bool worldMoved);
    }

    /// <summary>
    /// V5 M9c S0b: test-only server presets. A preset is taken only when <c>RP_ALLOW_TEST_PRESETS=1</c> is set in the
    /// server's environment, or by a test (<see cref="AllowForTests"/>); the V5 net assembly itself is compiled only in
    /// the Editor or with the V5_NET define, so no release build carries any of it.
    /// </summary>
    public static class NetTestPresets
    {
        public const string AllowVariable = "RP_ALLOW_TEST_PRESETS";
        public const string PresetVariable = "RP_TEST_PRESET";
        public const string Load200 = "load200";

        /// <summary>Tests only (reset on every play mode entry).</summary>
        public static bool AllowForTests { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => AllowForTests = false;

        public static bool Allowed => AllowForTests || Environment.GetEnvironmentVariable(AllowVariable) == "1";

        /// <summary>The preset the environment asks for, or null (none asked, or asked without the allow switch: refused).</summary>
        public static string Requested()
        {
            var preset = Environment.GetEnvironmentVariable(PresetVariable);
            if (string.IsNullOrEmpty(preset)) return null;
            if (!Allowed)
            {
                Debug.LogWarning($"[V5 Net] test preset '{preset}' refused: {AllowVariable}=1 is not set");
                return null;
            }
            return preset;
        }
    }

    /// <summary>
    /// V5 M9c S0b (4-6 h, plan item 4 of the review): the early go/no-go probe on the M9b server, before the multi-ship
    /// refactor. The <c>load200</c> test preset keeps about 200 enemies around the server's one real ship (invulnerable,
    /// a six-weapon late-game build firing for real, ambient spawning off, cards without a pause), and three synthetic
    /// ghost ships sail plausible paths near it, replaying the real ship's recorded effects (<see cref="NetFxRecorder"/>)
    /// a little later from their own positions. Per client (the real ship's and each ghost's view) it measures what a
    /// 4-ship match would send: the own snapshot (the real v5 size), other ships (11 B, 15 Hz near and 5 Hz far), the
    /// enemies in view (nearest <see cref="Settings.viewCap"/>, the device cap) delta-encoded against an acked baseline
    /// (<see cref="NetEnemyDelta"/>, the ack modelled one round trip late), the effects (salvos as one event, projectiles
    /// dead-reckoned) and the minimap's threats at 2 Hz; wire = payload + Netcode's 12 B a named message (as M9b). One
    /// per-snapshot byte budget is applied inside the server tick (the production-like cost); further budgets are
    /// replayed on the same captured snapshots after the tick, for what they would starve. Measurement only: nothing is
    /// sent; the real client still gets the v5 protocol.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class NetLoadProbe : MonoBehaviour, INetServerProbe
    {
        [Serializable]
        public sealed class Settings
        {
            public string name = "load200";
            public int targetEnemies = 200;
            public int ghosts = 3;
            public int viewCap = 108;
            public float rttMs = 270f;
            public bool weapons = true;
            public int weaponLevel = 8;
            public string[] weaponIds = { "swivel_guns", "deck_cannons", "broadside_battery", "mortar", "lightning_mast", "parrot_squadron" };
            public string[] enemyIds =
            {
                "deckhand_dinghy", "deckhand_dinghy", "cutthroat_skiff", "cutthroat_skiff", "rammer_raft", "shark", "shark", "swordfish",
                "manta_ray", "gunner_cog", "cannonboat", "bomb_boat", "drowned_harrier", "drowned_pursuer", "hammerhead", "boarding_barge",
            };
            /// <summary>Late-game hulls: with 200 enemies in reach the build's kill rate lands near the owner's late-game
            /// peak (4-7 kills a second in plan/playtest/sessions), not a wipe.</summary>
            public float enemyHpMultiplier = 8f;
            public float eliteShare = 0.05f;
            /// <summary>The budget applied inside the tick (wire bytes per snapshot per client; 0 = none).</summary>
            public int primaryBudget = 1250;
            /// <summary>Replayed after the tick on the same snapshots (0 = no cap).</summary>
            public int[] whatIfBudgets = { 0, 1000, 850, 700 };
            public float fxRadius = 80f;
            /// <summary>Measurement starts once the enemies reach this share of the target and this many seconds passed.</summary>
            public float warmupSeconds = 8f;
            public int seed = 2000;
            public string reportDir;
        }

        public Settings S { get; private set; }
        public NetFxRecorder Fx { get; private set; }
        public bool Measuring { get; private set; }
        public float MeasuredSeconds => Measuring ? Time.realtimeSinceStartup - _measureStart : 0f;
        public int ActiveEnemies => _enemies != null ? _enemies.ActiveCount : 0;
        public string LastReportPath { get; private set; }

        NetServer _server;
        V4Game _game;
        IEnemyWorldView _enemies;
        IEnemySpawnSink _sink;
        System.Random _rng;
        readonly List<int> _definitions = new List<int>();
        float _startedAt = -1f, _measureStart;
        uint _lastWorldTick;
        int _snapshotIndex;
        uint _lastSnapTick;
        float _nextReport;
        int _spawned, _spawnFailures, _killsAtStart;
        readonly List<float> _activeEnemies = new List<float>();
        readonly List<float> _primaryEncodeMs = new List<float>();
        readonly List<float> _probeTickMs = new List<float>();
        readonly Stopwatch _sw = new Stopwatch();
        long _ticksMeasured;
        float _measureStartRunTime;
        Dictionary<string, long> _callsAtStart = new Dictionary<string, long>();

        // ---- Ghost ships --------------------------------------------------------------------------------------------

        sealed class Ghost
        {
            public Vector3 position;
            public float angle, radius, angularSpeed, wobblePhase;
            public uint delayTicks;
        }

        readonly List<Ghost> _ghosts = new List<Ghost>();

        // ---- Start -----------------------------------------------------------------------------------------------------

        /// <summary>The preset from the environment (the Linux server); null when none is asked for or it is refused.</summary>
        public static NetLoadProbe StartFromEnvironment(NetServer server, V4Game game)
        {
            var preset = NetTestPresets.Requested();
            if (preset == null) return null;
            if (preset != NetTestPresets.Load200) { Debug.LogWarning($"[V5 Net] unknown test preset '{preset}'"); return null; }
            var s = new Settings { reportDir = Environment.GetEnvironmentVariable(NetServerHost.MetricsDirVariable) };
            if (Environment.GetEnvironmentVariable("RP_PROBE_WEAPONS") == "0") { s.weapons = false; s.name = "load200-noweapons"; }
            if (float.TryParse(Environment.GetEnvironmentVariable("RP_PROBE_RTT_MS"), NumberStyles.Float, CultureInfo.InvariantCulture, out var rtt) && rtt >= 0f) s.rttMs = rtt;
            return Attach(server, game, s);
        }

        public static NetLoadProbe Attach(NetServer server, V4Game game, Settings settings)
        {
            if (!NetTestPresets.Allowed) throw new InvalidOperationException("test presets are not allowed here (" + NetTestPresets.AllowVariable + ")");
            var probe = server.gameObject.AddComponent<NetLoadProbe>();
            probe.Begin(server, game, settings);
            return probe;
        }

        void Begin(NetServer server, V4Game game, Settings settings)
        {
            S = settings;
            _server = server;
            _game = game;
            _rng = new System.Random(settings.seed);
            game.Context.TryGet(out _enemies);
            game.Context.TryGet(out _sink);
            var ship = game.Shell.Player;
            ship.Damage.SetPlaytestInvulnerable(true);
            // Cards without a pause (the shared run's rule; the solo server would pause for them): level-ups apply as they come.
            game.Build.Progression.AutoLevelUp = () => { };
            var time = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            var experiment = game.Config.Single<ExperimentConfig>();
            if (experiment != null && !game.Config.TryApplyEdit(experiment.configId, nameof(ExperimentConfig.encounterSpawnEnabled), "false", time, out var error))
                Debug.LogWarning("[V5 Net] probe: ambient spawning not switched off: " + error);
            if (settings.weapons) EquipBuild(game);
            else game.Build.Progression.DevSetWeapons(new List<string>());

            Fx = new NetFxRecorder(() => _server.ServerTick, () => _server.Ship.Position);
            var context = game.Context;
            context.Replace<IWeaponEffects>(Fx);
            context.Replace<IProjectileViews>(Fx);
            context.Replace<IHostileShotViews>(Fx);
            context.Replace<IEnemyEffects>(Fx);
            context.Replace<IPickupViews>(Fx);
            context.Replace<IPowerupViews>(Fx);

            foreach (var id in settings.enemyIds)
            {
                var d = _sink != null ? _sink.FindDefinition(id) : -1;
                if (d >= 0 && !_sink.DefinitionIsBoss(d) && !_sink.DefinitionIsMiniboss(d)) _definitions.Add(d);
                else Debug.LogWarning($"[V5 Net] probe: enemy {id} not in the run database");
            }

            var radii = new[] { 25f, 45f, 65f, 35f, 55f };
            for (var g = 0; g < settings.ghosts; g++)
            {
                var r = radii[g % radii.Length];
                _ghosts.Add(new Ghost
                {
                    radius = r,
                    angle = g * 2.1f,
                    angularSpeed = (g % 2 == 0 ? 1f : -1f) * 9f / r, // about 9 m/s around the crew
                    wobblePhase = g * 1.3f,
                    delayTicks = (uint)(23 + 24 * g),
                });
            }

            foreach (var marker in Markers)
            {
                var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, marker, 1);
                _recorders.Add((marker, recorder, new List<float>()));
            }
            _encoders.Add(new Encoder(this, settings.primaryBudget));
            foreach (var b in settings.whatIfBudgets) _encoders.Add(new Encoder(this, b));
            server.Probe = this;
            _startedAt = Time.realtimeSinceStartup;
            _nextReport = _startedAt + 10f;
            Debug.Log($"[V5 Net] TEST PRESET {settings.name}: {settings.targetEnemies} enemies, {settings.ghosts} ghost ships, view cap {settings.viewCap}, " +
                      $"weapons {(settings.weapons ? string.Join(",", settings.weaponIds) + " L" + settings.weaponLevel : "none")}, rtt {settings.rttMs} ms, " +
                      $"budgets {settings.primaryBudget} (in tick) + {string.Join("/", settings.whatIfBudgets)}; never in a release build");
        }

        void EquipBuild(V4Game game)
        {
            var progression = game.Build.Progression;
            var ids = new List<string>();
            foreach (var id in S.weaponIds)
            {
                var known = false;
                foreach (var w in game.Config.All<WeaponConfig>()) if (w.weaponId == id) { known = true; break; }
                if (known) ids.Add(id); else Debug.LogWarning("[V5 Net] probe: weapon " + id + " unknown");
            }
            progression.DevSetWeapons(ids);
            foreach (var id in ids)
            {
                var index = progression.Loadout.IndexOf(id);
                for (var guard = 0; index >= 0 && progression.WeaponLevel(id) < S.weaponLevel && guard < 64; guard++) progression.DevRankUp(index);
            }
        }

        // ---- The tick (inside the server's tick time) -------------------------------------------------------------------

        public void OnServerTick(NetServer server, bool worldMoved)
        {
            if (!worldMoved) return;
            _sw.Restart();
            var dt = NetProtocol.TickSeconds;
            var center = server.Ship.Position;
            foreach (var g in _ghosts)
            {
                g.angle += g.angularSpeed * dt;
                var r = g.radius + 10f * Mathf.Sin(g.wobblePhase + g.angle * 0.7f);
                var p = center + new Vector3(Mathf.Cos(g.angle), 0f, Mathf.Sin(g.angle)) * r;
                g.position = PlayBounds.Clamp(p, 4f);
            }
            TopUp(center);
            if (!Measuring && Time.realtimeSinceStartup - _startedAt >= S.warmupSeconds && _enemies.ActiveCount >= S.targetEnemies * 0.95f) StartMeasuring();
            if (Measuring && server.ServerTick % NetProtocol.SnapshotEveryTicks == 0)
            {
                CaptureSnapshot(server);
                var t = Stopwatch.StartNew();
                _encoders[0].Process(_capture);
                _primaryEncodeMs.Add((float)t.Elapsed.TotalMilliseconds);
                _pendingCaptures.Add(_capture.Clone());
                _activeEnemies.Add(_enemies.ActiveCount);
            }
            if (Measuring) { _probeTickMs.Add((float)_sw.Elapsed.TotalMilliseconds); _ticksMeasured++; }
        }

        void StartMeasuring()
        {
            Measuring = true;
            _measureStart = Time.realtimeSinceStartup;
            _server.Metrics.Reset();
            _measureStartRunTime = RunManager.Instance != null ? RunManager.Instance.ElapsedSeconds : 0f;
            _killsAtStart = RunManager.Instance != null ? RunManager.Instance.KillCount : 0;
            _callsAtStart = new Dictionary<string, long>(Fx.Calls);
            _lastSnapTick = _server.ServerTick;
            Debug.Log($"[V5 Net] probe measuring from now ({_enemies.ActiveCount} enemies, {Time.realtimeSinceStartup - _startedAt:0.0} s after the start)");
        }

        void TopUp(Vector3 center)
        {
            if (_sink == null || _definitions.Count == 0) return;
            var deficit = Mathf.Min(S.targetEnemies - _enemies.ActiveCount, 8);
            for (var i = 0; i < deficit; i++)
            {
                Vector3 p = center;
                var ok = false;
                for (var attempt = 0; attempt < 4 && !ok; attempt++)
                {
                    var a = (float)(_rng.NextDouble() * Math.PI * 2);
                    var d = 28f + (float)_rng.NextDouble() * 16f; // inside the 45 m leash
                    p = PlayBounds.Clamp(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * d, 3f);
                    ok = PlayBounds.IsOpenWater(p, 3f);
                }
                var def = _definitions[_rng.Next(_definitions.Count)];
                var affix = _rng.NextDouble() < S.eliteShare ? ConfigEnums.EliteAffix.Armored : ConfigEnums.EliteAffix.None;
                var slot = _sink.Spawn(def, p, affix, 1f, S.enemyHpMultiplier, 1f);
                if (slot >= 0) _spawned++; else _spawnFailures++;
            }
        }

        // ---- Capture -------------------------------------------------------------------------------------------------

        public struct CapEnemy
        {
            public int id, def;
            public Vector3 position, velocity;
            public float hpFraction;
            public byte flags;
        }

        public struct FxItem
        {
            public byte source; // ship index (0 real, 1..n ghosts) or NetFxRecorder.World
            public Vector3 position;
            public short bytes;
            public byte kind;
        }

        public sealed class Capture
        {
            public int index;
            public uint tick;
            public bool full;
            public bool threatsDue;
            public int shipBytes;
            public readonly List<Vector3> ships = new List<Vector3>();
            public CapEnemy[] enemies = new CapEnemy[300];
            public int enemyCount;
            public readonly List<FxItem> fx = new List<FxItem>(512);

            public Capture Clone()
            {
                var c = new Capture { index = index, tick = tick, full = full, threatsDue = threatsDue, shipBytes = shipBytes, enemyCount = enemyCount };
                c.ships.AddRange(ships);
                if (c.enemies.Length < enemyCount) c.enemies = new CapEnemy[enemies.Length];
                Array.Copy(enemies, c.enemies, enemyCount);
                c.fx.AddRange(fx);
                return c;
            }
        }

        readonly Capture _capture = new Capture();
        readonly List<Capture> _pendingCaptures = new List<Capture>();
        readonly List<Encoder> _encoders = new List<Encoder>();

        void CaptureSnapshot(NetServer server)
        {
            var c = _capture;
            c.index = _snapshotIndex++;
            c.tick = server.ServerTick;
            c.full = c.index % NetProtocol.FarEveryNthSnapshot == 0;
            c.threatsDue = c.index % 15 == 0 || c.index % 15 == 8; // 2 Hz at 15 snapshots a second
            c.shipBytes = server.LastShipSnapshotBytes > 0 ? server.LastShipSnapshotBytes : 70;
            c.ships.Clear();
            c.ships.Add(server.Ship.Position);
            foreach (var g in _ghosts) c.ships.Add(g.position);
            c.enemyCount = 0;
            for (var i = 0; i < _enemies.Capacity; i++)
            {
                if (!_enemies.TryGet(i, out var e)) continue;
                if (c.enemyCount == c.enemies.Length) Array.Resize(ref c.enemies, c.enemies.Length * 2);
                byte flags = 0;
                if (e.isElite) flags |= NetProtocol.EnemyElite;
                if (e.isBoss) flags |= NetProtocol.EnemyBoss;
                c.enemies[c.enemyCount++] = new CapEnemy
                {
                    id = e.unitId, def = e.definitionIndex, position = e.position, velocity = e.velocity,
                    hpFraction = e.maxHp > 0f ? e.hp / e.maxHp : 0f, flags = flags,
                };
            }
            c.fx.Clear();
            var from = _lastSnapTick;
            var to = server.ServerTick;
            _lastSnapTick = to;
            CollectFx(c, 0, from, to, 0, c.ships[0]);
            for (var g = 0; g < _ghosts.Count; g++)
            {
                var d = _ghosts[g].delayTicks;
                if (from > d) CollectFx(c, (byte)(g + 1), from - d, to - d, 0, c.ships[g + 1]);
            }
            CollectFx(c, NetFxRecorder.World, from, to, NetFxRecorder.World, Vector3.zero);
        }

        readonly Dictionary<long, int> _salvos = new Dictionary<long, int>();
        readonly HashSet<int> _summonsMoved = new HashSet<int>();
        readonly List<(uint tick, Vector3 at, int index)> _drops = new List<(uint, Vector3, int)>();
        readonly List<int> _firedPending = new List<int>();

        /// <summary>The recorder's events of ticks (from, to] from <paramref name="recorded"/> as items of <paramref name="source"/>
        /// placed around <paramref name="at"/> (ship events) or where they happened (world events).</summary>
        void CollectFx(Capture c, byte source, uint from, uint to, byte recorded, Vector3 at)
        {
            _salvos.Clear();
            _drops.Clear();
            _summonsMoved.Clear();
            _firedPending.Clear();
            var events = Fx.Events;
            // Events are in tick order: start at the first one after `from`.
            int lo = 0, hi = events.Count;
            while (lo < hi) { var mid = (lo + hi) >> 1; if (events[mid].tick <= from) lo = mid + 1; else hi = mid; }
            for (var i = lo; i < events.Count; i++)
            {
                if (events[i].tick > to) break;
                var e = events[i];
                if (e.tick <= from || e.tick > to || e.source != recorded) continue;
                var pos = recorded == NetFxRecorder.World ? e.offset : at + e.offset;
                short bytes;
                switch (e.kind)
                {
                    case NetFxRecorder.Kind.ProjectileSpawn:
                    {
                        var key = ((long)e.weapon << 32) | e.tick;
                        if (_salvos.TryGetValue(key, out var index))
                        {
                            var item = c.fx[index];
                            item.bytes += 1; // one more heading in the salvo
                            c.fx[index] = item;
                            continue;
                        }
                        _salvos[key] = c.fx.Count;
                        bytes = 12; // kind, weapon, x/z, tick, speed, count, first id (2), one heading
                        break;
                    }
                    case NetFxRecorder.Kind.Fired: _firedPending.Add(i); continue; // folded into its salvo, or sent alone below
                    case NetFxRecorder.Kind.ProjectileTurn: bytes = 9; break;
                    case NetFxRecorder.Kind.ProjectileEnd: bytes = 3; break;
                    case NetFxRecorder.Kind.Directional: bytes = 9; break;
                    case NetFxRecorder.Kind.Effect: bytes = 8; break;
                    case NetFxRecorder.Kind.TimedRing: bytes = 9; break;
                    case NetFxRecorder.Kind.SummonSpawn: bytes = 9; break;
                    case NetFxRecorder.Kind.SummonEnd: bytes = 3; break;
                    case NetFxRecorder.Kind.SummonMove: _summonsMoved.Add(e.handle); continue;
                    case NetFxRecorder.Kind.HostileShot: bytes = 8; break;
                    case NetFxRecorder.Kind.HostileSplash: bytes = 7; break;
                    case NetFxRecorder.Kind.EnemyExplosion: bytes = 7; break;
                    case NetFxRecorder.Kind.Pickup:
                    {
                        // A kill's drop as one event (kind, x/z, tick, first id (2), then a type-and-value byte per pickup)
                        // plus each pickup's collection later (a 2-byte id step in a batched list): pickups of one tick
                        // within 3 m are one drop.
                        var merged = false;
                        for (var g = 0; g < _drops.Count; g++)
                        {
                            var (tick, at0, index) = _drops[g];
                            if (tick != e.tick || (at0 - pos).sqrMagnitude > 9f) continue;
                            var item = c.fx[index];
                            item.bytes += 1 + 2;
                            c.fx[index] = item;
                            merged = true;
                            break;
                        }
                        if (merged) continue;
                        _drops.Add((e.tick, pos, c.fx.Count));
                        bytes = 8 + 1 + 2;
                        break;
                    }
                    case NetFxRecorder.Kind.Powerup: bytes = 11; break;
                    default: continue;
                }
                c.fx.Add(new FxItem { source = source, position = pos, bytes = bytes, kind = (byte)e.kind });
            }
            foreach (var i in _firedPending)
            {
                var e = events[i];
                if (_salvos.ContainsKey(((long)e.weapon << 32) | e.tick)) continue;
                c.fx.Add(new FxItem { source = source, position = recorded == NetFxRecorder.World ? e.offset : at + e.offset, bytes = 9, kind = (byte)NetFxRecorder.Kind.Fired });
            }
            // Summons (parrots, kegs) move as small entities: id, a 2-byte step and a heading (4 B each a snapshot).
            if (_summonsMoved.Count > 0)
                c.fx.Add(new FxItem { source = source, position = at, bytes = (short)(2 + 4 * _summonsMoved.Count), kind = (byte)NetFxRecorder.Kind.SummonMove });
        }

        // ---- After the tick: what-if budgets, trimming, reports ----------------------------------------------------------

        void LateUpdate()
        {
            if (_pendingCaptures.Count > 0)
            {
                foreach (var c in _pendingCaptures)
                    for (var k = 1; k < _encoders.Count; k++) _encoders[k].Process(c);
                _pendingCaptures.Clear();
            }
            Fx?.Trim();
            if (Measuring)
            {
                _frames++;
                foreach (var r in _recorders) if (r.recorder.Valid) NetMetrics.AddCapped(r.ms, r.recorder.LastValue / 1e6f, 200000);
            }
            if (Time.realtimeSinceStartup >= _nextReport)
            {
                _nextReport = Time.realtimeSinceStartup + 10f;
                if (Measuring) WriteReport("periodic");
            }
        }

        /// <summary>The run's own profiler markers (per frame): what one ship's build costs against the enemy world. They
        /// record in the Editor and development players; a release player reads 0.</summary>
        static readonly string[] Markers =
        {
            "V4.Frame.Weapons", "V4.Enemies.PlayerProjectiles", "V4.Enemies.Frame", "V4.Enemies.Simulation", "V4.Sim.Behavior",
            "V4.Sim.Move", "V4.Sim.Combat", "V4.Sim.Overlaps", "V4.Enemies.Shots", "V4.Frame.Items",
        };
        readonly List<(string name, Unity.Profiling.ProfilerRecorder recorder, List<float> ms)> _recorders = new List<(string, Unity.Profiling.ProfilerRecorder, List<float>)>();
        int _frames;

        void OnDestroy()
        {
            foreach (var r in _recorders) r.recorder.Dispose();
            if (_server != null && ReferenceEquals(_server.Probe, this)) _server.Probe = null;
        }

        // ---- The per-client encoder ----------------------------------------------------------------------------------

        sealed class Window
        {
            public long own, ships, enemies, fxOwn, fxOthers, threats, framing;
            public long Payload => own + ships + enemies + fxOwn + fxOthers + threats;
            public long Wire => Payload + framing;
        }

        sealed class ClientStream
        {
            public readonly EnemyTable previous = new EnemyTable();
            public readonly EnemyTable target = new EnemyTable();
            public readonly EnemyTable[] ring = new EnemyTable[64];
            public readonly float[] sentAt = new float[64];
            public readonly int[] ringIndex = new int[64];
            public readonly Dictionary<int, int> lastFresh = new Dictionary<int, int>();
            public readonly Dictionary<int, int> firstSeen = new Dictionary<int, int>();
            public readonly List<(FxItem item, int age)> pending = new List<(FxItem, int)>();
            public Window window = new Window();
            public int windowSnapshots;
            public readonly List<Window> windows = new List<Window>();
            public readonly List<float> frameWire = new List<float>();
            public readonly List<float> enemyBytes = new List<float>();
            public readonly List<float> view = new List<float>();
            public readonly List<float> refreshed = new List<float>();
            public readonly List<float> nearAgeMs = new List<float>();
            public readonly List<float> closeAgeMs = new List<float>();
            public readonly List<float> ownFxDelayMs = new List<float>();
            public readonly List<float> othersFxDelayMs = new List<float>();
            public long fxSent, fxDeferred, fxDropped, nearStale, nearSamples, notShown, overBudget, baselineMissing;
            public float maxBaselineAgeMs;
            public readonly List<float> baselineAgeMs = new List<float>();

            public ClientStream() { for (var i = 0; i < ring.Length; i++) { ring[i] = new EnemyTable(); ringIndex[i] = -1; } }
        }

        struct Candidate
        {
            public int priority, bytes, kind, index; // kind 0 enemy, 1 own fx, 2 others' fx, 3 threats
        }

        sealed class Encoder
        {
            public readonly int Budget;
            readonly NetLoadProbe _p;
            readonly List<ClientStream> _clients = new List<ClientStream>();
            readonly List<(float d2, int i)> _view = new List<(float, int)>(320);
            readonly List<Candidate> _candidates = new List<Candidate>(512);
            readonly List<(FxItem item, int age)> _fxCandidates = new List<(FxItem, int)>(512);
            readonly QEnemy[] _q = new QEnemy[400];
            readonly int[] _prevIndex = new int[400];
            readonly bool[] _include = new bool[400];
            bool[] _fxInclude = new bool[1024];
            readonly NetWriter _w = new NetWriter(4096);
            readonly List<QEnemy> _targetList = new List<QEnemy>(256);
            readonly HashSet<int> _inView = new HashSet<int>();
            static readonly CandidateOrder Order = new CandidateOrder();

            public IReadOnlyList<ClientStream> Clients => _clients;

            public Encoder(NetLoadProbe probe, int budget) { _p = probe; Budget = budget; }

            const float SnapshotMs = 1000f * NetProtocol.SnapshotEveryTicks / NetProtocol.TickRate;
            const int Framing = NetProtocol.NamedMessageOverheadBytes;

            public void Process(Capture c)
            {
                while (_clients.Count < c.ships.Count) _clients.Add(new ClientStream());
                var now = c.index * SnapshotMs / 1000f; // the server's snapshot clock
                for (var k = 0; k < c.ships.Count; k++) Encode(c, k, _clients[k], now);
            }

            void Encode(Capture c, int k, ClientStream cs, float now)
            {
                var s = _p.S;
                var origin = c.ships[k];
                var near2 = NetProtocol.NearRadius * NetProtocol.NearRadius;
                var far2 = NetProtocol.FarRadius * NetProtocol.FarRadius;
                const float close2 = 30f * 30f;

                // The acked baseline: the newest snapshot whose ack is back (one round trip after it was sent).
                EnemyTable baseline = null;
                var bestIndex = -1;
                var rtt = s.rttMs / 1000f;
                for (var r = 0; r < cs.ring.Length; r++)
                {
                    if (cs.ringIndex[r] < 0 || cs.sentAt[r] + rtt > now + 1e-4f || cs.ringIndex[r] <= bestIndex) continue;
                    bestIndex = cs.ringIndex[r];
                    baseline = cs.ring[r];
                }
                if (bestIndex >= 0) cs.baselineAgeMs.Add((c.index - bestIndex) * SnapshotMs);
                else if (c.index > 0) cs.baselineMissing++;

                // The view: the nearest enemies within the far band, at most the device cap.
                _view.Clear();
                for (var i = 0; i < c.enemyCount; i++)
                {
                    var e = c.enemies[i];
                    var dx = e.position.x - origin.x;
                    var dz = e.position.z - origin.z;
                    var d2 = dx * dx + dz * dz;
                    if (d2 <= far2) _view.Add((d2, i));
                }
                _view.Sort((a, b) => a.d2.CompareTo(b.d2));
                var viewCount = Mathf.Min(_view.Count, s.viewCap);
                _inView.Clear();

                // Fixed parts: the own snapshot, other ships, the enemy message's header and its removals.
                var ownBytes = c.shipBytes;
                var shipsInMsg = 0;
                for (var j = 0; j < c.ships.Count; j++)
                {
                    if (j == k) continue;
                    var d = c.ships[j] - origin;
                    if (d.x * d.x + d.z * d.z <= near2 || c.full) shipsInMsg++;
                }
                var shipsBytes = shipsInMsg > 0 ? 2 + 11 * shipsInMsg : 0;
                var messages = 1 + (shipsInMsg > 0 ? 1 : 0) + 1;

                _candidates.Clear();
                for (var v = 0; v < viewCount; v++)
                {
                    var e = c.enemies[_view[v].i];
                    var q = NetEnemyDelta.Quantize(e.id, e.def, e.position, e.velocity, e.hpFraction, e.flags);
                    _q[v] = q;
                    _include[v] = false;
                    _inView.Add(e.id);
                    if (!cs.firstSeen.ContainsKey(e.id)) cs.firstSeen[e.id] = c.index;
                    var pi = cs.previous.IndexOf(e.id);
                    _prevIndex[v] = pi;
                    if (pi >= 0 && cs.previous.Items[pi].Equals(q)) { _include[v] = true; continue; } // unchanged: free
                    var d2 = _view[v].d2;
                    var nearBand = d2 <= near2;
                    if (!nearBand && !c.full) continue; // far band: every 3rd snapshot
                    var bi = baseline != null ? baseline.IndexOf(e.id) : -1;
                    var cost = 1 + NetEnemyDelta.ChangeBytes(q, bi >= 0, bi >= 0 ? baseline.Items[bi] : default);
                    var age = c.index - (cs.lastFresh.TryGetValue(e.id, out var lf) ? lf : cs.firstSeen[e.id]);
                    var band = d2 <= close2 ? 300 : nearBand ? 200 : 100;
                    _candidates.Add(new Candidate { kind = 0, index = v, bytes = cost, priority = band + 40 * age });
                }
                var removals = 0;
                for (var i = 0; i < cs.previous.Count; i++) if (!_inView.Contains(cs.previous.Items[i].id)) removals++;

                // Effects: this snapshot's plus those deferred (own always; others' and the world's within the radius).
                _fxCandidates.Clear();
                foreach (var pend in cs.pending) _fxCandidates.Add(pend);
                cs.pending.Clear();
                var fr2 = s.fxRadius * s.fxRadius;
                foreach (var f in c.fx)
                {
                    if (f.source != k)
                    {
                        var d = f.position - origin;
                        if (d.x * d.x + d.z * d.z > fr2) continue;
                    }
                    _fxCandidates.Add((f, 0));
                }
                for (var i = 0; i < _fxCandidates.Count; i++)
                {
                    var (f, age) = _fxCandidates[i];
                    var own = f.source == k;
                    _candidates.Add(new Candidate { kind = own ? 1 : 2, index = i, bytes = f.bytes, priority = (own ? 1000 : 150) + 40 * age });
                }
                var threatCount = 0;
                if (c.threatsDue)
                {
                    threatCount = c.enemyCount - viewCount;
                    if (threatCount > 0) _candidates.Add(new Candidate { kind = 3, bytes = 3 + 2 * threatCount + Framing, priority = 50 });
                }

                // Fill: fixed first, then by priority while it fits the budget (0: no cap).
                var fixedWire = ownBytes + Framing + (shipsBytes > 0 ? shipsBytes + Framing : 0) + NetEnemyDelta.HeaderBytes + Framing + 2 * removals;
                var remaining = Budget > 0 ? Budget - fixedWire : int.MaxValue;
                _candidates.Sort(Order);
                var fxHeader = false;
                if (_fxInclude.Length < _fxCandidates.Count) _fxInclude = new bool[_fxCandidates.Count * 2];
                for (var i = 0; i < _fxCandidates.Count; i++) _fxInclude[i] = false;
                var threatsSent = false;
                foreach (var cand in _candidates)
                {
                    var cost = cand.bytes;
                    if ((cand.kind == 1 || cand.kind == 2) && !fxHeader) cost += 4 + Framing;
                    if (cost > remaining) continue;
                    remaining -= cost;
                    switch (cand.kind)
                    {
                        case 0: _include[cand.index] = true; break;
                        case 1: case 2: fxHeader = true; _fxInclude[cand.index] = true; break;
                        case 3: threatsSent = true; break;
                    }
                }

                // The client's table after this snapshot: unchanged and refreshed entries now, the rest as it holds them.
                _targetList.Clear();
                var shown = 0;
                for (var v = 0; v < viewCount; v++)
                {
                    if (_include[v]) { _targetList.Add(_q[v]); shown++; }
                    else if (_prevIndex[v] >= 0) _targetList.Add(cs.previous.Items[_prevIndex[v]]);
                    else cs.notShown++;
                }
                cs.target.Clear();
                foreach (var q in _targetList) cs.target.Add(q);
                cs.target.SortById();
                var snapshotId = (ushort)(c.index % 65535);
                cs.target.SnapshotId = snapshotId;
                _w.Clear();
                var enemyPayload = NetEnemyDelta.Write(_w, snapshotId, baseline, cs.previous, cs.target, NetEnemyDelta.Grid(origin.x), NetEnemyDelta.Grid(origin.z));

                // Ages: an enemy is fresh when the client's value is the server's now.
                for (var v = 0; v < viewCount; v++)
                {
                    var id = _q[v].id;
                    var ti = cs.target.IndexOf(id);
                    var fresh = ti >= 0 && cs.target.Items[ti].Equals(_q[v]);
                    if (fresh) cs.lastFresh[id] = c.index;
                    var since = cs.lastFresh.TryGetValue(id, out var lf) ? lf : cs.firstSeen[id];
                    var ageMs = (c.index - since) * SnapshotMs;
                    var d2 = _view[v].d2;
                    if (d2 <= near2)
                    {
                        NetMetrics.AddCapped(cs.nearAgeMs, ageMs, 200000);
                        cs.nearSamples++;
                        if (!fresh) cs.nearStale++;
                    }
                    if (d2 <= close2) NetMetrics.AddCapped(cs.closeAgeMs, ageMs, 200000);
                }
                // Forget enemies that left the view (a return starts afresh).
                if (cs.firstSeen.Count > 4 * s.viewCap)
                {
                    var gone = new List<int>();
                    foreach (var id in cs.firstSeen.Keys) if (!_inView.Contains(id)) gone.Add(id);
                    foreach (var id in gone) { cs.firstSeen.Remove(id); cs.lastFresh.Remove(id); }
                }

                // Effects sent, deferred (up to 7 snapshots, about half a second) or dropped.
                long fxOwn = 0, fxOthers = 0;
                for (var i = 0; i < _fxCandidates.Count; i++)
                {
                    var (f, age) = _fxCandidates[i];
                    var own = f.source == k;
                    if (_fxInclude[i])
                    {
                        if (own) { fxOwn += f.bytes; cs.ownFxDelayMs.Add(age * SnapshotMs); }
                        else { fxOthers += f.bytes; cs.othersFxDelayMs.Add(age * SnapshotMs); }
                        cs.fxSent++;
                    }
                    else if (age < 7) { cs.pending.Add((f, age + 1)); cs.fxDeferred++; }
                    else cs.fxDropped++;
                }
                if (fxHeader) { fxOthers += 4; messages++; }
                var threatsBytes = threatsSent ? 3 + 2 * threatCount : 0;
                if (threatsSent) messages++;

                var w = cs.window;
                w.own += ownBytes;
                w.ships += shipsBytes;
                w.enemies += enemyPayload;
                w.fxOwn += fxOwn;
                w.fxOthers += fxOthers;
                w.threats += threatsBytes;
                var framing = messages * Framing;
                w.framing += framing;
                var frame = ownBytes + shipsBytes + enemyPayload + fxOwn + fxOthers + threatsBytes + framing;
                if (Budget > 0 && frame > Budget) cs.overBudget++;
                NetMetrics.AddCapped(cs.frameWire, frame, 100000);
                NetMetrics.AddCapped(cs.enemyBytes, enemyPayload, 100000);
                NetMetrics.AddCapped(cs.view, viewCount, 100000);
                NetMetrics.AddCapped(cs.refreshed, shown, 100000);
                if (++cs.windowSnapshots == NetProtocol.TickRate / NetProtocol.SnapshotEveryTicks)
                {
                    cs.windows.Add(cs.window);
                    cs.window = new Window();
                    cs.windowSnapshots = 0;
                }

                // Keep this snapshot as the client's table and in the history for later baselines.
                var slot = c.index % cs.ring.Length;
                cs.ring[slot].CopyFrom(cs.target);
                cs.sentAt[slot] = now;
                cs.ringIndex[slot] = c.index;
                cs.previous.CopyFrom(cs.target);
            }

            sealed class CandidateOrder : IComparer<Candidate>
            {
                public int Compare(Candidate a, Candidate b) => b.priority.CompareTo(a.priority);
            }
        }

        // ---- Report ------------------------------------------------------------------------------------------------------

        public string WriteReport(string kind)
        {
            try
            {
                var dir = string.IsNullOrEmpty(S.reportDir) ? Path.Combine(Application.dataPath, "..", "Temp", "V5Net") : S.reportDir;
                Directory.CreateDirectory(dir);
                var path = Path.Combine(dir, "probe-" + S.name + ".json");
                File.WriteAllText(path, Json(kind));
                LastReportPath = Path.GetFullPath(path);
                if (kind != "periodic") Debug.Log("[V5 Net] probe report: " + LastReportPath);
                return LastReportPath;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[V5 Net] probe report not written: " + e.Message);
                return null;
            }
        }

        static string F(double v) => v.ToString("0.###", CultureInfo.InvariantCulture);

        static void Stats(StringBuilder sb, string name, List<float> values, string indent)
        {
            sb.Append(indent).Append('"').Append(name).Append("\": {\"count\": ").Append(values.Count);
            double sum = 0; foreach (var v in values) sum += v;
            sb.Append(", \"mean\": ").Append(F(values.Count > 0 ? sum / values.Count : 0));
            sb.Append(", \"p50\": ").Append(F(NetMetrics.Percentile(values, 50f)));
            sb.Append(", \"p95\": ").Append(F(NetMetrics.Percentile(values, 95f)));
            sb.Append(", \"p99\": ").Append(F(NetMetrics.Percentile(values, 99f)));
            sb.Append(", \"max\": ").Append(F(NetMetrics.Max(values))).Append('}');
        }

        string Json(string kind)
        {
            var sb = new StringBuilder();
            var m = _server.Metrics;
            var seconds = MeasuredSeconds;
            var run = RunManager.Instance;
            sb.Append("{\n");
            sb.Append("  \"report\": \"V5 M9c S0b load probe\",\n");
            sb.Append("  \"kind\": \"").Append(kind).Append("\",\n");
            sb.Append("  \"utc\": \"").Append(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)).Append("\",\n");
            sb.Append("  \"editor\": ").Append(Application.isEditor ? "true" : "false").Append(",\n");
            sb.Append("  \"platform\": \"").Append(Application.platform).Append("\",\n");
#if ENABLE_IL2CPP
            sb.Append("  \"scriptingBackend\": \"IL2CPP\",\n");
#else
            sb.Append("  \"scriptingBackend\": \"Mono\",\n");
#endif
            sb.Append("  \"preset\": \"").Append(S.name).Append("\",\n");
            sb.Append("  \"settings\": {\"targetEnemies\": ").Append(S.targetEnemies).Append(", \"ghosts\": ").Append(S.ghosts)
              .Append(", \"viewCap\": ").Append(S.viewCap).Append(", \"rttMs\": ").Append(F(S.rttMs)).Append(", \"weapons\": ").Append(S.weapons ? "true" : "false")
              .Append(", \"weaponLevel\": ").Append(S.weaponLevel).Append(", \"weaponIds\": \"").Append(string.Join(",", S.weaponIds))
              .Append("\", \"enemyHpMultiplier\": ").Append(F(S.enemyHpMultiplier)).Append(", \"fxRadius\": ").Append(F(S.fxRadius))
              .Append(", \"nearRadius\": ").Append(F(NetProtocol.NearRadius)).Append(", \"farRadius\": ").Append(F(NetProtocol.FarRadius)).Append("},\n");
            sb.Append("  \"measuredSeconds\": ").Append(F(seconds)).Append(",\n");
            sb.Append("  \"runSecondsMeasured\": ").Append(F(run != null ? run.ElapsedSeconds - _measureStartRunTime : 0)).Append(",\n");
            sb.Append("  \"snapshots\": ").Append(_snapshotIndex).Append(",\n");
            sb.Append("  \"kills\": ").Append(run != null ? run.KillCount - _killsAtStart : 0).Append(",\n");
            sb.Append("  \"spawned\": ").Append(_spawned).Append(", \"spawnFailures\": ").Append(_spawnFailures).Append(", \"quantizeClamps\": ").Append(NetEnemyDelta.Clamps).Append(",\n");
            sb.Append("  \"level\": ").Append(_game != null && _game.Build != null ? _game.Build.Progression.Level : 0).Append(",\n");
            Stats(sb, "activeEnemies", _activeEnemies, "  "); sb.Append(",\n");
            Stats(sb, "serverTickMs", m.TickMs, "  "); sb.Append(",\n");
            sb.Append("  \"serverTickMsMax\": ").Append(F(m.TickMsMax)).Append(", \"serverTicksMeasured\": ").Append(m.TicksMeasured).Append(",\n");
            Stats(sb, "probeTickMs", _probeTickMs, "  "); sb.Append(",\n");
            sb.Append("  \"framesMeasured\": ").Append(_frames).Append(",\n");
            sb.Append("  \"markerMsPerFrame\": {\n");
            for (var i = 0; i < _recorders.Count; i++)
            {
                Stats(sb, _recorders[i].name, _recorders[i].ms, "    ");
                double sum = 0; foreach (var v in _recorders[i].ms) sum += v;
                sb.Length -= 1; // reopen the stats object
                sb.Append(", \"msPerSecond\": ").Append(F(sum / Math.Max(1f, seconds))).Append('}').Append(i + 1 < _recorders.Count ? ",\n" : "\n");
            }
            sb.Append("  },\n");
            double tickSum = 0; foreach (var v in m.TickMs) tickSum += v;
            sb.Append("  \"serverTickMsPerSecond\": ").Append(F(tickSum / Math.Max(1f, seconds))).Append(",\n");
            Stats(sb, "primaryEncodeMsPerSnapshot", _primaryEncodeMs, "  "); sb.Append(",\n");
            // The real client's traffic (the v5 protocol) and the up estimate with the 2-byte baseline ack per input message.
            sb.Append("  \"realClient\": [");
            var first = true;
            foreach (var pair in m.ClientsForReport())
            {
                var t = pair.Value;
                sb.Append(first ? "\n" : ",\n").Append("    {\"clientId\": ").Append(pair.Key)
                  .Append(", \"downPayloadPerSecond\": ").Append(F(t.bytesDown / Math.Max(1f, m.Elapsed)))
                  .Append(", \"upPayloadPerSecond\": ").Append(F(t.bytesUp / Math.Max(1f, m.Elapsed)))
                  .Append(", \"upMessagesPerSecond\": ").Append(F(t.messagesUp / Math.Max(1f, m.Elapsed)))
                  .Append(", \"upWireWithAckPerSecond\": ").Append(F((t.bytesUp + t.messagesUp * (2 + NetProtocol.NamedMessageOverheadBytes)) / Math.Max(1f, m.Elapsed)))
                  .Append('}');
                first = false;
            }
            sb.Append(first ? "],\n" : "\n  ],\n");
            // Effect calls a second from the real ship's build and the enemies (the fx rate estimate).
            sb.Append("  \"fxCallsPerSecond\": {");
            first = true;
            foreach (var pair in Fx.Calls)
            {
                _callsAtStart.TryGetValue(pair.Key, out var before);
                sb.Append(first ? "" : ", ").Append('"').Append(pair.Key).Append("\": ").Append(F((pair.Value - before) / Math.Max(1f, seconds)));
                first = false;
            }
            sb.Append("},\n");
            sb.Append("  \"budgets\": [\n");
            for (var b = 0; b < _encoders.Count; b++)
            {
                var enc = _encoders[b];
                sb.Append("    {\"budgetWireBytesPerSnapshot\": ").Append(enc.Budget).Append(", \"inTick\": ").Append(b == 0 ? "true" : "false").Append(", \"clients\": [\n");
                for (var k = 0; k < enc.Clients.Count; k++)
                {
                    var cs = enc.Clients[k];
                    sb.Append("      {\"client\": ").Append(k).Append(", \"ship\": \"").Append(k == 0 ? "real" : "ghost" + k).Append("\", \"windows\": ").Append(cs.windows.Count).Append(",\n");
                    Category(sb, "wire", cs.windows, w => w.Wire); sb.Append(",\n");
                    Category(sb, "payload", cs.windows, w => w.Payload); sb.Append(",\n");
                    Category(sb, "own", cs.windows, w => w.own); sb.Append(",\n");
                    Category(sb, "ships", cs.windows, w => w.ships); sb.Append(",\n");
                    Category(sb, "enemies", cs.windows, w => w.enemies); sb.Append(",\n");
                    Category(sb, "fxOwn", cs.windows, w => w.fxOwn); sb.Append(",\n");
                    Category(sb, "fxOthers", cs.windows, w => w.fxOthers); sb.Append(",\n");
                    Category(sb, "threats", cs.windows, w => w.threats); sb.Append(",\n");
                    Category(sb, "framing", cs.windows, w => w.framing); sb.Append(",\n");
                    Stats(sb, "frameWireBytes", cs.frameWire, "        "); sb.Append(",\n");
                    Stats(sb, "enemyPayloadBytes", cs.enemyBytes, "        "); sb.Append(",\n");
                    Stats(sb, "enemiesInView", cs.view, "        "); sb.Append(",\n");
                    Stats(sb, "enemiesWritten", cs.refreshed, "        "); sb.Append(",\n");
                    Stats(sb, "nearEnemyUpdateAgeMs", cs.nearAgeMs, "        "); sb.Append(",\n");
                    Stats(sb, "closeEnemyUpdateAgeMs", cs.closeAgeMs, "        "); sb.Append(",\n");
                    Stats(sb, "ownFxDelayMs", cs.ownFxDelayMs, "        "); sb.Append(",\n");
                    Stats(sb, "othersFxDelayMs", cs.othersFxDelayMs, "        "); sb.Append(",\n");
                    Stats(sb, "baselineAgeMs", cs.baselineAgeMs, "        "); sb.Append(",\n");
                    sb.Append("        \"nearStaleShare\": ").Append(F(cs.nearSamples > 0 ? (double)cs.nearStale / cs.nearSamples : 0))
                      .Append(", \"enemiesNotShown\": ").Append(cs.notShown).Append(", \"fxSent\": ").Append(cs.fxSent).Append(", \"fxDeferred\": ").Append(cs.fxDeferred)
                      .Append(", \"fxDropped\": ").Append(cs.fxDropped).Append(", \"framesOverBudget\": ").Append(cs.overBudget).Append(", \"baselineMissing\": ").Append(cs.baselineMissing).Append("}");
                    sb.Append(k + 1 < enc.Clients.Count ? ",\n" : "\n");
                }
                sb.Append("    ]}").Append(b + 1 < _encoders.Count ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        static readonly List<float> _scratch = new List<float>();

        static void Category(StringBuilder sb, string name, List<Window> windows, Func<Window, long> pick)
        {
            _scratch.Clear();
            foreach (var w in windows) _scratch.Add(pick(w));
            Stats(sb, name + "BytesPerSecond", _scratch, "        ");
        }
    }
}
