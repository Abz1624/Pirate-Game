using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// Local ship prediction — single integration step ported from ShipMovement.cs.
    /// Only the local ship is predicted; reconciliation happens in ShipPredictionReconciler.
    /// </summary>
    public static class ShipPredictor
    {
        public struct ShipState
        {
            public Vector3 position;
            public Vector3 velocity;
            public float headingDeg;
        }

        public struct ShipInput
        {
            public float throttle;
            public float steer;
        }

        public struct ShipParams
        {
            public float maxSpeed;
            public float reverseMaxSpeed;
            public float acceleration;
            public float reverseAcceleration;
            public float turnSpeed;
            public float waterDrag;
            public float brakeDrag;
        }

        public static readonly ShipParams DefaultParams = new()
        {
            maxSpeed = 50f,
            reverseMaxSpeed = 15f,
            acceleration = 15f,
            reverseAcceleration = 4f,
            turnSpeed = 800f,
            waterDrag = 3f,
            brakeDrag = 6f
        };

        /// <summary>One fixed timestep integration — must match Rust sim tick.</summary>
        public static ShipState Integrate(ShipState state, ShipInput input, ShipParams p, float dt)
        {
            var forward = Quaternion.Euler(0f, state.headingDeg, 0f) * Vector3.forward;
            var forwardSpeed = Vector3.Dot(state.velocity, forward);
            var turnRef = forwardSpeed >= 0f ? p.maxSpeed : p.reverseMaxSpeed;
            var turnEffectiveness = turnRef > 0f ? Mathf.Clamp01(Mathf.Abs(forwardSpeed) / turnRef) : 0f;

            if (Mathf.Abs(input.steer) > 0.01f && turnEffectiveness > 0f)
                state.headingDeg += input.steer * p.turnSpeed * turnEffectiveness * dt;

            if (Mathf.Abs(input.throttle) > 0.01f)
            {
                var thrust = input.throttle > 0f ? p.acceleration : p.reverseAcceleration;
                state.velocity += forward * (input.throttle * thrust * dt);

                forwardSpeed = Vector3.Dot(state.velocity, forward);
                if (Mathf.Sign(input.throttle) != Mathf.Sign(forwardSpeed) && Mathf.Abs(forwardSpeed) > 0.1f)
                    state.velocity *= Mathf.Max(0f, 1f - p.brakeDrag * dt);
            }

            state.velocity *= Mathf.Max(0f, 1f - p.waterDrag * dt);
            var speed = state.velocity.magnitude;
            var maxAllowed = forwardSpeed >= 0f ? p.maxSpeed : p.reverseMaxSpeed;
            if (speed > maxAllowed && speed > 0.0001f)
                state.velocity = state.velocity / speed * maxAllowed;

            state.position += state.velocity * dt;
            state.position.y = 0f;
            return state;
        }
    }

    public class ShipPredictionReconciler : MonoBehaviour
    {
        [SerializeField] float smoothTime = 0.15f;
        [SerializeField] float snapThreshold = 2f;

        ShipPredictor.ShipState predicted;
        Vector3 correctionVelocity;

        public ShipPredictor.ShipState Predicted => predicted;

        public void ResetState(Vector3 position, Vector3 velocity, float headingDeg)
        {
            predicted = new ShipPredictor.ShipState
            {
                position = position,
                velocity = velocity,
                headingDeg = headingDeg
            };
        }

        public void ApplyInput(ShipPredictor.ShipInput input, float dt)
        {
            predicted = ShipPredictor.Integrate(predicted, input, ShipPredictor.DefaultParams, dt);
        }

        public void Reconcile(Vector3 authoritativePosition, float authoritativeHeading)
        {
            var error = authoritativePosition - predicted.position;
            error.y = 0f;
            if (error.magnitude > snapThreshold)
            {
                predicted.position = authoritativePosition;
                predicted.headingDeg = authoritativeHeading;
                correctionVelocity = Vector3.zero;
                return;
            }
            correctionVelocity = error / smoothTime;
        }

        public Vector3 GetDisplayPosition()
        {
            return predicted.position + correctionVelocity * Time.deltaTime;
        }

        public float GetDisplayHeading()
        {
            return predicted.headingDeg;
        }
    }
}
