using HighSeas.Combat;
using HighSeas.Core;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Rogue Pirates v3 main menu with searchable placeholder UI widgets.</summary>
    public sealed class V3MainMenuController : MonoBehaviour
    {
        [SerializeField] Canvas menuCanvas;
        [SerializeField] Button playButton;
        [SerializeField] Button quitButton;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI versionText;
        [SerializeField] TextMeshProUGUI captainLabel;
        [SerializeField] Button rookButton;
        [SerializeField] Button brineButton;
        [SerializeField] Button flintButton;
        [SerializeField] TextMeshProUGUI captainDetailText;
        [SerializeField] TextMeshProUGUI combatProfileLabel;
        [SerializeField] TextMeshProUGUI combatProfileDetailText;
        [SerializeField] Button profileAButton;
        [SerializeField] Button profileBButton;
        [SerializeField] Button profileCButton;
        [SerializeField] Button profileDButton;
        [SerializeField] Toggle aimVisualsToggle;
        [SerializeField] Toggle captainActivesToggle;
        [SerializeField] RectTransform collectionPanel;
        V3MainMenuCollectionView _collectionView;
        [SerializeField] Button weaponsTabButton;
        [SerializeField] Button logbooksTabButton;
        [SerializeField] Button itemsTabButton;
        [SerializeField] Button questsTabButton;

        static readonly Color ProfileActiveColor = new(0.35f, 0.72f, 1f, 1f);
        static readonly Color ProfileInactiveColor = new(0.85f, 0.65f, 0.2f, 1f);

        V3CaptainId _selectedCaptain = V3CaptainId.Rook;
        string _selectedProfileId = "D_FULL_COMBAT_V2";
        string _activeCollectionTab = "weapons";
        readonly Dictionary<V3CaptainId, Button> _captainButtons = new();

        void Awake()
        {
            _selectedProfileId = CombatExperimentContext.SelectedProfileId;
            EnsureMenuCamera();
            if (menuCanvas == null)
                BuildPlaceholderMenu();
        }

        static void EnsureMenuCamera()
        {
            if (Camera.main != null) return;
            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.1f, 0.16f, 1f);
            cameraGo.AddComponent<AudioListener>();
            cameraGo.transform.position = new Vector3(0f, 12f, -18f);
            cameraGo.transform.rotation = Quaternion.Euler(24f, 0f, 0f);
        }

        void Start()
        {
            AppRoot.EnsureInitialized();
            _selectedCaptain = V3CaptainSelection.Selected;
            _selectedProfileId = CombatExperimentContext.SelectedProfileId;
            CombatExperimentContext.SetActiveProfileId(_selectedProfileId);
            if (titleText != null)
                titleText.text = "Rogue Pirates";
            if (versionText != null)
                versionText.text = $"v3 prototype · {Application.version}";
            if (playButton != null)
                playButton.onClick.AddListener(OnPlay);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuit);
            WireCaptainButtons();
            profileAButton?.onClick.AddListener(() => SelectCombatProfile("A_CURRENT"));
            profileBButton?.onClick.AddListener(() => SelectCombatProfile("B_SIX_ONLY"));
            profileCButton?.onClick.AddListener(() => SelectCombatProfile("C_SIX_DIRECTIONAL"));
            profileDButton?.onClick.AddListener(() => SelectCombatProfile("D_FULL_COMBAT_V2"));
            if (aimVisualsToggle != null)
            {
                aimVisualsToggle.isOn = CombatExperimentContext.ShowAimVisuals;
                aimVisualsToggle.onValueChanged.AddListener(OnAimVisualsToggled);
            }
            if (captainActivesToggle != null)
            {
                captainActivesToggle.isOn = CombatExperimentContext.CaptainActivesEnabled;
                captainActivesToggle.onValueChanged.AddListener(OnCaptainActivesToggled);
            }
            weaponsTabButton?.onClick.AddListener(() => SelectCollectionTab("weapons"));
            logbooksTabButton?.onClick.AddListener(() => SelectCollectionTab("logbooks"));
            itemsTabButton?.onClick.AddListener(() => SelectCollectionTab("items"));
            questsTabButton?.onClick.AddListener(() => SelectCollectionTab("quests"));
            RefreshCaptainUi();
            RefreshCombatProfileUi();
            RefreshCollectionUi();
        }

        void SelectCombatProfile(string profileId)
        {
            _selectedProfileId = profileId;
            CombatExperimentContext.SetActiveProfileId(profileId);
            RefreshCombatProfileUi();
            RefreshCollectionUi();
        }

        void RefreshCombatProfileUi()
        {
            var profile = CombatExperimentContext.Active;
            if (combatProfileLabel != null)
                combatProfileLabel.text = $"Combat profile: {CombatExperimentContext.GetDisplayName(_selectedProfileId)}";
            if (combatProfileDetailText != null)
            {
                combatProfileDetailText.text =
                    $"{CombatExperimentContext.GetDescription(_selectedProfileId)}\n" +
                    $"Weapon slots: {profile.weaponSlotCap} · Directional: {(profile.directionalWeaponTargetingEnabled ? "On" : "Off")} · Director V2: {(profile.directorV2Enabled ? "On" : "Off")}";
            }

            StyleProfileButton(profileAButton, _selectedProfileId == "A_CURRENT");
            StyleProfileButton(profileBButton, _selectedProfileId == "B_SIX_ONLY");
            StyleProfileButton(profileCButton, _selectedProfileId == "C_SIX_DIRECTIONAL");
            StyleProfileButton(profileDButton, _selectedProfileId == "D_FULL_COMBAT_V2");
        }

        static void StyleProfileButton(Button button, bool selected)
        {
            if (button == null) return;
            var colors = button.colors;
            var baseColor = selected ? ProfileActiveColor : ProfileInactiveColor;
            colors.normalColor = baseColor;
            colors.highlightedColor = baseColor * 1.08f;
            colors.pressedColor = baseColor * 0.88f;
            colors.selectedColor = baseColor;
            button.colors = colors;
        }

        void OnAimVisualsToggled(bool enabled) =>
            CombatExperimentContext.ShowAimVisuals = enabled;

        void OnCaptainActivesToggled(bool enabled) =>
            CombatExperimentContext.CaptainActivesEnabled = enabled;

        void SelectCollectionTab(string tab)
        {
            _activeCollectionTab = tab;
            RefreshCollectionUi();
        }

        void SelectCaptain(V3CaptainId id)
        {
            if (V3CaptainCatalog.IsLocked(id)) return;
            _selectedCaptain = id;
            V3CaptainSelection.Selected = id;
            RefreshCaptainUi();
            RefreshCollectionUi();
        }

        void WireCaptainButtons()
        {
            RegisterCaptainButton(V3CaptainId.Rook, rookButton);
            RegisterCaptainButton(V3CaptainId.Brine, brineButton);
            RegisterCaptainButton(V3CaptainId.Flint, flintButton);

            if (menuCanvas != null)
            {
                foreach (var id in V3CaptainCatalog.MenuOrder)
                {
                    if (_captainButtons.ContainsKey(id)) continue;
                    var label = V3CaptainCatalog.GetMenuLabel(id);
                    var button = V3PlaceholderFactory.CreateUiButton(
                        menuCanvas.transform,
                        $"main_menu_captain_{id.ToString().ToLowerInvariant()}",
                        label,
                        new Vector2(96f, 44f),
                        V3CaptainCatalog.GetPassiveSummary(id));
                    _captainButtons[id] = button;
                }

                LayoutCaptainButtons();
            }

            foreach (var id in V3CaptainCatalog.MenuOrder)
            {
                if (!_captainButtons.TryGetValue(id, out var button) || button == null) continue;
                button.onClick.RemoveAllListeners();
                var captainId = id;
                button.onClick.AddListener(() => SelectCaptain(captainId));
            }
        }

        void RegisterCaptainButton(V3CaptainId id, Button button)
        {
            if (button == null) return;
            _captainButtons[id] = button;
        }

        void LayoutCaptainButtons()
        {
            var bar = menuCanvas.transform.Find("PLACEHOLDER_v3_ui_main_menu_captain_bar");
            Transform barTransform;
            if (bar == null)
            {
                var barGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_captain_bar", typeof(RectTransform));
                barGo.transform.SetParent(menuCanvas.transform, false);
                var barRect = barGo.GetComponent<RectTransform>();
                barRect.anchorMin = new Vector2(0.5f, 0.5f);
                barRect.anchorMax = new Vector2(0.5f, 0.5f);
                barRect.anchoredPosition = new Vector2(0f, 48f);
                barRect.sizeDelta = new Vector2(680f, 48f);
                var layout = barGo.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 6f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = true;
                barTransform = barGo.transform;
            }
            else
            {
                barTransform = bar;
            }

            foreach (var id in V3CaptainCatalog.MenuOrder)
            {
                if (!_captainButtons.TryGetValue(id, out var button) || button == null) continue;
                var rect = button.GetComponent<RectTransform>();
                rect.SetParent(barTransform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(96f, 44f);
                var layoutElement = button.gameObject.GetComponent<LayoutElement>();
                if (layoutElement == null)
                    layoutElement = button.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 96f;
                layoutElement.preferredHeight = 44f;
            }
        }

        void RefreshCaptainUi()
        {
            var profile = V3CaptainProfile.Get(_selectedCaptain);
            if (captainLabel != null)
                captainLabel.text = profile.DisplayName;
            if (captainDetailText == null) return;

            var locked = V3CaptainCatalog.IsLocked(_selectedCaptain);
            var activesNote = CombatExperimentContext.CaptainActivesEnabled
                ? "Captain actives enabled (Q in run)."
                : "Captain actives off — enable in menu to test Q.";
            captainDetailText.text =
                $"{profile.DisplayName}\n" +
                $"Starter: {profile.StartingWeaponId.Replace('_', ' ')}\n" +
                $"Hull {profile.BaseHull:0} · Speed {profile.BaseMoveSpeed:0.1}\n\n" +
                $"Passive: {V3CaptainCatalog.GetPassiveSummary(_selectedCaptain)}\n" +
                $"Active: {V3CaptainCatalog.GetActiveSummary(_selectedCaptain)}\n\n" +
                activesNote + "\n" +
                (locked ? "LOCKED — complete first Saltwind conquest." : "READY");

            foreach (var id in V3CaptainCatalog.MenuOrder)
            {
                if (!_captainButtons.TryGetValue(id, out var button) || button == null) continue;
                var selected = id == _selectedCaptain;
                var isLocked = V3CaptainCatalog.IsLocked(id);
                StyleCaptainButton(button, selected, isLocked);
            }
        }

        static void StyleCaptainButton(Button button, bool selected, bool locked)
        {
            var colors = button.colors;
            Color baseColor;
            if (locked)
                baseColor = new Color(0.35f, 0.35f, 0.38f, 0.85f);
            else if (selected)
                baseColor = ProfileActiveColor;
            else
                baseColor = ProfileInactiveColor;
            colors.normalColor = baseColor;
            colors.highlightedColor = baseColor * 1.08f;
            colors.pressedColor = baseColor * 0.88f;
            colors.selectedColor = baseColor;
            colors.disabledColor = baseColor * 0.7f;
            button.colors = colors;
            button.interactable = !locked;
        }

        void RefreshCollectionUi()
        {
            EnsureCollectionView();
            if (_collectionView == null) return;

            var entries = _activeCollectionTab switch
            {
                "logbooks" => V3MainMenuCollectionData.GetLogbooks(),
                "items" => V3MainMenuCollectionData.GetItems(),
                "quests" => V3MainMenuCollectionData.GetQuests(),
                _ => V3MainMenuCollectionData.GetWeapons(_selectedCaptain, _selectedProfileId)
            };

            var title = _activeCollectionTab switch
            {
                "logbooks" => "LOGBOOKS",
                "items" => "ITEMS",
                "quests" => "QUESTS",
                _ => CombatExperimentContext.Active.weaponSlotCap >= 6 ? "WEAPONS · 12-POOL" : "WEAPONS"
            };
            _collectionView.SetEntries($"{title} — collection", entries);
        }

        void EnsureCollectionView()
        {
            if (_collectionView != null) return;
            if (collectionPanel == null) return;
            _collectionView = V3MainMenuCollectionView.Ensure(collectionPanel);
        }

        void OnPlay()
        {
            CombatExperimentContext.SetActiveProfileId(_selectedProfileId);
            AppRoot.Instance?.LoadScene(V3SceneNames.SaltwindRun);
        }

        static void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void BuildPlaceholderMenu()
        {
            var es = FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_canvas");
            menuCanvas = canvasGo.AddComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            canvasGo.AddComponent<PlaceholderMarker>().Configure("ui", "main_menu_canvas",
                "Main menu canvas — replace with production menu layout and art.");

            var bg = V3PlaceholderFactory.CreateUiPanel(menuCanvas.transform, "main_menu_background",
                new Vector2(1920f, 1080f), "Menu backdrop — replace with Saltwind port illustration.");
            var bgRect = bg.GetComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bg.GetComponent<Image>().color = new Color(0.04f, 0.1f, 0.16f, 1f);

            var titleGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_title");
            titleGo.transform.SetParent(menuCanvas.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = new Vector2(0f, 180f);
            titleRect.sizeDelta = new Vector2(800f, 100f);
            titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.fontSize = 64;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = V3PlaceholderFactory.UiAccent;
            titleGo.AddComponent<PlaceholderMarker>().Configure("ui", "main_menu_title", "Game title treatment.");

            var captainGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_captain_label");
            captainGo.transform.SetParent(menuCanvas.transform, false);
            var captainRect = captainGo.AddComponent<RectTransform>();
            captainRect.anchorMin = new Vector2(0.5f, 0.5f);
            captainRect.anchorMax = new Vector2(0.5f, 0.5f);
            captainRect.anchoredPosition = new Vector2(0f, 100f);
            captainRect.sizeDelta = new Vector2(520f, 40f);
            captainLabel = captainGo.AddComponent<TextMeshProUGUI>();
            captainLabel.fontSize = 28;
            captainLabel.alignment = TextAlignmentOptions.Center;
            captainLabel.color = V3PlaceholderFactory.UiAccent;

            var detailGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_captain_detail");
            detailGo.transform.SetParent(menuCanvas.transform, false);
            var detailRect = detailGo.AddComponent<RectTransform>();
            detailRect.anchorMin = new Vector2(0f, 0.5f);
            detailRect.anchorMax = new Vector2(0f, 0.5f);
            detailRect.pivot = new Vector2(0f, 0.5f);
            detailRect.anchoredPosition = new Vector2(32f, 20f);
            detailRect.sizeDelta = new Vector2(460f, 260f);
            captainDetailText = detailGo.AddComponent<TextMeshProUGUI>();
            captainDetailText.fontSize = 20;
            captainDetailText.alignment = TextAlignmentOptions.TopLeft;
            captainDetailText.color = V3PlaceholderFactory.UiText;

            var profileLabelGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_profile_label");
            profileLabelGo.transform.SetParent(menuCanvas.transform, false);
            var profileLabelRect = profileLabelGo.AddComponent<RectTransform>();
            profileLabelRect.anchorMin = new Vector2(0f, 0.5f);
            profileLabelRect.anchorMax = new Vector2(0f, 0.5f);
            profileLabelRect.pivot = new Vector2(0f, 0.5f);
            profileLabelRect.anchoredPosition = new Vector2(32f, -120f);
            profileLabelRect.sizeDelta = new Vector2(520f, 32f);
            combatProfileLabel = profileLabelGo.AddComponent<TextMeshProUGUI>();
            combatProfileLabel.fontSize = 22;
            combatProfileLabel.fontStyle = FontStyles.Bold;
            combatProfileLabel.alignment = TextAlignmentOptions.TopLeft;
            combatProfileLabel.color = V3PlaceholderFactory.UiAccent;

            var profileDetailGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_profile_detail");
            profileDetailGo.transform.SetParent(menuCanvas.transform, false);
            var profileDetailRect = profileDetailGo.AddComponent<RectTransform>();
            profileDetailRect.anchorMin = new Vector2(0f, 0.5f);
            profileDetailRect.anchorMax = new Vector2(0f, 0.5f);
            profileDetailRect.pivot = new Vector2(0f, 0.5f);
            profileDetailRect.anchoredPosition = new Vector2(32f, -210f);
            profileDetailRect.sizeDelta = new Vector2(520f, 72f);
            combatProfileDetailText = profileDetailGo.AddComponent<TextMeshProUGUI>();
            combatProfileDetailText.fontSize = 17;
            combatProfileDetailText.alignment = TextAlignmentOptions.TopLeft;
            combatProfileDetailText.color = V3PlaceholderFactory.UiText;

            var profileBarGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_profile_bar");
            profileBarGo.transform.SetParent(menuCanvas.transform, false);
            var profileBarRect = profileBarGo.AddComponent<RectTransform>();
            profileBarRect.anchorMin = new Vector2(0f, 0.5f);
            profileBarRect.anchorMax = new Vector2(0f, 0.5f);
            profileBarRect.pivot = new Vector2(0f, 0.5f);
            profileBarRect.anchoredPosition = new Vector2(32f, -155f);
            profileBarRect.sizeDelta = new Vector2(520f, 44f);
            var profileBarLayout = profileBarGo.AddComponent<HorizontalLayoutGroup>();
            profileBarLayout.spacing = 8f;
            profileBarLayout.childAlignment = TextAnchor.MiddleLeft;
            profileBarLayout.childControlWidth = false;
            profileBarLayout.childControlHeight = true;
            profileAButton = CreateMenuTab(profileBarGo.transform, "main_menu_profile_a", "A", "2-slot baseline.");
            profileBButton = CreateMenuTab(profileBarGo.transform, "main_menu_profile_b", "B", "Six-slot arsenal.");
            profileCButton = CreateMenuTab(profileBarGo.transform, "main_menu_profile_c", "C", "Six-slot + directional.");
            profileDButton = CreateMenuTab(profileBarGo.transform, "main_menu_profile_d", "D", "Full combat redesign.");

            var aimToggleGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_aim_toggle_row");
            aimToggleGo.transform.SetParent(menuCanvas.transform, false);
            var aimToggleRect = aimToggleGo.AddComponent<RectTransform>();
            aimToggleRect.anchorMin = new Vector2(0f, 0.5f);
            aimToggleRect.anchorMax = new Vector2(0f, 0.5f);
            aimToggleRect.pivot = new Vector2(0f, 0.5f);
            aimToggleRect.anchoredPosition = new Vector2(32f, -290f);
            aimToggleRect.sizeDelta = new Vector2(520f, 40f);
            aimVisualsToggle = CreateMenuToggle(aimToggleGo.transform, "main_menu_aim_visuals_toggle",
                "Show aim visuals (V in run)", CombatExperimentContext.ShowAimVisuals);

            var captainToggleGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_captain_active_toggle_row");
            captainToggleGo.transform.SetParent(menuCanvas.transform, false);
            var captainToggleRect = captainToggleGo.AddComponent<RectTransform>();
            captainToggleRect.anchorMin = new Vector2(0f, 0.5f);
            captainToggleRect.anchorMax = new Vector2(0f, 0.5f);
            captainToggleRect.pivot = new Vector2(0f, 0.5f);
            captainToggleRect.anchoredPosition = new Vector2(32f, -338f);
            captainToggleRect.sizeDelta = new Vector2(520f, 40f);
            captainActivesToggle = CreateMenuToggle(captainToggleGo.transform, "main_menu_captain_actives_toggle",
                "Enable captain actives (Q)", CombatExperimentContext.CaptainActivesEnabled);

            var columnGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_collection_column");
            columnGo.transform.SetParent(menuCanvas.transform, false);
            var columnRect = columnGo.AddComponent<RectTransform>();
            columnRect.anchorMin = new Vector2(1f, 0f);
            columnRect.anchorMax = new Vector2(1f, 1f);
            columnRect.pivot = new Vector2(1f, 0.5f);
            columnRect.anchoredPosition = new Vector2(-32f, 0f);
            columnRect.sizeDelta = new Vector2(540f, -48f);

            var columnLayout = columnGo.AddComponent<VerticalLayoutGroup>();
            columnLayout.childAlignment = TextAnchor.UpperRight;
            columnLayout.spacing = 10f;
            columnLayout.padding = new RectOffset(0, 0, 24, 24);
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;

            var tabBarGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_tab_bar");
            tabBarGo.transform.SetParent(columnGo.transform, false);
            tabBarGo.AddComponent<RectTransform>();
            var tabBarLayout = tabBarGo.AddComponent<HorizontalLayoutGroup>();
            tabBarLayout.spacing = 8f;
            tabBarLayout.childAlignment = TextAnchor.MiddleRight;
            tabBarLayout.childControlWidth = false;
            tabBarLayout.childControlHeight = true;
            tabBarLayout.childForceExpandWidth = false;
            tabBarLayout.childForceExpandHeight = false;
            var tabBarLayoutElement = tabBarGo.AddComponent<LayoutElement>();
            tabBarLayoutElement.preferredHeight = 44f;

            weaponsTabButton = CreateMenuTab(tabBarGo.transform, "main_menu_tab_weapons", "Weapons",
                "Weapon collection tab.");
            logbooksTabButton = CreateMenuTab(tabBarGo.transform, "main_menu_tab_logbooks", "Logbooks",
                "Logbook collection tab.");
            itemsTabButton = CreateMenuTab(tabBarGo.transform, "main_menu_tab_items", "Items",
                "Item collection tab.");
            questsTabButton = CreateMenuTab(tabBarGo.transform, "main_menu_tab_quests", "Quests",
                "Quest collection tab.");

            var collectionGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_collection");
            collectionGo.transform.SetParent(columnGo.transform, false);
            collectionPanel = collectionGo.AddComponent<RectTransform>();
            collectionPanel.anchorMin = Vector2.zero;
            collectionPanel.anchorMax = Vector2.one;
            collectionPanel.offsetMin = Vector2.zero;
            collectionPanel.offsetMax = Vector2.zero;
            var collectionLayoutElement = collectionGo.AddComponent<LayoutElement>();
            collectionLayoutElement.minHeight = 640f;
            collectionLayoutElement.flexibleHeight = 1f;
            collectionLayoutElement.flexibleWidth = 1f;
            _collectionView = V3MainMenuCollectionView.Ensure(collectionPanel);

            var captainBarGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_captain_bar", typeof(RectTransform));
            captainBarGo.transform.SetParent(menuCanvas.transform, false);
            var captainBarRect = captainBarGo.GetComponent<RectTransform>();
            captainBarRect.anchorMin = new Vector2(0.5f, 0.5f);
            captainBarRect.anchorMax = new Vector2(0.5f, 0.5f);
            captainBarRect.anchoredPosition = new Vector2(0f, 48f);
            captainBarRect.sizeDelta = new Vector2(680f, 48f);
            var captainBarLayout = captainBarGo.AddComponent<HorizontalLayoutGroup>();
            captainBarLayout.spacing = 6f;
            captainBarLayout.childAlignment = TextAnchor.MiddleCenter;
            captainBarLayout.childControlWidth = false;
            captainBarLayout.childControlHeight = true;

            foreach (var id in V3CaptainCatalog.MenuOrder)
            {
                var label = V3CaptainCatalog.GetMenuLabel(id);
                var button = V3PlaceholderFactory.CreateUiButton(
                    captainBarGo.transform,
                    $"main_menu_captain_{id.ToString().ToLowerInvariant()}",
                    label,
                    new Vector2(96f, 44f),
                    V3CaptainCatalog.GetPassiveSummary(id));
                var layoutElement = button.gameObject.AddComponent<LayoutElement>();
                layoutElement.preferredWidth = 96f;
                layoutElement.preferredHeight = 44f;
                switch (id)
                {
                    case V3CaptainId.Rook: rookButton = button; break;
                    case V3CaptainId.Brine: brineButton = button; break;
                    case V3CaptainId.Flint: flintButton = button; break;
                }

                _captainButtons[id] = button;
            }

            playButton = V3PlaceholderFactory.CreateUiButton(menuCanvas.transform, "main_menu_play", "Saltwind Voyage",
                new Vector2(320f, 72f), "Primary play CTA — starts V3SaltwindRun.");
            var playRect = playButton.GetComponent<RectTransform>();
            playRect.anchorMin = new Vector2(0.5f, 0.5f);
            playRect.anchorMax = new Vector2(0.5f, 0.5f);
            playRect.anchoredPosition = new Vector2(0f, -30f);

            quitButton = V3PlaceholderFactory.CreateUiButton(menuCanvas.transform, "main_menu_quit", "Quit",
                new Vector2(220f, 56f), "Quit button.");
            var quitRect = quitButton.GetComponent<RectTransform>();
            quitRect.anchorMin = new Vector2(0.5f, 0.5f);
            quitRect.anchorMax = new Vector2(0.5f, 0.5f);
            quitRect.anchoredPosition = new Vector2(0f, -120f);

            var versionGo = new GameObject("PLACEHOLDER_v3_ui_main_menu_version");
            versionGo.transform.SetParent(menuCanvas.transform, false);
            var versionRect = versionGo.AddComponent<RectTransform>();
            versionRect.anchorMin = new Vector2(1f, 0f);
            versionRect.anchorMax = new Vector2(1f, 0f);
            versionRect.anchoredPosition = new Vector2(-24f, 24f);
            versionRect.sizeDelta = new Vector2(320f, 32f);
            versionText = versionGo.AddComponent<TextMeshProUGUI>();
            versionText.fontSize = 18;
            versionText.alignment = TextAlignmentOptions.BottomRight;
            versionText.color = new Color(0.7f, 0.75f, 0.8f, 0.9f);
        }

        static Button CreateMenuTab(Transform parent, string id, string label, string hint)
        {
            var button = V3PlaceholderFactory.CreateUiButton(parent, id, label, new Vector2(118f, 40f), hint);
            var layoutElement = button.gameObject.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = 118f;
            layoutElement.preferredHeight = 40f;
            return button;
        }

        static Toggle CreateMenuToggle(Transform parent, string id, string label, bool startingValue)
        {
            var root = new GameObject($"PLACEHOLDER_v3_ui_{id}", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var toggle = root.AddComponent<Toggle>();
            toggle.isOn = startingValue;

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.18f, 0.92f);

            var checkGo = new GameObject("Checkmark", typeof(RectTransform));
            checkGo.transform.SetParent(root.transform, false);
            var checkRect = checkGo.GetComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0f, 0.5f);
            checkRect.anchorMax = new Vector2(0f, 0.5f);
            checkRect.pivot = new Vector2(0f, 0.5f);
            checkRect.anchoredPosition = new Vector2(8f, 0f);
            checkRect.sizeDelta = new Vector2(24f, 24f);
            var checkImage = checkGo.AddComponent<Image>();
            checkImage.color = ProfileActiveColor;
            toggle.graphic = checkImage;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(root.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(36f, 0f);
            labelRect.offsetMax = new Vector2(-8f, 0f);
            var text = labelGo.AddComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = V3PlaceholderFactory.UiText;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = 18;

            root.AddComponent<PlaceholderMarker>().Configure("ui", id, "Toggle combat aim arc visuals during playtests.");
            return toggle;
        }
    }
}
