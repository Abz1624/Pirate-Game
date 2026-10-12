using UnityEngine;

namespace RoguePirates.Hackathon
{
    /// <summary>
    /// Hackathon (12 Oct): web3 settings (Resources/HackathonSettings). Devnet only. The wallet adapter prefabs are the
    /// SDK's (its own Resources.Load path does not exist in the package, so they are referenced here for builds).
    /// </summary>
    [CreateAssetMenu(fileName = "HackathonSettings", menuName = "Rogue Pirates/Hackathon/Settings")]
    public sealed class HackathonSettings : ScriptableObject
    {
        public const string ResourcePath = "HackathonSettings";

        [Tooltip("Web3 mode: the menu leads with Connect Wallet instead of the guest name prompt.")]
        public bool web3Primary = true;
        [Tooltip("Custom devnet RPC (https). Empty: the public devnet endpoint.")]
        public string customRpc = "";
        [Tooltip("Custom devnet websocket RPC (wss). Empty: derived by the SDK.")]
        public string customStreamingRpc = "";
        [Tooltip("The SDK's WebGL wallet chooser (shown when more than one browser wallet is installed).")]
        public GameObject walletAdapterUIPrefab;
        public GameObject walletAdapterButtonPrefab;
        [Tooltip("Offer the in-game test wallet (Editor always; development builds when on).")]
        public bool testWalletInDevelopmentBuilds = true;
        [Tooltip("App identity shown by Mobile Wallet Adapter wallets.")]
        public string appName = "Rogue Pirates";
        public string appUri = "https://roguepirates.fun";
        public string appIconPath = "/favicon.ico";

        static HackathonSettings _cached;

        public static HackathonSettings Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<HackathonSettings>(ResourcePath);
            if (_cached == null)
            {
                _cached = CreateInstance<HackathonSettings>();
                _cached.hideFlags = HideFlags.HideAndDontSave;
            }
            return _cached;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => _cached = null;
    }
}
