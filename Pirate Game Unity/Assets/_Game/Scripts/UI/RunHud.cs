using System;
using System.Collections.Generic;
using HighSeas.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.UI
{
    public sealed class LevelUpModal : MonoBehaviour
    {
        public static LevelUpModal Instance { get; private set; }

        [SerializeField] GameObject panel;
        [SerializeField] Button[] cardButtons;
        [SerializeField] TextMeshProUGUI[] cardTitles;
        [SerializeField] TextMeshProUGUI[] cardDescriptions;

        Action<LevelSystem.LevelOffer> _callback;
        List<LevelSystem.LevelOffer> _offers;

        void Awake()
        {
            Instance = this;
            EnsureWired();
            if (panel != null) panel.SetActive(false);
        }

        void Update()
        {
            if (panel == null || !panel.activeSelf || _offers == null) return;
            if (Input.GetKeyDown(KeyCode.Alpha1)) Pick(0);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Pick(1);
            if (Input.GetKeyDown(KeyCode.Alpha3)) Pick(2);
        }

        public bool TryShow(List<LevelSystem.LevelOffer> offers, Action<LevelSystem.LevelOffer> callback)
        {
            EnsureWired();
            if (!HasUsableCards())
                return false;

            Show(offers, callback);
            return true;
        }

        public void Show(List<LevelSystem.LevelOffer> offers, Action<LevelSystem.LevelOffer> callback)
        {
            EnsureWired();
            _offers = offers;
            _callback = callback;
            if (panel != null) panel.SetActive(true);
            if (cardButtons == null) return;

            for (var i = 0; i < cardButtons.Length; i++)
            {
                var idx = i;
                if (cardTitles != null && i < cardTitles.Length && i < offers.Count && cardTitles[i] != null)
                    cardTitles[i].text = offers[i].title;
                if (cardDescriptions != null && i < cardDescriptions.Length && i < offers.Count && cardDescriptions[i] != null)
                    cardDescriptions[i].text = offers[i].description;
                if (i < cardButtons.Length && cardButtons[i] != null)
                {
                    cardButtons[i].gameObject.SetActive(i < offers.Count);
                    cardButtons[i].onClick.RemoveAllListeners();
                    cardButtons[i].onClick.AddListener(() => Pick(idx));
                }
            }
        }

        void EnsureWired()
        {
            if (panel == null)
                panel = gameObject;

            if (cardButtons != null && cardButtons.Length > 0 && cardButtons[0] != null)
                return;

            var buttons = new Button[3];
            var titles = new TextMeshProUGUI[3];
            var descs = new TextMeshProUGUI[3];

            for (var i = 0; i < 3; i++)
            {
                var card = transform.Find($"Card{i}");
                if (card == null) continue;

                buttons[i] = card.GetComponent<Button>();
                var texts = card.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (texts.Length > 0)
                    titles[i] = texts[0];

                for (var t = 0; t < texts.Length; t++)
                {
                    if (texts[t].gameObject.name == "Desc")
                    {
                        descs[i] = texts[t];
                        break;
                    }
                }

                if (descs[i] == null && texts.Length > 1)
                    descs[i] = texts[1];
            }

            cardButtons = buttons;
            cardTitles = titles;
            cardDescriptions = descs;
        }

        bool HasUsableCards()
        {
            if (cardButtons == null) return false;
            for (var i = 0; i < cardButtons.Length; i++)
            {
                if (cardButtons[i] != null)
                    return true;
            }
            return false;
        }

        void Pick(int index)
        {
            if (_offers == null || index < 0 || index >= _offers.Count) return;
            if (panel != null) panel.SetActive(false);
            _callback?.Invoke(_offers[index]);
        }
    }

    public sealed class RunHud : MonoBehaviour
    {
        [SerializeField] TextMeshProUGUI hullText;
        [SerializeField] TextMeshProUGUI xpText;
        [SerializeField] TextMeshProUGUI timerText;
        [SerializeField] TextMeshProUGUI goldText;
        [SerializeField] TextMeshProUGUI levelText;
        [SerializeField] Slider hullSlider;
        [SerializeField] Slider xpSlider;

        Player.PlayerShipController _player;
        LevelSystem _levels;

        void Start()
        {
            _player = FindFirstObjectByType<Player.PlayerShipController>();
            _levels = LevelSystem.Instance;
            if (RunManager.Instance != null)
                RunManager.Instance.GoldChanged += _ => Refresh();
        }

        void Update() => Refresh();

        void Refresh()
        {
            if (RunManager.Instance == null) return;
            var elapsed = RunManager.Instance.ElapsedSeconds;
            var mins = Mathf.FloorToInt(elapsed / 60f);
            var secs = Mathf.FloorToInt(elapsed % 60f);
            if (timerText != null)
            {
                timerText.text = RunManager.Instance.IsOvertime
                    ? $"DREAD TIDE {mins:00}:{secs:00}"
                    : $"{mins:00}:{secs:00}";
                timerText.color = RunManager.Instance.IsOvertime ? Color.red : Color.white;
            }
            if (goldText != null) goldText.text = $"Gold: {RunManager.Instance.Gold}";
            if (_levels != null)
            {
                if (levelText != null) levelText.text = $"Lv {_levels.Level}";
                if (xpText != null) xpText.text = $"{Mathf.FloorToInt(_levels.CurrentXP)}/{Mathf.FloorToInt(_levels.XPToNext)}";
                if (xpSlider != null)
                {
                    xpSlider.maxValue = _levels.XPToNext;
                    xpSlider.value = _levels.CurrentXP;
                }
            }
            if (_player != null && _player.Damage != null)
            {
                if (hullText != null) hullText.text = $"Hull {Mathf.CeilToInt(_player.Damage.CurrentHP)}/{Mathf.CeilToInt(_player.Damage.MaxHP)}";
                if (hullSlider != null)
                {
                    hullSlider.maxValue = _player.Damage.MaxHP;
                    hullSlider.value = _player.Damage.CurrentHP;
                }
            }
        }
    }
}
