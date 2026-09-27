using System;
using System.Collections.Generic;
using HighSeas.Core;

namespace RoguePirates.V4.Progression
{
    /// <summary>Adds and removes stat changes by source (items, Logbooks, Captains, powerups, landmarks, fallback cards).</summary>
    public interface IStatModifierSink
    {
        /// <summary>Adds a change to <paramref name="stat"/> (GameEnums StatId) owned by <paramref name="source"/>.</summary>
        void Add(string source, int stat, float additive, float multiplicative = 1f);
        /// <summary>Removes every change <paramref name="source"/> added.</summary>
        void RemoveSource(string source);
        /// <summary>Sets a stat's base value (Captain hull and speed).</summary>
        void SetBase(int stat, float value);
    }

    /// <summary>Read side: a stat's current value.</summary>
    public interface IStatReader
    {
        float Get(int stat);
    }

    /// <summary>
    /// The run's stats (M9 step 9.2): source-tagged changes kept in a ledger and mirrored into the kept shared
    /// <see cref="PlayerStats"/> (the ship, pickups, hull sync and the legacy readers still read it). Values are
    /// PlayerStats' own: (base + Σ additive) × Π multiplicative, so adding changes in V3's order gives V3's numbers.
    /// Stat ids outside 0–20 are ignored (V3's Navigator's Charts pointed at stat 22, which PlayerStats never matched).
    /// </summary>
    public sealed class StatResolver : IStatModifierSink, IStatReader
    {
        readonly Func<PlayerStats> _stats;
        readonly Dictionary<string, List<StatModifier>> _bySource = new Dictionary<string, List<StatModifier>>();

        /// <summary>Raised after a change to a stat (the ship re-reads its speed, the hull re-syncs).</summary>
        public event Action<int> Changed;

        public StatResolver(Func<PlayerStats> stats) => _stats = stats ?? throw new ArgumentNullException(nameof(stats));

        public static bool IsStat(int stat) => stat >= 0 && stat <= (int)StatId.MoveSpeed;

        public void Add(string source, int stat, float additive, float multiplicative = 1f)
        {
            if (!IsStat(stat)) return;
            var stats = _stats();
            if (stats == null) return;
            var modifier = new StatModifier((StatId)stat, additive, multiplicative);
            stats.AddModifier(modifier);
            if (!_bySource.TryGetValue(source ?? string.Empty, out var list)) _bySource[source ?? string.Empty] = list = new List<StatModifier>();
            list.Add(modifier);
            Changed?.Invoke(stat);
        }

        public void RemoveSource(string source)
        {
            if (!_bySource.TryGetValue(source ?? string.Empty, out var list)) return;
            _bySource.Remove(source ?? string.Empty);
            var stats = _stats();
            foreach (var modifier in list)
            {
                stats?.RemoveModifier(modifier); // value-equal changes are interchangeable in the sum and product
                Changed?.Invoke((int)modifier.stat);
            }
        }

        public void SetBase(int stat, float value)
        {
            if (!IsStat(stat)) return;
            _stats()?.SetBase((StatId)stat, value);
            Changed?.Invoke(stat);
        }

        public float Get(int stat) => IsStat(stat) && _stats() != null ? _stats().Get((StatId)stat) : 0f;

        /// <summary>How many changes <paramref name="source"/> holds.</summary>
        public int CountFor(string source) => _bySource.TryGetValue(source ?? string.Empty, out var list) ? list.Count : 0;

        /// <summary>A new run: the ledger forgets everything (the run's PlayerStats is new, as in V3's RunManager.BeginRun).</summary>
        public void ResetForRun() => _bySource.Clear();
    }
}
