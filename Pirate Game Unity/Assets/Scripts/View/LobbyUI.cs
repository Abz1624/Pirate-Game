using UnityEngine;
using UnityEngine.UI;

namespace PirateGame.View
{
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] Net.LobbyManager lobbyManager;
        [SerializeField] Transform lobbyListRoot;
        [SerializeField] GameObject lobbyRowPrefab;
        [SerializeField] Button createButton;
        [SerializeField] Button refreshButton;
        [SerializeField] Dropdown tideDropdown;
        [SerializeField] GameObject rosterPanel;
        [SerializeField] Button readyButton;
        [SerializeField] Button startButton;

        void Start()
        {
            if (lobbyManager == null)
                lobbyManager = FindFirstObjectByType<Net.LobbyManager>();

            lobbyManager.OnLobbyListUpdated += RefreshList;
            createButton?.onClick.AddListener(OnCreate);
            refreshButton?.onClick.AddListener(() => lobbyManager.RefreshLobbyList());
            readyButton?.onClick.AddListener(() => lobbyManager.SetReady(true));
            startButton?.onClick.AddListener(() => lobbyManager.StartMatch());
            lobbyManager.RefreshLobbyList();
        }

        void OnCreate()
        {
            var tide = (byte)(tideDropdown != null ? tideDropdown.value + 1 : 1);
            lobbyManager.CreateMatch(tide);
        }

        void RefreshList(System.Collections.Generic.IReadOnlyList<Net.LobbyInfo> lobbies)
        {
            if (lobbyListRoot == null || lobbyRowPrefab == null) return;
            foreach (Transform child in lobbyListRoot)
                Destroy(child.gameObject);

            foreach (var lobby in lobbies)
            {
                var row = Instantiate(lobbyRowPrefab, lobbyListRoot);
                var label = row.GetComponentInChildren<Text>();
                if (label)
                    label.text = $"Tide {lobby.tide} — {lobby.playerCount}/{lobby.capacity}";
                var btn = row.GetComponent<Button>();
                if (btn)
                {
                    var pk = lobby.matchPubkey;
                    btn.onClick.AddListener(() => lobbyManager.JoinMatch(pk));
                }
            }
        }
    }
}
