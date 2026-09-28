using System;
using System.Collections.Generic;
using HighSeas.Core;
using RoguePirates.V4.Config;
using RoguePirates.V4.Events;

namespace RoguePirates.V4.Rewards
{
    /// <summary>The Rum treasury in the shared save (the saved field stays <c>marks</c>; one receipt per run id).</summary>
    public sealed class SaveRumBank : IRumBank
    {
        readonly Func<SaveService> _save;
        public SaveRumBank(Func<SaveService> save) { _save = save; }
        public bool TrySettle(string runId, int rum) => _save() != null && _save().TrySettleRun(runId, rum);
        public int Treasury => _save()?.Meta.marks ?? 0;
    }

    /// <summary>
    /// Rum unlocks (M12, port of SaltwindMarksUnlockLogic): the goal is the next weapon in <see cref="UnlockConfig.weaponUnlocks"/>
    /// not owned; purchases cost the weapon's or Captain's Rum (UnlockConfig / CaptainConfig) and go through the shared save.
    /// Selecting a goal never spends Rum.
    /// </summary>
    public sealed class UnlockService
    {
        readonly Func<SaveService> _save;
        readonly Func<ConfigSetRuntime> _config;

        public UnlockService(Func<SaveService> save, Func<ConfigSetRuntime> config)
        {
            _save = save;
            _config = config;
        }

        UnlockConfig Unlocks => _config()?.Single<UnlockConfig>();
        string Currency => _config()?.Single<RewardConfig>()?.currencyName ?? "Rum";

        /// <summary>Rum cost of a weapon or Captain unlock, or -1 if it is not a Rum unlock.</summary>
        public int CostOf(string contentId)
        {
            if (string.IsNullOrEmpty(contentId)) return -1;
            var unlocks = Unlocks;
            if (unlocks?.weaponUnlocks != null)
                foreach (var u in unlocks.weaponUnlocks)
                    if (u != null && u.weaponId == contentId) return u.rumCost;
            var config = _config();
            if (config != null)
                foreach (var c in config.All<CaptainConfig>())
                    if (c != null && c.captainKey == contentId && c.rumCost > 0) return c.rumCost;
            return -1;
        }

        public string DisplayName(string contentId)
        {
            var config = _config();
            if (config != null)
            {
                foreach (var w in config.All<WeaponConfig>())
                    if (w != null && w.weaponId == contentId && !string.IsNullOrEmpty(w.displayName)) return w.displayName;
                foreach (var c in config.All<CaptainConfig>())
                    if (c != null && c.captainKey == contentId && !string.IsNullOrEmpty(c.displayName)) return c.displayName;
            }
            return contentId;
        }

        public string NextLockedWeapon()
        {
            var save = _save();
            var unlocks = Unlocks;
            if (save == null || unlocks?.weaponUnlocks == null) return null;
            foreach (var u in unlocks.weaponUnlocks)
                if (u != null && !string.IsNullOrEmpty(u.weaponId) && !save.HasEffectiveContentOwned(u.weaponId)) return u.weaponId;
            return null;
        }

        /// <summary>Sets the next locked weapon as the goal; returns the message to show.</summary>
        public string SelectNextGoal()
        {
            var save = _save();
            if (save == null) return null;
            var next = NextLockedWeapon();
            if (string.IsNullOrEmpty(next)) return $"All {Currency} weapons owned.";
            save.SetSelectedUnlockGoal(next);
            return $"Unlock goal: {DisplayName(next)}";
        }

        /// <summary>Buys the selected goal; returns the message to show.</summary>
        public string PurchaseGoal()
        {
            var save = _save();
            if (save == null) return null;
            var goal = save.Meta.selectedUnlockGoal;
            if (string.IsNullOrEmpty(goal)) return "Select an unlock goal first.";
            var cost = CostOf(goal);
            if (cost >= 0 && !save.HasEffectiveContentOwned(goal) && save.TryPurchaseMarksUnlock(goal, cost))
                return $"Unlocked {DisplayName(goal)}!";
            return $"Not enough {Currency} or already owned.";
        }

        /// <summary>The goal line on the results screen (V3's wording, in Rum).</summary>
        public string GoalProgress()
        {
            var save = _save();
            if (save == null) return string.Empty;
            var goal = save.Meta.selectedUnlockGoal;
            if (string.IsNullOrEmpty(goal)) return "\nUnlock goal: not selected (choose one in menu or results).\n";
            if (save.HasEffectiveContentOwned(goal)) return $"\nUnlock goal: {DisplayName(goal)} — owned.\n";
            var cost = CostOf(goal);
            if (cost < 0) return $"\nUnlock goal: {DisplayName(goal)} — not a {Currency} unlock.\n";
            return $"\nUnlock goal: {DisplayName(goal)} — {save.Meta.marks} / {cost} {Currency}\n";
        }
    }

    /// <summary>
    /// Damage share per weapon for the results screen (M12, replaces reading WeaponDamageLedger's static snapshot): the
    /// damage of every recorded weapon hit, by the same ledger id V3's results used (the weapon module records every hit,
    /// including orbit, cone, trail and area weapons whose enemy hits carry no weapon tag).
    /// </summary>
    public sealed class DamageShareTracker : IDisposable
    {
        readonly Dictionary<string, float> _damage = new Dictionary<string, float>();

        public void Reset() => _damage.Clear();
        public IReadOnlyDictionary<string, float> Damage => _damage;

        public void Record(string weaponId, float damage)
        {
            if (string.IsNullOrEmpty(weaponId) || !(damage > 0f)) return;
            _damage[weaponId] = _damage.TryGetValue(weaponId, out var d) ? d + damage : damage;
        }

        /// <summary>Weapons by share, largest first: (weapon id, percent).</summary>
        public void Shares(List<KeyValuePair<string, float>> into)
        {
            into.Clear();
            var total = 0f;
            foreach (var p in _damage) total += p.Value;
            if (total <= 0f) return;
            foreach (var p in _damage) into.Add(new KeyValuePair<string, float>(p.Key, p.Value / total * 100f));
            into.Sort((a, b) => b.Value.CompareTo(a.Value));
        }

        public void Dispose() { }
    }
}
