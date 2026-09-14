using HighSeas.World;

namespace HighSeas.V3
{
    public static class V3RunContext
    {
        /// <summary>Uniform visual scale for spawned enemies during V3 runs (gameplay radius unchanged).</summary>
        public const float EnemyVisualScale = 0.88f;

        public static bool IsActive { get; set; }
        public static StageLayout Layout { get; set; }
        public static V3CaptainId CaptainId { get; set; } = V3CaptainId.Rook;
    }
}
