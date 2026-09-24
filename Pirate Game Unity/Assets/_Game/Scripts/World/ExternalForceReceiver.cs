using HighSeas.Player;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class ExternalForceReceiver : MonoBehaviour
    {
        const float MaxExternalSpeed = 4.5f;
        const float Damping = 4f;
        const float BoundsMargin = 12f;
        const float BoundsReturnStrength = 10f;

        Vector3 _externalVelocity;

        /// <summary>Adds horizontal drift velocity (m/s).</summary>
        public void AddImpulse(Vector3 metersPerSecond) => _externalVelocity += metersPerSecond;

        public void BlendToward(Vector3 targetMetersPerSecond, float blendPerSecond)
        {
            _externalVelocity = Vector3.Lerp(
                _externalVelocity,
                targetMetersPerSecond,
                Time.deltaTime * blendPerSecond);
        }

        public void ClearForces() => _externalVelocity = Vector3.zero;

        void Update()
        {
            var ship = GetComponent<PlayerShipController>();
            if (ship == null) return;

            ApplyBoundsSteering(ship.Position);

            _externalVelocity = Vector3.ClampMagnitude(_externalVelocity, MaxExternalSpeed);
            if (_externalVelocity.sqrMagnitude < 0.0001f)
                return;

            var next = ship.transform.position + _externalVelocity * Time.deltaTime;
            var hullRadius = 1.15f;
            next = PlayBounds.Clamp(next, PlayBounds.StormEdgeMargin + hullRadius);
            ship.transform.position = next;

            _externalVelocity = Vector3.Lerp(_externalVelocity, Vector3.zero, Time.deltaTime * Damping);
        }

        void ApplyBoundsSteering(Vector3 position)
        {
            if (PlayBounds.IsInside(position, BoundsMargin))
                return;

            var toCenter = PlayBounds.Center - position;
            toCenter.y = 0f;
            if (toCenter.sqrMagnitude < 0.01f)
                return;

            var outward = Vector3.Dot(_externalVelocity, toCenter.normalized);
            if (outward < 0f)
                _externalVelocity -= toCenter.normalized * outward;

            _externalVelocity += toCenter.normalized * (BoundsReturnStrength * Time.deltaTime);
        }
    }
}
