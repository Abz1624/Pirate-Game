using System.Collections.Generic;
using UnityEngine;

namespace RoguePirates.V5.Net
{
    /// <summary>
    /// V5 M9b playtest fixes (A, B, C): how smoothly things move on screen, frame by frame. For each drawn thing the
    /// per-frame speed is its move over the frame's time; the jitter is how much that speed changes from one frame to the
    /// next (m/s). Sailing at a steady speed, a ship drawn at the 30 Hz net tick on a 60 Hz display moves every other frame
    /// (its per-frame speed swings between 0 and twice its speed: jitter about twice its speed); drawn between its ticks
    /// it is close to 0. An on-screen enemy moving faster than any enemy sails (<see cref="NetEnemyMirror.MaxEnemySpeed"/>)
    /// is counted as a jump: a frame-long one is a relocation drawn at once, several in a row are a slide.
    /// </summary>
    public sealed class NetSmoothness
    {
        public readonly List<float> ShipJitter = new List<float>();
        public readonly List<float> ShipSpeed = new List<float>();
        public readonly List<float> EnemyJitter = new List<float>();
        /// <summary>Frames an on-screen enemy moved faster than any enemy sails.</summary>
        public int EnemyFastFrames { get; private set; }
        /// <summary>Of those, frames that followed another fast frame of the same enemy (a slide, not one jump).</summary>
        public int EnemySlideFrames { get; private set; }
        public int Frames { get; private set; }
        /// <summary>Frames the thing stood still right after moving at over 2 m/s (a stutter: a held step, a late snapshot).</summary>
        public int ShipStallFrames { get; private set; }
        public int EnemyStallFrames { get; private set; }
        const float StallFromSpeed = 2f, StallShare = 0.1f;

        Vector3 _ship;
        float _shipSpeed = -1f;
        bool _haveShip;
        readonly Dictionary<int, (Vector3 p, float speed, int frame, bool fast)> _enemies = new Dictionary<int, (Vector3, float, int, bool)>();

        public void Reset()
        {
            ShipJitter.Clear();
            ShipSpeed.Clear();
            EnemyJitter.Clear();
            EnemyFastFrames = EnemySlideFrames = Frames = ShipStallFrames = EnemyStallFrames = 0;
            _haveShip = false;
            _shipSpeed = -1f;
            _enemies.Clear();
        }

        public void BeginFrame() => Frames++;

        /// <summary>The next frame starts afresh (after an away or a drop: the move across the gap is not a frame's move).</summary>
        public void Break()
        {
            _haveShip = false;
            _shipSpeed = -1f;
            _enemies.Clear();
        }

        /// <summary>The ship as drawn this frame (<paramref name="dt"/>: the frame's time).</summary>
        public void Ship(Vector3 drawn, float dt)
        {
            if (dt <= 1e-4f) return;
            if (_haveShip)
            {
                var speed = Flat(drawn - _ship) / dt;
                if (_shipSpeed >= 0f) ShipJitter.Add(Mathf.Abs(speed - _shipSpeed));
                if (_shipSpeed > StallFromSpeed && speed < _shipSpeed * StallShare) ShipStallFrames++;
                ShipSpeed.Add(speed);
                _shipSpeed = speed;
            }
            _ship = drawn;
            _haveShip = true;
        }

        /// <summary>An enemy as drawn this frame; only enemies drawn on screen in both frames count.</summary>
        public void Enemy(int unitId, Vector3 drawn, float dt, bool onScreen)
        {
            if (dt <= 1e-4f) return;
            if (_enemies.TryGetValue(unitId, out var last) && last.frame == Frames - 1 && onScreen)
            {
                var speed = Flat(drawn - last.p) / dt;
                var fast = speed > NetEnemyMirror.MaxEnemySpeed;
                if (fast)
                {
                    EnemyFastFrames++;
                    if (last.fast) EnemySlideFrames++;
                }
                else if (last.speed >= 0f && !last.fast)
                {
                    EnemyJitter.Add(Mathf.Abs(speed - last.speed));
                    if (last.speed > StallFromSpeed && speed < last.speed * StallShare) EnemyStallFrames++;
                }
                _enemies[unitId] = (drawn, fast ? -1f : speed, Frames, fast);
                return;
            }
            _enemies[unitId] = (drawn, -1f, onScreen ? Frames : -10, false);
        }

        static float Flat(Vector3 v) => Mathf.Sqrt(v.x * v.x + v.z * v.z);

        public static float Mean(List<float> values)
        {
            if (values.Count == 0) return 0f;
            var sum = 0f;
            foreach (var v in values) sum += v;
            return sum / values.Count;
        }

        /// <summary>The coefficient of variation of the per-frame speed (its standard deviation over its mean).</summary>
        public static float Variation(List<float> speeds)
        {
            if (speeds.Count < 2) return 0f;
            var mean = Mean(speeds);
            if (mean <= 1e-4f) return 0f;
            var sq = 0f;
            foreach (var s in speeds) sq += (s - mean) * (s - mean);
            return Mathf.Sqrt(sq / (speeds.Count - 1)) / mean;
        }
    }
}
