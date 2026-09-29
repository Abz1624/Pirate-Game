using RoguePirates.V4.Foundation.UI;
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The run's UI groundwork before the presenters bind (M14.14; was LegacyPresentationAdapter.EnsureRunUi, V3's
    /// V3RunUiBootstrap.EnsureAll steps): an event system, the run canvas on top (sorting order 20) with V3's canvas
    /// scaler settings and the HUD art settings. (M15: V3's placeholder HUD pieces and old RunHud layout, which this used
    /// to hide, are gone with V3.)
    /// </summary>
    public static class RunCanvasSetup
    {
        public static Canvas Apply()
        {
            EnsureEventSystem();
            var go = GameObject.Find("RunCanvas");
            var canvas = go != null ? go.GetComponent<Canvas>() : null;
            if (canvas == null) return null;
            canvas.sortingOrder = 20;
            UiCanvasConfigurator.Configure(canvas);
            PlayerInfoHudArtSettings.Reload();

            return canvas;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

    }
}
