using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PirateGame.Net.Generated;
using Solana.Unity.Rpc;
using Solana.Unity.Wallet;
using UnityEngine;

namespace PirateGame.Net
{
    /// <summary>
    /// Dual RPC client: lobby txs on base layer; submit_input + tick_world + snapshot on ER.
    /// </summary>
    public class ErClient : MonoBehaviour
    {
        [SerializeField] BlockchainConfig config;

        Account session;
        Account secondarySession;
        IRpcClient erRpc;
        PublicKey matchPda;
        PublicKey snapshotPda;
        PublicKey worldPda;
        PublicKey configPda;
        MatchSnapshot latestSnapshot = new();
        CancellationTokenSource cts;
        byte matchPlayerCount;

        sbyte lastSentThrottle;
        sbyte lastSentSteer;
        byte lastSentButtons;
        bool hasSentInput;
        float lastInputSendTime = -1f;
        float lastKeepaliveTime = -1f;

        sbyte lastSecondaryThrottle;
        sbyte lastSecondarySteer;
        byte lastSecondaryButtons;
        bool hasSentSecondaryInput;
        float lastSecondaryKeepaliveTime = -1f;
        float lastCrankTime = -1f;
        bool crankInFlight;
        uint lastSnapshotTick;
        float lastSnapshotReceiveTime;
        bool matchLive;
        SnapshotWsSubscriber wsSubscriber;

        const float InputKeepaliveSec = 0.5f;
        const float StaleSnapshotCrankSec = 0.35f;

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
        public PublicKey WorldPda => worldPda;
        public bool HasBoundMatch => matchPda != null;
        public bool MatchLive => matchLive && HasBoundMatch;
        public bool HasSecondarySession => secondarySession != null;
        public PublicKey SecondarySessionPublicKey => secondarySession?.PublicKey;
        public uint LastSnapshotTick => lastSnapshotTick;

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
            wsSubscriber?.Stop();
            cts?.Cancel();
            cts?.Dispose();
        }

        void Update()
        {
            if (!MatchLive) return;

            var now = Time.realtimeSinceStartup;
            var crankInterval = 1f / Mathf.Max(1, config.tickHz);

            if (lastCrankTime < 0f || now - lastCrankTime >= crankInterval)
                _ = CrankTickWorldAsync();
            else if (now - lastSnapshotReceiveTime > StaleSnapshotCrankSec && !crankInFlight)
                _ = CrankTickWorldAsync();

            if (hasSentInput && lastKeepaliveTime >= 0f && now - lastKeepaliveTime >= InputKeepaliveSec)
                SendInputIfChanged(lastSentThrottle, lastSentSteer, lastSentButtons, force: true);

            if (hasSentSecondaryInput && lastSecondaryKeepaliveTime >= 0f &&
                now - lastSecondaryKeepaliveTime >= InputKeepaliveSec)
                SendSecondaryInputIfChanged(lastSecondaryThrottle, lastSecondarySteer, lastSecondaryButtons, force: true);
        }

        public void BindSecondarySession(Account sessionKey)
        {
            secondarySession = sessionKey;
            Debug.Log($"[ER] Secondary session bound {sessionKey.PublicKey}");
        }

        public void ClearSecondarySession()
        {
            secondarySession = null;
            hasSentSecondaryInput = false;
            lastSecondaryKeepaliveTime = -1f;
        }

        public void BindMatch(PublicKey match, PublicKey snapshot, PublicKey world = null)
        {
            matchPda = match;
            snapshotPda = snapshot;
            worldPda = world ?? PirateCoopProgram.WorldPda(match);
            matchLive = true;
            matchPlayerCount = 0;
            lastSnapshotTick = 0;
            lastSnapshotReceiveTime = Time.realtimeSinceStartup;
            hasSentInput = false;
            Debug.Log($"[ER] Bound match={match} snapshot={snapshot} world={worldPda} rpc={config.ErRpc}");

            wsSubscriber?.Stop();
            wsSubscriber = new SnapshotWsSubscriber(config.ErWs, snapshotPda, ApplySnapshotBytes);
            wsSubscriber.Start();

            _ = RefreshMatchAndSnapshotAsync();
        }

        public void UnbindMatch()
        {
            wsSubscriber?.Stop();
            wsSubscriber = null;
            matchPda = null;
            snapshotPda = null;
            worldPda = null;
            matchLive = false;
            matchPlayerCount = 0;
        }

