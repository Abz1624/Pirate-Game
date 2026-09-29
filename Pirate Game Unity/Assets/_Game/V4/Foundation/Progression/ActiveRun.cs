namespace RoguePirates.V4.Foundation.Progression
{
    /// <summary>
    /// Whether a run is active (M15: the part of V3's run context V4 still reads). Set when a run starts; cleared when the
    /// run's stage profile ends (<see cref="Core.AppRoot.EndActiveSaltwindRunContext"/>) and when the menu opens. While
    /// it is set, weapon offers need their unlock, enemy models use the run scale and the home seed can lock.
    /// </summary>
    public static class ActiveRun
    {
        /// <summary>Uniform visual scale for enemy models during a run (the gameplay radius is unchanged).</summary>
        public const float EnemyVisualScale = 0.88f;

        public static bool IsActive { get; set; }
    }
}
