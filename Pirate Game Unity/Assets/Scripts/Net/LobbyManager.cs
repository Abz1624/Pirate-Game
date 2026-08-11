using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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

    /// <summary>
    /// Lobby listing abstraction — swap getProgramAccounts for indexer at scale.
    /// </summary>
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
            // getProgramAccounts on ER RPC filtered by MatchState::Lobby
            await Task.Delay(10);
            Debug.Log($"[Lobby] Listing lobbies via {config.ErRpc}");
            return Array.Empty<LobbyInfo>();
        }
    }

    public class LobbyManager : MonoBehaviour
    {
        [SerializeField] BlockchainConfig config;
        [SerializeField] ErClient erClient;

        ILobbyListService lobbyList;
        SessionKeyStore sessionKey = new();

        public event Action<IReadOnlyList<LobbyInfo>> OnLobbyListUpdated;
        public event Action OnJoinedLobby;
        public event Action OnMatchStarted;

        string currentMatchPubkey;
        byte localSlot;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<BlockchainConfig>("BlockchainConfig");
            lobbyList = new RpcLobbyListService(config);
            sessionKey.LoadOrCreate();
        }

        public async void RefreshLobbyList()
        {
            var list = await lobbyList.ListOpenLobbiesAsync();
            OnLobbyListUpdated?.Invoke(list);
        }

        public void CreateMatch(byte tide, byte capacity = 0)
        {
            var cap = capacity > 0 ? capacity : (byte)config.defaultLobbyCapacity;
            Debug.Log($"[Lobby] create_match tide={tide} capacity={cap} base={config.BaseRpc}");
            // Base-layer tx: create_match + delegate_match (sponsored)
        }

        public void JoinMatch(string matchPubkey)
        {
            currentMatchPubkey = matchPubkey;
            Debug.Log($"[Lobby] join_match {matchPubkey} session={Convert.ToBase64String(sessionKey.PublicKey)}");
            OnJoinedLobby?.Invoke();
        }

        public void SetReady(bool ready)
        {
            Debug.Log($"[Lobby] set_ready={ready}");
        }

        public void SelectHull(byte hullId)
        {
            Debug.Log($"[Lobby] hull={hullId}");
        }

        public void StartMatch()
        {
            OnMatchStarted?.Invoke();
        }
    }
}
