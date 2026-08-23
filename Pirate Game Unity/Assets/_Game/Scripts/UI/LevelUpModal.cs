using System;
using System.Collections.Generic;
using HighSeas.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace HighSeas.UI
{
    public sealed class LevelUpModal : MonoBehaviour
    {
        static LevelUpModal _instance;

        public static LevelUpModal Instance => ResolveInstance();

        [SerializeField] GameObject panel;
        [SerializeField] Button[] cardButtons;
        [SerializeField] Image[] cardFrames;
        [SerializeField] TextMeshProUGUI[] cardCategories;
        [SerializeField] TextMeshProUGUI[] cardTitles;
        [SerializeField] TextMeshProUGUI[] cardLevels;
        [SerializeField] TextMeshProUGUI[] cardRarities;
        [SerializeField] TextMeshProUGUI[] cardDescriptions;
        [SerializeField] TextMeshProUGUI[] cardHotkeys;

        Action<LevelOffer> _callback;
        List<LevelOffer> _offers;

        static LevelUpModal ResolveInstance()
        {
            if (_instance != null)
            {
                _instance.EnsureWired();
                if (_instance.HasUsableCards()) return _instance;
            }

            var modals = FindObjectsByType<LevelUpModal>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            LevelUpModal best = null;
            var bestScore = -1;
            for (var i = 0; i < modals.Length; i++)
            {
                modals[i].EnsureWired();
                var score = modals[i].ScoreUiReadiness();
                if (score > bestScore)
                {
                    bestScore = score;
                    best = modals[i];
                }
            }

            _instance = best;
            return _instance;
        }

        int ScoreUiReadiness()
        {
            var score = 0;
            if (GetComponent<RectTransform>() != null) score += 4;
            if (transform.childCount > 0) score += transform.childCount;
            if (cardButtons != null)
            {
                for (var i = 0; i < cardButtons.Length; i++)
                    if (cardButtons[i] != null) score += 10;
            }
            return score;
        }

        void Awake()
        {
            EnsureWired();
        }

        void Update()
        {
            if (panel == null || !panel.activeSelf || _offers == null) return;

            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) Pick(0);
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) Pick(1);
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) Pick(2);
        }

        public bool TryShow(List<LevelOffer> offers, Action<LevelOffer> callback)
        {
            EnsureWired();
            if (!HasUsableCards())
            {
                Debug.LogWarning($"LevelUpModal on '{name}' has no card buttons. Children={transform.childCount}.");
                return false;
            }

            Show(offers, callback);
            return true;
        }

        public void Show(List<LevelOffer> offers, Action<LevelOffer> callback)
        {
            EnsureWired();
            _offers = offers;
            _callback = callback;
            if (panel != null)
            {
                EnsureVisibleOnTop();
                panel.SetActive(true);
            }

            for (var i = 0; i < 3; i++)
            {
                var hasOffer = offers != null && i < offers.Count;
                if (cardButtons != null && i < cardButtons.Length && cardButtons[i] != null)
                {
                    cardButtons[i].gameObject.SetActive(hasOffer);
                    cardButtons[i].onClick.RemoveAllListeners();
                    if (hasOffer)
                    {
                        var idx = i;
                        cardButtons[i].onClick.AddListener(() => Pick(idx));
                    }
                }

                if (!hasOffer) continue;
                var offer = offers[i];
                if (cardCategories != null && i < cardCategories.Length && cardCategories[i] != null)
                    cardCategories[i].text = offer.categoryLabel;
                if (cardTitles != null && i < cardTitles.Length && cardTitles[i] != null)
                    cardTitles[i].text = offer.title;
                if (cardLevels != null && i < cardLevels.Length && cardLevels[i] != null)
                    cardLevels[i].text = offer.LevelLabel;
                if (cardRarities != null && i < cardRarities.Length && cardRarities[i] != null)
                    cardRarities[i].text = offer.rarity.ToString();
                if (cardDescriptions != null && i < cardDescriptions.Length && cardDescriptions[i] != null)
                    cardDescriptions[i].text = offer.description;
                if (cardHotkeys != null && i < cardHotkeys.Length && cardHotkeys[i] != null)
                    cardHotkeys[i].text = $"[{i + 1}]";
                if (cardFrames != null && i < cardFrames.Length && cardFrames[i] != null)
                    cardFrames[i].color = LevelUpRarityColors.ForRarity(offer.rarity);
            }
        }

        void EnsureVisibleOnTop()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindFirstObjectByType<Canvas>();
                if (canvas != null)
                    transform.SetParent(canvas.transform, false);
            }

            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            transform.SetAsLastSibling();
        }

        void EnsureWired()
        {
            if (panel == null)
                panel = gameObject;

            EnsurePanelRectTransform();
            WireExistingCards();
            if (!HasUsableCards())
                BuildRuntimeCards();
        }

        void EnsurePanelRectTransform()
        {
            if (GetComponent<RectTransform>() != null) return;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            var rt = gameObject.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(980f, 460f);
            rt.anchoredPosition = Vector2.zero;

            if (GetComponent<Image>() == null)
            {
                var img = gameObject.AddComponent<Image>();
                img.color = new Color(0f, 0f, 0f, 0.85f);
            }
        }

        void WireExistingCards()
        {
            if (cardButtons != null && cardButtons.Length > 0 && cardButtons[0] != null)
                return;

            cardButtons = new Button[3];
            cardFrames = new Image[3];
            cardCategories = new TextMeshProUGUI[3];
            cardTitles = new TextMeshProUGUI[3];
            cardLevels = new TextMeshProUGUI[3];
            cardRarities = new TextMeshProUGUI[3];
            cardDescriptions = new TextMeshProUGUI[3];
            cardHotkeys = new TextMeshProUGUI[3];

            for (var i = 0; i < 3; i++)
            {
                var card = FindChildRecursive(transform, $"Card{i}");
                if (card == null) continue;

                cardButtons[i] = card.GetComponent<Button>();
                cardFrames[i] = card.GetComponent<Image>();
                var texts = card.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (var t = 0; t < texts.Length; t++)
                {
                    switch (texts[t].gameObject.name)
                    {
                        case "Category":
                            cardCategories[i] = texts[t];
                            break;
                        case "Label":
                            cardTitles[i] = texts[t];
                            break;
                        case "Level":
                            cardLevels[i] = texts[t];
                            break;
                        case "Rarity":
                            cardRarities[i] = texts[t];
                            break;
                        case "Desc":
                            cardDescriptions[i] = texts[t];
                            break;
                        case "Hotkey":
                            cardHotkeys[i] = texts[t];
                            break;
                    }
                }

                if (cardTitles[i] == null && texts.Length > 0)
                    cardTitles[i] = texts[0];
                if (cardDescriptions[i] == null && texts.Length > 1)
                    cardDescriptions[i] = texts[1];
            }
        }

        void BuildRuntimeCards()
        {
            if (HasUsableCards()) return;

            for (var i = 0; i < 3; i++)
            {
                if (transform.Find($"Card{i}") != null) continue;
                CreateCard(i);
            }

            WireExistingCards();
        }

        void CreateCard(int index)
        {
            var cardGo = new GameObject($"Card{index}", typeof(RectTransform), typeof(Image), typeof(Button));
            cardGo.transform.SetParent(transform, false);
            var rt = cardGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(240f, 320f);
            rt.anchoredPosition = new Vector2(-280f + index * 280f, 0f);
            cardGo.GetComponent<Image>().color = new Color(0.25f, 0.25f, 0.3f, 1f);

            CreateLabel(cardGo.transform, "Category", "Category", new Vector2(0f, 120f), 14f);
            CreateLabel(cardGo.transform, "Label", "Offer", new Vector2(0f, 90f), 22f);
            CreateLabel(cardGo.transform, "Level", "Lv 1 → 2", new Vector2(0f, 60f), 16f);
            CreateLabel(cardGo.transform, "Rarity", "Common", new Vector2(0f, 35f), 14f);
            CreateLabel(cardGo.transform, "Desc", "", new Vector2(0f, -20f), 15f);
            CreateLabel(cardGo.transform, "Hotkey", $"[{index + 1}]", new Vector2(0f, -120f), 16f);
        }

        static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, Vector2 pos, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(220f, 40f);
            rt.anchoredPosition = pos;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        static Transform FindChildRecursive(Transform parent, string childName)
        {
            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                if (child.name == childName) return child;
                var nested = FindChildRecursive(child, childName);
                if (nested != null) return nested;
            }
            return null;
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

    public static class LevelUpRarityColors
    {
        public static Color ForRarity(Rarity rarity) => rarity switch
        {
            Rarity.Uncommon => new Color(0.2f, 0.55f, 0.35f, 1f),
            Rarity.Rare => new Color(0.2f, 0.45f, 0.75f, 1f),
            Rarity.Epic => new Color(0.45f, 0.25f, 0.7f, 1f),
            Rarity.Legendary => new Color(0.75f, 0.5f, 0.15f, 1f),
            _ => new Color(0.25f, 0.25f, 0.3f, 1f)
        };
    }
}
