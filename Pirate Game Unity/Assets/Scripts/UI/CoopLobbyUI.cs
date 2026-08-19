using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PirateGame.Net;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

namespace PirateGame.UI
{
    /// <summary>
    /// Wallet-gated host/join lobby for WebGL and devnet multiplayer.
    /// Builds its own Canvas at runtime — no prefab wiring required.
    /// </summary>
    public class CoopLobbyUI : MonoBehaviour
    {
        enum Screen
        {
            Wallet,
            Menu,
            Browse,
            Hosting,
            Joining,
            Waiting
        }

        [SerializeField] LobbyManager lobbyManager;
        [SerializeField] ErClient erClient;
        [SerializeField] WalletBootstrap walletBootstrap;
        [SerializeField] BlockchainConfig config;
        [SerializeField] byte defaultTide = 1;

        Canvas canvas;
        GameObject rootPanel;
        TextMeshProUGUI titleText;
        TextMeshProUGUI statusText;
        TextMeshProUGUI walletText;
        TextMeshProUGUI matchAddressText;
        TextMeshProUGUI playerCountText;
        TMP_InputField joinInput;
        Button primaryButton;
        Button secondaryButton;
        Button tertiaryButton;
        Button fourthButton;
        GameObject tideRow;
        TextMeshProUGUI tideLabel;
        byte selectedTide = 1;
        RectTransform lobbyListContent;
        ScrollRect lobbyListScroll;
        GameObject lobbyListPanel;

        Screen screen = Screen.Wallet;
        string statusMessage = "";
        float nextPlayerPollTime;
        bool matchLive;
        bool lobbyListLoading;

        public bool IsVisible => canvas != null && canvas.enabled && !matchLive;

        void Awake()
        {
            if (config == null)
                config = Resources.Load<BlockchainConfig>("BlockchainConfig");
            if (lobbyManager == null)
                lobbyManager = FindFirstObjectByType<LobbyManager>();
            if (erClient == null)
                erClient = FindFirstObjectByType<ErClient>();
            if (walletBootstrap == null)
                walletBootstrap = GetComponent<WalletBootstrap>() ?? gameObject.AddComponent<WalletBootstrap>();

            BuildUi();
            walletBootstrap.OnWalletConnected += OnWalletConnected;
            if (lobbyManager != null)
            {
                lobbyManager.OnStatusChanged += OnLobbyStatus;
                lobbyManager.OnLobbyFailed += OnLobbyFailed;
                lobbyManager.OnJoinedLobby += OnJoinedLobby;
                lobbyManager.OnMatchStarted += OnMatchStarted;
                lobbyManager.OnLobbyListUpdated += OnLobbyListUpdated;
            }
        }

        void OnDestroy()
        {
            if (walletBootstrap != null)
                walletBootstrap.OnWalletConnected -= OnWalletConnected;
            if (lobbyManager != null)
            {
                lobbyManager.OnStatusChanged -= OnLobbyStatus;
                lobbyManager.OnLobbyFailed -= OnLobbyFailed;
                lobbyManager.OnJoinedLobby -= OnJoinedLobby;
                lobbyManager.OnMatchStarted -= OnMatchStarted;
                lobbyManager.OnLobbyListUpdated -= OnLobbyListUpdated;
            }
        }

        async void Start()
        {
            walletBootstrap.ApplyNetworkFromConfig();
            TryApplyJoinAddressFromUrl();
            TryApplyParrelSyncMatch();

            if (!walletBootstrap.RequiresWalletLogin())
            {
                ShowScreen(Screen.Menu);
                UpdateWalletLabel();
                return;
            }

            ShowScreen(Screen.Wallet);
        }

        void TryApplyParrelSyncMatch()
        {
            if (!CoopParrelSync.IsCloneEditor)
                return;

            var shared = CoopParrelSync.ReadSharedMatchAddress();
            if (string.IsNullOrEmpty(shared))
            {
                statusMessage = "Waiting for host to create a room…";
                return;
            }

            if (joinInput != null)
                joinInput.text = shared;
            statusMessage = $"Host room ready — use Join Host Room ({shared[..Math.Min(8, shared.Length)]}…)";
        }

