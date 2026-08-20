using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PirateGame.Net.Generated;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Models;
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
        Account gameplaySigner;
        Account secondarySession;
        Account secondaryGameplaySigner;
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
        float lastSnapshotAdvanceTime;
        bool matchLive;
        bool crankTicks = true;
        byte localPlayerSlot;
        bool loggedMultiPlayerSnapshot;
        bool loggedCrankInputs;
        int crankWithInputCount;
        int submitInputFailCount;
        SnapshotWsSubscriber wsSubscriber;

        const float InputKeepaliveSec = 0.09f;
        const float StaleSnapshotCrankSec = 0.2f;
        const int SnapshotFallbackPollMs = 500;

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
            gameplaySigner = TryLoadGameplaySigner(config);
            erRpc = SolanaTx.Client(config.ErRpc);
            configPda = PirateCoopProgram.ConfigPda();
        }

        static Account TryLoadGameplaySigner(BlockchainConfig cfg = null)
        {
            try
            {
                return SolanaTx.LoadPayer(cfg);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ER] No local payer keypair for gameplay txs — using session key ({ex.Message})");
                return null;
            }
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

        async void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus || !MatchLive) return;
            try
            {
                await RefreshMatchAndSnapshotAsync();
                if (crankTicks)
                    await CrankTickWorldAsync();
                Debug.Log($"[ER] Focus restored — refreshed snapshot tick={lastSnapshotTick}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ER] Focus refresh failed: {ex.Message}");
            }
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused)
                OnApplicationFocus(true);
        }

        void Update()
        {
            if (!MatchLive) return;

            var now = Time.realtimeSinceStartup;
            var crankInterval = 1f / Mathf.Max(1, config.tickHz);

            if (crankTicks)
            {
                if (lastCrankTime < 0f || now - lastCrankTime >= crankInterval)
                    _ = CrankTickWorldAsync();
                else if (now - lastSnapshotAdvanceTime > StaleSnapshotCrankSec && !crankInFlight)
                    _ = CrankTickWorldAsync();
            }
        }

        public void BindSecondarySession(Account sessionKey, Account ownerSigner = null)
        {
            secondarySession = sessionKey;
            secondaryGameplaySigner = ownerSigner ?? TryLoadGameplaySigner(config);
            Debug.Log($"[ER] Secondary session bound {sessionKey.PublicKey}");
        }

        public void ClearSecondarySession()
        {
            secondarySession = null;
            secondaryGameplaySigner = null;
            hasSentSecondaryInput = false;
            lastSecondaryKeepaliveTime = -1f;
        }

        public void SetLocalPlayerSlot(byte slot) => localPlayerSlot = slot;

        public void BindMatch(PublicKey match, PublicKey snapshot, PublicKey world = null, bool crankTicks = true)
        {
            matchPda = match;
            snapshotPda = snapshot;
            worldPda = world ?? PirateCoopProgram.WorldPda(match);
            matchLive = true;
            this.crankTicks = crankTicks;
            matchPlayerCount = 0;
            loggedMultiPlayerSnapshot = false;
            loggedCrankInputs = false;
            crankWithInputCount = 0;
            submitInputFailCount = 0;
            lastSnapshotTick = 0;
            lastSnapshotReceiveTime = Time.realtimeSinceStartup;
            lastSnapshotAdvanceTime = lastSnapshotReceiveTime;
            hasSentInput = false;
            CoopInputRelay.Clear();
            var signer = ResolveGameplaySigner();
            Debug.Log(
                $"[ER] Bound match={match} snapshot={snapshot} world={worldPda} rpc={config.ErRpc} " +
                $"signer={signer.PublicKey} crank={(crankTicks ? "host" : "off")}");

            wsSubscriber?.Stop();
            wsSubscriber = new SnapshotWsSubscriber(config.ErWs, snapshotPda, ApplySnapshotBytes);
            wsSubscriber.Start();

            _ = RefreshMatchAndSnapshotAsync();
            _ = InitialCrankBurstAsync();
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
            CoopInputRelay.Clear();
        }

        /// <summary>
        /// Publish local input to the ParrelSync relay file. The host crank embeds all
        /// player inputs in tick_world because delegated world accounts are not writable
        /// by non-host wallets on MagicBlock ER.
        /// </summary>
        public void PublishLocalInput(byte slot, sbyte throttle, sbyte steer)
        {
            if (!MatchLive) return;
            CoopInputRelay.Publish(slot, throttle, steer);
        }

        public void SubmitInput(float throttle, float steer, byte buttons = 0, bool force = false)
        {
            // Kept for API compatibility; ship controllers should call PublishLocalInput with slot.
        }

        public void SubmitSecondaryInput(float throttle, float steer, byte buttons = 0, bool force = false)
        {
            if (secondarySession == null) return;
            var t = (sbyte)Mathf.RoundToInt(Mathf.Clamp(throttle, -1f, 1f) * 127f);
            var s = (sbyte)Mathf.RoundToInt(Mathf.Clamp(steer, -1f, 1f) * 127f);
            SendSecondaryInputIfChanged(t, s, buttons, force);
        }

        Account ResolveGameplaySigner(Account sessionFallback = null, Account ownerFallback = null)
        {
            return ownerFallback ?? gameplaySigner ?? sessionFallback ?? SessionAccount;
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

            var signer = ResolveGameplaySigner();
            var ix = PirateCoopProgram.SubmitInput(
                signer.PublicKey, matchPda, worldPda, throttle, steer, buttons);
            _ = SendInputAsync(signer, ix, isSecondary: false);
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

            var signer = ResolveGameplaySigner(secondarySession, secondaryGameplaySigner);
            var ix = PirateCoopProgram.SubmitInput(
                signer.PublicKey, matchPda, worldPda, throttle, steer, buttons);
            _ = SendInputAsync(signer, ix, isSecondary: true);
        }

        async Task SendInputAsync(Account signer, TransactionInstruction ix, bool isSecondary)
        {
            try
            {
                await SolanaTx.SendFireAndForgetAsync(
                    erRpc,
                    signer,
                    ix,
                    skipPreflight: false);
            }
            catch (Exception ex)
            {
                if (isSecondary)
                    return;

                submitInputFailCount++;
                if (submitInputFailCount <= 5 || submitInputFailCount % 25 == 0)
                {
                    Debug.LogWarning(
                        $"[ER] submit_input failed ({submitInputFailCount}x) signer={signer.PublicKey}: {ex.Message}");
                }
            }
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

        async Task InitialCrankBurstAsync()
        {
            if (!crankTicks) return;
            for (var i = 0; i < 3 && MatchLive; i++)
            {
                await CrankTickWorldAsync();
                await Task.Delay(200);
            }
        }

        async Task CrankTickWorldAsync()
        {
            if (!MatchLive || !crankTicks || worldPda == null || crankInFlight) return;
            crankInFlight = true;
            lastCrankTime = Time.realtimeSinceStartup;
            try
            {
                var throttles = new sbyte[CoopInputRelay.MaxSlots];
                var steers = new sbyte[CoopInputRelay.MaxSlots];
                var inputCount = Math.Max(matchPlayerCount, (byte)2);
                CoopInputRelay.ReadInto(throttles, steers, inputCount);
                LogCrankInputsIfNeeded(throttles, steers, inputCount);

                var signer = ResolveGameplaySigner();
                var ix = PirateCoopProgram.TickWorld(
                    signer.PublicKey,
                    matchPda,
                    snapshotPda,
                    worldPda,
                    configPda,
                    1,
                    inputCount,
                    throttles,
                    steers);
                await SolanaTx.SendFireAndForgetAsync(
                    erRpc,
                    signer,
                    ix,
                    1_400_000,
                    skipPreflight: false);
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

        void LogCrankInputsIfNeeded(sbyte[] throttles, sbyte[] steers, byte inputCount)
        {
            var hasInput = false;
            for (var i = 0; i < inputCount && i < throttles.Length; i++)
            {
                if (throttles[i] != 0 || steers[i] != 0)
                {
                    hasInput = true;
                    break;
                }
            }

            if (!hasInput)
                return;

            crankWithInputCount++;
            if (loggedCrankInputs && crankWithInputCount % 30 != 1)
                return;

            loggedCrankInputs = true;
            var t0 = inputCount > 0 ? throttles[0] : (sbyte)0;
            var s0 = inputCount > 0 ? steers[0] : (sbyte)0;
            var t1 = inputCount > 1 ? throttles[1] : (sbyte)0;
            var s1 = inputCount > 1 ? steers[1] : (sbyte)0;
            Debug.Log(
                $"[ER] crank inputs count={inputCount} localSlot={localPlayerSlot} " +
                $"p0(t={t0},s={s0}) p1(t={t1},s={s1})");
        }

        async Task SnapshotPollLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    // Poll periodically even with a connected WebSocket. This recovers from
                    // suspended editors and subscriptions that remain connected but go stale.
                    if (MatchLive && snapshotPda != null)
                        await RefreshSnapshotAsync();
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ER] snapshot poll: {ex.Message}");
                }

                try { await Task.Delay(SnapshotFallbackPollMs, token); }
                catch (OperationCanceledException) { break; }
            }
        }

        public void ApplySnapshotBytes(byte[] data)
        {
            if (data.Length > 44)
                matchPlayerCount = Math.Max(matchPlayerCount, data[44]);

            var decoded = SnapshotDecoder.Decode(data, matchPlayerCount);
            if (decoded.tick < lastSnapshotTick)
                return;

            var prevTick = lastSnapshotTick;
            latestSnapshot = decoded;
            lastSnapshotReceiveTime = Time.realtimeSinceStartup;
            if (decoded.tick > lastSnapshotTick)
            {
                lastSnapshotTick = decoded.tick;
                lastSnapshotAdvanceTime = lastSnapshotReceiveTime;
            }

            if (decoded.players.Length >= 2 && matchLive &&
                (!loggedMultiPlayerSnapshot || decoded.tick != prevTick))
            {
                var p0 = decoded.players[0];
                var p1 = decoded.players[1];
                Debug.Log(
                    $"[ER] snapshot players={decoded.players.Length} " +
                    $"p0(idx={p0.slot},{p0.x},{p0.z}) p1(idx={p1.slot},{p1.x},{p1.z}) tick={decoded.tick}");
                if (decoded.tick > 0)
                    loggedMultiPlayerSnapshot = true;
            }

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
