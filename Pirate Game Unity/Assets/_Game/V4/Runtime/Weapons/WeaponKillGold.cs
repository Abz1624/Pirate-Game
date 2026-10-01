using System;
using System.Collections.Generic;
using RoguePirates.V4.Config;

namespace RoguePirates.V4.Weapons
{
    /// <summary>
    /// V5 M4b: the extra gold kills by a weapon drop (Cursed Doubloons), at most the weapon's run cap, so a strong build
    /// cannot farm it (economy rule: rewards that scale with run output flatten and cap).
    /// </summary>
    public sealed class WeaponKillGold
    {
        readonly Func<ConfigSetRuntime> _config;
        readonly Dictionary<string, WeaponConfig> _weapons = new Dictionary<string, WeaponConfig>();
        readonly Dictionary<string, int> _paid = new Dictionary<string, int>();

        public WeaponKillGold(Func<ConfigSetRuntime> config) => _config = config;

        public void ResetForRun() => _paid.Clear();

        /// <summary>Gold to add to this kill's drop.</summary>
        public int Bonus(string weaponId)
        {
            var w = Find(weaponId);
            if (w == null || w.goldPerKill <= 0) return 0;
            _paid.TryGetValue(weaponId, out var paid);
            var bonus = Math.Min(w.goldPerKill, Math.Max(0, w.goldPerKillRunCap - paid));
            if (bonus > 0) _paid[weaponId] = paid + bonus;
            return bonus;
        }

        WeaponConfig Find(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId)) return null;
            if (_weapons.TryGetValue(weaponId, out var cached)) return cached;
            WeaponConfig found = null;
            var config = _config?.Invoke();
            if (config != null)
                foreach (var w in config.All<WeaponConfig>())
                    if (w.weaponId == weaponId) { found = w; break; }
            _weapons[weaponId] = found;
            return found;
        }
    }
}