        bool HasParrelSyncHostRoom()
        {
            return CoopParrelSync.IsCloneEditor &&
                   !string.IsNullOrEmpty(CoopParrelSync.ReadSharedMatchAddress());
        }

        void JoinHostRoomFromParrelSync()
        {
            var shared = CoopParrelSync.ReadSharedMatchAddress();
            if (string.IsNullOrEmpty(shared))
            {
                statusMessage = "No host room yet. Ask the host to press Host Room first.";
                if (statusText != null)
                    statusText.text = statusMessage;
                return;
            }

            JoinRoom(shared);
        }

        void Update()
        {
            LobbyUiState.LobbyScreenVisible = canvas != null && canvas.enabled && !matchLive;

            if (matchLive || lobbyManager == null)
                return;

            if (screen == Screen.Hosting && !lobbyManager.IsBusy && Time.unscaledTime >= nextPlayerPollTime)
            {
                nextPlayerPollTime = Time.unscaledTime + 2f;
                _ = PollPlayerCountAsync();
            }
        }

        void OnWalletConnected()
        {
            UpdateWalletLabel();
            if (screen == Screen.Wallet)
                ShowScreen(Screen.Menu);
        }

        void OnLobbyStatus(string message)
        {
            statusMessage = message;
            if (statusText != null)
                statusText.text = message;
            if (!string.IsNullOrEmpty(message) &&
                message.IndexOf("Room ready", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                SetButtonsInteractable(true);
                if (screen == Screen.Hosting)
                    statusMessage = $"{message}\nTap Copy Room Code to share with your co-op partner.";
            }
        }

        void OnLobbyFailed(string message)
        {
            statusMessage = message;
            if (statusText != null)
                statusText.text = message;
            SetButtonsInteractable(true);
        }

        void OnJoinedLobby()
        {
            if (lobbyManager.IsHost)
                ShowScreen(Screen.Hosting);
            else
                ShowScreen(Screen.Waiting);
            UpdateMatchAddress();
        }

        void OnLobbyListUpdated(IReadOnlyList<LobbyInfo> lobbies)
        {
            lobbyListLoading = false;
            RebuildLobbyList(lobbies);
            if (screen == Screen.Browse)
            {
                statusMessage = lobbies.Count == 0
                    ? RpcLobbyListService.LastScanMessage ?? "No open rooms found. Host one or use Paste Address."
                    : $"Found {lobbies.Count} open room(s). Tap to join.";
                if (statusText != null)
                    statusText.text = statusMessage;
            }
            SetButtonsInteractable(true);
        }

        void RebuildLobbyList(IReadOnlyList<LobbyInfo> lobbies)
        {
            if (lobbyListContent == null)
                return;

            for (var i = lobbyListContent.childCount - 1; i >= 0; i--)
                Destroy(lobbyListContent.GetChild(i).gameObject);

            if (lobbies == null || lobbies.Count == 0)
            {
                CreateLobbyListRow("No open rooms", null, false);
                return;
            }

            foreach (var lobby in lobbies)
            {
                var label = $"Tide {lobby.tide}  |  {lobby.playerCount}/{lobby.capacity} players";
                CreateLobbyListRow(label, lobby.matchPubkey, true);
            }
        }

        void CreateLobbyListRow(string label, string matchPubkey, bool clickable)
        {
            var row = new GameObject("LobbyRow");
            row.transform.SetParent(lobbyListContent, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 40;
            le.preferredHeight = 40;

            var bg = row.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, clickable ? 0.1f : 0.05f);

            if (clickable && !string.IsNullOrEmpty(matchPubkey))
            {
                var button = row.AddComponent<Button>();
                button.targetGraphic = bg;
                var pk = matchPubkey;
                button.onClick.AddListener(() => JoinRoom(pk));
            }

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(row.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(12, 0);
            rt.offsetMax = new Vector2(-12, 0);
        }

        void RefreshLobbyList()
        {
            if (lobbyManager == null)
                return;
            lobbyListLoading = true;
            statusMessage = "Scanning open rooms on-chain…";
            if (statusText != null)
                statusText.text = statusMessage;
            SetButtonsInteractable(false);
            lobbyManager.RefreshLobbyList();
        }

        void OnMatchStarted()
        {
            matchLive = true;
            if (canvas != null)
                canvas.enabled = false;
        }

        void TryApplyJoinAddressFromUrl()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            var url = Application.absoluteURL;
            if (string.IsNullOrEmpty(url))
                return;
            var match = Regex.Match(url, @"[?&]match=([1-9A-HJ-NP-Za-km-z]{32,44})", RegexOptions.IgnoreCase);
            if (!match.Success)
                return;
            var address = match.Groups[1].Value;
            if (joinInput != null)
                joinInput.text = address;
            statusMessage = $"Invite link detected for {address[..Math.Min(6, address.Length)]}…";
#endif
        }

        async void OnPrimaryClicked()
        {
            switch (screen)
            {
                case Screen.Wallet:
                    SetButtonsInteractable(false);
                    statusMessage = "Connecting wallet…";
                    statusText.text = statusMessage;
                    var ok = await walletBootstrap.ConnectWalletAsync();
                    SetButtonsInteractable(true);
                    if (!ok)
                    {
                        statusMessage = "Wallet connection cancelled or failed.";
                        statusText.text = statusMessage;
                    }
                    break;
                case Screen.Menu:
                    HostRoom();
                    break;
                case Screen.Browse:
                    RefreshLobbyList();
                    break;
                case Screen.Hosting:
                    StartHostedMatch();
                    break;
                case Screen.Joining:
                    JoinRoom();
                    break;
            }
        }

        void OnSecondaryClicked()
        {
            switch (screen)
            {
                case Screen.Menu:
                    if (!CoopParrelSync.IsCloneEditor)
                        ShowScreen(Screen.Browse);
                    RefreshLobbyList();
                    break;
                case Screen.Hosting:
                    CopyRoomCode();
                    break;
                case Screen.Joining:
                    CopyRoomCode();
                    break;
            }
        }

        void OnTertiaryClicked()
        {
            switch (screen)
            {
                case Screen.Menu:
                    if (HasParrelSyncHostRoom())
                        JoinHostRoomFromParrelSync();
                    else
                        ShowScreen(Screen.Joining);
                    break;
                case Screen.Hosting:
                    CopyShareLink();
                    break;
                case Screen.Browse:
                case Screen.Joining:
                case Screen.Waiting:
                    ShowScreen(Screen.Menu);
                    break;
            }
        }

        void OnFourthClicked()
        {
            switch (screen)
            {
                case Screen.Menu:
                    if (walletBootstrap.IsConnected)
                        walletBootstrap.Disconnect();
                    ShowScreen(Screen.Wallet);
                    break;
                case Screen.Hosting:
                    ShowScreen(Screen.Menu);
                    break;
            }
        }

        void HostRoom()
        {
            if (lobbyManager == null) return;
            var tide = ReadTide();
            SetButtonsInteractable(false);
            statusMessage = "Creating room…";
            statusText.text = statusMessage;
            lobbyManager.CreateLobby(tide);
            ShowScreen(Screen.Hosting);
        }

        void JoinRoom(string address = null)
        {
            if (lobbyManager == null) return;
            address = string.IsNullOrWhiteSpace(address)
                ? (joinInput != null ? joinInput.text.Trim() : "")
                : address.Trim();
            address = ExtractMatchAddress(address);
            if (string.IsNullOrEmpty(address))
            {
                statusMessage = "Paste the host's room address.";
                statusText.text = statusMessage;
                return;
            }

            if (joinInput != null)
                joinInput.text = address;

            SetButtonsInteractable(false);
            statusMessage = "Joining room…";
            statusText.text = statusMessage;
            lobbyManager.JoinMatch(address);
            ShowScreen(Screen.Waiting);
        }

        void StartHostedMatch()
        {
            if (lobbyManager == null) return;
            SetButtonsInteractable(false);
            statusMessage = "Starting match…";
            statusText.text = statusMessage;
            lobbyManager.StartHostedMatch();
        }

        void CopyRoomCode()
        {
            var address = lobbyManager != null ? lobbyManager.CurrentMatchAddress : "";
            if (string.IsNullOrEmpty(address))
                address = joinInput != null ? joinInput.text.Trim() : "";
            address = ExtractMatchAddress(address);
            if (string.IsNullOrEmpty(address))
            {
                statusMessage = "No room code to copy yet.";
                statusText.text = statusMessage;
                return;
            }

            GUIUtility.systemCopyBuffer = address;
            statusMessage = "Room code copied to clipboard.";
            statusText.text = statusMessage;
        }

        void CopyShareLink()
        {
            var address = lobbyManager != null ? lobbyManager.CurrentMatchAddress : "";
            if (string.IsNullOrEmpty(address))
            {
                statusMessage = "No room to share yet.";
                statusText.text = statusMessage;
                return;
            }

            var url = LobbyShareLinks.BuildMatchUrl(config, address);
            GUIUtility.systemCopyBuffer = url;
            statusMessage = string.IsNullOrEmpty(LobbyShareLinks.ResolveBaseUrl(config))
                ? "Share path copied (?match=…). Set BlockchainConfig.webLobbyBaseUrl for a full URL."
                : "Invite link copied to clipboard.";
            statusText.text = statusMessage;
        }

        static string ExtractMatchAddress(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "";
            input = input.Trim();
            var match = Regex.Match(input, @"[?&]match=([1-9A-HJ-NP-Za-km-z]{32,44})", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : input;
        }

        byte ReadTide() => selectedTide;

        void CycleTide()
        {
            selectedTide = selectedTide >= 3 ? (byte)1 : (byte)(selectedTide + 1);
            UpdateTideLabel();
        }

        void UpdateTideLabel()
        {
            if (tideLabel != null)
                tideLabel.text = $"Tide: {selectedTide}";
        }

        void SetTideRowVisible(bool visible)
        {
            if (tideRow != null)
                tideRow.SetActive(visible);
        }

        async Task PollPlayerCountAsync()
        {
            if (lobbyManager == null || !lobbyManager.HasActiveMatch)
                return;
            var count = await lobbyManager.GetLobbyPlayerCountAsync();
            if (playerCountText != null)
                playerCountText.text = $"Players: {count} / {config.defaultLobbyCapacity}";
        }

        void UpdateWalletLabel()
        {
            if (walletText == null) return;
            var network = config != null ? config.mode.ToString() : "Devnet";
            if (walletBootstrap.IsConnected)
            {
                var addr = walletBootstrap.WalletAddress;
                var shortAddr = addr.Length > 12 ? $"{addr[..4]}…{addr[^4..]}" : addr;
                walletText.text = $"{network} | {shortAddr}";
            }
            else if (!walletBootstrap.RequiresWalletLogin())
                walletText.text = $"{network} | local keypair";
            else
                walletText.text = $"{network} | wallet not connected";
        }

        void UpdateMatchAddress()
        {
            if (matchAddressText == null || lobbyManager == null) return;
            var address = lobbyManager.CurrentMatchAddress;
            matchAddressText.text = string.IsNullOrEmpty(address)
                ? "Waiting for room address…"
                : address;
            if (joinInput != null && !string.IsNullOrEmpty(address))
                joinInput.text = address;
        }

        void SetButtonsInteractable(bool interactable)
        {
            if (primaryButton != null) primaryButton.interactable = interactable && !lobbyListLoading;
            if (secondaryButton != null) secondaryButton.interactable = interactable;
            if (tertiaryButton != null) tertiaryButton.interactable = interactable;
            if (fourthButton != null) fourthButton.interactable = interactable;
        }

        void SetLobbyListVisible(bool visible)
        {
            if (lobbyListPanel != null)
                lobbyListPanel.SetActive(visible);
        }

        void ShowScreen(Screen next)
        {
            screen = next;
            SetButtonsInteractable(true);
            UpdateWalletLabel();
            UpdateMatchAddress();
            SetLobbyListVisible(screen == Screen.Browse);

            switch (screen)
            {
                case Screen.Wallet:
                    titleText.text = "Pirate Co-op";
                    statusText.text = "Connect your Solana wallet to host or join a room on devnet.";
                    primaryButton.gameObject.SetActive(true);
                    SetButtonLabel(primaryButton, "Connect Wallet");
                    secondaryButton.gameObject.SetActive(false);
                    tertiaryButton.gameObject.SetActive(false);
                    fourthButton.gameObject.SetActive(false);
                    SetTideRowVisible(false);
                    joinInput.gameObject.SetActive(false);
                    matchAddressText.gameObject.SetActive(false);
                    playerCountText.gameObject.SetActive(false);
                    break;

                case Screen.Menu:
                    titleText.text = CoopParrelSync.IsCloneEditor ? "Join Host (ParrelSync)" : "Multiplayer Lobby";
                    if (CoopParrelSync.IsCloneEditor && string.IsNullOrEmpty(statusMessage))
                        statusMessage = HasParrelSyncHostRoom()
                            ? "Host room found — tap Join Host Room."
                            : "Waiting for host to create a room…";
                    statusText.text = string.IsNullOrEmpty(statusMessage)
                        ? "Host a room, browse open lobbies, or paste an invite address."
                        : statusMessage;
                    primaryButton.gameObject.SetActive(!CoopParrelSync.IsCloneEditor);
                    SetButtonLabel(primaryButton, "Host Room");
                    secondaryButton.gameObject.SetActive(!CoopParrelSync.IsCloneEditor);
                    SetButtonLabel(secondaryButton, "Browse Rooms");
                    tertiaryButton.gameObject.SetActive(true);
                    if (HasParrelSyncHostRoom())
                    {
                        SetButtonLabel(tertiaryButton, "Join Host Room");
                    }
                    else
                    {
                        SetButtonLabel(tertiaryButton, CoopParrelSync.IsCloneEditor ? "Paste Address" : "Paste Address");
                    }
                    fourthButton.gameObject.SetActive(!CoopParrelSync.IsCloneEditor);
                    SetButtonLabel(fourthButton, "Disconnect");
                    SetTideRowVisible(true);
                    UpdateTideLabel();
                    joinInput.gameObject.SetActive(false);
                    matchAddressText.gameObject.SetActive(false);
                    playerCountText.gameObject.SetActive(false);
                    break;

                case Screen.Browse:
                    titleText.text = "Open Rooms";
                    statusText.text = lobbyListLoading
                        ? "Scanning open rooms on-chain…"
                        : statusMessage;
                    primaryButton.gameObject.SetActive(true);
                    SetButtonLabel(primaryButton, "Refresh");
                    secondaryButton.gameObject.SetActive(false);
                    tertiaryButton.gameObject.SetActive(true);
                    SetButtonLabel(tertiaryButton, "Back");
                    fourthButton.gameObject.SetActive(false);
                    SetTideRowVisible(false);
                    joinInput.gameObject.SetActive(false);
                    matchAddressText.gameObject.SetActive(false);
                    playerCountText.gameObject.SetActive(false);
                    break;

                case Screen.Hosting:
                    titleText.text = "Hosting Room";
                    statusText.text = statusMessage;
                    primaryButton.gameObject.SetActive(true);
                    SetButtonLabel(primaryButton, "Start Match");
                    secondaryButton.gameObject.SetActive(true);
                    SetButtonLabel(secondaryButton, "Copy Room Code");
                    tertiaryButton.gameObject.SetActive(true);
                    SetButtonLabel(tertiaryButton, "Share Link");
                    fourthButton.gameObject.SetActive(true);
                    SetButtonLabel(fourthButton, "Back");
                    SetTideRowVisible(false);
                    joinInput.gameObject.SetActive(false);
                    matchAddressText.gameObject.SetActive(true);
                    playerCountText.gameObject.SetActive(true);
                    _ = PollPlayerCountAsync();
                    break;

                case Screen.Joining:
                    titleText.text = "Join Room";
                    statusText.text = "Paste the host's room address or invite link path.";
                    primaryButton.gameObject.SetActive(true);
                    SetButtonLabel(primaryButton, "Join");
                    secondaryButton.gameObject.SetActive(false);
                    tertiaryButton.gameObject.SetActive(true);
                    SetButtonLabel(tertiaryButton, "Back");
                    fourthButton.gameObject.SetActive(false);
                    SetTideRowVisible(false);
                    joinInput.gameObject.SetActive(true);
                    matchAddressText.gameObject.SetActive(false);
                    playerCountText.gameObject.SetActive(false);
                    break;

                case Screen.Waiting:
                    titleText.text = "Waiting for Host";
                    statusText.text = statusMessage;
                    primaryButton.gameObject.SetActive(false);
                    secondaryButton.gameObject.SetActive(false);
                    tertiaryButton.gameObject.SetActive(false);
                    fourthButton.gameObject.SetActive(false);
                    SetTideRowVisible(false);
                    joinInput.gameObject.SetActive(false);
                    matchAddressText.gameObject.SetActive(true);
                    playerCountText.gameObject.SetActive(false);
                    break;
            }
        }

        void BuildUi()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
                es.AddComponent<InputSystemUIInputModule>();
#else
                es.AddComponent<StandaloneInputModule>();
#endif
            }

            var canvasGo = new GameObject("CoopLobbyCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            canvasGo.AddComponent<GraphicRaycaster>();

            rootPanel = CreatePanel(canvasGo.transform, new Color(0.05f, 0.08f, 0.14f, 0.92f),
                new Vector2(560, 500), Vector2.zero);

            var layout = rootPanel.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 24, 24);
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            titleText = CreateLabel(rootPanel.transform, "Pirate Co-op", 28, FontStyles.Bold, 36);
            walletText = CreateLabel(rootPanel.transform, "Devnet", 14, FontStyles.Normal, 22);
            statusText = CreateLabel(rootPanel.transform, "", 16, FontStyles.Normal, 56);
            statusText.alignment = TextAlignmentOptions.TopLeft;

            lobbyListPanel = CreateLobbyListScroll(rootPanel.transform);
            lobbyListPanel.SetActive(false);

            tideRow = CreateTideSelector(rootPanel.transform);
            selectedTide = defaultTide;
            UpdateTideLabel();
            joinInput = CreateInput(rootPanel.transform, "Paste room address…");
            matchAddressText = CreateLabel(rootPanel.transform, "", 14, FontStyles.Normal, 48);
            matchAddressText.alignment = TextAlignmentOptions.Center;
            matchAddressText.enableWordWrapping = true;
            playerCountText = CreateLabel(rootPanel.transform, "Players: 0", 16, FontStyles.Bold, 24);

            var buttonRow = new GameObject("Buttons");
            buttonRow.transform.SetParent(rootPanel.transform, false);
            var buttonLayout = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonLayout.spacing = 12;
            buttonLayout.childAlignment = TextAnchor.MiddleCenter;
            buttonLayout.childControlWidth = true;
            buttonLayout.childForceExpandWidth = true;
            var buttonLE = buttonRow.AddComponent<LayoutElement>();
            buttonLE.minHeight = 48;
            buttonLE.preferredHeight = 48;

            primaryButton = CreateButton(buttonRow.transform, "Connect Wallet", OnPrimaryClicked);
            secondaryButton = CreateButton(buttonRow.transform, "Browse Rooms", OnSecondaryClicked);
            tertiaryButton = CreateButton(buttonRow.transform, "Back", OnTertiaryClicked);
            fourthButton = CreateButton(buttonRow.transform, "Disconnect", OnFourthClicked);

            ShowScreen(Screen.Wallet);
        }

