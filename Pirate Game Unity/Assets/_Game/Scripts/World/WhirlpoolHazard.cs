using HighSeas.Combat;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class WhirlpoolHazard : MonoBehaviour
    {
        [SerializeField] float pullStrength = 6f;
        [SerializeField] float damagePerSecond = 8f;
        [SerializeField] float radius = 12f;

        void Update()
        {
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            if (ship == null) return;
            var offset = ship.Position - transform.position;
            offset.y = 0f;
            var dist = offset.magnitude;
            if (dist > radius) return;

            var receiver = ship.GetComponent<ExternalForceReceiver>();
            if (receiver != null && dist > 0.5f)
                receiver.AddImpulse(-offset.normalized * pullStrength);

            var dmg = ship.GetComponent<DamageReceiver>();
            if (dmg != null && damagePerSecond > 0f)
                dmg.TakeDamage(damagePerSecond * Time.deltaTime, transform.position, false);
        }
    }
}
