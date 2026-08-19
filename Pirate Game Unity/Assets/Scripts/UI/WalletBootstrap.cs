using System;
using System.IO;
using System.Threading.Tasks;
using PirateGame.Net;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using UnityEngine;

namespace PirateGame.UI
{
    /// <summary>
    /// Ensures Solana Unity Web3 is present and aligned with BlockchainConfig.
    /// WebGL builds require wallet-adapter login; editor can fall back to local keypair files.
    /// </summary>
    public class WalletBootstrap : MonoBehaviour
    {
        const string WalletControllerResource = "SolanaUnitySDK/[WalletController]";

        [SerializeField] BlockchainConfig config;

        public bool IsConnected => Web3.Account != null;
        public string WalletAddress => Web3.Account?.PublicKey.ToString() ?? "";

        public event Action OnWalletConnected;
        public event Action OnWalletDisconnected;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<BlockchainConfig>("BlockchainConfig");

            EnsureWeb3Instance();
            ApplyNetworkFromConfig();
        }

        void EnsureWeb3Instance()
        {
            if (Web3.Instance != null)
                return;

            var prefab = Resources.Load<GameObject>(WalletControllerResource);
            if (prefab == null)
            {
                Debug.LogWarning("[Wallet] WalletController prefab not found in Resources.");
                return;
            }

            Instantiate(prefab);
            Debug.Log("[Wallet] WalletController instantiated from Resources.");
        }

        public void ApplyNetworkFromConfig()
        {
            if (Web3.Instance == null || config == null)
                return;

            if (config.mode == BlockchainConfig.NetworkMode.Devnet)
            {
                Web3.Instance.rpcCluster = RpcCluster.DevNet;
                Web3.Instance.customRpc = config.devnetBaseRpc;
            }
            else
            {
                Web3.Instance.rpcCluster = RpcCluster.DevNet;
                Web3.Instance.customRpc = config.localBaseRpc;
            }
        }

        public bool RequiresWalletLogin()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return Web3.Account == null;
#else
            if (Web3.Account != null)
                return false;
            return !HasLocalKeypairFile();
#endif
        }

        public static bool HasLocalKeypairFile(BlockchainConfig cfg = null)
        {
            cfg ??= Resources.Load<BlockchainConfig>("BlockchainConfig");
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.localPayerKeypairPath))
                return false;
            var path = System.Environment.ExpandEnvironmentVariables(cfg.localPayerKeypairPath);
            return !string.IsNullOrEmpty(path) && File.Exists(path);
        }

        public async Task<bool> ConnectWalletAsync()
        {
            EnsureWeb3Instance();
            ApplyNetworkFromConfig();

            if (Web3.Instance == null)
            {
                Debug.LogError("[Wallet] Web3.Instance is null — add WalletController to the scene.");
                return false;
            }

            if (Web3.Account != null)
            {
                OnWalletConnected?.Invoke();
                return true;
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            var account = await Web3.Instance.LoginWalletAdapter();
#else
            Account account = null;
            try
            {
                account = await Web3.Instance.LoginWalletAdapter();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Wallet] Wallet adapter login failed in editor: {ex.Message}");
            }
#endif

            if (account != null)
            {
                Debug.Log($"[Wallet] Connected {account.PublicKey}");
                OnWalletConnected?.Invoke();
                return true;
            }

            return false;
        }

        public void Disconnect()
        {
            Web3.Instance?.Logout();
            OnWalletDisconnected?.Invoke();
        }
    }
}
