using RoguePirates.V4.Foundation.Platform;
using RoguePirates.V4.Views;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// V5 (owner, 1 Oct 2026): the touch button for the Captain's special ability, bottom right, drawn with the touch
    /// stick's own base and knob art; a dark radial sweep shows the cooldown and the knob glows when it is ready. Only on
    /// touch devices (<see cref="VirtualJoystick.TouchControlsWanted"/>) and while the Captain has an active. Reads only
    /// the Captain view; the press goes through <see cref="TouchAbilityButton"/>.
    /// </summary>
    public sealed class CaptainAbilityButtonPresenter : MonoBehaviour
    {
        public const float Size = 200f, Margin = 48f;
        static readonly Color ReadyKnob = new Color(1f, 0.85f, 0.45f, 1f);
        static readonly Color CoolingKnob = new Color(0.6f, 0.65f, 0.7f, 0.8f);

        IRunViews _views;
        RectTransform _rect;
        CanvasGroup _group;
        Image _base, _knob, _sweep;

        /// <summary>Shown (tests and the playable check). Hidden through a CanvasGroup, never by deactivating this
        /// object, so the presenter keeps running and the button comes back when the Captain's active does.</summary>
        public bool IsShown => _group != null && _group.alpha > 0f && isActiveAndEnabled;
        public RectTransform Pad => _rect;
        /// <summary>Share of the button still shaded by the cooldown (0 = ready).</summary>
        public float CooldownShade => _sweep != null ? _sweep.fillAmount : 0f;
        public bool HasStickArt => _base != null && _base.sprite != null;
        public TouchAbilityButton Button { get; private set; }

        public static CaptainAbilityButtonPresenter Create(Transform hudCanvas)
        {
            var existing = hudCanvas.GetComponentInChildren<CaptainAbilityButtonPresenter>(true);
            if (existing != null) return existing;
            var go = new GameObject("V4CaptainAbilityButton", typeof(RectTransform), typeof(CanvasGroup));
            // On the canvas itself (the SaltWind HUD can be switched off); the safe-area insets are applied in LateUpdate.
            go.transform.SetParent(hudCanvas, false);
            var presenter = go.AddComponent<CaptainAbilityButtonPresenter>();
            presenter.Build((RectTransform)go.transform);
            return presenter;
        }

        void Build(RectTransform rt)
        {
            _rect = rt;
            _group = rt.GetComponent<CanvasGroup>();
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.sizeDelta = new Vector2(Size, Size);
            rt.anchoredPosition = new Vector2(-Margin, Margin);
            var stick = FindFirstObjectByType<VirtualJoystick>(FindObjectsInactive.Include);

            _base = Layer("Base", rt, 1f);
            _base.sprite = stick != null ? stick.BaseSprite : null;
            _base.color = new Color(1f, 1f, 1f, 0.85f);
            _base.preserveAspect = true;
            _base.raycastTarget = true; // the touch area

            _knob = Layer("Knob", rt, 0.45f);
            _knob.sprite = stick != null ? stick.KnobSprite : null;
            _knob.preserveAspect = true;

            _sweep = Layer("Cooldown", rt, 0.92f);
            _sweep.sprite = _base.sprite;
            _sweep.color = new Color(0f, 0f, 0f, 0.55f);
            _sweep.type = Image.Type.Filled;
            _sweep.fillMethod = Image.FillMethod.Radial360;
            _sweep.fillOrigin = (int)Image.Origin360.Top;
            _sweep.fillClockwise = false;
            _sweep.preserveAspect = true;

            Button = rt.gameObject.AddComponent<TouchAbilityButton>();
        }

        Image Layer(string name, RectTransform parent, float scale)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(Size * scale, Size * scale);
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        public void Bind(IRunViews views) => _views = views;

        Canvas _canvas;
        RectTransform _loadout;
        bool _loadoutSearched;
        static readonly Vector3[] Corners = new Vector3[4];
        const float GapAboveLoadout = 16f;

        /// <summary>
        /// Just above the SaltWind HUD's weapon grid (bottom right) when it is shown, right-aligned with it; otherwise the
        /// corner, at least the margin from the screen edge plus the safe-area inset (D9: notch, cutout, rounded corners).
        /// </summary>
        void FitSafeArea()
        {
            if (!_loadoutSearched)
            {
                _loadoutSearched = true;
                foreach (var t in transform.parent.GetComponentsInChildren<RectTransform>(true))
                    if (t.name == "WeaponLoadout") { _loadout = t; break; }
            }
            if (_loadout != null && _loadout.gameObject.activeInHierarchy)
            {
                _loadout.GetWorldCorners(Corners); // 0 bottom-left, 1 top-left, 2 top-right, 3 bottom-right
                var parent = (RectTransform)_rect.parent;
                var topRight = parent.InverseTransformPoint(Corners[2]);
                var pivotOffset = new Vector2((0.5f - parent.pivot.x) * parent.rect.width, (0.5f - parent.pivot.y) * parent.rect.height);
                if (_rect.anchorMin != new Vector2(0.5f, 0.5f)) _rect.anchorMin = _rect.anchorMax = new Vector2(0.5f, 0.5f);
                _rect.anchoredPosition = (Vector2)topRight - pivotOffset + new Vector2(0f, GapAboveLoadout);
                return;
            }
            if (_rect.anchorMin != new Vector2(1f, 0f)) _rect.anchorMin = _rect.anchorMax = new Vector2(1f, 0f);
            var safe = Screen.safeArea;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            var scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            var right = Mathf.Max(0f, Screen.width - safe.xMax) / scale;
            var bottom = Mathf.Max(0f, safe.yMin) / scale;
            _rect.anchoredPosition = new Vector2(-(Margin + right), Margin + bottom);
        }

        void LateUpdate()
        {
            var captain = _views?.Captain;
            var show = VirtualJoystick.TouchControlsWanted && captain != null && captain.ActiveAvailable;
            if (IsShown != show)
            {
                _group.alpha = show ? 1f : 0f;
                _group.blocksRaycasts = show;
                _group.interactable = show;
            }
            if (!show) return;
            FitSafeArea();
            var duration = Mathf.Max(0.01f, captain.ActiveCooldownDuration);
            var remaining = Mathf.Max(0f, captain.ActiveCooldownRemaining);
            var ready = remaining <= 0f;
            _sweep.fillAmount = ready ? 0f : remaining / duration;
            _knob.color = ready ? Color.Lerp(ReadyKnob, Color.white, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f)) : CoolingKnob;
        }
    }
}
