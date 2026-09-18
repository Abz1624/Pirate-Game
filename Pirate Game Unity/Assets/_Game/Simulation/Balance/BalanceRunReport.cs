namespace RoguePirates.Simulation.Balance
{
    public sealed class BalanceRunReport
    {
        public string ProfileId;
        public int Seed;
        public int Ticks;
        public string InputPolicy;
        public int Kills;
        public int Level;
        public bool ObjectiveSecured;
        public bool PlayerSurvived;
        public float SimSeconds;
        public string StateHash;
        public int PeakEnemyCount;
        public string HarnessVersion = "balance_v1";
    }
}
