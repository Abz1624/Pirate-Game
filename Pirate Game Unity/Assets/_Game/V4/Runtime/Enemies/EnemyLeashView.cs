using RoguePirates.V4.Config;
using UnityEngine;

namespace RoguePirates.V4.Enemies
{
    /// <summary>
    /// V5 (owner, 10 Oct, D28 extended): the run camera's view of the sea, as data, for the enemy leash. The top-down camera
    /// sits <see cref="RunConfig.leashViewCameraHeight"/> above and <see cref="RunConfig.leashViewCameraBack"/> behind the
    /// point it follows (screen up is always world +z), looks at it, and has a vertical field of view of
    /// <see cref="RunConfig.leashViewFieldOfView"/>; its four view corners on the sea make a trapezoid around that point
    /// (30.5 m to the top edge, 13.9 m to the bottom one, wider at the top). The width is the widest screen the game is
    /// played on (<see cref="RunConfig.leashViewMaxAspect"/>): a narrower screen sees less, so an enemy out of this view
    /// is out of every player's view. The match server has no camera; the same numbers drive the leash in a normal
    /// voyage and online, so the shared simulation does not depend on anyone's window.
    /// </summary>
    public readonly struct EnemyLeashView
    {
        /// <summary>The view's corners on the sea, relative to the point the camera follows (x east, y north): top left, top
        /// right, bottom right, bottom left (clockwise seen from above).</summary>
        public readonly Vector2 TopLeft, TopRight, BottomRight, BottomLeft;
        public readonly bool Valid;

        EnemyLeashView(Vector2 tl, Vector2 tr, Vector2 br, Vector2 bl, bool valid)
        {
            TopLeft = tl;
            TopRight = tr;
            BottomRight = br;
            BottomLeft = bl;
            Valid = valid;
        }

        public static EnemyLeashView From(RunConfig config) =>
            config == null
                ? FromCamera(RunConfig.LeashViewCameraHeightDefault, RunConfig.LeashViewCameraBackDefault, RunConfig.LeashViewFieldOfViewDefault, RunConfig.LeashViewMaxAspectDefault)
                : FromCamera(config.leashViewCameraHeight, config.leashViewCameraBack, config.leashViewFieldOfView, config.leashViewMaxAspect);

        /// <summary>The view of a camera <paramref name="height"/> above and <paramref name="back"/> behind the point it
        /// follows, looking at it, with this vertical field of view (degrees) and aspect (width over height). Not valid when
        /// the view reaches the horizon (no footprint).</summary>
        public static EnemyLeashView FromCamera(float height, float back, float verticalFov, float aspect)
        {
            if (height <= 0f) return default;
            var camera = new Vector3(0f, height, -back);
            var rotation = Quaternion.LookRotation(-camera, Vector3.up);
            var tan = Mathf.Tan(verticalFov * 0.5f * Mathf.Deg2Rad);
            if (!Hit(camera, rotation, -tan * aspect, tan, out var tl) || !Hit(camera, rotation, tan * aspect, tan, out var tr) ||
                !Hit(camera, rotation, tan * aspect, -tan, out var br) || !Hit(camera, rotation, -tan * aspect, -tan, out var bl))
                return default; // a corner sees the horizon
            return new EnemyLeashView(tl, tr, br, bl, true);
        }

        static bool Hit(Vector3 camera, Quaternion rotation, float x, float y, out Vector2 onSea)
        {
            var dir = rotation * new Vector3(x, y, 1f);
            onSea = default;
            if (dir.y > -1e-4f) return false;
            var hit = camera + dir * (-camera.y / dir.y);
            onSea = new Vector2(hit.x, hit.z);
            return true;
        }

        public Vector2 Corner(int i) => (i & 3) == 0 ? TopLeft : (i & 3) == 1 ? TopRight : (i & 3) == 2 ? BottomRight : BottomLeft;
    }

    /// <summary>
    /// V5 (owner, 10 Oct, D28 extended): where the leash keeps relocations out of sight. The view (<see cref="EnemyLeashView"/>)
    /// swept along the ship's velocity from <see cref="RunConfig.leashViewLagSeconds"/> behind the ship (the follow camera
    /// trails it) to <see cref="RunConfig.leashViewLeadSeconds"/> ahead of it (online, the client draws its predicted ship
    /// ahead of the enemies it draws, by about its input delay plus the enemies' render delay): the convex hull of the two
    /// trapezoids. A relocation is put along its bearing from the ship just outside this shape (the margin plus the enemy's
    /// radius clear of every edge, <see cref="PlacementDistance"/>), and an enemy within it is never moved.
    /// One instance is reused (no allocation per leash check).
    /// </summary>
    public sealed class EnemyLeashShape
    {
        readonly Vector2[] _points = new Vector2[8];
        readonly Vector2[] _hull = new Vector2[9];
        int _count;

