using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Scrollable card grid for the v3 main-menu collection panel.</summary>
    public sealed class V3MainMenuCollectionView : MonoBehaviour
    {
        const float CardWidth = 220f;
        const float CardHeight = 108f;

        RectTransform _content;
        TextMeshProUGUI _headerText;
        readonly List<GameObject> _cards = new();

        public static V3MainMenuCollectionView Ensure(Transform parent)
        {
            var existing = parent.GetComponentInChildren<V3MainMenuCollectionView>(true);
            if (existing != null) return existing;

            var root = new GameObject("PLACEHOLDER_v3_ui_collection_view", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.09f, 0.14f, 0.94f);

            var headerGo = new GameObject("Header", typeof(RectTransform));
            headerGo.transform.SetParent(root.transform, false);
            var headerRect = headerGo.GetComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0f, 1f);
            headerRect.anchorMax = new Vector2(1f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = Vector2.zero;
            headerRect.sizeDelta = new Vector2(0f, 36f);
            var headerText = headerGo.AddComponent<TextMeshProUGUI>();
            headerText.fontSize = 20f;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.MidlineLeft;
            headerText.color = V3PlaceholderFactory.UiAccent;
            headerText.margin = new Vector4(16f, 0f, 16f, 0f);

            var scrollGo = new GameObject("Scroll", typeof(RectTransform));
            scrollGo.transform.SetParent(root.transform, false);
            var scrollRect = scrollGo.GetComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.offsetMin = new Vector2(8f, 8f);
            scrollRect.offsetMax = new Vector2(-8f, -40f);

            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var viewportGo = new GameObject("Viewport", typeof(RectTransform));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            var viewportRect = viewportGo.GetComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = Vector2.zero;
            viewportRect.offsetMax = Vector2.zero;
            viewportGo.AddComponent<RectMask2D>();
            scroll.viewport = viewportRect;

            var contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            var content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);
            scroll.content = content;

            var grid = contentGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(CardWidth, CardHeight);
            grid.spacing = new Vector2(12f, 12f);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.padding = new RectOffset(8, 8, 8, 8);
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var view = root.AddComponent<V3MainMenuCollectionView>();
            view._content = content;
            view._headerText = headerText;
            return view;
        }

        public void SetEntries(string tabTitle, IReadOnlyList<V3CollectionEntry> entries)
        {
            if (_headerText != null)
                _headerText.text = tabTitle;

            ClearCards();
            if (_content == null || entries == null) return;

            for (var i = 0; i < entries.Count; i++)
                _cards.Add(CreateCard(_content, entries[i]));
        }

        void ClearCards()
        {
            for (var i = 0; i < _cards.Count; i++)
            {
                if (_cards[i] != null)
                    Destroy(_cards[i]);
            }

            _cards.Clear();
        }

        static GameObject CreateCard(Transform parent, V3CollectionEntry entry)
        {
            var card = new GameObject($"Card_{entry.Id}", typeof(RectTransform));
            card.transform.SetParent(parent, false);

            var borderColor = entry.Equipped
                ? V3PlaceholderFactory.UiAccent
                : entry.Unlocked
                    ? new Color(0.35f, 0.72f, 1f, 1f)
                    : new Color(0.35f, 0.38f, 0.42f, 0.9f);

            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(card.transform, false);
            var borderRect = border.GetComponent<RectTransform>();
            borderRect.sizeDelta = new Vector2(CardWidth + 4f, CardHeight + 4f);
            border.GetComponent<Image>().color = borderColor;

            var fill = card.AddComponent<Image>();
            fill.color = new Color(0.08f, 0.12f, 0.18f, 0.96f);

            var badgeGo = new GameObject("Badge", typeof(RectTransform));
            badgeGo.transform.SetParent(card.transform, false);
            var badgeRect = badgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(1f, 1f);
            badgeRect.anchorMax = new Vector2(1f, 1f);
            badgeRect.pivot = new Vector2(1f, 1f);
            badgeRect.anchoredPosition = new Vector2(-8f, -8f);
            badgeRect.sizeDelta = new Vector2(72f, 20f);
            var badge = badgeGo.AddComponent<TextMeshProUGUI>();
            badge.text = entry.Equipped ? "EQUIPPED" : entry.Unlocked ? "UNLOCKED" : "LOCKED";
            badge.fontSize = 10f;
            badge.fontStyle = FontStyles.Bold;
            badge.alignment = TextAlignmentOptions.MidlineRight;
            badge.color = borderColor;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(card.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(12f, -10f);
            titleRect.sizeDelta = new Vector2(-84f, 24f);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = entry.DisplayName;
            title.fontSize = 16f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.TopLeft;
            title.color = entry.Unlocked ? V3PlaceholderFactory.UiText : new Color(0.7f, 0.72f, 0.75f, 1f);
            title.overflowMode = TextOverflowModes.Ellipsis;

            var descGo = new GameObject("Description", typeof(RectTransform));
            descGo.transform.SetParent(card.transform, false);
            var descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.offsetMin = new Vector2(12f, 28f);
            descRect.offsetMax = new Vector2(-12f, -34f);
            var desc = descGo.AddComponent<TextMeshProUGUI>();
            desc.text = entry.Description;
            desc.fontSize = 12f;
            desc.alignment = TextAlignmentOptions.TopLeft;
            desc.color = new Color(0.78f, 0.82f, 0.86f, 0.95f);
            desc.enableWordWrapping = true;

            var reqGo = new GameObject("Requirement", typeof(RectTransform));
            reqGo.transform.SetParent(card.transform, false);
            var reqRect = reqGo.GetComponent<RectTransform>();
            reqRect.anchorMin = new Vector2(0f, 0f);
            reqRect.anchorMax = new Vector2(1f, 0f);
            reqRect.pivot = new Vector2(0f, 0f);
            reqRect.anchoredPosition = new Vector2(12f, 8f);
            reqRect.sizeDelta = new Vector2(-24f, 18f);
            var req = reqGo.AddComponent<TextMeshProUGUI>();
            req.text = entry.Unlocked ? entry.UnlockRequirement : $"Requires: {entry.UnlockRequirement}";
            req.fontSize = 10f;
            req.alignment = TextAlignmentOptions.BottomLeft;
            req.color = entry.Unlocked
                ? new Color(0.55f, 0.62f, 0.68f, 0.9f)
                : new Color(0.95f, 0.55f, 0.35f, 0.95f);
            req.overflowMode = TextOverflowModes.Ellipsis;

            return card;
        }
    }
}
