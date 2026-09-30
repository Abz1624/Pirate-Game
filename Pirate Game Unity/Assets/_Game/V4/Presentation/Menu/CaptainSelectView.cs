using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using RoguePirates.V4.Foundation.Progression;
using RoguePirates.V4.Foundation.UI;

namespace RoguePirates.V4.Presentation.Menu
{
    /// <summary>One Captain card: the art id (V3's captain art is keyed by CaptainId), label, theme and lock.</summary>
    public struct CaptainMenuEntry
    {
        public string Key;
        public CaptainId ArtId;
        public string Label;
        public Color Theme;
        public bool Locked;
        /// <summary>What unlocks it (shown on the card when locked).</summary>
        public string LockReason;
        /// <summary>Demo art menu: the active's name and summary and the passive summary (Captain config).</summary>
        public string ActiveName;
        public string ActiveSummary;
        public string PassiveSummary;
    }

    /// <summary>
    /// The Captain carousel (M14.13; port of V3's V3MainMenuCharacterSelect, which read V3CaptainCatalog's order, labels,
    /// colours and locks): the same cards, art blend, focus layout, background and Set Sail button, over entries the menu
    /// builds from the Captain configs and the V4 lock rules. A locked card shows what unlocks it; the button reads
    /// <see cref="PlayLabel"/> (the menu turns it into a Rum unlock when that is all that is missing).
    /// </summary>
    public sealed class CaptainSelectView : MonoBehaviour, ICaptainMenuView
    {
        const float CardHeight = 520f;
        const float CardGap = 10f;
        const float RowHorizontalPadding = 40f;
        const float FocusExpandShare = 0.14f;
        const float BgTransitionSeconds = 0.38f;
        const float FocusedHeightScale = 1.24f;
        const float ArtHeightFill = 0.96f;

        [Tooltip("How quickly cards resize when focus changes.")]
        public float layoutSmoothing = 14f;

        [Tooltip("How quickly card art crossfades toward selected.")]
        public float artBlendSmoothing = 16f;

        /// <summary>The focused Captain's entry index (also for locked Captains, so the menu can offer the unlock).</summary>
        public event Action<int> CaptainChanged;
        public event Action PlayRequested;

        public Button PlayButton { get; private set; }

        Image _bgGradientTop;
        Image _bgGradientBottom;
        Image _bgVignette;
        Image _bgAccentGlow;
        RectTransform _row;
        CharacterCardArtSettings _art;

        readonly List<CardSlot> _slots = new();
        readonly float[] _focus = new float[6];

        int _selectedIndex;
        int _hoverIndex = -1;
        int _lastNotified = -1;
        readonly List<CaptainMenuEntry> _entries = new();
        TextMeshProUGUI _playLabel, _hint;
        string _hintText = "Captain for this voyage — more weapons from level-ups";
        bool _playEnabled = true;

        MenuVisuals.ThemeGradient _bgFrom;
        MenuVisuals.ThemeGradient _bgTo;
        float _bgTransitionT = 1f;
        bool _layoutReady;

        int CaptainCount => _entries.Count;

        int FocusIndex => _hoverIndex >= 0 ? _hoverIndex : _selectedIndex;

        struct CardSlot
        {
            public CaptainId captain;
            public TextMeshProUGUI lockLabel;
            public RectTransform root;
            public Image scrollImage;
            public Image selectedImage;
            public Image lockOverlay;
            public TextMeshProUGUI placeholderLabel;
            public float artBlend;
        }

