using System;
using System.Collections.Generic;
using RoguePirates.V4.Foundation.UI;
using RoguePirates.V4.Views;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RoguePirates.V4.Presentation.Hud
{
    /// <summary>
    /// Binds the art team's HUD_SaltWind prefab to the V4 run views (demo): the Captain panel (portrait, name and level,
    /// hull and XP bars), the timer, gold and kills, the objective banner (the current quest line, one pip per step done)
    /// and the weapon row (icons from the icon catalogue, levels). It replaces the authored HUD's profile, timer, counters,
    /// quest list and weapon row, which it keeps hidden; items and Logbooks, the minimap, boost, Captain active, level-up
    /// cards and results stay V4's. The HUD hides while the results screen hides the profile.
    /// </summary>
    public sealed class SaltWindHudPresenter : MonoBehaviour
    {
        static readonly string[] Replaced = { "TImer", "Coin_Count", "Kill_Count", "Profile", "Quest", "Weapon_Slot", "Loadout_Right" };

        IRunViews _views;
        IconCatalog _icons;
        SaltWindHudSettings _settings;
        AuthoredHudPresenter _authored;
        GameObject _hud;
        TextMeshProUGUI _name, _time, _gold, _kills, _rum;
        Image _portrait;
        Slider _hp, _xp;
        readonly List<Image> _markers = new List<Image>();
        readonly List<WeaponSlotView> _weapons = new List<WeaponSlotView>();
        readonly List<string> _quest = new List<string>(3);
        int _lastCaptain = -1;
        readonly List<Image> _items = new List<Image>();

        /// <summary>The art's minimap frame (V4's minimap follows it).</summary>
        public RectTransform ArtMinimap { get; private set; }
        /// <summary>The art's level-up cards (the level-up presenter shows them).</summary>
        public Transform UpgradeSelection { get; private set; }

        sealed class WeaponSlotView
        {
            public GameObject Root;
            public Image Icon;
            public GameObject Empty, Badge, Locked, Selected;
            public TextMeshProUGUI Level;
        }

        public GameObject Hud => _hud;
        /// <summary>Off: the authored HUD shows as before (the V4 HUD PlayMode tests cover those widgets).</summary>
        public static bool Disabled;

        /// <summary>Instantiates the HUD on the authored canvas; null when the settings or prefab are missing.</summary>
        public static SaltWindHudPresenter Install(Canvas authoredCanvas, AuthoredHudPresenter authored, IRunViews views, IconCatalog icons, MinimapPresenter minimap = null)
        {
            var settings = SaltWindHudSettings.Load();
            if (Disabled && authoredCanvas != null) authoredCanvas.transform.Find("HUD_SaltWind")?.gameObject.SetActive(false);
            if (Disabled || settings == null || settings.hudPrefab == null || authoredCanvas == null) return null;
            var p = authoredCanvas.GetComponent<SaltWindHudPresenter>();
            if (p == null) p = authoredCanvas.gameObject.AddComponent<SaltWindHudPresenter>();
            p.Bind(settings, authored, views, icons);
            return p;
        }

        void Bind(SaltWindHudSettings settings, AuthoredHudPresenter authored, IRunViews views, IconCatalog icons)
        {
            _settings = settings;
            _authored = authored;
            _views = views;
            _icons = icons;
            // The HUD is placed in the run scene (Tools > Rogue Pirates > Demo > Build Demo Scenes) so artists can tweak it;
            // a scene without it gets one from the prefab.
            if (_hud == null) _hud = transform.Find("HUD_SaltWind")?.gameObject;
            if (_hud != null && _weapons.Count == 0) BindWidgets(_hud.transform);
            if (_hud == null)
            {
                _hud = Instantiate(settings.hudPrefab, transform, false);
                _hud.name = "HUD_SaltWind";
                var rt = (RectTransform)_hud.transform;
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one;
                _hud.transform.SetAsFirstSibling(); // under the boost meter and the Captain active
                BindWidgets(_hud.transform);
            }
            _lastCaptain = -1;
            if (_authored != null && _hud != null) _authored.WeaponRowForBoost = Find(_hud.transform, "WeaponLoadout") as RectTransform;
        }

        void BindWidgets(Transform root)
        {
            _hud = root.gameObject;
            UpgradeSelection = Find(root, "UpgradeSelection");
            // The art's minimap is a frame: V4's minimap draws the map inside it (it follows ArtMinimap).
            ArtMinimap = Find(root, "Minimap") as RectTransform;
            SetActive(ArtMinimap, "MapContent", false);
            SetActive(ArtMinimap, "PlayerMarker", false);
            // The HUD is display only (the level-up cards keep their buttons).
            foreach (var g in root.GetComponentsInChildren<Graphic>(true))
                if (UpgradeSelection == null || !g.transform.IsChildOf(UpgradeSelection)) g.raycastTarget = false;
            foreach (var s in root.GetComponentsInChildren<Selectable>(true))
                if (UpgradeSelection == null || !s.transform.IsChildOf(UpgradeSelection)) s.interactable = false;

            var itemBar = Find(root, "ItemBar");
            if (itemBar != null)
                for (var i = 0; i < itemBar.childCount; i++)
                {
                    var icon = Find(itemBar.GetChild(i), "Icon")?.GetComponent<Image>();
                    if (icon != null) _items.Add(icon);
                }

            var panel = Find(root, "PlayerPanel");
            _name = Find(panel, "Name")?.GetComponent<TextMeshProUGUI>();
            _portrait = Find(panel, "Portrait")?.GetComponent<Image>();
            _hp = Find(panel, "HealthBar")?.GetComponent<Slider>();
            _xp = Find(panel, "ExperienceBar")?.GetComponent<Slider>();
            foreach (var s in new[] { _hp, _xp }) if (s != null) { s.minValue = 0f; s.maxValue = 1f; s.wholeNumbers = false; s.transition = Selectable.Transition.None; }
            _time = Find(Find(root, "MatchTimer"), "Time")?.GetComponent<TextMeshProUGUI>();
            var currency = Find(root, "CurrencyBar");
            _gold = Find(Find(currency, "Gold"), "Amount")?.GetComponent<TextMeshProUGUI>();
            _kills = Find(Find(currency, "Kills"), "Amount")?.GetComponent<TextMeshProUGUI>();
            _rum = Find(Find(currency, "Rum"), "Amount")?.GetComponent<TextMeshProUGUI>();
            var title = Find(root, "MatchTitle");
            var markers = Find(title, "KillMarkers");
            if (markers != null)
                for (var i = 0; i < markers.childCount; i++)
                {
                    var img = markers.GetChild(i).GetComponent<Image>();
                    if (img != null) _markers.Add(img);
                }

            var loadout = Find(root, "WeaponLoadout");
            if (loadout != null)
            {
                var slots = new List<Transform>();
                for (var i = 0; i < loadout.childCount; i++) slots.Add(loadout.GetChild(i));
                slots.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                foreach (var t in slots)
                {
                    t.SetSiblingIndex(slots.IndexOf(t));
                    var content = Find(t, "Content");
                    _weapons.Add(new WeaponSlotView
                    {
                        Root = t.gameObject,
                        Icon = Find(content, "Icon")?.GetComponent<Image>(),
                        Empty = Find(content, "EmptyLabel")?.gameObject,
                        Badge = Find(content, "LevelBadge")?.gameObject,
                        Level = Find(Find(content, "LevelBadge"), "Level")?.GetComponent<TextMeshProUGUI>(),
                        Locked = Find(t, "LockedOverlay")?.gameObject,
                        Selected = Find(t, "SelectedFrame")?.gameObject,
                    });
                }
            }
        }

        void LateUpdate()
        {
            if (_hud == null || _views == null) return;
            // The replaced authored widgets stay hidden (the results screen re-shows the profile on retry).
            for (var i = 0; i < Replaced.Length; i++)
            {
                var t = transform.Find(Replaced[i]);
                if (t != null && t.gameObject.activeSelf) t.gameObject.SetActive(false);
            }
            var show = _authored == null || _authored.ProfileVisible;
            if (_hud.activeSelf != show) _hud.SetActive(show);
            if (!show) return;

            var run = _views.Run;
            if (run != null && run.Exists)
            {
                var elapsed = run.ElapsedSeconds;
                if (_time != null)
                {
                    _time.text = $"{Mathf.FloorToInt(elapsed / 60f):00}:{Mathf.FloorToInt(elapsed % 60f):00}";
                    _time.color = run.IsOvertime ? RunHudTheme.TimerOvertime : Color.white;
                }
                if (_gold != null) _gold.text = run.Gold.ToString("N0");
                if (_kills != null) _kills.text = run.Kills.ToString("N0");
                // Rum: the player's treasury (this run's Rum is settled on the results screen).
                var app = RoguePirates.V4.Foundation.Core.AppRoot.Instance;
                if (_rum != null) _rum.text = (app != null && app.Save != null ? app.Save.Meta.marks : 0).ToString("N0");
            }

            var captain = _views.Captain;
            var progression = _views.Progression;
            if (captain != null && _name != null)
            {
                var level = progression != null && progression.Exists ? Mathf.Max(1, progression.Level) : 1;
                _name.text = $"{captain.DisplayTitle}  <size=80%>Lv {level}</size>";
                if (_portrait != null && captain.CaptainId != _lastCaptain)
                {
                    _lastCaptain = captain.CaptainId;
                    var sprite = _settings.PortraitOf(captain.CaptainId);
                    if (sprite != null) { _portrait.sprite = sprite; _portrait.preserveAspect = true; }
                }
            }
            if (progression != null && progression.Exists && _xp != null)
                _xp.value = Mathf.Clamp01(progression.CurrentXp / Mathf.Max(1f, progression.XpToNext));
            var ship = _views.Ship;
            if (ship != null && ship.Exists && _hp != null) _hp.value = Mathf.Clamp01(ship.Hp / Mathf.Max(1f, ship.MaxHp));

            RefreshObjective();
            RefreshWeapons();
            RefreshItems();
        }

        void RefreshItems()
        {
            var loadout = _views.Loadout;
            if (loadout == null) return;
            for (var i = 0; i < _items.Count; i++)
            {
                var sprite = i < loadout.ItemCount && _icons != null ? _icons.Item(loadout.ItemId(i)) : null;
                var slot = _items[i].transform.parent.gameObject;
                if (slot.activeSelf != (sprite != null)) slot.SetActive(sprite != null);
                if (sprite != null && _items[i].sprite != sprite) { _items[i].sprite = sprite; _items[i].preserveAspect = true; }
            }
        }

        void RefreshObjective()
        {
            var stage = _views.Stage;
            if (stage == null) return;
            QuestLines.Build(stage.Beacon, stage.BossSummoned, stage.BossDefeated, _quest);
            var done = stage.BossDefeated ? 3 : stage.BossSummoned ? 2 : stage.Beacon == BeaconReveal.Discovered ? 1 : 0;
            for (var i = 0; i < _markers.Count; i++)
            {
                var sprite = i < done ? _settings.markerDone : _settings.markerOpen;
                if (sprite != null && _markers[i].sprite != sprite) _markers[i].sprite = sprite;
            }
        }

        void RefreshWeapons()
        {
            var loadout = _views.Loadout;
            if (loadout == null) return;
            var cap = loadout.WeaponSlotCap;
            for (var i = 0; i < _weapons.Count; i++)
            {
                var w = _weapons[i];
                var open = i < cap;
                var filled = open && i < loadout.WeaponCount;
                if (w.Locked != null) w.Locked.SetActive(!open);
                if (w.Selected != null) w.Selected.SetActive(false);
                if (w.Empty != null) w.Empty.SetActive(open && !filled);
                if (w.Badge != null) w.Badge.SetActive(filled);
                if (w.Icon != null)
                {
                    var sprite = filled && _icons != null ? _icons.Weapon(loadout.WeaponId(i)) : null;
                    w.Icon.enabled = sprite != null;
                    if (sprite != null && w.Icon.sprite != sprite) { w.Icon.sprite = sprite; w.Icon.preserveAspect = true; }
                }
                if (filled && w.Level != null) w.Level.text = loadout.WeaponLevel(i).ToString();
            }
        }

        static Transform Find(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (var i = 0; i < root.childCount; i++)
            {
                var found = Find(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        static void SetActive(Transform root, string name, bool active)
        {
            var t = Find(root, name);
            if (t != null) t.gameObject.SetActive(active);
        }
    }
}
