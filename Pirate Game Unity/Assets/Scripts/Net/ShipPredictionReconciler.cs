using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>Client-side prediction with input-replay reconciliation.</summary>
    public class ShipPredictionReconciler : MonoBehaviour
    {
        [SerializeField] float snapThreshold = 12f;
        [SerializeField] int tickHz = 10;

        ShipPredictor.ShipState predicted;
        uint lastAckedTick;
        float tickAccumulator;

        public ShipPredictor.ShipState Predicted => predicted;

        public void ResetState(short x, short z, ushort heading)
        {
            predicted = ShipPredictor.FromSnapshot(x, z, heading);
            lastAckedTick = 0;
            tickAccumulator = 0f;
        }

        public void SyncToServerTick(uint serverTick, short x, short z, ushort heading)
        {
            predicted = ShipPredictor.FromSnapshot(x, z, heading);
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
            }
            else
            {
                // The compact snapshot has no velocity. Keep locally predicted
                // momentum and smoothly pull position/heading toward authority.
                predicted.posX += (authoritative.posX - predicted.posX) / 2;
                predicted.posZ += (authoritative.posZ - predicted.posZ) / 2;
                var headingError = ((authHeading - predicted.heading + 512) & 1023) - 512;
                predicted.heading = (ushort)((predicted.heading + headingError / 2) & 1023);
            }

            lastAckedTick = snapshotTick;
        }

        public Vector3 GetDisplayPosition() => ShipPredictor.ToWorldPosition(predicted);

        public float GetDisplayHeading() => ShipPredictor.HeadingToDegrees(predicted.heading);
    }
}
