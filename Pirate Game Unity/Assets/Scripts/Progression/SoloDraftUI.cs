using UnityEngine;

namespace PirateGame.Progression
{
    /// <summary>
    /// Level-up draft overlay using Combat Card Images.
    /// </summary>
    public class SoloDraftUI : MonoBehaviour
    {
        [SerializeField] PlayerRunProgress progress;
        [SerializeField] int offerCount = 3;

        UpgradeCardData[] offers;
        GUIStyle titleStyle;
        GUIStyle smallStyle;
        bool stylesReady;

        void Awake()
        {
            if (progress == null)
                progress = FindFirstObjectByType<PlayerRunProgress>();
        }

        void OnEnable()
        {
            if (progress != null)
                progress.OnLevelUp += HandleLevelUp;
        }

        void OnDisable()
        {
            if (progress != null)
                progress.OnLevelUp -= HandleLevelUp;
        }

        void HandleLevelUp(int level)
        {
            offers = progress.RollDraftOffers(offerCount, System.Environment.TickCount + level);
        }

        void OnGUI()
        {
            if (progress == null || !progress.DraftOpen || offers == null || offers.Length == 0)
                return;

            EnsureStyles();

            var w = Screen.width;
            var h = Screen.height;
            GUI.Box(new Rect(0, 0, w, h), GUIContent.none);

            GUI.Label(new Rect(0, 24, w, 40), $"Level {progress.Level} — choose an upgrade", titleStyle);

            var cardW = Mathf.Min(220f, (w - 80f) / offers.Length);
            var cardH = 300f;
            var totalW = offers.Length * cardW + (offers.Length - 1) * 16f;
            var startX = (w - totalW) * 0.5f;
            var y = h * 0.5f - cardH * 0.5f;

            for (var i = 0; i < offers.Length; i++)
            {
                var rect = new Rect(startX + i * (cardW + 16f), y, cardW, cardH);
                var card = offers[i];
                if (GUI.Button(rect, GUIContent.none))
                    progress.ApplyCard(card);

                if (card.icon != null)
                    DrawSprite(new Rect(rect.x + 8, rect.y + 8, cardW - 16, cardH - 56), card.icon);

                GUI.Label(new Rect(rect.x, rect.yMax - 44, cardW, 22), card.displayName, titleStyle);
                GUI.Label(new Rect(rect.x, rect.yMax - 24, cardW, 20), DescribeEffect(card), smallStyle);
            }
        }

        static string DescribeEffect(UpgradeCardData card)
        {
            return card.effect switch
            {
                UpgradeEffect.DamagePercent => $"+{card.magnitude * 100f:0}% damage",
                UpgradeEffect.FireRatePercent => $"+{card.magnitude * 100f:0}% fire rate",
                UpgradeEffect.CannonCount => $"+{Mathf.RoundToInt(card.magnitude)} cannon",
                UpgradeEffect.Pierce => $"+{Mathf.RoundToInt(card.magnitude)} pierce",
                UpgradeEffect.RangePercent => $"+{card.magnitude * 100f:0}% range",
                UpgradeEffect.MaxHealthPercent => $"+{card.magnitude * 100f:0}% hull",
                UpgradeEffect.MoveSpeedPercent => $"+{card.magnitude * 100f:0}% speed",
                UpgradeEffect.TurnRatePercent => $"+{card.magnitude * 100f:0}% turn",
                UpgradeEffect.HealthRegen => $"+{card.magnitude:0.#} HP/s",
                UpgradeEffect.PickupRadiusPercent => $"+{card.magnitude * 100f:0}% pickup",
                _ => card.family.ToString()
            };
        }

        static void DrawSprite(Rect rect, Sprite sprite)
        {
            var tex = sprite.texture;
            var r = sprite.rect;
            var coords = new Rect(
                r.x / tex.width,
                r.y / tex.height,
                r.width / tex.width,
                r.height / tex.height);
            GUI.DrawTextureWithTexCoords(rect, tex, coords, true);
        }

        void EnsureStyles()
        {
            if (stylesReady) return;
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 12,
                normal = { textColor = new Color(0.85f, 0.92f, 1f) }
            };
            stylesReady = true;
        }
    }
}
