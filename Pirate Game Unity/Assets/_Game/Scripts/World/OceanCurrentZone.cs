using HighSeas.Player;
using UnityEngine;

namespace HighSeas.World
{
    public sealed class OceanCurrentZone : MonoBehaviour
    {
        [SerializeField] Vector3 flowDirection = Vector3.forward;
        [SerializeField] float pushStrength = 4f;

        public void Configure(Vector3 direction, float strength = 4f)
        {
            flowDirection = direction;
            pushStrength = strength;
        }

        void OnTriggerStay(Collider other)
        {
            var receiver = other.GetComponent<ExternalForceReceiver>();
            if (receiver == null) return;
            var dir = flowDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = transform.forward;
            receiver.AddImpulse(dir.normalized * pushStrength);
        }
    }
}
