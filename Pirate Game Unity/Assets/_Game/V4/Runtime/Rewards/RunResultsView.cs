using System;
using System.Collections.Generic;
using System.Text;
using RoguePirates.V4.Views;

namespace RoguePirates.V4.Rewards
{
    /// <summary>
    /// The results screen's data (M12, replaces the legacy results view): the Rum breakdown from the reward module, the goal
    /// from the unlock service and the damage share from Hit events. Run facts that still live in the kept run manager
    /// (profile footer, gold, final build) are supplied by the composition root.
    /// </summary>
    public sealed class RunResultsView : IResultsView
    {
        readonly IRumView _rum;
        readonly UnlockService _unlocks;
        readonly DamageShareTracker _damage;
        readonly List<KeyValuePair<string, float>> _shares = new List<KeyValuePair<string, float>>();

        public Func<bool> Objective;
        public Func<string> Footer;
        public Func<int> Earned, Spent, Remaining;
        public Action<List<NamedValue>> FinalBuild;
        /// <summary>Weapon display names (V3's name table until the weapon configs carry every ledger id).</summary>
        public Func<string, string> WeaponName;

        public RunResultsView(IRumView rum, UnlockService unlocks, DamageShareTracker damage)
        {
            _rum = rum;
            _unlocks = unlocks;
            _damage = damage;
        }

        public bool ObjectiveSecured => Objective != null && Objective();
        public bool EndedByDeath => _rum != null && _rum.EndedByDeath;
        public string ProfileFooter => Footer != null ? Footer() : string.Empty;
        public int GoldEarned => Earned != null ? Earned() : 0;
        public int GoldSpent => Spent != null ? Spent() : 0;
        public int GoldRemaining => Remaining != null ? Remaining() : 0;
        public bool HasRewardSection => _rum != null;

        /// <summary>V3's breakdown layout, one line per enabled row, in Rum; null until the run is settled.</summary>
        public string RewardSection => _rum == null || !_rum.Settled ? null : Format(_rum.SettledRows, _rum.SettledTotal, _rum.Treasury, _rum.CurrencyName);

        public static string Format(IReadOnlyList<RumRowResult> rows, int total, int treasury, string currency)
        {
            if (total <= 0) return $"\n{currency}: none earned this run.\n";
            var sb = new StringBuilder();
            sb.Append('\n').Append(currency).Append(" earned this run: ").Append(total).Append('\n');
            foreach (var r in rows) sb.Append("  ").Append(r.Label).Append(": ").Append(r.Rum).Append('\n');
            sb.Append("Treasury ").Append(currency).Append(": ").Append(treasury).Append('\n');
            return sb.ToString();
        }

        public string GoalProgress => _unlocks != null ? _unlocks.GoalProgress() : string.Empty;

        public void CollectFinalBuild(List<NamedValue> into)
        {
            into.Clear();
            FinalBuild?.Invoke(into);
        }

        public void CollectContributions(List<NamedValue> into)
        {
            into.Clear();
            if (_damage == null) return;
            _damage.Shares(_shares);
            foreach (var s in _shares) into.Add(new NamedValue { name = WeaponName != null ? WeaponName(s.Key) : _unlocks != null ? _unlocks.DisplayName(s.Key) : s.Key, value = s.Value });
        }
    }
}
