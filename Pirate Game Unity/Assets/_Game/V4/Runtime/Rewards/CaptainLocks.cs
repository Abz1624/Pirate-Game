using System;
using System.Collections.Generic;
using System.Linq;
using RoguePirates.V4.Foundation.Core;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.Rewards
{
    /// <summary>Whether a Captain can sail, and what stands in the way.</summary>
    public readonly struct CaptainLockState
    {
        public readonly bool Locked;
        /// <summary>What unlocks it, for the menu (empty when unlocked).</summary>
        public readonly string Reason;
        /// <summary>Rum to buy it (0 = not a Rum unlock, or already owned).</summary>
        public readonly int RumCost;
        /// <summary>Only the Rum stands in the way and the treasury holds enough.</summary>
        public readonly bool CanBuy;

        public CaptainLockState(bool locked, string reason, int rumCost, bool canBuy)
        {
            Locked = locked;
            Reason = reason ?? string.Empty;
            RumCost = rumCost;
            CanBuy = canBuy;
        }

        public static CaptainLockState Open => new CaptainLockState(false, string.Empty, 0, false);
    }

    /// <summary>
    /// Captain locks for the menu (M14.13; the V4 menu enforces the adopted locks decided at the M9 gate). With
    /// <see cref="CaptainLockRule.CaptainConfig"/> (default): Rum Captains until bought (Hank 100, Sparky 140), Ember and
    /// Roxie until their weapon is owned, Richie until 5,000 gold has been earned across voyages. With
    /// <see cref="CaptainLockRule.V3Ownership"/> (the v3-parity preset): V3CaptainCatalog.IsLocked — Hank until the save
    /// owns "brine", Sparky until it owns "flint" or the First Conquest is done, the rest open. V3's playtest bypass opens
    /// every Captain under both rules.
    /// </summary>
    public sealed class CaptainLocks
    {
        readonly Func<ConfigSetRuntime> _config;
        readonly Func<SaveService> _save;
        readonly Func<bool> _firstConquestDone;
        readonly Func<int> _lifetimeGold;
        readonly Func<bool> _bypass;

        public CaptainLocks(Func<ConfigSetRuntime> config, Func<SaveService> save, Func<bool> firstConquestDone, Func<int> lifetimeGold, Func<bool> bypass = null)
        {
            _config = config;
            _save = save;
            _firstConquestDone = firstConquestDone;
            _lifetimeGold = lifetimeGold;
            _bypass = bypass;
        }

        UnlockConfig Unlocks => _config?.Invoke()?.Single<UnlockConfig>();
        string Currency => _config?.Invoke()?.Single<RewardConfig>()?.currencyName ?? "Rum";

        /// <summary>The Captains in menu order.</summary>
        public IReadOnlyList<CaptainConfig> MenuOrder() =>
            (_config?.Invoke()?.All<CaptainConfig>() ?? Enumerable.Empty<CaptainConfig>()).Where(c => c != null).OrderBy(c => c.menuOrder).ToList();

        public CaptainLockState StateOf(CaptainConfig captain)
        {
            if (captain == null) return new CaptainLockState(true, "Unknown Captain", 0, false);
            if (_bypass != null && _bypass()) return CaptainLockState.Open;
            var save = _save?.Invoke();
            var rule = Unlocks != null ? Unlocks.captainLockRule : CaptainLockRule.CaptainConfig;
            if (rule == CaptainLockRule.V3Ownership)
            {
                if (string.IsNullOrEmpty(captain.v3OwnershipKey)) return CaptainLockState.Open;
                if (save != null && save.HasEffectiveContentOwned(captain.v3OwnershipKey)) return CaptainLockState.Open;
                if (captain.v3UnlockedByFirstConquest && _firstConquestDone != null && _firstConquestDone()) return CaptainLockState.Open;
                return new CaptainLockState(true, "Locked", 0, false);
            }

            var reasons = new List<string>();
            var prerequisiteMet = true;
            if (RoguePirates.V4.Foundation.Combat.WeaponFeatureFlags.IsWeaponEnabled(captain.prerequisiteWeaponId) && !WeaponOwned(captain.prerequisiteWeaponId, save))
            {
                prerequisiteMet = false;
                reasons.Add($"Unlock {WeaponName(captain.prerequisiteWeaponId)} first");
            }
            var goldNeeded = captain.unlockLifetimeGold;
            if (goldNeeded > 0)
            {
                var have = _lifetimeGold != null ? _lifetimeGold() : 0;
                if (have < goldNeeded)
                {
                    prerequisiteMet = false;
                    reasons.Add($"Earn {goldNeeded:N0} gold across voyages ({have:N0})");
                }
            }
            var rumCost = 0;
            if (captain.rumCost > 0 && !(save != null && save.HasEffectiveContentOwned(captain.captainKey)))
            {
                rumCost = captain.rumCost;
                reasons.Add($"{rumCost} {Currency}");
            }
            if (reasons.Count == 0) return CaptainLockState.Open;
            var treasury = save != null ? save.Meta.marks : 0;
            var canBuy = rumCost > 0 && prerequisiteMet && treasury >= rumCost;
            return new CaptainLockState(true, string.Join(" · ", reasons), rumCost, canBuy);
        }

        public bool IsLocked(CaptainConfig captain) => StateOf(captain).Locked;

        bool WeaponOwned(string weaponId, SaveService save)
        {
            var starters = Unlocks?.starterWeaponIds;
            if (starters != null && Array.IndexOf(starters, weaponId) >= 0) return true;
            return save != null && save.HasEffectiveContentOwned(weaponId);
        }

        string WeaponName(string weaponId)
        {
            var config = _config?.Invoke();
            if (config != null)
                foreach (var w in config.All<WeaponConfig>())
                    if (w != null && w.weaponId == weaponId && !string.IsNullOrEmpty(w.displayName)) return w.displayName;
            return weaponId;
        }
    }
}
