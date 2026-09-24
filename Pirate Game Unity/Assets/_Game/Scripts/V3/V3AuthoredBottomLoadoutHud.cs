using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.UI;
using HighSeas.V3.MultiShip;
using HighSeas.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HighSeas.V3
{
    /// <summary>Drives authored Canvas bottom weapon row + item icons.</summary>
    [DisallowMultipleComponent]
    public sealed class V3AuthoredBottomLoadoutHud : MonoBehaviour
    {
        const int LogbookSlotCount = 4;
        const int ItemSlotCount = 4;
        const float SideInset = 24f;
        const float BottomInset = 14f;
        const float RowGap = 32f;
        const float SlotSpacing = 4f;
        const float BaseSlotWidth = 77.1365f;
        const int MaxLabelChars = 11;

        readonly List<PowerupSlotView> _weaponSlots = new();
        readonly List<PowerupSlotView> _logbookSlots = new();
        readonly List<PowerupSlotView> _itemSlots = new();

        RectTransform _weaponRow;
        RectTransform _rightCluster;
        RectTransform _logbookRow;
        RectTransform _itemRow;
        LevelSystem _levels;
        WeaponController _weapons;
        GameDatabase _database;
        int _lastLayoutWeaponCap = -1;
        float _lastLayoutWidth = -1f;

        struct PowerupSlotView
        {
            public GameObject root;
            public Image frame;
            public Image icon;
            public GameObject emptyMarker;
            public TextMeshProUGUI levelText;
        }

        static readonly Color WeaponAccent = new(0.45f, 0.78f, 1f, 1f);
        static readonly Color ItemAccent = new(1f, 0.78f, 0.35f, 1f);
        static readonly Color LogbookAccent = new(0.55f, 0.92f, 0.48f, 1f);
        static readonly Color EmptyFrame = new(1f, 1f, 1f, 0.18f);

        public static V3AuthoredBottomLoadoutHud Ensure(Transform hudRoot)
        {
            if (hudRoot == null) return null;
            var existing = hudRoot.GetComponent<V3AuthoredBottomLoadoutHud>();
            if (existing != null) return existing;
            return hudRoot.gameObject.AddComponent<V3AuthoredBottomLoadoutHud>();
        }

        void Awake() => BindIfNeeded();

        void Start()
        {
            BindIfNeeded();
            if (_levels != null)
                _levels.LoadoutChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (_levels != null)
                _levels.LoadoutChanged -= Refresh;
        }

        void LateUpdate() => Refresh();

        public void BindIfNeeded()
        {
            if (_weaponRow == null)
            {
                var root = transform;
                _weaponRow = root.Find("Weapon_Slot") as RectTransform;
                if (_weaponRow == null) return;

                _weaponRow.gameObject.SetActive(true);

                _weaponSlots.Clear();
                for (var i = 0; i < _weaponRow.childCount; i++)
                {
                    var slot = BindPowerupSlot(_weaponRow.GetChild(i));
                    if (slot.root != null)
                    {
                        ConfigureSlotForText(slot);
                        _weaponSlots.Add(slot);
                    }
                }

                _levels = LevelSystem.Instance;
                _weapons = V3MultiShipWeaponLoadouts.FindLocalHumanWeaponController()
                           ?? ResolveHumanWeaponController();
                _database = GameDatabaseLoader.Load();
            }

            EnsureItemRow(transform);
            EnsureLogbookRow(transform);
            ApplyLoadoutLayout(force: true);
        }

        void EnsureRightCluster(Transform root)
        {
            _rightCluster = root.Find("Loadout_Right") as RectTransform;
            if (_rightCluster != null) return;

            var go = new GameObject("Loadout_Right", typeof(RectTransform));
            _rightCluster = go.GetComponent<RectTransform>();
            _rightCluster.SetParent(root, false);

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

        void EnsureItemRow(Transform root)
        {
            EnsureRightCluster(root);

            _itemRow = root.Find("Loadout_Right/Item_Slot") as RectTransform;
            if (_itemRow == null)
                _itemRow = root.Find("Item_Slot") as RectTransform;
            if (_itemRow == null)
            {
                var go = new GameObject("Item_Slot", typeof(RectTransform));
                _itemRow = go.GetComponent<RectTransform>();
            }

            if (_itemRow.parent != _rightCluster)
                _itemRow.SetParent(_rightCluster, false);

            ConfigureSlotRow(_itemRow);

            var template = _weaponSlots.Count > 0 ? _weaponSlots[0].root?.transform : null;
            if (template == null && _weaponRow.childCount > 0)
                template = _weaponRow.GetChild(0);

            _itemSlots.Clear();
            for (var i = 0; i < ItemSlotCount; i++)
            {
                Transform slotTransform;
                if (i < _itemRow.childCount)
                    slotTransform = _itemRow.GetChild(i);
                else if (template != null)
                    slotTransform = Instantiate(template, _itemRow).transform;
                else
                    break;

                var slot = BindPowerupSlot(slotTransform);
                if (slot.root != null)
                {
                    ConfigureSlotForText(slot);
                    _itemSlots.Add(slot);
                }
            }
        }

        void EnsureLogbookRow(Transform root)
        {
            EnsureRightCluster(root);

            _logbookRow = _rightCluster.Find("Logbook_Slot") as RectTransform;
            if (_logbookRow == null)
            {
                var go = new GameObject("Logbook_Slot", typeof(RectTransform));
                _logbookRow = go.GetComponent<RectTransform>();
                _logbookRow.SetParent(_rightCluster, false);
                _logbookRow.SetAsFirstSibling();
            }

            ConfigureSlotRow(_logbookRow);

            var template = _weaponSlots.Count > 0 ? _weaponSlots[0].root?.transform : null;
            if (template == null && _weaponRow.childCount > 0)
                template = _weaponRow.GetChild(0);

            _logbookSlots.Clear();
            for (var i = 0; i < LogbookSlotCount; i++)
            {
                Transform slotTransform;
                if (i < _logbookRow.childCount)
                    slotTransform = _logbookRow.GetChild(i);
                else if (template != null)
                    slotTransform = Instantiate(template, _logbookRow).transform;
                else
                    break;

                var slot = BindPowerupSlot(slotTransform);
                if (slot.root != null)
                {
                    ConfigureSlotForText(slot);
                    _logbookSlots.Add(slot);
                }
            }
        }

        static void ConfigureSlotRow(RectTransform row)
        {
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout == null)
                layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();

            layout.spacing = SlotSpacing;
            layout.childAlignment = TextAnchor.LowerRight;
            layout.reverseArrangement = true;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var fitter = row.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        void ApplyLoadoutLayout(bool force = false)
        {
            if (_weaponRow == null || _itemRow == null || _rightCluster == null) return;

            var canvasRt = _weaponRow.GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
            var width = canvasRt != null ? canvasRt.rect.width : 1920f;
            var cap = CombatExperimentContext.Active.weaponSlotCap;
            if (!force && Mathf.Approximately(width, _lastLayoutWidth) && cap == _lastLayoutWeaponCap)
                return;

            _lastLayoutWidth = width;
            _lastLayoutWeaponCap = cap;

            ConfigureSlotRow(_weaponRow);
            EnsureWeaponRowFitter(_weaponRow);

            var scale = ComputeRowScale(width, cap);
            AnchorRowBottomLeft(_weaponRow, SideInset, BottomInset, scale);
            AnchorRowBottomRight(_rightCluster, SideInset, BottomInset, scale);

            var weaponLayout = _weaponRow.GetComponent<HorizontalLayoutGroup>();
            if (weaponLayout != null)
            {
                weaponLayout.spacing = SlotSpacing;
                weaponLayout.childAlignment = TextAnchor.LowerLeft;
                weaponLayout.reverseArrangement = false;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(_weaponRow);
            if (_logbookRow != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_logbookRow);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_itemRow);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_rightCluster);
        }

        static void EnsureWeaponRowFitter(RectTransform row)
        {
            var fitter = row.GetComponent<ContentSizeFitter>();
            if (fitter == null)
                fitter = row.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        float ComputeRowScale(float screenWidth, int visibleWeaponSlots)
        {
            var weaponCount = Mathf.Max(1, visibleWeaponSlots);
            var weaponSpan = weaponCount * BaseSlotWidth + (weaponCount - 1) * SlotSpacing;
            var logbookSpan = LogbookSlotCount * BaseSlotWidth + (LogbookSlotCount - 1) * SlotSpacing;
            var itemSpan = ItemSlotCount * BaseSlotWidth + (ItemSlotCount - 1) * SlotSpacing;
            var rightSpan = Mathf.Max(logbookSpan, itemSpan);
            var needed = weaponSpan + RowGap + rightSpan;
            var available = screenWidth - SideInset * 2f;
            if (needed <= 0f || available <= 0f)
                return 0.58f;

            return Mathf.Clamp(available / needed, 0.42f, 0.64f);
        }

        static void AnchorRowBottomLeft(RectTransform row, float insetX, float insetY, float scale)
        {
            row.anchorMin = new Vector2(0f, 0f);
            row.anchorMax = new Vector2(0f, 0f);
            row.pivot = new Vector2(0f, 0f);
            row.anchoredPosition = new Vector2(insetX, insetY);
            row.localScale = new Vector3(scale, scale, 1f);
        }

        static void AnchorRowBottomRight(RectTransform row, float insetX, float insetY, float scale)
        {
            row.anchorMin = new Vector2(1f, 0f);
            row.anchorMax = new Vector2(1f, 0f);
            row.pivot = new Vector2(1f, 0f);
            row.anchoredPosition = new Vector2(-insetX, insetY);
            row.localScale = new Vector3(scale, scale, 1f);
        }

        static void ConfigureSlotForText(PowerupSlotView slot)
        {
            if (slot.root == null) return;

            var weapon = slot.root.transform.Find("Weapon");
            if (weapon != null)
                weapon.localScale = Vector3.one * 0.48f;

            if (slot.emptyMarker != null)
            {
                var emptyRt = slot.emptyMarker.transform as RectTransform;
                if (emptyRt != null)
                {
                    emptyRt.localScale = Vector3.one * 0.28f;
                    emptyRt.anchorMin = new Vector2(0.5f, 0.5f);
                    emptyRt.anchorMax = new Vector2(0.5f, 0.5f);
                    emptyRt.pivot = new Vector2(0.5f, 0.5f);
                    emptyRt.anchoredPosition = Vector2.zero;
                    emptyRt.sizeDelta = new Vector2(56f, 56f);
                }

                if (slot.emptyMarker.TryGetComponent<Image>(out var emptyImg))
                    emptyImg.preserveAspect = true;
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

        static string CompactLabel(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var trimmed = value.Trim();
            if (trimmed.Length <= MaxLabelChars) return trimmed;
            return trimmed.Substring(0, MaxLabelChars - 1) + "…";
        }

        static PowerupSlotView BindPowerupSlot(Transform slotRoot)
        {
            if (slotRoot == null) return default;

            var weapon = slotRoot.Find("Weapon");
            var icon = weapon != null ? weapon.Find("Icon")?.GetComponent<Image>() : null;
            var level = weapon != null
                ? weapon.Find("Lvl_txt")?.GetComponent<TextMeshProUGUI>()
                : slotRoot.GetComponentInChildren<TextMeshProUGUI>(true);

            return new PowerupSlotView
            {
                root = slotRoot.gameObject,
                frame = weapon != null ? weapon.GetComponent<Image>() : null,
                icon = icon,
                emptyMarker = slotRoot.Find("Add_Sllot")?.gameObject,
                levelText = level
            };
        }

        static void ApplySlotVisual(PowerupSlotView slot, bool filled, Color accent, Sprite iconSprite = null)
        {
            if (slot.emptyMarker != null)
                slot.emptyMarker.SetActive(!filled);

            if (!filled && slot.emptyMarker != null)
            {
                var emptyRt = slot.emptyMarker.transform as RectTransform;
                if (emptyRt != null && emptyRt.localScale.x > 0.35f)
                    emptyRt.localScale = Vector3.one * 0.28f;
            }

            if (slot.frame != null)
                slot.frame.color = filled ? accent : EmptyFrame;

            if (slot.icon != null)
            {
                slot.icon.sprite = iconSprite;
                slot.icon.enabled = filled && iconSprite != null;
                slot.icon.color = Color.white;
            }

            if (slot.levelText != null)
                slot.levelText.color = filled ? Color.white : new Color(1f, 1f, 1f, 0.35f);
        }

        void Refresh()
        {
            if (_weaponRow == null) return;
            ApplyLoadoutLayout();
            if (_levels == null) _levels = LevelSystem.Instance;
            if (_weapons == null)
                _weapons = V3MultiShipWeaponLoadouts.FindLocalHumanWeaponController()
                           ?? ResolveHumanWeaponController();
            if (_database == null) _database = GameDatabaseLoader.Load();

            var cap = CombatExperimentContext.Active.weaponSlotCap;
            for (var i = 0; i < _weaponSlots.Count; i++)
            {
                var slot = _weaponSlots[i];
                var visible = i < cap;
                slot.root.SetActive(visible);
                if (!visible) continue;

                if (_weapons != null && i < _weapons.Weapons.Count)
                {
                    var weapon = _weapons.Weapons[i];
                    var sprite = V3RunHudIconResolver.GetWeaponIcon(weapon.def.id, weapon.def);
                    ApplySlotVisual(slot, true, WeaponAccent, sprite);
                    if (slot.levelText != null)
                    {
                        var name = CompactLabel(WeaponEvolutionRuntime.GetDisplayName(weapon.def.id, weapon.def.displayName));
                        slot.levelText.text = $"{name}\nLV{weapon.level}";
                    }
                }
                else
                {
                    ApplySlotVisual(slot, false, WeaponAccent);
                    if (slot.levelText != null)
                        slot.levelText.text = string.Empty;
                }
            }

            var items = V3RunItemInventory.Instance?.Items;
            for (var i = 0; i < _itemSlots.Count; i++)
            {
                var slot = _itemSlots[i];
                slot.root.SetActive(true);
                if (items != null && i < items.Count)
                {
                    var id = items[i];
                    var sprite = V3RunHudIconResolver.GetItemIcon(id);
                    ApplySlotVisual(slot, true, ItemAccent, sprite);
                    var stacks = V3RunItemInventory.Instance.GetStackCount(id);
                    if (slot.levelText != null)
                    {
                        var name = CompactLabel(V3ContentDescriptions.GetItemName(id));
                        slot.levelText.text = stacks > 1 ? $"{name}\nx{stacks}" : name;
                    }
                }
                else
                {
                    ApplySlotVisual(slot, false, ItemAccent);
                    if (slot.levelText != null)
                        slot.levelText.text = string.Empty;
                }
            }

            var owned = _levels?.OwnedLogbooks;
            for (var i = 0; i < _logbookSlots.Count; i++)
            {
                var slot = _logbookSlots[i];
                slot.root.SetActive(true);
                if (owned != null && i < owned.Count)
                {
                    var id = owned[i];
                    var def = _database?.GetLogbook(id);
                    ApplySlotVisual(slot, true, LogbookAccent);
                    if (slot.levelText != null)
                    {
                        var name = CompactLabel(def != null ? def.displayName : id);
                        var rank = _levels.GetLogbookLevel(id);
                        slot.levelText.text = $"{name}\nR{rank}";
                    }
                }
                else
                {
                    ApplySlotVisual(slot, false, LogbookAccent);
                    if (slot.levelText != null)
                        slot.levelText.text = string.Empty;
                }
            }
        }

        static WeaponController ResolveHumanWeaponController()
        {
            var player = FindFirstObjectByType<PlayerShipController>();
            var all = FindObjectsByType<WeaponController>(FindObjectsSortMode.None);
            for (var i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                if (all[i].GetComponent<PlayerShipController>() != null)
                    continue;
                return all[i];
            }

            return player != null ? player.GetComponent<WeaponController>() : null;
        }
    }
}
