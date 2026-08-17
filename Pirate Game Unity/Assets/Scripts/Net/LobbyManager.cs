using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PirateGame.Net.Generated;
using Solana.Unity.Rpc;
using Solana.Unity.Wallet;
using UnityEngine;

namespace PirateGame.Net
{
    public struct LobbyInfo
    {
        public string matchPubkey;
        public byte tide;
        public byte capacity;
        public byte playerCount;
        public byte state;
        public string authority;
    }

    public interface ILobbyListService
    {
        Task<IReadOnlyList<LobbyInfo>> ListOpenLobbiesAsync();
    }

    public class RpcLobbyListService : ILobbyListService
    {
        readonly BlockchainConfig config;

        public RpcLobbyListService(BlockchainConfig config)
        {
            this.config = config;
        }

        public async Task<IReadOnlyList<LobbyInfo>> ListOpenLobbiesAsync()
        {
            await Task.Delay(10);
            Debug.Log($"[Lobby] Listing lobbies via {config.ErRpc} (not yet indexed)");
            return Array.Empty<LobbyInfo>();
        }
    }

    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] BlockchainConfig config;
        [SerializeField] ErClient erClient;

        ILobbyListService lobbyList;
        Account payer;
        IRpcClient baseRpc;
        IRpcClient erRpc;
        PublicKey configPda;
        PublicKey currentMatch;
        PublicKey currentSnapshot;
        PublicKey currentWorld;
        bool busy;

        public string CurrentMatchAddress => currentMatch?.Key ?? "";
        public byte LocalPlayerSlot { get; private set; }
        public bool HasActiveMatch => currentMatch != null;

        public event Action<IReadOnlyList<LobbyInfo>> OnLobbyListUpdated;
        public event Action OnJoinedLobby;
        public event Action OnMatchStarted;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<BlockchainConfig>("BlockchainConfig");
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
            lobbyList = new RpcLobbyListService(config);
            configPda = PirateCoopProgram.ConfigPda();
            baseRpc = SolanaTx.Client(config.BaseRpc);
            erRpc = SolanaTx.Client(config.ErRpc);
        }

        public async void RefreshLobbyList()
        {
            var list = await lobbyList.ListOpenLobbiesAsync();
            OnLobbyListUpdated?.Invoke(list);
        }

        public void CreateMatch(byte tide, byte capacity = 0)
        {
            var cap = capacity > 0 ? capacity : (byte)config.defaultLobbyCapacity;
            _ = CreateAndDelegateAsync(tide, cap, autoStart: true);
        }

        /// <summary>Single-editor 2-player test: host wallet + player2 wallet, both inputs in one editor.</summary>
        public void CreateLocalDualMatch(byte tide, byte capacity = 0)
        {
            var cap = capacity > 0 ? capacity : (byte)config.defaultLobbyCapacity;
            _ = CreateLocalDualAsync(tide, cap);
        }

        public void CreateLobby(byte tide, byte capacity = 0)
        {
            var cap = capacity > 0 ? capacity : (byte)config.defaultLobbyCapacity;
            _ = CreateAndDelegateAsync(tide, cap, autoStart: false);
        }

        public void StartHostedMatch()
        {
            if (currentMatch == null)
            {
                Debug.LogWarning("[Lobby] no match to start");
                return;
            }
            _ = StartAndDelegateAsync();
        }

        public void JoinMatch(string matchPubkey)
        {
            _ = JoinExistingAsync(new PublicKey(matchPubkey));
        }

        public void SetReady(bool ready)
        {
            if (currentMatch == null) return;
            _ = SendReadyAsync(ready);
        }

        public void StartMatch()
        {
            if (erClient != null && erClient.HasBoundMatch)
            {
                OnMatchStarted?.Invoke();
                return;
            }
            _ = StartAndDelegateAsync();
        }

        async Task CreateAndDelegateAsync(byte tide, byte capacity, bool autoStart)
        {
            if (!Begin()) return;
            try
            {
                erClient?.ClearSecondarySession();
                erClient?.UnbindMatch();

                EnsurePayer();
                await EnsureConfigAsync();

                var seed = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                currentMatch = PirateCoopProgram.MatchPda(payer.PublicKey, seed);
                currentSnapshot = PirateCoopProgram.SnapshotPda(currentMatch);
                currentWorld = PirateCoopProgram.WorldPda(currentMatch);

                Debug.Log($"[Lobby] create_match seed={seed} tide={tide} cap={capacity} mode=PvPvE match={currentMatch}");

                await SolanaTx.SendAsync(baseRpc, payer,
                    PirateCoopProgram.CreateMatch(
                        payer.PublicKey, configPda, currentMatch, currentSnapshot,
                        seed, tide, capacity, PirateCoopProgram.MatchModePvPvE));

                await SolanaTx.WaitForProgramAccountAsync(
                    baseRpc, currentMatch, PirateCoopProgram.ProgramId);

                await EnsureWorldAccountAsync();

                await JoinCurrentAsync();
                await SendReadyAsync(true);
                if (autoStart)
                    await StartAndDelegateAsync();
                else
                {
                    CoopParrelSync.PublishMatchAddress(currentMatch.Key);
                    Debug.Log($"[Lobby] lobby open — share match address: {currentMatch}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Lobby] create failed: {ex.Message}");
            }
            finally { busy = false; }
        }

        async Task JoinExistingAsync(PublicKey matchPda)
        {
            if (!Begin()) return;
            try
            {
                EnsurePayer();
                currentMatch = matchPda;
                currentSnapshot = PirateCoopProgram.SnapshotPda(matchPda);
                currentWorld = PirateCoopProgram.WorldPda(matchPda);
                await JoinCurrentAsync();
                await SendReadyAsync(true);
                Debug.Log("[Lobby] joined lobby — waiting for host to start + delegate…");
                await WaitForDelegationAsync();
                await BindErAsync();
                OnMatchStarted?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Lobby] join failed: {ex.Message}");
            }
            finally { busy = false; }
        }

        async Task JoinCurrentAsync()
        {
            await JoinAsAsync(payer, erClient.SessionAccount.PublicKey);
            LocalPlayerSlot = await ResolveLocalSlotAsync(payer, erClient.SessionAccount.PublicKey);
            Debug.Log($"[Lobby] joined {currentMatch} slot={LocalPlayerSlot} session={erClient.SessionAccount.PublicKey}");
            OnJoinedLobby?.Invoke();
        }

        async Task JoinAsAsync(Account joinPayer, PublicKey session)
        {
            await SolanaTx.SendAsync(baseRpc, joinPayer,
                PirateCoopProgram.JoinMatch(joinPayer.PublicKey, currentMatch, session));
        }

        async Task<byte> ResolveLocalSlotAsync(Account owner, PublicKey session)
        {
            var matchData = await WaitForMatchAccountAsync();
            if (matchData != null)
            {
                if (!MatchDecoder.TryFindPlayerSlot(matchData, owner.PublicKey, session, out var slot))
                {
                    var count = MatchDecoder.ReadPlayerCount(matchData);
                    slot = count > 0 ? (byte)(count - 1) : (byte)0;
                    Debug.LogWarning($"[Lobby] slot decode fallback → {slot} (player_count={count})");
                }
                return slot;
            }
            return 0;
        }

        async Task<byte[]> WaitForMatchAccountAsync()
        {
            for (var i = 0; i < 20; i++)
            {
                var data = await SolanaTx.GetAccountDataAsync(baseRpc, currentMatch);
                if (data != null && data.Length > MatchDecoder.PlayersOffset + MatchDecoder.PlayerSlotLen)
                    return data;
                await Task.Delay(250);
            }
            return await SolanaTx.GetAccountDataAsync(baseRpc, currentMatch);
        }

        async Task WaitForPlayerCountAsync(byte expected)
        {
            for (var i = 0; i < 40; i++)
            {
                var data = await SolanaTx.GetAccountDataAsync(baseRpc, currentMatch);
                if (data != null && MatchDecoder.ReadPlayerCount(data) >= expected)
                    return;
                await Task.Delay(250);
            }

            var latest = await SolanaTx.GetAccountDataAsync(baseRpc, currentMatch);
            var count = latest != null ? MatchDecoder.ReadPlayerCount(latest) : (byte)0;
            throw new TimeoutException($"match {currentMatch} has {count} players, expected {expected}");
        }

        async Task SendReadyAsync(bool ready)
        {
            await SendReadyAsync(payer, ready);
        }

        async Task SendReadyAsync(Account readyPayer, bool ready)
        {
            EnsurePayer();
            await SolanaTx.SendAsync(baseRpc, readyPayer,
                PirateCoopProgram.SetReady(readyPayer.PublicKey, currentMatch, ready));
            Debug.Log($"[Lobby] set_ready={ready} payer={readyPayer.PublicKey}");
        }

        async Task CreateLocalDualAsync(byte tide, byte capacity)
        {
            if (!Begin()) return;
            try
            {
                erClient?.UnbindMatch();

                EnsurePayer();
                await EnsureConfigAsync();

                var seed = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                currentMatch = PirateCoopProgram.MatchPda(payer.PublicKey, seed);
                currentSnapshot = PirateCoopProgram.SnapshotPda(currentMatch);
                currentWorld = PirateCoopProgram.WorldPda(currentMatch);

                Debug.Log($"[Lobby] local dual match seed={seed} tide={tide} cap={capacity} match={currentMatch}");

                await SolanaTx.SendAsync(baseRpc, payer,
                    PirateCoopProgram.CreateMatch(
                        payer.PublicKey, configPda, currentMatch, currentSnapshot,
                        seed, tide, capacity, PirateCoopProgram.MatchModePvPvE));

                await SolanaTx.WaitForProgramAccountAsync(
                    baseRpc, currentMatch, PirateCoopProgram.ProgramId);

                await EnsureWorldAccountAsync();

                await JoinCurrentAsync();
                await WaitForPlayerCountAsync(1);
                await SendReadyAsync(true);

                var payer2 = SolanaTx.LoadSecondaryPayer(config);
                await SolanaTx.AirdropAsync(baseRpc, payer2.PublicKey);
                var session2 = SolanaTx.LoadOrCreateSession(config, "pirate_session_key_client");
                erClient.BindSecondarySession(session2);

                await JoinAsAsync(payer2, session2.PublicKey);
                await WaitForPlayerCountAsync(2);
                await SendReadyAsync(payer2, true);

                Debug.Log($"[Lobby] local dual ready — slot0={payer.PublicKey} slot1={payer2.PublicKey}");
                await StartAndDelegateAsync();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Lobby] local dual failed: {ex.Message}");
                erClient.ClearSecondarySession();
            }
            finally { busy = false; }
        }

        async Task StartAndDelegateAsync()
        {
            if (currentMatch == null) return;
            EnsurePayer();

            var preStart = await SolanaTx.GetAccountDataAsync(baseRpc, currentMatch);
            var preCount = preStart != null ? MatchDecoder.ReadPlayerCount(preStart) : (byte)0;
            Debug.Log($"[Lobby] start_match {currentMatch} players={preCount}");
            if (preCount < 2)
                Debug.LogWarning("[Lobby] only 1 player in match — press P after your co-op partner joins (O)");

            await SolanaTx.SendAsync(baseRpc, payer,
                PirateCoopProgram.StartMatch(payer.PublicKey, currentMatch, currentSnapshot, currentWorld));

            const uint delegateCu = 250_000;
            await SolanaTx.SendAsync(baseRpc, payer,
                PirateCoopProgram.DelegateMatchAccount(payer.PublicKey, currentMatch), delegateCu);
            await SolanaTx.SendAsync(baseRpc, payer,
                PirateCoopProgram.DelegateSnapshotAccount(payer.PublicKey, currentMatch, currentSnapshot), delegateCu);
            await SolanaTx.SendAsync(baseRpc, payer,
                PirateCoopProgram.DelegateWorldAccount(payer.PublicKey, currentMatch, currentWorld), delegateCu);
            Debug.Log($"[Lobby] delegate_match (3 txs) {currentMatch}");

            await WaitForDelegationAsync();
            await BindErAsync();
            OnMatchStarted?.Invoke();
        }

        async Task BindErAsync()
        {
            await SolanaTx.AirdropAsync(erRpc, erClient.SessionAccount.PublicKey);
            if (erClient.HasSecondarySession && erClient.SecondarySessionPublicKey != null)
                await SolanaTx.AirdropAsync(erRpc, erClient.SecondarySessionPublicKey);
            erClient.BindMatch(currentMatch, currentSnapshot, currentWorld);
            await erClient.RefreshMatchAndSnapshotAsync();
        }

        async Task WaitForDelegationAsync()
        {
            var deadline = DateTime.UtcNow.AddSeconds(90);
            var loggedWait = false;
            while (DateTime.UtcNow < deadline)
            {
                var data = await SolanaTx.GetAccountDataAsync(erRpc, currentMatch);
                if (data != null && MatchDecoder.IsDelegatedAndRunning(data))
                {
                    Debug.Log($"[Lobby] ER match live state={MatchDecoder.ReadState(data)} delegated={MatchDecoder.ReadDelegated(data)}");
                    return;
                }

                if (!loggedWait)
                {
                    Debug.Log("[Lobby] waiting for ER delegation (host press P)…");
                    loggedWait = true;
                }
                await Task.Delay(500);
            }
            throw new TimeoutException(
                $"Match {currentMatch} not delegated+running on {config.ErRpc} within 90s (host must press P)");
        }

        async Task EnsureWorldAccountAsync()
        {
            if (currentMatch == null || currentWorld == null)
                throw new InvalidOperationException("match/world PDA not set");

            if (!await SolanaTx.AccountExistsAsync(baseRpc, currentWorld))
            {
                Debug.Log($"[Lobby] init_world {currentWorld}");
                await SolanaTx.SendAsync(baseRpc, payer,
                    PirateCoopProgram.InitWorld(payer.PublicKey, currentMatch, currentWorld));
                await SolanaTx.WaitForProgramAccountAsync(
                    baseRpc, currentWorld, PirateCoopProgram.ProgramId);
            }

            await SolanaTx.AirdropAsync(baseRpc, payer.PublicKey);

            var target = PirateCoopProgram.WorldAccountTotalBytes;
            for (var round = 0; round < 8; round++)
            {
                var data = await SolanaTx.GetAccountDataAsync(baseRpc, currentWorld);
                var len = data?.Length ?? 0;
                if (len >= target)
                    break;

                Debug.Log($"[Lobby] extend_world {currentWorld} len={len} target={target}");
                await SolanaTx.SendAsync(baseRpc, payer,
                    PirateCoopProgram.ExtendWorld(payer.PublicKey, currentMatch, currentWorld));

                var after = await SolanaTx.GetAccountDataAsync(baseRpc, currentWorld);
                if ((after?.Length ?? 0) <= len)
                    break;
            }

            var finalData = await SolanaTx.GetAccountDataAsync(baseRpc, currentWorld);
            if (finalData == null || finalData.Length < target)
                throw new InvalidOperationException(
                    $"world account too small ({finalData?.Length ?? 0} bytes, need {target})");
        }

        async Task EnsureConfigAsync()
        {
            EnsurePayer();
            await EnsureConfigOnRpcAsync(baseRpc, "base");
            await EnsureConfigOnRpcAsync(erRpc, "ER");
        }

        async Task EnsureConfigOnRpcAsync(IRpcClient rpc, string label)
        {
            if (await SolanaTx.AccountExistsAsync(rpc, configPda))
                return;
            Debug.Log($"[Lobby] initialize_config on {label}");
            if (rpc == erRpc)
                await SolanaTx.AirdropAsync(rpc, payer.PublicKey);
            await SolanaTx.SendAsync(rpc, payer,
                PirateCoopProgram.InitializeConfig(payer.PublicKey, configPda, 1));
        }

        void EnsurePayer()
        {
            payer ??= SolanaTx.LoadPayer(config);
        }

        bool Begin()
        {
            if (busy)
            {
                Debug.LogWarning("[Lobby] already sending a transaction");
                return false;
            }
            busy = true;
            return true;
        }
    }
}
