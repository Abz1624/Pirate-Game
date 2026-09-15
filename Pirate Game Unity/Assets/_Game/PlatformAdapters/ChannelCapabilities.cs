using System.Collections.Generic;

namespace RoguePirates.PlatformAdapters
{
    public enum ChannelId
    {
        DirectWeb,
        Steam,
        IOS,
        Android,
        CrazyGames,
        Poki
    }

    public sealed class ChannelCapabilities
    {
        public ChannelId Channel { get; set; }
        public bool AdsSupported { get; set; }
        public bool IapSupported { get; set; }
        public bool BlockchainAllowed { get; set; }

        public static IReadOnlyDictionary<ChannelId, ChannelCapabilities> Matrix { get; } =
            new Dictionary<ChannelId, ChannelCapabilities>
            {
                [ChannelId.Steam] = new ChannelCapabilities { Channel = ChannelId.Steam, AdsSupported = false, IapSupported = true, BlockchainAllowed = false },
                [ChannelId.IOS] = new ChannelCapabilities { Channel = ChannelId.IOS, AdsSupported = true, IapSupported = true, BlockchainAllowed = false },
                [ChannelId.Android] = new ChannelCapabilities { Channel = ChannelId.Android, AdsSupported = true, IapSupported = true, BlockchainAllowed = false },
                [ChannelId.DirectWeb] = new ChannelCapabilities { Channel = ChannelId.DirectWeb, AdsSupported = true, IapSupported = true, BlockchainAllowed = false },
                [ChannelId.CrazyGames] = new ChannelCapabilities { Channel = ChannelId.CrazyGames, AdsSupported = true, IapSupported = false, BlockchainAllowed = false },
                [ChannelId.Poki] = new ChannelCapabilities { Channel = ChannelId.Poki, AdsSupported = true, IapSupported = false, BlockchainAllowed = false }
            };

        public static bool IsFeatureAllowed(ChannelId channel, string feature)
        {
            if (!Matrix.TryGetValue(channel, out var caps)) return false;
            return feature switch
            {
                "ads" => caps.AdsSupported,
                "iap" => caps.IapSupported,
                "blockchain" => caps.BlockchainAllowed,
                _ => false
            };
        }
    }
}
