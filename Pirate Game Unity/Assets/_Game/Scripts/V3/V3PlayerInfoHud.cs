using HighSeas.Player;
using HighSeas.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Top-left captain portrait, HP/XP bars, name, and level (art-driven).</summary>
    public sealed class V3PlayerInfoHud : MonoBehaviour
    {
        const int LayoutVersion = 2;
        const float EdgeInset = 12f;
        const float PortraitSize = 108f;
        const float PortraitToBarsGap = 18f;
        const float BarTrackWidth = 280f;
        const float BarHeight = 38f;
        const float BarRowSpacing = 7f;
        const float StatIconSize = 44f;
        const float LevelBadgeSize = 46f;
        const float FillInsetHorizontal = 11f;
        const float FillInsetVertical = 7f;

        [SerializeField] int _layoutVersion;

        V3PlayerInfoHudArtSettings _art;
        Image _portrait;
        Image _hpFill;
        Image _xpFill;
        Image _hpShieldOverlay;
        TextMeshProUGUI _hpText;
        TextMeshProUGUI _xpText;
        TextMeshProUGUI _nameText;
        TextMeshProUGUI _levelText;
        V3CaptainId _portraitCaptain = (V3CaptainId)(-1);

        PlayerShipController _player;
        LevelSystem _levels;

        public static V3PlayerInfoHud Ensure(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<V3PlayerInfoHud>(true);
            if (existing != null)
            {
                existing.RebuildIfNeeded();
                return existing;
            }

            var root = new GameObject("v3_ui_player_info", typeof(RectTransform));
            root.transform.SetParent(canvas, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(EdgeInset, -EdgeInset);
            rect.sizeDelta = new Vector2(PortraitSize + PortraitToBarsGap + BarTrackWidth, 148f);

            var hud = root.AddComponent<V3PlayerInfoHud>();
            hud.RebuildIfNeeded();
            return hud;
        }

        void Awake() => RebuildIfNeeded();

        public void RebuildIfNeeded()
        {
            if (_layoutVersion == LayoutVersion && _portrait != null)
                return;
            ClearChildren(transform);
            _layoutVersion = LayoutVersion;
            BuildUi();
        }

        void BuildUi()
        {
            _art = V3PlayerInfoHudArtSettings.Load();

            var portraitRoot = CreateRect(transform, "portrait", Vector2.zero, new Vector2(PortraitSize, PortraitSize));
            portraitRoot.pivot = new Vector2(0f, 1f);
            portraitRoot.anchorMin = new Vector2(0f, 1f);
            portraitRoot.anchorMax = new Vector2(0f, 1f);
            portraitRoot.anchoredPosition = Vector2.zero;

            _portrait = CreateImage(portraitRoot, "icon", Vector2.zero, new Vector2(PortraitSize, PortraitSize), Color.white);
            Stretch(_portrait.rectTransform);
            _portrait.preserveAspect = true;

            var levelBadge = CreateImage(portraitRoot, "level_badge", Vector2.zero, new Vector2(LevelBadgeSize, LevelBadgeSize), Color.white);
            var badgeRt = levelBadge.rectTransform;
            badgeRt.anchorMin = new Vector2(0f, 0f);
            badgeRt.anchorMax = new Vector2(0f, 0f);
            badgeRt.pivot = new Vector2(0.5f, 0.5f);
            badgeRt.anchoredPosition = new Vector2(8f, 8f);
            if (_art != null && _art.levelBadgeOverlay != null)
            {
                levelBadge.sprite = _art.levelBadgeOverlay;
                levelBadge.preserveAspect = true;
            }
            else
                levelBadge.color = new Color(0.72f, 0.55f, 0.18f, 0.95f);

            _levelText = CreateText(levelBadge.transform, "level_value", "1", Vector2.zero, new Vector2(LevelBadgeSize - 4f, LevelBadgeSize - 4f), 20f, FontStyles.Bold);
            _levelText.color = Color.white;
            _levelText.outlineWidth = 0.22f;
            _levelText.outlineColor = new Color(0f, 0f, 0f, 0.8f);
            levelBadge.transform.SetAsLastSibling();

            var columnLeft = PortraitSize + PortraitToBarsGap;
            var barsBlockHeight = BarHeight * 2f + BarRowSpacing;
            var barsTopInset = Mathf.Max(0f, (PortraitSize - barsBlockHeight) * 0.5f);

            var column = CreateRect(transform, "stats_column", new Vector2(columnLeft, 0f), new Vector2(BarTrackWidth, PortraitSize + 44f));
            column.pivot = new Vector2(0f, 1f);
            column.anchorMin = new Vector2(0f, 1f);
            column.anchorMax = new Vector2(0f, 1f);
            column.anchoredPosition = new Vector2(columnLeft, 0f);

            BuildStatBar(column, "hp_row", new Vector2(0f, -barsTopInset), out _hpFill, out _hpText, out _hpShieldOverlay, true);
            BuildStatBar(column, "xp_row", new Vector2(0f, -barsTopInset - BarHeight - BarRowSpacing), out _xpFill, out _xpText, out _, false);

            var namePlateY = -barsTopInset - barsBlockHeight - 6f;
            var namePlate = CreateImage(column, "name_plate", new Vector2(0f, namePlateY), new Vector2(BarTrackWidth, 34f), new Color(0f, 0f, 0f, 0.42f));
            var plateRt = namePlate.rectTransform;
            plateRt.pivot = new Vector2(0f, 1f);
            plateRt.anchorMin = new Vector2(0f, 1f);
            plateRt.anchorMax = new Vector2(0f, 1f);
            plateRt.anchoredPosition = new Vector2(0f, namePlateY);

            _nameText = CreateText(namePlate.transform, "captain_name", "Captain", Vector2.zero, Vector2.zero, 21f, FontStyles.Bold);
            Stretch(_nameText.rectTransform);
            _nameText.rectTransform.offsetMin = new Vector2(10f, 2f);
            _nameText.rectTransform.offsetMax = new Vector2(-8f, -2f);
            _nameText.alignment = TextAlignmentOptions.MidlineLeft;
            _nameText.color = Color.white;
            _nameText.outlineWidth = 0.16f;
            _nameText.outlineColor = new Color(0f, 0f, 0f, 0.55f);

            RefreshPortrait();
        }

        void BuildStatBar(
            RectTransform parent,
            string name,
            Vector2 anchoredPos,
            out Image fill,
            out TextMeshProUGUI valueText,
            out Image shieldOverlay,
            bool isHp)
        {
            var row = CreateRect(parent, name, anchoredPos, new Vector2(BarTrackWidth, BarHeight));
            row.pivot = new Vector2(0f, 1f);
            row.anchorMin = new Vector2(0f, 1f);
            row.anchorMax = new Vector2(0f, 1f);
            row.anchoredPosition = anchoredPos;

            var trackGo = new GameObject("track", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            trackGo.transform.SetParent(row, false);
            var trackRt = trackGo.GetComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0f, 0.5f);
            trackRt.anchorMax = new Vector2(0f, 0.5f);
            trackRt.pivot = new Vector2(0f, 0.5f);
            trackRt.anchoredPosition = Vector2.zero;
            trackRt.sizeDelta = new Vector2(BarTrackWidth, BarHeight);

            var track = trackGo.GetComponent<Image>();
            track.raycastTarget = false;
            if (_art != null && _art.emptyBar != null)
            {
                track.sprite = _art.emptyBar;
                track.type = Image.Type.Simple;
                track.color = Color.white;
            }
            else
                track.color = RunHudTheme.BarBackground;

            var icon = CreateImage(row, "icon", Vector2.zero, new Vector2(StatIconSize, StatIconSize), Color.white);
            var iconRt = icon.rectTransform;
            iconRt.anchorMin = new Vector2(0f, 0.5f);
            iconRt.anchorMax = new Vector2(0f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = new Vector2(StatIconSize * 0.48f, 0f);
            iconRt.SetAsLastSibling();
            if (_art != null)
            {
                var iconSprite = isHp ? _art.hpIcon : _art.xpIcon;
                if (iconSprite != null)
                {
                    icon.sprite = iconSprite;
                    icon.preserveAspect = true;
                }
            }

            var fillGo = CreateImage(trackGo.transform, "fill", Vector2.zero, Vector2.zero, Color.white);
            fill = fillGo;
            Stretch(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(FillInsetHorizontal, FillInsetVertical);
            fill.rectTransform.offsetMax = new Vector2(-FillInsetHorizontal, -FillInsetVertical);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            if (_art != null)
            {
                var fillSprite = isHp ? _art.fullHpBar : _art.fullXpBar;
                if (fillSprite != null)
                    fill.sprite = fillSprite;
            }
            else
                fill.color = isHp ? RunHudTheme.HullFill : RunHudTheme.XpFill;

            shieldOverlay = null;
            if (isHp && _art != null && _art.shieldOverlayPattern != null)
            {
                var shield = CreateImage(trackGo.transform, "shield_pattern", Vector2.zero, Vector2.zero, new Color(1f, 1f, 1f, 0.28f));
                shieldOverlay = shield;
                Stretch(shield.rectTransform);
                shield.rectTransform.offsetMin = fill.rectTransform.offsetMin;
                shield.rectTransform.offsetMax = fill.rectTransform.offsetMax;
                shield.sprite = _art.shieldOverlayPattern;
                shield.type = Image.Type.Filled;
                shield.fillMethod = Image.FillMethod.Horizontal;
                shield.fillOrigin = (int)Image.OriginHorizontal.Left;
                shield.fillAmount = 1f;
            }

            valueText = CreateText(trackGo.transform, "value", "—", Vector2.zero, Vector2.zero, 17f, FontStyles.Bold);
            Stretch(valueText.rectTransform);
            valueText.rectTransform.offsetMin = new Vector2(4f, 0f);
            valueText.rectTransform.offsetMax = new Vector2(-4f, 0f);
            valueText.alignment = TextAlignmentOptions.Center;
            valueText.color = Color.white;
            valueText.outlineWidth = 0.22f;
            valueText.outlineColor = new Color(0f, 0f, 0f, 0.85f);
            valueText.enableAutoSizing = true;
            valueText.fontSizeMin = 13f;
            valueText.fontSizeMax = 17f;
            valueText.transform.SetAsLastSibling();
        }

        void Update()
        {
            if (_player == null || _levels == null)
                BindSystems();
            Refresh();
        }

        void BindSystems()
        {
            if (_player == null)
                _player = FindFirstObjectByType<PlayerShipController>();
            if (_levels == null)
                _levels = LevelSystem.Instance;
        }

        void RefreshPortrait()
        {
            if (_portrait == null)
                return;

            var captain = V3RunContext.IsActive ? V3RunContext.CaptainId : V3CaptainSelection.Selected;
            if (captain == _portraitCaptain && _nameText != null)
                return;

            _portraitCaptain = captain;
            Sprite sprite = _art != null ? _art.GetCaptainIcon(captain) : null;
            if (sprite != null)
            {
                _portrait.sprite = sprite;
                _portrait.color = Color.white;
            }
            else
            {
                _portrait.sprite = null;
                _portrait.color = CaptainPlaceholderColor(captain);
            }

            if (_nameText != null)
                _nameText.text = V3CaptainCatalog.GetDisplayTitle(captain);
        }

        static Color CaptainPlaceholderColor(V3CaptainId captain)
        {
            return captain switch
            {
                V3CaptainId.Brine => new Color(0.2f, 0.55f, 0.85f),
                V3CaptainId.Richie => new Color(0.85f, 0.72f, 0.2f),
                V3CaptainId.Flint => new Color(0.95f, 0.45f, 0.15f),
                V3CaptainId.Ember => new Color(0.9f, 0.25f, 0.2f),
                V3CaptainId.Roxie => new Color(0.55f, 0.35f, 0.9f),
                _ => new Color(0.35f, 0.65f, 0.45f)
            };
        }

        void Refresh()
        {
            RefreshPortrait();

            if (_levels != null)
            {
                var displayLevel = Mathf.Max(1, _levels.Level);
                if (_levelText != null)
                    _levelText.text = displayLevel.ToString();

                var xpMax = Mathf.Max(1f, _levels.XPToNext);
                var xpCurrent = Mathf.Clamp(_levels.CurrentXP, 0f, xpMax);
                if (_xpText != null)
                    _xpText.text = $"{Mathf.FloorToInt(xpCurrent):N0} / {Mathf.FloorToInt(xpMax):N0}";
                if (_xpFill != null)
                    _xpFill.fillAmount = xpCurrent / xpMax;
            }

            if (_player != null && _player.Damage != null)
            {
                var hp = _player.Damage.CurrentHP;
                var maxHp = Mathf.Max(1f, _player.Damage.MaxHP);
                if (_hpText != null)
                    _hpText.text = $"{Mathf.CeilToInt(hp):N0} / {Mathf.CeilToInt(maxHp):N0}";
                var pct = Mathf.Clamp01(hp / maxHp);
                if (_hpFill != null)
                {
                    _hpFill.fillAmount = pct;
                    if (_art == null || _art.fullHpBar == null)
                    {
                        _hpFill.color = pct <= 0.25f
                            ? Color.Lerp(RunHudTheme.HullDanger, RunHudTheme.HullFill, Mathf.PingPong(Time.time * 3f, 1f))
                            : RunHudTheme.HullFill;
                    }
                }

                if (_hpShieldOverlay != null)
                    _hpShieldOverlay.fillAmount = pct;
            }
        }

        static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Destroy(child);
                else
                    DestroyImmediate(child);
            }
        }

        static RectTransform CreateRect(Transform parent, string name, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        static Image CreateImage(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (size != Vector2.zero)
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }

            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string text,
            Vector2 pos,
            Vector2 size,
            float fontSize,
            FontStyles style)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            if (size != Vector2.zero)
            {
                rt.anchoredPosition = pos;
                rt.sizeDelta = size;
            }

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            return tmp;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
