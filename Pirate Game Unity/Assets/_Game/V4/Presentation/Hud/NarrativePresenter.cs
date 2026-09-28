using RoguePirates.V4.Narrative;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Barks and tutorial prompts (M13). A bark is a small caption above the loadout — the speaker's name over the line —
    /// that fades after a few seconds; a tutorial prompt is a strip under the timer. Neither takes input or blocks the
    /// view; both have their own place, so they never fight the one-message toast. Reads only the narrative view.
    /// </summary>
    public sealed class NarrativePresenter : MonoBehaviour
    {
        const float FadeSeconds = 0.5f;

        INarrativeView _view;
        RectTransform _bark, _prompt;
        TextMeshProUGUI _speaker, _line, _promptText;
        Image _barkBack, _promptBack;
        int _seenBark, _seenPrompt;
        float _barkTimer, _promptTimer;

        public bool BarkShown => _bark != null && _bark.gameObject.activeSelf;
        public string BarkText => _speaker != null ? _speaker.text + ": " + _line.text : string.Empty;
        public bool PromptShown => _prompt != null && _prompt.gameObject.activeSelf;
        public string PromptText => _promptText != null ? _promptText.text : string.Empty;

        public static NarrativePresenter Create(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<NarrativePresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4Narrative", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var p = go.AddComponent<NarrativePresenter>();
            p.Build((RectTransform)go.transform);
            return p;
        }

        void Build(RectTransform root)
        {
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;

            _bark = Panel(root, "Bark", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 150f), new Vector2(540f, 78f), out _barkBack);
            _speaker = Text(_bark, "Speaker", 20f, FontStyles.Bold, new Vector2(14f, -8f), new Color(1f, 0.82f, 0.4f), TextAlignmentOptions.Left);
            _line = Text(_bark, "Line", 20f, FontStyles.Normal, new Vector2(14f, -36f), Color.white, TextAlignmentOptions.Left);
            _bark.gameObject.SetActive(false);

            _prompt = Panel(root, "TutorialPrompt", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(860f, 64f), out _promptBack);
            _promptText = Text(_prompt, "Prompt", 24f, FontStyles.Bold, new Vector2(0f, -14f), Color.white, TextAlignmentOptions.Center);
            _prompt.gameObject.SetActive(false);
        }

        static RectTransform Panel(RectTransform root, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, out Image back)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(root, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(anchor.x, anchor.y);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            back = rt.gameObject.AddComponent<Image>();
            back.color = new Color(0.04f, 0.08f, 0.12f, 0.72f);
            back.raycastTarget = false;
            return rt;
        }

        static TextMeshProUGUI Text(RectTransform parent, string name, float size, FontStyles style, Vector2 offset, Color color, TextAlignmentOptions align)
        {
            var rt = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = new Vector2(-28f, size * 1.4f);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal; // playtest round 1: long lines wrap instead of ending in "…"
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public void Bind(INarrativeView view)
        {
            _view = view;
            // Bound just after the run starts: whatever is already due (the first tutorial prompt) is shown.
            _seenBark = 0;
            _seenPrompt = 0;
        }

        void LateUpdate()
        {
            if (_view == null) return;
            if (_view.BarkSerial != _seenBark)
            {
                _seenBark = _view.BarkSerial;
                _speaker.text = _view.BarkSpeaker;
                _line.text = _view.BarkLine;
                FitHeight(_bark, _line, 36f, 12f); // speaker row + wrapped line
                _barkTimer = Mathf.Max(0.5f, _view.BarkSeconds);
                _bark.gameObject.SetActive(true);
            }
            if (_view.PromptSerial != _seenPrompt)
            {
                _seenPrompt = _view.PromptSerial;
                _promptText.text = _view.PromptText;
                FitHeight(_prompt, _promptText, 14f, 14f);
                _promptTimer = Mathf.Max(0.5f, _view.PromptSeconds);
                _prompt.gameObject.SetActive(true);
            }
            Fade(ref _barkTimer, _bark, _barkBack, _speaker, _line);
            Fade(ref _promptTimer, _prompt, _promptBack, _promptText, null);
        }

        /// <summary>Grows the panel (and the text box) to the wrapped text's height.</summary>
        static void FitHeight(RectTransform panel, TextMeshProUGUI text, float top, float bottom)
        {
            var width = panel.rect.width + ((RectTransform)text.transform).sizeDelta.x;
            var height = Mathf.Ceil(text.GetPreferredValues(text.text, Mathf.Max(50f, width), 0f).y);
            var rt = (RectTransform)text.transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, top + height + bottom);
        }

        static void Fade(ref float timer, RectTransform panel, Image back, TextMeshProUGUI a, TextMeshProUGUI b)
        {
            if (timer <= 0f) return;
            timer -= Time.unscaledDeltaTime;
            var alpha = Mathf.Clamp01(timer / FadeSeconds);
            a.alpha = alpha;
            if (b != null) b.alpha = alpha;
            var c = back.color;
            c.a = 0.72f * alpha;
            back.color = c;
            if (timer <= 0f) panel.gameObject.SetActive(false);
        }
    }
}
