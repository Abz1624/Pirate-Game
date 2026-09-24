using HighSeas.Player;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class OceanCurrentZone : MonoBehaviour
    {
        [SerializeField] Vector3 flowDirection = Vector3.forward;
        [SerializeField] float pushStrength = 1.35f;

        BoxCollider _box;

        void Awake()
        {
            if (EnvironmentalHazardsDisabled.Value)
                Destroy(gameObject);
        }

        public void Configure(Vector3 direction, float strength = 1.35f)
        {
            flowDirection = direction;
            flowDirection.y = 0f;
            if (flowDirection.sqrMagnitude < 0.01f)
                flowDirection = Vector3.forward;
            flowDirection.Normalize();
            pushStrength = strength;

            _box = GetComponent<BoxCollider>();
            if (_box != null)
                EnvironmentHazardVisual.AttachOceanCurrent(gameObject, flowDirection, _box.size);
        }

        void OnTriggerStay(Collider other)
        {
            var receiver = other.GetComponent<ExternalForceReceiver>();
            if (receiver == null) return;

            var dir = flowDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f)
                dir = transform.forward;
            receiver.BlendToward(dir.normalized * pushStrength, 3.5f);
        }
    }
}
