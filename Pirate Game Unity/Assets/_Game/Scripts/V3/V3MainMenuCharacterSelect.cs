using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HighSeas.V3
{
    public sealed class V3MainMenuCharacterSelect : MonoBehaviour
    {
        const float TransitionSeconds = 0.38f;
        const float PoseSlidePixels = 480f;
        const float DetailsPanelInset = 20f;
        const float DetailsTextPadding = 28f;

        public event Action<V3CaptainId> CaptainChanged;
        public event Action PlayRequested;

        public Button PlayButton { get; private set; }

        Image _bgGradientTop;
        Image _bgGradientBottom;
        Image _bgVignette;
        Image _bgAccentGlow;
        Image _bgFlat;
        Image _detailsPanelBg;
        Image _bgEmblem;
        bool _usePolishedUi = true;
        RectTransform _poseStage;
        Image _poseA;
        Image _poseB;
        CanvasGroup _detailsGroup;
        TextMeshProUGUI _nameText;
        TextMeshProUGUI _descText;
        Image _lockBanner;

        V3CharacterCardArtSettings _art;
        int _index;
        V3CaptainId _lastNotifiedCaptain = (V3CaptainId)(-1);
        bool _transitioning;
        float _transitionT;
        int _transitionDirection;
        int _transitionTargetIndex;
        V3MainMenuVisuals.ThemeGradient _transitionFromGradient;
        V3MainMenuVisuals.ThemeGradient _transitionToGradient;
        bool _uiBuilt;

        int CaptainCount => V3CaptainCatalog.MenuOrder.Length;

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
            V3CharacterCardArtSettings.Reload();
            _art = V3CharacterCardArtSettings.Load();
            _uiBuilt = false;
            BuildUi();
        }

        void Start()
        {
            if (_art == null)
                _art = V3CharacterCardArtSettings.Load();

            var start = IndexOfCaptain(V3CaptainSelection.Selected);
            _index = start >= 0 ? start : 0;
            ApplyCaptainInstant(_index);
            Canvas.ForceUpdateCanvases();
            ApplyPoseSprite(_poseA, CurrentCaptain());
        }

        void BuildUi()
        {
            if (_uiBuilt)
                return;
            _uiBuilt = true;

            for (var i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            if (_usePolishedUi)
            {
                var defaultGradient = V3MainMenuVisuals.BuildGradient(V3CaptainCatalog.GetMenuThemeColor(V3CaptainId.Rook));
                _bgGradientBottom = V3MainMenuVisuals.CreateGradientBand(transform, "bg_gradient_bottom", Vector2.zero, new Vector2(1f, 0.58f));
                _bgGradientTop = V3MainMenuVisuals.CreateGradientBand(transform, "bg_gradient_top", new Vector2(0f, 0.38f), Vector2.one);
                _bgAccentGlow = V3MainMenuVisuals.CreateGradientBand(transform, "bg_accent_glow", new Vector2(0.52f, 0f), Vector2.one);
                _bgVignette = CreateStretchImage(transform, "bg_vignette", new Color(0f, 0f, 0f, 0.38f));
                V3MainMenuVisuals.ApplyBackground(_bgGradientTop, _bgGradientBottom, _bgVignette, _bgAccentGlow, defaultGradient);

                var emblemGo = new GameObject("emblem", typeof(RectTransform), typeof(Image));
                emblemGo.transform.SetParent(transform, false);
                var emblemRect = emblemGo.GetComponent<RectTransform>();
                emblemRect.anchorMin = new Vector2(0.38f, 0.5f);
                emblemRect.anchorMax = new Vector2(0.38f, 0.5f);
                emblemRect.pivot = new Vector2(0.5f, 0.5f);
                emblemRect.sizeDelta = new Vector2(640f, 640f);
                emblemRect.anchoredPosition = new Vector2(-80f, -24f);
                emblemRect.localRotation = Quaternion.Euler(0f, 0f, 45f);
                _bgEmblem = emblemGo.GetComponent<Image>();
                _bgEmblem.color = new Color(0f, 0f, 0f, 0.14f);
                _bgEmblem.raycastTarget = false;
            }
            else
            {
                _bgFlat = CreateStretchImage(transform, "theme_background_flat", V3CaptainCatalog.GetMenuThemeColor(V3CaptainId.Rook));
            }

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
            if (_usePolishedUi)
                V3MainMenuVisuals.StyleHeader(header);

            var heroRow = new GameObject("hero_row", typeof(RectTransform));
            heroRow.transform.SetParent(transform, false);
            var heroRect = heroRow.GetComponent<RectTransform>();
            heroRect.anchorMin = new Vector2(0f, 0.12f);
            heroRect.anchorMax = new Vector2(1f, 0.88f);
            heroRect.offsetMin = Vector2.zero;
            heroRect.offsetMax = Vector2.zero;

            if (_usePolishedUi)
            {
                var poseShadow = CreateStretchImage(heroRow.transform, "pose_drop_shadow", new Color(0f, 0f, 0f, 0.35f));
                var poseShadowRect = poseShadow.rectTransform;
                poseShadowRect.anchorMin = new Vector2(0.08f, 0f);
                poseShadowRect.anchorMax = new Vector2(0.5f, 0.08f);
                poseShadowRect.offsetMin = Vector2.zero;
                poseShadowRect.offsetMax = Vector2.zero;
                V3MainMenuVisuals.AddDropShadow(poseShadow, new Vector2(0f, -6f), new Color(0f, 0f, 0f, 0.55f));
            }

            _poseStage = new GameObject("pose_stage", typeof(RectTransform)).GetComponent<RectTransform>();
            _poseStage.SetParent(heroRow.transform, false);
            _poseStage.anchorMin = new Vector2(0f, 0f);
            _poseStage.anchorMax = new Vector2(0.56f, 1f);
            _poseStage.offsetMin = new Vector2(72f, 16f);
            _poseStage.offsetMax = new Vector2(-8f, 0f);
            _poseA = CreatePoseLayer(_poseStage, "pose_a");
            _poseB = CreatePoseLayer(_poseStage, "pose_b");
            _poseB.gameObject.SetActive(false);

            var swipe = _poseStage.gameObject.AddComponent<CharacterSelectSwipeRelay>();
            swipe.Bind(this);

            CreateNavButton(transform, true);
            CreateNavButton(transform, false);

            var panel = new GameObject("details_panel", typeof(RectTransform), typeof(CanvasGroup));
            panel.transform.SetParent(heroRow.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.56f, 0f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.offsetMin = new Vector2(32f, 32f);
            panelRect.offsetMax = new Vector2(-80f, -16f);
            _detailsGroup = panel.GetComponent<CanvasGroup>();

            if (_usePolishedUi)
            {
                _detailsPanelBg = CreateStretchImage(panel.transform, "panel_glass", new Color(0f, 0f, 0f, 0.22f));
                var glassRect = _detailsPanelBg.rectTransform;
                glassRect.anchorMin = Vector2.zero;
                glassRect.anchorMax = Vector2.one;
                glassRect.offsetMin = new Vector2(DetailsPanelInset, DetailsPanelInset);
                glassRect.offsetMax = new Vector2(-DetailsPanelInset, -DetailsPanelInset);
                _detailsPanelBg.transform.SetAsFirstSibling();
                V3MainMenuVisuals.AddDropShadow(_detailsPanelBg, new Vector2(6f, -6f), new Color(0f, 0f, 0f, 0.45f));
            }

            var textContent = new GameObject("text_content", typeof(RectTransform));
            textContent.transform.SetParent(panel.transform, false);
            var textContentRect = textContent.GetComponent<RectTransform>();
            textContentRect.anchorMin = Vector2.zero;
            textContentRect.anchorMax = Vector2.one;
            textContentRect.offsetMin = new Vector2(DetailsTextPadding, DetailsTextPadding);
            textContentRect.offsetMax = new Vector2(-DetailsTextPadding, -DetailsTextPadding);

            _nameText = CreateTmp(textContent.transform, "captain_name", "Captain", 72, FontStyles.Bold);
            var nameRect = _nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0f, 1f);
            nameRect.anchoredPosition = new Vector2(0f, 0f);
            nameRect.sizeDelta = new Vector2(0f, 96f);
            _nameText.alignment = TextAlignmentOptions.Left;
            _nameText.color = Color.white;
            _nameText.enableAutoSizing = true;
            _nameText.fontSizeMin = 48f;
            _nameText.fontSizeMax = 72f;
            _nameText.characterSpacing = 2f;

            _descText = CreateTmp(textContent.transform, "description", "", 26, FontStyles.Normal);
            var descRect = _descText.rectTransform;
            descRect.anchorMin = new Vector2(0f, 0f);
            descRect.anchorMax = new Vector2(1f, 1f);
            descRect.offsetMin = new Vector2(0f, 0f);
            descRect.offsetMax = new Vector2(0f, -120f);
            _descText.alignment = TextAlignmentOptions.TopLeft;
            _descText.color = new Color(1f, 1f, 1f, 0.9f);
            _descText.lineSpacing = 6f;
            _descText.enableAutoSizing = true;
            _descText.fontSizeMin = 20f;
            _descText.fontSizeMax = 26f;
            if (_usePolishedUi)
                V3MainMenuVisuals.StyleBody(_descText);

            _lockBanner = CreateStretchImage(panel.transform, "lock_banner", new Color(0f, 0f, 0f, 0.72f));
            if (_usePolishedUi)
                V3MainMenuVisuals.AddDropShadow(_lockBanner, new Vector2(0f, -3f), new Color(0f, 0f, 0f, 0.5f));
            var lockLabel = CreateTmp(_lockBanner.transform, "label", "Complete your first conquest to unlock Sparky", 22, FontStyles.Bold);
            Stretch(lockLabel.rectTransform);
            lockLabel.alignment = TextAlignmentOptions.Center;
            lockLabel.color = new Color(1f, 0.85f, 0.55f, 1f);
            _lockBanner.gameObject.SetActive(false);

            PlayButton = CreatePlayButton();
        }

        static Image CreatePoseLayer(RectTransform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        void CreateNavButton(Transform parent, bool prev)
        {
            var go = new GameObject(prev ? "nav_prev" : "nav_next", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            if (prev)
            {
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0f, 0.5f);
                rect.anchoredPosition = new Vector2(24f, 0f);
            }
            else
            {
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.anchoredPosition = new Vector2(-24f, 0f);
            }

            rect.sizeDelta = new Vector2(64f, 140f);
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, _usePolishedUi ? 0.32f : 0.22f);
            if (_usePolishedUi)
                V3MainMenuVisuals.AddDropShadow(img, new Vector2(2f, -3f), new Color(0f, 0f, 0f, 0.55f));
            var label = CreateTmp(go.transform, "glyph", prev ? "‹" : "›", 52, FontStyles.Bold);
            Stretch(label.rectTransform);
            label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1f, 1f, 1f, 0.9f);
            go.GetComponent<Button>().onClick.AddListener(() => Step(prev ? -1 : 1));
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
            if (_usePolishedUi)
            {
                V3MainMenuVisuals.AddDropShadow(img, new Vector2(0f, -5f), new Color(0f, 0f, 0f, 0.55f));
            }

            var label = CreateTmp(go.transform, "label", "SET SAIL", 34, FontStyles.Bold);
            if (_usePolishedUi)
            {
                V3MainMenuVisuals.StyleBody(label);
                label.outlineWidth = 0.15f;
            }
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
            if (snap || !_uiBuilt)
            {
                _index = logical;
                ApplyCaptainInstant(_index);
                return;
            }

            if (logical == _index)
                return;

            var delta = logical - _index;
            if (delta > CaptainCount / 2)
                delta -= CaptainCount;
            else if (delta < -CaptainCount / 2)
                delta += CaptainCount;
            BeginTransition(logical, delta >= 0 ? 1 : -1);
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.zKey.wasPressedThisFrame)
                {
                    ToggleUiStyle();
                    return;
                }

                if (!_transitioning)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
                        Step(-1);
                    else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
                        Step(1);
                }
            }

            if (_transitioning)
                TickTransition();
        }

        internal void Step(int delta)
        {
            if (_transitioning || CaptainCount <= 0)
                return;

            var next = (_index + delta) % CaptainCount;
            if (next < 0)
                next += CaptainCount;
            BeginTransition(next, delta);
        }

        void BeginTransition(int targetIndex, int direction)
        {
            _transitioning = true;
            _transitionT = 0f;
            _transitionTargetIndex = targetIndex;
            _transitionDirection = direction >= 0 ? 1 : -1;
            _transitionFromGradient = GradientFor(_index);
            _transitionToGradient = GradientFor(targetIndex);

            var incomingCaptain = V3CaptainCatalog.MenuOrder[targetIndex];
            ApplyPoseSprite(_poseB, incomingCaptain);
            _poseB.gameObject.SetActive(true);
            _poseB.rectTransform.anchoredPosition = new Vector2(_transitionDirection * PoseSlidePixels, 0f);
            _poseB.color = new Color(1f, 1f, 1f, 0f);
            _poseA.rectTransform.anchoredPosition = Vector2.zero;
            _poseA.color = Color.white;
        }

        void TickTransition()
        {
            _transitionT += Time.unscaledDeltaTime / TransitionSeconds;
            var t = Mathf.Clamp01(_transitionT);
            var eased = SmootherStep(t);

            ApplyBackgroundLerp(eased);
            if (_usePolishedUi && _bgEmblem != null)
                _bgEmblem.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.14f, 0.2f, eased));

            var slide = PoseSlidePixels * eased;
            _poseA.rectTransform.anchoredPosition = new Vector2(-_transitionDirection * slide, 0f);
            _poseB.rectTransform.anchoredPosition = new Vector2(_transitionDirection * (PoseSlidePixels - slide), 0f);
            _poseA.color = new Color(1f, 1f, 1f, 1f - eased);
            _poseB.color = new Color(1f, 1f, 1f, eased);

            _detailsGroup.alpha = 1f - Mathf.Abs(eased - 0.5f) * 2f;

            if (t < 1f)
                return;

            _index = _transitionTargetIndex;
            _transitioning = false;
            SwapPoseLayers();
            ApplyCaptainDetails(_index);
            _detailsGroup.alpha = 1f;
            NotifyCaptainIfNeeded(CurrentCaptain());
            UpdatePlayButton();
        }

        void SwapPoseLayers()
        {
            var temp = _poseA;
            _poseA = _poseB;
            _poseB = temp;
            _poseB.gameObject.SetActive(false);
            _poseA.rectTransform.anchoredPosition = Vector2.zero;
            _poseA.color = Color.white;
        }

        void ApplyCaptainInstant(int index)
        {
            _index = Mathf.Clamp(index, 0, Mathf.Max(0, CaptainCount - 1));
            var captain = CurrentCaptain();
            ApplyPoseSprite(_poseA, captain);
            _poseB.gameObject.SetActive(false);
            ApplyThemeBackground(_index);
            ApplyCaptainDetails(_index);
            NotifyCaptainIfNeeded(captain);
            UpdatePlayButton();
        }

        void ApplyCaptainDetails(int index)
        {
            var captain = V3CaptainCatalog.MenuOrder[index];
            _nameText.text = V3CaptainCatalog.GetMenuLabel(captain).ToUpperInvariant();
            _descText.text = V3CaptainCatalog.GetMenuDescription(captain);
            ApplyNameTextStyle(index);
            if (_usePolishedUi)
                V3MainMenuVisuals.StyleBody(_descText);
            else
            {
                _descText.outlineWidth = 0f;
                _descText.color = new Color(1f, 1f, 1f, 0.9f);
            }

            var locked = V3CaptainCatalog.IsLocked(captain);
            _lockBanner.gameObject.SetActive(locked);
            _poseA.color = locked ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
        }

        void ApplyPoseSprite(Image image, V3CaptainId captain)
        {
            var pose = V3CharacterSelectPoseResolver.Resolve(captain, _art);
            image.sprite = pose;
            image.enabled = pose != null;
            if (pose == null)
            {
                image.color = new Color(1f, 1f, 1f, 0f);
                return;
            }

            image.color = Color.white;
            LayoutPoseImage(image, pose);
        }

        static void LayoutPoseImage(Image image, Sprite pose)
        {
            var rect = image.rectTransform;
            var parent = rect.parent as RectTransform;
            if (parent == null)
                return;

            var stageW = parent.rect.width;
            var stageH = parent.rect.height;
            if (stageW <= 1f || stageH <= 1f)
                return;

            var spriteW = pose.rect.width;
            var spriteH = pose.rect.height;
            if (spriteW <= 0f || spriteH <= 0f)
                return;

            var aspect = spriteW / spriteH;
            var targetH = stageH * 0.98f;
            var targetW = targetH * aspect;
            if (targetW > stageW * 0.98f)
            {
                targetW = stageW * 0.98f;
                targetH = targetW / aspect;
            }

            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetW);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, targetH);
            rect.anchoredPosition = Vector2.zero;
        }

        Color ThemeColorFor(int index) =>
            V3CaptainCatalog.GetMenuThemeColor(V3CaptainCatalog.MenuOrder[index]);

        V3MainMenuVisuals.ThemeGradient GradientFor(int index) =>
            V3MainMenuVisuals.BuildGradient(ThemeColorFor(index));

        void ApplyThemeBackground(int index)
        {
            if (_usePolishedUi)
            {
                V3MainMenuVisuals.ApplyBackground(
                    _bgGradientTop,
                    _bgGradientBottom,
                    _bgVignette,
                    _bgAccentGlow,
                    GradientFor(index));
                return;
            }

            if (_bgFlat != null)
                _bgFlat.color = ThemeColorFor(index);
        }

        void ApplyBackgroundLerp(float eased)
        {
            if (_usePolishedUi)
            {
                V3MainMenuVisuals.ApplyBackground(
                    _bgGradientTop,
                    _bgGradientBottom,
                    _bgVignette,
                    _bgAccentGlow,
                    V3MainMenuVisuals.Lerp(_transitionFromGradient, _transitionToGradient, eased));
                return;
            }

            if (_bgFlat != null)
                _bgFlat.color = Color.Lerp(ThemeColorFor(_index), ThemeColorFor(_transitionTargetIndex), eased);
        }

        void ApplyNameTextStyle(int index)
        {
            if (_usePolishedUi)
            {
                V3MainMenuVisuals.StyleCaptainName(_nameText, ThemeColorFor(index));
                return;
            }

            _nameText.enableVertexGradient = false;
            _nameText.outlineWidth = 0f;
            _nameText.color = Color.white;
        }

        void ToggleUiStyle()
        {
            _usePolishedUi = !_usePolishedUi;
            _uiBuilt = false;
            _transitioning = false;
            PlayButton = null;
            _bgGradientTop = null;
            _bgGradientBottom = null;
            _bgVignette = null;
            _bgAccentGlow = null;
            _bgFlat = null;
            _detailsPanelBg = null;
            _bgEmblem = null;
            BuildUi();
            ApplyCaptainInstant(_index);
            Canvas.ForceUpdateCanvases();
            ApplyPoseSprite(_poseA, CurrentCaptain());
        }

        V3CaptainId CurrentCaptain() => V3CaptainCatalog.MenuOrder[_index];

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
            if (PlayButton == null)
                return;
            PlayButton.interactable = !V3CaptainCatalog.IsLocked(CurrentCaptain());
        }

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

        static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

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
            V3MainMenuTypography.ApplyBody(tmp);
            return tmp;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        sealed class CharacterSelectSwipeRelay : MonoBehaviour, IBeginDragHandler, IEndDragHandler
        {
            V3MainMenuCharacterSelect _owner;
            Vector2 _start;

            public void Bind(V3MainMenuCharacterSelect owner) => _owner = owner;

            public void OnBeginDrag(PointerEventData eventData) => _start = eventData.position;

            public void OnEndDrag(PointerEventData eventData)
            {
                if (_owner == null)
                    return;
                var delta = eventData.position.x - _start.x;
                if (Mathf.Abs(delta) < 48f)
                    return;
                _owner.Step(delta < 0f ? 1 : -1);
            }
        }
    }
}
