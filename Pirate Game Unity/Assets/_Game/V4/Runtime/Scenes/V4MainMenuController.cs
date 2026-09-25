using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.V3;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace RoguePirates.V4
{
    /// <summary>
    /// V4 main menu: the V3 Captain carousel (reused as a view) with Play loading the V4 run scene.
    /// Captain locks follow V3 rules until the Captain config lands in M9.
    /// </summary>
    public sealed class V4MainMenuController : MonoBehaviour
    {
        [SerializeField] Canvas menuCanvas;

        V3MainMenuCharacterSelect _characterSelect;
        V3CaptainId _selectedCaptain = V3CaptainId.Rook;

        void Awake()
        {
            EnsureMenuCamera();
            EnsureEventSystem();
            if (menuCanvas == null) BuildCharacterSelectMenu();
            else _characterSelect = V3MainMenuCharacterSelect.Ensure(menuCanvas.transform);
        }

        void Start()
        {
            AppRoot.EndActiveSaltwindRunContext();
            AppRoot.EnsureInitialized();
            _selectedCaptain = V3CaptainSelection.Selected;
            CombatExperimentContext.SetActiveProfileId(CombatExperimentContext.SelectedProfileId);

            if (_characterSelect == null && menuCanvas != null)
                _characterSelect = V3MainMenuCharacterSelect.Ensure(menuCanvas.transform);
            if (_characterSelect == null) return;
            _characterSelect.CaptainChanged += OnCaptainChanged;
            _characterSelect.PlayRequested += OnPlay;
            _characterSelect.SetCaptain(_selectedCaptain, true);
        }

        void OnDestroy()
        {
            if (_characterSelect == null) return;
            _characterSelect.CaptainChanged -= OnCaptainChanged;
            _characterSelect.PlayRequested -= OnPlay;
        }

        void OnCaptainChanged(V3CaptainId captain)
        {
            if (V3CaptainCatalog.IsLocked(captain)) return;
            _selectedCaptain = captain;
            V3CaptainSelection.Selected = captain;
        }

        void OnPlay()
        {
            if (V3CaptainCatalog.IsLocked(_selectedCaptain)) return;
            V3CaptainSelection.Selected = _selectedCaptain;
            CombatExperimentContext.SetActiveProfileId(CombatExperimentProfile.FullCombatV2Id);
            SceneManager.LoadScene(V4SceneNames.SaltwindRun);
        }

        static void EnsureMenuCamera()
        {
            if (Camera.main != null) return;
            var cameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.1f, 0.16f, 1f);
            cameraGo.AddComponent<AudioListener>();
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        void BuildCharacterSelectMenu()
        {
            var canvasGo = new GameObject("v4_ui_main_menu_canvas");
            menuCanvas = canvasGo.AddComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _characterSelect = V3MainMenuCharacterSelect.Ensure(menuCanvas.transform);
        }
    }
}
