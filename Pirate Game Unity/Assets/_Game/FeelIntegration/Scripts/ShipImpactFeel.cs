using MoreMountains.Feedbacks;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.World;
using UnityEngine;

namespace RoguePirates.FeelIntegration
{
    /// <summary>
    /// Plays a Feel MMF_Player when the player's ship bounces off an island/obstacle.
    /// Listens to PlayerShipController.IslandImpacted (existing event, no game code changed). Harder hits play stronger.
    /// The feedback position is the contact point on the shore (nearest island collider), so splash effects appear
    /// there and the ship leans away from it. Finds the ship the camera follows, so it works when spawned at runtime.
    /// </summary>
    [AddComponentMenu("Rogue Pirates/Feel/Ship Impact Feel")]
    public sealed class ShipImpactFeel : MonoBehaviour
    {
        [Tooltip("Feel player to play on an obstacle hit. Defaults to the MMF_Player on this object.")]
        public MMF_Player ImpactFeedback;

        [Header("Impact speed (m/s into the obstacle) to intensity")]
        [Tooltip("At or below this speed the feedback plays at Min Intensity.")]
        public float SoftHitSpeed = 3f;
        [Tooltip("At or above this speed the feedback plays at full intensity.")]
        public float HardHitSpeed = 25f;
        [Range(0f, 1f)] public float MinIntensity = 0.7f;

        [Header("Contact point")]
        [Tooltip("How far past the hull to look for the shore that was hit (m).")]
        public float ContactSearch = 2f;

        static readonly Collider[] Overlap = new Collider[8];
        PlayerShipController _ship;
        float _nextSearch;

        void Awake()
        {
            if (ImpactFeedback == null) ImpactFeedback = GetComponent<MMF_Player>();
        }

        void OnDisable() => Bind(null);

        void LateUpdate()
        {
            var followed = FollowedShip();
            if (followed != null)
            {
                if (followed != _ship) Bind(followed);
                return;
            }
            if (_ship != null || Time.unscaledTime < _nextSearch) return;
            _nextSearch = Time.unscaledTime + 0.5f;
            var found = FindFirstObjectByType<PlayerShipController>();
            if (found != null) Bind(found);
        }

        static PlayerShipController FollowedShip()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var follow = cam.GetComponent<FollowCamera>();
            return follow != null && follow.target != null ? follow.target.GetComponent<PlayerShipController>() : null;
        }

        void Bind(PlayerShipController ship)
        {
            if (_ship != null) _ship.IslandImpacted -= OnImpact;
            _ship = ship;
            if (_ship != null) _ship.IslandImpacted += OnImpact;
        }

        void OnImpact(float impactSpeed)
        {
            if (ImpactFeedback == null || _ship == null) return;
            FeelPriority.MarkHit();
            var t = Mathf.InverseLerp(SoftHitSpeed, HardHitSpeed, impactSpeed);
            ImpactFeedback.PlayFeedbacks(ContactPoint(), Mathf.Lerp(MinIntensity, 1f, t));
        }

        /// <summary>The nearest point on an island collider around the hull, at the water line; ahead of the bow if none.</summary>
        Vector3 ContactPoint()
        {
            var center = _ship.transform.position;
            var radius = Mathf.Max(0.5f, _ship.IslandCollisionRadius);
            var best = Vector3.zero;
            var bestDistance = float.MaxValue;
            var mask = IslandObstacles.Mask;
            if (mask != 0)
            {
                var count = Physics.OverlapSphereNonAlloc(center, radius + ContactSearch, Overlap, mask, QueryTriggerInteraction.Ignore);
                for (var i = 0; i < count; i++)
                {
                    var c = Overlap[i];
                    if (c == null || !IslandObstacles.IsIslandCollider(c)) continue;
                    var concave = c is MeshCollider mesh && !mesh.convex;
                    var p = concave ? c.bounds.ClosestPoint(center) : c.ClosestPoint(center);
                    p.y = center.y;
                    var d = (p - center).sqrMagnitude;
                    if (d < 1e-4f) continue; // the ship is inside this collider's bounds (concave foliage): no direction
                    if (d < bestDistance) { bestDistance = d; best = p; }
                }
            }
            if (bestDistance < float.MaxValue && bestDistance > 1e-4f) return best;
            var forward = _ship.MovementForward;
            forward.y = 0f;
            return center + (forward.sqrMagnitude > 1e-4f ? forward.normalized : _ship.transform.forward) * radius;
        }

        /// <summary>Editor/testing: play the impact feedback as if the ship hit at 12 m/s.</summary>
        [ContextMenu("Test Impact Shake")]
        public void TestImpact() => TestImpact(12f);

        public void TestImpact(float impactSpeed)
        {
            if (ImpactFeedback == null) ImpactFeedback = GetComponent<MMF_Player>();
            if (ImpactFeedback == null) return;
            var t = Mathf.InverseLerp(SoftHitSpeed, HardHitSpeed, impactSpeed);
            var at = _ship != null ? ContactPoint() : transform.position;
            ImpactFeedback.PlayFeedbacks(at, Mathf.Lerp(MinIntensity, 1f, t));
        }
    }
}
