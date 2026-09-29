using HighSeas.UI;
using HighSeas.V3;
using UnityEngine;
using UnityEngine.InputSystem.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The run's UI groundwork before the presenters bind (M14.14; was LegacyPresentationAdapter.EnsureRunUi, V3's
    /// V3RunUiBootstrap.EnsureAll steps): an event system, the run canvas on top (sorting order 20) with V3's canvas
    /// scaler settings, the HUD art settings, and the V3 placeholder HUD pieces of the authored canvas hidden (the V4 HUD
    /// presenter drives the authored widgets), along with V3's old RunHud layout and weapon-level strips left in the scene.
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
            V3PlayerInfoHudArtSettings.Reload();

            if (AuthoredHudPresenter.FindAuthoredCanvas() != null)
            {
                HidePlaceholders<V3ObjectiveHud>(canvas.transform);
                HidePlaceholders<V3RunLoadoutHud>(canvas.transform);
                HidePlaceholders<V3RunCaptainActiveHud>(canvas.transform);
                HidePlaceholders<V3RunStatsHud>(canvas.transform);
                HidePlaceholders<V3RunTimerHud>(canvas.transform);
                HidePlaceholders<V3PlayerInfoHud>(canvas.transform);
            }
            var hudRoot = canvas.transform.Find(RunHudUiFactory.RootName);
            if (hudRoot != null) hudRoot.gameObject.SetActive(false);
            foreach (var strip in canvas.GetComponentsInChildren<WeaponLevelStripHud>(true)) strip.gameObject.SetActive(false);
            return canvas;
        }

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        static void HidePlaceholders<T>(Transform canvas) where T : Component
        {
            foreach (var component in canvas.GetComponentsInChildren<T>(true))
                if (component.gameObject.name.StartsWith("PLACEHOLDER_")) component.gameObject.SetActive(false);
        }
    }
}
