using RoguePirates.V4;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Safety net for a bug in the island collision (IslandObstacles): once the hull ends up more than ~0.3 m inside one
    /// of the islands' concave colliders (props such as rocks and palms), the game's own push-out can't free it (capped at
    /// 0.25 m while stopped, and the bounds-based push for concave colliders can point the wrong way), so the ship can
    /// still turn but no longer move. This watches for exactly that: the player is trying to move, the ship hasn't moved
    /// for a moment, and it overlaps an island. It then finds the shortest free way out (preferring the direction the
    /// player is steering) and slides the ship clear. No game code changed.
    /// Runs only in single-player runs (never in 1v1 / network play, where the ship's movement must stay in sync), and
    /// costs nothing until the ship is actually stuck.
    /// </summary>
    [DefaultExecutionOrder(-450)] // before the ship's own FixedUpdate
    [AddComponentMenu("Rogue Pirates/Fixes/Ship Unstuck")]
    public sealed class ShipUnstuck : MonoBehaviour
    {
        [Tooltip("How long the ship must be stuck (trying to move, not moving, overlapping an island) before it's freed (s).")]
        public float StuckTime = 0.4f;
        [Tooltip("Less than this much movement over Stuck Time counts as not moving (m).")]
        public float StillDistance = 0.15f;
        [Tooltip("Throttle or steering at least this strong counts as trying to move.")]
        [Range(0f, 1f)] public float InputThreshold = 0.2f;
        [Tooltip("Furthest it will slide the ship to free it (m).")]
        public float MaxEscape = 4f;
        [Tooltip("Extra clearance past the edge (m).")]
        public float Clearance = 0.12f;
        [Tooltip("Write a line to the console each time the ship is freed (for testing).")]
        public bool LogWhenFreed = true;

        /// <summary>How many times this run the ship was freed (tests and tuning).</summary>
        public int TimesFreed { get; private set; }

        PlayerShipController _ship;
        Rigidbody _body;
        RoguePirates.V4.Foundation.Combat.DamageReceiver _damage;
        V4Game _game;
        Vector3 _anchor;
        float _stuckFor, _nextSearch;

        void FixedUpdate()
        {
            if (!Find()) return;
            if (_game != null && _game.IsPvp) return;
            if (_ship.NetStepped) return;
            // The ship doesn't move (and its last input is stale) while the game is paused, the run is over or it's sunk.
            var run = RoguePirates.V4.Foundation.Core.RunManager.Instance;
            if (run != null && (run.IsPaused || run.IsRunEnded)) { _stuckFor = 0f; _anchor = _ship.transform.position; return; }
            if (_damage != null && !_damage.IsAlive) return;

            var input = _ship.LastSteerThrottle;
            var trying = Mathf.Abs(input.y) >= InputThreshold || Mathf.Abs(input.x) >= InputThreshold;
            var pos = _ship.transform.position;
            if (!trying) { _stuckFor = 0f; _anchor = pos; return; }

            var moved = pos - _anchor; moved.y = 0f;
            if (moved.magnitude > StillDistance) { _stuckFor = 0f; _anchor = pos; return; }

            _stuckFor += Time.fixedDeltaTime;
            if (_stuckFor < StuckTime) return;
            _stuckFor = 0f;
            _anchor = pos;

            var radius = _ship.IslandCollisionRadius;
            if (!IslandObstacles.IsPositionBlocked(pos, radius)) return; // not wedged: just against a shore, steering works

            // Prefer sliding out the way the player is pushing (throttle along the heading).
            var forward = _ship.MovementForward; forward.y = 0f;
            var preferred = forward.sqrMagnitude > 1e-4f ? forward.normalized * Mathf.Sign(input.y == 0f ? 1f : input.y) : Vector3.zero;

            Vector3 best;
            if (!TryFindExit(pos, radius, preferred, out best)) return;
            best.y = pos.y;
            if (_body != null) _body.position = best;
            _ship.transform.position = best;
            TimesFreed++;
            if (LogWhenFreed)
                Debug.Log("[ShipUnstuck] Freed the ship from an island collider: moved " + (best - pos).magnitude.ToString("F2") + " m.");
        }

        // The smallest move that clears every island collider, checking 16 directions; ties go to the preferred one.
        bool TryFindExit(Vector3 pos, float radius, Vector3 preferred, out Vector3 exit)
        {
            exit = pos;
            var found = false;
            var bestScore = float.MaxValue;
            const int directions = 16;
            for (var i = 0; i < directions; i++)
            {
                var dir = Quaternion.Euler(0f, i * (360f / directions), 0f) * Vector3.forward;
                for (var d = 0.1f; d <= MaxEscape; d += 0.1f)
                {
                    var p = pos + dir * d;
                    if (IslandObstacles.IsPositionBlocked(p, radius)) continue;
                    var clear = pos + dir * (d + Clearance);
                    if (IslandObstacles.IsPositionBlocked(clear, radius)) continue;
                    // Shorter is better; a little bonus for the direction the player wants to go.
                    var score = d - (preferred.sqrMagnitude > 0f ? Vector3.Dot(dir, preferred) * 0.3f : 0f);
                    if (score < bestScore) { bestScore = score; exit = clear; found = true; }
                    break;
                }
            }
            return found;
        }

        bool Find()
        {
            if (_ship != null && _ship.isActiveAndEnabled) return true;
            if (Time.unscaledTime < _nextSearch) return false;
            _nextSearch = Time.unscaledTime + 0.5f;
            var cam = Camera.main;
            var follow = cam != null ? cam.GetComponent<FollowCamera>() : null;
            _ship = follow != null && follow.target != null ? follow.target.GetComponent<PlayerShipController>() : null;
            if (_ship == null) _ship = FindFirstObjectByType<PlayerShipController>();
            _body = _ship != null ? _ship.GetComponent<Rigidbody>() : null;
            _damage = _ship != null ? _ship.GetComponent<RoguePirates.V4.Foundation.Combat.DamageReceiver>() : null;
            if (_game == null) _game = FindFirstObjectByType<V4Game>();
            if (_ship != null) _anchor = _ship.transform.position;
            return _ship != null;
        }
    }
}
