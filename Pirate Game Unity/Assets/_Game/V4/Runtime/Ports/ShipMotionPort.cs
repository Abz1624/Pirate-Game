using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.Combat;
using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Foundation.Player;
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
        public Vector3 Position => _ship != null ? _ship.Position : Vector3.zero;
        public Vector3 Forward => _ship != null ? _ship.MovementForward : Vector3.forward;
        public bool Controlled => _controlled;
        public float Hull => _ship != null && _ship.Damage != null ? _ship.Damage.CurrentHP : 0f;
        public float MaxHull => _ship != null && _ship.Damage != null ? _ship.Damage.MaxHP : 0f;
        public bool Alive => _ship != null && _ship.Damage != null && _ship.Damage.IsAlive;
        public Transform Root => _ship != null ? _ship.transform : null;

        /// <summary>The net layer drives the ship from here on: its own input, no physics-step movement, one step per call.</summary>
        public void TakeControl()
        {
            if (_ship == null || _controlled) return;
            _controlled = true;
            _ship.ConfigureDedicatedSteerInput(_input);
            _ship.DirectStep = true;
            _ship.enabled = false; // no FixedUpdate of its own: the net tick steps it
        }

        public PlayerShipController.MotionState Capture() => _ship.CaptureMotion();
        public void Restore(in PlayerShipController.MotionState state) => _ship.RestoreMotion(state);

        /// <summary>One movement step of <paramref name="dt"/> seconds on steer (x) and throttle (y).</summary>
        public void Step(Vector2 steerThrottle, float dt)
        {
            if (_ship == null) return;
            _input.Value = steerThrottle;
            _ship.TickMovement(dt);
        }

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
            ship.IslandImpact = source.IslandImpact;
            ship.IslandImpactDamage += (_, __) => { }; // impacts cost the server's hull, never this copy's
            ship.Damage.Initialize(1f);
            var body = copy.GetComponent<Rigidbody>();
            if (body != null) body.interpolation = RigidbodyInterpolation.None;
            var port = new ShipMotionPort(ship) { _ownsObject = true };
            port.TakeControl();
            port.Restore(source.CaptureMotion());
            return port;
        }

        public void Destroy()
        {
            if (_ship != null && _ownsObject) UnityEngine.Object.Destroy(_ship.gameObject);
        }

        static readonly HashSet<Type> Kept = new HashSet<Type> { typeof(PlayerShipController), typeof(DamageReceiver) };

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
