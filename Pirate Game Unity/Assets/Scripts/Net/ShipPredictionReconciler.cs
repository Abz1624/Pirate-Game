using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>Client-side prediction with input-replay reconciliation.</summary>
    public class ShipPredictionReconciler : MonoBehaviour
    {
        [SerializeField] float snapThreshold = 16f;
        [SerializeField] int tickHz = 10;
        [SerializeField, Range(0.05f, 0.5f)] float reconcileBlend = 0.15f;

        ShipPredictor.ShipState predicted;
        ShipPredictor.ShipState prevPredicted;
        uint lastAckedTick;
        float tickAccumulator;

        public ShipPredictor.ShipState Predicted => predicted;

        void Awake()
        {
            prevPredicted = predicted;
        }

        public void ResetState(short x, short z, ushort heading)
        {
            predicted = ShipPredictor.FromSnapshot(x, z, heading);
            prevPredicted = predicted;
            lastAckedTick = 0;
            tickAccumulator = 0f;
        }

        public void SyncToServerTick(uint serverTick, short x, short z, ushort heading)
        {
            predicted = ShipPredictor.FromSnapshot(x, z, heading);
            prevPredicted = predicted;
            lastAckedTick = serverTick;
            tickAccumulator = 0f;
        }

        /// <summary>Returns true once per sim tick at tickHz.</summary>
        public bool TryAdvanceSimulationTick(float deltaTime)
        {
            tickAccumulator += deltaTime;
            var interval = 1f / Mathf.Max(1, tickHz);
            if (tickAccumulator < interval)
                return false;

            tickAccumulator -= interval;
            prevPredicted = predicted;
            return true;
        }

        public void QueueInput(ShipPredictor.ShipInput input)
        {
            predicted = ShipPredictor.Integrate(predicted, input, tickHz);
        }

        public void Reconcile(uint snapshotTick, short authX, short authZ, ushort authHeading)
        {
            if (snapshotTick <= lastAckedTick) return;

            var authoritative = ShipPredictor.FromSnapshot(authX, authZ, authHeading);
            var err = Vector3.Distance(
                ShipPredictor.ToWorldPosition(predicted),
                ShipPredictor.ToWorldPosition(authoritative));
            if (err > snapThreshold)
            {
                predicted = authoritative;
                prevPredicted = authoritative;
            }
            else if (err > 0.05f)
            {
                var blend = reconcileBlend;
                predicted.posX += (int)((authoritative.posX - predicted.posX) * blend);
                predicted.posZ += (int)((authoritative.posZ - predicted.posZ) * blend);
                var headingError = ((authHeading - predicted.heading + 512) & 1023) - 512;
                predicted.heading = (ushort)((predicted.heading + (int)(headingError * blend)) & 1023);
            }

            lastAckedTick = snapshotTick;
        }

        public Vector3 GetDisplayPosition()
        {
            var interval = 1f / Mathf.Max(1, tickHz);
            var t = interval > 0f ? Mathf.Clamp01(tickAccumulator / interval) : 1f;
            return Vector3.Lerp(
                ShipPredictor.ToWorldPosition(prevPredicted),
                ShipPredictor.ToWorldPosition(predicted),
                t);
        }

        public float GetDisplayHeading()
        {
            var interval = 1f / Mathf.Max(1, tickHz);
            var t = interval > 0f ? Mathf.Clamp01(tickAccumulator / interval) : 1f;
            var from = ShipPredictor.HeadingToDegrees(prevPredicted.heading);
            var to = ShipPredictor.HeadingToDegrees(predicted.heading);
            return Mathf.LerpAngle(from, to, t);
        }
    }
}
