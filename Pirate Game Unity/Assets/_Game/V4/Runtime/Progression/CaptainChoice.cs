namespace RoguePirates.V4.Progression
{
    /// <summary>
    /// The Captain chosen in the menu, as the run reads it at start (M14.13): the shared saved selection (through the
    /// content port until the M15 move of shared code), by the Captain config's V3 id.
    /// </summary>
    public static class CaptainChoice
    {
        public static int LegacyId
        {
            get => RoguePirates.V4.Ports.ContentPort.SelectedCaptainId;
            set => RoguePirates.V4.Ports.ContentPort.SelectedCaptainId = value;
        }
    }
}
