using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace HighSeas.Player
{
    /// <summary>
    /// Player ship: movement matches references/scripts/ShipMovement (1).cs plus visual tilt + islands.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(DamageReceiver))]
    public sealed class PlayerShipController : MonoBehaviour
    {
        const float ReferenceMaxSpeed = 50f;

        [Header("Handling")]
        [SerializeField] float maxSpeed = 50f;
        [SerializeField] float reverseMaxSpeed = 15f;
        [SerializeField] float acceleration = 15f;
        [SerializeField] float reverseAcceleration = 4f;
        [FormerlySerializedAs("turnRate")]
        [SerializeField] float turnSpeed = 800f;
        [SerializeField] float waterDrag = 3f;
        [SerializeField] float brakeDrag = 6f;

        [Header("Model")]
        [Tooltip("Ship models face -Z instead of Unity +Z forward.")]
        [SerializeField] bool modelForwardIsNegativeZ = true;
        [SerializeField] Transform visualModel;
        [SerializeField] float maxRollAngle = 26f;
        [SerializeField] float rollSmoothing = 12f;
        [SerializeField] float rollTargetSmoothing = 3.5f;
        [SerializeField] float bankSpeedExponent = 1f;

        [Header("Island Collision")]
        [SerializeField] bool collideWithIslands = true;
        [SerializeField] float hullRadius = 1.15f;
        [SerializeField] float hullHeight = 4.5f;

        Rigidbody _body;
        CapsuleCollider _hullCollider;
        Vector3 _velocity;
        float _moveSpeedMultiplier = 1f;
        float _turnRateMultiplier = 1f;
        float _slowMultiplier = 1f;
        float _slowTimer;
        float _targetRoll;
        float _currentRoll;
        Quaternion _visualBaseLocalRotation = Quaternion.identity;
        DamageReceiver _damage;

        public Vector3 Position => transform.position;
        public Vector3 Velocity => _velocity;
        public DamageReceiver Damage => _damage;
        public Transform VisualModel => visualModel;
        public Vector3 MovementForward => GetShipForward();

        public float MoveSpeedMultiplier
        {
            get => _moveSpeedMultiplier;
            set => _moveSpeedMultiplier = Mathf.Max(0.1f, value);
        }

        public float TurnRateMultiplier
        {
            get => _turnRateMultiplier;
            set => _turnRateMultiplier = Mathf.Max(0.1f, value);
        }

        void Awake()
        {
            _damage = GetComponent<DamageReceiver>();
            _body = GetComponent<Rigidbody>();
            _body.useGravity = false;
            _body.isKinematic = true;
            _body.constraints = RigidbodyConstraints.FreezePositionY |
                               RigidbodyConstraints.FreezeRotationX |
                               RigidbodyConstraints.FreezeRotationZ;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            EnsureHullCollider();

            if (visualModel == null)
                visualModel = EnsureVisualPivot();
            else if (transform.Find("VisualTiltPivot") == null)
                visualModel = EnsureVisualPivot();

            if (visualModel != null)
                _visualBaseLocalRotation = visualModel.localRotation;

            SanitizeLegacyVisualComponents();
            SyncRunSpeedMultiplier();
        }

        public void InitializeFromRun(float hull)
        {
            if (_damage == null) _damage = GetComponent<DamageReceiver>();
            _damage.Initialize(hull);
            ResetMotion();
            SyncRunSpeedMultiplier();
        }

        public void ApplySlow(float multiplier, float durationSeconds)
        {
            _slowMultiplier = Mathf.Clamp(multiplier, 0.2f, 1f);
            _slowTimer = Mathf.Max(_slowTimer, durationSeconds);
        }

        void SyncRunSpeedMultiplier()
        {
            var statSpeed = RunManager.Instance != null
                ? RunManager.Instance.Stats.MoveSpeed
                : GameConstants.DefaultMoveSpeed;
            _moveSpeedMultiplier = statSpeed / ReferenceMaxSpeed;
        }

        public void ResetMotion()
        {
            _velocity = Vector3.zero;
            _targetRoll = 0f;
            _currentRoll = 0f;
            if (_body != null)
            {
                _body.position = transform.position;
                _body.rotation = transform.rotation;
            }
        }

        void FixedUpdate()
        {
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;

            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.fixedDeltaTime;
                if (_slowTimer <= 0f)
                    _slowMultiplier = 1f;
            }

            var dt = Time.fixedDeltaTime;
            var input = ReadShipInput();
            var forward = GetShipForward();
            var forwardSpeed = Vector3.Dot(_velocity, forward);
            var effectiveMaxSpeed = maxSpeed * _moveSpeedMultiplier * _slowMultiplier;
            var effectiveReverseMaxSpeed = reverseMaxSpeed * _moveSpeedMultiplier * _slowMultiplier;
            var turnReferenceSpeed = forwardSpeed >= 0f ? effectiveMaxSpeed : effectiveReverseMaxSpeed;
            var turnEffectiveness = turnReferenceSpeed > 0f
                ? Mathf.Clamp01(Mathf.Abs(forwardSpeed) / turnReferenceSpeed)
                : 0f;
            if (turnEffectiveness <= 0f && Mathf.Abs(input.steer) > 0.01f)
                turnEffectiveness = 0.35f;

            if (Mathf.Abs(input.steer) > 0.01f && turnEffectiveness > 0f)
            {
                var deltaYaw = input.steer * turnSpeed * _turnRateMultiplier * turnEffectiveness * dt;
                _body.MoveRotation(_body.rotation * Quaternion.Euler(0f, deltaYaw, 0f));
            }

            var bankForward = GetShipForward();
            var bankForwardSpeed = Vector3.Dot(_velocity, bankForward);
            var speedRatio = effectiveMaxSpeed > 0.01f
                ? Mathf.Clamp01(Mathf.Abs(bankForwardSpeed) / effectiveMaxSpeed)
                : 0f;
            if (bankSpeedExponent != 1f)
                speedRatio = Mathf.Pow(speedRatio, bankSpeedExponent);
            var desiredRoll = -input.steer * maxRollAngle * speedRatio;
            _targetRoll = Mathf.Lerp(_targetRoll, desiredRoll, 1f - Mathf.Exp(-rollTargetSmoothing * dt));

            if (Mathf.Abs(input.throttle) > 0.01f)
            {
                var thrust = input.throttle > 0f ? acceleration : reverseAcceleration;
                _velocity += forward * (input.throttle * thrust * _slowMultiplier * dt);

                forwardSpeed = Vector3.Dot(_velocity, forward);
                if (Mathf.Sign(input.throttle) != Mathf.Sign(forwardSpeed) && Mathf.Abs(forwardSpeed) > 0.1f)
                    _velocity *= Mathf.Max(0f, 1f - brakeDrag * dt);
            }

            _velocity *= Mathf.Max(0f, 1f - waterDrag * dt);
            _velocity = ClampSpeed(_velocity, forward, effectiveMaxSpeed, effectiveReverseMaxSpeed);

            var delta = _velocity * dt;
            var nextPosition = _body.position;

            if (collideWithIslands && IslandObstacles.IsAvailable)
            {
                nextPosition = IslandObstacles.ResolveCapsuleMovement(
                    _body.position,
                    delta,
                    GetHullWorldRadius(),
                    GetHullWorldHeight(),
                    ref _velocity);
            }
            else
            {
                nextPosition += delta;
            }

            nextPosition.y = _body.position.y;
            _body.MovePosition(nextPosition);
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
        }

        void LateUpdate()
        {
            if (visualModel == null) return;

            _currentRoll = Mathf.Lerp(_currentRoll, _targetRoll, 1f - Mathf.Exp(-rollSmoothing * Time.deltaTime));
            var rollAxis = modelForwardIsNegativeZ ? Vector3.back : Vector3.forward;
            visualModel.localRotation = _visualBaseLocalRotation * Quaternion.AngleAxis(_currentRoll, rollAxis);
        }

        public void TickMovement(float dt)
        {
            var wasFixed = Time.fixedDeltaTime;
            try
            {
                Time.fixedDeltaTime = dt;
                FixedUpdate();
            }
            finally
            {
                Time.fixedDeltaTime = wasFixed;
            }
        }

        Vector3 GetShipForward()
        {
            var forward = _body != null
                ? _body.rotation * Vector3.forward
                : transform.forward;
            if (modelForwardIsNegativeZ)
                forward = -forward;

            forward.y = 0f;
            return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }

        static Vector3 ClampSpeed(Vector3 currentVelocity, Vector3 forward, float maxForward, float maxReverse)
        {
            var speed = currentVelocity.magnitude;
            if (speed < 0.0001f)
                return Vector3.zero;

            var forwardSpeed = Vector3.Dot(currentVelocity, forward);
            var maxAllowed = forwardSpeed >= 0f ? maxForward : maxReverse;

            if (speed <= maxAllowed)
                return currentVelocity;

            return currentVelocity / speed * maxAllowed;
        }

        static ShipInput ReadShipInput()
        {
            var input = new ShipInput();

            if (Gamepad.current != null)
            {
                var stick = Gamepad.current.leftStick.ReadValue();
                input.throttle += stick.y;
                input.steer += stick.x;
            }

            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                    input.throttle += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                    input.throttle -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                    input.steer -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                    input.steer += 1f;
            }

            input.throttle = Mathf.Clamp(input.throttle, -1f, 1f);
            input.steer = Mathf.Clamp(input.steer, -1f, 1f);
            return input;
        }

        void EnsureHullCollider()
        {
            _hullCollider = GetComponent<CapsuleCollider>();
            if (_hullCollider == null)
            {
                _hullCollider = gameObject.AddComponent<CapsuleCollider>();
                _hullCollider.center = Vector3.zero;
                _hullCollider.radius = hullRadius;
                _hullCollider.height = hullHeight;
                _hullCollider.direction = 2;
            }

            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        float GetHullWorldRadius()
        {
            var scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            return _hullCollider.radius * scale;
        }

        float GetHullWorldHeight() =>
            _hullCollider.height * Mathf.Abs(transform.lossyScale.y);

        Transform EnsureVisualPivot()
        {
            var existing = transform.Find("VisualTiltPivot");
            if (existing != null)
                return existing;

            var pivot = new GameObject("VisualTiltPivot").transform;
            pivot.SetParent(transform, false);

            var children = new Transform[transform.childCount];
            for (var i = 0; i < children.Length; i++)
                children[i] = transform.GetChild(i);

            foreach (var child in children)
                child.SetParent(pivot, true);

            return pivot;
        }

        void SanitizeLegacyVisualComponents()
        {
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null) meshRenderer.enabled = false;

            foreach (var rb in GetComponentsInChildren<Rigidbody>(true))
            {
                if (rb == _body) continue;
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            foreach (var col in GetComponentsInChildren<Collider>(true))
            {
                if (col == _hullCollider) continue;
                col.enabled = false;
            }

            if (visualModel == null) return;
            DisableByName(visualModel, "HealthBar");
            DisableByName(visualModel, "Aim");
        }

        static void DisableByName(Transform root, string objectName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                    t.gameObject.SetActive(false);
            }
        }

        struct ShipInput
        {
            public float throttle;
            public float steer;
        }
    }
}
