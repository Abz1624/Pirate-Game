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
            if (glow != null) glow.color = style.glow;
            if (border != null) border.color = style.panelBorder;
            if (panelFill != null) panelFill.color = style.panelFill;
            if (topStripe != null) topStripe.color = style.accent;
            if (rarityBadge != null) rarityBadge.color = style.accent;

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
                rarityText.text = offer.rarity.ToString().ToUpperInvariant();
                rarityText.color = style.rarityText;
            }

            if (descriptionText != null)
            {
                descriptionText.text = offer.description;
                descriptionText.color = new Color(0.82f, 0.87f, 0.93f, 1f);
            }

            transform.localScale = _baseScale;
            _hoverT = 0f;
        }

        public void PlayReveal(float delay)
        {
            StopAllCoroutines();
            StartCoroutine(RevealRoutine(delay));
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

        static string CategoryForKind(LevelOfferKind kind) => kind switch
        {
            LevelOfferKind.WeaponUpgrade => "WEAPON UPGRADE",
            LevelOfferKind.NewWeapon => "NEW WEAPON",
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
