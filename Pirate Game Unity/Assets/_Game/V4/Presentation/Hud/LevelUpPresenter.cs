using System;
using System.Collections.Generic;
using HighSeas.UI;
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

        public bool TryShow(List<Offer> offers, Action<Offer> selected, Action skipped, Action refreshed, int refreshesRemaining)
        {
            if (_ui.cardButtons == null || _ui.cardButtons.Length == 0) return false;
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

        void HideNow()
        {
            _showing = false;
            if (_panel != null) _panel.SetActive(false);
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
