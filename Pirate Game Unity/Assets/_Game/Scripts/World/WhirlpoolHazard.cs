using HighSeas.Combat;
using HighSeas.Player;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class WhirlpoolHazard : MonoBehaviour
    {
        [SerializeField] float pullStrength = 2.2f;
        [SerializeField] float damagePerSecond = 5f;
        [SerializeField] float radius = 10f;

        void Awake()
        {
            if (EnvironmentalHazardsDisabled.Value)
            {
                Destroy(gameObject);
                return;
            }

            var sphere = GetComponent<SphereCollider>();
            if (sphere != null)
                radius = sphere.radius;
            EnvironmentHazardVisual.AttachWhirlpool(gameObject, radius);
        }

        void Update()
        {
            var ship = Object.FindFirstObjectByType<PlayerShipController>();
            if (ship == null) return;

            var offset = ship.Position - transform.position;
            offset.y = 0f;
            var dist = offset.magnitude;
            if (dist > radius || dist < 0.35f) return;

            var falloff = 1f - dist / radius;
            var receiver = ship.GetComponent<ExternalForceReceiver>();
            if (receiver != null)
                receiver.BlendToward(-offset.normalized * (pullStrength * falloff), 5f);

            var dmg = ship.GetComponent<DamageReceiver>();
            if (dmg != null && damagePerSecond > 0f)
                dmg.TakeDamage(damagePerSecond * falloff * Time.deltaTime, transform.position, false);
        }
    }
}
