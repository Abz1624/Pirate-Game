using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Platform;
using UnityEngine;

namespace HighSeas.Player
{
    [RequireComponent(typeof(DamageReceiver))]
    public sealed class PlayerShipController : MonoBehaviour
    {
        [SerializeField] Transform visualRoot;
        [SerializeField] LayerMask obstacleMask;

        IMoveInput _input;
        Camera _camera;
        Vector3 _velocity;
        Vector3 _accelRef;
        float _visualYaw;
        DamageReceiver _damage;

        public Vector3 Position => transform.position;
        public Vector3 Velocity => _velocity;
        public DamageReceiver Damage => _damage;

        void Awake()
        {
            _damage = GetComponent<DamageReceiver>();
            _camera = Camera.main;
        }

        void Start()
        {
            var joystick = FindFirstObjectByType<VirtualJoystick>();
            _input = new CompositeMoveInput(joystick);
        }

        public void InitializeFromRun(float hull)
        {
            if (_damage == null) _damage = GetComponent<DamageReceiver>();
            _damage.Initialize(hull);
        }

        void Update()
        {
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;
            TickMovement(Time.deltaTime);
        }

        public void TickMovement(float dt)
        {
            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;

            var input = _input?.ReadMove() ?? Vector2.zero;
            if (input.sqrMagnitude > 1f) input.Normalize();

            var camForward = _camera.transform.forward;
            camForward.y = 0f;
            if (camForward.sqrMagnitude < 0.001f) camForward = Vector3.forward;
            camForward.Normalize();
            var camRight = _camera.transform.right;
            camRight.y = 0f;
            camRight.Normalize();

            var worldDir = camRight * input.x + camForward * input.y;
            var moveSpeed = RunManager.Instance != null
                ? RunManager.Instance.Stats.MoveSpeed
                : GameConstants.DefaultMoveSpeed;
            var targetVelocity = worldDir * moveSpeed;
            _velocity = Vector3.SmoothDamp(_velocity, targetVelocity, ref _accelRef, GameConstants.SmoothDampTime);

            var delta = _velocity * dt;
            var nextPos = transform.position + delta;
            nextPos = SlideAgainstObstacles(transform.position, nextPos);
            transform.position = nextPos;

            if (worldDir.sqrMagnitude > 0.001f)
            {
                var targetYaw = Mathf.Atan2(worldDir.x, worldDir.z) * Mathf.Rad2Deg;
                _visualYaw = Mathf.LerpAngle(_visualYaw, targetYaw, 1f - Mathf.Exp(-GameConstants.VisualYawLerp * dt));
                var rotRoot = visualRoot != null ? visualRoot : transform;
                rotRoot.rotation = Quaternion.Euler(0f, _visualYaw, 0f);
            }
        }

        Vector3 SlideAgainstObstacles(Vector3 from, Vector3 to)
        {
            var delta = to - from;
            if (delta.sqrMagnitude < 0.0001f) return to;
            if (!Physics.SphereCast(from + Vector3.up * 0.5f, 0.8f, delta.normalized, out var hit, delta.magnitude, obstacleMask))
                return to;

            var slide = Vector3.ProjectOnPlane(delta, hit.normal);
            slide.y = 0f;
            return from + slide;
        }
    }
}
