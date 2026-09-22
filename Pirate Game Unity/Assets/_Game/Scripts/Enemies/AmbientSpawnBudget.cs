using UnityEngine;

namespace HighSeas.Enemies
{
    /// <summary>
    /// Accumulates threat budget while population-blocked; prevents dumping all carried budget on unblock.
    /// </summary>
    public sealed class AmbientSpawnBudget
    {
        public float Accumulated;
        public float MaxCarry = 3f;

        public void Reset() => Accumulated = 0f;

        public void AccumulateWhileBlocked(float deltaTime, float ratePerSecond, bool blocked)
        {
            if (!blocked || ratePerSecond <= 0f || deltaTime <= 0f)
                return;
            Accumulated = Mathf.Min(MaxCarry, Accumulated + deltaTime * ratePerSecond);
        }

        /// <summary>Returns spawns granted this tick (0 or 1).</summary>
        public int TryConsumeOne()
        {
            if (Accumulated < 1f)
                return 0;
            Accumulated -= 1f;
            return 1;
        }
    }
}
