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
    /// turned to its heading, its bar at its hull fraction; released when the mirror drops it. Nothing here decides
    /// anything: positions, hull and deaths are the server's.
    /// M9b playtest fixes (B, C): every enemy is drawn at the mirror's one render time (the newest snapshot's server time
    /// less two snapshot intervals and the link's jitter), between the two snapshots around it, or a little past the newest
    /// on its velocity when the next one is late. Before, each was drawn between its own last two snapshots by the time
    /// since the newest snapshot over one interval: a far-band enemy covered three intervals' move in one and stood still for
    /// two, a late snapshot froze them all and the next one jumped, and the leash's relocation slid across the screen. A jump
    /// (no enemy sails that fast) is drawn at once (the presenter clears its wake trails). An enemy the mirror drops is drawn
    /// until the render time reaches the snapshot that dropped it.
    /// </summary>
    public sealed class NetEnemyViews : MonoBehaviour
    {
        sealed class DrawnEnemy
        {
            public EnemyDefinition def;
            public Transform view, bar;
            public NetEnemyMirror.Track track;
            public float yaw;
            public byte hp = 255;
            /// <summary>The server tick of the snapshot that dropped it (0: still in the mirror), and when.</summary>
            public uint goneAtTick;
            public float goneAt;
        }

        NetClient _client;
        IEnemyViewSink _sink;
        readonly Dictionary<string, EnemyDefinition> _byId = new Dictionary<string, EnemyDefinition>();
        readonly Dictionary<int, DrawnEnemy> _drawn = new Dictionary<int, DrawnEnemy>();
        readonly List<int> _gone = new List<int>();

        /// <summary>A dropped enemy is drawn at most this long after the mirror dropped it.</summary>
        const float GoneGraceSeconds = 0.5f;

        public int Drawn => _drawn.Count;
        public int Unknown { get; private set; }
        /// <summary>The render time's delay behind the newest snapshot (s).</summary>
        public float DelaySeconds => _client != null ? _client.Enemies.DelaySeconds : 0f;

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
            var renderTick = mirror.RenderTick(NetFrameClock.Now);
            foreach (var e in mirror.All)
            {
                if (!_drawn.TryGetValue(e.unitId, out var d))
                {
                    if (!_byId.TryGetValue(mirror.DefinitionId(e.definition), out var def)) { Unknown++; continue; }
                    var elite = (e.flags & NetProtocol.EnemyElite) != 0;
                    var scale = EnemySimulation.AppearanceScale(def, elite, ActiveRun.EnemyVisualScale, _sink);
                    e.track.Sample(renderTick, out var at, out _, out var startHeading);
                    var view = _sink.CreateView(def, Ground(at, def), scale, elite, e.unitId);
                    if (view == null) continue;
                    d = new DrawnEnemy { def = def, view = view, track = e.track, bar = _sink.AttachHealthBar(def, view, elite), yaw = NetCodec.HeadingDegrees(startHeading) };
                    view.rotation = Quaternion.Euler(0f, d.yaw + def.visualYawOffset, 0f);
                    _drawn[e.unitId] = d;
                }
                d.track = e.track;
                d.goneAtTick = 0;
                if (d.hp != e.hp)
                {
                    d.hp = e.hp;
                    _sink.SetHealthFill(d.bar, e.hp / 255f);
                }
            }
            _gone.Clear();
            var now = Time.unscaledTime;
            foreach (var pair in _drawn)
            {
                var d = pair.Value;
                if (!mirror.TryGet(pair.Key, out _) && d.goneAtTick == 0) { d.goneAtTick = mirror.LastCompleteTick; d.goneAt = now; }
                if (d.goneAtTick != 0 && (renderTick >= d.goneAtTick || now - d.goneAt > GoneGraceSeconds)) { _gone.Add(pair.Key); continue; }
                Place(d, renderTick);
            }
            foreach (var id in _gone)
            {
                var d = _drawn[id];
                _sink.Release(d.def, d.view, d.bar);
                _drawn.Remove(id);
            }
        }

        void Place(DrawnEnemy d, float renderTick)
        {
            if (!d.track.Sample(renderTick, out var p, out var velocity, out var heading)) return;
            d.view.position = Ground(p, d.def);
            if (velocity.sqrMagnitude > 0.01f) d.yaw = NetCodec.HeadingDegrees(heading);
            var facing = Quaternion.Euler(0f, d.yaw + d.def.visualYawOffset, 0f);
            d.view.rotation = Quaternion.Slerp(d.view.rotation, facing, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
            _sink.TickWake(d.view, d.def.category, velocity);
        }

        /// <summary>Each drawn enemy's unit id and drawn position (measurements).</summary>
        public void ForEachDrawn(System.Action<int, Vector3> each)
        {
            foreach (var pair in _drawn) if (pair.Value.view != null) each(pair.Key, pair.Value.view.position);
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
