using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;
using RoguePirates.V4.Progression;
using RoguePirates.V4.Rewards;
using RoguePirates.V4.Weapons;
using UnityEngine;

namespace RoguePirates.V4.Views
{
    /// <summary>
    /// The run's Captain as the HUD shows it (M14.11; was LegacyCaptainView, which read V3's saved selection and
    /// V3CaptainCatalog titles): the Captain module's config — its V3 id for the portrait, its title, the active's name,
    /// cooldown, uses and theme colour.
    /// </summary>
    public sealed class CaptainView : ICaptainView
    {
        readonly CaptainModule _captain;
        readonly Func<bool> _activesOn;

        public CaptainView(CaptainModule captain, Func<bool> activesOn = null)
        {
            _captain = captain;
            _activesOn = activesOn;
        }

        CaptainConfig Config => _captain?.Config;
        public int CaptainId => Config != null ? Config.legacyId : 0;
        public string DisplayTitle => Config != null && !string.IsNullOrEmpty(Config.title) ? Config.title : "Captain Rook";
        public bool ActiveAvailable => Config?.active != null && (_activesOn == null || _activesOn());
        public string ActiveName => Config?.active?.displayName ?? string.Empty;
        public float ActiveCooldownRemaining => _captain != null ? _captain.CooldownRemaining : 0f;
        public float ActiveCooldownDuration => _captain != null ? _captain.CooldownDuration : 0f;
        public Func<string> KeyHint;
        public string ActiveKeyHint => KeyHint != null ? KeyHint() : "SPACE";
        public int ActiveUses => _captain != null ? _captain.ActiveUses : 0;
        public string ActiveSummary => Config?.activeSummary ?? string.Empty;
        public float ActiveEffectRemaining => _captain != null ? _captain.ActiveEffectRemaining : 0f;
        public float ActiveEffectDuration => _captain != null ? _captain.ActiveEffectDuration : 0f;
        public Color ThemeColor => Config != null ? Config.themeColor : Color.white;
    }

    /// <summary>
    /// The loadout as the HUD shows it (M14.11; was LegacyLoadoutView): weapons from the Weapon Manager's slots (names
    /// from the weapon and evolution configs), items from the item module with their names from the item configs (were
    /// V3's item catalog names).
    /// </summary>
    public sealed class LoadoutView : ILoadoutView
    {
        readonly IWeaponManager _weapons;
        readonly IItemInventory _items;
        readonly Func<int> _slotCap;
        readonly Func<string, ItemConfig> _itemConfig;

        public LoadoutView(IWeaponManager weapons, IItemInventory items, Func<int> slotCap, Func<string, ItemConfig> itemConfig)
        {
            _weapons = weapons;
            _items = items;
            _slotCap = slotCap;
            _itemConfig = itemConfig;
        }

        IReadOnlyList<WeaponSlot> Slots => _weapons != null ? _weapons.Slots : null;

        public int WeaponSlotCap => _slotCap != null ? _slotCap() : 6;
        public int WeaponCount => Slots != null ? Slots.Count : 0;
        public string WeaponId(int index) => Slots[index].Id;

        public string WeaponName(int index)
        {
            var slot = Slots[index];
            if (slot.Evolution != null && !string.IsNullOrEmpty(slot.Evolution.displayName)) return slot.Evolution.displayName;
            return WeaponIconName(index);
        }

        public string WeaponIconName(int index)
        {
            var config = Slots[index].Config;
            return config != null && !string.IsNullOrEmpty(config.displayName) ? config.displayName : Slots[index].Id;
        }

        public int WeaponLevel(int index) => Slots[index].Level;
        public int ItemCount => _items != null ? _items.Items.Count : 0;
        public string ItemId(int index) => _items.Items[index];

        public string ItemName(int index)
        {
            var id = ItemId(index);
            var item = _itemConfig?.Invoke(id);
            return item != null && !string.IsNullOrEmpty(item.displayName) ? item.displayName : id;
        }

        public int ItemStacks(int index) => _items.StackCount(ItemId(index));
    }

    /// <summary>What the run's view set is built from (the kept run manager's facts come in as delegates).</summary>
    public struct RunViewSources
    {
        public IRunStatusView Run;
        public IShipView Ship;
        public IToastSink Toasts;
        public IHudCommands Commands;
        public IEnemyView Enemies;
        public IWeaponManager Weapons;
        public CaptainModule Captain;
        public Func<bool> ActivesOn;
        public Func<string> ActiveKeyHint;
        public ProgressionModule Progression;
        public Func<string, LogbookConfig> Logbook;
        public IItemInventory Items;
        public Func<string, ItemConfig> Item;
        public Func<int> WeaponSlotCap;
        public ConfigSetRuntime Config;
        public IRumView Rum;
        public UnlockService Unlocks;
        public DamageShareTracker Damage;
        public Func<bool> ObjectiveSecured;
        public Func<string> ResultsFooter;
        public Func<int> GoldEarned, GoldSpent, GoldRemaining;
    }

    /// <summary>Builds the run's view set (M14.11; was LegacyViewFactory over the legacy hubs).</summary>
    public static class RunViewsFactory
    {
        public static RunViews Create(RunViewSources s)
        {
            var config = s.Config;
            string WeaponName(string id)
            {
                if (config != null)
                    foreach (var w in config.All<WeaponConfig>())
                        if (w.weaponId == id && !string.IsNullOrEmpty(w.displayName)) return w.displayName;
                return id;
            }
            var progression = s.Progression;
            var results = new RunResultsView(s.Rum, s.Unlocks, s.Damage)
            {
                Objective = s.ObjectiveSecured,
                Footer = s.ResultsFooter,
                Earned = s.GoldEarned,
                Spent = s.GoldSpent,
                Remaining = s.GoldRemaining,
                WeaponName = WeaponName, // M14.11: the weapon configs' names (were V3WeaponDisplayNames)
                FinalBuild = into =>
                {
                    if (progression == null) return;
                    for (var i = 0; i < progression.OwnedWeapons.Count; i++)
                    {
                        var id = progression.OwnedWeapons[i];
                        into.Add(new NamedValue { name = WeaponName(id), value = progression.WeaponLevel(id) });
                    }
                },
            };
            return new RunViews
            {
                Run = s.Run,
                Ship = s.Ship,
                Captain = new CaptainView(s.Captain, s.ActivesOn) { KeyHint = s.ActiveKeyHint },
                Progression = progression != null ? new ProgressionView(progression, s.Logbook) : null,
                Loadout = new LoadoutView(s.Weapons, s.Items, s.WeaponSlotCap, s.Item),
                Enemies = s.Enemies,
                Results = results,
                WeaponAim = s.Weapons is WeaponManager manager ? new WeaponAimView(manager.Combat) : null,
                Toasts = s.Toasts,
                Commands = s.Commands,
            };
        }
    }
}
