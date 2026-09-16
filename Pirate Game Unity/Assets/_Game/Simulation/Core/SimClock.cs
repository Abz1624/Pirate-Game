using System;

namespace RoguePirates.Simulation
{
    /// <summary>Pause reasons as a set — clearing one does not clear others.</summary>
    public sealed class PauseReasonSet
    {
        SimPauseReason _active;

        public bool IsPaused => _active != 0;
        public SimPauseReason Active => _active;

        public void Add(SimPauseReason reason) => _active |= reason;

        public void Remove(SimPauseReason reason) => _active &= ~reason;

        public bool Has(SimPauseReason reason) => (_active & reason) != 0;

        public bool BlocksSimulation => IsPaused;
    }

    public sealed class SimClock
    {
        public int Tick { get; private set; }
        public float SimSeconds => Tick * SimConstants.TickDurationSeconds;
        public float WallSeconds { get; private set; }

        public void AdvanceTick(bool simulationBlocked)
        {
            if (!simulationBlocked)
                Tick++;
        }

        public void AdvanceWall(float deltaSeconds) => WallSeconds += deltaSeconds;

        public bool IsOvertime => SimSeconds > SimConstants.StageDurationSeconds;
    }
}
