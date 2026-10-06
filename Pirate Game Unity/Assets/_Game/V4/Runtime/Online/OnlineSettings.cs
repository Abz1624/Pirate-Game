using UnityEngine;

namespace RoguePirates.V4.Online
{
    /// <summary>
    /// Playtest (3 Oct, owner): where the playtest API lives (Resources/V4OnlineSettings). The Editor talks to the local
    /// API; builds use <see cref="productionApiBaseUrl"/> (empty = the online features show "Offline"). Overrides for
    /// testing: the page's ?api=https://... on WebGL, or the PlayerPrefs key <see cref="PrefsOverrideKey"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "V4OnlineSettings", menuName = "Rogue Pirates/V4/Online Settings")]
    public sealed class OnlineSettings : ScriptableObject
    {
        public const string ResourcePath = "V4OnlineSettings";
        public const string PrefsOverrideKey = "rp.online.apiBaseUrl";

        [Tooltip("API base URL in the Editor (the local API: cd server/RoguePirates.Api; dotnet run).")]
        public string editorApiBaseUrl = "http://localhost:8080";
        [Tooltip("API base URL in builds, for example https://api.roguepirates.fun. Empty: online features show Offline.")]
        public string productionApiBaseUrl = "";
        [Tooltip("Development builds use the Editor URL (a local API) instead of the production one.")]
        public bool developmentBuildsUseEditorUrl;
        [Tooltip("Hosts a release WebGL build accepts from ?api= (\"*.example.com\" also matches example.com).")]
        public string[] allowedQueryHosts = { "localhost", "127.0.0.1", "*.roguepirates.fun", "api-production-dad8.up.railway.app" }; // Playtest (4 Oct, review): not any railway app
        [Tooltip("Seconds before an API request is given up (offline).")]
        [Min(1)] public int requestTimeoutSeconds = 10;
        [Tooltip("Seconds for the health check on launch.")]
        [Min(1)] public int healthTimeoutSeconds = 5;
        [Tooltip("Map id sent with runs and leaderboard reads.")]
        public string mapId = "saltwind";
        [Range(1, 100)] public int leaderboardLimit = 50;

        static OnlineSettings _cached;

        /// <summary>The settings asset, or defaults when it is missing.</summary>
        public static OnlineSettings Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<OnlineSettings>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<OnlineSettings>();
                _cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return _cached;
        }

        /// <summary>The API base URL for this platform and its overrides, or null (offline).</summary>
        public string ResolveBaseUrl()
        {
            string query = null;
#if UNITY_WEBGL && !UNITY_EDITOR
            query = OnlineRules.QueryValue(Application.absoluteURL, "api");
#endif
            return OnlineRules.ResolveBaseUrl(query, PlayerPrefs.GetString(PrefsOverrideKey, string.Empty), Application.isEditor,
                Debug.isDebugBuild, developmentBuildsUseEditorUrl, editorApiBaseUrl, productionApiBaseUrl, allowedQueryHosts);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _cached = null;
    }
}
