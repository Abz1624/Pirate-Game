using System;

namespace RoguePirates.Simulation.Combat
{
    public sealed class LastRumSystem
    {
        public bool HasLastRum { get; private set; } = true;
        public int ConsumptionsThisTick { get; private set; }

        public void BeginTick() => ConsumptionsThisTick = 0;

        /// <summary>Returns true if lethal damage was prevented by Last Rum this tick.</summary>
        public bool TryPreventLethal(ref SimFixed hull)
        {
            if (hull.Raw > 0) return false;
            if (!HasLastRum) return false;
            if (ConsumptionsThisTick > 0) return false;

            HasLastRum = false;
            ConsumptionsThisTick = 1;
            hull = SimFixed.FromFloat(SimConstants.DefaultHull * 0.25f);
            return true;
        }
    }
}
