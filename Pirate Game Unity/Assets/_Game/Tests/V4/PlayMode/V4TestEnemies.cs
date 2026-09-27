using RoguePirates.V4.Enemies;
using UnityEngine;

namespace RoguePirates.V4.Tests
{
    /// <summary>M8: the run's V4 enemy world (V3's EnemyWorld left the V4 scenes).</summary>
    public static class V4TestEnemies
    {
        public static EnemySimulation Simulation()
        {
            var game = Object.FindFirstObjectByType<V4Game>();
            return game != null && game.Shell != null && game.Shell.Enemies != null ? game.Shell.Enemies.Simulation : null;
        }
    }
}
