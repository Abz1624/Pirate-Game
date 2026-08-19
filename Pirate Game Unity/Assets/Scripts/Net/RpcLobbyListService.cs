using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PirateGame.Net.Generated;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using UnityEngine;

namespace PirateGame.Net
{
    public class RpcLobbyListService : ILobbyListService
    {
        public static string LastScanMessage { get; private set; }

        readonly BlockchainConfig config;
        readonly IRpcClient rpc;

        public RpcLobbyListService(BlockchainConfig config)
        {
            this.config = config;
            rpc = SolanaTx.Client(config.BaseRpc);
        }

        public async Task<IReadOnlyList<LobbyInfo>> ListOpenLobbiesAsync()
        {
            try
            {
                LastScanMessage = null;
                Debug.Log($"[Lobby] Scanning open rooms via {config.BaseRpc}");
                var response = await rpc.GetProgramAccountsAsync(
                    PirateCoopProgram.ProgramId,
                    Commitment.Confirmed);

                if (!response.WasSuccessful || response.Result == null)
                {
                    LastScanMessage = FormatScanFailure(response.Reason);
                    Debug.LogWarning($"[Lobby] getProgramAccounts failed: {response.Reason}");
                    return Array.Empty<LobbyInfo>();
                }

                var lobbies = new List<LobbyInfo>();
                foreach (var item in response.Result)
                {
                    var pubkey = item.PublicKey?.ToString();
                    if (string.IsNullOrEmpty(pubkey))
                        continue;

                    var data = DecodeAccountData(item.Account);
                    if (!MatchDecoder.IsOpenLobby(data))
                        continue;

                    lobbies.Add(new LobbyInfo
                    {
                        matchPubkey = pubkey,
                        tide = MatchDecoder.ReadTide(data),
                        capacity = MatchDecoder.ReadCapacity(data),
                        playerCount = MatchDecoder.ReadPlayerCount(data),
                        state = MatchDecoder.ReadState(data),
                        authority = MatchDecoder.ReadAuthority(data)
                    });
                }

                lobbies.Sort((a, b) =>
                {
                    var byPlayers = b.playerCount.CompareTo(a.playerCount);
                    return byPlayers != 0 ? byPlayers : string.Compare(a.matchPubkey, b.matchPubkey, StringComparison.Ordinal);
                });

                Debug.Log($"[Lobby] Found {lobbies.Count} open room(s)");
                if (lobbies.Count == 0)
                    LastScanMessage = "No open rooms on devnet right now.";
                return lobbies;
            }
            catch (Exception ex)
            {
                LastScanMessage = $"Lobby scan failed: {ex.Message}";
                Debug.LogWarning($"[Lobby] lobby scan failed: {ex.Message}");
                return Array.Empty<LobbyInfo>();
            }
        }

        static string FormatScanFailure(string reason)
        {
            if (string.IsNullOrEmpty(reason))
                return "Browse unavailable on this RPC. Use Share Link or Paste Address.";
            if (reason.IndexOf("getProgramAccounts", StringComparison.OrdinalIgnoreCase) >= 0)
                return "Browse needs an RPC with getProgramAccounts (Alchemy Pay-As-You-Go+). Use Share Link instead.";
            return $"Browse unavailable: {reason}";
        }

        static byte[] DecodeAccountData(AccountInfo account)
        {
            if (account?.Data == null || account.Data.Count == 0)
                return Array.Empty<byte>();

            var encoded = account.Data[0];
            if (string.IsNullOrEmpty(encoded))
                return Array.Empty<byte>();

            try
            {
                return Convert.FromBase64String(encoded);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Lobby] account decode failed: {ex.Message}");
                return Array.Empty<byte>();
            }
        }
    }
}
