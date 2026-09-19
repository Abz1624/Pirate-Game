using HighSeas.UI;
using HighSeas.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Ensures full v3 run HUD at runtime (Run.unity wiring is not always present).</summary>
    public static class V3RunUiBootstrap
    {
        public static Canvas EnsureAll(StageLayout layout)
        {
            EnsureEventSystem();
            var canvas = EnsureRunCanvas();
            canvas.sortingOrder = 20;
            UiCanvasConfigurator.Configure(canvas);

            DisableLegacyRunEnd(canvas);

            EnsureLevelSystem();

            var hud = canvas.GetComponent<RunHud>();
            if (hud == null)
                hud = canvas.gameObject.AddComponent<RunHud>();
            hud.RebuildUiLayout();
            hud.ForceRefresh();
            V3PlayerInfoHudArtSettings.Reload();

            EnsureLevelUpModal(canvas);
            EnsureV3ResultsScreen(canvas);
            V3DiscoverySystem.Ensure(layout);
            V3MinimapHud.Ensure(canvas.transform, layout);
            V3BossBeaconGuide.Ensure(canvas.transform);
            V3ToastHud.Ensure(canvas.transform);
            V3InteractionController.Ensure(canvas.transform);

            var authoredHud = V3AuthoredGameplayHud.EnsureInScene();
            if (authoredHud == null)
            {
                V3ObjectiveHud.Ensure(canvas.transform);
                V3RunLoadoutHud.Ensure(canvas.transform);
                V3RunCaptainActiveHud.Ensure(canvas.transform);
                V3RunStatsHud.Ensure(canvas.transform);
                V3RunTimerHud.Ensure(canvas.transform);
                V3PlayerInfoHud.Ensure(canvas.transform);
            }
            else
            {
                HideAuthoredPlaceholderDuplicates(canvas);
            }

            HideLegacyRunHudPanels(canvas);
            HideLegacyWeaponStrip(canvas);

            return canvas;
        }

        static void HideAuthoredPlaceholderDuplicates(Canvas canvas)
        {
            HideChildPlaceholders<V3ObjectiveHud>(canvas.transform);
            HideChildPlaceholders<V3RunLoadoutHud>(canvas.transform);
            HideChildPlaceholders<V3RunCaptainActiveHud>(canvas.transform);
            HideChildPlaceholders<V3RunStatsHud>(canvas.transform);
            HideChildPlaceholders<V3RunTimerHud>(canvas.transform);
            HideChildPlaceholders<V3PlayerInfoHud>(canvas.transform);
        }

        static void HideChildPlaceholders<T>(Transform canvas) where T : Component
        {
            var components = canvas.GetComponentsInChildren<T>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var go = components[i].gameObject;
                if (go.name.StartsWith("PLACEHOLDER_"))
                    go.SetActive(false);
            }
        }

        static void HideLegacyRunHudPanels(Canvas canvas)
        {
            var root = canvas.transform.Find($"{RunHudUiFactory.RootName}");
            if (root == null)
                return;

            var left = root.Find("LeftHudPanel");
            if (left != null)
                left.gameObject.SetActive(false);
        }

        static void HideLegacyWeaponStrip(Canvas canvas)
        {
            var strips = canvas.GetComponentsInChildren<WeaponLevelStripHud>(true);
            for (var i = 0; i < strips.Length; i++)
                strips[i].gameObject.SetActive(false);
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;

            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static Canvas EnsureRunCanvas()
        {
            var runCanvasGo = GameObject.Find("RunCanvas");
            if (runCanvasGo != null && runCanvasGo.TryGetComponent(out Canvas runCanvas))
                return runCanvas;

            var hudCanvas = Object.FindFirstObjectByType<RunHud>()?.GetComponentInParent<Canvas>();
            if (hudCanvas != null)
                return hudCanvas;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) return canvas;

            var go = new GameObject("RunCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        static void EnsureLevelSystem()
        {
            if (LevelSystem.Instance != null) return;

            var systems = GameObject.Find("Systems");
            if (systems == null) return;
            if (systems.GetComponent<LevelSystem>() == null)
                systems.AddComponent<LevelSystem>();
        }

        static void DisableLegacyRunEnd(Canvas canvas)
        {
            var legacy = canvas.GetComponentInChildren<RunEndScreen>(true);
            if (legacy != null)
                legacy.enabled = false;
        }

        static void EnsureLevelUpModal(Canvas canvas)
        {
            var modal = canvas.GetComponentInChildren<LevelUpModal>(true);
            if (modal == null)
            {
                var panel = new GameObject("LevelUpPanel", typeof(RectTransform));
                panel.transform.SetParent(canvas.transform, false);
                var rt = panel.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                modal = panel.AddComponent<LevelUpModal>();
            }

            modal.RebuildUiLayout();
            modal.gameObject.SetActive(false);
        }

        static void EnsureV3ResultsScreen(Canvas canvas)
        {
            if (canvas.GetComponent<V3RunResultsScreen>() == null)
                canvas.gameObject.AddComponent<V3RunResultsScreen>();
        }
    }
}
