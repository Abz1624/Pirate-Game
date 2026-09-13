using HighSeas.Core;
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

        V3CaptainId _selectedCaptain = V3CaptainId.Rook;

        void Awake()
        {
            if (menuCanvas == null)
                BuildPlaceholderMenu();
        }

        void Start()
        {
            AppRoot.EnsureInitialized();
            _selectedCaptain = V3CaptainSelection.Selected;
            if (titleText != null)
                titleText.text = "Rogue Pirates";
            if (versionText != null)
                versionText.text = $"v3 prototype · {Application.version}";
            if (playButton != null)
                playButton.onClick.AddListener(OnPlay);
            if (quitButton != null)
                quitButton.onClick.AddListener(OnQuit);
            if (rookButton != null)
                rookButton.onClick.AddListener(() => SelectCaptain(V3CaptainId.Rook));
            if (brineButton != null)
                brineButton.onClick.AddListener(() => SelectCaptain(V3CaptainId.Brine));
            if (flintButton != null)
                flintButton.onClick.AddListener(() => SelectCaptain(V3CaptainId.Flint));
            RefreshCaptainUi();
        }

        void SelectCaptain(V3CaptainId id)
        {
            _selectedCaptain = id;
            V3CaptainSelection.Selected = id;
            RefreshCaptainUi();
        }

        void RefreshCaptainUi()
        {
            if (captainLabel == null) return;
            var profile = V3CaptainProfile.Get(_selectedCaptain);
            captainLabel.text = $"Captain: {profile.DisplayName}";
        }

        void OnPlay() => AppRoot.Instance?.LoadScene(V3SceneNames.SaltwindRun);

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

            rookButton = V3PlaceholderFactory.CreateUiButton(menuCanvas.transform, "main_menu_captain_rook", "Rook",
                new Vector2(140f, 48f), "Generalist starter captain.");
            var rookRect = rookButton.GetComponent<RectTransform>();
            rookRect.anchorMin = new Vector2(0.5f, 0.5f);
            rookRect.anchorMax = new Vector2(0.5f, 0.5f);
            rookRect.anchoredPosition = new Vector2(-160f, 50f);

            brineButton = V3PlaceholderFactory.CreateUiButton(menuCanvas.transform, "main_menu_captain_brine", "Brine",
                new Vector2(140f, 48f), "Defensive starter captain.");
            var brineRect = brineButton.GetComponent<RectTransform>();
            brineRect.anchorMin = new Vector2(0.5f, 0.5f);
            brineRect.anchorMax = new Vector2(0.5f, 0.5f);
            brineRect.anchoredPosition = new Vector2(0f, 50f);

            flintButton = V3PlaceholderFactory.CreateUiButton(menuCanvas.transform, "main_menu_captain_flint", "Flint",
                new Vector2(140f, 48f), "Powderrunner captain — starts with kegs.");
            var flintRect = flintButton.GetComponent<RectTransform>();
            flintRect.anchorMin = new Vector2(0.5f, 0.5f);
            flintRect.anchorMax = new Vector2(0.5f, 0.5f);
            flintRect.anchoredPosition = new Vector2(160f, 50f);

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
    }
}
