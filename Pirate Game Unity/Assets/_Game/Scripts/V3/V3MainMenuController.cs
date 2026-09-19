using HighSeas.Combat;
using HighSeas.Core;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>V3 main menu — captain carousel and play CTA only.</summary>
    public sealed class V3MainMenuController : MonoBehaviour
    {
        [SerializeField] Canvas menuCanvas;

        V3MainMenuCharacterSelect _characterSelect;
        V3CaptainId _selectedCaptain = V3CaptainId.Rook;
        string _selectedProfileId = "D_FULL_COMBAT_V2";

        void Awake()
        {
            _selectedProfileId = CombatExperimentContext.SelectedProfileId;
            EnsureMenuCamera();
            EnsureEventSystem();
            if (menuCanvas == null)
                BuildCharacterSelectMenu();
            else
                _characterSelect = V3MainMenuCharacterSelect.Ensure(menuCanvas.transform);
        }

        void Start()
        {
            AppRoot.EnsureInitialized();
            _selectedCaptain = V3CaptainSelection.Selected;
            _selectedProfileId = CombatExperimentContext.SelectedProfileId;
            CombatExperimentContext.SetActiveProfileId(_selectedProfileId);

            if (_characterSelect == null && menuCanvas != null)
                _characterSelect = V3MainMenuCharacterSelect.Ensure(menuCanvas.transform);

            if (_characterSelect != null)
            {
                _characterSelect.CaptainChanged += OnCaptainChanged;
                _characterSelect.PlayRequested += OnPlay;
                _characterSelect.SetCaptain(_selectedCaptain, true);
            }
        }

        void OnDestroy()
        {
            if (_characterSelect == null)
                return;
            _characterSelect.CaptainChanged -= OnCaptainChanged;
            _characterSelect.PlayRequested -= OnPlay;
        }

        void OnCaptainChanged(V3CaptainId captain)
        {
            if (V3CaptainCatalog.IsLocked(captain))
                return;
            _selectedCaptain = captain;
            V3CaptainSelection.Selected = captain;
        }

        void OnPlay()
        {
            if (V3CaptainCatalog.IsLocked(_selectedCaptain))
                return;
            V3CaptainSelection.Selected = _selectedCaptain;
            CombatExperimentContext.SetActiveProfileId(CombatExperimentProfile.FullCombatV2Id);
            AppRoot.Instance?.LoadScene(V3SceneNames.SaltwindRun);
        }

        static void EnsureMenuCamera()
        {
            if (Camera.main != null)
                return;

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.1f, 0.16f, 1f);
            cameraGo.AddComponent<AudioListener>();
        }

        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
                return;

            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<InputSystemUIInputModule>();
        }

        void BuildCharacterSelectMenu()
        {
            var canvasGo = new GameObject("v3_ui_main_menu_canvas");
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