        public void SubmitInput(float throttle, float steer, byte buttons = 0)
        {
            var t = (sbyte)Mathf.RoundToInt(Mathf.Clamp(throttle, -1f, 1f) * 127f);
            var s = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steer, -1f, 1f) * 127f);
            SendInputIfChanged(t, s, buttons);
        }

        public void SubmitSecondaryInput(float throttle, float steer, byte buttons = 0)
        {
            if (secondarySession == null) return;
            var t = (sbyte)Mathf.RoundToInt(Mathf.Clamp(throttle, -1f, 1f) * 127f);
            var s = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steer, -1f, 1f) * 127f);
            SendSecondaryInputIfChanged(t, s, buttons);
        }

        void SendInputIfChanged(sbyte throttle, sbyte steer, byte buttons, bool force = false)
        {
            if (!MatchLive) return;
            if (!force && hasSentInput && throttle == lastSentThrottle && steer == lastSentSteer && buttons == lastSentButtons)
                return;

            lastSentThrottle = throttle;
            lastSentSteer = steer;
            lastSentButtons = buttons;
            hasSentInput = true;
            lastInputSendTime = Time.realtimeSinceStartup;
            lastKeepaliveTime = lastInputSendTime;

            var ix = PirateCoopProgram.SubmitInput(
                SessionAccount.PublicKey, matchPda, worldPda, throttle, steer, buttons);
            _ = SolanaTx.SendFireAndForgetAsync(erRpc, SessionAccount, ix);
        }

        void SendSecondaryInputIfChanged(sbyte throttle, sbyte steer, byte buttons, bool force = false)
        {
            if (!MatchLive || secondarySession == null) return;
            if (!force && hasSentSecondaryInput &&
                throttle == lastSecondaryThrottle && steer == lastSecondarySteer && buttons == lastSecondaryButtons)
                return;

            lastSecondaryThrottle = throttle;
            lastSecondarySteer = steer;
            lastSecondaryButtons = buttons;
            hasSentSecondaryInput = true;
            lastSecondaryKeepaliveTime = Time.realtimeSinceStartup;

            var ix = PirateCoopProgram.SubmitInput(
                secondarySession.PublicKey, matchPda, worldPda, throttle, steer, buttons);
            _ = SolanaTx.SendFireAndForgetAsync(erRpc, secondarySession, ix);
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

        async Task CrankTickWorldAsync()
        {
            if (!MatchLive || worldPda == null || crankInFlight) return;
            crankInFlight = true;
            lastCrankTime = Time.realtimeSinceStartup;
            try
            {
                var ix = PirateCoopProgram.TickWorld(
                    SessionAccount.PublicKey, matchPda, snapshotPda, worldPda, configPda, 1);
                await SolanaTx.SendFireAndForgetAsync(erRpc, SessionAccount, ix, 1_400_000);
                _ = RefreshSnapshotAsync();
            }
            catch (Exception ex)
            {
                if (IsAlreadyProcessedError(ex))
                {
                    _ = RefreshSnapshotAsync();
                    return;
                }
                Debug.LogWarning($"[ER] tick_world failed: {ex.Message}");
            }
            finally
            {
                crankInFlight = false;
            }
        }

        static bool IsAlreadyProcessedError(Exception ex)
        {
            var msg = ex.Message ?? "";
            return msg.IndexOf("already been processed", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        async Task SnapshotPollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (snapshotPda != null && (wsSubscriber == null || !wsSubscriber.IsConnected))
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
            lastSnapshotReceiveTime = Time.realtimeSinceStartup;
            if (decoded.tick >= lastSnapshotTick)
                lastSnapshotTick = decoded.tick;

            MainThreadDispatcher.Enqueue(() => OnSnapshotReceived?.Invoke(decoded));
        }
    }

    /// <summary>WebSocket accountSubscribe fallback for snapshot updates.</summary>
    sealed class SnapshotWsSubscriber
    {
        readonly string wsUrl;
        readonly PublicKey account;
        readonly Action<byte[]> onData;
        CancellationTokenSource cts;
        volatile bool connected;

        public bool IsConnected => connected;

        public SnapshotWsSubscriber(string wsUrl, PublicKey account, Action<byte[]> onData)
        {
            this.wsUrl = wsUrl;
            this.account = account;
            this.onData = onData;
        }

        public void Start()
        {
            Stop();
            cts = new CancellationTokenSource();
            _ = RunAsync(cts.Token);
        }

        public void Stop()
        {
            cts?.Cancel();
            cts = null;
            connected = false;
        }

        async Task RunAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var ws = new System.Net.WebSockets.ClientWebSocket();
                    await ws.ConnectAsync(new Uri(wsUrl), token);
                    connected = true;

                    var sub = $"{{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"accountSubscribe\",\"params\":[\"{account}\",{{\"encoding\":\"base64\",\"commitment\":\"confirmed\"}}]}}";
                    var subBytes = System.Text.Encoding.UTF8.GetBytes(sub);
                    await ws.SendAsync(subBytes, System.Net.WebSockets.WebSocketMessageType.Text, true, token);

                    var buffer = new byte[65536];
                    while (ws.State == System.Net.WebSockets.WebSocketState.Open && !token.IsCancellationRequested)
                    {
                        var result = await ws.ReceiveAsync(buffer, token);
                        if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close)
                            break;

                        var json = System.Text.Encoding.UTF8.GetString(buffer, 0, result.Count);
                        var data = TryExtractAccountData(json);
                        if (data != null)
                            onData(data);
                    }
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ER-WS] {ex.Message}");
                }
                finally
                {
                    connected = false;
                }

                try { await Task.Delay(2000, token); }
                catch (OperationCanceledException) { break; }
            }
        }

        static byte[] TryExtractAccountData(string json)
        {
            const string marker = "\"data\":[\"";
            var idx = json.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0) return null;
            idx += marker.Length;
            var end = json.IndexOf('"', idx);
            if (end <= idx) return null;
            try { return Convert.FromBase64String(json.Substring(idx, end - idx)); }
            catch { return null; }
        }
    }
}
