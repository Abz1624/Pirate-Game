using System;
using System.Threading;
using System.Threading.Tasks;
using PirateGame.Net.Generated;
using Solana.Unity.Rpc;
using Solana.Unity.Wallet;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// Dual RPC client: lobby txs on the base layer, submit_input + snapshot on the ER.
    /// Session key signs ER transactions (no wallet popup per tick).
    /// </summary>
    public class ErClient : MonoBehaviour
    {
        [SerializeField] BlockchainConfig config;

        Account session;
        Account secondarySession;
        IRpcClient erRpc;
        PublicKey matchPda;
        PublicKey snapshotPda;
        PublicKey configPda;
        MatchSnapshot latestSnapshot = new();
        CancellationTokenSource cts;
        byte matchPlayerCount;

        sbyte pendingThrottle;
        sbyte pendingSteer;
        byte pendingButtons;
        bool hasPendingInput;
        sbyte pendingSecondaryThrottle;
        sbyte pendingSecondarySteer;
        byte pendingSecondaryButtons;
        bool hasPendingSecondaryInput;
        bool sendingInput;
        bool sendingSecondaryInput;
        bool matchLive;
        bool loggedSecondarySubmit;
        float lastPrimarySendTime = -1f;
        float lastSecondarySendTime = -1f;
        uint lastLoggedSnapshotTick;

        public event Action<MatchSnapshot> OnSnapshotReceived;
        public MatchSnapshot LatestSnapshot => latestSnapshot;
        public byte MatchPlayerCount => matchPlayerCount;
        public Account SessionAccount
        {
            get
            {
                session ??= SolanaTx.LoadOrCreateSession(config);
                return session;
            }
        }
        public PublicKey MatchPda => matchPda;
        public PublicKey SnapshotPda => snapshotPda;
        public bool HasBoundMatch => matchPda != null;
        public bool MatchLive => matchLive && HasBoundMatch;
        public bool HasSecondarySession => secondarySession != null;
        public PublicKey SecondarySessionPublicKey => secondarySession?.PublicKey;

        void Awake()
        {
            Application.runInBackground = true;
            if (config == null)
                config = Resources.Load<BlockchainConfig>("BlockchainConfig");
            session = SolanaTx.LoadOrCreateSession(config);
            erRpc = SolanaTx.Client(config.ErRpc);
            configPda = PirateCoopProgram.ConfigPda();
        }

        void OnEnable()
        {
            cts = new CancellationTokenSource();
            _ = SnapshotPollLoop(cts.Token);
        }

        void OnDisable()
        {
            cts?.Cancel();
            cts?.Dispose();
        }

        void Update()
        {
            if (!MatchLive) return;
            var interval = 1f / Mathf.Max(1, config.tickHz);

            if (hasPendingInput && !sendingInput)
            {
                if (lastPrimarySendTime < 0f || Time.realtimeSinceStartup - lastPrimarySendTime >= interval)
                {
                    hasPendingInput = false;
                    lastPrimarySendTime = Time.realtimeSinceStartup;
                    _ = SendInputAsync(pendingThrottle, pendingSteer, pendingButtons);
                }
            }

            if (hasPendingSecondaryInput && HasSecondarySession && !sendingSecondaryInput)
            {
                if (lastSecondarySendTime < 0f || Time.realtimeSinceStartup - lastSecondarySendTime >= interval)
                {
                    hasPendingSecondaryInput = false;
                    lastSecondarySendTime = Time.realtimeSinceStartup;
                    _ = SendSecondaryInputAsync(pendingSecondaryThrottle, pendingSecondarySteer, pendingSecondaryButtons);
                }
            }
        }

        public void BindSecondarySession(Account sessionKey)
        {
            secondarySession = sessionKey;
            Debug.Log($"[ER] Secondary session bound {sessionKey.PublicKey}");
        }

        public void ClearSecondarySession()
        {
            secondarySession = null;
            hasPendingSecondaryInput = false;
        }

        public void BindMatch(PublicKey match, PublicKey snapshot)
        {
            matchPda = match;
            snapshotPda = snapshot;
            matchLive = true;
            matchPlayerCount = 0;
            lastLoggedSnapshotTick = 0;
            loggedSecondarySubmit = false;
            Debug.Log($"[ER] Bound match={match} snapshot={snapshot} rpc={config.ErRpc}");
            _ = RefreshMatchAndSnapshotAsync();
        }

        public void UnbindMatch()
        {
            matchPda = null;
            snapshotPda = null;
            matchLive = false;
            matchPlayerCount = 0;
            hasPendingInput = false;
        }

        public void SubmitInput(float throttle, float steer, byte buttons = 0)
        {
            var t = (sbyte)Mathf.RoundToInt(Mathf.Clamp(throttle, -1f, 1f) * 127f);
            var s = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steer, -1f, 1f) * 127f);
            if (t == 0 && s == 0) return;
            pendingThrottle = t;
            pendingSteer = s;
            pendingButtons = buttons;
            hasPendingInput = true;
        }

        public void SubmitSecondaryInput(float throttle, float steer, byte buttons = 0)
        {
            if (!HasSecondarySession) return;
            var t = (sbyte)Mathf.RoundToInt(Mathf.Clamp(throttle, -1f, 1f) * 127f);
            var s = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steer, -1f, 1f) * 127f);
            if (t == 0 && s == 0) return;
            pendingSecondaryThrottle = t;
            pendingSecondarySteer = s;
            pendingSecondaryButtons = buttons;
            hasPendingSecondaryInput = true;
        }

        public async Task RefreshMatchAndSnapshotAsync()
        {
            if (!MatchLive) return;

            if (matchPda != null)
            {
                var matchData = await SolanaTx.GetAccountDataAsync(erRpc, matchPda);
                if (matchData != null)
                    matchPlayerCount = Math.Max(matchPlayerCount, MatchDecoder.ReadPlayerCount(matchData));
            }

            await RefreshSnapshotAsync();
        }

        public async Task RefreshSnapshotAsync()
        {
            if (!MatchLive || snapshotPda == null) return;
            var data = await SolanaTx.GetAccountDataAsync(erRpc, snapshotPda);
            if (data != null)
                ApplySnapshotBytes(data);
        }

        async Task SendInputAsync(sbyte throttle, sbyte steer, byte buttons)
        {
            if (!MatchLive) return;
            sendingInput = true;
            try
            {
                var ix = PirateCoopProgram.SubmitInput(
                    SessionAccount.PublicKey, matchPda, snapshotPda, configPda, throttle, steer, buttons);
                var sig = await SolanaTx.SendAsync(erRpc, SessionAccount, ix);
                if (Time.frameCount % 30 == 0)
                    Debug.Log($"[ER] submit_input slot0 {throttle},{steer} sig={sig}");
                await RefreshSnapshotAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ER] submit_input failed: {ex.Message}");
            }
            finally
            {
                sendingInput = false;
            }
        }

        async Task SendSecondaryInputAsync(sbyte throttle, sbyte steer, byte buttons)
        {
            if (!MatchLive || !HasSecondarySession) return;
            sendingSecondaryInput = true;
            try
            {
                var ix = PirateCoopProgram.SubmitInput(
                    secondarySession.PublicKey, matchPda, snapshotPda, configPda, throttle, steer, buttons);
                var sig = await SolanaTx.SendAsync(erRpc, secondarySession, ix);
                if (!loggedSecondarySubmit || Time.frameCount % 30 == 0)
                {
                    loggedSecondarySubmit = true;
                    Debug.Log($"[ER] submit_input slot1 {throttle},{steer} sig={sig}");
                }
                await RefreshSnapshotAsync();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ER] secondary submit_input failed: {ex.Message}");
            }
            finally
            {
                sendingSecondaryInput = false;
            }
        }

        async Task SnapshotPollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (snapshotPda != null)
                        await RefreshSnapshotAsync();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ER] snapshot poll: {ex.Message}");
                }

                var delay = 1000 / Mathf.Max(1, config?.tickHz ?? 10);
                try { await Task.Delay(delay, token); }
                catch (OperationCanceledException) { break; }
            }
        }

        public void ApplySnapshotBytes(byte[] data)
        {
            if (data.Length > 44)
                matchPlayerCount = Math.Max(matchPlayerCount, data[44]);

            var decoded = SnapshotDecoder.Decode(data, matchPlayerCount);
            latestSnapshot = decoded;
            if (decoded.tick != lastLoggedSnapshotTick && decoded.players.Length > 0)
            {
                lastLoggedSnapshotTick = decoded.tick;
                if (decoded.tick <= 3 || decoded.tick % 20 == 0)
                {
                    var p0 = decoded.players.Length > 0 ? decoded.players[0] : default;
                    var p1 = decoded.players.Length > 1 ? decoded.players[1] : default;
                    Debug.Log(
                        $"[ER] snapshot tick={decoded.tick} players={decoded.players.Length} " +
                        $"p0=({p0.x},{p0.z}) p1=({p1.x},{p1.z}) matchPlayers={matchPlayerCount}");
                }
            }
            MainThreadDispatcher.Enqueue(() => OnSnapshotReceived?.Invoke(decoded));
        }
    }
}
