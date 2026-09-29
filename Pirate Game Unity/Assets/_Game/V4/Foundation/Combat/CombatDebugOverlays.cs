namespace RoguePirates.V4.Foundation.Combat
{
    /// <summary>Shared toggle for weapon aim meshes and enemy stat labels (O key).</summary>
    public static class CombatDebugOverlays
    {
        public static bool Visible { get; private set; }

        public static bool Toggle()
        {
            Visible = !Visible;
            CombatExperimentContext.ShowAimVisuals = Visible;
            return Visible;
        }

        public static void SetVisible(bool visible)
        {
            Visible = visible;
            CombatExperimentContext.ShowAimVisuals = visible;
        }
    }
}
