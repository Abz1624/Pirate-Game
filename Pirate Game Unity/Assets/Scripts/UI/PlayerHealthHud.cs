using PirateGame.Combat;
using UnityEngine;

namespace PirateGame.UI
{
    /// <summary>
    /// Simple ship health bar for solo runs.
    /// </summary>
    public class PlayerHealthHud : MonoBehaviour
    {
        [SerializeField] Health playerHealth;
        [SerializeField] Vector2 screenOffset = new(16f, 16f);
        [SerializeField] Vector2 barSize = new(220f, 22f);

        GUIStyle labelStyle;
        bool stylesReady;

        void Awake()
        {
            if (playerHealth == null)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                    playerHealth = player.GetComponent<Health>();
            }
        }

        void OnGUI()
        {
            if (playerHealth == null) return;

            EnsureStyles();

            var x = screenOffset.x;
            var y = Screen.height - screenOffset.y - barSize.y - 24f;
            var rect = new Rect(x, y, barSize.x, barSize.y);
            var fill = playerHealth.MaxHealth > 0f
                ? Mathf.Clamp01(playerHealth.CurrentHealth / playerHealth.MaxHealth)
                : 0f;

            GUI.Box(rect, GUIContent.none);
            var fillRect = new Rect(rect.x + 2f, rect.y + 2f, (rect.width - 4f) * fill, rect.height - 4f);
            var prev = GUI.color;
            GUI.color = Color.Lerp(new Color(0.85f, 0.2f, 0.15f), new Color(0.2f, 0.85f, 0.35f), fill);
            GUI.DrawTexture(fillRect, Texture2D.whiteTexture);
            GUI.color = prev;

            var hpLabel = $"Hull {Mathf.CeilToInt(playerHealth.CurrentHealth)} / {Mathf.CeilToInt(playerHealth.MaxHealth)}";
            GUI.Label(new Rect(x, y - 22f, barSize.x, 20f), hpLabel, labelStyle);
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white }
            };
            stylesReady = true;
        }
    }
}
