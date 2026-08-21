using HighSeas.Core;
using UnityEngine;

namespace HighSeas.Player
{
    public sealed class FollowCamera : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Vector3 offset = new(0f, 18f, -14f);
        [SerializeField] float smoothTime = 0.15f;
        [SerializeField] float lookAheadMax = 0.8f;

        Vector3 _vel;
        PlayerShipController _ship;

        void LateUpdate()
        {
            if (target == null)
            {
                _ship = FindFirstObjectByType<PlayerShipController>();
                if (_ship != null) target = _ship.transform;
                if (target == null) return;
            }

            var lookAhead = Vector3.zero;
            if (_ship != null)
            {
                lookAhead = _ship.Velocity.normalized * lookAheadMax;
                lookAhead.y = 0f;
            }

            var desired = target.position + offset + lookAhead;
            transform.position = Vector3.SmoothDamp(transform.position, desired, ref _vel, smoothTime);
            transform.rotation = Quaternion.Euler(GameConstants.CameraPitch, 0f, 0f);
        }
    }
}