        public bool Valid => _count >= 3;
        public int Count => _count;
        public Vector2 this[int i] => _hull[i];

        /// <summary>The view around the ship (offset 0), or swept from <paramref name="from"/> to <paramref name="to"/>
        /// (offsets of the point the camera follows, relative to the ship).</summary>
        public EnemyLeashShape Set(in EnemyLeashView view, Vector2 from, Vector2 to)
        {
            _count = 0;
            if (!view.Valid) return this;
            for (var i = 0; i < 4; i++)
            {
                _points[i] = view.Corner(i) + from;
                _points[i + 4] = view.Corner(i) + to;
            }
            Hull();
            return this;
        }

        public EnemyLeashShape Set(in EnemyLeashView view) => Set(view, Vector2.zero, Vector2.zero);

        /// <summary>The view for the leash at a ship sailing at <paramref name="velocity"/> (flat): swept from the follow
        /// camera's lag behind it to the online lead ahead of it.</summary>
        public EnemyLeashShape Set(RunConfig config, Vector3 velocity)
        {
            var lag = config != null ? config.leashViewLagSeconds : RunConfig.LeashViewLagSecondsDefault;
            var lead = config != null ? config.leashViewLeadSeconds : RunConfig.LeashViewLeadSecondsDefault;
            var v = new Vector2(velocity.x, velocity.z);
            return Set(EnemyLeashView.From(config), -v * lag, v * lead);
        }

        // Andrew's monotone chain over the 8 corners, counter-clockwise.
        void Hull()
        {
            var n = 8;
            for (var i = 1; i < n; i++) // insertion sort by x, then y (8 points)
            {
                var p = _points[i];
                var j = i - 1;
                while (j >= 0 && (_points[j].x > p.x || _points[j].x == p.x && _points[j].y > p.y)) { _points[j + 1] = _points[j]; j--; }
                _points[j + 1] = p;
            }
            var k = 0;
            for (var i = 0; i < n; i++)
            {
                while (k >= 2 && Cross(_hull[k - 2], _hull[k - 1], _points[i]) <= 0f) k--;
                _hull[k++] = _points[i];
            }
            for (int i = n - 2, lower = k + 1; i >= 0; i--)
            {
                while (k >= lower && Cross(_hull[k - 2], _hull[k - 1], _points[i]) <= 0f) k--;
                _hull[k++] = _points[i];
            }
            _count = k - 1; // the last point repeats the first
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>Is this point (relative to the ship) inside?</summary>
        public bool Contains(Vector2 p)
        {
            if (!Valid) return false;
            for (var i = 0; i < _count; i++)
                if (Cross(_hull[i], _hull[(i + 1) % _count], p) < 0f) return false; // counter-clockwise: inside is to the left
            return true;
        }

        /// <summary>How far this point (relative to the ship) is outside (0 inside).</summary>
        public float DistanceOutside(Vector2 p)
        {
            if (!Valid || Contains(p)) return 0f;
            var best = float.MaxValue;
            for (var i = 0; i < _count; i++)
            {
                var a = _hull[i];
                var ab = _hull[(i + 1) % _count] - a;
                var t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-8f, ab.sqrMagnitude));
                best = Mathf.Min(best, (a + ab * t - p).magnitude);
            }
            return best;
        }

        /// <summary>The distance from the ship along <paramref name="direction"/> (flat, any length) at which a point is
        /// <paramref name="clearance"/> outside (the edge along it when 0).</summary>
        public float PlacementDistance(Vector2 direction, float clearance)
        {
            if (!Valid || direction.sqrMagnitude < 1e-8f) return clearance;
            direction.Normalize();
            // The distance outside a convex shape grows along a ray from a point inside it (the ship): halve the interval.
            float lo = 0f, hi = 32f;
            while (DistanceOutside(direction * hi) < clearance && hi < 4096f) hi *= 2f;
            for (var i = 0; i < 24; i++)
            {
                var mid = (lo + hi) * 0.5f;
                if (DistanceOutside(direction * mid) >= clearance) hi = mid;
                else lo = mid;
            }
            return hi;
        }

        /// <summary>The leash's placement: <paramref name="angleDegrees"/> off the ship's <paramref name="heading"/> (flat,
        /// unit), just outside with this clearance (the margin plus the enemy's radius); relative to the ship.</summary>
        public Vector3 Placement(Vector3 heading, float angleDegrees, float clearance)
        {
            var dir = Quaternion.Euler(0f, angleDegrees, 0f) * heading;
            dir.y = 0f;
            dir.Normalize();
            return dir * PlacementDistance(new Vector2(dir.x, dir.z), clearance);
        }
    }
}
