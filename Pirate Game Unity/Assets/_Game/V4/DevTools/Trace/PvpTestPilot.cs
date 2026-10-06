using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Pvp;
using UnityEngine;

namespace RoguePirates.V4.DevTools.Trace
{
    /// <summary>
    /// Playtest (3 Oct, owner): an autopilot for checking the 1v1 mode from scripts (Editor and development builds): sails to
    /// the nearest barrel within 60 m, else circles the opponent at about 14 m, and turns back inside the storm ring.
    /// <c>PvpTestPilot.Enable()</c> from the Editor's eval.
    /// </summary>
    public sealed class PvpTestPilot : IShipSteerInput
    {
        readonly PlayerShipController _ship;
        readonly IPvpMatchView _match;

        PvpTestPilot(PlayerShipController ship, IPvpMatchView match)
        {
            _ship = ship;
            _match = match;
        }

        /// <summary>Hold mode: stays at this arena offset (x, z) from the centre instead of fighting (a match to time).</summary>
        Vector2? _hold;

        public static string Enable() => Enable(null);

        public static string Hold(float x, float z) => Enable(new Vector2(x, z));

        static string Enable(Vector2? hold)
        {
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            var game = Object.FindFirstObjectByType<V4Game>();
            if (ship == null || game == null || game.PvpMatch == null) return "no 1v1 run";
            ship.ConfigureBotControl(new PvpTestPilot(ship, game.PvpMatch) { _hold = hold });
            return "pilot on";
        }

        Vector3 _lastPos;
        float _checkAt, _reverseUntil;

        public Vector2 ReadSteerThrottle()
        {
            if (_ship == null || _match == null) return Vector2.zero;
            var p = _ship.Position;
            // Stuck on a shore: back off for a second, turning.
            if (Time.time < _reverseUntil) return new Vector2(1f, -1f);
            if (Time.time >= _checkAt)
            {
                var stuck = _match.Started && !_hold.HasValue && Vector3.Distance(p, _lastPos) < 1.5f;
                _lastPos = p;
                _checkAt = Time.time + 1.5f;
                if (stuck) { _reverseUntil = Time.time + 1.2f; return new Vector2(1f, -1f); }
            }
            var goal = _match.ArenaCentre;
            var best = 60f;
            var found = false;
            foreach (var b in _match.Barrels)
            {
                var d = Vector3.Distance(b.World, p);
                if (d < best) { best = d; goal = b.World; found = true; }
            }
            if (!found && _match.OpponentKnown && _match.OpponentAlive)
            {
                var away = p - _match.OpponentPosition;
                away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Vector3.right;
                var r = away.normalized;
                goal = _match.OpponentPosition + r * 14f + new Vector3(-r.z, 0f, r.x) * 8f;
            }
            if (_hold.HasValue) goal = _match.ArenaCentre + new Vector3(_hold.Value.x, 0f, _hold.Value.y);
            var fromCentre = p - _match.ArenaCentre;
            fromCentre.y = 0f;
            if (fromCentre.magnitude > _match.StormRadius - 5f) goal = _match.ArenaCentre;
            var to = goal - p;
            to.y = 0f;
            var angle = Vector3.SignedAngle(_ship.MovementForward, to, Vector3.up);
            if (_hold.HasValue && to.magnitude < 4f) return Vector2.zero;
            return new Vector2(Mathf.Clamp(angle / 30f, -1f, 1f), to.magnitude > 3f ? 1f : 0.3f);
        }
    }
}
