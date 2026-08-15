using UnityEngine;
using UnityEngine.InputSystem;

namespace PirateGame.Net
{
    /// <summary>
    /// Keyboard shortcuts for local ER testing (no LobbyUI required).
    /// U = host lobby (create + join + ready, wait for players)
    /// I = solo quick match (create through delegate)
    /// K = local 2-player match in one editor (WASD + arrow keys)
    /// O = join lobby (reads .coop-match on clone, or Join Match Address field)
    /// P = host start + delegate after everyone is ready
    /// </summary>
    public class CoopLocalHotkeys : MonoBehaviour
    {
        [SerializeField] LobbyManager lobbyManager;
        [SerializeField] ErClient erClient;
        [SerializeField] byte tide = 1;
        [SerializeField] string joinMatchAddress;

        void Awake()
        {
            if (lobbyManager == null)
                lobbyManager = FindFirstObjectByType<LobbyManager>();
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
        }

        void Update()
        {
            if (lobbyManager == null || Keyboard.current == null) return;

            var matchLive = erClient != null && erClient.MatchLive;
            if (matchLive)
                return;

            if (Keyboard.current.uKey.wasPressedThisFrame)
            {
                Debug.Log($"[Coop] U → CreateLobby tide={tide}");
                lobbyManager.CreateLobby(tide);
            }
            if (Keyboard.current.iKey.wasPressedThisFrame && !CoopParrelSync.IsCloneEditor)
            {
                Debug.Log($"[Coop] I → CreateMatch tide={tide}");
                lobbyManager.CreateMatch(tide);
            }
            if (Keyboard.current.kKey.wasPressedThisFrame && !CoopParrelSync.IsCloneEditor)
            {
                Debug.Log($"[Coop] K → CreateLocalDualMatch tide={tide}");
                lobbyManager.CreateLocalDualMatch(tide);
            }
            if (Keyboard.current.oKey.wasPressedThisFrame)
            {
                var match = ResolveJoinAddress();
                if (string.IsNullOrWhiteSpace(match))
                {
                    Debug.LogWarning("[Coop] O → no match address. Host press U first, or set Join Match Address.");
                    return;
                }
                Debug.Log($"[Coop] O → JoinMatch {match}");
                lobbyManager.JoinMatch(match);
            }
            if (Keyboard.current.pKey.wasPressedThisFrame && !CoopParrelSync.IsCloneEditor)
            {
                Debug.Log("[Coop] P → StartHostedMatch");
                lobbyManager.StartHostedMatch();
            }
        }

        string ResolveJoinAddress()
        {
            if (CoopParrelSync.IsCloneEditor)
            {
                var shared = CoopParrelSync.ReadSharedMatchAddress();
                if (!string.IsNullOrWhiteSpace(shared))
                    return shared;
            }
            return joinMatchAddress;
        }

        void OnGUI()
        {
            const float pad = 10f;
            var match = lobbyManager != null ? lobbyManager.CurrentMatchAddress : "";
            var shared = CoopParrelSync.ReadSharedMatchAddress();
            var matchLive = erClient != null && erClient.MatchLive;
            GUI.Label(new Rect(pad, pad, 960f, 22f),
                matchLive
                    ? $"{CoopParrelSync.RoleLabel} | LIVE | WASD=ship1 | Arrows=ship2"
                    : $"{CoopParrelSync.RoleLabel} | U=host | I=solo | K=local 2p | O=join | P=start");
            if (!string.IsNullOrEmpty(match))
                GUI.Label(new Rect(pad, pad + 22f, 820f, 22f), $"Match: {match}");
            else if (!string.IsNullOrEmpty(shared))
                GUI.Label(new Rect(pad, pad + 22f, 820f, 22f), $"Shared lobby: {shared}");
        }
    }
}
