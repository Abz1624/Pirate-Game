using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Platform;
using HighSeas.V3.MultiShip;
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
        [Tooltip("Top forward speed in m/s (before run stat and slow multipliers).")]
        [SerializeField] float maxSpeed = 75f;
        [Tooltip("Top reverse speed in m/s (before run stat and slow multipliers).")]
        [SerializeField] float reverseMaxSpeed = 20f;
        [Tooltip("Forward acceleration in m/s².")]
        [SerializeField] float acceleration = 15;
        [Tooltip("Reverse acceleration in m/s².")]
        [SerializeField] float reverseAcceleration = 10f;
        [FormerlySerializedAs("turnRate")]
        [Tooltip("Maximum yaw speed in degrees per second at full turn effectiveness.")]
        [SerializeField] float turnSpeed = 125f;
        [Tooltip("Distance from ship center to stern pivot used when rotating.")]
        [SerializeField] float sternPivotDistance = 1.5f;
        [Tooltip("Seconds of held steer input before reaching full turn rate.")]
        [SerializeField] float turnCommitTime = 1f;
        [Range(0.05f, 1f)]
        [Tooltip("Turn rate fraction at the start of a new turn or after a direction flip.")]
        [SerializeField] float minTurnRateFraction = 0.6f;
        [Tooltip("How quickly turn commit resets after releasing steer input.")]
        [SerializeField] float turnCommitDecay = 7f;
        [Tooltip("How quickly residual turn rate fades after releasing steer input.")]
        [SerializeField] float turnInertiaDecay = 4f;
        [Tooltip("Pause after flipping steer (A to D or D to A) before the new turn direction applies.")]
        [SerializeField] float turnReverseDelay = 0.14f;
        [Tooltip("How quickly sideways velocity is pulled back in line with the hull.")]
        [SerializeField] float lateralGrip = 14f;
        [Tooltip("Passive water drag applied every physics frame.")]
        [SerializeField] float waterDrag = 2f;
        [Tooltip("Extra drag when braking against current travel direction.")]
        [SerializeField] float brakeDrag = 3f;

        [Header("Model")]
        [Tooltip("Ship models face -Z instead of Unity +Z forward.")]
        [SerializeField] bool modelForwardIsNegativeZ = true;
        [Tooltip("Visual root that receives roll/bank tilt.")]
        [SerializeField] Transform visualModel;
        [Tooltip("Maximum visual roll angle while turning.")]
        [SerializeField] float maxRollAngle = 15f;
        [Tooltip("How fast the visual roll catches up to its target.")]
        [SerializeField] float maxRollDegreesPerSecond = 17f;
        [Tooltip("Curve applied to speed before banking. 1 = linear with speed.")]
        [SerializeField] float bankSpeedExponent = 1f;

        [Header("Island Collision")]
        [Tooltip("Block movement through procedural island colliders.")]
        [SerializeField] bool collideWithIslands = true;
        [Tooltip("Approximate hull radius used for island collision.")]
        [SerializeField] float hullRadius = 1.15f;
        [Tooltip("Approximate hull height used for island capsule checks when mesh bounds are unavailable.")]
        [SerializeField] float hullHeight = 4.5f;
        [SerializeField] float maxHullCollisionRadius = 2.25f;

        Rigidbody _body;
        MeshCollider _hullMeshCollider;
        Vector3 _velocity;
        float _moveSpeedMultiplier = 1f;
        float _turnRateMultiplier = 1f;
        float _slowMultiplier = 1f;
        float _slowTimer;
        float _moveEffectMultiplier = 1f;
        float _moveEffectTimer;
        bool _movementEnabled = true;
        float _targetRoll;
        float _currentRoll;
        float _steerCommit;
        float _lastSteerInput;
        float _yawRate;
        float _reverseTurnTimer;
        Quaternion _visualBaseLocalRotation = Quaternion.identity;
        DamageReceiver _damage;
        CompositeShipSteerInput _compositeInput;
        IShipSteerInput _botSteerInput;
        IShipSteerInput _dedicatedSteerInput;
        bool _acceptHumanInput = true;

        public Vector3 Position => transform.position;
        public Vector3 Velocity => _velocity;
        public DamageReceiver Damage => _damage;
        public Transform VisualModel => visualModel;
        public Vector3 MovementForward => GetShipForward();

        /// <summary>Run-scaled forward speed multiplier from player Move Speed stat.</summary>
        public float MoveSpeedMultiplier
        {
            get => _moveSpeedMultiplier;
            set => _moveSpeedMultiplier = Mathf.Max(0.1f, value);
        }

        /// <summary>Multiplier applied to yaw speed from upgrades and effects.</summary>
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

            if (visualModel == null)
                visualModel = EnsureVisualPivot();
            else if (transform.Find("VisualTiltPivot") == null)
                visualModel = EnsureVisualPivot();

            if (visualModel != null)
                _visualBaseLocalRotation = visualModel.localRotation;

            SanitizeLegacyVisualComponents();
            EnsureHullCollider();
            IgnoreIslandPhysicsCollisions();
            SyncRunSpeedMultiplier();

            var joystickRoot = GameObject.Find("JoystickBG");
            var joystick = joystickRoot != null ? joystickRoot.GetComponent<VirtualJoystick>() : null;
            _compositeInput = new CompositeShipSteerInput(joystick);
        }

        public void InitializeFromRun(float hull)
        {
            if (_damage == null) _damage = GetComponent<DamageReceiver>();
            _damage.Initialize(hull);
            ResetMotion();
            SyncRunSpeedMultiplier();
        }

        public void SetMovementEnabled(bool enabled) => _movementEnabled = enabled;

        public void ConfigureBotControl(IShipSteerInput botInput)
        {
            _botSteerInput = botInput;
            _dedicatedSteerInput = null;
            _acceptHumanInput = false;
        }

        public void ConfigureDedicatedSteerInput(IShipSteerInput steerInput)
        {
            _botSteerInput = null;
            _dedicatedSteerInput = steerInput;
            _acceptHumanInput = false;
        }

        public float IslandCollisionRadius => GetHullWorldRadius();

        public float IslandCollisionHeight => GetHullWorldHeight();

        public void ApplySlow(float multiplier, float durationSeconds)
        {
            _slowMultiplier = Mathf.Clamp(multiplier, 0.2f, 1f);
            _slowTimer = Mathf.Max(_slowTimer, durationSeconds);
        }

        /// <summary>Temporary move multiplier (0.2–1.5). Stacks with slow; values above 1 are speed boosts.</summary>
        public void ApplyMoveEffect(float multiplier, float durationSeconds)
        {
            _moveEffectMultiplier = Mathf.Clamp(multiplier, 0.2f, 1.5f);
            _moveEffectTimer = Mathf.Max(_moveEffectTimer, durationSeconds);
        }

        public void SyncRunSpeedMultiplier()
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
            _steerCommit = 0f;
            _lastSteerInput = 0f;
            _yawRate = 0f;
            _reverseTurnTimer = 0f;
            if (_body != null)
            {
                _body.position = transform.position;
                _body.rotation = transform.rotation;
            }
        }

        public void SetWorldPosition(Vector3 position)
        {
            position = ResolveSpawnPosition(position, GetHullWorldRadius());
            transform.SetPositionAndRotation(position, transform.rotation);
            if (_body != null)
            {
                _body.position = position;
                _body.rotation = transform.rotation;
            }

            Physics.SyncTransforms();
            ResetMotion();
        }

        Vector3 ResolveSpawnPosition(Vector3 position, float hullRadius)
        {
            if (!IslandObstacles.IsAvailable)
                return ClampV3PlayPosition(position, hullRadius);

            var height = IslandObstacles.GetCollisionHeight(hullRadius);
            for (var i = 0; i < 12; i++)
            {
                if (!IslandObstacles.IsPositionBlocked(position, hullRadius))
                    return ClampV3PlayPosition(position, hullRadius);

                position = IslandObstacles.Depenetrate(position, hullRadius, height);
            }

            for (var ring = 1; ring <= 16; ring++)
            {
                for (var i = 0; i < 12; i++)
                {
                    var angle = (i / 12f) * Mathf.PI * 2f;
                    var candidate = position + new Vector3(Mathf.Cos(angle) * ring * 2.5f, 0f, Mathf.Sin(angle) * ring * 2.5f);
                    if (!IslandObstacles.IsPositionBlocked(candidate, hullRadius))
                        return ClampV3PlayPosition(candidate, hullRadius);
                }
            }

            return ClampV3PlayPosition(position, hullRadius);
        }

        float GetPlayBoundsMargin(float hullRadius)
        {
            if (!V3.V3RunContext.IsActive)
                return hullRadius + 0.25f;

            if (V3RunSession.IsMultiShip)
            {
                var combatant = GetComponent<V3ShipCombatant>();
                if (combatant != null && !combatant.IsLocalHuman)
                    return V3MultiShipBotBounds.BotPlayMargin(hullRadius);
            }

            return hullRadius + 0.25f;
        }

        Vector3 ClampV3PlayPosition(Vector3 position, float hullRadius) => position;

        void FixedUpdate()
        {
            if (!_movementEnabled) return;
            if (_damage != null && !_damage.IsAlive) return;
            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded) return;
            if (RunManager.Instance != null && RunManager.Instance.IsPaused) return;

            if (_slowTimer > 0f)
            {
                _slowTimer -= Time.fixedDeltaTime;
                if (_slowTimer <= 0f)
                    _slowMultiplier = 1f;
            }

            if (_moveEffectTimer > 0f)
            {
                _moveEffectTimer -= Time.fixedDeltaTime;
                if (_moveEffectTimer <= 0f)
                    _moveEffectMultiplier = 1f;
            }

            var moveMult = _slowMultiplier * _moveEffectMultiplier;
            var dt = Time.fixedDeltaTime;
            var input = ReadShipInput();
            var forward = GetShipForward();
            var forwardSpeed = Vector3.Dot(_velocity, forward);
            var effectiveMaxSpeed = maxSpeed * _moveSpeedMultiplier * moveMult;
            var effectiveReverseMaxSpeed = reverseMaxSpeed * _moveSpeedMultiplier * moveMult;
            var turnReferenceSpeed = forwardSpeed >= 0f ? effectiveMaxSpeed : effectiveReverseMaxSpeed;
            var turnEffectiveness = turnReferenceSpeed > 0f
                ? Mathf.Clamp01(Mathf.Abs(forwardSpeed) / turnReferenceSpeed)
                : 0f;
            if (turnEffectiveness <= 0f && Mathf.Abs(input.steer) > 0.01f)
                turnEffectiveness = 0.35f;

            var hasSteerInput = Mathf.Abs(input.steer) > 0.01f;
            var steerReversed = hasSteerInput &&
                                Mathf.Sign(input.steer) != Mathf.Sign(_lastSteerInput) &&
                                Mathf.Abs(_lastSteerInput) > 0.01f;

            if (hasSteerInput && turnEffectiveness > 0f)
            {
                if (steerReversed)
                {
                    _reverseTurnTimer = turnReverseDelay;
                    _steerCommit = 0f;
                    _yawRate = 0f;
                }

                if (_reverseTurnTimer > 0f)
                {
                    _reverseTurnTimer = Mathf.Max(0f, _reverseTurnTimer - dt);
                    _yawRate = 0f;
                }
                else
                {
                    if (_steerCommit < 1f)
                    {
                        var commitRamp = turnCommitTime > 0.01f ? dt / turnCommitTime : 1f;
                        _steerCommit = Mathf.MoveTowards(_steerCommit, 1f, commitRamp);
                    }

                    var commitMultiplier = Mathf.Lerp(minTurnRateFraction, 1f, _steerCommit);
                    _yawRate = input.steer * turnSpeed * _turnRateMultiplier * turnEffectiveness * commitMultiplier;
                }
            }
            else if (!hasSteerInput)
            {
                _reverseTurnTimer = 0f;
                _steerCommit = Mathf.MoveTowards(_steerCommit, 0f, turnCommitDecay * dt);
                _yawRate *= Mathf.Exp(-turnInertiaDecay * dt);
                if (Mathf.Abs(_yawRate) < 0.12f)
                    _yawRate = 0f;
            }
            else
            {
                _yawRate = 0f;
            }

            if (Mathf.Abs(_yawRate) > 0.01f)
            {
                var coastScale = hasSteerInput ? 1f : Mathf.Lerp(0.6f, 1f, turnEffectiveness);
                var deltaYaw = _yawRate * coastScale * dt;
                var deltaRotation = Quaternion.Euler(0f, deltaYaw, 0f);
                _velocity = deltaRotation * _velocity;
                _body.MoveRotation(_body.rotation * deltaRotation);
            }

            _lastSteerInput = input.steer;

            forward = GetShipForward();
            var bankForward = forward;
            var bankForwardSpeed = Vector3.Dot(_velocity, bankForward);
            var speedRatio = effectiveMaxSpeed > 0.01f
                ? Mathf.Clamp01(Mathf.Abs(bankForwardSpeed) / effectiveMaxSpeed)
                : 0f;
            if (bankSpeedExponent != 1f)
                speedRatio = Mathf.Pow(speedRatio, bankSpeedExponent);
            var desiredRoll = -input.steer * maxRollAngle * speedRatio;
            _targetRoll = Mathf.MoveTowards(_targetRoll, desiredRoll, maxRollDegreesPerSecond * dt);

            if (Mathf.Abs(input.throttle) > 0.01f)
            {
                var thrust = input.throttle > 0f ? acceleration : reverseAcceleration;
                _velocity += forward * (input.throttle * thrust * moveMult * dt);

                forwardSpeed = Vector3.Dot(_velocity, forward);
                if (Mathf.Sign(input.throttle) != Mathf.Sign(forwardSpeed) && Mathf.Abs(forwardSpeed) > 0.1f)
                    _velocity *= Mathf.Max(0f, 1f - brakeDrag * dt);
            }

            _velocity *= Mathf.Max(0f, 1f - waterDrag * dt);
            _velocity = ClampSpeed(_velocity, forward, effectiveMaxSpeed, effectiveReverseMaxSpeed);
            _velocity = ApplyLateralGrip(_velocity, forward, dt);

            var movementDelta = _velocity * dt;
            var nextPosition = _body.position + movementDelta;

            var hullRadius = GetHullWorldRadius();
            var hullHeight = GetHullWorldHeight();
            if (collideWithIslands && IslandObstacles.IsAvailable)
            {
                if (movementDelta.sqrMagnitude < 1e-8f &&
                    IslandObstacles.IsPositionBlocked(_body.position, hullRadius, hullHeight))
                {
                    var corrected = IslandObstacles.Depenetrate(_body.position, hullRadius, hullHeight);
                    var correction = corrected - _body.position;
                    correction.y = 0f;
                    if (correction.sqrMagnitude <= 0.25f * 0.25f)
                        nextPosition = corrected;
                }
                else
                {
                    nextPosition = IslandObstacles.ResolveCapsuleMovement(
                        _body.position,
                        movementDelta,
                        hullRadius,
                        hullHeight,
                        ref _velocity);
                }
            }
            else
            {
                nextPosition = _body.position + movementDelta;
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

            _currentRoll = Mathf.Lerp(_currentRoll, _targetRoll, 1f - Mathf.Exp(-maxRollDegreesPerSecond * Time.deltaTime));
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

        Vector3 ApplyLateralGrip(Vector3 velocity, Vector3 forward, float dt)
        {
            if (velocity.sqrMagnitude < 0.0001f || lateralGrip <= 0f)
                return velocity;

            var forwardSpeed = Vector3.Dot(velocity, forward);
            var lateral = velocity - forward * forwardSpeed;
            var grip = Mathf.Exp(-lateralGrip * dt);
            return forward * forwardSpeed + lateral * grip;
        }

        Vector3 GetShipForward()
        {
            var forward = _body != null ? _body.rotation * Vector3.forward : transform.forward;
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

        ShipInput ReadShipInput()
        {
            var input = new ShipInput();
            if (_dedicatedSteerInput != null)
            {
                var dedicated = _dedicatedSteerInput.ReadSteerThrottle();
                input.steer = dedicated.x;
                input.throttle = dedicated.y;
            }
            else if (_botSteerInput != null)
            {
                var bot = _botSteerInput.ReadSteerThrottle();
                input.steer = bot.x;
                input.throttle = bot.y;
            }
            else if (_acceptHumanInput)
            {
                if (_compositeInput != null)
                {
                    var composite = _compositeInput.ReadSteerThrottle();
                    input.steer = composite.x;
                    input.throttle = composite.y;
                }

                if (Gamepad.current != null)
                {
                    var stick = Gamepad.current.leftStick.ReadValue();
                    if (Mathf.Abs(stick.x) > Mathf.Abs(input.steer))
                        input.steer = stick.x;
                    if (Mathf.Abs(stick.y) > Mathf.Abs(input.throttle))
                        input.throttle = stick.y;
                }
            }

            input.throttle = Mathf.Clamp(input.throttle, -1f, 1f);
            input.steer = Mathf.Clamp(input.steer, -1f, 1f);
            return input;
        }

        void IgnoreIslandPhysicsCollisions()
        {
            var islandLayer = IslandObstacles.Layer;
            if (islandLayer < 0) return;
            Physics.IgnoreLayerCollision(gameObject.layer, islandLayer, true);
        }

        void EnsureHullCollider()
        {
            foreach (var legacyCapsule in GetComponents<CapsuleCollider>())
                Destroy(legacyCapsule);

            if (_hullMeshCollider != null && _hullMeshCollider.gameObject != gameObject)
            {
                var meshFilter = FindPrimaryHullMeshFilter();
                if (meshFilter == null || _hullMeshCollider.gameObject != meshFilter.gameObject)
                    Destroy(_hullMeshCollider);
            }

            var source = FindPrimaryHullMeshFilter();
            if (source == null || source.sharedMesh == null)
            {
                _hullMeshCollider = GetComponent<MeshCollider>();
                if (_hullMeshCollider == null)
                    _hullMeshCollider = gameObject.AddComponent<MeshCollider>();
            }
            else
            {
                _hullMeshCollider = source.GetComponent<MeshCollider>();
                if (_hullMeshCollider == null)
                    _hullMeshCollider = source.gameObject.AddComponent<MeshCollider>();

                _hullMeshCollider.sharedMesh = source.sharedMesh;
                _hullMeshCollider.convex = true;
            }

            if (_hullMeshCollider != null)
            {
                // Trigger only: island blocking uses capsule math from mesh footprint, not rigidbody solver.
                _hullMeshCollider.isTrigger = true;
                ApplyHullPhysicsLayer();
            }

            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        void ApplyHullPhysicsLayer()
        {
            if (_hullMeshCollider == null) return;
            var layer = gameObject.layer;
            _hullMeshCollider.gameObject.layer = layer;
        }

        MeshFilter FindPrimaryHullMeshFilter()
        {
            if (visualModel == null) return null;

            var filters = visualModel.GetComponentsInChildren<MeshFilter>(true);
            MeshFilter best = null;
            var bestVolume = 0f;
            for (var i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                if (mesh == null) continue;
                var size = mesh.bounds.size;
                var volume = size.x * size.y * size.z;
                if (volume > bestVolume)
                {
                    bestVolume = volume;
                    best = filters[i];
                }
            }

            return best;
        }

        Mesh FindHullMesh() => FindPrimaryHullMeshFilter()?.sharedMesh;

        float GetHullWorldRadius()
        {
            var scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
            var baseline = hullRadius * scale;
            var filter = FindPrimaryHullMeshFilter();
            if (filter == null || filter.sharedMesh == null)
                return baseline;

            var ext = filter.sharedMesh.bounds.extents;
            var meshScale = filter.transform.lossyScale;
            var horizontal = Mathf.Max(
                ext.x * Mathf.Abs(meshScale.x),
                ext.z * Mathf.Abs(meshScale.z));
            var footprint = horizontal * 0.42f + 0.35f;
            return Mathf.Clamp(footprint, baseline * 0.85f, maxHullCollisionRadius * scale);
        }

        float GetHullWorldHeight()
        {
            var filter = FindPrimaryHullMeshFilter();
            if (filter != null && filter.sharedMesh != null)
            {
                var ext = filter.sharedMesh.bounds.extents;
                var height = ext.y * 2f * Mathf.Abs(filter.transform.lossyScale.y);
                return Mathf.Clamp(height, 1.5f, hullHeight * 1.35f);
            }

            return hullHeight * Mathf.Abs(transform.lossyScale.y);
        }

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
                if (col == _hullMeshCollider) continue;
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
