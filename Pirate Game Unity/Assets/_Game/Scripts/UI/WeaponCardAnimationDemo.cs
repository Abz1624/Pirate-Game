using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.V3;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HighSeas.UI
{
    /// <summary>Playtest visuals: G = scatter pop-in cards, H = horizontal auto-scroll strip (50+ cards).</summary>
    [DefaultExecutionOrder(50)]
    public sealed class WeaponCardAnimationDemo : MonoBehaviour
    {
        public const int ScrollCardCount = 52;
        const float ScatterPadding = 32f;
        const float ScatterGap = 22f;
        const float ScatterGlowPad = 18f;
        const float ScrollSpeed = 240f;
        const float ScrollCardScale = 0.38f;

        public static WeaponCardAnimationDemo Instance { get; private set; }

        RectTransform _root;
        RectTransform _scatterLayer;
        RectTransform _scrollLayer;
        RectTransform _scrollViewport;
        RectTransform _scrollContent;
        Image _backdrop;
        TextMeshProUGUI _hint;

        RunRng _rng;
        LevelUpSettings _levelUpSettings;
        DemoPacingSettings _demoPacing;
        GameDatabase _database;

        bool _scatterActive;
        bool _scrollActive;
        float _scrollOffset;
        float _scrollLoopWidth;
        float _scrollViewportWidth;

        public static WeaponCardAnimationDemo Ensure()
        {
            if (Instance != null)
                return Instance;

            var canvas = Object.FindFirstObjectByType<Canvas>();
            if (canvas == null)
                return null;

            var go = new GameObject("WeaponCardAnimationDemo", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            go.transform.SetAsLastSibling();
            Instance = go.AddComponent<WeaponCardAnimationDemo>();
            return Instance;
        }

        void Awake()
        {
            Instance = this;
            BuildUi();
            LoadData();
            SetLayersVisible(false);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void LoadData()
        {
            _database = GameDatabaseLoader.Load();
            _levelUpSettings = Resources.Load<LevelUpSettings>("LevelUpSettings");
            _demoPacing = Resources.Load<DemoPacingSettings>("DemoPacingSettings");
            _rng = new RunRng(System.Environment.TickCount);
        }

        void BuildUi()
        {
            UiCanvasConfigurator.ConfigureFromComponent(this);
            _root = transform as RectTransform;
            Stretch(_root);

            var overlayCanvas = gameObject.GetComponent<Canvas>();
            if (overlayCanvas == null)
                overlayCanvas = gameObject.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 320;
            if (gameObject.GetComponent<GraphicRaycaster>() == null)
                gameObject.AddComponent<GraphicRaycaster>();

            _backdrop = gameObject.AddComponent<Image>();
            _backdrop.color = new Color(0.02f, 0.05f, 0.1f, 0.72f);
            _backdrop.raycastTarget = true;

            _scatterLayer = CreateChild("ScatterLayer", _root);
            Stretch(_scatterLayer);

            _scrollLayer = CreateChild("ScrollLayer", _root);
            Stretch(_scrollLayer);

            _scrollViewport = CreateChild("ScrollViewport", _scrollLayer);
            Stretch(_scrollViewport);
            _scrollViewport.gameObject.AddComponent<RectMask2D>();

            _scrollContent = CreateChild("ScrollContent", _scrollViewport);
            var contentRt = _scrollContent;
            contentRt.anchorMin = new Vector2(0f, 0.5f);
            contentRt.anchorMax = new Vector2(0f, 0.5f);
            contentRt.pivot = new Vector2(0f, 0.5f);
            contentRt.anchoredPosition = Vector2.zero;

            var layout = _scrollContent.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            _scrollContent.gameObject.AddComponent<ContentSizeFitter>().horizontalFit =
                ContentSizeFitter.FitMode.PreferredSize;

            _hint = CreateHint(_root);
        }

        static TextMeshProUGUI CreateHint(RectTransform parent)
        {
            var go = new GameObject("Hint", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 24f);
            rt.sizeDelta = new Vector2(900f, 40f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 16f;
            tmp.color = new Color(0.78f, 0.86f, 0.95f, 0.9f);
            tmp.text = string.Empty;
            return tmp;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.gKey.wasPressedThisFrame)
                PlayScatter();
            else if (keyboard.hKey.wasPressedThisFrame)
                PlayScroll();
            else if (keyboard.escapeKey.wasPressedThisFrame && (_scatterActive || _scrollActive))
                HideAll();

            if (_scrollActive)
                TickScroll();
        }

        void TickScroll()
        {
            if (_scrollContent == null || _scrollLoopWidth <= 0f)
                return;

            _scrollOffset += ScrollSpeed * Time.unscaledDeltaTime;
            while (_scrollOffset >= _scrollLoopWidth)
                _scrollOffset -= _scrollLoopWidth;

            _scrollContent.anchoredPosition = new Vector2(_scrollViewportWidth - _scrollOffset, 0f);
        }

        public void PlayScatter()
        {
            EnsureData();
            HideScroll();
            ClearChildren(_scatterLayer);

            var width = GetOverlayWidth();
            var height = GetOverlayHeight();

            var positions = BuildScatterGrid(width, height, out var cardScale);
            _scatterActive = positions.Count > 0;
            if (!_scatterActive)
            {
                SetLayersVisible(false);
                return;
            }

            SetLayersVisible(true);
            Canvas.ForceUpdateCanvases();

            var staggerMax = 0.45f;

            for (var i = 0; i < positions.Count; i++)
            {
                var view = LevelUpUiFactory.CreateWeaponCard(_scatterLayer, i, interactable: false);
                var rt = view.transform as RectTransform;
                if (rt == null) continue;

                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = positions[i];
                rt.localScale = Vector3.one * cardScale;

                var offer = RollRandomWeaponOffer();
                view.Bind(offer);
                DisableCardRaycasts(view);
                view.PlayPopInFromZero(Random.Range(0f, staggerMax), 0.34f);
            }

            if (_hint != null)
                _hint.text = "G — scatter weapon cards · H — scroll strip · Esc — close";
        }

        public void PlayScroll()
        {
            EnsureData();
            HideScatter();
            _scrollActive = true;
            SetLayersVisible(true);
            ClearChildren(_scrollContent);

            _scrollViewportWidth = GetOverlayWidth();

            var halfCount = ScrollCardCount;
            for (var pass = 0; pass < 2; pass++)
            {
                for (var i = 0; i < halfCount; i++)
                {
                    var index = pass * halfCount + i;
                    var view = LevelUpUiFactory.CreateWeaponCard(_scrollContent, index, interactable: false);
                    var rt = view.transform as RectTransform;
                    if (rt != null)
                        rt.localScale = Vector3.one * ScrollCardScale;

                    var le = view.gameObject.GetComponent<LayoutElement>() ??
                             view.gameObject.AddComponent<LayoutElement>();
                    le.preferredWidth = LevelUpUiFactory.CardWidth;
                    le.preferredHeight = LevelUpUiFactory.CardHeight;

                    view.Bind(RollRandomWeaponOffer());
                    DisableCardRaycasts(view);
                }
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_scrollContent);
            _scrollLoopWidth = _scrollContent.rect.width * 0.5f;
            if (_scrollLoopWidth <= 0f)
                _scrollLoopWidth = halfCount * (LevelUpUiFactory.CardWidth * ScrollCardScale + 20f);

            _scrollOffset = 0f;
            _scrollContent.anchoredPosition = new Vector2(_scrollViewportWidth, 0f);
            Canvas.ForceUpdateCanvases();
            if (_hint != null)
                _hint.text = $"H — {ScrollCardCount}+ cards scrolling (R→L) · G — scatter · Esc — close";
        }

        float GetOverlayWidth()
        {
            if (_root != null && _root.rect.width > 1f)
                return _root.rect.width;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                return canvas.pixelRect.width / Mathf.Max(canvas.scaleFactor, 0.01f);
            return UiCanvasConfigurator.ReferenceResolution.x;
        }

        float GetOverlayHeight()
        {
            if (_root != null && _root.rect.height > 1f)
                return _root.rect.height;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
                return canvas.pixelRect.height / Mathf.Max(canvas.scaleFactor, 0.01f);
            return UiCanvasConfigurator.ReferenceResolution.y;
        }

        List<Vector2> BuildScatterGrid(float width, float height, out float cardScale)
        {
            var positions = new List<Vector2>();
            cardScale = 0.4f;

            var usableW = width - ScatterPadding * 2f;
            var usableH = height - ScatterPadding * 2f;

            for (var scaleTry = 0.5f; scaleTry >= 0.2f; scaleTry -= 0.025f)
            {
                var cellW = LevelUpUiFactory.CardWidth * scaleTry + ScatterGlowPad;
                var cellH = LevelUpUiFactory.CardHeight * scaleTry + ScatterGlowPad;
                var pitchX = cellW + ScatterGap;
                var pitchY = cellH + ScatterGap;
                var cols = Mathf.Max(1, Mathf.FloorToInt((usableW + ScatterGap) / pitchX));
                var rows = Mathf.Max(1, Mathf.FloorToInt((usableH + ScatterGap) / pitchY));
                if (cols * rows < 6)
                    continue;

                cardScale = scaleTry;
                positions.Clear();

                var totalW = cols * pitchX - ScatterGap;
                var totalH = rows * pitchY - ScatterGap;
                var startX = -totalW * 0.5f + cellW * 0.5f;
                var startY = totalH * 0.5f - cellH * 0.5f;

                for (var row = 0; row < rows; row++)
                {
                    for (var col = 0; col < cols; col++)
                    {
                        var x = startX + col * pitchX;
                        var y = startY - row * pitchY;
                        positions.Add(new Vector2(x, y));
                    }
                }

                return positions;
            }

            return positions;
        }

        LevelOffer RollRandomWeaponOffer()
        {
            if (_database == null)
                return default;

            var pool = V3WeaponPlaytestCatalog.All;
            if (pool == null || pool.Length == 0)
                return default;

            var id = pool[_rng.LootInt(0, pool.Length)];
            var def = _database.GetWeapon(id);
            if (def == null)
                return default;

            var rarity = _rng.RollRarity(2.5f);
            var level = _rng.LootInt(1, 6);
            if (_levelUpSettings != null && _demoPacing != null)
                return LevelOfferGenerator.BuildWeaponUpgradeOffer(
                    def, level, rarity, _levelUpSettings, _demoPacing, _rng);

            var offer = LevelOfferGenerator.BuildNewWeaponOffer(def);
            offer.rarity = rarity;
            offer.currentLevel = level;
            offer.nextLevel = level + 1;
            offer.kind = LevelOfferKind.WeaponUpgrade;
            offer.categoryLabel = "Weapon Upgrade";
            return offer;
        }

        void HideAll()
        {
            HideScatter();
            HideScroll();
            SetLayersVisible(false);
            if (_hint != null)
                _hint.text = string.Empty;
        }

        void HideScatter()
        {
            _scatterActive = false;
            ClearChildren(_scatterLayer);
        }

        void HideScroll()
        {
            _scrollActive = false;
            _scrollOffset = 0f;
            ClearChildren(_scrollContent);
        }

        void SetLayersVisible(bool visible)
        {
            var show = visible || _scatterActive || _scrollActive;
            if (_backdrop != null)
            {
                _backdrop.enabled = show;
                _backdrop.raycastTarget = show;
            }
            if (_scatterLayer != null)
                _scatterLayer.gameObject.SetActive(_scatterActive);
            if (_scrollLayer != null)
                _scrollLayer.gameObject.SetActive(_scrollActive);
            if (_hint != null)
                _hint.gameObject.SetActive(show);
            transform.SetAsLastSibling();
        }

        void EnsureData()
        {
            if (_database == null)
                LoadData();
            transform.SetAsLastSibling();
        }

        static void DisableCardRaycasts(LevelUpCardView view)
        {
            if (view == null) return;
            var graphics = view.GetComponentsInChildren<Graphic>(true);
            for (var i = 0; i < graphics.Length; i++)
                graphics[i].raycastTarget = false;
        }

        static RectTransform CreateChild(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        static void ClearChildren(RectTransform root)
        {
            if (root == null) return;
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }
    }
}
