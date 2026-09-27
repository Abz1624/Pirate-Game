using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The Captain's active on the HUD (M9 playtest): a small bar beside the boost meter with the active's name, a fill
    /// that refills over the cooldown, and its key when ready (SPACE, or the gamepad button; else the seconds left). Hidden when actives are off
    /// (the v3-parity preset) or the Captain has none. Reads only the Captain view.
    /// </summary>
    public sealed class CaptainActivePresenter : MonoBehaviour
    {
        const float Width = 190f, Height = 28f, Gap = 12f;
        static readonly Color ReadyColor = new Color(0.95f, 0.72f, 0.25f, 0.95f);
        static readonly Color CoolingColor = new Color(0.45f, 0.55f, 0.65f, 0.9f);

        IRunViews _views;
        RectTransform _rect;
        Image _fill;
        TextMeshProUGUI _label;
        RectTransform _boost;
        int _lastSeconds = int.MinValue;
        string _lastKey, _lastName; // the label is rebuilt only when these change

        /// <summary>Shown and ready (tests and the playable check).</summary>
        public bool IsShown => _rect != null && _rect.gameObject.activeSelf && _views?.Captain != null && _views.Captain.ActiveAvailable;
        public string Text => _label != null ? _label.text : string.Empty;
        public float Fill => _fill != null ? _fill.fillAmount : 0f;

        public static CaptainActivePresenter Create(Transform hudCanvas)
        {
            var existing = hudCanvas.GetComponentInChildren<CaptainActivePresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4CaptainActive", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(hudCanvas, false);
            var presenter = go.AddComponent<CaptainActivePresenter>();
            presenter.Build((RectTransform)go.transform);
            return presenter;
        }

        void Build(RectTransform rt)
        {
            _rect = rt;
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.sizeDelta = new Vector2(Width, Height);
            rt.anchoredPosition = new Vector2(24f + 180f + Gap, 24f);
            var bg = rt.GetComponent<Image>();
            bg.color = new Color(0.06f, 0.1f, 0.16f, 0.75f);
            bg.raycastTarget = false;

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(rt, false);
            var frt = (RectTransform)fillGo.transform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = new Vector2(2f, 2f);
            frt.offsetMax = new Vector2(-2f, -2f);
            _fill = fillGo.GetComponent<Image>();
            _fill.sprite = MakeWhiteSprite();
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = 0;
            _fill.raycastTarget = false;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(rt, false);
            var trt = (RectTransform)textGo.transform;
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(8f, 0f);
            trt.offsetMax = new Vector2(-8f, 0f);
            _label = textGo.AddComponent<TextMeshProUGUI>();
            _label.fontSize = 15f;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.color = Color.white;
            _label.raycastTarget = false;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.overflowMode = TextOverflowModes.Ellipsis;
        }

        static Sprite _white; // made once (a filled Image needs a sprite)

        static Sprite MakeWhiteSprite()
        {
            if (_white != null) return _white;
            var tex = Texture2D.whiteTexture;
            _white = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            return _white;
        }

        public void Bind(IRunViews views) => _views = views;

        void LateUpdate()
        {
            var captain = _views?.Captain;
            var show = captain != null && captain.ActiveAvailable;
            if (_rect.gameObject.activeSelf != show) _rect.gameObject.SetActive(show);
            if (!show) return;
            PlaceBesideBoost();

            var duration = Mathf.Max(0.01f, captain.ActiveCooldownDuration);
            var remaining = Mathf.Max(0f, captain.ActiveCooldownRemaining);
            var ready = remaining <= 0f;
            _fill.fillAmount = ready ? 1f : 1f - remaining / duration;
            _fill.color = ready ? ReadyColor : CoolingColor;
            var seconds = ready ? -1 : Mathf.CeilToInt(remaining);
            var key = captain.ActiveKeyHint;
            if (seconds != _lastSeconds || key != _lastKey || captain.ActiveName != _lastName)
            {
                _lastSeconds = seconds;
                _lastKey = key;
                _lastName = captain.ActiveName;
                _label.text = ready ? $"{_lastName}  ·  {key}" : $"{_lastName}  ·  {seconds}s";
            }
        }

        /// <summary>Just right of the boost meter (the placeholder meter moves above the weapon row, R-91).</summary>
        void PlaceBesideBoost()
        {
            if (_boost == null)
            {
                var t = transform.parent != null ? transform.parent.Find("Boost_Meter") : null;
                _boost = t as RectTransform;
                if (_boost == null) return;
            }
            if (_boost.anchorMin != Vector2.zero || _boost.anchorMax != Vector2.zero) return; // an authored meter elsewhere: keep the default place
            var scale = _boost.localScale;
            var pos = _boost.anchoredPosition + new Vector2((1f - _boost.pivot.x) * _boost.rect.width * scale.x + Gap, -_boost.pivot.y * _boost.rect.height * scale.y);
            var height = _boost.rect.height * _boost.localScale.y;
            _rect.sizeDelta = new Vector2(Width, Mathf.Max(Height, height));
            if ((_rect.anchoredPosition - pos).sqrMagnitude > 0.25f) _rect.anchoredPosition = pos;
        }
    }
}
