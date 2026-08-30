using HighSeas.Core;
using HighSeas.UI;
using TMPro;
using UnityEngine;

namespace HighSeas.Tuning
{
    public sealed class TuningSandboxHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI label;
        [SerializeField] TuningSandbox sandbox;

        void Awake()
        {
            if (label == null)
                label = GetComponent<TextMeshProUGUI>();
            if (label == null)
                label = gameObject.AddComponent<TextMeshProUGUI>();
            if (sandbox == null)
                sandbox = FindFirstObjectByType<TuningSandbox>();

            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(12f, -12f);
                rt.sizeDelta = new Vector2(560f, 420f);
            }

            label.fontSize = 15f;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.color = new Color(0.92f, 0.96f, 1f, 0.95f);
            label.raycastTarget = false;
        }

        void Update()
        {
            if (label == null || sandbox == null) return;
            if (!sandbox.HelpVisible)
            {
                label.text = "<b>Tuning</b>  (H = show help)";
                return;
            }

            var levels = LevelSystem.Instance;
            var run = RunManager.Instance;
            label.text =
                "<b>TUNING SANDBOX</b>\n" +
                "Window: HighSeas → Tuning Window\n" +
                "Sync: Apply to Play (in window) or Sync Tuning To Runtime\n\n" +
                "<b>Weapons</b>\n" +
                "F1 Deck Cannons | F2 Broadside | F3 Powder Kegs\n" +
                "F4 Lightning Mast | F5 Cutlasses | F6 Burning Oil\n" +
                "Shift+F# adds 2nd slot without clearing\n\n" +
                "<b>Combat</b>\n" +
                "E spawn cluster | [ ] or N/P cycle enemy | X clear\n" +
                "G XP crystal | C gold coins | R refill hull | L force level-up\n" +
                "- / + tune Broadside spread (F2)\n" +
                "H toggle this panel\n\n" +
                $"<b>Status</b> {sandbox.StatusMessage}\n" +
                $"Enemy: {sandbox.CurrentEnemyLabel()} ({sandbox.ActiveEnemyCount()} active)\n" +
                $"Weapons: {sandbox.EquippedWeaponsLabel()}\n" +
                (sandbox.BroadsideSpreadDegrees() is float spread ? $"Broadside spread: {spread:0}°\n" : "") +
                (levels != null ? $"Level {levels.Level}  XP {Mathf.FloorToInt(levels.CurrentXP)}/{Mathf.FloorToInt(levels.XPToNext)}\n" : "") +
                (run != null ? $"Time {run.ElapsedSeconds:0.0}s" : "");
        }
    }
}
