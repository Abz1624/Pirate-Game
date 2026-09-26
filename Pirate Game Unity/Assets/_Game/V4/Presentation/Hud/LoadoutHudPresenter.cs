using System.Collections.Generic;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// The bottom loadout rows (M6): port of V3AuthoredBottomLoadoutHud. Weapons bottom-left in the authored
    /// <c>Weapon_Slot</c> row; Logbooks above Items bottom-right, cloned from the weapon slot template; same scale, spacing,
    /// colours and labels. Reads the loadout and progression views; icons come from the <see cref="IconCatalog"/>.
    /// Refreshes only when what it shows changed (V3 rewrote every slot every frame).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LoadoutHudPresenter : MonoBehaviour
    {
        const int LogbookSlotCount = 4;
        const int ItemSlotCount = 4;
        const float SideInset = 24f;
        const float BottomInset = 14f;
        const float RowGap = 32f;
        const float SlotSpacing = 4f;
        const float BaseSlotWidth = 77.1365f;
        const int MaxLabelChars = 11;

        static readonly Color WeaponAccent = new Color(0.45f, 0.78f, 1f, 1f);
        static readonly Color ItemAccent = new Color(1f, 0.78f, 0.35f, 1f);
        static readonly Color LogbookAccent = new Color(0.55f, 0.92f, 0.48f, 1f);
        static readonly Color EmptyFrame = new Color(1f, 1f, 1f, 0.18f);

        struct SlotView
        {
            public GameObject root;
            public Image frame;
            public Image icon;
            public GameObject emptyMarker;
            public TextMeshProUGUI levelText;
            public string shown; // what the slot shows now (skip rewrites when unchanged)
        }

        readonly List<SlotView> _weaponSlots = new List<SlotView>();
        readonly List<SlotView> _logbookSlots = new List<SlotView>();
        readonly List<SlotView> _itemSlots = new List<SlotView>();

        RectTransform _weaponRow;
        RectTransform _rightCluster;
        RectTransform _logbookRow;
        RectTransform _itemRow;
        IRunViews _views;
        IconCatalog _icons;
        int _lastLayoutWeaponCap = -1;
        float _lastLayoutWidth = -1f;

        public int WeaponSlotCount => _weaponSlots.Count;
        public int ItemSlotViews => _itemSlots.Count;
        public int LogbookSlotViews => _logbookSlots.Count;

        public void Bind(IRunViews views, IconCatalog icons)
        {
            _views = views;
            _icons = icons;
            BindRows();
            Refresh();
        }

        void LateUpdate()
        {
            if (_views != null) Refresh();
        }

        void BindRows()
        {
            if (_weaponRow == null)
            {
                _weaponRow = transform.Find("Weapon_Slot") as RectTransform;
                if (_weaponRow == null) return;
                _weaponRow.gameObject.SetActive(true);
                _weaponSlots.Clear();
                for (var i = 0; i < _weaponRow.childCount; i++)
                {
                    var slot = BindSlot(_weaponRow.GetChild(i));
                    if (slot.root == null) continue;
                    ConfigureSlotForText(slot);
                    _weaponSlots.Add(slot);
                }
            }

            EnsureRightCluster();
            _itemRow = transform.Find("Loadout_Right/Item_Slot") as RectTransform;
            if (_itemRow == null) _itemRow = transform.Find("Item_Slot") as RectTransform;
            if (_itemRow == null) _itemRow = new GameObject("Item_Slot", typeof(RectTransform)).GetComponent<RectTransform>();
            if (_itemRow.parent != _rightCluster) _itemRow.SetParent(_rightCluster, false);
            ConfigureSlotRow(_itemRow);
            FillRow(_itemRow, ItemSlotCount, _itemSlots);

            _logbookRow = _rightCluster.Find("Logbook_Slot") as RectTransform;
            if (_logbookRow == null)
            {
                _logbookRow = new GameObject("Logbook_Slot", typeof(RectTransform)).GetComponent<RectTransform>();
                _logbookRow.SetParent(_rightCluster, false);
                _logbookRow.SetAsFirstSibling();
            }
            ConfigureSlotRow(_logbookRow);
            FillRow(_logbookRow, LogbookSlotCount, _logbookSlots);

            ApplyLayout(force: true);
        }

        void EnsureRightCluster()
        {
            _rightCluster = transform.Find("Loadout_Right") as RectTransform;
            if (_rightCluster != null) return;
            var go = new GameObject("Loadout_Right", typeof(RectTransform));
            _rightCluster = go.GetComponent<RectTransform>();
            _rightCluster.SetParent(transform, false);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 2f;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        /// <summary>Binds <paramref name="count"/> slots in a row, cloning the weapon slot template for missing ones (once).</summary>
        void FillRow(RectTransform row, int count, List<SlotView> into)
        {
            var template = _weaponSlots.Count > 0 && _weaponSlots[0].root != null ? _weaponSlots[0].root.transform : null;
            if (template == null && _weaponRow != null && _weaponRow.childCount > 0) template = _weaponRow.GetChild(0);
            into.Clear();
            for (var i = 0; i < count; i++)
            {
                Transform slotTransform;
                if (i < row.childCount) slotTransform = row.GetChild(i);
                else if (template != null) slotTransform = Instantiate(template, row).transform;
                else break;
                var slot = BindSlot(slotTransform);
                if (slot.root == null) continue;
                ConfigureSlotForText(slot);
                into.Add(slot);
            }
        }

        static void ConfigureSlotRow(RectTransform row)
        {
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = SlotSpacing;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.reverseArrangement = true;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            EnsureFitter(row);
        }

        static void EnsureFitter(RectTransform row)
        {
            var fitter = row.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void ApplyLayout(bool force = false)
        {
            if (_weaponRow == null || _itemRow == null || _rightCluster == null) return;
            var canvas = _weaponRow.GetComponentInParent<Canvas>();
            var canvasRt = canvas != null ? canvas.GetComponent<RectTransform>() : null;
            var width = canvasRt != null ? canvasRt.rect.width : 1920f;
            var cap = _views?.Loadout != null ? _views.Loadout.WeaponSlotCap : _weaponSlots.Count;
            if (!force && Mathf.Approximately(width, _lastLayoutWidth) && cap == _lastLayoutWeaponCap) return;
            _lastLayoutWidth = width;
            _lastLayoutWeaponCap = cap;

            ConfigureSlotRow(_weaponRow);
            EnsureFitter(_weaponRow);
            var scale = RowScale(width, cap);
            Anchor(_weaponRow, new Vector2(0f, 0f), new Vector2(SideInset, BottomInset), scale);
            Anchor(_rightCluster, new Vector2(1f, 0f), new Vector2(-SideInset, BottomInset), scale);

            var weaponLayout = _weaponRow.GetComponent<HorizontalLayoutGroup>();
            if (weaponLayout != null)
            {
                weaponLayout.spacing = SlotSpacing;
                weaponLayout.childAlignment = TextAnchor.LowerLeft;
                weaponLayout.reverseArrangement = false;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_weaponRow);
            if (_logbookRow != null) LayoutRebuilder.ForceRebuildLayoutImmediate(_logbookRow);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_itemRow);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rightCluster);
        }

        public static float RowScale(float screenWidth, int visibleWeaponSlots)
        {
            var weaponCount = Mathf.Max(1, visibleWeaponSlots);
            var weaponSpan = weaponCount * BaseSlotWidth + (weaponCount - 1) * SlotSpacing;
            var logbookSpan = LogbookSlotCount * BaseSlotWidth + (LogbookSlotCount - 1) * SlotSpacing;
            var itemSpan = ItemSlotCount * BaseSlotWidth + (ItemSlotCount - 1) * SlotSpacing;
            var needed = weaponSpan + RowGap + Mathf.Max(logbookSpan, itemSpan);
            var available = screenWidth - SideInset * 2f;
            if (needed <= 0f || available <= 0f) return 0.58f;
            return Mathf.Clamp(available / needed, 0.42f, 0.64f);
        }

        static void Anchor(RectTransform row, Vector2 corner, Vector2 position, float scale)
        {
            row.anchorMin = corner;
            row.anchorMax = corner;
            row.pivot = corner;
            row.anchoredPosition = position;
            row.localScale = new Vector3(scale, scale, 1f);
        }

        static void ConfigureSlotForText(SlotView slot)
        {
            if (slot.root == null) return;
            var weapon = slot.root.transform.Find("Weapon");
            if (weapon != null) weapon.localScale = Vector3.one * 0.48f;

            if (slot.emptyMarker != null)
            {
                if (slot.emptyMarker.transform is RectTransform emptyRt)
                {
                    emptyRt.localScale = Vector3.one * 0.28f;
                    emptyRt.anchorMin = new Vector2(0.5f, 0.5f);
                    emptyRt.anchorMax = new Vector2(0.5f, 0.5f);
                    emptyRt.pivot = new Vector2(0.5f, 0.5f);
                    emptyRt.anchoredPosition = Vector2.zero;
                    emptyRt.sizeDelta = new Vector2(56f, 56f);
                }
                if (slot.emptyMarker.TryGetComponent<Image>(out var emptyImg)) emptyImg.preserveAspect = true;
            }

            if (slot.levelText == null) return;
            slot.levelText.enableAutoSizing = true;
            slot.levelText.fontSizeMin = 6f;
            slot.levelText.fontSizeMax = 9f;
            slot.levelText.fontSize = 9f;
            slot.levelText.alignment = TextAlignmentOptions.Center;
            slot.levelText.enableWordWrapping = true;
            slot.levelText.overflowMode = TextOverflowModes.Truncate;
            slot.levelText.margin = new Vector4(2f, 2f, 2f, 2f);
            var textRect = slot.levelText.rectTransform;
            textRect.anchorMin = new Vector2(0.08f, 0.12f);
            textRect.anchorMax = new Vector2(0.92f, 0.88f);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        public static string CompactLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var trimmed = value.Trim();
            if (trimmed.Length <= MaxLabelChars) return trimmed;
            return trimmed.Substring(0, MaxLabelChars - 1) + "…";
        }

        static SlotView BindSlot(Transform slotRoot)
        {
            if (slotRoot == null) return default;
            var weapon = slotRoot.Find("Weapon");
            return new SlotView
            {
                root = slotRoot.gameObject,
                frame = weapon != null ? weapon.GetComponent<Image>() : null,
                icon = weapon != null ? weapon.Find("Icon")?.GetComponent<Image>() : null,
                emptyMarker = slotRoot.Find("Add_Sllot")?.gameObject,
                levelText = weapon != null ? weapon.Find("Lvl_txt")?.GetComponent<TextMeshProUGUI>() : slotRoot.GetComponentInChildren<TextMeshProUGUI>(true)
            };
        }

        /// <summary>Shows a filled or empty slot; skipped when the slot already shows exactly this.</summary>
        static void Show(List<SlotView> slots, int index, bool filled, Color accent, Sprite icon, string label)
        {
            var slot = slots[index];
            var key = filled ? label + "|" + (icon != null ? icon.GetInstanceID() : 0) : string.Empty;
            if (slot.shown == key) return;
            slot.shown = key;
            slots[index] = slot;

            if (slot.emptyMarker != null)
            {
                slot.emptyMarker.SetActive(!filled);
                if (!filled && slot.emptyMarker.transform is RectTransform emptyRt && emptyRt.localScale.x > 0.35f)
                    emptyRt.localScale = Vector3.one * 0.28f;
            }
            if (slot.frame != null) slot.frame.color = filled ? accent : EmptyFrame;
            if (slot.icon != null)
            {
                slot.icon.sprite = icon;
                slot.icon.enabled = filled && icon != null;
                slot.icon.color = Color.white;
            }
            if (slot.levelText != null)
            {
                slot.levelText.color = filled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                slot.levelText.text = filled ? label : string.Empty;
            }
        }

        void Refresh()
        {
            if (_weaponRow == null) return;
            ApplyLayout();
            var loadout = _views.Loadout;
            var progression = _views.Progression;

            var cap = loadout != null ? loadout.WeaponSlotCap : 0;
            for (var i = 0; i < _weaponSlots.Count; i++)
            {
                var visible = i < cap;
                if (_weaponSlots[i].root.activeSelf != visible) _weaponSlots[i].root.SetActive(visible);
                if (!visible) continue;
                if (loadout != null && i < loadout.WeaponCount)
                {
                    var icon = _icons != null ? _icons.Weapon(loadout.WeaponId(i)) : null;
                    Show(_weaponSlots, i, true, WeaponAccent, icon, $"{CompactLabel(loadout.WeaponName(i))}\nLV{loadout.WeaponLevel(i)}");
                }
                else Show(_weaponSlots, i, false, WeaponAccent, null, null);
            }

            for (var i = 0; i < _itemSlots.Count; i++)
            {
                if (!_itemSlots[i].root.activeSelf) _itemSlots[i].root.SetActive(true);
                if (loadout != null && i < loadout.ItemCount)
                {
                    var id = loadout.ItemId(i);
                    var stacks = loadout.ItemStacks(i);
                    var name = CompactLabel(loadout.ItemName(i));
                    Show(_itemSlots, i, true, ItemAccent, _icons != null ? _icons.Item(id) : null, stacks > 1 ? $"{name}\nx{stacks}" : name);
                }
                else Show(_itemSlots, i, false, ItemAccent, null, null);
            }

            for (var i = 0; i < _logbookSlots.Count; i++)
            {
                if (!_logbookSlots[i].root.activeSelf) _logbookSlots[i].root.SetActive(true);
                if (progression != null && progression.Exists && i < progression.LogbookCount)
                    Show(_logbookSlots, i, true, LogbookAccent, null, $"{CompactLabel(progression.LogbookName(i))}\nR{progression.LogbookRank(i)}");
                else Show(_logbookSlots, i, false, LogbookAccent, null, null);
            }
        }
    }
}
