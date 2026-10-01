using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Foundation.UI
{
    public static class LevelUpUiFactory
    {
        public const float CardWidth = 252f;
        public const float CardHeight = 360f;
        public const float CardSpacing = 20f;
        public const float RowContentWidth = CardWidth * 3f + CardSpacing * 2f;

        public struct ModalWiring
        {
            public Button[] cardButtons;
            public LevelUpCardView[] cardViews;
            public Image[] cardFrames;
            public TextMeshProUGUI[] cardCategories;
            public TextMeshProUGUI[] cardTitles;
            public TextMeshProUGUI[] cardLevels;
            public TextMeshProUGUI[] cardRarities;
            public TextMeshProUGUI[] cardDescriptions;
            public TextMeshProUGUI headerTitle;
            public TextMeshProUGUI headerSubtitle;
            public Button acceptButton;
            public Button rejectButton;
            public Button refreshButton;
            public TextMeshProUGUI footerHint;
        }

        public static ModalWiring Rebuild(Transform modalRoot)
        {
            UiCanvasConfigurator.ConfigureFromComponent(modalRoot);
            ClearChildren(modalRoot);

            var panelRt = modalRoot as RectTransform;
            if (panelRt != null)
            {
                panelRt.anchorMin = Vector2.zero;
                panelRt.anchorMax = Vector2.one;
                panelRt.offsetMin = Vector2.zero;
                panelRt.offsetMax = Vector2.zero;
                panelRt.pivot = new Vector2(0.5f, 0.5f);
            }

            var backdrop = modalRoot.GetComponent<Image>();
            if (backdrop == null) backdrop = modalRoot.gameObject.AddComponent<Image>();
            backdrop.color = new Color(0.02f, 0.05f, 0.1f, 0.82f);
            backdrop.raycastTarget = true;

            var headerTitle = CreateText(modalRoot, "HeaderTitle", "LEVEL UP!", AnchorTopCenter(), new Vector2(0f, -48f), new Vector2(720f, 56f), 40f, FontStyles.Bold);
            headerTitle.color = new Color(1f, 0.92f, 0.62f, 1f);
            headerTitle.enableAutoSizing = true;
            headerTitle.fontSizeMin = 28f;
            headerTitle.fontSizeMax = 40f;

            var headerSubtitle = CreateText(modalRoot, "HeaderSubtitle", "Choose your upgrade", AnchorTopCenter(), new Vector2(0f, -96f), new Vector2(640f, 32f), 18f, FontStyles.Italic);
            headerSubtitle.color = new Color(0.78f, 0.84f, 0.92f, 0.95f);
            headerSubtitle.enableAutoSizing = true;
            headerSubtitle.fontSizeMin = 14f;
            headerSubtitle.fontSizeMax = 18f;

            var cardRow = new GameObject("CardRow", typeof(RectTransform));
            cardRow.transform.SetParent(modalRoot, false);
            var rowRt = cardRow.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 0.5f);
            rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.anchoredPosition = new Vector2(0f, -24f);
            rowRt.sizeDelta = new Vector2(RowContentWidth, CardHeight);

            var fitter = cardRow.AddComponent<UiScaleToFitParent>();
            fitter.SetContentWidth(RowContentWidth);

            var layout = cardRow.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = CardSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;

            var wiring = new ModalWiring
            {
                cardButtons = new Button[3],
                cardViews = new LevelUpCardView[3],
                cardFrames = new Image[3],
                cardCategories = new TextMeshProUGUI[3],
                cardTitles = new TextMeshProUGUI[3],
                cardLevels = new TextMeshProUGUI[3],
                cardRarities = new TextMeshProUGUI[3],
                cardDescriptions = new TextMeshProUGUI[3],
                headerTitle = headerTitle,
                headerSubtitle = headerSubtitle
            };

            wiring.acceptButton = CreateFooterButton(modalRoot, "AcceptButton", "Take (E)", new Vector2(-220f, 48f), new Color(0.2f, 0.55f, 0.35f, 0.95f));
            wiring.rejectButton = CreateFooterButton(modalRoot, "RejectButton", "Skip (R)", new Vector2(0f, 48f), new Color(0.45f, 0.18f, 0.18f, 0.95f));
            wiring.refreshButton = CreateFooterButton(modalRoot, "RefreshButton", "Refresh (2)", new Vector2(220f, 48f), new Color(0.22f, 0.32f, 0.55f, 0.95f));
            wiring.footerHint = CreateText(modalRoot, "FooterHint", "1 / 2 / 3: pick a card", AnchorCenter(), new Vector2(0f, -CardHeight * 0.5f - 36f), new Vector2(640f, 28f), 16f, FontStyles.Normal);
            wiring.footerHint.color = new Color(0.75f, 0.82f, 0.9f, 0.95f);

            for (var i = 0; i < 3; i++)
            {
                var card = CreateCard(cardRow.transform, i);
                card.root.AddComponent<LayoutElement>().preferredWidth = CardWidth;
                card.root.GetComponent<LayoutElement>().preferredHeight = CardHeight;
                wiring.cardButtons[i] = card.button;
                wiring.cardViews[i] = card.view;
                wiring.cardFrames[i] = card.border;
                wiring.cardCategories[i] = card.category;
                wiring.cardTitles[i] = card.title;
                wiring.cardLevels[i] = card.level;
                wiring.cardRarities[i] = card.rarity;
                wiring.cardDescriptions[i] = card.description;
            }

            return wiring;
        }

        static Button CreateFooterButton(Transform parent, string name, string label, Vector2 pos, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(200f, 72f); // V5 M7: touch targets
            var img = go.GetComponent<Image>();
            img.color = color;
            var button = go.GetComponent<Button>();
            button.targetGraphic = img;
            var text = CreateText(go.transform, "Label", label, AnchorCenter(), Vector2.zero, new Vector2(190f, 40f), 18f, FontStyles.Bold);
            text.raycastTarget = false;
            return button;
        }

        struct CardParts
        {
            public GameObject root;
            public Button button;
            public LevelUpCardView view;
            public Image border;
            public TextMeshProUGUI category;
            public TextMeshProUGUI title;
            public TextMeshProUGUI level;
            public TextMeshProUGUI rarity;
            public TextMeshProUGUI description;
        }

        public static LevelUpCardView CreateWeaponCard(Transform parent, int index, bool interactable = true)
        {
            var card = CreateCard(parent, index);
            if (!interactable && card.button != null)
            {
                card.button.interactable = false;
                card.button.enabled = false;
            }

            return card.view;
        }

        static CardParts CreateCard(Transform parent, int index)
        {
            var root = new GameObject($"Card{index}", typeof(RectTransform), typeof(CanvasGroup));
            root.transform.SetParent(parent, false);
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(CardWidth, CardHeight);

            var glow = CreateImage(root.transform, "Glow", Vector2.zero, new Vector2(CardWidth + 14f, CardHeight + 14f), new Color(0.3f, 0.4f, 0.55f, 0.25f));
            glow.raycastTarget = false;

            var border = CreateImage(root.transform, "Border", Vector2.zero, new Vector2(CardWidth, CardHeight), new Color(0.35f, 0.4f, 0.48f, 1f));
            var fill = CreateImage(root.transform, "Fill", Vector2.zero, new Vector2(CardWidth - 6f, CardHeight - 6f), new Color(0.1f, 0.12f, 0.16f, 0.96f));
            var stripe = CreateImage(root.transform, "TopStripe", new Vector2(0f, CardHeight * 0.5f - 16f), new Vector2(CardWidth - 6f, 8f), new Color(0.55f, 0.62f, 0.72f, 1f));

            const float textWidth = CardWidth - 76f;
            var category = CreateCenteredText(root.transform, "Category", "WEAPON", new Vector2(0f, 116f), new Vector2(textWidth, 18f), 11f, FontStyles.Bold);
            var title = CreateCenteredText(root.transform, "Label", "Offer Title", new Vector2(0f, 74f), new Vector2(textWidth, 48f), 22f, FontStyles.Bold);
            title.enableWordWrapping = true;
            title.enableAutoSizing = true;
            title.fontSizeMin = 14f;
            title.fontSizeMax = 22f;
            title.overflowMode = TextOverflowModes.Ellipsis;

            var level = CreateCenteredText(root.transform, "Level", "Lv 1 → 2", new Vector2(0f, 34f), new Vector2(textWidth, 22f), 15f, FontStyles.Normal);
            var rarityBadge = CreateImage(root.transform, "RarityBadge", new Vector2(0f, 10f), new Vector2(124f, 28f), new Color(0.55f, 0.62f, 0.72f, 1f));
            var rarity = CreateCenteredText(root.transform, "Rarity", "COMMON", new Vector2(0f, 4f), new Vector2(textWidth, 22f), 12f, FontStyles.Bold);

            var description = CreateCenteredText(root.transform, "Desc", "Description", new Vector2(0f, -78f), new Vector2(textWidth, 82f), 12f, FontStyles.Normal);
            description.alignment = TextAlignmentOptions.Top;
            description.enableWordWrapping = true;
            description.overflowMode = TextOverflowModes.Truncate;
            description.enableAutoSizing = true;
            description.fontSizeMin = 10f;
            description.fontSizeMax = 12f;

            var button = root.AddComponent<Button>();
            button.targetGraphic = border;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.1f, 1.1f, 1.1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var view = root.AddComponent<LevelUpCardView>();
            view.WireReferences(glow, border, fill, stripe, rarityBadge, category, title, level, rarity, description);

            return new CardParts
            {
                root = root,
                button = button,
                view = view,
                border = border,
                category = category,
                title = title,
                level = level,
                rarity = rarity,
                description = description
            };
        }

        static (Vector2 min, Vector2 max, Vector2 pivot) AnchorTopCenter() => (new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        static (Vector2 min, Vector2 max, Vector2 pivot) AnchorCenter() => (new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

        static TextMeshProUGUI CreateCenteredText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, FontStyles style)
            => CreateText(parent, name, text, AnchorCenter(), pos, size, fontSize, style);

        static Image CreateImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            (Vector2 min, Vector2 max, Vector2 pivot) anchor,
            Vector2 pos,
            Vector2 size,
            float fontSize,
            FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchor.min;
            rt.anchorMax = anchor.max;
            rt.pivot = anchor.pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Object.Destroy(child);
                else
                    Object.DestroyImmediate(child);
            }
        }
    }
}
