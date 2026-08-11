using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// Ephemeral session key — signs ER transactions without wallet popups.
    /// Sponsor base-layer create/delegate; session key only needs ER lamports top-up if required.
    /// </summary>
    public class SessionKeyStore
    {
        public byte[] PublicKey { get; private set; } = Array.Empty<byte>();
        public byte[] SecretKey { get; private set; } = Array.Empty<byte>();

        public void Generate()
        {
            SecretKey = new byte[64];
            PublicKey = new byte[32];
            // Ed25519 keygen via Solana.Unity-SDK when wired; placeholder for local dev
            var rng = new System.Random();
            rng.NextBytes(SecretKey);
            rng.NextBytes(PublicKey);
            Debug.Log($"[SessionKey] Generated session key (dev placeholder)");
        }

        public void LoadOrCreate(string playerPrefsKey = "pirate_session_key")
        {
            if (PlayerPrefs.HasKey(playerPrefsKey))
            {
                SecretKey = Convert.FromBase64String(PlayerPrefs.GetString(playerPrefsKey + "_sec"));
                PublicKey = Convert.FromBase64String(PlayerPrefs.GetString(playerPrefsKey + "_pub"));
            }
            else
            {
                Generate();
                PlayerPrefs.SetString(playerPrefsKey + "_sec", Convert.ToBase64String(SecretKey));
                PlayerPrefs.SetString(playerPrefsKey + "_pub", Convert.ToBase64String(PublicKey));
                PlayerPrefs.Save();
            }
        }
    }

    /// <summary>
    /// Dual RPC client: base layer + Ephemeral Rollup.
    /// Transaction build/sign runs on a worker thread.
    /// </summary>
    public class ErClient : MonoBehaviour
    {
        [SerializeField] BlockchainConfig config;

        SessionKeyStore sessionKey = new();
        string cachedBlockhash = "";
        float blockhashRefreshTime;
        MatchSnapshot latestSnapshot = new();
        CancellationTokenSource cts;

        public event Action<MatchSnapshot> OnSnapshotReceived;
        public MatchSnapshot LatestSnapshot => latestSnapshot;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<BlockchainConfig>("BlockchainConfig");
            sessionKey.LoadOrCreate();
        }

        void OnEnable()
        {
            cts = new CancellationTokenSource();
            _ = SubscribeSnapshotLoop(cts.Token);
            _ = BlockhashRefreshLoop(cts.Token);
        }

        void OnDisable()
        {
            cts?.Cancel();
            cts?.Dispose();
        }

        public void SubmitInput(float throttle, float steer, byte buttons = 0)
        {
            var t = Mathf.Clamp(throttle, -1f, 1f);
            var s = Mathf.Clamp(steer, -1f, 1f);
            _ = Task.Run(() => SendInputAsync((sbyte)Mathf.RoundToInt(t * 127f), (sbyte)Mathf.RoundToInt(s * 127f), buttons));
        }

        async Task SendInputAsync(sbyte throttle, sbyte steer, byte buttons)
        {
            // Build submit_input instruction via Generated/PirateCoop when anchorgen is run.
            // Routes to config.ErRpc (gasless on ER).
            await Task.Delay(1);
            Debug.Log($"[ER] submit_input throttle={throttle} steer={steer} buttons={buttons} rpc={config?.ErRpc}");
        }

        async Task BlockhashRefreshLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // getBlockhashForAccounts on ER endpoint
                    cachedBlockhash = Guid.NewGuid().ToString("N")[..32];
                    blockhashRefreshTime = Time.realtimeSinceStartup;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ER] blockhash refresh failed: {ex.Message}");
                }
                await Task.Delay(1000, token);
            }
        }

        async Task SubscribeSnapshotLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // accountSubscribe on Snapshot PDA via config.ErWs
                    await Task.Delay(1000 / Mathf.Max(1, config?.tickHz ?? 10), token);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ER] subscribe error: {ex.Message}");
                }
            }
        }

        public void ApplySnapshotBytes(byte[] data)
        {
            var decoded = SnapshotDecoder.Decode(data);
            latestSnapshot = decoded;
            MainThreadDispatcher.Enqueue(() => OnSnapshotReceived?.Invoke(decoded));
        }
    }
}
