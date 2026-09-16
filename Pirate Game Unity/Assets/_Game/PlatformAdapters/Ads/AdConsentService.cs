using RoguePirates.PlatformAdapters;

namespace RoguePirates.PlatformAdapters.Ads
{
    public sealed class AdConsentState
    {
        public bool HasConsent { get; set; }
        public bool IsMinor { get; set; }
        public int DailyRewardedViews { get; set; }
        public int DailyCap { get; set; } = 5;
    }

    public static class AdConsentService
    {
        public static bool CanShowRewarded(AdConsentState state, ChannelId channel)
        {
            if (!ChannelCapabilities.IsFeatureAllowed(channel, "ads")) return false;
            if (!state.HasConsent) return false;
            if (state.IsMinor) return false;
            return state.DailyRewardedViews < state.DailyCap;
        }
    }
}
