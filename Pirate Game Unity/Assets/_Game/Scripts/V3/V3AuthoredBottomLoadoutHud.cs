using System.Collections.Generic;
using HighSeas.Combat;
using HighSeas.Core;
using HighSeas.Data;
using HighSeas.Player;
using HighSeas.UI;
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
        const int ItemSlotCount = 4;
        const float BottomInset = 22f;

        readonly List<PowerupSlotView> _weaponSlots = new();
        readonly List<PowerupSlotView> _itemSlots = new();

        RectTransform _weaponRow;
        RectTransform _itemRow;
        LevelSystem _levels;
        WeaponController _weapons;
        GameDatabase _database;

        struct PowerupSlotView
        {
            public GameObject root;
            public Image icon;
            public TextMeshProUGUI levelText;
        }

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
            if (_weaponRow != null) return;

            var root = transform;
            _weaponRow = root.Find("Weapon_Slot") as RectTransform;
            if (_weaponRow == null) return;

            _weaponRow.gameObject.SetActive(true);
            ApplyBottomAnchors(_weaponRow, BottomInset);

            _weaponSlots.Clear();
            for (var i = 0; i < _weaponRow.childCount; i++)
            {
                var slot = BindPowerupSlot(_weaponRow.GetChild(i));
                if (slot.root != null)
                    _weaponSlots.Add(slot);
            }

            EnsureItemRow(root);
            _levels = LevelSystem.Instance;
            _weapons = ResolveHumanWeaponController();
            _database = GameDatabaseLoader.Load();
        }

        void EnsureItemRow(Transform root)
        {
            _itemRow = root.Find("Item_Slot") as RectTransform;
            if (_itemRow == null)
            {
                var go = new GameObject("Item_Slot", typeof(RectTransform));
                _itemRow = go.GetComponent<RectTransform>();
                _itemRow.SetParent(root, false);
                var layout = go.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 14.58f;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = false;
                layout.childControlHeight = false;
            }

            ApplyBottomAnchors(_itemRow, BottomInset);
            var weaponWidth = _weaponRow != null ? _weaponRow.rect.width : 640f;
            _itemRow.anchoredPosition = new Vector2(weaponWidth * 0.5f + 48f, BottomInset);

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
                    _itemSlots.Add(slot);
            }
        }

        static void ApplyBottomAnchors(RectTransform row, float inset)
        {
            row.anchorMin = new Vector2(0.5f, 0f);
            row.anchorMax = new Vector2(0.5f, 0f);
            row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, inset);
        }

        static PowerupSlotView BindPowerupSlot(Transform slotRoot)
        {
            if (slotRoot == null) return default;

            var weapon = slotRoot.Find("Weapon");
            var icon = weapon != null ? weapon.GetComponent<Image>() : null;
            var level = weapon != null
                ? weapon.Find("Lvl_txt")?.GetComponent<TextMeshProUGUI>()
                : slotRoot.GetComponentInChildren<TextMeshProUGUI>(true);

            return new PowerupSlotView
            {
                root = slotRoot.gameObject,
                icon = icon,
                levelText = level
            };
        }

        void Refresh()
        {
            if (_weaponRow == null) return;
            if (_levels == null) _levels = LevelSystem.Instance;
            if (_weapons == null) _weapons = ResolveHumanWeaponController();
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
                    ApplyIcon(slot, sprite, true);
                    if (slot.levelText != null)
                        slot.levelText.text = $"LVL-{weapon.level}";
                }
                else
                {
                    ApplyIcon(slot, null, false);
                    if (slot.levelText != null)
                        slot.levelText.text = string.Empty;
                }
            }

            var items = V3RunItemInventory.Instance?.Items;
            for (var i = 0; i < _itemSlots.Count; i++)
            {
                var slot = _itemSlots[i];
                if (items != null && i < items.Count)
                {
                    var id = items[i];
                    var sprite = V3RunHudIconResolver.GetItemIcon(id);
                    ApplyIcon(slot, sprite, true);
                    var stacks = V3RunItemInventory.Instance.GetStackCount(id);
                    if (slot.levelText != null)
                        slot.levelText.text = stacks > 1 ? $"x{stacks}" : string.Empty;
                }
                else
                {
                    ApplyIcon(slot, null, false);
                    if (slot.levelText != null)
                        slot.levelText.text = string.Empty;
                }
            }
        }

        static void ApplyIcon(PowerupSlotView slot, Sprite sprite, bool filled)
        {
            if (slot.icon == null) return;
            slot.icon.sprite = sprite;
            slot.icon.enabled = filled && sprite != null;
            slot.icon.color = sprite != null ? Color.white : new Color(1f, 1f, 1f, 0.25f);
            slot.icon.preserveAspect = true;
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
