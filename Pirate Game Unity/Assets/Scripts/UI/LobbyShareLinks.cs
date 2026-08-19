using System;
using PirateGame.Net;
using UnityEngine;

namespace PirateGame.UI
{
    public static class LobbyShareLinks
    {
        public static string BuildMatchUrl(BlockchainConfig config, string matchAddress)
        {
            if (string.IsNullOrWhiteSpace(matchAddress))
                return "";

            var baseUrl = ResolveBaseUrl(config);
            if (string.IsNullOrWhiteSpace(baseUrl))
                return $"?match={matchAddress.Trim()}";

            return $"{baseUrl.TrimEnd('/')}?match={matchAddress.Trim()}";
        }

        public static string ResolveBaseUrl(BlockchainConfig config)
        {
            if (config != null && !string.IsNullOrWhiteSpace(config.webLobbyBaseUrl))
                return config.webLobbyBaseUrl.Trim();

#if UNITY_WEBGL && !UNITY_EDITOR
            return StripQuery(Application.absoluteURL);
#else
            return "";
#endif
        }

        static string StripQuery(string url)
        {
            if (string.IsNullOrEmpty(url))
                return "";
            var q = url.IndexOf('?', StringComparison.Ordinal);
            return q >= 0 ? url[..q] : url;
        }
    }
}
