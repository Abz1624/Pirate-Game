using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Foundation.Player;
using RoguePirates.V4.Foundation.PlayerSystems;
using UnityEngine;

namespace RoguePirates.V4.Ports
{
    /// <summary>
    /// V5 M9b (I9b.1): the net layer's handle on a ship's movement. The match server steps the run's own ship with the
    /// inputs its client sent (one step per input, at the net tick); the client steps a prediction copy of the same ship
    /// with the same inputs, rewinds it to the server's state and replays. Both run <see cref="PlayerShipController"/>'s
    /// one handling code (no copy of it): this port only feeds the input and the step.
    /// </summary>
    public sealed class ShipMotionPort
    {
        readonly PlayerShipController _ship;
        readonly HeldInput _input = new HeldInput();
        bool _controlled;
        bool _ownsObject;

        public ShipMotionPort(PlayerShipController ship) { _ship = ship; }

        /// <summary>The run's ship (the server's side).</summary>
        public static ShipMotionPort Of(V4Game game) =>
            game != null && game.Shell != null && game.Shell.Player != null ? new ShipMotionPort(game.Shell.Player) : null;

        public bool Exists => _ship != null;
        /// <summary>The simulated position (V5 M9b part 2b: the body's, not the drawn transform's).</summary>
        public Vector3 Position => _ship != null ? _ship.SimPosition : Vector3.zero;
        public Vector3 Forward => _ship != null ? _ship.MovementForward : Vector3.forward;
        /// <summary>V5 M9b playtest fixes: the simulated rotation (the body's, not the drawn transform's).</summary>
        public Quaternion Rotation => Body != null ? Body.rotation : _ship != null ? _ship.transform.rotation : Quaternion.identity;
        public bool Controlled => _controlled;
        public float Hull => _ship != null && _ship.Damage != null ? _ship.Damage.CurrentHP : 0f;
        public float MaxHull => _ship != null && _ship.Damage != null ? _ship.Damage.MaxHP : 0f;
        public bool Alive => _ship != null && _ship.Damage != null && _ship.Damage.IsAlive;
        public Transform Root => _ship != null ? _ship.transform : null;

        /// <summary>The net layer drives the ship from here on: its own input, no physics-step movement, one step per call;
        /// V5 M9b part 2b: the boost too (its own Update off; <see cref="Step(Vector2, bool, float, bool)"/> steps it).</summary>
        public void TakeControl()
        {
            if (_ship == null || _controlled) return;
            _controlled = true;
            _ship.ConfigureDedicatedSteerInput(_input);
            _ship.DirectStep = true;
            _ship.NetStepped = true; // no FixedUpdate of its own: the net tick steps it (its visual roll keeps running)
            // V5 M9b playtest fixes: no physics interpolation of the transform (the net client draws the ship itself,
            // between its net ticks; the body is written directly at each step).
            if (Body != null) Body.interpolation = RigidbodyInterpolation.None;
            var boost = Boost;
            if (boost != null) boost.enabled = false; // no frame-clock boost: the net tick steps it
        }

        ShipBoostController Boost => _ship != null ? _ship.GetComponent<ShipBoostController>() : null;
        Rigidbody _body;
        Rigidbody Body => _body != null ? _body : _ship != null ? _body = _ship.GetComponent<Rigidbody>() : null;

        /// <summary>
        /// V5 M9b playtest fixes: the net client steps the ship at the 30 Hz net tick but draws it every frame between its
        /// last two ticks. <see cref="Draw"/> moves only the transform (what is rendered and what the camera follows);
        /// <see cref="SetSimulatedPose"/> puts the transform and the body back on the simulated pose before the next step
        /// or rewind reads it (a physics step may have copied the drawn transform into the body meanwhile).
        /// </summary>
        public void Draw(Vector3 position, Quaternion rotation)
        {
            if (_ship != null) _ship.transform.SetPositionAndRotation(position, rotation);
        }

        public void SetSimulatedPose(Vector3 position, Quaternion rotation)
        {
            if (_ship == null) return;
            _ship.transform.SetPositionAndRotation(position, rotation);
            var body = Body;
            if (body == null) return;
            body.position = position;
            body.rotation = rotation;
        }

        /// <summary>V5 M9b part 2b: the ship is boosting now.</summary>
        public bool IsBoosting { get { var b = Boost; return b != null && b.IsBoosting; } }

        /// <summary>V5 M9b part 2b: the boost's timers (rewound with the motion).</summary>
        public ShipBoostController.NetState CaptureBoost()
        {
            var b = Boost;
            return b != null ? b.CaptureNet() : default;
        }

        public void RestoreBoost(in ShipBoostController.NetState state)
        {
            var b = Boost;
            if (b != null) b.RestoreNet(state);
        }

        /// <summary>V5 M9b part 2b: the client's ship is for show and prediction: nothing local damages it (the hull shown is
        /// the server's).</summary>
        public void IgnoreLocalDamage()
        {
            if (_ship != null && _ship.Damage != null) _ship.Damage.SetPlaytestInvulnerable(true);
            if (_ship != null) _ship.IslandImpactDamage += (_, __) => { };
        }

        public PlayerShipController.MotionState Capture() => _ship.CaptureMotion();
        public void Restore(in PlayerShipController.MotionState state) => _ship.RestoreMotion(state);

