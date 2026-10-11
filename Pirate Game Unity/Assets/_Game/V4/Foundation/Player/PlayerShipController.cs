using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Foundation.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace RoguePirates.V4.Foundation.Player
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

        /// <summary>
        /// Where the ship is for the game (V5 M9b close, determinism: the simulated position, <see cref="SimPosition"/>; was
        /// the transform's). The transform is interpolated between physics steps for drawing (Rigidbody interpolation), by
        /// the frame's render phase (Time.time against Time.fixedTime), so gameplay reading it made two plays of one seed
        /// with the same inputs diverge. What is drawn on the ship reads <see cref="DrawnPosition"/>.
        /// </summary>
        public Vector3 Position => SimPosition;
        /// <summary>V5 M9b part 2b: the simulated position (the body's; the transform is interpolated between physics steps
        /// for drawing, so it trails the body on a ship the net tick steps).</summary>
        public Vector3 SimPosition => _body != null ? _body.position : transform.position;
        /// <summary>V5 M9b close: the simulated rotation (the body's), the gameplay twin of the drawn transform's.</summary>
        public Quaternion SimRotation => _body != null ? _body.rotation : transform.rotation;
        /// <summary>V5 M9c S0 (determinism): the ship's scale from its hierarchy's local scales (lossyScale comes from the
        /// drawn world matrix and changes in its last bits with the interpolated rotation).</summary>
        public Vector3 SimScale => RestScale(transform);
        /// <summary>V5 M9b close: where the ship is drawn this frame (the interpolated transform): presentation only.</summary>
        public Vector3 DrawnPosition => transform.position;
        public Vector3 Velocity => _velocity + _knockback;
        public DamageReceiver Damage => _damage;
        public Transform VisualModel => visualModel;
        public Vector3 MovementForward => GetShipForward();

        public void SetModelForwardIsNegativeZ(bool negativeZ) => modelForwardIsNegativeZ = negativeZ;

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

            // The stick is authored inactive and switched on at run start in the mobile app: find it inactive too, or
            // touch steering never reaches the ship (an inactive stick reads zero).
            VirtualJoystick joystick = null;
            foreach (var j in FindObjectsByType<VirtualJoystick>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (j.gameObject.scene == gameObject.scene) { joystick = j; break; }
            _compositeInput = new CompositeShipSteerInput(joystick, GetShipForward); // the stick points where to sail
        }

        public void InitializeFromRun(float hull)
        {
            if (_damage == null) _damage = GetComponent<DamageReceiver>();
            _damage.Initialize(hull);
            ResetMotion();
            SyncRunSpeedMultiplier();
        }

        public void SetMovementEnabled(bool enabled) => _movementEnabled = enabled;

        /// <summary>V5 M9a (I9a.1): the steer and throttle the last physics step used (the run log samples it).</summary>
        public Vector2 LastSteerThrottle { get; private set; }

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

        /// <summary>The run's ship speed factor (RunConfig.shipSpeedMultiplier, set by the run bootstrap): top speed and
        /// acceleration, on top of the move-speed stat. A config-level exception to "stats change only through the
        /// resolver": it is a per-map tuning factor (the demo's switch, now RunConfig), not a build modifier.</summary>
        public float RunSpeedMultiplier { get; set; } = 1f;

        /// <summary>V5 (owner, 1 Oct 2026): hitting an island bounces the ship back and costs hull, so it never has to
        /// reverse off a shore. Set by the run bootstrap from RunConfig; all 0 = V3 (slide along, no damage).</summary>
        public IslandImpactSettings IslandImpact { get; set; }

        [System.Serializable]
        public struct IslandImpactSettings
        {
            /// <summary>Only hits faster than this (m/s into the shore) bounce and hurt.</summary>
            public float MinSpeed;
            /// <summary>Share of the impact speed given back as bounce, plus a fixed push-off speed (m/s).</summary>
            public float Restitution, PushSpeed;
            /// <summary>Hull lost per impact; seconds before another impact can bounce, and before another can hurt.</summary>
            public float Damage, Cooldown, DamageCooldown;
            /// <summary>Seconds the bounce's push takes to fade out.</summary>
            public float BounceSeconds;
            public bool Enabled => Restitution > 0f || PushSpeed > 0f || Damage > 0f;
        }

        /// <summary>Tests and the playable check: put the ship somewhere with a given velocity.</summary>
        public void PlaceForTest(Vector3 position, Vector3 velocity)
        {
            position.y = _body != null ? _body.position.y : transform.position.y;
            if (_body != null) _body.position = position;
            transform.position = position;
            _velocity = velocity;
            _knockback = Vector3.zero;
            _keptImpactAt = _nextIslandImpactAt;
            _keptDamageAt = _nextIslandDamageAt;
            _nextIslandImpactAt = _nextIslandDamageAt = double.NegativeInfinity;
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude > 1e-4f)
            {
                var rotation = Quaternion.LookRotation(modelForwardIsNegativeZ ? -flat : flat, Vector3.up);
                if (_body != null) _body.rotation = rotation;
                transform.rotation = rotation;
            }
        }

        // V5 M9c S0 (determinism): double, so the voyage's frame clock (Time.timeAsDouble) meets a cooldown on the same
        // frame in every play (a float Time.time far into a session rounds the boundary either way).
        double _nextIslandImpactAt = double.NegativeInfinity, _nextIslandDamageAt = double.NegativeInfinity;
        double _keptImpactAt = double.NegativeInfinity, _keptDamageAt = double.NegativeInfinity;

        /// <summary>Tests: undo the cooldown reset of the last <see cref="PlaceForTest"/> (a stick held into a shore).</summary>
        public void KeepIslandCooldownsForTest()
        {
            _nextIslandImpactAt = System.Math.Max(_nextIslandImpactAt, _keptImpactAt);
            _nextIslandDamageAt = System.Math.Max(_nextIslandDamageAt, _keptDamageAt);
        }
        Vector3 _knockback;
        float _knockbackDecay;
        /// <summary>Impacts that bounced the ship this run (tests and the playable check).</summary>
        public int IslandImpactCount { get; private set; }
        /// <summary>Impacts that cost hull this run.</summary>
        public int IslandDamageCount { get; private set; }
        /// <summary>A bounce: the impact speed into the shore.</summary>
        public event System.Action<float> IslandImpacted;
        /// <summary>The hull an impact costs, before the build's incoming-damage multiplier (the run applies it); with no
        /// listener the ship's own damage receiver takes it.</summary>
        public event System.Action<Vector3, float> IslandImpactDamage;

        void TryIslandImpact(Vector3 normal, float inwardSpeed, Vector3 at)
        {
            var s = IslandImpact;
            if (!s.Enabled || normal == Vector3.zero || inwardSpeed < Mathf.Max(0.01f, s.MinSpeed)) return;
            if (IslandClock < _nextIslandImpactAt) return;
            _nextIslandImpactAt = IslandCooldownEnd(s.Cooldown);
            // The resolver already took the inward speed out; push back off the shore (a fading push, see FixedUpdate).
            var push = inwardSpeed * s.Restitution + s.PushSpeed;
            _knockback = normal * push;
            _knockbackDecay = push / Mathf.Max(0.05f, s.BounceSeconds);
            IslandImpactCount++;
            IslandImpacted?.Invoke(inwardSpeed);
            if (s.Damage <= 0f || IslandClock < _nextIslandDamageAt) return;
            _nextIslandDamageAt = IslandCooldownEnd(s.DamageCooldown);
            IslandDamageCount++;
            var contact = at - normal * GetHullWorldRadius();
            if (IslandImpactDamage != null) IslandImpactDamage(contact, s.Damage);
            else if (_damage != null) _damage.TakeDamage(s.Damage, contact, false);
        }

        public void SyncRunSpeedMultiplier()
        {
            var statSpeed = RunManager.Instance != null
                ? RunManager.Instance.Stats.MoveSpeed
                : GameConstants.DefaultMoveSpeed;
            _moveSpeedMultiplier = statSpeed / ReferenceMaxSpeed * RunSpeedMultiplier;
        }

        public void ResetMotion()
        {
            _velocity = Vector3.zero;
            _knockback = Vector3.zero;
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

        /// <summary>Playtest (3 Oct, owner): turns the ship to sail along <paramref name="forward"/> (the 1v1 spawn heading).</summary>
        public void SetHeading(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) return;
            var rotation = Quaternion.LookRotation(modelForwardIsNegativeZ ? -forward.normalized : forward.normalized, Vector3.up);
            transform.rotation = rotation;
            if (_body != null) _body.rotation = rotation;
            ResetMotion();
        }

        /// <summary>The model faces -Z (the opponent's copy of the model turns the same way).</summary>
        public bool ModelForwardIsNegativeZ => modelForwardIsNegativeZ;

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
                return ClampPlayPosition(position, hullRadius);

            var height = IslandObstacles.GetCollisionHeight(hullRadius);
            for (var i = 0; i < 12; i++)
            {
                if (!IslandObstacles.IsPositionBlocked(position, hullRadius))
                    return ClampPlayPosition(position, hullRadius);

                position = IslandObstacles.Depenetrate(position, hullRadius, height);
            }

            for (var ring = 1; ring <= 16; ring++)
            {
                for (var i = 0; i < 12; i++)
                {
                    var angle = (i / 12f) * Mathf.PI * 2f;
                    var candidate = position + new Vector3(Mathf.Cos(angle) * ring * 2.5f, 0f, Mathf.Sin(angle) * ring * 2.5f);
                    if (!IslandObstacles.IsPositionBlocked(candidate, hullRadius))
                        return ClampPlayPosition(candidate, hullRadius);
                }
            }

            return ClampPlayPosition(position, hullRadius);
        }

        float GetPlayBoundsMargin(float hullRadius) => hullRadius + 0.25f;

        Vector3 ClampPlayPosition(Vector3 position, float hullRadius) => position;

        SessionClock _clock;
        /// <summary>
        /// V5 M9c S1: the run's session clock (the run injects it, and it reaches the ship's damage receiver and boost). A ship
        /// outside a run (a test's, a net client's prediction copy) keeps its own clock on Unity's frame.
        /// </summary>
        public SessionClock Clock
        {
            get => _clock ??= new SessionClock();
            set
            {
                _clock = value;
                if (_damage == null) _damage = GetComponent<DamageReceiver>();
                if (_damage != null) _damage.Clock = value;
            }
        }

        void FixedUpdate()
        {
            if (NetStepped) return; // V5 M9b part 2b: only the net tick steps it (TickMovement)
            if (Clock.IsDriven) return; // V5 M9c S1: a Driven session's owner steps its ships (TickMovement), not the physics step
            StepMovement(Clock.FixedDeltaTime);
        }

        /// <summary>One movement step of <paramref name="dt"/> seconds (the physics step in a voyage; the net tick when stepped).</summary>
        void StepMovement(float dt)
        {
            if (!_movementEnabled) return;
            if (_damage != null && !_damage.IsAlive) return;
            if (RunManager.Instance != null && RunManager.Instance.IsRunEnded) return;
            if (RunManager.Instance != null && RunManager.Instance.IsPaused && !(_netStepping && RunManager.Instance.PauseReason == PauseReason.LevelUp)) return;
            // (V5 M9b review: a net step goes through the run's level-up pause: the net layer holds the ship itself, on the
            // same client ticks on both sides, from the tick the client had reached when the cards arrived.)

            if (_slowTimer > 0f)
            {
                _slowTimer -= dt;
                if (_slowTimer <= 0f)
                    _slowMultiplier = 1f;
            }

            if (_moveEffectTimer > 0f)
            {
                _moveEffectTimer -= dt;
                if (_moveEffectTimer <= 0f)
                    _moveEffectMultiplier = 1f;
            }

            var moveMult = _slowMultiplier * _moveEffectMultiplier;
            _stepTime += dt;
            var input = ReadShipInput();
            LastSteerThrottle = new Vector2(input.steer, input.throttle);
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
            // The touch stick steers to a heading: it turns at once, both ways (no reverse pause or ramp-up).
            var headingSteer = _compositeInput != null && _compositeInput.HeadingActive;
            if (headingSteer) { _steerCommit = 1f; _reverseTurnTimer = 0f; }
            var steerReversed = !headingSteer && hasSteerInput &&
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
                RotateBody(_body.rotation * deltaRotation);
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
                var thrust = (input.throttle > 0f ? acceleration : reverseAcceleration) * RunSpeedMultiplier;
                _velocity += forward * (input.throttle * thrust * moveMult * dt);

                forwardSpeed = Vector3.Dot(_velocity, forward);
                if (Mathf.Sign(input.throttle) != Mathf.Sign(forwardSpeed) && Mathf.Abs(forwardSpeed) > 0.1f)
                    _velocity *= Mathf.Max(0f, 1f - brakeDrag * dt);
            }

            _velocity *= Mathf.Max(0f, 1f - waterDrag * dt);
            _velocity = ClampSpeed(_velocity, forward, effectiveMaxSpeed, effectiveReverseMaxSpeed);
            _velocity = ApplyLateralGrip(_velocity, forward, dt);

            // V5 island impact: the bounce is its own push that fades out, outside the steering's speed limits and grip
            // (a push away from a shore the ship faces reads as reverse, which the limits would cut).
            var movementDelta = (_velocity + _knockback) * dt;
            if (_knockback != Vector3.zero)
                _knockback = Vector3.MoveTowards(_knockback, Vector3.zero, _knockbackDecay * dt);
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
                        ref _velocity,
                        out var shoreHit);
                    if (shoreHit.Normal != Vector3.zero)
                    {
                        var into = Vector3.Dot(_knockback, shoreHit.Normal);
                        if (into < 0f) _knockback -= shoreHit.Normal * into;
                    }
                    TryIslandImpact(shoreHit.Normal, shoreHit.InwardSpeed, nextPosition);
                }
            }
            else
            {
                nextPosition = _body.position + movementDelta;
            }

            nextPosition.y = _body.position.y;

            MoveBody(nextPosition);
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }
        }

        void LateUpdate()
        {
            if (visualModel == null) return;

            _currentRoll = Mathf.Lerp(_currentRoll, _targetRoll, 1f - Mathf.Exp(-maxRollDegreesPerSecond * Clock.DeltaTime));
            var rollAxis = modelForwardIsNegativeZ ? Vector3.back : Vector3.forward;
            visualModel.localRotation = _visualBaseLocalRotation * Quaternion.AngleAxis(_currentRoll, rollAxis);
        }

        /// <summary>
        /// V5 M9b (I9b.1): the net layer steps the ship itself (the server at its fixed 30 Hz, the client's prediction when it
        /// replays inputs): every step writes the body and the transform at once instead of through the physics step's
        /// MovePosition/MoveRotation, so several steps in one frame each start from the last. Off by default (the voyage).
        /// </summary>
        public bool DirectStep
        {
            get => _directStep;
            set
            {
                if (_directStep == value) return;
                _directStep = value;
                _nextIslandImpactAt = _nextIslandDamageAt = double.NegativeInfinity; // the cooldowns change clocks
            }
        }
        bool _directStep;

        float _stepTime;
        /// <summary>The island cooldowns' clock: the session clock in the voyage (V5 M9c S1; was Unity's frame clock, which it
        /// still is in a voyage); with <see cref="DirectStep"/> the ship's own step clock (part of <see cref="MotionState"/>),
        /// so a replayed step meets the same cooldowns as the server's.</summary>
        double IslandClock => DirectStep ? _stepTime : Clock.Time;

        /// <summary>When a cooldown started now ends: on the step clock in float, as the net state carries it (the server and the
        /// prediction must agree to the bit); on the voyage's frame clock in double (V5 M9c S0).</summary>
        double IslandCooldownEnd(float seconds) =>
            DirectStep ? (double)(_stepTime + Mathf.Max(0f, seconds)) : Clock.Time + Mathf.Max(0f, seconds);

        void MoveBody(Vector3 position)
        {
            if (!DirectStep) { _body.MovePosition(position); return; }
            _body.position = position;
            transform.position = position;
        }

        void RotateBody(Quaternion rotation)
        {
            if (!DirectStep) { _body.MoveRotation(rotation); return; }
            _body.rotation = rotation;
            transform.rotation = rotation;
        }

        /// <summary>V5 M9b (I9b.1): everything one movement step reads and writes, so the client's prediction can rewind to
        /// the server's state and replay its inputs on this same code (no copy of the handling).</summary>
        [System.Serializable]
        public struct MotionState
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity, knockback;
            public float knockbackDecay, yawRate, steerCommit, lastSteer, reverseTurnTimer;
            public float slowMultiplier, slowTimer, moveEffectMultiplier, moveEffectTimer;
            public float moveSpeedMultiplier, turnRateMultiplier, runSpeedMultiplier, targetRoll;
            public float stepTime, nextIslandImpactAt, nextIslandDamageAt;
            public bool movementEnabled;
        }

        public MotionState CaptureMotion() => new MotionState
        {
            position = _body != null ? _body.position : transform.position,
            rotation = _body != null ? _body.rotation : transform.rotation,
            velocity = _velocity,
            knockback = _knockback,
            knockbackDecay = _knockbackDecay,
            yawRate = _yawRate,
            steerCommit = _steerCommit,
            lastSteer = _lastSteerInput,
            reverseTurnTimer = _reverseTurnTimer,
            slowMultiplier = _slowMultiplier,
            slowTimer = _slowTimer,
            moveEffectMultiplier = _moveEffectMultiplier,
            moveEffectTimer = _moveEffectTimer,
            moveSpeedMultiplier = _moveSpeedMultiplier,
            turnRateMultiplier = _turnRateMultiplier,
            runSpeedMultiplier = RunSpeedMultiplier,
            targetRoll = _targetRoll,
            stepTime = _stepTime,
            nextIslandImpactAt = (float)_nextIslandImpactAt,
            nextIslandDamageAt = (float)_nextIslandDamageAt,
            movementEnabled = _movementEnabled,
        };

        public void RestoreMotion(in MotionState s)
        {
            transform.SetPositionAndRotation(s.position, s.rotation);
            if (_body != null)
            {
                _body.position = s.position;
                _body.rotation = s.rotation;
            }
            _velocity = s.velocity;
            _knockback = s.knockback;
            _knockbackDecay = s.knockbackDecay;
            _yawRate = s.yawRate;
            _steerCommit = s.steerCommit;
            _lastSteerInput = s.lastSteer;
            _reverseTurnTimer = s.reverseTurnTimer;
            _slowMultiplier = s.slowMultiplier;
            _slowTimer = s.slowTimer;
            _moveEffectMultiplier = s.moveEffectMultiplier;
            _moveEffectTimer = s.moveEffectTimer;
            _moveSpeedMultiplier = s.moveSpeedMultiplier;
            _turnRateMultiplier = s.turnRateMultiplier;
            RunSpeedMultiplier = s.runSpeedMultiplier;
            _targetRoll = s.targetRoll;
            _stepTime = s.stepTime;
            _nextIslandImpactAt = s.nextIslandImpactAt;
            _nextIslandDamageAt = s.nextIslandDamageAt;
            _movementEnabled = s.movementEnabled;
        }

        /// <summary>One step of <paramref name="dt"/> seconds by the net layer (V5 M9c S1: the step's dt is passed in; Unity's
        /// fixed step is no longer written for it).</summary>
        public void TickMovement(float dt)
        {
            try
            {
                _netStepping = true;
                StepMovement(dt);
            }
            finally
            {
                _netStepping = false;
            }
        }

        /// <summary>
        /// V5 M9b part 2b: the net layer steps this ship (<see cref="TickMovement"/> once per net tick); the physics step
        /// does not move it any more. The component stays enabled, so its visual roll (LateUpdate) keeps running on the
        /// client's ship. Off by default (the voyage).
        /// </summary>
        public bool NetStepped { get; set; }
        bool _netStepping;

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

        /// <summary>
        /// V5 M9c S0 (determinism): a transform's scale in the world from the local scales up its hierarchy, not
        /// <see cref="Transform.lossyScale"/>, which Unity derives from the world matrix and so changes in its last bits with
        /// the drawn (Rigidbody-interpolated) rotation. The hull's footprint read lossyScale, so the enemies' contact radius
        /// with the ship wobbled from frame to frame and between two plays of one seed (the voyage fingerprint caught it).
        /// Equal to lossyScale wherever the ancestors' scales are uniform, as the ship's are.
        /// </summary>
        static Vector3 RestScale(Transform t)
        {
            var s = Vector3.one;
            for (; t != null; t = t.parent) s = Vector3.Scale(s, t.localScale);
            return s;
        }

        float GetHullWorldRadius()
        {
            var rootScale = RestScale(transform);
            var scale = Mathf.Max(Mathf.Abs(rootScale.x), Mathf.Abs(rootScale.z));
            var baseline = hullRadius * scale;
            var filter = FindPrimaryHullMeshFilter();
            if (filter == null || filter.sharedMesh == null)
                return baseline;

            var ext = filter.sharedMesh.bounds.extents;
            var meshScale = RestScale(filter.transform);
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
                var height = ext.y * 2f * Mathf.Abs(RestScale(filter.transform).y);
                return Mathf.Clamp(height, 1.5f, hullHeight * 1.35f);
            }

            return hullHeight * Mathf.Abs(RestScale(transform).y);
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
