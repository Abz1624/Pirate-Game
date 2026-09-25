namespace RoguePirates.V4
{
    /// <summary>One simulation step handed to every module, in TickOrder.</summary>
    public readonly struct SimTick
    {
        public readonly long Index;
        public readonly float Dt;
        public readonly float SimTime;

        public SimTick(long index, float dt, float simTime)
        {
            Index = index;
            Dt = dt;
            SimTime = simTime;
        }
    }

    public readonly struct RunStart
    {
        public readonly int Seed;
        public readonly bool IsRetry;

        public RunStart(int seed, bool isRetry)
        {
            Seed = seed;
            IsRetry = isRetry;
        }
    }

    public readonly struct RunEnd
    {
        public readonly float SimTime;
        public readonly bool ObjectiveSecured;

        public RunEnd(float simTime, bool objectiveSecured)
        {
            SimTime = simTime;
            ObjectiveSecured = objectiveSecured;
        }
    }

    /// <summary>
    /// A V4 module. Modules resolve their dependencies only through <see cref="RunContext.Get{T}"/> contracts,
    /// read their own config, publish facts to CombatEvents and never hold another module's concrete type.
    /// </summary>
    public interface IRunModule
    {
        string Id { get; }
        int TickOrder { get; }
        void Initialize(RunContext context);
        void BeginRun(in RunStart start);
        void Tick(in SimTick tick);
        void EndRun(in RunEnd end);
    }
}
