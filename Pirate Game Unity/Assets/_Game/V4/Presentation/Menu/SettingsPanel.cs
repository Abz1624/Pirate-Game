using System;
using RoguePirates.V4.Foundation.Platform;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Menu
{
    /// <summary>
    /// V5 M7: the settings screen, shared by the main menu and the pause menu: UI scale, music and effects volume, quality
    /// (automatic or a tier), reduced effects, screen shake, the stick's side and dead zone. Edits a copy and saves it on
    /// Close; every control is at least 72 units tall (touch).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        public const float RowHeight = 72f, Width = 760f;
        static readonly string[] QualityNames = { "Auto", "Low", "Mid", "High" };

        GameSettings _edit;
        Action _closed;
        RectTransform _rows;
        TextMeshProUGUI _qualityLabel, _effectsLabel, _shakeLabel, _sideLabel;
        Slider _ui, _music, _sfx, _dead;

        public bool IsOpen => gameObject.activeSelf;
        public GameSettings Editing => _edit;

        public static SettingsPanel Create(Transform parent)
        {
            var go = new GameObject("V5Settings", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            // V5 M7 review: sized to the screen (anchors), the rows scroll, so every row keeps its 72 units at any UI scale.
            rt.anchorMin = new Vector2(0.5f, 0.04f);
            rt.anchorMax = new Vector2(0.5f, 0.96f);
            rt.sizeDelta = new Vector2(Width, 0f);
            go.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.16f, 0.96f);
            var panel = go.AddComponent<SettingsPanel>();
            panel.Build(rt);
            go.SetActive(false);
            return panel;
        }

        public void Open(Action closed = null)
        {
            _closed = closed;
            _edit = GameSettings.Current.Copy();
            Refresh();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Close()
        {
            _edit?.Save();
            gameObject.SetActive(false);
            _closed?.Invoke();
        }

        void Build(RectTransform rt)
        {
            var scroll = new GameObject("Scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image), typeof(RectMask2D));
            scroll.transform.SetParent(rt, false);
            var srt = (RectTransform)scroll.transform;
            srt.anchorMin = Vector2.zero;
            srt.anchorMax = Vector2.one;
            srt.offsetMin = new Vector2(0f, RowHeight + 32f); // above the Close button
            srt.offsetMax = Vector2.zero;
            scroll.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            var content = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(scroll.transform, false);
            _rows = (RectTransform)content.transform;
            _rows.anchorMin = new Vector2(0f, 1f);
            _rows.anchorMax = new Vector2(1f, 1f);
            _rows.pivot = new Vector2(0.5f, 1f);
            var layout = content.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(32, 32, 24, 24);
            layout.spacing = 8f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var sr = scroll.GetComponent<ScrollRect>();
            sr.content = _rows;
            sr.horizontal = false;
            Label("Settings", 40, FontStyles.Bold);
            _ui = SliderRow("UI scale", GameSettings.MinUiScale, GameSettings.MaxUiScale, v => _edit.uiScale = v);
            _music = SliderRow("Music", 0f, 1f, v => _edit.musicVolume = v);
            _sfx = SliderRow("Effects volume", 0f, 1f, v => _edit.sfxVolume = v);
            _qualityLabel = CycleRow("Quality", () => _edit.quality = _edit.quality >= 2 ? -1 : _edit.quality + 1);
            _effectsLabel = CycleRow("Reduced effects", () => _edit.reducedEffects = !_edit.reducedEffects);
            _shakeLabel = CycleRow("Screen shake", () => _edit.screenShake = !_edit.screenShake);
            _sideLabel = CycleRow("Stick side", () => _edit.stickOnRight = !_edit.stickOnRight);
            _dead = SliderRow("Stick dead zone", 0.05f, 0.4f, v => _edit.stickDeadZone = v);
            var close = MakeButton(rt, "Close", Close);
            var crt = (RectTransform)close.transform;
            close.GetComponent<LayoutElement>().ignoreLayout = true;
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.sizeDelta = new Vector2(320f, RowHeight);
            crt.anchoredPosition = new Vector2(0f, 16f);
        }

        void Refresh()
        {
            if (_edit == null) return;
            _ui.SetValueWithoutNotify(_edit.uiScale);
            _music.SetValueWithoutNotify(_edit.musicVolume);
            _sfx.SetValueWithoutNotify(_edit.sfxVolume);
            _dead.SetValueWithoutNotify(_edit.stickDeadZone);
            _qualityLabel.text = QualityNames[_edit.quality + 1];
            _effectsLabel.text = _edit.reducedEffects ? "On" : "Off";
            _shakeLabel.text = _edit.screenShake ? "On" : "Off";
            _sideLabel.text = _edit.stickOnRight ? "Right" : "Left";
        }

        TextMeshProUGUI Label(string text, float size, FontStyles style = FontStyles.Normal, Transform parent = null)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent != null ? parent : _rows, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = new Color(0.95f, 0.93f, 0.88f, 1f);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            var tle = go.GetComponent<LayoutElement>();
            tle.preferredHeight = RowHeight;
            tle.minHeight = RowHeight;
            return t;
        }

        RectTransform Row(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(_rows, false);
            go.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            go.GetComponent<LayoutElement>().minHeight = RowHeight;
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 16f;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = true;
            return (RectTransform)go.transform;
        }

        Slider SliderRow(string name, float min, float max, Action<float> set)
        {
            var row = Row(name);
            Label(name, 28, FontStyles.Normal, row);
            var go = new GameObject("Slider", typeof(RectTransform), typeof(Slider), typeof(LayoutElement));
            go.transform.SetParent(row, false);
            go.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            var background = Child(go.transform, "Background", new Color(0.2f, 0.25f, 0.32f, 1f), new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            var fillArea = Child(go.transform, "Fill Area", Color.clear, new Vector2(0f, 0.4f), new Vector2(1f, 0.6f));
            var fill = Child(fillArea, "Fill", new Color(0.85f, 0.65f, 0.2f, 1f), Vector2.zero, Vector2.one);
            var handleArea = Child(go.transform, "Handle Slide Area", Color.clear, Vector2.zero, Vector2.one);
            var handle = Child(handleArea, "Handle", new Color(0.95f, 0.93f, 0.88f, 1f), new Vector2(0f, 0f), new Vector2(0f, 1f));
            handle.sizeDelta = new Vector2(48f, 0f);
            var slider = go.GetComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.onValueChanged.AddListener(v => set(v));
            background.GetComponent<Image>().raycastTarget = true;
            return slider;
        }

        static RectTransform Child(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = color.a > 0f;
            return rt;
        }

        TextMeshProUGUI CycleRow(string name, Action change)
        {
            var row = Row(name);
            Label(name, 28, FontStyles.Normal, row);
            var button = MakeButton(row, "-", () => { change(); Refresh(); });
            var label = button.GetComponentInChildren<TextMeshProUGUI>();
            return label;
        }

        void Button(string text, Action click) => MakeButton(_rows, text, click);

        static Button MakeButton(Transform parent, string text, Action click)
        {
            var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            go.GetComponent<LayoutElement>().preferredHeight = RowHeight;
            go.GetComponent<LayoutElement>().minHeight = RowHeight;
            go.GetComponent<LayoutElement>().minWidth = 200f;
            go.GetComponent<Image>().color = new Color(0.85f, 0.65f, 0.2f, 1f);
            var b = go.GetComponent<Button>();
            b.onClick.AddListener(() => click());
            var labelGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)labelGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = labelGo.GetComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 28;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.08f, 0.06f, 0.04f, 1f);
            return b;
        }

        /// <summary>A button with a label (other menus reuse the look; 72 units tall).</summary>
        public static Button CreateButton(Transform parent, string text, Action click) => MakeButton(parent, text, click);
    }
}
