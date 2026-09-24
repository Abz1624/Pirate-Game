using HighSeas.Player;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class ExternalForceReceiver : MonoBehaviour
    {
        Vector3 _externalVelocity;

        public void AddImpulse(Vector3 worldDeltaPerSecond) => _externalVelocity += worldDeltaPerSecond;

        void Update()
        {
            var ship = GetComponent<PlayerShipController>();
            if (ship == null || _externalVelocity.sqrMagnitude < 0.0001f) return;
            ship.transform.position += _externalVelocity * Time.deltaTime;
            _externalVelocity = Vector3.Lerp(_externalVelocity, Vector3.zero, Time.deltaTime * 2f);
        }
    }
}