        /// <summary>One movement step of <paramref name="dt"/> seconds on steer (x) and throttle (y).</summary>
        public void Step(Vector2 steerThrottle, float dt) => Step(steerThrottle, false, dt, true);

        /// <summary>
        /// V5 M9b part 2b: one net step: the boost (held <paramref name="boost"/> starts it when ready, its ease runs on
        /// this step's clock), then the movement, as a voyage frame orders them. The server and the client's prediction
        /// both step through here, so a boost replays exactly. <paramref name="silent"/>: no boost sound (a replay, a server).
        /// </summary>
        public void Step(Vector2 steerThrottle, bool boost, float dt, bool silent)
        {
            if (_ship == null) return;
            if (_ship.Damage != null && !_ship.Damage.IsAlive) return;
            var b = _controlled ? Boost : null;
            if (b != null && !RunPausedForNet) b.TickNet(dt, boost, silent);
            _input.Value = steerThrottle;
            _ship.TickMovement(dt);
        }

        /// <summary>The run stops a net step's boost when it ended or is paused, except for level-up cards: V5 M9b review,
        /// the net layer holds the ship on those itself (it moves up to the tick the client had reached, then holds).</summary>
        static bool RunPausedForNet => RunManager.Instance != null &&
            (RunManager.Instance.IsRunEnded || RunManager.Instance.IsPaused && RunManager.Instance.PauseReason != PauseReason.LevelUp);

        /// <summary>The ship's boost (the run's own boost controller; the server's side only).</summary>
        public void RequestBoost()
        {
            if (_ship == null) return;
            var boost = _ship.GetComponent<RoguePirates.V4.Foundation.PlayerSystems.ShipBoostController>();
            if (boost != null) boost.RequestBoost();
        }

        /// <summary>
        /// The client's prediction ship: a copy of <paramref name="source"/> with its handling, hull shape and model only
        /// (no boost, hull sync, audio or other run components), on the Ignore Raycast layer so no enemy query finds it,
        /// and a hull of its own that nothing damages (the hull the client shows is the server's).
        /// </summary>
        public static ShipMotionPort CreatePredictionCopy(PlayerShipController source, string name = "NetPredictedShip")
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var holder = new GameObject(name + "_Holder");
            holder.SetActive(false); // the copy's Awake waits until it is stripped
            var copy = UnityEngine.Object.Instantiate(source.gameObject, holder.transform);
            copy.name = name;
            Strip(copy);
            copy.transform.SetParent(null, true);
            UnityEngine.Object.Destroy(holder);
            copy.SetActive(true);
            SetLayer(copy.transform, 2); // after Awake, which ties the ship's own layer to the islands' collision rule
            var ship = copy.GetComponent<PlayerShipController>();
            var boost = copy.GetComponent<ShipBoostController>(); // V5 M9b part 2b: kept, so the boost is predicted
            if (boost != null) boost.Bind(ship);
            ship.IslandImpact = source.IslandImpact;
            ship.IslandImpactDamage += (_, __) => { }; // impacts cost the server's hull, never this copy's
            ship.Damage.Initialize(1f);
            var body = copy.GetComponent<Rigidbody>();
            if (body != null) body.interpolation = RigidbodyInterpolation.None;
            var port = new ShipMotionPort(ship) { _ownsObject = true };
            port.TakeControl();
            port.Restore(source.CaptureMotion());
            var sourceBoost = source.GetComponent<ShipBoostController>();
            if (sourceBoost != null) port.RestoreBoost(sourceBoost.CaptureNet());
            return port;
        }

        public void Destroy()
        {
            if (_ship != null && _ownsObject) UnityEngine.Object.Destroy(_ship.gameObject);
        }

        static readonly HashSet<Type> Kept = new HashSet<Type> { typeof(PlayerShipController), typeof(DamageReceiver), typeof(ShipBoostController) };

        static void Strip(GameObject copy)
        {
            // Several passes: a component another one requires goes after it.
            for (var pass = 0; pass < 4; pass++)
            {
                var removed = 0;
                foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null || Kept.Contains(behaviour.GetType()) && behaviour.gameObject == copy) continue;
                    if (RequiredByAnother(behaviour)) continue; // next pass, once the one needing it is gone
                    UnityEngine.Object.DestroyImmediate(behaviour);
                    removed++;
                }
                foreach (var audio in copy.GetComponentsInChildren<AudioSource>(true)) { UnityEngine.Object.DestroyImmediate(audio); removed++; }
                if (removed == 0) break;
            }
        }

        static bool RequiredByAnother(Component component)
        {
            var type = component.GetType();
            foreach (var other in component.GetComponents<Component>())
            {
                if (other == null || other == component) continue;
                foreach (RequireComponent require in other.GetType().GetCustomAttributes(typeof(RequireComponent), true))
                    if (Requires(require.m_Type0, type) || Requires(require.m_Type1, type) || Requires(require.m_Type2, type)) return true;
            }
            return false;
        }

        static bool Requires(Type required, Type type) => required != null && required.IsAssignableFrom(type);

        static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (var i = 0; i < t.childCount; i++) SetLayer(t.GetChild(i), layer);
        }

        sealed class HeldInput : IShipSteerInput
        {
            public Vector2 Value;
            public Vector2 ReadSteerThrottle() => Value;
        }
    }
}
