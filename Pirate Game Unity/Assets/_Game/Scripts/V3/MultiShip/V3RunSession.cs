namespace HighSeas.V3.MultiShip
{
    /// <summary>Active run mode for the loaded scene (set from <see cref="V3MultiShipSceneConfig"/>).</summary>
    public static class V3RunSession
    {
        public static V3RunSessionKind Kind { get; private set; } = V3RunSessionKind.SinglePlayer;

        public static bool IsPvP => Kind == V3RunSessionKind.PvP;
        public static bool IsCoOp => Kind == V3RunSessionKind.CoOp;
        public static bool IsSplitScreenCoOp => Kind == V3RunSessionKind.SplitScreenCoOp;
        public static bool IsLocalMultiplayer =>
            Kind == V3RunSessionKind.PvP ||
            Kind == V3RunSessionKind.CoOp ||
            Kind == V3RunSessionKind.SplitScreenCoOp;
        public static bool IsMultiShip => Kind != V3RunSessionKind.SinglePlayer;
        public static bool AllowShipVsShipCombat => IsPvP;

        public static void Configure(V3RunSessionKind kind) => Kind = kind;

        public static void Reset() => Kind = V3RunSessionKind.SinglePlayer;
    }
}