        public static CaptainSelectView Ensure(Transform canvasRoot)
        {
            var existing = canvasRoot.GetComponentInChildren<CaptainSelectView>(true);
            if (existing != null)
                return existing;

            var root = new GameObject("v4_ui_character_select", typeof(RectTransform));
            root.transform.SetParent(canvasRoot, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return root.AddComponent<CaptainSelectView>();
        }

        void Awake()
        {
            CharacterCardArtSettings.Reload();
            _art = CharacterCardArtSettings.Load();
        }

        /// <summary>Builds the cards for these Captains (menu order) and selects <paramref name="selected"/>.</summary>
        public void Show(IReadOnlyList<CaptainMenuEntry> entries, int selected, string hint)
        {
            if (_art == null) _art = CharacterCardArtSettings.Load();
            _entries.Clear();
            _entries.AddRange(entries);
            if (!string.IsNullOrEmpty(hint)) _hintText = hint;
            _lastNotified = -1;
            BuildUi();
            Canvas.ForceUpdateCanvases();
            _layoutReady = true;
            SelectIndex(Mathf.Clamp(selected, 0, Mathf.Max(0, _entries.Count - 1)), true);
        }

        /// <summary>Updates the locks (after an unlock) without rebuilding the cards.</summary>
        public void Refresh(IReadOnlyList<CaptainMenuEntry> entries)
        {
            for (var i = 0; i < entries.Count && i < _entries.Count && i < _slots.Count; i++)
            {
                _entries[i] = entries[i];
                _slots[i].lockOverlay.gameObject.SetActive(entries[i].Locked);
                if (_slots[i].lockLabel != null) _slots[i].lockLabel.text = entries[i].Locked ? entries[i].LockReason : string.Empty;
            }
        }

        /// <summary>The Set Sail button's text and whether it can be pressed.</summary>
        public void SetPlayButton(string label, bool enabled)
        {
            _playEnabled = enabled;
            if (_playLabel != null) _playLabel.text = label;
            if (PlayButton != null) PlayButton.interactable = enabled;
        }

        public string PlayLabel => _playLabel != null ? _playLabel.text : string.Empty;
        public int SelectedIndex => _selectedIndex;
        public int CardCount => _slots.Count;
        public bool CardLocked(int index) => index >= 0 && index < _slots.Count && _slots[index].lockOverlay.gameObject.activeSelf;
        public void Select(int index) => SelectIndex(index, false);

        void BuildUi()
        {
            for (var i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            _slots.Clear();
            Array.Clear(_focus, 0, _focus.Length);

            var defaultTheme = _entries.Count > 0 ? ThemeGradientFor(0) : MenuVisuals.BuildGradient(Color.blue);
            _bgGradientBottom = MenuVisuals.CreateGradientBand(transform, "bg_gradient_bottom", Vector2.zero, new Vector2(1f, 0.58f));
            _bgGradientTop = MenuVisuals.CreateGradientBand(transform, "bg_gradient_top", new Vector2(0f, 0.38f), Vector2.one);
            _bgAccentGlow = MenuVisuals.CreateGradientBand(transform, "bg_accent_glow", new Vector2(0.52f, 0f), Vector2.one);
            _bgVignette = CreateStretchImage(transform, "bg_vignette", new Color(0f, 0f, 0f, 0.38f));
            _bgFrom = defaultTheme;
            _bgTo = defaultTheme;
            MenuVisuals.ApplyBackground(_bgGradientTop, _bgGradientBottom, _bgVignette, _bgAccentGlow, defaultTheme);

            var header = CreateTmp(transform, "header", "SELECT YOUR CAPTAIN", 28, FontStyles.Bold);
            var headerRect = header.rectTransform;
            headerRect.anchorMin = new Vector2(0.5f, 1f);
            headerRect.anchorMax = new Vector2(0.5f, 1f);
            headerRect.pivot = new Vector2(0.5f, 1f);
            headerRect.anchoredPosition = new Vector2(0f, -36f);
            headerRect.sizeDelta = new Vector2(900f, 40f);
            header.alignment = TextAlignmentOptions.Center;
            header.color = new Color(1f, 1f, 1f, 0.65f);
            header.characterSpacing = 4f;
            MenuVisuals.StyleHeader(header);

            var rowGo = new GameObject("card_row", typeof(RectTransform));
            rowGo.transform.SetParent(transform, false);
            _row = rowGo.GetComponent<RectTransform>();
            _row.anchorMin = new Vector2(0f, 0.16f);
            _row.anchorMax = new Vector2(1f, 0.86f);
            _row.offsetMin = new Vector2(RowHorizontalPadding, 0f);
            _row.offsetMax = new Vector2(-RowHorizontalPadding, 0f);

            for (var i = 0; i < _entries.Count; i++)
                _slots.Add(CreateCard(_row, _entries[i], i));

            PlayButton = CreatePlayButton();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                    StepSelection(-1);
                else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                    StepSelection(1);
            }

            TickBackgroundTransition();
            TickLayoutAndVisuals();
        }

        void OnRectTransformDimensionsChange()
        {
            if (_layoutReady)
                LayoutCardsImmediate();
        }

        void StepSelection(int delta)
        {
            var count = CaptainCount;
            if (count <= 0)
                return;
            var next = (_selectedIndex + delta) % count;
            if (next < 0)
                next += count;
            SelectIndex(next, false);
        }

        void SelectIndex(int index, bool snap)
        {
            index = Mathf.Clamp(index, 0, Mathf.Max(0, CaptainCount - 1));
            if (index == _selectedIndex && !snap)
                return;

            _selectedIndex = index;
            _hoverIndex = -1;
            BeginBackgroundTransition(ThemeGradientFor(_selectedIndex));
            NotifyCaptainIfNeeded(_selectedIndex);

            if (snap)
            {
                for (var i = 0; i < _slots.Count; i++)
                    _focus[i] = i == FocusIndex ? 1f : 0f;
                LayoutCardsImmediate();
                RefreshCardArt(true);
            }
        }

        internal void NotifyCardHover(int index, bool entered)
        {
            if (index < 0 || index >= _slots.Count)
                return;

            if (entered)
            {
                if (_hoverIndex == index)
                    return;
                _hoverIndex = index;
                BeginBackgroundTransition(ThemeGradientFor(index));
            }
            else if (_hoverIndex == index)
            {
                _hoverIndex = -1;
                BeginBackgroundTransition(ThemeGradientFor(_selectedIndex));
            }
        }

        internal void NotifyCardClick(int index)
        {
            if (index < 0 || index >= _slots.Count)
                return;
            SelectIndex(index, false);
        }

        void TickBackgroundTransition()
        {
            if (_bgTransitionT >= 1f)
                return;

            _bgTransitionT += Time.unscaledDeltaTime / BgTransitionSeconds;
            var t = SmootherStep(Mathf.Clamp01(_bgTransitionT));
            MenuVisuals.ApplyBackground(
                _bgGradientTop,
                _bgGradientBottom,
                _bgVignette,
                _bgAccentGlow,
                MenuVisuals.Lerp(_bgFrom, _bgTo, t));
        }

        void BeginBackgroundTransition(MenuVisuals.ThemeGradient target)
        {
            var t = Mathf.Clamp01(_bgTransitionT);
            var current = t >= 1f ? _bgTo : MenuVisuals.Lerp(_bgFrom, _bgTo, SmootherStep(t));
            _bgFrom = current;
            _bgTo = target;
            _bgTransitionT = 0f;
        }

        void TickLayoutAndVisuals()
        {
            if (!_layoutReady || _row == null || _slots.Count == 0)
                return;

            var focusIndex = FocusIndex;
            var blend = 1f - Mathf.Exp(-layoutSmoothing * Time.unscaledDeltaTime);
            for (var i = 0; i < _slots.Count; i++)
            {
                var target = i == focusIndex ? 1f : 0f;
                _focus[i] = Mathf.Lerp(_focus[i], target, blend);
            }

            LayoutCardsFromFocus();
            RefreshCardArt(false);
            UpdateDrawOrder(focusIndex);
        }

        void LayoutCardsImmediate() => LayoutCardsFromFocus();

        void LayoutCardsFromFocus()
        {
            var rowW = _row.rect.width;
            if (rowW <= 1f)
                return;

            var n = _slots.Count;
            var totalGap = CardGap * (n - 1);
            var expandPool = rowW * FocusExpandShare;
            var baseWidth = (rowW - totalGap - expandPool) / n;

            var x = 0f;
            for (var i = 0; i < n; i++)
            {
                var slot = _slots[i];
                var w = baseWidth + expandPool * _focus[i];
                var h = CardHeightForFocus(_focus[i]);

                slot.root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
                slot.root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                slot.root.anchoredPosition = new Vector2(x + w * 0.5f, 0f);
                slot.root.localScale = Vector3.one;
                _slots[i] = slot;

                x += w + CardGap;
            }
        }

        void UpdateDrawOrder(int focusIndex)
        {
            for (var i = 0; i < _slots.Count; i++)
            {
                if (i != focusIndex)
                    _slots[i].root.SetSiblingIndex(i);
            }

            if (focusIndex >= 0 && focusIndex < _slots.Count)
                _slots[focusIndex].root.SetAsLastSibling();
        }

        void RefreshCardArt(bool instant)
        {
            var artLerp = instant ? 1f : 1f - Mathf.Exp(-artBlendSmoothing * Time.unscaledDeltaTime);
            for (var i = 0; i < _slots.Count; i++)
            {
                var slot = _slots[i];
                slot.artBlend = instant
                    ? _focus[i]
                    : Mathf.Lerp(slot.artBlend, _focus[i], artLerp);
                ApplyCardVisuals(
                    slot.scrollImage,
                    slot.selectedImage,
                    slot.captain,
                    slot.artBlend,
                    ArtHeightForFocus(slot.artBlend),
                    slot.placeholderLabel);
                _slots[i] = slot;
            }
        }

        CardSlot CreateCard(RectTransform parent, CaptainMenuEntry entry, int index)
        {
            var captain = entry.ArtId;
            var go = new GameObject($"card_{captain}", typeof(RectTransform), typeof(RectMask2D));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(200f, CardHeight);

            var hitGo = new GameObject("hit", typeof(RectTransform), typeof(Image));
            hitGo.transform.SetParent(go.transform, false);
            Stretch(hitGo.GetComponent<RectTransform>());
            var hit = hitGo.GetComponent<Image>();
            hit.sprite = null;
            hit.color = new Color(1f, 1f, 1f, 0f);
            hit.raycastTarget = true;
            hitGo.AddComponent<CardPointerRelay>().Bind(this, index);

            var artStack = new GameObject("art_stack", typeof(RectTransform));
            artStack.transform.SetParent(go.transform, false);
            Stretch(artStack.GetComponent<RectTransform>());

            var scrollImage = CreateCardArtLayer(artStack.transform, "scroll_art");
            var selectedImage = CreateCardArtLayer(artStack.transform, "selected_art");
            ApplyCardVisuals(scrollImage, selectedImage, captain, 0f, ArtHeightForFocus(0f), null);

            var labelGo = new GameObject("placeholder_label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            Stretch(labelGo.GetComponent<RectTransform>());
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.text = (entry.Label ?? string.Empty).ToUpperInvariant();
            label.fontSize = 28;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 1f, 1f, 0.35f);
            label.raycastTarget = false;
            label.enabled = !HasCardArt(captain);
            MenuTypography.ApplyBody(label);

            var lockGo = new GameObject("lock_overlay", typeof(RectTransform), typeof(Image));
            lockGo.transform.SetParent(go.transform, false);
            Stretch(lockGo.GetComponent<RectTransform>());
            var lockImg = lockGo.GetComponent<Image>();
            lockImg.color = new Color(0f, 0f, 0f, 0.55f);
            lockImg.raycastTarget = false;
            lockGo.SetActive(entry.Locked);

            // M14.13: what unlocks a locked Captain (V3 showed only the dark overlay).
            // A dark strip near the top of the card, clear of the name art at the bottom.
            var strip = CreateStretchImage(lockGo.transform, "lock_strip", new Color(0f, 0f, 0f, 0.7f));
            var stripRect = strip.rectTransform;
            stripRect.anchorMin = new Vector2(0f, 1f);
            stripRect.anchorMax = new Vector2(1f, 1f);
            stripRect.pivot = new Vector2(0.5f, 1f);
            stripRect.anchoredPosition = new Vector2(0f, -40f);
            stripRect.sizeDelta = new Vector2(0f, 76f);
            var lockText = CreateTmp(strip.transform, "lock_reason", entry.Locked ? entry.LockReason : string.Empty, 20, FontStyles.Bold);
            var lockRect = lockText.rectTransform;
            Stretch(lockRect);
            lockRect.offsetMin = new Vector2(8f, 4f);
            lockRect.offsetMax = new Vector2(-8f, -4f);
            lockText.alignment = TextAlignmentOptions.Center;
            lockText.enableWordWrapping = true;
            lockText.color = new Color(1f, 0.92f, 0.75f, 1f);
            lockText.raycastTarget = false;

            return new CardSlot
            {
                captain = captain,
                lockLabel = lockText,
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
            image.sprite = null;
            image.color = new Color(1f, 1f, 1f, 0f);
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.type = Image.Type.Simple;
            return image;
        }

        bool HasCardArt(CaptainId captain)
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
            rect.anchoredPosition = new Vector2(0f, 52f);
            rect.sizeDelta = new Vector2(380f, 84f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.96f, 0.9f, 0.72f, 1f);
            MenuVisuals.AddDropShadow(img, new Vector2(0f, -5f), new Color(0f, 0f, 0f, 0.55f));

            var label = CreateTmp(go.transform, "label", "SET SAIL", 34, FontStyles.Bold);
            _playLabel = label;
            MenuTypography.ApplyBody(label);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(0.18f, 0.14f, 0.1f, 1f);

            var hint = CreateTmp(transform, "captain_select_hint", _hintText, 16, FontStyles.Italic);
            _hint = hint;
            MenuTypography.ApplyBody(hint);
            var hintRect = hint.rectTransform;
            hintRect.anchorMin = new Vector2(0.5f, 0f);
            hintRect.anchorMax = new Vector2(0.5f, 0f);
            hintRect.pivot = new Vector2(0.5f, 0f);
            hintRect.anchoredPosition = new Vector2(0f, 148f);
            hintRect.sizeDelta = new Vector2(720f, 48f);
            hint.alignment = TextAlignmentOptions.Center;
            hint.color = new Color(0.82f, 0.88f, 0.95f, 0.92f);

            var button = go.GetComponent<Button>();
            button.onClick.AddListener(() => { if (_playEnabled) PlayRequested?.Invoke(); });
            return button;
        }

        MenuVisuals.ThemeGradient ThemeGradientFor(int index)
        {
            if (index < 0 || index >= _entries.Count) return MenuVisuals.BuildGradient(Color.blue);
            var entry = _entries[index];
            var theme = _art != null ? _art.GetMenuThemeColor(entry.ArtId) : entry.Theme;
            return MenuVisuals.BuildGradient(theme);
        }

        void NotifyCaptainIfNeeded(int index)
        {
            if (index == _lastNotified)
                return;
            _lastNotified = index;
            CaptainChanged?.Invoke(index);
        }

        static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        static float CardHeightForFocus(float focus) =>
            Mathf.Lerp(CardHeight, CardHeight * FocusedHeightScale, Mathf.Clamp01(focus));

        static float ArtHeightForFocus(float focus) =>
            CardHeightForFocus(focus) * ArtHeightFill;

        void ApplyCardVisuals(
            Image scrollImage,
            Image selectedImage,
            CaptainId captain,
            float focusFactor,
            float artHeight,
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
                    FitSpriteToCard(scrollImage, single, artHeight);
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
            var unifiedSize = UnifiedDualLayerSize(scroll, selected, artHeight);
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

        static void FitSpriteToCard(Image image, Sprite sprite, float targetHeight)
        {
            if (sprite == null)
                return;
            image.rectTransform.sizeDelta = FitSizeForSprite(sprite, targetHeight);
        }

        static Vector2 FitSizeForSprite(Sprite sprite, float targetHeight)
        {
            var w = sprite.rect.width;
            var h = sprite.rect.height;
            if (w <= 0f || h <= 0f)
                return new Vector2(200f, targetHeight);

            var targetW = targetHeight * (w / h);
            return new Vector2(targetW, targetHeight);
        }

        static Vector2 UnifiedDualLayerSize(Sprite scroll, Sprite selected, float targetHeight)
        {
            var scrollSize = FitSizeForSprite(scroll, targetHeight);
            var selectedSize = FitSizeForSprite(selected, targetHeight);
            return new Vector2(Mathf.Max(scrollSize.x, selectedSize.x), Mathf.Max(scrollSize.y, selectedSize.y));
        }

        static void ApplyPlaceholder(Image image, CaptainId captain, TextMeshProUGUI placeholderLabel)
        {
            image.sprite = null;
            image.color = GetPlaceholderColor(captain);
            image.preserveAspect = false;
            image.rectTransform.sizeDelta = new Vector2(200f, CardHeight * ArtHeightFill);
            if (placeholderLabel != null)
                placeholderLabel.enabled = true;
        }

        static Color GetPlaceholderColor(CaptainId id) => id switch
        {
            CaptainId.Brine => new Color(0.15f, 0.45f, 0.38f, 1f),
            CaptainId.Flint => new Color(0.42f, 0.22f, 0.55f, 1f),
            CaptainId.Richie => new Color(0.55f, 0.42f, 0.18f, 1f),
            CaptainId.Ember => new Color(0.62f, 0.22f, 0.12f, 1f),
            CaptainId.Roxie => new Color(0.12f, 0.42f, 0.62f, 1f),
            _ => new Color(0.45f, 0.38f, 0.15f, 1f)
        };

        static Image CreateStretchImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Stretch(go.GetComponent<RectTransform>());
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
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
            MenuTypography.ApplyBody(tmp);
            return tmp;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        sealed class CardPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
        {
            CaptainSelectView _owner;
            int _index;

            public void Bind(CaptainSelectView owner, int index)
            {
                _owner = owner;
                _index = index;
            }

            public void OnPointerEnter(PointerEventData eventData) => _owner?.NotifyCardHover(_index, true);

            public void OnPointerExit(PointerEventData eventData) => _owner?.NotifyCardHover(_index, false);

            public void OnPointerClick(PointerEventData eventData) => _owner?.NotifyCardClick(_index);
        }
    }
}
