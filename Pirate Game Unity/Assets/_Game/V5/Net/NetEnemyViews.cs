using System.Collections.Generic;
using RoguePirates.V4;
using RoguePirates.V4.Enemies;
using RoguePirates.V4.Foundation.Data;
using RoguePirates.V4.Foundation.Progression;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b part 2b: the server's enemies on the client's stage, drawn by the run's own enemy view presenter (the view
    /// pool's models, health bars, wakes and tints, through <see cref="IEnemyViewSink"/>, the contract the enemy simulation
    /// draws through in a voyage). The net client takes the simulation's place as the sink's producer: one view per enemy
    /// in the mirror, created at the server's kind (its definition from this run's database, matched by content id),
    /// placed between its last two snapshots (drawn one snapshot behind, interpolated), turned to its heading, its bar
    /// at its hull fraction; released when the mirror drops it. Nothing here decides anything: positions, hull and
    /// deaths are the server's.
    /// </summary>
    public sealed class NetEnemyViews : MonoBehaviour
    {
        sealed class DrawnEnemy
        {
            public EnemyDefinition def;
            public Transform view, bar;
            public uint tick;
            public float yaw;
            public byte hp = 255;
        }

        NetClient _client;
        IEnemyViewSink _sink;
        readonly Dictionary<string, EnemyDefinition> _byId = new Dictionary<string, EnemyDefinition>();
        readonly Dictionary<int, DrawnEnemy> _drawn = new Dictionary<int, DrawnEnemy>();
        readonly List<int> _gone = new List<int>();
        uint _lastComplete;
        float _completeAt;

        public int Drawn => _drawn.Count;
        public int Unknown { get; private set; }

        public void Bind(V4Game game, NetClient client)
        {
            _client = client;
            game.Context.TryGet(out _sink);
            _byId.Clear();
            var defs = game.Shell != null && game.Shell.Enemies != null ? game.Shell.Enemies.Simulation.Definitions : null;
            if (defs != null)
                foreach (var d in defs)
                    if (d != null && !string.IsNullOrEmpty(d.id) && !_byId.ContainsKey(d.id)) _byId[d.id] = d;
        }

        void LateUpdate()
        {
            if (_client == null || _sink == null) return;
            var mirror = _client.Enemies;
            if (mirror.LastCompleteTick != _lastComplete)
            {
                _lastComplete = mirror.LastCompleteTick;
                _completeAt = Time.unscaledTime;
            }
            // One snapshot interval behind: t runs 0 to 1 between the last two snapshots of each enemy.
            var interval = NetProtocol.TickSeconds * NetProtocol.SnapshotEveryTicks;
            var t = Mathf.Clamp01((Time.unscaledTime - _completeAt) / interval);
            foreach (var e in mirror.All)
            {
                if (!_drawn.TryGetValue(e.unitId, out var d))
                {
                    if (!_byId.TryGetValue(mirror.DefinitionId(e.definition), out var def)) { Unknown++; continue; }
                    var elite = (e.flags & NetProtocol.EnemyElite) != 0;
                    var scale = EnemySimulation.AppearanceScale(def, elite, ActiveRun.EnemyVisualScale, _sink);
                    var at = Ground(e.previous, def);
                    var view = _sink.CreateView(def, at, scale, elite, e.unitId);
                    if (view == null) continue;
                    d = new DrawnEnemy { def = def, view = view, bar = _sink.AttachHealthBar(def, view, elite), yaw = NetCodec.HeadingDegrees(e.heading) };
                    view.rotation = Quaternion.Euler(0f, d.yaw + def.visualYawOffset, 0f);
                    _drawn[e.unitId] = d;
                }
                var p = Ground(NetEnemyMirror.Interpolate(e, t), d.def);
                var velocity = e.tick > e.previousTick ? (e.position - e.previous) / ((e.tick - e.previousTick) * NetProtocol.TickSeconds) : Vector3.zero;
                velocity.y = 0f;
                d.view.position = p;
                if (velocity.sqrMagnitude > 0.01f) d.yaw = NetCodec.HeadingDegrees(e.heading);
                var facing = Quaternion.Euler(0f, d.yaw + d.def.visualYawOffset, 0f);
                d.view.rotation = Quaternion.Slerp(d.view.rotation, facing, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                _sink.TickWake(d.view, d.def.category, velocity);
                if (d.hp != e.hp)
                {
                    d.hp = e.hp;
                    _sink.SetHealthFill(d.bar, e.hp / 255f);
                }
                d.tick = e.tick;
            }
            _gone.Clear();
            foreach (var pair in _drawn)
                if (!mirror.TryGet(pair.Key, out _)) _gone.Add(pair.Key);
            foreach (var id in _gone)
            {
                var d = _drawn[id];
                _sink.Release(d.def, d.view, d.bar);
                _drawn.Remove(id);
            }
        }

        static Vector3 Ground(Vector3 p, EnemyDefinition def) => new Vector3(p.x, def.visualHeightOffset, p.z);

        void OnDestroy()
        {
            if (_sink == null) return;
            foreach (var d in _drawn.Values) if (d.view != null) _sink.Release(d.def, d.view, d.bar);
            _drawn.Clear();
        }
    }
}
