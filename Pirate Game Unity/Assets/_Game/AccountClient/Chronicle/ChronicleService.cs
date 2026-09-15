namespace RoguePirates.AccountClient.Chronicle
{
    public sealed class ChronicleProgress
    {
        public int Marks { get; set; }
        public int TrackLevel { get; set; }
        public bool HasPremiumTrack { get; set; }
    }

    public static class ChronicleService
    {
        public static void GrantMarks(ChronicleProgress progress, int marks)
        {
            progress.Marks += marks;
            while (progress.Marks >= 100)
            {
                progress.Marks -= 100;
                progress.TrackLevel++;
            }
        }
    }
}
