using System.Reflection;
using HighSeas.V3;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RoguePirates.V4.Legacy
{
    /// <summary>
    /// The legacy results screens build placeholder buttons in Awake and add listeners in Start that restart through
    /// V3RunBootstrap (absent in V4) or load the V3 menu. Once the run has ended every Start has run, so their
    /// Retry/Home listeners are replaced with V4 restart and V4 menu. Covers screens added at runtime too.
    /// </summary>
    [DeletedInPhase(6, "ResultsPresenter replaces the legacy results screens")]
    public static class LegacyResultsRebinder
    {
        const string RestartButtonName = "PLACEHOLDER_v3_ui_run_results_restart";
        const string MenuButtonName = "PLACEHOLDER_v3_ui_run_results_menu";
        static readonly FieldInfo ContinueField = typeof(V3RunResultsScreen).GetField("continueButton", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly FieldInfo MenuField = typeof(V3RunResultsScreen).GetField("menuButton", BindingFlags.Instance | BindingFlags.NonPublic);

        public static void Rebind(UnityAction restart, UnityAction menu)
        {
            foreach (var screen in Object.FindObjectsByType<V3RunResultsScreen>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Replace(ContinueField?.GetValue(screen) as Button ?? FindButton(screen.transform, RestartButtonName), restart);
                Replace(MenuField?.GetValue(screen) as Button ?? FindButton(screen.transform, MenuButtonName), menu);
            }
        }

        static Button FindButton(Transform root, string name)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                if (button.name == name) return button;
            return null;
        }

        static void Replace(Button button, UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }
    }
}
