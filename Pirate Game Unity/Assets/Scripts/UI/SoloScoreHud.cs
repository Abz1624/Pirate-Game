using PirateGame.Progression;
using UnityEngine;

namespace PirateGame.UI
{
    /// <summary>
    /// Solo run score, kills, level, and XP progress.
    /// </summary>
    public class SoloScoreHud : MonoBehaviour
    {
        [SerializeField] PlayerRunProgress progress;
        [SerializeField] Vector2 screenOffset = new(16f, 16f);
        [SerializeField] Vector2 panelSize = new(200f, 88f);
        [SerializeField] Vector2 xpBarSize = new(200f, 10f);

        GUIStyle titleStyle;
        GUIStyle valueStyle;
        GUIStyle smallStyle;
        bool stylesReady;

        void Awake()
        {
            if (progress == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    progress = player.GetComponent<PlayerRunProgress>();
            }
        }

        void OnGUI()
        {
            if (progress == null) return;

            EnsureStyles();

            var x = Screen.width - screenOffset.x - panelSize.x;
            var y = screenOffset.y;

            GUI.Box(new Rect(x, y, panelSize.x, panelSize.y), GUIContent.none);
            GUI.Label(new Rect(x + 10f, y + 6f, panelSize.x - 20f, 22f), "SCORE", titleStyle);
            GUI.Label(new Rect(x + 10f, y + 26f, panelSize.x - 20f, 28f), progress.Score.ToString("N0"), valueStyle);

            var metaY = y + 56f;
            GUI.Label(new Rect(x + 10f, metaY, 90f, 18f), $"Lv {progress.Level}", smallStyle);
            GUI.Label(new Rect(x + 100f, metaY, 90f, 18f), $"Kills {progress.KillCount}", smallStyle);

            if (progress.TryGetRecentCardToast(out var toast))
            {
                var toastRect = new Rect(x - 220f, y + 12f, 210f, 48f);
                GUI.Box(toastRect, GUIContent.none);
                GUI.Label(new Rect(toastRect.x + 8f, toastRect.y + 6f, toastRect.width - 16f, 36f), toast, smallStyle);
            }

            var barY = y + panelSize.y + 6f;
            var xpFill = progress.XpToNext > 0f ? Mathf.Clamp01(progress.Xp / progress.XpToNext) : 0f;
            GUI.Box(new Rect(x, barY, xpBarSize.x, xpBarSize.y), GUIContent.none);
            var fillRect = new Rect(x + 1f, barY + 1f, (xpBarSize.x - 2f) * xpFill, xpBarSize.y - 2f);
            var prev = GUI.color;
            GUI.color = new Color(0.35f, 0.75f, 1f, 0.95f);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void EnsureStyles()
        {
            if (stylesReady) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.85f, 0.9f, 1f, 0.85f) }
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white }
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = new Color(0.9f, 0.92f, 0.95f, 0.9f) }
            };

            stylesReady = true;
        }
    }
}