        GameObject CreateLobbyListScroll(Transform parent)
        {
            var panel = new GameObject("LobbyListPanel");
            panel.transform.SetParent(parent, false);
            var panelLe = panel.AddComponent<LayoutElement>();
            panelLe.minHeight = 180;
            panelLe.preferredHeight = 180;

            var bg = panel.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.06f);

            var scrollGo = new GameObject("Scroll");
            scrollGo.transform.SetParent(panel.transform, false);
            var scrollRt = scrollGo.AddComponent<RectTransform>();
            scrollRt.anchorMin = Vector2.zero;
            scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(8, 8);
            scrollRt.offsetMax = new Vector2(-8, -8);

            lobbyListScroll = scrollGo.AddComponent<ScrollRect>();
            lobbyListScroll.horizontal = false;
            lobbyListScroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollGo.transform, false);
            var viewportRt = viewport.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = Vector2.zero;
            viewportRt.offsetMax = Vector2.zero;
            viewport.AddComponent<RectMask2D>();
            var viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0f, 0f, 0f, 0.01f);

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            lobbyListContent = content.AddComponent<RectTransform>();
            lobbyListContent.anchorMin = new Vector2(0f, 1f);
            lobbyListContent.anchorMax = new Vector2(1f, 1f);
            lobbyListContent.pivot = new Vector2(0.5f, 1f);
            lobbyListContent.offsetMin = Vector2.zero;
            lobbyListContent.offsetMax = Vector2.zero;

            var contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 6;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            lobbyListScroll.viewport = viewportRt;
            lobbyListScroll.content = lobbyListContent;
            return panel;
        }

        static GameObject CreatePanel(Transform parent, Color color, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject("Panel");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
            var image = go.AddComponent<Image>();
            image.color = color;
            return go;
        }

        static TextMeshProUGUI CreateLabel(Transform parent, string text, int fontSize, FontStyles style, float height)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = style;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            return label;
        }

        static TMP_InputField CreateInput(Transform parent, string placeholder)
        {
            var go = new GameObject("Input");
            go.transform.SetParent(parent, false);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 40;
            le.preferredHeight = 40;

            var bg = go.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.12f);

            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(go.transform, false);
            var textRt = textArea.AddComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = new Vector2(10, 6);
            textRt.offsetMax = new Vector2(-10, -6);

            var text = new GameObject("Text");
            text.transform.SetParent(textArea.transform, false);
            var textComp = text.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 16;
            textComp.color = Color.white;
            var textRect = text.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textArea.transform, false);
            var placeholderText = placeholderGo.AddComponent<TextMeshProUGUI>();
            placeholderText.text = placeholder;
            placeholderText.fontSize = 16;
            placeholderText.color = new Color(1f, 1f, 1f, 0.35f);
            var phRect = placeholderGo.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = Vector2.zero;
            phRect.offsetMax = Vector2.zero;

            var input = go.AddComponent<TMP_InputField>();
            input.textComponent = textComp;
            input.placeholder = placeholderText;
            input.lineType = TMP_InputField.LineType.SingleLine;
            go.SetActive(false);
            return input;
        }

        GameObject CreateTideSelector(Transform parent)
        {
            var row = new GameObject("TideRow");
            row.transform.SetParent(parent, false);
            var le = row.AddComponent<LayoutElement>();
            le.minHeight = 36;
            le.preferredHeight = 36;

            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            tideLabel = CreateLabel(row.transform, "Tide: 1", 16, FontStyles.Normal, 36);
            tideLabel.alignment = TextAlignmentOptions.MidlineLeft;
            var labelLe = tideLabel.GetComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;

            CreateButton(row.transform, "Change Tide", CycleTide);
            row.SetActive(false);
            return row;
        }

        static Button CreateButton(Transform parent, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label);
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = new Color(0.18f, 0.45f, 0.78f, 1f);
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 16;
            text.fontStyle = FontStyles.Bold;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.Center;
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return button;
        }

        static void SetButtonLabel(Button button, string label)
        {
            if (button == null) return;
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = label;
        }
    }
}
