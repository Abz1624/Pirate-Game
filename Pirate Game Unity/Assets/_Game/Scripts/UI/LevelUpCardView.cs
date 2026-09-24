using HighSeas.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace HighSeas.UI
{
    [DisallowMultipleComponent]
    public sealed class LevelUpCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] Image glow;
        [SerializeField] Image border;
        [SerializeField] Image panelFill;
        [SerializeField] Image topStripe;
        [SerializeField] Image rarityBadge;
        [SerializeField] TextMeshProUGUI categoryText;
        [SerializeField] TextMeshProUGUI titleText;
        [SerializeField] TextMeshProUGUI levelText;
        [SerializeField] TextMeshProUGUI rarityText;
        [SerializeField] TextMeshProUGUI descriptionText;

        RectTransform _rect;
        Vector3 _baseScale;
        float _hoverT;

        void Awake()
        {
            _rect = transform as RectTransform;
            _baseScale = transform.localScale;
        }

        public void Bind(LevelOffer offer, int hotkeyIndex = 0)
        {
            var style = LevelUpRarityStyles.For(offer.rarity);
            var usedArt = ApplyRarityArt(offer.rarity);
            if (!usedArt)
            {
                if (glow != null) glow.color = style.glow;
                if (border != null)
                {
                    border.enabled = true;
                    border.color = style.panelBorder;
                }
                if (panelFill != null) panelFill.color = style.panelFill;
                if (topStripe != null)
                {
                    topStripe.enabled = true;
                    topStripe.color = style.accent;
                }
                if (rarityBadge != null)
                {
                    rarityBadge.enabled = true;
                    rarityBadge.color = style.accent;
                }
            }

            if (categoryText != null)
            {
                categoryText.text = string.IsNullOrEmpty(offer.categoryLabel)
                    ? CategoryForKind(offer.kind)
                    : offer.categoryLabel.ToUpperInvariant();
                categoryText.color = new Color(0.75f, 0.8f, 0.88f, 0.95f);
            }

            if (titleText != null)
            {
                titleText.text = offer.title;
                titleText.color = style.title;
            }

            if (levelText != null)
            {
                levelText.text = offer.LevelLabel;
                levelText.color = new Color(0.88f, 0.92f, 0.98f, 1f);
            }

            if (rarityText != null)
            {
                if (offer.hideRarity)
                {
                    rarityText.gameObject.SetActive(false);
                }
                else
                {
                    rarityText.gameObject.SetActive(true);
                    rarityText.text = offer.rarity.ToString().ToUpperInvariant();
                    rarityText.color = style.rarityText;
                }
            }

            if (descriptionText != null)
            {
                descriptionText.text = offer.effectLines != null && offer.effectLines.Length > 0
                    ? string.Join("\n", offer.effectLines)
                    : offer.description;
                descriptionText.color = new Color(0.82f, 0.87f, 0.93f, 1f);
            }

            if (usedArt)
                ApplyArtCardTextLayout();

            transform.localScale = _baseScale;
            _hoverT = 0f;
        }

        void ApplyArtCardTextLayout()
        {
            const float horizontalInset = 38f;
            const float contentWidth = LevelUpUiFactory.CardWidth - horizontalInset * 2f;

            ConfigureTextRect(categoryText, new Vector2(0f, 116f), new Vector2(contentWidth, 18f));
            ConfigureTextRect(titleText, new Vector2(0f, 74f), new Vector2(contentWidth, 48f));
            ConfigureTextRect(levelText, new Vector2(0f, 34f), new Vector2(contentWidth, 22f));
            ConfigureTextRect(rarityText, new Vector2(0f, 4f), new Vector2(contentWidth, 22f));
            ConfigureTextRect(descriptionText, new Vector2(0f, -78f), new Vector2(contentWidth, 82f));

            if (titleText != null)
            {
                titleText.enableWordWrapping = true;
                titleText.enableAutoSizing = true;
                titleText.fontSizeMin = 14f;
                titleText.fontSizeMax = 22f;
                titleText.overflowMode = TextOverflowModes.Ellipsis;
            }

            if (descriptionText != null)
            {
                descriptionText.alignment = TextAlignmentOptions.Top;
                descriptionText.enableWordWrapping = true;
                descriptionText.enableAutoSizing = true;
                descriptionText.fontSizeMin = 10f;
                descriptionText.fontSizeMax = 12f;
                descriptionText.overflowMode = TextOverflowModes.Truncate;
                descriptionText.margin = new Vector4(2f, 2f, 2f, 2f);
            }
        }

        static void ConfigureTextRect(TextMeshProUGUI text, Vector2 anchoredPos, Vector2 size)
        {
            if (text == null)
                return;

            var rect = text.rectTransform;
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = size;
        }

        public void PlayReveal(float delay)
        {
            StopAllCoroutines();
            StartCoroutine(RevealRoutine(delay));
        }

        public void PlayPopInFromZero(float delay, float duration = 0.32f)
        {
            StopAllCoroutines();
            StartCoroutine(PopInFromZeroRoutine(delay, duration));
        }

        System.Collections.IEnumerator PopInFromZeroRoutine(float delay, float duration)
        {
            if (_rect == null) yield break;
            transform.localScale = Vector3.zero;
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            var t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var u = Mathf.Clamp01(t / duration);
                var eased = 1f - Mathf.Pow(1f - u, 3f);
                var overshoot = 1f + Mathf.Sin(u * Mathf.PI) * 0.02f;
                cg.alpha = eased;
                transform.localScale = _baseScale * (eased * overshoot);
                yield return null;
            }

            cg.alpha = 1f;
            transform.localScale = _baseScale;
        }

        System.Collections.IEnumerator RevealRoutine(float delay)
        {
            if (_rect == null) yield break;
            transform.localScale = _baseScale * 0.88f;
            var cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

            const float duration = 0.22f;
            var t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                var eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / duration), 3f);
                cg.alpha = eased;
                transform.localScale = Vector3.Lerp(_baseScale * 0.88f, _baseScale, eased);
                yield return null;
            }

            cg.alpha = 1f;
            transform.localScale = _baseScale;
        }

        void Update()
        {
            var target = 1f + _hoverT * 0.06f;
            transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * target, Time.unscaledDeltaTime * 14f);
        }

        public void OnPointerEnter(PointerEventData eventData) => _hoverT = 1f;

        public void OnPointerExit(PointerEventData eventData) => _hoverT = 0f;

        bool ApplyRarityArt(Rarity rarity)
        {
            var settings = LevelUpCardArtSettings.Load();
            if (settings == null || !settings.TryGet(rarity, out var background, out var effect))
                return false;

            if (panelFill != null && background != null)
            {
                panelFill.enabled = true;
                panelFill.sprite = background;
                panelFill.color = Color.white;
                panelFill.type = Image.Type.Simple;
                panelFill.preserveAspect = false;
                Stretch(panelFill.rectTransform);
            }

            if (glow != null && effect != null)
            {
                glow.enabled = true;
                glow.sprite = effect;
                glow.color = Color.white;
                glow.type = Image.Type.Simple;
                glow.preserveAspect = false;
                Stretch(glow.rectTransform);
                if (panelFill != null)
                    glow.transform.SetSiblingIndex(panelFill.transform.GetSiblingIndex() + 1);
            }
            else if (glow != null)
            {
                glow.sprite = null;
                glow.color = styleFallbackGlow(rarity);
            }

            if (border != null)
            {
                border.enabled = false;
                border.sprite = null;
            }

            if (topStripe != null)
                topStripe.enabled = false;

            if (rarityBadge != null)
                rarityBadge.enabled = false;

            var button = GetComponent<Button>();
            if (button != null && panelFill != null)
                button.targetGraphic = panelFill;

            return background != null || effect != null;
        }

        static Color styleFallbackGlow(Rarity rarity) => LevelUpRarityStyles.For(rarity).glow;

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;
        }

        static string CategoryForKind(LevelOfferKind kind) => kind switch
        {
            LevelOfferKind.WeaponUpgrade => "WEAPON UPGRADE",
            LevelOfferKind.NewWeapon => "NEW WEAPON",
            LevelOfferKind.WeaponEvolution => "WEAPON EVOLUTION",
            LevelOfferKind.FallbackHeal or LevelOfferKind.FallbackDamage or LevelOfferKind.FallbackMoveSpeed => "FALLBACK",
            LevelOfferKind.Continue => "NO UPGRADE",
            _ => "LOGBOOK"
        };

        public void WireReferences(
            Image glowImage,
            Image borderImage,
            Image fillImage,
            Image stripeImage,
            Image badgeImage,
            TextMeshProUGUI category,
            TextMeshProUGUI title,
            TextMeshProUGUI level,
            TextMeshProUGUI rarity,
            TextMeshProUGUI description)
        {
            glow = glowImage;
            border = borderImage;
            panelFill = fillImage;
            topStripe = stripeImage;
            rarityBadge = badgeImage;
            categoryText = category;
            titleText = title;
            levelText = level;
            rarityText = rarity;
            descriptionText = description;
        }
    }
}
