using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.UI
{
    public static class RunHudTheme
    {
        public static readonly Color PanelFill = new(0.04f, 0.08f, 0.14f, 0.86f);
        public static readonly Color PanelBorder = new(0.22f, 0.34f, 0.48f, 0.95f);
        public static readonly Color Label = new(0.72f, 0.8f, 0.9f, 0.95f);
        public static readonly Color Value = new(0.94f, 0.97f, 1f, 1f);
        public static readonly Color Gold = new(1f, 0.86f, 0.38f, 1f);
        public static readonly Color HullFill = new(0.88f, 0.28f, 0.24f, 1f);
        public static readonly Color HullDanger = new(1f, 0.18f, 0.14f, 1f);
        public static readonly Color XpFill = new(0.28f, 0.78f, 0.92f, 1f);
        public static readonly Color BarBackground = new(0.08f, 0.1f, 0.14f, 0.92f);
        public static readonly Color SlotFill = new(0.06f, 0.1f, 0.16f, 0.88f);
        public static readonly Color TimerOvertime = new(1f, 0.42f, 0.36f, 1f);
    }

    public static class RunHudUiFactory
    {
        public const string RootName = "RunHudRoot";
        const float EdgeInset = 16f;
        const float PanelWidth = 248f;
        const float PanelHeight = 108f;
        const float TimerWidth = 176f;
        const float TimerHeight = 68f;
        const float StripWidth = 500f;

        static readonly string[] LegacyElementNames =
        {
            "HullText", "XpText", "TimerText", "GoldText", "LevelText", "HullSlider", "XpSlider"
        };

        public struct HudWiring
        {
            public TextMeshProUGUI hullLabel;
            public TextMeshProUGUI hullText;
            public TextMeshProUGUI xpLabel;
            public TextMeshProUGUI xpText;
            public TextMeshProUGUI timerText;
            public TextMeshProUGUI timerCaption;
            public TextMeshProUGUI goldText;
            public TextMeshProUGUI levelText;
            public Slider hullSlider;
            public Slider xpSlider;
            public Image hullFill;
            public Image xpFill;
            public WeaponLevelStripHud weaponStrip;
        }

        public static HudWiring Rebuild(Transform canvas)
        {
            UiCanvasConfigurator.Configure(canvas.GetComponent<Canvas>());

            RemoveLegacyElements(canvas);

            var strips = canvas.GetComponentsInChildren<WeaponLevelStripHud>(true);
            for (var i = 0; i < strips.Length; i++)
            {
                if (Application.isPlaying) Object.Destroy(strips[i].gameObject);
                else Object.DestroyImmediate(strips[i].gameObject);
            }

            var root = canvas.Find(RootName);
            if (root == null)
            {
                var rootGo = new GameObject(RootName, typeof(RectTransform));
                rootGo.transform.SetParent(canvas, false);
                root = rootGo.transform;
            }

            ClearChildren(root);
            Stretch(root as RectTransform);

            var wiring = new HudWiring();
            BuildTopLeftPanel(root, ref wiring);
            BuildTopCenterTimer(root, ref wiring);
            BuildTopRightPanel(root, ref wiring);
            wiring.weaponStrip = BuildWeaponStrip(root);
            return wiring;
        }

        static void BuildTopLeftPanel(Transform root, ref HudWiring wiring)
        {
            var panel = CreatePanel(root, "LeftHudPanel", AnchorTopLeft(), new Vector2(EdgeInset, -EdgeInset), new Vector2(PanelWidth, PanelHeight));
            CreateStatRow(panel, "HullRow", -8f, 46f, "HULL", "100 / 100",
                out wiring.hullLabel, out wiring.hullText, out wiring.hullSlider, out wiring.hullFill, RunHudTheme.HullFill);
            CreateStatRow(panel, "XpRow", -56f, 46f, "EXP", "0 / 100",
                out wiring.xpLabel, out wiring.xpText, out wiring.xpSlider, out wiring.xpFill, RunHudTheme.XpFill);
        }

        static void BuildTopCenterTimer(Transform root, ref HudWiring wiring)
        {
            var panel = CreatePanel(root, "TimerPanel", AnchorTopCenter(), new Vector2(0f, -EdgeInset), new Vector2(TimerWidth, TimerHeight));
            panel.GetComponent<Image>().color = new Color(RunHudTheme.PanelFill.r, RunHudTheme.PanelFill.g, RunHudTheme.PanelFill.b, 0.72f);

            wiring.timerCaption = CreateText(panel, "TimerCaption", "SURVIVE", new Vector2(0f, 16f), new Vector2(TimerWidth - 16f, 20f), 12f, FontStyles.Bold);
            wiring.timerCaption.color = RunHudTheme.Label;
            wiring.timerText = CreateText(panel, "TimerText", "00:00", new Vector2(0f, -12f), new Vector2(TimerWidth - 16f, 36f), 30f, FontStyles.Bold);
            wiring.timerText.color = RunHudTheme.Value;
            wiring.timerText.enableAutoSizing = true;
            wiring.timerText.fontSizeMin = 22f;
            wiring.timerText.fontSizeMax = 30f;
        }

        static void BuildTopRightPanel(Transform root, ref HudWiring wiring)
        {
            var panel = CreatePanel(root, "RightHudPanel", AnchorTopRight(), new Vector2(-EdgeInset, -EdgeInset), new Vector2(PanelWidth, PanelHeight));

            wiring.goldText = CreateText(panel, "GoldValue", "0", new Vector2(0f, -10f), new Vector2(PanelWidth - 24f, 28f), 22f, FontStyles.Bold);
            wiring.goldText.alignment = TextAlignmentOptions.MidlineRight;
            wiring.goldText.color = RunHudTheme.Gold;
            wiring.goldText.enableAutoSizing = true;
            wiring.goldText.fontSizeMin = 16f;
            wiring.goldText.fontSizeMax = 22f;
            wiring.goldText.overflowMode = TextOverflowModes.Ellipsis;

            var goldCaption = CreateText(panel, "GoldCaption", "GOLD", new Vector2(0f, -10f), new Vector2(PanelWidth - 24f, 18f), 12f, FontStyles.Bold);
            goldCaption.alignment = TextAlignmentOptions.MidlineLeft;
            goldCaption.color = RunHudTheme.Label;

            var levelBadge = CreateImage(panel, "LevelBadge", new Vector2(0f, -62f), new Vector2(136f, 40f), new Color(0.1f, 0.14f, 0.2f, 0.95f));
            var levelBorder = CreateImage(levelBadge.transform, "LevelBadgeBorder", Vector2.zero, new Vector2(140f, 44f), RunHudTheme.Gold);
            levelBorder.transform.SetAsFirstSibling();

            wiring.levelText = CreateText(levelBadge.transform, "LevelValue", "LV 1", Vector2.zero, new Vector2(120f, 32f), 20f, FontStyles.Bold);
            wiring.levelText.color = RunHudTheme.Value;
        }

        static WeaponLevelStripHud BuildWeaponStrip(Transform root)
        {
            var stripGo = new GameObject("WeaponLevelStrip", typeof(RectTransform));
            stripGo.transform.SetParent(root, false);
            var rt = stripGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 20f);
            rt.sizeDelta = new Vector2(StripWidth, 52f);

            var fitter = stripGo.AddComponent<UiScaleToFitParent>();
            fitter.SetContentWidth(StripWidth);

            var layout = stripGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);

            var strip = stripGo.AddComponent<WeaponLevelStripHud>();
            strip.BuildSlots(stripGo.transform);
            return strip;
        }

        static void CreateStatRow(
            Transform panel,
            string rowName,
            float topOffset,
            float rowHeight,
            string label,
            string value,
            out TextMeshProUGUI labelText,
            out TextMeshProUGUI valueText,
            out Slider slider,
            out Image fillImage,
            Color fillColor)
        {
            var row = new GameObject(rowName, typeof(RectTransform));
            row.transform.SetParent(panel, false);
            var rowRt = row.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0f, 1f);
            rowRt.anchorMax = new Vector2(1f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, topOffset);
            rowRt.sizeDelta = new Vector2(-20f, rowHeight);

            labelText = CreateText(row.transform, "Label", label, new Vector2(-4f, 10f), new Vector2(56f, 18f), 12f, FontStyles.Bold);
            labelText.alignment = TextAlignmentOptions.MidlineLeft;
            labelText.color = RunHudTheme.Label;

            valueText = CreateText(row.transform, "Value", value, new Vector2(-4f, 10f), new Vector2(120f, 18f), 14f, FontStyles.Bold);
            valueText.alignment = TextAlignmentOptions.MidlineRight;
            valueText.color = RunHudTheme.Value;
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 11f;
            valueText.fontSizeMax = 14f;
            valueText.overflowMode = TextOverflowModes.Ellipsis;
            var valueRt = valueText.rectTransform;
            valueRt.anchorMin = new Vector2(1f, 1f);
            valueRt.anchorMax = new Vector2(1f, 1f);
            valueRt.pivot = new Vector2(1f, 1f);
            valueRt.anchoredPosition = new Vector2(-4f, 10f);

            slider = CreateBar(row.transform, "Bar", new Vector2(0f, -16f), new Vector2(0f, 14f), fillColor, out fillImage);
            var barRt = slider.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0f, 0f);
            barRt.anchorMax = new Vector2(1f, 0f);
            barRt.pivot = new Vector2(0.5f, 0f);
            barRt.anchoredPosition = new Vector2(0f, 2f);
            barRt.sizeDelta = new Vector2(0f, 14f);
        }

        static (Vector2 min, Vector2 max, Vector2 pivot) AnchorTopLeft() => (new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f));
        static (Vector2 min, Vector2 max, Vector2 pivot) AnchorTopCenter() => (new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        static (Vector2 min, Vector2 max, Vector2 pivot) AnchorTopRight() => (new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));

        static Transform CreatePanel(Transform parent, string name, (Vector2 min, Vector2 max, Vector2 pivot) anchor, Vector2 pos, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = anchor.min;
            rt.anchorMax = anchor.max;
            rt.pivot = anchor.pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = panel.GetComponent<Image>();
            img.color = RunHudTheme.PanelFill;
            img.raycastTarget = false;

            var border = CreateImage(panel.transform, "Border", Vector2.zero, new Vector2(size.x + 4f, size.y + 4f), RunHudTheme.PanelBorder);
            border.transform.SetAsFirstSibling();
            border.raycastTarget = false;
            return panel.transform;
        }

        static Slider CreateBar(Transform parent, string name, Vector2 pos, Vector2 size, Color fillColor, out Image fillImage)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Slider));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            CreateImage(go.transform, "Background", Vector2.zero, size, RunHudTheme.BarBackground);
            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(go.transform, false);
            var fillAreaRt = fillArea.GetComponent<RectTransform>();
            Stretch(fillAreaRt);
            fillAreaRt.offsetMin = new Vector2(2f, 2f);
            fillAreaRt.offsetMax = new Vector2(-2f, -2f);

            var fill = CreateImage(fillArea.transform, "Fill", Vector2.zero, Vector2.zero, fillColor);
            var fillRt = fill.rectTransform;
            Stretch(fillRt);

            var slider = go.GetComponent<Slider>();
            slider.fillRect = fillRt;
            slider.targetGraphic = fill;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            fillImage = fill;
            return slider;
        }

        static Image CreateImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI CreateText(Transform parent, string name, string text, Vector2 pos, Vector2 size, float fontSize, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            return tmp;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        static void RemoveLegacyElements(Transform canvas)
        {
            for (var i = 0; i < LegacyElementNames.Length; i++)
            {
                var child = canvas.Find(LegacyElementNames[i]);
                if (child == null) continue;
                if (Application.isPlaying) Object.Destroy(child.gameObject);
                else Object.DestroyImmediate(child.gameObject);
            }
        }

        static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
        }
    }
}
