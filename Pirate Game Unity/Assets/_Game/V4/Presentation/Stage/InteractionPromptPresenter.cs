using HighSeas.V3;
using RoguePirates.V4.Stage;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Stage
{
    /// <summary>
    /// The interaction prompt (M7): port of V3InteractionController's panel. Bottom bar for landmarks, centred compact box
    /// for the Beacon hold, a large popup for the chest Take/Skip choice, with the touch USE button (held for the Beacon,
    /// tapped for chests and landmarks). Reads the interaction view and forwards the touch button; the rules are the
    /// runtime interaction model's.
    /// </summary>
    public sealed class InteractionPromptPresenter : MonoBehaviour
    {
        IInteractionPromptView _view;
        IInteractionTouch _touch;
        TextMeshProUGUI _text;
        Button _button;
        CanvasGroup _group;
        RectTransform _panel;
        RectTransform _textRect;
        PromptLayout _layout = PromptLayout.BottomBar;
        bool _built;

        public bool IsVisible => _group != null && _group.alpha > 0f;
        public string Text => _text != null ? _text.text : null;
        public PromptLayout Layout => _layout;

        public static InteractionPromptPresenter Create(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<InteractionPromptPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4InteractionPrompt", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var presenter = go.AddComponent<InteractionPromptPresenter>();
            presenter.Build(canvas);
            return presenter;
        }

        public void Bind(IInteractionPromptView view, IInteractionTouch touch)
        {
            _view = view;
            _touch = touch;
        }

        void Build(Transform canvas)
        {
            var panel = new GameObject("PLACEHOLDER_v3_ui_interaction_prompt");
            panel.transform.SetParent(transform, false);
            _panel = panel.AddComponent<RectTransform>();
            ApplyBottomAnchor();
            _panel.sizeDelta = new Vector2(520f, 72f);
            panel.AddComponent<Image>().color = new Color(0.06f, 0.1f, 0.16f, 0.92f);
            _group = panel.AddComponent<CanvasGroup>();

            var textGo = new GameObject("Prompt");
            textGo.transform.SetParent(panel.transform, false);
            _textRect = textGo.AddComponent<RectTransform>();
            _text = textGo.AddComponent<TextMeshProUGUI>();
            _text.color = V3PlaceholderFactory.UiText;
            ApplyCompactTextLayout();

            var buttonGo = new GameObject("TouchInteract");
            buttonGo.transform.SetParent(panel.transform, false);
            var buttonRect = buttonGo.AddComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0.5f);
            buttonRect.anchorMax = new Vector2(1f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(-60f, 0f);
            buttonRect.sizeDelta = new Vector2(96f, 48f);
            _button = buttonGo.AddComponent<Button>();
            var buttonImage = buttonGo.AddComponent<Image>();
            buttonImage.color = V3PlaceholderFactory.UiAccent;
            _button.targetGraphic = buttonImage;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(buttonGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = "USE";
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 20;

            var trigger = buttonGo.AddComponent<EventTrigger>();
            AddPointerEvent(trigger, EventTriggerType.PointerDown, _ => SetTouchHolding(true));
            AddPointerEvent(trigger, EventTriggerType.PointerUp, _ => SetTouchHolding(false));
            AddPointerEvent(trigger, EventTriggerType.PointerExit, _ => SetTouchHolding(false));
            _button.onClick.AddListener(() => _touch?.TouchPressed());
            SetVisible(false);
            _built = true;
        }

        void SetTouchHolding(bool holding)
        {
            if (_touch != null) _touch.TouchHolding = holding;
        }

        static void AddPointerEvent(EventTrigger trigger, EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> callback)
        {
            var entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(callback);
            trigger.triggers.Add(entry);
        }

        void LateUpdate()
        {
            if (!_built || _view == null) return;
            if (_view.Layout != _layout)
            {
                _layout = _view.Layout;
                switch (_layout)
                {
                    case PromptLayout.SalvageChestPopup: ApplyChestPopupAnchor(); break;
                    case PromptLayout.CenterCompact: ApplyCenterAnchor(); break;
                    default: ApplyBottomAnchor(); break;
                }
            }
            SetVisible(_view.Visible);
            if (_view.Visible && _text.text != _view.Text) _text.text = _view.Text;
        }

        void SetVisible(bool visible)
        {
            if (_group == null) return;
            _group.alpha = visible ? 1f : 0f;
            _group.blocksRaycasts = visible;
            _group.interactable = visible;
        }

        void ApplyBottomAnchor()
        {
            _panel.anchorMin = new Vector2(0.5f, 0f);
            _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.anchoredPosition = new Vector2(0f, 150f);
            _panel.sizeDelta = new Vector2(520f, 72f);
            ApplyCompactTextLayout();
            PlaceButtonRight();
        }

        void ApplyCenterAnchor()
        {
            _panel.anchorMin = new Vector2(0.5f, 0.5f);
            _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = new Vector2(560f, 88f);
            ApplyCompactTextLayout();
            PlaceButtonRight();
        }

        void ApplyChestPopupAnchor()
        {
            const float inset = 0.35f;
            _panel.anchorMin = new Vector2(inset, inset);
            _panel.anchorMax = new Vector2(1f - inset, 1f - inset);
            _panel.pivot = new Vector2(0.5f, 0.5f);
            _panel.anchoredPosition = Vector2.zero;
            _panel.sizeDelta = Vector2.zero;
            if (_text != null)
            {
                _textRect.anchorMin = new Vector2(0f, 0.22f);
                _textRect.anchorMax = Vector2.one;
                _textRect.offsetMin = new Vector2(28f, 12f);
                _textRect.offsetMax = new Vector2(-28f, -20f);
                _text.fontSize = 28;
                _text.alignment = TextAlignmentOptions.Center;
                _text.enableWordWrapping = true;
                _text.richText = true;
            }
            if (_button == null) return;
            _button.gameObject.SetActive(true);
            var tr = _button.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.5f, 0f);
            tr.anchorMax = new Vector2(0.5f, 0f);
            tr.pivot = new Vector2(0.5f, 0f);
            tr.anchoredPosition = new Vector2(0f, 28f);
            tr.sizeDelta = new Vector2(200f, 64f);
        }

        void PlaceButtonRight()
        {
            if (_button == null) return;
            _button.gameObject.SetActive(true);
            var tr = _button.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(1f, 0.5f);
            tr.anchorMax = new Vector2(1f, 0.5f);
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.anchoredPosition = new Vector2(-60f, 0f);
            tr.sizeDelta = new Vector2(96f, 48f);
        }

        void ApplyCompactTextLayout()
        {
            if (_text == null) return;
            _textRect.anchorMin = Vector2.zero;
            _textRect.anchorMax = Vector2.one;
            _textRect.offsetMin = new Vector2(16f, 8f);
            _textRect.offsetMax = new Vector2(-120f, -8f);
            _text.fontSize = 22;
            _text.alignment = TextAlignmentOptions.MidlineLeft;
            _text.enableWordWrapping = true;
        }
    }
}
