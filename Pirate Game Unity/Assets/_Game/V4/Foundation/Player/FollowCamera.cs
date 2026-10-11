using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace RoguePirates.V4.Foundation.Player
{
    /// <summary>
    /// Camera follow ported from the original PirateGame TopDownCameraFollow.
    /// Smooths a virtual anchor toward the rigidbody, then applies offset — avoids
    /// lerping the camera transform itself, which fights physics interpolation.
    /// </summary>
    public sealed class FollowCamera : MonoBehaviour
    {
        public enum RotationMode
        {
            LookAtTarget,
            Manual
        }

        [Header("Target")]
        public Transform target;

        [Header("Top-down — press C to cycle here")]
        public Vector3 offset = new(0f, 18f, 12f);
        public Vector3 lookAtOffset;
        public bool followShipRotation;
        public RotationMode rotationMode = RotationMode.LookAtTarget;
        public Vector3 rotationEuler = new(56f, 0f, 0f);
        public Vector3 rotationOffset;

        public Vector3 WorldOffset => ActiveOffset;

        [Header("Top-down smoothing")]
        [Tooltip("0 = locked to ship. Higher = snappier follow (8 ≈ 0.125s lag).")]
        public float positionSmoothing;
        public float rotationSmoothing = 6f;
        public bool snapOnStart = true;

        [Header("Third person — edit THESE while in third person (Play mode)")]
        [Tooltip("When enabled, run starts in high third-person. When off, starts top-down. Press C to cycle views.")]
        public bool defaultThirdPerson = false;

        [Tooltip("Camera position in ship-local space when Follow Ship Rotation is on:\n" +
                 "X = right (+) / left (-)\n" +
                 "Y = height above anchor\n" +
                 "Z = distance behind ship (sign ignored; 14 = 14m aft)")]
        public Vector3 thirdPersonOffset = new(0f, 6f, 14f);

        [Tooltip("Look-at point offset from ship anchor (ship-local when Follow Ship Rotation is on):\n" +
                 "X = look right of ship\n" +
                 "Y = look height\n" +
                 "Z = look ahead of ship (+Z = further in front)")]
        public Vector3 thirdPersonLookAtOffset = new(0f, 1.5f, 6f);

        [Tooltip("When on, offset/look-at use ship movement forward (recommended for third person).")]
        public bool thirdPersonFollowShipRotation = true;

        [Tooltip("LookAtTarget: extra euler applied after aiming at look point. Manual: sets camera rotation directly.")]
        public RotationMode thirdPersonRotationMode = RotationMode.LookAtTarget;

        [Tooltip("Used when Third Person Rotation Mode is Manual.")]
        public Vector3 thirdPersonRotationEuler = new(12f, 0f, 0f);

        [Tooltip("Extra euler twist after look-at aim (pitch/yaw roll tweak).")]
        public Vector3 thirdPersonRotationOffset;

        public float thirdPersonPositionSmoothing;
        public float thirdPersonRotationSmoothing = 6f;

        [Header("Third person (high) — press C from top-down to reach this view")]
        [Tooltip("Higher / farther rig. Used at run start only when Default Third Person is enabled.")]
        public Vector3 thirdPersonHighOffset = new(0f, 8f, -15f);

        [Tooltip("Leave at (0,0,0) to reuse Third Person Look At Offset.")]
        public Vector3 thirdPersonHighLookAtOffset;

        enum CameraViewMode
        {
            TopDown,
            ThirdPerson,
            ThirdPersonHigh
        }

        CameraViewMode _viewMode = CameraViewMode.TopDown;
        Vector3 _savedOffset;
        Vector3 _savedLookAtOffset;
        bool _savedFollowShipRotation;
        RotationMode _savedRotationMode;
        Vector3 _savedRotationEuler;
        Vector3 _savedRotationOffset;
        float _savedPositionSmoothing;
        float _savedRotationSmoothing;

        bool _snapped;
        bool _anchorInitialized;
        PlayerShipController _ship;
        Rigidbody _targetBody;
        Vector3 _smoothedAnchor;
        Vector3 _anchorVelocity;

        Vector3 ActiveOffset => _viewMode switch
        {
            CameraViewMode.ThirdPersonHigh => thirdPersonHighOffset,
            CameraViewMode.ThirdPerson => thirdPersonOffset,
            _ => offset
        };

        Vector3 ActiveLookAtOffset => _viewMode switch
        {
            CameraViewMode.ThirdPersonHigh when thirdPersonHighLookAtOffset.sqrMagnitude > 0.0001f =>
                thirdPersonHighLookAtOffset,
            CameraViewMode.ThirdPersonHigh => thirdPersonLookAtOffset,
            CameraViewMode.ThirdPerson => thirdPersonLookAtOffset,
            _ => lookAtOffset
        };

        bool ActiveFollowShipRotation => _viewMode != CameraViewMode.TopDown
            ? thirdPersonFollowShipRotation
            : followShipRotation;

        RotationMode ActiveRotationMode => _viewMode != CameraViewMode.TopDown
            ? thirdPersonRotationMode
            : rotationMode;

        Vector3 ActiveRotationEuler => _viewMode != CameraViewMode.TopDown
            ? thirdPersonRotationEuler
            : rotationEuler;

        Vector3 ActiveRotationOffset => _viewMode != CameraViewMode.TopDown
            ? thirdPersonRotationOffset
            : rotationOffset;

        float ActivePositionSmoothing => _viewMode != CameraViewMode.TopDown
            ? thirdPersonPositionSmoothing
            : positionSmoothing;

        float ActiveRotationSmoothing => _viewMode != CameraViewMode.TopDown
            ? thirdPersonRotationSmoothing
            : rotationSmoothing;

        bool IsThirdPersonView => _viewMode != CameraViewMode.TopDown;

        void Start()
        {
            if (target == null)
                TryFindPlayer();

            CacheTargetBody();
            CacheDefaultCameraSettings();

            if (defaultThirdPerson)
                SetViewMode(CameraViewMode.ThirdPersonHigh);
            else
                SetViewMode(CameraViewMode.TopDown);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame)
                CycleViewMode();
        }

        void LateUpdate()
        {
            LateUpdateView();
            // V5 M9b close 2: once the run steps the simulation view on its ticks (StepSimulationViewOf), only the run moves
            // it; before that (a scene without a run), the frame does.
            if (!_simSteppedByRun) UpdateSimulationView(Clock.DeltaTime);
        }

        void LateUpdateView()
        {
            if (target == null)
            {
                TryFindPlayer();
                if (target == null) return;
            }

            CacheTargetBody();

            if (_ship == null)
                _ship = target.GetComponent<PlayerShipController>();

            if (snapOnStart && !_snapped)
            {
                SnapToTarget();
                return;
            }

            var anchorPosition = GetAnchorPosition();
            if (IsThirdPersonView)
            {
                _smoothedAnchor = anchorPosition;
                _anchorVelocity = Vector3.zero;
                _anchorInitialized = true;

                var thirdPersonPosition = GetDesiredPosition(anchorPosition);
                if (ActivePositionSmoothing > 0f)
                {
                    transform.position = Vector3.SmoothDamp(
                        transform.position,
                        thirdPersonPosition,
                        ref _anchorVelocity,
                        1f / Mathf.Max(ActivePositionSmoothing, 0.01f),
                        Mathf.Infinity,
                        Time.deltaTime);
                }
                else
                    transform.position = thirdPersonPosition;

                ApplyRotation(transform.position, smooth: ActiveRotationSmoothing > 0f);
                return;
            }

            UpdateSmoothedAnchor(anchorPosition);
            _smoothedAnchor = ClampAnchorToPlayArea(_smoothedAnchor);

            var desiredPosition = GetDesiredPosition(_smoothedAnchor);
            transform.position = desiredPosition;
            ApplyRotation(desiredPosition, smooth: ActiveRotationSmoothing > 0f);
        }

        public void SnapToTarget()
        {
            if (target == null) return;

            CacheTargetBody();
            if (_ship == null) _ship = target.GetComponent<PlayerShipController>();
            var anchorPosition = GetAnchorPosition();
            _smoothedAnchor = IsThirdPersonView ? anchorPosition : ClampAnchorToPlayArea(anchorPosition);
            _anchorVelocity = Vector3.zero;
            _anchorInitialized = true;

            var desiredPosition = GetDesiredPosition(_smoothedAnchor);
            transform.position = desiredPosition;
            ApplyRotation(desiredPosition, smooth: false);
            _snapped = true;
            _simAnchorInitialized = false;
            UpdateSimulationView(0f); // V5 M9b close 2: the view exists (snapped) before the run's first tick asks it
        }

        // ---- V5 M9b close (determinism): the simulation view ----
        // The drawn camera follows the ship's transform, which Rigidbody interpolation moves between physics steps by the
        // frame's render phase (Time.time against Time.fixedTime). Spawn placement and the off-screen ambush asked that
        // camera what is on screen, so two plays of one seed with the same inputs placed enemies differently (the M9a
        // determinism test). The simulation view is the same camera (offset, aim, smoothing, play-area clamp and
        // projection) following the ship's simulated position instead: a hidden, never-rendering camera that gameplay
        // asks (SimulationViewOf); the drawn camera is unchanged.

        Camera _simulationView;
        Vector3 _simAnchor, _simAnchorVelocity;
        bool _simAnchorInitialized;

        /// <summary>The camera gameplay asks what is on screen: <paramref name="camera"/>'s simulation view when it is the
        /// run's follow camera, else <paramref name="camera"/> itself (null stays null).</summary>
        public static Camera SimulationViewOf(Camera camera)
        {
            if (camera == null) return null;
            var follow = camera.GetComponent<FollowCamera>();
            return follow != null && follow._simulationView != null ? follow._simulationView : camera;
        }

        /// <summary>The simulation view (null until the camera has a target).</summary>
        public Camera SimulationView => _simulationView;

        bool _simSteppedByRun;

        SessionClock _clock;
        /// <summary>V5 M9c S1: the run's session clock for the simulation view (the run sets it, <see cref="UseClockOf"/>);
        /// a camera without a run is on Unity's frame. The drawn camera's own smoothing stays on Unity's frame (presentation).</summary>
        public SessionClock Clock { get => _clock ??= new SessionClock(); set => _clock = value; }

        /// <summary>V5 M9c S1: <paramref name="camera"/>'s simulation view runs on <paramref name="clock"/> (no-op for a camera
        /// without a follow camera).</summary>
        public static void UseClockOf(Camera camera, SessionClock clock)
        {
            if (camera == null) return;
            var follow = camera.GetComponent<FollowCamera>();
            if (follow != null) follow.Clock = clock;
        }

        /// <summary>V5 M9b close 2: the run steps <paramref name="camera"/>'s simulation view once per simulation tick, by the
        /// tick's dt, before the tick's modules ask it what is on screen, so the view (and its follow smoothing) is the same
        /// at any frame rate (was stepped each drawn frame by the frame's delta time). No-op for a camera without one.</summary>
        public static void StepSimulationViewOf(Camera camera, float dt)
        {
            if (camera == null) return;
            var follow = camera.GetComponent<FollowCamera>();
            if (follow == null) return;
            follow._simSteppedByRun = true;
            follow.UpdateSimulationView(dt);
        }

        void UpdateSimulationView(float dt)
        {
            if (target == null || !_snapped) return;
            if (_viewCamera == null) _viewCamera = GetComponent<Camera>();
            if (_viewCamera == null) return;
            var ship = _ship != null ? _ship.SimPosition : target.position;
            var smoothing = ActivePositionSmoothing;
            if (!_simAnchorInitialized || smoothing <= 0f || IsThirdPersonView)
            {
                _simAnchor = ship;
                _simAnchorVelocity = Vector3.zero;
                _simAnchorInitialized = true;
            }
            else if (dt > 0f)
            {
                _simAnchor = Vector3.SmoothDamp(_simAnchor, ship, ref _simAnchorVelocity, 1f / Mathf.Max(smoothing, 0.01f), Mathf.Infinity, dt);
            }
            var anchor = IsThirdPersonView ? _simAnchor : ClampAnchorToPlayArea(_simAnchor, ship, _simAnchor);
            if (!IsThirdPersonView) _simAnchor = anchor;
            var position = GetDesiredPosition(anchor);
            var rotation = GetDesiredRotation(position, anchor);
            if (_simulationView == null)
            {
                // A child of the drawn camera: it goes with it (no separate object to clean up), and it is not saved.
                var go = new GameObject("FollowCameraSimulationView") { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(transform, false);
                _simulationView = go.AddComponent<Camera>();
                _simulationView.enabled = false; // never renders: gameplay only reads its view
            }
            _simulationView.transform.SetPositionAndRotation(position, rotation);
            _simulationView.orthographic = _viewCamera.orthographic;
            _simulationView.orthographicSize = _viewCamera.orthographicSize;
            _simulationView.fieldOfView = _viewCamera.fieldOfView;
            _simulationView.nearClipPlane = _viewCamera.nearClipPlane;
            _simulationView.farClipPlane = _viewCamera.farClipPlane;
            _simulationView.aspect = _viewCamera.aspect;
        }

        void UpdateSmoothedAnchor(Vector3 anchorPosition)
        {
            if (ActivePositionSmoothing <= 0f)
            {
                _smoothedAnchor = anchorPosition;
                _anchorVelocity = Vector3.zero;
                _anchorInitialized = true;
                return;
            }

            if (!_anchorInitialized)
            {
                _smoothedAnchor = anchorPosition;
                _anchorVelocity = Vector3.zero;
                _anchorInitialized = true;
                return;
            }

            _smoothedAnchor = Vector3.SmoothDamp(
                _smoothedAnchor,
                anchorPosition,
                ref _anchorVelocity,
                1f / Mathf.Max(ActivePositionSmoothing, 0.01f),
                Mathf.Infinity,
                Time.deltaTime);
        }

        Vector3 GetAnchorPosition() => target.position;

        [Tooltip("Demo: the view stops at the play area's walls (and at the storm's edge once it closes in), so the sea beyond is never shown.")]
        [SerializeField] bool clampToPlayArea = true;
        [Tooltip("While clamped to the storm edge, the ship stays at least this far inside the view (m).")]
        [SerializeField] float shipViewMargin = 4f;
        Camera _viewCamera;

        /// <summary>
        /// Moves the follow point so the camera's footprint on the water (its four view corners on the sea plane) stays
        /// inside the play area; once the storm is shrinking the safe area, inside the storm edge too, as long as the ship
        /// stays on screen. A view wider than the area centres on it.
        /// </summary>
        Vector3 ClampAnchorToPlayArea(Vector3 anchor) =>
            ClampAnchorToPlayArea(anchor, target.position, _anchorInitialized ? _smoothedAnchor : GetAnchorPosition());

        /// <summary>The clamp for a follow point, with the ship at <paramref name="shipPos"/> and the camera aimed from
        /// <paramref name="lookAnchor"/> (the drawn camera: the interpolated ship and its own anchor; the simulation view: the
        /// simulated ship and its anchor).</summary>
        Vector3 ClampAnchorToPlayArea(Vector3 anchor, Vector3 shipPos, Vector3 lookAnchor)
        {
            if (!clampToPlayArea || PlayBounds.HalfExtentX <= 0f || PlayBounds.HalfExtentZ <= 0f) return anchor;
            if (_viewCamera == null) _viewCamera = GetComponent<Camera>();
            if (_viewCamera == null) return anchor;

            var pos = GetDesiredPosition(anchor);
            var rot = GetDesiredRotation(pos, lookAnchor);
            var tan = Mathf.Tan(_viewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            var aspect = _viewCamera.aspect;
            // The footprint's middle row gives the sideways reach at the ship's depth (the tilted view's far corners spread
            // much wider and would pull the camera far off the ship); the bottom and top edge centres give the near and
            // far reach. The tall edge walls hide the little that the far corners see beyond.
            float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
            for (var i = 0; i < 4; i++)
            {
                var vx = i == 0 ? -tan * aspect : i == 1 ? tan * aspect : 0f;
                var vy = i == 2 ? -tan : i == 3 ? tan : 0f;
                var dir = rot * new Vector3(vx, vy, 1f);
                if (dir.y > -0.01f) return anchor; // the view reaches the horizon: no footprint to clamp
                var hit = pos + dir * (-pos.y / dir.y);
                var off = hit - anchor;
                minX = Mathf.Min(minX, off.x); maxX = Mathf.Max(maxX, off.x);
                minZ = Mathf.Min(minZ, off.z); maxZ = Mathf.Max(maxZ, off.z);
            }

            var c = PlayBounds.Center;
            var result = anchor;
            if (PlayBounds.UsesProfileStormSafe && PlayBounds.StormSafeScale < 0.999f)
            {
                var sx = (PlayBounds.HalfExtentX - PlayBounds.StormEdgeMargin) * PlayBounds.StormSafeScale;
                var sz = (PlayBounds.HalfExtentZ - PlayBounds.StormEdgeMargin) * PlayBounds.StormSafeScale;
                result.x = ClampAxis(result.x, c.x, sx, minX, maxX);
                result.z = ClampAxis(result.z, c.z, sz, minZ, maxZ);
            }
            result.x = ClampAxis(result.x, c.x, PlayBounds.HalfExtentX, minX, maxX);
            result.z = ClampAxis(result.z, c.z, PlayBounds.HalfExtentZ, minZ, maxZ);
            // The ship always stays on screen.
            result.x = KeepInView(result.x, shipPos.x, minX, maxX);
            result.z = KeepInView(result.z, shipPos.z, minZ, maxZ);
            return result;
        }

        static float ClampAxis(float value, float center, float half, float minOffset, float maxOffset)
        {
            var lo = center - half - minOffset;
            var hi = center + half - maxOffset;
            return lo > hi ? center - (minOffset + maxOffset) * 0.5f : Mathf.Clamp(value, lo, hi);
        }

        float KeepInView(float value, float ship, float minOffset, float maxOffset)
        {
            var lo = ship - maxOffset + shipViewMargin;
            var hi = ship - minOffset - shipViewMargin;
            return lo > hi ? value : Mathf.Clamp(value, lo, hi);
        }

        Vector3 GetDesiredPosition(Vector3 anchorPosition)
        {
            if (target == null)
                return transform.position;

            var activeOffset = ActiveOffset;
            if (ActiveFollowShipRotation && TryGetShipLocalCameraOffset(activeOffset, out var movementOffset))
                return anchorPosition + movementOffset;

            return ActiveFollowShipRotation
                ? anchorPosition + target.rotation * activeOffset
                : anchorPosition + activeOffset;
        }

        /// <summary>Camera rig: X lateral, Y height, |Z| = distance aft of ship.</summary>
        bool TryGetShipLocalCameraOffset(Vector3 shipLocalOffset, out Vector3 worldOffset)
        {
            worldOffset = default;
            if (_ship == null)
                return false;

            GetShipBasis(out var forward, out var right);
            var distanceAft = Mathf.Abs(shipLocalOffset.z);
            worldOffset = right * shipLocalOffset.x + Vector3.up * shipLocalOffset.y - forward * distanceAft;
            return true;
        }

        /// <summary>Look target: X lateral, Y height, +Z = ahead of ship.</summary>
        bool TryGetShipLocalLookOffset(Vector3 shipLocalOffset, out Vector3 worldOffset)
        {
            worldOffset = default;
            if (_ship == null)
                return false;

            GetShipBasis(out var forward, out var right);
            worldOffset = right * shipLocalOffset.x + Vector3.up * shipLocalOffset.y + forward * shipLocalOffset.z;
            return true;
        }

        void GetShipBasis(out Vector3 forward, out Vector3 right)
        {
            forward = _ship != null ? _ship.MovementForward : target.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = Vector3.forward;
            forward.Normalize();

            right = Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude < 0.0001f)
                right = target.right;
            right.y = 0f;
            right.Normalize();
        }

        void ApplyRotation(Vector3 cameraPosition, bool smooth)
        {
            var desiredRotation = GetDesiredRotation(cameraPosition);
            var rotSmoothing = ActiveRotationSmoothing;
            transform.rotation = smooth && rotSmoothing > 0f
                ? Quaternion.Slerp(transform.rotation, desiredRotation, SmoothFactor(rotSmoothing))
                : desiredRotation;
        }

        Quaternion GetDesiredRotation(Vector3 cameraPosition) =>
            GetDesiredRotation(cameraPosition, _anchorInitialized ? _smoothedAnchor : GetAnchorPosition());

        Quaternion GetDesiredRotation(Vector3 cameraPosition, Vector3 lookAnchor)
        {
            if (ActiveRotationMode == RotationMode.Manual)
                return Quaternion.Euler(ActiveRotationEuler);

            var lookPoint = lookAnchor;
            var lookOffset = ActiveLookAtOffset;

            if (ActiveFollowShipRotation && _ship != null &&
                TryGetShipLocalLookOffset(lookOffset, out var worldLookOffset))
            {
                lookPoint += worldLookOffset;
            }
            else if (ActiveFollowShipRotation)
            {
                lookPoint += target.rotation * lookOffset;
            }
            else
            {
                lookPoint += lookOffset;
            }

            var direction = lookPoint - cameraPosition;
            if (direction.sqrMagnitude < 0.0001f)
                return Quaternion.Euler(ActiveRotationEuler);

            return Quaternion.LookRotation(direction, Vector3.up) * Quaternion.Euler(ActiveRotationOffset);
        }

        public void SetTarget(Transform newTarget, bool snap = true)
        {
            target = newTarget;
            _ship = newTarget != null ? newTarget.GetComponent<PlayerShipController>() : null;
            _targetBody = null;
            CacheTargetBody();
            _snapped = false;
            _anchorInitialized = false;
            if (snap && target != null)
                SnapToTarget();
        }

        public bool IsThirdPerson => IsThirdPersonView;

        public void ToggleThirdPerson() => CycleViewMode();

        public void CycleViewMode()
        {
            var next = _viewMode switch
            {
                CameraViewMode.ThirdPersonHigh => CameraViewMode.ThirdPerson,
                CameraViewMode.ThirdPerson => CameraViewMode.TopDown,
                _ => CameraViewMode.ThirdPersonHigh
            };

            SetViewMode(next);
        }

        void SetViewMode(CameraViewMode mode)
        {
            if (mode != CameraViewMode.TopDown && _viewMode == CameraViewMode.TopDown)
                CacheDefaultCameraSettings();

            if (mode == CameraViewMode.TopDown && _viewMode != CameraViewMode.TopDown)
                RestoreTopDownSettings();

            _viewMode = mode;
            ApplyFarClip();
            SnapToTarget();
        }

        [Tooltip("V5 M3e: the camera's far clip in the top-down view (m). The angled top-down view sees about 60 m of sea, so " +
                 "nothing past this is drawn (was 1000 m); the third-person views keep the camera's own far clip.")]
        public float topDownFarClip = 250f;

        float _sceneFarClip = -1f;

        void ApplyFarClip()
        {
            if (_viewCamera == null) _viewCamera = GetComponent<Camera>();
            if (_viewCamera == null) return;
            if (_sceneFarClip < 0f) _sceneFarClip = _viewCamera.farClipPlane;
            _viewCamera.farClipPlane = _viewMode == CameraViewMode.TopDown && topDownFarClip > 0f ? Mathf.Min(_sceneFarClip, topDownFarClip) : _sceneFarClip;
        }

        /// <summary>The far clip the camera uses now (tests).</summary>
        public float FarClip => _viewCamera != null ? _viewCamera.farClipPlane : -1f;

        void RestoreTopDownSettings()
        {
            offset = _savedOffset;
            lookAtOffset = _savedLookAtOffset;
            followShipRotation = _savedFollowShipRotation;
            rotationMode = _savedRotationMode;
            rotationEuler = _savedRotationEuler;
            rotationOffset = _savedRotationOffset;
            positionSmoothing = _savedPositionSmoothing;
            rotationSmoothing = _savedRotationSmoothing;
        }

        void CacheDefaultCameraSettings()
        {
            _savedOffset = offset;
            _savedLookAtOffset = lookAtOffset;
            _savedFollowShipRotation = followShipRotation;
            _savedRotationMode = rotationMode;
            _savedRotationEuler = rotationEuler;
            _savedRotationOffset = rotationOffset;
            _savedPositionSmoothing = positionSmoothing;
            _savedRotationSmoothing = rotationSmoothing;
        }

        void TryFindPlayer()
        {
            _ship = FindFirstObjectByType<PlayerShipController>();
            if (_ship != null)
                target = _ship.transform;
        }

        void CacheTargetBody()
        {
            if (target == null)
            {
                _targetBody = null;
                return;
            }

            if (_targetBody == null || _targetBody.transform != target)
                _targetBody = target.GetComponent<Rigidbody>();
        }

        static float SmoothFactor(float smoothing) =>
            1f - Mathf.Exp(-smoothing * Time.deltaTime);
    }
}
