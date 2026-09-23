using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.UI;
using HighSeas.V3.MultiShip;
using HighSeas.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Megabonk-style bottom loadout bar: weapon slots, logbook slots, and run items.</summary>
    public sealed class V3RunLoadoutHud : MonoBehaviour
    {
        const int LogbookSlotCount = 2;
        const int ItemSlotCount = 4;
        const float BarWidth = 1040f;
        const float BarHeight = 92f;
        const float BarSidePadding = 18f;

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
            public Image icon;
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
            rect.anchoredPosition = new Vector2(0f, 12f);
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
            var weaponSlotCount = CombatExperimentContext.Active.weaponSlotCap;
            var slotWidth = weaponSlotCount >= 6 ? 50f : 64f;
            var slotHeight = 50f;
            var slotSpacing = weaponSlotCount >= 6 ? 54f : 68f;
            var logbookSlotWidth = 72f;
            var logbookSpacing = 76f;
            var itemSlotWidth = 50f;
            var itemSpacing = 54f;

            var barHalf = BarWidth * 0.5f;
            var rightEdge = barHalf - BarSidePadding;

            var itemCount = ItemSlotCount;
            var lastItemCenterX = rightEdge - itemSlotWidth * 0.5f;
            var firstItemCenterX = lastItemCenterX - (itemCount - 1) * itemSpacing;
            var itemsLeftEdge = firstItemCenterX - itemSlotWidth * 0.5f;

            var logbookCount = LogbookSlotCount;
            var lastLogbookCenterX = itemsLeftEdge - 20f - logbookSlotWidth * 0.5f;
            var firstLogbookCenterX = lastLogbookCenterX - (logbookCount - 1) * logbookSpacing;

            CreateSectionLabel(root, "WEAPONS", new Vector2(-barHalf + 88f, 30f));
            CreateSectionLabel(root, "LOGBOOKS", new Vector2(firstLogbookCenterX + logbookSlotWidth * 0.5f, 30f));
            CreateSectionLabel(root, "ITEMS", new Vector2(firstItemCenterX + (itemCount - 1) * itemSpacing * 0.5f, 30f));

            var weaponsSpan = (weaponSlotCount - 1) * slotSpacing;
            var weaponsRightLimit = firstLogbookCenterX - logbookSlotWidth * 0.5f - 24f;
            var weaponsLeft = -barHalf + BarSidePadding + slotWidth * 0.5f;
            var weaponsStartX = Mathf.Min(weaponsLeft, weaponsRightLimit - weaponsSpan - slotWidth * 0.5f);
            if (weaponsStartX > weaponsRightLimit - weaponsSpan)
                weaponsStartX = -barHalf + BarSidePadding + slotWidth * 0.5f;

            for (var i = 0; i < weaponSlotCount; i++)
            {
                var size = new Vector2(slotWidth, slotHeight);
                _weaponSlots.Add(CreateSlot(
                    root,
                    $"WeaponSlot{i}",
                    new Vector2(weaponsStartX + i * slotSpacing, -8f),
                    size,
                    new Color(0.35f, 0.72f, 1f)));
            }

            for (var i = 0; i < LogbookSlotCount; i++)
            {
                _logbookSlots.Add(CreateSlot(
                    root,
                    $"LogbookSlot{i}",
                    new Vector2(firstLogbookCenterX + i * logbookSpacing, -8f),
                    new Vector2(logbookSlotWidth, slotHeight),
                    new Color(0.62f, 0.92f, 0.48f)));
            }

            for (var i = 0; i < ItemSlotCount; i++)
            {
                _itemSlots.Add(CreateSlot(
                    root,
                    $"ItemSlot{i}",
                    new Vector2(firstItemCenterX + i * itemSpacing, -8f),
                    new Vector2(itemSlotWidth, slotHeight),
                    new Color(0.85f, 0.65f, 0.2f)));
            }
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

            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(root.transform, false);
            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = new Vector2(0f, 4f);
            iconRect.sizeDelta = new Vector2(size.x - 8f, size.y - 18f);
            var icon = iconGo.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(root.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = Vector2.zero;
            titleRect.anchorMax = Vector2.one;
            titleRect.offsetMin = new Vector2(4f, 14f);
            titleRect.offsetMax = new Vector2(-4f, -4f);
            var title = titleGo.AddComponent<TextMeshProUGUI>();
            title.fontSize = 8f;
            title.enableAutoSizing = true;
            title.fontSizeMin = 6f;
            title.fontSizeMax = 8f;
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
            subRect.offsetMin = new Vector2(4f, 2f);
            subRect.offsetMax = new Vector2(-4f, -16f);
            var subtitle = subGo.AddComponent<TextMeshProUGUI>();
            subtitle.fontSize = 7f;
            subtitle.enableAutoSizing = true;
            subtitle.fontSizeMin = 6f;
            subtitle.fontSizeMax = 7f;
            subtitle.alignment = TextAlignmentOptions.TopLeft;
            subtitle.color = RunHudTheme.Label;
            subtitle.enableWordWrapping = false;
            subtitle.overflowMode = TextOverflowModes.Ellipsis;
            subtitle.maxVisibleLines = 2;

            return new SlotView
            {
                root = root,
                border = borderImg,
                icon = icon,
                title = title,
                subtitle = subtitle
            };
        }

        void Start()
        {
            _levels = LevelSystem.Instance;
            _weapons = V3MultiShipWeaponLoadouts.FindLocalHumanWeaponController()
                ?? FindFirstObjectByType<WeaponController>();
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
            if (_weapons == null)
                _weapons = V3MultiShipWeaponLoadouts.FindLocalHumanWeaponController()
                    ?? FindFirstObjectByType<WeaponController>();
            if (_database == null) _database = GameDatabaseLoader.Load();

            RefreshWeaponSlots();
            RefreshLogbookSlots();
            RefreshItemSlots();
        }

        static readonly Color WeaponAccent = new(0.35f, 0.72f, 1f, 1f);
        static readonly Color LogbookAccent = new(0.62f, 0.92f, 0.48f, 1f);
        static readonly Color ItemAccent = new(0.85f, 0.65f, 0.2f, 1f);
        static readonly Color EmptyAccent = new(1f, 1f, 1f, 0.2f);

        void RefreshWeaponSlots()
        {
            for (var i = 0; i < _weaponSlots.Count; i++)
            {
                var slot = _weaponSlots[i];
                slot.root.SetActive(true);
                if (i < _weapons?.Weapons.Count)
                {
                    var weapon = _weapons.Weapons[i];
                    slot.title.text = WeaponEvolutionRuntime.GetDisplayName(weapon.def.id, weapon.def.displayName);
                    slot.subtitle.text = $"LV {weapon.level}";
                    var sprite = V3RunHudIconResolver.GetWeaponIcon(weapon.def.id, weapon.def);
                    ApplyRunSlotVisual(slot, true, WeaponAccent, sprite);
                }
                else
                {
                    slot.title.text = "Empty";
                    slot.subtitle.text = "—";
                    ApplyRunSlotVisual(slot, false, WeaponAccent);
                }
            }
        }

        void RefreshLogbookSlots()
        {
            var owned = _levels?.OwnedLogbooks;
            for (var i = 0; i < _logbookSlots.Count; i++)
            {
                var slot = _logbookSlots[i];
                slot.root.SetActive(true);
                if (owned != null && i < owned.Count)
                {
                    var id = owned[i];
                    var def = _database?.GetLogbook(id);
                    slot.title.text = def != null ? def.displayName : id;
                    var logbookDesc = def != null
                        ? V3ContentDescriptions.GetLogbookDescription(def)
                        : V3ContentDescriptions.GetLogbookDescription(id);
                    slot.subtitle.text = $"RANK {_levels.GetLogbookLevel(id)} · {logbookDesc}";
                    ApplyRunSlotVisual(slot, true, LogbookAccent);
                }
                else
                {
                    slot.title.text = "Empty";
                    slot.subtitle.text = "—";
                    ApplyRunSlotVisual(slot, false, LogbookAccent);
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
                slot.root.SetActive(true);
                if (items != null && i < items.Count)
                {
                    var id = items[i];
                    slot.title.text = V3ContentDescriptions.GetItemName(id);
                    var stacks = V3RunItemInventory.Instance.GetStackCount(id);
                    slot.subtitle.text = stacks > 1 ? $"x{stacks}" : "Equipped";
                    if (i == ItemSlotCount - 1 && overflow > 0)
                        slot.subtitle.text += $" · +{overflow}";
                    var sprite = V3RunHudIconResolver.GetItemIcon(id);
                    ApplyRunSlotVisual(slot, true, ItemAccent, sprite);
                }
                else
                {
                    slot.title.text = "—";
                    slot.subtitle.text = "EMPTY";
                    ApplyRunSlotVisual(slot, false, ItemAccent);
                }
            }
        }

        static void ApplyRunSlotVisual(SlotView slot, bool filled, Color accent, Sprite iconSprite = null)
        {
            if (slot.border != null)
                slot.border.color = filled ? accent : EmptyAccent;

            var fill = slot.root.GetComponent<Image>();
            if (fill != null)
                fill.color = filled ? RunHudTheme.SlotFill : new Color(RunHudTheme.SlotFill.r, RunHudTheme.SlotFill.g, RunHudTheme.SlotFill.b, 0.35f);

            if (slot.icon != null)
            {
                slot.icon.sprite = iconSprite;
                slot.icon.enabled = filled && iconSprite != null;
            }

            if (slot.title != null)
                slot.title.color = filled ? RunHudTheme.Value : RunHudTheme.Label;
            if (slot.subtitle != null)
                slot.subtitle.color = filled ? RunHudTheme.Label : new Color(RunHudTheme.Label.r, RunHudTheme.Label.g, RunHudTheme.Label.b, 0.45f);
        }

    }
}
