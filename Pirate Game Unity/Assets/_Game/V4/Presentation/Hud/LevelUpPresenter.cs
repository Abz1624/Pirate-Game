using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The level-up cards (M14.11; port of V3's LevelUpModal as V4 used it through LegacyLevelUpPresenter): the panel on
    /// the run canvas laid out by the kept card UI factory (three card views with their reveal, Skip and Refresh), the
    /// texts from <see cref="OfferCards"/>, picks by click or the 1/2/3 keys. A pick hides the panel and answers the
    /// progression module; Skip hides the panel too (V3 left it up after skipping the last level-up, register R-83,
    /// which V4 hid from a frame slot). Tools pick and skip through <see cref="IOfferChoice"/>.
    /// </summary>
    public sealed class LevelUpPresenter : MonoBehaviour, IOfferPresenter, IOfferChoice
    {
        public const string PanelName = "LevelUpPanel";

        ConfigSetRuntime _config;
        GameObject _panel;
        LevelUpUiFactory.ModalWiring _ui;
        readonly List<Offer> _offers = new List<Offer>();
        readonly List<LevelOffer> _cards = new List<LevelOffer>();
        Action<Offer> _selected;
        Action _skipped, _refreshed;
        int _refreshesRemaining;
        bool _showing;

        // Demo (30 Sep 2026): the art team's UpgradeSelection cards (HUD_SaltWind) instead of the generated ones.
        Transform _art;
        IconCatalog _icons;
        readonly List<Button> _artButtons = new List<Button>();
        public bool UsesArtCards => _art != null;

        public bool IsShowing => _showing && _panel != null && _panel.activeSelf;
        public IReadOnlyList<Offer> Shown => _offers;
        public IReadOnlyList<LevelOffer> Cards => _cards;
        public GameObject Panel => _panel;
        public Button SkipButton => _ui.rejectButton;

        /// <summary>Takes the run canvas's level-up panel (or makes one) and lays out the cards.</summary>
        public static LevelUpPresenter Create(Canvas canvas, ConfigSetRuntime config)
        {
            if (canvas == null) return null;
            var existing = canvas.transform.Find(PanelName);
            GameObject panel;
            if (existing != null) panel = existing.gameObject;
            else
            {
                panel = new GameObject(PanelName, typeof(RectTransform));
                panel.transform.SetParent(canvas.transform, false);
            }
            var presenter = panel.GetComponent<LevelUpPresenter>();
            if (presenter == null) presenter = panel.AddComponent<LevelUpPresenter>();
            presenter.Bind(config);
            return presenter;
        }

        public void Bind(ConfigSetRuntime config)
        {
            _config = config;
            _panel = gameObject;
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
            }
            _ui = LevelUpUiFactory.Rebuild(transform);
            HideNow();
        }

        /// <summary>
        /// Shows the offers on the art team's cards (Card_1.. under UpgradeSelection: Background by rarity, Icon, Title,
        /// Description); this panel keeps only its Skip and Refresh buttons. The cards' layout is the artists'.
        /// </summary>
        public void UseArtCards(Transform upgradeSelection, IconCatalog icons)
        {
            _art = upgradeSelection;
            _icons = icons;
            _artButtons.Clear();
            if (_art == null) return;
            var row = _art.Find("CardRow") ?? _art;
            for (var i = 0; i < row.childCount; i++)
            {
                var b = row.GetChild(i).GetComponent<Button>();
                if (b != null) _artButtons.Add(b);
            }
            _artButtons.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            _art.gameObject.SetActive(false);
        }

        void ShowArtCards()
        {
            _art.gameObject.SetActive(true);
            _art.SetAsLastSibling();
            var cardArt = RoguePirates.V4.Foundation.UI.LevelUpCardArtSettings.Load();
            for (var i = 0; i < _artButtons.Count; i++)
            {
                var b = _artButtons[i];
                var has = i < _cards.Count && i < 3;
                b.gameObject.SetActive(has);
                b.onClick.RemoveAllListeners();
                if (!has) continue;
                var index = i;
                b.onClick.AddListener(() => Pick(index));
                b.interactable = true;
                var card = _cards[i];
                var offer = _offers[i];
                var bg = b.transform.Find("Background")?.GetComponent<Image>();
                if (bg != null && cardArt != null && cardArt.TryGet(card.rarity, out var sprite, out _) && sprite != null) bg.sprite = sprite;
                var icon = b.transform.Find("Background/Icon")?.GetComponent<Image>();
                var shadow = b.transform.Find("Background/Shadow")?.GetComponent<Image>();
                var iconSprite = IconOf(offer);
                foreach (var img in new[] { icon, shadow })
                {
                    if (img == null) continue;
                    img.gameObject.SetActive(iconSprite != null);
                    if (iconSprite != null) { img.sprite = iconSprite; img.preserveAspect = true; }
                }
                var title = b.transform.Find("Background/Title")?.GetComponent<TextMeshProUGUI>();
                if (title != null) title.text = card.title;
                var description = b.transform.Find("Background/Description")?.GetComponent<TextMeshProUGUI>();
                if (description != null)
                {
                    var effect = card.effectLines != null && card.effectLines.Length > 0 ? string.Join("\n", card.effectLines) : card.description;
                    var level = card.LevelLabel;
                    description.text = string.IsNullOrEmpty(level) ? effect : level + "\n" + effect;
                }
            }
        }

        Sprite IconOf(Offer offer)
        {
            switch (offer.Kind)
            {
                case OfferKind.WeaponUpgrade:
                case OfferKind.NewWeapon:
                case OfferKind.WeaponEvolution:
                    // The weapon's card prefab (the artists' icon) first, then the HUD icon.
                    var cardIcon = SaltWindHudSettings.Load()?.WeaponCardIcon(offer.Id);
                    return cardIcon != null ? cardIcon : _icons?.Weapon(offer.Id);
                case OfferKind.NewRunItem: return _icons?.Item(offer.Id);
                default: return null;
            }
        }

        public bool TryShow(List<Offer> offers, Action<Offer> selected, Action skipped, Action refreshed, int refreshesRemaining)
        {
            if (_art == null && (_ui.cardButtons == null || _ui.cardButtons.Length == 0)) return false;
            _offers.Clear();
            _cards.Clear();
            if (offers != null)
                foreach (var o in offers)
                {
                    _offers.Add(o);
                    _cards.Add(OfferCards.ToCard(o, _config));
                }
            _selected = selected;
            _skipped = skipped;
            _refreshed = refreshed;
            _refreshesRemaining = refreshesRemaining;
            _showing = true;
            transform.SetAsLastSibling(); // V3 EnsureVisibleOnTop
            _panel.SetActive(true);
            if (_art != null)
            {
                // Only Skip and Refresh stay on this panel: its cards, title, hint and dimming belong to the generated look.
                foreach (Transform child in transform)
                    child.gameObject.SetActive(child == (_ui.rejectButton != null ? _ui.rejectButton.transform : null) || child == (_ui.refreshButton != null ? _ui.refreshButton.transform : null) || IsAncestorOfButtons(child));
                var panelImage = GetComponent<Image>();
                if (panelImage != null) panelImage.enabled = false;
                if (_ui.headerTitle != null) _ui.headerTitle.gameObject.SetActive(false);
                if (_ui.headerSubtitle != null) _ui.headerSubtitle.gameObject.SetActive(false);
                foreach (var t in GetComponentsInChildren<TextMeshProUGUI>(true))
                    if (t.text != null && t.text.Contains("pick a card")) t.gameObject.SetActive(false);
                ShowArtCards();
                // Demo: no Skip or Refresh on the art cards (Skip() and the refresh still work for tools and tests).
                SetupButton(_ui.rejectButton, false, "Skip", Skip);
                SetupButton(_ui.refreshButton, false, "Refresh", OnRefresh);
                return true;
            }

            for (var i = 0; i < 3; i++)
            {
                var has = i < _cards.Count;
                var button = i < _ui.cardButtons.Length ? _ui.cardButtons[i] : null;
                if (button != null)
                {
                    button.gameObject.SetActive(has);
                    button.onClick.RemoveAllListeners();
                    if (has)
                    {
                        var index = i;
                        button.onClick.AddListener(() => Pick(index));
                    }
                }
                if (!has) continue;
                var view = _ui.cardViews != null && i < _ui.cardViews.Length ? _ui.cardViews[i] : null;
                if (view != null)
                {
                    view.Bind(_cards[i], i);
                    view.PlayReveal(i * 0.07f);
                }
            }

            if (_ui.acceptButton != null) _ui.acceptButton.gameObject.SetActive(false);
            SetupButton(_ui.rejectButton, skipped != null, "Skip", Skip);
            SetupButton(_ui.refreshButton, refreshed != null && refreshesRemaining > 0, $"Refresh ({refreshesRemaining} left)", OnRefresh);
            return true;
        }

        static void SetupButton(Button button, bool show, string label, UnityEngine.Events.UnityAction onClick)
        {
            if (button == null) return;
            button.gameObject.SetActive(show);
            if (!show) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(onClick);
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;
        }

        public void Hide()
        {
            HideNow();
            _selected = null;
            _skipped = null;
            _refreshed = null;
        }

        bool IsAncestorOfButtons(Transform t)
        {
            foreach (var b in new[] { _ui.rejectButton, _ui.refreshButton })
                if (b != null && b.transform.IsChildOf(t)) return true;
            return false;
        }

        void HideNow()
        {
            _showing = false;
            if (_panel != null) _panel.SetActive(false);
            if (_art != null) _art.gameObject.SetActive(false);
        }

        /// <summary>V3 Pick: hides the cards and answers with the chosen offer.</summary>
        public void Pick(int index)
        {
            if (!IsShowing || index < 0 || index >= _offers.Count) return;
            var selected = _selected;
            var offer = _offers[index];
            HideNow();
            _skipped = null;
            selected?.Invoke(offer);
        }

        /// <summary>V3 OnSkipClicked, with the cards hidden (R-83); a next pending level-up shows them again.</summary>
        public void Skip()
        {
            if (!IsShowing) return;
            var skipped = _skipped;
            HideNow();
            skipped?.Invoke();
        }

        void OnRefresh()
        {
            if (_refreshesRemaining <= 0) return;
            _refreshed?.Invoke();
        }

        void Update()
        {
            if (!IsShowing) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) Pick(0);
            else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) Pick(1);
            else if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) Pick(2);
        }
    }
}
