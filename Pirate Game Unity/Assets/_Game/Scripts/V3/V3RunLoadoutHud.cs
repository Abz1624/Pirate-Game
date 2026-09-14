using System.Collections.Generic;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.UI;
using HighSeas.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Megabonk-style bottom loadout bar: weapon slots, logbook slots, and run items.</summary>
    public sealed class V3RunLoadoutHud : MonoBehaviour
    {
        const int WeaponSlotCount = 2;
        const int LogbookSlotCount = 2;
        const int ItemSlotCount = 4;
        const float BarWidth = 1000f;
        const float BarHeight = 128f;

        readonly List<SlotView> _weaponSlots = new();
        readonly List<SlotView> _logbookSlots = new();
        readonly List<SlotView> _itemSlots = new();

        LevelSystem _levels;
        WeaponController _weapons;
        GameDatabase _database;

        struct SlotView
        {
            public GameObject root;
            public Image border;
            public TextMeshProUGUI title;
            public TextMeshProUGUI subtitle;
        }

        public static V3RunLoadoutHud Ensure(Transform canvas)
        {
            var existing = canvas.GetComponentInChildren<V3RunLoadoutHud>(true);
            if (existing != null) return existing;

            var root = new GameObject("PLACEHOLDER_v3_ui_loadout_bar");
            root.transform.SetParent(canvas, false);
            var rect = root.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 18f);
            rect.sizeDelta = new Vector2(BarWidth, BarHeight);

            var bg = root.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.08f, 0.14f, 0.9f);

            var fitter = root.AddComponent<UiScaleToFitParent>();
            fitter.SetContentWidth(BarWidth);

            var hud = root.AddComponent<V3RunLoadoutHud>();
            hud.BuildLayout(root.transform);
            return hud;
        }

        void BuildLayout(Transform root)
        {
            CreateSectionLabel(root, "WEAPONS", new Vector2(-340f, 42f));
            CreateSectionLabel(root, "LOGBOOKS", new Vector2(-10f, 42f));
            CreateSectionLabel(root, "ITEMS", new Vector2(290f, 42f));

            for (var i = 0; i < WeaponSlotCount; i++)
                _weaponSlots.Add(CreateSlot(root, $"WeaponSlot{i}", new Vector2(-390f + i * 130f, -12f), new Vector2(116f, 78f), new Color(0.35f, 0.72f, 1f)));

            for (var i = 0; i < LogbookSlotCount; i++)
                _logbookSlots.Add(CreateSlot(root, $"LogbookSlot{i}", new Vector2(-60f + i * 130f, -12f), new Vector2(116f, 78f), new Color(0.62f, 0.92f, 0.48f)));

            for (var i = 0; i < ItemSlotCount; i++)
                _itemSlots.Add(CreateSlot(root, $"ItemSlot{i}", new Vector2(210f + i * 76f, -12f), new Vector2(72f, 78f), new Color(0.85f, 0.65f, 0.2f)));
        }

        static TextMeshProUGUI CreateSectionLabel(Transform parent, string text, Vector2 pos)
        {
            var go = new GameObject(text);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(120f, 20f);
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 12f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = RunHudTheme.Label;
            return label;
        }

        static SlotView CreateSlot(Transform parent, string name, Vector2 pos, Vector2 size, Color accent)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
            border.transform.SetParent(root.transform, false);
            var borderRect = border.GetComponent<RectTransform>();
            borderRect.sizeDelta = size + new Vector2(4f, 4f);
            var borderImg = border.GetComponent<Image>();
            borderImg.color = accent;

            var fill = root.AddComponent<Image>();
            fill.color = RunHudTheme.SlotFill;
            root.AddComponent<RectMask2D>();

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(root.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(6f, 18f);
            titleRect.offsetMax = new Vector2(-6f, -6f);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.fontSize = 11f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.TopLeft;
            title.color = RunHudTheme.Value;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.enableWordWrapping = false;

            var subGo = new GameObject("Subtitle", typeof(RectTransform));
            subGo.transform.SetParent(root.transform, false);
            var subRect = subGo.GetComponent<RectTransform>();
            subRect.anchorMin = Vector2.zero;
            subRect.anchorMax = Vector2.one;
            subRect.offsetMin = new Vector2(6f, 4f);
            subRect.offsetMax = new Vector2(-6f, -22f);
            var subtitle = subGo.AddComponent<TextMeshProUGUI>();
            subtitle.fontSize = 9f;
            subtitle.alignment = TextAlignmentOptions.TopLeft;
            subtitle.color = RunHudTheme.Label;
            subtitle.enableWordWrapping = false;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            subtitle.maxVisibleLines = 2;

            return new SlotView
            {
                root = root,
                border = borderImg,
                title = title,
                subtitle = subtitle
            };
        }

        void Start()
        {
            _levels = LevelSystem.Instance;
            _weapons = FindFirstObjectByType<WeaponController>();
            _database = GameDatabaseLoader.Load();
            if (_levels != null)
                _levels.LoadoutChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_levels != null)
                _levels.LoadoutChanged -= Refresh;
        }

        void Update() => Refresh();

        void Refresh()
        {
            if (_levels == null) _levels = LevelSystem.Instance;
            if (_weapons == null) _weapons = FindFirstObjectByType<WeaponController>();
            if (_database == null) _database = GameDatabaseLoader.Load();

            RefreshWeaponSlots();
            RefreshLogbookSlots();
            RefreshItemSlots();
        }

        void RefreshWeaponSlots()
        {
            for (var i = 0; i < _weaponSlots.Count; i++)
            {
                var slot = _weaponSlots[i];
                if (i < _weapons?.Weapons.Count)
                {
                    var weapon = _weapons.Weapons[i];
                    slot.root.SetActive(true);
                    slot.title.text = weapon.def.displayName;
                    slot.subtitle.text = $"LV {weapon.level} · {V3ContentDescriptions.GetWeaponDescription(weapon.def)}";
                }
                else
                {
                    slot.root.SetActive(true);
                    slot.title.text = "Empty";
                    slot.subtitle.text = "—";
                }
            }
        }

        void RefreshLogbookSlots()
        {
            var owned = _levels?.OwnedLogbooks;
            for (var i = 0; i < _logbookSlots.Count; i++)
            {
                var slot = _logbookSlots[i];
                if (owned != null && i < owned.Count)
                {
                    var id = owned[i];
                    var def = _database?.GetLogbook(id);
                    slot.root.SetActive(true);
                    slot.title.text = def != null ? def.displayName : id;
                    var logbookDesc = def != null
                        ? V3ContentDescriptions.GetLogbookDescription(def)
                        : V3ContentDescriptions.GetLogbookDescription(id);
                    slot.subtitle.text = $"RANK {_levels.GetLogbookLevel(id)} · {logbookDesc}";
                }
                else
                {
                    slot.root.SetActive(true);
                    slot.title.text = "Empty";
                    slot.subtitle.text = "—";
                }
            }
        }

        void RefreshItemSlots()
        {
            var items = V3RunItemInventory.Instance?.Items;
            var overflow = items != null ? Mathf.Max(0, items.Count - ItemSlotCount) : 0;
            for (var i = 0; i < _itemSlots.Count; i++)
            {
                var slot = _itemSlots[i];
                if (items != null && i < items.Count)
                {
                    var id = items[i];
                    slot.root.SetActive(true);
                    slot.title.text = V3ContentDescriptions.GetItemName(id);
                    var stacks = V3RunItemInventory.Instance.GetStackCount(id);
                    slot.subtitle.text = stacks > 1 ? $"x{stacks}" : "Equipped";
                    if (i == ItemSlotCount - 1 && overflow > 0)
                        slot.subtitle.text += $" · +{overflow}";
                }
                else
                {
                    slot.root.SetActive(true);
                    slot.title.text = "—";
                    slot.subtitle.text = "EMPTY";
                }
            }
        }

    }
}
