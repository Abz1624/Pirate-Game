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
            UiCanvasConfigurator.Configure(canvas);

            DisableLegacyRunEnd(canvas);

            var hud = canvas.GetComponent<RunHud>();
            if (hud == null)
                hud = canvas.gameObject.AddComponent<RunHud>();
            hud.RebuildUiLayout();

            EnsureLevelUpModal(canvas);
            EnsureV3ResultsScreen(canvas);
            V3ObjectiveHud.Ensure(canvas.transform);
            V3MinimapHud.Ensure(canvas.transform, layout);
            V3ToastHud.Ensure(canvas.transform);

            return canvas;
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
            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas != null) return canvas;

            var go = new GameObject("RunCanvas");
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
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
