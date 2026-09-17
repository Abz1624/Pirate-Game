using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HighSeas.V3
{
    public sealed class V3MainMenuCharacterSelect : MonoBehaviour
    {
        const int LoopCopies = 3;
        const float CardSizeMultiplier = 1.035f; // +15% then -10%
        static readonly float ScrollCardHeight = 520f * CardSizeMultiplier;
        static readonly float CardBaseWidth = 300f * CardSizeMultiplier;

        [Tooltip("Horizontal distance between card centers in the carousel.")]
        public float cardSpacing = 250f;
        const float SelectedScale = 1.28f * CardSizeMultiplier;
        const float SideScale = 0.78f * CardSizeMultiplier;
        [Tooltip("How quickly scroll art blends into selected art near center (higher = wider blend).")]
        public float highlightBlendDistance = 1f;
        [Tooltip("How fast card art crossfade follows carousel focus (lower = softer, less noticeable).")]
        public float artBlendSmoothing = 14f;
        const float SnapSpeed = 14f;

        public event Action<V3CaptainId> CaptainChanged;
        public event Action PlayRequested;

        public Button PlayButton { get; private set; }

        ScrollRect _scrollRect;
        RectTransform _content;
        RectTransform _viewport;
        V3CharacterCardArtSettings _art;
        readonly List<CardSlot> _slots = new();
        int _logicalIndex;
        V3CaptainId _lastNotifiedCaptain = (V3CaptainId)(-1);
        float _targetContentX;
        float _scrollBaseOffset;
        bool _dragging;
        bool _layoutReady;

        int CaptainCount => V3CaptainCatalog.MenuOrder.Length;

        struct CardSlot
        {
            public V3CaptainId captain;
            public RectTransform root;
            public Image scrollImage;
            public Image selectedImage;
            public Image lockOverlay;
            public TextMeshProUGUI placeholderLabel;
            public float artBlend;
        }

        public static V3MainMenuCharacterSelect Ensure(Transform canvasRoot)
        {
            var existing = canvasRoot.GetComponentInChildren<V3MainMenuCharacterSelect>(true);
            if (existing != null)
                return existing;

            var root = new GameObject("v3_ui_character_select", typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root.AddComponent<V3MainMenuCharacterSelect>();
        }

        void Awake()
        {
            _art = V3CharacterCardArtSettings.Load();
            BuildUi();
        }

        void BuildUi()
        {
            var bg = CreateStretchImage(transform, "background", Color.black);
            if (_art != null && _art.menuBackground != null)
            {
                bg.sprite = _art.menuBackground;
                bg.color = Color.white;
                bg.type = Image.Type.Simple;
                bg.preserveAspect = false;
            }
            else
            {
                bg.color = new Color(0.06f, 0.08f, 0.12f, 1f);
            }

            var header = CreateTmp(transform, "header", "SELECT YOUR CAPTAIN", 52, FontStyles.Bold);
            var headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -48f);
            headerRect.sizeDelta = new Vector2(900f, 72f);
            header.alignment = TextAlignmentOptions.Center;
            header.color = new Color(0.95f, 0.93f, 0.88f, 1f);

            var scrollGo = new GameObject("carousel_scroll", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGo.transform.SetParent(transform, false);
            var scrollRectTr = scrollGo.GetComponent<RectTransform>();
            scrollRectTr.anchorMin = new Vector2(0f, 0.18f);
            scrollRectTr.anchorMax = new Vector2(1f, 0.88f);
            scrollRectTr.offsetMin = Vector2.zero;
            scrollRectTr.offsetMax = Vector2.zero;
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            scrollGo.GetComponent<Image>().raycastTarget = true;

            _scrollRect = scrollGo.GetComponent<ScrollRect>();
            _scrollRect.horizontal = true;
            _scrollRect.vertical = false;
            _scrollRect.movementType = ScrollRect.MovementType.Unrestricted;
            _scrollRect.scrollSensitivity = 28f;
            _scrollRect.inertia = true;
            _scrollRect.decelerationRate = 0.12f;

            var viewportGo = new GameObject("viewport", typeof(RectTransform), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            _viewport = viewportGo.GetComponent<RectTransform>();
            Stretch(_viewport);
            _scrollRect.viewport = _viewport;

            var contentGo = new GameObject("content", typeof(RectTransform));
            contentGo.transform.SetParent(viewportGo.transform, false);
            _content = contentGo.GetComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 0.5f);
            _content.anchorMax = new Vector2(0f, 0.5f);
            _content.pivot = new Vector2(0f, 0.5f);
            _content.anchoredPosition = Vector2.zero;
            _scrollRect.content = _content;

            var order = V3CaptainCatalog.MenuOrder;
            var totalCards = order.Length * LoopCopies;
            for (var i = 0; i < totalCards; i++)
                _slots.Add(CreateCard(_content, order[i % order.Length]));

            PlayButton = CreatePlayButton();
            _scrollRect.onValueChanged.AddListener(_ =>
            {
                if (_dragging)
                {
                    WrapInfiniteStrip();
                    UpdateCardVisuals();
                }
            });
            scrollGo.AddComponent<CharacterSelectDragRelay>().Bind(this);
        }

        void Start()
        {
            if (_art == null)
                _art = V3CharacterCardArtSettings.Load();

            Canvas.ForceUpdateCanvases();
            RebuildCarouselLayout();
            SetCaptain(V3CaptainSelection.Selected, true);
        }

        void RebuildCarouselLayout()
        {
            if (_viewport == null || _slots.Count == 0)
                return;

            var pad = Mathf.Max(420f, _viewport.rect.width * 0.5f);
            _scrollBaseOffset = pad - _viewport.rect.width * 0.5f;
            var step = Mathf.Max(120f, cardSpacing);
            _content.sizeDelta = new Vector2(pad * 2f + step * (_slots.Count - 1), ScrollCardHeight + 80f);

            for (var i = 0; i < _slots.Count; i++)
                _slots[i].root.anchoredPosition = new Vector2(pad + i * step, 0f);

            _layoutReady = true;
        }

        internal void NotifyDragBegin() => _dragging = true;

        internal void NotifyDragEnd()
        {
            _dragging = false;
            WrapInfiniteStrip();
            UpdateFocusFromScrollPosition(true);
        }

        CardSlot CreateCard(RectTransform parent, V3CaptainId captain)
        {
            var go = new GameObject($"card_{captain}", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(CardBaseWidth, ScrollCardHeight);

            var artStack = new GameObject("art_stack", typeof(RectTransform));
            artStack.transform.SetParent(go.transform, false);
            Stretch(artStack.GetComponent<RectTransform>());

            var scrollImage = CreateCardArtLayer(artStack.transform, "scroll_art");
            var selectedImage = CreateCardArtLayer(artStack.transform, "selected_art");
            selectedImage.gameObject.SetActive(true);
            ApplyCardVisuals(scrollImage, selectedImage, captain, 0f, null);

            var labelGo = new GameObject("placeholder_label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            Stretch(labelRect);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = V3CaptainCatalog.GetMenuLabel(captain).ToUpperInvariant();
            label.fontSize = 36;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 1f, 1f, 0.35f);
            label.raycastTarget = false;
            label.enabled = !HasCardArt(captain);

            var lockGo = new GameObject("lock_overlay", typeof(RectTransform), typeof(Image));
            lockGo.transform.SetParent(go.transform, false);
            var lockRect = lockGo.GetComponent<RectTransform>();
            Stretch(lockRect);
            var lockImg = lockGo.GetComponent<Image>();
            lockImg.color = new Color(0f, 0f, 0f, 0.55f);
            lockImg.raycastTarget = false;
            lockGo.SetActive(V3CaptainCatalog.IsLocked(captain));

            return new CardSlot
            {
                captain = captain,
                root = rect,
                scrollImage = scrollImage,
                selectedImage = selectedImage,
                lockOverlay = lockImg,
                placeholderLabel = label
            };
        }

        static Image CreateCardArtLayer(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            return image;
        }

        bool HasCardArt(V3CaptainId captain)
        {
            if (_art == null)
                return false;
            return _art.GetScrollCard(captain) != null || _art.GetSelectedCard(captain) != null;
        }

        Button CreatePlayButton()
        {
            var go = new GameObject("play_button", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 56f);
            rect.sizeDelta = new Vector2(360f, 88f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.92f, 0.88f, 0.78f, 1f);

            var label = CreateTmp(go.transform, "label", "SET SAIL", 36, FontStyles.Bold);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.18f, 0.14f, 0.1f, 1f);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => PlayRequested?.Invoke());
            return button;
        }

        public void SetCaptain(V3CaptainId captain, bool snap)
        {
            var logical = IndexOfCaptain(captain);
            if (logical < 0)
                logical = 0;
            FocusLogical(logical, snap);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                    StepLogical(-1, true);
                else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                    StepLogical(1, true);
            }

            if (_dragging)
                UpdateCardVisuals();

            if (!_dragging && Mathf.Abs(_content.anchoredPosition.x - _targetContentX) > 0.5f)
            {
                var x = Mathf.Lerp(_content.anchoredPosition.x, _targetContentX, Time.unscaledDeltaTime * SnapSpeed);
                _content.anchoredPosition = new Vector2(x, 0f);
                UpdateCardVisuals();
            }
        }

        void LateUpdate()
        {
            if (!_layoutReady || _scrollRect == null)
                return;

            if (_dragging || _scrollRect.velocity.sqrMagnitude > 4f)
            {
                WrapInfiniteStrip();
                UpdateCardVisuals();
            }
        }

        void OnValidate()
        {
            cardSpacing = Mathf.Max(120f, cardSpacing);
            highlightBlendDistance = Mathf.Clamp(highlightBlendDistance, 0.35f, 2f);
            artBlendSmoothing = Mathf.Max(1f, artBlendSmoothing);
            if (_layoutReady && _content != null)
            {
                RebuildCarouselLayout();
                SnapToPhysical(CaptainCount + _logicalIndex);
            }
        }

        float CardStep => Mathf.Max(120f, cardSpacing);

        void StepLogical(int delta, bool snap)
        {
            var count = CaptainCount;
            if (count <= 0)
                return;
            var next = (_logicalIndex + delta) % count;
            if (next < 0)
                next += count;
            FocusLogical(next, snap);
        }

        void UpdateFocusFromScrollPosition(bool snap)
        {
            if (!_layoutReady)
                return;

            WrapInfiniteStrip();
            var physical = GetPhysicalIndexFromScroll();
            if (snap)
                SnapToPhysical(physical);
            else
                ApplyPhysicalFocus(physical);
        }

        void FocusLogical(int logical, bool snap, int? physicalOverride = null)
        {
            var count = CaptainCount;
            if (count <= 0)
                return;

            logical = ((logical % count) + count) % count;
            var physical = physicalOverride ?? (CaptainCount + logical);
            if (snap)
                SnapToPhysical(physical);
            else
                ApplyPhysicalFocus(physical);
        }

        void SnapToPhysical(int physical)
        {
            physical = Mathf.Clamp(physical, 0, _slots.Count - 1);
            _targetContentX = _scrollBaseOffset - physical * CardStep;
            _content.anchoredPosition = new Vector2(_targetContentX, 0f);
            WrapInfiniteStrip();
            ApplyPhysicalFocus(GetPhysicalIndexFromScroll());
        }

        void ApplyPhysicalFocus(int physical)
        {
            physical = Mathf.Clamp(physical, 0, _slots.Count - 1);
            _logicalIndex = physical % CaptainCount;
            _targetContentX = _scrollBaseOffset - physical * CardStep;
            NotifyCaptainIfNeeded(_slots[physical].captain);
            UpdatePlayButton();
            UpdateCardVisuals();
        }

        void NotifyCaptainIfNeeded(V3CaptainId captain)
        {
            if (captain == _lastNotifiedCaptain)
                return;
            _lastNotifiedCaptain = captain;
            if (!V3CaptainCatalog.IsLocked(captain))
                CaptainChanged?.Invoke(captain);
        }

        void UpdatePlayButton()
        {
            if (PlayButton == null || _slots.Count == 0)
                return;
            var captain = V3CaptainCatalog.MenuOrder[_logicalIndex];
            PlayButton.interactable = !V3CaptainCatalog.IsLocked(captain);
        }

        int GetPhysicalIndexFromScroll()
        {
            var physical = Mathf.RoundToInt((_scrollBaseOffset - _content.anchoredPosition.x) / CardStep);
            return Mathf.Clamp(physical, 0, _slots.Count - 1);
        }

        void WrapInfiniteStrip()
        {
            if (!_layoutReady || CaptainCount <= 0)
                return;

            var step = CardStep;
            var strip = CaptainCount * step;
            var physical = GetPhysicalIndexFromScroll();

            if (physical < CaptainCount)
            {
                ShiftContent(-strip);
                return;
            }

            if (physical >= CaptainCount * 2)
                ShiftContent(strip);
        }

        void ShiftContent(float deltaX)
        {
            var pos = _content.anchoredPosition;
            pos.x += deltaX;
            _content.anchoredPosition = pos;
            _targetContentX += deltaX;
        }

        void UpdateCardVisuals()
        {
            if (_viewport == null)
                return;

            var center = _viewport.TransformPoint(_viewport.rect.center);
            var closestDist = float.MaxValue;
            var closest = 0;
            var drawOrder = new List<(int index, float dist)>(_slots.Count);

            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                var dist = Mathf.Abs(slot.root.position.x - center.x);
                drawOrder.Add((i, dist));
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = i;
                }

                var t = Mathf.Clamp01(dist / (CardStep * 0.85f));
                var scale = Mathf.Lerp(SelectedScale, SideScale, t);
                slot.root.localScale = new Vector3(scale, scale, 1f);

                var focusFactor = ComputeFocusFactor(dist);
                var blendLerp = 1f - Mathf.Exp(-artBlendSmoothing * Time.unscaledDeltaTime);
                slot.artBlend = Mathf.Lerp(slot.artBlend, focusFactor, blendLerp);
                ApplyCardVisuals(slot.scrollImage, slot.selectedImage, slot.captain, slot.artBlend, slot.placeholderLabel);
                _slots[i] = slot;
            }

            drawOrder.Sort((a, b) => b.dist.CompareTo(a.dist));
            for (var rank = 0; rank < drawOrder.Count; rank++)
                _slots[drawOrder[rank].index].root.SetSiblingIndex(rank);

            var focusedCaptain = _slots[closest].captain;
            _logicalIndex = IndexOfCaptain(focusedCaptain);
            if (_logicalIndex < 0)
                _logicalIndex = 0;

            NotifyCaptainIfNeeded(focusedCaptain);
            UpdatePlayButton();
        }

        float ComputeFocusFactor(float distanceFromCenter)
        {
            var blendDistance = Mathf.Max(0.25f, highlightBlendDistance) * CardStep;
            var normalized = Mathf.Clamp01(distanceFromCenter / blendDistance);
            return 1f - SmootherStep(normalized);
        }

        static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        void ApplyCardVisuals(
            Image scrollImage,
            Image selectedImage,
            V3CaptainId captain,
            float focusFactor,
            TextMeshProUGUI placeholderLabel)
        {
            Sprite scroll = null;
            Sprite selected = null;
            if (_art != null)
            {
                scroll = _art.GetScrollCard(captain);
                selected = _art.GetSelectedCard(captain);
            }

            var hasDistinctSelected = scroll != null && selected != null && selected != scroll;
            if (!hasDistinctSelected)
            {
                var single = scroll ?? selected;
                scrollImage.gameObject.SetActive(true);
                selectedImage.gameObject.SetActive(false);
                if (single != null)
                {
                    scrollImage.sprite = single;
                    scrollImage.color = Color.white;
                    FitSpriteToCard(scrollImage, single);
                    if (placeholderLabel != null)
                        placeholderLabel.enabled = false;
                }
                else
                {
                    ApplyPlaceholder(scrollImage, captain, placeholderLabel);
                }

                return;
            }

            scrollImage.gameObject.SetActive(true);
            selectedImage.gameObject.SetActive(true);
            scrollImage.sprite = scroll;
            selectedImage.sprite = selected;
            var unifiedSize = UnifiedDualLayerSize(scroll, selected);
            scrollImage.rectTransform.sizeDelta = unifiedSize;
            selectedImage.rectTransform.sizeDelta = unifiedSize;
            scrollImage.rectTransform.anchoredPosition = Vector2.zero;
            selectedImage.rectTransform.anchoredPosition = Vector2.zero;

            var blend = SmootherStep(focusFactor);
            scrollImage.color = new Color(1f, 1f, 1f, 1f - blend);
            selectedImage.color = new Color(1f, 1f, 1f, blend);

            if (placeholderLabel != null)
                placeholderLabel.enabled = false;
        }

        static void FitSpriteToCard(Image image, Sprite sprite)
        {
            if (sprite == null)
                return;

            image.rectTransform.sizeDelta = FitSizeForSprite(sprite);
        }

        static Vector2 FitSizeForSprite(Sprite sprite)
        {
            var w = sprite.rect.width;
            var h = sprite.rect.height;
            if (w <= 0f || h <= 0f)
                return new Vector2(CardBaseWidth, ScrollCardHeight);

            var targetH = ScrollCardHeight;
            var targetW = targetH * (w / h);
            return new Vector2(targetW, targetH);
        }

        static Vector2 UnifiedDualLayerSize(Sprite scroll, Sprite selected)
        {
            var scrollSize = FitSizeForSprite(scroll);
            var selectedSize = FitSizeForSprite(selected);
            return new Vector2(Mathf.Max(scrollSize.x, selectedSize.x), Mathf.Max(scrollSize.y, selectedSize.y));
        }

        static void ApplyPlaceholder(Image image, V3CaptainId captain, TextMeshProUGUI placeholderLabel)
        {
            image.sprite = null;
            image.color = GetPlaceholderColor(captain);
            image.preserveAspect = false;
            image.rectTransform.sizeDelta = new Vector2(CardBaseWidth, ScrollCardHeight);
            if (placeholderLabel != null)
                placeholderLabel.enabled = true;
        }

        static Color GetPlaceholderColor(V3CaptainId id) => id switch
        {
            V3CaptainId.Brine => new Color(0.15f, 0.45f, 0.38f, 1f),
            V3CaptainId.Flint => new Color(0.42f, 0.22f, 0.55f, 1f),
            V3CaptainId.Richie => new Color(0.55f, 0.42f, 0.18f, 1f),
            V3CaptainId.Ember => new Color(0.62f, 0.22f, 0.12f, 1f),
            V3CaptainId.Roxie => new Color(0.12f, 0.42f, 0.62f, 1f),
            _ => new Color(0.45f, 0.38f, 0.15f, 1f)
        };

        int IndexOfCaptain(V3CaptainId captain)
        {
            var order = V3CaptainCatalog.MenuOrder;
            for (var i = 0; i < order.Length; i++)
            {
                if (order[i] == captain)
                    return i;
            }

            return -1;
        }

        static Image CreateStretchImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        static TextMeshProUGUI CreateTmp(Transform parent, string name, string text, float size, FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            return tmp;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        sealed class CharacterSelectDragRelay : MonoBehaviour, IBeginDragHandler, IEndDragHandler
        {
            V3MainMenuCharacterSelect _owner;

            public void Bind(V3MainMenuCharacterSelect owner) => _owner = owner;

            public void OnBeginDrag(PointerEventData eventData) => _owner?.NotifyDragBegin();

            public void OnEndDrag(PointerEventData eventData) => _owner?.NotifyDragEnd();
        }
    }
}
