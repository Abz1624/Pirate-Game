namespace RoguePirates.V4.Events
{
    /// <summary>
    /// Playtest (6 Oct, owner): who is hitting the ship right now (an enemy id, "hazard", "island", "stage_hit",
    /// "boss_director"), set around the hull calls so the PlayerDamaged event carries it in its tag (the session recorder
    /// sums damage by source). One per run; null = unknown. Assignments only, no allocation.
    /// </summary>
    public sealed class PlayerDamageSource
    {
        public string Current;
    }
}
